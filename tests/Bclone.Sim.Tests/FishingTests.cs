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
            // ⚠️ THE HUT'S STORE AS WELL AS THE ARMS. Since the buffer landed, a catch goes
            // DOWN at the hut and the fisher casts again — so watching only the arms reports
            // zero for a fishery that is working perfectly.
            caught = fisher.Carried[Goods.Fish] + hut.Store[Goods.Fish];
        }

        _output.WriteLine($"{fisher.Name} was holding {caught} fish; a cast is worth "
            + $"{config.FishYield} before vigour");

        Assert.True(caught > 0, $"{fisher.Name} held the job for a year and never caught anything.");
    }

    /// <summary>
    /// ⭐⭐ A fisher <b>walks the whole way to a distant hut and arrives</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔⛔ OTTO'S LOOP, GUARDED (Joe, 2026-09-02).</b> *"Otto the fisherman seems to be
    /// caught in a loop between walking to the fishing hut (far away) and stopping to eat, resting
    /// at home — he never goes to the fishing hut and never catches any fish."*
    /// </para>
    /// <para>
    /// <b>The cause was a reused state.</b> Fishing walked in `VillagerState.TravelingToFood`, and
    /// two predicates read that as foraging: `ErrandKind` maps it to `JobKind.Forager`, so
    /// `HoldsTheJobFor` was false for a fisher and **`GoHome` fired on the very next tick** — for
    /// ever. `IsForaging` also meant **winter recalled him**, which is the one season a fishery
    /// exists for. ⭐ *A state is not a label; it is whatever every predicate in the file
    /// classifies it as.*
    /// </para>
    /// <para>
    /// ⚠️ <b>I SAW THIS EXACT LOOP IN MY OWN FIXTURE AND DISMISSED IT.</b> While posing the
    /// staffing guard a fisher oscillated for three years toward a hut twenty-two tiles off; I
    /// wrote it off as a starving-village artefact of the pose and moved the villager instead of
    /// asking why. **It was this bug, and it went to Joe.** *A fixture that misbehaves is
    /// evidence, not an inconvenience.*
    /// </para>
    /// </remarks>
    [Fact]
    public void AFisherWalksTheWholeWayToADistantHut()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace hut = RaiseAFishery(world, TheFurthestBankTile(world));
        loop.Step(config.TicksPerYear + 1);

        Villager fisher = world.Villagers.First(v => v.Alive && v.WorkplaceId == hut.Id);
        int walk = world.TravelCost.Cost(fisher.Position, hut.Position)
            / TravelCostField.BaseTileCost;

        // Somewhere to put a catch, so the village genuinely wants the trip made.
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            store.Store.TakeAll(Goods.Food);
        }

        bool arrived = false;
        for (int tick = 0; tick < config.TicksPerYear && !arrived; tick++)
        {
            loop.StepOnce();
            arrived = fisher.Position == hut.Position;
        }

        _output.WriteLine($"{fisher.Name} started {walk} tiles from the hut and "
            + $"{(arrived ? "arrived" : "never got there")}");

        Assert.True(walk > 1, "The fisher started at the hut, so this guard proves nothing.");
        Assert.True(
            arrived,
            $"{fisher.Name} held the job for a year and never reached the hut {walk} tiles away — "
            + "something is recalling them mid-walk.");
    }

    /// <summary>⛔ And winter does not recall a fisher, which is the season they matter in.</summary>
    /// <remarks>
    /// Nothing can be picked in winter (D44) and a river does not stop. <b>The winter recall reads
    /// `IsForaging`</b>, so a fisher walking in a foraging state was marched home every winter —
    /// deleting the fishery in exactly the season D19 says outlying households cannot feed
    /// themselves.
    /// </remarks>
    [Fact]
    public void WinterDoesNotRecallAFisher()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace hut = RaiseAFishery(world);
        loop.Step(config.TicksPerYear + 1);

        Villager fisher = world.Villagers.First(v => v.Alive && v.WorkplaceId == hut.Id);
        fisher.Position = hut.Position;

        // Run to winter, keeping room for a catch so the only question is the season.
        int caught = 0;
        for (int tick = 0; tick < config.TicksPerYear * 2 && caught == 0; tick++)
        {
            loop.StepOnce();

            if (world.Clock.Season != Season.Winter)
            {
                continue;
            }

            foreach (StoreBuilding store in world.StoreBuildings)
            {
                store.Store.TakeAll(Goods.Food);
            }

            caught = fisher.Carried[Goods.Fish];
        }

        _output.WriteLine($"in winter {fisher.Name} was holding {caught} fish");
        Assert.True(caught > 0, "Winter stopped the fishing, and a river does not freeze here.");
    }

    // ---------------------------------------------------------------
    //  § The hut holds its catch — Joe, 2026-09-03
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ The catch goes into <b>the hut's own store</b>, not straight home.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe:</b> *"The fishing hut should have **300 storage space** which the marketer fetches.
    /// The fisherman also brings it to storage when it is full."* ⭐ It is the farmhouse's pattern
    /// exactly — `BuildingRow.LocalStoreCap`, and the reason it exists: **the store underfoot
    /// fills first and the walk lengthens once it is full.**
    /// </para>
    /// <para>
    /// ⚠️ <b>WRITTEN BEFORE THE FEATURE, DELIBERATELY.</b> Fishing has now shipped broken twice
    /// — unstaffable (D279) and unable to walk (D281) — and on both occasions every placement and
    /// yield guard passed while the thing did nothing. *The end-to-end claim goes first now.*
    /// </para>
    /// </remarks>
    [Fact]
    public void TheCatchGoesIntoTheHutsOwnStore()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace hut = RaiseAFishery(world);
        loop.Step(config.TicksPerYear + 1);

        Villager fisher = world.Villagers.First(v => v.Alive && v.WorkplaceId == hut.Id);
        fisher.Position = hut.Position;

        foreach (StoreBuilding store in world.StoreBuildings)
        {
            store.Store.TakeAll(Goods.Food);
        }

        int inTheHut = 0;
        for (int tick = 0; tick < config.TicksPerYear && inTheHut == 0; tick++)
        {
            loop.StepOnce();
            inTheHut = hut.Store[Goods.Fish];
        }

        _output.WriteLine($"{hut.Name} holds {inTheHut} fish of {hut.Store.Capacity} it can take");

        Assert.Equal(config.FishingHutStoreCap, hut.Store.Capacity);
        Assert.True(inTheHut > 0, "The catch never reached the hut's own store.");
    }

    /// <summary>
    /// ⭐⭐ A marketer <b>runs the fishery's buffer dry</b>, the way one runs a farm's.
    /// </summary>
    /// <remarks>
    /// <b>⛔ TWO THINGS BLOCKED FISH FROM THIS PATH, AND BOTH WERE FARM-SHAPED.</b>
    /// <c>SimWorld.BufferWorthClearing</c> asked <c>workplace.Store.Food &gt; 0</c> — so a hut full
    /// of fish read as empty — and measured "nearly full" against <c>CropYieldPerTile</c>, a
    /// farm's number. And <c>MarketGoods</c> was a hardcoded <c>{ Food, Firewood }</c> with an
    /// <c>if (goods == Goods.Food) … else … Firewood</c> **inside its own loop**, which would have
    /// put a load of fish down as firewood.
    /// </remarks>
    [Fact]
    public void AMarketerRunsTheFisheryBufferDry()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace hut = RaiseAFishery(world);
        loop.Step(config.TicksPerYear + 1);

        // ⚠️ A MARKETER IS PINNED, because the village will not spare one here and should not.
        // Marketers are asked LAST of every trade (D14: *"a marketer moves goods that already
        // exist, so a village that cannot spare anyone loses convenience rather than lives"*), and
        // a four-adult founding has nobody left by the time the question is reached — measured:
        // *"wants 0 marketers, 0 posted"*, with the errand correctly counted. **That is the sim
        // being right about priorities**, so the pose supplies the hand. A pin is a floor the
        // quota cannot argue down, which is exactly what a posed hand needs to be.
        Villager trader = world.Villagers.First(
            v => v.Alive && v.CanWork && v.WorkplaceId != hut.Id);
        world.SetPinnedTrade(trader, JobKind.Marketer);

        // Fill the buffer outright, so the question is only whether anybody comes for it.
        //
        // ⛔⚠️ AND KEEP THE STORES STOCKED WHILE DOING IT. Three hundred fish in a buffer counts
        // toward `FoodTheVillageHolds`, so the village concludes it is fed and stops foraging —
        // while the fish sits somewhere only a marketer can reach. **Measured: population 3 → 0,
        // five starved.** *That is a real trap and not only a fixture artefact* — a full fishery
        // with nobody to empty it is D79's "full granary, empty larder" one building over — but it
        // is not what this guard is about, so the larders are kept full while the question is put.
        foreach (Household household in world.Households)
        {
            int wanted = world.TargetFoodFor(household);
            if (world.FoodIn(household.Stockpile) < wanted)
            {
                household.Stockpile.Add(Goods.Food, wanted);
            }
        }

        hut.Store.Receive(Goods.Fish, hut.Store.Capacity);
        int filled = hut.Store[Goods.Fish];

        Assert.True(world.BufferWorthClearing(hut),
            "A fishery brimming with fish is not seen as worth clearing.");

        int lowest = filled;
        for (int tick = 0; tick < config.TicksPerYear * 2; tick++)
        {
            loop.StepOnce();
            lowest = System.Math.Min(lowest, hut.Store[Goods.Fish]);

            if (tick % 60 == 0)
            {
                foreach (Household household in world.Households)
                {
                    int wanted = world.TargetFoodFor(household);
                    if (world.FoodIn(household.Stockpile) < wanted)
                    {
                        household.Stockpile.Add(Goods.Food, wanted);
                    }
                }
            }
        }


        _output.WriteLine($"the buffer went from {filled} down to {lowest}");
        Assert.True(lowest < filled, "Nobody ever came to empty the fishery.");
    }

    /// <summary>
    /// The <b>furthest</b> buildable bank tile — for the guard about a long walk.
    /// </summary>
    /// <remarks>
    /// ⚠️ <c>ABankTile</c> returns the NEAREST, and on the shipped seed that is one tile from
    /// the founding site — so a guard about walking a long way was measuring a walk of one.
    /// Joe's Otto was **seventeen tiles out**, which is the case that broke.
    /// </remarks>
    private static GridPos TheFurthestBankTile(SimWorld world)
    {
        GridPos site = world.Map.FoundingSite;
        GridPos best = ABankTile(world);
        int furthest = -1;

        for (int y = world.Map.MinY; y < world.Map.MinY + world.Map.Height; y++)
        {
            for (int x = world.Map.MinX; x < world.Map.MinX + world.Map.Width; x++)
            {
                var at = new GridPos(x, y);
                if (!world.CanBuildAt(BuildingKind.FishingHut, at).Allowed)
                {
                    continue;
                }

                int cost = world.TravelCost.Cost(site, at);
                if (cost != TravelCostField.Unreachable && cost > furthest)
                {
                    furthest = cost;
                    best = at;
                }
            }
        }

        return best;
    }

    /// <summary>Raise a fishing hut outright, without waiting for a builder.</summary>
    private static Workplace RaiseAFishery(SimWorld world, GridPos? at = null)
    {
        GridPos bank = at ?? ABankTile(world);
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

    /// <summary>
    /// ⭐⭐ A fisher out-earns a forager <b>over a year</b> — both measured while actually working.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔ THIS GUARD USED TO COMPARE ONE CAST AGAINST ONE TRIP, AND THAT COMPARISON WAS
    /// MEANINGLESS.</b> It read <c>fish_yield</c> (100) against <c>GatherYieldAt</c> (77) and
    /// concluded fishing won. But a per-load comparison silently assumes <b>both jobs get the same
    /// number of loads</b>, and they do not: measured over a year in the fixture village, a fisher
    /// lands <b>~8 casts</b> and a forager makes <b>~7 trips</b> — against the <c>TripsPerYear</c>
    /// ceiling of 17 that neither of them reaches.
    /// </para>
    /// <para>
    /// ⭐ <b>Which is why <c>fish_ticks</c> could go 3 → 10 without touching the economy.</b> The
    /// cast was never the bottleneck — a fisher spends about a fifth of the year casting and the
    /// rest walking, eating and sleeping. Measured: <b>800 fish a year at three ticks and 800 at
    /// ten</b>, while time spent casting went 26 ticks → 94. Joe asked for a slower cast and got
    /// exactly that and nothing else.
    /// </para>
    /// <para>
    /// ⚠️ <b>Do not "simplify" this back to comparing the two config keys.</b> That is the bug
    /// this guard replaced, and it would pass while a longer cast quietly starved the fishery.
    /// </para>
    /// </remarks>
    [Fact]
    public void AFisherOutEarnsAForagerOverAYear()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace hut = RaiseAFishery(world);
        loop.Step(config.TicksPerYear + 1);

        Villager fisher = world.Villagers.First(v => v.Alive && v.WorkplaceId == hut.Id);
        fisher.Position = hut.Position;

        int caught = 0;
        int held = FishEverywhere(world);

        for (int tick = 0; tick < config.TicksPerYear; tick++)
        {
            loop.StepOnce();

            int now = FishEverywhere(world);
            if (now > held)
            {
                caught += now - held;
            }

            held = now;
        }

        int foraged = WhatAForagerBringsInAYear(config, out int busiest, out int perTrip);

        _output.WriteLine(
            $"over one year a fisher landed {caught} fish; in a village with no fishery the busiest "
            + $"forager spent {busiest} ticks gathering = ~{busiest / config.GatherTicks} trips of "
            + $"{perTrip} = ~{foraged} food (the TripsPerYear ceiling neither reaches is "
            + $"{VillageEconomy.TripsPerYear(config)})");

        Assert.True(perTrip > 0, "The fixture's hut has no trees, so this compares nothing.");
        Assert.True(foraged > 0, "No one foraged all year, so there is nothing to compare against.");
        Assert.True(
            caught > foraged,
            $"A fisher landed {caught} in the year against a forager's {foraged}. Joe's ranking is "
            + "that foraging is bottom of the totem pole, so a fishery has to beat it over a year "
            + "— not merely per load.");
    }

    /// <summary>
    /// What the busiest forager brings home in a year, <b>in a village with no fishery in it</b>.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>THE REFERENCE MUST COME FROM AN UNTOUCHED VILLAGE, AND THIS COST A RED CHECK TO
    /// LEARN.</b> Measuring both sides in the same world looks tidier and is nearly worthless:
    /// halving <c>fish_yield</c> also makes the village poorer, so the foragers make fewer trips
    /// and the comparison quietly re-balances — measured, <b>400 fish against 308 food, still
    /// passing</b>, when the honest reference was 539. <b>A guard whose baseline moves with the
    /// thing it is guarding is not a guard.</b>
    /// </remarks>
    private static int WhatAForagerBringsInAYear(SimConfig config, out int busiest, out int perTrip)
    {
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        loop.Step(config.TicksPerYear + 1);

        var gatherTicks = new Dictionary<int, int>();
        for (int tick = 0; tick < config.TicksPerYear; tick++)
        {
            loop.StepOnce();
            foreach (Villager villager in world.Villagers)
            {
                if (villager.Alive && villager.State == VillagerState.Gathering)
                {
                    gatherTicks[villager.Id] = gatherTicks.GetValueOrDefault(villager.Id) + 1;
                }
            }
        }

        Workplace ring = world.Workplaces.First(w => w.GatheringRadius > 0);
        perTrip = world.GatherYieldAt(ring);
        busiest = gatherTicks.Count == 0 ? 0 : gatherTicks.Values.Max();
        return busiest / config.GatherTicks * perTrip;
    }

    /// <summary>Fish is only ever destroyed by eating, so a rise in the total is production.</summary>
    private static int FishEverywhere(SimWorld world)
    {
        int total = 0;
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            total += store.Store[Goods.Fish];
        }

        foreach (Workplace workplace in world.Workplaces)
        {
            total += workplace.Store[Goods.Fish];
        }

        foreach (Villager villager in world.Villagers)
        {
            total += villager.Carried[Goods.Fish];
        }

        return total;
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
