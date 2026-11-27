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
        SimConfig without = with with { Skills = with.Skills.Where(s => s.Id <= 11).ToList() };
        Assert.True(with.Skills.Count > without.Skills.Count, "The premise: the shipped skills include W5's rows.");

        SimWorld a = SimFactory.CreatePhase0(with, new InMemoryLogSink()).World;
        SimWorld b = SimFactory.CreatePhase0(without, new InMemoryLogSink()).World;
        Assert.Equal(StateHash.Compute(b), StateHash.Compute(a));
    }
}
