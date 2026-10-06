using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// ⛔ D515's MEASUREMENT — the first-winter firewood race. Kept in tools/harness/, COPIED into
// tests/Bclone.Sim.Tests/ to run and DELETED from there before any commit. Never committed under tests/.
//
//   cp tools/harness/ZzFirstWinter.cs tests/Bclone.Sim.Tests/
//   dotnet test tests/Bclone.Sim.Tests -c Release --filter ZzFirstWinter --logger "console;verbosity=detailed"
//   rm tests/Bclone.Sim.Tests/ZzFirstWinter.cs
//
// Arms: `everyfix` is FoodConservationTests' founding (fixture config, no limits, lodge + fishery + farm
// raised free at t0); `every` is the same on the shipped config and starting limits; `played` is
// ColdStartTests.PlayTheOpening alone; `farm0` marks a farmhouse (a site the builders must raise) at t0
// and paints its field; `farm2` does the same at the start of year 2. One `ZZW` line per run.
// ZZ_ARMS=a,b and ZZ_SEEDS=1,2 narrow the runs. `housed@w1` is homes standing / living households on the
// first tick of winter in Year 1, with the logs felled by then.
public sealed class ZzFirstWinter
{
    private readonly ITestOutputHelper _o;

    public ZzFirstWinter(ITestOutputHelper o) => _o = o;

    [Theory]
    [MemberData(nameof(Runs))]
    public void Measure(string arm, ulong seed)
    {
        SimConfig config = (arm == "everyfix" ? VillageFixtures.Village : ShippedConfig.Load()) with { Seed = seed };
        SimLoop loop = SimFactory.CreatePhase0(config, new Bclone.Sim.Logging.InMemoryLogSink());
        SimWorld world = loop.World;
        if (arm != "everyfix")
        {
            var limits = ShippedConfig.Load().StartingStockLimits;
            for (int id = 0; id < world.GoodsCatalog.Count; id++)
            {
                if (limits.TryGetValue(world.GoodsCatalog[id].Name, out int limit))
                {
                    world.SetStockLimit((Goods)id, limit);
                }
            }
        }

        ColdStartTests.PlayTheOpening(world);
        if (arm is "every" or "everyfix")
        {
            HuntingTests.RaiseALodgeFor(world);
            FishingTests.RaiseAFishery(world);
            Workplace farm = FarmFixtures.RaiseAFarm(world);
            FarmFixtures.GiveItGround(world, farm, 3);
        }

        int ground = -1;
        int farmStood = -1, firstSplit = -1, hutStood = -1;
        string housedAtWinter = "?";
        int perYear = config.TicksPerYear, perSeason = config.TicksPerSeason;
        var winters = new List<string>();
        var turns = new List<string>();
        int warned = 0;
        int alive10 = -1;
        for (int t = 0; t < perYear * 50; t++)
        {
            if ((arm == "farm0" && t == 0) || (arm == "farm2" && t == perYear))
            {
                GridPos at = FarmFixtures.ClearGroundNear(world);
                if (world.Mark(BuildingKind.Farmhouse, at).Allowed)
                {
                    Workplace site = world.Workplaces.Last(w => w.Construction?.Kind == BuildingKind.Farmhouse);
                    ground = FarmFixtures.GiveItGround(world, site, 3);
                }
            }

            loop.StepOnce();
            int now = (int)world.Tick;

            if (farmStood < 0 && world.Workplaces.Any(w => w.Kind == JobKind.Farmer && !w.IsSite))
            {
                farmStood = now;
            }

            if (hutStood < 0 && world.Workplaces.Any(w => w.Kind == JobKind.Woodcutter && !w.IsSite))
            {
                hutStood = now;
            }

            if (now == perYear - perSeason)
            {
                housedAtWinter = $"{world.Households.Count(h => world.LivingMembersOf(h) > 0 && h.HasHome)}/{world.Households.Count(h => world.LivingMembersOf(h) > 0)} felled {world.LogsEverFelled}";
            }

            if (firstSplit < 0 && world.LogsEverSplit > 0)
            {
                firstSplit = now;
            }

            if (now % perSeason == 0 && now < perYear * 5)
            {
                SimClock clock = world.Clock;
                LabourQuota quota = LabourQuota.For(world);
                if (now < perYear * 3)
                {
                    turns.Add($"{clock.Season.ToString()[..2]}{clock.Year}:w{LabourQuota.WoodcuttersWanted(world)}/{quota.Woodcutters}f{quota.Farmers}");
                }

                if (clock.IsWinter)
                {
                    winters.Add($"y{clock.Year}:{InReach(world)}/{Burn(world)}");
                }
                else if (clock.Season is Season.Summer or Season.Fall
                    && quota.Woodcutters == 0 && InReach(world) < Burn(world))
                {
                    warned++;
                }
            }

            if (now == perYear * 10)
            {
                alive10 = world.Population;
            }
        }

        var coldByYear = world.Villagers
            .Where(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Cold && v.DiedAtTick is not null)
            .GroupBy(v => (int)(v.DiedAtTick!.Value / (ulong)perYear) + 1)
            .OrderBy(g => g.Key)
            .Select(g => $"y{g.Key}:{g.Count()}");
        int starved = world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Starvation);
        _o.WriteLine(
            $"ZZW {arm} {seed} ground {ground} farm {farmStood} split {firstSplit} "
            + $"winters [{string.Join(" ", winters)}] cold [{string.Join(" ", coldByYear)}] starved {starved} "
            + $"hut {hutStood} housed@w1 {housedAtWinter} warnable {warned} alive10 {alive10} alive50 {world.Population} turns [{string.Join(" ", turns)}]");
    }

    // The firewood the homes can reach: their own, the stores', and the heaps while some store has room.
    private static int InReach(SimWorld world)
    {
        int homes = 0;
        foreach (Household h in world.Households)
        {
            if (world.LivingMembersOf(h) > 0)
            {
                homes += h.Stockpile.Firewood;
            }
        }

        return homes + world.FirewoodInWarehouses()
            + (world.SomewhereToPut(Goods.Firewood) ? world.FirewoodOnTheGround() : 0);
    }

    private static int Burn(SimWorld world) =>
        world.Households.Count(h => world.LivingMembersOf(h) > 0)
        * VillageEconomy.FirewoodPerHouseholdPerWinter(world.Config);

    public static IEnumerable<object[]> Runs()
    {
        string[] arms = (Environment.GetEnvironmentVariable("ZZ_ARMS") ?? "everyfix,every,played,farm0,farm2").Split(',');
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
