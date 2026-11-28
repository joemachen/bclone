using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

// ⛔ D941's DIAGNOSIS — kept in tools/harness/, COPIED into tests/Kith.Sim.Tests/ to run and DELETED before any commit (D420).
// Why the forage-only played opening starves more with health on (ZzHealth): every starvation with the dead person's state, days
// of illness left, plain seasons, age and their larder's food at the death; and every person ill for more than a season (stuck).
//   ZZ_SEEDS (2,15,17), ZZ_YEARS (30). ZZW lines.
public sealed class ZzHealthWhy
{
    private readonly ITestOutputHelper _o;

    public ZzHealthWhy(ITestOutputHelper o) => _o = o;

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Why(int seed)
    {
        if (Environment.GetEnvironmentVariable("ZZ_WHY") != "1") { return; }
        int years = int.Parse(Environment.GetEnvironmentVariable("ZZ_YEARS") ?? "30");
        bool on = Environment.GetEnvironmentVariable("ZZ_HEALTH") != "0";
        SimConfig shipped = ShippedConfig.Load();
        SimConfig config = (on ? shipped : shipped with { HealthPlainPercent = 0 }) with { Seed = (ulong)seed };
        SimLoop loop = SimFactory.CreatePhase0(config, new Kith.Sim.Logging.InMemoryLogSink());
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);
        var sickSince = new Dictionary<int, ulong>();
        var lastState = new Dictionary<int, (VillagerState S, int Sick, int Plain, int Food, int Hunger, int MaxTicks)>();
        var bedTicksThisYear = new int[years];
        var starvedYear = new int[years];
        for (int t = 0; t < config.TicksPerYear * years; t++)
        {
            if (t == config.TicksPerYear * 3)
            {
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 8);
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Warehouse, world.Map.FoundingSite, 8);
            }

            foreach (Villager v in world.Villagers)
            {
                if (v.Alive && !lastState.ContainsKey(-v.Id - 1))
                {
                    lastState[v.Id] = (v.State, v.SickDays, v.PlainSeasons, world.FoodIn(world.HouseholdOf(v).Stockpile), v.Hunger, v.TicksAtMaxHunger);
                }
            }

            loop.StepOnce();
            int y = t / config.TicksPerYear;
            foreach (Villager v in world.Villagers)
            {
                if (v.Alive && v.State == VillagerState.SickInBed) { bedTicksThisYear[y]++; }
                if (v.Alive && v.SickDays > 0) { sickSince.TryAdd(v.Id, world.Tick); }
                if (v.Alive && v.SickDays == 0) { sickSince.Remove(v.Id); }
                if (!v.Alive && v.State != VillagerState.Left && v.CauseOfDeath == CauseOfDeath.Starvation && lastState.TryGetValue(v.Id, out var was) && lastState.Remove(v.Id))
                {
                    starvedYear[y]++;
                    _o.WriteLine($"ZZW {(on ? "on" : "off")} seed {seed} starved Y{world.Clock.Year} {world.Clock.Season} {v.Name} age {v.AgeYears} {v.LifeStage} wasState {was.S} sickDays {was.Sick} plain {was.Plain} larderFood {was.Food} "
                        + $"villageFood {world.FoodTheVillageHolds()} alive {world.Population} inBedNow {world.Villagers.Count(x => x.Alive && x.State == VillagerState.SickInBed)}");
                }
            }
        }

        foreach ((int id, ulong since) in sickSince)
        {
            Villager v = world.FindVillager(id)!;
            if (v.Alive && world.Tick - since > (ulong)(config.TicksPerDay * config.DaysPerSeason))
            {
                _o.WriteLine($"ZZW {(on ? "on" : "off")} seed {seed} STUCK {v.Name} {v.LifeStage} age {v.AgeYears} state {v.State} sickDays {v.SickDays} since {(world.Tick - since) / (ulong)config.TicksPerDay} days action {v.ActionTicksRemaining} school {v.SchoolId}");
            }
        }

        _o.WriteLine($"ZZW {(on ? "on" : "off")} seed {seed} bedTicksByYear {string.Join(",", bedTicksThisYear)} starvedByYear {string.Join(",", starvedYear)}");
    }

    public static IEnumerable<object[]> Seeds()
    {
        IEnumerable<int> seeds = new[] { 2, 15, 17 };
        if (Environment.GetEnvironmentVariable("ZZ_SEEDS") is string listed)
        {
            seeds = listed.Split(',').Select(int.Parse);
        }

        return seeds.Select(s => new object[] { s });
    }
}
