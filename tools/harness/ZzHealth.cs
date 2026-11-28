using Kith.Sim.Config;
using Kith.Sim.Core;
using Kith.Sim.Persistence;
using Kith.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Kith.Sim.Tests;

// ⛔ D941's MEASUREMENT HARNESS — kept in tools/harness/, COPIED into tests/Kith.Sim.Tests/ to run and DELETED from there
// before any commit (D420). `specs/health.md §9` step 6: what piece 1 does to a village — who is told, who runs down, the days
// in bed, and whether anyone more dies — health on against off (ZZ_HEALTH=0 sets health_plain_percent 0).
//
//   OnSaves  — ZZ_SAVES (a folder of COPIES of Joe's saves, the newest .save in each sub-folder), ZZ_YEARS (5) on.
//   OnPlayed — the played opening on the shipped file (forage only: an unattended village marks no fishery), a granary and a
//              warehouse at Year 3, seeds ZZ_SEEDS (1–24), ZZ_YEARS (30).
// One ZZH line a run: alive at the start and the end, deaths by cause, people told (a plain diet reached), run down at the end and
// ever, people who took to bed and the person-days in bed, and the two moments' counts.
//
// Copy in; `dotnet test tests/Kith.Sim.Tests -c Release --filter ZzHealth --logger "console;verbosity=detailed"`; delete.
public sealed class ZzHealth
{
    private readonly ITestOutputHelper _o;

    public ZzHealth(ITestOutputHelper o) => _o = o;

    private static SimConfig Arm(SimConfig c) =>
        Environment.GetEnvironmentVariable("ZZ_HEALTH") == "0" ? c with { HealthPlainPercent = 0 } : c;

    private static string Label => Environment.GetEnvironmentVariable("ZZ_HEALTH") == "0" ? "off" : "on";

    [Theory]
    [MemberData(nameof(Saves))]
    public void OnSaves(string folder)
    {
        string? root = Environment.GetEnvironmentVariable("ZZ_SAVES");
        if (root is null) { return; }
        int years = int.Parse(Environment.GetEnvironmentVariable("ZZ_YEARS") ?? "5");
        string file = Directory.GetFiles(Path.Combine(root, folder), "*.save").OrderByDescending(File.GetLastWriteTimeUtc).First();
        SaveOpened opened = SaveFile.Open(file, Arm(ShippedConfig.Load()));
        if (opened.Loop is null) { return; }
        if (opened.Loop.World.Clock.Year < 2 || opened.Loop.World.Population < 5) { return; }
        Measure(folder, opened.Loop, years, markAt: 0);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void OnPlayed(int seed)
    {
        if (Environment.GetEnvironmentVariable("ZZ_PLAYED") != "1") { return; }
        int years = int.Parse(Environment.GetEnvironmentVariable("ZZ_YEARS") ?? "30");
        SimLoop loop = SimFactory.CreatePhase0(Arm(ShippedConfig.Load()) with { Seed = (ulong)seed }, new Kith.Sim.Logging.InMemoryLogSink());
        ColdStartTests.PlayTheOpening(loop.World);
        Measure($"seed{seed}", loop, years, markAt: 3);
    }

    private void Measure(string label, SimLoop loop, int years, int markAt)
    {
        SimWorld world = loop.World;
        SimConfig config = world.Config;
        int y0 = world.Clock.Year, alive0 = world.Population, dead0 = world.Villagers.Count(v => !v.Alive);
        var dead0Ids = world.Villagers.Where(v => !v.Alive).Select(v => v.Id).ToHashSet();
        var told = new HashSet<int>();
        var everRunDown = new HashSet<int>();
        var tookToBed = new HashSet<int>();
        long bedTicks = 0;
        int illnesses = 0;
        var wasSick = new Dictionary<int, int>();
        int moments0 = world.Moments.Count;

        for (int t = 0; t < config.TicksPerYear * years; t++)
        {
            if (markAt > 0 && t == config.TicksPerYear * markAt)
            {
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Granary, world.Map.FoundingSite, 8);
                ColdStartTests.MarkSomewhereNear(world, BuildingKind.Warehouse, world.Map.FoundingSite, 8);
            }

            loop.StepOnce();
            foreach (Villager v in world.Villagers)
            {
                if (!v.Alive) { continue; }
                if (v.PlainSeasons >= config.HealthPlainDietSeasons) { told.Add(v.Id); }
                if (v.PlainSeasons >= config.HealthRunDownSeasons) { everRunDown.Add(v.Id); }
                if (v.State == VillagerState.SickInBed) { bedTicks++; tookToBed.Add(v.Id); }
                int was = wasSick.GetValueOrDefault(v.Id);
                if (v.SickDays > 0 && was == 0) { illnesses++; }
                wasSick[v.Id] = v.SickDays;
            }
        }

        var died = world.Villagers.Where(v => !v.Alive && !dead0Ids.Contains(v.Id) && v.State != VillagerState.Left).ToList();
        int left = world.Villagers.Count(v => v.State == VillagerState.Left && !dead0Ids.Contains(v.Id));
        var newMoments = world.Moments.Skip(moments0).ToList();
        _o.WriteLine($"ZZH {Label} {label} Y{y0}+{years} alive {alive0}->{world.Population} new {world.Villagers.Count - dead0 - alive0} "
            + $"died old {died.Count(v => v.CauseOfDeath == CauseOfDeath.OldAge)} starved {died.Count(v => v.CauseOfDeath == CauseOfDeath.Starvation)} "
            + $"cold {died.Count(v => v.CauseOfDeath == CauseOfDeath.Cold)} left {left} told {told.Count} rundownEver {everRunDown.Count} "
            + $"rundownNow {world.RunDownCount()} illnesses {illnesses} tookToBed {tookToBed.Count} bedDays {bedTicks / config.TicksPerDay} "
            + $"plainMoments {newMoments.Count(m => m.Title == "A plain diet")} rundownMoments {newMoments.Count(m => m.Title == "Run down")}");
    }

    public static IEnumerable<object[]> Saves()
    {
        string? root = Environment.GetEnvironmentVariable("ZZ_SAVES");
        if (root is null) { return new[] { new object[] { "none" } }; }
        return Directory.GetDirectories(root).Select(d => new object[] { Path.GetFileName(d) });
    }

    public static IEnumerable<object[]> Seeds()
    {
        IEnumerable<int> seeds = Enumerable.Range(1, 24);
        if (Environment.GetEnvironmentVariable("ZZ_SEEDS") is string listed)
        {
            seeds = listed.Split(',').Select(int.Parse);
        }

        return seeds.Select(s => new object[] { s });
    }
}
