# The herbalist's pictures — the hut on the map, its icon, herbs, the remedy and the trade (A324)

> **Status: ✍️ MOCKED (A325, 2026-11-27), nothing built. ⏸️ Six calls Joe's (§4).** Claimed A324. Art lane (CLAUDE.md
> *Two tracks*), branch `art`. W6 piece 2's ask of the lane (`health.md §11`), painted **ahead of the systems lane**: no
> `BuildingKind`, `JobKind`, good or job for the herbalist, herbs or remedy exists yet — W6 piece 2 makes them on its own slice.
> Mocks: `D:\Projects\brushes\herbalist\` (`herbalist_pick.jpg`; renders in `out\`, the scratch game in `game\`; `mock.py` and
> `VillageMap.MockHerb.cs` patch it, `patch.py` makes the save copy, `p3_icons_herb.jsx` + `paint_mock.ps1` the icons).
> ⚠️ **Not in this piece:** the herbalist *gathering herbs* and *tending* at a bedside (`health.md §11`) — they draw what piece 2's
> code does, so they wait for it.

## 1. What is there today

Nothing. A workplace with no row in `VillageMap.RoofOf(JobKind)` draws as a plain shingle roof with no yard, in no colour of its
own (`ColourOf` falls to the default); a building with no icon file keeps `BuildingGlyph`'s fallback; a good with none keeps its
chip; a trade with none keeps `TradeGlyph`'s. So without this piece the hut would arrive as an anonymous box.

What it has to say, from `health.md §6`: a small hut where one keeper makes **remedies from herbs**, kept at the hut — and
**autumn's stock of remedies is the winter's answer**, so the stock should be visible the way the tavern's casks are.

## 2. The mock

On a copy of his Hollowmere (honey-harrow, Year 93), a hut added on open grass among his houses below the town centre, between his
cabin and the market stalls (`patch.py`: a copy of his forager's hut's row, renamed; nothing of his touched). Drawn by the map's
own painter (`RoofDrawing`, its faces, pen, hatching and yards) in a scratch copy of the game, the closest zoom and the middle.

**The footprint is the systems lane's call; the mock is 2 × 2** (the tavern's). At 2 × 1 (the workshops') every garden was a
strip too thin to read at the closest zoom — the first try is in `out\`. ⭐ Recommended to the systems lane: **2 × 2**.

**The hut on the map** — the trade's colour washes the roof, as every workplace's does:

- ⭐ **A — the herb garden in front.** A thatched hut with a chimney (remedies are boiled) at the back of its plot; in front, a
  wattle-fenced garden with a path from the door to a gate and three beds a side, each its own herb (sage, lavender, calendula,
  yarrow, mint, borage), herbs drying on a rack along the wall. At the middle zoom it reads as a striped garden — nothing else on
  the map has one.
- **B — the drying yard.** A shingled hut; in front, two long frames hung with bundles, a bench with a mortar, potted herbs. The
  working look; at the middle zoom the frames read as fences, the weakest of the three.
- **C — the walled physic garden beside it.** A turfed hut down one side; beside it a low stone wall round a square garden
  quartered by paths, four beds and a bee skep in the middle. A chequer at the middle zoom; as distinct as A.

**The stock — the remedies, kept at the hut** (every look): a shelf of jars by the door, two jars a level (the stock read, 0–3,
like the tavern's casks): empty in spring, full by autumn. Mocked at 0, 1, 2 and 3.

**The colour** (the roof's wash on the map, the icon's walls, the trade's mark): ⭐ **lavender `#8b78a6`** — no trade is violet;
ΔE 17.5 from the merchant's blue-grey, its nearest. Sage green was mocked too: a green roof on green grass all but vanishes.

**The icons**, painted in Photoshop by the icons' own painter (`p3_icons.jsx`'s shapes, a scratch copy), shown big and at the
bar's 20 px beside the icons they stand among:

- **The hut** (`buildings/herbalists-hut` and `-owed`): ⭐ **A** the hut, lavender walls, thatch and chimney, a raised bed of
  herbs beside it (sage, a lavender spike); **B** the hut shingled, a drying frame hung with bundles beside it. Each pairs with
  its map look; mixing is fine (C goes with A's icon).
- **Herbs** (`goods/herbs`), sage `#8fae6a` (ΔE 18.5 from clothing's moss green, its nearest): ⭐ **a tied bunch**, five stems
  fanned, a gold twine round them; or **one sprig**.
- **The remedy** (`goods/remedy`), plum `#7a4a6e` (ΔE 26 from anything): ⭐ **a plum-glazed jar** with its cloth cover tied and a
  sprig tucked under the twine — the jar on the hut's shelf; or **a corked phial** with a paper label.
- **The herbalist** (`trades/herbalist`), not on `§11`'s list but the probe's `ui icons:` asks a file of every trade: a
  lavender mortar, its pestle and a sprig of sage.

## 3. What the systems lane wires (W6 piece 2) — the names and where the pictures live

The icons are found **by the row's name** (`UiIcons.Slug`), so they need no code:

| The row W6 piece 2 adds | Its name in the data | The file the art lane paints |
|---|---|---|
| the building | `herbalist's hut` | `textures/ui/icons/buildings/herbalists-hut.png` and `herbalists-hut-owed.png` (80 px) |
| the good | `herbs` | `textures/ui/icons/goods/herbs.png` |
| the good | `remedy` | `textures/ui/icons/goods/remedy.png` |
| the trade | `herbalist` | `textures/ui/icons/trades/herbalist.png` |

⚠️ A different name is a different file: if the slice names them otherwise (*remedies*, *herb garden*), the file is renamed with it
— tell the art lane. A file with no row is harmless (nothing looks for it), so they can sit in `main` before the slice lands.

**The map** needs one row each in two systems-lane files, keyed by the new `JobKind` (written by the slice, since the kind is
theirs):

- `VillageMap.Roofs.cs` `RoofOf(JobKind)`: `JobKind.Herbalist => new(RoofForm.Roof, RoofMaterial.Thatch, Chimneys: 1, Yard: YardKind.Herbalist)` — the
  art lane builds `YardKind.Herbalist` (its layout, its garden, its jars by the remedy stock) ahead, in `VillageMap.Yards.cs`, with
  its probe, so the slice adds only this row and the stock read.
- `VillageMap.Yards.cs` `StockReadOf`: `YardKind.Herbalist => (StockRead.Remedy, StockRead.None, false)` and a `StockRead.Remedy`
  reading the hut's own store of the remedy good — the slice's, because `Goods.Remedy` is.
- `VillageMap.cs` `ColourOf(JobKind)`: `JobKind.Herbalist => TradeGlyph.ColourOf(kind)`, with `TradeGlyph.ColourOf`'s
  `JobKind.Herbalist => new Color("#8b78a6")`; `GoodsPalette`'s `Goods.Herbs => #8fae6a`, `Goods.Remedy => #7a4a6e` (as W3's ale
  and brewer, D829).

## 4. Calls — ⏸️ Joe's

1. **The hut on the map:** ⭐ A (herb garden in front), B (drying yard) or C (walled garden beside).
2. **The hut's icon:** ⭐ A (raised bed) or B (drying frame).
3. **The herbs icon:** ⭐ the tied bunch, or one sprig.
4. **The remedy icon:** ⭐ the jar, or the phial.
5. **The colours:** ⭐ lavender for the herbalist, sage for herbs, plum for the remedy.
6. **His OK to build the yard ahead** in `VillageMap.Yards.cs` (a new `YardKind.Herbalist`, its layout and things) and its probe
   line, outside the lane's table like the tavern's and the great library's — nothing in `data/` or the sim, so the suite stays
   skipped by D846. The rows that name `JobKind.Herbalist` stay the slice's.
