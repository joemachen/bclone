using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// ⛔ D521's MEASUREMENT — the food chain's anchors, on the code as it is (`specs/food-chain.md §8`).
// Kept in tools/harness/, COPIED into tests/Bclone.Sim.Tests/ to run and DELETED from there before any
// commit. Never committed under tests/.
//
//   cp tools/harness/ZzFoodChain.cs tests/Bclone.Sim.Tests/
//   dotnet test tests/Bclone.Sim.Tests -c Release --filter ZzFoodChain --logger "console;verbosity=detailed"
//   rm tests/Bclone.Sim.Tests/ZzFoodChain.cs
//
// Nothing of the chain exists yet, so this measures what the chain's numbers must be set against:
//  - per trade, food brought in per seat-year held (forage / fish / meat / wheat) — the rung a miller and
//    baker pair must beat to be worth two hands;
//  - the wheat a village has reaped by each year — what `mill_unlock_wheat` is chosen from;
//  - the wheat left in stores at each year's end — what a mill could grind without anyone going hungry;
//  - how in step a household eats: the share of sampled ticks two adults of one household carry the same
//    hunger (D28 measured 100 % before D190's seeded rhythm).
// Arms: `every` (shipped config, the opening plus a lodge, a fishery and a farm raised free at t0 — the per-hand
// rates; a fixture effect for the timeline, D515); `farm0` / `farm2` (a farmhouse MARKED at t0 / Year 2 and its
// field painted — the timeline a player could have); a trailing `s` (`farm0s`, `farm2s`, `farm0x2s`) also staffs the
// farms as a player would (`SetJobLimit`, D109); `x2` marks two; a trailing `w` lifts the wheat limit alone, so a farm
// is never stood down by wheat nobody eats. ZZ_ARMS, ZZ_SEEDS narrow; ZZ_WHY=1 prints each farm's card every season.
public sealed class ZzFoodChain
{
    private static readonly Goods[] Foods = { Goods.Produce, Goods.Fish, Goods.Meat, Goods.Wheat };
    private static readonly JobKind[] Trades = { JobKind.Forager, JobKind.Fisher, JobKind.Hunter, JobKind.Farmer };

    private readonly ITestOutputHelper _o;

    public ZzFoodChain(ITestOutputHelper o) => _o = o;

    [Theory]
    [MemberData(nameof(Runs))]
    public void Measure(string arm, ulong seed)
    {
        SimConfig config = ShippedConfig.Load() with { Seed = seed };

        // `ZZ_MILL`: the mill's unlock, for an arm that asks when it should come (D522: 5,000 was never
        // reached under the shipped wheat limit).
        if (Environment.GetEnvironmentVariable("ZZ_MILL") is string mill)
        {
            config = config with { MillUnlockWheat = int.Parse(mill, System.Globalization.CultureInfo.InvariantCulture) };
        }
        SimLoop loop = SimFactory.CreatePhase0(config, new Bclone.Sim.Logging.InMemoryLogSink());
        SimWorld world = loop.World;
        var limits = config.StartingStockLimits;
        for (int id = 0; id < world.GoodsCatalog.Count; id++)
        {
            if (limits.TryGetValue(world.GoodsCatalog[id].Name, out int limit))
            {
                world.SetStockLimit((Goods)id, limit);
            }
        }

        ColdStartTests.PlayTheOpening(world);
        // ⚠️ `w`: lift the WHEAT limit alone. Measured: the village eats forage first (id order), never eats into
        // its wheat, and a farm stops reaping for good at the 1,000 limit after its first harvest. (Lifting every
        // limit — an `open` arm — killed the village outright, peak 4, and was dropped.)
        if (arm.Contains('w', StringComparison.Ordinal))
        {
            world.SetStockLimit(Goods.Wheat, null);
        }

        if (arm.StartsWith("every", StringComparison.Ordinal))
        {
            HuntingTests.RaiseALodgeFor(world);
            FishingTests.RaiseAFishery(world);
            Workplace farm = FarmFixtures.RaiseAFarm(world);
            FarmFixtures.GiveItGround(world, farm, 3);
        }

        int perYear = config.TicksPerYear;
        const int years = 50;
        const int fromYear = 5; // per-hand rates and lockstep from here on: past the founding's scramble
        var reapedBy = new List<int>();
        var wheatHeld = new List<int>();
        var seatTicks = new long[Trades.Length];
        var producedAtFrom = new int[Foods.Length];
        long woodSeatTicks = 0;
        int splitAtFrom = 0;
        long pairSamples = 0, pairsSame = 0;
        int peak = 0;
        int marked = 0;
        int farmStood = -1;
        var painted = new HashSet<int>();
        bool staffed = arm.StartsWith("farm", StringComparison.Ordinal) && arm.Contains('s'); // the player staffs the farms

        // ⭐ D522's POST-BUILD ARM (`food-chain.md §8` step 3): a trailing `B` builds the chain the way a player
        // would — a mill marked the tick it is learned, a bakery the tick that is, one hand each — and measures
        // whether one miller and one baker keep up with the farm, and whether a farm stalled at the wheat limit
        // reaps again (Joe's §9b call (a)).
        bool bread = arm.Contains('B', StringComparison.Ordinal);

        // `R`: the mill and the bakery are RAISED free the tick they are learned (the fixture's way, as `every`
        // raises its farm) — measured first: marked and left to the unattended builders, the mill stood in 1
        // valley of 17 and the bakery in none (24 stone each, and these villages dig little).
        bool raise = arm.Contains('R', StringComparison.Ordinal);
        int millStood = -1, bakeryStood = -1;
        bool millMarked = false, bakeryMarked = false;
        long millerTicks = 0, bakerTicks = 0;
        int reapedAtMill = -1;

        for (int t = 0; t < perYear * years; t++)
        {
            bool markNow = (arm.StartsWith("farm0", StringComparison.Ordinal) && t == 0)
                || (arm.StartsWith("farm2", StringComparison.Ordinal) && t == perYear);
            if (markNow)
            {
                for (int n = 0; n < (arm.Contains("x2", StringComparison.Ordinal) ? 2 : 1); n++)
                {
                    GridPos at = FarmFixtures.ClearGroundNear(world);
                    if (world.Mark(BuildingKind.Farmhouse, at).Allowed)
                    {
                        marked++;
                    }
                }

                if (staffed)
                {
                    world.SetJobLimit(JobKind.Farmer, marked * config.FarmhouseSeats);
                }
            }

            if (bread && !millMarked && world.IsUnlocked(BuildingKind.Mill))
            {
                millMarked = MarkNear(world, BuildingKind.Mill, raise);
                world.SetJobLimit(JobKind.Miller, 1);
                if (!millMarked && Environment.GetEnvironmentVariable("ZZ_WHY") is not null && t % perYear == 0)
                {
                    GridPos f = world.Map.FoundingSite;
                    _o.WriteLine($"WHYMILL {arm} {seed} t{t}: {world.CanBuildAt(BuildingKind.Mill, new GridPos(f.X + 6, f.Y + 6)).Reason} | {world.Mark(BuildingKind.Mill, new GridPos(f.X + 6, f.Y + 6)).Reason}");
                }
            }

            if (bread && !bakeryMarked && world.IsUnlocked(BuildingKind.Bakery))
            {
                bakeryMarked = MarkNear(world, BuildingKind.Bakery, raise);
                world.SetJobLimit(JobKind.Baker, 1);
            }

            loop.StepOnce();
            int now = (int)world.Tick;
            peak = Math.Max(peak, world.Population);
            if (millStood < 0 && world.Workplaces.Any(w => w.Kind == JobKind.Miller && !w.IsSite))
            {
                millStood = now;
                reapedAtMill = world.WheatEverReaped;
            }

            if (bakeryStood < 0 && world.Workplaces.Any(w => w.Kind == JobKind.Baker && !w.IsSite))
            {
                bakeryStood = now;
            }

            foreach (Villager v in world.Villagers)
            {
                if (v.Alive && v.HasJob && world.FindWorkplace(v.WorkplaceId)?.Kind is JobKind held)
                {
                    millerTicks += held == JobKind.Miller ? 1 : 0;
                    bakerTicks += held == JobKind.Baker ? 1 : 0;
                }
            }
            // ⚠️ A field painted on a SITE never reaped (measured: 90 farmer seat-years, 0 wheat) — paint it once the
            // farm stands, as a player whose farmhouse has just gone up would.
            foreach (Workplace farm in world.Workplaces)
            {
                if (farm.Kind == JobKind.Farmer && !farm.IsSite && painted.Add(farm.Id))
                {
                    if (farmStood < 0)
                    {
                        farmStood = now;
                    }

                    if (!arm.StartsWith("every", StringComparison.Ordinal))
                    {
                        FarmFixtures.GiveItGround(world, farm, 3);
                    }
                }
            }

            if (now == perYear * fromYear)
            {
                for (int g = 0; g < Foods.Length; g++)
                {
                    producedAtFrom[g] = ProducedEver(world, Foods[g]);
                }

                splitAtFrom = world.LogsEverSplit;
            }

            if (now >= perYear * fromYear)
            {
                foreach (Villager v in world.Villagers)
                {
                    if (!v.Alive || !v.HasJob)
                    {
                        continue;
                    }

                    JobKind? kind = world.FindWorkplace(v.WorkplaceId)?.Kind;
                    if (kind == JobKind.Woodcutter)
                    {
                        woodSeatTicks++;
                    }

                    for (int k = 0; k < Trades.Length; k++)
                    {
                        if (kind == Trades[k])
                        {
                            seatTicks[k]++;
                        }
                    }
                }

                // Lockstep: every pair of living adults in one household, every tick.
                foreach (var group in world.Villagers
                    .Where(v => v.Alive && v.LifeStage == LifeStage.Adult)
                    .GroupBy(v => v.HouseholdId))
                {
                    var members = group.ToList();
                    for (int a = 0; a < members.Count; a++)
                    {
                        for (int b = a + 1; b < members.Count; b++)
                        {
                            pairSamples++;
                            if (members[a].Hunger == members[b].Hunger)
                            {
                                pairsSame++;
                            }
                        }
                    }
                }
            }

            if (Environment.GetEnvironmentVariable("ZZ_WHY") is not null && now % config.TicksPerSeason == 0 && now / perYear <= 4)
            {
                foreach (Workplace farm in world.Workplaces.Where(w => w.Kind == JobKind.Farmer && !w.IsSite))
                {
                    _o.WriteLine($"WHY {arm} {seed} t{now} {world.Clock.SeasonAndYear()} buffer {farm.Store[Goods.Wheat]} "
                        + $"workers {world.Villagers.Count(v => v.Alive && v.WorkplaceId == farm.Id)} "
                        + $"wheat-limit-met {world.StockLimits.IsMet(Goods.Wheat, world.StoreBuildings.Sum(x => x.Store[Goods.Wheat]))} "
                        + $"idle '{world.IdleNote(farm)}' states [{string.Join(",", world.Villagers.Where(v => v.Alive && v.WorkplaceId == farm.Id).Select(v => v.State))}]");
                }
            }

            if (now % perYear == 0 && now / perYear <= years)
            {
                reapedBy.Add(ProducedEver(world, Goods.Wheat));
                wheatHeld.Add(world.StoreBuildings.Sum(s => s.Store[Goods.Wheat]));
            }
        }

        var perSeatYear = new List<string>();
        for (int k = 0; k < Trades.Length; k++)
        {
            int made = ProducedEver(world, Foods[k]) - producedAtFrom[k];
            double seatYears = seatTicks[k] / (double)perYear;
            perSeatYear.Add($"{Foods[k]}:{made}/{seatYears:F1}={(seatYears > 0 ? made / seatYears : 0):F0}");
        }

        // The stint-shaped converter: logs taken into splits per woodcutter seat-year (demand-gated — the
        // woodcutter splits only what the village wants burnt, so this is a floor on what a stint can do).
        double woodYears = woodSeatTicks / (double)perYear;
        int split = world.LogsEverSplit - splitAtFrom;
        perSeatYear.Add($"LogsSplit:{split}/{woodYears:F1}={(woodYears > 0 ? split / woodYears : 0):F0}");

        int edibleProduced = Foods.Sum(g => ProducedEver(world, g));
        double millerYears = millerTicks / (double)perYear, bakerYears = bakerTicks / (double)perYear;
        int yearsMilling = millStood < 0 ? 0 : years - (millStood / perYear);
        string chain = bread
            ? $" mill {(millStood < 0 ? -1 : millStood / perYear + 1)} bakery {(bakeryStood < 0 ? -1 : bakeryStood / perYear + 1)}"
                + $" ground {world.FlourEverGround} baked {world.BreadEverBaked} wheat-after-mill {(reapedAtMill < 0 ? -1 : world.WheatEverReaped - reapedAtMill)}"
                + $" per-year-milling {(yearsMilling > 0 ? (world.WheatEverReaped - reapedAtMill) / yearsMilling : 0)}"
                + $" miller {millerYears:F1}y={(millerYears > 0 ? world.FlourEverGround / millerYears : 0):F0} baker {bakerYears:F1}y={(bakerYears > 0 ? world.BreadEverBaked / bakerYears : 0):F0}"
                + $" points-added {world.BreadEverBaked * (world.GoodsCatalog.NutritionOf(Goods.Bread) - 1)}"
                + $" bread-held {world.InStores(Goods.Bread)} flour-held {world.InStores(Goods.Flour)}"
            : string.Empty;
        int starved = world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Starvation);
        _o.WriteLine(
            $"ZZF {arm} {seed} marked {marked} stood {farmStood} alive {world.Population} peak {peak} starved {starved} "
            + $"perseat [{string.Join(" ", perSeatYear)}] "
            + $"reapedBy [{string.Join(" ", reapedBy)}] wheatHeld [{string.Join(" ", wheatHeld)}] "
            + $"lockstep {(pairSamples > 0 ? 100.0 * pairsSame / pairSamples : -1):F1}% pairs {pairSamples} "
            + $"check produced {edibleProduced} ever {world.FoodEverProduced} eaten {world.FoodEverEaten}"
            + $" froze {world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Cold)}{chain}");
    }

    // A food's lifetime production, recorded where it comes into being (`SimWorld.RecordFoodProduced`, D387) — never
    // where it is put down. ⚠️ Summing `Stockpile.Produced` over arms, larders and stores double-counts: a gather is
    // `Add`ed to the arms and `Add`ed again to the store it is carried to (measured: 154,397 against 81,766).
    private static int ProducedEver(SimWorld world, Goods good) => world.FoodEverProducedOf(good);

    // A building marked near the founding, as a player would put it down.
    private static bool MarkNear(SimWorld world, BuildingKind kind, bool raise)
    {
        GridPos site = world.Map.FoundingSite;
        for (int radius = 3; radius < 30; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var tile = new GridPos(site.X + dx, site.Y + dy);
                    if (world.CanBuildAt(kind, tile).Allowed && world.Mark(kind, tile).Allowed)
                    {
                        if (raise)
                        {
                            Workplace raised = world.Workplaces.Last(w => w.Construction?.Kind == kind);
                            BuildFixtures.StockTheSite(raised);
                            for (int i = 0; i <= raised.Construction!.Recipe.WorkTicks; i++)
                            {
                                raised.Construction.Work();
                            }

                            world.Complete(raised);
                        }

                        return true;
                    }
                }
            }
        }

        return false;
    }

    public static IEnumerable<object[]> Runs()
    {
        string[] arms = (Environment.GetEnvironmentVariable("ZZ_ARMS") ?? "every,everyw,farm0s,farm0sw,farm2sw,farm0x2sw").Split(',');
        foreach (string arm in arms)
        {
            IEnumerable<ulong> seeds = Environment.GetEnvironmentVariable("ZZ_SEEDS") is string zs
                ? zs.Split(',').Select(ulong.Parse)
                : Enumerable.Range(1, 16).Select(x => (ulong)x).Append(12345UL);
            foreach (ulong s in seeds)
            {
                yield return new object[] { arm, s };
            }
        }
    }
}
