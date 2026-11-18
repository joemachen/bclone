using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.Logging;
using Bclone.Sim.World;

namespace Bclone.Sim.Systems;

/// <summary>
/// ⭐ The pupils' term (D754, `school-and-education.md §12` step 1): a tick served for every tick a pupil holds a place, and
/// a pupil who reaches <c>school_leaving_age</c> or a full term leaves for work. Who takes a place is
/// <see cref="SchoolRoll"/>'s, on the labour passes' cadence. With no pupil anywhere this does nothing at all.
/// </summary>
public sealed class SchoolSystem : ISimSystem
{
    public string Name => "school";

    public void Execute(SimWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        SimConfig config = world.Config;
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            Villager pupil = world.Villagers[i];
            if (!pupil.IsPupil)
            {
                continue;
            }

            if (!pupil.Alive)
            {
                pupil.SchoolId = 0;
                continue;
            }

            pupil.SchoolTicks++;
            if (pupil.SchoolTicks >= config.SchoolTermTicks || pupil.AgeYears >= config.SchoolLeavingAge)
            {
                SchoolRoll.Leave(world, pupil, finished: true);
            }
        }
    }
}

/// <summary>
/// ⭐ What school gives a villager, by the term she served (D753, D761, `school-and-education.md §11.4`, §12.5): a full
/// term's size, never more, and a share of it for a share of the term (call 5's credit).
/// </summary>
internal static class SchoolGives
{
    /// <summary>A full term's <paramref name="size"/> scaled by the term served; nought for nought, or for no school.</summary>
    internal static int ByTheTermServed(SimConfig config, Villager villager, int size)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(villager);
        int term = config.SchoolTermTicks;
        return size <= 0 || term <= 0 || villager.SchoolTicks <= 0 ? 0 : size * Math.Min(villager.SchoolTicks, term) / term;
    }

    /// <summary>
    /// ⭐ Fuller loads (Joe, D761): <paramref name="load"/>, and <c>school_carry_percent</c> more of it by the term served —
    /// <i>"she packs a basket properly"</i>. The one reader for every place a villager's load is what she can carry; at
    /// nought it is <paramref name="load"/> exactly.
    /// </summary>
    internal static int Load(SimConfig config, Villager villager, int load) =>
        load + (load * ByTheTermServed(config, villager, config.SchoolCarryPercent) / 100);
}

/// <summary>
/// ⭐ Who takes a school's places (D754, `school-and-education.md §11.3` call 4): the village fills them by itself, never a
/// pick of a child (D15, D51). A school's places are its teachers at work times <c>school_places_per_teacher</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Run by the labour passes, after the teachers are seated</b> — so a place counts only a teacher already at the school.
/// A pupil keeps their place from pass to pass (a reshuffle does not take it): the term is a commitment, not a job.
/// </para>
/// <para>
/// <b>Who goes first, a total order:</b> the most years before <c>school_leaving_age</c> (the youngest — a place is worth
/// most at twelve), then the most term still to serve, then the shortest walk from home to the school (the allocator's
/// cost-first), then the lowest id. ⚠️ By age, not by term alone: every child who has never sat a day has the whole term
/// ahead, so term alone cannot tell a twelve-year-old from a fifteen (the guard caught exactly that). A villager the
/// player has kept on a trade is never taken (that pin is his), nor one who has served the full term.
/// </para>
/// <para>
/// <b>When places fall short</b> — a teacher gone, a school pulled down — those with the least served leave first, with
/// credit for every tick they served (call 5).
/// </para>
/// </remarks>
internal static class SchoolRoll
{
    internal static void Roll(SimWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        SimConfig config = world.Config;

        // 1. Too few places: a school gone, unstaffed or short — the least served leave first.
        for (int w = 0; w < world.Workplaces.Count; w++)
        {
            Workplace school = world.Workplaces[w];
            if (school.Kind != JobKind.Teacher)
            {
                continue;
            }

            int places = PlacesAt(world, school);
            List<Villager> pupils = PupilsAt(world, school.Id);
            if (pupils.Count <= places)
            {
                continue;
            }

            pupils.Sort((a, b) => a.SchoolTicks != b.SchoolTicks ? a.SchoolTicks.CompareTo(b.SchoolTicks) : b.Id.CompareTo(a.Id));
            for (int i = 0; i < pupils.Count - places; i++)
            {
                Leave(world, pupils[i], finished: false);
            }
        }

        for (int v = 0; v < world.Villagers.Count; v++)
        {
            Villager pupil = world.Villagers[v];
            if (pupil.IsPupil && world.FindWorkplace(pupil.SchoolId) is not { Kind: JobKind.Teacher })
            {
                Leave(world, pupil, finished: false);
            }
        }

        // 2. Free places: the youngest first, then the nearest, then the lowest id.
        var free = new List<(Workplace School, int Free)>();
        for (int w = 0; w < world.Workplaces.Count; w++)
        {
            Workplace school = world.Workplaces[w];
            if (school.Kind == JobKind.Teacher && !school.IsSite)
            {
                int open = PlacesAt(world, school) - PupilsAt(world, school.Id).Count;
                if (open > 0)
                {
                    free.Add((school, open));
                }
            }
        }

        var candidates = new List<(int YearsLeft, int TermLeft, int Cost, int VillagerId, int SchoolId)>();
        for (int v = 0; v < world.Villagers.Count; v++)
        {
            Villager child = world.Villagers[v];
            // ⭐ By age alone (D758): below adult_age a pupil is a child, who works at nothing anyway.
            if (!MayTakeAPlace(config, child))
            {
                continue;
            }

            GridPos home = world.HomePlaceOf(child);
            for (int s = 0; s < free.Count; s++)
            {
                int cost = world.TravelCost.Cost(home, free[s].School.Tile);
                if (cost != TravelCostField.Unreachable)
                {
                    candidates.Add((config.SchoolLeavingAge - child.AgeYears, config.SchoolTermTicks - child.SchoolTicks, cost,
                        child.Id, free[s].School.Id));
                }
            }
        }

        candidates.Sort((a, b) =>
            a.YearsLeft != b.YearsLeft ? b.YearsLeft.CompareTo(a.YearsLeft)
            : a.TermLeft != b.TermLeft ? b.TermLeft.CompareTo(a.TermLeft)
            : a.Cost != b.Cost ? a.Cost.CompareTo(b.Cost)
            : a.VillagerId != b.VillagerId ? a.VillagerId.CompareTo(b.VillagerId)
            : a.SchoolId.CompareTo(b.SchoolId));

        for (int c = 0; c < candidates.Count; c++)
        {
            (_, _, _, int villagerId, int schoolId) = candidates[c];
            Villager child = world.FindVillager(villagerId)!;
            int slot = free.FindIndex(f => f.School.Id == schoolId);
            if (child.IsPupil || slot < 0 || free[slot].Free <= 0)
            {
                continue;
            }

            Enrol(world, child, free[slot].School);
            free[slot] = (free[slot].School, free[slot].Free - 1);
        }

        // 3. ⭐ Who is still waiting, while a school is taught — the village asks for another by name (D765, §6).
        // ⛔ TAUGHT, NOT STANDING (D786, Joe's Ashbourne log, D773): counted from the day a school stood, the ask fired before
        // its teacher had started — "5 children of school age have no place at school — another teacher … would bring 6 more"
        // with six places and nobody in them. Untaught, the school's own card says "Nobody teaches here"; nobody can have a
        // place yet, so nobody is left without one.
        bool aSchoolIsTaught = false;
        for (int w = 0; w < world.Workplaces.Count && !aSchoolIsTaught; w++)
        {
            aSchoolIsTaught = world.Workplaces[w] is { Kind: JobKind.Teacher, IsSite: false } standing && PlacesAt(world, standing) > 0;
        }

        int waiting = 0;
        for (int v = 0; v < world.Villagers.Count && aSchoolIsTaught; v++)
        {
            Villager child = world.Villagers[v];
            if (MayTakeAPlace(config, child))
            {
                waiting++;
            }
        }

        world.AskForAnotherSchoolIfFull(waiting);
    }

    /// <summary>
    /// ⭐ How long a pupil was at school, as the leaving sentence says it (D786, Joe's Ashbourne log, D773: a child who turned
    /// twelve inside their first year <i>"leaves school 1 after 0 years"</i>): years once there is one, seasons under a year,
    /// and <i>"less than a season"</i> under that — never nought.
    /// </summary>
    internal static string HowLong(int ticks, SimConfig config)
    {
        int years = ticks / config.TicksPerYear;
        if (years > 0)
        {
            return years == 1 ? "a year" : $"{years} years";
        }

        int seasons = ticks / config.TicksPerSeason;
        return seasons switch
        {
            0 => "less than a season",
            1 => "a season",
            _ => $"{seasons} seasons",
        };
    }

    /// <summary>Who may take a place: of school age, within the term, not at school already, and not kept on a trade.</summary>
    private static bool MayTakeAPlace(SimConfig config, Villager child) =>
        child.Alive && !child.IsPupil && !child.IsPinned
        && child.AgeYears >= config.SchoolStartingAge && child.AgeYears < config.SchoolLeavingAge
        && child.SchoolTicks < config.SchoolTermTicks;

    /// <summary>A school's places: its teachers at work times <c>school_places_per_teacher</c>; none for a site.</summary>
    internal static int PlacesAt(SimWorld world, Workplace school) =>
        school.IsSite || school.Kind != JobKind.Teacher ? 0 : school.WorkerIds.Count * world.Config.SchoolPlacesPerTeacher;

    /// <summary>The pupils holding a place at a school, in id order.</summary>
    internal static List<Villager> PupilsAt(SimWorld world, int schoolId)
    {
        var pupils = new List<Villager>();
        for (int v = 0; v < world.Villagers.Count; v++)
        {
            if (world.Villagers[v].Alive && world.Villagers[v].SchoolId == schoolId)
            {
                pupils.Add(world.Villagers[v]);
            }
        }

        return pupils;
    }

    private static void Enrol(SimWorld world, Villager child, Workplace school)
    {
        if (child.HasJob && world.FindWorkplace(child.WorkplaceId) is Workplace job)
        {
            job.WorkerIds.Remove(child.Id);
            child.LastWorkplaceId = child.WorkplaceId;
            child.WorkplaceId = 0;
        }

        child.SchoolId = school.Id;
        child.JobReason = string.Empty;
        child.WorkNote = string.Empty;
        world.Narrate($"{child.Name}, {child.AgeYears}, starts at {school.Name}. {world.Clock.SeasonAndYear()}.", LogCategory.Life);
    }

    /// <summary>A pupil leaves — the term done, or the place gone — keeping every tick served.</summary>
    internal static void Leave(SimWorld world, Villager pupil, bool finished)
    {
        string where = world.FindWorkplace(pupil.SchoolId)?.Name ?? "the school";
        pupil.SchoolId = 0;
        if (!pupil.Alive)
        {
            return;
        }

        string served = HowLong(pupil.SchoolTicks, world.Config);
        world.Narrate(
            finished
                ? $"{pupil.Name} leaves {where} after {served}, for work. {world.Clock.SeasonAndYear()}."
                : $"{pupil.Name} leaves {where} after {served} — there is no place for them there now. {world.Clock.SeasonAndYear()}.",
            LogCategory.Life);
    }
}
