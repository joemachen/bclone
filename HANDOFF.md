# Handoff — bclone

Read `CLAUDE.md`, then **`DESIGN.md` §0–§5 in full, §6, and §7 from D161 back to D142**, then
`METHODOLOGY.md`, then **`specs/crops-and-orchards.md`** — that spec is the work in flight and
it carries every ruling Joe has made about it.

---

## Where things are

**Branch `phase/2-wood-fuel-and-tools`.** Tree clean. **Eight commits ahead of `origin`** —
Joe has pushed once this session and has not asked again since.

**Suite: 589 passing, 0 failing, 2 skipped of 591. Green.** Both remaining skips are rulings,
not debt (D143's unattended village; D134's granary cap).

**The Godot view builds** (`dotnet build src/Bclone.Game/Bclone.Game.csproj` — the solution
build does *not* cover it, D11). It has **no automated verification of any kind** since D160
deleted the screenshot hook: looking at it is the test.

**Joe has played the merged build** — *"everything is going smoothly"* — so step C and the
house-cleaning are both confirmed at the screen.

---

## ⛔ START HERE — the farm

Crops is Phase 2's last slice and **three of its four steps are done**. What remains is the farm
itself, and `specs/crops-and-orchards.md §11b` has the reconnaissance already done:
**`JobKind` has 93 references across 11 files**, and a new kind obliges exactly six things —
the verb (`SimWorld:172`), the kind→building map (`SimWorld:2694`), the scarcity order
(`LabourAllocator:716`), the plural (`LabourAllocator:860`), a demand accessor
(`LabourQuota:94`), and **the seasonal demand arm**.

**⚠️ That last one is the one with teeth.** `SetStaffing` is a ceiling, not a summons (D146), so
if the quota does not actively *want* farmers when the fields are ripe, **the harvest stands and
rots and every guard will blame the crop system.** That is D146's bug waiting one job over.

The rest of the farm: `BuildingKind.Farmhouse`, seats derived like `RequiredForesterSeats`,
sowing and reaping as work, hauling to the nearest storage *with room*, and the **100-cap local
store** — which is the first time anything has written to `Workplace.Store` (see below).

**Then:** the crops × harvest-brush golden, then derive the numbers, then re-take the two
village goldens **last** (D152), then Phase 2's remaining Definition of Done in `DESIGN.md §4`.

---

## What crops has landed, in one line each

| | |
|---|---|
| **The food-reading seam** (`e9c2d9d`) | The village's food is what it *holds*, not what is in the granaries. |
| **The ground** (`0684fcd`) | `Terrain.Field/Sown/Ripe` + a crop id per tile, hashed sparsely. |
| **The year** (`399f04b`) | `CropSystem` at tick-order step 2: autumn ripens, winter rots. |
| **Warn and allow** | Building over a standing crop is permitted and says what it costs. |

**⭐ The seam is the one worth understanding before you touch anything.** `Workplace.Store` has
existed since D30 and **nothing had ever written to it**, so `FoodInGranaries()` (stores only)
and `TotalFood()` (everything) had never once disagreed. The farm's 100-cap buffer makes them
diverge — and the **birth gate**, the village-wide reason to gather and the food stock limit all
read the blind one. A village whose harvest sat at the farm would have **quietly stopped having
children**: D155's symptom from a new direction, and D81's bug, on record as *D76's seam for the
fifth time*. This is the sixth. `FoodTheVillageHolds()` is what decisions read now — stores plus
workplaces, **deliberately not larders**, because a larder is food already distributed and
counting it would re-add the term D153 removed.

**It was found by writing the spec, before the farm existed.** That is the argument for
spec-first in one sentence.

---

## Joe's rulings on crops — do not re-open these

1. **A farm has a building**, and the field zone is painted work ground the farmers sow and reap.
2. **Harvest goes to the granary; the farm holds up to 100 locally** (`farm_store_cap`). This
   wires up the fifth element of `professions.md`'s role model, dead since D30 — and re-aims it
   from the forester to the farm, which is corrected in that spec.
3. **The farmer reaps and hauls** to the nearest storage *with room*, so the farm's own buffer
   fills first and the walk lengthens once it is full.
4. **The marketer may source from farm storage — granary first**, and a farm only when it is
   *strictly nearer* than the nearest store holding the good. No threshold, no new tunable.
5. **Laborers never move farmed goods.** Hauling stays building materials.
6. **One crop now, in a model shaped for many** — a crop id per tile, crops defined in data.
7. **Use it or lose it**: a ripe field nobody reaps rots over winter, warned in autumn while it
   can still be acted on, then one log line when it goes.
8. **Building over a standing crop warns and is allowed**, never refused.

---

## ⚠️ Traps — the first one is this session's own

- **⭐⭐ CHECK EVERY GUARD RED, AND COUNT THE REDS.** Three separate near-misses today: a guard
  that was **vacuous and passed against the broken code** (its middle term was zero either way);
  a guard whose **name claimed to prove the sparse-hash contract** when its body only proved
  round-trip identity; and a **harness off-by-one that read as a broken feature**. The first was
  caught only because disabling the fix produced *two* reds out of three instead of three.
  **Counting matters as much as running.**
- **`SimLoop` runs the systems and *then* advances the tick.** So the moment `World.Clock` first
  reports a new season, **no system has run on it yet**. A test that steps until the season
  changes and then asserts is one tick early, and it looks exactly like a broken feature.
- **⭐ A green golden can mean "not covered" rather than "no-op"** (D157). Both 50-year goldens
  paint zero tiles in fifty years, which is why the clearing-path golden is still owed.
- **The audit trail is evidence and the suite is not.** D154 and D157 both came out of
  `src/Bclone.Game/logs/`. **Ask Joe for the log path from his header; it is in every screenshot.**
- **A control tested at its predicate and never at its deposit is a control nobody has tested**
  (D144), and **`SetStaffing` is a ceiling, not a summons** (D146). Both are live risks for the
  farm.
- **`python` string edits die on this repo's CRLF *and* its emoji** (`UnicodeDecodeError:
  charmap`). Use the Edit tool.
- **`dotnet test` buffers stdout when redirected**, so a background run looks frozen at ten lines
  for a quarter of an hour. `Get-Process testhost` and look at CPU to tell working from hung.
- **The full suite is ~13–15 minutes.** Background it.

---

## Working with Joe

Technical, not a game/systems programmer. Casual, direct; push back honestly — his rulings this
session were better than my proposals twice (build-queue ordering for clearing, and skill rather
than crops as the mid-game answer). **End every message with the explicit ask**, or he cannot
tell who is blocking whom.
