using System.Globalization;

namespace Bclone.Sim.Core;

/// <summary>
/// A direction, as a 16-bit binary angle — the sim's rotation type (gridless slice 2a, D318).
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐⭐ THE REPRESENTATION IS JOE'S, AND SO IS THE ARGUMENT FOR IT</b> (2026-09-06): *"a 16-bit
/// unsigned integer fixed-point angle. Range 0 to 65,535 mapping linearly to 0° through 360°.
/// Precision ≈0.0055° per step. It is 100% hashable, uses only 2 bytes, has zero float-precision
/// drift, and looks completely continuous to the human eye on screen."* A full turn is 2¹⁶, so one
/// step is 360/65536 = <b>0.0054931640625°</b> — finer than any screen shows and finer than any
/// hand aims.
/// </para>
/// <para>
/// ⛔ <b>It replaces the sixteen-step compass this project previously recommended</b>
/// (`specs/gridless.md §5`, since corrected). Sixteen poses is not *"placed facing any
/// direction"*; this is — and it is <em>more</em> exactly hashable rather than less.
/// </para>
///
/// <para>
/// <b>⭐⭐ THERE IS NO OVERFLOW POLICY, AND THAT IS THE POINT OF THE TYPE.</b> All 65,536 bit
/// patterns are valid angles, so <c>+</c> and <c>-</c> are <em>total</em>: a <c>ushort</c> rolling
/// over at 65536 <b>is</b> a circle closing at 360°. There is no <c>if (angle > 2pi) angle -= 2pi</c>
/// here and there never will be — **that entire class of bug does not exist in this
/// representation.** ⚠️ It is the sharpest contrast with <see cref="Fixed"/>, where the same
/// boundary throws, and the two are opposite for a reason: a coordinate that overflows is a bug,
/// where an angle that wraps is just pointing the other way.
/// </para>
/// <para>
/// ⚠️ <b>The C# trap this rests on:</b> <c>ushort + ushort</c> promotes to <c>int</c>, so the wrap
/// only happens on the cast back. Every operator below is written <c>unchecked((ushort)(…))</c> —
/// the cast truncates to sixteen bits, which is exactly the wrap wanted, and <c>unchecked</c> keeps
/// it correct if anybody ever turns on <c>CheckForOverflowUnderflow</c>.
/// </para>
///
/// <para>
/// <b>⛔⛔ WHY THE SINE TABLE IS CHECKED IN AND <c>Math.Sin</c> IS NEVER CALLED FROM HERE.</b>
/// IEEE-754 requires correctly-rounded results for <c>+ - * /</c> and square root, and <b>not</b>
/// for the transcendentals — so <c>Math.Sin</c> is not guaranteed bit-identical across platforms or
/// runtime versions, and a sine computed on the player's machine would break *same seed ⇒
/// byte-identical state* in the one way no test on a single machine can detect. **The numbers below
/// are constants, so there is nothing left to vary.** *(Square root would have been safe, for the
/// record — the ban is on the transcendentals specifically; see `specs/tick-loop.md §6`.)*
/// </para>
/// <para>
/// The tests may and do call <c>Math.Sin</c>: a float in a test is fine, and
/// <c>AngleTests.EveryAngleAgreesWithRealSineToWithinTheInterpolationBound</c> is what proves these
/// constants honest rather than merely fixed.
/// </para>
///
/// <para>
/// ⚠️ <b>No ordering operators, deliberately.</b> There is no sensible answer to *"is 359° greater
/// than 1°?"* on a circle, and a <c>&lt;</c> that quietly compared raw values would be right for
/// half the cases and wrong for the interesting half. Equality is exact and is enough.
/// </para>
/// </remarks>
public readonly record struct Angle
{
    /// <summary>A quarter turn — the axis the table's symmetry folds about.</summary>
    private const int QuarterTurn = 16384;

    /// <summary>How many angle steps sit between two table entries.</summary>
    private const int StepsPerEntry = 64;

    private readonly ushort _raw;

    /// <summary>
    /// Private, so <c>new Angle(5)</c> cannot exist.
    /// </summary>
    /// <remarks>
    /// Same reasoning as <see cref="Fixed"/>: a positional record struct would hand out a public
    /// constructor meaning <b>5/65536 of a turn</b> — about 0.027° — while reading exactly like a
    /// count of degrees.
    /// </remarks>
    private Angle(ushort raw) => _raw = raw;

    /// <summary>The underlying binary angle — the canonical save and hash form.</summary>
    public ushort Raw => _raw;

    public static Angle Zero => new(0);

    /// <summary>A quarter turn.</summary>
    public static Angle Right => new(QuarterTurn);

    public static Angle FromRaw(ushort raw) => new(raw);

    /// <summary>
    /// Degrees, wrapped onto the circle.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Exact for quarters, eighths and sixteenths, and nothing else.</b> 65536/360 is 182.04,
    /// so <c>FromDegrees(1)</c> floors to 182 raw and three hundred and sixty of them fall twelve
    /// steps short of a full turn. *Degrees are a human convenience; the raw value is the truth,
    /// and <c>AngleTests.TheCircleCloses</c> asserts that shortfall rather than pretending it away.*
    /// </remarks>
    public static Angle FromDegrees(int degrees)
    {
        // Positive remainder first, so negative degrees wrap instead of producing a negative raw.
        int wrapped = ((degrees % 360) + 360) % 360;
        return new Angle((ushort)(wrapped * 65536 / 360));
    }

    /// <summary>A fraction of a full turn — <c>FromTurnFraction(1, 4)</c> is a right angle.</summary>
    /// <remarks>
    /// ⭐ The exact door, where <see cref="FromDegrees"/> is the convenient one: any denominator
    /// that divides 65536 lands on the nose.
    /// </remarks>
    public static Angle FromTurnFraction(int numerator, int denominator)
    {
        if (denominator == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(denominator),
                $"A turn cannot be divided into zero parts (numerator was {numerator}).");
        }

        long steps = (long)numerator * 65536 / denominator;
        return new Angle(unchecked((ushort)steps));
    }

    public static Angle operator +(Angle left, Angle right) =>
        new(unchecked((ushort)(left._raw + right._raw)));

    public static Angle operator -(Angle left, Angle right) =>
        new(unchecked((ushort)(left._raw - right._raw)));

    public static Angle operator -(Angle value) =>
        new(unchecked((ushort)(-value._raw)));

    /// <summary>Sine of this angle.</summary>
    /// <remarks>
    /// <para>
    /// The quarter-wave table holds sine over [0°, 90°]; the other three quadrants are that wave
    /// reflected and negated. ⛔ <b>The reflection is why 90° and 270° need their own line:</b> the
    /// mirrored index reaches 16384, one past the last interval, and it is also the only place the
    /// answer is exactly ±1. **Removing it throws `IndexOutOfRangeException` on a right angle** —
    /// verified by deleting it and watching four guards go red, not assumed.
    /// </para>
    /// <para>
    /// ⚠️ <b>IT USED TO BE PROTECTED TWICE, AND THE RED CHECK IS WHAT FOUND THAT.</b> The
    /// interpolation carried a <c>step == 0</c> fast path which — at exactly 90° and 270°, where
    /// <c>step</c> is always 0 — returned the table's last entry without ever reading
    /// <c>index + 1</c>. So each branch silently covered for the other: deleting *either* alone
    /// left all twenty-seven guards green, and only deleting **both** produced the fault. *A
    /// safety that is provided twice is a safety no test can hold you to.* The fast path is gone;
    /// this branch is the single load-bearing line and now reddens on its own.
    /// </para>
    /// </remarks>
    public Fixed Sin()
    {
        int quadrant = _raw >> 14;
        int within = _raw & (QuarterTurn - 1);

        // Fold the circle onto the first quadrant: quadrants 1 and 3 read the wave backwards,
        // quadrants 2 and 3 read it upside down.
        int folded = quadrant is 1 or 3 ? QuarterTurn - within : within;
        bool negative = quadrant >= 2;

        if (folded == QuarterTurn)
        {
            return negative ? -Fixed.One : Fixed.One;
        }

        int index = folded / StepsPerEntry;
        int step = folded % StepsPerEntry;

        long low = SineQuarter[index];
        long value = low + ((SineQuarter[index + 1] - low) * step / StepsPerEntry);

        return Fixed.FromRawBits(negative ? -value : value);
    }

    /// <summary>Cosine of this angle.</summary>
    /// <remarks>
    /// ⭐ <b>One table and one function.</b> Cosine is sine a quarter turn along, and the
    /// wraparound that would otherwise need a modulo is free here — 270° + 90° is 0° because a
    /// <c>ushort</c> says so.
    /// </remarks>
    public Fixed Cos() => (this + Right).Sin();

    /// <summary>Degrees, rounded down — for logs and for people.</summary>
    public int ToDegrees() => _raw * 360 / 65536;

    public override string ToString() =>
        ToDegrees().ToString(CultureInfo.InvariantCulture) + "°";

    /// <summary>
    /// Sine over a quarter turn as <see cref="Fixed"/> raw bits — 257 entries, 64 angle steps apart.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>257 rather than 256, because the interpolation reads <c>index + 1</c></b>, and the last
    /// entry is exactly <c>Fixed.One</c>.
    /// </para>
    /// <para>
    /// ⚠️ <b>Rounded to NEAREST, where every arithmetic operation in this codebase floors — and the
    /// inconsistency is deliberate.</b> A table entry is *data*, not the result of an operation.
    /// Its quantisation error is 2⁻³² ≈ 2.3 × 10⁻¹⁰, **four orders of magnitude below** the
    /// 4.7 × 10⁻⁶ interpolation error that dominates it, so rounding cannot measurably change an
    /// answer; nearest is chosen only so the last entry lands on <c>Fixed.One</c> exactly rather
    /// than one unit short of it.
    /// </para>
    /// <para>
    /// ⭐ <b>Measured, not asserted:</b> worst error against real sine across all 65,536 angles is
    /// <b>20,213 raw units</b>, against a theoretical <c>h²/8</c> of 20,212 — the analysis and the
    /// implementation agree to one unit. A three-tile building's corner lands within 0.0000141 of a
    /// tile, far inside one screen pixel at any zoom this game offers.
    /// </para>
    /// </remarks>
    private static readonly long[] SineQuarter =
    {
        0, 26353424, 52705856, 79056303, 105403774, 131747276, 158085819, 184418409,
        210744057, 237061769, 263370557, 289669429, 315957395, 342233465, 368496651, 394745962,
        420980412, 447199012, 473400776, 499584716, 525749847, 551895183, 578019742, 604122538,
        630202589, 656258914, 682290530, 708296459, 734275721, 760227338, 786150333, 812043729,
        837906553, 863737830, 889536587, 915301854, 941032661, 966728038, 992387019, 1018008636,
        1043591926, 1069135926, 1094639673, 1120102207, 1145522571, 1170899806, 1196232957, 1221521071,
        1246763195, 1271958380, 1297105676, 1322204136, 1347252816, 1372250773, 1397197066, 1422090755,
        1446930903, 1471716574, 1496446837, 1521120759, 1545737412, 1570295869, 1594795204, 1619234497,
        1643612827, 1667929275, 1692182927, 1716372869, 1740498191, 1764557983, 1788551342, 1812477362,
        1836335144, 1860123788, 1883842400, 1907490086, 1931065957, 1954569124, 1977998702, 2001353810,
        2024633568, 2047837100, 2070963532, 2094011993, 2116981616, 2139871536, 2162680890, 2185408821,
        2208054473, 2230616993, 2253095531, 2275489241, 2297797281, 2320018810, 2342152991, 2364198992,
        2386155981, 2408023134, 2429799626, 2451484637, 2473077351, 2494576955, 2515982640, 2537293599,
        2558509031, 2579628136, 2600650120, 2621574191, 2642399561, 2663125446, 2683751066, 2704275644,
        2724698408, 2745018589, 2765235421, 2785348143, 2805355999, 2825258235, 2845054101, 2864742853,
        2884323748, 2903796051, 2923159027, 2942411948, 2961554089, 2980584729, 2999503152, 3018308645,
        3037000500, 3055578014, 3074040487, 3092387225, 3110617535, 3128730733, 3146726136, 3164603066,
        3182360851, 3199998822, 3217516315, 3234912670, 3252187232, 3269339351, 3286368382, 3303273682,
        3320054617, 3336710553, 3353240863, 3369644927, 3385922125, 3402071844, 3418093478, 3433986423,
        3449750080, 3465383855, 3480887161, 3496259414, 3511500034, 3526608449, 3541584088, 3556426389,
        3571134792, 3585708745, 3600147697, 3614451106, 3628618433, 3642649144, 3656542712, 3670298613,
        3683916329, 3697395348, 3710735162, 3723935269, 3736995171, 3749914379, 3762692404, 3775328765,
        3787822988, 3800174601, 3812383140, 3824448145, 3836369162, 3848145741, 3859777440, 3871263820,
        3882604450, 3893798902, 3904846754, 3915747591, 3926501002, 3937106583, 3947563934, 3957872662,
        3968032378, 3978042699, 3987903250, 3997613658, 4007173558, 4016582591, 4025840401, 4034946641,
        4043900968, 4052703044, 4061352537, 4069849124, 4078192482, 4086382299, 4094418266, 4102300081,
        4110027446, 4117600071, 4125017671, 4132279966, 4139386683, 4146337555, 4153132319, 4159770720,
        4166252509, 4172577440, 4178745276, 4184755784, 4190608739, 4196303920, 4201841112, 4207220108,
        4212440704, 4217502704, 4222405917, 4227150159, 4231735252, 4236161021, 4240427302, 4244533933,
        4248480760, 4252267634, 4255894413, 4259360959, 4262667143, 4265812840, 4268797931, 4271622305,
        4274285855, 4276788480, 4279130086, 4281310585, 4283329896, 4285187942, 4286884652, 4288419964,
        4289793820, 4291006167, 4292056960, 4292946160, 4293673732, 4294239650, 4294643893, 4294886444,
        4294967296,
    };
}
