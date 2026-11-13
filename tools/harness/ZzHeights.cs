using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.Persistence;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// ⛔ D735's MEASUREMENT — `heights.md §8`: what real heights cost. Kept in tools/harness/, COPIED into
// tests/Bclone.Sim.Tests/ to run and DELETED from there before any commit (D420).
//
// Two arms, run in one build (the heights are a config key and a map layer, so no second worktree is needed):
//
// 1. Joe's own villages, on COPIES of his saves. A save from before heights loads flat by design (`heights.md §9`), so the
//    hilly arm gives the copy its hills first — the Heights stage's own code (`MapGenerator.RaiseHeights`) over the save's
//    own river and founding site, seeded from its own seed — writes them into the copy's map, and opens that:
//      ZZ_SAVES=<folder> [ZZ_YEARS=20] [ZZ_HILLS=100] dotnet test tests/Bclone.Sim.Tests -c Release
//        --filter "FullyQualifiedName~ZzHeights.OnHisSaves" --logger "console;verbosity=detailed"
//    One ZZH line a save: alive at load → end, starved and cold since load, logs, firewood and food held at the end, and the
//    villagers' mean walking share (ticks on a leg against all living ticks), flat | hilly.
//
// 2. Unattended valleys founded fresh, flat / Gentle / Rolling / Hilly, over the seeds 1–86:
//      [ZZ_VALLEY_YEARS=12] dotnet test tests/Bclone.Sim.Tests -c Release --filter "FullyQualifiedName~ZzHeights.FreshValleys"
//    One ZZV line a setting: valleys dead by the end, people alive summed, starved and cold summed, the steep share of the land.
//    ⚠️ ON `ShippedConfig.Established()`, the shipped numbers with the founding buildings standing — `Load()` alone founds a
//    bare camp that freezes to the last soul in its first winter on every seed, flat or hilly (the first run's 86 of 86).
// ⚠️ A save is gzip (`SaveFile.Write`): read it through a GZipStream, not as text (the first run's `0x1F`).
public sealed class ZzHeights
{
    private readonly ITestOutputHelper _o;

    public ZzHeights(ITestOutputHelper o) => _o = o;

    private static int Years(string key, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(key), out int y) ? y : fallback;

    public static IEnumerable<object[]> Saves() =>
        Directory.GetFiles(Environment.GetEnvironmentVariable("ZZ_SAVES")!, "*.save").Select(p => new object[] { p });

    [Theory]
    [MemberData(nameof(Saves))]
    public void OnHisSaves(string path)
    {
        int years = Years("ZZ_YEARS", 20);
        string flat = Play(Open(path), years);
        string hillyCopy = WithHills(path);
        string hilly = Play(Open(hillyCopy), years);
        File.Delete(hillyCopy);
        _o.WriteLine($"ZZH {Path.GetFileNameWithoutExtension(path)} | flat {flat} | hilly {hilly}");
    }

    public static IEnumerable<object[]> Settings() => new[]
    {
        new object[] { "flat", 1, 100 },
        new object[] { "gentle", 16, 50 },
        new object[] { "rolling", 16, 100 },
        new object[] { "hilly", 16, 200 },
    };

    [Theory]
    [MemberData(nameof(Settings))]
    public void FreshValleys(string name, int levels, int hills)
    {
        int years = Years("ZZ_VALLEY_YEARS", 12);
        var results = new (bool Dead, int Alive, int Starved, int Cold, int Steep)[86];
        Parallel.For(0, results.Length, i =>
        {
            SimConfig config = ShippedConfig.Established() with { Seed = (ulong)(i + 1), HeightLevels = levels, HillScalePercent = hills };
            SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
            loop.Step(years * config.TicksPerYear);
            SimWorld world = loop.World;
            int steep = 0;
            for (int t = 0; t < world.Map.Heights.Count; t++)
            {
                steep += world.Map.SteepnessAtIndex(t) >= config.SlopeSteepAt ? 1 : 0;
            }

            results[i] = (world.Population == 0, world.Population, Dead(world, CauseOfDeath.Starvation), Dead(world, CauseOfDeath.Cold),
                steep * 100 / world.Map.Heights.Count);
        });

        _o.WriteLine($"ZZV {name} y{years}: dead {results.Count(r => r.Dead)} of 86, alive {results.Sum(r => r.Alive)}, "
            + $"starved {results.Sum(r => r.Starved)}, cold {results.Sum(r => r.Cold)}, steep {results.Average(r => r.Steep):F1} % of the land");
    }

    private static SimLoop Open(string path)
    {
        SaveOpened opened = SaveFile.Open(path, ShippedConfig.Load(), new InMemoryLogSink());
        Assert.True(opened.Loop is not null, opened.Detail);
        return opened.Loop!;
    }

    /// <summary>A copy of the save with its hills: the Heights stage over its own river and founding, from its own seed.</summary>
    private static string WithHills(string path)
    {
        SimWorld flat = Open(path).World;
        GeneratedMap map = flat.Map;
        SimConfig config = flat.Config with
        {
            HeightLevels = 16,
            HillScalePercent = int.TryParse(Environment.GetEnvironmentVariable("ZZ_HILLS"), out int h) ? h : 100,
        };
        var rng = new DeterministicRandom(MapGenerator.StageSeed(flat.Seed, MapGenerator.Stage.Heights));
        byte[] heights = MapGenerator.RaiseHeights(config, ref rng, map.Tiles.ToArray(), map.FoundingSite, map.Width, map.Height, map.MinX, map.MinY);

        string text;
        using (FileStream file = File.OpenRead(path))
        using (var zipped = new GZipStream(file, CompressionMode.Decompress))
        using (var reader = new StreamReader(zipped, Encoding.UTF8))
        {
            text = reader.ReadToEnd();
        }

        JsonObject document = JsonNode.Parse(text)!.AsObject();
        document["world"]!["map"]!["heights"] = SaveWriter.Bytes(heights);
        string copy = Path.Combine(Path.GetTempPath(), "zzh-hilly-" + Path.GetFileName(path));
        SaveFile.Write(copy, document);
        return copy;
    }

    private static string Play(SimLoop loop, int years)
    {
        SimWorld world = loop.World;
        int aliveAtLoad = world.Population;
        int starvedAtLoad = Dead(world, CauseOfDeath.Starvation);
        int coldAtLoad = Dead(world, CauseOfDeath.Cold);
        long walking = 0;
        long living = 0;
        int ticks = years * world.Config.TicksPerYear;
        for (int t = 0; t < ticks; t++)
        {
            loop.StepOnce();
            foreach (Villager v in world.Villagers)
            {
                if (v.Alive)
                {
                    living++;
                    walking += v.LegTicks > Fixed.Zero ? 1 : 0;
                }
            }
        }

        return $"alive {aliveAtLoad} -> {world.Population} starved {Dead(world, CauseOfDeath.Starvation) - starvedAtLoad} "
            + $"cold {Dead(world, CauseOfDeath.Cold) - coldAtLoad} logs {world.TotalLogs()} firewood {world.TotalFirewood()} "
            + $"walking {(living == 0 ? 0 : walking * 1000 / living / 10.0):F1}%";
    }

    private static int Dead(SimWorld world, CauseOfDeath cause) =>
        world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == cause);
}
