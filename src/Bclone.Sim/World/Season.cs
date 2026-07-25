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
    Resting,
    Dead,
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

/// <summary>How a villager's life ended.</summary>
public enum CauseOfDeath
{
    /// <summary>Still alive.</summary>
    None,

    /// <summary>Ran out of food. The failure arc.</summary>
    Starvation,

    /// <summary>Lived a full life. The good arc.</summary>
    OldAge,
}
