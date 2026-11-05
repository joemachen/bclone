using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// ⛔ D683's MEASUREMENT — the stalled hunter's lodge (`specs/hunting.md §6a`). Kept in tools/harness/,
// COPIED into tests/Bclone.Sim.Tests/ to run and DELETED from there before any commit.
//
//   dotnet test tests/Bclone.Sim.Tests -c Release --filter FullyQualifiedName~ZzLodge --logger "console;verbosity=detailed"
//
// Joe's Hollowmere, Year 25: a lodge holding 2,184 meat met the 2,500 meat limit with 636 on the shelves, the met
// limit stood the hunters down, nobody but the marketer clears a lodge, and the uncollected meat kept the limit met.
// Two openings, each with a lodge raised free at t0: `lodge` (the played opening + a lodge; 30 played seeds) and
// `every` (ZzClothing's: a lodge, a fishery and a farm; 13 seeds). Two limit arms: `x1` (the shipped starting limits
// as Joe plays them — meat 2,500 against a 2,700 lodge, his state) and `x10` (ZzClothing's ×10 at Year 5, where no
// lodge alone can meet the meat row — the control that should not move). Fifty years.
//
// One ZZL line a run: alive / peak / starved / frozen; the lodges' peak food; ticks and years any lodge was swollen
// (`BufferIsSwollen`, food at or above half its room); ticks the meat limit was met; STALL ticks (met limit, a swollen
// lodge, and nobody seated at any lodge — Joe's state exactly); hunter seat-ticks, and those under a met limit; hunters'
// carrying ticks (`ClearingABuffer` / `HaulingToStore`) and hunting ticks; the marketer's ticks walking to a lodge;
// meat carried out of the lodges (every fall) and hunts (every rise). ZZ_TRACE=1 adds a ZZY line each spring.
public sealed class ZzLodge
{
    private readonly ITestOutputHelper _o;

    public ZzLodge(ITestOutputHelper o) => _o = o;

    [Theory]
    [MemberData(nameof(Runs))]
    public void Measure(string arm, string limx, ulong seed)
    {
        SimConfig config = ShippedConfig.Load() with { Seed = seed };
        SimLoop loop = SimFactory.CreatePhase0(config, new Bclone.Sim.Logging.InMemoryLogSink());
        SimWorld world = loop.World;
        var limits = ShippedConfig.Load().StartingStockLimits;
        for (int id = 0; id < world.GoodsCatalog.Count; id++)
        {
            if (limits.TryGetValue(world.GoodsCatalog[id].Name, out int limit))
            {
                world.SetStockLimit((Goods)id, limit);
            }
        }

        int stoneTheyCost = BuildingRecipe.For(BuildingKind.Granary, config).Of(Goods.Stone)
            + BuildingRecipe.For(BuildingKind.Warehouse, config).Of(Goods.Stone);
        int aTile = new GoodsCatalog(config.GoodsCatalog).YieldPerTileOf(Goods.Stone);
        int extraRock = (stoneTheyCost + aTile - 1) / aTile;

        ColdStartTests.PlayTheOpening(world);
        HuntingTests.RaiseALodgeFor(world);
        if (arm == "every")
        {
            FishingTests.RaiseAFishery(world);
            Workplace farm = FarmFixtures.RaiseAFarm(world);
            FarmFixtures.GiveItGround(world, farm, 3);
        }

        int years = int.Parse(Environment.GetEnvironmentVariable("ZZ_YEARS") ?? "50", System.Globalization.CultureInfo.InvariantCulture);
        int multiplier = limx == "x10" ? 10 : 1;

        int peak = 0, lodgePeak = 0;
        long swollenTicks = 0, metTicks = 0, stallTicks = 0, seatTicks = 0, seatTicksMet = 0, carrierTicks = 0, huntTicks = 0, marketerTicks = 0;
        long carriedOut = 0, hunts = 0;
        int lodgeFoodLast = -1;
        int firstStallYear = 0;

        for (int t = 0; t < config.TicksPerYear * years; t++)
        {
            if (t == config.TicksPerYear * 3)
            {
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 8);
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Warehouse, world.Map.FoundingSite, 8);
                SeamFixtures.PaintNearest(world, Terrain.Rock, extraRock);
            }

            if (t == config.TicksPerYear * 5 && multiplier > 1)
            {
                // ZzClothing's ×10 (D664): tools and leather stay at their shipped limits.
                for (int id = 0; id < world.GoodsCatalog.Count; id++)
                {
                    string name = world.GoodsCatalog[id].Name;
                    if (limits.TryGetValue(name, out int limit) && !name.Contains("tools") && name != "leather")
                    {
                        world.SetStockLimit((Goods)id, limit * multiplier);
                    }
                }
            }

            // ZZ_DEATHS=1: one ZZD line per death — year, cause, state, seat, shelter, the household's firewood (D664's
            // trap: read the deaths per seed before reading a mean).
            List<Villager>? aliveBefore = Environment.GetEnvironmentVariable("ZZ_DEATHS") == "1"
                ? world.Villagers.Where(v => v.Alive).ToList()
                : null;
            loop.StepOnce();
            if (aliveBefore is not null)
            {
                foreach (Villager v in aliveBefore)
                {
                    if (!v.Alive)
                    {
                        string seat = v.HasJob ? world.FindWorkplace(v.WorkplaceId)?.Kind.ToString() ?? "?" : "laborer";
                        _o.WriteLine($"ZZD {arm} {limx} {seed} Y{t / config.TicksPerYear + 1} {world.Clock.Season} {v.CauseOfDeath} state {v.State} seat {seat} shelter {world.ShelterAt(v.Tile)} pop {world.Population} homes {world.Households.Count(h => h.HomeTile is not null)}/{world.Households.Count}");
                    }
                }
            }

            peak = Math.Max(peak, world.Population);

            int lodgeFood = 0;
            bool swollen = false;
            var lodgeTiles = new HashSet<GridPos>();
            foreach (Workplace w in world.Workplaces)
            {
                if (w.Kind != JobKind.Hunter || w.IsSite)
                {
                    continue;
                }

                lodgeFood += world.FoodIn(w.Store);
                swollen |= world.BufferIsSwollen(w);
                lodgeTiles.Add(w.Tile);
            }

            lodgePeak = Math.Max(lodgePeak, lodgeFood);
            if (lodgeFoodLast >= 0)
            {
                if (lodgeFood < lodgeFoodLast) { carriedOut += lodgeFoodLast - lodgeFood; }
                if (lodgeFood > lodgeFoodLast) { hunts++; }
            }

            lodgeFoodLast = lodgeFood;
            bool met = world.LimitIsMet(Goods.Meat);
            if (swollen) { swollenTicks++; }
            if (met) { metTicks++; }

            int seated = 0;
            foreach (Villager v in world.Villagers)
            {
                if (!v.Alive || !v.HasJob || world.FindWorkplace(v.WorkplaceId) is not Workplace at)
                {
                    continue;
                }

                if (at.Kind == JobKind.Hunter)
                {
                    seated++;
                    seatTicks++;
                    if (met) { seatTicksMet++; }
                    if (v.State is VillagerState.ClearingABuffer or VillagerState.HaulingToStore) { carrierTicks++; }
                    if (v.State is VillagerState.Hunting or VillagerState.TravelingToGame) { huntTicks++; }
                }
                else if (at.Kind == JobKind.Marketer && v.State == VillagerState.CollectingForMarket && lodgeTiles.Contains(new GridPos(v.ErrandX, v.ErrandY)))
                {
                    marketerTicks++;
                }
            }

            if (met && swollen && seated == 0)
            {
                stallTicks++;
                if (firstStallYear == 0) { firstStallYear = t / config.TicksPerYear + 1; }
            }

            if (Environment.GetEnvironmentVariable("ZZ_TRACE") == "1" && t % config.TicksPerYear == 0)
            {
                _o.WriteLine($"ZZY {arm} {limx} {seed} Y{t / config.TicksPerYear + 1} pop {world.Population} lodgeFood {lodgeFood} swollen {swollen} met {met} meatHeld {world.HeldAgainstItsLimit(Goods.Meat)} seated {seated} wants {LabourQuota.For(world).For(JobKind.Hunter)}");
            }
        }

        int starved = world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Starvation);
        int frozen = world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Cold);
        _o.WriteLine($"ZZL {arm} {limx} {seed} alive {world.Population} peak {peak} starved {starved} frozen {frozen} " +
            $"lodgePeak {lodgePeak} swollenTicks {swollenTicks} swollenYears {swollenTicks / (double)config.TicksPerYear:F2} metTicks {metTicks} " +
            $"stallTicks {stallTicks} firstStallYear {firstStallYear} seatTicks {seatTicks} seatTicksMet {seatTicksMet} carrierTicks {carrierTicks} huntTicks {huntTicks} " +
            $"marketerTicks {marketerTicks} carriedOut {carriedOut} hunts {hunts} meatStored {world.InStores(Goods.Meat)}");
    }

    public static IEnumerable<object[]> Runs()
    {
        string[] arms = (Environment.GetEnvironmentVariable("ZZ_ARMS") ?? "lodge,every").Split(',');
        string[] limxs = (Environment.GetEnvironmentVariable("ZZ_LIMX") ?? "x1,x10").Split(',');
        ulong[] played = new ulong[] { 12345, 1, 2, 3, 7, 11 }
            .Concat(Enumerable.Range(100, 24).Select(x => (ulong)x)).ToArray();
        ulong[] every = Enumerable.Range(1, 12).Select(x => (ulong)x).Append(12345UL).ToArray();
        if (Environment.GetEnvironmentVariable("ZZ_SEEDS") is string only) { every = played = only.Split(',').Select(ulong.Parse).ToArray(); }
        foreach (string arm in arms)
        {
            foreach (string limx in limxs)
            {
                foreach (ulong s in arm == "every" ? every : played)
                {
                    yield return new object[] { arm, limx, s };
                }
            }
        }
    }
}
