using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

// ⛔ D756's MEASUREMENT HARNESS — kept in tools/harness/, COPIED into tests/Kith.Sim.Tests/ to run and DELETED from there
// before any commit (D420). `school-and-education.md §12` step 3: what a school costs a village and what A and B return,
// measured before its sizes are typed. WALK — the able adults' years walking and carrying (D761: what fuller loads return).
//
// Arms: ZZ_SCHOOL=1 poses the school learned at Year 10 and marks one beside the founding (its stone given — no harness
// village holds stone, ZzWorkshop's lesson), the player's teachers ZZ_TEACHERS (1) asked for once it stands; unset is the
// baseline, no school. ZZ_OLDAGE = school_old_age_percent, ZZ_CARRY = school_carry_percent (D761), ZZ_HEED = school_heed_percent (all 0 shipped); ZZ_START and
// ZZ_LEAVE = school_starting_age and school_leaving_age (D758's two-year term, 11 and 13). The opening is
// ZzBase's — the shipped cold start, the opening played, a granary and a warehouse at Year 3 — on seeds 200–299
// (ZZ_SEEDS=a,b narrows), a century (ZZ_YEARS). ZZ_LABEL names the arm.
//
// ZZ_STATES=1 adds a ZZT line a run: the able adults' ticks by what they were doing — the village's time budget (D758),
// where a benefit could be worth something. `school.py --states <file>` sums it.
//
// One ZZS line a run: alive, peak, starved, cold, born; the year the school stood (−1 never); pupil-years served
// (the labour the village gave up) and graduates (half a term or more); STRONG years — villager-years at full vigour past
// vigour_full_until_age, what A returns; DANGER — villager-days at or past 90 % of the way to freezing (caught out), what B returns;
// SHELTER — villager-days going in to get warm (the winter's lost work); adult work-years (able, not a pupil); and the
// same weighted by vigour — the work a village's strength is actually worth (A's real return: a 33-year-old is ~96 % strong).
//
// Copy in; `dotnet test tests/Kith.Sim.Tests -c Release --filter ZzSchool --logger "console;verbosity=detailed"` per
// arm; delete.
public sealed class ZzSchool
{
    private readonly ITestOutputHelper _o;

    public ZzSchool(ITestOutputHelper o) => _o = o;

    [Theory]
    [MemberData(nameof(Runs))]
    public void Measure(ulong seed)
    {
        bool school = Environment.GetEnvironmentVariable("ZZ_SCHOOL") == "1";
        int teachers = int.Parse(Environment.GetEnvironmentVariable("ZZ_TEACHERS") ?? "1");
        int oldAge = int.Parse(Environment.GetEnvironmentVariable("ZZ_OLDAGE") ?? "0");
        int carry = int.Parse(Environment.GetEnvironmentVariable("ZZ_CARRY") ?? "0");
        int heed = int.Parse(Environment.GetEnvironmentVariable("ZZ_HEED") ?? "0");
        int years = int.Parse(Environment.GetEnvironmentVariable("ZZ_YEARS") ?? "100");
        SimConfig shipped = ShippedConfig.Load();
        int start = int.Parse(Environment.GetEnvironmentVariable("ZZ_START") ?? shipped.SchoolStartingAge.ToString(System.Globalization.CultureInfo.InvariantCulture));
        int leave = int.Parse(Environment.GetEnvironmentVariable("ZZ_LEAVE") ?? shipped.SchoolLeavingAge.ToString(System.Globalization.CultureInfo.InvariantCulture));
        string arm = Environment.GetEnvironmentVariable("ZZ_LABEL") ?? (school ? $"s{teachers}o{oldAge}c{carry}h{heed}" : "base");

        SimConfig config = shipped with
        {
            Seed = seed, SchoolOldAgePercent = oldAge, SchoolCarryPercent = carry, SchoolHeedPercent = heed, SchoolStartingAge = start, SchoolLeavingAge = leave,
        };
        SimLoop loop = SimFactory.CreatePhase0(config, new Kith.Sim.Logging.InMemoryLogSink());
        SimWorld world = loop.World;
        ColdStartTests.PlayTheOpening(world);

        int peak = 0, stoodYear = -1;
        long strongTicks = 0, dangerTicks = 0, shelterTicks = 0, workTicks = 0, strongWork = 0, walkTicks = 0;
        bool[] walking = Enum.GetValues<VillagerState>().Select(s => s.ToString() is var n
            && (n.StartsWith("Traveling", StringComparison.Ordinal) || n.StartsWith("Hauling", StringComparison.Ordinal)
                || n.StartsWith("Fetching", StringComparison.Ordinal) || n.StartsWith("Walking", StringComparison.Ordinal))).ToArray();
        bool staffed = false;
        bool states = Environment.GetEnvironmentVariable("ZZ_STATES") == "1";
        var stateTicks = new long[Enum.GetValues<VillagerState>().Length + 1];
        int danger = config.ExposureThreshold * 90 / 100;
        for (int t = 0; t < config.TicksPerYear * years; t++)
        {
            if (t == config.TicksPerYear * 3)
            {
                MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 8);
                MarkSomewhereNear(world, BuildingKind.Warehouse, world.Map.FoundingSite, 8);
            }

            if (school && t == config.TicksPerYear * 10)
            {
                // Posed learned (an unattended village never marks the gifted library), then marked and paid for as a player would.
                world.LearnTheSchool();
                MarkSomewhereNear(world, BuildingKind.School, world.Map.FoundingSite, 9);
                Workplace? marked = world.Workplaces.FirstOrDefault(w => w.Construction is { IsFinished: false } c && c.Kind == BuildingKind.School);
                marked?.Construction!.Deliver(Goods.Stone, marked.Construction.StillNeeded(Goods.Stone));
            }

            loop.StepOnce();
            peak = Math.Max(peak, world.Population);

            if (school && !staffed && world.Workplaces.Any(w => w.Kind == JobKind.Teacher && !w.IsSite))
            {
                stoodYear = t / config.TicksPerYear;
                world.SetJobLimit(JobKind.Teacher, teachers);
                staffed = true;
            }

            foreach (Villager v in world.Villagers)
            {
                if (!v.Alive)
                {
                    continue;
                }

                if (states && v.CanTakeAJob)
                {
                    stateTicks[Math.Min((int)v.State, stateTicks.Length - 1)]++;
                }

                if (v.CanTakeAJob)
                {
                    workTicks++;
                    if ((int)v.State < walking.Length && walking[(int)v.State])
                    {
                        walkTicks++;
                    }
                    strongWork += v.Vigour;
                }

                if (v.Vigour >= 100 && v.AgeYears > config.VigourFullUntilAge)
                {
                    strongTicks++;
                }

                if (v.Cold >= danger && danger > 0)
                {
                    dangerTicks++;
                }

                if (v.State == VillagerState.SeekingShelter)
                {
                    shelterTicks++;
                }
            }
        }

        int starved = world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Starvation);
        int cold = world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == CauseOfDeath.Cold);
        int born = world.Villagers.Count(v => v.BirthTick > 0);
        long pupilTicks = world.Villagers.Sum(v => (long)v.SchoolTicks);
        int graduates = world.Villagers.Count(v => v.SchoolTicks * 2 >= config.SchoolTermTicks && config.SchoolTermTicks > 0);
        int perDay = config.TicksPerDay;
        _o.WriteLine(
            $"ZZS {arm} {seed} alive {world.Population} peak {peak} starved {starved} cold {cold} born {born} "
            + $"stood {stoodYear} pupilyears {pupilTicks / config.TicksPerYear} graduates {graduates} "
            + $"strongyears {strongTicks / config.TicksPerYear} dangerdays {dangerTicks / perDay} shelterdays {shelterTicks / perDay} "
            + $"workyears {workTicks / config.TicksPerYear} vigourworkyears {strongWork / 100 / config.TicksPerYear} walkyears {walkTicks / config.TicksPerYear}");
        if (states)
        {
            WriteStates(arm, seed, stateTicks);
        }
    }

    private void WriteStates(string arm, ulong seed, long[] stateTicks)
    {
        _o.WriteLine($"ZZT {arm} {seed} " + string.Join(" ", Enum.GetValues<VillagerState>()
            .Where(s => stateTicks[Math.Min((int)s, stateTicks.Length - 1)] > 0)
            .Select(s => $"{s}:{stateTicks[Math.Min((int)s, stateTicks.Length - 1)]}")));
    }

    private static void MarkSomewhereNear(SimWorld world, BuildingKind kind, GridPos site, int radius)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (world.Mark(kind, new GridPos(site.X + dx, site.Y + dy)).Allowed)
                {
                    return;
                }
            }
        }
    }

    public static IEnumerable<object[]> Runs()
    {
        IEnumerable<int> seeds = Enumerable.Range(200, 100);
        if (Environment.GetEnvironmentVariable("ZZ_SEEDS") is string listed)
        {
            seeds = listed.Split(',').Select(int.Parse);
        }

        foreach (int s in seeds)
        {
            yield return new object[] { (ulong)s };
        }
    }
}
