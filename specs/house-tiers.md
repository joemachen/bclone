# Spec: House tiers — Wooden Cabin → Stone Cottage → Insulated Manor, on the firewood axis

**Decisions:** D716 (this document). It builds on D206 (Joe's `TECH-EXAMPLE.md` content pass: the ladder, its fuel
percentages and its gates, in his words), the §5 entry *"HOUSE UPGRADES, AND FIREWOOD IS THE AXIS"* (Joe, 2026-08-23:
*"want each house to use 60–80 firewood per winter"*), D153 (the house holds a capacity, asked of the building), D219
(*"three rows … and no new mechanism"*), D357 (yard modules refused; housing is the brush's), D386–D388 (plots, the lane,
the fence built with the house), D399 (the larder has walls — *"it is the house TIERS' hook"*), D189 / D365 (the burn's
integer grid, and why the last two raises landed where they did), D45 / D407 (cold is a place; a home with any firewood
is a lit hearth), D546–D551 (the bridge: a second row, rebuilt in place while the old one carries traffic), D552–D560
(building condition: wear on a derived clock, the builders mend), D674–D682 (the workshop: a counter, a learned flag
hashed only when true), D709 (every good starts with a limit — firewood at 600), D576 (a stop through `LearnedByDoing`
sets a hashed flag and moved four goldens).
**Status:** ✍️ **SPECCED (D716), EVERY CALL ANSWERED (D717, D718 — 2026-11-07/08) ON `slice/house-tiers`; THE BUILD
STARTS WITH PIECE 1, THE GENERATION (§14).** His answers (§11): one village-wide *Build homes as*, Cabin until changed;
24 / 12 / 5 first, the cabin's raise measured after; **the family tree gates both rungs** (a new generation born into the
valley + the village grown to hold it — cottages ~Year 30, manors ~Year 80–90); every house holds 5; a bigger larder and
its own look each; the three perks *lasts*, *keeps its warmth*, *bears the family's name*; the manor 2×1, drawn bigger.
Every number is a proposal until §10's table is filled.
Owner: Joe + Claude Code.

> **Where this sits.** Phase 5's exit gate, item 1: *"Four stay, and are built in his order: doors commit 5 → clothing →
> logistics → house tiers"* (D656). Doors, clothing and logistics are built, played and merged. **This is the last build
> item on the gate.** After it: the QA checklist (item 3), docs truth (item 4), a played run (item 5).

---

## 1. Goal

A house is a building the village **improves**, not only one it adds. Today every home is the same 2×1 timber house
burning the same 24 firewood a winter for ever. After this slice the village can learn to build in stone, then to build
tight, and **a better house burns less wood** — so the late village spends less of its forest and its hands keeping
warm, and the player can see on the map which streets have been rebuilt.

It is the first upgrade the player feels **at home** rather than at work (§5's own line): everything on the tech-tree map
so far is production.

## 2. What the number actually is today — and the §5 warning is stale

**⚠️ Measured from the config and the hearth, not remembered.** A household burns `firewood_per_winter_day` **3** on every
`firewood_burn_interval_days` **4**th day of winter, counted from the first (`HearthSystem`, D365) — **8 beats × 3 = 24 a
winter**, whether two live in the house or five (`VillageEconomy.FirewoodPerHouseholdPerWinter`).

§5 and §4 still say the 60–80 target is *"a 6–8× change"*. **That was true on 2026-08-23, when the burn was 8–10** (D189).
Joe has raised it twice since — *"2x faster"* (2026-09-01: 10 → 20) and *"+30%"* (D365: 20 → 24). **Against today's 24,
60–80 is a 2.5–3.3× change.** Still not a dial, still measured before typed; but a third the size the warning says.

**What it costs in Joe's own villages** (read off his saves 2026-11-07, `logs_ever_felled` and the homes standing; a
split turns `logs_per_split` 6 into `firewood_per_split` 53, so a winter of 24 is ~2.7 logs a house):

| Village | Year | Homes | Logs felled / year (recent) | Heating, logs / year | Heating's share |
|---|---|---|---|---|---|
| Elderwood-thorn-bay | 33 | 7 | ~82 | ~19 | ~23 % |
| Elderwood-thorn-bay | 77 | 15 | ~102 | ~41 | ~40 % |

**So heating is already a quarter to two-fifths of everything his village fells.** At a cabin of 64 the Year-77 village
would fell ~170 logs a year instead of ~102 (×1.7); at 80, ~197. ⭐ **That is where the tier ladder's teeth are**: on the
forest (§2.3's exhaustion pressure, D42) and the felling hands — not on cold deaths, which D189 measured firewood does not
cause. A cottage village at today's 24 saves ~20 logs a year at 15 homes; at a cabin of 64 it saves ~54.

## 3. What already exists, and is reused — traced, not remembered

| Need | Already there | Where |
|---|---|---|
| A dwelling as a catalogue row with its own capacity, recipe and larder | `BuildingRow.HouseCapacity`, `LocalStoreCap`, `Materials`, `WorkTicks` — *"three rows with three capacities and three recipes, and no new mechanism at all"* | `Building.cs`, `SimConfig` home row, D219 |
| Per-row wear | `WearsInYears` / `FailsInYears` / `RuinedInYears` on every row; the stone bridge does not wear | `Condition.cs`, D552 |
| A second row rebuilt over the first while the first stays in use | `BuildingKind.StoneBridge` + `ConstructionSite.RebuildsBridge`: *"the wooden deck carries traffic until the stone one is finished"*; the wood is not refunded | `bridges.md §5.3` |
| A card sentence for a queued rebuild and its progress | *"Being set in stone — 2nd in the queue, 12 of 36 logs and 0 of 12 stone delivered, 31 % done."* | `Main.Cards.BridgeRebuildSentence`, D704 |
| Learned by doing: a counter, a latched flag hashed only when true, `WhyNotYet`, a tech-map node, *"{n} of {N}"* | the workshop (`LogsEverFelled` → `WorkshopLearned`), the quarry, the mill, the stone bridge | `workshop-and-the-carpenter.md §4.1a`, D676 |
| The house's repair, raised by the season's walk | `RaiseARepairIfNoneFor(BuildingKind.Home, …)` when the house is worn | `SimWorld.Condition.cs` |
| The plot, the lane, the fence, the door | all derived from `HomePosition` + `HomeFacing` + the 2×1 extent, the fence fixed at the marking | `organic-housing.md`, D386–D388 |
| Warmth | a home holding any firewood is a lit hearth; the burn only consumes | `HearthSystem`, D407 |

**What does not exist:** a household does not know *which* dwelling it lives in. `BuildingKind.Home` is asked by name at
23 sim call sites and 13 view call sites (condition, repair recipe, footprint, extent, larder cap, capacity, the
birth gate, the glyph, the cards). ⚠️ **That routing is the slice's real code risk** — §4.6.

## 4. The rules

### 4.1 Three rows, one mechanism

| | Wooden Cabin | Stone Cottage | Insulated Manor |
|---|---|---|---|
| Row | `BuildingKind.Home` (5) — today's house, renamed in words only | `BuildingKind.Cottage` (28) | `BuildingKind.Manor` (29) |
| Extent | 2×1 | **2×1** | **2×1** (drawn taller — §6; a bigger footprint is §11 call 4) |
| Holds | 5 (`max_household_size`) | **5** | **5** (8 at D717, back to 5 at D718 — Joe: *"keep it at 5"*) |
| Firewood a winter | **24** (today's, to the log) | **12** (D206: −50 %) | **5** (D206: −80 % is 4.8) |
| Recipe (proposal) | 30 logs, 0 stone, 30 ticks (today's) | 20 logs, 30 stone, 45 ticks | 30 logs, 45 stone, 60 ticks |
| Wears / fails / ruin (proposal) | 8 / 12 / 20 years (today's) | 16 / 24 / 40 | 16 / 24 / 40 |
| Larder (`local_store_cap`) (proposal) | 400 | **600** | **800** — Joe, D717: each tier bigger than the last |

- ⭐ **Same footprint, recommended.** A bigger manor would move the plot, the lane, the fence, the door and `ChooseSite`'s
  packing at once (D386, D388, D619). The manor is *drawn* as a bigger house on the same ground — a second storey, two
  chimneys (§6). ✅ Joe, D718: *"I agree with your manor footprint/bigger drawing idea."*
- ✅ **Every house holds 5 (D718).** Joe raised the manor to 8 at D717 and withdrew it the next day: *"nevermind on
  increasing the resident volume of a manor. keep it at 5."* So D153's cap and the floor's `max_household_size` budget
  stay true of every house; the density axis stays closed.
- **Larders grow with the tier (Joe, D717).** At `stockpile_target` 95 a family of five wants 475 food (+ the hearth's
  share): a cabin's 400 caps any family of four or more (D399, measured as the largest number that still bites); a
  cottage's **600** holds a full family of five with room; a manor's **800** holds more than a winter for them. So a better house is fewer walks to the granary — the shopping round trip D668 measured at a fifth of every
  adult's ticks. The numbers are proposals for arm B; the validator's *"a cap below a winter's firewood + one armful"*
  refusal applies per row.
- ⛔ **No morale** (D206's manor *"+25 % Morale"*): morale is Phase 6 (D207), and a hidden home-tick bonus is the
  invisible multiplier D357 refused.
- ⛔ **No thatch, planks, cut stone, bricks or mortar.** TECH-EXAMPLE prices the ladder in five goods the game does not
  have. Logs and stone are what exists; a plank good (the carpenter's second *Makes:* row, which W's spec left room for)
  is a later slice, and so are the kiln and the lime burner.

### 4.2 The burn: a number a winter, on the row — and the grid goes away

Today the burn is two global keys whose product, on a 30-day winter, can only land on a few values — the reason D189 got
+25 % for +20 % and D365 got +20 % for +30 %. **The row states what Joe has always stated: firewood a winter.**

- New column on dwelling rows: **`winter_firewood`** (int ≥ 0). The beat stays global (`firewood_burn_interval_days` 4,
  8 beats a winter); on beat *k* of *B* a household burns `⌊(k+1)·W/B⌋ − ⌊k·W/B⌋` of its row's *W*, or what is left.
  Integer, deterministic, evenly spread, and **any W is reachable** — 12, 5, 64, 72, 80.
- ⭐ **For the cabin it is byte-identical**: W 24 over B 8 is 3 on every beat, today's burn to the log, so a village of
  cabins hashes exactly as it does now. A guard pins that identity (§9).
- `firewood_per_winter_day` is retired as a tuning key: the cabin row carries 24. It keeps one job — **0 still means
  "no fuel in this world"** (Phase 0's fixture), checked once at load — or the build replaces that switch with
  `winter_firewood` 0 on the fixture's row. Whichever reads cleaner at build time; both are guarded.
- **Warmth is unchanged.** A home with any firewood is lit (D407). A cottage is not warmer than a cabin; it is cheaper to
  keep warm. *"Cold is a place you are standing"* (D45) is untouched.

### 4.3 The economy: the floor stays the cabin's

- **`VillageEconomy` keeps deriving the survival floor against the Wooden Cabin** — the worst house a village can live in,
  which is the novice-floor shape (D174): nobody is ever worse than today, a better house is headroom. So
  `firewood_per_split`, the woodcutter seats and every derived fuel number **do not move** for this slice.
- **`LabourQuota`'s firewood demand reads each home's own row** (`demand += Σ winter_firewood` over standing homes, where
  it is `homes × FirewoodPerHouseholdPerWinter` today) — so a stone village stops its woodcutters sooner and the saving is
  real hands. Same for the household's own top-up target (`FirewoodStoreWantedPerHousehold` → per household).
- ⚠️ **If call 3 raises the cabin, the floor moves with it** and the derivation is re-run in the order the config's
  header demands (food before fuel). That is the measured commit of §10's arm C, not this slice's mechanism.

### 4.4 The gates — earned, not stone (⏸️ Joe's, D717: cottages around Year 30, manors around Year 80–90)

Joe, on the first draft's *stone laid*: ***"i dont want it to be stone related. it should feel earned in some way. how can
we innovate here? … Maybe two factors: population + ???"*** So each gate below is **two factors: the village has grown to
hold N people, and something it lived through.** Whatever is chosen keeps the workshop's shape (§3): the thing counted
where it happens, a `…Learned` flag latched the tick both hold and hashed only when true, `WhyNotYet`, a tech-map node,
*"{n} of {N}"*, a stop. Once learned, never lost.

**What his played villages say about timing** (read off every year-save he has, 2026-11-07):

| | Year ~30 | Year ~55–60 | Year ~70–77 |
|---|---|---|---|
| People alive | 18–27 | 31–42 | 45–60 |
| Founders | all four alive | the first dies ~Y35–40; the last by ~Y45–49 | gone |
| Town hall | — | raised Y45 (Stonebridge), Y49 (Elderwood) | standing |
| Deaths | — | **every one old age** | **every one old age — not one from cold or hunger, in any save** |

⛔ So *"N winters kept warm"* would be free: nobody in his villages has ever frozen. And a **master builder** is luck of the
founding — Stonebridge's founders brought one on day one, Elderwood's first native master came at Year ~65.

⭐ **And generations come quickly here** — `adult_age` is 12 and the first native child is born in Year 1, so the fastest
line runs roughly: grandchildren of the founders from ~Year 15, great-grandchildren ~Year 30, and a further generation every
~14–16 years after (arithmetic, not yet measured — the sim does not record a villager's generation today).

**The ideas, each two-factor:**

- **⭐ A — the family tree (recommended, both rungs).** *Each kind of house is learned when a new generation is born into
  the valley and the village has grown to hold it.* Cottage: **the founders' first great-grandchild is born** (the fourth
  generation) **and the village holds 25** — by the arithmetic above, Year ~30. Manor: **the Nth generation is born and the
  village holds 45**, N set by §10 so it lands Year 80–90 (by arithmetic the seventh or eighth). The moment reads like the
  game: *"Wyn Ashford is born — the fourth generation in the valley. A village that has buried its grandparents' parents
  here means to stay, and has learned to build in stone."* It is §1's fifth non-negotiable (*generational time is the core
  loop*) made into the house you live in, and it is visible: the villager card says *"fourth generation"*. **New state:** a
  `Generation` byte per villager (founders and arriving nomads 1; a child is one more than the elder of its two parents),
  saved, **not hashed** (the counters' D676 shape — the latch is hashed), so no golden moves for it.
- **B — a house that outlived its builders.** The cottage is learned the first time a cabin is lived in only by people born
  after it was raised — its builders all dead, their children's children still at its hearth — and the village holds 25.
  Diegetic, on one house's card (*"the Ashfords' cabin has sheltered three generations"*), needs no new state. ⚠️ Lands with
  the founders' deaths, ~Year 35–45 — later than Joe's 30.
- **C — the manor out of the cottages themselves.** The manor is learned when **a stone cottage has housed three
  generations of one family** and the village holds 45 — the ladder earned by climbing it; a village that never builds
  cottages never learns manors. Lands ~Year 70–90 if cottages come at 30. Needs the generations of A, kept per house.
- **D — the town hall's jubilee.** The manor is learned when the hall has stood 40 years and the village holds 45 — Year 85
  and 89 in his two villages that have a hall. Civic, simple, no new state; but the hall's own year varies (D252 measured 30
  and 58), so 70–98, and it is the founders' story again rather than the house's.
- **E — a master builder taught by a master.** *The craft passed hand to hand* (§2.1): the manor is learned when a builder
  who learned beside a master becomes a master. The knowledge pillar exactly; ⚠️ needs who-taught-whom recorded, and the
  timing is luck of the founding.

**✅ CHOSEN: A for both rungs (Joe, D718: *"I love the family tree unlock idea!"*).** B–E are kept above as the
reasoning, not built. *(It was: **Recommended: A for the cottage, and A for the manor — one idea, two rungs** (C is the alternative manor if Joe wants the
manor earned by cottages).)* ⛔ `StoneEverLaid`, the first draft's gate, is withdrawn (Joe); the manor's *6 cottages and a
workshop* goes with it.

### 4.5 Who upgrades — the player says what, the sim says where and when

⭐ **The proposal (call 2's recommendation): one village-wide choice on the housing brush — *"Build homes as: Cabin ·
Cottage · Manor"*, each button appearing when learned.**
- **Default: Cabin, and it stays Cabin after learning.** Learning masonry changes nothing in the village by itself; the
  stop says where the choice is (*"…set the housing brush to Cottage to build in stone"*). So the stone spend is always a
  decision the player made (§0.1: the challenge is in the planning), and an unattended village never rebuilds itself.
- **New homes** are built at the chosen tier (that row's recipe, work and wear).
- **Standing homes of a lower tier are rebuilt in place, one at a time, behind every other site in the build queue** —
  a rebuild never delays a granary, a house for a roofless family or a repair. The site carries `RebuildsHome` (the
  bridge's `RebuildsBridge` shape) on the house's own footprint; **the family lives in the old house until the new one is
  finished**, then `HomeKind` flips, `HomeRaisedAtTick` = now, `HomeRepairedAtTick` = 0, `HomeClosed` false. **The fence,
  the door and the plot are kept.** The old timber is not refunded — it is what the stone was set on (bridge, §5.3).
  Nearest-to-the-builder's-hut first, ties by household id: deterministic and legible.
- **A worn or ruined lower-tier home is mended as its new tier** — its repair site becomes the rebuild — so a ruin under
  *Cottage* comes back as a cottage. (A ruined home's family is roofless meanwhile, as today.)
- **Choosing a lower tier again** stops further rebuilds; nothing is pulled back down.
- A choice above what is learned is refused in words; the buttons for it are not shown.

**Rejected, with the reason:**
- *(b) A tier painted per area* (a stone street and a timber street) — the most legible on the map, but a second
  residential layer saved and hashed, a brush mode and an overlay; worth it only if Joe wants mixed villages on purpose.
  It can come later over (a) without undoing it.
- *(c) The sim alone* — learned ⇒ everything rebuilt as stone arrives. Zero clicks, but stone spent that nobody chose,
  against D62's *player ceiling over a derived floor*.
- *(d) A per-house click* (the bridge's) — ⛔ D42 / D357's click farm; there are 7–36 homes, not one or two bridges.

### 4.6 One question asked of the household, everywhere

`Household.HomeKind` (a `BuildingKind`, default `Home`) and **one door**, `SimWorld.HomeRowOf(household)`, replace the 23
sim and 13 view call sites that ask the catalogue for `BuildingKind.Home` by name (condition, the repair recipe, the
footprint and extent, the larder cap, the capacity in the birth gate, the yard count, the glyph, the cards). ⛔ The build
greps for `BuildingKind.Home` before and after and accounts for every hit; a missed site is a cottage that wears like a
cabin or a manor drawn as a cabin. ⚠️ The empty-house swap (a new couple moving into a standing empty home, D381) must
carry `HomeKind` with `HomePosition` — `HouseholdCapacity`'s own remark names that as *"the one place this design can go
wrong"*.

- The validator (D571) that refuses `house_capacity` on any row but the house narrows to **dwelling rows** — a row with
  `house_capacity > 0` must also have `winter_firewood` and the 2×1 house extent, or it is refused by name at load.

### 4.8 Perks beyond the fire and the larder (⏸️ Joe's, D717: *"any other perks we can think of?"*)

| Perk | Cabin | Cottage | Manor | Why it is legible, not a hidden multiplier | Recommended? |
|---|---|---|---|---|---|
| Firewood a winter | 24 | 12 | 5 | the card says it; the woodpile shows it | ✅ settled |
| Larder | 400 | 600 | 800 | fewer walks to the granary, on the map | ✅ Joe's |
| Holds | 5 | 5 | 5 | the family on the card | ✅ Joe's (D718) |
| **Lasts** — wears / fails / ruin | 8 / 12 / 20 y | 16 / 24 / 40 | 16 / 24 / 40 | the condition on the card and the map; fewer repairs | ✅ Joe's (D718) |
| **Keeps its warmth** — days a fireless house shelters before it kills (`exposure_days_sheltered` per row) | 25 | 30 | 40 | *"A cold cottage keeps a family safe 30 days without a fire"* on the card. ⚠️ Insurance, not a daily gain — nobody in Joe's villages has ever frozen — but it is what *insulated* means | ✅ Joe's (D718) |
| **Bears the family's name** — *"Ashford Manor"* on the map, the card and the log; *"the Ashford cottage"* | — | name | name | §1.4, stories from people: the house becomes a place with a history. Derived from the household, never stored | ✅ Joe's (D718) |
| ~~**Keeps the family together**~~ | — | — | — | moot: every house holds 5 (D718) | ⛔ withdrawn |
| A **kitchen garden** in the plot, tended as fallback work (D357's legible cousin) | — | later | later | a person walks out to it | later — its own slice |
| TECH-EXAMPLE's **+25 % morale** | — | — | Phase 6 | when morale exists (D207), as a stated, carded rule | recorded |
| ⛔ Faster work, more births, longer lives from a better house | | | | the invisible multiplier §1.1 / D207 / D357 refuse | refused |

### 4.7 What does not change

`ChooseSite`, the plots, lanes and fences; `max_household_size` (the cabin's and the floor's); the market and
the handcart (a cart is parked at any home); nomads and re-founders (they inherit whatever stands); the destroy brush
(the usual share of *that row's* recipe back).

## 5. The goods

None new. Logs and stone. *(Planks, bricks, mortar — later, §4.1.)*

## 6. The view, and what it says

- **The housing brush's bar**: *Build homes as* with **Cabin** always, **Cottage** and **Manor** as learned, the chosen
  one lit; tooltip *"New homes are built as stone cottages, and the builders rebuild the cabins one at a time when
  nothing else is waiting."*
- **The home card**: the caption names the tier (*"a stone cottage — 10 tiles to work and 3 to the granary…"*; the card
  already says *"a wooden cabin"*); a line *"Burns 12 firewood a winter (a cabin burns 24)."*; while a rebuild is queued,
  the bridge's sentence (D704): *"Being rebuilt in stone — 3rd in the queue, 8 of 20 logs and 30 of 30 stone delivered,
  64 % done."*
- **The map — each tier its own look (Joe, D717: *"each house state will needs its own look/design changes so the player
  can see the upgrade"*).** The cabin as today (timber, the dwelling colour); **the cottage** in stone — grey walls, a slate
  roof, a chimney; **the manor** on the same 2×1 ground but plainly bigger — two storeys drawn, two chimneys, a darker
  roof, its yard fence drawn as a low stone wall. A house being rebuilt shows the new walls rising over the old, as a stone
  bridge's band fills (D704). ⚠️ **Glyphs and colours under the three-band rule**, readable at every zoom the map has;
  `VillageMap.cs` and the glyph files are this slice's until it merges (the art lane waits on it, D699). A probe line draws
  one of each and checks they differ.
- **The villager card**: *"Fourth generation in the valley"* (idea A, §4.4).
- **The tech-tree map**: the two nodes in the fog where `tech-tree-map.md` already draws a stone cottage, each with its
  two factors as *"{n} of {N}"*.
- **The moments**: for idea A, *"Wyn Ashford is born — the fourth generation in the valley. A village that has buried its
  grandparents' parents here means to stay, and has learned to build in stone. The housing brush can build stone cottages
  now — they burn half the firewood and keep a bigger larder."* Through `LearnedByDoing` — ⚠️ D576's trap: that sets the
  hashed `ShownTheTechTree` in a village that had not yet seen the map; §10 checks which goldens cross.
- **A probe line `house tiers:`** — the brush's three buttons and the card's longest rebuild sentence inside their widths.

## 7. Saved, hashed, told

- **Save format 12 → 13.** New: `home_kind` on each household (omitted when the cabin), `homes_built_as` (the choice),
  `cottage_learned`, `manor_learned`, `rebuilds_home` on a site, and whatever the chosen gates count (for idea A,
  `generation` on each villager). Every format-12
  save is refused in words (no migrations before v1). ⏸️ Joe has accepted this before (*"I don't mind starting a new game"*,
  D675); restated in §11 so it is not assumed.
- **Hash — sparse, so a cabin village hashes as today:** `HomeKind` as a tag byte only when it is not the cabin; the
  choice only when it is not *Cabin*; the two learned flags only when true; `RebuildsHome` with the site. What the gates
  count is an **unhashed statistic** (D676's shape: two villages a generation apart hash alike until the flag latches —
  the gap is written where the flag is hashed and asserted by a guard, so nobody "fixes" it unread).
- **Expected goldens: none move — unless a golden run crosses the cottage's number**, which latches the flag (and the
  tech-map flag, D576). The Phase 5 posed village raises a smithy, quarry, mine, mill, bakery, post and barn in twenty
  years; whether that crosses is read at build time, and if it does each moved golden is proved by switching the crossing
  alone off and re-pinned with its cause (D682's method).
- **Log**: the rebuild's start and finish (*"The Ashfords' cabin is rebuilt in stone — the stone cottage burns half the
  wood"*), INFO; the burn per beat stays DEBUG.

## 8. Edge cases and failure modes

| Case | Rule |
|---|---|
| A rebuild queued and the family dies out | The site stays; the next couple to move in gets a cottage (or the site is cancelled with the house — whichever the empty-house swap already does with a repair site; read it at build time) |
| The house is demolished with a rebuild queued | The site goes with it, delivered materials refunded as a cancelled site's are |
| The stores never hold the stone | The site waits for deliveries like any site; the card says what it waits on. Nothing is half-built and the family stays housed |
| Choice set to Manor while some homes are still cabins | Rebuild straight to manor — never cabin → cottage → manor (two lots of stone for one house) |
| The firewood limit (600, D709) under a raised cabin | ⚠️ At 64 a 15-home village burns 960 a winter: **the starting limit would stop the woodcutters below one winter.** If call 3 raises the cabin, the limit is re-measured in the same commit |
| A cottage runs out of firewood | Exactly as a cabin: cold on the next beat; the warning line names the house |
| Re-founding inherits cottages | Fine: `HomeKind` is the house's, it stays with the house |
| A modded dwelling row | Allowed if it is a full dwelling row (§4.6); it appears on the brush's bar only if a learned rule names it — or never, recorded rather than built |

## 9. How it is tested — `tests/Bclone.Sim.Tests/HouseTierTests.cs` (TDD; every guard red-checked and its reds counted)

1. **A cabin burns today's firewood to the log** — the spread formula at W 24, B 8 is 3 every beat; a cabin village's
   hash over a winter equals today's (the identity guard that keeps the goldens honest).
2. A cottage burns 12 and a manor 5 over a whole winter; no beat burns more than ⌈W/B⌉; the last log burns (D407).
3. Warmth: a cottage with 1 firewood is a lit hearth; with 0 it is cold, as a cabin.
4. The cottage's two factors (for idea A: the generation born and the village's size) — the flag latches only when
   **both** hold, not a tick before either; `WhyNotYet` and `HowFar` say both numbers. (Idea A: a child's `Generation` is
   one more than the elder parent's; founders and arriving nomads are 1.)
5. The manor's two factors, the same way; and the cottage is learned first.
6. **Default Cabin: a village that has learned masonry and not been told rebuilds nothing** (the unattended guard).
7. *Cottage* chosen: the next new home is a cottage; a standing cabin is rebuilt; the family is housed the whole time;
   `HomeKind`, raised-at and repaired-at are set at the finish; the fence tiles are unchanged.
8. A rebuild is behind every other site: a granary marked after it is built first.
9. A worn cabin under *Cottage* gets a rebuild site, not a repair site; a ruin comes back a cottage.
10. Choosing *Manor* over a cabin goes straight to manor.
11. `LabourQuota`'s firewood demand is the sum of the rows; a village of cottages wants half the woodcutting.
12. **The survival floor is the cabin's** — every derived fuel number is unchanged with cottages in the catalogue.
13. Every `BuildingKind.Home` site reads `HomeRowOf` — posed as a cottage and asked condition (wears at 16), repair
    recipe, footprint, capacity, larder.
14. The empty-house swap carries `HomeKind`.
15. Save → load → hash equals live, with cottages, a manor, a queued rebuild; a format-12 save is refused in words.
16. Hash sparsity: a cabin village hashes as before (the goldens' own guard); the counter's gap is asserted.
17. Validator: a dwelling row without `winter_firewood` is refused by name; a non-dwelling with `house_capacity` still is.
18. Probe `house tiers:` (view).

## 10. Measured before typed

On `tools/harness/ZzHouseTiers.cs` (new, beside `ZzLogistics`), fifty years, the shipped seeds plus the played-shape
arms (an unattended valley plays badly — pair every table with Joe's saves before reading a loss as the design):

| Arm | What it answers |
|---|---|
| **A — control** (main's code) | the baseline: alive at 10 / 50, cold deaths, logs felled, heating's share, woodcutter seat-years, firewood held |
| **B — cottages chosen** when learned (cabin 24, cottage 12, manor 5; larders 400 / 600 / 800; every house holds 5) | **the year each gate's factors cross** (for A: the first birth of each generation, and the year the village holds 25 and 45 — so the two gates land at ~Year 30 and ~80–90); stone and builder-time the rebuilds cost; logs and woodcutting saved; granary walks saved by the larders; does a rebuild ever delay a roofless family |
| **C — the cabin raised** to 48 / 64 / 80, re-derived (call 3) | the founding: cold deaths Years 1–10, alive at 10; logs felled ×?; forest cover at 50; does the 600 firewood limit bind; is winter still *"a rhythm, not a crisis"* (§0) |
| **D — C with cottages** | how much of C's cost the ladder gives back, and by when |

And Joe's saves, read not re-simulated: homes and felling by year (§2's table, all of them), so the cottage's year
lands in *his* villages, not only the harness's.

## 11. Joe's calls

**Answered (D717, 2026-11-07):**
- ✅ **Call 2 — the player chooses once, the sim does the rest** (§4.5). Joe: *"agree"*.
- ✅ **Call 3 — two steps**: 24 / 12 / 5 in this slice, the cabin's raise toward 60–80 its own measured commit (§10 arm C).
  Joe: *"sure sounds good i suppose"*.
- ~~✅ **The manor holds 8** (§4.1). Joe: *"insulated manor should also allow up to 8 people to live in one home."*~~ Withdrawn by him at D718.
- ✅ **Larders grow with the tier** (§4.1, proposal 400 / 600 / 800). Joe: *"stone cottage should have a bigger larder than
  wooden cabin. and manor should have a larger larder"*.
- ✅ **Each tier its own look** (§6). Joe: *"each house state will needs its own look/design changes so the player can see
  the upgrade."*
- ⛔ **The first draft's gates are withdrawn** — *"i dont want it to be stone related. it should feel earned"*.

- ✅ **Call 1 — the gates: the family tree, both rungs** (§4.4 idea A). Joe, D718: *"I love the family tree unlock idea!"*
  The generation numbers and the populations (proposed 4th + 25 and the ~Year-80–90 generation + 45) are set by §10's arm
  B before they are typed.
- ✅ **Every house holds 5** (D718) — the manor's 8 withdrawn: *"keep it at 5"*.
- ✅ **Call 4 — the perks: lasts, keeps its warmth, bears the family's name** (§4.8). Joe, D718: *"go with your 3 perks!"*
- ✅ **Call 5 — the manor 2×1, drawn bigger.** Joe, D718: *"I agree with your manor footprint/bigger drawing idea."*

**The costs, unchanged as proposals** (measured in arm B): cabin 30 logs; cottage 20 logs + 30 stone, 45 ticks; manor
30 logs + 45 stone, 60 ticks. *(Stone stays in the price — it is what a stone house is made of; the gate is what is no
longer stone.)* And save format 13 refuses his format-12 saves, as before.

## 12. Acceptance — house tiers are done when

- [ ] Joe's three calls answered and written into this spec.
- [ ] §10's arms A and B filled before a number is typed into `data/sim.config.json`; C and D if call 3 says so.
- [ ] §9's guards green, each red-checked and its reds counted (mutants copied aside and restored by copy).
- [ ] The four CLAUDE.md checks: the suite (~16–17 min), the view build at 0 warnings, the probe (bar 151, tile centres ✅,
      new game ✅, `house tiers:` ✅, `done.`), the goldens `git diff` (every mover proved and named).
- [ ] Joe plays it — learns masonry, chooses *Cottage*, watches a street rebuilt — and says merge.
- [ ] DESIGN §4 / §5 / §6 / §7, `tech-tree.md §9.6`, `tech-tree-map.md`, `building-condition.md`, `buildings-catalog.md`,
      `save-load.md`, `content-inventory.md` and HANDOFF moved in the same commits.

## 13. Definition of Done

METHODOLOGY §3's six, with item 4 (the manual QA) as Joe's play above, and Phase 5 gate item 1 closed by it.

## 14. The build, in pieces (D718)

One commit each, docs in the same commit, every guard red-checked and counted; the four checks before each is called done.

1. **The generation** — `Villager.Generation`: founders and arriving nomads 1, a child one more than the elder of its two
   parents; saved (format 13), unhashed; the villager card's *"Nth generation in the valley"*. Then §10's arm B reads, on
   the harness, the year each generation is first born and the year the village first holds 25 and 45 — **the numbers
   for both gates come from that table, not from the arithmetic in §4.4.**
2. **The rows and the burn** — the Cottage and Manor rows, `winter_firewood` spread over the beat (byte-identical for the
   cabin), `Household.HomeKind` behind `HomeRowOf` (every `BuildingKind.Home` site accounted for), per-row larder, wear and
   `exposure_days_sheltered`, `LabourQuota` per home.
3. **The gates and the choice** — the two latched flags on the family tree, `WhyNotYet`, the tech-map nodes, the moments;
   *Build homes as* on the housing brush; new homes at the chosen tier; `RebuildsHome`, one at a time behind every site.
4. **The view** — each tier's look, the cards (tier, burn, warmth, rebuild progress, the family's name), the probe line.

