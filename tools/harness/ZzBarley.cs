using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.Persistence;
using Kith.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

// ⛔ D825's MEASUREMENT — what `barley_unlock_forage` is chosen from (`specs/barley.md §9.1`): how much forage a village
// has gathered, by year. Kept in tools/harness/, COPIED into tests/Kith.Sim.Tests/ to run and DELETED before any commit.
//
//   cp tools/harness/ZzBarley.cs tests/Kith.Sim.Tests/
//   ZZ_SAVES=<a COPY of Joe's saves> dotnet test tests/Kith.Sim.Tests -c Release --filter ZzBarley --logger "console;verbosity=detailed"
//   rm tests/Kith.Sim.Tests/ZzBarley.cs
//
// Two arms. SAVES (ZZ_SAVES set): every `.save` under the folder, read and never stepped — one ZZS line a save: the
// village, the year, alive, the forage ever gathered (`FoodEverProducedOf(Produce)`, which only the forager's gather
// writes), the wheat ever reaped and whether the mill is known. VALLEYS (ZZ_SAVES unset): seeds 200–219 (ZZ_SEEDS
// narrows), the shipped config, `ColdStartTests.PlayTheOpening`, ZZ_YEARS (30) — one ZZV line a year: the forage
// ever gathered, alive, foragers seated.
public sealed class ZzBarley
{
    private readonly ITestOutputHelper _o;

    public ZzBarley(ITestOutputHelper o) => _o = o;

    [Theory]
    [MemberData(nameof(Runs))]
    public void Measure(string what)
    {
        if (what.EndsWith(".save", StringComparison.Ordinal))
        {
            SaveOpened opened = SaveFile.Open(what, ShippedConfig.Load());
            if (opened.Loop is not SimLoop loaded)
            {
                _o.WriteLine($"ZZX {what} refused: {opened.Refusal}");
                return;
            }

            SimWorld w = loaded.World;
            string village = Path.GetFileName(Path.GetDirectoryName(what)) ?? what;
            _o.WriteLine(
                $"ZZS {village} {Path.GetFileName(what)} year {w.Clock.Year} alive {w.Villagers.Count(v => v.Alive)} "
                + $"forage {w.FoodEverProducedOf(Goods.Produce)} wheat {w.WheatEverReaped} "
                + $"mill {(w.IsUnlocked(BuildingKind.Mill) ? 1 : 0)} foragers {Seated(w, JobKind.Forager)}");
            return;
        }

        int years = int.Parse(Environment.GetEnvironmentVariable("ZZ_YEARS") ?? "30", System.Globalization.CultureInfo.InvariantCulture);
        SimConfig config = ShippedConfig.Load() with { Seed = ulong.Parse(what, System.Globalization.CultureInfo.InvariantCulture) };
        SimLoop loop = SimFactory.CreatePhase0(config, new Kith.Sim.Logging.InMemoryLogSink());
        ColdStartTests.PlayTheOpening(loop.World);
        for (int y = 1; y <= years; y++)
        {
            loop.Step(config.TicksPerYear);
            SimWorld w = loop.World;
            _o.WriteLine(
                $"ZZV {what} year {y} forage {w.FoodEverProducedOf(Goods.Produce)} "
                + $"alive {w.Villagers.Count(v => v.Alive)} foragers {Seated(w, JobKind.Forager)}");
        }
    }

    private static int Seated(SimWorld w, JobKind kind) =>
        w.Workplaces.Where(p => p.Kind == kind && !p.IsSite).Sum(p => p.WorkerIds.Count);

    public static IEnumerable<object[]> Runs()
    {
        if (Environment.GetEnvironmentVariable("ZZ_SAVES") is string folder)
        {
            foreach (string p in Directory.GetFiles(folder, "*.save", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
            {
                yield return new object[] { p };
            }

            yield break;
        }

        IEnumerable<int> seeds = Enumerable.Range(200, 20);
        if (Environment.GetEnvironmentVariable("ZZ_SEEDS") is string listed)
        {
            seeds = listed.Split(',').Select(int.Parse);
        }

        foreach (int s in seeds)
        {
            yield return new object[] { s.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        }
    }
}
