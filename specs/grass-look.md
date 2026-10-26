# Spec: The grass look — what Joe approved in the lab, and where it lives

**Decision:** D632 (Joe, 2026-10-26: *"Lock in the grass look from the lab. The current settings are approved."*).
Built on `seasonal-grass.md` (the pack, its import, THE SCENE, the anti-tiling options of §5.3). **Status:** ✅ **APPROVED
AND RECORDED (D632); ITS OPEN ITEMS 1–3 CHECKED IN THE LAB AND KEPT AS THEY ARE (D633) — the lab opens at it; the
game's valley does not draw it yet.** One file holds every value
(`src/Bclone.Game/looks/grass_approved.tres`); nothing in `Bclone.Sim` changed. Owner: Joe + Claude Code.

---

## 1. Where the look lives — one file

**`res://looks/grass_approved.tres`**, a Godot text resource backed by **`GrassLook.cs`**. Chosen because:

- `data/` is the sim's — `sim.config.json`, read by the Godot-free `Bclone.Sim` and hashed with the village. A render
  look is not sim content and must not move a golden.
- A `.tres` is text: diffable, and a modder can open it in any editor or in Godot's inspector.
- **`GrassLook` gives its properties no defaults.** The file is the only place a number lives; the probe asks that it
  sets every one (`grass lab:`), so a value that falls out of it is a red line, not a silent zero.

Who reads it: **the lab** opens at it and *Reset to approved* reloads it from disk (so an edit shows without a restart);
**the terrain shader** will read the same file when the grass reaches the valley. The shader's own uniform defaults are
*the pack as painted, every option off* — not the look, and commented as such.

**In the lab:** *Reset to approved* puts every look control back (the season, the zoom, the placeholders and the marks
are the lab's and stay); *Copy current values* puts the panel's settings on the clipboard and in the log, in the list
below, so a new set can be pasted back.

## 2. The approved values — and what each does

```
APPROVED SETTINGS
- Masks: v06
- Strength: 0.80
- Jitter: 0.15
- Paper grain: 0.15
- Tiles per repeat: 63
- Macro variation: ON. Tiles per cycle 85, strength amount 0.37, season amount 0.10
- Hex tiling: ON. Blend sharpness 8.5, blend mode "Height — each stamp whole"
- Version mixing: OFF (rejected). Region 48 tiles, transition 1.5 tiles
- Linear blend: OFF (rejected)
```

| value | what it does |
|---|---|
| **Masks v06** | the layout — REPORT §5's strongest: closest to the reference, clean in every season |
| **Strength 0.80** | the grass multiply blended 80 % of the way from bare parchment to the pack as painted |
| **Jitter 0.15** | the snippet's stand-in for the per-stamp colour jitter a density mask cannot hold (README's value) |
| **Paper grain 0.15** | ⚠️ **a starting point only.** The lab's grain is a placeholder (screen-anchored noise, `paper_grain.gdshader`); the real grain belongs to the full terrain shader |
| **Tiles per repeat 63** | one 2048² mask covers 63 × 63 tiles — 32.5 mask texels a tile. ✅ **Kept after the closer-zoom check** (D633; §7) |
| **Macro variation 85 / 0.37 / 0.10** | two octaves of value noise, one cycle per 85 tiles: a region's strength × (1 − 0.37·n₁) — down to 63 % of 0.80 — and its season ± 0.10 (a tenth of a season early or late) |
| **Hex tiling 8.5, Height** | each half-repeat hexagon of the mask turned and shifted by a hash of its lattice cell; weights raised to 8.5 (a narrow blend); Height keeps each stamp whole or drops it |

## 3. Rejected — kept switchable, off

- **Version mixing** (v02–v06 by region) — *"blending layouts caused ghosting and camo-level coverage"*. Measured (§5.3
  of `seasonal-grass.md`): ~70 % of the stamps in a linear mixing band are drawn at part strength.
- **Linear blend** — the same ghosting: in hex tiling at sharpness 4 it put a third of all ground at part strength,
  against 6 % with Height.

Both code paths stay (`grass_lab.gdshader`, `GrassLab.cs`, `GrassLook.MixingOn` / `HeightBlend`), labelled *rejected*
with the reason in a comment. The approved file has them off and the probe asks that it keeps them off.

## 4. Import — and the hazard

| setting | masks `grass_v0N_masks.png` | atlas `grass_seasons.png` |
|---|---|---|
| `compress/mode` | `0` Lossless | `0` Lossless |
| `mipmaps/generate` | `true` | **`false`** (rows would bleed) |
| `process/fix_alpha_border` | **`false`** | `false` |
| `process/premult_alpha` | **`false`** | `false` |
| `compress/normal_map` | `2` Disabled | `2` Disabled |
| `detect_3d/compress_to` | `0` Disabled | `0` Disabled |
| sampled | `texture`/`textureGrad`, repeat, linear + mipmaps | `texelFetch`, never filtered |

⛔⛔ **Godot's default import wipes the masks.** A is the *details* mask (zero over ≥ 73 % of v06). Fix Alpha Border —
on by default — rewrites RGB under low alpha (30,868–45,251 pixels a mask); Premult Alpha zeroes RGB wherever A = 0
(3,248,610 of v06's 4,194,304). Either destroys the olive, green and ochre masks there. Repeat and sRGB are *shader
hints* in Godot 4, not import settings; the snippet declares them. **The probe compares every imported level 0 with
its PNG byte for byte** — a spot-check of sampled pixels scored zero against Fix Alpha Border (D627).

## 5. GPU cost, and how to measure it again

**Method** (`tools/grass/lab_cost.gd`; re-run it every time a terrain layer is stacked):

```bash
"$GODOT" --path src/Bclone.Game -s "$(pwd -W)/tools/grass/lab_cost.gd"
```

The lab renders into a **3840 × 2160** viewport so the shader dwarfs the timer's noise; cases are **interleaved over
six rounds** (20 frames to settle, 60 timed by `viewport_get_measured_render_time_gpu`), the round's median and then the
median of rounds; zoom is set in px a tile, which fixes each pixel's work, and a full-screen fragment shader scales with
pixels — **1280 × 800 is 0.1235 of 4K, 2560 × 1440 is 0.444**. Placeholders off, panels hidden: the ground only.
⚠️ A first measurement at 1280 × 800 (D631) was all noise — *"all three"* came out cheaper than one option alone.

**D632, RTX 3070**, widest zoom = **13 px a tile** (the game's fit-to-width at 1280); gameplay zoom = **48 px a tile**
(the founding view, where the game opens):

| | widest, 1280 × 800 | widest, 2560 × 1440 | gameplay, 1280 × 800 | gameplay, 2560 × 1440 |
|---|---|---|---|---|
| the pack alone (no anti-tiling, no grain) | 0.10 ms | 0.36 ms | 0.10 ms | 0.34 ms |
| **approved, no grain** | **0.21 ms** | **0.77 ms** | **0.22 ms** | **0.79 ms** |
| approved, with the lab's grain | 0.25 ms | 0.91 ms | 0.23 ms | 0.83 ms |

So the look costs about **+0.12 ms a frame at 1280 × 800** over the pack alone — under 1 % of a 60 fps frame.
⚠️ Rounds spread ±30 % on this machine (the 4K pack-alone rounds ran 0.51–0.87 ms); read the medians, on a quiet
machine, and compare a new run with this table rather than with a single number.

## 6. Verification

- **Parity** (`tools/grass/lab_check.py parity`, after `lab_render.gd`): the lab at the approved look, about one mask
  texel a pixel (33 px a tile × 63), grain 0, against `tools/grass/grass_antitile_ref.py` — **≤ 2/255 in every season,
  ≤ 3 px of 1,024,000 off by more than 1**. The approved values are read from the `.tres` itself.
- ⭐ **Recommendation on parity — keep the mirror extended; it already is.** Since D631 the Python mirror covers macro
  variation, hex tiling, the Height blend and (rejected) mixing, not only the recolour. A recolour-only check would pass
  a broken hex lattice or a wrong hash: those are exactly where D631's two shader mutants landed (52–151/255 off), and
  only a mirror of the sampling sees them. The recolour itself stays Joe's `grass_ref.py`, which the mirror calls and
  holds itself to (`--selfcheck`). **What parity does not cover:** the paper grain (a placeholder, §2) and the
  placeholders. It is a manual check — a windowed Godot plus Python — not part of the suite.
- **Deterministic from world position.** No `TIME`, no frame counter, no clock, no unseeded random anywhere in the grass
  shaders or lab code (searched, D632). Every noise is an integer hash (PCG2D) of a world lattice cell with a fixed salt;
  the snippet's jitter is sines of world UV. ⚠️ **One exception, by design:** the lab's paper grain hashes *screen*
  pixels — deterministic, but tied to the window, not the land; the terrain shader's grain will decide that.
- **Probe `grass lab:`** — the `.tres` sets every `GrassLook` property (read by reflection) and keeps the rejected options
  off; the lab opens at it; every control reaches what it moves; *Reset* brings every look value back and leaves the
  season, the zoom and the placeholders; *Copy* says it in the list of §2. Red-checked: **6 mutants, 6 reds, no zeros**
  — a value missing from the file, mixing switched on in it, the lab opening off it, a value *Reset* skips, *Reset*
  moving the season, *Copy* misnaming a line.
- **Rendering only.** Nothing on this branch touches `src/Bclone.Sim`, `tests/` or `data/`; no golden moved.
- **Reference images** (`art/grass/`, from `lab_render.gd` + `lab_check.py shots`): the four seasons at the widest and
  the gameplay zoom, and the seasons strip — each season into the next at 0/25/50/75/100 %, at 24 px a tile.

## 7. Open

⭐ **Items 1–3 closed by Joe (D633, 2026-10-26), after checking each in the lab: *"no changes"*.** The values of §2
stand. The items stay here so the reasoning is not lost:

1. ✅ **Tiles per repeat: 63, or ~48? — 63 kept.** Checked at the closer zoom (at 48 px a tile 63 magnifies the mask
   1.5×; 48 would be about 1.1× and repeat about 2.5 times across the valley's width instead of 1.9).
2. ✅ **Does macro strength make summer read too dry? — 0.37 kept.** It takes a region down to 63 % of the look's
   strength; ~0.25 was the alternative.
3. ✅ **Clipped stamps at hex borders — seen, no change.** ⚠️ **Seen in the reference images:** the summer view at
   the widest zoom has a few straight cut edges where a hexagon's border clips a stamp. Verify with *Mark transitions*; sharpness 8.5 is narrow
   enough that a stamp can end on the border rather than fade.

Still open:

4. **Re-measure the cost once the full terrain shader exists** (§5's tool, the same table).
5. **Later: drive macro variation from sim data** — soil fertility or moisture — instead of noise. It would read sim
   state, never write it (DESIGN §3), and it would have to stay a pure function of a place, as the noise is.
