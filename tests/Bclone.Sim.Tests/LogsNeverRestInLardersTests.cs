using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// Timber may never come to rest in a household's larder.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the third time this project has shipped the same bug</b>, so it gets an
/// invariant rather than another fix. D25: the village cut wood year after year and
/// piled it in the woodcutter's own house where it could not be spent, and no home was
/// ever built. D29: the same thing with firewood. D30 was supposed to close the class
/// for good by moving goods into buildings — and it did not, because
/// <c>UnloadAtHome</c> still accepted an armful of logs from anyone whose trip was
/// interrupted.
/// </para>
/// <para>
/// <b>What makes it so quiet is that it does not look like a goods bug.</b> Nobody
/// starves and nobody freezes. The village simply cannot build, so no couple ever moves
/// out, so no new household forms, and the settlement ages out with a full granary —
/// which reads exactly like a demographic wave. That is the same disguise D34 wore.
/// </para>
/// <para>
/// The rule is one sentence: <b>nothing in the sim can spend a log that is sitting in a
/// larder</b> — the only code that reads <c>Household.Stockpile.Logs</c> is the state
/// hash — so any log that lands there is dead for the rest of the run.
/// </para>
/// </remarks>
public sealed class LogsNeverRestInLardersTests
{
    private readonly ITestOutputHelper _output;

    public LogsNeverRestInLardersTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void NoHouseholdEverHoldsLogs()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        int worstSeen = 0;
        int worstYear = 0;

        for (int year = 1; year <= 300; year++)
        {
            loop.Step(config.TicksPerYear);

            for (int i = 0; i < world.Households.Count; i++)
            {
                int logs = world.Households[i].Stockpile.Logs;
                if (logs > worstSeen)
                {
                    worstSeen = logs;
                    worstYear = year;
                }
            }
        }

        _output.WriteLine($"300 years; most logs ever held in a larder: {worstSeen}.");

        Assert.True(
            worstSeen == 0,
            $"A household was holding {worstSeen} logs in year {worstYear}. Nothing can spend " +
            "those — they are stranded for the rest of the run, and a village that cannot " +
            "build stops forming households and ages out with a full granary (D25, D30).");
    }

    /// <summary>
    /// The guard above must be able to fail. Anti-vacuity, per D7.
    /// </summary>
    /// <remarks>
    /// A test asserting "no logs in larders" would stay green forever if logs simply
    /// stopped being cut, or if households stopped existing, or if the run were too
    /// short for anyone's trip to be interrupted. So prove the village really is moving
    /// timber about during the window the invariant is checked over.
    /// </remarks>
    [Fact]
    public void TheVillageIsActuallyMovingTimberWhileWeWatch()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        loop.Step(config.TicksPerYear * 60);

        int felled = world.LifetimeLogsFelled();
        _output.WriteLine($"60 years; {felled} logs felled, {world.LogsInWarehouses()} in warehouses now.");

        Assert.True(
            felled > 0,
            "No logs were felled at all in sixty years, so the invariant above is vacuous.");
    }
}
