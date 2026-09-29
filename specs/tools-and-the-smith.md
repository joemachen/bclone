# Spec: Tools that wear, and the smith's hut — Phase 5's first production chain

**Decisions:** D391 (this document), D395 (Joe: the bonus goes on ticks), D429 (ticks at 34 %,
stacked with mastery by multiplying, **beside** the yield bonus — ticks alone measured at half its worth). Neighbours: D17/D64 (tools arrive in the cart and nobody can
replace them), D29 (the conversion workplace — the woodcutter's shape), D84/D90 (iron is a seam
the laborers clear), D107 (the shape every profession shares), D109 (the player staffs), D139 (a
met limit stops the work), D174/D187 (the novice floor; mastery bites in one seam), D196/D225 (a
technique bites in one seam), D291 (the hash is sparse over goods), D353 (the shape settled: wear
per use, slower without, never a break year), D378 (amber is the trade's quota).
**Status:** ✅ **built (2026-09-18, D391) and played.** ✅ **§3.4 BUILT FOR D429 (2026-09-29) on
`slice/tools-on-ticks`: a tool takes a third off the ticks AND keeps its quarter on the yield** —
specced before the code as ticks-instead-of-yield, measured, and changed to both by Joe's call on
the numbers (§6.1). `ToolsTests` fifteen guards; **unplayed by Joe as of this line.** Owner: Joe +
Claude Code.

---

## 1. Why this exists

Joe, 2026-09-17: *"then i think i want to go back to building out the professions and their
buildings."* Asked 2026-09-18 which chain first, against `buildings-plan.md §4`'s tiers and
`DESIGN.md §4` Phase 5's list — tools (the smith), bread (mill and bakery, waiting on the diet
derivation), stone (a quarry), maintenance (the workshop): **"Tools — the smith."** And how deep:
**"One building"** — the smelter and the charcoal burner stay later nodes of `tech-tree.md §9.4`.

What the code had before this spec, traced rather than remembered:

| Thing | State |
|---|---|
| `Goods.Tools` (id 4) | 20 in the founders' cart (`cart_tools`); stored by warehouse, cart, pile; **consumed by nothing** — `VillageEconomy.StockFloor` returns 0 with a comment saying so |
| `Goods.Iron` (id 5) | two seams a valley, 8 a tile, cleared by laborers under the harvest brush; **used by nothing** — *"reaching the iron is something the player chooses to do"*, for no payoff |
| A conversion workplace | the woodcutter: input taken from a store, a timed action at the hut, output to a store, a refusal sentence naming the store, a stint (D29, D384) |
| The bonus seams | `SimWorld.WorkTicksFor` (mastery, on ticks — **and the tool beside it since D429**) and `SimWorld.YieldWithTechnique` (a technique, on yield) — **neither reaches `VillageEconomy`**: the survival floor is solved for a village that knows nothing, and every bonus is upside above it |
| D353's ruling | *tools wear per use; a worker without one works at today's baseline and a tool is the bonus; never a break year (a Year-4 break was a cliff on D122's floor); uses-per-tool measured on the twelve-seed arm before it is typed* |

So the slice is two things that were each already half-decided: **give the tools something to do
and the iron something to become.**

## 2. Which pillars this serves

- **§2.2 — food comes from many kinds of work, and above it the processing tier.** The smith is
  the first chain whose input is not food or wood: iron the player chose to dig, made into a thing
  every trade uses. D39's *"an input consumed to make an output, a workplace that can be idle for
  want of stock"* — the D29 shape, built once for firewood, proved again here.
- **§2.1 — *"a skilled worker with a good tool"*.** `wood-fuel-and-tools.md §7` deferred tools
  *"until there is a workshop to make them at"* and asked that they hang off the villager, not the
  household, so the skill pillar could later say that sentence without a migration. It can now.
- **§2.7 — the smith's chain is content the tree already names** (`tech-tree.md §9.4`: forge /
  iron tools, *Metalworking*), and the smelter above it is the next node rather than this slice.
- **§1.1 — legible, in every sentence:** a villager card says *carrying a tool, 37 uses left*; a
  smith with no iron says which store is empty; a forge that would burn the winter's firewood says
  so; the tools chip goes amber by the smith's own quota.
- **§0.1 — cozy, not cruel:** the founders' tools are *a gift that fades*, not a bomb. There is no
  year in which everything breaks; there is a slow slide back to today's baseline unless somebody
  builds a smithy, and the slide is visible on every card.

## 3. The rules

### 3.1 A tool hangs off the villager

`Villager.ToolUses` — the uses left in the tool in their hands; **0 is no tool.** Hashed, sparsely
(non-zero only, under a tag so a tool of five and a forge stint of five cannot read the same —
D291's pattern), so a village posed with `cart_tools: 0` and no smithy hashes byte-identically to
one before this slice existed. ⚠️ Every
shipped golden moves once anyway: the founders' twenty are picked up in every village.

A villager who dies with a tool takes it with them — the cheapest honest rule, stated rather than
silently true. (A tool left on the ground where somebody fell is a later kindness, if play asks.)

### 3.2 Which trades use a tool is a column, not a switch

`JobRow.UsesTool` (`"uses_tool"`, default false). Built in: **forester, woodcutter, farmer,
fisher, hunter, forager — true; marketer — false; builder — false, in this slice.** A builder's
bonus would be on a site's work ticks, which is a second seam (§3.4); the builder takes a hammer
when Phase 5's maintenance item grows the builder's hut into a workshop. Written down here so it
is a decision and not an omission.

### 3.3 Wear is one use per work action begun

A gather, a fell or a planting, a split, a cast, a hunt, a sown or a reaped tile, a forge. The
eleven places an action begins all called `SimWorld.WorkTicksFor`; they call
**`SimWorld.BeginWork(villager, trade, ticks)`** now, which reads the ticks (`WorkTicksFor`, with
the tool in hand counted — §3.4), **then** wears the tool (`ToolUses` down by one when it is above
zero, and only for a trade whose row says `UsesTool` — the tool in the hand and the action's trade,
not the job held: a forager who helps clear painted ground swings the axe in their hands), and
returns the ticks. `WorkTicksFor` stays the pure reader it is named as. ⭐ **Read before the wear,
on purpose (D429):** the tool that begins an action is the tool that quickens it, so the last use
speeds its own action.
When the last use goes the log says so at DEBUG (*"Hattie's tool is worn out"*) and Hattie works on
at today's number.

### 3.4 The bonus is on ticks AND on yield — Joe's calls (D395, D429)

> ⭐ **What shipped, in one line:** a tool in hand takes 34 % off the ticks of the action it begins
> (multiplied with mastery, rounded once) **and** adds 25 % to what the action brings in. This
> section was written first as ticks *instead of* yield; §6.1's runs showed that at about half of
> what tools had been worth, and Joe chose both. The reasoning below is kept as it was specced.

Joe, 2026-09-19 (D395): ***"tool bonus on ticks (and eventually on yield when user unlocks upgrades
for it)"***. D391 had built it on yield, and this section said so and called it *"the one call Joe
can overrule"*. He has. The D391 reasoning is kept at the end of this section, because what it
warned about is still true and the number below was chosen with it in mind.

**The rule.** A tool in hand for a trade whose row says `UsesTool` takes
**`tool_speed_bonus_percent`** (34) off the ticks of the action it begins, in
`SimWorld.WorkTicksFor`, beside mastery. Integer (D2), and never below one tick.

**Why 34, and what it does at these durations.** The reduction is rounded down, as mastery's is, so
a percentage only matters once it amounts to a whole tick. 34 % is where a three-tick gather gets
one:

| Action | Base ticks | With a tool | Speed-up |
|---|---|---|---|
| gather, sow, reap | 3 | 2 | 1.5× |
| fell / plant, split, forge | 4 | 3 | 1.33× |
| cast | 10 | 7 | 1.43× |
| hunt | 15 | 10 | 1.5× |

⚠️ **This is not even across trades, and the table is the honest statement of it.** The four-tick
trades gain a third where the rest gain close to a half. It is much closer than D391 feared, though.
At 25 % the gather gains nothing and the hunt gains a third, but at 34 % every trade gains
*something*, and the spread is 1.33–1.5×.

**With mastery: multiplied, one rounding (Joe, 2026-09-29).** The ticks left are
`base × (1 − mastery share × 50 %) × (1 − 34 %)`, rounded **once**, so the order the two are applied
in cannot change the answer. In integers, where `m` is `mastery_speed_bonus_percent`, `share` is
0–100 of the way to mastery, and `t` is the tool's percent when a tool is in hand (else 0):

```
faster = base × (1,000,000 − (10,000 − m·share) × (100 − t)) / 1,000,000     (rounded down)
ticks  = max(1, base − faster)
```

With `t = 0` this is exactly today's `base·m·share / 10,000`, to the unit, so a village with no
tools hashes as it did. For a master (base → novice with tool / master / master with tool):
**3 → 2 / 2 / 1** and **4 → 3 / 2 / 2**. The fisher, the hunter and the smith have no skill row
(§6), so they get the tool alone. ⚠️ *A master woodcutter or forester gains nothing from a tool,*
because 4 × 0.33 = 1.32 ticks left rounds up to 2 (the reduction is rounded down). That follows from the rule rather than being a special
case, and the card should not promise otherwise.

**Mastery ships at 50 %, not 34 %.** The handoff that queued this slice said *"both 34 %"*.
`mastery_speed_bonus_percent` is 50: 34 was tried first and found marginal (its remarks in
`SimConfig`).

**~~The yield dial stays, at 0.~~ The yield dial stays at 25 (Joe, D429, on §6.1's numbers).** As
specced, `tool_yield_bonus_percent` went to 0 and was kept for Joe's *"eventually on yield when user
unlocks upgrades for it"*. Measured, ticks alone left tools worth about half of what they had been,
because **a forager's trip is one gather between two walks**: 3 → 2 ticks saves one tick in about
fifteen. Joe: ticks 34 % plus yield 25 %. The later upgrade becomes *more* yield, or iron tools
(D395), rather than switching the yield on.

**A tool might wear faster, and that is accepted (Joe, 2026-09-29) — measured, it does not.**
Quicker actions could mean more of them a year, and a tool is `tool_uses` actions. (§6.1: work is
demand-gated, so a quicker hand is stood down sooner rather than working more; the wear rate did not
move.) Joe: ***"keep 150 for default tools — there will be
different tool types later with different durabilities (stone v iron for example)"*** — D395's
smithy gift, where the village starts with stone tools and the smith makes iron ones. How long a
tool lasts is that slice's number. §6 records the lifetime under ticks.

⛔ **Nothing reaches `VillageEconomy`.** No tool = today's ticks to the unit — the D174 novice floor
and D187's *nobody is ever worse than today*, guarded in §7.

*D391's reasoning, kept:* *"at these durations a percentage on ticks is a step, not a ramp — a gather
is 3 ticks, a split 4, a cast 10, a hunt 15 — so a 25 % tool buys a forager nothing (0.75 of a tick
rounds to none) while at 50 % a hunt goes 15 → 8 and a gather 3 → 2."* That is still true. What it
argued was that no percentage on ticks treats every trade alike. The table above shows the closest
this one gets.

### 3.5 A tool is taken from a store, as an errand

A villager who holds a job in a tool trade and has no tool, when they next decide what to do,
walks to the nearest store holding a tool (warehouse, cart, pile — `Goods.Tools`' row), takes one
(`TryTake`, ⛔ the return read: D96, D144), and their hands hold `tool_uses`. `VillagerState.
FetchingATool`: the errand's tile fixed at departure, the `ClearingABuffer` shape. They fetch
whether or not their trade has work today — a hand with a job keeps a tool — which is one rule
rather than a rule per season. When no store within reach holds one, `WorkNote` says *"working
without a tool — none in any store"* and they work.

**The market does not stock tools in this slice.** Tools are not food or fuel; a tool is fetched
once in `tool_uses` actions, not once a half-larder. Measured, §6.

### 3.6 The smith's hut

`BuildingKind.Smithy` (15), `JobKind.Smith` (8) — appended, hashed by position, never renumbered.
Rows in `SimConfig` beside the woodcutter's: `smithy_logs` (a hut's 25), `smithy_stone` (12 — a
forge is a hearth of stone, the one hut that costs a lodge's worth), `smithy_work_ticks`,
`smithy_capacity` (**2**, `TECH-EXAMPLE.md`'s two smiths), 2 × 1 (D382's hut row),
`LimitedBy = Goods.Tools`, `UsesTool = true`. ⛔ **No `SkillRow`** — §6 says why: a row reshuffles
every founding. Build bar: *Works*, beside the builder's. The card's idle note is
`WhyTheForgeIsCold`, the one copy the smith reads too.

### 3.7 The forge is the woodcutter's stint, one good over

`VillagerState.Forging` after `TravelingToSmithy`. A forge takes `iron_per_tool` iron and
`firewood_per_tool` firewood from the nearest store holding both, spends `forge_ticks` at the hut,
and makes `tools_per_forge` tools — through `YieldFor` — into the nearest store accepting tools;
what will not fit goes on the ground beside the hut (D96, D134). Up to `forges_per_stint` a day
while:

1. the tools limit is not met (`StockLimits.IsMet(Goods.Tools, tools in the stores)` — D139:
   a met limit stops the work, not just the hiring);
2. a store within reach holds a forge's iron and firewood — else the refusal names the store and
   the good, as the woodcutter's does;
3. ⛔ **`LabourQuota.FirewoodShortfall(world) == 0`** — a forge never takes firewood the homes
   still want. The sentence: *"Nothing to forge — the village needs its firewood for the winter."*
   This is the chain-starves-in-the-middle guard (`wood-fuel-and-tools.md §8`) one consumer
   over: a smith who could burn the woodpile in autumn is a smith who freezes a household.

A smith with nothing to forge takes the spare work a woodcutter with an empty yard takes.

### 3.8 The quota wants smiths when tools run short

`LabourQuota.ToolShortfall(world)` = tools **wanted** − tools **held**, where wanted is one per
hand in a tool trade (a tool in every working pair of hands) plus what those hands will wear
through in a year (`ToolsWornPerYearAtWorst`, §6's measured pace), and held is the tools in every
store plus the tools in hands (a hand holding a tool is a hand that will not fetch one).
`SmithsWanted = ceil(shortfall ÷ ToolsForgedPerYearAtWorst)` — the `WoodcuttersWanted` shape. A
`smiths` slot in the quota; `KindsInOrder` takes Smith **after Woodcutter and before Marketer**
(⚠️ the allocator's and the quota's orders are one fact in two places). Player-staffed like every
trade (D109); the derived number is the panel's advice.

**The tools chip goes amber when `SmithsWanted > 0`.** D378 said *stone and tools never go amber*
because neither had a quota; tools have one now, and the bar reads it the way it reads the
woodcutter's. Recorded as the change to D378.

### 3.9 The survival floor does not move

`VillageEconomy.StockFloor(Goods.Tools)` stays **0**, and its comment changes from *"nothing spends
them yet"* to the reason: tools are upside above the floor (§3.4), so there is no floor to derive
— the quota (§3.8) is what asks for them. `cart_tools` stays a stated number, not a derivation.

## 4. Data

`data/sim.config.json`, each with its reason beside it:

| Key | Default | What |
|---|---|---|
| `tool_uses` | 150 (§6) | actions one tool lasts — kept at 150 under ticks by Joe's call; the wear rate did not move (§6.1); stone and iron tools will carry their own |
| `tool_speed_bonus_percent` | 34 (§3.4, D429) | what a tool takes off an action's ticks — multiplied with mastery, rounded once |
| `tool_yield_bonus_percent` | 25 (§6, kept by D429's §6.1) | what a tool adds to an action's yield, technique counted — beside the ticks |
| `smithy_logs` | 25 | a hut's timber |
| `smithy_stone` | 12 | a forge is a hearth of stone |
| `smithy_work_ticks` | 40 | as the huts |
| `smithy_capacity` | 2 | seats |
| `iron_per_tool` | 4 | half a seam tile of ore |
| `firewood_per_tool` | 4 | a batch, never the winter's (§3.7) |
| `forge_ticks` | 4 | a day, as a split |
| `tools_per_forge` | 1 | tools a forge makes |
| `forges_per_stint` | 4 | a day's forging, as `splits_per_stint` |

Validated at load: capacities and uses above zero, both bonuses 0–100.

## 5. Failure modes designed against

- **The gift is a cliff** (D353's refusal): no break year. Wear is per action, so a village that
  works harder wears faster, and the cards say how many uses are left.
- **The bonus reaches the floor:** `AWorkerWithoutAToolWorksAtTodaysNumberToTheUnit`.
- **A tool helps one trade and not another by rounding:** 34 % is chosen so every trade gains a
  whole tick (1.33–1.5×, §3.4's table). The four-tick trades gain least, and a master woodcutter
  gains nothing, and the spec says so instead of hiding it.
- **Mastery and a tool compound into nothing, or into a four-times worker:** one product, one
  rounding (§3.4). Rounding each separately would let the order decide; adding them would take a
  four-tick split to one tick.
- **The chain starves in the middle:** the firewood guard (§3.7 rule 3); iron is the player's
  choice to dig and the smith's refusal names it.
- **A silent stall:** every reason the forge does not run is a `WorkNote` naming the store.
- **A conjured tool:** every tool in a hand came out of a store's count (`AToolIsFetchedFromAStore`).
- **A per-tick rebuild:** nothing here is derived per tick; `ToolUses` is state changed where the
  action begins, `ToolShortfall` is read when the quota runs (once a season, as every arm is).

## 6. Measured before typed

A throwaway probe (deleted, as the D353 rule asks: the run goes in the document), on the code
as built:

**Actions a hand-year.** Twelve fixture seeds × fifty years, counting every action begun by
trade (`SimWorld.WorkActionsBegun`, a statistic kept for the next re-measure): **~55 actions a
hand-year** across the hands that hold a job — 23,700 gathers, 900 fells, 1,250 splits over 456
hand-years. The shipped cold start counts 230–320 because a laborer's clearing is counted under
the forester's trade and laborers hold no job. So at **`tool_uses` 150** a tool is about three
years of one pair of hands; the founders' twenty are all taken within the first fifty years of
every seed (232 taken over twelve). *A gift that lasts a generation, not a run.*

**The bonus.** Twelve fixture seeds × fifty years, alive / peak-sum / starved:

| Arm | Fixture | Shipped cold start, played opening |
|---|---|---|
| no tools at all | 201 / 222 / 0 | 78 / 116 / 31 |
| wear only, bonus 0 | 202 / 221 / 0 | — |
| bonus 15 | 203 / 227 / 0 | — |
| **bonus 25** | **218 / 238 / 0** | **98 / 133 / 19** |

Wear alone is noise; 25 % is +8 % people over fifty years in the fixture and a quarter fewer
starved in the cold start. The food ladder's rungs hold by construction: the same percentage
on every trade's yield moves every rig alike. **25 shipped.**

**The walk.** Tools taken per hand-year: **0.5** — one fetch walk every two hand-years. Not the
market's business; §3.5 stands.

**What the runs found on the way** (⛔, each in its own place):
- A warm start never put the founders' tools anywhere — `StockTheFoundingStores` said *food and
  tools* and stopped at the granary, which holds none. Harmless while nothing spent a tool.
  Fixed: into the warehouse.
- **A skill row for the smith reshuffles every founding.** `SimWorld`'s founders draw their
  mastered trades from the skills catalogue with the run's `Rng`, so a seventh row moved every
  seed's founders — the fixture's stores read full where they had read 2,424 held, and three
  food-limit guards went red for a village that had never seen a tool. D344's trap one catalogue
  over. **The smith has no skill row**, like the fisher and the hunter (found to be rowless the
  same way); a skill for the three wants the founding's draw made from a stated list first.
  ✅ **The list is D392** (`founding_trades`, the same day); the three rows are the measured
  slice still filed in `handoff.md` — mastery would halve a cast and a hunt.

### 6.1 Measured under ticks (D429)

`main` (yield 25, no ticks) run on a `git worktree` of `main`, never through a dial; the other three
arms on the branch, through the two dials and `cart_tools` (a throwaway harness, deleted).

**D420's 55 fifty-year villages** (30 shipped + 12 fixture + 13 every-source), alive / peak / starved:

| Arm | Alive | Peak | Starved |
|---|---|---|---|
| no tools at all | 321 | 529 | 152 |
| **ticks 34 %, yield 0 — as specced** | **380** | 587 | **159** |
| yield 25 %, no ticks — `main` (D391) | 440 | 631 | 113 |
| **ticks 34 % + yield 25 % — shipped (Joe)** | **444** | 628 | **122** |

The shipped seeds alone read 96 / 112 / 169 / 181 alive in that order. Ticks alone kept about half of
what tools are worth; **the reason is structural, not the number**: a forager's trip is one gather
between two walks, a fisher's and a hunter's one cast or one hunt, so a third off the action is a
tick or a few off a trip of fifteen-plus. No tick percentage mends that — the next step for a
three-tick gather is 67 % (3 → 1).

**The rigs** (food per hundred ticks worked, demand held open — trap 30): `main` forager 342, fisher
761, hunter 1,484; ticks alone forager ~280 *(the rig read 188 — see below)*, fisher 589, hunter
1,150 (−17 to −23 %); **both: forager 324, fisher 769, hunter 1,447.** The ladder's order and its
ratios hold (~2.4× and ~1.9×).

**Wear.** Twelve fixture seeds × twenty years: **10.6 actions and 0.19 tools taken a hand-year on
`main`, 10.5 and 0.19 on the branch** — the rate did not move, because work is demand-gated (a
quicker hand is stood down sooner, not kept busier). ⚠️ This count disagrees with §6's *~55 actions
a hand-year* by about five times; the denominator here is every hand holding a tool-trade seat,
idle or not, and the two are not reconciled.

**Found on the way** (⛔, each in its own place):
- **The forager rig counted trips as gather ticks ÷ `gather_ticks`** (`FishingTests.
  WhatAForagerBringsInAYear`), so with a tool (2 ticks a gather) it counted two trips in three and
  read 188 where the forager was near 280. It counts gathers begun now. ⚠️ Its red check scores
  **zero**, written down: the old count only made the forager read low, and the guard asserts the
  fisher out-earns it. It still prices a trip at the bare hut's number (a tool's quarter uncounted).
- **Three guards that were really measuring food went red under ticks alone and green again under
  both** — the builder behind a starved head (D414's open item: nobody took the builder's seat for
  the whole year), the market stocked to its limit, the food limit's foragers. The labour quota puts
  every hand on food while the village is short; they are not re-posed.
- **The timber gate's guard was a coin** (`WoodTests.TimberGatesGrowthWithoutStoppingIt`): the
  fixture dies out unattended on about half its seeds by year 120 with or without the gate (twelve
  seeds, gated / free alive: `main` 43 / 28, branch 51 / 32), and 12345 came up 0. Re-posed on seed 6,
  whose ungated village lives on both, with the table beside it.

## 7. How it is tested — `tests/Bclone.Sim.Tests/ToolsTests.cs`

- `AToolWearsOncePerAction` — red with the wear off.
- `AWorkerWithoutAToolWorksAtTodaysNumberToTheUnit` — red with the bonus handed to a bare hand;
  ticks and yield both (D429).
- `AToolMakesTheActionItBeginsAThirdQuicker` — §3.4's table, trade by trade (D429).
- `AMasterWithAToolMultipliesOnce` — 3 → 1 and 4 → 2; a master without one keeps 2 / 2 (D429).
- `TheLastUseStillSpeedsItsOwnAction` — the ticks are read before the wear (D429).
- `AToolIsQuickerAndBringsInMoreAsShipped` — data and C# agree: 34 on ticks, 25 on yield (D429).

Red-checked (D326), reds counted against each break: the tool's percent never read 4; the two
**added** instead of multiplied 1; rounded **separately** 1; the wear **before** the read 1; the bonus
on a bare hand 5; the yield dial at 0 in the data 1. The forager rig's trip count scores **zero**
(§6.1).
- `AToolIsFetchedFromAStoreNotConjured` — the cart 20 → 19; red: no store, no tool, and the
  note says so.
- `ASmithForgesToolsFromIronAndFirewood`.
- `AForgeNeverTakesTheWintersFirewood` — red with the guard off.
- `AMetToolsLimitStopsTheForge`.
- `SmithsAreWantedOnlyWhenToolsRunShort`.
- `AVillageWithNoToolsAndNoSmithHashesAsBefore`.
- `UsesTool` round-trips through JSON (`ModdedJobTests`' shape).
- Determinism green; the shipped goldens move once, counted, with this reason.

## 8. Definition of Done

Standard (`METHODOLOGY.md §3`), plus: a smithy can be built, staffed and seen forging; the
founders' tools wear out on the cards; the tools chip goes amber; a village with no smithy plays
exactly as today once its tools are gone; `DESIGN.md §4` Phase 5's tools bullet, §5's tools entry,
§6 and D391 in §7; `professions.md §4` true (the fisher and the hunter shipped a phase ago and its
table still said *new*); `wood-fuel-and-tools.md §7`'s deferral spent; `handoff.md`.
