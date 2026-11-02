using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// ⛔ D678's MEASUREMENT HARNESS — kept in tools/harness/, COPIED into tests/Bclone.Sim.Tests/ to run and DELETED from
// there before any commit (D420). `workshop-and-the-carpenter.md §10`: what a workshop costs a village before any cart
// does anything — the logs it takes out of the roofs' timber, measured before the recipe's numbers are typed.
//
// Arms: ZZ_WORKSHOP=1 poses the workshop learned and marks one beside the founding at Year 5 (unset: the baseline, no
// workshop); ZZ_LOGS=n sets logs_per_handcart (10 shipped); ZZ_MAKES=n makes_per_stint (1). The opening is ZzBase's —
// the shipped cold start, the opening played, a granary and a warehouse marked at Year 3 — on seeds 200–299
// (ZZ_SEEDS=a,b narrows), fifty years (ZZ_YEARS). ZZ_LABEL names the arm. One ZZW line a run: alive, peak, starved, cold,
// born, the year the workshop stood (−1 never), carts made and held, carpenter seat-years, logs held at 50, the roofs
// (house sites) that waited on logs, in site-ticks, and every site's log-wait.
//
// Copy in; `dotnet test tests/Bclone.Sim.Tests -c Release --filter ZzWorkshop --logger "console;verbosity=detailed"`
// per arm; delete.
public sealed class ZzWorkshop
{
    private readonly ITestOutputHelper _o;

    public ZzWorkshop(ITestOutputHelper o) => _o = o;

    [Theory]
    [MemberData(nameof(Runs))]
    public void Measure(ulong seed)
    {
        bool workshop = Environment.GetEnvironmentVariable("ZZ_WORKSHOP") == "1";
        int logs = int.Parse(Environment.GetEnvironmentVariable("ZZ_LOGS") ?? "10");
        int makes = int.Parse(Environment.GetEnvironmentVariable("ZZ_MAKES") ?? "1");
        int years = int.Parse(Environment.GetEnvironmentVariable("ZZ_YEARS") ?? "50");
        string arm = Environment.GetEnvironmentVariable("ZZ_LABEL") ?? (workshop ? $"w{logs}x{makes}" : "base");

        SimConfig config = ShippedConfig.Load() with { Seed = seed, LogsPerHandcart = logs, MakesPerStint = makes };
        SimLoop loop = SimFactory.CreatePhase0(config, new Bclone.Sim.Logging.InMemoryLogSink());
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);

        int peak = 0;
        int stoodYear = -1;
        long carpenterTicks = 0;
        long roofLogWait = 0;
        long siteLogWait = 0;
        for (int t = 0; t < config.TicksPerYear * years; t++)
        {
            if (t == config.TicksPerYear * 3)
            {
                MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 8);
                MarkSomewhereNear(world, BuildingKind.Warehouse, world.Map.FoundingSite, 8);
            }

            if (workshop && t == config.TicksPerYear * 5)
            {
                // Posed learned (no harness village fells 2,500 logs by Year 5), then marked and paid for as a player would.
                world.WorkshopLearned = true;
                MarkSomewhereNear(world, BuildingKind.Workshop, world.Map.FoundingSite, 8);

                // ⚠️ THE STONE IS GIVEN. No harness village holds stone (the quarry is never learned unattended, the smithy's lot
                // too — `tools-and-the-smith.md §9.4`), so a paid workshop never stands and the arm measures nothing. The
                // table is about the LOGS a cart takes from the roofs; the 12 stone is delivered free and said so in §10.
                Workplace? marked = world.Workplaces.FirstOrDefault(w => w.Construction is { IsFinished: false } c && c.Kind == BuildingKind.Workshop);
                marked?.Construction!.Deliver(Goods.Stone, marked.Construction.StillNeeded(Goods.Stone));
            }

            loop.StepOnce();
            peak = Math.Max(peak, world.Population);

            if (stoodYear < 0 && world.Workplaces.Any(w => w.Kind == JobKind.Carpenter && !w.IsSite))
            {
                stoodYear = t / config.TicksPerYear;
            }

            foreach (Villager v in world.Villagers)
            {
                if (v.Alive && v.CanWork && world.FindWorkplace(v.WorkplaceId) is { Kind: JobKind.Carpenter })
                {
                    carpenterTicks++;
                }
            }

            foreach (Workplace w in world.Workplaces)
            {
                if (w.Construction is { IsFinished: false, Demolishing: false } site && site.StillNeeded(Goods.Logs) > 0)
                {
                    siteLogWait++;
                    if (site.Kind == BuildingKind.Home)
                    {
                        roofLogWait++;
                    }
                }
            }
        }

        int starved = world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Starvation);
        int cold = world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Cold);
        int born = world.Villagers.Count(v => v.BirthTick > 0);
        int logsHeld = world.StoreBuildings.Where(s => s.IsStorage).Sum(s => s.Store[Goods.Logs]) + world.OnTheGround(Goods.Logs);
        Workplace? wsite = world.Workplaces.FirstOrDefault(w => w.Construction is { IsFinished: false } c && c.Kind == BuildingKind.Workshop);
        string wsiteWants = wsite is null ? "none" : $"logs{wsite.Construction!.StillNeeded(Goods.Logs)}-stone{wsite.Construction.StillNeeded(Goods.Stone)}";
        int stoneHeld = world.StoreBuildings.Where(s => s.IsStorage).Sum(s => s.Store[Goods.Stone]) + world.OnTheGround(Goods.Stone);
        _o.WriteLine(
            $"ZZW {arm} {seed} alive {world.Population} peak {peak} starved {starved} cold {cold} born {born} "
            + $"stood {stoodYear} carts {world.HandcartsEverMade} held {world.InStores(Goods.Handcart)} "
            + $"seatyears {carpenterTicks / config.TicksPerYear} logs {logsHeld} felled {world.LogsEverFelled} "
            + $"wsite {wsiteWants} stone {stoneHeld} roofwait {roofLogWait} sitewait {siteLogWait} households {world.Households.Count(h => h.HasHome && world.LivingMembersOf(h) > 0)}");
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
