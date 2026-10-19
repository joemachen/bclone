using Bclone.Sim.Core;
using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐ The trading boat on the river (`specs/trade.md §4.4`, commit 7 — View II): drawn sailing from its edge to the post,
/// tied up there, and on off the far edge — its place derived from the boat's three ticks along a line found once per post.
/// </summary>
public partial class VillageMap
{
    private static readonly Color HullColour = new("#7a5636");
    private static readonly Color HullEdge = new("#4a3320");
    private static readonly Color SailColour = new("#efe6d2");

    /// <summary>The line the boat sails, and the post and the ground it was found for.</summary>
    private BoatRoute? _boatRoute;

    private (int PostId, int Terrain) _boatRouteFor = (-1, -1);

    /// <summary>How many times the boat's line has been found — the probe's proof that it is once per post (D606).</summary>
    public int BoatRoutesFound { get; private set; }

    /// <summary>
    /// The line for the post that stands, found the first time it is asked for and kept until the post or the ground
    /// changes — the river never does (CLAUDE.md: keep the index, rebuild it only where the state changes).
    /// </summary>
    private BoatRoute? TheBoatsRoute(SimWorld world)
    {
        Workplace? post = null;
        for (int i = 0; i < world.Workplaces.Count && post is null; i++)
        {
            if (world.Workplaces[i] is { Kind: JobKind.Merchant, IsSite: false, Closed: false } standing)
            {
                post = standing;
            }
        }

        if (post is null)
        {
            return null;
        }

        (int, int) key = (post.Id, world.TerrainGeneration);
        if (key == _boatRouteFor)
        {
            return _boatRoute;
        }

        _boatRouteFor = key;
        BoatRoutesFound++;
        GeneratedMap map = world.Map;
        var water = new bool[map.Width * map.Height];
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                water[(y * map.Width) + x] = map.TerrainAt(new GridPos(map.MinX + x, map.MinY + y)) == Terrain.Water;
            }
        }

        _boatRoute = BoatRoute.Find(map.MinX, map.MinY, map.Width, map.Height, water, InViewTiles(post.Position), MapGenerator.CourseOf(world.Config, world.Seed));
        return _boatRoute;
    }

    /// <summary>The boat, if one is on the water: a hull and a sail, turned the way it sails.</summary>
    private void DrawTheBoat()
    {
        SimWorld world = _world!;
        if (world.Boat is not TradeBoat boat || TheBoatsRoute(world) is not BoatRoute route)
        {
            return;
        }

        bool fromUpriver = world.Config.Neighbours[boat.NeighbourId].Direction == NeighbourDirection.Upriver;
        (bool shown, Vector2 at, Vector2 heading) = route.Where(world.Tick + _alpha, boat.AppearsAtTick, boat.DocksAtTick, boat.LeavesAtTick, world.BoatSailTicks, fromUpriver);
        if (!shown)
        {
            return;
        }

        foreach (Vector2[] part in BoatShape(at, heading))
        {
            Vector2[] screen = part.Select(ToScreen).ToArray();
            DrawColoredPolygon(screen, part.Length == 6 ? HullColour : SailColour);
            DrawPolyline(screen.Append(screen[0]).ToArray(), HullEdge, 1f);
        }
    }

    /// <summary>
    /// <c>boat:</c> (D606) — rivers drawn by hand, west to east and north to south, a post on a bank: the line runs edge to
    /// edge on water and passes the mooring; a boat from upriver is at its edge when it appears, at the mooring while tied
    /// up, past it once it leaves and gone when it has sailed off; one from downriver comes in from the other edge.
    /// </summary>
    /// <remarks>
    /// ⚠️ Not probed: <i>once per post</i> against the live world — the view cannot raise a post (D596). The cache is one
    /// key (<see cref="TheBoatsRoute"/>), read on every frame a boat is out.
    /// </remarks>
    public string TheBoatSailsTheRiver()
    {
        var faults = new List<string>();
        foreach ((string course, bool alongX) in new[] { ("we", true), ("ns", false) })
        {
            const int Long = 30, Wide = 12;
            int width = alongX ? Long : Wide, height = alongX ? Wide : Long;
            var water = new bool[width * height];
            for (int i = 0; i < water.Length; i++)
            {
                int across = alongX ? i / width : i % width;
                water[i] = across >= 4 && across <= 7;
            }

            Vector2 post = alongX ? new Vector2(15f, 2f) : new Vector2(2f, 15f);
            if (BoatRoute.Find(0, 0, width, height, water, post, course) is not BoatRoute route)
            {
                faults.Add($"no line found on a {course} river");
                continue;
            }

            Vector2 first = route.Points[0], last = route.Points[^1];
            bool edgeToEdge = alongX ? first.X == 0f && last.X == width - 1 : first.Y == 0f && last.Y == height - 1;
            int dry = 0;
            for (int k = 0; k + 1 < route.Points.Length; k++)
            {
                for (int q = 0; q < 4; q++)
                {
                    Vector2 at = route.Points[k].Lerp(route.Points[k + 1], q / 4f);
                    int x = Mathf.RoundToInt(at.X), y = Mathf.RoundToInt(at.Y);
                    dry += x < 0 || y < 0 || x >= width || y >= height || !water[(y * width) + x] ? 1 : 0;
                }
            }

            const ulong Appears = 100, Docks = 108, Leaves = 148, Sail = 8;
            (bool inView, Vector2 coming, Vector2 heading) = route.Where(Appears, Appears, Docks, Leaves, Sail, true);
            (_, Vector2 docked, _) = route.Where(Docks + 5, Appears, Docks, Leaves, Sail, true);
            (_, Vector2 going, _) = route.Where(Leaves + (Sail / 2), Appears, Docks, Leaves, Sail, true);
            (bool stillThere, _, _) = route.Where(Leaves + Sail, Appears, Docks, Leaves, Sail, true);
            (bool before, _, _) = route.Where(Appears - 1, Appears, Docks, Leaves, Sail, true);
            (_, Vector2 fromBelow, Vector2 upHeading) = route.Where(Appears, Appears, Docks, Leaves, Sail, false);
            float Along(Vector2 p) => alongX ? p.X : p.Y;
            const float mooring = 15f;
            string? fault = !edgeToEdge ? $"the {course} line runs from {first} to {last}, not edge to edge"
                : dry > 0 ? $"{dry} samples of the {course} line are on land"
                : !inView || before || stillThere ? $"on the {course} river the boat shows {before}/{inView}/{stillThere} before, as it appears and after it has gone"
                : Along(coming) > 0.5f || Along(heading) <= 0f ? $"a boat from upriver on the {course} river appears at {coming} heading {heading}"
                : Mathf.Abs(Along(docked) - mooring) > 1.01f ? $"a boat tied up on the {course} river is at {docked}, not by the post"
                : Along(going) <= mooring + 1f ? $"a boat leaving on the {course} river is at {going}, not past the post"
                : Along(fromBelow) < (alongX ? width : height) - 1.5f || Along(upHeading) >= 0f ? $"a boat from downriver on the {course} river appears at {fromBelow} heading {upHeading}"
                : null;
            if (fault is not null)
            {
                faults.Add(fault);
            }
        }

        // ⛔ A boat never cuts a corner of land: two stretches of water meeting only corner to corner are not a channel
        // (D606's V4 scored zero on straight rivers alone).
        var kinked = new bool[30 * 12];
        for (int i = 0; i < kinked.Length; i++)
        {
            int x = i % 30, y = i / 30;
            kinked[i] = x < 15 ? y >= 2 && y <= 4 : y >= 5 && y <= 7;
        }

        if (BoatRoute.Find(0, 0, 30, 12, kinked, new Vector2(5f, 1f), "we") is not null)
        {
            faults.Add("a river joined only corner to corner is sailed across the land at the corner");
        }

        return faults.Count == 0
            ? "[widths] boat: ✅ on rivers west–east and north–south the line runs edge to edge on water past the post; a boat from upriver appears at its edge, ties up by the post, sails on past it and is gone; one from downriver comes in from the other edge; water meeting only at a corner is not sailed"
            : $"[widths] boat: ⛔ {string.Join("; ", faults)}";
    }

    /// <summary>A boat's hull (six points) and sail (three) in tile coordinates, at a place and a heading — pure, for the probe.</summary>
    internal static Vector2[][] BoatShape(Vector2 at, Vector2 heading)
    {
        Vector2 along = heading.Normalized() * 0.65f;
        Vector2 across = new Vector2(-heading.Y, heading.X).Normalized() * 0.22f;
        Vector2[] hull =
        {
            at + along, at + (along * 0.45f) + across, at - (along * 0.85f) + across,
            at - along, at - (along * 0.85f) - across, at + (along * 0.45f) - across,
        };
        Vector2[] sail = { at + (along * 0.35f), at - (along * 0.55f) + (across * 2.2f), at - (along * 0.55f) };
        return new[] { hull, sail };
    }
}
