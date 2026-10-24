using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// ⛔ D611's MEASUREMENT HARNESS — kept in tools/harness/, COPIED into tests/Bclone.Sim.Tests/ to run and DELETED from
// there before any commit (D420). What Joe's starting stock limits (D608) do to a village, before they ship.
//
// Arms (ZZ_LIMITS): "old" — the limits shipped before D611, typed here so the arm does not move with the file; "new" —
// Joe's, from his panel. Openings (ZZ_OPEN): "played" — ZzBase's: the shipped cold start, the opening played, a granary
// and a warehouse marked at Year 3, seeds 200–299; "established" — `ShippedConfig.Established()` (founding buildings,
// a market among them: the harness's own villages never place one, D601's trap), seeds 1–36 (D603's trap: twelve seeds
// cannot carry a survival claim); "every" — the played opening on the shipped file with a hunter's lodge, a fishery
// and a farm raised at once, seeds 1–36 (the only opening of the three that ever makes fish or meat). ZZ_YEARS (50).
// ZZ_ONLY=<good name> applies Joe's number for that good alone, the rest as before — which of them did it. ZZ_SEEDS=a,b
// narrows the seeds; ZZ_TRACE=1 prints a ZZY line a year (people, held goods, trades, deaths by cause); ZZ_LABEL renames
// the arm (D612 measured two builds of the market on the same limits: label each "old" / "new"). One ZZL a run, with
// the marketers' ticks carrying stock to a counter and what the counters held a year.
//
// Copy in; `dotnet test tests/Bclone.Sim.Tests -c Release --filter ZzLimits --logger "console;verbosity=detailed"`
// with each ZZ_LIMITS × ZZ_OPEN; delete; then `python tools/harness/limits.py <outputs>`.
public sealed class ZzLimits
{
    private static readonly Dictionary<string, int> Old = new()
    {
        ["forage"] = 2000, ["wheat"] = 1000, ["bread"] = 1000, ["flour"] = 200, ["fish"] = 250, ["meat"] = 250,
        ["logs"] = 200, ["firewood"] = 400, ["stone"] = 200, ["stone tools"] = 25, ["iron tools"] = 25,
        ["iron"] = 200, ["leather"] = 200,
    };

    private static readonly Dictionary<string, int> New = new()
    {
        ["forage"] = 2500, ["wheat"] = 2000, ["bread"] = 1000, ["flour"] = 200, ["fish"] = 2500, ["meat"] = 2500,
        ["logs"] = 400, ["firewood"] = 600, ["stone"] = 400, ["stone tools"] = 30, ["iron tools"] = 25,
        ["iron"] = 200, ["leather"] = 200,
    };

    private readonly ITestOutputHelper _o;

    public ZzLimits(ITestOutputHelper o) => _o = o;

    [Theory]
    [MemberData(nameof(Runs))]
    public void Measure(string open, ulong seed)
    {
        string arm = Environment.GetEnvironmentVariable("ZZ_LIMITS") ?? "old";
        var limits = new Dictionary<string, int>(arm == "new" ? New : Old);
        if (Environment.GetEnvironmentVariable("ZZ_ONLY") is string only)
        {
            limits = new Dictionary<string, int>(Old) { [only] = New[only] };
            arm = "only-" + only.Replace(' ', '_');
        }

        // ZZ_LABEL names the arm in the output — two builds of the code measured on the same limits (D612's market).
        arm = Environment.GetEnvironmentVariable("ZZ_LABEL") ?? arm;
        bool trace = Environment.GetEnvironmentVariable("ZZ_TRACE") == "1";
        int years = int.Parse(Environment.GetEnvironmentVariable("ZZ_YEARS") ?? "50");

        SimConfig config = (open == "established" ? ShippedConfig.Established() : ShippedConfig.Load()) with
        {
            Seed = seed,
            StartingStockLimits = limits,
        };

        SimLoop loop = SimFactory.CreatePhase0(config, new Bclone.Sim.Logging.InMemoryLogSink());
        SimWorld world = loop.World;
        if (open is "played" or "every")
        {
            ColdStartTests.PlayTheOpening(world);
        }

        if (open == "every")
        {
            HuntingTests.RaiseALodgeFor(world);
            FishingTests.RaiseAFishery(world);
            Workplace farm = FarmFixtures.RaiseAFarm(world);
            FarmFixtures.GiveItGround(world, farm, 3);
        }

        int peak = 0;
        long held = 0;
        long stocking = 0;
        long counter = 0;
        for (int t = 0; t < config.TicksPerYear * years; t++)
        {
            if (open is "played" or "every" && t == config.TicksPerYear * 3)
            {
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 8);
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Warehouse, world.Map.FoundingSite, 8);
            }

            loop.StepOnce();
            peak = Math.Max(peak, world.Population);

            // D612: the marketers' hauling (D197 measured a building-sized target doubling it) and what the counters hold.
            stocking += world.Villagers.Count(v => v.Alive && v.State == VillagerState.StockingTheMarket);
            if (t % config.TicksPerYear == 0)
            {
                counter += world.StoreBuildings.Where(s => s.Kind == StoreKind.Market).Sum(s => s.Store.Held);
            }
            if (t % config.TicksPerYear == 0)
            {
                held += world.StoreBuildings.Where(s => s.IsStorage).Sum(s => s.Store[Goods.Fish] + s.Store[Goods.Meat]);
            }

            // In winter's first day too: what the cold finds.
            if (trace && (t % config.TicksPerYear == 0 || t % config.TicksPerYear == config.TicksPerSeason * 3))
            {
                string Held(Goods g) => world.HeldAgainstItsLimit(g).ToString(System.Globalization.CultureInfo.InvariantCulture);
                string Hands(JobKind k) => world.Villagers.Count(v => v.Alive && world.FindWorkplace(v.WorkplaceId)?.Kind == k).ToString(System.Globalization.CultureInfo.InvariantCulture);
                _o.WriteLine(
                    $"ZZY {arm} {open} {seed} t {t} y {t / config.TicksPerYear} {(t % config.TicksPerYear == 0 ? "spr" : "win")} alive {world.Population} "
                    + $"firewood {Held(Goods.Firewood)} logs {Held(Goods.Logs)} forage {Held(Goods.Produce)} wheat {Held(Goods.Wheat)} stone {Held(Goods.Stone)} "
                    + $"wood {Hands(JobKind.Woodcutter)} forager {Hands(JobKind.Forager)} forester {Hands(JobKind.Forester)} builder {Hands(JobKind.Builder)} "
                    + $"quarrier {Hands(JobKind.Quarrier)} laborer {world.Villagers.Count(v => v.IsLaborer)} "
                    + $"starved {world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Starvation)} "
                    + $"cold {world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Cold)}");
            }
        }

        int starved = world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Starvation);
        int cold = world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Cold);
        int born = world.Villagers.Count(v => v.BirthTick > 0);
        _o.WriteLine(
            $"ZZL {arm} {open} {seed} alive {world.Population} peak {peak} starved {starved} cold {cold} born {born} "
            + $"fish {world.FoodEverProducedOf(Goods.Fish)} meat {world.FoodEverProducedOf(Goods.Meat)} "
            + $"forage {world.FoodEverProducedOf(Goods.Produce)} fishmeatheld {held / Math.Max(1, years)} "
            + $"firewood {world.StoreBuildings.Sum(s => s.Store[Goods.Firewood])} stocking {stocking} counter {counter / Math.Max(1, years)}");
    }

    public static IEnumerable<object[]> Runs()
    {
        string open = Environment.GetEnvironmentVariable("ZZ_OPEN") ?? "played";
        IEnumerable<int> seeds = open == "played" ? Enumerable.Range(200, 100) : Enumerable.Range(1, 36);
        if (Environment.GetEnvironmentVariable("ZZ_SEEDS") is string listed)
        {
            seeds = listed.Split(',').Select(int.Parse);
        }
        foreach (int s in seeds)
        {
            yield return new object[] { open, (ulong)s };
        }
    }
}
