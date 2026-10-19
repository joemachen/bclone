using Bclone.Sim.Core;

namespace Bclone.Sim.Systems;

/// <summary>
/// The neighbours wake with a trading post and step a season at a time (D594, `specs/trade.md §4.2`), and the boat
/// appears, docks and leaves on its three ticks (D595, §4.4). All of it is <see cref="SimWorld.TendTheRegion"/> and
/// <see cref="SimWorld.SailTheBoat"/>; this is their place in the order.
/// </summary>
public sealed class RegionSystem : ISimSystem
{
    public string Name => "region";

    public void Execute(SimWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        world.TendTheRegion();
        world.SailTheBoat();
    }
}
