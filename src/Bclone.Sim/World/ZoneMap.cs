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

    // ⭐⭐ THE SUB-TILE ARRAYS ARE THE STATE; THE TILE ARRAYS ABOVE ARE A SUMMARY OF THEM (D335).
    // `specs/sub-tile-zones.md`. Joe, looking at a 5×5 round brush: *"haha this is a circle????"* —
    // and he was right, because at five tiles across a square grid holds a diamond, a
    // square-with-bitten-corners, or a square, and **none of them is a circle**. The paint got a
    // finer grid; the ground did not.
    //
    // ⛔ **The summaries are maintained incrementally and never recomputed on read.** `IsHarvest`
    // is called inside scans that D179 already had to rescue once; folding sixteen sub-tiles per
    // call would be a sixteen-fold cost in exactly that path. *Every `Set` pays a little so every
    // read pays nothing.*
    private readonly bool[] _residentialSub;

    private readonly int[] _workGroundSub;

    private readonly bool[] _harvestSub;

    /// <summary>How many of each tile's sixteen sub-tiles are painted. Derived, never hashed.</summary>
    private readonly byte[] _residentialCount;

    private readonly byte[] _workGroundCount;

    private readonly byte[] _harvestCount;

    private readonly int _width;
    private readonly int _height;
    private readonly int _minX;
    private readonly int _minY;
    private readonly int _subWidth;
    private readonly int _subHeight;

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

        _subWidth = _width * SubTile.PerTile;
        _subHeight = _height * SubTile.PerTile;
        _residentialSub = new bool[_subWidth * _subHeight];
        _workGroundSub = new int[_subWidth * _subHeight];
        _harvestSub = new bool[_subWidth * _subHeight];
        _residentialCount = new byte[_width * _height];
        _workGroundCount = new byte[_width * _height];
        _harvestCount = new byte[_width * _height];
    }

    /// <summary>How wide the sub-tile grid is — for the hash and the renderer.</summary>
    public int SubWidth => _subWidth;

    /// <summary>Every painted sub-tile, in a fixed order — the state, hashed and drawn.</summary>
    public IReadOnlyList<bool> ResidentialSub => _residentialSub;

    /// <summary>Every sub-tile's owner, in a fixed order — the state, hashed and drawn.</summary>
    public IReadOnlyList<int> WorkGroundSub => _workGroundSub;

    /// <summary>Every marked sub-tile, in a fixed order — the state, hashed and drawn.</summary>
    public IReadOnlyList<bool> HarvestSub => _harvestSub;

    /// <summary>
    /// ⭐⭐ How many of a tile's sixteen sub-tiles each layer has painted — <b>so the
    /// renderer can skip a tile without asking sixteen questions about it</b> (D338).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔⛔ <b>THIS EXISTS BECAUSE THE COUNTS WERE ALREADY HERE AND THE VIEW COULD NOT SEE
    /// THEM.</b> D336 turned the wash into a sub-tile pass and it became <b>eighty per cent of every
    /// loop iteration the map runs</b> — ~88,000 a frame — for a valley that is mostly
    /// unpainted. **The arrays that answer "is there anything on this tile at all?" in one byte were
    /// three lines up, private.** *Joe: "the framerate feels A LOT more sluggish."*
    /// </para>
    /// <para>
    /// ⚠️ <b>These are NOT <see cref="IsResidential"/> and <see cref="IsHarvest"/>, and
    /// the difference is the whole point.</b> Those mean *"at least half"* — the threshold the
    /// economy asks in — so a tile with one quarter painted answers <c>false</c> to them **and
    /// still has paint to draw.** A renderer that skipped on those would erase the ragged edge the
    /// sub-tiles exist for.
    /// </para>
    /// <para>
    /// ⭐ <b>Derived, never hashed</b>, like the arrays behind them and like <see cref="Edits"/>
    /// — a count of painted quarters is a restatement of the sub-tile arrays, not a second fact
    /// about the village.
    /// </para>
    /// <para>
    /// ⭐ <b>A full count means one owner, for work ground too.</b> A sub-tile may only be given
    /// to the owner its tile already has (see <see cref="SetWorkGround(SubTile, int)"/>), so sixteen
    /// owned quarters are sixteen quarters owned by <see cref="WorkGroundOwner"/> — which is
    /// what lets the renderer collapse a whole tile into one rectangle.
    /// </para>
    /// </remarks>
    public int ResidentialSubTilesOn(GridPos tile)
    {
        int index = IndexOf(tile);
        return index < 0 ? 0 : _residentialCount[index];
    }

    /// <inheritdoc cref="ResidentialSubTilesOn"/>
    public int WorkGroundSubTilesOn(GridPos tile)
    {
        int index = IndexOf(tile);
        return index < 0 ? 0 : _workGroundCount[index];
    }

    /// <inheritdoc cref="ResidentialSubTilesOn"/>
    public int HarvestSubTilesOn(GridPos tile)
    {
        int index = IndexOf(tile);
        return index < 0 ? 0 : _harvestCount[index];
    }

    /// <summary>Where in the sub-tile arrays a sub-tile lives, or −1 if it is off the map.</summary>
    private int SubIndexOf(SubTile at)
    {
        int x = at.X - (_minX * SubTile.PerTile);
        int y = at.Y - (_minY * SubTile.PerTile);

        return x < 0 || x >= _subWidth || y < 0 || y >= _subHeight ? -1 : (y * _subWidth) + x;
    }

    /// <summary><see cref="SubIndexOf"/> run backwards, beside its inverse as the pair below is.</summary>
    public SubTile SubPositionOf(int index) =>
        new((index % _subWidth) + (_minX * SubTile.PerTile),
            (index / _subWidth) + (_minY * SubTile.PerTile));

    /// <summary>
    /// ⭐ Paint every one of a tile's sixteen sub-tiles — <b>what "paint this tile" now means</b>.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Kept as an overload rather than removed</b>, because the founding layout, the starting
    /// residential disc and a great many guards genuinely do mean *the whole tile* — and saying so
    /// by calling it is clearer than sixteen calls at each site. **Only the brush needs the finer
    /// door.**
    /// </remarks>
    private bool SetWholeTile(GridPos tile, System.Func<SubTile, bool> paint)
    {
        bool changed = false;

        for (int y = 0; y < SubTile.PerTile; y++)
        {
            for (int x = 0; x < SubTile.PerTile; x++)
            {
                changed |= paint(SubTile.Of(tile, x, y));
            }
        }

        return changed;
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
    /// <summary>
    /// ⭐ How many times any layer has changed — <b>so a drawing cache knows when it is stale</b>
    /// (D332).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔⛔ <b>THIS MUST NEVER ENTER <c>StateHash</c>.</b> It is not sim state — it is a count of
    /// edits, not a fact about the village, and two villages that reached the same painted ground by
    /// different routes are the same village. **Mixing it would move every golden for a number that
    /// describes the player's mouse rather than the world.**
    /// </para>
    /// <para>
    /// ⭐ The shape is <c>SimWorld.TerrainGeneration</c>'s, which <c>Minimap</c> already uses to
    /// decide when to re-bake its texture. *A monotonic counter is the cheapest honest answer to
    /// "has this changed since I last looked?"* — cheaper than a hash and impossible to get subtly
    /// wrong.
    /// </para>
    /// </remarks>
    public int Edits { get; private set; }

    public bool SetResidential(GridPos position, bool painted) =>
        SetWholeTile(position, at => SetResidential(at, painted));

    /// <summary>Paint or erase one SUB-tile. Returns true if it changed anything (D335).</summary>
    public bool SetResidential(SubTile at, bool painted)
    {
        int index = SubIndexOf(at);
        if (index < 0 || _residentialSub[index] == painted)
        {
            return false;
        }

        _residentialSub[index] = painted;
        Edits++;

        // ⭐ The tile-level answer is re-derived from a COUNT, not from a sweep of sixteen — see the
        // note on the arrays. `ResidentialTiles` and `_residential` keep meaning exactly what they
        // meant, so nothing downstream of them has to know this happened.
        int tile = IndexOf(at.Tile);
        if (tile < 0)
        {
            return true;
        }

        _residentialCount[tile] = (byte)(_residentialCount[tile] + (painted ? 1 : -1));

        bool nowPainted = _residentialCount[tile] >= SubTile.HalfATile;
        if (nowPainted != _residential[tile])
        {
            _residential[tile] = nowPainted;
            ResidentialTiles += nowPainted ? 1 : -1;
        }

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
    /// ⭐ Whether this building HOLDS this tile — any of it painted, and painted for them
    /// (D350, threshold moved to a quarter in D352).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The hold asked of one tile, so the plough and the un-plough can hang off the same answer
    /// <see cref="WorkGroundOf"/> gives — <b>two array reads, never a scan</b>. It is the question
    /// <c>SetWorkGround(SubTile, int)</c> already computes to keep the index; this is that
    /// question made available to the caller who has just painted.
    /// </para>
    /// <para>
    /// ⚠️ Since D352 it agrees with <see cref="WorkGroundOwner"/> on every tile — a quarter of a
    /// farm's brush is a quarter of a field, and the harvest is in proportion. It is kept as its
    /// own question because the plough and the un-plough ask *"do WE hold it?"*, owner included,
    /// and because the threshold has moved once already and may again.
    /// </para>
    /// </remarks>
    public bool Holds(int ownerId, GridPos tile)
    {
        int index = IndexOf(tile);
        return ownerId != 0
            && index >= 0
            && _workGround[index] == ownerId
            && _workGroundCount[index] > 0;
    }

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
    public bool SetWorkGround(GridPos position, int ownerId) =>
        SetWholeTile(position, at => SetWorkGround(at, ownerId));

    /// <summary>
    /// Give one SUB-tile to a building, or take it back with <paramref name="ownerId"/> 0 (D335).
    /// </summary>
    /// <remarks>
    /// ⭐⭐ <b>ONE OWNER PER TILE IS STILL THE RULE — IT JUST MOVED DOWN A LEVEL.</b> A sub-tile may
    /// only be given to the owner its tile already has, or to nobody. **Letting two huts share the
    /// quarters of one tile would be exactly the thing the rule exists to stop**, with the added
    /// cruelty that the player could not see the boundary: the argument on <c>_workGround</c> above
    /// is about two crews felling the same trees and a village that cannot say whose a stump is.
    /// </remarks>
    public bool SetWorkGround(SubTile at, int ownerId)
    {
        int sub = SubIndexOf(at);
        int index = IndexOf(at.Tile);
        if (sub < 0 || index < 0)
        {
            return false;
        }

        if (_workGroundSub[sub] == ownerId)
        {
            return false;
        }

        // Somebody else's ground. Theirs to give up, not ours to take — asked of the TILE, so a
        // hut cannot creep into a quarter of a neighbour's field.
        int owner = _workGround[index];
        if (owner != 0 && ownerId != 0 && owner != ownerId)
        {
            return false;
        }

        bool wasOwned = _workGroundSub[sub] != 0;
        _workGroundSub[sub] = ownerId;
        Edits++;

        _workGroundCount[index] = (byte)(_workGroundCount[index]
            + ((ownerId != 0 ? 1 : 0) - (wasOwned ? 1 : 0)));

        // ⭐⭐ ONE THRESHOLD FOR WORK GROUND NOW — ANY QUARTER — AND IT IS JOE'S (D352).
        //
        // D335 gave work ground two: *"whose ground is this tile?"* at any sub-tile, and *"how
        // much ground does this hut HAVE?"* at half, because the economy asks in whole tiles.
        // Then he painted a round field and read the difference off the screen — *"why is part of
        // this painted farm not farmland?"*, *"I want a fully round plot the same radius as the
        // paintbrush."* A tile a quarter painted was spoken for and not worked, and the quarter
        // showed as bare paint at the edge of every field.
        //
        // **So a farm works every tile it has any paint on, and a tile's harvest is in proportion
        // to how much of it is painted** (`BehaviorSystem`, the reap). The picture and the paint
        // are then the same shape, and a whole-tile village is exactly what it was. Residential
        // and harvest keep the half rule — a home needs a whole tile, a laborer clears a tile or
        // does not — so this is a rule about WORK GROUND, stated as one.
        int nowSpokenFor = _workGroundCount[index] > 0 ? (ownerId != 0 ? ownerId : owner) : 0;
        _workGround[index] = nowSpokenFor;

        bool holdsIt = _workGroundCount[index] > 0;
        bool held = _groundByOwner.TryGetValue(nowSpokenFor == 0 ? owner : nowSpokenFor,
            out List<int>? already) && already.BinarySearch(index) >= 0;

        if (holdsIt == held)
        {
            return true;
        }

        return GiveWholeTile(index, held ? (nowSpokenFor == 0 ? owner : nowSpokenFor) : 0,
            holdsIt ? nowSpokenFor : 0);
    }

    /// <summary>The tile-level bookkeeping, unchanged — it just has a new reason to run.</summary>
    private bool GiveWholeTile(int index, int current, int ownerId)
    {
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
        // ⚠️ NOT `_tilesByOwner.ContainsKey`, WHICH ONLY KNOWS ABOUT TILES THE HUT HELD (D335).
        // A hut whose ground was all quarter-painted holds no tiles at all, and an early-out on
        // that key would walk away leaving its paint behind — **still drawn, still hashed, and
        // owned by a building that no longer exists.**
        if (ownerId == 0)
        {
            return 0;
        }

        // ⛔ THE SUB-TILES ARE THE STATE, SO THEY ARE WHAT HAS TO BE CLEARED (D335). Wiping only
        // the tile summary would leave a demolished hut's ground still painted underneath, drawn
        // and hashed — *the "right stuff in the wrong place" shape this class's own comments record
        // costing four investigations.*
        for (int i = 0; i < _workGroundSub.Length; i++)
        {
            if (_workGroundSub[i] == ownerId)
            {
                _workGroundSub[i] = 0;
            }
        }

        // ⭐ THE NUMBER IS TILES THE HUT *KEPT*, BECAUSE THAT IS WHAT THE SENTENCE SAYS — *"the N
        // tiles it kept are free again."* Counting every tile it had a quarter of would over-report
        // to the player by however many corners they had clipped. *One number, one question* (D322).
        int freed = WorkGroundTiles(ownerId);

        for (int i = 0; i < _workGround.Length; i++)
        {
            if (_workGround[i] == ownerId)
            {
                _workGround[i] = 0;
                _workGroundCount[i] = 0;
            }
        }

        _tilesByOwner.Remove(ownerId);
        _groundByOwner.Remove(ownerId);
        Edits++;
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
    public bool SetHarvest(GridPos position, bool painted) =>
        SetWholeTile(position, at => SetHarvest(at, painted));

    /// <summary>Mark or unmark one SUB-tile. Returns true if it changed anything (D335).</summary>
    public bool SetHarvest(SubTile at, bool painted)
    {
        int sub = SubIndexOf(at);
        if (sub < 0 || _harvestSub[sub] == painted)
        {
            return false;
        }

        _harvestSub[sub] = painted;
        Edits++;

        int tile = IndexOf(at.Tile);
        if (tile < 0)
        {
            return true;
        }

        _harvestCount[tile] = (byte)(_harvestCount[tile] + (painted ? 1 : -1));

        bool nowMarked = _harvestCount[tile] >= SubTile.HalfATile;
        if (nowMarked == _harvest[tile])
        {
            return true;
        }

        return MarkWholeTile(tile, nowMarked);
    }

    /// <summary>The tile-level bookkeeping, unchanged — it just has a new reason to run.</summary>
    private bool MarkWholeTile(int index, bool painted)
    {
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
