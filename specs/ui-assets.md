# UI image assets — the spec sheet

> **Status: ✍️ written on `art` (A98, 2026-11-15) at Joe's ask (*"write up a spec sheet for the whole list"*); NOTHING
> PAINTED OR BUILT.** **A99: the list, sizes, margins, format, naming, paths, legibility rules and states are settled and
> followed exactly; the LOOK (§1.4's palette, the scroll forms in §2's descriptions) is reopened — any row a new look makes
> wrong is listed and put to Joe, never changed quietly. Painted in Photoshop by script; Phase 0 (the pipeline) proven.** **A101: Joe picked direction 2, the hung scroll, with thinner edges; icons raised to 20 logical px (§1.1, §4). Rows the hung scroll makes wrong, his to confirm: §2's `window` (one roll at the top, torn sides and foot), `roll` (the top roll itself, fattening as the sheet winds in), `modal` (the same form), and §1.4's Parchment (the painted sheet measures `#ead3a9`).** **A102: §2 and §3 painted — all 49 files in `textures/ui/chrome/` and `textures/ui/controls/`, PSDs outside git; two more rows the look changes, his to confirm: §3's `checkbox` (a wax seal in an inked socket) and `pill` (a ribbon, swallowtail met, frayed unmet). Not wired; the probe line (§8) comes with the wiring.** **A106: Joe — *"all yes and b"*: the six rows above are now the art as painted (§1.4 Parchment, §2 `window`, `roll`, `modal`, §3 `checkbox`, `pill`), and the owed mark is a corner medallion (§4.2), repainted.** **A103: §4 painted — all 113 icons in `textures/ui/icons/<set>/`, at 20 logical (master 80; moments 104), no shared 8 px silhouette in any set; the school and the teacher borrow `#4a5ea8` (no colour in code), hay keeps `#b3a05c`. ⏸️ The owed ring fills every owed icon's 8 px outline — a corner medallion is the alternative, his call.** **A104: §5 painted — `title-band`, `seal` (blank), `new-game`, `splash` (no name on it), `tech-card-known` (neutral, tinted in code) / `-fogged` / `-locked`, in `textures/ui/screens/`. `icon.svg` / `icon.ico` not made: they wait on the name.** The look is `ui-art.md §8` (manuscript scrolls, A97); this sheet is every image that look needs, with
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

So a 20-logical icon lands between **11 and 69 physical pixels** (A101: raised from 14, which landed at 8–48 and was
a speck on Joe's screen in every Phase 1 direction). **Every master is painted at 4 × its logical size or more**, and
every icon must still read at 8 px (§1.3) — the test stays stricter than the smallest real case.

### 1.2 Format
- **PNG, RGBA, sRGB, straight (unpremultiplied) alpha**, transparent background unless the sheet says otherwise.
- **Imported lossless with mipmaps and linear filtering**; the `.import` sidecar is committed beside each file (HANDOFF
  🅿️ trap 2), or the other checkout imports it with defaults.
- **Files under `src/Kith.Game/textures/ui/<set>/<name>.png`** — `textures/` is the art lane's. Names are lower-case
  with hyphens, matching the game's own word (`hunters-lodge.png`, `iron-tools.png`).
- **The layered master** (PSD or similar) is kept outside git with the other art sources, as the ground tiles are.

### 1.3 Legibility (Non-Negotiable §1.1)
1. **Silhouette first.** Each icon is identified by its outline at 8 px with its colour removed. Within a set no two
   silhouettes are alike (A90's lesson: today's villager dots were alike in 171 of 171 trade pairs at 30 px a tile).
2. **Colour is never the only cue** — a state is a mark or a change of form as well as a tint.
3. **Inked.** Everything that sits on parchment has an ink outline, `Ink #2a2018`, about 1/14 of the icon's side at
   master size (5 px on a 70 px icon), slightly irregular, a pen not a vector stroke (D637's rule for things with an area).
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
| Parchment | `#ead3a9` | the sheet (mottled ±10) — as painted and measured (A101; A97's `#ecdeba` before it) |
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

## 2. The chrome — parchment, no scrolls (A114), an inked edge (A116)

*Joe, 2026-11-17, shown his own reference beside the game: *"yes, repaint them like the first one"*. **Every sheet's edge is inked, not scorched** (`p2_chrome.jsx`'s `inked` sheets): a crisp dark ink line (`#1e140c` at 92 %), fine tears (a nick every 4 master px, ±1), the paper greying softly in the last few px before the line, and **no** orange wash rim and **no** burnt stroke. The sheets, the band and the slip all wear it.*

*A124 — Joe: *"the texture is so big and blobby. looks very amateur ... the concept style attached ... is literally perfect"*. **The paper is his concept's** (`ui-art.md §9.9`): A117's grey stains are gone; a fine fibre grain and a faint shapeless tone, and a warm darkening toward the torn edge (`#8e5a2a` multiply), held to ~77 L* where the words begin. Measured against his concept at the screen's scale (`D:\Projects\brushes\ui\grain\measure_grain.py`). The paragraphs below are the record.*

*Then, A118 — Joe: *"the edges dont look the same. remove the grey margin/padding whatever it is and increase the stroke darkness of the ink. maybe muddle it a bit like we do for paths?"* **No grey inside the edge at all.** The ink line is near-black (`#140c06` at 100 %) in the dry-brush tip, so its darkness breaks along its length, with a second dry-brush stroke (`#2a1a0e` at 70 %) that drifts up to 2 master px on and off it: the paths' wobbled, mottled rim, in ink. The shadow is a tight dark line under the sheet (2/3 px, blur 2), not a soft grey halo.*

*Joe, the same day: *"i want that too"* — **the paper weathered like his reference** (A117): a cooler base (`#f2e6d0`) and fewer, bolder grey-brown stains (`#7e705c` at 24 %), measured against his image. The middle tone is 217–221 against his 224, the spread 10–11 against 11–14, and the darkest tenth about (209, 187, 153) against his (209, 180, 138). ⚠️ **His heavy darkening under the words was not copied:** on it the coloured inks fail 4.5 : 1 unless they turn near-black. The paper's weathering darkens where words never sit, and the text inks were darkened once more for the stains (`ui-art.md §9.8`).*

*Joe, 2026-11-17: *"For the UI: i hate the scrolls. keep the parchment and remove the scrolls."* The hung scroll's roll, the strip's rolled ends, the modal's roll, the scroll-shaped title label, the roll alone (for the roll-up) and the title screen's hanging scroll are **retired** — their files removed from `textures/ui/`, their Photoshop masters kept in `D:\Projects\brushes\ui\psd\p2\chrome\`. The same sheets were repainted without rolls by the same script (`p2_chrome.jsx`: `sheet`, `sheet-selected`, `band`, `sheet-large`).*

| File | Master size | Margins L/T/R/B (master px) | What it is | States |
|---|---|---|---|---|
| `chrome/sheet.png` | 720 × 840 | 40 / 40 / 40 / 44 | **A torn, lightly scorched parchment sheet**, torn on all four sides, washes in the centre — no roll (A114). Every window and card. | — |
| `chrome/sheet-selected.png` | 720 × 840 | 56 / 40 / 40 / 44 | The same, with an **illuminated bar** down the left: gold leaf, lapis, a rubric pinstripe, about 18 px wide in all; a vine sprout at its foot only. The card being worked with. | — |
| `chrome/band.png` | 4096 × 240 (1800 until A124) | 40 / 20 / 40 / 20 | **A long torn sheet** across the screen — no rolls at its ends (A114). The control bar. Painted near the width it is drawn at 1920 and 75 % (A124), so its grain is not stretched. | — |
| `chrome/sheet-large.png` | 1200 × 900 | 60 / 60 / 60 / 60 | **A large sheet**, the window's form, heavier rim: the moment pop-up, the passing banner, pause and settings, the new-game screen — no roll (A114). | — |
| `chrome/slip.png` | 240 × 120 | 20 / 16 / 20 / 16 | **A torn slip**, no rolls: tooltips and drop-down menus. | — |
| `chrome/slip-long.png` | 1600 × 240 | 20 / 16 / 20 / 16 | **The slip, long** (A124): the top bar's boxes, which drew the small slip ~9 × stretched. | — |
| `chrome/wash-1.png` … `wash-4.png` | 512 × 512 | — | **Washes on their own**, optional: tea, ochre, green and umber blots with pooled rims on transparent, so each window can carry a different stain (placed by a hash of the window's name, never by chance). | — |

## 3. Controls

| File | Logical | Master | What it is | States |
|---|---|---|---|---|
| `controls/checkbox.png` | 14 × 14 | 56 × 56 | An inked ring, slightly uneven — a seal's empty socket. **Ticked:** a red wax seal pressed into it, lit upper left, a pressed ring (A102). | empty, ticked, disabled (both at 45 %) |
| `controls/dropdown-arrow.png` | 10 × 6 | 40 × 24 | A small inked triangle, pointing down. | — |
| `controls/scroll-track.png` | 6 × any | 24 × 96, 9-patch 0/12/0/12 | A faint inked groove. | — |
| `controls/scroll-grabber.png` | 6 × any | 24 × 96, 9-patch 0/12/0/12 | **A slim inked bar** with a tea wash — plain, no roll (A115, Joe: *"Yes, plain grabber"*). | normal, hover (rubric edge), held (a deeper wash) |
| `controls/bar-track.png` | any × 4 | 128 × 16, 9-patch 8/0/8/0 | A faint ruled line. | — |
| `controls/bar-fill.png` | any × 4 | 128 × 16, 9-patch 8/0/8/0 | **A single wash stroke**, dry-brushed at its end. Tinted in code: verdigris working, rubric warning. | working, warning |
| `controls/pill.png` | any × 16 | 96 × 64, 9-patch 28/0/28/0 | **A ribbon** with an ink edge: *Fed*, *Warm*, *Housed*. Met: verdigris, swallowtail ends; unmet: rubric, frayed square ends — a change of form as well as colour (A102). | met (verdigris), unmet (rubric) |
| `controls/light.png` | 8 × 8 | 32 × 32 | A small round wash, inked: a card's working light. | working, stopped |
| `controls/rule-h.png` / `rule-v.png` | any × 2 | 256 × 8, 9-patch 16/0/16/0 | **A pen stroke** that thickens and tapers at its ends. | — |
| `controls/underline.png` | any × 3 | 256 × 12, 9-patch 24/0/24/0 | **A rubric swash** under a held button. | — |
| `controls/grip.png` | 10 × 14 | 40 × 56 | Two columns of three ink dots (today's `⋮⋮`). | normal, hover |
| `controls/pin.png` | 14 × 14 | 56 × 56 | A pushpin seen from above (today's `⌖`). | normal, hover, pinned (rubric head) |
| `controls/close.png` | 14 × 14 | 56 × 56 | A pen cross (today's `✕`). | normal, hover (rubric) |
| `controls/find.png` | 14 × 14 | 56 × 56 | A ringed eye, or a magnifying lens. | normal, hover |
| `controls/fold.png` / `unfold.png` | 14 × 14 | 56 × 56 | **An arrow to or from a pen line** (the sheet's edge): folding up / unfolding — no roll (A115). | normal, hover |
| `controls/plus.png` / `minus.png` | 12 × 12 | 48 × 48 | Pen plus and minus (the Professions steppers). | normal, hover, disabled |

## 4. Icons

**All icons: 20 × 20 logical, master 80 × 80 with 5 px clear on every side (the drawing in 70 × 70), on transparent.**
*(A101, Joe: "yes raise icons to 20 logical px" — from 14 × 14 / 64 × 64. Moments stay 26. Controls — checkbox, pin,
close and the rest of §3 — are not icons and keep their sizes.)*
They sit straight on the parchment — ✅ **D637's parchment chip under pictures of world things was for slate panels; on
a parchment sheet it is redundant, so dropped** (A109, Joe: *"all as recommended"*). States are separate
files named `<name>-<state>.png`.

### 4.1 Goods (16, and barley's, A162) — `icons/goods/`
⚠️ Today a goods row shows a 10-logical colour chip. Icons at 20 widen the top bar and the stock rows, and make each
row taller than today's text line (a layout change in the seam — the build bar's buttons and the top bars grow with it). Fill colour from `GoodsPalette`.

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
| `barley.png` | wheat's sheaf, but the heads hang over its sides, each trailing long awns — must not read as `wheat` (its ears stand upright) — ✅ approved and in (A163, `painted-crops.md §9`); shown once W7's good merges |

| `ale.png` | W3 (A181): a wooden tankard, staved and hooped, a head of foam over its rim and down one side — must not read as `bread` (the nearest colour); ✅ approved and in (A183, *"Yes, merge them"*); shown once W3's good merges |

### 4.2 Buildings (29) — `icons/buildings/`
**Two files each:** normal, and **owed** (a gift waiting — today's amber *Stone bridge*, *School*: **a small gold-leaf
medallion on the building**, at the lowest-rightmost spot where it lies wholly inside the drawing — A106, Joe's *"b"*:
A103's ring behind made every owed icon the same disc at 8 px; a medallion in a corner still added the same cells to every
outline (11 owed pairs alike at the upper right, 6 at the lower); one inside the drawing adds none, so an owed icon has its
normal icon's outline exactly. Positions measured off the normal files, `D:\Projects\brushes\ui\medallions.json`). **Held** is the bar's rubric underline, no file. **No locked state:** the bar offers nothing the village has not
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
| `house.png` | a log cabin (home tier 1; the data's row is *house*) |
| `stone-cottage.png` | a stone cottage (tier 2) |
| `insulated-manor.png` | a manor (tier 3) |

| `brewery.png` (+ `-owed`) | W3 (A181): a workshop in the brewer's colour with a chimney, a copper on its fire beside it, piped into the wall; ✅ approved and in (A183) |
| `tavern.png` (+ `-owed`) | W3 (A181): a homely thatched hall in the keeper's colour, its two windows lit, a sign with a tankard hung from a bracket; ✅ approved and in (A183) |
| `market-stall.png` (+ `-owed`) | W14 (A193, `market-stand.md §8`): one bay of the market — its awning colour, its counter, its produce and loaf — under a peaked, scalloped canopy on two posts, where the market is a long flat awning across the whole icon; the owed medallion on the counter's right end (189, 205), the outline unchanged; ✅ approved and in (A194, *"Yes, merge it"*) |

*Every icon is named after its row's name in the data, lower case, apostrophes dropped, words hyphened (A111) — the
three home tiers were renamed from `cabin`, `cottage` and `manor` so the game finds them with no list of exceptions. A
modder's building is drawn by adding `<its name>.png` here.*

### 4.3 Trades (18) — `icons/trades/`
**What each carries, not who**: the trade's tool or yield, in its trade colour (`TradeGlyph`; the teacher's colour is still
the systems lane's one line, A93). Shown on the Professions table, a person's card and the roster's trade column, the word kept beside it (A109).

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
| `brewer.png` | W3 (A181): a steaming copper and an ear of barley (a mash paddle read as the baker's peel) — ✅ A183 | `tavern-keeper.png` | W3 (A181): the cask the keeper carries in, its tap dripping — ✅ A183 |

✅ **W3's colours (A181; his *"Yes, merge them"*, A183), the systems lane's two lines on `slice/w3-tavern`, as barley's was (D829):** the stand-ins sit too
close — ale `#b07a2a` 8.6 ΔE from bread, the brewer the same 8.6 from the baker, the keeper `#8a3b2e` 9.4 from the herdsman.
Recommended: **ale and the brewer `#9a5e14`** (12.6 from bread and the baker; the brewer wears his good's colour, as the baker
wears bread's) and **the keeper `#8a2f3e`** (22 from the herdsman). The icons above are painted in them
(`D:\Projects\brushes\tavern\w3_icons_mock.jpg`, both sets).

### 4.4 Tools (12) — `icons/tools/`
Same frame as the icons; drawn in ink with one rubric or gold accent, since they are actions, not things.

`paint-land.png` a brush laying a wash · `move.png` four arrows · `demolish.png` a mallet striking · `take-back.png` an
eraser on a stroke · `empty.png` a tipped crate · `destroy.png` a pen cross through a roof · `harvest-trees.png` an axe in
a trunk · `harvest-stone.png` a pick in rock · `harvest-iron.png` a pick in ore · `harvest-all.png` the three, small ·
`unmark.png` a struck-through ring · `cancel.png` a pen cross.

### 4.5 Seasons, alerts, moments — `icons/time/`, `icons/alerts/`, `icons/moments/`
| File | Logical | Draw |
|---|---|---|
| `spring.png` · `summer.png` · `autumn.png` · `winter.png` | 20 | a budding twig · a sun · a falling leaf · a snow crystal (A77's) |
| `warning.png` | 20 | a rubric triangle with an ink mark (replaces the font's ⚠) |
| `death.png` | 20 | a small cross over a mound (replaces ⛔ in the log) |
| `shortage.png` | 20 | an empty, tipped bowl |
| `nomads.png` | 26 (master 104) | figures with bundles walking |
| `bridge.png` | 26 (master 104) | an arch over water |
⚠️ One more moment icon each time the systems lane adds a moment kind.

### 4.6 Optional (words work today; the call is Joe's when the bar is built)
`time/pause.png`, `time/speed-1.png` … `speed-20.png` (pips), `time/skip-year.png`, `time/skip-ten.png`;
`tools/brush-square.png`, `tools/brush-round.png`.

## 5. Screens

| File | Master | What it is |
|---|---|---|
| *(title band)* | — | the title screen's band down the left is `chrome/sheet.png` (A114: the hanging scroll is retired) |
| `screens/seal.png` | 512 × 512 | **a wax seal, blank** (the name is a placeholder) — lumpy edge, a pressed ring, lit upper left |
| *(new game)* | — | the new-game screen's sheet is `chrome/sheet-large.png` (A114: the modal with its roll is retired) |
| `screens/tech-card-known.png` · `-fogged.png` · `-locked.png` | 9-patch 352 × 320, margins 24 | the tech tree's cards: known in its building's colour wash, fogged as a grey silhouette (`tech-tree-map.md §3.6`), locked inked only |
| `icon.svg` + `icon.ico` (16, 32, 48, 256) | 1024 × 1024 source | **the app and window icon** — today the Godot logo; replaces `src/Kith.Game/icon.svg` (the art lane's, HANDOFF's lane table). ⏸️ Waits on the name, or uses the blank seal until then |
| `screens/splash.png` | 1920 × 1080 | the boot splash on parchment (today Godot's) — `project.godot`'s `boot_splash` |

## 6. Later, each its own mock first (not specced here)
A wordmark (waits on the name) · a round inked minimap frame and a compass rose · a ribbon banner for the village name ·
an illuminated-initial frame for card titles · marginal vines and sprigs (2–4 corner pieces) · cursors (brush, demolish,
harvest, move) · log category marks · the town hall's collections (every crop, tree, fish, animal, technique, building and
founder — counted when `town-hall.md`'s slices 2–4 are specced).

## 7. Count

| Set | Files |
|---|---|
| Chrome | 5 (+ 4 optional washes) — A114: 4 sheets and the slip |
| Controls | 16 pieces, ~30 files with states |
| Goods | 16 |
| Buildings | 29 × 2 states = 58 |
| Trades | 18 |
| Tools | 12 |
| Seasons, alerts, moments | 9 |
| Screens | 6 (+ the `.ico`) — A114: the title band and new-game sheet are chrome's |
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
