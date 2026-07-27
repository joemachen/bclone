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
        int hands, int mouths, int foragersToFeedEveryone, int foragers, int loggers, int woodcutters)
    {
        Hands = hands;
        Mouths = mouths;
        ForagersToFeedEveryone = foragersToFeedEveryone;
        Foragers = foragers;
        Loggers = loggers;
        Woodcutters = woodcutters;
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
    public int Loggers { get; }

    /// <summary>Hands the village wants splitting logs into firewood.</summary>
    public int Woodcutters { get; }

    /// <summary>The quota for one kind of work.</summary>
    public int For(JobKind kind) => kind switch
    {
        JobKind.Forager => Foragers,
        JobKind.Logger => Loggers,
        JobKind.Woodcutter => Woodcutters,
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
        // woodcutters; woodcutters eat logs, so it wants loggers — for the firewood
        // as well as for the houses. Demand propagates back down the chain rather
        // than each workplace guessing at its own, which is the same lesson the
        // forager quota is a record of, one link further along.
        int woodcutters = WoodcuttersWanted(world);
        int loggersForHuts = LoggersToFeedTheHuts(world, woodcutters);
        int loggersForHouses = LoggersWanted(world);

        // Nobody is spared for building while the village as a whole is short of
        // food. Counting spare hands is not enough on its own: hands are spare only
        // if the food they are not gathering is food the village does not need.
        //
        // Deliberately measured across the WHOLE village rather than per household,
        // and that distinction cost a long run to find. "Is any household below its
        // sharing floor?" is true almost all the time — households dip below it and
        // are topped back up every season, by design — so gating on it meant no
        // timber was ever cut, no houses were ever built, no new households formed,
        // and the village aged out and died without a single villager starving. The
        // deaths were all old age, which is exactly what a village that stops having
        // children looks like.
        //
        // AND ONLY WHILE THERE IS FOOD TO GATHER. Pulling the loggers back onto
        // the berries in winter is not caution, it is waste: the larder always dips
        // in winter, and there is nothing out there to pick, so those hands simply
        // stood idle. Trees do not stop in winter — that asymmetry is most of why the
        // job is worth holding (D17), and this is the rule that lets the village act
        // on it.
        // A village that is genuinely short of food puts every hand on it — and this
        // gate has to cover the fuel chain too, not just the building half.
        //
        // Measured, twice, in opposite directions. Exempting fuel from the gate meant
        // the founding village put two of its four adults on firewood with an empty
        // larder and starved. Leaving fuel funded only from what was left AFTER the
        // food floor meant the woodpile drained a little every year until four
        // households froze in one winter, with a full larder and a yard full of logs.
        //
        // So: while there is food to gather and the village is short of it, everyone
        // gathers. Otherwise heating is a floor alongside eating, and only building is
        // funded from the leftovers. Winter is exempt because there is nothing out
        // there to pick — the larder always dips then, and pulling the woodcutters
        // onto empty berry patches is how the village froze.
        if (FoodSource.IsGatherable(world.Clock.Season) && VillageIsShortOfFood(world))
        {
            woodcutters = 0;
            loggersForHuts = 0;
            loggersForHouses = 0;
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
        // from what is left. Food comes first of the two because hunger kills in six
        // days and cold in ten — if the village can only afford one, it should buy the
        // one that kills soonest.
        int free = hands;

        int foragers = Take(ref free, toFeedEveryone);
        woodcutters = Take(ref free, Cap(woodcutters, TotalCapacityFor(world, JobKind.Woodcutter)));
        int loggers = Take(ref free, Cap(loggersForHuts, TotalCapacityFor(world, JobKind.Logger)));

        // Only now the discretionary half: logs for the homes the village wants to
        // build. This is the one that yields when times are hard.
        loggers += Take(ref free, Cap(loggersForHouses, TotalCapacityFor(world, JobKind.Logger) - loggers));

        // Everyone still spare forages. Berries keep, and a hand that gathers nothing
        // still eats.
        foragers += free;

        return new LabourQuota(hands, mouths, toFeedEveryone, foragers, loggers, woodcutters);
    }

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
    /// How many loggers the village has a use for.
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
    /// <em>two loggers</em> — and one cutter produces enough timber for several
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
    public static int LoggersWanted(SimWorld world)
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

        // Timber already standing in the village, wherever it is stored. Raising a
        // house draws on the whole settlement (see HouseholdSystem), so every stick
        // counts — and a village that already has the wood should put its hands back
        // on the berries rather than cutting more of what it cannot spend.
        int stored = 0;
        for (int h = 0; h < world.Households.Count; h++)
        {
            stored += world.Households[h].Stockpile.Logs;
        }

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
    /// Measured against the <b>whole village's</b> firewood, not each household's,
    /// because the sharing policy moves it around anyway. A per-household reading
    /// would staff the hut for a family that is about to be given fuel by a neighbour.
    /// </para>
    /// </remarks>
    public static int WoodcuttersWanted(SimWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        int homes = 0;
        int stored = 0;

        for (int i = 0; i < world.Households.Count; i++)
        {
            Household household = world.Households[i];
            stored += household.Stockpile.Firewood;

            if (world.LivingMembersOf(household) > 0)
            {
                homes++;
            }
        }

        int needed = homes * VillageEconomy.FirewoodStoreWantedPerHousehold(world.Config);
        int shortfall = needed - stored;
        if (shortfall <= 0)
        {
            return 0;
        }

        return CeilingDivide(shortfall, VillageEconomy.FirewoodMadePerYearAtWorst(world.Config));
    }

    /// <summary>
    /// Extra loggers needed to keep the huts in logs.
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

        int stored = 0;
        int target = 0;

        for (int i = 0; i < world.Households.Count; i++)
        {
            Household household = world.Households[i];
            if (world.LivingMembersOf(household) == 0)
            {
                continue;
            }

            stored += household.Stockpile.Food;
            target += world.TargetFoodFor(household);
        }

        return stored < target * world.Config.SharingNeedPercent / 100;
    }

    /// <summary>Every seat at every workplace of one kind.</summary>
    public static int TotalCapacityFor(SimWorld world, JobKind kind)
    {
        ArgumentNullException.ThrowIfNull(world);

        int total = 0;
        for (int i = 0; i < world.Workplaces.Count; i++)
        {
            if (world.Workplaces[i].Kind == kind)
            {
                total += world.Workplaces[i].Capacity;
            }
        }

        return total;
    }

    /// <summary>Integer division rounding up. No floats anywhere near this (D2).</summary>
    private static int CeilingDivide(int numerator, int denominator) =>
        denominator <= 0 ? 0 : (numerator + denominator - 1) / denominator;

    /// <summary>A one-line summary, for logs and for the sentence shown to the player.</summary>
    public override string ToString() =>
        $"{Hands} hands for {Mouths} mouths: {Foragers} foraging " +
        $"(at least {ForagersToFeedEveryone} to feed everyone), {Loggers} cutting.";
}
