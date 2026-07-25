namespace Bclone.Sim.Core;

/// <summary>
/// One unit of simulation behaviour, executed once per tick.
/// </summary>
/// <remarks>
/// <para>
/// Systems are run by <see cref="SimLoop"/> in registration order, single-threaded,
/// every tick. <b>That order is part of the determinism contract</b> — reordering
/// systems is a behavioural change and must be treated as one, not as a tidy-up.
/// </para>
/// <para>
/// A system must be a pure function of world state: no wall clock, no unseeded
/// randomness, no ambient I/O, no dependence on collection iteration order.
/// See specs/tick-loop.md §6.
/// </para>
/// </remarks>
public interface ISimSystem
{
    /// <summary>Name used in logs and inspectors. Keep it short and readable.</summary>
    string Name { get; }

    /// <summary>Advance this system by exactly one tick.</summary>
    void Execute(SimWorld world);
}
