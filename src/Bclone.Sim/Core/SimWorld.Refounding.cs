using Bclone.Sim.Logging;
using Bclone.Sim.World;

namespace Bclone.Sim.Core;

/// <summary>
/// A dead valley re-founded (`specs/nomads.md §4.6`, D567; Joe's call 4, D563: <i>automatic, a few years on</i>).
/// With nobody alive, four nomads walk in on the spring turn a hashed few years after the last death — a second
/// generation of founders — and the village the player left is theirs: the standing houses, the stores, the ruins.
/// </summary>
/// <remarks>
/// <para>
/// <b>No question asked</b>: nobody is left to speak for the village. The empty-village banner (D313) is untouched —
/// it is a passing line and the game runs on, which is what lets this happen. ⛔ No <c>Rng</c> draw anywhere: the
/// wait and the four are hashed, keyed on how many times the valley has been re-founded.
/// </para>
/// <para>
/// <b>The tick the valley emptied is derived</b> — the latest death on the roster — never stored (CLAUDE.md: nothing
/// derivable is kept twice). <see cref="Refoundings"/> is the one new fact, and it is sparse.
/// </para>
/// </remarks>
public sealed partial class SimWorld
{
    /// <summary>How many times the valley has been re-founded — the key the wait is hashed on. Hashed sparsely, saved.</summary>
    public int Refoundings { get; internal set; }

    /// <summary>The tick the last villager died, while nobody is alive — the latest death on the roster; null with anybody alive or nobody ever.</summary>
    public ulong? EmptiedAtTick
    {
        get
        {
            if (Villagers.Count == 0 || Population > 0)
            {
                return null;
            }

            ulong latest = 0;
            for (int i = 0; i < Villagers.Count; i++)
            {
                if (Villagers[i].DiedAtTick is ulong died && died > latest)
                {
                    latest = died;
                }
            }

            return latest;
        }
    }

    /// <summary>The year a re-founding is due for an emptying in <paramref name="emptiedInYear"/> — the floor plus a hashed share of the spread; 0 = never.</summary>
    public int RefoundYearAfter(int emptiedInYear) =>
        Config.RefoundYearsFloor <= 0
            ? 0
            : emptiedInYear + Config.RefoundYearsFloor + NameHash.RefoundOffset(Seed, Refoundings, Config.RefoundYearsSpread);

    /// <summary>On the spring turn of the due year, with nobody alive, four nomads re-found the valley. Called by <c>CivicSystem</c> every tick.</summary>
    internal void RefoundIfTheValleyHasWaited()
    {
        // One switch (Joe, D570): off, a dead valley stays dead as well.
        if (Config.RefoundYearsFloor <= 0 || !NomadsEnabled || Tick == 0UL || Tick % (ulong)Config.TicksPerYear != 0UL
            || EmptiedAtTick is not ulong emptied)
        {
            return;
        }

        int emptiedInYear = SimClock.FromTick(emptied, Config).Year;
        if (Clock.Year < RefoundYearAfter(emptiedInYear))
        {
            return;
        }

        RefoundVillage();
    }

    private void RefoundVillage()
    {
        int generation = Refoundings;
        Refoundings++;
        (int sound, int worn, int failing, int ruin) = CountWhatStands();
        int foodInStores = FoodTheVillageHolds();

        // The cart they arrived in: restocked where it stands, raised again where it was pulled down.
        if (TheCart is StoreBuilding cart)
        {
            cart.Store.Receive(Goods.Produce, Config.CartFood);
            cart.Store.Receive(Goods.Tools, Config.CartTools);
        }
        else
        {
            RaiseTheCart(Config, Map.FoundingSite);
        }

        // The founding's shape: two households of two adults, paired, a master and a journeyman among them — all by
        // hash (`NameHash`), keyed two million past any villager id so no first founding's name is repeated by chance.
        var taken = new HashSet<string>(StringComparer.Ordinal);
        var surnames = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyList<SkillRow> trades = Config.FoundingSkills();
        var available = new List<SkillRow>(trades);
        var names = new List<string>();
        int person = 0;
        for (int h = 0; h < 2; h++)
        {
            int householdId = NextHouseholdId();
            var household = new Household
            {
                Id = householdId,
                Surname = RefoundSurnameFor(generation, h, surnames),
                Stockpile = NewLarder(),
                DayForAChild = NameHash.DayForAChild(Seed, householdId, Config.DaysPerSeason * 4),
                HomePosition = null,
            };
            surnames.Add(household.Surname);
            Households.Add(household);
            names.Add($"the {household.Surname}s");

            Villager? first = null;
            for (int a = 0; a < 2; a++, person++)
            {
                int id = NextVillagerId();
                int key = 2_000_000 + (generation * 64) + person;
                var villager = new Villager
                {
                    Id = id,
                    Name = NomadNameFor(key, taken),
                    Surname = household.Surname,
                    LifespanYears = NameHash.NomadLifespan(Seed, key, Config.LifespanYearsBase, Config.LifespanYearsVariance),
                    Rhythm = Config.SeededRhythm ? NameHash.NomadRhythm(Seed, key, Config.TicksPerDay) : 0,
                    Carried = NewStockpile(),
                    Hunger = Config.SeededRhythm && Config.EatThreshold > 0 ? NameHash.MealPhase(Seed, id, Config.EatThreshold) : 0,
                    Position = BandCamp,
                    HouseholdId = household.Id,
                    BirthTick = (long)Tick - ((long)Config.FounderAge * Config.TicksPerYear) - NameHash.BirthdayOffset(Seed, id, Config.TicksPerYear),
                    AgeYears = Config.FounderAge,
                    LifeStage = LifeStage.Adult,
                    Founder = true,
                    ArrivedAtTick = Tick,
                };

                // The founding's tiers (`GiveTheFoundersTheirTrades`), the pick hashed rather than drawn.
                SkillTier tier = person < Config.FoundingMasters ? SkillTier.Master
                    : person < Config.FoundingMasters + Config.FoundingJourneymen ? SkillTier.Journeyman
                    : SkillTier.Novice;
                if (tier != SkillTier.Novice && available.Count > 0)
                {
                    SkillRow trade = available[(int)(NameHash.Mix(Seed, generation, NameHash.NomadTradeSalt, 16 + person) % (ulong)available.Count)];
                    available.Remove(trade);
                    SkillProgress progress = villager.ProgressIn(trade.Id);
                    if (tier == SkillTier.Master)
                    {
                        progress.Work = Config.MasteryWorkFor(trade);
                        progress.Ticks = Config.MasteryYearsFor(trade) * Config.TicksPerYear;
                        progress.Mastered = true;
                    }
                    else
                    {
                        progress.Work = Config.MasteryWorkFor(trade) * 3 / 4;
                        progress.Ticks = Config.MasteryYearsFor(trade) * Config.TicksPerYear * 3 / 4;
                    }
                }

                household.AddMember(villager.Id);
                Villagers.Add(villager);
                if (first is null)
                {
                    first = villager;
                }
                else
                {
                    first.PartnerId = villager.Id;
                    villager.PartnerId = first.Id;
                }
            }
        }

        StandingChanged();

        // The knock's clock resumes from the re-founding (§7): the next band comes a few years on, as after a band left.
        NextKnockYear = NextKnockYearAfter(Clock.Year, BandsEverKnocked);

        string standing = sound + worn + failing + ruin == 0
            ? "nothing standing but the ground"
            : $"{Roofs(sound, "sound")}, {Roofs(worn, "worn")}, {Roofs(failing, "failing")} and {Roofs(ruin, "fallen in")}";
        RaiseMoment(
            "Four nomads have found the empty valley",
            $"{Titled(string.Join(" and ", names))} walked into {Name} and found nobody — {standing}, and {foodInStores:N0} food "
            + $"still in the stores. The valley is theirs now. {Clock.SeasonAndYear()}.",
            stops: true,
            LogCategory.Discovery);
    }

    private static string Roofs(int count, string state) => $"{count} {state}";

    /// <summary>A surname no household has ever carried — living or dead — so a re-founding is a new lineage, not a ghost's.</summary>
    private string RefoundSurnameFor(int generation, int household, HashSet<string> surnames)
    {
        IReadOnlyList<string> names = Config.HouseholdNames;
        int key = 2_000_000 + (generation * 8) + household;
        for (int attempt = 0; attempt < names.Count * 4; attempt++)
        {
            string candidate = NameHash.Surname(Seed, key, attempt, names);
            if (!surnames.Contains(candidate) && !Households.Exists(h => h.Surname == candidate))
            {
                return candidate;
            }
        }

        return NameHash.Surname(Seed, key, 0, names);
    }

    /// <summary>Everything standing, by condition — for the moment that says what the four found.</summary>
    private (int Sound, int Worn, int Failing, int Ruin) CountWhatStands()
    {
        var counts = new int[4];
        void Count(Condition c) => counts[(int)c]++;
        for (int i = 0; i < Households.Count; i++)
        {
            if (Households[i].HasHome) { Count(ConditionOf(Households[i])); }
        }

        for (int i = 0; i < Workplaces.Count; i++)
        {
            if (!Workplaces[i].IsSite && JobsCatalog.WorksAt(Workplaces[i].Kind) is not null) { Count(ConditionOf(Workplaces[i])); }
        }

        for (int i = 0; i < StoreBuildings.Count; i++)
        {
            if (StoreBuildings[i].Kind is not StoreKind.Cart and not StoreKind.Pile) { Count(ConditionOf(StoreBuildings[i])); }
        }

        for (int i = 0; i < Libraries.Count; i++) { Count(ConditionOf(Libraries[i])); }
        for (int i = 0; i < Wells.Count; i++) { Count(ConditionOf(Wells[i])); }
        for (int i = 0; i < Bridges.Count; i++) { Count(ConditionOf(Bridges[i])); }
        if (TownHall is TownHall hall) { Count(ConditionOf(hall)); }
        return (counts[0], counts[1], counts[2], counts[3]);
    }
}
