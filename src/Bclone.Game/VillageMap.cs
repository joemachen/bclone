using System.Collections.Generic;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <summary>How much explanation the map draws on top of the village.</summary>
/// <remarks>
/// One control for both the home-to-work routes and the gathering rings, because they answer
/// the same question — <em>why is that person walking over there?</em> — and splitting them
/// into two toggles would mean two controls for one thought.
/// <para>
/// It used to draw <em>catchment</em> rings, which were the fence a workplace enforced on how
/// far anybody could come from. That fence is deleted (`forests-and-gathering.md §3`), and a
/// ring here is now a gatherer's hut's <b>gathering</b> radius — the ground its yield is
/// computed from, which is a fact about the building rather than a rule about people.
/// </para>
/// </remarks>
public enum MapDetail
{
    /// <summary>Just the village. For watching rather than auditing.</summary>
    Off = 0,

    /// <summary>The selected villager's route, and their workplace's ring if it has one.</summary>
    Selected = 1,

    /// <summary>Everybody's route and every ring. Busy, and meant to be.</summary>
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

    /// <summary>The generated river (D18).</summary>
    /// <remarks>
    /// Deliberately the most distinct colour on the map. Water is about to become the
    /// one thing a villager cannot walk over (D40), so it needs to read as an obstacle
    /// at a glance and long before a bridge exists to argue with it.
    /// </remarks>
    private static readonly Color WaterColour = new("#2f5f7a");

    /// <summary>Generated woodland. Quieter than the tree stand that stands in it.</summary>
    private static readonly Color ForestColour = new("#2a3d2c");

    /// <summary>A stone seam — pale and dry against the grass, so it reads as bare ground.</summary>
    private static readonly Color RockColour = new("#6b6459");

    /// <summary>An iron seam. Rusted, and warmer than the stone so the two never blur.</summary>
    /// <remarks>
    /// <b>Two seams a player must tell apart at a glance</b> (D67: you go after a seam
    /// because you can see it), so they differ in hue rather than only in lightness —
    /// which is also the one difference that survives being colour-blind.
    /// </remarks>
    private static readonly Color IronColour = new("#7a4a33");

    /// <summary>The woodcutter's hut — a workplace, not a stand of trees.</summary>
    private static readonly Color HutColour = new("#9a6b3f");

    /// <summary>The market (D14), which is both a workplace and a store.</summary>
    private static readonly Color MarketColour = new("#c98f4a");

    /// <summary>The ring round a store with no room left (D140).</summary>
    /// <remarks>
    /// Warm amber rather than red. A full store is not a disaster — it is usually a village
    /// doing well at something — and §1.1 wants the player to look, not to panic.
    /// </remarks>
    private static readonly Color FullStoreColour = new("#e8a13c");

    /// <summary>A building marked out but not yet raised (D43).</summary>
    private static readonly Color SiteColour = new("#8f9aa8");

    /// <summary>Land the player has painted for housing (D42). Faint on purpose.</summary>
    private static readonly Color ResidentialColour = new("#b98a52", 0.14f);

    /// <summary>Ground the village has been told to clear (D87).</summary>
    /// <remarks>
    /// <b>Warmer and stronger than the residential wash</b>, because the two overlap on the
    /// map and mean opposite things — one says *you may build here*, the other says *this is
    /// coming down*. Still translucent: it is an instruction about the ground, not a new kind
    /// of ground, and a marked wood must still read as a wood.
    /// </remarks>
    private static readonly Color HarvestColour = new("#d8892f", 0.26f);

    /// <summary>Ground a building has been given to work (D86).</summary>
    /// <remarks>
    /// <b>Cool, where the other two zone washes are warm</b>, because it means a different
    /// kind of thing and the three overlap: residential says <em>the village may build
    /// here</em>, harvest says <em>this is coming down</em>, and this says <em>a named
    /// building works this</em>. Two warm washes and a cool one is a distinction that
    /// survives being seen out of the corner of your eye, and being colour-blind.
    /// </remarks>
    private static readonly Color WorkGroundColour = new("#4a9ba8", 0.16f);

    /// <summary>The selected building's own ground, brighter than everybody else's.</summary>
    private static readonly Color WorkGroundMine = new("#5fc8d8", 0.30f);

    private static readonly Color GhostFine = new("#7fd48a");
    private static readonly Color GhostWarned = new("#e0b755");
    private static readonly Color GhostRefused = new("#d4685f");
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
    private GridPos? _selectedTile;
    private MapDetail _detail = MapDetail.Selected;

    /// <summary>
    /// Raised when the player clicks the map while not placing anything.
    /// </summary>
    /// <remarks>
    /// <b>A tile, not a building id.</b> The three things that can stand on a tile —
    /// a store, a workplace and a home — live in three lists with three independent id
    /// spaces, and the market is deliberately <em>both</em> a store and a workplace at
    /// one position (D36's known seam). Selecting a tile and asking the sim what is
    /// there describes the market correctly without having to pick which of its two
    /// halves the player meant.
    /// </remarks>
    public event System.Action<GridPos>? BuildingClicked;

    /// <summary>
    /// The player clicked on a person rather than on the ground (Joe, 2026-08-09).
    /// </summary>
    /// <remarks>
    /// <b>A villager id, and here it can be one</b>, unlike the tile above: a person is a
    /// single thing in a single list, and two of them standing on one tile are still two
    /// people. The tile-not-id argument was about buildings sharing a position, which people
    /// do all day and buildings do only at D36's seam.
    /// </remarks>
    public event System.Action<int>? VillagerClicked;

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
    public void Present(
        SimWorld world, double alpha, int selectedVillagerId, GridPos? selectedTile, MapDetail detail)
    {
        _world = world;
        _alpha = alpha;
        _selectedVillagerId = selectedVillagerId;
        _selectedTile = selectedTile;
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

            // A family with no house yet (D70) has nothing to frame — they are standing at
            // the cart, which the founding site already accounts for.
            if (household.HomePosition is not GridPos standing)
            {
                continue;
            }

            var home = new Vector2(standing.X, standing.Y);
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

    // ---------------------------------------------------------------
    //  Build mode (D43)
    // ---------------------------------------------------------------

    /// <summary>What the player is about to put down, or null when just looking.</summary>
    private BuildingKind? _building;

    /// <summary>True when the next click pulls a building down instead of raising one.</summary>
    private bool _demolishing;

    /// <summary>Painting homes: 0 not, 1 painting, -1 erasing.</summary>
    private int _brush;

    /// <summary>How wide the brush is, in tiles either side.</summary>
    /// <remarks>
    /// A brush rather than a single tile, because a residential area is a
    /// <em>neighbourhood</em> — asking the player to paint it a tile at a time would be
    /// exactly the click-farm zoning exists to avoid (D42).
    /// </remarks>
    private const int BrushRadius = 2;

    /// <summary>
    /// Which harvest mode the brush is set to, or null when it is painting homes.
    /// </summary>
    /// <remarks>
    /// <b>Modes of one tool</b> (D92, Joe): the mode decides which tiles take the paint and
    /// is then forgotten. So the view holds the setting and the sim holds one layer — there
    /// is no per-material state anywhere, which is what made the sim side free.
    /// </remarks>
    private HarvestBrush? _harvestMode;

    /// <summary>
    /// The workplace whose ground is being painted, or 0 when the brush is not doing that.
    /// </summary>
    /// <remarks>
    /// <b>The third brush, and the only one that belongs to a BUILDING rather than to the
    /// village</b> (D86). Residential land is the village's and harvest paint is nobody's;
    /// work ground has an owner, so the brush has to carry which one — and it is an id rather
    /// than a reference so that a hut demolished mid-stroke stops the painting instead of
    /// writing to a workplace that no longer exists.
    /// </remarks>
    private int _groundFor;

    /// <summary>Start or stop painting where the village may live (D42).</summary>
    public void BeginPainting(int direction)
    {
        _brush = direction;
        _harvestMode = null;
        _groundFor = 0;
        _building = null;
        _demolishing = false;
        Announce();
        QueueRedraw();
    }

    /// <summary>Start or stop marking what the village means to take (D87, D92).</summary>
    public void BeginHarvesting(HarvestBrush mode, int direction)
    {
        _brush = direction;
        _harvestMode = mode;
        _groundFor = 0;
        _building = null;
        _demolishing = false;
        Announce();
        QueueRedraw();
    }

    /// <summary>Start or stop giving ground to one building (D86).</summary>
    public void BeginPaintingGround(int workplaceId, int direction)
    {
        _brush = direction;
        _harvestMode = null;
        _groundFor = workplaceId;
        _building = null;
        _demolishing = false;
        Announce();
        QueueRedraw();
    }

    /// <summary>The tile under the cursor, and what the sim says about building on it.</summary>
    private GridPos _hovered;
    private PlacementVerdict _verdict = PlacementVerdict.Fine;

    /// <summary>Raised whenever the ghost's verdict changes, so the shell can say it.</summary>
    public event System.Action<string>? PlacementMessageChanged;

    /// <summary>Start marking out a building. Null stops.</summary>
    public void BeginBuilding(BuildingKind? kind)
    {
        _building = kind;
        _demolishing = false;
        _brush = 0;
        Announce();
        QueueRedraw();
    }

    /// <summary>Next click pulls a building down.</summary>
    public void BeginDemolishing()
    {
        _building = null;
        _demolishing = true;
        _brush = 0;
        Announce();
        QueueRedraw();
    }

    /// <summary>Whether the player is in the middle of placing, demolishing or painting.</summary>
    public bool IsPlacing => _building is not null || _demolishing || _brush != 0;

    public override void _GuiInput(InputEvent @event)
    {
        if (_world is null)
        {
            return;
        }

        // The ghost follows the cursor, and the verdict is recomputed as it moves.
        // CanBuildAt is pure, so asking it every frame costs nothing and changes
        // nothing — which is what lets the answer be shown BEFORE anybody commits.
        if (@event is InputEventMouseMotion motion && IsPlacing)
        {
            Vector2 tile = ToTile(motion.Position);
            var over = new GridPos(Mathf.RoundToInt(tile.X), Mathf.RoundToInt(tile.Y));
            if (over != _hovered)
            {
                _hovered = over;
                if (_building is not null)
                {
                    _verdict = _world.CanBuildAt(_building.Value, _hovered);
                }

                // Drag to paint. A neighbourhood is a shape you draw, not a sequence of
                // clicks — the brush exists so that deciding where people live costs one
                // gesture rather than forty (D42).
                if (_brush != 0 && motion.ButtonMask.HasFlag(MouseButtonMask.Left))
                {
                    PaintAround(_hovered);
                }

                Announce();
                QueueRedraw();
            }

            return;
        }

        if (@event is not InputEventMouseButton { Pressed: true } click)
        {
            return;
        }

        if (IsPlacing && click.ButtonIndex == MouseButton.Right)
        {
            BeginBuilding(null);
            _demolishing = false;
            AcceptEvent();
            return;
        }

        if (IsPlacing && click.ButtonIndex == MouseButton.Left)
        {
            PlaceOrPullDown(click.Position);
            AcceptEvent();
            return;
        }

        // Not placing anything, so a click is a question rather than an instruction:
        // "what is that?". The shell answers it in the same panel the villagers use,
        // because the player has one place they look to find out about a thing.
        if (click.ButtonIndex == MouseButton.Left)
        {
            // Somebody standing there is what you meant; the ground is the fallback.
            if (VillagerAt(click.Position) is Villager person)
            {
                VillagerClicked?.Invoke(person.Id);
                AcceptEvent();
                return;
            }

            Vector2 hit = ToTile(click.Position);
            BuildingClicked?.Invoke(new GridPos(Mathf.RoundToInt(hit.X), Mathf.RoundToInt(hit.Y)));
            AcceptEvent();
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

    /// <summary>Act on a click while in build or demolish mode.</summary>
    /// <remarks>
    /// <b>The game does not pause for this</b> (D43, Joe's call). The village carries on
    /// while you decide, because pausing would make placement a modal act — the world
    /// stopping to wait on you — and nothing here is urgent enough to stop the clock
    /// for. That is a claim about what kind of decision building is.
    /// </remarks>
    /// <summary>Paint or erase a brushful of residential land.</summary>
    private void PaintAround(GridPos centre)
    {
        string? warning = null;
        string? refused = null;

        for (int dy = -BrushRadius; dy <= BrushRadius; dy++)
        {
            for (int dx = -BrushRadius; dx <= BrushRadius; dx++)
            {
                if (Mathf.Abs(dx) + Mathf.Abs(dy) > BrushRadius)
                {
                    continue;
                }

                var tile = new GridPos(centre.X + dx, centre.Y + dy);

                // Ground given to one building (D86). Same stroke shape as the others — one
                // sentence for the drag, never one per tile — and it stops rather than
                // half-painting if the hut went away mid-stroke.
                if (_groundFor != 0)
                {
                    Workplace? owner = _world!.FindWorkplace(_groundFor);
                    if (owner is null)
                    {
                        _groundFor = 0;
                        continue;
                    }

                    if (_brush < 0)
                    {
                        _world.EraseWorkGround(owner, tile);
                        continue;
                    }

                    PlacementVerdict given = _world.PaintWorkGround(owner, tile);
                    if (!given.Allowed)
                    {
                        refused = given.Reason;
                    }
                    else if (given.HasWarning)
                    {
                        warning = given.Warning;
                    }

                    continue;
                }

                if (_harvestMode is not null)
                {
                    if (_brush < 0)
                    {
                        _world!.EraseHarvest(tile);
                        continue;
                    }

                    // Refusals are silent per tile and counted for the stroke: a drag
                    // across mixed ground is MEANT to skip what the mode does not take,
                    // and forty sentences would bury the one that matters (D42, D92).
                    PlacementVerdict marked = _world!.PaintHarvest(tile, _harvestMode.Value);
                    if (!marked.Allowed)
                    {
                        refused = marked.Reason;
                    }
                    else if (marked.HasWarning)
                    {
                        warning = marked.Warning;
                    }

                    continue;
                }

                if (_brush < 0)
                {
                    _world!.EraseResidential(tile);
                    continue;
                }

                PlacementVerdict verdict = _world!.PaintResidential(tile);
                if (verdict.HasWarning)
                {
                    warning = verdict.Warning;
                }
            }
        }

        // One warning for the stroke, not one per tile — which is the entire reason
        // zoning was a better answer than placing houses one at a time (D42).
        // A warning outranks a refusal: "you painted more than your hands can keep" is
        // something to act on, where "some of that was the wrong kind of ground" is the
        // brush doing exactly what the mode asked of it.
        if (warning is not null)
        {
            PlacementMessageChanged?.Invoke(warning);
        }
        else if (refused is not null)
        {
            PlacementMessageChanged?.Invoke(refused);
        }
    }

    private void PlaceOrPullDown(Vector2 at)
    {
        Vector2 tile = ToTile(at);
        var where = new GridPos(Mathf.RoundToInt(tile.X), Mathf.RoundToInt(tile.Y));

        if (_brush != 0)
        {
            PaintAround(where);
            QueueRedraw();
            return;
        }

        if (_demolishing)
        {
            // ⭐ SITES AND HUTS TOO, WHICH THIS COULD NOT TOUCH BEFORE (Joe: *"I can't
            // cancel/demolish a building that is under construction — demolish says nothing
            // there to pull down"*). It only ever searched the stores, so a construction site
            // and every hut in the game were permanent once marked. **A misplaced building
            // the player cannot take back is the opposite of the brush's whole promise.**
            //
            // Workplaces first, because that is where the thing the player is most likely to
            // be undoing lives: a site they have just marked in the wrong spot.
            foreach (Workplace workplace in _world!.Workplaces)
            {
                if (workplace.Position == where)
                {
                    string name = workplace.Construction?.Name ?? workplace.Name;
                    _world.Demolish(workplace);
                    PlacementMessageChanged?.Invoke($"{name} is gone.");
                    QueueRedraw();
                    return;
                }
            }

            foreach (StoreBuilding building in _world.StoreBuildings)
            {
                if (building.Position == where)
                {
                    _world.Demolish(building);
                    PlacementMessageChanged?.Invoke($"{building.Name} is coming down.");
                    QueueRedraw();
                    return;
                }
            }

            PlacementMessageChanged?.Invoke("Nothing there to pull down.");
            return;
        }

        PlacementVerdict verdict = _world!.Mark(_building!.Value, where);
        if (!verdict.Allowed)
        {
            // Stay in build mode: a refusal is information, not a dismissal, and
            // making the player reopen the menu to try one tile over would be a
            // punishment for exploring.
            PlacementMessageChanged?.Invoke(verdict.Reason);
            return;
        }

        PlacementMessageChanged?.Invoke(verdict.HasWarning
            ? $"Marked out. {verdict.Warning}"
            : "Marked out. The village will raise it when it can spare the hands.");

        QueueRedraw();
    }

    /// <summary>Tell the shell what the cursor is currently over.</summary>
    private void Announce()
    {
        // ⚠️ THE GROUND BRUSH FIRST, because it is a positive brush and the residential
        // wording below would otherwise claim it. Joe saw exactly that: pressing "Give ground"
        // announced *"drag to paint where the village may build homes"*, which is a sentence
        // about the wrong tool and made a working brush look broken.
        if (_groundFor != 0)
        {
            Workplace? owner = _world?.FindWorkplace(_groundFor);
            string whose = owner?.Name ?? "this building";

            PlacementMessageChanged?.Invoke(_brush < 0
                ? $"Drag to take ground back from {whose}. Right-click to stop."
                : $"Drag to give ground to {whose} — its people work what you paint. "
                    + "Right-click to stop.");
            return;
        }

        if (_brush > 0)
        {
            PlacementMessageChanged?.Invoke(
                "Drag to paint where the village may build homes. Right-click to stop.");
            return;
        }

        if (_brush < 0)
        {
            PlacementMessageChanged?.Invoke(
                "Drag to take land back. Houses already standing stay put. Right-click to stop.");
            return;
        }

        if (_demolishing)
        {
            PlacementMessageChanged?.Invoke("Click a building to pull it down. Right-click to stop.");
            return;
        }

        if (_building is null)
        {
            PlacementMessageChanged?.Invoke(string.Empty);
            return;
        }

        PlacementMessageChanged?.Invoke(_verdict switch
        {
            { Allowed: false } => _verdict.Reason,
            { HasWarning: true } => _verdict.Warning,
            _ => "Click to mark it out. Right-click to stop.",
        });
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

    /// <summary>
    /// The stretch of valley on screen right now, in tiles — what the minimap boxes.
    /// </summary>
    /// <remarks>
    /// Derived from the camera rather than stored beside it, so it cannot go stale: there is
    /// one centre and one zoom, and this is arithmetic on them.
    /// </remarks>
    public Rect2 VisibleTiles
    {
        get
        {
            if (_pixelsPerTile <= 0f)
            {
                return new Rect2();
            }

            Vector2 span = Size / _pixelsPerTile;
            return new Rect2(_centreTile - (span / 2f), span);
        }
    }

    /// <summary>Put the camera over a tile — the minimap's whole reason for being clickable.</summary>
    public void CentreOn(Vector2 tile)
    {
        _centreTile = tile;
        ClampCentre();
        QueueRedraw();
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

        // Over the buildings so it is not hidden by one, under the people so it never
        // hides them — the same rule the rest of this method follows.
        DrawSelectedTile();
        DrawVillagers();

        // The ghost last, over everything, because it is the thing being decided.
        DrawTheGhost();
    }

    /// <summary>
    /// The building about to be placed, under the cursor, coloured by what the sim says.
    /// </summary>
    /// <remarks>
    /// Three colours for three answers, and the middle one is the point (D43): green is
    /// fine, <b>amber is allowed but unwise</b>, red is impossible. The player may build
    /// on amber. The words alongside say why it is amber, because a colour on its own
    /// is the shrug this project keeps refusing.
    /// </remarks>
    private void DrawTheGhost()
    {
        if (_building is null)
        {
            return;
        }

        // WHERE THE AMBER STARTS, drawn only while placing.
        //
        // The warning fires past a distance the player cannot see, which makes it
        // undiscoverable — Joe placed several buildings and never met one. A ring at
        // exactly that radius turns a hidden constant into a line on the map, so
        // "people will spend their days walking to it" arrives as confirmation of
        // something already visible rather than as a surprise.
        //
        // Only while the build menu is open: it is a placement aid, not furniture.
        SimWorld world = _world!;
        GridPos village = world.FirstHomeOrFoundingSite();

        float comfortable = VillageEconomy.MaxHomeToVillageTiles(world.Config) * _pixelsPerTile;
        DrawArc(ToScreen(village), comfortable, 0f, Mathf.Tau, 96, GhostWarned with { A = 0.35f }, 1f);

        if (!world.Map.Contains(_hovered))
        {
            return;
        }

        Vector2 centre = ToScreen(_hovered);
        float size = Mathf.Max(10f, _pixelsPerTile * 0.9f);
        var rect = new Rect2(centre - (Vector2.One * size / 2f), Vector2.One * size);

        Color colour = _verdict switch
        {
            { Allowed: false } => GhostRefused,
            { HasWarning: true } => GhostWarned,
            _ => GhostFine,
        };

        DrawRect(rect, colour with { A = 0.35f });
        DrawRect(rect, colour, filled: false, width: 2f);
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
    /// <summary>A square around whatever the player last clicked.</summary>
    /// <remarks>
    /// A square rather than the ring villagers get, so the two selections never read as
    /// the same thing: people are round on this map and buildings are square, and the
    /// highlight should agree with that rather than argue with it.
    /// </remarks>
    private void DrawSelectedTile()
    {
        if (_selectedTile is not GridPos tile)
        {
            return;
        }

        Vector2 centre = ToScreen(tile);
        float side = _pixelsPerTile * 0.92f;

        DrawRect(
            new Rect2(centre - (new Vector2(side, side) / 2f), new Vector2(side, side)),
            SelectedRing,
            filled: false,
            width: 2f);
    }

    private void DrawStores()
    {
        SimWorld world = _world!;

        for (int i = 0; i < world.StoreBuildings.Count; i++)
        {
            StoreBuilding building = world.StoreBuildings[i];

            Vector2 centre = ToScreen(building.Position);
            float size = Mathf.Max(8f, _pixelsPerTile * 0.8f);
            var rect = new Rect2(centre - (Vector2.One * size / 2f), Vector2.One * size);

            Color colour = building.Kind switch
            {
                StoreKind.Granary => GranaryColour,
                StoreKind.Shed => ShedColour,
                _ => MarketColour,
            };
            DrawRect(rect, colour with { A = 0.85f });
            DrawRect(rect, colour, filled: false, width: 2f);

            // ⭐ A FULL STORE SAYS SO ON THE MAP (Joe, D140). D134 is the reason it has to:
            // a village can sit at "Logs 15" with 1,968 stranded outside a shed that filled
            // in year five, and every symptom of that reads as a shortage. The Overview line
            // says it in words now; this is the same fact where the player is actually
            // looking, on the building that is causing it.
            //
            // A ring rather than a badge, because it has to read at any zoom — the tile is
            // eight pixels across when the valley is fitted to the window.
            if (ShowsFullMarker(building) && building.Store.IsFull)
            {
                float halo = size * 0.85f;
                DrawArc(
                    centre,
                    halo,
                    0f,
                    Mathf.Tau,
                    24,
                    FullStoreColour,
                    width: Mathf.Max(2f, _pixelsPerTile * 0.12f));
            }
        }
    }

    /// <summary>
    /// Whether this building's full-marker is switched on — globally, and for itself.
    /// </summary>
    /// <remarks>
    /// <b>⚠️ VIEW STATE, DELIBERATELY, AND IT MUST STAY THAT WAY.</b> Joe asked for the marker
    /// to be dismissable *"by building or globally"*, which is a per-building fact and therefore
    /// looks like it belongs on <see cref="StoreBuilding"/>. It does not: the sim is hashed and
    /// replayed from a seed (D2), so putting a display preference in it would make two players
    /// who merely disagree about what to look at diverge into different worlds. A marker nobody
    /// can see must not change what anybody does.
    /// </remarks>
    private bool ShowsFullMarker(StoreBuilding building) =>
        _showFullMarkers && !_fullMarkerMuted.Contains(building.Id);

    private bool _showFullMarkers = true;
    private readonly HashSet<int> _fullMarkerMuted = new();

    /// <summary>Switch every full-store marker on or off at once.</summary>
    public void ShowFullMarkers(bool shown)
    {
        _showFullMarkers = shown;
        QueueRedraw();
    }

    /// <summary>Switch one building's marker on or off, and report where it landed.</summary>
    public bool ToggleFullMarker(int buildingId)
    {
        bool nowShown = _fullMarkerMuted.Remove(buildingId);
        if (!nowShown)
        {
            _fullMarkerMuted.Add(buildingId);
        }

        QueueRedraw();
        return nowShown;
    }

    /// <summary>Whether one building's marker is switched on, ignoring the global switch.</summary>
    public bool FullMarkerShownFor(int buildingId) => !_fullMarkerMuted.Contains(buildingId);

    private void DrawValley()
    {
        SimConfig config = _world!.Config;

        // Half a tile out on each side, so the edge sits at the outside of the last
        // tile rather than through the middle of it.
        Vector2 topLeft = ToScreen(new Vector2(config.MapMinX - 0.5f, config.MapMinY - 0.5f));
        Vector2 bottomRight = ToScreen(new Vector2(config.MapMaxX + 0.5f, config.MapMaxY + 0.5f));
        var valley = new Rect2(topLeft, bottomRight - topLeft);

        DrawRect(valley, Ground);

        // Clipped to what is actually on screen: at full zoom-in a loop over 120
        // columns is mostly wasted, and at full zoom-out it is 120 lines a pixel apart.
        Vector2 first = ToTile(Vector2.Zero);
        Vector2 last = ToTile(Size);

        int minX = Mathf.Max(config.MapMinX, Mathf.FloorToInt(first.X));
        int maxX = Mathf.Min(config.MapMaxX + 1, Mathf.CeilToInt(last.X));
        int minY = Mathf.Max(config.MapMinY, Mathf.FloorToInt(first.Y));
        int maxY = Mathf.Min(config.MapMaxY + 1, Mathf.CeilToInt(last.Y));

        // THE GENERATED TERRAIN (D18). Drawn under everything else, because it is the
        // ground the rest of the village stands on — and because without it a
        // generated valley is invisible, which makes "is this seed worth playing?"
        // a question nobody can answer by looking.
        DrawTerrain(minX, maxX, minY, maxY);
        DrawResidentialLand(minX, maxX, minY, maxY);

        DrawRect(valley, ValleyEdge, filled: false, width: 2f);

        // Only worth drawing the grid while tiles are big enough to read.
        if (_pixelsPerTile < 6f)
        {
            return;
        }

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
    /// Where the player has said the village may live (D42).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Drawn always, not only while painting. A residential zone is a standing decision
    /// — it is the answer to "why did that house go there?" and to "why has nobody
    /// moved out in twenty years?" — so it has to be visible when you are not thinking
    /// about it, which is exactly when those questions occur to you.
    /// </para>
    /// <para>
    /// Faint, though. It is ground the village <em>may</em> use, not something built,
    /// and it should never compete with the people standing on it.
    /// </para>
    /// </remarks>
    private void DrawResidentialLand(int minX, int maxX, int minY, int maxY)
    {
        ZoneMap zones = _world!.Zones;
        float size = _pixelsPerTile * 1.02f;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var tile = new GridPos(x, y);
                if (!zones.IsResidential(tile))
                {
                    continue;
                }

                Vector2 centre = ToScreen(tile);
                var rect = new Rect2(centre - (Vector2.One * size / 2f), Vector2.One * size);
                DrawRect(rect, ResidentialColour);
            }
        }

        // ⭐ GROUND THAT BELONGS TO A BUILDING (D86, D112) — and this is the layer that was
        // simply never drawn. Joe: *"'give ground' and 'take back' seem to do nothing."*
        // They did exactly what they say: the sim recorded the tiles and the hut's panel
        // counted them. **Nothing on the map changed, so from the chair the brush was dead.**
        //
        // A zone the player paints and cannot see is worse than one they cannot paint —
        // §1.1 is about the game explaining itself, and a brush whose only feedback is a
        // number in a row somewhere else is a brush that explains nothing.
        //
        // The selected building's ground is drawn stronger than everybody else's, which is
        // how one colour answers *"whose is this?"* without inventing a palette per hut.
        int selected = SelectedGroundOwner();
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var tile = new GridPos(x, y);
                int owner = zones.WorkGroundOwner(tile);
                if (owner == 0)
                {
                    continue;
                }

                Vector2 centre = ToScreen(tile);
                var rect = new Rect2(centre - (Vector2.One * size / 2f), Vector2.One * size);
                DrawRect(rect, owner == selected ? WorkGroundMine : WorkGroundColour);
            }
        }

        // What the village has been told to take (D87). Drawn over the terrain rather
        // than replacing it, so a marked wood still reads as a wood — the paint is an
        // instruction about the ground, not a new kind of ground.
        //
        // Last of the three, because harvest is the one that says something is about to
        // change and the other two say who may use ground that is staying as it is.
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var tile = new GridPos(x, y);
                if (!zones.IsHarvest(tile))
                {
                    continue;
                }

                Vector2 centre = ToScreen(tile);
                var rect = new Rect2(centre - (Vector2.One * size / 2f), Vector2.One * size);
                DrawRect(rect, HarvestColour);
            }
        }
    }

    /// <summary>
    /// Which building's ground the player is looking at, or zero if none is selected.
    /// </summary>
    /// <remarks>
    /// Asked of the selected <em>tile</em> rather than kept as state, for D49's reason: the
    /// selection already lives in one place and a second copy of it is a second thing to keep
    /// in step. A workplace that owns no ground answers the question harmlessly.
    /// </remarks>
    private int SelectedGroundOwner()
    {
        if (_selectedTile is not GridPos tile)
        {
            return 0;
        }

        foreach (Workplace workplace in _world!.Workplaces)
        {
            if (workplace.Position == tile && !workplace.IsSite)
            {
                return workplace.Id;
            }
        }

        return 0;
    }

    /// <summary>
    /// The river and the woods, as generated from the run's seed (D18).
    /// </summary>
    /// <remarks>
    /// <para>
    /// One filled rect per non-grass tile, clipped to the visible window. Grass is
    /// skipped rather than drawn, because it is already the valley's base colour and
    /// filling ninety per cent of the screen with rectangles of the colour underneath
    /// them is a lot of work to change nothing.
    /// </para>
    /// <para>
    /// A tile is drawn a hair over one tile wide. At fractional zoom, exactly-one-tile
    /// rects leave seams between neighbours where the rounding falls differently, and
    /// a river with gaps in it reads as a bug rather than as a river.
    /// </para>
    /// </remarks>
    private void DrawTerrain(int minX, int maxX, int minY, int maxY)
    {
        GeneratedMap map = _world!.Map;
        float size = _pixelsPerTile * 1.02f;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var tile = new GridPos(x, y);
                Terrain terrain = map.TerrainAt(tile);
                if (terrain == Terrain.Grass)
                {
                    continue;
                }

                Vector2 centre = ToScreen(tile);
                var rect = new Rect2(centre - (Vector2.One * size / 2f), Vector2.One * size);
                DrawRect(rect, ColourOf(terrain));
            }
        }
    }

    /// <summary>What a kind of ground is drawn as.</summary>
    /// <remarks>
    /// <b>Grass never reaches here from the valley itself</b> — it is the background, skipped
    /// by <see cref="DrawTerrain"/> so the common case draws nothing at all. It has an arm
    /// anyway because the minimap bakes every tile into a texture and has no background to
    /// skip against, and a <c>_ =></c> falling through to woodland would have painted the
    /// whole valley as forest.
    /// </remarks>
    internal static Color ColourOf(Terrain terrain) => terrain switch
    {
        Terrain.Water => WaterColour,
        Terrain.Rock => RockColour,
        Terrain.IronDeposit => IronColour,
        Terrain.Grass => Ground,
        _ => ForestColour,
    };

    /// <summary>
    /// The colours the minimap borrows, so there is one palette and not two.
    /// </summary>
    /// <remarks>
    /// A second set of terrain colours would drift the first time one of these was tuned,
    /// and then the small map and the big one would disagree about which green is a wood —
    /// which is the whole job of a minimap failing.
    /// </remarks>
    internal static Color GroundColour => Ground;

    internal static Color BeyondColour => Beyond;

    internal static Color BuildingColour => HutColour;

    internal static Color DwellingColour => HomeColour;

    internal static Color StoreColour => GranaryColour;

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
                ToScreen(world.RestingPlaceOf(villager)),
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

            // ⛔ THE CATCHMENT RING IS GONE WITH THE CATCHMENT. It drew *"how far it is
            // reasonable to come from"* as a faint circle — the fence made visible rather
            // than merely enforced — and there is no fence to draw
            // (`forests-and-gathering.md §3`).
            //
            // **Nothing replaces it, deliberately.** The thing that decides who works where
            // is a cost-first sort over every hand in the village, and there is no circle
            // that says that: the honest picture of it is the route lines this map already
            // draws, one per villager, which show the actual walk rather than a boundary
            // nobody is subject to. Drawing a ring that no longer means anything would be
            // worse than drawing nothing — it would still look like a rule.
            //
            // A gatherer's hut DOES still have a ring, and it is a different thing entirely:
            // the ground its yield is computed from. That is `GatheringRadius`, and it is
            // drawn below on the huts that have one.
            bool ringWanted = _detail == MapDetail.All
                || (_detail == MapDetail.Selected && workplace.Id == selectedWorkplace);

            if (ringWanted && workplace.GatheringRadius > 0)
            {
                float radius = workplace.GatheringRadius * _pixelsPerTile;
                if (radius <= Mathf.Max(Size.X, Size.Y) * 2f)
                {
                    DrawArc(centre, radius, 0f, Mathf.Tau, 64, colour with { A = 0.22f }, 1f);
                }
            }

            // A construction site is drawn as an outline that fills in as it is built,
            // rather than as a dot like the workplaces. A half-raised granary should be
            // legible on the map — that is one of the three things D43 says paying for
            // buildings with labour buys, and it is the one you can actually see.
            if (workplace.Construction is { } site)
            {
                float size = Mathf.Max(10f, _pixelsPerTile * 0.8f);
                var rect = new Rect2(centre - (Vector2.One * size / 2f), Vector2.One * size);

                int total = System.Math.Max(1, site.Recipe.Logs + site.Recipe.WorkTicks);
                float done = (site.LogsDelivered + site.WorkDone) / (float)total;

                DrawRect(rect, SiteColour with { A = 0.18f });
                if (done > 0f)
                {
                    var filled = new Rect2(
                        rect.Position + new Vector2(0f, rect.Size.Y * (1f - done)),
                        new Vector2(rect.Size.X, rect.Size.Y * done));
                    DrawRect(filled, SiteColour with { A = 0.55f });
                }

                DrawRect(rect, SiteColour, filled: false, width: 2f);
                continue;
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

            // Nothing to draw for a family that has not built yet (D70).
            if (household.HomePosition is not GridPos site)
            {
                continue;
            }

            Vector2 centre = ToScreen(site);
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

            // Where they are drawn this frame — and the click test asks the same method,
            // so what the player aims at is what they hit (see DrawnCentre).
            Vector2 centre = DrawnCentre(villager);
            float radius = VillagerRadius;

            var current = new Vector2(villager.Position.X, villager.Position.Y);
            Vector2 previous = _previousTiles.TryGetValue(villager.Id, out Vector2 known) ? known : current;

            if (current != previous && _alpha >= 0.999)
            {
                _previousTiles[villager.Id] = current;
            }
            else if (!_previousTiles.ContainsKey(villager.Id))
            {
                _previousTiles[villager.Id] = current;
            }

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

    /// <summary>How big a person is drawn, in pixels. Never smaller than a clickable dot.</summary>
    private float VillagerRadius => Mathf.Max(3f, _pixelsPerTile * 0.2f);

    /// <summary>
    /// Exactly where a villager is drawn on screen this frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One method, because drawing and clicking must agree.</b> A person is not drawn at
    /// their sim tile: they glide between tiles as the tick plays out (<c>_alpha</c>) and they
    /// are fanned off a crowded tile so a household reads as a household. A hit test written
    /// against <c>villager.Position</c> would therefore miss by most of a tile for anybody
    /// walking, and by the fan radius for anybody standing at home — which is to say, it would
    /// miss whenever it mattered.
    /// </para>
    /// <para>
    /// Pure: it reads the interpolation bookkeeping and never writes it, so asking where
    /// somebody is drawn cannot move them.
    /// </para>
    /// </remarks>
    private Vector2 DrawnCentre(Villager villager)
    {
        var current = new Vector2(villager.Position.X, villager.Position.Y);
        Vector2 previous = _previousTiles.TryGetValue(villager.Id, out Vector2 known) ? known : current;

        // Lerp from where they were to where they are. If they moved more than a
        // tile — being born, or moving house — snap instead, or they would glide
        // across the map.
        Vector2 drawTile = previous.DistanceSquaredTo(current) > 2f
            ? current
            : previous.Lerp(current, (float)_alpha);

        return ToScreen(drawTile + FanOffset(villager));
    }

    /// <summary>
    /// Whoever the player just clicked on, or null if they clicked past everybody.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ Clicking a villager on the map has never worked</b> — the map has only ever
    /// hit-tested buildings, so the roster was the sole way to select a person. Joe asked for
    /// it directly, and it is the obvious gesture: the people are the thing you are watching.
    /// </para>
    /// <para>
    /// <b>A person beats the ground they stand on.</b> Villagers are small and drawn on top of
    /// everything, and they stand on their own doorsteps constantly — so if the pointer is on
    /// somebody, they are what was meant, and the tile underneath is a click away by aiming
    /// anywhere else in it. The nearest of a crowd wins, which is what the fan is for.
    /// </para>
    /// <para>
    /// A little forgiveness on the radius, because a person is a four-pixel dot when the
    /// camera is out and a target you cannot hit is the same bug as a button behind a panel
    /// (D113). Not so much that the slack itself swallows a tile.
    /// </para>
    /// </remarks>
    private Villager? VillagerAt(Vector2 screen)
    {
        SimWorld world = _world!;
        float reach = VillagerRadius + 4f;
        float nearest = reach * reach;
        Villager? hit = null;

        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (!villager.Alive)
            {
                continue;
            }

            // Strictly nearer, so a tie goes to the lower id and the selection does not
            // flicker between two people standing on the same spot.
            float distance = DrawnCentre(villager).DistanceSquaredTo(screen);
            if (distance < nearest)
            {
                nearest = distance;
                hit = villager;
            }
        }

        return hit;
    }

    private bool InScope(int villagerId) =>
        _detail == MapDetail.All || (_detail == MapDetail.Selected && villagerId == _selectedVillagerId);

    /// <remarks>
    /// A switch rather than "forager, else assume trees". That shortcut was correct
    /// while there were two kinds of work; with four it drew the woodcutter's hut and
    /// the market as tree stands, so the map claimed the village had woodland it did
    /// not have.
    /// </remarks>
    private static Color ColourOf(JobKind kind) => kind switch
    {
        JobKind.Forager => BerryColour,
        JobKind.Forester => TreeColour,
        JobKind.Woodcutter => HutColour,
        _ => MarketColour,
    };

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
