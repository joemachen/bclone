# Handoff — bclone

Read `CLAUDE.md`, then **`DESIGN.md` §6 (Progress Tracker) and §7 (Decisions Log), D156 back to
D142** — that range is this session — then `METHODOLOGY.md`, then the spec for whatever you are
about to touch. New this session: `specs/housing-and-density.md`.

---

## Where things are

Branch **`wip/step-c-retire-thickets`**. Parent `phase/2-wood-fuel-and-tools` untouched.

**Suite: 563 passing, 0 failing, 9 skipped of 572. Green. Everything is committed; the tree is
clean.** It opened this session at 533 / 13 / 9 of 555.

**The Godot view builds** (`dotnet build src/Bclone.Game/Bclone.Game.csproj` — the solution build
does *not* cover it, D11).

---

## ⛔ START HERE — one item, then the merge

**Re-measure the cold start, then merge step C back to `phase/2-wood-fuel-and-tools`.** That is
the whole of what is left. `ColdStartTests` is the file; D119 is the last time it was measured
(*"one hut on the shipped 25 logs stands t224 and is staffed t241 against a winter at t360"*),
and **six of its guards are currently skipped** — check whether the economy has moved enough to
un-skip any of them.

Everything else on step C's list is done: the goldens are re-taken (D152, D155, D156), the
map-generation guards re-based (D150), the ageing guard re-based (D151).

⚠️ **Nine ticks of the cold start have moved this session** and none has been re-measured:
D153 (birth gate), D154 (builders no longer stall), D155 (`birth_food_percent` 80 → 60), D156
(`adult_age` 15 → 12). Expect the numbers to differ from D119's and **state the new ones**.

---

## What happened this session, in one line each

Every one of these came from Joe playing, except D145 and D150–D152.

| | |
|---|---|
| **D142** | Every fell in the village was billed `PlantTicks` — 12 ticks against a `cut_ticks` of 4 — since D137. Cost D36's market acceptance test. |
| **D143** | ⭐⭐ **An unattended village is *supposed* to die out** (Joe). Six acceptance guards had been demanding the game play itself. Also settles the D103/D131/D134 "the village never asks for the second one" family as working-as-designed. |
| **D144** | The store filter was answered by the predicate and ignored by two deposit paths — 1,500 firewood into a shed refusing it. Also found firewood being *destroyed* once the woodyard filled. |
| **D145** | Swept every player control for D144's gap. Found one more: a met Logs limit never reached the forester. |
| **D146** | The forester toggle is **felling**, not planting; a capped hut replants. `SetStaffing` is a ceiling, not a summons. |
| **D147** | Idle buildings get a ring on the map, like D140's full stores. The design work is all in what does *not* light up. |
| **D148** | The professions column carried three meanings at once. |
| **D149** | Panel columns were a fixed 400px — 19% of Joe's window was map, now 45%. |
| **D150–D152** | Three map guards re-based; the Phase 0 ageing guard re-based; the three goldens re-taken. |
| **D153** | ⭐ Birth gate loses its two household terms; `max_household_size` 7 → 5. |
| **D154** | A builder who delivered a part-load stood on the footprint for 250 ticks. |
| **D155** | `birth_food_percent` 80 → 60 — the village has children again. |
| **D156** | `adult_age` 15 → 12: an uneducated child works at twelve. |

---

## Open with Joe — do not decide alone

1. **His notes for later are in `DESIGN.md` §6**, recorded but not designed: a Banished-style
   **town hall**; an Animal-Crossing-style **museum** of everything unlocked; **real-time charts**
   (1/5/10/20-year lookbacks) gated behind the town hall; **variety** of fish, crops, trees, game
   and livestock; **nomads** and accepting or rejecting them. He has more detail to give on the
   town hall.
2. **⛔ The mid-game gap is the real design problem in that list**, and it is his phrasing: *"how
   to keep the game interesting between stabilising the village and the first children becoming
   laborers."* That window is now about **twelve years**. It is §2.3's dead-late-game question
   arriving early.
3. **Hunger is now a real cause of death** (10–21% of deaths, D155). Joe has not yet played a
   village long enough to say whether that *reads* as pressure or as failure. **Ask.**
4. **`firewood_per_split: 50`** — still unresolved from the previous handoff.

---

## ⚠️ Traps this session paid for

- **⭐ MEASURE, AND MEASURE THE RIGHT THING.** D142's attribution of the ageing guard was
  **wrong** and stood for a day: it blamed planting-by-default and the shed floor, and D151 found
  the metric was *trips per season*, which is floored at 1–2 in that fixture. **A measurement
  that only distinguishes 1 from 2 will happily attribute itself to whatever you last changed.**
- **A guard that searches for its own precondition reports the search.** Four `WorkGroundAllowance`
  tests and the river guard both did this.
- **⭐ Anti-vacuity is not optional and it caught real mistakes here.** *Three* guards I wrote this
  session passed against the unfixed code and had to be rewritten — D154's twice. **Check every
  new guard red before believing it.**
- **A control tested at its predicate and never at its deposit is a control nobody has tested**
  (D144). Five guards on the store filter all passed while it was ignored entirely.
- **`SetStaffing` is a ceiling, not a summons** (D146). A test that raises the number and steps is
  at the mercy of what the village happens to want that season. Pose the case directly.
- **⭐ The audit trail is evidence and the suite is not.** D154 was found in
  `src/Bclone.Game/logs/` — Hattie sat in `building` for 250 ticks — after I failed to reproduce
  it synthetically. **Ask Joe for the log path from his header; it is in every screenshot.**
- **`python` string replaces silently no-op on CRLF files.** Cost three wasted runs. Use the Edit
  tool, or read with `newline=''` and assert the match.
- **Fixture-vs-shipped divergence, now six times.** D155 refused a setting that was the *best* row
  on the fixture and one of the worst on shipped.
- **The full suite is ~13 minutes.** Background it. Do not trust `tail -120` to capture the
  failure list — it truncates the early ones.

---

## Working with Joe

Technical, not a game/systems programmer. Casual, direct; push back honestly — he overruled me
twice this session and was right both times (D146's capped hut replanting, and the residential
brush in D153). **End every message with the explicit ask**, or he cannot tell who is blocking
whom.
