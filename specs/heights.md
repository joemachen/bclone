# Spec: Real heights — hills that slow the walk, in the one cost field

**Decisions:** D734 (Joe's first call: height changes walking, *uphill is slower*), D735 (this spec; his
calls 1–4). Neighbours: D2 (integer costs), D15/§2.6 (one shared cost field), D179 (the sweep), D358 (worn
paths in the field), D383 (buildings as obstacles), D404 (fences), D473 (a stage per stream), D477 (height
parked *after the shell*, a stage and a row of its own), D546 (bridge decks), D641 (Phase 5's exit gate),
D666 (Joe's balance bar), D689 (a cart is slow on grass), A57 (the art lane's relief waits on this).
**Status:** ✍️ **SPECCED (D735, 2026-11-12), on `slice/heights` — not built. Built at Joe's word.**
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

- **A height per tile: a small integer level, `0` at the river's water up to a ceiling** (16 levels,
  `heights.levels` in `data/`). Integers only (D2): the generator never touches a float in anything the sim
  reads.
- **A pure function of the seed, the generator and the Hilliness row.** It never changes in play. No
  system writes it after generation.
- **Kept on `GeneratedMap` beside the terrain** (`HeightAt(tile)`), and **saved with the map.** That way a
  save never depends on the generator staying the same (§9, the old saves).
- **The slope class of a tile is derived once, at generation**, and kept beside it (CLAUDE.md: kept where
  the state changes, and it never changes):
  - **steepness** = the largest height difference between the tile and any of its four neighbours;
  - **flat** at a difference of 0, **gentle** at 1, **steep** at 2 or more (`heights.gentle_at`,
    `heights.steep_at` in `data/`).

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
  - **Water is level 0, and the banks are low.** A tile beside water is at most level 1, so the river never
    sits in a ravine and a bridge's ends are never steep.
- **Kept liveable** (`seeded-map-generation.md §10.4`: every seed survivable, none equally comfortable):
  - **the founding site and its ring** (the tiles the founders' first buildings take) are **smoothed to
    flat or gentle**, so no founding starts on a cliff;
  - **the passable valley stays connected.** Height never makes ground impassable: steep is slow, not a
    wall. Rock and water stay the only walls.
- **The Hilliness row on the new-game screen** (`new-game-screen.md`: D477's parked line, built here).
  Three choices, default *Rolling*, saved with the game and shown on the pause screen's *About this run*
  like the other rows.

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
3. Water is level 0; a bank tile is at most 1; the founding ring is flat or gentle; the passable valley is
   one connected piece on every seed of the 86.
4. The slope classes at their thresholds (0, 1, 2+), posed by hand.
5. The entry cost on each class, with and without wear, posed by hand: 10 / 12 / 15, worn steep 14, and a
   deck ignores the slope.
6. The field carries a 15 without throwing, and its answer matches a plain Dijkstra on a posed hilly map.
7. A walk across steep ground takes 1.5× the ticks of the same walk on the flat.
8. A cart on unworn steep ground is still slow-on-grass (§4.1.3), and on a worn steep path it is not.
9. A trip costs the same both ways across a hill, to within the end tiles.
10. An old save with no height layer loads flat and plays as it did (its own hash unchanged).
11. The probe: the Hilliness row on the new-game screen (three choices, default *Rolling*, founds the
    valley it says); *What's here* names a steep tile; `bar height` stays 151.

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
