using System.Text.Json.Serialization;

namespace Bclone.Sim.World;

/// <summary>
/// One batch trade — a hut where a hand turns one good into another a batch at a time (`specs/batch-trade.md §3`, W1).
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐ THE MILL, THE OVEN, THE TAILOR'S SHOP AND THE WORKSHOP AS ROWS.</b> D522's <see cref="Batch"/> already wrote the
/// walk, the work, the put-away and every stop once; what stayed per trade was every switch that turned a trade into its
/// words, its hands and its stops — and each new batch trade had to find them all by grep. This row is what they read.
/// </para>
/// <para>
/// <b>⚠️ A row in the catalogue's sense, not yet the data file's</b> (§2): the built-in four are priced from the
/// config's own keys (<c>wheat_per_grind</c>, <c>sew_ticks</c> …), exactly as <c>BuildingRow</c> is, so every
/// <c>Config with { GrindTicks = … }</c> still means what it says. Phase 7's first step moves the rows themselves.
/// </para>
/// <para>
/// <b>⭐ <see cref="Stop"/> and <see cref="Hands"/> name a rule, not a formula.</b> Bread feeds, flour feeds the ovens, a
/// coat keeps a body warm, a cart serves a household — four mechanics, each a named method in code. A new trade picks an
/// existing rule; a new kind of demand is a new rule and its name. <b>Content is a row; a new kind of demand is a
/// mechanic.</b>
/// </para>
/// </remarks>
public sealed record BatchRow
{
    /// <summary>Who works it — and so which building, through the job's <c>works_at</c>.</summary>
    [JsonPropertyName("trade")]
    public JobKind Trade { get; init; }

    /// <summary>
    /// ⭐ True when the recipe is the product's, not the row's: the hut's <see cref="Workplace.MakeGood"/> and that good's
    /// <c>made_from</c> (the carpenter, D678 — the smith reads his tool's <c>forged_from</c> the same way, D446). Then
    /// <see cref="Input"/>, <see cref="InputPerBatch"/> and <see cref="Output"/> are unused, and a batch makes one.
    /// </summary>
    [JsonPropertyName("from_the_product")]
    public bool FromTheProduct { get; init; }

    /// <summary>What one batch takes.</summary>
    [JsonPropertyName("input")]
    public Goods Input { get; init; }

    /// <summary>How much of the input one batch takes.</summary>
    [JsonPropertyName("input_per_batch")]
    public int InputPerBatch { get; init; }

    /// <summary>What one batch makes.</summary>
    [JsonPropertyName("output")]
    public Goods Output { get; init; }

    /// <summary>How much one batch makes, before a tool and a technique (which make it quicker, never bigger — D522).</summary>
    [JsonPropertyName("output_per_batch")]
    public int OutputPerBatch { get; init; } = 1;

    /// <summary>Firewood burned once a stint, when the hut is lit — nought for every hut but the oven.</summary>
    [JsonPropertyName("firing")]
    public int Firing { get; init; }

    /// <summary>Ticks one batch takes.</summary>
    [JsonPropertyName("ticks")]
    public int Ticks { get; init; }

    /// <summary>Batches before walking home.</summary>
    [JsonPropertyName("per_stint")]
    public int PerStint { get; init; }

    /// <summary>The card's verb: <em>"grind"</em> — <em>"Nothing to grind"</em>.</summary>
    [JsonPropertyName("verb")]
    public string Verb { get; init; } = string.Empty;

    /// <summary>One batch in a sentence: <em>"a grind"</em>. <c>{product}</c> is the output, one of it.</summary>
    [JsonPropertyName("one_of")]
    public string OneOf { get; init; } = string.Empty;

    /// <summary>Where the walk goes, in the village log: <em>"the mill"</em> — <em>"walking to the mill"</em>.</summary>
    [JsonPropertyName("walk_to")]
    public string WalkTo { get; init; } = string.Empty;

    /// <summary>The work in a word or two: <em>"grinding wheat"</em>. <c>{products}</c> is the output's name.</summary>
    [JsonPropertyName("working")]
    public string Working { get; init; } = string.Empty;

    /// <summary>
    /// What a stint's batches are called in <em>"the 2nd grind of the day"</em> — or empty for <em>"the 2nd of the day"</em>.
    /// </summary>
    [JsonPropertyName("stint_word")]
    public string StintWord { get; init; } = string.Empty;

    /// <summary>The card's line before the first batch is done: <em>"lighting the oven"</em>. <c>{product}</c> as above.</summary>
    [JsonPropertyName("first_batch")]
    public string FirstBatch { get; init; } = string.Empty;

    /// <summary>Why it stops besides the player's limit and an empty store — the demand rule it works toward.</summary>
    [JsonPropertyName("stop")]
    public BatchStop Stop { get; init; }

    /// <summary>How many hands the village asks for.</summary>
    [JsonPropertyName("hands")]
    public BatchHands Hands { get; init; }

    /// <summary>Where in the quota's order its hands are taken.</summary>
    [JsonPropertyName("quota_band")]
    public QuotaBand QuotaBand { get; init; }

    /// <summary>Whether its hands are asked for at all while food comes first (the tailor's are not; the carpenter's are).</summary>
    [JsonPropertyName("stands_down_in_famine")]
    public bool StandsDownInFamine { get; init; }
}

/// <summary>A batch trade's own reason to stop, besides the player's limit and an empty store (`batch-trade.md §3`).</summary>
[Flags]
public enum BatchStop
{
    /// <summary>Works while there is input and room — the oven, whose firewood guard is its <see cref="BatchRow.Firing"/>.</summary>
    None = 0,

    /// <summary>⛔ Not while the village is short of food: its input is grain somebody could eat (the mill, D522).</summary>
    FedFirst = 1,

    /// <summary>With no limit set, only toward what the ovens will bake (the mill, `food-chain.md §6`).</summary>
    OvensSupplied = 2,

    /// <summary>With no limit set, until every adult has a coat and the shelves a year's wear (the tailor, D659).</summary>
    EveryoneClothed = 4,

    /// <summary>With no limit set, until every household has a cart and the shelves a spare (the carpenter, D678).</summary>
    EveryHouseholdCarted = 8,
}

/// <summary>How many hands a batch trade is wanted for (`batch-trade.md §3`).</summary>
public enum BatchHands
{
    /// <summary>Every seat of every standing hut with work to do (the mill and the oven, D522).</summary>
    EverySeatWithWork = 0,

    /// <summary>
    /// The shortfall its <see cref="BatchRow.Stop"/> names over what one hand makes in a year, never more than the seats
    /// with work (the tailor and the carpenter, D659, D678 — the smith's shape).
    /// </summary>
    ShortfallOverAYear = 1,
}

/// <summary>Where in the quota's order a batch trade's hands are taken (`batch-trade.md §4`).</summary>
public enum QuotaBand
{
    /// <summary>After the smith, before the quarrier: a convenience every winter errand is better for (the coat, the cart).</summary>
    AfterTools = 0,

    /// <summary>After the miner, before the herdsman: upside above the food floor (the mill and the oven, D522).</summary>
    AfterStone = 1,
}

/// <summary>
/// The batch trades, built once from <c>SimConfig.BatchRows</c> and held on the world, as <see cref="JobsCatalog"/> is —
/// indexed by job id, so a trade asks for its row in one step.
/// </summary>
public sealed class BatchCatalog
{
    private readonly BatchRow?[] _byTrade;

    /// <summary>Build the catalogue; the rows keep their order, which is the order the quota takes them in a band.</summary>
    public BatchCatalog(IReadOnlyList<BatchRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        Rows = rows;
        int count = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            count = (int)rows[i].Trade + 1 > count ? (int)rows[i].Trade + 1 : count;
        }

        _byTrade = new BatchRow?[count];
        for (int i = 0; i < rows.Count; i++)
        {
            _byTrade[(int)rows[i].Trade] = rows[i];
        }
    }

    /// <summary>Every batch trade, in the config's order.</summary>
    public IReadOnlyList<BatchRow> Rows { get; }

    /// <summary>A trade's row, or null when it is not a batch trade.</summary>
    public BatchRow? For(JobKind trade) =>
        (int)trade >= 0 && (int)trade < _byTrade.Length ? _byTrade[(int)trade] : null;
}
