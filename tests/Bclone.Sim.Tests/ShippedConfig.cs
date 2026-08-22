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
    /// <summary>Load <c>data/sim.config.json</c> from the repo root, exactly as it ships.</summary>
    public static SimConfig Load() =>
        SimConfigLoader.LoadFromFile(Path.Combine(RepoRoot(), "data", "sim.config.json"));

    /// <summary>
    /// The shipped file's numbers, on a village that has already been built.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What the shipped-config guards were always actually about.</b> They exist because
    /// the hand-typed file drifts from <see cref="VillageFixtures"/>'s derived one, and the
    /// gap is where D48, D49 and D50 lived — a timber leak four times worse in the shipped
    /// file, a calendar that reached the game and not the tests, three woodcutter seats where
    /// the economy needed eight. Every one of those is a <em>number</em>. None of them is
    /// about how the village came to exist.
    /// </para>
    /// <para>
    /// Since D70 the shipped file starts cold, so running those guards against it unmodified
    /// would assert that four people with no houses survive three centuries — a true
    /// statement about nothing, failing for the right reason and testing the wrong thing.
    /// Turning the founding back on keeps them pointed at what they were built to catch.
    /// </para>
    /// <para>
    /// <b>The cold path is not thereby untested:</b> <c>ColdStartTests</c> covers it, and
    /// asserts that the real file still says <c>founding_buildings: false</c> — so this
    /// override cannot quietly become a way of never testing what ships.
    /// </para>
    /// </remarks>
    public static SimConfig Established() => Load() with { FoundingBuildings = true };

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
