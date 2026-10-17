using System.Text.Json.Serialization;

namespace Bclone.Sim.World;

/// <summary>
/// Where a neighbour lies, and so how its traders come (D587, `specs/trade.md §4.2`).
/// </summary>
/// <remarks>⛔ Hashed and saved by number: appended, never renumbered.</remarks>
public enum NeighbourDirection
{
    /// <summary>Up the river: its boat comes down from the edge the river enters by.</summary>
    Upriver = 0,

    /// <summary>Down the river: its boat comes up from the edge the river leaves by.</summary>
    Downriver = 1,

    /// <summary>Overland: its caravan waits for carts (`trade.md §4.7`, tier 2).</summary>
    Overland = 2,
}

/// <summary>
/// A kind of animal a herd is made of — <b>a row, so a modder adds sheep without restructuring</b> (Joe, D588: one kind
/// first; `specs/livestock.md §0`).
/// </summary>
/// <remarks>
/// Commit 1 of `trade.md §11` (D590) states only what it is called. Tiles a head grazes, hay a winter day, the
/// breeding time and what a culled beast gives are measured in commit 2 before they are typed (`livestock.md §10.1`).
/// </remarks>
public sealed record AnimalRow
{
    /// <summary>Its id — the order the region and the herd count animals in.</summary>
    [JsonPropertyName("id")]
    public int Id { get; init; }

    /// <summary>What one is called, and what many are (<i>cattle</i> is both).</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>What the herd card calls more than one.</summary>
    [JsonPropertyName("plural")]
    public string Plural { get; init; } = string.Empty;

    // ⭐ THE HERD'S NUMBERS (D592, commit 2 of `trade.md §11`) — Joe's own, from `TECH-EXAMPLE.md §3`, and the hunt's.

    /// <summary>Pasture tiles one head grazes — Joe's <i>"Large Ruminants (Cow) — 5 Tiles"</i> (`TECH-EXAMPLE.md §3`).</summary>
    [JsonPropertyName("pasture_tiles_per_head")]
    public int PastureTilesPerHead { get; init; } = 5;

    /// <summary>Hay one head eats a winter day — Joe's <i>"Large animals (Cows) consume 2 Feed Units/day"</i>.</summary>
    [JsonPropertyName("hay_per_head_per_winter_day")]
    public int HayPerHeadPerWinterDay { get; init; } = 2;

    /// <summary>
    /// Grazing head-days to a new head: the herd's head count is added each grazing day, a beast for every this many.
    /// </summary>
    /// <remarks>
    /// Joe's cow (`TECH-EXAMPLE.md §3`): a 90-day gestation, one calf, 120 days to maturity — on a 120-day year about one
    /// calf a cow a year, and with half the herd cows, half a head a head a year. Grazing is 90 days of the 120, so 240
    /// head-days: a herd of eight grazing a year adds three. One kind, one row, no sexes (`livestock.md §0`).
    /// </remarks>
    [JsonPropertyName("head_days_per_birth")]
    public int HeadDaysPerBirth { get; init; } = 240;

    /// <summary>Meat off one culled head — a hunted animal's, the same beast (`meat_yield`).</summary>
    [JsonPropertyName("meat_per_head")]
    public int MeatPerHead { get; init; } = 900;

    /// <summary>The hide off one culled head — a hunted animal's (`leather_yield`).</summary>
    [JsonPropertyName("leather_per_head")]
    public int LeatherPerHead { get; init; } = 10;
}

/// <summary>
/// A neighbouring settlement off the map (D587, `specs/trade.md §4.2`): what it has plenty of and what it is short of.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Neighbours with reasons</b> (Joe, D587 call 7): a boat brings what its neighbour has plenty of, and the rates
/// follow what it is short of — so the region panel can say why.
/// </para>
/// <para>
/// ⭐ A size is half the shipped starting limit of its good (D594, `trade.md §8.4`) — what a hall-era village at its
/// limits has to sell with a <i>keep</i> at half. Asleep until a post stands; stepped at each season turn after.
/// Its name is <c>SimWorld.NeighbourName</c>: the town names after the village's own.
/// </para>
/// </remarks>
public sealed record NeighbourRow
{
    /// <summary>Its id — the order the world keeps neighbours' stocks in.</summary>
    [JsonPropertyName("id")]
    public int Id { get; init; }

    /// <summary>Which way it lies.</summary>
    [JsonPropertyName("direction")]
    public NeighbourDirection Direction { get; init; }

    /// <summary>What it makes, and how much of each it keeps when it is well stocked.</summary>
    [JsonPropertyName("makes")]
    public IReadOnlyList<MaterialCost> Makes { get; init; } = Array.Empty<MaterialCost>();

    /// <summary>How many head it keeps when it is well stocked; 0 for a neighbour that sells no animals.</summary>
    [JsonPropertyName("makes_animals")]
    public int MakesAnimals { get; init; }

    /// <summary>What it is short of, and how much of each it would take.</summary>
    [JsonPropertyName("wants")]
    public IReadOnlyList<MaterialCost> Wants { get; init; } = Array.Empty<MaterialCost>();
}

/// <summary>
/// What one neighbour holds today (D590) — a fact about the world, so hashed and saved; the rates are derived from it
/// and are neither (D335).
/// </summary>
/// <remarks>
/// All zero until the region wakes, which is the season the first trading post stands (`trade.md §11.1`): a village
/// that never builds one mixes nothing for it, and the goldens hold.
/// </remarks>
public sealed class NeighbourStock
{
    /// <summary>A neighbour's stock for a run with <paramref name="goods"/> goods.</summary>
    public NeighbourStock(int goods) => Held = new int[goods];

    /// <summary>How much of each good it holds, by good id.</summary>
    public int[] Held { get; }

    /// <summary>How many head it holds.</summary>
    public int Animals { get; set; }

    /// <summary>Whether it holds nothing at all — as every neighbour does before the region wakes.</summary>
    public bool IsEmpty
    {
        get
        {
            if (Animals != 0)
            {
                return false;
            }

            for (int i = 0; i < Held.Length; i++)
            {
                if (Held[i] != 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}

/// <summary>
/// The boat on its way, at the post, or leaving (D587 call 6, `specs/trade.md §4.4`): <b>sim state with three ticks</b>.
/// </summary>
/// <remarks>
/// ⭐ Its place on the river between them is <b>derived</b> from the ticks and the river's course — never stepped per
/// tick (CLAUDE.md). <i>Send the boat on</i> moves <see cref="LeavesAtTick"/>, so it is in the hash.
/// </remarks>
public sealed record TradeBoat
{
    /// <summary>Whose boat — an index into the region's neighbours.</summary>
    public int NeighbourId { get; init; }

    /// <summary>The tick it comes onto the map at its neighbour's edge.</summary>
    public ulong AppearsAtTick { get; init; }

    /// <summary>The tick it ties up at the post and the orders are settled.</summary>
    public ulong DocksAtTick { get; init; }

    /// <summary>The tick it casts off and sails on, off the far edge.</summary>
    public ulong LeavesAtTick { get; init; }
}
