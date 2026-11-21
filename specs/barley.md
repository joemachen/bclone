# Spec: A second crop — barley (W7)

> Status: **📝 SPECCED (D822, 2026-11-21) on `slice/w7-barley`; ✅ CALLS 2–5 AS RECOMMENDED (D823, *"I like all as
> recommended"*); ⏸️ CALL 1 REOPENED BY JOE — barley learned from the wild, by foraging (§9.1). Nothing built.**
> *(Read "⏸️ five calls Joe's" at D822.)* *(Read "🔨 claimed; the spec is being written" at the claim commit.)*
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
| The neighbours grow it (trade, D587: a good joins the day its system lands) | ✅ 500 each | ✅ **500 each, as wheat** (call 4) |
| Counts toward the birth gate, the food limit, the obituary's lifetime food | ✅ | ✅ — through `EdibleGoods`, nothing new |

**The herd is untouched**: hay is cut on a pasture; *"wheat's"* in its config comment is the number it borrowed, not the good.

## 6. What the player sees (the view, this slice)

- **The farm's card**: *Grows:* and its choice, the *"from next spring"* line while the field disagrees.
- **The field**: until the art lane's paintings land, a ripe barley field draws its stalks in **barley's own colour**
  (`GoodsPalette`, a paler straw than wheat's gold — the flat fill and stalks every unpainted crop keeps,
  `painted-crops.md §4`), so two fields side by side are told apart in the summer's end without hovering.
- **The farmer**: *"Sowing barley at Ashford Farm"*; a sheaf of barley on the shoulder, as wheat's.
- **The stock limits panel, the Overview's food lines, the stores' cards**: barley as a row, wherever wheat is.
- **The log**, once, on a change: *"Ashford Farm will sow barley from next spring."*
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

1. **Can a new village grow barley from the start, or does it have to learn it?** *Recommended: from the start.* It is a
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
   each to trade (wheat's)?** *Recommended: yes, wheat's numbers.* You tune both in play.
5. **The mill grinds wheat only — no barley flour, and barley does not count toward learning the mill?** *Recommended:
   yes.* Wheat's chain stays wheat's (bread), barley's is beer (W3). That is D821's whole reason for barley.

### 9.1 ⏸️ Call 1, reopened — barley found wild by a forager (Joe, D823)

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
  units gathered by foragers, counted where they are gathered (`ForageEverGathered`, `StoneEverDug`'s shape), a
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

*Recommended now: the forager's find* (his idea), over barley from the founding.

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
