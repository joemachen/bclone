# Spec: Seasonal grass — the meadow changes colour with the year

**Decision:** D627 (this document; Joe's grass pack, `D:\Projects\brushes\seasons`). Neighbours: D342 (`ValleyTexture`,
the valley baked once on the CPU), D338/D366 (nothing per frame that a counter can answer), `environment-and-seasons.md`
(the calendar). **Status:** 📦 **ASSETS IMPORTED (D627); ✅ §6 SETTLED (D628); 🔨 THE SCENE BUILT (D629–D631); ✅ THE LOOK APPROVED (D632) — its values and
reasons are in `specs/grass-look.md`.** `.\run.bat grass`. The masks, the atlas and the recolour snippet are in the project with the import settings below, proved
lossless (§3); THE SCENE draws them over parchment (§5). **The game's valley does not use them yet**, and
`Bclone.Sim` is untouched. ✅ Joe looked and approved the look (D632–D633); merged to `main` (D634); §6.0's ground
question answered — parchment (D635, built D637). Drawing the grass in the valley is the terrain shader, 🅿️ parked
with the art (D639). *This said "Next: Joe looks at the scene; then §6.0" until D640.* Owner: Joe + Claude Code.

---

## 1. What this is

Joe painted six grass layouts by hand in Photoshop (`in-game_auto.psd`, `auto_pass_01..06`). His pack turns each into a
**density mask** and pairs them with a **season gradient atlas**, so a shader can tint the grass for spring, summer, fall
and winter and blend between them. Summer matches his Photoshop flatten to mean ΔE 1.5–1.9 (v02–v06); the pack's
`REPORT.md` has the numbers and its `README.md` the maths.

**The source of truth stays in the pack.** To retune a colour, edit `palettes.json` there and run
`python seasons/build.py`, then copy the outputs here again and re-run §3's check. Nothing in this repo regenerates them.

## 2. The data

| file | what | read as |
|---|---|---|
| `textures/grass/grass_v02_masks.png` … `v06` | 2048² RGBA8; **R olive, G green, B ochre, A details**; value / 255 = density | `texture()`, repeating, mipmapped |
| `textures/grass/grass_seasons.png` | 256 × 16 RGB8; row = `season * 4 + group`, column u = the group's Multiply colour at density u / 255 | `texelFetch`, never filtered |
| `shaders/grass_seasons.gdshaderinc` | the recolour maths, byte-identical to the pack's, verified there against `grass_ref.py` to ≤ 1/255 | `#include "res://shaders/grass_seasons.gdshaderinc"` |

**v01 is retired** (older palette, worst summer accuracy, most fragmented — `REPORT.md §5`) and is not in the project.
The pack's `work/`, `grass_ref.py`, `README.md` and `REPORT.md` stay in the pack.

## 3. Import — and the one setting that destroys the data

| setting | masks | atlas |
|---|---|---|
| `compress/mode` | `0` Lossless | `0` Lossless |
| `mipmaps/generate` | `true` | **`false`** (rows would bleed) |
| `process/fix_alpha_border` | **`false`** | `false` |
| `process/premult_alpha` | **`false`** | `false` |
| `compress/normal_map` | `2` Disabled | `2` Disabled |
| `detect_3d/compress_to` | `0` Disabled | `0` Disabled |

⛔⛔ **Godot's defaults are the broken import.** A fresh import writes `fix_alpha_border=true` and
`detect_3d/compress_to=1`. **A is the details mask, and it is zero over at least 73 % of v06**, so any setting that "cleans"
RGB under transparent alpha rewrites the other three masks there. Measured (D627) by importing at the defaults and
comparing to the source: Fix Alpha Border alone changed **30,868–45,251 pixels per mask**, up to 23,032 of them where A = 0.
Premult Alpha alone, on v06, changed **3,248,610 of 4,194,304**.

⚠️ **Repeat and sRGB are not import settings in Godot 4.** They are the shader's sampler hints, and the snippet already
declares them: `repeat_enable, filter_linear_mipmap` on `grass_masks`; `repeat_disable, filter_nearest` on the atlas; no
`source_color` on either, because both are data.

**The check** (D627, a throwaway GDScript run with `--headless --script`): each imported level 0 converted to RGBA8 equals
its source PNG **byte for byte**, the masks have mipmaps, the atlas has none; five A = 0 pixels per mask printed with their
RGB beside the source's. Red-checked twice: the defaults scored **10 reds** (each mask: no mipmaps, bytes differ), Premult
Alpha on v06 **6**. ⚠️ **The five sampled pixels survived Fix Alpha Border on every mask** — a spot-check alone scores
zero against it. Only the full byte comparison catches it.

## 4. How the ground is drawn today (D627, unchanged)

All 2D: `Main.tscn` is a `Control`; `VillageMap` draws in `_Draw()`; no camera node (zoom is `_pixelsPerTile`, fit-to-width
to 48). The ground is `ValleyTexture`, baked on the CPU at **16 px a tile** for a 120 × 80 valley and drawn with one
`DrawTextureRect`; grass is the flat `Ground` colour with a ±3.5 % bloom, and **the bake has no season**. Fields are a
per-tile pass; scenery and trails are `ArrayMesh` chunks. There are no shaders. Forward+, `hdr_2d` off: **2D blends in
sRGB.**

## 5. THE SCENE

The first use is a scene to look at the grass in, before it goes anywhere near the valley.

**Ground:** parchment `#e6d6b0` under the grass (D628, §6.0) — the pack is judged as it was painted.

**Debug UI:**
- `grass_jitter` slider, 0–0.25, defaulting to 0.15
- default version: v06, with v04 as the first alternative
- tiles per mask repeat: a slider, starting at 32 (D628, §6.3); the number found by eye goes into data
- season: a slider, 0–4, to judge the blends directly (D628, §6.4)
- pixels per tile: a slider, 13–48 (the game's zoom range at 1280 wide), starting at 24 — added in the build (D629),
  because *tiles per repeat* means nothing on screen without a zoom to say how big a tile is
- **strength**: a slider, 0–1, starting at 1 — 0 is bare parchment, 1 the pack as painted; the grass multiply blended
  toward white (Joe, after the first look; D630)
- **paper grain**: a slider, 0–0.4, starting at 0.15 — a rough multiply over everything, grass and placeholders alike,
  because grain knocks the contrast down and changes how loud the grass reads (Joe; D630). At 0 it is not drawn.
- **placeholders**: on by default — six homes, a granary, a wood and eight villagers, drawn as the game draws them
  (Joe: *"the game's own glyphs"*; D630)
- **H** hides the panel

⚠️ The snippet's uniform is `hint_range(0.0, 0.3) = 0.0`; the slider sets the value from code, and the snippet is not
edited. The pack disagrees with itself on the value: the snippet's comment says *~0.08–0.12*, `README.md` and
`REPORT.md` say 0.15. The slider follows the README.

### 5.1 As built (D629)

- **`GrassLab.tscn` / `GrassLab.cs`**: one full-window `ColorRect` with one `ShaderMaterial`, and a panel whose
  sliders only set uniforms. **`shaders/grass_lab.gdshader`** includes the snippet unchanged, turns the rect's UV back
  into tiles (`(UV − 0.5) × size_px / px_per_tile / tiles_per_repeat` — from the centre since D630), and writes `parchment × grass` — no linear step
  (§6.2). Only the version on show is loaded. Launched by `.\run.bat grass` (Godot's scene argument,
  `res://GrassLab.tscn`); the game itself is untouched.
- **Verified against Joe's reference, not by eye alone.** Rendered windowed (a throwaway script, not in the tree —
  D160) at 32 px a tile and 64 tiles a repeat, so one mask texel lands on one pixel, and compared with
  `grass_ref.over_ground(grass_ref.recolor(…))`: **≤ 1/255 on every pixel** of 1280 × 800 for v06 summer, v06 2.6
  with jitter 0.15, v04 3.5, and v06 spring with jitter 0.15. Red-checked: with the linear step put back, up to
  **67/255** off on 78 % of the pixels, all four cases red.
- **Probe line `grass lab:`** — the lab opens as this section says (v06 then v04 of five; jitter 0.15 on a 0–0.25
  slider; season 1; 32 tiles; the pack's parchment `#e6d6b0`), its sliders and version list reach the shader, and
  every grass texture's import still holds (§3: masks mipmapped, atlas not, each level 0 byte for byte its PNG).
  Red-checked: **7 mutants, 7 reds, no zeros** — the jitter default, the version order, a slider that no longer
  reaches its uniform, the parchment hex, the masks never handed to the shader, Fix Alpha Border on v03, mipmaps on
  the atlas.
- ⚠️ **Seen in the first renders:** at the game's far zoom (13 px a tile) a 32-tile repeat reads plainly as a
  pattern, about 3 × 2 across the window. That is the question the repeat slider is for (§6.3).

### 5.2 Strength, paper grain and the placeholders (D630)

- **Strength** is `mix(1, grass, strength)` before the multiply over parchment, in `grass_lab.gdshader` — the snippet
  is still untouched.
- **Paper grain** is its own overlay (`shaders/paper_grain.gdshader`, `render_mode blend_mul`) on top of the grass
  and the placeholders: three octaves of value noise in **screen pixels** (fibres at ~1 px, tooth at ~5, a faint
  cloud at ~37), `1 − amount × n`, so 0.15 is about 7 % darker on average. No image asset. ⚠️ **Screen-anchored
  is a lab choice, not a game one:** the lab never pans, so it cannot show whether grain belongs to the window or
  to the land. That is the game's question, when it pans.
- **The placeholders** (`GrassLabVillage.cs`) borrow everything but the layout: `VillageMap.FootprintQuadAt`, the
  home, door, granary, canopy and villager colours, the canopy scatter (`VillageMap.TreesOn` / `CanopyOn`, made
  `internal` for this, with `DoorColour`, `TreeCanopy` and `CanopyRadius`), the villager's radius, and the home and
  granary footprints from the loaded config's rows. ⚠️ The game draws canopies only above 24 px a tile and a baked
  wood below; the lab, with no bake, draws canopies at every zoom.
- **Grass and village now count tiles from the window's centre** (`(UV − 0.5) × size_px / px_per_tile`), so they zoom
  together. Re-verified against `grass_ref.py` with the shifted origin: **≤ 1/255 on every pixel** in the four D629
  cases and a fifth at strength 0.5; with the strength blend removed, that case is up to **76/255** off.
- **Probe:** `grass lab:` now also asks that the lab opens at strength 1, grain 0.15 drawn, placeholders on and
  drawn, and that strength, grain (0 = not drawn), the placeholders' box and the zoom (the village with the grass)
  reach what they move. Red-checked: **6 mutants, 6 reds, no zeros** — the strength and grain defaults, grain drawn
  at 0, a box that no longer hides the village, a village that does not zoom, strength never reaching its uniform.
- ⚠️ **What the placeholders showed at once — for §6.0, not for the lab:** an adult villager's dot (`#e8e2d4`) almost
  vanishes on bare parchment, and the granary's yellow barely separates from ochre grass. Both were chosen against
  slate. Moving the game to parchment re-picks them.

### 5.3 Anti-tiling — three options, each behind its own switch (D631, LAB ONLY)

Joe: *"The repeat is clearly visible at wide zoom. Lab only; keep each option behind a toggle so I can compare."* All
three open **off**, so the lab opens as before. The maths is `shaders/grass_antitile.gdshaderinc` (pure functions of
world position — no uniforms, no `TIME`), used by `grass_lab.gdshader`, and mirrored step for step by
**`tools/grass/grass_antitile_ref.py`**. ⭐ **The pack's recolour is unchanged:** these options decide how the masks are
*sampled* and what season and strength each place gets, never how a density becomes a colour — so Joe's
`grass_seasons.gdshaderinc` and `grass_ref.py` stay as he made them, and the mirror calls `grass_ref` for jitter and
holds its own per-pixel-season recolour to `grass_ref.recolor` (`--selfcheck`: 0/255; with the season blend broken,
44/255 — it can fail).

| option | what it does | controls (start) |
|---|---|---|
| **1. Macro variation** | two octaves of value noise at one cycle per *N* tiles: strength × (1 − amount·n₁), season + amount·(2n₂ − 1) | tiles a cycle 50–200 (100), strength amount 0–1 (0.3), season amount 0–0.5 (0.1) |
| **2. Version mixing** | v02–v06 by region: the cells of a jittered grid one region across, the position warped first at two scales so a border wiggles; within *width* tiles of a border the two layouts blend | region 16–128 tiles (48), transition 0–8 tiles (1.5; 0 = a hard cut) |
| **3. Hex tiling** | the triangle grid of Heitz & Neyret / Mikkelsen: each hexagon (about half a repeat across) turned by any angle and shifted; weights raised to *sharpness* | blend sharpness 1–16 (4) |
| **Blend** (2 and 3) | **Linear** keeps the weights; **Height** lifts each by its sample's densest group and keeps only those within 0.15 of the tallest — a stamp whole or not at all | Linear |
| **Mark transitions** | tints every pixel where samples are being blended | off |

A **GPU readout** under the panel gives the window's GPU time a frame, smoothed. Hashing is integer (PCG2D), so the
GPU and numpy pick the same regions and turns bit for bit. ⚠️ **Mixing loads all five layouts** (~105 MB of VRAM) the
first time it is switched on — §6.5's one-version rule does not hold while it is.

**Parity** — the lab rendered at one mask texel a pixel against the mirror, 1280 × 800:

| case | max | pixels off by > 1 |
|---|---|---|
| macro (cycle 50, strength 0.5, season 0.3) | 1/255 | 0 |
| mixing, linear / height / hard (region 16, band 2; 17 % of the window in the band) | 1/255 | 0 |
| hex, linear, sharpness 4 | 2/255 | 13 |
| hex, height, sharpness 4 | 5/255 | 187 (2 over 4) |
| all three, height | 4/255 | 136 |

The hex residue is the GPU's 8-bit filtering weights at turned sample positions, sharpened where the height blend's
cutoff falls between them. Red-checked: the region cells' salt moved puts every mixing case **117–151/255** off; the
height depth 0.15 → 0.30 puts the height cases **52–80/255** off; macro and linear hex stay green (untouched).

**Ghosting** — Joe: *"I want to know whether blending mask densities creates ghost shapes."* **It does.** Measured on
the mirror over 80 × 80 tiles: a pixel is a ghost when a source's stamp (its densest group > 0.2) is drawn at a
*partial* final weight (0.15–0.85). A stamp the blend cut whole is an edge, not a ghost.

| option | band, share of ground | ghosted, share of the band's stamps | ghosted, share of all ground |
|---|---|---|---|
| mixing, linear, 0.5 / 1.5 / 4-tile band | 1.8 / 5.4 / 13.0 % | 70 / 70 / 74 % | 1.2 / 3.5 / 9.0 % |
| mixing, height, 1.5 / 4-tile band | 5.4 / 13.0 % | 12.5 / 13.4 % | 0.6 / 1.6 % |
| hex, linear, sharpness 1 / 4 / 16 | 100 / 67 / 20 % | 90 / 52 / 43 % | 89 / 34 / 9 % |
| hex, height, sharpness 4 | 67 % | 9 % | 6 % |

**What it looks like** (the sheets in the D631 report): linear mixing fades both layouts' stamps through each other
across the band — the ghost, plainly; height keeps stamps whole but leaves the odd thin sliver where one stamp's edge
cuts another; a hard cut slices stamps along the border. Linear hex turns most of the ground to mush (two thirds of it
is in a band at sharpness 4); height hex is crisper with some torn edges; sharpness 16 is crisp with straight seams.
⚠️ A first ghost measure ("drawn under 70 % of the strongest source") counted cut stamps as ghosts and said height
blending barely helped; it was the measure, not the blend.

**At the widest zoom** (13 px a tile, 32 tiles a repeat): macro alone does not break the repeat on one screen — at one
cycle per 100 tiles it is valley-scale variation; mixing breaks it between regions, but a 48-tile region still repeats
inside itself; hex removes it, at the price of the character (washes look smaller and turned, less bare paper). ⚠️ The
snippet's jitter field is still periodic at the repeat (its `uv` is the world's), so it repeats under every option.

**GPU cost** (RTX 3070; the window at 3840 × 2160 so the shader's work dwarfs the noise, six interleaved rounds, the
median of the round medians — a first pass at 1280 × 800 was all noise, *"all three"* cheaper than mixing alone; a
full-screen fragment shader's cost scales with pixels, so 1280 × 800 is 0.12 of it):

| option | 4K, measured | over plain at 1280 × 800 | over plain at 2560 × 1440 |
|---|---|---|---|
| plain | 0.57 ms | (0.07 ms) | (0.25 ms) |
| macro | 0.79 ms | +0.03 ms | +0.10 ms |
| mixing, linear / height | 1.50 / 1.98 ms | +0.12 / +0.17 ms | +0.41 / +0.63 ms |
| hex, linear / height | 1.22 / 1.34 ms | +0.08 / +0.10 ms | +0.29 / +0.34 ms |
| all three, height | 2.57 ms | +0.25 ms | +0.89 ms |

Mixing, height, was bimodal across rounds (1.5 or 2.0 ms at 4K).

**Probe:** `grass lab:` asks that every switch opens off with Linear blending and nothing extra loaded, and that each
reaches the shader, mixing loading v02–v06 in order. Red-checked: **5 mutants, 5 reds, no zeros** — a switch whose
uniform name misses, switches that open on, the five layouts loaded before mixing is asked for, the Height option
miswired, the layouts out of order.

## 6. Settled — Joe's calls (D628, 2026-10-26, all as recommended)

0. **The ground under the grass.** ⛔ The pack's colours are Multiply over parchment `#e6d6b0`; the game's grass ground
   is slate `#2b3332` (`VillageMap.Ground`), and Multiply only darkens — summer olive's pooled `#7a9254` over slate is
   `#151d10`, near black. **THE SCENE puts parchment under the grass.** Moving the game's ground to parchment (the
   hand-drawn direction in `art/`) is **its own decision, after the scene**, and touches every colour the view picked
   against slate. Nothing in the game's view changes until then.
   ✅ **Decided (D635, 2026-10-26) — Joe: *"Yes, move the game's ground to parchment."*** The world's colours move with
   it (`world-legibility.md`, measured first); the UI stays slate; the terrain shader comes after (`terrain-shader.md`).
1. **`canvas_item`, not spatial.** The game is all 2D, so the shader writes `COLOR`. The snippet compiles there unchanged
   (the pack's own `godot_test` does exactly that).
2. **No linear conversion.** With `hdr_2d` off, Godot blends 2D in sRGB, and Multiply on sRGB values *is* Photoshop's
   Multiply, so the shader does not call `grass_srgb_to_linear`. A call-site choice, not a snippet edit. ⚠️ If `hdr_2d`
   is ever turned on, this flips.
3. **Tiles per mask repeat: a slider in THE SCENE, starting at 32.** 32 tiles is 64 mask px a tile — sharp at
   `MaxPixelsPerTile = 48` — and ~3.75 repeats across the 120-tile valley (`REPORT.md`: v06 tiles 3×3 with no stand-out
   shape). 1:1 with the bake (128) was soft up close; 20 risks a visible repeat. The number found by eye goes into data.
4. **The year: hold, then turn.** A season is 30 days × 4 ticks = 120 ticks. Each season holds its own palette for its
   first **20 days** and blends into the next over its last **10**, winter into spring included. The fraction is
   worked out in the view from the tick within the season; the sim is not touched. Rejected: continuous drift (mid-summer
   is half spring, half fall — never legibly one season) and a snap at the boundary.
5. **Load only the version shown.** A lossless 2048² mask with mipmaps is ~21 MB of VRAM; switching loads the new one and
   drops the old. (THE SCENE may hold v06 and v04 both, 42 MB, for an instant flip.)
6. **The bake meets the shader through a coverage map (option A).** `ValleyTexture` already finds, per pixel, which
   ground wins and how decisively; the same loop bakes a one-channel **grass coverage** image. One `canvas_item` shader on
   the existing valley `DrawTextureRect` multiplies the grass in by that coverage, so grass fades at banks and woods on the
   valley's own smooth edge. Season and jitter are uniforms set per frame — a uniform, never a rebuild (the D338 rule).
   Rejected: re-baking the CPU texture with grass in it (hundreds of ms a bake, and a blend would want one every frame);
   a separate overlay (the same coverage map plus a full-screen pass and a rect to keep aligned).
