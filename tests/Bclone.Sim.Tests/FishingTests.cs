using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ The fishing hut — <b>food that does not run out, and the first reason to go to the river</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Joe, 2026-09-02:</b> *"Fishing provides a consistent source of food that does not run out —
/// up to 4 seats. A step up from foraging in terms of food per worker. **Foraging is bottom of the
/// totem pole.**"*
/// </para>
/// <para>
/// ⭐ <b>D19 is why this is a prerequisite rather than content</b>: a binding walk-distance kills
/// outlying households when there is only one raw food source, so *"hunter and fisher are not
/// content — they are the prerequisite for §2.2's central rule being survivable rather than merely
/// cruel."*
/// </para>
/// </remarks>
public sealed class FishingTests
{
    private readonly ITestOutputHelper _output;

    public FishingTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimWorld World() =>
        SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;

    /// <summary>A buildable tile with the river beside it.</summary>
    private static GridPos ABankTile(SimWorld world)
    {
        GridPos site = world.Map.FoundingSite;
        for (int radius = 1; radius < 60; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);
                    if (world.CanBuildAt(BuildingKind.FishingHut, at).Allowed)
                    {
                        return at;
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException("Nowhere on the bank was buildable.");
    }

    // ---------------------------------------------------------------
    //  § It has to touch the water
    // ---------------------------------------------------------------

    /// <summary>⭐ A hut on the bank is allowed; one in a meadow is refused, and told why.</summary>
    /// <remarks>
    /// <b>⛔ THE FIRST POSITIVE TERRAIN RULE IN THE GAME.</b> Every refusal in `CanBuildAt` until
    /// now was an impossibility — under water, occupied, off the map, no route — or a
    /// warn-and-allow. *"It must touch water"* is neither: the meadow is perfectly good ground,
    /// it is simply not the ground this building is for.
    /// </remarks>
    [Fact]
    public void AFishingHutHasToStandAgainstTheWater()
    {
        SimWorld world = World();
        GridPos bank = ABankTile(world);

        PlacementVerdict onTheBank = world.CanBuildAt(BuildingKind.FishingHut, bank);
        Assert.True(onTheBank.Allowed, onTheBank.Reason);

        // ⚠️ DRY, REACHABLE **AND EMPTY** — the first draft used the founding site and got back
        // *"something already stands there"*, which is a true refusal for the wrong reason and
        // would have passed a guard that only checked `Allowed == false`. **The claim is about
        // which sentence the player is told.**
        GridPos meadow = default;
        bool found = false;
        for (int radius = 1; radius < 40 && !found; radius++)
        {
            for (int dy = -radius; dy <= radius && !found; dy++)
            {
                for (int dx = -radius; dx <= radius && !found; dx++)
                {
                    var at = new GridPos(world.Map.FoundingSite.X + dx, world.Map.FoundingSite.Y + dy);
                    if (world.CanBuildAt(BuildingKind.Granary, at).Allowed
                        && !world.CanBuildAt(BuildingKind.FishingHut, at).Allowed)
                    {
                        meadow = at;
                        found = true;
                    }
                }
            }
        }

        Assert.True(found, "Nowhere inland was buildable, so this guard proves nothing.");
        PlacementVerdict inland = world.CanBuildAt(BuildingKind.FishingHut, meadow);

        _output.WriteLine($"on the bank at {bank}: allowed. Inland: {inland.Reason}");

        Assert.False(inland.Allowed);
        Assert.Contains("water", inland.Reason, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ⛔ A hut across the river is refused for having no route — <b>not for the water</b>.
    /// </summary>
    /// <remarks>
    /// <b>D110/D111, guarded by name.</b> <em>"NOT WATER IS NOT THE SAME AS NOT CUT OFF BY
    /// WATER."</em> A tile that touches the river touches it on <b>both banks</b>, and the far
    /// bank is perfectly good ground nobody can walk to. <c>PaintTheStarterZone</c> made exactly
    /// this mistake — it skipped water tiles and painted the far side anyway — and <b>seed 11
    /// froze a whole village for it</b>.
    /// <para>
    /// ⚠️ The claim is about the SENTENCE as much as the refusal: two different mistakes must get
    /// two different answers, or <em>"why not there?"</em> has none (D43).
    /// </para>
    /// </remarks>
    [Fact]
    public void AHutAcrossTheRiverIsRefusedForTheRouteNotTheWater()
    {
        SimWorld world = World();
        GridPos village = world.Map.FoundingSite;

        GridPos? beyond = null;
        for (int dy = 1; dy < world.Map.Height && beyond is null; dy++)
        {
            foreach (int sign in new[] { 1, -1 })
            {
                var at = new GridPos(village.X, village.Y + (dy * sign));
                if (world.Map.Contains(at)
                    && world.Map.TerrainAt(at) != Terrain.Water
                    && !world.TravelCost.CanReach(village, at))
                {
                    beyond = at;
                    break;
                }
            }
        }

        if (beyond is null)
        {
            _output.WriteLine("this seed's river cuts nothing off, so there is no far bank here");
            return;
        }

        PlacementVerdict verdict = world.CanBuildAt(BuildingKind.FishingHut, beyond.Value);
        _output.WriteLine($"across the river at {beyond}: {verdict.Reason}");

        Assert.False(verdict.Allowed);
        Assert.Contains("route", verdict.Reason, System.StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------
    //  § Somebody actually works there
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ The village <b>asks for fishers and posts one</b> to a standing hut.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔⛔ THIS IS THE GUARD THAT WAS MISSING, AND THE BUG WENT STRAIGHT TO JOE.</b> Fishing
    /// shipped with a good, a building, a placement rule, a behaviour branch, a build button and
    /// four guards — and **`LabourQuota` had never heard of `JobKind.Fisher`**, nor had
    /// `LabourAllocator.KindsInOrder`. The quota was structurally zero and no candidate list was
    /// ever built, so the hut stood empty for ever. He built one and watched nothing happen:
    /// *"i added a fisher, but the game never staffed it or worked it."*
    /// </para>
    /// <para>
    /// ⚠️ <b>Every one of those four guards passed.</b> They tested placement, yield and the
    /// absence of a ring — all true, all beside the point. **A trade the labour system does not
    /// know about is a building that cannot work.**
    /// </para>
    /// </remarks>
    [Fact]
    public void TheVillageAsksForFishersAndPostsOne()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace hut = RaiseAFishery(world);
        loop.Step(config.TicksPerYear + 1);

        LabourQuota quota = LabourQuota.For(world);
        _output.WriteLine($"the village wants {quota.For(JobKind.Fisher)} fishers of "
            + $"{quota.Needed(JobKind.Fisher)} it could use; {hut.WorkerIds.Count} are posted");

        Assert.True(world.Population > 0, "The village died, so this guard proves nothing.");
        Assert.True(quota.For(JobKind.Fisher) > 0, "The village never asked for a fisher.");
        Assert.True(hut.WorkerIds.Count > 0, "Nobody was ever posted to the fishing hut.");
    }

    /// <summary>⭐⭐ A fisher standing at the hut catches fish, and carries it away.</summary>
    /// <remarks>
    /// <para>
    /// <b>The other half, and it caught two more bugs.</b> `TravelingToFood`'s mid-walk handler
    /// **hardcoded `Gathering` as the arrival state** — true while foraging was the only walk to
    /// food, and it silently redirected a fisher into a berry patch. And `ArriveAt` began a gather
    /// but not a cast, so a fisher who did arrive stood in the `Fishing` state with no duration
    /// and `CompleteAction` was never reached.
    /// </para>
    /// <para>
    /// ⚠️ <b>POSED AT THE HUT RATHER THAN WALKED TO IT, AND THAT IS DELIBERATE.</b> The first
    /// draft ran a whole village and got lost in its logistics: with every berry patch removed the
    /// larders drained, the fetch errand outranks work, and the fisher spent three years walking
    /// toward a hut twenty-two tiles off and being turned back — a true livelock about hauling,
    /// not about fishing. **The claim here is that a cast produces fish**, so the villager is put
    /// where the claim is.
    /// </para>
    /// </remarks>
    [Fact]
    public void AFisherAtTheHutCatchesFish()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace hut = RaiseAFishery(world);
        loop.Step(config.TicksPerYear + 1);

        Villager fisher = world.Villagers.First(v => v.Alive && v.WorkplaceId == hut.Id);

        // Somewhere to put a catch: a full granary is a village that needs no fisherman, and
        // says so — *"nothing to fish for, every store that takes food is full"*, which is the
        // sim being right rather than a bug.
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            store.Store.TakeAll(Goods.Food);
        }

        // ⭐ STOOD ON THE BANK, which is the half the first draft claimed and did not do. Left to
        // walk it she spent a year oscillating: the hut this seed offers is twenty-two tiles out,
        // and the fetch errand outranks work, so an empty larder turned her round every time.
        fisher.Position = hut.Position;

        int caught = 0;
        for (int tick = 0; tick < config.TicksPerYear && caught == 0; tick++)
        {
            loop.StepOnce();
            caught = fisher.Carried[Goods.Fish];
        }

        _output.WriteLine($"{fisher.Name} was holding {caught} fish; a cast is worth "
            + $"{config.FishYield} before vigour");

        Assert.True(caught > 0, $"{fisher.Name} held the job for a year and never caught anything.");
    }

    /// <summary>Raise a fishing hut outright, without waiting for a builder.</summary>
    private static Workplace RaiseAFishery(SimWorld world)
    {
        GridPos bank = ABankTile(world);
        Assert.True(world.Mark(BuildingKind.FishingHut, bank).Allowed);

        Workplace site = world.Workplaces.Single(
            w => w.Construction?.Kind == BuildingKind.FishingHut);
        BuildFixtures.StockTheSite(site);
        for (int i = 0; i <= site.Construction!.Recipe.WorkTicks; i++)
        {
            site.Construction.Work();
        }

        world.Complete(site);
        return world.Workplaces.Single(w => w.Kind == JobKind.Fisher && !w.IsSite);
    }

    // ---------------------------------------------------------------
    //  § What it is worth
    // ---------------------------------------------------------------

    /// <summary>⭐ A fisher out-earns a forager, measured against what a forager actually brings.</summary>
    /// <remarks>
    /// <b>⚠️ AGAINST THE REAL YIELD, NOT AGAINST <c>gather_yield</c>.</b> The raw key is 145, but
    /// that is the value of a trip at a <b>fully wooded</b> ring; a real hut's ring runs around
    /// half that, so comparing against the key would compare a fishery to a forest that does not
    /// exist. <c>GatherYieldAt</c> is what a forager actually carries home.
    /// </remarks>
    [Fact]
    public void FishingIsAStepUpFromForaging()
    {
        SimWorld world = World();
        Workplace hut = world.Workplaces.First(w => w.GatheringRadius > 0);

        int forager = world.GatherYieldAt(hut);
        int fisher = Config.FishYield;

        _output.WriteLine($"a forager's trip is worth {forager}; a cast is worth {fisher}");

        Assert.True(forager > 0, "The fixture's hut has no trees, so this compares nothing.");
        Assert.True(
            fisher > forager,
            $"A cast brings back {fisher} against a forager's {forager}. Joe's ranking is that "
            + "foraging is bottom of the totem pole, so a fishery has to beat it.");
    }

    /// <summary>⛔ A fishing hut has no ring, so it competes with nothing and thins nothing.</summary>
    /// <remarks>
    /// Joe: <em>"a consistent source of food that does not run out."</em> ⚠️ <b>The absence of a
    /// <c>GatheringRadius</c> is load-bearing rather than an omission</b>: <c>SharersOf</c> asks
    /// <c>GatheringRadius &gt; 0</c> and never <c>JobKind</c>, deliberately, so <em>"a modder's
    /// building is in the rule the day it exists"</em> — and a fishing hut given a ring would
    /// silently start competing with FORAGER huts over TREES.
    /// </remarks>
    [Fact]
    public void AFisheryCompetesWithNothingAndThinsNothing()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        GridPos bank = ABankTile(world);
        Assert.True(world.Mark(BuildingKind.FishingHut, bank).Allowed);

        Workplace site = world.Workplaces.Single(
            w => w.Construction?.Kind == BuildingKind.FishingHut);
        BuildFixtures.StockTheSite(site);
        for (int i = 0; i <= site.Construction!.Recipe.WorkTicks; i++)
        {
            site.Construction.Work();
        }

        world.Complete(site);

        Workplace hut = world.Workplaces.Single(w => w.Kind == JobKind.Fisher && !w.IsSite);

        Assert.Equal(0, hut.GatheringRadius);
        Assert.Equal(config.FishingHutSeats, hut.Capacity);

        // ⚠️ MEASURED ACROSS THE FISHERY APPEARING, NOT ACROSS A YEAR. The first draft stepped a
        // year and compared — and the forager's hut read **77 then 75**, which is `RegrowthSystem`
        // and the seasons doing their job, not the fishery taking anything. *A guard that cannot
        // tell the thing it is testing from the weather is testing the weather.*
        Workplace forage = world.Workplaces.First(w => w.GatheringRadius > 0);
        int worth = world.GatherYieldAt(forage);

        // Stand a second fishery right beside the first: two rings would halve each other.
        GridPos alongside = ABankTile(world);
        if (world.Mark(BuildingKind.FishingHut, alongside).Allowed)
        {
            Workplace second = world.Workplaces.Single(
                w => w.Construction?.Kind == BuildingKind.FishingHut);
            BuildFixtures.StockTheSite(second);
            for (int i = 0; i <= second.Construction!.Recipe.WorkTicks; i++)
            {
                second.Construction.Work();
            }

            world.Complete(second);
        }

        _output.WriteLine($"the fishery seats {hut.Capacity} and holds no ring; with a second one "
            + $"standing the forager's hut is still worth {world.GatherYieldAt(forage)} "
            + $"against {worth}");

        Assert.Equal(worth, world.GatherYieldAt(forage));
    }
}
