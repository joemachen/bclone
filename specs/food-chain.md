# Spec: The food chain — wheat → flour → bread, and what bread is for

**Decisions:** D520 (this document; Joe's three calls, 2026-10-06). Neighbours: D19/D39 (the
processing tier above the raw sources: *"bread baked from wheat … beer from wheat"*), D29 (the
conversion workplace: the woodcutter's shape), D277 (food is a capability; `GoodRow.Nutrition`;
**every edible good worth the same, enforced at load**), D298 (food is an umbrella, a sum of every
food), D348 (wheat is a real good, **edible raw because it had to be**), D357 (diet is health's first
content, Phase 6, *waiting on this derivation*), D391/D446 (the smith: two inputs from one store, never
the winter's firewood), D392 (the founding draws from `founding_trades`, not the skills catalogue),
D434/D444/D449 (gifts by doing: the quarry, the smithy, the mine), D440 (the tech-tree map), D28/D190
(villagers in lockstep, and the seeded rhythm that had to be manufactured to break it).
**Status:** ✍️ **specced, not started (2026-10-06).** Joe made three calls before it was written
(§0) and answered §9's six the same day (D521). **§8's measurement has started; nothing is built.** No code, config or test exists for anything in
this document. Owner: Joe + Claude Code.

---

## 0. Joe's calls (2026-10-06, D520)

He was asked three questions before a word of this was written, because each one changes what the spec
covers:

1. **What is bread for? → *"Sates longer."*** A meal of bread keeps a villager full for longer than a
   meal of grain, so they eat less often. This is his nutrition axis of 2026-08-23 taken literally
   (`food-catalog.md §0`: *"different 'nutritional' value which will help vary villagers' rhythms
   depending on what food they eat to sate their hunger"*). Rejected: *fewer loaves a meal* (the same
   value, but no rhythm), *more loaves a sheaf* (a yield multiplier that leaves the nutrition axis
   unbuilt and Phase 6 with the whole derivation), and *wait for Phase 6* (a chain that only costs hands).
2. **Beer? → *"Bread only, beer later."*** Beer comes with morale and the tavern (Phase 6), where it has a
   mechanic to plug into. §13 records the brewery's shape so it is not rediscovered.
3. **How do the buildings arrive? → *"Gift by doing."*** The mill after the village has reaped enough
   wheat, the bakery after its first flour is ground: the quarry's, smithy's and mine's shape. The
   numbers are measured (§8), not typed.

---

## 1. Why this exists, and what the code had

`DESIGN.md §4` (Phase 5): *"The food chain — wheat → flour → bread, wheat → beer; deriving a diet is
the precondition (D277, D348)."* The handoff put the trap first: **today bread would feed exactly
what its wheat fed**, so a mill and a bakery would only cost hands and timber.

Traced, not remembered:

| Thing | State |
|---|---|
| `Goods.Wheat` (9) | Reaped by farms, stored by granary/market/cart, **`Nutrition = 1`, edible raw** (D348: *"forced, not chosen"*) |
| `GoodRow.Nutrition` | Answers *"can it be eaten?"*, never *"how well?"* — `SimConfig.cs` (the edible-goods loop at load) **throws if two edible goods carry different values** |
| A meal | `BehaviorSystem.MealCostFor`: `food_per_meal` (4) units, a dependant's share for children and elders. `SimWorld.TakeAMealFrom` takes them **in id order** across `GoodsCatalog.EdibleGoods` |
| Hunger | `NeedsSystem` adds `hunger_per_tick` (7) up to `hunger_max`; `TryEat` fires at `eat_threshold` (80); `Feed` subtracts `eat_reduces_hunger` (80) and **clamps at 0**. Negative hunger is logged as a bug (`NeedsSystem`, the invariant). A meal every `MealIntervalTicks` = 80 / 7 = **11 ticks** (about 2¾ days) |
| The survival floor | `VillageEconomy.AdultFoodPerYear` = `MealsPerYear × food_per_meal`, in **units**; `RequiredGatherYield`, the farm's numbers and the quota all stand on it |
| A conversion workplace | The woodcutter (input from a store, a stint at the hut, output to a store, a refusal naming the store — D29, D384) and the smith (**two inputs from the one store holding both**, never the winter's firewood, `WhyTheForgeIsCold` read by worker and card — D391) |
| A gift by doing | `SimWorld.WhyNotYet` (one rule read by the build bar and by `Mark`), hashed counters (`StoneEverDug`, `IronEverDug`, `IronToolsEverForged`), `LearnedByDoing` (the moment), `TechTree.DefaultNodes` (the map's rows) |
| Bread, flour, mill, bakery, miller, baker | **Nothing.** Only names: `Miller` and `Baker` in the surname list, and *"Ale — no brewer, no barley"* as a roadmap row on the Overview |

So the slice is: **give food a worth beyond 1 without moving the floor, then build the two workplaces
that make a food worth 2.**

## 2. Which pillars this serves

- **§2.2: the processing tier.** D39 named it: *bread from wheat*. It is the second chain of D29's shape
  for food, and the first in which a workplace makes a raw good *better* rather than different.
- **§2.7: unlock by doing.** The design doc's own worked example is *"bake enough bread across enough
  winters → better oven"*. This slice gives that example a mill and an oven to happen at. The better
  oven is a later technique (§9 call 3 makes the skill row that would carry it).
- **§1.4 / D28: people, not lockstep.** Two villagers who ate different things get hungry at different
  times **for a reason the card can say**. D190 had to *manufacture* that variation with a seeded
  rhythm; this one falls out of content.
- **§1.1: legible.** A card says *"full from a meal of bread"*. A mill with nothing to grind says why,
  and names the store.
- **§0.1: cozy, not cruel.** Bread is **upside above the floor** (the tools precedent). A village that
  never builds a mill plays exactly as today, to the unit.

## 3. The nutrition rule, which answers `DESIGN.md §5`'s derivation question

`DESIGN.md §5` and `food-catalog.md §0` asked: *"a catalogue of values means the floor is solved
against the worst food a village might be living on, or the derivation changes shape."* **The answer
is the first one, and it is already true:**

### 3.1 The floor is solved at nutrition 1, the least an edible unit can be worth

`Nutrition` is an integer, and an edible good's is ≥ 1. Every derivation in `VillageEconomy` counts
**units** and assumes each unit is worth one plain meal's share, which is exactly a nutrition-1 food.
So:

- A diet of nothing but nutrition-1 foods is **exactly** the floor, as today.
- Any better food makes the real village **better fed than the floor assumes, never worse**.

That is `tools-and-the-smith.md §3.9`'s shape: *the survival floor is solved for a village that knows
nothing, and every bonus is upside above it.* **The load-time throw changes from "every edible good is
worth the same" to "an edible good is worth at least 1".** The comment over it keeps D48–D50 as the
reason, now pointed the right way: *a food worth LESS than the floor assumes is what kills a village,
and an integer ≥ 1 cannot be.*

⛔ **A guard pins it**: raising bread's nutrition moves no number in `VillageEconomy` (§11).

### 3.2 Sates longer: a full belly holds hunger off

A meal costs what it costs today: `MealCostFor` units, taken from the arms or the larder. What changes
is what the meal is **worth**:

- **P** = Σ (units taken × that good's nutrition), the meal's worth.
- **C** = the meal's cost in units, which is what a plain (nutrition-1) meal is worth.

`Feed` does exactly what it does today (hunger down by `eat_reduces_hunger`, clamped at 0), **then**:

```
Villager.FullFor += (P − C) × MealIntervalTicks / C          (integer, rounded down)
```

and `NeedsSystem`, while `FullFor > 0`, **counts it down instead of raising hunger**. Hunger never goes
below 0, so the invariant in `NeedsSystem` stands untouched.

At today's numbers: an adult meal of four loaves at nutrition 2 is P = 8, C = 4, so **`FullFor` gains
11 ticks** and that villager's next meal comes after 22 ticks instead of 11. A mixed meal (two forage
and two loaves) gains 5. A dependant's smaller meal scales the same way, because the formula is a
ratio.

- **`Villager.FullFor` is state**: hashed **sparsely** under a tag (non-zero only, D291's pattern), and
  saved like every field (`SaveLoadTests`' field guard will refuse it otherwise).
- ⭐ **Byte-identical while every food is worth 1**: P == C, `FullFor` never leaves 0, nothing is
  hashed. So **no golden moves when the rule lands**, only when a nutrition-2 good is eaten. That
  separates the two changes into two proofs.
- Starvation, `TicksAtMaxHunger` and the *"nothing left to eat"* narration read hunger, which a full
  villager is not near.

### 3.3 Eat the best food first (recommended, §9 call 4)

`TakeAMealFrom` walks the edible goods in **descending nutrition, then id**. While every food is worth
1, that is id order, so it is **byte-identical**.

**Why:** in id order bread (12) is eaten last. A household with forage in its larder would eat forage
until it ran out, and the rhythm change would show only in want. The village's **total** eating is
unchanged by the order: the same points are consumed either way, because a point bought as fullness
is a point not bought as a meal. The order only decides **when the player can see it**.
`EdibleGoods` is built once (`GoodsCatalog`), so this is a sort at load, never per meal (CLAUDE.md's
rule).

### 3.4 Every units reader stays in units, and that is the safety argument

These count **units** and are **not** changed:

| Reader | What it does with a unit of bread |
|---|---|
| `SimWorld.FoodTheVillageHolds`, `FoodInGranaries`, `FoodIn(store)` | counts it as one |
| The Food umbrella on the bar (D298) | one |
| `TargetFoodFor(household)`, `TargetFoodForTheGranary` | aims for units |
| The birth gate (`birth_food_percent`, `storage-and-distribution.md §12.4`) | reads units |
| `LabourQuota.VillageIsShortOfFood` | the hunger line, in units |
| `VillageEconomy.MarketStockWanted`, the larder's fetch at ≤ 50 % and its 20 % emergency floor | units |
| Stock limits (`StockLimits.IsMet`), granary capacity | units |

⭐ **Each one under-counts a better food and none over-counts it.** A granary full of bread holds twice
the meals its count says. A larder of bread empties half as fast as its target assumes. The birth gate
sees a poorer village than the real one. **Every error points the safe way**, which is the direction
D48–D50 did not go: each of those was a reader that *over*-counted and a village that starved against
it. A bread village is a little more cautious than it needs to be, never reckless.

⚠️ **One consequence, stated so it is not discovered:** bread is a **denser winter store**. A granary
holds the same number of units and twice the meals. That is a real reason to bake, and it is §2.3's
*"the winter buffer is priced"* (D39) one rung up. It is not a rule this slice adds.

## 4. The chain

### 4.1 The goods (appended, never renumbered)

| Good | Id | Category | Stored by | Nutrition |
|---|---|---|---|---|
| `Flour` | 11 | Materials | **Warehouse only**: kept dry, never a pile, and never a granary (the birth gate measures granary room) | 0, not food |
| `Bread` | 12 | Food | Granary, Market, Cart, like every food (*"the birth gate reads granaries"*) | **2**, the starting point §8 measures |

⚠️ **Ids collide with the modded fixtures and they move, as they did for D391 and D446**:
`ModdedGoodTests.PitchId` (11), `ModdedBuildingTests`' boatman job (11) and any test building at 19+.
That is a fixture renumbering, not a balance change, and the commit says so.

### 4.2 The buildings and trades (appended)

| | Mill | Bakery |
|---|---|---|
| `BuildingKind` | `Mill` (19) | `Bakery` (20) |
| `JobKind` | `Miller` (11) | `Baker` (12) |
| Seats | 2 | 2 |
| Footprint | 2×2: millstones and a store-room, larger than a hut (D382's table gets a row) | 2×1, a hut's row (D382) |
| Materials | timber and a little stone: millstones. Priced with the huts (`smithy_logs`/`smithy_stone` as the reference), measured with §8 | timber and stone: an oven is a hearth |
| `LimitedBy` | `Flour` | `Bread` |
| `UsesTool` | true | true |
| Build bar | *Food*, beside the farmhouse | *Food*, beside the mill |

### 4.3 The grind is the woodcutter's stint

`VillagerState.TravelingToMill` → `Grinding`. A grind takes `wheat_per_grind` wheat from the nearest
store holding it, spends `grind_ticks` at the mill, and puts `flour_per_grind` flour (through
`YieldFor`, so a tool and a technique count) into the nearest store that takes flour. What will not
fit goes on the ground beside the mill (D96, D134). Up to `grinds_per_stint` a day.

### 4.4 The bake is the forge, one good over

`VillagerState.TravelingToBakery` → `Baking`. A bake takes `flour_per_bake` flour from **the one store
holding both** flour and the oven's firewood; a warehouse holds both, which is why flour lives there. The
oven burns `firewood_per_firing` firewood once a stint, when it is lit (Joe's *"a little"*, sized in §8.1
finding 6). A bake spends `bake_ticks` at the oven and puts
`bread_per_bake` bread (through `YieldFor`) into the nearest store that takes food. Overflow goes on the
ground. Up to `bakes_per_stint` a day.

A miller or baker with nothing to do takes the spare work a woodcutter with an empty yard takes.

## 5. The guards in the chain

The chain-starves-in-the-middle guard (`wood-fuel-and-tools.md §8`), once per consumer:

1. ⛔ **The mill never grinds while `LabourQuota.VillageIsShortOfFood`.** Flour is not food. Grinding the
   last of the wheat in a hungry spring turns a meal into a sack nobody can eat until a baker gets to
   it. The sentence: *"Nothing to grind — the village needs its grain to eat."*
2. ⛔ **The oven never burns the winter's firewood** (`LabourQuota.FirewoodShortfall == 0`, the smith's
   rule word for word; it burns a little, Joe's §9 call 1). *"Nothing to bake — the village needs its
   firewood for the winter."*
3. **A met stock limit stops either one** (D139): flour at its limit stops the mill, bread at its limit
   stops the oven.
4. **Every reason is one copy**: `SimWorld.WhyTheMillIsStill(mill)` and `WhyTheOvenIsCold(bakery)`,
   read by the worker before the walk and after every action, and by the card's idle note. That is the
   `WhyTheForgeIsCold` shape, so the card and the worker cannot disagree.

## 6. The quota

- `LabourQuota.BreadShortfall`: bread wanted (the bread stock limit, the player's ceiling) less bread
  held. `BakersWanted` = `ceil(shortfall ÷ BreadBakedPerYearAtWorst)`, and **0 when no store holds
  flour**.
- `MillersWanted`: what keeps the bakers in flour, `ceil(flour the wanted bakers use a year ÷
  FlourGroundPerYearAtWorst)`, and **0 while the village is short of food**.
- The allocator's `KindsInOrder` (`LabourAllocator.cs`) and the quota's own order take **Miller then
  Baker after Smith**. ⚠️ Those are one fact in two places (the smith spec's warning, carried).
- **Player-staffed** (D109). The derived number is the panel's advice, as every trade's is.
- Bread and flour get **stock-limit rows for free**: limits are built over the goods list, under their
  row's category (D409: bread under *Food*, flour under *Materials*). Their default limits are §8's
  to measure.
- ⛔ **Nothing here reaches `VillageEconomy`.** Bread is upside. `StockFloor(Bread)` and
  `StockFloor(Flour)` are 0, with the reason written beside them, as `StockFloor(Tools)` has.

## 7. The gifts

- **`SimWorld.WheatEverReaped`**: wheat brought in by reaping, counted where the reap writes it.
  Hashed sparsely, the `StoneEverDug` shape. **The mill is known at `mill_unlock_wheat`.**
- **`SimWorld.FlourEverGround`**: counted where the grind writes it. **The bakery is known at
  `bakery_unlock_flour`.** The first grind is a natural default, and §8 decides.
- `WhyNotYet` gains two arms, in the village's words:
  - *"Nobody has built a mill here yet — the village has reaped {n} of the {N} wheat it takes to learn."*
  - *"Nobody here bakes yet — the mill has ground {n} of the {N} flour it takes."*
- Each crossing raises its `LearnedByDoing` moment (D442), as the quarry's and the smithy's do.
- Two `TechTree.DefaultNodes` rows, `"mill"` and `"bakery"` (bakery `Requires` mill), with new
  `TechCondition` values that read the same two keys. **No second copy of the number** (the
  `tech-tree-map.md §3.1` rule).
- A row on the milestone map (`DESIGN.md §4`) once §8 says what year a played village meets the mill.
- ⛔ **A cart's or a gift's wheat is not reaped wheat.** Only the reap counts, the way only digging by
  hand counts stone.

## 8. Measured before typed

Nothing in §4–§7 gets a number from this document. A harness arm (`tools/harness`, the
`ZzFirstWinter.cs` shape, kept as a record) poses a mill and a bakery on **twelve fixture seeds ×
fifty years** and on **the shipped played opening**, and measures:

1. **Is a miller and baker pair worth two hands?** Food made per hundred ticks worked, on the ladder
   D286 and D363 built (a forager, a fisher, a hunter, a farmer): a bake's worth is the **extra
   points** it makes, `bread × (nutrition − 1)`. A pair that makes less than two more farmers would is
   a chain the player is right to ignore. **Bread's nutrition (2 or 3) and the batch sizes come from
   here.**
2. **When does a played village meet the mill?** Wheat reaped by year, for `mill_unlock_wheat`. The
   aim is for a village's second or third farm to bring the mill, not the first harvest.
3. **What it does to a village**: alive / peak / starved against the same seeds without a mill, and how
   far apart two members of one household eat (the D28 measure, 99.9 % in lockstep when it was taken).
4. **Firewood** (Joe: yes, a little): the oven's burn against D515's first-winter margins.

The run and its numbers go into this section. The probe that produced them is deleted or kept as a
harness record, never shipped.

### 8.1 ✅ The anchors, measured on the code as it is (D521, 2026-10-06)

Nothing of the chain exists yet, so step 1 measured **what its numbers must be set against**:
`tools/harness/ZzFoodChain.cs` (summary: `foodchain.py`), shipped config, the played opening, seeds 1–16 +
12345, fifty years, six arms. `every` raises a farm, lodge and fishery free at t0 (a fixture effect for
any timeline, D515). `farm0s` / `farm2sw` mark a farmhouse at t0 / Year 2, paint its field once it stands
and staff it as a player would (`SetJobLimit`). A trailing `w` lifts the **wheat** limit alone.
Per seat-year held, from Year 5:

| Arm | wheat a farmer seat-year | forage a forager seat-year | reaped 2k / 5k / 10k by year | dead valleys |
|---|---|---|---|---|
| `farm0sw` (marked t0) | **828** (742–976) | 935 (786–1,245) | **3 / 4 / 7** | 5 / 17 |
| `farm2sw` (marked Year 2) | **755** (598–820) | 1,011 (801–1,275) | **4 / 6 / 9** | 5 / 17 |
| `everyw` (free farm, fixture) | 1,034 (924–1,269) | 928 (818–1,261) | 2 / 3 / 6 (8 of 17 valleys) | 9 / 17 |
| `farm0s` (**shipped wheat limit**) | **0** — never past 2,000 | 1,033 (870–1,324) | never (0 of 13 surviving) | 5 / 17 |

Medians, p10–p90 in brackets.

**What it says:**

1. ⛔⛔ **Today a forage-fed village never eats its wheat, and its farm stops for good after one
   harvest.** Meals take food in id order, so forage (0) is eaten before wheat (9). The wheat sits
   (1,067 held against the 1,000 limit), the limit stays met, and the farm's own card says *"The crop
   stands until the village eats into it, and winter takes the rest"* — every autumn for fifty years,
   in 13 of 13 surviving `farm0s` valleys. **That is the shipped game, not the harness**, and it is why
   ZzFirstWinter's `farm0` never showed a harvest either: a field painted on a *site* never reaps (the
   harness now paints it when the farm stands). ⚠️ §3.3's eat-best-first does **not** cure it, because
   wheat and forage are both worth 1. **Bread does, indirectly**: a mill draws the stored wheat down, so
   the limit stops being met and the farm reaps again. Raised to Joe (§9b).
2. **A farm makes about 2,000 wheat a year from two seats** (a seat-year 755–828), and these villages
   eat far less: produced food ran 135k against 60–83k eaten in the `w` arms. **There is spare grain
   for a mill in any village with a farm.**
3. **The rung a miller and baker must reach.** At nutrition 2, every unit of wheat that becomes bread
   adds one point of food. A pair that keeps up with one farm (~2,000 a year) adds ~2,000 points:
   **~1,000 a seat-year — a little above a farmer (755–828), level with a forager (935–1,033).** At
   nutrition 3 it would be ~2,000 a seat-year, a step far beyond every raw trade. The yardstick is two
   more farmers, which in a forage-fed village add wheat nobody eats (finding 1). **So nutrition 2 is
   enough, and the pair's real value is grain that would otherwise go to waste.**
4. **The mill's gift.** A farm marked at t0 has reaped 2,000 by Year 3, 5,000 by Year 4 and 10,000 by
   Year 7; one marked in Year 2 gets there in Years 4, 6 and 9. `mill_unlock_wheat` **5,000** is about a
   farm's third harvest: the village has proven it farms, and it is not the first autumn.
5. **Lockstep is already broken** (D190): two adults of one household share a hunger value on 1–3 % of
   ticks (D28 measured 100 % before the seeded rhythm). Bread gives a *reason* the card can state, not
   the fix. §8 step 3 measures whether it shows.
6. **The oven's "little" firewood.** A household burns 24 firewood a winter (8 burns × 3). One firewood a
   bake would be ~100 a year, four households' winter. **One a firing** — the oven lit once per stint —
   is ~25 a year, one household's winter. That is how *"a little"* was sized (§4.4 changed to match).

⚠️ **What the harness cannot say.** Dead valleys (5 of 17 in every farm arm) are the unattended harness
(D447), identical with the wheat limit on or off, so not the wheat. `farm0x2sw` (two farms staffed from
four founders at t0) died in 15 of 17 and is the harness's staffing, not a player's. The woodcutter's
splits a seat-year were too thin to use (2 runs, and demand-gated), so the stint sizes below are
**starting values that the post-build arm (step 3) must confirm**, not measurements.

### 8.2 The numbers proposed from it (Joe's to confirm, §9c)

| Key | Proposed | From |
|---|---|---|
| bread `nutrition` | **2** | §8.1 finding 3 |
| `mill_unlock_wheat` | **5,000** | finding 4 |
| `bakery_unlock_flour` | **20** (the first grind) | Joe's call 3: *"after the first flour"* |
| `wheat_per_grind` → `flour_per_grind` | 20 → 20 | sized so one miller keeps up with one farm (~2,000 a year); **confirmed after the build** |
| `grind_ticks`, `grinds_per_stint` | 4, 4 | the woodcutter's split and stint |
| `flour_per_bake` → `bread_per_bake` | 20 → 20 | one baker keeps up with one miller |
| `bake_ticks`, `bakes_per_stint` | 4, 4 | as the grind |
| `firewood_per_firing` | 1 | finding 6 — one household's winter a year |
| mill / bakery seats | 2 / 2 | as the smithy |
| flour / bread stock limits | 200 / 1,000 | a few days' baking; bread as wheat's own limit |

## 9. Joe's calls on the rest — ✅ ALL SIX ANSWERED (D521, 2026-10-06), each as recommended

Joe: *"1. yes 2. not in this slice 3. yes 4. yes 5. perfect 6. sure"*, and *"measurement can start"*.
So: **the oven burns a little firewood** under the winter guard; **one mill, anywhere** (the watermill is
later); **the miller and baker get skill rows**; **the best food is eaten first**; the card reads
*"Full from a meal of bread — not hungry for another 3 days"*; the *Ale* row stays. The questions as
they were put, kept:

1. **Does the oven burn firewood?** *Recommended: yes, a little* (one per batch, under §5 rule 2). It
   ties bread to the woodpile, a trade-off the player can read (*warmth or fullness*), and it is the
   smith's shape exactly. The cost is one more consumer of firewood, guarded the way the forge is.
2. **A watermill by the river?** `buildings-plan.md §4.3` wants *"one building, sited by terrain — on a
   river a watermill and faster; on open ground a windmill and slower."* *Recommended: not this slice.*
   One mill, anywhere. The water bonus is a later slice, or a technique, once the base chain is played.
3. **Skill rows for the miller and the baker?** *Recommended: yes.* Mastery quickens a grind and a
   bake (D174's floor holds), and the row is where §2.7's *"the bakers have opinions about ovens now"*
   will attach. D391 refused the smith a row because a row reshuffled every founding. **D392 fixed
   that** (`founding_trades` is a stated list), so a guard proves no founding moves when the rows land.
4. **Eat the best food first?** *Recommended: yes* (§3.3). Otherwise bread is eaten last and the
   rhythm shows only in want.
5. **The card's sentence.** *Recommended:* *"Full from a meal of bread — not hungry for another 3
   days."* (`FullFor` in days, rounded). And on bread's own row in *What's here*: *"A loaf keeps you
   full twice as long as grain."*
6. **The Overview's *"Ale — no brewer, no barley"* row** stays as a roadmap row until §13 is built
   (`Main.NotYetInTheValley`'s own rule: *"delete a row when it ships"*).

### 9b. ⏸️ Raised by the measurement: the farm that stops for good (§8.1 finding 1) — Joe's

A forage-fed village never eats its wheat, so with the shipped limit (1,000) a farm reaps once and then
stands idle every autumn, for decades, while its card promises the village will *"eat into it"*. Three
ways to answer it, and the recommendation:

- **(a) Let the mill answer it** *(recommended)*. Once there is a mill, stored wheat becomes bread, bread
  is eaten first, and the farm reaps again. Until there is a mill, the card's sentence is true and the
  player can raise the limit. Nothing extra to build.
- **(b) Fix eating now, its own small slice:** among foods of equal worth, eat the one the larder holds
  most of. That rotates the diet and drains a pile, but it moves every golden that eats two foods, and it
  is a rule about wheat, not bread.
- **(c) Change the card**, so it says *"the village eats its forage first"* instead of promising an
  eating that will not come.

### 9c. ⏸️ The numbers in §8.2 — Joe's to confirm

Bread at **2**, the mill after **5,000** wheat reaped, the bakery after the **first grind**, the oven
lit for **1** firewood a stint. Batch sizes are starting values, and the post-build arm checks that
one miller and one baker keep up with one farm.

## 10. Failure modes designed against

- **A chain that only costs hands**: the reason for §0 call 1, and §8.1 measures it before it ships.
- **A floor that over-counts**: §3.1 and §3.4. Every units reader under-counts, and the floor is solved
  at the least a food can be worth.
- **The chain starves in the middle**: §5 rules 1–2. Flour is not eaten, so the mill stops when people
  are hungry.
- **An invisible multiplier** (§1.1): `FullFor` is on the card in words, and bread's row says what it
  is worth.
- **A silent stall**: every reason a mill or an oven stands still is one `WorkNote` naming the store.
- **A per-tick rebuild**: the eat order is sorted once at load. `FullFor` is state changed where a meal
  is eaten. The quota is read when the quota runs.
- **Lockstep by another door**: a household that all eats bread together is still in step with itself.
  The variation comes from **what each one happened to eat**, and §8.3 measures whether it is real.
- **A seed contract by accident**: the skill rows (§9 call 3) are guarded against moving a founding.
  Bread and flour join no draw.

## 11. How it is tested (written first, each red-checked; D326)

`tests/Bclone.Sim.Tests/FoodChainTests.cs` and `NutritionTests.cs`, named for what they guard:

- `EveryFoodAtOneIsTodayToTheUnit`: a village with no bread hashes as before (the rule lands
  byte-identical).
- `TheFloorReadsNoNutrition`: raising bread's nutrition moves no `VillageEconomy` number.
- `AFoodWorthLessThanOneIsRefusedAtLoad`, and `TwoNutritionValuesNowLoad` (the old throw's inverse).
- `AMealOfBreadHoldsHungerOff`: P, C and `FullFor` to the tick, and **hunger never negative**.
- `AMixedMealIsWorthItsParts`, and `ADependantsMealScales`.
- `TheBestFoodIsEatenFirst`, and `TheMillDrawsDownStoredWheatAndTheFarmReapsAgain` (§8.1 finding 1).
- `FullForIsSaved` (the field guard covers it, and is red-checked by dropping it).
- `TheMillWaitsWhileTheVillageIsHungry`, `TheOvenNeverBurnsTheWintersFirewood`,
  `AMetLimitStopsTheMillAndTheOven`.
- `FlourAndBreadComeOutOfAStoresCount`: nothing conjured, and the `TryTake` return is read (D96, D144).
- `TheMillIsAGiftOfReapedWheat`, `TheBakeryIsAGiftOfGroundFlour`, and `OnlyTheReapCounts`.
- `NoFoundingMovesWithTheNewSkillRows`.
- `ShippedConfigTests`: the shipped file loads with bread at its measured nutrition.
- The probe gains a line for the mill and bakery cards if the build bar's height moves (it must stay 151).

## 12. Definition of Done

1. This spec current, with its status line true against the suite (D159).
2. §8 measured and written in, with Joe's call on the numbers.
3. §11's guards written first, passing, each red-checked, the reds counted and any zero written down.
4. Determinism green. Goldens move **only** in the commit where bread is first eaten, and are proven to
   move for that rule only.
5. The four CLAUDE.md verification lines. The probe stays green at bar height 151.
6. Joe plays it: a mill appears, a bakery after it, a card says *"full from a meal of bread"*.
7. `DESIGN.md` §4/§5/§6/§7, `food-catalog.md`, `buildings-plan.md` and `HANDOFF.md` updated in the same
   commits.

## 13. Beer — deferred, with its shape (Joe's call 2)

Beer is for **morale** (`morale.md`): a tavern within reach is one of its named reasons to stay.
Morale and the tavern are Phase 6 and unbuilt, so beer would arrive as food or as nothing.

- ⛔ **Not food.** "Liquid bread" as a third nutrition value would make the brewery a worse bakery and
  would spend beer's real job before morale exists to receive it.
- **The shape when it comes:** a brewery (`buildings-plan.md §4.2`: *"barley → ale"*) of the bakery's
  shape. The grain is **barley or wheat**, Joe's to name then (`food-catalog.md §4` lists barley as
  *"a dedicated beer grain if wheat is reserved for bread"*; a second crop is a `CropRow` plus a
  *"grow…"* control, `crops-and-orchards.md`). Ale is drunk at the tavern, and morale reads it.
- Its gift would be by doing too (*"Brewery: by doing"*, `buildings-plan.md §4.2`).
