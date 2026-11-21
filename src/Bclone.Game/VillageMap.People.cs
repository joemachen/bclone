using System.Diagnostics;
using Bclone.Sim.Core;
using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <remarks>
/// ⭐⭐ THE VILLAGERS PAINTED AS PEOPLE (A93, <c>specs/villager-art.md §3b</c>; Joe's pick, <i>"Yes, build E"</i>, of five
/// looks mocked on his own villages). With the watercolour tick on, each villager is a small person standing up on the
/// top-down map in the manner of an illuminated manuscript (<see cref="PersonPainter"/>): their trade in the gown's
/// colour, the hat and the tool, their load, their coat, which way they walk. People indoors — resting at home, at
/// school, teaching — are not drawn. With the tick off, the dots, to the byte.
/// </remarks>
public partial class VillageMap
{
    /// <summary>An adult stands this many tiles tall on screen (a 2 × 1 house is two tiles long).</summary>
    internal const float PersonTiles = 0.72f;

    /// <summary>However far out, a person is never drawn shorter than this many pixels.</summary>
    internal const float PersonMinPixels = 11f;

    /// <summary>The point a person is clicked and glided by sits this far up from their feet, in their height: the belt.</summary>
    internal const float PersonPointUp = 0.40f;

    /// <summary>
    /// At most this many people painted in one frame (a few milliseconds each, the probe's line says how many); the rest
    /// wait a frame in the look they had, or as their dot. A village opened is all painted within its first second.
    /// </summary>
    private const int PersonBakesPerFrame = 4;

    /// <summary>The villagers are people only with the tick on, like every painted layer.</summary>
    internal bool PaintsPeople => _watercolour;

    /// <summary>A painted person kept as a texture, and when it was last drawn.</summary>
    private sealed class PersonCache
    {
        public required CanvasTexture Texture { get; init; }

        public required PaintedFigure Figure { get; init; }

        public ulong LastSeen { get; set; }
    }

    /// <summary>Every painted person, by everything their picture depends on — painted once, kept until unused a while.</summary>
    private readonly Dictionary<FigureKey, PersonCache> _people = new();

    /// <summary>The look each villager was last drawn in, so a person whose new look waits a frame keeps their old one.</summary>
    private readonly Dictionary<int, FigureKey> _lastPersonKey = new();

    /// <summary>The order people are drawn in this frame, back to front; one list, refilled each frame.</summary>
    private readonly List<(float Y, Villager Who, Vector2 Point)> _peopleThisFrame = new();

    private ulong _peopleSwept;
    private int _personBakes;
    private int _bakesThisFrame;
    private ulong _bakesFrame;
    private double _personBakeMs;

    private static readonly Goods[] CarriedGoods = Enum.GetValues<Goods>().Where(g => g != Goods.Handcart).ToArray();

    /// <summary>The doing of a trade, at its place: the tool out and the person facing us.</summary>
    internal static bool AtWork(VillagerState state) => state is VillagerState.Gathering or VillagerState.Cutting
        or VillagerState.MakingFirewood or VillagerState.Building or VillagerState.Clearing or VillagerState.TidyingGround
        or VillagerState.Sowing or VillagerState.Reaping or VillagerState.Fishing or VillagerState.Hunting
        or VillagerState.Forging or VillagerState.DrawingWater or VillagerState.Quarrying or VillagerState.Mining
        or VillagerState.Tending or VillagerState.WorkingABatch or VillagerState.CuttingHay
        or VillagerState.FeedingTheHerd or VillagerState.Culling
        or VillagerState.StockingTheMarket or VillagerState.StockingThePost or VillagerState.ClearingAStore
        or VillagerState.ClearingABuffer;

    /// <summary>On the way to the work: the tool carried along.</summary>
    internal static bool GoingToWork(VillagerState state) => state is VillagerState.TravelingToFood
        or VillagerState.TravelingToTrees or VillagerState.TravelingToHut or VillagerState.TravelingToField
        or VillagerState.TravelingToWater or VillagerState.TravelingToGame or VillagerState.TravelingToSmithy
        or VillagerState.TravelingToQuarry or VillagerState.TravelingToMine or VillagerState.WalkingOutToTend
        or VillagerState.TravelingToABatch;

    /// <summary>
    /// ⭐ Pure: a household's cloak dye (A143, <c>villager-art.md §9</c>) — its place in the founding order round the seven, so
    /// households founded one after another never share one. Household ids are handed out in order (one past the highest), so
    /// the id is the founding order; a household keeps its dye for life, whoever else comes and goes.
    /// </summary>
    internal static int CloakDyeOf(int householdId) =>
        (((householdId - 1) % PersonPainter.CloakDyes.Length) + PersonPainter.CloakDyes.Length) % PersonPainter.CloakDyes.Length;

    /// <summary>
    /// What a villager's picture depends on this frame. Pure: it reads the villager and their heading and changes nothing.
    /// ⭐ <paramref name="heading"/> is the way their drawn point is moving, zero when they stand (A132): someone in a work
    /// state who is moving — a labourer clearing tile to tile, a builder round the site — walks, tool in hand; standing
    /// at it, they face us and work.
    /// </summary>
    internal static FigureKey PersonKeyOf(int id, LifeStage stage, JobKind? trade, VillagerState state, Vector2 heading,
        Goods? good, bool coat, bool cart, int dye = 0)
    {
        bool working = AtWork(state);
        bool moving = heading.LengthSquared() > 1e-6f;
        FigurePose pose = FigurePose.Front;
        bool flip = false;
        if (moving)
        {
            if (Mathf.Abs(heading.X) > Mathf.Abs(heading.Y) * 0.8f)
            {
                pose = FigurePose.Side;
                flip = heading.X < 0f;
            }
            else
            {
                pose = heading.Y > 0f ? FigurePose.Front : FigurePose.Back;
            }
        }

        bool cold = state == VillagerState.SeekingShelter;
        bool talking = state == VillagerState.Idle && !moving;
        bool hasTool = PersonPainter.HasATool(trade, stage);
        FigureHold hold = cold ? FigureHold.Hug
            : good is Goods g && !PersonPainter.OnTheShoulder(g) ? FigureHold.Front
            : (working || GoingToWork(state)) && hasTool ? FigureHold.Tool
            : talking && pose == FigurePose.Front ? FigureHold.Talk
            : FigureHold.None;
        bool toolOut = (hold == FigureHold.Tool) || (stage == LifeStage.Elder && hold == FigureHold.None);
        return new FigureKey(id, stage, trade, pose, flip, hold, moving, toolOut, good, coat, cart, cold, Dye: dye);
    }

    private FigureKey PersonKeyOf(Villager villager, SimWorld world, Vector2 heading)
    {
        JobKind? trade = villager.WorkplaceId != 0 && world.FindWorkplace(villager.WorkplaceId) is Workplace w ? w.Kind : null;
        Goods? most = null;
        int held = 0;
        foreach (Goods g in CarriedGoods)
        {
            int n = villager.Carried[g];
            if (n > held)
            {
                held = n;
                most = g;
            }
        }

        bool cart = villager.PushingACart && !world.TheWorksCartSleepsWith(villager);
        return PersonKeyOf(villager.Id, villager.LifeStage, trade, villager.State, heading, most, villager.CoatDays > 0, cart,
            CloakDyeOf(villager.HouseholdId));
    }

    /// <summary>An adult's height on screen at this zoom.</summary>
    private float PersonPixels => Mathf.Max(PersonMinPixels, _pixelsPerTile * PersonTiles);

    /// <summary>The painted person for a key: kept if painted before, else painted now (within the frame's budget).</summary>
    private PersonCache? CachedPerson(FigureKey key, ulong frame, bool mayPaint)
    {
        if (_people.TryGetValue(key, out PersonCache? held))
        {
            return held;
        }

        if (!mayPaint)
        {
            return null;
        }

        var watch = Stopwatch.StartNew();
        PaintedFigure figure = PersonPainter.Paint(key);
        using Image image = Image.CreateFromData(figure.Width, figure.Height, false, Image.Format.Rgba8, figure.Rgba);
        image.GenerateMipmaps();
        var texture = new CanvasTexture
        {
            DiffuseTexture = ImageTexture.CreateFromImage(image),
            TextureFilter = TextureFilterEnum.LinearWithMipmaps,
        };
        held = new PersonCache { Texture = texture, Figure = figure, LastSeen = frame };
        _people[key] = held;
        _personBakes++;
        _bakesThisFrame++;
        _personBakeMs += watch.Elapsed.TotalMilliseconds;
        return held;
    }

    /// <summary>Lets go of people not drawn for a while — a coat taken off, a load put down, a villager gone.</summary>
    private void ForgetUnseenPeople(ulong frame)
    {
        if (frame - _peopleSwept < 300)
        {
            return;
        }

        _peopleSwept = frame;
        List<FigureKey>? gone = null;
        foreach ((FigureKey key, PersonCache held) in _people)
        {
            if (frame - held.LastSeen > 600)
            {
                (gone ??= new()).Add(key);
            }
        }

        if (gone is null)
        {
            return;
        }

        foreach (FigureKey key in gone)
        {
            _people.Remove(key);
        }
    }

    /// <summary>
    /// ⭐ The villagers as people: back to front, each painted once for their look and kept; the selection a ring on the
    /// ground at their feet, the meal bowl and the arrival chevron over their head.
    /// </summary>
    private void DrawThePeople(SimWorld world, HashSet<int> stillAlive)
    {
        ulong frame = Engine.GetProcessFrames();
        if (_bakesFrame != frame)
        {
            _bakesFrame = frame;
            _bakesThisFrame = 0;
        }

        ForgetUnseenPeople(frame);
        _peopleThisFrame.Clear();
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (!villager.Alive)
            {
                continue;
            }

            stillAlive.Add(villager.Id);
            if (!ShownOnTheMap(world, villager))
            {
                continue;
            }

            Vector2 point = DrawnCentre(villager);
            _peopleThisFrame.Add((point.Y, villager, point));
        }

        _peopleThisFrame.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.Who.Id.CompareTo(b.Who.Id));
        float scale = PersonPixels / PersonPainter.BakeHeight;
        bool poses = PersonPixels >= PosesFromPixels;
        PersonCache shadow = TheShadow();
        foreach ((_, Villager villager, Vector2 point) in _peopleThisFrame)
        {
            // ⭐ The motion (A130): the pose this frame, and how the picture is moved — the step paid for by the distance
            // the drawn point has walked (where it is drawn: the glide, or the road a nomad walks in on).
            Vector2 live = DrawnTile(villager);
            (float walked, Vector2 heading) = WalkedTo(villager.Id, villager.LifeStage, WalkInPlaceOf(villager.Id, live, out _) ?? live);
            FigureKey still = PersonKeyOf(villager, world, heading);
            PersonMotion motion = MotionOf(still, AtWork(villager.State), walked, _peopleClock, poses);
            FigureKey key = still with { Beat = motion.Beat };
            PersonCache? held = CachedPerson(key, frame, _bakesThisFrame < PersonBakesPerFrame);
            if (held is not null)
            {
                _lastPersonKey[villager.Id] = key;
            }
            else if (_lastPersonKey.TryGetValue(villager.Id, out FigureKey before))
            {
                held = CachedPerson(before, frame, false);
            }

            if (held is null)
            {
                // Over the frame's budget and never painted before: the dot for one frame, in their life stage's colour.
                DrawCircle(point, VillagerRadius, villager.LifeStage switch
                {
                    LifeStage.Child => ChildColour,
                    LifeStage.Elder => ElderColour,
                    _ => AdultColour,
                });
                continue;
            }

            held.LastSeen = frame;
            KeepTheOtherPoses(key, frame);
            PaintedFigure f = held.Figure;
            float tall = f.FeetY / PersonPainter.FeetFromTop * scale;
            Vector2 feet = point + new Vector2(0f, tall * PersonPointUp);
            if (villager.Id == _selectedVillagerId)
            {
                DrawTheSelectionAtTheFeet(feet, tall);
            }

            // The shadow on the ground at their feet, scaled to them and not moved with them (A130).
            PaintedFigure sf = shadow.Figure;
            float ss = f.FeetY / sf.FeetY * scale;
            DrawTextureRect(shadow.Texture, new Rect2(feet - (new Vector2(sf.FeetX, sf.FeetY) * ss), new Vector2(sf.Width, sf.Height) * ss), tile: false);

            // The person, lifted, leaned and squashed about their feet as the motion says.
            DrawSetTransform(feet + (new Vector2(motion.Dx, motion.Dy) * tall), Mathf.DegToRad(motion.Turn), new Vector2(1f, motion.Squash));
            var rect = new Rect2(-(new Vector2(f.FeetX, f.FeetY) * scale), new Vector2(f.Width, f.Height) * scale);
            DrawTextureRect(held.Texture, rect, tile: false);
            DrawSetTransformMatrix(Transform2D.Identity);

            // ⭐ The water pail at their side (`water-need.md §2e`, D782) — the dots' stand-in, hung at a person's hip height.
            if (_pails.TryGetValue(villager.Id, out bool full))
            {
                DrawThePail(feet - new Vector2(0f, tall * 0.42f), tall * 0.16f, full);
            }

            Vector2 overHead = feet - new Vector2(0f, tall);
            float r = Mathf.Max(3f, tall * 0.12f);
            if (WalkInProgressOf(villager.Id) is float walking)
            {
                DrawTheArrivalMark(overHead, r, walking);
            }

            if (_mealShownUntil.TryGetValue(villager.Id, out double until))
            {
                float left = MealMarkLeft(until, _mealClock);
                if (left > 0f)
                {
                    DrawTheMealMark(overHead, r, left);
                }
                else
                {
                    _mealShownUntil.Remove(villager.Id);
                }
            }
        }

        foreach (int id in _lastPersonKey.Keys.Where(id => !stillAlive.Contains(id)).ToList())
        {
            _lastPersonKey.Remove(id);
        }

        foreach (int id in _walked.Keys.Where(id => !stillAlive.Contains(id)).ToList())
        {
            _walked.Remove(id);
        }
    }

    /// <summary>
    /// A person's other poses are part of their look: kept while any one of them is drawn, so a long pause (one pose shown
    /// for minutes) does not let the others go and leave the village repainting when it runs again (A130, after play).
    /// </summary>
    private void KeepTheOtherPoses(FigureKey key, ulong frame)
    {
        for (int b = 0; b <= 2; b++)
        {
            if (b != key.Beat && _people.TryGetValue(key with { Beat = b }, out PersonCache? other))
            {
                other.LastSeen = frame;
            }
        }
    }

    /// <summary>The selected person: the violet ring laid on the ground round their feet.</summary>
    private void DrawTheSelectionAtTheFeet(Vector2 feet, float tall)
    {
        const int n = 28;
        var ring = new Vector2[n + 1];
        float rx = tall * 0.30f, ry = tall * 0.11f;
        for (int k = 0; k <= n; k++)
        {
            float t = Mathf.Tau * k / n;
            ring[k] = feet + new Vector2(rx * Mathf.Cos(t), ry * Mathf.Sin(t));
        }

        DrawPolyline(ring, SelectedRing, 2f, antialiased: true);
    }

    /// <summary>How far from a person's point a click still takes them: their body, not the dot's radius.</summary>
    private float PersonReach => PaintsPeople ? Mathf.Max(VillagerRadius, PersonPixels * 0.42f) : VillagerRadius;
}
