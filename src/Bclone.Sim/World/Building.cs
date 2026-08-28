using System.Text.Json.Serialization;

namespace Bclone.Sim.World;

/// <summary>
/// One building — <b>a row in a data file, not a value in an enum</b>
/// (`specs/buildings-catalog.md`).
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐ THE FIFTH AND LAST APPLICATION OF D168's STANDING DISCIPLINE</b>, after
/// <see cref="SkillRow"/>, the crop id, <see cref="GoodRow"/> and <see cref="JobRow"/>.
/// `content-inventory.md` finding 8 is why this one is the gating half: the game has <b>ten</b>
/// building kinds and `TECH-EXAMPLE.md` names <b>forty-five</b>, and every knowledge-gated row in
/// `buildings-plan.md §4` is a building that does not exist.
/// </para>
/// <para>
/// <b>⛔ NOTHING IN THE SIM MAY SWITCH ON A BUILDING BY NAME</b>, with the exemptions
/// `buildings-catalog.md §2.2` and `§2.3` name on the record: the <em>derived</em> capacities,
/// which are the survival floor and therefore <see cref="VillageEconomy"/>'s business (D16);
/// <c>SimWorld.Complete</c>'s home arm, which moves a family in and is reasoning rather than a
/// value; and <c>Demolish</c>'s cart, which is not a building at all.
/// </para>
/// <para>
/// <b>⚠️ <see cref="Id"/> is appended, NEVER renumbered</b> — the rule <see cref="GoodRow"/>,
/// <see cref="JobRow"/>, <see cref="SkillRow"/> and <see cref="Terrain"/> all carry.
/// <b><see cref="BuildingKind"/> is not hashed today</b> — checked rather than assumed: `StateHash`
/// mixes a workplace's id, staffing, workers, queue rank, mode and store, and a store building's id
/// and store, and never either one's kind. That is what makes the catalogue a provable no-op. The
/// rule stands anyway, because the day a build queue or a save is serialised, an id is what it
/// carries.
/// </para>
/// <para>
/// <b>⛔ AND THE ROW DELIBERATELY DOES NOT CARRY A TRADE.</b> The building↔trade relation lives on
/// <see cref="JobRow.WorksAt"/>, which shipped first (D218) and is already what
/// <c>SimWorld.KindOf</c> reads. A trade column here would be the second source of truth this
/// catalogue exists to delete — so <see cref="BuildingsCatalog.EmployedBy(BuildingKind)"/> indexes that one
/// relation backwards instead.
/// </para>
/// </remarks>
public sealed record BuildingRow
{
    /// <summary>The id this building is known by. Appended, never renumbered.</summary>
    [JsonPropertyName("id")]
    public int Id { get; init; }

    /// <summary>
    /// What the village calls it — <em>"granary"</em>, <em>"storage shed"</em>.
    /// </summary>
    /// <remarks>
    /// <b>The label, not the identity.</b> <c>SimWorld.NameFor</c> numbers it — <em>"granary 2"</em>
    /// — except for a home, which is named by the family in it and never numbered.
    /// </remarks>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>What it costs, as goods and amounts. Empty for a building that is free.</summary>
    /// <remarks>
    /// <para>
    /// Zeros are dropped and the list is put in good order by <see cref="BuildingRecipe"/>, so a
    /// row may state a cost of zero without having to remember not to.
    /// </para>
    /// <para>
    /// <b>⚠️ The built-in ten are priced from the config keys they have always been priced from</b>
    /// — <c>logs_per_house</c>, <c>granary_logs</c>, <c>hut_stone</c> and the rest — rather than
    /// having those numbers restated here. That is deliberate and it is recorded in
    /// `buildings-catalog.md §3`: <c>logs_per_house</c> is <b>an economy anchor the recipe happens
    /// to spend</b>, read by the shed's capacity, the stockpile's capacity and the timber quota, so
    /// folding it into a row is a re-derivation rather than a move. <b>A modded row states its own
    /// cost inline</b>, which is the whole point.
    /// </para>
    /// </remarks>
    [JsonPropertyName("materials")]
    public IReadOnlyList<MaterialCost> Materials { get; init; } = Array.Empty<MaterialCost>();

    /// <summary>Ticks of work owed once the materials are on site.</summary>
    /// <remarks>
    /// <b>No materials and no work means free and instant</b>, and that is asked of the recipe
    /// rather than of the kind (D108) — <c>SimWorld.Mark</c> has tested
    /// <c>recipe.TotalMaterials == 0 &amp;&amp; recipe.WorkTicks == 0</c> since the builder's hut
    /// arrived. <b>No column is needed for it</b>, which is worth noticing: the one surface somebody
    /// might have added a <c>Free</c> bool for was made data three decisions ago by asking what that
    /// branch was actually testing.
    /// </remarks>
    [JsonPropertyName("work_ticks")]
    public int WorkTicks { get; init; }

    /// <summary>The store it becomes when it stands, or null for a building that stores nothing.</summary>
    /// <remarks>
    /// <b><see cref="StoreKind.Cart"/> is not among them and cannot be</b>: the cart is the wagon
    /// the founders arrive in, not a building the player may place.
    /// </remarks>
    [JsonPropertyName("stores")]
    public StoreKind? Stores { get; init; }

    /// <summary>
    /// How much that store holds, or <b>null to let the economy derive it</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ A STATED CAPACITY IS DATA; A DERIVED ONE IS THE SURVIVAL FLOOR</b>
    /// (`buildings-catalog.md §2.2`). The granary is a box of a stated size (D219). The shed and the
    /// stockpile are <em>solved</em> — a horizon of households, their firewood, the logs to split
    /// it, the stone the first huts cost — and typing those numbers in would be the exact move D16
    /// exists to refuse.
    /// </para>
    /// <para>
    /// <b>⚠️ A modded building has no derivation to appeal to and must state a capacity</b>, which
    /// is the test of whether this exemption is honest: it covers what the game already solves for
    /// itself, never what a modder can reach. Enforced at load.
    /// </para>
    /// </remarks>
    [JsonPropertyName("store_capacity")]
    public int? StoreCapacity { get; init; }

    /// <summary>
    /// How many people work there, or <b>null to let the economy derive it</b> — see
    /// <see cref="StoreCapacity"/>, which the same rule governs.
    /// </summary>
    /// <remarks>
    /// Stated for the woodcutter's hut, the farmhouse and the market; derived for the gatherer's
    /// hut (its ring ÷ tiles per worker), the forester's hut (what the woodcutters can eat, plus a
    /// hand for building) and the builder's hut.
    /// </remarks>
    [JsonPropertyName("seats")]
    public int? Seats { get; init; }

    /// <summary>The ring it gathers in, in tiles. <b>Zero for a workplace with no ring.</b></summary>
    [JsonPropertyName("gathering_radius")]
    public int GatheringRadius { get; init; }

    /// <summary>
    /// Its own buffer, in goods. <b>Zero for a workplace that hauls everything away.</b>
    /// </summary>
    /// <remarks>
    /// The farmhouse is the only building with one today (`crops-and-orchards.md §3.2a`): reaping
    /// is bursty and the granary is across the village, so the buffer underfoot fills first and the
    /// walk lengthens once it is full.
    /// </remarks>
    [JsonPropertyName("local_store_cap")]
    public int LocalStoreCap { get; init; }

    /// <summary>
    /// How many techniques it can hold records of. <b>Zero for a building that keeps none.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐⭐ A HARD CAP, AND `tech-tree.md §7c` IS EMPHATIC ABOUT WHY.</b> One record per node, no
    /// bundling: <em>"a library of N shelves holds N techniques, and choosing which N is the whole
    /// point. To hold more, build more libraries."</em> **A soft decay was considered and refused**
    /// — *"what is worth preserving?"* is a better question than *"is this record still legible?"*
    /// </para>
    /// <para>
    /// <b>⛔ AND IT IS CARRYING MORE WEIGHT THAN IT WAS DESIGNED TO.</b> `tech-tree.md §11`'s guard
    /// against *"the library is mandatory"* rested on three costs — the scriptorium's opportunity
    /// cost, this cap, and tacit nodes. **D204 deleted the first** by making recording automatic at
    /// mastery. *So a full library refusing a record, and saying so, is load-bearing rather than
    /// polish.*
    /// </para>
    /// <para>
    /// <b>⚠️ Shelves belong to the BUILDING, not to the village</b>, which is what makes *"build a
    /// second library"* an answer and — later — what makes copying a record into a second one
    /// survive a fire (§7c). Fire is not in this phase; the shape it needs is.
    /// </para>
    /// </remarks>
    [JsonPropertyName("shelves")]
    public int Shelves { get; init; }

    /// <summary>
    /// How many souls live in it. <b>Zero for a building nobody lives in.</b>
    /// </summary>
    /// <remarks>
    /// <b>⭐ THIS IS THE SEAM D153 RESERVED IN SO MANY WORDS</b> — <em>"a second arm, beside a
    /// `BuildingKind` appended to the enum"</em>, for Joe's <em>"eventually an unlock/tech that
    /// allows for larger homes/denser population."</em> The house-upgrade ladder `DESIGN.md §5`
    /// specifies — Wooden Cabin, Stone Cottage, Insulated Manor — is <b>three rows with three
    /// capacities and three recipes, and no new mechanism at all.</b>
    /// </remarks>
    [JsonPropertyName("house_capacity")]
    public int HouseCapacity { get; init; }
}

/// <summary>
/// The buildings that exist, indexed by id — <b>the one place the sim asks what a building is</b>.
/// </summary>
/// <remarks>
/// Rows go <b>at their stated id</b> rather than in file order, so reordering the list in a config
/// file cannot silently reinterpret every golden — <c>id</c> is the contract, position is not.
/// ⛔ <b>That is not a hypothetical:</b> eight of nine guards in `jobs-catalog.md` slice 2 passed a
/// break they should have caught, because the fixture happened to list rows in id order and so
/// could not tell the two apart (D218, D157's finding for the third time).
/// </remarks>
public sealed class BuildingsCatalog
{
    private readonly BuildingRow[] _rows;
    private readonly JobKind?[] _employs;

    /// <summary>Build the catalogue from config rows, and index the job relation backwards.</summary>
    /// <param name="rows">The buildings, in any order; each goes at its stated id.</param>
    /// <param name="jobs">
    /// The trades, whose <see cref="JobRow.WorksAt"/> is the <b>one</b> source of the
    /// building↔trade relation. This constructor only reads it the other way round.
    /// </param>
    public BuildingsCatalog(IReadOnlyList<BuildingRow> rows, JobsCatalog jobs)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(jobs);

        int count = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            count = rows[i].Id + 1 > count ? rows[i].Id + 1 : count;
        }

        _rows = new BuildingRow[count];
        for (int i = 0; i < rows.Count; i++)
        {
            _rows[rows[i].Id] = rows[i];
        }

        // ⭐ THE REVERSE INDEX, BUILT ONCE AND IN ID ORDER (`buildings-catalog.md §2.1`). The
        // relation is stated once, on the job; a column here would be a second copy that has to
        // agree with the first and that nothing checks — which is D148's finding as a data model
        // rather than as a word.
        _employs = new JobKind?[count];
        for (int id = 0; id < jobs.Count; id++)
        {
            BuildingKind? worksAt = jobs[id].WorksAt;
            if (worksAt is BuildingKind at && (int)at >= 0 && (int)at < count)
            {
                _employs[(int)at] = (JobKind)id;
            }
        }
    }

    /// <summary>How many buildings exist.</summary>
    public int Count => _rows.Length;

    /// <summary>The row for one building.</summary>
    public BuildingRow this[BuildingKind kind] => _rows[(int)kind];

    /// <summary>The row for one building by id — for a building a mod added, which has no enum value.</summary>
    public BuildingRow this[int id] => _rows[id];

    /// <summary>What the village calls it.</summary>
    public string NameOf(BuildingKind kind) => _rows[(int)kind].Name;

    /// <summary>What it costs to raise.</summary>
    public BuildingRecipe RecipeOf(BuildingKind kind) => RecipeOf((int)kind);

    /// <summary>What it costs to raise, by id.</summary>
    public BuildingRecipe RecipeOf(int id)
    {
        BuildingRow row = _rows[id]
            ?? throw new ArgumentOutOfRangeException(
                nameof(id), id, "That building has no row, so it has no recipe.");

        var materials = new MaterialCost[row.Materials.Count];
        for (int i = 0; i < materials.Length; i++)
        {
            materials[i] = row.Materials[i];
        }

        return new BuildingRecipe(row.WorkTicks, materials);
    }

    /// <summary>The store it becomes, or null.</summary>
    public StoreKind? StoresAs(BuildingKind kind) => _rows[(int)kind].Stores;

    /// <summary>The trade worked there, or null for a building nobody staffs.</summary>
    /// <remarks>
    /// <b>Read off <see cref="JobRow.WorksAt"/>, never off a column of its own</b> — see the
    /// constructor.
    /// </remarks>
    public JobKind? EmployedBy(BuildingKind kind) => _employs[(int)kind];

    /// <summary>The trade worked at a building id, or null.</summary>
    public JobKind? EmployedBy(int id) => _employs[id];

    /// <summary>The building of a given store kind, or null if no row claims it.</summary>
    /// <remarks>
    /// ⚠️ <b><see cref="StoreKind.Cart"/> has no building and never will</b> — <c>Demolish</c> names
    /// it, on the record (`buildings-catalog.md §2.3`), because the founders' wagon is not something
    /// the player put up.
    /// </remarks>
    public BuildingKind? ThatStores(StoreKind store)
    {
        for (int id = 0; id < _rows.Length; id++)
        {
            if (_rows[id]?.Stores == store)
            {
                return (BuildingKind)id;
            }
        }

        return null;
    }
}
