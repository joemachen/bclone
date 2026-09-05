# Spec: The buildings catalogue — a building becomes a row

> Status: **BUILT — slices 1 and 2 landed, proven and red-checked. Slice 3 (the view) is NOT
> started.** Owner: Joe + Claude Code · Pillar: `DESIGN.md §3` (data-driven, first-class modding) ·
> Format per `METHODOLOGY.md §2`.
>
> **804 passed, 0 failed, 2 skipped of 806** (was 786/788) — **and every golden byte-identical**,
> which is what says the village underneath did not move.
>
> **⭐ This is `goods-catalog.md` and `jobs-catalog.md` applied to the last enum**, and it
> deliberately does not re-argue the shape: same row-plus-catalogue, same *ids appended never
> renumbered*, same rule that **nothing in the sim may switch on a building by name**, same
> two-slice split (a provable no-op, then a modded row in real JSON). Read those two first; this
> one records only what is different about buildings — **and buildings are the one of the three
> with genuine per-kind reasoning in them**, so §2.2 and §2.3 are the interesting part.
>
> Neighbours: `building-placement.md` (the placement rules this must not disturb),
> `multi-material-construction.md` (the recipe this row will hold), `storage-and-distribution.md`
> (store kinds and capacities), `content-inventory.md` **finding 8** (which named this as the
> last remaining half).

---

## 1. Goal

`BuildingKind` is a **C# enum with 10 values**, and `TECH-EXAMPLE.md` names **45 buildings**.
`BuildingRecipe.For` is a switch over per-kind config keys, and **`JobRow.WorksAt` points straight
at the enum** — so a modder can change what a granary costs and **cannot add a fishery**, and a
modded trade can only staff a building that already exists.

**This is the half where the content pass actually needs the headroom** (`content-inventory.md`
finding 8): goods went from 6 to an open set of 62, jobs from 6 to open, and **buildings are still
ten**. The house-upgrade ladder (`DESIGN.md §5`: Wooden Cabin → Stone Cottage → Insulated Manor),
the mason's yard that *"gates every durable building"*, and every knowledge-gated row in
`buildings-plan.md §4` are all rows in a catalogue that does not exist yet.

⛔ **No new building ships with this slice.** The catalogue is the substrate; what goes in it is
Joe's content call and Phase 4's business.

---

## 2. What a building actually decides — measured, not guessed

**43 branches on `BuildingKind` across four files** (28 `=>` arms, 8 `case` arms, 7 equality
tests), plus **one ceiling nobody had counted**. They decide six things.

| Surface | Where | Becomes |
|---|---|---|
| Its name — *"granary"*, *"warehouse"*, *"woodcutter's hut"* | `SimWorld.NameFor` | `Name` |
| What it costs and how long it takes | `BuildingRecipe.For` | `Materials` + `WorkTicks` |
| Which store it becomes, if any | `SimWorld.RaiseStore`, `SimWorld.Demolish` | `Stores` |
| How much that store holds | `SimWorld.RaiseStore` → `VillageEconomy` | `StoreCapacity` — **or null, see §2.2** |
| Which trade works there, if any | `SimWorld.Complete`, `JobsCatalog.WorksAt` | **one relation, §2.1** |
| How many seats it has | `SimWorld.Complete` → `VillageEconomy` | `Seats` — **or null, see §2.2** |
| Its gathering ring | `SimWorld.Complete` | `GatheringRadius` |
| Its own local buffer | `SimWorld.Complete` | `LocalStoreCap` |
| How many souls live in it | `VillageEconomy.HouseholdCapacity` | `HouseCapacity` |

⛔ **AND THE CEILING: `SimWorld._buildingsNamed` is `new int[Enum.GetValues<BuildingKind>().Length]`**
— the naming counter that makes *"granary 2"*. **Building eleven walks off the end of it**, and it
is exactly the class of thing `goods-catalog.md` found twice by counting rather than by reasoning
(`Stockpile.Kinds` at six, `AllowedGoods` at thirty). *Sized from the catalogue, or the first
modded building throws an `IndexOutOfRangeException` in the middle of a run.*

### 2.1 ⭐ The building↔trade relation already lives in TWO places, and this slice must collapse it to one

**`JobsCatalog.WorksAt(job)` says forager → gatherer's hut. `SimWorld.Complete`'s switch says
gatherer's hut → forager.** Both are hand-written, they must agree, and **nothing checks that they
do** — which is D148's finding (*two vocabularies for one thing*) as a data model rather than as a
word.

> **The relation stays on the JOB row, and `BuildingRow` does not carry a trade.** `WorksAt`
> shipped first (D218), it is already the column `KindOf` reads, and a building→trade column beside
> it would be the second source of truth this slice exists to delete.

**What the buildings catalogue provides instead is the reverse index**, built once in the
constructor from the jobs catalogue: `EmployedBy(buildingId)`. One relation, one direction, one
place it can be wrong. ⚠️ **Two trades naming one building is then a load-time error with a
sentence** — legal-looking, currently impossible, and it would silently give one of them the
building's seats.

⛔ **And `WorksAt` must stop being `BuildingKind?` and become an id**, or this closes nothing: that
is the seam `jobs-catalog.md §3` recorded as *"honest and temporary"*, and this is the slice that
was supposed to close it.

> **⛔⛔ THAT PARAGRAPH WAS WRONG, AND BUILDING IT PROVED SO IN ONE TEST RUN — LEFT VISIBLE RATHER
> THAN EDITED AWAY.** `WorksAt` was changed to `int?`, and **six `ModdedJobTests` went red**: their
> JSON reads `"works_at": "GathererHut"`, which is a word, not a number.
>
> **The enum is an alias for the first ten ids, exactly as `Goods` is for the first six**
> (`goods-catalog.md §2.1`) — a modded building is `(BuildingKind)10`, which C# permits and
> `JsonStringEnumConverter` reads and writes as a number. `ModdedGoodTests` has done this since D210
> (`private static Goods Pitch => (Goods)PitchId;`). **What was missing was never the type; it was a
> catalogue for that value to resolve against.**
>
> ⚠️ **And keeping the enum keeps the word.** An int column makes every built-in row read
> `"works_at": 7` where it reads `"works_at": "GathererHut"` today — *a modder looking up which
> number is the gatherer's hut, in the file that exists to tell them.* **§1.1 applies to the data
> files too.**
>
> *The lesson is the one D200 and D220 both record from the other direction: a sentence that
> explains why it is true names its assumptions. This one assumed "points at an enum" meant "cannot
> point past it", and the sibling catalogue had already disproved it.*

### 2.2 ⚠️ HALF THE CAPACITIES ARE DERIVED, AND THAT IS D16 RATHER THAN LAZINESS

**Counted, because D219 banked a correction here and it was only half the picture** — that entry
says capacity is *mostly* data, which is true of **stores** and not of **seats**:

| Building | Store capacity | Seats |
|---|---|---|
| Granary | ✅ **stated** — `granary_capacity` (D219) | — |
| Market | two stated numbers multiplied | ✅ **stated** — `market_capacity` |
| Warehouse | ⛔ **derived** — a horizon of households, their firewood, the logs to split it, a house's timber, floored at a granary | — |
| Stockpile | ⛔ **derived** — the first buildings' logs *and stone*, plus the founders' firewood | — |
| Woodcutter's hut | — | ✅ **stated** — `woodcutter_hut_capacity` |
| Farmhouse | — | ✅ **stated** — `farmhouse_seats` |
| Gatherer's hut | — | ⛔ **derived** — tiles in the ring ÷ tiles per worker |
| Forester's hut | — | ⛔ **derived** — the woodcutters' appetite, plus a hand for building |
| Builder's hut | — | ⛔ **derived** |
| Home | — | ✅ **stated** — `max_household_size` |

> **⭐ The rule, and it is principled rather than a shortcut: a STATED capacity is data; a DERIVED
> capacity is the survival floor, and the survival floor is `VillageEconomy`'s business (D16).**
> Forcing `WarehouseCapacity` into a row would mean typing a number that is currently *solved* — the
> exact move D16 exists to refuse, and the one D219 called *"the derivation was the fault"* only
> because there the derivation was making a promise about people rather than about a box.

**So `Seats` and `StoreCapacity` are `int?`, and null means *ask the economy*.** A small named
switch survives, one arm per derived building — **recorded as an exemption on the record**, the way
`jobs-catalog.md §2.0` and `§2.1` recorded theirs.

**⭐ And a modded building is unaffected by the exemption**, which is the test of whether it is
honest: a mod has no derivation to appeal to, so it states its capacity, and the generic path is
the one it takes. *The exemption covers what the game already solves for itself, not what a modder
can reach.*

### 2.3 ⛔ Two things are per-building REASONING and must not be forced into data

**`Complete`'s `Home` arm moves a family in** — finds the household the site was raised for, sets
`HomePosition`, stands everybody outside their new door, clears `NeedsMoreResidentialLand` and
narrates it. **That is not a column.** It is the same shape as `jobs-catalog.md §2.1`'s idle note:
a modder can add a building; they cannot add a new kind of reasoning about what finishing one
means.

**`Demolish`'s `StoreKind.Cart => BuildingKind.Pile` arm** is the other. The cart is **not a
building** — it is the wagon the founders arrive in — and it borrows the pile's recipe to get the
right refund (nothing). Keep it named, keep the comment; a catalogue lookup would have to invent a
row for a thing the player cannot build.

⚠️ **The dwelling case is data even though the reasoning is not**: `HouseCapacity` on the row is
what `VillageEconomy.HouseholdCapacity` reads, and **that is the seam D153 reserved in so many
words** — *"a second arm, beside a `BuildingKind` appended to the enum"*. The house-upgrade ladder
is three rows with three capacities and three recipes, and it needs no new mechanism. **That is the
first real payoff of this slice and it should be stated where a content pass will find it.**

### 2.4 ⚠️ One column is on probation: the far-from-a-store warning — ⛔ NOT TAKEN

**Decided on the evidence: no column.** `WarningForAFarmFarFromAStore` still tests
`kind != BuildingKind.Farmhouse`, and it is **not** an exemption this spec claims is principled —
it is a column deferred because nothing yet needs it.

**The decoration test is what settled it.** A `WarnsIfFarFromAStore` bool would be **read in exactly
one place, by one building, with no default arm to give real behaviour** — which is
`jobs-catalog.md §4`'s first failure mode with nothing to save it. `LimitedBy` earned its place by
making the default arm mean something; this would not.

⚠️ **It becomes a column the day a second building hauls bulk to a store** — a fishery, an orchard,
a quarry hut. **Recorded here so that day is not spent rediscovering the question.**

`WarningForAFarmFarFromAStore` tests `kind != BuildingKind.Farmhouse` and is **read in exactly one
place** — which is `jobs-catalog.md §4`'s first failure mode, *the row is decoration*. A column
(`WarnsIfFarFromAStore`) would let a modded fishery or orchard inherit D194's hard-won sentence
rather than shipping without it.

**Decide at implementation with the decoration test**, and record which way it went: does the
default arm gain real behaviour (as `LimitedBy` did), or is it one bool read once (as it looks)?
*Written down rather than settled in advance, because guessing is how a column becomes decoration.*

---

## 3. The row

```
BuildingRow
  Id               int          appended, never renumbered — 0..9 are the built-in ten
  Name             string       the label: "granary", "warehouse", "woodcutter's hut"
  Materials        list         what it costs: (good id, amount), sorted by good id, no zeros
  WorkTicks        int          work owed once the materials are on site. 0 with no materials = free and instant
  Stores           StoreKind?   the store it becomes, or none
  StoreCapacity    int?         how much it holds. null = the economy derives it (§2.2)
  Seats            int?         how many work there. null = the economy derives it (§2.2)
  GatheringRadius  int          the ring it gathers in. 0 = no ring
  LocalStoreCap    int          its own buffer. 0 = no buffer of its own
  HouseCapacity    int          souls who live in it. 0 = nobody lives here
```

**Ids are appended, never renumbered.** ⚠️ **`BuildingKind` is NOT hashed today** — checked, not
assumed: `StateHash` mixes a workplace's id, staffing, workers, queue rank, mode and store, and a
store building's id and store, and **never its kind.** *That is what makes slice 1 a provable
no-op, and it is also why the rule still holds:* the day a save format or a build queue is
serialised, an id is what it will carry.

**Free-and-instant is already asked of the recipe, not of the kind** (D108) — `Mark` tests
`recipe.TotalMaterials == 0 && recipe.WorkTicks == 0`. **No column needed**, and it is worth
noticing that the one surface someone might have added a `Free` bool for was made data three
decisions ago by somebody asking *what is this branch actually asking?*

---

## 4. ⛔ The failure modes

| Failure | Guard |
|---|---|
| **The row is decoration** | Every column read by something. ⚠️ §2.4's warning column is the one at risk — decide it on the evidence |
| **Two sources of truth for building↔trade** | §2.1: the relation lives on the **job** row; the buildings catalogue only indexes it backwards. Two trades naming one building is a load-time error |
| **Behaviour changes** | ⭐ **Slice 1 is a provable no-op: goldens byte-identical.** Placement, the build queue and the allocator are untouched |
| **A modded building throws mid-run** | `_buildingsNamed` sized from the catalogue, not from the enum. **Count the ceilings; do not reason about them** — goods found two this way |
| **A capacity silently becomes zero** | A null `Seats` on a building the economy has **no** derivation for is a village that wants nobody there — *a workplace nobody can be assigned to, compiling perfectly.* Validate at load: null is legal only for the built-ins the exemption names |
| **A mod desyncs a save** | Rows go at their **stated id**, never file position — and the fixture must be one where the two differ. ⛔ **`jobs-catalog.md §5` slice 2 is the precedent and it is not optional: eight of nine guards were green and blind until a backwards-ordered catalogue was written** |
| **A building with no home in the world** | A row that neither stores, employs, nor houses anybody can be built and does nothing. Validate at load, the way skills and jobs already are |

---

## 5. Determinism

- **Integer only** (D2). The catalogue is content, read once at load.
- **Nothing new enters the hash in slice 1** — see §3. Both fifty-year goldens and `FarmGoldenTests`'
  **two** values stay byte-identical, or the change was not what it claimed.
  ⚠️ **Count the golden *values*, not the failing tests** — `FarmGoldenTests` asserts two.
- **The reverse index is built once, in id order**, so a mod's rows enter it deterministically.

---

## 6. Slices

### ✅ Slice 1 — the catalogue, as a provable no-op

**Landed. `BuildingRow`, `BuildingsCatalog`, ten defaults, nine surfaces reading the row, and the
naming ceiling lifted. Goldens byte-identical.** Four deliberate breaks, and **the useful one turned
up nothing** — see §6.1.

### ✅ Slice 2 — prove a modder can

**Landed. `ModdedBuildingTests`, 8 guards.** An eleventh building (*boathouse*) and a seventh trade
that staffs it **by an id the enum has no word for**, both in real JSON, driven through marking,
pricing, building, naming, storing and staffing.

**⭐ And the red check reproduced D218's finding exactly: placing rows by file position instead of
by their stated id reddens EXACTLY ONE guard of eighteen** —
`ReorderingTheFileDoesNotReinterpretTheBuildings`, the fixture that lists the eleven backwards.
**Seventeen were green and blind.** *Third instance, and the cure is now cheap enough that there is
no excuse for a fourth.*

### ⛔ Slice 1's red check found a hole, and it is recorded rather than papered over

Renaming the granary in the catalogue — **the word in the village log, in the placement
sentence and on the panel** — turned **zero** tests red across the whole suite. **D108 fixed a
default arm that *"called every unrecognised building a woodcutter's hut, in the log, in the panel,
and in every placement sentence"*, and nothing has ever guarded the words it fixed.**

Two guards now do (`ABuildingIsCalledWhatItsRowCallsIt`, `TheWordOnTheRowIsTheWordInTheVillageLog`),
and they are deliberately a pair: **the first proves the catalogue holds the word, the second proves
`NameFor` uses it.** *D108's bug was a naming path that ignored the right answer, not a wrong answer
stored somewhere.*

### The original plan, for the record

#### Slice 1 — the catalogue, as a provable no-op
`BuildingRow`, `BuildingsCatalog`, defaults for the ten in `SimConfig` beside `JobsCatalog` and
`GoodsCatalog`. The nine data surfaces read the row; `_buildingsNamed` sizes from the catalogue;
`JobRow.WorksAt` becomes an id. The derived capacities keep one named switch, §2.2's exemption.
**Acceptance: goldens byte-identical, suite green.**

### Slice 2 — prove a modder can
`ModdedBuildingTests.cs`. An eleventh building defined in **real JSON**, parsed by the shipping
loader, marked, hauled to, built, named, staffed and demolished. ⛔ **No new building ships into the
game.**

**And the red check is not optional, and it is not the ordinary one:**
1. Reinstate the pre-fix bug (place rows by file position) and confirm it reddens.
2. **`ReorderingTheFileDoesNotReinterpretTheBuildings`** — the same eleven rows listed **backwards**.
   *Without it, every guard is green and blind, because a catalogue written in id order cannot tell
   id from position.* **Three instances of this now (D157, D218); assume the fourth.**

### Slice 3 — the view catches up
`Main.BuildUi`'s build menu is **ten hand-written buttons in four categories**
(`BuildButton("Granary", BuildingKind.Granary)`), and `VillageMap` tests `_building ==
BuildingKind.Market` twice for the market's service-area preview. **A modded building has no
button**, so it exists and the player cannot reach it — *this project's fifth instance of a feature
that shipped unreachable* (D221). ⚠️ **The view has no automated verification of any kind** (D11,
D160): looking at it is the test.

⛔ **The category is a column the sim does not want** (*"Works"*, *"Food"*, *"Stores"*) — it is
presentation, and putting it on the row would be the sim carrying the view's vocabulary. **Joe's
call whether the menu becomes catalogue-driven at all**, or whether built-ins keep hand-placed
buttons and mods get an *"Other"* group.

---

## 7. Definition of Done

1. This spec current.
2. Defaults in code, overridable from config — **one source of truth**, per `goods-catalog.md §8.2`.
3. No `switch` on a building by name in `Bclone.Sim` **except the exemptions §2.2 and §2.3 name on
   the record**: the derived capacities, `Complete`'s `Home` arm, and `Demolish`'s cart. Each is
   reasoning or a derivation, never a value.
4. Unit tests passing; determinism green; **goldens byte-identical**.
5. Slice 2's data-defined-building test passing, **red-checked, including the backwards fixture**.
6. `DESIGN.md` Progress Tracker + Decisions Log updated.

---

## 8. Open

1. ✅ **§2.4's warning column — ANSWERED: no column.** It would be read once, by one building, with
   no default arm to give it work. It becomes one when a second building hauls bulk to a store.
2. **⏸️ DEFERRED ON JOE'S CALL, 2026-08-26 (D223) — does the menu become catalogue-driven?**
   **Not now.** ⛔ **The hole is real and stays stated: a modded building has no button and the
   player cannot reach it** — this project's fifth feature that exists without being reachable
   (D221). **What defers it is that closing it now solves a smaller problem twice:** the bar is ten
   hand-written buttons in four groups, and **that does not scale to 45 buildings** whatever the
   data model underneath says. The menu wants a redesign when the content lands, and *that* is the
   moment to decide whether it reads from the catalogue.
   - ⚠️ **It costs nothing today, because no eleventh building exists. It is a blocker the day one
     does** — including one of Joe's own, not just a modder's.
   - ⭐ **The option that was on the table and is still the cheap one:** built-ins keep hand-placed
     buttons, and anything with an id above the built-ins appears automatically in an *"Other"*
     group. **Recorded so it is not re-derived.**
5. **Should the per-building recipe keys fold into the rows?** `granary_logs`, `hut_stone`,
   `farmhouse_work_ticks` and their eighteen siblings are read by the default rows and by nothing
   else, so they would move cleanly — **except `logs_per_house`, `hut_logs`, `hut_stone` and
   `home_stone`, which the warehouse's capacity, the stockpile's capacity and the timber quota all derive
   against.** ⚠️ **That makes it a re-derivation rather than a move**, and several guards do
   `Config with { LogsPerHouse = … }`. **A separate slice with no behaviour in it, or never** — the
   keys are honest dials as they stand, and the row is what a modder writes.
6. **Should a modded row be allowed to add rather than replace?** Every catalogue in this project
   replaces wholesale (`goods`, `jobs`, `skills`, `household_names`), so a mod adding one building
   restates ten. **Consistent, and increasingly annoying at forty-five.** Not this slice's call.
3. **What does a knowledge flag look like on a row?** `buildings-plan.md §4` marks **18 rows** with
   one and **none of those 18 buildings exist**. ⛔ **Deliberately not designed here** — that is
   Phase 4's, and putting a `RequiresTechnique` column in now would be the tech tree arriving
   sideways while it is held (D205).
4. **Does a building row need a footprint?** Everything is one tile today. `TECH-EXAMPLE.md` does
   not say otherwise, and a multi-tile building touches placement, pathing and the cost field —
   **out of scope, named so it is not discovered mid-slice.**
