# Spec: The terrain shader — the valley drawn over parchment

**Status:** ⭐ **§5'S WIRING (A22–A25) AND §7'S INK AND POOLING (A26–A28) MERGED INTO `main` AT JOE'S WORD (A38, 2026-11-10, *"Merge art now"*, `f48cfab`) — behind the *paint the valley in watercolour* tick, ~~off by default; §7's values not yet approved as a look~~. ⭐ **THE DEFAULT LOOK SINCE A58** (Joe: *"yes, make watercolour the default look"*): the tick starts on, so the valley opens in watercolour, ink, painted trees and river included. Taken as his approval of §7's values as they stand. The tick stays, and off still draws the flat map byte for byte.** 🔨 **ALL FOUR PIECES BUILT IN THE LAB (the fake coverage map, A2, 2026-11-06; world-anchored grain
and drag to pan, A3; snow, A4; splat blending and edge bleed, A5 — all 2026-11-07). ⭐ JOE APPROVED SPLAT AND BLEED AT SHARPNESS 5.5, REACH 0.33,
SCALE 0.30 TILES (A7); ⭐ AND THE GRAIN AND SNOW AT HIS TUNING (A8–A9): GRAIN 0.065
TILES, UNDER THE VILLAGE; SNOW LERP, `#f8f6f1`, PATCHINESS 0.03, PATCH 3 TILES. THE MAP IS THE LAB'S FAKE, NOT JUDGED.
The GPU bar is 1 ms a frame at 4K for the whole stack (A11, §3.3); everything on is +0.64–0.80 ms, inside it. MERGED INTO `main` (D710, 2026-11-07), still in the lab only — not wired into the valley. ✍️ THE VALLEY'S WIRING
SPECCED (A22, 2026-11-09, on `art`, after house tiers merged, D724): §5; ALL SIX CALLS ANSWERED BY JOE, AS RECOMMENDED,
LEAN AS A STANDING ASK, A BEFORE/AFTER SLIDER ADDED TO PIECE 2 (A23). 🔨 PIECE 1 BUILT (A24, 2026-11-09): THE MIX IN
THE BAKE — THE COLOUR BYTE-IDENTICAL. 🔨 PIECE 2 BUILT (A25, 2026-11-09): THE VALLEY THROUGH THE SHADER BEHIND JOE'S
TICK, WITH THE BEFORE/AFTER LINE; TICK OFF BYTE-IDENTICAL TO BEFORE; +1.35–1.73 MS AT 4K. JOE PLAYED IT: *"QUITE A WAYS
AWAY FROM THE CONCEPT ART"* — HIS ART DIRECTION AND FOUR CALLS (A26), §7; 🔨 ITS PIECE 1, INK AND POOLING, BUILT (A27,
2026-11-10), +1.87 MS AT 4K; REVISED AT HIS WORD (A28): THE GROUND NOT OUTLINED, THE BANK A PEN STROKE, THE TREES
OUTLINED. ⏸️ JOE'S EYES.** §2–§4 are the lab's build; §4's table says which piece stands;
§5 is the wiring, its calls and its three pieces; §7 is the way toward the concept.
▶️ **The first slice of the art lane (D699): built and tuned in the Grass Lab, against a fake coverage map; not wired into
the valley (`VillageMap`'s `DrawTextureRect`, `ValleyTexture`'s bake) until house tiers merged (D724) — the wiring is §5.** Joe's eyes are the
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

**The snow colour** is `#f8f6f1`, L\* 96.9, since A8 (Joe: A4's `#f4f1ea`, L\* 95.2, was "a bit too grey";
"a touch whiter" raised every channel by about four levels and kept the warmth): the near white §2 asks for, warmed to sit on the paper rather than read as
screen white. It lives in `looks/terrain_lab.tres`, not in the shader.

**As built (A4):** `terrain_lab.gdshaderinc`'s `terrain_snow_*`, called from `grass_lab.gdshader` after the grass and
before the transition marks, so the grain overlay lies over it. *Snow*, *Snow blend* (Lerp, Screen, Lighten),
*Snow amount* (lab start 0.56), *Snow patchiness* (0–0.3, start 0.03) and **a fifth control the four above did not name,
*Snow patch size*** (2–48 tiles a cycle, start 3; the starts are Joe's since A8, A4 opened at 0.6, 0.08, 12): the place needs a size, and a number that only lives in a file
cannot be judged by eye. The threshold is two octaves of `at_value_noise` at salts 71–72, the macro variation's
weights. The amount is stretched by the patchiness before the smoothstep, `smoothstep(t − s, t + s, mix(−s, 1 + s,
amount))`, so 0 is bare everywhere and 1 white everywhere whatever a place's threshold (measured: amount 0 renders
byte-identical to snow off; amount 1, Lerp and Lighten, is `#f4f1ea` in 100 % of pixels, grain off; at A8 Lerp is `#f8f6f1` in 100 %). ⚠️ The noise is
not uniform, two octaves pile up round 0.5, so most of the change comes in the middle of the amount's range.

- ⚠️ **Lighten draws a fringe.** It is a per-channel max, so where the cover is partial the grey `snow × cover` wins
  the blue channel over the yellow ground and loses the other two: a purplish ring round every bare patch, widest at
  high patchiness. Expected from the maths, visible in the renders.
- ⛔ **Water: the snow follows the water as drawn, not its weight** (found by measurement before commit). The first
  build faded snow out on the water's raw weight between 0.3 and 0.5, as above. Where three kinds meet, water wins
  below 0.5, so half-strength snow lay on visible river there: up to 74 levels off, within 6 px of the bank. The snow
  now uses the water's **drawn share**, `terrain_splat`'s `.a`, so it stops at the bank's own contour and piece 4's
  splat blending carries it along. Measured after: no deep-water pixel changes; the bank's anti-aliased edge, 2 px at
  most, moves a median 2 levels.

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
  snow ground, and what it says goes in §6 before snow ships. ⚠️ **(A4: the dot is stale.** The adult's dot is
  `#454135` in today's `VillageMap.cs`, dark. The light fills now are the cattle `#e6dcc4`, the sail `#efe6d2` and the
  flour heap `#ebe0c7`, and each is outlined, so on snow they read by the outline alone. §6 has the check's result.)

## 3. The rest of the shader

### 3.1 Paper grain — anchored to the land

⭐ **Anchored to the world, with the strength the lab's existing *Paper grain* slider.** The placeholder hashes screen
pixels, so in a game that pans the paper would stay still while the valley slid under it: a dirty window, not a drawing.
Built as a switch on the same overlay (`paper_grain.gdshader`): tile space in place of `FRAGCOORD`, the size a *Grain
scale* slider in tiles, integer hashes (the sine hash is not the same on every GPU, and §6 asks that a place always looks
the same).

**As built (A3):** the three octaves keep the placeholder's ratios (1.3 : 5 : 37), *Grain scale* is the finest one's
cycle in tiles (0.02–0.5), and each is `grass_antitile.gdshaderinc`'s `at_value_noise` at its own salt (61–63). The
lab started at 0.08 tiles: two pixels a cycle at the lab's opening zoom, so the fibres are whole there and nowhere
wider. **Since A8 it starts at Joe's 0.065:** 1.6 px a cycle at 24 px a tile, so there the finest octave is about
half faded to its mean, whole only from 31 px a tile up, 3.1 px at 48. He also had *Grain under the village* on: that is his pick
for the wiring, but the box opens off, because it moves the screen grain under the village too, which is the
approved look. Off is the placeholder exactly.

- **The trade, to be looked at:** world-anchored grain magnifies with the zoom, 3.7× between the widest view (13 px a
  tile) and the closest (48). Fibres that read at one end are mush or sand at the other. Each octave therefore fades out
  as it nears a pixel (below two pixels a cycle) rather than aliasing, and the *Grain scale* slider is where Joe finds
  the size that holds up across the range. **As built:** an octave is whole from two pixels a cycle and gone at one,
  and it fades **to its own mean**, not to nothing, so zooming out loses fibres without lightening the page. At 0.08 the
  widest view keeps only the tooth and the cloud; at 48 px a tile the fibres are about four pixels, a visible mottle.
  ⚠️ From about 0.2 up the value noise's square lattice starts to show; a rotated or gradient noise is the fix if Joe
  wants grain that coarse.
- ⚠️ **The lab must pan to show any of this.** It never has (`seasonal-grass.md §5.2`). Drag to pan arrives with this
  piece: a view control like the zoom, moving grass, village and map together, left where it is by *Reset*. **As
  built:** a left or middle drag on the land; `view_tile`, the tile at the window's centre, on the grass's and the
  grain's shaders and the placeholders; stopped at the fake map's edge (±60 × ±40 tiles); **c** centres it.
- **Over the village or under it.** The lab's grain lies over the placeholders too (D630). In the game that is an overlay
  above the map and below the UI, not a line in this shader. ⚠️ **Built as *Grain under the village*, off** (A3; the
  spec said *over*, on): a switch that opened on would break §4's rule that every switch opens off and all off is the
  approved look, which the probe checks. Off is D630's order; on moves the grain between the ground and the
  placeholders, so only the land has tooth.

### 3.2 The ground kinds — flat fills, and how they meet

The ground kinds stay **flat fills in the band, in `VillageMap`'s palette**, and only the river's depth comes across from
`ValleyTexture.Dressed` (shallows at the bank, the same contour as the fill). Washes and textures per kind are Joe's
paint, not this slice's (`art-inventory.md §2`; the format he paints to is `ground-textures.md`, A12); the shader takes them as a later input without changing shape.

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

**As built (A5):** *Splat blending* and *Splat sharpness* (1–48, lab start 5.5), *Edge bleed*, *Bleed reach* (0–0.5
tiles, start 0.33) and *Bleed scale* (0.25–4 tiles a cycle, start 0.30). The starts are Joe's first tuning (A6;
A5 opened at 8, 0.25 and 1), ⭐ **approved by him at those values (A7)**; the switches still open off, and the
valley's wiring (§5) takes the three values from `terrain_lab.tres`. ⚠️ The approval was of the lab's fake map: at
scale 0.30 the warp's cycle is shorter than its reach (a fray, not a wander), so look again on the real valley for a
land feature one tile thin (D342's rule). Both read the coverage map and show nothing
without it. The bleed's warp is one octave of `at_value_noise` at salts 81–82, cheap on purpose (§3.3). Value noise
seldom reaches its ends, so the edge seldom wanders the whole reach.

- ⛔ **The water is separated from the land, by construction.** `terrain_splat_keeping_water` takes the water's drawn
  share from the unwarped weights, and the land kinds split what is left by their own, perhaps bled, weights. Both
  switches off keeps the old line exactly, so the coverage map's renders are byte-identical to piece 2's.
- ⚠️ **Spec changed: the splat does not soften the bank either.** §3.2 above has *Splat sharpness* run every kind, water
  included. Built so, sharpness 1 washed the river a tile into the land, and the river's drawn pixels fell 22 % (88,657
  to 69,401). The bank is where a villager cannot walk (D342), so the water's share keeps the hard power whatever the
  slider says, and the splat runs between land kinds only. A soft bank is a one-word change if Joe wants to see one.
- ⚠️ **Found by measurement: deep river drew as NaN.** Where there is no land at all, every land share is
  `(1e-3)^sharpness`, which underflows to 0, and 0 / 0 painted about 19,000 river pixels wrong, even at sharpness 48
  and reach 0. That case now returns early (its share is multiplied by a land total of 0 anyway).

### 3.3 GPU cost

Re-measured with `tools/grass/lab_cost.gd`'s method as each layer lands (`grass-look.md §5`, open item 4). The tool's
cases are fixed and it is not this lane's file, so the new cases run from a scratch copy; the numbers go in §6 beside
`grass-look.md`'s table. Bar: the D632 budget's size, about +0.1 ms a frame at 1280 × 800 for the whole stack (until A11). ⭐ **Since A11,
at Joe's word: 1 ms a frame at 4K on the RTX 3070 for the whole terrain stack**, about 6 % of a 60 fps frame. The old
bar was the grass look's own cost reused as a size, not a share of a frame, and the runs' ±0.02 ms made it no test.
It is measured again in the wired valley, and the bake (§5) is built only if that goes over.

⚠️ **Compare inside one session only.** On 2026-11-06 the same RTX 3070 read *approved, no grain* at **9.1 ms at 4K**
against D632's ~1.7 (0.77 at 2560 × 1440, scaled), about five times higher, and **the shader as it stood before this
slice read the same** (A/B/A: 9.14 / 9.34 / 9.14 widest, 9.06 / 9.25 / 9.07 gameplay), so it is not this work. **On
2026-11-07 the same case read 1.51 ms**, D632's scale again, with no change to the approved path between (its renders
byte-identical): the 9.1 was the machine that session, cause still unknown. So §3.3's numbers stay differences, each
row from one run.

| layer, against *approved, no grain* in the same run | widest, 4K | gameplay, 4K | as a share |
|---|---|---|---|
| **coverage map** (A2; 2026-11-06, the slow session) | +0.50 ms | +0.49 ms | **+5 %** |
| the placeholder's screen grain at the approved strength (D630; 2026-11-07) | +0.34 ms | +0.31 ms | +22 % / +20 % |
| **world-anchored grain** in its place (A3; same run) | **+0.13 ms over it** | **+0.12 ms over it** | **+7 %** over the screen grain; +0.016 ms at 1280 × 800 |
| world grain and the coverage map together (same run) | +0.53 ms | +0.53 ms | +35 % / +34 %; **+0.19 / +0.22 ms at 4K over the approved look with its grain, +0.027 ms at 1280 × 800: inside the +0.1 bar** |
| **snow**, Lerp, over the approved look with its grain (A4; 2026-11-07, a second run: *approved, no grain* read **2.51 ms** at 4K, against 1.51 that morning — trap 8 again) | **+0.43 ms** | **+0.43 ms** | **+14 %** over approved with grain; +0.053 ms at 1280 × 800. Lighten the same within 0.02 ms |
| world grain, coverage map and snow together (same run) | +0.78 ms over approved with grain | +0.78 ms | +26 %; **+0.096 ms at 1280 × 800: AT the +0.1 bar, with piece 4 still to come** |
| *(A5; 2026-11-07, a third run: *approved, no grain* 2.48 ms)* **coverage map** alone, over approved with grain | +0.16 ms | +0.16 ms | +5 %, as A2 found in the slow session |
| **splat blending**, over the map | **+0.08 ms** | **+0.09 ms** | +3 % |
| **edge bleed**, over the map | **+0.34 ms** | **+0.33 ms** | +11 %: a second read of the map and two noise lookups |
| **everything** — world grain, map, snow, splat, bleed (same run) | **+0.64 ms over approved with grain** | **+0.80 ms** | +21 % / +27 %; **+0.078 / +0.098 ms at 1280 × 800: AT the +0.1 bar.** A4's run read the stack without splat and bleed at +0.096; this run read it at +0.074, so the runs differ by ±0.02 ms, about the size of piece 4 |

⚠️ **The bar is spent before piece 4** (A4; that was the 0.1 ms bar — A11 made it 1 ms at 4K, which the stack fits). **(A5: and the whole stack lands on it, inside the runs' own noise.
The bleed is the dear one; with splat alone the stack stays under.)** Snow's threshold is two octaves of value noise, eight integer hashes a
pixel, about what the world grain's three octaves cost. Piece 4's warp adds more noise. So either the bar moves, which
is Joe's call, or the noise is shared: snow's threshold and the bleed's warp could read one octave set, and in the game
a place's threshold is a constant that the bake could store in a channel, costing nothing per frame (§5).

## 4. The build — four pieces in the lab, one commit each

Each piece is droppable: the branch is whole after any of them, and Joe can look at one and leave for a week.

| # | Piece | Controls (the *Terrain* panel) | State |
|---|---|---|---|
| 1 | **The fake coverage map** (`GrassLabGround.cs`) | *Coverage map* switch · *Coverage fade* | 🔨 built (A2), Joe's eyes pending |
| 2 | **World-anchored paper grain**, and drag to pan | *World-anchored grain* switch · *Grain scale* · *Grain under the village* (§3.1: built *under*, off) · drag, **c** centres | ⭐ approved (A9): 0.065 tiles, under the village |
| 3 | **Snow** | *Snow* switch · *Snow blend* (Lerp, Screen, Lighten) · *Snow amount* · *Snow patchiness* · *Snow patch size* (§2: a fifth, built A4) | ⭐ approved (A9): Lerp, `#f8f6f1`, patchiness 0.03, patch 3 tiles |
| 4 | **Splat blending and edge bleed** | *Splat blending* switch · *Splat sharpness* · *Edge bleed* switch · *Bleed reach* · *Bleed scale* | ⭐ approved (A7): 5.5, 0.33, 0.30 tiles; §3.2: the water keeps its hard edge under both |

**The fake map stands in for the bake, and is shaped like it.** One RGBA8 image, 8 px a tile, 120 × 80 tiles centred on
the lab's origin, bilinear, clamped: **R grass, G wood, B seam, A water**, weights summing to one, off the map all grass
(as `ValleyTexture.Blended` reads it). R is the coverage channel the bake will write. **The seam is stone or iron**
(A20, `ground-textures.md §5` piece 3): a second, one-channel image beside it (`ground_map_iron`) is white where an iron
patch is nearer than any stone one and black elsewhere, so splat, bleed and the water's contour stay four-way and the
kind flips only midway between seams, on grass. It is laid out as a test scene
rather than a valley, because the lab's village is fixed: the placeholders stand on open grass, their wood stands on a
wood, the river runs below the granary at the real two tiles wide, and woods and stone meet the river and each other,
so every junction is on screen at the lab's opening zoom. Its edges wander by `ValleyTexture`'s own three warp octaves.
It is a pure function of the tile: integer hashes, no RNG, no clock, no sim. Baked once on first switching it on, 0.44 s
(measured), so the lab opens without paying for it.

**Coverage** is `smoothstep(0.5, 0.5 + fade, R)`: nothing where grass does not win, rising from the contour inward,
as `Winner`'s confidence does. Grass never lands on water or a wood. The grass term becomes `mix(1, grass, strength ×
coverage)`, so at coverage 1 it is the approved sum exactly.

**Rules for every piece:**

- **Behind a switch, off at opening.** All off, the lab draws the approved look byte for byte (§6's comparison). *(The game's tick has started ON since A58. The switch and its byte-for-byte off stand.)*
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

## 5. Wiring it into the valley (A22) — specced; six calls answered by Joe (A23); nothing built

House tiers merged (D724, 2026-11-09), so `VillageMap.cs`, `ValleyTexture.cs` and the glyphs are the art lane's for this
slice (HANDOFF's banner). Until A22 this section was the list of what waited; each of its five items is answered in §5.2
(the coverage channel → call 1, `Dressed`'s noise → call 2, the grain's overlay → call 3, the GPU bar → call 4,
`VillageMap` drawing through the shader → §5.4 piece 2). **Joe answered §5.2 (A23); piece 1 is next.**

### 5.1 What the valley does today — read from the code, 2026-11-09

- **One bake, three readers.** `ValleyTexture` bakes the valley at 16 px a tile (1,920 × 1,280 for the 120 × 80
  valley, RGBA8, 9.8 MB) as **finished colour**: `Winner` picks one kind a pixel and `Dressed` paints it — the palette
  colour, plus the canopy's two noise octaves (lift 0.13, young wood 0.06), the stone's and iron's grain (0.09), the open
  ground's bloom (0.035), and the river's depth from how decisively the water won. Its alpha is a constant 255. Three
  things draw it, all with ordinary alpha blending: `VillageMap.DrawTheBakedValley` (one `DrawTextureRect`), the new-game
  preview (`NewGameScreen`, a `TextureRect`, re-baked on every change at a few px a tile) and the title backdrop
  (`TitleScreen`, a `TextureRect`).
- **The bake keeps only the winner.** `Winner` weighs every kind over a 3 × 3 kernel after the warp and each kind's
  jitter (`HowFreeToWander`), then throws all but the top one away. **The lab's fake map is those thrown-away weights**,
  and they are what splat, bleed, coverage and the ground textures read.
- **Six kinds, not four.** The valley has grass (worked ground is baked as grass, under the per-tile fields), forest,
  sapling, rock, iron and water. The fake map carries four weights and the iron pick (A20); the valley adds young wood,
  which the lab only previews (*Young wood preview*, A14).
- ⛔ **A material shades every draw of its canvas item.** `VillageMap` is one `Control` that draws the whole village in
  one `_Draw` — ground, trails, fields, washes, woods' game, herds, bridges, buildings, heaps, carts, people, the ghost. A
  `ShaderMaterial` on it would run the terrain shader over every building and villager. **The valley needs a canvas item
  of its own.** (`VillageMap` has no children today.)
- **Drawn over the bake, inside `DrawValley`:** the trails (`WornPath`, 60 % alpha), the worked ground (fields, opaque),
  the live canopies (from 24 px a tile), the wear (`WearWash`, 70 %), the residential land (28 %), the valley's edge and
  the grid. Then, in `_Draw`, the woods' game, the herds, the boat, the bridges and everything that stands or walks.
- **The game has no grass and no grain today.** The wiring brings both for the first time, not only the terrain layers.

### 5.2 The calls, Joe's — each with a recommendation

✅ **ANSWERED (A23, 2026-11-09), after a plain-words retelling:** *"1. yes 2. yes 3. ground only 4. OK - but i like that
we're keeping an eye on hardware resource costs. let's try to keep them as lean as possible for optimized performance
5. Yes add a tick box … 6. yes."* So: (1) the second image; (2) the mottle stays in the bake; (3) **(a)**, the grain in the
ground shader; (4) both bars, **and leanness is a standing instruction** — every piece reports its cost against both, the
cheaper of two equal looks is the one built, and a lever (§3.3's bake of the snow threshold and bleed warp) is pulled
when a piece comes near a bar, not only over it; (5) the tick box; (6) the winter blend, mid-winter fully white.
**Two asks rode in, answered in §5.3:** a before/after slider (*"i dont want it if it is insane effort and would only be
used once"*) and falling snow.

1. **Where the ground's mix lives** (the old "coverage channel"). ⭐ **Recommended: a second, smaller image beside the
   bake — never the bake's alpha.** The shader needs more than one coverage number: it needs *how much of each kind is at
   each spot*, because that is what blends the edges and picks the painting, and one spare channel cannot hold six
   kinds. Alpha would also make the new-game preview and the title backdrop see-through, since they draw the bake
   straight. So **the colour bake stays byte for byte what it is**, and the game's bake writes, in the same loop, the
   weights it throws away today: **one RGBA8 image in the fake map's layout** (R grass, G wood, B seam, A water, summing
   to one) **and one RG8 beside it** (R the share of the wood that is young, G the share of the seam that is iron), **at
   8 px a tile**, the fake map's resolution — what the approved values were judged on. 960 × 640: 2.5 + 1.2 MB, against
   the colour's 9.8, uploaded whole whenever a felling re-bakes, as the colour is (an `ImageTexture` has no partial
   update). *Not recommended:* 16 px a tile (four times the upload, for a smooth field the bilinear read smooths
   anyway); alpha (above).
   - ⚠️ **Shares, not the lab's pick.** The young and iron shares are fractions, sharpened at the splat's own power, so
     forest meets young wood and stone meets iron the way every land edge meets. A pick (A20's `≥ 0.5`) meets at a hard
     line (HANDOFF art trap 18), and in the valley young wood touches its forest all the time: a forager's thinning
     turns tiles inside a wood into saplings, and regrowth seeds them at its edge (`SimWorld`, `RegrowthSystem`). Young wood costs no extra read (the woods tile × `mix(1, young ÷ woods, share)`, A14's ratio); stone
     and iron read both tiles only where a seam is part of each.
2. **The bake's dressing — `Dressed`'s noise.** ⭐ **Recommended: it stays in the bake, and the shader does not draw
   it.** With the shader on, the woods, stone, iron and water are Joe's paintings and the meadow is the grass pack; the
   canopy's dapple, the stone's speckle and the meadow's bloom would be a second texture laid over his. The bake still
   draws them wherever the bake is drawn: the switch off, the new-game preview, the title. The river's depth comes
   across, as in the lab (`terrain_ground`'s shallows, the same ramp, D338). **So the wired valley is the lab's look,
   which is the look he approved.** *Not recommended:* porting the noise into the shader (a cost per pixel to paint
   over his paintings), or drawing the baked colour under the paintings (two images read and blended for a mottle
   nobody sees).
3. **Where the grain sits.** He picked *under the village* (A9). In the game that is one of three places:
   - ⭐ **(a) In the ground shader — recommended.** The meadow, the woods, the stone, the water and the snow have tooth;
     the paths, fields, washes, live trees and everything standing are drawn on top without it. Free — no extra draw —
     and it is the lab's order for everything the lab draws. The paths and washes are partly see-through (60 %, 70 %,
     28 %), so the tooth shows faintly through them; the fields are opaque, so they are smooth.
   - (b) **Its own layer over the ground *and* its marks** — paths, fields, washes — under the trees, animals, buildings
     and people. Needs `DrawValley`'s half of `VillageMap` moved onto a canvas item of its own: a large diff in the
     view's busiest file. The thing to build if (a) shows smooth fields in a grainy valley and they look wrong.
   - (c) **Over everything, under the UI** — D630's first order, the lab with the box off. One overlay node, cheap; but
     buildings and people get grain, which he turned down at A9.
4. **The GPU bar.** A11's 1 ms a frame at 4K is for the terrain layers, measured **over the approved grass**. ⚠️
   **Today's valley pays for neither**: it draws one plain texture. So the switch also brings the grass look itself,
   which D632 approved but never put against a frame: the approved grass with its grain reads 0.83–0.91 ms at 2560 ×
   1440 (D632), and 1.5–2.5 ms at 4K in the lab's later runs; the terrain layers are +0.64–0.80 on top (§3.3).
   **Estimated: the switch costs about 2–3.5 ms a frame at 4K on the 3070 — about a quarter of that in a 1920 × 1080
   window.** ⭐ **Recommended:** A11's 1 ms stays the bar for the terrain layers, and a second bar covers the switch as
   a whole — today's valley against everything on — at **3.5 ms at 4K** (about a fifth of a 60 fps frame at 4K, a
   twentieth at 1080). Both measured in the wired valley, A/B inside one session (art trap 8), at the widest and the
   gameplay zoom. Over either, the first lever is §3.3's: bake the snow's threshold and the bleed's warp into spare
   channels, one texture read for four noise lookups; the next is the grass's own hex tiling, which is the approved
   look and so his.
5. **Where the switch lives** *(new — the lab has a panel; the game does not)*. ⭐ **Recommended: a tick on the settings'
   view list beside *draw the tile grid*, off until he approves the look**, then on by default with today's valley as
   its off. That is about six lines in `Main.cs` (`_viewToggles`) — the systems lane's file and not on the art lane's
   list, so it needs his OK. *The alternative,* a key with no tick, touches `Main.cs` too (its input). Everything else
   lives in `VillageMap.cs`, `ValleyTexture.cs` and `shaders/`, and the new probe lines ride in checks `Main.cs`
   already prints (`ValleyTexture.SelfCheck`).
6. **Snow through the year** *(new — the lab drives it with a slider; in the valley the season does, A9, and no number
   says how)*. ⭐ **Recommended: snow follows the grass's own winter blend** (`seasonal-grass.md §6.4`): it starts to lie
   as autumn turns over its last ten days, lies through winter's first twenty, and melts as winter turns to spring over
   its last ten — so the snow and the winter grass come and go together, and the patches show on the way in and out (the
   place threshold, §2). A pure function of the tick within the season, a uniform per frame; the sim untouched. **How
   deep mid-winter goes:** ⭐ **1, the whole valley white** (the value band was checked at full Lerp snow — no new
   failure, §6); or **0.56**, the patchy cover he looked at in the lab, all winter long.

### 5.3 Settled without asking — his to overrule

- **His approved values are the look,** read from the two look files and nothing else: the grass at
  `grass_approved.tres` (paper grain 0.12, A10); splat 5.5, bleed 0.33 and 0.30 tiles (A7); the grain world-anchored at
  0.065 tiles (A9); snow Lerp `#f8f6f1`, patchiness 0.03, patch 3 tiles (A9); the ground textures repeating plainly at
  64 tiles (A14–A21 — hex stays off, it was over the bar, A14). ⚠️ **Coverage fade 0.25 is the fake map's and not
  approved:** the real coverage is the first new thing he judges in the valley (A9).
- **The new-game preview, the title backdrop and the minimap stay on the bake and the palette** (`ground-textures.md §6`'s
  open line): the four tiles' means sit 0.07–0.40 L\* from the palette (woods 0.23, stone 0.40, iron 0.19, water 0.07),
  too close to see at a preview's few pixels a tile.
- **The grass grows wherever the bake says grass** — worked ground included, under the fields, which cover it; through
  the paths and washes it shows in part. Whether a worn path or a building's footprint clears the grass is a later piece
  (it would read the sim's wear, a second counter to watch), and only if he asks.
- **A7's warning stands:** at bleed scale 0.30 the warp's cycle is shorter than its reach (a fray, not a wander), so a
  land feature one tile thin is looked for in the real valley (D342's rule).
- **The lab does not change**, nor do its 32 reference renders: the valley gets its own shader,
  `shaders/valley_terrain.gdshader`, which includes the lab's files and calls the same functions; anything added to
  `terrain_lab.gdshaderinc` is a new function, and the lab's renders are `cmp`'d before and after.

- **A before/after slider (A23, Joe's ask): built, as part of piece 2, behind a second tick** (*before/after slider*,
  under the first). Not insane: the old valley is already a loaded image, so the shader draws it left of a line and the
  new look right of it — one uniform, a few lines, and the new look's cost is paid only on its side. The fiddly half is
  the handle: drawn by `VillageMap`, and a drag that starts on it must move the line rather than pan the map. And it is
  not used once — every later ground painting (fields, quarry and mine floors, seasonal woods) is judged against what
  it replaces. Off, nothing is drawn and the tick costs nothing.
- **Falling snow (A23, Joe asked): not in this slice.** This slice is snow lying on the ground. Snow in the air is weather
  drawn over the whole map — its own layer, its own cost, and its own questions (how thick, how often; the sim has no
  weather, so it would follow the season as the ground's snow does, never the sim). Recorded on HANDOFF's art list as a
  later piece, his to ask for.

### 5.4 The build, after his answers — three pieces, one commit each, each ending at his eyes

| # | piece | what proves it |
|---|---|---|
| 1 | **The weights in the bake** (`ValleyTexture.cs`): `Winner`'s weights kept, the two images (call 1) written in the same loop and patched incrementally like the colour, saved beside the bake's PNG under `logs/` | the colour bake byte-identical (its PNG `cmp`'d before and after; `valley bake:` and `seams drawn:` unchanged); the weights' incremental re-bake equal to a full one and the fast path equal to the slow, as the colour's are; **the weights' hard contour against the bake's own winner** — every 16-px pixel's kind read back from the 8-px weights, bilinear, at power 48: the share that disagree, and the farthest a disagreeing water pixel lies from the bake's bank (D342; measured first, then the floor set under it); the bake's time and the upload, printed |
| 2 | **The valley through the shader** (`VillageMap.cs`, `valley_terrain.gdshader`, the tick): a child canvas item behind the map draws the beyond, the parchment and the shaded valley, and `VillageMap` skips those three when the tick is on; per frame, uniforms only (D338) — pixels a tile, the season and its blend, the snow's amount; **and the before/after slider** (A23: a second tick, the old bake left of a draggable line, the new look right of it) | **tick off:** `VillageMap`'s draw calls unchanged, and windowed renders of a fixed valley at fixed zooms `cmp`-identical before and after (a scratch render script — `tools/` is not the lane's); the probe asks the compiled shader for its uniforms (art trap 6), the tick opens off, every value is the look files'; **on:** renders, then Joe's eyes |
| 3 | **The cost, in the valley** | call 4's two bars, A/B in one session; the snow's threshold and the bleed's warp baked only if over |

**Piece 1, as built (A24, 2026-11-09).** `ValleyTexture(withWeights: true)` keeps the mix; **the default does not**, so
the game, the new-game preview and the title pay nothing until piece 2's tick asks (lean, A23). `Winner` is split: `Weigh`
does the warp, the kernel and the jitter and hands back every kind's weight, `Winner` picks from them as before, and the
mix's texels (`PaintTheMix`, in `PaintOneTile`'s loop, so a felling patches both) take the same weights at each texel's
exact centre (`TexelCentre`: `2·wx + 0.5` colour pixels — the sample point is a float now, which an int converts to
exactly). The two images are `WeightsTexture` (RGBA8 960 × 640) and `SharesTexture` (RG8), 3.69 MB, uploaded whole on a
re-bake. The probe's `valley mix:` line, beside `valley bake:` in the line `Main.cs` already prints, saves
`logs/valley-mix.png` (opaque for eyes: red grass, green wood, blue seam, black water) and the shares.

| measured on the founding valley (seed 12345) | |
|---|---|
| the colour bake | **byte-identical** — `logs/valley-bake.png` `cmp`'d against the one saved before the change; `seams drawn:` unchanged (99.8 % / 91.1 %) |
| the mix's kind against the colour's, read as the shader will (bilinear, the top of four, shares at a half) | **99.85 %** of 1,627,392 boundary pixels; 2,361 differ (floor 99.5 %) |
| water drawn where the bake has none, or none where it has | **444 pixels, at most 1 px — a sixteenth of a tile** — from the bake's bank (floor 2 px, an eighth) |
| the whole bake with the mix, against the colour alone | **~1.75 s against ~1.2–1.4 s** (+0.4–0.5 s, once, at the founding); a felling's patch 47 → ~60 ms; the mix's upload **1.0 ms** |
| the colour alone, before and after | 1,202 → 1,224–1,366 ms across five runs, the earlier side of it the same position in the check; the preview 374 → 376–397 ms — inside the runs' spread |

**Piece 2, as built (A25, 2026-11-09).** `shaders/valley_terrain.gdshader` and `VillageMap.Watercolour.cs`; two
Settings ticks in `Main.cs` beside *draw the tile grid* (Joe's OK, A23): ***paint the valley in watercolour*** and
***before/after slider over the watercolour***, both off, both remembered like every map tick (D504).

- **The layer.** A `Control` child of the map, `ShowBehindParent`, its one rect the map's whole size, built on the tick's
  first switching on — the shader, the masks, the four tiles and a bake that keeps the mix (A24) are loaded then and never
  before (lean). The shader paints the map's Beyond outside the valley (it no longer has the map's rect under it), the
  bake left of the line, the new look right of it. `VillageMap._Draw` leaves out the Beyond, the parchment and the bake
  with the tick on; everything else it draws as before, over the layer.
- **The look** is the lab's functions on the real mix: the approved grass (masks hex-tiled, height blend, macro), the
  ground's mix bled (0.33 / 0.30 tiles) and splatted (5.5) between land kinds with the water's contour kept, Joe's four
  painted tiles plainly repeating at 64 tiles, young wood the woods tile × the young ratio by the young share and iron
  against stone by the iron share (both sharpened at the splat's power), the shallows derived, coverage at fade 0.25, the
  snow (Lerp, `#f8f6f1`) on the season, and the grain (0.12 at 0.065 tiles) on the ground only. Every value is read from
  `grass_approved.tres` and `terrain_lab.tres`; the shader holds none. ⚠️ **One departure from the lab, for lean:** a
  painted kind is read only where its share exceeds 0.002 (half a level of 255); in the lab every kind's share is above 0
  wherever the splat is on, because the splat's floor `(1e-3)^5.5` does not underflow, so the lab reads every tile at
  every pixel.
- **The year** (`TheYearAt`, pure in the tick): the season holds two thirds, turns over the last third (D628); snow =
  autumn's turn, then winter's hold whole, then winter's turn melting (A23).
- **The line** (Joe's image): a hairline in the map's ink, a handle with two chevrons at mid-height, *Before* and
  *After* at the top; a press within 10 px of it takes hold before anything under it is clicked, a drag moves it, a release
  lets go. It opens mid-map.

| measured, 2026-11-09 | |
|---|---|
| **tick off against the build before piece 2** | **byte-identical** — windowed renders of the founding valley at tick 0, the opening zoom and the widest (`cmp`, scratch `render.gd`: the frame-sample door, the tree paused at founding); and tick off again after on, byte-identical to tick off first |
| the probe | `watercolour:` ✅ — opens off with nothing loaded; on, the shader declares all 46 uniforms the map sets, each value the look files' and the palette's; the mix and the colour handed over; the line taken and dragged, a press 40 px off not; the year's grass and snow on five moments; off, as it was |
| **the GPU, 4K, RTX 3070** (scratch `measure4k.gd`: the game inside a 3840 × 2160 SubViewport, `lab_cost.gd`'s method; three rounds a case alternating off and on in one sitting, medians) | **opening zoom 0.28 → 1.63 ms (+1.35); widest 0.36 → 2.09 ms (+1.73)** — inside call 4's 3.5 ms bar for the whole switch by half. The terrain layers' own share against A11's 1 ms needs a grass-only case: piece 3. ⚠️ At 1280 × 800 the same A/B read +0.9–1.0 ms — the GPU at low clock on a light load (art trap 8); the 4K run is the measurement |

⚠️ **Not measured by anything but eyes:** what it looks like in play — the seasons turning, the snow, fields and paths over
the grass, the one-tile seams under the bleed. Joe's to look at.

⚠️ **Found reading the mix:** several of the founding valley's seams are **one tile thin** (the blue threads in
`valley-mix.png`). A7's warning is about exactly that, the bleed fraying a one-tile feature, so piece 2 looks there first.
⚠️ **Fixed in passing:** the colour's own self-check put every felled tile back as *forest*, so a sapling it felled came
back grown and the probe went on with a valley the check had changed; it now puts back what was there.

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
- **Red-checked, piece 2 (A3):** 24 probe mutants, **28 reds, no zeros** (the file's switches and value, both shaders'
  uniforms and a grain shader that does not compile, the switch, the zoom and the window's size reaching the grain, the
  slider, the order of grain and village, *Reset* for each, the pan reaching grass, grain and placeholders, its sign,
  its button, its clamp, *Reset* leaving it, **c**, and *Copy*). ⚠️ **One scored zero on the first run** — the
  placeholders ignoring the view, because the probe read their `View` and not where they draw; `ToScreen` became a method
  the probe asks, and the mutant then went red. With every switch off the renders are still byte-identical (5 of 5).
- **The anchoring, measured** (piece 2, windowed, not in the probe): a pan of 84 × 54 px at 24 px a tile, and the panned
  picture compared with the unpanned one shifted by the same: **world grain 0.00 % of pixels off by more than 2 levels,
  the screen grain 88 %**, the grass with no grain 0.00 %. Nothing mirrors the octaves' fade; that is eyes.
- **Red-checked, piece 3 (A4):** 19 probe mutants, **24 reds, no zeros** (the file's switch, amount, blend and colour,
  a renamed uniform and an include that does not compile, the switch, the blend's uniform and its order, each slider
  reaching the wrong uniform or none, *Reset* for every control, the colour not the file's, and *Copy*). ⚠️ **Nine
  scored zero on the first run, every one an anchor and not a guard:** the edit scripts had written CRLF (HANDOFF trap
  11), so an anchor ending in `\n` matched nothing; rerun on LF, all nine went red. **Two render mutants the probe
  cannot see:** snow drawn whatever the switch says changes **5 of 5** approved renders; snow let onto the river
  changes **62,932** deep-water pixels. Restored, 0 and 0.
- **Red-checked, piece 4 (A5):** 16 probe mutants, **20 reds, no zeros** (the file's switch and reach, a renamed
  uniform and an include that does not compile, both switches, each slider reaching the wrong uniform or none, *Reset*
  for all five, the reach past half a tile, *Copy*). Every anchor was checked against an LF file first (trap 11).
  **Three render mutants:** the bleed drawn with its switch off changes the coverage map's render (piece 2's
  byte-identical one); the water's edge softened with the splat changes the river at **19,256 px** (built: 10); the
  water warped by the bleed changes it at **30,715 px** (built: 1). ⚠️ `bleed's limit` prints only when it fails: a
  guard with no green line, kept so because it is a property of the slider and not a control.
- **Splat and bleed, measured** (piece 4, grain off, coverage map on, against both switches off): with both off the
  map's render is byte-identical to piece 2's; splat at 48 and bleed at reach 0 are within **1 level** everywhere;
  the river's drawn pixels (88,657) differ at **1–3 px** under the bleed at its full half-tile reach, at either scale,
  and at **10 px** under the splat at sharpness 1. 3.8 % of the land changes by more than 2 levels at full reach, 14 %
  at sharpness 1. What they look like is eyes: at 8 the edges soften, at 2 the woods are a wash; at full reach and a
  fine scale the edges fray into droplets.
- **Snow, measured** (piece 3, windowed renders, grain off unless said): amount 0 is byte-identical to snow off;
  amount 1 is `#f4f1ea` in 100.00 % of pixels for Lerp and for Lighten; over the fake map at amount 1, no deep-water
  pixel changes, and the bank's anti-aliased edge (2 px at most) moves a median 2 levels. With the approved grain
  anchored to the world, full Lerp snow spans L\* 84.9–92.4 (p1–p99) and its middle is L\* 88.9: **the grain takes
  snow from 95 to about 89, three above bare parchment (86).** Snow reads as white beside the grass, not beside the
  paper.
- **The value band on snow** (`check.py` run read-only on a scratch copy, its `ground.json` plus two snow grounds
  measured from those renders: full and half Lerp): **no new failure**. 0 of 26 things that must read at 3:1, 0 of 41
  outlined things; every wash still seen (work ground ΔE 7.4 → 6.9, the selected one 14.6 → 12.6). The lightest common
  ground rises from L\* 82.6 to 92.4, and the parchment-coloured workplace falls to 1.06:1 against the middle of the
  snow, but it is outlined. ⚠️ The check judges colours, not draw sites: a light fill that lost its ink edge would
  vanish into snow and still pass (the eye-check in HANDOFF's art list).
- **Red-checked, the valley's piece 1 (A24):** 10 mutant runs, **10 reds, one zero kept**. Determinism (a mix that
  flips by bake), the fast path (a flat forest's mix saying young wood), the incremental patch (the mix skipped on a
  re-bake), the colour untouched (the mix writing a colour byte — 2 reds), the contour (a texel sampled at its corner:
  99.09 %; wood and seam swapped: 56 %; the young share inverted: 61 %) and the water (its share inflated: the bank 4 px
  out, both arms). ⚠️ **The zero:** the first "writes into the colour" mutant flipped `_pixels[wx·4]`, which each tile
  visits eight times, so every flip undid itself — a dud mutant, rewritten to flip each pixel once (M4b). ⚠️ **And one
  red on the wrong arm:** the determinism mutant first failed as *incremental* because a later arm overwrote the earlier
  failure; a failure already said now stands, and rerun it failed on its own arm.
- **Red-checked, the valley's piece 2 (A25):** 12 probe mutants, **14 reds, no zeros** — a shader that does not
  compile, a uniform renamed, a look value not the file's, the tick opening on (2), the layer over the map not behind it
  (2), the bake not swapped for the mix, a press anywhere taking the line, a season holding half, the snow in spring's
  turn, off leaving the layer showing, the line never reaching the shader, young wood in the woods' colour. ⚠️ The second
  red of "over, not behind" was the probe's own fault — its early return left the tick on for the line after; it puts the
  tick back now. **Two render mutants on the tick-off path** (the probe cannot draw): the before/after line drawn with
  the ticks off changes both tick-off renders (2 reds); ⚠️ **the Beyond left out with the tick off scored zero, kept** —
  in both frames the valley fills the window, so no Beyond is on screen to lose. A frame with the valley's edge in view
  is what would catch it; none of today's two has one.
- **Parity does not cover the new layers** (`tools/grass/grass_antitile_ref.py` mirrors the approved path only). Their
  look is Joe's eyes.
- **Rendering only.** `git diff main -- src/Bclone.Sim data tests` stays empty; no golden can move.
- **Not known until the valley is wired:** how any of this reads under the real bake and its dressing, or beside fences,
  fields and paths, which the lab does not draw.

## 7. Toward the concept (A26–A27) — Joe's art direction, five pieces, ink built

**Why (A26, 2026-11-10).** Joe played piece 2 and found it *"quite a ways away from the concept art"* (`art/` — the
inked, painted map with illustrated buildings), and named three gaps: no lines for height, no inked and flowing river
(*"i would like the game to animate this in some way"*), and a winter that is *"just a white blanket"*. Set side by side,
the gaps ranked by cost: **(1) no ink** — the concept is drawn first and painted second, every edge a sepia pen line;
**(2) busy mid-scale mottle** — house-sized yellow and green blotches where the concept has calm washes, the detail at
the edges; **(3) no relief**; **(4) water with no drawing in it**; **(5) illustrated trees and buildings** — the biggest
visual gap, but the art inventory's B4/B5 and building art, not this shader's.

**The brief every piece is judged against.**
- **Ink first, wash second.** One warm sepia-black ink (`#3a2d20`, ~75 %), a line ~1–1.5 screen px, the same at every
  zoom, a little wobbly, the river's bank heavier. Every terrain boundary gets one; worked ground keeps `VillageMap`'s.
- **Calm washes, detail at the edges.** Near-flat tint with faint granulation; pigment pooled darker at each wash's edge;
  the meadow varying over tens of tiles, not two.
- **A narrower, warmer palette**, held to the band (L\* ≥ 50) by `check.py` on a scratch copy.
- **Relief by hachure** — contour lines up the valley sides, hachures down the steeper parts, in the ink, lighter.
- **Water drawn, not filled** — inked bank, two echo lines, a darker channel, flow strokes drifting downstream.
- **Winter is a drawing too** — the ink survives the snow; cool blue-grey shadow washes; woods dark and capped; the river
  dark with an icy rim; cover patchy.

**Joe's four calls (A26, all as recommended):** (1) **relief decorative now** — land rising away from the river, changing
nothing in the sim; when the real height layer comes (D477) the drawing reads it. ⚠️ A deliberate exception to "what is
drawn is real", recorded so no session mistakes it for terrain. (2) **the water flows only while time runs** — pause looks
paused. (3) **winter patchy, with shadows** — replaces A23's fully white mid-winter; the peak about 0.7–0.8, measured and
shown to him. (4) **ink first**, then calmer ground, the river, winter, relief.

**The five pieces**, each behind the watercolour tick, the lab untouched, tick off byte-identical, each at Joe's eyes:

| # | piece | state |
|---|---|---|
| 1 | **Ink and pooling** | 🔨 built (A27); revised at Joe's word (A28): the bank only, a pen stroke, the trees outlined |
| 2 | **Calmer ground** — the grass's strength and macro variation at the wide zoom, a low-frequency meadow tint for the mottle, the coverage fade. ⛔ Changes the approved grass look (D632, A10): every value his word | — |
| 3 | **The river drawn** — echo lines, darker channel, flow strokes along the river's tangent, downstream by the valley's *River runs*, on a clock that stops with the game | ⭐ **approved (A47)**; 🔨 **built (A45, `river-drawn.md §9`)** at Joe's *"All as recommended"*, the ripples keeping the game's speed (A46); the river +0.04 ms at 4K. ✍️ **specced (A44, `river-drawn.md`)**: two still echo lines, a channel darker down the middle, ripples drifting downstream on a view clock that stops when paused, all laid in the river's own coordinates from a small table built once from its tiles; ⏸️ four calls Joe's. Nothing built |
| 4 | **Winter with structure** — the call-3 peak, shadow washes, capped canopies (`VillageMap`'s canopy colours by `TheYearAt`), the icy rim | 🔨 **built (A41)** — `SnowPeak`, the drifts' cool lee, the icy rim and the darker channel in `valley_terrain.gdshader`, values in `valley_watercolour.tres`; ⭐ **approved by Joe (A42), merged into `main` and pushed (A43)**. ✍️ **specced with the trees' seasons as one piece (A40, `tree-sprites.md §9`)**: the ground as A26 settled it; the canopies are the painted trees now, so *"capped canopies"* is their winter atlas, not the crowns' colours |
| 5 | **Relief** — a "height above the river" field baked once beside the mix (distance to water, blurred; it never changes in play), contour lines and hachures | 🔨 **built (A66, `hill-edges.md`)**: an inked lip on steep banks, a lit side, a shaded bank, from the sim's heights, Joe's B. ~~⏸️ **waits for real heights in the sim (A57).**~~ Joe, told the sim has no height and that a faked one would draw hills the game does not have (legibility): *"i do want to add in real heights in the simulation"*. So relief is drawn from the sim's own height once it exists, and ~~a height above the river baked from distance to water~~ is dropped |

**Piece 1, as built (A27, 2026-11-10).** The values live in a new look file, `looks/valley_watercolour.tres`
(`ValleyWatercolourLook.cs`, no defaults, like `GrassLook`): ink `#3a2d20` at 0.75, 1.2 px, the bank × 1.4, wobble 0.35;
pooling 0.16 over 3 px. **Nothing in it is approved yet.**
- **The line** is drawn where a kind's share crosses a half — the contour where the colour changes kind — its distance
  in screen pixels from the share's own slope (`dFdx`, `dFdy`), so it is the same width at every zoom; its width wanders
  by one octave of `at_value_noise` (salt 91). Woods, seams and the river each get one, and stone meets iron by a line
  where a seam is drawn; forest and young wood do not (a line there would read as a fence).
- ⭐ **The pen follows the edge before the bleed, and the wash bleeds past it** — the first render drew the pen on the bled
  edge and it came out ragged and specked: A7's warning (*"a fray, not a wander"*) made visible. The pen now takes the
  unbled shares (one more splat a pixel); the river's is the unwarped weight either way (D342).
- **The pooling** darkens each of the three washes inside its own, bled, edge — under the snow (snow covers the pooled
  wash), while the ink lies over the snow and under the grain.

| measured, 2026-11-10 | |
|---|---|
| tick off | byte-identical to `b6b5af4` at both zooms (`render.gd`, `cmp`) |
| the probe | `watercolour:` ✅, all 53 uniforms declared, the look file's seven values reaching the layer, a file that draws no ink refused |
| GPU, 4K, the 3070, alternating rounds | the switch **+1.87 ms** at both zooms (opening 0.26 → 2.14, widest 0.27 → 2.14) — the ink about +0.05–0.5 of it; inside 3.5 |
| the Beyond, in frame | ⚠️ still never — the widest zoom fits the window at any size (2400 × 1400 tried), so the valley fills every frame; catching it needs a pan to the edge (art trap 21 stands) |

**Piece 1, revised at Joe's word (A28, 2026-11-10).** He played the ink: *"forests/wooded areas and ore seams shouldn't get
ink outlines. look closely at the trees in the concept art - the trees themselves have an outline, but the ground isn't
outlined. add more stroke to the line too … i want it to look like actual ink."* So:
- **The ground is not outlined.** Woods and seams lose their lines and their pooling (a darker rim read as an outline);
  only the river keeps a pen line, and a pooled edge inside its wash.
- **The bank is a stroke, not a line** (`valley_ink_stroke`): the pen's **pressure** swells and thins it along the bank
  (0.65 of its width, a swell every 2.5 tiles) and the ink is a touch denser where laid thick; where the pressure is
  lightest the pen **lifts** and the stroke breaks (under 0.15 of the range); its edge **feathers** into the paper's fibres
  (0.7 px, at the paper's scale, not the land's). 2.6 px at middle pressure, strength 0.9. All world-anchored noise, so
  the stroke holds still on the land.
- ⚠️ **Measured from the river's raw weight, not its drawn share.** The drawn (hard) share crosses from land to water in
  about three pixels and is flat either side, so a stroke measured from it could never be wider than that — 1.2 px and
  2.6 px drew the same line. The raw weight changes over a tile and crosses a half on the drawn bank wherever two kinds
  meet; it also takes most of the facets off the bank (the mix's bilinear runs).
- **The trees are outlined** (`VillageMap.Meshes.cs`, `Canopies`, only while the tick is on): each crown laid on its own
  disc of the ink 0.05 tiles wider, crown by crown, so an overlapping crown draws its edge across the one before — the
  concept's scalloped clumps. Each crown is a cloud, not a coin (`MeshBuilder.Crown`: 32 points, 5–7 lobes 0.24 deep, a
  pure function of where it stands), with a lighter patch (0.28) on its sunlit side, up and left. About 230 vertices a
  tree against 36; at 0 ink not one vertex changes. ⚠️ **A stand-in, not the concept's trees:** those are illustrations
  — and drawn in three-quarter view (trunks showing), which is the art inventory's B4 (projection) and B5 (the tree look).
  Real trees are a painted sprite set, an art asset, Joe's call.

| measured, 2026-11-10 | |
|---|---|
| tick off | byte-identical to `b6b5af4` at both zooms, and after switching on and off (the canopies re-meshed both ways) |
| the probe | `watercolour:` ✅ — 55 uniforms; the trees' ink the file's while on and exactly 0 after |
| GPU, 4K, the 3070 | ⚠️ **this session the card read about twice the earlier ones** (off 0.58 ms, not 0.27). **A/B in one sitting, A27 against A28:** opening +3.0 → +3.2 ms, widest +3.4 → +3.3 — the same cost. Against A23's 3.5 ms bar today's state is close to it; **the cost split (§5.4 piece 3) comes before the river and winter add more** |
| red-check | 3 probe mutants, 3 reds; a render mutant (crowns drawn lobed with the tick off) red at the opening zoom, zero at the widest — no live trees are drawn there |
