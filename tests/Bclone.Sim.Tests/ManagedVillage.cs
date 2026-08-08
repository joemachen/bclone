using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;

namespace Bclone.Sim.Tests;

/// <summary>
/// A village with somebody staffing it — the suite's stand-in for a player (D109).
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐ Why this exists.</b> D109 retired auto-staffing: every workplace carries an explicit
/// number and the founders arrive as laborers, so <b>a village nobody manages does nothing at
/// all and dies.</b> That is the design, and it makes almost every guard in this suite assert
/// something false — <c>MarketTests</c>, <c>FirewoodTests</c>, <c>ShippedConfigTests</c> and
/// forty more are about a village that <em>runs</em>, and none of them are about staffing.
/// </para>
/// <para>
/// <b>⭐ And it is emphatically NOT the "scripted competent player" Joe declined.</b> That
/// would mark buildings, paint land and react to trouble — decisions. This does exactly one
/// thing: it takes <see cref="LabourQuota"/>'s advice, which the village has always computed
/// and still computes, and puts those numbers on the buildings. <b>It is the auto-staffing
/// that was just retired, moved out of the sim and into the harness</b>, which is the honest
/// place for it — the shipped game is fully manual, and the tests say out loud that they are
/// being played.
/// </para>
/// <para>
/// <b>The alternative was a config flag</b>, and it was refused: the tested game would then
/// differ from the shipped game on the single most load-bearing behaviour there is, which is
/// precisely the gap METHODOLOGY §3 exists to close and where D48, D49 and D50 each lived.
/// One sim behaviour, and a harness that plays it.
/// </para>
/// <para>
/// <b>It also means the long-horizon net is NOT thinner than D109 predicted.</b> Those runs
/// keep their old assertions under management, and gain a second arm asserting what an
/// unmanaged village does — which is a real and different question (see
/// <c>UnmanagedVillageTests</c>).
/// </para>
/// </remarks>
public static class ManagedVillage
{
    /// <summary>A loop whose village is staffed to the village's own advice, every pass.</summary>
    public static SimLoop Loop(
        SimConfig config, ISimLogger? logger = null, ulong? seedOverride = null)
    {
        SimWorld world = SimWorld.Create(config, logger, seedOverride);
        return new SimLoop(world, Systems());
    }

    /// <summary>The canonical order, with the stand-in player immediately before labour.</summary>
    /// <remarks>
    /// <b>Before <c>LabourSystem</c>, not after</b>, so the numbers are on the buildings when
    /// the allocator reads them. After it, every change would land a pass late and the village
    /// would run a season behind its own advice forever.
    /// </remarks>
    private static IReadOnlyList<ISimSystem> Systems()
    {
        var systems = new List<ISimSystem>();
        foreach (ISimSystem system in SimFactory.CreatePhase0Systems())
        {
            if (system is Systems.LabourSystem)
            {
                systems.Add(new StaffsToTheVillagesAdvice());
            }

            systems.Add(system);
        }

        return systems;
    }

    /// <summary>Staff every profession to what the village would have chosen for itself.</summary>
    private sealed class StaffsToTheVillagesAdvice : ISimSystem
    {
        public string Name => "staffing (test harness)";

        public void Execute(SimWorld world)
        {
            ArgumentNullException.ThrowIfNull(world);

            // On the labour cadence, not every tick: staffing is a decision a person makes
            // occasionally, and re-running it per tick would churn the allocator into
            // meaninglessness — which is the reason LabourSystem has a cadence at all.
            SimConfig config = world.Config;
            ulong reshuffle = (ulong)config.TicksPerYear * (ulong)config.LabourReshuffleYears;
            if (world.Tick % reshuffle != 0UL && world.Tick % (ulong)config.TicksPerSeason != 0UL)
            {
                return;
            }

            LabourQuota advice = LabourQuota.For(world);
            foreach (JobKind kind in Enum.GetValues<JobKind>())
            {
                // Quietly — this is a stand-in for somebody clicking, and a hundred years of
                // its clicking in the village log would bury the story the log is for (D9).
                world.DistributeStaffing(kind, advice.For(kind));
            }
        }
    }
}
