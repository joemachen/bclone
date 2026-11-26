using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <remarks>
/// ⭐⭐ PEOPLE GO IN AND OUT BY THE DOOR (A139, <c>specs/indoors-and-smoke.md §3</c>; Joe: <i>"villagers should go inside
/// buildings not just appear on them (like when they are cold)"</i>, his calls answered — <i>"okay"</i>, <i>"instant"</i>,
/// <i>"permission granted"</i>). A walk that ends inside a building with a door is drawn ending <b>at its door</b>, and a
/// walk that starts inside one starts there: the sim's tile is kept, only where the view draws it moves. Someone indoors
/// is gone the moment their walk reaches the door, and back the moment they leave it. <b>View only</b>, and only with the
/// people painted — the dots (tick off) are as they were, to the byte.
/// </remarks>
public partial class VillageMap
{
    /// <summary>
    /// A painted door's step, in the view's tiles, by the door's own tile — kept as each roof is laid
    /// (<see cref="CachedRoof"/>) and dropped with it. Derived; never hashed.
    /// </summary>
    private readonly Dictionary<GridPos, Vector2> _paintedSteps = new();

    /// <summary>
    /// ⭐ Indoors (A90 call 3; A133; A139). Pure: at school and teaching, always; resting, with a house (A133 — in the open
    /// before the first one); <b>warming by a fire, and the smith at the forge and the batch trades at their work, once inside
    /// a building with a door</b> (A139 calls 3 and §3.3) — the house's smoke, the forge's, says what they are doing; <b>and an
    /// errand into a store, once at its door</b> (A141, Joe: <i>"Yes, step inside"</i>) — see <see cref="GoesInByTheDoor"/>.
    /// </summary>
    internal static bool Indoors(VillagerState state, bool housed, bool inADooredBuilding) => state switch
    {
        // School and teaching, and the library, for as long as taking paper takes (A246): the walk home starts at its door.
        VillagerState.AtSchool or VillagerState.Teaching or VillagerState.AtTheLibrary => true,

        // ⭐ An evening at the tavern, and its keeper at work (W3, `tavern.md §9`): inside, the lit window the art lane draws.
        VillagerState.AtTheTavern or VillagerState.KeepingTheTavern => true,
        VillagerState.Resting => housed,
        _ => GoesInByTheDoor(state) && inADooredBuilding,
    };

    /// <summary>
    /// Pure: the states that are in once their walk has reached a building with a door — warming by a fire, the forge and the
    /// batch trades at work (A139), and ⭐ <b>every errand into a store</b> (A141): putting a load in (a store, the farm's
    /// buffer, the market, the post), taking one out (a household's fetch, a builder's materials, a tool, a coat, a handcart,
    /// hay, a marketer's or the post's collecting, clearing a store), and a shopper at the market. Open-air places — a pile,
    /// the well, a barn, a farmhouse — have no door (D624), so the same states stand there in the open.
    /// </summary>
    internal static bool GoesInByTheDoor(VillagerState state) => state is
        VillagerState.SeekingShelter or VillagerState.Forging or VillagerState.WorkingABatch
        or VillagerState.HaulingToStore or VillagerState.HaulingToFarm or VillagerState.StockingTheMarket
        or VillagerState.StockingThePost or VillagerState.FetchingFromStore or VillagerState.FetchingMaterials
        or VillagerState.FetchingATool or VillagerState.FetchingACoat or VillagerState.FetchingAHandcart
        or VillagerState.FetchingHay or VillagerState.FetchingFromThePost or VillagerState.CollectingForMarket
        or VillagerState.CollectingForThePost or VillagerState.ClearingAStore or VillagerState.WalkingToTheMarket;

    /// <summary>
    /// Whether a villager in this state is drawn: anyone not <see cref="Indoors"/>, and anyone indoors whose walk has not
    /// reached the door yet (<paramref name="arrived"/> false) — they are seen walking up to it, then gone (call 4: instant).
    /// </summary>
    internal static bool ShownAsAPerson(VillagerState state, bool housed, bool inADooredBuilding = false, bool arrived = true) =>
        !Indoors(state, housed, inADooredBuilding) || !arrived;

    /// <summary>
    /// <see cref="ShownAsAPerson"/> for this villager: drawn and clicked by the one answer. The household is asked only of
    /// someone resting, the door only of someone in a state that can be indoors.
    /// </summary>
    private bool ShownOnTheMap(SimWorld world, Villager villager)
    {
        VillagerState state = villager.State;
        bool housed = state == VillagerState.Resting && world.HouseholdOf(villager).HasHome;
        bool inside = GoesInByTheDoor(state) && world.DoorCovering(villager.Tile) is Door { Shut: false };
        bool arrived = !_tiles.TryGetValue(villager.Id, out var known)
            || AtTheDoorAlready(AtTheDoor(known.Previous, state), AtTheDoor(known.Current, state), (float)_alpha);
        return ShownAsAPerson(state, housed, inside, arrived);
    }

    /// <summary>Within this of the door (tiles), someone going in is in (call 4, <i>"instant"</i>): no slow last shuffle.</summary>
    internal const float InAtTheDoorTiles = 0.15f;

    /// <summary>Pure: whether a glide from <paramref name="from"/> to <paramref name="to"/>, <paramref name="alpha"/> of the way, has reached it.</summary>
    internal static bool AtTheDoorAlready(Vector2 from, Vector2 to, float alpha) =>
        from.DistanceTo(to) * (1f - Mathf.Clamp(alpha, 0f, 1f)) <= InAtTheDoorTiles;

    /// <summary>
    /// ⭐ Where a person standing at this view tile is drawn: at the door of the building whose tile it is, or where they
    /// are. A fisher at the hut stays at the water (their place is piece (b)'s). Feet on the step: the drawn point is the
    /// belt (<see cref="PersonPointUp"/>), so it stands that far above it.
    /// </summary>
    private Vector2 AtTheDoor(Vector2 viewTile, VillagerState state)
    {
        if (!PaintsPeople || _world is not SimWorld world)
        {
            return viewTile;
        }

        // ⭐ Fishing, on the dock's end (A277, `fishing-animation.md §2`): the sim keeps them at the hut; the view stands them
        // where the water is, so a cast can be seen. A hut with no water by it (no dock drawn) keeps them where they are.
        if (state == VillagerState.Fishing)
        {
            return FishingSpotOf(world, viewTile) is (Vector2 spot, _) ? spot - new Vector2(0f, PersonTiles * PersonPointUp) : viewTile;
        }

        var tile = new GridPos(Mathf.RoundToInt(viewTile.X), Mathf.RoundToInt(viewTile.Y));
        if (world.DoorCovering(tile) is not Door { Shut: false } door)
        {
            return viewTile;
        }

        return StepOf(door) - new Vector2(0f, PersonTiles * PersonPointUp);
    }

    /// <summary>The door's step as it is painted, or — before its roof has been laid — the middle of its wall, just outside.</summary>
    private Vector2 StepOf(Door door) =>
        _paintedSteps.TryGetValue(door.Tile, out Vector2 painted) ? painted : DoorWallOf(door);

    /// <summary>Pure: the middle of the door's wall, a little outside it, in the view's tiles (a tile's centre is whole).</summary>
    internal static Vector2 DoorWallOf(Door door)
    {
        var inside = new Vector2(door.Tile.X, door.Tile.Y);
        var outside = new Vector2(door.Doorstep.X, door.Doorstep.Y);
        return inside + ((outside - inside) * 0.6f);
    }

    /// <summary>Kept as a roof is laid: its door's painted step, the centre of its <c>step</c> piece (A86's).</summary>
    private void KeepTheStep(RoofSignature signature, List<RoofPiece> pieces)
    {
        if (signature.Door is not Door door)
        {
            return;
        }

        foreach (RoofPiece piece in pieces)
        {
            if (piece.Kind == RoofPieceKind.Fill && piece.Part == "step")
            {
                _paintedSteps[door.Tile] = CentreOf(piece.Points);
                return;
            }
        }
    }

    private static Vector2 CentreOf(Vector2[] points)
    {
        Vector2 sum = Vector2.Zero;
        foreach (Vector2 p in points)
        {
            sum += p;
        }

        return sum / points.Length;
    }
}

/// <remarks>The probe's <c>indoors and smoke:</c> line (A139, <c>specs/indoors-and-smoke.md §5</c>) — every expectation named
/// from the spec, never read back from the code's own tables (art traps 17, 25).</remarks>
public partial class VillageMap
{
    internal static string TheDoorsAndTheSmokeHold()
    {
        try
        {
            var faults = new List<string>();
            void Want(bool ok, string what)
            {
                if (!ok)
                {
                    faults.Add(what);
                }
            }

            // Indoors (§3): who goes in, and only once inside a building with a door.
            Want(Indoors(VillagerState.Resting, housed: true, inADooredBuilding: false), "resting with a house is not indoors");
            Want(!Indoors(VillagerState.Resting, housed: false, inADooredBuilding: false), "resting in the open is indoors (A133)");
            Want(Indoors(VillagerState.SeekingShelter, false, inADooredBuilding: true), "warming by a fire inside is not indoors");
            Want(!Indoors(VillagerState.SeekingShelter, false, inADooredBuilding: false), "walking to a fire is indoors");
            Want(Indoors(VillagerState.Forging, false, true) && Indoors(VillagerState.WorkingABatch, false, true), "the smith or a batch trade at work inside is not indoors (call 3)");
            Want(!Indoors(VillagerState.Forging, false, false), "a smith not inside is indoors");
            Want(Indoors(VillagerState.AtTheLibrary, false, false) && !Indoors(VillagerState.WalkingToTheLibrary, false, true), "taking paper is out on the library's roof, or walking there is indoors (A246)");
            Want(!ShownAsAPerson(VillagerState.AtTheLibrary, true) && ShownAsAPerson(VillagerState.AtTheLibrary, true, arrived: false), "someone in the library is drawn, or is not seen walking up to it (A246)");
            Want(!Indoors(VillagerState.Fishing, false, true), "a fisher at the hut is indoors (piece (b)'s place)");
            Want(Indoors(VillagerState.HaulingToStore, false, true) && Indoors(VillagerState.FetchingFromStore, false, true)
                && Indoors(VillagerState.FetchingMaterials, false, true) && Indoors(VillagerState.StockingTheMarket, false, true),
                "putting a load into a store, fetching from one, a builder's materials or stocking the market, at the door, is not indoors (A141)");
            Want(!Indoors(VillagerState.HaulingToStore, false, false) && !Indoors(VillagerState.FetchingFromStore, false, false),
                "a hauler on the road, or at a pile with no door, is indoors");
            Want(!Indoors(VillagerState.DrawingWater, false, true) && !Indoors(VillagerState.TravelingHome, false, true) && !Indoors(VillagerState.Cutting, false, true),
                "someone drawing water, walking home or cutting is indoors");
            Want(ShownAsAPerson(VillagerState.Resting, true, false, arrived: false) && !ShownAsAPerson(VillagerState.Resting, true, false, arrived: true),
                "going home: not seen walking up to the door, or still seen once there");

            // The door (§3.1): the middle of its wall, just outside; reached within a step (call 4: instant).
            var door = new Door(new GridPos(3, -8), new GridPos(2, -8), Compass.West, Compass.West, DoorBlock.None, new GridPos(2, -8), false);
            Vector2 wall = DoorWallOf(door);
            Want(wall.IsEqualApprox(new Vector2(2.4f, -8f)), $"a west door's wall at {wall} (want (2.4, -8))");
            Want(!AtTheDoorAlready(Vector2.Zero, new Vector2(1f, 0f), 0.5f), "half way up the path counts as in");
            Want(AtTheDoorAlready(Vector2.Zero, new Vector2(1f, 0f), 0.9f) && AtTheDoorAlready(Vector2.One, Vector2.One, 0f), "at the door does not count as in");

            // The smoke (§2, §4): true to the sim.
            Want(HearthBurns(Season.Winter, true, 5), "a lived-in home with firewood in winter does not smoke");
            Want(!HearthBurns(Season.Summer, true, 5) && !HearthBurns(Season.Fall, true, 5), "a home smokes out of winter");
            Want(!HearthBurns(Season.Winter, true, 0), "a home with no firewood smokes");
            Want(!HearthBurns(Season.Winter, false, 5), "an empty house smokes");
            Want(Mathf.IsEqualApprox(SmokeOn(0f, true, 0.5), 0.5f) && Mathf.IsEqualApprox(SmokeOn(1f, false, 2.0), 0f), "smoke does not come and go over a second");
            Want(Mathf.IsEqualApprox(SmokeOn(0.5f, true, 0.0), 0.5f) && Mathf.IsEqualApprox(SmokeOn(0.5f, false, 0.0), 0.5f), "smoke changes while paused");
            (Vector2 low, _, float born) = PuffAt(0, 0f, 0.0);
            (Vector2 high, float big, _) = PuffAt(0, 0f, 2.0);
            (_, float small, _) = PuffAt(0, 0f, 0.4);
            Want(high.Y < low.Y && big > small, $"a puff does not rise and swell ({low} → {high}, {small:F2} → {big:F2} tiles)");
            Want(born < 0.01f, $"a puff starts at the flue showing {born:F2} (want none)");
            Want(!PuffAt(0, 0f, 1.0).Offset.IsEqualApprox(PuffAt(0, 0.37f, 1.0).Offset), "two chimneys smoke in step");

            // The chimneys smoke comes from: one flue for a smithy and the school (call 2), two for a manor.
            int Flues(RoofLook look) => FluesOf(RoofDrawing(look, Vector2.Zero, 2f, 1f, 0, Colors.White, 1f, null, Vector2.Zero, 1, false)).Length;
            int smith = Flues(RoofOf(JobKind.Smith)), school = Flues(RoofOf(JobKind.Teacher)), manor = Flues(RoofOf(BuildingKind.Manor)), forager = Flues(RoofOf(JobKind.Forager));
            Want(smith == 1 && school == 1 && manor == 2 && forager == 0, $"flues: smithy {smith}, school {school}, manor {manor}, forager's hut {forager} (want 1, 1, 2, 0)");

            // ⭐ The windmill's sails (A145): gather way and come to rest, never jump, still while paused; a run of their own,
            // under the body, turned about the hub; a cottage has nothing that turns.
            Want(Mathf.IsEqualApprox(SailSpeedOn(0f, true, SailSpinUpSeconds), 1f) && Mathf.IsEqualApprox(SailSpeedOn(1f, false, SailSpinDownSeconds), 0f),
                "the sails do not reach speed, or come to rest, in their seconds");
            float gathering = SailSpeedOn(0f, true, 1.0);
            Want(gathering > 0f && gathering < 1f, $"the sails jump to speed ({gathering:F2} after a second; want between)");
            Want(Mathf.IsEqualApprox(SailSpeedOn(0.5f, true, 0.0), 0.5f) && Mathf.IsEqualApprox(SailSpeedOn(0.5f, false, 0.0), 0.5f), "the sails change while paused");
            List<RoofPiece> mill = RoofDrawing(RoofOf(JobKind.Miller), Vector2.Zero, 1.6f, 1.6f, 0, Colors.White, 1f, null, Vector2.Zero, 1, false);
            List<RoofRun> millRuns = RoofRuns(mill, 40f);
            int turning = millRuns.Count(r => r.Turns), sailPieces = mill.Count(TurnsWithTheSails);
            int turnAt = millRuns.FindIndex(r => r.Turns);
            Want(turning == 1 && sailPieces == 16 && millRuns[turnAt].Lines?.Count == 16 && turnAt > 0 && turnAt < millRuns.Count - 1,
                $"a mill's sails: {turning} turning runs, {sailPieces} sail pieces, the run at {turnAt} of {millRuns.Count} (want 1, 16 — four sails' cloth, lattice, edge and spar — all in it, under the body)");
            Want(RoofRuns(RoofDrawing(RoofOf(BuildingKind.Cottage), Vector2.Zero, 1f, 1f, 0, Colors.White, 1f, null, Vector2.Zero, 1, false), 40f).All(r => !r.Turns),
                "a cottage has something that turns");
            Vector2 hub = HubOf(mill);
            Transform2D quarter = SailsTurned(new Vector2(3f, -2f), Mathf.Pi / 2f);
            Want(hub.IsEqualApprox(Vector2.Zero) && (quarter * new Vector2(3f, -2f)).IsEqualApprox(new Vector2(3f, -2f))
                && (quarter * new Vector2(4f, -2f)).IsEqualApprox(new Vector2(3f, -1f)),
                $"the sails do not turn about the hub (hub at {hub}; a quarter turn takes (4, -2) to {quarter * new Vector2(4f, -2f)}, want (3, -1))");

            // ⭐ The smoke darker, with the watercolour in it (A145): darker than A139's wash, four paintings each its own, a
            // ragged edge, grain in the wash.
            double darkest = 0;
            var paintings = new List<byte[]>();
            for (int v = 0; v < PuffPaintings; v++)
            {
                byte[] puff = PaintThePuff(v);
                paintings.Add(puff);
                double tone = 0, weight = 0;
                for (int px = 0; px < puff.Length; px += 4)
                {
                    double a = puff[px + 3] / 255.0;
                    tone += a * ((0.2126 * puff[px]) + (0.7152 * puff[px + 1]) + (0.0722 * puff[px + 2])) / 255.0;
                    weight += a;
                }

                darkest = Math.Max(darkest, tone / weight);
            }

            Color a139 = new("#b0a89e");
            double before = (0.2126 * a139.R) + (0.7152 * a139.G) + (0.0722 * a139.B);
            Want(darkest < before - 0.1, $"the smoke's wash at {darkest:F3} is not darker than A139's {before:F3} by 0.1");
            Want(paintings.Select(p => Convert.ToBase64String(p)).Distinct().Count() == PuffPaintings, "two puff paintings are the same");

            byte[] first = paintings[0];
            int n = PuffPixels;
            for (int v = 0; v < PuffPaintings; v++)
            {
                var reach = Enumerable.Range(0, 64).Select(k => PuffEdgeAt(v, k * Mathf.Tau / 64f)).ToList();
                float ragged = (reach.Max() - reach.Min()) / reach.Average();
                Want(ragged > 0.12f, $"puff {v}'s edge is a circle (its reach varies {ragged:P0}; want over 12 %)");
            }

            // Granulation: the pigment changes from one pixel to the next in the middle — the bloom and the tint change slowly.
            double steps = 0;
            int counted = 0;
            for (int y = (n / 2) - 12; y < (n / 2) + 12; y++)
            {
                for (int x = (n / 2) - 12; x < (n / 2) + 12; x++)
                {
                    steps += Math.Abs(first[(((y * n) + x) * 4) + 3] - first[(((y * n) + x + 1) * 4) + 3]);
                    counted++;
                }
            }

            double grain = steps / counted;
            Want(grain > 3, $"the wash has no grain (its pigment steps {grain:F1} of 255 a pixel in the middle; want over 3)");

            // A zoom lays a run's pen lines again — a mill's sails must still turn after it.
            RoofRun sails = millRuns[turnAt];
            Want(sails.Lines is { } sailLines && RelaidForTheZoom(sails, sailLines, 13f).Turns, "a zoom stops the sails turning");

            return faults.Count == 0
                ? "[widths] indoors and smoke: ✅ resting at home, warming by a fire, the smith and the batch trades at work indoors once inside a door; resting in the open and fishers seen; seen walking up to the door and gone on reaching it (within 0.15 tiles); errands into a store in at its door, on the road or at a pile seen (A141); taking paper inside the library, seen walking up to it (A246); a west door's wall at (2.4, -8); a home smokes in winter, lived in, with firewood, and not otherwise; smoke comes and goes over a second, holds while paused; a puff rises and swells from nothing; two chimneys out of step; flues: smithy 1, school 1, manor 2, forager's hut 0; the sails gather way and come to rest, held while paused, a run of their own under the body turned about the hub, nothing turns on a cottage (A145); the smoke darker than A139's, four paintings, a ragged edge, grain in the wash (A145)"
                : $"[widths] indoors and smoke: ⛔ {string.Join("; ", faults)}";
        }
        catch (Exception e)
        {
            return $"[widths] indoors and smoke: ⛔ threw {e.GetType().Name}: {e.Message}";
        }
    }
}
