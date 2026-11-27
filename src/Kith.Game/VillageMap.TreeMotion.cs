using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <summary>
/// ⭐⭐ The trees' motion (A197, <c>tree-motion.md</c>; Joe, A173: <i>"animated transition for trees 1) maturing from saplings
/// to full trees and 2) being felled/falling"</i>; his six calls, A197: A, A, into the open, real seconds frozen when paused,
/// yes, approved) — <b>a few seconds of motion at the tick the sim changed a tile</b>, and nothing between.
/// </summary>
/// <remarks>
/// <para>
/// <b>Where it starts:</b> <see cref="RefreshTheScenery"/>'s diff of the shadow copy, which already knows each changed tile's
/// <i>was</i> and <i>is</i> (<c>tree-motion.md §2</c>): grown trees gone plays the fall, young become grown plays the growing,
/// saplings come rise in, saplings gone fade. Never on a fresh map (a load, a new game, a tick switched): the A169 rule.
/// </para>
/// <para>
/// <b>What it keeps:</b> the motions under way, each tree's place, cell, face and size worked out once, when it starts, by
/// the mesh's own <see cref="CanopyOn"/>, <see cref="PaintedTreeOn"/> and <see cref="KindOf"/> (D337) — so a fall's first
/// frame is the standing tree and a growing's last is the grown one. A growing or a rising <b>holds</b> its tile's trees out of
/// the chunk's mesh while it plays, and gives them back (one chunk re-meshed) the frame it ends. Only each tree's place along
/// its motion is worked out a frame (CLAUDE.md: it changes every frame; nothing derivable is rebuilt).
/// </para>
/// <para>
/// ⛔ View only: it reads the terrain and the drawn villagers, never the sim's state beyond them, and writes nothing back.
/// </para>
/// </remarks>
public partial class VillageMap
{
    /// <summary>The four motions (<c>tree-motion.md §3–§5</c>).</summary>
    internal enum TreeMotionKind
    {
        /// <summary>A grown tile's trees tip over into the open, lie a moment, and fade (§3, call 1: A).</summary>
        Fall,

        /// <summary>A young tile's saplings fill out and give way as its grown trees rise to their size (§4, call 2: A).</summary>
        Grow,

        /// <summary>A tile's new trees rise in from small and clear (§5, call 5: yes).</summary>
        Rise,

        /// <summary>A tile's saplings, ploughed or destroyed, fade where they stood (§5).</summary>
        Fade,
    }

    /// <summary>One painted tree as the mesh lays it: its foot (the crown's centre, from above), half its cell in tiles, the cell, the face.</summary>
    internal readonly record struct PaintedTree(Vector2 Foot, float Half, int Cell, bool Mirrored);

    /// <summary>One motion under way on one tile.</summary>
    internal sealed class TreeMotion
    {
        public GridPos Tile { get; init; }

        public TreeMotionKind Kind { get; init; }

        /// <summary>When it began, on <see cref="_treeClock"/>.</summary>
        public double StartedAt { get; init; }

        /// <summary>The fall line (a unit vector, tiles), for a fall; zero otherwise.</summary>
        public Vector2 Line { get; init; }

        /// <summary>What moves: the felled trees, the grown trees rising, the saplings rising or fading.</summary>
        public PaintedTree[] Trees { get; init; } = System.Array.Empty<PaintedTree>();

        /// <summary>The saplings giving way, for a growing; empty otherwise.</summary>
        public PaintedTree[] Giving { get; init; } = System.Array.Empty<PaintedTree>();

        /// <summary>Whether the tile's trees are kept out of its chunk's mesh while this plays (a growing, a rising).</summary>
        public bool Holds { get; init; }

        /// <summary>Seconds from its start to its end.</summary>
        public double Length { get; init; }
    }

    // -------- the numbers, each from `tree-motion.md` --------

    /// <summary>§3: the tip, eased in.</summary>
    internal const double FallTipSeconds = 1.0;

    /// <summary>§3: the settle after landing.</summary>
    internal const double FallSettleSeconds = 0.25;

    /// <summary>§3: the fade begins this long after a tree starts to tip (it lies from the settle until then).</summary>
    internal const double FallFadeFrom = 2.4;

    /// <summary>§3: the fade.</summary>
    internal const double FallFadeSeconds = 1.0;

    /// <summary>§3: the second tree on a tile falls this far behind the first.</summary>
    internal const double FallBeat = 0.3;

    /// <summary>§3: the second tree's fall line is splayed this far off the first's (degrees).</summary>
    internal const float FallSplayDegrees = 20f;

    /// <summary>§3: how far the crown lands from its foot, in halves of its own cell.</summary>
    internal const float FallReachHalves = 1.5f;

    /// <summary>§3: the settle back along the line, a share of the reach.</summary>
    internal const float FallSettleBack = 0.06f;

    /// <summary>§3: the crown lying, squashed along the line by up to this share (never turned: A31).</summary>
    internal const float FallSquash = 0.18f;

    /// <summary>§3: the trunk's width at the foot (tiles), the A27 ink round bark.</summary>
    internal const float TrunkWidth = 0.15f;

    /// <summary>§3: never onto the feller — a villager drawn within this many tiles of the tile…</summary>
    internal const float FellerReach = 1.0f;

    /// <summary>…and within this many degrees of the line turns it away.</summary>
    internal const float FellerClearDegrees = 60f;

    /// <summary>§4: the saplings giving way.</summary>
    internal const double GrowGiveSeconds = 1.5;

    /// <summary>§4: the saplings' size as they go.</summary>
    internal const float GrowGiveSwell = 1.45f;

    /// <summary>§4: the grown trees rising, from <see cref="GrowFrom"/> of their size.</summary>
    internal const double GrowSeconds = 3.0;

    internal const float GrowFrom = 0.5f;

    /// <summary>§4: the grown trees fading in.</summary>
    internal const double GrowShowSeconds = 1.2;

    /// <summary>§5: saplings rising in, from <see cref="RiseFrom"/> of their size, fading in over <see cref="RiseShowSeconds"/>.</summary>
    internal const double RiseSeconds = 1.5;

    internal const float RiseFrom = 0.3f;

    internal const double RiseShowSeconds = 0.8;

    /// <summary>§5: saplings fading out.</summary>
    internal const double FadeSeconds = 1.0;

    /// <summary>§6: a safety cap on the trees in motion at once; past it a tile changes at once, as before.</summary>
    internal const int TreesInMotionAtMost = 512;

    /// <summary>The longest frame the clock takes (s): a hitch must not skip a fall.</summary>
    private const double TreeClockStepAtMost = 0.25;

    private static readonly Color TrunkInk = new("3a2d20");
    private static readonly Color TrunkBark = new("685038");

    private readonly List<TreeMotion> _treeMotions = new();
    private readonly HashSet<GridPos> _treesHeld = new();
    private readonly MeshBuilder _trunks = new();
    private readonly ArrayMesh _motionCrowns = new();
    private readonly ArrayMesh _motionTrunks = new();
    private int _treesInMotion;

    /// <summary>The motion's clock (call 4): real seconds, frozen while the village is paused.</summary>
    private double _treeClock;

    /// <summary>The probe's switch: a headless run has no frames, so the motion is off there unless the probe asks for it.</summary>
    private bool _treeMotionForTheProbe;

    /// <summary>Whether a change of ground starts a motion: the painted trees drawn, the trees tick on, and a window to draw in.</summary>
    private bool TreeMotionOn => _paintedTrees && _treesShown && (_treeMotionForTheProbe || DisplayServer.GetName() != "headless");

    /// <summary>Whether a tile's trees are kept out of its chunk's mesh, a growing or a rising drawing them instead.</summary>
    private bool TreeHeldByMotion(GridPos tile) => _treesHeld.Count > 0 && _treesHeld.Contains(tile);

    /// <summary>Everything under way dropped: a fresh map plays nothing (A169).</summary>
    private void ForgetTheTreeMotion()
    {
        _treeMotions.Clear();
        _treesHeld.Clear();
        _treesInMotion = 0;
    }

    /// <summary>The painted trees on a tile, exactly as <see cref="PaintedTrees"/> lays them into the mesh.</summary>
    internal static PaintedTree[] PaintedTreesOf(GeneratedMap map, GridPos tile, bool grown)
    {
        var trees = new PaintedTree[PaintedTreesOn(tile, grown)];
        for (int i = 0; i < trees.Length; i++)
        {
            (Vector2 where, float _) = CanopyOn(tile, i);
            (int cell, bool mirrored, float scale) = PaintedTreeOn(tile, i, grown, KindOf(map, tile, i));
            trees[i] = new PaintedTree(where, (grown ? PaintedTreeHalf : YoungTreeHalf) * scale, cell, mirrored);
        }

        return trees;
    }

    /// <summary>
    /// A tile's ground changed, <paramref name="was"/> → <paramref name="now"/> — from <see cref="RefreshTheScenery"/>'s diff, with the
    /// map already changed. Starts what that change plays (<c>tree-motion.md §2</c>'s one rule), or nothing.
    /// </summary>
    private void StartTheTreeMotion(GeneratedMap map, GridPos tile, Terrain was, Terrain now)
    {
        bool wasGrown = was == Terrain.Forest, wasYoung = was == Terrain.Sapling;
        bool nowGrown = now == Terrain.Forest, nowYoung = now == Terrain.Sapling;
        if (!(wasGrown || wasYoung || nowGrown || nowYoung))
        {
            return;
        }

        // A tile that changes again while its trees are held: what was held is stale, so it is let go (a fall keeps falling).
        if (_treesHeld.Remove(tile))
        {
            for (int i = _treeMotions.Count - 1; i >= 0; i--)
            {
                if (_treeMotions[i].Holds && _treeMotions[i].Tile == tile)
                {
                    _treesInMotion -= _treeMotions[i].Trees.Length + _treeMotions[i].Giving.Length;
                    _treeMotions.RemoveAt(i);
                }
            }
        }

        if (wasGrown && !nowGrown)
        {
            PaintedTree[] felled = PaintedTreesOf(map, tile, grown: true);
            Add(new TreeMotion
            {
                Tile = tile,
                Kind = TreeMotionKind.Fall,
                StartedAt = _treeClock,
                Line = FallLineOf(map, tile, TheFellerBy(tile)),
                Trees = felled,
                Length = FallLengthOf(felled.Length),
            });
        }

        if (wasYoung && nowGrown)
        {
            Add(new TreeMotion
            {
                Tile = tile,
                Kind = TreeMotionKind.Grow,
                StartedAt = _treeClock,
                Trees = PaintedTreesOf(map, tile, grown: true),
                Giving = PaintedTreesOf(map, tile, grown: false),
                Holds = true,
                Length = GrowSeconds,
            });
        }
        else if ((nowYoung && !wasYoung) || (nowGrown && !wasGrown))
        {
            Add(new TreeMotion
            {
                Tile = tile,
                Kind = TreeMotionKind.Rise,
                StartedAt = _treeClock,
                Trees = PaintedTreesOf(map, tile, grown: nowGrown),
                Holds = true,
                Length = RiseSeconds,
            });
        }
        else if (wasYoung && !nowYoung && !nowGrown)
        {
            Add(new TreeMotion
            {
                Tile = tile,
                Kind = TreeMotionKind.Fade,
                StartedAt = _treeClock,
                Trees = PaintedTreesOf(map, tile, grown: false),
                Length = FadeSeconds,
            });
        }

        void Add(TreeMotion motion)
        {
            int trees = motion.Trees.Length + motion.Giving.Length;
            if (_treesInMotion + trees > TreesInMotionAtMost)
            {
                return;
            }

            _treesInMotion += trees;
            _treeMotions.Add(motion);
            if (motion.Holds)
            {
                _treesHeld.Add(tile);
            }
        }
    }

    /// <summary>How long a fall of <paramref name="trees"/> takes: the last one starts its beat late.</summary>
    internal static double FallLengthOf(int trees) => FallFadeFrom + FallFadeSeconds + (FallBeat * System.Math.Max(0, trees - 1));

    /// <summary>The drawn point of the villager nearest the tile, within <see cref="FellerReach"/>, or null.</summary>
    private Vector2? TheFellerBy(GridPos tile)
    {
        if (_world is null)
        {
            return null;
        }

        var centre = new Vector2(tile.X, tile.Y);
        Vector2? nearest = null;
        float best = FellerReach * FellerReach;
        foreach (Villager villager in _world.Villagers)
        {
            if (!villager.Alive)
            {
                continue;
            }

            Vector2 at = DrawnTile(villager);
            float away = at.DistanceSquaredTo(centre);
            if (away <= best)
            {
                best = away;
                nearest = at;
            }
        }

        return nearest;
    }

    /// <summary>
    /// ⭐ Which way a felled tile's trees fall (call 3, Joe: <i>"into the open"</i>): toward the neighbours that are not wood, so
    /// a tree never lands on the standing wood; never onto the feller. A <c>static</c> of the map, the tile and the feller.
    /// </summary>
    /// <remarks>
    /// The open neighbours' directions summed (eight round, the map's edge not counted, a young tile open: a sapling is under
    /// a falling crown). Where that average would point at a wooded neighbour (an inside corner, open north and west with
    /// the north-west wood) the line takes the open neighbour nearest it instead, so <b>the neighbour a tree falls toward is
    /// always open</b>. Open both ways at once (a ride through a wood) picks one open side by the tile; none open (the inside
    /// of a painted patch felled out of order) falls by the tile. A feller within <see cref="FellerReach"/> and
    /// <see cref="FellerClearDegrees"/> of the line turns it to the open side nearest it that is clear of them, or, with
    /// none, to <c>180 − FellerClearDegrees</c> from them on the side it already leaned.
    /// </remarks>
    internal static Vector2 FallLineOf(GeneratedMap map, GridPos tile, Vector2? feller)
    {
        Vector2 sum = Vector2.Zero;
        Span<bool> isOpen = stackalloc bool[8];
        int opened = 0;
        for (int k = 0; k < 8; k++)
        {
            (int dx, int dy) = Neighbour(k);
            var beside = new GridPos(tile.X + dx, tile.Y + dy);
            if (!map.Contains(beside) || map.TerrainAt(beside) == Terrain.Forest)
            {
                continue;
            }

            isOpen[k] = true;
            sum += Way(k);
            opened++;
        }

        uint spin = Scramble(tile.X - FallSalt, tile.Y + FallSalt);
        if (opened == 0)
        {
            return Vector2.Right.Rotated((spin % 628) / 100f);
        }

        Vector2 line;
        if (sum.LengthSquared() < 0.01f)
        {
            int pick = (int)(spin % (uint)opened);
            int k = 0;
            while (!isOpen[k] || pick-- > 0)
            {
                k++;
            }

            line = Way(k);
        }
        else
        {
            line = sum.Normalized();
            if (!isOpen[NearestNeighbour(line)])
            {
                line = NearestOpen(isOpen, line, null);
            }
        }

        if (feller is Vector2 stood)
        {
            Vector2 toward = stood - new Vector2(tile.X, tile.Y);
            if (toward.LengthSquared() > 1e-6f && DegreesBetween(toward, line) < FellerClearDegrees)
            {
                Vector2 clear = NearestOpen(isOpen, line, toward);
                float side = toward.Cross(line) >= 0f ? 1f : -1f;
                line = clear != Vector2.Zero ? clear : toward.Normalized().Rotated(side * Mathf.DegToRad(180f - FellerClearDegrees));
            }
        }

        return line;

        static Vector2 NearestOpen(Span<bool> isOpen, Vector2 to, Vector2? clearOf)
        {
            Vector2 best = Vector2.Zero;
            float nearest = float.MaxValue;
            for (int k = 0; k < 8; k++)
            {
                if (!isOpen[k] || (clearOf is Vector2 away && DegreesBetween(away, Way(k)) < FellerClearDegrees))
                {
                    continue;
                }

                float off = DegreesBetween(to, Way(k));
                if (off < nearest)
                {
                    nearest = off;
                    best = Way(k);
                }
            }

            return best;
        }
    }

    /// <summary>The eight neighbours in turn from east, clockwise on screen (y down): k × 45°.</summary>
    internal static (int Dx, int Dy) Neighbour(int k) => k switch
    {
        0 => (1, 0),
        1 => (1, 1),
        2 => (0, 1),
        3 => (-1, 1),
        4 => (-1, 0),
        5 => (-1, -1),
        6 => (0, -1),
        _ => (1, -1),
    };

    /// <summary>Neighbour <paramref name="k"/>'s direction as a unit vector.</summary>
    internal static Vector2 Way(int k)
    {
        (int dx, int dy) = Neighbour(k);
        return new Vector2(dx, dy).Normalized();
    }

    /// <summary>Which of the eight neighbours a line points nearest.</summary>
    internal static int NearestNeighbour(Vector2 line) => (int)(((Mathf.RoundToInt(line.Angle() / (Mathf.Pi / 4f)) % 8) + 8) % 8);

    /// <summary>The angle between two directions, 0–180°.</summary>
    internal static float DegreesBetween(Vector2 a, Vector2 b) => Mathf.RadToDeg(Mathf.Abs(a.AngleTo(b)));

    /// <summary>Keeps the fall line out of step with the trees' own choices.</summary>
    private const int FallSalt = 4421;

    /// <summary>
    /// The clock forward a frame (call 4: real seconds, frozen while paused) and every motion that has ended let go — its held
    /// trees given back to the mesh (one chunk) in the same frame, before the chunks are drawn, so nothing blinks.
    /// </summary>
    private void AdvanceTheTreeMotion()
    {
        _treeClock += Paused ? 0.0 : System.Math.Min(GetProcessDeltaTime(), TreeClockStepAtMost);
        RetireTheTreeMotion();
    }

    private void RetireTheTreeMotion()
    {
        if (_treeMotions.Count == 0)
        {
            return;
        }

        GeneratedMap? map = _sceneryOf;
        for (int i = _treeMotions.Count - 1; i >= 0; i--)
        {
            TreeMotion motion = _treeMotions[i];
            if (_treeClock - motion.StartedAt < motion.Length)
            {
                continue;
            }

            _treeMotions.RemoveAt(i);
            _treesInMotion -= motion.Trees.Length + motion.Giving.Length;
            if (motion.Holds && _treesHeld.Remove(motion.Tile) && map is not null)
            {
                BuildChunk(map, ChunkOf(motion.Tile.X - map.MinX, motion.Tile.Y - map.MinY), standing: true, berries: false, _berryRadiusBuilt);
            }
        }
    }

    /// <summary>One frame of a tree in motion: where its crown is, how big, how squashed along which line, how seen; its trunk's tip.</summary>
    internal readonly record struct TreeFrame(Vector2 Centre, float Half, float Squash, Vector2 Along, float Alpha, Vector2 TrunkTip, float TrunkAlpha);

    /// <summary>
    /// ⭐ Where a tree in motion is drawn, <paramref name="t"/> seconds into its motion — pure, so the probe asks it the same
    /// question the draw does. <paramref name="which"/> is the tree's place on the tile (the fall's beat and splay).
    /// </summary>
    internal static TreeFrame TreeFrameAt(TreeMotionKind kind, PaintedTree tree, bool giving, int which, Vector2 line, double t)
    {
        switch (kind)
        {
            case TreeMotionKind.Fall:
            {
                double own = t - (FallBeat * which);
                Vector2 along = which == 0 ? line : line.Rotated(Mathf.DegToRad(FallSplayDegrees) * (which % 2 == 1 ? -1f : 1f));
                if (own <= 0.0)
                {
                    return new TreeFrame(tree.Foot, tree.Half, 1f, along, 1f, tree.Foot, 0f);
                }

                float reach = own < FallTipSeconds ? Mathf.Sin(Mathf.Pi / 2f * (float)(own * own / (FallTipSeconds * FallTipSeconds)))
                    : own < FallTipSeconds + FallSettleSeconds ? 1f - (FallSettleBack * Mathf.Sin(Mathf.Pi * (float)((own - FallTipSeconds) / FallSettleSeconds)))
                    : 1f;
                float alpha = 1f - Clamp01((own - FallFadeFrom) / FallFadeSeconds);
                float out_ = FallReachHalves * tree.Half * reach;
                Vector2 tip = tree.Foot + (along * Mathf.Max(0f, out_ - (tree.Half * 0.35f)));
                return new TreeFrame(tree.Foot + (along * out_), tree.Half, 1f - (FallSquash * reach), along, alpha, tip, alpha * Clamp01(reach * 3f));
            }

            case TreeMotionKind.Grow when giving:
            {
                float u = Clamp01(t / GrowGiveSeconds);
                return Still(tree, tree.Half * (1f + ((GrowGiveSwell - 1f) * EaseOut(u))), 1f - u);
            }

            case TreeMotionKind.Grow:
                return Still(tree, tree.Half * (GrowFrom + ((1f - GrowFrom) * EaseOut(Clamp01(t / GrowSeconds)))), Clamp01(t / GrowShowSeconds));

            case TreeMotionKind.Rise:
                return Still(tree, tree.Half * (RiseFrom + ((1f - RiseFrom) * EaseOut(Clamp01(t / RiseSeconds)))), Clamp01(t / RiseShowSeconds));

            default:
                return Still(tree, tree.Half, 1f - Clamp01(t / FadeSeconds));
        }

        static TreeFrame Still(PaintedTree tree, float half, float alpha) =>
            new(tree.Foot, half, 1f, Vector2.Right, alpha, tree.Foot, 0f);
    }

    private static float Clamp01(double u) => (float)System.Math.Clamp(u, 0.0, 1.0);

    private static float EaseOut(float u) => 1f - ((1f - u) * (1f - u));

    /// <summary>
    /// The motions on screen, drawn over the canopy with the season's atlas at the canopy's share, and the next season's
    /// over it while a season turns (A41) — the trunks first, once, then the crowns. A frame with nothing under way draws nothing.
    /// </summary>
    private void DrawTheTreeMotion(int minX, int maxX, int minY, int maxY, TreeSet from, float fromAlpha, TreeSet to, float toAlpha)
    {
        if (_treeMotions.Count == 0)
        {
            return;
        }

        const int Reach = 3;
        var vertices = new List<Vector2>();
        var colours = new List<Color>();
        var uvs = new List<Vector2>();
        _trunks.Clear();
        foreach (TreeMotion motion in _treeMotions)
        {
            if (motion.Tile.X < minX - Reach || motion.Tile.X > maxX + Reach || motion.Tile.Y < minY - Reach || motion.Tile.Y > maxY + Reach)
            {
                continue;
            }

            double t = _treeClock - motion.StartedAt;
            for (int i = 0; i < motion.Giving.Length; i++)
            {
                LayTheTreeFrame(vertices, colours, uvs, motion.Giving[i], TreeFrameAt(motion.Kind, motion.Giving[i], giving: true, i, motion.Line, t));
            }

            for (int i = 0; i < motion.Trees.Length; i++)
            {
                TreeFrame frame = TreeFrameAt(motion.Kind, motion.Trees[i], giving: false, i, motion.Line, t);
                if (frame.TrunkAlpha > 0f)
                {
                    _trunks.Taper(motion.Trees[i].Foot, frame.TrunkTip, TrunkWidth / 2f, TrunkWidth / 3f, TrunkInk with { A = frame.TrunkAlpha });
                    _trunks.Taper(motion.Trees[i].Foot, frame.TrunkTip, TrunkWidth / 3.4f, TrunkWidth / 5f, TrunkBark with { A = frame.TrunkAlpha });
                }

                LayTheTreeFrame(vertices, colours, uvs, motion.Trees[i], frame);
            }
        }

        Transform2D toScreen = TileToScreen();
        Rid canvas = GetCanvasItem();
        _motionTrunks.ClearSurfaces();
        _trunks.AddSurfaceTo(_motionTrunks);
        if (_motionTrunks.GetSurfaceCount() > 0)
        {
            RenderingServer.CanvasItemAddMesh(canvas, _motionTrunks.GetRid(), toScreen, Colors.White);
        }

        _motionCrowns.ClearSurfaces();
        if (vertices.Count == 0)
        {
            return;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colours.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
        _motionCrowns.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        RenderingServer.CanvasItemAddMesh(canvas, _motionCrowns.GetRid(), toScreen, TreeTintFor(from) with { A = fromAlpha }, TheTreeAtlasFor(from)!.GetRid());
        if (toAlpha > 0f)
        {
            RenderingServer.CanvasItemAddMesh(canvas, _motionCrowns.GetRid(), toScreen, TreeTintFor(to) with { A = toAlpha }, TheTreeAtlasFor(to)!.GetRid());
        }
    }

    /// <summary>
    /// One tree's frame as six vertices — <see cref="MeshBuilder.Sprite"/>'s cell and face to the vertex, its square squashed
    /// along the fall line about its centre and never turned (A31: the light is painted in).
    /// </summary>
    internal static void LayTheTreeFrame(List<Vector2> vertices, List<Color> colours, List<Vector2> uvs, PaintedTree tree, TreeFrame frame)
    {
        if (frame.Alpha <= 0f)
        {
            return;
        }

        (float u0, float v0, float du, float dv) = CellUv(tree.Cell);
        Vector2 Uv(float s, float t) => tree.Mirrored ? new Vector2(u0 + (t * du), v0 + (s * dv)) : new Vector2(u0 + (s * du), v0 + (t * dv));
        Vector2 Corner(float sx, float sy)
        {
            var off = new Vector2(sx * frame.Half, sy * frame.Half);
            return frame.Centre + off + (frame.Along * ((frame.Squash - 1f) * off.Dot(frame.Along)));
        }

        Color seen = Colors.White with { A = frame.Alpha };
        Vector2 tl = Corner(-1f, -1f), tr = Corner(1f, -1f), br = Corner(1f, 1f), bl = Corner(-1f, 1f);
        // ⭐ The top corners carry the wind's mark as the mesh's do (A299), so a fall still starts as the mesh's own tree.
        var mark = new Vector2(MeshBuilder.SwayMark, 0f);
        foreach ((Vector2 at, Vector2 uv) in new[] { (tl, Uv(0f, 0f) + mark), (tr, Uv(1f, 0f) + mark), (br, Uv(1f, 1f)), (tl, Uv(0f, 0f) + mark), (br, Uv(1f, 1f)), (bl, Uv(0f, 1f)) })
        {
            vertices.Add(at);
            colours.Add(seen);
            uvs.Add(uv);
        }
    }

    /// <summary>A tree cell's corner and size in the atlas's 0–1 — <see cref="MeshBuilder.Sprite"/>'s arithmetic (the probe holds the two together).</summary>
    internal static (float U0, float V0, float Du, float Dv) CellUv(int cell)
    {
        bool young = cell >= MeshBuilder.GrownSprites;
        int k = young ? cell - MeshBuilder.GrownSprites : cell;
        int across = young ? MeshBuilder.YoungSpriteColumns : MeshBuilder.SpriteColumns;
        float du = 1f / across, dv = (young ? 0.5f : 1f) / MeshBuilder.SpriteRows;
        float u0 = (k % across) * du;
        float v0 = ((young ? MeshBuilder.GrownSpriteRows : 0) + ((k / across) * (young ? 0.5f : 1f))) / MeshBuilder.SpriteRows;
        return (u0, v0, du, dv);
    }
}
