using System.Text.Json.Nodes;
using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.Determinism;
using Kith.Sim.Logging;
using Kith.Sim.Persistence;
using Kith.Sim.Systems;
using Kith.Sim.World;
using Xunit;

namespace Kith.Sim.Tests;

/// <summary>
/// ⭐ W6, health — diet first (`specs/health.md`). Piece 1: food groups, what a person ate this season, the plain seasons that
/// wear a body down, and falling ill — never fatally.
/// </summary>
public sealed class HealthTests
{
    // ---------------------------------------------------------------
    //  §5.1 — food groups, a column on the good (step 1)
    // ---------------------------------------------------------------

    /// <summary>
    /// Every shipped food sits in its group (D357, D935): wheat, barley and bread are grain; the hunter's and the herd's meat is
    /// meat; forage is forage; fish is fish. Nothing is poultry yet. A good nobody eats has no group.
    /// </summary>
    [Fact]
    public void EveryShippedFoodSitsInItsGroup()
    {
        foreach (SimConfig config in new[] { VillageFixtures.Village, ShippedConfig.Load() })
        {
            GoodsCatalog goods = new(config.GoodsCatalog);
            Assert.Equal(FoodGroup.Forage, goods.FoodGroupOf(Goods.Produce));
            Assert.Equal(FoodGroup.Fish, goods.FoodGroupOf(Goods.Fish));
            Assert.Equal(FoodGroup.Meat, goods.FoodGroupOf(Goods.Meat));
            Assert.Equal(FoodGroup.Grain, goods.FoodGroupOf(Goods.Wheat));
            Assert.Equal(FoodGroup.Grain, goods.FoodGroupOf(Goods.Barley));
            Assert.Equal(FoodGroup.Grain, goods.FoodGroupOf(Goods.Bread));
            Assert.DoesNotContain(goods.EdibleGoods, g => goods.FoodGroupOf(g) == FoodGroup.Poultry);
            Assert.All(goods.EdibleGoods, g => Assert.NotEqual(FoodGroup.None, goods.FoodGroupOf(g)));
            Assert.Equal(FoodGroup.None, goods.FoodGroupOf(Goods.Logs));
            Assert.Equal(FoodGroup.None, goods.FoodGroupOf(Goods.Flour));
            Assert.Equal(FoodGroup.None, goods.FoodGroupOf(Goods.Ale));
        }
    }

    /// <summary>Five groups (D935, Joe: <i>"poultry is its own group"</i>) — and nothing else counts as one.</summary>
    [Fact]
    public void ThereAreFiveFoodGroups()
    {
        Assert.Equal(5, FoodGroups.All.Count);
        Assert.Equal(
            new[] { FoodGroup.Grain, FoodGroup.Meat, FoodGroup.Poultry, FoodGroup.Fish, FoodGroup.Forage },
            FoodGroups.All);
    }

    /// <summary>
    /// ⛔ An edible good with no group is refused at load — a modder's new food lands in a group or not at all (the D277
    /// shape) — and a group on a good nobody eats is refused too, because it would be a fact that means nothing.
    /// </summary>
    [Fact]
    public void AFoodWithNoGroupAndAGroupOnNoFoodAreRefused()
    {
        SimConfig config = VillageFixtures.Village;

        var noGroup = config.GoodsCatalog.Select(r => r.Id == (int)Goods.Fish ? r with { FoodGroup = FoodGroup.None } : r).ToList();
        SimConfigException a = Assert.Throws<SimConfigException>(() => (config with { GoodsList = noGroup }).Validate());
        Assert.Contains("'fish'", a.Message, StringComparison.Ordinal);
        Assert.Contains("food_group", a.Message, StringComparison.Ordinal);

        var stray = config.GoodsCatalog.Select(r => r.Id == (int)Goods.Logs ? r with { FoodGroup = FoodGroup.Meat } : r).ToList();
        SimConfigException b = Assert.Throws<SimConfigException>(() => (config with { GoodsList = stray }).Validate());
        Assert.Contains("'logs'", b.Message, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------
    //  §5.2–5.4 — the season's plate, the plain seasons, the warnings (step 2)
    // ---------------------------------------------------------------

    private static SimConfig Config => VillageFixtures.Village;

    private static ulong SeasonTicks(SimConfig c) => (ulong)c.TicksPerDay * (ulong)c.DaysPerSeason;

    /// <summary>A founded world with its clock at the first tick of season <paramref name="season"/>.</summary>
    private static SimWorld WorldAt(int season)
    {
        SimWorld world = SimFactory.CreatePhase0(Config, new InMemoryLogSink()).World;
        world.Tick = SeasonTicks(world.Config) * (ulong)season;
        return world;
    }

    private static void Read(SimWorld world) => new HealthSystem().Execute(world);

    private static void Ate(Villager v, FoodGroup group, int units, int meals = 6)
    {
        v.AteThisSeason[FoodGroups.Slot(group)] += units;
        v.MealsThisSeason += meals;
    }

    private static void NextSeason(SimWorld world) => world.Tick += SeasonTicks(world.Config);

    /// <summary>A meal goes on the eater's plate by group — a share of each food the larder holds — and counts one meal.</summary>
    [Fact]
    public void AMealGoesOnTheEatersPlateByGroup()
    {
        SimWorld world = WorldAt(1);
        Villager adult = world.Villagers.First(v => v.CanWork);
        var larder = new Stockpile(Stockpile.Kinds);
        larder.Add(Goods.Produce, 30);
        larder.Add(Goods.Fish, 30);
        Assert.True(world.TakeAMealFrom(larder, 6, adult, out _, out _));
        Assert.Equal(3, adult.AteThisSeason[FoodGroups.Slot(FoodGroup.Forage)]);
        Assert.Equal(3, adult.AteThisSeason[FoodGroups.Slot(FoodGroup.Fish)]);
        Assert.Equal(1, adult.MealsThisSeason);

        // A meal nobody keeps count of (the old overload) puts nothing on anyone's plate.
        Assert.True(world.TakeAMealFrom(larder, 6, out _, out _));
        Assert.Equal(1, adult.MealsThisSeason);
    }

    /// <summary>⭐ Ninety percent one group is plain (Joe's call 3); eighty-nine is varied; too few meals is not read.</summary>
    [Fact]
    public void NinetyPercentOneGroupIsAPlainSeason()
    {
        SimWorld world = WorldAt(1);
        Villager[] v = world.Villagers.Where(x => x.Alive).Take(3).ToArray();
        Ate(v[0], FoodGroup.Forage, 90);
        Ate(v[0], FoodGroup.Fish, 10, meals: 0);
        Ate(v[1], FoodGroup.Forage, 89);
        Ate(v[1], FoodGroup.Fish, 11, meals: 0);
        v[1].PlainSeasons = 3;
        Ate(v[2], FoodGroup.Forage, 30, meals: 2);
        v[2].PlainSeasons = 5;
        Read(world);

        Assert.Equal((1, FoodGroup.Forage), (v[0].PlainSeasons, v[0].PlainOn));
        Assert.Equal(1, v[1].PlainSeasons);  // varied: two off
        Assert.Equal(5, v[2].PlainSeasons);  // too few meals: as it was
        Assert.All(v, x => Assert.Equal(0, x.MealsThisSeason));
        Assert.All(v, x => Assert.All(x.AteThisSeason, a => Assert.Equal(0, a)));
    }

    /// <summary>A varied season never takes the count below nought, and only a season's turn reads.</summary>
    [Fact]
    public void AVariedSeasonNeverGoesBelowNoughtAndOnlyTheTurnReads()
    {
        SimWorld world = WorldAt(1);
        Villager a = world.Villagers.First(x => x.Alive);
        a.PlainSeasons = 1;
        Ate(a, FoodGroup.Forage, 50);
        Ate(a, FoodGroup.Grain, 50);
        world.Tick += (ulong)world.Config.TicksPerDay;  // a day into the season: no read
        Read(world);
        Assert.Equal(1, a.PlainSeasons);
        Assert.Equal(12, a.MealsThisSeason);

        world.Tick = SeasonTicks(world.Config) * 2;
        Read(world);
        Assert.Equal(0, a.PlainSeasons);
    }

    /// <summary>⭐ The card's words (§5.3): well, then a plain diet at a year, then run down at three — never a number.</summary>
    [Fact]
    public void TheCardSaysWellThenAPlainDietThenRunDown()
    {
        SimWorld world = WorldAt(1);
        Villager a = world.Villagers.First(x => x.CanWork);
        Assert.Equal(HealthState.Well, world.HealthOf(a));
        Assert.Null(world.HealthNoteFor(a));

        a.PlainSeasons = 4;
        a.PlainOn = FoodGroup.Forage;
        Assert.Equal(HealthState.PlainDiet, world.HealthOf(a));
        Assert.Equal($"{a.Name} has eaten nothing but forage for a year. A body wants more than one kind of food.", world.HealthNoteFor(a));

        a.PlainSeasons = 12;
        Assert.Equal(HealthState.RunDown, world.HealthOf(a));
        Assert.Equal($"{a.Name} has eaten nothing but forage for three years, and it's telling. Grain, meat or fish would set it right.",
            world.HealthNoteFor(a));
        Assert.Equal(1, world.RunDownCount());
    }

    /// <summary>
    /// ⭐ Said before it bites (gate item 6): the village is told once, the turn the first of them reaches a plain diet — and
    /// once again the turn the first is run down. Never every season.
    /// </summary>
    [Fact]
    public void TheVillageIsToldOnceAtAYearAndOnceAtThreeYears()
    {
        SimWorld world = WorldAt(1);
        Villager[] v = world.Villagers.Where(x => x.Alive).ToArray();
        foreach (Villager x in v)
        {
            x.PlainSeasons = 3;
        }

        // One comes to it a season behind the rest — crossing a line the village has already been told of says nothing.
        Villager late = v[^1];
        late.PlainSeasons = 2;

        int Said(string title) => world.Moments.Count(m => m.Title == title);

        void APlainSeason()
        {
            foreach (Villager x in v)
            {
                Ate(x, FoodGroup.Forage, 60);
            }

            Read(world);
            NextSeason(world);
        }

        APlainSeason();
        Assert.Equal(1, Said("A plain diet"));
        Moment told = world.Moments.Single(m => m.Title == "A plain diet");
        Assert.False(told.WaitsToBeDismissed);
        Assert.Contains("nothing but forage for a year", told.Body, StringComparison.Ordinal);
        Assert.Contains("a farm, a hunter's lodge or a fishery", told.Body, StringComparison.Ordinal);

        for (int s = 0; s < 7; s++)
        {
            APlainSeason();
        }

        Assert.Equal(1, Said("A plain diet"));
        Assert.Equal(0, Said("Run down"));

        APlainSeason();
        Assert.Equal(1, Said("Run down"));
        Assert.Equal(v.Length - 1, world.RunDownCount());
        Assert.Equal(HealthState.PlainDiet, world.HealthOf(late));

        APlainSeason();
        Assert.Equal(v.Length, world.RunDownCount());
        Assert.Equal(1, Said("Run down"));
        Assert.Equal(1, Said("A plain diet"));
    }

    /// <summary>⭐ A village eating forage alone is told within its second year — read off the meals it really eats.</summary>
    [Fact]
    public void AForageOnlyVillageIsToldByItsSecondYear()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);
        loop.Step(world.Config.TicksPerYear * 2);
        Assert.Contains(world.Moments, m => m.Title == "A plain diet");
        Assert.Contains(world.Villagers, x => x.Alive && x.PlainOn == FoodGroup.Forage && x.PlainSeasons >= 4);
    }

    /// <summary>Health off (<c>health_plain_percent</c> 0) reads nothing and tells nobody.</summary>
    [Fact]
    public void HealthOffReadsNothing()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config with { HealthPlainPercent = 0 }, new InMemoryLogSink());
        ColdStartTests.PlayTheOpening(loop.World);
        loop.Step(loop.World.Config.TicksPerYear * 2);
        Assert.All(loop.World.Villagers, x => Assert.Equal(0, x.PlainSeasons));

        // ⭐ And nothing of it is kept: no plate, no meals counted — so a village with health off hashes as before W6.
        Assert.All(loop.World.Villagers, x => Assert.Equal(0, x.MealsThisSeason));
        Assert.All(loop.World.Villagers, x => Assert.All(x.AteThisSeason, a => Assert.Equal(0, a)));
        Assert.DoesNotContain(loop.World.Moments, m => m.Title == "A plain diet");
    }

    /// <summary>Joe's numbers are shipped (D935) and the validator keeps the warning ahead of the bite.</summary>
    [Fact]
    public void JoesNumbersShipAndTheWarningComesFirst()
    {
        SimConfig shipped = ShippedConfig.Load();
        Assert.Equal((90, 3, 2, 4, 12), (shipped.HealthPlainPercent, shipped.HealthReadMinMeals, shipped.HealthRecoverSeasons,
            shipped.HealthPlainDietSeasons, shipped.HealthRunDownSeasons));
        Assert.Throws<SimConfigException>(() => (Config with { HealthRunDownSeasons = 4 }).Validate());
        Assert.Throws<SimConfigException>(() => (Config with { HealthPlainPercent = 50 }).Validate());
        (Config with { HealthPlainPercent = 0 }).Validate();
    }

    [Theory]
    [InlineData("ate")]
    [InlineData("meals")]
    [InlineData("plain")]
    [InlineData("plain_on")]
    [InlineData("sick")]
    public void EveryHealthFieldIsHashed(string field)
    {
        SimWorld world = WorldAt(0);
        Villager a = world.Villagers.First(x => x.Alive);
        ulong before = StateHash.Compute(world);
        switch (field)
        {
            case "ate": a.AteThisSeason[FoodGroups.Slot(FoodGroup.Fish)] = 1; break;
            case "meals": a.MealsThisSeason = 1; break;
            case "plain": a.PlainSeasons = 1; break;
            case "plain_on": a.PlainOn = FoodGroup.Meat; break;
            case "sick": a.SickDays = 1; break;
            default: throw new ArgumentOutOfRangeException(nameof(field), field, "no such field");
        }

        Assert.NotEqual(before, StateHash.Compute(world));
    }

    /// <summary>Every health field survives a save and a load; somebody with nothing counted loads well.</summary>
    [Fact]
    public void TheHealthFieldsAreSaved()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        Villager a = loop.World.Villagers.First(x => x.Alive);
        Ate(a, FoodGroup.Fish, 7, meals: 2);
        Ate(a, FoodGroup.Grain, 5, meals: 0);
        a.PlainSeasons = 9;
        a.PlainOn = FoodGroup.Fish;
        a.SickDays = 3;

        JsonObject document = SaveGame.Capture(loop, "test", "now", "test");
        SimWorld back = SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), loop.World.Config).World;
        Villager b = back.Villagers.Single(x => x.Id == a.Id);
        Assert.Equal(a.AteThisSeason, b.AteThisSeason);
        Assert.Equal((2, 9, FoodGroup.Fish, 3), (b.MealsThisSeason, b.PlainSeasons, b.PlainOn, b.SickDays));
        Villager other = back.Villagers.First(x => x.Id != a.Id);
        Assert.Equal((0, FoodGroup.None), (other.PlainSeasons, other.PlainOn));
    }

    // ---------------------------------------------------------------
    //  §5.5 — falling ill, never fatally (step 4)
    // ---------------------------------------------------------------

    private static void RunDown(Villager v)
    {
        v.PlainSeasons = 12;
        v.PlainOn = FoodGroup.Forage;
    }

    /// <summary>
    /// ⭐ A run-down person falls ill about once a year (Joe's call 6) — and a village that went run down together never takes to
    /// bed on one day: the draw is per person, seeded.
    /// </summary>
    [Fact]
    public void TheRunDownFallIllAboutOnceAYearAndNotAllAtOnce()
    {
        int people = 0, fell = 0;
        for (ulong seed = 1; seed <= 12; seed++)
        {
            SimWorld world = SimFactory.CreatePhase0(Config with { Seed = seed }, new InMemoryLogSink()).World;
            Villager[] v = world.Villagers.Where(x => x.Alive).ToArray();
            foreach (Villager x in v)
            {
                RunDown(x);
            }

            var dayFell = new Dictionary<int, int>();
            int days = world.Config.DaysPerSeason * 4;
            for (int d = 1; d <= days; d++)
            {
                world.Tick = (ulong)(d * world.Config.TicksPerDay) + 1UL * (ulong)world.Config.TicksPerDay;
                Read(world);
                foreach (Villager x in v)
                {
                    if (x.SickDays > 0 && !dayFell.ContainsKey(x.Id))
                    {
                        dayFell[x.Id] = d;
                        Assert.Equal(world.Config.HealthSickDays, x.SickDays);
                    }
                }
            }

            people += v.Length;
            fell += dayFell.Count;
            if (v.Length >= 3)
            {
                Assert.True(dayFell.Values.Distinct().Count() > 1 || dayFell.Count < v.Length, $"seed {seed}: everybody fell ill on one day");
            }
        }

        // One in 120 a day over a year: about 63 % fall ill at least once.
        Assert.InRange(fell * 100 / people, 40, 85);
    }

    /// <summary>Only the run down fall ill: a village on a plain diet, or well, draws nothing in years.</summary>
    [Fact]
    public void OnlyTheRunDownFallIll()
    {
        SimWorld world = WorldAt(1);
        Villager[] v = world.Villagers.Where(x => x.Alive).ToArray();
        v[0].PlainSeasons = 11;
        v[0].PlainOn = FoodGroup.Forage;
        for (int d = 1; d <= 3 * world.Config.DaysPerSeason * 4; d++)
        {
            world.Tick = (ulong)d * (ulong)world.Config.TicksPerDay + 7UL * (ulong)world.Config.TicksPerDay;
            new HealthSystem().Execute(world);
            foreach (Villager x in v)
            {
                x.MealsThisSeason = 0;  // nothing read: the counts stay where they were posed
            }
        }

        Assert.All(v, x => Assert.Equal(0, x.SickDays));
    }

    /// <summary>
    /// ⭐ Ill, they finish what they are doing, go home and lie there <c>health_sick_days</c> days — eating, keeping their job —
    /// then get up still run down. Nobody dies of it.
    /// </summary>
    [Fact]
    public void TheIllGoHomeToBedForFiveDaysAndGetUpStillRunDown()
    {
        var log = new InMemoryLogSink();
        SimLoop loop = SimFactory.CreatePhase0(Config with { HealthFallIllOneInDays = int.MaxValue }, log);
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);
        loop.Step(world.Config.TicksPerDay * 10);
        Villager ada = world.Villagers.First(x => x.Alive && x.CanWork);
        RunDown(ada);
        int job = ada.WorkplaceId;
        ada.SickDays = world.Config.HealthSickDays;
        Assert.Equal(HealthState.Sick, world.HealthOf(ada));
        Assert.StartsWith($"{ada.Name} is ill", world.HealthNoteFor(ada), StringComparison.Ordinal);

        int toBed = 0;
        while (ada.State != VillagerState.SickInBed)
        {
            loop.Step(1);
            Assert.True(++toBed < world.Config.TicksPerDay * 10, "never got home to bed");
        }

        Assert.Equal(world.RestingPlaceOf(ada), ada.Tile);
        int inBed = 0;
        while (ada.State == VillagerState.SickInBed)
        {
            loop.Step(1);
            inBed++;
        }

        // Five days a-bed, give or take the ticks a meal costs.
        Assert.InRange(inBed, world.Config.HealthSickDays * world.Config.TicksPerDay, world.Config.HealthSickDays * world.Config.TicksPerDay + 8);
        Assert.True(ada.Alive);
        Assert.Equal(0, ada.SickDays);
        Assert.Equal(HealthState.RunDown, world.HealthOf(ada));
        Assert.Equal(job, ada.WorkplaceId);
    }

    /// <summary>A run-down village lives on: three years of nothing but forage, people in bed now and then, nobody dead of it.</summary>
    [Fact]
    public void ARunDownVillageFallsIllAndNobodyDiesOfIt()
    {
        var log = new InMemoryLogSink();
        SimLoop loop = SimFactory.CreatePhase0(Config, log);
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);
        foreach (Villager x in world.Villagers.Where(x => x.Alive))
        {
            RunDown(x);
        }

        int bedTicks = 0;
        for (int t = 0; t < world.Config.TicksPerYear * 2; t++)
        {
            loop.Step(1);
            bedTicks += world.Villagers.Count(x => x.State == VillagerState.SickInBed);
        }

        Assert.True(bedTicks > 0, "nobody took to bed in two years run down");
        Assert.Contains(log.Entries, e => e.Message.Contains("has taken to bed", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------
    //  Phase 6 gate item 2 — a golden that reaches health
    // ---------------------------------------------------------------

    /// <summary>
    /// ⭐ A GOLDEN THAT REACHES HEALTH (Phase 6 gate item 2, D941): the fixture's founding on the played opening — forage alone,
    /// as every unattended village eats — six years on. By then the founders have been told of a plain diet, gone run down and
    /// taken to bed; every other golden either eats several foods or is too short to. Health off, the same village hashes as it
    /// did before W6 — the guard has teeth both ways.
    /// </summary>
    [Fact]
    public void TheGoldenThatSeesAVillageRunDown()
    {
        SimLoop loop = SimFactory.CreatePhase0(Config, new InMemoryLogSink());
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);
        int bedTicks = 0;
        for (int t = 0; t < world.Config.TicksPerYear * 6; t++)
        {
            loop.StepOnce();
            bedTicks += world.Villagers.Count(x => x.State == VillagerState.SickInBed);
        }

        Assert.Contains(world.Moments, m => m.Title == "A plain diet");
        Assert.Contains(world.Moments, m => m.Title == "Run down");
        Assert.True(bedTicks > 0, "nobody took to bed: the golden does not reach the illness");
        Assert.Equal(RunDownGolden, StateHash.Compute(world));

        SimLoop off = SimFactory.CreatePhase0(Config with { HealthPlainPercent = 0 }, new InMemoryLogSink());
        ColdStartTests.PlayTheOpening(off.World);
        off.Step(off.World.Config.TicksPerYear * 6);
        Assert.NotEqual(RunDownGolden, StateHash.Compute(off.World));
    }

    private const ulong RunDownGolden = 14704821933836161333UL;
}
