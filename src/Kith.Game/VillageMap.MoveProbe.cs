using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <summary>The move's probe line (`move-revamp.md §5`, D947).</summary>
public partial class VillageMap
{
    /// <summary>
    /// ⭐ Move, revamped, driven through the map's own clicks and keys — <b>a probe line</b>. Picking a building up holds its
    /// kind and its own facing; R turns it; a refused spot opens no question; a good one does and freezes the outline;
    /// <i>Not here</i> keeps it in hand; <i>Move it</i> marks a site at the turned facing (called off again); right-click
    /// lets go before it puts the tool down.
    /// </summary>
    /// <param name="boxShows">Whether the shell's box is up — the question has to reach the screen, not only the map.</param>
    internal string TheMoveIsAskedAndTurns(System.Func<bool> boxShows)
    {
        SimWorld world = _world!;
        var faults = new List<string>();

        // Anything standing the sim would let go of — a store, a workplace, a library, a well, the hall: whatever this
        // probe's valley has (twelve years in it may have only a site and the founders' stores).
        var standing = new List<GridPos>();
        standing.AddRange(world.StoreBuildings.Select(b => b.Tile));
        standing.AddRange(world.Workplaces.Where(w => !w.IsSite).Select(w => w.Tile));
        standing.AddRange(world.Libraries.Select(b => b.Tile));
        standing.AddRange(world.Wells.Select(b => b.Tile));
        if (world.TownHall is not null)
        {
            standing.Add(world.TownHall.Tile);
        }

        // ⚠️ AND WHEN THERE IS NONE, A LIBRARY IS POSED AND TAKEN AWAY AGAIN (Main.Cards' library line does the same): this
        // probe's valley, twelve years in, holds only the founders' cart and a granary still being built — the first run of
        // this line said "nothing in the village that may be moved" and proved nothing.
        Library? posed = null;
        if (!standing.Exists(t => world.WhyItStaysPut(t) is null))
        {
            GridPos centre = world.Map.FoundingSite;
            for (int r = 3; r < 20 && posed is null; r++)
            {
                foreach ((int dx, int dy) in new[] { (r, 0), (-r, 0), (0, r), (0, -r) })
                {
                    var tile = new GridPos(centre.X + dx, centre.Y + dy);
                    if (world.Map.Contains(tile) && world.CanBuildAt(BuildingKind.Library, tile, alreadyStanding: true).Allowed)
                    {
                        posed = new Library { Position = world.AnchorOn(BuildingKind.Library, tile), Name = "the probe's library", Shelves = 3 };
                        world.Libraries.Add(posed);
                        standing.Add(posed.Tile);
                        break;
                    }
                }
            }
        }

        if (!standing.Exists(t => world.WhyItStaysPut(t) is null))
        {
            if (posed is not null)
            {
                world.Libraries.Remove(posed);
            }

            return "[widths] move: ⛔ nothing in the village that may be moved — "
                + string.Join("; ", standing.Take(4).Select(t => $"{world.NameOnTheTile(t)}: {world.WhyItStaysPut(t)}"));
        }

        GridPos from = standing.Find(t => world.WhyItStaysPut(t) is null);
        BuildingKind kind = world.WhatStandsAt(from)!.Value;
        Angle facing = world.FootprintOn(from)!.Value.Facing;
        Vector2 Screen(GridPos tile) => ToScreen(Point.CentreOf(tile));
        void Hover(GridPos tile) => _GuiInput(new InputEventMouseMotion { Position = Screen(tile) });
        void Click(GridPos tile, MouseButton button = MouseButton.Left) =>
            _GuiInput(new InputEventMouseButton { ButtonIndex = button, Pressed = true, Position = Screen(tile) });

        int asked = 0;
        void Count(MoveQuestion? q)
        {
            if (q is not null)
            {
                asked++;
            }
        }

        GridPos? refused = null, allowed = null;
        MoveQuestionChanged += Count;
        try
        {
            PutTheToolDown();
            BeginMoving();
            Click(from);
            if (_moveFrom != from || _building != kind || _ghostFacing != facing)
            {
                faults.Add($"picked up, it held {_building} from {_moveFrom} facing {_ghostFacing.Raw}, not {kind} from {from} facing {facing.Raw}");
            }

            TurnTheGhost();
            Angle turned = facing + Angle.FromTurnFraction(1, 64);
            if (_ghostFacing != turned)
            {
                faults.Add($"R left it facing {_ghostFacing.Raw}, not {turned.Raw}");
            }

            // A spot the sim refuses, and one it allows, round the building — judged as the cursor would put it down.
            for (int r = 2; r < 16 && (refused is null || allowed is null); r++)
            {
                foreach ((int dx, int dy) in new[] { (r, 0), (-r, 0), (0, r), (0, -r), (r, r), (-r, -r), (r, -r), (-r, r) })
                {
                    var tile = new GridPos(from.X + dx, from.Y + dy);
                    if (!world.Map.Contains(tile))
                    {
                        continue;
                    }

                    PlacementVerdict v = world.CanMoveTo(from, WhereItWouldStand(Screen(tile)), turned);
                    if (!v.Allowed)
                    {
                        refused ??= tile;
                    }
                    else
                    {
                        // A warned spot is a good one too — this probe's valley has no builders, so every spot warns so.
                        allowed ??= tile;
                    }
                }
            }

            if (refused is not GridPos no || allowed is not GridPos yes)
            {
                return $"[widths] move: ⛔ round {from}, a refused spot {refused?.ToString() ?? "none"}, an allowed one {allowed?.ToString() ?? "none"}";
            }

            Hover(no);
            Click(no);
            if (AMoveIsAsked || asked != 0 || _moveFrom != from || _verdict.Allowed)
            {
                faults.Add($"a refused spot ({no}) asked {asked} times, or let go, or was drawn as allowed");
            }

            Hover(yes);
            if (!_verdict.Allowed)
            {
                faults.Add($"the outline over an allowed spot ({yes}) was drawn refused: {_verdict.Reason}");
            }

            Click(yes);
            if (!AMoveIsAsked || asked != 1 || !boxShows())
            {
                faults.Add($"a good spot ({yes}) asked {asked} times, the box up {boxShows()}");
            }

            Point frozen = _hoveredPoint;
            Hover(no);
            TurnTheGhost();
            if (_hoveredPoint != frozen || _ghostFacing != turned)
            {
                faults.Add("the outline moved or turned while the question was up");
            }

            if (!LetGoFirst() || AMoveIsAsked || boxShows() || _moveFrom != from)
            {
                faults.Add("Not here did not close the question, or let go of the building");
            }

            Hover(yes);
            Click(yes);
            ConfirmTheMove();
            Workplace? site = world.MoveUnderWay(from);
            if (site is null || site.Facing != turned || _moveFrom is not null || AMoveIsAsked)
            {
                faults.Add($"Move it marked {(site is null ? "no site" : $"a site facing {site.Facing.Raw}")}, want {turned.Raw}; still holding {_moveFrom}");
            }

            if (!world.CallOffTheMove(from) || world.MoveUnderWay(from) is not null)
            {
                faults.Add("the move could not be called off");
            }

            // Right-click lets go first, and only the second one puts the tool down.
            Click(from);
            Click(from, MouseButton.Right);
            bool letGo = _moveFrom is null && Tool == MapTool.Moving;
            Click(from, MouseButton.Right);
            if (!letGo || Tool != MapTool.None)
            {
                faults.Add($"right-click: let go {letGo}, then the tool down {Tool == MapTool.None}");
            }
        }
        finally
        {
            MoveQuestionChanged -= Count;
            PutTheToolDown();
            world.CallOffTheMove(from);
            if (posed is not null)
            {
                world.Libraries.Remove(posed);
            }
        }

        return faults.Count == 0
            ? $"[widths] move: ✅ {(posed is null ? world.NameOnTheTile(from) : "a posed library")} picked up at its own facing and turned by R; refused at {refused} with no question; "
                + $"asked at {allowed}, the box up and the outline still; Not here kept it in hand; Move it marked a site at the turn (called off); "
                + "right-click let go, then put the tool down"
            : $"[widths] move: ⛔ {string.Join("; ", faults)}";
    }
}
