using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// ⛔ D777's MEASUREMENT HARNESS — kept in tools/harness/, COPIED into tests/Bclone.Sim.Tests/ to run and DELETED from there
// before any commit (D420). `water-need.md §5` step 1: D428's open question — why the well's trip every 10 days cost 21
// shipped villagers of 169 (`organic-housing.md §9.12f`), re-measured on today's code over 100 valleys with a noise arm,
// and the time budget read for where the hours went.
//
// Arms: ZZ_WELL=1 marks a well at Year 3 on the first free tile five to nine out from the founding (§9.12f's pose), its
// stone given (no harness village holds stone, ZzWorkshop's lesson); ZZ_TRIP = water_trip_every_days (20 shipped; 100000 is
// "trips off"). ZZ_NOISE=n adds n to every stock limit (§9.12f's noise arm: main's limits +1). The opening is ZzBase's —
// the shipped cold start, the opening played, a granary and a warehouse at Year 3 — on seeds 200–299 (ZZ_SEEDS=a,b
// narrows), ZZ_YEARS (50). ZZ_LABEL names the arm.
//
// One ZZW line a run: alive, peak, starved, cold, born; trips begun; and the able adults' years by kind — walking to the
// well and drawing, resting, working at a trade, fetching for the household, seeking warmth, eating. ZZ_STATES=1 adds
// ZzSchool's ZZT line (`school.py --states` sums it).
//
// Copy in; `dotnet test tests/Bclone.Sim.Tests -c Release --filter ZzWell --logger "console;verbosity=detailed"` per arm;
// delete.
public sealed class ZzWell
{
    private readonly ITestOutputHelper _o;

    public ZzWell(ITestOutputHelper o) => _o = o;

    [Theory]
    [MemberData(nameof(Runs))]
    public void Measure(ulong seed)
    {
        bool well = Environment.GetEnvironmentVariable("ZZ_WELL") == "1";
        int years = int.Parse(Environment.GetEnvironmentVariable("ZZ_YEARS") ?? "50");
        int noise = int.Parse(Environment.GetEnvironmentVariable("ZZ_NOISE") ?? "0");
        SimConfig shipped = ShippedConfig.Load();
        // ZZ_TRIP was water_trip_every_days until D778; it is the jar now (40 at one a person a day is the old 20 for two).
        int trip = int.Parse(Environment.GetEnvironmentVariable("ZZ_TRIP")
            ?? shipped.WaterJar.ToString(System.Globalization.CultureInfo.InvariantCulture));
        string arm = Environment.GetEnvironmentVariable("ZZ_LABEL") ?? (well ? $"w{trip}" : "base") + (noise != 0 ? $"n{noise}" : "");

        SimConfig config = shipped with { Seed = seed, WaterJar = trip };
        SimLoop loop = SimFactory.CreatePhase0(config, new Bclone.Sim.Logging.InMemoryLogSink());
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);
        if (noise != 0)
        {
            foreach (Goods g in Enum.GetValues<Goods>())
            {
                if (world.StockLimits.For(g) is int limit)
                {
                    world.StockLimits.Set(g, limit + noise);
                }
            }
        }

        bool states = Environment.GetEnvironmentVariable("ZZ_STATES") == "1";
        var stateTicks = new long[Enum.GetValues<VillagerState>().Length + 1];
        var before = new Dictionary<int, VillagerState>();
        int peak = 0, trips = 0, wellYear = -1;
        long toWell = 0, resting = 0, adultTicks = 0;
        for (int t = 0; t < config.TicksPerYear * years; t++)
        {
            if (t == config.TicksPerYear * 3)
            {
                MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 8);
                MarkSomewhereNear(world, BuildingKind.Warehouse, world.Map.FoundingSite, 8);
                if (well)
                {
                    MarkAWell(world);
                }
            }

            loop.StepOnce();
            peak = Math.Max(peak, world.Population);
            if (well && wellYear < 0 && world.Wells.Count > 0)
            {
                wellYear = t / config.TicksPerYear;
            }

            foreach (Villager v in world.Villagers)
            {
                if (!v.Alive)
                {
                    continue;
                }

                // A trip to the well or, since D779, to the river bank.
                if (v.State is VillagerState.WalkingToTheWell or VillagerState.WalkingToTheRiver
                    && (!before.TryGetValue(v.Id, out VillagerState was) || was != v.State))
                {
                    trips++;
                }

                before[v.Id] = v.State;
                if (!v.CanTakeAJob)
                {
                    continue;
                }

                adultTicks++;
                stateTicks[Math.Min((int)v.State, stateTicks.Length - 1)]++;
                if (v.State is VillagerState.WalkingToTheWell or VillagerState.DrawingWater
                    or VillagerState.WalkingToTheRiver or VillagerState.DrawingAtTheRiver)
                {
                    toWell++;
                }

                if (v.State == VillagerState.Resting)
                {
                    resting++;
                }
            }
        }

        int starved = world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Starvation);
        int cold = world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Cold);
        int born = world.Villagers.Count(v => v.BirthTick > 0);
        int perYear = config.TicksPerYear;
        _o.WriteLine(
            $"ZZW {arm} {seed} alive {world.Population} peak {peak} starved {starved} cold {cold} born {born} "
            + $"wellyear {wellYear} trips {trips} adultyears {adultTicks / perYear} wellticks {toWell} restyears {resting / perYear}");
        if (states)
        {
            _o.WriteLine($"ZZT {arm} {seed} " + string.Join(" ", Enum.GetValues<VillagerState>()
                .Where(s => stateTicks[Math.Min((int)s, stateTicks.Length - 1)] > 0)
                .Select(s => $"{s}:{stateTicks[Math.Min((int)s, stateTicks.Length - 1)]}")));
        }
    }

    // §9.12f's pose: the first tile, ring by ring from five out to nine, where a well may be marked; its stone given.
    private static void MarkAWell(SimWorld world)
    {
        GridPos site = world.Map.FoundingSite;
        for (int r = 5; r <= 9; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r
                        || !world.Mark(BuildingKind.Well, new GridPos(site.X + dx, site.Y + dy)).Allowed)
                    {
                        continue;
                    }

                    foreach (Workplace w in world.Workplaces)
                    {
                        if (w.Construction is { IsFinished: false } c && c.Kind == BuildingKind.Well)
                        {
                            c.Deliver(Goods.Stone, c.StillNeeded(Goods.Stone));
                        }
                    }

                    return;
                }
            }
        }
    }

    private static void MarkSomewhereNear(SimWorld world, BuildingKind kind, GridPos site, int radius)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (world.Mark(kind, new GridPos(site.X + dx, site.Y + dy)).Allowed)
                {
                    return;
                }
            }
        }
    }

    public static IEnumerable<object[]> Runs()
    {
        IEnumerable<int> seeds = Enumerable.Range(200, 100);
        if (Environment.GetEnvironmentVariable("ZZ_SEEDS") is string listed)
        {
            seeds = listed.Split(',').Select(int.Parse);
        }

        foreach (int s in seeds)
        {
            yield return new object[] { (ulong)s };
        }
    }
}
