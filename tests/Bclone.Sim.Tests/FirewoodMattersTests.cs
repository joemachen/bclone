using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐⭐ Firewood is a factor: it is only made when the village wants it, and a hearth with nothing
/// left to burn is a cold house (D407).
/// </summary>
/// <remarks>
/// Joe, 2026-09-26: *"I can skip 10 years without a woodcutter and still have lots of firewood. It
/// seems like a non-factor in the game."* Measured before a line was changed: a cold-start village
/// with two homes held **978 firewood after three years** (they burn 48 a year), and an established
/// one ran its stores dry by year six and then kept **both homes warm on 1–2 logs for four more
/// winters** — a burn takes three or nothing, and a larder with any firewood is a lit hearth.
/// </remarks>
public sealed class FirewoodMattersTests
{
    private readonly ITestOutputHelper _output;

    public FirewoodMattersTests(ITestOutputHelper output) => _output = output;

    private static int HeldAnywhere(SimWorld world)
    {
        int total = 0;
        foreach (Stockpile store in world.AllStores())
        {
            total += store[Goods.Firewood];
        }

        foreach (Villager villager in world.Villagers)
        {
            total += villager.Carried[Goods.Firewood];
        }

        foreach (GroundStack heap in world.GroundStacks)
        {
            if (heap.Goods == Goods.Firewood)
            {
                total += heap.Amount;
            }
        }

        return total;
    }

    /// <summary>
    /// ⛔ With nobody splitting, a village burns its firewood down to nothing — a log or two in a
    /// larder is not a fire that lasts for ever.
    /// </summary>
    /// <remarks>Red with a burn that takes three or nothing (the free fire).</remarks>
    [Fact]
    public void WithNoWoodcutterTheFirewoodRunsOut()
    {
        SimConfig config = ShippedConfig.Established();
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        loop.Step(config.TicksPerYear * 3);
        Assert.True(world.SetJobLimit(JobKind.Woodcutter, 0).Allowed);
        int split = world.LogsEverSplit;
        int start = HeldAnywhere(world);

        loop.Step(config.TicksPerYear * 10);

        _output.WriteLine($"ten years with no woodcutter: {start} firewood → {HeldAnywhere(world)}; logs split {world.LogsEverSplit - split}");
        Assert.Equal(split, world.LogsEverSplit);
        Assert.True(start > 0, "There was no firewood to burn down, so the guard proved nothing.");
        Assert.Equal(0, HeldAnywhere(world));
    }

    /// <summary>
    /// ⛔ With no limit set, nobody splits firewood the homes do not want: the stores never hold
    /// more than the village's derived want and one stint besides.
    /// </summary>
    /// <remarks>Red with a woodcutter who splits whenever there are logs (the unchecked dispatch).</remarks>
    [Fact]
    public void NobodySplitsFirewoodTheVillageDoesNotWant()
    {
        SimConfig config = ShippedConfig.Load();
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);

        int stint = config.SplitsPerStint * config.FirewoodPerSplit;
        int worst = 0;
        int worstBar = 0;
        for (int tick = 0; tick < config.TicksPerYear * 10; tick++)
        {
            loop.StepOnce();
            int homes = world.Households.Count(h => world.LivingMembersOf(h) > 0);
            int bar = (homes * (VillageEconomy.FirewoodStoreWantedPerHousehold(config) + VillageEconomy.FirewoodPerHouseholdPerWinter(config))) + stint;
            int held = world.FirewoodInWarehouses();
            if (held - bar > worst - worstBar)
            {
                worst = held;
                worstBar = bar;
            }

            Assert.True(
                held <= bar,
                $"Tick {world.Tick}: the stores hold {held} firewood for {homes} homes, whose want and one stint is {bar}. Somebody is splitting firewood nobody asked for.");
        }

        _output.WriteLine($"ten years: the stores came closest to their bar at {worst} against {worstBar}; logs split {world.LogsEverSplit}");
        Assert.True(world.LogsEverSplit > 0, "Nobody ever split anything, so the guard proved nothing.");
    }

    /// <summary>
    /// D395's guard, as the handoff wrote it: a limit of 400 and a woodcutter kept on — a year on,
    /// the stores are never past 400 and one stint.
    /// </summary>
    [Fact]
    public void AFirewoodLimitIsWhereTheSplittingStops()
    {
        SimConfig config = ShippedConfig.Load();
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);
        Assert.True(world.SetStockLimit(Goods.Firewood, 400).Allowed);

        int stint = config.SplitsPerStint * config.FirewoodPerSplit;
        int most = 0;
        for (int tick = 0; tick < config.TicksPerYear * 3; tick++)
        {
            loop.StepOnce();
            most = Math.Max(most, world.FirewoodInWarehouses());
            Assert.True(
                world.FirewoodInWarehouses() <= 400 + stint,
                $"Tick {world.Tick}: {world.FirewoodInWarehouses()} firewood against a limit of 400 and one stint of {stint}.");
        }

        _output.WriteLine($"three years at a limit of 400: the stores held at most {most}; logs split {world.LogsEverSplit}");
    }
}
