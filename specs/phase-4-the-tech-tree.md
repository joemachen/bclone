# Spec: Phase 4 — the tech tree

> Status: **OPEN — Joe lifted the D205 hold on 2026-08-26 (*"start"*).** Owner: Joe + Claude Code ·
> Pillar: `DESIGN.md §2.7` · Format per `METHODOLOGY.md §2`.
>
> **⭐ This is the phase plan and the QA checklist in one document, and the checklist exists from
> day one on purpose.** Phase 3 shipped without one and its walk was **waived rather than
> performed** (D203) — *"if a Phase 3 regression ships, that is where it got through."* **That debt
> does not compound.** §5 is written before the first line of code, so it can be walked rather than
> invented at merge time.
>
> Neighbours: `tech-tree.md` (the design, settled and unbuilt), `skills-catalog.md §6` (**the
> contract, and all five items are built**), `school-and-education.md`, `content-inventory.md`
> (findings 3, 5 and the audit), `TECH-EXAMPLE.md` (Joe's 39 techniques).

---

## 1. ⛔ The thing to get right first: what this phase is actually gating

**`content-inventory.md` finding 5 is the substantive objection to building this now, and it has to
be answered rather than routed around.** Two roadmaps disagree:

| Source | Where knowledge sits |
|---|---|
| `DESIGN.md §4` | **Phase 4 — next** |
| `buildings-plan.md §10` | **Step 8 of 11**, behind food breadth, cemetery, preservation, crop and pasture zones, forestry, stone, and iron and tools |

**And the audit's own argument is sharp: 18 catalogue rows carry a knowledge flag and none of those
18 buildings exist.** A tree built to gate buildings would today gate **ten buildings and six
trades**, most of which `buildings-plan.md` classifies as Founding-tier and therefore **ungated by
design.** *There would be almost nothing to gate.*

### ⭐⭐ The resolution: the first content is NOT a gate on a building. It is a technique that makes an existing trade better.

**This is D196, and it is Joe's own model, stated concretely a month ago:**

> *A master woodcutter works out "splitting lumber in a way that gives more cords" — **+15% firewood
> per log**. The technique enters the library's records when he reaches mastery. When he dies **his
> proficiency dies with him but the technique does not**, and the next woodcutter spends idle time
> in the library learning it.*

**Nothing in that needs a building that does not exist.** It runs against the **six trades that
already ship**, the proficiency substrate Phase 3 built, and the mastery event that already fires.
**The three states become real with zero new content-tier buildings**, and `buildings-plan.md §10`'s
*one hard dependency* — *"knowledge before re-locking is punishing"* — is satisfied inside this
phase rather than eight steps away.

> **⛔ So the two roadmaps are reconciled rather than one being declared the winner:** the *gating*
> half of the tree genuinely does want the T1/T2 buildings first, and **that half is not in this
> phase.** What is in this phase is the half that needs nothing but people — which is also the half
> `DESIGN.md §2.7` calls the pillar (*"knowledge literally lives in people"*).
>
> **Record this in `buildings-plan.md §10` and `content-inventory.md` finding 5 when it lands, or
> the disagreement outlives the decision** — which is D159's failure exactly.

---

## 2. ⭐ Four design calls, decided here with reasons, and every one of them Joe's to overrule

These were about to become four questions. **Each has enough recorded evidence to decide, and Joe
said *"start"* rather than *"ask me things"* — so they are decided, stated loudly, and cheap to
reverse.**

### 2.1 What a technique DOES: a yield effect on the trade. ⛔ The mastery-gain half is deferred.

D196 names two effects: **+15% firewood per log** and **+5% mastery gain**. **Ship the first, defer
the second.**

**Why the second is different in kind, and the handoff already flagged it:** a technique granting
*"+5% mastery gain"* is **a soft ratchet on proficiency itself, one level up** — the only piece of
the model that touches `§3a`'s anti-ratchet rule rather than sitting beside it. **It is bounded and
probably fine, and "probably fine" is not how this project treats the rule that protects the late
game.** It wants its own measurement, after the loop is running and can be measured against.

⚠️ **The yield effect must BITE when it ships, not accrue silently.** D177's rule for Phase 3 —
*mastery bites, gated by nothing* — is what kept that phase from shipping **D56's shape: a system
that accrues, is visible, and changes nothing.** The same rule binds here.

### 2.2 The library IS a building. (`content-inventory.md` finding 3, resolved on recency.)

Three sources, **two of them Joe's own words at different times**:

| Source | Says | Age |
|---|---|---|
| `tech-tree.md §7c` | Its own building — hard cap, keeper, upgrades, and it burns | older |
| `buildings-plan.md §6` cut list | *"the library is the room the scriptorium's output lives in. **Not a building.**"* | **older** |
| **D196 (Joe)** | *"the next woodcutter can spend idle time **in the library** learning it"* | **newest** |

**The cut list's reasoning is now void on its own terms:** it cut the library *because* it was a
room inside the scriptorium — and **D204 took the scriptorium off the critical path entirely**
(recording is automatic at mastery). A room inside a building that is no longer being built is not a
room. **The newest statement is also the only one still standing on live premises.**

⭐ **And it has to be a building for the phase to have a decision in it.** `§11`'s guard against
*"the library is mandatory"* rested on three costs; **D204 deleted one (the scriptorium's
opportunity cost), so the hard shelf cap is carrying that guard nearly alone.** A cap only means
something if it belongs to a thing you build, site, fill and can lose.

### 2.3 Proficiency retained from a record is ZERO, not a floor.

⚠️ **`tech-tree.md §12` and `skills-catalog.md §12` both ask this, and both say explicitly it must be
answered in both places at once or the documents will disagree** — D159's failure mode, pre-labelled.
**So it is answered in both, in the same commit, or not at all.**

**Zero**, for three reasons: it is the stronger reading of D176's *"at most a floor, never
restoration"*; it is what `§3a` already promises in plain words (*"the next person to open that
record starts near-zero"*); and **a floor is a tuning knob that can be added later with a
measurement behind it, whereas removing one would move goldens for a reason nobody could name.**
*Start at the end of the range the rule allows, and let a probe argue it back.*

### 2.4 Which techniques ship: a small set on the trades that already exist.

⛔ **No new building-tier content.** Candidates already written down and attached to live trades —
the non-★ nodes of `tech-tree.md §9`, plus D196's own example:

| Technique | Trade | Mechanism | Effect |
|---|---|---|---|
| Splitting lumber (D196's example) | woodcutter | PEOPLE | more firewood per log |
| Coppicing | forester | DOING | more logs per stand, or faster regrowth |
| Crop rotation | farmer | PEOPLE | `DESIGN.md §2.7`'s own worked example |
| Tended patches | forager | DOING | more food per trip |

⚠️ **Every number is a PROPOSAL until a run produces it** — `tech-tree.md §12`'s standing refusal,
and *if a number goes into a document, it comes from a run.* **The set is deliberately four, not
thirty-nine**: the thirty-nine are attached to buildings that do not exist, and a phase that ships
four working techniques is a phase you can stop after.

---

## 3. Slices

Each is a thing you could stop after, in the house style.

### Slice 1 — a technique is a row, and the three states are real
`TechniqueRow` + `TechniquesCatalog` in data — **the sixth application of D168's discipline**, and
it follows `goods-catalog.md`, `jobs-catalog.md` and `buildings-catalog.md` without re-arguing the
shape. Techniques attach to **skills**, which exist. Three states hashed and deterministic. A master
works one out (PEOPLE), and it **bites** (§2.1). **Nothing is written down yet** — so a technique
dies with its last knower, and the anti-vacuity case is the default.

⭐ **That is deliberately an unhappy ending, and it is the right first slice**: it makes the loss
real before the remedy exists, so the library in slice 2 is answering a pressure the player has
felt. It is `§0.1`'s pattern — *the pressure and its remedy shipping close together* — with the
pressure first by one slice, not by a phase.

### Slice 2 — the library, and Known becomes Established
The building (§2.2), a **hard shelf cap**, automatic recording at mastery when a shelf is free
(D204), and **a full library that refuses the record and says so** — which `§11` now leans on almost
alone. The next worker learns the method from the record and **starts at zero proficiency** (§2.3).

### Slice 3 — the knowledge screen, and re-lock the player can see coming
`§8`'s screen, and the at-risk line already exists (`SimWorld.KnowledgeAtRiskNote`, D195) — this is
where it stops being one villager's panel row and becomes the village's memory. ⛔ **No unlock the
player cannot account for** (`§11`, non-negotiable 1).

### ⏸️ Explicitly NOT in this phase
The scriptorium (D204 took it off the path), literacy, the school (specced, `school-and-education.md`,
wants its own slice), the town hall and collections, fire, record decay, SEREN, ADJ, CRISIS, IMPORT,
TERRAIN, and the 39 building-tier techniques. **Named so they are not smuggled in.**

---

## 4. Definition of Done

Per `METHODOLOGY.md §3`, and item 4 is the one this phase is not allowed to waive.

1. `tech-tree.md` and this spec current and **reconciled with what was actually built**.
2. Node content in **data files** — a modder can add a technique (`DESIGN.md §3`).
3. Unit tests passing; **determinism green**; goldens moved once, deliberately, with a stated reason.
4. **⛔ THE QA WALK IN §5, PERFORMED AND TICKED.** Not *"the checklist is good"* — D164 and D168 both
   refused that, and D203 waived the walk itself. **The document and the walk are different things.**
5. No new errors in the log across a clean 200-year playthrough.
6. `DESIGN.md` Progress Tracker + Decisions Log updated; `buildings-plan.md §10` and
   `content-inventory.md` finding 5 reconciled (§1).

---

## 5. ⭐ The QA checklist — written before the code, walked before the merge

> **How to walk it:** start a fresh valley, play at 1× or 2×, and **do not fast-forward past a
> death.** The whole phase is about what happens when somebody dies, so the deaths are the test.
> ⚠️ **A century is the unit** — mastery is twenty years and re-locking needs a master to die of old
> age, so anything shorter cannot reach the loop.

### 5a. A technique arrives, and you can account for it

| # | Check | Result |
|---|---|---|
| 1 | A master reaching twenty years works something out, and **one log line names the person, the trade and the years** | |
| 2 | The line reads as a person doing something, not as a node unlocking | |
| 3 | Nothing arrives that the log did not announce | |
| 4 | The technique **visibly changes output** — the trade produces more, and you can see it in the stores | |
| 5 | Two runs of the same seed produce the same techniques, in the same years, to the same people | |

### 5b. It dies with them, and you saw it coming

| # | Check | Result |
|---|---|---|
| 6 | The at-risk warning fires **years before** the last knower dies, and names them | |
| 7 | The warning is actionable — it says what to do about it, not merely that it is happening | |
| 8 | When the last knower dies unwritten, **the technique re-locks and one line says so** | |
| 9 | The village's output drops correspondingly, and the drop is attributable | |
| 10 | ⛔ **No funeral surprise:** nothing is lost that was not warned about first | |

### 5c. The library is a decision, not a formality

| # | Check | Result |
|---|---|---|
| 11 | Placing a library is refused or warned about in a **sentence**, like every other building | |
| 12 | A technique is recorded automatically at mastery, and the log says which shelf it took | |
| 13 | **A full library refuses the record and says so** — and the sentence tells you to build another | |
| 14 | Choosing *which* techniques get shelves is a decision the player can actually make | |
| 15 | A recorded technique **survives its last knower's death** — the node stays Established | |
| 16 | The next worker learning from the record starts at **zero proficiency**, and the panel shows it | |
| 17 | ⭐ **A record never hands anybody years they did not spend** — verified by reading a villager's panel, not by trusting the code | |

### 5d. The whole, over a century

| # | Check | Result |
|---|---|---|
| 18 | Play a hundred years and **want to keep watching** — Phase 3's success test still holds | |
| 19 | A village that never builds a library **visibly loses things**, and it is legible why | |
| 20 | A village that builds one **keeps some and loses others**, and the choice was the player's | |
| 21 | The knowledge screen answers *"what does this village know, and who knows it?"* at a glance | |
| 22 | Nothing in the log is an error or a warning across the run | |

---

## 6. Success test

**Borrowed from Phase 3's, because it is the same claim one level up:** play a village through
**a knowledge loss and a recovery**, and be able to say **who** was lost, **what** went with them,
and **what the village did about it** — without opening the code.

⛔ **If the answer is "a node re-locked", the phase has failed** even with every test green. The
answer has to be a person's name.
