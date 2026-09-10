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
/// <b>⚠️ That bookkeeping rolls forward on the sim's tick and on nothing else</b>
/// (<see cref="AdvanceInterpolation"/>). Alpha says how far through a tick we are; it can
/// never say that a tick ended, and reading it as though it could is what put the jitter
/// Joe watched for months on the screen (D169).
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
    private static readonly Color WarehouseColour = new("#8a7a63");
    private static readonly Color BerryColour = new("#5aa04a");
    private static readonly Color TreeColour = new("#2f6b3a");

    /// <summary>The generated river (D18).</summary>
    /// <remarks>
    /// Deliberately the most distinct colour on the map. Water is about to become the
    /// one thing a villager cannot walk over (D40), so it needs to read as an obstacle
    /// at a glance and long before a bridge exists to argue with it.
    /// </remarks>
    private static readonly Color WaterColour = new("#2f5f7a");

    /// <summary>
    /// ⭐ The water where it meets the land — <b>this is the shoreline now</b> (D342).
    /// </summary>
    /// <remarks>
    /// <b>The bank used to be a traced polyline over the water tiles (D337), and it was drawn half
    /// a tile off them (D338) and came out as a chain of scallops.</b> A river is shallow at its
    /// edge; the bake lerps between this and <see cref="WaterColour"/> by how decisively the water
    /// field won at each pixel, which is the same contour as the fill **by construction**. *There
    /// is no second shape to get wrong any more.*
    /// </remarks>
    internal static readonly Color ShallowsColour = new("#4c86a0");

    /// <summary>A fishing hut — the river's colour, lifted so the hut reads against it.</summary>
    private static readonly Color FisheryColour = new(0.42f, 0.68f, 0.78f);

    /// <summary>Generated woodland. Quieter than the tree stand that stands in it.</summary>
    private static readonly Color ForestColour = new("#2a3d2c");

    /// <summary>Young trees — <b>between bare ground and woodland, and visibly so</b>.</summary>
    /// <remarks>
    /// ⭐⭐ It had NO COLOUR AT ALL until D221 (Joe, from a screenshot). `ColourOf`'s
    /// <c>_ =&gt;</c> arm handed it <see cref="ForestColour"/>, so a sapling was drawn as a mature
    /// tree and a freshly planted patch looked identical to old woodland.
    /// <para>
    /// <b>That is D125's own argument broken in the one place it was making it:</b> a sapling is
    /// its own ground rather than a hidden countdown *because it is visible* — *"the player can
    /// see their wood coming back, see where it has not"*. Drawn as forest, it could not.
    /// </para>
    /// <para>
    /// Lighter and yellower than <see cref="ForestColour"/>, so young growth reads as thinner
    /// than old wood at a glance, and clearly not <see cref="Ground"/>.
    /// </para>
    /// </remarks>
    private static readonly Color SaplingColour = new("#3d5433");

    /// <summary>A canopy, a shade above the ground it stands on so the wood has texture (D337).</summary>
    private static readonly Color TreeCanopy = new("#3c5a3e");

    /// <summary>A young tree — smaller, lighter, and obviously not yet timber.</summary>
    private static readonly Color SaplingCanopy = new("#4f6b41");

    /// <summary>Keeps the trees' scatter out of step with the animals' (D337).</summary>
    private const int TreeSalt = 5701;

    /// <summary>A stone seam — pale and dry against the grass, so it reads as bare ground.</summary>
    private static readonly Color RockColour = new("#6b6459");

    /// <summary>An iron seam. Rusted, and warmer than the stone so the two never blur.</summary>
    /// <remarks>
    /// <b>Two seams a player must tell apart at a glance</b> (D67: you go after a seam
    /// because you can see it), so they differ in hue rather than only in lightness —
    /// which is also the one difference that survives being colour-blind.
    /// </remarks>
    private static readonly Color IronColour = new("#7a4a33");

    // ---------------------------------------------------------------
    //  The field, in its three states (`specs/crops-and-orchards.md`)
    // ---------------------------------------------------------------
    //
    // ⭐⭐ THESE ARE THE MECHANIC, NOT A DECORATION, AND THAT IS THE WHOLE ARGUMENT FOR CROPS.
    // `environment-and-seasons.md §5.1` proposed a seasonal yield CURVE — three multipliers on
    // foraging — and its own account of what the player would see was *"a villager simply comes
    // home with more in autumn"*, offered as a virtue because it needed no UI. That is the
    // objection: it is a number going up where nobody can watch it. A field is bare, then sown,
    // then standing, then bare again, and **the difference on the screen is the feature**.
    //
    // ⚠️ AND THEY HAD TO BE NAMED HERE OR THEY WOULD HAVE SHIPPED AS WOODLAND. `ColourOf`'s
    // default arm is `ForestColour` — a deliberate choice for the sapling, and a trap for
    // anything else appended to `Terrain`. Three new values would have drawn a farm as a wood
    // in every season, which is D108's silent-default finding arriving in the view.

    /// <summary>Ploughed and bare. Winter and early spring — turned earth, warm and dark.</summary>
    private static readonly Color FieldColour = new("#4a3a2b");

    /// <summary>Sown. Young growth: green, but paler and yellower than the woods.</summary>
    private static readonly Color SownColour = new("#4a5c33");

    /// <summary>Standing ripe. The one colour on the map that says "come and take this".</summary>
    /// <remarks>
    /// <b>Differs from <see cref="SownColour"/> in hue as well as lightness</b>, on the rule
    /// D67 set for the two seams: the difference a player has to read at a glance is the one
    /// that must survive being colour-blind.
    /// </remarks>
    private static readonly Color RipeColour = new("#b8933f");

    /// <summary>The woodcutter's hut — a workplace, not a stand of trees.</summary>
    private static readonly Color HutColour = new("#9a6b3f");

    /// <summary>The market (D14), which is both a workplace and a store.</summary>
    private static readonly Color MarketColour = new("#c98f4a");

    // A library reads as ink rather than as grain or timber — deliberately unlike every other
    // building on the map, because it is the only one that produces nothing you can eat, burn or
    // build with. Cool blue against a palette that is otherwise earth and harvest.
    private static readonly Color LibraryColour = new("#6a7fc9");

    // The town hall is cut stone with the founders' names on it — pale, and the lightest thing on
    // the map. It is next to the library on the wheel because they are the two buildings that
    // produce nothing the village can eat, burn or build with, and far enough from it that
    // *"which of those two blue squares is which?"* is never a question.
    private static readonly Color TownHallColour = new("#b9b2a6");

    /// <summary>The ring round a store with no room left (D140).</summary>
    /// <remarks>
    /// Warm amber rather than red. A full store is not a disaster — it is usually a village
    /// doing well at something — and §1.1 wants the player to look, not to panic.
    /// </remarks>
    private static readonly Color FullStoreColour = new("#e8a13c");

    /// <summary>The ring round a workplace that cannot do its job (D147).</summary>
    /// <remarks>
    /// <b>Cool, where the full-store ring is warm</b>, because they are different facts and the
    /// player should be able to tell them apart without reading anything. A full store is
    /// usually a village doing well at something; an idle hut never is. Still not red — the fix
    /// is always a decision rather than an emergency (§0.1).
    /// </remarks>
    private static readonly Color IdleWorkplaceColour = new("#7fb2d9");

    /// <summary>A building marked out but not yet raised (D43).</summary>
    private static readonly Color SiteColour = new("#8f9aa8");

    // A building coming down reads as warm rust against the site's cool grey -- the two are the
    // same shape doing opposite things, so colour is what tells them apart at a glance.
    private static readonly Color DemolishColour = new("#b5714a");

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

    /// <summary>
    /// ⭐ Marked ground with nothing left on it — <b>a standing order that is waiting,
    /// not shouting</b> (D343).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe, at a felled-out circle under his forager's hut: *"lets solve for this thing that
    /// shouldn't even show up."*</b> ⛔ **It should show up — that is his own D127.**
    /// Un-painting cleared ground was tried and rejected: *"with regrowth, a bare painted tile is
    /// not finished work — it is work that is waiting."* The wood comes back and the village
    /// fells it again, because he asked it to.
    /// </para>
    /// <para>
    /// ⛔ <b>So it cannot be hidden either.</b> An invisible standing instruction that fells
    /// a wood years later is exactly the unexplainable behaviour §1.1 forbids, and it is
    /// what D42 and D123 deleted in another medium. **The outline stays at full strength** —
    /// *this ground is spoken for* — and only the fill drops away, because the fill is what
    /// says *there is work here now*.
    /// </para>
    /// </remarks>
    private static readonly Color HarvestWaitingColour = new("#d8892f", 0.07f);

    /// <summary>Ground a building has been given to work (D86).</summary>
    /// <remarks>
    /// <b>Cool, where the other two zone washes are warm</b>, because it means a different
    /// kind of thing and the three overlap: residential says <em>the village may build
    /// here</em>, harvest says <em>this is coming down</em>, and this says <em>a named
    /// building works this</em>. Two warm washes and a cool one is a distinction that
    /// survives being seen out of the corner of your eye, and being colour-blind.
    /// </remarks>
    private static readonly Color WorkGroundColour = new("#4a9ba8", 0.16f);

    // ⭐ THE BORDERS ARE THE SAME HUES AT FULL STRENGTH (D332). The wash says *this ground is
    // spoken for*; the line says *this far and no further*, and it is the line that has to survive
    // being read at a glance over terrain. **Same colour, so it is obviously the same zone** —
    // a border in a new hue would be a fourth thing to learn.
    private static readonly Color ResidentialEdge = new("#b98a52", 0.55f);

    private static readonly Color HarvestEdge = new("#d8892f", 0.70f);

    private static readonly Color WorkGroundEdge = new("#4a9ba8", 0.65f);

    /// <summary>The selected building's own ground, brighter than everybody else's.</summary>
    private static readonly Color WorkGroundMine = new("#5fc8d8", 0.30f);

    /// <summary>Ground better than ordinary, on the soil overlay (D178).</summary>
    /// <remarks>
    /// <b>Green for rich and brown for thin</b> — the one place in this palette where the
    /// obvious colours are the right ones, because they are what soil actually looks like.
    /// Both are keyed off <see cref="VillageEconomy.ReferenceSoil"/>, so the eye reads
    /// *distance from ordinary* rather than an absolute nobody could calibrate.
    /// </remarks>
    private static readonly Color RichGround = new("#6fbf5f", 0.55f);

    /// <summary>Ground worse than ordinary, on the soil overlay (D178).</summary>
    private static readonly Color ThinGround = new("#9a7448", 0.55f);

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

    /// <summary>
    /// The two ends of this frame's glide for each villager: the tile they were on last
    /// tick, and the tile they are on now.
    /// </summary>
    /// <remarks>
    /// <b>Both ends are kept because only the sim knows when a tick happened.</b> Alpha says
    /// how far through a tick we are; it never says that one ended. Advancing this on a
    /// reading of alpha is what D169 fixed.
    /// </remarks>
    private readonly Dictionary<int, (Vector2 Previous, Vector2 Current)> _tiles = new();

    /// <summary>Reused each frame so a busy village does not allocate per redraw.</summary>
    private readonly Dictionary<GridPos, List<int>> _byTile = new();

    /// <summary>
    /// The sim tick <see cref="_tiles"/> was last advanced for. <see cref="ulong.MaxValue"/>
    /// until the first frame, which cannot collide with a real tick.
    /// </summary>
    private ulong _interpolatedThroughTick = ulong.MaxValue;

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
            if (household.HomeTile is not GridPos standing)
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

    /// <summary>Which way the building in the player's hand is turned (gridless 2b, D320).</summary>
    /// <remarks>
    /// ⭐ <b>View state, not sim state, until the moment it is marked.</b> A ghost being turned is a
    /// decision in progress; only placing it makes it a fact about the village.
    /// ⚠️ <b>A sixteenth of a turn per press is a UI choice, not a limit of the type.</b>
    /// <c>Angle</c> holds 65,536 of them (D318); 22.5° is simply what a keypress can aim. *If a
    /// finer control ever wants it — a drag, a modifier — the type is already there.*
    /// </remarks>
    private Angle _ghostFacing;

    /// <summary>True when the next click pulls a building down instead of raising one.</summary>
    private bool _demolishing;

    /// <summary>Painting homes: 0 not, 1 painting, -1 erasing.</summary>
    private int _brush;

    /// <summary>How wide the brush is, in tiles either side. <b>The wheel drives it</b> (D327).</summary>
    /// <remarks>
    /// <para>
    /// A brush rather than a single tile, because a residential area is a
    /// <em>neighbourhood</em> — asking the player to paint it a tile at a time would be
    /// exactly the click-farm zoning exists to avoid (D42). ⭐ <b>And a brush that is always the
    /// same size is that click-farm again</b> the moment the ground the player has in mind is
    /// bigger or smaller than five tiles across.
    /// </para>
    /// <para>
    /// ⛔ <b>DELIBERATELY NOT WRITTEN BY <see cref="SetTool"/>, unlike every other brush field.</b>
    /// The size and the shape are settings that outlive what is in hand: picking up the harvest
    /// brush after sizing the land brush must not silently resize it. <c>SetTool</c> stays the one
    /// writer of the fields that say *what* is held (`specs/build-bar.md §5.1`, three bugs); these
    /// two say *how*, and they are outside that set on purpose.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// ⛔⛔ <b>IN SUB-TILES SINCE D336, AND THE SENTENCE STILL SAYS TILES.</b> The brush lays down
    /// quarter-tiles because that is the only way a small round brush can be round (D335); it is
    /// *described* in tiles because that is what the valley is measured in and what every other
    /// number the player has learned is in. **Two units for one quantity is D322's trap**, so the
    /// conversion lives in exactly one place — <c>BrushStroke.AcrossInTiles</c>, where the words
    /// are made.
    /// </remarks>
    private int _brushRadius = BrushStroke.DefaultSubRadius;

    /// <summary>Whether the brush is a square or a round (D327). Square is what has always shipped.</summary>
    private BrushShape _brushShape = BrushShape.Square;

    /// <summary>What shape the brush is set to, so the bar can say so.</summary>
    public BrushShape BrushShapeInHand => _brushShape;

    /// <summary>Swap the brush between a square and a round — the bar's button and <c>B</c>.</summary>
    /// <remarks>
    /// ⚠️ Redraws and re-announces even when no brush is held, because the sentence and the preview
    /// are the only places the setting is visible and the player may well set it before picking a
    /// brush up.
    /// </remarks>
    public void CycleBrushShape()
    {
        _brushShape = _brushShape == BrushShape.Square ? BrushShape.Round : BrushShape.Square;
        Announce();
        QueueRedraw();
        BrushChanged?.Invoke();
    }

    /// <summary>Raised when the brush's size or shape changes, so the bar can relabel its button.</summary>
    public event System.Action? BrushChanged;

    /// <summary>How the brush reads in a sentence — <em>"5×5 square"</em>.</summary>
    private string TheBrushInWords()
    {
        // ⭐ THE CONVERSION LIVES HERE, WHERE THE WORDS ARE MADE (D336). The sim counts sub-tiles;
        // turning that into "5.25 tiles" is presentation, and `FloatBanTests` said so by reddening
        // when the arithmetic sat in `Bclone.Sim` instead.
        float across = BrushStroke.AcrossInSubTiles(_brushRadius) / (float)SubTile.PerTile;

        return $"{across:0.##} tiles {(_brushShape == BrushShape.Round ? "round" : "square")}";
    }

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

    /// <summary>
    /// <b>What is in the player's hand</b> — one value, where there used to be eight fields.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>The bar needs to read this and could not.</b> Seven <c>Begin*</c> methods each set
    /// some subset of <c>_building</c>, <c>_demolishing</c>, <c>_brush</c>, <c>_harvestMode</c>,
    /// <c>_groundFor</c>, <c>_moving</c>, <c>_moveFrom</c> and <c>_emptying</c>, and nothing
    /// could ask what the answer was — <b>so no tab could light up</b>.
    /// </para>
    /// <para>
    /// ⛔⛔ <b>AND EIGHT FIELDS CLEARED IN SEVEN PLACES HAD ALREADY GONE WRONG THREE TIMES</b>
    /// (`specs/build-bar.md §5.1`). Four of the seven never cleared <c>_groundFor</c>, which
    /// <see cref="Announce"/> tests <em>first</em> — so pressing <b>Demolish</b> after painting
    /// work ground announced <em>"Drag to give ground to forester's hut"</em>, and
    /// <b>right-clicking to cancel</b> answered with <em>"Right-click to stop."</em> The third
    /// was worse and is fixed in <see cref="PaintAround"/>. <b>One writer is the fix</b>, and it
    /// is D145's rule: the moment two callers want the same clearing, it stops being private.
    /// </para>
    /// </remarks>
    public enum MapTool
    {
        /// <summary>Just looking. Nothing on the bar is lit.</summary>
        None,

        /// <summary>About to put a building down — <see cref="PendingBuilding"/> says which.</summary>
        Building,

        /// <summary>The next click pulls a building down.</summary>
        Demolishing,

        /// <summary>Painting where the village may live (D42).</summary>
        PaintingHomes,

        /// <summary>Taking that land back — and over a house, an order to pull it down.</summary>
        ErasingHomes,

        /// <summary>Marking what the village means to take — <see cref="PendingHarvest"/> says what.</summary>
        Harvesting,

        /// <summary>Rubbing that marking out.</summary>
        Unmarking,

        /// <summary>Giving ground to one building (D86). <b>Not on the bar</b> — see below.</summary>
        PaintingGround,

        /// <summary>Taking that building's ground back. <b>Not on the bar</b> — see below.</summary>
        ErasingGround,

        /// <summary>Picking a building up to put it down elsewhere (D229).</summary>
        Moving,

        /// <summary>Marking a store to be carried out into the others (D231).</summary>
        Emptying,
    }

    /// <summary>What the player is holding right now.</summary>
    /// <remarks>
    /// ⚠️ <see cref="MapTool.PaintingGround"/> and <see cref="MapTool.ErasingGround"/> are on the
    /// enum but <b>deliberately not on the build bar</b>: that brush belongs to a <em>building</em>
    /// and is reached from that building's panel (D86, D93 — <em>"a control that is always there
    /// but never says WHAT it acts on is the thing you hunt for"</em>). The bar needs the values
    /// only so it can unlight every tab while one is in hand.
    /// </remarks>
    public MapTool Tool { get; private set; } = MapTool.None;

    /// <summary>Which building is about to go down, or null when that is not the tool.</summary>
    /// <remarks>
    /// The strip lights the <em>button</em>, not just the tab, which is why this is public
    /// alongside <see cref="Tool"/>.
    /// </remarks>
    public BuildingKind? PendingBuilding => _building;

    /// <summary>Which harvest mode the brush is set to, or null when that is not the tool.</summary>
    public HarvestBrush? PendingHarvest => _harvestMode;

    /// <summary>
    /// Raised whenever <b>what is in the player's hand</b> changes, so the bar can relight itself.
    /// </summary>
    /// <remarks>
    /// ⭐ <b><see cref="Announce"/> is the seam and that is not a coincidence</b> — every
    /// <c>Begin*</c> already calls it to say what the tool does, so the one place that already
    /// runs on every tool change is the one place this needs to fire from. <b>No new call
    /// sites.</b>
    /// </remarks>
    public event System.Action? ToolChanged;

    /// <summary>
    /// <b>The only writer of the brush fields.</b> Everything else asks this.
    /// </summary>
    private void SetTool(
        MapTool tool,
        BuildingKind? building = null,
        HarvestBrush? harvest = null,
        int groundFor = 0,
        int brush = 0)
    {
        // ⚠️ THE WHOLE HAND, NOT JUST THE ENUM. Granary → Warehouse is `Building` → `Building`,
        // and Trees → Stone is `Harvesting` → `Harvesting` — so a `Tool != tool` test would say
        // nothing changed and **leave the bar lighting the button the player just moved off.**
        // *The tool is the enum AND what it is set to.*
        bool changed = Tool != tool
            || _building != building
            || _harvestMode != harvest
            || _groundFor != groundFor;

        // ⛔ EVERY FIELD, EVERY TIME, IN ONE PLACE. The bug this replaces was never a wrong
        // value — it was a field somebody forgot to clear in one of seven near-identical
        // blocks, three times over.
        _building = building;
        _harvestMode = harvest;
        _groundFor = groundFor;
        _brush = brush;
        _demolishing = tool == MapTool.Demolishing;
        _moving = tool == MapTool.Moving;
        _emptying = tool == MapTool.Emptying;
        _moveFrom = null;

        Tool = tool;

        Announce();
        QueueRedraw();

        // ⚠️ AFTER Announce, not before: the bar reads `Tool` when this fires, and a listener
        // that ran first would light a tab the message had not caught up with.
        if (changed)
        {
            ToolChanged?.Invoke();
        }
    }

    /// <summary>Start or stop painting where the village may live (D42).</summary>
    public void BeginPainting(int direction) =>
        SetTool(direction < 0 ? MapTool.ErasingHomes : MapTool.PaintingHomes, brush: direction);

    /// <summary>Start or stop marking what the village means to take (D87, D92).</summary>
    public void BeginHarvesting(HarvestBrush mode, int direction) =>
        SetTool(
            direction < 0 ? MapTool.Unmarking : MapTool.Harvesting,
            harvest: mode,
            brush: direction);

    /// <summary>Start or stop giving ground to one building (D86).</summary>
    public void BeginPaintingGround(int workplaceId, int direction) =>
        SetTool(
            direction < 0 ? MapTool.ErasingGround : MapTool.PaintingGround,
            groundFor: workplaceId,
            brush: direction);

    /// <summary>The tile under the cursor, and what the sim says about building on it.</summary>
    private GridPos _hovered;

    /// <summary>Where the cursor actually is, as the sim will believe it (gridless 2c, D330).</summary>
    /// <remarks>
    /// ⭐ <b><see cref="_hovered"/> stays</b> — it is *"which tile is the cursor over?"*, which the
    /// brush, the map bounds and the market's service ring all still ask. This is *"where is the
    /// cursor?"*, and it is what a building is placed at. **Two questions, two fields**, rather
    /// than one field that has to mean both.
    /// </remarks>
    private Point _hoveredPoint;

    /// <summary>Which quarter-tile the cursor is over — the brush's centre (D336).</summary>
    private SubTile _hoveredSub;

    /// <summary>Whether placement rounds to a tile centre. On by default (Joe's call, D330).</summary>
    /// <remarks>
    /// ⛔ <b>AN INPUT-LAYER SETTING, NOT A SIM ONE, AND THAT IS WHAT KEEPS IT HONEST.</b> It rounds
    /// the point <em>before</em> <c>Mark</c> ever sees it, so the sim believes exactly what it is
    /// told and there is no facade — which is the failure `gridless.md §7.2` refuses in as many
    /// words: *a view that draws a building at 30° while the sim believes an axis-aligned tile is
    /// D80 at architectural scale.*
    /// ⚠️ Nothing in this project persists settings, so it comes back on at every launch. Harmless
    /// while on is the default; worth knowing before anybody reports it as a bug.
    /// </remarks>
    private bool _snapToGrid = true;

    /// <summary>Whether placement is snapping to tile centres, so the bar can say so.</summary>
    public bool SnapsToGrid => _snapToGrid;

    /// <summary>Turn snapping on or off.</summary>
    public void SnapToGrid(bool on)
    {
        _snapToGrid = on;
        Announce();
        QueueRedraw();
    }

    /// <summary>
    /// ⛔⛔ The cursor as a <see cref="Point"/> — <b>and the float stops here</b> (D330).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A float must never become sim state</b> (D2): determinism is *same seed ⇒ byte-identical*,
    /// and float rounding breaks it. The cursor is unavoidably a float, so it is turned into an
    /// <em>exact rational</em> at this boundary — <c>Fixed.FromRatio(round(tiles × 4096), 4096)</c>
    /// — rather than converted bit-for-bit. **The sim receives a number it could have written
    /// itself.**
    /// </para>
    /// <para>
    /// ⭐ <b>1/4096 of a tile is far finer than a pixel at any zoom this game offers</b> — the map
    /// draws at most a few dozen pixels per tile — so the quantisation is invisible, and it is
    /// stated rather than hidden because it is the one place the two number systems touch.
    /// </para>
    /// </remarks>
    private const int CursorSteps = 4096;

    /// <summary>Which quarter-tile the cursor is over — <b>where the brush lands</b> (D336).</summary>
    /// <remarks>
    /// ⚠️ <b>A floor, not a round.</b> The sub-tile grid is an index like the tile grid is, and
    /// `SubTile`'s own doc records why: truncation would fold the quarter at zero to twice the
    /// width of every other, in a valley that straddles its own origin.
    /// </remarks>
    private SubTile SubTileUnderTheCursor(Vector2 screen)
    {
        Vector2 tile = ToTile(screen) + new Vector2(0.5f, 0.5f);

        return new SubTile(
            Mathf.FloorToInt(tile.X * SubTile.PerTile),
            Mathf.FloorToInt(tile.Y * SubTile.PerTile));
    }

    private Point PointUnderTheCursor(Vector2 screen)
    {
        Vector2 tile = ToTile(screen);

        // ⭐ Plus half a tile on the way IN, because the view's integer tile coordinate is that
        // tile's centre and the sim's is its corner — the same seam `ToScreen(Point)` crosses, in
        // the other direction. *One conversion each way, and they are inverses.*
        return new Point(
            Fixed.FromRatio(Mathf.RoundToInt((tile.X + 0.5f) * CursorSteps), CursorSteps),
            Fixed.FromRatio(Mathf.RoundToInt((tile.Y + 0.5f) * CursorSteps), CursorSteps));
    }

    /// <summary>Where a building put down right now would stand — snapped, or exactly here.</summary>
    private Point WhereItWouldStand(Vector2 screen) =>
        _snapToGrid
            ? Point.CentreOf(new GridPos(
                Mathf.RoundToInt(ToTile(screen).X), Mathf.RoundToInt(ToTile(screen).Y)))
            : PointUnderTheCursor(screen);
    private PlacementVerdict _verdict = PlacementVerdict.Fine;

    /// <summary>Raised whenever the ghost's verdict changes, so the shell can say it.</summary>
    public event System.Action<string>? PlacementMessageChanged;

    /// <summary>Start marking out a building. Null stops.</summary>
    public void BeginBuilding(BuildingKind? kind) =>
        SetTool(kind is null ? MapTool.None : MapTool.Building, building: kind);

    /// <summary>Turn the building in the player's hand a sixteenth of a turn.</summary>
    /// <remarks>
    /// ⭐ <b>Bound to R</b>, because a facing the player cannot reach is a sim capability that does
    /// not exist as far as the game is concerned — D227's rule, and this project has shipped four
    /// features that way already.
    /// ⚠️ It deliberately does nothing when no building is held: R while holding a harvest brush
    /// should not silently turn something the player cannot see.
    /// </remarks>
    /// <summary>Whether a middle-button drag is currently turning the held building.</summary>
    private bool _turningTheGhost;

    /// <summary>
    /// ⭐⭐ Turn the held building by a mouse drag — the finest control the type can offer (D325).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe asked whether full freedom means 1/360, and the honest answer is that it depends on
    /// the DRAG, not on the type.</b> <c>Angle</c> holds 65,536 poses (D318). A drag can only reach
    /// as many of them as it has pixels to spend, so the pixels-per-turn ratio <em>is</em> the
    /// granularity.
    /// </para>
    /// <para>
    /// ⭐ <b>At <c>DegreesPerPixel</c> = 1 a full turn is a 360px drag and you can reach 360
    /// positions — so yes, plain dragging is effectively 1/360.</b> ⚠️ <b>Holding shift makes it a
    /// tenth of that</b>, which reaches 3,600 — and at that point the limit is the hand rather than
    /// the arithmetic. *The remainder is carried between events rather than thrown away, so a slow
    /// drag accumulates the fine steps a fast one skips.*
    /// </para>
    /// </remarks>
    private void TurnTheGhostBy(float pixels, bool fine)
    {
        if (_building is null)
        {
            return;
        }

        // Raw steps per pixel: a full turn is 65,536, and a degree is 65,536/360.
        float perPixel = 65536f / 360f * (fine ? 0.1f : 1f);

        // ⚠️ CARRIED, NOT TRUNCATED. Rounding each event to whole steps would silently drop the
        // fraction on every motion, so a slow careful drag would turn LESS than a fast one over
        // the same distance — the opposite of what a fine control should do.
        _turnRemainder += pixels * perPixel;

        int steps = (int)_turnRemainder;
        _turnRemainder -= steps;

        if (steps == 0)
        {
            return;
        }

        _ghostFacing += Angle.FromRaw(unchecked((ushort)steps));
        QueueRedraw();
    }

    private float _turnRemainder;

    public void TurnTheGhost(bool toTheQuarter = false)
    {
        if (_building is null)
        {
            return;
        }

        // ⭐ A SIXTY-FOURTH — 5.6° — BECAUSE A SIXTEENTH WAS TOO COARSE (Joe, 2026-09-07:
        // *"i want finer control than a sixteenth for the turn increment"*). ⚠️ Still a UI choice
        // rather than a limit of the type: `Angle` holds 65,536 poses (D318), and this number is
        // one line to change again.
        // ⭐⭐ AND SHIFT SNAPS TO THE QUARTER, which is what makes a fine step usable rather than
        // tedious: sixteen taps to get back to square would be a worse control than the coarse one
        // it replaced. *Fine by default, coarse on demand — the opposite way round would make the
        // common case the awkward one.*
        _ghostFacing += toTheQuarter
            ? Angle.FromTurnFraction(1, 4)
            : Angle.FromTurnFraction(1, 64);

        QueueRedraw();
    }

    /// <summary>Next click pulls a building down.</summary>
    public void BeginDemolishing() => SetTool(MapTool.Demolishing);

    /// <summary>The tile a move has picked up, waiting for somewhere to put it down.</summary>
    /// <remarks>
    /// <b>Two clicks, like every other two-part act in this game</b> — pick the building, then the
    /// destination. The first click says what is being moved and the second says where, so a
    /// misclick costs a click rather than a building.
    /// </remarks>
    private GridPos? _moveFrom;
    private bool _moving;
    private bool _emptying;

    /// <summary>Pick a building up and put it down somewhere else (D229).</summary>
    public void BeginMoving() => SetTool(MapTool.Moving);

    /// <summary>Mark a store to be carried out into the others, or stop (D231).</summary>
    public void BeginEmptying() => SetTool(MapTool.Emptying);

    /// <summary>Put down whatever is in hand and go back to just looking.</summary>
    /// <remarks>
    /// ⭐ <b>The cancel path used to be <c>BeginBuilding(null)</c> followed by four hand-written
    /// clears</b>, and it still left <c>_groundFor</c> and <c>_harvestMode</c> standing — so
    /// right-clicking out of the work-ground brush answered the cancel with
    /// <em>"Right-click to stop."</em>
    /// ⭐ <b>Three ways in now (D327): <c>Escape</c>, the Cancel button, and right-click for
    /// everything that is not a brush.</b> Right-click stopped being universal the day it started
    /// taking paint back, so the cancel needed a gesture that works for every tool — and
    /// <c>Escape</c> was free.
    /// </remarks>
    public void PutTheToolDown() => SetTool(MapTool.None);

    /// <summary>Whether the player is in the middle of placing, demolishing or painting.</summary>
    public bool IsPlacing => Tool != MapTool.None;

    public override void _GuiInput(InputEvent @event)
    {
        if (_world is null)
        {
            return;
        }

        // The ghost follows the cursor, and the verdict is recomputed as it moves.
        // CanBuildAt is pure, so asking it every frame costs nothing and changes
        // nothing — which is what lets the answer be shown BEFORE anybody commits.
        // ⭐⭐ MIDDLE-DRAG TURNS THE BUILDING IN YOUR HAND (D325, Joe: *"holding down the middle
        // mouse button and drag the mouse left or right to rotate. full freedom."*). Handled
        // BEFORE the `Pressed: true` guard below, because a drag needs the RELEASE as much as the
        // press and that guard throws every release away.
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Middle } wheelClick)
        {
            _turningTheGhost = wheelClick.Pressed && _building is not null;
            return;
        }

        if (_turningTheGhost && @event is InputEventMouseMotion turn)
        {
            TurnTheGhostBy(turn.Relative.X, turn.ShiftPressed);
            return;
        }

        if (@event is InputEventMouseMotion motion && IsPlacing)
        {
            Vector2 tile = ToTile(motion.Position);
            var over = new GridPos(Mathf.RoundToInt(tile.X), Mathf.RoundToInt(tile.Y));

            // ⚠️ THE TILE GATE STAYS, AND A SECOND TEST RIDES BESIDE IT (D330). `over != _hovered`
            // is a recompute throttle: it exists so `CanBuildAt` and a redraw do not run on every
            // motion event. With snapping off the ghost moves *within* a tile, so it has to follow
            // the cursor — but only then, and only while a building is in hand. **The throttle is
            // kept where it still works rather than deleted because one case outgrew it.**
            SubTile overSub = SubTileUnderTheCursor(motion.Position);
            bool brushMoved = _brush != 0 && overSub != _hoveredSub;
            _hoveredSub = overSub;

            Point wouldStand = WhereItWouldStand(motion.Position);
            bool freelyMoved = !_snapToGrid && _building is not null && wouldStand != _hoveredPoint;

            // ⚠️ AND THE BRUSH MOVES A QUARTER-TILE AT A TIME NOW (D336), so the tile gate alone
            // would leave the preview four steps behind the cursor. *A throttle keyed on a coarser
            // grid than the thing it is throttling stops being a throttle and becomes a lag.*
            if (over != _hovered || freelyMoved || brushMoved)
            {
                _hovered = over;
                _hoveredPoint = wouldStand;
                if (_building is not null)
                {
                    // ⭐ AT THE ANGLE IN YOUR HAND (D328). The ghost has been drawn with
                    // `_ghostFacing` since D320 and the verdict was computed without it, so a
                    // turned longhouse could be shown green over ground the sim had never checked.
                    _verdict = _world.CanBuildAt(_building.Value, _hoveredPoint, facing: _ghostFacing);
                }

                // Drag to paint. A neighbourhood is a shape you draw, not a sequence of
                // clicks — the brush exists so that deciding where people live costs one
                // gesture rather than forty (D42).
                //
                // ⭐⭐ RIGHT-DRAG TAKES BACK (D327), and it is read from the mask rather than
                // held in a field: the `Pressed: true` guard below throws every RELEASE away, so
                // a field set on the press would never be cleared. *The event already knows.*
                if (_brush != 0 && motion.ButtonMask.HasFlag(MouseButtonMask.Right))
                {
                    PaintAround(_hoveredSub, -1);
                }
                else if (_brush != 0 && motion.ButtonMask.HasFlag(MouseButtonMask.Left))
                {
                    PaintAround(_hoveredSub, _brush);
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

        // ⭐⭐ RIGHT TAKES BACK WHILE A BRUSH IS HELD, AND STILL CANCELS OTHERWISE (D327, Joe's
        // call). **One sentence a player can be told: left paints, right takes back** — and it
        // does not depend on which brush button was last pressed. With an erase brush already in
        // hand both buttons erase, which is harmless; the alternative (*right does the opposite of
        // what is held*) makes the right button mean a different thing depending on state.
        //
        // ⛔ The cancel it displaces is `Escape`, bound in `Main._UnhandledKeyInput`, and the
        // brush's own announce sentence says so. **A gesture removed without a replacement named
        // in the same breath is a tool the player cannot put down.**
        if (_brush != 0 && click.ButtonIndex == MouseButton.Right)
        {
            PaintAround(SubTileUnderTheCursor(click.Position), -1);
            QueueRedraw();
            AcceptEvent();
            return;
        }

        if (IsPlacing && click.ButtonIndex == MouseButton.Right)
        {
            PutTheToolDown();
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

            // ⭐⭐ THE BUILDING UNDER THE POINT FIRST, THE TILE UNDER THE POINT SECOND
            // (D338, Joe: *"i should be able to click anywhere on the building to select it, but
            // there are areas of a building in which clicking selects a non-building tile even
            // though part of the building looks like it is in that spot."*).
            //
            // **He was describing the centre rule seen from the mouse.** D319 says a building
            // covers the tiles whose CENTRES it stands on — which is the right thing for
            // ownership and legibility — but a turned building is DRAWN over ground it does
            // not claim, and this line rounded the click to a tile before anything was asked
            // about buildings. *You could see the wall and click straight through it.*
            //
            // ⛔ **The fallback is load-bearing and this must not become an if/else on the
            // rectangle alone.** A 1×1 turned 45° between tile centres is drawn away from
            // the middle of its own anchor tile, and D331 deliberately lets that anchor be clicked
            // anyway. Asking the rectangle first and the tile second can only ADD a hit.
            Vector2 hit = ToTile(click.Position);
            GridPos tile = _world!.WhatStandsUnder(PointUnderTheCursor(click.Position))
                ?? new GridPos(Mathf.RoundToInt(hit.X), Mathf.RoundToInt(hit.Y));

            BuildingClicked?.Invoke(tile);
            AcceptEvent();
            return;
        }

        // ⚠️ Middle mouse turns the building in your hand (D325) and is handled at the top of
        // this method, above the `Pressed: true` guard, because a drag needs the release too.
        // *This comment said it was "deliberately unbound" for a stretch after it was bound.*
        if (click.ButtonIndex is not (MouseButton.WheelUp or MouseButton.WheelDown))
        {
            return;
        }

        // ⭐⭐ ALT+WHEEL SIZES THE BRUSH; THE PLAIN WHEEL ALWAYS ZOOMS (D327, Joe: *"i dont want the
        // brush sizing action to compete with zoom function. i find it confusing."*).
        // ⛔⛔ **IT WAS THE BARE WHEEL FOR ONE COMMIT AND THAT WAS THE WRONG CALL.** Overloading the
        // wheel on whether a brush happens to be in hand makes the *same gesture* do two things
        // depending on invisible state — and the player is holding a brush precisely when they are
        // most likely to want to zoom in and look. **A modifier is a promise that the plain gesture
        // never changes meaning**, which is what the zoom needs to be.
        // ⚠️ Still guarded on `_brush != 0` rather than `IsPlacing`: a building ghost has no size to
        // change, so alt+wheel over one falls through and zooms rather than doing nothing.
        if (_brush != 0 && click.AltPressed)
        {
            int wanted = _brushRadius + (click.ButtonIndex == MouseButton.WheelUp ? 1 : -1);
            _brushRadius = BrushStroke.ClampSub(wanted);

            Announce();
            QueueRedraw();
            BrushChanged?.Invoke();
            AcceptEvent();
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
    /// <summary>Paint or erase a brushful of ground.</summary>
    /// <remarks>
    /// <para>
    /// ⭐⭐ <b><paramref name="direction"/> COMES FROM THE GESTURE, NOT FROM THE TOOL</b> (D327).
    /// <b>Left paints, right takes back</b> — one sentence a player can be told, and it does not
    /// depend on which button they last pressed. <c>_brush</c> keeps its other job, *"is a brush in
    /// hand"*; it no longer decides which way this stroke goes.
    /// </para>
    /// <para>
    /// ⛔ <b>The shape is <see cref="BrushStroke"/>'s and this method does not own a loop any
    /// more.</b> It and <see cref="DrawTheBrushful"/> were a copy-paste of each other, comment
    /// block included — and the comment said *"a preview that disagrees with the paint is worse
    /// than no preview"* while nothing but discipline held them together.
    /// </para>
    /// </remarks>
    private void PaintAround(SubTile centre, int direction)
    {
        string? warning = null;
        int homesUnderTheBrush = 0;
        string? refused = null;

        // ⭐ THE STROKE IS SUB-TILES; THE VERDICTS ARE STILL TILES (D336). Whether ground may be
        // painted is a question about terrain, and terrain is tiled — *a quarter of a tile is not
        // under water on its own.* Only where the player chose to paint got finer.
        foreach (SubTile at in BrushStroke.SubTilesUnder(centre, _brushRadius, _brushShape))
        {
            GridPos tile = at.Tile;

            // Ground given to one building (D86). Same stroke shape as the others — one
            // sentence for the drag, never one per tile — and it stops rather than
            // half-painting if the hut went away mid-stroke.
            if (_groundFor != 0)
            {
                Workplace? owner = _world!.FindWorkplace(_groundFor);
                if (owner is null)
                {
                    // ⛔⛔ IT PUTS THE TOOL DOWN AND LEAVES THE STROKE, WHICH IS WHAT THE
                    // COMMENT ABOVE HAS ALWAYS CLAIMED IT DID. It used to clear `_groundFor`
                    // and `continue` — so every remaining tile of that stroke fell through
                    // to the residential arm below and **painted housing land the player
                    // never asked for**. It abandoned the tool and kept the brush.
                    PlacementMessageChanged?.Invoke(
                        "That building is gone, so there is nothing to give ground to.");
                    SetTool(MapTool.None);
                    return;
                }

                if (direction < 0)
                {
                    _world.EraseWorkGround(owner, at);
                    continue;
                }

                PlacementVerdict given = _world.PaintWorkGround(owner, at);
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
                if (direction < 0)
                {
                    _world!.EraseHarvest(at);
                    continue;
                }

                // Refusals are silent per tile and counted for the stroke: a drag
                // across mixed ground is MEANT to skip what the mode does not take,
                // and forty sentences would bury the one that matters (D42, D92).
                PlacementVerdict marked = _world!.PaintHarvest(at, _harvestMode.Value);
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

            if (direction < 0)
            {
                // ⭐⭐ ERASING OVER HOUSES IS A DEMOLITION ORDER NOW (Joe, 2026-08-26), and the
                // objection the sim used to make is answered here rather than argued away:
                // *"pulling houses down because somebody adjusted a brush would be a cruel
                // reading of an undo."* **True of an accident, false of an intent** — so the
                // stroke is counted, warned about, and takes a SECOND deliberate stroke.
                //
                // ⚠️ Armed per stroke rather than per tile, because a neighbourhood is erased
                // with one drag: warning once and requiring one confirmation is the shape D42
                // chose for painting and D221 for destroying a full store.
                if (_world!.HouseholdAt(tile) is not null)
                {
                    homesUnderTheBrush++;
                }

                _world.EraseResidential(tile);
                continue;
            }

            PlacementVerdict verdict = _world!.PaintResidential(at);
            if (verdict.HasWarning)
            {
                warning = verdict.Warning;
            }
        }

        // ⛔ THE SECOND STROKE IS THE CONSENT, AND THE WARNING HAS TO SAY WHERE THEY WOULD GO.
        // A family turned out in autumn with no painted ground left is a winter death caused by a
        // brush stroke — the unforeseeable punishment §2.1 and §0.1 both refuse. The village
        // already computes *"nowhere to build"*, so the sentence can be specific rather than
        // ominous.
        // ⛔⛔ THE CONFIRMATION IS GONE, AND DELETING IT IS THE FIX (Joe, playing: *"I tried to
        // 'take back' residential land that a house existed on and it wouldn't let me unpaint the
        // land."*). **It was written when unpainting LEVELLED a house on the spot** — the guard
        // against *"pulling houses down because somebody adjusted a brush would be a cruel reading
        // of an undo"*.
        //
        // ⭐ D230 MADE THAT IMPOSSIBLE ONE COMMIT LATER AND NOBODY NOTICED THE GATE HAD BECOME
        // REDUNDANT. Unpainting now only **marks**, and repainting **cancels** right up until the
        // first hammer swings — so a brush wobble already costs nothing, and the second stroke was
        // guarding against a thing that can no longer happen. *A safety built into the mechanism
        // does not also need a click.*
        //
        // ⚠️ The SENTENCE stays, because it is information the player wants — how many homes this
        // touches, and whether there is anywhere for those families to go. **It just is not a gate
        // any more.**
        if (homesUnderTheBrush > 0)
        {
            string homes = homesUnderTheBrush == 1 ? "1 household" : $"{homesUnderTheBrush} households";

            PlacementMessageChanged?.Invoke(_world!.NeedsMoreResidentialLand
                ? $"{homes} marked to come down, and there is no other painted ground for them to "
                    + "move to — paint somewhere else, or paint this back."
                : $"{homes} marked to come down; they will rebuild on ground you have painted "
                    + "elsewhere. Paint it back to call it off.");
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
            PaintAround(SubTileUnderTheCursor(at), _brush);
            QueueRedraw();
            return;
        }

        // ⭐ EMPTYING IS A TOGGLE, because the player may change their mind and a store that is
        // being cleared and one that is not are the same building in two moods.
        if (_emptying)
        {
            if (_world!.StoreAt(where) is not StoreBuilding store)
            {
                PlacementMessageChanged?.Invoke("That is not a store.");
                return;
            }

            store.Emptying = !store.Emptying;
            PlacementMessageChanged?.Invoke(store.Emptying
                ? $"{store.Name} is being cleared out — its {store.Store.Held} goods will be "
                    + "carried to the other stores."
                : $"{store.Name} is back in use.");

            QueueRedraw();
            return;
        }

        // ⭐ TWO CLICKS: what to move, then where to. The sim refuses anything it should refuse —
        // a house, a full store, an illegal tile — and says why, so this only has to carry the
        // question rather than duplicate the rules.
        if (_moving)
        {
            if (_moveFrom is not GridPos from)
            {
                if (_world!.WhatStandsAt(where) is null && _world.HouseholdAt(where) is null)
                {
                    PlacementMessageChanged?.Invoke("There is nothing there to move.");
                    return;
                }

                _moveFrom = where;
                PlacementMessageChanged?.Invoke(
                    $"Moving {_world.NameOnTheTile(where)} — click where it should stand.");
                QueueRedraw();
                return;
            }

            // ⭐ Where the cursor is, not the middle of the square under it (D330) — the same
            // freedom placing a building has, because putting one down and moving one are the
            // same act with a different starting point.
            PlacementVerdict moved = _world!.MarkRelocation(from, WhereItWouldStand(at));
            PlacementMessageChanged?.Invoke(moved.Allowed
                ? $"{_world.NameOnTheTile(where)} is being moved."
                : moved.Reason);

            // Only let go of the building once the move is actually under way, so a refused
            // destination leaves them still holding it rather than starting over.
            if (moved.Allowed)
            {
                _moveFrom = null;
            }

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
            // A CONSTRUCTION SITE IS CANCELLED, A STANDING BUILDING IS MARKED, and the two are
            // different acts. Calling off something nobody has finished is an undo and stays
            // instant; taking down something that stands is work, and since 2026-08-26 it is a
            // builder's job with a site of its own (Joe: "reverse-construction, essentially").
            // ⭐ One finder, then the question (D328). A standing building and a site cannot both
            // cover a tile — nothing may be raised on ground something already stands on — so the
            // first thing covering it is the thing this branch means.
            if (_world!.WorkplaceCovering(where) is { IsSite: true } workplace)
            {
                string name = workplace.Construction!.Name;

                // A demolition already under way is not cancellable once begun -- the sim
                // decides that, not this brush, so ask it rather than duplicating the rule.
                if (workplace.Construction.Demolishing)
                {
                    PlacementMessageChanged?.Invoke(_world.CancelDemolition(where)
                        ? $"{name} is to stand after all."
                        : $"{name} is already coming down; it is too late to stop it.");
                    QueueRedraw();
                    return;
                }

                _world.Demolish(workplace);
                PlacementMessageChanged?.Invoke($"{name} is gone.");
                QueueRedraw();
                return;
            }

            // Anything that STANDS -- a hut, a store, a library, a house -- is marked, and a
            // builder comes to it. One call for all four kinds, because the sim knows what is on
            // a tile and this brush should not have to.
            PlacementVerdict marked = _world.MarkDemolition(where);
            PlacementMessageChanged?.Invoke(marked.Allowed
                ? $"{_world.NameOnTheTile(where)} is marked to come down."
                : marked.Reason);

            QueueRedraw();
            return;
        }

        // ⭐ THE FACING GOES WITH IT (gridless 2b, D320). Turning the ghost and then placing a
        // building that faces north would be the feature existing everywhere except where the
        // player looked for it.
        // ⭐⭐ WHERE THE PLAYER PUT IT (gridless 2c, D330). `WhereItWouldStand` has already applied
        // the snap setting, so the sim is told a position it could have written itself and believes
        // exactly what is drawn. **The ghost and the building are the same geometry.**
        PlacementVerdict verdict = _world!.Mark(_building!.Value, WhereItWouldStand(at), _ghostFacing);
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
    private void Announce() => PlacementMessageChanged?.Invoke(TheSentenceForWhatIsHeld());

    /// <summary>
    /// <b>The sentence for whatever is in hand</b> — separated from saying it (D327).
    /// </summary>
    /// <remarks>
    /// ⭐ <b>So the width probe can pose every one of them.</b> `PinTheBarHeight` reserves the
    /// placement label at a bare newline rather than a real sentence, so a message that wraps at
    /// runtime grows the bar past its own pin and nothing catches it. **A list nobody can enumerate
    /// cannot be measured**, and these are a list — the sim's own refusals, which share the label,
    /// are not. *One condition, two callers, applied to a string.*
    /// </remarks>
    private string TheSentenceForWhatIsHeld()
    {
        // ⚠️ THE GROUND BRUSH FIRST, because it is a positive brush and the residential
        // wording below would otherwise claim it. Joe saw exactly that: pressing "Give ground"
        // announced *"drag to paint where the village may build homes"*, which is a sentence
        // about the wrong tool and made a working brush look broken.
        if (_groundFor != 0)
        {
            Workplace? owner = _world?.FindWorkplace(_groundFor);
            string whose = owner?.Name ?? "this building";

            return _brush < 0
                ? $"Drag to take ground back from {whose}{TheBrushKeys(erasing: true)}"
                : $"Drag to give ground to {whose}{TheBrushKeys(erasing: false)}";
        }

        if (_moving)
        {
            return "Click a building to move, then click where it should stand. A store must be "
                + "empty first, and houses move by the land brush. Right-click or Esc to stop.";
        }

        if (_emptying)
        {
            return "Click a store to have its goods carried out to the others, or click it again "
                + "to stop. Right-click or Esc to put the tool down.";
        }

        // ⛔⛔ THE HARVEST BRUSH HAD NO SENTENCE OF ITS OWN AND FELL THROUGH TO THE RESIDENTIAL
        // ONE (found while rewriting these, D327). Marking a wood for felling announced
        // *"Drag to paint where the village may build homes."* — **a sentence about the wrong
        // tool, over a tool that is not that one**, which is `build-bar.md §5.1` bug 1 arriving
        // from a third direction. It has to be tested BEFORE the `_brush` arms, for the same
        // reason the ground brush is: those arms are written as if residential were the only
        // brush, and they claim anything that reaches them.
        if (_harvestMode is not null)
        {
            return _brush < 0
                ? $"Drag to rub the marking out{TheBrushKeys(erasing: true)}"
                : $"Drag to mark {WhatTheHarvestBrushTakes()} to take"
                    + TheBrushKeys(erasing: false);
        }

        if (_brush > 0)
        {
            return $"Drag to paint where homes may stand{TheBrushKeys(erasing: false)}";
        }

        if (_brush < 0)
        {
            return $"Drag to take land back{TheBrushKeys(erasing: true)}";
        }

        if (_demolishing)
        {
            return "Click a building to pull it down. Right-click or Esc to stop.";
        }

        if (_building is null)
        {
            return string.Empty;
        }

        return _verdict switch
        {
            { Allowed: false } => _verdict.Reason,
            { HasWarning: true } => _verdict.Warning + TheMarketsServiceArea(),
            // ⭐ THE TURN KEYS LIVE HERE, NOT IN THE PERMANENT HINT LINE (D323). Adding
            // "(shift: quarter)" to the bar's footer wrapped it to another row — **measured at 181
            // tall against 161** — which would have spent map room Joe had twice asked to get back,
            // to explain a key that only matters while a building is in your hand. *A contextual
            // hint costs nothing when it is not needed.*
            // ⚠️ TRIMMED TO PAY FOR "or Esc" (D327). This is the longest sentence the label can be
            // given — it carries the market's service area on top — and the width probe measured it
            // at **1274px of the 1280 the window has**. Adding the new cancel to it without taking
            // something out would have wrapped it, which grows the bar past the single line
            // `PinTheBarHeight` reserves. *"shift for fine" → "shift: fine" is the cheapest three
            // words in the sentence.*
            // ⭐ AND WHICH WAY IT WILL LAND (D330). A player who has turned snapping off needs to
            // know it is off at the moment they are aiming, not by noticing later that a granary
            // sits half a tile out. *The contextual line again, for the same reason as the turn
            // keys: it costs nothing when it is not needed.*
            // ⚠️ AND THIS IS THE ONE SENTENCE WITH NO ROOM, WHICH IS WHY IT DROPS "Right-click or".
            // It is the only message that carries the market's service area on top of itself, so
            // it starts 47px from the edge where every other line has hundreds. **The probe posed
            // both modes and measured the free one at 1348 of 1280 — it would have wrapped, and
            // wrapping grows the bar past the single line `PinTheBarHeight` reserves.** Right-click
            // still cancels a held building; the sentence names the gesture that works for every
            // tool instead of both.
            _ => (_snapToGrid ? "Click to mark it out. " : "Free — click to mark it out. ")
                + "Middle-drag turns it (shift: fine), R steps. Esc to stop."
                + TheMarketsServiceArea(),
        };
    }

    /// <summary>
    /// ⭐ What every brush can do, in one clause — <b>written once because it is true of all four</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The size, the wheel and the right button are the same facts for the land brush, the harvest
    /// brush and the ground brush. Four copies of one clause is four places for it to go stale —
    /// which is exactly what happened to the shape comment this slice deleted.
    /// </para>
    /// <para>
    /// ⛔ <b>It lives here rather than in the bar's permanent hint line</b> (D323): adding one
    /// clause to that footer wrapped the bar to another row, **measured at 181 against 161**. *A
    /// contextual hint costs nothing when it is not needed.*
    /// </para>
    /// <para>
    /// ⚠️ <b>Every one of these sentences must stay on ONE line at 1280 logical pixels.</b>
    /// <c>PinTheBarHeight</c> reserves the placement label at a bare newline rather than posing a
    /// real sentence, so a wrapped message grows the bar past its own pin and nothing catches it.
    /// The width probe poses them (D327) — keep them there.
    /// </para>
    /// </remarks>
    private string TheBrushKeys(bool erasing) => erasing
        ? $" — {TheBrushInWords()}, alt+wheel resizes. Esc to stop."
        : $" — {TheBrushInWords()}, alt+wheel resizes, right-drag takes back. Esc to stop.";

    /// <summary>
    /// ⭐⭐ Every sentence the placement line can be given by a tool — <b>for the width probe</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔ <b>Posed and put back.</b> It sets the hand fields directly rather than going through
    /// <see cref="SetTool"/>, which would fire <see cref="ToolChanged"/> and relight the bar
    /// mid-measurement — and restores every one of them afterwards. *An instrument that leaves the
    /// game in a state it could not have reached on its own is how it starts lying about something
    /// else* (D326's fold probe, the same lesson).
    /// </para>
    /// <para>
    /// ⚠️ <b>It reads the real sentences rather than holding copies</b>, or the probe would be
    /// measuring text the game does not say — which is the whole failure mode it exists to catch.
    /// ⚠️ The sim's own refusals share this label and are NOT here: there is no list to take them
    /// from, which is the reason `PinTheBarHeight` reserves a placeholder in the first place.
    /// </para>
    /// </remarks>
    public IEnumerable<(string Tool, string Sentence)> EverySentenceAToolCanSay()
    {
        MapTool wasTool = Tool;
        BuildingKind? wasBuilding = _building;
        HarvestBrush? wasHarvest = _harvestMode;
        int wasGroundFor = _groundFor;
        int wasBrush = _brush;
        bool wasDemolishing = _demolishing;
        bool wasMoving = _moving;
        bool wasEmptying = _emptying;
        int wasRadius = _brushRadius;
        BrushShape wasShape = _brushShape;
        bool wasSnapping = _snapToGrid;

        var said = new List<(string, string)>();

        // ⛔ THE BIGGEST BRUSH AND THE LONGEST SHAPE WORD, because a sentence that fits at 5×5
        // square and wraps at 13×13 round is the "correct at startup, wrong later" fault every
        // other pose in this probe exists to refuse. **Measure the widest the player can reach.**
        // ⛔ THE BIGGEST BRUSH THE PLAYER CAN ACTUALLY REACH (D336). This posed `MaxRadius`, which
        // is the TILE-era ceiling and is now a smaller number in a different unit — so the probe
        // was measuring a brush four times narrower than the one a spun wheel produces. *An
        // instrument that keeps a constant after the constant changes meaning measures the past.*
        _brushRadius = BrushStroke.MaxSubRadius;
        _brushShape = BrushShape.Round;

        void Say(string tool)
        {
            said.Add((tool, TheSentenceForWhatIsHeld()));
        }

        Clear();
        _brush = 1;
        Say("paint land");

        _brush = -1;
        Say("take land");

        Clear();
        _brush = 1;
        _harvestMode = HarvestBrush.Everything;
        Say("harvest");

        _brush = -1;
        Say("unmark");

        // The longest workplace name the catalogue can produce, so the ground brush is posed at
        // its widest rather than at whatever happens to stand in this village.
        Clear();
        _brush = 1;
        _groundFor = LongestNamedWorkplace();
        Say("give ground");

        _brush = -1;
        Say("take ground");

        Clear();
        _demolishing = true;
        Say("demolish");

        Clear();
        _moving = true;
        Say("move");

        Clear();
        _emptying = true;
        Say("empty");

        // ⛔⛔ BOTH WAYS ROUND, BECAUSE THE FREE ONE IS LONGER AND IS NOT THE DEFAULT (D330).
        // The probe poses whatever state the map is in, and snapping starts ON — so measuring
        // once would have measured the SHORT sentence and reported 47px spare on a line that
        // wraps the moment somebody turns snapping off. *D242's rule, which this probe exists to
        // enforce: every look anybody takes at the UI is a look at the default state.*
        Clear();
        _building = BuildingKind.Market;
        _verdict = PlacementVerdict.Fine;
        _snapToGrid = true;
        Say("place");

        _snapToGrid = false;
        Say("place free");

        _building = wasBuilding;
        _harvestMode = wasHarvest;
        _groundFor = wasGroundFor;
        _brush = wasBrush;
        _demolishing = wasDemolishing;
        _moving = wasMoving;
        _emptying = wasEmptying;
        _brushRadius = wasRadius;
        _brushShape = wasShape;
        _snapToGrid = wasSnapping;
        Tool = wasTool;

        return said;

        void Clear()
        {
            _building = null;
            _harvestMode = null;
            _groundFor = 0;
            _brush = 0;
            _demolishing = false;
            _moving = false;
            _emptying = false;
        }
    }

    /// <summary>Which standing workplace has the longest name — the ground brush's worst case.</summary>
    private int LongestNamedWorkplace()
    {
        int id = 0;
        int longest = -1;

        foreach (Workplace place in _world?.Workplaces ?? [])
        {
            if (place.Name.Length > longest)
            {
                longest = place.Name.Length;
                id = place.Id;
            }
        }

        return id;
    }

    /// <summary>What the harvest brush is set to take, in the words the player chose it by.</summary>
    private string WhatTheHarvestBrushTakes() => _harvestMode switch
    {
        HarvestBrush.Trees => "trees",
        HarvestBrush.Stone => "stone",
        HarvestBrush.Iron => "iron",
        _ => "everything standing",
    };

    /// <summary>
    /// ⭐⭐ What a market here would actually serve (D201, Joe) — <b>a count, not a ring</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe asked to see the market's service area before placing it.</b> ⛔ <b>There is no
    /// radius to draw</b> — a marketer picks the cheapest errand from where they stand and
    /// households fetch from whatever store is nearest, so nothing in the model refuses a
    /// distance, and inventing a ring would rebuild the catchment fence D120 deleted.
    /// </para>
    /// <para>
    /// <b>⭐ The truthful answer is the homes this would be the CLOSEST food store for</b>, which
    /// is exactly the set whose walk it shortens — and it is not circular, because it depends on
    /// where the granary already is. **That is Joe's own point about positioning, made visible:**
    /// a market beside the granary reads *"0 homes"*, because the granary was already nearer.
    /// </para>
    /// <para>
    /// Empty for every other building, so it is information where it means something rather than
    /// a line the player learns to skip (D42).
    /// </para>
    /// </remarks>
    private string TheMarketsServiceArea()
    {
        if (_building != BuildingKind.Market || _world is null || !_world.Map.Contains(_hovered))
        {
            return string.Empty;
        }

        int homes = _world.HomesAMarketHereWouldBeNearestFor(_hovered);

        return homes == 0
            ? "  ⚠ No home would be closer to this than to a store they already use — "
                + "a market here shortens nobody's walk."
            : $"  {homes} {(homes == 1 ? "home is" : "homes are")} closer to this than to any "
                + "other food store.";
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

    /// <summary>
    /// Draw a building as the ground it actually stands on — turned (gridless 2b, D320).
    /// </summary>
    /// <remarks>
    /// ⛔ <b><c>DrawRect</c> cannot rotate</b>, which is why every building on this map was an
    /// axis-aligned square until now and a finished workplace was a bare circle — a shape with no
    /// direction to show. A quad over four turned corners can, and it is the same four corners the
    /// sim uses in <c>Footprint</c>.
    /// ⭐ <b>Floats and trigonometry are fine here.</b> This is presentation: it never feeds back
    /// into sim state, so <c>Mathf.Cos</c> is exactly the right tool where <c>Angle.Cos()</c> is the
    /// right one a layer down. *The boundary is the point, not the arithmetic.*
    /// </remarks>
    private void DrawFootprint(
        Vector2 centre, float widthTiles, float heightTiles, ushort facing, Color fill, Color edge)
    {
        Vector2[] quad = FootprintQuad(centre, widthTiles, heightTiles, facing);

        DrawColoredPolygon(quad, fill);

        for (int i = 0; i < 4; i++)
        {
            DrawLine(quad[i], quad[(i + 1) % 4], edge, 2f);
        }
    }

    /// <summary>
    /// The four turned corners of a building — or of a BAND across it (D324).
    /// </summary>
    /// <remarks>
    /// ⭐ <b>The band is what lets a construction site fill as it is built.</b> <c>from</c> and
    /// <c>to</c> run 0 at the building's own top edge to 1 at its own bottom — <em>its</em> frame,
    /// not the screen's — so a turned building fills along itself instead of being sliced
    /// horizontally by a fill that does not know it has been turned.
    /// </remarks>
    private Vector2[] FootprintQuad(
        Vector2 centre,
        float widthTiles,
        float heightTiles,
        ushort facing,
        float from = 0f,
        float to = 1f)
    {
        float radians = facing * Mathf.Tau / 65536f;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        float halfWidth = widthTiles * _pixelsPerTile / 2f;
        float halfHeight = heightTiles * _pixelsPerTile / 2f;

        float top = Mathf.Lerp(-halfHeight, halfHeight, from);
        float bottom = Mathf.Lerp(-halfHeight, halfHeight, to);

        Vector2 Corner(float x, float y) =>
            centre + new Vector2((x * cos) - (y * sin), (x * sin) + (y * cos));

        return new[]
        {
            Corner(-halfWidth, top),
            Corner(halfWidth, top),
            Corner(halfWidth, bottom),
            Corner(-halfWidth, bottom),
        };
    }

    private Vector2 ToScreen(Vector2 tile) => ((tile - _centreTile) * _pixelsPerTile) + (Size / 2f);

    private Vector2 ToScreen(GridPos tile) => ToScreen(new Vector2(tile.X, tile.Y));

    /// <summary>
    /// ⛔⛔ Where a building actually is, on screen — <b>and the half-tile seam lives here and
    /// nowhere else</b> (gridless 2c, D329).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>THE VIEW AND THE SIM DISAGREE ABOUT WHAT AN INTEGER TILE COORDINATE MEANS, AND BOTH ARE
    /// INTERNALLY CONSISTENT.</b> This view has always drawn tile <c>(x, y)</c> <em>centred</em> on
    /// <c>ToScreen(x, y)</c> — which is why the valley border and the grid lines are drawn at
    /// <c>−0.5</c>. The sim says tile <c>(x, y)</c> covers <c>[x, x+1)</c> and its centre is
    /// <c>(x+½, y+½)</c> (`Point.cs`), which is what makes <c>ToTile</c> a floor.
    /// </para>
    /// <para>
    /// ⛔ <b>So a <see cref="Point"/> must lose half a tile on each axis to land where the tile
    /// grid draws it.</b> Get this wrong and **every building on the map shifts by half a tile**,
    /// which reads as a drawing bug rather than as a units bug. *One conversion, one place, one
    /// comment — the alternative was moving the view's convention, which would have touched
    /// terrain, soil, the grid lines and the minimap to save this subtraction.*
    /// </para>
    /// <para>
    /// ⚠️ <b>The float appears HERE and never travels the other way.</b> Fixed-point to float is
    /// safe — this is drawing — but float into sim state is D2's ban, so the input path builds its
    /// <see cref="Point"/> from an exact rational instead (see <c>PointUnderTheCursor</c>).
    /// </para>
    /// </remarks>
    private Vector2 ToScreen(Point at) =>
        ToScreen(new Vector2(InTiles(at.X) - 0.5f, InTiles(at.Y) - 0.5f));

    /// <summary>
    /// ⛔⛔ A corner the tracer produced, brought back to tile space — <b>and every one
    /// of the three call sites had it wrong, which is why the riverbank wandered</b> (D338).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe, from play: *"the riverbank looks pretty questionable."*</b> It was, and so were the
    /// zone borders and the brush outline beside it, for one reason spelled three times.
    /// </para>
    /// <para>
    /// ⭐⭐ <b>WHAT THE TRACER ACTUALLY EMITS.</b> <see cref="ZoneOutline"/> works in
    /// doubled coordinates — a cell's left edge is <c>(x * 2) - 1</c> — and halves them on
    /// the way out, so <b>a corner comes back in CELL units, where cell <c>c</c> spans
    /// <c>[c - 0.5, c + 0.5]</c></b>. **The half-tile is already in it.** All three call sites
    /// subtracted another one: the shore came out half a tile up and to the left of its own water,
    /// and the zone and brush borders an eighth of a tile inside their own wash.
    /// </para>
    /// <para>
    /// ⭐ <b>One formula for all three, and it is the identity when a cell IS a tile.</b> Cell
    /// <c>c</c>'s left edge must land where the rectangle drawn for it starts —
    /// <c>c / cellsPerTile - 0.5</c>, which is what <see cref="SubTileRect"/> and
    /// <see cref="TileRect"/> use — so the traced value is shifted by half a cell before
    /// scaling and the view's half-tile comes off after. *At <paramref name="cellsPerTile"/> 1 the
    /// two halves cancel exactly, which is why the shoreline's extra subtraction was a whole half
    /// tile and visible from across the valley while the zones' was an eighth and merely wrong.*
    /// </para>
    /// <para>
    /// ⚠️ <b>The tracer does not know which grid it was handed and should not.</b> It is
    /// fed tiles by the shoreline and sub-tiles by the zones and the brush; the caller says which,
    /// here, once. **The probe checks this against the rectangles rather than trusting the
    /// arithmetic** — <c>ZoneOutline.SelfCheck</c> passed throughout all three bugs, because
    /// it only ever tested the tracer.
    /// </para>
    /// </remarks>
    private static Vector2 InTileSpace(Vector2 traced, int cellsPerTile) =>
        ((traced + new Vector2(0.5f, 0.5f)) / cellsPerTile) - new Vector2(0.5f, 0.5f);

    /// <summary>One sub-tile's rectangle, snapped to whole pixels for the same reason (D336).</summary>
    private Rect2 SubTileRect(SubTile at)
    {
        float size = 1f / SubTile.PerTile;
        float left = (at.X * size) - 0.5f;
        float top = (at.Y * size) - 0.5f;

        Vector2 topLeft = ToScreen(new Vector2(left, top)).Round();
        Vector2 bottomRight = ToScreen(new Vector2(left + size, top + size)).Round();

        return new Rect2(topLeft, bottomRight - topLeft);
    }

    /// <summary>
    /// ⛔⛔ One tile's rectangle, snapped to whole pixels — <b>and this is what stopped the painted
    /// ground drawing its own grid</b> (D333).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe, with the grid lines switched OFF:</b> *"even with the grid settings toggled off, the
    /// painted home area shows a grid."* **It was not the grid lines.** Every per-tile wash drew its
    /// rect **2% oversized** — a trick that exists to hide sub-pixel seams between *opaque* terrain
    /// squares at fractional zoom, and which is exactly wrong for a translucent one: **the 2% band
    /// where two rects overlap gets the colour blended TWICE.** Residential land is alpha 0.14, so
    /// every tile boundary came out at 0.26 — nearly double — in a thin line. *The overdraw that
    /// hides a grid in opaque paint draws one in translucent paint.*
    /// </para>
    /// <para>
    /// ⭐ <b>Snapping to whole pixels is the fix for both failures at once.</b> A tile's right edge
    /// and its neighbour's left edge are the same coordinate, so rounding them lands them on the
    /// same pixel: **no overlap to blend twice, and no gap to show through.** *Exact tiling beats
    /// a fudge in either direction.*
    /// </para>
    /// <para>
    /// ⚠️ <b>Terrain keeps its overdraw and should.</b> It is opaque, so drawing a band of it twice
    /// is invisible — and the comment there records that it was written for the river, where a
    /// one-pixel gap *"reads as a bug rather than as a river"*.
    /// </para>
    /// </remarks>
    private Rect2 TileRect(GridPos tile)
    {
        Vector2 topLeft = ToScreen(new Vector2(tile.X - 0.5f, tile.Y - 0.5f)).Round();
        Vector2 bottomRight = ToScreen(new Vector2(tile.X + 0.5f, tile.Y + 0.5f)).Round();

        return new Rect2(topLeft, bottomRight - topLeft);
    }

    /// <summary>A fixed-point value as a float number of tiles. Drawing only.</summary>
    private static float InTiles(Fixed value) =>
        (float)(value.RawBits / 4294967296.0);

    /// <summary>
    /// ⛔⛔ Does a tile's CENTRE draw where the tile draws? — for the width probe (D329).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The half-tile seam is the one thing in this slice that no test can reach and that fails
    /// silently.</b> If <see cref="ToScreen(Point)"/> loses its offset, every building on the map
    /// moves half a tile down and right — a change that looks like a drawing bug, months after
    /// anybody remembers there were two conventions. The view has no automated verification at all
    /// (D11, D160), so the probe is the only instrument there is, and this is the question to ask
    /// it: <c>ToScreen(Point.CentreOf(t))</c> must land exactly where <c>ToScreen(t)</c> lands.
    /// </para>
    /// <para>
    /// ⚠️ <b>Several tiles, spread out and including negatives</b>, because the valley straddles
    /// its own founding site — an offset that is right at the origin and wrong elsewhere is a
    /// scaling bug rather than a translation bug, and one sample cannot tell them apart.
    /// </para>
    /// </remarks>
    public string TheCentreOfATileDrawsWhereTheTileDoes()
    {
        float worst = 0f;
        GridPos where = default;

        foreach (GridPos tile in new[]
        {
            new GridPos(0, 0), new GridPos(1, 0), new GridPos(0, 1),
            new GridPos(-7, -3), new GridPos(40, 31), new GridPos(-40, 62),
        })
        {
            float off = ToScreen(Point.CentreOf(tile)).DistanceTo(ToScreen(tile));
            if (off > worst)
            {
                worst = off;
                where = tile;
            }
        }

        return worst <= 0.001f
            ? $"[widths] tile centres: ✅ a point at a tile's centre draws on the tile, "
                + $"worst {worst:F4}px"
            : $"[widths] tile centres: ⛔ {worst:F2}px adrift at {where} — every building on the "
                + $"map is off by {worst / Mathf.Max(1f, _pixelsPerTile):F2} of a tile";
    }

    /// <summary>
    /// ⛔⛔ A traced outline lands on the rectangle it was traced from — <b>the guard
    /// that was missing while the riverbank was half a tile out</b> (D338).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe: *"the riverbank looks pretty questionable."*</b> It was drawn half a tile up and to
    /// the left of its own water, the zone borders an eighth of a tile inside their own wash, and
    /// **<c>ZoneOutline.SelfCheck</c> was green through all of it** — because it checks that
    /// the tracer closes its loops and keeps their area, which it always did. *The bug was not in
    /// the tracer. It was in what three call sites believed the tracer's units were.*
    /// </para>
    /// <para>
    /// ⭐ <b>So this checks the seam rather than either side of it.</b> Trace one lone cell,
    /// convert its corners the way the drawing code does, and require them to land on the corners
    /// of the rectangle the fill draws for that same cell — <see cref="TileRect"/> at one cell
    /// per tile, <see cref="SubTileRect"/> at four. **If the outline and the fill disagree about
    /// where a cell is, one of them is lying and the player can see it.**
    /// </para>
    /// <para>
    /// ⚠️ <b>Both grids, because the two bugs were different sizes.</b> At one cell per
    /// tile the error was a whole half tile and obvious; at four it was an eighth and merely wrong.
    /// A guard posed at only one of them would have found only one of them — *the
    /// pose-the-wrong-state family again (D242, D326, D332, D336).*
    /// </para>
    /// <para>
    /// ⛔ <b>IT IS THE BOUNDING BOX, NOT THE CORNERS, AND THE FIRST VERSION GOT THAT WRONG.</b>
    /// It required every traced point to be a corner of the rectangle and **scored 24px on correct
    /// code** — because <see cref="ZoneOutline"/> cuts corners, so a lone cell comes back as a
    /// rounded octagon whose points sit along the edges rather than at their ends. *Measure the
    /// thing that is actually invariant.* **The extremes survive the smoothing** — the midpoint
    /// of each side is never moved off it — so the box the outline occupies is exactly the box
    /// the fill draws, and a translation error of any size breaks that.
    /// </para>
    /// </remarks>
    public string ATracedOutlineLandsOnItsOwnRectangle()
    {
        float worst = 0f;
        string where = "nowhere";

        // Away from the origin on purpose: an error that is right at (0,0) and wrong elsewhere is
        // a scaling bug rather than a translation bug, and the origin cannot tell them apart.
        Check("tiles", 1, TileRect(new GridPos(6, -4)), new Vector2I(6, -4));
        Check("sub-tiles", SubTile.PerTile, SubTileRect(new SubTile(25, -15)), new Vector2I(25, -15));

        return worst <= 1f
            ? $"[widths] outline seam: ✅ outline meets fill, worst {worst:F2}px at {where}"
            : $"[widths] outline seam: ⛔ {worst:F2}px adrift at {where} — that is "
                + $"{worst / Mathf.Max(1f, _pixelsPerTile):F2} of a tile between a border and the "
                + "ground it is supposed to be the border of";

        void Check(string grid, int cellsPerTile, Rect2 fill, Vector2I cell)
        {
            var one = new HashSet<Vector2I> { cell };
            var box = new Rect2();
            bool started = false;

            foreach (Vector2[] loop in ZoneOutline.Trace(one))
            {
                for (int p = 0; p < loop.Length; p++)
                {
                    Vector2 drawn = ToScreen(InTileSpace(loop[p], cellsPerTile));

                    box = started ? box.Expand(drawn) : new Rect2(drawn, Vector2.Zero);
                    started = true;
                }
            }

            if (!started)
            {
                worst = float.PositiveInfinity;
                where = grid + " (traced nothing at all)";
                return;
            }

            Vector2 topLeft = (box.Position - fill.Position).Abs();
            Vector2 bottomRight = (box.End - fill.End).Abs();

            float off = Mathf.Max(
                Mathf.Max(topLeft.X, topLeft.Y), Mathf.Max(bottomRight.X, bottomRight.Y));

            if (off > worst)
            {
                worst = off;
                where = grid;
            }
        }
    }

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

        // ⭐ WHAT LIVES AND GROWS SITS ON THE GROUND, UNDER EVERYTHING THE PLAYER ACTS ON. Animals
        // move, so the rule below would argue for drawing them late — but that rule is about
        // things the player needs to find, and these are scenery. A deer must never be
        // mistaken for a villager or hide a building, so it goes down with the terrain.
        DrawTheWoods();

        // Routes under everything, then workplaces, then homes, then people on top —
        // the things that move must never be hidden behind the things that do not.
        DrawRoutes();
        DrawWorkplaces();
        DrawStores();
        DrawLibraries();
        DrawTheTownHall();
        DrawHomes();

        // ⚠️ Over the buildings, because a heap beside a full warehouse is the whole point:
        // the overview says *"Stone 0 (+12 on the ground — no room in store)"* and until now
        // there was nowhere on screen to find the twelve.
        DrawHeaps();

        // Over the buildings so it is not hidden by one, under the people so it never
        // hides them — the same rule the rest of this method follows.
        DrawSelectedTile();
        DrawVillagers();

        // The ghost last, over everything, because it is the thing being decided.
        DrawTheGhost();
        DrawTheBrushful();
    }

    /// <summary>
    /// ⭐⭐ The brushful about to be laid down, under the cursor (D198, Joe).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe, playing:</b> *"when I'm painting I don't see an outline of the area I'm about to
    /// paint. I just have to point and click and hope it's covering the right area."* A building
    /// has had a ghost since D43; **the brush — which puts down twelve tiles at a time — had
    /// nothing at all.**
    /// </para>
    /// <para>
    /// <b>⭐ IT SHOWS WHAT WOULD HAPPEN, NOT WHERE THE BRUSH IS.</b> A plain outline would be a
    /// rectangle; this asks the sim tile by tile and colours each one by the answer, so the
    /// harvest brush's filter (D90) — *"the brush is set to fell trees and that is a stone
    /// seam"* — is visible **before** the click rather than discovered by it. That is §1.1
    /// applied to the one tool that had escaped it.
    /// </para>
    /// <para>
    /// <b>Asked through the same doors the paint uses</b> — <c>CanPaintResidential</c>,
    /// <c>CanPaintHarvest</c>, <c>CanPaintWorkGround</c> — which is why those were split out of
    /// the paint methods (D142's rule): a preview computed from a second copy of the condition is
    /// a preview that can lie.
    /// </para>
    /// <para>
    /// ⛔⛔ <b>THE SHAPE IS <see cref="BrushStroke"/>'S, AND NEITHER THIS METHOD NOR
    /// <see cref="PaintAround"/> OWNS A LOOP ANY MORE (D327).</b> This doc-comment used to say
    /// *"The diamond, not a square"* — three lines above an inline comment reading
    /// <b>SQUARE, NOT A DIAMOND</b>, which is what the code actually did. **Both loops carried the
    /// same pasted comment block warning that they had to change together, and one of the two
    /// copies had already gone stale.** *That is the argument for one function, made by the code
    /// itself.*
    /// </para>
    /// <para>
    /// ⭐ <b>And it follows the GESTURE, not the tool</b> — during a right-drag the preview shows
    /// the erase colour, because the promise is being tested in exactly that moment.
    /// </para>
    /// </remarks>
    private void DrawTheBrushful()
    {
        if (_brush == 0 || _world is null || !_world.Map.Contains(_hovered))
        {
            return;
        }

        int direction = TheStrokeInProgress();
        var under = new HashSet<Vector2I>();

        // ⛔ THE FILL STAYS PER TILE AND MUST. Each tile is coloured by what the sim says about
        // THAT tile (D198) — the harvest brush's filter means a drag across mixed ground is green
        // on the trees and red on the stone, and one colour for the whole brushful would be a
        // preview that tells the player less than the click will.
        foreach (SubTile at in BrushStroke.SubTilesUnder(_hoveredSub, _brushRadius, _brushShape))
        {
            if (!_world.Map.Contains(at.Tile))
            {
                continue;
            }

            // ⛔ Snapped, not oversized (D333). A translucent rect drawn 2% wide blends twice
            // where it laps its neighbour, which draws a grid inside the brushful.
            // ⭐ The COLOUR is still asked of the tile (D336): the sim's refusals are about terrain
            // and terrain is tiled. Only where the paint lands got finer.
            DrawRect(SubTileRect(at), ColourForTheBrushOn(at.Tile, direction) with { A = 0.26f });
            under.Add(new Vector2I(at.X, at.Y));
        }

        // ⭐⭐ AND ONE OUTLINE ROUND THE LOT (D332, Joe: *"why isnt the paint brush a smooth
        // circle?"*). It was twenty-five separately outlined boxes, which drew the GRID rather than
        // the brush — every internal edge was a line saying nothing, because the tile boundaries
        // inside a brushful are not a thing the player is choosing. **The shape they are aiming is
        // its border.**
        // ⚠️ Traced from `BrushStroke.TilesUnder`, so the outline is the paint: one shape function
        // decides what lands (D327) and this draws a picture of that answer rather than a second
        // opinion about it. *A round brush now looks round.*
        float thickness = Mathf.Max(1.5f, _pixelsPerTile * 0.06f);
        foreach (Vector2[] loop in ZoneOutline.Trace(under, SubTile.PerTile))
        {
            var onScreen = new Vector2[loop.Length];
            for (int p = 0; p < loop.Length; p++)
            {
                onScreen[p] = ToScreen(InTileSpace(loop[p], SubTile.PerTile));
            }

            DrawPolyline(onScreen, BrushEdgeFor(direction), thickness, antialiased: true);
        }
    }

    /// <summary>The colour of the brush's own border — what the stroke as a whole would do.</summary>
    /// <remarks>
    /// ⚠️ <b>The border is about the GESTURE, the fill is about each TILE.</b> They are allowed to
    /// disagree, and that is the useful part: an amber outline over a mix of green and red tiles
    /// says *"this takes back"* while the tiles still say which of them have anything to take.
    /// </remarks>
    private Color BrushEdgeFor(int direction) =>
        direction < 0 ? GhostWarned with { A = 0.85f } : GhostFine with { A = 0.85f };

    /// <summary>Which way the stroke under the cursor would go, right now (D327).</summary>
    /// <remarks>
    /// ⭐ <b>The right button outranks the tool</b>, because right always takes back. With no
    /// button down this is simply the held brush, which is what the preview shows while the player
    /// is only hovering.
    /// </remarks>
    private int TheStrokeInProgress() =>
        Input.IsMouseButtonPressed(MouseButton.Right) ? -1 : _brush;

    /// <summary>What the brush would do to this tile, as a colour.</summary>
    /// <remarks>
    /// <b>Erasing is never refused</b>, so it is drawn plain: a rubber that showed red over a
    /// tile with no paint on it would be telling the player they had done something wrong when
    /// they had not.
    /// </remarks>
    private Color ColourForTheBrushOn(GridPos tile, int direction)
    {
        SimWorld world = _world!;

        if (direction < 0)
        {
            return GhostWarned;
        }

        PlacementVerdict verdict;

        if (_groundFor != 0)
        {
            Workplace? owner = world.FindWorkplace(_groundFor);
            verdict = owner is null
                ? PlacementVerdict.No("That building is gone.")
                : world.CanPaintWorkGround(owner, tile);
        }
        else if (_harvestMode is not null)
        {
            verdict = world.CanPaintHarvest(tile, _harvestMode.Value);
        }
        else
        {
            verdict = world.CanPaintResidential(tile);
        }

        return verdict switch
        {
            { Allowed: false } => GhostRefused,
            { HasWarning: true } => GhostWarned,
            _ => GhostFine,
        };
    }

    /// <summary>
    /// The building about to be placed, under the cursor, coloured by what the sim says.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three colours for three answers, and the middle one is the point (D43): green is
    /// fine, <b>amber is allowed but unwise</b>, red is impossible. The player may build
    /// on amber. The words alongside say why it is amber, because a colour on its own
    /// is the shrug this project keeps refusing.
    /// </para>
    /// <para>
    /// ⚠️ <b>This comment spent a stretch stranded above <see cref="DrawTheBrushful"/></b>, whose
    /// own summary followed it immediately — so the compiler bound the second one and this said
    /// nothing about anything. Restored to the method it describes (D327).
    /// </para>
    /// </remarks>
    private void DrawTheGhost()
    {
        if (_building is null)
        {
            return;
        }

        // ⛔ THE COMFORTABLE-WALK RING IS GONE (Joe, 2026-08-25): *"remove the circle from the
        // UI. If there can't be a radius for the market service area, then why does this exist?
        // It means nothing helpful to the user."*
        //
        // ⭐ THAT IS D201'S ARGUMENT, APPLIED WHERE IT WAS FIRST WRITTEN. The market's service
        // area was refused a ring because a ring would have LIED -- it is a count of households
        // reached by travel cost, not a distance, and water or a hill makes the same radius mean
        // different things in different directions. **`MaxHomeToVillageTiles` is the same kind of
        // number**: the warning it feeds is measured in TRAVEL COST round a river, and a perfect
        // circle drawn on the map claims a straight-line distance the sim never uses.
        //
        // It was added so a hidden constant would stop being undiscoverable, and that problem is
        // real -- but the sentence under the build menu already says it in the units the sim
        // actually measures: *"That is 9 tiles from the village; it budgets 8."* One true sentence
        // beats one approximate picture.
        SimWorld world = _world!;

        if (!world.Map.Contains(_hovered))
        {
            return;
        }

        Vector2 centre = ToScreen(_hoveredPoint);

        Color colour = _verdict switch
        {
            { Allowed: false } => GhostRefused,
            { HasWarning: true } => GhostWarned,
            _ => GhostFine,
        };

        // ⭐⭐ THE GHOST SHOWS THE GROUND, WHICH IS THE WHOLE POINT OF A PREVIEW (gridless 2b, D320).
        // It drew a fixed 0.9-tile square for every building, which was honest while every building
        // was one tile and becomes a lie the moment one is three. **This is the only place the
        // player ever sees a footprint before committing to it**, so it is the one that had to
        // learn the extent first.
        // ⭐ Through the sim's own helper, so the ghost cannot disagree with what placement will
        // actually refuse (D321). Two ways of asking "how big is this building?" is how a preview
        // starts lying.
        Footprint shape = world.FootprintOf(_building.Value, _hoveredPoint, _ghostFacing);
        float wide = shape.Width * 0.9f;
        float deep = shape.Height * 0.9f;

        // ⭐⭐ AND THE TILES IT WILL CLAIM, UNDER IT (D330, Joe's call). **D319's rule is that a
        // building covers the tiles whose CENTRES it stands on** — which was invisible while every
        // building was 1×1 on a grid, because a 1×1 always covered its own tile at every one of
        // 65,536 angles. **Free placement is exactly when it stops being obvious**: a nudge of half
        // a tile changes which ground the building takes, and without this the player would have no
        // way to see why. *§1.1 is the game explaining itself, and this is the moment it has to.*
        // ⚠️ Drawn UNDER the rectangle and fainter, so the building is still the thing you are
        // aiming and the coverage is the consequence you are being shown.
        foreach (GridPos claimed in shape.CoveredTiles())
        {
            if (!world.Map.Contains(claimed))
            {
                continue;
            }

            float size = _pixelsPerTile * 0.94f;
            Vector2 at = ToScreen(claimed);
            DrawRect(
                new Rect2(at - (Vector2.One * size / 2f), Vector2.One * size),
                colour with { A = 0.16f });
        }

        DrawFootprint(centre, wide, deep, _ghostFacing.Raw, colour with { A = 0.35f }, colour);

        DrawTheHomesThisMarketWouldServe();
    }

    /// <summary>
    /// ⭐ Ring the homes a market here would be the nearest food store for (D201).
    /// </summary>
    /// <remarks>
    /// <b>The count in the placement line says how many; this says which.</b> A number tells the
    /// player whether to move the building, and the rings tell them <em>which way</em> — which is
    /// the difference between a stat and a decision (§1.1). Drawn only while a market is on the
    /// cursor, because it is a placement aid rather than furniture.
    /// </remarks>
    private void DrawTheHomesThisMarketWouldServe()
    {
        if (_building != BuildingKind.Market || _world is null)
        {
            return;
        }

        SimWorld world = _world;
        float radius = Mathf.Max(4f, _pixelsPerTile * 0.55f);

        foreach (Household household in world.Households)
        {
            if (household.HomeTile is not GridPos home
                || world.LivingMembersOf(household) == 0)
            {
                continue;
            }

            // Asked one home at a time, off the same method the placement line counts with, so
            // the rings and the number can never disagree (D142, D147's rule for `IdleNote`).
            if (world.HomesAMarketHereWouldBeNearestFor(_hovered) == 0)
            {
                return;
            }

            int here = world.TravelCost.Cost(home, _hovered);
            if (here == TravelCostField.Unreachable || !IsNearestFoodStore(world, home, here))
            {
                continue;
            }

            DrawArc(ToScreen(home), radius, 0f, Mathf.Tau, 24, GhostFine with { A = 0.9f }, 2f);
        }
    }

    /// <summary>Whether a store at the cursor would beat every food store this home can reach.</summary>
    private static bool IsNearestFoodStore(SimWorld world, GridPos home, int here)
    {
        foreach (StoreBuilding store in world.StoreBuildings)
        {
            // The capability, not the good — `SimWorld.CanEverHoldFood` exists for exactly
            // this and its own remarks claimed all three sites had been converted. This was
            // the third.
            if (!world.CanEverHoldFood(store))
            {
                continue;
            }

            int theirs = world.TravelCost.Cost(home, store.Tile);
            if (theirs != TravelCostField.Unreachable && theirs <= here)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The granary and the materials warehouse.
    /// </summary>
    /// <remarks>
    /// Drawn as squares like homes, because they are the same sort of thing — a place
    /// that holds goods — and a different shape would imply a distinction that is not
    /// there. Distinguished by colour, and outlined so a store never reads as just
    /// another house: "why is nobody fetching food?" has to be answerable by looking.
    /// </remarks>
    /// <summary>
    /// ⭐⭐ An outline around whatever the player last clicked — <b>the BUILDING's
    /// shape when there is one, the tile's when there is not</b> (D341).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe, from play: *"clicking a rotated building selects it as expected, but it looks off
    /// visually, because the building's square is outlined, which doesn't align with the building
    /// itself."*</b> D339 taught the CLICK about rectangles and left the HIGHLIGHT drawing a
    /// one-tile axis-aligned square at the tile's centre — so selecting a turned building
    /// drew a box at the wrong angle, the wrong size, and the wrong place, all three at once.
    /// *Half a fix reads worse than none, because it puts the mismatch on screen.*
    /// </para>
    /// <para>
    /// ⭐ <b>The same <see cref="FootprintQuad"/> the building itself is drawn from</b>, so
    /// the outline cannot disagree with the thing it is outlining — it is not a second
    /// opinion about where the building is, it is the same answer.
    /// </para>
    /// <para>
    /// ⚠️ <b>Bare ground keeps the square, and that is not a leftover.</b> Clicking
    /// grass selects a TILE and still has to say which one; a tile is square and one tile across.
    /// *The square was only ever wrong because it was being used for both questions.*
    /// </para>
    /// <para>
    /// A square rather than the ring villagers get, so the two selections never read as the same
    /// thing: people are round on this map and buildings are not.
    /// </para>
    /// </remarks>
    private void DrawSelectedTile()
    {
        if (_selectedTile is not GridPos tile)
        {
            return;
        }

        if (_world!.FootprintOn(tile) is Footprint standing)
        {
            // A hair proud of the building, so the outline reads as around it rather than as part
            // of it — the same 0.92-of-a-tile inset the plain square has always had, applied
            // the other way because a building is drawn at 0.8 of its extent.
            Vector2[] quad = FootprintQuad(
                ToScreen(standing.Origin),
                standing.Width * 0.92f,
                standing.Height * 0.92f,
                standing.Facing.Raw);

            for (int i = 0; i < 4; i++)
            {
                DrawLine(quad[i], quad[(i + 1) % 4], SelectedRing, 2f);
            }

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

    /// <summary>Draw the libraries, which are neither stores nor workplaces.</summary>
    /// <remarks>
    /// <b>⛔ ITS ABSENCE IS WHAT JOE HIT:</b> *"it was constructed as buildings usually are, but no
    /// final building showed up upon completion. Just an open ground tile."* The sim had the
    /// building the whole time — it is in <c>SimWorld.Libraries</c>, and **this view knew about
    /// exactly two lists.**
    /// </remarks>
    private void DrawLibraries()
    {
        SimWorld world = _world!;

        for (int i = 0; i < world.Libraries.Count; i++)
        {
            Library library = world.Libraries[i];

            Vector2 centre = ToScreen(library.Position);
            float size = Mathf.Max(8f, _pixelsPerTile * 0.8f);
            // ⭐ Through the footprint like every other building (D325), so a multi-tile library
            // works the day a modder types one rather than being the class that ignores its own
            // `extent` column. One tile today, so this draws exactly what the rect did.
            DrawFootprint(
                centre,
                library.ExtentWidth * 0.8f,
                library.ExtentHeight * 0.8f,
                library.Facing.Raw,
                LibraryColour with { A = 0.85f },
                LibraryColour);

            // ⭐ A FULL LIBRARY SAYS SO ON THE MAP, for the same reason a full store does (D140):
            // the consequence of a full shelf is a technique dying with somebody years from now,
            // and the player should not have to click to find that out. Same ring, same reason —
            // *this building is the cause of the thing you are about to be annoyed by.*
            if (!library.HasRoom)
            {
                DrawArc(centre, size * 0.85f, 0f, Mathf.Tau, 24, LibraryColour, width: 2f);
            }
        }
    }

    /// <summary>The founders' hall, if one stands (D252).</summary>
    /// <remarks>
    /// <b>⛔ DRAWN IN THE SAME COMMIT AS THE FEATURE</b>, which is the rule seven features in this
    /// repo have broken — the library itself shipped built, tested and <em>invisible</em>. **A sim
    /// feature is not done until something in the view calls it, and no test in this suite can tell
    /// you that.**
    /// ⭐ <b>Slightly larger than everything else</b>, deliberately: it is the one building the
    /// village raised for a reason other than living, and it should read as the middle of the town
    /// from across the map.
    /// </remarks>
    private void DrawTheTownHall()
    {
        if (_world!.TownHall is not { } hall)
        {
            return;
        }

        Vector2 centre = ToScreen(hall.Position);
        float size = Mathf.Max(9f, _pixelsPerTile * 0.95f);
        // ⭐ Through the footprint like every other building (D325).
        DrawFootprint(
            centre,
            hall.ExtentWidth * 0.95f,
            hall.ExtentHeight * 0.95f,
            hall.Facing.Raw,
            TownHallColour with { A = 0.9f },
            TownHallColour);
    }

    private void DrawStores()
    {
        SimWorld world = _world!;

        for (int i = 0; i < world.StoreBuildings.Count; i++)
        {
            StoreBuilding building = world.StoreBuildings[i];

            Vector2 centre = ToScreen(building.Position);
            float size = Mathf.Max(8f, _pixelsPerTile * 0.8f);
            Color colour = building.Kind switch
            {
                StoreKind.Granary => GranaryColour,
                StoreKind.Warehouse => WarehouseColour,
                _ => MarketColour,
            };

            // ⛔⛔ THE LONGHOUSE DREW AS ONE SQUARE, AND THIS IS WHERE (D321, Joe: *"that's what a
            // built longhouse looks like. 1 square."*). A `Rect2` through `DrawRect` can neither
            // rotate nor stretch, so a three-tile storehouse and a one-tile granary drew
            // identically — while the GHOST of the same building drew correctly at 3×1, because
            // the ghost path already went through `DrawFootprint`.
            // ⚠️ THE DATA WAS RIGHT THE WHOLE TIME — the inspector read "24 of 7,500 used", three
            // warehouses' worth — so the extent reached the building and only the drawing never
            // learned it. *A feature can be correct in the sim and absent from the game.*
            DrawFootprint(
                centre,
                building.ExtentWidth * 0.8f,
                building.ExtentHeight * 0.8f,
                building.Facing.Raw,
                colour with { A = 0.85f },
                colour);

            // ⭐ A FULL STORE SAYS SO ON THE MAP (Joe, D140). D134 is the reason it has to:
            // a village can sit at "Logs 15" with 1,968 stranded outside a warehouse that filled
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
        DrawTheBakedValley(valley);
        DrawWorkedGround(minX, maxX, minY, maxY);
        DrawTheTrees(minX, maxX, minY, maxY);

        // Under the zone washes, because soil is a property of the ground while the zones
        // are instructions about it (D178).
        DrawSoil(minX, maxX, minY, maxY);
        DrawResidentialLand(minX, maxX, minY, maxY);

        DrawRect(valley, ValleyEdge, filled: false, width: 2f);

        // ⭐⭐ THE GRID LINES ARE THE LITERAL GRAPH PAPER, AND THEY HAD NO SWITCH (D332). Joe,
        // playing the free-placement build: *"if the game is gridless, then why is everything still
        // in a grid?"* **Two of the three things he was looking at are drawing, not simulation** —
        // the sim being tile-indexed is his own settled call (`gridless.md §10.2`), and nothing had
        // ever revisited whether the tiles should be *visible*.
        // ⛔ OFF BY DEFAULT NOW (D340, Joe: *"the default setting for showing the grid should
        // be off. it is presently on."*). D332 shipped them on, arguing they are useful while
        // aiming — **and that argument was already stale when it was written**, because D330
        // had made placement continuous a commit earlier. *A grid is a readout of a constraint
        // that no longer exists; it is a ruler you can pick up, not the shape of the world.*
        // Only worth drawing at all while tiles are big enough to read.
        if (!_showGrid || _pixelsPerTile < 6f)
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
    /// How good the ground is — <b>the overlay that turns per-site yield from a lottery into a
    /// decision</b> (`specs/per-site-yield.md §5`, D178).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔ WITHOUT THIS THE WHOLE SLICE IS AN INVISIBLE MULTIPLIER.</b> A farm on good ground
    /// out-yields one on thin ground by two to one, and if the player cannot see which is which
    /// then siting a farm is a lottery — which is precisely what D67 refused for ore
    /// (*"you can see a seam, so going after it is a decision rather than a lottery"*) and what
    /// §1.1 refuses in general. **This project has rejected an invisible number three times**
    /// (D37's spoilage, the seasonal yield curve, `skills-catalog.md §7`); shipping the yield
    /// without the overlay would be the fourth.
    /// </para>
    /// <para>
    /// <b>Off by default and behind its own control</b>, because it is a question the player
    /// asks occasionally — *where is the good ground?* — rather than something to look at all
    /// the time. An always-on wash over the whole valley is the standing alert D42 and D123
    /// deleted, in a different medium.
    /// </para>
    /// <para>
    /// <b>Green for rich, bare for thin</b>, keyed on
    /// <see cref="VillageEconomy.ReferenceSoil"/> so the midpoint is *"ordinary"* and the eye
    /// reads distance from it rather than an absolute. Under the zone washes, because soil is a
    /// property of the ground while the zones are instructions about it.
    /// </para>
    /// </remarks>
    private void DrawSoil(int minX, int maxX, int minY, int maxY)
    {
        if (!_showSoil)
        {
            return;
        }

        SimWorld world = _world!;
        int reference = VillageEconomy.ReferenceSoil(world.Config);
        if (reference <= 0)
        {
            return;
        }


        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var tile = new GridPos(x, y);
                if (!world.Map.Contains(tile) || world.Map.TerrainAt(tile) == Terrain.Water)
                {
                    continue;
                }

                // How far from ordinary, as a share. Clamped so a config with a wild soil
                // range cannot drive the alpha out of its own bounds.
                int soil = world.Map.SoilAt(tile);
                float away = Mathf.Clamp((soil - reference) / (float)reference, -1f, 1f);

                Color wash = away >= 0f
                    ? RichGround with { A = RichGround.A * away }
                    : ThinGround with { A = ThinGround.A * -away };

                DrawRect(TileRect(tile), wash);
            }
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
    /// <summary>
    /// ⭐⭐ Re-trace the painted borders, but only when the paint has actually moved (D332).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>This walks the WHOLE map, not the visible window</b>, and that is deliberate: a region
    /// half off screen has a border that runs off the edge, and tracing only what is visible would
    /// draw a fence across the middle of it where the viewport happens to end. **The cost is paid
    /// once per brush stroke rather than once per frame**, which is the trade the counter buys.
    /// </para>
    /// <para>
    /// ⭐ Cheaper than what it sits beside: the visible window is already walked **six times a
    /// frame** by the terrain, soil, zone and woods passes with no caching at all.
    /// </para>
    /// </remarks>
    private void TraceTheZonesIfTheyMoved(ZoneMap zones)
    {
        if (_outlinesTracedAt == zones.Edits)
        {
            return;
        }

        _outlinesTracedAt = zones.Edits;
        _zoneOutlines.Clear();

        var residential = new HashSet<Vector2I>();
        var harvest = new HashSet<Vector2I>();
        var byOwner = new Dictionary<int, HashSet<Vector2I>>();
        var owners = new List<int>();

        SimConfig config = _world!.Config;

        // ⭐⭐ TRACED FROM THE SUB-TILES (D336), so the border is the shape the player drew rather
        // than the shape the tile summary rounded it to. **The outline and the wash come from one
        // set of quarter-tiles**, which is what stops them disagreeing at the edge.
        // ⚠️ Sixteen times the cells, and it is affordable for exactly one reason: it runs on a
        // brush stroke, never on a frame. *`ZoneMap.Edits` is what buys that.*
        for (int y = config.MapMinY * SubTile.PerTile;
            y < (config.MapMaxY + 1) * SubTile.PerTile; y++)
        {
            for (int x = config.MapMinX * SubTile.PerTile;
                x < (config.MapMaxX + 1) * SubTile.PerTile; x++)
            {
                int index = IndexOfSub(zones, new SubTile(x, y));
                if (index < 0)
                {
                    continue;
                }

                var at = new Vector2I(x, y);

                if (zones.ResidentialSub[index])
                {
                    residential.Add(at);
                }

                if (zones.HarvestSub[index])
                {
                    harvest.Add(at);
                }

                int owner = zones.WorkGroundSub[index];
                if (owner == 0)
                {
                    continue;
                }

                if (!byOwner.TryGetValue(owner, out HashSet<Vector2I>? theirs))
                {
                    theirs = new HashSet<Vector2I>();
                    byOwner[owner] = theirs;

                    // ⚠️ The owners are collected in a LIST as they are met, and the drawing walks
                    // that — never the dictionary. *Hash-table order is not a thing to draw from.*
                    owners.Add(owner);
                }

                theirs.Add(at);
            }
        }

        Keep(residential, Layer.Residential, ResidentialEdge);
        Keep(harvest, Layer.Harvest, HarvestEdge);

        for (int i = 0; i < owners.Count; i++)
        {
            Keep(byOwner[owners[i]], Layer.WorkGround, WorkGroundEdge);
        }

        // ⚠️ The LAYER is stored, not inferred from the colour (D340). The three
        // toggles have to hide a border and its wash together, and *"whichever loops came out
        // `HarvestEdge`"* is a coincidence of palette rather than a fact about the layer.
        void Keep(HashSet<Vector2I> tiles, Layer layer, Color edge)
        {
            foreach (Vector2[] loop in ZoneOutline.Trace(tiles, SubTile.PerTile))
            {
                _zoneOutlines.Add((layer, edge, loop));
            }
        }
    }

    /// <summary>
    /// ⭐ The painted borders, drawn as one smooth line each instead of a staircase of rects.
    /// </summary>
    private void DrawTheZoneOutlines()
    {
        for (int i = 0; i < _zoneOutlines.Count; i++)
        {
            (Layer layer, Color edge, Vector2[] loop) = _zoneOutlines[i];
            if (!Showing(layer))
            {
                continue;
            }

            var onScreen = new Vector2[loop.Length];
            for (int p = 0; p < loop.Length; p++)
            {
                onScreen[p] = ToScreen(InTileSpace(loop[p], SubTile.PerTile));
            }

            DrawPolyline(onScreen, edge, Mathf.Max(1.5f, _pixelsPerTile * 0.07f), antialiased: true);
        }
    }

    /// <summary>
    /// ⛔⛔ The painted ground — <b>and the pass that was eighty per cent of the
    /// frame</b> (D338).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe, from play: *"the framerate feels A LOT more sluggish."*</b> D336 was right to draw
    /// the wash from the sub-tiles — the paint is quarter-tiles, and a picture drawn from the
    /// tile summary would show a blockier shape than the player laid down — but it stepped
    /// **every** visible quarter-tile: sixteen cells per tile, ~88,000 iterations a frame, for a
    /// valley that is almost entirely unpainted. *The cost was paid on the empty ground.*
    /// </para>
    /// <para>
    /// ⭐⭐ <b>THE ANSWER WAS ALREADY IN <c>ZoneMap</c>, PRIVATE.</b> It has kept a byte per
    /// tile per layer since D335 — how many of the sixteen are painted. Asked first, the pass
    /// becomes three cases: <b>an empty tile costs one comparison, a full tile costs one
    /// rectangle, and only the ragged edge pays sixteen.</b> A ragged edge is a thin ring round a
    /// region, so the sixteens are a rounding error on a real village.
    /// </para>
    /// <para>
    /// ⛔ <b>NOT <c>IsResidential</c> / <c>IsHarvest</c>, and that is the trap this had to
    /// avoid.</b> Those mean *"at least half"* — the threshold the economy asks in — so a
    /// tile with one quarter painted answers <c>false</c> **and still has paint to draw.** Skipping
    /// on them would have erased exactly the ragged edge the sub-tiles were built for, and it would
    /// have looked like the feature rather than like a bug.
    /// </para>
    /// <para>
    /// ⭐ <b>A full work-ground tile is one owner</b>, so it collapses like the other two: a
    /// sub-tile may only be given to the owner its tile already has
    /// (<c>ZoneMap.SetWorkGround</c>), so sixteen owned quarters are sixteen quarters owned by
    /// <c>WorkGroundOwner</c>.
    /// </para>
    /// <para>
    /// ⚠️ <b>And the selected owner is asked ONCE.</b> It used to be asked inside the
    /// inner loop, and <c>SelectedGroundOwner</c> walks every workplace doing
    /// <c>Footprint.Covers</c> — *the sim's hottest call, in fixed-point, with a rotation in
    /// it* — which came to roughly nineteen thousand rotated-rectangle tests a frame to answer
    /// a question whose answer cannot change during the pass.
    /// </para>
    /// </remarks>
    /// <summary>Re-find the spent marks, but only when the paint or the ground has moved.</summary>
    private void FindTheSpentMarksIfTheyMoved(ZoneMap zones)
    {
        SimWorld world = _world!;

        if (_spentAtEdits == zones.Edits && _spentAtTerrain == world.TerrainGeneration)
        {
            return;
        }

        _spentAtEdits = zones.Edits;
        _spentAtTerrain = world.TerrainGeneration;
        _spentMarks.Clear();

        SimConfig config = world.Config;

        for (int y = config.MapMinY; y <= config.MapMaxY; y++)
        {
            for (int x = config.MapMinX; x <= config.MapMaxX; x++)
            {
                var tile = new GridPos(x, y);

                if (zones.HarvestSubTilesOn(tile) > 0 && !world.HasSomethingToHarvest(tile))
                {
                    _spentMarks.Add(tile);
                }
            }
        }
    }

    private void DrawResidentialLand(int minX, int maxX, int minY, int maxY)
    {
        ZoneMap zones = _world!.Zones;

        TraceTheZonesIfTheyMoved(zones);
        FindTheSpentMarksIfTheyMoved(zones);

        int mine = SelectedGroundOwner();
        _zoneRectsLastFrame = 0;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var tile = new GridPos(x, y);

                int homes = Showing(Layer.Residential) ? zones.ResidentialSubTilesOn(tile) : 0;
                int work = Showing(Layer.WorkGround) ? zones.WorkGroundSubTilesOn(tile) : 0;
                int harvest = Showing(Layer.Harvest) ? zones.HarvestSubTilesOn(tile) : 0;

                // The whole point: an unpainted tile is three byte reads and nothing else.
                if ((homes | work | harvest) == 0)
                {
                    continue;
                }

                DrawLayer(tile, Layer.Residential, homes, ResidentialColour);
                DrawLayer(
                    tile,
                    Layer.WorkGround,
                    work,
                    zones.WorkGroundOwner(tile) == mine ? WorkGroundMine : WorkGroundColour);
                DrawLayer(
                    tile,
                    Layer.Harvest,
                    harvest,
                    _spentMarks.Contains(tile) ? HarvestWaitingColour : HarvestColour);
            }
        }

        DrawTheZoneOutlines();

        void DrawLayer(GridPos tile, Layer layer, int painted, Color colour)
        {
            if (painted == 0)
            {
                return;
            }

            if (painted == SubTile.PerWholeTile)
            {
                DrawRect(TileRect(tile), colour);
                _zoneRectsLastFrame++;
                return;
            }

            for (int sy = 0; sy < SubTile.PerTile; sy++)
            {
                for (int sx = 0; sx < SubTile.PerTile; sx++)
                {
                    SubTile at = SubTile.Of(tile, sx, sy);
                    int index = IndexOfSub(zones, at);
                    if (index < 0)
                    {
                        continue;
                    }

                    bool here = layer switch
                    {
                        Layer.Residential => zones.ResidentialSub[index],
                        Layer.WorkGround => zones.WorkGroundSub[index] != 0,
                        _ => zones.HarvestSub[index],
                    };

                    if (here)
                    {
                        DrawRect(SubTileRect(at), colour);
                        _zoneRectsLastFrame++;
                    }
                }
            }
        }
    }

    /// <summary>
    /// How many rectangles the painted-ground pass drew last frame — <b>for the debug readout,
    /// because *"it feels sluggish"* needs a number</b> (D338).
    /// </summary>
    public int ZoneRectsLastFrame => _zoneRectsLastFrame;

    private int _zoneRectsLastFrame;

    /// <summary>Where a sub-tile lives in the zone arrays, or −1 if it is off the map.</summary>
    private int IndexOfSub(ZoneMap zones, SubTile at)
    {
        SimConfig config = _world!.Config;

        int x = at.X - (config.MapMinX * SubTile.PerTile);
        int y = at.Y - (config.MapMinY * SubTile.PerTile);
        int width = zones.SubWidth;

        return x < 0 || x >= width || y < 0 || y >= (zones.ResidentialSub.Count / width)
            ? -1
            : (y * width) + x;
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

        // ⭐ THE WHOLE FOOTPRINT, NOT THE ANCHOR (D321). Clicking the second or third tile of a
        // longhouse selected nothing, which reads as the building being unclickable rather than as
        // the anchor being special — the player has no way to know which tile is the anchor.
        return _world!.StandingWorkplaceCovering(tile)?.Id ?? 0;
    }

    /// <summary>
    /// ⛔⛔ How big a tile must be before individual canopies are drawn — <b>and the
    /// old value could never once have fired</b> (D342).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It was 10, and its comment claimed that was what stopped *"the per-frame full-map walk
    /// `Minimap`'s comment records this project being bitten by twice."* It stopped nothing.</b>
    /// Minimum zoom is <em>derived from the window</em> — <c>Size.X / (MapWidth * 0.8)</c>
    /// — which is **13.3 px/tile at 1280 wide and more on any larger screen**, so the floor
    /// was unreachable at every playable window size. *The tree pass ran at every zoom the game
    /// has, including the one where 1,640 woodland tiles are on screen at once.*
    /// </para>
    /// <para>
    /// ⛔ <b>Measured cost: ~5,740 <c>DrawCircle</c> a frame — and a circle becomes a
    /// `CommandPolygon`, which BREAKS Godot's 2D batching</b> where a rect batches. That is why
    /// 1,830 terrain rects were nearly free and the trees were ~85 ms. Joe: *"11 fps zoomed
    /// out."*
    /// </para>
    /// <para>
    /// ⭐ <b>The gate now admits the near view instead of pretending to exclude the far
    /// one</b>, and the far view gets its foliage from <see cref="ValleyTexture"/> — baked
    /// once, one draw call, and *ragged at the treeline for the same reason the riverbank is*.
    /// Set above <c>ValleyTexture.PixelsPerTile</c> so live canopies take over at the zoom where
    /// the bake would start to soften. **A gate stated in units the system cannot produce is not
    /// a gate** — D242, D326, D332, D336, D338, and now this.
    /// </para>
    /// </remarks>
    private const float TreeZoomFloor = ValleyTexture.PixelsPerTile * 1.5f;

    /// <summary>How far a canopy may sit from its tile's centre, in tiles.</summary>
    /// <remarks>
    /// ⭐⭐ <b>MORE THAN HALF A TILE, DELIBERATELY — THE OVERHANG IS THE WHOLE TRICK (D337).</b>
    /// Joe: *"why are forests grid-shaped?"* A canopy near the edge of a woodland tile spills onto
    /// the grass beside it, so **the treeline becomes ragged for the reason real treelines are
    /// ragged.** *A smoothed outline would have made the forest edge a different wrong shape; this
    /// makes it not a shape at all.*
    /// </remarks>
    private const float CanopySpread = 0.55f;

    /// <summary>How close to its tile's centre the nearest canopy sits.</summary>
    /// <remarks>
    /// ⚠️ <b>Not zero, and the probe is why.</b> Spread drawn uniformly from nothing to the full
    /// reach put most canopies well inside the tile, and across three sampled tiles not one crossed
    /// the boundary — *"nothing overhangs, so every treeline is still square"*. **A wood needs its
    /// trees pushed outward to have an edge at all.**
    /// </remarks>
    private const float CanopyHuddle = 0.16f;

    /// <summary>A canopy's own radius in tiles — <b>it is what actually crosses the line</b>.</summary>
    /// <remarks>
    /// ⭐ The guard first measured the CENTRE of each canopy and reported failure while the wood
    /// was in fact spilling over. *A tree overhangs by its branches, not by its trunk* — so the
    /// reach that matters is the centre plus this.
    /// </remarks>
    private const float CanopyRadius = 0.20f;

    /// <summary>
    /// ⭐⭐ The trees — <b>and until D337 there were none at all</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A wood was a flat rectangle of <see cref="ForestColour"/>.</b> `DrawTheWoods` draws
    /// animals and berry patches and has never drawn a tree, which the audit answering Joe's
    /// question turned up and which is most of why a forest read as a block.
    /// </para>
    /// <para>
    /// ⛔ <b>Nothing is stored.</b> Every canopy is derived from <see cref="Scramble"/> over the
    /// tile's own coordinates, so it cannot drift out of step with the terrain the way a cached
    /// scatter would — and a tile that is felled simply stops having trees, with no invalidation
    /// to remember. *The same statelessness the animals and the berries already rely on.*
    /// </para>
    /// <para>
    /// ⚠️ <b>Its own salt.</b> Sharing <c>Scramble(x, y)</c> with the animals would put a beast in
    /// the same place as a particular tree pattern for ever, which is the kind of correlation that
    /// reads as a pattern once somebody stares at it.
    /// </para>
    /// </remarks>
    private void DrawTheTrees(int minX, int maxX, int minY, int maxY)
    {
        if (_pixelsPerTile < TreeZoomFloor)
        {
            return;
        }

        GeneratedMap map = _world!.Map;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var tile = new GridPos(x, y);
                if (!map.Contains(tile))
                {
                    continue;
                }

                Terrain terrain = map.TerrainAt(tile);
                bool grown = terrain == Terrain.Forest;

                if (!grown && terrain != Terrain.Sapling)
                {
                    continue;
                }

                // ⭐ A young wood is fewer and smaller marks, so replanting is finally something
                // the player can SEE. D221 gave saplings a colour and a sentence; they still had
                // no texture, so a replanted acre read as flat ground in a slightly different green.
                Canopies(tile, grown);
            }
        }
    }

    /// <summary>How many trees stand on one tile.</summary>
    private static int TreesOn(GridPos tile, bool grown) =>
        grown ? 3 + (int)(Scramble(tile.X + TreeSalt, tile.Y - TreeSalt) % 2) : 2;

    /// <summary>
    /// ⭐⭐ Where one canopy sits and how dark it is — <b>a `static` that knows only
    /// the tile</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⛔⛔ <b>THE `static` IS THE GUARD, AND IT REPLACED ONE THAT SCORED ZERO (D337).</b>
    /// The probe asserted the scatter was deterministic by computing it twice and comparing —
    /// and **that check could never have failed**, because both passes ran in one call with one
    /// tick. A seed that drifted frame to frame, making a wood shimmer as the camera moved, would
    /// have sailed straight through it.
    /// </para>
    /// <para>
    /// ⭐ <b>So determinism stopped being asserted and became impossible.</b> A static method
    /// taking a tile and an index cannot reach <c>_world.Tick</c>, <c>_alpha</c> or anything else
    /// that moves — *the compiler refuses the bug instead of a test looking for it.* The
    /// probe's job is now the half it can actually check: that the canopies reach past their tile.
    /// </para>
    /// </remarks>
    private static (Vector2 Where, float Shade) CanopyOn(GridPos tile, int which)
    {
        uint spin = Scramble(tile.X + TreeSalt, tile.Y - TreeSalt) >> (which * 7);

        double angle = ((spin % 628) / 100.0) + (which * 2.4);
        float spread = CanopyHuddle
            + ((CanopySpread - CanopyHuddle) * (((spin >> 9) % 100) / 100f));

        var where = new Vector2(
            tile.X + ((float)Math.Cos(angle) * spread),
            tile.Y + ((float)Math.Sin(angle) * spread));

        // A hair of variation per tree, so a wood is not a field of identical dots.
        return (where, 0.88f + (((spin >> 17) % 24) / 100f));
    }

    /// <summary>The trees standing on one tile, and the ones leaning off it.</summary>
    private void Canopies(GridPos tile, bool grown)
    {
        float radius = _pixelsPerTile * (grown ? CanopyRadius : 0.13f);
        Color trunk = grown ? TreeCanopy : SaplingCanopy;

        for (int i = 0; i < TreesOn(tile, grown); i++)
        {
            (Vector2 where, float shade) = CanopyOn(tile, i);

            DrawCircle(
                ToScreen(where),
                radius * shade,
                trunk with { R = trunk.R * shade, G = trunk.G * shade, B = trunk.B * shade });
        }
    }

    /// <summary>
    /// ⭐⭐ Are the trees where they should be? — <b>for the probe</b> (D337).
    /// </summary>
    /// <remarks>
    /// The view has no test project (D11, D160), so the two properties that make the scatter work
    /// are asserted here: it is **deterministic** (the same tile gives the same wood twice, or a
    /// forest would shimmer as the camera moved), and it **overhangs** (a canopy reaches past its
    /// own tile, which is the entire reason a treeline stops looking square).
    /// </remarks>
    public string TheTreesAreScatteredAndOverhang()
    {
        float furthest = 0f;
        int counted = 0;
        int overhanging = 0;

        // A spread of tiles, including negatives, because the valley straddles its own origin.
        foreach (GridPos tile in new[]
        {
            new GridPos(0, 0), new GridPos(7, -4), new GridPos(-9, 12),
            new GridPos(31, 24), new GridPos(-3, -17),
        })
        {
            for (int i = 0; i < TreesOn(tile, grown: true); i++)
            {
                (Vector2 where, float _) = CanopyOn(tile, i);

                // ⭐ THE BRANCHES, NOT THE TRUNK. A canopy centred at 0.45 with a radius of
                // 0.20 has already crossed its tile's edge at 0.5. **The first version of this
                // measured the centre and reported failure while the wood was in fact spilling
                // over.** *Measure the thing that is actually over the line.*
                float reach =
                    new Vector2(where.X - tile.X, where.Y - tile.Y).Length() + CanopyRadius;

                furthest = Mathf.Max(furthest, reach);
                counted++;
                if (reach > 0.5f)
                {
                    overhanging++;
                }
            }
        }

        return overhanging > 0
            ? $"[widths] trees: ✅ {counted} canopies, {overhanging} overhanging, furthest "
                + $"{furthest:F2} tiles from centre"
            : $"[widths] trees: ⛔ nothing overhangs — furthest canopy is "
                + $"{furthest:F2} tiles, so every treeline is still square";
    }

    /// <summary>
    /// ⭐⭐ The whole valley in ONE draw call — <b>and the end of the grid showing
    /// through</b> (D342).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This replaced a loop that issued <b>one <c>DrawRect</c> per non-grass tile</b> — about
    /// 1,830 a frame at full zoom-out — and, far more importantly, it replaced what those
    /// rects <em>looked like</em>: hard axis-aligned squares, which is the *"underlying grid
    /// structure"* Joe could see under the smoothed riverbank.
    /// </para>
    /// <para>
    /// ⚠️ <b>Drawn over the valley's own rect</b>, which spans half a tile beyond the
    /// outermost tile centres on every side — exactly the extent
    /// <see cref="ValleyTexture"/> bakes, so the texture lands on the ground it describes and no
    /// half-tile is owed in either direction. *That seam has now been got wrong once (D338); it
    /// is stated in one place here.*
    /// </para>
    /// </remarks>
    private void DrawTheBakedValley(Rect2 valley)
    {
        _valley.Refresh(_world!);

        if (_valley.Texture is ImageTexture baked)
        {
            DrawTextureRect(baked, valley, tile: false);
        }
    }

    private readonly ValleyTexture _valley = new();

    /// <summary>
    /// ⛔ Ground the village has WORKED, drawn as squares — <b>because it is square</b>
    /// (D342).
    /// </summary>
    /// <remarks>
    /// <b>The valley is a field; the fields are not.</b> Everything natural now comes out of
    /// <see cref="ValleyTexture"/> as the level set of a continuous field, which is what makes a
    /// river look like a river. **A ploughed field is man-made and reads as man-made precisely
    /// because its edges are straight**, so smoothing it would be taking the smoothing rule and
    /// applying it where its whole justification is absent. *Foundation's fields have hard edges
    /// too, and for the same reason.*
    /// </remarks>
    private void DrawWorkedGround(int minX, int maxX, int minY, int maxY)
    {
        GeneratedMap map = _world!.Map;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var tile = new GridPos(x, y);
                Terrain terrain = map.TerrainAt(tile);

                if (terrain is not (Terrain.Field or Terrain.Sown or Terrain.Ripe))
                {
                    continue;
                }

                DrawRect(TileRect(tile), ColourOf(terrain));
            }
        }
    }

    /// <summary>What a kind of ground is drawn as.</summary>
    /// <remarks>
    /// <b>Every arm is reached now, and the one that used to be dead is the busiest</b> (D342).
    /// <c>DrawTerrain</c> skipped grass because grass was the background it drew over;
    /// <see cref="ValleyTexture"/> paints every pixel of the valley, so <c>Terrain.Grass</c> is
    /// asked for more often than anything else. *The arm existed anyway — for the minimap,
    /// which bakes every tile for the same reason — and a <c>_ =></c> falling through to
    /// woodland would have painted the whole valley as forest.*
    /// </remarks>
    internal static Color ColourOf(Terrain terrain) => terrain switch
    {
        Terrain.Water => WaterColour,
        Terrain.Rock => RockColour,
        Terrain.IronDeposit => IronColour,
        Terrain.Sapling => SaplingColour,
        Terrain.Grass => Ground,
        Terrain.Field => FieldColour,
        Terrain.Sown => SownColour,
        Terrain.Ripe => RipeColour,
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
    // `GroundColour` was the third of these and is deleted (D159). The minimap borrows
    // `BeyondColour` and `BuildingColour` and paints its ground from the terrain directly,
    // so this one had been an accessor onto a colour nobody asked it for.
    internal static Color BeyondColour => Beyond;

    internal static Color BuildingColour => HutColour;

    internal static Color DwellingColour => HomeColour;

    internal static Color StoreColour => GranaryColour;

    // ⭐ FOUR MORE, FOR THE BUILD BAR'S DRAWN ICONS (`specs/build-bar.md §4`). Same reason as
    // the four above and as `GoodsPalette`: a mark on the bar and a building in the valley
    // meaning different colours would be two facts rather than one. `BuildingGlyph` is the only
    // caller, and it borrows from here rather than restating a hex — which is the one thing
    // `TradeGlyph` did not do, and the reason its colours can drift from the map's.
    internal static Color StoreTone => WarehouseColour;

    internal static Color MarketTone => MarketColour;

    internal static Color LibraryTone => LibraryColour;

    internal static Color CivicTone => TownHallColour;

    internal static Color TimberTone => TreeColour;

    internal static Color FieldTone => FieldColour;

    internal static Color WaterTone => FisheryColour;

    internal static Color StoneTone => RockColour;

    internal static Color IronTone => IronColour;

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
                // ⛔⛔ THE SITE SHOWS THE GROUND IT WILL TAKE, TURNED THE WAY IT WAS PLACED (D324,
                // Joe: *"presently it only shows 1 square during the construction phase (and it is
                // the default orientation, not the rotated/placed orientation)"*). D321 gave a site
                // its extent and facing in the SIM and D320 taught only the FINISHED branch to draw
                // them — so a longhouse was a three-tile ghost, a one-tile square for the years it
                // took to build, and a three-tile building at the end.
                // ⭐ **The site is the moment the footprint matters MOST**, because it is the last
                // point at which there is still time to move it.
                float wide = workplace.ExtentWidth * 0.8f;
                float deep = workplace.ExtentHeight * 0.8f;
                ushort facing = workplace.Facing.Raw;

                // Every material plus the work, so a two-material building fills its ring
                // honestly rather than showing full while its stone is still coming (D213).
                int total = System.Math.Max(1, site.Recipe.TotalMaterials + site.Recipe.WorkTicks);
                float done = (site.TotalDelivered + site.WorkDone) / (float)total;

                // ⭐⭐ A DEMOLITION DRAINS WHERE A CONSTRUCTION FILLS, which is *"reverse
                // construction"* said in the one place the player is actually looking (Joe,
                // 2026-08-26). A building marked to come down starts full and empties as the crew
                // work — so **"how far along is it?" reads the same way in both directions**, and a
                // marked house is obvious the moment you unpaint the ground under it.
                bool pullingDown = site.Demolishing;
                Color colourOfWork = pullingDown ? DemolishColour : SiteColour;
                float shown = pullingDown ? 1f - done : done;

                DrawColoredPolygon(
                    FootprintQuad(centre, wide, deep, facing), colourOfWork with { A = 0.18f });

                // ⭐ Filled from the building's own bottom edge rather than the screen's, so a
                // turned site fills ALONG itself instead of being sliced by a horizontal band that
                // does not know it has been turned.
                if (shown > 0f)
                {
                    DrawColoredPolygon(
                        FootprintQuad(centre, wide, deep, facing, 1f - shown, 1f),
                        colourOfWork with { A = 0.55f });
                }

                Vector2[] outline = FootprintQuad(centre, wide, deep, facing);
                for (int edge = 0; edge < 4; edge++)
                {
                    DrawLine(outline[edge], outline[(edge + 1) % 4], colourOfWork, 2f);
                }

                continue;
            }

            // ⭐⭐ A FINISHED WORKPLACE WAS A BARE CIRCLE, WHICH IS A SHAPE WITH NO DIRECTION
            // (gridless 2b, D320). It could not show a facing however hard it tried, and it could
            // not show an extent either — so a three-tile longhouse and a one-tile hut drew
            // identically. It is the ground it stands on now, turned the way it is turned.
            // ⚠️ 0.8 of a tile rather than the full tile, so neighbouring buildings still read as
            // separate things rather than one continuous slab.
            DrawFootprint(
                centre,
                workplace.ExtentWidth * 0.8f,
                workplace.ExtentHeight * 0.8f,
                workplace.Facing.Raw,
                colour,
                colour with { A = 0.85f });

            // ⭐ AND A BUILDING THAT CANNOT DO ITS JOB SAYS SO (Joe, D147). The same shape as
            // D140's full-store ring, for the same reason and with the same switches — and it
            // earns its place because three times in one session a hut looked idle because of a
            // number set on a different panel. `SimWorld.IdleNote` is the one place that decides
            // what counts, so the ring and the sentence in the inspector cannot drift apart.
            //
            // A COOLER RING THAN THE FULL-STORE ONE, because they are different facts and a
            // player should be able to tell them apart at a glance without reading anything: a
            // full store is usually a village doing well at something, an idle hut never is.
            if (ShowsIdleMarker(workplace) && world.IdleNote(workplace) is not null)
            {
                DrawArc(
                    centre,
                    Mathf.Max(7f, _pixelsPerTile * 0.7f),
                    0f,
                    Mathf.Tau,
                    24,
                    IdleWorkplaceColour,
                    width: Mathf.Max(2f, _pixelsPerTile * 0.12f));
            }
        }
    }

    /// <summary>
    /// Whether this workplace's idle marker is switched on — globally, and for itself.
    /// </summary>
    /// <remarks>
    /// <b>⚠️ VIEW STATE, for the reason D140 spells out on its own marker:</b> the sim is
    /// hashed and replayed from a seed (D2), so a display preference living there would make
    /// two players who merely disagree about what to look at diverge into different worlds.
    /// </remarks>
    private bool ShowsIdleMarker(Workplace workplace) =>
        _showIdleMarkers && !_idleMarkerMuted.Contains(workplace.Id);

    private bool _showIdleMarkers = true;
    private readonly HashSet<int> _idleMarkerMuted = new();

    /// <summary>Switch every idle-workplace marker on or off at once.</summary>
    public void ShowIdleMarkers(bool shown)
    {
        _showIdleMarkers = shown;
        QueueRedraw();
    }

    /// <summary>Switch one workplace's marker on or off, and report where it landed.</summary>
    public bool ToggleIdleMarker(int workplaceId)
    {
        bool nowShown = _idleMarkerMuted.Remove(workplaceId);
        if (!nowShown)
        {
            _idleMarkerMuted.Add(workplaceId);
        }

        QueueRedraw();
        return nowShown;
    }

    /// <summary>Whether one workplace's marker is switched on, ignoring the global switch.</summary>
    public bool IdleMarkerShownFor(int workplaceId) => !_idleMarkerMuted.Contains(workplaceId);

    /// <summary>
    /// The three things the player can paint on the ground — <b>named, because two passes now
    /// have to agree about which is which</b> (D338).
    /// </summary>
    /// <remarks>
    /// ⚠️ The wash and the outline are drawn by different code from the same sub-tiles, so
    /// the layer had to stop being *"whichever colour it came out"*.
    /// </remarks>
    private enum Layer { Residential, WorkGround, Harvest }

    /// <summary>The smoothed borders of every painted region, and what they were traced from.</summary>
    /// <remarks>
    /// <para>
    /// ⭐⭐ <b>CACHED IN TILE SPACE AND TRANSFORMED EACH FRAME</b>, which is what makes this
    /// affordable at all: tracing is O(painted tiles) and panning or zooming does not change the
    /// shape, only where it lands. **Keyed on <c>ZoneMap.Edits</c>**, a monotonic counter that rises
    /// only when a tile actually changes hands — the same shape <c>Minimap</c> uses against
    /// <c>SimWorld.TerrainGeneration</c> to decide when to re-bake.
    /// </para>
    /// <para>
    /// ⚠️ <b>Work ground is traced PER OWNER</b>, or two farms whose fields touch would come out as
    /// one shape and the border would stop answering *"whose is this?"* — which is the question
    /// D86's brighter wash exists for.
    /// </para>
    /// </remarks>
    private readonly List<(Layer Layer, Color Edge, Vector2[] Loop)> _zoneOutlines = new();

    private int _outlinesTracedAt = -1;

    /// <summary>
    /// Marked tiles with nothing left to take — <b>kept, not asked per frame</b> (D343).
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Two counters, because it depends on two things.</b> A tile stops being
    /// worth harvesting when the ground changes (<c>TerrainGeneration</c>) *or* when the paint
    /// moves (<c>ZoneMap.Edits</c>), and keying on one would leave the map lying after the other.
    /// ⭐ Kept rather than recomputed, per <c>CLAUDE.md</c>: *nothing derivable incrementally
    /// may be rebuilt per tick or per frame.*
    /// </remarks>
    private readonly HashSet<GridPos> _spentMarks = new();

    private int _spentAtEdits = -1;

    private int _spentAtTerrain = -1;

    /// <summary>Whether the tile grid is drawn (D332). ⛔ <b>OFF by default (D340).</b></summary>
    /// <remarks>
    /// ⚠️ <b>View-only, like the other map toggles</b> — nothing about the village changes, so the
    /// hash cannot diverge on it. It is here rather than in the sim for the same reason the marker
    /// toggles are.
    /// </remarks>
    private bool _showGrid;

    /// <summary>Draw the tile grid, or stop.</summary>
    public void ShowGrid(bool on)
    {
        _showGrid = on;
        QueueRedraw();
    }

    /// <summary>
    /// ⭐ Whether each painted layer is drawn at all — <b>one switch each, and each hides
    /// the wash AND the border</b> (D340, Joe's call).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe: *"i would like to be able to toggle these overlays of the painted areas for trees,
    /// houses, and farms off in the settings."*</b> He chose one toggle per layer over a single
    /// *"painted ground"* switch, and it is the right call: the three answer different questions
    /// and a player tidying the map may well want to keep one.
    /// </para>
    /// <para>
    /// ⛔ <b>The wash and the border go together.</b> Half a layer showing would read as a
    /// bug rather than as a setting — and the border is the louder half, so hiding only the
    /// fill would barely quieten the map at all.
    /// </para>
    /// <para>
    /// ⚠️ <b>View-only, like every other map toggle</b>, so nothing about the village
    /// changes and the hash cannot diverge on it.
    /// </para>
    /// </remarks>
    private bool Showing(Layer layer) => layer switch
    {
        Layer.Residential => _showResidential,
        Layer.WorkGround => _showWorkGround,
        _ => _showHarvest,
    };

    /// <summary>What the map believes, so the probe can check the ticks against it (D340).</summary>
    public bool GridShown => _showGrid;

    /// <inheritdoc cref="GridShown"/>
    public bool ResidentialLandShown => _showResidential;

    /// <inheritdoc cref="GridShown"/>
    public bool WorkGroundShown => _showWorkGround;

    /// <inheritdoc cref="GridShown"/>
    public bool HarvestLandShown => _showHarvest;

    private bool _showResidential = true;

    private bool _showWorkGround = true;

    private bool _showHarvest = true;

    /// <summary>Show or hide the ground painted for housing.</summary>
    public void ShowResidentialLand(bool on)
    {
        _showResidential = on;
        QueueRedraw();
    }

    /// <summary>Show or hide the ground a workplace has claimed.</summary>
    public void ShowWorkGround(bool on)
    {
        _showWorkGround = on;
        QueueRedraw();
    }

    /// <summary>Show or hide the ground marked for harvest.</summary>
    public void ShowHarvestLand(bool on)
    {
        _showHarvest = on;
        QueueRedraw();
    }

    /// <summary>Whether the soil overlay is being drawn (D178).</summary>
    public bool SoilShown => _showSoil;

    /// <summary>Show or hide the soil overlay.</summary>
    /// <remarks>
    /// <b>Off by default</b> — it answers a question the player asks occasionally (*where is
    /// the good ground?*) rather than one they want answered continuously, and a permanent
    /// wash over the whole valley is the standing alert D42 and D123 deleted in another medium.
    /// </remarks>
    public void ShowSoil(bool shown)
    {
        _showSoil = shown;

        // ⚠️ Redrawn here rather than relying on `Present` coming round next frame. It would
        // — `Main._Process` calls it every frame, paused or not — but that is an accident of
        // the frame loop rather than a decision, and every sibling toggle on this class
        // (`ShowIdleMarkers`, `ShowFullMarkers`, `ToggleFullMarker`) queues its own.
        QueueRedraw();
    }

    private bool _showSoil;

    private void DrawHomes()
    {
        SimWorld world = _world!;

        for (int i = 0; i < world.Households.Count; i++)
        {
            Household household = world.Households[i];
            bool occupied = world.LivingMembersOf(household) > 0;

            // Nothing to draw for a family that has not built yet (D70).
            // ⭐ THE TRUE POSITION, LIKE EVERY OTHER BUILDING (D329) — not the tile it is filed
            // under. Homes always sit on a tile centre today and always will (the land brush
            // places them, D42), so this draws identically; it is written this way so a home that
            // ever does move off centre is drawn where it is rather than where it is indexed.
            if (household.HomePosition is not Point site)
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

    // ---------------------------------------------------------------
    //  Wildlife and heaps — Joe, 2026-09-05
    // ---------------------------------------------------------------

    /// <summary>
    /// One animal for every this much woodland — <b>24 tiles' worth</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Conservative on purpose</b> (Joe: *"alive rather than infested"*, and after playing it:
    /// *"density looks good for now"*). A hunter's lodge reaches about eighty forest tiles, so this
    /// puts three or four animals in its range — enough that the woods are moving, few enough that
    /// felling one wood is a visible loss rather than a rounding error.
    /// </para>
    /// <para>
    /// ⚠️ <b>Read it as "per 24 tiles' WORTH of wood", not "per 24 tiles".</b> The game is going
    /// gridless eventually (`DESIGN.md §4.1`) and a density per unit of woodland survives that
    /// change; a density per tile has to be rediscovered.
    /// </para>
    /// </remarks>
    private const int TilesPerAnimal = 24;

    /// <summary>One berry patch for every 16 tiles' worth of wood — commoner than game.</summary>
    /// <remarks>
    /// Food in a wood should be commoner than deer in it. Same "per unit of woodland" reading as
    /// <see cref="TilesPerAnimal"/>.
    /// </remarks>
    private const int TilesPerBerryPatch = 16;

    /// <summary>Salts the second selection so berries do not land where animals do by accident.</summary>
    /// <remarks>
    /// ⛔ <b>They ARE allowed to share a tile</b> — Joe: *"animals and berries can be in the same
    /// area, and they might be sometimes, especially since animals move over time."* An earlier
    /// draft skipped any tile holding an animal, which was over-engineering: once the animals roam
    /// a tile's width the two overlap constantly anyway, so the rule only held while nothing moved.
    /// The salt is here to make the two selections <b>independent</b>, not exclusive.
    /// </remarks>
    private const int ForageSalt = 0x5BF0;

    /// <summary>Below this zoom the woods are drawn bare, because a speck is noise.</summary>
    /// <remarks>The same floor the grid lines use.</remarks>
    private const float WoodsZoomFloor = 7f;

    /// <summary>How far an animal strays from the wood it belongs to, in tiles.</summary>
    /// <remarks>
    /// ⭐ <b>Joe: *"I want animals to roam a bit more."*</b> This was <b>0.22</b>, which pinned an
    /// animal inside its own tile — it jiggled rather than went anywhere.
    /// </remarks>
    private const float RoamTiles = 1.4f;

    private static readonly Color GameColour = new("#8a6a3f");

    /// <summary>Whether the woods are drawn with anything living in them.</summary>
    private bool _showGame = true;

    /// <summary>Whether the woods are drawn with anything growing in them.</summary>
    private bool _showForage = true;

    /// <summary>Turn the wildlife on or off (Settings).</summary>
    internal void ShowGame(bool shown)
    {
        _showGame = shown;
        QueueRedraw();
    }

    /// <summary>Turn the berry patches on or off (Settings).</summary>
    internal void ShowForage(bool shown)
    {
        _showForage = shown;
        QueueRedraw();
    }

    /// <summary>
    /// Whether this ground is woodland — <b>the one seam the gridless change has to move</b>.
    /// </summary>
    /// <remarks>
    /// ⚠️ Every *"is this woodland?"* in the drawing below goes through here rather than testing
    /// the terrain in four places, so when tiles stop being the unit (`DESIGN.md §4.1`) there is
    /// one call site to rewrite instead of a scatter.
    /// </remarks>
    private bool IsWoodland(GridPos tile) =>
        _world is not null && _world.Map.Contains(tile)
        && _world.Map.TerrainAt(tile) == Terrain.Forest;

    /// <summary>
    /// ⭐⭐ What lives and grows in the woods — <b>a picture of a number, not a herd of entities</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe:</b> *"I want the user to see animals/game roaming the forest"*, and *"fewer animals
    /// in a smaller area of trees and more in a larger tree zone"* — then berries beside them,
    /// because the forest is where <b>produce</b> comes from too and that half had no picture.
    /// </para>
    /// <para>
    /// ⛔⛔ <b>THE SIM GROWS NOTHING FOR THIS.</b> Available game and gatherable food are both
    /// already functions of standing woodland (D297), so both are drawn FROM the forest tiles
    /// rather than tracked beside them. **No sim state, no state-hash surface, no pathfinding,
    /// nothing to desync** — and felling a wood empties it of animals AND berries for free, which
    /// is the only feedback the player gets that both yields have fallen.
    /// </para>
    /// <para>
    /// ⚠️ <b>DERIVED, NOT RANDOM.</b> Every other mark on this map is backed by sim state, and even
    /// <c>FanOffset</c> takes its angle from a villager's RANK rather than a roll, precisely so the
    /// arrangement does not jitter. A per-frame <c>GD.Randi</c> here would make the woods boil. The
    /// tile's own coordinates are the seed, so a given tile either has an animal or a patch or
    /// both, for ever.
    /// </para>
    /// <para>
    /// ⭐ <b>The animals move on the SIM's clock</b>, so a paused game looks paused and 10× speed
    /// looks like 10×. The berries do not move at all, which is the cheapest way to tell the two
    /// apart at a glance: <em>what moves is alive, what stays is growing.</em>
    /// </para>
    /// <para>
    /// ⚠️ <b>One walk, both decorations, culled to the visible tiles.</b> `Minimap` carries the
    /// warning this obeys: a full-map per-frame pass is *"exactly the trap this project has been
    /// bitten by twice in the sim"*. The per-entity passes elsewhere get away with walking whole
    /// lists only because those are tens of items; forest tiles are thousands.
    /// </para>
    /// </remarks>
    private void DrawTheWoods()
    {
        if (_world is null || _pixelsPerTile < WoodsZoomFloor || (!_showGame && !_showForage))
        {
            return;
        }

        Vector2 first = ToTile(Vector2.Zero);
        Vector2 last = ToTile(Size);
        int minX = Mathf.FloorToInt(first.X);
        int maxX = Mathf.CeilToInt(last.X);
        int minY = Mathf.FloorToInt(first.Y);
        int maxY = Mathf.CeilToInt(last.Y);

        float beastRadius = Mathf.Max(2f, _pixelsPerTile * 0.13f);
        float berryRadius = Mathf.Max(1f, _pixelsPerTile * 0.07f);
        Color berryColour = GoodsPalette.ColourOf(Goods.Produce);
        double season = _world.Tick + _alpha;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var tile = new GridPos(x, y);
                if (!IsWoodland(tile))
                {
                    continue;
                }

                uint seed = Scramble(x, y);
                var home = new Vector2(x, y);

                // ⚠️ BOTH TESTS RUN WHATEVER THE TOGGLES SAY. A display setting must never move
                // what is where — switching the animals off is not allowed to relocate a berry.
                bool beast = seed % TilesPerAnimal == 0;
                bool patch = Scramble(x + ForageSalt, y - ForageSalt) % TilesPerBerryPatch == 0;

                if (patch && _showForage)
                {
                    DrawBerryPatch(home, seed, berryRadius, berryColour);
                }

                if (beast && _showGame)
                {
                    DrawCircle(ToScreen(Roam(home, seed, season)), beastRadius, GameColour);
                }
            }
        }
    }

    /// <summary>
    /// Where an animal has wandered to — <b>and never out of the woods</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ The path is a Lissajous figure — <c>cos(drift)</c> against <c>sin(drift × 0.7)</c> — so
    /// the two axes never come back into step and the route reads as wandered rather than orbited.
    /// **It only ever looked mechanical because it was tiny**; the shape did not need replacing,
    /// the amplitude did.
    /// </para>
    /// <para>
    /// ⛔ <b>THE STEP-BACK IS WHAT MAKES ROAMING SAFE.</b> At the old ±0.22 an animal could not
    /// leave its own tile, so nothing had to check where it went. At ±1.4 it will walk into the
    /// river, onto a farm and through a roof — and *"the animal is that tile's worth of game"*
    /// stops being true the moment one is standing in the water. So the full offset is tried, then
    /// half, then home: **three terrain lookups at worst**, against a terrain pass that already
    /// does thousands. ⚠️ It holds in the other direction too — an animal cannot wander out of a
    /// felled wood and go on being drawn there.
    /// </para>
    /// </remarks>
    private Vector2 Roam(Vector2 home, uint seed, double season)
    {
        double phase = (seed % 628) / 100.0;
        double drift = (season * 0.012) + phase;

        var offset = new Vector2(
            (float)Math.Cos(drift) * RoamTiles,
            (float)Math.Sin(drift * 0.7) * RoamTiles);

        for (float reach = 1f; reach > 0.4f; reach -= 0.5f)
        {
            Vector2 spot = home + (offset * reach);
            var on = new GridPos(Mathf.RoundToInt(spot.X), Mathf.RoundToInt(spot.Y));
            if (IsWoodland(on))
            {
                return spot;
            }
        }

        return home;
    }

    /// <summary>
    /// A handful of berries under the trees — <b>still, where the animals move</b>.
    /// </summary>
    /// <remarks>
    /// Three dots rather than one, so a patch reads as growing rather than as a good somebody
    /// dropped — <see cref="DrawHeaps"/> already owns the single-square shape. Their arrangement
    /// comes off the tile's own seed, so no two patches are laid out alike and none of them move.
    /// </remarks>
    private void DrawBerryPatch(Vector2 home, uint seed, float radius, Color colour)
    {
        for (int i = 0; i < 3; i++)
        {
            double angle = (((seed >> (i * 5)) % 628) / 100.0) + (i * 2.1);
            float spread = 0.16f + (((seed >> (i * 3)) % 10) / 100f);

            var at = new Vector2(
                home.X + ((float)Math.Cos(angle) * spread),
                home.Y + ((float)Math.Sin(angle) * spread));

            DrawCircle(ToScreen(at), radius, colour);
        }
    }

    /// <summary>
    /// A stable value for a tile — <b>derived from where it is, never rolled</b>.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Not an RNG, and not the sim's.</b> The sim's seeded RNG must never be advanced by
    /// drawing (D2), and a view-local RNG would give a tile a different answer every frame. This
    /// is a plain integer scramble of the coordinates: same tile, same answer, for ever, and no
    /// state anywhere.
    /// </remarks>
    /// <remarks>
    /// ⭐ <b>MEASURED RATHER THAN ASSUMED</b>, because a hash with a pattern in it would draw
    /// the animals in stripes and the fix would be invisible from the code. Over a
    /// 121×121 block: <b>1 tile in 22.4 selected against a target of 1 in 24</b>, and both
    /// per-row and per-column counts ran 1–11 around a mean of 5.4 — no dead axis, no banding.
    /// </remarks>
    private static uint Scramble(int x, int y)
    {
        unchecked
        {
            uint h = (uint)((x * 73856093) ^ (y * 19349663));
            h ^= h >> 13;
            h *= 0x85EBCA6Bu;
            h ^= h >> 16;
            return h;
        }
    }

    /// <summary>
    /// ⭐⭐ Goods lying where somebody set them down — <b>state that had no picture until now</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe asked whether this was possible and a good idea. It is both, and it closes a
    /// legibility gap rather than adding decoration.</b> `GroundStacks` have been real, hashed sim
    /// state since D134 — position, good and amount — and the overview has been reporting them as
    /// *"Stone 0 (+12 on the ground — no room in store)"* with **nowhere on screen to find the
    /// twelve**. The player was told a number and denied the place.
    /// </para>
    /// <para>
    /// ⚠️ <b>A heap must not read as a building.</b> It is drawn small and offset low so a stack on
    /// a workplace tile does not swallow the workplace, and it is not clickable — selection stays
    /// the buildings' and the villagers', which are the things the player acts on.
    /// </para>
    /// <para>
    /// ⭐ Coloured from <see cref="GoodsPalette"/>, which is why that table left <c>Main</c>: the
    /// chip in the overview and the heap in the valley are one fact.
    /// </para>
    /// </remarks>
    private void DrawHeaps()
    {
        if (_world is null || _pixelsPerTile < WoodsZoomFloor)
        {
            return;
        }

        float size = Mathf.Max(3f, _pixelsPerTile * 0.28f);

        for (int i = 0; i < _world.GroundStacks.Count; i++)
        {
            GroundStack heap = _world.GroundStacks[i];
            if (heap.Amount <= 0)
            {
                continue;
            }

            Vector2 centre = ToScreen(heap.Position) + new Vector2(0f, _pixelsPerTile * 0.22f);
            var box = new Rect2(centre - (Vector2.One * size / 2f), new Vector2(size, size));

            DrawRect(box, GoodsPalette.ColourOf(heap.Goods));
            DrawRect(box, HeapEdge, filled: false, width: 1f);
        }
    }

    private static readonly Color HeapEdge = new(0f, 0f, 0f, 0.45f);

    private void DrawVillagers()
    {
        SimWorld world = _world!;
        var stillAlive = new HashSet<int>();

        GroupByTile(world);

        // Before anybody is drawn, because DrawnCentre reads what this writes and the click
        // test asks DrawnCentre too — one answer per frame, or the dot and the hit test
        // would disagree by a tile.
        AdvanceInterpolation(world);

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

    /// <summary>
    /// Roll each villager's glide forward when — and only when — the sim has actually
    /// taken a tick.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⛔⭐⭐ THIS IS THE JITTER (D169), and it was never in the sim.</b> The glide used to
    /// advance on <c>_alpha &gt;= 0.999</c>. <see cref="FixedTimestepDriver.Alpha"/> is the
    /// accumulator remainder in <c>[0,1)</c>, sampled once a frame, so that condition asks a
    /// frame to land in the last thousandth of a tick — which at any speed the game is
    /// actually watched at is a coin flip nobody wins for tens of seconds at a time. So
    /// <c>Previous</c> froze on a tile from some while ago.
    /// </para>
    /// <para>
    /// <b>What a frozen <c>Previous</c> looks like on screen is exactly what Joe described.</b>
    /// While the villager is more than a tile away from it, <see cref="DrawnCentre"/> snaps and
    /// nothing is wrong. The moment they are standing within one tile of it — which is most of
    /// the time for <em>a farmer working a small field or a forester working painted ground</em>
    /// — the lerp runs from the stale tile instead, so the dot jumps back to it as alpha resets
    /// at each tick and glides forward again: <b>a bounce between two tiles, once a tick, for as
    /// long as they stay put</b>. Nothing in the audit trail shows it, because nothing in the sim
    /// did it: a sweep of the whole of <c>bclone-20260822-000011.log</c> for a villager returning
    /// to a tile they had just left finds <b>15 in 10,476 ticks</b>, none of them a farmer at
    /// work.
    /// </para>
    /// <para>
    /// <b>The tick is the thing to watch, so watch the tick.</b> Alpha says how far through a
    /// tick we are and can never say that one ended.
    /// </para>
    /// </remarks>
    private void AdvanceInterpolation(SimWorld world)
    {
        bool tickAdvanced = world.Tick != _interpolatedThroughTick;
        _interpolatedThroughTick = world.Tick;

        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (!villager.Alive)
            {
                continue;
            }

            var current = new Vector2(villager.Position.X, villager.Position.Y);

            // First sight of somebody — born, or the first frame of the run. They start
            // standing still rather than gliding in from nowhere.
            if (!_tiles.TryGetValue(villager.Id, out (Vector2 Previous, Vector2 Current) known))
            {
                _tiles[villager.Id] = (current, current);
                continue;
            }

            if (!tickAdvanced)
            {
                continue;
            }

            // `known.Current` is where they were on the previous frame, and a sim position
            // only changes on a tick boundary — so it is where they stood last tick.
            _tiles[villager.Id] = (known.Current, current);
        }
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
        Vector2 previous =
            _tiles.TryGetValue(villager.Id, out (Vector2 Previous, Vector2 Current) known)
                ? known.Previous
                : current;

        // Lerp from where they were to where they are. If they moved more than a
        // tile — being born, moving house, or several ticks passing inside one frame at
        // 10× — snap instead, or they would glide across the map.
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

        // ⭐ The river's own colour, lightened — a fishing hut should read as belonging to the
        // water it has to stand against, which is the one thing that decides where it can go.
        JobKind.Fisher => FisheryColour,
        _ => MarketColour,
    };

    /// <summary>Forget interpolation state for the dead, so ids can never be confused
    /// and the dictionary does not grow for the whole run.</summary>
    private void PruneTheDead(HashSet<int> stillAlive)
    {
        if (_tiles.Count <= stillAlive.Count)
        {
            return;
        }

        var gone = new List<int>();
        foreach (KeyValuePair<int, (Vector2 Previous, Vector2 Current)> entry in _tiles)
        {
            if (!stillAlive.Contains(entry.Key))
            {
                gone.Add(entry.Key);
            }
        }

        foreach (int id in gone)
        {
            _tiles.Remove(id);
        }
    }
}
