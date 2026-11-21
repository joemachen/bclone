using Kith.Sim.Logging;
using Kith.Sim.Systems;
using Kith.Sim.World;

namespace Kith.Sim.Core;

/// <summary>
/// ⭐ THE PERSON CARD'S CONTENTMENT SENTENCE (W2, `morale.md §6` and §13 step 3): what the last season's read found, in words —
/// never the number. The pill beside <i>Fed · Warm · Housed</i> reads <i>Content</i> or <i>Unsettled</i>, and this is its
/// tooltip; one copy for the card and the probe, as <see cref="WaterNoteFor"/> is. ⭐ And the departure (step 5, §13.2): the
/// warning, the leaving and its one line, in the same phrases.
/// </summary>
public sealed partial class SimWorld
{
    /// <summary>
    /// Null for a child (contentment is an adult's, D798) and for an adult the last read found nothing troubling. Otherwise
    /// <i>"Otto is unsettled — the walk to work is a long one and last winter was a cold one."</i> past the line, or
    /// <i>"Otto is content, though the walk to work is a long one."</i> short of it. From a year unsettled, <i>"Otto is
    /// thinking of leaving — …"</i>; ready to go and held by a partner who is not, <i>"Otto would leave, but Mira is staying
    /// — …"</i> (step 5). The causes in the data file's order.
    /// </summary>
    public string? ContentmentNoteFor(Villager villager)
    {
        ArgumentNullException.ThrowIfNull(villager);
        if (!villager.CanWork || villager.ContentmentCauses == 0 || CausesSaid(new[] { villager }) is not string listed)
        {
            return null;
        }

        int leaving = Config.ContentmentLeavingSeasons;
        if (leaving > 0 && villager.SeasonsUnsettled >= leaving && FindVillager(villager.PartnerId) is { Alive: true } partner)
        {
            return $"{villager.Name} would leave, but {partner.Name} is staying — {listed}.";
        }

        if (leaving > 0 && villager.SeasonsUnsettled >= Config.ContentmentThinkingOfLeavingSeasons)
        {
            return $"{villager.Name} is thinking of leaving — {listed}.";
        }

        return villager.IsUnsettled
            ? $"{villager.Name} is unsettled — {listed}."
            : $"{villager.Name} is content, though {listed}.";
    }

    /// <summary>
    /// ⭐ THE HOUSEHOLD CARD'S SENTENCE FOR THE LONGER WAIT (W2 step 4, `morale.md §10` call 4: <i>"The Ashfords are not
    /// ready for another child — Otto is unsettled"</i>): said only while it is contentment that holds the next child off —
    /// a child already, the ordinary interval past, the longer one not, and a roof (without one the roof is what holds a child
    /// off, D71). Null otherwise. The card's title names the family.
    /// </summary>
    public string? ChildNoteFor(Household household)
    {
        ArgumentNullException.ThrowIfNull(household);
        if (household.LastBirthYear == 0 || !household.HasHome
            || HouseholdSystem.UnsettledParentOf(this, household, Config) is not Villager unsettled)
        {
            return null;
        }

        int since = Clock.Year - household.LastBirthYear;
        return since >= Config.BirthIntervalYears && since < HouseholdSystem.BirthIntervalFor(this, household, Config)
            ? $"Not ready for another child — {unsettled.Name} is unsettled."
            : null;
    }

    /// <summary>
    /// The warning, once a spell (step 5, §13.2 item 2): <i>"Otto Ashford is thinking of leaving the valley — …"</i>, a
    /// <c>Warning</c> — something the player can still act on.
    /// </summary>
    internal void SayTheyAreThinkingOfLeaving(Villager villager)
    {
        if (CausesSaid(new[] { villager }) is string listed)
        {
            Narrate($"{villager.FullName} is thinking of leaving the valley — {listed}. {Clock.SeasonAndYear()}.", LogCategory.Warning);
        }
    }

    /// <summary>
    /// ⭐ THEY LEAVE (step 5, §13.2 items 4–6): a fate beside death — not alive, not dead — so everything that asks
    /// <i>"alive?"</i> lets them go as it lets the dead go; one line for the party, naming them and why; and, if that was the
    /// last of the village, the unstopping moment a last death raises.
    /// </summary>
    internal void LetThemGo(IReadOnlyList<Villager> adults, IReadOnlyList<Villager> children)
    {
        string names = NamesOf(adults);
        string verb = adults.Count == 1 ? "has" : "have";
        string taking = children.Count switch
        {
            0 => string.Empty,
            1 => ", taking the child",
            _ => $", taking the {CountWord(children.Count)} children",
        };
        string why = CausesSaid(adults) is string listed ? $" — {listed}" : string.Empty;

        foreach (Villager person in adults.Concat(children))
        {
            person.Alive = false;
            person.State = VillagerState.Left;
            person.LeftAtTick = Tick;
            person.ActionTicksRemaining = 0;

            // A cart counts as home, as at a death (`MortalitySystem.Kill`): it is the household's, or the works'.
            person.PushingACart = false;
            person.CartForTheWorks = false;
        }

        Narrate($"{names} {verb} left the valley{taking}{why}. {Clock.SeasonAndYear()}.", LogCategory.Life);

        if (Population == 0)
        {
            RaiseMoment(
                $"{Name} is empty",
                $"The last of {Name} are gone — {names} {verb} left the valley {Clock}. "
                + "The valley keeps its buildings and its stores for now, and the woods will take them back if nobody comes.",
                stops: false,
                LogCategory.Death);
        }
    }

    /// <summary>
    /// The causes <paramref name="people"/> hold, in the data file's order, each phrase said once — <i>"a, b and c"</i>; null
    /// when none is held. One copy for the card, the warning and the departure.
    /// </summary>
    private string? CausesSaid(IReadOnlyList<Villager> people)
    {
        var said = new List<string>();
        foreach (ContentmentCauseRow row in Config.ContentmentCauses)
        {
            foreach (Villager person in people)
            {
                if ((person.ContentmentCauses & (1 << (int)row.Cause)) == 0)
                {
                    continue;
                }

                string phrase = row.Cause switch
                {
                    ContentmentCause.NoRoof => "they have no roof of their own",
                    ContentmentCause.FarWalk => "the walk to work is a long one",
                    ContentmentCause.ColdWinter => "last winter was a cold one",
                    ContentmentCause.FireOut => "the fire went out at home last winter",
                    ContentmentCause.DryJar => "the water jar ran dry last season",
                    ContentmentCause.DeathInTheFamily => WhoTheyLost(person, row) is Villager lost
                        ? $"they are grieving {lost.Name}"
                        : "there has been a death in the family",
                    _ => $"{row.Cause}",
                };

                if (!said.Contains(phrase))
                {
                    said.Add(phrase);
                }
            }
        }

        return said.Count switch
        {
            0 => null,
            1 => said[0],
            _ => string.Join(", ", said.Take(said.Count - 1)) + " and " + said[^1],
        };
    }

    /// <summary><i>"Otto Ashford"</i>; <i>"Otto and Mira Ashford"</i> when they share a name; else each in full.</summary>
    private static string NamesOf(IReadOnlyList<Villager> adults)
    {
        string surname = adults[0].Surname;
        bool shared = surname.Length > 0 && adults.All(a => a.Surname == surname);
        List<string> each = adults.Select(a => shared ? a.Name : a.FullName).ToList();
        string listed = each.Count == 1 ? each[0] : string.Join(", ", each.Take(each.Count - 1)) + " and " + each[^1];
        return shared ? $"{listed} {surname}" : listed;
    }

    private static string CountWord(int n) => n switch
    {
        2 => "two",
        3 => "three",
        4 => "four",
        5 => "five",
        6 => "six",
        _ => n.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    /// <summary>
    /// The latest of <paramref name="villager"/>'s dead — their household's, or their partner — since the row's
    /// <c>held_seasons</c> before this season's turn, the window the read counted in. Ties go to the first in the list.
    /// ⭐ Only the dead: somebody who left is not grieved (§13.2 item 4).
    /// </summary>
    private Villager? WhoTheyLost(Villager villager, ContentmentCauseRow row)
    {
        ulong seasonTicks = (ulong)Config.TicksPerDay * (ulong)Config.DaysPerSeason;
        ulong turn = (ulong)SeasonNumber * seasonTicks;
        ulong window = seasonTicks * (ulong)row.HeldSeasons;
        ulong since = turn > window ? turn - window : 0UL;
        Villager? lost = null;
        for (int i = 0; i < Villagers.Count; i++)
        {
            Villager dead = Villagers[i];
            if (!dead.Alive && dead.DiedAtTick is ulong at && at >= since
                && (dead.HouseholdId == villager.HouseholdId || dead.PartnerId == villager.Id)
                && (lost is null || at > lost.DiedAtTick))
            {
                lost = dead;
            }
        }

        return lost;
    }
}
