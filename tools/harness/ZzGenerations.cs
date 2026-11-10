using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// House tiers piece 1 (`specs/house-tiers.md §10` arm B, §14.1, D718): the family-tree gates' timing. Per valley, the year
// each generation is first born, and the year the village first holds 15 / 20 / 25 / 30 / 45 people, over ZZ_YEARS (100).
// The opening played as `ColdStartTests` does, a granary and a warehouse marked at Year 3, then unattended — ⚠️ a harness is
// not a player (D447) and no harness village grows the way Joe's do, so the populations are read off his saves as well
// (§4.4's table); the generations are what this measures.
// Copy in; `dotnet test tests/Bclone.Sim.Tests -c Release --filter ZzGenerations --logger "console;verbosity=detailed"`; delete.
public sealed class ZzGenerations
{
    private static readonly int[] Sizes = { 15, 20, 25, 30, 45 };

    private readonly ITestOutputHelper _o;

    public ZzGenerations(ITestOutputHelper o) => _o = o;

    [Theory]
    [MemberData(nameof(Runs))]
    public void Measure(ulong seed)
    {
        int years = int.Parse(Environment.GetEnvironmentVariable("ZZ_YEARS") ?? "100");
        bool fixture = Environment.GetEnvironmentVariable("ZZ_FIX") == "1";
        string arm = fixture ? "fixture" : "shipped";

        SimConfig config = (fixture ? VillageFixtures.Village : ShippedConfig.Load()) with { Seed = seed };
        SimLoop loop = SimFactory.CreatePhase0(config, new Bclone.Sim.Logging.InMemoryLogSink());
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);

        var firstBorn = new SortedDictionary<int, int>();
        var firstHeld = new Dictionary<int, int>();
        var aliveAt = new List<int>();
        int peak = 0;
        int seen = world.Villagers.Count;
        for (int t = 0; t < config.TicksPerYear * years; t++)
        {
            if (t == config.TicksPerYear * 3)
            {
                MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 8);
                MarkSomewhereNear(world, BuildingKind.Warehouse, world.Map.FoundingSite, 8);
            }

            loop.StepOnce();
            int year = t / config.TicksPerYear + 1;
            for (int i = seen; i < world.Villagers.Count; i++)
            {
                int g = world.Villagers[i].Generation;
                if (g > 1 && !firstBorn.ContainsKey(g))
                {
                    firstBorn[g] = year;
                }
            }

            seen = world.Villagers.Count;
            peak = Math.Max(peak, world.Population);
            foreach (int size in Sizes)
            {
                if (world.Population >= size && !firstHeld.ContainsKey(size))
                {
                    firstHeld[size] = year;
                }
            }

            if ((t + 1) % (config.TicksPerYear * 10) == 0)
            {
                aliveAt.Add(world.Population);
            }
        }

        string gens = string.Join(" ", firstBorn.Select(kv => $"g{kv.Key}@{kv.Value}"));
        string held = string.Join(" ", Sizes.Select(s => $"p{s}@{(firstHeld.TryGetValue(s, out int y) ? y : -1)}"));
        _o.WriteLine($"ZZG {arm} {seed} peak {peak} alive/10y {string.Join(",", aliveAt)} {held} {gens}");
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
        IEnumerable<int> seeds = Enumerable.Range(1, 24).Append(12345);
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
