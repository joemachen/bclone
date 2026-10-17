using Bclone.Sim.Core;
using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐ The nomads walk in from the valley's edge (`nomads.md §4.3a`, D573; Joe's note 4, D568: <i>"i want to see the
/// nomads immigrants walking from off-screen to the village … they should have an indicator above their heads while
/// they are walking"</i>).
/// </summary>
/// <remarks>
/// <para>
/// <b>View only.</b> In the sim a band is villagers standing at <see cref="SimWorld.BandCamp"/> from the tick they
/// settle (`SimWorld.Nomads.cs`); nothing here changes that, hashes anything or is saved. The map notices the
/// arrivals — anybody alive whose <see cref="Villager.ArrivedAtTick"/> is a tick computed since it last looked — works out
/// <b>once, at the arrival</b> (CLAUDE.md: kept where the state changes) the edge tile the one cost field reaches
/// most cheaply from the camp and the route from it, and for <see cref="WalkInSeconds"/> of real time draws each
/// of them along that route with a chevron bobbing overhead, filing in one behind the other. The route is the sim's
/// own (`TravelCostField.RouteFrom`) — never a second travel system — smoothed by a three-tile mean so the
/// four-connected staircase reads as a line, and the smoothed point never leaves its own tile (so never the water).
/// </para>
/// <para>
/// <b>Real seconds, like the meal mark (D531)</b>, so the walk reads the same at every speed — but <b>frozen while
/// the village is paused</b> (<see cref="Paused"/>): a paused village is a still picture. A load never replays a
/// walk-in: a new world is watched from its own tick. Re-founders (D567) walk in by the same rule; the first four
/// founders (<c>ArrivedAtTick</c> 0) never do. The shell holds the arrival moment until the last of them is in
/// (<c>Main.ShowAnyMoment</c>) and keeps the village at 1× meanwhile.
/// </para>
/// </remarks>
public partial class VillageMap
{
    /// <summary>How long the leader's walk from the edge to the camp takes — real time, whatever the distance (Joe).</summary>
    internal const double WalkInSeconds = 8.0;

    /// <summary>Each nomad sets off this long after the one before, so a band files in rather than arriving as a blob.</summary>
    internal const double WalkInFileGapSeconds = 0.35;

    /// <summary>The chevron fades over the last of the walk, so nobody arrives with a mark still over their head.</summary>
    internal const double ArrivalMarkFadeSeconds = 0.5;

    /// <summary>The walk bends to wherever the arrival actually stands over this last share of the walk, so the handover to the ordinary glide never snaps.</summary>
    internal const float WalkInBlendShare = 0.15f;

    /// <summary>The arrival mark — the dun the huddle used (D565), between the ground and a villager's chalk.</summary>
    internal static readonly Color ArrivalMark = new("#c9b48a");

    /// <summary>One band's walk from the edge to the camp, worked out at the arrival and read by every frame until the last is in.</summary>
    private sealed class WalkIn
    {
        public Vector2[] Path = System.Array.Empty<Vector2>();
        public float[] Cumulative = System.Array.Empty<float>();
        public List<int> Ids = new();
        public double StartedAt;
    }

    private readonly List<WalkIn> _walkIns = new();

    /// <summary>The world the arrivals are being watched on, and the tick they were last looked for. A new world starts from its own tick.</summary>
    private SimWorld? _watchedWorld;
    private ulong _arrivalsSeenThroughTick;

    /// <summary>The map's clock for the walk-in: real seconds, summed from frame deltas while the village runs. View only.</summary>
    private double _walkClock;

    /// <summary>Whether the village is paused — set by the shell each refresh; the walk-in's clock stands still while it is.</summary>
    public bool Paused { get; set; }

    /// <summary>Whether anybody is still walking in — the shell holds the arrival moment and the 1× until nobody is.</summary>
    public bool WalkInUnderway
    {
        get
        {
            for (int i = 0; i < _walkIns.Count; i++)
            {
                if (WalkInProgress(_walkIns[i].StartedAt, _walkIns[i].Ids.Count - 1, _walkClock) < 1f)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Look for arrivals since the last look and begin their walk-in. Called by the shell <b>before</b> it shows any
    /// moment — the map's own draw runs after the moment panel would already be up. Returns whether a walk began.
    /// </summary>
    public bool WatchForArrivals(SimWorld world)
    {
        if (!ReferenceEquals(world, _watchedWorld))
        {
            // A new village, or a save loaded: watched from here on, nothing replayed.
            _watchedWorld = world;
            _arrivalsSeenThroughTick = world.Tick;
            _walkIns.Clear();
            return false;
        }

        if (world.Tick == _arrivalsSeenThroughTick)
        {
            return false;
        }

        // [since, now) — every tick computed since the last look — so a skip that steps a year in one frame still
        // catches the knock inside it.
        List<int> ids = ArrivalsSince(world.Villagers, _arrivalsSeenThroughTick, world.Tick);
        _arrivalsSeenThroughTick = world.Tick;
        return ids.Count > 0 && BeginWalkIn(world, ids);
    }

    /// <summary>
    /// Everybody alive who arrived in a tick computed between two looks — <c>[since, now)</c> — by id. Pure, so the
    /// probe can pose it.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Not <c>(since, now]</c> (D574/D575).</b> <c>SimLoop.StepOnce</c> runs the systems at tick <c>T</c> and then
    /// moves the clock to <c>T + 1</c>, so a band is stamped <c>T</c> and first seen by a look at <c>T + 1</c>; the
    /// look before was at <c>T</c>. A load watched from <c>T + 1</c> starts its window there and never replays
    /// <c>T</c>. <c>ArrivedAtTick</c> 0 means nobody arrived — founders and the born-here — and a new game is
    /// watched from tick 0, so it is refused by name: without it the founders would walk in from the edge.
    /// </remarks>
    internal static List<int> ArrivalsSince(IReadOnlyList<Villager> villagers, ulong since, ulong now)
    {
        var ids = new List<int>();
        for (int i = 0; i < villagers.Count; i++)
        {
            Villager villager = villagers[i];
            if (villager.Alive && villager.ArrivedAtTick != 0UL
                && villager.ArrivedAtTick >= since && villager.ArrivedAtTick < now)
            {
                ids.Add(villager.Id);
            }
        }

        ids.Sort();
        return ids;
    }

    /// <summary>
    /// The tile on the valley's edge that the one cost field reaches most cheaply from the camp, or null when no edge
    /// tile reaches it at all (an island). Ties go to the first in a fixed sweep: the top row, the bottom row, then
    /// the two sides.
    /// </summary>
    internal static GridPos? EdgeNearestTheCamp(SimWorld world, GridPos camp)
    {
        GeneratedMap map = world.Map;
        int maxX = map.MinX + map.Width - 1;
        int maxY = map.MinY + map.Height - 1;
        GridPos? best = null;
        int cheapest = TravelCostField.Unreachable;

        for (int x = map.MinX; x <= maxX; x++)
        {
            Consider(new GridPos(x, map.MinY));
            Consider(new GridPos(x, maxY));
        }

        for (int y = map.MinY + 1; y < maxY; y++)
        {
            Consider(new GridPos(map.MinX, y));
            Consider(new GridPos(maxX, y));
        }

        return best;

        void Consider(GridPos edge)
        {
            if (!TerrainRules.IsPassable(map.TerrainAt(edge)))
            {
                return;
            }

            int cost = world.TravelCost.Cost(edge, camp);
            if (cost != TravelCostField.Unreachable && cost < cheapest)
            {
                cheapest = cost;
                best = edge;
            }
        }
    }

    /// <summary>The route a band walks in by: the cheapest edge tile, then the sim's own route from it to the camp. Empty when there is none.</summary>
    internal static List<GridPos> WalkInRoute(SimWorld world)
    {
        GridPos camp = world.BandCamp.ToTile();
        var route = new List<GridPos>();
        if (EdgeNearestTheCamp(world, camp) is not GridPos edge)
        {
            return route;
        }

        route.Add(edge);
        if (edge != camp)
        {
            List<GridPos> steps = world.TravelCost.RouteFrom(edge, camp);
            if (steps.Count == 0)
            {
                route.Clear();
                return route;
            }

            route.AddRange(steps);
        }

        return route;
    }

    /// <summary>Begin the walk-in for <paramref name="ids"/> along the valley's own route to the camp; false when there is no route and they appear at the camp as before.</summary>
    private bool BeginWalkIn(SimWorld world, List<int> ids)
    {
        List<GridPos> route = WalkInRoute(world);
        if (route.Count == 0)
        {
            return false;
        }

        BeginWalkIn(route, ids);
        return true;
    }

    /// <summary>Begin a walk-in along <paramref name="route"/> for <paramref name="ids"/>, the lowest id leading — the live door and the probe's.</summary>
    internal void BeginWalkIn(IReadOnlyList<GridPos> route, List<int> ids)
    {
        Vector2[] path = SmoothedPath(route);
        var walk = new WalkIn
        {
            Path = path,
            Cumulative = CumulativeLengths(path),
            Ids = new List<int>(ids),
            StartedAt = _walkClock,
        };
        walk.Ids.Sort();
        _walkIns.Add(walk);
    }

    /// <summary>
    /// The route as points in the view's tile space, each inner point the mean of its tile's centre and its two
    /// neighbours' — a four-connected staircase becomes a line. ⭐ Every point stays inside its own tile (the two
    /// neighbours pull it at most a third of a tile along each axis), so nobody is ever drawn over the water the route
    /// went round. The ends are the tiles' own centres. Pure, so the probe can pose it.
    /// </summary>
    internal static Vector2[] SmoothedPath(IReadOnlyList<GridPos> route)
    {
        var path = new Vector2[route.Count];
        for (int i = 0; i < route.Count; i++)
        {
            Vector2 here = Centre(route[i]);
            path[i] = i == 0 || i == route.Count - 1
                ? here
                : (Centre(route[i - 1]) + here + Centre(route[i + 1])) / 3f;
        }

        return path;

        // The view's tile space puts tile (x, y)'s centre at (x, y) — `TheCentreOfATileDrawsWhereTheTileDoes`.
        static Vector2 Centre(GridPos tile) => new(tile.X, tile.Y);
    }

    /// <summary>Distance along <paramref name="path"/> to each of its points. Pure.</summary>
    internal static float[] CumulativeLengths(Vector2[] path)
    {
        var cumulative = new float[path.Length];
        for (int i = 1; i < path.Length; i++)
        {
            cumulative[i] = cumulative[i - 1] + path[i - 1].DistanceTo(path[i]);
        }

        return cumulative;
    }

    /// <summary>Where along <paramref name="path"/> a walker is at <paramref name="progress"/> (0 the edge, 1 the camp), by arc length. Pure.</summary>
    internal static Vector2 AlongThePath(Vector2[] path, float[] cumulative, float progress)
    {
        if (path.Length == 0)
        {
            return Vector2.Zero;
        }

        float total = cumulative[^1];
        if (progress >= 1f || total <= 0f)
        {
            return path[^1];
        }

        if (progress <= 0f)
        {
            return path[0];
        }

        float target = progress * total;
        int i = 0;
        while (i + 1 < path.Length - 1 && cumulative[i + 1] < target)
        {
            i++;
        }

        float span = cumulative[i + 1] - cumulative[i];
        float along = span <= 0f ? 0f : (target - cumulative[i]) / span;
        return path[i].Lerp(path[i + 1], along);
    }

    /// <summary>How far through their walk the nomad of <paramref name="rank"/> is at <paramref name="now"/>: 0 at the edge, 1 at the camp; rank 0 leads. Pure.</summary>
    internal static float WalkInProgress(double startedAt, int rank, double now) =>
        (float)Math.Clamp((now - startedAt - (rank * WalkInFileGapSeconds)) / WalkInSeconds, 0d, 1d);

    /// <summary>How much of the arrival mark shows at <paramref name="progress"/>: 1 on the road, fading to 0 over the last <see cref="ArrivalMarkFadeSeconds"/>. Pure.</summary>
    internal static float ArrivalMarkLeft(float progress)
    {
        float fadeShare = (float)(ArrivalMarkFadeSeconds / WalkInSeconds);
        return Mathf.Clamp((1f - progress) / fadeShare, 0f, 1f);
    }

    /// <summary>The mark's bob above the head, in pixels, at <paramref name="now"/> — a gentle rise and fall, so a walking band reads as walking. Pure.</summary>
    internal static float ArrivalMarkBob(double now) => 2f * Mathf.Sin((float)(Mathf.Tau * 1.2 * now));

    /// <summary>How far through their walk this villager is, or null when they are not walking in.</summary>
    private float? WalkInProgressOf(int villagerId)
    {
        for (int i = 0; i < _walkIns.Count; i++)
        {
            int rank = _walkIns[i].Ids.IndexOf(villagerId);
            if (rank < 0)
            {
                continue;
            }

            float progress = WalkInProgress(_walkIns[i].StartedAt, rank, _walkClock);
            return progress < 1f ? progress : null;
        }

        return null;
    }

    /// <summary>
    /// Where a villager walking in is drawn this frame, in the view's tile space — along the route, bending over the
    /// last <see cref="WalkInBlendShare"/> of the walk to wherever they actually stand (<paramref name="liveTile"/>, the
    /// ordinary glide), so when the walk ends the dot is already where the glide would put it and nothing snaps.
    /// Null when they are not walking in.
    /// </summary>
    private Vector2? WalkInPlaceOf(int villagerId, Vector2 liveTile, out float progress)
    {
        progress = 1f;
        if (_walkIns.Count == 0 || WalkInProgressOf(villagerId) is not float along)
        {
            return null;
        }

        progress = along;
        for (int i = 0; i < _walkIns.Count; i++)
        {
            if (!_walkIns[i].Ids.Contains(villagerId))
            {
                continue;
            }

            Vector2 onTheRoad = AlongThePath(_walkIns[i].Path, _walkIns[i].Cumulative, along);
            float blend = Mathf.Clamp((along - (1f - WalkInBlendShare)) / WalkInBlendShare, 0f, 1f);
            return onTheRoad.Lerp(liveTile, blend);
        }

        return null;
    }

    /// <summary>A small chevron above a nomad walking in, bobbing, fading as they reach the camp.</summary>
    private void DrawTheArrivalMark(Vector2 centre, float radius, float progress)
    {
        float left = ArrivalMarkLeft(progress);
        if (left <= 0f)
        {
            return;
        }

        float size = Mathf.Max(3f, radius * 0.9f);
        Vector2 apex = centre + new Vector2(0f, -(radius + size + 2f + ArrivalMarkBob(_walkClock)));
        var mark = new Color(ArrivalMark, ArrivalMark.A * left);

        // A chevron pointing down at the head: two strokes meeting at the apex (screen y runs down, so the arms rise).
        DrawLine(apex + new Vector2(-size, -size * 0.8f), apex, mark, 2f);
        DrawLine(apex, apex + new Vector2(size, -size * 0.8f), mark, 2f);
    }

    /// <summary>Forget every walk whose last nomad is in — once a tick, where the glide's bookkeeping rolls.</summary>
    private void PruneTheFinishedWalkIns()
    {
        for (int i = _walkIns.Count - 1; i >= 0; i--)
        {
            if (WalkInProgress(_walkIns[i].StartedAt, _walkIns[i].Ids.Count - 1, _walkClock) >= 1f)
            {
                _walkIns.RemoveAt(i);
            }
        }
    }

    /// <summary>The walk clock's frame step: real seconds while the village runs, nothing while it is paused.</summary>
    private void TickTheWalkClock(double delta)
    {
        if (!Paused)
        {
            _walkClock += delta;
        }
    }

    /// <summary>The probe's hands on the walk clock: run it forward without a frame.</summary>
    internal void AdvanceTheWalkClockForTheProbe(double seconds) => _walkClock += seconds;

    /// <summary>The probe's broom: drop every posed or live walk-in so the lines after read as they did.</summary>
    internal void ClearWalkInsForTheProbe() => _walkIns.Clear();

    /// <summary>
    /// ⭐ The nomads walk in from the edge (D573) — <b>a probe line</b>: on this valley the edge picked is the cheapest
    /// passable perimeter tile to the camp and the route from it is whole; a posed staircase smooths to points inside
    /// their own tiles with the ends unmoved; a walker is where the arc length says, the file sets off a gap apart, the
    /// mark fades; arrivals are told from founders, natives and the dead; the clock stands still paused; and a posed
    /// walker is drawn — and clicked — at the edge.
    /// </summary>
    public string TheNomadsWalkInFromTheEdge()
    {
        var faults = new List<string>();
        SimWorld world = _world!;

        // (a) The edge, against a brute-force pick over the same perimeter.
        GridPos camp = world.BandCamp.ToTile();
        GridPos? edge = EdgeNearestTheCamp(world, camp);
        GeneratedMap map = world.Map;
        int maxX = map.MinX + map.Width - 1;
        int maxY = map.MinY + map.Height - 1;
        if (edge is not GridPos start)
        {
            faults.Add("no edge tile reaches the camp");
        }
        else
        {
            bool onTheEdge = start.X == map.MinX || start.X == maxX || start.Y == map.MinY || start.Y == maxY;
            if (!onTheEdge || !map.Contains(start))
            {
                faults.Add($"the edge picked, {start}, is not on the valley's perimeter");
            }

            if (!TerrainRules.IsPassable(map.TerrainAt(start)))
            {
                faults.Add($"the edge picked, {start}, is water");
            }

            int picked = world.TravelCost.Cost(start, camp);
            bool cheaperFound = false;
            for (int x = map.MinX; x <= maxX && !cheaperFound; x++)
            {
                for (int y = map.MinY; y <= maxY; y++)
                {
                    if (x != map.MinX && x != maxX && y != map.MinY && y != maxY)
                    {
                        continue;
                    }

                    var tile = new GridPos(x, y);
                    if (!TerrainRules.IsPassable(map.TerrainAt(tile)))
                    {
                        continue;
                    }

                    int cost = world.TravelCost.Cost(tile, camp);
                    if (cost != TravelCostField.Unreachable && cost < picked)
                    {
                        faults.Add($"{tile} reaches the camp for {cost} but {start} was picked at {picked}");
                        cheaperFound = true;
                        break;
                    }
                }
            }

            List<GridPos> route = WalkInRoute(world);
            if (route.Count == 0 || route[0] != start || route[^1] != camp)
            {
                string ends = route.Count > 0 ? $"from {route[0]} to {route[^1]}" : "nowhere";
                faults.Add($"the route runs {route.Count} tiles {ends}, not from {start} to the camp {camp}");
            }

            for (int i = 0; i < route.Count; i++)
            {
                if (!map.Contains(route[i]) || !TerrainRules.IsPassable(map.TerrainAt(route[i])))
                {
                    faults.Add($"the route crosses {route[i]}, which is off the map or water");
                    break;
                }

                if (i > 0 && route[i - 1].ManhattanDistanceTo(route[i]) != 1)
                {
                    faults.Add($"the route jumps from {route[i - 1]} to {route[i]}");
                    break;
                }
            }
        }

        // (b) A staircase with a corner smooths to points inside their own tiles, ends unmoved.
        var staircase = new List<GridPos>
        {
            new(0, 0), new(1, 0), new(1, 1), new(2, 1), new(2, 2), new(3, 2), new(3, 3), new(3, 4), new(3, 5), new(4, 5),
        };
        Vector2[] path = SmoothedPath(staircase);
        for (int i = 0; i < path.Length; i++)
        {
            Vector2 off = path[i] - new Vector2(staircase[i].X, staircase[i].Y);
            if (Mathf.Abs(off.X) > (1f / 3f) + 0.0001f || Mathf.Abs(off.Y) > (1f / 3f) + 0.0001f)
            {
                faults.Add($"smoothed point {i} sits {off} from its tile's centre — outside the tile");
                break;
            }
        }

        if (path[0] != new Vector2(0, 0) || path[^1] != new Vector2(4, 5))
        {
            faults.Add($"the smoothed route's ends moved to {path[0]} and {path[^1]}");
        }

        if (path[1] == new Vector2(1, 0))
        {
            faults.Add("the staircase was not smoothed — its corner is still a corner");
        }

        // The walker is where the arc length says.
        float[] cumulative = CumulativeLengths(path);
        if (AlongThePath(path, cumulative, 0f) != path[0] || AlongThePath(path, cumulative, 1f) != path[^1])
        {
            faults.Add("a walker at progress 0 or 1 is not at the route's end");
        }

        Vector2 midway = AlongThePath(path, cumulative, 0.5f);
        float nearest = float.MaxValue;
        for (int i = 1; i < path.Length; i++)
        {
            nearest = Mathf.Min(nearest, Geometry2D.GetClosestPointToSegment(midway, path[i - 1], path[i]).DistanceTo(midway));
        }

        if (nearest > 0.001f)
        {
            faults.Add($"a walker half way is {nearest:F3} tiles off the route");
        }

        // (c) The file: rank 0 is in at the walk's length; rank 2 sets off two gaps later and is in two gaps later.
        const double Started = 50d;
        if (WalkInProgress(Started, 0, Started) != 0f
            || WalkInProgress(Started, 0, Started + WalkInSeconds) != 1f
            || WalkInProgress(Started, 2, Started + (2 * WalkInFileGapSeconds) - 0.01) != 0f
            || WalkInProgress(Started, 2, Started + WalkInSeconds + (2 * WalkInFileGapSeconds) - 0.01) >= 1f
            || WalkInProgress(Started, 2, Started + WalkInSeconds + (2 * WalkInFileGapSeconds)) != 1f)
        {
            faults.Add("the file does not set off a gap apart and arrive a gap apart");
        }

        float halfFaded = 1f - (float)(ArrivalMarkFadeSeconds / WalkInSeconds / 2d);
        if (ArrivalMarkLeft(0f) != 1f || ArrivalMarkLeft(0.5f) != 1f || ArrivalMarkLeft(1f) != 0f
            || Mathf.Abs(ArrivalMarkLeft(halfFaded) - 0.5f) > 0.001f)
        {
            faults.Add("the mark does not hold on the road and fade over the last half second");
        }

        // (d) Arrivals, told from founders, natives and the dead — stamped as the sim stamps them: the tick the
        // systems ran at, one before the tick the view first sees (D575).
        var roster = new List<Villager>
        {
            Posed(1, arrived: 0UL, alive: true),
            Posed(2, arrived: 0UL, alive: true),
            Posed(3, arrived: 99UL, alive: true),
            Posed(4, arrived: 99UL, alive: false),
            Posed(5, arrived: 100UL, alive: true),
        };
        List<int> arrived = ArrivalsSince(roster, 99UL, 100UL);
        if (arrived.Count != 1 || arrived[0] != 3)
        {
            faults.Add($"arrivals since 99 by 100 read [{string.Join(", ", arrived)}], want [3] — the band stamped 99, not the one stamped 100");
        }

        if (ArrivalsSince(roster, 100UL, 100UL).Count != 0)
        {
            faults.Add("a look at the tick already looked at finds arrivals");
        }

        if (ArrivalsSince(roster, 0UL, 1UL).Count != 0)
        {
            faults.Add("a new game's first look counts the founders (arrived 0) as arrivals");
        }

        // (e) Paused, the clock stands still — the frame step run by hand.
        double clockWas = _walkClock;
        bool pausedWas = Paused;
        Paused = true;
        TickTheWalkClock(1.0);
        bool stoodStill = _walkClock == clockWas;
        Paused = false;
        TickTheWalkClock(1.0);
        bool ran = _walkClock == clockWas + 1.0;
        Paused = pausedWas;
        _walkClock = clockWas;
        if (!stoodStill || !ran)
        {
            faults.Add("the walk clock runs while the village is paused, or stands still while it runs");
        }

        // (f) A posed walker is drawn at the edge — and clicked there — on the real route, not at the camp.
        if (edge is GridPos from && faults.Count == 0)
        {
            Villager? walker = null;
            for (int i = 0; i < world.Villagers.Count; i++)
            {
                if (world.Villagers[i].Alive)
                {
                    walker = world.Villagers[i];
                    break;
                }
            }

            if (walker is not null)
            {
                int walkInsWere = _walkIns.Count;
                BeginWalkIn(WalkInRoute(world), new List<int> { walker.Id });
                Vector2 drawn = DrawnCentre(walker);
                Vector2 atTheEdge = ToScreen(from);
                Villager? hit = VillagerAt(drawn);
                if (drawn.DistanceTo(atTheEdge) > 0.001f)
                {
                    faults.Add($"a walker just set off is drawn {drawn.DistanceTo(atTheEdge):F1}px from the edge tile");
                }

                if (hit is null || hit.Id != walker.Id)
                {
                    faults.Add("a click on the walker at the edge does not hit them");
                }

                _walkIns.RemoveRange(walkInsWere, _walkIns.Count - walkInsWere);
            }
        }

        return faults.Count == 0
            ? $"[widths] walk-in: ✅ the cheapest passable edge tile to the camp, the sim's own route from it smoothed inside its tiles, {WalkInSeconds} s in a file {WalkInFileGapSeconds} s apart, the mark fading, arrivals told from founders and the dead, the clock still when paused, the walker drawn and clicked at the edge"
            : $"[widths] walk-in: ⛔ {string.Join("; ", faults)}";

        Villager Posed(int id, ulong arrived, bool alive) => new()
        {
            Id = id,
            Name = "posed",
            LifespanYears = 1,
            Carried = new Stockpile(world.GoodsCatalog.Count),
            Position = world.BandCamp,
            ArrivedAtTick = arrived,
            Alive = alive,
        };
    }
}
