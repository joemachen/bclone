using System.Collections.Generic;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <summary>How much explanation the map draws on top of the village.</summary>
/// <remarks>
/// One control for both the home-to-work routes and the catchment rings, because they
/// answer the same question — <em>why is that person walking over there?</em> — and
/// splitting them into two toggles would mean two controls for one thought.
/// </remarks>
public enum MapDetail
{
    /// <summary>Just the village. For watching rather than auditing.</summary>
    Off = 0,

    /// <summary>The selected villager's route and their workplace's catchment.</summary>
    Selected = 1,

    /// <summary>Everybody's route and every catchment. Busy, and meant to be.</summary>
    All = 2,
}

/// <summary>
/// Draws the village: the valley, the homes, the places people work, and the people
/// walking between them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Villagers are interpolated between ticks.</b> The sim moves them a whole tile at
/// a time, once per tick, and at one tick per second that would be a slideshow. So
/// this keeps each villager's previous tile — <em>view-side only</em>, never in sim
/// state — and lerps toward the current one using
/// <see cref="FixedTimestepDriver.Alpha"/>.
/// </para>
/// <para>
/// <b>People who are standing on the same tile are fanned apart.</b> Four adults
/// resting at one house are four people, and drawing them at one point makes them look
/// like one. This is the whole phase's Success Test in miniature — "watching twelve
/// villagers is still legible" is unanswerable if twelve villagers render as three
/// dots. The offset is view-only; sim positions are untouched.
/// </para>
/// <para>
/// Reads sim state, never writes it (DESIGN.md §3).
/// </para>
/// </remarks>
public partial class VillageMap : Control
{
    private static readonly Color Ground = new("#2b3332");
    private static readonly Color Beyond = new("#202625");
    private static readonly Color ValleyEdge = new("#485453");
    private static readonly Color GridLine = new("#343d3d");
    private static readonly Color HomeColour = new("#b98a52");
    private static readonly Color GranaryColour = new("#d8c56a");
    private static readonly Color ShedColour = new("#8a7a63");
    private static readonly Color BerryColour = new("#5aa04a");
    private static readonly Color TreeColour = new("#2f6b3a");
    private static readonly Color AdultColour = new("#e8e2d4");
    private static readonly Color ChildColour = new("#8fc7e8");
    private static readonly Color ElderColour = new("#d9a05b");
    private static readonly Color SelectedRing = new("#f2c14e");

    /// <summary>Closest the camera will get, in pixels per tile.</summary>
    private const float MaxPixelsPerTile = 48f;

    /// <summary>
    /// How much of the valley's <b>width</b> is on screen when zoomed fully out.
    /// </summary>
    /// <remarks>
    /// Joe's requirement was "zooming out full should let you see most of the full
    /// map, but not all of it". The map panel is wide and short — roughly 4.6:1 —
    /// against a 1.5:1 valley, so "most but not all" is only meaningful along the
    /// width; vertically you pan. That is the practical reading of the requirement
    /// given the shape of the panel, not a fudge.
    /// </remarks>
    private const float ZoomedOutShowsThisMuchOfTheWidth = 0.8f;

    /// <summary>Tiles per second the camera pans, measured on screen rather than in
    /// the world — so it feels the same however far you are zoomed in.</summary>
    private const float PanPixelsPerSecond = 520f;

    private const float ZoomStep = 1.12f;

    /// <summary>How far apart people on the same tile are drawn, in tiles.</summary>
    private const float FanRadiusTiles = 0.30f;

    private readonly Dictionary<int, Vector2> _previousTiles = new();

    /// <summary>Reused each frame so a busy village does not allocate per redraw.</summary>
    private readonly Dictionary<GridPos, List<int>> _byTile = new();

    private SimWorld? _world;
    private double _alpha;
    private int _selectedVillagerId;
    private MapDetail _detail = MapDetail.Selected;

    private Vector2 _centreTile;

    /// <summary>Never zero, so the very first frame — before layout has given the
    /// panel a size — cannot divide by it.</summary>
    private float _pixelsPerTile = 16f;

    private bool _framed;

    public override void _Ready()
    {
        // Wheel events only reach _GuiInput when the cursor is actually over the map,
        // which is what stops the map zooming while you are scrolling the village log.
        MouseFilter = MouseFilterEnum.Stop;
    }

    /// <summary>Hand the map the state to draw. Called every frame by <see cref="Main"/>.</summary>
    public void Present(SimWorld world, double alpha, int selectedVillagerId, MapDetail detail)
    {
        _world = world;
        _alpha = alpha;
        _selectedVillagerId = selectedVillagerId;
        _detail = detail;

        if (!_framed && Size.X > 0f)
        {
            CentreOnTheVillage();
        }

        QueueRedraw();
    }

    /// <summary>
    /// Frame the settlement: centred on where people live, zoomed so their homes and
    /// the work around them fill the panel.
    /// </summary>
    /// <remarks>
    /// The map used to do this every frame, fitting every workplace on screen. Once
    /// there were forage sites seven tiles out and a settlement three tiles across,
    /// that meant the village was a permanent smudge in the middle of an empty panel.
    /// Framing is now something that happens when you ask for it.
    /// </remarks>
    public void CentreOnTheVillage()
    {
        if (_world is null || Size.X <= 0f || Size.Y <= 0f)
        {
            return;
        }

        // Frame the HOMES, not every workplace. Fitting the workplaces is what the map
        // used to do, and with forage sites seven tiles out it meant the settlement
        // was a permanent smudge in an empty panel. Homes are where the people are.
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        int homes = 0;

        for (int i = 0; i < _world.Households.Count; i++)
        {
            Household household = _world.Households[i];
            if (_world.LivingMembersOf(household) == 0)
            {
                continue;
            }

            var home = new Vector2(household.HomePosition.X, household.HomePosition.Y);
            min = min.Min(home);
            max = max.Max(home);
            homes++;
        }

        if (homes == 0)
        {
            min = Vector2.Zero;
            max = Vector2.Zero;
        }

        _centreTile = (min + max) / 2f;

        // Margin enough that the nearest work is on screen too, and that a village of
        // two houses is not framed so tightly it fills the window.
        const float marginTiles = 5f;
        Vector2 span = (max - min) + (Vector2.One * marginTiles * 2f);

        // Fit BOTH axes — whichever is tighter wins. The panel is much wider than it
        // is tall, so for anything but the founding village that will be the height.
        SetZoom(Mathf.Min(Size.X / span.X, Size.Y / span.Y));
        ClampCentre();
        _framed = true;
    }

    // ---------------------------------------------------------------
    //  Camera
    // ---------------------------------------------------------------

    public override void _Process(double delta)
    {
        if (_world is null)
        {
            return;
        }

        // Polled rather than event-driven: held keys have to pan smoothly, and key
        // events only fire on press and repeat. Polling globally is safe here because
        // the UI has no text fields for WASD to be typed into.
        var direction = Vector2.Zero;
        if (Input.IsPhysicalKeyPressed(Key.W)) { direction.Y -= 1f; }
        if (Input.IsPhysicalKeyPressed(Key.S)) { direction.Y += 1f; }
        if (Input.IsPhysicalKeyPressed(Key.A)) { direction.X -= 1f; }
        if (Input.IsPhysicalKeyPressed(Key.D)) { direction.X += 1f; }

        if (direction == Vector2.Zero)
        {
            return;
        }

        // Divided by the zoom, so a keypress moves the same distance across the SCREEN
        // whether you are looking at one house or the whole valley.
        float tiles = PanPixelsPerSecond * (float)delta / _pixelsPerTile;
        _centreTile += direction.Normalized() * tiles;
        ClampCentre();
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (_world is null || @event is not InputEventMouseButton { Pressed: true } click)
        {
            return;
        }

        // Middle mouse is deliberately unbound. Rotation was asked for and deferred:
        // the view is flat top-down, so rotating it would spin the map like paper on a
        // table rather than orbit it, and that is a different feature to build once
        // the view has depth.
        if (click.ButtonIndex is not (MouseButton.WheelUp or MouseButton.WheelDown))
        {
            return;
        }

        // Zoom about the cursor: the tile under the pointer stays under the pointer,
        // so you zoom toward what you are looking at rather than toward the middle.
        Vector2 anchorTile = ToTile(click.Position);
        SetZoom(_pixelsPerTile * (click.ButtonIndex == MouseButton.WheelUp ? ZoomStep : 1f / ZoomStep));
        _centreTile += anchorTile - ToTile(click.Position);

        ClampCentre();
        QueueRedraw();
        AcceptEvent();
    }

    private void SetZoom(float pixelsPerTile)
    {
        _pixelsPerTile = Mathf.Clamp(pixelsPerTile, MinPixelsPerTile(), MaxPixelsPerTile);
    }

    /// <summary>The furthest out the camera will go.</summary>
    private float MinPixelsPerTile()
    {
        if (_world is null || Size.X <= 0f)
        {
            return 1f;
        }

        return Size.X / (_world.Config.MapWidth * ZoomedOutShowsThisMuchOfTheWidth);
    }

    /// <summary>
    /// Keep the valley filling the view.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Clamps the <em>visible rectangle</em> to the valley rather than the camera
    /// centre. Clamping the centre is one line shorter and lets you push an edge to
    /// the middle of the panel, which at full zoom-out leaves half the screen blank
    /// and no clue which way the village is. This way you can reach every edge and
    /// never sail past one.
    /// </para>
    /// <para>
    /// When the view is wider than the valley on an axis — which it is horizontally
    /// at some zoom levels, the panel being much wider than it is tall — the valley is
    /// simply centred on that axis, because there is nothing to pan toward.
    /// </para>
    /// </remarks>
    private void ClampCentre()
    {
        if (_world is null || _pixelsPerTile <= 0f)
        {
            return;
        }

        SimConfig config = _world.Config;

        // A little slack so the valley's edge is visible as an edge rather than only
        // ever sitting exactly at the frame.
        const float edgeSlackTiles = 2f;

        Vector2 halfView = Size / 2f / _pixelsPerTile;
        _centreTile = new Vector2(
            ClampAxis(_centreTile.X, config.MapMinX, config.MapMaxX, halfView.X - edgeSlackTiles),
            ClampAxis(_centreTile.Y, config.MapMinY, config.MapMaxY, halfView.Y - edgeSlackTiles));
    }

    private static float ClampAxis(float centre, float min, float max, float halfView)
    {
        // View covers more than the valley does: nothing to pan toward, so centre it.
        if (halfView * 2f >= max - min)
        {
            return (min + max) / 2f;
        }

        return Mathf.Clamp(centre, min + halfView, max - halfView);
    }

    private Vector2 ToScreen(Vector2 tile) => ((tile - _centreTile) * _pixelsPerTile) + (Size / 2f);

    private Vector2 ToScreen(GridPos tile) => ToScreen(new Vector2(tile.X, tile.Y));

    private Vector2 ToTile(Vector2 screen) => ((screen - (Size / 2f)) / _pixelsPerTile) + _centreTile;

    // ---------------------------------------------------------------
    //  Drawing
    // ---------------------------------------------------------------

    public override void _Draw()
    {
        if (_world is null)
        {
            return;
        }

        if (!_framed)
        {
            CentreOnTheVillage();
        }

        // Everything outside the valley reads as off-the-map rather than as more of
        // the same ground, so an empty corner is legibly an edge and not a bug.
        DrawRect(new Rect2(Vector2.Zero, Size), Beyond);
        DrawValley();

        // Routes under everything, then workplaces, then homes, then people on top —
        // the things that move must never be hidden behind the things that do not.
        DrawRoutes();
        DrawWorkplaces();
        DrawStores();
        DrawHomes();
        DrawVillagers();
    }

    /// <summary>
    /// The granary and the materials shed.
    /// </summary>
    /// <remarks>
    /// Drawn as squares like homes, because they are the same sort of thing — a place
    /// that holds goods — and a different shape would imply a distinction that is not
    /// there. Distinguished by colour, and outlined so a store never reads as just
    /// another house: "why is nobody fetching food?" has to be answerable by looking.
    /// </remarks>
    private void DrawStores()
    {
        SimWorld world = _world!;

        for (int i = 0; i < world.StoreBuildings.Count; i++)
        {
            StoreBuilding building = world.StoreBuildings[i];

            Vector2 centre = ToScreen(building.Position);
            float size = Mathf.Max(8f, _pixelsPerTile * 0.8f);
            var rect = new Rect2(centre - (Vector2.One * size / 2f), Vector2.One * size);

            Color colour = building.Kind == StoreKind.Granary ? GranaryColour : ShedColour;
            DrawRect(rect, colour with { A = 0.85f });
            DrawRect(rect, colour, filled: false, width: 2f);
        }
    }

    private void DrawValley()
    {
        SimConfig config = _world!.Config;

        // Half a tile out on each side, so the edge sits at the outside of the last
        // tile rather than through the middle of it.
        Vector2 topLeft = ToScreen(new Vector2(config.MapMinX - 0.5f, config.MapMinY - 0.5f));
        Vector2 bottomRight = ToScreen(new Vector2(config.MapMaxX + 0.5f, config.MapMaxY + 0.5f));
        var valley = new Rect2(topLeft, bottomRight - topLeft);

        DrawRect(valley, Ground);
        DrawRect(valley, ValleyEdge, filled: false, width: 2f);

        // Only worth drawing while tiles are big enough to read.
        if (_pixelsPerTile < 6f)
        {
            return;
        }

        // Clipped to what is actually on screen: at full zoom-in a loop over 120
        // columns is mostly wasted, and at full zoom-out it is 120 lines a pixel apart.
        Vector2 first = ToTile(Vector2.Zero);
        Vector2 last = ToTile(Size);

        int minX = Mathf.Max(config.MapMinX, Mathf.FloorToInt(first.X));
        int maxX = Mathf.Min(config.MapMaxX + 1, Mathf.CeilToInt(last.X));
        int minY = Mathf.Max(config.MapMinY, Mathf.FloorToInt(first.Y));
        int maxY = Mathf.Min(config.MapMaxY + 1, Mathf.CeilToInt(last.Y));

        for (int x = minX; x <= maxX; x++)
        {
            DrawLine(
                ToScreen(new Vector2(x - 0.5f, minY - 0.5f)),
                ToScreen(new Vector2(x - 0.5f, maxY - 0.5f)),
                GridLine,
                1f);
        }

        for (int y = minY; y <= maxY; y++)
        {
            DrawLine(
                ToScreen(new Vector2(minX - 0.5f, y - 0.5f)),
                ToScreen(new Vector2(maxX - 0.5f, y - 0.5f)),
                GridLine,
                1f);
        }
    }

    /// <summary>
    /// A line from where somebody lives to where they work.
    /// </summary>
    /// <remarks>
    /// The visual counterpart of <see cref="Villager.JobReason"/>. The sentence says
    /// <em>"took work at the western thicket — 6 tiles from home; the tree stand was
    /// nearer at 2, but the village has all the woodcutters it needs"</em>; the line
    /// says the same thing at a glance, and makes a bad allocation visible as a route
    /// crossing straight past a nearer site.
    /// </remarks>
    private void DrawRoutes()
    {
        if (_detail == MapDetail.Off)
        {
            return;
        }

        SimWorld world = _world!;

        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (!villager.Alive || !villager.HasJob || !InScope(villager.Id))
            {
                continue;
            }

            Workplace? workplace = world.FindWorkplace(villager.WorkplaceId);
            if (workplace is null)
            {
                continue;
            }

            Color colour = ColourOf(workplace.Kind);
            bool selected = villager.Id == _selectedVillagerId;

            DrawLine(
                ToScreen(world.HomeOf(villager)),
                ToScreen(workplace.Position),
                colour with { A = selected ? 0.75f : 0.3f },
                selected ? 2f : 1f);
        }
    }

    private void DrawWorkplaces()
    {
        SimWorld world = _world!;
        int selectedWorkplace = world.FindVillager(_selectedVillagerId)?.WorkplaceId ?? 0;

        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            Workplace workplace = world.Workplaces[i];
            Vector2 centre = ToScreen(workplace.Position);
            Color colour = ColourOf(workplace.Kind);

            // Catchment as a faint ring: the "does not walk across the map" rule made
            // visible rather than merely enforced. Scoped to the detail control,
            // because seven overlapping rings drawn at all times was most of the
            // clutter and none of the meaning.
            bool ringWanted = _detail == MapDetail.All
                || (_detail == MapDetail.Selected && workplace.Id == selectedWorkplace);

            if (ringWanted)
            {
                float radius = workplace.CatchmentRadius / (float)TravelCostField.BaseTileCost * _pixelsPerTile;
                if (radius <= Mathf.Max(Size.X, Size.Y) * 2f)
                {
                    DrawArc(centre, radius, 0f, Mathf.Tau, 64, colour with { A = 0.22f }, 1f);
                }
            }

            DrawCircle(centre, Mathf.Max(4f, _pixelsPerTile * 0.4f), colour);
        }
    }

    private void DrawHomes()
    {
        SimWorld world = _world!;

        for (int i = 0; i < world.Households.Count; i++)
        {
            Household household = world.Households[i];
            bool occupied = world.LivingMembersOf(household) > 0;

            Vector2 centre = ToScreen(household.HomePosition);
            float size = Mathf.Max(6f, _pixelsPerTile * 0.62f);
            var rect = new Rect2(centre - (Vector2.One * size / 2f), Vector2.One * size);

            // A house whose family has died still stands, and reads as abandoned.
            DrawRect(rect, occupied ? HomeColour : HomeColour with { A = 0.25f });
        }
    }

    private void DrawVillagers()
    {
        SimWorld world = _world!;
        var stillAlive = new HashSet<int>();

        GroupByTile(world);

        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (!villager.Alive)
            {
                continue;
            }

            stillAlive.Add(villager.Id);

            var current = new Vector2(villager.Position.X, villager.Position.Y);
            Vector2 previous = _previousTiles.TryGetValue(villager.Id, out Vector2 known) ? known : current;

            // Lerp from where they were to where they are. If they moved more than a
            // tile — being born, or moving house — snap instead, or they would glide
            // across the map.
            Vector2 drawTile = previous.DistanceSquaredTo(current) > 2f
                ? current
                : previous.Lerp(current, (float)_alpha);

            if (current != previous && _alpha >= 0.999)
            {
                _previousTiles[villager.Id] = current;
            }
            else if (!_previousTiles.ContainsKey(villager.Id))
            {
                _previousTiles[villager.Id] = current;
            }

            Vector2 centre = ToScreen(drawTile + FanOffset(villager));
            float radius = Mathf.Max(3f, _pixelsPerTile * 0.2f);

            Color colour = villager.LifeStage switch
            {
                LifeStage.Child => ChildColour,
                LifeStage.Elder => ElderColour,
                _ => AdultColour,
            };

            DrawCircle(centre, radius, colour);

            if (villager.Id == _selectedVillagerId)
            {
                DrawArc(centre, radius + 4f, 0f, Mathf.Tau, 24, SelectedRing, 2f);
            }
        }

        PruneTheDead(stillAlive);
    }

    /// <summary>Everyone alive, bucketed by the tile they are standing on.</summary>
    private void GroupByTile(SimWorld world)
    {
        foreach (KeyValuePair<GridPos, List<int>> bucket in _byTile)
        {
            bucket.Value.Clear();
        }

        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (!villager.Alive)
            {
                continue;
            }

            if (!_byTile.TryGetValue(villager.Position, out List<int>? here))
            {
                here = new List<int>();
                _byTile[villager.Position] = here;
            }

            // Villagers are walked in id order, so each bucket comes out sorted by id
            // without needing to be sorted.
            here.Add(villager.Id);
        }
    }

    /// <summary>
    /// Where to draw somebody standing on a crowded tile.
    /// </summary>
    /// <remarks>
    /// Four adults resting at one house are four people, and drawing them at one point
    /// makes them look like one — which is exactly the question the phase's Success
    /// Test asks. So a crowded tile spreads its occupants around a small ring.
    /// <para>
    /// The offset depends only on <em>rank within the tile</em> and <em>how many are
    /// on it</em>, and rank comes from villager id order, so the arrangement is stable
    /// from frame to frame and nobody jitters. It is view-only: sim positions never
    /// move (DESIGN.md §3).
    /// </para>
    /// </remarks>
    private Vector2 FanOffset(Villager villager)
    {
        if (!_byTile.TryGetValue(villager.Position, out List<int>? here) || here.Count <= 1)
        {
            return Vector2.Zero;
        }

        int rank = here.IndexOf(villager.Id);
        if (rank < 0)
        {
            return Vector2.Zero;
        }

        float angle = Mathf.Tau * rank / here.Count;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * FanRadiusTiles;
    }

    private bool InScope(int villagerId) =>
        _detail == MapDetail.All || (_detail == MapDetail.Selected && villagerId == _selectedVillagerId);

    private static Color ColourOf(JobKind kind) => kind == JobKind.Forager ? BerryColour : TreeColour;

    /// <summary>Forget interpolation state for the dead, so ids can never be confused
    /// and the dictionary does not grow for the whole run.</summary>
    private void PruneTheDead(HashSet<int> stillAlive)
    {
        if (_previousTiles.Count <= stillAlive.Count)
        {
            return;
        }

        var gone = new List<int>();
        foreach (KeyValuePair<int, Vector2> entry in _previousTiles)
        {
            if (!stillAlive.Contains(entry.Key))
            {
                gone.Add(entry.Key);
            }
        }

        foreach (int id in gone)
        {
            _previousTiles.Remove(id);
        }
    }
}
