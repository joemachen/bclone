using Kith.Sim.Core;
using Kith.Sim.Persistence;
using Kith.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

// ⛔ D857's READ OF JOE'S SAVE — kept in tools/harness/, COPIED into tests/ to run and DELETED from there (D420). Joe: "i painted
// the farm field on the existing quarry and it overlapped." Before a fix: on the NEWEST save of each folder under ZZ_SAVES (a
// COPY), every painted tile that is (a) under a standing building's footprint (WhatStandsAt), or (b) held by one workplace and
// beside a tile another holds — the two ways a field can "overlap" a quarry: in the sim, or only where the drawing meets.
// Reads, never steps. Copy in, `--filter ZzFieldOverlap --logger "console;verbosity=detailed"`, delete.
public sealed class ZzFieldOverlap
{
    private readonly ITestOutputHelper _o;

    public ZzFieldOverlap(ITestOutputHelper o) => _o = o;

    [Fact]
    public void Read()
    {
        if (Environment.GetEnvironmentVariable("ZZ_SAVES") is not string folder)
        {
            return;
        }

        foreach (string dir in Directory.GetDirectories(folder).OrderBy(d => d, StringComparer.Ordinal))
        {
            string newest = Directory.GetFiles(dir, "*.save").OrderByDescending(File.GetLastWriteTimeUtc).First();
            SaveOpened opened = SaveFile.Open(newest, ShippedConfig.Load());
            if (opened.Loop is not SimLoop loop)
            {
                _o.WriteLine($"ZZX {newest} refused {opened.Refusal}");
                continue;
            }

            SimWorld world = loop.World;
            _o.WriteLine($"ZZO {Path.GetFileName(dir)} {Path.GetFileName(newest)} year {world.Clock.Year}");
            foreach (Workplace w in world.Workplaces.Where(w => !w.IsSite && w.Kind is JobKind.Quarrier or JobKind.Farmer or JobKind.Miner))
            {
                var held = new List<GridPos>();
                for (int y = -world.Map.Height; y <= world.Map.Height; y++)
                {
                    for (int x = -world.Map.Width; x <= world.Map.Width; x++)
                    {
                        var t = new GridPos(x, y);
                        if (world.Map.Contains(t) && world.Zones.WorkGroundOwner(t) == w.Id)
                        {
                            held.Add(t);
                        }
                    }
                }

                var foot = w.Footprint.CoveredTiles().ToList();
                var underABuilding = held.Where(t => world.WhatStandsAt(t) is BuildingKind k).Select(t => $"({t.X},{t.Y}):{world.WhatStandsAt(t)}").ToList();
                var onStone = held.Count(t => TerrainRules.IsSeam(world.Map.TerrainAt(t)));
                _o.WriteLine(
                    $"ZZO   {w.Kind} #{w.Id} {w.Name} at ({w.Tile.X},{w.Tile.Y}) footprint [{string.Join(' ', foot.Select(t => $"({t.X},{t.Y})"))}] "
                    + $"ground {held.Count} tiles, x {(held.Count == 0 ? "-" : $"{held.Min(t => t.X)}..{held.Max(t => t.X)}")} "
                    + $"y {(held.Count == 0 ? "-" : $"{held.Min(t => t.Y)}..{held.Max(t => t.Y)}")}; on seam {onStone}; "
                    + $"under a building [{string.Join(' ', underABuilding)}]");

                // Around a quarry: every tile's owner (q = this quarry, f = a farm, . = nobody), its painted sub-tiles of 16,
                // and B where a building stands — the corner the drawing and the sim must agree about.
                if (w.Kind == JobKind.Quarrier)
                {
                    for (int y = w.Tile.Y - 5; y <= w.Tile.Y + 3; y++)
                    {
                        var row = new System.Text.StringBuilder();
                        for (int x = w.Tile.X - 4; x <= w.Tile.X + 6; x++)
                        {
                            var t = new GridPos(x, y);
                            int owner = world.Zones.WorkGroundOwner(t);
                            char who = owner == 0 ? '.' : owner == w.Id ? 'q' : world.FindWorkplace(owner)?.Kind == JobKind.Farmer ? 'f' : '?';
                            char stands = world.WhatStandsAt(t) is BuildingKind ? 'B' : ' ';
                            char seam = TerrainRules.IsSeam(world.Map.TerrainAt(t)) ? 's' : ' ';
                            row.Append($"{who}{world.Zones.WorkGroundSubTilesOn(t),2}{stands}{seam} ");
                        }

                        _o.WriteLine($"ZZG   y {y,4} x {w.Tile.X - 4}.. {row}");
                    }
                }
            }
        }
    }
}
