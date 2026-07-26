using System.Text.Json.Serialization;

namespace Bclone.Sim.Config;

/// <summary>A place on the map, as it appears in a data file.</summary>
/// <remarks>
/// Deliberately its own type rather than a bare pair, so a content file reads
/// <c>{ "x": -7, "y": -5 }</c> and a modder adding a site does not have to guess
/// which number comes first.
/// </remarks>
public sealed record SitePosition
{
    [JsonPropertyName("x")]
    public int X { get; init; }

    [JsonPropertyName("y")]
    public int Y { get; init; }
}
