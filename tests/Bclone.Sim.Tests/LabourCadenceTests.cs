using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐⭐ How long the village takes to do as it is told — <c>labour_slack_ticks</c> (D200).
/// </summary>
/// <remarks>
/// <para>
/// <b>Joe, playing: *"30 days feels unresponsive."*</b> The pass that fills openings and lets go
/// of a surplus was hard-wired to a season, so a change made on the professions panel waited up
/// to thirty in-game days to bite — measured at <b>25, 15 and 5 days</b> depending on where in
/// the season it was set.
/// </para>
/// <para>
/// <b>⚠️ AND THE MEASUREMENT SAYS THE TRADE IS CHURN FOR RESPONSIVENESS, WITH NOTHING ECONOMIC
/// EITHER WAY.</b> Across three seeds at fifty years, population, food and the share of able
/// adults holding no job are **flat** at 30, 10 and 5 days. What moves is job changes: about
/// **800** at a season, **1,220** at ten days, **1,960** at five. *A faster pass does not get
/// more work done; it obeys sooner.*
/// </para>
/// </remarks>
public sealed class LabourCadenceTests
{
    private readonly ITestOutputHelper _output;

    public LabourCadenceTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Shipped => ShippedConfig.Established();

    private static SimLoop Loop(SimConfig config) =>
        SimFactory.CreatePhase0(config, new InMemoryLogSink());

    private static int Holding(SimWorld world, JobKind kind) => world.Villagers.Count(v =>
        v.Alive && v.HasJob
        && world.FindWorkplace(v.WorkplaceId) is Workplace w && w.Kind == kind);

    // ---------------------------------------------------------------
    //  ⭐⭐ The thing Joe asked for
    // ---------------------------------------------------------------

    /// <summary>⭐⭐ A change on the professions panel bites within the stated cadence.</summary>
    /// <remarks>
    /// <b>Asserted from wherever in the cycle the instruction lands</b>, because that is what the
    /// player experiences: the same change took 25 days set early in a season and 5 days set
    /// late. The claim is the **worst** case, not the lucky one.
    /// </remarks>
    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(27)]
    [InlineData(39)]
    public void TheVillageObeysWithinTheCadence(int offsetTicks)
    {
        SimConfig config = Shipped;
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        // ⚠️ HALF A SEASON PAST THE YEAR EDGE, WHICH IS NOT PADDING (D181's trap, and the first
        // draft of this guard walked straight into it). At *Day 1, Spring* the reshuffle has torn
        // every allocation down and not yet rebuilt it — **0 of 4 able adults hold a job on that
        // exact tick** — so a fixture that starts at `TicksPerYear * n` samples the hole and
        // reports that nobody is foraging.
        for (int i = 0; i < (config.TicksPerYear * 10) + config.TicksPerSeason + offsetTicks; i++)
        {
            loop.StepOnce();
        }

        int before = Holding(world, JobKind.Forager);
        Assert.True(before > 0, "Nobody is foraging, so this measures nothing.");

        world.SetJobLimit(JobKind.Forager, before - 1);

        int took = -1;
        for (int i = 1; i <= config.TicksPerYear; i++)
        {
            loop.StepOnce();
            if (Holding(world, JobKind.Forager) < before)
            {
                took = i;
                break;
            }
        }

        _output.WriteLine(
            $"set {offsetTicks} ticks into the cycle: foragers {before} → "
            + $"{Holding(world, JobKind.Forager)} after "
            + $"{(took < 0 ? "never" : $"{took} ticks = {took / config.TicksPerDay} days")}");

        Assert.True(took > 0, "The village never acted on the instruction at all.");
        Assert.True(
            took <= config.LabourSlackTicks,
            $"The village took {took} ticks to act on an instruction, and the cadence is "
            + $"{config.LabourSlackTicks}. Whatever is deciding when to obey is not "
            + "`labour_slack_ticks`.");
    }

    /// <summary>
    /// ⭐ And the shipped cadence is the one that was measured, in days a person can quote.
    /// </summary>
    /// <remarks>
    /// <b>A guard over the config rather than over the code</b>, in the shape
    /// <c>ShippedConfigTests</c> uses: the number that reaches the game is typed by hand, and
    /// METHODOLOGY §3 records three bugs (D48, D49, D50) that were exactly this drifting.
    /// </remarks>
    [Fact]
    public void TheShippedCadenceIsFifteenDays()
    {
        SimConfig config = Shipped;
        int days = config.LabourSlackTicks / config.TicksPerDay;

        _output.WriteLine($"labour_slack_ticks {config.LabourSlackTicks} = {days} in-game days");
        Assert.Equal(15, days);
    }

    // ---------------------------------------------------------------
    //  ⛔ What must not change, and it is the whole reason this is safe
    // ---------------------------------------------------------------

    /// <summary>
    /// ⛔⛔ A career is still a thing that lasts — the pass must not become the reshuffle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠️ THE FIRST DRAFT OF THIS GUARD ASSERTED SOMETHING THAT WAS NEVER TRUE, AND FINDING
    /// THAT OUT IS THE POINT OF WRITING IT.</b> <c>LabourSystem</c>'s own remarks say the
    /// seasonal pass *"never moves someone who already has a job, so the reason they were given
    /// for holding it stays true until the next reshuffle."* **It does move them.**
    /// <c>ShedSurplus</c> releases a villager the village no longer wants and <c>Match</c> puts
    /// them in an opening **in the same pass**, so from outside it is one move from trade to
    /// trade. Measured at the *old* seasonal cadence: **67, 78 and 83** such moves over fifty
    /// years on three seeds. *The comment was wrong before this slice existed* — D159's
    /// doc-versus-reality drift, found by writing a guard for the claim.
    /// </para>
    /// <para>
    /// <b>⭐ And what it actually does is the feature rather than a fault:</b> *the village wanted
    /// fewer foragers, so Agnes was let go and took the open builder's seat the same day.* That
    /// is precisely what Joe asked to happen sooner.
    /// </para>
    /// <para>
    /// <b>So this guards the rate instead of the absolute</b>, which is the claim worth holding:
    /// D20 rejected a seasonal reshuffle for *"churning jobs faster than a player can read the
    /// reason for holding one"*, and a pass that moved everybody constantly would be that by the
    /// back door. Measured over fifty years — <b>67–83 at thirty days, 103–146 at ten, 182–381 at
    /// five.</b> The bar sits above ten days and below five.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(12345UL)]
    [InlineData(2UL)]
    [InlineData(42UL)]
    public void ACareerStillLastsAtTheShippedCadence(ulong seed)
    {
        SimConfig config = Shipped with { Seed = seed };
        SimLoop loop = Loop(config);
        SimWorld world = loop.World;

        var held = new Dictionary<int, int>();
        int movedTradeToTrade = 0;
        int reshuffleInterval = config.TicksPerYear * config.LabourReshuffleYears;

        for (int i = 0; i < config.TicksPerYear * 50; i++)
        {
            loop.StepOnce();

            // ⚠️ The reshuffle is ALLOWED to move people and runs on its own cadence, so the
            // tick it lands on is skipped — sampling it would have this fail for the one system
            // that is supposed to do this.
            bool reshuffled = world.Tick % (ulong)reshuffleInterval == 0UL;

            foreach (Villager villager in world.Villagers)
            {
                if (!villager.Alive || !villager.CanWork)
                {
                    held.Remove(villager.Id);
                    continue;
                }

                int now = villager.HasJob ? villager.WorkplaceId : 0;

                if (!reshuffled
                    && held.TryGetValue(villager.Id, out int was)
                    && was != 0 && now != 0 && was != now)
                {
                    movedTradeToTrade++;
                }

                held[villager.Id] = now;
            }
        }

        _output.WriteLine(
            $"seed {seed}: {movedTradeToTrade} moves from one trade straight to another over "
            + $"fifty years (measured 67–83 at a seasonal pass, 103–146 at this one)");

        Assert.True(
            movedTradeToTrade < 250,
            $"{movedTradeToTrade} villagers were moved straight from one trade to another outside "
            + "a reshuffle. Measured at 103–146 when this shipped and 182–381 at a five-day pass "
            + "— so this is the slack pass becoming the reshuffle, which is what D20 and D46 both "
            + "refused.");
    }

    /// <summary>
    /// ⚠️ A config that never heard of the key keeps the cadence it was measured at.
    /// </summary>
    /// <remarks>
    /// <b>The fixture villages predate this key</b>, and every number in <c>VillageFixtures</c>
    /// was measured against a seasonal pass. Defaulting to a season rather than requiring the key
    /// is what stops this slice moving numbers nobody asked it to touch.
    /// </remarks>
    [Fact]
    public void AConfigWithoutTheKeyStillRunsItSeasonally()
    {
        SimConfig config = VillageFixtures.Village;

        _output.WriteLine(
            $"the fixture says {config.LabourSlackTicks}, a season is {config.TicksPerSeason}");

        Assert.Equal(config.TicksPerSeason, config.LabourSlackTicks);
    }
}
