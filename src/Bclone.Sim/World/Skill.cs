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
    /// <b>⛔⛔ AND NO ROW SETS ONE, BECAUSE THE MEASUREMENT KILLED THE REASON TO.</b> Building
    /// the probe that was to derive these numbers found the winter story was **wrong**: foraging,
    /// forestry, woodcutting and trading are all worked in **all four seasons**, and the
    /// mid-winter figure that looked like availability was a **headcount**. The real cause was
    /// decay, since deleted (D183). *The mechanism is kept because trades may genuinely diverge
    /// one day; tuning it to a cause that does not exist would have buried the one that does.*
    /// </para>
    /// <para>
    /// ⚠️ <b>So this is an inert column, honestly labelled</b>, in the same standing as
    /// <see cref="Recordable"/> — real, unused, and cheaper to have now than to retrofit across
    /// the hash and every golden later.
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
    /// What the years were spent being, for the villager panel: *"sixteen years <b>as a
    /// farmer</b>."*
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The sentence, not the number</b> (§7). The years are the diegetic fact; <c>proficiency
    /// 73</c> is the spreadsheet this game is defined against (§1.4).
    /// </para>
    /// <para>
    /// <b>⚠️ IT NAMES THE PROFESSION, AND IT MUST MATCH WHAT THE REST OF THE SCREEN CALLS THAT
    /// JOB</b> (Joe, D188). It used to be scene-setting — *"in the fields"*, *"among the
    /// trees"*, *"at the woodpile"* — which read well and left the player joining a phrase to a
    /// trade by inference. **Naming the trade is what makes it answer the question the panel is being
    /// asked**: *what has this person been?*
    /// </para>
    /// <para>
    /// <b>⛔ THE VIEW ALREADY HAS TWO VOCABULARIES FOR THESE JOBS AND THIS IS NOW A THIRD PLACE
    /// THEY MUST AGREE.</b> `Main.ProfessionName` says **Gatherer** and **Vendor**;
    /// `Main.TradeOf` says **forager** and **marketer**; the same job, two words, two panels.
    /// These follow `TradeOf`, because that is what the roster shows beside a villager's name
    /// and therefore what sits directly above this line. **The split itself is unresolved and
    /// is Joe's to settle** — see `DESIGN.md §5`.
    /// </para>
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
/// How practised somebody is, in words — <b>a reading of proficiency, not a second stored
/// thing</b> (`skills-catalog.md §3.2c`, D190).
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐ THE PLAYER READS WORDS AND NOT NUMBERS.</b> *"Master woodcutter"* is a sentence;
/// <c>proficiency 73</c> is a spreadsheet, and §1.1 refuses it. Joe's own vocabulary was already
/// tiered — *"masters, mids — whatever that is"*, *"apprentice forester"* — and the historical
/// ladder fits the game's register exactly. **Joe's call, 2026-08-23**, with *journeyman* as the
/// word for the middle: it is the one tier with no obvious plain-English name, which is
/// presumably why he wrote *"whatever that is"*.
/// </para>
/// <para>
/// <b>⛔ FOUR NAMES OVER ONE INTEGER, NEVER A STORED FIELD.</b> Two sources of truth for one fact
/// is D148's bug and D76's seam; a tier that could disagree with the ticks behind it would be
/// exactly that.
/// </para>
/// <para>
/// <b>⚠️ AND ONLY TWO OF THEM WORK AT DIFFERENT SPEEDS, which Joe accepted knowingly.</b> Action
/// durations are 3 and 4 ticks, so the sim can express one speed step and no more (D187) — it
/// falls at about 70% of mastery, **inside the journeyman band**. So an apprentice works exactly
/// as fast as a novice, and a journeyman past the step works exactly as fast as a master. **The
/// names are honest about a career; they are not four behaviours**, and they will only become
/// four if the durations ever grow enough to hold them.
/// </para>
/// </remarks>
public enum SkillTier
{
    /// <summary>No time on the task at all. *"Wendell has never swung an axe."*</summary>
    Novice = 0,

    /// <summary>Learning. *"Agnes is learning the wood."*</summary>
    Apprentice = 1,

    /// <summary>Competent and unremarkable — Joe's *"mid"*. *"Otto knows his trade."*</summary>
    Journeyman = 2,

    /// <summary>Twenty years on the task (§3.3b). Never lost once reached.</summary>
    Master = 3,
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
/// <b>⭐ <see cref="Mastered"/> is not redundant with <see cref="Work"/>.</b> It is what makes
/// §11.6's *fires **once*** true: without it, anybody at the threshold would be narrated again on
/// the following tick and every tick after. **It is also §5.4's *record of achievement* arriving
/// early** — permanent, dies with the person, and **grants nothing**, which is the only reading
/// that leaves `tech-tree.md §11`'s ratchet intact.
/// </para>
/// <para>
/// <b>⭐⭐ NOTHING EVER TAKES ANY OF THIS AWAY, AND DECAY WAS BUILT BEFORE IT WAS DELETED
/// (D183, Joe: *"let's give to the player, not punish or decay"*).</b> §3.4 argued decay was
/// required, on the grounds that *"a fifty-year-old who did six jobs is a master of six"*.
/// **Measured, that is arithmetically impossible:** mastery needs 9,600 ticks and an adult life
/// is about 26,400, so **at most two masteries fit in a whole life even holding a trade every
/// waking tick** — and over sixty years the most any living villager had mastered was **one**.
/// Meanwhile the rate that was shipped took **37% of everything one forager earned**, which is
/// exactly the trap §3.4 itself forbids. *The spec's fear was unfounded and its cure was the
/// disease.*
/// </para>
/// </remarks>
public sealed class SkillProgress
{
    /// <summary>Which skill, by <see cref="SkillRow.Id"/>.</summary>
    public required int SkillId { get; init; }

    /// <summary>
    /// Ticks spent holding the trade — <b>the honest calendar fact</b>, and what the panel says.
    /// </summary>
    /// <remarks>
    /// <b>This is the number a player reads</b> (*"Seventeen years in the woods"*), so it counts
    /// time and nothing else: every tick holding the seat is worth exactly one, whether the
    /// villager spent it felling or waiting for logs. <see cref="Work"/> is where the weighting
    /// lives, precisely so this one cannot drift from the truth about somebody's life.
    /// </remarks>
    public int Ticks { get; set; }

    /// <summary>
    /// Weighted work put into the trade — <b>what mastery is measured against</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A tick out on the job is worth more than a tick waiting for it</b> (D183) — see
    /// <c>SimConfig.SkillWorkPerActiveTick</c>. In hundredths of a tick rather than ticks, so the
    /// weighting can be a percentage without a float anywhere near sim state (D2).
    /// </para>
    /// <para>
    /// ⚠️ <b>It is deliberately not shown anywhere.</b> `proficiency 73` is the spreadsheet §7
    /// rejects by name; this is the machinery under the sentence, not the sentence.
    /// </para>
    /// </remarks>
    public int Work { get; set; }

    /// <summary>Whether this person has ever reached mastery. Set once; never cleared.</summary>
    public bool Mastered { get; set; }
}
