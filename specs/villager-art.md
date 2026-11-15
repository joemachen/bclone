# The villagers drawn — people seen straight down, their trade, age, load and state on the map (A90)

> **Status: ✍️ specced and mocked (A90, 2026-11-15) — ⏸️ four calls Joe's (§6); nothing built.** Joe: *"i want to work on
> villagers for the next art piece. And then the UI"* — the art lane's order: **the villagers, then the parchment-and-wood
> interface.** Mocked on copies of his saves, the buildings' way (A85): `D:\Projects\brushes\villagers\` —
> `villagers_options.jpg` (four scenes × today / A / B / C), `villagers_catalogue.jpg` (every trade, age and state at three
> zooms, on grass and on snow), `villagers_design.jpg` (the same drawn large, to show the parts).
> Owner: Joe + Claude Code · art lane (`art`, A-series) · Non-negotiable §1.1 (legibility) is the bar this is judged by.
>
> Neighbours: `building-roofs.md` (A85, straight down, lit from the upper left, turned freely), `building-yards.md` (A86,
> pen and wash; A87's founders' cart), `trades-visibly-work.md` (*"you read work from where a person stands"*, D384),
> `handcart.md §8` (the pushed cart), `clothing.md` (the coat, D659–D667), `the-cards.md §2a` (no portraits, D431),
> `school-and-education.md` (children at school, coats from five, D767). Style source: Joe's
> `art/concept art/09. Villager lineup.jpg` and `10. Props and work-site dressing.jpg` — **style only (A80)**: they are
> drawn standing, at an angle; on the map everyone is seen from above (A31).

---

## 1. What and why

Every villager on the map is a dot. Its colour says one thing — child, adult or elder — and nothing else: a farmer, a
smith and a labourer are the same dark disc, and a load of logs, a coat and a walk home to get warm all look the same.
The village has been painted (the ground, trees, paths, snow, the buildings and their yards); the people are the last
flat marks in it, and they are the subject of the game (§1.4, *people, not spreadsheets*).

**The bar (§1.1):** at play zoom a player can tell apart, without opening a card,

1. **the job** — what this person does;
2. **the age** — child, adult or elder;
3. **carrying** — whether they hold a load, and roughly what;
4. **the state** — out working, walking home, cold and going in, in a coat.

## 2. Today, read off the code (`main` = `art` at 85c1349)

| What | How it is drawn | Where |
|---|---|---|
| A villager | a filled disc, radius `max(3 px, 0.2 tile)` — 9.6 px at the closest zoom (48 px a tile), 6.1 at 30, 3.9 at 19 | `VillageMap.DrawVillagers`, `VillagerRadius` |
| Age | the disc's colour: adult `#454135`, child `#02465f`, elder `#5d3900` | `AdultColour` … `ElderColour` |
| Job, load, coat, tool | **not drawn** — the sim knows each (`WorkplaceId` → `Workplace.Kind`; `Carried`; `CoatDays > 0`; `ToolUses`, `ToolGood`) | — |
| State | **not drawn, on purpose** (`trades-visibly-work.md §1`): you read work from *where* a person stands and for how long | — |
| A crowd | fanned apart on a shared tile by rank, gliding (D525) | `FanTargets` |
| At home | **drawn on the roof, at the house's centre — a family of four is one dot** (they share the point) | `DrawnCentre` |
| Pushing a cart | a small rectangle in `HandcartColour` behind the dot, against the walk | `DrawTheCartBehind` |
| Eating | a bowl arc above the dot for 0.5 s (D531) | `DrawTheMealMark` |
| Walking in (nomads) | a chevron, bobbing and fading (D573) | `DrawTheArrivalMark` |
| Selected | a violet ring `#59238e` | `SelectedRing` |

**The trade colours already exist** and are the game's vocabulary: `TradeGlyph.ColourOf` gives every trade a colour, the
Professions panel uses it, and `BuildingGlyph` borrows it so *"a hut and the person who works it read as the same colour,
which is one fact rather than two."* ⚠️ **The teacher has none** (D752 added the trade; `ColourOf` falls through to the
modder's drab grey).

**Seen on Joe's saves:** at a year's turn the village is mostly indoors — Hollowmere's `year-85.save` (winter) has 46 of
55 on screen resting at home, drawn as single dots on the roofs; `year-63.save` (summer, a working tick) 23 of 47.

## 3. The three looks mocked

Each mock hides the villagers in a render of Joe's village (a scratch hook, never committed, dumped every villager's
drawn centre, radius and sim state) and paints them back from that dump. Every figure faces the way its villager walks
(the sim's leg), so it turns freely like the buildings; the light is the valley's, from the upper left.

| | Look | What says the job | What says the age | Load | Coat |
|---|---|---|---|---|---|
| **A** | **Painted token** — today's disc, filled in the trade's colour, inked | the disc's colour | size (a child 0.7×); an elder's brown centre | a square chip in the good's colour, ahead of them | a moss ring round the disc |
| **B** | **Painted figure** — a person from above: shoulders in the trade's colour, the hat by trade (concept sheet 09: the farmer's straw hat, the fisher's grey brim, the forager's white coif, the hunter's and forester's hoods, the builder's leather cap, the miller's and baker's white caps…), the tool in the hand while working, a soft shadow lower right, ink outline | tunic colour + hat + the tool at work | a child 0.7× and bareheaded in today's child teal; an elder hooded in today's elder brown, with a stick | carried in front, in the good's colour and its shape from sheet 10 (logs, a bundle, a sack, a basket, fish, a block) | a moss mantle over the back of the shoulders; the tunic still shows in front |
| **C** | **Figure in homespun** — B's figure, but everyone in undyed and plant-dyed cloth, as the concept's lineup | the hat, the tool and the load only | as B | as B | as B |

**Measured (`measure.py`, the buildings' threshold, A85):** each trade's adult walking, no load, no coat, drawn at its
size and composited on the summer grass; two trades *look alike* when the mean CIE76 ΔE over the pixels either covers is
under 10 (C's two samples a trade, since its cloth varies by person; the closer pair counts).

| 19 trades, 171 pairs | today | A | B | C |
|---|---|---|---|---|
| alike at 30 px a tile (middle zoom) | **171** (one colour) | 5 | **4** | 34 |
| alike at 19 px a tile (far) | 171 | 9 | **8** | 41 |
| trades sinking into the grass (mean ΔE < 15 from it) | — | 0 | 0 | 0 |

B's four at 30 px: woodcutter/carpenter, and the labourer against the smith, the quarrier and the tailor (bareheaded,
greyish cloth). C's 34 are mostly the bareheaded trades (woodcutter, smith, tailor, carpenter, labourer) being one
another, and the white caps (miller, baker); they part only when working, by the tool.

## 4. Common to every look (proposed; settled when Joe picks)

- **Straight down, facing the walk, turned freely** (A31) — a standing villager faces down the screen.
- **The load in front, in `GoodsPalette`'s colour**, with a shape per good from sheet 10 — logs, a firewood bundle, a stone
  or iron block, a basket (produce, bread), fish, a haunch, a sheaf (wheat, hay), a sack (flour), a fold (cloth, leather),
  crossed tools. Shown while `Carried` holds anything; the most-held good is drawn.
- **The tool while at work** (B, C) — out to the side: the axe, spade, hammer, pitchfork, rod, bow, pick, crook, peel,
  saw, book, basket. Not while walking home or to a store, where the hands hold the load.
- **Cold, going in** (`SeekingShelter`) — a cold-blue arc over the head, so *"why did she stop work?"* is on the map.
- **The pushed cart, the meal bowl, the arrival chevron, the selection ring** — as today, drawn over the new figure;
  the pushed cart drawn as a cart (A87's wagon, small: wheels, a bed, the load) is part of this piece.
- **Tick off, today's dots byte for byte** — behind *paint the valley in watercolour*, like every painted layer.
- **The 3 px floor stays**: far out the figure shrinks to its colours, as the catalogue's 19 px row shows.

## 5. Not in this piece

Animals (cattle, game — `art-inventory.md §7`), portraits (B12 stands: none), walk cycles or swing animations, lit windows
at night, villagers in the UI (the next piece). New states Phase 6 adds are listed in `art-inventory.md §15` and drawn
in this look when their systems land.

## 6. ⏸️ Joe's calls — in plain words, each with a recommendation

1. **Which look: A, B or C?** *(see `villagers_options.jpg`, then `villagers_catalogue.jpg`)*
   ⭐ **Recommended: B, the painted figure in trade colours.** It is the concept's style (hats, tools, ink, seen from
   above) and keeps the colour each trade already has on its hut and in the Professions panel, so a farmer looks like
   the farm. It confuses 4 trade pairs where C confuses 34. C is the prettiest up close, but most of its people look
   alike until they lift a tool. A is the plainest and nearly as legible, but it is a board-game counter in a painted
   valley.
2. **Size: a third bigger than today's dot?** Today a person is 0.4 of a tile across; the mocks draw B and C at 0.52
   (A at 0.46) — big beside a 2 × 1 house, but a person's hat, tool and load need the pixels. ⭐ **Recommended: yes.**
3. **People indoors: stop drawing them on the roof?** Today a family at home is one dot sitting on its roof (four
   people, one dot), and children at school and the teacher likewise sit on the school. ⭐ **Recommended: not drawn while
   indoors** (at home resting, at school, teaching) — the village empties at night and fills in the morning, which reads
   as a day; the house's and the school's cards already list who is in. The mocks A–C all do this; today's column
   shows the roofs.
4. **The teacher's colour.** The teacher has no trade colour (the drab grey a modder's trade gets). ⭐ **Recommended:**
   the library's blue, deepened (`#4a5ea8`, as mocked) — the school and the books are one fact. It is one line in
   `TradeGlyph.cs`, a systems-lane file: added with the systems lane's agreement, or held in the villager draw until
   then.

**After the calls, the build** (one piece, on `art`): a `VillageMap.Villagers.cs` partial; each figure kind (trade, age,
coat, load, tool) laid once as a small mesh for the zoom and kept by that key, a frame handing each villager one
transform (the rule: nothing derivable rebuilt per frame — A85's roofs are the shape); probe line `painted villagers:`;
the red-check counted; `_Draw` measured against today on Hollowmere; the four checks.

## 7. The mock tools (outside git, `D:\Projects\brushes\villagers\`)

`hook.py` installs and removes the scratch dump (`VillageMap.MockVillagers.cs.txt`); `run_render.sh` and
`render_villagers.gd` open a save in a redirected `APPDATA` and shoot each zoom with and without the villagers;
`figures.py` draws A, B and C; `paint.py`, `sheet.py`, `catalogue.py`, `design.py` make the sheets; `measure.py` the
table in §3.
