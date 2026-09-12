using Bclone.Sim.World;

namespace Bclone.Sim.Core;

/// <summary>
/// A position in continuous space — <see cref="Fixed"/> in both axes (gridless 2b, D319).
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐⭐ THIS IS THE TYPE OPTION C TURNS ON.</b> `specs/gridless.md §7.3` chose *"the grid becomes
/// an index, not a constraint"*: terrain, soil and the cost field stay tile-indexed, while things
/// that are <em>placed</em> and things that <em>move</em> get real coordinates. <see cref="GridPos"/>
/// does not go away — it becomes the answer to *"which tile is this?"* rather than *"where is
/// this?"*, and <see cref="ToTile"/> is the conversion between the two.
/// </para>
/// <para>
/// <b>⛔ THE TILE CONVENTION, STATED ONCE SO NOTHING HAS TO GUESS IT.</b> Tile <c>(x, y)</c> covers
/// the square from <c>(x, y)</c> up to but not including <c>(x+1, y+1)</c>, and its centre is
/// <c>(x + ½, y + ½)</c>. So <see cref="ToTile"/> is a <b>floor</b>, which is exactly why
/// <see cref="Fixed"/> floors — *"two rounding conventions would put a seam right here"*
/// (`Fixed.cs`). A building's <c>Position</c> is still a <see cref="GridPos"/>, and its geometric
/// centre is that tile's centre.
/// </para>
/// <para>
/// ⚠️ <b>One distance, and it arrived with its slice.</b> <see cref="DistanceTo"/> is the
/// hypotenuse, through <see cref="Fixed.Sqrt"/> (floored), and it exists for exactly one reason:
/// clock B charges a leg the distance it actually is (D361, `specs/gridless.md §8` slice 5). Every
/// other distance in this sim is still the cost field's (`TravelCostField`) — catchment, routes,
/// "how far is that?" — and this must not become a second answer to those questions.
/// </para>
/// </remarks>
public readonly record struct Point(Fixed X, Fixed Y)
{
    public static Point Origin => new(Fixed.Zero, Fixed.Zero);

    /// <summary>The straight-line distance to another point, floored to 2⁻³² (D361).</summary>
    public Fixed DistanceTo(Point other)
    {
        Fixed dx = other.X - X;
        Fixed dy = other.Y - Y;
        return ((dx * dx) + (dy * dy)).Sqrt();
    }

    /// <summary>The centre of a tile — half a tile in from its corner on both axes.</summary>
    public static Point CentreOf(GridPos tile) =>
        new(Fixed.FromRatio((2 * tile.X) + 1, 2), Fixed.FromRatio((2 * tile.Y) + 1, 2));

    /// <summary>Which tile this point falls in.</summary>
    /// <remarks>
    /// A floor on both axes, which is the only correct tile index for a negative coordinate — and
    /// the valley straddles its own founding site, so negatives are ordinary here.
    /// </remarks>
    public GridPos ToTile() => new(X.ToInt(), Y.ToInt());

    public static Point operator +(Point left, Point right) =>
        new(left.X + right.X, left.Y + right.Y);

    public static Point operator -(Point left, Point right) =>
        new(left.X - right.X, left.Y - right.Y);

    /// <summary>
    /// This point turned about the origin.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ordinary rotation matrix, in fixed point:
    /// <c>x' = x·cos − y·sin</c>, <c>y' = x·sin + y·cos</c>.
    /// </para>
    /// <para>
    /// ⚠️ <b>Every product floors, and multiplication in <see cref="Fixed"/> is not associative</b>,
    /// so the order of these four products is part of the answer. **Do not reassociate them** —
    /// `FixedTests.MultiplicationIsNotAssociativeAndThatIsAHazardNotACuriosity` exists to make that
    /// concrete. The error is bounded by the sine table's own 4.7 × 10⁻⁶ (D318) and is far below a
    /// pixel at any zoom this game offers.
    /// </para>
    /// </remarks>
    public Point RotatedBy(Angle facing)
    {
        Fixed sin = facing.Sin();
        Fixed cos = facing.Cos();

        return new Point(
            (X * cos) - (Y * sin),
            (X * sin) + (Y * cos));
    }

    /// <summary>This point turned about <paramref name="pivot"/> rather than the origin.</summary>
    public Point RotatedAbout(Point pivot, Angle facing) =>
        pivot + (this - pivot).RotatedBy(facing);

    public override string ToString() => $"({X}, {Y})";
}
