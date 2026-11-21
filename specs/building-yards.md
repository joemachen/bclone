# Building yards, and the buildings in pen and wash (A86)

> **Status: ✅ MERGED INTO `main` AND PUSHED (A88, 2026-11-15; built on `art`, A86; the founders' cart §3.4, A87), PLAYED (A89: *"cart and yards look good"*).** On top of A85's roofs
> (`building-roofs.md`), behind the same watercolour tick; with it off the map is byte-identical to the build before. Art
> lane (CLAUDE.md *Two tracks*). Nothing in the sim, `data/` or `tests/` changes.

## 1. What Joe asked, and what he chose

On A85's roofs, Joe: *"They look cute, but i'd like more visual identity for each building. like, the fishing hut should
have a dock and netting outside … what can be unique about the granary's 'yard' … there's nothing in a home's yard. what
does the fencing look like? … accounting for adding things like a cart … Question: should we consider more of a 'water
paint' coloring for the buildings?"* Three sketch sheets later (`D:\Projects\brushes\buildings\yards\`: `yards_kinds_ref.jpg`,
`yards_homes_ref.jpg`, `yards_style.jpg`) he agreed *"100%"* with the stock rule (§2), sent his house-tier concept sheet as
the look to match, and said **"Yes, build it"** to the pen-and-wash pass.

## 2. ⛔ The stock rule (Joe's, "agree with this 100%")

**Anything that is stock is drawn only while the building really holds it** — read from the sim, never invented:

| Drawn | Only while | Read from |
|---|---|---|
| fish on the fishing hut's rack | the hut holds fish | `Workplace.Store[Fish]` |
| meat on the hunter's smoking rack; a hide on its frame | the lodge holds meat; leather | `Store[Meat]`, `Store[Leather]` |
| grain sacks at the farmhouse | the farm holds wheat | `Store[Wheat]` |
| sacks in the granary's yard | the granary holds food | its `Store`, the edible goods |
| crates, barrels, bales at the warehouse and the trading post | they hold anything | `Store.Held` |
| a made handcart at the warehouse | one is on its shelf | `Store[Handcart]` |
| baskets of goods at the market | it holds anything | `Store.Held` |
| the hay rick at the barn | the barn holds hay | `Store[Hay]` |
| a home's woodpile (its length) | the household holds firewood | `Household.Stockpile.Firewood` |
| a home's larder by the back door (barrels, sacks) | it holds food | the household's edible goods |
| the founders' cart's sacks and basket (§3.4) | it holds food | its `Store`, the edible goods |
| the founders' cart's crates (§3.4) | it holds tools | `Store[Tools]` |
| a cart parked at a home, the market, the trading post | they own one and nobody has it out | `HandcartUses`, as `DrawParkedCarts` reads it |

**Where the building keeps no stock** (the woodcutter's logs, the builder's planks and stone, the quarry's blocks, the
mine's ore, the forager's full baskets, the smith's iron, the bakery's firewood, the tailor's cloth and wool), **the pile
shows while the building is working** — `SimWorld.IdleNote(workplace) is null`, the same test as the idle ring — and an
idle hut's yard is its bare dressing. **Dressing** (a dock, a net frame, a sawhorse, a chopping block, a clay oven, a
quench barrel, beds of seedlings, a trough, paving, a notice board, a flag) is what the building *is* and always stands.

**Amounts are drawn in levels**, so a building is laid again only when a level moves (CLAUDE.md: nothing derivable rebuilt
per frame): 0 → none; 1–4 → 1; 5–19 → 2; 20–59 → 3; 60 + → 4 (`YardLevel`). A home's woodpile is as long as its level.

⛔ **No kitchen garden.** The sim has none yet (D357: *later, its own slice*); vegetable beds would draw food that is not
there. A home's back yard keeps grass where the garden will go.

## 3. The yards (look and layout)

**The building's ground in the game is unchanged;** with the tick on, the roof gives part of it to the yard. In the
building's own turned frame (x along its width, y along its depth), the roof is a smaller rectangle and the rest is
trodden earth with the yard's things on it:

| Kind (extent) | Roof (centre, size in tiles) | The yard |
|---|---|---|
| woodcutter, builder, hunter (2 × 1) | one tile, on the door's half (§3.1) | the other tile |
| smithy, quarry, mine, workshop, bakery, tailor, trading post (2 × 1) | 1.25–1.3 × 0.8, on the door's end | the last third |
| forager, forester, fishing hut (1 × 1) | 0.6 × 0.55, toward the back | the front, and the fishing hut's dock out over the water |
| granary, warehouse (2 × 2) | 1.75 × 1.15 at the back | a front strip |
| farmhouse (2 × 2) | 1.5 × 1.15 at the back, to one side | front strip and one side |
| barn (3 × 2) | 2.0 × 1.55, on the door's side | the last tile's width |
| library (2 × 2), town hall (3 × 2) | at the back | a paved walk / square at the front |
| market, stockpile, mill, well | as A85 | their things added round them |

**3.1 The door stays on the building.** The sim's door (`doors.md §12.2`) is on one wall of the whole footprint; the roof
is placed on the half or end nearest it, so the door step (A85) is always at the roof's own wall. A door in the middle of
a long wall puts the roof on the side the building's seed picks.

**3.2 Each kind's things** (the sketches' set; chunky enough to read at play zoom, small things for close up):
woodcutter — log pile, sawhorse with a bucksaw, chopping block with an axe, chips, a stump; fishing hut — a dock out
over the nearest water, a boat moored by it, a net drying on two poles, the fish rack, creels; hunter — a hide
stretched on a frame, the smoking rack, a stump; forager — three baskets (berries, mushrooms, herbs), a herb rack; forester
— two beds of seedlings; builder — planks, cut stone, a ladder, a tool rack; granary — the hoist's beam out over the yard,
the sacks under it, a ramp; warehouse — crates, barrels, bales, a made cart; market — A85's stalls with baskets, crates and
its cart; smithy — charcoal heap, quench barrel, iron bars, a cart wheel; quarry — white blocks, a barrow; mine — the ore
heap, rails and a tub; workshop — planks and a sawhorse; bakery — a clay bread oven and its firewood; tailor — dyed cloth
on a line (madder, woad, weld), wool bales; trading post — crates and barrels; farmhouse — grain sacks, a tool rack, a
barrow; mill — sacks at the door; barn — the hay rick, a trough; library — a paved walk, two benches, a sundial; town hall
— a paved square, a notice board, a flag; well — a bucket, a trough.

**3.3 The fishing hut's water** is the side of its footprint with water nearest, looked for on the map when the hut is
laid (four directions, up to two tiles out); none found, no dock. Laid again when `TerrainGeneration` moves.

**3.4 The founders' cart (A87).** Joe: *"the cart needs art - it's basically treated as a building"*, with a 3D render of a
market cart for inspiration (a plank bed with iron-strapped side rails, two big spoked wheels at the middle, shafts forward,
a canvas cover over the back, sacks, a crate and a basket of potatoes in it, a bucket hung at the back). A85 left it a
flat chip (`StoreKind.Cart` was *not a building*, §8 of `building-roofs.md`); it is drawn now, in its own frame on its
1 × 1 ground, seen straight down in the same pen and wash: a hatched shadow; the two wheels at the sides as an iron-tyred
rim with their hubs out; the bed in the carts' own colour (`HandcartColour`, as the handcarts, so every cart reads as one
family), its boards drawn along it, side rails with iron straps; two shafts and a crossbar forward; the canvas over the
back third, its folds in pen; a bucket at the back corner. **Its load follows §2:** sacks and an open basket while it
holds food, crates while it holds tools — an emptied cart is its bare boards.

## 4. Homes

**4.1 The yard is the household's plot** (`organic-housing.md §3.1`: 3 × 2 tiles, the house on the front row): every plot
tile the house does not stand on. The tiles are the sim's (`ZoneMap.PlotOwner`), so a plot clipped by the paint is a
smaller yard, and a house at any angle still dresses the tiles it owns.

**4.2 What is in it — the same things in every home, placed differently per household** (the household's id is the
seed; never `Rng`): the yard tiles ordered by distance from the door, the farthest first —
- the **privy** (a manor: a **woodshed**) in the farthest tile's far corner; a **sundial** for a manor;
- the **washing line** across the farthest tiles;
- the **woodpile** in the yard tile nearest the house's back, against the house, its length the firewood level (§2), and
  the **chopping block** beside it;
- the **larder** by the back door: barrels and a sack, as many as the food level (§2);
- a **water butt** at a corner of the house, a **bench** by the door.
The **cart** stands where `DrawParkedCarts` puts its chip today (beside the door, clear of the heap), drawn as a cart.

**4.3 The fences, by tier** (the walls the sim built, D404 — the same segments, the gate the same gap): the cabin's
**woven wattle** (a brown band, the weave ticked across it, a stake every third of a tile), the cottage's **post and rail**
(a rail, a post every 0.4 tile), the manor's **dry-stone wall** (A85's grey, a chain of stones). Laid once per fence
rebuild as one mesh (the outlines' own cache), never a line a frame.

## 5. Pen and wash (after Joe's reference)

- **The pen draws the material** on each roof face, in its own frame (along the eave, up the slope): shingle, slate, tile
  and stone slab as hand-drawn courses with uneven joints between them; thatch as strands down the slope with a dark
  fringe along the eave and a binding every so often; boards as the joints between boards; turf as tufts. Sepia ink
  (`Ink`), a pixel wide, faint (0.33–0.5).
- **The zoom decides how many:** a texture's strokes are never closer than **3 px** on screen — at play zoom the lines
  thin to every second or third, close up all of them (`RoofPiece.Spacing`; the line runs are laid per zoom already, A85).
- **The shade is hatched:** a face turned away from the light gets diagonal hatching, denser the darker it is; the cast
  shadow on the ground is hatched too.
- **The wash carries the colour:** the seven materials' tone maps keep only a third of their pattern and carry a mottled,
  blooming wash pooled at the eave instead (mean 0.8 held, A85 §3); the kind's colour is mixed 12 % toward the paper
  (`#efe6d0`), as a wash is the paper through the pigment.
- **The yard's ground** is an irregular patch of trodden earth, softened at its edge, with the pen's stipple, pebbles and
  tufts in it.

## 6. Code

`VillageMap.Yards.cs`: the yard table (`YardOf`), `YardState` (levels, working, water side, cart — what a building's yard
depends on, in its `RoofSignature`), the layouts and the things (pure, in the building's frame, so the probe reads them),
the home yard from the plot tiles, the fences' mesh, and the pen (`PenTexture`, `Hatch`, convex clipping). `RoofDrawing`
takes the yard; everything is laid into A85's cached runs, so a frame still hands each building over with one transform.

## 7. Probe: `building yards:`

Named from this spec (art traps 17, 25): each stock thing present only with its stock and absent at zero, the levels'
edges; the not-stocked piles present only while working; the dressing always; every yard thing inside the building's
ground (the dock and the boat excepted, over water); the roof on the door's side; a home's things only on its plot's
tiles and never under the house; the woodpile's length by level; no beds of vegetables; the fences by tier; the pen's
strokes inside their face, never closer than 3 px at any zoom, hatching only on faces darker than 1; the textures' mean
0.8 still; tick off, nothing.

## 7a. As built — measured

Tick off byte-identical to A85's build at five different views (Hollowmere, Larkspur). Probe: 120 ✅ 0 ⛔. Red-check: 29 mutants, 29 red, each a ⛔ line, none a crash, after two re-aimed: the first run scored 27 of 29 — *a sawhorse off the woodcutter's ground* and *the washing line off the plot* both moved their thing onto ground that is still the building's or the plot's (the sawhorse slid onto the roof's half, the line one tile over in the same plot), so the guard never saw a break; aimed past the yard's far end and past the plot's back fence they are red. The first aims are kept as zeros, and they show a gap written down: **a yard thing slid under its own roof is not caught.** Draw-time zeros, written down: the household fences drawn by tier in place of the line, the parked cart where its chip stood, the yard state read at each draw site (the reads and the table are probed, the call sites are not), the fishing hut's water found on the map, the home's yard tiles from its plot, the look of the things, the ground's soft edge and the wash textures — the renders are their evidence. Cost (4K SubViewport, Hollowmere's west end, tick on, this build / A85 / this / A85): `_Draw` 9.80 / 10.29 / 9.77 / 10.14 ms (cheaper than A85: the fills and the pen lines one mesh), GPU 9.14 / 9.13 / 9.16 / 9.39 ms (no dearer).

## 7b. ✅ A field stops at a building (A142; merged at his word, A144, `31a6682`)

Joe, 2026-11-21: *"if a farm field is placed under a farmhouse, the field shouldnt be seen where the farmhouse yard exists. it
presently looks like it is a visible layer below"*. A yard is its building's own ground (§3: the roof drawn smaller, the yard the
rest of the footprint), washed at half strength over whatever is under it — so a field painted under a farmhouse showed through.
`FieldStageDrawn(ground, standing)` draws no field stage on a tile a building stands on (`SimWorld.StandsOn`, the standing index);
the fields re-trace when it moves (`StandingGeneration`, D599). One line of `VillageMap.cs` asks it. Seen on his Ravenscar Y33
(farm 76's farmhouse on four field tiles, one sown): `D:\Projects\brushes\buildings\field_under_farmhouse_before_after.png`.
Probe `building yards:` — a field, sown or ripe tile under a building not drawn as field, in the open drawn as itself, grass never.
Red-check 3 mutants, 2 red; ⚠️ one zero written down (the map never asking what stands — no farm under a house in the probe's
game; the render is its evidence).

## 8. Not done

Roofs in snow; the card's picture and the glyphs; animals in yards; smoke; a kitchen garden (the sim's, first).
