# Spec: The terrain shader — the valley drawn over parchment

**Status:** 🔨 **ALL FOUR PIECES BUILT IN THE LAB (the fake coverage map, D701, 2026-11-06; world-anchored grain
and drag to pan, D702; snow, D703; splat blending and edge bleed, D704 — all 2026-11-07). ⭐ JOE APPROVED SPLAT AND BLEED AT SHARPNESS 5.5, REACH 0.33,
SCALE 0.30 TILES (D706); ⭐ AND THE GRAIN AND SNOW AT HIS TUNING (D707–D708): GRAIN 0.065
TILES, UNDER THE VILLAGE; SNOW LERP, `#f8f6f1`, PATCHINESS 0.03, PATCH 3 TILES. THE MAP IS THE LAB'S FAKE, NOT JUDGED.
The GPU bar is 1 ms a frame at 4K for the whole stack (D710, §3.3); everything on is +0.64–0.80 ms, inside it.** §2–§4 are the build; §4's table says which piece stands; §5
waits for house tiers.
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

**The snow colour** is `#f8f6f1`, L\* 96.9, since D707 (Joe: D703's `#f4f1ea`, L\* 95.2, was "a bit too grey";
"a touch whiter" raised every channel by about four levels and kept the warmth): the near white §2 asks for, warmed to sit on the paper rather than read as
screen white. It lives in `looks/terrain_lab.tres`, not in the shader.

**As built (D703):** `terrain_lab.gdshaderinc`'s `terrain_snow_*`, called from `grass_lab.gdshader` after the grass and
before the transition marks, so the grain overlay lies over it. *Snow*, *Snow blend* (Lerp, Screen, Lighten),
*Snow amount* (lab start 0.56), *Snow patchiness* (0–0.3, start 0.03) and **a fifth control the four above did not name,
*Snow patch size*** (2–48 tiles a cycle, start 3; the starts are Joe's since D707, D703 opened at 0.6, 0.08, 12): the place needs a size, and a number that only lives in a file
cannot be judged by eye. The threshold is two octaves of `at_value_noise` at salts 71–72, the macro variation's
weights. The amount is stretched by the patchiness before the smoothstep, `smoothstep(t − s, t + s, mix(−s, 1 + s,
amount))`, so 0 is bare everywhere and 1 white everywhere whatever a place's threshold (measured: amount 0 renders
byte-identical to snow off; amount 1, Lerp and Lighten, is `#f4f1ea` in 100 % of pixels, grain off; at D707 Lerp is `#f8f6f1` in 100 %). ⚠️ The noise is
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
  snow ground, and what it says goes in §6 before snow ships. ⚠️ **(D703: the dot is stale.** The adult's dot is
  `#454135` in today's `VillageMap.cs`, dark. The light fills now are the cattle `#e6dcc4`, the sail `#efe6d2` and the
  flour heap `#ebe0c7`, and each is outlined, so on snow they read by the outline alone. §6 has the check's result.)

## 3. The rest of the shader

### 3.1 Paper grain — anchored to the land

⭐ **Anchored to the world, with the strength the lab's existing *Paper grain* slider.** The placeholder hashes screen
pixels, so in a game that pans the paper would stay still while the valley slid under it: a dirty window, not a drawing.
Built as a switch on the same overlay (`paper_grain.gdshader`): tile space in place of `FRAGCOORD`, the size a *Grain
scale* slider in tiles, integer hashes (the sine hash is not the same on every GPU, and §6 asks that a place always looks
the same).

**As built (D702):** the three octaves keep the placeholder's ratios (1.3 : 5 : 37), *Grain scale* is the finest one's
cycle in tiles (0.02–0.5), and each is `grass_antitile.gdshaderinc`'s `at_value_noise` at its own salt (61–63). The
lab started at 0.08 tiles: two pixels a cycle at the lab's opening zoom, so the fibres are whole there and nowhere
wider. **Since D707 it starts at Joe's 0.065:** 1.6 px a cycle at 24 px a tile, so there the finest octave is about
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
  above the map and below the UI, not a line in this shader. ⚠️ **Built as *Grain under the village*, off** (D702; the
  spec said *over*, on): a switch that opened on would break §4's rule that every switch opens off and all off is the
  approved look, which the probe checks. Off is D630's order; on moves the grain between the ground and the
  placeholders, so only the land has tooth.

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

**As built (D704):** *Splat blending* and *Splat sharpness* (1–48, lab start 5.5), *Edge bleed*, *Bleed reach* (0–0.5
tiles, start 0.33) and *Bleed scale* (0.25–4 tiles a cycle, start 0.30). The starts are Joe's first tuning (D705;
D704 opened at 8, 0.25 and 1), ⭐ **approved by him at those values (D706)**; the switches still open off, and the
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
`grass-look.md`'s table. Bar: the D632 budget's size, about +0.1 ms a frame at 1280 × 800 for the whole stack (until D710). ⭐ **Since D710,
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
| **coverage map** (D701; 2026-11-06, the slow session) | +0.50 ms | +0.49 ms | **+5 %** |
| the placeholder's screen grain at the approved strength (D630; 2026-11-07) | +0.34 ms | +0.31 ms | +22 % / +20 % |
| **world-anchored grain** in its place (D702; same run) | **+0.13 ms over it** | **+0.12 ms over it** | **+7 %** over the screen grain; +0.016 ms at 1280 × 800 |
| world grain and the coverage map together (same run) | +0.53 ms | +0.53 ms | +35 % / +34 %; **+0.19 / +0.22 ms at 4K over the approved look with its grain, +0.027 ms at 1280 × 800: inside the +0.1 bar** |
| **snow**, Lerp, over the approved look with its grain (D703; 2026-11-07, a second run: *approved, no grain* read **2.51 ms** at 4K, against 1.51 that morning — trap 8 again) | **+0.43 ms** | **+0.43 ms** | **+14 %** over approved with grain; +0.053 ms at 1280 × 800. Lighten the same within 0.02 ms |
| world grain, coverage map and snow together (same run) | +0.78 ms over approved with grain | +0.78 ms | +26 %; **+0.096 ms at 1280 × 800: AT the +0.1 bar, with piece 4 still to come** |
| *(D704; 2026-11-07, a third run: *approved, no grain* 2.48 ms)* **coverage map** alone, over approved with grain | +0.16 ms | +0.16 ms | +5 %, as D701 found in the slow session |
| **splat blending**, over the map | **+0.08 ms** | **+0.09 ms** | +3 % |
| **edge bleed**, over the map | **+0.34 ms** | **+0.33 ms** | +11 %: a second read of the map and two noise lookups |
| **everything** — world grain, map, snow, splat, bleed (same run) | **+0.64 ms over approved with grain** | **+0.80 ms** | +21 % / +27 %; **+0.078 / +0.098 ms at 1280 × 800: AT the +0.1 bar.** D703's run read the stack without splat and bleed at +0.096; this run read it at +0.074, so the runs differ by ±0.02 ms, about the size of piece 4 |

⚠️ **The bar is spent before piece 4** (D703; that was the 0.1 ms bar — D710 made it 1 ms at 4K, which the stack fits). **(D704: and the whole stack lands on it, inside the runs' own noise.
The bleed is the dear one; with splat alone the stack stays under.)** Snow's threshold is two octaves of value noise, eight integer hashes a
pixel, about what the world grain's three octaves cost. Piece 4's warp adds more noise. So either the bar moves, which
is Joe's call, or the noise is shared: snow's threshold and the bleed's warp could read one octave set, and in the game
a place's threshold is a constant that the bake could store in a channel, costing nothing per frame (§5).

## 4. The build — four pieces in the lab, one commit each

Each piece is droppable: the branch is whole after any of them, and Joe can look at one and leave for a week.

| # | Piece | Controls (the *Terrain* panel) | State |
|---|---|---|---|
| 1 | **The fake coverage map** (`GrassLabGround.cs`) | *Coverage map* switch · *Coverage fade* | 🔨 built (D701), Joe's eyes pending |
| 2 | **World-anchored paper grain**, and drag to pan | *World-anchored grain* switch · *Grain scale* · *Grain under the village* (§3.1: built *under*, off) · drag, **c** centres | ⭐ approved (D708): 0.065 tiles, under the village |
| 3 | **Snow** | *Snow* switch · *Snow blend* (Lerp, Screen, Lighten) · *Snow amount* · *Snow patchiness* · *Snow patch size* (§2: a fifth, built D703) | ⭐ approved (D708): Lerp, `#f8f6f1`, patchiness 0.03, patch 3 tiles |
| 4 | **Splat blending and edge bleed** | *Splat blending* switch · *Splat sharpness* · *Edge bleed* switch · *Bleed reach* · *Bleed scale* | ⭐ approved (D706): 5.5, 0.33, 0.30 tiles; §3.2: the water keeps its hard edge under both |

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
- **The GPU bar** (§3.3, D703–D704; 1 ms at 4K since D710, which the lab's stack fits): measured again in the wired
  valley, whose own costs the lab cannot see (`Dressed`'s noise, below). If it goes over, two things only the wired
  valley can do would buy room (the bake's 16 px a tile holds even the approved 0.30-tile warp at about 5 px a cycle): the snow's per-place threshold and the bleed's warp are pure functions of the tile, so the bake could store
  them in a channel and the shader read them for one texture fetch instead of four noise lookups.
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
- **Red-checked, piece 2 (D702):** 24 probe mutants, **28 reds, no zeros** (the file's switches and value, both shaders'
  uniforms and a grain shader that does not compile, the switch, the zoom and the window's size reaching the grain, the
  slider, the order of grain and village, *Reset* for each, the pan reaching grass, grain and placeholders, its sign,
  its button, its clamp, *Reset* leaving it, **c**, and *Copy*). ⚠️ **One scored zero on the first run** — the
  placeholders ignoring the view, because the probe read their `View` and not where they draw; `ToScreen` became a method
  the probe asks, and the mutant then went red. With every switch off the renders are still byte-identical (5 of 5).
- **The anchoring, measured** (piece 2, windowed, not in the probe): a pan of 84 × 54 px at 24 px a tile, and the panned
  picture compared with the unpanned one shifted by the same: **world grain 0.00 % of pixels off by more than 2 levels,
  the screen grain 88 %**, the grass with no grain 0.00 %. Nothing mirrors the octaves' fade; that is eyes.
- **Red-checked, piece 3 (D703):** 19 probe mutants, **24 reds, no zeros** (the file's switch, amount, blend and colour,
  a renamed uniform and an include that does not compile, the switch, the blend's uniform and its order, each slider
  reaching the wrong uniform or none, *Reset* for every control, the colour not the file's, and *Copy*). ⚠️ **Nine
  scored zero on the first run, every one an anchor and not a guard:** the edit scripts had written CRLF (HANDOFF trap
  11), so an anchor ending in `\n` matched nothing; rerun on LF, all nine went red. **Two render mutants the probe
  cannot see:** snow drawn whatever the switch says changes **5 of 5** approved renders; snow let onto the river
  changes **62,932** deep-water pixels. Restored, 0 and 0.
- **Red-checked, piece 4 (D704):** 16 probe mutants, **20 reds, no zeros** (the file's switch and reach, a renamed
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
- **Parity does not cover the new layers** (`tools/grass/grass_antitile_ref.py` mirrors the approved path only). Their
  look is Joe's eyes.
- **Rendering only.** `git diff main -- src/Bclone.Sim data tests` stays empty; no golden can move.
- **Not known until the valley is wired:** how any of this reads under the real bake and its dressing, or beside fences,
  fields and paths, which the lab does not draw.
