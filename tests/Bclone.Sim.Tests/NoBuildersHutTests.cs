using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// What the village says when something is marked and there is no builder's hut (D322).
/// </summary>
/// <remarks>
/// <para>
/// <b>⛔⛔ THIS COST A VILLAGE, AND THE SIM WAS NEVER WRONG.</b> Joe marked six longhouses across
/// two runs. Nothing was ever raised — correctly, because a village with no builder's hut raises
/// nothing (<c>BuildersHutTests.NothingIsRaisedWithoutAHutAndTheVillageSaysSo</c>). The village log
/// said so at tick 0, every time.
/// </para>
/// <para>
/// <b>But the Professions panel told him "there is nothing marked to build"</b>, which was flatly
/// false, so he went looking for a placement bug instead of building a hut, and everybody starved.
/// The cause was one expression answering two questions: <c>BuildersWanted</c> returns
/// <c>anythingToBuild ? seats : 0</c>, which is zero for *nothing marked* and for *nowhere to build
/// from* alike.
/// </para>
/// <para>
/// ⚠️ <b>Pre-existing</b> — <c>LabourQuota</c> was last touched in the food umbrella work, long
/// before the longhouse. The longhouse only made somebody walk into it.
/// </para>
/// </remarks>
public sealed class NoBuildersHutTests
{
    private readonly ITestOutputHelper _output;

    public NoBuildersHutTests(ITestOutputHelper output) => _output = output;

    /// <summary>A cold valley: nothing standing, so no builder's hut either.</summary>
    private static SimWorld ColdValley()
    {
        SimConfig config = VillageFixtures.Village with { FoundingBuildings = false };
        return SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;
    }

    private static GridPos Nearby(SimWorld world) =>
        new(world.Map.FoundingSite.X + 3, world.Map.FoundingSite.Y + 3);

    /// <summary>⛔ The sentence that sent Joe looking for the wrong bug.</summary>
    [Fact]
    public void WithSomethingMarkedAndNoHutTheVillageNamesTheHutAndNotTheMarking()
    {
        SimWorld world = ColdValley();
        Assert.False(world.HasABuildersHut());

        Assert.True(world.Mark(BuildingKind.Granary, Nearby(world)).Allowed);

        string? why = LabourQuota.WhyTheVillageWantsNone(world, JobKind.Builder);
        _output.WriteLine($"the village says: {why}");

        Assert.NotNull(why);

        // ⛔ THE EXACT FALSE SENTENCE. Something IS marked — it is in the build queue.
        Assert.DoesNotContain("nothing marked to build", why, StringComparison.Ordinal);
        Assert.Contains("builder's hut", why, StringComparison.Ordinal);
    }

    /// <summary>⭐ ANTI-VACUITY (D7): with nothing marked, the old sentence is still the right one.</summary>
    /// <remarks>
    /// Without this, "always blame the hut" would pass the guard above and be just as wrong in the
    /// other direction — a village with a hut and nothing to build would be told to build a hut.
    /// </remarks>
    [Fact]
    public void WithNothingMarkedItStillSaysNothingIsMarked()
    {
        SimWorld world = ColdValley();

        string? why = LabourQuota.WhyTheVillageWantsNone(world, JobKind.Builder);
        _output.WriteLine($"nothing marked, no hut: {why}");

        Assert.Equal("there is nothing marked to build", why);
    }

    /// <summary>
    /// ⭐⭐ The remedy line can fire at last — it was structurally dead for builders.
    /// </summary>
    /// <remarks>
    /// The panel prints *"⚠ needs N, build another X"* when <c>Needed(kind) &gt; seats</c>.
    /// <c>Needed(Builder)</c> came from the same seat-capped number, so it was 0, so <c>0 &gt; 0</c>
    /// was false **forever** — for the one trade whose demand is defined as its own seat count.
    /// Every other trade got the sentence; Forager and Fisher both show it in Joe's screenshot.
    /// </remarks>
    [Fact]
    public void TheVillageSaysItNeedsABuilderWhenItHasNowhereToPutOne()
    {
        SimWorld world = ColdValley();
        Assert.True(world.Mark(BuildingKind.Granary, Nearby(world)).Allowed);

        LabourQuota quota = LabourQuota.For(world);
        int seats = LabourQuota.TotalCapacityFor(world, JobKind.Builder);

        _output.WriteLine($"needs {quota.Needed(JobKind.Builder)}, seats {seats}");

        Assert.Equal(0, seats);
        Assert.True(
            quota.Needed(JobKind.Builder) > seats,
            "Needed is not above the seats, so the 'build a builder's hut' line stays silent.");
    }

    /// <summary>⛔ And the line the player reads at the moment of clicking told them the opposite.</summary>
    /// <remarks>
    /// It said *"Marked out. The village will raise it when it can spare the hands."* — cheerful,
    /// and false: it cannot spare them and never will until a hut stands.
    /// </remarks>
    [Fact]
    public void MarkingWithNoHutWarnsRatherThanPromising()
    {
        SimWorld world = ColdValley();

        PlacementVerdict verdict = world.CanBuildAt(BuildingKind.Granary, Nearby(world));
        _output.WriteLine($"allowed {verdict.Allowed}, warning: {verdict.Warning}");

        // ⭐ ALLOWED, not refused: laying a village out before the hut is a reasonable want, and
        // a refusal is information rather than a dismissal.
        Assert.True(verdict.Allowed);
        Assert.True(verdict.HasWarning);
        Assert.Contains("builder's hut", verdict.Warning, StringComparison.Ordinal);
    }

    /// <summary>⭐ Silent once a hut stands, or the warning is noise that teaches players to ignore it.</summary>
    [Fact]
    public void OnceAHutStandsNothingWarns()
    {
        SimWorld world = ColdValley();
        Assert.True(world.Mark(BuildingKind.BuilderHut, Nearby(world)).Allowed);
        Assert.True(world.HasABuildersHut());

        PlacementVerdict verdict = world.CanBuildAt(
            BuildingKind.Granary, new GridPos(world.Map.FoundingSite.X + 5, world.Map.FoundingSite.Y + 5));

        _output.WriteLine($"with a hut: allowed {verdict.Allowed}, warning '{verdict.Warning}'");
        Assert.DoesNotContain("builder's hut", verdict.Warning ?? string.Empty, StringComparison.Ordinal);
    }
}
