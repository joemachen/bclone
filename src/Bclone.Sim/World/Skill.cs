using System.Text.Json.Serialization;

namespace Bclone.Sim.World;

/// <summary>
/// One skill — <b>a row in a data file, not a value in an enum</b>
/// (`specs/skills-catalog.md §4.1`, D168).
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐ THIS IS D168's STANDING DISCIPLINE APPLIED AT THE FIRST OPPORTUNITY SINCE IT WAS
/// WRITTEN.</b> Joe: *"modders should be able to add buildings, essentially add anything to the
/// game."* <see cref="BuildingKind"/>, <see cref="JobKind"/>, <c>Goods</c> and <see cref="Terrain"/>
/// are four C# enums hashed by position and pinned by every golden — **a modder can change their
/// numbers and cannot add one.** `crops-and-orchards.md §4` is the one place this project got it
/// right, and this is the second.
/// </para>
/// <para>
/// <b>⛔ NOTHING IN THE SIM MAY SWITCH ON A SKILL BY NAME.</b> The behaviour a skill drives comes
/// from the row, never from a <c>switch</c> on an id — otherwise the row is decoration over an
/// enum that still exists, just spelled differently.
/// </para>
/// <para>
/// <b>⚠️ <see cref="Id"/> enters the state hash in a stated order</b> (§4.1, §8), like the crop id
/// and for the same reason: *same seed + same content ⇒ same history* is the contract a mod API
/// has to respect. **Ids are appended, never renumbered** — renumbering silently reinterprets
/// every golden and every seed, which is the rule <see cref="JobKind.Forester"/> is pinned to 1 by.
/// </para>
/// <para>
/// <b>Id 0 is deliberately not a skill.</b> A default <c>int</c> must never name something, so
/// that a field nobody filled in cannot quietly mean *foraging*. That is D108's silent-default
/// finding, headed off rather than found later.
/// </para>
/// </remarks>
public sealed record SkillRow
{
    /// <summary>The id this skill is hashed and stored under. Appended, never renumbered.</summary>
    [JsonPropertyName("id")]
    public int Id { get; init; }

    /// <summary>What the village calls it.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The work that grows it — <b>a job kind, and not necessarily one-to-one forever</b>.
    /// </summary>
    /// <remarks>
    /// §4.3: the table happens to be 1:1 today and **the model must not assume it**, because a
    /// skill two jobs grow (a smith and a farrier) is obviously coming, and because
    /// <see cref="JobKind"/> is an enum while this is a row.
    /// </remarks>
    [JsonPropertyName("grown_by")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public JobKind GrownBy { get; init; }

    /// <summary>
    /// Whether this skill can be written down — <b>the column that ships empty-but-honoured</b>.
    /// </summary>
    /// <remarks>
    /// `tech-tree.md §3b` needs <c>false</c> to be a real column so apprenticeship is never
    /// obsoleted by the school — *a midwife's hands, an eye for soil, knowing when the fish run*.
    /// **The first genuinely tacit skill arrives with the physician or the herbalist** (§4.2), and
    /// the column exists now so that arrival is not a retrofit across the hash and every golden.
    /// </remarks>
    [JsonPropertyName("recordable")]
    public bool Recordable { get; init; } = true;

    /// <summary>
    /// Years <b>on the task</b> before this trade is mastered, or null to use
    /// <c>mastery_years</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐⭐ PER SKILL BECAUSE THE TRADES DO NOT ACCRUE AT THE SAME RATE, AND THAT WAS
    /// MEASURED</b> (D182, Joe's call). `mastery_years` is twenty and §3.3b promises *"a founder
    /// who sticks to one trade masters it, and is a master for the back half of their life."*
    /// Landing 1's probe found that promise held for a farmer and quietly failed for everybody
    /// seasonal: **twenty years on the task was 32 calendar years for a forager and 34 for a
    /// marketer**, because D44 stands their work down every winter. A farmer mastered on
    /// schedule and a forager took half again as long, **with nothing on screen saying why.**
    /// </para>
    /// <para>
    /// <b>So each trade states the years of its own work that make a master</b>, derived from
    /// what a year of that trade is actually worth
    /// (`SkillTests.WhatAYearOnEachTradeIsActuallyWorth`). **Every trade now masters at about
    /// twenty calendar years**, which is what the design promised in the first place.
    /// </para>
    /// <para>
    /// <b>⛔ THE ALTERNATIVE WAS ONE LINE AND IT WAS THE DISHONEST ONE</b> — credit a held seat
    /// through the winter even though the village stood the work down. That would have made the
    /// panel say *"nineteen years in the woods"* about somebody who spent five of them idle at
    /// home, and this game's whole claim is that its numbers mean what they say. **This is D165's
    /// split instead: a stated fact about each trade, with the consequence derived.**
    /// </para>
    /// <para>
    /// Null rather than a default of twenty, so a modder adding a row gets the game's headline
    /// number without having to know it, and <c>mastery_years</c> stays the one place to move
    /// them all at once.
    /// </para>
    /// </remarks>
    [JsonPropertyName("mastery_years")]
    public int? MasteryYears { get; init; }

    /// <summary>
    /// Where the years were spent, for the villager panel: *"nineteen years <b>in the
    /// fields</b>."*
    /// </summary>
    /// <remarks>
    /// <b>The sentence, not the number</b> (§7). The years are the diegetic fact; <c>proficiency
    /// 73</c> is the spreadsheet this game is defined against (§1.4).
    /// </remarks>
    [JsonPropertyName("years_phrase")]
    public string YearsPhrase { get; init; } = string.Empty;

    /// <summary>
    /// The line the village log carries when somebody masters this — <c>{0}</c> is the name,
    /// <c>{1}</c> the years.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⭐⭐ JOE ASKED FOR THIS BY NAME</b> (§3.3b, D174): *"it should be noted in the event log
    /// when someone achieves mastery."* It is the sentence `DESIGN.md`'s opening paragraph
    /// promises the game will produce, and **the first thing in this whole design the player will
    /// feel** — it works from the day the substrate lands, whether or not mastery is doing
    /// anything mechanical yet.
    /// </para>
    /// <para>
    /// <b>Content, so it is here rather than in code</b> (D3), and **written without pronouns**:
    /// villagers have names and no sex, so a line that says *"she"* is guessing about a person
    /// the sim knows nothing about.
    /// </para>
    /// </remarks>
    [JsonPropertyName("mastery_line")]
    public string MasteryLine { get; init; } = string.Empty;
}

/// <summary>
/// What one villager has put into one skill — <b>the whole of the substrate's state</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sparse: an entry exists only once the villager has actually done the work.</b> A village
/// where nobody has held a job carries no entries and mixes nothing into the hash (§8).
/// </para>
/// <para>
/// <b>⭐ <see cref="Mastered"/> is not redundant with <see cref="Ticks"/>, and §3.6 records
/// why.</b> It is what makes §11.6's *fires **once*** true: without it, somebody who masters,
/// moves trades, decays back under the threshold and returns would be narrated twice. **It is
/// also §5.4's *record of achievement* arriving early** — permanent, dies with the person, and
/// **grants nothing**, which is the only reading that leaves `tech-tree.md §11`'s ratchet intact.
/// </para>
/// </remarks>
public sealed class SkillProgress
{
    /// <summary>Which skill, by <see cref="SkillRow.Id"/>.</summary>
    public required int SkillId { get; init; }

    /// <summary>Ticks spent holding the trade. Integer only (D2).</summary>
    public int Ticks { get; set; }

    /// <summary>Whether this person has ever reached mastery. Set once; never cleared.</summary>
    public bool Mastered { get; set; }
}
