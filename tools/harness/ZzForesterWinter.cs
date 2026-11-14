using Bclone.Sim.Core;
using Bclone.Sim.Persistence;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// ⛔ D728's MEASUREMENT — `forests-and-gathering.md §10`: what stopping a forester planting in winter costs. Kept in
// tools/harness/, COPIED into tests/Bclone.Sim.Tests/ to run and DELETED from there before any commit (D420). The
// unattended harness never paints a forester's ground, so this runs on Joe's own villages, which have it.
// Run once on the parent and once on the change, on a folder of COPIES of his saves (`SaveFile.Open` keeps a damaged
// file as .bad beside it):
//   ZZ_SAVES=<folder> ZZ_LABEL=<old|new> [ZZ_YEARS=10] dotnet test tests/Bclone.Sim.Tests -c Release
//     --filter "FullyQualifiedName~ZzForesterWinter" --logger "console;verbosity=detailed"
// One ZZF line a save:
//   huts / ground — forester's huts with painted ground, and their tiles;
//   planted sp/su/fa/wi — grass → a young sapling on that ground (a forester's, D220), by season; seeded — the sweep's;
//   felled — logs felled anywhere since load; wooded/young/bare at load → end on the foresters' ground;
//   winter hand-ticks of forester-job holders: work (to the trees, at the trees), spare (clearing, tidying, a buffer),
//     other (home, eating, resting, fetching); alive, starved, cold since load; logs and firewood held at the end.
public sealed class ZzForesterWinter
{
    private readonly ITestOutputHelper _o;

    public ZzForesterWinter(ITestOutputHelper o) => _o = o;

    public static IEnumerable<object[]> Saves() =>
        Directory.GetFiles(Environment.GetEnvironmentVariable("ZZ_SAVES")!, "*.save").Select(p => new object[] { p });

    [Theory]
    [MemberData(nameof(Saves))]
    public void Step(string path)
    {
        SaveOpened opened = SaveFile.Open(path, ShippedConfig.Load(), new Bclone.Sim.Logging.InMemoryLogSink());
        Assert.True(opened.Loop is not null, opened.Detail);
        SimLoop loop = opened.Loop!;
        SimWorld world = loop.World;
        string label = Environment.GetEnvironmentVariable("ZZ_LABEL") ?? "arm";
        int years = int.TryParse(Environment.GetEnvironmentVariable("ZZ_YEARS"), out int y) ? y : 10;

        var ground = new List<GridPos>();
        int huts = 0;
        foreach (Workplace w in world.Workplaces)
        {
            if (w.Kind != JobKind.Forester || w.IsSite)
            {
                continue;
            }

            IReadOnlyList<int> owned = world.Zones.WorkGroundOf(w.Id);
            if (owned.Count > 0)
            {
                huts++;
            }

            for (int i = 0; i < owned.Count; i++)
            {
                ground.Add(world.Zones.PositionOf(owned[i]));
            }
        }

        var was = new Terrain[ground.Count];
        for (int i = 0; i < ground.Count; i++)
        {
            was[i] = world.Map.TerrainAt(ground[i]);
        }

        string atLoad = Shape(world, ground);
        int aliveAtLoad = world.Population;
        int starvedAtLoad = Dead(world, CauseOfDeath.Starvation);
        int coldAtLoad = Dead(world, CauseOfDeath.Cold);
        int felledAtLoad = world.LogsEverFelled;

        var planted = new int[4];
        int seeded = 0;
        long work = 0, spare = 0, other = 0;
        int ticks = world.Config.TicksPerYear * years;

        for (int t = 0; t < ticks; t++)
        {
            // A step runs on the clock it starts on and moves the tick after (`SimLoop.StepOnce`): read the season first, or
            // a sapling put in on autumn's last tick is counted as winter's (D728's first after-run did exactly that).
            Season season = world.Clock.Season;
            loop.StepOnce();

            for (int i = 0; i < ground.Count; i++)
            {
                Terrain now = world.Map.TerrainAt(ground[i]);
                if (now == Terrain.Sapling && was[i] == Terrain.Grass)
                {
                    // A forester's sapling carries the young bit (D220); one the sweep seeded does not.
                    if (world.Map.IsYoungSapling(ground[i]))
                    {
                        planted[(int)season]++;
                    }
                    else
                    {
                        seeded++;
                    }
                }

                was[i] = now;
            }

            if (season != Season.Winter)
            {
                continue;
            }

            foreach (Villager v in world.Villagers)
            {
                if (!v.Alive || world.FindWorkplace(v.WorkplaceId) is not { Kind: JobKind.Forester })
                {
                    continue;
                }

                switch (v.State)
                {
                    case VillagerState.TravelingToTrees or VillagerState.Cutting:
                        work++;
                        break;
                    case VillagerState.Clearing or VillagerState.TidyingGround or VillagerState.ClearingABuffer:
                        spare++;
                        break;
                    default:
                        other++;
                        break;
                }
            }
        }

        _o.WriteLine(
            $"ZZF {label} {Path.GetFileNameWithoutExtension(path)} y{world.Clock.Year} huts {huts} ground {ground.Count} "
            + $"planted sp {planted[0]} su {planted[1]} fa {planted[2]} wi {planted[3]} "
            + $"seeded {seeded} felled {world.LogsEverFelled - felledAtLoad} wood {atLoad} -> {Shape(world, ground)} "
            + $"winter work {work} spare {spare} other {other} "
            + $"alive {aliveAtLoad} -> {world.Population} starved {Dead(world, CauseOfDeath.Starvation) - starvedAtLoad} "
            + $"cold {Dead(world, CauseOfDeath.Cold) - coldAtLoad} logs {world.TotalLogs()} firewood {world.TotalFirewood()}");
    }

    private static string Shape(SimWorld world, List<GridPos> ground)
    {
        int wooded = 0, young = 0, bare = 0;
        foreach (GridPos at in ground)
        {
            switch (world.Map.TerrainAt(at))
            {
                case Terrain.Forest: wooded++; break;
                case Terrain.Sapling: young++; break;
                case Terrain.Grass: bare++; break;
            }
        }

        return $"{wooded}/{young}/{bare}";
    }

    private static int Dead(SimWorld world, CauseOfDeath cause) =>
        world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == cause);
}
