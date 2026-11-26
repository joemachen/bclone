using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <remarks>
/// ⭐⭐ FISHING YOU CAN WATCH (A277, <c>specs/fishing-animation.md</c>; Joe's play note (b): <i>"the cast, the catch with a splash,
/// several versions shuffled"</i>; his pick, <i>"B, as recommended"</i>). A fisher at work stands at the end of the hut's dock
/// (<see cref="AtTheDoor"/>), casts, waits with the float bobbing and the rod dipping, and lands the catch with a splash — each cast a
/// small fish, a big one or a thrown net, shuffled by the person and the cast. ⛔ <b>The view's, never the sim's</b>: the sim's cast
/// is a countdown (<c>fish_ticks</c>, the fisher's <c>ActionTicksRemaining</c>) that ends in a catch every time; the drawing follows
/// it, so the splash lands when the fish does. Nothing here is written back.
/// </remarks>
public partial class VillageMap
{
    /// <summary>The fisher stands this far out along the dock (its far end is <see cref="DockOut"/> + half its length), and as far
    /// aside as the dock is.</summary>
    internal const float DockStandOut = DockOut + (DockLength / 2f) - 0.15f;

    /// <summary>Real seconds the throw takes at the cast's start, and the catch at its end (§3, as mocked).</summary>
    internal const float CastSeconds = 1.4f, CatchSeconds = 2.0f;

    /// <summary>A cast shorter than this in real seconds (a fast game) shows only the wait and the splash: nobody flails (§1.2).</summary>
    internal const float QuickCastSeconds = 5f;

    /// <summary>The rod dips once in this many seconds while the float waits; the dip itself is the first share of it.</summary>
    private const float DipSeconds = 2.4f, DipShare = 0.25f;

    internal enum CastVersion
    {
        SmallFish,
        BigFish,
        Net,
    }

    internal enum CastPart
    {
        Throw,
        Wait,
        Catch,
    }

    /// <summary>Where in a cast a fisher is: the pose to paint, the part, how far through it (0–1), the version, and whether the
    /// game runs too fast for the throw and the haul.</summary>
    internal readonly record struct CastMoment(int Beat, CastPart Part, float Within, CastVersion Version, bool Quick, float WaitSeconds);

    /// <summary>A fisher's cast this frame: the moment, which way the water is on screen, where the float lands, and the facing.</summary>
    private readonly record struct Cast(CastMoment Moment, Vector2 Spot, Vector2 Way, Vector2 Landing, bool Flip, ulong Start);

    /// <summary>Pure: the dock's standing place in the hut's own frame, for the side its water is on.</summary>
    internal static Vector2 DockStandLocal(int water)
    {
        Vector2 way = SideOut(water);
        var side = new Vector2(-way.Y, way.X);
        return (way * DockStandOut) + (side * DockAside);
    }

    /// <summary>
    /// ⭐ Pure: the cast's version, from the person and the tick the cast began — so two fishers never repeat each other and a reload
    /// shows the same cast again. Half the casts a small fish, a quarter a big one, a quarter the net.
    /// </summary>
    internal static CastVersion VersionOf(int id, ulong startTick)
    {
        float r = PersonPainter.Hash01(unchecked((int)(startTick * 2654435761UL) ^ (id * 40503)), "cast");
        return r < 0.5f ? CastVersion.SmallFish : r < 0.75f ? CastVersion.BigFish : CastVersion.Net;
    }

    /// <summary>
    /// ⭐ Pure: the moment of a cast <paramref name="elapsedTicks"/> into its <paramref name="lengthTicks"/>, at
    /// <paramref name="secondsPerTick"/> real seconds a tick — the throw in its first <see cref="CastSeconds"/>, the catch in its
    /// last <see cref="CatchSeconds"/>, the wait between; a quick cast (<see cref="QuickCastSeconds"/>) waits and splashes only.
    /// </summary>
    internal static CastMoment CastAt(CastVersion version, float elapsedTicks, int lengthTicks, double secondsPerTick)
    {
        double cycle = lengthTicks * secondsPerTick;
        double t = Math.Clamp(elapsedTicks, 0f, lengthTicks) * secondsPerTick;
        double left = cycle - t;
        bool net = version == CastVersion.Net;
        int Waiting(double w) => net ? PersonPainter.NetThrow : (w % DipSeconds) < DipSeconds * DipShare ? PersonPainter.RodDip : PersonPainter.RodWait;

        if (cycle < QuickCastSeconds)
        {
            float quickCatch = (float)Math.Min(0.5, cycle * 0.3);
            return left < quickCatch
                ? new CastMoment(Waiting(t), CastPart.Catch, (float)(1.0 - (left / quickCatch)), version, true, (float)t)
                : new CastMoment(Waiting(t), CastPart.Wait, (float)(t / Math.Max(1e-6, cycle - quickCatch)), version, true, (float)t);
        }

        if (t < CastSeconds)
        {
            float u = (float)(t / CastSeconds);
            int beat = net ? PersonPainter.NetThrow : u < 0.3f ? PersonPainter.RodBack : u < 0.55f ? PersonPainter.RodForward : PersonPainter.RodWait;
            return new CastMoment(beat, CastPart.Throw, u, version, false, 0f);
        }

        if (left < CatchSeconds)
        {
            float c = (float)(1.0 - (left / CatchSeconds));
            int beat = net ? PersonPainter.NetThrow
                : c < 0.15f ? PersonPainter.RodDip
                : c < 0.35f ? (version == CastVersion.BigFish ? PersonPainter.RodBent : PersonPainter.RodStrike)
                : PersonPainter.RodHaul;
            return new CastMoment(beat, CastPart.Catch, c, version, false, (float)(t - CastSeconds));
        }

        double wait = t - CastSeconds;
        return new CastMoment(Waiting(wait), CastPart.Wait, (float)(wait / Math.Max(1e-6, cycle - CastSeconds - CatchSeconds)), version, false, (float)wait);
    }

    // ------------------------------------------------------------------ the dock's end, by the hut the fisher stands on

    /// <summary>Each fishing hut by where it stands (its view tile) — kept, and laid again only when the workplaces change.</summary>
    private readonly Dictionary<Vector2, int> _fishingHutAt = new();

    /// <summary>What the table was laid from: how many workplaces, the newest's id, and the tick (a miss lays it again once a tick).</summary>
    private (int Count, int Newest, ulong Tick) _fishingHutsFrom = (-1, -1, ulong.MaxValue);

    /// <summary>The dock's standing place and the way to the water (view tiles) for a fisher standing on a hut's spot; null when the
    /// hut has no water by it.</summary>
    private (Vector2 Spot, Vector2 Way)? FishingSpotOf(SimWorld world, Vector2 viewTile)
    {
        int newest = world.Workplaces.Count == 0 ? 0 : world.Workplaces[^1].Id;
        bool stale = _fishingHutsFrom.Count != world.Workplaces.Count || _fishingHutsFrom.Newest != newest;
        if (stale || (!_fishingHutAt.ContainsKey(viewTile) && _fishingHutsFrom.Tick != world.Tick))
        {
            // ⚠️ A hut moved keeps the count and the ids, so a fisher found on no hut's spot lays the table again — once a tick.
            _fishingHutAt.Clear();
            foreach (Workplace w in world.Workplaces)
            {
                if (w.Kind == JobKind.Fisher)
                {
                    _fishingHutAt[InViewTiles(w.Position)] = w.Id;
                }
            }

            _fishingHutsFrom = (world.Workplaces.Count, newest, world.Tick);
        }

        if (!_fishingHutAt.TryGetValue(viewTile, out int id) || world.FindWorkplace(id) is not Workplace hut)
        {
            return null;
        }

        sbyte water = WaterSideOf(world, hut);
        if (water < 0)
        {
            return null;
        }

        float radians = hut.Facing.Raw * Mathf.Tau / 65536f;
        Vector2 centre = InViewTiles(hut.Position);
        return (centre + DockStandLocal(water).Rotated(radians), SideOut(water).Rotated(radians));
    }

    /// <summary>
    /// A fisher's cast this frame, or null: fishing, rod in hand, facing us, and standing on a dock. The cast's length is the sim's
    /// own (<c>WorkTicksFor</c>: the tool and the mastery shorten it), so where they are in it is the countdown read back.
    /// </summary>
    private Cast? CastOf(SimWorld world, Villager villager, FigureKey still)
    {
        if (villager.State != VillagerState.Fishing || still.Trade != JobKind.Fisher || still.Hold != FigureHold.Tool || still.Walking)
        {
            return null;
        }

        if (FishingSpotOf(world, InViewTiles(villager.Position)) is not (Vector2 spot, Vector2 way))
        {
            return null;
        }

        int length = Math.Max(1, world.WorkTicksFor(villager, JobKind.Fisher, world.Config.FishTicks));
        int remaining = Math.Clamp(villager.ActionTicksRemaining, 0, length);
        float elapsed = length - remaining + (float)_alpha;
        ulong start = world.Tick - (ulong)(length - remaining);
        double secondsPerTick = 1.0 / (world.Config.TargetTicksPerSecond * Math.Max(0.05, _flowPace));
        CastVersion version = VersionOf(villager.Id, start);
        CastMoment moment = CastAt(version, elapsed, length, secondsPerTick);

        // The float lands out on the water: a little further for some casts, and nearer if the water ends first.
        float reach = 0.8f + (0.5f * PersonPainter.Hash01(unchecked((int)start ^ villager.Id), "reach"));
        var aside = new Vector2(-way.Y, way.X) * ((PersonPainter.Hash01(unchecked((int)start ^ villager.Id), "aside") - 0.5f) * 0.4f);
        Vector2 landing = spot + (way * reach) + aside;
        var tile = new GridPos(Mathf.RoundToInt(landing.X), Mathf.RoundToInt(landing.Y));
        if (!world.Map.Contains(tile) || world.Map.TerrainAt(tile) != Terrain.Water)
        {
            landing = spot + (way * 0.45f);
        }

        return new Cast(moment, spot, way, landing, way.X < -0.1f, start);
    }

    // ------------------------------------------------------------------ the drawing

    // The fish wears the yard's own silver (the fish rack's, A86).
    private static readonly Color LineColour = new("#ece4d0"), FloatRed = new("#c8463a"),
        FishShade = new("#96a0aa"), Splash = new("#ffffff"), NetCord = new("#463a2c"), CastInk = new("#2a2018");

    /// <summary>Under the fisher: the rings where the float sits, the splash, the net on the water.</summary>
    private void DrawTheCastOnTheWater(Cast cast, Vector2 feet, float tall)
    {
        CastMoment m = cast.Moment;
        Vector2 landing = FloatPointOf(cast, feet);
        float k = tall / 30f;
        if (m.Version == CastVersion.Net && !m.Quick)
        {
            switch (m.Part)
            {
                case CastPart.Throw when m.Within > 0.4f:
                    float spread = Mathf.Clamp((m.Within - 0.4f) / 0.45f, 0f, 1f);
                    DrawTheNet(landing, (6f + (4f * spread)) * k * spread, false, k);
                    break;
                case CastPart.Wait:
                    DrawTheNet(landing, 10f * k, false, k);
                    break;
                case CastPart.Catch:
                    float c = Smooth(m.Within);
                    Vector2 at = Bezier(landing, ((landing + feet) / 2f) - new Vector2(0f, 4f * k), feet - new Vector2(3f * k * (cast.Flip ? -1f : 1f), 4f * k), c);
                    if (m.Within < 0.4f)
                    {
                        DrawTheSplash(landing, k, m.Within / 0.4f, true);
                    }

                    DrawTheNet(at, 10f * k * (1f - (0.6f * c)), true, k);
                    break;
            }

            return;
        }

        if (m.Part == CastPart.Throw && m.Within >= 0.85f)
        {
            DrawRing(landing, (2f * k) + (4f * k * ((m.Within - 0.85f) / 0.15f)), 1f - ((m.Within - 0.85f) / 0.15f), k);
        }
        else if (m.Part == CastPart.Wait)
        {
            float ring = (m.WaitSeconds % 1.6f) / 1.6f;
            DrawRing(landing, (2f * k) + (4f * k * ring), 1f - ring, k);
        }
        else if (m.Part == CastPart.Catch)
        {
            bool big = m.Version == CastVersion.BigFish;
            float s = m.Quick ? m.Within : (m.Within - 0.15f) / 0.6f;
            if (s is > 0f and < 1f)
            {
                DrawTheSplash(landing, k, s, big);
            }
        }
    }

    /// <summary>Over the fisher: the line from the rod's tip, the float, the fish on the line.</summary>
    private void DrawTheLineAndTheCatch(Cast cast, Vector2 feet, float tall, int drawnBeat)
    {
        CastMoment m = cast.Moment;
        if (m.Version == CastVersion.Net && !m.Quick)
        {
            return;
        }

        float k = tall / 30f;
        (float tx, float ty) = PersonPainter.RodTip(drawnBeat);
        Vector2 tip = feet + (new Vector2(cast.Flip ? -tx : tx, ty) * tall);
        Vector2 landing = FloatPointOf(cast, feet);
        Vector2? floatAt = null;
        Vector2? fishAt = null;
        float fishTurn = 0f;
        bool big = m.Version == CastVersion.BigFish;
        float side = cast.Flip ? -1f : 1f;

        switch (m.Part)
        {
            case CastPart.Throw when m.Within >= 0.4f:
                float fl = Smooth((m.Within - 0.4f) / 0.45f);
                floatAt = Bezier(tip, ((tip + landing) / 2f) - new Vector2(0f, 14f * k), landing, fl);
                break;
            case CastPart.Wait:
                floatAt = landing + new Vector2(0f, Mathf.Sin(m.WaitSeconds * 2.2f) * 0.6f * k);
                break;
            case CastPart.Catch when m.Quick:
                floatAt = landing;
                break;
            case CastPart.Catch when m.Within < 0.15f:
                floatAt = landing + new Vector2(0f, 2.2f * k * (m.Within / 0.15f));
                break;
            case CastPart.Catch when m.Within < 0.35f:
                fishAt = landing;
                fishTurn = -30f * side;
                break;
            case CastPart.Catch:
                float f = Smooth((m.Within - 0.35f) / 0.65f);
                fishAt = Bezier(landing, new Vector2((landing.X + feet.X) / 2f, feet.Y - (34f * k)), feet - new Vector2(4f * k * side, 3f * k), f);
                fishTurn = (-40f + (80f * f)) * side;
                break;
        }

        Vector2? end = fishAt ?? floatAt;
        if (end is Vector2 e)
        {
            Vector2 sag = new((tip.X + e.X) / 2f, Mathf.Max(tip.Y, e.Y) + ((m.Part == CastPart.Throw ? -3f : 2f) * k));
            var line = new Vector2[13];
            for (int i = 0; i < line.Length; i++)
            {
                line[i] = Bezier(tip, sag, e, i / 12f);
            }

            DrawPolyline(line, AtNight(LineColour with { A = 0.9f }), Mathf.Max(1f, 0.45f * k), antialiased: true);
        }
        else
        {
            // Between the haul and the next throw: the line hangs from the tip, as the painted rod's did.
            DrawLine(tip, tip + new Vector2(-1f * k * side, 9f * k), AtNight(LineColour with { A = 0.9f }), Mathf.Max(1f, 0.45f * k), true);
        }

        if (floatAt is Vector2 fa)
        {
            DrawCircle(fa, Mathf.Max(1.2f, 0.9f * k) + 0.6f, AtNight(CastInk));
            DrawCircle(fa, Mathf.Max(1.2f, 0.9f * k), AtNight(FloatRed));
        }

        if (fishAt is Vector2 fish)
        {
            DrawTheFish(fish, k, big, fishTurn);
        }
    }

    /// <summary>The float's resting place on screen, from the fisher's own feet (they stand on the dock's spot), so a crowd's fan
    /// and the person's least size move the two together.</summary>
    private Vector2 FloatPointOf(Cast cast, Vector2 feet) => feet + ((cast.Landing - cast.Spot) * _pixelsPerTile);

    private void DrawRing(Vector2 c, float r, float strength, float k)
    {
        if (r <= 0f || strength <= 0f)
        {
            return;
        }

        const int n = 24;
        var ring = new Vector2[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float a = Mathf.Tau * i / n;
            ring[i] = c + new Vector2(r * Mathf.Cos(a), r * 0.45f * Mathf.Sin(a));
        }

        DrawPolyline(ring, AtNight(Splash with { A = 0.8f * strength }), Mathf.Max(1f, 0.6f * k), antialiased: true);
    }

    private void DrawTheSplash(Vector2 c, float k, float t, bool big)
    {
        int n = big ? 6 : 4;
        for (int i = 0; i < n; i++)
        {
            float a = -Mathf.Pi * (0.15f + (0.7f * i / (n - 1)));
            float r = (big ? 5f : 3.5f) * k * t;
            var p = new Vector2(c.X + (Mathf.Cos(a) * r), c.Y + (Mathf.Sin(a) * r * 1.3f) + (6f * k * t * t));
            DrawCircle(p, Mathf.Max(0.8f, (big ? 1.1f : 0.8f) * k * (1f - t)), AtNight(Splash with { A = 0.92f * (1f - t) }));
        }

        DrawRing(c, (big ? 6f : 4f) * k * (0.3f + t), 1f - t, k);
    }

    private void DrawTheFish(Vector2 c, float k, bool big, float turnDegrees)
    {
        float l = (big ? 7f : 4.5f) * k, h = (big ? 2.6f : 1.8f) * k;
        float a = Mathf.DegToRad(turnDegrees);
        Vector2 Turned(float x, float y) => c + new Vector2(x, y).Rotated(a);
        var body = new Vector2[16];
        for (int i = 0; i < body.Length; i++)
        {
            float u = Mathf.Tau * i / body.Length;
            body[i] = Turned(Mathf.Cos(u) * l / 2f, Mathf.Sin(u) * h / 2f);
        }

        Vector2[] tail = { Turned(-l / 2f, 0f), Turned((-l / 2f) - (2.2f * k), -1.6f * k), Turned((-l / 2f) - (2.2f * k), 1.6f * k) };
        DrawColoredPolygon(tail, AtNight(FishShade));
        DrawColoredPolygon(body, AtNight(FishSilver));
        var outline = new Vector2[body.Length + 1];
        body.CopyTo(outline, 0);
        outline[^1] = body[0];
        DrawPolyline(outline, AtNight(CastInk), Mathf.Max(1f, 0.4f * k), antialiased: true);
    }

    private void DrawTheNet(Vector2 c, float r, bool hauled, float k)
    {
        if (r <= 0f)
        {
            return;
        }

        Color cord = AtNight(NetCord with { A = 0.85f });
        const int n = 24;
        var rim = new Vector2[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float a = Mathf.Tau * i / n;
            rim[i] = c + new Vector2(r * Mathf.Cos(a), r * 0.5f * Mathf.Sin(a));
        }

        DrawPolyline(rim, cord, Mathf.Max(1f, 0.6f * k), antialiased: true);
        for (int i = 0; i < 8; i++)
        {
            float a = Mathf.Tau * i / 8f;
            DrawLine(c, c + new Vector2(r * Mathf.Cos(a), r * 0.5f * Mathf.Sin(a)), cord, 1f, true);
        }

        if (hauled)
        {
            for (int j = 0; j < 3; j++)
            {
                DrawTheFish(c + new Vector2((j - 1) * 2.5f * k, (j % 2) * k), k * 0.8f, false, 20f * (j - 1));
            }
        }
    }

    private static float Smooth(float t)
    {
        t = Mathf.Clamp(t, 0f, 1f);
        return t * t * (3f - (2f * t));
    }

    private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t) => ((1 - t) * (1 - t) * a) + (2 * (1 - t) * t * b) + (t * t * c);
}
