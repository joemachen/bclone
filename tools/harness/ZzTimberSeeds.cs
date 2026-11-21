using System.Collections.Concurrent;
using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

/// <summary>
/// D804's measure (copied into tests/ to run, deleted before any commit, D420): `WoodTests.TimberGatesGrowthWithoutStoppingIt`'s
/// seed table — seeds 1–12, alive at Year 120, gated (30 logs a house) and free — with contentment's longer wait on (200) and
/// off (100). Does the wait kill villages, or move the coin?
/// </summary>
public sealed class ZzTimberSeeds
{
    private readonly ITestOutputHelper _output;

    public ZzTimberSeeds(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Table()
    {
        var rows = new ConcurrentDictionary<(int Seed, bool Gated, int Wait), (int Alive, int Born, int UnsettledSeasons)>();
        var jobs = new List<(int, bool, int)>();
        for (int seed = 1; seed <= 12; seed++)
        {
            foreach (bool gated in new[] { true, false })
            {
                foreach (int wait in new[] { 200, 100 })
                {
                    jobs.Add((seed, gated, wait));
                }
            }
        }

        Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, job =>
        {
            (int seed, bool gated, int wait) = job;
            SimConfig config = VillageFixtures.Village with
            {
                LogsPerHouse = gated ? 30 : 0,
                ContentmentBirthIntervalPercent = wait,
            };
            SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink(), (ulong)seed);
            int unsettled = 0;
            ulong season = (ulong)config.TicksPerDay * (ulong)config.DaysPerSeason;
            for (int s = 0; s < 120 * 4; s++)
            {
                loop.Step((int)season);
                unsettled += loop.World.Villagers.Count(v => v.IsUnsettled);
            }

            int born = loop.World.Villagers.Count(v => v.BirthTick > 0 && v.ArrivedAtTick == 0UL);
            rows[job] = (loop.World.Population, born, unsettled);
        });

        _output.WriteLine("seed | gated 200 | gated 100 | free 200 | free 100   (alive@120 / born / unsettled adult-seasons)");
        for (int seed = 1; seed <= 12; seed++)
        {
            string Cell(bool g, int w)
            {
                (int a, int b, int u) = rows[(seed, g, w)];
                return $"{a}/{b}/{u}";
            }

            _output.WriteLine($"{seed} | {Cell(true, 200)} | {Cell(true, 100)} | {Cell(false, 200)} | {Cell(false, 100)}");
        }

        foreach (bool g in new[] { true, false })
        {
            foreach (int w in new[] { 200, 100 })
            {
                int alive = 0, dead = 0;
                for (int seed = 1; seed <= 12; seed++)
                {
                    alive += rows[(seed, g, w)].Alive;
                    dead += rows[(seed, g, w)].Alive == 0 ? 1 : 0;
                }

                _output.WriteLine($"{(g ? "gated" : "free")} wait {w}: alive {alive}, villages dead {dead}");
            }
        }
    }
}
