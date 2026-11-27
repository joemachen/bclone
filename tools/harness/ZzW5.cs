using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

// ⛔ D928's MEASUREMENT HARNESS — kept in tools/harness/, COPIED into tests/Kith.Sim.Tests/ to run and DELETED from there
// before any commit (D420). `skills-for-every-trade.md §9` step 7: what W5's rows do to a village over a century.
//
// The opening is ZzLimits' "every": the played opening on the shipped file, a hunter's lodge, a fishery and a farm raised
// free (an unattended village marks none of them, so W5's trades would never be held), a granary and a warehouse at Year 3.
// Seeds 1–36 (ZZ_SEEDS=a,b narrows), a century (ZZ_YEARS). Arms: ZZ_W5=0 takes W5's rows out (skills 12–20, techniques
// 4–10, the great library's latch can then never fire) — the village as `main` had it; ZZ_W5=1 (default) is the shipped file.
//
// One ZZW line a run: alive, peak, starved, cold; fish, meat, wheat ever brought in; masters reached here per new trade
// (fisher hunter smith quarrier miner herdsman merchant keeper groundskeeper) with the mean age at mastery; techniques known
// at the end and ever lost; the year every technique was known (−1 never); carriers of reading the water at the end.
//
// Copy in; `dotnet test tests/Kith.Sim.Tests -c Release --filter ZzW5 --logger "console;verbosity=detailed"` per arm; delete.
public sealed class ZzW5
{
    private readonly ITestOutputHelper _o;

    public ZzW5(ITestOutputHelper o) => _o = o;

    [Theory]
    [MemberData(nameof(Runs))]
    public void Measure(ulong seed)
    {
        bool w5 = Environment.GetEnvironmentVariable("ZZ_W5") != "0";
        int years = int.Parse(Environment.GetEnvironmentVariable("ZZ_YEARS") ?? "100");
        SimConfig shipped = ShippedConfig.Load();
        SimConfig config = (w5 ? shipped : shipped with
        {
            Skills = shipped.Skills.Where(s => s.Id <= 11).ToList(),
            Techniques = shipped.Techniques.Where(t => t.Id <= 3).ToList(),
        }) with { Seed = seed };

        SimLoop loop = SimFactory.CreatePhase0(config, new Kith.Sim.Logging.InMemoryLogSink());
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);
        HuntingTests.RaiseALodgeFor(world);
        FishingTests.RaiseAFishery(world);
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        FarmFixtures.GiveItGround(world, farm, 3);

        JobKind[] trades =
        {
            JobKind.Fisher, JobKind.Hunter, JobKind.Smith, JobKind.Quarrier, JobKind.Miner, JobKind.Herdsman,
            JobKind.Merchant, JobKind.TavernKeeper, JobKind.Groundskeeper,
        };
        var masters = new int[trades.Length];
        var ageSum = new long[trades.Length];
        var seen = new HashSet<(int Villager, int Skill)>();
        var was = new KnowledgeState[world.TechniquesCatalog.Count];
        int lost = 0, allKnownYear = -1, peak = 0;

        for (int t = 0; t < config.TicksPerYear * years; t++)
        {
            if (t == config.TicksPerYear * 3)
            {
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 8);
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Warehouse, world.Map.FoundingSite, 8);
            }

            loop.StepOnce();
            peak = Math.Max(peak, world.Population);

            if (t % config.TicksPerDay != 0)
            {
                continue;
            }

            for (int k = 0; k < trades.Length; k++)
            {
                if (world.SkillGrownBy(trades[k]) is not SkillRow skill)
                {
                    continue;
                }

                foreach (Villager v in world.Villagers)
                {
                    if (v.Alive && v.FindProgressIn(skill.Id) is { MasteredHere: true } && seen.Add((v.Id, skill.Id)))
                    {
                        masters[k]++;
                        ageSum[k] += v.AgeYears;
                    }
                }
            }

            for (int id = 0; id < was.Length; id++)
            {
                if (was[id] != KnowledgeState.Unknown && world.KnowledgeStates[id] == KnowledgeState.Unknown)
                {
                    lost++;
                }

                was[id] = world.KnowledgeStates[id];
            }

            if (allKnownYear < 0 && world.GreatLibraryLearned)
            {
                allKnownYear = world.Clock.Year;
            }
        }

        int carriers = 0;
        if (w5 && world.TechniquesCatalog.Count > 4)
        {
            TechniqueRow water = world.TechniquesCatalog[4];
            carriers = world.Villagers.Count(v => v.Alive && world.Carries(v, water)
                && v.FindProgressIn(water.Skill) is { Mastered: false });
        }

        string per = string.Join(" ", trades.Select((tr, k) => $"{tr}={masters[k]}@{(masters[k] == 0 ? 0 : ageSum[k] / masters[k])}"));
        _o.WriteLine($"ZZW {(w5 ? "w5" : "base")} seed {seed} alive {world.Population} peak {peak} "
            + $"starved {world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Starvation)} cold {world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Cold)} fish {world.FoodEverProducedOf(Goods.Fish)} "
            + $"meat {world.FoodEverProducedOf(Goods.Meat)} wheat {world.WheatEverReaped} "
            + $"known {world.TechniquesKnown()}/{world.TechniquesCatalog.Count} lost {lost} allknown {allKnownYear} "
            + $"carriers {carriers} {per}");
    }

    public static IEnumerable<object[]> Runs()
    {
        IEnumerable<int> seeds = Enumerable.Range(1, 36);
        if (Environment.GetEnvironmentVariable("ZZ_SEEDS") is string listed)
        {
            seeds = listed.Split(',').Select(int.Parse);
        }

        return seeds.Select(s => new object[] { (ulong)s });
    }
}
