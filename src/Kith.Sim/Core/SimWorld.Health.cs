using Kith.Sim.Logging;
using Kith.Sim.World;

namespace Kith.Sim.Core;

/// <summary>How a person's body is, as the card's sixth pill says it (W6, `health.md §5.3–5.4`).</summary>
public enum HealthState
{
    /// <summary>Nothing troubling.</summary>
    Well = 0,

    /// <summary>A year or more on one food: the warning, and nothing yet.</summary>
    PlainDiet = 1,

    /// <summary>Three years or more on one food: they may fall ill (step 4).</summary>
    RunDown = 2,

    /// <summary>Ill — on their way home to bed, or in it (§5.5).</summary>
    Sick = 3,
}

/// <summary>
/// ⭐ W6's words (`health.md §5.4`): the person card's health sentence and the village's two warnings — the diet said before it
/// bites (Phase 6 gate item 6). Never a number: the pill says <i>Well / Plain diet / Run down</i> and this says why.
/// </summary>
public sealed partial class SimWorld
{
    /// <summary>Where <paramref name="villager"/>'s body stands — read off the plain seasons, never stored twice.</summary>
    public HealthState HealthOf(Villager villager)
    {
        ArgumentNullException.ThrowIfNull(villager);
        if (villager.SickDays > 0 || villager.State == VillagerState.SickInBed)
        {
            return HealthState.Sick;
        }

        if (!Config.HealthOn || villager.PlainSeasons < Config.HealthPlainDietSeasons)
        {
            return HealthState.Well;
        }

        return villager.PlainSeasons >= Config.HealthRunDownSeasons ? HealthState.RunDown : HealthState.PlainDiet;
    }

    /// <summary>
    /// The card's sentence: null when well; <i>"Ada has eaten nothing but forage for a year. A body wants more than one kind of
    /// food."</i> on a plain diet; <i>"Ada has eaten nothing but forage for three years, and it's telling. Fish, meat or grain
    /// would set it right."</i> run down.
    /// </summary>
    public string? HealthNoteFor(Villager villager)
    {
        HealthState state = HealthOf(villager);
        if (state == HealthState.Well)
        {
            return null;
        }

        string food = FoodGroups.Word(villager.PlainOn);
        string years = YearsWord(villager.PlainSeasons);
        if (state == HealthState.Sick)
        {
            return $"{villager.Name} is ill — run down after {years} of nothing but {food}. A few days in bed, and up again.";
        }

        return state == HealthState.PlainDiet
            ? $"{villager.Name} has eaten nothing but {food} for {years}. A body wants more than one kind of food."
            : $"{villager.Name} has eaten nothing but {food} for {years}, and it's telling. {Capitalised(OtherFoods(villager.PlainOn))} would set it right.";
    }

    /// <summary>How many of the living are run down — the village panel's count (derived, never hashed: D335).</summary>
    public int RunDownCount()
    {
        int n = 0;
        foreach (Villager v in Villagers)
        {
            n += v.Alive && HealthOf(v) == HealthState.RunDown ? 1 : 0;
        }

        return n;
    }

    /// <summary>
    /// ⭐ The first warning (§5.4): <i>"Nine people in Fernhollow have eaten nothing but forage for a year. In two more they will
    /// start to wear down — a fishery, a hunter's lodge or a farm would give them something else."</i> Said on the turn the
    /// first of them reaches the plain diet, and not again until the village has been well.
    /// </summary>
    internal void SayThePlainDiet(int plain, IReadOnlyList<Villager> newly)
    {
        FoodGroup food = CommonestPlainFood(newly);
        string who = plain == 1 ? $"{newly[0].FullName} has" : $"{CountWord(plain)} people in {Name} have";
        int more = (Config.HealthRunDownSeasons - Config.HealthPlainDietSeasons + 3) / 4;
        string after = more == 1 ? "In another year" : $"In {CountWord(more)} more years";
        RaiseMoment(
            "A plain diet",
            $"{who} eaten nothing but {FoodGroups.Word(food)} for a year. {after} they will start to wear down — "
                + $"{Answers(food)} would give them something else.",
            stops: false,
            LogCategory.Warning,
            new[] { MomentMark.PlainDiet });
    }

    /// <summary>
    /// ⭐ The second (§5.4): <i>"The first of Fernhollow are run down: Ada and Tom have eaten nothing but forage for three years,
    /// and it's telling. A fishery, a hunter's lodge or a farm would give them something else."</i>
    /// </summary>
    internal void SayTheFirstRunDown(int runDown, IReadOnlyList<Villager> newly)
    {
        FoodGroup food = CommonestPlainFood(newly);
        string who = runDown == 1 ? $"{newly[0].FullName} has" : $"{CountWord(runDown)} people in {Name} have";
        RaiseMoment(
            "Run down",
            $"{who} eaten nothing but {FoodGroups.Word(food)} for {YearsWord(Config.HealthRunDownSeasons)}, and it's telling. "
                + $"{Capitalised(Answers(food))} would give them something else.",
            stops: false,
            LogCategory.Warning,
            new[] { MomentMark.RunDown });
    }

    /// <summary>The log's line when a run-down person falls ill (§5.5): <i>"Ada Fern has taken to bed — run down after three years of nothing but forage."</i></summary>
    internal void SayTheyFellIll(Villager villager) =>
        Narrate($"{villager.FullName} has taken to bed — run down after {YearsWord(villager.PlainSeasons)} of nothing but "
            + $"{FoodGroups.Word(villager.PlainOn)}. {Clock.SeasonAndYear()}.");

    private static FoodGroup CommonestPlainFood(IReadOnlyList<Villager> newly)
    {
        var counts = new int[FoodGroups.Count];
        foreach (Villager v in newly)
        {
            if (v.PlainOn != FoodGroup.None)
            {
                counts[FoodGroups.Slot(v.PlainOn)]++;
            }
        }

        int best = 0;
        for (int g = 1; g < counts.Length; g++)
        {
            best = counts[g] > counts[best] ? g : best;
        }

        return FoodGroups.All[best];
    }

    /// <summary>The other groups a village can raise today, as food — <i>"fish, meat or grain"</i>. Poultry is not offered until something is poultry.</summary>
    private string OtherFoods(FoodGroup plainOn) =>
        Listed(RaisedGroups().Where(g => g != plainOn).Select(FoodGroups.Word).ToList());

    /// <summary>The buildings that answer a diet on <paramref name="plainOn"/> — <i>"a fishery, a hunter's lodge or a farm"</i>.</summary>
    private string Answers(FoodGroup plainOn) =>
        Listed(RaisedGroups().Where(g => g != plainOn).Select(g => g switch
        {
            FoodGroup.Forage => "a forager's hut",
            FoodGroup.Fish => "a fishery",
            FoodGroup.Meat => "a hunter's lodge",
            FoodGroup.Grain => "a farm",
            _ => FoodGroups.Word(g),
        }).ToList());

    /// <summary>The groups some edible good of this game sits in, in the groups' order.</summary>
    private IEnumerable<FoodGroup> RaisedGroups() =>
        FoodGroups.All.Where(g => GoodsCatalog.EdibleGoods.Any(e => GoodsCatalog.FoodGroupOf(e) == g));

    private static string Listed(IReadOnlyList<string> items) => items.Count switch
    {
        0 => "something else",
        1 => items[0],
        _ => string.Join(", ", items.Take(items.Count - 1)) + " or " + items[^1],
    };

    private static string YearsWord(int seasons)
    {
        int years = Math.Max(1, seasons / 4);
        return years == 1 ? "a year" : $"{CountWord(years)} years";
    }
}
