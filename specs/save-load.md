# Spec: Save and load — the village keeps

**Decisions:** D353 (the shell's order: error boundary → per-stage seeds → new-game screen → settings
persistence → **save/load** → title and pause), D364 (`SimLoop.Fault` — *"a corrupt save will want the
same door"*), D504 (*share the work out* is the village's, not a setting — it travels here), **D507 (Joe's
calls for this slice, 2026-10-05 — §2), D508 (his answers to §11)**. Neighbours: D3 (JSON), D335 (a derived index is never hashed),
D419 (a round trip posed from its own capture agrees with itself), D15 (the reflection-guard shape),
`tick-loop.md §5` (the fault door) and its open question *"state hash vs. full serialization"*,
`new-game-screen.md §5` (the share code), `settings-persistence.md` (the file pattern this copies).
**Status:** ✍️ **SPECCED (D507); Joe answered §11 (D508). 🔨 BUILT (D509) on `slice/save-load` — the sim half
and the view, guards §9 red-checked (14 mutants, all red; two zeros on the way, fixed by poses and written
down). ⏸️ UNPLAYED by Joe** — no automated check sees a real quit write the autosave, or *Continue* open it.
Owner: Joe + Claude Code.

---

## 1. Why this exists

A village is fifty years of a lineage, and today it lives exactly as long as the window is open. Quitting
is losing it. That is the §0.1 *punishment for stepping away* in its purest form: no player can give a
village a generation if they have to give it one sitting. Save/load is what makes §1.5's *generational time
is the core loop* playable at all.

## 2. Joe's calls (D507, 2026-10-05)

- **A snapshot, not a replay.** §4 offered *seed + input log with replay* or *a snapshot built beside
  `StateHash`*, to be chosen by measuring. The measurement is in §3. What decided it is something the
  measurement cannot show: **a replay save is only as good as the sim it was recorded against.** Every
  slice that moves a golden, which is most of them, would make every old save replay as a different
  village, silently (D465: *"every seed replays as another history"*). A snapshot is the village's state.
  A balance change carries an old village on under the new rules.
- **Autosave plus named saves.** The game saves itself, and the player can also save under a name
  whenever they like. ⏸️ **One slot per village, "ironman mode", is for later**, and it wants a better
  name (Joe: *"we'll have to come up with a better theme name than that"*). It is a mode over this same
  file, not a second save system (§10).
- **An older save format is refused, in words.** There are no migrations before v1. A save written by a
  build with a different `format` does not open, and the load list says which build wrote it.

## 3. The measurement (D507)

What a replay save would cost to load: the founding plus fifty shipped years, re-simulated
(`tools/harness/ZzReplay.cs`, Release, ten seeds an arm, Windows, 2026-10-05).

| Arm | Peak people | Fifty years, re-simulated |
|---|---|---|
| unattended (founded and left) | 4 | 19–56 ms (every village dies in its first years) |
| played (`ColdStartTests`' opening, granary + warehouse at year 3) | 9–16 | 300–570 ms, one 1.7 s |
| every (played + lodge, fishery, farm) | 16–19 | 670–950 ms, one 1.8 s |
| established (the warm founding) | 8–19 | 340–640 ms |

- ⚠️ **No harness village grows past nineteen people**: an unattended village is granary-capped (D257).
  A village Joe has played for fifty years is bigger, and the cost grows at least with the number of
  people. So *"a second or two"* is a **floor**, and it grows again with every further decade.
- **One `StateHash.Compute` over a fifty-year village takes about 2 ms.** That is a full walk of the
  hashed state, which is most of what a snapshot reads and writes. So a snapshot should load in tens of
  milliseconds whatever the village's age.
- ✅ **Measured once it was built (D509), the same forty villages after their fifty years:** writing the
  save (capture + gzip + the atomic move) **5–9 ms**; opening it (gunzip + parse + build, hash checked
  equal) **14–31 ms**, one outlier 52 ms on the run whose fifty years also took 2.6 s (the machine, that
  minute); the file **5–11 KB**. Against 300–2,500 ms to replay the same villages, and that is the floor.

## 4. What a save is

One file, holding:

| Part | What | Why |
|---|---|---|
| `format` | an integer, starting at 1 | §7: a different number is refused |
| `build` | the `VERSION` that wrote it (`0.0.1` today) | said on the load list and in a refusal |
| `share_code` | `NewGame.ShareCode` — the seed and every new-game row | the config is rebuilt from it (below) |
| `village_name` | the name the player gave | never hashed, never in the share code |
| `tick` | `SimWorld.Tick` | |
| `rng` | `DeterministicRandom.State` and `.Inc` | restored with `FromState` (`DeterministicRandom.cs:51`), which exists for this |
| `world` | the village (§5) | |
| `saved_at` | the real date and time, for the load list only | never read by the sim; the sim bans the clock |

**The config is not stored. It is rebuilt** with `NewGame.Apply(the data files' config, the share code)`,
exactly as a founding builds it. So **today's `data/` governs a loaded village**: a balance change, a
re-tuned yield or a new stock-limit default reaches an old village the moment it is loaded. That is what
*"carries on under the new rules"* means. Nothing is **generated** on load. The map, the people and the
buildings all come from the file, and the generator never runs.

⚠️ This means a changed `data/` can meet state it did not make, for example a building kind a modder has
since removed. That is a refusal in words (§7), never a half-load.

## 5. What goes in — three lists, and the one that stays out

### 5.1 Everything `StateHash.Compute` mixes
About 120 fields, in `StateHash.cs:71-718`'s order: the map, the zones' sub-tile layers, the player's
controls (`StockLimits`, `JobLimits`, **`VillageSharesOutWork`**, which is how *share the work out* gets
remembered, D504), every villager, household, workplace, store, library, well and the town hall,
knowledge, the food ledger, the gifts owed, path wear, the dug and forged counters, the ground stacks, and
the buildings waiting. **`StateHash` is the checklist and the save follows its order.** A field the hash
mixes and the save forgets is caught by §9.1.

### 5.2 Truth the hash does not see — and the sim reads
Found by reading the code (D507). Each one either changes what the village does after a load or is
history no other state records:

| State | Where | Why it must be saved |
|---|---|---|
| `GeneratedMap._everWooded` | `GeneratedMap.cs:247, 454` | the terrain's **history**, not its present: not derivable from today's map |
| `PathWear._priceClass`, `_routesDirty` | `PathWear.cs:46, 168, 213` | the hysteresis remembers the last hand-over's class; today's wear does not say it |
| `TravelCostField._entryCost`, `_anythingWorn` | `TravelCostField.cs:84-87, 344-355` | the prices copied at the last **spring** re-price, not today's. ⚠️ The build proves whether these rebuild exactly from `_priceClass` (§9.2's spring pose). If they do not, they are saved too |
| `SimWorld._nextWorkplaceId` | `SimWorld.cs:4784, 9776` | keeps a demolished workplace's id from being reused |
| `ConstructionSite._delivered`, `WorkDone` | `Construction.cs:606-615` | a half-built site; `Workplace.Construction` is never mixed |
| `Workplace.Kind`, `Capacity`, `GatheringRadius` | `Workplace.cs` | not mixed; the building's own identity |
| `Villager.LastWorkplaceId` | `LabourAllocator.cs:109, 329, 1101` | its comment says *explanation only*, but the allocator **reads** it |

⭐ **This table is what a snapshot built only "beside `StateHash`" would have lost**, and it is the case
for §9.2 and §9.4. The hash is a fingerprint (`StateHash.cs:12-23`), not a serializer. It was always
allowed to miss a field that never changes the future, and these do.

### 5.3 What the player reads
Not simulated, and still the village's:
- **Names**: `Villager.Surname`, household and building renames (D376).
- **The narration latches** (`_saidThereIsNowhereFor`, `_saidKnowledgeIsAtRisk`, `_saidSiteIsWaiting`,
  `SaidTheyCanWrite`, `WorkSeenUndoneOnce`, …). Lose one and a loaded village says a thing twice.
- **The statistics** (`FoodEverProduced` / `Eaten` and per good, `LogsEverFelled`, `LogsEverSplit`,
  `ToolsEverForged`, `ToolsEverTaken`, `WorkActionsBegun`). These are what the town hall's charts will
  read.
- **`Moments`** still waiting to be dismissed.
- **`JobReason`**, the *why* on a villager's card (§1.1). A loaded village must still answer *"why is
  Elias at the stand?"*

### 5.4 What stays out, and is rebuilt once on load
- **D335's derived indexes**: ZoneMap's tile summaries and owner indexes, `_onTheGround`, the
  `_standing*` arrays, `_freeGroundToday`, `_plotsAsked`, `Workplace.CachedWoodedTiles`, and every
  cached flow field. ZoneMap's summaries are rebuilt by painting the saved sub-tiles back through the
  brush's own `Set…` doors (one owner per tile makes the order free); `_onTheGround` is summed once from
  the heaps; the rest are rebuilt on the first ask, as at a founding. ⛔ None is ever rebuilt per tick to
  make loading easier (CLAUDE.md).
- **Generation counters** (`_terrainGeneration`, `BuildingGeneration`, `StandingGeneration`, `Edits`,
  `WallGeneration`, `PathWear.Generation`) start fresh. They only answer *"has this changed since I last
  looked?"*, and after a load nothing has looked yet.
- **Within-a-tick scratch**: `PacedOnTick` / `PaceLeft`, `BehaviorSystem._carriedBefore`.

⚠️ **Two things this list first put here are saved after all (as built, D509).** *A plot*: it is derived
from where a house's **front** was and which way it faced when it was marked (`SimWorld.PlotFor`), and
the front is kept nowhere else, so each household's plot tiles, lane, fence edges and gate go in whole
and the walls are raised again from the fences. *First names, `DayForAChild` and `AgeYears`*: pure
functions of the seed and an id or of `BirthTick`, but stored on the objects, so they are saved as they
stand rather than re-derived — cheaper than proving the derivation, and §9.4 compares them.

⭐ **The systems are stateless** (checked, D507: the only field in `Systems/` is that scratch array). All
state lives in `SimWorld`, so a loaded world is driven by `SimFactory.CreatePhase0`'s own list of
systems, in its order (D5).

## 6. The file

```json
{
  "format": 1,
  "build": "0.0.1",
  "saved_at": "2026-10-05 21:14",
  "share_code": "mossy-lantern-41#river=3,flow=we,woods=35,stone=moderate,iron=moderate",
  "village_name": "Ashford",
  "tick": 24000,
  "rng": { "state": "9f3c…", "inc": "0b51…" },
  "world": { "map": { "terrain": "<base64>", "…": "…" }, "villagers": [ … ], "…": "…" }
}
```

- snake_case JSON with keys written in a fixed order (D3), the way `PlayerSettings` writes. A save is
  something a modder or a bug report can open and read.
- **The bulk arrays** (terrain, `_everWooded`, the sub-tile zone layers, path wear and price classes) are
  base64 of their bytes, not JSON arrays of numbers. The whole file is **gzip'd**
  (`System.IO.Compression`, which is in the BCL), and a fifty-year harness village comes to **5–11 KB** (D509, §3).
- **No floats.** `Fixed` and `Angle` go in as their raw bits, as the hash mixes them. A `ulong` goes in as
  hex text, because JSON numbers are doubles to most readers.
- `saved_at` is the only wall-clock value, and it is written by the **view**. The sim never reads it,
  because `BannedSymbols.txt` bans the clock there.

## 7. Where, when, and what can go wrong

**Where.** `user://saves/<village>/`, for example
`%APPDATA%\Godot\app_userdata\bclone\saves\ashford-mossy-lantern-41\`. The folder name is the village
name and the seed, so two Ashfords are two folders. Autosaves are `autosave-1.save` (newest),
`autosave-2.save` and `autosave-3.save`, rotated. A named save is `<name>.save`.

**When.**
- **Autosave at the turn of every year** (Spring, Day 1, after the tick that turns it), **and on quit.**
  It is written between ticks, so there is no half-run tick to capture.
- **Named saves** whenever the player asks, paused or running. The save is taken between frames, so
  between ticks.
- Every write is **atomic**: `.tmp` and then a move, as `PlayerSettingsFile.Save` does
  (`PlayerSettings.cs:254`).
- ⛔ **A faulted village (`SimLoop.Fault`) is never saved.** There is nothing sound to save
  (`tick-loop.md §5`). The autosave and *Save* are refused in words (the table). The last good autosave
  is the way back, which is exactly why there are three of them.
- ⏸️ **The cadence becomes the player's when full settings exist** (Joe, D508): yearly is the default,
  and a later *full settings* screen lets the player choose it. Until then it is a constant in the view,
  and nothing about the file depends on it.

| What | What happens |
|---|---|
| No saves yet | *Continue* is not offered. Nothing is said. |
| A different `format` | Refused: *"Saved by build 0.0.3, whose saves this build cannot read."* It stays on the list, greyed, so the player can see it was not lost. |
| Not gzip / not JSON / a key missing / a wrong type / a duplicate key | Refused: *"This save can't be read — it's damaged. A copy was kept as ashford-1.save.bad."* The file is **copied to `<file>.bad` first**, `KeepTheBrokenFile`'s pattern (`PlayerSettings.cs:271`). **Never half-loaded**: the world is built whole, or not at all. The detail (which key, which byte) goes to the audit log, not the sentence. |
| State today's `data/` cannot hold (a building kind or good that no longer exists, a crop id gone) | Refused: *"This save holds “Smokehouse”, which this game no longer has, so it can't be opened."* — the thing named as the save spells it (`SaveDataException.What`): a kind by its name, a good by its id (*"good 12"*), or *"a different list of goods"* when the catalogue itself changed. Not damage: no `.bad` copy. A modder changed the data, and the player should hear that rather than see a village quietly missing a building. |
| *Save* or the autosave on a faulted village | Refused: *"The village stopped on an error, so it can't be saved. The last autosave is from Year 12."* |
| The first tick after a load throws | **D364's door**, unchanged: `Main.HaltTheVillage`. The load was sound and the sim was not, so that is a fault, not a corrupt save. |
| The disk will not take the write | Said in the village log and Godot's error stream. The game carries on, and the previous autosave stands (the atomic write never touched it). |

Every refusal and problem is also written to the audit log (METHODOLOGY §4). Nothing is swallowed.

## 8. Where it lives

- **`Bclone.Sim/Persistence/`, pure and tested** (as built, D509): `SaveGame` (the header record, `Capture`,
  `HeaderOf`, `ConfigFor` — today's data with the save's share code, through `NewGame.TryRead` and
  `NewGame.Apply` — and `Load`), `SaveFile` (gzip, the atomic write, the autosave rotation, and **`Open`,
  the one door a save comes in by**, which never throws for anything in the file) and `SaveData`
  (`SaveReader` / `SaveWriter`, every value spelled in one place; `SaveFormatException` for damage,
  `SaveDataException` for a sound save over changed data). ⚠️ A good is saved by its **id**, never its
  enum name: a modder's good has an id and no name.
- **Each class saves itself** — `ToSave` / `FromSave` (or `ReadSave` into a fresh one) beside its fields,
  so the field and its line in the save are read together. **`SimWorld`** holds the rest: an `internal`
  `CaptureTheVillage`, an `internal static Restore`, and the constructor's one new argument — given a save,
  it reads the map from it instead of generating one, wires the wear exactly as a founding does, then reads
  the village (`ReadTheVillage`) and returns before anything is founded.
- **The view**, `Main.Saves.cs`. Until the title screen exists (§4's next shell step):
  - **the new-game screen** gains **Continue** — the newest save that can be opened, of any kind — and
    **Load…**, a popup of every save (village, season and year, file, build), newest first, an unreadable
    one greyed with its refusal as the tooltip. ⚠️ A popup, not a list in the column: the column is
    measured at 400 by the probe's `new game:` line;
  - **in the game**, a name and *Save* sit in Settings under *How the village runs* (an empty name saves
    as `year-N`); the village log says *"Saved as … — path"*;
  - **autosave at the year's turn**: a frame's ticks are stepped up to the turn, the autosave written,
    then the rest — so it is the village of Spring, Day 1 whatever the speed. **And on quit**
    (`NotificationWMCloseRequest`).

## 9. Guards (each red-checked, the reds counted — D326)

**Sim (`SaveLoadTests`):**
1. **`save → load → StateHash == live`** — §4's guard. Necessary and weak: it cannot see §5.2 or §5.3.
2. ⭐ **`save → load → run a year → hash and the sim log == a village that never stopped`.** This is the
   guard that sees §5.2: a field the sim reads and the save forgot makes the year diverge. Poses:
   - the shipped config, the fixture, and the played opening (`ColdStartTests.PlayTheOpening`);
   - save points **mid-walk** (a leg half-walked), **mid-construction** (materials half-delivered),
     **in winter**, and **the last tick before spring's re-price** (catches `_entryCost` /
     `_priceClass`), plus a village with *share the work out* off;
   - ⛔ **every pose is built by playing, never by editing the capture** (D419: a round trip posed from
     its own capture only agrees with itself).
3. **`save → load → save` gives identical bytes.**
4. ⭐ **The reflection guard.** Every instance field of `SimWorld`, `Villager`, `Household`, `Workplace`,
   `StoreBuilding`, `ConstructionSite`, `GeneratedMap`, `ZoneMap`, `PathWear` and `TravelCostField` is
   either **saved** or listed as **derived, with its reason** (§5.4). A field in neither list fails, with
   its name. This is what keeps the save true after the next slice adds a field, which the next slice
   will. It is D15's shape, and it is the guard this whole feature rests on.
5. **Refusals**: a different `format`; truncated bytes; not gzip; not JSON; a missing key; a duplicate
   key; an unknown building kind. Each gives one sentence naming the file, a `.bad` file holding the
   original bytes, no exception, and no world.
6. **Writing** leaves no `.tmp` behind, replaces an existing file, and rotates three autosaves.
7. **A faulted loop refuses to save.**

**View (probe line `save:`):** found the probe's village, save it to a **scratch path** (never the
player's `saves/`, `settings-persistence.md §7`'s rule), load it into a second world, and compare the
hash. Then the *Load…* list reads the scratch save back with its village and year.

**As built (D509): eight poses, every one played** — the opening while a site is half-supplied and a
villager is half way along a leg (tick 61, found by scanning, not guessed); the last tick before a spring
re-price; winter; year 6; the fixture in winter; the warm founding with *share the work out* off; the warm
founding with **every store closed** (the *"nowhere to keep it"* latch); and an **unattended village gone
empty** with its moment waiting. `ThePosesCatchTheMomentsTheyAreNamedFor` asserts each premise. §9.4 is
two tests: `EveryFieldIsSavedOrNamed` walks every class reachable from `SimWorld` through what is saved
and fails on any field not in its table (*Saved*, *Rebuilt* — compared too, so the rebuild is proven —
*Cache*, or *Data*, each with its reason); `EverySavedFieldComesBackAsItWas` compares every *Saved* and
*Rebuilt* field, deep, between the village saved and the village loaded.

**The red-check (D326), one saved field dropped at a time — 14 mutants, all red:**

| Dropped | Reds | Caught by |
|---|---|---|
| `_everWooded` | 10 | field compare, byte identity — ⚠️ not the run-on year (regrowth needs a felled wood) |
| `LastWorkplaceId` | 10 | field compare, byte identity — ⚠️ not the run-on year (the allocator reads it at the three-yearly reshuffle) |
| last spring's prices rebuilt from today's | 6 | byte identity only — ⚠️ the loaded year did not part in any pose: the copy and today's classes agreed there. Kept exact anyway |
| a site's delivered materials | 3 | the run-on year, field compare, byte identity |
| `_nextWorkplaceId` | 8 | field compare, byte identity |
| the waiting-site latch | 6 | field compare, byte identity |
| `Moments` | **0 → 2** | ⛔ zero until the emptied-village pose existed |
| *share the work out* | 5 | the hash, the run-on year, field compare |
| the plots | 12 | field compare, byte identity |
| the RNG | 24 | everything |
| the `.bad` copy | 6 | the damage cases |
| `_routesDirty` | 7 | the run-on year, field compare |
| the *nowhere to keep it* latch | **0 → 3** | ⛔ zero until the closed-stores pose existed; then the run-on year's **log** caught it (said twice) |
| a new field nobody saved | 1 | `EveryFieldIsSavedOrNamed`, by name |

⭐ **The run-on year alone would have missed seven of the twelve dropped fields**, and §4's own hash guard would have
missed all but two. The field guard is the one this feature rests on, as §9.4 said it would be.

## 10. Out of scope

- **Ironman mode** (one save per village, overwritten, no going back). Joe wants it later, under a better
  name. It is a mode over this file (§7's autosave with no rotation and no *Save as…*), not a second
  format.
- **Migrations** from an older `format`. Not before v1 (§2).
- **Replays and an input log.** These may come back one day as a debugging tool, or for co-op (§3 of
  DESIGN), but not as a save.
- **The title and pause screens**, which are §4's next shell step. This slice puts *Continue* and
  *Load…* on the new-game screen until then.
- **Cloud sync, and saving mid-tick.**

## 11. Joe's calls before the build — ✅ answered (D508, 2026-10-05)

1. ✅ **Autosave cadence: every year at Spring, Day 1** (a year is the game's own rhythm, 480 ticks, about
   eleven minutes at 1×). ⏸️ *"user can adjust this in full settings later"* — the cadence becomes a
   player setting when a full settings screen exists (§7).
2. ✅ **Three autosaves kept.**
3. ✅ **Save sits in Settings under *How the village runs*** — *"until the pause screen exists (and full
   settings exist)"*; it moves there when they do.
4. ✅ **The words**: *Continue*, *Load…*, *Save*, *Save as…*, and the refusal sentences in §7's table
   (Joe asked to see them; shown to him in D508's session).

## 12. Order of work

1. ✅ **This spec** (D507), with §3's measurement; Joe's answers (D508).
2. ✅ **The sim half** (D509): `SaveGame`, `SaveFile`, `SaveData`, each class's `ToSave` / `FromSave`, the
   capture and restore in `SimWorld`, and guards §9.1–9.7 (`SaveLoadTests`, 47 cases). Load time and file
   size measured into §3.
3. ✅ **The view** (D509): *Continue*, *Load…*, a name and *Save* in Settings, autosave at the year's turn
   and on quit, the share-out tick set from the loaded village, and the probe's `save:` line.
4. ⏸️ **Joe plays it.**

## 13. Definition of Done

1. This spec current, status line true; §3 carries the snapshot's measured load time and §6 the file
   size.
2. Guards §9 written, red-checked by **deleting one saved field at a time**, with the reds counted.
   Determinism green, and **no golden moved** (a save is read, never mixed).
3. View builds with 0 warnings; probe green: `bar height` 151, `new game:`, `settings:` and `save:` ✅,
   `done.` printed.
4. Joe has played it: plays a village a few years, saves under a name, quits, *Continue* opens it where
   he left it, *Load…* opens the named one, *share the work out* off survives the trip, and a hand-broken
   save is refused in words and kept as `.bad`.
5. DESIGN §4, §6 and §7, and `HANDOFF.md`, updated in the same commits as the code.
