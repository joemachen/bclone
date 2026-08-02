using Bclone.Sim.Config;

namespace Bclone.Sim.Tests;

/// <summary>
/// The config file the game actually loads, for tests that must assert against it.
/// </summary>
/// <remarks>
/// <b>One loader, not one per test class.</b> METHODOLOGY §3 requires anything the economy
/// depends on to be guarded against the shipped file as well as against
/// <see cref="VillageFixtures"/> — the gap between the two is where D48, D49 and D50 all
/// lived — so more than one fixture needs to open it. The path walk was already copied
/// once; a rule that exists twice is one that can be corrected in one place and not the
/// other, which is this project's most repeated bug (D57).
/// </remarks>
public static class ShippedConfig
{
    /// <summary>Load <c>data/sim.config.json</c> from the repo root.</summary>
    public static SimConfig Load() =>
        SimConfigLoader.LoadFromFile(Path.Combine(RepoRoot(), "data", "sim.config.json"));

    /// <summary>The directory holding <c>bclone.sln</c>, walked up from the test binaries.</summary>
    public static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "bclone.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the repo root.");
    }
}
