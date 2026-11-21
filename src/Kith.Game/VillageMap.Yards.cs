using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <summary>Which yard a building has (`building-yards.md §3`).</summary>
internal enum YardKind
{
    None,
    Woodcutter,
    Builder,
    Hunter,
    Forager,
    Forester,
    Fisher,
    Granary,
    Warehouse,
    Market,
    Smith,
    Quarry,
    Mine,
    Workshop,
    Bakery,
    Tailor,
    TradingPost,
    Farmhouse,
    Mill,
    Barn,
    Library,
    TownHall,
    Well,
    Cabin,
    Cottage,
    Manor,
    FoundersCart,
}

/// <summary>
/// What a building's yard depends on, from the sim — in its <see cref="VillageMap.RoofSignature"/>, so the yard is laid again
/// only when one of these moves (`building-yards.md §2`): two stock levels (0–4, <see cref="VillageMap.YardLevel"/>), whether
/// it is working, the side its water is on (−1 none, 0 −y, 1 +x, 2 +y, 3 −x), and a ground counter (the plot's or the
/// terrain's edits).
/// </summary>
internal readonly record struct YardState(byte Stock = 0, byte Stock2 = 0, bool Working = false, sbyte Water = -1, int Ground = 0);

/// <remarks>
/// ⭐⭐ THE YARDS AND THE PEN (A86, `specs/building-yards.md`; Joe: *"a 'yard' for each building to add some visual
/// flavour"*, *"agree with this 100%"* on the stock rule, *"Yes, build it"* on the pen-and-wash pass after his house-tier
/// sheet). Every thing in a yard is a <see cref="RoofPiece"/> laid into A85's cached runs, in the building's turned frame;
/// ⛔ anything that is stock is drawn only while the building holds it (§2).
/// </remarks>
public partial class VillageMap
{
    private static readonly Color Paper = new("#efe6d0");
    private static readonly Color TroddenEarth = new("#b9a17a");
    private static readonly Color Bark = new("#6a4a30");
    private static readonly Color EndGrain = new("#d9b47c");
    private static readonly Color Timber = new("#8a6440");
    private static readonly Color TimberLight = new("#b08a5c");
    private static readonly Color TimberDark = new("#5a3e27");
    private static readonly Color PlankColour = new("#c6a272");
    private static readonly Color Straw = new("#d9bf68");
    private static readonly Color SackColour = new("#d2bc8e");
    private static readonly Color YardStone = new("#b2ada2");
    private static readonly Color CutStone = new("#e2ddd2");
    private static readonly Color YardIron = new("#4b4e54");
    private static readonly Color YardWater = new("#6f97ad");
    private static readonly Color HideColour = new("#c79c6a");
    private static readonly Color FishSilver = new("#c4ced2");
    private static readonly Color MeatRed = new("#8e4a3a");
    private static readonly Color Ore = new("#8e4c32");
    private static readonly Color OvenClay = new("#b9693f");
    private static readonly Color Seedling = new("#5e8f3a");
    private static readonly Color Wool = new("#e4dac0");
    private static readonly Color Charcoal = new("#2b2826");
    private static readonly Color BoatHull = new("#4d6a80");
    private static readonly Color NetTwine = new("#a99c82");
    private static readonly Color Pavement = new("#cfc8b8");
    private static readonly Color Wicker = new("#9a7b4a");
    private static readonly Color[] Linen = { new("#e9e1cf"), new("#6f86a6"), new("#a5574a"), new("#d4b25a"), new("#8a9a6a") };
    private static readonly Color[] Dyes = { new("#a5574a"), new("#5d77a0"), new("#d4b25a") };

    /// <summary>Pen strokes never closer than this on screen (§5): the zoom thins them.</summary>
    internal const float PenMinPixels = 3f;

    /// <summary>How much of the paper is in a wash (§5).</summary>
    internal const float WashPaper = 0.12f;

    /// <summary>A stock amount as a level (§2): 0 → 0; 1–4 → 1; 5–19 → 2; 20–59 → 3; 60 + → 4.</summary>
    internal static byte YardLevel(int amount) => amount switch
    {
        <= 0 => 0,
        < 5 => 1,
        < 20 => 2,
        < 60 => 3,
        _ => 4,
    };

    /// <summary>A kind's colour as a wash: a little of the paper through it.</summary>
    internal static Color Washed(Color colour) => colour.Lerp(Paper, WashPaper) with { A = colour.A };

    // ------------------------------------------------------------------ the sim's side: a building's yard state

    private readonly Dictionary<int, (int Generation, sbyte Side)> _waterSides = new();

    /// <summary>What of a building's own store a yard reads (§2's table).</summary>
    internal enum StockRead
    {
        None,
        Fish,
        Meat,
        Leather,
        Wheat,
        Edible,
        Held,
        HeldLessCarts,
        Handcarts,
        Hay,
        Tools,
    }

    /// <summary>
    /// ⭐ §2's table: what each yard's two stock levels read from its building's own store, and whether its piles show while
    /// it is working instead (the kinds that keep no stock).
    /// </summary>
    internal static (StockRead Stock, StockRead Stock2, bool WhileWorking) StockReadOf(YardKind yard) => yard switch
    {
        YardKind.Fisher => (StockRead.Fish, StockRead.None, false),
        YardKind.Hunter => (StockRead.Meat, StockRead.Leather, false),
        YardKind.Farmhouse => (StockRead.Wheat, StockRead.None, false),
        YardKind.Granary => (StockRead.Edible, StockRead.None, false),
        YardKind.Warehouse => (StockRead.HeldLessCarts, StockRead.Handcarts, false),
        YardKind.Market or YardKind.TradingPost => (StockRead.Held, StockRead.None, false),
        YardKind.Barn => (StockRead.Hay, StockRead.None, false),
        YardKind.FoundersCart => (StockRead.Edible, StockRead.Tools, false),
        YardKind.Woodcutter or YardKind.Builder or YardKind.Forager or YardKind.Smith or YardKind.Quarry or YardKind.Mine
            or YardKind.Bakery or YardKind.Tailor or YardKind.Mill => (StockRead.None, StockRead.None, true),
        _ => (StockRead.None, StockRead.None, false),
    };

    /// <summary>The amount a <see cref="StockRead"/> names, in a store.</summary>
    internal static int Read(Stockpile store, StockRead read, IReadOnlyList<Goods> edible) => read switch
    {
        StockRead.Fish => store[Goods.Fish],
        StockRead.Meat => store[Goods.Meat],
        StockRead.Leather => store[Goods.Leather],
        StockRead.Wheat => store[Goods.Wheat],
        StockRead.Edible => EdibleIn(store, edible),
        StockRead.Held => store.Held,
        StockRead.HeldLessCarts => store.Held - store[Goods.Handcart],
        StockRead.Handcarts => store[Goods.Handcart],
        StockRead.Hay => store[Goods.Hay],
        StockRead.Tools => store[Goods.Tools],
        _ => 0,
    };

    /// <summary>A workplace's yard state (§2): its own store for the kinds that keep one, working for the rest.</summary>
    private YardState YardStateOf(SimWorld world, Workplace workplace, YardKind yard)
    {
        (StockRead stock, StockRead stock2, bool whileWorking) = StockReadOf(yard);
        IReadOnlyList<Goods> edible = world.GoodsCatalog.EdibleGoods;
        bool fisher = yard == YardKind.Fisher;
        return new YardState(
            Stock: YardLevel(Read(workplace.Store, stock, edible)),
            Stock2: YardLevel(Read(workplace.Store, stock2, edible)),
            Working: whileWorking && world.IdleNote(workplace) is null,
            Water: fisher ? WaterSideOf(world, workplace) : (sbyte)-1,
            Ground: fisher ? world.TerrainGeneration : 0);
    }

    /// <summary>A store's yard state (§2).</summary>
    private YardState YardStateOf(StoreBuilding building, YardKind yard)
    {
        (StockRead stock, StockRead stock2, _) = StockReadOf(yard);
        IReadOnlyList<Goods> edible = _world!.GoodsCatalog.EdibleGoods;
        return new YardState(Stock: YardLevel(Read(building.Store, stock, edible)), Stock2: YardLevel(Read(building.Store, stock2, edible)));
    }

    /// <summary>A household's yard state (§2, §4): its firewood, its food, and the plot's edits.</summary>
    private YardState YardStateOf(Household household) =>
        new(Stock: YardLevel(household.Stockpile.Firewood), Stock2: YardLevel(EdibleIn(household.Stockpile, _world!.GoodsCatalog.EdibleGoods)), Ground: _world!.Zones.Edits);

    private static int EdibleIn(Stockpile store, IReadOnlyList<Goods> edible)
    {
        int food = 0;
        foreach (Goods goods in edible)
        {
            food += store[goods];
        }

        return food;
    }

    /// <summary>
    /// The side of a fishing hut's ground with water nearest (§3.3), in its own frame — four directions, two tiles out;
    /// −1 if none. Kept per hut until the terrain changes (a counter, CLAUDE.md).
    /// </summary>
    private sbyte WaterSideOf(SimWorld world, Workplace hut)
    {
        if (_waterSides.TryGetValue(hut.Id, out (int Generation, sbyte Side) held) && held.Generation == world.TerrainGeneration)
        {
            return held.Side;
        }

        sbyte side = -1;
        float radians = hut.Facing.Raw * Mathf.Tau / 65536f;
        Vector2 centre = InViewTiles(hut.Position);
        for (int reach = 1; reach <= 2 && side < 0; reach++)
        {
            for (sbyte s = 0; s < 4 && side < 0; s++)
            {
                Vector2 local = SideOut(s) * (0.5f + (reach * 0.6f));
                Vector2 at = centre + local.Rotated(radians);
                var tile = new GridPos(Mathf.RoundToInt(at.X), Mathf.RoundToInt(at.Y));
                if (world.Map.Contains(tile) && world.Map.TerrainAt(tile) == Terrain.Water)
                {
                    side = s;
                }
            }
        }

        _waterSides[hut.Id] = (world.TerrainGeneration, side);
        return side;
    }

    private static Vector2 SideOut(int side) => side switch { 0 => new Vector2(0, -1), 1 => new Vector2(1, 0), 2 => new Vector2(0, 1), _ => new Vector2(-1, 0) };

    /// <summary>
    /// The household's yard tiles in its house's own frame (§4.1): its plot's tiles, less the ones the house stands on.
    /// Asked only when the house is laid again (its plot's edits are in its signature).
    /// </summary>
    private Vector2[] HomeYardTiles(Household household, Vector2 at, ushort facing, float wide, float deep)
    {
        if (!_plotTilesByOwner.TryGetValue(household.Id, out List<GridPos>? tiles))
        {
            return Array.Empty<Vector2>();
        }

        float radians = facing * Mathf.Tau / 65536f;
        var yard = new List<Vector2>();
        foreach (GridPos tile in tiles)
        {
            Vector2 local = (new Vector2(tile.X, tile.Y) - at).Rotated(-radians);
            if (Mathf.Abs(local.X) < (wide / 2f) + 0.2f && Mathf.Abs(local.Y) < (deep / 2f) + 0.2f)
            {
                continue;
            }

            yard.Add(local);
        }

        return yard.ToArray();
    }

    /// <summary>Each household's plot tiles, in the order the outline pass met them; rebuilt with the outlines (on the plots' edits).</summary>
    private readonly Dictionary<int, List<GridPos>> _plotTilesByOwner = new();

    // ------------------------------------------------------------------ the layouts (§3)

    /// <summary>Where the roof stands and where the yard is, in the building's frame (§3).</summary>
    internal readonly record struct YardLayout(Vector2 RoofAt, float RoofHw, float RoofHd, Vector2 YardAt, Vector2 YardAlong, float YardLong, float YardShort)
    {
        /// <summary>A point in the yard by fractions (−1..1) along and across it.</summary>
        public Vector2 P(float along, float across) =>
            YardAt + (YardAlong * along * YardLong) + (new Vector2(-YardAlong.Y, YardAlong.X) * across * YardShort);

        public float Turn => YardAlong.Angle();
    }

    /// <summary>
    /// ⭐ Pure: the stage a farm's tile is drawn in its field — none where a building stands (A142; Joe: <i>"if a farm field is
    /// placed under a farmhouse, the field shouldnt be seen where the farmhouse yard exists. it presently looks like it is a
    /// visible layer below"</i>). A yard is its building's own ground (§3: the roof drawn smaller, the yard the rest of the
    /// footprint), washed at half strength over whatever is under it — so a field painted under a farmhouse showed through
    /// its yard. Whatever stands on the tile owns it; the field stops at the building. Asked of <c>SimWorld.StandsOn</c>, the
    /// standing index, and the fields are re-traced when it moves (<c>StandingGeneration</c>).
    /// </summary>
    internal static Terrain FieldStageDrawn(Terrain ground, bool standing) =>
        !standing && ground is Terrain.Field or Terrain.Sown or Terrain.Ripe ? ground : Terrain.Grass;

    /// <summary>The drawn share of the extent a kind's roof had in A85 (the ground is the extent).</summary>
    internal static float GroundShareOf(YardKind yard) => yard switch { YardKind.TownHall => 0.95f, YardKind.Well => 0.7f, _ => 0.8f };

    /// <summary>
    /// ⭐ The roof's rectangle and the yard's (§3). <paramref name="hw"/>, <paramref name="hd"/> are A85's drawn halves;
    /// <paramref name="doorOut"/> the door wall's outward way and <paramref name="doorLocal"/> its middle, in the frame.
    /// </summary>
    internal static YardLayout LayoutOf(YardKind yard, float hw, float hd, Vector2 doorOut, Vector2 doorLocal, int seed)
    {
        float share = GroundShareOf(yard);
        float fw = hw / share;
        float fd = hd / share;
        bool alongX = hw >= hd;
        Vector2 longAxis = alongX ? new Vector2(1, 0) : new Vector2(0, 1);
        float longHalf = alongX ? hw : hd;
        float shortHalf = alongX ? hd : hw;
        float fullLong = alongX ? fw : fd;
        float fullShort = alongX ? fd : fw;

        // The door's end along the long axis: its wall if it is an end, else the half its middle is in (§3.1).
        float doorAlong = doorOut.Dot(longAxis);
        float sx = Mathf.Abs(doorAlong) > 0.5f ? Mathf.Sign(doorAlong)
            : Mathf.Abs(doorLocal.Dot(longAxis)) > 0.05f ? Mathf.Sign(doorLocal.Dot(longAxis))
            : ((seed & 2) == 0 ? 1f : -1f);
        const float gap = RoofEaves + 0.05f;

        switch (yard)
        {
            case YardKind.Woodcutter or YardKind.Builder or YardKind.Hunter:
            {
                float roofHalf = longHalf / 2f;
                Vector2 roofAt = longAxis * sx * roofHalf;
                float yardLong = (fullLong - gap) / 2f;
                return Make(roofAt, roofHalf, shortHalf, -longAxis * sx * (gap + yardLong), longAxis, yardLong, fullShort);
            }

            case YardKind.Smith or YardKind.Quarry or YardKind.Mine or YardKind.Workshop or YardKind.Bakery or YardKind.Tailor
                or YardKind.TradingPost or YardKind.Barn:
            {
                float roofHalf = longHalf * (yard == YardKind.Barn ? 0.68f : 0.8f);
                float roofCentre = sx * (longHalf - roofHalf);
                float inner = roofCentre - (sx * (roofHalf + gap));
                float yardLong = (Mathf.Abs(inner - (-sx * fullLong))) / 2f;
                return Make(longAxis * roofCentre, roofHalf, shortHalf, longAxis * (inner - (sx * yardLong)), longAxis, yardLong, fullShort);
            }

            case YardKind.Granary or YardKind.Warehouse or YardKind.Library or YardKind.TownHall or YardKind.Farmhouse:
            {
                // A strip on the door's side; the roof at the back. The strip runs across the door's way.
                Vector2 way = DominantAxis(doorOut);
                bool wayIsX = Mathf.Abs(way.X) > 0.5f;
                float depthHalf = wayIsX ? hw : hd;
                float fullDepth = wayIsX ? fw : fd;
                float widthHalf = wayIsX ? hd : hw;
                float fullWidth = wayIsX ? fd : fw;
                float roofDepthHalf = depthHalf * (yard is YardKind.Library ? 0.75f : 0.68f);
                float roofCentre = -(depthHalf - roofDepthHalf);
                float roofWidthHalf = widthHalf * (yard == YardKind.Farmhouse ? 0.9f : 1f);
                Vector2 across = new(-way.Y, way.X);
                float side = yard == YardKind.Farmhouse ? ((seed & 4) == 0 ? 1f : -1f) * (widthHalf - roofWidthHalf) : 0f;
                Vector2 roofAt = (way * roofCentre) + (across * side);
                float inner = roofCentre + roofDepthHalf + gap;
                float yardDepth = (fullDepth - inner) / 2f;
                float roofHw = wayIsX ? roofDepthHalf : roofWidthHalf;
                float roofHd = wayIsX ? roofWidthHalf : roofDepthHalf;
                return new YardLayout(roofAt, roofHw, roofHd, way * (inner + yardDepth), across, fullWidth, yardDepth);
            }

            case YardKind.Forager or YardKind.Forester or YardKind.Fisher:
            {
                Vector2 way = DominantAxis(doorOut);
                Vector2 across = new(-way.Y, way.X);
                float roofHw = hw * 0.75f;
                float roofHd = hd * 0.69f;
                Vector2 roofAt = -way * 0.1f;
                float back = ((Mathf.Abs(way.X) > 0.5f) ? roofHw : roofHd) - 0.1f;
                float inner = back + gap;
                float yardDepth = (fd - inner) / 2f;
                return new YardLayout(roofAt, roofHw, roofHd, way * (inner + yardDepth), across, fw, yardDepth);
            }

            case YardKind.Mill:
            {
                Vector2 way = DominantAxis(doorOut);
                return new YardLayout(Vector2.Zero, hw, hd, way * (fd * 0.78f), new Vector2(-way.Y, way.X), fw * 0.8f, fd * 0.2f);
            }

            default:
                return new YardLayout(Vector2.Zero, hw, hd, Vector2.Zero, new Vector2(1, 0), fw, fd);
        }

        static YardLayout Make(Vector2 roofAt, float roofLong, float roofShort, Vector2 yardAt, Vector2 axis, float yardLong, float yardShort)
        {
            bool x = Mathf.Abs(axis.X) > 0.5f;
            return new YardLayout(roofAt, x ? roofLong : roofShort, x ? roofShort : roofLong, yardAt, axis, yardLong, yardShort);
        }
    }

    private static Vector2 DominantAxis(Vector2 v) =>
        Mathf.Abs(v.X) >= Mathf.Abs(v.Y) ? new Vector2(Mathf.Sign(v.X == 0 ? 1 : v.X), 0) : new Vector2(0, Mathf.Sign(v.Y));

    /// <summary>The door's wall, outward, and its middle, in the building's frame (as A85's step finds them).</summary>
    private static (Vector2 Out, Vector2 Middle) DoorInFrame(RoofFrame frame, float wide, float deep, ushort facing, Door door, Vector2 doorAt, float ppt)
    {
        Vector2 centre = frame.At(Vector2.Zero);
        Vector2[] notch = DoorQuadAt(centre, wide, deep, facing, door, doorAt, ppt);
        int round = ((int)door.Faces - (int)door.Front) & 3;
        Vector2 outward = round switch { 1 => new Vector2(1, 0), 2 => new Vector2(0, 1), 3 => new Vector2(-1, 0), _ => new Vector2(0, -1) };
        Vector2 middle = frame.Local(((notch[0] + notch[1] + notch[2] + notch[3]) / 4f - centre) / ppt);
        return (outward, middle);
    }

    // ------------------------------------------------------------------ the pen (§5)

    /// <summary>Deterministic 0..1 from three ints (no <c>Rng</c>: the view never touches the sim's).</summary>
    internal static float Hash01(int a, int b, int c)
    {
        unchecked
        {
            uint h = (uint)((a * 374761393) + (b * 668265263) + (c * 1274126177));
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFF) / 65535f;
        }
    }

    /// <summary>A segment clipped to a convex polygon (Cyrus–Beck), or null if nothing of it is inside.</summary>
    internal static (Vector2, Vector2)? ClipToConvex(Vector2 a, Vector2 b, Vector2[] poly)
    {
        float t0 = 0f;
        float t1 = 1f;
        Vector2 d = b - a;
        Vector2 inside = Vector2.Zero;
        foreach (Vector2 p in poly)
        {
            inside += p;
        }

        inside /= poly.Length;
        for (int i = 0; i < poly.Length; i++)
        {
            Vector2 p0 = poly[i];
            Vector2 p1 = poly[(i + 1) % poly.Length];
            Vector2 edge = p1 - p0;
            var n = new Vector2(-edge.Y, edge.X);
            if (n.Dot(inside - p0) < 0)
            {
                n = -n;
            }

            float num = n.Dot(a - p0);
            float den = n.Dot(d);
            if (Mathf.Abs(den) < 1e-9f)
            {
                if (num < 0)
                {
                    return null;
                }

                continue;
            }

            float t = -num / den;
            if (den > 0)
            {
                t0 = Mathf.Max(t0, t);
            }
            else
            {
                t1 = Mathf.Min(t1, t);
            }

            if (t0 > t1)
            {
                return null;
            }
        }

        return (a + (d * t0), a + (d * t1));
    }

    /// <summary>Diagonal hatching over a convex shape, in the frame (§5): strokes toward the upper right on screen.</summary>
    private static List<Vector2> Hatch(RoofFrame frame, Vector2[] localShape, float spacing)
    {
        Vector2 way = frame.Local(new Vector2(1f, -1f).Normalized());
        var across = new Vector2(-way.Y, way.X);
        Vector2 mid = Vector2.Zero;
        float reach = 0f;
        foreach (Vector2 p in localShape)
        {
            mid += p;
        }

        mid /= localShape.Length;
        foreach (Vector2 p in localShape)
        {
            reach = Mathf.Max(reach, p.DistanceTo(mid));
        }

        var pairs = new List<Vector2>();
        for (float k = -reach; k <= reach; k += spacing)
        {
            Vector2 a = mid + (across * k) - (way * reach);
            Vector2 b = mid + (across * k) + (way * reach);
            if (ClipToConvex(a, b, localShape) is (Vector2 from, Vector2 to))
            {
                pairs.Add(frame.At(from));
                pairs.Add(frame.At(to));
            }
        }

        return pairs;
    }

    /// <summary>Hatch a shade (§5): faces darker than 1, and every cast shadow.</summary>
    private static void HatchInto(List<RoofPiece> pieces, RoofFrame frame, Vector2[] localShape, string part, float darkness)
    {
        if (darkness <= 0.01f)
        {
            return;
        }

        List<Vector2> pairs = Hatch(frame, localShape, 0.06f);
        if (pairs.Count > 0)
        {
            pieces.Add(new RoofPiece(RoofPieceKind.Lines, part + " hatch", pairs.ToArray(), Ink with { A = Mathf.Min(0.42f, darkness * 0.42f) }, Spacing: 0.06f));
        }
    }

    /// <summary>The courses a material is drawn in by the pen, in tiles: between courses, and between joints (§5).</summary>
    internal static (float Course, float Joint) PenSpacingOf(RoofMaterial material) => material switch
    {
        RoofMaterial.Thatch => (0.2f, 0.05f),
        RoofMaterial.Shingle => (0.11f, 0.075f),
        RoofMaterial.Slate => (0.09f, 0.12f),
        RoofMaterial.Tile => (0.08f, 0.065f),
        RoofMaterial.Boards => (0.12f, 0.12f),
        RoofMaterial.Turf => (0.12f, 0.06f),
        _ => (0.24f, 0.3f),
    };

    /// <summary>
    /// ⭐ The pen's material on one roof face (§5), in the face's frame: <paramref name="eaveA"/> to <paramref name="eaveB"/> is
    /// its eave, <paramref name="inward"/> up the slope, <paramref name="height"/> to the ridge; every stroke clipped to the
    /// face (<paramref name="face"/>, local, convex). Courses and joints, strands and a fringe, boards, or tufts.
    /// </summary>
    private static void PenTexture(
        List<RoofPiece> pieces, RoofFrame frame, string part, RoofMaterial material, Vector2[] face, Vector2 eaveA, Vector2 eaveB, Vector2 inward, float height, int seed)
    {
        (float course, float joint) = PenSpacingOf(material);
        Vector2 along = (eaveB - eaveA).Normalized();
        float length = eaveA.DistanceTo(eaveB);
        Vector2 Pt(float t, float e) => eaveA + (along * t) + (inward * e);
        var courses = new List<Vector2>();
        var joints = new List<Vector2>();

        void Add(List<Vector2> into, Vector2 a, Vector2 b)
        {
            if (ClipToConvex(a, b, face) is (Vector2 from, Vector2 to) && from.DistanceSquaredTo(to) > 1e-6f)
            {
                into.Add(frame.At(from));
                into.Add(frame.At(to));
            }
        }

        int n = 0;
        switch (material)
        {
            case RoofMaterial.Thatch:
                // Strands down the slope, a little bent, in clumps; a dark fringe along the eave; a binding every so often.
                int strands = Mathf.CeilToInt(length * height / (joint * 0.09f));
                for (int i = 0; i < strands; i++)
                {
                    float e0 = Hash01(seed, i, 1) * height;
                    float t0 = Hash01(seed, i, 2) * length;
                    float l = 0.05f + (Hash01(seed, i, 3) * 0.09f);
                    float bend = (Hash01(seed, i, 4) - 0.5f) * 0.02f;
                    Add(joints, Pt(t0, e0), Pt(t0 + bend, e0 + (l / 2f)));
                    Add(joints, Pt(t0 + bend, e0 + (l / 2f)), Pt(t0 + (bend * 1.3f), e0 + l));
                }

                for (float t = 0f; t < length; t += 0.02f)
                {
                    Add(courses, Pt(t, 0f), Pt(t + ((Hash01(seed, n, 5) - 0.5f) * 0.01f), 0.035f + (Hash01(seed, n++, 6) * 0.03f)));
                }

                for (float e = course; e < height; e += course * 1.7f)
                {
                    Add(courses, Pt(0, e + ((Hash01(seed, n, 7) - 0.5f) * 0.01f)), Pt(length / 2f, e + ((Hash01(seed, n, 8) - 0.5f) * 0.01f)));
                    Add(courses, Pt(length / 2f, e + ((Hash01(seed, n, 8) - 0.5f) * 0.01f)), Pt(length, e + ((Hash01(seed, n++, 9) - 0.5f) * 0.01f)));
                }

                pieces.Add(new RoofPiece(RoofPieceKind.Lines, part + " pen", joints.ToArray(), Ink with { A = 0.33f }, Spacing: 0.03f));
                pieces.Add(new RoofPiece(RoofPieceKind.Lines, part + " pen", courses.ToArray(), Ink with { A = 0.45f }, Spacing: 0.02f));
                return;

            case RoofMaterial.Boards:
                for (float t = joint; t < length; t += joint)
                {
                    Add(joints, Pt(t, 0f), Pt(t, height));
                }

                pieces.Add(new RoofPiece(RoofPieceKind.Lines, part + " pen", joints.ToArray(), Ink with { A = 0.45f }, Spacing: joint));
                return;

            case RoofMaterial.Turf:
                int tufts = Mathf.CeilToInt(length * height / (joint * joint));
                for (int i = 0; i < tufts; i++)
                {
                    float t = Hash01(seed, i, 1) * length;
                    float e = Hash01(seed, i, 2) * height;
                    Add(joints, Pt(t, e), Pt(t + 0.02f, e + 0.03f));
                    Add(joints, Pt(t, e), Pt(t - 0.02f, e + 0.03f));
                }

                pieces.Add(new RoofPiece(RoofPieceKind.Lines, part + " pen", joints.ToArray(), Ink with { A = 0.4f }, Spacing: joint));
                return;

            default:
                // Courses by hand — each in five pieces, a little off the last — and the joints between shingles, uneven.
                int row = 0;
                for (float e = course; e < height; e += course * (0.9f + (Hash01(seed, row, 11) * 0.2f)))
                {
                    Vector2 prev = Pt(-0.05f, e + ((Hash01(seed, row, 12) - 0.5f) * 0.01f));
                    for (int k = 1; k <= 5; k++)
                    {
                        Vector2 next = Pt(-0.05f + ((length + 0.1f) * k / 5f), e + ((Hash01(seed, row * 7 + k, 13) - 0.5f) * 0.012f));
                        Add(courses, prev, next);
                        prev = next;
                    }

                    float t = -0.05f + ((row & 1) == 1 ? joint / 2f : 0f) + (Hash01(seed, row, 14) * joint * 0.5f);
                    int j = 0;
                    while (t < length + 0.05f)
                    {
                        float top = e - (course * (0.9f + (Hash01(seed, row * 31 + j, 15) * 0.1f)));
                        Add(joints, Pt(t, top), Pt(t + ((Hash01(seed, row * 31 + j, 16) - 0.5f) * 0.008f), e - (course * (0.1f + (Hash01(seed, row * 31 + j, 17) * 0.2f)))));
                        t += joint * (0.7f + (Hash01(seed, row * 31 + j++, 18) * 0.6f));
                    }

                    row++;
                }

                pieces.Add(new RoofPiece(RoofPieceKind.Lines, part + " pen", courses.ToArray(), Ink with { A = 0.5f }, Spacing: course));
                pieces.Add(new RoofPiece(RoofPieceKind.Lines, part + " pen", joints.ToArray(), Ink with { A = 0.45f }, Spacing: joint));
                return;
        }
    }

    // ------------------------------------------------------------------ the drawing kit (local tiles, in the building's frame)

    /// <summary>A pen and brush in a building's frame: every shape is a convex polygon, fanned (A85's runs).</summary>
    private readonly struct YardKit
    {
        private readonly List<RoofPiece> _pieces;
        private readonly RoofFrame _frame;

        public YardKit(List<RoofPiece> pieces, RoofFrame frame)
        {
            _pieces = pieces;
            _frame = frame;
        }

        public RoofFrame Frame => _frame;

        public List<RoofPiece> Pieces => _pieces;

        public Vector2[] On(Vector2[] local)
        {
            var screen = new Vector2[local.Length];
            for (int i = 0; i < local.Length; i++)
            {
                screen[i] = _frame.At(local[i]);
            }

            return screen;
        }

        public void Fill(string part, Vector2[] local, Color colour) => _pieces.Add(new RoofPiece(RoofPieceKind.Fill, part, On(local), colour));

        public void Outline(string part, Vector2[] local, float alpha = 0.85f, float width = 1f) =>
            _pieces.Add(new RoofPiece(RoofPieceKind.Line, part, On(local), Ink with { A = alpha }, Width: width, Closed: true));

        public void Thing(string part, Vector2[] local, Color colour, float ink = 0.85f)
        {
            Fill(part, local, colour);
            if (ink > 0f)
            {
                Outline(part, local, ink);
            }
        }

        public void Line(string part, Vector2 a, Vector2 b, Color colour, float width = 1f) =>
            _pieces.Add(new RoofPiece(RoofPieceKind.Line, part, new[] { _frame.At(a), _frame.At(b) }, colour, Width: width));

        public void Strokes(string part, List<Vector2> localPairs, Color colour, float spacing = 0f, float width = 1f)
        {
            if (localPairs.Count > 0)
            {
                _pieces.Add(new RoofPiece(RoofPieceKind.Lines, part, On(localPairs.ToArray()), colour, Width: width, Spacing: spacing));
            }
        }

        /// <summary>A soft shadow under a thing, to the lower right (A85's light).</summary>
        public void Shade(string part, Vector2[] local, float lift = 0.4f)
        {
            Vector2 off = _frame.Local(new Vector2(0.13f, 0.17f) * lift);
            var moved = new Vector2[local.Length];
            for (int i = 0; i < local.Length; i++)
            {
                moved[i] = local[i] + off;
            }

            _pieces.Add(new RoofPiece(RoofPieceKind.Fill, part, On(moved), RoofShadow with { A = 0.22f }));
        }
    }

    private static Vector2[] Box(Vector2 at, float w, float h, float turn)
    {
        var corners = new[] { new Vector2(-w / 2, -h / 2), new Vector2(w / 2, -h / 2), new Vector2(w / 2, h / 2), new Vector2(-w / 2, h / 2) };
        for (int i = 0; i < 4; i++)
        {
            corners[i] = at + corners[i].Rotated(turn);
        }

        return corners;
    }

    private static Vector2[] Oval(Vector2 at, float rx, float ry, float turn, int n = 14)
    {
        var points = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.Tau / n;
            points[i] = at + new Vector2(rx * Mathf.Cos(a), ry * Mathf.Sin(a)).Rotated(turn);
        }

        return points;
    }

    /// <summary>A star-shaped blob as a fan from its centre (so a wobbly edge still fans right).</summary>
    private static Vector2[] Blob(Vector2 at, float rx, float ry, float turn, int seed, float wobble, int n = 18)
    {
        var points = new Vector2[n + 2];
        points[0] = at;
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.Tau / n;
            float k = 1f + ((Hash01(seed, i, 21) - 0.5f) * 2f * wobble);
            points[i + 1] = at + new Vector2(rx * k * Mathf.Cos(a), ry * k * Mathf.Sin(a)).Rotated(turn);
        }

        points[n + 1] = points[1];
        return points;
    }

    private static Vector2 Tn(Vector2 at, float x, float y, float turn) => at + new Vector2(x, y).Rotated(turn);

    // ------------------------------------------------------------------ the things (§3.2, §4.2)

    /// <summary>
    /// ⭐ The trodden ground of a yard (§5): an irregular patch, softened at its edge by two fainter rings of it, with the
    /// pen's stipple, a few pebbles and tufts.
    /// </summary>
    private static void YardGround(YardKit k, Vector2 at, float halfLong, float halfShort, float turn, int seed, Color? earth = null, float alpha = 0.5f)
    {
        Color colour = (earth ?? TroddenEarth) with { A = alpha * 0.45f };
        int n = 28;
        foreach (float grow in new[] { 1.06f, 1f, 0.93f })
        {
            var points = new Vector2[n + 2];
            points[0] = at;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.Tau / n;
                float ca = Mathf.Cos(a);
                float sa = Mathf.Sin(a);
                float square = 1f / Mathf.Pow(Mathf.Pow(Mathf.Abs(ca), 4f) + Mathf.Pow(Mathf.Abs(sa), 4f), 0.25f);
                float wobble = 0.92f + (Hash01(seed, i, 31) * 0.12f);
                points[i + 1] = at + new Vector2(halfLong * ca * square * wobble * grow, halfShort * sa * square * wobble * grow).Rotated(turn);
            }

            points[n + 1] = points[1];
            k.Fill("ground", points, colour);
        }

        var stipple = new List<Vector2>();
        int dots = Mathf.CeilToInt(halfLong * halfShort * 4f / (0.06f * 0.06f));
        for (int i = 0; i < dots; i++)
        {
            Vector2 p = Tn(at, (Hash01(seed, i, 32) * 2f - 1f) * halfLong * 0.9f, (Hash01(seed, i, 33) * 2f - 1f) * halfShort * 0.9f, turn);
            stipple.Add(p);
            stipple.Add(p + new Vector2(0.012f, 0.008f));
        }

        k.Strokes("ground stipple", stipple, Ink with { A = 0.4f }, 0.06f);
        int bits = Mathf.CeilToInt(halfLong * halfShort * 4f * 5f) + 2;
        var tufts = new List<Vector2>();
        for (int i = 0; i < bits; i++)
        {
            Vector2 p = Tn(at, (Hash01(seed, i, 34) * 2f - 1f) * halfLong * 0.85f, (Hash01(seed, i, 35) * 2f - 1f) * halfShort * 0.85f, turn);
            if (Hash01(seed, i, 36) < 0.5f)
            {
                k.Thing("ground pebble", Oval(p, 0.016f, 0.013f, 0f, 8), YardStone, 0.6f);
            }
            else
            {
                foreach (float dx in new[] { -0.02f, 0f, 0.02f })
                {
                    tufts.Add(p);
                    tufts.Add(p + new Vector2(dx, -0.035f));
                }
            }
        }

        k.Strokes("ground tuft", tufts, Ink with { A = 0.55f });
    }

    private static void Log(YardKit k, string part, Vector2 at, float turn, float length = 0.6f, float r = 0.07f)
    {
        k.Thing(part, Box(at, length, 2 * r, turn), Bark, 0.8f);
        k.Line(part, Tn(at, (-length / 2) + 0.03f, 0, turn), Tn(at, (length / 2) - 0.05f, 0, turn), TimberDark with { A = 0.5f });
        k.Thing(part, Oval(Tn(at, length / 2, 0, turn), 0.03f, r, turn, 10), EndGrain, 0.8f);
    }

    private static void LogPile(YardKit k, Vector2 at, float turn, int logs = 4)
    {
        const float r = 0.068f;
        k.Shade("log pile shadow", Box(at, 0.65f, logs * 2.05f * r, turn));
        for (int i = 0; i < logs; i++)
        {
            Log(k, "log pile", Tn(at, 0, (i - ((logs - 1) / 2f)) * 2.05f * r, turn), turn, 0.68f, r);
        }

        for (int i = 0; i < logs - 1; i++)
        {
            Log(k, "log pile", Tn(at, 0.03f, (i - ((logs - 2) / 2f)) * 2.05f * r, turn), turn, 0.62f, r * 0.95f);
        }
    }

    /// <summary>Split wood stacked, its length by its level (§4.2): 0 draws nothing.</summary>
    private static void Firewood(YardKit k, string part, Vector2 at, float turn, float fullLength, int level)
    {
        if (level <= 0)
        {
            return;
        }

        float length = Mathf.Max(0.14f, fullLength * level / 4f);
        const float depth = 0.18f;
        k.Thing(part, Box(at, length, depth, turn), TimberDark, 0.85f);
        int pieces = Mathf.Max(2, Mathf.RoundToInt(length / 0.06f));
        for (int i = 0; i < pieces; i++)
        {
            float x = (-length / 2) + ((i + 0.5f) * length / pieces);
            k.Fill(part, Box(Tn(at, x, 0, turn), length / pieces * 0.8f, depth * 0.8f, turn), (i & 1) == 1 ? EndGrain : new Color("#c9a46e"));
        }
    }

    private static void ChoppingBlock(YardKit k, Vector2 at)
    {
        k.Thing("chopping block", Oval(at, 0.09f, 0.09f, 0f, 12), EndGrain, 0.85f);
        k.Line("chopping block", at + new Vector2(0.014f, -0.014f), at + new Vector2(0.18f, -0.1f), TimberDark, 2f);
        k.Thing("chopping block", Box(at + new Vector2(0.007f, -0.007f), 0.07f, 0.04f, -0.5f), YardIron, 0.7f);
    }

    private static void Sawhorse(YardKit k, Vector2 at, float turn)
    {
        var legs = new List<Vector2>();
        foreach (float x in new[] { -0.16f, 0.16f })
        {
            legs.AddRange(new[] { Tn(at, x - 0.05f, -0.07f, turn), Tn(at, x + 0.05f, 0.07f, turn), Tn(at, x - 0.05f, 0.07f, turn), Tn(at, x + 0.05f, -0.07f, turn) });
        }

        k.Strokes("sawhorse", legs, TimberDark, 0f, 1.5f);
        Log(k, "sawhorse", at, turn, 0.44f, 0.05f);
        k.Line("sawhorse saw", Tn(at, -0.15f, 0.13f, turn), Tn(at, 0.15f, 0.13f, turn), new Color("#cfd2d4"));
    }

    private static void Stump(YardKit k, Vector2 at, float r = 0.08f)
    {
        k.Thing("stump", Oval(at, r, r, 0f, 12), new Color("#bf9a64"), 0.85f);
        k.Pieces.Add(new RoofPiece(RoofPieceKind.Line, "stump", k.On(Oval(at, r * 0.55f, r * 0.55f, 0f, 10)), TimberDark with { A = 0.5f }, Closed: true));
    }

    private static void Chips(YardKit k, Vector2 at, float r, int count, int seed)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 p = at + new Vector2((Hash01(seed, i, 41) * 2f - 1f) * r, (Hash01(seed, i, 42) * 2f - 1f) * r);
            k.Fill("chips", Box(p, 0.045f, 0.022f, Hash01(seed, i, 43) * 3f), EndGrain);
        }
    }

    private static void Planks(YardKit k, Vector2 at, float turn, float length = 0.7f, float depth = 0.25f)
    {
        k.Shade("planks shadow", Box(at, length, depth, turn));
        k.Thing("planks", Box(at, length, depth, turn), PlankColour, 0.85f);
        var lines = new List<Vector2>();
        for (int i = 1; i < 4; i++)
        {
            float y = (-depth / 2) + (i * depth / 4);
            lines.Add(Tn(at, -length / 2, y, turn));
            lines.Add(Tn(at, length / 2, y, turn));
        }

        k.Strokes("planks", lines, TimberDark with { A = 0.55f });
    }

    private static void Stones(YardKit k, string part, Vector2 at, int count, int seed, float size, Color colour, float spread)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 p = at + new Vector2((Hash01(seed, i, 51) * 2f - 1f) * spread, (Hash01(seed, i, 52) * 2f - 1f) * spread);
            float s = size * (0.75f + (0.5f * Hash01(seed, i, 53)));
            k.Thing(part, Box(p, s, s * 0.8f, Hash01(seed, i, 54) * 0.6f), colour, 0.8f);
        }
    }

    private static void Sack(YardKit k, string part, Vector2 at, float turn, Color colour)
    {
        k.Thing(part, Oval(at, 0.085f, 0.064f, turn, 12), colour, 0.8f);
        k.Fill(part + " tie", Oval(Tn(at, 0.064f, 0, turn), 0.017f, 0.017f, 0f, 6), TimberDark);
    }

    private static void Sacks(YardKit k, string part, Vector2 at, float turn, int count, int seed, Color colour)
    {
        for (int i = 0; i < count; i++)
        {
            Sack(k, part, Tn(at, ((i % 3) * 0.14f) - 0.14f + (Hash01(seed, i, 61) * 0.03f), (i / 3) * 0.12f, turn), turn + Hash01(seed, i, 62), colour);
        }
    }

    private static void Barrel(YardKit k, string part, Vector2 at, float r, Color? top = null)
    {
        k.Thing(part, Oval(at, r, r, 0f, 14), Timber, 0.9f);
        k.Pieces.Add(new RoofPiece(RoofPieceKind.Line, part + " hoop", k.On(Oval(at, r * 0.72f, r * 0.72f, 0f, 12)), TimberDark with { A = 0.7f }, Closed: true));
        k.Fill(part + " hoop", Oval(at, r * 0.5f, r * 0.5f, 0f, 10), top ?? TimberLight);
    }

    private static void Crate(YardKit k, string part, Vector2 at, float turn, float s = 0.17f)
    {
        k.Thing(part, Box(at, s, s, turn), TimberLight, 0.85f);
        k.Strokes(part, new List<Vector2> { Tn(at, -s / 2, -s / 2, turn), Tn(at, s / 2, s / 2, turn), Tn(at, -s / 2, s / 2, turn), Tn(at, s / 2, -s / 2, turn) }, TimberDark with { A = 0.7f });
    }

    /// <summary>A handcart from above (the game's <see cref="HandcartColour"/>): a bed, two wheels, two shafts.</summary>
    private static void Handcart(YardKit k, string part, Vector2 at, float turn, float scale = 1f)
    {
        foreach (float y in new[] { -0.135f, 0.135f })
        {
            k.Fill(part, Box(Tn(at, 0, y * scale, turn), 0.2f * scale, 0.035f * scale, turn), new Color("#2f2b28"));
        }

        k.Thing(part, Box(at, 0.34f * scale, 0.22f * scale, turn), HandcartColour, 0.9f);
        var boards = new List<Vector2>();
        foreach (float x in new[] { -0.08f, 0f, 0.08f })
        {
            boards.Add(Tn(at, x * scale, -0.1f * scale, turn));
            boards.Add(Tn(at, x * scale, 0.1f * scale, turn));
        }

        k.Strokes(part, boards, new Color("#3e5258") with { A = 0.6f });
        k.Strokes(part, new List<Vector2>
        {
            Tn(at, 0.17f * scale, -0.09f * scale, turn), Tn(at, 0.4f * scale, -0.04f * scale, turn),
            Tn(at, 0.17f * scale, 0.09f * scale, turn), Tn(at, 0.4f * scale, 0.04f * scale, turn),
        }, TimberDark, 0f, 1.5f);
    }

    private static void Dock(YardKit k, Vector2 at, float turn, float length, float width = 0.24f)
    {
        k.Shade("dock shadow", Box(at, length, width, turn), 0.5f);
        k.Thing("dock", Box(at, length, width, turn), new Color("#a8875e"), 0.85f);
        var boards = new List<Vector2>();
        int count = Mathf.Max(2, Mathf.RoundToInt(length / 0.07f));
        for (int i = 1; i < count; i++)
        {
            float x = (-length / 2) + (i * length / count);
            boards.Add(Tn(at, x, -width / 2, turn));
            boards.Add(Tn(at, x, width / 2, turn));
        }

        k.Strokes("dock", boards, TimberDark with { A = 0.5f }, 0.07f);
        foreach (float x in new[] { (-length / 2) + 0.05f, (length / 2) - 0.04f })
        {
            foreach (float y in new[] { (-width / 2) - 0.02f, (width / 2) + 0.02f })
            {
                k.Thing("dock post", Oval(Tn(at, x, y, turn), 0.03f, 0.03f, 0f, 8), TimberDark, 0.6f);
            }
        }
    }

    private static void Boat(YardKit k, Vector2 at, float turn)
    {
        const float l = 0.7f;
        const float w = 0.24f;
        var hull = new[] { new Vector2(-l / 2, 0), new Vector2(-l / 4, -w / 2), new Vector2(l / 4, -w / 2), new Vector2(l / 2, 0), new Vector2(l / 4, w / 2), new Vector2(-l / 4, w / 2) };
        var outer = new Vector2[hull.Length];
        var inner = new Vector2[hull.Length];
        for (int i = 0; i < hull.Length; i++)
        {
            outer[i] = at + hull[i].Rotated(turn);
            inner[i] = at + (new Vector2(hull[i].X * 0.78f, hull[i].Y * 0.62f)).Rotated(turn);
        }

        k.Shade("boat shadow", outer, 0.3f);
        k.Thing("boat", outer, BoatHull, 0.9f);
        k.Fill("boat", inner, new Color("#9a7a56"));
        k.Strokes("boat", new List<Vector2> { Tn(at, -0.1f, -0.07f, turn), Tn(at, -0.1f, 0.07f, turn), Tn(at, 0.1f, -0.07f, turn), Tn(at, 0.1f, 0.07f, turn) }, TimberDark, 0f, 1.2f);
    }

    private static void NetFrame(YardKit k, Vector2 at, float turn, float length = 0.6f)
    {
        const float h = 0.2f;
        k.Thing("net", Box(at, length, h, turn), NetTwine with { A = 0.75f }, 0.5f);
        var mesh = new List<Vector2>();
        for (int i = 1; i < 9; i++)
        {
            float x = (-length / 2) + (i * length / 9);
            mesh.AddRange(new[] { Tn(at, x - 0.035f, -h / 2, turn), Tn(at, x + 0.035f, h / 2, turn), Tn(at, x + 0.035f, -h / 2, turn), Tn(at, x - 0.035f, h / 2, turn) });
        }

        k.Strokes("net", mesh, new Color("#5f5444") with { A = 0.55f }, 0.035f);
        k.Line("net pole", Tn(at, (-length / 2) - 0.04f, 0, turn), Tn(at, (length / 2) + 0.04f, 0, turn), TimberDark, 1.5f);
    }

    /// <summary>A rack on two feet with things hung on it (fish, meat, herbs) — <paramref name="hung"/> of them.</summary>
    private static void Rack(YardKit k, string part, Vector2 at, float turn, float length, int hung, Color colour, Vector2 size)
    {
        k.Strokes(part, new List<Vector2>
        {
            Tn(at, -length / 2, 0, turn), Tn(at, length / 2, 0, turn),
            Tn(at, -length / 2, -0.09f, turn), Tn(at, -length / 2, 0.09f, turn),
            Tn(at, length / 2, -0.09f, turn), Tn(at, length / 2, 0.09f, turn),
        }, TimberDark, 0f, 1.5f);
        for (int i = 0; i < hung; i++)
        {
            float x = (-length / 2) + ((i + 0.5f) * length / hung);
            k.Thing(part + " hung", Oval(Tn(at, x, 0.045f, turn), size.X, size.Y, turn, 10), colour, 0.7f);
        }
    }

    private static void Creels(YardKit k, Vector2 at)
    {
        for (int i = 0; i < 3; i++)
        {
            Vector2 c = at + new Vector2(i * 0.1f, (i % 2) * 0.065f);
            k.Thing("creel", Oval(c, 0.055f, 0.055f, 0f, 10), new Color("#8c7448"), 0.8f);
            k.Strokes("creel", new List<Vector2> { c + new Vector2(-0.04f, 0), c + new Vector2(0.04f, 0), c + new Vector2(0, -0.04f), c + new Vector2(0, 0.04f) }, TimberDark with { A = 0.6f });
        }
    }

    private static void HideFrame(YardKit k, Vector2 at, float turn, bool hide, int seed)
    {
        const float s = 0.42f;
        k.Pieces.Add(new RoofPiece(RoofPieceKind.Line, "hide frame", k.On(Box(at, s, s, turn)), TimberDark, Width: 2f, Closed: true));
        if (hide)
        {
            k.Thing("hide", Blob(at, s * 0.36f, s * 0.4f, turn, seed, 0.18f, 16), HideColour, 0.8f);
            var laces = new List<Vector2>();
            foreach ((float x, float y) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
            {
                laces.Add(Tn(at, x * s * 0.3f, y * s * 0.32f, turn));
                laces.Add(Tn(at, x * s / 2, y * s / 2, turn));
            }

            k.Strokes("hide", laces, TimberDark with { A = 0.7f });
        }
    }

    private static void HayRick(YardKit k, Vector2 at, float r)
    {
        k.Shade("hay rick shadow", Oval(at, r, r, 0f, 16), 0.6f);
        k.Thing("hay rick", Oval(at, r, r, 0f, 18), Straw, 0.85f);
        k.Fill("hay rick", new[] { at + new Vector2(-r * 0.9f, -r * 0.1f), at + new Vector2(r * 0.1f, -r * 0.95f), at + new Vector2(-r * 0.7f, -r * 0.7f) }, new Color("#efdb92") with { A = 0.6f });
        var straws = new List<Vector2>();
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.Pi / 5f;
            var way = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            straws.Add(at + (way * r * 0.2f));
            straws.Add(at + (way * r * 0.9f));
        }

        k.Strokes("hay rick", straws, new Color("#a68a3c") with { A = 0.5f });
    }

    private static void WashingLine(YardKit k, Vector2 from, Vector2 to, int seed)
    {
        k.Thing("washing line post", Oval(from, 0.028f, 0.028f, 0f, 8), TimberDark, 0.6f);
        k.Thing("washing line post", Oval(to, 0.028f, 0.028f, 0f, 8), TimberDark, 0.6f);
        k.Line("washing line", from, to, new Color("#e8e2d4") with { A = 0.9f });
        float turn = (to - from).Angle();
        int cloths = 3 + (int)(Hash01(seed, 0, 71) * 3f);
        for (int i = 0; i < cloths; i++)
        {
            Vector2 c = from.Lerp(to, (i + 0.7f) / (cloths + 0.6f));
            Color colour = Linen[(int)(Hash01(seed, i, 72) * Linen.Length) % Linen.Length];
            k.Thing("washing", Box(Tn(c, 0, 0.04f, turn), 0.09f, 0.08f, turn), colour, 0.6f);
        }
    }

    private static void Bench(YardKit k, Vector2 at, float turn) => k.Thing("bench", Box(at, 0.34f, 0.085f, turn), TimberLight, 0.8f);

    private static void Heap(YardKit k, string part, Vector2 at, float r, Color colour, Color dots, int seed)
    {
        k.Shade(part + " shadow", Oval(at, r, r * 0.85f, 0f, 14), 0.3f);
        k.Thing(part, Blob(at, r, r * 0.85f, 0f, seed, 0.15f), colour, 0.8f);
        for (int i = 0; i < 9; i++)
        {
            k.Fill(part, Oval(at + new Vector2((Hash01(seed, i, 81) - 0.5f) * r, (Hash01(seed, i, 82) - 0.5f) * r * 0.8f), r * 0.13f, r * 0.13f, 0f, 6), dots);
        }
    }

    private static void Oven(YardKit k, Vector2 at, float r)
    {
        k.Shade("oven shadow", Oval(at, r, r, 0f, 14), 0.5f);
        k.Thing("oven", Oval(at, r, r, 0f, 18), OvenClay, 0.9f);
        k.Fill("oven", new[] { at + new Vector2(-r * 0.8f, -r * 0.2f), at + new Vector2(-r * 0.1f, -r * 0.85f), at + new Vector2(-r * 0.55f, -r * 0.6f) }, new Color("#d99a72") with { A = 0.6f });
        k.Thing("oven mouth", Box(at + new Vector2(0, r * 0.85f), r * 0.6f, r * 0.35f, 0f), new Color("#2a1d17"), 0.6f);
    }

    private static void Bales(YardKit k, Vector2 at, float turn, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 c = Tn(at, (i % 2) * 0.17f, (i / 2) * 0.13f, turn);
            k.Thing("bale", Box(c, 0.16f, 0.11f, turn), Wool, 0.8f);
        }
    }

    private static void Basket(YardKit k, Vector2 at, Color? fill, int seed)
    {
        k.Thing("basket", Oval(at, 0.085f, 0.085f, 0f, 12), Wicker, 0.8f);
        if (fill is Color full)
        {
            k.Fill("basket goods", Oval(at, 0.06f, 0.06f, 0f, 10), full);
            for (int i = 0; i < 4; i++)
            {
                k.Fill("basket goods", Oval(at + new Vector2((Hash01(seed, i, 91) - 0.5f) * 0.07f, (Hash01(seed, i, 92) - 0.5f) * 0.07f), 0.015f, 0.015f, 0f, 6), full.Darkened(0.3f));
            }
        }
    }

    private static void SeedlingBed(YardKit k, Vector2 at, float turn, float length, float depth, int seed)
    {
        k.Thing("seedling bed", Box(at, length, depth, turn), new Color("#6e5238"), 0.7f);
        int rows = Mathf.Max(2, Mathf.RoundToInt(depth / 0.07f));
        int cols = Mathf.Max(2, Mathf.RoundToInt(length / 0.07f));
        for (int j = 0; j < rows; j++)
        {
            for (int i = 0; i < cols; i++)
            {
                float x = (-length / 2) + ((i + 0.5f) * length / cols);
                float y = (-depth / 2) + ((j + 0.5f) * depth / rows);
                float r = 0.022f + (0.008f * Hash01(seed, (j * 31) + i, 95));
                k.Fill("seedling", Oval(Tn(at, x, y, turn), r, r, 0f, 6), Seedling.Lightened(0.15f * Hash01(seed, (j * 31) + i, 96)));
            }
        }
    }

    private static void ToolRack(YardKit k, Vector2 at, float turn, float length = 0.44f)
    {
        var lines = new List<Vector2> { Tn(at, -length / 2, 0, turn), Tn(at, length / 2, 0, turn) };
        for (int i = 0; i < 4; i++)
        {
            float x = (-length / 2) + ((i + 0.5f) * length / 4);
            lines.Add(Tn(at, x, 0, turn));
            lines.Add(Tn(at, x, 0.14f, turn));
            k.Fill("tool", Box(Tn(at, x, 0.15f, turn), 0.045f, 0.026f, turn), YardIron);
        }

        k.Strokes("tool rack", lines, Timber, 0f, 1.3f);
    }

    private static void Ladder(YardKit k, Vector2 at, float turn, float length = 0.55f)
    {
        var lines = new List<Vector2>();
        foreach (float y in new[] { -0.045f, 0.045f })
        {
            lines.Add(Tn(at, -length / 2, y, turn));
            lines.Add(Tn(at, length / 2, y, turn));
        }

        for (int i = 0; i < 6; i++)
        {
            float x = (-length / 2) + ((i + 0.5f) * length / 6);
            lines.Add(Tn(at, x, -0.045f, turn));
            lines.Add(Tn(at, x, 0.045f, turn));
        }

        k.Strokes("ladder", lines, Timber, 0f, 1.2f);
    }

    private static void Paving(YardKit k, Vector2 at, float halfLong, float halfShort, float turn, int seed)
    {
        Vector2[] slab = Box(at, halfLong * 2, halfShort * 2, turn);
        k.Thing("paving", slab, Pavement, 0.4f);
        var joints = new List<Vector2>();
        int row = 0;
        for (float y = -halfShort + 0.09f; y < halfShort - 0.02f; y += 0.09f)
        {
            joints.Add(Tn(at, -halfLong, y, turn));
            joints.Add(Tn(at, halfLong, y, turn));
            float x = -halfLong + (Hash01(seed, row, 101) * 0.12f);
            int j = 0;
            while (x < halfLong)
            {
                joints.Add(Tn(at, x, y - 0.09f, turn));
                joints.Add(Tn(at, x, y, turn));
                x += 0.12f + (Hash01(seed, (row * 17) + j++, 102) * 0.06f);
            }

            row++;
        }

        k.Strokes("paving", joints, new Color("#9d9585") with { A = 0.5f }, 0.09f);
    }

    private static void Flag(YardKit k, Vector2 at, Color colour)
    {
        k.Thing("flag pole", Oval(at, 0.035f, 0.035f, 0f, 8), TimberDark, 0.7f);
        var cloth = new[] { at + new Vector2(0, -0.015f), at + new Vector2(0.32f, -0.055f), at + new Vector2(0.29f, 0.085f), at + new Vector2(0, 0.1f) };
        k.Shade("flag shadow", cloth, 0.4f);
        k.Thing("flag", cloth, colour, 0.8f);
    }

    private static void NoticeBoard(YardKit k, Vector2 at, float turn)
    {
        k.Thing("notice board", Box(at, 0.32f, 0.1f, turn), Timber, 0.8f);
        foreach (float x in new[] { -0.09f, 0f, 0.09f })
        {
            k.Fill("notice", Box(Tn(at, x, 0, turn), 0.055f, 0.065f, turn), new Color("#efe8d6"));
        }
    }

    private static void Trough(YardKit k, Vector2 at, float turn, float length = 0.5f)
    {
        k.Thing("trough", Box(at, length, 0.14f, turn), Timber, 0.85f);
        k.Fill("trough water", Box(at, length - 0.05f, 0.085f, turn), YardWater);
    }

    private static void Barrow(YardKit k, Vector2 at, float turn, Color? load)
    {
        k.Thing("barrow", new[] { Tn(at, -0.13f, -0.08f, turn), Tn(at, 0.08f, -0.08f, turn), Tn(at, 0.12f, 0, turn), Tn(at, 0.08f, 0.08f, turn), Tn(at, -0.13f, 0.08f, turn) }, TimberLight, 0.85f);
        k.Fill("barrow", Box(Tn(at, 0.16f, 0, turn), 0.08f, 0.028f, turn), new Color("#2f2b28"));
        k.Strokes("barrow", new List<Vector2> { Tn(at, -0.13f, -0.065f, turn), Tn(at, -0.27f, -0.08f, turn), Tn(at, -0.13f, 0.065f, turn), Tn(at, -0.27f, 0.08f, turn) }, TimberDark, 0f, 1.3f);
        if (load is Color c)
        {
            k.Thing("barrow load", Oval(Tn(at, -0.015f, 0, turn), 0.06f, 0.06f, 0f, 10), c, 0.6f);
        }
    }

    private static void ClothLine(YardKit k, Vector2 from, Vector2 to, bool cloth)
    {
        k.Thing("cloth line post", Oval(from, 0.03f, 0.03f, 0f, 8), TimberDark, 0.6f);
        k.Thing("cloth line post", Oval(to, 0.03f, 0.03f, 0f, 8), TimberDark, 0.6f);
        k.Line("cloth line", from, to, new Color("#e8e2d4") with { A = 0.9f });
        float turn = (to - from).Angle();
        float length = from.DistanceTo(to);
        for (int i = 0; cloth && i < Dyes.Length; i++)
        {
            Vector2 c = from.Lerp(to, (i + 0.5f) / Dyes.Length);
            k.Thing("cloth", Box(Tn(c, 0, 0.07f, turn), 0.75f / Dyes.Length * length, 0.14f, turn), Dyes[i], 0.6f);
        }
    }

    private static void Sundial(YardKit k, Vector2 at)
    {
        k.Thing("sundial", Oval(at, 0.065f, 0.065f, 0f, 12), YardStone, 0.8f);
        k.Line("sundial", at, at + new Vector2(0.04f, -0.04f), YardIron, 1.3f);
    }

    private static void Wheel(YardKit k, Vector2 at, float r)
    {
        k.Pieces.Add(new RoofPiece(RoofPieceKind.Line, "wheel", k.On(Oval(at, r, r, 0f, 18)), TimberDark, Width: 2f, Closed: true));
        var spokes = new List<Vector2>();
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.Pi / 3f;
            spokes.Add(at);
            spokes.Add(at + (new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r));
        }

        k.Strokes("wheel", spokes, Timber);
    }

    /// <summary>
    /// ⭐ The founders' cart (A87, `building-yards.md §3.4`), seen straight down on its 1 × 1 ground, its length along x and its
    /// shafts forward (+x): a hatched shadow; the two wheels at its sides, iron-tyred, their hubs out; the bed in the carts'
    /// colour, boards along it, rails strapped with iron; the shafts and a crossbar; the load — sacks and an open basket while
    /// it holds food, crates while it holds tools (§2) — and the canvas over the back third; a bucket hung at the back.
    /// </summary>
    private static void Wagon(List<RoofPiece> pieces, RoofFrame frame, YardState s, int seed)
    {
        var k = new YardKit(pieces, frame);
        const float back = -0.36f;
        const float front = 0.2f;
        const float half = 0.2f;
        float length = front - back;
        var bedAt = new Vector2((front + back) / 2f, 0f);
        Vector2[] bed = Box(bedAt, length, half * 2f, 0f);

        // The shadow, hatched, under the bed and the wheels.
        Shadow(pieces, frame, (length + 0.06f) / 2f, half + 0.1f, bedAt + new Vector2(0.03f, 0f), 0.6f);

        // The shafts and their crossbar, under the bed's front.
        k.Strokes("wagon shaft", new List<Vector2>
        {
            new(front - 0.04f, -0.15f), new(0.49f, -0.07f), new(front - 0.04f, 0.15f), new(0.49f, 0.07f),
        }, TimberDark, 0f, 2.5f);
        k.Line("wagon shaft", new Vector2(0.44f, -0.08f), new Vector2(0.44f, 0.08f), TimberDark, 2f);

        // The wheels, iron-tyred rims seen edge on, the hubs standing out.
        foreach (float side in new[] { -1f, 1f })
        {
            Vector2 at = new(-0.08f, side * (half + 0.055f));
            k.Thing("wagon wheel", Box(at, 0.46f, 0.07f, 0f), Timber.Darkened(0.15f), 0.9f);
            k.Strokes("wagon wheel", new List<Vector2>
            {
                at + new Vector2(-0.23f, side * -0.02f), at + new Vector2(0.23f, side * -0.02f),
                at + new Vector2(-0.23f, side * 0.02f), at + new Vector2(0.23f, side * 0.02f),
            }, YardIron with { A = 0.8f }, 0f, 1f);
            k.Thing("wagon hub", Oval(at + new Vector2(0f, side * 0.05f), 0.04f, 0.04f, 0f, 10), YardIron, 0.8f);
        }

        // The bed: boards along it in the carts' own colour, a tailboard, rails strapped with iron.
        Color boards = Washed(HandcartColour);
        k.Thing("wagon bed", bed, boards, 0.9f);
        var joints = new List<Vector2>();
        for (float y = -half + 0.08f; y < half; y += 0.08f)
        {
            joints.Add(new Vector2(back, y));
            joints.Add(new Vector2(front, y));
        }

        k.Strokes("wagon bed", joints, new Color("#3e5258") with { A = 0.55f }, 0.08f);
        foreach (float side in new[] { -1f, 1f })
        {
            k.Thing("wagon rail", Box(new Vector2(bedAt.X, side * (half - 0.02f)), length + 0.04f, 0.04f, 0f), TimberDark, 0.7f);
            for (float x = back + 0.06f; x < front; x += 0.14f)
            {
                k.Fill("wagon strap", Box(new Vector2(x, side * (half - 0.02f)), 0.025f, 0.05f, 0f), YardIron);
            }
        }

        k.Thing("wagon rail", Box(new Vector2(back + 0.015f, 0f), 0.03f, half * 2f, 0f), TimberDark, 0.7f);

        // The load (§2): crates while it holds tools, sacks and a basket of food while it holds food.
        for (int i = 0; i < Mathf.Min(2, (int)s.Stock2); i++)
        {
            Crate(k, "wagon tools", new Vector2(-0.02f + (i * 0.02f), -0.08f + (i * 0.15f)), 0.08f * i, 0.15f);
        }

        if (s.Stock > 0)
        {
            Sacks(k, "wagon food", new Vector2(0.1f, -0.1f), Mathf.Pi / 2f, Mathf.Min(3, (int)s.Stock), seed, SackColour);
            Basket(k, new Vector2(0.13f, 0.11f), new Color("#c8b48a"), seed + 3);
        }

        // The canvas over the back third, its folds in pen, and the bucket hung at the back.
        Vector2[] canvas = Box(new Vector2(back + 0.12f, 0f), 0.26f, (half * 2f) + 0.06f, 0f);
        k.Thing("wagon canvas", canvas, new Color("#8a8f80"), 0.85f);
        var folds = new List<Vector2>();
        for (float x = back + 0.03f; x < back + 0.25f; x += 0.045f)
        {
            folds.Add(new Vector2(x, -half - 0.02f));
            folds.Add(new Vector2(x + 0.015f, half + 0.02f));
        }

        k.Strokes("wagon canvas", folds, Ink with { A = 0.4f }, 0.045f);
        HatchInto(pieces, frame, canvas, "wagon canvas", 0.6f);
        Barrel(k, "wagon bucket", new Vector2(back - 0.05f, half + 0.03f), 0.04f, YardWater);
    }

    /// <summary>A small roof (a privy, a woodshed) in the yard: A85's faces, pen and all.</summary>
    private static void Shed(List<RoofPiece> pieces, RoofFrame frame, Vector2 at, float hw, float hd, Color colour, int seed)
    {
        Shadow(pieces, frame, hw, hd, at, 0.6f);
        RoofFaces(pieces, frame, hw, hd, at, Washed(colour), RoofMaterial.Boards, (seed & 1) == 1, false, "shed", seed);
    }

    // ------------------------------------------------------------------ the yards (§3.2)

    /// <summary>
    /// ⭐⭐ A building's yard, in its frame: the ground first and the things on it, every stock thing only with its stock (§2).
    /// Drawn before the roof; <see cref="YardOver"/> draws what stands over it.
    /// </summary>
    private static void YardUnder(List<RoofPiece> pieces, RoofFrame frame, YardKind yard, YardLayout l, YardState s, int seed)
    {
        var k = new YardKit(pieces, frame);
        float t = l.Turn;
        switch (yard)
        {
            case YardKind.Woodcutter:
                YardGround(k, l.YardAt, l.YardLong, l.YardShort, t, seed);
                if (s.Working)
                {
                    LogPile(k, l.P(0.05f, -0.45f), t, 4);
                }

                Chips(k, l.P(-0.45f, 0.5f), 0.15f, 14, seed);
                ChoppingBlock(k, l.P(-0.5f, 0.55f));
                Sawhorse(k, l.P(0.4f, 0.5f), t + 0.1f);
                break;

            case YardKind.Builder:
                YardGround(k, l.YardAt, l.YardLong, l.YardShort, t, seed);
                if (s.Working)
                {
                    Planks(k, l.P(0.05f, -0.5f), t);
                    Stones(k, "cut stone", l.P(0.45f, 0.45f), 4, seed, 0.16f, YardStone, 0.12f);
                }

                Ladder(k, l.P(-0.3f, 0.5f), t + 0.25f);
                break;

            case YardKind.Hunter:
                YardGround(k, l.YardAt, l.YardLong, l.YardShort, t, seed);
                HideFrame(k, l.P(-0.15f, -0.35f), t + 0.1f, s.Stock2 > 0, seed);
                if (s.Stock > 0)
                {
                    Rack(k, "smoking rack", l.P(0.1f, 0.6f), t, 0.55f, 1 + s.Stock, MeatRed, new Vector2(0.045f, 0.065f));
                }

                Stump(k, l.P(0.7f, -0.45f));
                break;

            case YardKind.Forager:
                YardGround(k, l.YardAt, l.YardLong * 0.9f, l.YardShort, t, seed);
                Basket(k, l.P(-0.55f, 0.1f), s.Working ? new Color("#a3283a") : null, seed);
                Basket(k, l.P(-0.15f, 0.35f), s.Working ? new Color("#d8c7a6") : null, seed + 1);
                Basket(k, l.P(0.25f, 0.05f), s.Working ? new Color("#4f7a35") : null, seed + 2);
                if (s.Working)
                {
                    Rack(k, "herb rack", l.P(0.62f, 0.6f), t + (Mathf.Pi / 2f), 0.34f, 3, Seedling, new Vector2(0.04f, 0.055f));
                }

                break;

            case YardKind.Forester:
                YardGround(k, l.YardAt, l.YardLong * 0.9f, l.YardShort, t, seed);
                SeedlingBed(k, l.P(-0.2f, 0f), t, l.YardLong * 1.1f, l.YardShort * 1.3f, seed);
                break;

            case YardKind.Fisher:
                YardGround(k, l.YardAt, l.YardLong * 0.9f, l.YardShort, t, seed, null, 0.35f);
                NetFrame(k, l.P(-0.62f, 0.6f), t + (Mathf.Pi / 2f), 0.34f);
                Creels(k, l.P(-0.15f, 0.2f));
                if (s.Stock > 0)
                {
                    Rack(k, "fish rack", l.P(0.62f, 0.6f), t + (Mathf.Pi / 2f), 0.34f, 1 + s.Stock, FishSilver, new Vector2(0.032f, 0.07f));
                }

                if (s.Water >= 0)
                {
                    // The dock out over the water on its side (§3.3), the boat moored beside it.
                    Vector2 way = SideOut(s.Water);
                    Vector2 side = new(-way.Y, way.X);
                    float turn = way.Angle();
                    Dock(k, (way * 0.95f) + (side * 0.22f), turn, 0.9f);
                    Boat(k, (way * 1.05f) - (side * 0.12f), turn + 0.08f);
                }

                break;

            case YardKind.Granary:
                YardGround(k, l.YardAt, l.YardLong, l.YardShort, t, seed);
                Planks(k, l.P(-0.1f, -0.55f), t, 0.42f, 0.18f);
                if (s.Stock > 0)
                {
                    Sacks(k, "grain", l.P(0.42f, 0.05f), t, 1 + (s.Stock * 2), seed, Straw.Darkened(0.05f));
                }

                break;

            case YardKind.Warehouse:
                YardGround(k, l.YardAt, l.YardLong, l.YardShort, t, seed);
                Planks(k, l.P(0f, -0.55f), t, 0.42f, 0.18f);
                for (int i = 0; i < s.Stock; i++)
                {
                    Crate(k, "goods", l.P(-0.75f + (i % 2 * 0.17f), -0.1f + (i / 2 * 0.45f)), t + (0.1f * i));
                    Barrel(k, "goods", l.P(0.5f + (i % 2 * 0.15f), -0.1f + (i / 2 * 0.45f)), 0.085f);
                }

                if (s.Stock2 > 0)
                {
                    Handcart(k, "made cart", l.P(-0.25f, 0.3f), t + 0.2f);
                }

                break;

            case YardKind.Market:
                if (s.Stock > 0)
                {
                    Basket(k, l.P(-0.8f, 0.1f), new Color("#a3283a"), seed);
                    Basket(k, l.P(-0.62f, 0.2f), Straw, seed + 1);
                }

                if (s.Stock > 1)
                {
                    Crate(k, "goods", l.P(0.72f, 0.12f), t);
                    Barrel(k, "goods", l.P(0.88f, 0.2f), 0.085f);
                }

                break;

            case YardKind.TradingPost:
                YardGround(k, l.YardAt, l.YardLong, l.YardShort, t, seed);
                for (int i = 0; i < s.Stock; i++)
                {
                    Crate(k, "goods", l.P(-0.5f + (0.35f * (i % 2)), -0.5f), t + (0.15f * i));
                    Barrel(k, "goods", l.P(-0.4f + (0.35f * (i % 2)), 0.45f), 0.078f);
                }

                break;

            case YardKind.Smith:
                YardGround(k, l.YardAt, l.YardLong, l.YardShort, t, seed);
                Barrel(k, "quench", l.P(0f, -0.55f), 0.1f, YardWater);
                Heap(k, "charcoal", l.P(0.2f, 0.25f), 0.17f, Charcoal, new Color("#55504a"), seed);
                Wheel(k, l.P(-0.6f, 0.5f), 0.11f);
                if (s.Working)
                {
                    foreach (float y in new[] { 0.55f, 0.65f, 0.75f })
                    {
                        k.Thing("iron", Box(l.P(-0.1f, y), 0.36f, 0.035f, t), YardIron, 0.5f);
                    }
                }

                break;

            case YardKind.Quarry:
                YardGround(k, l.YardAt, l.YardLong, l.YardShort, t, seed, Pavement, 0.6f);
                if (s.Working)
                {
                    Stones(k, "blocks", l.P(0f, -0.4f), 5, seed, 0.15f, CutStone, 0.08f);
                }

                Barrow(k, l.P(0f, 0.5f), t + (Mathf.Pi / 2f), s.Working ? CutStone : null);
                break;

            case YardKind.Mine:
                YardGround(k, l.YardAt, l.YardLong, l.YardShort, t, seed, new Color("#9a7b62"), 0.5f);
                if (s.Working)
                {
                    Heap(k, "ore", l.P(0f, -0.4f), 0.17f, Ore, new Color("#4f2c1e"), seed);
                }

                var rails = new List<Vector2>();
                var sleepers = new List<Vector2>();
                Vector2 from = l.P(-1f, 0.6f);
                Vector2 to = l.P(1f, 0.6f);
                Vector2 dir = (to - from).Normalized();
                var n = new Vector2(-dir.Y, dir.X);
                for (float d = 0f; d <= from.DistanceTo(to); d += 0.08f)
                {
                    sleepers.Add(from + (dir * d) - (n * 0.075f));
                    sleepers.Add(from + (dir * d) + (n * 0.075f));
                }

                rails.AddRange(new[] { from - (n * 0.05f), to - (n * 0.05f), from + (n * 0.05f), to + (n * 0.05f) });
                k.Strokes("rails sleeper", sleepers, TimberDark with { A = 0.85f }, 0.08f, 2f);
                k.Strokes("rails", rails, YardIron, 0f, 1.2f);
                k.Thing("tub", Box(l.P(0.3f, 0.6f), 0.26f, 0.18f, t), YardIron, 0.85f);
                if (s.Working)
                {
                    k.Fill("tub ore", Oval(l.P(0.3f, 0.6f), 0.07f, 0.07f, 0f, 8), Ore);
                }

                break;

            case YardKind.Workshop:
                YardGround(k, l.YardAt, l.YardLong, l.YardShort, t, seed);
                Planks(k, l.P(0f, -0.45f), t + (Mathf.Pi / 2f), 0.5f, 0.22f);
                Sawhorse(k, l.P(0f, 0.45f), t + (Mathf.Pi / 2f));
                Chips(k, l.P(0f, 0.45f), 0.14f, 8, seed);
                break;

            case YardKind.Bakery:
                YardGround(k, l.YardAt, l.YardLong, l.YardShort, t, seed);
                Oven(k, l.P(0f, -0.35f), 0.2f);
                Firewood(k, "oven wood", l.P(0f, 0.55f), t, 0.46f, s.Working ? 4 : 0);
                break;

            case YardKind.Tailor:
                YardGround(k, l.YardAt, l.YardLong, l.YardShort, t, seed);
                ClothLine(k, l.P(0f, -0.85f), l.P(0f, 0.3f), s.Working);
                if (s.Working)
                {
                    Bales(k, l.P(0f, 0.42f), t + (Mathf.Pi / 2f), 2);
                }

                break;

            case YardKind.Farmhouse:
                YardGround(k, l.YardAt, l.YardLong, l.YardShort, t, seed);
                if (s.Stock > 0)
                {
                    Sacks(k, "grain", l.P(-0.6f, -0.1f), t, 1 + s.Stock, seed, Straw.Darkened(0.05f));
                }

                ToolRack(k, l.P(0.1f, -0.5f), t);
                Barrow(k, l.P(0.6f, 0.2f), t + 0.3f, null);
                break;

            case YardKind.Mill:
                YardGround(k, l.YardAt, l.YardLong, l.YardShort * 1.6f, t, seed);
                if (s.Working)
                {
                    Sacks(k, "flour", l.P(0.4f, 0f), t, 4, seed, SackColour);
                }

                break;

            case YardKind.Barn:
                YardGround(k, l.YardAt, l.YardLong, l.YardShort, t, seed);
                if (s.Stock > 0)
                {
                    HayRick(k, l.P(-0.35f, 0f), 0.14f + (0.04f * s.Stock));
                }

                Trough(k, l.P(0.55f, 0f), t + (Mathf.Pi / 2f));
                break;

            case YardKind.Library:
                Paving(k, l.P(0f, 0f), l.YardShort * 0.45f, l.YardShort, t + (Mathf.Pi / 2f), seed);
                Bench(k, l.P(-0.55f, 0f), t);
                Bench(k, l.P(0.55f, 0f), t);
                Sundial(k, l.P(0.85f, -0.2f));
                break;

            case YardKind.TownHall:
                Paving(k, l.YardAt, l.YardLong * 0.97f, l.YardShort, t, seed);
                NoticeBoard(k, l.P(-0.6f, 0f), t);
                Bench(k, l.P(0.25f, 0.4f), t);
                break;

            case YardKind.Well:
                YardGround(k, Vector2.Zero, l.YardLong * 0.68f, l.YardLong * 0.68f, 0f, seed, new Color("#9fa48a"), 0.4f);
                Barrel(k, "bucket", new Vector2(l.YardLong * 0.55f, -l.YardLong * 0.35f), 0.07f, YardWater);
                Trough(k, new Vector2(0, l.YardLong * 0.6f), 0f, 0.44f);
                break;
        }
    }

    /// <summary>What stands over the roof: the granary's hoist beam out over its yard, the town hall's flag.</summary>
    private static void YardOver(List<RoofPiece> pieces, RoofFrame frame, YardKind yard, YardLayout l)
    {
        var k = new YardKit(pieces, frame);
        if (yard == YardKind.Granary)
        {
            Vector2 from = l.RoofAt + (l.P(0.38f, 0f) - l.YardAt).Project(l.YardAlong);
            Vector2 to = l.P(0.38f, 0f);
            k.Line("hoist", from, to, TimberDark, 3f);
            k.Thing("hoist", Oval(to, 0.035f, 0.035f, 0f, 8), YardIron, 0.6f);
        }
        else if (yard == YardKind.TownHall)
        {
            Flag(k, l.P(0.8f, -0.1f), new Color("#8e3b35"));
        }
    }

    /// <summary>
    /// ⭐⭐ A home's yard (§4.2): the same things in every home, placed per household by its id — on its own plot's tiles
    /// (<paramref name="tiles"/>, in the house's frame), the farthest from the door first.
    /// </summary>
    private static void HomeYard(List<RoofPiece> pieces, RoofFrame frame, BuildingKindTier tier, Vector2[] tiles, Vector2 doorOut, float hw, float hd, YardState s, int seed)
    {
        var k = new YardKit(pieces, frame);
        if (tiles.Length == 0)
        {
            return;
        }

        // The tiles, the farthest from the door first (ties by position, never by hash order).
        Vector2 door = doorOut * hd;
        var order = tiles.OrderByDescending(p => (p - door).LengthSquared()).ThenBy(p => p.X).ThenBy(p => p.Y).ToArray();
        Vector2 back = -doorOut;

        // The washing line across the farthest one or two tiles.
        Vector2 far = order[0];
        Vector2 lineWay = new Vector2(-back.Y, back.X);
        float shift = (Hash01(seed, 1, 111) - 0.5f) * 0.3f;
        WashingLine(k, far + (lineWay * (-0.38f + shift)) + (back * 0.1f), far + (lineWay * (0.38f + shift)) - (back * (0.05f + (Hash01(seed, 2, 112) * 0.15f))), seed);

        // The privy (a manor's woodshed and its sundial) in the farthest tile's far corner.
        float corner = Hash01(seed, 3, 113) < 0.5f ? -1f : 1f;
        Vector2 privyAt = far + (back * 0.28f) + (lineWay * corner * 0.3f);
        if (tier == BuildingKindTier.Manor)
        {
            Shed(pieces, frame, privyAt, 0.22f, 0.16f, new Color("#6a5a48"), seed);
            if (order.Length > 1)
            {
                Sundial(k, order[1] + (lineWay * -corner * 0.25f));
            }
        }
        else
        {
            Shed(pieces, frame, privyAt, 0.15f, 0.15f, new Color("#8f8676"), seed);
        }

        // The woodpile and the block in the yard tile nearest the house's back wall.
        Vector2 nearBack = tiles.OrderBy(p => (p - (back * hd)).LengthSquared()).ThenBy(p => p.X).ThenBy(p => p.Y).First();
        Vector2 toHouse = (-nearBack).Normalized();
        float pileTurn = new Vector2(-toHouse.Y, toHouse.X).Angle();
        Firewood(k, "woodpile", nearBack + (toHouse * 0.3f), pileTurn, 0.8f, s.Stock);
        ChoppingBlock(k, nearBack + (toHouse * -0.1f) + (new Vector2(-toHouse.Y, toHouse.X) * (Hash01(seed, 4, 114) < 0.5f ? 0.3f : -0.3f)));

        // The larder by the back door: as many barrels and sacks as its level (§2) — only if the plot has ground behind the house.
        Vector2 larder = (back * (hd + 0.14f)) + (new Vector2(-back.Y, back.X) * (Hash01(seed, 5, 115) < 0.5f ? -1f : 1f) * hw * 0.55f);
        bool behind = tiles.Any(p => p.Dot(back) > 0.5f && Mathf.Abs(p.Dot(new Vector2(-back.Y, back.X)) - larder.Dot(new Vector2(-back.Y, back.X))) < 0.6f);
        for (int i = 0; behind && i < s.Stock2; i++)
        {
            Vector2 at = larder + (new Vector2(-back.Y, back.X) * (i * 0.15f)) + (back * ((i & 1) * 0.07f));
            if (i == 3)
            {
                Sack(k, "larder", at, 0.4f, SackColour);
            }
            else
            {
                Barrel(k, "larder", at, 0.075f);
            }
        }

        // The water butt at a corner of the house; the bench in the yard tile nearest the door (the house's front is the lane).
        float side = Hash01(seed, 6, 116) < 0.5f ? -1f : 1f;
        Vector2 across = new(-doorOut.Y, doorOut.X);
        Barrel(k, "water butt", (across * side * (hw + 0.1f)) + (back * hd * 0.6f), 0.08f, YardWater);
        Vector2 nearDoor = tiles.OrderBy(p => (p - door).LengthSquared()).ThenBy(p => p.X).ThenBy(p => p.Y).First();
        Bench(k, nearDoor + (doorOut * 0.28f) - (nearDoor.Dot(across) > 0f ? across : -across) * 0.15f, across.Angle());
    }

    /// <summary>A house's tier, for its yard and its fence (§4.3).</summary>
    internal enum BuildingKindTier
    {
        Cabin,
        Cottage,
        Manor,
    }

    internal static BuildingKindTier TierOf(YardKind yard) => yard switch
    {
        YardKind.Cottage => BuildingKindTier.Cottage,
        YardKind.Manor => BuildingKindTier.Manor,
        _ => BuildingKindTier.Cabin,
    };

    internal static BuildingKindTier TierOf(BuildingKind kind) => kind switch
    {
        BuildingKind.Cottage => BuildingKindTier.Cottage,
        BuildingKind.Manor => BuildingKindTier.Manor,
        _ => BuildingKindTier.Cabin,
    };

    // ------------------------------------------------------------------ the fences (§4.3)

    /// <summary>A household fence's sides, in tile space, with its tier and whether it stands (the outlines' pass fills it).</summary>
    private readonly List<(Vector2 From, Vector2 To, BuildingKindTier Tier, bool Standing)> _householdFences = new();

    /// <summary>The fences' pieces, laid when the outlines are traced (never a frame), as runs; null until first drawn.</summary>
    private List<RoofRun>? _fenceRuns;

    private List<RoofPiece>? _fencePieces;

    /// <summary>
    /// ⭐ The fences by tier (§4.3): wattle — a brown band, the weave ticked across it, a stake every third of a tile; post
    /// and rail — a rail, a post every 0.4 tile; dry stone — a grey band, a chain of stones. Pure, in tile space.
    /// </summary>
    internal static List<RoofPiece> FencePieces(IReadOnlyList<(Vector2 From, Vector2 To, BuildingKindTier Tier, bool Standing)> sides)
    {
        var pieces = new List<RoofPiece>();
        var weave = new List<Vector2>();
        int n = 0;
        foreach ((Vector2 a, Vector2 b, BuildingKindTier tier, bool standing) in sides)
        {
            float length = a.DistanceTo(b);
            if (length < 1e-4f)
            {
                continue;
            }

            float alpha = standing ? 1f : 0.35f;
            Vector2 d = (b - a) / length;
            var nrm = new Vector2(-d.Y, d.X);
            switch (tier)
            {
                case BuildingKindTier.Manor:
                    pieces.Add(new RoofPiece(RoofPieceKind.Fill, "fence wall", new[] { a + (nrm * 0.05f), b + (nrm * 0.05f), b - (nrm * 0.05f), a - (nrm * 0.05f) }, new Color("#9b968c") with { A = 0.9f * alpha }));
                    for (float t = 0f; t <= length; t += 0.06f)
                    {
                        Vector2 c = a + (d * t) + (nrm * (Hash01(n, (int)(t * 100), 121) - 0.5f) * 0.02f);
                        float r = 0.032f + (0.012f * Hash01(n, (int)(t * 100), 122));
                        var stone = new Vector2[8];
                        for (int i = 0; i < 8; i++)
                        {
                            float ang = i * Mathf.Tau / 8f;
                            stone[i] = c + (new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r);
                        }

                        pieces.Add(new RoofPiece(RoofPieceKind.Fill, "fence stone", stone, YardStone.Darkened(0.1f * Hash01(n, (int)(t * 100), 123)) with { A = alpha }));
                        pieces.Add(new RoofPiece(RoofPieceKind.Line, "fence stone", stone, Ink with { A = 0.5f * alpha }, Closed: true));
                    }

                    break;

                case BuildingKindTier.Cottage:
                    pieces.Add(new RoofPiece(RoofPieceKind.Fill, "fence rail", new[] { a + (nrm * 0.035f), b + (nrm * 0.035f), b - (nrm * 0.035f), a - (nrm * 0.035f) }, new Color("#8f6e46") with { A = alpha }));
                    for (float t = 0f; t <= length + 1e-4f; t += 0.4f)
                    {
                        Vector2 c = a + (d * Mathf.Min(t, length));
                        Vector2[] post = { c + new Vector2(-0.045f, -0.045f), c + new Vector2(0.045f, -0.045f), c + new Vector2(0.045f, 0.045f), c + new Vector2(-0.045f, 0.045f) };
                        pieces.Add(new RoofPiece(RoofPieceKind.Fill, "fence post", post, TimberDark with { A = alpha }));
                    }

                    break;

                default:
                    pieces.Add(new RoofPiece(RoofPieceKind.Fill, "fence wattle", new[] { a + (nrm * 0.035f), b + (nrm * 0.035f), b - (nrm * 0.035f), a - (nrm * 0.035f) }, new Color("#7d5c37") with { A = 0.95f * alpha }));
                    int k = 0;
                    for (float t = 0f; t < length; t += 0.045f)
                    {
                        Vector2 c = a + (d * t);
                        float s = (k++ & 1) == 1 ? 0.028f : -0.028f;
                        weave.Add(c + (nrm * s));
                        weave.Add(c + (d * 0.04f) - (nrm * s));
                    }

                    for (float t = 0f; t <= length + 1e-4f; t += 0.33f)
                    {
                        Vector2 c = a + (d * Mathf.Min(t, length));
                        var stake = new Vector2[6];
                        for (int i = 0; i < 6; i++)
                        {
                            float ang = i * Mathf.Tau / 6f;
                            stake[i] = c + (new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 0.028f);
                        }

                        pieces.Add(new RoofPiece(RoofPieceKind.Fill, "fence stake", stake, TimberDark with { A = alpha }));
                    }

                    break;
            }

            n++;
        }

        if (weave.Count > 0)
        {
            pieces.Add(new RoofPiece(RoofPieceKind.Lines, "fence weave", weave.ToArray(), new Color("#4e361f") with { A = 0.7f }, Spacing: 0.045f));
        }

        return pieces;
    }

    /// <summary>Draw the household fences painted (§4.3): laid once a trace, the lines per zoom (A85's runs).</summary>
    private void DrawPaintedFences()
    {
        if (_householdFences.Count == 0)
        {
            return;
        }

        if (_fenceRuns is null)
        {
            _fencePieces = FencePieces(_householdFences);
            _fenceRuns = RoofRuns(_fencePieces, _pixelsPerTile);
        }

        DrawRoofRuns(new RoofCache { Signature = default, Runs = _fenceRuns }, 1f);
    }
}
