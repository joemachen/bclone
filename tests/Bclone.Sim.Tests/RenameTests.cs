using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The player names their buildings — <c>specs/the-cards.md §3</c> (D376, Joe: *"the ability to
/// rename the building (from woodcutter's hut 1 to whatever the user types in)"*).
/// </summary>
public sealed class RenameTests
{
    private readonly ITestOutputHelper _output;

    public RenameTests(ITestOutputHelper output) => _output = output;

    private static SimWorld Build() =>
        SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink()).World;

    /// <summary>A given name is what the village calls it, a blank hands the born name back, and the log says so.</summary>
    [Fact]
    public void ABuildingTakesTheNameItIsGivenAndABlankHandsTheOldOneBack()
    {
        var sink = new InMemoryLogSink();
        SimWorld world = SimFactory.CreatePhase0(VillageFixtures.Village, sink).World;
        StoreBuilding granary = world.AnyStoreOf(StoreKind.Granary);
        string born = granary.Name;

        Assert.True(world.Rename(granary, "  The Long Barn  ").Allowed);
        Assert.Equal("The Long Barn", granary.Name);
        Assert.Equal("The Long Barn", granary.GivenName);
        Assert.Equal(born, granary.BornAs);
        Assert.Contains(sink.Entries, e => e.Message.Contains($"{born} is called The Long Barn now"));

        Assert.True(world.Rename(granary, "   ").Allowed);
        Assert.Equal(born, granary.Name);
        Assert.Null(granary.GivenName);

        Workplace hut = world.Workplaces.First(w => !w.IsSite);
        Assert.True(world.Rename(hut, "Wendell's yard").Allowed);
        Assert.Equal("Wendell's yard", hut.Name);

        Household home = world.Households[0];
        Assert.True(world.Rename(home, "the Ashfords").Allowed);
        Assert.Equal("the Ashfords", home.Name);

        _output.WriteLine($"granary born {born}; hut now {hut.Name}; home now {home.Name}");
    }

    /// <summary>A name past the limit is refused with the reason, and nothing changes.</summary>
    [Fact]
    public void ANameTooLongIsRefused()
    {
        SimWorld world = Build();
        StoreBuilding granary = world.AnyStoreOf(StoreKind.Granary);
        string born = granary.Name;

        PlacementVerdict verdict = world.Rename(granary, new string('x', SimWorld.NameLengthLimit + 1));
        _output.WriteLine(verdict.Reason);
        Assert.False(verdict.Allowed);
        Assert.Contains($"{SimWorld.NameLengthLimit}", verdict.Reason);
        Assert.Equal(born, granary.Name);

        Assert.True(world.Rename(granary, new string('x', SimWorld.NameLengthLimit)).Allowed);
    }

    /// <summary>
    /// ⛔ A given name is state the hash sees (D7's anti-vacuity), and the born name is not — a
    /// village where nobody renamed anything hashes as it did before renaming existed.
    /// </summary>
    [Fact]
    public void TheHashSeesAGivenNameAndNotABornOne()
    {
        SimWorld world = Build();
        ulong before = StateHash.Compute(world);

        StoreBuilding granary = world.AnyStoreOf(StoreKind.Granary);
        Assert.True(world.Rename(granary, "The Long Barn").Allowed);
        ulong renamed = StateHash.Compute(world);
        Assert.NotEqual(before, renamed);

        Assert.True(world.Rename(granary, "The Long Barm").Allowed);
        Assert.NotEqual(renamed, StateHash.Compute(world));

        Assert.True(world.Rename(granary, null).Allowed);
        Assert.Equal(before, StateHash.Compute(world));

        Workplace hut = world.Workplaces.First(w => !w.IsSite);
        Assert.True(world.Rename(hut, "Wendell's yard").Allowed);
        Assert.NotEqual(before, StateHash.Compute(world));
        Assert.True(world.Rename(hut, string.Empty).Allowed);

        Household home = world.Households[0];
        Assert.True(world.Rename(home, "the Ashfords").Allowed);
        Assert.NotEqual(before, StateHash.Compute(world));
        Assert.True(world.Rename(home, string.Empty).Allowed);
        Assert.Equal(before, StateHash.Compute(world));
    }
}
