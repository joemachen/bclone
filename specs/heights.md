# Spec: Real heights — hills that slow the walk, in the one cost field

**Decisions:** D734 (Joe's first call: height changes walking, *uphill is slower*), D735 (this spec; his
calls 1–4). Neighbours: D2 (integer costs), D15/§2.6 (one shared cost field), D179 (the sweep), D358 (worn
paths in the field), D383 (buildings as obstacles), D404 (fences), D473 (a stage per stream), D477 (height
parked *after the shell*, a stage and a row of its own), D546 (bridge decks), D641 (Phase 5's exit gate),
D666 (Joe's balance bar), D689 (a cart is slow on grass), A57 (the art lane's relief waits on this).
**Status:** 🔨 **BEING BUILT on `slice/heights` at Joe's word (*"Build it"*, 2026-11-12), §12's four steps — step 1
✅ (the layer, the stage, the slope classes); step 2 ✅ (the slope in the one cost field, the goldens re-taken); step 3 ✅
(saves, the Hilliness row, *What's here*, *climbing*, the wash); step 4 ✅ measured (§8.1) — the numbers stand.
🔁 **Then smoothed (D737, 2026-11-13; Joe, playing Hilly: *"things look a little weird"*), §5.1: 256 fine levels, the
ground smoothed before rounding, steepness land to land; re-measured (§8.2). ⏸️ UNPLAYED (the smoothed build), UNMERGED:
Joe plays a Rolling valley and a Hilly one (§11), then merges at his word.**
Heights is not on Phase 5's exit gate (D641); building it goes past the gate, and that is his to say.

---

## 1. Why this exists

Joe, 2026-11-11, on the art lane: *"i do want to add in real heights in the simulation."* Asked what
height should *do*, he picked: *"Uphill is slower - i 100% agree. not 100% slower for clarification"*
(D734). Then, of the questions below: *"Any slope and as you recommend"* (D735).

Today the valley is flat. The river runs along the bottom of nothing. A hill would make the map read as a
place (Non-Negotiable 1, legibility), and a slow climb is a cost the player can see and reason about:
build the store on the near side of the hill.

## 2. Joe's calls (2026-11-12)

| # | The question | His answer |
|---|---|---|
| 1 | Does height do anything beyond the look? | **Yes: it slows walking** (D734). Not looks only. |
| 2 | Only climbing slow, or any slope? | **Any slope**, up or down. A trip costs the same both ways, as every trip does today. |
| 3 | How slow is the steepest ground? | **About half again as long (×1.5)**, a little extra on gentle slopes, flat unchanged. Worn paths still help on top. Measured on his saves before the number is final. |
| 4 | What shape is a valley? | **A bowl down to the river plus rolling hills**, from the seed, never changing in play. |
| 5 | A row on the new-game screen? | **Yes: *Hilliness — Gentle / Rolling / Hilly*, default *Rolling*** (D477's place for it). |
| 6 | Now, or after Phase 5's gate? | **Spec now; build at his word.** |

⛔ **Out unless he asks:** building on slopes (level ground, levelling as work), farming worse on a slope,
snow lying longer up high, terraforming. Each would be its own call.

## 3. What a height is

- **A height per tile: a small integer level, `0` at the river's water up to a ceiling** (`height_levels` in
  `data/`: **256** since D737; 16 until then, too coarse for a smooth hill — §5.1). Integers only (D2): the generator
  never touches a float in anything the sim reads.
- **A pure function of the seed, the generator and the Hilliness row.** It never changes in play. No
  system writes it after generation.
- **Kept on `GeneratedMap` beside the terrain** (`HeightAt(tile)`), and **saved with the map.** That way a
  save never depends on the generator staying the same (§9, the old saves).
- **The slope class of a tile is derived once, at generation**, and kept beside it (CLAUDE.md: kept where
  the state changes, and it never changes):
  - **steepness** = the largest height difference between the tile and any of its four neighbours **on land**
    (D737: nobody walks into the river, so the drop from a bank to the water is not a slope; water's steepness is 0);
  - **flat** below 4, **gentle** at 4, **steep** at 8 or more (`slope_gentle_at`, `slope_steep_at` in `data/`; 1 and 2
    of 16 levels until D737).

  Three classes, not a continuous number, so it is legible: *What's here* can say *steep ground — slow to
  cross*, and a player can learn the three by sight.

## 4. What it costs to walk — the one cost field (§2.6)

- **A slope raises a tile's entry cost,** the same number worn paths lower, so movement, catchment,
  routing, markets and the economy keep reading **one field and nothing else**:

  | Ground | Entry cost (base 10) | `data/` key |
  |---|---|---|
  | flat | ×1.0 → 10 | — |
  | gentle | ×1.2 → 12 | `heights.gentle_cost_percent: 120` |
  | steep | ×1.5 → 15 | `heights.steep_cost_percent: 150` |

- **Worn ground and a slope multiply**: a worn path (9) on steep ground costs 9 × 150 % = 13.5, rounded to
  the nearest whole step (half up) = 14. A packed path on the flat is unchanged.
- **A bridge's deck ignores the slope.** It is a level deck (D546), and the river's banks are the bowl's
  bottom anyway.
- **Any slope, both ways (Joe's call 2).** The cost is the tile's, not the direction of the step, so the
  field stays one Dijkstra per destination and A→B costs what B→A does (to within the end tiles, exactly as
  worn ground already is).
- **The walk is slower, not only the route.** `BehaviorSystem` already times a leg by its tile's entry cost
  against `BaseTileCost` (`LegTicks = distance × entry / 10`), so a villager on steep ground walks at two
  thirds of their flat pace, visibly. No new code reads the slope.

### 4.1 What the field has to change to carry it

1. **The bucket ring is sized to the largest step, not to `BaseTileCost`.** Dial's algorithm needs
   `maxStep + 1` buckets; today `maxStep` is pinned at `BaseTileCost` (10) and a step above it throws.
   Steep ground's 15 makes it 16 buckets. The throw stays, with the bound raised to the configured ceiling.
2. **Uniform ground is no longer uniform**: D179's breadth-first sweep is exact only when every tile costs
   the same. On a valley with any slope the field takes the ring every time. ⚠️ **This is the performance
   risk**, and it is measured before and after (§8): D179 was six minutes against eleven.
3. **`== BaseTileCost` means "costs 10", not "unworn grass".** Five places in `BehaviorSystem` (lines
   ~4238, 4440, 4463, 4646, 4686 today) ask *"is this grass?"* that way: the cart's slow-on-grass rule
   (D689) and the route's grass count. On a slope an unworn tile costs 12 or 15, and every one of them would
   quietly stop treating it as grass. They ask the wear class instead (`PathWear.ClassAt == 0`) through
   one new `TravelCostField.IsUnworn(tile)`, so the slope and the wear stay two facts.
4. **`ReadWearFrom`'s and the deck's bound** (`≤ BaseTileCost`) stay as they are: worn costs and decks are
   still at most 10. Only the slope may push a step above it.

## 5. How a valley is shaped — a new generator stage

- **Stage 6, *Heights*** (`seeded-map-generation.md §13.2`: a new stage appends a new id and never
  renumbers). Its own stream, so **no other stage's valley moves**: the river, the founding, the seams and
  the woods are where they were on the same seed. Only the walking changes.
- It runs **after the river** and reads it:
  - **The bowl.** A tile's base height rises with its distance from the nearest river tile, to a cap.
  - **The hills.** Integer value noise on a coarse lattice (one point every 12 tiles), smoothed between
    lattice points with integer weights, added to the bowl. *Hilliness* scales the noise: *Gentle* ½,
    *Rolling* 1, *Hilly* 2.
  - **Water is level 0, and the banks are low.** *(D735 said "at most level 1" and clamped the bank alone, which
    left a cliff behind it — §5.1.)* The ground rises no more than 12 a tile from the water and is smoothed after, so
    the river runs along the bottom with no cliff near it, and a bank is never steep (steepness is land to land).
- **Kept liveable** (`seeded-map-generation.md §10.4`: every seed survivable, none equally comfortable):
  - **the founding site and its ring** (the tiles the founders' first buildings take) are **smoothed to
    flat or gentle**, so no founding starts on a cliff;
  - **the passable valley stays connected.** Height never makes ground impassable: steep is slow, not a
    wall. Rock and water stay the only walls.
- **The Hilliness row on the new-game screen** (`new-game-screen.md`: D477's parked line, built here).
  Three choices, default *Rolling*, saved with the game and shown on the pause screen's *About this run*
  like the other rows.

### 5.1 Smoothed (D737, 2026-11-13)

Joe, playing *Hilly*: *"things look a little weird"*. A picture of the shading (the probe cannot judge a look) showed
why: straight lines between lattice points made every 10 × 10 cell a flat-faced panel with a crease at its edge; whole
levels of 16 made smooth ground a staircase (hatching in the wash); a square founding ring read as a hard-edged plateau;
and the bank, clamped to 1 with the hill a tile behind it left standing, put a **cliff of 7 levels behind the river**
that the bank guard said was fine. Built now:
- the ground is composed in thousandths of a level — the bowl rising `height_bowl_rise_per_tile` 2 a tile to a rim of
  64, the hills (0–130 at 100 %) faded in **gently** (by the square of the distance) over `height_river_ramp_tiles` 10,
  and never rising more than `height_rise_from_water_per_tile` 12 a tile from the water — then **smoothed**, two box
  passes across half a lattice cell each way, and only then rounded to 256 levels;
- the founding ring is a **circle**, flattened before the smoothing and wider by the smoothing's reach, so the ring
  itself stays flat or gentle;
- steepness is counted **land to land** (§3), so a riverbank is not steep and a riverside path not slow;
- ⭐⭐ **drawn as the first build's soft wash, the heights grouped in steps of 8 for drawing only (D740)** — Joe, on the contours: *"oh, no i hate that. i liked the very first iteration way more"*; shown his own valleys in that style at steps of 8 and 6, *"Yes, build 2"*. The sim keeps its smooth 256 levels; the view groups them into steps of 8, softens a tile, and shades them — at the first build's 0.16 a step until D741, then **0.26**, matched by measurement: the first build's wash visibly covered 24 % of Hollowmere's land, 0.16 on the smoothed hills 9 % of Millbrook's (Joe: *"i dont detect any hills on any setting"*), 0.26 about 29 % — 8 texels a tile (~90 ms once per valley). The broad light and dark patches he liked came from the first build's coarse steps; now they follow smooth hills, with no panels and no cliff. *(Superseded, kept:)*
- ~~**drawn as contour lines and a faint shade (D739)**~~ — Joe, on the shade alone: *"it just looks like dark spots"*; shown his own valley three ways, he picked *"C: contours + shade"*. Thin ink lines every 16 levels, bolder every 64, nothing over the river; the shade faint (0.03 a level), its lit side boosted three times so it shows on parchment; 16 texels a tile so a line stays thin; baked once per valley (~610 ms, at the valley's first draw);
- the view (`VillageMap.Relief.cs`) softens the wash across a tile and draws water level with its banks, so neither the
  whole-level steps nor the drop to the water show as lines; the wash is **0.06** a level (a level is now 1/16 of the old) — ⚠️ first set at 0.025 from a preview on bare parchment, and Joe saw no hills at all on Hilly under the watercolour and 55 % woods (D738).
- Each step was chosen by measuring the spread of steepness on eight seeds (the targets the first build had: about 2 %,
  7 % and 25 % steep), not by eye alone; the first tries are in D737 — a limit from the water applied after the
  smoothing capped most of the valley into one ramp, and before it alone left the banks steep.

## 6. What the player sees

- **The sim's half (systems track):**
  - `HeightAt` and `SlopeAt` for the view;
  - *What's here* says the ground's slope when it is not flat (*gentle slope*, *steep ground — slow to
    cross*);
  - a villager's card says *climbing* in place of *walking* while their leg is on steep ground.
- **The look (art track, A57, after this lands):** relief shading, contour lines and hachures drawn from
  `HeightAt`, so they show something true. **Until then, the view draws a plain light-and-shade wash**
  from the slope so the hills are visible at all. Without it the change would be invisible except as slower
  walking, which would read as a bug.
- **No new panel, no number on the bar.** A hill is a thing on the map, not a statistic (Non-Negotiable 4).

## 7. What changes meaning

- **Distances in the economy are now real walks on a hilly map.** `VillageEconomy`'s budgets
  (`RoundTripTicks` and the rest) read `TravelCost`, so they follow on their own. But the generator's *"the
  real distance is twelve tiles, on every seed"* sentences (`SimConfig.cs` ~2905) assumed flat ground: each
  is re-read and re-measured, not assumed.
- ⭐ **The code's default is flat; the shipped data has hills** (`height_levels`: 1 in `SimConfig`, 16 in
  `data/sim.config.json`) — `river_course`'s precedent, a new generator key whose code default is the valley as it
  was. Measured why: with the code default at 16, 21 tests failed, 14 of them posed villages that are about something
  other than hills (the Phase 0 pinned clock, a √5 diagonal leg, lane-keeping, the hunters' lodge under a met limit)
  and broke only because their year now played out on a slope. Flat by default, those keep the ground they were written
  for; the shipped valley, the goldens taken on it and the game have hills.
- **Every golden that hashes a walk moves, by design** (D734). Each is re-taken with the old value beside
  it, and **proven by ablation**: with Hilliness at a flat setting (`heights.levels: 1`) every old golden
  passes, which proves the change is the heights and nothing else.
- **D666's bar** (one source building plus a workshop comfortably supplies ~10 adults) is re-measured on
  *Rolling* and on *Hilly*. If *Hilly* breaks it, *Hilly* is a harder valley by design, and that goes to
  Joe, not into a quiet rebalance.

## 8. Measured before typed — `tools/harness/ZzHeights.cs`

The numbers in §3–§5 are starting points, as Joe asked (*"measured on his saves"*). Before any of them is
typed into `data/`:

1. **The field's cost**: suite wall-clock, and one rebuild of every field on his largest save, flat
   against *Rolling* against *Hilly* (§4.1's ring-every-time risk).
2. **Survival over 86 seeds** (the `ZzNomads` arms' valleys), flat against *Rolling* against *Hilly*:
   dead valleys, people in Year 12, food and logs.
3. **Joe's own saves**, copies only: twenty years flat against *Rolling*, per village (alive, starved,
   cold, logs, firewood, haulers' trip time).
4. **What a slope does to a trip**: the median and worst trip, home to work and work to store, on each
   setting.

If ×1.5 makes *Rolling* noticeably harder than flat on his saves, the number comes down before it ships,
and the table above is rewritten with the measured one.

### 8.1 What it measured (D736, 2026-11-13) — the numbers stand

**Joe's villages, copies of his saves, twenty years, flat against hilly.** A save from before heights loads flat, so the
hilly arm gives each copy its hills from its own river, founding and seed first (the Heights stage's own code). Of the
twelve copies in the last measurement's folder, ten are foundings or dead valleys the unattended harness freezes either
way (D447: the harness is not a player), so the six established copies were run too, on *Rolling* and on *Hilly*:

| Village (alive at load) | Flat: alive at the end, walking | *Rolling* | *Hilly* |
|---|---|---|---|
| Hollowmere (70) | 78, 34.6 % | 74, 37.5 % | 70, 39.2 % |
| Oakhaven (24) | 55, 22.5 % | 56, 23.8 % | 53, 24.8 % |
| Stonebridge (18) | 18, 39.1 % (22 starved) | 19, 39.2 % (22) | 21, 39.1 % (17) |
| Willowdale, elder-yarrow (16) | 40, 31.7 % | 43, 31.3 % | 37, 32.4 % |
| Willowdale, hazel-kestrel (12) | 25, 28.5 % | 24, 29.7 % | 24, 30.2 % |
| **Five together** | **216** | **216** | **205 (−5 %)** |

*Walking* is the share of living villager-ticks spent on a leg. Logs held moved by at most 2 % either way; firewood by
−9 % at most (Hollowmere on *Rolling*), mostly within a few percent.

**Fresh valleys, 86 seeds, twelve years unattended** (`ShippedConfig.Established()`):

| Setting | Dead valleys | Alive at the end | Starved | Steep share of the land |
|---|---|---|---|---|
| flat | 1 | 1,074 | 10 | 0 % |
| *Gentle* | 0 | 1,061 | 8 | 1.6 % |
| *Rolling* | 0 | 1,076 | 10 | 6.8 % |
| *Hilly* | 0 | 1,076 | 8 | 25 % |

**The century survival guard** (five valleys, `ShippedConfigTests`): peaks −3 with nothing marked, −12 with four
buildings marked, nearly all of it one valley — recorded at its price in step 2.

**Verdict:** ×1.5 at the steepest does **not** make *Rolling* noticeably harder (§8's rule for bringing it down), so the
numbers in §3–§5 are the shipped ones. *Hilly* costs about 5 % of an established village's people over twenty years and
puts a quarter of the land on steep ground: a harder valley, by design, and Joe's to say if it is too hard. The field's
cost (§8.1's first item) was not timed on its own; the suite's wall-clock, which runs the ring every time on every hilly
valley, did not move (14m39–14m54 against 15m53–18m37 the slices before).

### 8.2 Re-measured after the smoothing (D737)

| Five established villages, twenty years | Flat | *Rolling* | *Hilly* |
|---|---|---|---|
| Hollowmere (70) | 78 | 69 | 72 |
| Oakhaven (24) | 55 | 56 | 52 |
| Stonebridge (18) | 18 | 18 | 21 |
| Willowdale, elder-yarrow (16) | 40 | 44 | 37 |
| Willowdale, hazel-kestrel (12) | 25 | 24 | 28 |
| **Together** | **216** | **211 (−2 %)** | **210 (−3 %)** |

Walking took up to 2 points more of their time. ⚠️ **Firewood held runs lower on hills** — Oakhaven 1,600 → 1,275 on
*Rolling*, Hollowmere 1,682 → 1,406 on *Hilly* — with nobody colder: logs carried over slopes. 86 fresh valleys, twelve
years: flat 1,074 alive, *Rolling* 1,075, *Hilly* 1,070, one dead in each. Steep ground: *Gentle* 0 %, *Rolling* 4.7 %,
*Hilly* 30.6 %. The verdict of §8.1 stands — *Rolling* plays as the flat did, *Hilly* a little harder — and the slope's
costs (×1.2, ×1.5) are unchanged. ⚠️ The harness's first run after the smoothing pinned `height_levels` at the old 16
and measured flat valleys (hilly equal to flat to the digit, 0 % steep); it uses the data's 256 now.

## 9. Saved, hashed, drawn, told

- **Saved**: the height layer and the Hilliness setting, with the map. ⚠️ **His old saves**: a save from
  before heights has no height layer and **loads flat**, so the villages he built are not re-priced under
  him. Only a new game gets hills. *(Recommended; the other way, regenerating hills from the old save's
  seed, is a one-line change, and his call.)*
- **Hashed**: the height layer is world state and goes into the state hash. The slope classes are derived
  and are not hashed (D335).
- **Determinism**: integer noise from the stage's own stream; the determinism test stays green on every
  setting.
- **Told**: nothing in the log. A hill is not news.

## 10. Guards (TDD, red first, red-checked and counted — D326)

1. Same seed, same Hilliness ⇒ the same height layer, byte for byte. A different seed ⇒ a different one.
2. Stage 6 moves no other stage: on the same seed the river, the founding, the seams and the woods are
   identical with heights on and off.
3. Water is level 0; the river runs along the bottom (nineteen banks in twenty below the land's middle height —
   *"a bank is at most 1"* until D737); the founding ring is flat or gentle; no step over 20 on *Hilly* anywhere
   (`TheHillsHaveNoCreases`, D737); the passable valley is one connected piece on every seed of the 86.
4. The slope classes at their thresholds, posed by hand.
5. The entry cost on each class, with and without wear, posed by hand: 10 / 12 / 15, worn steep 14, and a
   deck ignores the slope.
6. The field carries a 15 without throwing, and its answer matches a plain Dijkstra on a posed hilly map.
7. A walk across steep ground takes 1.5× the ticks of the same walk on the flat.
8. A cart on unworn steep ground is still slow-on-grass (§4.1.3), and on a worn steep path it is not.
9. A trip costs the same both ways across a hill, to within the end tiles.
10. An old save with no height layer loads flat and plays as it did (its own hash unchanged).
11. The probe: the Hilliness row on the new-game screen (three choices, default *Rolling*, founds the
    valley it says); *What's here* names a steep tile; `bar height` stays 151.

**Step 1's red-check (D326): 8 mutants, 7 red, one equivalent zero kept.** The bank rule off (guard 3); the
founding ring skipped (guard 3; first written as a deleted call, which the analyzers refused to build); the stage's seed
pinned (guard 1 — ⚠️ a zero at first: two seeds still differed by their rivers and then by their founding rings, so the
guard now also compares two valleys with neither); diagonals counted (guard 4); the up-neighbour dropped (guard 4 — ⚠️ a
zero at first: the posed map's 8 came from below as well, so the corner below is 1 now); the Hilliness scale ignored
(`HillierValleysAreSteeper`); the class threshold off by one (guard 4). ⚠️ **The zero, kept:** water given the hills'
formula instead of skipped. The river ramp already fades a hill to nothing at distance 0, so the skip is a work-saver
the formula makes redundant; no guard can see it.

**Measured on step 1** (6 seeds, the shipped 120 × 80 valley): steep tiles are about 2 % of a *Gentle* valley, 7.5 %
of a *Rolling* one and 27 % of a *Hilly* one. Starting points for §8.

**Step 2, built (D735).** `TravelCostField.ReadSlope` builds a slope table once from the map (null on a flat valley,
which then routes on exactly the old code); `CostToEnter` is the wear price on the tile's slope, half up, a deck level;
`ComposeTheRouteCost` slopes the whole table, so a hilly valley takes the ring every time; the ring is sized to the
dearest step (`_largestStep`, 15). `IsUnworn(tile)` (grass's price before the slope) replaces the five
`CostToEnter == BaseTileCost` grass tests. The heights are hashed, sparsely, so a flat valley mixes nothing.
- **Goldens re-taken (5), the old values beside them:** `PhaseFiveGoldenTests`' two (2488598668680717856 →
  3617843879071782529; before-anybody-got-better 7275266205845974493 → 11978217067257029817 — ⚠️ first read as unmoved,
  because the test stops at its first failed assert), `SkillTests`' shipped fifty years (3174496459823721064 →
  2791566434779995178), `StockLimitTests`' shipped fifty years (1984555868935464164 → 17224396567075353750), and
  `MapGenerationTests.DrawOrderIsTheContract`'s map hash moved only while the code default was 16 (it is flat by default
  now, so it did not move). ⭐ **Proven by ablation:** with `height_levels` 1 in the shipped data, every one of the 21
  tests that failed passes on its old value.
- **One test re-read, not re-taken:** `AFinishedBridgeOpensTheFarBank` priced a square crossing at 10 a tile; the bank it
  sets off from is gentle now (12), so it prices that tile on its own slope (52 for 50).
- **One bar moved at its measured price, Joe's to disagree:** `TheShippedVillageSurvivesBeingAskedToBuild`. Five
  valleys, unattended for a century: flat, marked 106 / nothing marked 101; hilly, 94 / 98. Hills cost 3 at the peak with
  nothing marked and 12 with four buildings marked, nearly all one valley (seed 12345 marked, 33 → 22). Starving is the
  harness's either way (176 / 179 flat, 166 / 180 hilly). Held at −4 from −3.
- **Step 2's red-check: 8 mutants, 7 red, one zero kept.** Grass tested by cost again (guard 8 and the goldens); the ring
  at 10 (12 tests, the walk across steep and the bridge among them; first written as an unused field, which the
  analyzers refused); the routes unsloped (guard 6 and the bridge); a deck given the slope (⚠️ only the Phase 5 golden
  sees it); the heights unhashed (the goldens); the world never reading the slope (guards 7–8); rounding down (guard 5).
  ⚠️ **The zero, kept:** the cart's slow-on-grass rule on the last fraction of a tile before its target
  (`BehaviorSystem`'s first site) tested by cost again. `WalkedOver` is guarded; that one line is not.

**Step 3, built (D735).** A save keeps its heights (`map.heights`); one without them loads flat and plays exactly as
it did (guard 10, both ways). `NewGame`'s **Hilliness** row (`hills`: gentle 50 / rolling 100 / hilly 200 of
`hill_scale_percent`, after *River runs*; the share code carries it, an old code without it opens on *Rolling*).
`SimWorld.Heights.cs`: `SlopeAt`, `SayTheSlope` (*"Steep ground — slow to cross, up or down."*, *"A gentle slope — a little
slower to cross."*, nothing on the flat or the water) and `IsClimbing` (on the move across steep ground). *What's here*
adds the slope's line under the ground's; a villager's card reads *climbing* for *walking* while they cross steep ground.
`VillageMap.Relief.cs` bakes the wash once per valley — a hillshade lit from the upper left, 8 texels a tile, at most
0.34 — and draws it over the ground and under the trails; a flat valley bakes nothing. Probe: `relief:` (the wash at the
valley's size, the steepest tile washed, the flat founding not, a flat valley baking nothing, and the words through the
real panels — *What's here* and a card posed crossing the steepest tile, put back as found). Two new-game tests had
used `hills` as their made-up unknown setting; it is `weather` now.
- **Step 3's red-check: 9 mutants, 9 red.** An old save made to need heights; a hilly save saved flat; the steep
  sentence dropped; *climbing* while standing still; *Hilly* set to 100 (`ARowSetsItsKeys`); and through the probe,
  *What's here*'s line, the card's *climbing*, a flat valley baking a wash, a wash that lights nothing.

## 11. Definition of Done

- §10's guards green and red-checked, the reds counted, any zero written down.
- §8 measured and written into this spec, the numbers in `data/` the measured ones.
- The four checks (CLAUDE.md), goldens re-taken with ablation proof.
- **Joe plays a *Rolling* valley and a *Hilly* one** and says whether the hills read and the pace feels
  right. That is the success test, not the suite.

## 12. Order of work, when he says build

1. The layer, the stage and the slope classes (guards 1–4); no cost yet, so no golden moves.
2. The cost (guards 5–9): the ring, `IsUnworn`, the multiply; goldens re-taken here, with ablation.
3. Saves (guard 10), the Hilliness row, *What's here*, the card's *climbing*, the light-and-shade wash
   (guard 11).
4. §8's measurement, the numbers set, Joe plays it.

Each step is its own commit on `slice/heights`, and the four checks run at each.
