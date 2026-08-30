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
    /// <param name="Cost">Travel cost from where this villager rests to the workplace.</param>
    /// <param name="VillagerId">Who is claiming it.</param>
    /// <param name="WorkplaceId">What they are claiming.</param>
    /// <param name="Pinned">Whether the player has kept this villager on this trade.</param>
    /// <remarks>
    /// It carried a <c>Rank</c> as well until D108 — <em>"what the player marked is staffed
    /// before a house the village marked for itself"</em> (D102), expressed as a site's place
    /// in the build queue (D104). <b>Construction sites are no longer staffed at all</b>, so
    /// there is nothing here to rank: builders hold a job at the hut and walk out to
    /// <see cref="SimWorld.NextToBuild"/>, which is the head of that same queue. The
    /// guarantee moved to the errand rather than being dropped.
    /// </remarks>
    private readonly record struct Candidate(int Cost, int VillagerId, int WorkplaceId, bool Pinned);

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
        MakeRoomForPins(world, quota);

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
            if (world.Villagers[i].WorkplaceId != 0)
            {
                world.Villagers[i].LastWorkplaceId = world.Villagers[i].WorkplaceId;
            }

            world.Villagers[i].WorkplaceId = 0;

            // Cleared with the job, so a reshuffle that gives somebody a shorter walk does
            // not leave last year's complaint about the road on them.
            world.Villagers[i].CommuteNote = string.Empty;
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
        MakeRoomForPins(world, quota);
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
            // ⭐⭐ A PIN OUTRANKS THE WALK, AND THIS IS THE LAST OF THE FIVE PIECES.
            //
            // ⛔ Displacing the incumbent was not enough on its own: `MakeRoomForPins` let go of
            // Ambrose, and the cost sort — doing exactly its job — **hired him straight back**
            // ahead of the person the player had pinned, because he lived nearer. Four correct
            // mechanisms and the feature still measured **0 of 4,311 ticks**.
            //
            // ⚠️ **This is the one place cost is not the first question**, and it is a deliberate
            // hole in D58's rule rather than an oversight: the player has said who does this, so
            // the walk is a consequence rather than a criterion. **The village still chooses
            // WHICH hut** — cost decides that, one line down — which is what keeps §2.2 intact.
            if (a.Pinned != b.Pinned)
            {
                return a.Pinned ? -1 : 1;
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

            // ⭐ THE SNAPSHOT FIRST, THE LONGER MEMORY SECOND. Within one pass the snapshot is
            // the truth — it is what they held when the pass began. `LastWorkplaceId` is what
            // is left when a winter has already emptied the huts, which is most of the time
            // since D262 (see `Villager.LastWorkplaceId`).
            int snapshot = previousWorkplaces is null ? 0 : previousWorkplaces[IndexOf(world, villager)];
            int previous = snapshot != 0 ? snapshot : villager.LastWorkplaceId;
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

        for (int v = 0; v < world.Villagers.Count; v++)
        {
            Villager villager = world.Villagers[v];
            if (!villager.CanWork || villager.HasJob)
            {
                continue;
            }

            // ⭐⭐ SOMEBODY THE PLAYER HAS KEPT ON ANOTHER TRADE IS NOT A CANDIDATE FOR THIS ONE.
            //
            // ⛔ THE SHED PASS ALONE IS NOT ENOUGH, and this is the half that would have been
            // missed: `Reshuffle` tears EVERY allocation down before rebuilding, so on that tick
            // a pinned villager holds no job and looks exactly like anybody else. Refusing to
            // shed them protects the slack pass; refusing to offer them elsewhere is what
            // survives the reshuffle.
            //
            // ⚠️ It does not force them INTO their trade — the quota floor above makes the seat
            // wanted, and the ordinary cost-first match then fills it with the person who is
            // available for it, which by this line is them. **The village still picks the hut.**
            if (villager.IsPinned && villager.PinnedTrade != kind)
            {
                continue;
            }

            GridPos home = world.RestingPlaceOf(villager);

            for (int w = 0; w < world.Workplaces.Count; w++)
            {
                Workplace workplace = world.Workplaces[w];

                // ⭐ A CONSTRUCTION SITE IS NOT SOMEWHERE ANYBODY WORKS (D108). Builders
                // hold their job at the hut and walk out to the head of the build queue as
                // an errand, so a site must never be offered as a claim — it has no seats
                // to give, and offering it would mean a villager whose whole livelihood
                // vanished the tick the building was finished.
                if (workplace.Kind != kind || workplace.IsSite)
                {
                    continue;
                }

                int cost = world.TravelCost.Cost(home, workplace.Position);

                // ⭐ THE FENCE IS GONE (`forests-and-gathering.md §3`, Joe's third call). This
                // was `cost > workplace.CatchmentRadius`, a hard cutoff at ten tiles, and it
                // is now *can they get there at all*.
                //
                // **Reachability is not the same fence made bigger.** Catchment said "too far
                // is forbidden"; this says "no route is impossible", which is a fact about the
                // valley rather than a rule about the village. What used to be a refusal is a
                // cost now: the candidates are sorted cost-first, so a nearer workplace still
                // wins — the walk shapes the answer instead of bounding it. **That is D58's
                // settled mechanism arriving at last** (spec §3.2).
                //
                // ⚠️ The unreachable check must stay. Since D40 the river is impassable and
                // `Unreachable` is a sentinel rather than a big number, so dropping this
                // would not merely allow long walks — it would assign people to workplaces on
                // the far bank they can never arrive at, which is D110's seed 11 by another
                // door.
                if (cost == TravelCostField.Unreachable)
                {
                    continue;
                }

                candidates.Add(new Candidate(cost, villager.Id, workplace.Id, villager.PinnedTrade == kind));
            }
        }

        return candidates;
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
        villager.CommuteNote = DescribeTheCommute(world, workplace, cost);

        // Into the audit log as well as onto the villager. The sentence on a villager
        // answers "why is she doing that?" for whoever is looking right now; the log
        // answers "why did the whole village rearrange itself in year 84?", which is a
        // question nobody can ask a UI panel a century later.
        world.LogVillager(LogLevel.Debug, villager, "labour", villager.JobReason);
    }

    /// <summary>
    /// What the walk to work costs, said out loud — <b>and only when it costs enough</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ This is the condition Joe's third call carried</b> (D112, spec §7.1). Catchment
    /// used to make a ruinous commute impossible; deleting it makes one <em>silent</em>, and a
    /// village that thins out with nothing on screen saying why is §1.1 failing.
    /// </para>
    /// <para>
    /// <b>⚠️ THE THRESHOLD IS THE ECONOMY'S OWN BUDGET, AND THE FIRST VERSION GOT THIS BADLY
    /// WRONG.</b> It said the walk was worth mentioning past a third of the working day, on
    /// the assumption that would be rare — and measurement said otherwise: <b>a five-tile
    /// commute is already 76% of the day</b>, because walking dominates gathering in this
    /// economy. Every working villager got a note, which is a note that says nothing about
    /// anybody.
    /// </para>
    /// <para>
    /// So the anchor is <see cref="VillageEconomy.MaxHomeToWorkTiles"/> — <b>the walk the
    /// village's food supply is actually derived against</b> (D16). Inside it there is
    /// nothing to say: the economy planned for this person. Beyond it they are costing the
    /// village more than it budgeted, which is precisely the consequence that deleting
    /// catchment introduced and precisely what §7.1 requires be visible.
    /// </para>
    /// <para>
    /// <b>And it says what it costs, not how far it is.</b> "Nineteen tiles" is a number the
    /// player must convert into a consequence; <em>"brings back about a third of what a
    /// nearer pair of hands would"</em> is the consequence. The same shape as the gatherer
    /// hut's own sentence (D112), because it is the same kind of fact.
    /// </para>
    /// </remarks>
    private static string DescribeTheCommute(SimWorld world, Workplace workplace, int cost)
    {
        int tiles = Tiles(cost);
        int budget = VillageEconomy.MaxHomeToWorkTiles(world.Config);
        if (tiles <= budget)
        {
            return string.Empty;
        }

        // Trips are what a year of work actually produces, and a trip is the walk there and
        // back plus the work at the end. Somebody on the doorstep is the yardstick.
        int walking = world.TravelCost.TicksForCost(cost) * 2;
        int atTheDoor = world.Config.GatherTicks;
        int theirs = walking + atTheDoor;

        int share = theirs <= 0 ? 100 : atTheDoor * 100 / theirs;

        return $"It is {tiles} tiles to {workplace.Name} — beyond the {budget} the village's "
            + $"food is budgeted for, so they bring back about {share}% of what a pair of "
            + "hands at the door would.";
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
        Workplace? nearest = NearestReachable(world, villager, out int nearestCost);
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
            : $" tiles, but the village has all the {Plural(world, nearest.Kind)} it needs.");
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

            // ⭐⭐ PINNED TO A TRADE THE VILLAGE HAS NOWHERE TO DO. This sentence was written while
            // chasing a red check that turned out to have four other causes — the hypothesis was
            // wrong about that bug and right that the state exists and said nothing.
            //
            // ⛔ **A player can make that same mistake in one click** — keep somebody on forestry
            // before building the hut — and would have got the same silence. It is the player's
            // own standing order starving them, which is the most answerable kind of idleness
            // there is and was the least explained.
            if (villager.IsPinned && !AnySeatFor(world, villager.PinnedTrade!.Value))
            {
                villager.JobReason =
                    $"No work: you keep {villager.Name} on "
                    + $"{world.JobsCatalog.NameOf(villager.PinnedTrade.Value)}, and the village "
                    + "has nowhere to do it. Build somewhere, or hand them back.";
                world.LogVillager(LogLevel.Debug, villager, "labour", villager.JobReason);
                continue;
            }

            Workplace? reachable = NearestReachable(world, villager, out _);
            if (reachable is null)
            {
                // ⚠️ THE ONLY WAY TO REACH THIS SINCE THE FENCE CAME DOWN IS WATER. It used
                // to mean "everything is further than its catchment"; now it means there is
                // no walk at all from this doorstep to any workplace in the valley — rarer,
                // and much more serious, so it says a different thing.
                //
                // ⚠️ And it quotes the distance a CROW would fly, deliberately. The nearest
                // workplace here is by definition unreachable, so its travel cost is the
                // `Unreachable` sentinel and printing it would put a nonsense number in a
                // sentence whose whole job is being true.
                Workplace? closest = NearestAnywhere(world, villager, out _);
                villager.JobReason = closest is null
                    ? "No work: there is nowhere in the valley to work."
                    : $"No work: there is no way to walk from home to any of it. The nearest, "
                      + $"{closest.Name}, is {world.RestingPlaceOf(villager).ManhattanDistanceTo(closest.Position)} "
                      + "tiles off in a straight line — and the water is in the way.";
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

            // ⛔⛔ AND "ALL THE HANDS IT NEEDS" WAS FLATLY FALSE WHENEVER THE PLAYER'S OWN
            // NUMBER WAS THE CAP (2026-08-27). Joe's Year-44 log says it **1,129 times**, and
            // the sentence contradicts itself inside its own punctuation:
            //
            //   "the village has all the hands it needs on the work that matters
            //    — 16 hands for 21 mouths: 2 foraging (at least 4 to feed everyone), 0 cutting."
            //
            // **He had set the forager number to 2 himself**, against seven seats standing
            // empty. So the claim was about NEED while the number came from the player, and the
            // clause that followed named the very shortfall the claim denied. Ten of sixteen
            // adults stood idle while the village went hungry, and the sentence said everything
            // was fine.
            //
            // ⭐ The remedy is one click away and was nowhere near the sentence: **raise the
            // number.** A refusal that does not name its own cause is the silent stall §1.1
            // forbids — the same argument D146 makes about a limit reading as "nothing to do".
            //
            // ⚠️ Claimed only when the player's figure is genuinely the binding one. `Asked`
            // returns `min(asked, seats, hands)`, so `asked <= Foragers` is what says their
            // number won rather than the seats or the roster.
            string? askedTooFew = null;
            if (world.JobLimits.For(JobKind.Forager) is int askedFor
                && askedFor <= quota.Foragers
                && quota.Foragers < quota.ForagersToFeedEveryone)
            {
                askedTooFew =
                    $"No work: you asked the village for {askedFor} on gathering, and it needs "
                    + $"{quota.ForagersToFeedEveryone} to feed {quota.Mouths}. Raise the number "
                    + "on gathering, or these hands have nothing to do while the village goes "
                    + "short.";
            }

            villager.JobReason = askedTooFew
                ?? (reachable.IsFull
                    ? $"No work: {reachable.Name} is full — it has its {reachable.Capacity} " +
                      $"{(reachable.Capacity == 1 ? "hand" : "hands")}, and it is the nearest place " +
                      $"within reach of home."
                    : $"No work: the village has all the hands it needs on the work that matters " +
                      $"— {quota}");

            world.LogVillager(LogLevel.Debug, villager, "labour", villager.JobReason);
        }
    }

    private static void NarrateChanges(SimWorld world, int[] before)
    {
        var movers = new List<string>();

        for (int i = 0; i < before.Length && i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (before[i] == villager.WorkplaceId)
            {
                continue;
            }

            // ⭐⭐ THE POOL COUNTS AS SOMEWHERE TO GO, AND D262 IS WHY IT HAD TO (Joe's two-seat
            // cap). This used to skip anybody whose id was zero on either side — *only a move
            // from one building to another was a move* — and that was fine while the quota
            // soaked every spare hand into the berry patch, because hands really did travel
            // building to building.
            //
            // ⛔ WITH TWO SEATS A HUT THEY DO NOT. A hand that cannot be seated goes back to the
            // labourers, and comes back to the same hut next spring — job → 0 → the same job,
            // which the old test skipped at both ends. **Measured: not one narrated reshuffle in
            // a hundred and fifty years**, and the per-villager reason never once said "Moved
            // to". The village was reorganising itself every single year and saying nothing,
            // which is precisely what D20 forbids: *a reshuffle that cannot explain itself is
            // worse than no reshuffle.*
            if (villager.WorkplaceId == 0)
            {
                if (!villager.CanWork)
                {
                    // Retired, or died, or grew too old for it — that is a life event and the
                    // village narrates it elsewhere. Reporting it as a labour move would put
                    // the same death in the log twice, wearing a job title.
                    continue;
                }

                movers.Add($"{villager.Name} to the labourers");
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
    /// <summary>Let go of an unpinned worker so somebody the player kept can take the seat.</summary>
    /// <remarks>
    /// <para>
    /// <b>⛔⛔ THE HALF THAT THREE EARLIER FIXES ALL MISSED, AND THE RED CHECK HAD TO SAY IT THREE
    /// TIMES.</b> Refusing to shed a pinned villager, refusing to offer them another trade, and
    /// flooring the quota at the pin count are each necessary and **together still not enough**:
    /// if an *unpinned* villager already holds the only seat, the trade sits exactly at quota,
    /// <see cref="ShedSurplus"/> never fires (it only sheds when <em>over</em>), and the pinned
    /// villager is locked out for ever. Measured: <b>0 of 4,311 ticks</b>, with the hut showing
    /// free places and the quota wanting one.
    /// </para>
    /// <para>
    /// ⭐ <b>A pin is a CLAIM on the trade, not merely a refusal to be moved off it</b> — that is
    /// what Joe asked for (*"lock that person to that trade"*), and half of it is worthless.
    /// The unpinned holder who gives way is <b>the furthest-travelling</b>, exactly as
    /// <see cref="ShedSurplus"/> chooses, so the village stays consistent about who moves.
    /// </para>
    /// <para>
    /// ⚠️ <b>It can never displace another pinned villager</b>, so two pins on a one-seat trade
    /// settle rather than fight: the second simply waits, and `ExplainTheIdle` says why.
    /// </para>
    /// </remarks>
    private static void MakeRoomForPins(SimWorld world, LabourQuota quota)
    {
        for (int k = 0; k < KindsInOrder.Length; k++)
        {
            JobKind kind = KindsInOrder[k];

            int waiting = 0;
            for (int i = 0; i < world.Villagers.Count; i++)
            {
                Villager villager = world.Villagers[i];
                if (villager.CanWork && !villager.HasJob && villager.PinnedTrade == kind)
                {
                    waiting++;
                }
            }

            // ⚠️⚠️ THE ROOM THAT MATTERS IS IN THE QUOTA, NOT IN THE BUILDING — and the first
            // draft of this asked about seats and did nothing at all. The forester's hut had two
            // places and one worker, so there *was* a free seat; but `MatchOneKind` stops at
            // `held < wanted` and the village wanted one forester, which Ambrose already was.
            // **Dorcas waited nine years beside an empty chair.**
            //
            // ⭐ *"Is there a spare seat?"* and *"does the village want another of these?"* are
            // different questions, and only the second one gates hiring.
            while (waiting > 0
                && (CountHolding(world, kind) >= quota.For(kind) || !AnyFreeSeatFor(world, kind)))
            {
                Villager? furthest = null;
                int furthestCost = -1;

                for (int i = 0; i < world.Villagers.Count; i++)
                {
                    Villager villager = world.Villagers[i];
                    if (villager.IsPinned || !villager.HasJob)
                    {
                        continue;
                    }

                    Workplace? workplace = world.FindWorkplace(villager.WorkplaceId);
                    if (workplace is null || workplace.Kind != kind)
                    {
                        continue;
                    }

                    int cost = CostBetween(world, villager, workplace);
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
                Release(
                    furthest,
                    $"No work: you keep somebody else on {world.JobsCatalog.NameOf(kind)}, and "
                        + $"{furthest.Name} had the longest walk to it.");
                waiting--;
            }
        }
    }

    /// <summary>Whether any workplace of this kind has a seat going spare.</summary>
    private static bool AnyFreeSeatFor(SimWorld world, JobKind kind)
    {
        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            Workplace workplace = world.Workplaces[i];
            if (workplace.Kind == kind && !workplace.IsSite && !workplace.IsFull)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether anywhere in the village does this trade at all.</summary>
    /// <remarks>
    /// <b>A standing site does not count.</b> Nobody is ever posted to a construction site (D108),
    /// so a half-built forester's hut is not somewhere a pinned forester can work — and telling
    /// the player it is would be the reassuring-but-wrong sentence D237 spent a decision on.
    /// </remarks>
    private static bool AnySeatFor(SimWorld world, JobKind kind)
    {
        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            if (world.Workplaces[i].Kind == kind && !world.Workplaces[i].IsSite)
            {
                return true;
            }
        }

        return false;
    }

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

            if (!CanReach(world, villager, workplace))
            {
                workplace.WorkerIds.Remove(villager.Id);
                Release(villager, $"No work: moved too far from {workplace.Name} to keep working there.");
                continue;
            }

            // ⭐⭐ AND SOMEBODY THE PLAYER HAS KEPT ON A TRADE LETS GO OF ANY OTHER ONE.
            //
            // ⛔ THE RED CHECK FOUND THIS AND NOTHING ELSE WOULD HAVE. Pinning Dorcas to forestry
            // while she held a forager's job did **nothing at all** — 0 of 4,311 ticks on the
            // pinned trade. `BuildCandidates` only ever offers work to somebody with **no** job,
            // and `ShedSurplus` only releases people from a trade that is **over quota**; hers
            // was not. So she sat in the wrong trade for ever, and both halves of the feature
            // looked correct in isolation.
            //
            // ⭐ This is the third arm of the same idea the two above it express: *let go of
            // anyone who can no longer do this job.* A pin makes the job wrong for them in
            // exactly the way distance or age does — **the reason is the player rather than the
            // world, and the mechanism is identical.**
            if (villager.IsPinned && villager.PinnedTrade != workplace.Kind)
            {
                workplace.WorkerIds.Remove(villager.Id);
                Release(
                    villager,
                    $"No work: you keep {villager.Name} on "
                        + $"{world.JobsCatalog.NameOf(villager.PinnedTrade!.Value)}, so they have "
                        + $"left {workplace.Name}.");
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

                    // ⭐⭐ A PINNED VILLAGER IS NEVER THE ONE SHED. This is the whole of what the
                    // player bought: the reshuffle moves people every three years and the slack
                    // pass every sixty ticks, and *"Hattie is a forager"* has to survive both or
                    // the control is decoration.
                    //
                    // ⚠️ AND IT CANNOT SPIN. `while (held > wanted)` would loop for ever if every
                    // holder were pinned, so the `furthest is null` break below is now
                    // load-bearing rather than defensive — it is the exit when the only people
                    // left on this trade are ones the player keeps there. `LabourQuota` also
                    // floors the trade at its pin count, so the two agree rather than fight.
                    if (villager.IsPinned && villager.PinnedTrade == kind)
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
                        : $"No work: the village needs only {wanted} {Counted(world, kind, wanted)} for its " +
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
        // Remembered before it is thrown away, so next year's sentence can name the place they
        // were let go from. See `Villager.LastWorkplaceId` — a winter used to erase this.
        if (villager.WorkplaceId != 0)
        {
            villager.LastWorkplaceId = villager.WorkplaceId;
        }

        villager.WorkplaceId = 0;
        villager.JobReason = why;

        // Somebody with no job has no commute. Leaving the note behind would have the panel
        // telling a resting villager how much of their day the road takes.
        villager.CommuteNote = string.Empty;
    }

    // ---------------------------------------------------------------
    //  Queries
    // ---------------------------------------------------------------

    /// <summary>Job kinds, in the order the village cares about them.</summary>
    private static readonly JobKind[] KindsInOrder =
    {
        JobKind.Forager, JobKind.Farmer, JobKind.Forester, JobKind.Woodcutter,
        JobKind.Marketer, JobKind.Builder,
    };

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

    /// <remarks>
    /// <b>Construction sites are skipped here as well as in <see cref="BuildCandidates"/>,
    /// and this half is about sentences rather than about jobs</b> (D108). These readers feed
    /// <see cref="ExplainTheIdle"/>, so a site left in would have an idle villager told
    /// <em>"the granary (building) is full — it has its 0 hands"</em>: true of a place nobody
    /// can ever work, and useless to act on.
    /// </remarks>
    private static Workplace? NearestReachable(SimWorld world, Villager villager, out int cost)
    {
        Workplace? best = null;
        cost = int.MaxValue;

        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            Workplace workplace = world.Workplaces[i];
            if (workplace.IsSite)
            {
                continue;
            }

            int candidate = CostBetween(world, villager, workplace);

            if (candidate != TravelCostField.Unreachable && candidate < cost)
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
            if (workplace.IsSite
                || !workplace.IsFull
                || CountHolding(world, workplace.Kind) >= quota.For(workplace.Kind))
            {
                continue;
            }

            int candidate = CostBetween(world, villager, workplace);
            if (candidate != TravelCostField.Unreachable && candidate < bestCost)
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
            if (world.Workplaces[i].IsSite)
            {
                continue;
            }

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

    /// <summary>Whether any walk at all gets this villager from home to that work.</summary>
    /// <remarks>
    /// <b>This was <c>InCatchment</c></b>, and the rename is the change: since the fence came
    /// down the only thing that disqualifies a workplace is having no route to it.
    /// </remarks>
    internal static bool CanReach(SimWorld world, Villager villager, Workplace workplace) =>
        CostBetween(world, villager, workplace) != TravelCostField.Unreachable;

    private static int Tiles(int cost) => cost / TravelCostField.BaseTileCost;

    // ⭐ Six arms naming six trades; the plural is a column on the row now (D218). It is a
    // column of its own rather than the name plus an "s" because the marketer is "traders"
    // here and "marketer" on the roster — D188 unresolved, and not this row's to settle.
    private static string Plural(SimWorld world, JobKind kind) => world.JobsCatalog.PluralOf(kind);

    /// <summary>The trade's name, singular or plural to match a count.</summary>
    /// <remarks>
    /// <b>Found by a guard that was looking for something else</b> (2026-08-27): a village capped
    /// at one forager read <i>"the village needs only 1 foragers for its 10 mouths"</i>. Both
    /// words come from the catalogue row, so this stays data-driven — <c>NameOf</c> is the
    /// singular the row already carries, not an "s" chopped off the plural, which is the kind of
    /// guess that breaks the moment a mod adds a trade whose plural is irregular.
    /// </remarks>
    private static string Counted(SimWorld world, JobKind kind, int count) =>
        count == 1 ? world.JobsCatalog.NameOf(kind) : world.JobsCatalog.PluralOf(kind);

    /// <summary>Place names read "the berry patch"; sometimes one has to start a sentence.</summary>
    private static string Capitalise(string text) =>
        string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
