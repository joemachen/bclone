# Spec: The terrain shader — the valley drawn over parchment

**Status:** 📝 **NOT STARTED — design notes gathered, the spec is not written.** Nothing renders from this file yet.
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
- **The value band:** the ground and every ground kind at L\* ≥ 50 (`world-legibility.md §5`, pending Joe's call on the
  rule). Today's ground kinds, picked against slate, are mostly below it and get re-picked here.

## 2. ⚠️ Snow cannot be Multiply — it needs its own blend

**Multiply only darkens:** the result is never lighter than the parchment under it (L\* ~86, `#e6d6b0`). Snow is lighter
than parchment, near white at L\* ~95, so a Multiply layer cannot produce it at any strength. The pack's own winter palette
is Multiply too, so it reads as dormant ground, not snow (L\* 55–84, `world-legibility.md §2`).

So snow is **its own layer with its own blend**, on top of the grass result:

- a **lerp toward the snow colour by snow coverage**: `mix(ground, snow, coverage)`. It covers whatever is beneath,
  grass and parchment alike, and it is the simplest one to keep legible;
- or **Screen / Lighten**, which brightens without fully covering. Screen over the darkest grass lifts it only part of the
  way, so pooled grass shows through as grey patches; that is plausible as thin snow, wrong as deep snow.

Things to settle when the spec is written:

- **Where coverage comes from:** a season-and-weather driven value read from sim state, never written. It must stay a
  pure function of place and time, like the macro noise.
- **Whether the paper grain sits over snow.** A Multiply grain over snow stays correct (it only darkens).
- **What snow does to the value band.** Snow raises the ground's light end. Dark and ink-outlined things gain contrast;
  any light fill loses what little it had. The `world-legibility.md` check should be run with a snow ground before snow
  ships.

## 3. To do when this spec is written

- **Re-measure the GPU cost** with `tools/grass/lab_cost.gd`'s method once the shader exists (`grass-look.md §5`, open
  item 4).
- **Paper grain:** world-anchored or screen-anchored. The lab's grain is screen-anchored, a placeholder (`grass-look.md
  §2`).
- **Re-pick the ground kinds into the band** (`world-legibility.md §4`, 13 of 17 below it): woods, seams, fields, the
  river and its shallows, paths, the quarry and mine floors, the valley's edge and outside it.
