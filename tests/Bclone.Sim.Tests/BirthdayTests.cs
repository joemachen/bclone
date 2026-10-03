using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Birthdays — <c>specs/names-and-birthdays.md §5</c> (D468): a villager is born on a tick, is a
/// year older on that day each year rather than at New Year, and comes of age and dies of old age on
/// it.
/// </summary>
public sealed class BirthdayTests
{
    private readonly ITestOutputHelper _output;

    public BirthdayTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Every founder is <c>founder_age</c> on the first tick, born in the year before it at a hashed
    /// point — so the founders of one valley do not share a birthday, and across valleys the days
    /// land all over the year.
    /// </summary>
    [Fact]
    public void FoundersAreTheirAgeOnTheFirstTickWithBirthdaysOfTheirOwn()
    {
        SimConfig config = ShippedConfig.Load();
        long year = config.TicksPerYear;
        var days = new HashSet<long>();

        for (ulong seed = 1; seed <= 50; seed++)
        {
            SimWorld world = SimFactory.CreatePhase0(config with { Seed = seed }, new InMemoryLogSink()).World;
            List<Villager> founders = world.Villagers.Where(v => v.Founder).ToList();

            Assert.All(founders, v =>
            {
                Assert.Equal(config.FounderAge, v.AgeYears);
                Assert.InRange(v.BirthTick, -(config.FounderAge + 1) * year + 1, -config.FounderAge * year);
            });
            Assert.True(founders.Select(v => v.BirthTick).Distinct().Count() > 1, $"seed {seed}: one birthday for every founder");
            days.UnionWith(founders.Select(v => (-v.BirthTick) % year / config.TicksPerDay));
        }

        _output.WriteLine($"{days.Count} different days of the year among 200 founders");
        Assert.True(days.Count > 60, "the founders' birthdays bunch up");
    }

    /// <summary>
    /// ⭐ A year older on their own day, not at New Year: in the first year every founder's age
    /// turns on the tick a whole year since their birth completes, and on no other.
    /// </summary>
    [Fact]
    public void AVillagerAgesOnTheirBirthdayNotAtNewYear()
    {
        SimConfig config = ShippedConfig.Load();
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        List<Villager> founders = world.Villagers.Where(v => v.Founder).ToList();
        var turned = new Dictionary<int, long>();

        for (int i = 0; i <= config.TicksPerYear; i++)
        {
            long tick = (long)world.Tick;
            var before = founders.ToDictionary(v => v.Id, v => v.AgeYears);
            loop.StepOnce();
            foreach (Villager v in founders.Where(v => v.Alive && v.AgeYears != before[v.Id]))
            {
                Assert.False(turned.ContainsKey(v.Id), $"{v.FullName} aged twice in a year");
                turned[v.Id] = tick;
            }
        }

        foreach (Villager v in founders.Where(v => v.Alive))
        {
            Assert.True(turned.ContainsKey(v.Id), $"{v.FullName} never aged in Year 1");
            Assert.Equal(0, (turned[v.Id] - v.BirthTick) % config.TicksPerYear);
            Assert.Equal(config.FounderAge + 1, v.AgeYears);
        }

        Assert.Contains(turned.Values, tick => tick % config.TicksPerYear != 0);
        _output.WriteLine(string.Join(", ", founders.Select(v => $"{v.FullName} at tick {turned.GetValueOrDefault(v.Id)} ({world.BirthdayOf(v)})")));
    }

    /// <summary>
    /// ⭐ Each household tries for a child on a day of its own (D469, Joe: <i>"spread births through
    /// the year - each household has its own day"</i>): every child of a forty-year village is born on
    /// the first tick of their household's day, a household's children are whole years apart and at
    /// least <c>birth_interval_years</c>, and the village's children are not all born at New Year.
    /// </summary>
    [Fact]
    public void EachHouseholdTriesForAChildOnItsOwnDay()
    {
        SimConfig config = VillageFixtures.Village with { StartingHouseholds = 3 };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        long year = config.TicksPerYear;
        var births = new List<(Household Home, long Tick)>();

        for (int day = 0; day < config.TicksPerYear / config.TicksPerDay * 40; day++)
        {
            int known = world.Villagers.Count;
            loop.Step(config.TicksPerDay);
            for (int i = known; i < world.Villagers.Count; i++)
            {
                births.Add((world.HouseholdOf(world.Villagers[i]), world.Villagers[i].BirthTick));
            }
        }

        Assert.True(births.Count >= 6, $"only {births.Count} births in forty years");
        Assert.All(births, b => Assert.Equal(b.Home.DayForAChild * (long)config.TicksPerDay, b.Tick % year));
        foreach (IGrouping<Household, (Household Home, long Tick)> family in births.GroupBy(b => b.Home))
        {
            List<long> ticks = family.Select(b => b.Tick).Order().ToList();
            for (int i = 1; i < ticks.Count; i++)
            {
                long apart = ticks[i] - ticks[i - 1];
                Assert.Equal(0, apart % year);
                Assert.True(apart >= config.BirthIntervalYears * year, $"the {family.Key.Name} household's children {apart} ticks apart");
            }
        }

        // And the day is the household's own — the founding's and every couple's — not a default every
        // household falls back to. (Without this the guard scored zero for a household made with no
        // day: day 0 is a day, and a household on it is consistent with itself, D469.)
        Assert.Contains(world.Households, h => h.Id > config.StartingHouseholds);
        Assert.All(world.Households, h => Assert.Equal(
            NameHash.DayForAChild(world.Seed, h.Id, config.DaysPerSeason * 4), h.DayForAChild));

        List<long> days = births.Select(b => b.Tick % year / config.TicksPerDay).Distinct().ToList();
        Assert.True(days.Count > 1 && days.Any(d => d != 0), "the village's children are all born on one day");
        _output.WriteLine($"{births.Count} births in forty years on {days.Count} days of the year: " +
                          string.Join(", ", births.Take(6).Select(b => world.BirthdayOf(new Villager
                          {
                              Id = 0, Name = "", LifespanYears = 1, Carried = world.NewStockpile(), BirthTick = b.Tick,
                          }))));
    }

    /// <summary>A child becomes an adult on the tick of their <c>adult_age</c>-th birthday — not before, not at New Year.</summary>
    [Fact]
    public void AChildComesOfAgeOnTheirBirthday()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        Villager? child = null;
        long appearedOn = -1;
        for (int i = 0; i < config.TicksPerYear * 10 && child is null; i++)
        {
            appearedOn = (long)world.Tick;
            loop.StepOnce();
            child = world.Villagers.FirstOrDefault(v => !v.Founder);
        }

        // The birth tick is the tick they were born on — held against the tick they appeared, not
        // read back from the child, or a birth tick that lied would move this guard's clock with it
        // (it scored zero that way first, D468).
        Assert.NotNull(child);
        Assert.Equal(appearedOn, child.BirthTick);
        long comesOfAge = child.BirthTick + (long)config.AdultAge * config.TicksPerYear;
        while ((long)world.Tick < comesOfAge && child.Alive)
        {
            loop.StepOnce();
        }

        Assert.True(child.Alive, $"{child.FullName} died a child, so the pose saw nothing");
        Assert.Equal(LifeStage.Child, child.LifeStage);
        Assert.Equal(config.AdultAge - 1, child.AgeYears);

        loop.StepOnce();
        Assert.Equal(LifeStage.Adult, child.LifeStage);
        Assert.Equal(config.AdultAge, child.AgeYears);
        _output.WriteLine($"{child.FullName}, born {world.BirthdayOf(child)}, came of age on tick {comesOfAge}");
    }

    /// <summary>
    /// Old age comes on the birthday: every old-age death of a century falls on the day the dead
    /// turned their lifespan — so a year's old-age deaths spread across its days.
    /// </summary>
    [Fact]
    public void OldAgeDeathsComeOnTheirBirthday()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        loop.Step(config.TicksPerYear * 100);

        List<Villager> old = loop.World.Villagers
            .Where(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.OldAge).ToList();
        Assert.True(old.Count >= 4, $"only {old.Count} died of old age in a century");
        Assert.All(old, v => Assert.Equal(0, ((long)v.DiedAtTick!.Value - v.BirthTick) % config.TicksPerYear));
        Assert.Contains(old, v => v.DiedAtTick!.Value % (ulong)config.TicksPerYear != 0);
        _output.WriteLine($"{old.Count} died of old age, on {old.Select(v => v.DiedAtTick!.Value % (ulong)config.TicksPerYear).Distinct().Count()} different days of the year");
    }

    /// <summary>
    /// The birth tick is in the fingerprint and the age is not (D335): two valleys alike but for
    /// their founders' age hash differently, and an age posed by hand moves nothing.
    /// </summary>
    [Fact]
    public void TheBirthTickIsInTheFingerprint()
    {
        SimConfig config = VillageFixtures.Village;
        SimWorld twenty = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;
        SimWorld older = SimFactory.CreatePhase0(config with { FounderAge = config.FounderAge + 1 }, new InMemoryLogSink()).World;

        Assert.NotEqual(StateHash.Compute(twenty), StateHash.Compute(older));

        ulong before = StateHash.Compute(twenty);
        twenty.Villagers[0].AgeYears += 7;
        Assert.Equal(before, StateHash.Compute(twenty));
    }

    /// <summary>A birthday reads as a day — and a founder's never as a negative year.</summary>
    [Fact]
    public void ABirthdayReadsAsADay()
    {
        SimConfig config = ShippedConfig.Load();
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;
        Villager founder = world.Villagers.First(v => v.Founder);

        string said = world.BirthdayOf(founder);
        Assert.Matches(@"^Day \d+, (Spring|Summer|Fall|Winter), 20 years before the founding$", said);

        var child = new Villager
        {
            Id = 999, Name = "posed", LifespanYears = 60, Carried = world.NewStockpile(),
            BirthTick = config.TicksPerYear * 9L + config.TicksPerDay * (config.DaysPerSeason * 2 + 19),
        };
        Assert.Equal("Day 20, Fall, Year 10", world.BirthdayOf(child));
        _output.WriteLine($"{founder.FullName}: born {said}; posed: born {world.BirthdayOf(child)}");
    }
}
