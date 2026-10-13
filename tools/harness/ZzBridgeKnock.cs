using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// ⛔ D576's MEASUREMENT HARNESS — kept in tools/harness/, COPIED into tests/Bclone.Sim.Tests/ WITH ZzBridges.cs (its
// seed list) to run, and DELETED from there before any commit. Joe's D576 note: when does a village first hold
// 10 / 12 / 15 / 20 people now that bands knock, and was that first tick a knock tick (the bridge's stop and the
// nomads' stop raised together)? Release, ~50 s for the 43 villages.
// ZZK arm seed peak alive knocks | per mark: year(-1 never) and K if that tick was a knock.
public sealed class ZzBridgeKnock
{
    private readonly ITestOutputHelper _o;

    public ZzBridgeKnock(ITestOutputHelper o) => _o = o;

    [Theory]
    [MemberData(nameof(ZzBridges.Villages), MemberType = typeof(ZzBridges))]
    public void Knock(string arm, ulong seed)
    {
        SimConfig config = ShippedConfig.Load() with { Seed = seed };
        SimLoop loop = SimFactory.CreatePhase0(config, new Bclone.Sim.Logging.InMemoryLogSink());
        SimWorld world = loop.World;
        var limits = config.StartingStockLimits;
        for (int id = 0; id < world.GoodsCatalog.Count; id++)
        {
            if (limits.TryGetValue(world.GoodsCatalog[id].Name, out int limit)) { world.SetStockLimit((Goods)id, limit); }
        }

        ColdStartTests.PlayTheOpening(world);
        if (arm == "every")
        {
            HuntingTests.RaiseALodgeFor(world);
            FishingTests.RaiseAFishery(world);
            Workplace farm = FarmFixtures.RaiseAFarm(world);
            FarmFixtures.GiveItGround(world, farm, 3);
        }

        int[] marks = { 10, 12, 15, 20 };
        int[] first = marks.Select(_ => -1).ToArray();
        bool[] onKnock = new bool[marks.Length];
        int peak = 0;
        for (int t = 0; t < config.TicksPerYear * 50; t++)
        {
            if (t == config.TicksPerYear * 3)
            {
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 8);
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Warehouse, world.Map.FoundingSite, 8);
            }

            int knocksWere = world.BandsEverKnocked;
            loop.StepOnce();
            bool knocked = world.BandsEverKnocked != knocksWere;
            peak = Math.Max(peak, world.MostEverAlive);
            for (int m = 0; m < marks.Length; m++)
            {
                if (first[m] < 0 && world.MostEverAlive >= marks[m])
                {
                    first[m] = t / config.TicksPerYear;
                    onKnock[m] = knocked;
                }
            }
        }

        string Mark(int m) => $"at{marks[m]} {first[m]}{(onKnock[m] ? "K" : string.Empty)}";
        _o.WriteLine($"ZZK {arm} {seed} peak {peak} alive {world.Population} knocks {world.BandsEverKnocked} "
            + string.Join(" ", Enumerable.Range(0, marks.Length).Select(Mark)));
    }
}
