using Kith.Sim.Config;
using Kith.Sim.Logging;
using Kith.Sim.World;

namespace Kith.Sim.Core;

/// <summary>
/// ⭐ The library's borrowing (`specs/library-borrowing.md`, D864–D865): which library a home is near enough to, whether a
/// household is writing to kin, and what the library's card says. One copy each, read by the walk's offer and the card —
/// the tavern's shape (`SimWorld.Tavern.cs`, D843).
/// </summary>
public sealed partial class SimWorld
{
    /// <summary>
    /// ⭐ The letters switch (Joe's addition 2, D865) — the Nomads switch's shape: on, households near a library write to kin;
    /// off, nobody fetches paper and no kin come. The village's own, started from <c>letters_enabled</c>, saved, hashed only
    /// when off; a toggle on every library's card.
    /// </summary>
    public bool LettersEnabled { get; set; } = true;

    /// <summary>The library standing on this tile — an errand's fixed destination — or null.</summary>
    public Library? LibraryAt(GridPos tile)
    {
        for (int i = 0; i < Libraries.Count; i++)
        {
            if (Libraries[i].Tile == tile)
            {
                return Libraries[i];
            }
        }

        return null;
    }

    /// <summary>
    /// The nearest open library within <c>library_reach_tiles</c> of this household's home by the one cost field, or null (no
    /// home, no library, none near enough). Ties go to the first in the list (D15).
    /// </summary>
    public Library? LibraryWithinReachOf(Household household)
    {
        ArgumentNullException.ThrowIfNull(household);
        if (household.HomeTile is not GridPos home)
        {
            return null;
        }

        Library? best = null;
        int bestCost = int.MaxValue;
        for (int i = 0; i < Libraries.Count; i++)
        {
            Library library = Libraries[i];
            if (library.Closed)
            {
                continue;
            }

            int cost = WalkToTheLibrary(home, library);
            if (cost < bestCost)
            {
                best = library;
                bestCost = cost;
            }
        }

        return best;
    }

    /// <summary>
    /// The walk from a home to a library in the one cost field, or <see cref="int.MaxValue"/> beyond <c>library_reach_tiles</c> or
    /// not walkable at all — <b>the one copy of "within reach"</b>, asked by the offer and the card alike.
    /// </summary>
    private int WalkToTheLibrary(GridPos home, Library library)
    {
        int cost = TravelCost.Cost(home, library.Tile);
        return cost != TravelCostField.Unreachable && cost <= Config.LibraryReachTiles * TravelCostField.BaseTileCost
            ? cost
            : int.MaxValue;
    }

    /// <summary>
    /// Whether a household is writing to kin (§5.2): letters on, a spare bed (Joe's cap), no answer standing, and its generation
    /// open — no kin come within <c>letters_generation_years</c> (call 2A).
    /// </summary>
    public bool HouseholdIsWriting(Household household)
    {
        ArgumentNullException.ThrowIfNull(household);
        return LettersEnabled && HasASpareBed(household) && !household.AnswerWaiting && TheGenerationIsOpen(household);
    }

    /// <summary>
    /// Whether a lived-in home has a bed nobody sleeps in — Joe's cap, kin need a bed; <c>HouseholdCapacity</c> is the birth
    /// gate's own question.
    /// </summary>
    public bool HasASpareBed(Household household)
    {
        ArgumentNullException.ThrowIfNull(household);
        int living = LivingMembersOf(household);
        return household.HasHome && living > 0 && living < VillageEconomy.HouseholdCapacity(household.HomeKind, Config);
    }

    /// <summary>
    /// ⭐ The birth gate's food half, asked of kin as of a child (Joe's call 4A, D865): the village holds a birth's food and last
    /// year's harvest fed one more. The same two questions `HouseholdSystem`'s birth gate asks, by the same two doors.
    /// </summary>
    public bool TheVillageHasFoodToSpare() =>
        FoodTheVillageHolds() >= FoodABirthNeeds(Population) && TheHarvestFeedsOneMore();

    /// <summary>The year this family may next be answered — <c>letters_generation_years</c> after its kin came — or 0 if none has.</summary>
    public int KinMayComeAgainFrom(Household household)
    {
        ArgumentNullException.ThrowIfNull(household);
        Villager? kin = household.KinId == 0 ? null : FindVillager(household.KinId);
        return kin is null ? 0 : SimClock.FromTick(kin.ArrivedAtTick, Config).Year + Config.LettersGenerationYears;
    }

    private bool TheGenerationIsOpen(Household household) =>
        household.KinId == 0 || Clock.Year >= KinMayComeAgainFrom(household);

    /// <summary>The letter this family's kin answer on (§5.2) — <c>[1, letters_answered_within]</c>, a fresh hash a generation.</summary>
    public int AnsweringLetter(Household household)
    {
        ArgumentNullException.ThrowIfNull(household);
        return NameHash.AnsweringLetter(Seed, household.Id, household.KinAnswered, Config.LettersAnsweredWithin);
    }

    /// <summary>
    /// What a standing answer waits on, in the card's words — <i>a bed</i>, then <i>food to spare</i>, else <i>the season's
    /// turn</i> — or null with none standing. Asked when wanted, never stored (D335).
    /// </summary>
    public string? WhatTheAnswerWaitsOn(Household household)
    {
        ArgumentNullException.ThrowIfNull(household);
        return !household.AnswerWaiting ? null
            : !HasASpareBed(household) ? "a bed"
            : !TheVillageHasFoodToSpare() ? "food to spare"
            : "the season's turn";
    }

    /// <summary>
    /// ⭐ The letter, written at the writer's own door (§5.1–§5.2): the household's count rises, the generation's first is said
    /// once, and on the answering letter kin come — or the answer stands, said once. Paper brought home after the house stopped
    /// writing (letters off, an answer standing, kin come meanwhile) is put away uncounted. ⚠️ The bed is <b>not</b> asked here:
    /// the paper was fetched with one, and a child born before the letter is written is exactly the answer the bed holds.
    /// </summary>
    internal void WriteALetter(Villager writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.CarryingPaper = false;
        Household household = HouseholdOf(writer);
        if (!LettersEnabled || household.AnswerWaiting || !TheGenerationIsOpen(household))
        {
            return;
        }

        household.LettersWritten++;
        if (household.LettersWritten == 1)
        {
            Narrate($"{writer.FullName} wrote to kin in the lowlands. {Clock.SeasonAndYear()}.", LogCategory.Life);
        }

        if (household.LettersWritten < AnsweringLetter(household))
        {
            return;
        }

        household.LetterWriterId = writer.Id;
        if (TryToBringTheKin(household))
        {
            return;
        }

        // ⭐ SAID ONCE (§5.2): the card carries what it waits on from here, and kin come at the first season's turn both hold.
        household.AnswerWaiting = true;
        string when = HasASpareBed(household) ? "the village has food to spare" : "there is a bed in the house";
        Narrate($"A letter came back for the {household.Name} household: kin will come when {when}. {Clock.SeasonAndYear()}.",
            LogCategory.Life);
    }

    /// <summary>On a season's turn, every standing answer is asked again (§5.2). Called by <c>CivicSystem</c> every tick; returns at once off the turn.</summary>
    internal void AnswerTheWaitingLettersAtTheTurn()
    {
        if (Tick == 0UL || Tick % (ulong)Config.TicksPerSeason != 0UL)
        {
            return;
        }

        HonourTheWaitingAnswers();
    }

    /// <summary>Kin come to every household whose answer stands and whose bed and village's food now hold — letters off, none.</summary>
    internal void HonourTheWaitingAnswers()
    {
        if (!LettersEnabled)
        {
            return;
        }

        for (int i = 0; i < Households.Count; i++)
        {
            Household household = Households[i];
            if (household.AnswerWaiting && TryToBringTheKin(household))
            {
                household.AnswerWaiting = false;
            }
        }
    }

    /// <summary>
    /// ⭐ Kin come (§5.3), if there is a bed and the village has food to spare: one unmarried adult of 18–30, a surname of their own
    /// from the lowlands, a first name, lifespan and rhythm by the nomads' hashes keyed clear of theirs, generation 1, new to the
    /// valley, at the door — carrying the nomads' food into the larder by the ledger's own door (<c>FoodEverBroughtIn</c>).
    /// </summary>
    private bool TryToBringTheKin(Household household)
    {
        if (!HasASpareBed(household) || !TheVillageHasFoodToSpare())
        {
            return false;
        }

        SimConfig c = Config;
        int key = 3_000_000 + (household.Id * 64) + (household.KinAnswered % 64);
        int id = NextVillagerId();
        int age = NameHash.NomadAge(Seed, key, 18, 30);
        var kin = new Villager
        {
            Id = id,
            Name = NomadNameFor(key, new HashSet<string>(StringComparer.Ordinal)),
            Surname = SurnameNobodyCarries(key, new HashSet<string>(StringComparer.Ordinal)),
            LifespanYears = NameHash.NomadLifespan(Seed, key, c.LifespanYearsBase, c.LifespanYearsVariance),
            Rhythm = c.SeededRhythm ? NameHash.NomadRhythm(Seed, key, c.TicksPerDay) : 0,
            Carried = NewStockpile(),
            Hunger = c.SeededRhythm && c.EatThreshold > 0 ? NameHash.MealPhase(Seed, id, c.EatThreshold) : 0,
            Position = Point.CentreOf(household.HomeTile!.Value),
            HouseholdId = household.Id,
            BirthTick = (long)Tick - ((long)age * c.TicksPerYear) - NameHash.BirthdayOffset(Seed, id, c.TicksPerYear),
            AgeYears = age,
            LifeStage = LifeStage.Adult,
            Founder = false,
            ArrivedAtTick = Tick,
            CameByLetterOf = household.Id,
        };

        household.AddMember(id);
        Villagers.Add(kin);
        FoodEverBroughtIn += household.Stockpile.Receive(Goods.Produce, c.NomadsFoodEach);
        household.KinId = id;
        household.KinAnswered++;
        household.LettersWritten = 0;

        Villager? writer = household.LetterWriterId == 0 ? null : FindVillager(household.LetterWriterId);
        string whose = writer is null ? "at a letter from home" : $"at {writer.Name}'s letter";
        Narrate($"{kin.FullName} has come up the valley to live with the {household.Name} household, {whose}. "
            + $"{Clock.SeasonAndYear()}.", LogCategory.Life);
        return true;
    }

    /// <summary>
    /// ⭐ The library's card sentence (§5.4, D843's lesson; Joe's addition, D865): whether letters are off, the library fallen in,
    /// how many lived-in homes are within reach, and how many of them are writing with how many letters out. Counted when asked,
    /// never stored (D335). <c>Working</c> is false when nobody can borrow.
    /// </summary>
    public (bool Working, string Sentence) SayTheLibrary(Library library)
    {
        ArgumentNullException.ThrowIfNull(library);
        if (!LettersEnabled)
        {
            return (false, "Letters to kin are off — nobody writes, and nobody comes.");
        }

        if (library.Closed)
        {
            return (false, "It has fallen in — nobody borrows until the builders mend it.");
        }

        int reach = Config.LibraryReachTiles;
        int within = 0, homes = 0, writing = 0, letters = 0, onABed = 0, onFood = 0, came = 0;
        for (int i = 0; i < Households.Count; i++)
        {
            Household household = Households[i];
            if (household.HomeTile is not GridPos home || LivingMembersOf(household) == 0)
            {
                continue;
            }

            homes++;
            if (WalkToTheLibrary(home, library) == int.MaxValue)
            {
                continue;
            }

            within++;
            if (household.KinId != 0)
            {
                came++;
            }

            if (household.AnswerWaiting)
            {
                // ⭐ JOE'S ADDITION (D865): the answers waiting, and on what — a bed first, as the answer asks.
                if (!HasASpareBed(household))
                {
                    onABed++;
                }
                else
                {
                    onFood++;
                }
            }
            else if (household.LettersWritten > 0)
            {
                writing++;
                letters += household.LettersWritten;
            }
        }

        if (within == 0)
        {
            return (false, $"No home is within a {reach}-tile walk, so nobody comes to borrow. A library draws the homes around "
                + "it: build it near them.");
        }

        string lends = within == homes
            ? $"Lends paper to every home — all are within a {reach}-tile walk"
            : $"Lends paper to {within} of {homes} homes within a {reach}-tile walk";
        var parts = new List<string>();
        if (writing > 0)
        {
            parts.Add($"{writing} {(writing == 1 ? "is" : "are")} writing to kin, {letters} {(letters == 1 ? "letter" : "letters")} "
                + (onABed + onFood + came == 0 ? "out, none answered yet" : "out"));
        }

        if (onABed > 0)
        {
            parts.Add($"{onABed} {(onABed == 1 ? "answer waits" : "answers wait")} on a bed");
        }

        if (onFood > 0)
        {
            parts.Add($"{onFood} {(onFood == 1 ? "answer waits" : "answers wait")} on food to spare");
        }

        if (came > 0)
        {
            parts.Add($"kin have come to {came} {(came == 1 ? "home" : "homes")}");
        }

        return (true, parts.Count == 0 ? lends + "." : $"{lends} — {string.Join(", ", parts)}.");
    }
}
