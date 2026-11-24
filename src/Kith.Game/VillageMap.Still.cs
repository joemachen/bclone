using Kith.Sim.Core;
using Kith.Sim.World;
using Godot;

namespace Kith.Game;

/// <summary>
/// ⭐ A map drawn for one still picture (A234, `title-snapshot.md §3`): what it takes from the live map, so the picture costs a
/// draw and not a second valley.
/// </summary>
public sealed partial class VillageMap
{
    /// <summary>Where the camera is: the tile at the map's centre, and pixels a tile — what <see cref="LookAt"/> was given (A236).</summary>
    internal (Vector2 Centre, float Ppt) Camera => (_centreTile, _pixelsPerTile);

    /// <summary>Whether every figure this map drew last was painted — a frame paints only a few new ones.</summary>
    internal bool EveryoneIsPainted => _bakesThisFrame < PersonBakesPerFrame;

    /// <summary>
    /// Take what <paramref name="live"/> has already made of <paramref name="world"/>: the baked ground, the woods, the lanes, the
    /// painted land's shapes, the figures, the buildings (their smoke and lamplight with them). Thornfield's first draw takes 1.7 s
    /// without it.
    /// Call it once the watercolour is on and before the first draw. A live map not in watercolour, or on another world, has
    /// nothing this one can use.
    /// </summary>
    /// <remarks>
    /// ⚠️ The ground and the woods are <b>shared, not copied</b>: both are brought up to date by whichever map draws first, from a
    /// diff both see, so neither goes stale. The buildings are copied, because their pen lines are laid for a zoom.
    /// </remarks>
    internal void BorrowTheGroundOf(VillageMap live, SimWorld world)
    {
        if (!live._watercolour || live._watercolourMaterial is null || _watercolourMaterial is null || !ReferenceEquals(live._world, world))
        {
            return;
        }

        _valley = live._valley;

        _scenery = live._scenery;
        _sceneryShadow = live._sceneryShadow;
        _faceShadow = live._faceShadow;
        _facesAt = live._facesAt;
        _sceneryOf = live._sceneryOf;
        _sceneryAtGeneration = live._sceneryAtGeneration;
        _berryRadiusBuilt = live._berryRadiusBuilt;
        _chunksAcross = live._chunksAcross;
        _chunksDown = live._chunksDown;

        _trailsOf = live._trailsOf;
        _trailsCollectedAt = live._trailsCollectedAt;
        _trailsWallsAt = live._trailsWallsAt;
        _trailCollections = live._trailCollections;
        _trailGrade = live._trailGrade;
        _trail.AddRange(live._trail);
        _network = live._network;
        _networkWindow = live._networkWindow;

        // The field the ground's paths are painted from; with it, a lane that moves between the two draws takes a texture of its own.
        _pathsOf = live._pathsOf;
        _pathsTexture = live._pathsTexture;
        _pathFieldShown = live._pathFieldShown;
        _pathsWorld = live._pathsWorld;

        // What was traced from the paint, and every counter it was traced at (`TraceTheZonesIfTheyMoved` clears and refills its own).
        _zoneOutlines.AddRange(live._zoneOutlines);
        _palisadePosts.AddRange(live._palisadePosts);
        _zoneFills.AddRange(live._zoneFills);
        _fieldFills.AddRange(live._fieldFills);
        _fieldEdges.AddRange(live._fieldEdges);
        _householdFences.AddRange(live._householdFences);
        _householdFenceLoops.UnionWith(live._householdFenceLoops);
        _fencePieces = live._fencePieces;
        _fenceRuns = live._fenceRuns?.ToList();
        _spentMarks.UnionWith(live._spentMarks);
        foreach ((int owner, List<GridPos> plot) in live._plotTilesByOwner)
        {
            _plotTilesByOwner[owner] = plot;
        }

        _outlinesTracedAt = live._outlinesTracedAt;
        _outlinesTracedAtTerrain = live._outlinesTracedAtTerrain;
        _outlinesTracedAtBuildings = live._outlinesTracedAtBuildings;
        _outlinesTracedAtWalls = live._outlinesTracedAtWalls;
        _outlinesTracedAtStanding = live._outlinesTracedAtStanding;
        _spentAtEdits = live._spentAtEdits;
        _spentAtTerrain = live._spentAtTerrain;

        _roofTextures = live._roofTextures;
        foreach ((FigureKey key, PersonCache person) in live._people)
        {
            _people[key] = person;
        }

        _roofCacheGeneration = live._roofCacheGeneration;
        foreach ((Vector2 at, RoofCache roof) in live._roofCache)
        {
            _roofCache[at] = new RoofCache
            {
                Signature = roof.Signature,
                Runs = roof.Runs.ToList(),
                LastSeen = roof.LastSeen,
                Flues = roof.Flues,
                Smoke = roof.Smoke,
                Hub = roof.Hub,
                Lamps = roof.Lamps,
                Light = roof.Light,
                Copper = roof.Copper,
                Steam = roof.Steam,
                SailsAt = roof.SailsAt,
                SailSpeed = roof.SailSpeed,
            };
        }

        foreach ((GridPos door, Vector2 step) in live._paintedSteps)
        {
            _paintedSteps[door] = step;
        }

        // The river's and the hills' bakes live in the layer's own uniforms: handed over whole, the camera's set again by the draw.
        foreach (Godot.Collections.Dictionary uniform in live._watercolourMaterial.Shader.GetShaderUniformList())
        {
            StringName name = uniform["name"].AsStringName();
            _watercolourMaterial.SetShaderParameter(name, live._watercolourMaterial.GetShaderParameter(name));
        }

        _riverOf = live._riverOf;
        _riverTable = live._riverTable;
        _hillsOf = live._hillsOf;
        _hillsTexture = live._hillsTexture;
        _hachuresTexture = live._hachuresTexture;
    }

    /// <summary>The capture tools' door (GDScript reaches only public members): this village's picture for the title, to a file.</summary>
    public void TakeTheTitlePicture(string path)
    {
        if (_world is not null)
        {
            TitleSnapshot.Take(GetTree().Root, this, _world, path);
        }
    }
}
