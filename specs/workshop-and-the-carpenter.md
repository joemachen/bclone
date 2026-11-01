# Spec: The Workshop, and the Carpenter who makes the handcart — Slice W

**Decisions:** D673 (this document). It builds on D672 (Joe: *"Yes, all six"* — every call in `handcart.md §10`),
D670 (the handcart thought through; `handcart.md §5` redefines this slice), D553 call 6 (the slice as first owed:
*the builder's hut growing into a workshop with a carpenter trade and the builder's hammer* — redefined here, see §8),
D617 (every hands-on adult takes a tool once a smithy stands — why there is no hammer), D391 / D446 (the smithy and the
smith, the shape this copies: a building, two seats, a recipe, a dropdown on the card, a limit that stops him),
D522 / D659 (the `Batch` shape: mill, oven, tailor's shop — the third of its kind becomes the fourth), D139 (a met
limit stops the work), D109 (the player staffs), D62 (the player's ceiling over a derived floor), D382 (footprints
per building type), D392 (a skill row is safe to add), D291 (the hash is sparse over goods), D510 (an older save is
refused, never half-loaded), D660 (a typed starting limit is hashed and moves every golden).
**Status:** 📝 **SPEC WRITTEN, NOTHING BUILT (D673, 2026-11-01).** No code, no data, no tests. **Every number in
this document is a proposal until it is measured** — on the harness in §10 before it is typed into
`data/sim.config.json`, and on `ZzLogistics` when Slice H gives the cart something to do. Owner: Joe + Claude Code.

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
- Not a knowledge node: the gate is the building (Joe, D672 call 3); *Heavy Haulage* stays the draught-animal node
  (`tech-tree.md §9.3`, *Cartwright Warehouse*; `TECH-EXAMPLE.md:206`).
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

**The gate is the building alone — proposed: on the build bar from the founding.** Joe's call 3 (D672): the handcart is
gated on the building, not a knowledge node. The workshop itself proposes to be an *ordinary* button like the
woodcutter's hut — no `TechTree` row, no `WhyNotYet` arm, no gift, no learned-by-doing stop — because any stop on the
workshop would be a second gate on the cart, and the building's own price (25 logs and 12 stone when a small village
spends both on roofs) is already the honest cost. ⚠️ **A reasonable alternative, not recommended, for Joe to overrule:**
a civic stop in §2.7's third shape — *the workshop is learned when the village has N households* (the demand is one a
household, so N is where a cart starts to matter). It would be the tailor's `TechTree` row one condition over and a
counter nothing keeps today. It is not recommended because the measured problem (D668) is in *every* established
village and the gate would only delay the answer. Recorded as a call in §11.

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

⭐ **Proposed: no `Workplace.MakeGood` field until there is a second product.** With one workshop product the
setting has one answer, so `SimWorld.MakeGoodOf(workshop)` *derives* it — the first (only) row with a `made_from` —
and the dropdown's setter has nothing to set. **That keeps `SaveGame.Format` at 9 (§7)**, keeps every hand-posed
`Workplace` in the suite and the probe untouched, and mixes nothing into the hash. The field, its sparse hash under a
tag (the default mixing nothing — `ForgeGood`'s rule), its save key and the format bump come with planks, when a
workshop can be set to two things. *Alternative:* the field now, saved, format 9 → 10, every format-9 save of Joe's
refused (D510) — for a dropdown that cannot change. Not recommended; §11.

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
- `HandcartsShortfall` = `HandcartsWanted` − carts on the shelves (every store's count), floored at 0 —
  `ClothingShortfall`'s shape.
- `WhyTheBatchWaits` for the carpenter with `StockLimits.For(Goods.Handcart) is null`: *"Nothing to make — every
  household has a handcart, and the shelves hold one for the next (N stored)."* ⚠️ The spare: proposed **one**, so a
  new household fetches without waiting a stint — the tailor's *year's wear* has no analogue because carts wear by the
  trip (Slice H), and until H nothing wears. Measured in H, typed then; in W the derived stop is `HandcartsWanted + 1`.
- No `starting_stock_limits` row for handcarts — **clothing's precedent (D660): a typed starting limit is hashed and
  would move every shipped golden** (D446's one golden moved for `"iron tools": 200` alone). The panel still shows the
  row, unset; Joe's number, if he wants one, is his to set on the panel — and if he wants it shipped, every golden is
  re-pinned once with this line as the cause. The config comment, in its own voice, proposed:

  ```jsonc
  // HANDCARTS (workshop-and-the-carpenter.md 4.4, D673): no starting limit, as
  // clothing (D660) -- a set limit is hashed and moves every golden. With none
  // set the carpenter makes one a household and one spare, and stops. The
  // panel's row is the ceiling if Joe wants one; the demand is one a household.
  ```

**Demand to hands:** `CarpentersWanted = ceil(HandcartsShortfall ÷ VillageEconomy.HandcartsMadePerYearAtWorst)`
(`CoatsSewnPerYearAtWorst`'s shape: stints a year at the worst walk × `makes_per_stint`). A `carpenters` slot in the
quota's `Asked` / `AtLeastPinned`; **`KindsInOrder` takes Carpenter after Tailor and before Quarrier** — proposed: a cart
is a convenience of the coat's kind, and the demand is finite (one a household, then spares), so he is wanted in a
burst when a workshop first stands and rarely after. The handcarts chip goes amber when `CarpentersWanted > 0`, as the
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
| `trade_worth` | **20** | sized from the logs it takes, as clothing's 8 was from leather (a log's worth × `logs_per_handcart`, rounded to the iron tool's 20); no neighbour stocks one — a cart is the village's make, and the caravan that would trade carts is what Slice H unblocks (`trade.md §4.7`) |
| `made_from` | `[{logs, logs_per_handcart}]` | §4.3; the one row with a workshop recipe |
| `source_name` | *the workshop* | the card's *"made at …"* |
| a cart's own columns | **none in W** | `cart_trips`, `cart_armfuls`, its slow on grass — Slice H's, on this row then (`handcart.md §6`) |

The founders' cart (`StoreKind.Cart`) is a *store*; a handcart is a *good*. **The good is named so the two cannot be
confused in code** (`Goods.Handcart`, never `Goods.Cart`), and the card calls it *a handcart* throughout.

## 6. The view, and what it says

- **Build bar:** a *Workshop* button under **Works**, beside the smithy (`BuildCategory.Works`; the tailor's shop went
  to *Resources* at Joe's word, D665, because it is a shop — a workshop makes things, so it sits with the smithy). An
  ordinary known button (§4.1): no `_workshopKnown` latch, no `RefreshALearnedButton`. ⚠️ **The bar must stay 151.**
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
- **The new-game screen and the founding probe:** the carpenter appears in the professions list at nobody wanted; the
  workshop is on the bar from the founding, so `NothingUnlearnedIsOffered` is unchanged (it is not unlearned).

**Probe lines:** the `cards:` line gains the workshop's dropdown check in the smithy's shape — the item count equals
the rows with a `made_from` and is **≥ 1** (the smithy's check demands ≥ 2; the workshop's one item is the design, so its
check is written for one), and every item fits the card's inner width. A line of its own, **`[widths] workshop:`** ✅ /
⛔, poses a finished workshop and reads: the *Makes:* row visible with *handcarts* selected, the idle note inside the
width, and the Works row of the bar unwrapped. Bar height **151**, `tile centres` ✅, `new game:` ✅, `done.`.

## 7. Saved, hashed, drawn, told

- **Hash:** `BuildingKind.Workshop` 27 mixes by position as every workplace does; `Goods.Handcart` 15 mixes in a
  store only when held (D291: sparse over goods); `JobKind.Carpenter` 16 mixes with the job held. **No new field**
  (§4.3), so nothing new is mixed: **a village with no workshop hashes byte for byte as before, and every golden is the
  check.**
- **Save format stays 9** (§4.3). The new enum values travel as the ints they are; a format-9 save without a workshop
  loads as it did. `SaveLoadTests.Fields` names nothing new because nothing new exists; `save-load.md §9` gains the
  line that says so. ⚠️ If the build finds it needs a field after all (a derived `MakeGoodOf` that cannot be derived),
  that is format 10 and D510's refusal of every format-9 save — **said to Joe before it is typed, not after.**
- **Goldens — expected to hold, every one:** the shipped-config harness and the Phase 5 pose raise no workshop; there
  is no starting limit (§4.4), no counter, no gate, no new field; the skill row is safe (D392); the goods list grows at
  the end. **A golden that moves is a finding, not a re-pin:** its cause is named and proved by its parent (D554's
  method: the suspected line switched off, the old value passing) before anything is re-taken. The one expected
  movement is `ModdedGoodTests`' fixture id (trap D348), which is not a golden.
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
11. `TheWorkshopIsOnTheBarFromTheFounding` — `WhyNotYet(Workshop)` null at tick 0; `NothingUnlearnedIsOffered`'s list
    unchanged. Red: a `TechTree` row added.
12. `TheRowsAreAsShipped` — data and C# defaults agree: 25 / 12 / 40 / 2 / 2×1, `logs_per_handcart`, `make_ticks`,
    `makes_per_stint`, `made_from` on the handcart row and empty elsewhere, `stored_by`, `trade_worth`, `uses_tool`
    true, `tool_waits_for_the_smithy` false, no `starting_stock_limits` row. Red: each number wrong in the data.
13. `ABadWorkshopRowIsRefusedAtLoad` — a `made_from` naming a good no store holds; `logs_per_handcart` 0;
    `makes_per_stint` 0. Red: each check gone.
14. `AVillageWithNoWorkshopHashesAsBefore` — a fixture village before and after the rows, hash to hash — and **every
    shipped golden stands as the same guard.**
15. `UsesToolAndMadeFromRoundTripThroughJson` — `ModdedJobTests` / `ModdedGoodTests`' shape; the modded fixture's id
    bumped with the reason in its comment (trap D348).
16. Save: a village with a workshop and three carts on the shelf saved and loaded to the same hash and the same village
    a year on; `EveryFieldIsSavedOrNamed` green with nothing added to `Fields`.

**View (`viewred.py`'s shape, by hand):** the *Makes:* dropdown left empty → the `cards:` line ⛔; an item too long →
⛔; the Works row wrapping → bar height ≠ 151. Determinism green. **No golden moves** (§7); the suite's wall-clock noted
beside the last run's (15m06 at D671).

## 10. Measured before typed — the workshop's own cost

`handcart.md §10` says Slice W has *"nothing to measure but that he works, stops at the limit, and the suite and
probe stay green."* One cheap number is worth taking anyway, because the build is the moment the numbers are typed:
**what a workshop costs a village before any cart does anything.** `tools/harness/ZzLimits.cs`'s `ZZ_LABEL` arms on
D420's 55 fifty-year villages (the D445 painting), a workshop marked at year 5 beside the smithy's spot, no limit set:

| Arm | Alive at 50 | Peak | Logs held at 50 | Carts made | Carpenter seat-years | Roofs waiting on logs |
|---|---|---|---|---|---|---|
| no workshop (the baseline, = the branch's parent to the person) | — | — | — | — | — | — |
| a workshop, `logs_per_handcart` 10 / `make_ticks` 20 / 1 a stint | — | — | — | — | — | — |
| … 6 logs | — | — | — | — | — | — |
| … 15 logs | — | — | — | — | — | — |

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

## 11. Joe's calls — proposed, none made

1. **The workshop on the bar from the founding** (§4.1, recommended), or a civic stop at N households? ⭐ From the
   founding: the measured problem is in every established village, and the price is the gate.
2. **No shipped `handcarts` starting limit** (§4.4, recommended — clothing's precedent, no golden moves), or a typed
   number and every golden re-pinned once? ⭐ None shipped; the derived stop (one a household and a spare) is the default.
3. **No `MakeGood` field until a second product** (§4.3, recommended — save format stays 9, Joe's saves load), or the
   field now and format 10? ⭐ Derived until planks.
4. **The carpenter's rank: after Tailor, before Quarrier** (§4.4), or lower? ⭐ After Tailor; the demand is a burst.
5. **The numbers, every one a proposal until §10's table is filled:** `workshop_logs` 25, `workshop_stone` 12,
   `workshop_capacity` 2, `logs_per_handcart` 10, `make_ticks` 20, `makes_per_stint` 1, the spare 1, `trade_worth` 20.

## 12. Acceptance — Slice W is done when

1. A player marks a Workshop under *Works* at the founding, it is built from 25 logs and 12 stone, and a carpenter can
   be seated there from the professions panel.
2. The carpenter fetches a tool as any trade does, walks to a store, takes a cart's logs, makes at the workshop, and
   racks the cart in the nearest warehouse, cart or pile — **never the market**; the warehouse card shows *handcarts*.
3. He stops at a set `handcarts` limit; with none set he stops at one a household and a spare, and the card's idle
   sentence says which.
4. A workshop with no logs in reach says which store and what a cart takes; a village short of firewood still makes
   carts (no firing).
5. The professions panel wants a carpenter only while households lack carts; the handcarts chip goes amber by it.
6. The *Makes:* dropdown shows *handcarts*, the one item, inside the card's width; the `workshop:` probe line is ✅.
7. **No golden moved; save format 9; a village with no workshop hashes byte for byte as before**; the four checks green;
   bar height 151; `done.` printed.
8. §10's table filled on the harness before the numbers were typed; §9's guards red-checked with the reds counted.
9. Docs in the same commit: this spec's status line true (*BUILT* with the date and the branch); `DESIGN.md §4` (the
   logistics entry), §6, §7; `HANDOFF.md` (banner, START HERE, the traps carried forward, one added);
   `professions.md §4` a *Carpenter* row; `content-inventory.md`; `save-load.md §9`; `art-inventory.md` the glyph row;
   `building-condition.md §4.6` naming this spec; the `handcart.md` status line (*Slice W built; H next*).
10. Joe plays it (`.\run.bat`): a workshop marked and raised, somebody making carts, the card's *Makes:* row, the
    warehouse row, the idle sentence once the village is carted. Merged at his word only. **Then Slice H.**

## 13. Definition of Done

`METHODOLOGY.md §3`'s six, with §12 as the manual QA checklist, and: nothing in this slice touches `carry_capacity`,
the household, `BehaviorSystem`'s fetch paths, `PathWear` or `TerrainCostField` — **if a change there seems needed,
it is Slice H leaking in, and the answer is to stop and read `handcart.md`.**
