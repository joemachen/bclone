# Spec: Tree sprites — the concept's trees, painted, in place of the inked discs

**Status:** ✍️ **SPECCED (A29, 2026-11-10); JOE'S FOUR CALLS ANSWERED (A30) — AND THE ANGLE REVERSED AT HIS WORD (A31): TREES ARE
DRAWN STRAIGHT DOWN, BECAUSE BUILDINGS MUST ROTATE. NOTHING BUILT — THE SHADOW ANSWERED (A33): PAINTED, A SEE-THROUGH WASH, THE ONE EXCEPTION TO THE ALPHA RULE. 🎨 THE ATLAS PAINTED IN PHOTOSHOP BY SCRIPT AT JOE'S WORD (A34), ALL EIGHT TREES INSIDE §4; ⭐ APPROVED BY JOE (A35: *"Approved, build piece 1"*). 🔨 PIECE 1 BUILT (A35): `textures/trees/tree_sprites.png`, the painted trees in place of the crowns behind the watercolour tick, the probe's `painted trees:` line; tick off byte-identical. ✅ JOE PLAYED IT (*"this is looking pretty!"*) AND ASKED FOR FOUR THINGS (A36): THREE MORE KINDS (conifer, birch, willow by the water — PAINTED AND BUILT, §8); THE TREES OVER EVERYTHING BUT THE GHOST (BUILT — §2 changed, a Non-Negotiable tension recorded); A *draw the trees* TICK (BUILT); TREES AT EVERY ZOOM (piece 3, BUILT, at full density). ⭐ APPROVED BY JOE (A37: *"Approved"*) — piece 1, the three kinds, the canopy over the village, the tick, the far view. ⭐ MERGED INTO `main` AT HIS WORD (A38, *"Merge art now"*: `f48cfab`), `main` merged back into `art`; pushed at his word (A39). Young trees (piece 2) — ✍️ SPECCED (A50, §10, Joe's *"Young trees"*); ✅ HIS THREE CALLS ANSWERED, ALL AS RECOMMENDED (A51); 🔨 BUILT (A51, §10): SIXTEEN PAINTED SAPLINGS IN EACH SEASON'S ATLAS, THREE A YOUNG TILE, EACH PLACE'S OWN KIND. ⭐ APPROVED BY JOE (A52, *"everything about saplings looks great"*); ⭐ MERGED INTO `main` AT HIS WORD (A55, `630f296`) AND PUSHED (A56). 🔨 BUILDING SITES DASHED THROUGH THE CANOPY (A53, §2, Joe's ask): BUILT; ⭐ APPROVED (A54, *"Approved"*); MERGED (A55, `630f296`) AND PUSHED (A56). ✍️ SEASONS (piece 4) SPECCED WITH §7'S WINTER AS ONE PIECE (A40, §9); ✅ HIS FOUR CALLS ANSWERED, ALL AS RECOMMENDED (A41); 🔨 BUILT (A41): THE AUTUMN AND WINTER ATLASES, THE CROSSFADE, THE WINTER GROUND. ⭐ APPROVED BY JOE (A42, *"Approved"*); ⭐ MERGED INTO `main` AND PUSHED AT HIS WORD (A43, *"Merge and push"*, `b04166c`).** Art lane, branch `art`. The terrain shader's §7 (*Toward the concept*) is what asked for it: Joe, A28, *"the
trees themselves have an outline, but the ground isn't outlined … is that fixed with an actual art asset?"* — for the
trees, yes. Owner: Joe + Claude Code.

---

## 1. What this is, and what it is not

Today a tree is drawn as a disc: 3–4 crowns a woods tile, 0.4 tiles across, one colour (`TreeCanopy` `#7d8c5c`), shown
only from 24 px a tile up (`TreeZoomFloor`); below that the woods are the painted woods floor alone. With the watercolour
tick on (A28) each disc is a lobed crown on its own ink outline with a lit side — **a stand-in**. The concept's trees are
**illustrations**: ink line and watercolour wash, about a tile across, clumped, overlapping. They are drawn at a
three-quarter angle with trunks showing — ⛔ **which this game does not take** (A31, Joe: *"we shouldnt draw at an angle.
buildings must rotate."*): an angled drawing cannot turn, and a house turns to any of 64 angles. So everything on the map
is drawn **straight down**, and the trees are painted crowns seen from above, keeping the concept's ink, wash and clumping.

This spec is the **format Joe's trees are painted to**, and how the game draws them. It replaces the crowns *only while
the watercolour tick is on*, as every terrain piece has; with the tick off the map is byte for byte what it is today.

**Not in this spec:** buildings, villagers, animals and other sprites — they follow the same convention, straight down
(`art-inventory.md` B4, answered A31); the far-view woods (below `TreeZoomFloor`, still the woods floor painting — §5, piece 3);
fruit and berries (`ForageShown`, its own row).

## 2. What the sim says, and must keep saying

- **A tree is a tile.** The sim knows `Forest` and `Sapling` tiles and nothing finer (`GeneratedMap.Terrain`). How many
  trees a tile shows, where, and which drawing is a **pure function of the tile** (`CanopyOn`'s rule, D337 — a `static`
  of the position, so a wood never shimmers and a place always looks the same). Nothing in this spec reads the tick, a
  clock or an RNG.
- **Felling is legible.** A felled tile loses its trees at once, and a sapling tile shows young trees. The chunk
  re-meshes as today (`VillageMap.Meshes.cs`, only the chunk whose tiles changed).
- ~~**Trees never hide people or buildings.** Scenery is drawn under everything that stands or walks (`_Draw`'s order), so
  a crown overhanging a neighbouring tile lies *under* a villager or a house there, as the woods' game does.~~ *(Reversed
  for the painted trees at Joe's word, A36: "buildings should be covered by trees"; asked what should stay above the
  canopy, he chose "Everything under".)* **With the watercolour tick on, the painted canopy is drawn over the whole
  village** — buildings, sites, heaps, carts, routes, animals, berries, villagers — and only what is in the player's hand
  stays above it: the selection, the placement ghost, the brushful, the before/after line. ⚠️ **A Non-Negotiable tension,
  flagged to Joe before building and recorded (DESIGN §1, legibility):** a villager walking or working in the woods —
  a woodcutter at their tree — is hidden under the canopy. The answer in the game is the *draw the trees* tick (§8), which
  shows the land and everyone on it. With the watercolour tick off the order is the old one, to the byte.
- ⭐ **A building site shows through the canopy, dashed** (A53, Joe, after placing a forager's hut in the woods and
  finding only its selection square: *"can we still show a 'dashed lined' version of construction placements 'under' the
  canopies so the user can see building placement?"*). With the canopy over the village, **every construction site's
  outline is drawn again after the trees, as a dashed line**: the same turned footprint and the same edge colour as its solid
  outline below (the site's grey; the clearing's orange while trees still stand on its ground, D350; the demolition red
  while it comes down). So a site under the trees reads as a dashed outline through the leaves, and one in the open
  is unchanged, since the dashes lie on its own solid edge. Only the outline comes through: not the fill that shows how
  far it is built, which stays under the trees with the building. A house's rebuild (D722) and a bridge's span are sites
  and come through too. ⚠️ **Dashed edges already mean a building that has fallen in** (D556), drawn in the building's
  own colour. These are in a site's colours, which no finished building uses, so the two are told apart by colour. Off
  while the trees are hidden or the watercolour is off (no canopy, nothing to see through). ~~⛔ A free building waiting
  on wooded ground (the systems lane's 🐞, A36) is not a site and still draws nothing.~~ ✅ It is drawn now, as a site
  in the clearing's orange, and comes through the canopy with the sites (D726, `footprints.md §7.6`).
  **As built (A53):** `VillageMap.SiteShapeOf` is the one place a site's turned footprint and colours are worked out,
  for its solid draw in `DrawWorkplaces` and for `SitesThroughTheCanopy`, which `DrawTheSitesThroughTheCanopy` dashes
  straight after the canopy (2 px, the fallen-in building's dash length). **Verified:** with a gatherer's hut marked in
  the woods and a granary in the open (a temporary hook, removed), watercolour-off renders at four zooms are byte-identical
  to the build before, which proves the shared shape. With the watercolour on, the only pixels that change are round the
  woods site: 119 at the opening zoom, 42 at the widest. The probe's `sites through the canopy:` line marks a site in the
  woods and checks its outline (its own 0.8-of-extent footprint, the clearing's orange), one outline per site, none with
  the trees hidden or the watercolour off, then takes it back. **Red-check:** 5 mutants, 4 red: outlines with no canopy over them; every site in the site's grey; the outline a quarter too big; only demolitions coming through. **One zero, written down:** the map never calling the dashed draw. The probe checks what would be drawn, not that `_Draw` draws it; the before/after renders are that call's evidence (the dashes through the fir).

## 3. The file Joe delivers

| | |
|---|---|
| **Name** | `tree_sprites.png` (one atlas), later `tree_sprites_young.png` and season sets (§5) |
| **Layout** | a grid of **256 × 256 px cells**, 4 across × 2 down to start (8 trees) — 1024 × 512, RGBA, 8 bits, sRGB, PNG, **transparent background** (straight alpha). **Since A36: 4 × 5, 1024 × 1280** — rows 1–2 the eight leafy trees, row 3 four conifers, row 4 four birches, row 5 four willows (§8) |
| **Scale** | **128 px of painting is one game tile** (the closest zoom is 48 px a tile, so a tree is drawn at 0.38× or less — sharp everywhere, mipmapped). A mature tree's **crown about 1.0–1.3 tiles across** (128–166 px) |
| **Anchor** | **the centre of each cell is the centre of the crown** (origin 0.5, 0.5). The game lays that point on the tree's place. ⚠️ Not `art/`'s asset sheet's 0.5, 1.0 — that is a ¾ convention (A31) |
| **Angle** | **Straight down — a crown seen from above, no trunk** (A31: the map's sprites must turn). **Light from the upper left**, so a crown's lit side is up-left and its shadow falls down-right |
| **The source** | PSDs stay in Joe's pack, outside the repo, as the grass's and the ground tiles' do; the repo gets the PNG and its `.import` (lossless, mipmapped — HANDOFF art trap 2) |

**How to paint it so it survives the game:**
- **Ink and wash, as the concept:** a sepia ink line round the crown's lobes (the A27 ink, `#3a2d20`), and a few inner
  strokes for the clumps of leaves; a watercolour wash inside, the lit side up-left warmer and lighter, the shadow side
  cooler and darker; a soft shadow cast down-right on the transparent ground (it lands on whatever ground is there) —
  **a see-through dark wash, the one exception to §4's alpha rule** (A33).
- **Eight different trees, not one tree eight times:** differing crown shapes, heights and lean. A wood is a few hundred of
  them; one memorable blot repeats.
- ⚠️ **The game does not turn a tree — the light is painted in.** A crown turned a quarter would carry its lit side round
  with it, and the wood would be lit from four directions. The one move that keeps the light where it is: **mirroring across
  the up-left to down-right diagonal** (the light's own line), which the game does by the tree's place, so each drawing has
  two faces and the eight drawings carry the rest of the variety. Turning freely is a later option only if the light is
  painted as a separate layer.
- **Leave a margin:** nothing within 8 px of a cell's edge (mipmaps bleed neighbours together).
- **Detail under ~6 px of painting is lost** at the widest zoom the trees show at (24 px a tile: the painting is drawn at
  0.19×).

## 4. The limits, checked rather than asked for

| rule | value | why |
|---|---|---|
| **The crown's middle tone at L\* ≥ 50**; ink and shadow (L\* < 50) **under 20 %** of a crown's pixels | `world-legibility.md §9` | a villager's dot (L\* ≤ 29) walking among trees must still read at 3 : 1 against the crowns round it; the dots are drawn over scenery, but the eye finds them by contrast |
| **The crowns' mean within the woods' hue family**, `TreeCanopy` `#7d8c5c`'s | the minimap and the far view use the palette | the woods must still read as the same thing at every zoom |
| **The crown's alpha: fully opaque or fully clear,** a 1–2 px soft edge at most; **the cast shadow is the one exception** — a see-through wash down-right of the crown, measured apart (A33) | the mipmaps | a large half-clear halo greys at the widest zoom; a shadow is meant to be half-clear, and greying fainter at the widest zoom is harmless |

**Measured, not eyeballed**, as the ground tiles were (`ground-textures.md §3`): each tree's crown L\* at p1, p50, p99,
its ink share, its mean ΔE to `TreeCanopy`, and `tools/legibility/check.py` on a scratch copy with the crowns sampled in
(art trap 16). A tree outside the limits goes to Joe with the numbers before anything is built on it.

## 5. How the game draws them, and the pieces

**The drawing (piece 1).** In `Canopies` (`VillageMap.Meshes.cs`), with the tick on, each tree becomes one textured quad
in the chunk's mesh instead of crowns:
- **Where:** the tree's foot at `CanopyOn`'s place; **how many:** `TreesOn` scaled for bigger trees — call 4 (the concept's
  are fewer and larger: 1–2 a tile).
- **Which drawing, and mirrored across the light's diagonal or not:** from the tile's own hash, so a tree is the same tree
  every time. (Mirroring across the up-left/down-right diagonal keeps the light where it was.)
- **Size:** the cell at 128 px a tile, × 0.85–1.1 by the same hash; a sapling tile draws the young set (piece 2), or the
  mature set at 0.55 until it exists.
- **Order:** as the crowns are laid today, tile by tile; seen from above, no tree stands in front of another.
- **Cost — leaner than now:** 6 vertices a tree against the stand-in's ~230 (A28) and the discs' 36, one atlas texture
  (~2.8 MB with mipmaps), one draw a chunk (`CanvasItemAddMesh` with the texture). Measured with `measure4k.gd` in one
  sitting, A/B against the stand-in.
- **Behind the tick:** off, `Canopies` draws the discs to the vertex as now (the A28 guard: tick off byte-identical).

| # | piece | starts when |
|---|---|---|
| 1 | **Mature trees** — the loader, the atlas, the quads, the sort; the probe lines (the import, the atlas's size, the tick reaching the meshes, off untouched) | the atlas arrives and is measured inside §4 |

**Piece 1, as built (A35).** `VillageMap.Watercolour.cs` loads the atlas on the tick's first switching on and wraps it in
a `CanvasTexture` drawn `LinearWithMipmaps` (the map's own filter is plain linear, and changing the map's would move every
other textured draw). `BuildChunk` lays each wooded tile's trees with `PaintedTrees` into **a mesh of their own per chunk**
(`SceneryChunk.Trees`), because a textured mesh cannot share a surface with the untextured boulders. `DrawTheTrees` draws
every visible chunk's tree mesh before the standing meshes, so the boulders stay over the leaves; it reaches 2 tiles past
the screen, where the crowns reached 1. `MeshBuilder.Sprite` lays a cell as 6 vertices with UVs, and transposes the cell for
the mirrored face; a builder refuses a mix of sprites and plain fills. Counts and choices are `static`s of the tile:
`PaintedTreesOn` (1–2 grown, 2 young), `PaintedTreeOn` (the drawing, the face, × 0.85–1.10, young × 0.55), and the
foot on `CanopyOn`'s place. ⛔ A missing atlas is said with `GD.PushError`, and the inked crowns stand in.

**Piece 1, verified (A35):** tick off, windowed renders at founding at four zooms (the opening, which is the closest, two
further out, the widest) byte-identical to `8f1f53e`; the probe's `painted trees:` line (sizes and counts named from this
spec, not from the code; a tile made young for the check); red-checked, 13 mutants, 12 red, with the one zero (trees 30 %
too big) fixed and red after; GPU at 4K, three back-to-back runs: tick on 2.02 and 1.94 ms with the painted trees against
2.43 ms with the inked crowns, tick off 0.262 ms in each, so no dearer than the stand-in. Vertices: 22,620 for 3,770 trees
against the crowns' 316,368 as discs.
| 2 | **Young trees** — ~~`tree_sprites_young.png`~~ a sixth row of half-size cells in each season's atlas (§10), the sapling tiles | 1 approved, the atlas arrives — ✍️ **SPECCED (A50, §10)**; 🔨 **BUILT (A51)** at Joe's *"All as recommended"*; ⭐ **approved (A52)** |
| 3 | **The far view** — trees below 24 px a tile, at a coarser density, so the woods are trees at every zoom as in the concept (cost measured first: the whole valley's trees on screen at once) | 1 approved — 🔨 **BUILT (A36) at full density**: the whole valley's 3,770 trees on screen cost +0.1–0.2 ms at 4K, inside the runs' own spread, so nothing is thinned; the mipmaps draw a tree a few pixels wide |
| 4 | **Seasons** — autumn tints, winter bare or capped (`terrain-shader.md §7` piece 4 asks the canopies to carry the winter) | after §7's winter piece — ✍️ **SPECCED WITH IT (A40, §9): one piece, winter and the trees; four calls Joe's** |

## 6. Joe's calls, put in plain words (A29)

✅ **ANSWERED (A30, 2026-11-10), all four as recommended:** ~~angled like the concept~~ ⛔ **reversed (A31): straight down —
*"we shouldnt draw at an angle. buildings must rotate."*** — when told the angle leans on how buildings are drawn; one leafy
kind first; Joe provides the eight drawings to §3 *(changed A34 at his word, *"use COMS to paint it in photoshop"*: Claude paints
them in Photoshop by script, as the ground tiles were — `D:\Projects\brushes\trees\build_tree_sprites.jsx`)*; fewer, bigger trees,
1–2 a tile. **Next: his eyes on the atlas (A34), then piece 1.**

✅ **ANSWERED (A33, 2026-11-10): (a), as recommended — Joe: *"option a"*.** The shadow is painted, a see-through wash, the one
exception (§3, §4). The call as it was put:

⏸️ **One more call before he paints (A32, found when the measuring was written):** §3 asks for *a soft shadow cast down-right on
the transparent ground*, and §4 says *alpha fully opaque or fully clear*. A shadow that lets the ground through is neither, so
the two cannot both hold. ⭐ **Recommended: (a) the shadow is the one exception.** Paint it as a soft, see-through dark wash.
The opaque-or-clear rule applies to the crown's own edge, and the measuring reports the shadow separately. This is the easiest
to paint, and the shadow falls on whatever ground lies beneath. **(b)** No painted shadow: the game draws one from each crown's
outline, shifted down-right. That keeps the alpha clean and gives one shadow everywhere, at the cost of more game work and one
more thing to tune. Either way, mirroring along the light's diagonal keeps the shadow down-right.

1. **The angle.** Trees drawn at a slight angle with their trunks showing, like the concept, standing on our flat
   top-down map — or straight down, crowns only. ⭐ **Recommended: the angle, like the concept.** Most hand-drawn maps do
   this. ⚠️ It decides the buildings too, when they get art (B4): angled sprites can't spin to the 64 turns a house can
   take today — a later call, but this one leans on it.
2. **What kinds.** ⭐ **Recommended: one leafy kind first** (eight drawings), conifers later as a second set. The sim has
   one kind of wood; a conifer stand would be decoration chosen by place, the same timber.
3. **Who draws them.** ⭐ **Recommended: Joe paints or sources them to §3** (Photoshop, as the ground tiles, or a generator
   he trusts), and the session measures, cuts and wires them. The procedural stand-in (A28) stays until then.
4. **How many a tile.** ⭐ **Recommended: fewer, bigger — 1–2 trees a tile, about a tile across**, as the concept has. The
   woods would read as clumps of trees rather than a carpet of dots. The sim doesn't change: trees are tiles.

## 8. Three more kinds, the canopy over the village, the trees tick (A36)

Joe, after playing piece 1: *"I want you to make more types of tree sprites to show different tree types/shapes
(conifers, etc) - like 2-3 more types for a nice variety."* Asked which, he chose all three recommended: **conifers,
birch, willow by the water**. A30's call 2 (*one leafy kind first, conifers later*) is the "later".

**Painted** in Photoshop by script (`D:\Projects\brushes\trees\build_tree_kinds.jsx`, the leafy trees' layers, so the
four read as one hand), four of each, measured inside §4:
- **Conifer** — a fir from above: three tiers of shaggy spikes, a ridge to each, blue-green; ~0.75–0.9 tiles.
- **Birch** — small and airy: a loose cluster of little lobes round a slight middle, light yellow-green, a finer pen;
  ~0.65–0.8 tiles.
- **Willow** — one soft pale mass with a fringe of hanging lobes and strands falling from the middle; ~1.1–1.25 tiles.

**Placed by place, never at random** (`KindOf`, a `static` of the tile and its neighbours — D337): a waterside wooded tile
(water any of eight round it) grows willows (60 % of its trees); conifers stand where a slow value-noise field over the
valley (lattice 9 tiles) runs above 0.62, so they come in groves with ragged edges; a wood's edge tile (open ground any of
four round it) grows birches (40 %); leafy everywhere else. The sim has one kind of wood: the same timber, the same yield.
In the founding valley: 28 % conifers, 151 willows, 387 birches, of 3,770 trees. A fell re-meshes the chunks beside it
when the painted trees are on, since a neighbour's kind can change with it.

**The canopy over the village** (§2, changed). **The *draw the trees* tick** in Settings, on by default: off, no tree is
meshed in either look — the land, the boulders and the berries show. **Every zoom** (piece 3): the painted trees have no
zoom floor.

## 9. Through the year — winter and the trees (A40, piece 4 with `terrain-shader.md §7` piece 4)

**Why.** Joe, asked what next, chose *"Winter and the trees"*. His own Year 8 winter screenshot shows it: the valley goes
white and every tree stays its summer green, pasted on the snow. And the snow itself is still A23's *"white blanket"*
for most of winter, which A26 call 3 already replaced on paper.

**What is already settled (A26, not asked again):** the winter ground is a drawing, not a blanket. **Patchy cover with a
peak about 0.7–0.8** (measured and shown to Joe); **cool blue-grey shadow washes** in the snow; **the river dark with an
icy rim**; **the ink kept over the snow** (the bank's pen line already draws over it). All in the valley shader, behind
the watercolour tick, as every §7 piece.

**What is new — the trees through the year.** The painted trees change with the season by **swapping the atlas they are
drawn with**, never by re-meshing: the meshes, the places, the kinds and the mirroring stay as they are. Each season set is
the same 1024 × 1280 grid as `tree_sprites.png`, cell for cell, painted by script from the same shapes (so a tree keeps its
outline from summer into autumn and its place into winter):

| file | when | what changes |
|---|---|---|
| `tree_sprites.png` | spring, summer | — (A34, A36) |
| `tree_sprites_autumn.png` | autumn | leafy russet, orange and gold, mixed by tree; birches gold; willows pale yellow; conifers as summer |
| `tree_sprites_winter.png` | winter | leafy, birch and willow bare (calls 1); conifers snow-laden (call 2) |

**How a season turns (call 4).** The valley's year is `TheYearAt` (a season holds for its first two thirds, then turns
over the last third, the grass and the snow on the same clock). The trees use the same clock: while a season turns, each
chunk is drawn twice, the outgoing atlas, then the incoming one over it at the turn's share. Outside a turn there is one
draw a chunk, as now. **Cost:** two more atlases, about 7 MB of video memory each with mipmaps, loaded the first time
autumn arrives (A23's leanness); two draws a chunk only during a turn — measured at 4K, A/B, before it is called done.

**Winter's legibility bonus.** Bare deciduous woods are see-through: the land, the paths and the villagers under the
trees show again in winter. That eases A36's tension (the canopy hides a villager in the woods) for a quarter of the year,
and is the concept's own winter.

**§4's limits per season.** The middle tone at L\* ≥ 50 and ink and shadow under 20 % hold for every set. Autumn's hue is
its own, not the woods' (that is the point). A bare tree is mostly transparent: its limits are read on its branches and
its snow, and `measure_trees.py` reports the share of the cell that is tree.

**Not in this piece:** spring blossom or a fresh-green spring set (spring draws the summer set); felled stumps; falling
leaves or falling snow (weather is its own layer, A23's *"a later piece if he asks for it"*); the young trees (piece 2).

✅ **Answered (A41), all four as recommended:** bare with a little snow; snow on the conifers; an autumn set; a slow
crossfade.

**As built (A41).** `textures/trees/tree_sprites_autumn.png` and `tree_sprites_winter.png`, each 1024 × 1280, the summer
grid. They are painted by the same scripts from the same seeds (`build_tree_sprites.jsx` and `build_tree_kinds.jsx` with
a season argument; winter's bare trees and conifer snow in `bare_trees.jsxinc`), so autumn's footprint is the summer one
to the pixel. **Autumn:** the leafy trees russet, orange, gold, amber and one half-turned olive; birches gold; willows
pale yellow; conifers as summer. **Winter:** leafy, birch and willow bare — an ink trunk and limbs out to where the summer
lobes were, each limb bent once, twigs at the ends, snow along the limbs' upper-left, a faint see-through twig haze and a
lacy shadow. The willow's twigs hang as golden strands. A bare tree is 9–18 % as opaque as its summer crown. Conifers
keep their crowns with snow on each tier's upper-left rim. §4 holds for every autumn tree and every winter conifer.
**Drawn:** `TreeSeasonAt` (a `static` of `TheYearAt`'s season) picks the set and, while a season turns, the next set over
it at the turn's share. Into winter the old crowns fade out as the branches come in. Spring draws the summer set. The
atlases are loaded on first need.
**The winter ground:** `valley_watercolour.tres` gains `SnowPeak` 0.75 (about 70 % of the land under snow at midwinter, measured
in a render), a drift's lee washed `SnowShadowColour` at 0.55, an `IceColour` rim 0.35 tiles inside the bank at 0.75, and
the river 20 % darker. The ink still draws over all of it.

### Joe's calls (A40), in plain words, each with a recommendation

1. **The leafy trees, birches and willows in winter.** ⭐ **Recommended: bare** — an ink drawing of the branches seen from
   above, a little snow lying on their upper-left sides, the ground showing through. The alternatives: their summer
   crowns with snow on top (green leaves in winter), or bare with no snow at all.
2. **The conifers in winter.** ⭐ **Recommended: snow on them** — white lying on each tier's lit side, still dark green
   underneath, so the firs are the dark shapes in a white valley, as in the concept. Or unchanged.
3. **Autumn.** ⭐ **Recommended: an autumn set** — the leafy trees turn russet, orange and gold (each tree its own), the
   birches gold, the willows pale yellow, the conifers stay green. Or no autumn: summer straight to winter.
4. **How the change shows.** ⭐ **Recommended: a slow crossfade over the season's turn** — the last third of a season, the
   same days the grass and the snow turn, so the woods colour and then bare over about ten days. Or a switch on one day.

## 10. Young trees (A50, piece 2)

**Why.** Joe, asked what next after the river, chose *"Young trees"*. Today a sapling tile, whether a forester planted it
or it seeded itself at a wood's edge, draws **two of the grown trees shrunk to 0.55** (§5). From a few zooms out a young
stand is just a thinner wood, so you can't see where the foresters have been or where a cleared wood is coming back.
§2 has promised since A29 that *"a sapling tile shows young trees"*. This piece keeps that promise.

**What the sim says (unchanged).** A tile is `Sapling` or `Forest`, with nothing in between. A cleared tile next to wood
becomes a sapling after one regrowth period (72 days, `regrowth_period_days`) and a grown wood after a second; a forester's
planting is a sapling at once. **So a young stand lasts about 72 days**, more than half a year on the 120-day calendar,
and usually sees a change of season. The view reads the tile and nothing else. Which young tree stands where is a
`static` of the tile, as the grown ones are (D337): no tick, no clock, no RNG.

**What it looks like.** Saplings, not small trees: **a few slender young crowns a tile** (call 1), each **about 0.3–0.5
tiles across**, drawn in the same ink and wash as the grown trees but in a **fresher, lighter green** with a finer pen,
with the young-wood ground (`#b3b98d`) showing between them. The point is that a young stand reads as young at a glance:
smaller, lighter and gappier than the wood around it. **Each place grows its own kind** (call 2): young firs in a fir
grove, young birches at a wood's edge, young willows by the water, by the same `KindOf` the grown trees use. So a felled
fir grove comes back as little firs and then as firs. **Through the year** (call 3), the young trees turn with the grown
ones: gold in autumn, bare whips with a little snow in winter, and the little firs keep their green under snow.

**The painting.** By script in Photoshop, as the grown trees were (`D:\Projects\brushes\trees\`, a new
`build_young_trees.jsx` from the same layers and seeds), measured inside §4 before it is built on:
- **16 drawings: four of each kind** (leafy, conifer, birch, willow), each with two faces by mirroring across the light's
  diagonal, as the grown trees.
- **Half-size cells:** a sapling needs at most a tile of painting with its shadow and margin, so the young trees sit in
  **128 × 128 cells, eight across**, at the same 128 px a tile. Sixteen drawings take one 256-px strip.
- **They go into the atlases the grown trees already use, as a sixth row:** `tree_sprites.png`, `_autumn` and `_winter`
  each grow from 1024 × 1280 to **1024 × 1536**. So the young trees ride the same mesh, the same single draw a chunk and
  the same seasonal crossfade, with no new texture or draw. The cost is about 1.4 MB more video memory per atlas with
  mipmaps; the GPU is measured at 4K, A/B, before it is called done.
- **§4's limits hold for every young drawing:** the middle tone at L\* ≥ 50 and ink and shadow under 20 %. A young
  tree's green is lighter than the grown trees' but stays in the woods' hue family, so the minimap still reads the woods.

**In the game.** `PaintedTreesOn` gives a young tile its count from call 1, placed by `CanopyOn`, and `PaintedTreeOn`
picks a young cell by kind at its painted size (no 0.55 shrink). `MeshBuilder.Sprite` learns the half-size cell. The
probe's `painted trees:` line names the new atlas size, the young cells, and a tile made young drawing young cells and
not grown ones. **Behind the watercolour tick and the *draw the trees* tick, as every tree:** with the watercolour tick
off, the saplings stay the inked discs they are today, and the renders compare byte for byte.

**Not in this piece:** a tree that grows gradually from sapling to wood (the sim has one step, so the view does too:
the day a sapling tile becomes forest, its young trees give way to grown ones); planted rows that tell a forester's
saplings from self-seeded ones (the sim keeps that difference only until the next regrowth sweep); stumps on a felled
tile; spring blossom.

### Joe's calls (A50), in plain words, each with a recommendation

1. **How many young trees a tile.** ⭐ **Recommended: three, each a third to half a tile across.** That's enough to read
   as a stand of saplings, with the young-wood ground showing between them. Or **two** (as today's stand-in, but painted
   young), or **four or five tiny ones**, like a nursery bed.
2. **Young trees by kind.** ⭐ **Recommended: each place grows the young of its own kind**: little firs in the fir groves,
   young birches at the edges, young willows by the water. A felled grove then grows back as what it was. Or **one
   sapling drawing for everywhere**, which is simpler and the same green throughout.
3. **Young trees through the year.** ⭐ **Recommended: they turn with the grown trees**, gold in autumn, bare in winter
   with a little snow, the little firs snowy. A young stand usually lives through a change of season, so summer-green
   saplings in a white valley would be the problem the winter piece just fixed. Or **summer green all year**.

✅ **Answered (A51): *"All as recommended"*** — three a tile; each place's own kind; turning with the year.

**As built (A51, 2026-11-11).**
- **Painted** by `D:\Projects\brushes\trees\build_young_trees.jsx` (seed 51, a season argument; winter's bare saplings and
  the little firs' snow from `bare_trees.jsxinc`). A first painting broke §4: a 2 px outline round a crown a third the
  size ran 27–33 % ink on the firs and birches. Fixed by **a finer pen** (1 px on the firs and birches, 2 px on the leafy
  and willow saplings, against the grown trees' 3 and 2), crowns a little larger (leafy 0.38–0.48 tiles across, firs
  0.31–0.41, birches 0.30–0.38, willows 0.38–0.45), **no inner ink arcs** (a sapling is too small to carry them), and
  lighter edge pooling. **Measured:** every summer and autumn sapling and every winter fir is inside §4, with crown L\*
  p50 62–75 and ink under 20 % (the worst 19.9 %). The bare winter saplings are read as the grown bare trees were (§9):
  mostly transparent, 5–19 % of their cell is tree.
- **The atlases:** `tree_sprites.png`, `_autumn`, `_winter` each 1024 × 1536, the grown rows unchanged byte for byte
  and the young strip below them. `MeshBuilder.Sprite` reads cells 20–35 as half-size cells, eight across, in the last
  row. `PaintedTreesOn` gives a young tile `YoungTreesOnATile` (3), and `PaintedTreeOn` gives cell
  `20 + kind × 4 + variant` at `YoungTreeHalf` (0.5 tiles) × 0.85–1.10. The 0.55 shrink is gone.
- **The probe's `painted trees:` line** makes every sixth wooded tile young for the check (419 tiles, 1,257 saplings) and
  puts them back. It checks the new size (1024 × 1536 for all three atlases), three young cells a young tile, each a whole
  half-size cell at 0.85–1.10 tiles across, each on a young tile, each kind in its place (young willows by water, young
  birches at edges), and all 36 drawings used.

**Verified (A51):**
- **Watercolour off:** windowed renders at four zooms are byte-identical to the build before (`552bf77`).
- **Watercolour on, at founding** (no young wood): not byte-identical. Up to 2 levels of 255 move on 0.2–0.4 % of pixels,
  because the grown trees' texture coordinates are now fractions of a taller atlas and its smallest mipmap levels differ.
  The grown trees' pixels in the atlas are unchanged.
- **The look:** a render of the founding valley with blocks of the woods made young (a temporary hook, removed before
  the commit). The young stands read as young at the opening zoom and from further out: smaller, lighter, the ground
  showing between them.
- **GPU, 4K, the 3070:** the same blocks of young wood against none, runs paired A B A B. The floors with young wood are
  1.81–1.87 ms, inside the 1.83–2.05 ms of the runs without it, so no cost can be measured. The switch stays under
  A23's 3.5 ms.
- **Red-check:** 9 mutants, 9 red, no zero: three saplings cut to two; young tiles drawing grown cells and grown tiles drawing young ones; every sapling leafy whatever its place (caught only by *24 of 36 drawings used*, since the place checks cover willows and birches); saplings 20 % too big, or at a grown tree's size; young cells laid four across, or read from the top of the atlas; a mirrored sapling's cell stretched. Each read on the `painted trees:` line, the break counted landed once.

## 7. Verification (each piece)

- **Tick off:** windowed renders at the opening zoom byte-identical to the build before the piece (`render.gd`, cmp).
- **The probe:** the atlas imported lossless and mipmapped at 1024 × 512; the tick reaching the meshes (vertex counts
  that match the tree count × 6); off, the discs' count as today; red-checked, reds counted (D326).
- **The limits (§4)**, measured and `check.py` read-only on a scratch copy.
- **The GPU:** `measure4k.gd`, the trees' piece A/B against the stand-in in one sitting (HANDOFF art trap 22).
- **The four checks** in `CLAUDE.md`; Joe's eyes; merged only at his word.
