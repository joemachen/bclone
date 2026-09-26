# Spec: Organic housing — plots and lanes

**Decisions:** D386 (this document), D388, D411 (§9). Joe, 2026-09-17: *"organic housing - and then i think i want to
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
Suite 1184 passing, 0 failing, 2 skipped of 1186. **Slice 3, a village and not a street (§9, D411): ✍️ SPECCED 2026-09-26, NOT BUILT, and waiting on Joe's calls in §9.9.** Owner:
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
✅ **Built and merged (D404–D406), down to 0.3 %** — Joe: *"let longer walks be the price of
fences."* ⚠️ Two rules it adds reach this spec: `plot_apart_tiles` charges a side WITH a
neighbour now (it charged the open sides, which packed plots until fences shut doors), and
`plot_depth` is 3; and the chooser refuses a facing whose gate would open onto a building
(`SimWorld.GateOpensAt`).

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

---

## 9. Slice 3: a village, not a street (D411, **✍️ SPECCED, NOT BUILT, and waiting on Joe's calls in §9.9**)

### 9.1 What Joe asked, three times

- D388 (2026-09-18), playing D386: *"the homes should have less uniform orientation. this isn't
  supposed to be suburbs."*
- D408 (2026-09-26), with a screenshot of five houses in one column, all facing the same lane,
  their fenced plots stacked like a street of semis: *"the home placement looks like suburbia
  instead of having more naturally occurring orientations, no?"*
- 2026-09-26, asking for this spec: **organic housing, and NOT uniform rows of housing.**

D388 fixed the *facing* on one axis and made the *row* worse. §3.3's measurement shows it without
naming it: *"49 of 70 houses fronting a lane that was already there"*. That is a street, counted
as a success.

### 9.2 Why it happens (read from the code, `Household.ChooseSite`)

Four causes. Each is a rule that is right on its own, and together they build a street:

1. **The lane term spreads, and it is the first sort key.** `IsALaneAlready` (`Household.cs`
   ~548) counts a lane-row tile that only *touches* a tile some plot fronts (*"a street
   continues"*). So every plot next to the first street finds its lane row "already a lane" and
   faces it, and that extends the street by one more plot. The facings are sorted lane-first
   (`Household.cs` ~332), so no yard, neighbour or hash term ever outvotes it. The daily walks and
   worn paths feed the same term, so houses near the road all face the road.
2. **The walks pick the next plot along from the last one.** `toWork + toStore` changes smoothly
   across the valley, so the best free plot is nearly always the one beside the last house, along
   the same line of equal walk. D404 flipped `apart` to charge for a neighbour. By D404's own
   measurement the flip changed the result but the size of the charge did not (−2, −4 and −6 were
   identical), so it cannot break a line.
3. **Every plot is the same plot.** 3 × 3, the house flush on the front row, left or right by
   hash. A line of identical rectangles reads as a row of semis however each one faces.
4. **A guard enforces cause 1.** `APlotBesideAStreetFrontsIt` asserts that *every plot that could
   front a neighbour's lane does*. That is the suburbia rule written as a test. It has to be
   re-posed, and ⛔ only with Joe's word, because it is D388's rule (§9.9).

**Not a cause, but a limit on the fix:** the fence is a per-tile edge mask (D404) and the one cost
field is tile-based, so plots stay axis-aligned rectangles facing one of four ways. Variety has to
come from **which** way, **where** and **what size**, not from free angles (§9.4, R4).

### 9.3 What "organic" means here, in things a guard can count

The picture is Joe's *Foundation* screenshot (§ top) and a *Banished* village: **small clusters of
two to four houses with gaps between them, fronts that do not share one straight line, facings
that differ within a cluster, yards of different sizes, and lanes that wander because they are
desire paths (D358).** Measured:

| Measure | Definition | Suburbia reads | The aim |
|---|---|---|---|
| **run** | houses in a chain where each plot touches the next, all with the same facing and their door tiles on one line | runs of 5 (the D408 shot) | **no run longer than 2**, a pair (§9.9 Q2) |
| **modal facing** | the share of all houses that face the most common way | high | **≤ 50 %** of any village of 8+ houses |
| **lined-up doors** | houses whose door tile is on the same row or column as 2+ other same-facing doors within 6 tiles | most | a minority, measured before/after |
| **beside a neighbour** | as §3.3 (27 of 70 at D388) | | kept roughly; clusters are still clusters |
| **founders' walk to the hut** | as D405 (~10 tiles since fences) | | not worse by more than a tile |

### 9.4 The rules (proposed)

> **R1: a lane is a lane where there is one, and it does not spread.** Drop the *touches a fronted
> tile* clause from `IsALaneAlready`. A lane-row tile counts only if a plot fronts it, a daily walk
> crosses it, or feet have worn it. **The lane stops being the first sort key and becomes a cost in
> the facing's own score:** a facing whose lane row is not already a lane pays `plot_new_lane_tiles`
> (2) tiles of walk. That puts it in the same currency as `apart` and `clipped`, and the household's
> hash breaks the ties. A house beside a real path still usually fronts it, and a house beside a
> *plot* no longer has to.

> **R2: a lane holds a pair, not a row.** A plot whose door would extend a **run** (§9.3) pays
> `plot_row_tiles` (3) tiles of walk for each house already in the run beyond `plot_row_free` (1).
> The second house in a line is free, the third costs 3 and the fourth 6. It goes into the
> **site's** score, not only the facing's, so the chooser goes somewhere else: across the lane
> (facing back), round a corner, or past a gap to start a new cluster. It is legible: *"not a
> third along the lane to the south; the lane has its pair."* ⭐ This is the direct cure. R1 alone
> gives variety inside a row, and only R2 stops the row.

> **R3: every plot its own shape, by hash (after Joe has seen R1 + R2).** `plot_width` 3 or 4,
> `plot_depth` 3 or 4, and the house on the near side, the far side or (width 4) the middle, all
> from `PlotShape`'s hash of the household id. ⛔ Never the `Rng`. The config becomes a range
> (`plot_width: [3, 4]`, `plot_depth: [3, 4]`). ⚠️ It costs paint: a plot plus its lane averages
> ~15.75 tiles against 12 today (3×3 + 3, 3×4 + 3, 4×3 + 4 and 4×4 + 4, evenly). And bigger yards mean longer walks round fences, the price Joe
> accepted for fences (D405) but measured again here. A **set-back** (house one row behind the
> front with a front garden) was considered and **not proposed**: it moves the door off the gate
> edge that `GateOpensAt`, `TrialFence` and the wall-off sweep all assume.

> **R4: a house turned a few degrees inside its fence (view only). NOT PROPOSED, asked.** A hashed
> ±6° turn on the drawn house, with the sim's 2 × 1 footprint unchanged, would do the most for
> *"naturally occurring orientations"* at the least cost. But the house drawn would not be the
> house the sim walks round, and a turned 2 × 1 overhangs its tiles. §1.6 (*traceable over
> clever*) says ask first (§9.9 Q4).

**Not proposed, and why:**
- ⛔ **§9.4 of `fences-as-walls.md`, the walk priced from the door.** It pulls every door toward the
  granary, which is D386's uniformity again. Joe declined it (D405).
- ⛔ **Off-axis plots.** The fence is tile edges (D404).
- ⏸️ **The well as a focal point.** Clusters round a green are the most organic shape there is,
  and the well is Phase 6's. §3.3 already says it would be *"one more term"*. R2's clusters leave
  room for it, and nothing here builds it.

### 9.5 The score, whole, after R1 + R2

> **site score = toWork + toStore + detour + apart + clipped + newLane + row**

Every term is in tiles walked. The facing is the lowest `apart + clipped + newLane + row` for that
tile, then the household's hash, and the wall-off sweep runs as before. The card's sentence gets
one clause per new term that is not zero. For example: *"8 tiles to work and 3 to the granary,
facing a lane of its own to the west, beside the Ashfords; not a third along the lane to the
south."* Nothing is refused that was allowed before. R1 and R2 are prices, never refusals, so a
cramped valley still gets its house (D120's shape).

### 9.6 Determinism, state and hashing

- **No new state.** The facing and the plot are already `HomeFacing` and `FencedTiles` (hashed).
  R3's width, depth and side are derived from the id hash, so they are recomputable, the same way
  the house's side is today.
- **The run is read from the plot layer** (`ZoneMap`: owner and lane per tile, already maintained
  where plots change). It is a walk along one line of at most a few plots per candidate facing.
  ⛔ It is not rebuilt per tick and not cached across ticks (CLAUDE.md's index rule: the plot layer
  *is* the index).
- ⚠️ **The chooser's cost.** D405 took the site-chooser from 211 ms to 63 ms. R2 adds a short line
  walk per candidate facing. The suite's clock and the D405 tick timing are both read before and
  after.
- **Goldens move once**, for the one reason *"homes are sited by §9's rules"*. Every fixture golden
  and the shipped skills hash will move, because sites and facings change from the first house.

### 9.7 Guards (`OrganicHousingTests`), each red-checked with the reds counted

- `NoStreetRunsPastAPair`: a wide painted rectangle away from the founding, a dozen households
  housed one after another. The longest run is ≤ 2. Red with R2's term off (expected: the D408
  column).
- `ALaneDoesNotSpreadByTouchingAPlot`: a plot whose lane row only *touches* a fronted tile faces
  by hash, not toward the street. Red with the touch clause put back.
- `AHouseBesideAWornPathStillFrontsIt`: R1 keeps D388's good half. A plot whose own lane row is a
  worn path faces it when nothing else costs more.
- `TheFacingsSpread`: the fixture village at year 30 on the twelve fixture seeds. The modal facing
  is ≤ 50 % on each seed with 8+ houses. Stated as a rate over the sample, not *"every"* (D344's
  lesson).
- *(R3)* `PlotShapesVaryByHashNotRng`: the same valley twice is identical, and 64 ids cover every
  width, depth and side.
- **Re-posed with Joe's word:** `APlotBesideAStreetFrontsIt`. Proposed as *"a plot beside a street
  fronts it unless the street has its pair"*, which is D388's rule with R2 as the stated exception.
- **Kept unchanged:** `HousesWithNoLaneFaceByHash`, `TheFenceIsWhatWasBuilt`, and every
  fences-as-walls guard.

### 9.8 Measurement, and the order of the build

Measured **as played** (D409's lesson): six shipped seeds × fifty years, cold start, the starting
stock limits on, **and the storage arm** (granary and warehouse marked in year 3, since the
harness's own opening never raises one). Plus the twelve fixture seeds. Every §9.3 measure is taken
alongside alive / peak / starved / froze.

| build | runs ≥ 3 | longest run | modal facing | lined-up doors | beside a neighbour | founders' walk | shipped alive / peak / starved / froze | fixture alive / peak / starved |
|---|---|---|---|---|---|---|---|---|
| main (D410), the baseline | *to measure first* | | | | | | | |
| + R1 | | | | | | | | |
| + R1 + R2 | | | | | | | | |
| + R3 (if asked for) | | | | | | | | |

**Order:** measure the baseline, **and draw the sim's picture of a shipped seed at year 30 for
Joe** (D352: the picture is the spec). Then R1, measured, then R2, measured, and a new picture.
R3 only after he has looked. **The merge gate:** the shipped arm's alive count not below the
baseline by more than the seed-to-seed noise, which is D402's rule. A drop past that goes to Joe as
a trade, the way D404 did.

### 9.9 ⏸️ Joe's calls before building

1. **Which rules.** Recommended: **R1 + R2 now**, R3 after he has seen them, R4 not yet.
2. **The run cap.** A pair (`plot_row_free` 1: the third in a line pays) is the recommendation. A
   three (a short terrace) is his if he wants a little street.
3. **R3's paint.** A bigger average plot means fewer houses per painted tile and longer walks. Is
   that a trade he wants, once R1 + R2 are on screen?
4. **R4, the drawn skew.** Yes or no. It is the cheapest "organic" there is, and the drawn house
   stops being exactly the sim's house.
5. **`APlotBesideAStreetFrontsIt`** is re-posed as in §9.7, which is a partial reversal of D388's
   *"the lane picks the door"* on his word.

### 9.10 Definition of Done (slice 3)

R1 + R2 in `Household.ChooseSite` (with `PlotShape`, `ZoneMap`'s plot layer read, never
rebuilt). The new config values are in `data/sim.config.json` and `SimConfig`, with the reasons
beside them. The card's sentence names the new terms. The §9.7 guards are red-checked and counted.
Goldens move once with the one reason. §9.8's table is filled. The before-and-after picture is shown
to Joe. Suite, game build, probe and golden grep are all green (CLAUDE.md). DESIGN §6/§7 and this
status line move in the same commit. **Joe plays it**: the picture is the spec.
