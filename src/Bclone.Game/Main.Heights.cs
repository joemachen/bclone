using Bclone.Sim.Core;
using Bclone.Sim.World;

namespace Bclone.Game;

/// <summary>The shell's half of the heights (`specs/heights.md §6`, D735): the probe line that reads the real panels.</summary>
public partial class Main
{
    /// <summary>
    /// <c>relief:</c> (guard 11) — the map's wash (<see cref="VillageMap.TheHillsAreShaded"/>), then the words through the
    /// real panels: <i>What's here</i> on the steepest tile names it steep, and a villager's card, posed walking home
    /// across it, says <i>climbing home</i>.
    /// </summary>
    /// <remarks>⛔ The villager is put back as found — where they stood, their state, their leg (D730's trap).</remarks>
    private string TheHillsAreShadedAndSaid()
    {
        string shaded = _map.TheHillsAreShaded();
        if (shaded.Contains('⛔', StringComparison.Ordinal))
        {
            return shaded;
        }

        SimWorld world = _loop.World;
        var faults = new List<string>();
        GridPos steepest = world.Map.FoundingSite;
        for (int i = 0; i < world.Map.Heights.Count; i++)
        {
            var tile = new GridPos((i % world.Map.Width) + world.Map.MinX, (i / world.Map.Width) + world.Map.MinY);
            if (world.Map.TerrainAt(tile) != Terrain.Water && world.Map.SteepnessAtIndex(i) > world.Map.SteepnessAt(steepest))
            {
                steepest = tile;
            }
        }

        string here = DescribeWhatIsAt(world, steepest);
        if (!here.Contains("Steep ground", StringComparison.Ordinal))
        {
            faults.Add($"What's here on the steepest tile ({steepest}) does not say it is steep");
        }

        if (DescribeWhatIsAt(world, world.Map.FoundingSite).Contains("slope", StringComparison.Ordinal))
        {
            faults.Add("What's here on the flat founding site names a slope");
        }

        Villager villager = world.Villagers.First(v => v.Alive);
        (Point at, VillagerState state, Fixed leg) = (villager.Position, villager.State, villager.LegTicks);
        (GridPos? wasTile, int wasVillager) = (_selectedTile, _selectedVillagerId);
        string said = "nothing";
        try
        {
            villager.StandAt(Point.CentreOf(steepest));
            villager.State = VillagerState.TravelingHome;
            villager.LegTicks = Fixed.FromInt(1);
            OpenCard(new CardSubject(CardKind.Villager, villager.Id));
            said = _selectedCard!.Status.Text;
            CloseCard(_selectedCard);
            if (!said.Contains("limbing home", StringComparison.Ordinal))
            {
                faults.Add($"a villager walking home across the steepest tile reads \"{said}\"");
            }
        }
        finally
        {
            villager.StandAt(at);
            villager.State = state;
            villager.LegTicks = leg;
            (_selectedTile, _selectedVillagerId) = (wasTile, wasVillager);
            Refresh();
        }

        return faults.Count == 0
            ? shaded + $"; What's here names the steepest tile steep and the founding flat; a card crossing it reads \"{said}\""
            : $"[widths] relief: ⛔ {string.Join("; ", faults)}";
    }
}
