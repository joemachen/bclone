using Bclone.Sim.Core;

namespace Bclone.Sim.World;

/// <summary>
/// How many hands the village wants on each kind of work, this year.
/// </summary>
/// <remarks>
/// <para>
/// This is the half of labour demand that a single workplace cannot know.
/// <see cref="Workplace.Capacity"/> says how many people fit at the berry patch;
/// this says how many the <em>village</em> wants foraging at all. Conflating the two
/// is the mistake <c>specs/labour-allocation.md §3</c> is a record of — four
/// different values of one workplace field, each of which broke the village in a
/// different way, because no value of a local field can express a global constraint.
/// </para>
/// <para>
/// <b>The whole policy, in one sentence: a village short of hands feeds itself before
/// it builds.</b> That sentence is the test of whether this is legible, and it is the
/// only thing the quota decides.
/// </para>
/// <para>
/// Derived, never tuned — the same discipline as <see cref="VillageEconomy"/> (D16).
/// Every number below comes from a count of people, so a village that grows changes
/// its own mind about what it needs without anyone touching a config file.
/// </para>
/// </remarks>
public readonly record struct LabourQuota
{
    /// <remarks>
    /// Internal rather than private so tests can pose a quota the village would not
    /// currently produce — "what happens when the village suddenly wants one fewer
    /// forager?" is otherwise only reachable by killing the right person.
    /// </remarks>
    internal LabourQuota(
        int hands,
        int mouths,
        int foragersToFeedEveryone,
        int foragers,
        int foresters,
        int woodcutters,
        int marketers = 0,
        int builders = 0,
        int farmers = 0)
    {
        Builders = builders;
        Hands = hands;
        Mouths = mouths;
        ForagersToFeedEveryone = foragersToFeedEveryone;
        Foragers = foragers;
        Foresters = foresters;
        Woodcutters = woodcutters;
        Marketers = marketers;
        Farmers = farmers;
    }

    /// <summary>Villagers able to do a day's work.</summary>
    public int Hands { get; }

    /// <summary>Villagers who eat — which is everyone, children included.</summary>
    public int Mouths { get; }

    /// <summary>
    /// Foragers the village must have before anyone is spared for timber.
    /// </summary>
    /// <remarks>
    /// <see cref="VillageEconomy.RequiredDependants"/> is what one adult can carry at
    /// their weakest, so dividing the mouths by it gives the hands that guarantee
    /// everyone eats even in the worst case. Rounded up, and never below one: a
    /// village with anyone left in it needs feeding.
    /// </remarks>
    public int ForagersToFeedEveryone { get; }

    /// <summary>Hands the village wants foraging.</summary>
    public int Foragers { get; }

    /// <summary>Hands the village wants felling trees.</summary>
    public int Foresters { get; }

    /// <summary>Hands the village wants splitting logs into firewood.</summary>
    public int Woodcutters { get; }

    /// <summary>Hands the village wants working the market (D14).</summary>
    public int Marketers { get; }

    /// <summary>Hands the village wants raising what the player marked out (D43).</summary>
    public int Builders { get; }

    /// <summary>
    /// Hands the village wants sowing and reaping (`crops-and-orchards.md`, D161).
    /// </summary>
    /// <remarks>
    /// <b>⛔ The arm with teeth.</b> See <see cref="Core.SimWorld.FarmerSeatsWithGroundToWork"/>
    /// for why this must actively <em>want</em> people when the fields are standing:
    /// <c>SetStaffing</c> is a ceiling and not a summons (D146), so a farm nobody is wanted at
    /// is a harvest that rots while every guard blames the crop system.
    /// </remarks>
    public int Farmers { get; }

    /// <summary>The quota for one kind of work.</summary>
    public int For(JobKind kind) => kind switch
    {
        JobKind.Forager => Foragers,
        JobKind.Forester => Foresters,
        JobKind.Woodcutter => Woodcutters,
        JobKind.Marketer => Marketers,
        JobKind.Builder => Builders,
        JobKind.Farmer => Farmers,
        _ => 0,
    };

    /// <summary>
    /// Work out what this village needs, from the people currently in it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Food first, then timber, then food again.</b> The order is the policy:
    /// </para>
    /// <list type="number">
    ///   <item>
    ///     Staff <see cref="ForagersToFeedEveryone"/> before anything else. Below this
    ///     line the village is not feeding itself, and nothing else is worth doing.
    ///   </item>
    ///   <item>
    ///     Spare what is left over for timber, up to what the stands can physically
    ///     hold — <b>but only if no household in the village is going hungry</b>. In a
    ///     lean year that is nobody, and the village stops growing until it has its
    ///     stores back, which is the <em>point</em>: a consequence the player can
    ///     watch rather than a rule they must be told.
    ///   </item>
    ///   <item>
    ///     Everyone still spare goes back to foraging.
    ///   </item>
    /// </list>
    /// <para>
    /// <b>That third step is a deliberate departure from the spec as written</b>
    /// (§4a proposed <c>foragers = ceil(mouths / RequiredDependants)</c> as a hard
    /// ceiling). A ceiling leaves able adults idle, and food is stored
    /// <em>per household</em> (D14) — so an idle adult is not a spare resource, they
    /// are a household producing nothing and living on its neighbours' charity. That
    /// is attempt #2 in the spec's own table of failures: "mathematically sufficient
    /// and still fatal". The floor gets the priority the ceiling was reaching for
    /// without re-running the failure the same document records.
    /// </para>
    /// <para>
    /// So the quota binds on <em>timber</em>, not on food. A villager turned away from
    /// the tree stand is being told something true and specific: the village needs
    /// its hands eating first.
    /// </para>
    /// </remarks>
    public static LabourQuota For(SimWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        int hands = 0;
        int mouths = 0;
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager villager = world.Villagers[i];
            if (!villager.Alive)
            {
                continue;
            }

            mouths++;
            if (villager.CanWork)
            {
                hands++;
            }
        }

        int toFeedEveryone = CeilingDivide(mouths, VillageEconomy.MouthsFedByOneAdult(world.Config));
        if (toFeedEveryone < 1)
        {
            toFeedEveryone = 1;
        }

        // THE CHAIN, WORKED BACKWARDS (D29). Homes need heating, so the village wants
        // woodcutters; woodcutters eat logs, so it wants foresters — for the firewood
        // as well as for the houses. Demand propagates back down the chain rather
        // than each workplace guessing at its own, which is the same lesson the
        // forager quota is a record of, one link further along.
        int woodcutters = WoodcuttersWanted(world);
        int forestersForHuts = LoggersToFeedTheHuts(world, woodcutters);
        int forestersForHouses = ForestersWanted(world);
        int marketersWanted = MarketersWanted(world);
        int buildersWanted = BuildersWanted(world);

        // WHILE THERE IS FOOD TO GATHER AND THE VILLAGE IS SHORT OF IT, EVERY HAND
        // GATHERS. Timber, fuel, building and the market all yield — a marketer most
        // readily of anything, since they produce nothing at all. That is §4a's
        // one-sentence policy: a village short of hands feeds itself before it builds.
        //
        // Each clause was measured, and each cost a long run:
        //
        //   ACROSS THE WHOLE VILLAGE, not per household. "Is any household below its
        //   sharing floor?" is true almost all the time — they dip and are topped back
        //   up every season, by design — so gating on it meant no timber was ever cut,
        //   no houses built, no households formed, and the village aged out and died
        //   without one villager starving.
        //
        //   ONLY WHILE THERE IS FOOD TO GATHER. Pulling hands back onto the berries in
        //   winter is not caution, it is waste: the larder always dips then and there
        //   is nothing out there to pick, so they simply stood idle. Trees do not stop
        //   in winter, which is most of why the job is worth holding (D17).
        //
        //   AND THE FUEL CHAIN YIELDS TOO, which was got wrong in both directions.
        //   Exempting fuel put two of the founding four on firewood with an empty
        //   larder, and they starved. Funding fuel only from what was left after the
        //   food floor drained the woodpile a little every year until four households
        //   froze in one winter, with a full larder and a yard full of logs.
        bool foodComesFirst =
            SeasonRules.IsGatherable(world.Clock.Season) && VillageIsShortOfFood(world);

        if (foodComesFirst)
        {
            woodcutters = 0;
            forestersForHuts = 0;
            forestersForHouses = 0;
            marketersWanted = 0;
            buildersWanted = 0;
        }

        // ---- Survival first, in the order things kill you -------------
        //
        // Firewood is a SURVIVAL resource now, not a surplus one, and treating it as
        // spare-hands work was fatal. Measured: the village funded timber out of
        // whatever was left after feeding everyone, the feeding floor took four hands
        // in five, and so the woodpile drained a little every year until the fourth
        // decade, when four households froze in a single winter with a full larder and
        // a yard full of logs. Nothing was wrong with the chain; it was never staffed.
        //
        // So heating is a floor alongside eating, and only house-building is funded
        // from what is left. Food comes first of the two because hunger kills in six days
        // and an unheated house in twenty-five (D45) — if the village can only afford one,
        // it should buy the one that kills soonest.
        int free = hands;

        // NOTHING CAN BE PICKED IN WINTER, so the village does not staff a berry patch
        // then — not even the food floor (D44).
        //
        // This was the shape of the idle winter: the floor was taken first regardless
        // of season, every hand left over was dumped into foraging by the last line of
        // this method, and BehaviorSystem then sent all of them home because
        // FoodSource.IsGatherable is false. A quarter of the working year, spent
        // resting, by whoever held the commonest job in the village — and no reshuffle
        // ever noticed, because the allocator had never heard of seasons.
        bool canGather = SeasonRules.IsGatherable(world.Clock.Season);

        // ---- The player's ceiling, applied on top of the derivation (D62) ----
        //
        // A limit does not replace any of the demand worked out above; it caps it. So the
        // village goes on knowing what it needs, and simply stops once the player has as
        // much as they asked for — which is what makes "nobody is splitting logs because
        // you asked for 200 firewood and the village has 214" a sentence the game can say.
        //
        // EACH LIMIT READS THE SAME SUPPLY ITS DEMAND FUNCTION READS, and that is not a
        // detail. WoodcuttersWanted counts firewood IN THE SHED, deliberately: a pile in
        // somebody else's home is not supply because no errand reaches it. A limit counting
        // firewood everywhere would disagree with the demand it caps, and D29 is the record
        // of what that costs — the village believed itself stocked, staffed one woodcutter,
        // and froze to extinction with a hundred and eighty firewood in homes and an empty
        // shed. Reading the wrong total here would be that bug with the player's own number
        // on it.
        StockLimits limits = world.StockLimits;

        if (limits.IsMet(Goods.Firewood, world.FirewoodInSheds()))
        {
            woodcutters = 0;
        }

        if (limits.IsMet(Goods.Logs, world.LogsInSheds()))
        {
            // ⭐ A MET LOG LIMIT STOPS THE FELLING, NOT THE PROFESSION (Joe, D146). *"A capped
            // hut can replant. Priority should be replant → extra-hands labour. It just
            // shouldn't fell if it has met its cap."*
            //
            // ⛔ ZEROING BOTH ARMS MADE THAT IMPOSSIBLE, AND IT TOOK A MEASUREMENT TO SEE IT.
            // `SimWorld.MayFell` sends a capped forester to bare ground instead of to a tree —
            // but the hut had **nobody standing in it to send**: the quota wanted zero
            // foresters, so the allocator emptied the building, and the replanting could never
            // happen however the behaviour branch was written. `SetStaffing` is a ceiling, not
            // a summons. Measured on a hut with 88 bare tiles and a limit of 0: *most hands
            // ever at the hut 0 of 2, quota wants 0 foresters.*
            //
            // So the demand becomes the PLANTING demand — the seats at huts that still own
            // ground to put back — which is derived rather than typed (D16) and falls to zero
            // by itself once every painted tile is wooded again. That is Joe's ordering exactly:
            // replant until the painted area is maxed out, then be spare hands.
            //
            // ⚠️ AND NEVER OVER THE FOOD GATE ABOVE. This is an assignment rather than a
            // reduction, so on its own it would hand foresters back to a village that had just
            // zeroed every non-food job because its larders were empty — planting outranking
            // eating, which is §4a exactly backwards. Replanting is the least urgent thing in
            // the game: it feeds nobody this year or next.
            forestersForHuts = foodComesFirst ? 0 : world.ForesterSeatsWithGroundToPlant();

            // The discretionary half stays zero: logs for houses is felling by another name.
            forestersForHouses = 0;
        }

        // Food is the one that can cost lives, and it is still obeyed. §3 of the spec: a
        // game that refuses the player's number argues with them, and one that obeys it
        // silently has killed them without saying so. The saying-so happens once, when the
        // limit is set (SimWorld.SetStockLimit) — not once a tick, which is the nag D42
        // deliberately avoided.
        // What the village holds, not only what is in the granaries (D161): a player's food
        // limit is about the village's stock, and a farm's buffer is part of it.
        bool foodIsEnough = limits.IsMet(Goods.Food, world.FoodTheVillageHolds());

        int foragers = canGather && !foodIsEnough ? Take(ref free, toFeedEveryone) : 0;

        // ⭐ THE FARM, AND IT SITS HERE FOR A STATED REASON (`crops-and-orchards.md`, D161).
        //
        // Immediately after the foraging floor and ahead of timber, because **farming is food**
        // — the same class of work as gathering, and unlike a log, a standing crop is on a
        // clock. A field sown in spring and not reaped in autumn is taken by winter (Joe, *use
        // it or lose it*), so hands spared from the harvest are not deferred, they are spent.
        //
        // ⛔⛔ AND IT IS NOT ZEROED BY `foodComesFirst` ABOVE, deliberately: that gate exists to
        // put every hand on FOOD when the village is short of it, and this IS the hand on food.
        // Zeroing it would be the gate arguing with its own purpose.
        //
        // ⚠️ NOR IS IT ZEROED BY A MET FOOD LIMIT, and that is not an oversight either — the cap
        // is folded in one level down, by `SimWorld.MaySow`, which stops the SOWING and leaves
        // the REAPING alone. A capped village still brings its harvest in and simply does not
        // commit next year's ground; leaving a standing crop to rot on a number the player set
        // for another reason would spend a year of their work to obey them. (D146's *"a capped
        // hut can replant"* one job over.)
        //
        // Bounded by what the year has work for, which is what makes it safe to place this
        // high: `FarmerSeatsWithGroundToWork` is zero when nothing is sown and nothing is
        // standing, so a village with no farm — or a farm in winter — spends nothing here.
        int farmers = Take(
            ref free,
            Cap(world.FarmerSeatsWithGroundToWork(), TotalCapacityFor(world, JobKind.Farmer)));

        woodcutters = Take(ref free, Cap(woodcutters, TotalCapacityFor(world, JobKind.Woodcutter)));
        int foresters = Take(ref free, Cap(forestersForHuts, TotalCapacityFor(world, JobKind.Forester)));

        // Only now the discretionary half: logs for the homes the village wants to
        // build. This is the one that yields when times are hard.
        foresters += Take(ref free, Cap(forestersForHouses, TotalCapacityFor(world, JobKind.Forester) - foresters));

        // And the market, last of all, out of hands nobody else needs (D14).
        //
        // Deliberately the LOWEST priority of every job, which is the mechanical form
        // of the promise in spec §14.4: a marketer moves goods that already exist, so
        // a village that cannot spare anyone loses convenience rather than lives.
        // Funding it any higher would make the market the cliff that fetching was
        // designed to avoid.
        // Building, out of what is left after the village has fed and warmed itself.
        //
        // Deliberately discretionary, alongside cutting logs for houses: a village
        // short of hands feeds itself before it builds (§4a), and a granary half-raised
        // through a hard winter is a better outcome than a finished one nobody lived to
        // use. It is also what makes marking six buildings at once a real decision
        // rather than six purchases — they compete with the berry patch.
        //
        // AND IT MAY NEVER TAKE MORE THAN HALF THE HANDS THAT ARE LEFT. Without that
        // cap, "how many builders does the village want?" answers with every seat at
        // every site the player has marked — mark four buildings and the answer is
        // twelve, which is the whole settlement. Food production drops to its bare
        // floor for as long as the work lasts, and the village dies with the buildings
        // finished. Measured: four buildings marked in year 15, two granaries and two
        // sheds standing, nobody alive a century later.
        //
        // Half is the same margin FuelBudgetInHands uses and for the same reason: the
        // floor is what the village must not fall below, not what it should live on.
        //
        // ⚠️ AND IT IS WHY BUILDING IS SO SLOW — MEASURED, NOT FIXED HERE (D103). `free / 2`
        // is integer division, so a village with ONE hand spare wants ZERO builders, and a
        // four-adult founding has exactly one hand spare for most of the year. On Joe's own
        // opening the builder quota is zero for the whole of autumn and goes to one the tick
        // winter stops the foraging — which is what he watched: "they built one house at the
        // very last minute, even though they had all of fall to build it, and then never
        // built the 2nd."
        //
        // Narrowing it to "never round a willing hand down to nobody" was tried and reverted:
        // it fixes the founding outright (both houses up by t300 against t403 and t900) and
        // then KILLS SEED 11 OF ELEVEN — a village that peaks at 32 and ages out to nothing
        // by year 160, with zero starved and zero frozen and four sites it never builds.
        // D93 tried the same narrowing once before and it killed the village then too.
        //
        // ⭐ The measurement that matters for whoever takes this next: builders got 0.1% of
        // all adult ticks in that run, WITH the cap widened and twenty-four adults standing
        // about. So `free / 2` is not the only gate — `VillageIsShortOfFood` above zeroes
        // `buildersWanted` outright for three seasons in four once the stocking target scales
        // with population, and that is `specs/cold-start.md §7.1b`'s gate showing up in the
        // steady state rather than at the founding. Both have to move together, and the
        // seed-11 arm is the guard that says whether they moved correctly.
        int buildersAfforded = Math.Min(buildersWanted, free / 2);
        int builders = Take(ref free, Cap(buildersAfforded, TotalCapacityFor(world, JobKind.Builder)));

        // ⭐ AND A LOG LIMIT SET ABOVE WHAT THE VILLAGE SPENDS IS AN AMBITION (D130). Joe:
        // *"the village should want timber it isn't spending if the user sets a limit above
        // what the village uses — that is a stockpile/growth play tool for the user."*
        //
        // **Every other reason to fell was demand-driven**, and that is the trap this fixes:
        // foresters were wanted only to feed the fuel chain and the houses already marked, so
        // the village could never save up for anything — it cut exactly what it was about to
        // burn. Measured, and the measurement is the whole argument: making fuel cheaper made
        // the shortage WORSE. `firewood_per_split` 7 → 50 dropped fuel from 60% of all timber
        // to 41%, and total production fell 365 → 174 logs with the village holding 78 at the
        // end instead of 137. A cheaper habit is not a woodpile. Nothing was ASKING.
        //
        // So an unmet log limit asks. It is the instruction a stock limit has always been —
        // *how much to keep* — read in the direction nobody had built yet: a met limit stops
        // the work, and now an unmet one starts it.
        //
        // ⚠️ AND IT MAY NEVER TAKE MORE THAN HALF THE HANDS THAT ARE LEFT, which cost a run to
        // learn. Taken uncapped it filled every forester seat in the village, and the "free"
        // hands it drank were never idle — they are the labourers who carry food to the
        // larders and firewood to the homes. Twelve years at a 200-log ambition: the woodpile
        // worked, 174 → 266 logs produced and 242 held, and **the population fell from ten to
        // four**. A village that hauls nothing starves beside full sheds, which is D29's
        // lesson arriving through the player's own number.
        //
        // Half is the margin building already uses (above) and for the same reason. Placed
        // after building too, because a stockpile is the most discretionary want in the
        // village: houses the player marked are a plan, and a number in a box is a wish.
        if (limits.For(Goods.Logs) is int wantedInStore && world.LogsInSheds() < wantedInStore)
        {
            foresters += Take(
                ref free,
                Cap(free / 2, TotalCapacityFor(world, JobKind.Forester) - foresters));
        }

        int marketers = Take(ref free, Cap(marketersWanted, TotalCapacityFor(world, JobKind.Marketer)));

        // Everyone still spare forages. Berries keep, and a hand that gathers nothing
        // still eats.
        //
        // IN WINTER, NOTHING IS ADDED HERE — a hand the village has no work for is left
        // without one, and the sentence LabourAllocator writes for them says so.
        //
        // The first version sent every spare winter hand to the woods, bounded by the
        // stands' seats and by "is any shed not yet full?" — which bounds the SHED, not
        // the work. Demand for timber is answered twice further up this method and funded
        // before this point, so every hand the fill added was cutting logs nothing wanted.
        // It packed the sheds, crowded out the firewood the birth gate reads, and cost the
        // village a third of its population. D52 has the measurements.
        //
        // So the idle winter is narrowed rather than filled. Removing the food floor still
        // gives the woodcutters and foresters the village DOES want first call on every
        // hand, which is the half of D44 that was a real bug. The half that is left —
        // hands with genuinely nothing to do between the last harvest and the first — is
        // not a gap to paper over with make-work: it is D44's own forward note, and it
        // wants herding and slaughtering (D39's roadmap) to answer it honestly.
        // A stock limit stops this too, and it has to: leaving the spare hands here would
        // cap the quota and then hand every freed body straight back to the berry patch,
        // so the granary would sail past the number the player asked for and the control
        // would appear broken while working perfectly.
        if (canGather && !foodIsEnough)
        {
            foragers += free;
        }

        // ⭐ AND THEN THE PLAYER HAS THE LAST WORD (D106). Everything above is the village
        // deciding for itself; a profession target replaces its answer for that one kind.
        //
        // APPLIED AT THE END, DELIBERATELY, and it is the difference between a control and a
        // suggestion. Folding a target into the middle would let the food floor and the
        // `free / 2` building cap quietly overrule it — and overruling it is precisely what
        // the player is reaching for the panel to stop. D103 is the case: building is funded
        // from what is left, and measured, what is left is nothing for most of the year. The
        // village cannot fix that for itself without killing some valleys (tried twice); a
        // player who can say "two builders" fixes the one in front of them.
        //
        // STILL BOUNDED BY WHAT EXISTS — capacity, and the number of able adults. You may ask
        // for six woodcutters; you may not conjure the seats or the people. That bound is not
        // the game arguing back, it is the game not lying about what it did with your number.
        foragers = Asked(world, JobKind.Forager, foragers, hands);
        foresters = Asked(world, JobKind.Forester, foresters, hands);
        woodcutters = Asked(world, JobKind.Woodcutter, woodcutters, hands);
        marketers = Asked(world, JobKind.Marketer, marketers, hands);
        builders = Asked(world, JobKind.Builder, builders, hands);
        farmers = Asked(world, JobKind.Farmer, farmers, hands);

        return new LabourQuota(
            hands, mouths, toFeedEveryone, foragers, foresters, woodcutters, marketers, builders,
            farmers);
    }

    /// <summary>What the player asked for on this kind of work, or what the village decided.</summary>
    /// <remarks>
    /// <para>
    /// <b>Bounded by the seats that exist, the people who exist, and — since D128 — by a
    /// stock limit that has been reached.</b> It is still deliberately allowed to exceed what
    /// the village would have chosen, which is the whole reason the control exists, and still
    /// allowed to be zero, which is how a player turns a profession's hands into laborers.
    /// </para>
    /// <para>
    /// <b>⚠️ THE STOCK LIMIT WAS BEING IGNORED, AND IT IS D127's CHANGE THAT EXPOSED IT.</b>
    /// A limit works by zeroing the village's own figure above; the player's number was then
    /// applied over the top, bounded only by seats and hands. That was harmless while every
    /// profession defaulted to <em>"village decides"</em> and this method returned
    /// <c>decided</c> — and the moment the tick came off the panel, every profession carried
    /// an explicit number from the first frame and **no stock limit could stop any work
    /// again**. Joe, playing: *"they are ignoring the limits for firewood. 452 at a limit of
    /// 50 and they keep cutting more."*
    /// </para>
    /// <para>
    /// <b>And it was two bugs wearing one coat.</b> Woodcutters that never stop turn every
    /// log into firewood, so the same run had **452 firewood and 8 logs**, and no granary or
    /// shed could ever be afforded — *"even when they clear half a forest every year, there
    /// aren't enough logs to build."*
    /// </para>
    /// <para>
    /// <b>A stock limit is a stop, not a preference</b>, which is what the panel says it is:
    /// <em>how much to keep before the work stops</em>. So a limit that has been reached wins
    /// over the staffing number. That is a narrow exception to D106's *applied last* rule,
    /// and the distinction is worth keeping: D106 was protecting the player's number from the
    /// food floor and the building cap — the village's own opinions — not from another
    /// instruction the player gave.
    /// </para>
    /// </remarks>
    private static int Asked(SimWorld world, JobKind kind, int decided, int hands)
    {
        if (world.JobLimits.For(kind) is not int asked)
        {
            return decided;
        }

        // The village's figure is already zero when a limit has been met, so this is simply
        // "a stop the player set outranks a number the player set".
        if (decided == 0 && StoppedByAStockLimit(world, kind))
        {
            return 0;
        }

        int seats = TotalCapacityFor(world, kind);
        int bounded = asked < seats ? asked : seats;
        return bounded < hands ? bounded : hands;
    }

    /// <summary>Whether this kind of work is one a reached stock limit puts a stop to.</summary>
    /// <remarks>
    /// Only the two that make a good the player can cap. A gatherer is governed by the food
    /// limit through the food floor rather than here, because stopping the food chain dead
    /// is how a village starves with a full granary and an empty larder (D79).
    /// </remarks>
    private static bool StoppedByAStockLimit(SimWorld world, JobKind kind) => kind switch
    {
        JobKind.Woodcutter => world.StockLimits.IsMet(Goods.Firewood, world.FirewoodInSheds()),

        // ⭐ AND A FORESTER IS ONLY STOPPED WHEN THERE IS NOTHING TO PUT BACK EITHER (D146).
        // A met log limit stops the felling; a hut with bare ground of its own still has work,
        // so a professions number the player typed must not be overruled while it does.
        JobKind.Forester => world.StockLimits.IsMet(Goods.Logs, world.LogsInSheds())
            && world.ForesterSeatsWithGroundToPlant() == 0,

        // ⭐ AND A FARMER ONLY WHEN THERE IS NOTHING LEFT TO BRING IN. Exactly the forester's
        // shape: a met food limit stops the sowing (`SimWorld.MaySow`), and a farm with a crop
        // still standing has work a cap has no business cancelling — so a professions number
        // the player typed must not be overruled while it does.
        JobKind.Farmer => world.StockLimits.IsMet(Goods.Food, world.FoodTheVillageHolds())
            && world.FarmerSeatsWithGroundToWork() == 0,

        // ⭐ ANYTHING ELSE — INCLUDING A TRADE A MOD ADDED — IS STOPPED BY ITS ROW'S LIMIT IF IT
        // NAMES ONE, AND NEVER OTHERWISE (D218). This arm used to be a flat `false`, which meant
        // a trade the sim had not been taught about could not be capped at all: the player sets a
        // limit and the work carries on, silently.
        //
        // ⚠️ THE THREE ARMS ABOVE ARE NOT REDUNDANT WITH IT, and that is why they stay. Each
        // carries an escape clause that is real per-trade reasoning rather than data — a met log
        // limit does not stop a forester who still has bare ground to plant (D146), and a met food
        // limit does not stop a farmer with a crop still standing. `jobs-catalog.md §2.1` records
        // this as the second exemption beside the idle note: **the good is data; knowing when the
        // cap has no business applying is not.**
        _ => world.JobsCatalog.LimitedBy(kind) is Goods limited
            && world.StockLimits.IsMet(limited, world.InStores(limited)),
    };

    /// <summary>Draw up to <paramref name="wanted"/> hands from those still free.</summary>
    private static int Take(ref int free, int wanted)
    {
        int taken = wanted > free ? free : wanted;
        if (taken < 0)
        {
            taken = 0;
        }

        free -= taken;
        return taken;
    }

    private static int Cap(int wanted, int ceiling)
    {
        if (ceiling < 0)
        {
            ceiling = 0;
        }

        return wanted > ceiling ? ceiling : wanted;
    }

    /// <summary>
    /// How many foresters the village has a use for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Asked the same way as the forager quota: what is this work FOR, and how much
    /// of it does the village actually need?</b> For foraging the answer comes from
    /// mouths. For timber it comes from houses — right now the only thing wood is
    /// spent on is a home for a couple who have grown up and want one, so the honest
    /// measure is the couples currently waiting, less the timber already in store.
    /// </para>
    /// <para>
    /// Deriving it mattered more than expected. The first version simply spared every
    /// hand not needed for food, which for a founding village of four adults meant
    /// <em>two foresters</em> — and one cutter produces enough timber for several
    /// houses a year, so the village was putting half its labour into a resource it
    /// could not spend while the other half tried to feed everyone. It oscillated for
    /// a century and died. Wood is simply much cheaper than food, and the quota had no
    /// way of knowing that until it was asked what the wood was for.
    /// </para>
    /// <para>
    /// <b>This will need revisiting when wood does more.</b> D17 gives it two more
    /// jobs — winter fuel and tools — and each is another term in this sum. The shape
    /// of the question stays the same; only the demand side grows.
    /// </para>
    /// </remarks>
    public static int ForestersWanted(SimWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        int waiting = 0;
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            if (Systems.HouseholdSystem.IsSeekingAHome(world.Villagers[i], world.Config))
            {
                waiting++;
            }
        }

        // Two people make one household — but rounded UP, not down. Rounding down
        // meant a single young person waiting for a partner counted as no house at
        // all, so the village cut nothing, they never got a home, births stopped, and
        // the settlement aged out and died with its last generation. One person
        // waiting is a house coming.
        //
        // Plus one more: THE VILLAGE KEEPS A WOODPILE. Cutting only what the couples
        // in front of you need turns timber from a job into an errand — a hand is put
        // on the stand at the new year, cuts thirty logs by midspring, and is taken
        // off again, which is not a livelihood anyone holds and not something the
        // player can watch someone do. Keeping enough for the next home as well means
        // somebody is usually at the stand, and it is what any village that has been
        // through a winter would actually do.
        int housesWanted = CeilingDivide(waiting, 2) + 1;

        // A FAMILY WITH NO HOUSE WANTS ONE, and this had to be said out loud (D72).
        //
        // "Waiting for a home" was counted only as UNPAIRED adults, because until the cold
        // start the only way to want a house was to grow up and find somebody. The founders
        // are already paired and already a household — they simply have no roof — so they
        // were invisible here. Measured by Joe playing it: the tree stand read "the village
        // wants 0 on this kind of work" while four people stood in the open, and they froze
        // in winter 1 without a log being cut.
        for (int i = 0; i < world.Households.Count; i++)
        {
            Household household = world.Households[i];
            if (!household.HasHome && world.LivingMembersOf(household) > 0)
            {
                housesWanted++;
            }
        }

        // Timber standing ANYWHERE in the village — the shed, a workplace buffer, or
        // in somebody's arms on the way there.
        //
        // Counting only household piles was right until goods moved into buildings
        // (D30), and then silently wrong: logs go to the shed now, so every household
        // read zero, the village believed it had no timber at all, and it put half its
        // hands on the tree stand forever. It finished a century with five thousand
        // firewood, six people ever born, and nobody left alive — no starvation, no
        // cold, just a settlement that spent its whole life cutting wood it already
        // had instead of raising children.
        int stored = world.TotalLogs();

        int shortfall = (housesWanted * world.Config.LogsPerHouse) - stored;
        if (shortfall <= 0)
        {
            return 0;
        }

        return CeilingDivide(shortfall, VillageEconomy.WoodCutPerYearAtWorst(world.Config));
    }

    /// <summary>
    /// How many woodcutters the village has a use for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Asked the same way as every other quota: what is this work <em>for</em>? For
    /// firewood the answer is winter — every occupied home has to be heated through
    /// it, less whatever is already stacked against the wall.
    /// </para>
    /// <para>
    /// <b>Demand is counted per home and supply only from the sheds</b> — see the comment
    /// in the body for the village that froze proving why. This used to say the opposite,
    /// on the grounds that "the sharing policy moves it around anyway"; that policy was
    /// deleted by D30 and the reading it justified outlived it by several slices.
    /// </para>
    /// </remarks>
    public static int WoodcuttersWanted(SimWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        // ---- Demand is per home; supply is the shed, and only the shed ----
        //
        // This used to compare two village-wide totals: all the firewood anywhere,
        // against what every home together wanted. The stated reason was that "the
        // sharing policy moves it around anyway" — and that policy was deleted by
        // storage slice 3, so the justification went with it and the reading did not.
        //
        // <b>A household can only fetch firewood from the SHED.</b> Firewood stacked in
        // somebody else's home is not supply; there is no errand that reaches it. Adding
        // it in let a surplus in one house cancel a shortage in another, and the village
        // believed itself stocked. Measured: it froze to extinction over four years with
        // a hundred and eighty firewood sitting in homes, an empty shed, and the quota
        // reading 191 against a need of 210 — so it staffed one woodcutter for a village
        // that needed every hand it had. The right stuff in the wrong place, which is
        // the shape nearly every goods bug here has had.
        //
        // So the two halves are asked separately, and the direction matters:
        //   demand — what homes are short of, counted per home so surpluses never
        //            cancel shortages, plus the winter the village is about to burn;
        //   supply — what is in the shed, because that is what a fetch can reach.
        //
        // Cutting MORE firewood is only the answer when the shed cannot cover the
        // demand. If it can, the wood already exists and what those homes need is a
        // trip, not a woodcutter — and staffing one anyway is not harmless: the shed
        // holds logs and firewood in the same room, so firewood nobody needs crowds
        // out the logs the village builds with. Measured that too, in the other
        // direction: the shed packed to six hundred firewood, logs could not be
        // deposited, no house was ever raised again and the village dwindled to three
        // people without a single soul freezing.
        int demand = 0;
        int homes = 0;
        for (int i = 0; i < world.Households.Count; i++)
        {
            Household household = world.Households[i];
            if (world.LivingMembersOf(household) == 0)
            {
                continue;
            }

            homes++;

            int wanted = VillageEconomy.FirewoodStoreWantedPerHousehold(world.Config);
            int missing = wanted - household.Stockpile.Firewood;
            if (missing > 0)
            {
                demand += missing;
            }
        }

        // Plus the winter about to be burned. Asking only "is anyone below target?" is
        // a thermostat that switches on after the house is already cold: every store
        // sits exactly at target, the quota reads no shortfall and staffs nobody,
        // winter burns through it, and the hut is staffed again a season after people
        // started freezing. Measured across several runs — the village held twenty-five
        // people with its firewood exactly at target and no buffer, and one winter took
        // twenty-eight of them. Including the burn makes this proportional control
        // rather than a switch, which is what a woodpile IS.
        demand += homes * VillageEconomy.FirewoodPerHouseholdPerWinter(world.Config);

        // Across every shed (D38): a household can fetch from any of them, so all of
        // them are supply. Reading one would have had the village cutting wood it
        // already had the moment somebody built a second shed — the same shape as the
        // bug where this counted firewood stranded in homes.
        int shortfall = demand - world.FirewoodInSheds();
        if (shortfall <= 0)
        {
            return 0;
        }

        return CeilingDivide(shortfall, VillageEconomy.FirewoodMadePerYearAtWorst(world.Config));
    }

    /// <summary>
    /// Extra foresters needed to keep the huts in logs.
    /// </summary>
    /// <remarks>
    /// The back-propagation step. Without it the village staffs its huts, burns
    /// through the woodpile that was cut for houses, and then both stop — the chain
    /// starving in the middle, which is the failure mode processing introduces.
    /// </remarks>
    private static int LoggersToFeedTheHuts(SimWorld world, int woodcutters)
    {
        if (woodcutters <= 0)
        {
            return 0;
        }

        int logsEaten = woodcutters * VillageEconomy.LogsConsumedPerYearAtWorst(world.Config);
        return CeilingDivide(logsEaten, VillageEconomy.WoodCutPerYearAtWorst(world.Config));
    }

    /// <summary>
    /// Hands the village wants raising the buildings the player has marked out (D43).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every seat at every builder's hut, and none at all when there is nothing marked
    /// out</b> (D108). It used to count the seats at every unfinished <em>site</em>, because
    /// a site was where builders worked; sites are errands now and the hut is the workplace,
    /// so the seats are the hut's and the demand is whether there is anything to do.
    /// </para>
    /// <para>
    /// Unlike the other quotas this is still not derived from a need the village worked out
    /// for itself — <b>it is the player's intent, counted</b>, in two halves now: how many
    /// hands they put in the hut, and whether they have marked anything.
    /// </para>
    /// <para>
    /// <b>Two questions in one pass</b>, because this is asked on every labour pass and
    /// <see cref="SimWorld.BuildQueue"/> sorts.
    /// </para>
    /// </remarks>
    public static int BuildersWanted(SimWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        int seats = 0;
        bool anythingToBuild = false;

        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            Workplace workplace = world.Workplaces[i];
            if (workplace.Construction is { IsFinished: false })
            {
                anythingToBuild = true;
            }
            else if (workplace.Kind == JobKind.Builder)
            {
                seats += workplace.Places;
            }
        }

        // NOTHING MARKED, NOBODY BUILDING. A hut is a livelihood somebody holds and there
        // will be work at it again next spring — but staffing it while there is nothing to
        // raise takes a hand off the berries for no yield at all, which is the make-work D52
        // measured as costing the village a third of its population.
        return anythingToBuild ? seats : 0;
    }

    /// <summary>
    /// Hands the village wants working the market.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Stated as a count of errands, not a share of the population:</b> one hand per
    /// household whose goods are in the wrong place — either short of what it needs, or
    /// holding what it does not. That is literally the work available, so a village
    /// where everything is already where it should be staffs nobody, and one recovering
    /// from a bad winter staffs as many as it can spare.
    /// </para>
    /// <para>
    /// A house whose family has died counts, and is most of why this exists: its larder
    /// is stranded, and the marketer is the only one who can reach it (D34, spec §14.3).
    /// </para>
    /// </remarks>
    public static int MarketersWanted(SimWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        int errands = 0;
        for (int i = 0; i < world.Households.Count; i++)
        {
            Household household = world.Households[i];
            bool occupied = world.LivingMembersOf(household) > 0;

            if (!occupied)
            {
                // A house with nobody in it and goods still on the shelf. Only a
                // marketer can reach it (D34).
                if (household.Stockpile.Food + household.Stockpile.Firewood > 0)
                {
                    errands++;
                }

                continue;
            }

            // Or a family short of something. Counted as SHORT rather than "not
            // exactly at target", because a household is above target every time its
            // forager walks in and treating that as work to do had marketers stripping
            // families the moment they got ahead.
            if (household.Stockpile.Food < world.TargetFoodFor(household)
                || household.Stockpile.Firewood
                    < VillageEconomy.FirewoodStoreWantedPerHousehold(world.Config))
            {
                errands++;
            }
        }

        // ⭐⭐ AND A WORKPLACE BUFFER THAT NEEDS EMPTYING IS AN ERRAND TOO (D185, Joe).
        //
        // ⛔ THIS ARM WAS MISSING AND THE COST WAS THE WHOLE OF D171. `PlanMarketErrand` has had
        // a leg since then that clears a farm's buffer — written precisely so that
        // `crops-and-orchards.md §3.2`'s *"running it dry is the market's job"* would finally be
        // true — but this method counted errands from HOUSEHOLDS and nothing else. **So the
        // village never staffed a marketer because a farm needed emptying.** With every
        // household content the quota was zero, nobody worked the market, and the leg could not
        // run however full the farm stood.
        //
        // **The behaviour existed and the demand did not.** That is D36's own rule — *bounded
        // by errands and never by spare hands* — held for two of three leg types and quietly
        // dropped for the third, which is the shape D148 records one control over: two places
        // that must agree, and only one of them told.
        //
        // ⚠️ IT IS STILL BOUNDED BY ERRANDS THAT EXIST, which is the half §5.1 of
        // `stock-limits-and-laborers.md` warns about by name: D52 deleted a winter labour fill
        // bounded by *"is any shed not yet full?"* and it cost the village a third of its
        // population for a century. A farm with room in its buffer asks for nobody.
        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            if (world.BufferWorthClearing(world.Workplaces[i]))
            {
                errands++;
            }
        }

        return errands;
    }

    /// <summary>
    /// Whether the village, taken together, is short of food.
    /// </summary>
    /// <remarks>
    /// "Short" is <see cref="Config.SimConfig.SharingNeedPercent"/> of what every
    /// occupied household in it is aiming to store — the same line at which a
    /// neighbour would step in and give someone something. One definition of being
    /// short, used both by the thing that feeds people and by the thing that decides
    /// whether anyone can be spared from feeding them.
    /// </remarks>
    public static bool VillageIsShortOfFood(SimWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        // A HUNGER LINE, NOT A STOCKING TARGET (D73, Joe's (1)).
        //
        // This used to compare against the sum of TargetFoodFor(household) — which is
        // stockpile_target per member, a winter's eating PLUS the winter buffer, and the
        // number the village aims to have banked. Asking "do we have everything we would
        // like?" and calling the answer "short of food" is a category error, and it had
        // one consequence in every world and a fatal one in the newest:
        //
        //   In an established village it is nearly always false, because the granary
        //   starts full and stays there — so nobody noticed the question was wrong.
        //
        //   In a cold start it is true from the first week and never stops being true.
        //   The founders' cart holds less than two households' aspiration, so every hand
        //   foraged, no timber was ever felled, a marked woodcutter's hut sat at 0 of 25
        //   logs, and everybody froze. Joe watched it happen twice, and no amount of food
        //   in the cart could fix it: the target scales with members, so filling the cart
        //   raises the bar with it.
        //
        // So the line is what the village must EAT to get through the season it cannot
        // gather in — the bare ration, without the buffer. The buffer is what a village
        // wants; this is what it needs. Falling below the buffer is a village that should
        // work harder at food; falling below the ration is a village that should drop
        // everything, and only the second is what this question was ever asked for.
        int mouths = 0;
        for (int i = 0; i < world.Households.Count; i++)
        {
            mouths += world.LivingMembersOf(world.Households[i]);
        }

        int ration = VillageEconomy.WinterRationPerHead(world.Config) * mouths;

        // Food anywhere, granary included. A village with a full granary is not short
        // of food, however thin the individual larders happen to be at this instant —
        // and reading only the larders would send everyone foraging past a full store.
        return world.TotalFood() < ration;
    }

    /// <summary>Every seat at every workplace of one kind.</summary>
    /// <remarks>
    /// <b>A construction site is not one of them</b> (D108). It has no seats and nobody is
    /// ever assigned to it, and its <see cref="Workplace.Capacity"/> of zero already says so
    /// — but the player can set a staffing number on anything, and a site counted here would
    /// let a number typed at a footprint raise the ceiling on hands that can only ever sit in
    /// a hut. Belt and braces, which is what the zero and this skip are together.
    /// </remarks>
    public static int TotalCapacityFor(SimWorld world, JobKind kind)
    {
        ArgumentNullException.ThrowIfNull(world);

        int total = 0;
        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            if (world.Workplaces[i].Kind == kind && !world.Workplaces[i].IsSite)
            {
                total += world.Workplaces[i].Places;
            }
        }

        return total;
    }

    /// <summary>Integer division rounding up — <see cref="VillageEconomy.CeilingDivide"/>.</summary>
    private static int CeilingDivide(int numerator, int denominator) =>
        VillageEconomy.CeilingDivide(numerator, denominator);

    /// <summary>A one-line summary, for logs and for the sentence shown to the player.</summary>
    public override string ToString() =>
        $"{Hands} hands for {Mouths} mouths: {Foragers} foraging " +
        $"(at least {ForagersToFeedEveryone} to feed everyone), {Foresters} cutting.";
}
