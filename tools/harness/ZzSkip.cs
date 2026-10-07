using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// ⛔ THE SKIP'S MEASUREMENT (D525; D402/D403's open item, Joe 2026-10-06: "there are definitely still skips.
// frequently."). Kept in tools/harness/, COPIED into tests/Bclone.Sim.Tests/ to run and DELETED from there.
//
//   cp tools/harness/ZzSkip.cs tests/Bclone.Sim.Tests/
//   dotnet test tests/Bclone.Sim.Tests -c Release --filter ZzSkip --logger "console;verbosity=detailed"
//   rm tests/Bclone.Sim.Tests/ZzSkip.cs
//
// What the eye sees, measured in the sim, per villager per tick:
//  - SNAP: the position moved more than √2 tiles — `VillageMap.DrawnCentre` stops gliding and teleports.
//    Counted by the state change that made it (`before->after`), which is what says which code did it.
//  - DASH: moved more than one tile but no more than √2 — a glide visibly faster than a walk.
//  - FAN: the crowd fan (`VillageMap.FanOffset`: rank by id among everyone alive on the tile, a ring of radius
//    0.30) moved by more than 0.15 of a tile. It is not interpolated, so it is an instant jump. Split into the
//    villager's own (they crossed onto or off a crowded tile) and a bystander's (someone else came or went).
// Also: a histogram of how far a tick moves anybody (`walk`: 0 / ≤0.5 / ≤0.95 / ≤1.05 / ≤1.3 / more — a walk is a
// tile a tick on grass and 1.25 on a packed path, so ≤1.3 is walking, not a skip), the size of each fan jump, and
// a STUTTER: a walker who stood a tick between two moving ticks of one journey — and how many of those were a
// meal on the road (eating pre-empts everything and costs a tick, by design: D10).
// Arms: `played` (ColdStartTests.PlayTheOpening, shipped config); `every` (plus a lodge, fishery and farm raised
// free) died out in the first run (D515's fixture effect) and is kept only by name. ZZ_ARMS, ZZ_SEEDS, ZZ_YEARS.
public sealed class ZzSkip
{
    private const double FanRadius = 0.30;
    private const double SnapSquared = 2.0;
    private const double FanVisible = 0.15;

    private readonly ITestOutputHelper _o;

    public ZzSkip(ITestOutputHelper o) => _o = o;

    [Theory]
    [MemberData(nameof(Runs))]
    public void Measure(string arm, ulong seed)
    {
        SimConfig config = ShippedConfig.Load() with { Seed = seed };
        SimLoop loop = SimFactory.CreatePhase0(config, new Bclone.Sim.Logging.InMemoryLogSink());
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);
        if (arm == "every")
        {
            HuntingTests.RaiseALodgeFor(world);
            FishingTests.RaiseAFishery(world);
            Workplace farm = FarmFixtures.RaiseAFarm(world);
            FarmFixtures.GiveItGround(world, farm, 3);
        }

        int years = Environment.GetEnvironmentVariable("ZZ_YEARS") is string y ? int.Parse(y, System.Globalization.CultureInfo.InvariantCulture) : 20;
        long villagerTicks = 0, snaps = 0, dashes = 0, fanOwn = 0, fanBystander = 0, anySkip = 0, stutters = 0, stutterMeals = 0;
        var walkBins = new long[6]; // a tick's move: 0, (0,0.5], (0.5,0.95], (0.95,1.05], (1.05,1.3], >1.3
        var fanBins = new long[3];  // a fan jump's size: (0.15,0.3], (0.3,0.45], >0.45
        var lastMove = new Dictionary<int, double>();
        var ateLast = new Dictionary<int, bool>();
        double worst = 0;
        var snapCauses = new Dictionary<string, int>();
        var dashCauses = new Dictionary<string, int>();
        var fanOwnCauses = new Dictionary<string, int>();

        // ⭐ D525's rule, modelled: the fan among the STANDING only (a villager whose position did not move this tick),
        // gliding over a tick. Counted as GLIDES (a change of more than 0.15 to someone's place on a ring) — no
        // longer an instant jump, and never caused by a walker passing.
        long glides = 0;
        var newFan = new Dictionary<int, (double X, double Y)>();
        var before = Snapshot(world);
        for (int t = 0; t < config.TicksPerYear * years; t++)
        {
            loop.StepOnce();
            var after = Snapshot(world);
            foreach ((int id, (double X, double Y, GridPos Tile, double FX, double FY, VillagerState State, bool Ate) now) in after)
            {
                if (!before.TryGetValue(id, out var was))
                {
                    continue;
                }

                villagerTicks++;
                double dx = now.X - was.X, dy = now.Y - was.Y;
                double d2 = (dx * dx) + (dy * dy);
                worst = Math.Max(worst, Math.Sqrt(d2));
                string cause = $"{was.State}->{now.State}";
                bool skipped = false;
                if (d2 > SnapSquared)
                {
                    snaps++;
                    skipped = true;
                    snapCauses[cause] = snapCauses.GetValueOrDefault(cause) + 1;
                }
                else if (d2 > 1.0)
                {
                    dashes++;
                    dashCauses[cause] = dashCauses.GetValueOrDefault(cause) + 1;
                }

                double moved = Math.Sqrt(d2);
                walkBins[moved == 0 ? 0 : moved <= 0.5 ? 1 : moved <= 0.95 ? 2 : moved <= 1.05 ? 3 : moved <= 1.3 ? 4 : 5]++;

                // A STUTTER: a walker who stood still for a tick between two moving ticks, same journey.
                if (lastMove.TryGetValue(id, out double previousMove) && previousMove == 0 && moved > 0.5
                    && was.State == now.State && IsWalking(now.State))
                {
                    stutters++;
                    if (ateLast.GetValueOrDefault(id))
                    {
                        stutterMeals++;
                    }
                }

                lastMove[id] = moved;
                ateLast[id] = now.Ate;

                double fx = now.FX - was.FX, fy = now.FY - was.FY;
                double fan = Math.Sqrt((fx * fx) + (fy * fy));
                if (fan > FanVisible)
                {
                    skipped = true;
                    fanBins[fan <= 0.3 ? 0 : fan <= 0.45 ? 1 : 2]++;
                    if (now.Tile != was.Tile)
                    {
                        fanOwn++;
                        fanOwnCauses[cause] = fanOwnCauses.GetValueOrDefault(cause) + 1;
                    }
                    else
                    {
                        fanBystander++;
                    }
                }

                if (skipped)
                {
                    anySkip++;
                }
            }

            var standingOn = new Dictionary<GridPos, List<int>>();
            foreach ((int id, var now) in after)
            {
                if (before.TryGetValue(id, out var was) && was.X == now.X && was.Y == now.Y)
                {
                    if (!standingOn.TryGetValue(now.Tile, out List<int>? here))
                    {
                        here = new List<int>();
                        standingOn[now.Tile] = here;
                    }

                    here.Add(id);
                }
            }

            var target = new Dictionary<int, (double X, double Y)>();
            foreach (List<int> here in standingOn.Values)
            {
                here.Sort();
                for (int rank = 0; rank < here.Count && here.Count > 1; rank++)
                {
                    double angle = 2 * Math.PI * rank / here.Count;
                    target[here[rank]] = (Math.Cos(angle) * FanRadius, Math.Sin(angle) * FanRadius);
                }
            }

            foreach (int id in after.Keys)
            {
                (double X, double Y) next = target.GetValueOrDefault(id);
                (double X, double Y) last = newFan.GetValueOrDefault(id);
                double gx = next.X - last.X, gy = next.Y - last.Y;
                if ((gx * gx) + (gy * gy) > FanVisible * FanVisible)
                {
                    glides++;
                }

                newFan[id] = next;
            }

            before = after;
        }

        static string Top(Dictionary<string, int> causes) =>
            string.Join(" ", causes.OrderByDescending(c => c.Value).Take(6).Select(c => $"{c.Key}={c.Value}"));

        double per1000 = 1000.0 / Math.Max(1, villagerTicks);
        _o.WriteLine(
            $"ZZS {arm} {seed} vticks {villagerTicks} alive {world.Population} "
            + $"snap {snaps} ({snaps * per1000:F2}/1k) dash {dashes} ({dashes * per1000:F2}/1k) "
            + $"fanOwn {fanOwn} ({fanOwn * per1000:F2}/1k) fanBy {fanBystander} ({fanBystander * per1000:F2}/1k) "
            + $"any {anySkip} ({anySkip * per1000:F2}/1k) worst {worst:F2} stutter {stutters} ({stutters * per1000:F2}/1k, {stutterMeals} a meal) "
            + $"walk [{string.Join(" ", walkBins)}] fan [{string.Join(" ", fanBins)}] glides-D525 {glides} ({glides * per1000:F2}/1k) "
            + $"snapCauses [{Top(snapCauses)}] dashCauses [{Top(dashCauses)}] fanOwnCauses [{Top(fanOwnCauses)}]");
    }

    private static double Tiles(Fixed value) => value.RawBits / 4294967296.0;

    private static bool IsWalking(VillagerState state) => state.ToString().StartsWith("Travel", StringComparison.Ordinal)
        || state is VillagerState.HaulingToStore or VillagerState.FetchingFromStore or VillagerState.CollectingForMarket
            or VillagerState.StockingTheMarket or VillagerState.WalkingOutToTend or VillagerState.WalkingBackToTheSteading;

    // Position and the drawn fan offset of everyone alive, the view's own rule: rank by id among the living on a tile.
    private static Dictionary<int, (double X, double Y, GridPos Tile, double FX, double FY, VillagerState State, bool Ate)> Snapshot(SimWorld world)
    {
        var byTile = new Dictionary<GridPos, List<int>>();
        foreach (Villager v in world.Villagers)
        {
            if (!v.Alive)
            {
                continue;
            }

            if (!byTile.TryGetValue(v.Tile, out List<int>? here))
            {
                here = new List<int>();
                byTile[v.Tile] = here;
            }

            here.Add(v.Id);
        }

        var snap = new Dictionary<int, (double, double, GridPos, double, double, VillagerState, bool)>();
        foreach (Villager v in world.Villagers)
        {
            if (!v.Alive)
            {
                continue;
            }

            List<int> here = byTile[v.Tile];
            double fx = 0, fy = 0;
            if (here.Count > 1)
            {
                double angle = 2 * Math.PI * here.IndexOf(v.Id) / here.Count;
                fx = Math.Cos(angle) * FanRadius;
                fy = Math.Sin(angle) * FanRadius;
            }

            snap[v.Id] = (Tiles(v.Position.X), Tiles(v.Position.Y), v.Tile, fx, fy, v.State, v.JustAte);
        }

        return snap;
    }

    public static IEnumerable<object[]> Runs()
    {
        string[] arms = (Environment.GetEnvironmentVariable("ZZ_ARMS") ?? "played").Split(',');
        foreach (string arm in arms)
        {
            IEnumerable<ulong> seeds = Environment.GetEnvironmentVariable("ZZ_SEEDS") is string zs
                ? zs.Split(',').Select(ulong.Parse)
                : Enumerable.Range(1, 8).Select(x => (ulong)x).Append(12345UL);
            foreach (ulong s in seeds)
            {
                yield return new object[] { arm, s };
            }
        }
    }
}
