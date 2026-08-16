# Handoff — bclone: **build the farm**

Read `CLAUDE.md`, then **`DESIGN.md` §0–§5 in full, §6, and §7 from D161 back to D142**, then
`METHODOLOGY.md`, then **`specs/crops-and-orchards.md` in full** — that spec is this session's
whole job and it already carries every ruling Joe has made about it.

---

## Where things are

**Branch `phase/2-wood-fuel-and-tools`, pushed and in sync with `origin` at `80eadb0`.** Tree
clean.

**Suite: 589 passing, 0 failing, 2 skipped of 591. Green.** Both skips are rulings, not debt
(D143's unattended village; D134's granary cap).

**The Godot view builds** — `dotnet build src/Bclone.Game/Bclone.Game.csproj`. The solution
build does **not** cover it (D11), and since D160 the view has **no automated verification of any
kind**: looking at it is the test.

**Joe has played this build** — *"everything is going smoothly."*

---

## ⛔ THE JOB: the farm. It is the last slice of Phase 2.

Crops is **three-quarters built and entirely dormant** — nothing can sow yet, which is why the
first three steps could ship without moving a golden. The farm is what turns it on.

### What already exists and is waiting for it

| Piece | Where | State |
|---|---|---|
| `Terrain.Field / Sown / Ripe` | `GeneratedMap.cs` | ✅ values pinned at 6/7/8 by a test |
| A crop id per tile | `GeneratedMap.CropAt` / `SetCrop` | ✅ hashed sparsely |
| Ripen in autumn, rot in winter | `Systems/CropSystem.cs` | ✅ tick-order step 2 |
| `SeasonRules.IsSowing` / `IsReaping` | `World/Season.cs` | ✅ |
| `FoodTheVillageHolds()` | `SimWorld` | ✅ the seam fix, see below |
| Warn on building over a crop | `SimWorld.WarningForBuildingOverACrop` | ✅ |

### The design, already settled — do not redesign it

**A farm is a forester's hut with a different verb.** `buildings-plan.md §8.1` argued fields
should be brushes and left the resolution open; `crops-and-orchards.md §3` closes it: **a
workplace whose extent is painted work ground**. That means `PaintWorkGround`,
`WorkGroundAllowanceFor`, the overstretched warning, the labour quota, the idle ring (D147), the
refusal sentences (D43) and the build queue all apply on day one, because they are properties of
*a workplace with painted ground* and not of forestry.

Copy the forester's hut. It is the working example of every part of this.

### ⚠️ The six things a new `JobKind` obliges

**Measured, not guessed: `JobKind` has 93 references across 11 files.** Line numbers are as of
`80eadb0` — search the symbol if they have drifted.

| Where | What it owes |
|---|---|
| `SimWorld.cs:172` | the verb a villager is described by — *"farming"* |
| `SimWorld.cs:2737` | the `JobKind` → `BuildingKind` map |
| `LabourAllocator.cs:716` | membership of the scarcity-order set |
| `LabourAllocator.cs:860` | the plural — *"farmers"* |
| `LabourQuota.cs:94` | a demand accessor |
| `LabourQuota` demand | **the seasonal arm — start here** |

### ⛔⛔ The trap, and it will cost you a day if you meet it cold

**`SetStaffing` is a ceiling, not a summons (D146).** If the quota does not *actively want*
farmers in spring and autumn, the fields sit sown, the harvest stands, winter takes it — and
**every guard you have written will blame `CropSystem`**, which will be working perfectly. That
is D146's bug waiting one job over.

**Build the seasonal demand arm first and prove it in isolation**, before sowing or reaping
exists: assert that `LabourQuota` wants farmers in spring and autumn and does not in summer and
winter. Then the rest of the farm has somewhere true to stand.

### The order to build it in

1. **The seasonal quota arm** (above), proved alone.
2. `BuildingKind.Farmhouse` + `JobKind.Farmer` + the six plumbing points + recipe and seats
   (`RequiredFarmerSeats`, derived like `VillageEconomy.RequiredForesterSeats`).
3. **The 100-cap local store** — `farm_store_cap` in data. See the warning below.
4. **Sowing and reaping** as work, on the farm's painted ground.
5. **Hauling**: the farmer carries to the nearest storage *with room*, so the farm's own buffer
   fills first and the walk lengthens once it is full.
6. **The market reaching a workplace store** — granary first (see rulings).
7. **The crops × harvest-brush golden**, written *with* the feature, not after.
8. **Derive the numbers** (§5.1's surviving target), then **re-take the two village goldens
   last**, one commit, one stated reason each (D152).

---

## ⭐ The seam that is already fixed, and why you must not undo it

`Workplace.Store` has existed since D30 and **nothing had ever written to it**, so
`FoodInGranaries()` (store buildings only) and `TotalFood()` (everything) had never once
disagreed. The farm's buffer makes them diverge — and **the birth gate**, the village-wide reason
to gather and the food stock limit all read the blind one. A village whose harvest sat at the
farm would have **quietly stopped having children**: D155's symptom from a new direction, and
D81's bug, on record as *D76's seam for the fifth time*. **This was the sixth.**

`FoodTheVillageHolds()` — stores **plus workplaces**, **deliberately not larders** — is what
decisions read now. Larders are excluded because a family's larder is food already distributed,
and counting it would re-add the household term D153 removed from the birth gate.

**It was found by writing the spec, before the farm existed.** `WorkplaceStoreTests` guards it.

⚠️ **When you fill the farm's store, `Stockpile.Add` returns what it actually took and the caller
must read it.** Not reading it is D96 exactly — 17,451 food into a full granary and out of the
world — and D144 is the same shape one deposit path over. **This store has never been written to,
so that path has never been exercised.**

---

## Joe's rulings on crops — settled, do not re-open

1. **A farm has a building**, and the field zone is painted work ground the farmers sow and reap.
2. **Harvest goes to the granary; the farm holds up to 100 locally.** This wires up the fifth
   element of `professions.md`'s role model, dead since D30 — and re-aims it from the forester to
   the farm, which is already corrected in that spec.
3. **The farmer reaps and hauls** to the nearest storage *with room*.
4. **The marketer may source from farm storage — granary first**, and a farm only when it is
   *strictly nearer* than the nearest store building holding that good. No threshold, no new
   tunable. ⚠️ `NearestStoreAccepting` iterates `StoreBuildings` only, so this is a real change to
   the market's reach — and it must not become a **second way to find a store** (D145).
5. **Laborers never move farmed goods.** Hauling stays building materials.
6. **One crop now, in a model shaped for many** — the id exists; crops belong in data.
7. **Use it or lose it.** Already built.
8. **Building over a standing crop warns and is allowed.** Already built.

---

## ⚠️ Traps — the first three are this session's own, all the same shape

- **⭐⭐ CHECK EVERY GUARD RED, AND COUNT THE REDS.** Three near-misses in one session: a guard
  that was **vacuous and passed against the broken code**; a guard whose **name claimed more than
  its body proved**; and a **harness off-by-one that read as a broken feature**. The first was
  caught *only* because disabling the fix produced **two** reds out of three instead of three.
  **Counting matters as much as running.**
- **`SimLoop` runs the systems and *then* advances the tick.** The moment `World.Clock` first
  reports a new season, **no system has run on it yet**. A test that steps until the season
  changes and then asserts is one tick early and looks exactly like a broken feature.
- **⭐ A green golden can mean "not covered", not "no-op"** (D157) — both 50-year goldens paint
  zero tiles in fifty years. **The clearing-path golden is still owed** and is on §4's queue.
- **⭐ The goldens are SUPPOSED to move once a farmer sows.** Every crop step so far has been
  provably invisible, which made verification easy; that ends here. Re-take them **last**, in
  their own commit, with one stated reason each (D152). **If they move before you expect it,
  something is wrong — say so rather than re-taking them.**
- **The audit trail is evidence and the suite is not.** D154 and D157 both came out of
  `src/Bclone.Game/logs/`. **Ask Joe for the log path from his header; it is in every screenshot.**
- **A control tested at its predicate and never at its deposit is a control nobody has tested**
  (D144, and D145's sweep). Both live risks for the farm's store and the market's new source.
- **Assert against the shipped config, not only the fixture** — they have diverged six times, and
  METHODOLOGY §3 exists because of it.
- **`python` string edits die on this repo's CRLF *and* its emoji** (`UnicodeDecodeError:
  charmap`). Use the Edit tool.
- **`dotnet test` buffers stdout when redirected**, so a background run looks frozen at ten lines
  for a quarter of an hour. `Get-Process testhost` and look at CPU to tell working from hung.
- **The full suite is ~13–17 minutes.** Background it.

---

## After the farm — do not drift into these, they are Joe's calls

Phase 2's Definition of Done is in **`DESIGN.md §4`** and the farm is item 1 of five. The rest:
the clearing-path golden, a **QA checklist walked by Joe** (not by you), the release blockers
(`VERSION` has no reader; `export_presets.cfg` does not exist), and `CHANGELOG.md`'s header.
**Then merge to `main` via PR #3.**

**The mid-game gap is NOT Phase 2's to claim** (D161). Crops is the *rhythm* of those sixteen
years; **skill is the answer**, and that is Phase 3 — whose success test is already written:
*play years 1 through 16 at normal speed, without fast-forwarding, and want to keep watching.*

---

## Working with Joe

Technical, not a game/systems programmer. Casual, direct; **push back honestly** — his rulings
were better than the proposals twice this session (build-queue order for clearing, and skill
rather than crops as the mid-game answer). **End every message with the explicit ask**, or he
cannot tell who is blocking whom.
