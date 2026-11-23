using Kith.Sim.World;

namespace Kith.Sim.Core;

/// <summary>
/// ⭐ THE LONG WALK (W14, `market-stand.md §5.4`, Joe, D839: <i>"The long walk"</i>; his numbers, D852) — how the village learns
/// the market stall: once enough different households have walked far to shop, the far houses ask for a stall of their own.
/// </summary>
/// <remarks>
/// <para>
/// <b>Counted at the counter, never per tick</b> (CLAUDE.md's rule): a shopper collecting at a store whose round trip from
/// their home — twice the walk in the one cost field, the PLANNED walk (D851 measured it against the elapsed one; the planned
/// never counts a trip made long by a stop for shelter) — is at least <c>market_stall_walk_ticks</c> marks their household,
/// once. The village's count of such households reaching <c>market_stall_households</c> sets the latch.
/// </para>
/// <para>
/// ⛔ <b>The workshop's shape (D676): the latch is the state, the count a statistic.</b> <see cref="MarketStallLearned"/> is
/// hashed as a tag byte when true; <see cref="HouseholdsThatWalkedFar"/> and <c>Household.WalkedFar</c> are not — two
/// villages at two and four households hash alike and diverge later, a future difference below the gate that nothing reads.
/// <c>MarketStandTests.TheLongWalksCountIsNotInTheFingerprint</c> asserts the gap on purpose.
/// </para>
/// </remarks>
public sealed partial class SimWorld
{
    /// <summary>
    /// Whether the village has learned the market stall (W14) — set once, where the long walk's count crosses
    /// <c>market_stall_households</c>. Saved as <c>market_stall_learned</c> only when true; hashed as a tag byte when true.
    /// </summary>
    public bool MarketStallLearned { get; internal set; }

    /// <summary>
    /// How many different households have made a long walk to shop (§5.4) — a statistic the latch is set from, never hashed.
    /// Saved as <c>households_that_walked_far</c> when above nought. It stops counting once the stall is learned.
    /// </summary>
    public int HouseholdsThatWalkedFar { get; internal set; }

    /// <summary>
    /// A shopper has collected at <paramref name="store"/> (<c>BehaviorSystem.CollectFromStore</c>) — mark their household if
    /// the round trip from its home is the long walk, and learn the stall at the count's knob.
    /// </summary>
    internal void WalkedToShop(Household household, Villager walker, StoreBuilding store)
    {
        if (MarketStallLearned || household.WalkedFar || household.HomeTile is not GridPos home)
        {
            return;
        }

        int there = TravelCost.TicksBetween(home, store.Tile);
        if (there == TravelCostField.Unreachable || 2 * there < Config.MarketStallWalkTicks)
        {
            return;
        }

        household.WalkedFar = true;
        HouseholdsThatWalkedFar++;
        if (HouseholdsThatWalkedFar < Config.MarketStallHouseholds)
        {
            return;
        }

        MarketStallLearned = true;
        LearnedByDoing(
            "The far houses ask for a stall",
            $"{walker.Name} walked {DaysInWords(2 * there)} for {WhatIsCarried(walker)}; the far houses ask for a stall. Build a "
            + "market stall among them: it keeps half what the market does, and a marketer of its own fills it from the "
            + $"storehouses. {Clock.SeasonAndYear()}.");
    }

    /// <summary>What the build bar says before the stall is learned — how far the long walk has come (§5.4).</summary>
    private string WhyNoStallYet() =>
        $"The far houses have not asked for a stall yet — {HouseholdsThatWalkedFar} of the {Config.MarketStallHouseholds} "
        + $"households it takes have walked {DaysInWords(Config.MarketStallWalkTicks)} there and back to shop.";

    /// <summary>A round trip in days, in words: <i>"ten days"</i>, <i>"a day"</i>.</summary>
    private string DaysInWords(int ticks)
    {
        int days = ticks / Config.TicksPerDay;
        string[] words =
        {
            "no", "a", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve",
            "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen", "twenty",
        };

        string count = days >= 0 && days < words.Length ? words[days] : days.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return days == 1 ? "a day" : $"{count} days";
    }

    /// <summary>The first good in a villager's arms, by its name — what the long walk was for.</summary>
    private string WhatIsCarried(Villager walker)
    {
        for (int id = 0; id < GoodsCatalog.Count; id++)
        {
            if (walker.Carried[(Goods)id] > 0)
            {
                return GoodsCatalog.NameOf((Goods)id);
            }
        }

        return "their shopping";
    }
}
