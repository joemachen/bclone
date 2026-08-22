# Spec / Catalog: Knowledge-Based Tech Tree

> Status: **design settled, unbuilt** · Owner: Joe + Claude Code · Pillar: `DESIGN.md §2.7`
> Format per `METHODOLOGY.md §2`. Written ahead of the build phase so there is settled content to implement against rather than content invented mid-implementation.
>
> **⭐ ITS SUBSTRATE IS NOW SPECCED: `specs/skills-catalog.md` (D173).** This document is written entirely on top of a proficiency model that **does not exist in the sim** — §3a's *"a record preserves the method, not the proficiency"* is the rule that stops this tree becoming a ratchet, and it cannot be implemented against nothing. That spec's **§6 is the contract** stating exactly what this one may assume exists: a per-villager per-skill integer proficiency, a stated reference level, years-in-practice, a `Recordable` flag, and a *who still knows this and how old are they* query. **It is queue item 2 and this is item 5** (`DESIGN.md §4`). ⚠️ **§12's open question about whether skill retained from a record is zero or a small floor is the same question `skills-catalog.md §12` asks from the other side — they must be answered together, or the two documents will disagree**, which is precisely what D159 spent a session unpicking.
>
> **This document is foundational guidance, not prescription.** It fixes the *shape* of the system — states, mechanisms, buildings, the rules that make it legible — and sketches the content that fills it. Node lists, prerequisites, and every number here are expected to change once the thing is running and testable. What should **not** drift without a recorded decision: the three knowledge states, the record-preserves-method rule, the hard library cap, and the agency levers. Those are load-bearing; the rest is furniture.

---

## 1. Goal

Give the village **progression without research points.** Advances are unlocked because a person knows a thing, or because the village has done a thing long enough, or because the settlement has become the kind of place where a thing is possible. Never because a counter filled up.

The tree and the population pyramid should be **the same object.** Looking at what the village knows should mean looking at who is alive.

---

## 2. Which pillars / non-negotiables this serves

- **§2.7 Knowledge-based tech tree** — the pillar itself.
- **§2.1 Villagers as agents** — knowledge lives in people; this is where that becomes progression rather than flavour.
- **Non-negotiable 1: Legibility.** Every unlock must produce one plain sentence naming who, and why, and when. An advance the player cannot account for is a bug.
- **Non-negotiable 5: Generational time as the core loop.** The school's payoff arrives after the founder is dead. That is the point, not a side effect.
- **Non-negotiable 6: Traceable over clever.** Seeded, explainable rolls; no hidden combination tables; no weighted scores nobody can read.
- **§2.3 Systemic pressure** — the tree must not become a ratchet, or it re-creates the dead late game it was meant to fix.

---

## 3. The core model: three states, not two

Every node is in exactly one state:

| State | Meaning | Lost when |
|---|---|---|
| **Unknown** | Nobody here has ever done it | — |
| **Known** | At least one living person knows it | The last knower dies without an apprentice |
| **Established** | Written down, or held by enough people that one funeral cannot erase it | The records are destroyed **and** the last knower dies |

The third state is the design. It makes "make this permanent" a goal the player works toward over decades, instead of a free consequence of unlocking.

### 3a. The rule that keeps it from being a ratchet

**A record preserves the method, not the proficiency.**

When the last master smith dies with the technique written down, the village does not lose steel. But the next person to open that record starts near-zero and needs years at the forge to reach where she was. A record converts a **catastrophic loss into an expensive setback**.

This is settled and load-bearing: *a written record never fully replaces a living knower.* It protects the late game permanently, because the skill half of every technique stays mortal no matter how good the library is.

> **⭐ REINFORCED, NOT WEAKENED, BY D176 — and `skills-catalog.md §5.4` is where the words were finally separated.** Joe asked how *"mastery is lost only from the person who dies"* squares with the library, and the answer is that **"mastery" was doing two jobs**. There are **three** distinct things and only the middle one is what this document is about:
>
> | | Held by | Lost when | Written? |
> |---|---|---|---|
> | **Proficiency** — Mabel's twenty-five years | one person | **she dies, always** | ⛔ never |
> | **Technique** — crop rotation, which she worked out *because* of those years | the village | last knower dies untaught and no record — §5's re-lock | ✅ → `Established` |
> | **Record of achievement** — *"this village once had a master farmer"* | the town hall's collections (§7f) | **never** | it *is* the writing |
>
> **So a record still preserves the method and never the proficiency.** The collections say *that it happened*; they never hand anybody years they did not work. ⛔ **And mastery-the-tier is not a node**: it is twenty years on the task, no knowledge state may gate it, and it therefore cannot re-lock. *A village can forget crop rotation. It cannot forget that people get good at things.*

### 3b. Unwritable knowledge

Some nodes are flagged **tacit** and can never be recorded. They pass person-to-person or not at all: a midwife's hands, an eye for soil, knowing when the fish run.

This exists so apprenticeship is never obsoleted by the school. It is also true, which is the best kind of game rule.

---

## 4. Unlock mechanisms

Eight, all diegetic. Each node names which one(s) apply.

| Code | Mechanism | How it reads to the player |
|---|---|---|
| **PEOPLE** | Knowledge lives in a person. A master develops the advance after long enough in the work. | *"Mabel has farmed these fields for twenty-five years. This spring she began resting one field in three."* |
| **DOING** | The village has practised the underlying thing at sufficient scale, for long enough. | *"The village has baked through eleven winters. The bakers have opinions about ovens now."* |
| **SCALE** | Civic gating. The settlement is large / permanent / long-lived enough for the thing to make sense. | *"Sixty souls and forty years in one place. Time to build properly."* |
| **SEREN** | Serendipity **with a prerequisite** — a seeded annual roll, available only to someone already deep in the practice. | *"Anke has fired pots for twenty-two years. This spring she tried a hotter fire."* |
| **IMPORT** | Knowledge arrives with a person. Two routes: a **migrant** who already knows it, or a **youth sent away** for years who returns with it. | *"Otto came back from Hallenbeck after six winters. He can read."* |
| **ADJ** | Two knowers working within the same catchment work something out neither had. | *"The wheelwright and the smith have been arguing about axles all summer."* |
| **CRISIS** | The failure teaches the technique. A brutal winter produces preservation; an outbreak produces quarantine. | *"They ate bark that winter. They have not stopped smoking fish since."* |
| **TERRAIN** | Some things are only *thinkable* in some places. No fall on the river, no watermill — ever. | Surfaced at map selection, never discovered thirty years in. |

### 4a. Constraints on the riskier mechanisms

**SEREN** must be seeded, deterministic, and produce a log line that names the person and their years in the work. A discovery the player cannot account for violates non-negotiable 1.

**ADJ** is constrained hard, or it imports a min-max layout puzzle into a game about watching people:
1. **A small hand-authored set** — five to eight named pairs, not a combinatorial system.
2. **Stated up front, never hidden.** It appears in the knowledge screen as a hint (*"a wheelwright working near a smith may work something out"*), so it is a puzzle the player can pursue rather than a combo they look up.
3. **Always a bonus route, never the only route to a node.**

**CRISIS** must never be net-positive. The technique is a partial recovery from a real loss, not a reward for failing.

**TERRAIN** gating means the map generator is also a difficulty generator. Unavailable branches must be stated in plain language at map selection: *"This valley has no fall on the river; milling will not be possible here."* Discovering it in year thirty is the "difficulty that reads as arbitrary" failure mode from §2.5.

---

## 5. Re-locking, and what it leaves behind

When the last knower of a **Known** node dies and there is no record, the node re-locks. Per `DESIGN.md §2.7` this must never be a funeral surprise — the at-risk state is surfaced continuously (§8).

**Lost arts leave wreckage.** A re-locked node does not simply grey out. It leaves physical evidence in the village:

- Tools in the shed that nobody can make another of, wearing out one by one.
- A building that still stands, still works, and cannot be repaired or rebuilt when it fails.
- Products in storage that will not be replaced.

Re-discovery is possible but costs more than the first time: the wreckage is a prerequisite hint, not a shortcut. This does the same job paving does in §2.6 — it fossilises a decision into the shape of the town.

---

## 6. Player agency

The player never picks a node from a menu. Choice lives in **allocation and commitment** — four levers:

| Lever | What it costs | What it buys |
|---|---|---|
| **Direct an apprenticeship** | A master's partial attention for years; a youth not fully productive | High-fidelity transfer of one technique to one person |
| **Adopt a practice as village policy** | Labour and disruption; **limited slots** | A technique one person knows becomes how the whole village works. Reversible. |
| **Send a youth away** | A worker gone for years, and they may not come back | IMPORT — knowledge the village could not reach alone |
| **Commit to writing it down** | A scribe *and* the master, off productive work for seasons; a shelf slot | Known → Established |

**Policy slots expand on civic scale** (population, longevity, permanence), **not** on library capacity. Keeping those two pressures on separate dials is deliberate: otherwise libraries become the one building that does everything.

---

## 7. Writing, and the three buildings

### 7a. Where literacy comes from

Two routes, deliberately asymmetric.

**The granary route (slow, certain, self-sufficient).** The granary is a *staffed* building whose job is counting. A keeper who has tallied stores for long enough begins marking sacks with signs of her own devising. Tally marks → notation → letters. Pure DOING.

> *Bertha has kept the granary's count for nineteen winters. This spring she began marking the sacks with signs of her own devising.*

The structural payoff matters more than the flavour: **the storage branch feeds the knowing branch.** The tree stops being parallel columns and becomes a web. And the player does not set out to invent writing — they set out not to starve, and writing is what a well-run granary eventually produces.

**The import route (fast, contingent).** A literate migrant settles, or a youth sent away returns able to read.

Keep both. The asymmetry is the point.

### 7b. Scriptorium — where recording happens

This is where *commit to writing it down* gets its teeth. Recording one technique is a **project measured in seasons**, occupying both a literate scribe **and** the master who knows the thing. Both off productive work for the duration.

There is no button that instantly preserves a technique. You spend a year of your best farmer's life having her dictate. **The opportunity cost is the mechanic.**

### 7c. Library — where records are stored

- **Hard capacity.** One record per node, no bundling. A library of N shelves holds N techniques, and choosing which N is the whole point. To hold more, build more libraries.
- **Needs a keeper**, or records degrade.
- **Burns.** Fire stops being flavour and becomes the most consequential random event in the game — one bad night can undo three generations of recording.
- **Upgradeable** (timber → stone → vaulted), buying down decay and fire risk.

**Emergent consequence worth protecting:** because capacity is per-building and buildings burn, **copying a record into a second library** becomes something a player would deliberately do. It costs a shelf slot in each place and a scribe's season, and it survives one fire. The sensible layout is libraries kept *apart*, not one grand central archive.

That is monastic manuscript culture arriving from two rules with no special-case system. It also gives the late game a permanent sink for scribe labour that is not "unlock more stuff" — it is **insurance**, which is exactly right for a game with no win condition.

The tension to preserve: *one big library is efficient and fragile; three small ones are wasteful and durable.*

### 7d. School — where records become people again

A library nobody reads is furniture. The school converts records back into knowers, and it is the strategic fork of the whole system:

| | Apprenticeship | School |
|---|---|---|
| Ratio | 1 master → 1 youth | 1 teacher → many children |
| Fidelity | High — full skill over years | Low — the basics only; years on the job still required |
| Requires | Two people | Literacy + records + a building + a library in reach |
| Fails when | The pair breaks | The library burns |

So: **a village of masters** (deep, high output, fragile) versus **a village of schooled generalists** (shallow, resilient, lower peak). Mutually exclusive in practice, because both consume the same scarce thing — the best people's time.

**Two costs make the school a real decision:**

1. **Teachers are your best people, removed from production.** Mabel teaching eight children is not farming. A permanent tax on peak performers.
2. **Literacy is its own prerequisite, and children need it first.** The first school generation produces no craft benefit at all — it produces *readers*. A two-to-three-decade investment that pays out after the player who started it is dead.

That second cost is the generational-time pillar expressed as a single building.

### 7e. Placement rule: school requires a library in catchment

The one place ADJ is **structural** rather than bonus. Enforced at placement, using the existing shared cost field (`TravelCostField`) — no new system.

**UI behaviour (Joe, settled):**
- While placing a school, the marker is **red** when no library is within catchment, and the placement is **refused**.
- The marker turns **green** inside a valid catchment, and placement is allowed.
- A tooltip states the reason in plain language rather than making the player infer it from the colour.

The consequence is that the knowledge quarter of the town becomes a real place with a real footprint.

### 7f. ⭐⭐ Town hall — where the village reads its own memory (D176, Joe)

**The fourth building, and the only one that does not touch a technique.** The scriptorium
**writes**, the library **stores**, the school **teaches** — and the town hall is where the
village **reads what it has become**. Joe: *"added to the 'collections' tab of the town hall along
with all of the other unlockables… I wonder if building the town hall is what unlocks the tech
tree?"*

**⛔ IT GATES THE SCREEN, NOT THE TREE — and that distinction is the whole design.**

- **The village goes on learning with or without one.** Gating the *tree* behind a building the
  player chooses to raise would convert DOING, CRISIS and SEREN from emergence into a menu
  unlock, and **§7a's best argument would die with it**: *"the player does not set out to invent
  writing — they set out not to starve, and writing is what a well-run granary eventually
  produces."* A granary keeper who invents notation must not be waiting on a civic permit.
- **It would also flatten the early game**, which is the mid-game gap (`DESIGN.md §4`, D161)
  made worse rather than better.
- **Before a town hall, nothing is invisible**: every discovery, apprenticeship, mastery and
  at-risk warning is **narrated on its edge in the village log** as it happens (§8, D123, D147).
  The player hears their history.
- **After it, the whole roster is browsable** — who knows what, who is the best knower, what is
  written and where, what is at risk. **The progression is *anecdote → archive***, and that is a
  real payoff for a civic building rather than a tax on progression.

**Singleton** (D38; `building-placement.md` lists the town hall as *the* example of a build-once
building). It is also where Joe's **charts** live (`DESIGN.md §4`), which is the same idea one
level up: the town hall is the building whose product is *information about yourself*.

#### 7f.1 The collections — and why they are permanent

Every crop, tree, fish, animal, technique and **first master** the village has ever met. Joe's
Animal-Crossing museum note (`DESIGN.md §4`) lands here rather than as a separate building,
because `buildings-plan.md` already says what a town hall is: *"Records, census, lineage. **Not a
stats screen** — the place where the village's memory is kept."*

**⛔ THE COLLECTIONS GRANT NOTHING, AND THAT IS LOAD-BEARING RATHER THAN A CHOICE OF SCOPE.**
This is the one place Joe's *"they can't ever lose it"* had to be handled with care: applied to
**capability**, permanence breaks three of the four guards §11 uses against the ratchet — hard
shelf capacity, decay and fire. Applied to a **record of what happened**, it breaks nothing.

- A collections entry is **memory, not machinery.** It says the village once had a master farmer;
  it does not make the next farmer better.
- **A lost technique still shows in the collections** — *we knew this once* — which is `§5`'s
  wreckage rule expressed as a sentence instead of a ruined building. **That is the best thing
  the collections do**: they are how a village remembers what it can no longer do.
- ⛔ **The day an entry confers a bonus, this becomes the ratchet §11 exists to prevent.** Written
  down on both sides (§11, and `skills-catalog.md §5.4`) so nobody later "improves" it into one.

---

## 8. Legibility: the knowledge screen

**The tree is not a graph of icons to click.** It is a roster of what the village knows and who knows it. If the tree and the population pyramid are the same object, the interface should make that literally true.

> **⭐ THIS SCREEN IS THE TOWN HALL'S INTERIOR (§7f, D176), and it is reachable only once one stands.** Before that the village log carries the same information as it happens, one line per event on its edge — so **nothing is ever hidden, it is simply not yet collected**. *Anecdote → archive.* ⚠️ **The at-risk line (below) is the exception and must narrate from day one**, town hall or no town hall: §2.1 requires knowledge-at-risk to be *visible and actionable*, and a warning nobody can see is the funeral surprise this design refuses.

One row per node:

| Column | Shows |
|---|---|
| Technique | Name, branch |
| State | Unknown / Known / Established |
| Known by | **Faces**, with names and ages |
| Best knower | Name, age, years in the work |
| Written? | No / Recorded (which library, condition) / Tacit — cannot be written |
| At risk | The warning line |

The at-risk line is what stops re-locking from feeling unfair:

> **Mabel, 68, is the only soul who knows herbalism. Herbalism cannot be written down.**

That must be a standing, visible, actionable state — surfaced years ahead of the funeral, not discovered at it.

---

## 9. Branch catalogue (sketch — expect revision)

**Shape: broad, not tall.** Eight trunks, roughly four to six deep. No final node. The mutual exclusions come from **land and labour competing**, not from a "pick one" prompt: pasture and cropland want the same cleared ground; charcoal and managed forest want the same trees; every hand at a kiln is a hand not foraging. You do not pick a branch — you drift down one and notice thirty years later that you cannot afford the other.

### 9.1 Ground — soil and crops

| Node | Unlocked by | Notes |
|---|---|---|
| Foraging | — | Starting practice |
| Tended patches | DOING | Working the same forage sites for years |
| Sowing | DOING + PEOPLE | |
| Crop rotation | PEOPLE | The `DESIGN.md §2.7` worked example |
| Manuring | ADJ (herder + farmer) | |
| Fallowing | CRISIS or PEOPLE | Typically taught by soil depletion biting |
| Drainage | TERRAIN (wet ground) + SCALE | |

### 9.2 Woods

| Node | Unlocked by | Notes |
|---|---|---|
| Felling | — | Starting practice |
| Coppicing | DOING | |
| Managed forestry / replanting | DOING | *Log a stand for two generations* (§2.7 example) |
| Charcoal burning | ADJ (woodsman + kiln) | Competes with managed forest for the same trees |
| Orchard | PEOPLE + SCALE | Pays out over decades |

### 9.3 Herd

| Node | Unlocked by | Notes |
|---|---|---|
| Hunting | — | Starting practice |
| Trapping | DOING | |
| Penning | SCALE | |
| Husbandry | PEOPLE | |
| Dairying | PEOPLE | |
| Draught animals | ADJ (herder + builder) | Feeds hauling and the road branch |

### 9.4 Fire and materials

| Node | Unlocked by | Notes |
|---|---|---|
| Hearth | — | Starting practice |
| Kiln / pottery | DOING + TERRAIN (clay) | SEREN candidate — the hotter fire |
| Lime burning | TERRAIN (limestone) | |
| Smelting | TERRAIN (ore) + IMPORT | |
| Forge / iron tools | PEOPLE | Tools are real stockpiled resources (D17) |
| Steel | PEOPLE + ADJ | |

### 9.5 Keeping — preservation and storage

| Node | Unlocked by | Notes |
|---|---|---|
| Drying | DOING | |
| Salting | TERRAIN (salt) or IMPORT | |
| Smoking | CRISIS | The bark winter |
| Root cellar | SCALE | |
| **Granary** | SCALE | **Feeds the Knowing branch — the literacy route** |
| Icehouse | TERRAIN + SCALE | |

### 9.6 Building and ground works

| Node | Unlocked by | Notes |
|---|---|---|
| Timber frame | — | Starting practice |
| Stone footing | SCALE + TERRAIN (stone) | Permanence milestone |
| Mortar | ADJ (lime burner + builder) | |
| Watermill | TERRAIN (fall on river) | The headline terrain gate |
| Bridge | SCALE | |
| Paving / auto-pave policy | SCALE | The civic tier §2.6 already anticipates |

### 9.7 Bodies

| Node | Unlocked by | Notes |
|---|---|---|
| Midwifery | PEOPLE | **Tacit — unwritable** |
| Herbalism | PEOPLE | **Tacit — unwritable** |
| Clean water / well | CRISIS + SCALE | |
| Quarantine practice | CRISIS | Taught by an outbreak |

This branch is deliberately the most tacit-heavy. Health knowledge should be the most fragile thing the village holds.

### 9.8 Knowing

| Node | Unlocked by | Notes |
|---|---|---|
| Tally-keeping | DOING (granary keeper) | |
| Letters | DOING (from tally) **or** IMPORT | Two routes, asymmetric by design |
| Scriptorium | SCALE + Letters | |
| Library | SCALE | Hard capacity; burns |
| School | Library in catchment | |
| Formal apprenticeship | SCALE | Institutionalises the lever |
| Contracts / regional trade | IMPORT + SCALE | Hands off to §2.4 |

---

## 10. Milestones

The events that should produce a log line worth screenshotting. Not mechanics — punctuation.

**⭐ A log line first, a collections entry second** (§7f.1, D176). Every one of these narrates on
its edge when it happens, whether or not a town hall stands; the town hall is where they are kept
together afterwards.

- **First winter nobody went hungry.** The camp becomes a village.
- **⭐ First master.** Twenty years on one task (`skills-catalog.md §3.3b`) — *"Hattie has farmed
  these fields for twenty years. There is nothing about this ground she does not know."* **Joe
  asked for this line by name**, and it is the earliest of these the player will see.
- **First apprenticeship completed.** Knowledge becomes heritable at all.
- **First person to live an entire life here, birth to old age.** A generation closed.
- **First stone building.** The settlement now outlives its founders.
- **First technique written down.** Mortality stops being able to take everything.
- **First field left fallow / first stand replanted.** The village plans past one lifetime.
- **First surplus sent outward.** You become a node in the region.
- **A lost technique recovered.** Proof the village is resilient, not merely lucky.

---

## 11. Failure modes to design against

| Failure | Symptom | Guard |
|---|---|---|
| **Ratchet / dead late game** | Once everything is written, tension ends | Record = method not proficiency; hard cap; decay; fire |
| **⭐ The collections become a ratchet** (D176) | A permanent, never-lost list starts conferring permanent, never-lost benefits | **They grant nothing — they are memory, not machinery** (§7f.1). Permanence is only safe because it buys nothing. Stated on both sides, here and in `skills-catalog.md §5.4`, so it is not "improved" into one later |
| **⭐ Mastery gated by knowledge** (D176) | A villager works twenty years and is told they may not be a master because the village forgot something | **Mastery-the-tier is not a node and no knowledge state may gate it** (`skills-catalog.md §6.7`). Techniques re-lock; competence does not |
| **The library is mandatory** | Writing everything down is always correct, so it is not a decision | Hard capacity; scribe *and* master off work for seasons; tacit nodes that cannot be written at all |
| **Re-lock feels unfair** | Player loses a technique they had no warning about | The at-risk line (§8), surfaced years ahead |
| **Emergence without agency** | The tree becomes weather — it happens *to* the player | The four levers (§6). Choice is allocation and commitment. |
| **Adjacency becomes a layout puzzle** | Players optimise building placement over watching people | Small authored set; stated up front; never the only route |
| **Crisis rewards failure** | Players deliberately starve people to learn preservation | Never net-positive; always partial recovery from real loss |
| **Terrain gate reads as arbitrary** | "Why can't I build a mill?" in year thirty | Stated in plain language at map selection |
| **Opaque discovery** | An unlock the player cannot account for | Every unlock writes one sentence naming the person and the reason |

---

## 12. Open — tuning, not design

Deliberately unspecified. These need a running sim to answer, and inventing numbers now would be false precision.

- Starting library capacity, and shelves per upgrade tier.
- Seasons to record one technique; how much of a master's output it costs.
- SEREN annual probability, and the years-in-practice threshold that opens it.
- School throughput: children per teacher, years to literacy, fidelity of transfer versus apprenticeship.
- Record decay rate; keeper labour required to hold it steady.
- Fire probability per library tier — high enough to matter, low enough not to feel punitive.
- Re-discovery cost multiplier for a lost art with wreckage present.
- How many policy slots at which civic thresholds.
- Whether skill retained from a record is zero or a small floor. ⚠️ **Bounded by D176: it is at most a *floor*, never restoration** — `skills-catalog.md §6.6` guarantees no record, school or policy hands anybody years they did not work. The same question is asked from the other side in `skills-catalog.md §12`, and **the two must be answered together or the documents will disagree** (D159).

---

## 13. Testing

Sim logic is pure and deterministic; exploit it (`METHODOLOGY §3`).

- **Determinism:** same seed, 200 years, identical knowledge state and identical unlock log. Extends the P0 test.
- **Re-lock:** last knower of an unwritten node dies ⇒ node re-locks, wreckage is created, and the at-risk warning fired *before* the death.
- **Record semantics:** last knower of a *written* node dies ⇒ node stays Established, and the next learner starts at low proficiency, not the dead master's.
- **Tacit nodes:** cannot be recorded by any path. Assert the scriptorium refuses them.
- **Hard cap:** a full library refuses an eleventh record; a second library accepts it.
- **Fire:** burning the only library holding a node whose knowers are all dead ⇒ node re-locks. Burning one of two copies ⇒ it does not.
- **School placement:** rejected outside library catchment, accepted inside, using the shared cost field.
- **SEREN:** fires only for villagers over the practice threshold; identical across two same-seed runs.
- **Anti-vacuity:** a run with no apprenticeships and no records must actually lose techniques. If nothing is ever lost, the system is decorative.
- **Legibility:** every unlock and re-lock emits exactly one narrative line naming the person.

---

## 14. Definition of Done

1. This spec current and reconciled with what was actually built.
2. Node content in **data files**, not code — a modder can add a branch (`DESIGN.md §3`).
3. Unit tests above passing; determinism test still green.
4. Manual QA: play a village through a knowledge loss and a recovery, and read why it happened without opening the code.
5. No new errors in the log across a clean 200-year playthrough.
6. `DESIGN.md` Progress Tracker + Decisions Log updated.

---

## 15. Proposed Decisions Log entries

For `DESIGN.md §7`, numbered from D29 — renumber to fit whatever is current.

- **D29 · Knowledge has three states, not two: Unknown, Known, Established.** Redundancy (more knowers) and durability (records) are made genuinely different mechanisms with different costs and different failure modes, rather than one being a better version of the other.
- **D30 · A written record preserves the method, not the proficiency.** Joe's call. A record converts a catastrophic loss into an expensive setback; it never fully replaces a living knower. This is what keeps the tree from becoming a ratchet and re-creating the dead late game §2.3 exists to fix.
- **D31 · Library capacity is a hard cap; more knowledge means more libraries.** Joe's call, over soft decay. Chosen because "what is worth preserving?" is a better question than "is this record still legible?", and because a hard cap plus destructible buildings makes *copying a record to a second library* a decision the player arrives at themselves. One record per node, no bundling.
- **D32 · Literacy emerges from the granary.** Joe's call. A keeper who has tallied stores for long enough invents notation. Makes the storage branch feed the knowing branch, so the tree is a web rather than parallel columns — and the player reaches writing by trying not to starve, not by setting out to invent writing. The import route (migrant, or a returning youth) is retained as the fast-but-contingent alternative.
- **D33 · Adjacency is a small authored set, stated up front, and never the only route to a node.** Constrained deliberately so it rewards a thoughtfully laid-out town without importing a min-max layout puzzle into a game about watching people.
- **D34 · Policy slots are gated on civic scale, not library capacity.** Keeps two pressures on two dials and stops the library becoming the building that does everything.
- **D35 · A school requires a library within catchment, enforced at placement.** Red marker and refused placement outside, green inside, with a plain-language tooltip. The one place adjacency is structural rather than bonus. Uses the existing shared cost field — no second travel system (§2.6).
- **D36 · The map seed becomes a seed *string*, amending D18.** A Foundation-style map creator (archetype + weighted sliders) means a bare number cannot reproduce a hand-tuned map. The shareable string packs seed + archetype + slider values. Terrain-gated unlocks make the map creator a difficulty creator, so unavailable branches must be stated in plain language at map selection.
