using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The residential brush — <c>specs/building-placement.md §12</c> (D42), slice 3.
/// </summary>
/// <remarks>
/// The player paints where the village may live; the village builds inside it when it
/// has a reason to, and asks for more when it runs out. <b>Player intent as sim
/// state</b> — saved, hashed, and part of the determinism contract.
/// </remarks>
public sealed class ResidentialZoneTests
{
    private readonly ITestOutputHelper _output;

    public ResidentialZoneTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimLoop Build(SimConfig config, ulong? seed = null) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink(), seed);

    [Fact]
    public void TheExilesArriveHavingChosenWhereToLive()
    {
        // A village founded with nothing painted could never build a house, and the
        // first thing the game asked of a player would be a decision they had no basis
        // for (spec §12.7). So the starter zone is both a courtesy and a tutorial.
        SimWorld world = Build(Config).World;

        _output.WriteLine($"{world.Zones.ResidentialTiles} tiles painted at the founding.");
        Assert.True(world.Zones.ResidentialTiles > 0);

        foreach (Household household in world.Households)
        {
            Assert.True(world.Zones.IsResidential(household.Home()),
                $"{household.Name} was founded outside the residential land.");
        }
    }

    [Fact]
    public void HomesAreOnlyEverBuiltOnPaintedLand()
    {
        // The whole claim of the brush. If a single home appears outside it, the player
        // is not deciding anything.
        SimConfig config = Config;
        SimLoop loop = Build(config);

        for (int year = 1; year <= 150; year++)
        {
            loop.Step(config.TicksPerYear);

            foreach (Household household in loop.World.Households)
            {
                if (!household.HasHome)
                {
                    continue;
                }

                Assert.True(loop.World.Zones.IsResidential(household.Home()),
                    $"{household.Name} stands at {household.Home()}, which nobody painted.");
            }

            // ⭐ AND THE HOUSES STILL BEING RAISED, which is where the claim now has to be
            // made (D102). A house is a construction site before it is a home, so checking
            // only standing homes would let a site be marked on unpainted land and only
            // notice a year later — or never, if it was never finished. The brush's claim is
            // about where the village DECIDES to build, and that decision is the site.
            foreach (Workplace site in loop.World.Workplaces)
            {
                if (site.Construction?.Kind != BuildingKind.Home)
                {
                    continue;
                }

                Assert.True(loop.World.Zones.IsResidential(site.Position),
                    $"A house is being raised at {site.Position}, which nobody painted.");
            }
        }
    }

    [Fact]
    public void NoPaintedLandMeansNoNewHomes()
    {
        // "No need, no houses" has a twin: no room, no houses. Erase the unbuilt land
        // and the village stops spreading — which is the constraint that makes painting
        // a decision rather than a formality.
        SimConfig config = Config;
        SimLoop loop = Build(config);
        loop.Step(config.TicksPerYear * 10);

        SimWorld world = loop.World;

        // Erase everything nobody has built on yet.
        for (int y = world.Map.MinY; y < world.Map.MinY + world.Map.Height; y++)
        {
            for (int x = world.Map.MinX; x < world.Map.MinX + world.Map.Width; x++)
            {
                var tile = new GridPos(x, y);
                if (!world.Zones.IsResidential(tile))
                {
                    continue;
                }

                bool lived = false;
                foreach (Household household in world.Households)
                {
                    if (household.Home() == tile)
                    {
                        lived = true;
                    }
                }

                if (!lived)
                {
                    world.EraseResidential(tile);
                }
            }
        }

        int households = world.Households.Count;
        loop.Step(config.TicksPerYear * 60);

        _output.WriteLine($"{households} households when the land ran out, {world.Households.Count} sixty years on.");
        Assert.Equal(households, world.Households.Count);
    }

    [Fact]
    public void TheVillageAsksWhenItRunsOutOfRoom()
    {
        // The other half of the brush (D42): the game says when a decision is due
        // rather than expecting the player to notice a couple quietly not moving out.
        SimConfig config = Config;
        var sink = new InMemoryLogSink();
        SimLoop loop = SimFactory.CreatePhase0(config, sink);
        loop.Step(config.TicksPerYear * 10);

        SimWorld world = loop.World;
        for (int y = world.Map.MinY; y < world.Map.MinY + world.Map.Height; y++)
        {
            for (int x = world.Map.MinX; x < world.Map.MinX + world.Map.Width; x++)
            {
                var tile = new GridPos(x, y);
                bool lived = false;
                foreach (Household household in world.Households)
                {
                    if (household.Home() == tile)
                    {
                        lived = true;
                    }
                }

                if (!lived)
                {
                    world.EraseResidential(tile);
                }
            }
        }

        loop.Step(config.TicksPerYear * 60);

        string? asked = null;
        foreach (LogEntry entry in sink.Entries)
        {
            if (entry.Message.Contains("needs somewhere new to build", System.StringComparison.Ordinal))
            {
                asked = entry.Message;
                break;
            }
        }

        _output.WriteLine(asked ?? "(the village never asked)");
        Assert.True(asked is not null, "The village ran out of room and said nothing.");
        Assert.True(world.NeedsMoreResidentialLand);
    }

    [Fact]
    public void PaintingMoreLandLetsTheVillageGrowAgain()
    {
        // And the loop closes: the player answers, and the village builds. This is the
        // whole mechanic end to end.
        SimConfig config = Config;
        SimLoop loop = Build(config);
        loop.Step(config.TicksPerYear * 10);
        SimWorld world = loop.World;

        // Squeeze the village down to only the land it already stands on.
        for (int y = world.Map.MinY; y < world.Map.MinY + world.Map.Height; y++)
        {
            for (int x = world.Map.MinX; x < world.Map.MinX + world.Map.Width; x++)
            {
                var tile = new GridPos(x, y);
                bool lived = false;
                foreach (Household household in world.Households)
                {
                    if (household.Home() == tile) lived = true;
                }

                if (!lived) world.EraseResidential(tile);
            }
        }

        loop.Step(config.TicksPerYear * 40);
        int stalled = world.Households.Count;

        // The player paints. Around the village, so it is land anyone can work from.
        GridPos village = world.Households[0].Home();
        int painted = 0;
        for (int dy = -5; dy <= 5; dy++)
        {
            for (int dx = -5; dx <= 5; dx++)
            {
                if (world.PaintResidential(new GridPos(village.X + dx, village.Y + dy)).Allowed)
                {
                    painted++;
                }
            }
        }

        loop.Step(config.TicksPerYear * 40);

        _output.WriteLine(
            $"stalled at {stalled} households; {painted} tiles painted; " +
            $"{world.Households.Count} forty years later.");

        Assert.True(world.Households.Count > stalled,
            "The player painted more land and the village still did not build.");
    }

    [Fact]
    public void NobodyIsAskedToLiveOnWater()
    {
        SimWorld world = Build(Config, 1UL).World;

        GridPos wet = default;
        bool found = false;
        for (int y = world.Map.MinY; y < world.Map.MinY + world.Map.Height && !found; y++)
        {
            for (int x = world.Map.MinX; x < world.Map.MinX + world.Map.Width && !found; x++)
            {
                var here = new GridPos(x, y);
                if (world.Map.TerrainAt(here) == Terrain.Water)
                {
                    wet = here;
                    found = true;
                }
            }
        }

        Assert.True(found, "This seed has no water (D7).");

        PlacementVerdict verdict = world.PaintResidential(wet);
        _output.WriteLine(verdict.Reason);

        Assert.False(verdict.Allowed);
        Assert.False(world.Zones.IsResidential(wet));
    }

    [Fact]
    public void PaintingFarFromFoodWarnsOnceRatherThanPerHouse()
    {
        // Why zoning was the better answer than per-house placement (D42): the player
        // is told about a bad neighbourhood WHEN THEY PAINT IT, not on every house the
        // village later builds there.
        SimConfig config = Config;
        SimWorld world = Build(config).World;

        GridPos village = world.Households[0].Home();
        int budget = VillageEconomy.MaxHomeToWorkTiles(config);

        PlacementVerdict? warned = null;
        for (int distance = budget; distance < budget + 20 && warned is null; distance++)
        {
            PlacementVerdict verdict = world.PaintResidential(
                new GridPos(village.X + distance, village.Y));

            if (verdict.Allowed && verdict.HasWarning)
            {
                warned = verdict;
            }
        }

        Assert.True(warned is not null, "Nowhere was far enough from food to warn about (D7).");
        _output.WriteLine(warned!.Value.Warning);

        Assert.True(warned.Value.Allowed, "A distant neighbourhood must be allowed, not refused.");
        Assert.Contains("tiles from the nearest food", warned.Value.Warning, System.StringComparison.Ordinal);
    }

    [Fact]
    public void ZonesAreSimStateAndAreHashed()
    {
        // A zone is a decision somebody made, so two runs given the same decisions must
        // produce the same village — and a run given DIFFERENT decisions must not
        // silently agree (D7).
        SimLoop loop = Build(Config);
        loop.Step(Config.TicksPerYear);

        ulong before = StateHash.Compute(loop.World);

        GridPos somewhere = loop.World.Map.FoundingSite;
        loop.World.PaintResidential(new GridPos(somewhere.X + 9, somewhere.Y + 9));

        Assert.NotEqual(before, StateHash.Compute(loop.World));
    }

    [Fact]
    public void ErasingLandDoesNotPullDownTheHousesOnIt()
    {
        // Erasing says where the village may build NEXT. Demolishing homes because
        // somebody adjusted a brush would be a cruel reading of an undo.
        SimLoop loop = Build(Config);
        loop.Step(Config.TicksPerYear * 20);

        SimWorld world = loop.World;
        int before = world.Households.Count;
        GridPos lived = world.Households[0].Home();

        world.EraseResidential(lived);

        Assert.Equal(before, world.Households.Count);
        Assert.Equal(lived, world.Households[0].Home());
    }
}
