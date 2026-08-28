using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐⭐ The farm's autumn, attributed tick by tick and armful by armful — <b>the ledger that
/// unparked the farm</b> (`per-site-yield.md §4.2a`, D194).
/// </summary>
/// <remarks>
/// <para>
/// <b>⛔ FOUR ATTEMPTS AT THIS BUG PROPOSED FOUR CAUSES AND MEASUREMENT KILLED EVERY ONE</b> —
/// the granary haul, the daily commute, farmhands getting cold, and <c>farm_store_cap</c>. The
/// fifth attempt was told, in the handoff, to stop hypothesising and build a ledger instead.
/// <b>This is that ledger, kept rather than thrown away</b>, so the numbers in §4.2a can be
/// re-taken by anybody who doubts them instead of being trusted because they are written down.
/// </para>
/// <para>
/// <b>⚠️ IT IS A MEASUREMENT, AND ITS ASSERTIONS ARE DELIBERATELY LOOSE.</b> Each one guards a
/// *claim the spec makes* rather than a number, because the numbers move whenever the farm gets
/// better and a ledger that fails for an improvement is a ledger nobody will keep.
/// </para>
/// </remarks>
public sealed class FarmLedgerTests
{
    private readonly ITestOutputHelper _output;

    public FarmLedgerTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => ShippedConfig.Established();

    /// <summary>
    /// ⭐⭐ Where a distant farm's autumn actually goes, by state and by armful.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The finding this exists to hold: the harvest walks to the granary, not to the
    /// steading.</b> <c>VillageEconomy.FieldTileTicks</c> charges *"a round trip to the
    /// steading"* — four ticks — for every armful, and the ledger says <b>the farm's own buffer
    /// takes barely one load in six.</b> <c>farm_store_cap</c> is 100 and a tile of average
    /// ground yields 67, so the buffer holds <b>one and a half armfuls</b>: it is full after the
    /// first tile and every tile after that makes the long walk.
    /// </para>
    /// <para>
    /// <b>⛔ AND THAT IS WHY THE BUFFER IS NOT THE LEVER, MEASURED TWICE NOW.</b> Given an 8.7×
    /// buffer it still took only 23 loads of 72, because it fills once and the market cannot
    /// keep it drained — so the ceiling moved from 6 tiles to 6 at ten ticks out. <i>Do not
    /// propose <c>farm_store_cap</c> a third time.</i>
    /// </para>
    /// </remarks>
    [Fact]
    public void ADistantFarmsHarvestWalksToTheGranaryAndNotToItsOwnBuffer()
    {
        var sink = new InMemoryLogSink();
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, sink);
        SimWorld world = loop.World;

        Workplace farm = FarmTestGround.SiteAFarm(world, walkAway: 10, out int walk);
        FarmFixtures.GiveItGround(world, farm, reach: 3);

        for (int i = 0; i < config.TicksPerYear * 10; i++)
        {
            loop.StepOnce();
        }

        (int toBuffer, int onward, IReadOnlyList<int> onwardWalks) = ReadTheHaulLedger(sink);
        int hauls = toBuffer + onward;

        _output.WriteLine(
            $"{walk} ticks out, ten years: {hauls} armfuls — {toBuffer} into the farm's own "
            + $"buffer, {onward} carried on to a store");
        _output.WriteLine(
            $"farm_store_cap {config.FarmStoreCap}, a tile of average ground yields "
            + $"{config.CropYieldPerTile} — the buffer holds "
            + $"{config.FarmStoreCap / config.CropYieldPerTile} armful(s)");
        _output.WriteLine(
            "mean walk of the ones carried on: "
            + $"{(onwardWalks.Count == 0 ? 0 : onwardWalks.Sum() / onwardWalks.Count)} ticks");

        Assert.True(hauls > 10, $"Only {hauls} armfuls were ever carried, so this measures nothing.");
        Assert.True(
            onward > toBuffer,
            $"{toBuffer} of {hauls} armfuls went into the farm's own buffer. The derivation "
            + "charges a round trip to the steading for every one of them, and this ledger is "
            + "the evidence that it does not happen — if the buffer is genuinely taking the "
            + "harvest now, `FieldTileTicks` is right after all and §4.2a needs re-reading.");
    }

    /// <summary>
    /// ⭐⭐ A distant farm works its autumn instead of standing in a field it was told was too big.
    /// </summary>
    /// <remarks>
    /// <b>This is the whole bug, in one number.</b> Before D194 a farm ten ticks from its store
    /// spent <b>27% of every autumn resting</b> while reaping five tiles of the thirteen the
    /// derivation gives — the cap had cut its field, and the idleness then read back as proof
    /// that the field had been too big. <i>The cap was self-fulfilling</i>, and four sessions
    /// looked for a physical cause that was not there.
    /// </remarks>
    [Theory]
    [InlineData(10, 27)]
    [InlineData(16, 45)]
    [InlineData(22, 55)]
    public void AndItIsNotIdleThroughIt(int walkAway, int idleBefore)
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Workplace farm = FarmTestGround.SiteAFarm(world, walkAway, out int walk);
        FarmFixtures.GiveItGround(world, farm, reach: 3);

        var census = new Dictionary<VillagerState, int>();
        int handTicks = 0;
        int reaped = 0;

        for (int i = 0; i < config.TicksPerYear * 12; i++)
        {
            loop.StepOnce();
            bool fall = world.Clock.Season == Season.Fall;

            foreach (Villager villager in world.Villagers)
            {
                if (!villager.Alive || villager.WorkplaceId != farm.Id)
                {
                    continue;
                }

                if (fall)
                {
                    handTicks++;
                    census.TryGetValue(villager.State, out int had);
                    census[villager.State] = had + 1;
                }

                if (villager.State == VillagerState.Reaping && villager.ActionTicksRemaining == 1)
                {
                    reaped++;
                }
            }
        }

        census.TryGetValue(VillagerState.Resting, out int resting);
        census.TryGetValue(VillagerState.Idle, out int idling);
        int idle = handTicks == 0 ? 0 : (resting + idling) * 100 / handTicks;

        _output.WriteLine($"=== {walk} ticks from the nearest store, twelve years ===");
        _output.WriteLine(
            $"{reaped} tiles reaped; the farm has learned it can bring in "
            + $"{farm.FieldTilesLearned} a hand");
        _output.WriteLine($"autumn was {idle}% idle; it was {idleBefore}% before D194:");

        foreach (KeyValuePair<VillagerState, int> pair in census.OrderByDescending(p => p.Value))
        {
            _output.WriteLine($"    {pair.Key,-20} {pair.Value,6}  {pair.Value * 100 / handTicks,3}%");
        }

        Assert.True(handTicks > 0, "Nobody ever held the farm, so this measures nothing.");
        Assert.True(
            idle < idleBefore / 2,
            $"A farm {walk} ticks out still spends {idle}% of its autumns idle against the {idleBefore}% "
            + "measured before this slice. That idleness is a cap cutting a field the farmer then "
            + "has time to spare on, and a cap that proves itself right is what D194 deleted.");
    }

    /// <summary>
    /// ⛔ What the farm can <em>never</em> do, so nobody goes looking for it again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe's complaint that opened all this: *"a farmer plants 5 tiles of the 13."*</b> The
    /// arithmetic says thirteen is not available at that distance and never was — autumn is
    /// <c>days_per_season × ticks_per_day</c> ticks, and thirteen tiles each costing a reap plus
    /// a round trip to a store ten ticks away needs about twice that. <b>The farm was short of
    /// one or two tiles, not eight.</b>
    /// </para>
    /// <para>
    /// <b>⭐ So the lever is the walk, and this guard's second half is the one that matters:</b>
    /// the same farm, given a store beside its fields, commits the full derived field. That is
    /// what §4.3's placement warning tells the player at the moment they can still act on it.
    /// </para>
    /// </remarks>
    [Fact]
    public void ThirteenTilesTenTicksFromAStoreIsNotSomethingTheGroundCanGive()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        int autumn = config.DaysPerSeason * config.TicksPerDay;
        int derived = VillageEconomy.FieldTilesOneFarmerKeeps(config);

        Workplace farm = FarmTestGround.SiteAFarm(world, walkAway: 10, out int walk);
        FarmFixtures.GiveItGround(world, farm, reach: 3);

        int perTile = config.ReapTicks + (walk * 2);
        _output.WriteLine(
            $"autumn is {autumn} ticks; {derived} tiles {walk} ticks out cost about "
            + $"{derived * perTile} ({config.ReapTicks} to reap plus a {walk * 2}-tick round trip each)");

        Assert.True(
            derived * perTile > autumn,
            $"Thirteen tiles {walk} ticks out now fits inside one autumn. Either a duration or "
            + "the season changed, and §4.2a's central claim needs re-measuring before anybody "
            + "quotes it again.");

        // ⭐ AND THE LEVER THAT DOES WORK. A store beside the fields, and the same farm commits
        // the whole derived field.
        FarmTestGround.RaiseAGranaryBeside(world, farm);
        int commits = world.FieldTilesThisFarmCommitsPerHand(farm);

        _output.WriteLine($"with a store beside the fields it commits {commits} of {derived}");
        Assert.Equal(derived, commits);
    }

    /// <summary>
    /// Every armful, attributed to where it went, off <c>HaulTheHarvest</c>'s own reason line.
    /// </summary>
    private static (int ToBuffer, int Onward, IReadOnlyList<int> OnwardWalks) ReadTheHaulLedger(
        InMemoryLogSink sink)
    {
        int toBuffer = 0;
        int onward = 0;
        var onwardWalks = new List<int>();

        foreach (LogEntry entry in sink.Entries)
        {
            string line = entry.Message;
            if (!line.Contains("food from the field", StringComparison.Ordinal))
            {
                continue;
            }

            // "… (cost X), nearest store NAME (cost Y)."
            int cut = line.IndexOf("), nearest store ", StringComparison.Ordinal);
            if (cut < 0)
            {
                continue;
            }

            string farmHalf = line[..cut];
            string storeHalf = line[(cut + "), nearest store ".Length)..].TrimEnd('.');

            string farmCost = farmHalf[
                (farmHalf.LastIndexOf("(cost ", StringComparison.Ordinal) + "(cost ".Length)..];
            int bracket = storeHalf.LastIndexOf(" (cost ", StringComparison.Ordinal);
            string storeName = storeHalf[..bracket];
            string storeCost = storeHalf[(bracket + " (cost ".Length)..].TrimEnd(')');

            bool farmHasRoom = farmCost != "no room";
            bool storeReachable = storeName != "none" && storeCost != "unreachable";

            if (farmHasRoom && (!storeReachable || int.Parse(farmCost) <= int.Parse(storeCost)))
            {
                toBuffer++;
            }
            else if (storeReachable)
            {
                onward++;
                onwardWalks.Add(int.Parse(storeCost) / TravelCostField.BaseTileCost);
            }
        }

        return (toBuffer, onward, onwardWalks);
    }
}
