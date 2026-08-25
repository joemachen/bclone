# Spec: The jobs catalogue — a trade becomes a row

> Status: **specced, in progress.** Owner: Joe + Claude Code · Pillar: `DESIGN.md §3`
> (data-driven, first-class modding) · Format per `METHODOLOGY.md §2`.
>
> **⭐ This is `goods-catalog.md` applied one enum over**, and it deliberately does not re-argue
> the shape: same row-plus-catalogue, same *ids appended never renumbered*, same rule that
> **nothing in the sim may switch on a job by name**. Read that spec first; this one records only
> what is different about jobs.
>
> Neighbours: `labour-allocation.md` (the allocator this must not disturb), `professions.md`,
> `content-inventory.md` **finding 8** (which named this as the remaining half).

---

## 1. Goal

`JobKind` is a **C# enum hashed by position** with **6 values**, and `TECH-EXAMPLE.md` names about
**40 worker roles**. A modder can change what a forager does and **cannot add a fisherman.**

**Jobs are the smaller and cleaner half of finding 8** (Joe's call, 2026-08-25: *jobs first, then
reassess*) — which is why they go first: they prove the pattern on the enum that has no hard
question in it, before buildings, which carry placement, recipes and capacity.

---

## 2. What a job actually decides — measured, not guessed

**31 switch arms across three files.** Five of the six surfaces are **plain data**:

| Surface | Where | Becomes |
|---|---|---|
| The gerund — *"gathering"*, *"felling timber"* | `SimWorld` | `Doing` |
| The plural — *"foragers"*, *"traders"* | `LabourAllocator` | `Plural` |
| Which building it is worked at | `SimWorld` | `WorksAt` |
| Which good's stock limit stands it down | `LabourQuota` | `LimitedBy` |
| Its slot in the quota | `LabourQuota` | **an index, not a named field** |

⚠️ **Note the plural is not the name.** `JobKind.Marketer` is *"traders"* to the allocator and
*"marketer"* to the roster — **D188's unresolved vocabulary split, which this must not silently
pick a side in.** The row carries both words; deciding which the player sees stays Joe's.

### 2.0 ⚠️ CORRECTION — there are TWO exemptions, not one

**This spec first claimed the idle note was the only surface that resists being data. Building it
found a second**, and the correction is recorded rather than edited away because the *reason* is
the same both times.

**`LabourQuota.StoppedByAStockLimit` is not purely data.** Which good caps a trade is — that is
`LimitedBy`. But the forester's and farmer's arms each carry an **escape clause**: a met log limit
does *not* stop a forester who still has bare ground to plant (D146), and a met food limit does not
stop a farmer with a crop still standing.

> **The good is data. Knowing when the cap has no business applying is not.**

⛔ **So `LimitedBy` does NOT delete that switch, and pretending otherwise would have made the row
decoration** — §4's first failure mode. **What it does instead is give the default arm real work:**
it used to be a flat `false`, meaning *a trade the sim had not been taught about could not be capped
at all* — the player sets a limit and the work carries on, silently. A modded trade now respects its
row's limit through the generic path, while the three built-ins keep their bespoke clauses.

*That is the honest shape: the row handles every trade the sim has no special opinion about, and
the special opinions stay where they can be read.*

### 2.1 ⭐ The other exemption is the idle note, and it needs no decision

**The idle note is real per-job logic** — `ForesterIdleNote` asks about painted ground, work modes
and log limits; `FarmIdleNote` asks about sowing seasons. **These are not reducible to data and
should not be forced into it.**

**But no design call is required, because the codebase already answers it: two of the six jobs —
Marketer and Builder — have no idle note at all**, and `IdleNoteFor` returns null for them. *"This
trade offers no explanation"* is therefore an existing, valid state, and **a modder's job inherits
it for free.** It says nothing, which is honest, rather than saying something wrong.

⛔ **So the idle note stays keyed on the built-in ids** and is the one thing a row does not carry.
That is the line: **a modder can add a trade; they cannot add a new kind of reasoning about why a
trade is idle.**

---

## 3. The row

```
JobRow
  Id         int      appended, never renumbered — 0..5 are the built-in six
  Name       string   what the sim calls it: "forager"
  Plural     string   what the allocator says: "foragers", "traders"
  Doing      string   the gerund for the log: "gathering", "felling timber"
  WorksAt    BuildingKind?   the workplace it staffs, or none
  LimitedBy  Goods?   whose stock limit stands this trade down, or none
```

⚠️ **`WorksAt` points at an enum that is still an enum.** That is honest and temporary: buildings
are the next slice, and until then a modded job can only staff a building that already exists.
**Recorded rather than glossed** — it is the seam this spec cannot close on its own.

---

## 4. ⛔ The failure modes

| Failure | Guard |
|---|---|
| **The row is decoration** | Every column must be read by something. ⚠️ `LimitedBy` nearly failed this — see §2.0: it earns its place by giving the default arm real behaviour, not by existing |
| **The allocator changes behaviour** | ⭐ **The whole slice is a provable no-op: goldens byte-identical.** `labour-allocation.md`'s cost-first pass is untouched |
| **The quota silently loses a trade** | `LabourQuota` sizes from the catalogue, not from six named fields. A job with no slot would read zero and *look like a village that wanted none* |
| **⚠️ A reader that quietly defaults** | Converting the six from stored fields to readers, one was left as an auto-property after the constructor stopped assigning it — so `Farmers` read **zero for ever**: a village wanting no farmers, **compiling perfectly.** ⭐ **But the suite catches it, and that was checked rather than assumed**: reintroducing it turns `TheVillageWantsFarmersWhileTheYearHasFieldWorkInIt` and `AMetFoodLimitStopsTheSowingAndNotTheReaping` red. **The compiler is blind to this class of bug and the guards are not** — which is D146's guard doing exactly the job it was written for. *The lesson is about the compiler, not a coverage hole* |
| **D188 gets settled by accident** | The row carries **both** words. Nothing here picks which the player sees |
| **A modded job with no workplace** | Legal — `WorksAt` is nullable, and a laborer is already *"the villagers no job currently wants"* (D66) |

---

## 5. Slices

### Slice 1 — the catalogue, as a provable no-op
`JobRow`, `JobsCatalog`, defaults for the six. The five data surfaces read the row.
`LabourQuota` becomes indexed. **Acceptance: goldens byte-identical.**

### Slice 2 — prove a modder can
A test defining a **seventh job in real JSON**, driven through the quota, the allocator's naming
and the state hash — the shape `ModdedGoodTests` established. ⛔ **No new job ships into the game.**

---

## 6. Definition of Done

1. This spec current.
2. Defaults in code, overridable from config — **one source of truth**, per `goods-catalog.md §8.2`.
3. No `switch` on a job by name in `Bclone.Sim` **except the two §2.0 and §2.1 exempt on the record**: the idle note, and the stock-limit escape clauses. Both are per-trade reasoning rather than values.
4. Unit tests passing; determinism green; **goldens byte-identical**.
5. Slice 2's data-defined-job test passing, **red-checked**.
6. `DESIGN.md` Progress Tracker + Decisions Log updated.
