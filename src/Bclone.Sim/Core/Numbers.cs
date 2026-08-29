using System.Globalization;

namespace Bclone.Sim.Core;

/// <summary>
/// How a number is written where a person reads it — <b>grouped, and the same everywhere</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐ Joe, 2026-08-29:</b> <em>"use commas for numbers when appropriate. 2834 food becomes
/// 2,834."</em> A four-digit run of bare digits in a sentence is something the reader has to
/// count; a grouped one is something they can see.
/// </para>
/// <para>
/// <b>⛔⛔ INVARIANT CULTURE, DELIBERATELY, AND THIS IS THE WHOLE REASON THE HELPER EXISTS RATHER
/// THAN A BARE <c>:N0</c>.</b> `N0` reads the ambient culture, so the same seed on a German machine
/// would write <em>"2.834 food stored"</em> into the audit trail. **This project's logs are
/// evidence** — nearly every bug that mattered came out of grepping them (D236), and a run whose
/// log bytes depend on the operator's locale is a run two people cannot compare.
/// <em>`BannedSymbols.txt` bans the wall clock and the unseeded RNG for the same reason; the
/// machine's locale is the third door.</em>
/// </para>
/// <para>
/// <b>⚠️ "WHEN APPROPRIATE" IS THE HALF THAT NEEDS JUDGEMENT, AND IT IS A RULE RATHER THAN A
/// TASTE:</b> group <b>quantities</b> — goods, capacities, stores, food — and never group
/// <b>counts of people, years, ages or ticks</b>. *"2,834 food"* helps; **"1,024 villagers" would
/// be a village nobody has, and "Year 1,203" is not a year anybody plays.** A number that cannot
/// reach four digits gains nothing and loses the plainness this game's voice is written in.
/// </para>
/// </remarks>
public static class Numbers
{
    /// <summary>"2,834" — a quantity, grouped, in the same style on every machine.</summary>
    public static string Grouped(this int value) =>
        value.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>"2,834" — the wider form, for lifetime totals that outgrow an int's readability.</summary>
    public static string Grouped(this long value) =>
        value.ToString("N0", CultureInfo.InvariantCulture);
}
