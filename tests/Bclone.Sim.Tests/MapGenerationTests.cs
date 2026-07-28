using System.Text;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The valley is generated from the seed — <c>specs/seeded-map-generation.md</c> (D18).
/// </summary>
public sealed class MapGenerationTests
{
    private readonly ITestOutputHelper _output;

    public MapGenerationTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static GeneratedMap Generate(SimConfig config, ulong seed) =>
        MapGenerator.Generate(config, new DeterministicRandom(seed));

    [Fact]
    public void SameSeedGivesTheSameValley()
    {
        ulong hashA = StateHash.MixMap(0UL, Generate(Config, 12345UL));
        ulong hashB = StateHash.MixMap(0UL, Generate(Config, 12345UL));

        Assert.Equal(hashA, hashB);
    }

    [Fact]
    public void DifferentSeedsGiveDifferentValleys()
    {
        // Anti-vacuity (D7). Without this the test above passes just as happily over a
        // generator that ignores its seed and returns the same valley every time —
        // which would be the most expensive kind of green.
        ulong hashA = StateHash.MixMap(0UL, Generate(Config, 1UL));
        ulong hashB = StateHash.MixMap(0UL, Generate(Config, 2UL));

        Assert.NotEqual(hashA, hashB);
    }

    [Fact]
    public void TheWorldIsGeneratedFromTheRunsOwnSeed()
    {
        // The point of D18: quoting one number reproduces an entire run, world
        // included. Two worlds built from the same seed must agree about the valley
        // before anybody has taken a step.
        SimWorld a = SimFactory.CreatePhase0(Config, new InMemoryLogSink(), seedOverride: 777UL).World;
        SimWorld b = SimFactory.CreatePhase0(Config, new InMemoryLogSink(), seedOverride: 777UL).World;
        SimWorld c = SimFactory.CreatePhase0(Config, new InMemoryLogSink(), seedOverride: 778UL).World;

        Assert.Equal(StateHash.MixMap(0UL, a.Map), StateHash.MixMap(0UL, b.Map));
        Assert.NotEqual(StateHash.MixMap(0UL, a.Map), StateHash.MixMap(0UL, c.Map));
    }

    [Fact]
    public void DrawOrderIsTheContract()
    {
        // A golden hash, and the failure it exists for is silent: reordering the
        // generator's draws shifts every subsequent value, so every seed anybody has
        // written down stops reproducing its world. That is a save-breaking change
        // wearing the clothes of a tidy-up, so it should fail the build.
        //
        // If this breaks and the change was DELIBERATE, update the constant and say so
        // in the commit. If it broke by accident, that is the bug.
        ulong actual = StateHash.MixMap(0UL, Generate(Config, 12345UL));
        _output.WriteLine($"golden map hash for seed 12345: {actual}UL");

        Assert.Equal(GoldenMapHash, actual);
    }

    /// <summary>Seed 12345 on the village config. See <see cref="DrawOrderIsTheContract"/>.</summary>
    private const ulong GoldenMapHash = 2208871881858546589UL;

    [Fact]
    public void NothingIsGeneratedOutsideTheValley()
    {
        for (ulong seed = 1; seed <= 50; seed++)
        {
            GeneratedMap map = Generate(Config, seed);

            foreach (GridPos site in map.ForageSites)
            {
                Assert.True(map.Contains(site), $"Seed {seed}: forage site {site} is off the map.");
            }

            foreach (GridPos stand in map.TreeStands)
            {
                Assert.True(map.Contains(stand), $"Seed {seed}: tree stand {stand} is off the map.");
            }

            Assert.True(map.Contains(map.FoundingSite), $"Seed {seed}: founding site is off the map.");
        }
    }

    [Fact]
    public void TheVillageIsNeverFoundedInTheRiver()
    {
        for (ulong seed = 1; seed <= 50; seed++)
        {
            GeneratedMap map = Generate(Config, seed);
            Assert.NotEqual(Terrain.Water, map.TerrainAt(map.FoundingSite));
        }
    }

    [Fact]
    public void ForageSitesStaySpread()
    {
        // D24, which is a record of what happens when they do not: the extra sites all
        // went out to the map edges, every home near the middle competed for the one
        // original patch, and tightening catchment left central families idle beside a
        // full thicket while the outskirts had nothing in reach.
        //
        // The generator guarantees this by construction — evenly spaced slots with
        // jitter, rather than free angles — so this asserts the guarantee rather than
        // hoping the draw was kind.
        SimConfig config = Config;

        for (ulong seed = 1; seed <= 50; seed++)
        {
            GeneratedMap map = Generate(config, seed);

            int east = 0, west = 0, north = 0, south = 0;
            foreach (GridPos site in map.ForageSites)
            {
                if (site.X > map.FoundingSite.X) east++;
                if (site.X < map.FoundingSite.X) west++;
                if (site.Y > map.FoundingSite.Y) south++;
                if (site.Y < map.FoundingSite.Y) north++;
            }

            Assert.True(east > 0 && west > 0, $"Seed {seed}: sites are all on one side ({west}W/{east}E).");
            Assert.True(north > 0 && south > 0, $"Seed {seed}: sites are all on one side ({north}N/{south}S).");
        }
    }

    [Fact]
    public void EveryValleyMeetsTheEconomysDistanceBudget()
    {
        // The guarantee that replaces hand-placement's implicit one, asserted directly
        // rather than through survival — so a failure says WHICH constraint broke
        // rather than "the village died, good luck".
        SimConfig config = Config;
        int budget = VillageEconomy.MaxHomeToWorkTiles(config);

        for (ulong seed = 1; seed <= 30; seed++)
        {
            SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink(), seed).World;

            foreach (Household household in world.Households)
            {
                int nearest = int.MaxValue;
                foreach (GridPos site in world.Map.ForageSites)
                {
                    nearest = Math.Min(nearest, household.HomePosition.ManhattanDistanceTo(site));
                }

                Assert.True(nearest <= budget,
                    $"Seed {seed}: {household.Name} is {nearest} tiles from work, budget {budget}.");
            }
        }
    }

    [Fact]
    public void EverySeedProducesAValleyAVillageSurvivesIn()
    {
        // THE property test, and the thing a generated world needs that a hand-placed
        // one never did: hand-placement was checked once by a human, and generation has
        // to be right for valleys nobody has ever looked at.
        SimConfig config = Config;
        var results = new List<string>();

        for (ulong seed = 1; seed <= 12; seed++)
        {
            SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink(), seed);

            int lowest = int.MaxValue;
            int peak = 0;
            for (int year = 1; year <= 200; year++)
            {
                loop.Step(config.TicksPerYear);
                peak = Math.Max(peak, loop.World.Population);
                if (year >= 40)
                {
                    lowest = Math.Min(lowest, loop.World.Population);
                }
            }

            results.Add($"seed {seed,2}: peak {peak,3}, low {lowest,3}, final {loop.World.Population,3}");

            Assert.True(loop.World.Population >= config.StartingPopulation,
                $"Seed {seed} died out — finished at {loop.World.Population}. " +
                $"({string.Join("; ", results)})");
        }

        foreach (string line in results)
        {
            _output.WriteLine(line);
        }
    }

    // ---------------------------------------------------------------
    //  Water you have to go round — specs/pathfinding-and-water.md (D40)
    // ---------------------------------------------------------------

    [Fact]
    public void NobodyEverStandsOnWater()
    {
        // The regression this whole slice exists to prevent, asserted every tick
        // rather than inferred from the village surviving.
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink(), seedOverride: 1UL);

        for (int i = 0; i < config.TicksPerYear * 60; i++)
        {
            loop.StepOnce();

            foreach (Villager villager in loop.World.Villagers)
            {
                if (!villager.Alive)
                {
                    continue;
                }

                Assert.NotEqual(Terrain.Water, loop.World.Map.TerrainAt(villager.Position));
            }
        }
    }

    [Fact]
    public void NothingIsEverBuiltOnWater()
    {
        // Homes, workplaces and stores alike. A building in the river is unreachable
        // by construction (TerrainCostField refuses to serve one), so this failing
        // would show up later as a village that mysteriously cannot make firewood —
        // which is exactly how it did show up on seed 1.
        SimConfig config = Config;

        for (ulong seed = 1; seed <= 20; seed++)
        {
            SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink(), seed);
            loop.Step(config.TicksPerYear * 60);
            SimWorld world = loop.World;

            foreach (Workplace workplace in world.Workplaces)
            {
                Assert.NotEqual(Terrain.Water, world.Map.TerrainAt(workplace.Position));
            }

            foreach (StoreBuilding store in world.StoreBuildings)
            {
                Assert.NotEqual(Terrain.Water, world.Map.TerrainAt(store.Position));
            }

            foreach (Household household in world.Households)
            {
                Assert.NotEqual(Terrain.Water, world.Map.TerrainAt(household.HomePosition));
            }
        }
    }

    [Fact]
    public void EveryVillageCanReachItsOwnBuildings()
    {
        // §6 of the pathfinding spec: until bridges exist (D40) the generator owes the
        // village a valley it can live in. A shed on the far bank is as useless as one
        // under water, and the difference is invisible on the map.
        SimConfig config = Config;

        for (ulong seed = 1; seed <= 20; seed++)
        {
            SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink(), seed).World;
            GridPos home = world.Households[0].HomePosition;

            foreach (StoreBuilding store in world.StoreBuildings)
            {
                Assert.True(world.TravelCost.CanReach(home, store.Position),
                    $"Seed {seed}: {store.Name} is cut off from the village.");
            }

            int reachableForage = 0;
            foreach (Workplace workplace in world.Workplaces)
            {
                if (workplace.Kind == JobKind.Forager
                    && world.TravelCost.CanReach(home, workplace.Position))
                {
                    reachableForage++;
                }
            }

            Assert.True(reachableForage > 0, $"Seed {seed}: no forage site is reachable at all.");
        }
    }

    [Fact]
    public void AWalkAcrossTheRiverIsLongerThanTheStraightLine()
    {
        // And the anti-vacuity twin below it — without that, a field that quietly
        // returned Manhattan distance everywhere would pass this happily.
        SimConfig config = Config;
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink(), seedOverride: 1UL).World;

        GridPos from = world.Households[0].HomePosition;
        int detoured = 0;

        foreach (Workplace workplace in world.Workplaces)
        {
            int path = world.TravelCost.Cost(from, workplace.Position);
            if (path == TravelCostField.Unreachable)
            {
                detoured++;
                continue;
            }

            int straight = from.ManhattanDistanceTo(workplace.Position) * TravelCostField.BaseTileCost;
            Assert.True(path >= straight, "A path cannot be shorter than the straight line.");

            if (path > straight)
            {
                detoured++;
            }
        }

        _output.WriteLine($"{detoured} of {world.Workplaces.Count} places cost more than the crow flies.");
        Assert.True(detoured > 0,
            "Every route on this seed was a straight line, so the river is costing nothing — " +
            "either the terrain is not being read, or this seed has no water in the way.");
    }

    [Fact]
    public void WithNoRiverEveryCostIsTheStraightLine()
    {
        // The anti-vacuity twin (D7). If the field were broken in the other direction —
        // always going the long way round, or always reporting unreachable — the test
        // above would still pass. This pins the other end: with nothing in the way, a
        // real path and plain distance must agree exactly.
        SimConfig config = Config with { RiverWidthTiles = 0 };
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink(), seedOverride: 1UL).World;

        GridPos from = world.Households[0].HomePosition;

        foreach (Workplace workplace in world.Workplaces)
        {
            int path = world.TravelCost.Cost(from, workplace.Position);
            int straight = from.ManhattanDistanceTo(workplace.Position) * TravelCostField.BaseTileCost;

            Assert.Equal(straight, path);
        }
    }

    [Fact]
    public void AreTheseValleysWorthPlayingTwice()
    {
        // Not an assertion — a contact sheet. The one question about generated worlds
        // that no test can answer is whether they are interesting, and the honest
        // thing is to put them where a human will look rather than declare it done on
        // the strength of the property tests passing (spec §9).
        SimConfig config = Config;

        for (ulong seed = 1; seed <= 3; seed++)
        {
            SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink(), seed).World;
            _output.WriteLine($"---- seed {seed} ----");
            _output.WriteLine(Draw(world, radius: 14));
            _output.WriteLine(string.Empty);
        }
    }

    /// <summary>A small ASCII window on the valley around the settlement.</summary>
    private static string Draw(SimWorld world, int radius)
    {
        GridPos centre = world.Map.FoundingSite;
        var text = new StringBuilder();

        for (int y = centre.Y - radius; y <= centre.Y + radius; y++)
        {
            for (int x = centre.X - radius * 2; x <= centre.X + radius * 2; x++)
            {
                var here = new GridPos(x, y);
                text.Append(SymbolAt(world, here));
            }

            text.AppendLine();
        }

        text.AppendLine("  . grass   ~ water   \" forest   F forage   T stand   h home   G granary   S shed   M market");
        return text.ToString();
    }

    private static char SymbolAt(SimWorld world, GridPos here)
    {
        foreach (StoreBuilding building in world.StoreBuildings)
        {
            if (building.Position == here)
            {
                return building.Kind switch
                {
                    StoreKind.Granary => 'G',
                    StoreKind.Shed => 'S',
                    _ => 'M',
                };
            }
        }

        foreach (Workplace workplace in world.Workplaces)
        {
            if (workplace.Position == here)
            {
                return workplace.Kind switch
                {
                    JobKind.Forager => 'F',
                    JobKind.Logger => 'T',
                    JobKind.Woodcutter => 'W',
                    _ => 'M',
                };
            }
        }

        foreach (Household household in world.Households)
        {
            if (household.HomePosition == here)
            {
                return 'h';
            }
        }

        return world.Map.TerrainAt(here) switch
        {
            Terrain.Water => '~',
            Terrain.Forest => '"',
            _ => '.',
        };
    }
}
