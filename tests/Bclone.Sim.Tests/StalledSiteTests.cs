using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// A building the village has marked out but cannot raise says so — 2026-08-27.
/// </summary>
/// <remarks>
/// <para>
/// <b>⛔⛔ JOE'S SECOND GRANARY WAS MARKED OUT IN WINTER, YEAR 23 AND WAS STILL A SITE AT
/// YEAR 44.</b> Twenty-one years, in total silence, while three houses went up around it. It
/// needed 40 logs and 10 stone, the village had no stone, and nothing in the game ever said
/// that the thing it was waiting for was not coming.
/// </para>
/// <para>
/// <b>⭐ The inspector was not silent — it was worse than silent, it was reassuring.</b> It read
/// <em>"Materials: still wants 10 stone"</em> every year for twenty-one years, which is a true
/// sentence that describes a building arriving shortly. **A building that is never coming has to
/// be distinguishable from one that is coming slowly**, and that is §1.1 rather than polish.
/// </para>
/// <para>
/// The condition is deliberately <b>"the village cannot otherwise get this"</b> — the site still
/// wants the material and no store anywhere holds any of it — which is the same test
/// <c>NearestHarvest</c>'s <c>waitedOn</c> already makes. A site merely waiting on a builder's
/// legs is not stalled, and saying so would be a nag the player learns to click past (D42).
/// </para>
/// </remarks>
public sealed class StalledSiteTests
{
    private readonly ITestOutputHelper _output;

    public StalledSiteTests(ITestOutputHelper output) => _output = output;

    /// <summary>A buildable tile near the village with nothing standing on it.</summary>
    private static GridPos ClearGroundNear(SimWorld world, BuildingKind kind)
    {
        GridPos site = world.Map.FoundingSite;
        for (int radius = 1; radius < 12; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var at = new GridPos(site.X + dx, site.Y + dy);
                    if (!world.HasSomethingToHarvest(at) && world.CanBuildAt(kind, at).Allowed)
                    {
                        return at;
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException("No clear ground near the founding site.");
    }

    private static Workplace SiteFor(SimWorld world, BuildingKind kind) =>
        Assert.Single(world.Workplaces, place => place.Construction?.Kind == kind);

    /// <summary>⭐ A site short of a material nothing holds says what to do about it.</summary>
    [Fact]
    public void ASiteWaitingOnAMaterialTheVillageCannotGetSaysSo()
    {
        SimConfig config = VillageFixtures.Village;
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;

        GridPos at = ClearGroundNear(world, BuildingKind.Granary);
        Assert.True(world.Mark(BuildingKind.Granary, at).Allowed);

        Workplace site = SiteFor(world, BuildingKind.Granary);

        // ⭐⭐ POSED AS JOE'S VILLAGE ACTUALLY WAS, and the first draft of this guard was not.
        // It left the shed empty and then asserted the sentence named STONE — but a fresh
        // valley has no logs either, and the note reports the first material the village cannot
        // get, so it correctly said "40 logs". **The pose was wrong, not the code.** His
        // Fernhollow held 206 logs and 0 stone, which is the case worth guarding: the timber is
        // there, the site still cannot be raised, and only one material explains it.
        StoreBuilding shed = Assert.Single(
            world.StoreBuildings, store => store.Kind == StoreKind.Shed);
        Assert.True(world.SetStoreAccepts(shed, Goods.Logs, accepted: true).Allowed);
        shed.Store.Add(Goods.Logs, site.Construction!.Recipe.Materials[0].Amount * 4);

        // ⚠️ ANTI-VACUITY (D7): this proves nothing unless the granary really does want stone
        // and the village really has none. The founders' cart carries food and tools only
        // (D215), so a fresh valley has no stone until somebody clears a seam.
        Assert.True(
            site.Construction.StillNeeded(Goods.Stone) > 0,
            "The granary does not want stone here, so there is nothing to be stalled on.");
        Assert.Equal(0, world.InStores(Goods.Stone));
        Assert.True(world.InStores(Goods.Logs) > 0, "The timber must be in hand, as his was.");

        string? note = world.SiteWaitingNote(site);
        _output.WriteLine($"the site says: {note ?? "(nothing)"}");

        Assert.NotNull(note);
        Assert.Contains("stone", note, System.StringComparison.OrdinalIgnoreCase);

        // ⭐ AND IT NAMES THE REMEDY. A warning whose remedy is unstated is an alert rather than
        // information — the rule `KnowledgeAtRiskNote` is written to, one system over.
        Assert.Contains("paint", note, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>⭐ And it stays quiet about a site that is merely waiting its turn.</summary>
    /// <remarks>
    /// <b>The half that stops this becoming a nag.</b> A site whose materials are all in store
    /// is not stalled, however long the queue ahead of it — the queue already has its own line
    /// in the inspector, and D93's ruling is that the player is told what is <em>in front of
    /// it</em>, not warned about it.
    /// </remarks>
    [Fact]
    public void ASiteThatIsOnlyWaitingItsTurnSaysNothing()
    {
        SimConfig config = VillageFixtures.Village;
        SimWorld world = SimFactory.CreatePhase0(config, new InMemoryLogSink()).World;

        GridPos at = ClearGroundNear(world, BuildingKind.Granary);
        Assert.True(world.Mark(BuildingKind.Granary, at).Allowed);
        Workplace site = SiteFor(world, BuildingKind.Granary);

        // Give the village everything the site could possibly want, so the ONLY thing left is
        // somebody's legs. Read the materials out of the recipe rather than writing numbers in
        // — an instrument that assumes a simpler world measures something else.
        StoreBuilding shed = Assert.Single(
            world.StoreBuildings, store => store.Kind == StoreKind.Shed);

        foreach (MaterialCost material in site.Construction!.Recipe.Materials)
        {
            Assert.True(
                world.SetStoreAccepts(shed, material.Goods, accepted: true).Allowed);
            shed.Store.Add(material.Goods, material.Amount * 4);
        }

        _output.WriteLine(
            $"the shed holds {site.Construction.Recipe.Describe(world.GoodsCatalog)} over, "
            + $"and the site says: {world.SiteWaitingNote(site) ?? "(nothing)"}");

        Assert.Null(world.SiteWaitingNote(site));
    }

    /// <summary>⭐⭐ The village log carries it too, once, and not every season after.</summary>
    /// <remarks>
    /// <b>Both halves from one method</b>, which is D195's rule for the at-risk line and the
    /// reason it is one method rather than two sentences that can drift. Said once on the edge,
    /// shown on the panel for as long as it is true.
    /// </remarks>
    [Fact]
    public void TheVillageLogSaysItOnceRatherThanEverySeason()
    {
        SimConfig config = VillageFixtures.Village;
        var sink = new InMemoryLogSink();
        SimLoop loop = SimFactory.CreatePhase0(config, sink);
        SimWorld world = loop.World;

        GridPos at = ClearGroundNear(world, BuildingKind.Granary);
        Assert.True(world.Mark(BuildingKind.Granary, at).Allowed);

        for (int tick = 0; tick < config.TicksPerYear * 3; tick++)
        {
            loop.StepOnce();
        }

        int said = 0;
        foreach (LogEntry entry in sink.Entries)
        {
            if (entry.Subsystem == "life" && entry.Message.Contains("is waiting for", System.StringComparison.Ordinal))
            {
                said++;
                _output.WriteLine($"[t{entry.Tick}] {entry.Message}");
            }
        }

        // ⚠️ Three years is twelve seasonal sweeps. Said once per site per material is the
        // claim; a village that heard it twelve times would have learned to ignore it.
        Assert.True(said > 0, "Three years passed and the stalled granary was never mentioned.");
        Assert.True(
            said <= 2,
            $"The stall was announced {said} times in three years. Once on the edge is the "
                + "rule — a warning repeated every season is a nag, not information.");
    }
}
