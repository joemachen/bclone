# Spec: The goods catalogue — a good becomes a row

> Status: **slices 1 and 1b BUILT; §4.0's carried-load bug PROVED AND FIXED (D211); slice 2
> (prove a modder can) outstanding.** Owner: Joe + Claude Code · Pillar: `DESIGN.md §3`
> (data-driven, first-class modding) · Format per `METHODOLOGY.md §2`.
>
> Neighbours: `skills-catalog.md §4.1` (**the template — a row, not an enum value**),
> `crops-and-orchards.md §4` (the other one), `storage-and-distribution.md`,
> `content-inventory.md` **finding 8** (why this is now the gating item).

---

## 1. Goal

**Let a good be defined in data**, so the content in `TECH-EXAMPLE.md` can exist and so a modder
can add one — which is the promise `DESIGN.md §3` has carried since day one and D168 sharpened:
*"modders should be able to add buildings, essentially add anything to the game."*

**The gap it closes** (`content-inventory.md` finding 8): the game has **6 goods** and the content
pass needs roughly **35**. `Goods` is a C# enum hashed by position and pinned by every golden — so
today a modder can change the numbers on the six that exist and **cannot add a seventh.**

---

## 2. ⭐ Joe's two calls, 2026-08-24 (D210)

### 2.1 The enum survives, as the built-in six

**Not deleted.** Goods `0–5` stay as enum values, so the ~346 existing `Goods.Food`-style
references keep their readability and the hash keeps its order. **What changes is where
*behaviour* comes from**, and that the set is **open above 5**.

> **⛔ The rule, taken straight from `SkillRow`: NOTHING IN THE SIM MAY SWITCH ON A GOOD BY NAME.**
> What a good *is called*, *where it comes from*, *what it yields* and *who stores it* comes from
> **the row** — otherwise the row is decoration over an enum that still exists, just spelled
> differently.

⚠️ **This is a narrower promise than `SkillRow`'s and it is stated rather than glossed:** skills
have no enum at all. Goods keep one **because `Stockpile` indexes by it and the whole economy names
food directly** — `TotalFood`, the birth gate, the quota, granary capacity. *The enum is an alias
for the first six ids, not a second source of truth.*

### 2.2 Food varieties stay `Goods.Food`

Fish, meat, wheat, cheese and apples remain **one good**, per `professions.md` (Joe) and
`crops-and-orchards.md §4` — *varieties are flavour and unlock, not new goods.* **This is what
takes the target from ~70 to ~35.**

⚠️ **The condition on that ruling is written down because it will expire.** `professions.md` says
*"nothing yet distinguishes them mechanically"* — and `DESIGN.md §5`'s **foods with different
nutritional values** would. **When that lands, every reader of *how much food has the village got*
has to ask a capability question instead of naming a good** — D76's seam, on the one axis the whole
economy is derived from. **Not now, and the catalogue must not make it harder.**

---

## 3. The row

```
GoodRow
  Id            int      appended, never renumbered — 0..5 are the built-in six
  Name          string   the display word: "food", "logs", "firewood"
  SourceName    string?  what it is taken from: "woodland", "a stone seam". Falls back to Name
  YieldPerTile  int      what one tile of its source gives. 0 for goods nothing harvests
  StoredBy      flags    which StoreKinds accept it
```

**Ids are hashed by position and appended, never renumbered** — the rule `SkillRow`, `JobKind` and
`Terrain` are all pinned by. Renumbering silently reinterprets every golden and every seed.

---

## 4. What moves into it — the whole good-behaviour surface, which is smaller than it looks

**Only 14 switch arms in the sim branch on a good.** They decide four things:

| Today | Where | Becomes |
|---|---|---|
| Display name | ⛔ **`Stockpile.Name` AND `SimWorld` — two places, same words** | `Name` |
| Harvest source name | `SimWorld.Describe` | `SourceName` |
| Per-tile yield | `SimWorld`, reading three config keys **nothing else reads** | `YieldPerTile` |
| Who stores it | `StoreBuilding.KindAccepts` | `StoredBy` |

### 4.0 ✅ FIXED IN ITS OWN SLICE (D211) — A THIRD CEILING, AND IT WAS LIVE: A VILLAGER COULD NOT CARRY STONE

**Found while checking that no switch on a good survived.** Three comparisons remained in
`BehaviorSystem`, and they were not lookups — they were the villager's **carried load**, which was
**three named fields** rather than an indexed stockpile:

```
public int CarriedLogs   { get; set; }
public int CarriedFirewood { get; set; }
public int CarriedFood   { get; set; }
```

**D82 made every *store* an indexed array. It never reached the villager's arms.** So the six goods
that existed were not equally real: three could be carried, three could not.

**⛔ And the clearing path destroyed what it took.** In `VillagerState.Clearing`:

```
villager.CarriedLogs += goods == Goods.Logs ? taken : 0;
int left            =  goods == Goods.Logs ? amount - taken : 0;
```

`Harvest` has **already set the tile to Grass** — the seam is spent. For stone or iron, nothing was
carried *and* nothing was left on the ground. **The yield simply stopped existing.**

⚠️ **Reachability was the part still to prove, and it was measured rather than reasoned.**
`HasSomethingToHarvest` is `TerrainRules.Yields(...) is not null`, which is true for `Rock` and
`IronDeposit` — so a painted seam *is* selectable by `NearestHarvest`, and `HarvestBrush.Stone`
exists as a paint mode. **That said the path was open, not that a village walked it.**

#### ⭐⭐ The red check, and it went red on the first run

`CarryingSeamGoodsTests.WhatALaborerClearsReachesAStore` paints the **eight nearest reachable
tiles** of a seam, runs the fixture village for two years, and asserts twice — *were any cleared?*
and *did any of it reach a store?* **The first assertion is what tells latent from live**, because
an unreachable seam and a destroyed yield look identical from the store.

| | before | after |
|---|---|---|
| stone seams cleared in 2y | **8 of 8** | 8 of 8 |
| stone in stores | **0** | **96** (8 × `yield_per_tile` 12) |
| stone on the ground | **0** | 0 |
| iron seams cleared in 2y | **8 of 8** | 8 of 8 |
| iron in stores | **0** | **64** (8 × 8) |

**Live, not latent** — nothing had to be added to the path a laborer walks to a painted seam.

#### The fix: the arms became the indexed load D82 never gave them

`Villager.Carried` is a `Stockpile`, sized from the run's catalogue like every larder, shed and
cart, and **the named readers survive as readers** — exactly the split `Stockpile` itself records,
where `Food`/`Logs`/`Firewood` stayed and the named *mutators* went so the compiler makes every
write say which good it means. Everything *generic* about a load is a loop over the catalogue now:
setting down, depositing at a store, tidying a heap, the audit trail, and which store a load is
walking to.

⚠️ **The forester's fell had the same test and the same leak** (`felled == Goods.Logs`), so a seam
on a hut's painted work ground was spent for nothing too — the second of the two felling paths
D133 already found drifting apart.

⭐ **The goldens moved for the hash's shape and NOT for the village, and that is measured.**
`MixVillager` mixed three named goods and mixes the whole carried stockpile now, so all five
village goldens move. **Restoring the three old lines on top of the finished fix makes every one
byte-identical again** — nothing paints a seam in an unattended run. Re-taken last, one commit,
one stated reason (D152).

⚠️ **And the stale comment mattered.** `HasSomethingToHarvest` still said *"only forest today;
stone and iron are D84's finite deposits and land next"* — untrue since the seams shipped, and read
as evidence while this section was being written. **A comment that lies is worse than no comment,
because it is believed at the moment somebody is orienting.** Fixed, with the old sentence recorded
beside it — D159's finding, in a doc comment rather than a spec status line.

### 4.1 ✅ FIXED — and it was a latent bug, found while counting

`BehaviorSystem.HeldOf` **was** a hand-written switch:

```
Goods.Food => store.Food,
Goods.Logs => store.Logs,
_          => store.Firewood,      // ⛔ stone, tools and iron all land here
```

**`Stockpile` already has `this[Goods]`, which handles every good correctly.** `HeldOf` duplicates
it and its default arm answers *"how much stone?"* with **the firewood count.**

- **It was latent, not live** — traced rather than assumed: all six callers reach it only with
  Food, Logs or Firewood, the three it got right.
- ⛔ **It would have gone live the moment a builder asked for stone**, which is exactly what
  multi-material recipes bring (`content-inventory.md` finding 2).
- ✅ **Deleted in favour of `Stockpile`'s indexer**, which handled every good correctly the whole
  time — *the helper you need may already exist.*

---

## 5. Slices

Following **D82's shape**, which did this once already and recorded why it worked: *"the refactor
moved nothing, and that is what made it safe to build on."*

### ✅ Slice 1 — the catalogue exists and the words come from it

**Done.** As a provable no-op:

- `GoodRow`, `GoodsCatalog` and `SimConfig.GoodsCatalog`, defaulting to today's six.
- **Three of the four switch surfaces read the row**: display name, source name, per-tile yield.
  The three loose config keys (`logs_per_forest_tile`, `stone_per_rock_tile`,
  `iron_per_deposit_tile`) are gone — *read by one switch and nothing else, which is what made
  moving them safe*.
- **`Stockpile.Name` deleted.** ⭐ Every arm of it produced exactly what its own default arm
  already did — three hand-written lines restating the fallback.
- Validation at load: duplicate ids, missing built-ins, a good no store will take, **and both
  ceilings below**.

### ✅ Slice 1b — the open set

**Done, in three commits, each verified green before the next was started.** Both ceilings lifted:

| Was | Now |
|---|---|
| **6 goods** — `Stockpile` sized from `Enum.GetValues<Goods>().Length`, in a field initializer | **Sized from the catalogue.** `Stockpile` takes a slot count and has **no parameterless constructor**, so the compiler listed all 20 sites; the stockpile is `required` on households, store buildings and workplaces |
| **30 goods** — `AllowedGoods` an `int` with `Spoken` at bit 30 | **62 goods** — a `long` with the sentinel at bit 62, and **both halves hashed** |

⛔ **A mutable static was never the fix**, for the record: the suite runs **~9.5× parallel with a
world per test**, so a global count set at load would be a cross-test race and a determinism
hazard — the one class of bug this project treats as P0.

**⭐ And the goldens did not move for the widening — the guard's doing rather than luck.**
`AllowedGoods` is hashed only when non-zero, and zero is *"the player has not said"*, which is
every store in an unattended fifty-year run.

**Also landed:** `KindAccepts` reads `stored_by` off the row — the catalogue is held **on the store
building**, because `Accepts` has **46 call sites** and a store building has **8** — and `HeldOf`
is deleted in favour of `Stockpile`'s indexer.

⚠️ **One compiler catch worth recording:** widening the mask surfaced `mask &= ~bit` where `bit`
was still an `int`. Negated and then widened, it **sign-extends and wipes the sentinel.** CS0675
caught it — *the kind of bug that would have read as a filter forgetting itself.*

### Slice 2 — prove a modder can

**A test that defines a seventh good entirely in data** and drives it through storing, limits,
hashing and a store's acceptance. ⛔ **No new good ships into the game** — the proof is the test,
not content nobody asked for.

⚠️ **Slices 1 and 1b are unproven until this exists.** D82's lesson is that *the new good is what
proves the refactor*; here the test plays that part.

---

## 6. ⛔ Failure modes

| Failure | Guard |
|---|---|
| **The row is decoration** | §2.1's rule — nothing switches on a good by name. A grep for `Goods.X =>` should find nothing in the sim after slice 1 |
| **Two sources of truth** | The enum is an **alias for the first six ids**. `Stockpile.Kinds` comes from the catalogue, never from `Enum.GetValues` |
| **A silent hash change** | Ids 0–5 keep their values and their order. **Goldens byte-identical or the change was not what it claimed** |
| **A mod desyncs a save** | *Same seed + same content ⇒ same history.* Mod goods enter the hash **in id order**, and ids are stated in data rather than inferred from file position |
| **A good with no home** | A good no `StoreKind` accepts can be produced and never stored. **Validate at load**, the way `SimConfig` already validates skills |

---

## 7. Determinism

- **Integer only** (D2). The catalogue is content, read once at load.
- **Hashed in id order.** `MixStore` already iterates `0..Kinds` — it keeps doing exactly that, with
  `Kinds` now coming from the catalogue.
- ⚠️ **`Stockpile` arrays become runtime-sized.** They already are (`new int[Kinds]`); only the
  source of `Kinds` moves.

---

## 8. Definition of Done

1. This spec current.
2. **Defaults in code, overridable from the config** — the pattern `Skills`, `HouseholdNames` and
   `TownNames` already use, and **one source of truth rather than two.**
   - ⚠️ **`content-inventory.md` finding 6 called this a mistake and that was too strong.** Writing
     the rows into `data/sim.config.json` *as well* would recreate exactly the fixture-versus-shipped
     drift METHODOLOGY §3 warns about — the gap that produced D48, D49 and D50. **The discoverability
     complaint stands** (a modder reading the data file sees no goods and no skills); *the fix is a
     comment pointing at the defaults, not a second copy of them.*
3. ✅ No `switch` on a good by name anywhere in `Bclone.Sim`. ✅ **And the three comparisons that remained are gone (D211)** — they were the villager's carried load, which is an indexed stockpile now. See §4.0.
4. ✅ `HeldOf` deleted; the indexer used.
5. Unit tests passing; **determinism test green; both fifty-year goldens byte-identical** — ✅ true for slices 1 and 1b. ⚠️ **D211 moved five goldens deliberately**, and its own §4.0 records the measurement that says the village underneath them did not move.
6. Slice 2's data-defined-good test passing.
7. `DESIGN.md` Progress Tracker + Decisions Log updated.

---

## 9. Open

1. **Do `JobKind` and `BuildingKind` follow?** The same argument applies — ~40 roles and ~45
   buildings against 6 and 10. **This spec deliberately does one axis first**, because goods is the
   one the content pass needs most and the one with a proven precedent (D82).
2. **`StoredBy` as flags or as a list per store kind?** Flags are cheaper; a list reads better in
   data. Decide at implementation.
3. **⚠️ NOW THAT STONE ACCUMULATES, NOTHING SPENDS IT — AND A SHED'S CAPACITY IS TOTAL** (found by
   D211, not fixed by it). `Stockpile.Capacity` is physical room *across every good*, deliberately
   (*"a shed packed with logs has nowhere to stack firewood, and being made to choose is the
   interesting part"*). Stone has no consumer: `BuildingRecipe` is **one material slot**
   (`content-inventory.md` finding 2). **And the harvest brush answers to no stock limit** — it is
   a standing instruction the player paints and only the player unmarks (D127), which is right, and
   which means *paint a seam and the shed fills with stone the village cannot spend.* Not a bug in
   the fix — the yield existing is strictly better than it vanishing — but the pressure is real and
   its answer is multi-material recipes, not a cap here.
4. **Does a mod-added good need a display colour?** The view has one per good
   (`Main.cs`). Out of scope for slice 1 — the sim does not care — but it is the first thing a
   modder will ask for.
