using Kith.Sim.World;

namespace Kith.Sim.Core;

/// <summary>
/// ⭐ What the heights mean to the people reading the valley (`specs/heights.md §6`, D735): a tile's slope in words, and
/// whether somebody is climbing. The walking itself is the cost field's (`TravelCostField.ReadSlope`) — nothing here
/// changes a route or a tick.
/// </summary>
public sealed partial class SimWorld
{
    /// <summary>A tile's slope class, by today's thresholds (<c>slope_gentle_at</c>, <c>slope_steep_at</c>).</summary>
    public Slope SlopeAt(GridPos tile) =>
        Slopes.ClassOf(Map.SteepnessAt(tile), Config.SlopeGentleAt, Config.SlopeSteepAt);

    /// <summary>
    /// Whether this tile refuses a building, a house, a field or a pasture for being steep (`heights.md §13`, D814) —
    /// false everywhere while <c>steep_refuses_building</c> is off.
    /// </summary>
    public bool TooSteepToBuild(GridPos tile) => Config.SteepRefusesBuilding && SlopeAt(tile) == Slope.Steep;

    /// <summary>The one sentence every steep refusal says (Joe's proposal: <i>"too steep to build on"</i>).</summary>
    public const string TooSteepSentence = "Too steep to build on.";

    /// <summary>
    /// <i>What's here</i>'s line for a tile's slope — or null on the flat, where it would be noise on most of the valley
    /// (D358's rule for the trail lines). Water is level and says nothing.
    /// </summary>
    public string? SayTheSlope(GridPos tile)
    {
        if (Map.TerrainAt(tile) == Terrain.Water)
        {
            return null;
        }

        return SlopeAt(tile) switch
        {
            Slope.Steep => "Steep ground — slow to cross, up or down.",
            Slope.Gentle => "A gentle slope — a little slower to cross.",
            _ => null,
        };
    }

    /// <summary>
    /// Whether a villager is on the move across steep ground this tick — the card's <i>climbing</i> for its
    /// <i>walking</i> (`heights.md §6`). Up or down: any slope is slow (Joe's call 2), and <i>climbing</i> is the word.
    /// </summary>
    public bool IsClimbing(Villager villager)
    {
        ArgumentNullException.ThrowIfNull(villager);
        return villager.Alive && villager.LegTicks > Fixed.Zero && SlopeAt(villager.Tile) == Slope.Steep;
    }
}
