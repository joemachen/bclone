using Bclone.Sim.Logging;
using Bclone.Sim.Systems;
using Bclone.Sim.World;

namespace Bclone.Sim.Core;

/// <summary>
/// ⭐ What the school says (D765, `school-and-education.md §12` step 4, §6): the sim's sentences, never the view's — the
/// school's card (<i>"14 of 20 places taken"</i>), a villager's schooling and what it gave her, and the village asking for
/// another school by name when the places run out.
/// </summary>
public sealed partial class SimWorld
{
    /// <summary>
    /// Children of school age left without a place at the last labour pass, while a school stands (D765). Derived — set by
    /// the school roll, never hashed or saved: after a load it is nought until the next pass, at most a labour pass away.
    /// </summary>
    public int ChildrenWaitingForSchool { get; internal set; }

    /// <summary>
    /// Whether the village has already asked for another school this time (D765) — the residential land's edge-detector one
    /// building over (D42): said once when children are first left without a place, again only after every child has one.
    /// Saved, not hashed — bookkeeping about narration.
    /// </summary>
    public bool SchoolsAreFull { get; internal set; }

    /// <summary>A school's places — its seated teachers times <c>school_places_per_teacher</c> (the card's number).</summary>
    public int SchoolPlacesAt(Workplace school) => SchoolRoll.PlacesAt(this, school);

    /// <summary>The pupils holding a place at a school (the card's number).</summary>
    public int SchoolPupilsAt(Workplace school)
    {
        ArgumentNullException.ThrowIfNull(school);
        return SchoolRoll.PupilsAt(this, school.Id).Count;
    }

    /// <summary>
    /// The school card's sentence (D765): the places taken, who waits, and — in winter — the stove (D764). <i>"14 of 20
    /// places taken — 3 children of school age wait for a place. The stove is lit."</i>
    /// </summary>
    public string SayTheSchool(Workplace school)
    {
        ArgumentNullException.ThrowIfNull(school);
        int places = SchoolRoll.PlacesAt(this, school);
        if (places == 0)
        {
            return "Nobody teaches here — ask for a teacher on the professions panel, and each one brings "
                + $"{Config.SchoolPlacesPerTeacher} places.";
        }

        int pupils = SchoolRoll.PupilsAt(this, school.Id).Count;
        string said = $"{pupils} of {places} places taken";
        said += ChildrenWaitingForSchool switch
        {
            0 => ".",
            1 => " — a child of school age waits for a place.",
            int n => $" — {n} children of school age wait for a place.",
        };

        if (Clock.IsWinter && Config.SchoolWinterFirewood > 0)
        {
            said += ShelterAt(school.Tile) == Shelter.Fire
                ? " The stove is lit."
                : " The stove is cold — the homes need the village's firewood first.";
        }

        return said;
    }

    /// <summary>
    /// A villager's schooling, in a sentence (D765, §6: <i>"Wren spent four years at the school"</i>) — and what it gave her,
    /// each benefit named only while its size is on (D761, D753): <i>"Spent two years at the school — carries a fuller load,
    /// will age more gently, and goes in from the cold by the length of the walk to a fire."</i> ⛔ Never a number: fuller
    /// loads would be an invisible multiplier without this sentence (D207). Null for anyone who never went.
    /// </summary>
    public string? SayTheSchooling(Villager villager)
    {
        ArgumentNullException.ThrowIfNull(villager);
        int year = Config.TicksPerYear;
        int term = Config.SchoolTermTicks;
        if (villager.IsPupil)
        {
            string where = FindWorkplace(villager.SchoolId)?.Name ?? "the school";
            int of = Math.Max(1, term / year);
            int now = Math.Min(of, (villager.SchoolTicks / year) + 1);
            return $"At {where} — in the {Ordinal(now)} of {Years(of)}.";
        }

        if (villager.SchoolTicks <= 0)
        {
            return null;
        }

        int served = Math.Max(1, villager.SchoolTicks / year);
        bool whole = term > 0 && villager.SchoolTicks >= term;
        string said = whole ? $"Spent {Years(served)} at the school" : $"Spent {Years(served)} at the school before the place went";

        var gave = new List<string>();
        if (SchoolGives.ByTheTermServed(Config, villager, Config.SchoolCarryPercent) > 0)
        {
            gave.Add("carries a fuller load");
        }

        if (SchoolGives.ByTheTermServed(Config, villager, Config.SchoolOldAgePercent) > 0)
        {
            gave.Add("will age more gently");
        }

        if (BehaviorSystem.ReadsTheYear(Config, villager))
        {
            gave.Add("goes in from the cold by the length of the walk to a fire");
        }

        return gave.Count switch
        {
            0 => said + ".",
            1 => $"{said} — {gave[0]}.",
            2 => $"{said} — {gave[0]} and {gave[1]}.",
            _ => $"{said} — {string.Join(", ", gave.Take(gave.Count - 1))}, and {gave[^1]}.",
        };

        static string Years(int n) => n == 1 ? "a year" : n == 2 ? "two years" : n == 3 ? "three years" : n == 4 ? "four years" : $"{n} years";

        static string Ordinal(int n) => n switch
        {
            1 => "first",
            2 => "second",
            3 => "third",
            4 => "fourth",
            _ => $"{n}th",
        };
    }

    /// <summary>
    /// ⭐ The village asks for another school by name (D765, §6 — D42's shape): once when children of school age are first
    /// left without a place, naming the cheaper answer while there is one — another teacher where a school has an empty
    /// seat — and the school itself when every one is full. Run by the school roll after it fills the places.
    /// </summary>
    internal void AskForAnotherSchoolIfFull(int waiting)
    {
        ChildrenWaitingForSchool = waiting;
        if (waiting == 0)
        {
            SchoolsAreFull = false;
            return;
        }

        if (SchoolsAreFull)
        {
            return;
        }

        SchoolsAreFull = true;
        Workplace? seat = null;
        for (int i = 0; i < Workplaces.Count && seat is null; i++)
        {
            Workplace school = Workplaces[i];
            if (school.Kind == JobKind.Teacher && !school.IsSite && school.WorkerIds.Count < school.Places)
            {
                seat = school;
            }
        }

        string children = waiting == 1 ? "A child of school age has" : $"{waiting} children of school age have";
        Narrate(
            seat is not null
                ? $"{children} no place at school — another teacher at {seat.Name} would bring {Config.SchoolPlacesPerTeacher} more, or build another school. {Clock.SeasonAndYear()}."
                : $"{children} no place at school — every school is full. Build another. {Clock.SeasonAndYear()}.",
            LogCategory.Warning);
    }
}
