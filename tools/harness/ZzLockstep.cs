using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// ⛔ DO THE FOUNDERS STOP ON THE SAME TICK? (D528; Joe 2026-10-07: "all 4 founders skip at the same time it seems").
// Kept in tools/harness/, COPIED into tests/Bclone.Sim.Tests/ to run and DELETED from there.
//
//   cp tools/harness/ZzLockstep.cs tests/Bclone.Sim.Tests/
//   dotnet test tests/Bclone.Sim.Tests -c Release --filter ZzLockstep --logger "console;verbosity=detailed"
//   rm tests/Bclone.Sim.Tests/ZzLockstep.cs
//
// The view glides each villager from last tick's position to this tick's, linearly, over the tick
// (`VillageMap.DrawnCentre`), so a dot's on-screen speed is the tick's move and it changes only at a tick boundary —
// one every villager shares. D525 measured how far a tick moves anybody, per villager; this asks whether they
// STOP TOGETHER. A STALL is a walker who moved more than half a tile last tick, moved nothing this tick and is still
// on the same errand (D525's stutter); a tick is counted by how many villagers stalled on it, and how many of the
// stalls were a meal (`Villager.JustAte`, a meal on the road costs a tick by design — D10).
// Arms: `played` (`ColdStartTests.PlayTheOpening`) and `unattended` (nothing marked: what `BCLONE_FRAME_SAMPLE`
// founds). ZZ_ARMS, ZZ_SEEDS, ZZ_DAYS (default 120), ZZ_TABLE (ticks to print, default 48; a `*` is a meal).
public sealed class ZzLockstep
{
    private readonly ITestOutputHelper _o;

    public ZzLockstep(ITestOutputHelper o) => _o = o;

    [Theory]
    [MemberData(nameof(Runs))]
    public void Measure(string arm, ulong seed)
    {
        SimConfig config = ShippedConfig.Load() with { Seed = seed };
        SimLoop loop = SimFactory.CreatePhase0(config, new Bclone.Sim.Logging.InMemoryLogSink());
        SimWorld world = loop.World;
        if (arm.StartsWith("played", StringComparison.Ordinal))
        {
            ColdStartTests.PlayTheOpening(world);
        }

        // ⭐ THE STAGGER'S UNITS (D528). D190 starts each villager's hunger at their `Rhythm` — 0..3, a number of
        // TICKS — as hunger POINTS, and hunger rises 7 a tick: the offset is under one tick's worth, so meals still
        // land together. `ticks` restates it in ticks' worth of hunger; `spread` spaces the founders evenly over a
        // meal's whole cycle (the threshold). Neither changes the sim — the harness posts them at t0.
        if (arm.EndsWith("+ticks", StringComparison.Ordinal))
        {
            foreach (Villager v in world.Villagers)
            {
                v.Hunger = v.Rhythm * config.HungerPerTick;
            }
        }
        else if (arm.EndsWith("+spread", StringComparison.Ordinal))
        {
            int n = world.Villagers.Count;
            for (int i = 0; i < n; i++)
            {
                world.Villagers[i].Hunger = i * config.EatThreshold / n;
            }
        }

        int days = Environment.GetEnvironmentVariable("ZZ_DAYS") is string d ? int.Parse(d, System.Globalization.CultureInfo.InvariantCulture) : 120;
        int table = Environment.GetEnvironmentVariable("ZZ_TABLE") is string tb ? int.Parse(tb, System.Globalization.CultureInfo.InvariantCulture) : 48;

        var stalledTogether = new long[5]; // ticks on which 1, 2, 3, 4+ villagers stalled (index 0 unused)
        long stalls = 0, mealStalls = 0, stallsInAGroup = 0, mealStallsInAGroup = 0, walkerTicks = 0;
        var lastMove = new Dictionary<int, double>();
        var before = Positions(world);

        for (int t = 0; t < config.TicksPerDay * days; t++)
        {
            ulong tick = world.Tick;
            loop.StepOnce();
            var after = Positions(world);

            var stalledNow = new List<(int Id, bool Ate)>();
            var row = new List<string>();
            foreach ((int id, (double x, double y, VillagerState state, bool ate)) in after)
            {
                double move = before.TryGetValue(id, out var was) ? Math.Sqrt(((x - was.X) * (x - was.X)) + ((y - was.Y) * (y - was.Y))) : 0d;
                bool walkedLastTick = lastMove.TryGetValue(id, out double previous) && previous > 0.5;
                if (walkedLastTick)
                {
                    walkerTicks++;
                }

                if (walkedLastTick && move == 0d && was.State == state)
                {
                    stalledNow.Add((id, ate));
                }

                lastMove[id] = move;
                row.Add($"{id}:{move:F2}{(ate ? "*" : string.Empty)} {state}");
            }

            if (stalledNow.Count > 0)
            {
                stalledTogether[Math.Min(stalledNow.Count, 4)]++;
                stalls += stalledNow.Count;
                mealStalls += stalledNow.Count(s => s.Ate);
                if (stalledNow.Count > 1)
                {
                    stallsInAGroup += stalledNow.Count;
                    mealStallsInAGroup += stalledNow.Count(s => s.Ate);
                }
            }

            if (t < table)
            {
                _o.WriteLine($"ZZT {arm} t{tick,4} day-tick {tick % (ulong)config.TicksPerDay} stalled {stalledNow.Count} | {string.Join(" | ", row)}");
            }

            before = after;
        }

        _o.WriteLine($"ZZL {arm} seed {seed}, {days} days, alive {before.Count}: {stalls} stalls in {walkerTicks} walker-ticks ({stalls * 1000.0 / Math.Max(1, walkerTicks):F1} per 1,000), "
            + $"{mealStalls} of them meals; ticks with 1 stalled {stalledTogether[1]}, 2 {stalledTogether[2]}, 3 {stalledTogether[3]}, 4+ {stalledTogether[4]}; "
            + $"stalls in a group of 2+: {stallsInAGroup} ({stallsInAGroup * 100.0 / Math.Max(1, stalls):F0} %), {mealStallsInAGroup} of them meals");
    }

    private static double Tiles(Fixed value) => value.RawBits / 4294967296.0;

    private static Dictionary<int, (double X, double Y, VillagerState State, bool Ate)> Positions(SimWorld world)
    {
        var at = new Dictionary<int, (double, double, VillagerState, bool)>();
        foreach (Villager v in world.Villagers)
        {
            if (v.Alive)
            {
                at[v.Id] = (Tiles(v.Position.X), Tiles(v.Position.Y), v.State, v.JustAte);
            }
        }

        return at;
    }

    public static IEnumerable<object[]> Runs()
    {
        string[] arms = (Environment.GetEnvironmentVariable("ZZ_ARMS") ?? "played").Split(',');
        foreach (string arm in arms)
        {
            IEnumerable<ulong> seeds = Environment.GetEnvironmentVariable("ZZ_SEEDS") is string zs
                ? zs.Split(',').Select(ulong.Parse)
                : new[] { 12345UL, 1UL, 2UL, 3UL, 4UL, 5UL };
            foreach (ulong s in seeds)
            {
                yield return new object[] { arm, s };
            }
        }
    }
}
