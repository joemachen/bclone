using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// D641's gate item 2 (DESIGN §4, the end of Phase 5): which Phase 5 chains does any pinned
// golden actually reach? Replays each years-long golden's village exactly — the final hash is
// asserted against the golden's own constant, so the run measured IS the run pinned — and samples
// it once a day for every Phase 5 building, good, beast, bridge, boat, band and repair it ever saw.
// Prints one ZZG line per run. Copy into the test project, `--filter ZzGoldens`, delete.
public sealed class ZzGoldens
{
    private readonly ITestOutputHelper _output;

    public ZzGoldens(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData("fixture50", 17390278967619327199UL)]   // StockLimitTests.FixtureFiftyYearHash
    [InlineData("shipped50", 12295009579609975522UL)]   // StockLimitTests.ShippedFiftyYearHash
    [InlineData("fixture50-noskill", 3828778980944462616UL)]   // SkillTests, ComputeIgnoringSkills
    [InlineData("shipped50-noskill", 14563583951307060232UL)]  // SkillTests, ComputeIgnoringSkills
    [InlineData("farm20", 12490923102850685382UL)]      // FarmGoldenTests.SeamGoldenHash
    public void ZzGolden(string run, ulong golden)
    {
        SimConfig config = run switch
        {
            "fixture50" => VillageFixtures.Village,
            "shipped50" => ShippedConfig.EstablishedWithNoLimitsSet(),
            "fixture50-noskill" => NoSkill(VillageFixtures.Village),
            "shipped50-noskill" => NoSkill(ShippedConfig.Established()),
            "farm20" => VillageFixtures.Village,
            _ => throw new ArgumentException(run),
        };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        int years = 50;
        if (run == "farm20")
        {
            years = 20;
            Workplace farm = FarmFixtures.RaiseAFarm(world);
            FarmFixtures.GiveItGround(world, farm, reach: 2);
            for (int dy = -6; dy <= 6; dy++)
            {
                for (int dx = -6; dx <= 6; dx++)
                {
                    var at = new GridPos(farm.Tile.X + dx, farm.Tile.Y + dy);
                    if (world.Map.Contains(at) && world.Zones.WorkGroundOwner(at) == 0)
                    {
                        world.PaintHarvest(at);
                    }
                }
            }
        }

        var siteEver = new SortedDictionary<JobKind, int>();
        var standingMax = new SortedDictionary<JobKind, int>();
        var workedDays = new SortedDictionary<JobKind, int>();
        var storeMax = new SortedDictionary<StoreKind, int>();
        var heldMax = new SortedDictionary<Goods, int>();
        int bridgesMax = 0, herdMax = 0, peak = 0, wellsMax = 0;
        int homesMax = 0, doorsMax = 0;
        bool boatEver = false;
        var repaired = new HashSet<int>();
        long total = (long)config.TicksPerYear * years;

        for (long t = 0; t < total; t++)
        {
            loop.StepOnce();
            if (t % config.TicksPerDay != 0)
            {
                continue;
            }

            var standing = new Dictionary<JobKind, int>();
            var held = new Dictionary<Goods, int>();
            int herd = 0;
            foreach (Workplace w in world.Workplaces)
            {
                if (w.IsSite)
                {
                    siteEver[w.Kind] = siteEver.GetValueOrDefault(w.Kind) + 0;
                    continue;
                }

                standing[w.Kind] = standing.GetValueOrDefault(w.Kind) + 1;
                if (w.WorkerIds.Count > 0)
                {
                    workedDays[w.Kind] = workedDays.GetValueOrDefault(w.Kind) + 1;
                }

                if (w.RepairedAtTick > 0)
                {
                    repaired.Add(w.Id);
                }

                herd += w.HerdHead;
                Add(held, w.Store);
            }

            var stores = new Dictionary<StoreKind, int>();
            foreach (StoreBuilding s in world.StoreBuildings)
            {
                stores[s.Kind] = stores.GetValueOrDefault(s.Kind) + 1;
                Add(held, s.Store);
            }

            foreach (Household h in world.Households)
            {
                Add(held, h.Stockpile);
            }

            foreach (var (k, n) in standing)
            {
                standingMax[k] = Math.Max(standingMax.GetValueOrDefault(k), n);
            }

            foreach (var (k, n) in stores)
            {
                storeMax[k] = Math.Max(storeMax.GetValueOrDefault(k), n);
            }

            foreach (var (g, n) in held)
            {
                heldMax[g] = Math.Max(heldMax.GetValueOrDefault(g), n);
            }

            bridgesMax = Math.Max(bridgesMax, world.Bridges.Count);
            herdMax = Math.Max(herdMax, herd);
            peak = Math.Max(peak, world.Villagers.Count);
            wellsMax = Math.Max(wellsMax, world.Wells.Count);
            homesMax = Math.Max(homesMax, world.Households.Count(h => h.HasHome));
            doorsMax = Math.Max(doorsMax, world.Households.Count(h => h.HasHome && world.DoorOf(h) is not null));
            boatEver |= world.Boat is not null;
        }

        ulong hash = run.EndsWith("-noskill", StringComparison.Ordinal)
            ? StateHash.ComputeIgnoringSkills(world)
            : StateHash.Compute(world);

        string Join<TK>(IDictionary<TK, int> d) where TK : notnull =>
            string.Join(" ", d.Select(kv => $"{kv.Key}={kv.Value}"));

        _output.WriteLine($"ZZG {run} years={years} hash={hash} golden={golden} match={hash == golden}");
        _output.WriteLine($"ZZG {run} standing-max: {Join(standingMax)}");
        _output.WriteLine($"ZZG {run} sites-ever: {string.Join(" ", siteEver.Keys)}");
        _output.WriteLine($"ZZG {run} worked-days: {Join(workedDays)}");
        _output.WriteLine($"ZZG {run} stores-max: {Join(storeMax)}");
        _output.WriteLine($"ZZG {run} held-max: {Join(heldMax)}");
        _output.WriteLine(
            $"ZZG {run} bridges={bridgesMax} herd={herdMax} boat={boatEver} bands={world.BandsEverKnocked} "
            + $"repaired={repaired.Count} wells={wellsMax} homes={homesMax} doors={doorsMax} peak={peak} alive={world.Villagers.Count}");

        Assert.Equal(golden, hash);
    }

    private static SimConfig NoSkill(SimConfig config) => config with
    {
        MasterySpeedBonusPercent = 0,
        FoundingMasters = 0,
        FoundingJourneymen = 0,
        SeededRhythm = false,
    };

    private static void Add(Dictionary<Goods, int> held, Stockpile store)
    {
        foreach (Goods g in Enum.GetValues<Goods>())
        {
            int n = store[g];
            if (n > 0)
            {
                held[g] = held.GetValueOrDefault(g) + n;
            }
        }
    }
}
