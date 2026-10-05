using System.Text.Json.Nodes;
using Bclone.Sim.Persistence;

namespace Bclone.Sim.World;

/// <summary>
/// ⭐⭐ How trodden each tile is — <b>the desire-path substrate</b> (`DESIGN.md §2.6`,
/// `specs/desire-paths.md`, D358).
/// </summary>
/// <remarks>
/// <para>
/// <b>The thesis of §2.6:</b> the player paves what the village already proved matters. Every
/// step a villager takes treads the tile under them; wear fades a little each season; past a
/// threshold the grass reads as a worn path and the tile is <em>cheaper to cross</em>, so routes
/// converge onto it — the reinforcement loop that makes a footpath. Both halves of that loop
/// read <b>one</b> cost field (`CLAUDE.md`), so labour catchment and movement cannot disagree
/// about how far a thing is.
/// </para>
/// <para>
/// <b>⛔ It is SIM STATE and it is hashed</b> — sparsely, the non-zero tiles in index order, so a
/// valley nobody has walked hashes as it did (the `ZoneMap` sub-tile idiom). It is not a view
/// effect: it changes where people go and how long they take.
/// </para>
/// <para>
/// ⛔ <b>Decay is a seasonal sweep, never a per-tick pass</b> (`CLAUDE.md`'s standing rule). And
/// <b>wear reaches the cost field only when the season turns</b>: <see cref="Generation"/> is
/// bumped by <see cref="Decay"/>, and the world forgets its cached flow fields on that bump
/// (`TravelCost.Forget()`), never on a step — every cost change clears every field, and a village
/// that re-Dijkstra'd itself each tick would be the D179 regression on purpose.
/// </para>
/// </remarks>
public sealed class PathWear
{
    private readonly ushort[] _wear;
    private readonly int _width;
    private readonly int _height;
    private readonly int _minX;
    private readonly int _minY;

    public PathWear(GeneratedMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        _width = map.Width;
        _height = map.Height;
        _minX = map.MinX;
        _minY = map.MinY;
        _wear = new ushort[_width * _height];
        _priceClass = new byte[_width * _height];
    }

    /// <summary>Every tile's wear, in map order — the state, hashed and drawn.</summary>
    public IReadOnlyList<ushort> Tiles => _wear;

    /// <summary>
    /// How many times the wear has been <b>applied</b> — decayed and handed to the cost field. For
    /// caches (the flow fields, the view's trails), never hashed.
    /// </summary>
    public int Generation { get; private set; }

    /// <summary>How many tiles carry any wear at all. Derived, never hashed.</summary>
    public int TroddenTiles { get; private set; }

    /// <summary>How many tiles are a path — worn or packed by class — as of the last sweep. Derived, never hashed.</summary>
    public int PathTiles { get; private set; }

    /// <summary>How trodden one tile is; 0 off the map.</summary>
    public int At(GridPos tile)
    {
        int index = IndexOf(tile);
        return index < 0 ? 0 : _wear[index];
    }

    /// <summary>A footstep: add <paramref name="amount"/> to the tile, stopping at the ceiling rather than wrapping.</summary>
    /// <remarks>
    /// <para>
    /// ⛔⛔ <b>THE GROUND'S MEMORY IS BOUNDED (D414).</b> Wear stops at <see cref="CapAt">the
    /// ceiling</see> — `path_wear_ceiling`, a little past packed. Until D414 it ran on to
    /// <see cref="ushort.MaxValue"/>, and measured, the hub tiles of a twenty-five-year village held
    /// 2,000–6,400 wear: at a decay of 4 a season a path abandoned there would have stayed packed
    /// for a century or more, where the spec promised six seasons of grace. A busy tile is no more a
    /// path at 6,000 than at 100 — the class is the whole answer (D362) — so all the excess ever
    /// bought was a path that could not fade.
    /// </para>
    /// <para>
    /// ⛔ And it still never WRAPS — a wrap would turn the most-walked tile in the village back into
    /// fresh grass, identically on both machines, so the determinism suite would stay green while
    /// the main street vanished (the `Fixed` overflow argument, D317). The ceiling is validated to
    /// fit a <see cref="ushort"/>.
    /// </para>
    /// </remarks>
    public void Tread(GridPos tile, int amount)
    {
        int index = IndexOf(tile);
        if (index < 0 || amount <= 0)
        {
            return;
        }

        if (_wear[index] == 0)
        {
            TroddenTiles++;
        }

        int next = _wear[index] + amount;
        if (next > _ceiling)
        {
            next = Math.Max(_ceiling, (int)_wear[index]);
        }

        _wear[index] = (ushort)next;
    }

    /// <summary>
    /// The most wear a tile can hold — `path_wear_ceiling` (D414). A tile at the ceiling that nobody
    /// walks is back under the packed line after <c>(ceiling − (packed − holdsFor)) ⁄ decay</c>
    /// seasons.
    /// </summary>
    public void CapAt(int ceiling)
    {
        if (ceiling < 1 || ceiling > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ceiling), $"The wear ceiling must run 1..{ushort.MaxValue} (got {ceiling}).");
        }

        _ceiling = ceiling;
    }

    private int _ceiling = ushort.MaxValue;

    /// <summary>
    /// The season turns: every tile fades by <paramref name="amount"/>, floored at nothing, and the
    /// wear is handed to the cost field — <b>but the routes are told only if some tile's PRICE
    /// changed</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔⛔ <b>THE FIRST DRAFT REBUILT EVERY FLOW FIELD EVERY SEASON AND THE SUITE WENT PAST TEN
    /// MINUTES</b> (D358) — the D329 regression by another door. Routes do not care how worn a
    /// tile is; they care which of three prices it carries. So each tile's price class as of the
    /// last hand-over is kept (<c>_priceClass</c>), and <see cref="RoutesGeneration"/> moves only
    /// when the sweep finds a tile whose class differs. A valley whose paths are settled rebuilds
    /// nothing; one whose lanes are wearing in rebuilds when they cross a threshold and not before.
    /// </para>
    /// <para>
    /// <see cref="Generation"/> still moves every sweep — the view's trails redraw on it.
    /// </para>
    /// </remarks>
    /// <param name="amount">How much every tile fades.</param>
    /// <param name="reprice">Whether to hand the new classes to the routes. ⭐ The season's sweep
    /// passes false three times a year and true once (spring): every re-price refills ~a hundred
    /// flow fields, and pricing four times a year cost the suite half again on top of hysteresis.
    /// ⭐ Since D362 the CLASSES move every sweep — a path appears on the map the season it wears
    /// through and holds for its grace — and only the hand-over to the routes waits for spring,
    /// which is when a footpath firms into a way people rely on.</param>
    /// <returns>How many tiles still carry wear.</returns>
    public int Decay(int amount, bool reprice = true)
    {
        int trodden = 0;
        int paths = 0;
        for (int i = 0; i < _wear.Length; i++)
        {
            int next = _wear[i] - amount;
            _wear[i] = next <= 0 ? (ushort)0 : (ushort)next;
            if (_wear[i] > 0)
            {
                trodden++;
            }

            byte price = PriceClassOf(_wear[i], _priceClass[i], _holdsFor);
            if (price != _priceClass[i])
            {
                _priceClass[i] = price;
                _routesDirty = true;
            }

            if (price > 0)
            {
                paths++;
            }
        }

        TroddenTiles = trodden;
        PathTiles = paths;
        Generation++;
        if (reprice && _routesDirty)
        {
            RoutesGeneration++;
            _routesDirty = false;
        }

        return trodden;
    }

    /// <summary>A tile's class as of the last sweep — 0 grass, 1 worn, 2 packed — by map-order index. The ONE answer the routes price and the view draws (D362).</summary>
    public byte PriceClassAt(int index) => _priceClass[index];

    /// <summary>A tile's class as of the last sweep; 0 off the map.</summary>
    public byte ClassAt(GridPos tile)
    {
        int index = IndexOf(tile);
        return index < 0 ? (byte)0 : _priceClass[index];
    }

    /// <summary>
    /// How many times the PRICE of some tile changed at a hand-over — what the cost field's cache
    /// keys on. Never hashed.
    /// </summary>
    public int RoutesGeneration { get; private set; }

    private readonly byte[] _priceClass;
    private int _wornAt = int.MaxValue;
    private int _packedAt = int.MaxValue;
    private int _holdsFor;
    private bool _routesDirty;

    /// <summary>
    /// Tell the wear where the price steps are and how far under its line a path may fall before it
    /// stops being one (<paramref name="holdsFor"/>, D362), so the sweep can see a class change.
    /// </summary>
    public void PriceAt(int wornAt, int packedAt, int holdsFor = 0)
    {
        _wornAt = wornAt;
        _packedAt = packedAt;
        _holdsFor = holdsFor;
    }

    /// <summary>
    /// A tile's price class, with HYSTERESIS: a tile that has become a path stays one until its
    /// wear falls <paramref name="band"/> below the threshold — `path_holds_for` (D362) — so a lane
    /// hovering at the line does not flap, and an abandoned lane goes back to grass together, after
    /// its grace, rather than tile by tile.
    /// </summary>
    /// <remarks>
    /// ⛔ Measured before it existed (D358): the shipped valley re-priced its routes in 26 of the
    /// first decade's 40 seasons — every re-price is ~a hundred flow fields refilled — because
    /// well-used tiles sat within one season's decay of a threshold and flapped across it. The band
    /// was the decay itself then; Joe's play (D362) made it a dial: *"once it exists, it should
    /// exist for longer before growing back."*
    /// </remarks>
    private byte PriceClassOf(int wear, byte previous, int band)
    {
        if (wear >= _packedAt || (previous == 2 && wear >= _packedAt - band))
        {
            return 2;
        }

        if (wear >= _wornAt || (previous >= 1 && wear >= _wornAt - band))
        {
            return 1;
        }

        return 0;
    }

    /// <summary>How many tiles are at or past <paramref name="threshold"/> — for the log and the panel.</summary>
    public int TilesAtLeast(int threshold)
    {
        int count = 0;
        for (int i = 0; i < _wear.Length; i++)
        {
            if (_wear[i] >= threshold)
            {
                count++;
            }
        }

        return count;
    }

    private int IndexOf(GridPos tile)
    {
        int x = tile.X - _minX;
        int y = tile.Y - _minY;
        return x < 0 || x >= _width || y < 0 || y >= _height ? -1 : (y * _width) + x;
    }

    /// <summary>Where a map-order index is — the inverse of the indexing, for the hash and the view.</summary>
    public GridPos PositionOf(int index) => new((index % _width) + _minX, (index / _width) + _minY);

    // ---------------------------------------------------------------
    //  Save and load (`specs/save-load.md §5.2`)
    // ---------------------------------------------------------------
    //
    // ⚠️ The price classes are saved beside the wear: the hysteresis remembers the class each tile had at
    // the last hand-over, and today's wear alone cannot say it. The thresholds and the ceiling are the
    // config's and are set again by `SimWorld` exactly as at a founding. `Generation` starts fresh — it
    // only answers "has this changed since I looked?" and nothing has looked yet.

    internal JsonObject ToSave() => new()
    {
        ["wear"] = SaveWriter.UShorts(_wear),
        ["price_class"] = SaveWriter.Bytes(_priceClass),
        ["trodden_tiles"] = TroddenTiles,
        ["path_tiles"] = PathTiles,
        ["routes_generation"] = RoutesGeneration,
        ["routes_dirty"] = _routesDirty,
    };

    internal void ReadSave(SaveReader save)
    {
        save.UShortsInto("wear", _wear);
        save.BytesInto("price_class", _priceClass);
        TroddenTiles = save.Int("trodden_tiles");
        PathTiles = save.Int("path_tiles");
        RoutesGeneration = save.Int("routes_generation");
        _routesDirty = save.Bool("routes_dirty");
    }
}
