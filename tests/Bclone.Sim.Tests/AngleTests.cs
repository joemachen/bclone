using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// <see cref="Angle"/> — a 16-bit binary angle, and the sim's first trigonometry (gridless 2a).
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐ Joe specified the representation and the reasoning behind it</b> (2026-09-06): *"a 16-bit
/// unsigned integer fixed-point angle. Range 0 to 65,535 mapping linearly to 0° through 360°.
/// Precision ≈0.0055° per step. 100% hashable, uses only 2 bytes, has zero float-precision drift,
/// and looks completely continuous to the human eye on screen."*
/// </para>
/// <para>
/// <b>⛔ The reason this needed a slice of its own is that the sim had NO trigonometry at all</b>,
/// and it may not borrow the runtime's: IEEE-754 requires correct rounding for <c>+ - * /</c> and
/// <c>sqrt</c> and <b>not</b> for the transcendentals, so <c>Math.Sin</c> is not guaranteed
/// bit-identical across platforms or runtime versions. **Sim code may never call it.** These tests
/// may, and do — a float in a test is fine, and it is how the checked-in table is proved honest.
/// </para>
/// </remarks>
public sealed class AngleTests
{
    private readonly ITestOutputHelper _output;

    public AngleTests(ITestOutputHelper output) => _output = output;

    // ---------------------------------------------------------------
    //  Representation
    // ---------------------------------------------------------------

    [Fact]
    public void ZeroIsTheDefault()
    {
        Assert.Equal(Angle.Zero, default);
        Assert.Equal(0, Angle.Zero.Raw);
    }

    /// <summary>⭐ 360 divides 65536 exactly at the angles anybody actually types.</summary>
    /// <remarks>
    /// A full turn is 2¹⁶, so quarters, eighths and sixteenths are exact. <c>FromDegrees(1)</c> is
    /// not — 65536/360 is 182.04 — and that is stated rather than hidden: **degrees are a human
    /// convenience and the raw value is the truth.**
    /// </remarks>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(45, 8192)]
    [InlineData(90, 16384)]
    [InlineData(180, 32768)]
    [InlineData(270, 49152)]
    [InlineData(360, 0)]      // a full turn is zero
    [InlineData(-90, 49152)]  // negative wraps
    [InlineData(450, 16384)]  // more than a turn wraps
    public void DegreesMapOntoTheCircleAndWrap(int degrees, int expectedRaw)
    {
        Assert.Equal(expectedRaw, Angle.FromDegrees(degrees).Raw);
    }

    [Fact]
    public void TurnFractionsAreExact()
    {
        Assert.Equal(Angle.FromDegrees(90), Angle.FromTurnFraction(1, 4));
        Assert.Equal(Angle.FromDegrees(180), Angle.FromTurnFraction(1, 2));
        Assert.Equal(Angle.FromDegrees(270), Angle.FromTurnFraction(3, 4));
        Assert.Equal(Angle.Zero, Angle.FromTurnFraction(1, 1));
    }

    // ---------------------------------------------------------------
    //  Wraparound — the property that makes this type total
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ Wraparound is the type's ARITHMETIC, not a correction applied to it.
    /// </summary>
    /// <remarks>
    /// A <c>ushort</c> rolling over at 65536 <em>is</em> a circle closing at 360°, so there is no
    /// <c>if (angle &gt; 2π) angle -= 2π</c> anywhere and there never will be. **That whole class
    /// of bug does not exist in this type** — which is the payoff of the representation Joe chose,
    /// and the sharpest contrast with <c>Fixed</c>, where the equivalent boundary throws.
    /// </remarks>
    [Fact]
    public void TheCircleCloses()
    {
        Assert.Equal(Angle.Zero, Angle.FromRaw(65535) + Angle.FromRaw(1));
        Assert.Equal(Angle.FromRaw(65535), Angle.Zero - Angle.FromRaw(1));

        // Three quarters plus a half is a turn and a quarter, which is a quarter.
        // ⚠️ Stated in TURN FRACTIONS rather than degrees on purpose — see the next test for why
        // the degree spelling of this same sum comes out one step short.
        Assert.Equal(
            Angle.FromTurnFraction(1, 4),
            Angle.FromTurnFraction(3, 4) + Angle.FromTurnFraction(1, 2));

        // Three hundred and sixty one-degree steps land exactly back at the start — which is only
        // true because a degree is not stored as a degree.
        Angle walked = Angle.Zero;
        for (int i = 0; i < 360; i++)
        {
            walked += Angle.FromDegrees(1);
        }

        _output.WriteLine($"360 one-degree steps land on raw {walked.Raw}");

        // ⚠️ NOT zero, and that is the honest answer: FromDegrees(1) floors to 182 raw, so 360 of
        // them are 65,520 — twelve steps short of a full turn, about 0.088°. The exactness above
        // belongs to quarters and eighths, never to arbitrary degrees.
        Assert.Equal(65520, walked.Raw);
    }

    /// <summary>
    /// ⚠️ DEGREES DO NOT ADD. Two floored conversions floor twice, and the answer is a step short.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This was found by the guard above failing, and it is worth its own test rather than a
    /// footnote.</b> <c>FromDegrees(300) + FromDegrees(150)</c> is 450° and ought to be a right
    /// angle — it comes out **89°**. Each conversion floors 182.04 steps-per-degree down to 182,
    /// and two floors are two errors: 54,613 + 27,306 wraps to 16,383, one step under the 16,384 a
    /// right angle actually is.
    /// </para>
    /// <para>
    /// ⛔ <b>Nothing is broken; the type is doing exactly what it says.</b> The lesson is the one
    /// <c>FromDegrees</c>'s own remarks state — *degrees are a human convenience and the raw value
    /// is the truth* — and the fix for any caller that cares is <c>FromTurnFraction</c>, which is
    /// exact for every denominator that divides 65,536. **Pinned here so that a future session
    /// meeting an off-by-one degree reads a documented property instead of hunting a bug.**
    /// </para>
    /// </remarks>
    [Fact]
    public void AddingDegreesAddsTheirRoundingErrorsToo()
    {
        Angle viaDegrees = Angle.FromDegrees(300) + Angle.FromDegrees(150);
        Angle exact = Angle.FromTurnFraction(1, 4);

        _output.WriteLine(
            $"300° + 150° = {viaDegrees} (raw {viaDegrees.Raw}); a true right angle is raw {exact.Raw}");

        Assert.Equal(16383, viaDegrees.Raw);
        Assert.Equal(16384, exact.Raw);
        Assert.NotEqual(exact, viaDegrees);

        // And the error is one step — about 0.0055° — not something that compounds into nonsense.
        Assert.Equal(1, exact.Raw - viaDegrees.Raw);
    }

    /// <summary>
    /// ⚠️ Nothing an <see cref="Angle"/> can be asked to do can throw.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ I PREDICTED THIS GUARD WOULD SCORE ZERO ON ITS RED CHECK AND I WAS WRONG</b>, which is
    /// worth recording because the prediction was reasoned and still false. The argument was that
    /// every bit pattern is a valid angle and every operation wraps, so there is no throw path to
    /// break.
    /// </para>
    /// <para>
    /// <b>There is one, and it is the single word <c>unchecked</c>.</b> Swapping it for
    /// <c>checked</c> in <c>Angle</c>'s operators reddens this test immediately — 65,535 + 65,535
    /// overflows a <c>ushort</c> — and takes <c>CosineIsExactAtTheCardinals(270°)</c> with it,
    /// because cosine at 270° is <em>exactly</em> the wraparound case, 270° + 90° rolling to zero.
    /// *The thing I could not imagine breaking was one keyword, and it is load-bearing in two
    /// places.*
    /// </para>
    /// </remarks>
    [Fact]
    public void NoAngleArithmeticCanThrow()
    {
        Angle extreme = Angle.FromRaw(ushort.MaxValue);

        Angle sum = extreme + extreme;
        Angle difference = Angle.Zero - extreme;
        Angle negated = -extreme;

        _output.WriteLine($"{extreme.Raw} + itself = {sum.Raw}; 0 - it = {difference.Raw}; -it = {negated.Raw}");

        Assert.Equal(65534, sum.Raw);
        Assert.Equal(1, difference.Raw);
        Assert.Equal(1, negated.Raw);
    }

    // ---------------------------------------------------------------
    //  Trigonometry
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ The four cardinals are EXACT — 0, +1, 0, −1 — with no interpolation error at all.
    /// </summary>
    /// <remarks>
    /// <b>This is what the <c>w == 16384</c> guard in <c>Angle.Sin</c> buys</b>, and it is the
    /// off-by-one the whole table design turns on. Without it, 90° and 270° would be interpolated
    /// like any other angle — landing a hair under one — and the code would index one past the end
    /// of a 257-entry table. *A right angle is the angle a player is most likely to pick, so it is
    /// the one that must not be approximate.*
    /// </remarks>
    [Theory]
    [InlineData(0, 0L)]
    [InlineData(90, 4_294_967_296L)]
    [InlineData(180, 0L)]
    [InlineData(270, -4_294_967_296L)]
    public void SineIsExactAtTheCardinals(int degrees, long expectedRaw)
    {
        Assert.Equal(expectedRaw, Angle.FromDegrees(degrees).Sin().RawBits);
    }

    [Theory]
    [InlineData(0, 4_294_967_296L)]
    [InlineData(90, 0L)]
    [InlineData(180, -4_294_967_296L)]
    [InlineData(270, 0L)]
    public void CosineIsExactAtTheCardinals(int degrees, long expectedRaw)
    {
        // Cos is Sin(a + 90°) and nothing more — one table, one function, and the wraparound at
        // 270° + 90° does the work that a modulo would otherwise have to.
        Assert.Equal(expectedRaw, Angle.FromDegrees(degrees).Cos().RawBits);
    }

    /// <summary>
    /// ⭐⭐ The table is honest — checked against the runtime's own sine, at every one of 65,536 angles.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A float in a test is fine; a float in the sim is not.</b> <c>Math.Sin</c> cannot be
    /// called from sim code because it is not guaranteed bit-identical across platforms — but it is
    /// perfectly good as an oracle here, where the comparison is a tolerance rather than an
    /// equality and any platform's sine is accurate to far better than the bound below.
    /// </para>
    /// <para>
    /// ⛔ <b>The bound is deliberately tight enough to catch a missing interpolation.</b> Measured
    /// worst error is <b>20,213 raw</b> (4.7 × 10⁻⁶), against a theoretical <c>h²/8</c> of 20,212 —
    /// *the analysis and the implementation agree to one raw unit.* Dropping the interpolation and
    /// using the nearest table entry raises the error to about 6.1 × 10⁻³, **1,300× larger**, so a
    /// 25,000 bound reddens immediately. A loose bound here would be a guard that cannot fail.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryAngleAgreesWithRealSineToWithinTheInterpolationBound()
    {
        const long Bound = 25_000L;

        long worst = 0;
        int worstAt = 0;

        for (int raw = 0; raw <= ushort.MaxValue; raw++)
        {
            long ours = Angle.FromRaw((ushort)raw).Sin().RawBits;
            double truth = Math.Sin(raw * 2.0 * Math.PI / 65536.0) * 4_294_967_296.0;

            long error = Math.Abs(ours - (long)Math.Round(truth));
            if (error > worst)
            {
                worst = error;
                worstAt = raw;
            }
        }

        _output.WriteLine(
            $"worst error {worst} raw units at raw={worstAt} "
            + $"({worst / 4_294_967_296.0:0.000e+0} of a unit); bound is {Bound}");

        Assert.True(
            worst <= Bound,
            $"Sine is off by {worst} raw units at raw={worstAt}, over the {Bound} bound. Either the "
            + "table is wrong or the interpolation is not happening.");

        // ⭐ ANTI-VACUITY (D7): the error must be REAL, or this is comparing the table to itself.
        // A perfect zero would mean the oracle and the implementation share a bug.
        Assert.True(worst > 0, "Zero error across 65,536 angles means this is not measuring anything.");
    }

    /// <summary>The identity that catches a quadrant mapped to the wrong sign.</summary>
    /// <remarks>
    /// ⭐ Cheap, and it covers a failure the cardinals cannot see: a quadrant whose symmetry is
    /// reflected the wrong way still gives exact values at 0/90/180/270 and wrong ones between.
    /// </remarks>
    [Fact]
    public void SineSquaredPlusCosineSquaredIsOne()
    {
        double worst = 0;

        for (int raw = 0; raw <= ushort.MaxValue; raw += 7)
        {
            var angle = Angle.FromRaw((ushort)raw);
            double s = angle.Sin().RawBits / 4_294_967_296.0;
            double c = angle.Cos().RawBits / 4_294_967_296.0;
            worst = Math.Max(worst, Math.Abs((s * s) + (c * c) - 1.0));
        }

        _output.WriteLine($"worst |sin²+cos²-1| = {worst:0.000e+0}");
        Assert.True(worst < 1e-5, $"sin²+cos² drifted by {worst}, so a quadrant is mapped wrongly.");
    }

    /// <summary>Sine climbs without stumbling from 0° to 90°.</summary>
    [Fact]
    public void SineIsMonotonicThroughTheFirstQuadrant()
    {
        long previous = long.MinValue;

        for (int raw = 0; raw <= 16384; raw++)
        {
            long value = Angle.FromRaw((ushort)raw).Sin().RawBits;
            Assert.True(
                value >= previous,
                $"Sine fell from {previous} to {value} at raw={raw}, which a quarter wave never does.");
            previous = value;
        }
    }

    // ---------------------------------------------------------------
    //  Determinism
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ The table's own contents, pinned — so editing one literal by hand is caught.
    /// </summary>
    /// <remarks>
    /// The accuracy guard above would catch a badly wrong entry, but a single digit changed in the
    /// middle of 257 numbers moves the worst error by far less than the bound. **This is the guard
    /// that makes the table immutable without a stated reason** — the same standing a golden has
    /// (D152). It samples through the public API rather than reaching for the array, because the
    /// table is an implementation detail and should stay one.
    /// </remarks>
    [Fact]
    public void TheSineTableIsPinned()
    {
        ulong hash = 0UL;
        for (int raw = 0; raw <= ushort.MaxValue; raw += 64)
        {
            hash = StateHash.MixFixed(hash, Angle.FromRaw((ushort)raw).Sin());
        }

        _output.WriteLine($"sine table hash: {hash}UL");
        Assert.Equal(14_897_684_010_207_565_105UL, hash);
    }

    /// <summary>
    /// ⛔ The hash sees every step of the circle — 0.0055° apart must not collide.
    /// </summary>
    /// <remarks>
    /// D317's raw-not-quantised discipline, one type over. **And it is worth restating here because
    /// Joe raised the opposite rule from the other direction** — *"quantize for the hash, not for
    /// the state"* — which is correct for a <em>bucketing</em> hash (draw-call batching, spatial
    /// keys, sprite-facing lookup) and wrong for a <em>fingerprint</em>. <c>StateHash</c> is a
    /// fingerprint: two villages whose buildings face a hair apart are different villages.
    /// </remarks>
    [Fact]
    public void TheHashDistinguishesAdjacentAngles()
    {
        Angle a = Angle.FromDegrees(90);
        Angle b = Angle.FromRaw((ushort)(a.Raw + 1));

        Assert.NotEqual(a, b);
        Assert.NotEqual(StateHash.MixAngle(0UL, a), StateHash.MixAngle(0UL, b));
    }

    [Fact]
    public void ItReadsAsAnAngleInALog()
    {
        Assert.Equal("0°", Angle.Zero.ToString());
        Assert.Equal("90°", Angle.FromDegrees(90).ToString());
        Assert.Equal("270°", Angle.FromDegrees(270).ToString());
    }
}
