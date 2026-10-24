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

    /// <summary>Each seam's tiles by map-order index, in map order — <c>[patch]</c>; index 0 unused. Derived with
    /// <see cref="_seamPatch"/>, never hashed (D613: the claim and the ghost read a whole seam, and a scan of the map per
    /// ask would be the D179 shape).</summary>
    private List<int>[] _seamTiles = System.Array.Empty<List<int>>();

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

        // Each seam's tiles, listed once while the index is fresh — walked in map order, so each list is in map order.
        _seamTiles = new List<int>[next + 1];
        for (int patch = 1; patch <= next; patch++)
        {
            _seamTiles[patch] = new List<int>();
        }

        for (int i = 0; i < tiles; i++)
        {
            if (_seamPatch[i] != 0)
            {
                _seamTiles[_seamPatch[i]].Add(i);
            }
        }
    }

    /// <summary>The tiles of one seam, by map-order index, in map order (D613). Empty for 0 or a patch that is not one.</summary>
    internal IReadOnlyList<int> SeamTilesOf(int patch)
    {
        RebuildTheSeamIndexIfStale();
        return patch > 0 && patch < _seamTiles.Length ? _seamTiles[patch] : System.Array.Empty<int>();
    }

    /// <summary>
    /// ⭐ THE SEAM A QUARRY OR A MINE WOULD CLAIM STANDING HERE (D613, `seam-reach.md §8`) — pure, so the ghost asks it as it
    /// moves and completion claims exactly what the ghost showed.
    /// </summary>
    /// <remarks>
    /// Of the face tiles in the building's placement diamond, the nearest to its tile by that same distance — ties to map
    /// order, never a draw — that the village can walk to and whose seam no other building holds; then every tile of that
    /// seam it may take (<see cref="MayClaim"/>). Empty for a building with no face, or none to claim.
    /// </remarks>
    /// <param name="kind">The building.</param>
    /// <param name="position">Its tile.</param>
    /// <param name="claimant">The workplace that would hold it, or 0 for a building not yet raised: its own ground is no obstacle.</param>
    public IReadOnlyList<GridPos> SeamToClaim(BuildingKind kind, GridPos position, int claimant = 0)
    {
        if (BuildingsCatalog[kind]?.MustBeNear is not NearRule near
            || BuildingsCatalog.EmployedBy(kind) is not JobKind trade
            || JobsCatalog.FaceOf(trade) is not Terrain face
            || face != near.Terrain)
        {
            return System.Array.Empty<GridPos>();
        }

        GridPos village = FirstHomeOrFoundingSite();
        for (int distance = 0; distance <= near.Tiles; distance++)
        {
            for (int dy = -distance; dy <= distance; dy++)
            {
                int span = distance - Math.Abs(dy);
                foreach (int dx in span == 0 ? new[] { 0 } : new[] { -span, span })
                {
                    var at = new GridPos(position.X + dx, position.Y + dy);
                    if (!Map.Contains(at) || Map.TerrainAt(at) != face || !TravelCost.CanReach(village, at)
                        || HolderOfTheSeam(SeamPatchOf(at), claimant) is not null)
                    {
                        continue;
                    }

                    var claim = new List<GridPos>();
                    IReadOnlyList<int> seam = SeamTilesOf(SeamPatchOf(at));
                    for (int i = 0; i < seam.Count; i++)
                    {
                        GridPos tile = Zones.PositionOf(seam[i]);
                        if (MayClaim(face, tile, claimant, village))
                        {
                            claim.Add(tile);
                        }
                    }

                    return claim;
                }
            }
        }

        return System.Array.Empty<GridPos>();
    }

    /// <summary>
    /// A tile a quarry or a mine may take as its claim (D613) — the rules painted ground already keeps
    /// (`CanPaintWorkGround`): its face, walkable to from the village, nobody else's ground, nobody's yard.
    /// </summary>
    private bool MayClaim(Terrain face, GridPos tile, int claimant, GridPos village) =>
        Map.TerrainAt(tile) == face
        && TravelCost.CanReach(village, tile)
        && Zones.WorkGroundOwner(tile) is var owner && (owner == 0 || owner == claimant)
        && WhoseYard(tile) is null;

    /// <summary>
    /// The card's line for a quarry or a mine (D613): <i>"Works the seam beside it — 23 tiles of rock."</i> — or, with none to
    /// work, why. Null for a building with no face.
    /// </summary>
    public string? SayTheClaim(Workplace workplace)
    {
        ArgumentNullException.ThrowIfNull(workplace);
        if (FaceOf(workplace) is not Terrain face)
        {
            return null;
        }

        int tiles = Zones.WorkGroundTiles(workplace.Id);
        return tiles > 0
            ? $"Works the seam beside it — {tiles} {(tiles == 1 ? "tile" : "tiles")} of {FaceWord(face)}."
            : $"Works no seam — none of the {FaceWord(face)} it stands by is its own to work.";
    }

    /// <summary>The building other than <paramref name="claimant"/> that holds any tile of this seam, or null — a seam is never shared (D613).</summary>
    internal Workplace? HolderOfTheSeam(int patch, int claimant = 0)
    {
        IReadOnlyList<int> seam = SeamTilesOf(patch);
        for (int i = 0; i < seam.Count; i++)
        {
            int owner = Zones.WorkGroundOwner(Zones.PositionOf(seam[i]));
            if (owner != 0 && owner != claimant && FindWorkplace(owner) is Workplace holder)
            {
                return holder;
            }
        }

        return null;
    }

    /// <summary>
    /// ⭐ A finished quarry or mine takes the seam it stands by (D613) — whole tiles, through the brush's own door
    /// (<see cref="PaintWorkGround(Workplace, GridPos)"/>), so everything that reads painted ground reads this. Called when
    /// one is raised and when one finishes moving; a building with no face claims nothing.
    /// </summary>
    /// <returns>How many tiles were claimed.</returns>
    private int ClaimTheSeam(Workplace workplace)
    {
        if (JobsCatalog.FaceOf(workplace.Kind) is null || JobsCatalog.WorksAt(workplace.Kind) is not BuildingKind kind)
        {
            return 0;
        }

        int claimed = 0;
        IReadOnlyList<GridPos> seam = SeamToClaim(kind, workplace.Position.ToTile(), workplace.Id);
        for (int i = 0; i < seam.Count; i++)
        {
            claimed += PaintWorkGround(workplace, seam[i]).Allowed ? 1 : 0;
        }

        return claimed;
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
