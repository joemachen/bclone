using System.Text.Json.Nodes;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.Persistence;
using Bclone.Sim.Systems;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// `specs/handcart.md §3–§7` — Slice H: the household's handcart. Fetched the way a coat is (one per household, from a
/// store that racks tools, never the market), parked at the home, taken from the doorstep by the shopper, worn a use a
/// round trip, slower on grass and quick on a path, and every tile its wheels cross is a path at once. The workshop
/// that makes it is Slice W's (`WorkshopTests`).
/// </summary>
public sealed class HandcartTests
{
    private readonly ITestOutputHelper _output;

    public HandcartTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static StoreBuilding TheWarehouse(SimWorld world) => world.AnyStoreOf(StoreKind.Warehouse);

    /// <summary>The households a cart can serve: a roof and somebody under it (`workshop-and-the-carpenter.md §4.4`).</summary>
    private static List<Household> Housed(SimWorld world) =>
        world.Households.Where(h => h.HasHome && world.LivingMembersOf(h) > 0).ToList();

    private static void SetLarder(Household home, int food)
    {
        home.Stockpile.TryTake(Goods.Produce, home.Stockpile[Goods.Produce]);
        home.Stockpile.Add(Goods.Produce, food);
    }

    private static void NobodyWorks(SimWorld world)
    {
        foreach (JobKind kind in JobLimits.Kinds)
        {
            world.SetJobLimit(kind, 0);
        }
    }

    // ---------------------------------------------------------------
    //  §3 — the household's cart, fetched like a coat and parked at home
    // ---------------------------------------------------------------

    /// <summary>
    /// §3: a housed household with no cart sends a free member to the nearest store holding one; the cart walks home
    /// on the fetcher's state and parks on the household (<c>HandcartUses</c> = <c>handcart_uses</c>). One fetcher a
    /// household, and none once it has one.
    /// </summary>
    [Fact]
    public void AHouseholdWithoutACartFetchesOneAndWalksItHome()
    {
        SimConfig config = Config;
        var sink = new InMemoryLogSink();
        SimLoop loop = SimFactory.CreatePhase0(config, sink);
        SimWorld world = loop.World;
        ToolsTests.KeepTheVillageWarmAndFed(world);
        int housed = Housed(world).Count;
        Assert.True(housed > 0, "The fixture founders are homeless, so nobody could park a cart.");
        TheWarehouse(world).Store.Add(Goods.Handcart, 3);

        // ⚠️ Everybody starts at their own door, not beside the shelves — so a fetch is a WALK and a second member can
        // be seen setting off while the first is out (red-checked: posed at the shelves the take is instant and the
        // one-fetcher and the carted-household rules both scored zero).
        foreach (Villager v in world.Villagers.Where(v => v.Alive))
        {
            v.StandAt(world.HomePoint(v));
        }

        int fetchingTicks = 0;
        int pushingHomeTicks = 0;
        for (int tick = 0; tick < config.TicksPerSeason; tick++)
        {
            loop.StepOnce();
            foreach (Household household in world.Households)
            {
                // The household's fetchers — a member out for the MARKET's cart (§13) is not one.
                int fetching = household.MemberIds.Count(id => world.FindVillager(id) is { Alive: true, State: VillagerState.FetchingAHandcart, CartForTheWorks: false });
                Assert.True(fetching <= 1, $"{household.Name}: {fetching} members out for the one cart at once.");
                if (household.HandcartUses > 0)
                {
                    Assert.Equal(0, fetching);
                }

                fetchingTicks += fetching;
            }

            pushingHomeTicks += world.Villagers.Count(v => v.State == VillagerState.PushingAHandcart);
        }

        int carted = world.Households.Count(h => h.HandcartUses > 0);
        int onShelves = world.InStores(Goods.Handcart);
        // The market is an owner too since §13 — the allocator seats a marketer here, who may take one of the three.
        int tradesCarted = world.Workplaces.Count(w => w.HandcartUses > 0);
        int handedOver = sink.Entries.Count(e => e.Message.Contains("took a handcart from", StringComparison.Ordinal));
        _output.WriteLine($"{carted} of {housed} households carted + {tradesCarted} trade(s) + {onShelves} on the shelves = {carted + tradesCarted + onShelves}; {handedOver} handed over; "
            + $"{fetchingTicks} villager-ticks walking for one, {pushingHomeTicks} pushing one home.");

        Assert.True(carted > 0, "Nobody fetched a handcart in a season.");
        Assert.Equal(3, carted + tradesCarted + onShelves);
        Assert.Equal(carted + tradesCarted, handedOver);
        Assert.True(fetchingTicks > 0, "A cart was taken without anybody being seen walking for it.");
        Assert.True(pushingHomeTicks > 0, "A cart reached a home without anybody being seen pushing it there.");
        Assert.All(world.Households.Where(h => h.HandcartUses > 0), h => Assert.Equal(config.HandcartUses, h.HandcartUses));
        Assert.All(world.Households.Where(h => h.HandcartUses > 0), h => Assert.True(h.HasHome, $"{h.Name} has a cart and no house to park it at."));
    }

    /// <summary>§3, posed directly: one fetcher a household while a housemate is out for the cart, and none once the household has one.</summary>
    [Fact]
    public void OneFetcherAHouseholdAndNoneOnceCarted()
    {
        SimConfig config = Config;
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;
        ToolsTests.KeepTheVillageWarmAndFed(world);
        NobodyWorks(world);
        TheWarehouse(world).Store.Add(Goods.Handcart, 5);
        Household home = Housed(world).First(h => world.Villagers.Count(v => v.Alive && v.CanWork && v.HouseholdId == h.Id) >= 2);
        List<Villager> adults = world.Villagers.Where(v => v.Alive && v.CanWork && v.HouseholdId == home.Id).ToList();
        foreach (Villager v in world.Villagers)
        {
            v.WorkplaceId = 0;
            v.StandAt(world.HomePoint(v));
        }

        // A housemate is already walking for it: the second stays.
        adults[0].State = VillagerState.FetchingAHandcart;
        BehaviorSystem.DecideForTest(world, adults[1]);
        Assert.NotEqual(VillagerState.FetchingAHandcart, adults[1].State);

        // Nobody out, no cart: the second goes.
        adults[0].State = VillagerState.Idle;
        BehaviorSystem.DecideForTest(world, adults[1]);
        Assert.Equal(VillagerState.FetchingAHandcart, adults[1].State);

        // A carted household, carts still on the shelf: nobody walks for another.
        adults[1].State = VillagerState.Idle;
        adults[1].StandAt(world.HomePoint(adults[1]));
        home.HandcartUses = 3;
        BehaviorSystem.DecideForTest(world, adults[0]);
        Assert.NotEqual(VillagerState.FetchingAHandcart, adults[0].State);
        Assert.Equal(5, TheWarehouse(world).Store[Goods.Handcart]);
    }

    /// <summary>§3: a roofless family wants no cart and fetches none — a cart is parked at a door (Joe, D672 call 1).</summary>
    [Fact]
    public void ARooflessFamilyFetchesNoCart()
    {
        SimConfig config = ShippedConfig.Load();
        var sink = new InMemoryLogSink();
        SimLoop loop = SimFactory.CreatePhase0(config, sink);
        SimWorld world = loop.World;
        Assert.All(world.Households, h => Assert.False(h.HasHome));
        StoreBuilding cart = world.AnyStoreOf(StoreKind.Cart);
        cart.Store.Add(Goods.Handcart, 2);
        Assert.Equal(2, world.InStores(Goods.Handcart));

        // ⚠️ The founders arrive beside the cart's shelves, where a fetch would begin and be refused inside one tick and
        // leave no state to see (red-checked: posed at the shelves this scored zero). Stood a few ticks off, a fetch is a
        // walk the loop below can catch.
        GridPos? off = null;
        for (int r = 3; r < 12 && off is null; r++)
        {
            for (int dx = -r; dx <= r && off is null; dx++)
            {
                var tile = new GridPos(cart.Tile.X + dx, cart.Tile.Y + r);
                int ticks = world.TravelCost.TicksBetween(tile, cart.Tile);
                if (ticks != TravelCostField.Unreachable && ticks > 2)
                {
                    off = tile;
                }
            }
        }

        Assert.NotNull(off);
        foreach (Villager v in world.Villagers)
        {
            v.StandAt(Point.CentreOf(off!.Value));
        }

        for (int tick = 0; tick < config.TicksPerSeason / 2; tick++)
        {
            loop.StepOnce();
            Assert.DoesNotContain(world.Villagers, v => v.State is VillagerState.FetchingAHandcart or VillagerState.PushingAHandcart);
        }

        // ⚠️ The founders stand beside the cart's shelves, so a fetch there would begin and be refused inside one tick
        // — the audit trail is what sees it (red-checked: the state alone scored zero).
        Assert.Equal(0, sink.Entries.Count(e => e.Message.Contains("fetching a handcart", StringComparison.Ordinal)));
        Assert.Equal(2, world.InStores(Goods.Handcart));
        Assert.All(world.Households, h => Assert.Equal(0, h.HandcartUses));
    }

    /// <summary>§3: a cart on a market's counter is nobody's to fetch — the row's stores only, as a tool's (D672 call 1).</summary>
    [Fact]
    public void ACartIsFetchedFromWhereToolsAreAndNeverTheMarket()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        ToolsTests.KeepTheVillageWarmAndFed(world);
        StoreBuilding market = world.AnyStoreOf(StoreKind.Market);
        Assert.False(market.Gives(Goods.Handcart));

        // Straight into the counter's stockpile, past the row — the one way a cart could ever be there.
        market.Store.Add(Goods.Handcart, 2);
        for (int tick = 0; tick < config.TicksPerSeason / 2; tick++)
        {
            loop.StepOnce();
            Assert.DoesNotContain(world.Villagers, v => v.State == VillagerState.FetchingAHandcart);
        }

        Assert.Equal(2, market.Store[Goods.Handcart]);
        Assert.All(world.Households, h => Assert.Equal(0, h.HandcartUses));
    }

    /// <summary>§3: a household that dies out leaves its cart at the home for the next family — the larder's own rule.</summary>
    [Fact]
    public void ADeadHouseholdLeavesItsCartAtTheHome()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        Household gone = Housed(world)[0];
        int wantedAtStart = LabourQuota.HandcartsWanted(world);
        gone.HandcartUses = 17;
        Assert.Equal(wantedAtStart - 1, LabourQuota.HandcartsWanted(world));

        // The last of them dies on the road with the cart in their hands: a MortalitySystem death, so the cart is seen
        // to stay the household's and the pusher's flag to clear with them.
        List<Villager> members = world.Villagers.Where(v => v.HouseholdId == gone.Id).ToList();
        Villager last = members[^1];
        foreach (Villager villager in members.Where(v => v != last))
        {
            villager.Alive = false;
        }

        last.PushingACart = true;
        last.AgeYears = last.LifespanYears;
        loop.StepOnce();
        Assert.False(last.Alive, "The posed death did not happen.");
        Assert.False(last.PushingACart, "The dead keep pushing.");

        // A dead household is not counted either way; its cart waits for the next family and wears nothing meanwhile.
        Assert.Equal(wantedAtStart - 1, LabourQuota.HandcartsWanted(world));
        loop.Step(config.TicksPerYear);

        Assert.True(gone.HasHome, "The dead family's home fell, so there is nowhere for a cart to wait.");
        Assert.Equal(17, gone.HandcartUses);
        Assert.Equal(
            world.Households.Count(h => h.HasHome && h.HandcartUses <= 0 && world.LivingMembersOf(h) > 0)
                + world.Workplaces.Count(w => w.IsACartOwner && w.HandcartUses <= 0),
            LabourQuota.HandcartsWanted(world));
    }

    /// <summary>
    /// §4.4 of the workshop spec, finished here: the village wants a cart for every housed household <b>without one</b>,
    /// so the carpenter stops and the professions panel stays honest once the households are carted.
    /// </summary>
    [Fact]
    public void AHouseholdWithACartWantsNoneFromTheCarpenter()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        List<Household> housed = Housed(world);
        Assert.True(housed.Count >= 2, "Two housed households are needed to tell a count from a flag.");
        int wanted = LabourQuota.HandcartsWanted(world);
        int shortfall = LabourQuota.HandcartsShortfall(world);
        Assert.Equal(housed.Count + world.Workplaces.Count(w => w.IsACartOwner), wanted); // the fixture's market is an owner too (§13.2)

        housed[0].HandcartUses = 40;
        Assert.Equal(wanted - 1, LabourQuota.HandcartsWanted(world));
        Assert.Equal(shortfall - 1, LabourQuota.HandcartsShortfall(world));

        housed[1].HandcartUses = 1;
        Assert.Equal(wanted - 2, LabourQuota.HandcartsWanted(world));

        // A cart on its way home is the household's already.
        housed[1].HandcartUses = 0;
        Assert.Equal(wanted - 1, LabourQuota.HandcartsWanted(world));
    }

    // ---------------------------------------------------------------
    //  §4 — the shopper takes it from the doorstep
    // ---------------------------------------------------------------

    /// <summary>
    /// §4: a shopper setting out from home with the household's cart pushes it to the shop and home again
    /// (<c>Villager.PushingACart</c> on the way out, off at the door); one pusher a household at a time; and a trip
    /// begun anywhere but the doorstep takes no cart — a cart kept anywhere else would cost a walk to get it.
    /// </summary>
    [Fact]
    public void TheShopperTakesTheCartFromTheDoorstepAndParksItOnReturn()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        NobodyWorks(world);
        world.AnyStoreOf(StoreKind.Granary).Store.Receive(Goods.Produce, 2000);
        Household home = Housed(world)[0];
        home.HandcartUses = config.HandcartUses;
        GridPos door = home.HomeTile!.Value;
        SetLarder(home, world.TargetFoodFor(home) * 40 / 100);

        var lastTile = new Dictionary<int, GridPos>();
        var wasPushing = new HashSet<int>();
        int pushingOut = 0;
        int pushingBack = 0;
        int parkedAgain = 0;
        for (int tick = 0; tick < config.TicksPerSeason; tick++)
        {
            foreach (Villager v in world.Villagers)
            {
                lastTile[v.Id] = v.Tile;
            }

            loop.StepOnce();

            int pushers = 0;
            foreach (int id in home.MemberIds)
            {
                if (world.FindVillager(id) is not { Alive: true } member)
                {
                    continue;
                }

                if (member.PushingACart)
                {
                    pushers++;
                    if (!wasPushing.Add(id))
                    {
                        pushingOut += member.State == VillagerState.FetchingFromStore ? 1 : 0;
                        pushingBack += member.State == VillagerState.TravelingHome ? 1 : 0;
                    }
                    else
                    {
                        // The tick the cart was taken: they decided at the door, and the leg walked since is this tick's.
                        Assert.Equal(door, lastTile[id]);
                    }
                }
                else if (wasPushing.Remove(id))
                {
                    parkedAgain++;
                    Assert.Equal(door, member.Tile);
                }
            }

            Assert.True(pushers <= 1, $"{pushers} members of {home.Name} pushing the one cart on tick {tick}.");
        }

        _output.WriteLine($"{home.Name}: {pushingOut} ticks pushing the cart to the shop, {pushingBack} pushing a load home, parked again {parkedAgain} times.");
        Assert.True(pushingOut > 0, "Nobody set off with the cart.");
        Assert.True(parkedAgain > 0, "The cart never came home.");
        Assert.All(world.Villagers.Where(v => v.HouseholdId != home.Id), v => Assert.False(v.PushingACart, $"{v.Name} pushes a cart their household does not own."));
    }

    /// <summary>§4: one pusher a household — a second shopper setting off while the cart is out goes on foot.</summary>
    [Fact]
    public void OnePusherAHousehold()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        NobodyWorks(world);
        world.AnyStoreOf(StoreKind.Granary).Store.Receive(Goods.Produce, 2000);
        Household home = Housed(world).First(h => world.Villagers.Count(v => v.Alive && v.CanWork && v.HouseholdId == h.Id) >= 2);
        home.HandcartUses = config.HandcartUses;
        List<Villager> adults = world.Villagers.Where(v => v.Alive && v.CanWork && v.HouseholdId == home.Id).ToList();
        foreach (Villager v in world.Villagers)
        {
            v.WorkplaceId = 0;
        }

        // The first is out with the cart already; the second decides at the door with the larder low.
        Villager out_ = adults[0];
        out_.PushingACart = true;
        out_.State = VillagerState.TravelingHome;
        out_.StandAt(world.AnyStoreOf(StoreKind.Granary).Position);
        Villager second = adults[1];
        second.StandAt(world.HomePoint(second));
        SetLarder(home, world.TargetFoodFor(home) * 20 / 100);

        BehaviorSystem.DecideForTest(world, second);

        Assert.Equal(VillagerState.FetchingFromStore, second.State);
        Assert.False(second.PushingACart, "Two members of one household set off pushing the one cart.");
    }

    /// <summary>§4: a shopper deciding away from home — at the granary, say — takes no cart; the cart stays at the door.</summary>
    [Fact]
    public void AShopperSettingOutFromElsewhereTakesNoCart()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        NobodyWorks(world);
        StoreBuilding granary = world.AnyStoreOf(StoreKind.Granary);
        granary.Store.Receive(Goods.Produce, 2000);
        Household home = Housed(world)[0];
        home.HandcartUses = config.HandcartUses;
        Villager shopper = world.Villagers.First(v => v.Alive && v.CanWork && v.HouseholdId == home.Id);
        SetLarder(home, world.TargetFoodFor(home) * 40 / 100);

        // Nobody holds a seat, so the spare-hand rule (D385) cannot hand the trip to a housemate at home.
        foreach (Villager v in world.Villagers)
        {
            v.WorkplaceId = 0;
        }

        shopper.StandAt(granary.Position);
        Assert.NotEqual(home.HomeTile, shopper.Tile);
        Assert.True(BehaviorSystem.PlanFetchForTest(world, shopper) is not null,
            $"The pose does not want a fetch: larder {world.FoodIn(home.Stockpile)} of {world.TargetFoodFor(home)}, granary {world.FoodIn(granary.Store)}.");

        BehaviorSystem.DecideForTest(world, shopper);

        // Standing at the counter already, the trip is decided and collected in one call: an armful in their arms,
        // the walk home begun — and no cart, because the trip did not begin at the door.
        Assert.Equal(VillagerState.TravelingHome, shopper.State);
        Assert.True(shopper.IsCarrying, "The shopper decided at the granary and left with nothing.");
        Assert.False(shopper.PushingACart, "A shopper who was nowhere near the door set off pushing the cart.");
        Assert.Equal(config.HandcartUses, home.HandcartUses);
    }

    // ---------------------------------------------------------------
    //  §5 — what it buys
    // ---------------------------------------------------------------

    /// <summary>
    /// §5: with the cart in hand a shopper brings `cart_armfuls` armfuls home — the shortfall up to that, never more than the
    /// larder has room for; on foot the same shopper brings one armful. `carry_capacity` itself is untouched (D537).
    /// </summary>
    [Fact]
    public void ACartedShopperBringsCartArmfulsHomeAndAWalkerOne()
    {
        // ⚠️ At an armful of 40, on purpose (D537's own pose): a founding couple wants 150, which a cart of three shipped armfuls
        // (240) swallows whole, so the cap could not be seen at 80. The rule never reads the armful's size.
        SimConfig config = Config with { CarryCapacity = 40 };
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;
        Assert.Equal(80, ShippedConfig.Load().CarryCapacity);
        Assert.True(config.CartArmfuls >= 2, "The pose cannot tell a cart from an armful at one armful a cart.");
        StoreBuilding granary = world.AnyStoreOf(StoreKind.Granary);
        granary.Store.Receive(Goods.Produce, 5000);
        Household home = Housed(world)[0];
        Villager shopper = world.Villagers.First(v => v.Alive && v.CanWork && v.HouseholdId == home.Id);
        int target = world.TargetFoodFor(home);
        Assert.True(target > config.CarryCapacity * config.CartArmfuls,
            $"a target of {target} fits in a cart's {config.CarryCapacity * config.CartArmfuls}, so the cap could not be seen");
        SetLarder(home, 0);
        shopper.StandAt(granary.Position);

        // On foot: one armful.
        shopper.PushingACart = false;
        BehaviorSystem.CollectForTest(world, shopper);
        int onFoot = shopper.Carried.Held;
        shopper.Carried.TakeAll(Goods.Produce);

        // With the cart: cart_armfuls of them, the larder's room allowing.
        shopper.PushingACart = true;
        home.HandcartUses = config.HandcartUses;
        BehaviorSystem.CollectForTest(world, shopper);
        int pushed = shopper.Carried.Held;
        _output.WriteLine($"{home.Name} wants {target}: on foot {onFoot}, with the cart {pushed} (the larder has room for {home.Stockpile.FreeSpace}).");

        Assert.Equal(config.CarryCapacity, onFoot);
        Assert.Equal(Math.Min(config.CarryCapacity * config.CartArmfuls, Math.Min(target, home.Stockpile.FreeSpace)), pushed);
        Assert.True(pushed > onFoot, "The cart carried no more than the arms.");
    }

    /// <summary>
    /// §5: the trip's worth is the cart's when the cart goes — a market stocked for an armful is not stocked for three, so a
    /// carted shopper at the door shops at the storehouse that holds the trip's worth (D372's scrap rule, the cart's size).
    /// </summary>
    [Fact]
    public void ThePlanAsksForTheCartsWorth()
    {
        // At an armful of 40, as above: the couple's 150 is then over three armfuls, so a cart's worth differs from an armful's.
        SimConfig config = Config with { CarryCapacity = 40 };
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;
        NobodyWorks(world);
        foreach (Villager v in world.Villagers)
        {
            v.WorkplaceId = 0;
        }

        StoreBuilding market = world.AnyStoreOf(StoreKind.Market);
        StoreBuilding granary = world.AnyStoreOf(StoreKind.Granary);
        foreach (StoreBuilding s in world.StoreBuildings)
        {
            s.Store.TakeAll(Goods.Produce);
        }

        Household home = Housed(world)[0];
        Villager shopper = world.Villagers.First(v => v.Alive && v.CanWork && v.HouseholdId == home.Id);
        shopper.StandAt(world.HomePoint(shopper));
        SetLarder(home, 0);
        int target = world.TargetFoodFor(home);

        // The market holds one armful and a little; the granary holds plenty.
        market.Store.Receive(Goods.Produce, config.CarryCapacity + 10);
        granary.Store.Receive(Goods.Produce, 2000);
        Assert.True(target >= config.CarryCapacity * 2, $"a target of {target} is under two armfuls, so the cart's worth is the armful's");

        home.HandcartUses = 0;
        Assert.Same(market, BehaviorSystem.PlanFetchForTest(world, shopper));

        home.HandcartUses = config.HandcartUses;
        Assert.Same(granary, BehaviorSystem.PlanFetchForTest(world, shopper));

        // And stocked for the cart's trip, the market wins again.
        market.Store.Receive(Goods.Produce, config.CarryCapacity * config.CartArmfuls);
        Assert.Same(market, BehaviorSystem.PlanFetchForTest(world, shopper));
    }

    // ---------------------------------------------------------------
    //  §6 — wear
    // ---------------------------------------------------------------

    /// <summary>§6: a shopping round trip wears the cart one trip, taken at the door; a trip that never comes home wears none yet.</summary>
    [Fact]
    public void ARoundTripWearsTheCartOneTrip()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        NobodyWorks(world);
        world.AnyStoreOf(StoreKind.Granary).Store.Receive(Goods.Produce, 2000);
        Household home = Housed(world)[0];
        home.HandcartUses = 9;
        SetLarder(home, world.TargetFoodFor(home) * 40 / 100);

        int tripsSeen = 0;
        var wasPushing = new HashSet<int>();
        for (int tick = 0; tick < config.TicksPerSeason && tripsSeen == 0; tick++)
        {
            loop.StepOnce();
            foreach (int id in home.MemberIds)
            {
                if (world.FindVillager(id) is not { Alive: true } member)
                {
                    continue;
                }

                if (member.PushingACart)
                {
                    wasPushing.Add(id);
                    Assert.Equal(9, home.HandcartUses); // Nothing is worn on the road.
                }
                else if (wasPushing.Remove(id))
                {
                    tripsSeen++;
                }
            }
        }

        Assert.True(tripsSeen > 0, "No round trip was made in a season.");
        Assert.Equal(8, home.HandcartUses);
    }

    /// <summary>§6: at nought the cart is gone — the log says so — and the household fetches another from the shelf.</summary>
    [Fact]
    public void AWornOutCartIsGoneAndAnotherIsFetched()
    {
        SimConfig config = Config;
        var sink = new InMemoryLogSink();
        SimLoop loop = SimFactory.CreatePhase0(config, sink);
        SimWorld world = loop.World;
        NobodyWorks(world);
        ToolsTests.KeepTheVillageWarmAndFed(world);
        world.AnyStoreOf(StoreKind.Granary).Store.Receive(Goods.Produce, 2000);
        TheWarehouse(world).Store.Add(Goods.Handcart, 2);
        Household home = Housed(world)[0];
        home.HandcartUses = 1;
        SetLarder(home, world.TargetFoodFor(home) * 30 / 100);
        foreach (Villager v in world.Villagers)
        {
            v.StandAt(world.HomePoint(v));
        }

        // Every other household carted already, so the shelf's two are this household's to fetch.
        foreach (Household other in Housed(world).Where(h => h != home))
        {
            other.HandcartUses = config.HandcartUses;
        }

        bool wornOut = false;
        for (int tick = 0; tick < config.TicksPerSeason * 2; tick++)
        {
            loop.StepOnce();
            wornOut |= home.HandcartUses == 0 && !home.MemberIds.Any(id => world.FindVillager(id) is { Alive: true, PushingACart: true });
            if (wornOut && home.HandcartUses == config.HandcartUses)
            {
                break;
            }
        }

        Assert.True(wornOut, "The cart on its last trip never wore out in two seasons.");
        Assert.Equal(1, sink.Entries.Count(e => e.Message.Contains("handcart is worn through", StringComparison.Ordinal)));
        Assert.Equal(config.HandcartUses, home.HandcartUses);
        Assert.Equal(1, TheWarehouse(world).Store[Goods.Handcart]);
    }

    // ---------------------------------------------------------------
    //  §6–§7 — slow on grass, quick on the path; a cart's pass makes a path
    // ---------------------------------------------------------------

    /// <summary>
    /// §6: a leg's grass tiles cost `cart_off_path_slow_percent` of their ticks under a cart and a packed lane costs what it
    /// always did — the walk slows, the grass count the lane rule reads is unchanged, and the route is the walker's.
    /// </summary>
    [Fact]
    public void ACartIsSlowOnGrassAndQuickOnThePath()
    {
        SimConfig config = Config;
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;
        Assert.True(config.CartOffPathSlowPercent > 100, "At 100 a cart is no slower, so the pose could show nothing.");
        GridPos? start = ClearStretch(world, across: 6, down: 1);
        Assert.NotNull(start);
        var tiles = new List<GridPos>();
        for (int x = 0; x <= 6; x++)
        {
            tiles.Add(new GridPos(start!.Value.X + x, start.Value.Y));
        }

        Assert.All(tiles, t => Assert.Equal(TravelCostField.BaseTileCost, world.TravelCost.CostToEnter(t)));
        (int onFoot, int grassOnFoot) = BehaviorSystem.WalkedOver(world, tiles, tiles.Count - 1);
        (int pushed, int grassPushed) = BehaviorSystem.WalkedOver(world, tiles, tiles.Count - 1, pushing: true);
        int slow = (TravelCostField.BaseTileCost * config.CartOffPathSlowPercent / 100) - TravelCostField.BaseTileCost;
        _output.WriteLine($"{tiles.Count} grass tiles: {onFoot} on foot, {pushed} under a cart ({slow} more a tile); grass counted {grassOnFoot} / {grassPushed}.");
        Assert.Equal(tiles.Count, grassOnFoot);
        Assert.Equal(grassOnFoot, grassPushed);
        Assert.Equal(onFoot + (tiles.Count * slow), pushed);

        // The same tiles packed: the cart pays what the walker pays.
        foreach (GridPos t in tiles)
        {
            world.Paths.Tread(t, config.PathPackedAt + 5);
        }

        world.Paths.Decay(0);
        Assert.All(tiles, t => Assert.True(world.TravelCost.CostToEnter(t) < TravelCostField.BaseTileCost, "the lane did not price as a path"));
        (int onFootPacked, _) = BehaviorSystem.WalkedOver(world, tiles, tiles.Count - 1);
        (int pushedPacked, _) = BehaviorSystem.WalkedOver(world, tiles, tiles.Count - 1, pushing: true);
        Assert.Equal(onFootPacked, pushedPacked);
        Assert.True(pushedPacked < pushed, "the packed lane is not quicker for the cart than the grass was");
    }

    /// <summary>
    /// §7 (D587 call 8): one pass by a cart treads every tile its wheels cross to `path_worn_at`; a walker's pass leaves a
    /// footstep; a tile already past the line takes the footstep, never a reset.
    /// </summary>
    [Fact]
    public void ACartsPassMakesAPathWhereAWalkersLeavesAFootstep()
    {
        SimConfig config = Config;
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;
        const int down = 8;
        GridPos? start = ClearStretch(world, across: 1, down: down);
        Assert.NotNull(start);
        GridPos from = start!.Value;
        var to = new GridPos(from.X, from.Y + down);
        Villager walker = world.Villagers.First(v => v.Alive && v.CanWork);

        // On foot first, over fresh grass: footsteps, under the line.
        walker.PushingACart = false;
        int stepsOnFoot = Walk(world, walker, from, to);
        var afterFoot = Trodden(world, from, to);
        Assert.True(afterFoot.Count > 0, "the walk trod nothing");
        Assert.All(afterFoot.Values, wear => Assert.True(wear < config.PathWornAt, $"a walker's pass wore a tile to {wear}, a path"));

        // One tile past the line already, then the cart's pass.
        GridPos already = new(from.X, from.Y + (down / 2));
        world.Paths.Tread(already, config.PathWornAt + 20 - world.Paths.At(already));
        int alreadyBefore = world.Paths.At(already);
        walker.PushingACart = true;
        int stepsUnderTheCart = Walk(world, walker, from, to);
        var afterCart = Trodden(world, from, to);
        _output.WriteLine($"on foot: {afterFoot.Count} tiles at {afterFoot.Values.Min()}–{afterFoot.Values.Max()} in {stepsOnFoot} ticks; under the cart: {afterCart.Count} tiles at {afterCart.Values.Min()}–{afterCart.Values.Max()} in {stepsUnderTheCart} ticks (the line is {config.PathWornAt}).");
        Assert.All(afterCart.Values, wear => Assert.True(wear >= config.PathWornAt, $"a cart's pass left a tile at {wear}, under the line"));
        Assert.Equal(alreadyBefore + config.PathWearPerStep, world.Paths.At(already));

        // §6, lived: the same grass took the cart longer — the leg's ticks carry the slow (the walker's footsteps left the
        // tiles grass by class, so both walks were priced as grass).
        Assert.True(stepsUnderTheCart > stepsOnFoot, $"the cart crossed {down} grass tiles in {stepsUnderTheCart} ticks against {stepsOnFoot} on foot — no slower");
    }

    private static int Walk(SimWorld world, Villager walker, GridPos from, GridPos to)
    {
        walker.StandAt(Point.CentreOf(from));
        walker.LegTicks = Fixed.Zero;
        int steps = 0;
        while (steps < 100 && !(walker.Tile == to && walker.LegTicks == Fixed.Zero))
        {
            BehaviorSystem.TravelForTest(world, walker, to);
            steps++;
        }

        Assert.Equal(to, walker.Tile);
        return steps;
    }

    /// <summary>Every tile strictly between the two ends of a straight walk down a column, with its wear.</summary>
    private static Dictionary<GridPos, int> Trodden(SimWorld world, GridPos from, GridPos to)
    {
        var trodden = new Dictionary<GridPos, int>();
        for (int y = from.Y + 1; y < to.Y; y++)
        {
            var t = new GridPos(from.X, y);
            trodden[t] = world.Paths.At(t);
        }

        return trodden;
    }

    /// <summary>Open, unbuilt, unwalled ground near the founding — `DesirePathTests.ClearStretch`'s shape.</summary>
    private static GridPos? ClearStretch(SimWorld world, int across, int down)
    {
        bool Open(GridPos t) =>
            world.Map.Contains(t) && TerrainRules.IsPassable(world.Map.TerrainAt(t))
            && world.FootprintCovering(t).Count == 0 && world.Zones.WallsOn(t) == 0
            && world.TravelCost.CostToEnter(t) == TravelCostField.BaseTileCost;
        for (int r = 0; r < 20; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    var a = new GridPos(world.Map.FoundingSite.X + dx, world.Map.FoundingSite.Y + dy);
                    bool clear = true;
                    for (int y = -1; y <= down + 1 && clear; y++)
                    {
                        for (int x = -1; x <= across + 1 && clear; x++)
                        {
                            clear = Open(new GridPos(a.X + x, a.Y + y));
                        }
                    }

                    if (clear)
                    {
                        return a;
                    }
                }
            }
        }

        return null;
    }

    // ---------------------------------------------------------------
    //  §9 — hashed, saved, said
    // ---------------------------------------------------------------

    [Fact]
    public void TheHashCoversTheCartAndThePusher()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        ulong bare = StateHash.Compute(world);

        world.Households[0].HandcartUses = 5;
        ulong carted = StateHash.Compute(world);
        Assert.NotEqual(bare, carted);

        world.Villagers[0].PushingACart = true;
        Assert.NotEqual(carted, StateHash.Compute(world));
    }

    [Fact]
    public void ACartAndItsPusherSurviveTheRoundTripAndAFormatTenSaveIsRefused()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        Household home = Housed(world)[0];
        home.HandcartUses = 123;
        Villager pusher = world.Villagers.First(v => v.Alive && v.HouseholdId == home.Id);
        pusher.PushingACart = true;
        TheWarehouse(world).Store.Add(Goods.Handcart, 2);

        Assert.Equal(12, SaveGame.Format); // 11 at D686; 12 since the trades' carts (§13.7), whose own guard poses the refusal of 11.
        SimLoop loaded = Reload(loop);
        Assert.Equal(StateHash.Compute(world), StateHash.Compute(loaded.World));
        Assert.Equal(123, loaded.World.Households.Single(h => h.Id == home.Id).HandcartUses);
        Assert.True(loaded.World.Villagers.Single(v => v.Id == pusher.Id).PushingACart);

        loop.Step(config.TicksPerYear);
        loaded.Step(config.TicksPerYear);
        Assert.Equal(StateHash.Compute(world), StateHash.Compute(loaded.World));

        // Joe's format-10 saves (the Slice W builds) — refused, naming the build.
        string folder = Path.Combine(Path.GetTempPath(), "bclone-handcart-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            JsonObject document = SaveGame.Capture(loop, "0.0.1", "2026-11-04 12:00", NewGame.ShareCode(
                config, NewGame.Defaults(config, world.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture))));
            document["format"] = 10;
            string path = Path.Combine(folder, $"format-ten{SaveFile.Extension}");
            SaveFile.Write(path, document);
            SaveOpened opened = SaveFile.Open(path, config);
            Assert.Null(opened.Loop);
            Assert.Equal("Saved by build 0.0.1, whose saves this build cannot read.", opened.Refusal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>§8: the two states and the pushing flag read as sentences in both registers, and neither is work.</summary>
    [Fact]
    public void TheCartStatesHaveSentences()
    {
        var villager = new Villager
        {
            Id = 1, Name = "Posed", LifespanYears = 45, Carried = new Stockpile(Stockpile.Kinds),
        };

        villager.State = VillagerState.FetchingAHandcart;
        Assert.Equal("fetching a handcart from a store", villager.DescribeState());
        Assert.Equal("fetching a handcart from a store", BehaviorSystem.Describe(VillagerState.FetchingAHandcart));
        villager.State = VillagerState.PushingAHandcart;
        Assert.Equal("pushing the new handcart home", villager.DescribeState());
        Assert.Equal("pushing a handcart home", BehaviorSystem.Describe(VillagerState.PushingAHandcart));

        villager.PushingACart = true;
        villager.State = VillagerState.FetchingFromStore;
        Assert.Equal("pushing the cart to the shops", villager.DescribeState());
        villager.State = VillagerState.TravelingHome;
        Assert.Equal("pushing the cart home", villager.DescribeState());
        villager.Carried.Add(Goods.Produce, 10);
        Assert.Equal("pushing a load home", villager.DescribeState());
        villager.PushingACart = false;
        Assert.Equal("walking home", villager.DescribeState());

        Assert.False(SkillSystem.OutOnTheWork(VillagerState.FetchingAHandcart));
        Assert.False(SkillSystem.OutOnTheWork(VillagerState.PushingAHandcart));
    }

    /// <summary>The knob as shipped, its C# default agreeing, and a cart of no trips refused at load.</summary>
    [Fact]
    public void TheCartKnobsAreAsShipped()
    {
        SimConfig shipped = ShippedConfig.Load();
        Assert.Equal(150, shipped.HandcartUses);
        Assert.Equal(new SimConfig().HandcartUses, shipped.HandcartUses);

        SimConfigException refused = Assert.Throws<SimConfigException>(() => SimConfigLoader.Parse("""{ "handcart_uses": 0 }""", "<modded>"));
        _output.WriteLine(refused.Message);
        Assert.Contains("handcart_uses", refused.Message, StringComparison.Ordinal);

        Assert.Equal(3, shipped.CartArmfuls);
        Assert.Equal(new SimConfig().CartArmfuls, shipped.CartArmfuls);
        SimConfigException noArmful = Assert.Throws<SimConfigException>(() => SimConfigLoader.Parse("""{ "cart_armfuls": 0 }""", "<modded>"));
        Assert.Contains("cart_armfuls", noArmful.Message, StringComparison.Ordinal);

        Assert.Equal(150, shipped.CartOffPathSlowPercent);
        Assert.Equal(new SimConfig().CartOffPathSlowPercent, shipped.CartOffPathSlowPercent);
        SimConfigException quicker = Assert.Throws<SimConfigException>(() => SimConfigLoader.Parse("""{ "cart_off_path_slow_percent": 99 }""", "<modded>"));
        Assert.Contains("cart_off_path_slow_percent", quicker.Message, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------
    //  §13 — the trades' carts: the market's and the trading post's (Joe, 2026-11-05: "one more owner of the same good")
    // ---------------------------------------------------------------

    /// <summary>The fixture's one standing market, as a workplace — the owner of its cart (§13.2).</summary>
    private static Workplace TheMarket(SimWorld world) => world.Workplaces.Single(w => w.Kind == JobKind.Marketer && !w.IsSite);

    /// <summary>Every housed household carted, so the only owner wanting a cart is the trade under test.</summary>
    private static void CartEveryHousehold(SimWorld world)
    {
        foreach (Household home in Housed(world))
        {
            home.HandcartUses = world.Config.HandcartUses;
        }
    }

    /// <summary>A marketer, seated by pinning (the quota keeps a pinned trade seated, as TradeTests pins its merchant).</summary>
    private static Villager PinAMarketer(SimWorld world, int skip = 0)
    {
        Villager marketer = world.Villagers.Where(v => v.Alive && v.CanWork).Skip(skip).First();
        world.SetPinnedTrade(marketer, JobKind.Marketer);
        return marketer;
    }

    /// <summary>§13.2: a standing market with no cart sends a free marketer for one — from a store that racks tools, never the market's own counter — who pushes it to the market and parks it there with every trip in it; no household's cart moves.</summary>
    [Fact]
    public void AMarketWithoutACartSendsItsMarketerForOneAndParksItThere()
    {
        SimConfig config = Config;
        var sink = new InMemoryLogSink();
        SimLoop loop = SimFactory.CreatePhase0(config, sink);
        SimWorld world = loop.World;
        ToolsTests.KeepTheVillageWarmAndFed(world);
        NobodyWorks(world);
        CartEveryHousehold(world);
        Workplace market = TheMarket(world);
        StoreBuilding counter = world.AnyStoreOf(StoreKind.Market);
        Villager marketer = PinAMarketer(world);
        foreach (Villager v in world.Villagers.Where(v => v.Alive))
        {
            v.StandAt(world.HomePoint(v));
        }

        // Two carts straight into the market's own stockpile, past the row: nobody's to fetch (§13.2, D672 call 1).
        counter.Store.Add(Goods.Handcart, 2);
        for (int tick = 0; tick < config.TicksPerSeason / 2; tick++)
        {
            loop.StepOnce();
            Assert.DoesNotContain(world.Villagers, v => v.State == VillagerState.FetchingAHandcart);
        }

        Assert.Equal(0, market.HandcartUses);
        Assert.Equal(2, counter.Store.TakeAll(Goods.Handcart));

        // Two on the warehouse shelf: the marketer walks for one and pushes it to the market.
        TheWarehouse(world).Store.Add(Goods.Handcart, 2);
        int fetchingTicks = 0;
        int pushingTicks = 0;
        for (int tick = 0; tick < config.TicksPerSeason; tick++)
        {
            loop.StepOnce();
            foreach (Villager v in world.Villagers)
            {
                if (v.State is VillagerState.FetchingAHandcart or VillagerState.PushingAHandcart)
                {
                    Assert.True(v.CartForTheWorks, $"{v.Name} is out for a cart that is not the works' while every household is carted.");
                    Assert.Equal(JobKind.Marketer, world.FindWorkplace(v.WorkplaceId)?.Kind);
                }

                fetchingTicks += v.State == VillagerState.FetchingAHandcart ? 1 : 0;
                pushingTicks += v.State == VillagerState.PushingAHandcart ? 1 : 0;
            }
        }

        _output.WriteLine($"{marketer.Name}: {fetchingTicks} ticks walking for the market's cart, {pushingTicks} pushing it to the market; the market has {market.HandcartUses} trips.");
        Assert.True(fetchingTicks > 0, "Nobody was seen walking for the market's cart.");
        Assert.True(pushingTicks > 0, "Nobody was seen pushing the market's cart to the market.");
        // Filled to `handcart_uses` at the shelf; the marketer works the counter with it once it is parked (§13.3), so a trip or two wear.
        Assert.InRange(market.HandcartUses, config.HandcartUses - 5, config.HandcartUses);
        Assert.Equal(1, TheWarehouse(world).Store[Goods.Handcart]);
        Assert.Equal(1, sink.Entries.Count(e => e.Message.Contains($"for {market.Name}", StringComparison.Ordinal) && e.Message.Contains("took a handcart", StringComparison.Ordinal)));
        // The fetcher parks it at the market and works the counter with it; with nothing to move they keep it for the night (§13.2b),
        // so a holder is the market's own worker and nobody else.
        Assert.All(world.Villagers, v => Assert.True(!v.CartForTheWorks || (v.PushingACart && world.FindWorkplace(v.WorkplaceId)?.Kind == JobKind.Marketer),
            $"{v.Name} holds the market's cart without a seat at the market."));
        Assert.All(Housed(world), h => Assert.True(h.HandcartUses > 0, $"{h.Name} lost its own cart while the market fetched one."));
    }

    /// <summary>§13.2, posed directly: one fetcher a market while a colleague is out for its cart, none once the market has one, and never a marketer with a load in hand.</summary>
    [Fact]
    public void OneFetcherAMarketAndNoneOnceCarted()
    {
        SimConfig config = Config;
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;
        ToolsTests.KeepTheVillageWarmAndFed(world);
        NobodyWorks(world);
        CartEveryHousehold(world);
        Workplace market = TheMarket(world);
        TheWarehouse(world).Store.Add(Goods.Handcart, 5);
        Villager first = PinAMarketer(world);
        Villager second = PinAMarketer(world, skip: 1);
        foreach (Villager v in world.Villagers)
        {
            v.WorkplaceId = 0;
            v.StandAt(world.HomePoint(v));
        }

        first.WorkplaceId = market.Id;
        second.WorkplaceId = market.Id;
        market.WorkerIds.Add(first.Id);
        market.WorkerIds.Add(second.Id);

        // A colleague is already walking for it: the second stays.
        first.State = VillagerState.FetchingAHandcart;
        first.CartForTheWorks = true;
        BehaviorSystem.DecideForTest(world, second);
        Assert.NotEqual(VillagerState.FetchingAHandcart, second.State);

        // Nobody out, no cart: the second goes, for the works.
        first.State = VillagerState.Idle;
        first.CartForTheWorks = false;
        second.State = VillagerState.Idle;
        BehaviorSystem.DecideForTest(world, second);
        Assert.Equal(VillagerState.FetchingAHandcart, second.State);
        Assert.True(second.CartForTheWorks);

        // A carted market, carts still on the shelf: nobody walks for another.
        second.State = VillagerState.Idle;
        second.CartForTheWorks = false;
        second.StandAt(world.HomePoint(second));
        market.HandcartUses = 3;
        BehaviorSystem.DecideForTest(world, first);
        Assert.NotEqual(VillagerState.FetchingAHandcart, first.State);
        Assert.Equal(5, TheWarehouse(world).Store[Goods.Handcart]);
    }

    /// <summary>§13.2: a standing trading post is an owner too — its merchant fetches one and parks it at the post.</summary>
    [Fact]
    public void APostWithoutACartSendsItsMerchantForOne()
    {
        SimLoop loop = SimFactory.CreatePhase0(ShippedConfig.Established(), new InMemoryLogSink());
        SimWorld world = loop.World;
        SimConfig config = world.Config;
        ToolsTests.KeepTheVillageWarmAndFed(world);
        NobodyWorks(world);
        CartEveryHousehold(world);
        Workplace post = TradeTests.RaiseAPost(world);
        TheWarehouse(world).Store.Add(Goods.Handcart, 2);
        Villager merchant = world.Villagers.First(v => v.Alive && v.CanWork);
        world.SetPinnedTrade(merchant, JobKind.Merchant);
        foreach (Villager v in world.Villagers.Where(v => v.Alive))
        {
            v.StandAt(world.HomePoint(v));
        }

        int pushingTicks = 0;
        for (int tick = 0; tick < config.TicksPerSeason; tick++)
        {
            loop.StepOnce();
            pushingTicks += world.Villagers.Count(v => v.State == VillagerState.PushingAHandcart && v.CartForTheWorks);
        }

        _output.WriteLine($"{merchant.Name}: {pushingTicks} ticks pushing the post's cart to the post; the post has {post.HandcartUses} trips.");
        Assert.True(pushingTicks > 0, "Nobody was seen pushing the post's cart to the post.");
        Assert.Equal(config.HandcartUses, post.HandcartUses);
        Assert.Equal(1, TheWarehouse(world).Store[Goods.Handcart]);
        Assert.Equal(0, TheMarket(world).HandcartUses); // nobody is seated at the market, so it waits
        // Parked at the post by the fetcher, then — nothing to carry — taken home for the night (§13.2b): a holder is the merchant.
        Assert.All(world.Villagers, v => Assert.True(!v.CartForTheWorks || (v.PushingACart && world.FindWorkplace(v.WorkplaceId)?.Kind == JobKind.Merchant),
            $"{v.Name} holds the post's cart without a seat at the post."));
    }

    /// <summary>§13.2: the quota counts a standing market and a standing post without a cart as an owner each — a site is not an owner, and a carted one wants none.</summary>
    [Fact]
    public void TheQuotaCountsAStandingMarketAndPostAsOwners()
    {
        SimWorld world = SimFactory.CreatePhase0(ShippedConfig.Established(), new InMemoryLogSink()).World;
        Workplace market = TheMarket(world);
        int households = Housed(world).Count;
        Assert.Equal(households + 1, LabourQuota.HandcartsWanted(world));

        market.HandcartUses = 5;
        Assert.Equal(households, LabourQuota.HandcartsWanted(world));

        // A post raised: one more owner; a site under construction is not one.
        GridPos founding = world.Map.FoundingSite;
        ColdStartTests.MarkSomewhereNear(world, BuildingKind.Market, founding, 6);
        Assert.Contains(world.Workplaces, w => w.IsSite && w.Construction!.Kind == BuildingKind.Market);
        Assert.Equal(households, LabourQuota.HandcartsWanted(world));
        Workplace post = TradeTests.RaiseAPost(world);
        Assert.Equal(households + 1, LabourQuota.HandcartsWanted(world));
        post.HandcartUses = 1;
        Assert.Equal(households, LabourQuota.HandcartsWanted(world));
        Assert.Equal(LabourQuota.HandcartsWanted(world) + LabourQuota.HandcartSpare - world.InStores(Goods.Handcart), LabourQuota.HandcartsShortfall(world));
    }

    /// <summary>§13.2: a pusher who dies, or whose seat the allocator took, leaves the works' cart at its building — the flags clear, the building's trips stand.</summary>
    [Fact]
    public void APusherWhoLosesTheSeatOrDiesLeavesTheWorksCartAtTheBuilding()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        ToolsTests.KeepTheVillageWarmAndFed(world);
        NobodyWorks(world);
        CartEveryHousehold(world);
        Workplace market = TheMarket(world);
        market.HandcartUses = 17;
        Villager marketer = PinAMarketer(world);
        marketer.WorkplaceId = market.Id;
        marketer.PushingACart = true;
        marketer.CartForTheWorks = true;
        marketer.StandAt(world.AnyStoreOf(StoreKind.Granary).Position);

        // The seat gone: the next decision drops the cart where it counts as home, and the household's cart is not touched.
        world.SetPinnedTrade(marketer, null);
        marketer.WorkplaceId = 0;
        Household home = world.HouseholdOf(marketer);
        BehaviorSystem.DecideForTest(world, marketer);
        Assert.False(marketer.CartForTheWorks);
        Assert.False(marketer.PushingACart);
        Assert.Equal(17, market.HandcartUses);
        Assert.Equal(config.HandcartUses, home.HandcartUses);

        // A death on the road with the works' cart in hand.
        marketer.WorkplaceId = market.Id;
        marketer.PushingACart = true;
        marketer.CartForTheWorks = true;
        marketer.AgeYears = marketer.LifespanYears;
        loop.StepOnce();
        Assert.False(marketer.Alive, "The posed death did not happen.");
        Assert.False(marketer.CartForTheWorks);
        Assert.False(marketer.PushingACart);
        Assert.Equal(17, market.HandcartUses);
    }

    [Fact]
    public void TheHashCoversTheWorksCartAndItsPusher()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        ulong bare = StateHash.Compute(world);

        TheMarket(world).HandcartUses = 5;
        ulong carted = StateHash.Compute(world);
        Assert.NotEqual(bare, carted);

        world.Villagers[0].PushingACart = true;
        ulong pushing = StateHash.Compute(world);
        world.Villagers[0].CartForTheWorks = true;
        Assert.NotEqual(pushing, StateHash.Compute(world));
    }

    [Fact]
    public void TheWorksCartSurvivesTheRoundTripAndAFormatElevenSaveIsRefused()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        Workplace market = TheMarket(world);
        market.HandcartUses = 77;
        Villager pusher = world.Villagers.First(v => v.Alive && v.CanWork);
        pusher.PushingACart = true;
        pusher.CartForTheWorks = true;

        Assert.Equal(12, SaveGame.Format);
        SimLoop loaded = Reload(loop);
        Assert.Equal(StateHash.Compute(world), StateHash.Compute(loaded.World));
        Assert.Equal(77, loaded.World.Workplaces.Single(w => w.Id == market.Id).HandcartUses);
        Assert.True(loaded.World.Villagers.Single(v => v.Id == pusher.Id).CartForTheWorks);

        loop.Step(config.TicksPerYear);
        loaded.Step(config.TicksPerYear);
        Assert.Equal(StateHash.Compute(world), StateHash.Compute(loaded.World));

        // Joe's format-11 saves (the Slice H builds) — refused, naming the build.
        string folder = Path.Combine(Path.GetTempPath(), "bclone-handcart-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            JsonObject document = SaveGame.Capture(loop, "0.0.1", "2026-11-05 12:00", NewGame.ShareCode(
                config, NewGame.Defaults(config, world.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture))));
            document["format"] = 11;
            string path = Path.Combine(folder, $"format-eleven{SaveFile.Extension}");
            SaveFile.Write(path, document);
            SaveOpened opened = SaveFile.Open(path, config);
            Assert.Null(opened.Loop);
            Assert.Equal("Saved by build 0.0.1, whose saves this build cannot read.", opened.Refusal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>§13.5: the works' cart's walks read as sentences — the new cart's walk in, and the empty leg back.</summary>
    [Fact]
    public void TheWorksCartStatesHaveSentences()
    {
        var villager = new Villager
        {
            Id = 1, Name = "Posed", LifespanYears = 45, Carried = new Stockpile(Stockpile.Kinds),
        };

        villager.CartForTheWorks = true;
        villager.PushingACart = true;
        villager.State = VillagerState.PushingAHandcart;
        Assert.Equal("pushing the new handcart to work", villager.DescribeState());
        villager.State = VillagerState.StockingTheMarket;
        Assert.Equal("pushing the market's handcart back", villager.DescribeState());
        villager.State = VillagerState.StockingThePost;
        Assert.Equal("pushing the post's handcart back", villager.DescribeState());

        // With a load in the arms the leg keeps its words; the rectangle behind the dot says the rest.
        villager.Carried.Add(Goods.Produce, 10);
        villager.State = VillagerState.StockingTheMarket;
        Assert.Equal("carrying goods to the market", villager.DescribeState());

        villager.CartForTheWorks = false;
        villager.Carried.TakeAll(Goods.Produce);
        villager.State = VillagerState.PushingAHandcart;
        Assert.Equal("pushing the new handcart home", villager.DescribeState());

        // The first leg (§13.2a): empty-handed, no cart yet — the sentence says what the walk is for.
        villager.State = VillagerState.WalkingToTheMarket;
        Assert.Equal("walking to the market for its handcart", villager.DescribeState());

        // The night (§13.2b): the works' cart pushed home with empty arms; the household's cart keeps its words.
        villager.CartForTheWorks = true;
        villager.PushingACart = true;
        villager.State = VillagerState.TravelingHome;
        Assert.Equal("pushing the handcart home for the night", villager.DescribeState());
        villager.CartForTheWorks = false;
        Assert.Equal("pushing the cart home", villager.DescribeState());
    }

    // ---------------------------------------------------------------
    //  §13.2–§13.3 — the works' cart taken at the door, three armfuls a trip, parked and worn, brought back
    // ---------------------------------------------------------------

    /// <summary>A market short of food beside a stocked granary, every household carted and fed, one pinned marketer stood at the market.</summary>
    private static (SimLoop loop, SimWorld world, Workplace market, StoreBuilding counter, Villager marketer) AMarketErrandPose(SimConfig config, int cartUses)
    {
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        ToolsTests.KeepTheVillageWarmAndFed(world);
        NobodyWorks(world);
        CartEveryHousehold(world);
        Workplace market = TheMarket(world);
        StoreBuilding counter = world.AnyStoreOf(StoreKind.Market);
        counter.Store.TakeAll(Goods.Produce);
        world.AnyStoreOf(StoreKind.Granary).Store.Receive(Goods.Produce, 3000);
        market.HandcartUses = cartUses;
        Villager marketer = PinAMarketer(world);
        foreach (Villager v in world.Villagers.Where(v => v.Alive))
        {
            v.StandAt(world.HomePoint(v));
        }

        marketer.WorkplaceId = market.Id;
        market.WorkerIds.Add(marketer.Id);
        marketer.StandAt(market.Position);
        return (loop, world, market, counter, marketer);
    }

    /// <summary>
    /// §13.2–§13.3: a marketer setting out from the market takes its cart, brings `cart_armfuls` armfuls to the counter in one
    /// trip, and parks it at the market on arrival — worn one trip, the flags clear, no household's cart touched.
    /// </summary>
    [Fact]
    public void TheMarketerTakesTheMarketsCartAndBringsThreeArmfulsToTheCounter()
    {
        SimConfig config = Config;
        (SimLoop loop, SimWorld world, Workplace market, StoreBuilding counter, Villager marketer) = AMarketErrandPose(config, config.HandcartUses);
        int cartLoad = config.CarryCapacity * config.CartArmfuls;
        Assert.True(counter.RoomFor(Goods.Produce) >= cartLoad, "the counter has no room for a cart's load, so the pose could show nothing");

        int setOutWithTheCart = 0;
        int biggestLoad = 0;
        int parked = 0;
        bool wasPushing = false;
        for (int tick = 0; tick < config.TicksPerSeason && parked == 0; tick++)
        {
            loop.StepOnce();
            if (marketer.PushingACart)
            {
                Assert.True(marketer.CartForTheWorks, "the marketer pushes a cart that is not the works'");
                if (!wasPushing)
                {
                    setOutWithTheCart++;
                }

                biggestLoad = Math.Max(biggestLoad, marketer.Carried.Held);
            }
            else if (wasPushing)
            {
                parked++;
                Assert.Equal(market.Tile, marketer.Tile);
                Assert.False(marketer.CartForTheWorks);
            }

            wasPushing = marketer.PushingACart;
        }

        _output.WriteLine($"{marketer.Name} set out with the market's cart {setOutWithTheCart} time(s), carried at most {biggestLoad} (an armful is {config.CarryCapacity}), parked {parked}; the market has {market.HandcartUses} trips left.");
        Assert.True(setOutWithTheCart > 0, "The marketer never set out with the market's cart.");
        Assert.Equal(cartLoad, biggestLoad);
        Assert.Equal(1, parked);
        Assert.Equal(config.HandcartUses - 1, market.HandcartUses);
        Assert.Equal(cartLoad, counter.Store[Goods.Produce]);
        Assert.All(Housed(world), h => Assert.True(h.HandcartUses > 0));
    }

    /// <summary>
    /// §13.2, posed directly: one pusher a market — a colleague setting out while the cart is out goes on foot, straight to the
    /// source; and a marketer deciding away from the market takes no cart from there — ⭐ since §13.2a (Joe, 2026-11-06) they
    /// walk to the market for it first, the cart still at the door until they stand there.
    /// </summary>
    [Fact]
    public void OnePusherAMarketAndNoCartFromAwayFromTheMarket()
    {
        SimConfig config = Config;
        (_, SimWorld world, Workplace market, _, Villager first) = AMarketErrandPose(config, config.HandcartUses);
        Villager second = PinAMarketer(world, skip: 1);
        second.WorkplaceId = market.Id;
        market.WorkerIds.Add(second.Id);
        StoreBuilding granary = world.AnyStoreOf(StoreKind.Granary);

        // The first is out with it; the second decides at the market.
        first.PushingACart = true;
        first.CartForTheWorks = true;
        first.State = VillagerState.CollectingForMarket;
        first.StandAt(granary.Position);
        second.StandAt(market.Position);
        BehaviorSystem.DecideForTest(world, second);
        Assert.Contains(second.State, new[] { VillagerState.CollectingForMarket, VillagerState.StockingTheMarket });
        Assert.False(second.PushingACart, "two marketers set off pushing the one cart");
        Assert.False(second.CartForTheWorks);

        // Nobody out, but the second decides at the granary, not at the market: the first leg (§13.2a) — on foot to the
        // market's door, nothing in the arms, the cart still parked there until they stand on its tile.
        first.PushingACart = false;
        first.CartForTheWorks = false;
        first.State = VillagerState.Idle;
        second.State = VillagerState.Idle;
        second.StandAt(granary.Position);
        BehaviorSystem.DecideForTest(world, second);
        Assert.Equal(VillagerState.WalkingToTheMarket, second.State);
        Assert.False(second.PushingACart, "a marketer who was nowhere near the market set off pushing its cart");
        Assert.False(second.CartForTheWorks);
        Assert.Equal(config.HandcartUses, market.HandcartUses);
        Assert.Equal(0, second.Carried.Held);
    }

    /// <summary>
    /// ⭐ §13.2a (Joe, 2026-11-06: *"have the marketer's day begin with a walk to the market so the cart gets used on nearly
    /// every trip"*): a marketer deciding at home, the market short and its cart parked at the door, walks to the market first
    /// with nothing in hand, and the trip that follows is the cart's — three armfuls on the counter in one go.
    /// </summary>
    [Fact]
    public void AMarketerDecidingAtHomeWalksToTheMarketForItsCartFirst()
    {
        SimConfig config = Config;
        (SimLoop loop, SimWorld world, Workplace market, StoreBuilding counter, Villager marketer) = AMarketErrandPose(config, config.HandcartUses);
        int cartLoad = config.CarryCapacity * config.CartArmfuls;
        marketer.StandAt(world.HomePoint(marketer));
        Assert.NotEqual(market.Tile, marketer.Tile);

        BehaviorSystem.DecideForTest(world, marketer);
        Assert.Equal(VillagerState.WalkingToTheMarket, marketer.State);
        Assert.False(marketer.PushingACart);
        Assert.Equal(0, marketer.Carried.Held);

        int atTheDoor = -1;
        int setOutWithTheCart = -1;
        int stocked = -1;
        for (int tick = 0; tick < config.TicksPerSeason && stocked < 0; tick++)
        {
            loop.StepOnce();
            if (atTheDoor < 0 && marketer.Tile == market.Tile)
            {
                atTheDoor = tick;
            }

            if (setOutWithTheCart < 0 && marketer.PushingACart)
            {
                setOutWithTheCart = tick;
                Assert.True(atTheDoor >= 0 && atTheDoor <= tick, "the cart was taken before the marketer reached the door");
                Assert.True(marketer.CartForTheWorks);
                Assert.Equal(VillagerState.CollectingForMarket, marketer.State);
            }

            if (counter.Store[Goods.Produce] > 0)
            {
                stocked = tick;
            }
        }

        _output.WriteLine($"{marketer.Name}: at the door on tick {atTheDoor}, out with the cart on tick {setOutWithTheCart}, the counter stocked on tick {stocked} with {counter.Store[Goods.Produce]}.");
        Assert.True(setOutWithTheCart >= 0, "the marketer never set out with the market's cart");
        Assert.True(stocked >= 0, "the counter was never stocked");

        // The cart's load, less what a hungry marketer ate of it on the longer walk (D10) — more than two armfuls, which no
        // walker ever puts down at once.
        Assert.True(counter.Store[Goods.Produce] > 2 * config.CarryCapacity && counter.Store[Goods.Produce] <= cartLoad,
            $"the first trip put {counter.Store[Goods.Produce]} on the counter; a cart's load is {cartLoad}");
    }

    /// <summary>
    /// §13.2a's two conditions, each alone: no cart at the market and the errand starts from home on foot, as before the leg
    /// existed; a cart but nothing to move and nobody walks to the market for it.
    /// </summary>
    [Fact]
    public void TheFirstLegIsWalkedOnlyForACartAndAnErrand()
    {
        SimConfig config = Config;

        // No cart: straight to the granary, as always.
        (_, SimWorld world, Workplace market, _, Villager marketer) = AMarketErrandPose(config, cartUses: 0);
        marketer.StandAt(world.HomePoint(marketer));
        BehaviorSystem.DecideForTest(world, marketer);
        Assert.Contains(marketer.State, new[] { VillagerState.CollectingForMarket, VillagerState.StockingTheMarket });
        Assert.False(marketer.PushingACart);

        // A cart but nothing to move — the counter full of food and every other store bare, so no restock and no haul: no walk
        // to the market for the cart.
        (_, world, market, StoreBuilding counter, marketer) = AMarketErrandPose(config, config.HandcartUses);
        counter.Store.Receive(Goods.Produce, counter.RoomFor(Goods.Produce));
        foreach (StoreBuilding store in world.StoreBuildings.Where(store => store != counter))
        {
            for (int g = 0; g < world.GoodsCatalog.Count; g++)
            {
                store.Store.TakeAll((Goods)g);
            }
        }

        marketer.StandAt(world.HomePoint(marketer));
        BehaviorSystem.DecideForTest(world, marketer);
        Assert.NotEqual(VillagerState.WalkingToTheMarket, marketer.State);
        Assert.NotEqual(VillagerState.CollectingForMarket, marketer.State);
        Assert.Equal(config.HandcartUses, market.HandcartUses);
    }

    /// <summary>
    /// §13.2b: a marketer who hauls a buffer to the granary with the market's cart and then has nothing left to carry goes
    /// <b>home</b> with it for the night (*"pushing the handcart home for the night"*) — never D693's empty leg back — rests
    /// at home with the cart unworn, and the household's own cart is untouched.
    /// </summary>
    [Fact]
    public void TheMarketsCartGoesHomeForTheNightAfterAHaul()
    {
        SimConfig config = Config;
        (SimLoop loop, SimWorld world, Workplace market, StoreBuilding counter, Villager marketer) = AMarketErrandPose(config, config.HandcartUses);

        // Nothing for the counter (every limit nought), and a farm brimming with food — the buffer leg is the only errand.
        for (int g = 0; g < world.GoodsCatalog.Count; g++)
        {
            var goods = (Goods)g;
            if (counter.Accepts(goods))
            {
                world.SetMarketLimit(counter, goods, 0);
            }
        }

        // A granary with room — a buffer is worth clearing only where a store will have the load.
        StoreBuilding granary = world.AnyStoreOf(StoreKind.Granary);
        granary.Store.TakeAll(Goods.Produce);
        granary.Store.Receive(Goods.Produce, 500);
        Workplace farm = FarmFixtures.RaiseAFarm(world);
        farm.Store.Add(Goods.Produce, farm.Store.Capacity);
        Assert.True(world.BufferWorthClearing(farm), $"the farm's buffer is not worth clearing (the granary holds {world.FoodIn(granary.Store)} of {granary.Store.Capacity}), so there is nothing to order");

        Household home = world.HouseholdOf(marketer);
        marketer.Rhythm = 0; // a fresh world's first ticks are a rest in place (D190's stagger)
        int biggestHaul = 0;
        int emptyLegTicks = 0;
        int homeLegTicks = 0;
        int saidHome = 0;
        for (int tick = 0; tick < config.TicksPerSeason * 2 && !world.TheWorksCartSleepsWith(marketer); tick++)
        {
            loop.StepOnce();
            if (marketer.PushingACart && marketer.State == VillagerState.HaulingToStore)
            {
                biggestHaul = Math.Max(biggestHaul, marketer.Carried.Held);
            }

            if (marketer.PushingACart && marketer.State == VillagerState.StockingTheMarket && !marketer.IsCarrying)
            {
                emptyLegTicks++;
            }

            if (marketer.PushingACart && marketer.State == VillagerState.TravelingHome)
            {
                homeLegTicks++;
                saidHome += marketer.DescribeState() == "pushing the handcart home for the night" ? 1 : 0;
            }
        }

        _output.WriteLine($"{marketer.Name} hauled at most {biggestHaul} out of the farm with the cart, pushed it home for {homeLegTicks} ticks, is {marketer.State} at ({marketer.Tile.X},{marketer.Tile.Y}); {market.HandcartUses} trips left.");
        Assert.True(biggestHaul > config.CarryCapacity, $"the haul out of the buffer was {biggestHaul}, an armful or less");
        Assert.Equal(0, emptyLegTicks);
        Assert.True(homeLegTicks > 0, "the cart was never pushed home for the night");
        Assert.True(saidHome > 0, "the walk home never read 'pushing the handcart home for the night'");
        Assert.Equal(VillagerState.Resting, marketer.State);
        Assert.Equal(world.HomePlaceOf(marketer), marketer.Tile);
        Assert.True(world.TheWorksCartSleepsWith(marketer), "the cart did not sleep at the marketer's door");
        Assert.Equal(config.HandcartUses, market.HandcartUses);
        Assert.Equal(config.HandcartUses, home.HandcartUses);
    }

    /// <summary>
    /// §13.2b: at the market's door with nothing to move the marketer takes the cart home for the night — seen and said — and
    /// rests with it at the door, the market's count and the household's own cart untouched; a colleague at the door next
    /// morning finds it out and goes on foot (one pusher a building); a death at home counts it back at the market.
    /// </summary>
    [Fact]
    public void AMarketerWithNothingToMoveAtTheDoorTakesTheCartHomeForTheNight()
    {
        SimConfig config = Config;
        (SimLoop loop, SimWorld world, Workplace market, StoreBuilding counter, Villager marketer) = AMarketErrandPose(config, config.HandcartUses);
        NothingToMove(world, counter);
        Household home = world.HouseholdOf(marketer);
        marketer.Rhythm = 0; // a fresh world's first ticks are a rest in place (D190's stagger); a rest at the door re-decides and parks
        Assert.Equal(market.Tile, marketer.Tile);

        BehaviorSystem.DecideForTest(world, marketer);
        Assert.Equal(VillagerState.TravelingHome, marketer.State);
        Assert.True(marketer.CartForTheWorks && marketer.PushingACart, "the marketer left the market's door without its cart");
        Assert.Equal("pushing the handcart home for the night", marketer.DescribeState());
        Assert.Equal(config.HandcartUses, market.HandcartUses);

        int walked = 0;
        for (; walked < config.TicksPerSeason && !world.TheWorksCartSleepsWith(marketer); walked++)
        {
            loop.StepOnce();
        }

        _output.WriteLine($"{marketer.Name} was home and resting after {walked} ticks with the {market.Name} cart; the household's cart has {home.HandcartUses} trips.");
        Assert.Equal(VillagerState.Resting, marketer.State);
        Assert.Equal(world.HomePlaceOf(marketer), marketer.Tile);
        Assert.True(world.TheWorksCartSleepsWith(marketer), "the cart did not sleep at the marketer's door");
        Assert.Equal(config.HandcartUses, market.HandcartUses);
        Assert.Equal(config.HandcartUses, home.HandcartUses);

        // The morning after, still nothing to move: the holder decides at home and stays — never D693's push back to the market
        // with empty arms, which until D700 ran from this very door (a haul's arrival sends every hauler home before they decide).
        marketer.State = VillagerState.Idle;
        BehaviorSystem.DecideForTest(world, marketer);
        Assert.Equal(VillagerState.Resting, marketer.State);
        Assert.True(world.TheWorksCartSleepsWith(marketer), "the holder left home with the cart though there was nothing to move");
        Assert.Equal(config.HandcartUses, market.HandcartUses);

        // Morning, an errand again: a colleague at the market's door finds the cart out with the holder and goes on foot.
        world.AnyStoreOf(StoreKind.Granary).Store.Receive(Goods.Produce, 3000);
        counter.Store.TakeAll(Goods.Produce);
        Villager colleague = PinAMarketer(world, skip: 1);
        colleague.WorkplaceId = market.Id;
        market.WorkerIds.Add(colleague.Id);
        colleague.StandAt(market.Position);
        BehaviorSystem.DecideForTest(world, colleague);
        Assert.Equal(VillagerState.CollectingForMarket, colleague.State);
        Assert.False(colleague.PushingACart, "the colleague took a cart that was sleeping at the holder's door");

        // A death at home with the cart: it counts as back at the market (§13.2's rule, from a home as from the road).
        marketer.AgeYears = marketer.LifespanYears;
        loop.StepOnce();
        Assert.False(marketer.Alive, "The posed death did not happen.");
        Assert.False(marketer.CartForTheWorks || marketer.PushingACart);
        Assert.Equal(config.HandcartUses, market.HandcartUses);
    }

    /// <summary>
    /// §13.2b: a marketer waking at home with the market's cart sets out from the doorstep carted — no first leg (§13.2a) — and
    /// the counter gets a cart's load; parked at the market on arrival, worn once, as every round trip is.
    /// </summary>
    [Fact]
    public void AMarketerWakingWithTheCartLeavesTheDoorstepCartedWithNoFirstLeg()
    {
        SimConfig config = Config;
        (SimLoop loop, SimWorld world, Workplace market, StoreBuilding counter, Villager marketer) = AMarketErrandPose(config, config.HandcartUses);
        int cartLoad = config.CarryCapacity * config.CartArmfuls;
        marketer.StandAt(world.HomePoint(marketer));
        marketer.PushingACart = true;
        marketer.CartForTheWorks = true;
        marketer.State = VillagerState.Idle;
        marketer.Rhythm = 0; // a fresh world's first ticks are a rest in place (D190's stagger); the pose is the morning after
        Assert.NotEqual(market.Tile, marketer.Tile);

        BehaviorSystem.DecideForTest(world, marketer);
        Assert.Equal(VillagerState.CollectingForMarket, marketer.State);
        Assert.True(marketer.CartForTheWorks && marketer.PushingACart, "the cart was left at the door");

        int stocked = -1;
        int biggestLoad = 0;
        int parks = 0;
        bool wasPushing = true;
        for (int tick = 0; tick < config.TicksPerSeason && stocked < 0; tick++)
        {
            loop.StepOnce();
            Assert.NotEqual(VillagerState.WalkingToTheMarket, marketer.State);
            if (marketer.State == VillagerState.StockingTheMarket)
            {
                biggestLoad = Math.Max(biggestLoad, marketer.Carried.Held);
            }

            // A park at the door: the flags clear on the market's tile (the shoppers may empty the counter the same tick, D372).
            if (wasPushing && !marketer.PushingACart)
            {
                parks++;
                Assert.Equal(market.Tile, marketer.Tile);
            }

            wasPushing = marketer.PushingACart;
            if (parks > 0 && stocked < 0)
            {
                stocked = tick;
            }
        }

        _output.WriteLine($"{marketer.Name}: the first round from home ended at the counter on tick {stocked}, carrying {biggestLoad}; {parks} park(s), {market.HandcartUses} trips left.");
        Assert.True(stocked >= 0, "the marketer never brought a load to the counter");
        Assert.Equal(cartLoad, biggestLoad);
        Assert.Equal(config.HandcartUses - parks, market.HandcartUses);
    }

    /// <summary>
    /// §13.2b for the post: a merchant with nothing to carry takes the post's cart home for the night from its door, and a
    /// merchant away from the post with the cart in hand and nothing to carry goes home with it rather than back.
    /// </summary>
    [Fact]
    public void AMerchantWithNothingToCarryTakesThePostsCartHomeForTheNight()
    {
        SimLoop loop = SimFactory.CreatePhase0(ShippedConfig.Established(), new InMemoryLogSink());
        SimWorld world = loop.World;
        SimConfig config = world.Config;
        ToolsTests.KeepTheVillageWarmAndFed(world);
        NobodyWorks(world);
        CartEveryHousehold(world);
        Workplace post = TradeTests.RaiseAPost(world);
        post.HandcartUses = config.HandcartUses;
        Villager merchant = world.Villagers.First(v => v.Alive && v.CanWork);
        world.SetPinnedTrade(merchant, JobKind.Merchant);
        merchant.WorkplaceId = post.Id;
        post.WorkerIds.Add(merchant.Id);
        foreach (Villager v in world.Villagers.Where(v => v.Alive))
        {
            v.StandAt(world.HomePoint(v));
        }

        // At the post's door, the cart parked, nothing kept and nothing a boat left: home with it.
        merchant.StandAt(post.Position);
        BehaviorSystem.DecideForTest(world, merchant);
        Assert.Equal(VillagerState.TravelingHome, merchant.State);
        Assert.True(merchant.CartForTheWorks && merchant.PushingACart, "the merchant left the post's door without its cart");

        // Away from the post with the cart in hand and nothing to carry: home with it, never D693's empty leg back.
        merchant.StandAt(TheWarehouse(world).Position);
        merchant.State = VillagerState.Idle;
        BehaviorSystem.DecideForTest(world, merchant);
        Assert.Equal(VillagerState.TravelingHome, merchant.State);
        Assert.True(merchant.CartForTheWorks && merchant.PushingACart);
        Assert.Equal(config.HandcartUses, post.HandcartUses);

        // And the household's errands wait while the post's cart is in hand (§13.2): a low larder at home, nobody else to shop —
        // the merchant still goes home with the cart rather than shopping with it.
        Household home = world.HouseholdOf(merchant);
        foreach (Villager housemate in world.Villagers.Where(v => v.HouseholdId == home.Id && v != merchant))
        {
            housemate.Alive = false; // the spare-hand rule (D385) would hand the shopping to a housemate at home
        }

        SetLarder(home, world.TargetFoodFor(home) * 20 / 100);
        merchant.State = VillagerState.Idle;
        Assert.True(BehaviorSystem.PlanFetchForTest(world, merchant) is not null, "the pose does not want a fetch");
        BehaviorSystem.DecideForTest(world, merchant);
        Assert.Equal(VillagerState.TravelingHome, merchant.State);
        Assert.True(merchant.CartForTheWorks && merchant.PushingACart, "the merchant shopped with the post's cart");
    }

    /// <summary>§13.2b: spare work (D372) stays on foot — at the door with nothing to move and a heap on the ground, the marketer tidies without the cart, which stays parked and unworn.</summary>
    [Fact]
    public void AMarketerWithNothingToMoveTidiesGroundOnFootAndLeavesTheCartAtTheDoor()
    {
        SimConfig config = Config;
        (_, SimWorld world, Workplace market, StoreBuilding counter, Villager marketer) = AMarketErrandPose(config, config.HandcartUses);
        NothingToMove(world, counter);
        marketer.Rhythm = 0;
        world.SetDown(new GridPos(market.Tile.X + 3, market.Tile.Y), Goods.Logs, 5);

        BehaviorSystem.DecideForTest(world, marketer);
        Assert.Equal(VillagerState.TidyingGround, marketer.State);
        Assert.False(marketer.PushingACart || marketer.CartForTheWorks, "the marketer took the market's cart to tidy a heap");
        Assert.Equal(config.HandcartUses, market.HandcartUses);
    }

    /// <summary>§13.2b: the works' cart sleeps only with a holder resting where they rest — not on the road, not at the market, and never the household's cart.</summary>
    [Fact]
    public void TheWorksCartSleepsOnlyWithARestingHolderAtHome()
    {
        SimConfig config = Config;
        (_, SimWorld world, Workplace market, _, Villager marketer) = AMarketErrandPose(config, config.HandcartUses);
        marketer.StandAt(world.HomePoint(marketer));
        marketer.PushingACart = true;
        marketer.CartForTheWorks = true;
        marketer.State = VillagerState.Resting;
        Assert.True(world.TheWorksCartSleepsWith(marketer));

        marketer.State = VillagerState.TravelingHome;
        Assert.False(world.TheWorksCartSleepsWith(marketer));

        marketer.State = VillagerState.Resting;
        marketer.StandAt(market.Position);
        Assert.False(world.TheWorksCartSleepsWith(marketer));

        marketer.StandAt(world.HomePoint(marketer));
        marketer.CartForTheWorks = false;
        Assert.False(world.TheWorksCartSleepsWith(marketer), "the household's cart was read as the works' sleeping at the door");
        marketer.CartForTheWorks = true;
        marketer.PushingACart = false;
        Assert.False(world.TheWorksCartSleepsWith(marketer));
    }

    /// <summary>
    /// §13.2c: with `restock_short_armfuls_with_cart` 3 a counter at a market that owns a cart is short only with room for three
    /// armfuls; the cart worn out, it is short at one again; the shipped number is three (D707) and nought is refused.
    /// </summary>
    [Fact]
    public void TheCounterRunsDownToTheKnobsArmfulsUnderACartOnly()
    {
        SimConfig config = Config with { RestockShortArmfulsWithCart = 3 };
        (_, SimWorld world, Workplace market, StoreBuilding counter, _) = AMarketErrandPose(config, config.HandcartUses);
        int armful = config.CarryCapacity;
        int limit = world.MarketStockLimit(counter, Goods.Produce);
        Assert.True(limit >= 3 * armful, $"the counter's limit of {limit} cannot be three armfuls short");

        // Two armfuls short under a cart: not short.
        counter.Store.TakeAll(Goods.Produce);
        counter.Store.Receive(Goods.Produce, limit - (2 * armful));
        Assert.Equal(2 * armful, counter.RoomFor(Goods.Produce));
        Assert.False(world.CounterShortOf(counter, Goods.Produce));

        // Three short: short.
        Assert.True(counter.Store.TryTake(Goods.Produce, armful));
        Assert.True(world.CounterShortOf(counter, Goods.Produce));

        // One short: not under a cart; short once the market's cart is worn out.
        counter.Store.Receive(Goods.Produce, 2 * armful);
        Assert.Equal(armful, counter.RoomFor(Goods.Produce));
        Assert.False(world.CounterShortOf(counter, Goods.Produce));
        market.HandcartUses = 0;
        Assert.True(world.CounterShortOf(counter, Goods.Produce));

        // As shipped the number is three — a cart's load, D707 — and nought is refused in words.
        Assert.Equal(3, ShippedConfig.Load().RestockShortArmfulsWithCart);
        SimConfigException refused = Assert.Throws<SimConfigException>(() => SimConfigLoader.Parse("""{ "restock_short_armfuls_with_cart": 0 }""", "<modded>"));
        Assert.Contains("restock_short_armfuls_with_cart", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>The counter full and every other store bare, so no restock and no haul exists (§13.2a's second case).</summary>
    private static void NothingToMove(SimWorld world, StoreBuilding counter)
    {
        counter.Store.Receive(Goods.Produce, counter.RoomFor(Goods.Produce));
        foreach (StoreBuilding store in world.StoreBuildings.Where(store => store != counter))
        {
            for (int g = 0; g < world.GoodsCatalog.Count; g++)
            {
                store.Store.TakeAll((Goods)g);
            }
        }
    }

    /// <summary>
    /// §13.2: a pusher who comes to decide standing at the market's door — a haul that ended at the counter itself — parks the
    /// cart first, so the round wears a trip, before any errand takes it out again.
    /// </summary>
    [Fact]
    public void APusherDecidingAtTheMarketsDoorParksFirst()
    {
        SimConfig config = Config;
        (_, SimWorld world, Workplace market, _, Villager marketer) = AMarketErrandPose(config, config.HandcartUses);
        marketer.PushingACart = true;
        marketer.CartForTheWorks = true;
        marketer.State = VillagerState.Idle;
        Assert.Equal(market.Tile, marketer.Tile);

        BehaviorSystem.DecideForTest(world, marketer);

        // Parked and worn at the door; the counter is short, so the next errand takes it out again from the door.
        Assert.Equal(config.HandcartUses - 1, market.HandcartUses);
        Assert.Equal(VillagerState.CollectingForMarket, marketer.State);
        Assert.True(marketer.CartForTheWorks && marketer.PushingACart);
    }

    /// <summary>§13.2: with the market's cart in hand the marketer does the market's work before the household's errands — the cart is never borrowed for the shopping.</summary>
    [Fact]
    public void AMarketerWithTheWorksCartDoesTheMarketsWorkBeforeTheHouseholds()
    {
        SimConfig config = Config;
        (_, SimWorld world, Workplace market, _, Villager marketer) = AMarketErrandPose(config, config.HandcartUses);
        StoreBuilding granary = world.AnyStoreOf(StoreKind.Granary);
        Household home = world.HouseholdOf(marketer);
        foreach (Villager housemate in world.Villagers.Where(v => v.HouseholdId == home.Id && v != marketer))
        {
            housemate.Alive = false; // the spare-hand rule (D385) would hand the shopping to a housemate at home
        }

        SetLarder(home, world.TargetFoodFor(home) * 20 / 100);
        marketer.PushingACart = true;
        marketer.CartForTheWorks = true;
        marketer.State = VillagerState.Idle;
        marketer.StandAt(granary.Position);
        Assert.True(BehaviorSystem.PlanFetchForTest(world, marketer) is not null, "the pose does not want a fetch");

        BehaviorSystem.DecideForTest(world, marketer);

        // Standing at the granary with the cart: the counter's load, three armfuls, and the household's shopping waits.
        Assert.Equal(VillagerState.StockingTheMarket, marketer.State);
        Assert.Equal(config.CarryCapacity * config.CartArmfuls, marketer.Carried.Held);
        Assert.True(marketer.CartForTheWorks && marketer.PushingACart);
        Assert.Equal(config.HandcartUses, home.HandcartUses);
    }

    /// <summary>§13.2: the works' cart wears a trip at the door, is gone at nought — the log says so — and the marketer fetches another from the shelf.</summary>
    [Fact]
    public void TheWorksCartWearsATripAndIsReplacedAtNought()
    {
        SimConfig config = Config;
        var sink = new InMemoryLogSink();
        SimLoop loop = SimFactory.CreatePhase0(config, sink);
        SimWorld world = loop.World;
        ToolsTests.KeepTheVillageWarmAndFed(world);
        NobodyWorks(world);
        CartEveryHousehold(world);
        Workplace market = TheMarket(world);
        StoreBuilding counter = world.AnyStoreOf(StoreKind.Market);
        counter.Store.TakeAll(Goods.Produce);
        world.AnyStoreOf(StoreKind.Granary).Store.Receive(Goods.Produce, 3000);
        TheWarehouse(world).Store.Add(Goods.Handcart, 2);
        market.HandcartUses = 1;
        Villager marketer = PinAMarketer(world);
        foreach (Villager v in world.Villagers.Where(v => v.Alive))
        {
            v.StandAt(world.HomePoint(v));
        }

        marketer.WorkplaceId = market.Id;
        market.WorkerIds.Add(marketer.Id);
        marketer.StandAt(market.Position);

        bool wornOut = false;
        for (int tick = 0; tick < config.TicksPerSeason * 2; tick++)
        {
            loop.StepOnce();
            wornOut |= market.HandcartUses == 0 && !marketer.PushingACart;
            if (wornOut && market.HandcartUses == config.HandcartUses && !marketer.CartForTheWorks)
            {
                break;
            }
        }

        Assert.True(wornOut, "The market's cart on its last trip never wore out in two seasons.");
        Assert.Equal(1, sink.Entries.Count(e => e.Message.Contains($"{market.Name} handcart is worn through", StringComparison.Ordinal)));
        Assert.Equal(config.HandcartUses, market.HandcartUses);
        Assert.Equal(1, TheWarehouse(world).Store[Goods.Handcart]);
    }

    /// <summary>
    /// §13.2–§13.3: the merchant takes the post's cart for both runs — three armfuls of the surplus down to the post, parked and
    /// worn there; then what a boat left, three armfuls home with the cart, the cart brought back and worn again.
    /// </summary>
    [Fact]
    public void TheMerchantTakesThePostsCartForBothRuns()
    {
        SimLoop loop = SimFactory.CreatePhase0(ShippedConfig.Established(), new InMemoryLogSink());
        SimWorld world = loop.World;
        SimConfig config = world.Config;
        ToolsTests.KeepTheVillageWarmAndFed(world);
        NobodyWorks(world);
        CartEveryHousehold(world);
        Workplace post = TradeTests.RaiseAPost(world);
        StoreBuilding postStore = world.PostStoreOf(post)!;
        post.HandcartUses = config.HandcartUses;
        int cartLoad = config.CarryCapacity * config.CartArmfuls;
        Villager merchant = world.Villagers.First(v => v.Alive && v.CanWork);
        world.SetPinnedTrade(merchant, JobKind.Merchant);
        merchant.WorkplaceId = post.Id;
        post.WorkerIds.Add(merchant.Id);
        foreach (Villager v in world.Villagers.Where(v => v.Alive))
        {
            v.StandAt(world.HomePoint(v));
        }

        merchant.StandAt(post.Position);

        // Exactly a cart's load of stone above the keep.
        StoreBuilding warehouse = world.StoreBuildings.First(s => s.Accepts(Goods.Stone));
        Assert.Equal(cartLoad, warehouse.Put(Goods.Stone, cartLoad));
        world.TradeKeep.Set(Goods.Stone, world.InStores(Goods.Stone) - cartLoad);

        int biggestDown = 0;
        for (int tick = 0; tick < config.TicksPerSeason && postStore.Store[Goods.Stone] < cartLoad; tick++)
        {
            loop.StepOnce();
            if (merchant.State == VillagerState.StockingThePost && merchant.PushingACart)
            {
                Assert.True(merchant.CartForTheWorks);
                biggestDown = Math.Max(biggestDown, merchant.Carried.Held);
            }
        }

        _output.WriteLine($"down: {biggestDown} in one trip; the post holds {postStore.Store[Goods.Stone]} stone and its cart has {post.HandcartUses} trips.");
        Assert.Equal(cartLoad, biggestDown);
        Assert.Equal(cartLoad, postStore.Store[Goods.Stone]);
        loop.Step(2);
        Assert.True(!merchant.CartForTheWorks || merchant.State is VillagerState.TravelingHome or VillagerState.Resting,
            "the cart was neither parked at the post after the run down nor taken home for the night");
        Assert.Equal(config.HandcartUses - 1, post.HandcartUses);

        // What a boat left: a cart's load of logs at the post, no keep order on logs — it goes home with the cart, and the cart comes back.
        Assert.Null(world.TradeKeep.For(Goods.Logs));
        int logsBefore = world.InStores(Goods.Logs);
        Assert.Equal(cartLoad, postStore.Put(Goods.Logs, cartLoad));
        int biggestHome = 0;
        int emptyLegTicks = 0;
        int homeLegTicks = 0;
        int saidHome = 0;
        for (int tick = 0; tick < config.TicksPerSeason && !(world.InStores(Goods.Logs) == logsBefore + cartLoad && world.TheWorksCartSleepsWith(merchant)); tick++)
        {
            loop.StepOnce();
            if (merchant.State == VillagerState.HaulingToStore && merchant.PushingACart && merchant.CartForTheWorks)
            {
                biggestHome = Math.Max(biggestHome, merchant.Carried.Held);
            }

            if (merchant.State == VillagerState.StockingThePost && merchant.CartForTheWorks && !merchant.IsCarrying)
            {
                emptyLegTicks++;
            }

            if (merchant.State == VillagerState.TravelingHome && merchant.CartForTheWorks)
            {
                homeLegTicks++;
                saidHome += merchant.DescribeState() == "pushing the handcart home for the night" ? 1 : 0;
            }
        }

        _output.WriteLine($"home: {biggestHome} in one trip, {homeLegTicks} ticks pushing the cart home for the night, {emptyLegTicks} back empty; the post's cart has {post.HandcartUses} trips.");
        Assert.Equal(cartLoad, biggestHome);
        Assert.Equal(logsBefore + cartLoad, world.InStores(Goods.Logs));
        // §13.2b: the haul home ends the day — the cart sleeps at the merchant's door, never D693's empty leg back to the post.
        Assert.Equal(0, emptyLegTicks);
        Assert.True(homeLegTicks > 0, "the post's cart was never pushed home for the night");
        Assert.True(saidHome > 0, "the walk home never read 'pushing the handcart home for the night'");
        Assert.True(world.TheWorksCartSleepsWith(merchant), "the cart did not sleep at the merchant's door");
        // Two parks at the post's door wore it: the run down, and the arrival from home for the boat's goods — a round that ends at
        // the door wears one whichever door it began at (§13.2b).
        Assert.Equal(config.HandcartUses - 2, post.HandcartUses);
    }

    // ---------------------------------------------------------------
    //  Helpers
    // ---------------------------------------------------------------

    private static SimLoop Reload(SimLoop loop)
    {
        JsonObject document = SaveGame.Capture(loop, "test", "2026-11-04 12:00", "test");
        return SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), loop.World.Config, null);
    }
}
