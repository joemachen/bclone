# Spec: Crops — the year you can watch

**Decisions:** D161 (this document's reason for existing), D16 (numbers are derived, not picked),
D19/D39 (raw food sources beyond one patch), D85 (`SetTerrain` is the one door), D86/D118/D146
(work ground), D112 (the surface rule), D127 (a painted zone is a standing instruction), D152
(goldens go last).
Neighbours: `environment-and-seasons.md §5.1` (**superseded by this**), `buildings-plan.md §8.1`
(which predicted this shape and left the choice open), `food-catalog.md §7` (do not build a
recipe tree), `professions.md` (fish and meat are `Goods.Food`), `forests-and-gathering.md` (the
mechanic this is a sibling of).
**Status:** ⚠️ **Specced, not started.** Written before the code (METHODOLOGY §2).

> **⚠️ This status line is load-bearing. Update it the day the slice merges** — D159 found five
> specs claiming "not started" for systems that had shipped, one of them for the slice merged
> that morning, and CLAUDE.md now requires a spec's status to be checked against the suite.

---

## 1. Goal

**The year becomes something you watch happen, instead of a number that changes.** Ground is
painted for a field, sown in spring, grows through summer, is reaped in autumn, and lies in
stubble through winter. The granary fills from a harvest rather than from a trickle.

This is the last slice of Phase 2 and it **replaces `environment-and-seasons.md §5.1`**, which
proposed a seasonal *yield curve* — three multipliers on foraging.

**Why the curve is the wrong answer, in one line:** §5.1's own description of what the player
would see is *"a villager simply comes home with more in autumn."* **That is invisible.** It
needs no UI because there is nothing to look at, which was argued there as a virtue and is
actually the objection — it is a number going up where the player cannot watch it, the same shape
D37 rejected spoilage for and D45 rejected winter severity for. **A crop is the same idea made
visible: the field looks different in every season, and the difference is the mechanic.**

**§5.1's stated target survives intact**, because it was right and it is how the numbers get
derived rather than picked (D16):

> *A household working normally through spring, summer and autumn fills its winter store by the
> first day of winter — with the leanest of the three still leaving it able to eat that season.*

## 2. Which pillar, and which non-negotiables

- **§2.2** — a second raw food source, which D19 says is *structural rather than content*: a
  catchment can only bind if a distant household has something nearby to work.
- **§1.1 Legibility** — the primary claim. A field states its condition by looking like it.
- **§1.2 Meditative pace** — sow once, reap once. **A field is not a thing you babysit.** If
  crops add a per-season click, the design is wrong.
- **§1.5 Generational time** — weakly here, strongly in the orchard slice (§8).
- **§2.3** — soil exhaustion is the natural second axis and is **deliberately not in this slice**
  (§7).

---

## 3. The shape — a zone plus a steading, and this closes an open question

`buildings-plan.md §8.1` argued crop fields should be **brushes, not a farm building with a
radius**, and left the resolution open:

> *"Likely resolution — a zone plus a small barn/steading that is the workplace, with the zone
> defining the work's extent. Worth deciding deliberately rather than by default."*

**Decided: that resolution, and it costs no new mechanic, because the game already has it.** A
forester's hut is a workplace whose extent is painted work ground (D86, D118), whose brush is a
standing instruction rather than a one-off order (D127), and whose planting is on by default with
felling as the toggle (D146). **A farm is the same object with a different verb.**

| | Forester's hut | Farmhouse |
|---|---|---|
| Workplace | ✅ exists | `JobKind.Farmer`, `BuildingKind.Farmhouse` |
| Extent | painted work ground | **the same** `PaintWorkGround` |
| Seats | `RequiredForesterSeats` | derived the same way |
| Yield scales with | `WoodedTilesAround` | **sown tiles in the zone** |
| Idle reason | `IdleNote` | **the same** — a farm with no ground says so |
| Local store | ⚠️ dead | **✅ the first one that is real — see §3.1** |

**What this buys, stated so it is not re-litigated:** work-ground allowance
(`WorkGroundAllowanceFor`), the overstretched warning, the labour quota, the idle ring on the
map (D147), refusal sentences (D43) and the build queue all apply on day one, because they are
properties of *a workplace with painted ground* and not of forestry.

### 3.1 ⭐ The farm's own store — and it wires up the fifth element of the role model

Joe: *"the harvested goods go to the granary although the farm itself can store up to 100 of the
harvest goods by default."*

**That is bigger than a config default.** `professions.md §4` lists every profession as the same
five things — a `JobKind`, a building the player places, seats, **a local store with a stated
cap**, and a destination for its output — and records that the fifth *"exists and is dead"*:
`Workplace.Store` has been on the type since D30, is uncapped, has **never been written to by
anything in the sim**, and the building panel has a branch for it that can never be true. D107
called wiring it up *"the first real slice of the model."*

**The farm is where it gets wired up.** A harvest is exactly the case that needs it: reaping is
bursty, the granary is across the village, and a farmer who walks every armful back individually
is D10's teleport-with-extra-steps in reverse.

- **`farm_store_cap: 100`**, in data, per D16 and because a modder should be able to touch it.
- **The store is a buffer, not a destination.** The granary is still where food lives; the farm
  holds a harvest until it is carried. **A full local store must not destroy the overflow** —
  that is D96 and D144's shape exactly, twice-shipped, and §6 gives it a seam guard.

### 3.2 Who carries it (Joe, 2026-08-15)

1. **The farmer reaps and hauls to the nearest available storage.** Nearest *with room* — so the
   farm's own store fills first because it is underfoot, and once it is full the walk gets
   longer. **That is what makes the 100 mean something**: the buffer is free, and running it dry
   is the market's job.
2. **The marketer collects harvested goods from farm storage as well as granaries.** Its
   destination is unchanged (households below target, D36); what widens is where it may *source*
   from.
3. **⛔ Laborers do not move farmed goods** — not to granaries, not to markets. Hauling stays
   what it is today: building materials. **Farm food moves by farmer or by trader, or it sits.**

**⚠️ The marketer cannot currently see a workplace store at all.** `NearestStoreAccepting` and
`NearestStore` iterate `StoreBuildings` only; `Workplace.Store` is a different list. Ruling 2 is
therefore a real change to the market's reach, not a config flip — and it must not become a
second way to find a store (D145: *a control is safe when its state is read at a chokepoint, and
at risk the moment there are two ways to do the thing*).
- **⚠️ This changes `professions.md §11`'s stated order**, which planned to prove the local store
  *"on the forester and woodcutter — the two professions that already have one."* It is proved
  here instead, on Joe's call. That spec's ordering should be corrected rather than left to
  disagree, which is D159's whole lesson.
- **The dead panel branch becomes reachable**, so it needs looking at rather than trusting: it
  has never once rendered.

---

## 4. Data model

**Crop state lives in `Terrain`, through `SetTerrain` (D85's one door).** `Terrain.Sapling` is
the precedent: a tile that was planted, is not yet what it will become, and grows on the
regrowth sweep.

| New terrain | What it is | Becomes |
|---|---|---|
| `Field` | Ploughed, bare. Winter and early spring. | `Sown` when a farmer sows it |
| `Sown` | Planted, nothing to take yet. | `Ripe` on the growth sweep, if it survives |
| `Ripe` | Standing crop, harvestable. | `Field` when reaped |

- **Passable and buildable-refusing.** Unlike `Water` they are walked over; unlike `Grass` a
  building marked on one should warn, because it destroys a year's work.
- **`TerrainRules.Yields(Ripe) => Goods.Food`**, which makes a ripe field harvestable by the
  machinery that already harvests a wood — and **that is the seam to be careful about** (§6).
- **No new `Goods`.** Crops are `Goods.Food`, consistent with `professions.md`'s ruling on fish
  and meat, and `food-catalog.md §7`'s warning against a recipe tree. Varieties of crop are
  flavour and unlock, not new goods.
- **⭐ ONE CROP, IN A MODEL SHAPED FOR MANY** (Joe). The terrain triple says *what stage this
  tile is at*; a **`CropId` per tile** says *what is growing on it*, and the crops themselves
  live in **data** (`data/`), not in the enum — CLAUDE.md's rule, and the assumption that a
  modder will want to touch them. **Exactly one crop is defined to begin with.** The cost of
  deferring this is the thing being avoided: retrofitting an id onto a shipped terrain triple
  means touching the hash, the goldens and every call site at once, where adding a *row to a
  data file* later costs nothing.
  - ⚠️ **The id is sim state, so it is hashed** (D51's rule) — and hashed **sparsely**, the way
    zones and ground stacks are, so a village with no fields mixes nothing at all.
- **The map golden does not move**, and this is checkable: the generator never produces these
  terrains, so a generated valley is byte-identical. **The two 50-year village goldens *will*
  move**, once, deliberately, in their own commit with a stated reason (D152).

**Growth runs on the existing `RegrowthSystem` sweep**, whose slice is a function of `world.Tick`
— *no cursor to store and nothing new to hash*. Crops must not introduce one either.

---

## 5. The calendar

`SeasonRules` (`src/Bclone.Sim/World/Season.cs`) is where "what can be done now" lives — it was
created in D159 for exactly this kind of question, and it currently answers one.

| Season | The field | The farmer |
|---|---|---|
| Spring | `Field` → `Sown` | **Sows.** The year's one commitment. |
| Summer | `Sown`, growing | Tends — and this is where *the hands are free for something else* |
| Autumn | `Sown` → `Ripe` → reaped | **Reaps.** The granary fills. |
| Winter | `Field`, stubble | Nothing. The farmer is a spare hand. |

**The winter consequence is the point and is inherited from §5.1's argument:** when there is
nothing to farm, the marginal hand is worth more at the wood. That is §2.2's stated advantage
arriving as a *consequence* rather than as a rule.

**⚠️ Sowing missed is a year missed.** A village that fails to sow in spring does not get a
second chance in summer — that is what makes spring a decision. It must be **said, early**
(the village log, and the farm's `IdleNote`), because §1.1 forbids a village dying of something
it could not have seen coming (D88).

### 5.1 ⭐ Use it or lose it — a ripe field nobody reaps rots over winter (Joe)

**And the load-bearing half is the warning, not the loss.** Use-it-or-lose-it is only fair if the
player could see it coming, so this is two things and the first matters more:

1. **Before — while it can still be acted on.** Autumn, crop standing ripe, nobody reaping: the
   farm says so on its panel, takes the idle ring on the map (D147), and the village log says it
   **once, on the edge** (D123 — narrate on beginnings and clearings, never a permanent alert).
2. **After — when winter takes it.** **One** village-log line naming how much food was lost. One
   line for the event, *not one per tile*, or the log becomes the receipt roll Phase 1's QA
   checklist rules out.

**Why this gets a rule rather than a shrug: this project has shipped goods silently leaving the
world twice.** D96 found 17,451 food deposited into a full granary and out of existence; D144
found firewood being destroyed once the woodyard filled. Both were invisible, both were found by
Joe playing rather than by the suite. **A rotting harvest is that same shape on purpose**, which
is exactly why it must be the one case that says so out loud.

---

## 6. Failure modes, and the seams to guard

**D161's rule: when two systems meet, the golden goes over the seam, not over either side.**
Crops meet five existing systems, and each meeting is where the bug will be.

### 6.1 ⛔⛔ THE ONE THAT WILL BITE: two food totals that have never disagreed, and are about to

**Found while speccing, before a line of code — which is the whole argument for spec-first.**
The village has two ways to ask how much food it has, and **writing to `Workplace.Store` for the
first time in the project's history makes them diverge by up to `farm_store_cap` per farm.**

| Reader | Source | Sees farm stores? |
|---|---|---|
| `FoodInGranaries()` → `TotalAccepting` | `StoreBuildings` only | ❌ **no** |
| `TotalFood()` → `AllStores()` | households + **workplaces** + stores | ✅ yes |

**Four load-bearing things read the blind one:**

| Site | What it decides |
|---|---|
| `HouseholdSystem.cs:573` | **the birth gate** |
| `BehaviorSystem.cs:1477` | whether the village has any reason to gather at all |
| `LabourQuota.cs:293` | the food stock limit |
| `SimWorld.FoodTheVillageHasRoomFor` | how much room there is for more |

**So a village whose food is sitting in farm stores believes it is poorer than it is, and stops
having children.** That is **D155's exact symptom** — Joe: *"They aren't having any kids?"* —
arriving from a new direction, and **D81's exact bug**: one comparison asking two different
questions, which cost a century of one household resting. D81 is recorded as *"D76's seam for the
fifth time."* **This would be the sixth.**

**The rule, and it is D81's own:** the question and the thing it is compared against must be the
same question. *"How much food does the village have?"* includes a full farm store — the food is
real, it is reachable, and a trader will come for it. **The four readers above must see workplace
stores**, and the guard is a village with a full farm store and an empty granary that still
passes its birth gate. **Checked red first**, against the current code, where it will fail.

| Seam | The failure it invites |
|---|---|
| **Crops × the harvest brush** | `Yields(Ripe) => Food` makes a ripe field look like a wood to `NearestHarvest`. **A laborer clearing painted ground must not reap the farm**, and D157's footprint-priority pass must not target a field. |
| **Crops × building placement** | A house or a hut marked on `Sown` ground destroys a year's food. Refuse, or warn loudly (D43's pattern). |
| **Crops × the labour quota** | A farmer is wanted in spring and autumn and not in summer or winter. **`SetStaffing` is a ceiling, not a summons** (D146) — the quota has to *want* farmers seasonally, or the fields sit sown and unreaped. |
| **Crops × the food economy** | `VillageEconomy` derives one `gather_yield` against a worst-case walk. A second food source with a different rhythm changes what "the village can feed itself" means, and the derivation has to be re-stated rather than quietly out-voted. |
| **Crops × the farm's local store** | A full 100-cap store must **refuse** the overflow, not swallow it. `Stockpile.Add` returns what it actually took and **the caller has to read it** — D96 is precisely the bug of not reading it (17,451 food out of the world), and D144 is the same shape one deposit path over. This store has never been written to, so it has never been tested. |
| **Crops × the market** | The marketer must reach `Workplace.Store` (§3.2 ruling 2) **without becoming a second way to find a store**. One door, or it is D144's shape again — a rule answered by one path and ignored by another. |

**Other failure modes:**
- **A crafting minigame.** `food-catalog.md §7` is explicit. No recipes, no per-crop unlocks.
- **A per-season click.** If the player has to tell the village to sow each spring, §1.2 is
  broken. **The zone is a standing instruction** (D127): painted once, sown every year until
  unpainted.
- **A field that fails silently.** Every refusal and every un-sown spring writes its reason.

---

## 7. Not in this slice, and why

- **Soil fertility, rotation and fallow.** `buildings-plan.md` lists rotation as a *knowledge
  node*, and §2.3 wants exhaustion as a pressure axis. **It needs the tech tree (Phase 4)** and
  it would double this slice. The data model above leaves room: fertility is a per-tile number
  when it arrives, which is `mutable-terrain.md`'s surface rule again.
- **Orchards** — see §8.
- **New crop varieties.** Wheat/barley/rye/roots/cabbage/flax are in `buildings-plan.md §4` and
  are **flavour and unlock**, not mechanics. One crop, well, first.
- **Milling, baking, brewing.** The processing tier (D39) is a later chain of D29's shape.

## 8. Orchards — deferred, with the reason

The queue item was *"crops and orchards"*. **Orchards are a separate slice and should not ride
along**, because their whole point is different: `buildings-plan.md` calls an orchard *"slow to
mature — a planting whose payoff is a generation out, which is §1.5 in miniature."*

**That makes an orchard a generational-time mechanic wearing a farming costume**, and it deserves
to land where it can be felt — beside apprenticeship in Phase 3, where the player is already
being asked to think past one lifetime. Building it here would spend it as a food source.

---

## 9. How it is tested

- **Spec-first, then a failing test, then the code** (METHODOLOGY §3).
- **Anti-vacuity on every guard (D7), checked red.** A village that never sows must measurably
  starve harder than one that does, or the whole slice is decorative. D159's rule: *do not
  believe a guard you have not seen fail.*
- **A golden over the seam** (D161), not over either side: the case to write with the feature is
  **a laborer clearing painted harvest ground beside a ripe field** — the crops × brush seam,
  which is the one that will silently eat a harvest.
- **Against the shipped config, not only the fixture** (METHODOLOGY §3, and six recorded
  instances of the two diverging).
- **The determinism test stays green** — P0. Crops add terrain to a hashed map and a growth sweep;
  both are places a desync would live.
- **The 300-year acceptance run** still stands at the end, and **D143 still holds**: an unattended
  village is supposed to die out. A village that never sows is *not* a bug.

## 10. Definition of Done

1. This spec current, and its status line corrected on merge.
2. A field can be painted, is sown in spring, ripens, is reaped in autumn, and refills the
   granary — watched, not inferred.
3. `environment-and-seasons.md §5.1` struck through and pointed here.
4. §5.1's derived target met: a household working normally fills its winter store by the first
   day of winter.
5. The four seams in §6 each have a guard, each checked red first.
6. Determinism green; the two village goldens re-taken **last**, in one commit, with one stated
   reason each (D152).
7. `DESIGN.md` §6 and §7 updated.

## 11. ✅ Resolved — the three questions (Joe, 2026-08-15)

1. **A farm has a building, and the field zone is painted and worked by the farmers** — sown and
   reaped. **Harvest goes to the granary, and the farm holds up to 100 of it locally by
   default.** §3 and §3.1. The local store is the answer with the longest reach: it wires up the
   fifth element of `professions.md`'s role model, dead since D30.
2. **One crop for now, structured to plan for many** — a `CropId` per tile from the start, crops
   defined in data, exactly one of them defined. §4.
3. **A ripe field nobody reaps rots over winter — use it or lose it.** §5.1, where *"loudly
   mourned"* is replaced by something actionable: **a warning in autumn while it can still be
   acted on**, then one log line when the loss happens.

## 12. Still open

- **Where the derived numbers land.** `farm_store_cap: 100` is Joe's stated default; the yield
  per sown tile, the seats per farm and the work-ground allowance are all **derived** against
  §1's target, not picked (D16), and none of them is derived yet. **Expect the fixture and the
  shipped config to disagree at least once** — that has happened six times and METHODOLOGY §3
  exists because of it.
- **What a farm store full of food does to the winter forecast and the market's priorities.**
  §6.1 makes the four blind readers see it; whether the *market* should prefer draining a farm
  over a granary is a separate question and is not answered here.

*(The hauling question — farmer versus laborer — was open here and is resolved in §3.2.)*
