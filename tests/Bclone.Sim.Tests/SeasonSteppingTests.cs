using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The harness that walks the calendar — <see cref="FarmFixtures.StepToTheStartOf"/> (D424).
/// </summary>
/// <remarks>
/// <b>A test of a test helper, because this one lied for months.</b> Asked for a season the
/// world was already in, it returned two ticks later, so a loop meaning "one more year" read one
/// winter over and over. D422 found a guard whose "twelve years" were ticks 361–383, and the
/// claim it had been passing on was false. Nothing in the suite could have reported it.
/// </remarks>
public sealed class SeasonSteppingTests
{
    private readonly ITestOutputHelper _output;

    public SeasonSteppingTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimLoop Build() => SimFactory.CreatePhase0(Config, new InMemoryLogSink());

    /// <summary>⛔ Asked for the season it is already in, it refuses rather than standing still.</summary>
    [Fact]
    public void ItRefusesToReadTheSameSeasonTwice()
    {
        SimLoop loop = Build();
        FarmFixtures.StepToTheStartOf(loop, Season.Winter);
        ulong first = loop.World.Tick;

        var refused = Assert.Throws<System.InvalidOperationException>(
            () => FarmFixtures.StepToTheStartOf(loop, Season.Winter));
        _output.WriteLine($"t{first}: {refused.Message}");

        // The way through: another season first, and the next winter is a year on.
        FarmFixtures.StepToTheStartOf(loop, Season.Spring);
        FarmFixtures.StepToTheStartOf(loop, Season.Winter);
        _output.WriteLine($"winter at t{first}, the next at t{loop.World.Tick}");
        Assert.Equal(first + (ulong)Config.TicksPerYear, loop.World.Tick);
    }

    /// <summary>
    /// …but a season's first tick is its start — nothing has run on it yet — so asking from there
    /// stays in that season, as every guard that opens with <c>StepToTheStartOf(Spring)</c> needs.
    /// </summary>
    /// <remarks>
    /// The anti-vacuity half (D7): a refusal that fired whenever the clock read the season asked
    /// for would pass the guard above and throw at tick 0 in five farm guards.
    /// </remarks>
    [Fact]
    public void ASeasonsFirstTickIsItsStart()
    {
        SimLoop loop = Build();
        FarmFixtures.StepToTheStartOf(loop, Season.Spring);
        _output.WriteLine($"from t0: spring at t{loop.World.Tick}");
        Assert.Equal(Season.Spring, loop.World.Clock.Season);
        Assert.Equal(1, loop.World.Clock.Year);

        int ticksPerSeason = Config.TicksPerYear / 4;
        loop.Step(ticksPerSeason * 2 - (int)loop.World.Tick);
        Assert.Equal(Season.Fall, loop.World.Clock.Season);
        Assert.Equal(1, loop.World.Clock.DayOfSeason);

        FarmFixtures.StepToTheStartOf(loop, Season.Fall);
        _output.WriteLine($"from fall's first tick t{ticksPerSeason * 2}: fall at t{loop.World.Tick}");
        Assert.Equal(Season.Fall, loop.World.Clock.Season);
        Assert.Equal(1, loop.World.Clock.Year);
    }
}
