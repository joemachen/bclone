using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The valley has a name, and it is <b>derived from the seed rather than drawn from it</b>.
/// </summary>
/// <remarks>
/// <para>
/// A run you can quote by number is reproducible; a run you can name is one you can talk
/// about, which is §1.4's people-not-spreadsheets applied to the place itself.
/// </para>
/// <para>
/// <b>⚠️ The whole risk in a village name is one line of code nobody would look at twice:</b>
/// <c>names[rng.Next(names.Count)]</c>. Draw order is the seed contract — inserting a draw
/// shifts every subsequent value for every seed anybody has written down, which would move
/// the map golden, both 50-year hashes and every measurement in <c>DESIGN.md</c> that quotes
/// a seed. These guards exist so the arithmetic derivation cannot quietly become a draw.
/// </para>
/// </remarks>
public sealed class VillageNameTests
{
    private readonly ITestOutputHelper _output;

    public VillageNameTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimWorld World(ulong seed) =>
        SimFactory.CreatePhase0(Config, new InMemoryLogSink(), seed).World;

    [Fact]
    public void OneSeedAlwaysSettlesTheSameValley()
    {
        Assert.Equal(World(7UL).Name, World(7UL).Name);
    }

    /// <summary>Anti-vacuity (D7): a constant would pass the test above perfectly.</summary>
    [Fact]
    public void DifferentSeedsGetDifferentNames()
    {
        var seen = new HashSet<string>();
        for (ulong seed = 1; seed <= 24; seed++)
        {
            seen.Add(World(seed).Name);
        }

        _output.WriteLine($"{seen.Count} distinct names across 24 seeds.");
        Assert.True(seen.Count > 1, "Every seed named its valley the same thing.");
    }

    /// <summary>
    /// ⭐ The name costs the random stream nothing.
    /// </summary>
    /// <remarks>
    /// The state hash covers the map and everything derived from it, so a draw spent on a
    /// name would show up here as two identical seeds hashing differently once one of them
    /// had been asked what it was called. Reading the name a dozen times must change nothing.
    /// </remarks>
    [Fact]
    public void AskingTheValleyItsNameConsumesNoDraw()
    {
        SimWorld quiet = World(11UL);
        SimWorld asked = World(11UL);

        ulong before = StateHash.Compute(asked);
        for (int i = 0; i < 12; i++)
        {
            _ = asked.Name;
        }

        Assert.Equal(before, StateHash.Compute(asked));
        Assert.Equal(StateHash.Compute(quiet), StateHash.Compute(asked));
    }

    /// <summary>
    /// The name is content, so a modder's list is the one that is used.
    /// </summary>
    [Fact]
    public void TheNamePoolIsConfigurable()
    {
        SimConfig one = Config with { TownNames = new[] { "Ashvale" } };
        Assert.Equal(
            "Ashvale",
            SimFactory.CreatePhase0(one, new InMemoryLogSink(), 3UL).World.Name);
    }

    /// <summary>
    /// An empty pool fails at start-up, where the mistake is — not at the first frame that
    /// tries to draw a header (METHODOLOGY §4).
    /// </summary>
    [Fact]
    public void AnEmptyNamePoolIsRefusedLoudly()
    {
        SimConfigException thrown = Assert.Throws<SimConfigException>(
            () => (Config with { TownNames = new string[0] }).Validate());

        Assert.Contains("town_names", thrown.Message);
    }
}
