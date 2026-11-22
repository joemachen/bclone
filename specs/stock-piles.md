# Stock piles — the stockpile's goods, and goods on the ground, drawn as piles (A158, A159)

> **Status: ✅ MERGED INTO `main` (A160, 2026-11-21) — built A159, played and approved: *"Looks good, merge it"*. Pushed (A161).** His five calls answered *"A, all as recommended"*
> (§10). Specced and mocked A158. Art lane (CLAUDE.md *Two tracks*), piece (d) of his play notes (HANDOFF, *"JOE'S PLAY
> NOTES, 2026-11-20 … SORTED BY LANE"*, his order A137), at his *"Start piles"*. View only: nothing in the sim, `data/` or
> `tests/` changes. Mocks, rendered by the game itself from copies of his saves: `D:\Projects\brushes\piles\piles_mock.jpg`
> (today / A / B in six scenes) and `piles_catalogue.jpg` (every good at every size, A beside B); the build's own renders
> are §11's.

## 1. What Joe asked

*"Stockpiles show piles of each good they hold (the bars stay on other buildings for now), and goods on the ground show
as piles, not the coloured block."* Two things, both with the watercolour on: the **stockpile** (`StoreKind.Pile`, the
1 × 1 the player places first, holding anything, 150 at most) and the **ground heap** (`SimWorld.GroundStacks`: goods set
down where there was no room — D134, D370).

## 2. What draws them today

- **The stockpile** is painted by shape (`building-roofs.md`, `RoofForm.Yard`): a patch of trodden earth with a post at
  each corner and nothing else (`VillageMap.Roofs.cs`, `Yard`). Its stock is **the same bars every store gets**:
  `DrawStores` → `CratesIn` / `CratesAt` (`VillageMap.cs`), a bar per good the store may hold, in the good's colour, as
  tall as its share of the store, five a row — and for a stockpile, which may hold every good, that is sixteen slots in
  one tile, so the bars are slivers a few pixels across and vanish below 8 px a tile (`StockDetailFrom`). The probe line
  is `store stock:` (`EveryStoreShowsItsStock`).
- **A ground heap** is a square chip in the good's colour (`GoodsPalette.ColourOf`, the overview's chip — *"one fact"*)
  with a 2 px ink edge, 0.28 of a tile and never under 3 px, at the tile's lower-right corner or just past a building's
  edge (`DrawHeaps`, `EveryHeapChip`, `HeapRectOf`, D371). Two goods on one tile are two chips side by side. The chip is
  also what the player clicks (`HeapUnder`, D396): one place decides the rectangle, for the draw and the click.
- Both are drawn before the painted canopy (A36, *"everything under"*), so **a heap or a stockpile under a tree is hidden**.

## 3. What his saves say (every save in his folder read, `brushes\piles\survey2.py`)

- **A stockpile holds goods in the first years only.** Once warehouses and granaries stand, the piles empty: Ravenscar's
  two held nothing from year 9 to 38. Early on they are full or nearly: Larkspur year 2, 138 and 150 of 150.
- **What they hold:** firewood (219 piles seen), logs (205), stone (135); rarely iron, leather, clothing, a handcart,
  tools. **At most three goods at once** in any save; usually one or two.
- **Ground heaps** are mostly logs at a woodcutter's or stockpile's door, and food at a full granary's: logs (243 seen),
  fish (109), firewood, meat, produce, flour, wheat. **Up to 1,518 of one good**, and two goods on one tile happens
  (14 saves).
- **Hollowmere year 72 has three heaps under the canopy** — 1,472 food and 129 meat at one granary, 295 fish at another
  — none of them visible today, as chip or pile (§10, call 4).

## 4. The look — A, each good as itself (recommended), or B, a mound in its colour

In pen and wash like the yards (`building-yards.md §5`): every thing a fill in its wash with a sepia ink edge, a soft
shadow to the lower right. **A** draws each good as the thing it is, mostly with the yards' own painters:

| Good | Its pile (A) |
|---|---|
| Logs | a stack: a bottom row and a row on it, ends showing |
| Firewood | split wood stacked in rows — a cord, the end grain light and dark |
| Stone | a mound of stones, the outer first, the top lighter |
| Iron (ore) | the mine's rust heap |
| Hay | the barn's rick |
| Produce, bread | baskets, filled in the good's colour |
| Fish | creels of fish laid head to tail |
| Meat | joints, a pale knuckle at the narrow end |
| Wheat, flour | sacks in the good's colour |
| Leather | hides laid over one another |
| Clothing | bundles of folded cloth, tied |
| Stone tools, iron tools | an open crate, the handles out; the iron tools' heads dark metal, the stone tools' grey |
| Handcart | the cart itself (two at most) |
| **any other good** (barley and beer are coming, D821; a mod's) | sacks in the good's colour — a new good needs no art to appear |

**B** draws every good as one mound (the ore heap's shape) in the good's colour, wider as it grows. It reads by colour
alone, which is the chip's strength — and its weakness: at a glance logs, leather, meat and bread are four brown mounds.

**The colour stays the good's** in both: A's baskets, sacks, cloth and hides are washed in `GoodsPalette`'s colour, and
the natural things (logs, firewood, stone, ore) are already near it. So the overview's chip, the store's bar and the
pile still say one colour for one good.

## 5. How a pile grows

**Six sizes, by amount, the same everywhere** (recommended, call 3): 1–9, 10–29, 30–69, 70–149, 150–399, 400 and up
(`PileLevel`). A pile of 60 logs is the same picture on a stockpile and on the ground. A stockpile (150 at most) reaches
the fifth size only with a single good; the sixth is the ground's (a full granary's door). In A the count of things
grows (one log, two, three, five, seven, nine; a cord one row deep to four), and the mounds and ricks grow wider; in B the
mound grows from about a third of its bay to all of it.

*The other way, not recommended:* each good sized by its **share of the stockpile**, so a full stockpile always looks
packed. The full ring already says *full* (D140), and the same amount drawn two sizes in two places is one more thing
to learn.

**Laid only when a size changes** (CLAUDE.md: nothing derivable rebuilt per frame): the levels are in the stockpile's
`RoofSignature` (a new `YardState.Piles` — the held goods' levels, packed), so the stockpile is laid again only when a
good arrives, runs out or changes size; a ground heap is kept per heap the same way.

## 6. Several goods on one stockpile

**The goods it holds, in catalogue order, each in its own bay** over the yard: one good the whole yard; two or three
side by side in strips; four a 2 × 2; five or six 3 × 2; seven to nine 3 × 3 (more than nine are not drawn — the card
lists them; no save has had more than three). Each pile lies along its bay's long side. Catalogue order, so a good's bay
moves only when the set of goods changes, never because a neighbour grew. The bars are not drawn on a stockpile with the
watercolour on; **every other store keeps its bars** (his *"the bars stay on other buildings for now"*).

## 7. Goods on the ground

- **Where:** today's place — the tile's corner, or just past a building's edge on the tile's side (D371) — but pushed out
  by the pile's own size, so a big heap never lies over the building whose door it is at. A second good on the same tile
  steps on from the first (out from the building, or along the tile) a tenth of its own width at a time until it is clear
  of every heap before it on that tile (`EveryHeapChip`, `LiesOnAnEarlierHeap`) — the mock's 1,472 food over the 129 meat
  and the granary's corner is gone.
- **The spill** (A only, recommended, call 2): a wash of the good's colour under the things, with a soft ink edge — so
  a heap still reads as a spot of that good's colour from across the valley, as the chip did, and *"something is lying
  out"* is never quieter than it was. Without it, 68 meat at Hollowmere was three small joints that are easy to miss.
- **Its size:** the spill is about a third of a tile at the first size and grows to about seven tenths at the sixth
  (`HeapWidthOf`: 2.1 × (0.12 + 0.035 × size) tiles). **Far out, a heap narrower than 7 px on screen is today's chip**
  (`SmallestPilePixels`) — the things would be specks, and the chip in the good's colour is what reads at that size.
- **Clicked as today** (D396): the click's rectangle is the pile's drawn bounds, decided in the one place the draw asks.
- **Tidy and tumbled:** a stockpile's things are laid square to its posts; a heap's are tumbled a little (turned by its
  seed) — stored against set down.

## 8. Legibility (Non-Negotiable 1)

What the player reads, and from where: *which good* — its own shape up close, its own colour at any distance (the spill,
the baskets and sacks, the natural colours); *how much* — six sizes at a glance, the exact number on the card and on
*What's here*, as today; *full* — the stockpile's full ring, unchanged; *goods lying out* — the spill in the good's colour
where the chip was. Nothing is drawn that the sim does not hold (`building-yards.md §2`, the stock rule): a pile is there
exactly while its goods are. With the watercolour off the map is today's to the byte (bars and chips).

## 9. Code, probe, cost

- **Files:** a new `VillageMap.Piles.cs` (the levels, the bays, a painter per good, the heap's spill and place);
  `VillageMap.Yards.cs` (`YardState` gains `Piles`); `VillageMap.Roofs.cs` (one line: the stockpile's yard lays its
  piles). ⚠️ **And three small places in `VillageMap.cs`** (call 5): `DrawStores` packs the stockpile's goods into its yard
  state and skips its bars; `DrawHeaps` draws the painted heaps; `EveryHeapChip` / `HeapRectOf` size and space the heap
  by its pile.
- **Probe `stock piles:`** (named from this spec, traps 17 and 25; ⚠️ printed on the line after `building yards:`,
  riding on its return, because the list of probe lines is `Main.cs`, the systems lane's): a stockpile's pile present
  for each good it holds
  and absent at zero; its bays inside its yard and apart; a good's bay unmoved when a neighbour grows; the sizes' edges
  (9 → 10 … 399 → 400); a good the painters do not name drawn as sacks; the stockpile laid again when a size moves and
  not when an amount moves within one; a heap clear of its building and of a second heap on its tile; the click's
  rectangle the drawn bounds; laid once a size; watercolour off, the bars and chips as before. `store stock:` keeps
  asking every other store for its bars. ⚠️ **What it cannot see** (draw-time, the probe calls the pieces, not `_Draw`):
  the bars skipped on a stockpile, the sizes handed to its yard at the draw site, the heaps drawn above the canopy, the
  chip below 7 px, the heap cache forgetting what is off screen — §11's renders are their evidence.
- **Cost:** the stockpile rides the roof cache (one mesh run more, laid on a size change); the heaps are cached per heap
  (position, good, size), never laid per frame. Measured at 4K on Hollowmere before and after, as A86 did.

## 10. ✅ Joe's calls — answered *"A, all as recommended"* (2026-11-21)

1. **The look — A or B?** A: each good as itself — logs stacked, firewood corded, stone heaped, fish in creels, flour in
   sacks. B: every good a mound in its colour. ⭐ **A** — B's mounds tell logs, leather, meat and bread apart by colour
   alone, and four of them are brown. ✅ **A.**
2. **Goods on the ground: a wash of the good's colour under the pile?** It keeps a heap as easy to spot as today's square.
   ⭐ **Yes.** ✅ **Yes.**
3. **How piles grow — the same picture for the same amount everywhere** (six sizes, 1–9 up to 400+), or each good on a
   stockpile sized by its share of the stockpile? ⭐ **The same everywhere**; the full ring already says *full*.
   ✅ **The same everywhere.**
4. **Goods on the ground under trees.** Today the canopy covers them (your *"everything under"*, A36): at Hollowmere
   year 72, 1,472 food, 129 meat and 295 fish lie hidden. Goods on the ground mean a store is full — the one thing the
   player most needs to find (D134, D371). Draw ground heaps above the trees? ⭐ **Yes** — only the heaps; stockpiles
   and buildings stay under. ✅ **Yes** — with the painted trees on, the heaps are drawn after the canopy.
5. **Three small edits in `VillageMap.cs`** (the stockpile's bars, the heap draw, the heap's place) — the systems lane's
   file by the lane table. OK to touch them? ✅ **Yes.**

## 11. As built (A159)

- **Files:** `VillageMap.Piles.cs` (new: `PileLevel`, `PileSizesOf`, `PileBays`, `PilesOnTheYard`, `PileOf` — a painter
  per good — `HeapDrawing`, the heap cache, `DrawPiledHeaps`); `VillageMap.PilesProbe.cs` (new, the probe);
  `VillageMap.Yards.cs` (`YardState.Piles`); `VillageMap.Roofs.cs` (one line); `VillageMap.YardsProbe.cs` (the line it
  rides on); `VillageMap.cs` (`DrawStores`, `DrawHeaps`, `EveryHeapChip` / `HeapRectOf`, `_Draw`'s order — his call 5).
- **Probe:** 138 ✅, 0 ⛔; `stock piles:` ✅ — two heaps at the founders' cart's door 26 and 33 px wide, clear of it and of
  each other, each the click's, laid once a size; `heaps:`, `building yards:`, `store stock:` unchanged and ✅; bar
  height 183, tile centres ✅, new game ✅.
- **Red-check (D326): 16 of 16 red**, each on its own clause (`brushes\piles\redcheck159.py`, `red159.log`): the 9/10
  edge, a skipped size, bays shifted and too wide, every bay the first good, the stone's seed taken from its neighbour, a
  new good drawing nothing, size 5 one thing, the spill in the wrong colour, a heap a chip's width, no stepping apart, a
  heap over its building, a heap laid every amount, piles with the tick off, a stockpile drawn with nothing, sizes
  written as amounts. **Zeros, written down:** the five draw-time behaviours in §9 the probe cannot reach.
- **Suite** 2,182 passed, 5 skipped, 16 m 42 s; no golden moved (`git diff`, 448 lines); game build 0 warnings.
- **Cost:** **none measured.** The map's own `_Draw` time over 300 frames after 60 to warm, paused, at the closest zoom in his 2560 × 1440 window, this build against `HEAD`'s, alternated (`brushes\piles\measure.sh`, `cost.log`): Hollowmere year 72 over its food, meat and fish heaps and two stockpiles, medians 9.37, 9.14, 9.33, 9.34 ms before against 8.91, 9.11, 8.76, 8.84 after; Larkspur year 2 over its two full stockpiles and two log heaps, 2.66, 2.32, 2.43 ms before against 2.37, 2.32, 2.59 after. Inside the noise both ways: the stockpile rides the roof cache and the heaps their own, each laid once a size.
- **Renders** of the build from copies of his saves: `D:\Projects\brushes\piles\built_lark.jpg`, `built_rav.jpg`, `built_holl.jpg` — before (`HEAD`) beside after, the same shots, close to far (`render_final.sh`, `compose_final.py`). Larkspur year 2: its two full stockpiles' logs, firewood and stone as stacks, a cord and a mound, its two log heaps beside them. Ravenscar: two stockpiles, a firewood heap at the woodcutter's. Hollowmere year 72: the 1,472 food, 129 meat and 295 fish that were under the canopy now lie above it, clear of the granary and of each other; far out every heap is today's chip again.
