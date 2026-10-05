# Spec: Footprints per building — a granary is bigger than a hut

**Decisions:** D382 (this document). Neighbours: D319 (the centre rule), D322 (the longhouse), D329–D331 (free placement, SAT), D344 (a re-take with one reason), D353 (item 4 of Joe's 2026-09-12 list).
**Status:** ✅ **BUILT (2026-09-16, D382)**; §7 (the whole footprint is cleared, B5) ✅ **BUILT (D498), unplayed** — the table is Joe's ("Modest", chosen 2026-09-16); unplayed as of this line. Owner: Joe + Claude Code.

---

## 1. Why this exists

Joe, 2026-09-12, from a Foundation screenshot: *"see how the buildings take up more than one
square? … bakeries are different height, width, and depth than a blacksmith."* Every building in
this game is one tile because nobody typed the numbers: `Footprint` has carried width × height
with rotation and SAT collision since D331, and the longhouse (3×1, D322) exists so extent and
facing are content rather than machinery with nothing behind it. This slice types the rest.

## 2. The table (tiles, width × depth before turning)

| Building | Extent | Why this and not bigger |
|---|---|---|
| house | **2×1** | brush-sited in a plot behind its lane, turned to face it (D386, `organic-housing.md`); was 1×1 until Phase 5's organic housing. ⚠️ Anchored for its facing by `SimWorld.HomeAnchorOn` — the even extent turned a quarter is anchored north, not west — so it claims exactly two tiles either way |
| stockpile | 1×1 | free ground, a pile; it is the founding's first store and stands anywhere |
| forager's hut, forester's hut, fishing hut | 1×1 | a hut in the woods or on the bank; the ring, not the hut, is the size |
| woodcutter's hut, builder's hut, hunter's lodge | 2×1 | a yard beside the door |
| granary, warehouse, market, farmhouse, library | 2×2 | the stores and the house-sized civic buildings |
| town hall | 3×2 | the one building that should dominate |
| longhouse | 3×1 | as D322 built it |

The numbers live in `SimConfig.DefaultBuildings()` (`ExtentWidth`, `ExtentHeight`), and a modded
`buildings` list may say otherwise per row — the validator already refuses an extent under 1.

## 3. The rule the player can be told: **the tile you point at is its south-east corner — it grows west and north**

`Footprint.Covers` is the centre rule with an inclusive edge (`≤`, D319): a building covers the
tiles whose centres it stands on *or touches*. For an odd extent anchored on a tile centre that is
exactly the tiles you would draw. ⛔ **For an even extent it is not** — a 2×2 anchored on a tile
centre has its edges *on* the four neighbours' centres and claims a 3×3. The longhouse never hit
this because 3 is odd.

So a building has an **anchor** for a tile: `SimWorld.AnchorOn(kind, tile)` = the tile's centre,
*less* half a tile westward if the width is even and half a tile northward if the depth is even. A
2×2 pointed at (1,−1) stands on (0,−2), (1,−2), (0,−1), (1,−1) — four tiles, exactly, at every
snap. ⚠️ **Minus, not plus, because of `Tile`:** a building's `Tile` is its anchor's floor (D329)
and every finder, site lookup and guard keys on *the tile you pointed at*; an anchor half a tile
east floors to the next tile over, half a tile west floors to the tile itself. (The first cut went
east/south and thirteen finders lost their building.) Every place the sim puts a building on a *tile* uses it — the `GridPos` overloads of
`Mark` / `CanBuildAt` / `FootprintOf`, the Move tool's destination, the founding layout — and so
does the view's snap (`VillageMap.WhereItWouldStand`), so the ghost shows the four tiles it will
claim. Free placement (snap off) is unchanged: the point is the point. Rotation is unchanged: the
rect turns about its anchor and the centre rule resolves it (D331's floor stands: at least the
tile the anchor is in).

## 4. Consequences, stated

- **The fixture's founding layout is re-spaced — by one building, and not the one you would
  guess.** With west/north anchoring the 2×2 warehouse at (−2,0) stands on (−3..−2, −1..0) and
  the 2×1 builder's hut at (−1,−1) on (−2..−1, −1): one tile shared. The builder's hut moves to
  (−1,−2). ⛔ **Not the woodcutter's hut** — `VillageEconomy.FirewoodRoundTripTicks` reads its
  offset, and moving it a row re-derived the fuel economy (the shipped file stopped meeting its
  fuel target). The founding raises blindly, so a guard now says no two founding buildings share
  a tile. ⛔ **Every fixture-based golden re-takes once, with this as the one reason** (D152, D344's
  shape); the shipped cold start has no founding buildings and its goldens are expected to hold.
  ⚠️ **Superseded by D383's lanes** (`buildings-as-obstacles.md §4`): with buildings as obstacles
  this ring walled the founders in, and every founding building now has free ground on all
  four sides — warehouse (−1,−1), granary (2,−1), market (3,3), builder's hut (0,3), the
  woodcutter's hut unmoved.
- **The starter diamond holds fewer homes** — bigger stores sit on painted tiles — which is the
  fixture village growing differently, not a rule change; `ChooseSite` already skips what stands.
- **`SiteAt`** reads the site from the tile the builder stands on (D108); a corner-anchored
  building's `Tile` is the anchor's floor, one of its own tiles — the longhouse proved the
  multi-tile path.
- **The view** draws the footprint quad, the selection outline, the ghost, the card's portrait
  and the heap chip from the footprint already (D341, D371, D376); nothing there is typed by size.

## 5. How it is tested

- `FootprintTests`: `AnEvenExtentAnchoredOnATileCoversExactlyItsTiles` — a 2×2 by `AnchorOn`
  covers four tiles with `Tile` the pointed one (⛔ nine when anchored on the centre, the red
  check); `TheCatalogueCarriesTheTable` pins §2 row by row; `TheFoundingLayoutOverlapsNothing` —
  no tile covered twice (red with the builder's hut at its old offset).
- ⚠️ **The founding layout set `Position` and never the extent** — the fixture's granary read 1×1
  at a corner the day the table was typed while a marked granary read 2×2. `ExtentOf(kind)` is
  the one reader; the founding uses it now.
- Guards that posed a granary as a one-tile neighbour pose a stockpile; two clock pins moved by a
  fixed tick (the stores' doors are half a tile away) and are re-pinned with the reason; the
  firewood conservation guard was found to be counting store transfers, not felling, and reads
  `LogsEverFelled` / `LogsEverSplit` (counted at the stump and the block) now.
- A windowed shot: a 2×2 granary site, a 3×2 hall turned an eighth, a 2×1 hut, a 2×2 ghost —
  all at their sizes; the hook is not in the tree.
- Goldens: **six constants in four tests moved once** — the fixture and the established shipped
  fifty-year hashes, the skills pair, the farm pair — for this one reason; the cold-start shipped
  hashes are byte-identical, which is the proof the change is scoped to what stands at a founding.

## 6. Definition of Done

Table typed; anchor rule in the sim and the snap; founding re-spaced with a guard; goldens
re-taken once; specs (`gridless.md §2.3` says "every building is 1×1" — no longer),
`DESIGN.md §6/§7`, `handoff.md`; Joe has placed a 2×2 and a 3×2 and turned them.

## 7. Clearing the whole footprint (B5 — D496 measured, D497 Joe's call, D498 built)

**Status:** ✅ **BUILT (D498) on `slice/b5-whole-footprint` — unplayed.**

### 7.1 What was wrong, measured

D100 (*the village clears the ground, the player does not have to*) and D101 (*no work goes in
until the ground is bare*) were written when every building was one tile, and every check asks
`Workplace.Tile` — the anchor. D382 made five buildings wider and D386 made the house 2×1, and
nobody carried the extent to the clearing. **Measured (D496, `tools/harness/ZzB5.cs`, 80 houses
over 24 runs of 30 years): every front tile was cleared before raising, and 30 houses rose with a
tree (23) or rock (7) on their other tile — still there at year 30**, one of them in the unposed
played opening. A granary posed over four trees stood on two. Joe (2026-10-05): *"yes"* — clear
every tile.

### 7.2 The rule

**Every tile a building covers is asked, never only the one it is filed under.**

- **Marked** (`Mark`, `MarkHome`, and a relocation's destination in `Relocate` — which painted
  nothing until now and relied on D138's builder): every covered tile with something clearable on
  it (`TerrainRules.Yields`) is painted for harvest. One helper, three doors.
- **Waited on** (D101): a site takes no work, and is not served, until **every** covered tile is
  clear — `SimWorld.FootprintIsClear(Workplace)` in `NextSiteToServe`, `BegunSiteWithWhatItNeeds`,
  `NextBuildableSite`, the waited-on-goods pass in `NearestHarvest`, and both builder checks in
  `BehaviorSystem`.
- **Cleared:** `NextFootprintToClear` sends a laborer to the first painted, still-standing covered
  tile of the head site (row order of `CoveredTiles`); D138's builder clears the first unpainted
  one. A free building waiting on its ground (`_waitingOnTheGround`) is raised when the **last** of
  its covered tiles comes clear, from whichever tile cleared last.
- **Shown:** the view draws a site in the clearing's orange while any covered tile still stands
  (`VillageMap`, the D350 colour) — not only the anchor.

### 7.3 Cost (CLAUDE.md's rule: nothing derivable incrementally is rebuilt per tick)

`FootprintIsClear` is asked by every builder on every tick, of every site. A site's covered tiles
are a pure function of its position, extent and facing, so the workplace keeps them
(`Workplace.CoveredTiles`), computed on first ask and dropped when it moves (`MoveTo`). **Derived,
so never hashed** (D335). A 1×1 site asks one tile, as before.

### 7.4 What it changes, stated

- Houses and the five wider buildings in wooded ground take a little longer to start — one or three
  more tiles to fell — and the timber comes home.
- ⚠️ **And the trees under the far half of a house are gone for good** — residential ground never
  regrows (Joe kept that, D497) — so a village whose homes paint lies in its foragers' wood loses a
  few more ring trees than it did. **Measured (D498, D420's harness, `main` against the branch back
  to back, 50 years unattended):** 200 shipped seeds **43 → 47 dead valleys** (21.5 → 23.5 %, the
  guard's line is 25 %), alive **1,072 → 1,078**; 50 fixture valleys **9 → 7 dead**, alive **581 →
  559**, starved 92 → 110. Half the shipped seeds play byte for byte as before; the flips are
  villages of 1–3 survivors either way (4 died, 1 lived, on seeds 200–299). A lean, not a cliff —
  and the fixture's default village at year 7 holds no forage where `main` held 263, which is
  why `FoodLimitTests` now poses its store (below).
- **Goldens moved once, for this reason — four:** `SkillTests`' fixture and shipped fifty-year
  pair and `StockLimitTests`' pair. The cold start, the determinism guard and every other golden
  held.

### 7.5 Guards (`FootprintClearingTests`), each red-checked with the reds counted

One mutant per rule, each a compiling change back to the anchor (D420: a mutant that does not
build scores zero — three first drafts did not, and were rewritten). **10 of 11 mutants red, one
red each:**

- `MarkingAHousePaintsBothItsTiles` — a house over two trees asks for both. *(`MarkHome` on the
  anchor: red.)*
- `NoWorkGoesInWhileAnyTileStands` — a stocked site with its anchor clear and its other tile wooded
  is not buildable, and is once the tree falls. *(`FootprintIsClear` asking the anchor: red.)*
- `ALaborerIsSentToTheTileStillStanding` — anchor clear, the other painted, a nearer painted tree
  by the founding: the errand goes to the house. *(`NextFootprintToClear` on the anchor: red.)*
- `AWiderBuildingMarkedOnWoodPaintsEveryTile` — a granary over four trees: four painted. *(`Mark`
  on the anchor: red.)*
- `AFreeBuildingWaitsForItsLastTile` — a 2×1 builder's hut over two trees does not stand when one
  is cleared, and does when both are. *(the waiting list raised on the anchor: red.)*
- `AMovedBuildingAsksForItsNewGround` — a relocation onto wood paints its destination.
  *(nothing painted: red.)*
- `ABuilderClearsEveryTileOfTheirSite` — played: a house over two trees with its marks taken off is
  raised on bare ground, by the builder alone (D138). *(the builder's errand on the anchor: red.)*
- `OrganicHousingTests.AHouseGoesRoundASeam` (rock and iron) — a seam under the far tile of the
  chooser's own pick moves the house. *(`TilesClippedOff` blind to seams: red. ⚠️ First posed with
  both tiles seamed, which the front check caught first — **it scored zero**, and was re-posed.)*
- `OrganicHousingTests.AFamilyWithOnlySeamsSaysSo` — all-seam homes land says *"N on a stone or
  iron seam"*. *(the front check off: red; the reason unsaid: red.)*
- ⛔ **Scored zero, kept, written down:** the second builder gate (`BehaviorSystem`, D101's *"a
  builder can already be standing here"*) put back on the anchor turns nothing red — the gates
  before it stop the same case first. ⛔ **Unguarded:** the view's orange (`VillageMap`); the probe
  has no site-colour line. Joe sees it.
- Re-posed for the rule, not weakened: `NoBuildersHutTests.ClearGroundNearby` and
  `BuildersHutTests.AHutMarkedOnWoodedGroundStandsOnceTheGroundIsCleared` (a 2×1 hut's other half
  may be wooded), `HousesAreBuiltTests.PoseABegunSite` (both 2×2 sites cleared whole), and
  `FoodLimitTests.AMetFoodLimitStopsTheGatheringAndLeavesTheTrade` (its year-7 store posed — see §7.4).
- Played: `ZzB5.cs` re-run, 24 runs × 30 years — **0 houses raised over a tree or seam (was 30), and
  no house sited on one**; the posed granary stands on four grass tiles. Dead posed villages 5 of 24
  before and after (one each way; `wood 13` traced: both sites clear by tick 80, the houses 30 tiles
  out never got their logs, everybody froze in the first winter — it scraped through on `main` with
  one house at t497).
