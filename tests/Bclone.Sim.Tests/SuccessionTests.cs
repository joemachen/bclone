using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ When a worker dies and a free hand takes the seat, the village log says who (Joe, 2026-09-26:
/// *"the village log should say … and X person took over their job at X"*).
/// </summary>
public sealed class SuccessionTests
{
    private readonly ITestOutputHelper _output;

    public SuccessionTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// A forager dies with a laborer free: within two ticks the log names both and the trade, at
    /// the Deaths category, and the heir named is a forager now, at the workplace the line names.
    /// </summary>
    /// <remarks>Red with <c>LabourAllocator.SayWhoTookOver</c> saying nothing.</remarks>
    [Fact]
    public void AnHeirToADeadWorkersSeatIsNamedInTheLog()
    {
        var sink = new InMemoryLogSink(LogLevel.Info);
        SimLoop loop = SimFactory.CreatePhase0(VillageFixtures.Village, sink);
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);
        loop.Step(VillageFixtures.Village.TicksPerYear * 2);

        // ⚠️ THE PREMISE, STATED: a fed fixture can want no work at all, so the shelves are emptied
        // — the village wants food, and the allocator seats foragers of its own accord — and the
        // player keeps foraging to three hands, so of the fixture's four adults one is a laborer
        // (all four forage otherwise, and nobody is free to inherit anything).
        Assert.True(world.SetJobLimit(JobKind.Forager, 3).Allowed);
        void Hungry()
        {
            foreach (StoreBuilding store in world.StoreBuildings)
            {
                foreach (Goods goods in world.GoodsCatalog.EdibleGoods)
                {
                    store.Store.TakeAll(goods);
                }
            }
        }

        Villager? worker = null;
        for (int t = 0; t < VillageFixtures.Village.TicksPerYear && worker is null; t++)
        {
            Hungry();
            loop.StepOnce();
            worker = world.Villagers.FirstOrDefault(v => v.Alive && v.HasJob
                && world.FindWorkplace(v.WorkplaceId) is { Kind: JobKind.Forager }
                && world.Villagers.Exists(o => o.Alive && o.CanWork && !o.HasJob));
        }

        Assert.True(worker is not null, "Nobody was ever seated as a forager with a free hand beside them, so there is no seat to inherit.");
        Hungry();

        int before = sink.Entries.Count;
        worker!.Alive = false;
        Hungry();
        loop.StepOnce();
        Hungry();
        loop.StepOnce();

        var said = new List<LogEntry>();
        for (int i = before; i < sink.Entries.Count; i++)
        {
            if (sink.Entries[i].Message.Contains("took over", StringComparison.Ordinal))
            {
                said.Add(sink.Entries[i]);
            }
        }

        LogEntry line = Assert.Single(said);
        _output.WriteLine(line.Message);
        Assert.Equal(LogCategory.Death, line.Category);
        Assert.Contains($"{worker.Name}'s work as a forager, at ", line.Message, StringComparison.Ordinal);

        // The heir named is a forager now, at the workplace the line names.
        Villager heir = world.Villagers.Single(v => v.Alive && line.Message.StartsWith(v.Name + " took over", StringComparison.Ordinal));
        Workplace now = world.FindWorkplace(heir.WorkplaceId)!;
        Assert.Equal(JobKind.Forager, now.Kind);
        Assert.EndsWith($"at {now.Name}.", line.Message, StringComparison.Ordinal);
    }
}
