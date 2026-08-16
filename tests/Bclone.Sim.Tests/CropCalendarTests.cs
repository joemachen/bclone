using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// The year happening to a field — <c>specs/crops-and-orchards.md §5</c> (D161).
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the half of the slice that makes the seasons mechanically distinct</b>, which is
/// what <c>environment-and-seasons.md §5.1</c> set out to do with three multipliers on foraging
/// and could only have done invisibly. Sowing and reaping are a farmer's work and land with the
/// farm; **the turning of the year is the calendar's**, and it happens whether or not anybody is
/// there to watch — a field left behind by a dead village still rots.
/// </para>
/// <para>
/// Nobody sows yet. These pose the states directly, which is D146's lesson: a test that waits
/// for the village to happen to do something is at the mercy of what it wants that season.
/// </para>
/// </remarks>
public sealed class CropCalendarTests
{
    private readonly ITestOutputHelper _output;

    public CropCalendarTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimLoop Build() => SimFactory.CreatePhase0(Config, new InMemoryLogSink());

    /// <summary>Which season a thing may be done in, asked of the season (D159's pattern).</summary>
    [Fact]
    public void TheCalendarKnowsWhenToSowAndWhenToReap()
    {
        Assert.True(SeasonRules.IsSowing(Season.Spring));
        Assert.False(SeasonRules.IsSowing(Season.Summer));
        Assert.False(SeasonRules.IsSowing(Season.Fall));
        Assert.False(SeasonRules.IsSowing(Season.Winter));

        Assert.True(SeasonRules.IsReaping(Season.Fall));
        Assert.False(SeasonRules.IsReaping(Season.Spring));
        Assert.False(SeasonRules.IsReaping(Season.Summer));
        Assert.False(SeasonRules.IsReaping(Season.Winter));

        // The one that already existed, unchanged — foraging still stops in winter.
        Assert.False(SeasonRules.IsGatherable(Season.Winter));
    }

    /// <summary>⭐ A sown field stands ripe when autumn comes.</summary>
    [Fact]
    public void AutumnRipensWhatWasSown()
    {
        SimLoop loop = Build();
        SimWorld world = loop.World;
        GridPos tile = SowOneTile(world);

        StepToTheStartOf(loop, Season.Fall);

        _output.WriteLine(
            $"{world.Clock.SeasonAndYear()}: the tile is {world.Map.TerrainAt(tile)}");

        Assert.Equal(Terrain.Ripe, world.Map.TerrainAt(tile));

        // The field remembers what it grows, so next spring knows what to sow (§4).
        Assert.Equal(1, world.Map.CropAt(tile));
    }

    /// <summary>It does not ripen early — summer is a field still growing.</summary>
    /// <remarks>
    /// Anti-vacuity for the guard above (D7): a rule that ripened everything on any season turn
    /// would pass that test and be wrong about three-quarters of the year.
    /// </remarks>
    [Fact]
    public void SummerLeavesTheCropStanding()
    {
        SimLoop loop = Build();
        SimWorld world = loop.World;
        GridPos tile = SowOneTile(world);

        StepToTheStartOf(loop, Season.Summer);

        _output.WriteLine(
            $"{world.Clock.SeasonAndYear()}: the tile is {world.Map.TerrainAt(tile)}");
        Assert.Equal(Terrain.Sown, world.Map.TerrainAt(tile));
    }

    /// <summary>⭐⭐ Use it or lose it: a ripe field nobody reaped rots when winter comes.</summary>
    /// <remarks>
    /// Joe's ruling. The point of a calendar is that autumn is not optional — a harvest that
    /// waited would make the year shapeless.
    /// </remarks>
    [Fact]
    public void WinterTakesAHarvestNobodyReaped()
    {
        SimLoop loop = Build();
        SimWorld world = loop.World;
        GridPos tile = SowOneTile(world);

        StepToTheStartOf(loop, Season.Fall);
        Assert.Equal(Terrain.Ripe, world.Map.TerrainAt(tile));

        StepToTheStartOf(loop, Season.Winter);

        _output.WriteLine(
            $"{world.Clock.SeasonAndYear()}: the tile is {world.Map.TerrainAt(tile)}");

        Assert.Equal(Terrain.Field, world.Map.TerrainAt(tile));
        Assert.Equal(1, world.Map.CropAt(tile));
    }

    /// <summary>⭐ And it says so — once for the loss, not once per tile.</summary>
    /// <remarks>
    /// <para>
    /// <b>The half that matters is the warning, and the half that must not be missing is this
    /// one.</b> This project has shipped goods silently leaving the world twice — D96 put 17,451
    /// food into a full granary and out of existence, D144 destroyed firewood once the woodyard
    /// filled — and both were found by Joe playing rather than by the suite. A rotting harvest
    /// is that shape on purpose, so it is the one case that has to say so out loud.
    /// </para>
    /// <para>
    /// <b>One line, not one per tile</b>: the village log is the story (D8/D9), and a receipt
    /// roll is what Phase 1's QA checklist rules out by name.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheLostHarvestIsMournedOnceAndNotPerTile()
    {
        var sink = new InMemoryLogSink();
        SimLoop loop = SimFactory.CreatePhase0(Config, sink);
        SimWorld world = loop.World;

        GridPos site = world.Map.FoundingSite;
        for (int dx = 0; dx < 6; dx++)
        {
            var tile = new GridPos(site.X + dx, site.Y + 3);
            world.Map.SetTerrain(tile, Terrain.Sown);
            world.Map.SetCrop(tile, 1);
        }

        StepToTheStartOf(loop, Season.Fall);
        StepToTheStartOf(loop, Season.Winter);

        int lines = 0;
        foreach (LogEntry entry in sink.Entries)
        {
            if (entry.Message.Contains("rotted", System.StringComparison.OrdinalIgnoreCase)
                || entry.Message.Contains("unreaped", System.StringComparison.OrdinalIgnoreCase))
            {
                lines++;
                _output.WriteLine($"t{entry.Tick} [{entry.Level}] {entry.Message}");
            }
        }

        Assert.Equal(1, lines);
    }

    /// <summary>A village with no fields pays nothing and says nothing.</summary>
    /// <remarks>
    /// The same restraint every layer in this game gets: a feature nobody has switched on costs
    /// nothing at all. Here that means no log line on a season turn in a village that has never
    /// sown, which is what stops the village log filling with weather.
    /// </remarks>
    [Fact]
    public void AVillageWithNoFieldsIsNeverToldAboutThem()
    {
        var sink = new InMemoryLogSink();
        SimLoop loop = SimFactory.CreatePhase0(Config, sink);

        loop.Step(Config.TicksPerYear);

        foreach (LogEntry entry in sink.Entries)
        {
            Assert.DoesNotContain("rotted", entry.Message, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("field", entry.Message, System.StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---------------------------------------------------------------

    /// <summary>Put one sown tile on the map, without waiting for a farmer to exist.</summary>
    private static GridPos SowOneTile(SimWorld world)
    {
        var tile = new GridPos(world.Map.FoundingSite.X + 3, world.Map.FoundingSite.Y + 3);
        world.Map.SetTerrain(tile, Terrain.Sown);
        world.Map.SetCrop(tile, 1);
        return tile;
    }

    /// <summary>Step until the given season has begun <em>and the systems have seen it</em>.</summary>
    /// <remarks>
    /// <b>⚠️ The extra step is not padding, and leaving it out is an off-by-one that reads as a
    /// broken feature.</b> <see cref="SimLoop.StepOnce"/> runs the systems and *then* advances
    /// the tick, so the moment <c>World.Clock</c> first reports the new season, no system has
    /// run on it yet — the calendar has turned and nothing has answered. Stopping there had
    /// three of these guards reporting a field that never ripened, when what had actually
    /// happened is that <see cref="Systems.CropSystem"/> had not been given the tick.
    /// </remarks>
    private static void StepToTheStartOf(SimLoop loop, Season season)
    {
        for (int i = 0; i < loop.World.Config.TicksPerYear; i++)
        {
            loop.StepOnce();
            if (loop.World.Clock.Season == season)
            {
                loop.StepOnce();
                return;
            }
        }

        throw new System.InvalidOperationException($"A whole year passed without reaching {season}.");
    }
}
