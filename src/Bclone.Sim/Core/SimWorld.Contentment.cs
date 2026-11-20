using Bclone.Sim.World;

namespace Bclone.Sim.Core;

/// <summary>
/// ⭐ THE PERSON CARD'S CONTENTMENT SENTENCE (W2, `morale.md §6` and §13 step 3): what the last season's read found, in words —
/// never the number. The pill beside <i>Fed · Warm · Housed</i> reads <i>Content</i> or <i>Unsettled</i>, and this is its
/// tooltip; one copy for the card and the probe, as <see cref="WaterNoteFor"/> is.
/// </summary>
public sealed partial class SimWorld
{
    /// <summary>
    /// Null for a child (contentment is an adult's, D798) and for an adult the last read found nothing troubling. Otherwise
    /// <i>"Otto is unsettled — the walk to work is a long one and last winter was a cold one."</i> past the line, or
    /// <i>"Otto is content, though the walk to work is a long one."</i> short of it. The causes in the data file's order.
    /// </summary>
    public string? ContentmentNoteFor(Villager villager)
    {
        ArgumentNullException.ThrowIfNull(villager);
        if (!villager.CanWork || villager.ContentmentCauses == 0)
        {
            return null;
        }

        var said = new List<string>();
        foreach (ContentmentCauseRow row in Config.ContentmentCauses)
        {
            if ((villager.ContentmentCauses & (1 << (int)row.Cause)) == 0)
            {
                continue;
            }

            said.Add(row.Cause switch
            {
                ContentmentCause.NoRoof => "they have no roof of their own",
                ContentmentCause.FarWalk => "the walk to work is a long one",
                ContentmentCause.ColdWinter => "last winter was a cold one",
                ContentmentCause.FireOut => "the fire went out at home last winter",
                ContentmentCause.DryJar => "the water jar ran dry last season",
                ContentmentCause.DeathInTheFamily => WhoTheyLost(villager, row) is Villager lost
                    ? $"they are grieving {lost.Name}"
                    : "there has been a death in the family",
                _ => $"{row.Cause}",
            });
        }

        if (said.Count == 0)
        {
            return null;
        }

        string listed = said.Count == 1 ? said[0] : string.Join(", ", said.Take(said.Count - 1)) + " and " + said[^1];
        return villager.IsUnsettled
            ? $"{villager.Name} is unsettled — {listed}."
            : $"{villager.Name} is content, though {listed}.";
    }

    /// <summary>
    /// The latest of <paramref name="villager"/>'s dead — their household's, or their partner — since the row's
    /// <c>held_seasons</c> before this season's turn, the window the read counted in. Ties go to the first in the list.
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
