using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// ⛔ D420's MEASUREMENT HARNESS — kept in tools/harness/, COPIED into tests/Bclone.Sim.Tests/ to run
// (tools/harness/arms.sh does it) and DELETED from there before any commit. Never committed under tests/.
public sealed class ZzBase
{
    private readonly ITestOutputHelper _o;

    public ZzBase(ITestOutputHelper o) => _o = o;

    [Theory]
    [MemberData(nameof(Runs))]
    public void Measure(string arm, ulong seed)
    {
        SimConfig config = (arm is "shipped" or "wide" ? ShippedConfig.Load() : VillageFixtures.Village) with { Seed = seed };
        if (Environment.GetEnvironmentVariable("ZZ_ROW") is string zr)
        {
            string[] kv = zr.Split('=');
            NewGameSettings d = NewGame.Defaults(config, seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var vals = new Dictionary<string, string>(d.Values) { [kv[0]] = kv[1] };
            config = NewGame.Apply(config, d with { Values = vals }) with { Seed = seed };
        }

        if (Environment.GetEnvironmentVariable("ZZ_SPEED") is string sp) { config = config with { ToolSpeedBonusPercent = int.Parse(sp) }; }
        if (Environment.GetEnvironmentVariable("ZZ_YIELD") is string yi) { config = config with { ToolYieldBonusPercent = int.Parse(yi) }; }
        if (Environment.GetEnvironmentVariable("ZZ_USES") is string us) { config = config with { ToolUses = int.Parse(us) }; }
        if (Environment.GetEnvironmentVariable("ZZ_CART") is string ca) { config = config with { CartTools = int.Parse(ca) }; }
        if (Environment.GetEnvironmentVariable("ZZ_STONEX") is string sx)
        {
            int k = int.Parse(sx);
            config = config with
            {
                GranaryStone = config.GranaryStone * k / 10, WarehouseStone = config.WarehouseStone * k / 10,
                MarketStone = config.MarketStone * k / 10, HutStone = config.HutStone * k / 10,
                GathererHutStone = config.GathererHutStone * k / 10, ForesterHutStone = config.ForesterHutStone * k / 10,
                FarmhouseStone = config.FarmhouseStone * k / 10, FishingHutStone = config.FishingHutStone * k / 10,
                HunterLodgeStone = config.HunterLodgeStone * k / 10, SmithyStone = config.SmithyStone * k / 10,
                WellStone = config.WellStone * k / 10, LibraryStone = config.LibraryStone * k / 10,
                TownHallStone = config.TownHallStone * k / 10,
            };
        }

        // D445 (Joe): the unattended village paints as many rock tiles as the granary and the
        // warehouse it marks at year 3 cost, from their recipes. ZZ_PAINT overrides (0 = the old arm).
        int stoneTheyCost = BuildingRecipe.For(BuildingKind.Granary, config).Of(Goods.Stone)
            + BuildingRecipe.For(BuildingKind.Warehouse, config).Of(Goods.Stone);
        int aTile = new GoodsCatalog(config.GoodsCatalog).YieldPerTileOf(Goods.Stone);
        int extraRock = Environment.GetEnvironmentVariable("ZZ_PAINT") is string zp
            ? int.Parse(zp)
            : (stoneTheyCost + aTile - 1) / aTile;
        SimLoop loop = SimFactory.CreatePhase0(config, new Bclone.Sim.Logging.InMemoryLogSink());
        SimWorld world = loop.World;
        var limits = ShippedConfig.Load().StartingStockLimits;
        for (int id = 0; id < world.GoodsCatalog.Count; id++)
        {
            if (limits.TryGetValue(world.GoodsCatalog[id].Name, out int limit))
            {
                world.SetStockLimit((Goods)id, limit);
            }
        }

        ColdStartTests.PlayTheOpening(world);
        if (arm == "every")
        {
            HuntingTests.RaiseALodgeFor(world);
            FishingTests.RaiseAFishery(world);
            Workplace farm = FarmFixtures.RaiseAFarm(world);
            FarmFixtures.GiveItGround(world, farm, 3);
        }

        int peak = 0;
        for (int t = 0; t < config.TicksPerYear * 50; t++)
        {
            if (t == config.TicksPerYear * 3)
            {
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 8);
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Warehouse, world.Map.FoundingSite, 8);
                if (extraRock > 0) { SeamFixtures.PaintNearest(world, Terrain.Rock, extraRock); }
            }

            // D446 arms: a smithy raised at year 5 beside an iron seam painted for clearing, set to
            // ZZ_SMITHY (stone / iron); none by default.
            if (t == config.TicksPerYear * 5 && Environment.GetEnvironmentVariable("ZZ_SMITHY") is string kind)
            {
                SeamFixtures.PaintNearest(world, Terrain.IronDeposit, int.Parse(Environment.GetEnvironmentVariable("ZZ_IRON") ?? "16"));
                Workplace smithy = ToolsTests.RaiseASmithy(world);
                if (kind == "iron") { world.SetForgeGood(smithy, Goods.IronTools); }
            }

            loop.StepOnce();
            peak = Math.Max(peak, world.Population);
        }

        int starved = world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Starvation);
        int cold = world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Cold);
        int sites = world.Workplaces.Count(w => w.Construction is { IsFinished: false });
        int iron = world.Villagers.Count(v => v.Alive && v.ToolUses > 0 && v.ToolGood == Goods.IronTools);
        int stone = world.Villagers.Count(v => v.Alive && v.ToolUses > 0 && v.ToolGood == Goods.Tools);
        _o.WriteLine($"ZZB {arm} {seed} sites {sites} alive {world.Population} peak {peak} starved {starved} cold {cold} hash {StateHash.Compute(world)} forged {world.ToolsEverForged} hands-iron {iron} hands-stone {stone} taken {world.ToolsEverTaken}");
    }

    public static IEnumerable<object[]> Runs()
    {
        if (Environment.GetEnvironmentVariable("ZZ_FIX") == "1")
        {
            foreach (ulong s in Enumerable.Range(400, 50).Select(x => (ulong)x)) { yield return new object[] { "fixture", s }; }
            yield break;
        }

        if (Environment.GetEnvironmentVariable("ZZ_WIDE") == "2")
        {
            foreach (ulong s in Enumerable.Range(300, 100).Select(x => (ulong)x)) { yield return new object[] { "wide", s }; }
            yield break;
        }

        if (Environment.GetEnvironmentVariable("ZZ_WIDE") == "1")
        {
            foreach (ulong s in Enumerable.Range(200, 100).Select(x => (ulong)x)) { yield return new object[] { "wide", s }; }
            yield break;
        }

        foreach (ulong s in new ulong[] { 12345, 1, 2, 3, 7, 11 }) { yield return new object[] { "shipped", s }; }
        foreach (ulong s in Enumerable.Range(100, 24).Select(x => (ulong)x)) { yield return new object[] { "shipped", s }; }
        foreach (ulong s in new ulong[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12345 }) { yield return new object[] { "fixture", s }; }
        foreach (ulong s in Enumerable.Range(1, 12).Select(x => (ulong)x).Append(12345UL)) { yield return new object[] { "every", s }; }
    }
}
