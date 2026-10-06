using System.Text.Json.Serialization;
using Bclone.Sim.Config;
using Bclone.Sim.Core;

namespace Bclone.Sim.World;

/// <summary>What a tech-tree node waits on (`tech-tree-map.md §3.1`).</summary>
/// <remarks>Read from data by name (<c>"StoneDug"</c>) through the loader's global enum converter.</remarks>
public enum TechCondition
{
    /// <summary>Stone dug by hand — <see cref="SimWorld.StoneEverDug"/> against <c>quarry_unlock_stone</c>.</summary>
    StoneDug,

    /// <summary>Iron dug by hand — <see cref="SimWorld.IronEverDug"/> against <c>smithy_unlock_iron</c>.</summary>
    IronDug,

    /// <summary>Years since the first granary was raised, against <c>literacy_years</c>.</summary>
    KeptGranaryYears,

    /// <summary>The founders dead — <see cref="SimWorld.SaidTheFoundersAreGone"/>.</summary>
    FoundersGone,

    /// <summary>The horizon: nobody in this valley knows how yet. In sight, never known.</summary>
    NotYet,

    /// <summary>Iron tools forged — <see cref="SimWorld.IronToolsEverForged"/> against <c>mine_unlock_iron_tools</c> (D449).</summary>
    IronToolsForged,

    /// <summary>Wheat reaped — <see cref="SimWorld.WheatEverReaped"/> against <c>mill_unlock_wheat</c> (D522).</summary>
    WheatReaped,

    /// <summary>Flour ground — <see cref="SimWorld.FlourEverGround"/> against <c>bakery_unlock_flour</c> (D522).</summary>
    FlourGround,
}

/// <summary>Where a node stands for this village (`tech-tree-map.md §3.2`).</summary>
public enum TechState
{
    /// <summary>Not yet seen — a silhouette, no name.</summary>
    Fogged,

    /// <summary>A node it hangs from is known, or its own progress is above nought.</summary>
    InSight,

    /// <summary>Its condition is met.</summary>
    Known,
}

/// <summary>One node of the tech-tree map — a row, so a modder adds one in data (D440).</summary>
public sealed record TechNodeRow
{
    /// <summary>A stable id the <see cref="Requires"/> of other rows name.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>The name as the player reads it.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>The building it puts in the menu, or none for a horizon node.</summary>
    [JsonPropertyName("unlocks")]
    public BuildingKind? Unlocks { get; init; }

    /// <summary>What it waits on.</summary>
    [JsonPropertyName("condition")]
    public TechCondition Condition { get; init; }

    /// <summary>The ids of the nodes it hangs from — the map's edges.</summary>
    [JsonPropertyName("requires")]
    public IReadOnlyList<string> Requires { get; init; } = Array.Empty<string>();

    /// <summary>What it takes, in the village's words.</summary>
    [JsonPropertyName("says")]
    public string Says { get; init; } = string.Empty;
}

/// <summary>
/// ⭐ The tech-tree map's reader (`tech-tree-map.md`, D440) — <b>pure, over state the sim already
/// keeps</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing here unlocks anything.</b> The gates are the sim's own (<see cref="SimWorld.WhyNotYet"/>,
/// <see cref="SimWorld.HasLiteracy"/>, <see cref="SimWorld.SaidTheFoundersAreGone"/>); the map reads
/// the same numbers, so it cannot say a building is known while <c>Mark</c> refuses it.
/// </para>
/// <para>
/// ⛔ Not hashed and not kept — derived on asking. The view asks when the panel opens and once a
/// season while it is open, never per frame.
/// </para>
/// </remarks>
public static class TechTree
{
    /// <summary>The built-in nodes: what exists (D440), and a fogged horizon from `tech-tree.md §9`.</summary>
    public static IReadOnlyList<TechNodeRow> DefaultNodes() => new[]
    {
        new TechNodeRow
        {
            Id = "quarry", Name = "The quarry", Unlocks = BuildingKind.Quarry, Condition = TechCondition.StoneDug,
            Says = "Dig stone out of a seam by hand, and somebody works out how to cut a quarry.",
        },
        new TechNodeRow
        {
            Id = "smithy", Name = "The smithy", Unlocks = BuildingKind.Smithy, Condition = TechCondition.IronDug,
            Says = "Dig iron out of a seam by hand, and the smith's craft comes to the village.",
        },
        new TechNodeRow
        {
            Id = "library", Name = "The library", Unlocks = BuildingKind.Library, Condition = TechCondition.KeptGranaryYears,
            Says = "Keep a granary's count for long enough, and the counting becomes writing.",
        },
        new TechNodeRow
        {
            Id = "town-hall", Name = "The town hall", Unlocks = BuildingKind.TownHall, Condition = TechCondition.FoundersGone,
            Says = "When the last of the founders is gone, the village raises a hall to remember them.",
        },
        new TechNodeRow
        {
            Id = "mason", Name = "The mason's yard", Condition = TechCondition.NotYet, Requires = new[] { "quarry" },
            Says = "Cut stone dressed into blocks — what every lasting building stands on.",
        },
        new TechNodeRow
        {
            Id = "cottage", Name = "The stone cottage", Condition = TechCondition.NotYet, Requires = new[] { "mason" },
            Says = "A house of dressed stone, that burns half the firewood of a wooden one.",
        },
        new TechNodeRow
        {
            Id = "mine", Name = "The iron mine", Unlocks = BuildingKind.Mine, Condition = TechCondition.IronToolsForged,
            Requires = new[] { "smithy" },
            Says = "Let the smith work iron, and the village learns good ore when it sees it — a mine "
                + "that never runs out as a seam does.",
        },
        new TechNodeRow
        {
            Id = "mill", Name = "The mill", Unlocks = BuildingKind.Mill, Condition = TechCondition.WheatReaped,
            Says = "Reap enough wheat, and somebody works out how to set millstones turning.",
        },
        new TechNodeRow
        {
            Id = "bakery", Name = "The bakery", Unlocks = BuildingKind.Bakery, Condition = TechCondition.FlourGround,
            Requires = new[] { "mill" },
            Says = "Grind the first flour, and somebody knows what an oven does with it — bread, "
                + "which keeps a villager full for longer than grain.",
        },
        new TechNodeRow
        {
            Id = "school", Name = "The school", Condition = TechCondition.NotYet, Requires = new[] { "library" },
            Says = "Where what the library keeps becomes what the young know.",
        },
    };

    /// <summary>A node's state, and its progress against what it takes (`tech-tree-map.md §3.2–§3.3`).</summary>
    public static (TechState State, int Progress, int Of) StateOf(SimWorld world, TechNodeRow node)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(node);

        (int progress, int of) = ProgressOf(world, node.Condition);
        bool known = node.Condition switch
        {
            TechCondition.NotYet => false,
            TechCondition.FoundersGone => world.SaidTheFoundersAreGone,
            TechCondition.KeptGranaryYears => world.HasLiteracy,
            _ => of > 0 && progress >= of,
        };

        if (known)
        {
            return (TechState.Known, progress, of);
        }

        bool aParentIsKnown = false;
        IReadOnlyList<TechNodeRow> all = world.Config.TechNodeRows;
        for (int i = 0; i < node.Requires.Count && !aParentIsKnown; i++)
        {
            for (int j = 0; j < all.Count; j++)
            {
                if (all[j].Id == node.Requires[i] && StateOf(world, all[j]).State == TechState.Known)
                {
                    aParentIsKnown = true;
                    break;
                }
            }
        }

        return (aParentIsKnown || progress > 0 ? TechState.InSight : TechState.Fogged, progress, of);
    }

    /// <summary>
    /// The gate's own number and its bar — read from the same state and the same config key the sim
    /// gates on, never a second copy (`tech-tree-map.md §3.3`).
    /// </summary>
    public static (int Progress, int Of) ProgressOf(SimWorld world, TechCondition condition)
    {
        ArgumentNullException.ThrowIfNull(world);

        switch (condition)
        {
            case TechCondition.StoneDug:
                return (world.StoneEverDug, world.Config.QuarryUnlockStone);
            case TechCondition.IronDug:
                return (world.IronEverDug, world.Config.SmithyUnlockIron);
            case TechCondition.IronToolsForged:
                return (world.IronToolsEverForged, world.Config.MineUnlockIronTools);
            case TechCondition.WheatReaped:
                return (world.WheatEverReaped, world.Config.MillUnlockWheat);
            case TechCondition.FlourGround:
                return (world.FlourEverGround, world.Config.BakeryUnlockFlour);
            case TechCondition.KeptGranaryYears:
                int years = world.FirstGranaryTick == 0 || world.Tick < world.FirstGranaryTick
                    ? 0
                    : (int)((world.Tick - world.FirstGranaryTick) / (ulong)world.Config.TicksPerYear);
                return (Math.Min(years, world.Config.LiteracyYears), world.Config.LiteracyYears);
            case TechCondition.FoundersGone:
                int founders = 0;
                int gone = 0;
                for (int i = 0; i < world.Villagers.Count; i++)
                {
                    if (world.Villagers[i].Founder)
                    {
                        founders++;
                        if (!world.Villagers[i].Alive)
                        {
                            gone++;
                        }
                    }
                }

                return (gone, founders);
            default:
                return (0, 0);
        }
    }

    /// <summary>Whether a condition is the village learning by DOING with its hands — the kind that introduces the map (Joe, D440).</summary>
    public static bool IntroducesTheMap(TechCondition condition) =>
        condition is TechCondition.StoneDug or TechCondition.IronDug or TechCondition.IronToolsForged
            or TechCondition.WheatReaped or TechCondition.FlourGround;

    /// <summary>How deep a node sits — the longest chain of <see cref="TechNodeRow.Requires"/> above it.</summary>
    public static int DepthOf(IReadOnlyList<TechNodeRow> nodes, TechNodeRow node)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(node);

        int deepest = 0;
        for (int i = 0; i < node.Requires.Count; i++)
        {
            for (int j = 0; j < nodes.Count; j++)
            {
                if (nodes[j].Id == node.Requires[i])
                {
                    deepest = Math.Max(deepest, 1 + DepthOf(nodes, nodes[j]));
                }
            }
        }

        return deepest;
    }

    /// <summary>Refuse a tree that cannot be drawn honestly: repeated ids, unknown parents, a cycle.</summary>
    public static void Validate(IReadOnlyList<TechNodeRow> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (TechNodeRow node in nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id) || !ids.Add(node.Id))
            {
                throw new SimConfigException($"tech_nodes repeats or blanks an id (\"{node.Id}\").");
            }
        }

        foreach (TechNodeRow node in nodes)
        {
            foreach (string parent in node.Requires)
            {
                if (!ids.Contains(parent))
                {
                    throw new SimConfigException($"tech node \"{node.Id}\" requires \"{parent}\", which is no node.");
                }
            }

            if (node.Unlocks is null != (node.Condition == TechCondition.NotYet))
            {
                throw new SimConfigException(
                    $"tech node \"{node.Id}\" must unlock a building unless it is on the horizon (not_yet), and only then.");
            }
        }

        foreach (TechNodeRow node in nodes)
        {
            Climb(node, new HashSet<string>(StringComparer.Ordinal));
        }

        void Climb(TechNodeRow node, HashSet<string> above)
        {
            if (!above.Add(node.Id))
            {
                throw new SimConfigException($"tech_nodes has a cycle through \"{node.Id}\".");
            }

            foreach (string parent in node.Requires)
            {
                foreach (TechNodeRow row in nodes)
                {
                    if (row.Id == parent)
                    {
                        Climb(row, new HashSet<string>(above, StringComparer.Ordinal));
                    }
                }
            }
        }
    }
}
