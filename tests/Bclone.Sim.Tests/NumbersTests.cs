using System.Globalization;
using Bclone.Sim.Core;
using Xunit;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ Quantities are grouped, and grouped the same way on every machine (D258).
/// </summary>
public sealed class NumbersTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(704, "704")]
    [InlineData(1000, "1,000")]
    [InlineData(2834, "2,834")]
    [InlineData(1695000, "1,695,000")]
    public void QuantitiesAreGroupedWithCommas(int value, string expected) =>
        Assert.Equal(expected, value.Grouped());

    /// <summary>
    /// ⛔ The machine's locale cannot change what a log says.
    /// </summary>
    /// <remarks>
    /// <b>⭐⭐ THE WHOLE REASON THIS HELPER EXISTS RATHER THAN A BARE <c>:N0</c>.</b> `N0` reads the
    /// ambient culture, so the same seed on a German machine would write <em>"2.834 food
    /// stored"</em> into the audit trail. **This project's logs are evidence** — nearly every bug
    /// that mattered came out of grepping them — and a run whose bytes depend on the operator's
    /// locale is a run two people cannot compare. *`BannedSymbols.txt` shuts the wall clock and the
    /// unseeded RNG; the locale is the third door.*
    /// </remarks>
    [Fact]
    public void TheLocaleCannotChangeWhatALogSays()
    {
        CultureInfo was = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal("2,834", 2834.Grouped());

            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            Assert.Equal("2,834", 2834.Grouped());
        }
        finally
        {
            CultureInfo.CurrentCulture = was;
        }
    }
}
