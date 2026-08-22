# Spec: Phase 2 — the village you can play

**Decisions:** D159 (which named this phase for what it became), D161/D162 (crops, its last slice),
D163/D166/D169 (the jitter, three causes deep). **Status:** ✅ **WALKED. All five Definition-of-Done
items are met** — Joe walked this checklist on 2026-08-22 (D169) and approved the document, which
was the last one open. What he found while walking it is D168's four rulings, and those are
Phase 2 polish rather than checks that failed.

> **⚠️ THIS DOCUMENT EXISTS BECAUSE PHASE 1 HAD ONE AND PHASE 2 DID NOT.** METHODOLOGY §3
> requires a manual playthrough against a **written** checklist before a phase merges, and
> `phase-1-households-and-labour.md §8` has one — *"written down so it is repeatable rather than
> remembered"*. Phase 2 built roughly ten times as much and had nothing. D159 caught that; this
> closes it.

---

## 1. What this phase turned out to be

Phase 2 began as *wood as fuel* and grew, on Joe's calls, into **everything the player touches**.
`DESIGN.md §4` tells the story; the short version is that this is the phase where the player
stopped watching and started deciding.

That is what the checklist has to test. **Not "does it work" — does it answer.** §1.1 is the
first Non-Negotiable and the only one a QA pass can really measure: *the player must always be
able to trace why something happened.*

---

## 2. ⛔ How to walk it

**Joe walks this, not Claude** (`DESIGN.md §4`, DoD item 3). That is not ceremony: since D160 the
view has **no automated verification of any kind** (METHODOLOGY §6), so his eyes are the test —
and nine of the ten bugs in the cold start surfaced because he played it, against three wrong
diagnoses reasoned from the code the same evening.

```bash
run.bat
```

**Controls:** space to pause · 1–4 speed · WASD pan · wheel zoom · tab routes · home recentre ·
c fold panels · h hide them.

**Before starting, note the log path from the header.** It is beside the seed, and together they
are what reproduces and explains a run (METHODOLOGY §4). **D154, D157 and D163 were all diagnosed
from that file and none of them from the suite** — so if something looks wrong and is hard to
describe, the log line is worth more than the description.

**A check fails if the answer is not on the screen.** "I know why because I remember the code" is
a fail. The whole claim of this phase is that the game explains itself.

---

## 3. The checklist

### 3a. The opening — the first ten minutes (D70–D82, D119, D157)

| # | Check | Result |
|---|---|---|
| 1 | Four founders arrive in an empty valley with a cart, and nothing is built for them | |
| 2 | The valley reads at a glance: woodland, water, stone and iron seams tell apart | |
| 3 | Marking a builder's hut is free and instant, and the village says so if you mark anything before you have one | |
| 4 | A gatherer's hut sited **in real woodland** stands, gets staffed, and feeds people | |
| 5 | The first winter is survivable and the schedule is legible — you can see the firewood coming before you need it | |
| 6 | Nothing kills a founder for a reason that was invisible beforehand | |

> **⚠️ Check 4 is the one with history.** A hut in woodland was fatal until D157 — the footprint
> was never cleared, because the nearest painted tile was always closer. It is the single most
> load-bearing line in this section.

### 3b. Building, and the queue (D38, D42, D43, D100–D105, D108)

| # | Check | Result |
|---|---|---|
| 7 | Every refusal to build is a **sentence**, not a red outline — and it names the actual reason | |
| 8 | A site that is merely far is **allowed** and warned about, not refused | |
| 9 | A marked site says where it stands in the queue and what is immediately ahead of it | |
| 10 | ▲ Sooner / ▼ Later visibly change which building goes up first | |
| 11 | A site waiting on trees says so, and the village clears them without being told twice | |
| 12 | Demolishing returns half the logs and says what was lost | |

### 3c. Work, and why this person (D2.2, D51, D106, D112, D120, D148)

| # | Check | Result |
|---|---|---|
| 13 | Clicking any villager answers **"why this job?"** — naming the place, the distance, and the runner-up | |
| 14 | A villager with no work names the constraint that excluded them | |
| 15 | Nobody can be assigned by hand, and no control affords it | |
| 16 | The professions panel's numbers reconcile: what you asked for, who turned up, and the laborer count add up | |
| 17 | A long commute says so **on the villager** rather than being silently forbidden | |
| 18 | Setting a profession number visibly overrides what the village would have chosen | |

### 3d. Goods, and where they are (D30–D36, D96, D132, D140, D141, D144)

| # | Check | Result |
|---|---|---|
| 19 | Goods only move because somebody carried them — no total ever changes with nobody walking | |
| 20 | A full store is marked on the map, and the marker can be switched off per building and globally | |
| 21 | A store set to logs-only **actually refuses** firewood — watch a villager put it down rather than in | |
| 22 | Goods refused by a full store become a visible heap, and somebody eventually fetches it | |
| 23 | The Overview's food figure and what you can see in the buildings agree | |
| 24 | Switching the market off costs convenience and not lives | |

> **⚠️ Check 21 is D144 and check 22 is D96**, and both shipped as *predicates that were never
> tested at their deposit*. Watch a villager arrive, not a tooltip.

### 3e. Limits, and the player's ceiling (D62, D128, D139, D145, D147)

| # | Check | Result |
|---|---|---|
| 25 | A stock limit stops the **work**, not just the hiring — the woodcutter stops splitting | |
| 26 | A row with no limit set says *"no limit"* rather than showing a default number as though it were a rule | |
| 27 | A building idle because of a limit set two windows away **says which limit**, and takes the idle ring | |
| 28 | A limit set below the survival floor is obeyed, and the village says what it will cost | |
| 29 | The idle ring stays **silent** for a gatherer in winter, a hut you emptied on purpose, and a construction site | |

### 3f. The year (D44, D45, D49, D53, D162, D163)

| # | Check | Result |
|---|---|---|
| 30 | The four seasons are mechanically distinct — you can tell which one it is without reading the date | |
| 31 | A farmhouse can be placed, given ground with the work-ground brush, and its field is **ploughed where you painted** | |
| 32 | The field changes visibly through the year: bare → sown → standing ripe → bare | |
| 33 | The harvest is reaped and reaches the granary; the farm's own store fills first and the walk lengthens after | |
| 34 | A crop nobody reaps is mourned **once**, in autumn while it can still be acted on, and again when winter takes it | |
| 35 | ⛔ **Nobody bounces between two tiles.** Watch a cold villager go in to get warm — they stay in until they are warm | |
| 36 | Cold reads as a place you are standing, not a number attached to your family | |

> **⛔ Check 35 is D163 and is the one Joe watched go wrong for months.** A villager carrying
> logs used to be flipped straight back out of the house on the tick after arriving. It is fixed;
> this is the check that says whether it *looks* fixed.

### 3g. The shell, and the pace (D54, D55, D113–D118, D149)

| # | Check | Result |
|---|---|---|
| 37 | The valley gets the larger half of the window | |
| 38 | A panel that grows cannot move the world, and nothing opened can become unreachable | |
| 39 | The minimap shows the whole valley, and clicking or dragging it moves the camera | |
| 40 | Work ground and harvest paint are both **visible on the map**, and tell apart | |
| 41 | Every panel's text is readable at your window size | |
| 42 | The village log reads as a story rather than a receipt roll | |
| 43 | The pace stays meditative at 4× — a bigger village does not demand more clicking | |

### 3h. The record

| # | Check | Result |
|---|---|---|
| 44 | A clean playthrough logs no `WARN` or `ERROR` lines that name a real problem | |
| 45 | The seed and the log path are both on screen, and quoting the seed reproduces the run | |

---

## 4. Definition of Done for the phase

From `DESIGN.md §4`, honestly:

1. ✅ **Crops and orchards** — D162. Orchards deliberately deferred to Phase 3 (§8 of that spec).
2. ✅ **A golden over a village that clears ground** — `FarmGoldenTests`, which does the crops ×
   brush seam and the clearing path in one run.
3. 🔨 **This checklist, walked by Joe.** Written; not walked.
4. ⛔ **The release blockers** (METHODOLOGY §5): `VERSION` is read by nothing, and
   `src/Bclone.Game/export_presets.cfg` does not exist — without it `release.yml` can never
   succeed, because it exports the "Windows Desktop" preset from a clean checkout.
5. ✅ **`CHANGELOG.md`'s header** reconciled with METHODOLOGY §5.

**Then merge to `main` via [PR #4](https://github.com/joemachen/bclone/pull/4)** — #3 was taken by the closed screenshot-hook PR.

---

## 5. What a failed check is worth

**A check that fails is a finding, not a defeat**, and this project's record says so plainly: of
the last four bugs that mattered — D154's stalled builder, D157's uncleared footprint, D162's
demolition leak and D163's jitter — **three were found by Joe playing and one by reading code.
None was found by the suite.** The suite is 616 tests and it has never once noticed any of them.

So the useful output of this walk is not forty-five ticks. It is the two or three lines that say
*"this looked wrong and here is the tick it happened on"*.
