# UI image assets — the spec sheet

> **Status: ✍️ written on `art` (A98, 2026-11-15) at Joe's ask (*"write up a spec sheet for the whole list"*); NOTHING
> PAINTED OR BUILT.** The look is `ui-art.md §8` (manuscript scrolls, A97); this sheet is every image that look needs, with
> the size, format, states and drawing rules for each, so each piece can be painted to it and dropped in. Sizes are read off
> the code at `art` 70a7620. The four calls still open in `ui-art.md §8.5` (title face, roll-up, room) do not change any
> asset here except where noted.

## 1. Rules for every asset

### 1.1 How big things get on screen
The game lays out at **1280 × 800 logical pixels** and stretches to the window (`canvas_items`); the **UI-size dial runs
55–115 %** (default 75 %, D305). One logical pixel of UI is therefore drawn at:

| Case | Physical px per logical px |
|---|---|
| Smallest: a 1280-wide window, dial at 55 % | **0.55** |
| Joe today: 1920 × 1080, 75 % | **≈ 1.0** |
| Largest: 3840-wide (4K), dial at 115 % | **3.45** |

So a 14-logical icon lands between **8 and 48 physical pixels**. **Every master is painted at 4 × its logical size or
more**, and every icon must still read at 8 px (§1.3).

### 1.2 Format
- **PNG, RGBA, sRGB, straight (unpremultiplied) alpha**, transparent background unless the sheet says otherwise.
- **Imported lossless with mipmaps and linear filtering**; the `.import` sidecar is committed beside each file (HANDOFF
  🅿️ trap 2), or the other checkout imports it with defaults.
- **Files under `src/Bclone.Game/textures/ui/<set>/<name>.png`** — `textures/` is the art lane's. Names are lower-case
  with hyphens, matching the game's own word (`hunters-lodge.png`, `iron-tools.png`).
- **The layered master** (PSD or similar) is kept outside git with the other art sources, as the ground tiles are.

### 1.3 Legibility (Non-Negotiable §1.1)
1. **Silhouette first.** Each icon is identified by its outline at 8 px with its colour removed. Within a set no two
   silhouettes are alike (A90's lesson: today's villager dots were alike in 171 of 171 trade pairs at 30 px a tile).
2. **Colour is never the only cue** — a state is a mark or a change of form as well as a tint.
3. **Inked.** Everything that sits on parchment has an ink outline, `Ink #2a2018`, about 1/14 of the icon's side at
   master size (4 px on a 56 px icon), slightly irregular, a pen not a vector stroke (D637's rule for things with an area).
4. **Values.** Fills sit in the mid band (L\* 30–75) so the ink line and the parchment (L\* ≈ 86) both separate from them;
   nothing in an icon is as light as the parchment unless it is outlined.
5. **Light from the upper left**, as on the map (A77, A85): a lit edge upper left, shade lower right, no cast shadow on
   icons (a shadow reads as a second shape at 8 px).

### 1.4 The palette (`ui-art.md §8.3`; the map's palette is D637's)

| Name | Hex | Use |
|---|---|---|
| Ink | `#2a2018` | outlines, text |
| Ink, muted | `#5c4833` | secondary text, faint marks |
| Rubric | `#9b2d1f` | headings, held, warnings, deaths |
| Verdigris (ok) | `#2f5a28` | working, met (darkened from A97's `#3b6a33` for 4.5 : 1 over the washes) |
| Gold leaf | `#c4983e` | the illuminated bar, owed gifts, highlights |
| Lapis | `#30488c` | the illuminated bar, born |
| Parchment | `#ecdeba` | the sheet (mottled ±10) |
| Roll | `#decca0` | the rolled paper |
| Washes | tea `#aa783c`, ochre `#b48c46`, green `#788c5a`, umber `#966e50` | watercolour, 10–22 % opacity, pooled rims |

Goods, buildings and trades keep **their own world colours** (`GoodsPalette`, `VillageMap.ColourOf`, `TradeGlyph`) so a
thing looks the same on the map and in the UI; the painted icon takes that colour as its main fill.

### 1.5 9-patches
A 9-patch is one image whose corners stay fixed and whose edges and centre stretch. **The margins below are in master
pixels at 2×** (the mocks' `scroll/margins.json` doubled). **Nothing that must not stretch goes outside the corners** —
curls, studs, seals, sprigs. Edges stretch along their length only, so a stroke along an edge is fine and a stroke across
it smears. The centre stretches both ways: washes there are soft and large on purpose. ⚠️ A 2× texture needs the build's
small custom StyleBox to draw it at half (Godot's `StyleBoxTexture` draws texture pixels 1 : 1); noted for the build,
`ui-art.md §8.6`.

## 2. The scroll chrome

| File | Master size | Margins L/T/R/B (master px) | What it is | States |
|---|---|---|---|---|
| `chrome/window.png` | 720 × 840 | 40 / 44 / 40 / 44 | **The hanging scroll**: a roll across the top and the bottom (Ø ≈ 32), curled, hatched ends that stand out ~16 px past the sheet; the sheet between, inked hand-drawn sides, a handled darker tone at its edges, the rolls' shadow on it, washes in the centre. Every window and card. | — |
| `chrome/window-selected.png` | 720 × 840 | 56 / 44 / 40 / 44 | The same, with an **illuminated bar** down the left: gold leaf, lapis, a rubric pinstripe, about 18 px wide in all; vine sprouts only in its corners. The card being worked with. | — |
| `chrome/strip.png` | 1800 × 240 | 56 / 20 / 56 / 16 | **A scroll on its side**: rolls at the left and right ends (curls top and bottom), the sheet between with inked top and bottom edges. The build bar, the two top bars, short and folded windows. | — |
| `chrome/title-label.png` | 400 × 68 | 28 / 16 / 28 / 16 | **A small scroll label** with curled ends, the style frame's *VILLAGE MENU* plate; window titles sit on it. | normal, hover (a shade warmer) |
| `chrome/roll.png` | 720 × 40 | — | **The loose roll**: the bottom roll alone, drawn by the roll-up animation as it climbs; the code fattens it to 1.35× height as it winds. Same ends as the window's. | — |
| `chrome/modal.png` | 1200 × 900 | 60 / 60 / 60 / 60 | **A large sheet** for the moment pop-up, the passing banner, pause and settings: rolls top and bottom, heavier. | — |
| `chrome/slip.png` | 240 × 120 | 20 / 16 / 20 / 16 | **A torn slip**, no rolls: tooltips and drop-down menus. | — |
| `chrome/wash-1.png` … `wash-4.png` | 512 × 512 | — | **Washes on their own**, optional: tea, ochre, green and umber blots with pooled rims on transparent, so each window can carry a different stain (placed by a hash of the window's name, never by chance). | — |

## 3. Controls

| File | Logical | Master | What it is | States |
|---|---|---|---|---|
| `controls/checkbox.png` | 14 × 14 | 56 × 56 | An inked square, slightly uneven. **Ticked:** a quick pen tick in ink running past the box. | empty, ticked, disabled (both at 45 %) |
| `controls/dropdown-arrow.png` | 10 × 6 | 40 × 24 | A small inked triangle, pointing down. | — |
| `controls/scroll-track.png` | 6 × any | 24 × 96, 9-patch 0/12/0/12 | A faint inked groove. | — |
| `controls/scroll-grabber.png` | 6 × any | 24 × 96, 9-patch 0/12/0/12 | A short wound roll, seen end-on at both ends. | normal, hover, held |
| `controls/bar-track.png` | any × 4 | 128 × 16, 9-patch 8/0/8/0 | A faint ruled line. | — |
| `controls/bar-fill.png` | any × 4 | 128 × 16, 9-patch 8/0/8/0 | **A single wash stroke**, dry-brushed at its end. Tinted in code: verdigris working, rubric warning. | working, warning |
| `controls/pill.png` | any × 16 | 96 × 64, 9-patch 28/0/28/0 | A rounded wash lozenge with an ink edge: *Fed*, *Warm*, *Housed*. | met (verdigris), unmet (rubric) |
| `controls/light.png` | 8 × 8 | 32 × 32 | A small round wash, inked: a card's working light. | working, stopped |
| `controls/rule-h.png` / `rule-v.png` | any × 2 | 256 × 8, 9-patch 16/0/16/0 | **A pen stroke** that thickens and tapers at its ends. | — |
| `controls/underline.png` | any × 3 | 256 × 12, 9-patch 24/0/24/0 | **A rubric swash** under a held button. | — |
| `controls/grip.png` | 10 × 14 | 40 × 56 | Two columns of three ink dots (today's `⋮⋮`). | normal, hover |
| `controls/pin.png` | 14 × 14 | 56 × 56 | A pushpin seen from above (today's `⌖`). | normal, hover, pinned (rubric head) |
| `controls/close.png` | 14 × 14 | 56 × 56 | A pen cross (today's `✕`). | normal, hover (rubric) |
| `controls/find.png` | 14 × 14 | 56 × 56 | A ringed eye, or a magnifying lens. | normal, hover |
| `controls/fold.png` / `unfold.png` | 14 × 14 | 56 × 56 | A tiny roll with an arrow: winding up / unwinding. | normal, hover |
| `controls/plus.png` / `minus.png` | 12 × 12 | 48 × 48 | Pen plus and minus (the Professions steppers). | normal, hover, disabled |

## 4. Icons

**All icons: 14 × 14 logical, master 64 × 64 with 4 px clear on every side (the drawing in 56 × 56), on transparent.**
They sit straight on the parchment — ⏸️ **D637's parchment chip under pictures of world things was for slate panels; on
a parchment sheet it is redundant, so recommended dropped** (Joe's to confirm when the build starts). States are separate
files named `<name>-<state>.png`.

### 4.1 Goods (16) — `icons/goods/`
⚠️ Today a goods row shows a 10-logical colour chip. Icons at 14 widen the top bar and the stock rows a little (a layout
change in the seam). Fill colour from `GoodsPalette`.

| File | Draw |
|---|---|
| `forage.png` | a bowl of berries and greens |
| `logs.png` | three logs, ends showing rings (the banner's logs) |
| `firewood.png` | a bundle of split sticks tied with cord |
| `stone.png` | three cut blocks stacked (the banner's stone) |
| `stone-tools.png` | a stone axe head on a haft |
| `iron.png` | a lump of ore, rust-brown with a dark seam |
| `fish.png` | one fish, head left |
| `meat.png` | a haunch on the bone |
| `leather.png` | a stretched hide |
| `wheat.png` | a standing sheaf, tied (the banner's sheaf) |
| `iron-tools.png` | an iron axe head, grey-blue, on a haft — must not read as `stone-tools` (form: a socket and a bright edge) |
| `flour.png` | a tied sack, dusted white at the neck |
| `bread.png` | a round loaf, scored |
| `hay.png` | a loose, low stook — must not read as `wheat` (no ears, no tie) |
| `clothing.png` | a folded coat (the moss mantle, A93) |
| `handcarts.png` | a two-wheeled handcart, from the side |

### 4.2 Buildings (29) — `icons/buildings/`
**Two files each:** normal, and **owed** (a gift waiting — today's amber *Stone bridge*, *School*: a gold-leaf ring
behind). **Held** is the bar's rubric underline, no file. **No locked state:** the bar offers nothing the village has not
learned (D502, no spoilers); the tech tree draws its own locked cards (§5). **58 files.** Seen as a small
three-quarter vignette or straight down? ⏸️ **The map draws buildings straight down (A31); the icons may be drawn as a
little elevation, as the style frame's are** — recommended: elevation (a front view reads at 8 px; a roof from above
does not), a call for Joe. Fill colour from the building's map colour.

| File | Draw |
|---|---|
| `granary.png` | a raised store on staddle stones |
| `warehouse.png` | a long barn-door shed |
| `market.png` | a striped awning over a stall |
| `stockpile.png` | crates and a sack under a lean-to |
| `woodcutters-hut.png` | a hut with an axe in a block |
| `builders-hut.png` | a hut with a ladder and mallet |
| `foragers-hut.png` | a hut with a basket |
| `foresters-hut.png` | a hut with a sapling |
| `farmhouse.png` | a house with a field's furrows in front |
| `library.png` | a gabled hall with an open book over the door |
| `town-hall.png` | a hall with a small bell tower |
| `fishing-hut.png` | a hut on stilts with a net |
| `hunters-lodge.png` | a lodge with antlers over the door |
| `smithy.png` | a forge with an anvil, a red glow |
| `well.png` | a well with its winch and bucket |
| `quarry.png` | a cut rock face with a pick |
| `iron-mine.png` | a timbered adit in a rock face |
| `mill.png` | a mill with its sails (or wheel by water) |
| `bakery.png` | a house with a round oven mouth |
| `wooden-bridge.png` | a timber span |
| `stone-bridge.png` | a stone arch |
| `barn.png` | a big-doored barn |
| `trading-post.png` | a jetty post with a bale |
| `tailors-shop.png` | a shop with shears on its sign |
| `workshop.png` | a shed with a cartwheel (the carpenter, D678) |
| `school.png` | a small house with a slate and a bell |
| `cabin.png` | a log cabin (home tier 1) |
| `cottage.png` | a stone cottage (tier 2) |
| `manor.png` | a manor (tier 3) |

### 4.3 Trades (18) — `icons/trades/`
**What each carries, not who**: the trade's tool or yield, in its trade colour (`TradeGlyph`; the teacher's colour is still
the systems lane's one line, A93). Shown on the Professions table, and may join the cards and the roster.

| File | Draw | File | Draw |
|---|---|---|---|
| `forager.png` | a basket of berries | `smith.png` | hammer on anvil |
| `forester.png` | a sapling and trowel | `quarrier.png` | a pick on a block |
| `woodcutter.png` | an axe | `miner.png` | a pick and a lamp |
| `marketer.png` | a hand cart of wares | `miller.png` | a millstone |
| `builder.png` | a mallet and a square | `baker.png` | a peel with a loaf |
| `farmer.png` | a sickle | `herdsman.png` | a crook |
| `fisher.png` | a fish on a line | `merchant.png` | a balance |
| `hunter.png` | a bow | `tailor.png` | shears and thread |
| `carpenter.png` | a cartwheel | `teacher.png` | a slate and stylus |

### 4.4 Tools (12) — `icons/tools/`
Same frame as the icons; drawn in ink with one rubric or gold accent, since they are actions, not things.

`paint-land.png` a brush laying a wash · `move.png` four arrows · `demolish.png` a mallet striking · `take-back.png` an
eraser on a stroke · `empty.png` a tipped crate · `destroy.png` a pen cross through a roof · `harvest-trees.png` an axe in
a trunk · `harvest-stone.png` a pick in rock · `harvest-iron.png` a pick in ore · `harvest-all.png` the three, small ·
`unmark.png` a struck-through ring · `cancel.png` a pen cross.

### 4.5 Seasons, alerts, moments — `icons/time/`, `icons/alerts/`, `icons/moments/`
| File | Logical | Draw |
|---|---|---|
| `spring.png` · `summer.png` · `autumn.png` · `winter.png` | 14 | a budding twig · a sun · a falling leaf · a snow crystal (A77's) |
| `warning.png` | 14 | a rubric triangle with an ink mark (replaces the font's ⚠) |
| `death.png` | 14 | a small cross over a mound (replaces ⛔ in the log) |
| `shortage.png` | 14 | an empty, tipped bowl |
| `nomads.png` | 26 (master 104) | figures with bundles walking |
| `bridge.png` | 26 (master 104) | an arch over water |
⚠️ One more moment icon each time the systems lane adds a moment kind.

### 4.6 Optional (words work today; the call is Joe's when the bar is built)
`time/pause.png`, `time/speed-1.png` … `speed-20.png` (pips), `time/skip-year.png`, `time/skip-ten.png`;
`tools/brush-square.png`, `tools/brush-round.png`.

## 5. Screens

| File | Master | What it is |
|---|---|---|
| `screens/title-band.png` | 9-patch, 760 × 2160, margins 60/80/60/80 | a long hanging scroll down the left of the title screen |
| `screens/seal.png` | 512 × 512 | **a wax seal, blank** (the name is a placeholder) — lumpy edge, a pressed ring, lit upper left |
| `screens/new-game.png` | 9-patch as `chrome/modal.png` | the new-game screen's sheet behind its controls (today a flat dark fill) |
| `screens/tech-card-known.png` · `-fogged.png` · `-locked.png` | 9-patch 352 × 320, margins 24 | the tech tree's cards: known in its building's colour wash, fogged as a grey silhouette (`tech-tree-map.md §3.6`), locked inked only |
| `icon.svg` + `icon.ico` (16, 32, 48, 256) | 1024 × 1024 source | **the app and window icon** — today the Godot logo; replaces `src/Bclone.Game/icon.svg` (the art lane's, HANDOFF's lane table). ⏸️ Waits on the name, or uses the blank seal until then |
| `screens/splash.png` | 1920 × 1080 | the boot splash on parchment (today Godot's) — `project.godot`'s `boot_splash` |

## 6. Later, each its own mock first (not specced here)
A wordmark (waits on the name) · a round inked minimap frame and a compass rose · a ribbon banner for the village name ·
an illuminated-initial frame for card titles · marginal vines and sprigs (2–4 corner pieces) · cursors (brush, demolish,
harvest, move) · log category marks · the town hall's collections (every crop, tree, fish, animal, technique, building and
founder — counted when `town-hall.md`'s slices 2–4 are specced).

## 7. Count

| Set | Files |
|---|---|
| Chrome | 7 (+ 4 optional washes) |
| Controls | 16 pieces, ~30 files with states |
| Goods | 16 |
| Buildings | 29 × 2 states = 58 |
| Trades | 18 |
| Tools | 12 |
| Seasons, alerts, moments | 9 |
| Screens | 8 (+ the `.ico`) |
| **Total** | **≈ 160 files**, of which the icons are 113 |
| Optional | ~10 |

## 8. Who draws, and the order
- **The procedural mocks are a start, not the art**: `D:\Projects\brushes\ui\scrolls.py` paints the chrome; Joe or the
  lane paints the masters to this sheet, as the ground tiles and tree atlas were (A13, A30).
- **Recommended order:** the chrome and controls first (the look comes from them, and the seam can ship with them alone);
  then goods, buildings and trades (they replace code-drawn glyphs in `GoodsPalette.cs`, `BuildingGlyph.cs` and
  `TradeGlyph.cs`, which are the systems lane's files — the same seam, after water); then tools, seasons, alerts and
  moments; then the screens.
- **Each set lands with a probe line** that every file exists, imports, and has its size (A85's *painted buildings:*
  pattern), red-checked.
