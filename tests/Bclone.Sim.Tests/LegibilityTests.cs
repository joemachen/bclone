using System;
using Bclone.Sim.Systems;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Non-negotiable §1.1: the game explains itself in words a person reads.
/// </summary>
/// <remarks>
/// <para>
/// Guards for the places where that promise is kept by a lookup table, because a lookup
/// table is exactly the kind of thing that falls behind an enum without anybody noticing.
/// That is not hypothetical: <c>Villager.DescribeState</c> covered ten of seventeen
/// villager states and fell through to <c>State.ToString()</c> for the rest, so the panel
/// whose entire job is explaining a villager printed <c>HaulingToStore</c> and
/// <c>SeekingShelter</c> at the player. All seven of the missing states were added after
/// the method was written.
/// </para>
/// <para>
/// A raw enum name on screen is the shrug this project is defined against (§1.4). It is
/// cheap to assert and it cannot be caught any other way — nothing in
/// <c>src/Bclone.Game</c> is testable (D11), so the sim has to hold the line.
/// </para>
/// </remarks>
public sealed class LegibilityTests
{
    private readonly ITestOutputHelper _output;

    public LegibilityTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void EveryVillagerStateReadsAsEnglishInThePanel()
    {
        foreach (VillagerState state in Enum.GetValues<VillagerState>())
        {
            var villager = new Villager { Id = 1, Name = "Test", LifespanYears = 45, State = state };
            string said = villager.DescribeState("the berry patch");

            _output.WriteLine($"{state} — \"{said}\"");

            Assert.False(string.IsNullOrWhiteSpace(said), $"{state} describes itself as nothing.");
            Assert.NotEqual(state.ToString(), said);
        }
    }

    [Fact]
    public void EveryVillagerStateReadsAsEnglishInTheLog()
    {
        // The second mapping, and it is deliberate rather than an oversight: the log wants
        // "idle → gathering" where the panel wants "gathering berries". Two registers for
        // two readers. What is not allowed is either of them going stale, so both are
        // walked.
        foreach (VillagerState state in Enum.GetValues<VillagerState>())
        {
            string said = BehaviorSystem.Describe(state);

            Assert.False(string.IsNullOrWhiteSpace(said), $"{state} describes itself as nothing.");
            Assert.NotEqual(state.ToString(), said);
        }
    }
}
