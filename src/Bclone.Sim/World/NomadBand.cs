using Bclone.Sim.Config;

namespace Bclone.Sim.World;

/// <summary>
/// One nomad in a band that has knocked — a person the village takes in the same tick
/// (`specs/nomads.md §4.2`, D564; D572).
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
}

/// <summary>
/// A band of nomads at the knock — who walks in, derived from the seed, the count of bands and the village as it
/// stands that tick (`specs/nomads.md §4.2`), and <b>settled the same tick</b> (D572; Joe: the switch is the permission).
/// </summary>
/// <remarks>
/// <para>
/// <b>A value, never state.</b> D565 stored it (a waiting band, a card, two buttons — the buttons sat in a fold and
/// Joe never saw them); since D572 it lives for the length of <c>SimWorld.Knock</c> and is gone into the roster by the
/// end of it. Nothing of it is hashed or saved: the villagers it became are.
/// </para>
/// <para>
/// ⛔ <b>Nothing here draws on <c>Rng</c>.</b> A village that never reaches a knock hashes byte for byte as it did
/// before bands existed.
/// </para>
/// </remarks>
public sealed class NomadBand
{
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
}
