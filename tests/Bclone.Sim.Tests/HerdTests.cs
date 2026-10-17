using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ Commit 2 of `specs/trade.md §11` (D592): <b>the herd lives</b> — `specs/livestock.md` as re-read in its §0. Free
/// grazing spring to autumn, hay eaten from the barn every winter day, a beast lost for each day the hay is short,
/// growth to what the pasture holds and the cull above it into meat and leather. No herdsman yet (commit 3): these
/// guards feed the herd by hand, which is what makes each rule visible on its own. ⭐ Since D593 the herd eats only
/// hay <i>put out</i> on its pasture (<see cref="Workplace.HayPutOut"/>), and a beast born beyond the pasture waits for a
/// herdsman's cull — with nobody keeping the herd, it starves on the overgrazed ground instead.
/// </summary>
public sealed class HerdTests
{
    private readonly ITestOutputHelper _output;

    public HerdTests(ITestOutputHelper output) => _output = output;

    // ---------------------------------------------------------------
    //  The pose: an established village, its hall, a barn, a pasture, a herd
    // ---------------------------------------------------------------

    /// <summary>
    /// An established village where <b>nobody keeps the herd</b> (the herdsman's target at nought, D106): these guards
    /// are the herd's own rules (D592); the herdsman's are <c>HerdsmanTests</c> (D593).
    /// </summary>
    private static SimLoop AVillage()
    {
        SimLoop loop = SimFactory.CreatePhase0(ShippedConfig.Established(), new InMemoryLogSink());
        Assert.True(loop.World.SetJobLimit(JobKind.Herdsman, 0).Allowed);
        return loop;
    }

    private static AnimalRow Cattle(SimWorld world) => world.Config.Animals[0];

    /// <summary>Raise a barn outright — the hall first, since a barn waits for it (D590).</summary>
    internal static Workplace RaiseABarn(SimWorld world)
    {
        world.TownHall ??= new TownHall { Position = Point.CentreOf(world.Map.FoundingSite), Name = "the hall", RaisedAtTick = 1 };
        GridPos where = BuildableNear(world, BuildingKind.Barn);
        Assert.True(world.Mark(BuildingKind.Barn, where).Allowed, $"Could not mark a barn at {where}.");
        Workplace site = Assert.Single(world.Workplaces, w => w.Construction?.Kind == BuildingKind.Barn);
        BuildFixtures.StockTheSite(site);
        for (int i = 0; i <= site.Construction!.Recipe.WorkTicks; i++)
        {
            site.Construction.Work();
        }

        GridPos stood = site.Tile;
        world.Complete(site);
        return Assert.Single(world.Workplaces, w => w.Kind == JobKind.Herdsman && w.Tile == stood && !w.IsSite);
    }

    /// <summary>Paint exactly <paramref name="tiles"/> grass tiles round the barn as its pasture.</summary>
    internal static void GivePasture(SimWorld world, Workplace barn, int tiles)
    {
        int given = 0;
        for (int reach = 1; reach < 20 && given < tiles; reach++)
        {
            for (int dy = -reach; dy <= reach && given < tiles; dy++)
            {
                for (int dx = -reach; dx <= reach && given < tiles; dx++)
                {
                    var at = new GridPos(barn.Tile.X + dx, barn.Tile.Y + dy);
                    if (world.Map.Contains(at) && world.Map.TerrainAt(at) == Terrain.Grass
                        && world.Zones.WorkGroundOwner(at) == 0 && world.PaintWorkGround(barn, at).Allowed)
                    {
                        given++;
                    }
                }
            }
        }

        Assert.Equal(tiles, world.Zones.WorkGroundTiles(barn.Id));
    }

    private static GridPos BuildableNear(SimWorld world, BuildingKind kind)
    {
        GridPos site = world.Map.FoundingSite;
        for (int radius = 4; radius < 30; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);
                    if (!world.HasSomethingToHarvest(at) && world.CanBuildAt(kind, at).Allowed)
                    {
                        return at;
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException($"Nowhere to put a {kind} near the founding site.");
    }

    private static StoreBuilding BarnOf(SimWorld world, Workplace barn) =>
        world.BarnStoreOf(barn) ?? throw new Xunit.Sdk.XunitException("The barn raised no store.");

    /// <summary>Step to the first tick of the next <paramref name="season"/>.</summary>
    private static void StepToTheStartOf(SimLoop loop, Season season)
    {
        do
        {
            loop.StepOnce();
        }
        while (!(loop.World.Clock.Season == season && loop.World.Clock.DayOfSeason == 1
            && loop.World.Tick % (ulong)loop.World.Config.TicksPerDay == 0UL));
    }

    // ---------------------------------------------------------------
    //  The pasture
    // ---------------------------------------------------------------

    [Fact]
    public void ThePastureHoldsItsTilesOverTheTilesAHead()
    {
        SimLoop loop = AVillage();
        Workplace barn = RaiseABarn(loop.World);
        GivePasture(loop.World, barn, 23);
        Assert.Equal(23 / Cattle(loop.World).PastureTilesPerHead, loop.World.PastureFor(barn));
        Assert.Equal(4, loop.World.PastureFor(barn)); // 23 tiles at Joe's 5 a head (TECH-EXAMPLE §3)
    }

    /// <summary>A pasture is never a seam, and it keeps its trees (Joe, D490: "animals can live in the woods").</summary>
    [Fact]
    public void APastureIsNeverASeamAndKeepsItsTrees()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        Workplace barn = RaiseABarn(world);

        GridPos? rock = null, wood = null;
        for (int y = world.Map.MinY; y < world.Map.MinY + world.Map.Height && (rock is null || wood is null); y++)
        {
            for (int x = world.Map.MinX; x < world.Map.MinX + world.Map.Width; x++)
            {
                var at = new GridPos(x, y);
                if (world.Zones.WorkGroundOwner(at) != 0 || world.Zones.IsResidential(at))
                {
                    continue;
                }

                if (rock is null && world.Map.TerrainAt(at) == Terrain.Rock) { rock = at; }
                if (wood is null && world.Map.TerrainAt(at) == Terrain.Forest && !world.Zones.IsHarvest(at)) { wood = at; }
            }
        }

        Assert.NotNull(rock);
        Assert.NotNull(wood);
        Assert.False(world.PaintWorkGround(barn, rock!.Value).Allowed, "a pasture was painted on a seam");
        Assert.True(world.PaintWorkGround(barn, wood!.Value).Allowed);
        Assert.Equal(Terrain.Forest, world.Map.TerrainAt(wood.Value));
        Assert.False(world.Zones.IsHarvest(wood.Value), "a pasture's tree was marked to be felled");
    }

    // ---------------------------------------------------------------
    //  Grazing and winter
    // ---------------------------------------------------------------

    /// <summary>Spring to autumn the herd feeds itself: not a straw of hay leaves the barn.</summary>
    [Fact]
    public void GrazingIsFree()
    {
        SimLoop loop = AVillage();
        Workplace barn = RaiseABarn(loop.World);
        GivePasture(loop.World, barn, 40);
        barn.HerdHead = 6;
        StepToTheStartOf(loop, Season.Spring);
        BarnOf(loop.World, barn).Store.Receive(Goods.Hay, 500);
        barn.HayPutOut = 300;

        StepToTheStartOf(loop, Season.Winter);
        Assert.Equal(500, BarnOf(loop.World, barn).Store[Goods.Hay]);
        Assert.Equal(300, barn.HayPutOut);
    }

    /// <summary>
    /// ⭐ A herd fed its winter never loses a beast (`livestock.md §9.3`) — the counterweight to "the herd starves" (Joe,
    /// D60): a pressure the player cannot answer is a leak. Fifty winters, the barn topped up each autumn by hand.
    /// </summary>
    [Fact]
    public void AHerdFedItsWinterNeverLosesABeast()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        Workplace barn = RaiseABarn(world);
        GivePasture(world, barn, 40);
        barn.HerdHead = 5;
        AnimalRow cattle = Cattle(world);
        int winterDays = world.Config.DaysPerSeason;

        for (int year = 0; year < 50; year++)
        {
            StepToTheStartOf(loop, Season.Winter);
            int head = barn.HerdHead;
            int growth = barn.HerdGrowth;
            Assert.InRange(head, 1, world.PastureFor(barn) + 1);
            barn.HayPutOut = head * cattle.HayPerHeadPerWinterDay * winterDays;

            StepToTheStartOf(loop, Season.Spring);
            Assert.True(barn.HerdHead == head, $"year {year}: {head} head went into winter fed and {barn.HerdHead} came out");

            // ⭐ Nothing is born in winter: the grass is gone and so is the growth (`livestock.md §3`).
            Assert.True(barn.HerdGrowth == growth, $"year {year}: the herd grew over winter ({growth} → {barn.HerdGrowth})");
            Assert.Equal(0, barn.HayPutOut);
        }
    }

    /// <summary>
    /// ⭐ The herd starves (Joe, D60) — one beast for each winter day the barn comes up short (`livestock.md §0`), and
    /// the anti-vacuity half of the guard above (D7): a starvation rule that never fires is untested.
    /// </summary>
    [Fact]
    public void AHerdShortOfHayLosesABeastEachDayItGoesShort()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        Workplace barn = RaiseABarn(world);
        GivePasture(world, barn, 40);
        barn.HerdHead = 8;
        AnimalRow cattle = Cattle(world);
        StepToTheStartOf(loop, Season.Winter);

        // Ten days' hay for the whole herd, out on the pasture.
        barn.HayPutOut = 8 * cattle.HayPerHeadPerWinterDay * 10;

        loop.Step(world.Config.TicksPerDay * 10);
        Assert.Equal(8, barn.HerdHead);
        Assert.Equal(0, barn.HayPutOut);

        loop.Step(world.Config.TicksPerDay * 3);
        Assert.Equal(5, barn.HerdHead);

        StepToTheStartOf(loop, Season.Spring);
        Assert.Equal(0, barn.HerdHead);
    }

    // ---------------------------------------------------------------
    //  Growth and the cull
    // ---------------------------------------------------------------

    /// <summary>
    /// Growth: the herd's head count added each growing day, a beast for every <c>head_days_per_birth</c>, while the
    /// pasture has room and one beast beyond it — and with nobody keeping the herd that one starves on the overgrazed
    /// ground the next day, with no yield (D593: the cull is the herdsman's work).
    /// </summary>
    [Fact]
    public void TheHerdGrowsToItsPastureAndNobodyKeepingItLosesTheExtra()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        Workplace barn = RaiseABarn(world);
        GivePasture(world, barn, 20); // four head
        AnimalRow cattle = Cattle(world);
        StepToTheStartOf(loop, Season.Spring);
        barn.HerdHead = 2;
        barn.HerdGrowth = 0;
        int meatBefore = world.FoodEverProducedOf(Goods.Meat);

        // Two head: a birth every 120 days of grazing — none in this spring's thirty.
        StepToTheStartOf(loop, Season.Summer);
        Assert.Equal(2, barn.HerdHead);
        Assert.Equal(2 * world.Config.DaysPerSeason, barn.HerdGrowth);

        // At its pasture, a growing year more: the births go one beyond it, and the overgrazed ground takes that one.
        barn.HerdHead = 4;

        // Half a birth's growth already in: the calf comes mid-summer, so a grazing day follows it (a birth on the year's
        // last grazing day would go into winter one beyond the pasture, where the overgrazing rule does not run).
        barn.HerdGrowth = cattle.HeadDaysPerBirth / 2;
        bool beyond = false;
        while (world.Clock.Season != Season.Winter)
        {
            loop.StepOnce();
            beyond |= barn.HerdHead == 5;
            Assert.InRange(barn.HerdHead, 4, 5);
        }

        Assert.True(beyond, "no calf was born beyond the pasture — the guard would be vacuous");
        Assert.Equal(4, barn.HerdHead);
        Assert.True(world.FoodEverProducedOf(Goods.Meat) == meatBefore, "meat came of a beast nobody culled");
        _output.WriteLine($"herd {barn.HerdHead}, growth {barn.HerdGrowth} of {cattle.HeadDaysPerBirth}");
    }

    /// <summary>A herd larger than its pasture, with nobody keeping it, starves down a beast a grazing day — no yield.</summary>
    [Fact]
    public void AHerdLargerThanItsPastureStarvesDownWithNobodyKeepingIt()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        Workplace barn = RaiseABarn(world);
        GivePasture(world, barn, 10); // two head
        StepToTheStartOf(loop, Season.Spring);
        barn.HerdHead = 6;
        int meatBefore = world.FoodEverProducedOf(Goods.Meat);

        loop.Step(world.Config.TicksPerDay * 4);
        Assert.Equal(2, barn.HerdHead);
        Assert.Equal(meatBefore, world.FoodEverProducedOf(Goods.Meat));
    }

    /// <summary>Nothing happens to a herd that is not on a barn — the herd lives on the barn (`livestock.md §0`).</summary>
    [Fact]
    public void AHerdOnlyLivesOnABarn()
    {
        SimLoop loop = AVillage();
        SimWorld world = loop.World;
        Workplace notABarn = world.Workplaces.First(w => w.Kind != JobKind.Herdsman && !w.IsSite);
        notABarn.HerdHead = 5;
        StepToTheStartOf(loop, Season.Winter);
        StepToTheStartOf(loop, Season.Spring);
        Assert.Equal(5, notABarn.HerdHead);
        Assert.Equal(0, notABarn.HerdGrowth);
    }
}
