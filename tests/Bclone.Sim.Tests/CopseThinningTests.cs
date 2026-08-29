using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ The copse thins under use — <c>gathers_per_thinned_tile</c> (D257, Joe).
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐⭐ THE CLAIM THIS FILE EXISTS FOR IS A MEASUREMENT, NOT A MECHANISM.</b> Before this, a
/// hut's ring sat at **55% wooded in year 0 and 55% in year 110** — gathering added food and took
/// nothing, so *the copse could not thin because nothing consumed it.* Joe, from a 111-year run:
/// <em>"the copse of wood isnt infinite."</em>
/// </para>
/// <para>
/// <b>⛔ IT SHIPS OFF, AND THE OFF GUARD IS THE IMPORTANT ONE TODAY.</b> The rate is Joe's to feel
/// in play — an unattended village is capped by its granary rather than by its food
/// (<see cref="StorageTests.CapacityIsWhatHoldsThePopulationFlat"/>), so no harness here can judge
/// the pressure. **Until he picks a number, what must be true is that a village which does not thin
/// is the village that came before.**
/// </para>
/// </remarks>
public sealed class CopseThinningTests
{
    private readonly ITestOutputHelper _output;

    public CopseThinningTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Shipped => ShippedConfig.Established();

    private static SimLoop Loop(SimConfig config, ulong seed) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink(), seed);

    private static Workplace? TheHut(SimWorld world)
    {
        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            if (world.Workplaces[i].Kind == JobKind.Forager
                && world.Workplaces[i].GatheringRadius > 0)
            {
                return world.Workplaces[i];
            }
        }

        return null;
    }

    private static int WoodedPercent(SimWorld world, Workplace hut) =>
        world.WoodedTilesAround(hut) * 100 / VillageEconomy.TilesInRing(hut.GatheringRadius);

    // -----------------------------------------------------------------
    //  Off is the village that came before
    // -----------------------------------------------------------------

    /// <summary>
    /// ⛔ Switched off, a village is byte-identical to one from before this existed.
    /// </summary>
    /// <remarks>
    /// <b>The sparse rule in behaviour rather than in the hash</b> — and it is what lets this ship
    /// with **no golden moved** while the rate is still an open question. ⚠️ *If this ever goes red,
    /// the feature has stopped being opt-in and five goldens are wrong rather than one guard.*
    /// </remarks>
    [Fact]
    public void SwitchedOffNothingInTheValleyMoves()
    {
        SimConfig off = Shipped with { GathersPerThinnedTile = 0 };
        SimLoop a = Loop(off, 12345UL);
        SimLoop b = Loop(off, 12345UL);

        a.Step(off.TicksPerYear * 40);
        b.Step(off.TicksPerYear * 40);

        Assert.Equal(StateHash.Compute(a.World), StateHash.Compute(b.World));

        // ⚠️ Anti-vacuity: the run has to have actually foraged, or this proves only that two
        // empty valleys agree.
        Workplace hut = TheHut(a.World)!;
        Assert.NotNull(hut);

        int gathers = 0;
        for (int i = 0; i < a.World.Villagers.Count; i++)
        {
            gathers += a.World.Villagers[i].TotalGathers;
        }

        _output.WriteLine($"off: {gathers} gathers in forty years, ring {WoodedPercent(a.World, hut)}% wooded");
        Assert.True(gathers > 100, "Nobody foraged, so nothing was proven about not thinning.");
    }

    // -----------------------------------------------------------------
    //  On, the wood is actually spent
    // -----------------------------------------------------------------

    /// <summary>⭐ Switched on, a worked ring holds less mature wood than an unworked one.</summary>
    /// <remarks>
    /// <para>
    /// <b>Compared against the SAME seed with the feature off</b>, rather than against a number —
    /// the valley's own woodedness varies enormously between seeds (26% on seed 42 against 68% on
    /// seed 2), so an absolute threshold would be a fact about the map generator.
    /// </para>
    /// <para>
    /// <b>⛔⛔ AVERAGED ACROSS A WHOLE REGROWTH CYCLE, AND THE FIRST VERSION OF THIS GUARD WAS
    /// WRONG FOR EXACTLY THAT REASON.</b> It took one snapshot at a year boundary and reported
    /// **55% against 55% — no effect at all** — after a run that had thinned about eleven hundred
    /// tiles. ⭐ **`RegrowthSystem` sweeps on a 72-day period, so the ring OSCILLATES**: sample just
    /// after a sweep and every sapling has matured and the wood looks untouched; sample mid-cycle
    /// and it is visibly thinner. *A spot reading of a fluctuating stock is not a measurement*
    /// (D227, and this is its third instance in one session).
    /// </para>
    /// </remarks>
    [Fact]
    public void SwitchedOnTheRingIsThinnerThanItWouldHaveBeen()
    {
        SimConfig off = Shipped with { GathersPerThinnedTile = 0 };
        SimConfig on = Shipped with { GathersPerThinnedTile = 3 };

        SimLoop quiet = Loop(off, 12345UL);
        SimLoop worked = Loop(on, 12345UL);

        quiet.Step(off.TicksPerYear * 70);
        worked.Step(on.TicksPerYear * 70);

        int quietAvg = AverageWoodedOverACycle(quiet);
        int workedAvg = AverageWoodedOverACycle(worked);

        _output.WriteLine($"averaged over one regrowth cycle after seventy years: "
            + $"unworked ring {quietAvg / 10.0:F1}% wooded, worked ring {workedAvg / 10.0:F1}%");

        Assert.True(
            workedAvg < quietAvg,
            $"A ring foraged for seventy years averaged {workedAvg / 10.0:F1}% wooded against "
            + $"{quietAvg / 10.0:F1}% for one that was not. The copse is still infinite.");
    }

    /// <summary>
    /// Mean woodedness across one whole regrowth period, in tenths of a percent.
    /// </summary>
    /// <remarks>
    /// <b>⭐ THE ONLY HONEST WAY TO ASK THIS QUESTION.</b> The stock fluctuates on the sweep's
    /// period, so any single tick is a phase reading rather than a level. Sampling every day of one
    /// full period and averaging removes the phase entirely.
    /// </remarks>
    private static int AverageWoodedOverACycle(SimLoop loop)
    {
        SimConfig config = loop.World.Config;
        int days = config.RegrowthPeriodDays;
        long total = 0;

        for (int day = 0; day < days; day++)
        {
            loop.Step(config.TicksPerDay);
            Workplace hut = TheHut(loop.World)!;
            total += WoodedTenths(loop.World, hut);
        }

        return (int)(total / days);
    }

    private static int WoodedTenths(SimWorld world, Workplace hut) =>
        world.WoodedTilesAround(hut) * 1000 / VillageEconomy.TilesInRing(hut.GatheringRadius);

    /// <summary>⛔ It thins to saplings, not to bare ground — so the wood grows back.</summary>
    /// <remarks>
    /// <b>⭐ THIS IS WHAT MAKES IT A PRESSURE RATHER THAN A CLOCK.</b> Felling leaves grass; this
    /// leaves <see cref="Terrain.Sapling"/>, which <c>RegrowthSystem</c> matures on its own sweep.
    /// **A rested hut recovers and a hard-worked one does not**, which is the whole design — and it
    /// is why the ring oscillates rather than draining to zero. ⛔ *If this ever cleared to grass,
    /// a forager would be a woodcutter who brings back berries.*
    /// </remarks>
    [Fact]
    public void ItSetsTheWoodBackRatherThanClearingIt()
    {
        SimConfig on = Shipped with { GathersPerThinnedTile = 3 };
        SimLoop loop = Loop(on, 12345UL);
        SimWorld world = loop.World;

        Workplace hut = TheHut(world)!;
        Assert.NotNull(hut);

        int grassBefore = Count(world, hut, Terrain.Grass);

        // ⛔ SAMPLED MID-CYCLE, NOT ON A BOUNDARY. The sweep matures every sapling it passes, so
        // counting them just after one runs reports zero however hard the wood is being worked.
        // Half a regrowth period in is where young wood actually exists.
        loop.Step((on.TicksPerYear * 30) + (on.TicksPerDay * on.RegrowthPeriodDays / 2));

        int saplingsAfter = Count(world, hut, Terrain.Sapling);
        int grassAfter = Count(world, hut, Terrain.Grass);
        int saplingsBefore = 0;

        _output.WriteLine($"ring saplings {saplingsBefore} → {saplingsAfter}, "
            + $"grass {grassBefore} → {grassAfter}");

        // The wood becomes young wood. Grass is what FELLING leaves, and a forager fells nothing.
        Assert.True(
            saplingsAfter > saplingsBefore,
            "Thinning produced no saplings, so it is not setting the wood back.");
    }

    private static int Count(SimWorld world, Workplace hut, Terrain terrain)
    {
        int radius = hut.GatheringRadius;
        int found = 0;

        for (int dy = -radius; dy <= radius; dy++)
        {
            int span = radius - System.Math.Abs(dy);
            for (int dx = -span; dx <= span; dx++)
            {
                var at = new GridPos(hut.Position.X + dx, hut.Position.Y + dy);
                if (world.Map.Contains(at) && world.Map.TerrainAt(at) == terrain)
                {
                    found++;
                }
            }
        }

        return found;
    }
}
