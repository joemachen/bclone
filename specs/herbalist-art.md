# The herbalist's pictures — the hut on the map, its icon, herbs, the remedy and the trade (A324)

> **Status: the poses ✍️ MOCKED (A330, §6); ✅ Joe: *"All as recommended"* (A331) — the build waits for `slice/w6-fever`'s code (§6.5). The hut and icons ✅ BUILT (A326, 2026-11-27), MERGED INTO `main` AT JOE'S WORD (A327, *"Yes, merge"*, `51f6fc8`), pushed at his word (A328) — Joe: *"All as recommended"* (§5). Nothing shows in play until W6 piece 2 adds the rows of §3.** Mocked A325; claimed A324. Art lane (CLAUDE.md
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

## 5. Built (A326) — all six as recommended

His answer, said back before building: **the map look A, the hut's icon A, herbs the tied bunch, the remedy the plum jar, lavender / sage / plum, and his OK for `VillageMap.Yards.cs` and the probe line.**

- **The yard:** `YardKind.Herbalist` (`VillageMap.Yards.cs`: the enum, `LayoutOf`'s case, `YardUnder`'s case); everything else in the new `VillageMap.Herbalist.cs`. The hut at the back of its plot (0.44 of the depth), the garden in front across the door's way: **seven slots, the path in the one nearest the door and a bed in each of the other six**, so the garden keeps its six beds wherever the door is (the mock's beds followed the door and lost two at an end); sage, lavender, calendula, yarrow, mint and borage in that order; the wattle on three sides with a gate at the path; five bundles on a rack by the wall; **the remedies two jars a level (`YardState.Stock`), to six, on a shelf only when there is one** — plum jars, cream covers. It reads the stock it is given: until the slice adds `StockReadOf`'s row it is 0 (an empty shelf), as any yard with no read.
- **The icons**, painted by `D:/Projects/brushes/herbalist/p3_icons_herb.jsx` (a copy of `p3_icons.jsx` with the herbalist's shapes; the shared painter untouched), 80 × 80, mipmapped: `buildings/herbalists-hut` and `-owed` (the owed medallion at (205, 208) on the 256 grid, measured as the great library's was), `goods/herbs`, `goods/remedy`, `trades/herbalist`. Found by name, so they wait harmlessly for their rows.
- **The probe:** `herbalist's hut:`, riding on `building yards:` after the tavern's (trap 73), draws the row §3 gives the slice itself: the hut behind and under half the plot with the garden in front at four doors; six beds, the path within half a slot of the door at five doors and no bed on it, the wattle, five bundles; the jars two a level to six, the shelf only with a jar; every thing on its ground at four turns, at 2 × 2 and at 2 × 1. ⚠️ Its first run found a real fault: the path took the slot the door would have at −1…1 where the slots span −0.84…0.84, so an end door's path sat a slot inward. Red-checked: 11 mutants, 11 red (a bed on the path too went red by the exception the line catches — a seventh bed runs past the six colours).
- **For the slice, besides §3:** `building yards:`'s §2 table wants a row for `YardKind.Herbalist` once `StockReadOf` reads the remedy — `(YardKind.Herbalist, StockRead.Remedy, StockRead.None, false)` — or that line goes red.
- **Seen:** `herbalist_built.jpg` (on his Hollowmere, the hut drawn by the built code through a scratch stand-in for §3's row; the shelf empty and full).

## 6. The herbalist's two poses — ✍️ MOCKED (A330); ✅ all four as recommended (A331); not built until the slice's code exists

Joe to the coordinator: *"Yes, mock them now"* (A329's claim). `health.md §11`'s *gathering herbs* and *tending* (kneeling at a
bedside), mocked before W6 piece 2 (`slice/w6-fever`, D951) has a state for either. Sheet:
`D:\Projects\brushes\herbalist\poses\herbalist_poses_pick.jpg`. The figures are the game's own `PersonPainter`, a scratch
copy (`poses/cand.py` → `PersonPainter.cand.cs`, painted by `poses/harness/`); the map behind them is the built game's renders
(60 px a tile at the closest zoom, a person 0.72 tiles: 43 px). Painted as a forager key with a mock switch; nothing in the repo.

### 6.1 The look — call 1

The trade's lavender (`#8b78a6`, §2) for the gown, calf length, and ⭐ **A — a plum hood** (`Head.Liripipe`, hood `#6c4a6e`, the
forester's and hunter's shape in a colour of its own), or **B — a coif and a linen apron**. B is the baker's and the miller's head and
apron: across the map, a lavender baker. A is nobody else's.

### 6.2 Gathering herbs — call 2

Out to the site with **a sickle in the hand and a trug of herbs on the other arm** (new: `Tool.Sickle`, and the trug, drawn
whenever the herbalist is going to or at work). At the site, the work's three beats (beat 0 the sickle held, 1 reached down to the
stems, 2 the cut drawn back), two ways:

- ⭐ **A — stooped** — the forager's bend, deeper (the body leaning into the cut); the silhouette of someone at work.
- **B — kneeling** — the body lowered, the skirt pooled on the ground, the sickle by the knee. A new silhouette, low and wide.

⚠️ The gatherer's sites are in the woods, and people there are drawn **under the canopy** (A36): deep in a wood the herbalist is
hidden, as the foragers are; at a wood's edge they show. Nothing to change — the same rule as everyone.

### 6.3 Tending — call 3

The herbalist **walks to the sick home carrying a remedy jar in both hands** (the plum jar of `goods/remedy`, cream cover) — seen on
the way in either option. Then:

- ⭐ **A — inside with them.** The sick are drawn indoors (Joe's call, A320–A321: the face over the roof), so a bedside inside is
  never seen. The herbalist goes in like anyone entering a house, and **the remedy jar sits beside the sick face over the roof while
  someone tends them** — at a glance, *tended* against *alone*, which is what decides whether a run-down person lives
  (`health.md §6.1`: fever kills only the untended).
- **B — kneeling at the door,** side on, the jar held out: the literal *kneeling at a bedside*, moved outside so it can be seen.
  The same figure-by-the-door Joe turned down for the sick themselves (A311).

### 6.4 What the systems lane should name — for the build (and Joe's OK for the files: call 4)

The pictures key on the trade and the state, as every trade's do; the names W6 piece 2 would ideally use:

| What | Name | What the view does with it |
|---|---|---|
| the herbalist at a gatherer's site | `VillagerState.Gathering`, with `JobKind.Herbalist` (the forager's state) — or a new `GatheringHerbs` counted by `VillageMap.AtWork` | the sickle's beats, the trug |
| going there | `TravelingToWork` (today's) | the sickle and trug in hand |
| walking to the sick with the remedy | carrying `Goods.Remedy` (`Villager.Carried`), any walking state | the jar in both hands (`OnTheShoulder` false) |
| at the sick person's home | ~~`VillagerState.Tending`~~ ⚠️ **taken — the farmhand's summer state (`Season.cs`), A331:** ✅ **`VillagerState.TendingTheSick`** (the slice's D956), **housed at the patient's home** in `DescribeState` / `ShownOnTheMap` (A320's trap: a state the view does not know is indoors is drawn at a door) | A: indoors; B: kneeling at that door |
| whether a sick person is tended | a read like `SimWorld.IsTended(Villager)` (or `TendedBy`) | A: the jar beside their face |

**Files the build would touch** (call 4, Joe's OK): `PersonPainter.cs` (the look, `Tool.Sickle`, its beats, the trug, the jar; and B
the kneel), `VillageMap.SickMark.cs` (A: the jar by the face), `VillageMap.Indoors.cs` (only if the slice's `Tending` needs the view
to house it), `VillageMap.PeopleProbe.cs` or a new probe file. Nothing in `data/` or the sim: the suite stays skipped by D846.

### 6.5 Joe's answer (A331) and what the build waits for

Joe: *"All as recommended"* — **the plum hood; gathering stooped, with the sickle and trug; tending indoors, the remedy jar beside the sick face while tended; his OK for `PersonPainter.cs`, `VillageMap.SickMark.cs` (and `VillageMap.Indoors.cs` if needed).**

**Not built yet, on purpose:** every picture keys on something W6 piece 2 has not written — `JobKind.Herbalist` (the look, the sickle's beats), `Goods.Remedy` (the jar in the hands), the herbalist's tending state and `SimWorld.IsTended` (the jar by the face). `slice/w6-fever` took §6.4's names into `health.md` §9 step 7 (D953) and is spec-only (D954). The art lane builds when those exist — on `art` after the slice merges into `main`, or from the slice's branch at Joe's word — with the A326 discipline: a probe line of its own, red-checked, the four checks.

⚠️ **A name the art lane asked for was taken** (found A331): `VillagerState.Tending` is the farmhand tending a sown field in summer (`BehaviorSystem`: *"tending the field"*). §6.4 asked the slice for *"a new `VillagerState.Tending`"* and D953 took it as written — it would not compile, and a view keyed on `Tending` would put herbalists in farmers' fields. Sent to the slice: a state of its own (`TendingTheSick` suggested).

✅ **Taken (A332, the slice's D956):** `VillagerState.TendingTheSick`, housed at the patient's home in `DescribeState` / `ShownOnTheMap`, and `SimWorld.IsTended(Villager)`. The rest as §6.4: `JobKind.Herbalist`, `Goods.Herbs`, `Goods.Remedy`; gathering is `VillagerState.Gathering` with `JobKind.Herbalist`; the walk carries `Goods.Remedy` in `Villager.Carried`. The slice has built step 1 (fever); herbs, the hut and tending are its steps 2–4, and it will say when they are in code.
