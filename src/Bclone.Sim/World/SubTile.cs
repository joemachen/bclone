namespace Bclone.Sim.World;

/// <summary>
/// ⭐⭐ A quarter of a tile — <b>the resolution the player PAINTS at</b> (gridless, D335).
/// </summary>
/// <remarks>
/// <para>
/// <b>Joe, looking at a 5×5 round brush:</b> *"haha this is a circle????"* **He was right, and the
/// evidence is arithmetic rather than taste.** At five tiles across a square grid holds exactly
/// three shapes — a 13-tile diamond, a 21-tile square with its corners bitten out, and the full 25 —
/// and **none of them is a circle.** A disc only starts reading as one at about nine tiles across,
/// which is far bigger than anyone wants a default brush to be. *The information is not there.*
/// </para>
/// <para>
/// ⛔ <b>So the PAINT gets a finer grid than the GROUND.</b> Terrain, the travel-cost field and all
/// nineteen tile-keyed economy entries stay exactly where they are — <c>gridless.md §10.2</c> is
/// Joe's own closed call and this does not reopen it. What got finer is the decision about *where*,
/// which is the only thing he was ever looking at.
/// </para>
/// <para>
/// ⛔⛔ <b>ITS OWN TYPE, NOT A <see cref="GridPos"/> IN DIFFERENT UNITS.</b> D322's rule —
/// *one number answering two questions will report the wrong one* — and D38's method: a distinct
/// type makes the compiler enumerate every call site so each one gets a decision rather than a
/// rename. **A sub-tile that could be passed where a tile was meant is a bug nothing would catch**,
/// because both are a pair of small integers and both would look right in a log.
/// </para>
/// <para>
/// ⚠️ <b>Negative coordinates are ordinary here</b> — the valley straddles its own founding site —
/// so the conversion down to a tile is a <b>floor</b>, never a truncation. Truncation folds the
/// bucket at zero to twice the width of every other, which is the same reason <c>Fixed</c> floors
/// (D317) and <c>Point.ToTile</c> floors (D319).
/// </para>
/// </remarks>
public readonly record struct SubTile(int X, int Y)
{
    /// <summary>How many sub-tiles span one tile, per axis. Sixteen to a tile.</summary>
    /// <remarks>
    /// ⭐ <b>Four, and it is the smallest number that actually answers the complaint.</b> At two per
    /// axis a radius-2 brush is ten sub-tiles across and still visibly stepped; at four it is twenty
    /// and reads as round. ⚠️ **It is a constant rather than a dial on purpose**: it is in the seed
    /// contract through the hash, so changing it moves every golden — that is a decision, not a
    /// setting.
    /// </remarks>
    public const int PerTile = 4;

    /// <summary>How many sub-tiles cover one whole tile.</summary>
    public const int PerWholeTile = PerTile * PerTile;

    /// <summary>
    /// ⭐ Half a tile's worth, which is the threshold the whole design turns on.
    /// </summary>
    /// <remarks>
    /// <b>A tile counts as painted when at least half of its sixteen sub-tiles are.</b> One
    /// sentence, all three layers — and it is what lets every tile-level question in the sim keep
    /// its meaning and its numbers for any village that paints whole tiles, which is every village
    /// that exists today.
    /// </remarks>
    public const int HalfATile = PerWholeTile / 2;

    /// <summary>The tile this sub-tile falls in.</summary>
    public GridPos Tile => new(FloorDivide(X), FloorDivide(Y));

    /// <summary>Which of the sixteen it is within its tile, as (0..3, 0..3).</summary>
    public (int X, int Y) Within => (X - (FloorDivide(X) * PerTile), Y - (FloorDivide(Y) * PerTile));

    /// <summary>The first sub-tile of a tile — its top-left quarter.</summary>
    public static SubTile Of(GridPos tile) => new(tile.X * PerTile, tile.Y * PerTile);

    /// <summary>One of the sixteen sub-tiles of a tile.</summary>
    public static SubTile Of(GridPos tile, int withinX, int withinY) =>
        new((tile.X * PerTile) + withinX, (tile.Y * PerTile) + withinY);

    public override string ToString() => $"({X}, {Y})/{PerTile}";

    /// <summary>
    /// Divide by <see cref="PerTile"/>, rounding toward negative infinity.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>C#'s <c>/</c> truncates toward zero</b>, which would put sub-tile −1 in tile 0 alongside
    /// sub-tile 0 — a tile twice as wide as every other, straddling the origin, in a valley whose
    /// founding site is at the origin. *The same trap `Fixed` and `Point` each record.*
    /// </remarks>
    private static int FloorDivide(int value) =>
        value >= 0 ? value / PerTile : ((value + 1) / PerTile) - 1;
}
