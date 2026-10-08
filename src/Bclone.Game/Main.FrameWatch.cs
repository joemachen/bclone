using Bclone.Sim.Config;
using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐⭐ The whole frame on the debug line, and a timed sample of it (D528) — see <see cref="FrameWatch"/>.
/// </summary>
public partial class Main
{
    /// <summary>The first three seconds of a village are its own build, not play.</summary>
    private readonly FrameWatch _frameWatch = new(warmUpMs: 3000d);

    /// <summary>Close the frame before this one and log it if it ran long. Debug builds only print.</summary>
    private void TheFrameBegins()
    {
        double nowMs = System.Diagnostics.Stopwatch.GetTimestamp() * 1000d / System.Diagnostics.Stopwatch.Frequency;
        FrameWatch.Frame? longOne = _frameWatch.Begin(
            nowMs, _map.TakeTheDrawTime(), System.GC.CollectionCount(0), System.GC.CollectionCount(2));

        if (longOne is { } frame && OS.IsDebugBuild())
        {
            GD.Print($"[frame] long: {frame}");
        }
    }

    // ---------------------------------------------------------------
    //  The frame sample: BCLONE_FRAME_SAMPLE=<seconds>
    // ---------------------------------------------------------------

    /// <summary>
    /// Seconds to run a frame sample for, or 0 — <c>BCLONE_FRAME_SAMPLE</c>, with the speed in
    /// <c>BCLONE_FRAME_SAMPLE_SPEED</c> (1 if unset).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>So the frame can be measured on the machine Joe plays on without his hands.</b> The title and the
    /// new-game screen are passed by, the config's own valley is founded with the screen's defaults — four
    /// founders, unattended, the opening Joe saw skip — and the village runs in a real window for that many
    /// seconds; every long frame is printed as it closes, the summary at the end, then it quits.
    /// </para>
    /// <para>
    /// ⛔ <b>Nothing is written</b>: no autosave lands in the saves folder (one would show under
    /// <i>Continue</i>), and the player's settings are neither read nor written — <see cref="NothingIsWritten"/>,
    /// as under the probe. Diagnostic only: it changes nothing the game does (D160's line).
    /// </para>
    /// <para>
    /// ⚠️ <b>It needs a window.</b> Under <c>--headless</c> nothing is rendered, so the gap it measures is not
    /// the frame a player sees.
    /// </para>
    /// </remarks>
    private static double FrameSampleSeconds =>
        double.TryParse(
            System.Environment.GetEnvironmentVariable("BCLONE_FRAME_SAMPLE"),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out double seconds) && seconds > 0d
            ? seconds
            : 0d;

    /// <summary>True under the probe or a frame sample: no save, no settings file, read or written.</summary>
    private static bool NothingIsWritten => TheProbeIsRunning || FrameSampleSeconds > 0d;

    /// <summary>When the sample began, in the stopwatch's milliseconds; NaN when no sample runs.</summary>
    private double _sampleStartedMs = double.NaN;

    /// <summary>Found the config's own valley at the sample's speed — <c>_Ready</c>'s door when a sample is asked for.</summary>
    private void StartTheFrameSample(SimConfig config)
    {
        NewGameSettings defaults = NewGame.Defaults(config, config.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        FoundTheVillage(NewGame.Apply(config, defaults), NewGame.ShareCode(config, defaults), defaults.Values);

        double speed = double.TryParse(
            System.Environment.GetEnvironmentVariable("BCLONE_FRAME_SAMPLE_SPEED"),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out double asked) && asked > 0d
            ? asked
            : 1d;

        SetSpeed(speed);
        _sampleStartedMs = System.Diagnostics.Stopwatch.GetTimestamp() * 1000d / System.Diagnostics.Stopwatch.Frequency;
        GD.Print($"[frame] sample: {FrameSampleSeconds:0.#} s at {speed:0.#}x, {DisplayServer.WindowGetSize()} window, vsync {DisplayServer.WindowGetVsyncMode()}");
    }

    private void EndTheFrameSampleWhenDue()
    {
        if (double.IsNaN(_sampleStartedMs))
        {
            return;
        }

        double nowMs = System.Diagnostics.Stopwatch.GetTimestamp() * 1000d / System.Diagnostics.Stopwatch.Frequency;
        if (nowMs - _sampleStartedMs < FrameSampleSeconds * 1000d)
        {
            return;
        }

        GD.Print($"[frame] sample done at tick {_loop.World.Tick:N0}: {_frameWatch.Summary}");
        _sampleStartedMs = double.NaN;
        GetTree().Quit();
    }
}
