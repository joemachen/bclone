# Spec: Water as a household need — the well's clock becomes the larder's

> Status: **📝 SPECCED, ⏸️ FOUR CALLS JOE'S (D775, 2026-11-15, Joe: *"water is next for systems"*) — 🔨 ON
> `slice/water-need`; NOTHING BUILT.** Owner: Joe + Claude Code · Pillar: `DESIGN.md §2.1` (needs), §2.6 (one cost field)
> · Phase 6, the ordered list's item 2 (D750). Format per `METHODOLOGY.md §2`.
>
> Neighbours: `organic-housing.md §9.12` (the well, D427 — **the baseline this is measured against is §9.12f**),
> `clothing.md` (the last need-shaped errand: a coat fetched from a store), `pathfinding-and-water.md` (the river is
> impassable and generated once), and Phase 6's later pieces that read this one: **drought** (its answer is the well,
> D656) and **health** (*"the well is health's door to water"*, §4).

---

## 1. Goal

Joe, 2026-09-29 (D427), when the well went in on a fixed clock: *"the water function isnt even in the game yet - the
household larder and home water needs will determine how often villagers go to the well, but this data (every 20 days)
helps with that when the time comes."*

So: **a household keeps water at home and drinks it; somebody goes for more when it runs low.** The walk is the
whole cost, and it is visible, the way the shopping walk is (D668). The well's 20-day clock goes.

## 2. What is there today (re-read against the code, 2026-11-15)

- **The well** (`BuildingKind.Well`, `SimWorld.Wells`, D427): optional; with none, nothing is tried and nothing hashed.
- **The trip** (`BehaviorSystem.TryDrawWater`): offered only where a rest at home would begin, to an adult with empty
  hands, when `Household.WaterDrawnOnDay` is `water_trip_every_days` (20) old and no housemate is out, and **only to a
  well within `home_well_reach_tiles` (12)**. `WalkingToTheWell` → `DrawingWater` (`well_draw_ticks` 2) → home.
  **Nothing is carried and nothing is drunk.**
- **Hashed:** `WaterDrawnOnDay`, when set. Saved: `water_drawn_on_day`.
- ⚠️ **Open since D428, and first in the order of work:** *"why the water trip costs survival at higher frequencies —
  trace it before Phase 6 makes water a need."* Every 10 days cost 21 shipped alive of 169 (§9.12f); the `Decide`-only
  ablation read 145, so the cost is not the job-holder's idle moment, and its mechanism is still unproven. **A need
  that sends people more often than every 20 days inherits that cost unexplained.**

## 3. ⏸️ Joe's four calls — each with a recommendation

### Call 1 — What water is

- **A (recommended): a number the household keeps at home**, beside its larder: filled at the well or the river,
  drunk by every member each day. **Not a good**: never hauled to a warehouse, stocked at the market, traded or
  limited on the stock bar. It needs none of that machinery, and none of it would mean anything for water.
- B: a good (`Goods.Water`) in the larder. It brings the whole goods machinery with it, and a stock-limit row nobody
  would ever set.

### Call 2 — Where water comes from

- **A (recommended): the nearer of a well or the river bank.** Every valley has its river (D547). With no well, a
  household walks to the bank. **The well's payoff becomes walking saved**, the market's shape: build one near the
  houses and the trips shorten. Drought can later lower the river and leave the well, which is the answer D656 asked
  for. ⚠️ **Every golden moves**: every village, well or no well, now walks for water.
- B: only a well. Then a village without one either has no water (and call 3 must punish it) or the need does
  nothing until one is built. A need that does nothing before a building is a building's tax, not a need.

### Call 3 — What running dry does

- **A (recommended): nobody dies of thirst; a household that runs dry goes at once.** It sends somebody ahead of work,
  like the emergency restock, and **says so**: the household card reads *"No water — Dovlow has gone to the river"*,
  and the log says it once per dry spell. The cost is the hours walked, and you can see whose. Health (Phase 6, item 5)
  can later read *where* the water came from: the well is its door.
- B: thirst like hunger, which can kill. A second way to die, which §0.1 admits only if foretold and answerable. It
  would be answerable (a well), but it is a death spiral D207 refused for morale, and it lands before health exists to
  explain it.

### Call 4 — The numbers, measured before his pick

`water_per_person_per_day` and `water_jar` (how much a home holds), and so how often a household goes: a family of two
with a jar of 40 at one a person a day goes every 20 days (today's clock), and a family of five every 8. **Measured
first, as D427 was:** villages alive at 50 and 100 years at three or four jar sizes, against §9.12f's baseline and
against today's main. **After step 1's trace**, so the number is not picked on a cost nobody understands.

## 4. What the art lane will draw (D774's rule: every systems spec names it)

Under the recommended calls:

| Kind | What | New? |
|---|---|---|
| Buildings | the well | exists (D427); no new building |
| Goods | none: water is the household's, not a good (call 1A) | — |
| Villager states | `WalkingToTheWell`, `DrawingWater` | exist |
| Villager states | **drawing water at the river bank** | new (call 2A) |
| Villager states | **carrying water home** (a pail, or a yoke and two pails) | new: today nothing is carried |
| Map | the lane from the houses to the bank, worn by the feet | the path wear that exists; no new paint |

Under call 1B the stock bar would also want a water icon.

## 5. The order of work (after his calls)

1. **Trace D428's open question.** Why a well trip every 10 days cost 21 of 169. Report it before any number.
2. **Water at home, drunk, at size nought.** The household's water and its daily drink, the trip filling it. Shipped
   at sizes that reproduce today's 20-day clock for a family of two, so the change is the mechanism, not the price.
3. **The river bank** (call 2A): the nearest walkable tile beside the river by the shared cost field (§2.6, never a
   second distance), a well preferred when nearer.
4. **Running dry** (call 3): the trip ahead of work, the card line, the log sentence.
5. **Measured** (call 4) at three or four jar sizes; Joe picks.
6. **The view**: the card's line and NEEDS, the carried pail (or the art lane's stand-in), the probe line.
7. **Saves and the hash**: the household's water saved and hashed; `water_drawn_on_day` retired with a save-format
   step; the goldens re-taken in one commit, the diff read.

## 6. Acceptance

1. A household's trips follow its size: five go more often than two, at the same jar.
2. A well nearer than the river shortens the trips; built far off, it is not used.
3. Running dry is said before anybody walks, and nobody dies of it (call 3A).
4. Hunger and cold still come first by structure, as D427 guarded.
5. Villages at 50 and 100 years within the noise of §9.12f's baseline at Joe's numbers, or the cost explained.

## 7. Definition of Done

This spec current; content in data (`water_per_person_per_day`, `water_jar`); the guards red-checked and counted; the
determinism golden re-taken in one commit and the diff read; the four checks; Joe plays it; `DESIGN.md §6`, §7,
`HANDOFF.md` and `content-inventory.md` updated.
