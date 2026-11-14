namespace Bclone.Sim.World;

/// <summary>
/// How steep a tile's ground is — three classes, not a continuous number, so a player can learn them by sight and
/// <i>What's here</i> can say which (`specs/heights.md §3`, D735).
/// </summary>
public enum Slope
{
    /// <summary>No step to any neighbour: the walk's own pace.</summary>
    Flat = 0,

    /// <summary>A step of one level: a little slower to cross.</summary>
    Gentle = 1,

    /// <summary>A step of two levels or more: about half again as long to cross.</summary>
    Steep = 2,
}

/// <summary>The slope classes' one rule (`heights.md §3`).</summary>
public static class Slopes
{
    /// <summary>
    /// A tile's class from its <see cref="GeneratedMap.SteepnessAt"/>: steep at <paramref name="steepAt"/> or more,
    /// gentle at <paramref name="gentleAt"/> or more, flat below (<c>slope_gentle_at</c>, <c>slope_steep_at</c>).
    /// </summary>
    public static Slope ClassOf(int steepness, int gentleAt, int steepAt) =>
        steepness >= steepAt ? Slope.Steep
        : steepness >= gentleAt ? Slope.Gentle
        : Slope.Flat;
}
