using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Names — <c>specs/names-and-birthdays.md</c> (D395, D465): a first name is a prefix and a suffix
/// from Joe's lists, hashed from the seed and the villager's id, never drawn.
/// </summary>
public sealed class NameTests
{
    private readonly ITestOutputHelper _output;

    public NameTests(ITestOutputHelper output) => _output = output;

    private static SimWorld Build(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;

    /// <summary>
    /// ⛔ Naming takes nothing from the stream (§3). Until D465 every founder and every birth spent a
    /// draw on a name, so the length of the name list was part of every seed.
    /// </summary>
    [Fact]
    public void NamingDrawsNothingFromTheStream()
    {
        SimWorld world = Build(VillageFixtures.Village);
        DeterministicRandom before = world.Rng;

        string name = world.NameFor(999);

        Assert.Equal(before, world.Rng);
        Assert.False(string.IsNullOrEmpty(name));
        _output.WriteLine($"villager 999 of seed {world.Seed} is {name}");
    }

    /// <summary>
    /// A name is a function of the seed and the id, not of the order people were named in — the
    /// claim that lets a list grow without reshuffling anybody already in a valley.
    /// </summary>
    [Fact]
    public void TheSameSeedAndIdGiveTheSameName()
    {
        SimWorld first = Build(VillageFixtures.Village);
        SimWorld second = Build(VillageFixtures.Village);

        Assert.Equal(first.Villagers.Select(v => v.Name), second.Villagers.Select(v => v.Name));

        // Asked in a different order in the second world: the answer is the id's, not the turn's.
        string seventhFirst = first.NameFor(7);
        _ = second.NameFor(30);
        _ = second.NameFor(12);
        Assert.Equal(seventhFirst, second.NameFor(7));

        // And another valley names its founders otherwise.
        SimWorld other = Build(VillageFixtures.Village with { Seed = VillageFixtures.Village.Seed + 1 });
        Assert.NotEqual(first.Villagers.Select(v => v.Name), other.Villagers.Select(v => v.Name));
        _output.WriteLine($"{string.Join(", ", first.Villagers.Select(v => v.Name))} / " +
                          $"{string.Join(", ", other.Villagers.Select(v => v.Name))}");
    }

    /// <summary>
    /// Only the living hold a name: a founder's own first choice is taken while she lives and
    /// free again once she is dead — a grandchild may carry it.
    /// </summary>
    [Fact]
    public void ADeadVillagersNameMayComeBack()
    {
        SimWorld world = Build(VillageFixtures.Village);
        Villager founder = world.Villagers[0];

        Assert.NotEqual(founder.Name, world.NameFor(founder.Id));

        founder.Alive = false;
        Assert.Equal(founder.Name, world.NameFor(founder.Id));
    }

    /// <summary>
    /// No two living villagers share a first name. Posed on 25 names (5 × 5) so that a village of
    /// a dozen or two would collide by chance within a few years if nothing prevented it — on
    /// Joe's 9,801 a broken check could pass a fifty-year run by luck.
    /// </summary>
    [Fact]
    public void NoTwoLivingVillagersShareAFirstName()
    {
        SimConfig config = VillageFixtures.Village with
        {
            FirstNamePrefixes = new[] { "Ag", "Wen", "Hat", "Ot", "Am" },
            FirstNameSuffixes = new[] { "nes", "dell", "tie", "to", "os" },
        };
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        int checkedDays = 0;
        int mostAlive = 0;
        for (int day = 0; day < config.TicksPerYear / config.TicksPerDay * 30; day++)
        {
            loop.Step(config.TicksPerDay);
            List<string> living = world.Villagers.Where(v => v.Alive).Select(v => v.Name).ToList();
            mostAlive = Math.Max(mostAlive, living.Count);
            if (living.Count > 25)
            {
                continue;
            }

            checkedDays++;
            Assert.Equal(living.Count, living.Distinct(StringComparer.Ordinal).Count());
        }

        _output.WriteLine($"{world.Villagers.Count} ever lived, at most {mostAlive} at once; {checkedDays} days checked");
        Assert.True(world.Villagers.Count > 12, "the pose needs births to name");
    }

    /// <summary>A name list may hold nothing twice and nothing blank, and the refusal names the key.</summary>
    [Fact]
    public void ANameListMayNotRepeatAnEntry()
    {
        SimConfig good = VillageFixtures.Village;

        SimConfigException prefix = Assert.Throws<SimConfigException>(() =>
            (good with { FirstNamePrefixes = new[] { "Ag", "Val", "Val" } }).Validate());
        Assert.Contains("first_name_prefixes", prefix.Message);
        Assert.Contains("\"Val\" twice", prefix.Message);

        SimConfigException suffix = Assert.Throws<SimConfigException>(() =>
            (good with { FirstNameSuffixes = new[] { "tis", "nes", "tis" } }).Validate());
        Assert.Contains("first_name_suffixes", suffix.Message);

        SimConfigException surname = Assert.Throws<SimConfigException>(() =>
            (good with { HouseholdNames = new[] { "Cooper", "Cooper" } }).Validate());
        Assert.Contains("household_names", surname.Message);

        SimConfigException blank = Assert.Throws<SimConfigException>(() =>
            (good with { FirstNameSuffixes = new[] { "nes", " " } }).Validate());
        Assert.Contains("blank", blank.Message);

        // And Joe's own lists, as shipped, pass.
        ShippedConfig.Load().Validate();
        Assert.Equal(99, good.FirstNamePrefixes.Count);
        Assert.Equal(99, good.FirstNameSuffixes.Count);
    }
}
