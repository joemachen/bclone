using System.Text.Json.Serialization;

namespace Bclone.Sim.World;

/// <summary>
/// What the village knows about one technique — <b>three states, not two</b>
/// (`specs/tech-tree.md §3`).
/// </summary>
/// <remarks>
/// <b>⭐ THE THIRD STATE IS THE DESIGN.</b> It makes *"make this permanent"* a goal the player
/// works toward over decades, instead of a free consequence of unlocking. Redundancy (more
/// knowers) and durability (a record) become genuinely different mechanisms with different costs,
/// rather than one being a better version of the other.
/// </remarks>
public enum KnowledgeState
{
    /// <summary>Nobody here has ever worked it out.</summary>
    Unknown = 0,

    /// <summary>At least one living person knows it. <b>Lost when the last of them dies.</b></summary>
    Known = 1,

    /// <summary>
    /// Written down, so one funeral cannot erase it.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>UNREACHABLE UNTIL THE LIBRARY LANDS</b> (`phase-4-the-tech-tree.md` slice 2). It is
    /// declared now because the state machine is the slice, and a two-state enum that grows a third
    /// value later would be a migration for no reason. <b>Nothing sets it yet, and that is the
    /// slice's whole point:</b> a technique currently dies with its last knower, every time, so the
    /// loss is real before the remedy exists.
    /// </remarks>
    Established = 2,
}

/// <summary>
/// One technique — <b>a row in a data file</b> (`specs/tech-tree.md`, `phase-4-the-tech-tree.md`).
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐ THE SIXTH APPLICATION OF D168's STANDING DISCIPLINE</b>, after <see cref="SkillRow"/>, the
/// crop id, <see cref="GoodRow"/>, <see cref="JobRow"/> and <see cref="BuildingRow"/>. It does not
/// re-argue the shape: rows at their stated id, ids appended and never renumbered, defaults in code
/// and overridable from config.
/// </para>
/// <para>
/// <b>⛔ A TECHNIQUE IS NOT A NODE THE PLAYER PICKS.</b> `DESIGN.md §2.7` refuses the Civil-style
/// research menu by name. Nobody chooses this: <b>a master works it out because of the years they
/// have already spent</b>, which is §4's PEOPLE mechanism and the only one this slice implements.
/// </para>
/// <para>
/// <b>⭐⭐ AND IT IS THE VILLAGE'S, NOT THE PERSON'S — which is what makes it different from
/// proficiency and what makes losing it hurt.</b> Once anybody has worked it out, <em>every</em>
/// worker of that trade does the job the better way. That is why <see cref="YieldBonusPercent"/>
/// is village-wide, and why the whole village's output drops when the last knower dies. A bonus
/// that applied only to the knower would be indistinguishable from mastery, which already bites
/// (D187).
/// </para>
/// <para>
/// <b>⛔ WHAT THIS ROW DELIBERATELY CANNOT DO: MAKE ANYBODY LEARN FASTER.</b> D196 proposed two
/// effects — more output, and *"+5% mastery gain"* — and <b>Joe held the second back on
/// 2026-08-26.</b> A technique that accelerates proficiency is <b>a soft ratchet on the one rule
/// protecting the late game</b> (`tech-tree.md §3a`): skill is personal and mortal, and a village
/// where each generation learns faster than the last has quietly banked something it can never
/// lose. **It is probably fine at 5%, and "probably fine" is not how this project treats that
/// rule.** It returns with a measurement behind it, not before.
/// </para>
/// </remarks>
public sealed record TechniqueRow
{
    /// <summary>The id this technique is known and hashed under. Appended, never renumbered.</summary>
    [JsonPropertyName("id")]
    public int Id { get; init; }

    /// <summary>What the village calls it — <em>"crop rotation"</em>.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The id of the <see cref="SkillRow"/> whose masters work this out.
    /// </summary>
    /// <remarks>
    /// <b>⭐ IT ATTACHES TO A SKILL, NOT TO A BUILDING, AND THAT IS WHAT MAKES THIS PHASE
    /// BUILDABLE NOW.</b> `content-inventory.md` finding 5's objection is real — **18 catalogue
    /// rows carry a knowledge flag and none of those 18 buildings exist** — so a tree that gated
    /// buildings would today gate almost nothing. **Skills exist**, all six of them, with a
    /// proficiency substrate Phase 3 built and a mastery event that already fires.
    /// </remarks>
    [JsonPropertyName("skill")]
    public int Skill { get; init; }

    /// <summary>
    /// How much more the trade produces once the village knows it, as a percentage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠️ A PROPOSAL UNTIL A RUN PRODUCES IT.</b> `tech-tree.md §12`'s standing refusal of false
    /// precision, and this project's rule that <em>if a number goes into a document, it comes from
    /// a run</em>. D196's own example is <em>"+15% firewood per log"</em>, and that is where these
    /// start rather than where they end.
    /// </para>
    /// <para>
    /// <b>⭐ IT IS UPSIDE ABOVE THE SURVIVAL FLOOR, NEVER A MOVE IN THE FLOOR ITSELF.</b>
    /// <see cref="VillageEconomy"/> solves what the village must produce not to die against the
    /// <em>base</em> config numbers, and nothing here touches them. **So losing a technique can
    /// never drop a village below the line it was built to survive on** — which is §0.1's *"you
    /// lose villagers, not runs"* applied to knowledge, and it is the reason this bonus lives at
    /// the point of production rather than in the derivation.
    /// </para>
    /// </remarks>
    [JsonPropertyName("yield_bonus_percent")]
    public int YieldBonusPercent { get; init; }

    /// <summary>
    /// What the village log says the first time anybody works it out. <c>{0}</c> is the person.
    /// </summary>
    /// <remarks>
    /// <b>⛔ ONE SENTENCE NAMING THE PERSON, OR IT IS A BUG.</b> `tech-tree.md §11`'s *opaque
    /// discovery* failure mode, which is non-negotiable 1 applied to this system: **an advance the
    /// player cannot account for is a bug**, not a surprise. It fires on the transition into
    /// <see cref="KnowledgeState.Known"/> and never again, so a second master is not news.
    /// </remarks>
    [JsonPropertyName("discovery_line")]
    public string DiscoveryLine { get; init; } = string.Empty;

    /// <summary>
    /// What the log says when the last knower dies unwritten. <c>{0}</c> is the person.
    /// </summary>
    /// <remarks>
    /// <b>⚠️ THIS IS NOT ALLOWED TO BE THE FIRST THE PLAYER HEARS OF IT.</b> The at-risk warning
    /// (<c>SimWorld.KnowledgeAtRiskNote</c>, D195) fires years earlier and names the same person.
    /// **A funeral surprise is `tech-tree.md §11`'s named failure mode** and `DESIGN.md §2.7`'s.
    /// </remarks>
    [JsonPropertyName("lost_line")]
    public string LostLine { get; init; } = string.Empty;
}

/// <summary>
/// The techniques that exist, indexed by id — <b>the one place the sim asks what a technique is</b>.
/// </summary>
/// <remarks>
/// Rows go <b>at their stated id</b> rather than in file order, so reordering a config file cannot
/// silently reinterpret a golden. ⛔ <b>D157's finding has now cost three slices</b> (D218, D222):
/// a catalogue written in id order cannot tell id from position, so the guard for this lives in a
/// fixture that lists the rows <em>backwards</em>.
/// </remarks>
public sealed class TechniquesCatalog
{
    private readonly TechniqueRow[] _rows;
    private readonly int[] _bySkill;

    /// <summary>Build the catalogue from config rows, and index them by the skill that yields them.</summary>
    public TechniquesCatalog(IReadOnlyList<TechniqueRow> rows, IReadOnlyList<SkillRow> skills)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(skills);

        int count = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            count = rows[i].Id + 1 > count ? rows[i].Id + 1 : count;
        }

        _rows = new TechniqueRow[count];
        for (int i = 0; i < rows.Count; i++)
        {
            _rows[rows[i].Id] = rows[i];
        }

        // ⭐ ONE TECHNIQUE PER SKILL TODAY, AND THE INDEX IS SIZED SO THAT STAYS AN ACCIDENT OF
        // CONTENT RATHER THAN A RULE IN THE CODE. `skills-catalog.md §4.3` warns in so many words
        // that the model must not assume one-to-one, and this is the second place that warning
        // applies. A second technique on one skill wins by lowest id, stated so it cannot become
        // an unordered tie (D15) — and the day it matters, this is the line to change.
        int widest = 0;
        for (int i = 0; i < skills.Count; i++)
        {
            widest = skills[i].Id + 1 > widest ? skills[i].Id + 1 : widest;
        }

        _bySkill = new int[widest];
        for (int i = 0; i < _bySkill.Length; i++)
        {
            _bySkill[i] = -1;
        }

        for (int id = 0; id < _rows.Length; id++)
        {
            TechniqueRow? row = _rows[id];
            if (row is null || row.Skill < 0 || row.Skill >= widest)
            {
                continue;
            }

            if (_bySkill[row.Skill] < 0)
            {
                _bySkill[row.Skill] = id;
            }
        }
    }

    /// <summary>How many techniques exist.</summary>
    public int Count => _rows.Length;

    /// <summary>The row for one technique by id.</summary>
    public TechniqueRow this[int id] => _rows[id];

    /// <summary>The technique a master of this skill works out, or -1 if the skill yields none.</summary>
    public int FromSkill(int skillId) =>
        skillId >= 0 && skillId < _bySkill.Length ? _bySkill[skillId] : -1;
}

/// <summary>
/// A library — <b>shelves with records on them, and the only thing that outlives a knower</b>
/// (`specs/tech-tree.md §7c`).
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐ IT IS ITS OWN KIND OF THING, AND THAT WAS FORCED RATHER THAN CHOSEN.</b> A finished
/// building becomes a store, a workplace, or a home, and a library is none of the three — it holds
/// no goods, nobody works there yet, and nobody lives in it. <c>SimConfig.ValidateBuildings</c>
/// refuses a row that <em>"stores nothing, employs nobody and houses nobody"</em>, and it was right
/// to: <b>the validator caught that a library needed a reason to exist before the library did.</b>
/// </para>
/// <para>
/// <b>⚠️ NO KEEPER YET, AND THAT IS A DEBT RATHER THAN A DESIGN.</b> §7c says a library
/// <em>"needs a keeper, or records degrade"</em> — and decay is out of this phase
/// (`phase-4-the-tech-tree.md §3`), so a keeper would be a seventh trade competing for hands with
/// nothing to do. **When decay lands, the keeper lands with it**, and this is the type it attaches
/// to.
/// </para>
/// </remarks>
public sealed class Library
{
    private GridPos _position;

    /// <summary>Where it stands.</summary>
    /// <remarks>
    /// <b>⚠️ <c>init</c> for building it, <see cref="MoveTo"/> for moving it.</b> A library is
    /// worth moving precisely because its records travel with it — the shelves are the building.
    /// </remarks>
    public required GridPos Position { get => _position; init => _position = value; }

    /// <summary>Move it. Only a finished relocation may.</summary>
    internal void MoveTo(GridPos to) => _position = to;

    /// <summary>What the village calls it.</summary>
    public required string Name { get; init; }

    /// <summary>How many records it can hold. Stated by its row, never derived.</summary>
    public required int Shelves { get; init; }

    /// <summary>
    /// The techniques written down here, in the order they were recorded.
    /// </summary>
    /// <remarks>
    /// <b>Order is recording order and it is hashed</b>, so two runs of one seed shelve the same
    /// techniques in the same order. A set would read more naturally and would let two runs that
    /// recorded the same things in different years hash identically — <b>which would be a lie</b>:
    /// they are different villages that made the same choices at different times, and the shelf a
    /// record sits on is what a later slice's fire will take.
    /// </remarks>
    public List<int> Records { get; } = new();

    /// <summary>Whether there is room for one more.</summary>
    public bool HasRoom => Records.Count < Shelves;
}
