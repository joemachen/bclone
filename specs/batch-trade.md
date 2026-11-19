# Spec: The batch trade as a row — W1

> Status: **✅ BUILT (D792–D795, 2026-11-18) and ✅ MERGED INTO `main` AND PUSHED AT JOE'S WORD (D796, *"Merge and
> push"*): steps 1–6, the four checks green on the merged tree, no golden moved. ⏸️ His play of any village with a mill,
> a bakery, a tailor and a workshop (§9).** *(Read "✅ BUILT on `slice/batch-trade` … ⏸️ The merge at Joe's word" at
> D795.)* History: 📝 Specced (D791, 2026-11-17); ✅ Joe's call A (D792, *"A, as
> recommended"*) — one shared pair of states, the trade a field beside it (§7). ✅ Step 1, today pinned (D792): 28 tests,
> 11 mutants, 11 red. ✅ Steps 2 and 4 — the row, and hands and stops by it (D793, one commit). ✅ Step 5 — one shared
> pair of states and `Villager.BatchTrade` (D794); **no golden moved**. ✅ Step 6, a fifth trade from its row (D795).**
> Phase 6, W1 of the workplan (`systems-complete.md §5.1`, D788–D789): the enabler before beer (W3), health's herbs (W6)
> and the wool coat (W10), each of which would otherwise be a fifth, sixth and seventh hand-written copy.
>
> Neighbours: `food-chain.md §4` (the `Batch` shape, D522), `clothing.md §8` (*"every Miller/Baker switch gains Tailor"*),
> `workshop-and-the-carpenter.md §4.3–§4.5` (the carpenter joined every switch; `made_from`), `tools-and-the-smith.md`
> (the smith, which keeps its own path), D281 (a reused state is a reused classification), D218 (the quota indexed by job
> id so a trade can be added), D510 (a save is read whole or refused).

---

## 1. Why

The review (D788) said each batch trade was *"hand-written: its own VillagerState pair plus ErrandKind arms"*. **Traced
on the code (2026-11-17), half of that is already fixed:** the decide, the walk, the work, the put-away and every reason
to stand still are written once — `SimWorld.BatchFor`, `WhyTheBatchWaits`, `StoreForTheBatch`, `StoreForTheOutput`,
`BehaviorSystem.BeginBatches` and `FinishABatch` (D522, built so the bakery would not be a second mill).

What is still per trade is **every switch that turns a trade into its states, its words and its hands** — and each
new batch trade has had to find them all by grep (`workshop-and-the-carpenter.md §4.5` lists the grep). Counted:

| Piece | Where | Per trade today |
|---|---|---|
| The recipe | `SimWorld.BatchFor` (:4382) | a `switch` arm; the miller and baker read config keys, the tailor a literal output of 1, the carpenter the goods row's `made_from` |
| The two states | `World/Season.cs` (:298–349) | `TravelingToMill`/`Grinding`, `TravelingToBakery`/`Baking`, `TravelingToTailor`/`Sewing`, `TravelingToWorkshop`/`Making` |
| State ⇄ trade | `BehaviorSystem` `BatchState` (:702), `BatchStatesOf` (:711), `BeginBatches` (:5976) | three switches, **each with the baker as the silent default** — a fifth trade forgotten there would bake |
| Predicates | `BehaviorSystem` `IsMilling`/`IsBaking`/`IsSewing`/`IsMaking` (:689), `IsOnAWorkErrand` (:740), four `ErrandKind` arms (:800–820), the walk case in `ActOne` (:520), `CompleteAction` (:7621) | an arm each |
| Words | `BehaviorSystem.Describe` (:131), `Villager.DescribeState` (:905) | *"grind of the day"*, *"batch of the day"*, *"of the day"*; `Making` says *"handcarts"* whatever the workshop makes |
| Skill | `SkillSystem.OutOnTheWork` (:277) | an arm each; the default throws |
| Hands | `LabourQuota` constructor (:50, :78), readers (:175), wants (:321, :338), takes (:643–662), `Asked` (:843), `AtLeastPinned` (:873); `LabourAllocator.KindsInOrder` (:1160) | ~14 lines each |
| How much is wanted | `LabourQuota` `MillersWanted`/`BakersWanted` (every seat with work), `TailorsWanted`/`CarpentersWanted` (shortfall over a year's work, capped by seats); `VillageEconomy` (:445–484) the tailor's and carpenter's per-year sums | a function each |
| Stops | `WhyTheBatchWaits` (:4447) | the miller waits while the village is hungry and while the ovens have flour enough; the tailor at `ClothingWanted`; the carpenter at `HandcartsWanted` + the spare; the baker for firewood |
| Who knows of it | `SimWorld.KnowsOf` (:4179), `AStandingShopIsShortOf` (:3761) | a list |
| The view | `VillageMap.People.cs` `AtWork` (:74), `GoingToWork` (:84) | a name each |

**Nine sim files and one view file, about sixty lines a trade, none of which says anything a row could not.** The
brewer (W3), the herbalist (W6) and the wool coat (W10) would each copy them again.

## 2. Pillars

- **Traceable over clever (§1):** one table of batch trades, read by every switch, is easier to follow than ten switches
  that must agree; the baker-as-default is exactly the silent wrong answer the pillar forbids.
- **Data-driven from day one (CLAUDE.md):** a batch trade becomes a row in the catalogue. ⚠️ **In the catalogue's
  sense, not yet the data file's** — like every content row today it is a `SimConfig` C# default with its numbers in
  `data/sim.config.json`; Phase 7's first step moves the rows themselves (D789). This spec does not pretend otherwise.

## 3. The row

`SimConfig.BatchRows` — one `BatchRow` per batch trade, in hiring order, built from today's config keys (no new key,
no number changes):

> **✅ Built (D793)** in `World/BatchTrade.cs`: `BatchRow`, the rule enums `BatchStop` (flags), `BatchHands`, `QuotaBand`,
> and `BatchCatalog`, which the world builds once as `SimWorld.Batches`, indexed by job id. `SimConfig.BatchTrades`
> (`batch_trades`) is a modder's whole list; **null means the built-in four priced from this config's keys** —
> `Buildings`' rule and for its reason: a row built once as a property default would keep the shipped numbers through
> every `Config with { … }` (`ABatchRowReadsTheConfigsOwnKeys`). The columns as built: `from_the_product`, `one_of`,
> `walk_to`, `working`, `stint_word`, `first_batch` (the table's *idle*); `{product}` / `{products}` are the output's name,
> one and many, put in by `SimWorld.Worded`. Validation at load: one row a trade, a job with a hut, ticks/per_stint/
> output of at least one, goods that exist, the words, and a shortfall-hired row's stop naming a shortfall
> (`ABadBatchRowIsRefused`).

| Column | Miller | Baker | Tailor | Carpenter |
|---|---|---|---|---|
| `trade` | Miller | Baker | Tailor | Carpenter |
| `input`, `input_per_batch` | wheat, `wheat_per_grind` | flour, `flour_per_bake` | leather, `leather_per_coat` | **from the product's `made_from`** |
| `output`, `output_per_batch` | flour, `flour_per_grind` | bread, `bread_per_bake` | clothing, 1 | **the workshop's `MakeGood`**, 1 |
| `firing` (firewood a stint) | 0 | `firewood_per_firing` | 0 | 0 |
| `ticks`, `per_stint` | `grind_ticks`, `grinds_per_stint` | `bake_ticks`, `bakes_per_stint` | `sew_ticks`, `sews_per_stint` | `make_ticks`, `makes_per_stint` |
| `verb`, `doing`, `noun` | grind, grinding, a grind | bake, baking, a bake | sew, sewing, a coat | make, making, *a {product}* |
| `stint_word` | grind | batch | — | — |
| `idle` (the card's work line) | at the stones | lighting the oven | cutting a hide for a coat | truing a wheel for *a {product}* |
| `stop` | **`FedFirst` + `OvensSupplied`** | none | **`EveryoneClothed`** | **`EveryHouseholdCarted`** |
| `hands` | `EverySeatWithWork` | `EverySeatWithWork` | `ShortfallOverAYear` | `ShortfallOverAYear` |
| `quota_band` | after stone | after stone | after tools | after tools |
| `stands_down_in_famine` | no | no | **yes** | no |

- **`stop` and `hands` name a rule, not a formula.** The four demand rules are genuinely different mechanics —
  bread feeds, flour feeds the ovens, a coat keeps a body warm, a cart serves a household — and each stays one named
  method in code (`ClothingWanted`, `HandcartsWanted`, `FlourWantedForTheOvens`, the hunger stop). A new trade picks an
  existing rule, or brings one new method and its name. **That is the honest line: content is a row, a new kind of
  demand is a mechanic** (the review's §3 cut, applied inside one system).
- **The carpenter's recipe stays on the product's row** (`made_from`, D446's shape, Joe's D675 call 3). The `BatchRow`
  says *"read the recipe from the product"*; the four other columns come from it as today, through
  `CarpentersBatch`'s first-standing-workshop rule unchanged.
- **The smith stays out.** His recipe is the tool's row and his card picks the tool; he was never a `Batch` (D446).
- **Validation at load:** every `BatchRow`'s trade has a `JobRow` and a building whose `Kind` it is; its goods exist;
  `ticks` and `per_stint` above nought; no two rows for one trade. The checks already at `SimConfig` :6355–6371 move
  onto the row.

## 4. What reads the row

Every switch in §1's table becomes a lookup: `BatchFor(trade)` finds the row and builds the `Batch` as today;
`BatchStatesOf`, `BatchState`, `BeginBatches`, the predicates, the `ErrandKind` arms, `Describe`, `DescribeState`,
`OutOnTheWork`, `KnowsOf` and `AStandingShopIsShortOf` iterate or index the rows. **An unknown trade throws** — the
baker-as-default goes (§1). The quota's four named takes become two loops over the rows of a band, **in row order,
at exactly the places the named lines stand today** (smith → *after tools* → quarrier, miner → *after stone* →
herdsman), so `Take(ref free)` is called in the same order with the same numbers. `KindsInOrder` is untouched — it
already lists the four where the bands put them, and a guard holds the two orders equal. `VillageEconomy`'s two
per-year sums become one, `BatchesPerYearAtWorst(row)`, after a test shows it returns the same integer for both.

**Nothing a player sees changes,** with one invisible exception: `Making` says the workshop's product from the row
rather than the word *"handcarts"* — the same word while handcarts are the only product.

## 5. Determinism — what must not move

Traced (the order-sensitive details, each with its guard in §6):

1. **The quota's take order** — §4's bands keep it; a test compares the old order with the new on every shipped golden
   valley at Year 30, and every golden is the final proof.
2. **First match wins** in `AStandingShopIsShortOf` and `CarpentersBatch` — both still walk `Workplaces` in list order.
3. **`FinishABatch`'s order of takes** (firing check, input, firewood, output store, set-down) — not touched.
4. **No RNG** on any batch path (traced); `BeginWork` unchanged.
5. **The state byte** — `StateHash` hashes `(byte)villager.State`; the four pairs are 41–44 and 53–56. **This is
   §7's call.**

## 6. Steps — each its own commit, the goldens grepped before and after

1. ✅ **(D792 — `BatchTradeTests`, 28 tests over 10 methods; 11 mutants, each red on the test aimed at it, none scoring
   zero.)** **Pin today first** (tests only, written before any change, each red-checked by a mutant): every trade's `Batch`
   field by field; its two states and their words (`Describe`, `DescribeState`, idle line); its `OutOnTheWork` skill;
   its stop sentences; its quota want on a posed village; the quota's take order. Many exist
   (`TheCarpenterJoinsEveryBatchTradeSwitch`, `FoodChainTests`, `TailorTests`, `WorkshopTests`) — the new ones fill
   the gaps, and **a guard that cannot fail is counted as zero and written down** (D326).
2. ✅ **(D793.)** **The row** — `BatchRow`, `SimConfig.BatchRows` and its validation; `BatchFor` reads it. Goldens
   unchanged (the suite on step 2 alone: 2101 / 1 / 5 in 16m15 — the one red the save guard naming `Batches`, fixed).
3. **The switches** — states, predicates, errand arms, words, skill, `KnowsOf`, `AStandingShopIsShortOf` read the
   rows; the baker default throws. Goldens unchanged. **⚠️ Under A (D792) the state switches are deleted by step 5, not
   rewritten here** — so the rest of this step (`KnowsOf`, the idle note, `SetMakeGood`; `AStandingShopIsShortOf`
   already asked `BatchFor`) landed with step 4 (D793), and the states with step 5.
4. ✅ **(D793.)** **Hands and stops** — the quota's bands, `BatchSeatsWanted` / the shortfall rule by the row's `hands`,
   `WhyTheBatchWaits` by the row's `stop`, `VillageEconomy`'s one sum. Goldens unchanged. `LabourQuota`'s four named
   batch parameters became one array by job id (`batch:`); `MillersWanted` … `CarpentersWanted` stay as named readers
   over `BatchHandsWanted` (`Main.cs` reads `CarpentersWanted`). **Red-check: 9 mutants; 4 red first time, 5 zero —
   one a no-op of mine (invalid), four real gaps:** a band moved or the famine rule flipped changes no number in a
   four-hand village; the mill's *"ovens have flour enough"* stop had **no test anywhere**; the year's divisor can't
   show while one tailor is wanted either way. Three tests added (`TheShippedBatchRowsRulesAreAsSpecced`,
   `EachBatchTradesOwnStopReadsAsShipped`, `AShortfallTradeIsHiredByTheYearsWorkUpToTheSeats`); **all 8 valid mutants
   red.**
5. ✅ **(D794.)** **The states** — per Joe's call (§7). The view's `AtWork` / `GoingToWork` read a sim helper rather than
   eight names. **As built:** `TravelingToABatch` (41) / `WorkingABatch` (42) — the miller's old numbers; 43–44 and 53–56
   retired, `CuttingHay = 45` and `FetchingAHandcart = 57` explicit so every later state keeps its number. The view names
   the two (no helper needed for two names). `Villager.BatchTrade` set by `SimWorld.SetBatchOf` with `BatchWords` (the row's
   words, product put in — derived, put back after the workplaces load); the recall reads it (`HoldsTheJobFor`); hashed
   sparse under tag 13 only while at a batch; saved as `batch_trade`, the eight old names read as the pair and the trade.
   `BeginBatches` throws on no trade or no batch — **the baker default is gone**. **⭐ No golden moved** — not the two
   §7 expected: the Phase 5 village is hashed once, at year 20, with nobody at a batch, so the new numbers never reach
   it; twenty posed years with a mill and an oven ran identically. The proof §7 promised (old numbers restored) had
   nothing to restore; `WhoseBatchIsInTheFingerprintOnlyAtABatch` reads the new hash directly instead. **Red-check: 8
   mutants, 8 red** (two re-run after the runner failed to restore them — HANDOFF trap).
6. ✅ **(D795 — `FifthBatchTradeTests`.)** The modded boathouse's boatman (`ModdedBuildingTests`, job 18, building 10)
   given a batch row — stone into tools — and a skill row, through config alone: the recipe from the row, wanted by the
   quota from the row, walked to, worked, delivered, described in the row's words (*"shaping stone tools, the 2nd tool of
   the day"*) and its skill grown. Red: the catalogue limited to the built-in ids fails it, and only it. **⚠️ What the
   enums do stop: hiring.** `LabourAllocator.KindsInOrder` is a list of the shipped `JobKind`s, so **the village never
   seats a modded trade, batch or not** (the boatman never was) — the test seats its hand, with the share-out switched
   off. And the quota does count a modded batch row's hands in its band, so in a modded game those hands are held for a
   trade nobody is seated at. **So W3's brewer, a shipped trade, needs in the batch path: a `JobKind` and a
   `BuildingKind` value, its rows (job, building, skill, batch), and its place in `KindsInOrder`** — nothing else. Making
   the allocator's order data is Phase 7's (with the rows moving into the data file), and every modded trade's.
   *(As specced:)* **The proof that a trade is now a row:** a test adds a fifth `BatchRow` (and its job and building rows) through
   config alone — no sim code — and watches a villager hired, walk, work, deliver, be described and grow its skill.
   ⚠️ **If the job or building catalogue cannot take a row past the built-ins** (they are C# enums underneath — Phase 7's
   work), that is written down here and W3's brewer becomes the proof, needing a `JobKind` and `BuildingKind` value
   and nothing else in the batch path.

**The four checks at the end** (CLAUDE.md), the suite's clock compared with D788's 14m48.

## 7. ⏸️ Joe's call — how a villager at a batch hut is stored

Today a miller at the mill is stored as *"Grinding"*, a baker as *"Baking"*, and so on — a pair of names per trade.
For a new trade to be only a row, it can't need two new names in the code.

- **A — one shared pair for every batch trade** (*walking to the hut* / *working at the hut*), with the trade's row
  supplying the words you read (*"grinding"*, *"baking"*). **What you'd notice: nothing.** Your existing saves still
  load — the four old names are read as the shared pair. The cost: the two Phase 5 golden numbers (the run that builds a
  mill and a bakery) change **once**, only because the stored state's number changes; I prove it by putting the old
  numbers back in a scratch build and getting the old goldens exactly, then re-pin.
- **B — the four keep their own names; new trades use the shared pair.** Nothing moves and saves load, but there are two
  ways of doing the same thing forever, and the next person has to know which trades are which.

**Recommended: A.** It is the version a modder can use, and the one golden move is proved, not waved through.

> **✅ ANSWERED: A (D792, 2026-11-17, Joe: *"A, as recommended"*). ✅ Built (D794) — and no golden moved after all
> (§6 step 5).** ⚠️ **One thing A needs that §7 did not say, found
> pinning step 1:** the shared state cannot say *whose* batch it is, and the D281 recall reads exactly that — the yearly
> share-out moves a miller to the bakery mid-stint, and today `ErrandKind` sends them home because *Grinding* is not a
> baker's errand. Read the trade off the seat instead and the miller walks on, and bakes standing at the mill
> (`ABatchErrandIsRecalledWhenTheSeatIsAnotherTrades` pins it). **So the trade is a field beside the state,
> `Villager.BatchTrade`, set when the walk begins** — saved by name and read tolerantly (an old save's *Grinding* reads as
> the shared state and the miller), and hashed **sparse, only while at a batch**, so a run with no batch hut hashes as
> before. Still the same two goldens, still once, still proved by restoring the old numbers.


### Found while tracing — not changed by W1

- **The tailor stands down in a famine, the carpenter doesn't** (`LabourQuota` :401 zeroes only `tailorsWanted`). W1
  keeps both as they are (`stands_down_in_famine` per row) because W1 changes nothing you see. Worth deciding with
  morale or health; not now.
- `Main.Cards.cs` :3413 repeats the carpenter's waiting sentence using `LogsPerHandcart` directly. Left alone — the art
  lane is editing that file. Raised for after its hook-in.
- **Each batch building's unlock** (`mill_unlock_wheat`, `tailor_unlock_leather`, `workshop_unlock_logs`) is still a
  `WhyNotYet` arm and a tech node per building. Out of W1: the brewery's unlock is W3's, and the tech nodes are already
  rows.

## 8. Art (D774's rule)

Nothing new to draw. W1 changes no building, good or state anybody sees.

## 9. Acceptance

- §6's steps 1–6 built, each commit with the goldens grepped before and after; **no golden moves, except — under §7's
  A — the two Phase 5 goldens, once, proved by restoring the old state numbers.**
- A new batch trade needs, in the batch path: **a row**, plus a demand rule only if its demand is new. The grep in
  `workshop-and-the-carpenter.md §4.5` returns nothing to edit.
- The four checks green; the suite's clock within reach of D788's.
- Joe plays a village with a mill, a bakery, a tailor and a workshop working (any of his saves) and sees nothing
  different.
