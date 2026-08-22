namespace Bclone.Sim.World;

/// <summary>
/// Where the player has said the village may build homes (D42), and which ground each
/// workplace has been given to work (D86).
/// </summary>
/// <remarks>
/// <para>
/// <b>Intent painted over ground, which the village acts on when it has a reason
/// to.</b> A residential area with no housing shortage produces nothing, and that is
/// not the brush failing — it is the brush working. The player decides the
/// neighbourhood; the sim still decides which tile, because
/// <see cref="Household.ChooseSite"/> knows about the walk to work and the walk to the
/// store and a cursor does not.
/// </para>
/// <para>
/// This is what let placement be handed over without giving up the guarantee the food
/// economy rests on. Per-house placement would have broken
/// <see cref="VillageEconomy.MaxHomeToWorkTiles"/> — a bound the <em>sim</em> keeps —
/// whereas a zone merely narrows where `ChooseSite` may look. The bound survives, and
/// the warning about a bad neighbourhood happens once, when the area is painted,
/// instead of on every house.
/// </para>
/// <para>
/// <b>Sim state, therefore hashed and deterministic.</b> A zone is a decision the
/// player made; two runs given the same decisions must produce the same village.
/// </para>
/// <para>
/// <b>Two shapes, on purpose (D86, Joe).</b> Residential ground is <em>global</em> — it
/// belongs to the village, and D42's whole division is that the player picks the
/// neighbourhood while the sim picks the tile. Work ground is <em>owned by a building</em>:
/// a forester's hut is given land to keep, and *"who fells here?"* has to have an answer,
/// because the labour allocator is built entirely around workplaces with a catchment
/// (D21–D25) and a zone belonging to nobody contains no workplace.
/// </para>
/// <para>
/// <b>One general "layer per owner" model would have been less code and a worse
/// description.</b> Residential owned by nobody is a null that every reader would have to
/// interpret, and it would quietly permit a second hut to be given the same ground. Two
/// shapes say what is true.
/// </para>
/// </remarks>
public sealed class ZoneMap
{
    private readonly bool[] _residential;

    /// <summary>Which building owns each tile's work, or 0 for none.</summary>
    /// <remarks>
    /// <b>One owner per tile, and that is the rule rather than a limitation.</b> Two huts
    /// sharing ground would have two crews felling the same trees, and the village could not
    /// say which of them a stump belonged to — which is the *"right stuff in the wrong
    /// place"* shape that has cost this project four investigations (D25, D29, D48, D57).
    /// </remarks>
    private readonly int[] _workGround;

    /// <summary>Tiles per owner. Derived from <see cref="_workGround"/>, never hashed.</summary>
    /// <remarks>
    /// Kept only so that *"is this hut overstretched?"* is a question the view can ask every
    /// frame. It is <b>not</b> state: the array is the truth, and a dictionary's iteration
    /// order must never reach the hash (D2, and D51's trap one door along).
    /// </remarks>
    private readonly Dictionary<int, int> _tilesByOwner = new();

    /// <summary>Ground the village means to clear of whatever is standing on it (D87).</summary>
    /// <remarks>
    /// <para>
    /// <b>Global and owned by nobody</b>, which makes this the third shape in here and is not
    /// untidiness. Residential belongs to the <em>village</em>; work ground belongs to a
    /// <em>building</em>; harvest belongs to <em>no one</em>, because the people who do it are
    /// <see cref="Villager.IsLaborer"/> — able adults no workplace wants — and there is no
    /// workplace for the paint to hang off.
    /// </para>
    /// <para>
    /// <b>Painting harvest is taking; a forester's ground is keeping.</b> One is a decision to
    /// spend what is standing there, the other is a decision to farm it. Trees are the one
    /// resource that can be either, and that is a choice the player makes rather than a
    /// contradiction (D87, D84).
    /// </para>
    /// </remarks>
    private readonly bool[] _harvest;

    private readonly int _width;
    private readonly int _height;
    private readonly int _minX;
    private readonly int _minY;

    public ZoneMap(GeneratedMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        _width = map.Width;
        _height = map.Height;
        _minX = map.MinX;
        _minY = map.MinY;
        _residential = new bool[_width * _height];
        _workGround = new int[_width * _height];
        _harvest = new bool[_width * _height];
    }

    /// <summary>How many tiles are painted for housing.</summary>
    public int ResidentialTiles { get; private set; }

    /// <summary>Whether the village may put a home on this tile.</summary>
    public bool IsResidential(GridPos position)
    {
        int index = IndexOf(position);
        return index >= 0 && _residential[index];
    }

    /// <summary>Paint or erase one tile. Returns true if it changed anything.</summary>
    public bool SetResidential(GridPos position, bool painted)
    {
        int index = IndexOf(position);
        if (index < 0 || _residential[index] == painted)
        {
            return false;
        }

        _residential[index] = painted;
        ResidentialTiles += painted ? 1 : -1;
        return true;
    }

    /// <summary>Every painted tile, in a fixed order — for hashing and for drawing.</summary>
    public IReadOnlyList<bool> Residential => _residential;

    // ---------------------------------------------------------------
    //  Work ground (D86)
    // ---------------------------------------------------------------

    /// <summary>Which building has been given this tile to work, or 0 for none.</summary>
    public int WorkGroundOwner(GridPos position)
    {
        int index = IndexOf(position);
        return index < 0 ? 0 : _workGround[index];
    }

    /// <summary>How much ground a building has been given.</summary>
    public int WorkGroundTiles(int ownerId) =>
        _tilesByOwner.TryGetValue(ownerId, out int tiles) ? tiles : 0;

    /// <summary>
    /// The tiles a building holds, as indices in map order — so nobody walks the valley to
    /// find them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The same lesson `PaintedHarvest` was just taught, applied before it costs anything</b>
    /// (`forests-and-gathering.md`). A forester asks *"which of my tiles do I work next?"* on
    /// every trip; answering it by scanning 9,600 tiles is exactly the shape that took the suite
    /// from six minutes to eleven, and it is cheaper to not write it than to find it later.
    /// </para>
    /// <para>
    /// <b>In map order, and never hashed</b> — the owner array is the state and this is an
    /// index into it, maintained in the same two methods that maintain the counts so there is
    /// no third place for them to disagree.
    /// </para>
    /// </remarks>
    public IReadOnlyList<int> WorkGroundOf(int ownerId) =>
        _groundByOwner.TryGetValue(ownerId, out List<int>? tiles) ? tiles : Array.Empty<int>();

    private readonly Dictionary<int, List<int>> _groundByOwner = new();

    /// <summary>
    /// Give one tile to a building, or take it back with <paramref name="ownerId"/> 0.
    /// Returns whether it changed anything.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ground already owned by a different building is left alone</b> rather than taken —
    /// the caller gets <c>false</c> and can say whose it is. Silently stealing it would let a
    /// careless drag unstaff a hut on the other side of the valley, and the player would have
    /// no way to see that they had.
    /// </para>
    /// <para>
    /// <b>Refusal is per tile and quiet, because painting is a drag.</b> A sentence per tile
    /// would be forty sentences per stroke; D42 settled that shape already — the village
    /// speaks <em>once per stroke</em>, and the caller is what counts the refusals up.
    /// </para>
    /// </remarks>
    public bool SetWorkGround(GridPos position, int ownerId)
    {
        int index = IndexOf(position);
        if (index < 0)
        {
            return false;
        }

        int current = _workGround[index];
        if (current == ownerId)
        {
            return false;
        }

        // Somebody else's ground. Theirs to give up, not ours to take.
        if (current != 0 && ownerId != 0)
        {
            return false;
        }

        _workGround[index] = ownerId;

        if (current != 0)
        {
            _tilesByOwner[current] = _tilesByOwner[current] - 1;
            if (_tilesByOwner[current] == 0)
            {
                _tilesByOwner.Remove(current);
            }

            if (_groundByOwner.TryGetValue(current, out List<int>? had))
            {
                int at = had.BinarySearch(index);
                if (at >= 0)
                {
                    had.RemoveAt(at);
                }

                if (had.Count == 0)
                {
                    _groundByOwner.Remove(current);
                }
            }
        }

        if (ownerId != 0)
        {
            _tilesByOwner[ownerId] = WorkGroundTiles(ownerId) + 1;

            if (!_groundByOwner.TryGetValue(ownerId, out List<int>? tiles))
            {
                tiles = new List<int>();
                _groundByOwner[ownerId] = tiles;
            }

            // Kept in index order, which is map order — see WorkGroundOf.
            int insertAt = tiles.BinarySearch(index);
            tiles.Insert(insertAt < 0 ? ~insertAt : insertAt, index);
        }

        return true;
    }

    /// <summary>
    /// Give up every tile a building held. Returns how many were freed.
    /// </summary>
    /// <remarks>
    /// <b>Demolition has to reach this or the ground is haunted.</b> A hut pulled down while
    /// still owning forty tiles would leave land no other hut could ever be given, refused by
    /// a building that no longer exists — and the refusal would name nothing, because there is
    /// nothing left to name.
    /// </remarks>
    public int ReleaseWorkGround(int ownerId)
    {
        if (ownerId == 0 || !_tilesByOwner.ContainsKey(ownerId))
        {
            return 0;
        }

        int freed = 0;
        for (int i = 0; i < _workGround.Length; i++)
        {
            if (_workGround[i] == ownerId)
            {
                _workGround[i] = 0;
                freed++;
            }
        }

        _tilesByOwner.Remove(ownerId);
        _groundByOwner.Remove(ownerId);
        return freed;
    }

    /// <summary>Every tile's owner, in a fixed order — for hashing and for drawing.</summary>
    public IReadOnlyList<int> WorkGround => _workGround;

    // ---------------------------------------------------------------
    //  Harvest — what the village means to clear (D87)
    // ---------------------------------------------------------------

    /// <summary>How many tiles are painted to be harvested.</summary>
    public int HarvestTiles { get; private set; }

    /// <summary>Whether the village means to take what is standing on this tile.</summary>
    public bool IsHarvest(GridPos position)
    {
        int index = IndexOf(position);
        return index >= 0 && _harvest[index];
    }

    /// <summary>Paint or erase one tile. Returns true if it changed anything.</summary>
    public bool SetHarvest(GridPos position, bool painted)
    {
        int index = IndexOf(position);
        if (index < 0 || _harvest[index] == painted)
        {
            return false;
        }

        _harvest[index] = painted;
        HarvestTiles += painted ? 1 : -1;

        if (painted)
        {
            // Kept in index order, which is map order — see PaintedHarvest.
            int at = _paintedHarvest.BinarySearch(index);
            _paintedHarvest.Insert(at < 0 ? ~at : at, index);
        }
        else
        {
            int at = _paintedHarvest.BinarySearch(index);
            if (at >= 0)
            {
                _paintedHarvest.RemoveAt(at);
            }
        }

        return true;
    }

    /// <summary>
    /// The painted tiles themselves, in map order — so nobody has to walk the valley to find
    /// them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠️ THIS IS D87'S TRAP, RE-ARMED BY A WOODED VALLEY.</b> `SimWorld.NearestHarvest` is
    /// asked by every idle adult every tick and used to scan all 9,600 tiles, guarded by an
    /// early-out for a village that had painted nothing — which was almost every village, so
    /// the scan was free in practice. **Then the valley became woodland**
    /// (`forests-and-gathering.md` slice 1): every house the village sites now lands on trees
    /// and paints itself for clearing (D100), so the early-out stopped firing for anybody and
    /// the full scan came back. **Measured: the twelve-seed arm went from about three minutes
    /// to six, and the suite from six to ten and a half.** That is the same regression D87
    /// records, arriving from worldgen rather than from the brush.
    /// </para>
    /// <para>
    /// <b>Kept in index order, which is map order (row by row, left to right)</b> — exactly the
    /// order the old scan visited tiles in. That is not tidiness: `NearestHarvest` keeps the
    /// first tile of the lowest cost, so a different order would break ties differently and
    /// move every golden for a change that is supposed to be a pure speed-up.
    /// </para>
    /// <para>
    /// <b>Derived from <c>_harvest</c> and never hashed.</b> The bool array is the state; this
    /// is an index into it, and two of anything that must agree is the shape of half the bugs
    /// in this project — so it is written in exactly one place, immediately beside the array
    /// itself.
    /// </para>
    /// </remarks>
    public IReadOnlyList<int> PaintedHarvest => _paintedHarvest;

    private readonly List<int> _paintedHarvest = new();

    /// <summary>Every painted tile, in a fixed order — for hashing and for drawing.</summary>
    public IReadOnlyList<bool> Harvest => _harvest;

    private int IndexOf(GridPos position)
    {
        int x = position.X - _minX;
        int y = position.Y - _minY;

        return x < 0 || x >= _width || y < 0 || y >= _height ? -1 : (y * _width) + x;
    }

    /// <summary><see cref="IndexOf"/> run backwards — the tile an index refers to.</summary>
    /// <remarks>
    /// Beside its inverse deliberately: two conversions between a position and an index that
    /// live apart are two that can disagree, and every painted tile in the game is round-tripped
    /// through this pair.
    /// </remarks>
    public GridPos PositionOf(int index) =>
        new((index % _width) + _minX, (index / _width) + _minY);
}
