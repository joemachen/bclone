using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.Systems;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Cold is a place you are standing — <c>specs/shelter-and-exposure.md</c> (D45).
/// </summary>
/// <remarks>
/// The system this replaces asked one question of everyone, everywhere, identically:
/// <em>does this villager's household have firewood?</em> So a villager froze because of
/// a number attached to their relatives. These tests are mostly about proving the new
/// answer depends on <b>where somebody is</b>, which is the whole of the decision.
/// </remarks>
public sealed class ShelterAndExposureTests
{
    private readonly ITestOutputHelper _output;

    public ShelterAndExposureTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static SimLoop Build(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink());

    /// <summary>A world stepped to the first winter, so the hearth system is live.</summary>
    private static SimWorld InWinter(SimConfig config)
    {
        SimLoop loop = Build(config);
        while (loop.World.Clock.Season != Season.Winter)
        {
            loop.StepOnce();
        }

        return loop.World;
    }

    /// <summary>
    /// Hold everyone still and let only the cold happen, for a stated number of ticks.
    /// </summary>
    /// <remarks>
    /// The hearth system is driven directly rather than through the loop, because the
    /// question here is what a <em>position</em> costs — and a villager left to
    /// <see cref="BehaviorSystem"/> would walk off the tile being measured. Firewood is
    /// topped back up each tick so the fire under test does not go out mid-measurement.
    /// </remarks>
    private static void Chill(SimWorld world, int ticks)
    {
        var hearth = new HearthSystem();
        for (int i = 0; i < ticks; i++)
        {
            foreach (Household household in world.Households)
            {
                household.Stockpile.Add(Goods.Firewood, 10);
            }

            hearth.Execute(world);
        }
    }

    // ---------------------------------------------------------------
    //  It is about the place, not the household
    // ---------------------------------------------------------------

    [Fact]
    public void TwoPeopleOfOneHouseholdGetColdAtDifferentRates()
    {
        // THE test for this slice. Same family, same shelf, same tick — and one of them
        // is out in the snow. Under the shipped model these two were identical by
        // construction, because the only thing the model read was the household.
        SimConfig config = Config;
        SimWorld world = InWinter(config);

        Household home = world.Households[0];
        Villager inside = world.FindVillager(home.MemberIds[0])!;
        Villager outside = world.FindVillager(home.MemberIds[1])!;

        inside.Position = home.Home();
        outside.Position = FarFromAnyBuilding(world);
        inside.Cold = 0;
        outside.Cold = 0;

        Chill(world, 20);

        _output.WriteLine($"indoors by the fire {inside.Cold}, out in the open {outside.Cold}.");

        Assert.Equal(0, inside.Cold);
        Assert.True(outside.Cold > 0,
            "Standing in the open through twenty ticks of winter cost nothing at all.");
    }

    [Fact]
    public void ARoofWithNoFireSlowsTheColdButDoesNotStopIt()
    {
        // The middle row of D45's table, and the reason it exists. A shed is not a
        // hearth: it buys time, which is a different thing from safety.
        SimConfig config = Config;
        SimWorld world = InWinter(config);

        Villager underARoof = world.Villagers[0];
        Villager inTheOpen = world.Villagers[1];

        underARoof.Position = world.AnyStoreOf(StoreKind.Shed).Position;
        inTheOpen.Position = FarFromAnyBuilding(world);
        underARoof.Cold = 0;
        inTheOpen.Cold = 0;

        Chill(world, 20);

        _output.WriteLine($"under a roof {underARoof.Cold}, in the open {inTheOpen.Cold}.");

        Assert.True(underARoof.Cold > 0, "A roof with no fire under it stopped the cold entirely.");
        Assert.True(underARoof.Cold < inTheOpen.Cold, "A roof was worth nothing against open ground.");
    }

    [Fact]
    public void AFireGivesItBackRatherThanWipingIt()
    {
        // Joe's answer (c), and the measurement behind it is in the spec §8b: a fire
        // that RESET the count killed nobody in a hundred and twenty years, because
        // villagers spend three quarters of winter standing at one. Thawing keeps the
        // sentence true — you never freeze at a burning fire — without letting one warm
        // minute erase a fortnight in the snow.
        SimConfig config = Config;
        SimWorld world = InWinter(config);

        Villager villager = world.Villagers[0];
        villager.Position = world.Households[0].Home();
        villager.Cold = config.ExposureThreshold;

        Chill(world, 1);
        int afterOneTick = villager.Cold;

        _output.WriteLine(
            $"from {config.ExposureThreshold} to {afterOneTick} after one tick at a fire.");

        Assert.True(afterOneTick > 0,
            "One tick beside a fire wiped the whole count, which is the model measured to kill nobody.");
        Assert.True(afterOneTick < config.ExposureThreshold, "A fire gave nothing back at all.");

        // And it does get all the way back to zero, given the time it took to earn.
        Chill(world, config.ExposureTicksOutdoors);
        Assert.Equal(0, villager.Cold);
    }

    [Fact]
    public void ANeighboursFireCountsToo()
    {
        // §4.3. The alternative encodes a cruelty the player cannot act on: two houses
        // side by side, one warm and one not, and the sim insisting you freeze in the
        // correct doorway.
        SimConfig config = Config;
        SimWorld world = InWinter(config);

        Household mine = world.Households[0];
        Household theirs = world.Households[1];
        Assert.NotEqual(mine.Id, theirs.Id);

        Villager villager = world.FindVillager(mine.MemberIds[0])!;
        villager.Position = theirs.Home();
        villager.Cold = config.ExposureThreshold / 2;

        int before = villager.Cold;
        Chill(world, 5);

        _output.WriteLine($"at a neighbour's fire: {before} then {villager.Cold}.");
        Assert.True(villager.Cold < before, "A neighbour's hearth did nothing for them.");
    }

    // ---------------------------------------------------------------
    //  The hole a counter-per-state would have left
    // ---------------------------------------------------------------

    [Fact]
    public void ExposureAddsUpAcrossTheTwoStates()
    {
        // The reason there is one accumulator and not two counters. With a counter per
        // row of D45's table, a villager who alternates — a spell outdoors, a spell in a
        // cold room, a spell outdoors — trips neither, and is immortal in conditions
        // that should kill them. Partial exposure has to add up or the model has a hole
        // in the middle of its ordinary case.
        SimConfig config = Config;
        SimWorld world = InWinter(config);

        Villager villager = world.Villagers[0];
        villager.Cold = 0;

        GridPos open = FarFromAnyBuilding(world);
        GridPos roof = world.AnyStoreOf(StoreKind.Shed).Position;

        // Deliberately under each individual threshold in each individual state.
        int spell = config.ExposureTicksOutdoors - 1;
        Assert.True(spell < config.ExposureTicksOutdoors);
        Assert.True(spell < config.ExposureTicksSheltered);

        villager.Position = open;
        Chill(world, spell / 2);
        villager.Position = roof;
        Chill(world, spell);
        villager.Position = open;
        Chill(world, spell / 2);

        _output.WriteLine(
            $"{spell / 2} out, {spell} under a roof, {spell / 2} out again: " +
            $"{villager.Cold} of {config.ExposureThreshold}.");

        Assert.True(villager.Cold >= config.ExposureThreshold,
            $"Alternating between the open and a cold room reached only {villager.Cold} of " +
            $"{config.ExposureThreshold}. Neither spell was fatal alone, and together they have " +
            "to be, or a villager can survive any winter by changing rooms.");
    }

    // ---------------------------------------------------------------
    //  It still kills, and only for a reason you can point at (D7, D17)
    // ---------------------------------------------------------------

    [Fact]
    public void ColdKillsWhenTheWoodpileFails()
    {
        // Anti-vacuity, and the whole of Joe's answer (d): this system fires when the
        // FUEL CHAIN fails, and that is the pressure the player answers. So the guard is
        // not "somebody froze once in three hundred years" — it is "break the chain in a
        // way a player could cause, and people freeze."
        //
        // ⚠️ IT USED TO BREAK THE CHAIN WITH `FirewoodPerSplit = 1` — a village whose
        // woodcutter cannot keep up however many hands it puts on the hut — and **that stopped
        // freezing anybody**, which is worth reading as a result rather than a broken test.
        // Joe asked for firewood to burn four times slower ("make firewood consumption take
        // longer, like 4x longer") and `firewood_burn_interval_days: 4` delivered it. A trickle
        // of one firewood per split is now enough to heat the village. The fuel chain got hard
        // to break by starving its yield.
        //
        // ⭐ SO IT IS BROKEN THE WAY A PLAYER ACTUALLY BREAKS IT, which this guard's own words
        // asked for all along — *"break the chain in a way a player could cause"*. A config
        // constant no player can reach was always the weaker version of that. A firewood limit
        // of zero is the strongest: the game warns and then obeys (D42, D62), the quota reads
        // the limit as met and staffs nobody, and no firewood is ever made again. It is the
        // one road to freezing that the player is genuinely driving down.
        SimConfig broken = Config;
        SimLoop loop = Build(broken);
        loop.World.SetStockLimit(Goods.Firewood, 0);
        loop.Step(broken.TicksPerYear * 100);

        int froze = 0;
        foreach (Villager villager in loop.World.Villagers)
        {
            if (!villager.Alive && villager.CauseOfDeath == CauseOfDeath.Cold)
            {
                froze++;
            }
        }

        _output.WriteLine($"{froze} froze in a village the player told to keep no firewood.");

        Assert.True(froze > 0,
            "Nobody froze in a village whose fuel chain cannot produce enough firewood to heat " +
            "it. CauseOfDeath.Cold has gone dormant, and D17 allowed a second death system on " +
            "the condition that it did not.");
    }

    [Fact]
    public void PeopleActuallyBreakOffWorkToGetWarm()
    {
        // Anti-vacuity (D7) for the behaviour half — and it is the ORDINARY village that
        // proves it, which was a surprise. Somebody is walking in to get warm for about
        // 2% of every winter even when the woodpile holds: a logger out at the stand, a
        // marketer on a long leg. That is the mechanic doing exactly what it is for, and
        // it is why it must be measured against a village that lives rather than one
        // that dies.
        //
        // Measured the wrong way round first: asserted against a village whose splitter
        // yields one firewood, where it reads zero — not because nobody is cold but
        // because the whole settlement is dead by its first spring and there is no fire
        // left anywhere to walk to. A guard that only fires in a corpse is not a guard.
        SimConfig config = Config;
        SimLoop loop = Build(config);

        int seekingTicks = 0;
        string? who = null;

        for (int i = 0; i < config.TicksPerYear * 60; i++)
        {
            loop.StepOnce();

            foreach (Villager villager in loop.World.Villagers)
            {
                if (villager.Alive && villager.State == VillagerState.SeekingShelter)
                {
                    seekingTicks++;
                    who ??= villager.Name;
                }
            }
        }

        _output.WriteLine($"{seekingTicks} ticks spent walking to a fire; {who} was the first.");

        Assert.True(seekingTicks > 0,
            "Nobody ever broke off work to get warm in sixty years. seek_shelter_percent is " +
            "describing a decision no villager ever makes.");
    }

    [Fact]
    public void NobodyEverFreezesStandingAtABurningFire()
    {
        // The invariant the epitaph would otherwise have to explain. MortalitySystem
        // prints "beside a burning fire, which should not be possible" for exactly this
        // case, and this is the test that keeps it unprinted.
        SimConfig broken = Config with { FirewoodPerSplit = 1 };
        SimLoop loop = Build(broken);

        for (int i = 0; i < broken.TicksPerYear * 100; i++)
        {
            loop.StepOnce();

            foreach (Villager villager in loop.World.Villagers)
            {
                if (!villager.Alive || villager.CauseOfDeath != CauseOfDeath.Cold
                    || villager.DiedAtTick != loop.World.Tick)
                {
                    continue;
                }

                Assert.NotEqual(Shelter.Fire, loop.World.ShelterAt(villager.Position));
            }
        }
    }

    // ---------------------------------------------------------------
    //  Switching it off, and what that world is
    // ---------------------------------------------------------------

    [Fact]
    public void OutdoorColdCanBeSwitchedOffAndThatIsTheWorldClothingMakes()
    {
        // The market_capacity: 0 pattern. Clothing removes the outdoor danger entirely
        // (D45), and it waits on D19/D39's production tier — so the world it creates has
        // to be reachable and testable now, or the slice after this one has nothing to
        // aim at.
        SimConfig clothed = Config with { ExposureDaysOutdoors = 0 };

        Assert.Equal(0, clothed.ExposureThreshold);

        SimLoop loop = Build(clothed);
        loop.Step(clothed.TicksPerYear * 100);

        foreach (Villager villager in loop.World.Villagers)
        {
            Assert.NotEqual(CauseOfDeath.Cold, villager.CauseOfDeath);
        }

        _output.WriteLine($"{loop.World.Population} alive at year 100 with cold switched off.");
        Assert.True(loop.World.Population > clothed.StartingPopulation);
    }

    [Fact]
    public void TheColdModelDoesNotTouchTheFoodOrFuelDerivation()
    {
        // D45 must not move a number in the derivation chain (spec §3). If it does,
        // something has been done wrong — the economy is derived from trips, mouths and
        // winters, and how somebody freezes is none of those.
        SimConfig warm = Config;
        SimConfig frozen = Config with { ExposureDaysOutdoors = 1, ExposureDaysSheltered = 1 };

        Assert.Equal(VillageEconomy.RequiredGatherYield(warm), VillageEconomy.RequiredGatherYield(frozen));
        Assert.Equal(
            VillageEconomy.RequiredStockpilePerAdult(warm),
            VillageEconomy.RequiredStockpilePerAdult(frozen));
        Assert.Equal(
            VillageEconomy.RequiredFirewoodPerSplit(warm),
            VillageEconomy.RequiredFirewoodPerSplit(frozen));
    }

    // ---------------------------------------------------------------
    //  Determinism
    // ---------------------------------------------------------------

    [Fact]
    public void TheHashCoversHowColdSomebodyIs()
    {
        // Anti-vacuity (D7) for the state hash. Cold is villager state and changes
        // outcomes, so two runs that differ by it are two different worlds.
        SimLoop loop = Build(Config);
        loop.Step(Config.TicksPerYear * 5);

        ulong before = StateHash.Compute(loop.World);
        loop.World.Villagers[0].Cold += 1;
        Assert.NotEqual(before, StateHash.Compute(loop.World));
    }

    [Fact]
    public void GettingColdIsDeterministic()
    {
        SimConfig config = Config with { FirewoodPerSplit = 1 };
        SimLoop a = Build(config);
        SimLoop b = Build(config);

        a.Step(config.TicksPerYear * 80);
        b.Step(config.TicksPerYear * 80);

        Assert.Equal(StateHash.Compute(a.World), StateHash.Compute(b.World));
    }

    /// <summary>A tile with no building on it, for measuring what open ground costs.</summary>
    private static GridPos FarFromAnyBuilding(SimWorld world)
    {
        for (int x = 0; x < world.Map.Width; x++)
        {
            for (int y = 0; y < world.Map.Height; y++)
            {
                var candidate = new GridPos(x, y);
                if (world.ShelterAt(candidate) == Shelter.Outdoors)
                {
                    return candidate;
                }
            }
        }

        throw new Xunit.Sdk.XunitException("The whole valley is under cover, which cannot be right.");
    }
}
