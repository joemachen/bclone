using Bclone.Sim.Core;
using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <summary>
/// ⭐ Leavers walk out to the valley's edge (`morale.md §13.2` item 9, W2 step 5b): D573's walk-in reversed.
/// </summary>
/// <remarks>
/// <para>
/// <b>View only.</b> In the sim a leaver is gone the tick they leave (<see cref="Villager.LeftAtTick"/>, D806): not alive,
/// off the roster, their house let go. Nothing here changes that, hashes anything or is saved, and nothing in the sim
/// waits on it. The map notices departures in the same window it notices arrivals — every tick computed since it last
/// looked — and works out <b>once, at the departure</b> (CLAUDE.md: kept where the state changes) one edge for each
/// household that goes, the edge the one cost field reaches most cheaply from its first member, and each member's route
/// to it: <c>TravelCost.RouteFrom</c>, the sim's own (never a second travel system), smoothed as the walk-in's is. Each
/// sets off from where they were drawn the frame before (a person resting at home comes out of the door), one behind
/// the other a file gap apart, with the walk-in's chevron overhead, and fades over the last
/// <see cref="WalkOutFadeSeconds"/> at the edge.
/// </para>
/// <para>
/// <b>The walk-in's clock</b> (real seconds, frozen while paused or while a moment is open), <b>but not its hold</b>: a
/// walk out never slows the village to 1× or holds a moment open. A load never replays one. A leaver walking out is not
/// clicked: the card of someone gone is the roster's.
/// </para>
/// </remarks>
public partial class VillageMap
{
    /// <summary>A leaver fades over this last stretch of their walk, the chevron with them, and is gone at the edge.</summary>
    internal const double WalkOutFadeSeconds = 1.0;

    /// <summary>One leaver's walk from where they stood to their household's edge, worked out at the departure.</summary>
    private sealed class WalkOut
    {
        public int Id;
        public Vector2[] Path = System.Array.Empty<Vector2>();
        public float[] Cumulative = System.Array.Empty<float>();
        public int Rank;
        public double StartedAt;
    }

    private readonly List<WalkOut> _walkOuts = new();

    /// <summary>
    /// Everybody who left in a tick computed between two looks — <c>[since, now)</c>, the walk-in's window and for the
    /// same reason (D575: a party stamped <c>T</c> is first seen by a look at <c>T + 1</c>) — by id. Pure, so the probe
    /// can pose it.
    /// </summary>
    internal static List<int> DeparturesSince(IReadOnlyList<Villager> villagers, ulong since, ulong now)
    {
        var ids = new List<int>();
        for (int i = 0; i < villagers.Count; i++)
        {
            if (villagers[i].LeftAtTick is ulong left && left >= since && left < now)
            {
                ids.Add(villagers[i].Id);
            }
        }

        ids.Sort();
        return ids;
    }

    /// <summary>The route a leaver walks out by: their own tile, then the sim's own route from it to the edge. Empty when there is none.</summary>
    internal static List<GridPos> WalkOutRoute(SimWorld world, GridPos from, GridPos edge)
    {
        var route = new List<GridPos> { from };
        if (from != edge)
        {
            List<GridPos> steps = world.TravelCost.RouteFrom(from, edge);
            if (steps.Count == 0)
            {
                route.Clear();
                return route;
            }

            route.AddRange(steps);
        }

        return route;
    }

    /// <summary>
    /// Begin the walk out for <paramref name="ids"/>, household by household: one edge a household, from its lowest id;
    /// each member on their own route to it from where they were drawn, filing a gap apart. Someone with no route (an
    /// island) is simply gone, as before 5b.
    /// </summary>
    private void BeginWalkOuts(SimWorld world, List<int> ids)
    {
        var parties = new SortedDictionary<int, List<Villager>>();
        for (int i = 0; i < ids.Count; i++)
        {
            if (world.FindVillager(ids[i]) is not Villager leaver)
            {
                continue;
            }

            if (!parties.TryGetValue(leaver.HouseholdId, out List<Villager>? party))
            {
                parties[leaver.HouseholdId] = party = new List<Villager>();
            }

            party.Add(leaver);
        }

        foreach (List<Villager> party in parties.Values)
        {
            party.Sort((a, b) => a.Id.CompareTo(b.Id));
            if (EdgeNearest(world, party[0].Tile) is not GridPos edge)
            {
                continue;
            }

            int rank = 0;
            foreach (Villager leaver in party)
            {
                List<GridPos> route = WalkOutRoute(world, leaver.Tile, edge);
                if (route.Count == 0)
                {
                    continue;
                }

                BeginWalkOut(leaver.Id, route, DrawnTile(leaver), rank++);
            }
        }
    }

    /// <summary>Begin one leaver's walk along <paramref name="route"/>, setting off from <paramref name="from"/> (the view's tile space) — the live door and the probe's.</summary>
    internal void BeginWalkOut(int id, IReadOnlyList<GridPos> route, Vector2 from, int rank)
    {
        Vector2[] path = SmoothedPath(route);
        path[0] = from;
        _walkOuts.Add(new WalkOut
        {
            Id = id,
            Path = path,
            Cumulative = CumulativeLengths(path),
            Rank = rank,
            StartedAt = _walkClock,
        });
    }

    /// <summary>How much of a leaver shows at <paramref name="progress"/>: all of them on the road, fading to nothing over the last <see cref="WalkOutFadeSeconds"/>. Pure.</summary>
    internal static float WalkOutLeft(float progress)
    {
        float fadeShare = (float)(WalkOutFadeSeconds / WalkInSeconds);
        return Mathf.Clamp((1f - progress) / fadeShare, 0f, 1f);
    }

    /// <summary>Where a leaver is drawn this frame, in the view's tile space, and how much of them shows; null once they are at the edge, or not walking out.</summary>
    private Vector2? WalkOutPlaceOf(int villagerId, out float shown)
    {
        shown = 0f;
        for (int i = 0; i < _walkOuts.Count; i++)
        {
            WalkOut walk = _walkOuts[i];
            if (walk.Id != villagerId)
            {
                continue;
            }

            float progress = WalkInProgress(walk.StartedAt, walk.Rank, _walkClock);
            if (progress >= 1f)
            {
                return null;
            }

            shown = WalkOutLeft(progress);
            return AlongThePath(walk.Path, walk.Cumulative, progress);
        }

        return null;
    }

    /// <summary>Everybody walking out this frame who is still on the road — gone from the sim, so the draw asks here, never the roster.</summary>
    private void AddTheLeavers(SimWorld world, HashSet<int> stillOnTheMap, List<(Villager Who, Vector2 Tile, float Shown)> into)
    {
        for (int i = 0; i < _walkOuts.Count; i++)
        {
            int id = _walkOuts[i].Id;
            if (WalkOutPlaceOf(id, out float shown) is not Vector2 tile || world.FindVillager(id) is not Villager leaver || leaver.Alive)
            {
                continue;
            }

            stillOnTheMap.Add(id);
            into.Add((leaver, tile, shown));
        }
    }

    /// <summary>Forget every walk out that has reached the edge — once a tick, with the walk-ins.</summary>
    private void PruneTheFinishedWalkOuts()
    {
        for (int i = _walkOuts.Count - 1; i >= 0; i--)
        {
            if (WalkInProgress(_walkOuts[i].StartedAt, _walkOuts[i].Rank, _walkClock) >= 1f)
            {
                _walkOuts.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// ⭐ Leavers walk out to the edge (5b) — <b>a probe line</b>: departures are told from the living, the dead and those
    /// who left outside the window; a leaver's route runs on the sim's own steps from their tile to the edge nearest them;
    /// posed walking, they are drawn where they stood, then along the road, fading at the edge, never clicked; the walk
    /// holds nothing (no 1×, no moment), and it is forgotten at the edge.
    /// </summary>
    public string TheLeaversWalkOutToTheEdge()
    {
        var faults = new List<string>();
        SimWorld world = _world!;

        // (a) Departures, stamped as the sim stamps them: the tick the systems ran at, one before the view first sees it.
        var roster = new List<Villager>
        {
            Posed(1, left: null, alive: true),
            Posed(2, left: 99UL, alive: false),
            Posed(3, left: 98UL, alive: false),
            Posed(4, left: 100UL, alive: false),
            Posed(5, left: null, alive: false),
        };
        List<int> gone = DeparturesSince(roster, 99UL, 100UL);
        if (gone.Count != 1 || gone[0] != 2)
        {
            faults.Add($"departures since 99 by 100 read [{string.Join(", ", gone)}], want [2] — the party stamped 99, not 98, 100, the living or the dead");
        }

        // (b) A leaver's route: from their tile, on the sim's own four-connected steps, to the edge nearest them.
        Villager? who = null;
        for (int i = 0; i < world.Villagers.Count && who is null; i++)
        {
            if (world.Villagers[i].Alive)
            {
                who = world.Villagers[i];
            }
        }

        if (who is null)
        {
            return "[widths] walk out: ⛔ nobody alive to pose";
        }

        GridPos? edge = EdgeNearest(world, who.Tile);
        List<GridPos> route = edge is GridPos to ? WalkOutRoute(world, who.Tile, to) : new List<GridPos>();
        if (edge is not GridPos end || route.Count == 0 || route[0] != who.Tile || route[^1] != end)
        {
            faults.Add($"no whole route from {who.Tile} to the edge ({route.Count} tiles)");
            return $"[widths] walk out: ⛔ {string.Join("; ", faults)}";
        }

        for (int i = 1; i < route.Count; i++)
        {
            if (route[i - 1].ManhattanDistanceTo(route[i]) != 1 || !TerrainRules.IsPassable(world.Map.TerrainAt(route[i])))
            {
                faults.Add($"the route jumps or wets its feet at {route[i]}");
                break;
            }
        }

        // (c) Posed walking out: drawn where they stood, then on the road, faded at the edge, never clicked, holding nothing.
        int walkOutsWere = _walkOuts.Count;
        double clockWas = _walkClock;
        // Posed off their tile's centre, as someone fanned on a crowded tile or on a doorstep stands, so a walk that set
        // off from the tile's centre instead of from them is told from one that did not.
        Vector2 stood = DrawnTile(who) + new Vector2(0.3f, -0.2f);
        BeginWalkOut(who.Id, route, stood, rank: 0);
        if (WalkInUnderway && _walkIns.Count == 0)
        {
            faults.Add("a walk out reads as a walk-in underway — it would hold the village at 1×");
        }

        if (WalkOutPlaceOf(who.Id, out float atFirst) is not Vector2 first || first.DistanceTo(stood) > 0.001f || atFirst != 1f)
        {
            faults.Add("a leaver just set off is not drawn, whole, where they stood");
        }

        AdvanceTheWalkClockForTheProbe(WalkInSeconds / 2d);
        Vector2[] path = SmoothedPath(route);
        path[0] = stood;
        Vector2 halfway = AlongThePath(path, CumulativeLengths(path), 0.5f);
        if (WalkOutPlaceOf(who.Id, out float midShown) is not Vector2 mid || mid.DistanceTo(halfway) > 0.001f || midShown != 1f)
        {
            faults.Add("a leaver half way is not on the road, whole");
        }

        AdvanceTheWalkClockForTheProbe((WalkInSeconds / 2d) - (WalkOutFadeSeconds / 2d));
        WalkOutPlaceOf(who.Id, out float fading);
        if (Mathf.Abs(fading - 0.5f) > 0.01f)
        {
            faults.Add($"half a fade from the edge, {fading:F2} of the leaver shows, not half");
        }

        // The posed leaver is alive in the world, so the click test would find them by the ordinary rule; a real leaver is
        // not alive, and AddTheLeavers is the only door they are drawn through — asked for one who is gone.
        var drawn = new List<(Villager Who, Vector2 Tile, float Shown)>();
        AddTheLeavers(world, new HashSet<int>(), drawn);
        if (drawn.Count != 0)
        {
            faults.Add("a living person posed walking out was drawn twice — once as a leaver");
        }

        AdvanceTheWalkClockForTheProbe(WalkOutFadeSeconds);
        if (WalkOutPlaceOf(who.Id, out _) is not null)
        {
            faults.Add("a leaver at the edge is still drawn");
        }

        PruneTheFinishedWalkOuts();
        if (_walkOuts.Count != walkOutsWere)
        {
            faults.Add("a walk out that reached the edge was not forgotten");
        }

        _walkOuts.RemoveRange(walkOutsWere, _walkOuts.Count - walkOutsWere);
        _walkClock = clockWas;

        // (d) A real leaver: gone from the sim, begun by the walk out's own door (BeginWalkOuts, which the shell's watch
        // calls), drawn through AddTheLeavers and kept on the map for it — and never clicked. Put back after. ⚠️ Not
        // through the watch itself: the probe's world is a new one at tick 0, and a departure must be stamped before the
        // tick a look sees, so the watch's one call is not covered here (D809).
        bool aliveWas = who.Alive;
        ulong? leftWas = who.LeftAtTick;
        who.Alive = false;
        who.LeftAtTick = world.Tick;
        BeginWalkOuts(world, new List<int> { who.Id });
        var seen = new HashSet<int>();
        var leaving = new List<(Villager Who, Vector2 Tile, float Shown)>();
        AddTheLeavers(world, seen, leaving);
        if (leaving.Count != 1 || leaving[0].Who.Id != who.Id || !seen.Contains(who.Id))
        {
            faults.Add($"a real leaver is drawn {leaving.Count} times, kept on the map {seen.Contains(who.Id)}");
        }
        else if (VillagerAt(ToScreen(leaving[0].Tile)) is Villager hit && hit.Id == who.Id)
        {
            faults.Add("a click on a leaver walking out selects them");
        }

        who.Alive = aliveWas;
        who.LeftAtTick = leftWas;
        _walkOuts.RemoveRange(walkOutsWere, _walkOuts.Count - walkOutsWere);

        return faults.Count == 0
            ? $"[widths] walk out: ✅ departures told from the living, the dead and outside the window; {route.Count} tiles on the sim's own steps from {who.Tile} to the edge {end}; drawn where they stood, on the road half way, half faded a half second from the edge, gone at it and forgotten; no hold; a real leaver, gone from the sim, drawn and never clicked"
            : $"[widths] walk out: ⛔ {string.Join("; ", faults)}";

        Villager Posed(int id, ulong? left, bool alive) => new()
        {
            Id = id,
            Name = "posed",
            LifespanYears = 1,
            Carried = new Stockpile(world.GoodsCatalog.Count),
            Position = world.BandCamp,
            LeftAtTick = left,
            Alive = alive,
        };
    }
}
