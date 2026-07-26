using Bclone.Sim.Config;
using Bclone.Sim.Logging;
using Bclone.Sim.Systems;

namespace Bclone.Sim.Core;

/// <summary>
/// Builds a ready-to-run simulation with the systems in their canonical order.
/// </summary>
/// <remarks>
/// One place defines the tick order, so the view layer, the tests, and any future
/// headless tool all run <em>the same simulation</em>. If system order lived at each
/// call site, two of them would eventually disagree and the difference would show up
/// as a mysterious determinism failure.
/// </remarks>
public static class SimFactory
{
    /// <summary>
    /// The canonical Phase 0 tick order (spec §5). This ordering is part of the
    /// determinism contract — see DESIGN.md §7, decision D5.
    /// </summary>
    public static IReadOnlyList<ISimSystem> CreatePhase0Systems() => new ISimSystem[]
    {
        new ClockSystem(),      // 1. advance the calendar, narrate season/year turns
        new AgeingSystem(),     // 2. age becomes declining vigour and life stage
        new HouseholdSystem(),  // 3. households grow
        new NeedsSystem(),      // 4. hunger rises
        new BehaviorSystem(),   // 5. decide and act
        new MortalitySystem(),  // 6. starvation or old age
    };

    /// <summary>Create a world and loop wired with the Phase 0 systems.</summary>
    public static SimLoop CreatePhase0(SimConfig config, ISimLogger? logger = null, ulong? seedOverride = null)
    {
        SimWorld world = SimWorld.Create(config, logger, seedOverride);
        return new SimLoop(world, CreatePhase0Systems());
    }
}
