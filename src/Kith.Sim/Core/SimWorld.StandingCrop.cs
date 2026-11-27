using Kith.Sim.World;

namespace Kith.Sim.Core;

/// <summary>
/// ⭐ Joe's crop that stands into winter (W5, `skills-for-every-trade.md §5`) — the day the frost takes what stands, and
/// whether a farmer may reap today.
/// </summary>
public sealed partial class SimWorld
{
    /// <summary>
    /// The last day of winter a crop still standing at winter's turn may be reaped, or 0 when nothing stands into this winter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Set once, at the turn</b> (<see cref="Systems.CropSystem"/>), from what the village knew then — <i>"the frost will
    /// take them in five days"</i> is a promise, so a technique lost in the week does not take the crop early. Cleared when
    /// the frost takes the rest, at the start of the day after it.
    /// </para>
    /// <para>State, not a cache: the sim reads it to decide who may reap. Saved and hashed, sparse — 0 mixes nothing.</para>
    /// </remarks>
    public int CropStandsUntilDay { get; internal set; }

    /// <summary>
    /// How many days into winter a standing crop keeps while the village knows what it knows now — the most any known
    /// technique gives (<see cref="TechniqueRow.StandsIntoWinterDays"/>), 0 with none.
    /// </summary>
    /// <remarks>Asked once a year, at winter's turn: a short walk of the catalogue, never a scan of the map.</remarks>
    public int CropStandsIntoWinterDays()
    {
        int days = 0;
        for (int id = 0; id < TechniquesCatalog.Count; id++)
        {
            if (TechniquesCatalog[id] is { StandsIntoWinterDays: > 0 } row
                && KnowledgeStates[id] != KnowledgeState.Unknown
                && row.StandsIntoWinterDays > days)
            {
                days = row.StandsIntoWinterDays;
            }
        }

        return days;
    }

    /// <summary>
    /// Whether a ripe crop may be reaped today: autumn, as always — and winter's first days, up to
    /// <see cref="CropStandsUntilDay"/>, once the village knows how to keep a standing crop.
    /// </summary>
    /// <remarks>
    /// ⭐ The one question every reaping site asks (it was <c>SeasonRules.IsReaping</c> at nine): the farm already keeps its
    /// seats while a crop stands in any season (<see cref="FarmerSeatsWithGroundToWork"/>), so without this a farmer would
    /// stand in a ripe winter field with nothing he was allowed to do.
    /// </remarks>
    public bool MayReapToday()
    {
        SimClock clock = Clock;
        return SeasonRules.IsReaping(clock.Season)
            || (CropStandsUntilDay > 0 && clock.Season == Season.Winter && clock.DayOfSeason <= CropStandsUntilDay);
    }
}
