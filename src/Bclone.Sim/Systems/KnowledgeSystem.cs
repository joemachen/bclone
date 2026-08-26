using Bclone.Sim.Core;
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
                    + $"{world.Clock.SeasonAndYear()}.");
                world.KnowledgeStates[id] = KnowledgeState.Known;
                was = KnowledgeState.Known;
            }

            Villager? knower = FirstLivingMasterOf(world, technique.Skill);
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
                if (world.WriteDown(id) is Library shelved)
                {
                    world.KnowledgeStates[id] = KnowledgeState.Established;
                    world.Narrate($"{technique.Name} was written down at {shelved.Name} — "
                        + $"{shelved.Shelves - shelved.Records.Count} shelves left. "
                        + $"{world.Clock.SeasonAndYear()}.");

                    if (was == KnowledgeState.Unknown)
                    {
                        world.Narrate(Said(technique.DiscoveryLine, knower!.Name));
                    }

                    continue;
                }

                if (was == KnowledgeState.Unknown && world.Libraries.Count > 0)
                {
                    world.Narrate($"There was no shelf left for {technique.Name}. "
                        + "Build another library, or it goes when its last knower does. "
                        + $"{world.Clock.SeasonAndYear()}.");
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
                world.Narrate(Said(technique.DiscoveryLine, knower!.Name));
                continue;
            }

            // ⚠️ The person named here is the one who was last known to hold it — recorded when it
            // was learned rather than looked up now, because by this tick they are dead and no
            // longer in any list the scan can reach.
            world.Narrate(Said(technique.LostLine, world.LastKnowerOf(id)));
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
}
