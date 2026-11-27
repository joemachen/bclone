using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.Determinism;
using Kith.Sim.Logging;
using Kith.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

/// <summary>
/// ⭐ W5 — <b>skill for every working trade</b> (`specs/skills-for-every-trade.md §2`, D919–).
/// </summary>
/// <remarks>
/// The rows are all it takes: <see cref="SimWorld.BeginWork"/> asks <see cref="SimWorld.SkillGrownBy"/> for a trade's row,
/// so a master fisher casts in half the time the moment the fishing row exists. These guards hold that the rows exist, that
/// they bite where there is an action to shorten, and that adding them moved nobody's founding.
/// </remarks>
public sealed class TradeSkillTests
{
    private readonly ITestOutputHelper _output;

    public TradeSkillTests(ITestOutputHelper output) => _output = output;

    /// <summary>Every trade a villager can hold grows a skill — but the teacher, by Joe's call (D754).</summary>
    [Fact]
    public void EveryWorkingTradeButTheTeacherGrowsASkill()
    {
        SimConfig config = ShippedConfig.Load();
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;

        var without = new List<JobKind>();
        foreach (JobKind trade in Enum.GetValues<JobKind>())
        {
            if (trade != JobKind.Teacher && world.SkillGrownBy(trade) is null)
            {
                without.Add(trade);
            }
        }

        Assert.True(without.Count == 0, $"Trades with no skill: {string.Join(", ", without)}.");
        Assert.Null(world.SkillGrownBy(JobKind.Teacher));
    }

    /// <summary>Every row says what the years were and what the log says at mastery — pronoun-free, as the eleven are.</summary>
    [Fact]
    public void EverySkillRowHasItsCardLineAndItsMasteryLine()
    {
        foreach (SkillRow skill in ShippedConfig.Load().Skills)
        {
            Assert.StartsWith("as a", skill.YearsPhrase, StringComparison.Ordinal);
            Assert.Contains("{0}", skill.MasteryLine, StringComparison.Ordinal);
            Assert.Contains("{1}", skill.MasteryLine, StringComparison.Ordinal);
            foreach (string pronoun in new[] { " she ", " he ", " her ", " his ", " him " })
            {
                Assert.DoesNotContain(pronoun, $" {skill.MasteryLine.ToLowerInvariant()} ", StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// ⭐ A master of each of the new action trades takes mastery's ticks; a novice takes today's, to the tick (§2).
    /// </summary>
    [Fact]
    public void AMasterOfEachNewTradeTakesHalfTheTimeAndANoviceTodays()
    {
        SimConfig config = ShippedConfig.Load();
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;
        Villager novice = world.Villagers.First(v => v.Alive);
        Villager master = world.Villagers.Last(v => v.Alive);
        novice.ToolUses = 0;
        master.ToolUses = 0;

        (JobKind Trade, int Ticks, int Mastered)[] work =
        {
            (JobKind.Fisher, config.FishTicks, 5),
            (JobKind.Hunter, config.HuntTicks, 8),
            (JobKind.Smith, config.ForgeTicks, 2),
            (JobKind.Quarrier, config.QuarryDigTicks, 2),
            (JobKind.Miner, config.MineDigTicks, 3),
            (JobKind.Herdsman, config.ReapTicks, 2),
            (JobKind.Herdsman, config.HayFeedTicks, 2),
            (JobKind.Herdsman, config.CullTicks, 4),
            (JobKind.Groundskeeper, config.GraveDigTicks + config.GraveFillTicks, 6),
        };

        foreach ((JobKind trade, int baseTicks, int mastered) in work)
        {
            SkillRow skill = world.SkillGrownBy(trade)!;
            Assert.NotNull(skill);
            master.ProgressIn(skill.Id).Work = config.MasteryWorkFor(skill);

            int novices = world.WorkTicksFor(novice, trade, baseTicks);
            int masters = world.WorkTicksFor(master, trade, baseTicks);
            _output.WriteLine($"{trade,-14} {novices} ticks for a novice, {masters} for a master");

            Assert.Equal(baseTicks, novices);
            Assert.Equal(mastered, masters);
        }
    }

    /// <summary>
    /// ⛔ The new rows move no founding (D392): the founders' trades come from <c>founding_trades</c>, never the catalogue.
    /// </summary>
    [Fact]
    public void NoFoundingMovesWithTheTradeRows()
    {
        SimConfig with = ShippedConfig.Load();
        SimConfig without = with with
        {
            Skills = with.Skills.Where(s => s.Id <= 11).ToList(),
            Techniques = with.Techniques.Where(t => t.Skill <= 11).ToList(),
        };
        Assert.True(with.Skills.Count > without.Skills.Count, "The premise: the shipped skills include W5's rows.");

        SimWorld a = SimFactory.CreatePhase0(with, new InMemoryLogSink()).World;
        SimWorld b = SimFactory.CreatePhase0(without, new InMemoryLogSink()).World;
        Assert.Equal(StateHash.Compute(b), StateHash.Compute(a));
    }

    // -----------------------------------------------------------------
    //  §4 — the techniques
    // -----------------------------------------------------------------

    /// <summary>A home-grown master of one skill (`TechniqueTests.MakeAMasterOf`'s pose).</summary>
    internal static Villager MakeAMasterOf(SimWorld world, JobKind trade)
    {
        SkillRow skill = world.SkillGrownBy(trade)!;
        Villager villager = world.Villagers.First(v => v.Alive && v.CanWork);
        SkillProgress progress = villager.ProgressIn(skill.Id);
        progress.Work = world.Config.MasteryWorkFor(skill);
        progress.Ticks = world.Config.MasteryYearsFor(skill) * world.Config.TicksPerYear;
        progress.Mastered = true;
        progress.MasteredHere = true;
        return villager;
    }

    /// <summary>
    /// ⭐ Each new trade's technique raises what its action brings in, once a master here works it out — and not before
    /// (§4: +15 % fish, +15 % meat, +10 % stone, +20 % iron, +15 % hay).
    /// </summary>
    [Theory]
    [InlineData(JobKind.Fisher, 280, 322)]
    [InlineData(JobKind.Hunter, 900, 1035)]
    [InlineData(JobKind.Quarrier, 10, 11)]
    [InlineData(JobKind.Miner, 5, 6)]
    [InlineData(JobKind.Herdsman, 67, 77)]
    public void EachTradesTechniqueRaisesItsYieldOnceKnown(JobKind trade, int amount, int known)
    {
        var sink = new InMemoryLogSink();
        SimLoop loop = SimFactory.CreatePhase0(ShippedConfig.Load(), sink);
        SimWorld world = loop.World;

        Assert.Equal(amount, world.YieldWithTechnique(trade, amount));
        Villager master = MakeAMasterOf(world, trade);
        loop.Step(1);

        Assert.Equal(known, world.YieldWithTechnique(trade, amount));
        Assert.Contains(sink.Entries, e => e.Message.Contains(master.Name, StringComparison.Ordinal)
            && e.Category == LogCategory.Discovery);
    }

    /// <summary>
    /// ⭐ Tempering: a tool taken up lasts a quarter longer while the village knows it (§4) — a forge makes one tool, so a
    /// yield bonus there would round to nothing. Stone 150 → 187, iron 250 → 312.
    /// </summary>
    [Fact]
    public void TemperingMakesEveryToolTakenUpLastAQuarterLonger()
    {
        SimLoop loop = SimFactory.CreatePhase0(ShippedConfig.Load(), new InMemoryLogSink());
        SimWorld world = loop.World;
        IReadOnlyList<Goods> tools = world.GoodsCatalog.ToolsBestFirst;

        var before = tools.Select(t => world.ToolUsesWhenTaken(t)).ToList();
        Assert.Equal(tools.Select(t => world.GoodsCatalog[t].ToolUses), before);

        MakeAMasterOf(world, JobKind.Smith);
        loop.Step(1);

        var after = tools.Select(t => world.ToolUsesWhenTaken(t)).ToList();
        _output.WriteLine($"uses: {string.Join(", ", before)} -> {string.Join(", ", after)}");
        Assert.Equal(tools.Select(t => world.GoodsCatalog[t].ToolUses * 125 / 100), after);
        Assert.Contains(187, after);
        Assert.Contains(312, after);

        // ⛔ And it is no yield bonus: a forge still makes what it made.
        Assert.Equal(world.Config.ToolsPerForge, world.YieldWithTechnique(JobKind.Smith, world.Config.ToolsPerForge));
    }

    /// <summary>⭐ And at the site: a founder who takes the cart's tool once tempering is known takes it with the quarter.</summary>
    [Fact]
    public void AToolTakenUpOnceTemperingIsKnownComesWithTheQuarter()
    {
        SimConfig config = VillageFixtures.Village; // ToolsTests' village: its trades fetch the cart's tools in the first year
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;
        MakeAMasterOf(world, JobKind.Smith);
        loop.Step(1);
        foreach (Villager v in world.Villagers)
        {
            v.ToolUses = 0;
        }

        int taken = world.ToolsEverTaken;
        for (int i = 0; i < config.TicksPerYear && world.ToolsEverTaken == taken; i++)
        {
            loop.Step(1);
        }

        Assert.True(world.ToolsEverTaken > taken, "The premise: somebody took a tool up.");
        int most = world.Villagers.Max(v => v.ToolUses);
        Assert.True(most > config.ToolUses, $"The tool taken has {most} uses; untempered it has {config.ToolUses}.");
    }

    /// <summary>
    /// ⭐ The herdsman's cut goes through <c>YieldFor</c> now, so good hay reaches the tile he cuts — not only the number.
    /// </summary>
    [Fact]
    public void AHerdsmanWhoseVillageKnowsGoodHayCutsMoreFromATile()
    {
        SimLoop loop = SimFactory.CreatePhase0(ShippedConfig.Established(), new InMemoryLogSink());
        SimWorld world = loop.World;
        Workplace barn = HerdTests.RaiseABarn(world);
        HerdTests.GivePasture(world, barn, 40);
        barn.HerdHead = 4;
        Villager herdsman = world.Villagers.First(v => v.Alive && v.CanWork);
        world.SetPinnedTrade(herdsman, JobKind.Herdsman);
        herdsman.ToolUses = 0;
        MakeAMasterOf(world, JobKind.Herdsman);
        loop.Step(1);
        Assert.Equal(77, world.YieldWithTechnique(JobKind.Herdsman, world.Config.HayPerTile));

        int firstArmful = 0;
        for (int i = 0; i < 2000 && firstArmful == 0; i++)
        {
            loop.Step(1);
            firstArmful = herdsman.Carried[Goods.Hay];
        }

        Assert.Equal(77 * herdsman.Vigour / 100, firstArmful);
    }
}
