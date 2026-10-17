using Bclone.Sim.Logging;
using Bclone.Sim.World;

namespace Bclone.Sim.Core;

/// <summary>
/// The nomads — bands that knock and settle the same tick (`specs/nomads.md`, D563–D564, D572).
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐ A knock is a hash of the seed and the count of bands, never a draw</b> (§4.1): the year of the first
/// knock is <c>nomads_first_year_floor</c> plus a hashed share of its spread, and every later one is the
/// last plus a floor and a spread — a shorter floor once the town hall stands (Joe, D563: <em>the hall widens
/// them</em>). Nothing in this file touches <see cref="SimWorld.Rng"/>, so a village that sends every band
/// away keeps the stream it had.
/// </para>
/// <para>
/// <b>⛔ The switch is the permission (Joe, D572).</b> D565 stored a waiting band with a card and two buttons; the
/// buttons sat in a fold nobody opened and the band moved on unanswered. Now a band that knocks while
/// <see cref="NomadsEnabled"/> is on <b>settles that tick</b>, and the moment that says so is verb-free as every
/// moment is (`Moment.cs`). Nothing waits, nothing is stored: a band is derived at the knock and gone into the
/// roster by the end of it.
/// </para>
/// </remarks>
public sealed partial class SimWorld
{
    /// <summary>How many bands have ever knocked — the key the next knock's year is hashed on. Hashed sparsely, saved.</summary>
    public int BandsEverKnocked { get; internal set; }

    /// <summary>
    /// ⭐ The Nomads switch (Joe, D568; D570; the permission itself since D572). On: a band that knocks settles that
    /// tick. Off: no band knocks and an empty valley is never found again (<see cref="RefoundIfTheValleyHasWaited"/>).
    /// Back on with a knock year already past, the band comes on the next year's turn — the door was shut, they were on
    /// the road. <b>The village's own</b>, in the share-out switch's shape: hashed only when off, saved, started from
    /// <c>nomads_enabled</c> (the new-game screen's row); a tick on the villagers bar — never a setting
    /// (`settings-persistence.md §3`).
    /// </summary>
    public bool NomadsEnabled { get; set; } = true;

    /// <summary>
    /// The year of the next knock once a band has come and gone, or 0 before the first (whose year is
    /// <see cref="FirstKnockYear"/>, derived — so a village nobody has knocked on hashes as it did). Hashed sparsely, saved.
    /// </summary>
    public int NextKnockYear { get; internal set; }

    /// <summary>Where a band camps — the cart while it stands, else the founding site's centre.</summary>
    public Point BandCamp => TheCart?.Position ?? Point.CentreOf(Map.FoundingSite);

    /// <summary>
    /// Where the band camps, in the village's own words — <i>by the cart</i> while it stands, else <i>at the founding
    /// site</i> (D569; Joe, D568: <i>"what is 'the pile' in the travellers arrival message?"</i>). Derived from
    /// <see cref="BandCamp"/>'s rule, never stored.
    /// </summary>
    public string WhereTheBandCamps => TheCart is not null ? "by the cart" : "at the founding site";

    /// <summary>The year the first band knocks for this seed — the floor plus a hashed share of the spread; 0 = never.</summary>
    public int FirstKnockYear() =>
        Config.NomadsFirstYearFloor <= 0
            ? 0
            : Config.NomadsFirstYearFloor + NameHash.KnockOffset(Seed, 0, Config.NomadsFirstYearSpread);

    /// <summary>
    /// The year the band after band number <paramref name="bandsSoFar"/> knocks, counted from
    /// <paramref name="fromYear"/>: the between-floor (the hall's shorter one while it stands) plus a hashed share
    /// of the between-spread.
    /// </summary>
    public int NextKnockYearAfter(int fromYear, int bandsSoFar)
    {
        int floor = TownHall is null ? Config.NomadsYearsBetweenFloor : Config.NomadsHallYearsBetweenFloor;
        return fromYear + Math.Max(1, floor) + NameHash.KnockOffset(Seed, bandsSoFar, Config.NomadsYearsBetweenSpread);
    }

    /// <summary>
    /// On the spring turn of the knock's year, with somebody alive and the switch on, a band knocks — and settles.
    /// Called by <c>CivicSystem</c> every tick; it returns at once on every tick that is not a spring turn.
    /// </summary>
    internal void KnockIfItIsTime()
    {
        if (Config.NomadsFirstYearFloor <= 0 || !NomadsEnabled || Tick == 0UL
            || Tick % (ulong)Config.TicksPerYear != 0UL || Population == 0)
        {
            return;
        }

        // The first knock's year is derived, never stored (the hash stays sparse until somebody has knocked);
        // every later one was set when the last band left or settled.
        int due = NextKnockYear == 0 ? FirstKnockYear() : NextKnockYear;
        if (Clock.Year < due)
        {
            return;
        }

        Knock();
    }

    /// <summary>
    /// The band settles: each family becomes a roofless household holding what it carried, and its people are
    /// villagers at the camp from this tick (`nomads.md §4.4`). From here the village does what it already does —
    /// the roofless queue sites their houses, or they sleep in the open. Returns whether their coming taught the
    /// village to bridge water — said in the nomads' moment, not a second one (D576).
    /// </summary>
    private bool Settle(NomadBand band)
    {
        bool knewHowToBridge = IsUnlocked(BuildingKind.Bridge);
        int nextVillager = NextVillagerId();
        int nextHousehold = NextHouseholdId();

        foreach (NomadFamily family in band.Families)
        {
            var household = new Household
            {
                Id = nextHousehold++,
                Surname = family.Surname,
                Stockpile = NewLarder(),
                DayForAChild = NameHash.DayForAChild(Seed, nextHousehold - 1, Config.DaysPerSeason * 4),
                HomePosition = null,
            };

            // What they carried, between them: received, not produced here (Stockpile.Receive) — and the village's
            // ledger sees it come in by its own door (`FoodEverBroughtIn`, D572), or the conservation guard would
            // see food appear from nowhere.
            int carried = family.People.Count * Config.NomadsFoodEach;
            household.Stockpile.Receive(Goods.Produce, carried);
            FoodEverBroughtIn += carried;
            Households.Add(household);

            Villager? firstAdult = null;
            foreach (NomadPerson person in family.People)
            {
                int id = nextVillager++;
                var villager = new Villager
                {
                    Id = id,
                    Name = person.Name,
                    Surname = household.Surname,
                    LifespanYears = person.LifespanYears,
                    Rhythm = person.Rhythm,
                    Carried = NewStockpile(),

                    // Their place on the meal's cycle, as a child born here gets one (D529) — never a draw.
                    Hunger = Config.SeededRhythm && Config.EatThreshold > 0 ? NameHash.MealPhase(Seed, id, Config.EatThreshold) : 0,
                    Position = BandCamp,
                    HouseholdId = household.Id,

                    // A birthday of their own, their age back from today (D468's shape for a founder).
                    BirthTick = (long)Tick - ((long)person.AgeYears * Config.TicksPerYear)
                        - NameHash.BirthdayOffset(Seed, id, Config.TicksPerYear),
                    AgeYears = person.AgeYears,
                    LifeStage = person.Adult ? LifeStage.Adult : LifeStage.Child,
                    Founder = false,
                    ArrivedAtTick = Tick,
                };

                if (person.TradeSkillId != 0)
                {
                    // A journeyman, three quarters of the way, for the founding's reason (`GiveTheFoundersTheirTrades`).
                    SkillRow? row = null;
                    for (int i = 0; i < Config.Skills.Count; i++)
                    {
                        if (Config.Skills[i].Id == person.TradeSkillId)
                        {
                            row = Config.Skills[i];
                        }
                    }

                    if (row is not null)
                    {
                        SkillProgress progress = villager.ProgressIn(row.Id);
                        progress.Work = Config.MasteryWorkFor(row) * 3 / 4;
                        progress.Ticks = Config.MasteryYearsFor(row) * Config.TicksPerYear * 3 / 4;
                    }
                }

                household.AddMember(villager.Id);
                Villagers.Add(villager);

                // ⛔ THE TWO ADULTS ARE A COUPLE, as the founding pairs its founders — unpaired, they would
                // pair across the band on this very tick and take over a dead household whole (D564's trap).
                if (person.Adult)
                {
                    if (firstAdult is null)
                    {
                        firstAdult = villager;
                    }
                    else if (firstAdult.PartnerId == 0)
                    {
                        firstAdult.PartnerId = villager.Id;
                        villager.PartnerId = firstAdult.Id;
                    }
                }
            }
        }

        Narrate(
            $"{Titled(band.FamilyNames)} have settled — {band.People} more souls, and no roof yet. {Clock.SeasonAndYear()}.",
            LogCategory.Life);

        // Arrivals count toward the most the village has ever held, which the wooden bridge is learned by (D546).
        // ⭐ Not `NoticeTheVillageSize`: its stop and the nomads' popped up together (Joe, D576) — and a band is what
        // carries the village past the number in nearly every village that reaches it — so the knock says it.
        return LearnedToBridgeJustNow(knewHowToBridge);
    }

    private void Knock()
    {
        int population = Population;
        bool hall = TownHall is not null;
        var band = new NomadBand();
        int people = NomadBand.PeopleFor(Config, population, hall);
        int families = NomadBand.FamiliesFor(people, hall);
        int knockIndex = BandsEverKnocked;
        BandsEverKnocked++;

        // Two adults a family, the rest children shared round. The hashes are keyed on the band's own
        // numbers — a million past any villager id — so a nomad's name is as fixed as a founder's and as
        // free of the stream (`NameHash`). One journeyman a band, by hash, of a hashed founding trade.
        int adults = families * 2;
        int children = Math.Max(0, people - adults);
        var taken = new HashSet<string>(StringComparer.Ordinal);
        var surnames = new HashSet<string>(StringComparer.Ordinal);
        int journeyman = (int)(NameHash.Mix(Seed, knockIndex, NameHash.NomadTradeSalt, 0) % (ulong)adults);
        IReadOnlyList<SkillRow> trades = Config.FoundingSkills();
        int trade = trades.Count == 0
            ? 0
            : trades[(int)(NameHash.Mix(Seed, knockIndex, NameHash.NomadTradeSalt, 1) % (ulong)trades.Count)].Id;

        int person = 0;
        for (int f = 0; f < families; f++)
        {
            var family = new NomadFamily { Surname = NomadSurnameFor(knockIndex, f, surnames) };
            surnames.Add(family.Surname);
            int ownChildren = (children / families) + (f < children % families ? 1 : 0);
            for (int m = 0; m < 2 + ownChildren; m++, person++)
            {
                bool adult = m < 2;
                int key = 1_000_000 + (knockIndex * 64) + person;
                family.People.Add(new NomadPerson
                {
                    Name = NomadNameFor(key, taken),
                    AgeYears = adult ? NameHash.NomadAge(Seed, key, 18, 40) : NameHash.NomadAge(Seed, key, 2, 11),
                    Adult = adult,
                    LifespanYears = NameHash.NomadLifespan(Seed, key, Config.LifespanYearsBase, Config.LifespanYearsVariance),
                    Rhythm = Config.SeededRhythm ? NameHash.NomadRhythm(Seed, key, Config.TicksPerDay) : 0,
                    TradeSkillId = adult && (f * 2) + m == journeyman ? trade : 0,
                });
            }

            band.Families.Add(family);
        }

        // ⭐ THE SWITCH IS THE PERMISSION (Joe, D572): they settle the tick they come, and the next knock's year is set
        // from this one. The moment stops (D232: at 4× an arrival that adds people must not slide past unread) and
        // carries no verbs (Moment.cs) — there is nothing to decide, only something to know.
        string where = WhereTheBandCamps;
        bool learnedToBridge = Settle(band);
        NextKnockYear = NextKnockYearAfter(Clock.Year, BandsEverKnocked);

        // ⭐ TWO SHORT PARAGRAPHS, NOT A WALL (Joe, D577, from a screenshot: *"too verbose … 1-2 sentences only"*): who
        // came and where they sleep; then, only when their coming taught it, the bridge — credited to their road.
        // The food a head went with the wall; the bar carries it.
        string body = $"{Titled(band.FamilyNames)}, {band.People} souls, have come up the valley to make a home here. "
            + $"Until a house is raised for them in the painted land, they sleep in the open {where}. {Clock.SeasonAndYear()}.";
        var marks = new List<MomentMark> { MomentMark.Nomads };
        if (learnedToBridge)
        {
            body += Moment.ParagraphBreak + TheyBroughtTheBridge;
            marks.Add(MomentMark.Bridge);
        }

        RaiseMoment($"Nomads have settled {where}", body, stops: true, LogCategory.Discovery, marks);
    }

    /// <summary>The bridge, when a band's coming is what teaches it (D576) — said as theirs (Joe, D577).</summary>
    private const string TheyBroughtTheBridge = "On the long road here they learned to lay timber across running water. "
        + "The village can build wooden bridges now, and the far bank is within reach.";

    private string NomadNameFor(int key, HashSet<string> taken)
    {
        IReadOnlyList<string> prefixes = Config.FirstNamePrefixes;
        IReadOnlyList<string> suffixes = Config.FirstNameSuffixes;
        int attempts = prefixes.Count * suffixes.Count;
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            string candidate = NameHash.FirstName(Seed, key, attempt, prefixes, suffixes);
            if (!IsNameInUse(candidate) && taken.Add(candidate))
            {
                return candidate;
            }
        }

        return NameHash.FirstName(Seed, key, 0, prefixes, suffixes);
    }

    /// <summary>A surname nobody living carries and no other family of this band took — the founding's rule, keyed on the band.</summary>
    private string NomadSurnameFor(int knockIndex, int family, HashSet<string> surnames)
    {
        IReadOnlyList<string> names = Config.HouseholdNames;
        int key = 1_000_000 + (knockIndex * 8) + family;
        for (int attempt = 0; attempt < names.Count * 4; attempt++)
        {
            string candidate = NameHash.Surname(Seed, key, attempt, names);
            if (!surnames.Contains(candidate) && !Households.Exists(h => LivingMembersOf(h) > 0 && h.Surname == candidate))
            {
                return candidate;
            }
        }

        return NameHash.Surname(Seed, key, 0, names);
    }

    /// <summary>Ids are never reused: one past the highest ever given.</summary>
    internal int NextVillagerId()
    {
        int max = 0;
        for (int i = 0; i < Villagers.Count; i++)
        {
            max = Math.Max(max, Villagers[i].Id);
        }

        return max + 1;
    }

    internal int NextHouseholdId()
    {
        int max = 0;
        for (int i = 0; i < Households.Count; i++)
        {
            max = Math.Max(max, Households[i].Id);
        }

        return max + 1;
    }

    private static string Titled(string names) =>
        names.Length == 0 ? names : char.ToUpperInvariant(names[0]) + names[1..];
}
