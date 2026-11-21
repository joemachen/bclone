using System.Collections.Concurrent;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// W2 step 6's measure (copied into tests/ to run, deleted before any commit, D420): the anti-vacuity pair's shapes before a
/// number is typed (`morale.md §11` item 4). A NEGLECTED village — no house can ever be raised (timber past reach), so nobody
/// ever has a roof — against the KEPT one, the fixture as shipped. Seeds 1–12: who left, who died, who is alive, the first
/// departure's year.
/// </summary>
public sealed class ZzLeaving
{
    private readonly ITestOutputHelper _output;

    public ZzLeaving(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData("neglected", 25)]
    [InlineData("kept", 50)]
    public void Table(string arm, int years)
    {
        var rows = new ConcurrentDictionary<int, string>();
        Parallel.ForEach(Enumerable.Range(1, 12), new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, seed =>
        {
            SimConfig config = arm == "neglected" ? VillageFixtures.Village with { LogsPerHouse = 1_000_000 } : VillageFixtures.Village;
            SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink(), (ulong)seed);
            for (int y = 0; y < years; y++)
            {
                loop.Step(config.TicksPerYear);
            }

            SimWorld w = loop.World;
            int left = w.Villagers.Count(v => v.LeftAtTick is not null);
            int died = w.Villagers.Count(v => v.DiedAtTick is not null);
            ulong? first = w.Villagers.Where(v => v.LeftAtTick is not null).Select(v => v.LeftAtTick).Min();
            string firstYear = first is ulong f ? SimClock.FromTick(f, config).Year.ToString() : "-";
            int housed = w.Households.Count(h => h.HasHome && w.LivingMembersOf(h) > 0);
            rows[seed] = $"{seed} | left {left} | died {died} | alive {w.Population} | first left Y{firstYear} | housed households {housed}";
        });

        _output.WriteLine($"{arm}, {years} years");
        foreach (int seed in rows.Keys.OrderBy(k => k))
        {
            _output.WriteLine(rows[seed]);
        }
    }
}
