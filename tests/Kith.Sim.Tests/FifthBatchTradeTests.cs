using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.Logging;
using Kith.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

/// <summary>
/// `specs/batch-trade.md §6` step 6 (W1, D794) — <b>the proof that a batch trade is now a row:</b> a fifth one, added
/// through config alone, with no line of sim code that knows it — walked to, worked, delivered, described, its skill grown,
/// wanted by the quota.
/// </summary>
/// <remarks>
/// ⚠️ <b>Hired by hand, and that is the finding, not a shortcut.</b> The allocator seats only the trades in
/// <c>LabourAllocator.KindsInOrder</c>, a list of the shipped enum values — <b>no modded trade is ever seated by the village,
/// batch or not</b> (the boatman of <see cref="ModdedBuildingTests"/> is not either). So a modder's batch trade is a row
/// everywhere in the batch path and still needs its place in that list; W3's brewer, a shipped trade, takes a
/// <c>JobKind</c> value and that one entry. Recorded in the spec's §6 rather than fixed here: it is every modded trade's,
/// and Phase 7's (moving the rows into the data file) is where the list stops being code.
/// </remarks>
public sealed class FifthBatchTradeTests
{
    private readonly ITestOutputHelper _output;

    public FifthBatchTradeTests(ITestOutputHelper output) => _output = output;

    /// <summary>The boathouse's modded trade (<see cref="ModdedBuildingTests"/>), made a batch trade: stone into tools.</summary>
    private static JobKind Boatman => (JobKind)18;

    private static BuildingKind Boathouse => (BuildingKind)10;

    private static readonly BatchRow Shaping = new()
    {
        Trade = Boatman, Input = Goods.Stone, InputPerBatch = 2, Output = Goods.Tools, OutputPerBatch = 1,
        Ticks = 6, PerStint = 2, Verb = "shape", OneOf = "a {product}", WalkTo = "the boathouse",
        Working = "shaping {products}", StintWord = "tool", FirstBatch = "splitting a stone for a {product}",
        Stop = BatchStop.None, Hands = BatchHands.EverySeatWithWork, QuotaBand = QuotaBand.AfterStone,
    };

    /// <summary>The modded valley, its boatman's batch row, and a skill the batch grows — config, and nothing else.</summary>
    private static SimConfig WithAFifthBatchTrade()
    {
        SimConfig modded = ModdedBuildingTests.ConfigWithABoathouse();
        int skillId = modded.Skills.Max(s => s.Id) + 1;
        SimConfig config = modded with
        {
            BatchTrades = modded.BatchRows.Append(Shaping).ToArray(),
            Skills = modded.Skills.Append(new SkillRow { Id = skillId, Name = "stonecraft", GrownBy = Boatman }).ToArray(),
        };
        config.Validate();
        return config;
    }

    [Fact]
    public void AFifthBatchTradeWorksFromItsRowAlone()
    {
        SimConfig config = WithAFifthBatchTrade();
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        // The recipe, read off the row.
        Assert.Equal(
            new Batch(Boatman, Goods.Stone, 2, Goods.Tools, 1, 0, 6, 2, "shape", "a stone tool"),
            world.BatchFor(Boatman));

        // Raised, stocked, and the village kept from needing its hands elsewhere.
        GridPos site = ModdedBuildingTests.SomewhereBuildable(world);
        Assert.True(world.Mark(Boathouse, site).Allowed);
        ModdedBuildingTests.FinishTheSiteAt(world, site);
        Workplace boathouse = world.Workplaces.Single(w => w.Tile == site && !w.IsSite);
        ToolsTests.KeepTheVillageWarmAndFed(world);
        // Every shipped trade at nought, and no stock limit set: the boatman's row is limited by produce, and a met limit
        // would send him home for a reason that has nothing to do with the batch.
        foreach (JobKind kind in JobLimits.Kinds)
        {
            world.SetJobLimit(kind, 0);
        }
        StoreBuilding stones = world.StoreBuildings.First(s => s.Kind != StoreKind.Market && s.Accepts(Goods.Stone) && s.Tile != site);
        stones.Store.Add(Goods.Stone, 40);
        Assert.Null(world.WhyTheBatchWaits(boathouse));

        // ⭐ Wanted by the quota from the row: every seat with work, taken in the stone band.
        Assert.Equal(boathouse.Capacity, LabourQuota.BatchHandsWanted(world, Shaping));

        // Hired by hand (see the remarks): the allocator never seats a modded trade.
        // And the share-out switched off (the player's own switch, D20), or its first pass clears every seat, this one too.
        world.VillageSharesOutWork = false;
        Villager hand = world.Villagers.First(v => v.Alive && v.CanTakeAJob);
        foreach (Workplace other in world.Workplaces)
        {
            other.WorkerIds.Remove(hand.Id);
        }

        boathouse.WorkerIds.Add(hand.Id);
        hand.WorkplaceId = boathouse.Id;
        int toolsBefore = world.InStores(Goods.Tools) + world.OnTheGround(Goods.Tools);
        int stoneBefore = world.InStores(Goods.Stone);

        bool walked = false;
        bool worked = false;
        string? atWork = null;
        for (int tick = 0; tick < config.TicksPerSeason && !(walked && worked && atWork is not null); tick++)
        {
            loop.StepOnce();
            if (hand.BatchTrade == Boatman)
            {
                walked |= hand.State == VillagerState.TravelingToABatch;
                worked |= hand.State == VillagerState.WorkingABatch;
                if (hand.State == VillagerState.WorkingABatch && hand.BatchesThisStint > 0)
                {
                    atWork = hand.DescribeState(boathouse.Name);
                }
            }
        }

        for (int tick = 0; tick < config.TicksPerSeason; tick++)
        {
            loop.StepOnce();
        }

        int made = world.InStores(Goods.Tools) + world.OnTheGround(Goods.Tools) - toolsBefore;
        SkillRow stonecraft = config.Skills.Single(s => s.GrownBy == Boatman);
        int grown = hand.Skills.Where(p => p.SkillId == stonecraft.Id).Sum(p => p.Ticks);
        _output.WriteLine($"walked {walked}, worked {worked}, \"{atWork}\"; {made} tools from {stoneBefore - world.InStores(Goods.Stone)} stone; stonecraft {grown}");

        Assert.True(walked, "Nobody walked to the fifth trade's hut.");
        Assert.True(worked, "Nobody worked a batch there.");
        Assert.True(made > 0, "The fifth trade made nothing.");
        Assert.Equal("shaping stone tools, the 2nd tool of the day", atWork);
        Assert.True(grown > 0, "The fifth trade's skill did not grow.");
    }
}
