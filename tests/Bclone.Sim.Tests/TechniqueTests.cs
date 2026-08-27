using System.Linq;
using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// ⭐ Slice 1 of <c>phase-4-the-tech-tree.md</c> — <b>a technique is a row, and the three states
/// are real</b>.
/// </summary>
/// <remarks>
/// <para>
/// The claim under test is <c>tech-tree.md §1</c>'s: <b>the tree and the population pyramid are the
/// same object.</b> What the village knows is what its living people know — so these guards are
/// mostly about <em>deaths</em>, which is why several of them run a century rather than a season.
/// </para>
/// <para>
/// <b>⛔ THE ANTI-VACUITY GUARD IS THE ONE THAT MATTERS</b>
/// (<see cref="AVillageThatNeverKeepsAMasterActuallyLosesWhatItKnew"/>). <c>tech-tree.md §13</c>
/// asks for it by name: *"a run with no apprenticeships and no records must actually lose
/// techniques. If nothing is ever lost, the system is decorative."* **That is D56's shape — a
/// system that accrues, is visible and changes nothing — and it is what D177 kept Phase 3 clear
/// of.** The same rule binds here.
/// </para>
/// </remarks>
public sealed class TechniqueTests
{
    private readonly ITestOutputHelper _output;

    public TechniqueTests(ITestOutputHelper output) => _output = output;

    private const int SplittingLumber = 0;
    private const int Coppicing = 1;
    private const int CropRotation = 2;
    private const int TendedPatches = 3;

    private static SimLoop Loop(SimConfig config, InMemoryLogSink sink) =>
        SimFactory.CreatePhase0(config, sink);

    private static string Said(InMemoryLogSink sink)
    {
        var said = new System.Text.StringBuilder();
        foreach (LogEntry entry in sink.Entries)
        {
            said.Append(entry.Message).Append(" | ");
        }

        return said.ToString();
    }

    // -----------------------------------------------------------------
    //  The catalogue
    // -----------------------------------------------------------------

    [Fact]
    public void EveryTechniqueIsWorkedOutByASkillThatExists()
    {
        SimConfig config = VillageFixtures.Village;
        var catalogue = new TechniquesCatalog(config.Techniques, config.Skills);

        Assert.Equal(4, catalogue.Count);

        for (int id = 0; id < catalogue.Count; id++)
        {
            TechniqueRow row = catalogue[id];

            // ⭐ Found by asking the catalogue the question the sim asks: *which technique does a
            // master of this skill work out?* A row whose skill nothing claims would sit at Unknown
            // for three centuries and read exactly like one whose masters never appeared.
            Assert.Equal(id, catalogue.FromSkill(row.Skill));
            Assert.True(row.YieldBonusPercent > 0, $"{row.Name} does nothing.");
            Assert.NotEqual(string.Empty, row.DiscoveryLine);
            Assert.NotEqual(string.Empty, row.LostLine);
        }
    }

    /// <summary>⛔ A technique nobody could ever learn is refused at load, not left silent.</summary>
    [Fact]
    public void ATechniqueAttachedToNoSkillIsRefusedAtLoad()
    {
        SimConfig config = VillageFixtures.Village;
        var rows = new List<TechniqueRow>(config.Techniques)
        {
            new TechniqueRow
            {
                Id = 4,
                Name = "glassblowing",
                Skill = 99,
                YieldBonusPercent = 10,
                DiscoveryLine = "{0} learned it.",
                LostLine = "{0} took it.",
            },
        };

        SimConfigException blew = Assert.Throws<SimConfigException>(
            () => (config with { Techniques = rows }).Validate());

        _output.WriteLine(blew.Message);
        Assert.Contains("glassblowing", blew.Message, System.StringComparison.Ordinal);
    }

    /// <summary>⛔ Every unlock owes the player a sentence — a silent one is refused.</summary>
    [Fact]
    public void ATechniqueWithNoSentenceIsRefusedAtLoad()
    {
        SimConfig config = VillageFixtures.Village;
        var rows = new List<TechniqueRow>(config.Techniques);
        rows[0] = rows[0] with { DiscoveryLine = string.Empty };

        SimConfigException blew = Assert.Throws<SimConfigException>(
            () => (config with { Techniques = rows }).Validate());

        _output.WriteLine(blew.Message);
        Assert.Contains("sentence", blew.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------
    //  The state machine
    // -----------------------------------------------------------------

    /// <summary>A fresh village knows nothing, and says nothing about it.</summary>
    [Fact]
    public void AVillageWithNoMastersKnowsNothing()
    {
        var sink = new InMemoryLogSink();
        SimWorld world = Loop(VillageFixtures.Village, sink).World;

        for (int id = 0; id < world.TechniquesCatalog.Count; id++)
        {
            Assert.Equal(KnowledgeState.Unknown, world.KnowledgeStates[id]);
        }
    }

    /// <summary>
    /// ⭐⭐ A master works something out, and the yield of the whole trade moves with it.
    /// </summary>
    /// <remarks>
    /// <b>Posed rather than played, and deliberately:</b> mastery is twenty years, so playing to it
    /// would make this a slow guard that tests the calendar as much as the technique. What cannot
    /// be posed is checked by the century-long guards below. ⚠️ <c>Mastered</c> IS posable — unlike
    /// <c>LifeStage</c> and <c>AgeYears</c>, which D195 found are recomputed within one tick —
    /// because only <c>SkillSystem</c> ever sets it and only on crossing the threshold.
    /// </remarks>
    [Fact]
    public void AMasterWorksItOutAndTheWholeTradeGetsBetter()
    {
        var sink = new InMemoryLogSink();
        SimLoop loop = Loop(VillageFixtures.Village, sink);
        SimWorld world = loop.World;

        int before = world.YieldWithTechnique(JobKind.Woodcutter, 100);

        MakeAMasterOf(world, skillId: 3);
        loop.Step(1);

        int after = world.YieldWithTechnique(JobKind.Woodcutter, 100);

        _output.WriteLine($"a hundred logs' worth of splitting: {before} -> {after}");

        Assert.Equal(KnowledgeState.Known, world.KnowledgeStates[SplittingLumber]);
        Assert.Equal(100, before);
        Assert.Equal(115, after);

        // ⛔ One sentence, naming the person. An advance the player cannot account for is a bug
        // (`tech-tree.md §11`, non-negotiable 1).
        Assert.Contains("gives more cords", Said(sink), System.StringComparison.Ordinal);
    }

    /// <summary>⭐ A technique is the village's, not the knower's — every worker gets it.</summary>
    /// <remarks>
    /// <b>This is what makes a technique different from proficiency</b>, and why losing one hurts
    /// the whole village rather than one workplace. A bonus that applied only to its knower would
    /// be indistinguishable from mastery, which already bites (D187).
    /// </remarks>
    [Fact]
    public void TheBonusIsTheVillagesAndNotTheKnowersAlone()
    {
        SimLoop loop = Loop(VillageFixtures.Village, new InMemoryLogSink());
        SimWorld world = loop.World;

        MakeAMasterOf(world, skillId: 3);
        loop.Step(1);

        // Asked without naming anybody at all — the signature is the claim.
        Assert.Equal(115, world.YieldWithTechnique(JobKind.Woodcutter, 100));
    }

    /// <summary>
    /// ⛔⛔ It dies with the last person who knew it, and the village says whose it was.
    /// </summary>
    [Fact]
    public void ATechniqueDiesWithItsLastKnower()
    {
        var sink = new InMemoryLogSink();
        SimLoop loop = Loop(VillageFixtures.Village, sink);
        SimWorld world = loop.World;

        Villager master = MakeAMasterOf(world, skillId: 4);
        loop.Step(1);

        Assert.Equal(KnowledgeState.Known, world.KnowledgeStates[CropRotation]);

        master.Alive = false;
        loop.Step(1);

        Assert.Equal(KnowledgeState.Unknown, world.KnowledgeStates[CropRotation]);

        string said = Said(sink);
        _output.WriteLine(said);

        // ⭐ The sentence names the person, which is `phase-4-the-tech-tree.md §6`'s success test:
        // if the answer to "what happened?" is "a node re-locked", the phase has failed.
        Assert.Contains(master.Name, said, System.StringComparison.Ordinal);
        Assert.Contains("rest them", said, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// ⚠️ And it names the LAST holder, not the first — the bug that hid behind a correct edge.
    /// </summary>
    /// <remarks>
    /// <b>Found by reading the loop rather than by a failing test.</b> Recording the knower only on
    /// the Unknown-to-Known transition leaves the note pointing at whoever mastered it first — so a
    /// village with two masters would, on losing the technique years later, name a woman who had
    /// been dead the whole time. **The edge fires correctly and the sentence is wrong**, which is
    /// the hardest class of bug in this repo to see (D200, D220).
    /// </remarks>
    [Fact]
    public void TheVillageNamesTheLastSoulWhoKnewItAndNotTheFirst()
    {
        var sink = new InMemoryLogSink();
        SimLoop loop = Loop(VillageFixtures.Village, sink);
        SimWorld world = loop.World;

        Villager first = MakeAMasterOf(world, skillId: 4);
        loop.Step(1);

        Villager second = MakeAMasterOf(world, skillId: 4, first);
        first.Alive = false;
        loop.Step(1);

        // Still known — somebody alive holds it.
        Assert.Equal(KnowledgeState.Known, world.KnowledgeStates[CropRotation]);

        second.Alive = false;
        loop.Step(1);

        string said = Said(sink);
        _output.WriteLine($"first={first.Name} second={second.Name}");

        Assert.Equal(KnowledgeState.Unknown, world.KnowledgeStates[CropRotation]);
        Assert.Contains($"{second.Name} was the only one who knew", said, System.StringComparison.Ordinal);
    }

    /// <summary>⭐ Learning it again is a fresh start for the person, and the village gets it back.</summary>
    [Fact]
    public void ALostTechniqueCanBeWorkedOutAgain()
    {
        SimLoop loop = Loop(VillageFixtures.Village, new InMemoryLogSink());
        SimWorld world = loop.World;

        Villager first = MakeAMasterOf(world, skillId: 1);
        loop.Step(1);
        first.Alive = false;
        loop.Step(1);

        Assert.Equal(KnowledgeState.Unknown, world.KnowledgeStates[TendedPatches]);

        MakeAMasterOf(world, skillId: 1, first);
        loop.Step(1);

        Assert.Equal(KnowledgeState.Known, world.KnowledgeStates[TendedPatches]);
    }

    // -----------------------------------------------------------------
    //  ⛔ Anti-vacuity — the guard the whole slice answers to
    // -----------------------------------------------------------------

    /// <summary>
    /// ⛔⛔ A village that never keeps a master actually loses what it knew.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>tech-tree.md §13</c> asks for this by name: <em>"a run with no apprenticeships and no
    /// records must actually lose techniques. If nothing is ever lost, the system is
    /// decorative."</em>
    /// </para>
    /// <para>
    /// <b>⭐ AND IT IS THE SLICE'S DESIGN, NOT AN EDGE CASE.</b> Nothing can be written down yet —
    /// the library is slice 2 — so <b>every technique this village works out is eventually lost</b>,
    /// on purpose. The loss is made real before the remedy exists, so the library arrives answering
    /// a pressure the player has felt (`phase-4-the-tech-tree.md §3`).
    /// </para>
    /// </remarks>
    [Fact]
    public void AVillageThatNeverKeepsAMasterActuallyLosesWhatItKnew()
    {
        var sink = new InMemoryLogSink();
        SimLoop loop = Loop(VillageFixtures.Village, sink);
        SimWorld world = loop.World;

        int learned = 0;
        int lost = 0;
        var held = new bool[world.TechniquesCatalog.Count];

        // A century, which is the unit: mastery is twenty years and a master has to die of old age
        // afterwards for anything to be lost.
        for (int year = 0; year < 100; year++)
        {
            for (int tick = 0; tick < world.Config.TicksPerYear; tick++)
            {
                loop.Step(1);
            }

            for (int id = 0; id < held.Length; id++)
            {
                bool now = world.KnowledgeStates[id] != KnowledgeState.Unknown;
                if (now && !held[id])
                {
                    learned++;
                }
                else if (!now && held[id])
                {
                    lost++;
                }

                held[id] = now;
            }
        }

        _output.WriteLine($"over a century: learned {learned}, lost {lost}");

        Assert.True(learned > 0, "A century produced no techniques at all — nobody ever mastered a trade.");
        Assert.True(
            lost > 0,
            $"A village with no way to write anything down lost nothing in a hundred years "
            + $"(learned {learned}). The system is decorative.");

        // ⛔ Nothing is Established, because nothing can be yet. If this ever fails, the library
        // landed without this guard being revisited.
        for (int id = 0; id < held.Length; id++)
        {
            Assert.NotEqual(KnowledgeState.Established, world.KnowledgeStates[id]);
        }
    }

    /// <summary>
    /// ⚠️ The survival floor never moves, however much the village knows.
    /// </summary>
    /// <remarks>
    /// <b>A technique is upside above the line, never a move in the line itself</b> — so losing one
    /// can cost a village its surplus and can never cost it the run. <em>§0.1's "you lose villagers,
    /// not runs", applied to knowledge.</em> If this fails, <see cref="VillageEconomy"/> has started
    /// deriving against a number a funeral can change.
    /// </remarks>
    [Fact]
    public void KnowingThingsNeverMovesTheSurvivalFloor()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        int floorBefore = VillageEconomy.FoodGatheredPerYear(config, 100);

        MakeAMasterOf(world, skillId: 1);
        loop.Step(1);

        Assert.Equal(KnowledgeState.Known, world.KnowledgeStates[TendedPatches]);
        Assert.Equal(floorBefore, VillageEconomy.FoodGatheredPerYear(config, 100));
    }

    /// <summary>⛔ A forestry technique must not improve the village's quarry.</summary>
    /// <remarks>
    /// The harvest yield is one method handing out whatever the ground gives — logs from a stand,
    /// stone from a seam, iron from a deposit. **Applying the forester's technique unconditionally
    /// there would have made a master forester improve the stonework**, silently, with nothing to
    /// see but a number slightly too good.
    /// </remarks>
    [Fact]
    public void ACoppicingMasterDoesNotImproveTheStoneSeams()
    {
        SimLoop loop = Loop(VillageFixtures.Village, new InMemoryLogSink());
        SimWorld world = loop.World;

        MakeAMasterOf(world, skillId: 2);
        loop.Step(1);

        Assert.Equal(KnowledgeState.Known, world.KnowledgeStates[Coppicing]);

        // The technique reaches logs and stops there.
        Assert.Equal(112, world.YieldWithTechnique(JobKind.Forester, 100));
        Assert.Equal(100, world.YieldWithTechnique(JobKind.Builder, 100));
    }

    // -----------------------------------------------------------------

    //  Slice 2 — the library, and Known becoming Established
    // -----------------------------------------------------------------

    /// <summary>
    /// A founding master keeps a technique alive and cannot be the one who works it out.
    /// </summary>
    /// <remarks>
    /// <b>⛔ THE SHIPPED FOUNDING SEEDS ONE MASTER, so a technique was being worked out on tick
    /// one</b>, before the village had a house — which Joe hit in play as *"it feels out of sync"*.
    /// <b>Discovery is an event and persistence is a scan</b>: she is skilled, and she did not have
    /// the moment here.
    /// </remarks>
    [Fact]
    public void AFoundingMasterCannotWorkAnythingOut()
    {
        SimLoop loop = Loop(VillageFixtures.Village, new InMemoryLogSink());
        SimWorld world = loop.World;

        // Exactly what the founding does: mastered, but not mastered here.
        Villager arrived = FirstLiving(world);
        SkillProgress progress = arrived.ProgressIn(4);
        progress.Work = int.MaxValue / 2;
        progress.Mastered = true;

        loop.Step(1);
        Assert.Equal(KnowledgeState.Unknown, world.KnowledgeStates[CropRotation]);

        // ⭐ But once somebody DOES work it out here, she is a knower like any other — so the
        // technique survives the discoverer's death while she is still alive.
        Villager grewIntoIt = MakeAMasterOf(world, 4, arrived);
        loop.Step(1);
        Assert.Equal(KnowledgeState.Known, world.KnowledgeStates[CropRotation]);

        grewIntoIt.Alive = false;
        loop.Step(1);
        Assert.Equal(KnowledgeState.Known, world.KnowledgeStates[CropRotation]);
    }

    /// <summary>
    /// ⛔ A library cannot be built before anybody can write, and the refusal says what to do.
    /// </summary>
    /// <remarks>
    /// <b>D32 and `tech-tree.md §7a`: literacy comes out of the granary.</b> The player does not
    /// set out to invent writing — they set out not to starve, and a kept count is what eventually
    /// teaches it. ⚠️ <b>The rule is asked of the ROW, not of the kind</b>: any building with
    /// shelves waits on literacy, including one this sim has never heard of. The first version
    /// compared against <c>BuildingKind.Library</c> and refused a modder's building that happened
    /// to share the id.
    /// </remarks>
    [Fact]
    public void NobodyCanBuildALibraryBeforeTheyCanWrite()
    {
        SimLoop loop = Loop(VillageFixtures.Village, new InMemoryLogSink());
        SimWorld world = loop.World;

        GridPos site = SomewhereBuildable(world);
        PlacementVerdict tooSoon = world.CanBuildAt(BuildingKind.Library, site);

        Assert.False(tooSoon.Allowed);
        Assert.Contains("granary", tooSoon.Reason, System.StringComparison.OrdinalIgnoreCase);
        _output.WriteLine(tooSoon.Reason);

        Assert.False(world.HasLiteracy);
    }

    /// <summary>
    /// ⭐⭐ The village GIVES the player a library the year it learns to write.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Joe, 2026-08-26, with a SimCity screenshot</b> — the mayor's house you are gifted for
    /// doing well. **A library you build is an item on a list; a library the village gives you is
    /// what fifteen years of keeping a granary bought.** ⛔ No characters: nobody hands it over, it
    /// is simply there.
    /// </para>
    /// <para>
    /// <b>⭐ Beside the granary, and the position is the story</b> — literacy came out of keeping
    /// that building's count (D32), so the records start where the counting happened.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheVillageGivesItselfALibraryWhenItLearnsToWrite()
    {
        var sink = new InMemoryLogSink();
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config, sink);
        SimWorld world = loop.World;

        Assert.Empty(world.Libraries);

        GridPos site = SomewhereBuildable(world);
        world.Mark(BuildingKind.Granary, site);
        FinishTheSiteAt(world, site);

        StoreBuilding granary = world.StoreAt(site)!;

        loop.Step(config.TicksPerYear * (config.LiteracyYears + 2));

        Assert.True(world.HasLiteracy);

        // ⭐⭐ THE GIFT IS THE MATERIALS, NOT THE BUILDING (Joe, from play: *"I think it's best to
        // let the user place the library"*). Nothing is standing yet — the village has gathered
        // the timber and stone and is waiting to be told where.
        Assert.Empty(world.Libraries);
        Assert.True(world.AFreeLibraryIsOwed);

        // ⛔ And it is a moment worth stopping for, not just a log line.
        Assert.Contains(
            world.Moments,
            m => m.Title.Contains("write", System.StringComparison.OrdinalIgnoreCase));

        // ⭐ Marking one costs nothing but the work — the crew still raise it.
        GridPos spot = SomewhereBuildable(world);
        Assert.True(world.Mark(BuildingKind.Library, spot).Allowed);

        Workplace raising = world.Workplaces.Last(w => w.Position == spot && w.IsSite);
        _output.WriteLine($"the gifted library costs {raising.Construction!.Recipe.TotalMaterials} "
            + $"materials and {raising.Construction.Recipe.WorkTicks} ticks of work");

        Assert.Equal(0, raising.Construction.Recipe.TotalMaterials);
        Assert.True(raising.Construction.Recipe.WorkTicks > 0, "Somebody still has to build it.");
        Assert.False(world.AFreeLibraryIsOwed);

        _ = granary;
    }

    /// <summary>⛔ The gift is the FIRST library only — the rest are built and paid for.</summary>
    /// <remarks>
    /// <b>What keeps the shelf cap a decision</b> (`tech-tree.md §11`). A village handed a library
    /// every time it needed one would never have to choose which techniques survive, which is the
    /// guard D204 already left carrying most of the weight.
    /// </remarks>
    [Fact]
    public void OnlyTheFirstLibraryIsAGift()
    {
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config, new InMemoryLogSink());
        SimWorld world = loop.World;

        world.Mark(BuildingKind.Granary, SomewhereBuildable(world));
        FinishTheSiteAt(world, world.Workplaces[^1].Position);
        loop.Step(config.TicksPerYear * (config.LiteracyYears + 2));

        Assert.True(world.AFreeLibraryIsOwed);

        // Spend it.
        world.Mark(BuildingKind.Library, SomewhereBuildable(world));
        Assert.False(world.AFreeLibraryIsOwed);

        // ⭐ A second one costs what a library costs — which is what keeps the shelf cap a
        // decision rather than a formality.
        GridPos second = SomewhereBuildable(world);
        Assert.True(world.Mark(BuildingKind.Library, second).Allowed);

        Workplace site = world.Workplaces.Last(w => w.Position == second && w.IsSite);
        Assert.True(
            site.Construction!.Recipe.TotalMaterials > 0,
            "The second library was free too — the gift is meant to be spent once.");

        // ⛔ And another decade grants nothing further.
        loop.Step(config.TicksPerYear * 10);
        Assert.False(world.AFreeLibraryIsOwed);
    }

    /// <summary>
    /// ⛔ The founders' cart can be pulled down, and the refusal names what is inside it.
    /// </summary>
    /// <remarks>
    /// <b>Joe, playing:</b> *"when I try to demolish the cart, the UI tells me 'there is nothing
    /// there to pull down' — note the cart has items in it."* **`WhatStandsAt` asks the buildings
    /// catalogue which building stores as a `Cart`, and no row may claim that** — validated at
    /// load, deliberately, because the wagon is not something the player puts up. *A correct rule
    /// producing a false sentence, and the third time this session a list that did not know about
    /// a kind of thing said "there is nothing here."*
    /// </remarks>
    [Fact]
    public void TheCartComesDownOnceItIsEmpty()
    {
        // ⚠️ A COLD START, because the cart is the cold start's own building — it only exists in a
        // village that was not handed anything (`founding_buildings: false`, D64). The warm fixture
        // has no wagon to pull down, and asserting against it would have been a guard about the
        // fixture rather than about the cart.
        SimConfig config = VillageFixtures.Village with { FoundingBuildings = false };
        SimWorld world = Loop(config, new InMemoryLogSink()).World;
        StoreBuilding cart = world.StoreBuildings.First(s => s.Kind == StoreKind.Cart);

        // ⚠️ It already holds the founders' supplies — the cart arrives full, which is its whole
        // reason for existing (D64). Read the number rather than assuming one.
        int aboard = cart.Store.Held;
        Assert.True(aboard > 0, "The founders' cart should arrive with their supplies in it.");

        PlacementVerdict refused = world.MarkDemolition(cart.Position);

        _output.WriteLine(refused.Reason);
        Assert.False(refused.Allowed);
        Assert.DoesNotContain(
            "nothing there", refused.Reason, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            aboard.ToString(System.Globalization.CultureInfo.InvariantCulture),
            refused.Reason,
            System.StringComparison.Ordinal);

        // ⭐ Emptied, it is wheeled away rather than dismantled — D64's own "demolishable once
        // empty", finally reachable.
        for (int g = 0; g < world.GoodsCatalog.Count; g++)
        {
            cart.Store.TakeAll((Goods)g);
        }
        Assert.True(world.MarkDemolition(cart.Position).Allowed);
        Assert.DoesNotContain(world.StoreBuildings, s => s.Kind == StoreKind.Cart);
    }

    /// <summary>Literacy arrives from a granary that has been kept, and the village says so.</summary>
    [Fact]
    public void KeepingAGranarysCountForYearsTeachesTheVillageToWrite()
    {
        var sink = new InMemoryLogSink();
        SimConfig config = VillageFixtures.Village;
        SimLoop loop = Loop(config, sink);
        SimWorld world = loop.World;

        Assert.False(world.HasLiteracy);

        // ⚠️ BUILT BY HAND RATHER THAN WAITED FOR, and the first draft of this guard waited.
        // A cold-start village has to raise a builder's hut, gather timber and stone and get
        // through a queue, and **it had not finished a granary in seventeen years** — so the guard
        // failed for a reason that had nothing to do with literacy. *Its own anti-vacuity check is
        // what said so, instead of leaving a confusing red.*
        world.Mark(BuildingKind.Granary, SomewhereBuildable(world));
        FinishTheSiteAt(world, world.Workplaces[^1].Position);

        loop.Step(config.TicksPerYear * (config.LiteracyYears + 2));

        // ⚠️ Only meaningful if the granary actually got built — a run where the village never
        // finished it would pass this guard for the wrong reason.
        Assert.True(
            world.FirstGranaryTick > 0,
            "The village never finished a granary, so this proves nothing about literacy.");
        Assert.True(world.HasLiteracy);
        Assert.Contains("signs of their own devising", Said(sink), System.StringComparison.Ordinal);

        PlacementVerdict now = world.CanBuildAt(BuildingKind.Library, SomewhereBuildable(world));
        Assert.True(now.Allowed, now.Reason);
    }

    /// <summary>A tile the village may build on, found rather than assumed.</summary>
    private static GridPos SomewhereBuildable(SimWorld world)
    {
        for (int y = 0; y < world.Map.Height; y++)
        {
            for (int x = 0; x < world.Map.Width; x++)
            {
                var at = new GridPos(x, y);
                if (world.Map.TerrainAt(at) == Terrain.Grass
                    && world.CanBuildAt(BuildingKind.Granary, at).Allowed)
                {
                    return at;
                }
            }
        }

        throw new System.InvalidOperationException("No buildable tile in the valley.");
    }

    /// <summary>Deliver a site's materials and work it to completion, as a builder's crew would.</summary>
    private static void FinishTheSiteAt(SimWorld world, GridPos site)
    {
        Workplace? found = null;
        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            if (world.Workplaces[i].Position == site && world.Workplaces[i].IsSite)
            {
                found = world.Workplaces[i];
            }
        }

        Assert.NotNull(found);

        ConstructionSite plan = found!.Construction!;
        foreach (MaterialCost owed in plan.Recipe.Materials)
        {
            plan.Deliver(owed.Goods, owed.Amount);
        }

        while (!plan.IsFinished)
        {
            plan.Work();
        }

        world.Complete(found);
    }

    private static Villager FirstLiving(SimWorld world)
    {
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            if (world.Villagers[i].Alive)
            {
                return world.Villagers[i];
            }
        }

        throw new System.InvalidOperationException("Nobody is alive.");
    }

    /// <summary>A written technique outlives the death of everyone who knew it.</summary>
    /// <remarks>
    /// <b>The third state earning its place.</b> Slice 1's village lost everything, every time;
    /// this one guard is the whole difference a library makes.
    /// </remarks>
    [Fact]
    public void AWrittenTechniqueOutlivesItsLastKnower()
    {
        var sink = new InMemoryLogSink();
        SimLoop loop = Loop(VillageFixtures.Village, sink);
        SimWorld world = loop.World;

        GiveThemALibrary(world);

        Villager master = MakeAMasterOf(world, 4);
        loop.Step(1);

        Assert.Equal(KnowledgeState.Established, world.KnowledgeStates[CropRotation]);
        Assert.True(world.IsWrittenDown(CropRotation));

        master.Alive = false;
        loop.Step(1);

        // Nobody alive knows it, and the village still does it her way.
        Assert.Equal(KnowledgeState.Established, world.KnowledgeStates[CropRotation]);
        Assert.Equal(115, world.YieldWithTechnique(JobKind.Farmer, 100));

        _output.WriteLine(Said(sink));
    }

    /// <summary>
    /// A full library refuses the record and says what to do about it.
    /// </summary>
    /// <remarks>
    /// <b>Load-bearing rather than polish.</b> `tech-tree.md §11`'s guard against *"the library is
    /// mandatory"* rested on three costs, and <b>D204 deleted one of them</b> by making recording
    /// automatic at mastery — so the hard shelf cap carries that guard nearly alone. If this ever
    /// goes green by accident, writing everything down has become always-correct and the building
    /// has stopped being a decision.
    /// </remarks>
    [Fact]
    public void AFullLibraryRefusesTheRecordAndSaysSo()
    {
        var sink = new InMemoryLogSink();
        SimLoop loop = Loop(VillageFixtures.Village, sink);
        SimWorld world = loop.World;

        Library library = GiveThemALibrary(world);
        Assert.Equal(3, library.Shelves);

        Villager a = MakeAMasterOf(world, 1);
        Villager b = MakeAMasterOf(world, 2, a);
        Villager c = MakeAMasterOf(world, 3, a, b);
        loop.Step(1);

        Assert.Equal(3, library.Records.Count);
        Assert.False(library.HasRoom);

        Villager d = MakeAMasterOf(world, 4, a, b, c);
        loop.Step(1);

        string said = Said(sink);
        _output.WriteLine(said);

        // Known, not Established — the village does it, and cannot keep it.
        Assert.Equal(KnowledgeState.Known, world.KnowledgeStates[CropRotation]);
        Assert.False(world.IsWrittenDown(CropRotation));
        Assert.Contains("no shelf left", said, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Build another library", said, System.StringComparison.Ordinal);

        // And the refusal is real: it dies with him, exactly as it would have with no library.
        d.Alive = false;
        loop.Step(1);
        Assert.Equal(KnowledgeState.Unknown, world.KnowledgeStates[CropRotation]);
    }

    /// <summary>A second library is the answer, and it works.</summary>
    [Fact]
    public void ASecondLibraryTakesWhatTheFirstCouldNotHold()
    {
        SimLoop loop = Loop(VillageFixtures.Village, new InMemoryLogSink());
        SimWorld world = loop.World;

        GiveThemALibrary(world);

        Villager a = MakeAMasterOf(world, 1);
        Villager b = MakeAMasterOf(world, 2, a);
        Villager c = MakeAMasterOf(world, 3, a, b);
        loop.Step(1);

        GiveThemALibrary(world, at: new GridPos(3, 3));

        MakeAMasterOf(world, 4, a, b, c);
        loop.Step(1);

        Assert.Equal(KnowledgeState.Established, world.KnowledgeStates[CropRotation]);
        Assert.Single(world.Libraries[1].Records);
    }

    /// <summary>
    /// Pulling the library down loses the record, and the technique is mortal again.
    /// </summary>
    /// <remarks>
    /// <b>This is what stops <c>Established</c> being a ratchet in this slice.</b> Fire is not in
    /// this phase, so demolition is the only way a record can be lost — and without it everything
    /// written down would be permanent, which is `tech-tree.md §11`'s *"the collections become a
    /// ratchet"* arriving by the back door.
    /// </remarks>
    [Fact]
    public void PullingDownTheLibraryPutsTheTechniqueBackAtRisk()
    {
        var sink = new InMemoryLogSink();
        SimLoop loop = Loop(VillageFixtures.Village, sink);
        SimWorld world = loop.World;

        Library library = GiveThemALibrary(world);
        Villager master = MakeAMasterOf(world, 4);
        loop.Step(1);

        Assert.Equal(KnowledgeState.Established, world.KnowledgeStates[CropRotation]);

        world.Demolish(library);
        loop.Step(1);

        // Still known — he is alive. But it is mortal again.
        Assert.Equal(KnowledgeState.Known, world.KnowledgeStates[CropRotation]);
        Assert.Contains("crop rotation", Said(sink), System.StringComparison.Ordinal);

        master.Alive = false;
        loop.Step(1);
        Assert.Equal(KnowledgeState.Unknown, world.KnowledgeStates[CropRotation]);
    }

    /// <summary>A record preserves the method and never the proficiency.</summary>
    /// <remarks>
    /// <b>`tech-tree.md §3a`'s anti-ratchet rule, and `skills-catalog.md §6.6` is the side that
    /// enforces it.</b> The open question both specs asked is settled: <b>proficiency retained from
    /// a record is ZERO, not a floor</b> — the village keeps the method and the next person still
    /// owes it twenty years. <em>A record converts a catastrophic loss into an expensive setback.</em>
    /// </remarks>
    [Fact]
    public void ARecordPreservesTheMethodAndNeverTheProficiency()
    {
        SimLoop loop = Loop(VillageFixtures.Village, new InMemoryLogSink());
        SimWorld world = loop.World;

        GiveThemALibrary(world);
        Villager master = MakeAMasterOf(world, 4);
        loop.Step(1);

        master.Alive = false;
        loop.Step(1);

        Assert.Equal(KnowledgeState.Established, world.KnowledgeStates[CropRotation]);

        // And nobody else got a single year of her life for it.
        foreach (Villager villager in world.Villagers)
        {
            if (!villager.Alive)
            {
                continue;
            }

            Assert.False(
                villager.FindProgressIn(4) is { Mastered: true },
                $"{villager.Name} became a master of farming without working for it.");
        }
    }

    /// <summary>Nothing can be built on top of a library.</summary>
    /// <remarks>
    /// A library is the fourth kind of thing that can stand on a tile, and
    /// <c>SomethingStandsAt</c> knew about three. <b>That method's own comment is about this going
    /// wrong once already</b> — two rules for *"is this tile free?"*, with the wrong one facing the
    /// player.
    /// </remarks>
    [Fact]
    public void NothingCanBeBuiltOnTopOfALibrary()
    {
        SimWorld world = Loop(VillageFixtures.Village, new InMemoryLogSink()).World;

        Library library = GiveThemALibrary(world);
        PlacementVerdict verdict = world.CanBuildAt(BuildingKind.Granary, library.Position);

        Assert.False(verdict.Allowed);
        Assert.Contains(
            "already stands", verdict.Reason, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Stand a library in the village without making anybody build it.</summary>
    private static Library GiveThemALibrary(SimWorld world, GridPos? at = null)
    {
        var library = new Library
        {
            Position = at ?? new GridPos(2, 2),
            Name = $"library {world.Libraries.Count + 1}",
            Shelves = world.Config.LibraryShelves,
        };

        world.Libraries.Add(library);
        return library;
    }

    /// <summary>Pose a living master of one skill, and hand them back.</summary>
    /// <remarks>
    /// <b>⚠️ <c>Mastered</c> is safe to pose and most of this villager is not</b> — D195 found that
    /// <c>LifeStage</c> and <c>AgeYears</c> both last exactly one tick before something recomputes
    /// them. This writes the one field <c>SkillSystem</c> owns and nothing else recomputes.
    /// </remarks>
    // -----------------------------------------------------------------
    private static Villager MakeAMasterOf(SimWorld world, int skillId, params Villager[] skip)
    {
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (!villager.Alive || System.Array.Exists(skip, s => ReferenceEquals(s, villager)))
            {
                continue;
            }

            SkillProgress progress = villager.ProgressIn(skillId);
            progress.Work = int.MaxValue / 2;
            progress.Mastered = true;

            // ⭐ A HOME-GROWN MASTER, WHICH IS NOW A DIFFERENT THING FROM A MASTER. Since a
            // founding master cannot work anything out, every guard here that expects a discovery
            // has to pose somebody who reached mastery **in this valley** — and
            // <see cref="AFoundingMasterCannotWorkAnythingOut"/> is the guard for the other side.
            progress.MasteredHere = true;
            return villager;
        }

        throw new System.InvalidOperationException("No living villager to make a master of.");
    }
}
