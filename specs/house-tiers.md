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
**Status:** ✍️ **SPECCED (D716, 2026-11-07) ON `slice/house-tiers` — NOTHING BUILT. Joe's three calls are in §11, each
with a recommendation; every number in this document is a proposal until §10's table is filled on the harness.**
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
| Extent | 2×1 | **2×1** | **2×1** |
| Holds | 5 (`max_household_size`) | **5** | **5** |
| Firewood a winter | **24** (today's, to the log) | **12** (D206: −50 %) | **5** (D206: −80 % is 4.8) |
| Recipe (proposal) | 30 logs, 0 stone, 30 ticks (today's) | 20 logs, 30 stone, 45 ticks | 30 logs, 45 stone, 60 ticks |
| Wears / fails / ruin (proposal) | 8 / 12 / 20 years (today's) | 16 / 24 / 40 | 16 / 24 / 40 |
| Larder (`local_store_cap`) | 400 | 400 | 400 |

- ⭐ **Same footprint, on purpose.** A bigger manor would move the plot, the lane, the fence, the door and `ChooseSite`'s
  packing at once (D386, D388, D619). The ladder is the *fuel* axis D206 named; a bigger house is the *density* axis
  D153 reserved, and it is a different decision (§11, call 1c).
- ⭐ **Same capacity, on purpose.** D153 measured that at seven the cap was scenery and at five it binds — the village
  stopped dying out. TECH-EXAMPLE's 4 / 5 / 8 would move the birth gate in exactly the regime that measurement says is
  load-bearing, and `SpareHandsAt` budgets against `max_household_size`, not the row. **Not in this slice.**
- **Larder unchanged.** A cottage's reserved hearth share is 12 where a cabin's is 24, so its family already keeps 12
  more food at home for free — *the hook D399 named*, without a new number.
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

### 4.4 The gates — learned by doing, latched, never lost

Both follow the workshop's shape exactly (`workshop-and-the-carpenter.md §4.1a`): a counter where the thing happens, a
`…Learned` flag set the tick it crosses, **hashed as a tag byte only when true** (no golden moves below the number), a
`WhyNotYet` arm, `TechCondition` + a node on the tech-tree map, `HowFar`'s *"{n} of {N}"*, a stop through
`LearnedByDoing`. Once learned, never lost — every building unlock in the game latches.

- **Masonry → the Stone Cottage: stone *laid*.** A new counter, `StoneEverLaid`: the stone in every site the builders
  finish (the recipe's stone, counted at completion; repairs and refunds do not count). *"The village has laid 412 of 600
  stone in its buildings."* Masonry is laying stone; the counter rises with what the player chooses to build in it.
  - ⛔ **Not `StoneEverDug`**, which counts only stone dug *by hand* and stops at the quarry (Joe's saves: 192–612 for
    life, flat from the quarry on). There is no quarried-stone counter today either.
  - **Proposal: `cottage_unlock_stone_laid` — a number set by §10 so a played village learns it around Year 20–25**,
    after the quarry and the workshop and before the town hall (30–60). ⏸️ The year is Joe's (call 1a).
- **Joinery → the Insulated Manor: stone cottages raised, with a workshop standing.** `CottagesEverRaised` crosses
  `manor_unlock_cottages` while a workshop stands (the carpenter's trade, D678). Masonry *then* joinery, in that order by
  construction: no manor before the village has rebuilt in stone. *"The village has raised 4 of 6 stone cottages; a
  joiner's workshop would teach it to build them tight."*
  - ⚠️ **Not a master carpenter** (the PEOPLE mechanism `tech-tree.md §9.6` names) — **too late and too uncertain,
    measured in Joe's saves** (mastery is `mastery_years` 20 of held seat, 960,000 skill work): Elderwood's workshop
    stood from ~Year 15 and its one master carpenter, Rantis, mastered **between Year 53 and 61** and died at 77, leaving
    the best living carpenter at 48 %; Stonebridge has **none by Year 71** (best 64 %); his Year-27–39 villages sit at
    14–17 %. D46's reshuffle and a trade staffed only while carts are wanted are why. It would make the manor a Year-55-or-
    never building — a legitimate *generational* choice, and offered as the alternative in §11, not the recommendation.
  - ⛔ **Not handcarts made**: 9 by Year 39 in Hollowmere, 98 by Year 77 in Elderwood — it measures how many households
    wore out carts, not joinery, and spreads one gate over forty years of villages.
  - **Proposal: `manor_unlock_cottages` 6**, so the manor lands a decade or so after the cottage. ⏸️ Joe's (call 1a).

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

### 4.7 What does not change

`ChooseSite`, the plots, lanes and fences; `max_household_size`; the birth gate's numbers; the larder; the market and
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
- **The map**: a cottage and a manor are drawn differently from a cabin (a stone wall colour; a manor's darker roof) —
  ⚠️ **glyphs and colours under the three-band rule**, and `VillageMap.cs` / the glyph files are this slice's until it
  merges (the art lane's table waits on it).
- **The tech-tree map**: *masonry* and *joinery* nodes in the fog where `tech-tree-map.md` already draws
  *"Mason's yard ··· ──► Stone cottage ···"*.
- **The moments**: *"The village has laid 600 stone in its buildings, and somebody has worked out how to raise a house of
  it. The housing brush can build stone cottages now — they burn half the firewood."* Through `LearnedByDoing` — ⚠️ D576's
  trap: that sets the hashed `ShownTheTechTree` in a village that had not yet seen the map; §10 checks which goldens cross.
- **A probe line `house tiers:`** — the brush's three buttons and the card's longest rebuild sentence inside their widths.

## 7. Saved, hashed, told

- **Save format 12 → 13.** New: `home_kind` on each household (omitted when the cabin), `homes_built_as` (the choice),
  `stone_ever_laid`, `cottages_ever_raised`, `cottage_learned`, `manor_learned`, `rebuilds_home` on a site. Every format-12
  save is refused in words (no migrations before v1). ⏸️ Joe has accepted this before (*"I don't mind starting a new game"*,
  D675); restated in §11 so it is not assumed.
- **Hash — sparse, so a cabin village hashes as today:** `HomeKind` as a tag byte only when it is not the cabin; the
  choice only when it is not *Cabin*; the two learned flags only when true; `RebuildsHome` with the site. The two counters
  are **unhashed statistics** (D676's shape: two villages at 100 and 590 stone laid hash alike and diverge later — the gap
  is written where the flag is hashed and asserted by a guard, so nobody "fixes" it unread).
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
4. `StoneEverLaid` counts a finished site's stone, not a repair's, not a refund's; the flag latches at the number and
   not one stone before; `WhyNotYet` and `HowFar` say the numbers.
5. The manor waits for both: cottages raised **and** a workshop standing.
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
| **B — cottages chosen** when learned (cabin 24, cottage 12, manor 5) | when `StoneEverLaid` crosses 300 / 600 / 1,000 (→ the number for Year 20–25); stone and builder-time the rebuilds cost; logs and woodcutting saved; does a rebuild ever delay a roofless family |
| **C — the cabin raised** to 48 / 64 / 80, re-derived (call 3) | the founding: cold deaths Years 1–10, alive at 10; logs felled ×?; forest cover at 50; does the 600 firewood limit bind; is winter still *"a rhythm, not a crisis"* (§0) |
| **D — C with cottages** | how much of C's cost the ladder gives back, and by when |

And Joe's saves, read not re-simulated: homes and felling by year (§2's table, all of them), so the cottage's year
lands in *his* villages, not only the harness's.

## 11. Joe's calls — each with a recommendation

**Call 1 — the tiers' costs and gates.**
- *(a) The gates.* **Recommended:** cottage learned by **stone laid** in the village's buildings, the number set so a
  played village learns it **around Year 20–25**; manor learned by **6 stone cottages raised with a workshop standing**,
  about a decade later. *Alternative for the manor:* the first **master carpenter** (D206's own words, *"what a mason
  knows once he has mastered the trade"*) — in his saves that is Year ~55 in Elderwood and not yet by Year 71 in
  Stonebridge, so the manor becomes a late, generational house rather than the ladder's next rung. *(Not carts made — 9 to
  98 by the same age.)* **Joe: is Year 20–25 for the cottage, then ~a decade for the manor, the right rhythm?**
- *(b) The costs.* **Recommended (proposals, measured in arm B):** cabin as today (30 logs); cottage **20 logs + 30
  stone**, 45 ticks; manor **30 logs + 45 stone**, 60 ticks; both stone rows wear **twice as slowly** (16 / 24 / 40). No new
  goods — thatch, planks, bricks and mortar are later.
- *(c) Capacity and size.* **Recommended: all three hold 5 and stand 2×1.** The ladder is the fuel axis; a bigger manor is
  the density axis (D153), which moves the birth gate and the plots and wants its own measurement. (TECH-EXAMPLE's 4 / 5 /
  8 and the manor's morale are left out.)

**Call 2 — the brush's, or the sim's.** **Recommended: the player chooses once, the sim does the rest** (§4.5) — a
village-wide *Build homes as: Cabin · Cottage · Manor* on the housing brush, **Cabin until he changes it**; new homes at
that tier; standing ones rebuilt in place one at a time behind all other work, the family at home throughout; a worn one
mended as the new tier. *Alternatives:* a tier painted per area (more map, more machinery), or the sim rebuilding on its own
once learned (no decision, stone spent unasked).

**Call 3 — the firewood number.** **Recommended: two steps.** **First** this slice ships the ladder on today's burn — cabin
**24**, cottage **12**, manor **5**, any number now typeable (§4.2) — so nothing moves for a village that never builds in
stone and the mechanism is played on its own. **Then** the cabin's raise toward his 60–80 is its own measured commit, from
§10's arm C (48 / 64 / 80) with the founding, the forest and the firewood limit in the table; ⭐ the honest lean is that the
ladder only *matters* if the cabin is heavier than today — at 24 a stone village saves ~20 logs a year at 15 homes, at 64 it
saves ~54 — so the raise is probably right, and the table says how far. *Alternative:* raise the cabin in this slice and
measure everything once (every golden moves; the founding is re-tuned in the same branch).

**And a default, restated rather than assumed:** save format 13 refuses his format-12 saves (Elderwood, Stonebridge…), as
before.

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
