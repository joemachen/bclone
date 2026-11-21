# Inventory: every piece of art the game still needs

> **Status: an audit, read off `main` at 880db1e (2026-10-30, D653); B1 resolved (D654).** Joe: *"Build an art asset inventory for Kith so I
> know everything that still needs real art."* Not a spec and not a plan — it invents nothing and decides nothing, and
> **it does not reopen the art.** ~~Everything here stays under HANDOFF's **🅿️ ART: PARKED** (D639) until Joe reopens it.~~ **Reopened in part (D699, 2026-11-06): a second lane, in its own worktree and systems first, works the view-only rows in files the systems lane does not open — the terrain shader in the lab (§3), the theme and font (§11), the app icon and splash (§12). The building, glyph, goods, tree and world-draw rows (§4–§10) wait for house tiers. The lane rule and the no-go table are HANDOFF's 🅿️ section.**
> Companion to `content-inventory.md` (what the village *has*); this is what the village *looks like* and what stands in.
> ⚠️ **Dated the day it was read.** A building, good or animal added after D653 is not in these tables.
>
> ⭐ **The art lane's order (Joe, 2026-11-15, A90): *"i want to work on villagers for the next art piece. And then the UI"*** —
> the villagers (`villager-art.md`, specced and mocked A90), then the parchment-and-wood interface (§11). **And a running list
> (his: *"take note of the systems work being done and all of the buildings and art pieces that will have to be created for
> that"*) — §15, kept current from each systems spec's art section (D774's rule).**
>
> **Key:** ✅ finished / approved · 🟨 placeholder, drawn in code · ⬜ not drawn at all · 💭 planned, nothing in the sim yet.
> **Blocked by** names a call in §1 (B1…B14).

## 0. The one-paragraph answer

**There is no art in the game.** Every world thing and every icon is a flat-colour polygon, disc or line drawn in code on
parchment with an ink edge; the ground is one CPU-baked texture of flat colours with value noise
(`ValleyTexture.cs`). The image files in the repo are the grass masks and season atlas (`textures/grass/`), used only by
the separate Grass Lab scene, and the reference JPGs under `art/`. **The one approved look is the grass**
(`looks/grass_approved.tres`, D632–D633), and it is not in the valley yet. The world *palette* is approved for
legibility (D637–D639) — approved as colour, not as art. The UI is Godot's default theme and built-in font, and the app
icon is the stock Godot logo.

## 1. The calls that block the style

> ⭐ **The concept sheets are a STYLE source only, not a projection (A80, 2026-11-14; Joe: *"Style only, top-down"*).** Thirty
> images in `art/concept art/` (committed on `art` at his *"yes"*, A81): twelve numbered sheets (visual target, shape
> language, building design sheet, tier progression, material studies, vegetation and terrain kit, seasonal colour script, path
> and road tiers, villager lineup, props, UI style frame, readability test) and eighteen single buildings. Every building in them
> is drawn at an angle; **A31 stands — the valley, its trees and its buildings are drawn straight down.** What is taken from them:
> the timber framing and the roof and wall colours (sheet 03's palette: *terra umbra*, *tectum rubrum*, *lignum quercus*,
> *ferrum veteris*, *paries dealbatus*), the ink line, and later the parchment. Sheet 08 (meadow, trampled trail, worn footpath,
> rutted cart track, cobbled road) **bears on the path look (Joe: *"yes"*, A81)** — set beside the A/B/C mocks and the painted
> paths he picked (B, A72–A74), perhaps as a progression the wear grades step through; the pick is his, with the art lane.

These are what decide *what kind* of art each row needs. Most rows below cannot be drawn finally until B1–B4 are answered.

| # | The call | Where it stands | What it blocks |
|---|---|---|---|
| **B1** | ✅ **RESOLVED (D654): the game ships image assets** — textures, sprites, icons, backdrops. B1 no longer blocks any row below. *As found:* **Is the game allowed image assets at all?** The view cited *"the project ships no image assets (D26)"* in five places (`GoodsPalette.cs:22`, `TradeGlyph.cs:10`, `BuildingGlyph.cs:11`, `ToolGlyph.cs:56`, `MomentGlyph.cs:10`) and `build-bar.md:37` calls it *"⛔ D26 — no image assets"*. ⚠️ **But D26 in `DESIGN.md §7` is "the world has bounds and the map has a camera"** — it says nothing about images. The rule was real in the code and had no decision behind it in the log; D654 voids it and every citation now points there. | ✅ Joe, D654 | Nothing now. It blocked every sprite, texture, icon, portrait and logo below. |
| **B2** | **D68's destination — *Foundation* (painted 3D) or a 2D drawn map** (`world-legibility.md §7` call 5; D68 at `DESIGN.md` §7). Parchment and the `art/` references point at 2D drawn. **✅ ANSWERED (D725, 2026-11-11), Joe: *"2d map only."* — a 2D drawn map.** | ✅ 2D drawn map (D725) | Everything in the world: projection, buildings, villagers, animals. |
| **B3** | **Phases 7–8: a 3D front end (`src/Kith.Game3D`) on top of the same sim**, the 2D view kept as the debug/probe view (`DESIGN.md §4`, "Phases 7 and 8"). If it stands, finished 2D world art is the debug view's. **⛔ WITHDRAWN (D725): there is no 3D front end; the 2D map is the game, and its art is the shipped art.** | ✅ Withdrawn (D725) | Whether 2D world art is the shipped look or a stopgap. Tied to B2. |
| **B4** | **Projection and turning.** The view is flat top-down (D27) and footprints turn freely — homes to any 1/64 turn (D411), any building since gridless, one piece at any angle (D650). The `art/` references are **fixed-orientation ¾ sprites** (origin 0.5, 1.0; `art/art-direction-hand-drawn-orthographic-asset-pipeline.jpg`). Nothing reconciles the two. Store crates already snap to a quarter turn (D423), which is a precedent. **✅ ANSWERED (A31, 2026-11-10), Joe: *"we shouldnt draw at an angle. buildings must rotate."* — every sprite is drawn straight down; `art/`'s ¾ sheet is the style, not the projection.** ⚠️ Still open: a turned sprite turns its painted light with it — buildings painted unlit and lit by the game, or the light turned; the trees dodge it by mirroring along the light's diagonal (`tree-sprites.md §3`). | ✅ Straight down (A31); the light, open | Every building, villager, animal and object sprite. A ¾ sprite cannot turn to 1/64. |
| **B5** | **The tree look** — ink-outlined canopies over an in-band green, or mid-tone washes without lines (`world-legibility.md §7` call 4). Canopies sit in the ground's band as an interim (D637). **A28: ink-outlined crowns, the ground not outlined (Joe); A29: painted tree sprites specced, `specs/tree-sprites.md`; A30–A31: Joe's four calls answered — straight down (crowns seen from above, mirrored along the light, never turned), one leafy kind, he paints the atlas, 1–2 trees a tile.** | ⏸️ waiting on Joe's atlas, `tree_sprites.png` (`tree-sprites.md §3`); the four calls answered (A30–A31) | Canopies, saplings, the woods ground, berries and game drawn in the woods. |
| **B6** | **The terrain shader's open questions** (`terrain-shader.md §2–4`, *specced A1, being built in the lab*): snow's blend (lerp or lighten — Multiply cannot reach a colour lighter than parchment), paper grain anchored to the land or the window, ground kinds as flat fills, washes or textures, splat blending and edge bleed. | 🅿️ Parked | All terrain rows; the grass going into the valley; snow. |
| **B7** | **UI chrome: slate or parchment.** D637 call 3 keeps the UI's own chrome slate (only pictures of world things sit on a parchment chip); the `art/` references show parchment panels and wax seals. **✍️ Reopened by the UI piece (A96, `specs/ui-art.md`): three looks mocked on his saves — A parchment leaves (recommended), B parchment in oak, C leather and gilt (dark kept).** | ✅ **Not slate (A97, Joe: *"confirmed i dont want the UI to stay slate"*): parchment scrolls in the manner of manuscripts, with watercolour of their own, rolled up to close** — `ui-art.md §8`; was slate (D637) | Panels, windows, buttons, theme, font. |
| **B8** | **Tiers, which are content not yet decided:** house tiers (cabin → cottage → manor; `DESIGN.md §5`, D206), barn tiers (lean-to → timber → stone; `TECH-EXAMPLE.md`), the mill as a watermill by water and a windmill elsewhere (`buildings-plan.md §4.3`). | 💭 No rows | Each tier needs its own art. |
| **B9** | **The condition look** — *"steps rather than cracks or smoke"* (`building-condition.md` line 101) was Claude's call; Joe approved what he saw (D560) but never chose against cracks. | ✅ Played | Whether damage art is a tint or drawn wear. |
| **B10** | **Which animals.** Kept: cattle only, one kind first (`livestock.md §0`). Wild: no species, *"no models is fine for now — but we're going to have to add models in eventually"* (`hunting.md §9`). Fish subtypes deferred (`fishing.md §10.1`). `TECH-EXAMPLE.md` names 25 species. | Open | The whole animal list. |
| **B11** | **Fire, blight and drought** must be *telegraphed, legible on the map, and answerable* (`DESIGN.md §4`, Phase 5) and have no spec, so no visual state is designed. | 💭 Phase 5 | Burning/burnt buildings, blighted fields, dry ground. |
| **B12** | **A villager has no picture, on purpose** (D431, `the-cards.md §2a`). | ✅ Decided | Villager portraits — none needed unless reopened. |
| **B13** | **Clothing** ~~is specced and parked (D534, `clothing.md`); no look is specified.~~ ✅ **The coat is built (D659–D667) and invisible on the map; its look is a moss mantle in `villager-art.md` (A90)** — the wool coat (§15, item 8) wants a second. | ✍️ A90, ⏸️ Joe's pick | Villager variants by dress. |
| **B14** | **Goods seen arriving and leaving stores** — Joe, D418: *"visual goods being added to / removed from each building"*; the crates are the placeholder (`storage-and-distribution.md`, D419). | Wanted, art pass | Store stock art. |

## 2. Terrain

| Item | Stands in now | Variants needed | Blocked by | State |
|---|---|---|---|---|
| **Grass / open ground** | Flat parchment `#e6d6b0` with a ±3.5% bloom in the bake (`VillageMap.cs:71`; `ValleyTexture.cs:608`) | 4 seasons, blended (20 days held, 10 blending — `seasonal-grass.md §6.4`) | B6 — the shader to put it in the valley | ✅ **Look approved** (D632–D633, `looks/grass_approved.tres`, refs in `art/grass/`) — **lab only, not in the valley** |
| Water and shallows | `#6f97ad` shading to `#9cbccb` by edge confidence — the only shoreline (`VillageMap.cs:123`, `:135`) | Seasonal tint? River never freezes (`fishing.md §10.3`). **⭐ A47 approved; 🔨 A45 built, A46 at the game's speed (✍️ A44): drawn, not filled, with the tick on — echo lines, a darker channel, ripples drifting downstream while time runs (`river-drawn.md`)** | B6 | 🟨 |
| Woods ground / young wood | `#9aa47a` / `#b3b98d` with two-octave foliage noise (`ValleyTexture.cs`) | Seasons (no spec asks), far-view bake | B5, B6 | 🟨 interim (D637) |
| Stone seam / iron seam | `#b4ab99` / `#b9906f` with grain noise | Unworked / worked | B6 | 🟨 |
| Quarry and mine floor | Traced fills `#a39b8e` / `#bf957c` (`VillageMap.Meshes.cs:486`, `:495`) | Grows as worked | B6 | 🟨 |
| Ploughed / sown / ripe field | Traced fills `#b39874` / `#a8b07a` / `#d6b55c`, drawn per tile, not baked (`VillageMap.cs:208–219`) | See §4 (crops) | B6 | 🟨 ⭐ **A62 approved;** 🔨 **A61 built (✍️ A59, `painted-crops.md`)** — wheat's young, growing and ripe and the shared ploughed, stubble and snow, seamless, world-anchored, rows east–west; a folder a crop; for Joe's eyes |
| Valley edge and beyond | `#c4b48f` beyond, a 2px `#8c7a5c` edge (`VillageMap.cs:77`, `:4003`) | — | B6 | 🟨 |
| Optional grid | 1px `#d2c29c` lines (`VillageMap.cs:4016`) | — | — | 🟨 debug aid |
| **Snow** | ⬜ nothing — no season is drawn anywhere in the valley | Snow cover, melt | B6 (needs a non-Multiply blend) | 🟨 lab only: its own layer, Lerp / Screen / Lighten, behind a switch (A4); ⭐ approved at Joe's tuning (A9): Lerp, `#f8f6f1`, patchiness 0.03, patch 3 tiles, the amount the season's; the valley waits for house tiers 🔨 **A76: snowfalls you can see (`snowfall.md`) — three or four a year, flakes on their own see-through layer; the ground a frost then drifts read off the land; for Joe's eyes** 🔨 **A77, at his first look: the flakes fall into the valley (their shadows closing in) as six designs of ink-and-wash crystal and fine specks; the snow lies by the land's shape — the heights first, a painted edge pooled along the contours, the banks shedding it, the slopes' relief cool on it** |
| Watercolour textures for stone, clay, dirt, the rest | ⬜ — flat fills in the band | woods, stone, iron, water (young wood and shallows derived) | B6 | ✍️ specced, `ground-textures.md` (A12): full colour, one seamless 2048² tile a kind, woods first; nothing painted. Fields and quarry floors wait for `VillageMap`. ⚠️ *Clay has no terrain kind in the sim.* |
| Paving (dirt → gravel → cobble) | ⬜ | 3 grades | B6 | 💭 `DESIGN.md §2.6`, no sim. ⚠️ The cobbles are kept for this (A72): Joe's concept cobbles the village's lanes, and cobbling packed earth would claim stone nobody laid (`painted-paths.md §2`); A72's mock C is the first sketch of the cobble grade |
| Dry ground (drought) | ⬜ | — | B11 | 💭 |

## 3. Shader and tile support

| Item | Stands in now | Needs | Blocked by | State |
|---|---|---|---|---|
| **The valley's terrain shader** | ⬜ — the valley is a `DrawTextureRect` of the CPU bake (`VillageMap.cs:5224`) | One canvas_item shader with a coverage channel (D628 option A) | B6 | 🟨 `terrain-shader.md` specced (A1); all four pieces built in the lab (A2–A5); splat, bleed, grain and snow approved (A7–A9), the map a lab fake; the whole stack fits the GPU bar, 1 ms at 4K (A11); §4 says which piece stands |
| Grass shader | `grass_lab.gdshader` + season and anti-tile includes, masks v02–v06, `grass_seasons.png` (256×16) | Integrated into the valley | B6 | ✅ look / ⬜ in game |
| Paper grain | `paper_grain.gdshader`, procedural, **screen-anchored** placeholder, strength 0.15 a placeholder; the approved look's is 0.12 (A10) | World-anchored grain | B6 | 🟨 lab only; world-anchored built behind a switch (A3); ⭐ approved at 0.065 tiles, under the village (A9) |
| Splat blending between ground kinds | The bake's domain-warp and 3×3 kernel give smooth contours (`ValleyTexture.cs:468–588`); no blending | Blend + edge bleed | B6 | 🟨 lab only: splat and bleed behind switches (A5), the river's edge held hard; ⭐ approved by Joe at sharpness 5.5, reach 0.33, scale 0.30 (A6–A7); the valley waits for house tiers |
| Snow blend | ⬜ | lerp / lighten mode | B6 | 🟨 all three built in the lab (A4), ⭐ Lerp, approved (A9) |
| Debug readout backing | ⬜ — frame/draw lines are illegible on parchment (`Main.cs:8275`) | A dark backing | — | ⬜ 🅿️ parked list |

## 4. Trees and vegetation

| Item | Stands in now | Variants needed | Blocked by | State |
|---|---|---|---|---|
| **Mature tree** | 3–4 discs per forest tile, `#7d8c5c`, r 0.20, per-disc shade; only above ~24 px a tile, the bake below (`VillageMap.Meshes.cs:418–427`; `VillageMap.cs:4947`, `:5118`) | Seasons (leaf colour, bare winter) — `tree-sprites.md` piece 4; felled/stump. **🔨 A35: with the watercolour tick on, Joe's painted atlas (`textures/trees/tree_sprites.png`, 8 crowns from above), 1–2 a tile; A36: + 4 conifers, 4 birches, 4 willows chosen by place, over the whole village, at every zoom, a *draw the trees* tick; A41: through the year — an autumn set, a winter set (deciduous bare, conifers snowy), crossfaded over each turn** | B5, B4 | 🟨 interim (tick off) / 🔨 painted (tick on, piece 1) |
| Sapling | 2 smaller discs, `#98a56f`, r 0.13 (`Meshes.cs:420`); with the tick on (A35) 2 painted trees at 0.55 until the young atlas | Growth to mature; ~~`tree_sprites_young.png`~~ young cells in each season's atlas (piece 2) | B5 | 🟨 ⭐ **A52 approved;** 🔨 **A51 built (✍️ A50, `tree-sprites.md §10`): three painted saplings a young tile, each place's kind, through the year — for Joe's eyes** |
| Tree species | ⬜ — the sim has no species, only Forest and Sapling | — | B10-ish (content) | 💭 |
| Berry bush (forage) | 3 discs `#840c26` on 1 woodland tile in 16, toggleable (`Meshes.cs:362–382`) | Picked / regrowing, seasons | B5 | 🟨 |
| Wheat field — ploughed | Field fill + 3 furrow lines at 0.8× | — | B6 | 🟨 |
| Wheat field — sown | Sown fill + 5–7 short shoots | Growth through the season | B6 | 🟨 |
| Wheat field — ripe | Ripe fill + 10–12 stalks in the wheat goods colour (`VillageMap.cs:6423–6514`) | Reaped stubble, winter rot (`crops-and-orchards.md §5–5.1`) | B6 | 🟨 |
| A trail worn through a field | Extra trail mesh (`VillageMap.cs:6312`) | — | — | 🟨 |
| Other crops | ⬜ — wheat is the only crop | 6 exotic crops in `TECH-EXAMPLE.md` | — | 💭 |
| Orchards | ⬜ | Growth, blossom, fruit | — | 💭 deferred (`crops-and-orchards.md §8`) |
| Blighted field | ⬜ | — | B11 | 💭 |

## 5. Buildings

> ⭐ **A86 (2026-11-15): with the watercolour on, every building has a yard** of its own things (stock only while held), homes are dressed on their plots with fences by tier, and the roofs are drawn in pen and wash (`specs/building-yards.md`).

> ⭐ **A85 (2026-11-15): with the watercolour on, every finished building is painted** — its roof seen straight down, by material, lit by its facing, with a shadow and a door step; the market, stockpile, mill and well drawn by shape (`specs/building-roofs.md`). The rows below describe the tick off, which is unchanged.

**Every building today is the same thing**: a rotated footprint quad at 0.8× its extent, filled with its colour, a 2px
ink `#2a2018` edge (`VillageMap.cs:2217–2243`). There are **no per-kind shapes in the world** — kind is colour only.
Sizes: 3×2 town hall, barn · 2×2 granary, warehouse, market, farmhouse, library, mill · 2×1 house, huts, smithy, quarry,
mine, bakery, trading post · 1×1 forager's hut, forester's hut, fishing hut, well, stockpile.

### 5.1 States every building shares

| State | Stands in now | Needs | Blocked by | State |
|---|---|---|---|---|
| **Construction site** | Outline + a fill rising from one edge with progress, `#8f9aa8` / edge `#38424e`; harvest orange while the ground still needs clearing (`VillageMap.cs:283–292`, `:6739–6807`) | Staged art. `art/…asset-pipeline.jpg` shows **three stages: blueprint ghost, under construction, completed** — no spec asks for stages. **A53:** with the watercolour's canopy over the village, a site's outline also comes through the trees, dashed, in its own colours (`tree-sprites.md §2`) | B1, B4 | 🟨 |
| Placement ghost | Footprint quad in fine `#004c1b` / warned `#523e01` / refused `#7e1b1d` (`VillageMap.cs:3082–3260`) | — | — | 🟨 (functional; may stay drawn) |
| Demolition | `#b5714a` / `#6a310d`, the fill drains (`VillageMap.cs:283–292`) | Coming-down stages | B4 | 🟨 |
| **Condition: sound / worn / failing** | Three steps darker-and-duller (`Weathered`, `VillageMap.cs:87–102`) — every kind but the town hall and the stone bridge | Drawn wear or a tint | B9 | ✅ played (D560) as placeholder |
| **Ruin** | The third step, edges **dashed** (`VillageMap.cs:2236`); a ruin stays until mended or pulled down (`building-condition.md §4.5`) | Ruin art per kind | B4, B9 | ✅ played as placeholder |
| Abandoned home | 25% alpha, faded edge (`VillageMap.cs:7111–7151`) | — | B4 | 🟨 |
| Burning / burnt | ⬜ | — | B11 | 💭 |
| Snow on roofs | ⬜ — **specced nowhere**; `DESIGN.md §4` lists only "winter shaders" | Winter variant per kind | B6, B2 | ⬜ gap |
| **Door** | A darker notch `#4a3421` on the lane edge, homes only (`VillageMap.cs:111`, `:7148`) | Every building's door — the sim has doors on every footprint (D626); the **doors view (commit 5) is not built** (`doors.md`) | B4 | 🟨 homes / ⬜ the rest |
| Status rings | Idle `#024665`, full store `#5c3a01`, library "no room" (`VillageMap.cs:6836`, `:3542`, `:3352`) | — | — | 🟨 (UI-like; may stay drawn) |

### 5.2 Per kind

| Building | World fill now | Bar glyph (`BuildingGlyph.cs`) | Extra variants needed | State |
|---|---|---|---|---|
| House | `#b98a52` + door notch | ✅ glyph (but the brush places it — no bar button) | **Tiers** (B8); turned to any 1/64 (B4) | 🟨 |
| Granary | `#d8c56a` + stock crates | ✅ | Stock seen (B14) | 🟨 |
| Warehouse | `#8a7a63` + crates | ✅ | Stock seen (B14) | 🟨 |
| Market | `MarketColour` `#c98f4a` + crates | ✅ | Stalls, stock (B14) | 🟨 |
| Stockpile | ⚠️ draws in **MarketColour** — no colour of its own (`VillageMap.cs:262`) | ✅ | Stock heaps — ✍️ specced and mocked, `stock-piles.md` (A158) | 🟨 |
| Founders' cart | ✅ `HandcartColour` `#6b8a94` (Slice H, `handcart.md §8`) — a store quad in the cart colour, shared with the handcart; it borrowed `MarketColour` until then | — | A cart | 🟨 |
| Builder's hut | ⚠️ **falls through to MarketColour** (`VillageMap.cs:8203`, `_ => MarketColour`) | ✅ | — | 🟨 |
| Woodcutter's hut | `#9a6b3f` | ✅ | Log pile? | 🟨 |
| Forager's hut | `#5aa04a` (the berry colour) | ✅ | — | 🟨 |
| Forester's hut | `#2f6b3a` (the tree colour) | ✅ | — | 🟨 |
| Farmhouse | ⚠️ **falls through to MarketColour** | ✅ | — | 🟨 |
| Fishing hut | `#6badc7` (the river, lightened) | ✅ | Drying racks, a boat? | 🟨 |
| Hunter's lodge | ⚠️ **falls through to MarketColour** | ✅ | Hides drying? | 🟨 |
| Smithy | Its trade colour | ✅ | Forge glow / smoke | 🟨 |
| Quarry | Trade colour + worked floor | ✅ | The face grows | 🟨 |
| Iron mine | Trade colour + floor + seam palisade | ✅ | Mine mouth | 🟨 |
| Mill | Trade colour | ✅ | **Watermill vs windmill** (B8) | 🟨 |
| Bakery | Trade colour | ✅ | Oven smoke | 🟨 |
| Workshop (D678) | Trade colour (fresh-sawn oak, `TradeGlyph.ColourOf(Carpenter)`) | ✅ a hut with a wheel leaning on its wall | A saw-pit, shavings; the cart itself is drawn by Slice H (`handcart.md §8`, `VillageMap.HandcartColour`, §6 below) | 🟨 |
| Barn | Trade colour + cattle on its tiles | ✅ | **Tiers** (B8), hay | 🟨 |
| Trading post | Trade colour + the boat docks here | ✅ | Jetty | 🟨 |
| Library | `#6a7fc9` | ✅ | — | 🟨 |
| Town hall | `#b9b2a6`; never weathers | ✅ | — | 🟨 |
| Well | `#8d8577` + a water dot | ✅ | — | 🟨 |
| Wooden bridge | Deck quad `#8a6a48` + plank lines; failing drops every other plank, closed is dashed (`VillageMap.cs:3476–3505`) | ⚠️ **grey square** — no glyph, no colour (`BuildingGlyph.cs:89`, `:327`), and it *is* on the Works tab | Condition (built) | 🟨 / ⬜ glyph |
| Stone bridge | Deck quad `#a8a49b` + joints; an upgrade rebuilt over the wood; never weathers | ⚠️ **grey square** | — | 🟨 / ⬜ glyph |
| Pasture fence | Traced lines with a gate gap (`VillageMap.cs:4247`, `:4476–4600`) | Grey square (brush-placed, no bar button) | — | 🟨 |
| Household plot fence | `#583b1d` lines, 35% alpha before the house stands (`VillageMap.cs:4219`) | — | Gate | 🟨 |
| Seam palisade | `#3f2d1a` lines + post dots, 35% while planned (`VillageMap.cs:4274`) | — | — | 🟨 |
| Longhouse | Retired id (D571) | Grey square | — | — (dead) |

### 5.3 Planned, not in the sim

💭 Cemetery (D35 — graves that grow), chapel/church, tavern, school, root cellar, smokehouse, tannery, kiln, mason's
yard and the rest of T1/T2 (`buildings-plan.md §4`); **the Shining Rock** gift (`DESIGN.md §5`, no look described);
`TECH-EXAMPLE.md`'s 33 with no sim row (sawmill, brick kiln, dairy, brewery, smelter, glassworks, cathedral, the dock
and the rest). Each will need world art, a glyph and its own construction/ruin states.

## 6. Villagers

| Item | Stands in now | Variants needed | Blocked by | State |
|---|---|---|---|---|
| **A villager** | A dot, r max(3px, 0.2 tile), coloured by life stage — adult `#454135`, child `#02465f`, elder `#5d3900` (`VillageMap.DrawVillagers`); crowds fan apart; **a family at home is one dot on its roof** | Life stage ×3; facing/walk; per trade | B2 ✅, B4 ✅ (A31) | ✅ **built (A93, `villager-art.md §8`), merged into `main` (A94): people standing up, in the manner of illuminated manuscripts (mock E, Joe's pick of five, A90–A92) — trade by gown, hat and tool; load, coat, cold, cart; indoors not drawn** |
| Selection ring | `#59238e` | — | — | 🟨 (UI-like) |
| Meal mark | A bowl arc `#543d01` for 0.5 s (`VillageMap.cs:7888`) | — | — | 🟨 |
| Arrival chevron (nomads) | `#4e3f1b`, bobbing and fading (`VillageMap.WalkIn.cs:46`, `:367`) | — | — | 🟨 |
| Visible work | The view draws **no** work state on purpose — you read work from *where* a person stands (`trades-visibly-work.md:28`) | Working poses (chop, dig, reap)? | B4 | 🟨 by design |
| Carried goods | ⬜ — the sim tracks a carried load, nothing draws it | Per good | B4 ✅ | ✍️ A90: carried in front, the good's colour, a shape per good from concept sheet 10 |
| Tools in hand | ⬜ | Stone / iron | B4 ✅ | ✍️ A90: one tool a trade, held out while at work (B, C) |
| Clothing | ⬜ — the coat is in the sim (D667), not on the map | Per garment | B13 | ✍️ A90: the coat a moss mantle; the wool coat later (§15) |
| Cold, going in | ⬜ — `SeekingShelter` looks like any walk | — | — | ✍️ A90: a cold-blue arc over the head |
| Teacher's colour | ⚠️ none — `TradeGlyph.ColourOf` falls through to the modder's grey (D752 added the trade) | — | — | 🟨 on the map the painter holds `#4a5ea8` (A93); ⬜ **the systems lane's one line in `TradeGlyph.cs`**, so the Professions panel and the school's glyph agree |
| Portrait | ⬜ by design | — | B12 | ✅ decided: none |

## 7. Animals

| Item | Stands in now | Variants needed | Blocked by | State |
|---|---|---|---|---|
| **Cattle (kept)** | Discs `#e6dcc4`, edge `#5a3424`, drifting on the barn's tiles above 7 px a tile (`VillageMap.cs:7184–7275`) | Calf (the herd is a count, so ages are not drawn); grazing; winter in the barn | B1, B4, B10 | 🟨 |
| Wild game | Brown discs `#573c13` on 1 woodland tile in 24, roaming, toggleable (`VillageMap.cs:7300`, `:7409`) | **No species** — *"models eventually"* (`hunting.md §9`) | B5, B10 | 🟨 |
| Fish | ⬜ | Subtypes deferred | B10 | ⬜ |
| Other livestock / wild species | ⬜ | `TECH-EXAMPLE.md` names 24 more: ox, horse, donkey, dog, pig, sheep, goat, chicken, duck/goose, alpaca, yak, pigeon, bees, silkworms; deer, elk, caribou, bison, ibex, boar, rabbit, beaver, fox, pheasant | B10 | 💭 |
| Animal card / portrait | ⬜ — there is no animal card (`Main.Cards.cs:31–49`); the herd is numbers on the barn's card | — | B10 | ⬜ |

## 8. Small world objects

| Item | Stands in now | Variants needed | Blocked by | State |
|---|---|---|---|---|
| **Heaps / goods on the ground** | Square chips in the goods colour, ink edge, side by side beside a building (`VillageMap.cs:7526–7631`) | Per good; **amount** (no size variant today) — ✍️ piles specced and mocked, `stock-piles.md` (A158) | B1, B4 | 🟨 |
| **Store stock** | A crate per good, height = fill, snapped to a quarter turn (D417–D423) | Goods arriving and leaving (B14) | B14, B4 | 🟨 played (D418) |
| Boulders and ore lumps | Discs `#cdc6b8` / `#5c3424`, 2–4 a tile; a worked face has one fewer, cut to 0.6 (`Meshes.cs:471–498`) | Unworked / worked | B6 | 🟨 ⭐ **A62 approved;** 🔨 **A60 built (✍️ A59, `painted-deposits.md`)** — painted rocks from an atlas, a band a kind named in `looks/node_sprites.tres`, worked drawings, winter snow; for Joe's eyes |
| **Trading boat** | 6-point hull `#7a5636`, triangle sail `#efe6d2` (`VillageMap.Boat.cs:13–15`, `:68–88`) | Arriving / docked / leaving (built) | B4 | 🟨 |
| Fishing boats | ⬜ | — | — | ⬜ |
| Overland caravan, cart road | ⬜ | — | blocked on carts (`trade.md §4.7`) | 💭 |
| Smoke, fire, chimney | ✅ **merged (A139, A140, `indoors-and-smoke.md`)** — watercolour puffs from painted chimneys while firewood burns: homes and the school in winter, the forge, the oven | Forge glow, oven light | B11 | ✅ |
| Tools on the ground | ⬜ | — | — | 💭 |
| **The handcart** (Slice H, `handcart.md §8`) | ✅ placeholder: `HandcartColour` `#6b8a94` three ways — a chip beside the home in the heap chips' geometry with its axle across it when parked (`VillageMap.DrawParkedCarts`), a small rectangle trailing the pushing villager's dot against their heading (`DrawTheCartBehind`), a crate in the warehouse like any good | A drawn cart: wheels, a bed, a load | B1, B2 | 🟨 |
| Doorsteps and the plaza | Drawn by the trail network (D605) | — | B6 | 🟨 |
| Graves | ⬜ | Growing cemetery (D35) | — | 💭 |

## 9. Overlays (probably stay drawn — listed so nothing is forgotten)

Zone fills and dashed edges (residential, harvest, destroy, work ground — `VillageMap.cs:295–369`, `:4643–4840`); the
**paths** — worn `#8c7457` and packed `#8a7056` trail meshes with tapered ends and junction discs (`Meshes.cs:761–883`),
which are world art as much as overlay (B6) — 🔨 **with the watercolour on, painted into the ground as worn dirt tracks
with a pen line down each side (A72, `specs/painted-paths.md`, Joe's *"B, painted track"*), the pen lightened (A73), ⭐ merged (A74); 🔨 **A82: lightly worn ground a trampled trail (concept sheet 08, tier II), packed the footpath (III)**; the mesh stays the
flat map's; the wear debug wash; the brush preview; the selection outline; the
gathering-radius ring; home-to-work route lines. All 🟨, all functional. Whether paths become painted ground is B6's.

## 10. Icons and portraits (UI)

| Item | Stands in now | Variants needed | Blocked by | State |
|---|---|---|---|---|
| **Building glyphs** (bar, cards) | 14px code-drawn silhouettes on a parchment chip with an ink edge (`BuildingGlyph.cs:139–312`), 22 kinds drawn (the workshop's wheel, D678) | Locked / held / lit; one per kind | B1 | 🟨 — ⚠️ **bridge and stone bridge are grey squares** |
| **Tool glyphs** | Code-drawn (`ToolGlyph.cs:120–210`): paint land, move, demolish, take back, empty, destroy, harvest trees/stone/iron/all, unmark | — | B1 | 🟨 — ⚠️ the drawn Cancel X is unused; the Cancel button is text (`Main.cs:7448`) |
| **Trade glyphs** | Code-drawn, all 17 trades (`TradeGlyph.cs:105–245`; the carpenter's cartwheel, D678); used only in the Professions panel | Could go on cards and the roster | B1 | 🟨 |
| **Goods icons** | **No shapes — a colour chip only** (`GoodsPalette.cs:30–61`, `Main.cs:4518`, `:4529`) | One icon per good (14) | B1 | 🟨 — ⚠️ **iron tools and hay have no colour** (drab grey; iron tools reads as tools) |
| Roadmap goods (coal, cloth, ale, medicine…) | A blank 10%-white chip (`Main.cs:4439`) | — | — | ⬜ |
| Moment marks | 26px code-drawn: nomads, bridge, none (`MomentGlyph.cs:41–63`) | One per moment kind | B1 | 🟨 |
| Season / date | **Text only** (`Main.cs:2949`) | 4 seasons | B1 | ⬜ |
| Speed controls | Text buttons Pause/1x…20x (`Main.cs:5432`) | — | B1 | ⬜ |
| Tabs and category chips | Text (BUILD/REMOVAL/HARVEST; All/Works/Food…) | — | B1 | ⬜ |
| Alerts | ⚠ text glyph, ⛔ emoji in the log, amber numbers | Warning / death / shortage | B1 | 🟨 — ⚠️ **font-dependent, against the code's own reason for drawing icons** |
| Card controls | Unicode ⋮⋮ ✎ ⌖ ✕ ✓ − + ▸ ▾ ⠿ (`Main.Cards.cs:365–392`; `Main.cs:6268`, `:6283`) | — | B1 | 🟨 — ⚠️ same font dependence |
| Need pills, status light | Rounded StyleBox, green/amber; 8px ColorRect (`Main.Cards.cs:404`, `:1823`) | — | B7 | 🟨 |
| **Building portrait** (card) | The footprint quad in its map colour, turned, weathered, dashed when ruined (`Main.Cards.cs:3197–3259`) | Follows the building art | B2, B4 | ✅ counted done by `the-cards.md` — as a rectangle |
| Villager portrait | ⬜ | — | B12 | ✅ decided: none |
| Animal portrait | ⬜ | — | B10 | ⬜ |
| Town hall collections (every crop, tree, fish, animal, building) | ⬜ | Catalogue icons for each | — | 💭 `town-hall.md §5` |

## 11. Panels, chrome, minimap, tech tree

> 📋 **The UI's image assets, one spec sheet (A98): `specs/ui-assets.md`** — chrome, controls, 16 goods, 29 buildings,
> 18 trades, 12 tools, seasons, alerts, moments, screens; ≈ 160 files. Covers §10's icon rows too.
>
> ✍️ **Specced and mocked (A96): `specs/ui-art.md`** — what the UI is today (§2 there, read off the code at 65818ff),
> the lane seam it needs (§4), three looks measured (§5), Joe's four calls (§6).

| Item | Stands in now | Needs | Blocked by | State |
|---|---|---|---|---|
| **Theme and font** | ⬜ — Godot's default theme and built-in font; no `[gui]` section, no `.tres` theme, no font file. TechTreeView's "bold" does nothing (no bold face) | A theme, a font (and a bold) | B7 | ⬜ |
| Window / card skin | `PanelSkin`: slate StyleBoxFlat, 1px khaki border, 3px radius (`Main.cs:6859`); gold edge when selected | — | B7 | 🟨 |
| Moment modal, passing banner, tech nodes | Godot's **default** panel (`Main.cs:2735`, `:2655`; `TechTreeView.cs:123`) | The game's skin at least | B7 | ⬜ |
| All controls (buttons, checkboxes, sliders, progress bars, tooltips, popups, the roster tree) | Default theme | — | B7 | ⬜ |
| Cursor | ⬜ — the OS arrow; `Move` shape on grips only | Per tool (brush, demolish, harvest)? | B1 | ⬜ |
| **Minimap** | Terrain baked 1px a tile; marks are ink-edged squares in **three colours only** — homes, stores, workplaces (`Minimap.cs:114`, `:189–219`) | Library, well, town hall, bridges get no mark; sites look finished; no frame, compass or legend | B7 | 🟨 |
| **Tech tree view** | Default-theme cards 176×160; known = fixed green (`TechTreeView.cs:35`); fogged = 45% + "?" | Spec: known in **its building's colour**, fogged as a **grey silhouette** (`tech-tree-map.md §3.6`); no glyph on the cards though `BuildingGlyph` could supply one | B1, B7 | 🟨 — ⚠️ not to its own spec |

## 12. Title and menu art

| Item | Stands in now | Needs | Blocked by | State |
|---|---|---|---|---|
| Title backdrop | The newest save's real valley, re-baked live, dimmed band down the left (`TitleScreen.cs:95`, `:163`) | — | — | ✅ to spec (`title-and-pause.md §1`) |
| **Logo** | The label *"Kith"*, 56pt, default font (`TitleScreen.cs:177`); the spec asks no more | A wordmark (and a name) | B1 | 🟨 |
| New-game screen | Flat dark backdrop, live valley preview, ringed camp (`NewGameScreen.cs:143–208`) | — | B7 | 🟨 |
| Pause | 55% black dim, slate panel, "Paused" 26pt (`PauseScreen.cs:40–63`) | — | B7 | 🟨 |
| Settings, saves | Default rows in a PanelSkin window | — | B7 | 🟨 |
| **App / window icon** | ⚠️ **the stock Godot logo** (`icon.svg`, `project.godot:22`); `export_presets.cfg:56` `application/icon=""`, so the .exe has the default icon | An app icon (.svg + .ico) | B1 | ⬜ |
| Boot splash | ⚠️ Godot's default splash (no `boot_splash` setting) | A splash | B1 | ⬜ |

## 13. What is finished

- ✅ **The grass look** — masks v06, the season atlas, strength 0.80, jitter 0.15, 63 tiles a repeat, macro variation,
  hex tiling with the height blend (`looks/grass_approved.tres`; `grass-look.md`, D632–D633). Covers the grass layer
  only, and **only in the lab**. Not paper grain (0.15 is a placeholder), not snow, not any other ground kind.
- ✅ **The world palette, for legibility** — parchment ground (D635), the three-band rule and re-picked palette
  (D637–D639), `tools/legibility/check.py` at 0 failures. *Approved colour, not approved art.*
- ✅ **Played and approved as placeholders, not as art:** the condition look (D560), store crates (D418), villager cards
  (D433), turned footprints (D652), the title backdrop.
- Everything else is 🟨 or ⬜ above.

## 14. Cheap gaps found on the way (none fixed — this is read-only)

1. **Bridge and stone bridge have no bar glyph** — grey squares on the Works tab.
2. **Iron tools and hay have no goods colour** — drab grey; iron tools is close to stone tools.
3. **The builder's hut, farmhouse and hunter's lodge have no world colour** — they draw as markets, the bug D449 fixed for
   the quarry. **The stockpile and the cart** also draw in the market's colour (`ColourOf(StoreKind)`).
4. **The minimap marks only three things**, and a site looks finished.
5. **The tech tree view is not to its own spec** (known colour, fogged silhouette).
6. **Unicode symbols as icons** throughout the cards and windows — the font dependence the code rejects for goods.
7. **"No image assets" is cited to a decision that does not say it** (B1) — ✅ **resolved, D654**: images are allowed and the citations are corrected.

## 15. ⭐ Phase 6's art — the running list (Joe, A90; kept current from each systems spec, D774)

Joe: *"take note of the systems work being done and all of the buildings and art pieces that will have to be created for
that."* In `DESIGN.md §4`'s D750 order. **Every systems spec now names the buildings, goods and villager states it adds
in a section of its own (D774)**; this list copies from there and is corrected when a spec moves. ⚠️ **Rows marked
*not specced* are read off §4's one-line entries and are guesses until their spec is written** — and the *systems
complete* review (D774, after water) will add more, upgrade tiers and tech unlocks among them.

| # | Systems piece | Where it stands | Buildings | Goods / icons | Professions | Villager states and things on the map |
|---|---|---|---|---|---|---|
| 1 | **The school** | ✅ built and merged (D752–D769), played (D773) | **the school** — ⚠️ painted by the default (a shingle roof, no yard; `VillageMap.Roofs.cs` has no row for it); no bar glyph (`BuildingGlyph` *Unchosen*) | — | **the teacher** — ⚠️ no trade colour (A90 call 4) | `WalkingToSchool`, `AtSchool`, `Teaching` (indoors — A90 call 3); children in coats from five (D767) |
| 2 | **Water as a household need** | 🔨 on `slice/water-need` (`water-need.md` §4; calls answered D776) | the well (exists) | none — water is the household's, not a good (call 1A) | — | **drawing water at the river bank** (new); **carrying water home** — a pail, or a yoke and two pails (new: the first thing carried that is not a good); the well's lane worn by the existing path wear |
| 3 | **Morale** | specced, not started (`morale.md`; re-read against D207 first) | the levers, `tech-tree.md §9.10`: **a tavern and inn**, **a village chapel** (and the cemetery D35 gives a shape) | — | (tavern keeper, priest — not named) | **leaving the village** — the one new mechanism: a departure walked out, the arrival chevron's reverse (a bundle on the back?); ⛔ no morale bar or face (`morale.md §5`: sentences on the card only) |
| 4 | **Beer** | waits on morale (`food-catalog.md §6`) | **a brewery** | **beer / ale** (today a blank roadmap chip, §10); barley if wheat is kept for bread | **a brewer** | carrying a cask (sheet 10's barrels); the tavern's stock |
| 5 | **Health, diet first** | not specced (after §5's survival floor is re-derived) | **an infirmary**; an herb garden? | **medicine / herbs**; the diet's four food groups (grain, meat, fish, fruit/herbs) as marks on the card | **an herbalist** | **sick** — a state that must read on the map before it bites (§1.1, Phase 6's gate item 6); graves |
| 6 | **Drought, then fire and blight** | not specced; each *telegraphed, legible on the map, answerable* (§0.1) | irrigation? a firebreak? | — | — | **dry ground** (B11) and the year's warning on the map; **a building on fire**, smoke, **burnt** (a ruin per kind, §5.1); **a blighted field** (§4); villagers carrying water to a fire? |
| 7 | **The town hall's tabs and the knowledge screen** | `town-hall.md` slices 2–4; after the last content the *systems complete* review adds (D776) | — | **an icon for every crop, tree, fish, animal, technique, building and first master** (the collections, §10) | — | the founders' page, charts, the knowledge screen — **the UI piece's**, after the villagers |
| 8 | **Warm clothing — sheep and wool** | Phase 6's now, unless Joe moves it out at the review (D776 on `slice/water-need`, not yet in `main`; was optional, D723) | a fold or shearing shed? (not decided) | **wool**; **the wool coat** | spinner / weaver? (not decided) | **sheep** — a second herd (§7), shorn and unshorn; **shearing**; **a wool coat** on the villager, apart from the leather coat's moss |
