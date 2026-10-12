using Bclone.Sim.Logging;
using Bclone.Sim.World;

namespace Bclone.Sim.Core;

/// <summary>
/// The nomads — bands that knock, wait at the camp, and settle or move on (`specs/nomads.md`, D563–D564).
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
/// <b>⛔ The decision is a method, not a moment.</b> The knock raises a verb-free <see cref="Moment"/>
/// (`Moment.cs`: a moment never asks the player to choose); the view's band card calls
/// <see cref="LetTheBandSettle"/> or <see cref="SendTheBandOn"/>, as every other decision in the game is a
/// call on this class. Unanswered, the band leaves at the autumn turn (`nomads_wait_seasons`).
/// </para>
/// </remarks>
public sealed partial class SimWorld
{
    /// <summary>How many bands have ever knocked — the key the next knock's year is hashed on. Hashed sparsely, saved.</summary>
    public int BandsEverKnocked { get; internal set; }

    /// <summary>
    /// The year of the next knock once a band has come and gone, or 0 before the first (whose year is
    /// <see cref="FirstKnockYear"/>, derived — so a village nobody has knocked on hashes as it did). Hashed sparsely, saved.
    /// </summary>
    public int NextKnockYear { get; internal set; }

    /// <summary>The band waiting at the camp, or null. Hashed, saved.</summary>
    public NomadBand? Band { get; internal set; }

    /// <summary>Where a band camps — the cart while it stands, else the founding site's centre.</summary>
    public Point BandCamp => TheCart?.Position ?? Point.CentreOf(Map.FoundingSite);

    /// <summary>
    /// Where the band camps, in the village's own words — <i>by the cart</i> while it stands, else <i>at the founding
    /// site</i> (D569; Joe, D568: <i>"what is 'the pile' in the travellers arrival message?"</i>). Derived from
    /// <see cref="BandCamp"/>'s rule, never stored.
    /// </summary>
    public string WhereTheBandCamps => TheCart is not null ? "by the cart" : "at the founding site";

    /// <summary>The tick a waiting band leaves if nobody has answered — the season turn `nomads_wait_seasons` on.</summary>
    public ulong BandLeavesAtTick =>
        Band is null ? 0UL : Band.KnockedAtTick + ((ulong)Config.NomadsWaitSeasons * (ulong)Config.TicksPerSeason);

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
    /// On the spring turn of the knock's year, with somebody alive and nobody already waiting, a band knocks.
    /// Called by <c>CivicSystem</c> every tick; it returns at once on every tick that is not a spring turn.
    /// </summary>
    internal void KnockIfItIsTime()
    {
        if (Config.NomadsFirstYearFloor <= 0 || Band is not null || Tick == 0UL
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

    /// <summary>A band waiting past its deadline moves on — checked on the season turn, never per tick.</summary>
    internal void TheBandMovesOnIfUnanswered()
    {
        if (Band is null || Tick % (ulong)Config.TicksPerSeason != 0UL || Tick < BandLeavesAtTick)
        {
            return;
        }

        NomadBand band = Band;
        DepartBand();
        RaiseMoment(
            "The nomads moved on",
            $"{Titled(band.FamilyNames)} waited {WhereTheBandCamps} through {Config.NomadsWaitSeasons} seasons and nobody said "
            + $"they could stay; they moved on up the valley. {Clock.SeasonAndYear()}.",
            stops: false,
            LogCategory.Life);
    }

    /// <summary>The player sends the waiting band away. No harm, no foul (Joe): the clock just advances.</summary>
    public void SendTheBandOn()
    {
        NomadBand band = Band ?? throw new InvalidOperationException("No band is waiting to be sent on.");
        DepartBand();
        Narrate($"{Titled(band.FamilyNames)} were sent on their way — {band.People} souls. {Clock.SeasonAndYear()}.", LogCategory.Life);
    }

    /// <summary>
    /// The player lets the waiting band settle: each family becomes a roofless household holding what it
    /// carried, and its people are villagers at the camp from this tick (`nomads.md §4.4`). From here the
    /// village does what it already does — the roofless queue sites their houses, or they sleep in the open.
    /// </summary>
    public void LetTheBandSettle()
    {
        NomadBand band = Band ?? throw new InvalidOperationException("No band is waiting to settle.");
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

            // What they carried, between them: received, not produced here (Stockpile.Receive).
            household.Stockpile.Receive(Goods.Produce, family.People.Count * Config.NomadsFoodEach);
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

        DepartBand();
        Narrate(
            $"{Titled(band.FamilyNames)} have settled — {band.People} more souls, and no roof yet. {Clock.SeasonAndYear()}.",
            LogCategory.Life);

        // Arrivals count toward the most the village has ever held, which the wooden bridge is learned by (D546).
        NoticeTheVillageSize(knewHowToBridge);
    }

    /// <summary>The band is gone, whichever way; the next knock's year is set from this one.</summary>
    private void DepartBand()
    {
        Band = null;
        NextKnockYear = NextKnockYearAfter(Clock.Year, BandsEverKnocked);
    }

    private void Knock()
    {
        int population = Population;
        bool hall = TownHall is not null;
        var band = new NomadBand { KnockedAtTick = Tick, MetAPopulationOf = population };
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

        Band = band;

        // ⭐ IT WAITS, like the library's and the hall's gifts: there is something to do about it — a card to
        // open. ⛔ No verbs here (Moment.cs); the card has them.
        int food = FoodTheVillageHolds();
        int perHead = population == 0 ? 0 : food / population;
        RaiseMoment(
            $"Nomads {WhereTheBandCamps}",
            $"{Titled(band.FamilyNames)} — {band.People} souls, {band.Adults} grown and {band.Children} "
            + $"{(band.Children == 1 ? "child" : "children")}, carrying a few days' food — have come up the valley asking "
            + $"for a place to settle. They will camp {WhereTheBandCamps} until the leaves turn. The village holds {food:N0} food, "
            + $"{perHead} a head. Click them to decide. {Clock.SeasonAndYear()}.",
            stops: true,
            LogCategory.Discovery);
    }

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
