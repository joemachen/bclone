using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;

namespace Bclone.Sim.Systems;

/// <summary>
/// What the village knows, recomputed from who is alive — <b>the tree and the population pyramid
/// are the same object</b> (`specs/tech-tree.md §1`).
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐⭐ THE STATE IS DERIVED, NOT ACCUMULATED, AND THAT IS THE WHOLE ANTI-RATCHET.</b> Nothing
/// here ever sets a flag saying *"the village has learned this"*. Every tick it asks the only
/// question that matters — <em>is anybody alive who has mastered the trade?</em> — and the answer
/// is the state. **A technique cannot be banked**, because there is no place to bank it: the
/// village knows what its living people know, and nothing else.
/// </para>
/// <para>
/// <b>⛔ So re-locking needs no machinery at all.</b> `tech-tree.md §5` describes it as an event;
/// it is not one. The last master dies, the scan finds nobody, and the technique is Unknown again
/// on the next tick. *There is no code path that could forget to fire.*
/// </para>
/// <para>
/// <b>⚠️ THE STORED STATE IS THEREFORE REDUNDANT, AND IT IS STORED ANYWAY — FOR THE EDGE.</b> The
/// village has to <em>say</em> when something is worked out or lost, and a sentence on a transition
/// needs to know what was true last tick. That is the only reason
/// <c>SimWorld.KnowledgeStates</c> exists. **It is hashed** (`StateHash`), because the sim reads it
/// when it applies a yield bonus — and *state the sim reads and the hash cannot see is two runs
/// that read identical and are not*, the trap this project treats as P0.
/// </para>
/// <para>
/// <b>⭐ THE PEOPLE MECHANISM, AND ONLY IT.</b> `tech-tree.md §4` names eight; this implements
/// **PEOPLE** — *"a master develops the advance after long enough in the work"* — and the other
/// seven are named in `phase-4-the-tech-tree.md §3` as explicitly out of this phase so they are not
/// smuggled in. SEREN in particular needs a seeded roll and is deliberately absent: **nothing here
/// touches the RNG**, so the knowledge state cannot shift the draw order of anything else.
/// </para>
/// </remarks>
public sealed class KnowledgeSystem : ISimSystem
{
    public string Name => "knowledge";

    public void Execute(SimWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        NoticeIfAnybodyLearnedToWrite(world);

        TechniquesCatalog catalogue = world.TechniquesCatalog;

        for (int id = 0; id < catalogue.Count; id++)
        {
            TechniqueRow? technique = catalogue[id];
            if (technique is null)
            {
                continue;
            }

            KnowledgeState was = world.KnowledgeStates[id];

            // ⛔⛔ ESTABLISHED SURVIVES ITS LAST KNOWER — THAT IS THE ENTIRE POINT OF THE THIRD
            // STATE, and this early exit is what makes it true. The scan below only ever decides
            // between Unknown and Known; a written technique is not up for reconsideration by a
            // headcount of the living (`tech-tree.md §3`).
            //
            // ⚠️ It is not permanent, though — it is *durable*. `IsWrittenDown` is asked afresh,
            // so demolishing the last library holding a record puts the technique back at the mercy
            // of who is alive. **A record is a building, and buildings can be lost.**
            if (was == KnowledgeState.Established)
            {
                if (world.IsWrittenDown(id))
                {
                    continue;
                }

                // The record is gone. Fall through: the technique is now worth exactly as much as
                // the people who still know it, which may be nobody.
                world.Narrate($"The village's record of {technique.Name} is gone. "
                    + $"{world.Clock.SeasonAndYear()}.", LogCategory.Discovery);
                world.KnowledgeStates[id] = KnowledgeState.Known;
                was = KnowledgeState.Known;
            }

            Villager? knower = FirstLivingMasterOf(world, technique.Skill);

            // ⭐⭐ DISCOVERY IS AN EVENT; PERSISTENCE IS A SCAN — and they ask different questions
            // (Joe, 2026-08-26, from play). A technique the village has never had is worked out by
            // somebody who reached mastery **here**; once it exists, **any** living master keeps it
            // alive, home-grown or arrived-with-the-cart.
            //
            // ⛔ THE BUG THIS CLOSES: the shipped founding seeds one master, so a technique was
            // being worked out on **tick one**, before the village had a house. *"Unlock by doing"*
            // with nothing done. **A founding master is skilled and did not have the moment here.**
            bool nobodyHasEverHadIt = was == KnowledgeState.Unknown;
            if (nobodyHasEverHadIt && !AnybodyMasteredItHere(world, technique.Skill))
            {
                knower = null;
            }

            KnowledgeState now = knower is null ? KnowledgeState.Unknown : KnowledgeState.Known;

            // ⚠️ REMEMBERED EVERY TICK IT IS HELD, NOT ONCE WHEN IT IS LEARNED — and the difference
            // is a wrong name in the saddest sentence the village writes. Recording only the first
            // master means that when a village has two and the FIRST one dies, the note kept
            // pointing at a woman who had been dead for years — so the line about losing the
            // technique would name her rather than whoever actually carried it to the end.
            // *Caught by reading the loop, not by a test, which is the fifth time in this project a
            // stale reference has hidden behind an edge that fires correctly.*
            if (knower is not null)
            {
                world.RememberKnowerOf(id, knower);
            }

            // ⭐⭐ RECORDING IS AUTOMATIC AT MASTERY (D204, Joe) — there is no button and no
            // project. A village that knows something and has a shelf free writes it down, because
            // the alternative Joe overturned was `tech-tree.md §7b`'s seasons-long scriptorium and
            // it took a scribe, a literate one, and the master off work to get it.
            //
            // ⛔ AND THE REFUSAL IS THE FEATURE, NOT THE FAILURE. §11's guard against *"the library
            // is mandatory"* rested on three costs and D204 deleted one, so **a full library saying
            // so, by name, is what is left of it.** Said once on the edge rather than every tick,
            // or the log would fill with it for ever.
            if (now == KnowledgeState.Known && !world.IsWrittenDown(id))
            {
                Library? shelved = world.WriteDown(id, knower?.Name ?? world.LastKnowerOf(id));
                if (shelved is not null)
                {
                    world.KnowledgeStates[id] = KnowledgeState.Established;
                }

                // ⭐⭐ A DISCOVERY IS A MOMENT, AND IT SAYS WHETHER THE VILLAGE CAN KEEP IT
                // (Joe, 2026-08-27, from play): *"it should also say that the technique lives in
                // the library (unless a library isn't built or there isn't room, in which case
                // it should call those cases out) — a little more celebratory than just a line
                // in the village log."*
                //
                // ⛔ **THE "NO LIBRARY AT ALL" CASE HAD NO SENTENCE ANYWHERE** — the guard below
                // used to read `world.Libraries.Count > 0`, so a village that had never built one
                // discovered a technique and was told nothing about the fact that it would die
                // with its knower. **The one case where the player most needs telling was the one
                // case that said nothing**, which is §1.1 failing in the player's favour.
                //
                // ⚠️ It does NOT stop the game (`stops: false`). A gift must be acted on; this is
                // news. See `Moment.Stops`.
                if (was == KnowledgeState.Unknown)
                {
                    // ⚠️ The state has to be settled BEFORE the `continue`, and the first draft
                    // of this branch forgot it: the old code let an unshelved discovery fall
                    // through to the assignment below, so short-circuiting here left the
                    // technique reading Unknown for ever while its discovery line had already
                    // been said. *A `continue` added to a loop inherits every side effect it
                    // now skips.*
                    world.KnowledgeStates[id] =
                        shelved is not null ? KnowledgeState.Established : now;

                    world.RaiseMoment(
                        $"{knower!.Name} worked something out",
                        $"{Said(technique.DiscoveryLine, knower.Name)} "
                            + Kept(world, technique, knower, shelved),
                        stops: false);

                    continue;
                }

                // Not a discovery — a technique the village already knew finally finding a
                // shelf. The accounting line still matters, and there is no news in it.
                if (shelved is not null)
                {
                    world.Narrate($"{technique.Name} was written down at {shelved.Name} — "
                        + $"{shelved.Shelves - shelved.Records.Count} shelves left. "
                        + $"{world.Clock.SeasonAndYear()}.", LogCategory.Discovery);
                    continue;
                }
            }

            if (now == was)
            {
                continue;
            }

            world.KnowledgeStates[id] = now;

            if (now == KnowledgeState.Known)
            {
                // ⭐ ONCE, ON THE EDGE — a second master is not news. The mastery line has already
                // fired for this person (`SkillSystem`), and this is the different claim: not
                // *"she is good at this"* but *"the village does it her way now."*
                world.Narrate(Said(technique.DiscoveryLine, knower!.Name), LogCategory.Discovery);
                continue;
            }

            // ⚠️ The person named here is the one who was last known to hold it — recorded when it
            // was learned rather than looked up now, because by this tick they are dead and no
            // longer in any list the scan can reach.
            world.Narrate(Said(technique.LostLine, world.LastKnowerOf(id)), LogCategory.Discovery);
        }
    }

    /// <summary>
    /// The first living villager who has mastered a skill, or null if nobody has.
    /// </summary>
    /// <remarks>
    /// <b>⭐ `skills-catalog.md §6`'s fifth contract item, which Phase 3 built</b> — the *who still
    /// knows this, and how old are they* query. <c>SimWorld.KnowledgeAtRiskNote</c> asks the same
    /// question for the warning, so the sentence the player reads years ahead and the state that
    /// changes on the death are **made of the same fact**.
    /// </remarks>
    /// <summary>Say it, once, on the year the granary's count turns into writing.</summary>
    /// <remarks>
    /// <b>⭐ §7a's OWN SENTENCE, and it is the whole reason literacy comes from the granary rather
    /// than from a threshold.</b> The player reaches writing by trying not to starve — so the line
    /// has to be about a person keeping a count, not about a milestone being met.
    /// </remarks>
    private static void NoticeIfAnybodyLearnedToWrite(SimWorld world)
    {
        if (world.SaidTheyCanWrite || !world.HasLiteracy)
        {
            return;
        }

        world.SaidTheyCanWrite = true;

        // ⭐⭐ THE VILLAGE GIVES THE PLAYER A LIBRARY, AND IT IS A REWARD RATHER THAN A CHORE (Joe,
        // 2026-08-26, with a SimCity screenshot: the mayor's house you are gifted for doing well).
        // **A library you BUILD is an item on a list; a library the village GIVES you is what
        // fifteen years of keeping a granary bought.** ⛔ No characters — nobody hands it over, it
        // is simply there, which is the half of SimCity's version worth keeping.
        //
        // ⛔ THE GIFT IS THE FIRST ONE ONLY. Every further library costs materials, which is what
        // keeps the shelf cap a decision (`tech-tree.md §11`) rather than a formality D204 already
        // half-dissolved.
        string body =
            $"The granary's count has been kept for {world.Config.LiteracyYears} years, and "
            + "somebody has begun marking the sacks with signs of their own devising. "
            + $"The village can write things down now. {world.Clock.SeasonAndYear()}.";

        world.AFreeLibraryIsOwed = true;

        world.RaiseMoment(
            "The village learned to write",
            body + " They have gathered timber and stone for a library — put it wherever you "
                + "like, and it will cost you nothing.");
    }

    /// <summary>Whether any living villager reached mastery of a skill in this valley.</summary>
    /// <remarks>
    /// <b>The discovery half of the rule.</b> Asked only when nobody has ever had the technique —
    /// once the village has it, <see cref="FirstLivingMasterOf"/>'s looser question keeps it alive.
    /// </remarks>
    private static bool AnybodyMasteredItHere(SimWorld world, int skillId)
    {
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (villager.Alive && villager.FindProgressIn(skillId) is { MasteredHere: true })
            {
                return true;
            }
        }

        return false;
    }

    private static Villager? FirstLivingMasterOf(SimWorld world, int skillId)
    {
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (villager.Alive && villager.FindProgressIn(skillId) is { Mastered: true })
            {
                return villager;
            }
        }

        return null;
    }

    private static string Said(string line, string who) =>
        line.Length == 0 ? string.Empty : string.Format(
            System.Globalization.CultureInfo.InvariantCulture, line, who);

    /// <summary>Whether the village can keep this, said plainly — Joe, 2026-08-27.</summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ Four cases, and three of them used to say nothing.</b> Only *"written down"* and
    /// *"no shelf left"* existed, and the second was gated on the village already owning a
    /// library — so **a village with no library heard nothing at all**, which is exactly the
    /// case where the warning is worth most.
    /// </para>
    /// <para>
    /// ⚠️ <b>The no-library case splits on literacy, because the remedy differs and a wrong
    /// remedy is worse than none.</b> A village that cannot yet write is not being told to build
    /// something it has no way to use — writing comes out of a granary kept for fifteen years
    /// (D227), not out of a build menu.
    /// </para>
    /// <para>
    /// ⛔ <b>The phrase "no shelf left" is load-bearing</b> — `TechniqueTests` asserts it, and
    /// §11's guard against *"the library is mandatory"* leans on the refusal being audible since
    /// D204 removed one of the three costs it used to rest on.
    /// </para>
    /// </remarks>
    private static string Kept(
        SimWorld world, TechniqueRow technique, Villager knower, Library? shelved)
    {
        if (shelved is not null)
        {
            int free = shelved.Shelves - shelved.Records.Count;
            return $"It is written down at {shelved.Name} now — {free} "
                + $"{(free == 1 ? "shelf" : "shelves")} left — so it will outlive "
                + $"{knower.Name}.";
        }

        if (world.Libraries.Count > 0)
        {
            return $"But there was no shelf left for {technique.Name}, so it lives only in "
                + $"{knower.Name}'s hands. Build another library, or it goes when its last "
                + "knower does.";
        }

        return world.HasLiteracy
            ? $"But the village has no library, so it lives only in {knower.Name}'s hands. "
                + "Build one, or it goes when its last knower does."
            : $"But nobody in the village can write yet, so it lives only in {knower.Name}'s "
                + "hands — and it goes when its last knower does.";
    }
}
