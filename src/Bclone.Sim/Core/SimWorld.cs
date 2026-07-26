using Bclone.Sim.Config;
using Bclone.Sim.Determinism;
using Bclone.Sim.Logging;
using Bclone.Sim.World;

namespace Bclone.Sim.Core;

/// <summary>
/// The single root of all simulation state.
/// </summary>
/// <remarks>
/// <para>
/// If it affects the simulation, it lives here (or is reachable from here) and
/// is mixed into <see cref="StateHash"/>. One root makes "what is the state?" a
/// question with exactly one answer — which is what makes determinism testable,
/// saves possible, and desyncs findable.
/// </para>
/// <para>
/// The renderer may <em>read</em> this. It must never write to it (DESIGN.md §3).
/// </para>
/// </remarks>
public sealed class SimWorld
{
    /// <summary>
    /// Ticks elapsed. The sim's only notion of time — there is no wall clock in
    /// here, by design and by build-time enforcement (BannedSymbols.txt).
    /// </summary>
    public ulong Tick { get; internal set; }

    /// <summary>Seeded generator. Its state is part of world state.</summary>
    public DeterministicRandom Rng;

    /// <summary>Tunables for this run. Immutable once the run starts.</summary>
    public SimConfig Config { get; }

    /// <summary>Structured sink. Entries are stamped with the current tick.</summary>
    public ISimLogger Logger { get; }

    /// <summary>
    /// The calendar for the current tick.
    /// </summary>
    /// <remarks>
    /// Computed on demand rather than stored. Storing it meant the clock lagged
    /// <see cref="Tick"/> by one — the tick counter advanced at the end of
    /// <c>StepOnce</c> while the cached calendar still described the tick just
    /// finished — so the UI would have shown a date and a tick that disagreed.
    /// Deriving it makes that class of bug impossible, and keeps the calendar out
    /// of the state hash entirely, since it carries no information the tick does
    /// not already have.
    /// </remarks>
    public SimClock Clock => SimClock.FromTick(Tick, Config);

    /// <summary>
    /// Everyone in the village, ordered by id and never reordered.
    /// </summary>
    /// <remarks>
    /// A stable ordered list, deliberately not a dictionary. .NET guarantees no
    /// iteration order for <c>Dictionary</c>, and with N villagers competing for M
    /// jobs, iteration order decides who picks first — which would make the whole
    /// village non-deterministic (spec §4b).
    /// </remarks>
    public List<Villager> Villagers { get; } = new();

    /// <summary>Every household, ordered by id.</summary>
    public List<Household> Households { get; } = new();

    /// <summary>
    /// The shared travel-cost field. One instance, for everything that asks
    /// "how far is that" (DESIGN.md §2.6).
    /// </summary>
    public TravelCostField TravelCost { get; }

    /// <summary>The berry patch.</summary>
    public FoodSource FoodSource { get; }

    /// <summary>The stand of trees.</summary>
    public TreeStand TreeStand { get; }

    /// <summary>Every workplace, ordered by id.</summary>
    public List<Workplace> Workplaces { get; } = new();

    /// <summary>Look up a workplace by id, or null.</summary>
    public Workplace? FindWorkplace(int id)
    {
        for (int i = 0; i < Workplaces.Count; i++)
        {
            if (Workplaces[i].Id == id)
            {
                return Workplaces[i];
            }
        }

        return null;
    }

    /// <summary>Seed this run was created with — shown in the UI so a run that
    /// produced an interesting life can be reproduced exactly.</summary>
    public ulong Seed { get; }

    private SimWorld(SimConfig config, ISimLogger logger, ulong seed)
    {
        Config = config;
        Logger = logger;
        Seed = seed;
        Rng = new DeterministicRandom(seed);
        Tick = 0UL;

        TravelCost = new TravelCostField(config.TravelTicksPerUnit);

        FoodSource = new FoodSource
        {
            Position = new GridPos(config.FoodSourceX, config.FoodSourceY),
            YieldPerGather = config.GatherYield,
        };

        TreeStand = new TreeStand
        {
            Position = new GridPos(config.TreeStandX, config.TreeStandY),
            YieldPerCut = config.CutYield,
        };

        FoundVillage(config);

        int catchment = TravelCostField.TilesToCost(config.ForagerCatchmentTiles);
        int nextWorkplaceId = 1;

        Workplaces.Add(new Workplace
        {
            Id = nextWorkplaceId++,
            Kind = JobKind.Forager,
            Name = "the berry patch",
            Position = FoodSource.Position,
            Capacity = config.ForageSiteCapacity,
            CatchmentRadius = catchment,
        });

        // Several sites, spread around the valley, so an outlying household has
        // somewhere near enough to work. This is what a binding catchment needs in
        // order to be survivable rather than merely cruel (D19).
        for (int i = 0; i < config.ExtraForageSites.Count; i++)
        {
            SitePosition site = config.ExtraForageSites[i];
            var position = new GridPos(site.X, site.Y);

            Workplaces.Add(new Workplace
            {
                Id = nextWorkplaceId++,
                Kind = JobKind.Forager,
                Name = DescribeDirection(position),
                Position = position,
                Capacity = config.ForageSiteCapacity,
                CatchmentRadius = catchment,
            });
        }

        // Last id, so that where ids break a tie the food comes first. The real
        // "feed yourself before you build" rule is the quota, not the ordering -
        // see LabourQuota - but there is no reason for the two to disagree.
        Workplaces.Add(new Workplace
        {
            Id = nextWorkplaceId++,
            Kind = JobKind.Woodcutter,
            Name = "the tree stand",
            Position = TreeStand.Position,
            Capacity = config.TreeStandCapacity,
            CatchmentRadius = catchment,
        });
    }

    /// <summary>
    /// Create the founding households and their adults.
    /// </summary>
    /// <remarks>
    /// Draw order is part of the seed contract: for each villager, <b>name then
    /// lifespan, always</b>, and households in id order. Reordering any of it shifts
    /// every subsequent value in the stream, silently invalidating saved seeds and
    /// every golden test.
    /// </remarks>
    private void FoundVillage(SimConfig config)
    {
        int nextVillagerId = 1;

        for (int h = 0; h < config.StartingHouseholds; h++)
        {
            GridPos home = Household.PlacementFor(h, config.HomeX, config.HomeY, config.HouseholdSpacing);

            var household = new Household
            {
                Id = h + 1,
                Name = config.HouseholdNames[h % config.HouseholdNames.Count],
                HomePosition = home,
            };

            for (int a = 0; a < config.AdultsPerHousehold; a++)
            {
                string name = DrawUnusedName();

                int lifespan = config.LifespanYearsBase;
                if (config.LifespanYearsVariance > 0)
                {
                    lifespan += Rng.NextInt(-config.LifespanYearsVariance, config.LifespanYearsVariance + 1);
                }

                var villager = new Villager
                {
                    Id = nextVillagerId++,
                    Name = name,
                    LifespanYears = lifespan,
                    Position = home,
                    HouseholdId = household.Id,

                    // Year 1 is the first year, so someone aged N at founding was
                    // born in year 1-N. Deriving it this way means ClockSystem's
                    // per-tick recalculation reproduces the founding age rather
                    // than resetting every founder to zero on the first tick.
                    BirthYear = 1 - config.FounderAge,
                    AgeYears = config.FounderAge,
                    LifeStage = LifeStage.Adult,
                };

                household.AddMember(villager.Id);
                Villagers.Add(villager);
            }

            Households.Add(household);
        }

        PairFounders(config);
    }

    /// <summary>
    /// Founding adults arrive as couples, two to a household.
    /// </summary>
    /// <remarks>
    /// Otherwise the founders are all unpaired and immediately walk out of their own
    /// homes looking for partners, which is a strange way to found a settlement.
    /// </remarks>
    private void PairFounders(SimConfig config)
    {
        for (int h = 0; h < Households.Count; h++)
        {
            IReadOnlyList<int> members = Households[h].MemberIds;

            for (int i = 0; i + 1 < members.Count; i += 2)
            {
                Villager a = Villagers[members[i] - 1];
                Villager b = Villagers[members[i + 1] - 1];
                a.PartnerId = b.Id;
                b.PartnerId = a.Id;
            }
        }
    }

    /// <summary>
    /// Draw a name nobody in the village is already using.
    /// </summary>
    /// <remarks>
    /// Two villagers called Bess is a small thing that undercuts a large one: this
    /// game is defined against fungible labour units (§1.4), and you cannot tell a
    /// story about someone whose name is not theirs. Drawing with replacement from a
    /// short list produced twins-by-accident within two years.
    /// <para>
    /// Deterministic: pick an index from the seeded RNG, then walk forward to the
    /// first unused name. Walking is a fixed rule, so the same seed still yields the
    /// same village. If every name is taken, reuse is allowed rather than failing -
    /// a repeated name is a blemish, a crash is a bug.
    /// </para>
    /// </remarks>
    internal string DrawUnusedName()
    {
        IReadOnlyList<string> pool = Config.VillagerNames;
        int start = (int)Rng.NextUInt((uint)pool.Count);

        for (int offset = 0; offset < pool.Count; offset++)
        {
            string candidate = pool[(start + offset) % pool.Count];
            if (!IsNameInUse(candidate))
            {
                return candidate;
            }
        }

        return pool[start];
    }

    private bool IsNameInUse(string name)
    {
        for (int i = 0; i < Villagers.Count; i++)
        {
            // Only the living hold a name. The dead keep theirs in the log, but a
            // grandchild may carry it again - which is how families actually work.
            if (Villagers[i].Alive
                && string.Equals(Villagers[i].Name, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A place name from a direction, so the log reads as somewhere real.
    /// </summary>
    /// <remarks>
    /// "The western thicket" is a place; "Forage Site 3" is a row in a table. The
    /// same reason villagers are called Mabel (§1.4) applies to where they work — and
    /// it matters more here, because these names end up inside the sentence that
    /// explains why someone walks the way they do.
    /// </remarks>
    private static string DescribeDirection(GridPos position)
    {
        string northSouth = position.Y < 0 ? "northern" : position.Y > 0 ? "southern" : string.Empty;
        string eastWest = position.X < 0 ? "western" : position.X > 0 ? "eastern" : string.Empty;

        string where = string.IsNullOrEmpty(northSouth)
            ? eastWest
            : string.IsNullOrEmpty(eastWest) ? northSouth : $"{northSouth} {eastWest}";

        return string.IsNullOrEmpty(where) ? "the near thicket" : $"the {where} thicket";
    }

    /// <summary>Look up a villager by id, or null if there is no such person.</summary>
    public Villager? FindVillager(int id)
    {
        for (int i = 0; i < Villagers.Count; i++)
        {
            if (Villagers[i].Id == id)
            {
                return Villagers[i];
            }
        }

        return null;
    }

    /// <summary>The household a villager belongs to.</summary>
    public Household HouseholdOf(Villager villager)
    {
        ArgumentNullException.ThrowIfNull(villager);

        for (int i = 0; i < Households.Count; i++)
        {
            if (Households[i].Id == villager.HouseholdId)
            {
                return Households[i];
            }
        }

        throw new InvalidOperationException(
            $"Villager {villager.Id} ({villager.Name}) belongs to household {villager.HouseholdId}, which does not exist.");
    }

    /// <summary>Living members of a household.</summary>
    public int LivingMembersOf(Household household)
    {
        ArgumentNullException.ThrowIfNull(household);

        int count = 0;
        for (int i = 0; i < Villagers.Count; i++)
        {
            Villager villager = Villagers[i];
            if (villager.Alive && villager.HouseholdId == household.Id)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Food a household aims to have stored before it stops foraging and rests.
    /// </summary>
    /// <remarks>
    /// Scales with the number of mouths. A fixed target was tuned for Phase 0's
    /// single villager, and left a four-person household permanently one bad season
    /// from empty — perpetually foraging, never building a surplus, and therefore
    /// never having children. A household stores enough for <em>everyone in it</em>.
    /// </remarks>
    public int TargetFoodFor(Household household) =>
        Config.StockpileTarget * Math.Max(1, LivingMembersOf(household));

    /// <summary>Where a villager lives and returns to.</summary>
    public GridPos HomeOf(Villager villager) => HouseholdOf(villager).HomePosition;

    /// <summary>Living villagers. Convenience for narration and the UI.</summary>
    public int Population
    {
        get
        {
            int count = 0;
            for (int i = 0; i < Villagers.Count; i++)
            {
                if (Villagers[i].Alive)
                {
                    count++;
                }
            }

            return count;
        }
    }

    // ---------------------------------------------------------------
    //  Single-founder shorthand
    // ---------------------------------------------------------------
    // Phase 0 is not a special mode — it is the 1 household x 1 adult case. These
    // accessors let the Phase 0 tests keep saying what they mean without pretending
    // the world still holds one villager.

    /// <summary>The first villager. Meaningful when the village has exactly one.</summary>
    public Villager Villager => Villagers[0];

    /// <summary>The first household's store.</summary>
    public Stockpile Stockpile => Households[0].Stockpile;

    /// <summary>The first household's home position.</summary>
    public GridPos Home => Households[0].HomePosition;

    /// <summary>
    /// Create a world.
    /// </summary>
    /// <param name="config">Validated tunables.</param>
    /// <param name="logger">Where entries go. Defaults to discarding them.</param>
    /// <param name="seedOverride">
    /// Overrides <see cref="SimConfig.Seed"/>. Exists for tests and for
    /// "new game with a different seed" — the config file stays untouched.
    /// </param>
    public static SimWorld Create(SimConfig config, ISimLogger? logger = null, ulong? seedOverride = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();

        ulong seed = seedOverride ?? config.Seed;
        var world = new SimWorld(config, logger ?? NullSimLogger.Instance, seed);

        world.Log(LogLevel.Debug, "sim", $"World created (seed={seed}).");

        if (world.Villagers.Count == 1)
        {
            world.Narrate($"{world.Villager.Name} begins. {world.Clock.SeasonAndYear()}, no food stored.");
        }
        else
        {
            world.Narrate(
                $"{world.Villagers.Count} exiles arrive in {world.Households.Count} households. " +
                $"{world.Clock.SeasonAndYear()}, no food stored.");
        }

        return world;
    }

    /// <summary>
    /// Write a line of the villager's story.
    /// </summary>
    /// <remarks>
    /// The life log is not a separate system — it is the <c>INFO</c>-level view of
    /// sim events (spec §7). Same sink, same tick-stamping, same ordering, so the
    /// story a player reads and the log an engineer debugs from are the same
    /// artifact. Keep the wording plain and past-tense; this is the legibility
    /// deliverable, and it should read like a life rather than a changelog.
    /// </remarks>
    public void Narrate(string text) => Log(LogLevel.Info, "life", text);

    /// <summary>
    /// Log an entry stamped with the current tick.
    /// </summary>
    /// <remarks>
    /// Routing all sim logging through here is what guarantees METHODOLOGY.md §4's
    /// tick-stamping requirement — there is no way to emit an unstamped entry.
    /// </remarks>
    public void Log(LogLevel level, string subsystem, string message) =>
        Logger.Log(Tick, level, subsystem, message);
}
