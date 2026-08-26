using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Bclone.Sim.World;

/// <summary>
/// One trade — <b>a row in a data file, not a value in an enum</b> (`specs/jobs-catalog.md`, D218).
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐ THE FOURTH APPLICATION OF D168's STANDING DISCIPLINE</b>, after <see cref="SkillRow"/>,
/// the crop id and <see cref="GoodRow"/>. `content-inventory.md` finding 8 named goods and jobs
/// together: the game has <b>six</b> trades and `TECH-EXAMPLE.md` names about <b>forty</b> worker
/// roles, so a modder can change what a forager does and <b>cannot add a fisherman</b>.
/// </para>
/// <para>
/// <b>⛔ NOTHING IN THE SIM MAY SWITCH ON A JOB BY NAME</b> — the rule <see cref="GoodRow"/> and
/// <see cref="SkillRow"/> are both pinned by. <b>With exactly one exemption, recorded rather than
/// quietly taken: the idle note</b> (`jobs-catalog.md §2.1`). That is real per-trade reasoning —
/// a forester asks about painted ground and work modes, a farm about sowing seasons — and forcing
/// it into a data column would be a worse lie than leaving it in code.
/// </para>
/// <para>
/// <b>And the exemption costs a modder nothing, which is why it needed no ruling.</b> Two of the
/// six built-in trades — the marketer and the builder — <b>already have no idle note</b>, so
/// *"this trade offers no explanation"* is an existing, valid state. A modded trade inherits it:
/// it says nothing, which is honest, rather than saying something wrong.
/// </para>
/// <para>
/// <b>⚠️ <see cref="Id"/> is hashed by position and appended, NEVER renumbered.</b>
/// <see cref="JobKind.Forester"/> is pinned to 1 by every golden and every saved staffing figure.
/// </para>
/// </remarks>
public sealed record JobRow
{
    /// <summary>The id this trade is stored and hashed under. Appended, never renumbered.</summary>
    [JsonPropertyName("id")]
    public int Id { get; init; }

    /// <summary>What the sim calls it: <em>"forager"</em>.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// What the staffing panel calls a roomful of them: <em>"foragers"</em>, <em>"traders"</em>.
    /// </summary>
    /// <remarks>
    /// <b>⚠️ DELIBERATELY A SECOND COLUMN RATHER THAN <see cref="Name"/> PLUS AN "s".</b> The
    /// marketer is <em>"traders"</em> to the allocator and <em>"marketer"</em> to the roster —
    /// <b>D188's unresolved vocabulary split</b>, which is Joe's to settle and which this row must
    /// not settle by accident. Carrying both words keeps the question open; deriving one from the
    /// other would answer it silently.
    /// </remarks>
    [JsonPropertyName("plural")]
    public string Plural { get; init; } = string.Empty;

    /// <summary>The gerund the village log uses: <em>"gathering"</em>, <em>"felling timber"</em>.</summary>
    [JsonPropertyName("doing")]
    public string Doing { get; init; } = string.Empty;

    /// <summary>
    /// The workplace this trade staffs, or null for a trade with no building.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐ THE SEAM `jobs-catalog.md §3` RECORDED IS CLOSED, AND IT TURNED OUT NOT TO BE THE
    /// TYPE.</b> That spec called this column <em>"honest and temporary — it points at an enum that
    /// is still an enum, so a modded trade can only staff a building that already exists"</em>, and
    /// the obvious reading was that it had to become an <c>int</c>. <b>It did not.</b> The enum is
    /// <b>an alias for the first ten ids</b>, exactly as <see cref="Goods"/> is for the first six
    /// (`goods-catalog.md §2.1`) — a modded building is <c>(BuildingKind)10</c>, which C# permits and
    /// <c>JsonStringEnumConverter</c> writes and reads as a number. What was actually missing was a
    /// <b>catalogue for that value to resolve against</b>, and that is what arrived.
    /// </para>
    /// <para>
    /// <b>⚠️ And keeping the enum keeps the word.</b> An int column would make every built-in row
    /// read <c>"works_at": 7</c> where it reads <c>"works_at": "GathererHut"</c> today — a modder
    /// looking up which number is the gatherer's hut, in the file that is supposed to tell them.
    /// <em>Legibility applies to the data files too.</em>
    /// </para>
    /// <para>
    /// <b>⭐⭐ AND IT IS THE ONE SOURCE OF THE BUILDING↔TRADE RELATION.</b>
    /// <see cref="BuildingRow"/> carries no trade column;
    /// <see cref="BuildingsCatalog.EmployedBy(BuildingKind)"/> indexes this one backwards at load.
    /// Two hand-written directions that must agree, with nothing checking that they do, is D148's
    /// finding as a data model rather than as a word — and it is what `SimWorld.Complete` and this
    /// column were, side by side, for a phase.
    /// </para>
    /// </remarks>
    [JsonPropertyName("works_at")]
    public BuildingKind? WorksAt { get; init; }

    /// <summary>
    /// Whose stock limit stands this trade down, or null for a trade no limit reaches.
    /// </summary>
    /// <remarks>
    /// A woodcutter stops when the village has the firewood it asked for; a forester when it has
    /// the logs; a farmer when it has the food. <b>A forager is deliberately absent</b> — food is
    /// gathered as well as farmed, and standing the gatherers down on a full granary is a decision
    /// nobody has taken.
    /// </remarks>
    [JsonPropertyName("limited_by")]
    public Goods? LimitedBy { get; init; }
}

/// <summary>
/// The trades that exist, indexed by id — <b>the one place the sim asks what a job is</b>.
/// </summary>
/// <remarks>
/// Built once from <c>SimConfig.JobsCatalog</c> and held on the world, exactly as
/// <see cref="GoodsCatalog"/> is. <b><see cref="Count"/> is the source of truth for how many trades
/// exist</b> — not <c>Enum.GetValues&lt;JobKind&gt;()</c>, which can only ever return six.
/// </remarks>
public sealed class JobsCatalog
{
    private readonly JobRow[] _rows;

    /// <summary>Build the catalogue from config rows, placed at their stated ids.</summary>
    /// <remarks>
    /// Rows go <b>at their stated id</b> rather than in file order, so reordering the list in a
    /// config file cannot silently reinterpret a golden — <c>id</c> is the contract, position is
    /// not.
    /// </remarks>
    public JobsCatalog(IReadOnlyList<JobRow> rows)
    {
        int count = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            count = rows[i].Id + 1 > count ? rows[i].Id + 1 : count;
        }

        _rows = new JobRow[count];
        for (int i = 0; i < rows.Count; i++)
        {
            _rows[rows[i].Id] = rows[i];
        }
    }

    /// <summary>How many trades exist.</summary>
    public int Count => _rows.Length;

    /// <summary>The row for one trade.</summary>
    public JobRow this[JobKind kind] => _rows[(int)kind];

    /// <summary>The row for one trade by id — for a trade a mod added, which has no enum value.</summary>
    public JobRow this[int id] => _rows[id];

    /// <summary>What the sim calls it.</summary>
    public string NameOf(JobKind kind) => _rows[(int)kind].Name;

    /// <summary>What a roomful of them is called.</summary>
    public string PluralOf(JobKind kind) => _rows[(int)kind].Plural;

    /// <summary>The gerund, for the village log.</summary>
    public string DoingOf(JobKind kind) => _rows[(int)kind].Doing;

    /// <summary>The workplace this trade staffs, or null.</summary>
    public BuildingKind? WorksAt(JobKind kind) => _rows[(int)kind].WorksAt;

    /// <summary>The id of the workplace this trade staffs, or null.</summary>
    /// <remarks>
    /// For a modded building, which has an id and no name of its own in the enum. The two are the
    /// same number — see <see cref="JobRow.WorksAt"/>.
    /// </remarks>
    public int? WorksAtId(JobKind kind) =>
        _rows[(int)kind].WorksAt is BuildingKind kind_ ? (int)kind_ : null;

    /// <summary>Whose stock limit stands this trade down, or null.</summary>
    public Goods? LimitedBy(JobKind kind) => _rows[(int)kind].LimitedBy;
}
