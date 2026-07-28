namespace Bclone.Sim.World;

/// <summary>The four seasons, in order. Winter is the one with teeth.</summary>
public enum Season
{
    Spring = 0,
    Summer = 1,
    Fall = 2,
    Winter = 3,
}

/// <summary>What a villager is doing right now.</summary>
public enum VillagerState
{
    Idle,
    TravelingToFood,
    Gathering,
    TravelingHome,
    TravelingToTrees,
    Cutting,
    Resting,
    Dead,

    /// <summary>Walking to the woodcutter's hut.</summary>
    TravelingToHut,

    /// <summary>Splitting logs into firewood at the hut (D29).</summary>
    MakingFirewood,

    /// <summary>Hauling a load to a store building (D30).</summary>
    HaulingToStore,

    /// <summary>Walking to a store to collect what the household is short of (D30).</summary>
    FetchingFromStore,

    /// <summary>A marketer walking to pick up goods that are in the wrong place (D14).</summary>
    CollectingForMarket,

    /// <summary>A marketer carrying goods to the household that needs them (D14).</summary>
    DeliveringToHome,

    /// <summary>A builder fetching materials for a site the player marked out (D43).</summary>
    FetchingMaterials,

    /// <summary>A builder raising a marked building.</summary>
    Building,
}

/// <summary>
/// Coarse bands of physical decline, used to narrate the turn of a life once
/// rather than every tick.
/// </summary>
public enum VigourStage
{
    /// <summary>Working at full strength.</summary>
    Prime = 0,

    /// <summary>Past their peak — the same work takes more trips.</summary>
    Slowing = 1,

    /// <summary>Visibly failing. Every winter is now a question.</summary>
    Frail = 2,
}

/// <summary>
/// What a villager is capable of, derived from age.
/// </summary>
/// <remarks>
/// Childhood exists from Phase 1 rather than Phase 0 because it only makes sense
/// once there is a household to depend on — a frail child alone is just an
/// unsurvivable opening (decision D13).
/// </remarks>
public enum LifeStage
{
    /// <summary>Too young to work. Eats from the household store and gives nothing back.</summary>
    Child = 0,

    /// <summary>Working age.</summary>
    Adult = 1,

    /// <summary>Still working, but visibly declining — see <see cref="VigourStage"/>.</summary>
    Elder = 2,
}

/// <summary>How a villager's life ended.</summary>
public enum CauseOfDeath
{
    /// <summary>Still alive.</summary>
    None,

    /// <summary>Ran out of food. The failure arc.</summary>
    Starvation,

    /// <summary>Lived a full life. The good arc.</summary>
    OldAge,

    /// <summary>
    /// Froze. The household ran out of firewood in winter (D29).
    /// </summary>
    /// <remarks>
    /// Phase 0 refused a second death system on the grounds that winter's danger
    /// should be food and nothing else. Reversing that is deliberate, and the
    /// condition attached to the reversal is that a death must never be <em>ambiguous</em>
    /// between this and <see cref="Starvation"/> — see <c>MortalitySystem</c>.
    /// </remarks>
    Cold,
}
