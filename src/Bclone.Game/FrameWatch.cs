using System.Globalization;

namespace Bclone.Game;

/// <summary>
/// ⭐⭐ The whole frame, measured — what each frame cost from one <c>_Process</c> to the next, and what
/// it was doing (D528, the movement skip).
/// </summary>
/// <remarks>
/// <para>
/// <b>Joe, playing D525:</b> *"slice/movement-skip still has skipping. all 4 founders skip at the same
/// time it seems."* A skip that moves every dot in the same instant cannot be any one villager's: the
/// glide is wall-clock (<c>FixedTimestepDriver.Alpha</c>), so a frame that runs long freezes everybody
/// for its length and the next frame moves them all forward together.
/// </para>
/// <para>
/// ⛔ <b>THE INSTRUMENT D403 READ TIMED HALF OF THE FRAME.</b> <c>slowest step</c> wraps the sim step
/// alone and <c>catch-up</c> counts only frames that carried two ticks; the map's draw passes are
/// smoothed nine-tenths old to one-tenth new, so a single long frame vanishes from them. A frame made
/// long by anything else was invisible to all three. This counts the gap between frames itself.
/// </para>
/// <para>
/// ⚠️ <b>A gap is charged to the frame BEFORE it.</b> The time between <c>_Process</c> N−1 and N is
/// N−1's own work, its draw (which runs after its <c>_Process</c>) and the wait for the screen — so the
/// frame is closed when the next one begins, and what it did (ticks, step, refresh, draw, collections) is
/// read back then. Charging the gap to the frame it was measured in blames the wrong frame every time.
/// </para>
/// <para>
/// Pure, and given its clock: <see cref="Begin"/> takes the time rather than reading it, so the probe can
/// pose a run of frames and ask where the long one landed (<see cref="SelfCheck"/>).
/// </para>
/// </remarks>
internal sealed class FrameWatch
{
    /// <summary>A frame longer than this is two frames at 60 Hz — every dot visibly stops for one.</summary>
    internal const double LongMs = 1000.0 / 30.0;

    /// <summary>What one frame did, closed when the next began.</summary>
    internal readonly record struct Frame(
        double GapMs, int Ticks, ulong FirstTick, bool DayStart, bool SeasonTurn,
        double StepMs, double RefreshMs, double DrawMs, int Gen0, int Gen2)
    {
        /// <summary>The gap nobody timed: rendering, the GPU, the wait for the screen, the rest of the engine.</summary>
        public double OtherMs => System.Math.Max(0d, GapMs - StepMs - RefreshMs - DrawMs);

        public override string ToString()
        {
            string when = Ticks == 0 ? "no tick"
                : $"{Ticks} tick{(Ticks == 1 ? string.Empty : "s")} from {FirstTick.ToString("N0", CultureInfo.InvariantCulture)}"
                    + (SeasonTurn ? ", a season's turn" : DayStart ? ", a day's start" : string.Empty);
            return $"{GapMs:F0}ms, {when}: step {StepMs:F1} · refresh {RefreshMs:F1} · draw {DrawMs:F1} · other {OtherMs:F1}"
                + (Gen0 + Gen2 > 0 ? $" · gc0 {Gen0}, gc2 {Gen2}" : string.Empty);
        }
    }

    /// <param name="warmUpMs">Frames that begin sooner than this after the first are not counted — the founding's own build.</param>
    internal FrameWatch(double warmUpMs) => _warmUpMs = warmUpMs;

    private readonly double _warmUpMs;

    private bool _open;
    private double _firstMs = double.NaN;
    private double _openedMs;
    private int _ticks;
    private ulong _firstTick;
    private bool _dayStart;
    private bool _seasonTurn;
    private double _stepMs;
    private double _refreshMs;
    private int _gen0At;
    private int _gen2At;

    private long _frames;
    private long _long;
    private long _longWithATick;
    private long _longAtADayStart;
    private long _framesWithATick;
    private double _gapWithATick;
    private double _gapWithout;

    /// <summary>The longest frame counted, or null before one is.</summary>
    internal Frame? Longest { get; private set; }

    internal long Frames => _frames;

    internal long LongFrames => _long;

    internal long LongWithATick => _longWithATick;

    internal long LongAtADayStart => _longAtADayStart;

    /// <summary>
    /// A frame begins: close the one before it — <paramref name="drawMs"/> is the map's draw that ran after
    /// its <c>_Process</c> — and open this one. Returns the closed frame when it was long, for the log.
    /// </summary>
    internal Frame? Begin(double nowMs, double drawMs, int gen0, int gen2)
    {
        Frame? longOne = null;
        if (_open)
        {
            var closed = new Frame(
                nowMs - _openedMs, _ticks, _firstTick, _dayStart, _seasonTurn,
                _stepMs, _refreshMs, drawMs, gen0 - _gen0At, gen2 - _gen2At);

            if (double.IsNaN(_firstMs))
            {
                _firstMs = _openedMs;
            }

            if (_openedMs - _firstMs >= _warmUpMs && Count(closed))
            {
                longOne = closed;
            }
        }

        _open = true;
        _openedMs = nowMs;
        _ticks = 0;
        _dayStart = false;
        _seasonTurn = false;
        _stepMs = 0d;
        _refreshMs = 0d;
        _gen0At = gen0;
        _gen2At = gen2;
        return longOne;
    }

    /// <summary>The open frame stepped the sim: <paramref name="ticks"/> ticks from <paramref name="firstTick"/>, in <paramref name="ms"/>.</summary>
    internal void Stepped(int ticks, ulong firstTick, double ms, int ticksPerDay, int ticksPerSeason)
    {
        _firstTick = firstTick;
        _ticks = ticks;
        _stepMs = ms;
        for (ulong t = firstTick; t < firstTick + (ulong)ticks; t++)
        {
            _dayStart |= t % (ulong)ticksPerDay == 0UL;
            _seasonTurn |= t % (ulong)ticksPerSeason == 0UL;
        }
    }

    /// <summary>The open frame's <c>Refresh()</c> — panels, cards, the roster and the map's present.</summary>
    internal void Refreshed(double ms) => _refreshMs = ms;

    /// <summary>True when the frame was long.</summary>
    private bool Count(Frame frame)
    {
        _frames++;
        if (frame.Ticks > 0)
        {
            _framesWithATick++;
            _gapWithATick += frame.GapMs;
        }
        else
        {
            _gapWithout += frame.GapMs;
        }

        if (Longest is null || frame.GapMs > Longest.Value.GapMs)
        {
            Longest = frame;
        }

        if (frame.GapMs < LongMs)
        {
            return false;
        }

        _long++;
        if (frame.Ticks > 0)
        {
            _longWithATick++;
        }

        if (frame.DayStart)
        {
            _longAtADayStart++;
        }

        return true;
    }

    /// <summary>The debug line's second row: how many frames ran long, what they were doing, and the longest.</summary>
    internal string Summary
    {
        get
        {
            if (_frames == 0)
            {
                return "frames: warming up";
            }

            long without = _frames - _framesWithATick;
            string meanWith = _framesWithATick == 0 ? "–" : $"{_gapWithATick / _framesWithATick:F1}";
            string meanWithout = without == 0 ? "–" : $"{_gapWithout / without:F1}";
            return $"frames {_frames:N0} · over {LongMs:F0}ms: {_long:N0} ({_longWithATick:N0} stepped a tick, {_longAtADayStart:N0} at a day's start)"
                + $" · mean gap with a tick {meanWith}ms, without {meanWithout}ms · longest {Longest}";
        }
    }

    /// <summary>
    /// ⭐ The probe's <c>frames:</c> line — a posed run of frames, asked where its long ones were charged.
    /// </summary>
    /// <remarks>
    /// The pose is the shape of the bug: a frame that steps a day's first tick and then waits, then a short
    /// frame, then a long frame that stepped nothing. The first long gap must be charged to the stepping frame
    /// (not the one it was measured in), at a day's start, with what nobody timed left as <c>other</c>.
    /// </remarks>
    internal static string SelfCheck()
    {
        var watch = new FrameWatch(warmUpMs: 0d);
        var faults = new List<string>();

        watch.Begin(0d, 0d, 0, 0);
        watch.Stepped(1, 8UL, 40d, ticksPerDay: 4, ticksPerSeason: 60);
        watch.Refreshed(5d);

        // The stepping frame closes 60 ms later, its draw 3 ms and one collection inside it.
        Frame? first = watch.Begin(60d, 3d, 1, 0);
        watch.Refreshed(2d);
        Frame? second = watch.Begin(76d, 3d, 1, 0);
        watch.Refreshed(2d);
        Frame? third = watch.Begin(120d, 3d, 1, 0);

        if (first is not { Ticks: 1, DayStart: true, SeasonTurn: false, Gen0: 1 } f || System.Math.Abs(f.OtherMs - 12d) > 1e-9)
        {
            faults.Add($"the stepping frame's gap was charged as {first?.ToString() ?? "short"}");
        }

        if (second is not null)
        {
            faults.Add($"a 16 ms frame was called long ({second})");
        }

        if (third is not { Ticks: 0, DayStart: false })
        {
            faults.Add($"the long frame that stepped nothing was charged as {third?.ToString() ?? "short"}");
        }

        if (watch.Frames != 3 || watch.LongFrames != 2 || watch.LongWithATick != 1 || watch.LongAtADayStart != 1)
        {
            faults.Add($"counted {watch.Frames} frames, {watch.LongFrames} long, {watch.LongWithATick} with a tick, {watch.LongAtADayStart} at a day's start (want 3, 2, 1, 1)");
        }

        if (watch.Longest is not { GapMs: 60d })
        {
            faults.Add($"the longest is {watch.Longest?.ToString() ?? "none"}, not the 60 ms one");
        }

        return faults.Count == 0
            ? "[widths] frames: ✅ a long gap is charged to the frame before it — the 60 ms after a day's first tick reads as that tick's, with 12 ms nobody timed"
            : $"[widths] frames: ❌ {string.Join("; ", faults)}";
    }
}
