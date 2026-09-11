using System.Text.Json.Serialization;

namespace Bclone.Sim.World;

/// <summary>
/// One crop a farm can grow — <b>a row that names its good</b> (`crops-and-orchards.md`, D348).
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐ THE TILE HAS CARRIED A CROP ID SINCE D161, AND NOTHING EVER SAID WHAT THE ID MEANT.</b>
/// <c>GeneratedMap.CropAt</c> is a byte beside the terrain, sown as <c>1</c> by a constant called
/// <c>TheOneCrop</c>, and the reap wrote <c>Goods.Produce</c> by name regardless. *One crop, in a
/// model shaped for many* — this is the many. The id is now the index of a row, and the row says
/// what comes off the field.
/// </para>
/// <para>
/// <b>Joe's ruling (2026-09-11): wheat is a real good, not a rename.</b> <c>Goods.Produce</c> stays
/// the umbrella a forager fills; a farm grows <see cref="Yields"/>. `food-catalog.md` lists wheat,
/// barley, oats and corn as *Grain* — **each is a row here the day it is wanted**, and a modder can
/// write one without touching a line of C#.
/// </para>
/// <para>
/// ⚠️ <b>Id 0 means "nothing sown"</b> — that is what a bare field carries and what the hash has
/// mixed since D161 — so rows start at 1. **Appended, never renumbered**: the id is hashed per tile.
/// </para>
/// </remarks>
public sealed record CropRow
{
    /// <summary>The id a sown tile carries. 1 upward; 0 is bare ground.</summary>
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    /// <summary>What the crop is called — the one place the word lives.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>The good a reaped tile of it gives.</summary>
    [JsonPropertyName("yields")]
    public required Goods Yields { get; init; }
}

/// <summary>
/// The crops that exist, by id — <b>the one place the sim asks what a field grows</b>.
/// </summary>
public sealed class CropsCatalog
{
    private readonly CropRow?[] _rows;

    public CropsCatalog(IReadOnlyList<CropRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        int highest = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            highest = Math.Max(highest, rows[i].Id);
        }

        _rows = new CropRow?[highest + 1];
        for (int i = 0; i < rows.Count; i++)
        {
            _rows[rows[i].Id] = rows[i];
        }

        Goods = BuildGoods();
    }

    /// <summary>How many ids the catalogue spans, bare ground included.</summary>
    public int Count => _rows.Length;

    /// <summary>The row with this id, or null for bare ground and for ids nobody defined.</summary>
    public CropRow? this[int id] => id >= 0 && id < _rows.Length ? _rows[id] : null;

    /// <summary>
    /// ⭐ The crop a farm sows today — <b>the lowest id that exists</b>, until the farm can choose.
    /// </summary>
    /// <remarks>
    /// One crop, in a model shaped for many. When the farmhouse gets a *"grow…"* control this
    /// becomes the farm's own choice; nothing else in the sim should ask this catalogue which crop
    /// to plant.
    /// </remarks>
    public CropRow TheOne
    {
        get
        {
            for (int i = 1; i < _rows.Length; i++)
            {
                if (_rows[i] is CropRow row)
                {
                    return row;
                }
            }

            throw new InvalidOperationException("No crop is defined, so nothing can be sown.");
        }
    }

    /// <summary>
    /// What a tile carrying this crop id gives when reaped.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>THIS IS THE ONLY PLACE THE REAPED GOOD IS DECIDED.</b> Before D348 the reap wrote
    /// <c>Goods.Produce</c> by name. A tile with an id nobody defined is a defect, not a fallback
    /// — it throws rather than quietly yielding produce.
    /// </remarks>
    public Goods GoodOf(byte cropId) =>
        this[cropId]?.Yields
        ?? throw new InvalidOperationException(
            $"A field carries crop id {cropId}, which no crop row defines.");

    /// <summary>Every good some crop yields, in id order — what a farmhand may be carrying home.</summary>
    public IReadOnlyList<Goods> Goods { get; }

    private IReadOnlyList<Goods> BuildGoods()
    {
        var goods = new List<Goods>();
        for (int i = 1; i < _rows.Length; i++)
        {
            if (_rows[i] is CropRow row && !goods.Contains(row.Yields))
            {
                goods.Add(row.Yields);
            }
        }

        return goods;
    }
}
