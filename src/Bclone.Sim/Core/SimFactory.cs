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
        new CropSystem(),       // 2. and the ground answers it: autumn ripens, winter rots (D161)
        new AgeingSystem(),     // 3. age becomes declining vigour and life stage
        new HouseholdSystem(),  // 4. households grow
        new NeedsSystem(),      // 5. hunger rises
        new HearthSystem(),     // 6. homes burn firewood; cold homes chill their people
        new LabourSystem(),     // 7. villagers take work themselves
        new BehaviorSystem(),   // 8. decide and act
        new MortalitySystem(),  // 9. old age, starvation, or cold
        new RegrowthSystem(),   // 10. the valley grows back (D125)
        new SkillSystem(),      // 11. and the people who worked it got better at it (Phase 3)
    };

    // ⭐ WHY THE CROPS TURN AT STEP 2 AND NOT AT THE END (D161). The order is part of the
    // determinism contract (D5), so a new system's position is a decision rather than a
    // detail. It goes directly after the calendar because the causal sentence is *the season
    // turned, so the ground changed, so people acted on it* — which means a farmer reaching
    // BehaviorSystem on the first tick of autumn finds the fields already ripe and can reap
    // that same day. Placed after BehaviorSystem it would be a day late every year, and the
    // village would spend the first day of winter reaping a harvest that had already rotted.
    //
    // It is a no-op for every village that has never sown, so adding it moves no golden —
    // which is the property that let it ship before farming did.

    /// <summary>Create a world and loop wired with the Phase 0 systems.</summary>
    public static SimLoop CreatePhase0(SimConfig config, ISimLogger? logger = null, ulong? seedOverride = null)
    {
        SimWorld world = SimWorld.Create(config, logger, seedOverride);
        return new SimLoop(world, CreatePhase0Systems());
    }
}
