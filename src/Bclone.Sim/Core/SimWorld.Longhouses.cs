using Bclone.Sim.Logging;
using Bclone.Sim.World;

namespace Bclone.Sim.Core;

/// <summary>
/// The longhouses, and who lodges in them (`specs/nomads.md §4.5`, D566). Everything a family does through its
/// larder goes through <see cref="LarderOf(Household)"/>, which hands a lodging family the longhouse's.
/// </summary>
public sealed partial class SimWorld
{
    /// <summary>The longhouses standing in the village — placement order, hashed sparsely, like the wells.</summary>
    public List<Longhouse> Longhouses { get; } = new();

    /// <summary>The longhouse standing on a tile, or null.</summary>
    public Longhouse? LonghouseCovering(GridPos tile)
    {
        for (int i = 0; i < Longhouses.Count; i++)
        {
            if (Longhouses[i].Footprint.Covers(tile))
            {
                return Longhouses[i];
            }
        }

        return null;
    }

    public Longhouse? FindLonghouse(int id)
    {
        for (int i = 0; i < Longhouses.Count; i++)
        {
            if (Longhouses[i].Id == id)
            {
                return Longhouses[i];
            }
        }

        return null;
    }

    /// <summary>The longhouse a family lodges in, or null — a family with a house of its own lodges nowhere.</summary>
    public Longhouse? LodgingOf(Household household)
    {
        ArgumentNullException.ThrowIfNull(household);
        return household.LodgedIn == 0 || household.HasHome ? null : FindLonghouse(household.LodgedIn);
    }

    /// <summary>
    /// ⭐ The larder a family eats from and fetches to — its own, or the longhouse's while it lodges there (Joe:
    /// <i>"the communal house needs its own food and firewood larder"</i>). The one seam every larder read goes through.
    /// </summary>
    public Stockpile LarderOf(Household household) =>
        LodgingOf(household) is Longhouse lodging ? lodging.Larder : household.Stockpile;

    public Stockpile LarderOf(Villager villager) => LarderOf(HouseholdOf(villager));

    /// <summary>The living people lodging here, counted off the households — never a second list.</summary>
    public int LodgersAlive(Longhouse longhouse)
    {
        ArgumentNullException.ThrowIfNull(longhouse);
        int lodgers = 0;
        for (int i = 0; i < Households.Count; i++)
        {
            if (LodgingOf(Households[i]) == longhouse)
            {
                lodgers += LivingMembersOf(Households[i]);
            }
        }

        return lodgers;
    }

    /// <summary>The families lodging here, in household order.</summary>
    public IEnumerable<Household> LodgersOf(Longhouse longhouse)
    {
        ArgumentNullException.ThrowIfNull(longhouse);
        for (int i = 0; i < Households.Count; i++)
        {
            if (LodgingOf(Households[i]) == longhouse && LivingMembersOf(Households[i]) > 0)
            {
                yield return Households[i];
            }
        }
    }

    /// <summary>
    /// A roofless family takes a bed in the first standing longhouse with room for all of them — the lowest id,
    /// so a tie is a fact about the village and not about iteration (D15). Says so once. False when none has room.
    /// </summary>
    internal bool LodgeIfRoom(Household household)
    {
        ArgumentNullException.ThrowIfNull(household);
        int members = LivingMembersOf(household);
        for (int i = 0; i < Longhouses.Count; i++)
        {
            Longhouse longhouse = Longhouses[i];
            if (longhouse.Closed || LodgersAlive(longhouse) + members > longhouse.Capacity)
            {
                continue;
            }

            household.LodgedIn = longhouse.Id;
            Narrate(
                $"The {household.Name} household is lodging at {longhouse.Name} until a house of their own is raised. "
                + $"{Clock.SeasonAndYear()}.", LogCategory.Life);
            return true;
        }

        return false;
    }

    /// <summary>A family with a roof of its own moves out — the day its house is raised, or the day it took an empty one.</summary>
    internal void MoveOutOfTheLonghouse(Household household)
    {
        ArgumentNullException.ThrowIfNull(household);
        if (household.LodgedIn == 0)
        {
            return;
        }

        string from = FindLonghouse(household.LodgedIn)?.Name ?? "the longhouse";
        household.LodgedIn = 0;
        if (LivingMembersOf(household) > 0)
        {
            Narrate($"The {household.Name} household moved out of {from} into a house of their own. {Clock.SeasonAndYear()}.", LogCategory.Life);
        }
    }

    /// <summary>Pull a longhouse down: a share of its materials back in store, its lodgers roofless again, its larder lost with it.</summary>
    public void Demolish(Longhouse longhouse)
    {
        ArgumentNullException.ThrowIfNull(longhouse);

        StandingChanged();
        if (!Longhouses.Remove(longhouse))
        {
            throw new ArgumentException($"{longhouse.Name} is not standing.", nameof(longhouse));
        }

        var turnedOut = new List<string>();
        for (int i = 0; i < Households.Count; i++)
        {
            if (Households[i].LodgedIn == longhouse.Id)
            {
                Households[i].LodgedIn = 0;
                if (LivingMembersOf(Households[i]) > 0)
                {
                    turnedOut.Add(Households[i].Name);
                }
            }
        }

        IReadOnlyList<MaterialCost> back = RefundFor(BuildingRecipe.For(BuildingKind.Longhouse, Config));
        string recovered = ReturnToStore(longhouse.Tile, back);
        string lodgers = turnedOut.Count == 0 ? string.Empty
            : $" The {string.Join(", the ", turnedOut)} {(turnedOut.Count == 1 ? "household sleeps" : "households sleep")} in the open again.";
        Narrate(back.Count > 0
            ? $"{Capitalised(longhouse.Name)} was pulled down — {recovered} recovered.{lodgers} {Clock.SeasonAndYear()}."
            : $"{Capitalised(longhouse.Name)} was pulled down.{lodgers} {Clock.SeasonAndYear()}.", LogCategory.Building);
    }

    private int NextLonghouseId()
    {
        int max = 0;
        for (int i = 0; i < Longhouses.Count; i++)
        {
            max = Math.Max(max, Longhouses[i].Id);
        }

        return max + 1;
    }
}
