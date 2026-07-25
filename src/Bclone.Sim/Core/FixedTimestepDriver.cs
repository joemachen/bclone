using Bclone.Sim.Config;
using Bclone.Sim.Logging;

namespace Bclone.Sim.Core;

/// <summary>
/// Converts elapsed real time into a whole number of ticks to run.
/// </summary>
/// <remarks>
/// <para>
/// This is the boundary between real time and sim time, and the only component in
/// the codebase that knows real time exists. Everything on the sim side of it
/// counts in ticks.
/// </para>
/// <para>
/// <b>Why the doubles at the boundary are safe.</b> A caller hands in a
/// floating-point delta, which looks like it contradicts "no floats in sim state" —
/// it does not. The driver can only decide <em>how many</em> times
/// <see cref="SimLoop.StepOnce"/> gets called; it cannot influence what happens
/// inside a tick. Float noise here changes pacing, never outcomes.
/// </para>
/// <para>
/// <b>Why the accumulator is nonetheless an integer.</b> The obvious implementation —
/// <c>while (acc &gt;= secondsPerTick) { acc -= secondsPerTick; ticks++; }</c> — is
/// wrong in a way that is easy to miss: 0.1 is not representable in binary, so
/// subtracting it 25 times from 2.5 does not land on zero, and the loop yields 24
/// ticks instead of 25. That error compounds every frame, so the game clock would
/// fall steadily behind real time. Accumulating in whole nanoseconds and taking a
/// single integer division removes the drift entirely.
/// </para>
/// <para>
/// <b>It does not read the clock.</b> <see cref="Advance"/> takes the delta as a
/// parameter, so the driver is a pure function and fully testable without a clock,
/// and the single real <c>_Process(delta)</c> read happens in the Godot view layer —
/// outside the sim assembly entirely.
/// </para>
/// </remarks>
public sealed class FixedTimestepDriver
{
    private const long NanosPerSecond = 1_000_000_000L;

    /// <summary>
    /// Ceiling on how much real time one <see cref="Advance"/> call may contribute,
    /// so a wild delta cannot overflow the integer accumulator. Anything this large
    /// is a stall that will be dropped by the spiral guard regardless.
    /// </summary>
    private const double MaxDeltaSeconds = 1_000_000.0;

    private readonly ISimLogger? _logger;
    private readonly long _nanosPerTick;
    private long _accumulatorNanos;
    private double _speedMultiplier = 1.0;

    public FixedTimestepDriver(SimConfig config, ISimLogger? logger = null)
        : this(ValidatedRate(config), config.MaxTicksPerFrame, logger)
    {
    }

    public FixedTimestepDriver(double targetTicksPerSecond, int maxTicksPerFrame, ISimLogger? logger = null)
    {
        if (targetTicksPerSecond <= 0.0 || double.IsNaN(targetTicksPerSecond) || double.IsInfinity(targetTicksPerSecond))
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetTicksPerSecond), "Must be a positive finite number.");
        }

        if (maxTicksPerFrame <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTicksPerFrame), "Must be greater than zero.");
        }

        _nanosPerTick = (long)Math.Round(NanosPerSecond / targetTicksPerSecond);
        if (_nanosPerTick < 1L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetTicksPerSecond),
                $"Rate is too high to represent: a tick would be under one nanosecond (got {targetTicksPerSecond}).");
        }

        SecondsPerTick = 1.0 / targetTicksPerSecond;
        MaxTicksPerFrame = maxTicksPerFrame;
        _logger = logger;
    }

    private static double ValidatedRate(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();
        return config.TargetTicksPerSecond;
    }

    /// <summary>Real seconds one tick represents at 1x speed.</summary>
    public double SecondsPerTick { get; }

    /// <summary>Ceiling on ticks per <see cref="Advance"/> call — the
    /// spiral-of-death guard.</summary>
    public int MaxTicksPerFrame { get; }

    /// <summary>
    /// Playback speed. <c>0</c> is pause, <c>1</c> normal, <c>4</c> four times as
    /// many ticks per second.
    /// </summary>
    /// <remarks>
    /// This scales <b>how many ticks run per real second</b> — never the size of a
    /// tick. Every tick is identical at every speed, so a run at 4x produces exactly
    /// the same history as a run at 1x. Scaling a delta into the sim instead would
    /// make each speed a different simulation, and determinism would be gone.
    /// </remarks>
    public double SpeedMultiplier
    {
        get => _speedMultiplier;
        set
        {
            if (value < 0.0 || double.IsNaN(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), $"Speed multiplier cannot be negative or NaN (got {value}).");
            }

            _speedMultiplier = value;
        }
    }

    /// <summary>True when playback is paused. The sim is simply not stepped —
    /// there is no "paused" flag inside sim logic.</summary>
    public bool IsPaused => _speedMultiplier == 0.0;

    /// <summary>
    /// Fractional progress toward the next tick, in <c>[0,1)</c>. Handed to the
    /// renderer so it can interpolate between the previous and current sim state.
    /// The sim never sees this.
    /// </summary>
    public double Alpha => (double)_accumulatorNanos / _nanosPerTick;

    /// <summary>Ticks dropped by the spiral guard over this driver's lifetime.
    /// Surfaced so a stuttering build is diagnosable rather than mysterious.</summary>
    public long DroppedTickCount { get; private set; }

    /// <summary>
    /// Accumulate elapsed real time and return how many whole ticks are owed.
    /// </summary>
    /// <param name="deltaSeconds">
    /// Real seconds since the last call. Passed in, never read from a clock.
    /// </param>
    /// <param name="simTick">Current sim tick, used only to stamp warnings.</param>
    /// <returns>Ticks to run, in <c>[0, MaxTicksPerFrame]</c>.</returns>
    public int Advance(double deltaSeconds, ulong simTick = 0UL)
    {
        if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds))
        {
            // A NaN delta would poison the accumulator permanently, and the sim
            // would silently stop advancing. Say so; do not swallow.
            _logger?.Log(simTick, LogLevel.Error, "driver",
                $"Ignoring non-finite frame delta ({deltaSeconds}).");
            return 0;
        }

        if (deltaSeconds < 0.0)
        {
            _logger?.Log(simTick, LogLevel.Warn, "driver",
                $"Ignoring negative frame delta ({deltaSeconds:F6}s).");
            return 0;
        }

        double scaledSeconds = deltaSeconds * _speedMultiplier;
        if (scaledSeconds > MaxDeltaSeconds)
        {
            scaledSeconds = MaxDeltaSeconds;
        }

        _accumulatorNanos += (long)Math.Round(scaledSeconds * NanosPerSecond);

        // One integer division, so no error accumulates no matter how long the
        // session runs. The remainder stays in the accumulator as whole
        // nanoseconds and carries into the next frame exactly.
        long owed = _accumulatorNanos / _nanosPerTick;

        if (owed > MaxTicksPerFrame)
        {
            // The frame took so long that catching up fully would make the next
            // frame slower still — the classic death spiral. Drop the backlog and
            // accept that sim time slipped behind real time.
            long dropped = owed - MaxTicksPerFrame;
            DroppedTickCount += dropped;
            _accumulatorNanos = 0L;

            _logger?.Log(simTick, LogLevel.Warn, "driver",
                $"Frame backlog exceeded {MaxTicksPerFrame} ticks; dropped {dropped} tick(s) to avoid a catch-up spiral.");

            return MaxTicksPerFrame;
        }

        _accumulatorNanos -= owed * _nanosPerTick;
        return (int)owed;
    }

    /// <summary>
    /// Discard accumulated time without running ticks. Use after a known stall
    /// (level load, breakpoint) so the first frame back does not lurch.
    /// </summary>
    public void ResetAccumulator() => _accumulatorNanos = 0L;
}
