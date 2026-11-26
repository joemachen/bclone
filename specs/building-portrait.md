# The building's picture on its window (A260–A261)

**Status: ✍️ mocked, ⏸️ Joe's calls (§4). Nothing built.** Art lane, branch `art`.

Joe's play note 5 (2026-11-24): *"on building windows, the building should look like the actual in-game version"*.

## 1. Today

A building's card has a picture (`BuildingPortrait`, `Main.Cards.cs`): its footprint as a coloured rectangle on a parchment
chip, turned as it stands, worn as it is (D376, D556), with a house's tier marks (D722). It is older than the painted
buildings (A85 roofs, A86 yards): the map has drawn every building as a painted roof and yard since, and the card never
learned it. A building site is a builder-blue rectangle whatever it will become.

## 2. The idea

The card draws the building **with the map's own painter**: the same pieces (`VillageMap.RoofDrawing`) from the same inputs
(its look, colour and wear, door, seed, yard stock), so the card and the map cannot disagree. Turned as it stands; a ruin
falls in on both; a yard's stock is the one the map shows. Laid again only when one of those inputs changes (CLAUDE.md:
nothing derivable rebuilt per frame) — the map's `RoofSignature` is already that key.

## 3. The mocks (2026-11-25)

Scratch game copy in `D:\Projects\brushes\portraits\` (`mock.py`, `render.gd`, `run.sh`, `sheets.py`): the real card, opened
on a copy of his save, the picture drawn four ways.

- `portraits_1_the_look.jpg` — Elderwood (thistle-copse-32), Year 40: two cottages, the market, a granary, the smithy, the
  workshop, the tavern, a farmhouse. Columns: today · **A** painted at today's size (64) · **B** painted, the picture taller
  (120) · **C** painted, with the map itself around it (the map's own ground, paths, trees and people).
- `portraits_2_sites.jpg` — Stonebridge (meadow-robin-14): three new buildings going up. Columns: today · **S1** as the map
  draws a site (the outline filling as it is built) · **S2** the finished building faint under the fill · **C**.

Found mocking: a repair (*"the builders mending it"*) is a site on a standing building — the map shows the building, so its
picture is the painted building in every option. The idle and full rings stay on the picture as today.

## 4. Joe's calls

1. **The look — A, B or C.** Recommended **B**: the map's drawing, big enough to read the yard and the stock. A is the
   same at today's size and the yard is crumbs. C is the most "in-game", but it is a second drawing of the valley each time
   a card opens (unmeasured — the title's still took 41 ms for a whole screen), and trees and walkers can cover the
   building (the granary).
2. **The picture's height.** B and C make it 120 (today 64): the card grows by about 40 px. Recommended yes.
3. **A building site — S1 or S2.** Recommended **S1**, what the map shows; the card's title already says what it will be.
4. **The files outside the lane's table** (`Main.Cards.cs` and `VillageMap.cs` are the systems lane's):
   - `Main.Cards.cs`: the picture's class moves to its own file (`BuildingPortrait.cs`, the art lane's); one line where each
     card sets its picture (house, store, workplace, library, well, town hall); the height. `Show(...)` keeps its shape, so
     W4's cemetery card compiles unchanged and shows today's shape until it is painted.
   - `VillageMap.cs`: where it works out how to paint a house, a store, a workplace, the library, a well and the hall, the
     inputs go through one small helper the card calls too — one table, not two copies that drift (D596).

## 5. Build (after his calls)

Not started. Probe line `portrait:` (a painted card's picture has the map's pieces; a site draws as the map's site), red-checked.
