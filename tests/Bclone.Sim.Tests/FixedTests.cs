using System.Globalization;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// <see cref="Fixed"/> — Q32.32, the number type gridless is built on (`specs/gridless.md` slice 1).
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐ Every literal in this file is exact and hand-checkable</b>, which is the whole reason a
/// fixed-point type can be tested at all where a float one cannot. Q32.32 means one unit is
/// 2³² = 4,294,967,296 raw, so <c>1.5</c> is exactly 6,442,450,944 and <c>1/3</c> is exactly
/// <c>floor(2³²/3)</c> = 1,431,655,765. **No approximate comparisons appear anywhere below.**
/// </para>
/// <para>
/// <b>⛔ The corners are the point, not the round trips.</b> Anyone can make 2+2 work. What breaks
/// fixed-point code in practice is rounding on the negative side, silent overflow, and the
/// assumption that multiplication is associative — so those get the most assertions.
/// </para>
/// </remarks>
public sealed class FixedTests
{
    private readonly ITestOutputHelper _output;

    public FixedTests(ITestOutputHelper output) => _output = output;

    // ---------------------------------------------------------------
    //  Representation
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ One is 2³², and this is the cheapest high-value assertion in the file.
    /// </summary>
    /// <remarks>
    /// <b>C# masks shift counts to the width of the operand</b>, so <c>1 &lt;&lt; 32</c> is
    /// <b>1</b>, not 4,294,967,296 — and it compiles without a warning. Writing
    /// <c>One = 1 &lt;&lt; 32</c> instead of <c>1L &lt;&lt; 32</c> would make every distance in the
    /// game wrong by a factor of four billion, and every other test here would fail in a way that
    /// pointed at the operators rather than at the constant. *Pin the constant so the failure
    /// names itself.*
    /// </remarks>
    [Fact]
    public void OneIsTwoToThe32()
    {
        Assert.Equal(4_294_967_296L, Fixed.One.RawBits);
        Assert.Equal(32, Fixed.FractionBits);
    }

    [Fact]
    public void ZeroIsTheDefault()
    {
        // Slice 3 will hold Fixed in arrays, which zero-initialise. "Zero means the origin, not
        // invalid" has to be a guarantee rather than a coincidence.
        Assert.Equal(Fixed.Zero, default);
        Assert.Equal(0L, Fixed.Zero.RawBits);

        // Two's complement has no negative zero, so every route to zero is the same 64 bits.
        // A float-based Point could never promise this, and neither could a type with a NaN.
        Assert.Equal(Fixed.Zero, Fixed.FromInt(0));
        Assert.Equal(Fixed.Zero, Fixed.FromRatio(0, 7));
        Assert.Equal(Fixed.Zero, -Fixed.Zero);
    }

    [Fact]
    public void RawBitsRoundTrip()
    {
        foreach (long raw in new[] { 0L, 1L, -1L, 4_294_967_296L, -6_442_450_944L, long.MaxValue, long.MinValue })
        {
            Assert.Equal(raw, Fixed.FromRawBits(raw).RawBits);
        }
    }

    /// <summary>⭐ The integer part of a Q32.32 is always exactly an <c>int</c>.</summary>
    /// <remarks>
    /// Worth pinning because it retires a whole class of worry from slice 3: converting a
    /// <c>Fixed</c> position to a tile index can never overflow, whatever the value.
    /// </remarks>
    [Fact]
    public void ToIntCanNeverOverflow()
    {
        Assert.Equal(int.MinValue, Fixed.MinValue.ToInt());
        Assert.Equal(int.MaxValue, Fixed.MaxValue.ToInt());
    }

    // ---------------------------------------------------------------
    //  Rounding — floor, and the negative side is where it matters
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ FLOOR, NOT TRUNCATION — and −1.5 is the line that tells them apart.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Truncation toward zero has a FOLD at the origin:</b> −0.5 and +0.5 both become 0, so the
    /// bucket containing zero is twice as wide as every other bucket. Floor has a bias instead,
    /// and keeps <c>ToInt(x+1) == ToInt(x)+1</c> for every x.
    /// </para>
    /// <para>
    /// <b>⛔ This is not hypothetical here.</b> <c>GeneratedMap</c> carries <c>MinX</c>/<c>MinY</c>
    /// and the valley straddles its founding site, so coordinates genuinely go negative — the fold
    /// would sit exactly where the village is. Floor is also the only correct <em>tile index</em>
    /// for a negative coordinate, and <c>Point → GridPos</c> is the load-bearing conversion of the
    /// whole gridless direction; two rounding conventions would put a seam right there.
    /// </para>
    /// <para>
    /// ⚠️ It does not contradict <c>TravelCostField.TicksForCost</c>, which truncates so that
    /// *"a sub-tile remainder is free"* — for non-negative values floor and truncation are the same
    /// operation. Floor is that rule, extended to the case where the value can be negative.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(-3, 2, -2)]  // -1.5 -> -2   ⛔ truncation says -1
    [InlineData(-1, 2, -1)]  // -0.5 -> -1   ⛔ truncation says 0
    [InlineData(1, 2, 0)]    //  0.5 ->  0
    [InlineData(3, 2, 1)]    //  1.5 ->  1
    [InlineData(-2, 1, -2)]  // exact negatives are unaffected
    [InlineData(2, 1, 2)]
    public void ToIntFloorsTowardNegativeInfinity(int numerator, int denominator, int expected)
    {
        Fixed value = Fixed.FromRatio(numerator, denominator);

        Assert.Equal(expected, value.ToInt());
        Assert.Equal(expected, value.Floor().ToInt());

        // Floor() lands on an exact integer, so it must round-trip.
        Assert.Equal(Fixed.FromInt(expected), value.Floor());
    }

    [Fact]
    public void HalvesAndIntegersAreExact()
    {
        Assert.Equal(6_442_450_944L, Fixed.FromRatio(3, 2).RawBits);
        Assert.Equal(-6_442_450_944L, Fixed.FromRatio(-3, 2).RawBits);
        Assert.Equal(4_294_967_296L, Fixed.FromInt(1).RawBits);
        Assert.Equal(-4_294_967_296L, Fixed.FromInt(-1).RawBits);
        Assert.Equal(1_073_741_824L, Fixed.FromRatio(1, 4).RawBits);
    }

    /// <summary>
    /// ⭐ A third is not representable, and floor decides which way it misses — both times, down.
    /// </summary>
    /// <remarks>
    /// <b>This is what makes <c>FromRatio(1,3)</c> documentable</b> as "the largest representable
    /// value not exceeding one third" rather than the useless word "approximately". The negative
    /// pair is one ULP further from zero than a truncating implementation would produce, and that
    /// single bit is the whole difference between the two policies.
    /// </remarks>
    [Fact]
    public void AThirdMissesDownwardOnBothSidesOfZero()
    {
        Assert.Equal(1_431_655_765L, Fixed.FromRatio(1, 3).RawBits);
        Assert.Equal(-1_431_655_766L, Fixed.FromRatio(-1, 3).RawBits);

        // The sign may be carried by either operand and must not change the answer — a truncating
        // implementation gets these two different, which is how sign bugs hide.
        Assert.Equal(Fixed.FromRatio(-1, 3), Fixed.FromRatio(1, -3));

        // Division agrees with FromRatio, or the type has two rounding rules.
        Assert.Equal(Fixed.FromRatio(1, 3), Fixed.FromInt(1) / Fixed.FromInt(3));
        Assert.Equal(-1_431_655_766L, (Fixed.FromInt(-1) / Fixed.FromInt(3)).RawBits);
    }

    /// <summary>⭐ A third times three is not one, and it misses downward from both directions.</summary>
    [Fact]
    public void AThirdTimesThreeFallsShortAndSaysSoInBothDirections()
    {
        Fixed positive = Fixed.FromRatio(1, 3) * Fixed.FromInt(3);
        Fixed negative = Fixed.FromRatio(-1, 3) * Fixed.FromInt(3);

        _output.WriteLine($"1/3*3 = {positive} (raw {positive.RawBits}); -1/3*3 = {negative} (raw {negative.RawBits})");

        Assert.Equal(4_294_967_295L, positive.RawBits);   // one ULP below 1.0
        Assert.Equal(0, positive.ToInt());

        Assert.Equal(-4_294_967_298L, negative.RawBits);  // two ULP below -1.0
        Assert.Equal(-2, negative.ToInt());
    }

    // ---------------------------------------------------------------
    //  The algebra you do not get
    // ---------------------------------------------------------------

    /// <summary>
    /// ⛔⛔ MULTIPLICATION IS NOT ASSOCIATIVE — reassociating a product CHANGES THE SIM.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every product floors, so the discarded bits depend on the order the products are taken in.
    /// This is not a curiosity; it is a <b>hazard notice</b>. A refactor that rewrites
    /// <c>(a * b) * c</c> as <c>a * (b * c)</c> — the kind of change that looks like tidying —
    /// produces a different world, and the determinism suite will not object because both runs
    /// agree with themselves.
    /// </para>
    /// <para>
    /// ⚠️ The triple you would reach for first, ⅓·⅓·3, happens to be associative under floor, so
    /// it proves nothing. This one does not.
    /// </para>
    /// </remarks>
    [Fact]
    public void MultiplicationIsNotAssociativeAndThatIsAHazardNotACuriosity()
    {
        Fixed a = Fixed.FromRawBits(3);
        Fixed b = Fixed.FromRatio(1, 3);
        Fixed c = Fixed.FromInt(3);

        long left = ((a * b) * c).RawBits;
        long right = (a * (b * c)).RawBits;

        _output.WriteLine($"(a*b)*c = {left}; a*(b*c) = {right}");

        Assert.Equal(0L, left);
        Assert.Equal(2L, right);
        Assert.NotEqual(left, right);
    }

    [Fact]
    public void AdditionIsAssociativeAndCommutative()
    {
        Fixed a = Fixed.FromRatio(1, 3);
        Fixed b = Fixed.FromRatio(5, 7);
        Fixed c = Fixed.FromRatio(-9, 11);

        // Addition is exact in fixed point — nothing is discarded — so these hold outright.
        Assert.Equal((a + b) + c, a + (b + c));
        Assert.Equal(a + b, b + a);
        Assert.Equal(a - b, -(b - a));
    }

    // ---------------------------------------------------------------
    //  Overflow — loudly, never silently
    // ---------------------------------------------------------------

    /// <summary>
    /// ⛔⛔ Overflow throws. Wrapping would be silently wrong IDENTICALLY ON BOTH MACHINES.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All three policies — wrap, saturate, throw — are equally deterministic, so determinism does
    /// not choose here; the failure mode does. <b>Unchecked wrap is the worst option available and
    /// specifically worse than the same bug in a non-deterministic engine:</b> two runs would
    /// agree perfectly and both be nonsense, so the determinism suite stays green while a villager
    /// teleports across the valley. Saturation is silently wrong too, and destroys the algebra
    /// invisibly.
    /// </para>
    /// <para>
    /// ⚠️ <b>"But a throw inside a tick crashes the player's game"</b> — it does, and that is
    /// already this sim's house policy: <c>TravelCostField.TicksForCost</c>,
    /// <c>DeterministicRandom.NextUInt(0)</c> and <c>SimConfig</c> validation all throw from inside
    /// a tick. <c>Fixed</c> joins an existing, consistent rule rather than inventing a risk.
    /// *What the game should DO when a tick throws is a real and separate question, and it applies
    /// equally to those three.*
    /// </para>
    /// </remarks>
    [Fact]
    public void OverflowThrowsRatherThanWrapping()
    {
        Assert.Throws<OverflowException>(() => Fixed.MaxValue + Fixed.FromRawBits(1));
        Assert.Throws<OverflowException>(() => Fixed.MinValue - Fixed.FromRawBits(1));
        Assert.Throws<OverflowException>(() => -Fixed.MinValue);
        Assert.Throws<OverflowException>(() => Fixed.MaxValue * Fixed.FromInt(2));
        Assert.Throws<OverflowException>(() => Fixed.MaxValue / Fixed.FromRatio(1, 2));
    }

    /// <summary>⭐ Anti-vacuity (D7): the check is not simply "throw always".</summary>
    [Fact]
    public void ArithmeticThatFitsDoesNotThrow()
    {
        Assert.Equal(Fixed.MaxValue, Fixed.MaxValue + Fixed.Zero);
        Assert.Equal(Fixed.MinValue, Fixed.MinValue - Fixed.Zero);
        Assert.Equal(Fixed.MaxValue, Fixed.MaxValue * Fixed.One);
        Assert.Equal(Fixed.MaxValue, Fixed.MaxValue / Fixed.One);
    }

    [Fact]
    public void DividingByZeroThrows()
    {
        Assert.Throws<DivideByZeroException>(() => Fixed.FromInt(1) / Fixed.Zero);
        Assert.Throws<ArgumentOutOfRangeException>(() => Fixed.FromRatio(1, 0));
    }

    /// <summary>METHODOLOGY §4's "with context" half — the message has to name the operands.</summary>
    /// <remarks>
    /// <c>OverflowException</c>'s default text is *"Arithmetic operation resulted in an
    /// overflow."*, which is useless at tick four million. The house shape is
    /// <c>TravelCostField</c>'s <c>$"... (got {x})"</c>.
    /// </remarks>
    [Fact]
    public void TheOverflowMessageNamesTheOperands()
    {
        OverflowException error = Assert.Throws<OverflowException>(
            () => Fixed.MaxValue * Fixed.FromInt(2));

        _output.WriteLine(error.Message);

        Assert.Contains(Fixed.MaxValue.ToString(), error.Message, StringComparison.Ordinal);
        Assert.Contains(Fixed.FromInt(2).ToString(), error.Message, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------
    //  Ordering
    // ---------------------------------------------------------------

    /// <summary>
    /// <c>record struct</c> hands you <c>==</c> and <c>!=</c> but not <c>&lt;</c>.
    /// </summary>
    [Fact]
    public void OrderingWorksAcrossZero()
    {
        Fixed low = Fixed.FromRatio(-3, 2);
        Fixed mid = Fixed.Zero;
        Fixed high = Fixed.FromRatio(3, 2);

        Assert.True(low < mid);
        Assert.True(mid < high);
        Assert.True(high > low);
        // Reflexivity via a separately built equal value — `low <= low` is a compiler warning,
        // and it would also be a weaker claim: this proves the operators read the value, not the
        // reference.
        Fixed sameAsLow = Fixed.FromRatio(-3, 2);
        Assert.True(low <= sameAsLow);
        Assert.True(low >= sameAsLow);
        Assert.False(low < sameAsLow);
        Assert.False(low > mid);

        Assert.True(low.CompareTo(high) < 0);
        Assert.True(high.CompareTo(low) > 0);
        Assert.Equal(0, mid.CompareTo(Fixed.Zero));
    }

    // ---------------------------------------------------------------
    //  Formatting — the locale is "the third door"
    // ---------------------------------------------------------------

    /// <summary>
    /// ⚠️ −1.5 has floor −2 and fraction .5, so naive formatting prints "-2.5".
    /// </summary>
    /// <remarks>
    /// The magnitude has to be formatted and the sign prepended — which means taking
    /// <c>Math.Abs</c> of the raw bits, which <b>overflows at <c>MinValue</c></b>. Both halves are
    /// asserted here because both are one-line mistakes.
    /// </remarks>
    [Fact]
    public void NegativeValuesFormatFromTheirMagnitude()
    {
        Assert.Equal("-1.5", Fixed.FromRatio(-3, 2).ToString());
        Assert.Equal("1.5", Fixed.FromRatio(3, 2).ToString());
        Assert.Equal("0", Fixed.Zero.ToString());
        Assert.Equal("-2", Fixed.FromInt(-2).ToString());

        // Must not throw: Math.Abs(long.MinValue) does.
        string extreme = Fixed.MinValue.ToString();
        _output.WriteLine($"MinValue formats as {extreme}");
        Assert.StartsWith("-", extreme, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⭐ The machine's locale is the third door, after the wall clock and the unseeded RNG.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Copied from <c>NumbersTests.TheLocaleCannotChangeWhatALogSays</c>, which exists because
    /// <c>Core/Numbers.cs</c> had to be written for exactly this. A German machine renders a
    /// decimal point as a comma, and a number in a log or a save file that changes shape by
    /// machine is the same class of bug as a float that rounds differently.
    /// </para>
    /// <para>
    /// ⛔⛔ <b>ITS RED CHECK SCORED ZERO, AND THAT IS RECORDED RATHER THAN QUIETLY PASSED (D317).</b>
    /// Swapping every <c>InvariantCulture</c> in <c>Fixed.ToString</c> for <c>CurrentCulture</c>
    /// leaves this test — and all twenty-four — <b>green</b>. **So today it guards nothing.**
    /// </para>
    /// <para>
    /// <b>The reason is that the implementation is culture-proof by construction rather than by
    /// the argument it passes:</b> it formats two <em>integers</em> with no format specifier and
    /// joins them with a <b>literal <c>'.'</c></b>. No culture changes the digits of a plain
    /// integer, and the separator never goes near a <c>NumberFormatInfo</c>. The
    /// <c>InvariantCulture</c> arguments are correct and worth keeping, and they are currently
    /// decorative.
    /// </para>
    /// <para>
    /// ⭐ <b>It is kept because it is a ratchet, not a discovery</b> — the same standing as
    /// <c>FloatBanTests</c>. The day somebody reformats the fraction through a <c>decimal</c>, an
    /// <c>"F6"</c> specifier or anything else that consults <c>NumberFormatInfo</c>, this fires.
    /// *A guard that cannot fail today but will fail the day the risk appears is worth having, as
    /// long as nobody mistakes it for evidence that the risk was faced.*
    /// </para>
    /// </remarks>
    [Fact]
    public void TheLocaleCannotChangeWhatAFixedLooksLike()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal("-1.5", Fixed.FromRatio(-3, 2).ToString());

            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            Assert.Equal("-1.5", Fixed.FromRatio(-3, 2).ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // ---------------------------------------------------------------
    //  Determinism, and the hash
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐⭐ A scripted run of fixed-point arithmetic, folded through the state hash and pinned.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is `specs/gridless.md` slice 1's *"determinism test extended to fixed-point
    /// arithmetic"*</b>, and it is honest about what it proves: it is a <em>self-captured</em>
    /// vector, so it demonstrates <b>stability</b>, not correctness. Correctness is what every
    /// hand-checkable assertion above is for.
    /// </para>
    /// <para>
    /// ⚠️ <b>And nothing in this process can prove the number is the same on another machine.</b>
    /// What makes it so is that every operation is integer and there is no <c>double</c> anywhere
    /// near the type — which is asserted structurally by <c>FloatBanTests</c>, not here. *The two
    /// tests are halves of one claim and neither is worth much alone.*
    /// </para>
    /// </remarks>
    [Fact]
    public void AScriptedRunOfFixedArithmeticHashesTheSameEveryTime()
    {
        Assert.Equal(Replay(), Replay());

        ulong hash = Replay();
        _output.WriteLine($"fixed-point replay hash: {hash}UL");

        // Pinned. If this moves, the arithmetic moved — say why in one sentence, here (D152).
        Assert.Equal(18_274_212_356_918_558_434UL, hash);

        static ulong Replay()
        {
            var rng = new DeterministicRandom(20260906UL);
            ulong hash = 0UL;
            Fixed running = Fixed.One;

            for (int i = 0; i < 500; i++)
            {
                // Bounded so the sequence exercises rounding rather than the overflow guard.
                Fixed operand = Fixed.FromRatio(rng.NextInt(-1000, 1000), rng.NextInt(1, 64));

                running += operand;
                running = running * Fixed.FromRatio(3, 4);
                running = running / Fixed.FromRatio(7, 5);

                hash = StateHash.MixFixed(hash, running);
            }

            return hash;
        }
    }

    /// <summary>
    /// ⛔ The hash sees the FRACTION. Two positions a sub-quantum apart must not collide.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the assertion that makes "raw bits, not quantised" a fact rather than an opinion
    /// in a comment.</b> <c>specs/gridless.md §5</c> once said positions hash as *"quantised
    /// fixed-point bits"*, which came from D303 — where quantisation is right, because that hash is
    /// a <em>pseudo-random source</em> for the map generator and nearby queries are supposed to
    /// land in one bucket.
    /// </para>
    /// <para>
    /// <b><c>StateHash</c> is the opposite kind of thing: a fingerprint.</b> Its entire contract is
    /// that any differing state byte differs the hash. A quantising <c>MixFixed</c> would let two
    /// worlds whose villagers stand a fraction apart hash identically — **the determinism suite
    /// would go green across a real divergence**, which is the trap every sparse block in
    /// <c>StateHash</c> is written to avoid.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheHashDistinguishesValuesThatDifferOnlyInTheirFraction()
    {
        Fixed whole = Fixed.One;
        Fixed aHairMore = Fixed.FromRawBits(Fixed.One.RawBits + 1);

        Assert.NotEqual(whole, aHairMore);
        Assert.Equal(whole.ToInt(), aHairMore.ToInt());

        Assert.NotEqual(
            StateHash.MixFixed(0UL, whole),
            StateHash.MixFixed(0UL, aHairMore));
    }
}
