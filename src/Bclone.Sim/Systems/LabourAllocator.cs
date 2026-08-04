using System.Text;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;

namespace Bclone.Sim.Systems;

/// <summary>
/// Decides who works where across the whole village, in one pass.
/// </summary>
/// <remarks>
/// <para>
/// Specified in <c>specs/labour-allocation.md</c>, which was written after three
/// improvised attempts each broke the village. The diagnosis those attempts earned:
/// <b>labour demand is a global allocation problem, and every attempt solved it with
/// local, per-workplace rules.</b> So the two questions are now separated —
/// <see cref="LabourQuota"/> answers "how much of each kind of work does the village
/// need?", and this answers "who does it, and where?".
/// </para>
/// <para>
/// <b>Internal on purpose.</b> There is no public way for a caller to assign a
/// villager to a workplace (D15); the Banished pattern this deletes should be
/// unexpressible, not merely discouraged, and a reflection test asserts it stays that
/// way.
/// </para>
/// <para>
/// <b>Greedy nearest-first, not optimal.</b> A minimum-total-travel matching would
/// need Hungarian-style assignment, and its output cannot be explained one villager at
/// a time — "you work here because it minimised a village-wide sum" fails the
/// legibility non-negotiable outright. Greedy-nearest fits in a sentence, and is what
/// a person would actually do.
/// </para>
/// <para>
/// <b>Cost-first, not villager-first.</b> Walking villagers in id order lets villager
/// #1 claim a distant site before villager #9 — who lives beside it — gets a look in.
/// Sorting the whole candidate list by cost means the shortest commutes are claimed
/// first, which is both a better allocation and an easier one to justify.
/// </para>
/// </remarks>
internal static class LabourAllocator
{
    /// <summary>One villager's claim on one workplace, and what it would cost them.</summary>
    /// <remarks>
    /// Sorted by <c>(cost, villagerId, workplaceId)</c> — a <b>total</b> order, so no
    /// two candidates ever compare equal and nothing is left to the sort's stability
    /// (spec §5). This is the largest ordering surface in the sim so far.
    /// </remarks>
    /// <param name="Rank">
    /// Which claims get looked at first, whatever they cost. <b>Zero for everything except a
    /// house the village marked for itself</b> (D102), which is one.
    /// </param>
    /// <param name="Cost">Travel cost from where this villager rests to the workplace.</param>
    /// <param name="VillagerId">Who is claiming it.</param>
    /// <param name="WorkplaceId">What they are claiming.</param>
    private readonly record struct Candidate(int Rank, int Cost, int VillagerId, int WorkplaceId);

    // ---------------------------------------------------------------
    //  The two entry points
    // ---------------------------------------------------------------

    /// <summary>
    /// Discard every assignment and share the work out again from scratch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Joe's call (D20), following <em>Banished</em>: rather than pinning people in
    /// place with rules, the village periodically re-runs the whole allocation, so
    /// workers drift toward the jobs nearest where they live. A hard "one forager per
    /// household" floor would be a constraint the player has to be <em>told</em>; a
    /// reshuffle is a behaviour they can <em>watch</em>. It also self-corrects the
    /// cases a floor cannot — a forager dying, or a family moving house.
    /// </para>
    /// <para>
    /// Which is why this clears first rather than patching incrementally. Cost-first
    /// ordering then does the rest by itself: a villager who moved closer to a site
    /// out-ranks a distant incumbent on the next pass without anyone having to notice.
    /// </para>
    /// </remarks>
    internal static void Reshuffle(SimWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        LabourQuota quota = LabourQuota.For(world);
        ReleaseUnfit(world);

        // Snapshot before clearing, so a job change can say what it changed from.
        // Churn that cannot explain itself is worse than no churn at all (D20).
        int[] before = new int[world.Villagers.Count];
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            before[i] = world.Villagers[i].WorkplaceId;
        }

        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            world.Workplaces[i].WorkerIds.Clear();
        }

        for (int i = 0; i < world.Villagers.Count; i++)
        {
            world.Villagers[i].WorkplaceId = 0;
        }

        Match(world, quota, before);
        ExplainTheIdle(world, quota, shedThisPass: null);
        NarrateChanges(world, before);

        world.Log(LogLevel.Debug, "labour", $"Work shared out again — {quota}");
    }

    /// <summary>
    /// Between reshuffles: let go of anyone who can no longer do the job, shed
    /// anyone the village no longer needs, and give the work to whoever is free.
    /// </summary>
    /// <remarks>
    /// <b>Why this exists alongside the annual reshuffle.</b> A child comes of age, a
    /// forager dies, a couple builds a house — and food is stored per household (D14),
    /// so a household left with no worker does not have a year to wait. The reshuffle
    /// is what moves people <em>closer</em>; this is what stops anyone sitting idle
    /// beside an opening in the meantime. It never moves a villager who already has a
    /// job, so the reason strings it writes stay true until the next reshuffle.
    /// </remarks>
    internal static void TakeUpSlack(SimWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        LabourQuota quota = LabourQuota.For(world);

        ReleaseUnfit(world);
        List<int> shed = ShedSurplus(world, quota);
        Match(world, quota, previousWorkplaces: null);
        ExplainTheIdle(world, quota, shed);
    }

    // ---------------------------------------------------------------
    //  The matching pass (spec §4b)
    // ---------------------------------------------------------------

    /// <summary>
    /// Fill each kind of work in turn, <b>the kind the village needs fewest of
    /// first</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One global cost-sorted pass over every workplace at once was the spec's design
    /// (§4b) and it has a bug the spec did not anticipate. A village of ten hands
    /// wants nine foragers and one logger. Sorted purely by cost, every villager
    /// near the tree stand takes an even nearer berry patch first, and the single
    /// timber job falls to whoever is left over at the end — who is by construction
    /// the most remote person in the village, and often cannot reach the stand at all.
    /// So the job went unfilled, no timber was cut, no houses were built, and the
    /// settlement aged out. It failed hardest exactly when catchment was tight, which
    /// is to say exactly where the pillar is supposed to work.
    /// </para>
    /// <para>
    /// Filling the scarce work first hands it to whoever genuinely lives nearest, and
    /// it costs food nothing: the quota has <em>already</em> decided that timber only
    /// gets hands the village can spare from eating. It is also the more explainable
    /// of the two — "Elias cuts timber because he lives nearest the stand" beats
    /// "Elias cuts timber because he was the last one left".
    /// </para>
    /// </remarks>
    private static void Match(SimWorld world, LabourQuota quota, int[]? previousWorkplaces)
    {
        JobKind[] order = KindsByScarcity(quota);
        for (int k = 0; k < order.Length; k++)
        {
            MatchOneKind(world, quota, previousWorkplaces, order[k]);
        }
    }

    /// <summary>Job kinds, fewest wanted first; ties keep the declared order.</summary>
    private static JobKind[] KindsByScarcity(LabourQuota quota)
    {
        var order = new JobKind[KindsInOrder.Length];
        System.Array.Copy(KindsInOrder, order, KindsInOrder.Length);

        // Insertion sort over two items, written generally so adding a third kind
        // does not quietly change the rule.
        for (int i = 1; i < order.Length; i++)
        {
            for (int j = i; j > 0 && quota.For(order[j]) < quota.For(order[j - 1]); j--)
            {
                (order[j], order[j - 1]) = (order[j - 1], order[j]);
            }
        }

        return order;
    }

    private static void MatchOneKind(SimWorld world, LabourQuota quota, int[]? previousWorkplaces, JobKind kind)
    {
        List<Candidate> candidates = BuildCandidates(world, kind);

        // Sorted by an explicit total ordering rather than relying on the sort being
        // stable — see the note on Candidate.
        candidates.Sort(static (a, b) =>
        {
            // ⭐ RANK BEFORE COST, AND ONLY BUILDING USES IT (D102). What the player marked
            // is staffed before what the village marked for itself — see BuildCandidates.
            int byRank = a.Rank.CompareTo(b.Rank);
            if (byRank != 0)
            {
                return byRank;
            }

            int byCost = a.Cost.CompareTo(b.Cost);
            if (byCost != 0)
            {
                return byCost;
            }

            int byVillager = a.VillagerId.CompareTo(b.VillagerId);
            return byVillager != 0 ? byVillager : a.WorkplaceId.CompareTo(b.WorkplaceId);
        });

        // Anyone who kept their job through an incremental pass already counts
        // against the quota; after a reshuffle this starts at zero.
        int held = CountHolding(world, kind);
        int wanted = quota.For(kind);

        for (int i = 0; i < candidates.Count && held < wanted; i++)
        {
            Candidate candidate = candidates[i];

            Villager? villager = world.FindVillager(candidate.VillagerId);
            if (villager is null || villager.HasJob)
            {
                continue;
            }

            Workplace? workplace = world.FindWorkplace(candidate.WorkplaceId);
            if (workplace is null || workplace.IsFull)
            {
                continue;
            }

            int previous = previousWorkplaces is null ? 0 : previousWorkplaces[IndexOf(world, villager)];
            Assign(world, villager, workplace, candidate.Cost, candidates, i, previous);
            held++;
        }
    }

    /// <summary>
    /// Every able, unemployed villager paired with every workplace of one kind within
    /// reach.
    /// </summary>
    /// <remarks>
    /// Built by walking villagers in id order and then workplaces in id order, so the
    /// list before sorting is itself a fact about the village rather than about
    /// iteration. Catchment is applied here, which is what stops anyone even being
    /// <em>considered</em> for a job across the valley.
    /// </remarks>
    private static List<Candidate> BuildCandidates(SimWorld world, JobKind kind)
    {
        var candidates = new List<Candidate>();

        // The build queue, once rather than per candidate — it sorts every unfinished site,
        // and asking it inside the loop below would do that for every villager.
        List<Workplace>? queue = kind == JobKind.Builder ? world.BuildQueue() : null;

        for (int v = 0; v < world.Villagers.Count; v++)
        {
            Villager villager = world.Villagers[v];
            if (!villager.CanWork || villager.HasJob)
            {
                continue;
            }

            GridPos home = world.RestingPlaceOf(villager);

            for (int w = 0; w < world.Workplaces.Count; w++)
            {
                Workplace workplace = world.Workplaces[w];
                if (workplace.Kind != kind)
                {
                    continue;
                }

                int cost = world.TravelCost.Cost(home, workplace.Position);

                if (cost > workplace.CatchmentRadius)
                {
                    continue;
                }

                candidates.Add(new Candidate(RankOf(queue, workplace), cost, villager.Id, workplace.Id));
            }
        }

        return candidates;
    }

    /// <summary>
    /// ⭐ What the player marked is staffed before what the village marked for itself (D102).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Measured, and this is the whole reason it exists.</b> Making houses real
    /// construction sites put two of them in front of the woodcutter's hut at the founding,
    /// because <c>HouseTheRoofless</c> marks a house for every roofless family on tick 4. The
    /// hut's timber then arrived at <b>t364</b> against a winter starting at t360, and the
    /// founding died — 2 alive, 2 frozen. The cost was not the work: with
    /// <c>home_work_ticks</c> set to zero the timeline did not move by one tick, because the
    /// bottleneck is the <em>timber a builder hauls</em>, and two houses' worth goes first.
    /// </para>
    /// <para>
    /// <b>It is a rule rather than a patch, and the policy already exists one file over.</b>
    /// <c>LabourQuota</c> says in as many words that <em>"heating is a floor alongside eating,
    /// and only house-building is funded from what is left"</em> — houses already yield to the
    /// fuel chain when hands are shared out. This says the same thing about the hands that
    /// build: a woodcutter's hut the player asked for outranks a house the village decided on
    /// by itself.
    /// </para>
    /// <para>
    /// <b>It is a priority, not an exclusion.</b> Once the marked buildings are staffed, the
    /// houses take whoever is left — so a village that has built what it was told to goes back
    /// to housing itself, and nobody is homeless forever because of a rule.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// <b>⭐ It is the site's place in <see cref="SimWorld.BuildQueue"/>, not a two-way
    /// player/village flag (D104).</b> The panel shows the player *"3rd of 5 — the granary is
    /// immediately ahead of it"*, and a number a player plans against has to be the number the
    /// village actually works to. A flag would have made the first claim true and the second
    /// one only usually true, which is the worse of both.
    /// </remarks>
    private static int RankOf(List<Workplace>? queue, Workplace workplace)
    {
        if (queue is null)
        {
            return 0;
        }

        for (int i = 0; i < queue.Count; i++)
        {
            if (queue[i].Id == workplace.Id)
            {
                return i;
            }
        }

        return queue.Count;
    }

    // ---------------------------------------------------------------
    //  Legibility (spec §6) — the phase's actual deliverable
    // ---------------------------------------------------------------

    private static void Assign(
        SimWorld world,
        Villager villager,
        Workplace workplace,
        int cost,
        List<Candidate> candidates,
        int index,
        int previousWorkplaceId)
    {
        villager.WorkplaceId = workplace.Id;
        workplace.WorkerIds.Add(villager.Id);

        Workplace? previous = previousWorkplaceId == 0 || previousWorkplaceId == workplace.Id
            ? null
            : world.FindWorkplace(previousWorkplaceId);

        var reason = new StringBuilder();
        reason.Append(previous is null ? "Took work at " : "Moved to ");
        reason.Append(workplace.Name);
        reason.Append(" — ");
        reason.Append(Tiles(cost));
        reason.Append(" tiles from home.");

        if (previous is not null)
        {
            int previousCost = CostBetween(world, villager, previous);
            reason.Append(previousCost > cost
                ? $" Closer than {previous.Name}, at {Tiles(previousCost)}."
                : $" {Capitalise(previous.Name)} was {Tiles(previousCost)} tiles away and had no room this year.");
        }

        AppendNearerPlaceTheyDidNotGet(world, villager, workplace, cost, reason);
        AppendRival(world, workplace, cost, candidates, index, reason);

        villager.JobReason = reason.ToString();

        // Into the audit log as well as onto the villager. The sentence on a villager
        // answers "why is she doing that?" for whoever is looking right now; the log
        // answers "why did the whole village rearrange itself in year 84?", which is a
        // question nobody can ask a UI panel a century later.
        world.LogVillager(LogLevel.Debug, villager, "labour", villager.JobReason);
    }

    /// <summary>
    /// "The berry patch was nearer (2) but already had its hands."
    /// </summary>
    /// <remarks>
    /// Without this, a villager assigned to the second-nearest site looks like a
    /// mistake. Naming the nearer place <em>and the constraint that excluded them from
    /// it</em> is the difference between a village that reads as inscrutable and one
    /// that reads as full.
    /// </remarks>
    private static void AppendNearerPlaceTheyDidNotGet(
        SimWorld world, Villager villager, Workplace taken, int cost, StringBuilder reason)
    {
        Workplace? nearest = NearestInCatchment(world, villager, out int nearestCost);
        if (nearest is null || nearest.Id == taken.Id || nearestCost >= cost)
        {
            return;
        }

        reason.Append(' ');
        reason.Append(Capitalise(nearest.Name));
        reason.Append(" was nearer at ");
        reason.Append(Tiles(nearestCost));
        reason.Append(nearest.IsFull
            ? " tiles, but already had its hands."
            : $" tiles, but the village has all the {Plural(nearest.Kind)} it needs.");
    }

    /// <summary>"Bess was equally close; the tie went to the elder claim."</summary>
    /// <remarks>
    /// "Why Otto and not Bess?" has to have an answer, or the assignment is opaque
    /// even when it is correct. The runner-up is the next candidate for this same
    /// workplace who is still without work — later in the sorted list, so by
    /// construction they lost to this one.
    /// </remarks>
    private static void AppendRival(
        SimWorld world, Workplace workplace, int cost, List<Candidate> candidates, int index, StringBuilder reason)
    {
        for (int j = index + 1; j < candidates.Count; j++)
        {
            if (candidates[j].WorkplaceId != workplace.Id)
            {
                continue;
            }

            Villager? rival = world.FindVillager(candidates[j].VillagerId);
            if (rival is null || rival.HasJob)
            {
                continue;
            }

            reason.Append(candidates[j].Cost == cost
                ? $" {rival.Name} was equally close; the tie went to the elder claim."
                : $" Next nearest was {rival.Name} at {Tiles(candidates[j].Cost)} tiles.");
            return;
        }
    }

    /// <summary>
    /// Every villager without work must be able to name the constraint that
    /// excluded them: catchment, capacity, or quota.
    /// </summary>
    /// <remarks>
    /// Three different sentences, because they mean three genuinely different things
    /// to a player — "build somewhere nearer", "you need another site", and "you have
    /// more hands than mouths" are three different next moves. A single "no work
    /// available" would collapse all of them into a shrug.
    /// </remarks>
    private static void ExplainTheIdle(SimWorld world, LabourQuota quota, List<int>? shedThisPass)
    {
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (!villager.CanWork || villager.HasJob)
            {
                continue;
            }

            // Someone shed a moment ago already has a better, more specific reason
            // than anything this could write.
            if (shedThisPass is not null && shedThisPass.Contains(villager.Id))
            {
                continue;
            }

            Workplace? reachable = NearestInCatchment(world, villager, out _);
            if (reachable is null)
            {
                Workplace? closest = NearestAnywhere(world, villager, out int closestCost);
                villager.JobReason = closest is null
                    ? "No work: there is nowhere in the valley to work."
                    : $"No work: nothing within reach of home — the nearest is {closest.Name}, " +
                      $"{Tiles(closestCost)} tiles away and outside its catchment of " +
                      $"{Tiles(closest.CatchmentRadius)} tiles.";
                continue;
            }

            // Reported against the NEAREST place they could reach, not against the
            // village as a whole. Asking "is every reachable workplace full?" gave the
            // wrong answer whenever the patch on their doorstep was full and a distant
            // stand merely had no quota — technically not all full, so they were told
            // the village had enough hands, when what they actually needed was another
            // patch nearby. The nearest opening is the one worth acting on.
            //
            // But "nearest" alone is not enough either, and the second version of this
            // produced a sentence that contradicted itself: with one full berry patch
            // and a tree stand the village wanted nobody at, the stand happened to be
            // a tile closer, so three idle villagers were told "the village has all
            // the hands it needs — 4 foraging", while exactly one of them was
            // foraging. The village wanted four and had room for one.
            //
            // So: report the nearest place that is FULL AND STILL WANTED, because that
            // is the one the player can act on by building another. Only when no such
            // place exists does "we have enough hands" become the true answer.
            reachable = NearestFullAndWanted(world, villager, quota) ?? reachable;

            villager.JobReason = reachable.IsFull
                ? $"No work: {reachable.Name} is full — it has its {reachable.Capacity} " +
                  $"{(reachable.Capacity == 1 ? "hand" : "hands")}, and it is the nearest place " +
                  $"within reach of home."
                : $"No work: the village has all the hands it needs on the work that matters " +
                  $"— {quota}";

            world.LogVillager(LogLevel.Debug, villager, "labour", villager.JobReason);
        }
    }

    private static void NarrateChanges(SimWorld world, int[] before)
    {
        var movers = new List<string>();

        for (int i = 0; i < before.Length && i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (before[i] == 0 || villager.WorkplaceId == 0 || before[i] == villager.WorkplaceId)
            {
                continue;
            }

            Workplace? now = world.FindWorkplace(villager.WorkplaceId);
            if (now is not null)
            {
                movers.Add($"{villager.Name} to {now.Name}");
            }
        }

        // The quota that produced this pass, whether or not anybody moved. Logged
        // BEFORE the early return on purpose: "nobody changed jobs this year" is itself
        // something an audit needs explained, and the demand is the explanation.
        world.Log(LogLevel.Debug, "labour", $"The village wants: {LabourQuota.For(world)}");

        if (movers.Count == 0)
        {
            return;
        }

        // One line, not one per mover. A fifty-year village reshuffles fifty times,
        // and narrating each individual move would bury the story the same way
        // narrating each gather did (D9). The per-villager reason is on the villager.
        string who = movers.Count <= 3
            ? string.Join("; ", movers)
            : $"{movers.Count} villagers changed work";

        world.Narrate($"Work was shared out again — {world.Clock.SeasonAndYear()}. {who}.");
    }

    // ---------------------------------------------------------------
    //  Losing a job
    // ---------------------------------------------------------------

    /// <summary>
    /// Give up jobs held by the dead, by children, and by anyone whose home has moved
    /// out of catchment.
    /// </summary>
    private static void ReleaseUnfit(SimWorld world)
    {
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (!villager.HasJob)
            {
                continue;
            }

            Workplace? workplace = world.FindWorkplace(villager.WorkplaceId);
            if (workplace is null)
            {
                Release(villager, "No work: their workplace no longer exists.");
                continue;
            }

            if (!villager.CanWork)
            {
                workplace.WorkerIds.Remove(villager.Id);
                Release(villager, villager.Alive ? "No work: too young to work." : "No work: died.");
                continue;
            }

            if (!InCatchment(world, villager, workplace))
            {
                workplace.WorkerIds.Remove(villager.Id);
                Release(villager, $"No work: moved too far from {workplace.Name} to keep working there.");
            }
        }
    }

    /// <summary>
    /// When a quota shrinks, let the <b>furthest-travelling</b> worker go first.
    /// </summary>
    /// <remarks>
    /// Not the highest id, which is the tempting shortcut and is what the previous
    /// implementation did. The longest commute is the weakest claim, and — unlike an
    /// id — it is a reason that can be said out loud. Ties go to the higher id, so the
    /// newer claim yields to the older one; that secondary rule exists only to keep
    /// the ordering total.
    /// </remarks>
    internal static List<int> ShedSurplus(SimWorld world, LabourQuota quota)
    {
        var shed = new List<int>();

        for (int k = 0; k < KindsInOrder.Length; k++)
        {
            JobKind kind = KindsInOrder[k];
            int held = CountHolding(world, kind);
            int wanted = quota.For(kind);

            while (held > wanted)
            {
                Villager? furthest = null;
                int furthestCost = -1;

                for (int i = 0; i < world.Villagers.Count; i++)
                {
                    Villager villager = world.Villagers[i];
                    Workplace? workplace = villager.HasJob ? world.FindWorkplace(villager.WorkplaceId) : null;
                    if (workplace is null || workplace.Kind != kind)
                    {
                        continue;
                    }

                    int cost = CostBetween(world, villager, workplace);

                    // >= rather than >, walking in id order, hands a tie to the
                    // higher id without needing a second comparison.
                    if (cost >= furthestCost)
                    {
                        furthest = villager;
                        furthestCost = cost;
                    }
                }

                if (furthest is null)
                {
                    break;
                }

                world.FindWorkplace(furthest.WorkplaceId)!.WorkerIds.Remove(furthest.Id);

                // Wanting none of a kind of work is a different statement from wanting
                // fewer, and it is the one the player most needs to read: it is the
                // village choosing food over building, in the moment it chooses.
                Release(
                    furthest,
                    wanted == 0
                        ? $"No work: every hand went back to food — a household is going hungry, " +
                          $"so nothing is cut until the village is fed. Yours was the longest walk " +
                          $"at {Tiles(furthestCost)} tiles."
                        : $"No work: the village needs only {wanted} {Plural(kind)} for its " +
                          $"{quota.Mouths} mouths, and yours was the longest walk at " +
                          $"{Tiles(furthestCost)} tiles.");

                shed.Add(furthest.Id);
                held--;
            }
        }

        return shed;
    }

    private static void Release(Villager villager, string why)
    {
        villager.WorkplaceId = 0;
        villager.JobReason = why;
    }

    // ---------------------------------------------------------------
    //  Queries
    // ---------------------------------------------------------------

    /// <summary>Job kinds, in the order the village cares about them.</summary>
    private static readonly JobKind[] KindsInOrder =
        { JobKind.Forager, JobKind.Forester, JobKind.Woodcutter, JobKind.Marketer, JobKind.Builder };

    private static int CountHolding(SimWorld world, JobKind kind)
    {
        int count = 0;
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (villager.HasJob && world.FindWorkplace(villager.WorkplaceId)?.Kind == kind)
            {
                count++;
            }
        }

        return count;
    }

    private static Workplace? NearestInCatchment(SimWorld world, Villager villager, out int cost)
    {
        Workplace? best = null;
        cost = int.MaxValue;

        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            Workplace workplace = world.Workplaces[i];
            int candidate = CostBetween(world, villager, workplace);

            if (candidate <= workplace.CatchmentRadius && candidate < cost)
            {
                best = workplace;
                cost = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// The nearest reachable workplace that is full <em>and</em> whose kind the village
    /// still wants more of — the refusal a player can actually do something about.
    /// </summary>
    private static Workplace? NearestFullAndWanted(
        SimWorld world, Villager villager, LabourQuota quota)
    {
        Workplace? best = null;
        int bestCost = int.MaxValue;

        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            Workplace workplace = world.Workplaces[i];
            if (!workplace.IsFull || CountHolding(world, workplace.Kind) >= quota.For(workplace.Kind))
            {
                continue;
            }

            int candidate = CostBetween(world, villager, workplace);
            if (candidate <= workplace.CatchmentRadius && candidate < bestCost)
            {
                best = workplace;
                bestCost = candidate;
            }
        }

        return best;
    }

    private static Workplace? NearestAnywhere(SimWorld world, Villager villager, out int cost)
    {
        Workplace? best = null;
        cost = int.MaxValue;

        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            int candidate = CostBetween(world, villager, world.Workplaces[i]);
            if (candidate < cost)
            {
                best = world.Workplaces[i];
                cost = candidate;
            }
        }

        return best;
    }

    private static int IndexOf(SimWorld world, Villager villager)
    {
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            if (ReferenceEquals(world.Villagers[i], villager))
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>
    /// Travel cost from a villager's <em>home</em> to a workplace, from the one shared
    /// field (DESIGN.md §2.6).
    /// </summary>
    /// <remarks>
    /// From home, not from wherever they happen to be standing. A job is a daily
    /// commute, and measuring from their current position would make assignment
    /// flicker as people walk about.
    /// </remarks>
    internal static int CostBetween(SimWorld world, Villager villager, Workplace workplace) =>
        world.TravelCost.Cost(world.RestingPlaceOf(villager), workplace.Position);

    internal static bool InCatchment(SimWorld world, Villager villager, Workplace workplace) =>
        CostBetween(world, villager, workplace) <= workplace.CatchmentRadius;

    private static int Tiles(int cost) => cost / TravelCostField.BaseTileCost;

    private static string Plural(JobKind kind) => kind switch
    {
        JobKind.Forager => "foragers",
        JobKind.Forester => "foresters",
        JobKind.Woodcutter => "woodcutters",
        JobKind.Marketer => "traders",
        JobKind.Builder => "builders",
        _ => "workers",
    };

    /// <summary>Place names read "the berry patch"; sometimes one has to start a sentence.</summary>
    private static string Capitalise(string text) =>
        string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
