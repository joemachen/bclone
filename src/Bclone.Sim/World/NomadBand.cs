using System.Text.Json.Nodes;
using Bclone.Sim.Config;
using Bclone.Sim.Persistence;

namespace Bclone.Sim.World;

/// <summary>
/// One nomad in a band that has knocked — a person the village has not taken in yet
/// (`specs/nomads.md §4.2`, D564).
/// </summary>
/// <remarks>
/// <b>Everything about them is a hash of the seed and their place in the band</b>, never an <c>Rng</c>
/// draw, so a knock moves no other draw in the village (§4.1). Their name is fixed at the knock, like a
/// founder's, and is the one thing here that depends on the roster: it skips the names the living held
/// that spring. ⚠️ Not hashed — a name is a function of the seed and the history, as a villager's is.
/// </remarks>
public sealed class NomadPerson
{
    public required string Name { get; init; }

    /// <summary>Years old at the knock — 18–40 for an adult, 2–11 for a child.</summary>
    public required int AgeYears { get; init; }

    /// <summary>Whether they are grown. A band's family is two adults and its children.</summary>
    public required bool Adult { get; init; }

    public required int LifespanYears { get; init; }

    /// <summary>Their personal rhythm (§3.5), picked by hash.</summary>
    public required int Rhythm { get; init; }

    /// <summary>The skill row they are a journeyman of, or 0 for a novice. One journeyman a band.</summary>
    public required int TradeSkillId { get; init; }

    internal JsonObject ToSave() => new()
    {
        ["name"] = Name,
        ["age_years"] = AgeYears,
        ["adult"] = Adult,
        ["lifespan_years"] = LifespanYears,
        ["rhythm"] = Rhythm,
        ["trade"] = TradeSkillId,
    };

    internal static NomadPerson FromSave(SaveReader save) => new()
    {
        Name = save.String("name"),
        AgeYears = save.Int("age_years"),
        Adult = save.Bool("adult"),
        LifespanYears = save.Int("lifespan_years"),
        Rhythm = save.Int("rhythm"),
        TradeSkillId = save.Int("trade"),
    };
}

/// <summary>A family in a band — a surname nobody living carries, and its people.</summary>
public sealed class NomadFamily
{
    public required string Surname { get; init; }

    public List<NomadPerson> People { get; } = new();

    public int Adults
    {
        get
        {
            int adults = 0;
            for (int i = 0; i < People.Count; i++)
            {
                if (People[i].Adult)
                {
                    adults++;
                }
            }

            return adults;
        }
    }

    internal JsonObject ToSave()
    {
        var people = new JsonArray();
        foreach (NomadPerson person in People)
        {
            people.Add(person.ToSave());
        }

        return new JsonObject { ["surname"] = Surname, ["people"] = people };
    }

    internal static NomadFamily FromSave(SaveReader save)
    {
        var family = new NomadFamily { Surname = save.String("surname") };
        foreach (SaveReader person in save.Objects("people"))
        {
            family.People.Add(NomadPerson.FromSave(person));
        }

        return family;
    }
}

/// <summary>
/// A band of nomads camped by the cart or at the founding site, asking to settle — <b>not villagers yet</b>
/// (`specs/nomads.md §4.3`). The village has until the autumn turn to answer; the player answers on
/// the band's card, never in the moment that announced them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Stored, not derived, and the reason is the names.</b> The plan said a band could be re-derived from
/// (seed, count, population); its people's names are picked against the roster <em>at the knock</em> and a
/// birth during the wait would change them, so the card and the settling could disagree about who was
/// standing there. A band is a few dozen bytes, hashed as sim state (its ages, lifespans, rhythms and the
/// trade decide what the village becomes if it says yes); its names are not (D465's rule for every name).
/// </para>
/// <para>
/// ⛔ <b>Nothing here draws on <c>Rng</c>.</b> A village that sends every band away keeps the stream it had,
/// and a village that never reaches a knock hashes byte for byte as it did before bands existed.
/// </para>
/// </remarks>
public sealed class NomadBand
{
    /// <summary>The spring tick they knocked on. The autumn turn after it is the deadline.</summary>
    public required ulong KnockedAtTick { get; init; }

    /// <summary>The living the band met — what its size was derived from; kept so the card can say so.</summary>
    public required int MetAPopulationOf { get; init; }

    public List<NomadFamily> Families { get; } = new();

    public int People
    {
        get
        {
            int people = 0;
            for (int i = 0; i < Families.Count; i++)
            {
                people += Families[i].People.Count;
            }

            return people;
        }
    }

    public int Adults
    {
        get
        {
            int adults = 0;
            for (int i = 0; i < Families.Count; i++)
            {
                adults += Families[i].Adults;
            }

            return adults;
        }
    }

    public int Children => People - Adults;

    /// <summary><i>"the Hales"</i>, <i>"the Hales and the Dunns"</i>.</summary>
    public string FamilyNames
    {
        get
        {
            if (Families.Count == 0)
            {
                return "nobody";
            }

            if (Families.Count == 1)
            {
                return $"the {Families[0].Surname}s";
            }

            var names = new List<string>(Families.Count);
            for (int i = 0; i < Families.Count; i++)
            {
                names.Add($"the {Families[i].Surname}s");
            }

            return $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}";
        }
    }

    /// <summary>The food they carry between them — a few days' worth a head (`nomads_food_each`).</summary>
    public int FoodCarried(SimConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return People * config.NomadsFoodEach;
    }

    /// <summary>
    /// How many people a band is for a village of <paramref name="population"/> — a share, clamped
    /// (`nomads.md §4.2`), and two more with a town hall standing (call 1: the hall widens them).
    /// </summary>
    public static int PeopleFor(SimConfig config, int population, bool townHallStands)
    {
        ArgumentNullException.ThrowIfNull(config);
        int people = Math.Clamp(population / Math.Max(1, config.NomadsShareDivisor), config.NomadsBandMin, config.NomadsBandMax);
        return townHallStands ? people + 2 : people;
    }

    /// <summary>One family up to four people, two above it — and a family more with the hall.</summary>
    public static int FamiliesFor(int people, bool townHallStands) => (people <= 4 ? 1 : 2) + (townHallStands ? 1 : 0);

    internal JsonObject ToSave()
    {
        var families = new JsonArray();
        foreach (NomadFamily family in Families)
        {
            families.Add(family.ToSave());
        }

        return new JsonObject
        {
            ["knocked_at"] = SaveWriter.Hex(KnockedAtTick),
            ["met"] = MetAPopulationOf,
            ["families"] = families,
        };
    }

    internal static NomadBand FromSave(SaveReader save)
    {
        var band = new NomadBand { KnockedAtTick = save.ULong("knocked_at"), MetAPopulationOf = save.Int("met") };
        foreach (SaveReader family in save.Objects("families"))
        {
            band.Families.Add(NomadFamily.FromSave(family));
        }

        return band;
    }
}
