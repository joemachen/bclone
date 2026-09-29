using Bclone.Sim.Core;

namespace Bclone.Sim.World;

/// <summary>
/// A well — <b>where households walk for water</b> (D427, `specs/organic-housing.md §9.12`).
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐ ITS OWN KIND OF THING, FOR THE LIBRARY'S REASON.</b> A finished building becomes a store, a
/// workplace, a home, a library or the hall, and a well is none of them: it holds nothing, nobody
/// works there and nobody lives in it. What it does is be walked to — every household in reach sends
/// somebody on its water trip, and the lane their feet wear is the point (P6).
/// </para>
/// <para>
/// ⚠️ <b>No water yet.</b> Nothing is carried home and nothing is drunk; that is Phase 6, and this is
/// the type it attaches to.
/// </para>
/// </remarks>
public sealed class Well
{
    private Point _position;

    /// <summary>Where it stands. <c>init</c> for building it, <see cref="MoveTo"/> for moving it.</summary>
    public required Point Position { get => _position; init => _position = value; }

    /// <summary>Which tile this building is filed under — derived, never stored (D329).</summary>
    public GridPos Tile => _position.ToTile();

    /// <summary>Which way it is turned.</summary>
    public Angle Facing { get; init; }

    public int ExtentWidth { get; init; } = 1;

    public int ExtentHeight { get; init; } = 1;

    public Footprint Footprint => new()
    {
        Origin = _position,
        Width = ExtentWidth,
        Height = ExtentHeight,
        Facing = Facing,
    };

    /// <summary>Move it. Only a finished relocation may.</summary>
    internal void MoveTo(Point to) => _position = to;

    /// <summary>What the village calls it.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Which row it was raised from — <b>read from the catalogue, never assumed</b>, so a modder's
    /// pump reports its own kind to the finders rather than the shipped well's.
    /// </summary>
    public required BuildingKind Kind { get; init; }

    /// <summary>How many times somebody has drawn water here.</summary>
    /// <remarks>
    /// <b>History, and not hashed</b> — nothing in the sim reads it (D258's line for a library's
    /// finder). The card says it.
    /// </remarks>
    public int Draws { get; internal set; }
}
