using Bclone.Sim.World;

namespace Bclone.Sim.Core;

/// <summary>
/// ⭐ THE RIVER BANK AS A HOUSEHOLD'S WATER (D779, `water-need.md §5` step 3; Joe's call 2A, D776: <i>"the nearer of a well or
/// the river bank"</i>). Every valley has its river (D547), so a village with no well walks to the bank, and a well's payoff is
/// the walking it saves.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>A bank tile</b> is a land tile with the river on one of its four sides and nothing standing on it — a villager stands
/// there to draw. <b>The bank a home uses is the one cheapest to walk home from, by the one cost field</b> (`DESIGN.md §2.6`):
/// every candidate is priced on the field <em>to the home</em>, which every walk home has already built, so no field is built per
/// bank tile and no second distance comes into being.
/// </para>
/// <para>
/// ⚠️ The list of bank tiles is derived from the terrain and rebuilt when it changes (<see cref="TerrainGeneration"/>, the
/// monotonic counter `CLAUDE.md` names); <b>never hashed, never saved</b> (D335) — the river is the map's, and the map is.
/// </para>
/// </remarks>
public sealed partial class SimWorld
{
    private List<GridPos>? _banks;
    private int _banksBuiltAt = -1;

    /// <summary>Whether a villager may stand on <paramref name="tile"/> to draw from the river: land, beside water, nothing on it.</summary>
    public bool IsBank(GridPos tile) =>
        Map.Contains(tile) && Map.TerrainAt(tile) != Terrain.Water && BesideTheRiver(tile) && !StandsOn(tile);

    /// <summary>
    /// The bank tile cheapest to walk home to <paramref name="home"/> from, and how many tiles' walk it is — or null when no bank
    /// can be walked to (D779). Ties go to the first in row order.
    /// </summary>
    public GridPos? NearestBank(GridPos home, out int tiles)
    {
        GridPos? best = null;
        int cheapest = TravelCostField.Unreachable;
        List<GridPos> banks = Banks();
        for (int i = 0; i < banks.Count; i++)
        {
            GridPos bank = banks[i];
            if (StandsOn(bank))
            {
                continue;
            }

            int cost = TravelCost.Cost(bank, home);
            if (cost < cheapest)
            {
                cheapest = cost;
                best = bank;
            }
        }

        tiles = best is null ? int.MaxValue : cheapest / TravelCostField.BaseTileCost;
        return best;
    }

    /// <summary>Every land tile beside the river, in row order, rebuilt when the terrain changes.</summary>
    private List<GridPos> Banks()
    {
        if (_banks is not null && _banksBuiltAt == TerrainGeneration)
        {
            return _banks;
        }

        var banks = new List<GridPos>();
        for (int y = Map.MinY; y < Map.MinY + Map.Height; y++)
        {
            for (int x = Map.MinX; x < Map.MinX + Map.Width; x++)
            {
                var tile = new GridPos(x, y);
                if (Map.TerrainAt(tile) != Terrain.Water && BesideTheRiver(tile))
                {
                    banks.Add(tile);
                }
            }
        }

        _banks = banks;
        _banksBuiltAt = TerrainGeneration;
        return banks;
    }

    private bool BesideTheRiver(GridPos tile) =>
        IsWater(new GridPos(tile.X + 1, tile.Y)) || IsWater(new GridPos(tile.X - 1, tile.Y))
        || IsWater(new GridPos(tile.X, tile.Y + 1)) || IsWater(new GridPos(tile.X, tile.Y - 1));

    private bool IsWater(GridPos tile) => Map.Contains(tile) && Map.TerrainAt(tile) == Terrain.Water;
}
