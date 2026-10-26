# Spec: Seasonal grass — the meadow changes colour with the year

**Decision:** D627 (this document; Joe's grass pack, `D:\Projects\brushes\seasons`). Neighbours: D342 (`ValleyTexture`,
the valley baked once on the CPU), D338/D366 (nothing per frame that a counter can answer), `environment-and-seasons.md`
(the calendar). **Status:** 📦 **ASSETS IMPORTED (D627), NOTHING DRAWN.** The masks, the atlas and the recolour snippet
are in the project with the import settings below, proved lossless (§3). No shader uses them, no scene exists, and
nothing in `src/*.cs` or `Bclone.Sim` changed. Owner: Joe + Claude Code.

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

**Debug UI:**
- `grass_jitter` slider, 0–0.25, defaulting to 0.15
- default version: v06, with v04 as the first alternative

⚠️ The snippet's uniform is `hint_range(0.0, 0.3) = 0.0`; the slider sets the value from code, and the snippet is not
edited. The pack disagrees with itself on the value: the snippet's comment says *~0.08–0.12*, `README.md` and
`REPORT.md` say 0.15. The slider follows the README.

## 6. Open — to settle before anything is drawn

1. **`canvas_item`, not spatial.** The snippet's usage comment writes `ALBEDO`; ours would write `COLOR`. It compiles in a
   `canvas_item` shader unchanged (the pack's own `godot_test` does exactly that).
2. **No linear conversion.** With `hdr_2d` off, `COLOR` is sRGB, and Multiply in sRGB *is* Photoshop's Multiply — so the
   shader would not call `grass_srgb_to_linear`. A call-site choice, not a snippet edit.
3. **How many tiles one mask repeat covers.** At `ValleyTexture`'s 16 px a tile, one 2048² repeat at 1:1 is 128 tiles —
   more than the valley is wide.
4. **Where the season comes from.** `world.Clock.Season` is whole seasons; the blend wants a fraction.
5. **Load only the version shown.** A lossless 2048² mask with mipmaps is ~21 MB of VRAM; five is ~105 MB.
6. **The bake and the shader.** `ValleyTexture` decides where grass is; how a shader layer meets it is the design question,
   and changing the bake is out of scope until it is answered.
