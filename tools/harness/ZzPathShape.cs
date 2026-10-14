using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Persistence;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// ⛔ D579's MEASUREMENT HARNESS for the paths' gridding (Joe's note 1, `specs/path-shapes.md`) — kept in tools/harness/,
// COPIED into tests/Bclone.Sim.Tests/ to run and DELETED from there before any commit. Never committed under tests/.
//
// Opens each save in ZZ_SAVES (⚠️ a COPY of Joe's folder — SaveFile.Open keeps a damaged file as .bad beside it), runs it
// ZZ_YEARS more years (default 10) under the arm, and prints one ZZP line: how grid-like the worn ground is, in the
// village and outside it, and how the village fared. With ZZ_OUT set it also writes the village's ground as a character
// grid (`<save>.<arm>.grid`) for tools/harness/pathshape.py to draw.
//
// Arms (ZZ_ARMS, comma-separated): `now` (the save as Joe left it, not run on), `base`, `allowN`
// (path_shortcut_grass_allowance N). A code arm is the same harness run on a tree carrying that change.
public sealed class ZzPathShape
{
    private readonly ITestOutputHelper _o;

    public ZzPathShape(ITestOutputHelper o) => _o = o;

    public static IEnumerable<object[]> Runs()
    {
        string dir = Environment.GetEnvironmentVariable("ZZ_SAVES") ?? string.Empty;
        string[] arms = (Environment.GetEnvironmentVariable("ZZ_ARMS") ?? "now,base").Split(',');
        if (!Directory.Exists(dir))
        {
            yield return new object[] { "none", "none" };
            yield break;
        }

        foreach (string save in Directory.GetFiles(dir, "*.save").Order(StringComparer.Ordinal))
        {
            foreach (string arm in arms)
            {
                yield return new object[] { arm, save };
            }
        }
    }

    [Theory]
    [MemberData(nameof(Runs))]
    public void Shape(string arm, string save)
    {
        if (arm == "none")
        {
            _o.WriteLine("ZZP no saves: set ZZ_SAVES to a folder of copies");
            return;
        }

        SimConfig data = ShippedConfig.Load();
        if (arm.StartsWith("allow", StringComparison.Ordinal))
        {
            data = data with { PathShortcutGrassAllowance = int.Parse(arm[5..], System.Globalization.CultureInfo.InvariantCulture) };
        }

        SaveOpened opened = SaveFile.Open(save, data, new Bclone.Sim.Logging.InMemoryLogSink());
        Assert.True(opened.Loop is not null, opened.Detail);
        SimLoop loop = opened.Loop!;
        SimWorld world = loop.World;

        int years = int.TryParse(Environment.GetEnvironmentVariable("ZZ_YEARS"), out int y) ? y : 10;
        int aliveAtLoad = world.Population;
        int starvedAtLoad = Starved(world);
        if (arm != "now")
        {
            loop.Step(world.Config.TicksPerYear * years);
        }

        GeneratedMap map = world.Map;
        int w = map.Width, h = map.Height;
        var path = new byte[w * h];
        var built = new bool[w * h];
        for (int j = 0; j < h; j++)
        {
            for (int i = 0; i < w; i++)
            {
                var tile = new GridPos(i + map.MinX, j + map.MinY);
                path[(j * w) + i] = world.Paths.ClassAt(tile);
                built[(j * w) + i] = world.FootprintCovering(tile).Count > 0;
            }
        }

        // The village: within three tiles of a building. Outside it is the woods and the fields' edges.
        var village = new bool[w * h];
        for (int j = 0; j < h; j++)
        {
            for (int i = 0; i < w; i++)
            {
                if (!built[(j * w) + i])
                {
                    continue;
                }

                for (int dj = -3; dj <= 3; dj++)
                {
                    for (int di = -3; di <= 3; di++)
                    {
                        int x = i + di, yy = j + dj;
                        if (x >= 0 && x < w && yy >= 0 && yy < h)
                        {
                            village[(yy * w) + x] = true;
                        }
                    }
                }
            }
        }

        bool P(int x, int yy) => x >= 0 && x < w && yy >= 0 && yy < h && path[(yy * w) + x] > 0;

        int Run(int x, int yy, int dx, int dy)
        {
            int n = 0;
            while (P(x + (dx * (n + 1)), yy + (dy * (n + 1))))
            {
                n++;
            }

            return n;
        }

        // Per path tile: in a straight north-south or east-west lane of five or more; a sharp corner (two straight arms of
        // three or more meeting at a right angle, nothing across the corner); a diagonal link (a path tile diagonally next
        // to it with neither tile between them a path — a lane running at an angle).
        var counts = new int[2, 5];
        for (int j = 0; j < h; j++)
        {
            for (int i = 0; i < w; i++)
            {
                if (!P(i, j))
                {
                    continue;
                }

                int where = village[(j * w) + i] ? 0 : 1;
                counts[where, 0]++;
                bool B(int x, int yy) => x >= 0 && x < w && yy >= 0 && yy < h && built[(yy * w) + x];
                if (B(i + 1, j) || B(i - 1, j) || B(i, j + 1) || B(i, j - 1))
                {
                    counts[where, 4]++;
                }
                int across = 1 + Run(i, j, 1, 0) + Run(i, j, -1, 0);
                int down = 1 + Run(i, j, 0, 1) + Run(i, j, 0, -1);
                if (Math.Max(across, down) >= 5)
                {
                    counts[where, 1]++;
                }

                foreach ((int sx, int sy) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
                {
                    if (Run(i, j, sx, 0) >= 3 && Run(i, j, 0, sy) >= 3 && !P(i + sx, j + sy)
                        && !P(i - sx, j) && !P(i, j - sy))
                    {
                        counts[where, 2]++;
                        break;
                    }
                }

                foreach ((int sx, int sy) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
                {
                    if (P(i + sx, j + sy) && !P(i + sx, j) && !P(i, j + sy))
                    {
                        counts[where, 3]++;
                        break;
                    }
                }
            }
        }

        string Pct(int part, int whole) => whole == 0 ? "-" : $"{100 * part / whole}%";
        string name = Path.GetFileNameWithoutExtension(Path.GetDirectoryName(save) ?? save);
        name = Path.GetFileName(save);
        _o.WriteLine(
            $"ZZP {arm} {name} year {world.Clock.Year} alive {aliveAtLoad}->{world.Population} starved +{Starved(world) - starvedAtLoad} "
            + $"village path {counts[0, 0]} lanes {Pct(counts[0, 1], counts[0, 0])} corners {counts[0, 2]} diagonal {Pct(counts[0, 3], counts[0, 0])} beside {Pct(counts[0, 4], counts[0, 0])} "
            + $"| outside path {counts[1, 0]} lanes {Pct(counts[1, 1], counts[1, 0])} corners {counts[1, 2]} diagonal {Pct(counts[1, 3], counts[1, 0])}");

        if (Environment.GetEnvironmentVariable("ZZ_OUT") is string outDir)
        {
            WriteTheGrid(outDir, $"{name}.{arm}.grid", world, path, built, w, h);
        }
    }

    private static int Starved(SimWorld world) => world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Starvation);

    /// <summary>The village's ground as characters: ~ water, # a building, p packed, w worn, , a field, . anything else.</summary>
    private static void WriteTheGrid(string dir, string file, SimWorld world, byte[] path, bool[] built, int w, int h)
    {
        GeneratedMap map = world.Map;
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int j = 0; j < h; j++)
        {
            for (int i = 0; i < w; i++)
            {
                if (built[(j * w) + i])
                {
                    minX = Math.Min(minX, i); minY = Math.Min(minY, j); maxX = Math.Max(maxX, i); maxY = Math.Max(maxY, j);
                }
            }
        }

        if (maxX < 0)
        {
            return;
        }

        minX = Math.Max(0, minX - 8); minY = Math.Max(0, minY - 8);
        maxX = Math.Min(w - 1, maxX + 8); maxY = Math.Min(h - 1, maxY + 8);
        var lines = new List<string>();
        for (int j = minY; j <= maxY; j++)
        {
            var row = new System.Text.StringBuilder();
            for (int i = minX; i <= maxX; i++)
            {
                int k = (j * w) + i;
                Terrain t = map.TerrainAt(new GridPos(i + map.MinX, j + map.MinY));
                row.Append(built[k] ? '#'
                    : path[k] == 2 ? 'p'
                    : path[k] == 1 ? 'w'
                    : t == Terrain.Water ? '~'
                    : t is Terrain.Field or Terrain.Sown or Terrain.Ripe ? ','
                    : '.');
            }

            lines.Add(row.ToString());
        }

        Directory.CreateDirectory(dir);
        File.WriteAllLines(Path.Combine(dir, file), lines);
    }
}
