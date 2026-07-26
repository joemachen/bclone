using Bclone.Sim.Config;
using Bclone.Sim.Core;
using Bclone.Sim.World;

namespace Bclone.Sim.Systems;

/// <summary>
/// Step 3 of the tick order: households grow.
/// </summary>
/// <remarks>
/// <para>
/// Birth is deliberately <b>conditional on a food surplus</b>. A village that breeds
/// into a famine is not telling a story, it is oscillating — and the resulting deaths
/// would not be traceable to any decision the player made, which fails the legibility
/// non-negotiable. Requiring a full-ish larder first makes population growth a
/// <em>consequence of a good decade</em>, which is exactly the kind of thing the
/// player can read off the log afterwards.
/// </para>
/// <para>
/// Determinism: households are visited in id order and the RNG is drawn from
/// <b>only when a birth actually happens</b>. A draw on every check would make the
/// stream depend on how many households merely considered it, which is the sort of
/// thing that changes when you add an unrelated feature.
/// </para>
/// </remarks>
public sealed class HouseholdSystem : ISimSystem
{
    public string Name => "household";

    public void Execute(SimWorld world)
    {
        SimConfig config = world.Config;

        // Births only resolve on a year boundary — a household does not reconsider
        // four times a day, and it keeps the log to one line per event.
        if (world.Tick == 0UL || world.Tick % (ulong)config.TicksPerYear != 0UL)
        {
            return;
        }

        int year = world.Clock.Year;

        // Snapshot the count: newborns are appended, and a baby must not be
        // considered for parenthood on the tick it is born.
        int householdCount = world.Households.Count;

        for (int i = 0; i < householdCount; i++)
        {
            TryBirth(world, world.Households[i], config, year);
        }
    }

    private static void TryBirth(SimWorld world, Household household, SimConfig config, int year)
    {
        if (!IsReadyForAChild(world, household, config, year))
        {
            return;
        }

        // Draw order is part of the seed contract: name, then lifespan. Same order
        // as founding, so there is one rule rather than two.
        string name = config.VillagerNames[(int)world.Rng.NextUInt((uint)config.VillagerNames.Count)];

        int lifespan = config.LifespanYearsBase;
        if (config.LifespanYearsVariance > 0)
        {
            lifespan += world.Rng.NextInt(-config.LifespanYearsVariance, config.LifespanYearsVariance + 1);
        }

        var child = new Villager
        {
            Id = NextVillagerId(world),
            Name = name,
            LifespanYears = lifespan,
            BirthYear = year,
            AgeYears = 0,
            LifeStage = LifeStage.Child,
            HouseholdId = household.Id,
            Position = household.HomePosition,
        };

        household.AddMember(child.Id);
        household.LastBirthYear = year;
        world.Villagers.Add(child);

        world.Narrate(
            $"{name} was born to the {household.Name} household — {world.Clock.SeasonAndYear()}. " +
            $"The village is now {world.Population}.");
    }

    /// <summary>
    /// Whether a household will have a child this year: two fertile adults, room in
    /// the house, food in the store, and enough time since the last birth.
    /// </summary>
    /// <remarks>
    /// A ranked list of plain conditions rather than a fertility score, for the same
    /// reason the behaviour system is an if-chain: the player has to be able to be
    /// told why, in one sentence.
    /// </remarks>
    public static bool IsReadyForAChild(SimWorld world, Household household, SimConfig config, int year)
    {
        if (household.MemberIds.Count >= config.MaxHouseholdSize)
        {
            return false;
        }

        if (household.LastBirthYear != 0 && year - household.LastBirthYear < config.BirthIntervalYears)
        {
            return false;
        }

        if (household.Stockpile.Food < config.BirthFoodThreshold)
        {
            return false;
        }

        return CountFertileAdults(world, household, config) >= 2;
    }

    private static int CountFertileAdults(SimWorld world, Household household, SimConfig config)
    {
        int fertile = 0;

        for (int i = 0; i < household.MemberIds.Count; i++)
        {
            Villager? member = FindVillager(world, household.MemberIds[i]);
            if (member is null || !member.Alive)
            {
                continue;
            }

            if (member.AgeYears >= config.FertilityMinAge && member.AgeYears <= config.FertilityMaxAge)
            {
                fertile++;
            }
        }

        return fertile;
    }

    private static Villager? FindVillager(SimWorld world, int id)
    {
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            if (world.Villagers[i].Id == id)
            {
                return world.Villagers[i];
            }
        }

        return null;
    }

    /// <summary>Ids are never reused, so the dead keep theirs and the log stays honest.</summary>
    private static int NextVillagerId(SimWorld world)
    {
        int max = 0;
        for (int i = 0; i < world.Villagers.Count; i++)
        {
            if (world.Villagers[i].Id > max)
            {
                max = world.Villagers[i].Id;
            }
        }

        return max + 1;
    }
}
