using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The forester's hut, and the first thing in this game that puts something back —
/// <c>specs/forests-and-gathering.md</c>, slice 4.
/// </summary>
/// <remarks>
/// <para>
/// <b>Joe:</b> <em>"Foresters can plant trees/forests in a painted area — this will allow the
/// user to sculpt their forests to their own desires."</em>
/// </para>
/// <para>
/// <b>⭐ Every other job consumes or converts.</b> A forester with planting on is the only
/// worker who leaves the valley richer than they found it — which is what makes over-clearing
/// recoverable rather than terminal (§0.1: *you lose villagers, not runs*), and therefore what
/// has to exist before the thickets can retire.
/// </para>
/// <para>
/// <b>Lands beside the tree stands, retiring nothing</b> — so no golden moves until somebody
/// places one. The same licence the gatherer's hut took beside the thickets.
/// </para>
/// </remarks>
public sealed class ForesterHutTests
{
    private readonly ITestOutputHelper _output;

    public ForesterHutTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimLoop Loop(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink());

    /// <summary>Raise a forester's hut outright, without waiting years for a builder.</summary>
    private static Workplace RaiseAHut(SimWorld world, GridPos at)
    {
        Assert.True(world.Mark(BuildingKind.ForesterHut, at).Allowed, $"Could not mark at {at}.");

        Workplace site = Assert.Single(
            world.Workplaces, place => place.Construction?.Kind == BuildingKind.ForesterHut);

        site.Construction!.Deliver(site.Construction.Recipe.Logs);
        for (int i = 0; i <= site.Construction.Recipe.WorkTicks; i++)
        {
            site.Construction.Work();
        }

        GridPos where = site.Position;
        world.Complete(site);

        return Assert.Single(
            world.Workplaces,
            place => place.Kind == JobKind.Forester && place.Position == where && !place.IsSite);
    }

    /// <summary>A buildable, bare tile near the village.</summary>
    private static GridPos ClearGroundNear(SimWorld world)
    {
        GridPos site = world.Map.FoundingSite;
        for (int radius = 1; radius < 12; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);
                    if (!world.HasSomethingToHarvest(at)
                        && world.CanBuildAt(BuildingKind.ForesterHut, at).Allowed)
                    {
                        return at;
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException("Nowhere buildable near the founding site.");
    }

    /// <summary>Give the hut every tile of one kind within reach. Returns how many.</summary>
    private static int GiveGround(SimWorld world, Workplace hut, int reach, bool wooded)
    {
        int given = 0;
        for (int dy = -reach; dy <= reach; dy++)
        {
            for (int dx = -reach; dx <= reach; dx++)
            {
                var at = new GridPos(hut.Position.X + dx, hut.Position.Y + dy);
                if (!world.Map.Contains(at))
                {
                    continue;
                }

                bool isWood = world.Map.TerrainAt(at) == Terrain.Forest;
                if (isWood != wooded || (!wooded && world.Map.TerrainAt(at) != Terrain.Grass))
                {
                    continue;
                }

                if (world.PaintWorkGround(hut, at).Allowed)
                {
                    given++;
                }
            }
        }

        return given;
    }

    // ---------------------------------------------------------------
    //  The building
    // ---------------------------------------------------------------

    /// <summary>It is a forester's workplace — the job kind is reused for the third time.</summary>
    [Fact]
    public void ItIsAForestersWorkplace()
    {
        SimWorld world = Loop(Config).World;
        Workplace hut = RaiseAHut(world, ClearGroundNear(world));

        Assert.Equal(JobKind.Forester, hut.Kind);
        Assert.Equal(VillageEconomy.RequiredTreeStandSeats(Config), hut.Capacity);
        // D137: a new hut TENDS its wood — fells while trees stand, replants when none do.
        // Joe: "the hut toggle for planting is off and I want it to be on by default."
        Assert.Equal(WorkMode.Plant, hut.Mode);
        Assert.Equal(Workplace.DefaultMode, hut.Mode);
    }

    /// <summary>Putting a tree back costs more than taking one down — and it is derived.</summary>
    /// <remarks>
    /// <b>The number that decides whether over-clearing is a mistake or a shrug.</b> Stated as
    /// a multiple of felling rather than as its own tick count, so the two cannot drift apart.
    /// </remarks>
    [Fact]
    public void PlantingCostsMoreThanFelling()
    {
        SimConfig config = Config;

        Assert.True(VillageEconomy.PlantTicks(config) > config.CutTicks,
            "Planting that costs no more than felling makes a cleared valley meaningless.");

        // And the consequence is derived and sane: re-wooding the ground one pair of hands
        // keeps should be a job of years, not of days or of never.
        int years = VillageEconomy.YearsToRewoodOnesGround(config);
        _output.WriteLine($"plant {VillageEconomy.PlantTicks(config)} ticks against "
            + $"{config.CutTicks} to fell; one forester re-woods their ground in ~{years} years");

        Assert.True(years >= 1, "Re-wooding a whole ground inside a year is not a mistake at all.");
        Assert.True(years <= 40, $"Re-wooding takes {years} years, which is never rather than slow.");
    }

    // ---------------------------------------------------------------
    //  ⭐ Felling its own ground, and putting it back
    // ---------------------------------------------------------------

    /// <summary>⭐ A forester fells the ground the player gave it, and the wood recedes.</summary>
    /// <remarks>
    /// A tree stand yields forever from a spot you stand on — the placeholder this replaces.
    /// A hut's timber has a location and a quantity, so felling it takes it off the map where
    /// the player can see the gap.
    /// </remarks>
    [Fact]
    public void ItFellsItsOwnGroundAndTheWoodRecedes()
    {
        SimConfig config = Config;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        Workplace hut = RaiseAHut(world, ClearGroundNear(world));
        int given = GiveGround(world, hut, 5, wooded: true);
        Assert.True(given > 0, "The hut was given no woodland, so nothing was tested.");

        int woodedBefore = CountOwnedForest(world, hut);
        world.SetStaffing(hut, hut.Capacity);

        // ⚠️ THE LOW-WATER MARK, NOT THE COUNT AT THE END, AND D126 IS WHY. This read the
        // owned woodland after three years and asked whether it had shrunk — which was the
        // right question in a valley where nothing grew back. It is the wrong one now: the
        // ground is re-wooded in about a year, so three years of hard felling can finish on
        // the same count it started with. **The test began failing because the valley got
        // better, not because the forester stopped working.**
        //
        // What the claim actually is — *felling takes the wood off the map where the player
        // can see the gap* — is a statement about the gap existing, not about it being
        // permanent. So sample it: the wood must be visibly thinner at some point than the
        // day the hut was staffed.
        // Measured when this was written: 13 tiles given, 13 wooded at the start and 13 at
        // the end — and **48 logs in the shed**. The felling was never in doubt.
        int thinnest = woodedBefore;
        for (int step = 0; step < config.TicksPerYear * 3; step += 10)
        {
            loop.Step(10);
            int now = CountOwnedForest(world, hut);
            if (now < thinnest)
            {
                thinnest = now;
            }
        }

        _output.WriteLine($"{given} tiles given; wooded {woodedBefore} → thinnest {thinnest} → "
            + $"{CountOwnedForest(world, hut)} after three years, "
            + $"{world.LogsInSheds()} logs in store");

        Assert.True(thinnest < woodedBefore,
            "Three years of foresters and not one tree came down on their own ground.");
    }

    /// <summary>⭐ And with planting on, the ground comes back.</summary>
    /// <remarks>
    /// <b>The only worker in this game who leaves the valley richer than they found it.</b>
    /// This is the guard that has to pass before the thickets can retire, because after that a
    /// felled ring is a village with no food.
    /// </remarks>
    [Fact]
    public void WithPlantingOnTheGroundComesBack()
    {
        SimConfig config = Config;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        Workplace hut = RaiseAHut(world, ClearGroundNear(world));
        int given = GiveGround(world, hut, 5, wooded: false);
        Assert.True(given > 0, "The hut was given no bare ground, so nothing was tested.");

        int woodedBefore = CountOwnedForest(world, hut);
        hut.Mode = WorkMode.Plant;
        world.SetStaffing(hut, hut.Capacity);

        loop.Step(config.TicksPerYear * 5);

        int woodedAfter = CountOwnedForest(world, hut);
        _output.WriteLine($"{given} bare tiles given; wooded {woodedBefore} → {woodedAfter} "
            + "after five years of planting");

        Assert.True(woodedAfter > woodedBefore,
            "Five years of planting and the ground is as bare as it started.");
    }

    /// <summary>Planting carries nothing home — it is a trade, not a free good.</summary>
    /// <remarks>
    /// A forester who plants all year feeds nobody and stocks no timber. **That is the trade
    /// the player makes when they switch the mode**, and it is why planting is a mode rather
    /// than a second job that could run alongside.
    /// </remarks>
    [Fact]
    public void PlantingBringsNothingBack()
    {
        SimConfig config = Config;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        Workplace hut = RaiseAHut(world, ClearGroundNear(world));
        GiveGround(world, hut, 5, wooded: false);
        hut.Mode = WorkMode.Plant;
        world.SetStaffing(hut, hut.Capacity);

        // ⚠️ HALF A YEAR, NOT A YEAR, AND D137 IS WHY. The claim is that *the planting errand*
        // yields nothing, and it still holds — but a tending forester now fells whatever is
        // standing on their ground, and D126 grows a sapling into a tree inside a year. Run for
        // a full year and the hut correctly plants its bare ground, waits for it, and starts
        // felling it, so "planting brings nothing back" stops being a statement about planting
        // and becomes a statement about how fast the valley grows.
        int logsBefore = world.TotalLogs();
        loop.Step(config.TicksPerYear / 2);

        _output.WriteLine($"half a year of planting: logs {logsBefore} → {world.TotalLogs()}");

        // Other people are still felling elsewhere, so this asserts the planters themselves
        // never turn up carrying anything.
        foreach (Villager villager in world.Villagers)
        {
            if (villager.Alive && villager.WorkplaceId == hut.Id)
            {
                Assert.Equal(0, villager.CarriedLogs);
            }
        }
    }

    // ---------------------------------------------------------------
    //  It changes nothing until somebody uses it
    // ---------------------------------------------------------------

    /// <summary>A village where nobody has touched a mode hashes as it always did.</summary>
    /// <remarks>
    /// The same licence the queue rank and the stock limits take, and the reason step B moves
    /// no golden: a control that is a provable no-op until used can ship without re-taking a
    /// single fifty-year hash.
    /// </remarks>
    [Fact]
    public void ModesAreSilentUntilSomebodySwitchesOne()
    {
        SimWorld untouched = Loop(Config).World;
        SimWorld switched = Loop(Config).World;

        Assert.Equal(StateHash.Compute(untouched), StateHash.Compute(switched));

        // ⚠️ SWITCHED TO `Harvest`, WHICH IS NOW THE NON-DEFAULT (D137). This set `Plant`,
        // which was the switch until Joe made tending the default — after which "switching
        // one" meant setting it to what it already was, and the hash correctly did not move.
        // The guard is about the sentinel tracking the default, so it has to switch AWAY from
        // whatever the default currently is.
        Workplace stand = switched.Workplaces.First(place => place.Kind == JobKind.Forester);
        Assert.Equal(Workplace.DefaultMode, stand.Mode);
        stand.Mode = WorkMode.Harvest;

        Assert.NotEqual(StateHash.Compute(untouched), StateHash.Compute(switched));
    }

    private static int CountOwnedForest(SimWorld world, Workplace hut)
    {
        int wooded = 0;
        foreach (int index in world.Zones.WorkGroundOf(hut.Id))
        {
            if (world.Map.TerrainAt(world.Zones.PositionOf(index)) == Terrain.Forest)
            {
                wooded++;
            }
        }

        return wooded;
    }
}
