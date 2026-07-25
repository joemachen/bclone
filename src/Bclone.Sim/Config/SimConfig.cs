using System.Text.Json.Serialization;

namespace Bclone.Sim.Config;

/// <summary>
/// Tunables for a run, loaded from <c>data/sim.config.json</c>.
/// </summary>
/// <remarks>
/// Data-driven from day one (DESIGN.md §3) — nothing here is hardcoded, and a
/// modder is expected to edit the JSON. Immutable for the duration of a run:
/// config that changes mid-run is config that breaks replay.
/// </remarks>
public sealed record SimConfig
{
    /// <summary>
    /// Seed for the run. Same seed + same config + same tick count produces a
    /// byte-identical history.
    /// </summary>
    [JsonPropertyName("seed")]
    public ulong Seed { get; init; } = 12345UL;

    /// <summary>
    /// Ticks per in-game day. The only thing that gives a tick a "duration".
    /// </summary>
    [JsonPropertyName("ticks_per_day")]
    public int TicksPerDay { get; init; } = 4;

    /// <summary>
    /// Ticks per real second at 1x speed. <b>Playback only</b> — sim logic must
    /// never read this.
    /// </summary>
    [JsonPropertyName("target_ticks_per_second")]
    public double TargetTicksPerSecond { get; init; } = 10.0;

    /// <summary>
    /// Spiral-of-death guard: most ticks the driver will run for one frame.
    /// <b>Playback only</b> — sim logic must never read this.
    /// </summary>
    [JsonPropertyName("max_ticks_per_frame")]
    public int MaxTicksPerFrame { get; init; } = 250;

    /// <summary>
    /// Fail loudly on nonsense values rather than letting them cause a baffling
    /// symptom a thousand ticks later.
    /// </summary>
    /// <exception cref="SimConfigException">If any value is out of range.</exception>
    public void Validate()
    {
        if (TicksPerDay <= 0)
        {
            throw new SimConfigException($"ticks_per_day must be greater than zero (got {TicksPerDay}).");
        }

        if (TargetTicksPerSecond <= 0.0 || double.IsNaN(TargetTicksPerSecond) || double.IsInfinity(TargetTicksPerSecond))
        {
            throw new SimConfigException(
                $"target_ticks_per_second must be a positive finite number (got {TargetTicksPerSecond}).");
        }

        if (MaxTicksPerFrame <= 0)
        {
            throw new SimConfigException($"max_ticks_per_frame must be greater than zero (got {MaxTicksPerFrame}).");
        }
    }
}

/// <summary>Thrown when config is missing, malformed, or out of range.</summary>
public sealed class SimConfigException : Exception
{
    public SimConfigException(string message) : base(message) { }

    public SimConfigException(string message, Exception innerException) : base(message, innerException) { }
}
