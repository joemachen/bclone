# Spec: Footprints per building — a granary is bigger than a hut

**Decisions:** D382 (this document). Neighbours: D319 (the centre rule), D322 (the longhouse), D329–D331 (free placement, SAT), D344 (a re-take with one reason), D353 (item 4 of Joe's 2026-09-12 list).
**Status:** ✅ **BUILT (2026-09-16, D382)** — the table is Joe's ("Modest", chosen 2026-09-16); unplayed as of this line. Owner: Joe + Claude Code.

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
