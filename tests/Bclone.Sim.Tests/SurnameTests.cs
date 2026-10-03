using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Surnames — <c>specs/names-and-birthdays.md §4</c> (D467): a founding household's from Joe's sixty,
/// a couple's the older partner's, a taken-over house the couple's, and a rename a label only.
/// </summary>
public sealed class SurnameTests
{
    private readonly ITestOutputHelper _output;

    public SurnameTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// The founding never names two families alike, and every founder carries their household's
    /// name. Posed on two surnames for two households, so a founding that did not look would
    /// collide on about half the seeds.
    /// </summary>
    [Fact]
    public void FoundingHouseholdsHaveDifferentSurnames()
    {
        SimConfig tight = VillageFixtures.Village with { HouseholdNames = new[] { "Cooper", "Mason" } };
        foreach (SimConfig config in new[] { tight, ShippedConfig.Load() })
        {
            for (ulong seed = 1; seed <= 60; seed++)
            {
                SimWorld world = SimFactory.CreatePhase0(config with { Seed = seed }, new InMemoryLogSink()).World;
                List<string> surnames = world.Households.Select(h => h.Surname).ToList();

                Assert.Equal(surnames.Count, surnames.Distinct(StringComparer.Ordinal).Count());
                Assert.All(world.Villagers, v => Assert.Equal(world.HouseholdOf(v).Surname, v.Surname));
                Assert.All(world.Households, h => Assert.Equal(h.Surname, h.Name));
            }
        }

        SimWorld shown = SimFactory.CreatePhase0(ShippedConfig.Load(), new InMemoryLogSink()).World;
        _output.WriteLine(string.Join(", ", shown.Villagers.Select(v => v.FullName)));
    }

    /// <summary>
    /// ⭐ A couple carries <b>the older partner's</b> surname (Joe, D467) — the earlier birth, the
    /// lower id on a tie — checked on every pairing a village makes, against the surnames both
    /// partners carried the day before.
    /// </summary>
    [Fact]
    public void ACoupleCarriesTheOlderPartnersName()
    {
        SimConfig config = VillageFixtures.Village with { StartingHouseholds = 3 };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        int couples = 0;
        int tookTheYoungers = 0;
        int founders = world.Villagers.Count;
        WatchThePairings(loop, years: 60, (elder, younger, wasElders, wasYoungers, household) =>
        {
            Assert.Equal(wasElders, household.Surname);
            Assert.Equal(wasElders, elder.Surname);
            Assert.Equal(wasElders, younger.Surname);
            couples++;
            if (wasElders != wasYoungers)
            {
                tookTheYoungers++;
            }
        }, skipFounders: founders);

        _output.WriteLine($"{couples} couples, {tookTheYoungers} of them where the younger gave up a different name");
        Assert.True(tookTheYoungers > 0, "the pose needs a couple of two different names");
    }

    /// <summary>
    /// A couple taking over a dead family's house makes it theirs: their name, and not the name the
    /// player gave the family who lived there. ⭐ And the dead keep theirs — they stay in the
    /// household's member list, which is why a villager's surname is stored, not read through it.
    /// </summary>
    [Fact]
    public void TakingOverAnEmptyHouseTakesTheCouplesNameAndTheDeadKeepTheirs()
    {
        SimConfig config = VillageFixtures.Village with { StartingHouseholds = 3 };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        // The first household dies at the founding, and the player had named it.
        Household empty = world.Households[0];
        string theirs = empty.Surname;
        Assert.True(world.Rename(empty, "the Ashfords").Allowed);
        List<Villager> dead = empty.MemberIds.Select(id => world.Villagers[id - 1]).ToList();
        foreach (Villager founder in dead)
        {
            founder.Alive = false;
        }

        bool tookItOver = false;
        WatchThePairings(loop, years: 60, (elder, _, wasElders, _, household) =>
        {
            if (household.Id != empty.Id)
            {
                return;
            }

            tookItOver = true;
            Assert.Equal(wasElders, empty.Surname);
            Assert.Null(empty.GivenName);
            Assert.Equal(wasElders, empty.Name);
        }, skipFounders: world.Villagers.Count, until: () => tookItOver);

        Assert.True(tookItOver, "no couple ever took the empty house over");
        Assert.All(dead, founder =>
        {
            Assert.Equal(theirs, founder.Surname);
            Assert.EndsWith(" " + theirs, founder.FullName, StringComparison.Ordinal);
        });
        _output.WriteLine($"the {theirs} house (called the Ashfords) is the {empty.Name} household now; " +
                          $"{string.Join(", ", dead.Select(v => v.FullName))} keep theirs");
    }

    /// <summary>
    /// ⛔ A rename is the household's label, never a surname (D467): the player's free text names
    /// the household, and a child born after it carries the family's surname — nobody is ever
    /// called <i>Ren the Ashfords</i>.
    /// </summary>
    [Fact]
    public void ARenameIsALabelNotASurname()
    {
        SimLoop loop = SimFactory.CreatePhase0(VillageFixtures.Village, new InMemoryLogSink());
        SimWorld world = loop.World;
        Household home = world.Households[0];
        string surname = home.Surname;

        Assert.True(world.Rename(home, "the Ashfords").Allowed);
        Assert.Equal("the Ashfords", home.Name);
        Assert.Equal(surname, home.Surname);
        Assert.All(home.MemberIds, id => Assert.Equal(surname, world.Villagers[id - 1].Surname));

        int before = home.MemberIds.Count;
        for (int day = 0; day < world.Config.TicksPerYear / world.Config.TicksPerDay * 20 && home.MemberIds.Count == before; day++)
        {
            loop.Step(world.Config.TicksPerDay);
        }

        Assert.True(home.MemberIds.Count > before, "no child was born to the renamed household");
        Villager child = world.Villagers[home.MemberIds[^1] - 1];
        Assert.Equal(surname, child.Surname);
        Assert.DoesNotContain("Ashfords", child.FullName, StringComparison.Ordinal);
        _output.WriteLine($"{child.FullName} was born to {home.Name}");
    }

    /// <summary>
    /// Step a day at a time; whenever a villager who was unpaired yesterday is paired today, hand the
    /// couple to <paramref name="check"/> with the surnames both carried yesterday — the older first.
    /// </summary>
    private static void WatchThePairings(
        SimLoop loop, int years, Action<Villager, Villager, string, string, Household> check,
        int skipFounders, Func<bool>? until = null)
    {
        SimWorld world = loop.World;
        int days = world.Config.TicksPerYear / world.Config.TicksPerDay * years;

        for (int day = 0; day < days && !(until?.Invoke() ?? false); day++)
        {
            var yesterday = world.Villagers.ToDictionary(v => v.Id, v => (v.Surname, v.IsPaired));
            loop.Step(world.Config.TicksPerDay);

            foreach (Villager a in world.Villagers)
            {
                if (a.Id <= skipFounders || !a.IsPaired || !yesterday.TryGetValue(a.Id, out var was) || was.IsPaired)
                {
                    continue;
                }

                Villager b = world.Villagers[a.PartnerId - 1];
                if (b.Id < a.Id && !yesterday[b.Id].IsPaired)
                {
                    continue; // the pair is handed over once, from its lower id
                }

                // The older, worked out here rather than asked of the code under test.
                Villager elder = a.BirthYear != b.BirthYear
                    ? (a.BirthYear < b.BirthYear ? a : b)
                    : (a.Id < b.Id ? a : b);
                Villager younger = ReferenceEquals(elder, a) ? b : a;
                check(elder, younger, yesterday[elder.Id].Surname, yesterday[younger.Id].Surname, world.HouseholdOf(a));
            }
        }
    }
}
