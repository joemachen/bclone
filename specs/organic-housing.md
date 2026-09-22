# Spec: Organic housing — plots and lanes

**Decisions:** D386 (this document). Joe, 2026-09-17: *"organic housing - and then i think i want to
go back to building out the professions and their buildings."* The picture is his, from a
*Foundation* screenshot (DESIGN §4, Phase 5): *a painted zone, a hand-placed well, houses auto-placed
each in its own fenced irregular yard, packed like fields, with dirt lanes between them running to
the well, houses facing the lane.*
Neighbours: D42 (the residential brush — the player paints, the village builds), D350 (a home is
sited on a whole-painted tile), D358/D368 (desire paths — the lanes), D381 (the site-chooser walks
the paint), D382 (footprints; *"2×1 houses in a plot are Phase 5's organic housing"*), D383
(buildings are obstacles; the founding leaves lanes; the site-chooser prices the road), D357 (⛔ not
yard modules the player attaches; the kitchen garden comes later, *after plots*).
**Status:** ✅ **BUILT (2026-09-17, D386): slice 1 the sim, slice 2 the view.** Built with D387 (the
birth gate reads the harvest, `storage-and-distribution.md §12.4`). **✅ Joe's play notes built
(2026-09-18, D388): the lane picks the door and the fence is built with the house (§3.3, §3.5).**
Suite 1184 passing, 0 failing, 2 skipped of 1186. **D388 unplayed by Joe as of this line.** Owner:
Joe + Claude Code.

---

## 1. Goal

A home stops being a square dropped on the nearest painted tile and becomes **a house in a plot**:
a fenced patch of the neighbourhood that a household *owns*, with the house at its front, facing
the lane the household walks down every day. Neighbours share fences; lanes run between the rows;
the yard behind the house is where a kitchen garden will go. Nothing here is a new thing the
player does — the brush is unchanged (D42) — it is what the village does inside the paint.

## 2. Which pillars this serves, and what it must not touch

- **§1.1 legibility.** *Whose ground is this?* has one answer per tile, drawn as a fence. *Why is
  the house there?* has a sentence: *near the granary, facing the lane, beside the Ashfords.*
- **§2.6 desire paths.** A lane is not a new system — it is the row of ground every household
  fronts onto, and the walks wear it. Plots leave it free; the paths make it a road.
- **§1.3 people, not spreadsheets.** A family's plot is theirs across generations (D381: a new
  couple takes over a dead family's house — and its plot).
- ⛔ **Determinism.** Where a house stands and which way it faces is derived from the valley and a
  hash of the household — never an `Rng` draw. A cosmetic must not reshuffle every seed (DESIGN
  §4's own sentence).
- ⛔ **No click-farm** (D357). No per-house button; no yard modules. The player paints; the sim
  sites.
- ⛔ **Not a second placement system.** `Household.ChooseSite` stays the one site-chooser for homes,
  scored in the currency it already uses (tiles walked, D120/D383), and every other building's
  placement is untouched.

## 3. The rules

### 3.1 A plot

> **A plot is `plot_width` × `plot_depth` whole tiles (3 × 2), owned by one household. The house
> is 2 × 1 and stands on the plot's front row, flush to the lane; the rest is the yard.**

⚠️ **Three by two, not three by three, and it was measured (§5).** A plot and its lane are nine
tiles of paint at two deep against twelve at three; the fixture's diamond held eleven plots against
eight. Four yard tiles is a kitchen garden's worth. Joe's to widen (`plot_depth`) once he has seen
the yards.

- The **front** is the side the house faces; the **lane** is the row of tiles immediately beyond
  it. The plot's tiles are the front row and `plot_depth − 1` rows behind it.
- The house sits on the **left or the right** of the front row (three tiles wide, a house two
  wide), chosen by a hash of the household id — so a row of houses is not a row of identical
  boxes, and the third tile of the front row is the side yard, the gap between neighbours a
  fence runs along. ⛔ Never an `Rng` draw.
- The two house tiles must be **whole-painted** (D350: a house never overhangs the border), on
  land, free, in nobody's plot and on nobody's lane. **The yard is the rest of the rectangle,
  painted or not** — in nobody's plot and on nobody's lane, but a yard tile the brush never
  reached, or water, or a building already standing, is simply *clipped*: the plot is smaller,
  not refused. The fence follows the paint (§3.6), which is the *irregular* in Joe's picture.
  ⚠️ *Every tile of the yard painted* was the first rule, and it starved the fixture of plots
  (three in a diamond of eighty-five tiles: a diamond's diagonal edge never holds a full three
  by three); *a full plot first, a clipped one only if none* was the second, and in a cramped
  valley it sent the founders' second house twelve tiles from the hut past a clipped plot six
  away. **A clipped tile costs a tile of walk in the score (§3.3)** — the exchange rate the
  sentence can say — and nothing else.
- A plot's ground is **not an obstacle.** A fence is a line the view draws round owned ground, as
  it draws a farm's; villagers cross yards. A fence that walls is a later slice (`tech-tree.md
  §9.6`, Joe's call of 2026-09-16).

### 3.2 The lane

> **The row of tiles across a plot's front is its lane. A lane tile may not be claimed by any
> plot, and a plot may not front onto another plot's ground.**

- Two rows of plots facing each other across one row of tiles share a lane; that is the street.
  Plots side by side share a fence and no gap — *packed like fields*.
- A lane is **reserved, not built**: the tiles stay whatever they are (painted or not), the player
  may still put a building there (D11.4: free-form placement with warnings), and `ChooseSite`'s
  wall-off refusal (D383) still keeps every door reachable. Lanes are where desire paths (D358)
  will wear, because every household in the row walks out through its front.
- The tile in front of the door must be walkable land nobody stands on, or the plot is not a
  candidate: a house must front onto somewhere.

### 3.3 Choosing a plot (`Household.ChooseSite`)

Candidates are every (front-row tile, facing) pair in the painted land that §3.1–3.2 allow — the
four facings are four candidates per tile — walked in row order (a desync waits in an unordered
tie). Each is scored in **tiles walked**, the currency the chooser already uses:

> **score = toWork + toStore + detour + apart + clipped**

- `toWork`, `toStore`: from the house's **lane tile in front of the door** (the tile the household
  actually steps out onto), to the nearest workplace and the nearest granary — as today (D120's
  budget, never a refusal; unreachable is refused, D111).
- `detour`: what the village's daily walks lengthen by with the **house** stood there (D383's
  `DetourOfAHouseAt`, unchanged) — the yard is not an obstacle and adds nothing.
- `apart` (**new**): `plot_apart_tiles` (2) for each of the plot's two **sides** that does not
  touch another plot's ground. A plot beside a neighbour scores as if two tiles nearer; a plot on
  its own scores four further. That is the whole of *packing*, and it is legible: *"beside the
  Ashfords"* is worth two tiles of walk each way. ⚠️ Measured a nudge at two: over twelve seeds
  and fifty years 18 of 67 houses stood beside a neighbour with the term off, 22 of 69 with it on
  — the founding's stores crowd the fixture's diamond, and the walks decide most sites. Joe's
  to widen once he has seen the rows; the guard that would prove it scored zero and says so.
- `clipped` (**new**): one tile of walk for every yard tile the paint, the water or a building
  clips off (§3.1).
- Ties between tiles: nearest the founding site, then row order — as today.

**⭐ The walk picks the plot; the lane picks the door (D388).** Joe, playing D386: *"the homes should
have less uniform orientation. this isn't supposed to be suburbs."* D386 read `toWork + toStore`
from the **door** tile, so every door landed on the granary's side and a street was a row of houses
all facing one way — and its tie-break was the facings' fixed order, north first. Now the walks are
read from the plot's own tile, the same for all four facings, and **which way the house faces is
decided per tile, in order:** *(1)* the facing whose lane row is **already a lane** on the most tiles
— a tile some plot fronts, a tile *touching* one some plot fronts (a street continues), a tile the
village's daily walks cross (`TheDailyWalks()`'s routes), a tile feet have worn (`Paths.At ≥
path_worn_at`); *(2)* the facing whose yard the paint clips least and whose sides have a neighbour
(`apart + clipped`); *(3)* a **hash of the household id** (`PlotShape.FacingByHash`, ⛔ never the
`Rng`) — the founders' first houses and a plot with no lane nearby face by hash, which is where the
variety comes from. The wall-off sweep (D383) is asked of the chosen facing and, if it walls, the
next. The sentence says which: *"facing the lane to the south"* / *"facing a lane of its own to the
west"*. **Measured:** the fixture at year sixty faces four ways (D386: two, five of seven west) and
its lane row at y=0 has houses fronting it from both sides; twelve seeds × fifty years **199 / 221 /
0** (D387: 198 / 224 / 0), 49 of 70 houses fronting a lane that was already there, 27 of 70 beside a
neighbour (22 before — a street packs). Guards: `APlotBesideAStreetFrontsIt` (posed twelve tiles
from the founding so no walk crosses the square; red with the lane term off — the hash faces a plot
away from the street beside it), `HousesWithNoLaneFaceByHash`.

**And the walk is priced (D386, the D383 shape).** A house in a plot stands behind its lane, so
the tile a walk is measured from is a tile further out than a house dropped on the nearest painted
tile — measured across thirty generated valleys at the founding: the typical family's walk to the
hut went 6½ → 7½, +1 on most seeds, never more than +2. `VillageEconomy.PlotLaneTiles` (1) is on
every leg to or from a home, beside D383's detour tile: `gather_yield` **112 → 123** (eleven trips
a year, not twelve — and the floor is asked of the year's own truncating sum now, which 122 fed
257 against a need of 258), the fuel floor 40 → 41 (shipped 53 kept). ⛔ **The farm's derivation sat
one tick from a cliff and the lane tile pushed it over:** `FieldTilesOneFarmerKeeps` priced every
tile of a radius-two diamond at radius two's walk, 91 ticks against a season of 92; it prices each
tile from its own ring now (79), the diamond is the same thirteen, and the claim is still a floor
reality beats. Twelve seeds × fifty years, plots at radius six: **204 / 247 / 21 unpriced → 241 /
261 / 0 priced.**

Facing falls out of the score: `toStore` is read from the lane tile, so a plot faces the side its
walks leave by, which is the lane between it and the village; a second row across the lane faces
back at the first because its own walks leave through the same lane. The well as a focal point
(DESIGN §4: *later*) would simply be one more term.

### 3.4 What the plot is, in state

The state is the house and its fence: `Household.HomePosition` (a `Point`), **`Household.HomeFacing`
(an `Angle`, hashed)** and **`Household.FencedTiles` (the tiles the fence encloses, house tiles
included, hashed — D388)**. The house's footprint (`FootprintOf(Home, position, facing)`, 2 × 1
turned) is the front row's house tiles, the facing says which way the lane lies, and the hash of the
id says which side the house sits on; `PlotShape` is the arithmetic that turns those into the
*proposal* — the rectangle, the lane row, the sides — and `FencedTiles` is what was built of it.
`ZoneMap` keeps a **plot layer** — per-tile owner and per-tile lane count, the work-ground shape one
level up — fed `FencedTiles` and **maintained where the state changes** (a home marked, raised,
inherited, demolished; a site cancelled), never rebuilt per tick and never hashed: it restates the
households.

A site is a claim: the plot is reserved when the house is **marked** (`MarkHome` → `RaiseSiteFor`,
facing carried on the `Construction`), so the next household cannot choose ground under a house
that is still a plan. The site's facing becomes the household's when the house is raised.

### 3.5 The fence is built, not painted (D388)

Joe, playing D386: *"when a constructed yard shape changes based on painting/unpainting, it should
require a round of proper construction/demolition with some level of cost/payback. it feels too
malleable."* D386 derived the plot from the house and drew the fence along the paint at draw time,
so the brush moved a fence for free. Now:

- **The fence is fixed the day the house is marked.** `SimWorld.FencedTilesFor(plot)` takes the
  proposal's rectangle less what the paint, the water, a building or another plot clips off *that
  day* (the house's own two tiles always), and stores it on the household. A yard tile the paint had
  not reached is outside the fence for good, even painted the day after; a fenced tile stays fenced
  when its paint goes.
- **The fence is on the recipe.** `fence_logs_per_tile` (1) × yard tiles is added to the house's
  logs (`HomeRecipeWithFence`), hauled and worked by the builders like the rest; the site is named
  for it — *"a house and 4 tiles of fence"* — and the marking line prices it. The founders' fences
  cost nothing, as their houses do (the warm start raises them).
- **The brush never moves a built fence.** Unpainting a yard tile changes nothing (the house stands,
  the plot layer stands); unpainting a *house* tile still marks the house for demolition (D228), and
  the fence comes down with the house — its logs refunded with the house's
  (`DemolitionReturnsPercent`). Painting more ground beside a built plot does not grow the yard: the
  yard is what was fenced, and the brush is the player's intent for the *next* house.
- **A new couple taking a dead family's house (D381) takes the fence** — `FencedTiles` and the plot
  layer move with the house. A roofless family moving into a standing house gives up the site being
  raised for it, fence and all (D386).
⏸️ **AND SINCE D400 THE FENCE HAS A SPEC OF ITS OWN FOR BECOMING A WALL** — `fences-as-walls.md`,
Joe's call from his D396 play notes (*"villagers are definitely walking through other villager's
yards"*): measured at **16.2 % of every step in the valley** taken inside somebody else's yard.
Written, not started.

- Guard: `TheFenceIsWhatWasBuilt` — a yard tile unpainted on the marking day stays outside, painted
  after; a fenced tile stays inside, unpainted after; the recipe carries a log a yard tile and the
  site says so; the fence is in the hash. Red with the fence re-read from the rectangle.

### 3.6 The look (slice 2, the view)

- The **fence**: each household's fenced tiles, whole, traced and outlined per household exactly as
  a farm's ground is (`ZoneOutline`) — straight timber along tile edges, not the brush's curve — in
  a fence colour; no fill of its own, the wash is the neighbourhood's. Drawn with the residential
  layer, hidden with it; faint while the house is a site (the fence is on the recipe and not yet up).
- The **house**: drawn as its 2 × 1 footprint quad, turned by `HomeFacing`, the door on the lane
  side (a darker notch on the front edge).
- The **card**: a home's caption says the sentence — *"a wooden cabin — 10 tiles to work and 3 to
  the granary, facing the lane to the south, beside the Ashfords."* — from the terms that chose it,
  held on the household when the site was chosen (a string is cheaper than re-deriving it, and it
  is what the chooser *said*).

## 4. Data model

| Held | Where | Hashed? |
|---|---|---|
| `HomeFacing` | `Household` | ✅ new state |
| `FencedTiles` — the tiles the fence encloses, fixed at the marking (D388) | `Household` | ✅ state, hashed |
| the house site's facing | `Construction.Facing` (exists) | ✅ (already) |
| plot layer: per-tile owner (household id), per-tile lane count | `ZoneMap` | ❌ derived, incremental |
| `plot_width` 3, `plot_depth` 2, `plot_apart_tiles` 2, `fence_logs_per_tile` 1 (D388) | `SimConfig` / `data/sim.config.json` | config |
| `VillageEconomy.PlotLaneTiles` 1 | the tile every home leg carries for the lane | derivation |
| house extent 2 × 1 | `SimConfig.DefaultBuildings()` (`ExtentWidth` 2) | config |

## 5. Edge cases and failure modes

- **The founders.** The four founders' houses at tick 0 take plots like everyone else — one rule.
  ⚠️ **The fixture's painted diamond moved, four → six (`starting_residential_radius`), and not
  further, and it was measured.** At four the diamond held three plots where the fixture raises
  seven houses. ⛔ Not seven or eight: the diamond's west side lies inside the founding forager's
  ring, residential ground does not regrow (RegrowthSystem), and every ring of paint beyond six
  costs food — twelve seeds read 259 / 278 / 12 at four, 199 / 252 / 46 at six and 167 / 239 / 62
  at seven *with the old one-tile houses*. Six is the smallest diamond that holds the village;
  with plots at six the same seeds read 204 / 247 / 21 before the lane was priced — the plots
  cost nothing, the paint did. **Every fixture golden re-takes once, with this as the one reason**
  (D152/D344's shape); the valley pin re-pinned a twelfth time.
- ⛔ **A roofless family moving into a standing house gives up the site being raised for it.** A
  couple inherits a dead household whole, site included (D381), and until D386 that site went on
  being built and handed them a second house; a family holds one plot, and `HandPlotOn` refused
  the second.
- ⛔ **Found on the way: a hunter's carry-back never arrived (D384's bug).** `HaulingToFarm` is
  the farmer's errand in `ErrandKind`, so `HoldsTheJobFor` read false on the first tick of every
  hunter's carry and the recall sent them home with the meat — D384's guard passed on *one* rise
  of the lodge in two years (a hunt on the tile beside it), D385's store-always rule walked the
  meat to the granary instead, and plots put the hunt tiles far enough out to read zero. The
  errand is the holder's when the load is bound for the building they hold; and the hunter now
  stands on the lodge for the tick they put it down (D373), as the fisher and the marketer do.
  Guards re-posed on what actually leaves the lodge and reaches a shelf, tick by tick.
- **A plot that would wall someone off** is refused the way a house is (D383) — the house is the
  obstacle, and the wall-off check runs on the house footprint.
- **Nowhere for a plot** in painted land that has room for a house is the new *"none can take
  one"* reason, and the warning says it: *"N painted only in part, M without room for a plot
  (3 × 3 and a lane)"*.
- **The turned 2 × 1 and the centre rule.** An even extent turned a quarter is anchored *north*
  instead of *west* (the D382 anchor rule applied to the turned extent — `SimWorld.HomeAnchorOn`)
  so the house claims exactly its two tiles at every facing — guarded, because D331 showed the
  centre rule slipping; red with the anchor read unturned (the east and west facings claim four).
- **Paint that is not a rectangle.** A house needs two whole-painted tiles side by side and a
  lane in nobody's plot; a one-tile strip yields no house where it used to. The *nowhere to build*
  reason counts it: *"N without room for a plot round them (3 by 2 painted tiles and a lane)"*.
- **Unpainting under a house.** A house is two tiles now, and unpainting *either* marks it for
  demolition (D228's rule, unchanged) — two zone guards that kept only the house's front tile
  painted turned the whole village out and it froze.
- **Guards whose premise was a one-tile house or a tile-dropped site** — some twenty, each
  re-posed with the reason in place: the occupancy index reads `HomeFootprintOf`; the straight-line
  walk is building to building and allows a tile or two round what stands (D383); the lone walker's
  doorstep is the house's whole edge, and one worn tile is a scuff, not a path; the second town
  hall is sought where the hall's own rule refuses it; the farm guards pin a hand (D384's premise,
  shared) and paint no more slivers than a spring can sow; the winter-drain guard counts the load
  in the villager's arms; the unchanged-ground guard runs the lone forager, because the village's
  second house stands on trees now.

## 6. How it is tested

- `OrganicHousingTests` (new): a house is 2 × 1 in a 3 × 3 plot whose tiles are the household's
  (owner per quarter); the lane row is nobody's and no later plot claims it; two households side
  by side share a side (apart scores them nearer — red with the term off); across a lane they
  face each other; the house sits left or right by hash and not by `Rng` (the same valley twice,
  the same side); a dead family's house hands its plot on; demolition releases it; a turned house
  covers exactly two tiles at all four facings; the chooser's sentence names its three terms.
- The determinism test stays green; `HomeFacing` is in the hash (a guard flips it and the hash
  moves).
- Every fixture-based golden re-takes once; the twelve-seed measurement is re-read and written in
  §7 with the radius that was chosen.

## 7. Measurement

Twelve fixture seeds × fifty years (alive / peak / starved) against D385's **259 / 278 / 12**, and
the fixture village at year 150 (peak, deaths). Filled in per slice.

| | alive | peak | starved | notes |
|---|---|---|---|---|
| D385 | 259 | 278 | 12 | radius 4, 1 × 1 houses packed tile to tile |
| the old houses at radius 6 | 199 | 252 | 46 | the paint alone: the diamond's west side eats the forager's ring |
| plots, radius 6, depth 2, unpriced | 204 | 247 | 21 | parity with the line above — the plots cost nothing, the paint did |
| plots, the lane priced (`gather_yield` 123) | 241 | 261 | 0 | |
| **+ D387, the harvest gate** | **198** | **224** | **0** | the peak is the harvest's ceiling now, not the granary's — the fixture holds 13–16 for 150 years (2 starved, 26 of old age) where it bred to 22 and lost ten |

| **D388 — the lane picks the door, the fence is built** | **199** | **221** | **0** | 49 of 70 houses front a lane already there, 27 of 70 beside a neighbour (22), four facings used |

Depth 3 was measured and not kept: 182 / 236 / 49 at radius 7 against 177 / 241 / 58 at depth 2,
and eight plots in the diamond against eleven.

## 8. Definition of Done

**Slice 1 — the sim:** ✅ the rules of §3 in `Household.ChooseSite` / `SimWorld` / `ZoneMap`
(`PlotShape` holds the arithmetic), the 2 × 1 house, `HomeFacing` hashed, the plot layer
maintained at every state change, seven guards in `OrganicHousingTests` (five red-checked, one
scored zero and says so, one reads the fixture), the fixture radius re-based and every golden
re-taken once, §7 filled, the docs in the same commit.
**Slice 2 — the view:** ✅ the fence (each plot's painted quarters traced per household, drawn
with the residential layer), the house as its turned footprint quad with a door on the lane side,
the card's caption *"a wooden cabin — 10 tiles to work and 3 to the granary, facing the lane to
the south, beside the Ashfords."*; view 0 warnings, probe green. ⚠️ **The windowed shot was not
had** (the hook's frame never fired this time; D384's thread) — the sim's own picture of the
fixture at year sixty was drawn instead and reads as the design: a street with houses facing it
from both sides, fences clipped along the diamond's rim. **Joe looks, because the picture is the
spec (D352's lesson).**
