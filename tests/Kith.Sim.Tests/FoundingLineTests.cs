using Kith.Sim.Core;
using Kith.Sim.Logging;
using Xunit;

namespace Kith.Sim.Tests;

/// <summary>
/// ⭐ The founding line says what the stores hold (`hud.md §14` step 9, D882; A207): it read "no food stored" while the
/// cart held 1,200 food. "No food stored" only when there is none.
/// </summary>
public sealed class FoundingLineTests
{
    private static string FoundingLine(Config.SimConfig config)
    {
        var sink = new InMemoryLogSink();
        SimFactory.CreatePhase0(config, sink);
        return Phase0Fixtures.LifeLog(sink)[0];
    }

    [Fact]
    public void TheShippedFoundingSaysWhatTheCartHolds()
    {
        Config.SimConfig shipped = ShippedConfig.Load();
        Assert.Equal(1200, shipped.CartFood);
        Assert.Equal(
            "4 exiles arrive in 2 households with a cart: 1,200 food and 20 stone tools. Spring, Year 1.",
            FoundingLine(shipped));
    }

    [Fact]
    public void AnEmptyCartIsNoFoodStored()
    {
        string line = FoundingLine(ShippedConfig.Load() with { CartFood = 0 });
        Assert.EndsWith(", no food stored.", line, StringComparison.Ordinal);
        Assert.DoesNotContain(" food and", line, StringComparison.Ordinal);
    }
}
