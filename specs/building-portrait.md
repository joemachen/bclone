# The building's picture on its window (A260–A262)

**Status: 🔨 built on `art` (A262), Joe's calls answered — *"Yes, as recommended"*: B, 120, S1, and his OK for `Main.Cards.cs` and `VillageMap.cs`. ✅ Played and merged into `main` at his word (A263, *"Played it, merge"*); pushed at his word (A264).** Art lane, branch `art`.

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

## 5. Built (A262)

- **The card reads the map's own cache** (`VillageMap.PaintedAt`, new `VillageMap.Portrait.cs`): the map paints every building
  every frame wherever the camera is, keyed by where it stands, so the card takes those very pieces — a far building's card
  too. Null when the watercolour tick is off: the map draws rectangles then, and so does the card. ⭐ **This made §4.4's
  `VillageMap.cs` ask smaller than asked:** no paint-inputs helper is needed — the cache *is* the one table. `VillageMap.cs`
  changed in two places only: `SiteShapeOf` is `internal`, and a site's fill fraction is `SiteShown` (one rule the card asks too).
- **`BuildingPortrait.cs`** (moved out of `Main.Cards.cs`, the art lane's now): `Show(...)` unchanged — it resets to the
  rectangle — then `Paint(map, point)` or `Paint(map, world, workplace)`. Scaled by the footprint (0.35 tile each side), centred
  on the building, the yard clipped at the picture's edge; the pen lines laid again for the picture's scale only when the
  map re-lays the building or the picture changes size (held, never dropped — A261's trap); a mill's sails where the map's
  are; the idle and full rings as before. A site: the map's wash, band and edge (`SiteShapeOf`'s colours — orange while its
  ground is uncleared, the demolition's while it comes down), over the painted building when it stands (a repair).
- **`Main.Cards.cs`**: the class moved out; one `Paint` line on the store, workplace, house, library, well and town hall cards;
  the picture 120 tall; the probe line printed from `TheCardsHoldTheirShape` (`Main.cs`'s list is the systems lane's).
- **Not painted, on purpose:** a bridge (painted by its own path, not a roof), the town hall card the probe poses where none
  stands, and any new building whose card calls only `Show` (W4's cemetery) — the rectangle, until it is painted.
- **Probe `portrait:`** (`Main.PortraitProbe.cs`, `VillageMap.PortraitProbe.cs`): a store the map paints shows the map's own
  entry on its card (by reference), 120 tall; the rectangle with the painting off; a site's colours and fill are the map's.
