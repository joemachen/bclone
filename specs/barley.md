# Spec: A second crop — barley (W7)

> Status: **✅ BUILT on `slice/w7-barley` (D825–D826, 2026-11-21), UNPLAYED — §11 is what was built. ⏸️ Joe's play, then
> the merge at his word; ⏸️ his to tune in play: `barley_unlock_forage` (5,000, measured §11.2); ⛔ the probe's `ui icons:`
> waits on the art lane's barley icon (§7).** History: 📝 specced (D822);
> ✅ calls 2–5 as recommended (D823, *"I like all as recommended"*); ✅ call 1, barley found wild by a forager (D824,
> *"Yes, the forager's find"*, §9.1). *(Read "🔨 the build (§10), the gate's number measured before it is typed" at D824,
> "⏸️ call 1 reopened" at D823, "⏸️ five calls Joe's" at D822.)* *(Read "🔨 claimed; the spec is being written" at the claim commit.)*
> Phase 6, W7 of the workplan (`systems-complete.md §5.1`), moved ahead of W3 at Joe's word (D821, *"Yes, barley first"*).
>
> Neighbours: `crops-and-orchards.md` (the farm, the field, the calendar — D161/D162), D348 (a crop is a `CropRow`),
> D277/D283 (food is a capability — and the mouth must learn it too), `food-chain.md` (the mill and the bakery, wheat's),
> `tools-and-the-smith.md §9.3` and `workshop-and-the-carpenter.md §4.3` (a building's *what to make* choice on its card —
> the shape copied here), `painted-crops.md` (a crop is a folder of paintings, A59–A63), `trade.md §8.4` (D587/D594: a new
> good joins the neighbours the day its system lands).

---

## 1. Why — W7's two jobs and W3's

D821 put it in one line: **wheat was one field feeding three things** — food, the mill's flour, and the herd's hay number
— and a brewery on it would make a fourth. Barley does three jobs with one row:

1. **Blight's answer (W8).** Blight on a one-crop village has one answer — not to be one-crop (`systems-complete.md §4`).
   W8 will strike *one crop's* fields in a year; a village with barley beside its wheat loses half a harvest, not all of it.
2. **A second grain to eat.** Barley is eaten like wheat (raw, worth one meal a unit, D277). ⚠️ **Honestly: it is a second
   *grain*, not a second food *group*.** When W6's diet arrives (grain, meat, fish, fruit/herbs), wheat and barley sit in
   the same group, so barley does not make a diet varied — it makes the grain supply sturdy.
3. **The brewery's own grain (W3, not this slice).** W3's brewery brews barley and only barley, so beer never pulls grain
   from the mill under Joe's one-chain-ten-adults bar (D666).

**What it needs is small, and that is the point of D348:** a crop has been a data row since then, and barley is one more
row. What does **not** exist yet is a farm's *choice* of crop: every farm sows `CropsCatalog.TheOne` — *"the lowest id that
exists, until the farm can choose"*. This slice is mostly that choice.

## 2. Which pillar, and which non-negotiables

- **§2.2** — food from many kinds of work; **§2.3** — the second crop is what makes W8's blight answerable (§0.1: a pressure
  is admitted only once its answer exists).
- **§1.1 Legibility** — a farm says what it grows on its card, its field looks like what it grows, and every sentence that
  names *wheat* today names the farm's own crop.
- **§1.2 Meditative pace** — one choice per farm, kept until changed. **No per-season click**, no rotation to babysit.

## 3. Barley, the good

| | wheat (today) | **barley** |
|---|---|---|
| Good id | `Goods.Wheat = 9` | **`Goods.Barley = 16`** — appended, never renumbered |
| Category | Food | Food |
| Nutrition | 1 | **1** — a raw grain, as wheat (D277); bread stays the only food worth more |
| Stored in | granary, market, the founders' cart | **the same** (the birth gate reads granaries) |
| Crop row | `{ id 1, "wheat", Wheat }` | **`{ id 2, "barley", Barley }`** — in `SimConfig.Crops`, where wheat's row is |
| Starting limit (shipped) | 2000 | **2000** (call 4) |

**Eaten, counted and carried through the doors that already exist** — `GoodsCatalog.EdibleGoods`, `FoodIn`,
`TakeAMealFrom`, `FoodTheVillageHolds`, the marketer's market-stored goods. ⛔ **D283's lesson is the guard here:** D277
converted every *reader* of the food total and missed the *mouth*, and fish was food nobody could eat. So the tests prove
the consumer, not only the count: a household with nothing but barley in its larder **eats it**.

## 4. How a farm is told what to grow — *Grows:* on the farmhouse card

**The workshop's *make* and the smithy's *forge* choice, a third time** (`Workplace.MakeGood`, `Workplace.ForgeGood`):

- **`Workplace.Grows`** — a crop id, **wheat (1) unless the player says otherwise** (`Workplace.DefaultGrows`). Ignored by
  every other workplace. Set only through **`SimWorld.SetGrows(farm, cropId)`**, which refuses an id no crop row defines
  and logs the change.
- **On the farm's card: *Grows: wheat ▾ / barley*.** One crop per farm (call 3). To grow both, build two farms.
- **It takes effect at the next sowing.** A field already sown is reaped as what was sown — the tile carries its crop id
  (D161) and `CropsCatalog.GoodOf` reads the tile, not the farm. So a change in summer changes nothing until spring, and
  the card says so: *"Sows barley from next spring — this year's wheat is still standing."*
- **The sowing writes the farm's choice** (`BehaviorSystem`'s `Sowing` case: `farm.Grows`, where it wrote
  `Crops.TheOne.Id`). `CropsCatalog.TheOne` is then asked by nothing in the sim and is **deleted** (CLAUDE.md: dead code
  goes).
- **Saved** as `grows` on every workplace (a `Workplace` is one shape, as `make_good` is); an older save reads wheat.
  **Hashed sparsely, against the default** — a farm left on wheat mixes nothing, so the control is a provable no-op until
  somebody uses it, and **no golden moves for it existing** (`MakeGood`'s rule, D678).

### 4.1 A farm's limit is its own crop's

Today a farm stops reaping at **one** limit — the farmer row's `limited_by: Wheat`, asked by `SimWorld.MayReap()` with no
farm in the question. With two crops that is wrong both ways: a barley farm would stop when the *wheat* is full and reap
on past a full barley store.

- **`MayReap(farm)`** asks the limit of **the good the farm's standing crop yields** — and for the sowing-cap and the
  seats, the good it grows. `FarmerSeatsWithGroundToWork` and `NextFieldToWork` ask it per farm.
- **The sentences name the crop:** *"Ashford Farm has stopped reaping — you asked the village to keep 2000 barley and it
  has 2004 stored."*; the farmer's work note the same.
- **The farmer row keeps `limited_by: Wheat`** as the answer for anything that asks of the *trade* rather than a farm (the
  stock-limits panel's *"stands the farmers down"* line). ⚠️ To be read against every caller at build time — any that
  means *a farm* moves to the farm's crop.

## 5. What the village does with two crops

| | wheat | barley |
|---|---|---|
| Eaten raw | ✅ | ✅ |
| The mill grinds it, and learns from it (`mill_unlock_wheat`) | ✅ | ⛔ (call 5) — wheat's chain stays wheat's |
| The brewery brews it (W3) | ⛔ | ✅ — W3's spec takes it from here |
| Its own stock limit | ✅ 2000 | ✅ 2000 (call 4) |
| The neighbours trade it (trade, D587: a good joins the day its system lands) | ✅ the lowlands grow 500; the hills and the grazing country want 500 each | ✅ **exactly as wheat** (call 4; ⚠️ the spec read *"500 each"* until D825 — only the lowlands grow wheat) |
| Counts toward the birth gate, the food limit, the obituary's lifetime food | ✅ | ✅ — through `EdibleGoods`, nothing new |

**The herd is untouched**: hay is cut on a pasture; *"wheat's"* in its config comment is the number it borrowed, not the good.

## 6. What the player sees (the view, this slice)

- **The farm's card**: *Grows:* and its choice, the *"from next spring"* line while the field disagrees.
- **The field**: until the art lane's paintings land, a ripe barley field draws its stalks in **barley's own colour**
  (`GoodsPalette`, a paler straw than wheat's gold — the flat fill and stalks every unpainted crop keeps,
  `painted-crops.md §4`), so two fields side by side are told apart in the summer's end without hovering.
- **The farmer**: *"Sowing barley at Ashford Farm"*; a sheaf of barley on the shoulder, as wheat's.
- **The stock limits panel, the Overview's food lines, the stores' cards**: barley as a row, wherever wheat is.
- **The log**, once, on a change: *"Ashford Farm will sow barley from next spring."* *(As built, D825: an Info line in the
  game log — the workshop's `SetMakeGood` shape — not a village-log narration: the player made the change and the card
  already says it.)*
- **The probe**: a `barley:` line — the card's *Grows* row read at a posed farm, and a barley field's ripe colour told
  apart from wheat's.

## 7. The art lane's list (D774) — a request in HANDOFF, not drawn here

**Barley's paintings, in `painted-crops.md`'s shape** — a folder `textures/crops/barley/` with **young, growing and ripe**
(ploughed and stubble are shared, `_ground/`), each 1024² at 128 px a tile, seamless, rows east–west, the hue families
measured as wheat's were. Barley reads apart from wheat by its **long bristled awns, nodding heads and a paler, silvery
straw** when ripe; greener and lower than wheat in its young rows. Until it lands, a barley field keeps the flat fill and
stalks in barley's colour (§6) — never drawn as nothing. Also: barley's colour for the carried sheaf (`PersonPainter`).

## 8. Determinism, saves and goldens

- **Expected: no golden moves for the mechanism.** Every unattended village's farms stay on wheat; `grows` is hashed
  sparsely; a good with zero everywhere mixes nothing (the handcart and hay arrived the same way, D678, D590).
- **The shipped config's two new numbers may move goldens that read it** — barley's starting limit and the neighbours'
  barley — if limits or neighbour stocks are hashed. **Each moved golden is proven by ablation**: the shipped number taken
  back out, the old golden reproduced, then re-taken.
- **Save format**: `grows` added with a default, so an older save loads (its farms grow wheat); the format number bumps
  only if the reader needs it to (D510: a save is read whole or refused).

## 9. Joe's calls — in plain words

**✅ Calls 2–5 as recommended (D823, 2026-11-21, *"I like all as recommended"*). ⏸️ Call 1 reopened by him — §9.1.**

1. ✅ **Superseded by §9.1 (D824): learned when a forager finds it wild.** ~~**Can a new village grow barley from the start, or does it have to learn it?** *Recommended: from the start.*~~ It is a
   field choice, not a new building, and it is the answer to a blight W8 will bring — a gate in front of an answer is a
   pressure with no answer yet. (The alternative: learned by doing, e.g. after some wheat is reaped.)
2. **Does barley give the same harvest as wheat — same yield, same sowing and reaping seasons?** *Recommended: yes, the
   same.* The difference between them is what each one feeds (the mill, the brewery) and, later, which one a blight
   strikes. A weaker barley would only be the worse food until beer exists. (Barley *could* ripen earlier or yield less;
   that would be a second set of numbers to tune.)
3. **One crop per farm, picked on the farm's card, starting from the next spring?** *Recommended: yes.* To grow both,
   build two farms. (The alternatives: half a field of each, painted — a second brush; or a farm that alternates crops
   every year — rotation, which is better kept for W8 if blight wants it.)
4. **Barley's numbers in the shipped game: a starting stock limit of 2000 (wheat's), and the three neighbours growing 500
   each to trade (wheat's)?** *(⚠️ Corrected at the build, D825: only the lowlands grow wheat (500); the hills and the
   grazing country WANT 500 each. "Wheat's numbers" is what was called, so barley is traded exactly as wheat is.)* *Recommended: yes, wheat's numbers.* You tune both in play.
5. **The mill grinds wheat only — no barley flour, and barley does not count toward learning the mill?** *Recommended:
   yes.* Wheat's chain stays wheat's (bread), barley's is beer (W3). That is D821's whole reason for barley.

### 9.1 ✅ Call 1 — barley found wild by a forager (Joe, D823; answered D824, *"Yes, the forager's find"*)

Joe: *"if barley is available from the start, then beer should be available from the start, and bars/pubs, brewers? it
feels like a lot and should be unlocked somehow … is there wild barley? could a forager "discover" barley after X forage
units harvested"*.

- **Barley first does not mean beer first.** Wheat is sown from the founding and the mill is learned after 5,000 wheat
  reaped (D523); W3 can give the brewery the same kind of gate on barley. So the brewery's unlock is W3's call either way.
- **Wild barley is real, and his idea is how barley actually began.** Wild barley grows wild across the Near East, and
  people gathered it for thousands of years before anyone sowed it; it was one of the first crops ever farmed. A forager
  bringing home wild barley and the village learning to sow it is history compressed, and 5,000 wheat to a mill is
  compressed the same way.
- **It matches every learned-by-doing gate the game has**: the smithy after 50 iron, the tailor after 50 leather, the
  quarry after 200 stone, the workshop after 1,500 logs, the mill after 5,000 wheat. Barley: **`barley_unlock_forage`**
  units gathered by foragers, counted where they are gathered (`ForageEverGathered`, `StoneEverDug`'s shape — *as built,
  D825: the count already existed, `FoodEverProducedOf(Produce)`, saved in every save*), a
  *learned* moment (*"A forager has brought home wild barley …"*), and *Lore* shows the road to it.
- **Blight then waits for barley.** §0.1 admits a pressure once its answer exists, so W8 strikes a field only in a village
  that knows barley. That keeps the gate honest.
- **The number is measured, not picked** (D16): how much forage the harness villages gather a year, and the year a
  played village would reach it. Proposed aim: **well before the mill** (Year 14), because beer is contentment's first
  lever — the number is put to Joe with the measurement.
- **Before it is known**, the farm card's *Grows* shows barley greyed: *"Not known yet — a forager may find it growing
  wild (412 of 1,000 gathered)."*
- **Goldens:** the unattended villages forage, so they will learn barley; the learned flag is hashed, so goldens move
  once, each proven by ablation (the gate set out of reach reproduces today's).

*Recommended: the forager's find* (his idea), over barley from the founding. ✅ **Taken (D824).** Shape: a
`BarleyLearned` latch, hashed as a tag byte when set and saved as `barley_learned`; `ForageEverGathered` *(as built:
`FoodEverProducedOf(Produce)`, which only a forager's load writes — D825)* an unhashed,
saved statistic beside it — the workshop's shape exactly (D676/D678), because every village forages from day one and
hashing the counter would move every golden for a difference below the gate that nothing reads. A tech node `barley`
(`TechCondition.ForageGathered`) shows the road in *Lore*; the moment is a learned-by-doing stop (D442).

## 10. How it is built and tested, once the calls are in

Spec first, then the failing tests, then the code (METHODOLOGY §2–§3). Each step a commit, the four checks at the end.

1. **Today pinned** — the farm sows wheat and reaps wheat; a farm's limit; the sentences — so the refactor is proved
   byte-identical (no golden moves through step 3).
2. **The good and the row** — `Goods.Barley`, its `GoodRow`, the crop row; `EveryCropNamesAnEdibleGoodWithAHome` covers it.
   Tests: barley is edible, stored in a granary, **eaten from a larder that holds nothing else** (D283), counted by the
   food total and the birth gate.
3. **The farm's choice** — `Workplace.Grows`, `SetGrows`, the sowing, save and sparse hash; `TheOne` deleted. Tests: a
   farm set to barley sows barley next spring and reaps barley; a change mid-summer leaves the standing wheat wheat;
   an undefined id refused; a save round-trips `grows`; an untouched farm's hash equals today's.
4. **The limit per farm** — `MayReap(farm)`, the seats, the sentences. Tests: a full barley store stops the barley farm
   and not the wheat farm, and the reverse.
5. **The mill stays wheat's** — barley reaped does not move `WheatEverReaped`; the mill never takes barley.
6. **The shipped numbers** (after call 4) — the limit and the neighbours; any golden that moves, proven by ablation.
7. **The view** — the card's *Grows* control, the field's colour, the carried sheaf, the panels; the probe's `barley:`.
8. **The four checks**, every red check counted (D326), the docs in the same commits, then Joe plays it.

**Done** = §10's steps, the four CLAUDE.md checks read, a D-entry per decision, the art lane's request in HANDOFF, and
Joe's play. **W3 (the brewery and the tavern) is the next slice, not this one.**

## 11. ✅ As built (D825)

### 11.1 What is in

- **The good and the row**: `Goods.Barley = 16`, its `GoodRow` (wheat's to the letter), crop row 2 in `SimConfig.Crops`.
  ⚠️ Two modded-good test files held a modder's good at 16 (`ModdedGoodTests`' pitch, `WorkshopTests`' planks) — moved to
  17, the sixth time that remark records.
- **The farm's choice**: `Workplace.Grows` (default `DefaultGrows` = 1, wheat), set only through `SimWorld.SetGrows`, refused
  by `WhyCannotGrow` (no such crop; a crop whose node is not known). Saved as `grows` only off the default; hashed sparsely
  (tag `0xF3`). The sowing reads the farm's choice; **`CropsCatalog.TheOne` is deleted**.
- **The limit per crop** (§4.1): `MayReap(Goods)`; `NextFieldToWork` and `FarmerSeatsWithGroundToWork` ask each tile's own
  crop through a stack-only `ReapLimits` (the limit once per crop per call, nothing kept between calls);
  `GrainHeldBackAt(farm)` names the crop held back in the farm's card and its farmers' note; the quota's farmer arm is
  `EveryFarmIsAtItsLimit` (the smith's shape) and `GoodTheTradeMakes(Farmer)` is the first standing farm's crop. The
  farmer row's `limited_by` now says only *whether* farms are limited.
- **The forager's find** (§9.1): `SimWorld.Foraged` is the one door a forager's load comes through — the food ledger and the
  latch `BarleyLearned` (tag byte `0x88`, saved as `barley_learned` only when true). The count is the existing
  `FoodEverProducedOf(Produce)`, so **every older save carries it**: a village past the gate learns barley at its next
  gather. A learned-by-doing moment, *"A forager found wild barley"*; the Lore node `barley`.
- **⭐ A tech node may unlock a crop** — `TechNodeRow.UnlocksCrop` (`unlocks_crop`, a crop id), validated against the crop
  rows; a node unlocks a building or a crop, never both. `WhyCannotGrow` asks the nodes, not a name: **a crop no node
  unlocks is sown from the founding** (wheat), and a modder's oats behind a node of their own wait the same way. The Lore
  card says *"Known — a farm's card can sow it."* and its bar *"n of 5000 gathered from the wild"*.
- **The view**: the farm card's **Grows:** dropdown (the workshop's Makes: shape), barley greyed *"Barley (not known
  yet)"* with its sentence as the tooltip until the find; the note *"Sows barley from next spring — this year's wheat is
  still in the ground."* while the field disagrees; ripe stalks in the crop's own colour (`GoodsPalette` barley, a paler
  straw); a sheaf of barley carried on the shoulder as wheat's; the farmhouse yard's grain counts both. The probe's
  `barley:` line poses the card (the learned half on a copy read back from a save with `barley_learned` set — the view
  cannot set the latch, and the running village is never touched).
- **The shipped numbers** (call 4): `barley_unlock_forage` 5,000; a starting limit of 2,000; the lowlands grow 500, the
  hills and the grazing country want 500 each — exactly wheat's trade.
- **Not changed, on purpose**: the mill grinds and learns from wheat only (`SimWorld.Reaped` already counted wheat alone —
  call 5 needed no code, only `ReapedBarleyDoesNotTeachTheMill`); the watercolour reads a crop's paintings by its row's
  name, so barley falls back to the flat fill until `textures/crops/barley/` exists — no code waits on the art.

### 11.2 The gate's number, measured (D825, `tools/harness/ZzBarley.cs`)

Forage ever gathered (`FoodEverProducedOf(Produce)`) read off **252 of Joe's saves** (a copy; 225 older-format saves
refused) and **twenty valleys** (seeds 200–219, the shipped config, `ColdStartTests.PlayTheOpening`, thirty years):

| | Year 3 | Year 4 | Year 5 | Year 6 | Year 8 | a year, once a hut stands |
|---|---|---|---|---|---|---|
| his saves, median | 468 | 2,660 | 3,012 | 5,136 | 5,523 | 700–900 |
| the valleys, median | 3,732 | 4,638 | 5,444 | 6,429 | 8,466 | ~1,100–1,500 |

**5,000** reaches the valleys at Year 5 (p10–p90: 5–7; one seed never) and his villages around Year 6–8. 3,000 would come
at Year 3 in the valleys and Year 5 in his. **The mill in his saves is learned at Year 44 and 69** (two villages), so
5,000 forage sits well before it. One valley in twenty and five of his saves past Year 3 never forage: those villages
never learn barley — and since W8's blight strikes only a village that knows barley, nothing is asked of them that they
cannot answer.

### 11.4 The find says what it means here (Joe, 2026-11-21)

Joe, after the build: *"the barley 'discovery' popup should have dynamic text. if the player has not already built a farm
by the time barley is discovered, then the popup should reference that the villagers are happy because now you can build a
farm, plant barley, and brew beer. if the player already has a farm built, it should say the same thing excluding "now you
can build a farm" - instead it should say the villagers are excited for you to plant it at a farm."*

- **No farm** (none standing or marked): *"… one of them has come home with an armful of wild barley. The villagers are
  excited: build a farm, and it can sow barley as well as wheat — choose it on the farm's card, and it goes in the ground
  the next spring."*
- **A farm** (one standing or marked — the player has already chosen where the fields go): *"… The villagers are excited to
  see it sown at Ashford Farm — choose barley on a farm's card …"*; with two or more, *"at the village's farms"*.
- ⚠️ **The beer clause waits for W3**, the one part of his words not built: the brewery does not exist yet, and a moment
  that promises beer sends the player looking for a building that is not there (§1.1). **W3 adds it when the brewery is
  built** — *"… and brew beer from it at a brewery"* — in `SimWorld.WhatTheFindMeansHere`. *"Excited"* rather than *"happy"*:
  the moment is flavour, and contentment (W2) reads no cause from it, so it must not sound like one.

### 11.3 Proved by

`BarleyTests` (20), and the lists that grew by one good (`ModdedGoodTests`, `EdibleGoodsTests`, `WorkshopTests`,
`StartingStockLimitsTests`, `SaveLoadTests.Fields`, `TechTreeTests`' goods unknown on day one). **Red-checked: 18 mutants,
18 red** (D326) — six rewritten to compile after the analysers refused them as first written (unreachable code, an unread
field), and one first scored zero (the quota's every-farm arm) until `TheFarmersStopSentenceNamesTheFarmsOwnCrop` closed it. **Nine goldens moved, each proven by ablation** (D825): the gate
alone moved seven; barley's limit one more; the neighbours' barley the Phase 5 pair. ⛔ **Found by the suite:** `KnowsOf`
called barley known from the founding, so markets set room aside for it — fixed, `BarleyIsUnknownUntilItIsFound`.
