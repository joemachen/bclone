using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Bclone.Sim.World;

/// <summary>
/// One good — <b>a row in a data file, not a value in an enum</b>
/// (`specs/goods-catalog.md`, D210).
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐ THE THIRD APPLICATION OF D168's STANDING DISCIPLINE</b>, after
/// <see cref="SkillRow"/> and the crop id. Joe: *"modders should be able to add buildings,
/// essentially add anything to the game."* `content-inventory.md` finding 8 is why this one
/// became urgent: the game has <b>six</b> goods and the content pass needs about <b>thirty-five</b>.
/// </para>
/// <para>
/// <b>⚠️ THE PROMISE HERE IS NARROWER THAN <see cref="SkillRow"/>'s, AND IT IS STATED RATHER THAN
/// GLOSSED.</b> Skills have no enum at all. <see cref="Goods"/> keeps one, because
/// <see cref="Stockpile"/> indexes by it and because the whole economy names food directly —
/// `TotalFood`, the birth gate, the quota, granary capacity. <b>The enum is an alias for the first
/// six ids, not a second source of truth</b>: the catalogue is what says how many goods there are
/// and what each one does.
/// </para>
/// <para>
/// <b>⛔ NOTHING IN THE SIM MAY SWITCH ON A GOOD BY NAME.</b> What a good is called, where it comes
/// from, what a tile of it yields and who will store it all come from the row — otherwise the row
/// is decoration over an enum that still exists, just spelled differently. That rule is
/// <see cref="SkillRow"/>'s, word for word, and it is the one that makes this real.
/// </para>
/// <para>
/// <b>⚠️ <see cref="Id"/> is hashed by position and appended, NEVER renumbered</b> — the rule
/// <see cref="JobKind.Forester"/> is pinned to 1 by, and <see cref="Terrain"/> too. Renumbering
/// silently reinterprets every golden, every saved stock limit and every seed.
/// </para>
/// <para>
/// <b>Id 0 IS a good here, unlike <see cref="SkillRow"/>.</b> That is deliberate and it is the one
/// place these two rows differ: a skill id of zero must never name anything, because a villager who
/// has done no work has no row and a default <c>int</c> would quietly mean *foraging*. A good id of
/// zero is <see cref="Goods.Food"/>, which <see cref="Stockpile"/> has indexed from zero since D82
/// and which every golden is pinned to.
/// </para>
/// </remarks>
public sealed record GoodRow
{
    /// <summary>The id this good is stored and hashed under. Appended, never renumbered.</summary>
    [JsonPropertyName("id")]
    public int Id { get; init; }

    /// <summary>
    /// What the village calls it — <b>the one place the word lives</b>.
    /// </summary>
    /// <remarks>
    /// <b>⛔ IT USED TO LIVE IN TWO PLACES, WITH THE SAME WORDS IN BOTH.</b>
    /// <c>Stockpile.Name</c> and <c>SimWorld</c> each carried
    /// <c>Goods.Food =&gt; "food", Goods.Logs =&gt; "logs", Goods.Firewood =&gt; "firewood"</c>.
    /// That is D148's finding and D188's — <em>two vocabularies for one thing</em> — in code rather
    /// than in the view, and it is exactly the drift a row exists to stop.
    /// </remarks>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// What the ground it is taken from is called: <em>"woodland"</em>, <em>"a stone seam"</em>.
    /// </summary>
    /// <remarks>
    /// Empty for a good nothing harvests, in which case <see cref="Name"/> is used — which is what
    /// the switch this replaced did in its default arm.
    /// </remarks>
    [JsonPropertyName("source_name")]
    public string SourceName { get; init; } = string.Empty;

    /// <summary>
    /// What one tile of its source gives up when cleared. <b>Zero for goods nothing harvests.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// These were <c>logs_per_forest_tile</c>, <c>stone_per_rock_tile</c> and
    /// <c>iron_per_deposit_tile</c> — three loose config keys read by <b>exactly one switch and
    /// nothing else</b>, which is what made moving them safe. The comment beside that switch asked
    /// for precisely this: <em>"a new harvestable kind is a row… not a fifth place to remember."</em>
    /// </para>
    /// <para>
    /// <b>A tile of the source is a DEPOSIT and this is what is in it</b> (D84, D87) — take it and
    /// the ground is grass. That makes it a different quantity from <c>cut_yield</c>: a tree stand
    /// yields forever, and this yields once.
    /// </para>
    /// <para>
    /// <b>⚠️ Content today, DERIVED the moment the tree stand retires.</b>
    /// `building-placement.md §12.8` is explicit that per-tile yield is what the whole timber
    /// economy gets re-derived against — while stands still stand, the brush is an extra source
    /// rather than the only one, so nothing hangs off it yet.
    /// </para>
    /// <para>
    /// <b>Less iron per tile than stone, and fewer iron seams</b> — together those are what make
    /// iron worth walking for rather than merely further away.
    /// </para>
    /// </remarks>
    [JsonPropertyName("yield_per_tile")]
    public int YieldPerTile { get; init; }

    /// <summary>
    /// Which kinds of store will hold it — <b>a list in data, a bitmask in memory</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A list because that is what reads well in a config file a modder is editing; the mask is
    /// built once at load. This replaces <c>StoreBuilding.KindAccepts</c>, which was a switch over
    /// <see cref="StoreKind"/> naming goods on the right-hand side — so adding a good meant editing
    /// a method in the sim.
    /// </para>
    /// <para>
    /// <b>⚠️ The pile is deliberately absent from every row.</b> A heap does not specialise: it
    /// takes anything, and its <em>size</em> rather than its rules is what stops it being the
    /// granary. That stays in code because it is a statement about the pile, not about any good.
    /// </para>
    /// </remarks>
    /// <para>
    /// Parsed from strings by the global <c>JsonStringEnumConverter</c> on
    /// <c>SimConfigLoader.Options</c>, so a row reads <c>"stored_by": ["Shed", "Cart"]</c> rather
    /// than a list of integers a modder would have to look up.
    /// </para>
    [JsonPropertyName("stored_by")]
    public IReadOnlyList<StoreKind> StoredBy { get; init; } = new List<StoreKind>();
}

/// <summary>
/// The goods that exist, indexed by id — <b>the one place the sim asks what a good is</b>.
/// </summary>
/// <remarks>
/// <para>
/// Built once from <c>SimConfig.GoodsCatalog</c> and held on the world. <b>Every question the sim
/// used to answer with a <c>switch</c> over <see cref="Goods"/> is answered here instead</b>, which
/// is what makes <see cref="GoodRow"/> real rather than decorative.
/// </para>
/// <para>
/// <b>⚠️ <see cref="Count"/> is the source of truth for how many goods there are</b> — not
/// <c>Enum.GetValues&lt;Goods&gt;()</c>, which is what <see cref="Stockpile"/> and
/// <c>StockLimits</c> used to ask and which can only ever return six.
/// </para>
/// </remarks>
public sealed class GoodsCatalog
{
    private readonly GoodRow[] _rows;
    private readonly int[] _storedBy;

    /// <summary>Build the catalogue from config rows, ordered and indexed by id.</summary>
    /// <remarks>
    /// Rows are placed <b>at their stated id</b> rather than in file order, so that reordering the
    /// list in a config file cannot silently reinterpret every golden — <c>id</c> is the contract,
    /// position in the file is not.
    /// </remarks>
    public GoodsCatalog(IReadOnlyList<GoodRow> rows)
    {
        int count = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            count = rows[i].Id + 1 > count ? rows[i].Id + 1 : count;
        }

        _rows = new GoodRow[count];
        _storedBy = new int[count];

        for (int i = 0; i < rows.Count; i++)
        {
            GoodRow row = rows[i];
            _rows[row.Id] = row;

            int mask = 0;
            for (int k = 0; k < row.StoredBy.Count; k++)
            {
                mask |= 1 << (int)row.StoredBy[k];
            }

            _storedBy[row.Id] = mask;
        }
    }

    /// <summary>How many goods exist.</summary>
    public int Count => _rows.Length;

    /// <summary>The row for one good.</summary>
    public GoodRow this[Goods goods] => _rows[(int)goods];

    /// <summary>The row for one good, by id — for goods a mod added, which have no enum value.</summary>
    public GoodRow this[int id] => _rows[id];

    /// <summary>What the village calls it: <em>"food"</em>, <em>"logs"</em>.</summary>
    public string NameOf(Goods goods) => _rows[(int)goods].Name;

    /// <summary>
    /// What the ground it comes from is called, falling back to the good's own name.
    /// </summary>
    /// <remarks>
    /// The fallback is what the switch this replaced did in its default arm
    /// (<c>goods.ToString().ToLowerInvariant()</c>), preserved so the sentence a brush writes is
    /// unchanged for every good that never named a source.
    /// </remarks>
    public string SourceNameOf(Goods goods)
    {
        GoodRow row = _rows[(int)goods];
        return string.IsNullOrEmpty(row.SourceName) ? row.Name : row.SourceName;
    }

    /// <summary>What one tile of its source gives up. Zero for goods nothing harvests.</summary>
    public int YieldPerTileOf(Goods goods) => _rows[(int)goods].YieldPerTile;

    /// <summary>Whether a kind of store will hold this good at all.</summary>
    public bool StoredBy(Goods goods, StoreKind kind) =>
        (_storedBy[(int)goods] & (1 << (int)kind)) != 0;
}
