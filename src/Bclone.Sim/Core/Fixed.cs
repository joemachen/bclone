using System.Globalization;

namespace Bclone.Sim.Core;

/// <summary>
/// A fixed-point number, Q32.32 — the sim's only fractional type (D2, D317).
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐⭐ WHY THIS EXISTS AND A <c>double</c> DOES NOT.</b> The determinism contract is
/// <em>same seed + same inputs ⇒ byte-identical state</em>, and integer-only sim state is how it
/// has been guaranteed since D2. Gridless (`specs/gridless.md`) needs positions, footprints and
/// facings that are not locked to whole tiles, which is genuinely fractional math — the first time
/// this project has needed any. <b>This is the type D2 reserved for that day</b>, and it keeps the
/// contract because every operation below is integer arithmetic: no rounding mode, no FPU flags,
/// no compiler licence to reassociate, and identical results on every machine.
/// </para>
/// <para>
/// One unit is 2³² = 4,294,967,296 raw, so the integer part is a full <c>int</c> and the fraction
/// resolves to about 2.3 × 10⁻¹⁰. The valley is ~121 tiles across, so the range is enormously
/// more than the game needs — which is deliberate: <b>overflow here means a bug, not a big
/// number</b>, and that is what licenses the throwing policy below.
/// </para>
///
/// <para>
/// <b>⭐⭐ ROUNDING IS FLOOR — TOWARD NEGATIVE INFINITY — EVERYWHERE.</b> Multiply, divide,
/// <see cref="FromRatio"/>, <see cref="ToInt"/> and <see cref="Floor"/> all agree, and the
/// consistency is the point rather than the individual choice.
/// </para>
/// <para>
/// <b>Truncation toward zero was the alternative and it has a FOLD at the origin:</b> −0.5 and
/// +0.5 both become 0, so the bucket containing zero is twice as wide as every other bucket.
/// Floor has a bias instead, and a bias is a constant you can reason about where a fold is a
/// discontinuity. ⛔ <b>And it would sit exactly where the village is</b> — <c>GeneratedMap</c>
/// carries <c>MinX</c>/<c>MinY</c> and the valley straddles its founding site, so coordinates
/// genuinely go negative. Floor is also the only correct <em>tile index</em> for a negative
/// coordinate, and <c>Point → GridPos</c> is the load-bearing conversion of the entire gridless
/// direction; two rounding conventions would put a seam precisely there.
/// </para>
/// <para>
/// ⚠️ It does not contradict <c>TravelCostField.TicksForCost</c>, whose comment says truncation
/// makes *"a sub-tile remainder free"*: for non-negative values floor and truncation are the same
/// operation, so that rule is preserved byte-for-byte and merely extended to negatives.
/// </para>
///
/// <para>
/// <b>⛔⛔ OVERFLOW THROWS. It does not wrap and it does not saturate.</b> All three are equally
/// deterministic, so determinism does not choose — the failure mode does. <b>Unchecked wrap is
/// the worst option available, and specifically worse here than in a non-deterministic engine:</b>
/// two runs would agree perfectly and both be nonsense, so the determinism suite stays green while
/// a villager teleports across the valley. Saturation is silently wrong too and destroys the
/// algebra invisibly.
/// </para>
/// <para>
/// ⚠️ <b>A throw inside a tick is already this sim's house policy</b> — <c>TravelCostField</c>,
/// <c>DeterministicRandom.NextUInt(0)</c> and <c>SimConfig</c> validation all do it — so this
/// introduces no new class of risk. Messages name both operands, because METHODOLOGY §4's
/// *"with context"* is the half that gets skipped and <c>OverflowException</c>'s default text is
/// useless at tick four million. *What the game should DO when a tick throws is a real and separate
/// question, and it applies equally to those three.*
/// </para>
///
/// <para>
/// <b>⛔ NO SENTINEL, EVER.</b> Every bit pattern is a valid <c>Fixed</c>: there is no NaN, no
/// infinity, and — because two's complement has no negative zero — exactly one zero. An
/// out-of-band answer must be <c>Fixed?</c> or a separate flag, never a magic value.
/// <c>TravelCostField.Unreachable</c>'s own comment records why: *"a sentinel that takes part in
/// arithmetic silently wins nearest-thing searches."*
/// </para>
/// <para>
/// <b>⚠️ MULTIPLICATION IS NOT ASSOCIATIVE.</b> Every product floors, so the bits discarded depend
/// on the order the products are taken in. <c>(a * b) * c</c> and <c>a * (b * c)</c> can differ —
/// <c>FixedTests</c> pins a case where they do. **A refactor that reassociates a product changes
/// the sim**, and both runs will still agree with themselves, so no determinism guard will object.
/// </para>
/// <para>
/// <b>⚠️ Performance, measured at first use rather than guessed at now (D179).</b> Multiply and
/// divide widen to <see cref="Int128"/> so the intermediate is exact — <c>a * b</c> overflows
/// <c>long</c> for ordinary values. If it ever measures as hot: <c>Math.BigMul(long, long, out
/// long)</c> is a drop-in for multiply with identical floor semantics, and <c>Int128</c>
/// <em>division</em> is the real cliff (software long division). ⭐ The better fix is
/// architectural — a per-tick movement step should be computed once per path segment and
/// accumulated, never divided per villager per tick. **Nothing consumes this type yet, so its cost
/// today is exactly zero and optimising it now would be the thing D179 warns against.**
/// </para>
/// <para>
/// <b>⚠️ <see cref="RawBits"/> is the canonical wire and save form.</b> Whatever serialisation
/// this project grows, a <c>Fixed</c> is written as its <c>long</c> — never as a decimal string and
/// never as a <c>double</c>.
/// </para>
/// <para>
/// ⚠️ <c>GetHashCode</c> is .NET's, inherited from <c>record struct</c>. It is fine for a
/// dictionary lookup and must never appear in sim logic — see <c>BannedSymbols.txt</c> on
/// iterating hashed collections.
/// </para>
/// </remarks>
public readonly record struct Fixed : IComparable<Fixed>
{
    /// <summary>How many of the 64 bits are fraction.</summary>
    public const int FractionBits = 32;

    /// <summary>
    /// One, as raw bits.
    /// </summary>
    /// <remarks>
    /// ⛔ <b><c>1L</c>, never <c>1</c>.</b> C# masks a shift count to the width of its operand, so
    /// <c>1 &lt;&lt; 32</c> is <b>1</b> — it compiles without a warning and would make every value
    /// in the game wrong by a factor of four billion. <c>FixedTests.OneIsTwoToThe32</c> pins it so
    /// the failure names itself instead of pointing at the operators.
    /// </remarks>
    private const long OneRaw = 1L << FractionBits;

    private readonly long _raw;

    /// <summary>
    /// Private, so <c>new Fixed(5)</c> cannot exist.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>This type is deliberately NOT positional</b>, unlike <c>GridPos</c>. A positional
    /// record struct would give a public <c>new Fixed(5)</c> that means <b>5 ÷ 2³²</b> — roughly
    /// 0.0000000012 — while reading exactly like <see cref="FromInt"/>. *A constructor that looks
    /// like the thing a caller wants and silently means something else is a loaded gun.* The only
    /// doors are <see cref="FromInt"/>, <see cref="FromRatio"/> and <see cref="FromRawBits"/>.
    /// </remarks>
    private Fixed(long raw) => _raw = raw;

    /// <summary>The underlying Q32.32 bits — the canonical save and hash form.</summary>
    public long RawBits => _raw;

    public static Fixed Zero => new(0L);

    public static Fixed One => new(OneRaw);

    public static Fixed MinValue => new(long.MinValue);

    public static Fixed MaxValue => new(long.MaxValue);

    /// <summary>Exactly <paramref name="value"/>.</summary>
    /// <remarks>
    /// Takes an <c>int</c> rather than a <c>long</c> on purpose: an <c>int</c> always fits, so the
    /// safe case needs no range check, and the signature documents the type's integer range.
    /// </remarks>
    public static Fixed FromInt(int value) => new((long)value << FractionBits);

    /// <summary>The largest representable value not exceeding <c>numerator / denominator</c>.</summary>
    /// <remarks>
    /// ⭐ <b>"Not exceeding", not "approximately"</b> — floor makes the direction of the error a
    /// stated fact. <c>FromRatio(1, 3)</c> is 1,431,655,765 raw and <c>FromRatio(-1, 3)</c> is
    /// −1,431,655,766: both below the true value, which is what floor means on both sides of zero.
    /// Takes <c>long</c>s because a ratio legitimately needs the width its operands imply.
    /// </remarks>
    public static Fixed FromRatio(long numerator, long denominator)
    {
        if (denominator == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(denominator),
                $"Denominator cannot be zero (numerator was {numerator}).");
        }

        Int128 scaled = (Int128)numerator << FractionBits;
        return new Fixed(Narrow(FloorDivide(scaled, denominator), "FromRatio", numerator, denominator));
    }

    /// <summary>Reconstruct from <see cref="RawBits"/> — for loading and for tests.</summary>
    public static Fixed FromRawBits(long raw) => new(raw);

    /// <summary>The greatest integer not greater than this value.</summary>
    public int ToInt() => (int)(_raw >> FractionBits);

    /// <summary>This value rounded down to a whole number, still as a <see cref="Fixed"/>.</summary>
    public Fixed Floor() => new((_raw >> FractionBits) << FractionBits);

    public static Fixed operator +(Fixed left, Fixed right) =>
        new(Checked(left, right, "+", static (a, b) => (Int128)a + b));

    public static Fixed operator -(Fixed left, Fixed right) =>
        new(Checked(left, right, "-", static (a, b) => (Int128)a - b));

    public static Fixed operator -(Fixed value)
    {
        // long.MinValue has no positive counterpart; negating it is the classic silent wrap.
        if (value._raw == long.MinValue)
        {
            throw new OverflowException($"Negating {value} overflows Fixed.");
        }

        return new Fixed(-value._raw);
    }

    public static Fixed operator *(Fixed left, Fixed right)
    {
        // Widened because a * b overflows long for ordinary values: 1/3 times 3 is already
        // 1.8e19 before the shift, against long.MaxValue of 9.2e18.
        Int128 product = (Int128)left._raw * right._raw;
        return new Fixed(Narrow(product >> FractionBits, "*", left, right));
    }

    public static Fixed operator /(Fixed left, Fixed right)
    {
        if (right._raw == 0)
        {
            throw new DivideByZeroException($"Dividing {left} by zero.");
        }

        Int128 scaled = (Int128)left._raw << FractionBits;
        return new Fixed(Narrow(FloorDivide(scaled, right._raw), "/", left, right));
    }

    public static bool operator <(Fixed left, Fixed right) => left._raw < right._raw;

    public static bool operator >(Fixed left, Fixed right) => left._raw > right._raw;

    public static bool operator <=(Fixed left, Fixed right) => left._raw <= right._raw;

    public static bool operator >=(Fixed left, Fixed right) => left._raw >= right._raw;

    public int CompareTo(Fixed other) => _raw.CompareTo(other._raw);

    /// <summary>
    /// A readable decimal, invariant of culture.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Invariant, because the machine's locale is the third door</b> after the wall clock and
    /// the unseeded RNG — <c>Core/Numbers.cs</c> exists for exactly this reason, and a German
    /// machine would otherwise write a decimal comma into every log line and save file.
    /// </para>
    /// <para>
    /// ⚠️ <b>Formatted from the MAGNITUDE with the sign prepended</b>, which is not fussiness:
    /// −1.5 has floor −2 and fraction 0.5, so building the string from those two parts prints
    /// <c>"-2.5"</c>. And taking the magnitude means <c>Math.Abs</c>, which throws on
    /// <c>long.MinValue</c> — handled explicitly below.
    /// </para>
    /// <para>
    /// ⚠️ The fraction is shown to at most six places and trailing zeros are trimmed. That is a
    /// <em>display</em> rounding and it truncates; it says nothing about the arithmetic, which is
    /// exact to the bit. Nothing should ever parse this back — <see cref="RawBits"/> is the
    /// round-trip form.
    /// </para>
    /// </remarks>
    public override string ToString()
    {
        bool negative = _raw < 0;

        // Math.Abs(long.MinValue) throws, so take the magnitude in unsigned space where it fits.
        ulong magnitude = negative ? (ulong)(-(_raw + 1)) + 1UL : (ulong)_raw;

        ulong whole = magnitude >> FractionBits;
        ulong fraction = magnitude & 0xFFFF_FFFFUL;

        string sign = negative ? "-" : string.Empty;
        string text = whole.ToString(CultureInfo.InvariantCulture);

        if (fraction == 0UL)
        {
            return sign + text;
        }

        // Six places, floored — fits a long comfortably (2^32 * 10^6 is about 4.3e15).
        ulong places = fraction * 1_000_000UL >> FractionBits;
        string digits = places.ToString(CultureInfo.InvariantCulture).PadLeft(6, '0').TrimEnd('0');

        return digits.Length == 0 ? sign + text : $"{sign}{text}.{digits}";
    }

    /// <summary>
    /// Integer division that floors, rather than truncating toward zero as C# does.
    /// </summary>
    /// <remarks>
    /// C#'s <c>/</c> rounds toward zero, so <c>-7 / 2</c> is −3 where floor wants −4. The
    /// correction is applied only when the division was inexact <em>and</em> the operands disagree
    /// in sign, which is precisely the case the two conventions differ on.
    /// </remarks>
    private static Int128 FloorDivide(Int128 numerator, Int128 denominator)
    {
        Int128 quotient = numerator / denominator;
        Int128 remainder = numerator - (quotient * denominator);

        if (remainder != 0 && ((remainder < 0) != (denominator < 0)))
        {
            quotient -= 1;
        }

        return quotient;
    }

    private static long Checked(Fixed left, Fixed right, string op, Func<long, long, Int128> apply) =>
        Narrow(apply(left._raw, right._raw), op, left, right);

    private static long Narrow(Int128 value, string op, object left, object right)
    {
        if (value < long.MinValue || value > long.MaxValue)
        {
            throw new OverflowException(
                $"Fixed arithmetic overflowed: {left} {op} {right}.");
        }

        return (long)value;
    }
}
