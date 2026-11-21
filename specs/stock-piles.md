# Stock piles — the stockpile's goods, and goods on the ground, drawn as piles (A158)

> **Status: ✍️ SPECCED AND MOCKED (A158, 2026-11-21) — ⏸️ FIVE CALLS JOE'S (§10). Nothing built.** Art lane (CLAUDE.md
> *Two tracks*), piece (d) of his play notes (HANDOFF, *"JOE'S PLAY NOTES, 2026-11-20 … SORTED BY LANE"*, his order A137),
> at his *"Start piles"*. View only: nothing in the sim, `data/` or `tests/` changes. Mocks, rendered by the game itself
> from copies of his saves: `D:\Projects\brushes\piles\piles_mock.jpg` (today / A / B in six scenes) and
> `piles_catalogue.jpg` (every good at every size, A beside B).

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
  by the pile's own size, so a big heap never lies over the building whose door it is at; two goods on one tile side by
  side by their drawn widths (the mock does neither yet: at Hollowmere's granary its 1,472 food lies over the 129 meat
  and the roof's corner).
- **The spill** (A only, recommended, call 2): a wash of the good's colour under the things, with a soft ink edge — so
  a heap still reads as a spot of that good's colour from across the valley, as the chip did, and *"something is lying
  out"* is never quieter than it was. Without it, 68 meat at Hollowmere was three small joints that are easy to miss.
- **Its size:** the spill is about a third of a tile at the first size and grows to about three quarters at the sixth;
  never drawn smaller than today's chip on screen (3 px). Far out, below a few pixels, it is the spill alone.
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
- **Probe `stock piles:`** (named from this spec, traps 17 and 25): a stockpile's pile present for each good it holds
  and absent at zero; its bays inside its yard and apart; a good's bay unmoved when a neighbour grows; the sizes' edges
  (9 → 10 … 399 → 400); a good the painters do not name drawn as sacks; the stockpile laid again when a size moves and
  not when an amount moves within one; a heap clear of its building and of a second heap on its tile; the click's
  rectangle the drawn bounds; never under 3 px; watercolour off, the bars and chips as before. `store stock:` keeps
  asking every other store for its bars.
- **Cost:** the stockpile rides the roof cache (one mesh run more, laid on a size change); the heaps are cached per heap
  (position, good, size), never laid per frame. Measured at 4K on Hollowmere before and after, as A86 did.

## 10. ⏸️ Joe's calls

1. **The look — A or B?** A: each good as itself — logs stacked, firewood corded, stone heaped, fish in creels, flour in
   sacks. B: every good a mound in its colour. ⭐ **A** — B's mounds tell logs, leather, meat and bread apart by colour
   alone, and four of them are brown.
2. **Goods on the ground: a wash of the good's colour under the pile?** It keeps a heap as easy to spot as today's square.
   ⭐ **Yes.**
3. **How piles grow — the same picture for the same amount everywhere** (six sizes, 1–9 up to 400+), or each good on a
   stockpile sized by its share of the stockpile? ⭐ **The same everywhere**; the full ring already says *full*.
4. **Goods on the ground under trees.** Today the canopy covers them (your *"everything under"*, A36): at Hollowmere
   year 72, 1,472 food, 129 meat and 295 fish lie hidden. Goods on the ground mean a store is full — the one thing the
   player most needs to find (D134, D371). Draw ground heaps above the trees? ⭐ **Yes** — only the heaps; stockpiles
   and buildings stay under.
5. **Three small edits in `VillageMap.cs`** (the stockpile's bars, the heap draw, the heap's place) — the systems lane's
   file by the lane table. OK to touch them?
