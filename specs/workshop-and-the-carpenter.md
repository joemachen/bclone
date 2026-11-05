# Spec: The Workshop, and the Carpenter who makes the handcart — Slice W

**Decisions:** D674 (this document), **D675 (Joe's five calls on §11, answered 2026-11-01), D676 (his number for the
unlock, 2,500, and the hash as a learned flag), D678 (built), D679 (traders can sell carts, his call), D682 (played; the unlock
lowered to 1,500 at his second call)**. It builds on D672 (Joe: *"Yes, all six"* — every call in `handcart.md §10`),
D670 (the handcart thought through; `handcart.md §5` redefines this slice), D553 call 6 (the slice as first owed:
*the builder's hut growing into a workshop with a carpenter trade and the builder's hammer* — redefined here, see §8),
D617 (every hands-on adult takes a tool once a smithy stands — why there is no hammer), D391 / D446 (the smithy and the
smith, the shape this copies: a building, two seats, a recipe, a dropdown on the card, a limit that stops him),
D522 / D659 (the `Batch` shape: mill, oven, tailor's shop — the third of its kind becomes the fourth), D139 (a met
limit stops the work), D109 (the player staffs), D62 (the player's ceiling over a derived floor), D382 (footprints
per building type), D392 (a skill row is safe to add), D291 (the hash is sparse over goods), D510 (an older save is
refused, never half-loaded), D660 (a typed starting limit is hashed and moves every golden).
**Status:** ✅ **BUILT (D678, 2026-11-01) ON `claude/slice-workshop-2083f6` — SIM, DATA, TESTS, VIEW AND DOCS IN ONE COMMIT; ✅ PLAYED BY JOE (2026-11-04, Hollowmere: the workshop lit at 2,511 logs in Summer,
Year 34, a carpenter made carts, every household carted plus a spare, the idle sentence right — *"everything else works properly
for the workshop"*); THE UNLOCK LOWERED TO 1,500 AT HIS SECOND CALL (D682: *"it took me too long for the workshop. bring it down
to 1500"*), WHICH THE FOUR FIFTY-YEAR GOLDENS CROSS — EACH MOVED ONCE, PROVED AND RE-PINNED (§4.1b); MERGED INTO `main` (D684, 2026-11-04).** `tests/Bclone.Sim.Tests/WorkshopTests.cs` holds §9's guards (20 facts), each red-checked and counted (§9 *as built*);
§10's table was filled on the harness **before** a number was typed into `data/sim.config.json`, and every proposal stood
(a workshop at Year 5 costs the roofs nothing a seed can see); the four checks are green, bar 151, `done.`; **392 goldens, every
value unchanged but four — the farm-and-clear pair, which fells 3,180 logs in twenty years and learns the
workshop (§4.1b), and the Phase 5 pair, for the overland neighbour's handcarts (§5; a hashed stock slot, D661's
precedent) — each proved by switching its one cause off and re-pinned with it**; save format **10**. His answers (§11) stand as built: the workshop **learned by felling wood** — `workshop_unlock_logs`
**1,500** (2,500 at D676, lowered after play at D682), his number, the quarry's shape, hashed as a learned flag (§4.1a); **no shipped `handcarts` limit**; **`MakeGood`
now**; the carpenter **after Tailor**. Owner: Joe + Claude Code. **Next: Joe plays on (`.\run.bat`), merges at his word; then
Slice H (`handcart.md`).**

> **Where this sits.** Logistics was measured (D668), Joe chose to change the game (D669) and picked two mechanics
> (D670): *nearby* given a meaning, built as `market_detour_ticks` 4 (D671), and the handcart, thought through in
> `specs/handcart.md` and settled when he took all six of its calls as proposed (D672). The cart is made by a
> carpenter at a workshop, so **Slice W — this spec — comes before Slice H, the cart** (`handcart.md §10`). ⛔ **Carts
> being fetched, parked, pushed, drawn, worn and wearing paths are Slice H and live in `handcart.md` §3, §4, §6–§9**;
> this document stops at a handcart sitting on a warehouse shelf.

---

## 1. Goal

A player can raise a **Workshop**, staff it with a **Carpenter**, and watch logs become **handcarts** that go to the
warehouse and wait there — the way a smithy makes tools and a tailor's shop makes coats. He stops when the village
has what it wants: a set `handcarts` limit, or, with none set, one cart a household. The slice is complete when he
works, stops, says why he is idle, and nothing else in the game has changed: the suite, the probe and every golden as
they were.

**What it is not** — each named so it is not quietly built here:

- Not the hut growing (D553's sentence): the builder's hut stays as it is, §8.
- Not a hammer: D617 gave the builder a tool; a hammer would be a second tool for the same hands (`handcart.md §5`).
- Not a knowledge node on the cart: the cart's gate is the building (Joe, D672 call 3); *Heavy Haulage* stays the
  draught-animal node (`tech-tree.md §9.3`, *Cartwright Warehouse*; `TECH-EXAMPLE.md:206`). **The workshop itself is
  learned by doing** — by felling wood (Joe, D675 call 1; §4.1a) — which is §2.7's second mechanism and the quarry's,
  mill's and tailor's shape, not a node a person carries.
- Not the cart doing anything. A handcart in Slice W is a good on a shelf with a name, a colour and a row.
- Not planks or furniture. The *Makes:* dropdown is built for them (§4.3), their rows are not.

## 2. Which pillars this serves

- **§2.2 — the processing tier, the D29 shape again.** An input consumed to make an output, a workplace that can be
  idle for want of stock rather than for want of a worker, built once for firewood and proved for tools, flour, bread
  and coats. The workshop is the fifth conversion workplace and should cost nothing new in machinery.
- **§1.1 — legible in every sentence.** The card says what the workshop makes and why it is not making it; the
  professions panel says when the village wants a carpenter and how many carts it is short; the warehouse card lists
  the carts beside the tools.
- **§1.2 — meditative, not babysat.** The derived stop (one a household) means a player who never opens the stock
  limits panel still gets a carted village and never a warehouse of carts. The limit is a ceiling he may set, not a
  chore he must.
- **§1.6 — traceable over clever.** Three enum values appended, one `Batch` arm, one quota arm, one card row. No new
  kind of thing.
- **§0.1 — cozy.** A workshop is logs and a little stone; a village that never builds one plays byte for byte as today.

## 3. What already exists to copy — traced, not remembered

| Thing | Where | What the workshop takes from it |
|---|---|---|
| The smithy | `BuildingKind.Smithy` 15, `JobKind.Smith` 8; 2×1; `smithy_logs` 25, `smithy_stone` **36**, 40 work ticks, 2 seats (`SimConfig.BuildingRows`) | footprint, seats, cost range, the card's *Forges:* dropdown and its probe check (`Main.Cards.cs` `FillForge`, the `cards:` line's dropdown budget) |
| The tailor's shop | `BuildingKind.TailorShop` 26, `JobKind.Tailor` 15; a hut's price (`hut_logs`, `hut_stone`), 2 seats | the newest building: every switch it joined is the list the carpenter joins (§4.5) |
| The `Batch` | `SimWorld.BatchFor(JobKind)`: input, amount, output, amount, firing, ticks, per stint, verb, noun; `WhyTheBatchWaits` the one copy of every idle reason | the carpenter's make is a batch: logs → a handcart, no firing |
| The derived stop | `WhyTheBatchWaits`: the tailor with no limit set stops at `LabourQuota.ClothingWanted` (*every adult has a coat, and the shelves hold a year's wear*) | the carpenter with no limit set stops at `HandcartsWanted` (§4.4) |
| Demand → hands | `ClothingShortfall` / `TailorsWanted` (`ToolShortfall` / `SmithsWanted`'s shape), `VillageEconomy.CoatsSewnPerYearAtWorst`, the allocator's `KindsInOrder` | `HandcartsShortfall` / `CarpentersWanted` / `HandcartsMadePerYearAtWorst` |
| A tool's row | `GoodsRow`: `StoredBy`, `TradeWorth`, `ForgedFrom` (the smith's recipe as a column, D446) | the handcart's row: `stored_by` the tools' three stores; its workshop recipe as a column (§4.3) |
| Where tools live | `StoredBy = Warehouse, Cart, Pile` on both tool rows and the clothing row; *the market does not stock tools* (`tools-and-the-smith.md §3.5`) | the handcart's `stored_by`, to the letter (Joe, D672 call 1) |
| The tool in the trade's hands | `JobRow.UsesTool`; wear in `SimWorld.BeginWork`, one use per action begun (`tools-and-the-smith.md §3.3`) | the carpenter's row says `uses_tool`; a make begun wears a use |
| A skill row | `SkillRow` 9 *tailoring*; safe to add since `founding_trades` is a stated list (D392) | row 10, *carpentry* |
| A learned-by-doing unlock | `quarry_unlock_stone` 200 / `smithy_unlock_iron` 50 / `mill_unlock_wheat` 5000 / `tailor_unlock_leather` 50 / `bridge_unlock_population` 10 / `stone_bridge_unlock_crossings` 150; a `WhyNotYet` arm, a `TechNodeRow` with a `TechCondition`, `TechTree.ProgressOf`, `TechTreeView.HowFar`'s sentence, `LearnedByDoing` (the log line and the map's first showing), the bar's `RefreshALearnedButton` | `workshop_unlock_logs` against `SimWorld.LogsEverFelled` (§4.1a) |
| The wood counter | `SimWorld.LogsEverFelled`, counted at the stump (D382), saved as `logs_ever_felled`, **not hashed** — *"statistics, not state: nothing in the sim reads them"*; `StoneEverDug` beside it says what must change the day something does | sets a learned flag where it moves; the flag is the state and is hashed (§4.1a, §7) |
| The view | the smithy's button under *Works* (`BuildCategory.Works`), `BuildingGlyph` / `TradeGlyph` colours under the three-band rule (D637, `tools/legibility/check.py`), the idle sentence (`Main.cs` ~3769), the probe (`BCLONE_PROBE_WIDTHS`) | §6 |
| Save and load | `SaveGame.Format` 9; `SaveLoadTests.Fields` names every field or fails (`EveryFieldIsSavedOrNamed`); a missing key throws (D510) | §7 |

⚠️ **Found while tracing: `tools-and-the-smith.md §3.6` and its §4 table say `smithy_stone` 12; the shipped config says 36**
(`data/sim.config.json`, *"a hut's timber and a forge's worth of stone"*; `bridges.md §4`'s table already reads 36).
Not changed here — a stale number in another spec is a follow-up, named so it is not copied. This spec prices from the
config.

## 4. The rules

### 4.1 The Workshop — a new building in the smithy's shape

`BuildingKind.Workshop` — **the next free id, 27** (26 is the tailor's shop; ids are hashed by position and never
renumbered). A row in `SimConfig.BuildingRows` beside the tailor's shop, priced from its own keys:

| Key | Proposed | Why — until measured (§10) |
|---|---|---|
| `workshop_logs` | **25** | a hut's timber, as the smithy's and the bakery's — a bench and a saw-pit, not a forge |
| `workshop_stone` | **12** | the floor and the hearth-stone of a hut (`hut_stone`'s range) — **less than the smithy's 36**, because there is no forge; the smithy's spec once priced its forge at 12 too (§3's finding) |
| `workshop_work_ticks` | **40** | as every hut |
| `workshop_capacity` | **2** | two seats, as the smithy's and the shop's; `TECH-EXAMPLE.md`'s *Cartwright Shed* seats two wheelwrights |
| footprint | **2×1** | D382's hut row: *woodcutter's, builder's, hunter's 2×1*; the smithy and the tailor's shop are 2×1 |

**Placement:** the hut's rules and no more — on buildable ground, by a lane if there is one, a door on its front
(`doors.md`), no water or woods requirement (the logs come from a store, not a stand). It wears, fails and falls as
every timber building does (`building-condition.md`: `wears_in_years` from the shipped three). It is **not** a store
and holds nothing; a finished cart goes to the nearest store that takes handcarts (§4.3), and what will not fit goes on
the ground beside the workshop (D96, D134) — the forge's rule.

### 4.1a The unlock — learned by felling wood (Joe, D675 call 1)

~~Proposed: on the build bar from the founding.~~ **Overruled.** Joe: *"does anything currently trigger on a certain
amount of trees/wood/lumber harvested? If not, perhaps the workshop can appear on that — but I'd want it to be a larger
number than anything else we currently have in-game."* Nothing gates on wood today. So **the Workshop is learned by
doing, in the quarry's, mill's and tailor's shape, on logs felled:**

- **The knob: `workshop_unlock_logs`**, read against **`SimWorld.LogsEverFelled`** — the counter that already exists,
  counted at the stump (D382: *what actually came off the tile*, a laborer's clearing included), saved as
  `logs_ever_felled`. Validated at load like `quarry_unlock_stone` (at least zero).
- **✅ The number, lowered: `workshop_unlock_logs` 1,500 (D682, 2026-11-04).** Joe, after playing the build (Hollowmere: the
  workshop lit at 2,511 logs in Summer, Year 34 — *"everything else works properly for the workshop"*): *"it took me too
  long for the workshop. bring it down to 1500."* His Hollowmere felled ~74 logs a year, so **1,500 lands around Year 20**;
  it is still larger than every unlock in the game but the mill's. ⚠️ **The unattended valley fells 30–45 a year and now
  crosses it between Year 40 and 50, inside the fifty-year goldens — all four moved once, each proved (§4.1b).** The knob's
  comment in `data/sim.config.json` carries both calls and the warning. *The D676 reasoning, as it was:*
- **The number: `workshop_unlock_logs` ~~2,500~~ — ✅ Joe's (D676, 2026-11-01: *"2500 logs"*).** The reasoning he was given
  and took: his played villages fell roughly **70–100 logs a year** and stand at **2,600–4,500 logs felled after 25–50
  years** (one at 6,618), so **2,500 lands around Year 25–35** — after the mill in a farming village, sooner in one that
  cuts hard — while a number above `mill_unlock_wheat` 5,000, the largest in the game today, would arrive after Year 50
  in nearly every village he has played. It is larger than every unlock but the mill's by an order of magnitude, and
  inside a played village's lifetime, which a logistics fix has to be (D668's problem is in *every* established village).
  §10's table takes the unlock year on the harness. The knob's comment, in `data/sim.config.json`'s voice:

  ```jsonc
  // THE WORKSHOP IS LEARNED BY FELLING WOOD (workshop-and-the-carpenter.md 4.1a,
  // Joe D675: "a larger number than anything else we currently have in-game";
  // D676: "2500 logs"; D682, his second call after playing it, 2026-11-04: "it
  // took me too long for the workshop. bring it down to 1500"). Read against
  // SimWorld.LogsEverFelled, counted at the stump (D382) -- a laborer's clearing
  // counts, a split does not. His Hollowmere felled ~74 a year and lit the workshop
  // at 2,511 in Year 34; at 1,500 it lands around Year 20. Still larger than every
  // unlock in the game but the mill's (5,000). WARNING: the unattended valley fells
  // 30-45 a year, so the fifty-year goldens now cross this around Year 45-48 and
  // learn the workshop inside the run (the learned flag is hashed; 4.1b has the
  // table). The quarry's and the tailor's shape: a stop, paid for, no gift.
  "workshop_unlock_logs": 1500,
  ```
- **The arm:** `WhyNotYet(Workshop)` while **`!WorkshopLearned`** — *"Nobody here builds carts yet — the village has
  felled {n} of the {N} logs it takes to learn."* (the sentence reads the counter and the knob; the gate reads the flag,
  below). `Mark` refuses in that sentence; the bar hides the button until then and lights it like the quarry's
  (`RefreshALearnedButton`, paid for, lit until the first is marked); the crossing — checked where the counter moves, in
  the fell path (`if (!WorkshopLearned && LogsEverFelled >= Config.WorkshopUnlockLogs)`), never per tick — sets the flag
  and goes through `LearnedByDoing` (*"The village has felled {N} logs, and somebody has worked out how to true
  a wheel and hang a bed on it. A workshop can be built."*), which stops the village the first time and shows the map
  (D440). `TechCondition.LogsFelled`, a `TechNodeRow` *"workshop"* (*Fell enough timber, and somebody works out the
  wheelwright's trade — a workshop that makes a household's handcart.*), `ProgressOf` returning the counter and the knob,
  `IntroducesTheMap` true (hands-on), and `TechTreeView.HowFar`'s sentence *"{n} of {N} logs felled"*. The tree's card
  stays one sentence (160 tall, the `tech tree:` probe line holds it).
- **Fixtures that raise a workshop directly (`RaiseFreeBuilding`, the test helpers) are not gated**, as the smithy's
  are not: the gate is on the player's `Mark`.

⛔ **What the gate reads becomes state, and so must be hashed.** `LogsEverFelled`'s own remark says *"statistics, not
state: nothing in the sim reads them, so they are not hashed"*, and `StoneEverDug`'s beside it says why that flips the
day something does: *two villages that differ here differ in what the player may build*. Three choices were weighed, and
**the decision is (2), written down with its gap — D676:**

1. ~~**Hash the counter, densely**~~ (a tag and the value, every village). Strict and complete — two villages that differ
   in logs felled differ in *when* the workshop comes, below the gate as well as above it. **Cost: every golden moves
   once**, because every village fells wood from day one. *The first draft of this section recommended it (D675);
   overruled for (2) by the session that put the calls to Joe, and the reasoning below is why that is right rather than
   merely cheaper.*
2. ✅ **Hash a learned flag only — `SimWorld.WorkshopLearned`, set at the crossing, a tag byte when true**
   (`ShownTheTechTree`'s and `AFreeSmithyIsOwed`'s shape). **The gate reads the flag, not the counter**, so the thing the
   sim reads *is* what is hashed: two villages that differ in what the player may build differ in hash, which is exactly
   the `StoneEverDug` remark's sentence. `LogsEverFelled` stays a statistic — its remark gains one line: *the workshop's
   flag is set from it where it moves, and the flag is the state*. **No golden moves** unless its village crosses the
   number — none did at 2,500; at 1,500 (D682) the four fifty-year arms do, and moved once, proved (§4.1b). **The gap, stated:** two villages at 100 and 2,400 logs felled hash alike and will
   diverge later — the counter's path to the gate is not in the fingerprint. It is a *future* difference, not a present
   one; everything felling changes in the present (the tile, the store, the villager's load and state) is hashed, so a
   nondeterminism that moved only this counter and nothing else is not a case the fingerprint has ever been asked to
   catch; and the counter is saved (`logs_ever_felled`), so the round trip keeps it. Written where the flag is hashed, so
   the next reader of `StateHash.cs` is not surprised by the counter beside it.
3. ~~**Hash the counter sparsely once the gate is reached.**~~ The same gap as (2) below the gate, and no cheaper above it.

**A modded knob and a loaded village:** the flag is checked and set *in the fell path*, so a village loaded with a lower
knob unlocks at its next fell, not at load — the quarry reads its counter live and would unlock at once; the difference is
small and is said here. **The goldens that would cross the number would change behaviour, not only hash** — the crossing
fires `LearnedByDoing` (a stop, a log line, `ShownTheTechTree` flipping) — and §4.1b says which do: none at 2,500, the fifty-year runs at 1,500 (D682).

### 4.1b Which golden runs cross the number — measured, not guessed

A throwaway probe (deleted; the run is here, as the D353 rule asks) stepped each golden's own village and read
`LogsEverFelled` on the way, 2026-11-01, on `main`'s code (421a372):

| Golden run | Year 10 | Year 20 | Year 25 | Year 30 | Year 40 | Year 50 | Crosses 2,500? | Crosses 1,500 (D682)? |
|---|---|---|---|---|---|---|---|---|
| `SkillTests` fixture, 50 years (`VillageFixtures.Village`) | 165 | 426 | 636 | 801 | 1,251 | **1,701** | no | **yes, ~Year 45** — moved (`SkillTests` fixture arm and `StockLimitTests.FixtureFiftyYearHash`, the same village), proved |
| `SkillTests` shipped, 50 years (`ShippedConfig.Established()`) | 555 | 777 | 957 | 1,122 | 1,392 | **1,770** | no | **yes, ~Year 43** — moved, proved |
| `StockLimitTests.ShippedFiftyYearHash` (no limits set) | 150 | 402 | 567 | 819 | 1,194 | **1,602** | no | **yes, ~Year 47** — moved, proved |
| `PhaseFiveGoldenTests`' pose, 20 years | 450 | **1,050** | — | — | — | — | no | no (1,050 at twenty years) |
| `FarmGoldenTests`' scenario, 20 years (crops × the harvest brush) | not probed — its runner is private; the pose above clears for a farm too and reads 1,050 at 20 | | | | | | ~~not expected~~ **✅ YES — 3,180 at 20 (D678, read in the build)**: the brush clears a whole wood, three times the pose's rate; the village learns the workshop in it, and both of its goldens moved and were re-pinned with the proof (the crossing switched off, the old values pass) | **yes** — 3,180 at twenty years; crossed at 2,500 already (D678) |

- **At 2,500 (D676): no golden run crosses it, or 2,000; the nearest is 1,770 at fifty years.** An unattended village fells 30–45 logs a
  year; Joe's fell 70–100. So at Joe's 2,500 **no golden changes behaviour** (no `LearnedByDoing` fires inside one) **and,
  with the flag hashed rather than the counter (§4.1a choice 2), no golden moves at all.** The fifty-year unattended
  goldens are **not expected to cross**: at 30–45 a year they would reach 2,500 around Year 55–80, and they stop at 50. Below
  ~1,600 they would start to cross, and the number is not going there. ⚠️ A golden that *does* move in the build is a
  finding, not a re-pin.
- **At 1,500 (D682, 2026-11-04) — the number went there, at Joe's second call, and the fifty-year runs cross it.** Three
  villages, four golden arms: the fixture village (`SkillTests`' fixture arm and `StockLimitTests.FixtureFiftyYearHash` run
  the same `VillageFixtures.Village`) at 1,251 by Year 40 and 1,701 by Year 50, so around Year 45; `SkillTests`' shipped arm
  (1,392 → 1,770) around Year 43; `StockLimitTests.ShippedFiftyYearHash` (1,194 → 1,602) around Year 47. Each learns the
  workshop inside its run — `WorkshopLearned` hashed, `LearnedByDoing`'s stop — and **each moved exactly once:** `SkillTests`
  3828778980944462616 → 16912250691129698349 (fixture) and 14563583951307060232 → 2910132351223792017 (shipped);
  `StockLimitTests` 17390278967619327199 → 2460416229105199918 (fixture) and 12295009579609975522 → 6661378206269367323
  (shipped). ⭐ **Proved:** with the crossing alone switched off (`&& Tick == ulong.MaxValue` on the one `if` in
  `SimWorld.Felled`), all four old values pass on the new knob; the file restored and hash-checked. The twenty-year runs do
  not cross (the Phase 5 pose reads 1,050 at twenty; the farm scenario crossed already at 2,500); the Year-40 columns
  (1,251 / 1,392 / 1,194) are all under 1,500, so the crossing sits between Year 40 and 50 in every fifty-year run.
  `WorkshopTests` keeps reading the knob through its non-default 7, and asserts the shipped 1,500.
- ⚠️ **What the table says about the number:** an unattended valley at 30–45 logs a year reaches 2,500 at year 55–80 and
  5,000 never; Joe's played villages at 70–100 reach 2,500 at year 25–35 and 5,000 at 50–70. The workshop at 2,500 is a
  mid-game building for a played village and a never for the harness — which is the smithy's and the tailor's lot too
  (`tools-and-the-smith.md §9.4`: *the shipped cold starts never staff the smithy*).
- The probe was four facts over three villages, about ten seconds each; it compiled against `IDE0005` on the first try
  (an unnecessary `using` fails the build — D246), which is worth knowing before the next throwaway.

### 4.2 The Carpenter — a new trade

`JobKind.Carpenter` — **the next free id, 16.** A `JobRow` beside the tailor's:

| Column | Value | Note |
|---|---|---|
| `name` / `plural` / `doing` | *carpenter* / *carpenters* / *making handcarts* | the plural is the professions panel's row |
| `works_at` | `BuildingKind.Workshop` | |
| `limited_by` | `Goods.Handcart` | D139: a met limit stops the work, not only the hiring |
| `uses_tool` | **true** | hands-on work; wears one use per make begun through `BeginWork`, as every trade's does (`tools-and-the-smith.md §3.3`) — the tool in his hands quickens the make by its row's percent, as a sew is quickened |
| `tool_waits_for_the_smithy` | false | a *trade*, so the founders' tools are his as they are the tailor's (D617: only the builder, the herdsman and the laborers wait) |

A `SkillRow` 10, *carpentry*, grown by the carpenter, *"as a carpenter"*, with a mastery line in the tailor's voice
(*"{0} has made the village's carts for {1} years. A wheel is true before it is fitted."* — words to be read against the
others when typed). Safe to add: the founders' trades come from `founding_trades`, a stated list (D392), so a tenth
row moves no founding. The carpenter's seat is player-staffed like every trade (D109); the derived number is the
panel's advice.

**States:** `VillagerState.TravelingToWorkshop` and `VillagerState.Making`, appended after `Sewing`; `DescribeState`
says *"walking to the workshop"* and *"making handcarts, the 2nd of the day"* (`BatchesThisStint`, the shared counter —
no new field on the villager). `BehaviorSystem`'s state → trade switch gains `Making => JobKind.Carpenter`.

### 4.3 The make — a `Batch`, and the recipe as a column on the handcart's row

`BatchFor(JobKind.Carpenter)`: **`logs_per_handcart` logs → 1 handcart**, `Firing: 0`, `make_ticks` a cart,
`makes_per_stint` a stint, verb *make*, noun *a handcart*. The logs come from the nearest store holding a cart's worth
(`StoreForTheBatch`); the cart goes to the nearest store that takes handcarts; the refusal names the store and the
good, as the mill's does. ⛔ **Never a firing, so the forge's winter-firewood guard is never asked** — a saw-pit has no
fire. ⚠️ A cart takes *logs*, which a small village spends on roofs and a cold one on firewood: `logs_per_handcart`
competes with `LabourQuota`'s timber for the woodcutters, and §10 measures that before the number is typed.

**The recipe lives on the good's row, not in a config switch** — the smith's shape (D446: `forged_from` is a column on
the tool's row, and the dropdown lists every row that has one). The handcart's `GoodsRow` gains **`made_from`**: a list
of `{goods, amount}`, built in `DefaultGoods()` from `logs_per_handcart`, empty on every other row. **A good with a
non-empty `made_from` is a workshop product.** Planks (logs → planks) and furniture (planks → furniture) later are rows
with a `made_from` and nothing else new; house tiers (D206) will want them. The `Batch` has one input and one output,
and every workshop product so far has one of each; a two-input product (a wheel with an iron rim) is the day the
`Batch` grows a second input, which is not this slice.

**The *Makes:* dropdown — one item today.** The workshop's card carries a *Makes:* row in the smithy's *Forges:* shape
(`FillForge`'s twin, `FillMake`): every good whose row says `made_from`, in row order, the one the workshop is set to
selected. **Today it offers one item, *handcarts*, and that is the point of building it now rather than with planks:**
the card says in one word what the building is for, the probe's budget check (`cards:`, trap 139 — the longest item must
fit the card) runs from day one, and the second product arrives as a row and nothing else.

~~⭐ Proposed: no `Workplace.MakeGood` field until there is a second product.~~ **Overruled — the field now (Joe, D675
call 3: *"I don't mind starting a new game — that's actually my favourite part"*).** `Workplace.MakeGood`, default
`Goods.Handcart`, is `ForgeGood`'s twin: set through `SimWorld.SetMakeGood`, which refuses a good with no `made_from` in a
sentence; **hashed sparsely** under a tag only when it is not the default, so a workshop left on handcarts mixes nothing
(`StateHash.cs:394`'s rule for `ForgeGood`); **saved as `make_good`** beside `forge_good` on every workplace (a
`Workplace` is one shape, so the key is written for every kind, as `forge_good` is); entered in `SaveLoadTests.Fields`.
**`SaveGame.Format` 9 → 10.** Every format-9 save — all of Joe's — is refused in words (D510: *"Saved by build …, whose
saves this build cannot read"*), and he has accepted that. The dropdown's setter works from day one, with one item to
choose. `save-load.md`'s format history records the bump and the reason.

### 4.4 Stopping: the limit, and the derived stop under it (D62)

`Goods.Handcart` — **the next free id, 15** — gets its stock-limits row for free (`StockLimits.Kinds` enumerates the
goods). Its row's name is **"handcarts"**, so the key is `"handcarts"`, as *"stone tools"* and *"clothing"* are. A set
limit is the ceiling, read first in `WhyTheBatchWaits` (*"— the handcarts limit is met (N stored)"*), the way every
batch trade's is.

**With no limit set, the carpenter makes toward what the village wants — one cart a household:**

- `LabourQuota.HandcartsWanted(world)` = **the households with a home, less the households that own a cart.** In
  Slice W no household owns one (the household's cart is Slice H, `handcart.md §3`), so it is the count of housed
  households; when H lands, the owned carts come off it and the shelves hold the spares. ⚠️ *Households*, not adults
  and not homes: a cart serves the larder, which is the household's (Joe, call 1); an empty house wants none.
- `HandcartsShortfall` = `HandcartsWanted` **plus the spare** − carts on the shelves (every store's count), floored at 0 —
  `ClothingShortfall`'s shape. ⚠️ *As built (D678):* the spare is in the shortfall as well as in the stop, read from one
  constant (`LabourQuota.HandcartSpare`), so the hiring and the bench agree to the cart — with the spare in the stop alone
  the quota would want nobody while the card still said there was a cart to make.
- `WhyTheBatchWaits` for the carpenter with `StockLimits.For(Goods.Handcart) is null`: *"Nothing to make — every
  household has a handcart, and the shelves hold one for the next (N stored)."* ⚠️ The spare: proposed **one**, so a
  new household fetches without waiting a stint — the tailor's *year's wear* has no analogue because carts wear by the
  trip (Slice H), and until H nothing wears. Measured in H, typed then; in W the derived stop is `HandcartsWanted + 1`.
- No `starting_stock_limits` row for handcarts — **clothing's precedent (D660): a typed starting limit is hashed and
  would move every shipped golden** (D446's one golden moved for `"iron tools": 200` alone). ✅ **Joe, D675 call 2:
  none.** The panel still shows the row, unset; his number, if he ever wants one, is his to set on the panel. The config
  comment, in its own voice:

  ```jsonc
  // HANDCARTS (workshop-and-the-carpenter.md 4.4, D674; Joe D675: none): no
  // starting limit, as clothing (D660) -- a set limit is hashed and moves every
  // golden. With none set the carpenter makes one a household and one spare,
  // and stops. The panel's row is the ceiling if Joe wants one; the demand is
  // one a household.
  ```

**Demand to hands:** `CarpentersWanted = ceil(HandcartsShortfall ÷ VillageEconomy.HandcartsMadePerYearAtWorst)`
(`CoatsSewnPerYearAtWorst`'s shape: stints a year at the worst walk × `makes_per_stint`). A `carpenters` slot in the
quota's `Asked` / `AtLeastPinned`; **`KindsInOrder` takes Carpenter after Tailor and before Quarrier** (✅ Joe, D675
call 4) — a cart is a convenience of the coat's kind, and the demand is finite (one a household, then spares), so he is
wanted in a burst when a workshop first stands and rarely after. The handcarts chip goes amber when `CarpentersWanted > 0`, as the
tools and clothing chips do; the panel's sentence is *"the village wants a carpenter"*. **The stop is read where the
work is decided and when the quota runs, never per tick** (CLAUDE.md's rule): `HandcartsWanted` is a count over the
households list, the same cost as `ClothingWanted`'s over the villagers.

### 4.5 Every switch the tailor joined, the carpenter joins

The tailor's slice recorded *"every Miller/Baker switch gains Tailor"* (`clothing.md §8` item 3). The grep to run on
the day: `JobKind.Tailor` in `src/Bclone.Sim` — `SimConfig.cs` (the rows), `SimWorld.cs` (`BatchFor`, the batch
trades' `WorkTicksFor` arm at ~2445, the standing-shop list at ~3989), `BehaviorSystem.cs` (state → trade, ~5392),
`LabourAllocator.cs` (`KindsInOrder`), `LabourQuota.cs` (`Asked`, `AtLeastPinned`, the stood-down-by-limit switch at
~963). Each gains Carpenter; a guard holds the list (§9).

### 4.6 The survival floor does not move

`VillageEconomy.StockFloor(Goods.Handcart)` is **0** with the tools' reason: a cart is upside above the floor (its
payoff is Slice H's), so there is no floor to derive; the quota (§4.4) is what asks for them. Nothing in
`VillageEconomy`'s derivations reads a cart.

## 5. The good — `Goods.Handcart`

| Column | Proposed | Note |
|---|---|---|
| id | 15 | appended; `ModdedGoodTests`' fixture bumps its modded id by one (trap D348) |
| `name` | *handcarts* | the panel's row and the limit's key; the card says *a handcart* |
| `stored_by` | **Warehouse, Cart, Pile** | the tools' three stores, to the letter; **never the Market, never a Granary** (Joe, D672 call 1; `tools-and-the-smith.md §3.5`) — a cart is fetched once in years, not once a half-larder |
| `trade_worth` | **20** | sized from the logs it takes, as clothing's 8 was from leather (a log's worth × `logs_per_handcart`, rounded to the iron tool's 20); ~~no neighbour stocks one~~ **as built (D678): the overland neighbour makes 10** — Joe's standing rule (D587 call 5, guarded by `TradeRowsTests`) is that the three make every good the game has; by cart only (tier 2), so no boat brings one, and the caravan that would is what Slice H unblocks (`trade.md §4.7`). **Joe's decision, 2026-11-01, in his words: *"sure traders can sell carts"* — the suite asked, he approved (D679)** |
| `made_from` | `[{logs, logs_per_handcart}]` | §4.3; the one row with a workshop recipe |
| `source_name` | *the workshop* | the card's *"made at …"* |
| a cart's own columns | **none in W** | `cart_trips`, `cart_armfuls`, its slow on grass — Slice H's, on this row then (`handcart.md §6`) |

The founders' cart (`StoreKind.Cart`) is a *store*; a handcart is a *good*. **The good is named so the two cannot be
confused in code** (`Goods.Handcart`, never `Goods.Cart`), and the card calls it *a handcart* throughout.

## 6. The view, and what it says

- **Build bar:** a *Workshop* button under **Works**, beside the smithy (`BuildCategory.Works`; the tailor's shop went
  to *Resources* at Joe's word, D665, because it is a shop — a workshop makes things, so it sits with the smithy). **A
  learned button** (§4.1a): `_workshopKnown` / `_workshopTried` and a `RefreshALearnedButton` line beside the tailor's;
  the `IsKnown` switch gains `BuildingKind.Workshop => _workshopKnown` — ⛔ without that arm `_ => true` lights the tab at
  the founding for a building nobody may place, which the probe's `filters:` line catches (`Main.cs:7809`'s own warning).
  ⚠️ **The bar must stay 151.**
  Works holds a row of buttons at 1280 logical pixels; a tenth button may wrap the row, and the probe is what says so.
  If it wraps, the row is solved (a shorter label, the category's layout) — **never by raising the number.**
- **Glyph and colour:** `BuildingGlyph` case `Workshop` → `TradeGlyph.ColourOf(JobKind.Carpenter)`; a saw or a wheel for
  the trade's glyph; the colour a **warm wood-brown distinct from logs' colour and the smith's grey** under the
  three-band rule (D637), checked by `tools/legibility/check.py` before the commit (0 of its marks may fail). The good's
  colour in `GoodsPalette` for `Goods.Handcart` is **the cart colour `handcart.md §8` asks for** — the one the pushed
  cart, the parked chip and the founders' cart will all draw in from Slice H — so it is chosen here once, against the
  palette, and H reuses it. `art-inventory.md` gains the workshop as a placeholder-glyph row (every glyph is code-drawn;
  the art is parked, D639).
- **The workshop's card:** the smithy's card with *Forges:* read *Makes:* — the *Makes:* dropdown (§4.3), the seats, the
  idle note from `WhyTheBatchWaits`. **The legibility sentences, proposed:**
  - *"Makes: handcarts"* (the dropdown's one item).
  - *"Nothing to make — no store in reach holds the 10 logs a handcart takes."* (the input refusal, naming the store
    where the mill's does).
  - *"Nothing to make — the handcarts limit is met (12 stored)."*
  - *"Nothing to make — every household has a handcart, and the shelves hold one for the next (9 stored)."*
  - *"Nobody works here"* / *"Alys is making handcarts, the 2nd of the day"* as every workplace card.
- **The carpenter's card:** the person's card as any trade's — the TOOL line (*"Stone tools — 37 of 150 uses left"*),
  the skill line (*carpentry, N years*), the state sentence. Nothing new.
- **The idle sentence** (`Main.cs` ~3769, the ghost's and the catalogue's): `JobKind.Carpenter => "logs are made into
  handcarts here"`.
- **The professions panel:** a *carpenters* row from the job row; *"the village wants a carpenter"* when
  `CarpentersWanted > 0`; the **handcarts chip** goes amber by it.
- **The stock-limits panel:** a *handcarts* row, unset (§4.4), grouped with the materials.
- **The warehouse card:** a *handcarts* row for free — a store's card lists every good it can hold (D393).
- **The log:** at INFO, once, when the first handcart is racked: *"The first handcart stands in the warehouse."* — the
  story's line (D8/D9); nothing per cart.
- **The tech tree map:** the *workshop* node, greyed until learned, its card one sentence, its how-far line *"{n} of
  {N} logs felled"* (`TechTreeView.HowFar`). It is a hands-on learning, so it can be the first to show the map (D440).
- **The new-game screen and the founding probe:** the carpenter appears in the professions list at nobody wanted; the
  workshop is **greyed at the founding** — the founding probe's unknown-trade list gains *Carpenter*, and
  `NothingUnlearnedIsOffered` and the `filters:` line both read the workshop as not yet offered.

**Probe lines:** the `cards:` line gains the workshop's dropdown check in the smithy's shape — the item count equals
the rows with a `made_from` and is **≥ 1** (the smithy's check demands ≥ 2; the workshop's one item is the design, so its
check is written for one), and every item fits the card's inner width. A line of its own, **`[widths] workshop:`** ✅ /
⛔, poses a finished workshop and reads: the *Makes:* row visible with *handcarts* selected, the idle note inside the
width, and the Works row of the bar unwrapped. The **`filters:`** line must still read the workshop as greyed at the
founding, and the **`tech tree:`** line must hold the new node's card at 160. Bar height **151**, `tile centres` ✅,
`new game:` ✅, `done.`.

## 7. Saved, hashed, drawn, told

- **Hash:** `BuildingKind.Workshop` 27 mixes by position as every workplace does; `Goods.Handcart` 15 mixes in a
  store only when held (D291: sparse over goods); `JobKind.Carpenter` 16 mixes with the job held;
  `Workplace.MakeGood` sparsely, only off its default (§4.3); and **`WorkshopLearned`, a tag byte only when true**
  (§4.1a choice 2 — `LogsEverFelled` stays an unhashed statistic, its remark and the hash's gaining the line that says
  why, and the gap). So **a village that has not learned the workshop hashes byte for byte as before, and every golden
  is the check.**
- **Save format 9 → 10** (§4.3, Joe's call): `make_good` on every workplace. A format-9 save is refused in words (D510);
  Joe's saves are format 9 and he has said he will start new. `logs_ever_felled` is already saved; **`workshop_learned`**
  is new, saved beside `a_free_smithy_is_owed`'s kind of flag. `SaveLoadTests.Fields` gains `MakeGood` and
  `WorkshopLearned` as saved; `save-load.md §4`'s format row and §9's field list record the bump.
- **Goldens — expected to hold, every one** ⚠️ *as built (D678): every one but the farm-and-clear pair, which crosses
  2,500 at 3,180 felled (§4.1b) and learns the workshop, and the Phase 5 pair, which carries the neighbours' stocks and so
  the overland neighbour's handcarts (§5; D661's precedent) — findings, each proved and re-pinned; 392 goldens before, 396
  grep lines after (the four RE-TAKEN notes carry the old values), every value unchanged but those four*: no fifty-year run crosses `workshop_unlock_logs` (§4.1b), so the flag is false
  and mixes nothing; there is no starting limit (§4.4); `MakeGood` at its default mixes nothing; the skill row is safe
  (D392); the goods list grows at the end. **A golden that moves is a finding, not a re-pin:** its cause is named and
  proved by its parent (D554's method: the suspected line switched off, the old value passing) before anything is
  re-taken. The `ModdedGoodTests` fixture id moves (trap D348) and is not a golden.
- **Determinism:** nothing here reads the clock or draws from `Rng`; the make is a timed action through `BeginWork`.

## 8. What the builder's hut does not do — D553 call 6, redefined

D553 call 6 owed *"the builder's hut growing into a workshop with a carpenter trade and the builder's hammer"*.
`handcart.md §5` redefined it and Joe took the redefinition (D672 call 2). So, stated rather than left to inference:

- **The builder's hut stays the builders' seat and the repair site's** (`building-condition.md §4.3`). Nothing about it
  changes kind, cost, seats or card. No building in the game changes kind.
- **No carpenter works at the hut.** The carpenter's `works_at` is the Workshop; the allocator never seats one there.
- **There is no hammer.** D617 gave the builder a tool — a third faster, iron doubling the work, worn a use a stint — and
  that *is* the builder's tool. `Goods.Hammer` is not made, not stored, not fetched.
- **The word *workshop* has meant three things in this project**, and this spec is the only one it names a building:
  `wood-fuel-and-tools.md §7` said *"until there is a workshop to make them at"* and meant **the smithy** (spent, D391);
  D553 meant **the grown hut** (redefined, D670 / D672); this is **`BuildingKind.Workshop`**. A reader of the older two
  should not expect either to be this.
- `building-condition.md §4.6` already points here (*"its shape, redefined, is sketched in `handcart.md §5`"*); it
  gains this spec's name when this slice is built, in that commit.

## 9. How it is tested — `tests/Bclone.Sim.Tests/WorkshopTests.cs`

TDD, red first, each red-checked by a mutant and the reds counted (D326); a zero is written down, never hidden.

1. `ACarpenterMakesAHandcartFromLogs` — `logs_per_handcart` out of a store's count, one handcart into a store that
   takes them; the stint counts `BatchesThisStint`. Red: the batch arm gone; the take skipped (⚠️ zero if the store
   always has logs — pose the store to run dry).
2. `AHandcartIsRackedWhereToolsAreAndNeverAtTheMarket` — a market and a granary beside the workshop, a warehouse further:
   the cart walks to the warehouse. Red: `stored_by` widened to the market.
3. `AMetHandcartsLimitStopsTheWorkshop` — the limit at the shelves' count; `WhyTheBatchWaits` names it. Red: the limit
   unread.
4. `WithNoLimitSetTheWorkshopStopsAtOneAHouseholdAndASpare` — three housed households, no limit: he stops at four on
   the shelves and the sentence says *every household has a handcart*. Red: the derived stop gone (he makes for ever);
   the spare counted twice.
5. `AnEmptyHouseWantsNoCart` — a house with no household does not count in `HandcartsWanted`. Red: homes counted
   instead of households.
6. `AWorkshopWithNoLogsInReachSaysWhichStore` — the input refusal names the store and the recipe (*"the 10 logs a
   handcart takes"*). Red: the sentence's recipe left out.
7. `TheWorkshopNeverAsksAfterTheWintersFirewood` — `FirewoodShortfall > 0` and the workshop still makes (no firing).
   Red: `Firing` set to 1 in the batch.
8. `CarpentersAreWantedOnlyWhenHouseholdsLackCarts` — `CarpentersWanted` 0 at one a household plus the spare, > 0 below.
   Red: the shelves ignored (⚠️ zero if the hands hold them — carts are never in hands in W; pose the shelves full).
9. `TheCarpentersToolWearsAUseAMake` — `ToolUses` down one per make begun. Red: `uses_tool` false on the row.
10. `TheCarpenterJoinsEveryBatchTradeSwitch` — the §4.5 list as a table: `BatchFor`, `KindsInOrder`, the stood-down
    switch, state → trade. Red: each omission, one at a time (one red each, counted).
11. `NoWorkshopBeforeTheLogsAreFelled` / `TheWorkshopIsLearnedAtTheNumber` — `WhyNotYet(Workshop)` names the count and
    the knob below it, `Mark` refuses in that sentence, the bar hides it; at `workshop_unlock_logs` felled the arm is
    null, `LearnedByDoing` has fired once (the log line, `ShownTheTechTree`), the tree's node reads the counter and the
    knob (`ProgressOf`), and `HowFar`'s words are as written. Red: the arm reading `>` for `>=`; the knob unread (⚠️
    zero if the pose fells exactly the knob's worth — pose one short, then one more); the node's condition wrong.
11a. `ALearnedWorkshopIsInTheFingerprint` — two villages differing only in `WorkshopLearned` hash differently, and two
    differing only in `LogsEverFelled` **below the gate hash alike** (the gap, asserted on purpose so nobody "fixes" it
    without reading §4.1a); `TheWorkshopsProductIsInTheFingerprintOnlyOffItsDefault` — a workshop on handcarts mixes
    nothing; set to a modded second product it does. Red: each `Mix` line gone.
11b. `AFixtureRaisedWorkshopIsNotGated` — `RaiseFreeBuilding` stands one at tick 0, as the smithy's helper does.
12. `TheRowsAreAsShipped` — data and C# defaults agree: 25 / 12 / 40 / 2 / 2×1, `workshop_unlock_logs`,
    `logs_per_handcart`, `make_ticks`, `makes_per_stint`, `made_from` on the handcart row and empty elsewhere,
    `stored_by`, `trade_worth`, `uses_tool` true, `tool_waits_for_the_smithy` false, no `starting_stock_limits` row.
    Red: each number wrong in the data.
13. `ABadWorkshopRowIsRefusedAtLoad` — a `made_from` naming a good no store holds; `logs_per_handcart` 0;
    `makes_per_stint` 0; `workshop_unlock_logs` below zero; `SetMakeGood` given a good with no recipe. Red: each check
    gone.
14. `AVillageThatHasNotLearnedTheWorkshopHashesAsBefore` — a fixture village before and after the rows, hash to hash —
    and **every shipped golden stands as the same guard.**
15. `UsesToolAndMadeFromRoundTripThroughJson` — `ModdedJobTests` / `ModdedGoodTests`' shape; the modded fixture's id
    bumped with the reason in its comment (trap D348).
16. Save: a village with a workshop set to its product and three carts on the shelf saved and loaded to the same hash
    and the same village a year on; a format-9 save refused in words; `EveryFieldIsSavedOrNamed` green with `MakeGood`
    entered as saved.

**View (`viewred.py`'s shape, by hand):** the *Makes:* dropdown left empty → the `cards:` line ⛔; an item too long →
⛔; the Works row wrapping → bar height ≠ 151; the `IsKnown` arm left out → the `filters:` line ⛔. Determinism green.
**No golden moves** (§7); the suite's wall-clock noted beside the last run's (15m06 at D671).

**As built (D678, 2026-11-01) — 20 facts in `WorkshopTests.cs`, 37 mutants, the reds counted (filter `WorkshopTests`, 20
tests; a red is a test that went red under the mutant):**

| Guard | Mutant | Reds |
|---|---|---|
| 1 | the `BatchFor` arm gone (made unreachable — removing the call is `IDE0051`, trap D621) | **11** |
| 1 | the take skipped (`FinishABatch` takes nothing) | 1 |
| 2 | `stored_by` widened to the market | 2 |
| 3 | the limit unread in `WhyTheBatchWaits` | 1 |
| 4 | the derived stop gone | 1 |
| 4 | the spare counted twice | 1 |
| 5 | homes counted instead of living households | 1 |
| 6 | the recipe left out of the input sentence | 1 |
| 7 | `Firing` 1 in the carpenter's batch | 3 |
| 8 | the shelves ignored in `HandcartsShortfall` | 1 |
| 8 | the seat cap gone from `CarpentersWanted` | 2 |
| 9 | `uses_tool` false on the row | 2 |
| 10 | `KindsInOrder` without the carpenter | 6 |
| 10 | the state → trade arm (`ErrandKind`) gone | 5 |
| 10 | the `KnowsOf` list without the carpenter | 1 |
| 10 | the idle-note switch without the carpenter | 2 |
| 10 | `SkillSystem.OutOnTheWork(Making)` false | **0 → 0 → 1**: zero against *"ticks > 0"*, zero against the bench's ticks (the walk grows the skill too), red once the ruling is asserted by name — a worth, not a count |
| 11 | `>` for `>=` at the crossing | 1 |
| 11 | the knob unread (2,500 hard-coded; the pose's knob is 7) | 2 |
| 11 | the node's condition wrong | 2 |
| 11 | the brush's fell not counted (`TakeFromTheGround`) | 1 |
| 11 | the forester's stand fell not counted (`BehaviorSystem`) | **0, written down**: no fixture village takes that path in a year — every fell goes through the brush since the stands retired |
| 11a | the flag's `Mix` gone | 1 |
| 11a | `MakeGood`'s `Mix` gone | 1 |
| 12 | `WorkshopStone` 13 in the C# default | **0, and right**: the shipped file governs, so the data mutant is the json's |
| 12 | `workshop_stone` 13 in the json | 1 |
| 12 | `logs_per_handcart` 9 in the json | 1 |
| 12 | `trade_worth` 8 | 1 |
| 13 | the `made_from` catalogue check gone | 1 |
| 13 | the `made_from` amount check gone | 1 |
| 13 | `SetMakeGood`'s recipe check gone | 1 |
| 13 | the `workshop_unlock_logs` check gone | 1 |
| 16 | `workshop_learned` not saved | 1 |
| 16 | `make_good` not saved | 1 |
| 16 | `handcarts_ever_made` not saved | 1 |

Guard 14 is the shipped goldens (392 before, 395 grep lines after — the RE-TAKEN notes carry the four old values; the two
farm values moved for the reason §4.1b names and the two Phase 5 values for §5's neighbour line, each proved alone and
no other, read off a `git diff` of the tests, not a passing suite) and the three silent mixes
asserted on a shipped village; 11b is the standing workshop working with the flag taken back; 15 is the modded JSON. ⚠️
`ModdedGoodTests` had been testing clothing under the name *pitch* since D659 — the fixture now restates clothing (14) and
handcarts (15) and pitch sits at 16 (its own remark's bug, a fourth time).

## 10. Measured before typed — the workshop's own cost

`handcart.md §10` says Slice W has *"nothing to measure but that he works, stops at the limit, and the suite and
probe stay green."* One cheap number is worth taking anyway, because the build is the moment the numbers are typed:
**what a workshop costs a village before any cart does anything.** `tools/harness/ZzLimits.cs`'s `ZZ_LABEL` arms on
D420's 55 fifty-year villages (the D445 painting), a workshop marked at year 5 beside the smithy's spot, no limit set:

**✅ Filled 2026-11-01 (D678), before `data/sim.config.json` was typed** — `tools/harness/ZzWorkshop.cs` (ZzLimits' shape:
the shipped cold start, the opening played, a granary and a warehouse at Year 3), seeds 200–254, fifty years, four arms
in one sitting on the same build. Totals over the 55 valleys; medians in brackets. *Roofs waiting on logs* is site-ticks
in which a **house** site still wanted logs; *every site* beside it.

| Arm | Alive at 50 | Peak | Logs held at 50 | Carts made | Carpenter seat-years | Roofs waiting on logs | Every site waiting on logs |
|---|---|---|---|---|---|---|---|
| no workshop (the baseline, this build) | 769 (7 valleys dead) | 987 | 20,403 (420) | — | — | 779,670 (1,686) | 1,429,767 |
| a workshop, `logs_per_handcart` 10 / `make_ticks` 20 / 1 a stint | 732 (7 dead) | 984 | 20,182 (416) | 370 | 17 | 811,457 (1,689) | 1,632,723 |
| … 6 logs | 738 (7 dead) | 979 | 20,282 (417) | 370 | 17 | 798,469 (1,546) | 1,614,837 |
| … 15 logs | 726 (7 dead) | 982 | 20,192 (418) | 371 | 17 | 805,391 (1,621) | 1,621,042 |

Seed by seed against the baseline: alive **−0.67 / −0.56 / −0.78** a valley at 10 / 6 / 15 logs (sd 2.7–3.0; no valley
died with a workshop that lived without one, nor the reverse); logs held at 50 **−4 / −2 / −4**; roofs waiting on logs
**+578 / +342 / +468 site-ticks on the mean and +15 / +3 / −9 on the median**, against a baseline median of 1,686 — the
mean is a handful of valleys and the median is the village. **Reading:** the workshop stood in **49 of 55 valleys** (the
six it never stood in were dead by Year 5 or starving — the mark fell on a valley with nobody to build it) and made
**370 carts for 299 housed households at Year 50** — one a household and the spare, then stopped, in **17 seat-years
across 49 villages**: a burst of a third of a year each, as §4.4 said. The alive dip is the same at 6, 10 and 15 logs, so it
is not the recipe's cost; it is inside a 55-seed harness's noise (t ≈ −1.6 to −2.1) and reads as the workshop's own 25 logs
and the carpenter's hands for a season. **Nothing moved: 10 logs, 20 ticks, 1 a stint, 25 / 12 / 40 / 2 are typed as
proposed.** ⚠️ **One pose, said:** no harness village ever holds stone (the quarry is never learned unattended —
`tools-and-the-smith.md §9.4`'s lot for the smithy), so the arm *gives* the marked workshop its 12 stone; the logs are
paid. The table is about the logs, and those were.

What the table is for: the **logs** column and the **roofs waiting** column. A cart is logs a small village spends on
roofs and a cold one on firewood (`handcart.md §4`, *honest costs*); if the derived stop (one a household) takes the
timber from a house in the first ten years, the stop or the recipe is wrong before the cart ever moves. **Alive** is
reported and is *not* the bar — in W a cart does nothing, so a workshop can only cost (the arm's own cost dominated the
smithy's table too, `tools-and-the-smith.md §9.4`). The numbers go into this table and `DESIGN.md §7` before
`data/sim.config.json` is typed; a shipped unattended village never raises a workshop, so the goldens do not see
the arm.

**The payoff is measured in Slice H**, on `ZzLogistics`, against `logistics.md §5` and `handcart.md §9`: the Fetch share
toward the control's 3–13 %, trips a household-year down ~2.5×, the median trip unchanged. Not here.

**Joe's balance bar** (D666): a source building and a workshop should comfortably supply ~10 adults. Ten adults is
three to five households; at one cart every `make_ticks` 20 plus the walks, a carpenter carts them in a season and
stops. If the measured pace says a season is too slow for a village already standing when the workshop comes, the
proposal moves to 2 a stint; the number is his after play.

## 11. Joe's calls — ✅ answered 2026-11-01 (D675), relayed by the session that asked him

1. ~~The workshop on the bar from the founding (recommended), or a civic stop at N households?~~ **Neither — learned by
   felling wood.** Joe: *"does anything currently trigger on a certain amount of trees/wood/lumber harvested? If not,
   perhaps the workshop can appear on that — but I'd want it to be a larger number than anything else we currently have
   in-game."* Built as §4.1a: `workshop_unlock_logs` against `LogsEverFelled`, the quarry's shape. ✅ **The number is
   his: 2,500 (D676, *"2500 logs"*)** — his villages fell 70–100 logs a year and stand at 2,600–4,500 after 25–50 years, so
   it lands around Year 25–35, where anything above the mill's 5,000 would come after Year 50 in nearly every village he
   has played. ✅ **The hash: the learned flag, sparse** (§4.1a choice 2, decided D676) — the gate reads the flag, no
   golden moves, the gap written down.
2. ~~No shipped `handcarts` starting limit, or a typed number?~~ ✅ **None.** The derived stop — one a household and a
   spare — is the default; the panel's row is his ceiling if he ever wants one.
3. ~~No `MakeGood` field until a second product, or the field now and format 10?~~ ✅ **Now.** The field, saved, hashed
   off its default, **save format 10**; his format-9 saves refused — *"I don't mind starting a new game — that's actually
   my favourite part."*
4. ~~The carpenter's rank?~~ ✅ **After Tailor, before Quarrier.**
5. ~~The numbers?~~ ✅ **Agreed: every one a proposal until §10's table is filled** — `workshop_logs` 25, `workshop_stone`
   12, `workshop_capacity` 2, `logs_per_handcart` 10, `make_ticks` 20, `makes_per_stint` 1, the spare 1, `trade_worth` 20,
   and `workshop_unlock_logs` 2,500 (his, D676 — the one number in this spec that is not a proposal).

## 12. Acceptance — Slice W is done when

**✅ Items 1–9 met as built (D678, 2026-11-01); item 10 is Joe's.** The glyph is a hut with a wheel leaning on its wall, the
trade's a cartwheel; the trade colour fresh-sawn oak and the good's a painted cart's slate blue-green, both passing
`tools/legibility/check.py` (0 of 41 fail). The `workshop:` probe line reads *Makes: Handcarts (1 item), the idle sentences
fit a 268 card, the Works row is 151 with the button and 151 without*.

1. The Workshop is hidden and refused in a sentence until `workshop_unlock_logs` logs are felled; then the village is
   told once, the map shows, the button lights under *Works*; a player marks it, it is built from 25 logs and 12 stone,
   and a carpenter can be seated there from the professions panel.
2. The carpenter fetches a tool as any trade does, walks to a store, takes a cart's logs, makes at the workshop, and
   racks the cart in the nearest warehouse, cart or pile — **never the market**; the warehouse card shows *handcarts*.
3. He stops at a set `handcarts` limit; with none set he stops at one a household and a spare, and the card's idle
   sentence says which.
4. A workshop with no logs in reach says which store and what a cart takes; a village short of firewood still makes
   carts (no firing).
5. The professions panel wants a carpenter only while households lack carts; the handcarts chip goes amber by it.
6. The *Makes:* dropdown shows *handcarts*, the one item, inside the card's width, and setting it works; the
   `workshop:` probe line is ✅, `filters:` still greys the workshop at the founding, `tech tree:` holds the node's card.
7. **~~No golden moved~~ *(as built: 392 goldens, four values moved — the farm pair for the reason §4.1b names, the
   scenario learns the workshop; the Phase 5 pair for §5's neighbour line — each proved and re-pinned)*; a village that has not learned the workshop hashes byte for byte as
   before; save format 10, a format-9 save refused in words**; the four checks green; bar height 151; `done.` printed.
8. §10's table filled on the harness before the numbers were typed; §9's guards red-checked with the reds counted.
9. Docs in the same commit: this spec's status line true (*BUILT* with the date and the branch); `DESIGN.md §4` (the
   logistics entry), §6, §7; `HANDOFF.md` (banner, START HERE, the traps carried forward, one added);
   `professions.md §4` a *Carpenter* row; `content-inventory.md`; `save-load.md §9` and its format history (format 10);
   `tech-tree.md`'s node list (the workshop node); `art-inventory.md` the glyph row;
   `building-condition.md §4.6` naming this spec; the `handcart.md` status line (*Slice W built; H next*).
10. ✅ Joe plays it (2026-11-04, Hollowmere): a workshop marked and raised, somebody making carts, the card's *Makes:* row, the
    warehouse row, the idle sentence once the village is carted — *"everything else works properly for the workshop"*; his one
    change, the unlock to 1,500 (D682). Merged at his word only. **Then Slice H.**

## 13. Definition of Done

`METHODOLOGY.md §3`'s six, with §12 as the manual QA checklist, and: nothing in this slice touches `carry_capacity`,
the household, `BehaviorSystem`'s fetch paths, `PathWear` or `TerrainCostField` — **if a change there seems needed,
it is Slice H leaking in, and the answer is to stop and read `handcart.md`.**
