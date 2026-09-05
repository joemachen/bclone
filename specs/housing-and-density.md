# Spec: Housing, household size, and density

**Decisions:** D153 (this document's reason for existing), D71 (no roof, no children), D42
(the residential brush), D33 (the granary is the population ceiling), D143 (an unattended
village should die out), D98 (delete rather than zero).
Neighbours: `buildings-plan.md` (the Home row, and the *tier of warehouse* precedent),
`tech-tree.md` §5 (`SCALE` gating), `phase-1-households-and-labour.md` (household formation).
**Status:** ⚠️ **Written after the first slice, not before it** — the change was small enough
to measure directly, and the measuring is what produced the design. Current as of D153.

---

## 1. Why this exists

There was no spec on housing anywhere, and housing had quietly become the thing three separate
decisions were reaching for. Joe, reading the birth gate:

> *"Can we remove conditions 4 and 6? And put a cap size on family homes to limit the number of
> children a couple can have? (Eventually an unlock/tech that allows for larger homes / denser
> population.)"*

## 2. Which pillars this serves

- **§2.1 generational time.** How many children a couple can have is the rate the whole
  generational loop runs at.
- **§1.1 legibility.** A cap on a house is a rule the player can see and act on. The granary
  threshold it replaces as the effective brake is invisible and lives on another panel.
- **§2.7 civic gating.** A larger dwelling is the natural `SCALE` unlock — *"sixty souls and
  forty years in one place. Time to build properly."*
- **§0.1 the niche.** Challenge in the planning: the player decides where people may live
  (the brush) and, later, how densely (the unlock).

## 3. ⭐ What actually limits a village, measured

This is the part worth keeping, because three plausible answers were wrong and only measurement
separated them. Sampling which gate refuses, per household-year, over 150- and 300-year runs on
both configs:

| gate | share of household-years it refuses |
|---|---|
| the granary (village-wide food) | **42–70%** |
| the family's own larder | 6–10% |
| **room in the house** | **1–3%** |
| the family's own firewood | 0–1% |

- **Loosening food does not hand the brake to housing.** Taking `birth_food_percent` 80 → 10
  raised the peak from 37 to 68 and left the cap refusing 1–3% throughout. What it handed the
  brake to was **starvation** (0 → 78 deaths).
- **Nor does the residential brush**, which was the obvious candidate: *housing is scarce if the
  player doesn't paint*. Measured across radius 3 to radius 9 — **identical outcomes**, because
  25 painted tiles house fifty people when a house holds seven. Land is not the constraint at
  any plausible zone size.
- **The cap itself is the lever.** Three is below replacement (peak 9, extinct by year 30); six
  and seven overrun the food; **five binds without buying it with hunger.**

## 4. The model

| Thing | Where | Notes |
|---|---|---|
| `max_household_size` | config, **5** | Content, not derivation — a fact about the building, the same class as `work_ground_tiles_per_worker`. What must stay derived is the *consequence*, `PopulationCeiling` (D16). |
| `VillageEconomy.HouseholdCapacity(kind, config)` | sim | **The one place that answers "how many fit in this house."** One arm today; the unlock is a second arm. Throws on any other kind rather than defaulting (D108). |
| `SpareHandsAt` | sim | Still reads `max_household_size` directly, and should: it is a *budgeting worst case* feeding the derived fuel target, and wants "the biggest a household can get". |
| A house | `Household.HomePosition`, a `GridPos?` | **Not an entity.** No id, no record — `SimWorld.NameFor` says *"A HOUSE IS NOT NUMBERED"*, and `Complete`'s `Home` arm adds nothing to any list, unlike every other building kind. |

### 4.1 ⛔ Why there is no per-house capacity field yet

There is only one dwelling kind, so a `HomeKind` on the household could hold exactly one value —
which is D98's rule (`construction_site_capacity` was **deleted rather than zeroed**, on the
grounds that *a number which is always zero is a lie waiting to be found*). The field arrives
with the second dwelling, when it has two values on its first day.

## 5. Edge cases and failure modes

- **⚠️ The empty-house swap is where this design will go wrong.** `HouseholdSystem` moves a
  roofless family into a standing empty home by reassigning `HomePosition` between households.
  **Any per-house fact added later must move with it**, or a family will carry the capacity of
  the house they left.
- **A house is a position, so two facts about "the house" live on the family.** That is
  tolerable at one kind and gets sharper at two.
- **Below replacement.** A cap of three kills the village — a couple plus one child cannot
  replace itself once anybody dies young. Any future *smaller* dwelling must be a second home
  for a village that has others, never the only kind.
- **The unlock must not silently resize existing houses.** Raising a global number would change
  every home at once, which is the opposite of a building you place. A second `BuildingKind`
  (appended — the enum is hashed by position, never renumbered) keeps it a thing you build.

## 6. How it is tested

- `HouseholdTests` / `VillageTests` — the gate's conditions, and
  `AVillageThatCannotFillItsGranaryNeverHasAChild` for the one food term that remains.
- `ResidentialZoneTests.PaintingMoreLandLetsTheVillageGrowAgain` — the loop end to end: the
  player paints, the village builds. **It asserts the village is still alive when the paint
  arrives**, after a squeezed village of three made it report the answer as a failure.
- The long-horizon acceptance guards carry the real claim: nobody starves, nobody freezes, and
  `aged > froze + starved` — *pressure, not the normal way to die*.
- `ShippedConfigTests` — because the cap lives in `data/sim.config.json` and the fixture, and
  those two drifting is this project's most repeated bug (D48–D50, D128, D132).

## 7. Definition of Done

1. This spec current. ✅
2. Cap read through `HouseholdCapacity`, one arm, throwing default. ✅
3. Shipped config and fixture in step. ✅
4. Full suite green, goldens re-taken last with a stated reason. ✅
5. `DESIGN.md` §6 and §7 updated (D153). ✅
6. **Joe plays it** — the last five real bugs came from him, not the suite. ⬜

## 8. Not built, and deliberately

**The larger dwelling and its tech gate.** The seam is: append a `BuildingKind`, give it a
recipe and a `HouseholdCapacity` arm, gate it `SCALE` per `tech-tree.md` §5, and add the
per-house field at that moment. `buildings-plan.md` already records the shape for its sibling
case — *"a bigger one is a **tier of warehouse**, not a new building."*
