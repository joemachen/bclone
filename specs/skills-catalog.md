# Spec: Skills — the catalogue, and the model underneath it

**Decisions:** D172 (scheduled this), D28 (make time-on-task personal — this discharges it),
D16 (numbers are derived, not picked), D2 (integer-only sim state), D3 (content is data),
D51/D62/D106 (the player says how many, the sim says who), D107 (`professions.md`'s role model),
D156 (an uneducated child works at twelve), D168 (a new kind of thing should be a data row).
Neighbours: **`tech-tree.md` (this is its missing substrate — §6)**, `professions.md §4` (the
roles a skill attaches to), `labour-allocation.md` (who gets the job), `clothing.md` and
`livestock.md` (parked, and both add skills when they land).
**Status:** 📝 **WRITTEN, NOT BUILT.** Nothing in this document exists in the sim. `Villager` has
`Vigour` and `VigourStage` and **no concept of skill at all**; there is no apprenticeship, no
proficiency and no teaching. This is `DESIGN.md §4` queue item 2, ahead of per-site yield and
Phase 3.

> **⚠️ This status line is load-bearing. Update it the day the slice merges** — D159 found five
> specs claiming "not started" for systems that had shipped, and `CLAUDE.md` now requires a
> spec's status to be checked against the suite rather than against itself.

---

## 1. Why this exists, and why it is written before anything is built

**`tech-tree.md` is written entirely on top of a proficiency model that does not exist.** Its
load-bearing rule is §3a:

> **A record preserves the method, not the proficiency.** When the last master smith dies with
> the technique written down, the village does not lose steel. But the next person to open that
> record starts near-zero and needs years at the forge to reach where she was.

**That rule is what stops the tech tree becoming a ratchet**, which is what stops it re-creating
the dead late game §2.3 exists to fix. It is the single most load-bearing sentence in that
document — and **it cannot be implemented against nothing**. "Starts near-zero" needs a zero to
start near; "where she was" needs a *there*.

So this is a catalogue *and* a model, in that order of importance. `buildings-plan.md` and
`food-catalog.md` established the habit: **write the list before writing the code**, because a
list you can read is a design you can argue with, and the tenth entry is the one that shows the
shape was wrong.

**⚠️ What this document is not.** It is not Phase 3. It decides what a skill *is*, what the
catalogue *contains*, and what the sim owes the tech tree. **The slice that builds it is Phase 3**
and it will need its own measurements — this spec deliberately leaves every number that wants a
running sim to §12, because inventing them here would be the false precision `tech-tree.md §12`
already refuses.

---

## 2. Which pillars, and which non-negotiables

- **§2.1 Villagers as agents** — the whole of it. *"A villager is an agent with a growing,
  transferable skill, not a headcount"*, and *"that skill dies with the person unless an elder
  apprentices a youth."* This spec is that pillar's data model.
- **§2.7 Knowledge-based tech tree** — §6 is the contract.
- **§1.1 Legibility above all.** A skill the player cannot read off the screen is an invisible
  multiplier, which is the same objection `crops-and-orchards.md §1` raised against the seasonal
  yield curve: *a number going up where the player cannot watch it*. §7 is not decoration.
- **§1.2 Meditative pace.** ⛔ **Skill must not add babysitting.** If the player ends up assigning
  apprenticeships every few years to avoid losing things, the design is wrong — see §5.3.
- **§1.4 Stories from people, not spreadsheets.** *"Old Mabel trained her granddaughter as
  herbalist before the fever took her"* is DESIGN.md's opening sentence about what this game is
  for. **Skill is the mechanic that sentence is made of.**
- **§1.6 Slow and traceable over clever and opaque.** §3.1 refuses experience points for the same
  reason §2.7 refuses research points.

---

## 3. ⭐ The model — what a skill is

### 3.1 Time on the task, counted in ticks. Not experience points.

**A villager's proficiency in a skill is the time they have spent doing it.** Not a score that
work adds to — the time itself, accumulated in sim ticks, converted to a proficiency by a stated
curve.

**Why the distinction matters rather than being pedantry.** An XP number is a thing the player
learns to farm: it invites "what gives the most XP?", which is a question this game should never
be able to answer. Time-on-task can only be answered one way — *she did the work* — and it is
the same argument §2.7 makes when it refuses a Civ-style research bar. It is also, as
`tech-tree.md §3b` says of tacit knowledge, **true**, which is the best kind of game rule.

**It is already how the game talks.** D120's `CommuteNote`, D147's `IdleNote` and D148's staffing
rows all say what somebody *did*; a villager panel reading *"nineteen years in the fields"* is the
same voice.

### 3.2 ⭐⭐ THE REFERENCE IS A COMPETENT VILLAGER — and this is the rule that keeps the economy standing

**This is the most important decision in the document and the one most likely to be got wrong.**

Every derived number in this game — `gather_yield`, `firewood_per_split`, `crop_yield_per_tile`,
`MaxHomeToWorkTiles`, the fuel budget — is solved against **a villager with no skill concept at
all**, because that is the only villager the sim has ever had. `VillageEconomy` asks *how much
must one pair of hands bring back so that the village does not die?* and answers it against
today's flat behaviour.

**So if skill multiplies output above today's baseline, every one of those numbers is wrong the
day skill ships** — the village gets richer for free, the birth gate opens sooner, and the
economy has to be re-derived from the bottom. **D122 froze nineteen people the last time that
chain moved**, and it moved by one tile.

**The rule: skill is a spread around the present behaviour, not a bonus on top of it.**

- **A villager at the reference proficiency behaves exactly as villagers behave today.**
- A novice is **worse** than that; a master is **better**.
- **The reference is placed so that a working life's average sits on it** — which is what makes
  the existing derivation still true of the village as a whole.

**What this buys, stated so it is not re-litigated:** losing a master is a real loss and gaining
one is a real gain, **without a single derived number moving**. The economy keeps its meaning,
the guards keep their bars, and the goldens move once for behaviour rather than for arithmetic.
**The alternative — skill as a multiplier above one — is an economy-wide re-derivation wearing a
character system's clothes.**

> ⚠️ **The honest cost of this choice:** a village of nothing but novices is *poorer* than
> today's village, so **the founding gets harder**. Four founders who have never done the work
> are exactly the opening `cold-start.md` measures, and that measurement has to be re-run.
> **This is the single biggest risk in Phase 3 and it should be probed before anything is built**
> (METHODOLOGY §3 — *probe a mechanic before building it*), the way D53's cold model and D56's
> clothing were both measured as no-ops before a line was written. See §12.

### 3.3 What skill changes: **time first, yield second** — which discharges D28

D28 has been an open Phase 1 debt since 2026-07-26, and it says this in its own words:

> *Let vigour, and later skill, scale **how long** a job takes as well as how much it yields.
> Most diegetic, does double duty for the skill pillar, and deepens D12: an old villager would
> not just bring back less, they would be out longer.*

**Today only yield is scaled**, in six places, all of the form `x * villager.Vigour / 100`.
Nothing anywhere scales duration, and that is precisely why **two adults of one household holding
one job are on the same tile 99.9% of ticks** (§5's measurement). They run the same deterministic
program on the same inputs for their whole lives.

**So skill scales the action's duration** — `gather_ticks`, `cut_ticks`, `sow_ticks`,
`reap_ticks`, `split_ticks`, `PlantTicks` — and that alone breaks the lockstep, because two
people who take different numbers of ticks to do the same thing stop arriving together within a
season and never re-synchronise.

**⚠️ Duration is not a free axis either, and the spec must say so.** `VillageEconomy` derives
trips-per-year from a fixed round trip; a faster master takes *more* trips. **That is exactly why
§3.2's reference rule is load-bearing** — the derivation moves to the reference villager and the
spread cancels across a village. The Phase 3 slice must assert that, not assume it.

**Yield may also be scaled, and it is the second lever rather than the first**, for a legibility
reason: a villager who is *out longer* is visible on the map, and one who *brings back less* is
only visible in a panel.

### 3.4 Skill decays — slowly, and only off the task

A villager who leaves a trade loses ground in it. **Not to zero, and not fast.**

**Why it has to exist at all:** without decay, a fifty-year-old who did six jobs is a master of
six, and *"knowledge lives in people"* becomes *"old people are simply better"* — which flattens
back into vigour and deletes the reason apprenticeship is interesting. **Decay is what makes a
career a choice.**

**Why it must be gentle:** D46's reshuffle moves people between jobs every three years, and the
player moves them with the professions panel whenever they like. **A decay rate that punishes
either would make the labour allocator feel like a trap**, and the player would start fighting a
system that exists to save them work — §1.2, and D51's whole argument.

Rate is §12's, and it should be **derived against the reshuffle cadence** rather than picked.

---

## 4. The catalogue

### 4.1 ⭐ Skills are rows in a data file, not values in an enum

**This is D168's standing discipline applied at the first opportunity since it was written.**
Joe, 2026-08-22: *"modders should be able to add buildings, essentially add anything to the
game."* `BuildingKind`, `JobKind`, `Goods` and `Terrain` are four C# enums hashed by position and
pinned by every golden — **a modder can change their numbers and cannot add one** — and
`crops-and-orchards.md §4` is the one place this project got it right, with the crop id in data
rather than in the enum.

**A skill is a row: an id, a name, the work that grows it, and whether it can be written down.**
Nothing in the sim should switch on a skill by name.

**The cost of the other choice is known and quoted:** retrofitting an enum means touching the
state hash, every golden and every call site at once. Cheap now, expensive later — and skills are
the one kind of thing this design *guarantees* will grow, because every profession in
`professions.md §4` that is still ❌ brings one.

**⚠️ The id enters the state hash in a stated order**, like the crop id and for the same reason:
*same seed + same content ⇒ same history* is the contract a mod API has to respect (§4 of
`DESIGN.md`'s modding audit). **Hashed sparsely**, so a village where nobody has any proficiency
hashes exactly as it does today — which is the no-op contract D165, D112 and D87 have all used to
land a system without moving a golden.

### 4.2 The skills that exist on day one

**One per job that exists, and no more.** Every ❌ profession in `professions.md §4` brings its
own when it lands; inventing them now would be a catalogue of things nobody can hold.

| Skill | Grown by | What mastery looks like | Recordable? |
|---|---|---|---|
| **Foraging** | `JobKind.Forager` | Knows which ground is worth walking to and works a ring faster | ✅ |
| **Forestry** | `JobKind.Forester` | Fells and plants quicker; the wood recovers around them | ✅ |
| **Woodcutting** | `JobKind.Woodcutter` | More firewood from the same logs, and faster | ✅ |
| **Farming** | `JobKind.Farmer` | Sows and reaps quicker — **the visible one**, because a field is a place you watch | ✅ |
| **Building** | `JobKind.Builder` | Raises a frame in fewer ticks | ✅ |
| **Trading** | `JobKind.Marketer` | Picks better legs; less walking for the same delivery | ✅ |

**⛔ Laborers hold no skill, and that is deliberate.** D66 refused `JobKind.Laborer` on the
grounds that a laborer is *"the villagers no job currently wants"* rather than a trade — a
position in the priority order, not a profession (D87). **A skill in being spare is a
contradiction**, and it would quietly make the fallback a career.

**⚠️ Tacit skills exist in the model and none are in this table.** `tech-tree.md §3b` needs
`Recordable: false` to be a real column so apprenticeship is never obsoleted by the school — *a
midwife's hands, an eye for soil, knowing when the fish run*. **The first genuinely tacit skill
arrives with the physician or the herbalist**, and the column ships empty-but-honoured rather
than being retrofitted.

### 4.3 What a skill is *not* attached to

- **Not to a building.** A forester who moves to a different hut is the same forester. Skill is
  on the person; `professions.md §3`'s five elements are about the workplace.
- **Not to a household.** Inheritance is apprenticeship (§5) and nothing else — *knowledge lives
  in people* means it does not quietly flow down a family tree for free.
- **Not to a `JobKind` one-to-one, forever.** The table above happens to be 1:1 today. **The
  model must not assume it**, because a skill that two jobs grow (a smith and a farrier) is
  obviously coming, and because `JobKind` is an enum and a skill is a row.

---

## 5. Transfer — how skill outlives a person

### 5.1 Apprenticeship is the mechanism, and it is the pillar's whole point

**§2.1: *skill dies with the person unless an elder apprentices a youth.*** An experienced
villager working alongside an inexperienced one in the same trade **speeds the youth's growth**;
without it, the youth grows at the ordinary rate and the master's years die with them.

**⚠️ Working alongside, not a menu.** The strong version of §2.2 applies here: the player says
*how many* and the sim says *who* (D51, D62, D106). **If apprenticeship becomes a per-pair
assignment screen, this design has grown a slotting UI on the one axis the whole game refuses
it.** The lever is a *policy* — see §5.3.

### 5.2 What a record gives, and what it does not — the tech-tree contract

Restating `tech-tree.md §3a` in this document's terms, because this is the side that has to
implement it:

| | The method | The proficiency |
|---|---|---|
| **A living knower dies, node written** | ✅ kept — node stays `Established` | ⛔ **lost with them** |
| **A living knower dies, node unwritten** | ⛔ lost — node re-locks | ⛔ lost |
| **An apprentice was trained** | ✅ kept | ✅ **partly carried** — the apprentice has real years |

**That middle column is the entire anti-ratchet.** A library makes a catastrophe into a setback;
**only a person makes it into continuity.**

### 5.3 ⛔ The failure mode this must design against

§2.1 names it: *"punishing the player for losses they couldn't foresee. Knowledge-at-risk must be
**visible and actionable**."*

**Visible** is §7. **Actionable** is the harder half, and it constrains the design: if the only
remedy is *"assign an apprentice to Mabel before she dies"*, then the player must be watching
every elder in the village, forever — **which is babysitting, and §1.2 forbids it.**

**So the default must be safe and the lever must be coarse.** A village that is left alone should
apprentice *by itself* wherever an elder and a youth already work the same trade — the player's
control is a policy (*how much of the village's labour goes into teaching*), not a pairing. **The
player's job is to notice a trade with one old holder and no youths, and to put somebody there.**
That is a decision about staffing, which is a control that already exists.

> ⚠️ **AND THE ANTI-VACUITY GUARD IS THE ONE THAT DECIDES WHETHER ANY OF THIS IS REAL**
> (`tech-tree.md §13`, and D143's lesson): **a run with no apprenticeships must actually lose
> something.** If a village that never teaches ends up where a village that does ends up, the
> whole pillar is decoration — and this project has shipped a decorative system before and only
> found out by measuring (D56's clothing, a no-op over 300 years).

---

## 6. ⭐ The contract with `tech-tree.md`

What Phase 4 may assume exists, once Phase 3 lands. **Written as a contract because the tree is
already specced against it**, and a promise made in one document and read in another is exactly
where D159 found five specs lying.

1. **A per-villager, per-skill proficiency** that is an integer, hashed, deterministic, and
   readable from the tree's own code.
2. **A stated reference level** (§3.2), so *"starts near-zero"* and *"where she was"* both name
   something.
3. **A years-in-practice figure per villager per skill**, because four of the tree's eight unlock
   mechanisms need it: **PEOPLE** (*"long enough in the work"*), **SEREN** (*"available only to
   someone already deep in the practice"*), **ADJ** (*"two knowers"*) and **DOING** at the
   village scale.
4. **A `Recordable` flag on every skill** (§4.2), so the scriptorium can refuse tacit ones
   without the tree hard-coding a list.
5. **A "who still knows this, and how old are they" query**, which is what the at-risk warning
   (§7) and the tree's re-lock rule are both made of.

**⛔ What the tree must NOT assume:** that a skill maps to exactly one `JobKind` (§4.3), that
proficiency is bounded above by anything the tree knows, or that a record can restore
proficiency (§5.2).

---

## 7. Legibility — what the player actually sees

**A skill the player cannot read is an invisible multiplier**, and this project has rejected that
shape twice already (D37's spoilage, `environment-and-seasons.md §5.1`'s yield curve).

- **On the villager panel** — the sentence, not the number: *"Hattie · farmer · nineteen years in
  the fields."* The years are the diegetic fact; a percentage is the spreadsheet.
- **On the workplace panel** — who works here and how practised they are, in the same vocabulary
  D148 gave the professions rows (*"2 working of 3 seats"*), because that is the panel a player
  looks at when they want to know why a hut is slow.
- **⭐ The at-risk line, and it is the one §2.1 demands** — *"Mabel is 68 and the only soul who
  knows herbalism."* **Narrated on its edges, in the village log**, not shown permanently in the
  Overview: D42, D123 and D147 all settled that an always-on alert is one the player stops
  reading, and D147's rule is the model — `IdleNote` returns *the sentence or nothing*, so the
  marker and the panel cannot disagree.
- **In the life log** — apprenticeship is a life event. *"Mabel took Wren to the fields."* This
  is the sentence DESIGN.md's opening paragraph promises the game will produce.

---

## 8. Determinism and the state hash

- **Integer only** (D2). Proficiency is an integer; any curve is integer arithmetic. **No floats
  in sim-critical paths**, and the banned-API analyzer already enforces it at build time.
- **Hashed, in a stated order, sparsely** (§4.1). A village with no proficiency anywhere hashes
  as it does today — which is what lets the substrate land before the behaviour, the way
  `Terrain.Field/Sown/Ripe` did in `crops-and-orchards.md`.
- **Seeded, never random per-tick.** Apprenticeship pairing and any SEREN-style roll draw from
  the seeded stream in a fixed order (D15 — *an unordered tie is a desync waiting to happen*).
- **⚠️ The goldens will move**, and once: the moment duration varies by person, every history
  downstream differs. That is D163's shape and it is expected — **taken last, one commit, one
  stated reason** (D152).

---

## 9. What is deliberately not here

- **⛔ No skill for laborers** (§4.2).
- **⛔ No talent, aptitude or birth-luck.** A villager is what they have done. Rolling a "born
  gifted" stat would make the most important thing about a person something the player cannot
  see coming or act on — which is §2.3's failure mode (*pressure that isn't traceable to a
  decision*) wearing a character sheet.
- **⛔ No skill-gated job refusal.** The allocator stays cost-first (D15, D23, D120). **Skill may
  make somebody a better choice; it must never make them an ineligible one** — D120 deleted the
  last fence in this game and traded it for a consequence, and this must not quietly rebuild one.
- **⛔ No literacy, schooling or records.** Those are `tech-tree.md`'s and they need this first.
- **⛔ No numbers that want a running sim** — they are §12, per `tech-tree.md §12`'s own refusal
  of false precision.

---

## 10. Testing

Sim logic is pure and deterministic; exploit it (METHODOLOGY §3).

- **Determinism** — same seed, 200 years, identical proficiency state for every living villager.
  A regression here is P0.
- **The no-op contract** — with the substrate in and nothing growing, both fifty-year goldens are
  **byte-identical**. Landing it any other way means the substrate and the behaviour cannot be
  told apart when something breaks.
- **⭐ Growth is time-on-task and nothing else** — a villager moved off a trade stops gaining in
  it the same tick, and one who never holds it never gains.
- **⭐⭐ The reference rule holds (§3.2)** — a village whose villagers sit at the reference
  produces **within a stated tolerance of today's village** over fifty years. **This is the guard
  that decides whether the economy still stands**, and it must be written before the curve is
  tuned rather than after.
- **⭐⭐ Anti-vacuity (§5.3)** — a run with no apprenticeships **loses** proficiency the village
  had, measurably, against a run with them. *If nothing is ever lost, the pillar is decorative.*
- **Decay is gentle** — a villager moved by D46's three-year reshuffle and moved back has not
  lost a career.
- **Lockstep is broken (D28)** — two adults of one household holding one job are on the same tile
  **far less** than the measured 99.9%. **The number to beat is on record**, which is what makes
  this guard falsifiable rather than a vibe.
- **Legibility** — every apprenticeship and every at-risk transition emits exactly one narrative
  line naming the person.
- **Shipped config, not only the fixture** (METHODOLOGY §3) — `ShippedConfigTests` runs the real
  file, and the drift between the two has produced D48, D49 and D50.

---

## 11. Definition of Done

1. This spec current, and its status line true.
2. The substrate lands as a **provable no-op**: goldens unmoved, determinism green.
3. Growth, decay and the reference rule guarded, each **checked red and counted** — the standing
   rule, and it has caught a vacuous guard four times.
4. **D28 discharged**, with the lockstep measurement re-run and quoted.
5. **The cold start re-measured** (§3.2's stated risk) — a founding of novices either survives or
   the reference moves, and either way the number comes from a run.
6. The at-risk warning reachable by the player, because *a feature the player cannot reach does
   not exist* (D103).
7. `DESIGN.md §6` and §7 updated; goldens re-taken last, one commit, one stated reason (D152).

---

## 12. Open — Joe's calls, and the things that want a running sim

**Design questions for Joe:**

- **⭐ How much better is a master than a novice?** This is the whole feel of the pillar. A narrow
  spread makes skill a footnote; a wide one makes a village of novices unviable and the founding
  brutal. **§3.2's reference rule holds either way** — this is choosing the *width*, not the
  centre.
- **⭐ How long is "a master"?** §2.1's own example says *twenty years in the fields*, against a
  working life of about fifty-five (twelve to a lifespan of 55–79). **Twenty is a third of a
  career and it feels right, but it is content, not derivation** — the same class of number as
  `farmhouse_seats` and `granary_feeds_people` (D165's split).
- **Does skill scale yield as well as duration** (§3.3), or duration alone in the first slice?
- **Should the apprenticeship policy be a village-wide slider or per-workplace?** §5.3 argues
  coarse; the professions panel is where a village-wide one would live.

**Tuning, which wants a running sim and must not be guessed:**

- The growth curve's shape, and where the reference sits on it.
- The decay rate, derived against `labour_reshuffle_years: 3`.
- How much an apprentice's growth is accelerated, and whether the master pays for it in output.
- Whether proficiency retained from a record is zero or a small floor
  (`tech-tree.md §12` asks the same question from the other side — **they must be answered
  together, or the two documents will disagree**).
