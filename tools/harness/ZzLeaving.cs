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
    [InlineData("kept-water", 50)]
    public void Table(string arm, int years)
    {
        var rows = new ConcurrentDictionary<int, string>();
        Parallel.ForEach(Enumerable.Range(1, 12), new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, seed =>
        {
            SimConfig config = arm switch
            {
                "neglected" => VillageFixtures.Village with { LogsPerHouse = 1_000_000 },
                "kept-water" => VillageFixtures.Village with { WaterPerPersonPerDay = 0 },
                _ => VillageFixtures.Village,
            };
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
            string why = string.Join("; ", w.Villagers.Where(v => v.LeftAtTick is not null).Select(v =>
                $"{v.Name} Y{SimClock.FromTick(v.LeftAtTick!.Value, config).Year} [" + string.Join(",", Enum.GetValues<ContentmentCause>().Where(c => (v.ContentmentCauses & (1 << (int)c)) != 0)) + $"] tiles {v.CommuteTiles} {(v.LifeStage == LifeStage.Child ? "child" : "")}"));
            rows[seed] = $"{seed} | left {left} | died {died} | alive {w.Population} | first left Y{firstYear} | housed households {housed} | {why}";
        });

        _output.WriteLine($"{arm}, {years} years");
        if (arm == "kept")
        {
            // Phase 0's village of one, leaving on: what does its villager hold?
            SimConfig p0 = Phase0Fixtures.Plenty with { ContentmentLeavingSeasons = 8 };
            SimLoop one = SimFactory.CreatePhase0(p0, new InMemoryLogSink());
            var held = new List<string>();
            for (int s = 0; s < 12; s++)
            {
                one.Step(p0.TicksPerDay * p0.DaysPerSeason);
                Villager v = one.World.Villagers[0];
                Household? h = one.World.FindHousehold(v.HouseholdId);
                held.Add($"s{s + 1}:[" + string.Join(",", Enum.GetValues<ContentmentCause>().Where(c => (v.ContentmentCauses & (1 << (int)c)) != 0)) + $"] u{v.SeasonsUnsettled} roof {h?.HasHome} alive {v.Alive}");
            }

            _output.WriteLine("phase 0: " + string.Join(" ", held));
        }

        foreach (int seed in rows.Keys.OrderBy(k => k))
        {
            _output.WriteLine(rows[seed]);
        }
    }
}
