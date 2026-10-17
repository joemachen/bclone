using Bclone.Sim.Core;

namespace Bclone.Sim.Systems;

/// <summary>
/// The herds graze, eat their hay, grow and starve (D592, `specs/livestock.md §0`) — a day at a time, on the day's
/// first tick. All of it is <see cref="SimWorld.TendTheHerds"/>; this is its place in the order (after the hearth,
/// before anybody acts on the day).
/// </summary>
public sealed class HerdSystem : ISimSystem
{
    public string Name => "herd";

    public void Execute(SimWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        world.TendTheHerds();
    }
}
