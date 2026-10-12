using Bclone.Sim.World;

namespace Bclone.Sim.Core;

/// <summary>
/// Which seam each tile belongs to — the ground a quarry or a mine may work (D561, `seam-reach.md §6`). A seam is the rock
/// (or the iron) joined to itself through any of a tile's eight neighbours, so an outcrop drawn as one blob is one seam.
/// </summary>
/// <remarks>
/// ⭐ <b>AN INDEX KEPT ON THE GROUND'S OWN COUNTER</b> (CLAUDE.md: nothing derivable rebuilt per ask). The brush asks a
/// verdict per quarter-tile of every stroke; a flood fill per ask would be the D179 shape. Rebuilt only when
/// <see cref="TerrainGeneration"/> has moved since it was built — a seam cleared or destroyed, a crop turning — and only
/// when somebody asks. Never hashed (D335: it restates the map) and never saved.
/// </remarks>
public sealed partial class SimWorld
{
    /// <summary>The seam each tile belongs to by map-order index, 0 for no seam. Derived, never hashed.</summary>
    private int[]? _seamPatch;

    private int _seamPatchBuiltAt = -1;

    /// <summary>The seam this tile belongs to, or 0 — the same number for every tile of one seam, rock and iron never shared.</summary>
    internal int SeamPatchOf(GridPos tile)
    {
        RebuildTheSeamIndexIfStale();
        int index = IndexOfTile(tile);
        return index >= 0 ? _seamPatch![index] : 0;
    }

    private void RebuildTheSeamIndexIfStale()
    {
        if (_seamPatchBuiltAt == _terrainGeneration && _seamPatch is not null)
        {
            return;
        }

        _seamPatchBuiltAt = _terrainGeneration;
        int tiles = Map.Width * Map.Height;
        _seamPatch ??= new int[tiles];
        System.Array.Clear(_seamPatch);

        var frontier = new Stack<int>();
        int next = 0;
        for (int start = 0; start < tiles; start++)
        {
            Terrain kind = Map.Tiles[start];
            if (_seamPatch[start] != 0 || !TerrainRules.IsSeam(kind))
            {
                continue;
            }

            next++;
            _seamPatch[start] = next;
            frontier.Push(start);
            while (frontier.Count > 0)
            {
                int at = frontier.Pop();
                int x = at % Map.Width;
                int y = at / Map.Width;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx;
                        int ny = y + dy;
                        if ((dx == 0 && dy == 0) || nx < 0 || nx >= Map.Width || ny < 0 || ny >= Map.Height)
                        {
                            continue;
                        }

                        int neighbour = (ny * Map.Width) + nx;
                        if (_seamPatch[neighbour] == 0 && Map.Tiles[neighbour] == kind)
                        {
                            _seamPatch[neighbour] = next;
                            frontier.Push(neighbour);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Whether this tile is part of a seam the workplace stands by — one with a tile of its face within the placement
    /// reach of the building (`MustBeNear`, the diamond `CanBuildAt` asks). At most thirteen lookups.
    /// </summary>
    private bool IsOnASeamItStandsBy(Workplace workplace, NearRule near, GridPos tile)
    {
        int patch = SeamPatchOf(tile);
        if (patch == 0)
        {
            return false;
        }

        GridPos at = workplace.Position.ToTile();
        for (int dy = -near.Tiles; dy <= near.Tiles; dy++)
        {
            int span = near.Tiles - Math.Abs(dy);
            for (int dx = -span; dx <= span; dx++)
            {
                var nearby = new GridPos(at.X + dx, at.Y + dy);
                if (Map.Contains(nearby) && Map.TerrainAt(nearby) == near.Terrain && SeamPatchOf(nearby) == patch)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
