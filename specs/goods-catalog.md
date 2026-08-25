# Spec: The goods catalogue — a good becomes a row

> Status: **slices 1 and 1b BUILT; slice 2 (prove a modder can) outstanding.** Owner: Joe + Claude Code · Pillar: `DESIGN.md §3`
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

### 4.0 ⛔⛔ A THIRD CEILING, AND THIS ONE LOOKS LIVE: A VILLAGER CANNOT CARRY STONE

**Found while checking that no switch on a good survived.** Three comparisons remain in
`BehaviorSystem`, and they are not lookups — they are the villager's **carried load**, which is
still **three named fields**, not an indexed stockpile:

```
public int CarriedLogs   { get; set; }
public int CarriedFirewood { get; set; }
public int CarriedFood   { get; set; }
```

**D82 made every *store* an indexed array. It never reached the villager's arms.** So the six goods
that exist are not equally real: three can be carried, three cannot.

**⛔ And the clearing path appears to destroy what it takes.** In `VillagerState.Clearing`:

```
villager.CarriedLogs += goods == Goods.Logs ? taken : 0;
int left            =  goods == Goods.Logs ? amount - taken : 0;
```

`Harvest` has **already set the tile to Grass** — the seam is spent. For stone or iron, nothing is
carried *and* nothing is left on the ground. **The yield simply stops existing.**

⚠️ **Reachability is the part still to prove, and it must be measured rather than reasoned.**
`HasSomethingToHarvest` is `TerrainRules.Yields(...) is not null`, which is true for `Rock` and
`IronDeposit` — so a painted seam *is* selectable by `NearestHarvest`, and `HarvestBrush.Stone`
exists as a paint mode. **That says the path is open, not that a village walks it.**
*(Its own doc comment still says "only forest today; stone and iron land next", which stopped being
true when seams shipped — so the comment is not evidence either way.)*

⛔ **Deliberately NOT fixed in this slice.** Making stone accumulate is **a behaviour change, not a
refactor** — it would move the goldens, and it belongs in its own slice with a **red check first**:
*write the guard, watch it fail, then fix it.* Folding it into a no-op refactor is exactly how a
real change hides inside a safe one.

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
3. ✅ No `switch` on a good by name anywhere in `Bclone.Sim`. ⚠️ Three comparisons remain and are NOT lookups — they are the villager's carried load, which is still three named fields. See §4.0.
4. ✅ `HeldOf` deleted; the indexer used.
5. Unit tests passing; **determinism test green; both fifty-year goldens byte-identical.**
6. Slice 2's data-defined-good test passing.
7. `DESIGN.md` Progress Tracker + Decisions Log updated.

---

## 9. Open

1. **Do `JobKind` and `BuildingKind` follow?** The same argument applies — ~40 roles and ~45
   buildings against 6 and 10. **This spec deliberately does one axis first**, because goods is the
   one the content pass needs most and the one with a proven precedent (D82).
2. **`StoredBy` as flags or as a list per store kind?** Flags are cheaper; a list reads better in
   data. Decide at implementation.
3. **Does a mod-added good need a display colour?** The view has one per good
   (`Main.cs`). Out of scope for slice 1 — the sim does not care — but it is the first thing a
   modder will ask for.
