# Spec: The armful and the meal — carry 80, eat once every 4¼ days

**Decisions:** D395 (Joe's call), D474 (confirmed), D534 (taken next), D535 (this spec), D536 (Joe's three
calls), D537 (the quarter day, and built).
**Status:** ✅ **built 2026-10-09 on `slice/armful` (D537, D538), sim only, merged to `main` at Joe's word
(*"merge"*) unplayed.** The guards in §6 and §7a are green and red-checked; six goldens moved with D537 (§6b),
none with D538. **The founding race is fixed (§7a): lost foundings 22 → 13 of 100, under the 15 before.**

---

## 1. Why this exists

Two things Joe asked for in one sentence on 2026-09-19 (D395), confirmed on 2026-10-03 (D474):

> *"armful 40 → 80"* and *"they stop to eat 1x every 2 days — let's change it to 4."*

- **The armful.** D473 traced a household ten tiles from its store fetching about as fast as it eats:
  an armful of 40, twenty ticks each way, and the family eats ~16 of it on the walk home. One adult
  spent 89 of a season's 120 ticks fetching food. The fetch is honest (D32: distance to the granary
  should cost something), and it costs too much.
- **The meal.** A villager eats at hunger 80, gaining 7 a tick: a meal every 11–12 ticks, about every
  three days. Each meal costs a tick (D10), and since D531 shows a bowl above the dot. Joe reads that
  as people forever stopping to eat.

Both are **D16 re-derivations**: the food economy is derived from what an adult eats a year and how
far an armful goes, so neither is a cosmetic dial.

**Pillars:** §1.2 *meditative pace* (fewer interruptions, a working day that looks like work), §2.2
*smart labour* (fewer hands lost to errands). ⛔ It must not quietly loosen D363's scarcity (*"foraging
gives too much food now"*), which is the trap in §7 Q1.

---

## 2. The rule

| Key | Today | After | What it means |
|---|---|---|---|
| `carry_capacity` | 40 | **80** | One armful, for **every** errand that carries (§3). |
| `hunger_per_tick` | 7 | **5** | |
| `eat_threshold`, `eat_reduces_hunger` | 80, 80 | **85, 85** (D537) | Hunger reaches 85 in **17 ticks = 4¼ days** (`ticks_per_day` 4). |
| `food_per_meal` | 4 | **6** (Joe, D536) | An adult eats 168 a year against 172 (−2 %). |
| `hunger_max`, `starvation_ticks` | 100, 24 | unchanged | From the threshold to `hunger_max` is 15: 3 ticks at 5, as it was 3 at 7. |

The village fixture follows all five (D223's seam, `VillageFixtures.Village`); Phase 0's fixture keeps its own world.

**No new mechanism.** Three numbers in `data/sim.config.json`, the derived values that follow from
them, and the guards that pin them.

---

## 3. Every reader of the armful

`CarryCapacity` is read at fourteen call sites outside its declaration (three in `SimWorld`, ten in
`BehaviorSystem`, one config check). Each one, grouped, and what 80 does to it:

| Reader | What it carries | At 80 |
|---|---|---|
| `CollectFromStore`, `ATripsWorth` (`BehaviorSystem`) | a household's fetch: the shortfall, up to an armful | **The point of the change.** A fetch carries up to 80; the larder target (`stockpile_target` 95) fills in one trip from the 50% trigger. |
| `WorthTheTrip` | the fetch bar: the smaller of ¼ armful and ¼ of what's wanted | ¼ armful is 20; the target's share (≤ 24) usually decides anyway. |
| `LoadMaterials` | a builder's load: what the site still needs, up to an armful | Houses (34–40 logs) and huts (25) need less than 40 already, so no change; bigger buildings take half the trips. |
| `LoadForTheMarket`, `LoadForTheRound` | the marketer's load | Half the trips. |
| `TakeFromTheBuffer`, `TakeALoadOutOf`, `PickUpFromTheGround` | clearing a buffer, a store or a heap | Half the trips. |
| `SimWorld.WorthTheWalk` (buffer clearing) | a hut's buffer is cleared once it holds an armful | Cleared at 80, not 40: a buffer waits longer before somebody fetches it. |
| `SimWorld.CounterShortOf` | the market restocks a good only if an armful still fits under its limit | ⚠️ With one household the derived market limit is 100, so a counter restocks only below 20 (was 60). A market serving a small village runs lower before it is topped up. |
| `SimWorld.ArmfulsWaitingIn` | the card's *"N armfuls waiting"* | Counts in 80s. |
| `CompleteAction` (quarry and mine stints) | another dig fits in the arms | ⚠️ **Changes for a worker with a tool** (corrected while writing the guards; the first draft said *no change*). A dig is `YieldFor` × vigour, so a tool's 25 % makes a stone dig 12 and an iron dig 6. At 40 the arms end the stint early (3 digs = 36 stone, 6 digs = 36 iron); at 80 the stint runs to its own count (4 × 12 = 48, 8 × 6 = 48): **a third more a stint with a tool**, unchanged without one. That is the stint length `digs_per_stint` was always meant to set. |
| `SimConfig` validation | `home_store_cap` ≥ a winter's firewood + one armful of food | 400 still clears it (validated at build). |

**Producers are not armful-bound:** a forager's gather, a hunt, a cast and a felled tree go into the
arms at their own yield. That is unchanged.

⚠️ **One armful, one meaning.** The alternative (80 for a household's fetch, 40 everywhere else) is
two meanings for one word, the D148 / D240 trap, and §7 Q2 asks it only so it is refused on purpose.

⭐ **The Cartwright Warehouse** (`buildings-plan.md`) is the later building that raises what one person
carries. After this slice it raises it **from 80**.

---

## 4. Every reader of the meal clock

`hunger_per_tick` reaches the sim in one place (`NeedsSystem`), and the meal clock through
`VillageEconomy.MealIntervalTicks` (`eat_threshold / hunger_per_tick`: 11 today, 16 after). Everything
below follows by derivation:

- **The food floor** (`MealsPerYear`, `AdultFoodPerYear`, `RequiredGatherYield`, the stockpile and
  granary sizes): 43 meals a year → 30. A year's food per adult is 172 today; 180 at 6 a meal (+5%),
  150 at 5 (−13%). The yields are guarded as floors, so at 6 a guard may redden and say which yield sits
  under it. That is the derivation working, and it is fixed by measurement, not by typing.
- **Bread sates longer** (`Feed`, D522): `FullFor` is the meal's surplus × `MealIntervalTicks` / cost, so
  bread scales with the interval. The config comment's *"next meal after 22 ticks, not 11"* becomes 32
  after 16 and is rewritten.
- **The founders' first meals spread over one interval** (D529, `SimWorld` founding): derived, so the
  spread widens from 11 ticks to 16. The goldens move with it.
- **Starvation:** from the eat threshold to `hunger_max` is 20 hunger, 3 ticks at 7 and 4 at 5, then
  `starvation_ticks` 24. A hungry villager has one tick longer before the starvation clock starts.
- **The meal mark** (D531, view): one bowl per meal, so 30% fewer bowls. Nothing to change.

---

## 5. Measurement (2026-10-09, `tools/harness/ZzBase.cs` with `ZZ_CARRY`, `ZZ_HUNGER`, `ZZ_MEAL`)

Unattended harness (D447: a harness is not a player). Fifty years each. *Fetches* are household food
fetches begun per household-year; *meal stops* are meals per household-year.

**100 valleys (seeds 200–299), the shipped config:**

| Arm | Alive at 50 | Valleys dead | Lost in the founding | Fetches / hh-year | Meal stops / hh-year |
|---|---|---|---|---|---|
| Today (40; 7 / 4) | 546 | 24 | 15 | 12.7 | 140 |
| Armful 80 only | 553 | 25 | 18 | **7.5 (−41%)** | 138 |
| 80, every 4 days, **6** a meal (+5% food a year) | 514 | 28 | 19 | 7.9 | **100 (−29%)** |
| 80, every 4 days, **5** a meal (−13% food a year) | **774 (+42%)** | 25 | 20 | 6.7 | 100 |

**55 runs (ZzBase's default: 30 shipped, 12 fixture, 13 every-source), alive at 50:**

| Arm | shipped | fixture | every |
|---|---|---|---|
| Today | 168 | 89 | 93 |
| Armful 80 only | 173 | 105 | 110 |
| Every 4 days, 6 a meal (armful 40) | 156 | 91 | 93 |
| Every 4 days, 5 a meal (armful 40) | 209 | 117 | 179 |
| 80, every 4 days, 6 a meal | 171 | 101 | 108 |

**Reading it:**
- **The armful does what D473 asked:** 41% fewer food fetches, and the village is no worse for it.
- **The meal clock alone is neutral if the year's food is held.** At 6 a meal (+5%) the village is a
  little smaller (546 → 514); at 5 a meal (−13%) it grows by 42%. **The population moves with what
  people eat, not with how often they stop.** Hence §7 Q1.
- **Every 4 days at 6 a meal plus the armful is today's village** (shipped 171 vs 168), with 38% fewer
  fetches and 29% fewer meal stops: the look Joe asked for at no change to the economy.

### 5.1 ⚠️ A founding race the armful nudges (found measuring, traced on seed 2)

Lost foundings go 15 → 18 of 100 with the armful. Traced (shipped seed 2, Year 1, a scratch test,
deleted): at 80, laborers bring stone sooner, so the builders finish the gatherer's hut first. The
woodcutter meanwhile splits every log that reaches the store into firewood, because **nothing stops
splitting below the 400 firewood limit while a house waits on logs.** By autumn the two houses hold
4 of 74 logs, the stores hold ~110 more firewood than at 40, and the founders freeze under the
builder's-hut roof in winter.

**This is not the armful's bug.** It is the order of events at the founding, and it exists at 40;
there the same race happens to go to the houses. It is D515's family (a first winter decided by what
the founding did first) and D447's (an unattended harness never notices unfunded houses). §7 Q3.

### 5.2 ⚠️ What building it found: 6 every 4 days is 5 % more food, and that bites (2026-10-09)

Joe answered Q1 *"6"*. With the numbers in, the suite reddened two things the harness tables above could
not show:

- **The food floor** (`VillageEconomyTests.TheShippedConfigFileMeetsTheTarget`): at 180 a year a forager
  keeps themselves and a dependant only at `gather_yield` **128**; the config ships **123** (D363's cut).
- **The market-off promise** (`MarketTests`, §14.4: *switching the market off costs convenience, never
  lives*). Isolated over eight fixture seeds × 150 years with the market off, forage at each version's floor:

| Variant | Food a year | Starved | Old age |
|---|---|---|---|
| Today (every 11–12 ticks, 4 a meal) | 172 | 40 | 153 |
| Armful 80 only | 172 | 36 | 159 |
| Every 16 ticks, 6 a meal | 180 (+5 %) | **55** | 131 |
| **Every 17 ticks (4¼ days), 6 a meal** | **168 (−2 %)** | **42** | 135 |
| Every 16 ticks, 5 a meal | 150 (−13 %) | 26 | 160 |

**The extra starvation is the 5 % more food, not bigger, rarer meals**: the same meal shape at −2 % starves
as today does. At 17 ticks the floor wants **120**, under the shipped 123, so no yield moves.

**So Q1 comes back to Joe (Q1b):** keep exactly four days at 6 (raise forage 123 → 128 and accept a
hungrier village without a market), or take **4¼ days at 6** (`eat_threshold` and `eat_reduces_hunger`
80 → 85: the same rare meals, 2 % less food than today, nothing else moves). Recommended: 4¼ days.

### 5.3 After the build (D537): 100 valleys, the same as §5's, against the parent

| | Alive at 50 | Starved | Food fetches / hh-year | Meal stops / hh-year | Valleys dead | Lost in the founding | Froze |
|---|---|---|---|---|---|---|---|
| Before (parent) | 546 | 358 | 12.7 | 140 | 24 | 15 | 56 |
| **After** | **560** | **289** | **7.5** | **93** | 29 | **22** | 88 |

Everything the slice is for landed: 41 % fewer food trips, a third fewer meal stops, a fifth fewer starved.
⚠️ **The founding race of §5.1 grew to 22 lost foundings of 100** (15 at 40; 18–20 in §5's armful arms):
seven more valleys lose their four founders in Year 1, and their 28 deaths are most of the extra cold.
Joe chose Q3 *"keep"* before this number existed, so it goes back to him.

---

## 6. Guards (TDD — written red first, each red-checked and counted)

1. **Shipped config** (`ShippedConfigTests`): `carry_capacity` 80, `hunger_per_tick` 5, and the meal
   interval is **16 ticks = 4 days** (asserted as days, so a `ticks_per_day` change says so).
2. **A fetch carries a whole armful** (`StorageTests` / `MarketShopTests` shape): a household at the
   trigger with a stocked store comes home with up to `carry_capacity`, and 80 at the shipped config.
3. **Fewer trips, posed:** one household, one store ten tiles off, one season. Fetches at 80 are at
   most ~60% of fetches at 40. That is D473's finding as a guard.
4. **A villager eats every 16 ticks** at the shipped config with no bread (posed: one villager, food
   at hand, count `JustAte` over a season).
5. **At 80 a stint is ended by its digs, not the arms**, for a quarrier and a miner holding a tool (§3).
6. **The derivation guards** (`VillageEconomyTests`, `ShippedConfigTests`' floors) stay green, or are
   re-measured and typed with the numbers that moved, in this slice's commit.
7. **Determinism** stays green; **the goldens move** (four or so, as D529's did), each re-taken with the
   parent commit proving the move is this slice's.

---

### 6a. Red-checks (D537): 8 mutants, 12 reds, one that did not build, one zero

| Mutant | Reds |
|---|---|
| shipped armful 80 → 40 | 3: the shipped armful, the stint, the fixture-follows-the-game |
| shipped eating 85 → 80 | 2: the shipped meal, the fixture-follows-the-game |
| fixture armful 80 → 40 | 2: a fetch brings a whole armful, the fixture-follows-the-game |
| fixture eating 85 → 80 | 2: a villager eats every 4¼ days, the fixture-follows-the-game |
| `ATripsWorth` capped at 40 | ⛔ **did not build** (it left `config` unused, IDE0060): scores nothing |
| a fetch's `load` capped at 40 | 2: a fetch brings a whole armful, 80 takes fewer trips |
| the top-up never remembered | ⚠️ **0 on the re-posed half-larder guard, and 0 on the parent too**; `ALoadOnItsWayHomeCountsAsHeld` reddens (1) |
| the lanes arm cuts corners too | 1: the founding hub wears lanes |

### 6b. What moved, and why

- **Six goldens**: `StockLimitTests` (fixture, shipped), `SkillTests` (fixture, shipped), `FarmGoldenTests`'
  `SeamGoldenHash` and, behind it, `SeamBeforeAnybodyGotBetter` (asserted second, so it could only redden
  once the first was re-taken). Each proven by the parent (`f3decb0`) holding the old value with the suite green.
- **One pin**: `VillagerPointTests`' clock, 7 / 213 / 489 → 7 / 192 / 499; the first trip unchanged.
- **Two re-poses**: the half-larder guard poses an armful of 40 (at 80 a couple's 150 fits in two armfuls,
  which is the point); the lanes guard sums six valleys, because the fixture's one valley was a coin (D470).

## 7. Joe's calls — answered (D536, D537)

**Answered 2026-10-09:** Q1 *"6"*, then, once building found that 6 every four days was 5 % more food (§5.2),
*"proceed with every 4¼ days at 6 a meal"*; Q2 *"everyone"*; Q3 *"keep"* — ⚠️ given before §5.3's 22.

- **Q1 — Does eating less often mean eating less?** Every 4 days, a meal of **6 (recommended: about
  what a villager eats today, +5% a year, the village level with today)** or of **5 (−13% a year, the
  village +42% in the harness, a big easing on top of D363's deliberate scarcity)**. A third shape holds
  the year's food almost exactly (meal every 17 ticks = 4¼ days, 6 a meal, −2%) and is offered only if
  the 4 matters less than the food.
- **Q2 — One armful for everybody (recommended), or 80 only for a household's food fetch?** Everybody:
  builders, marketers and laborers take half the trips, and one word keeps one meaning. Fetch-only
  is narrower and splits the word.
- **Q3 — The founding race in §5.1: leave it (recommended for this slice) or fix it?** Leaving it keeps
  this slice to its numbers; a played founding sees two marked houses at *4 of 40 logs* and the card
  says so. Fixing it is a rule like *"the woodcutter does not split logs a funded site is waiting on
  while firewood is above the winter's need"*: its own slice, measured on its own.

---

## 7a. The founding race — fixed (Joe, D538: *"fix it"*) ✅ built

**The cause** (§5.1, traced on seed 2): `TheVillageWantsMoreFirewood()` reads the **player's** firewood limit
when one is set, and the game starts with one (`starting_stock_limits.firewood` 400). So a woodcutter splits
every log in the stores until 400, whatever the village needs to get through winter, and a founding's two
marked houses wait at a few logs while the warehouse fills with firewood nobody can burn yet.

**The rule:** *while a construction site still waits on logs, the woodcutter splits beyond the village's own
winter need only from spare logs.*

```
MaySplitLogs = TheVillageWantsMoreFirewood()
               && (FirewoodShortfall > 0                       // below the derived winter need: fuel first
                   || LogsInWarehouses − LogsTheSitesStillNeed ≥ logs_per_split)   // above it: spare logs only
```

- **Below the derived need, fuel wins**, exactly as today. That is D515's ground (a first winter decided
  by firewood) and D17's (cold is a live death axis), so the fix cannot freeze anybody who is not frozen now.
- **Above it, the player's limit still holds**, just not over logs a house is waiting on. *Derived floor,
  player ceiling* (D62) — and the ceiling yields to building only between the two.
- `LogsTheSitesStillNeed` is every unfinished, non-demolishing site's `StillNeeded(Logs)`, summed when a
  woodcutter asks (per decision, never per tick for the village; a few dozen sites at most).
- **Said on the card**: a woodcutter held back by it reads *"Nothing to split — the N logs in store are
  wanted for building (M still to deliver), and the homes have the firewood they need."*
- Read at both places a woodcutter commits to a split: choosing to go (`Decide`) and carrying on a stint.

**Guards** (`LogsForBuildingTests`): the predicate in its three states (held back / spare logs / fuel
first); and a posed village with the winter's firewood in and a site waiting on logs, where the stores never
fall below what the site waits on, against the same village with no site, where the woodcutter splits the
stores bare. ⚠️ The first draft asked *"splits none"* and the rule failed it honestly: the fixture's other
stores held logs beyond the site's need and the woodcutter split exactly those. **Red-checked: 5 mutants,
7 reds, no zeros**: the rule removed (2), no fuel first (1), sites need nothing (2), only `Decide` asks (1),
only the stint asks (1).

**Measured** on §5.3's 100 valleys (seeds 200–299), against the same valleys:

| | Alive at 50 | Valleys dead | Lost in the founding | Froze | Starved |
|---|---|---|---|---|---|
| Before the slice | 546 | 24 | 15 | 56 | 358 |
| Armful and meal, no fix (D537) | 560 | 29 | 22 | 88 | 289 |
| **With the fix (D538)** | **620** | **19** | **13** | **52** | 354 |

The race was costing villages before the armful too. On 100 fresh valleys (seeds 300–399) with the fix:
12 lost foundings, 25 dead. **No golden moved**: the rule differs from before only while a firewood limit
is set, and no golden village sets one.

## 8. Definition of Done

- [x] Joe's calls answered (D536: 6, everyone, keep; D537: the quarter day) and written into §2 / §7.
- [x] Guards §6 written red first; every red-check counted, zeros written down (§6a).
- [x] `data/sim.config.json` and its comments (hunger, eating, meal, carry, bread's *"34 ticks"*) and the
      village fixture, in one commit with the docs. `SimConfig.cs`'s code defaults are deliberately left
      (they already differ from the shipped file and are what Phase 0's fixture reads).
- [ ] The four checks (suite, game build, probe, golden grep before and after), the goldens' moves
      accounted for one by one (§6b).
- [x] Re-measured on the harness after the build (§5.3).
- [ ] Played by Joe: a household's trip to the market at 2–4×, the founders stopping to eat about every
      four days, and whether the world feels right.
- [ ] `DESIGN.md` §6 / §7 and `HANDOFF.md` in the same commit; the status line above made true.
