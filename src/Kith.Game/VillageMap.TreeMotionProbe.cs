using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

public partial class VillageMap
{
    /// <summary>
    /// ⭐ The probe's <c>tree motion:</c> line (A197, <c>tree-motion.md §7</c>). Every expectation is named from the spec, not
    /// the code (art trap 17): what each change of ground plays, its first and last frames against the mesh's own trees to the
    /// vertex, the fall line into the open and clear of a feller, the beat between two trees, the hold and its giving back,
    /// nothing on a fresh map or with a tick off, the clock frozen while paused, the cap.
    /// </summary>
    /// <remarks>
    /// The probe's valley is changed with <c>map.SetTerrain</c> (the sim's own door would move its hash) and put back, as
    /// <c>painted trees:</c> does; the motion is off headless (no frames), so the probe switches it on for itself and off after.
    /// ⚠️ The draw (<see cref="DrawTheTreeMotion"/>) runs only inside <c>_Draw</c>; what it draws is <see cref="TreeFrameAt"/>
    /// laid by <see cref="LayTheTreeFrame"/>, both checked here, and the renders are its own check.
    /// </remarks>
    internal string TheTreeMotionHolds()
    {
        if (_world is not SimWorld world)
        {
            return "[widths] tree motion: ⛔ no world to draw";
        }

        GeneratedMap map = world.Map;
        var faults = new List<string>();
        var changed = new Dictionary<GridPos, Terrain>();
        bool wasOn = _watercolour, wasShown = _treesShown, wasPaused = Paused;
        double clockWas = _treeClock;
        int falls = 0, grows = 0, rises = 0, fades = 0, lineTiles = 0, fellerTiles = 0, edgeTiles = 0;

        try
        {
            ShowWatercolour(true);
            ShowTrees(true);
            _treeMotionForTheProbe = true;
            Refresh();
            if (!_paintedTrees)
            {
                return "[widths] tree motion: ⛔ the painted trees are not on with the tick — nothing to move";
            }

            // ---- the fall line, over every wooded tile with open ground beside it (§3, call 3: into the open) ----
            for (int i = 0; i < map.Tiles.Count; i++)
            {
                if (map.Tiles[i] != Terrain.Forest)
                {
                    continue;
                }

                var tile = new GridPos(map.MinX + (i % map.Width), map.MinY + (i / map.Width));
                if (!AnyOpenBeside(tile))
                {
                    continue;
                }

                lineTiles++;
                Vector2 line = FallLineOf(map, tile, null);
                (int dx, int dy) = Neighbour(NearestNeighbour(line));
                var toward = new GridPos(tile.X + dx, tile.Y + dy);
                if (!map.Contains(toward) || map.TerrainAt(toward) == Terrain.Forest)
                {
                    Fault($"{tile} falls toward {toward}, which is {(map.Contains(toward) ? "wood" : "off the map")} (want open ground)");
                }

                // On a straight edge (open on one side's three neighbours, wood on the other five) it falls straight out of it:
                // the most open way, not just any open one (A197's red-check: a line by the tile alone, snapped, scored zero).
                int straight = StraightEdgeOf(tile);
                if (straight >= 0)
                {
                    edgeTiles++;
                    if (DegreesBetween(line, Way(straight)) > 0.5f)
                    {
                        Fault($"{tile}, a straight edge open to {Way(straight)}, falls {DegreesBetween(line, Way(straight)):F0}° off it (want straight out)");
                    }
                }

                if (FallLineOf(map, tile, null) != line || Mathf.Abs(line.Length() - 1f) > 1e-4f)
                {
                    Fault($"{tile}'s fall line is not one unit line, the same twice");
                }

                // A feller posed right on the line, half a tile out: the line must turn 60° or more away from them.
                fellerTiles++;
                Vector2 stood = new Vector2(tile.X, tile.Y) + (line * 0.5f);
                Vector2 clear = FallLineOf(map, tile, stood);
                if (DegreesBetween(stood - new Vector2(tile.X, tile.Y), clear) < 60f - 0.01f)
                {
                    Fault($"{tile} falls {DegreesBetween(stood - new Vector2(tile.X, tile.Y), clear):F0}° from a feller beside it (want 60° or more)");
                }
            }

            // ---- the fall (§3): a grown tile of two trees, felled ----
            GridPos felled = FindTile(t => map.TerrainAt(t) == Terrain.Forest && PaintedTreesOn(t, grown: true) == 2 && AnyOpenBeside(t));
            PaintedTree[] standing = PaintedTreesOf(map, felled, grown: true);
            int chunk = ChunkOf(felled.X - map.MinX, felled.Y - map.MinY);
            int before = _scenery[chunk].TreeVertices;
            _treeClock = 100.0;
            Set(felled, Terrain.Grass);
            Refresh();
            TreeMotion? fall = Only(felled, TreeMotionKind.Fall);
            if (fall is not null)
            {
                falls++;
                if (_scenery[chunk].TreeVertices != before - (12) || TreeHeldByMotion(felled))
                {
                    Fault($"felled, its chunk went {before} → {_scenery[chunk].TreeVertices} tree vertices, held {TreeHeldByMotion(felled)} (want 12 fewer, not held)");
                }

                if (!SameTrees(fall.Trees, standing))
                {
                    Fault("the falling trees are not the standing ones (place, cell, face, size)");
                }

                for (int k = 0; k < standing.Length; k++)
                {
                    TreeFrame first = TreeFrameAt(fall.Kind, fall.Trees[k], giving: false, k, fall.Line, 0.0);
                    if (!AsTheMesh(fall.Trees[k], first))
                    {
                        Fault($"the fall's first frame of tree {k} is not the mesh's tree to the vertex");
                    }
                }

                // A beat apart (0.3 s): at 0.2 s the first has moved off its foot and the second has not.
                TreeFrame one = TreeFrameAt(fall.Kind, fall.Trees[0], false, 0, fall.Line, 0.2), two = TreeFrameAt(fall.Kind, fall.Trees[1], false, 1, fall.Line, 0.2);
                if (one.Centre == fall.Trees[0].Foot || two.Centre != fall.Trees[1].Foot)
                {
                    Fault("two trees on a tile do not fall a beat apart (at 0.2 s the first should be moving, the second still standing)");
                }

                // Lying: about 1.5 halves out along its line, its trunk showing, squashed along the line, never turned.
                TreeFrame lying = TreeFrameAt(fall.Kind, fall.Trees[0], false, 0, fall.Line, 2.0);
                float reach = (lying.Centre - fall.Trees[0].Foot).Length() / fall.Trees[0].Half;
                if (Mathf.Abs(reach - 1.5f) > 0.01f || DegreesBetween(lying.Centre - fall.Trees[0].Foot, fall.Line) > 0.5f
                    || lying.TrunkAlpha < 0.99f || Mathf.Abs(lying.Squash - 0.82f) > 0.01f || lying.Alpha < 0.99f)
                {
                    Fault($"lying at 2 s: {reach:F2} halves out, {DegreesBetween(lying.Centre - fall.Trees[0].Foot, fall.Line):F1}° off its line, trunk {lying.TrunkAlpha:F2}, squashed to {lying.Squash:F2}, seen {lying.Alpha:F2} (want 1.5, on the line, 1, 0.82, 1)");
                }

                // Never turned (A31): squashed along its line about its centre, so a line along x pulls the corners in on x only.
                foreach (Vector2 along in new[] { Vector2.Right, Vector2.Down })
                {
                    var laid = new List<Vector2>();
                    LayTheTreeFrame(laid, new List<Color>(), new List<Vector2>(), fall.Trees[0], lying with { Along = along, Squash = 0.82f });
                    float h = lying.Half;
                    Vector2 corner = lying.Centre + (along == Vector2.Right ? new Vector2(-0.82f * h, -h) : new Vector2(-h, -0.82f * h));
                    if (laid.Count != 6 || laid[0].DistanceTo(corner) > 1e-4f)
                    {
                        Fault($"a crown lying along {along} has its top-left corner at {(laid.Count > 0 ? laid[0] : Vector2.Zero)} (want {corner}: squashed along the line, never turned)");
                    }
                }

                TreeFrame splayed =TreeFrameAt(fall.Kind, fall.Trees[1], false, 1, fall.Line, 2.3);
                if (Mathf.Abs(DegreesBetween(splayed.Along, fall.Line) - 20f) > 0.5f)
                {
                    Fault($"the second tree falls {DegreesBetween(splayed.Along, fall.Line):F1}° off the first (want 20°)");
                }

                if (TreeFrameAt(fall.Kind, fall.Trees[0], false, 0, fall.Line, 3.4).Alpha > 0f || TreeFrameAt(fall.Kind, fall.Trees[0], false, 0, fall.Line, 3.3).Alpha <= 0f)
                {
                    Fault("the first tree is not gone at 3.4 s (or gone before it)");
                }

                ClockAt(100.0 + 3.5);
                if (Only(felled, TreeMotionKind.Fall) is null)
                {
                    Fault("the fall ended at 3.5 s, before its second tree's 3.7");
                }

                ClockAt(100.0 + 3.7);
                if (_treeMotions.Count != 0 || _treesInMotion != 0)
                {
                    Fault($"3.7 s after the fell, {_treeMotions.Count} motions and {_treesInMotion} trees are still under way (want none)");
                }
            }

            // ---- the growing (§4): a young tile made a wood ----
            GridPos grown = FindTile(t => map.TerrainAt(t) == Terrain.Forest && Far(t, felled));
            int growChunk = ChunkOf(grown.X - map.MinX, grown.Y - map.MinY);
            int asWood = _scenery[growChunk].TreeVertices;
            _treeMotionForTheProbe = false;
            Set(grown, Terrain.Sapling);
            Refresh();
            int asYoung = _scenery[growChunk].TreeVertices;
            _treeMotionForTheProbe = true;
            if (_treeMotions.Count != 0)
            {
                Fault("with the motion off, a change still started one");
            }

            _treeClock = 200.0;
            Set(grown, Terrain.Forest);
            Refresh();
            TreeMotion? grow = Only(grown, TreeMotionKind.Grow);
            if (grow is not null)
            {
                grows++;
                int heldOut = asYoung - (YoungTreesOnATile * 6);
                if (!TreeHeldByMotion(grown) || _scenery[growChunk].TreeVertices != heldOut)
                {
                    Fault($"growing, its chunk holds {_scenery[growChunk].TreeVertices} tree vertices, held {TreeHeldByMotion(grown)} (want {heldOut}: the saplings gone, the grown trees held out)");
                }

                if (!SameTrees(grow.Trees, PaintedTreesOf(map, grown, grown: true)) || grow.Giving.Length != YoungTreesOnATile)
                {
                    Fault($"the growing's trees are not the tile's grown ones, or it gives up {grow.Giving.Length} saplings (want 3)");
                }

                for (int k = 0; k < grow.Trees.Length; k++)
                {
                    if (!AsTheMesh(grow.Trees[k], TreeFrameAt(grow.Kind, grow.Trees[k], false, k, Vector2.Zero, 3.0)))
                    {
                        Fault($"the growing's last frame of tree {k} is not the mesh's grown tree to the vertex");
                    }

                    TreeFrame early = TreeFrameAt(grow.Kind, grow.Trees[k], false, k, Vector2.Zero, 0.0);
                    if (early.Alpha != 0f || Mathf.Abs((early.Half / grow.Trees[k].Half) - 0.5f) > 1e-4f)
                    {
                        Fault($"the growing's tree {k} starts seen {early.Alpha:F2} at {early.Half / grow.Trees[k].Half:F2} of its size (want clear, at half)");
                    }
                }

                TreeFrame gone = TreeFrameAt(grow.Kind, grow.Giving[0], true, 0, Vector2.Zero, 1.5);
                TreeFrame swelling = TreeFrameAt(grow.Kind, grow.Giving[0], true, 0, Vector2.Zero, 1.4);
                if (gone.Alpha > 0f || swelling.Alpha <= 0f || swelling.Half <= grow.Giving[0].Half)
                {
                    Fault("the saplings do not fill out and give way over 1.5 s");
                }

                ClockAt(200.0 + 2.9);
                if (!TreeHeldByMotion(grown))
                {
                    Fault("the growing let its trees go before its 3 s");
                }

                ClockAt(200.0 + 3.0);
                if (TreeHeldByMotion(grown) || _scenery[growChunk].TreeVertices != asWood || _treeMotions.Count != 0)
                {
                    Fault($"3 s after it grew, the chunk holds {_scenery[growChunk].TreeVertices} tree vertices, held {TreeHeldByMotion(grown)} (want {asWood}, the wood as it stood, given back)");
                }
            }

            // ---- saplings rising in (§5, call 5), and fading when ploughed ----
            GridPos sown = FindTile(t => map.TerrainAt(t) == Terrain.Grass && Far(t, felled) && Far(t, grown));
            int sownChunk = ChunkOf(sown.X - map.MinX, sown.Y - map.MinY);
            int bare = _scenery[sownChunk].TreeVertices;
            _treeClock = 300.0;
            Set(sown, Terrain.Sapling);
            Refresh();
            TreeMotion? rise = Only(sown, TreeMotionKind.Rise);
            if (rise is not null)
            {
                rises++;
                if (rise.Trees.Length != YoungTreesOnATile || !TreeHeldByMotion(sown) || _scenery[sownChunk].TreeVertices != bare)
                {
                    Fault($"rising, {rise.Trees.Length} saplings, held {TreeHeldByMotion(sown)}, the chunk at {_scenery[sownChunk].TreeVertices} (want 3, held, {bare})");
                }

                TreeFrame low = TreeFrameAt(rise.Kind, rise.Trees[0], false, 0, Vector2.Zero, 0.0);
                if (low.Alpha != 0f || Mathf.Abs((low.Half / rise.Trees[0].Half) - 0.3f) > 1e-4f || !AsTheMesh(rise.Trees[0], TreeFrameAt(rise.Kind, rise.Trees[0], false, 0, Vector2.Zero, 1.5)))
                {
                    Fault("the saplings do not rise from a third of their size, clear, to the mesh's own in 1.5 s");
                }

                ClockAt(300.0 + 1.5);
                if (TreeHeldByMotion(sown) || _scenery[sownChunk].TreeVertices != bare + (YoungTreesOnATile * 6))
                {
                    Fault($"1.5 s after the saplings rose, the chunk holds {_scenery[sownChunk].TreeVertices} tree vertices (want {bare + (YoungTreesOnATile * 6)}, given back)");
                }
            }

            _treeClock = 400.0;
            Set(sown, Terrain.Field);
            Refresh();
            TreeMotion? fade = Only(sown, TreeMotionKind.Fade);
            if (fade is not null)
            {
                fades++;
                if (TreeHeldByMotion(sown) || TreeFrameAt(fade.Kind, fade.Trees[0], false, 0, Vector2.Zero, 1.0).Alpha > 0f
                    || !AsTheMesh(fade.Trees[0], TreeFrameAt(fade.Kind, fade.Trees[0], false, 0, Vector2.Zero, 0.0)))
                {
                    Fault("ploughed, the saplings do not fade from where they stood over 1 s");
                }
            }

            ClockAt(500.0);

            // ---- a forager's thinning (§2): the grown trees fall and the saplings rise ----
            GridPos thinned = FindTile(t => map.TerrainAt(t) == Terrain.Forest && Far(t, felled) && Far(t, grown) && Far(t, sown));
            _treeClock = 600.0;
            Set(thinned, Terrain.Sapling);
            Refresh();
            if (_treeMotions.Count != 2 || Only(thinned, TreeMotionKind.Fall, quiet: true) is null || Only(thinned, TreeMotionKind.Rise, quiet: true) is null)
            {
                Fault($"thinned, {_treeMotions.Count} motions (want a fall and a rising)");
            }

            ClockAt(700.0);

            // ---- the clock (call 4): frozen while paused ----
            Paused = true;
            double still = _treeClock;
            AdvanceTheTreeMotion();
            if (_treeClock != still)
            {
                Fault($"paused, the motion's clock moved {_treeClock - still:F3} s (want 0)");
            }

            Paused = wasPaused;

            // ---- nothing on a fresh map (A169) ----
            Set(felled, Terrain.Forest);
            Refresh();
            ClockAt(800.0);
            Set(felled, Terrain.Grass);
            Refresh();
            _sceneryOf = null;
            Refresh();
            if (_treeMotions.Count != 0 || _treesHeld.Count != 0)
            {
                Fault($"a fresh map kept {_treeMotions.Count} motions (want none)");
            }

            // ---- nothing with the trees tick off, or the watercolour ----
            Set(felled, Terrain.Forest);
            Refresh();
            ShowTrees(false);
            Refresh();
            Set(felled, Terrain.Grass);
            Refresh();
            int treesOff = _treeMotions.Count;
            ShowTrees(true);
            Refresh();
            Set(felled, Terrain.Forest);
            Refresh();
            ShowWatercolour(false);
            Refresh();
            Set(felled, Terrain.Grass);
            Refresh();
            int paintOff = _treeMotions.Count;
            ShowWatercolour(true);
            Refresh();
            if (treesOff != 0 || paintOff != 0)
            {
                Fault($"a fell started {treesOff} motions with the trees tick off and {paintOff} with the watercolour off (want none)");
            }

            // ---- the cap (§6): a stroke across the whole valley's wood ----
            int woods = 0;
            for (int i = 0; i < map.Tiles.Count; i++)
            {
                if (map.Tiles[i] == Terrain.Forest)
                {
                    Set(new GridPos(map.MinX + (i % map.Width), map.MinY + (i / map.Width)), Terrain.Grass);
                    woods++;
                }
            }

            Refresh();
            int capped = _treesInMotion, cappedMotions = _treeMotions.Count;
            if (capped > 512 || capped < 511 || cappedMotions >= woods)
            {
                Fault($"felling all {woods} wooded tiles at once put {capped} trees in motion on {cappedMotions} tiles (want at most 512, the rest at once)");
            }

            if (edgeTiles == 0)
            {
                Fault("no straight wood edge in the probe's valley to check the line on");
            }

            if (falls != 1 || grows != 1 || rises != 1 || fades != 1)
            {
                Fault($"seen {falls} falls, {grows} growings, {rises} risings, {fades} fades (want one of each)");
            }

            return faults.Count == 0
                ? $"[widths] tree motion: ✅ a fell plays the fall, its two trees the standing ones to the vertex at its first frame, 0.3 s apart, splayed 20°, lying 1.5 halves out along the line with the trunk showing, squashed to 0.82 and never turned, gone at 3.4 s and 3.7; the line into the open on all {lineTiles} wooded tiles with open ground beside them (never toward wood), straight out of all {edgeTiles} straight edges, the same twice, and 60° or more clear of a feller posed on it ({fellerTiles}); a young tile made a wood plays the growing, its grown trees held out of the mesh for 3 s, rising from half size, clear, to the mesh's own to the vertex, the saplings filling out and gone at 1.5 s, then given back; saplings rise in from a third, clear, in 1.5 s, held and given back, and fade when ploughed; a thinning plays a fall and a rising; each of the four kinds seen once; the clock frozen while paused; nothing on a fresh map, with the trees tick off or the watercolour off; a whole valley felled at once puts {capped} trees in motion, the rest at once"
                : $"[widths] tree motion: ⛔ {string.Join("; ", faults)}";
        }
        catch (System.Exception e)
        {
            return $"[widths] tree motion: ⛔ threw {e.GetType().Name}: {e.Message}";
        }
        finally
        {
            foreach ((GridPos tile, Terrain was) in changed)
            {
                map.SetTerrain(tile, was);
            }

            ForgetTheTreeMotion();
            _treeMotionForTheProbe = false;
            _treeClock = clockWas;
            Paused = wasPaused;
            ShowTrees(wasShown);
            ShowWatercolour(wasOn);
            _sceneryOf = null;
            RefreshTheScenery(world);
            _sceneryAtGeneration = world.TerrainGeneration;
        }

        void Fault(string what)
        {
            if (faults.Count < 8)
            {
                faults.Add(what);
            }
        }

        void Refresh() => RefreshTheScenery(world, generation: _sceneryAtGeneration + 1);

        void ClockAt(double at)
        {
            _treeClock = at;
            RetireTheTreeMotion();
        }

        void Set(GridPos tile, Terrain to)
        {
            changed.TryAdd(tile, map.TerrainAt(tile));
            map.SetTerrain(tile, to);
        }

        // Which way a straight edge faces (its middle open neighbour, 0, 2, 4 or 6), or -1.
        int StraightEdgeOf(GridPos tile)
        {
            for (int side = 0; side < 8; side += 2)
            {
                bool fits = true;
                for (int k = 0; k < 8 && fits; k++)
                {
                    (int dx, int dy) = Neighbour(k);
                    var beside = new GridPos(tile.X + dx, tile.Y + dy);
                    bool open = map.Contains(beside) && map.TerrainAt(beside) != Terrain.Forest;
                    int off = System.Math.Min((k - side + 8) % 8, (side - k + 8) % 8);
                    fits = map.Contains(beside) && open == (off <= 1);
                }

                if (fits)
                {
                    return side;
                }
            }

            return -1;
        }

        bool AnyOpenBeside(GridPos tile)
        {
            for (int k = 0; k < 8; k++)
            {
                (int dx, int dy) = Neighbour(k);
                var beside = new GridPos(tile.X + dx, tile.Y + dy);
                if (map.Contains(beside) && map.TerrainAt(beside) != Terrain.Forest)
                {
                    return true;
                }
            }

            return false;
        }

        GridPos FindTile(System.Func<GridPos, bool> fits)
        {
            for (int i = 0; i < map.Tiles.Count; i++)
            {
                var tile = new GridPos(map.MinX + (i % map.Width), map.MinY + (i / map.Width));
                if (fits(tile))
                {
                    return tile;
                }
            }

            throw new System.InvalidOperationException("the probe's valley has no tile to check this on");
        }

        static bool Far(GridPos a, GridPos b) => System.Math.Abs(a.X - b.X) + System.Math.Abs(a.Y - b.Y) > 4;

        TreeMotion? Only(GridPos tile, TreeMotionKind kind, bool quiet = false)
        {
            TreeMotion? found = null;
            int count = 0;
            foreach (TreeMotion motion in _treeMotions)
            {
                if (motion.Tile == tile && motion.Kind == kind)
                {
                    found = motion;
                    count++;
                }
            }

            if (count != 1 && !quiet)
            {
                Fault($"{tile} changed and started {count} {kind} motions (want one)");
            }

            return count == 1 ? found : null;
        }
    }

    private static bool SameTrees(PaintedTree[] a, PaintedTree[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Whether a frame lays exactly what <see cref="MeshBuilder.Sprite"/> lays for the tree: its six corners and their texture coordinates.</summary>
    private static bool AsTheMesh(PaintedTree tree, TreeFrame frame)
    {
        var builder = new MeshBuilder();
        builder.Sprite(tree.Foot, tree.Half, tree.Cell, tree.Mirrored);
        var mesh = new ArrayMesh();
        builder.AddSurfaceTo(mesh);
        Godot.Collections.Array arrays = mesh.SurfaceGetArrays(0);
        Vector2[] corners = arrays[(int)Mesh.ArrayType.Vertex].AsVector2Array();
        Vector2[] uvs = arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();

        var vertices = new List<Vector2>();
        var colours = new List<Color>();
        var laid = new List<Vector2>();
        LayTheTreeFrame(vertices, colours, laid, tree, frame);
        if (vertices.Count != corners.Length || frame.Alpha < 1f)
        {
            return false;
        }

        for (int i = 0; i < corners.Length; i++)
        {
            if (vertices[i].DistanceTo(corners[i]) > 1e-5f || laid[i].DistanceTo(uvs[i]) > 1e-6f)
            {
                return false;
            }
        }

        return true;
    }
}
