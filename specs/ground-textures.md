# Spec: Ground-kind textures — Joe's watercolour, one seamless tile per kind

**Status:** ✅ **SPECCED (A12); `ground_woods.png` PAINTED AND MEASURED (A13) AND APPROVED BY JOE (A14); PIECE 1 BUILT IN THE LAB (A14, 2026-11-07): the loader, the *Ground textures* switch, *Texture tiles per repeat*, *Texture anti-tiling* (Off, Hex), *Young wood preview*, every switch off at opening and the approved renders byte-identical. ⭐ MERGED INTO `main` AT JOE'S WORD (A15, 2026-11-08). 🎨 `ground_stone.png` AND `ground_water.png` PAINTED AND MEASURED (A16, 2026-11-08); 🔨 PIECE 2 BUILT IN THE LAB (A17, 2026-11-08): the one *Ground textures* switch draws all three painted kinds, the shallows derived from the water tile; every switch off at opening and the approved renders byte-identical. ⭐ APPROVED BY JOE (A18, 2026-11-08: *"approved"*) — the stone and water tiles, the derived shallows, one switch for all three. ⭐ MERGED INTO `main` AT HIS WORD (A19, 2026-11-08: *"Merge art now"*) and pushed (*"Push main"*). 🎨 `ground_iron.png` PAINTED AND MEASURED AND 🔨 PIECE 3 BUILT IN THE LAB (A20, 2026-11-09): the fake map's fifth kind rides on the seam, the one switch draws all four painted kinds; every switch off at opening, the 32 approved renders byte-identical. ⭐ APPROVED BY JOE AND MERGED INTO `main` AT HIS WORD (A21, 2026-11-09: *"approve and merge now"*); not pushed. All four ground kinds painted; the valley's wiring waits for house tiers (§1).**
The format Joe paints to, before he paints. The art lane's next slice after the terrain shader (`terrain-shader.md`, A1–A11),
lab only until house tiers merge (`terrain-shader.md §5`). Joe's calls, 2026-11-07: **full colour**, not a value-only wash;
**woods first**, through the whole loop, before the other three are painted. Owner: Joe + Claude Code.

---

## 1. What this is, and what it is not

Today every ground kind is a flat fill in the band, `VillageMap`'s palette, and the terrain shader draws it that way
(`terrain-shader.md §3.2`: *"washes and textures per kind are Joe's paint … a later input without changing shape"*). This
replaces each kind's **flat colour** with a texture Joe paints in Photoshop, the way he painted the grass layouts
(`seasonal-grass.md §1`). Nothing else in the shader changes: the weights still choose the kind, splat and bleed still draw
the edges, the grass still multiplies over the parchment, snow still lies on top.

| Joe paints | The shader derives, nothing painted |
|---|---|
| **woods floor** (`Terrain.Forest`, `#9aa47a`, L\* 65.6) | **young wood**: the woods tile × (young ÷ woods) per channel, `#b3b98d` ÷ `#9aa47a` |
| **stone seam** (`Terrain.Rock`, `#b4ab99`, L\* 70.3) | — |
| **iron seam** (`Terrain.IronDeposit`, `#b9906f`, L\* 62.9) | — |
| **water** (`Terrain.Water`, `#6f97ad`, L\* 60.4) | **shallows**: the water tile × (shallows ÷ water) per channel, read off the same weight as today's depth ramp, so the bank and the fill cannot disagree (D338) |

**Not in this spec:** the ploughed, sown and ripe fields and the quarry and mine floors. Those are drawn by `VillageMap.cs`,
which is the systems lane's file, so they wait for house tiers. Clay has no terrain kind in the sim. Paving has no sim
(`DESIGN.md §2.6`). Seasons for the woods: no spec asks for them, and they can be added later as more tiles.

## 2. The file Joe delivers

| | |
|---|---|
| **Name** | `ground_woods.png`, then `ground_stone.png`, `ground_iron.png`, `ground_water.png` |
| **Pixels** | **2048 × 2048**, RGB, 8 bits a channel, sRGB, PNG. Alpha is ignored; leave it out |
| **Seamless** | It must wrap left ↔ right and top ↔ bottom. In Photoshop: *Filter → Other → Offset* by 1024, 1024, paint out the cross, offset back |
| **Scale** | The tile covers **64 × 64 game tiles**: **32 px of the painting is one tile**, the grass's own density (2048 for 63 tiles, `grass-look.md §2`). A house is 2–4 tiles (64–128 px); a villager's dot is about a third of a tile (~10 px) |
| **The source** | The PSD stays in Joe's pack, outside the repo, as the grass's does (`seasonal-grass.md §1`). The repo gets the PNG |

**How to paint it so it survives the game:**
- **It is a map wash, not a photograph.** No cast shadows, no light from one side, nothing that says "this is a slope". The
  valley is flat and top-down.
- **Nothing memorable.** A single distinctive blot repeats every 64 tiles, and even turned and shifted (§4) the eye finds it.
  Even granulation, several overlapping washes and soft blooms hide the repeat. One loud mark gives it away.
- **Detail under ~5 px is lost when the view zooms out.** At the widest view (13 px a tile) the painting is shrunk 2.5×.
  At the closest (48 px a tile) it is enlarged 1.5×, so 32 px a tile is enough for watercolour, which is soft anyway.
- **The colour is the real colour.** What Joe paints is what the game draws (A12's call: full colour). Hue shifts inside a
  kind, like warm and cool granulation in the stone, are welcome.

## 3. The limits, which are checked rather than asked for

The ground is a band, and the world's dots and lines are dark so they read on it (`world-legibility.md §9`, D637). A tile
that dips too dark takes the band with it.

| rule | value | why |
|---|---|---|
| **No pixel darker than L\* 50**; aim for the darkest 1 % at **L\* 55 or above** | the ground band | the dots and lines are L\* ≤ 29 and must read at 3 : 1 |
| **The mean within ±5 L\* of the kind's palette colour**, the same hue family | woods 65.6, stone 70.3, iron 62.9, water 60.4 | the kinds must still tell apart, and the minimap and the bake use the palette |
| **Stone and iron stay apart in hue** | stone grey-warm, iron rust | D67 |

**Measured, not eyeballed:** each piece reports, for every tile, its L\* at the 0.1, 1, 50 and 99 percentiles and its mean
ΔE to the palette colour. `tools/legibility/check.py` is run on a scratch copy with the textured grounds sampled in, as A4
did for snow. A tile outside the limits is shown to Joe with the numbers before anything is approved.

**Woods, measured (A13–A14).** `ground_woods.png`: L\* min 54.00, p0.1 58.25, p1 60.13, p50 65.95, p99 70.68, max 74.25;
mean `#9ba57b` (L\* 65.84, a\* −10.67, b\* 20.33) against `#9aa47a` (65.61, −10.92, 20.67), hue 117.7° vs 117.8°, mean
per-pixel ΔE 4.62. `check.py` on a scratch copy with the tile's mean, its darkest 1 % (`#8d926e`, L\* 59.3) and its darkest
0.1 % (`#8a8d6b`, L\* 57.6) sampled in as grounds: all three in the band, **0 of 41 outlined things and 0 of 26 marks fail**,
unchanged. The villagers' dots (L\* 27–28) read **3.9 : 1 against the tile's mean, 3.14 : 1 against its darkest 1 %, and
2.96 : 1 against its darkest 0.1 %** (3.87 : 1 against today's flat woods); the ink outline 4.6 : 1 at worst. The rarest
dark pixels sit a hair under 3 : 1, which the check reports and does not fail (it judges marks against p1, as the grass
is judged). ⚠️ If Joe wants the margin, the tile's floor is a *painting* knob — less grain and mottle, or a lighter base
— and not a shader one; a rebuild is a minute.

**Stone and water, measured (A16).** `ground_stone.png`: L\* min 58.71, p0.1 63.54, p1 65.37, p50 70.82, p99 74.89, max
78.62; mean `#b5ac9a` (L\* 70.67, a\* −0.04, b\* 10.24) against `#b4ab99` (70.27, 0.13, 10.43), hue 90.2° vs 89.3°, mean
per-pixel ΔE 4.27 — granulation, warm `#b79e82` and cool `#9fa497` drift inside it, hue 90° against iron's 65°.
`ground_water.png`: L\* min 57.72, p0.1 58.18, p1 58.57, p50 60.47, p99 62.07, max 63.29; mean `#6e97ae` (L\* 60.43,
a\* −8.24, b\* −16.39) against `#6f97ad` (60.36, −8.10, −15.87), hue 243.3° vs 243.0°, mean per-pixel ΔE 1.57. Both
seamless (wrapped edge at or under the interior mean). `check.py` on a scratch copy with each tile's percentiles sampled
in as a ground: **0 of 26 marks and 0 of 41 outlined things fail**, unchanged. The dots read **4.53 : 1 against the stone's
mean, 3.84 : 1 at its darkest 1 %, 3.62 : 1 at 0.1 %**; **3.26, 3.07 and 3.03 : 1 on the water**.
- ⛔ **For water the §3 floor is not the binding limit — the dots are.** The palette's water is L\* 60.4, and every
  mark at L\* ≤ 29 holds 3 : 1 only down to about **L\* 58.2**, so flat water already passes with 0.2 to spare (all 26
  marks at 3.1–3.2 : 1). The first water tile met §3 (p1 56.4) and **put 21 of the 26 marks under 3 : 1**. The tile that
  ships keeps its value drift inside ±2 L\* (p1 58.6) and carries the wash in **hue** instead — green `#6894a3` to blue
  `#6e94b5` at one lightness. *A darker or busier water is a palette call (lighter `WaterColour`), not a painting one.*
- ⚠️ The work-ground wash (`#4a9ba8` at 16 %) is not seen over water (ΔE 2.2) — over flat water too; the tile changes
  nothing there. Reported for the wiring, not this piece's.

**Iron, measured (A20).** `ground_iron.png`: L\* min 54.52, p0.1 57.75, p1 59.07, p50 63.12, p99 66.45, max 69.25; mean
`#ba9070` (L\* 63.04, a\* 11.12, b\* 23.10) against `#b9906f` (62.85, 11.01, 23.47), hue 64.3° vs 64.9° — 26° from the
stone's 90° (D67) — mean per-pixel ΔE 3.16, p99 7.47: a rust wash drifting between ochre `#a98a5b` and red-brown
`#bb816c` at one lightness, its value held tighter than the stone's (iron sits 4.7 L\* above the dots' floor, the stone
12). Seamless: wrapped ΣΔLab 2.79 left↔right and 2.66 top↔bottom against interior means 2.72 / 2.68 and maxima 2.90 /
2.88, and no cross in the 1024-offset view at three times the contrast. `check.py` on a scratch copy with the tile's
percentiles sampled in as a ground (art trap 16): **0 of 26 marks and 0 of 41 outlined things fail**; the iron's p1
(L\* 59.1) becomes the darkest common ground, so the 3 : 1 line moves from L\* 29.3 to 28.7 and every mark still clears
it. The dots read **3.56 : 1 against the tile's mean, 3.12 : 1 at its darkest 1 %, 2.98 : 1 at 0.1 %** (3.53 : 1
against flat iron) — the woods' margin (2.96 at p0.1), reported and not failed, as the woods' was.

## 4. How the shader draws it

- `terrain_ground` (`terrain_lab.gdshaderinc`) samples each painted kind at `tile / 64` instead of using a flat
  `kind_*` colour. The tiles live in `textures/ground/`, imported **lossless, mipmapped, repeating**, with their `.import`
  sidecars committed (`seasonal-grass.md §3`; HANDOFF's art trap 2).
- **Anti-tiling** reuses the grass's own functions (`grass_antitile.gdshaderinc`: `at_tri_grid`, `at_turn`, `at_shift`),
  behind its own switch, so Joe judges a plain repeat against hex tiling. ⚠️ The grass's linear blend ghosted (D632). A full-
  colour wash may blend differently from a density mask, so the blend is a choice in the lab, not a given.
- **Behind a switch, off when the lab opens.** With it off, the lab draws byte-for-byte what it draws today, which is the
  rule every terrain piece has kept.
- **Built (A14, piece 1):** `terrain_wood` in `terrain_lab.gdshaderinc` samples `ground_wood` at `tile / ground_tex_tiles`
  with `textureGrad`, so the mip level is right under a hexagon's turn; `terrain_ground` takes the wood it returns in place of
  `kind_wood`, and with the switch off is handed `kind_wood` itself, so the off path is the old maths exactly. **Hex** reuses
  `at_tri_grid`, `at_turn` and `at_shift` with the grass's *Blend sharpness* (`hex_sharpness`), two hexagons a repeat as the
  grass has. ⚠️ **Its blend is linear:** the grass's Height blend keys on a stamp's density, which a colour wash has not got;
  if the lab shows ghosting at the hexagon borders a wash-specific blend is the next option, not a given. **Young wood** is the
  spec's ratio (`kind_young / kind_wood` per channel, `#b3b98d / #9aa47a`) behind *Young wood preview*, which draws every wood
  that way so the derived colour can be judged before a kind exists to carry it. **The loader** reads the tile on first
  switching on (about 21 MB of VRAM with its mipmaps), as the fake map is baked on first switching on. ⚠️ **"Repeating" is
  not an import flag in Godot 4** — the `.import` holds lossless and mipmapped; the repeat is the sampler's own
  `repeat_enable` hint in the shader, which the probe's `woods tile import` line does not see and the renders do.
- **Built (A17, piece 2):** the sampling is one function, `terrain_painted(sampler, …)`, which `terrain_wood`,
  `terrain_stone` and `terrain_water` call with `ground_wood`, `ground_stone` and `ground_water` — same `textureGrad`,
  same Hex path, same linear blend. **The shallows** are `terrain_shallows(water) = water × (kind_shallows ÷ kind_water)`
  per channel (`#9cbccb ÷ #6f97ad`), and `terrain_ground` mixes them with the deep water by the **same**
  `smoothstep(0.45, 0.95, water_weight)` as before, so the bank and the fill still cannot disagree (D338). With the
  switch off `terrain_ground` is handed `kind_wood`, `kind_stone`, `kind_water` and `kind_shallows` themselves — the
  shallows are *not* computed as water × ratio there, because that is not `kind_shallows` to the last bit. **One switch**
  for the three kinds, as §5 allows. **A kind not drawn at a pixel is not sampled** (its share is exactly 0 at the hard
  contour): the derivatives are taken where every pixel runs and `textureGrad` is legal inside the branch, so the drawn
  colour is the same and the cost is paid only where a kind shows. The loader is `LoadTheTiles` (`TilesLoaded`):
  the three tiles on first switching on, ~21 MB of VRAM each.
- **Built (A20, piece 3):** `terrain_iron` calls `terrain_painted` with `ground_iron`, as the other three. ⭐ **The fifth
  kind rides on the seam:** the fake map's B is now *the seam's* weight (stone or iron — the spec has called both seams
  from the start), and a second one-channel map, `ground_map_iron` (`GrassLabGround.BakeIron`), is white where an iron
  patch is nearer than any stone one and black elsewhere. So splat, bleed and the water's contour stay four-way, exactly
  as approved, and the kind flips only midway between seams, out on the grass. The shader asks it only where a seam is
  drawn (`kinds.z > 0`), as a pick (`≥ 0.5`), never a blend: `seam = iron ? kind_iron : kind_stone`, and with the
  textures on `iron ? terrain_iron(…) : terrain_stone(…)` — only one of the two tiles is ever read at a pixel. Where the
  map is black, or before it is baked (`hint_default_black`), the path is the stone's exactly. The map has **one iron
  seam**, at (−9.5, −8) with radii 3.2 × 2.2 tiles, on the grass between the village and the western wood, on screen at
  the opening zoom and about seven tiles from the nearest stone. ⚠️ **An iron seam touching a stone one would meet at a
  hard line, not a splatted edge** — the probe's `fake map` line fails any seam texel with both kinds within a texel of
  it (red-checked: an iron laid against the east stone is 235 such texels); the valley's own encoding is the wiring's.
  Iron's flat colour is `VillageMap`'s `IronDeposit` (`#b9906f`) as `kind_iron`; the loader reads four tiles.
- **Cost:** a texture read per kind drawn, three with hex tiling, inside A11's bar (1 ms a frame at 4K for the whole
  stack). Measured each piece. **Piece 1 (A14; RTX 3070, 4K, one run, against *coverage map, no grain* in the same run,
  `lab_cost.gd`'s method):** the woods texture **+0.13 ms** at the widest zoom and **+0.04 ms** at gameplay; **hex +0.67 /
  +0.68 ms**; the young preview +0.04 / +0.06 (the run's noise). With A5's stack at +0.64–0.80 ms, the plain texture stays
  inside the bar; **hex would take the whole stack to about +1.3–1.5 ms, over it** — Joe's to weigh against the repeat.
  **Piece 2 (A17; same machine and method, two runs, the low-clock rounds agreeing):** all three textures **+0.04 ms**
  at both zooms; **hex +0.20 / +0.21 ms**; young +0.04 (noise). Cheaper than piece 1's wood alone, because a kind is now
  sampled only where it is drawn, and the fake map is mostly grass. ⚠️ **So the cost is per screenful of painted kind,
  not per screen:** a view that is all woods, stone or water pays about piece 1's per-pixel price (hex ~+0.67 ms), since
  each pixel still samples one kind (two at a border). The valley's real share of those kinds is measured when it is wired.
  **Piece 3 (A20; same machine and method, A/B of HEAD's shader files against the new ones, alternated four runs in one
  sitting):** **no difference the runs can see.** The plain coverage map's own baseline wandered 1.63–1.80 ms at 4K
  between runs, more than any step between the two shaders; inside a run the textures add +0.01–0.05 ms and hex
  +0.12–0.18 ms for both alike (one round at the widest zoom read +0.31 / +0.33 off a low baseline). Iron costs one R8
  fetch on seam pixels only — the iron tile replaces the stone's read there, never adds to it.

## 5. The pieces, each its own commit, each ending at Joe's eyes

| # | piece | starts when | controls |
|---|---|---|---|
| 1 | **Woods**: the loader, `ground_woods.png` drawn as the woods floor, young wood derived — 🔨 **built (A14), Joe's eyes pending** | ✅ arrived and approved (A13–A14) | *Ground textures* switch · *Texture tiles per repeat* (start 64) · *Texture anti-tiling* (Off, Hex) · *Young wood preview* (added A14: the derived colour needs a way to be seen) |
| 2 | **Stone and water**, shallows derived from the depth ramp — 🎨 **tiles painted (A16)**, 🔨 **built in the lab (A17)**, ⭐ **approved (A18)** | after 1 is approved, and the tiles arrive | the same — **one switch for all three kinds** (A17); per kind only if he wants them apart |
| 3 | **Iron**: the fake coverage map gains a fifth kind (it held four: grass, wood, stone, water) — 🎨 **tile painted** and 🔨 **built in the lab (A20)**, ⭐ **approved and merged (A21)** | the tile arrives | — the same one switch, now for all four kinds |

Each piece: probe lines for every new control and for the import settings, the approved renders byte-identical with the
switch off, the §3 numbers, a red-check of every new guard, and the cost against A11.

## 6. Open, and Joe's

- Woods in seasons (§1): later tiles, if he wants them.
- Worked stone and iron (a quarry's floor is `VillageMap`'s): waits for house tiers.
- Whether the minimap and the far-view bake take a tile's mean or stay on the palette: decided when the valley is wired.
