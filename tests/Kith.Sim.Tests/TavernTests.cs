using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.Determinism;
using System.Text.Json.Nodes;
using Kith.Sim.Logging;
using Kith.Sim.Persistence;
using Kith.Sim.Systems;
using Kith.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

/// <summary>
/// W3 steps 3–4 (`specs/tavern.md §5.4`, §6, D834–D836): the tavern kept by a tavern keeper (Joe's call 3B) who carries its
/// ale in; the evening there, an errand offered where a rest at home begins; and the comfort it is for — an evening this
/// season eases one held cause.
/// </summary>
public sealed class TavernTests
{
    private readonly ITestOutputHelper _output;

    public TavernTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Config => VillageFixtures.Village;

    private static ulong SeasonTicks(SimConfig c) => (ulong)c.TicksPerDay * (ulong)c.DaysPerSeason;

    // -----------------------------------------------------------------
    //  The keeper (§5.4a)
    // -----------------------------------------------------------------

    /// <summary>
    /// ⭐ Hired by the village from shipped rows (no hand seated, no number typed) and carrying ale from the granary into the
    /// tavern, up to <c>tavern_stock</c> and no further. Red with the keeper out of <c>KindsInOrder</c>.
    /// </summary>
    [Fact]
    public void AKeeperIsHiredAndCarriesTheAleIn()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        Workplace tavern = ATavern(world);
        TheGranary(world).Store.Add(Goods.Ale, 60);
        Assert.True(LabourQuota.KeepersWanted(world) > 0, "No keeper wanted with ale in the warehouse.");

        bool carried = false;
        int most = 0;
        for (int tick = 0; tick < 2 * config.TicksPerSeason && !(carried && tavern.Store[Goods.Ale] > 0); tick++)
        {
            loop.StepOnce();
            carried |= world.Villagers.Any(v => v.State == VillagerState.CarryingAle && v.Carried[Goods.Ale] > 0);
            most = Math.Max(most, tavern.Store[Goods.Ale]);
        }

        _output.WriteLine($"keepers {tavern.WorkerIds.Count}, carried {carried}, the tavern holds {tavern.Store[Goods.Ale]} (most {most}); {world.WhyTheTavernIsShut(tavern) ?? "open"}");
        Assert.True(tavern.WorkerIds.Count > 0, "The village never hired a keeper.");
        Assert.True(carried, "Nobody was seen carrying ale to the tavern.");
        Assert.True(tavern.Store[Goods.Ale] > 0, "The tavern holds no ale.");
        Assert.True(most <= config.TavernStock, $"The keeper carried past tavern_stock ({most} > {config.TavernStock}).");
        Assert.Null(world.WhyTheTavernIsShut(tavern));
    }

    /// <summary>⛔ An unkept tavern is shut, says so, and nobody goes — ale in it or not (Joe's call 3B: the keeper keeps it open).</summary>
    [Fact]
    public void AnUnkeptTavernIsShutAndNobodyGoes()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        Workplace tavern = ATavern(world);
        tavern.Store.Add(Goods.Ale, 20);
        world.SetJobLimit(JobKind.TavernKeeper, 0);

        bool went = false;
        for (int tick = 0; tick < config.TicksPerSeason; tick++)
        {
            loop.StepOnce();
            went |= world.Villagers.Any(v => v.State is VillagerState.WalkingToTheTavern or VillagerState.AtTheTavern);
        }

        Assert.Empty(tavern.WorkerIds);
        Assert.False(went, "Somebody went to a tavern nobody keeps.");
        Assert.Contains("No keeper", world.WhyTheTavernIsShut(tavern), StringComparison.Ordinal);
    }

    /// <summary>A kept tavern with no ale is dry, and its sentence says where ale comes from.</summary>
    [Fact]
    public void ADryTavernSaysSo()
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        Workplace tavern = ATavern(world);
        Keep(world, tavern);
        Assert.Contains("dry", world.WhyTheTavernIsShut(tavern), StringComparison.Ordinal);
        Assert.Contains("brewery", world.WhyTheTavernIsShut(tavern), StringComparison.Ordinal);
        Assert.Equal(0, LabourQuota.KeepersWanted(world));
    }

    // -----------------------------------------------------------------
    //  The evening (§5.4b)
    // -----------------------------------------------------------------

    /// <summary>
    /// ⭐ An open tavern: adults within reach go of an evening, each drinks one ale, and comes home — and children never go, and
    /// nobody goes twice inside <c>tavern_every_days</c>. The ale drunk is the evenings counted, to the cup.
    /// </summary>
    [Fact]
    public void AdultsWithinReachSpendAnEveningThere()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        ToolsTests.KeepTheVillageWarmAndFed(world);
        Workplace tavern = ATavern(world);
        Keep(world, tavern);
        tavern.Store.Add(Goods.Ale, 200);
        int aleBefore = tavern.Store[Goods.Ale];

        var lastDay = new Dictionary<int, int>();
        int evenings = 0;
        bool aChildWent = false;
        bool tooSoon = false;
        for (int tick = 0; tick < config.TicksPerSeason - 2; tick++)
        {
            loop.StepOnce();
            foreach (Villager v in world.Villagers)
            {
                if (!v.CanWork && v.State is VillagerState.WalkingToTheTavern or VillagerState.AtTheTavern)
                {
                    aChildWent = true;
                }

                if (v.LastTavernDay > 0 && lastDay.GetValueOrDefault(v.Id) != v.LastTavernDay)
                {
                    if (lastDay.TryGetValue(v.Id, out int before) && v.LastTavernDay - before < config.TavernEveryDays)
                    {
                        tooSoon = true;
                    }

                    lastDay[v.Id] = v.LastTavernDay;
                    evenings++;
                    Assert.NotNull(world.TavernWithinReachOf(v));
                }
            }
        }

        _output.WriteLine($"{evenings} evenings by {lastDay.Count} adults; ale {aleBefore} → {tavern.Store[Goods.Ale]}; the card's count {tavern.EveningsThisSeason}");
        Assert.True(evenings > 0, "Nobody spent an evening at an open tavern beside their homes.");
        Assert.False(aChildWent, "A child went to the tavern.");
        Assert.False(tooSoon, "Somebody went again inside tavern_every_days.");
        Assert.Equal(evenings, aleBefore - tavern.Store[Goods.Ale]);
        Assert.Equal(evenings, tavern.EveningsThisSeason);
        Assert.Equal(evenings, world.Villagers.Sum(v => v.TavernEveningsThisSeason));
    }

    /// <summary>
    /// ⛔ <c>tavern_every_days</c> is a wait: with it past the run's length, nobody goes twice. ⚠️ Posed this way because the fixture's
    /// one drinker within reach has about three rests at home a season — the 5-day wait never bit there, and its red-check
    /// scored nought against <see cref="AdultsWithinReachSpendAnEveningThere"/> (D326's zero, written down).
    /// </summary>
    [Fact]
    public void NobodyGoesAgainInsideTheWait()
    {
        SimConfig config = Config with { TavernEveryDays = 1000 };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        ToolsTests.KeepTheVillageWarmAndFed(world);
        Workplace tavern = ATavern(world);
        Keep(world, tavern);
        tavern.Store.Add(Goods.Ale, 200);

        var evenings = new Dictionary<int, int>();
        var last = new Dictionary<int, int>();
        for (int tick = 0; tick < 2 * config.TicksPerSeason; tick++)
        {
            loop.StepOnce();
            foreach (Villager v in world.Villagers)
            {
                if (v.LastTavernDay > 0 && last.GetValueOrDefault(v.Id) != v.LastTavernDay)
                {
                    last[v.Id] = v.LastTavernDay;
                    evenings[v.Id] = evenings.GetValueOrDefault(v.Id) + 1;
                }
            }
        }

        _output.WriteLine($"evenings by person: {string.Join(", ", evenings.Values)}");
        Assert.NotEmpty(evenings);
        Assert.All(evenings.Values, n => Assert.Equal(1, n));
    }

    /// <summary>⛔ Beyond <c>tavern_reach_tiles</c> nobody goes — the walk, not a ring. Red with the reach unread.</summary>
    [Fact]
    public void NobodyGoesFromBeyondReach()
    {
        SimConfig config = Config with { TavernReachTiles = 1 };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        ToolsTests.KeepTheVillageWarmAndFed(world);
        Workplace tavern = ATavern(world);
        Keep(world, tavern);
        tavern.Store.Add(Goods.Ale, 200);
        Assert.All(world.Villagers.Where(v => v.CanWork), v => Assert.Null(world.TavernWithinReachOf(v)));

        loop.Step(config.TicksPerSeason - 2);
        Assert.All(world.Villagers, v => Assert.Equal(0, v.LastTavernDay));
        Assert.Equal(200, tavern.Store[Goods.Ale]);
    }

    /// <summary>
    /// ⛔ HANDOFF's D513 trap, both ways: no household's larder ends holding ale (the keeper's armful goes to the tavern, a
    /// broken-off one back to a store), and an evening grows no skill.
    /// </summary>
    [Fact]
    public void TheAleNeverEndsUpInALarderAndAnEveningIsNoTrade()
    {
        SimConfig config = Config;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        ToolsTests.KeepTheVillageWarmAndFed(world);
        ATavern(world);
        TheGranary(world).Store.Add(Goods.Ale, 100);
        loop.Step(2 * config.TicksPerSeason);

        Assert.All(world.Households, h => Assert.Equal(0, h.Stockpile[Goods.Ale]));
        Assert.False(SkillSystem.OutOnTheWork(VillagerState.AtTheTavern));
        Assert.False(SkillSystem.OutOnTheWork(VillagerState.WalkingToTheTavern));
    }

    // -----------------------------------------------------------------
    //  The comfort (§6)
    // -----------------------------------------------------------------

    /// <summary>⭐ Two causes and an evening at the tavern: content, and the card says the tavern made up for it.</summary>
    [Fact]
    public void AnEveningEasesOneCause()
    {
        SimWorld world = WorldAtTheTurnOf(1);
        (Villager adult, Household home) = AHousedAdult(world);
        adult.CommuteTiles = 30;
        home.HomePosition = null;
        adult.TavernEveningsThisSeason = 1;
        Read(world);

        Assert.False(adult.IsUnsettled);
        Assert.NotEqual(0, adult.ContentmentComforts);
        Assert.Equal(
            $"{adult.Name} is content — they have no roof of their own and the walk to work is a long one, but the evenings at "
            + "the tavern make up for it.",
            world.ContentmentNoteFor(adult));
        Assert.Equal(0, adult.TavernEveningsThisSeason);
    }

    /// <summary>Three causes: one eased is not enough, and the card says so.</summary>
    [Fact]
    public void ThreeCausesAreTooManyForOneEvening()
    {
        SimWorld world = WorldAtTheTurnOf(1);
        (Villager adult, Household home) = AHousedAdult(world);
        adult.CommuteTiles = 30;
        home.HomePosition = null;
        home.DryDaysThisSeason = 3;
        adult.TavernEveningsThisSeason = 1;
        Read(world);

        Assert.True(adult.IsUnsettled);
        Assert.EndsWith("; the evenings at the tavern were not enough.", world.ContentmentNoteFor(adult), StringComparison.Ordinal);
    }

    /// <summary>⛔ With nothing held a comfort does nothing — no credit banked, no bit set, no sentence.</summary>
    [Fact]
    public void AComfortWithNothingHeldDoesNothing()
    {
        SimWorld world = WorldAtTheTurnOf(1);
        (Villager adult, _) = AHousedAdult(world);
        adult.TavernEveningsThisSeason = 3;
        Read(world);
        Assert.Equal(0, adult.ContentmentComforts);
        Assert.Equal(0, adult.SeasonsUnsettled);
        Assert.Null(world.ContentmentNoteFor(adult));
    }

    /// <summary>⛔ No evening, no comfort: two causes unsettle exactly as in W2 — the mirror of the comfort above.</summary>
    [Fact]
    public void WithoutAnEveningTwoCausesUnsettleAsBefore()
    {
        SimWorld world = WorldAtTheTurnOf(1);
        (Villager adult, Household home) = AHousedAdult(world);
        adult.CommuteTiles = 30;
        home.HomePosition = null;
        Read(world);
        Assert.True(adult.IsUnsettled);
        Assert.Equal(0, adult.ContentmentComforts);
    }

    /// <summary>Every new villager field is hashed, sparsely, and survives a save.</summary>
    [Theory]
    [InlineData("last_tavern_day")]
    [InlineData("evenings")]
    [InlineData("comforts")]
    public void EveryTavernFieldIsHashedAndSaved(string field)
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        SimWorld world = loop.World;
        Villager adult = world.Villagers.First(v => v.CanWork);
        ulong before = StateHash.Compute(world);
        switch (field)
        {
            case "last_tavern_day": adult.LastTavernDay = 7; break;
            case "evenings": adult.TavernEveningsThisSeason = 2; break;
            case "comforts": adult.ContentmentComforts = 1; break;
            default: throw new ArgumentOutOfRangeException(nameof(field), field, "no such field");
        }

        Assert.NotEqual(before, StateHash.Compute(world));
        JsonObject document = SaveGame.Capture(loop, "test", "now", "test");
        SimWorld loaded = SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), world.Config).World;
        Villager back = loaded.Villagers.Single(v => v.Id == adult.Id);
        Assert.Equal(adult.LastTavernDay, back.LastTavernDay);
        Assert.Equal(adult.TavernEveningsThisSeason, back.TavernEveningsThisSeason);
        Assert.Equal(adult.ContentmentComforts, back.ContentmentComforts);
    }

    /// <summary>The shipped comfort is Joe's call 1A: an evening eases one cause; the validator refuses a comfort that eases nothing.</summary>
    [Fact]
    public void TheShippedComfortIsJoesAndTheValidatorRefusesNought()
    {
        SimConfig shipped = ShippedConfig.Load();
        ContentmentComfortRow row = Assert.Single(shipped.ContentmentComforts);
        Assert.Equal(ContentmentComfort.TavernEvening, row.Comfort);
        Assert.Equal(1, row.At);
        Assert.Equal(1, row.Eases);
        SimConfig broken = shipped with { ContentmentComforts = new[] { row with { Eases = 0 } } };
        Assert.Throws<SimConfigException>(() => broken.Validate());
    }

    // -----------------------------------------------------------------
    //  helpers
    // -----------------------------------------------------------------

    /// <summary>A tavern raised beside the founding, the village having found barley.</summary>
    private static Workplace ATavern(SimWorld world)
    {
        world.BarleyLearned = true;
        return FoodChainTests.RaiseA(world, BuildingKind.Tavern);
    }

    /// <summary>Seat a keeper by hand, the share-out switched off so the pass does not unseat them (the fifth trade's pose).</summary>
    private static void Keep(SimWorld world, Workplace tavern)
    {
        world.VillageSharesOutWork = false;
        Villager hand = world.Villagers.First(v => v.Alive && v.CanTakeAJob);
        foreach (Workplace other in world.Workplaces)
        {
            other.WorkerIds.Remove(hand.Id);
        }

        tavern.WorkerIds.Add(hand.Id);
        hand.WorkplaceId = tavern.Id;
    }

    private static StoreBuilding TheGranary(SimWorld world) =>
        world.StoreBuildings.First(s => s.Kind == StoreKind.Granary);

    private static SimWorld WorldAtTheTurnOf(int season)
    {
        SimWorld world = SimWorld.Create(Config);
        world.Tick = SeasonTicks(world.Config) * (ulong)season;
        return world;
    }

    private static (Villager Adult, Household Home) AHousedAdult(SimWorld world)
    {
        Villager adult = world.Villagers.First(v => v.CanWork);
        Household home = world.FindHousehold(adult.HouseholdId)!;
        home.HomePosition ??= adult.Position;
        foreach (Villager v in world.Villagers)
        {
            if (v.HouseholdId != home.Id && world.FindHousehold(v.HouseholdId) is Household other)
            {
                other.HomePosition ??= v.Position;
            }
        }

        return (adult, home);
    }

    private static void Read(SimWorld world) => new ContentmentSystem().Execute(world);
}
