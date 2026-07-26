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

    /// <summary>
    /// Timber on hand.
    /// </summary>
    /// <remarks>
    /// Wood serves three purposes (decision D17): building material, winter fuel,
    /// and tools. Building material lands first because it needs no new death
    /// mechanic and it ties household formation to labour — the village can only
    /// spread as fast as it can build, which is what makes "forage or cut timber?"
    /// a genuinely contested decision rather than a cosmetic one.
    /// </remarks>
    public int Wood { get; private set; }

    /// <summary>Total wood ever cut.</summary>
    public int LifetimeWoodCut { get; private set; }

    public void Add(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), $"Cannot add negative food ({amount}).");
        }

        Food += amount;
        LifetimeGathered += amount;
    }

    public void AddWood(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), $"Cannot add negative wood ({amount}).");
        }

        Wood += amount;
        LifetimeWoodCut += amount;
    }

    /// <summary>Take wood if there is enough, changing nothing otherwise.</summary>
    public bool TryTakeWood(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), $"Cannot take negative wood ({amount}).");
        }

        if (Wood < amount)
        {
            return false;
        }

        Wood -= amount;
        return true;
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

/// <summary>
/// A stand of trees. Unlike the berry patch, timber can be cut year-round —
/// which is exactly why it is worth having someone on it in winter.
/// </summary>
public sealed class TreeStand
{
    public required GridPos Position { get; init; }

    public required int YieldPerCut { get; init; }
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
