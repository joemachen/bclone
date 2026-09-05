namespace Bclone.Sim.World;

/// <summary>
/// Where the game has been hunted out, and when it comes back — <b>sparse, and empty in a
/// village with no lodge</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Joe's design in one sentence:</b> fishing *"does not run out"* and hunting does. That
/// contrast is the whole point of having both — <em>a fishery is reliable and modest, a lodge is
/// rich and exhaustible</em> — and D3057 chose hunting over livestock partly for exactly this,
/// *"depletion as a §2.3 pressure"*.
/// </para>
/// <para>
/// ⛔⛔ <b>IT DOES NOT REUSE THE FOREST-EXHAUSTION MACHINERY, AND `specs/hunting.md §4` WAS WRONG
/// TO SAY IT SHOULD.</b> That machinery is <c>ThinTheRingOf</c>, which turns a
/// <c>Terrain.Forest</c> tile into a <c>Terrain.Sapling</c> — i.e. **it fells the tree**. Hunting
/// through it would have made a hunter into a logger and put lodges back into competition with
/// forager huts over wood, *which is the exact thing <c>HuntingRadius</c> exists to prevent*
/// (D292). **Game is a second quantity on the same ground, not a smaller forest.**
/// </para>
/// <para>
/// ⚠️ <b>Sparse by construction, because the state hash demands it.</b> A tile is in this list
/// only while it is hunted out, and <see cref="Recover"/> removes it the moment the game is back
/// — so a village that never builds a lodge holds an empty list and hashes byte-identically to
/// one from before hunting existed (D291's rule, applied on the way in rather than retrofitted).
/// </para>
/// </remarks>
public sealed class GameRange
{
    /// <summary>Hunted-out tiles, kept in index order so the hash is stable.</summary>
    private readonly List<int> _tiles = new();

    /// <summary>The tick each of those tiles has its game back, index-aligned with the above.</summary>
    private readonly List<ulong> _returns = new();

    /// <summary>How many tiles are hunted out right now.</summary>
    public int Count => _tiles.Count;

    /// <summary>The hunted-out tile at this slot, in map-index order.</summary>
    public int TileAt(int slot) => _tiles[slot];

    /// <summary>When the tile at this slot has its game back.</summary>
    public ulong ReturnsAt(int slot) => _returns[slot];

    /// <summary>Whether this tile has been hunted out and has not recovered.</summary>
    public bool IsHuntedOut(int tileIndex) => _tiles.BinarySearch(tileIndex) >= 0;

    /// <summary>
    /// Mark a tile hunted out until <paramref name="until"/>. Returns false if it already was.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>An already-hunted tile is NOT re-stamped with a later return.</b> Otherwise a lodge
    /// working the same wood every day would push its recovery permanently into the future and
    /// the range would never come back at all — <em>exhaustible is the design; permanently dead
    /// is not</em>.
    /// </remarks>
    public bool HuntOut(int tileIndex, ulong until)
    {
        int at = _tiles.BinarySearch(tileIndex);
        if (at >= 0)
        {
            return false;
        }

        int insert = ~at;
        _tiles.Insert(insert, tileIndex);
        _returns.Insert(insert, until);
        return true;
    }

    /// <summary>Give back every tile whose game has returned by this tick.</summary>
    public int Recover(ulong tick)
    {
        int given = 0;
        for (int i = _tiles.Count - 1; i >= 0; i--)
        {
            if (_returns[i] > tick)
            {
                continue;
            }

            _tiles.RemoveAt(i);
            _returns.RemoveAt(i);
            given++;
        }

        return given;
    }
}
