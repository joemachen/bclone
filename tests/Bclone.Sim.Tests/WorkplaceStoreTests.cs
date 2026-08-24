using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The local store at a workplace — <c>specs/professions.md §5</c> (D30, D107) and
/// <c>specs/crops-and-orchards.md §3.1</c> (D161).
/// </summary>
/// <remarks>
/// <para>
/// <b>⛔ THESE GUARD A SEAM THAT HAS NEVER BEEN CROSSED.</b> <c>Workplace.Store</c> has existed
/// since D30 and <b>nothing in the sim has ever written to it</b> — so the village has two ways
/// to ask how much food it has, and they have never once disagreed:
/// </para>
/// <list type="bullet">
/// <item><c>FoodInGranaries()</c> reads <c>StoreBuildings</c> only — blind to a workplace.</item>
/// <item><c>TotalFood()</c> reads <c>AllStores()</c>, which includes workplaces.</item>
/// </list>
/// <para>
/// The farm is the first profession to fill a local store (Joe, D161: <i>"the farm itself can
/// store up to 100 of the harvest goods"</i>), and on that day the two answers diverge by up to
/// a hundred food per farm. <b>Four load-bearing things read the blind one</b> — the birth gate,
/// the village-wide reason to gather, the food stock limit, and how much room there is for more.
/// </para>
/// <para>
/// <b>So a village whose food sat in its farms would believe itself poorer than it is and stop
/// having children.</b> That is D155's symptom — Joe: <i>"They aren't having any kids?"</i> —
/// arriving from a new direction, and structurally it is D81's bug: one comparison asking two
/// different questions. D81 is recorded as <i>D76's seam for the fifth time</i>; this is the
/// sixth, and it was found by writing the spec rather than by playing.
/// </para>
/// <para>
/// <b>These land before crops do, deliberately.</b> Fixing a latent seam while nothing writes to
/// the store means the fix is provably a no-op — both 50-year goldens must stay put — where
/// fixing it afterwards would mean untangling it from a new mechanic.
/// </para>
/// </remarks>
public sealed class WorkplaceStoreTests
{
    private readonly ITestOutputHelper _output;

    public WorkplaceStoreTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimLoop Build(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink());

    /// <summary>
    /// ⭐ Where food is kept does not change how much food the village believes it has.
    /// </summary>
    /// <remarks>
    /// <b>The invariant, stated at the level the bug lives at.</b> Moving food out of a granary
    /// and into a workplace's own store is a change of *place*, not of quantity — so every
    /// question the village asks about its food must give the same answer either side of the
    /// move. This is deliberately not a test of one reader: D144's lesson is that a rule
    /// answered by the predicate and ignored by the deposit is a rule nobody has tested.
    /// </remarks>
    [Fact]
    public void MovingFoodIntoAWorkplaceStoreChangesNoAnswerAboutFood()
    {
        SimLoop loop = Build(Config);
        SimWorld world = loop.World;

        // Let the village get on its feet, so there is a real granary with real food in it.
        loop.Step(Config.TicksPerYear * 2);

        StoreBuilding granary = world.AnyStoreOf(StoreKind.Granary);
        Workplace workplace = world.Workplaces[0];

        int before = world.FoodTheVillageHolds();
        int roomBefore = world.FoodTheVillageHasRoomFor();

        // The move. A hundred is `farm_store_cap`'s stated default (D161).
        const int Moved = 100;
        Assert.True(granary.Store.TryTake(Goods.Food, Moved), "The granary had no food to move.");
        Assert.Equal(Moved, workplace.Store.Add(Goods.Food, Moved));

        int after = world.FoodTheVillageHolds();
        int roomAfter = world.FoodTheVillageHasRoomFor();

        _output.WriteLine(
            $"moved {Moved} food from {granary.Name} into {workplace.Name}: "
            + $"the village holds {before} -> {after}, room for {roomBefore} -> {roomAfter}, "
            + $"and FoodInGranaries reads {world.FoodInGranaries()}");

        // Anti-vacuity (D7): the move has to have actually happened, or this compares two
        // identical worlds and passes for the wrong reason.
        Assert.Equal(Moved, workplace.Store.Food);

        Assert.Equal(before, after);
        Assert.Equal(roomBefore, roomAfter);
    }

    /// <summary>
    /// ⭐ And the consequence: a village whose food is in its farms still has children.
    /// </summary>
    /// <remarks>
    /// The behavioural half, and the one that would have caught this from the outside. The
    /// birth gate compares the village's food against a share of the granary target (D153,
    /// D155); with the food sitting in a workplace it must still clear that bar, because the
    /// food is real, reachable, and a trader will come for it.
    /// </remarks>
    [Fact]
    public void FoodInAFarmStoreStillCountsTowardsTheBirthGate()
    {
        SimLoop loop = Build(Config);
        SimWorld world = loop.World;

        loop.Step(Config.TicksPerYear * 2);

        int bar = world.TargetFoodForTheGranary() * Config.BirthFoodPercent / 100;
        Workplace workplace = world.Workplaces[0];

        // Empty EVERY store that holds food into the farm, so the *only* village food is in a
        // workplace.
        //
        // ⚠️ IT USED TO EMPTY THE GRANARY ALONE, AND THAT STOPPED BEING THE WHOLE STORY (D197).
        // The market now gets deliberately stocked (`storage-and-distribution.md §14.8`), so
        // there is a second building holding food and `FoodInGranaries` — which counts every
        // store that accepts food, not only granaries — read 75 instead of 0. **The claim was
        // never wrong; the fixture's premise was.**
        int all = 0;
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            int held = store.Store.Food;
            if (held > 0 && store.Store.TryTake(Goods.Food, held))
            {
                all += held;
            }
        }

        Assert.True(all > bar, $"The village's stores hold {all}, which is not above the bar of "
            + $"{bar} — this fixture cannot pose the case.");
        workplace.Store.Add(Goods.Food, all);

        _output.WriteLine(
            $"all {all} of the village's stored food is in {workplace.Name}; the birth gate's bar is "
            + $"{bar}, FoodInGranaries reads {world.FoodInGranaries()}, "
            + $"FoodTheVillageHolds reads {world.FoodTheVillageHolds()}");

        Assert.Equal(0, world.FoodInGranaries());
        Assert.True(
            world.FoodTheVillageHolds() >= bar,
            "The village's whole food store sits in a farm and the birth gate cannot see a "
            + "grain of it, so it will stop having children — D81's seam, one store over.");
    }

    /// <summary>Household larders are not village stores, and must not be counted as such.</summary>
    /// <remarks>
    /// <para>
    /// <b>The boundary that keeps this from becoming <c>TotalFood()</c>.</b> D153 removed the
    /// household terms from the birth gate on purpose — a family's own larder is food already
    /// distributed, and counting it would re-add the term Joe deleted. So the new reader sits
    /// between the two that exist: wider than the granaries, narrower than everything.
    /// </para>
    /// <para>
    /// <b>⚠️ THE FIRST VERSION OF THIS GUARD WAS VACUOUS AND PASSED AGAINST THE UNFIXED CODE.</b>
    /// It compared the three tiers on a village where <em>no workplace held anything</em>, so
    /// the workplace term was zero and the equation balanced whether or not the reader counted
    /// workplaces at all. Caught by disabling the fix and finding only two of three guards went
    /// red — which is the whole reason that check is mandatory here. **It now puts food in a
    /// workplace first**, so all three tiers are non-zero and the boundary is genuinely pinned.
    /// </para>
    /// </remarks>
    [Fact]
    public void AHouseholdLarderIsNotVillageFood()
    {
        SimLoop loop = Build(Config);
        SimWorld world = loop.World;

        loop.Step(Config.TicksPerYear * 2);

        // A workplace has to be holding something, or the middle tier is zero and this
        // equation balances for the wrong reason.
        StoreBuilding granary = world.AnyStoreOf(StoreKind.Granary);
        Workplace workplace = world.Workplaces[0];
        Assert.True(granary.Store.TryTake(Goods.Food, 100));
        workplace.Store.Add(Goods.Food, 100);

        int held = world.FoodTheVillageHolds();
        int inLarders = 0;
        foreach (Household household in world.Households)
        {
            inLarders += household.Stockpile.Food;
        }

        _output.WriteLine(
            $"stores {world.FoodInGranaries()} + workplaces {workplace.Store.Food} "
            + $"= the village holds {held}; larders hold {inLarders}; "
            + $"TotalFood is {world.TotalFood()}");

        // Anti-vacuity on both tiers this is drawing a line between (D7).
        Assert.True(inLarders > 0, "No household has any food, so this guard is watching nothing.");
        Assert.True(workplace.Store.Food > 0, "No workplace holds anything, so the middle tier "
            + "is zero and this passes whether or not it is counted.");

        Assert.Equal(held + inLarders, world.TotalFood());
    }
}
