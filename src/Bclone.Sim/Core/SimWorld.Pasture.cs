using Bclone.Sim.Logging;
using Bclone.Sim.World;

namespace Bclone.Sim.Core;

/// <summary>What a barn's pasture fence is now — the barn card's button reads it.</summary>
public enum PastureFenceState
{
    /// <summary>No fence: the pasture can be painted and repainted freely.</summary>
    None = 0,

    /// <summary>Fenced on paper: the ground is locked and the builders have a site to raise it.</summary>
    Site = 1,

    /// <summary>The fence stands: its edges are walls, the gate the one way in.</summary>
    Standing = 2,
}

/// <summary>
/// ⭐ The fenced pasture (D598, `specs/livestock.md §0.2`): <i>Fence the pasture</i> locks a barn's painted ground and
/// queues a fence site; the builders raise it; its edges become walls (D404's layer — a player-built fence,
/// `fences-as-walls.md §10`), the gate the one way in; to change the pasture the fence comes down and the ground with it.
/// </summary>
/// <remarks>
/// <para>
/// Joe (D597): <i>"when the player places/gives the ground for a barn, it should trigger a builder to build a fence around
/// that area with an entrance/exit. once painted it has to be completely unpainted (and demolished) and then repainted."</i>
/// His calls: a button, not a stroke; the fence blocks walking but at its gate; logs by its length.
/// </para>
/// <para>
/// ⛔ <b>The fence is a fact; the walls restate it.</b> The barn's <c>FencedTiles</c>, <c>FenceGate</c> and
/// <c>FenceStands</c> are saved and hashed; the walls and the lock index are derived, never hashed (D335), rebuilt on
/// load — and written only where the fence changes, never per tick (CLAUDE.md).
/// </para>
/// </remarks>
public sealed partial class SimWorld
{
    /// <summary>
    /// Which barn's fence holds a tile — its fenced tiles and its gate's outside tile — from the moment the pasture is
    /// fenced on paper (D598). Derived, never hashed; maintained where fences are put up and taken down.
    /// </summary>
    private readonly Dictionary<GridPos, int> _pastureLocks = new();

    /// <summary>Where a barn's fence stands, or is being raised.</summary>
    public PastureFenceState FenceStateOf(Workplace barn)
    {
        ArgumentNullException.ThrowIfNull(barn);
        return barn.FencedTiles.Count == 0 ? PastureFenceState.None
            : barn.FenceStands ? PastureFenceState.Standing
            : PastureFenceState.Site;
    }

    /// <summary>The site raising this barn's fence, if one stands.</summary>
    public Workplace? FenceSiteOf(Workplace barn)
    {
        ArgumentNullException.ThrowIfNull(barn);
        for (int i = 0; i < Workplaces.Count; i++)
        {
            if (Workplaces[i].Construction?.FencesBarnId == barn.Id)
            {
                return Workplaces[i];
            }
        }

        return null;
    }

    /// <summary>The barn whose fence holds this tile — inside it, or the lane in front of its gate — or null.</summary>
    public Workplace? FencedPastureAt(GridPos tile) =>
        _pastureLocks.TryGetValue(tile, out int barn) ? FindWorkplace(barn) : null;

    /// <summary>
    /// The edges a fence round this barn's pasture would run, where its gate would be, and the stray tiles it would give
    /// back — or why it cannot be fenced.
    /// </summary>
    private (List<GridPos> Tiles, GridPos Gate, List<(GridPos From, GridPos To)> Edges, List<GridPos> Strays)? PlanAFence(Workplace barn, out string? why)
    {
        why = null;
        IReadOnlyList<int> held = Zones.WorkGroundOf(barn.Id);
        if (held.Count == 0)
        {
            why = "There is no pasture to fence — give the barn ground first.";
            return null;
        }

        var all = new List<GridPos>(held.Count);
        for (int i = 0; i < held.Count; i++)
        {
            all.Add(Zones.PositionOf(held[i]));
        }

        // ⭐ ONE PIECE IS FENCED; STRAYS ARE GIVEN BACK (D598). A fence has one gate, and a brush on rough ground leaves
        // crumbs — Joe's own pasture was 100 tiles and 2 apart from them, which a refusal would have refused.
        (List<GridPos> tiles, List<GridPos> strays) = TheMainPiece(all, barn.Tile);
        var inside = new HashSet<GridPos>(tiles);

        // ⭐ THE GATE: the fenced edge whose outside tile is nearest the barn, free and reached from the village —
        // in the pasture's own order, then N/E/S/W, so it is the same edge every run (§0.2.3 rule 2).
        GridPos home = FirstHomeOrFoundingSite();
        GridPos? gate = null;
        int nearest = int.MaxValue;
        for (int t = 0; t < tiles.Count; t++)
        {
            foreach ((int dx, int dy) in FourWays)
            {
                var front = new GridPos(tiles[t].X + dx, tiles[t].Y + dy);
                if (inside.Contains(front) || !Map.Contains(front) || Map.TerrainAt(front) == Terrain.Water
                    || StandsOn(front) || WhoseYard(front) is not null || _pastureLocks.ContainsKey(front)
                    || !TravelCost.CanReach(home, front))
                {
                    continue;
                }

                int far = Math.Abs(front.X - barn.Tile.X) + Math.Abs(front.Y - barn.Tile.Y);
                if (far < nearest)
                {
                    nearest = far;
                    gate = front;
                }
            }
        }

        if (gate is not GridPos gateFront)
        {
            why = "There is no way in — a gate needs open ground outside the pasture that the village can walk to.";
            return null;
        }

        List<(GridPos From, GridPos To)> edges = ZoneMap.FenceEdges(tiles, new[] { gateFront }, Array.Empty<GridPos>(), out _);
        return (tiles, gateFront, edges, strays);
    }

    private static readonly (int Dx, int Dy)[] FourWays = { (0, -1), (1, 0), (0, 1), (-1, 0) };

    /// <summary>
    /// The pasture's main piece — the largest 4-connected shape, ties to the one nearest the barn, then the pasture's own
    /// order — and every tile apart from it.
    /// </summary>
    private static (List<GridPos> Main, List<GridPos> Strays) TheMainPiece(List<GridPos> tiles, GridPos barn)
    {
        var inside = new HashSet<GridPos>(tiles);
        var seen = new HashSet<GridPos>();
        List<GridPos>? best = null;
        int bestNear = int.MaxValue;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (!seen.Add(tiles[i]))
            {
                continue;
            }

            var piece = new List<GridPos> { tiles[i] };
            var queue = new Queue<GridPos>();
            queue.Enqueue(tiles[i]);
            while (queue.Count > 0)
            {
                GridPos at = queue.Dequeue();
                foreach ((int dx, int dy) in FourWays)
                {
                    var next = new GridPos(at.X + dx, at.Y + dy);
                    if (inside.Contains(next) && seen.Add(next))
                    {
                        queue.Enqueue(next);
                        piece.Add(next);
                    }
                }
            }

            int near = piece.Min(t => Math.Abs(t.X - barn.X) + Math.Abs(t.Y - barn.Y));
            if (best is null || piece.Count > best.Count || (piece.Count == best.Count && near < bestNear))
            {
                best = piece;
                bestNear = near;
            }
        }

        var main = new HashSet<GridPos>(best!);
        var mainInOrder = new List<GridPos>(best!.Count);
        var strays = new List<GridPos>();
        for (int i = 0; i < tiles.Count; i++)
        {
            (main.Contains(tiles[i]) ? mainInOrder : strays).Add(tiles[i]);
        }

        return (mainInOrder, strays);
    }

    /// <summary>
    /// What fencing would do, in words — the barn card's button says it before it is pressed (D598): the lengths, the
    /// logs, and any stray tiles it would give back. Null when it cannot be fenced (<see cref="WhyNotFence"/> says why).
    /// </summary>
    public string? SayWhatFencingWouldDo(Workplace barn)
    {
        ArgumentNullException.ThrowIfNull(barn);
        if (WhyNotFence(barn) is not null || PlanAFence(barn, out _) is not { } plan)
        {
            return null;
        }

        string strays = plan.Strays.Count == 0
            ? string.Empty
            : $" — and the {plan.Strays.Count} {(plan.Strays.Count == 1 ? "tile" : "tiles")} apart from the pasture given back";
        return $"Fence {plan.Tiles.Count} tiles: {plan.Edges.Count} lengths and a gate, "
            + $"{PastureFenceRecipe(plan.Edges.Count).Describe(GoodsCatalog)}{strays}.";
    }

    /// <summary>What a fence of this many edges costs — the row prices one edge, as a bridge's prices one tile.</summary>
    public BuildingRecipe PastureFenceRecipe(int edges)
    {
        BuildingRecipe each = BuildingRecipe.For(BuildingKind.PastureFence, Config);
        var priced = new MaterialCost[each.Materials.Count];
        for (int i = 0; i < priced.Length; i++)
        {
            priced[i] = new MaterialCost(each.Materials[i].Goods, each.Materials[i].Amount * edges);
        }

        return new BuildingRecipe(each.WorkTicks * edges, priced);
    }

    /// <summary>Why this barn's pasture may not be fenced now — or null.</summary>
    public string? WhyNotFence(Workplace barn)
    {
        ArgumentNullException.ThrowIfNull(barn);
        if (barn.Kind != JobKind.Herdsman || barn.IsSite)
        {
            return "Only a standing barn's pasture is fenced.";
        }

        if (FenceStateOf(barn) != PastureFenceState.None)
        {
            return barn.FenceStands ? "The pasture is fenced already." : "The fence is being built already.";
        }

        if (PlanAFence(barn, out string? why) is not { } plan)
        {
            return why;
        }

        return WhatAFenceWouldWallOff(TrialWalls(plan.Edges), plan.Gate);
    }

    /// <summary>
    /// ⭐ <i>Fence the pasture</i> (a player input, D598): the ground is locked and a fence site is queued at the gate's
    /// outside tile — <b>logs and work by its edges</b>. Refused with the reason when it cannot be (§0.2.3 rule 1).
    /// </summary>
    public PlacementVerdict FenceThePasture(Workplace barn)
    {
        if (WhyNotFence(barn) is string why)
        {
            return PlacementVerdict.No(why);
        }

        (List<GridPos> _, GridPos gate, List<(GridPos From, GridPos To)> edges, List<GridPos> strays) = PlanAFence(barn, out _)!.Value;

        // The strays first, through the brush's own door — the fence goes round one piece (D598).
        for (int i = 0; i < strays.Count; i++)
        {
            EraseWorkGround(barn, strays[i]);
        }

        // Whole tiles, as the zone map indexes them — frozen, so a built fence never follows later paint (D388's lesson).
        var indices = new List<int>(Zones.WorkGroundOf(barn.Id));
        indices.Sort();
        barn.FencedTiles = indices;
        barn.FenceGate = gate;
        barn.FenceStands = false;
        LockThePasture(barn);

        BuildingRecipe recipe = PastureFenceRecipe(edges.Count);
        RaiseSiteFor(BuildingKind.PastureFence, Point.CentreOf(gate), $"{Capitalised(barn.Name)}'s fence", recipe, forHouseholdId: 0, fencesBarnId: barn.Id);
        string gaveBack = strays.Count == 0
            ? string.Empty
            : $" The {strays.Count} {(strays.Count == 1 ? "tile" : "tiles")} apart from the pasture were given back.";
        Narrate($"{Capitalised(barn.Name)}'s pasture is to be fenced — {edges.Count} lengths of fence and a gate, "
            + $"{recipe.Describe(GoodsCatalog)}. The pasture cannot be repainted while it stands.{gaveBack} {Clock.SeasonAndYear()}.",
            LogCategory.Building);
        return PlacementVerdict.Fine;
    }

    /// <summary>
    /// <i>Take the fence down</i> (a player input, D598): a fence still being built is cancelled — its logs come back,
    /// and the pasture stays painted; a standing fence comes down at once — half its logs back, its walls gone and the
    /// ground unpainted (Joe: <i>"completely unpainted"</i>).
    /// </summary>
    public PlacementVerdict TakeTheFenceDown(Workplace barn)
    {
        ArgumentNullException.ThrowIfNull(barn);
        switch (FenceStateOf(barn))
        {
            case PastureFenceState.Site:
                if (FenceSiteOf(barn) is Workplace site)
                {
                    CancelConstruction(site);
                }
                else
                {
                    UnlockThePasture(barn);
                }

                return PlacementVerdict.Fine;

            case PastureFenceState.Standing:
            {
                int edges = Zones.PastureFenceOf(barn.Id).Count;
                Zones.PullAPastureFenceDown(barn.Id);
                UnlockThePasture(barn);
                string recovered = ReturnToStore(barn.Tile, RefundFor(PastureFenceRecipe(edges)));
                UnpaintThePasture(barn);
                Narrate($"{Capitalised(barn.Name)}'s fence was taken down — {recovered} went back to store, and its "
                    + "pasture with it: paint new ground and fence it again"
                    + (barn.HerdHead > 0 ? $", or the {barn.HerdHead} head will go hungry" : string.Empty)
                    + $". {Clock.SeasonAndYear()}.", LogCategory.Building);
                return PlacementVerdict.Fine;
            }

            default:
                return PlacementVerdict.No("There is no fence to take down.");
        }
    }

    /// <summary>Take every tile of the barn's ground back, through the brush's own door (the field's un-ploughing with it).</summary>
    private void UnpaintThePasture(Workplace barn)
    {
        var held = new List<GridPos>();
        IReadOnlyList<int> ground = Zones.WorkGroundOf(barn.Id);
        for (int i = 0; i < ground.Count; i++)
        {
            held.Add(Zones.PositionOf(ground[i]));
        }

        for (int i = 0; i < held.Count; i++)
        {
            EraseWorkGround(barn, held[i]);
        }
    }

    /// <summary>A fence's site is finished: its edges become walls and the gate the one way in (§0.2.3 rule 5).</summary>
    private void FinishAFence(Workplace site, ConstructionSite plan)
    {
        RetireWorkplace(site);
        if (FindWorkplace(plan.FencesBarnId) is not { } barn || barn.FencedTiles.Count == 0)
        {
            Narrate($"{plan.Name} was finished, but its barn is gone. {Clock.SeasonAndYear()}.", LogCategory.Warning);
            return;
        }

        barn.FenceStands = true;
        RaiseTheFenceOf(barn);
        Narrate($"{plan.Name} is up — the pasture is shut in, and everybody goes in and out by its gate. "
            + $"{Clock.SeasonAndYear()}.", LogCategory.Building);
    }

    /// <summary>Wall a standing fence from the barn's facts — at its finishing, and on load.</summary>
    private void RaiseTheFenceOf(Workplace barn)
    {
        var tiles = new List<GridPos>(barn.FencedTiles.Count);
        for (int i = 0; i < barn.FencedTiles.Count; i++)
        {
            tiles.Add(Zones.PositionOf(barn.FencedTiles[i]));
        }

        StandingChanged();
        Zones.RaiseAPastureFence(barn.Id, tiles, barn.FenceGate!.Value);
    }

    /// <summary>The barn's fence comes down with it (§0.2.3 rule 8) — its walls, or its site.</summary>
    private void DropTheFenceWith(Workplace barn)
    {
        if (FenceStateOf(barn) == PastureFenceState.None)
        {
            return;
        }

        if (FenceSiteOf(barn) is Workplace site)
        {
            CancelConstruction(site);
        }

        Zones.PullAPastureFenceDown(barn.Id);
        UnlockThePasture(barn);
    }

    private void LockThePasture(Workplace barn)
    {
        for (int i = 0; i < barn.FencedTiles.Count; i++)
        {
            _pastureLocks[Zones.PositionOf(barn.FencedTiles[i])] = barn.Id;
        }

        if (barn.FenceGate is GridPos gate)
        {
            _pastureLocks[gate] = barn.Id;
        }

        StandingChanged();
    }

    /// <summary>Forget the fence: the barn's facts cleared, the lock index emptied of it. The walls are the caller's.</summary>
    private void UnlockThePasture(Workplace barn)
    {
        for (int i = 0; i < barn.FencedTiles.Count; i++)
        {
            _pastureLocks.Remove(Zones.PositionOf(barn.FencedTiles[i]));
        }

        if (barn.FenceGate is GridPos gate)
        {
            _pastureLocks.Remove(gate);
        }

        barn.FencedTiles = new List<int>();
        barn.FenceGate = null;
        barn.FenceStands = false;
        StandingChanged();
    }

    /// <summary>On load: every fence's lock, and every standing fence's walls, from the barns' facts.</summary>
    private void RebuildThePastureFences()
    {
        for (int i = 0; i < Workplaces.Count; i++)
        {
            Workplace barn = Workplaces[i];
            if (barn.FencedTiles.Count == 0)
            {
                continue;
            }

            LockThePasture(barn);
            if (barn.FenceStands)
            {
                RaiseTheFenceOf(barn);
            }
        }
    }

    /// <summary>A fenced pasture's ground cannot change (§0.2.3 rule 3) — the brush's refusal, or null.</summary>
    private string? WhyThePastureIsLocked(Workplace workplace) =>
        workplace.Kind == JobKind.Herdsman && workplace.FencedTiles.Count > 0
            ? "The pasture is fenced — take the fence down to change it."
            : null;

    /// <summary>Whether the brush may take this tile back from the workplace — the erase's own door (D598).</summary>
    public PlacementVerdict CanEraseWorkGround(Workplace workplace, GridPos tile)
    {
        ArgumentNullException.ThrowIfNull(workplace);
        if (WhyThePastureIsLocked(workplace) is string locked)
        {
            return PlacementVerdict.No(locked);
        }

        return Zones.WorkGroundOwner(tile) == workplace.Id
            ? PlacementVerdict.Fine
            : PlacementVerdict.No("That ground is not this building's.");
    }

    /// <summary>A fence's trial walls for the wall-off sweep, both sides of every edge (the yard's shape, `TrialFence`).</summary>
    private static Dictionary<GridPos, byte> TrialWalls(List<(GridPos From, GridPos To)> edges)
    {
        var walls = new Dictionary<GridPos, byte>(edges.Count * 2);
        for (int e = 0; e < edges.Count; e++)
        {
            (GridPos from, GridPos to) = edges[e];
            walls[from] = (byte)(walls.GetValueOrDefault(from) | ZoneMap.EdgeBit(to.X - from.X, to.Y - from.Y));
            walls[to] = (byte)(walls.GetValueOrDefault(to) | ZoneMap.EdgeBit(from.X - to.X, from.Y - to.Y));
        }

        return walls;
    }
}
