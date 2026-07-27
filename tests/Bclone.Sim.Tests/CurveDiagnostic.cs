using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>TEMPORARY measurement scaffold — delete before committing.</summary>
public sealed class CurveDiagnostic
{
    private readonly ITestOutputHelper _output;

    public CurveDiagnostic(ITestOutputHelper output) => _output = output;

    [Fact]
    public void WhyNoFirewood()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());

        _output.WriteLine($"per-household: store wanted {VillageEconomy.FirewoodStoreWantedPerHousehold(config)}, "
            + $"winter burn {VillageEconomy.FirewoodPerHouseholdPerWinter(config)}; "
            + $"one woodcutter makes {VillageEconomy.FirewoodMadePerYearAtWorst(config)}/yr");
        _output.WriteLine("year pop  hh occupied perHouse | totalFw  needed  wants | shedFw homeFw | cold");

        for (int year = 1; year <= 110; year++)
        {
            loop.Step(config.TicksPerYear);
            SimWorld w = loop.World;

            int occupied = 0;
            foreach (Household h in w.Households)
            {
                if (w.LivingMembersOf(h) > 0) occupied++;
            }

            int homeFw = 0;
            foreach (Household h in w.Households) homeFw += h.Stockpile.Firewood;

            int needed = (occupied * VillageEconomy.FirewoodStoreWantedPerHousehold(config))
                + (occupied * VillageEconomy.FirewoodPerHouseholdPerWinter(config));

            int cold = 0;
            foreach (Villager v in w.Villagers)
            {
                if (v.CauseOfDeath == CauseOfDeath.Cold) cold++;
            }

            if (year % 10 == 0 || (year >= 90 && year <= 105))
            {
                _output.WriteLine(
                    $"{year,4} {w.Population,3} {w.Households.Count,3} {occupied,8} " +
                    $"{(occupied == 0 ? 0 : w.Population * 10 / occupied) / 10.0,8:F1} | " +
                    $"{w.TotalFirewood(),7} {needed,7} {LabourQuota.WoodcuttersWanted(w),6} | " +
                    $"{w.StorageShed.Store.Firewood,6} {homeFw,6} | {cold,4}");
            }
        }
    }
}
