using Bclone.Sim.World;
using Godot;

namespace Bclone.Game;

/// <remarks>
/// ⭐⭐ THE VILLAGERS MOVE (A130, <c>specs/villager-animation.md</c>; Joe's pick of three looks mocked on his Hollowmere,
/// <i>"B, all as recommended"</i>). Look B: a second painted pose for each motion — the other step, the tool raised and
/// the stroke, the palm lowered — and the picture lifted, leaned and swayed as it is drawn. <b>View only</b>: it reads
/// what <see cref="DrawThePeople"/> already reads and writes nothing to the sim; nothing here is hashed.
/// </remarks>
public partial class VillageMap
{
    /// <summary>An adult's step, in tiles (a person is <see cref="PersonTiles"/> tall): at 1× on grass, 2.1 steps a second.</summary>
    internal const float StepTiles = 0.36f;

    /// <summary>⭐ However fast the village runs, nobody steps quicker than this (Joe's call 2, §1.2): above it the stride lengthens.</summary>
    internal const float StepsASecondAtMost = 2.4f;

    /// <summary>The work, the talk and the sway keep the game's speed up to this many times 1× (Joe's call 2).</summary>
    internal const double PeoplePaceAtMost = 1.5;

    /// <summary>Under this many pixels tall only the first pose is painted; the picture is moved instead (§2).</summary>
    internal const float PosesFromPixels = 16f;

    /// <summary>A drawn point moving further than this in one frame jumped (a birth, a move of house): not walked.</summary>
    private const float SnapTilesInAFrame = 1f;

    private const float StepRise = 0.022f, LoadedRise = 1.45f, FarTilt = 1.3f, SwayDegrees = 1f;
    private const float TalkSeconds = 2.4f, SwaySeconds = 4.8f, LeanSeconds = 3.2f, ShiverHz = 2.5f, ShiverShake = 0.012f;

    /// <summary>The people's clock (seconds): the game's speed, capped at <see cref="PeoplePaceAtMost"/>; still while paused (Joe's call 3).</summary>
    private double _peopleClock;

    /// <summary>This frame's real seconds while the village runs; nought while it is paused.</summary>
    private double _peopleFrameSeconds;

    /// <summary>
    /// A drawn point that has not moved for this many seconds of the running village has stopped: they stand. Long enough
    /// that a late tick (the glide waiting at its end for a frame or two) does not flick a walker to standing and back.
    /// </summary>
    internal const float StandAfterSeconds = 0.25f;

    /// <summary>
    /// A person's walk as the view keeps it: where their drawn point was last frame, how far it has walked (tiles), which
    /// way it last went, and how long it has been still (running seconds).
    /// </summary>
    private readonly record struct Walk(Vector2 Tile, float Walked, Vector2 Heading, float StillFor);

    /// <summary>
    /// Each person's <see cref="Walk"/> — kept, and advanced where the point moves (CLAUDE.md: maintained, not rebuilt).
    /// Dropped with the villager.
    /// </summary>
    private readonly Dictionary<int, Walk> _walked = new();

    /// <summary>A frame of the people's clock — <see cref="_Process"/>, beside the meal bowl's.</summary>
    private void TickThePeopleClock(double delta)
    {
        _peopleFrameSeconds = Paused ? 0.0 : delta;
        _peopleClock += _peopleFrameSeconds * Math.Clamp(_flowPace, 0.0, PeoplePaceAtMost);
    }

    /// <summary>A person's step, in tiles: a child's 0.62 of an adult's, an elder's 0.85.</summary>
    internal static float StepOf(LifeStage stage) => StepTiles * stage switch
    {
        LifeStage.Child => 0.62f,
        LifeStage.Elder => 0.85f,
        _ => 1f,
    };

    /// <summary>
    /// The distance a drawn point has walked, advanced by how far it moved since last frame — never more than
    /// <see cref="StepsASecondAtMost"/> steps' worth of this frame's real seconds (so paused, nothing), and nothing for a jump.
    /// </summary>
    internal static float WalkedOn(float walkedBefore, float movedTiles, float step, double frameSeconds)
    {
        if (movedTiles > SnapTilesInAFrame)
        {
            return walkedBefore;
        }

        return walkedBefore + Mathf.Min(movedTiles, StepsASecondAtMost * step * (float)frameSeconds);
    }

    /// <summary>
    /// ⭐⭐ WHICH WAY A PERSON IS WALKING IS WHICH WAY THEIR DRAWN POINT MOVES (A132, Joe: <i>"right from the very beginning,
    /// these people do not move their legs at all"</i>). Not the job's state — a labourer <c>Clearing</c> walks tile to
    /// tile in a work state and slid across the grass facing us, legs still — and not the sim's leg, which outlives the
    /// walk (someone home stood mid-stride on a finished leg). Pure: the heading and stillness before, this frame's move
    /// (view tiles), and this frame's running seconds. Paused (nought seconds), nothing changes; a jump is not a walk.
    /// </summary>
    internal static (Vector2 Heading, float StillFor) HeadingOn(Vector2 headingBefore, float stillBefore, Vector2 moved, double frameSeconds)
    {
        if (frameSeconds <= 0.0)
        {
            return (headingBefore, stillBefore);
        }

        float d = moved.Length();
        if (d > 1e-5f && d <= SnapTilesInAFrame)
        {
            return (moved / d, 0f);
        }

        float still = stillBefore + (float)frameSeconds;
        return (still >= StandAfterSeconds ? Vector2.Zero : headingBefore, still);
    }

    /// <summary>
    /// This person's walk, advanced to <paramref name="tile"/> (the view's tile space, where they are drawn): how far they
    /// have walked, and which way they are going (zero when they stand).
    /// </summary>
    private (float Walked, Vector2 Heading) WalkedTo(int id, LifeStage stage, Vector2 tile)
    {
        if (!_walked.TryGetValue(id, out Walk was))
        {
            _walked[id] = new Walk(tile, 0f, Vector2.Zero, StandAfterSeconds);
            return (0f, Vector2.Zero);
        }

        Vector2 moved = tile - was.Tile;
        float walked = WalkedOn(was.Walked, moved.Length(), StepOf(stage), _peopleFrameSeconds);
        (Vector2 heading, float still) = HeadingOn(was.Heading, was.StillFor, moved, _peopleFrameSeconds);
        _walked[id] = new Walk(tile, walked, heading, still);
        return (walked, heading);
    }

    /// <summary>
    /// Which pose a person is painted in this frame, and how the picture is moved as it is drawn: <c>Dx</c>, <c>Dy</c> as
    /// shares of their height (up is negative), <c>Turn</c> degrees clockwise about the feet, <c>Squash</c> toward the feet.
    /// </summary>
    internal readonly record struct PersonMotion(int Beat, float Dx, float Dy, float Turn, float Squash);

    /// <summary>
    /// ⭐ The motion (§1, §3). Pure: the key, whether the person is at their trade's work, how far they have walked, the
    /// people's clock, and whether this zoom paints poses. Every beat starts at a phase from the person's id, so nobody
    /// steps or swings in time with anybody (D28).
    /// </summary>
    internal static PersonMotion MotionOf(FigureKey key, bool atWork, float walked, double clock, bool poses)
    {
        int id = key.Id;
        int beat = 0;
        float dx = 0f, dy = 0f, turn = 0f, squash = 1f;
        float Phase(float seconds, string salt) => (float)(((clock / seconds) + PersonPainter.Hash01(id, salt)) % 1.0);

        if (key.Walking)
        {
            float s = (walked / StepOf(key.Stage)) + (2f * PersonPainter.Hash01(id, "step"));
            float within = s - Mathf.Floor(s);
            float rise = StepRise * (key.Good is not null || key.Cart ? LoadedRise : 1f);
            dy = -rise * Mathf.Sin(Mathf.Pi * within);
            if (poses)
            {
                beat = key.Pose == FigurePose.Side ? (within < 0.5f ? 0 : 1) : ((int)Mathf.Floor(s) & 1);
            }

            // Toward or away (where the legs show least), and too small for a pose at all: the weight goes from foot to
            // foot, a tilt each step (A130, after Joe's play: *"their legs look stationary"*).
            if (!poses || key.Pose != FigurePose.Side)
            {
                turn = FarTilt * Mathf.Sin(Mathf.Pi * s);
            }
        }
        else if (atWork)
        {
            float swing = PersonPainter.SwingSeconds(key.Trade, key.Stage);
            float stoop = PersonPainter.StoopOf(key.Trade, key.Stage);
            if (poses && swing > 0f && key.Hold == FigureHold.Tool)
            {
                // held up most of the beat, then the stroke, the body dipping into it
                beat = Phase(swing, "beat") < 0.62f ? 1 : 2;
                if (beat == 2)
                {
                    dy = 0.012f;
                    squash = 1f - stoop;
                }
            }
            else
            {
                // no pose to paint (no tool that swings, or too small to see one): lean into it and back
                float p = Phase(swing > 0f ? swing : LeanSeconds, "beat");
                float k = Mathf.Max(0f, Mathf.Sin(Mathf.Tau * p));
                turn = -2.2f * Mathf.Sin(Mathf.Tau * p);
                dy = 0.012f * k;
                squash = 1f - ((stoop + 0.015f) * k);
            }
        }
        else
        {
            if (key.Hold == FigureHold.Talk && poses)
            {
                beat = Phase(TalkSeconds, "talk") < 0.6f ? 0 : 1;
            }

            turn = SwayDegrees * Mathf.Sin(Mathf.Tau * Phase(SwaySeconds, "sway"));
        }

        if (key.Cold)
        {
            dx += ShiverShake * Mathf.Sin(Mathf.Tau * ShiverHz * (float)(clock % 1000.0));
        }

        return new PersonMotion(beat, dx, dy, turn, squash);
    }

    /// <summary>The shadow under every person, painted once (<see cref="PersonPainter.PaintShadow"/>).</summary>
    private PersonCache? _personShadow;

    private PersonCache TheShadow()
    {
        if (_personShadow is null)
        {
            PaintedFigure figure = PersonPainter.PaintShadow();
            using Image image = Image.CreateFromData(figure.Width, figure.Height, false, Image.Format.Rgba8, figure.Rgba);
            image.GenerateMipmaps();
            _personShadow = new PersonCache
            {
                Texture = new CanvasTexture
                {
                    DiffuseTexture = ImageTexture.CreateFromImage(image),
                    TextureFilter = TextureFilterEnum.LinearWithMipmaps,
                },
                Figure = figure,
            };
        }

        return _personShadow;
    }
}
