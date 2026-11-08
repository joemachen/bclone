# Spec: Ground-kind textures — Joe's watercolour, one seamless tile per kind

**Status:** ✅ **SPECCED (A12); `ground_woods.png` PAINTED AND MEASURED (A13) AND APPROVED BY JOE (A14); PIECE 1 BUILT IN THE LAB (A14, 2026-11-07): the loader, the *Ground textures* switch, *Texture tiles per repeat*, *Texture anti-tiling* (Off, Hex), *Young wood preview*, every switch off at opening and the approved renders byte-identical. ⭐ MERGED INTO `main` AT JOE'S WORD (A15, 2026-11-08). 🎨 `ground_stone.png` AND `ground_water.png` PAINTED AND MEASURED (A16, 2026-11-08), NOT YET SEEN BY JOE; piece 2's lab half not built. Iron not painted.**
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
| **iron seam** (`#b9906f`, L\* 62.9) | — |
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
- **Cost:** a texture read per kind drawn, three with hex tiling, inside A11's bar (1 ms a frame at 4K for the whole
  stack). Measured each piece. **Piece 1 (A14; RTX 3070, 4K, one run, against *coverage map, no grain* in the same run,
  `lab_cost.gd`'s method):** the woods texture **+0.13 ms** at the widest zoom and **+0.04 ms** at gameplay; **hex +0.67 /
  +0.68 ms**; the young preview +0.04 / +0.06 (the run's noise). With A5's stack at +0.64–0.80 ms, the plain texture stays
  inside the bar; **hex would take the whole stack to about +1.3–1.5 ms, over it** — Joe's to weigh against the repeat.

## 5. The pieces, each its own commit, each ending at Joe's eyes

| # | piece | starts when | controls |
|---|---|---|---|
| 1 | **Woods**: the loader, `ground_woods.png` drawn as the woods floor, young wood derived — 🔨 **built (A14), Joe's eyes pending** | ✅ arrived and approved (A13–A14) | *Ground textures* switch · *Texture tiles per repeat* (start 64) · *Texture anti-tiling* (Off, Hex) · *Young wood preview* (added A14: the derived colour needs a way to be seen) |
| 2 | **Stone and water**, shallows derived from the depth ramp | after 1 is approved, and the tiles arrive | the same, per kind if he wants them apart |
| 3 | **Iron**: the fake coverage map gains a fifth kind (it holds four today: grass, wood, stone, water) | the tile arrives | — |

Each piece: probe lines for every new control and for the import settings, the approved renders byte-identical with the
switch off, the §3 numbers, a red-check of every new guard, and the cost against A11.

## 6. Open, and Joe's

- Woods in seasons (§1): later tiles, if he wants them.
- Worked stone and iron (a quarry's floor is `VillageMap`'s): waits for house tiers.
- Whether the minimap and the far-view bake take a tile's mean or stay on the palette: decided when the valley is wired.
