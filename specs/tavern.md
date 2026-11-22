# Spec: Beer and the tavern (W3)

> Status: **✅ BUILT ON `slice/w3-tavern` (2026-11-22, D836; ale brewed to its limit and kept in the granary at his word
> while playing, D838) — STEPS 1–6: THE GATE'S GOLDEN AND THE TAVERN'S ANTI-VACUITY BUILT (D841); ⏸️ JOE'S PLAY OF THE
> NEW BUILD, THEN THE MERGE AT HIS WORD (`main` INTO THE SLICE FIRST).** Three goldens moved, each proven by ablation (§7). Two things changed at the build
> and he kept both (§5.2's firewood, §5.3's limit — D837, *"Keep both"*), and the keeper never drinks at their own tavern (§5.4a).
> Specced 2026-11-21 while `slice/steep-quarry` held the checkout; Joe answered 2026-11-22 (§10): *"all as recommended
> except for who runs the tavern. B: a tavern keeper"*, and the three follow-ups the keeper raised, *"All as
> recommended"* (§10.1). Built: ale, the brewery, the tavern and its keeper, the evening, the comfort, the find's clause and
> the cards (D836); the gate's golden (D841). Next: Joe's play.
> Phase 6, W3 of the workplan (`systems-complete.md §5.1`): **barley → ale at a brewery (W1's batch row), drunk at a tavern;
> contentment's first lever (W2, `morale.md`).** W1 and W7 were its prerequisites (D789, D821); both are in `main`.
>
> Neighbours: `batch-trade.md` (the brewery is a row; §6 step 6 says what a *shipped* batch trade still needs),
> `barley.md` (the grain; §11.4 the find's sentence, whose *"brew beer"* clause is owed here — D826), `morale.md` (§4.1 the
> tavern's row, §13.1 the measurement, §14 call 5's *"a tier above the cabin may raise contentment later, with the tavern's
> rows"*), `water-need.md` / D427 (the well trip — the errand shape the evening at the tavern copies), `school-and-education.md`
> (a building warm in winter while you are in it), `buildings-plan.md` (Brewery: placed, by doing; Tavern: placed, civic),
> `trade.md §8.4` (D587: a good joins the neighbours the day its system lands).

---

## 1. Why

W2 shipped contentment with **only causes that lower it** (`morale.md §4.1`: *"W2 ships with no building that raises
it"*). The review put W3 straight after it for that reason — *"a morale with no lever to pull is §1.1's invisible
weather"* (`systems-complete.md §5.1`). W3 is the first thing the player can **build** to keep a person who would
otherwise go, and the first new chain since the mill to sit on top of a crop.

Barley is its grain by D821: beer never pulls wheat from the mill, and a blighted wheat year (W8) does not take the ale.

## 2. Pillars and non-negotiables

- **§2.8 Contentment** — the lever; ⛔ still only its two effects (leaving, a longer wait for a child). The tavern never
  speeds work, never feeds anyone, never heals anyone (D207, D797).
- **§2.2 Smart labour** — the brewer is hired like the miller, the tavern keeper like any trade: nearest hands first,
  every refusal says why. ⚠️ The keeper is a hand that produces nothing (Joe's call 3B) — the marketer's honesty applies:
  the card says what the village gets for it, the evenings.
- **§1.1 Legibility** — every evening at the tavern is a villager you can see walk there; every effect on contentment is
  a sentence on the card naming the tavern.
- **§1.4 People, not spreadsheets** — *"Otto is content — the walk to the quarry is long, but his evenings at the
  tavern make up for it."*
- **§0.1** — ⛔ the slot machine (`morale.md §5`): there is still no bar to fill. A village does not *need* a tavern to be
  content; an unsettled person may be kept by one.

## 3. What exists today, read off the code (2026-11-21)

| Thing | Today | W3 needs |
|---|---|---|
| **Barley** | `Goods.Barley = 16`, crop row 2, a forager's find at 5,000 forage (`BarleyLearned`, D825); eaten like wheat; stored by granary, market, cart | the brewery's input |
| **A batch trade as a row** | `BatchRow` (`World/BatchTrade.cs`), four built-in rows in `SimConfig` (tailor, carpenter, miller, baker) | a fifth row: brewer, barley → ale |
| **Hiring a shipped trade** | `LabourAllocator.KindsInOrder` is a list of shipped `JobKind`s — a modded trade is never seated (D795) | `JobKind.Brewer` (18), `BuildingKind.Brewery` (31), job/building/skill/batch rows, a place in `KindsInOrder` (after the baker) |
| **Where a batch takes and puts** | input from the **nearest reachable store holding a batch** (`StoreForTheBatch`, D713), output to the nearest store with room (`StoreForTheOutput`) — nobody carries either way | the same; ale stored where flour is (the warehouse) |
| **A batch's demand rule** | `BatchStop` flags (`FedFirst`, `OvensSupplied`, `EveryoneClothed`, `EveryHouseholdCarted`) and `BatchHands` | a new rule, `TavernsSupplied` (§5.3) — a new kind of demand is a named method, per W1's own rule |
| **An errand from home** | the well trip (D427, D778): offered **where a rest at home would begin**, to a `CanWork` adult, hunger and cold above it by structure; `WalkingToTheWell` → `DrawingWater` → home | the evening at the tavern, the same shape (§5.4) |
| **Warm while inside somewhere** | the school is warm in winter (D769) | the tavern, the same |
| **A building open only while staffed** | the school: `JobKind.Teacher` (17), taught only while `WorkerIds.Count > 0` (`SchoolSystem`, `HearthSystem` skip a school with no teacher); its own `Workplace.Store` (firewood, taken from the nearest store by nobody carrying) | the tavern: `JobKind.TavernKeeper` (19), open only while kept, its ale in its own `Store` |
| **A worker who carries to their building** | the marketer (`WorkTheMarket`, `StockingTheMarket`) — many goods, a cart, D693's legs; ⚠️ too entangled with carts to reuse whole | the keeper's armful of ale, a small errand of its own (call 3a) |
| **Contentment** | `contentment_causes` rows, each weight **> 0** (validated), summed against `contentment_unsettled_at` 2, read each season's turn (`ContentmentSystem.ReadTheSeason`) | a **comfort** — something that takes a cause off (§6), if call 1 goes that way |
| **Learned-by-doing nodes** | `TechNodeRow` (*Lore*), `TechCondition` — the mill and the bakery learned together at 5,000 wheat | a brewery node (and the tavern with it, as the bakery comes with the mill) |
| **The find's sentence** | `SimWorld.WhatTheFindMeansHere` — *"build a farm, and it can sow barley as well as wheat"*; the beer clause withheld until the brewery exists (D826) | the clause, now true |

⚠️ **A trap already paid for (HANDOFF, D513):** *"any new place somebody rests (the tavern …) inherits both"* —
arriving to rest runs `UnloadAtHome` wherever the villager stands, and `TravelingHome` counts as work on the skill clock.
**The evening at the tavern is therefore an errand, not a rest** (§5.4): its own two states, home afterwards the ordinary
way, read against `ArriveAt`'s tail and `OutOnTheWork` before a line is written.

## 4. What the player sees — in order

1. **The moment** brewing is learned (when, call 2): one line in the village log in the shape of the mill's —
   *"… somebody remembers how barley is brewed — the brewery and the tavern can be built."* The brewery and the tavern
   join the build bar; *Lore* shows the node.
2. **The brewery** — a workshop by the fields or the granary. Its card: the brewer, *"brewing ale"*, *"the 2nd brew of the
   day"*; when idle, one sentence why — *"No store within reach has the barley a brew takes"*, *"The village is short of
   food — barley is grain somebody could eat"*, *"No tavern stands to drink it"*, *"The taverns have ale enough"*.
3. **Ale** — a good on the stock-limits panel, in the granary beside its barley (D838), on the top bar's goods.
4. **The tavern** — placed among the homes, **kept by a tavern keeper** (*"Mira keeps the tavern"*), whom you see carry
   ale over from the granary (D838). Its card: its keeper, how much ale it holds, how many came this season, which homes are
   within reach, and when it is shut, why — *"No keeper — the tavern is shut"*, *"The tavern is dry — no store within
   reach has ale"*, *"Shut while the village is short of food"*.
5. **Of an evening**, adults within reach walk to the tavern instead of straight home, sit a while inside (lit, warm),
   and walk home. A villager's card: *"at the tavern"*.
6. **On a person's card** (the W2 tooltip): *"Otto is content — the walk to work is long, but his evenings at the tavern
   make up for it."* — or, past it, *"Otto is unsettled — no roof of their own and a long walk; an evening at the tavern
   was not enough."* A person with no tavern within reach reads exactly as today.
7. ⛔ **Never:** a number, a bar, an always-on alert, or a tavern that changes anybody's work.

## 5. The shape

### 5.1 Ale — a good (`Goods.Ale = 17`)

A `GoodRow`: not edible (nutrition 0, so it never counts as food and is never eaten), category *Fuel and goods* (beside
tools and clothing — ⚠️ the row's own name for the heading), stored by **granary and cart** — *beside its barley, Joe's D838; the draft had the warehouse, flour's* — and **`kept_with_food`**, so a granary that may hold it is no shared roof and keeps all its room for food (D838: without it, every granary's food could fill only half of it) —, a trade worth of
2–3 (processed, as bread). Appended, never renumbered; D827 opens every older save with it held nowhere. **⚠️ The modded good
`Planks` in `WorkshopTests` moves 17 → 18** (it moved 16 → 17 for barley).

### 5.2 The brewery — a fifth batch row

`JobKind.Brewer`, `BuildingKind.Brewery`, and their rows (job: works at the brewery, seats; building: footprint, materials
— logs and a little stone, the bakery's order of cost; skill: *brewing*; batch, below). **Nothing else in the batch path**
(`batch-trade.md §6` step 6). ⚠️ The modded test trade `Boatman` (job 18, in `FifthBatchTradeTests`, `ModdedJobTests`,
`ModdedBuildingTests`) moves up **two**, to 20 (the brewer at 18, the tavern keeper at 19); the brewery is building 31
and the tavern 32 — the modded buildings sit at 10 and `NextFreeId` and do not move.

```
Trade Brewer · Input Barley × 20 · Output Ale × 10 · no Firing (D836 — see below)
Verb "brew" · OneOf "a brew" · WalkTo "the brewery" · Working "brewing ale" · StintWord "brew" · FirstBatch "mashing the barley"
Stop FedFirst | TavernsSupplied · Hands ShortfallOverAYear · QuotaBand AfterStone · StandsDownInFamine true
```

- ⚠️ **No firewood (D836, changed at the build — call 6 had said *"a little firewood like the oven"*).** A batch takes its
  input and its firing from **one** store (D713: one walk, priced), and barley is kept where food is (granary, market,
  cart) while firewood is not — the only store holding both is the market, which a batch never uses. A copper lit with
  firewood would never have brewed. ✅ **Kept at his word (D837, *"Keep both"*).** Firewood back would mean barley in the
  warehouse too, or a second walk the stint does not price.
- **`FedFirst`** — barley is food, so the brewer stands down while the village is short of food (the miller's rule, D522).
- **`ShortfallOverAYear`** — the tailor's shape: hands for the shortfall over what one brewer brews in a year.
- **N, M and the ticks are measured before typed** (§8 step 1), to Joe's bar (D666): **one barley farm and one brewery
  comfortably keep about ten adults in ale, with a two-to-one margin**.

### 5.3 The brewery's demand — the player's stock limit (D838)

**Brew to the ale limit, tavern or not** — the oven's shape: every seat with work wanted, `FedFirst` above it, the limit
the stop (Joe, D838: *"i want ale to have a stock limit - not be determined by the tavern"*). Ale starts at **50** (D697's
number); the lowlands make 25 and the hills want 25 (D594's half).

*(Superseded, D838 — kept for the reasoning:)* D834–D837 had a named rule, `TavernsSupplied`: brew toward a season's
drinking for the adults within reach of a standing tavern, and with no tavern nothing; read under the limit (D836, kept at
D837). Joe, playing it, wanted the limit alone — a number he sets and can see, not one the taverns derive — so the rule and
its three methods (`AleWanted`, `AleShortfall`, `EveningsASeason`) were deleted, not left unread.

### 5.4 The tavern and the evening there

`BuildingKind.Tavern`, a workplace for **`JobKind.TavernKeeper` (19)** — Joe's call 3B. The school's shape: a tavern is
**open only while it has a keeper** (`WorkerIds.Count > 0`) **and ale in its own `Store`**. Drinkers drink from the
tavern's store, never from the granary — the keeper is the only way ale reaches it.

#### 5.4a The keeper

- **Their day** (call 3a): while the tavern holds less than `tavern_stock` ale, carry an armful from the **nearest
  reachable store holding ale** (D713's one question) into the tavern — `FetchingAle` → `CarryingAle` → the tavern's store;
  otherwise `KeepingTheTavern` at the building, a day, then home (the teacher's lesson's shape). ⚠️ **A small errand of its own, not
  `WorkTheMarket`** — the marketer's loop is cart-shaped (D693, `handcart.md §13.2`) and a tavern has no cart.
- **Who asks for them** (call 3b): the village wants **one keeper per standing tavern while there is ale to pour**
  (in a store within reach, or in the tavern), the builder's-hut shape of "a want equal to its seats", after the brewer in
  `KindsInOrder`. **Stands down in famine** — a hand back to food, and the card says the tavern is shut for it (*"a need
  before a comfort"*, as the water comes before the evening). The player's number overrides, as every trade's (D109).
- **How many** (call 3c): **one place a tavern**. Joe's content pass has three innkeepers at a *Tavern & Inn* — the inn
  half (travellers, meals) is not W3, and a hand is what a young village is short of.
- **No skill** — nothing the keeper does gets faster with practice (the teacher's precedent, D754 call 5). No tool worn.
- **Their own evening:** a keeper at work is at work, not out for the evening — their evening is the ordinary rule,
  and their home is near the tavern more often than not. ⚠️ Not a call: it falls out of "an errand offered where a rest
  begins", and a keeper whose shift *was* the evening counting it would be a bookkeeping trick on the card.
- ⚠️ **The D513 trap again:** the keeper's armful must be put into the tavern's store by its own arrival, never by
  `UnloadAtHome` — a keeper who goes home with an armful takes it home. Tested (§8 step 3).

#### 5.4b The evening there

Offered **where a rest at home would begin** (the well trip's spot, D427), to a `CanWork` adult whose home
is within **`tavern_reach_tiles`** of an **open** tavern (kept, with ale in it), who has not been for
**`tavern_every_days`**, after the household's water (the jar first — a need before a comfort), hunger and cold above it by
structure. `WalkingToTheTavern` → `AtTheTavern` for `tavern_sit_ticks`, **one ale drunk** (from the tavern's store), then
home. ⚠️ Shut on the walk there (the keeper stood down, the last ale drunk) — they turn for home, and the card says why.
Warm while inside, **by the school's own stove** (`HearthSystem`, `ShelterAt`): a fire while a keeper is seated and its
pile holds wood (`school_winter_firewood`, taken by nobody carrying), a roof otherwise — so the warmth is paid for. The day counted on the villager (`TavernEveningsThisSeason`, cleared at the season's
read).

- **Within reach** is the home's walk, in the one cost field (§2.6) — not a ring drawn on the map (D120 deleted rings; a
  tavern across a river is far). Measured before typed (call 5).
- **Children do not go**; contentment is an adult's (D802).
- ⛔ **It costs time, honestly.** An evening walked is an evening not spent at home; it never touches work hours, but a
  far tavern is a long walk every few days, and that is the reason *within reach* exists.

### 5.5 Learning it (call 2)

A tech node in *Lore* — `brewing`, unlocking the brewery, and `tavern` learned with it (the bakery-with-the-mill shape),
both requiring `barley`. Either at the find itself, or once the village has reaped its first barley
(`FoodEverProducedOf(Goods.Barley)` already counts it — no new counter, every save carries it, D825's ⭐).
The find's sentence (`WhatTheFindMeansHere`) gains its beer clause in the shape the answer makes true.

## 6. Contentment — a comfort (if call 1 is A)

A new list beside the causes, `contentment_comforts`, a row each:

```
{ "comfort": "TavernEvening", "at": 1, "eases": 1 }   // at least `at` evenings at the tavern in the season
```

At the season's read: **weight = causes held − comforts held' eases, never below nought**, against
`contentment_unsettled_at` as today. A comfort **only takes off a cause the person holds** — it never banks credit for
later, never makes anybody *more* than content, and with nothing held it does nothing at all. Its own enum
(`ContentmentComfort`), appended-never-reordered, its own bitmask on the villager (`ContentmentComforts`) so the card can
say it; hashed sparsely (nought when none). **Why a second list and not a negative weight:** the causes' validator
refuses weight ≤ 0, and every sentence reads *"the causes held"* — a cause that is secretly a comfort would make each of
those sentences wrong once.

⭐ **The two-cause line stays.** With a tavern a person needs **three** held causes to be unsettled, so what it buys is
exactly one cause's slack — the walk, a cold winter, a death in the family — said on the card by name.

## 7. Determinism, the hash, saves, goldens

- Integer only; no draw (the visit is a threshold on days, as the well's is a threshold on the jar).
- **Hashed sparsely:** the villager's evening count and comforts mask only when non-zero; the new states by number as
  every state is; the new latch, if any (call 2A has none — derived from `BarleyLearned`). The tavern's ale is in its
  `Workplace.Store`, hashed already as the school's firewood is — ⚠️ checked against the hash's code before believing it.
- **Saved off the default** (`tavern_evenings`, `comforts`) — no format bump; D827 opens older saves with ale held nowhere.
- **Goldens: expected none to move from the good, the rows or the rule existing** — no unattended village sows barley (a
  farm grows wheat until told), so none brews, so none builds a tavern. **Checked by `git diff` at every step, and any that
  moves is proven by ablation** (the task's rule; D825's nine were). A **posed** golden reaches an evening at the tavern
  and a comfort that keeps a person (Phase 6 gate item 2).

## 8. Build order — one step a commit, TDD, measured before typed

1. **Measure** (`tools/harness/ZzTavern.cs`, deleted before commit, D420): on Joe's saves and the shipped seeds, how far
   adults' homes are from where a tavern would stand (the market's spot as a proxy), how many evenings a season fit
   between work and rest, and what W2's read would say with a tavern-evening comfort — **how many of the people W2 would
   lose a tavern keeps**. Then N, M, `tavern_reach_tiles`, `tavern_every_days`, `tavern_sit_ticks` and the comfort's `at`,
   put to Joe with the measurement (D800's shape).
2. **Ale and the brewery** — the good, the four rows, `KindsInOrder`, `TavernsSupplied` (stop + quota + card), the node.
   Tests: the batch trade proof (`FifthBatchTradeTests`'s shape, now a shipped trade *hired*), `FedFirst`, the stop's three
   arms, a modded good/trade test moved up and still green.
3. **The tavern, its keeper and the evening** — the building, `JobKind.TavernKeeper` and its rows, the keeper's armful
   and stint, the want (one a tavern, stood down in famine), the errand, the states, warmth, one ale a visit, the card
   lines. Tests: the keeper **hired** by the village from shipped rows / carries ale in until `tavern_stock` / an unkept
   tavern is shut and nobody goes / famine shuts it / within reach goes / out of reach does not / a dry tavern does not /
   water first / children never / the D513 trap both ways (the keeper's armful never unloaded at home, nothing unloaded
   at the tavern by a drinker, no skill from the walk).
4. **The comfort** — `contentment_comforts`, the read, the card's sentence, the hash. Tests: one cause eased, never below
   nought, nothing with nothing held; **anti-vacuity — a posed village that W2 empties keeps a person with a tavern**, and
   its mirror — a village with no tavern reads exactly as W2 (`AKeptVillageLosesNobodyItKeeps` and
   `AVillageThatIgnoresContentmentLosesPeople` unchanged).
5. **The words** — the find's clause, the moment, the log lines, the probe's lines (the brewery and tavern cards, the
   spoilers line learned with the rest), the art lane's request in HANDOFF.
6. **The gate's golden, the four checks, Joe's play** (a checklist like `morale.md §15`).

**Where it stands (D836):** steps 1–5 built in one commit after step 1's. ✅ Tests: `BreweryTests` (6), `TavernTests` (16), the
batch, modded, save and limit pins moved with the new rows. ✅ Red-checked: 12 mutants, 14 reds — **one zero written down**: the
adults-only gate (a child never goes) scored nought, because the fixture's founders are all adults and no child stands at a
door; and the first evening guard scored nought against the 5-day wait (one drinker, three rests a season), so
`NobodyGoesAgainInsideTheWait` poses it (6 evenings red, 1 green). ✅ **Built since (D841):** the village-level anti-vacuity and the gate's golden — `TavernTests.ATavernSettlesWhatContentmentUnsettled`
and `TheGoldenThatSeesATavernSettleAValley`: the fixture's seed 9 (W2's departure valley) with a tavern by the founding, its
keeper the village's hire, its ale topped up each season, fifteen years — with the comfort 1 adult-season thinking of
leaving against 12 without it, and 13 alive against 10. ⚠️ **Nobody leaves that valley in either arm, even in thirty
years** (partners hold each other), so a departure *prevented* is not shown — written down, not claimed. Red-checked: the
comfort's easing zeroed, 1 mutant, 2 reds. *(What this line said before D841:)* ⏸️ **Not built:** step 4's village-level anti-vacuity (a
posed village W2 empties keeps a person with a tavern) and step 6's posed golden — both the gate's, next. ⚠️ The firewood
stove of §5.4b is the school's (`school_winter_firewood`); the brewery burns none (§5.2).

### 8.1 Step 1, measured (D835) — `tools/harness/ZzTavern.cs`

Joe's saves, the newest of each of 175 folders (a copy): **105 open**, 70 saved by v0.0.1 and refused (`ZZX`); **33 have a
home with an adult in it**, nine of them ten adults or more. A tavern stood, for the measure, beside the **home whose walk to
every adult's home, summed, is least** (the medoid in the one cost field — not the market, which half of them lack).

| Walk from home to the tavern | ≤ 4 | 5–8 | 9–12 | 13–16 | 17–24 | 25+ |
|---|---|---|---|---|---|---|
| every adult (313) | 33 % | 43 % | 13 % | 6 % | 3 % | 1 % |
| in villages of 10+ adults (201) | 20 % | 49 % | 16 % | 10 % | 4 % | 1 % |

- **Reach:** 12 tiles takes in **89 %** of adults (85 % in the big villages); 8 takes 76 %, 16 takes 95 %. ⭐ **12 is
  recommended** — nine in ten without a tavern on every street, and the far walk to work is a cause only at 24 (D800), so
  a tavern's reach is half the distance the village already calls *far*.
- **How often the evening is offered:** a rest spell begun at home — the well's spot — comes **0.51 a working adult-day**
  (median village 0.48, p25 0.21; two years on the 33). **`tavern_every_days` 5** → an adult within reach goes about
  **three evenings a season** (the p25 village two), so the comfort's **`at` 1** is met by anyone who can go at all.
- **What a tavern would ease** (four years on, W2's read as shipped): of 5,325 adult-seasons, 170 unsettled (3.2 %) — and
  **162 of them read exactly the line (2)**, the season one eased cause settles. Within 12 tiles, 143 of 152 unsettled
  seasons were at the line: **a tavern within reach would have settled 94 % of the unsettled seasons there.** Nobody left
  in these four years (W2 measured 4 in 120 over ten years, all Hollowmere's), so *who it would keep* is not in these
  saves — the posed golden (§8 step 4) is where it is shown.
- **The keeper's walk** from that home to the nearest warehouse: median **9** tiles, p90 15 (25 villages with one).
- **The brewery's numbers, to D666's bar:** ten adults × three evenings × four seasons = **120 ale a year**. At **20 barley
  → 10 ale** a brew (brewing boils away; the mill's batch of 20 in), that is 240 barley — **an eighth of one farm's ~2,000**
  (`food-chain.md §8`, a farm's two seats). A stint is the mill's, 4 brews of 4 ticks: **40 ale a stint**, so one brewer
  makes a year's drinking for ten in three stints — the 2:1 margin many times over, and the rest of the barley is eaten.
- **`tavern_stock` 20** — what the keeper keeps in: two brews, a fortnight of evenings for ten; **`tavern_sit_ticks` 2** —
  half a day, a rest spell's length less one, so an evening out is shorter than a night in.

⏸️ **Joe's to tune in play** (his call 5, *"measure first"*): reach 12, every 5 days, `at` 1, 20 → 10 a brew, a stock of
20. Each is one key in `data/sim.config.json`.

⚠️ **The reach above was measured from the medoid home — where a tavern *would* stand — not from where a player puts it
(D843).** Joe's first tavern (Elderwood, Year 33) stood 14 to 26 tiles' walk from every home, so nobody came: a year run on
from his save, 140-odd rests begun at home and not one evening. The card said *"Open of an evening"*. It now counts the
homes within reach — *"Open of an evening — 4 of 7 homes are within a 12-tile walk"* — and with none, it is not working
and says *"Open, but no home is within a 12-tile walk, so nobody comes of an evening. A tavern draws the homes around it:
build it among them."* (`SimWorld.SayTheTavern`, `HomesWithinReachOf`; the reach is `WalkToTheTavern`, one copy for the
offer and the count.) ⏸️ Whether 12 is right is still his (HANDOFF's W3 line).

## 9. For the art lane (D774) — to HANDOFF's 🅿️ ART section, as a request, not drawn here

- **Buildings:** the **brewery** (a workshop with a copper/kettle, a chimney — it burns firewood) and the **tavern**
  (a homely building with a sign; lit windows of an evening while anybody sits inside).
- **Good:** **ale** — a UI icon (the stock-limits and top-bar rows) and a carried look (a cask or a jug), its colour in
  `GoodsPalette` taken from the painting, as barley's was (D829).
- **Villager states:** *brewing* (the shared batch state — the building's own look), *carrying ale* (the keeper with a
  cask — the one new carried look), *keeping the tavern* (indoors), *walking to the tavern* (an ordinary walk), *at the
  tavern* (indoors; nothing drawn but the lit window).
- Until the art lands: both buildings draw in the generic workshop look, ale in a stand-in colour (`ui icons:` ⛔ until
  the icon is in `main` — the barley precedent, D827–D830).

## 10. Joe's calls — answered 2026-11-22

> Joe: *"all as recommended except for who runs the tavern. B: a tavern keeper."*
> **1A** a comfort that eases one worry · **2A** brewing learned with barley at the find · **3B a tavern keeper** ·
> **4** yes · **5** measure first · **6** yes.

### 10.1 What the keeper raises — answered 2026-11-22

> Joe: *"All as recommended"* — **3a A** the keeper carries ale in · **3b A** the village asks for one a tavern, stood
> down in famine · **3c one** keeper a tavern.

- **3a. What the keeper does all day.** **A (recommended):** carries ale over from the warehouse in armfuls — you see
  them walk with a cask — and otherwise stands in the tavern. It is what *"a job like the marketer, who carries ale in"*
  promised. **B:** only stands in the tavern; the ale arrives by itself the way the school's firewood does. Less code,
  less to see.
- **3b. Who asks for the keeper.** **A (recommended):** the village asks for one per tavern by itself once there is ale
  to pour, after the food trades, and lets them go when food is short (the tavern shuts and says so). **B:** the
  teacher's way — nobody until you type a number.
- **3c. How many keepers a tavern.** **One (recommended).** Your content pass has three innkeepers, but that's a
  tavern-and-inn with travellers and meals; W3's tavern pours ale, and a hand is what young villages are short of.

### The calls as put on 2026-11-21

1. **What does the tavern do to contentment?** *(his eye needed)*
   - **A — it eases one worry (recommended).** Someone who's had an evening at the tavern this season counts one fewer
     cause. The card says so by name. A village without a tavern is exactly as it is today — it never punishes you for
     not having one.
   - **B — not having a tavern becomes a worry of its own,** once brewing is learned. This is what W2's spec quietly
     assumed (*"its teeth from … the tavern's absence"*). ⚠️ Why I recommend against: learning to brew would make every
     adult out of reach one worry closer to leaving the day it is learned — and W2 measured *one worry at a time* as a
     fifth of every village gone in ten years. That's a tax every village must pay, which is the slot machine. W4 (the
     dead with nowhere to lie) is where contentment's teeth belong.
   - ⚠️ **A's honest downside:** in a village that is already content — most young villages — the tavern changes no
     outcome; what you get is the evening walk and the sentence. It earns its keep in big, spread villages like
     Hollowmere.
2. **When is brewing learned?**
   - **A — with barley itself, at the forager's find (recommended).** Your own words for the find (D826): *"now you can
     build a farm, plant barley, and brew beer."* One moment, the lever as soon as possible; the brewery sits on the
     build bar with *"nothing to brew"* until the first barley is in, which says what to do next.
   - **B — once the first barley is reaped**, a second small moment (*"the barley's in, and somebody remembers how to
     brew"*). Two beats, each pointing at the next step; the lever a year or two later.
   - Either way the tavern comes with the brewery (as the bakery comes with the mill).
3. **Who runs the tavern?**
   - **A — nobody; it pours from the stores near it (recommended).** The same rule every workshop already uses for its
     inputs. Costs no hands. A dry tavern says *"no store within reach has ale"*.
   - **B — a tavern keeper,** a job like the marketer, who carries ale in. A person to know (*"Mira keeps the tavern"*),
     your content pass's innkeeper — but a hand per tavern, in villages where hands are what's short.
4. **When do people go?** *(just say yes)* Of an evening, instead of going straight home; every so many days (measured);
   all year — it's warm inside, like the school; adults only; after the water's fetched. Never instead of work.
5. **How far is "within reach"?** *(just say yes to "measure first")* The walk from home, not a circle on the map. I'll
   measure your saves and bring you a number with what it means — the way the walk-to-work's 24 tiles was chosen (D800).
6. **The small numbers and rules** *(just say yes)*: sized so one barley farm and one brewery comfortably keep ~10 adults
   in ale (your D666 bar); the brewery burns a little firewood like the oven; the brewer stands down when food is short
   (barley is food); ale is stored in the warehouse and has a stock limit like every good; the lowlands brew it and the
   hills want it (a new good joins the neighbours, D587).

## 11. Acceptance (Definition of Done)

- §8's six steps, each commit with the goldens grepped before and after; any golden that moves proven by ablation.
- The brewer **and the tavern keeper hired** by the village from shipped rows (not seated by a test).
- Anti-vacuity both ways (§8 step 4); a posed golden that reaches a comfort.
- Every new guard red-checked and its reds counted (D326).
- The four CLAUDE.md checks, all read; docs in the same commit; one D-entry per decision.
- Joe plays it; his log read for what the walk was meant to hit before *"all fine"* is recorded.
