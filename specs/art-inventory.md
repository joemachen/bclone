# Inventory: every piece of art the game still needs

> **Status: an audit, read off `main` at 880db1e (2026-10-30, D653).** Joe: *"Build an art asset inventory for bclone so I
> know everything that still needs real art."* Not a spec and not a plan — it invents nothing and decides nothing, and
> **it does not reopen the art.** Everything here stays under HANDOFF's **🅿️ ART: PARKED** (D639) until Joe reopens it.
> Companion to `content-inventory.md` (what the village *has*); this is what the village *looks like* and what stands in.
> ⚠️ **Dated the day it was read.** A building, good or animal added after D653 is not in these tables.
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

These are what decide *what kind* of art each row needs. Most rows below cannot be drawn finally until B1–B4 are answered.

| # | The call | Where it stands | What it blocks |
|---|---|---|---|
| **B1** | **Is the game allowed image assets at all?** The view cites *"the project ships no image assets (D26)"* in five places (`GoodsPalette.cs:22`, `TradeGlyph.cs:10`, `BuildingGlyph.cs:11`, `ToolGlyph.cs:56`, `MomentGlyph.cs:10`) and `build-bar.md:37` calls it *"⛔ D26 — no image assets"*. ⚠️ **But D26 in `DESIGN.md §7` is "the world has bounds and the map has a camera"** — it says nothing about images. The rule is real in the code and has no decision behind it in the log. | ⚠️ Unrecorded | **Every** sprite, texture, icon, portrait and logo below. Answering it is the first art decision. |
| **B2** | **D68's destination — *Foundation* (painted 3D) or a 2D drawn map** (`world-legibility.md §7` call 5; D68 at `DESIGN.md` §7). Parchment and the `art/` references point at 2D drawn. | ⏸️ Joe's | Everything in the world: projection, buildings, villagers, animals. |
| **B3** | **Phases 7–8: a 3D front end (`src/Bclone.Game3D`) on top of the same sim**, the 2D view kept as the debug/probe view (`DESIGN.md §4`, "Phases 7 and 8"). If it stands, finished 2D world art is the debug view's. | Roadmap | Whether 2D world art is the shipped look or a stopgap. Tied to B2. |
| **B4** | **Projection and turning.** The view is flat top-down (D27) and footprints turn freely — homes to any 1/64 turn (D411), any building since gridless, one piece at any angle (D650). The `art/` references are **fixed-orientation ¾ sprites** (origin 0.5, 1.0; `art/art-direction-hand-drawn-orthographic-asset-pipeline.jpg`). Nothing reconciles the two. Store crates already snap to a quarter turn (D423), which is a precedent. | Undecided | Every building, villager, animal and object sprite. A ¾ sprite cannot turn to 1/64. |
| **B5** | **The tree look** — ink-outlined canopies over an in-band green, or mid-tone washes without lines (`world-legibility.md §7` call 4). Canopies sit in the ground's band as an interim (D637). | ⏸️ Joe's, parked | Canopies, saplings, the woods ground, berries and game drawn in the woods. |
| **B6** | **The terrain shader's open questions** (`terrain-shader.md §2–3`, *not started*): snow's blend (lerp or lighten — Multiply cannot reach a colour lighter than parchment), paper grain anchored to the land or the window, ground kinds as flat fills, washes or textures, splat blending and edge bleed. | 🅿️ Parked | All terrain rows; the grass going into the valley; snow. |
| **B7** | **UI chrome: slate or parchment.** D637 call 3 keeps the UI's own chrome slate (only pictures of world things sit on a parchment chip); the `art/` references show parchment panels and wax seals. | Decided slate (D637), references disagree | Panels, windows, buttons, theme, font. |
| **B8** | **Tiers, which are content not yet decided:** house tiers (cabin → cottage → manor; `DESIGN.md §5`, D206), barn tiers (lean-to → timber → stone; `TECH-EXAMPLE.md`), the mill as a watermill by water and a windmill elsewhere (`buildings-plan.md §4.3`). | 💭 No rows | Each tier needs its own art. |
| **B9** | **The condition look** — *"steps rather than cracks or smoke"* (`building-condition.md` line 101) was Claude's call; Joe approved what he saw (D560) but never chose against cracks. | ✅ Played | Whether damage art is a tint or drawn wear. |
| **B10** | **Which animals.** Kept: cattle only, one kind first (`livestock.md §0`). Wild: no species, *"no models is fine for now — but we're going to have to add models in eventually"* (`hunting.md §9`). Fish subtypes deferred (`fishing.md §10.1`). `TECH-EXAMPLE.md` names 25 species. | Open | The whole animal list. |
| **B11** | **Fire, blight and drought** must be *telegraphed, legible on the map, and answerable* (`DESIGN.md §4`, Phase 5) and have no spec, so no visual state is designed. | 💭 Phase 5 | Burning/burnt buildings, blighted fields, dry ground. |
| **B12** | **A villager has no picture, on purpose** (D431, `the-cards.md §2a`). | ✅ Decided | Villager portraits — none needed unless reopened. |
| **B13** | **Clothing** is specced and parked (D534, `clothing.md`); no look is specified. | ⏸️ Parked | Villager variants by dress. |
| **B14** | **Goods seen arriving and leaving stores** — Joe, D418: *"visual goods being added to / removed from each building"*; the crates are the placeholder (`storage-and-distribution.md`, D419). | Wanted, art pass | Store stock art. |

## 2. Terrain

| Item | Stands in now | Variants needed | Blocked by | State |
|---|---|---|---|---|
| **Grass / open ground** | Flat parchment `#e6d6b0` with a ±3.5% bloom in the bake (`VillageMap.cs:71`; `ValleyTexture.cs:608`) | 4 seasons, blended (20 days held, 10 blending — `seasonal-grass.md §6.4`) | B6 — the shader to put it in the valley | ✅ **Look approved** (D632–D633, `looks/grass_approved.tres`, refs in `art/grass/`) — **lab only, not in the valley** |
| Water and shallows | `#6f97ad` shading to `#9cbccb` by edge confidence — the only shoreline (`VillageMap.cs:123`, `:135`) | Seasonal tint? River never freezes (`fishing.md §10.3`) | B6 | 🟨 |
| Woods ground / young wood | `#9aa47a` / `#b3b98d` with two-octave foliage noise (`ValleyTexture.cs`) | Seasons (no spec asks), far-view bake | B5, B6 | 🟨 interim (D637) |
| Stone seam / iron seam | `#b4ab99` / `#b9906f` with grain noise | Unworked / worked | B6 | 🟨 |
| Quarry and mine floor | Traced fills `#a39b8e` / `#bf957c` (`VillageMap.Meshes.cs:486`, `:495`) | Grows as worked | B6 | 🟨 |
| Ploughed / sown / ripe field | Traced fills `#b39874` / `#a8b07a` / `#d6b55c`, drawn per tile, not baked (`VillageMap.cs:208–219`) | See §4 (crops) | B6 | 🟨 |
| Valley edge and beyond | `#c4b48f` beyond, a 2px `#8c7a5c` edge (`VillageMap.cs:77`, `:4003`) | — | B6 | 🟨 |
| Optional grid | 1px `#d2c29c` lines (`VillageMap.cs:4016`) | — | — | 🟨 debug aid |
| **Snow** | ⬜ nothing — no season is drawn anywhere in the valley | Snow cover, melt | B6 (needs a non-Multiply blend) | ⬜ |
| Watercolour textures for stone, clay, dirt, the rest | ⬜ — flat fills in the band | — | B1, B6 | ⬜ 🅿️ parked list. ⚠️ *Clay has no terrain kind in the sim.* |
| Paving (dirt → gravel → cobble) | ⬜ | 3 grades | B6 | 💭 `DESIGN.md §2.6`, no sim |
| Dry ground (drought) | ⬜ | — | B11 | 💭 |

## 3. Shader and tile support

| Item | Stands in now | Needs | Blocked by | State |
|---|---|---|---|---|
| **The valley's terrain shader** | ⬜ — the valley is a `DrawTextureRect` of the CPU bake (`VillageMap.cs:5224`) | One canvas_item shader with a coverage channel (D628 option A) | B6 | ⬜ `terrain-shader.md`, not started |
| Grass shader | `grass_lab.gdshader` + season and anti-tile includes, masks v02–v06, `grass_seasons.png` (256×16) | Integrated into the valley | B6 | ✅ look / ⬜ in game |
| Paper grain | `paper_grain.gdshader`, procedural, **screen-anchored** placeholder, strength 0.15 a placeholder | World-anchored grain | B6 | 🟨 lab only |
| Splat blending between ground kinds | The bake's domain-warp and 3×3 kernel give smooth contours (`ValleyTexture.cs:468–588`); no blending | Blend + edge bleed | B6 | 🟨 |
| Snow blend | ⬜ | lerp / lighten mode | B6 | ⬜ |
| Debug readout backing | ⬜ — frame/draw lines are illegible on parchment (`Main.cs:8275`) | A dark backing | — | ⬜ 🅿️ parked list |

## 4. Trees and vegetation

| Item | Stands in now | Variants needed | Blocked by | State |
|---|---|---|---|---|
| **Mature tree** | 3–4 discs per forest tile, `#7d8c5c`, r 0.20, per-disc shade; only above ~24 px a tile, the bake below (`VillageMap.Meshes.cs:418–427`; `VillageMap.cs:4947`, `:5118`) | Seasons (leaf colour, bare winter) — **no spec asks**; felled/stump | B5, B4 | 🟨 interim |
| Sapling | 2 smaller discs, `#98a56f`, r 0.13 (`Meshes.cs:420`) | Growth to mature | B5 | 🟨 |
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

**Every building today is the same thing**: a rotated footprint quad at 0.8× its extent, filled with its colour, a 2px
ink `#2a2018` edge (`VillageMap.cs:2217–2243`). There are **no per-kind shapes in the world** — kind is colour only.
Sizes: 3×2 town hall, barn · 2×2 granary, warehouse, market, farmhouse, library, mill · 2×1 house, huts, smithy, quarry,
mine, bakery, trading post · 1×1 forager's hut, forester's hut, fishing hut, well, stockpile.

### 5.1 States every building shares

| State | Stands in now | Needs | Blocked by | State |
|---|---|---|---|---|
| **Construction site** | Outline + a fill rising from one edge with progress, `#8f9aa8` / edge `#38424e`; harvest orange while the ground still needs clearing (`VillageMap.cs:283–292`, `:6739–6807`) | Staged art. `art/…asset-pipeline.jpg` shows **three stages: blueprint ghost, under construction, completed** — no spec asks for stages | B1, B4 | 🟨 |
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
| Stockpile | ⚠️ draws in **MarketColour** — no colour of its own (`VillageMap.cs:262`) | ✅ | Stock heaps | 🟨 |
| Founders' cart | ⚠️ draws in **MarketColour** — a store quad, no cart | — | A cart | 🟨 |
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
| **A villager** | A dot, r max(3px, 0.2 tile), coloured by life stage — adult `#454135`, child `#02465f`, elder `#5d3900` (`VillageMap.cs:385–388`, `:7822–7881`); crowds fan apart | Life stage ×3; facing/walk; per trade? | B1, B2, B4 | 🟨 |
| Selection ring | `#59238e` | — | — | 🟨 (UI-like) |
| Meal mark | A bowl arc `#543d01` for 0.5 s (`VillageMap.cs:7888`) | — | — | 🟨 |
| Arrival chevron (nomads) | `#4e3f1b`, bobbing and fading (`VillageMap.WalkIn.cs:46`, `:367`) | — | — | 🟨 |
| Visible work | The view draws **no** work state on purpose — you read work from *where* a person stands (`trades-visibly-work.md:28`) | Working poses (chop, dig, reap)? | B4 | 🟨 by design |
| Carried goods | ⬜ — the sim tracks a carried load, nothing draws it, no spec asks | Per good | B4 | ⬜ gap |
| Tools in hand | ⬜ | Stone / iron | B4 | ⬜ gap |
| Clothing | ⬜ | Per garment | B13 | ⏸️ parked |
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
| **Heaps / goods on the ground** | Square chips in the goods colour, ink edge, side by side beside a building (`VillageMap.cs:7526–7631`) | Per good; **amount** (no size variant today) | B1, B4 | 🟨 |
| **Store stock** | A crate per good, height = fill, snapped to a quarter turn (D417–D423) | Goods arriving and leaving (B14) | B14, B4 | 🟨 played (D418) |
| Boulders and ore lumps | Discs `#cdc6b8` / `#5c3424`, 2–4 a tile; a worked face has one fewer, cut to 0.6 (`Meshes.cs:471–498`) | Unworked / worked | B6 | 🟨 |
| **Trading boat** | 6-point hull `#7a5636`, triangle sail `#efe6d2` (`VillageMap.Boat.cs:13–15`, `:68–88`) | Arriving / docked / leaving (built) | B4 | 🟨 |
| Fishing boats | ⬜ | — | — | ⬜ |
| Overland caravan, cart road | ⬜ | — | blocked on carts (`trade.md §4.7`) | 💭 |
| Smoke, fire, chimney | ⬜ — none anywhere | Per working building, winter hearths | B11 | ⬜ |
| Tools on the ground, carts | ⬜ | — | — | ⬜ |
| Doorsteps and the plaza | Drawn by the trail network (D605) | — | B6 | 🟨 |
| Graves | ⬜ | Growing cemetery (D35) | — | 💭 |

## 9. Overlays (probably stay drawn — listed so nothing is forgotten)

Zone fills and dashed edges (residential, harvest, destroy, work ground — `VillageMap.cs:295–369`, `:4643–4840`); the
**paths** — worn `#8c7457` and packed `#8a7056` trail meshes with tapered ends and junction discs (`Meshes.cs:761–883`),
which are world art as much as overlay (B6); the wear debug wash; the brush preview; the selection outline; the
gathering-radius ring; home-to-work route lines. All 🟨, all functional. Whether paths become painted ground is B6's.

## 10. Icons and portraits (UI)

| Item | Stands in now | Variants needed | Blocked by | State |
|---|---|---|---|---|
| **Building glyphs** (bar, cards) | 14px code-drawn silhouettes on a parchment chip with an ink edge (`BuildingGlyph.cs:139–312`), 21 kinds drawn | Locked / held / lit; one per kind | B1 | 🟨 — ⚠️ **bridge and stone bridge are grey squares** |
| **Tool glyphs** | Code-drawn (`ToolGlyph.cs:120–210`): paint land, move, demolish, take back, empty, destroy, harvest trees/stone/iron/all, unmark | — | B1 | 🟨 — ⚠️ the drawn Cancel X is unused; the Cancel button is text (`Main.cs:7448`) |
| **Trade glyphs** | Code-drawn, all 15 trades (`TradeGlyph.cs:105–235`); used only in the Professions panel | Could go on cards and the roster | B1 | 🟨 |
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
| **Logo** | The label *"bclone"*, 56pt, default font (`TitleScreen.cs:177`); the spec asks no more | A wordmark (and a name) | B1 | 🟨 |
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
7. **"No image assets" is cited to a decision that does not say it** (B1).
