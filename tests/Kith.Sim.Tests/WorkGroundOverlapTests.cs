using Kith.Sim.Core;
using Kith.Sim.Logging;
using Kith.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

/// <summary>
/// Joe, playing Millbrook (2026-11-22): <i>"i painted the farm field on the existing quarry and it overlapped. this should not be
/// allowed to happen."</i> Read off his save first (D857, `tools/harness/ZzFieldOverlap.cs`): <b>the sim kept them apart</b> —
/// the field holds 12 of the 16 quarters of the grass tile in the quarry's top-left notch, the quarry the stone tiles right of and
/// below it, no tile shared. What overlapped was the DRAWING: the outline's rounding cut the quarry's notch outward into the
/// field's tile (fixed in the view, `ZoneOutline.Trace`'s keep-clear rule, probe <c>outline keeps clear:</c>). This pins the sim
/// half so the conclusion cannot quietly stop being true: a field can never take a quarry's ground, by owner or by terrain.
/// </summary>
public sealed class WorkGroundOverlapTests
{
    private readonly ITestOutputHelper _output;

    public WorkGroundOverlapTests(ITestOutputHelper output) => _output = output;

    /// <summary>⭐ Every tile a raised quarry holds is refused to a farm's brush, and painting it changes no owner.</summary>
    [Fact]
    public void AFieldCannotTakeAQuarrysGround()
    {
        SimWorld world = SimFactory.CreatePhase0(ShippedConfig.Established(), new InMemoryLogSink()).World;
        Workplace quarry = QuarryTests.RaiseAQuarry(world, QuarryTests.TheNearestReachableRock(world));
        Workplace farm = FarmFixtures.RaiseAFarm(world);

        var held = new List<GridPos>();
        for (int y = -world.Map.Height; y <= world.Map.Height; y++)
        {
            for (int x = -world.Map.Width; x <= world.Map.Width; x++)
            {
                var tile = new GridPos(x, y);
                if (world.Map.Contains(tile) && world.Zones.WorkGroundOwner(tile) == quarry.Id)
                {
                    held.Add(tile);
                }
            }
        }

        Assert.NotEmpty(held);
        foreach (GridPos tile in held)
        {
            PlacementVerdict verdict = world.PaintWorkGround(farm, tile);
            Assert.False(verdict.Allowed, $"a farm painted ({tile.X},{tile.Y}), which {quarry.Name} holds");
            Assert.Equal(quarry.Id, world.Zones.WorkGroundOwner(tile));
        }

        _output.WriteLine($"{held.Count} of {quarry.Name}'s tiles, each refused: \"{world.CanPaintWorkGround(farm, held[0]).Reason}\"");
    }
}
