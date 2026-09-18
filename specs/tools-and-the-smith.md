# Spec: Tools that wear, and the smith's hut — Phase 5's first production chain

**Decisions:** D391 (this document). Neighbours: D17/D64 (tools arrive in the cart and nobody can
replace them), D29 (the conversion workplace — the woodcutter's shape), D84/D90 (iron is a seam
the laborers clear), D107 (the shape every profession shares), D109 (the player staffs), D139 (a
met limit stops the work), D174/D187 (the novice floor; mastery bites in one seam), D196/D225 (a
technique bites in one seam), D291 (the hash is sparse over goods), D353 (the shape settled: wear
per use, slower without, never a break year), D378 (amber is the trade's quota).
**Status:** ✅ **built (2026-09-18, D391)** — written before the code, as METHODOLOGY §2 asks; the
numbers in §4 come from the runs in §6; `ToolsTests` (eleven guards, six red-checked, two zeros
written down). **Unplayed by Joe as of this line.** Owner: Joe + Claude Code.

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
| The bonus seams | `SimWorld.WorkTicksFor` (mastery, on ticks) and `SimWorld.YieldWithTechnique` (a technique, on yield) — **neither reaches `VillageEconomy`**: the survival floor is solved for a village that knows nothing, and every bonus is upside above it |
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
**`SimWorld.BeginWork(villager, trade, ticks)`** now, which wears the tool (`ToolUses` down by one
when it is above zero, and only for a trade whose row says `UsesTool` — the tool in the hand and
the action's trade, not the job held: a forager who helps clear painted ground swings the axe in
their hands) and returns the ticks. `WorkTicksFor` stays the pure reader it is named as.
When the last use goes the log says so at DEBUG (*"Hattie's tool is worn out"*) and Hattie works on
at today's number.

### 3.4 The bonus is on yield, not on ticks — ⭐ the one call Joe can overrule

Joe's phrase is *"slower without"*, and read literally that is a bonus on the action's ticks.
`WorkTicksFor`'s own remark is why it is not built that way: **at these durations a percentage on
ticks is a step, not a ramp** — a gather is 3 ticks, a split 4, a cast 10, a hunt 15 — so a 25 %
tool buys a forager nothing (0.75 of a tick rounds to none) while at 50 % a hunt goes 15 → 8 and
a gather 3 → 2, a hunter half again as fast and a forager a third. *A tool that helps one trade
and not another by an accident of duration is the illegible outcome §1.1 forbids.*

`YieldWithTechnique` already applies an even percentage to what an action brings in. The tool
stands beside it: **`SimWorld.YieldFor(villager, trade, base)`** = the technique's yield, then
**`WithTool`**: `+ amount × tool_yield_bonus_percent / 100` when `ToolUses > 0` and the trade's
row says `UsesTool` — on the amount *with* the technique, integer, rounded down (D2), so a tool
never invents a unit. The six yield sites call one or the other (the forager's trip and the farm's
tile through `WithTool`, because `GatherYieldAt` and `CropYieldAt` fold the technique in before
the villager is known and the cards quote them; the fell, the cast, the kill and the split through
`YieldFor`). A smith with a tool forges more by the same line — one tool a forge at 25 % is still
one tool.

⛔ **Nothing reaches `VillageEconomy`.** No tool = today's number to the unit — the D174 novice
floor and D187's *nobody is ever worse than today*, guarded in §7.

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
| `tool_uses` | 150 (§6) | actions one tool lasts — about three years of one pair of hands |
| `tool_yield_bonus_percent` | 25 (§6) | what a tool adds to an action's yield, technique counted |
| `smithy_logs` | 25 | a hut's timber |
| `smithy_stone` | 12 | a forge is a hearth of stone |
| `smithy_work_ticks` | 40 | as the huts |
| `smithy_capacity` | 2 | seats |
| `iron_per_tool` | 4 | half a seam tile of ore |
| `firewood_per_tool` | 4 | a batch, never the winter's (§3.7) |
| `forge_ticks` | 4 | a day, as a split |
| `tools_per_forge` | 1 | tools a forge makes |
| `forges_per_stint` | 4 | a day's forging, as `splits_per_stint` |

Validated at load: capacities and uses above zero, the bonus 0–100.

## 5. Failure modes designed against

- **The gift is a cliff** (D353's refusal): no break year. Wear is per action, so a village that
  works harder wears faster, and the cards say how many uses are left.
- **The bonus reaches the floor:** `AWorkerWithoutAToolWorksAtTodaysNumberToTheUnit`.
- **A tool helps one trade and not another by rounding:** yield, not ticks (§3.4).
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

## 7. How it is tested — `tests/Bclone.Sim.Tests/ToolsTests.cs`

- `AToolWearsOncePerAction` — red with the wear off.
- `AWorkerWithoutAToolWorksAtTodaysNumberToTheUnit` — red with the bonus handed to a bare hand.
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
