namespace Bclone.Sim.World;

/// <summary>
/// The villager's food store.
/// </summary>
/// <remarks>
/// Always accessible rather than sited at a location (spec §11). A Phase 0 death
/// caused by starving two tiles from a full larder would read as a bug, not a
/// lesson — and legibility is the deliverable here. Granaries with a real
/// location arrive with households.
/// </remarks>
public sealed class Stockpile
{
    /// <summary>Food on hand. Never negative — asserted, not hoped.</summary>
    public int Food { get; private set; }

    /// <summary>Total food ever gathered, for the epitaph.</summary>
    public int LifetimeGathered { get; private set; }

    public void Add(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), $"Cannot add negative food ({amount}).");
        }

        Food += amount;
        LifetimeGathered += amount;
    }

    /// <summary>Take food if there is enough. Returns false without changing
    /// anything if there is not.</summary>
    public bool TryTake(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), $"Cannot take negative food ({amount}).");
        }

        if (Food < amount)
        {
            return false;
        }

        Food -= amount;
        return true;
    }
}

/// <summary>Where food is foraged. Barren in winter.</summary>
public sealed class FoodSource
{
    public required GridPos Position { get; init; }

    public required int YieldPerGather { get; init; }

    /// <summary>
    /// Winter is the pressure in Phase 0, and food scarcity is its only weapon —
    /// there is deliberately no separate cold/warmth stat (spec §3).
    /// </summary>
    public static bool IsGatherable(Season season) => season != Season.Winter;
}
