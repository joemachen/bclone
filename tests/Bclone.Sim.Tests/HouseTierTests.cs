using System.Text.Json.Nodes;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.Persistence;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// House tiers (`specs/house-tiers.md`, D716–D718). <b>Piece 1, the generation</b> (§14.1): the family tree that gates
/// both rungs (§4.4 idea A, Joe: <i>"I love the family tree unlock idea!"</i>) needs a villager to know which generation
/// in the valley they are — founders and arriving nomads the first, a child one past the elder of its two parents.
/// </summary>
public sealed class HouseTierTests
{
    private readonly ITestOutputHelper _output;

    public HouseTierTests(ITestOutputHelper output) => _output = output;

    private static SimConfig Village => VillageFixtures.Village;

    /// <summary>Everybody the founding puts in the valley is its first generation.</summary>
    [Fact]
    public void TheFoundersAreTheFirstGeneration()
    {
        foreach (SimConfig config in new[] { Village, ShippedConfig.Load() })
        {
            SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;
            Assert.NotEmpty(world.Villagers);
            Assert.All(world.Villagers, v => Assert.Equal(1, v.Generation));
        }
    }

    /// <summary>
    /// ⭐ Every child born in sixty fixture years is one generation past the elder of the couple it was born to, read off the
    /// household the tick before — and the run reaches the founders' great-grandchildren, so a rule that only ever wrote 2
    /// (a child of founders) cannot pass.
    /// </summary>
    [Fact]
    public void AChildIsOneGenerationPastTheElderOfItsParents()
    {
        SimLoop loop = SimFactory.CreatePhase0(Village with { StartingHouseholds = 3 }, new InMemoryLogSink());
        SimWorld world = loop.World;
        int births = 0;
        int deepest = 1;
        int ticks = 60 * world.Config.TicksPerYear;
        for (int t = 0; t < ticks; t++)
        {
            Dictionary<int, int> eldest = ElderParentGenerationByHousehold(world);
            int before = world.Villagers.Count;
            loop.StepOnce();
            for (int i = before; i < world.Villagers.Count; i++)
            {
                Villager child = world.Villagers[i];
                if (child.ArrivedAtTick != 0UL || child.Founder)
                {
                    Assert.Equal(1, child.Generation);
                    continue;
                }

                Assert.True(eldest.TryGetValue(child.HouseholdId, out int parents),
                    $"{child.FullName} was born to a household with no couple the tick before");
                Assert.Equal(parents + 1, child.Generation);
                births++;
                deepest = Math.Max(deepest, child.Generation);
            }
        }

        _output.WriteLine($"{births} births in sixty years; the deepest generation born is {deepest}");
        Assert.True(births > 0, "the pose needs births");
        Assert.True(deepest >= 4, $"the pose needs the founders' great-grandchildren (deepest was {deepest})");
    }

    /// <summary>
    /// Saved and loaded with the villager; ⛔ not hashed — a statistic the gates count, D676's shape, so no golden moves for
    /// it. The gap is asserted here so nobody "fixes" it unread: two villages differing only in a generation hash alike.
    /// </summary>
    [Fact]
    public void TheGenerationIsSavedAndNotHashed()
    {
        SimLoop loop = SimFactory.CreatePhase0(Village with { StartingHouseholds = 3 }, new InMemoryLogSink());
        loop.Step(30 * loop.World.Config.TicksPerYear);
        Villager child = loop.World.Villagers.First(v => v.Generation >= 2);

        JsonObject document = SaveGame.Capture(loop, "test", "2026-11-08 12:00", "test");
        SimLoop loaded = SaveGame.Load(JsonNode.Parse(SaveFile.TextOf(document))!.AsObject(), loop.World.Config, null);
        Assert.Equal(
            loop.World.Villagers.Select(v => v.Generation),
            loaded.World.Villagers.Select(v => v.Generation));

        ulong before = StateHash.Compute(loop.World);
        child.Generation += 7;
        Assert.Equal(before, StateHash.Compute(loop.World));
    }

    /// <summary>
    /// Format 13 since the generation (`house-tiers.md §7`): a format-12 save — Joe's Elderwood and Stonebridge, 2026-11-05 to
    /// 11-08 — is refused naming its build, never half-loaded with every villager the first generation.
    /// </summary>
    [Fact]
    public void AFormatTwelveSaveIsRefused()
    {
        SimConfig config = Village;
        SimLoop loop = SimFactory.CreatePhase0(config, new InMemoryLogSink());
        Assert.Equal(13, SaveGame.Format);

        string folder = Path.Combine(Path.GetTempPath(), "bclone-house-tier-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            JsonObject document = SaveGame.Capture(loop, "0.0.1", "2026-11-08 12:00", NewGame.ShareCode(
                config, NewGame.Defaults(config, loop.World.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture))));
            document["format"] = 12;
            string path = Path.Combine(folder, $"format-twelve{SaveFile.Extension}");
            SaveFile.Write(path, document);
            SaveOpened opened = SaveFile.Open(path, config);
            Assert.Null(opened.Loop);
            Assert.Equal("Saved by build 0.0.1, whose saves this build cannot read.", opened.Refusal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>The elder generation of each household's living, paired, fertile-age couple — the parents of a birth.</summary>
    private static Dictionary<int, int> ElderParentGenerationByHousehold(SimWorld world)
    {
        var eldest = new Dictionary<int, int>();
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager v = world.Villagers[i];
            if (!v.Alive || !v.IsPaired)
            {
                continue;
            }

            Villager? partner = world.FindVillager(v.PartnerId);
            if (partner is null || !partner.Alive || partner.HouseholdId != v.HouseholdId)
            {
                continue;
            }

            int pair = Math.Max(v.Generation, partner.Generation);
            eldest[v.HouseholdId] = eldest.TryGetValue(v.HouseholdId, out int had) ? Math.Max(had, pair) : pair;
        }

        return eldest;
    }
}
