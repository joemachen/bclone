using System.Diagnostics;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Persistence;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// ⛔ D507's MEASUREMENT — what a replay save would cost to load: the founding plus fifty shipped years,
// timed. Kept in tools/harness/, COPIED into tests/Bclone.Sim.Tests/ to run and DELETED from there before
// any commit. Run: cp, then
//   dotnet test tests/Bclone.Sim.Tests -c Release --filter "FullyQualifiedName~ZzReplay" --logger "console;verbosity=detailed"
// Four arms: "unattended" (founded and left), "played" (ColdStartTests' opening, a granary and a
// warehouse at year 3 — ZzBase's shape), "every" (played, plus a lodge, a fishery and a farm — ZzBase's
// "every") and "established" (the warm founding, ShippedConfig.Established). One ZZR line per run: founding ms, fifty-year ms, ticks/sec,
// alive and peak, how long one StateHash takes at the end, and — since the snapshot was built — how long the
// village takes to save and to load back (hash checked) and how big the file is.
public sealed class ZzReplay
{
    private readonly ITestOutputHelper _o;

    public ZzReplay(ITestOutputHelper o) => _o = o;

    [Theory]
    [MemberData(nameof(Runs))]
    public void Measure(string arm, ulong seed)
    {
        SimConfig config = (arm == "established" ? ShippedConfig.Established() : ShippedConfig.Load()) with { Seed = seed };

        var found = Stopwatch.StartNew();
        SimLoop loop = SimFactory.CreatePhase0(config, new Bclone.Sim.Logging.InMemoryLogSink());
        SimWorld world = loop.World;
        found.Stop();

        var limits = config.StartingStockLimits;
        for (int id = 0; id < world.GoodsCatalog.Count; id++)
        {
            if (limits.TryGetValue(world.GoodsCatalog[id].Name, out int limit)) { world.SetStockLimit((Goods)id, limit); }
        }

        if (arm is "played" or "every") { ColdStartTests.PlayTheOpening(world); }
        if (arm == "every")
        {
            HuntingTests.RaiseALodgeFor(world);
            FishingTests.RaiseAFishery(world);
            Workplace farm = FarmFixtures.RaiseAFarm(world);
            FarmFixtures.GiveItGround(world, farm, 3);
        }

        int peak = 0;
        var run = Stopwatch.StartNew();
        for (int t = 0; t < config.TicksPerYear * 50; t++)
        {
            if (arm is "played" or "every" && t == config.TicksPerYear * 3)
            {
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 8);
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Warehouse, world.Map.FoundingSite, 8);
            }

            loop.StepOnce();
            peak = Math.Max(peak, world.Population);
        }

        run.Stop();
        var hash = Stopwatch.StartNew();
        ulong h = StateHash.Compute(world);
        hash.Stop();
        long ticks = config.TicksPerYear * 50L;

        // D507's other half, measured once the snapshot existed: write the village and read it back the way
        // the game does — capture + gzip to a file, then gunzip + parse + build — and the file's size.
        string path = Path.Combine(Path.GetTempPath(), $"zzreplay-{arm}-{seed}.save");
        var write = Stopwatch.StartNew();
        SaveFile.Write(path, SaveGame.Capture(loop, "0.0.1", "now", seed.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        write.Stop();
        long bytes = new FileInfo(path).Length;
        var read = Stopwatch.StartNew();
        using (var zipped = new System.IO.Compression.GZipStream(File.OpenRead(path), System.IO.Compression.CompressionMode.Decompress))
        using (var text = new StreamReader(zipped))
        {
            SimLoop loaded = SaveGame.Load(System.Text.Json.Nodes.JsonNode.Parse(text.ReadToEnd())!.AsObject(), config);
            Assert.Equal(h, StateHash.Compute(loaded.World));
        }

        read.Stop();
        File.Delete(path);
        _o.WriteLine($"ZZR {arm} {seed} found_ms {found.ElapsedMilliseconds} fifty_ms {run.ElapsedMilliseconds} tps {ticks * 1000 / Math.Max(1, run.ElapsedMilliseconds)} alive {world.Population} peak {peak} buildings {world.Workplaces.Count + world.StoreBuildings.Count} hash_us {hash.Elapsed.TotalMicroseconds:F0} save_ms {write.ElapsedMilliseconds} load_ms {read.ElapsedMilliseconds} save_kb {bytes / 1024} hash {h}");
    }

    public static IEnumerable<object[]> Runs()
    {
        ulong[] seeds = { 12345, 200, 201, 202, 203, 204, 205, 206, 207, 208 };
        foreach (ulong s in seeds) { yield return new object[] { "unattended", s }; }
        foreach (ulong s in seeds) { yield return new object[] { "played", s }; }
        foreach (ulong s in seeds) { yield return new object[] { "every", s }; }
        foreach (ulong s in seeds) { yield return new object[] { "established", s }; }
    }
}
