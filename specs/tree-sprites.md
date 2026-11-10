# Spec: Tree sprites — the concept's trees, painted, in place of the inked discs

**Status:** ✍️ **SPECCED (A29, 2026-11-10); JOE'S FOUR CALLS ANSWERED (A30) — AND THE ANGLE REVERSED AT HIS WORD (A31): TREES ARE
DRAWN STRAIGHT DOWN, BECAUSE BUILDINGS MUST ROTATE. NOTHING PAINTED OR BUILT — ⏸️ WAITING ON JOE'S ATLAS (§3), AND ON ONE CALL BEFORE HE PAINTS IT: THE SHADOW (§6, A32).** Art lane, branch `art`. The terrain shader's §7 (*Toward the concept*) is what asked for it: Joe, A28, *"the
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
- **Trees never hide people or buildings.** Scenery is drawn under everything that stands or walks (`_Draw`'s order), so
  a crown overhanging a neighbouring tile lies *under* a villager or a house there, as the woods' game does.

## 3. The file Joe delivers

| | |
|---|---|
| **Name** | `tree_sprites.png` (one atlas), later `tree_sprites_young.png` and season sets (§5) |
| **Layout** | a grid of **256 × 256 px cells**, 4 across × 2 down to start (8 trees) — 1024 × 512, RGBA, 8 bits, sRGB, PNG, **transparent background** (straight alpha) |
| **Scale** | **128 px of painting is one game tile** (the closest zoom is 48 px a tile, so a tree is drawn at 0.38× or less — sharp everywhere, mipmapped). A mature tree's **crown about 1.0–1.3 tiles across** (128–166 px) |
| **Anchor** | **the centre of each cell is the centre of the crown** (origin 0.5, 0.5). The game lays that point on the tree's place. ⚠️ Not `art/`'s asset sheet's 0.5, 1.0 — that is a ¾ convention (A31) |
| **Angle** | **Straight down — a crown seen from above, no trunk** (A31: the map's sprites must turn). **Light from the upper left**, so a crown's lit side is up-left and its shadow falls down-right |
| **The source** | PSDs stay in Joe's pack, outside the repo, as the grass's and the ground tiles' do; the repo gets the PNG and its `.import` (lossless, mipmapped — HANDOFF art trap 2) |

**How to paint it so it survives the game:**
- **Ink and wash, as the concept:** a sepia ink line round the crown's lobes (the A27 ink, `#3a2d20`), and a few inner
  strokes for the clumps of leaves; a watercolour wash inside, the lit side up-left warmer and lighter, the shadow side
  cooler and darker; a soft shadow cast down-right on the transparent ground (it lands on whatever ground is there).
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
| **Alpha: fully opaque or fully clear,** a 1–2 px soft edge at most | the mipmaps | a large half-clear halo greys at the widest zoom |

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
| 2 | **Young trees** — `tree_sprites_young.png`, the sapling tiles | 1 approved, the atlas arrives |
| 3 | **The far view** — trees below 24 px a tile, at a coarser density, so the woods are trees at every zoom as in the concept (cost measured first: the whole valley's trees on screen at once) | 1 approved |
| 4 | **Seasons** — autumn tints, winter bare or capped (`terrain-shader.md §7` piece 4 asks the canopies to carry the winter) | after §7's winter piece |

## 6. Joe's calls, put in plain words (A29)

✅ **ANSWERED (A30, 2026-11-10), all four as recommended:** ~~angled like the concept~~ ⛔ **reversed (A31): straight down —
*"we shouldnt draw at an angle. buildings must rotate."*** — when told the angle leans on how buildings are drawn; one leafy
kind first; Joe provides the eight drawings to §3; fewer, bigger trees, 1–2 a tile. **Next: his atlas, then piece 1.**

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

## 7. Verification (each piece)

- **Tick off:** windowed renders at the opening zoom byte-identical to the build before the piece (`render.gd`, cmp).
- **The probe:** the atlas imported lossless and mipmapped at 1024 × 512; the tick reaching the meshes (vertex counts
  that match the tree count × 6); off, the discs' count as today; red-checked, reds counted (D326).
- **The limits (§4)**, measured and `check.py` read-only on a scratch copy.
- **The GPU:** `measure4k.gd`, the trees' piece A/B against the stand-in in one sitting (HANDOFF art trap 22).
- **The four checks** in `CLAUDE.md`; Joe's eyes; merged only at his word.
