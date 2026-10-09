# Spec: The armful and the meal — carry 80, eat once every four days

**Decisions:** D395 (Joe's call), D474 (confirmed), D534 (taken next), D535 (this spec).
**Status:** ✍️ **specced 2026-10-09 on `slice/armful`, NOT built.** Measured first (§5); three calls for
Joe in §7 before any code.

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
| `hunger_per_tick` | 7 | **5** | Hunger reaches `eat_threshold` 80 in **16 ticks = 4 days** (`ticks_per_day` 4). |
| `food_per_meal` | 4 | **Joe's call, §7 Q1** (6 recommended) | What one meal costs. |
| `eat_threshold`, `eat_reduces_hunger`, `hunger_max`, `starvation_ticks` | 80, 80, 100, 24 | unchanged | |

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
| `CompleteAction` (quarry and mine stints) | another dig fits in the arms | **No change.** `digs_per_stint` 4 × 10 stone and `mine_digs_per_stint` 8 × 5 iron already stop a stint at 40. |
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
5. **Quarry and mine stints are unchanged** at 80 (digs per stint still bind).
6. **The derivation guards** (`VillageEconomyTests`, `ShippedConfigTests`' floors) stay green, or are
   re-measured and typed with the numbers that moved, in this slice's commit.
7. **Determinism** stays green; **the goldens move** (four or so, as D529's did), each re-taken with the
   parent commit proving the move is this slice's.

---

## 7. Joe's calls

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

## 8. Definition of Done

- [ ] Joe's three calls answered and written into §2 / §7.
- [ ] Guards §6 written red first; every red-check counted, zeros written down (D326).
- [ ] `data/sim.config.json` and its comments (hunger, carry, bread's *"22 ticks"*), the derived values
      the guards ask for, and `SimConfig.cs` defaults, all in one commit with the docs.
- [ ] The four checks (suite, game build, probe, golden grep before and after), the goldens' moves
      accounted for one by one.
- [ ] Re-measured on the harness after the build (the 100-seed arm), and the table in §5 updated.
- [ ] Played by Joe: a household's trip to the market at 2–4×, the founders stopping to eat about every
      four days, and whether the world feels right.
- [ ] `DESIGN.md` §6 / §7 and `HANDOFF.md` in the same commit; the status line above made true.
