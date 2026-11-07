# Spec: The terrain shader — the valley drawn over parchment

**Status:** 🔨 **PIECE 1 OF 4 BUILT IN THE LAB (the fake coverage map, D701, 2026-11-06), JOE HAS NOT LOOKED AT IT YET;
PIECES 2–4 SPECCED (D700), NOT BUILT.** §2–§4 are the build; §4's table says which piece stands.
▶️ **The first slice of the art lane (D699): built and tuned in the Grass Lab, against a fake coverage map; not wired into
the valley (`VillageMap`'s `DrawTextureRect`, `ValleyTexture`'s bake) until house tiers merge (§5).** Joe's eyes are the
test for the look; nothing here is approved until he says so, and the approved grass look (`grass-look.md`) does not move.
The terrain shader is the next slice after the world colours' legibility (`world-legibility.md`). Owner: Joe + Claude
Code.

## 1. What is already settled

- **The ground is parchment** (D635, `seasonal-grass.md §6.0`). The grass is Joe's pack, Multiply over it, at the look
  in `src/Bclone.Game/looks/grass_approved.tres` (`grass-look.md`).
- **One `canvas_item` shader on the valley's existing `DrawTextureRect`, through a coverage map** (D628,
  `seasonal-grass.md §6.6`, option A). `ValleyTexture`'s bake also writes a grass-coverage channel. Season and jitter are
  uniforms set per frame, never a re-bake (D338).
- **No linear conversion:** with `hdr_2d` off, Godot blends 2D in sRGB, so Multiply on sRGB values is Photoshop's
  Multiply (`seasonal-grass.md §6.2`).
- **The year:** each season holds its palette for 20 days, then blends into the next over 10 (`seasonal-grass.md §6.4`).
- **The value band:** the ground and every ground kind at L\* ≥ 50 (`world-legibility.md §5`, adopted D637). Since D637
  the valley's ground is **flat parchment** and the ground kinds sit in the band (`VillageMap.cs`'s palette;
  `world-legibility.md §9`) — this shader lays the grass over that, and may refine the ground kinds; `check.py` holds
  whatever it picks to the band.

## 2. ⚠️ Snow cannot be Multiply — it needs its own blend

**Multiply only darkens:** the result is never lighter than the parchment under it (L\* ~86, `#e6d6b0`). Snow is lighter
than parchment, near white at L\* ~95, so a Multiply layer cannot produce it at any strength. The pack's own winter palette
is Multiply too, so it reads as dormant ground, not snow (L\* 55–84, `world-legibility.md §2`).

So snow is **its own layer with its own blend**, on top of the grass result and under the paper grain. The lab builds
all three blends behind one dropdown, so they are compared on the same ground:

- **Lerp** — `mix(ground, snow, coverage)`. It covers whatever is beneath, grass and parchment alike, and it is the
  simplest to keep legible. ⭐ **The recommendation, unless Joe's eye says otherwise.**
- **Screen** — `1 − (1 − ground)(1 − snow × coverage)`. Brightens without fully covering: over the darkest grass it lifts
  part of the way, so pooled grass shows through as grey patches. Plausible as thin snow, wrong as deep snow.
- **Lighten** — `max(ground, snow × coverage)`, per channel. Nothing beneath it survives where snow is brighter; between
  the two as a look, and the hardest edge of the three.

**The snow colour** is `#f4f1ea`, L\* 95.2: the near white §2 asks for, warmed to sit on the paper rather than read as
screen white. It lives in `looks/terrain_lab.tres`, not in the shader.

**The questions this section carried, answered:**

- **Where coverage comes from.** ⚠️ **The sim has no snow and no weather** (searched 2026-11-06: `snow` appears only in
  a comment), so today there is nothing to read but the season. Coverage is therefore two things, kept apart: an
  **amount** (0–1, one uniform, one number for the whole valley) and a **place** (a threshold per position, from the
  same integer-hash noise as the macro variation). A place is white once the amount passes its threshold, so snow arrives
  in patches and closes up, and thin snow is always in the same places. `coverage = smoothstep(t − s, t + s, amount)`,
  with `s` the *Snow patchiness* slider. In the lab the amount is a slider. In the game it is a pure function of the
  season fraction the view already works out (`seasonal-grass.md §6.4`), so a place and a time always give the same
  picture; if Pillar 2.3's climate drift ever gives the sim a winter's severity, the view reads it and never writes it.
- **Whether the paper grain sits over snow.** **Yes.** The grain is a Multiply, so over snow it stays correct, and snow
  without tooth would be the one thing on the page that is not on paper.
- **Water.** Snow does not lie on the river: coverage is scaled by the water's weight going away. A frozen river is a
  different picture and a sim question (can it be crossed?), not this slice's.
- **What snow does to the value band.** It raises the ground's light end. Dark and ink-outlined things gain contrast; any
  light fill loses what little it had — the adult villager's dot (`#e8e2d4`) first. The placeholders in the lab show this
  at once. `tools/legibility/check.py` is the systems lane's this month, so it is run read-only on a scratch copy with a
  snow ground, and what it says goes in §6 before snow ships.

## 3. The rest of the shader

### 3.1 Paper grain — anchored to the land

⭐ **Anchored to the world, with the strength the lab's existing *Paper grain* slider.** The placeholder hashes screen
pixels, so in a game that pans the paper would stay still while the valley slid under it: a dirty window, not a drawing.
Built as a switch on the same overlay (`paper_grain.gdshader`): tile space in place of `FRAGCOORD`, the size a *Grain
scale* slider in tiles, integer hashes (the sine hash is not the same on every GPU, and §6 asks that a place always looks
the same).

- **The trade, to be looked at:** world-anchored grain magnifies with the zoom, 3.7× between the widest view (13 px a
  tile) and the closest (48). Fibres that read at one end are mush or sand at the other. Each octave therefore fades out
  as it nears a pixel (below two pixels a cycle) rather than aliasing, and the *Grain scale* slider is where Joe finds
  the size that holds up across the range.
- ⚠️ **The lab must pan to show any of this.** It never has (`seasonal-grass.md §5.2`). Drag to pan arrives with this
  piece: a view control like the zoom, moving grass, village and map together, left where it is by *Reset*.
- **Over the village or under it.** The lab's grain lies over the placeholders too (D630). In the game that is an overlay
  above the map and below the UI, not a line in this shader; *Grain over the village* stays on as D630 has it, and off
  draws it under the placeholders so the two can be compared.

### 3.2 The ground kinds — flat fills, and how they meet

The ground kinds stay **flat fills in the band, in `VillageMap`'s palette**, and only the river's depth comes across from
`ValleyTexture.Dressed` (shallows at the bank, the same contour as the fill). Washes and textures per kind are Joe's
paint, not this slice's (`art-inventory.md §2`); the shader takes them as a later input without changing shape.

Where they meet is two separate questions:

- **Splat blending — how wide the transition is.** The bake already weighs every kind at every boundary pixel and throws
  all but the winner away (`ValleyTexture.Winner`). The fake map keeps the weights, and the shader sharpens them, `k =
  w^sharpness / Σ w^sharpness`, the same power the hex tiling uses: 48 is the bake's hard contour, 1 a wide wash.
  *Splat sharpness* is the slider, off is the hard contour.
- **Edge bleed — how irregular the transition is.** Pigment running past the line on damp paper: the sample point
  for the weights is warped by a noise of its own, *Bleed reach* tiles at most (0–0.5), at *Bleed scale* tiles a cycle.
- ⛔ **Water keeps its contour.** D342 paid for this: a river drawn a tile from its tiles puts villagers on water, and
  §1.1 fails. Bleed runs between land kinds only, and the water's own weight is always read at the unwarped point, so
  no slider can move the bank. D342's other rule binds it too: a warp octave's wavelength must exceed the narrowest
  feature it may touch, and the river is two tiles wide, which is why the reach stops at half a tile.

### 3.3 GPU cost

Re-measured with `tools/grass/lab_cost.gd`'s method as each layer lands (`grass-look.md §5`, open item 4). The tool's
cases are fixed and it is not this lane's file, so the new cases run from a scratch copy; the numbers go in §6 beside
`grass-look.md`'s table. Bar: the D632 budget's size, about +0.1 ms a frame at 1280 × 800 for the whole stack.

⚠️ **Compare inside one session only.** On 2026-11-06 the same RTX 3070 read *approved, no grain* at **9.1 ms at 4K**
against D632's ~1.7 (0.77 at 2560 × 1440, scaled), about five times higher, and **the shader as it stood before this
slice read the same** (A/B/A: 9.14 / 9.34 / 9.14 widest, 9.06 / 9.25 / 9.07 gameplay), so it is not this work. Why is not
known (nothing else was on the GPU; the rounds repeat to three figures, which D632's did not). Until someone finds out,
§3.3's numbers are differences, not absolutes, and `grass-look.md §5`'s table cannot be compared with them.

| layer, against *approved, no grain* in the same run | widest, 4K | gameplay, 4K | as a share |
|---|---|---|---|
| **coverage map** (D701) | +0.50 ms | +0.49 ms | **+5 %** (+0.06 ms at 1280 × 800 on today's scale) |

## 4. The build — four pieces in the lab, one commit each

Each piece is droppable: the branch is whole after any of them, and Joe can look at one and leave for a week.

| # | Piece | Controls (the *Terrain* panel) | State |
|---|---|---|---|
| 1 | **The fake coverage map** (`GrassLabGround.cs`) | *Coverage map* switch · *Coverage fade* | 🔨 built (D701), Joe's eyes pending |
| 2 | **World-anchored paper grain**, and drag to pan | *World-anchored grain* switch · *Grain scale* · *Grain over the village* | ⬜ |
| 3 | **Snow** | *Snow* switch · *Snow blend* (Lerp, Screen, Lighten) · *Snow amount* · *Snow patchiness* | ⬜ |
| 4 | **Splat blending and edge bleed** | *Splat blending* switch · *Splat sharpness* · *Edge bleed* switch · *Bleed reach* · *Bleed scale* | ⬜ |

**The fake map stands in for the bake, and is shaped like it.** One RGBA8 image, 8 px a tile, 120 × 80 tiles centred on
the lab's origin, bilinear, clamped: **R grass, G wood, B stone, A water**, weights summing to one, off the map all grass
(as `ValleyTexture.Blended` reads it). R is the coverage channel the bake will write. It is laid out as a test scene
rather than a valley, because the lab's village is fixed: the placeholders stand on open grass, their wood stands on a
wood, the river runs below the granary at the real two tiles wide, and woods and stone meet the river and each other,
so every junction is on screen at the lab's opening zoom. Its edges wander by `ValleyTexture`'s own three warp octaves.
It is a pure function of the tile: integer hashes, no RNG, no clock, no sim. Baked once on first switching it on, 0.44 s
(measured), so the lab opens without paying for it.

**Coverage** is `smoothstep(0.5, 0.5 + fade, R)`: nothing where grass does not win, rising from the contour inward,
as `Winner`'s confidence does. Grass never lands on water or a wood. The grass term becomes `mix(1, grass, strength ×
coverage)`, so at coverage 1 it is the approved sum exactly.

**Rules for every piece:**

- **Behind a switch, off at opening.** All off, the lab draws the approved look byte for byte (§6's comparison).
- **`grass_approved.tres` and `GrassLook` do not change.** The options' starting values live in their own file,
  `looks/terrain_lab.tres`, backed by `GrassLabTerrain.cs`, which like `GrassLook` gives its properties no defaults. It is
  a lab file and says so: nothing in it is approved.
- **Reset to approved** puts every terrain control back to that file, and so every switch off, and `DiffersFrom` still
  finds the look exactly.
- **Copy current values** says Joe's ten lines unchanged, then a separate *TERRAIN LAB* list, so a set can be pasted back.
- **The panel:** *Terrain* shares the top-right corner with *Anti-tiling* (three panels do not fit 1280 wide); **t** or
  the button swaps them, **h** still hides everything, and hiding the left panel hides this one, so `lab_render.gd` and
  `lab_cost.gd` work unchanged. Their controls answer to the names above.
- **The shaders:** the functions in a new `shaders/terrain_lab.gdshaderinc`, pure functions of place, called from
  `grass_lab.gdshader`. The pack's snippet and `grass_antitile.gdshaderinc` are not edited.
- **A probe line per control** in `GrassLab.SelfCheck`, each `[widths] terrain lab, …`.

## 5. What waits for house tiers

Not in this slice, and not to be started until house tiers merge on `main`:

- **`ValleyTexture.cs` writes the coverage channel.** `Winner`'s loop already holds it. ⚠️ The bake's alpha is a constant
  255 and looks free, but the new-game preview draws the same bake with no shader, so coverage in alpha needs that
  draw to ignore it; a second image needs a second upload on every re-bake. To be settled when it is wired. The splat
  weights ask the same question, and a smooth field can be coarser than the bake.
- **`VillageMap` draws the valley through the shader**, and the year's fraction and the snow amount become per-frame
  uniforms.
- **The grain's overlay** finds its place between the map and the UI (§3.1).
- **`Dressed`'s noise** — the wood's canopy, the stone's grain, the bloom — stays in the bake or moves into the shader.
  The lab does not draw it, so the lab's woods and stone are flatter than the game's.

## 6. Verification, and what the lab cannot say

- **The approved look holds:** with every switch off the lab's renders are **byte-identical** to the same cases rendered
  before this slice (`lab_render.gd`, a cases file; `cmp`, not a tolerance). `lab_check.py parity` still applies.
- **The probe** (`BCLONE_PROBE_WIDTHS=1`, headless): every new control reaches its uniform, opens off, and comes back on
  *Reset*; the fake map is what §4 says. ⚠️ **A shader that does not compile fails quietly:** a headless Godot does
  compile it and logs `SHADER ERROR`, but every line above stays ✅, because a material holds whatever it is told. So
  `terrain lab, shader:` asks the compiled shader for each uniform the panel sets (a failed one declares none). It
  proves the shader compiles, not that it draws the right thing: each piece is also rendered windowed and looked at.
- **Red-checked, piece 1 (D326):** 20 probe mutants, **24 reds, no zeros** (the file, the switch, the slider, *Reset*, the
  palette, the panels, *Copy*, the map's sums, determinism, kinds, the river's width and place, the wood, the broken
  and the renamed shader). The byte comparison too: a 1 % change on the off path differs in 5 of 5 cases, restored 0 of
  5. ⚠️ **The on path's maths has no guard but eyes:** nothing mirrors `terrain_coverage` or `terrain_ground`.
- **Parity does not cover the new layers** (`tools/grass/grass_antitile_ref.py` mirrors the approved path only). Their
  look is Joe's eyes.
- **Rendering only.** `git diff main -- src/Bclone.Sim data tests` stays empty; no golden can move.
- **Not known until the valley is wired:** how any of this reads under the real bake and its dressing, or beside fences,
  fields and paths, which the lab does not draw.
