# Spec: The cold start — the founders arrive with a cart and nothing else

**Decisions:** **D63**, **D64**, **D69**. Slice **C** of Joe's A → B → C, pulled ahead of B
(stone) on his call. **Status:** ✅ **BUILT** (D70–D82), and re-measured against a valley with no
free food or timber in it at all (D119, D157). Guarded by `ColdStartTests` — thirteen guards, six
of which were skipped for a week on a wrong diagnosis and restored by D157. Marked *"not started"*
for a month after it shipped; see D159.

---

## 1. Goal

The village begins with **no buildings at all**. Four founders, a cart of supplies, and a
valley. The player paints where people may live and marks what to raise; the founders fell
the timber and build it. **Winter 1 is survivable only if they get there in time.**

Joe's bar, and it is the acceptance test: *winter 1 shouldn't be survivable unless the user
builds houses for all founding villagers and their children, and a woodcutter with stocked
firewood in each home, before they freeze.*

---

## 2. The finding that makes this small: the cold model already does it

**No new difficulty is needed and none should be added.** Winter is 120 ticks. The shipped
exposure model (D45, D53) already gives:

| The founders have | What happens |
|---|---|
| No house | `Shelter.Outdoors`, 100 a tick — **dead at tick 60**, halfway through winter |
| A house, no firewood | `Shelter.Roof`, 60 a tick — **dead at tick 100**, five days before spring |
| House + woodcutter + firewood | `Shelter.Fire` thaws them — **survival** |

That is Joe's specification exactly, arrived at from the other direction by two earlier
slices. It has never fired because `SimWorld.Create` hands the village its buildings.

**And the safety net removes itself.** D53 measured cold as killing nobody in 300 years
because `TrySeekWarmth` always finds a hearth to walk to. `NearestFire` returns null when no
occupied home holds firewood, so in a village with nothing built the break-off rule simply
does not fire. **The protection was always the village's, never the villager's** — which is
the design working, not a hole.

**So this slice is a founding change and a refactor, not a balance exercise.** If winter 1
turns out to be too easy or too hard, the numbers to reach for are the cart's contents and
the founding season — never the exposure rates, which describe a person in the cold (D53's
refusal of option (b)).

---

## 3. The refactor this actually is

**`Household.HomePosition` is `required` and non-nullable: a household *is* its home.**
There is no homeless state anywhere in the sim, and every villager belongs to a household
from birth. That is the whole difficulty of this slice.

**Resolution: `HomePosition` becomes `GridPos?`.**

- **The compiler does the audit, and that is the argument.** `GridPos` is a struct, so
  `GridPos?` is a genuinely different type; with `TreatWarningsAsErrors` on, **every one of
  the 29 call sites across 7 files must be dealt with or the build fails.** This project's
  single most repeated bug is code reading state from where it used to live (D25, D29, D48,
  D57), and a nullable makes that class unmissable here.
- **A sentinel position is refused** — parking homeless households at the cart would change
  no readers and lie to all of them. `Shelter.Fire` would treat the cart as a hearth, and a
  household larder held at the cart is precisely the right-stuff-in-the-wrong-place shape
  that has cost this project four investigations.
- **Homes as first-class buildings is the end state and not this slice.** D64's hut builds
  *"all buildings"*, and `specs/storage-and-distribution.md §4` already wants one `Building`
  type — but merging that seam (D36) while also removing the founding is two hard things at
  once, which D42 taught twice.

### 3.1 What a homeless villager does

| Question | Answer |
|---|---|
| Where do they eat? | The cart, as a store. **D10 is not negotiable** — a meal must be takeable where they stand, and Phase 0 killed somebody who starved beside a full larder. |
| Where do they rest? | At the cart. |
| What shelter are they in? | `Shelter.Outdoors`. This is the whole tension. |
| Where does their work go? | The cart, until a shed stands. |
| Can they pair and have children? | **Yes, and this needs a decision — see §7.1.** |

---

## 4. The cart (D64)

One `StoreBuilding` present at founding, at the founding site.

- Holds the founders' **food**, and later their tools and clothes when those goods exist.
- **Demolishable once empty**, and **usable as small storage while it stands** — Joe's own
  terms, and Banished's.
- **It keeps D30 intact:** goods still live in a building. The cart is simply the one
  building the player did not raise, which is the story §0 already tells about exiles.
- It is **not shelter**. No roof, no hearth, no exception in `ShelterAt`.
- Its capacity is small and stated in config — content, not economy (§10).

---

## 5. What the player must do, and in what order

1. **Paint residential land.** `starting_residential_radius`'s auto-painted zone **goes**;
   D42's brush becomes load-bearing for the first time rather than optional.
2. **Mark a house site** — or rather, mark nothing: `HouseholdSystem` already builds a home
   when a couple wants one and there is painted land and timber. **Homes stay
   village-built, not player-placed**, because that is D42's settled division — the player
   picks the neighbourhood, the sim picks the tile — and changing it here would give up
   `MaxHomeToWorkTiles`, the bound the whole food economy is derived against.
3. **Mark a woodcutter's hut**, or nobody makes firewood and everybody dies under a roof
   five days before spring.
4. **Mark a shed and a granary** when the cart runs short.

**Every one of those already works.** The build menu (D38, D43), the brush (D42) and the
home-building (D42, `LogsPerHouse`) are all shipped. This slice removes the head start, it
does not add the machinery.

---

## 6. The derivations that read buildings out of the config

Three of them have no answer at tick 0 in a world with nothing built, and each needs a
stated behaviour rather than a crash:

| Reader | Reads | With nothing built |
|---|---|---|
| `VillageEconomy.CutRoundTripTicks` | `StorageShedX/Y` | Budget against the **founding site**, which is where goods go until a shed exists. |
| `VillageEconomy.FirewoodRoundTripTicks` | `WoodcutterHutX/Y` | Same. |
| `Household.ChooseSite` | nearest granary | Fall back to the founding site — the cart is the store. |

**These are the steady-state economy's inputs and they must not move**, or every derived
number in the project changes for a founding that lasts one year. The config coordinates
stay as the *planned* positions; the fallback only applies while the building is absent.
**Guarded by the golden hash** (`StockLimitTests`): a village that builds where it always
built must hash as it always hashed once it is standing.

---

## 7. Open questions

### 7.1 ✅ A homeless couple cannot have children — resolved (Joe, D71)

Option (a). **No roof, no children.** Diegetic, harsh, and it makes the first house genuinely
urgent: a slow player watches the founding generation age with nothing behind it.

**Written as its own rule in `IsReadyForAChild` rather than left to fall out of the food and
firewood gates.** It would have been true either way — a homeless family has no larder and no
hearth, so it could never pass them — but a rule that happens by arithmetic is one a later
change can silently repeal. This one is load-bearing enough to say out loud.

### 7.1b ⛔ Two findings from Joe's second run — both open, and the second is the blocker

**1. ✅ RESOLVED (D102, Joe: *"homes still build instantly — and they shouldn't"*).** A house
is a `ConstructionSite` like everything else now: marked out by the village, its timber hauled
by a builder, and worked on. It killed the founding on the first attempt — two house sites went
in front of the woodcutter's hut and its timber arrived four ticks after winter — and the fix
was **builders staffing what the player marked before a house the village marked for itself**,
not anything about the work. The original finding is kept below because its reasoning is what
the fix had to answer.

**1. A house is built instantly; every other building is a construction site.** Joe:
*"they built homes (immediate builds btw, not a visual timed thing like other buildings)."*
`HouseTheRoofless` and `FormNewHouseholds` both take the timber and set `HomePosition` in
one tick, where a granary or a hut is marked, hauled to, and worked on for a stated number
of ticks (D38, D43). **That inconsistency is now visible and it hides the cost of a house**
— it also means houses never compete with anything else for builders, which is exactly the
distortion that made winter 1 look winnable when it is not. Houses should go through
`ConstructionSite` like everything else. **Not a small change:** `HouseholdSystem` currently
assumes a house exists the moment it is paid for.

**2. ⛔ The woodcutter's hut was marked and ignored — and this is the root cause.** Joe's
panel: *"Materials: 0 of 25 logs — 25 still to come. Work: 0 of 40 ticks done. Nobody works
here. Staffing: left to the village — it wants 0 on this kind of work."*

The chain, and it is one condition:

```
LabourQuota.For:
    if (gatherable season && VillageIsShortOfFood(world))
        woodcutters = loggers = builders = marketers = 0
```

`VillageIsShortOfFood` compares **all the food in the village** against the sum of
`TargetFoodFor(household)` — a *stocking* target of `stockpile_target` per member, not a
starvation line. At the founding that is roughly **760 for two households against a cart
holding 800**, so the village crosses into "short of food" within days of arriving and
**never leaves it**: every hand forages, nobody fells, nobody builds, and the marked hut sits
at 0 of 25 logs until everybody freezes.

**This is not a cold-start bug.** The gate has always read a stocking target; an established
village simply starts above it and stays there. The cold start is the first world that
begins below it. **The fix is a design decision, not a patch**, and it is Joe's: either the
gate reads a genuine hunger line rather than a stocking target, or the founding starts above
the target, or building is exempt from it the way heating already is. **Until this is
answered, no amount of tuning the cart makes winter 1 winnable** — the cart is what puts the
village below the line in the first place.

### 7.1c ⚠️ Option (2) was tried and reverted — it needs a session of its own

Joe chose **(2) exempt building from the gate, then (1) fix the gate** as a follow-up. The
one-line change was made and **reverted the same session**, because it is not a one-line
change:

**Seven tests fail, and three of them are the deliberate guards for the rule being
overridden** — not incidental breakage.

| Failing | Why |
|---|---|
| `PlacementTests.BuildingYieldsToFeedingPeople` | Its comment *is* the rule: *"a village with an empty larder and berries to pick cannot afford to have anybody raising a granary."* |
| `LabourAllocationTests.AVillageShortOfHandsPutsAllOfThemOnFood` | Same policy, one layer down. |
| `LabourAllocationTests.AFedVillageWithSomeoneWaitingForAHouseCutsTimber` | The paired case. |
| `WoodTests.SpareWorkersTakeTheTreeStand`, `VacancyTests…` | Knock-on. |
| `StockLimitTests` golden hashes ×2 | Behaviour genuinely changed, so both goldens must be re-taken. |

**This is the "when a test fails, ask whether the test was right" case, and here they are
right.** They encode §4a's stated policy. Changing it means **rewriting those guards to
express the new rule and re-deriving two goldens** — deliberate work, not a patch, and
exactly the kind of thing that goes wrong when squeezed into the end of a session.

**Recommendation for whoever takes it:** do **(1) first, not (2)**. Making
`VillageIsShortOfFood` ask a genuine hunger question rather than compare against a stocking
target is the change that is actually *correct* in every world — and it may well make (2)
unnecessary, because a village with 800 food in the cart and nobody hungry would simply stop
reporting itself short. Doing (2) first means editing three guards to permit something that
(1) would then make moot.

### 7.1d Joe's third run — houses yes, hut no; and the year is too short to play

The hunger-line fix (D73) landed and the village still does not raise what is marked.
Joe: *"they built the houses but not the woodcutter's hut"*, with the shed reading
**"0 of 30 logs — 30 still to come. Work: 0 of 45 ticks done. Staffing: left to the
village — it wants 0 on this kind of work."**

**Hypothesis, and it needs a probe before anybody acts on it** (METHODOLOGY §3 — every
diagnosis reasoned from precedent in this project has been wrong, and every one from a
measurement right). `LabourQuota.For` spends its hands in a fixed order:

```
foragers (floor)  →  woodcutters  →  loggers for huts
                  →  loggers for houses  →  builders (capped at free / 2)
```

With **four founders**, the food floor takes one and about three are spare. `LoggersWanted`
is non-zero — the village has no timber and wants a woodpile — so the loggers take what is
left, and `builders = min(wanted, free / 2)` is then **zero or one**. The marked hut waits
on a builder who is never funded, while logs pile up in the cart with nothing to spend them.

If that is right, the shape of the fix is **not** a bigger cart. It is that
**building is funded before speculative timber at the founding** — a village with a marked
hut and no fire wants a builder more than it wants a woodpile. Note the `free / 2` cap was
introduced for a real failure (four buildings marked, twelve builders, the village dead with
the buildings finished), so it must be narrowed rather than deleted.

**And the year is too short to play.** Joe: *"year 1 goes by way too quickly — it's like 20
seconds at 10×."* That is D49 working as specified (a year is 80s at 4×, ~32s at 10×), and
the specification is now in tension with the cold start: **the founding is the one stretch
of the game that demands the player act inside a single year**, and eighty seconds is not
long enough to paint land, read the map, mark three buildings and understand why nothing is
happening. D49 stated pacing as *a life takes 60 real minutes at 4×* because the
generational loop is the core loop — that argument still holds for the steady state and
does not hold for the founding. **Joe's call**, and the options are: slow the whole game,
slow only the first year, or start the founding in a season that leaves more of the year in
front of it (§7.2's other dial).

### 7.2 What the cart holds, and the founding season

> **⚠️ CORRECTION (D93, 2026-08-03): there is only one dial, not two.** The founders already
> arrive at **tick 0, which is Spring** — 360 of the year's 480 ticks before winter, and the
> furthest from the frost any founding can be. There is no `founding_season` key and adding
> one could only make the opening *harder*. **The opening has been running at maximum slack
> the whole time**, and still passes its guards by about sixty ticks: first firewood at t300
> against a winter starting at t360. So the cart's contents is the only lever this section
> actually offers.

The two dials §2 says to reach for if winter 1 lands wrong. Both are config. **Not
guessable — they want a probe** (METHODOLOGY §3): measure how many ticks a competent opening
actually takes against the 360 available before winter, then set the cart so an *incompetent*
one dies. Back-of-envelope from D63 says the founders have roughly ten times the time they
need for one house, so the pressure will have to come from the *number* of buildings, not
from any one of them.

### 7.3 Tattered furs (D69)

D69 withdraws "founders start clothed" and asks for a few winters of *almost* freezing. That
needs a **third exposure rate** between bare and dressed. **It is not in this slice** — it is
a clothing change and belongs with `specs/clothing.md`'s rewrite — but it is recorded here
because winter 1's difficulty is tuned against whichever rate the founders actually have.

---

## 8. Failure modes to design against

- **A founding that is unsurvivable however well it is played.** The counterweight to §1, and
  it gets a test: a *competently* played opening — land painted at once, a hut marked early —
  must survive winter 1 every time, on every seed.
- **A founding that is survivable by doing nothing.** The other side, and the actual bar Joe
  set. Doing nothing must kill.
- **A crash rather than a consequence.** Nothing may throw because a building is missing.
  Every fallback in §6 is stated, and the degenerate cases get the same treatment
  `BehaviorSystem` already gives a village with no shed: put it down, and **say so loudly**
  (METHODOLOGY §4).
- **The steady state moving.** §6, guarded by the golden hash.
- **The founders being unable to eat.** D10. A meal must be takeable where they stand, and
  the cart is what makes that true before there is a larder.

---

## 9. How it is tested

Against **both** `VillageFixtures.Village` **and the shipped config**.

1. **Determinism green**; the cart and the null home are in the state hash.
2. **Nothing throws in a village with no buildings** — the founding is stepped a full year
   with nothing painted and nothing marked, and the run stays clean apart from the deaths it
   is supposed to produce.
3. **⭐ Doing nothing kills.** No land painted, nothing marked: the founders are dead or dying
   by the end of winter 1. Joe's bar.
4. **⭐ A competent opening survives.** Land painted at founding, a woodcutter's hut marked:
   the village comes through winter 1 alive, on **every seed tested** — twelve, as
   `MapGenerationTests` already uses.
5. **The steady state is unchanged** once the village has built what it used to start with.
6. **Anti-vacuity (D7):** the "competent" arm must actually have been *at risk* — somebody's
   cold must climb meaningfully — or it is proving survival in a world where nothing was
   dangerous.
7. **No new warnings or errors** beyond the intended ones in a clean playthrough.

---

## 10. Numbers

Config, because they are content: what the cart holds and how much it can hold. **Derived,
because they are consequences:** everything in `VillageEconomy`, unchanged by §6's fallbacks.

---

## 11. Definition of Done

1. This spec current.
2. The seven guards in §9 green.
3. Determinism test green.
4. **Joe's manual QA: start a new village, and find out whether winter 1 is fair.** This one
   cannot be automated and is the whole point of the slice.
5. No unintended errors in a clean playthrough.
6. `DESIGN.md` §6 and §7 updated.
