using Kith.Sim.World;

namespace Kith.Sim.Core;

/// <summary>
/// ⭐ The library's borrowing (`specs/library-borrowing.md`, D864–D865): which library a home is near enough to, whether a
/// household is writing to kin, and what the library's card says. One copy each, read by the walk's offer and the card —
/// the tavern's shape (`SimWorld.Tavern.cs`, D843).
/// </summary>
public sealed partial class SimWorld
{
    /// <summary>
    /// ⭐ The letters switch (Joe's addition 2, D865) — the Nomads switch's shape: on, households near a library write to kin;
    /// off, nobody fetches paper and no kin come. The village's own, started from <c>letters_enabled</c>, saved, hashed only
    /// when off; a toggle on every library's card.
    /// </summary>
    public bool LettersEnabled { get; set; } = true;

    /// <summary>The library standing on this tile — an errand's fixed destination — or null.</summary>
    public Library? LibraryAt(GridPos tile)
    {
        for (int i = 0; i < Libraries.Count; i++)
        {
            if (Libraries[i].Tile == tile)
            {
                return Libraries[i];
            }
        }

        return null;
    }

    /// <summary>
    /// The nearest open library within <c>library_reach_tiles</c> of this household's home by the one cost field, or null (no
    /// home, no library, none near enough). Ties go to the first in the list (D15).
    /// </summary>
    public Library? LibraryWithinReachOf(Household household)
    {
        ArgumentNullException.ThrowIfNull(household);
        if (household.HomeTile is not GridPos home)
        {
            return null;
        }

        Library? best = null;
        int bestCost = int.MaxValue;
        for (int i = 0; i < Libraries.Count; i++)
        {
            Library library = Libraries[i];
            if (library.Closed)
            {
                continue;
            }

            int cost = WalkToTheLibrary(home, library);
            if (cost < bestCost)
            {
                best = library;
                bestCost = cost;
            }
        }

        return best;
    }

    /// <summary>
    /// The walk from a home to a library in the one cost field, or <see cref="int.MaxValue"/> beyond <c>library_reach_tiles</c> or
    /// not walkable at all — <b>the one copy of "within reach"</b>, asked by the offer and the card alike.
    /// </summary>
    private int WalkToTheLibrary(GridPos home, Library library)
    {
        int cost = TravelCost.Cost(home, library.Tile);
        return cost != TravelCostField.Unreachable && cost <= Config.LibraryReachTiles * TravelCostField.BaseTileCost
            ? cost
            : int.MaxValue;
    }

    /// <summary>
    /// Whether a household is writing to kin (§5.2): letters on, a home lived in, and a spare bed in it (Joe's cap — kin need a
    /// bed; `HouseholdCapacity` is the birth gate's own question).
    /// </summary>
    public bool HouseholdIsWriting(Household household)
    {
        ArgumentNullException.ThrowIfNull(household);
        int living = LivingMembersOf(household);
        return LettersEnabled
            && household.HasHome
            && living > 0
            && living < VillageEconomy.HouseholdCapacity(household.HomeKind, Config);
    }

    /// <summary>
    /// ⭐ The library's card sentence (§5.4, D843's lesson; Joe's addition, D865): whether letters are off, the library fallen in,
    /// how many lived-in homes are within reach, and how many of them are writing with how many letters out. Counted when asked,
    /// never stored (D335). <c>Working</c> is false when nobody can borrow.
    /// </summary>
    public (bool Working, string Sentence) SayTheLibrary(Library library)
    {
        ArgumentNullException.ThrowIfNull(library);
        if (!LettersEnabled)
        {
            return (false, "Letters to kin are off — nobody writes, and nobody comes.");
        }

        if (library.Closed)
        {
            return (false, "It has fallen in — nobody borrows until the builders mend it.");
        }

        int reach = Config.LibraryReachTiles;
        int within = 0, homes = 0, writing = 0, letters = 0;
        for (int i = 0; i < Households.Count; i++)
        {
            Household household = Households[i];
            if (household.HomeTile is not GridPos home || LivingMembersOf(household) == 0)
            {
                continue;
            }

            homes++;
            if (WalkToTheLibrary(home, library) == int.MaxValue)
            {
                continue;
            }

            within++;
            if (household.LettersWritten > 0)
            {
                writing++;
                letters += household.LettersWritten;
            }
        }

        if (within == 0)
        {
            return (false, $"No home is within a {reach}-tile walk, so nobody comes to borrow. A library draws the homes around "
                + "it: build it near them.");
        }

        string lends = within == homes
            ? $"Lends paper to every home — all are within a {reach}-tile walk"
            : $"Lends paper to {within} of {homes} homes within a {reach}-tile walk";
        return (true, writing == 0
            ? lends + "."
            : $"{lends} — {writing} {(writing == 1 ? "is" : "are")} writing to kin, {letters} "
                + $"{(letters == 1 ? "letter" : "letters")} out, none answered yet.");
    }
}
