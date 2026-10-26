using Bclone.Sim.Core;
using Bclone.Sim.Persistence;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

// ⛔ D623's MEASUREMENT — `specs/doors.md §5`'s fourth arm: Joe's own villages, stepped on, before and after doors. Kept in
// tools/harness/, COPIED into tests/Bclone.Sim.Tests/ to run and DELETED from there before any commit (D420). A harness
// never turns a building and never raises a market or a smithy (D447, D601, D617); his saves have all of that.
// Run in two worktrees (the parent and the change), on a folder of COPIES of his saves (`SaveFile.Open` keeps a damaged
// file as .bad beside it):
//   ZZ_SAVES=<folder> ZZ_LABEL=<old|new> [ZZ_YEARS=20] dotnet test tests/Bclone.Sim.Tests -c Release
//     --filter "FullyQualifiedName~ZzDoorSaves" --logger "console;verbosity=detailed"
// One ZZS line a save: alive at load and after, the deaths since load by cause, births since load.
public sealed class ZzDoorSaves
{
    private readonly ITestOutputHelper _o;

    public ZzDoorSaves(ITestOutputHelper o) => _o = o;

    public static IEnumerable<object[]> Saves() =>
        Directory.GetFiles(Environment.GetEnvironmentVariable("ZZ_SAVES")!, "*.save").Select(p => new object[] { p });

    [Theory]
    [MemberData(nameof(Saves))]
    public void Step(string path)
    {
        SaveOpened opened = SaveFile.Open(path, ShippedConfig.Load(), new Bclone.Sim.Logging.InMemoryLogSink());
        Assert.True(opened.Loop is not null, opened.Detail);
        SimLoop loop = opened.Loop!;
        SimWorld world = loop.World;
        string label = Environment.GetEnvironmentVariable("ZZ_LABEL") ?? "arm";
        int years = int.TryParse(Environment.GetEnvironmentVariable("ZZ_YEARS"), out int y) ? y : 20;

        int aliveAtLoad = world.Population;
        int villagersAtLoad = world.Villagers.Count;
        int starvedAtLoad = Dead(world, CauseOfDeath.Starvation);
        int coldAtLoad = Dead(world, CauseOfDeath.Cold);
        int deadAtLoad = world.Villagers.Count(v => !v.Alive);

        loop.Step(world.Config.TicksPerYear * years);

        _o.WriteLine(
            $"ZZS {label} {Path.GetFileNameWithoutExtension(path)} alive {aliveAtLoad} -> {world.Population} "
            + $"starved {Dead(world, CauseOfDeath.Starvation) - starvedAtLoad} cold {Dead(world, CauseOfDeath.Cold) - coldAtLoad} "
            + $"died {world.Villagers.Count(v => !v.Alive) - deadAtLoad} born {world.Villagers.Count - villagersAtLoad}");
    }

    private static int Dead(SimWorld world, CauseOfDeath cause) =>
        world.Villagers.Count(v => !v.Alive && v.CauseOfDeath == cause);
}
