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

        Villager second = MakeAMasterOf(world, skillId: 4, skip: first);
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

        MakeAMasterOf(world, skillId: 1, skip: first);
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

    /// <summary>Pose a living master of one skill, and hand them back.</summary>
    /// <remarks>
    /// <b>⚠️ <c>Mastered</c> is safe to pose and most of this villager is not</b> — D195 found that
    /// <c>LifeStage</c> and <c>AgeYears</c> both last exactly one tick before something recomputes
    /// them. This writes the one field <c>SkillSystem</c> owns and nothing else recomputes.
    /// </remarks>
    private static Villager MakeAMasterOf(SimWorld world, int skillId, Villager? skip = null)
    {
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (!villager.Alive || ReferenceEquals(villager, skip))
            {
                continue;
            }

            SkillProgress progress = villager.ProgressIn(skillId);
            progress.Work = int.MaxValue / 2;
            progress.Mastered = true;
            return villager;
        }

        throw new System.InvalidOperationException("No living villager to make a master of.");
    }
}
