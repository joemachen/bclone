using System.Collections.Generic;
using System.Linq;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The farm remembers — <c>specs/per-site-yield.md §4.2a</c> (D194). The sowing cap stops
/// predicting what a farm can bring in and starts asking what it did.
/// </summary>
/// <remarks>
/// <para>
/// <b>⛔ THE THING THESE GUARDS EXIST TO STOP COMING BACK: a cap that is self-fulfilling.</b>
/// <c>ReapableShareAt</c> cut a distant farm's field to 40%, the farmer then had nothing left to
/// do, and the idleness read back as proof that the field had been too big. Measured, a farm ten
/// ticks from its store spent <b>27% of the autumn resting</b> — and 45% at sixteen ticks, 55% at
/// twenty-two. <i>A guard that says "distant farms reap fewer tiles" can be measuring the cap
/// rather than a physical limit</i> (D157's blind-guard rule, one system over).
/// </para>
/// <para>
/// <b>⛔⛔ AND THE THING NOBODY SHOULD TRY AGAIN: thirteen tiles ten ticks out.</b> Autumn is 120
/// ticks and thirteen tiles at that distance needs about 230. The farm is short by <b>one or
/// two</b> tiles, not eight. <b>The lever for thirteen is the walk</b> — see
/// <see cref="FarmTests"/>'s distance guard and §4.3's placement warning.
/// </para>
/// <para>
/// <b>⭐⭐ WHAT DISCOVERS A BETTER YEAR IS THE VILLAGE'S OWN SPRING, NOT A PROBE — AND THE RED
/// CHECK IS WHY THAT IS KNOWN.</b> Two drafts of this slice had the farm commit
/// <c>learned + 1</c> tiles a year and latch once a tile rotted. <b>Deleting both turned nothing
/// red</b>: the settled memory and the tiles reaped came out identical at ten, sixteen and
/// twenty-two ticks — <b>6/5/4 learned and 72/60/48 reaped either way</b> — because
/// <see cref="SimWorld.HarvestOneFarmCanBringIn"/> multiplies by the hands standing in the field
/// <em>at that moment</em>, so a farm with two hands in spring and one by autumn already commits
/// ground for two. <b>D86's live-allowance rule was always going to over-reach; the memory only
/// has to notice.</b> A deliberate probe on top would have been the invisible no-op this project
/// has rejected four times (D56, D177, D187), and the failure modes agree: with no probe the
/// worst a farm can do is sit on today's behaviour, and with one it is to rot a tile every year.
/// </para>
/// </remarks>
public sealed class FarmMemoryTests
{
    private readonly ITestOutputHelper _output;

    public FarmMemoryTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => ShippedConfig.Established();

    private static SimLoop Loop(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink());

    // ---------------------------------------------------------------
    //  The memory itself
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ A distant farm ends up committing more ground than the prediction ever gave it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The whole mechanism, in one assertion.</b> Without it the opening guess is the answer
    /// for ever and this slice is <c>ReapableShareAt</c> with extra steps.
    /// </para>
    /// <para>
    /// ⚠️ <b>A DISTANT FARM, BECAUSE A NEAR ONE OPENS AT THE DERIVED CAP AND HAS NOWHERE TO GO.</b>
    /// Posing this beside the granary gives a guard that passes whether or not the memory exists
    /// — D157's blind fixture, one system over.
    /// </para>
    /// <para>
    /// ⚠️ <b>AND AGAINST THE OPENING GUESS, NOT AGAINST THE RAW FIELD.</b>
    /// <c>FieldTilesLearned</c> is zero on a farm that has never sown, so *"it went up from
    /// zero"* is true the first time the memory is written at all and proves nothing.
    /// </para>
    /// </remarks>
    [Fact]
    public void ADistantFarmEndsUpCommittingMoreGroundThanThePredictionGaveIt()
    {
        SimLoop loop = Loop(Config);
        SimWorld world = loop.World;

        Workplace farm = FarmTestGround.SiteAFarm(world, walkAway: 10, out int walk);
        FarmFixtures.GiveItGround(world, farm, reach: 3);

        int opening = world.FieldTilesThisFarmCommitsPerHand(farm);

        // ⚠️ NOTHING IS POSED, AND THAT IS THE FIX TO THIS GUARD'S FIRST DRAFT. It sowed a
        // single tile a year and called that "a clean autumn" — the farm brought in one tile,
        // correctly recorded that one tile is what it had managed, and the guard then failed
        // for the feature working. **The memory is a high-water mark**, so a posed field
        // smaller than the farm's own commitment is a WORSE year, not an easier one.
        for (int i = 0; i < Config.TicksPerYear * 6; i++)
        {
            loop.StepOnce();
        }

        int now = world.FieldTilesThisFarmCommitsPerHand(farm);
        _output.WriteLine(
            $"{walk} ticks out: commits per hand {opening} → {now} (learned {farm.FieldTilesLearned})");
        Assert.True(
            now > opening,
            $"A farm {walk} ticks out still commits {now} tiles a hand — the same {opening} the "
            + "prediction gave it. The memory is doing nothing, and a cap that cannot learn is "
            + "that prediction with extra steps.");
    }

    /// <summary>
    /// ⭐⭐ A thin year never lowers what the farm has already proved.
    /// </summary>
    /// <remarks>
    /// <b>D183's *give, never take*, one system over.</b> What a farm brought in once it can
    /// bring in again — a thin year is about the hands that turned up, not about the ground — so
    /// one short-staffed autumn must never become a permanent verdict on the field.
    /// </remarks>
    [Fact]
    public void AThinYearNeverLowersWhatTheFarmHasAlreadyProved()
    {
        SimLoop loop = Loop(Config);
        SimWorld world = loop.World;
        Workplace farm = FarmTestGround.SiteAFarm(world, walkAway: 10, out int walk);
        int painted = FarmFixtures.GiveItGround(world, farm, reach: 3);
        Assert.True(painted > 13, "The farm needs more ground than it can ever reap.");

        // Two full fields, so the farm proves what it can really do.
        for (int year = 0; year < 2; year++)
        {
            FarmFixtures.StepToTheStartOf(loop, Season.Summer);
            SowEveryTile(world, farm);
            FarmFixtures.StepToTheStartOf(loop, Season.Winter);
        }

        int proved = farm.FieldTilesLearned;
        Assert.True(proved > 1, "The farm proved nothing, so there is nothing to protect.");

        // Then three deliberately miserable years — a single tile each, which is the shape of a
        // farm that lost its hands or sat under a met stock limit.
        for (int year = 0; year < 3; year++)
        {
            FarmFixtures.StepToTheStartOf(loop, Season.Summer);
            ClearTheField(world, farm);
            SowExactly(world, farm, 1);
            FarmFixtures.StepToTheStartOf(loop, Season.Winter);
        }

        _output.WriteLine(
            $"{walk} ticks out: proved {proved}, then three one-tile years → {farm.FieldTilesLearned}");
        Assert.Equal(proved, farm.FieldTilesLearned);
    }

    /// <summary>
    /// ⚠️ A year the farm never sowed teaches it nothing — the met-limit trap, named in §4.2a.
    /// </summary>
    /// <remarks>
    /// <b>An empty field at the turn of winter looks exactly like a cleared one</b>, and a farm
    /// held by a met stock limit would read years of idleness as years of success, climb to the
    /// cap, and over-commit the moment the player raised the limit.
    /// </remarks>
    [Fact]
    public void AYearWithNoCropTeachesTheFarmNothing()
    {
        SimLoop loop = Loop(Config);
        SimWorld world = loop.World;
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        FarmFixtures.GiveItGround(world, farm, reach: 3);

        // Nothing sown at all, three years running.
        int before = farm.FieldTilesLearned;
        for (int year = 0; year < 3; year++)
        {
            FarmFixtures.StepToTheStartOf(loop, Season.Summer);
            ClearTheField(world, farm);
            FarmFixtures.StepToTheStartOf(loop, Season.Winter);
        }

        _output.WriteLine($"learned {before} → {farm.FieldTilesLearned} over three fallow years");
        Assert.Equal(before, farm.FieldTilesLearned);
    }

    /// <summary>
    /// ⛔ The memory can never carry a farm past what the economy derives.
    /// </summary>
    /// <remarks>
    /// <b>`FieldTilesOneFarmerKeeps` is the survival floor the whole economy is solved against</b>
    /// (D16, D189). A well-sited farm's physical ceiling measures <b>21</b> tiles; the derivation
    /// says <b>13</b>; thirteen wins. A memory that could climb past it would inflate a derived,
    /// locked number from the far end — the exact move D189 refused for
    /// <c>crop_yield_per_tile</c>.
    /// </remarks>
    [Fact]
    public void TheMemoryNeverClimbsPastWhatTheEconomyDerives()
    {
        SimConfig config = Config;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        FarmFixtures.GiveItGround(world, farm, reach: 4);

        int derived = VillageEconomy.FieldTilesOneFarmerKeeps(config);

        // ⚠️ EVERY TILE, EVERY YEAR, WHICH IS THE ONLY POSE THAT PUSHES AGAINST THE CEILING.
        // The first draft sowed one tile a year and passed with `learned` sitting at 1 —
        // **green and blind** (D157). A guard about a ceiling has to be given a field big
        // enough to reach it.
        for (int year = 0; year < 25; year++)
        {
            FarmFixtures.StepToTheStartOf(loop, Season.Summer);
            SowEveryTile(world, farm);
            FarmFixtures.StepToTheStartOf(loop, Season.Winter);
        }

        _output.WriteLine($"after twenty-five full fields: learned {farm.FieldTilesLearned}, derived {derived}");
        Assert.True(
            farm.FieldTilesLearned > 1,
            "The farm learned nothing at all, so this guard is not testing a ceiling.");
        Assert.True(
            farm.FieldTilesLearned <= derived,
            $"The farm's memory reached {farm.FieldTilesLearned} against a derived {derived}. "
            + "The memory may only ever commit LESS than the derivation, never more.");

        Assert.True(
            world.HarvestOneFarmCanBringIn(farm) <= derived * farm.Places,
            "The committed ground went past the derivation for the seats the farm has.");
    }

    /// <summary>
    /// ⭐ A store built by the fields lets the farm try again.
    /// </summary>
    /// <remarks>
    /// <b>The latch has to be releasable or it is a trap.</b> A farm that learned its limit at ten
    /// ticks out is answering a question about a walk, and when the player changes the walk the
    /// old answer stops being true. This is the difference between a memory and a scar.
    /// </remarks>
    [Fact]
    public void AStoreBuiltByTheFieldsLetsTheFarmTryAgain()
    {
        SimLoop loop = Loop(Config);
        SimWorld world = loop.World;

        Workplace farm = FarmTestGround.SiteAFarm(world, walkAway: 12, out int walk);
        int painted = FarmFixtures.GiveItGround(world, farm, reach: 3);
        Assert.True(painted > 4, "The farm needs ground to over-reach on.");
        Assert.True(walk > 6, $"The farm landed {walk} ticks out — too near to measure anything.");

        // Twice, for the reason `AFarmThatCannotBringItInSettlesBackAndStopsClimbing` states:
        // a first rot that also sets a record is a good year.
        for (int year = 0; year < 2; year++)
        {
            FarmFixtures.StepToTheStartOf(loop, Season.Summer);
            SowEveryTile(world, farm);
            FarmFixtures.StepToTheStartOf(loop, Season.Winter);
        }


        int shortWalk = farm.FieldWalkWhenLearned;

        // ⚠️ PER HAND, NOT `HarvestOneFarmCanBringIn`. That multiplies by the hands standing in
        // the field, and a posed farm the allocator has not staffed yet has none — so both
        // sides of the comparison came out zero and the guard passed nothing. The claim is
        // about the farm's own reckoning, which is the per-hand number.
        int committedFar = world.FieldTilesThisFarmCommitsPerHand(farm);

        // A granary right beside the fields — the lever that actually buys the tiles.
        StoreBuilding near = FarmTestGround.RaiseAGranaryBeside(world, farm);
        Assert.True(
            world.TravelCost.TicksBetween(farm.Position, near.Position) < walk,
            "The new granary is no nearer than the old one, so this measures nothing.");

        int committedNear = world.FieldTilesThisFarmCommitsPerHand(farm);
        _output.WriteLine(
            $"learned at a walk of {shortWalk}: committed {committedFar}; "
            + $"with a granary beside the fields: {committedNear}");

        Assert.True(
            committedNear > committedFar,
            $"A granary beside the fields moved the committed ground from {committedFar} to "
            + $"{committedNear}. The walk is the lever, and it did nothing.");
    }


    // ---------------------------------------------------------------
    //  ⭐⭐ The anti-vacuity guards — what the slice is FOR
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ A distant farm stops standing idle in a field it was told was too big.
    /// </summary>
    /// <remarks>
    /// <b>This is the finding, and it is the guard the whole slice exists to hold.</b> Measured
    /// before the change, a farm ten ticks from its store spent <b>27% of every autumn resting</b>
    /// while reaping five tiles — the cap had cut its field and then the idleness proved the cap
    /// right. <i>The cap was self-fulfilling.</i>
    /// </remarks>
    [Fact]
    public void ADistantFarmNoLongerIdlesThroughTheAutumnItWasTooBusyFor()
    {
        SimConfig config = Config;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        Workplace farm = FarmTestGround.SiteAFarm(world, walkAway: 10, out int walk);
        FarmFixtures.GiveItGround(world, farm, reach: 3);

        int idle = 0;
        int handTicks = 0;
        int reaped = 0;

        const int Years = 12;
        for (int i = 0; i < config.TicksPerYear * Years; i++)
        {
            loop.StepOnce();
            bool fall = world.Clock.Season == Season.Fall;

            foreach (Villager villager in world.Villagers)
            {
                if (!villager.Alive || villager.WorkplaceId != farm.Id)
                {
                    continue;
                }

                if (fall)
                {
                    handTicks++;
                    if (villager.State is VillagerState.Resting or VillagerState.Idle)
                    {
                        idle++;
                    }
                }

                if (villager.State == VillagerState.Reaping && villager.ActionTicksRemaining == 1)
                {
                    reaped++;
                }
            }
        }

        int idleShare = handTicks == 0 ? 0 : idle * 100 / handTicks;
        _output.WriteLine(
            $"{walk} ticks out over {Years} years: {reaped} tiles reaped, "
            + $"{idleShare}% of the autumn idle, memory settled at {farm.FieldTilesLearned}");

        Assert.True(handTicks > 0, "Nobody ever held the farm, so this measures nothing.");
        Assert.True(
            idleShare < 15,
            $"A farm {walk} ticks out spent {idleShare}% of its autumns idle. It was measured at "
            + "27% before this slice, and that idleness is the cap cutting a field the farmer then "
            + "had time to spare on. A self-fulfilling cap is what this slice deleted.");
    }

    /// <summary>
    /// ⭐⭐ …and it brings home more food for it, which is the point of not idling.
    /// </summary>
    /// <remarks>
    /// <b>The companion to the guard above, and neither is enough alone.</b> Idleness could be
    /// removed by giving the farmhand busywork; tiles could be raised by letting the crop rot in
    /// the field. <b>Only both together say the farm got better.</b>
    /// </remarks>
    [Fact]
    public void AndItBringsInMoreThanThePredictionEverLetIt()
    {
        int reaped = FarmTestGround.TilesReapedOverTenYears(Config, walkAway: 10, out int walk, out int broughtIn);

        _output.WriteLine($"{walk} ticks out: {reaped} tiles reaped, {broughtIn}% brought in");

        // The prediction produced 51 tiles over ten years at this distance, measured.
        Assert.True(
            reaped > 51,
            $"A farm {walk} ticks out reaped {reaped} tiles in ten years. The prediction it "
            + "replaced managed 51, and the ledger says the ground is there for more.");

        // ⛔ AND THE ROT LINE STAYS HONEST (D167). Bringing in more by sowing far more and
        // losing the difference to winter is the bug this slice's ancestor fixed.
        Assert.True(
            broughtIn >= 75,
            $"The farm brought in only {broughtIn}% of what it sowed. Rot every year by "
            + "construction is weather, and the player cannot act on weather (D167).");
    }

    // ---------------------------------------------------------------
    //  §4.3 — the player is told, while they can still move it
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ Marking a farmhouse far from any food store says so, and says what it will cost.
    /// </summary>
    /// <remarks>
    /// <b>The single largest legible consequence in the farm, and nothing said it.</b> There is a
    /// distance warning at placement already, and it measures the walk to the <em>village</em> —
    /// which is not the walk that halves a harvest. <b>Warned, never refused</b> (D43, D86).
    /// </remarks>
    [Fact]
    public void MarkingAFarmFarFromAStoreWarnsAboutTheWalkTheHarvestMakes()
    {
        SimWorld world = Loop(Config).World;

        GridPos far = FarmTestGround.GroundAtAboutThisWalk(world, walkAway: 14, out int walk);
        PlacementVerdict verdict = world.CanBuildAt(BuildingKind.Farmhouse, far);

        _output.WriteLine($"{walk} ticks from the nearest granary: \"{verdict.Warning}\"");

        Assert.True(verdict.Allowed, "A distant farm is a decision, not an impossibility (D43).");
        Assert.True(
            verdict.HasWarning,
            $"A farmhouse {walk} ticks from the nearest food store drew no warning at all. Its "
            + "harvest will be roughly half a well-sited farm's and the game never mentions it.");
        Assert.Contains("store", verdict.Warning, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The anti-vacuity companion (D7): a farm beside the granary is not nagged.
    /// </summary>
    /// <remarks>
    /// Without this, a warning that fired on every farmhouse in the valley would pass the guard
    /// above and teach the player to click past it — D42's rule about one considered sentence
    /// rather than a nag.
    /// </remarks>
    [Fact]
    public void ButAFarmBesideTheStoresIsNotWarnedAboutAnything()
    {
        SimWorld world = Loop(Config).World;

        GridPos near = FarmTestGround.GroundAtAboutThisWalk(world, walkAway: 1, out int walk);
        PlacementVerdict verdict = world.CanBuildAt(BuildingKind.Farmhouse, near);

        _output.WriteLine($"{walk} ticks from the nearest granary: \"{verdict.Warning}\"");
        Assert.DoesNotContain("store", verdict.Warning, System.StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------
    //  Posing helpers
    // ---------------------------------------------------------------

    /// <summary>
    /// Put every tile of a farm's ground under seed — <b>the over-reaching year, posed</b>.
    /// </summary>
    /// <remarks>
    /// <b>⚠️ THERE IS DELIBERATELY NO "SOW EXACTLY N" HELPER ANY MORE, AND ITS ABSENCE IS A
    /// FINDING.</b> The first draft of this file had one and used it to pose *a clean autumn* as
    /// a single tile — so the farm brought in one tile, correctly recorded that one tile is what
    /// it had managed, and the guard failed for the feature working. **The memory is a
    /// high-water mark, so a posed field smaller than the farm's own commitment is a WORSE year,
    /// not an easier one.** The only two poses this file needs are *no crop at all* and *more
    /// ground than anybody could take in*.
    /// </remarks>
    private static void SowExactly(SimWorld world, Workplace farm, int tiles)
    {
        List<GridPos> owned = world.Zones.WorkGroundOf(farm.Id)
            .Select(world.Zones.PositionOf)
            .OrderBy(at => world.TravelCost.Cost(farm.Position, at))
            .ThenBy(at => at.Y)
            .ThenBy(at => at.X)
            .ToList();

        int standing = 0;
        foreach (GridPos at in owned)
        {
            if (world.Map.TerrainAt(at) is Terrain.Sown or Terrain.Ripe)
            {
                standing++;
            }
            else if (standing < tiles && SimWorld.IsSowable(world.Map.TerrainAt(at)))
            {
                world.SetTerrain(at, Terrain.Sown);
                world.Map.SetCrop(at, 1);
                standing++;
            }
        }
    }

    private static void SowEveryTile(SimWorld world, Workplace farm)
    {
        IReadOnlyList<int> owned = world.Zones.WorkGroundOf(farm.Id);
        for (int i = 0; i < owned.Count; i++)
        {
            GridPos at = world.Zones.PositionOf(owned[i]);
            if (SimWorld.IsSowable(world.Map.TerrainAt(at)))
            {
                world.SetTerrain(at, Terrain.Sown);
                world.Map.SetCrop(at, 1);
            }
        }
    }

    /// <summary>Take every crop off a farm's ground, so a posed year starts from nothing.</summary>
    private static void ClearTheField(SimWorld world, Workplace farm)
    {
        IReadOnlyList<int> owned = world.Zones.WorkGroundOf(farm.Id);
        for (int i = 0; i < owned.Count; i++)
        {
            GridPos at = world.Zones.PositionOf(owned[i]);
            if (world.Map.TerrainAt(at) is Terrain.Sown or Terrain.Ripe)
            {
                world.SetTerrain(at, Terrain.Field);
            }
        }
    }
}
