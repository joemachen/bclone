# Spec: Ground-kind textures — Joe's watercolour, one seamless tile per kind

**Status:** 🎨 **SPECCED (A12); `ground_woods.png` PAINTED AND MEASURED (A13, 2026-11-07) — inside every §3 limit, unmerged, waiting for Joe's eye; THE LOADER (§5 PIECE 1) NOT BUILT. 🔨 on `art`. Woods first.**
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

## 4. How the shader draws it

- `terrain_ground` (`terrain_lab.gdshaderinc`) samples each painted kind at `tile / 64` instead of using a flat
  `kind_*` colour. The tiles live in `textures/ground/`, imported **lossless, mipmapped, repeating**, with their `.import`
  sidecars committed (`seasonal-grass.md §3`; HANDOFF's art trap 2).
- **Anti-tiling** reuses the grass's own functions (`grass_antitile.gdshaderinc`: `at_tri_grid`, `at_turn`, `at_shift`),
  behind its own switch, so Joe judges a plain repeat against hex tiling. ⚠️ The grass's linear blend ghosted (D632). A full-
  colour wash may blend differently from a density mask, so the blend is a choice in the lab, not a given.
- **Behind a switch, off when the lab opens.** With it off, the lab draws byte-for-byte what it draws today, which is the
  rule every terrain piece has kept.
- **Cost:** a texture read per kind drawn, three with hex tiling, inside A11's bar (1 ms a frame at 4K for the whole
  stack). Measured each piece.

## 5. The pieces, each its own commit, each ending at Joe's eyes

| # | piece | starts when | controls |
|---|---|---|---|
| 1 | **Woods**: the loader, `ground_woods.png` drawn as the woods floor, young wood derived | Joe's `ground_woods.png` arrives | *Ground textures* switch · *Texture tiles per repeat* (start 64) · *Texture anti-tiling* (Off, Hex) |
| 2 | **Stone and water**, shallows derived from the depth ramp | after 1 is approved, and the tiles arrive | the same, per kind if he wants them apart |
| 3 | **Iron**: the fake coverage map gains a fifth kind (it holds four today: grass, wood, stone, water) | the tile arrives | — |

Each piece: probe lines for every new control and for the import settings, the approved renders byte-identical with the
switch off, the §3 numbers, a red-check of every new guard, and the cost against A11.

## 6. Open, and Joe's

- Woods in seasons (§1): later tiles, if he wants them.
- Worked stone and iron (a quarry's floor is `VillageMap`'s): waits for house tiers.
- Whether the minimap and the far-view bake take a tile's mean or stay on the palette: decided when the valley is wired.
