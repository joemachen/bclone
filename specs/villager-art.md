# The villagers drawn — people standing up on the map, their trade, age, load and state (A90–A93)

> **Status: ✍️ specced and mocked (A90, 2026-11-15); A, B and C rejected — Joe: *"i hate all of those. make them look like actual people."* — and remocked as **D, standing people (§3a, A91)**; Joe: *"you're heading in the right direction, now let's incorporate a little flavour from some real life examples"* — **E, D with the flavour of medieval illuminated manuscripts (§3b, A92)**; **✅ E built (A93, §8) at his word — *"Yes, build E"* — every §6a call as recommended; played and ✅ MERGED INTO `main` at his word (A94, *"Played it, merge"*, c269441) and pushed (A95, *"Push it"*).** Joe: *"i want to work on
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

## 3a. ⭐ D — actual people (A91, after Joe rejected A, B and C)

Joe, on A–C: *"i hate all of those. make them look like actual people."* A person seen straight down is a hat on two
shoulders, and every version of that read as a token or a beetle. **D draws people standing up**, as the concept lineup
(sheet 09) draws them, small: a head with a face and hair (or the trade's hat), a tunic in the trade's colour pulled
toward undyed cloth, a belt, arms, hose and shoes; the tool in hand at work; the load on the shoulder, on the back or in
the arms; the coat a moss cloak behind and over the shoulders; a shadow on the ground to the lower right. Mocks
(`D:\Projects\brushes\villagers\`): `people_options.jpg` (today vs D, five scenes at three zooms),
`people_closeup.jpg` (2×), `people_large.jpg` (every trade, child, elder, cold and cart, drawn large),
`people_{closest,middle,far}_{grass,snow}.png` (the same at the game's sizes). Tools: `people.py`, `people_sheets.py`,
`people_compare.py`.

- **Facing**: three views from the walk — toward you (and anyone standing at work), away (the back of the head, no
  face), and from the side, mirrored for left; walking people mid-stride.
- **Height**: an adult stands **0.72 of a tile** (45 px at the closest zoom, 27 at the middle, 17 far; never under
  13); a child 0.62 of that with a bigger head; an elder a little stooped, grey-haired, hooded in the elder brown, with a stick.
- **Who**: skin, hair colour and style (short, cropped, tied, long), tunic length and hose are drawn from the villager's
  id, so a person keeps their look for life. ⚠️ **No sex is drawn** — the sim has none (*"villagers have names and no
  sex"*, `SimConfig`), so there are no men's and women's clothes, only people.
- **Cold** (`SeekingShelter`): arms hugged in, two blue shiver marks by the head. **The pushed cart** is a small wheeled
  cart beside or behind them.
- ⚠️ **The view**: the ground and the buildings stay seen from straight above (A31) and the people stand up on them —
  the convention of an illustrated map. It is a departure from A31 for people only, and it is Joe's call (§6a, 1).
- Not measured yet against A85's ΔE threshold: D says the trade by colour *and* hat *and* tool, and the eye reads a
  person's shape before their colour; the measure is rerun if he picks it.

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

Animals (cattle, game — `art-inventory.md §7`), portraits (B12 stands: none), walk cycles or swing animations (now their
own piece: `villager-animation.md`, A129), lit windows
at night, villagers in the UI (the next piece). New states Phase 6 adds are listed in `art-inventory.md §15` and drawn
in this look when their systems land.

## 3b. ⭐ E — D with the manuscripts' flavour (A92)

Joe, on D: *"you're heading in the right direction, now let's incorporate a little flavour from some real life
examples"* — seven pages of medieval illumination he sent (courtly scenes in the Codex Manesse manner, choristers at a
lectern, a crowd in a landscape, a watercolour of three women and a child, paired figures from a medical book, boys at a
game, an Adoration). **E is D's people, with what those pages share:**

- **Long A-line gowns** widening to the hem — ankle for the merchant, the tailor, the teacher and the elders; calf or knee
  for the working trades, over **grey or coloured hose**, in **pointed black shoes**.
- **Pigment colour**: each trade's colour (`TradeGlyph`) pushed toward its pigment — vermilion, ultramarine, verdigris,
  rose, ochre — instead of D's pull toward undyed cloth. **Half of everyone shows sleeves of a second pigment** (a kirtle
  under the gown).
- **Ink**: every shape outlined, **fold lines** drawn down the gown and the cloak, a pale stroke on the lit side (left).
- **Heads**: rolled curly bobs (fair, auburn, ginger, brown, black); **white linen coifs tied under the chin** (the
  forager, miller and baker, and about a fifth of the bareheaded); **hoods with a hanging tail** (forester, hunter,
  elders); **red caps** (builder, carpenter, some others); the merchant's rolled hat with its tail; the teacher's black
  cap and red shoulder cape. **Pink cheeks and dot eyes** at the closest zoom.
- **A slight sway** in every body; **a raised palm** on anyone standing idle, as the pages' people talk.
- **The coat a cloak with a pale fur collar.**

Mocks: `illum_options.jpg` (D vs E, five scenes), `illum_closeup.jpg` (2×), `illum_large.jpg` (every trade, child,
elder, cold, cart, and a row talking), `illum_{closest,middle,far}_{grass,snow}.png`. Tools: `illum.py`,
`people_sheets.py` (`LOOK=illum`), `illum_compare.py`. Still no sex drawn: coifs, curls, gown lengths and colours fall
by the villager's id for everyone.

## 6a. ⏸️ Joe's calls on D (A91, and E, A92) — supersede §6's first two

0. **D or E?** ⭐ Recommended: E — it is D with the pages' flavour he asked for, and the pigments make the trades
   easier to tell apart, not harder.
1. **Standing people on a map seen from above — yes?** ⭐ Recommended: yes. It is the only way a villager at this size
   looks like a person, and old painted maps do exactly this.
2. **The size: 0.72 of a tile tall** (about a third of a 2 × 1 house's length). ⭐ Recommended as mocked.
3. **People indoors not drawn on the roof** (§6, call 3) — still stands; D's mocks do it.
4. **The teacher's colour** (§6, call 4) — still stands; D's mocks use `#4a5ea8`.

## 6. ⏸️ Joe's calls (A90, on A–C) — in plain words, each with a recommendation

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

## 8. ✅ As built (A93) — E, at Joe's word: *"Yes, build E"*

Every §6a call as recommended: **E**; **people standing up** on the top-down map; **0.72 of a tile** tall (never under
11 px); **indoors not drawn** (resting **at home** — A133, §8a: resting in the open before the first house is on the map —, at school, teaching — and not clickable there either); **the teacher in the
library's blue deepened** — ⚠️ held in the painter's own trade table, not in `TradeGlyph.cs` (the systems lane's file),
so the Professions panel's teacher is still the modder's grey: a one-line systems ask, `art-inventory.md §6`.

- **`PersonPainter.cs`** — the mock's painter (`illum.py`) ported line for line: the same parts, order and figure units.
  Godot-free: it fills polygons into its own 4× supersampled buffer and hands back RGBA bytes 110 × 100 for an adult
  (64 px tall), the feet at (54.4, 84.48). A person's look falls from their id through FNV-1a (`Hash01`) — stable across
  runs, so a person keeps their face for life; the mock used CRC-32, so individuals differ from the mock sheets, the
  style does not.
- **`VillageMap.People.cs`** — with the watercolour tick on, `DrawVillagers` hands over to `DrawThePeople`: back to
  front by screen y; each villager's `FigureKey` (id, stage, trade, pose, mirrored, hands, walking, tool out, good, coat,
  cart, cold) painted **once** into a mipmapped texture and kept (CLAUDE.md's rule — the roofs' shape, A85), unused ones
  let go after 600 frames; **at most 4 paintings a frame** — a person over the budget keeps the look they had, or shows
  their dot for a frame. The point the sim glides and the click test asks (`DrawnCentre`) is the person's belt (0.40 of
  their height up from the feet); a click reaches 0.42 of a person's height. The selection is the violet ring laid on
  the ground at their feet; the meal bowl and the arrival chevron sit over their head; the pushed cart is painted with
  them. **Tick off: `DrawVillagers` untouched — the dots to the byte.**
- **The pose** from the sim's leg (`LegTo − LegFrom` on screen): along it more than 0.8 × across → from the side,
  mirrored for the left; else toward or away. At work (the trade's doing-states, `AtWork`) facing us, the tool out; on
  the way to work (`GoingToWork`) walking with the tool; `SeekingShelter` hugging; a good not carried on the shoulder in
  both arms; `Idle` and still, a palm raised.
- **Probe line `painted villagers:`** (`VillageMap.PeopleProbe.cs`): the tick; who is indoors; twelve poses; every trade
  and the labourer in four poses at 110 × 100; the same key the same bytes, another person different; a child 0.62 of an
  adult; walking left 97% the mirror of walking right; the gowns apart; painted once and kept, mipmapped.
  **Red-checked, nine breaks in one run, nine reds** — ⚠️ the child's check scored **zero** as first written (it measured
  a child from an adult's feet; a child is painted on a smaller canvas) and was fixed to measure each from its own feet,
  then red alone.

### 8a. ✅ Resting in the open is not indoors (A133) — Joe, on a new Hollowmere: *"the villagers flashed on for a moment when i unpaused and then disappeared. they arent visible at all."*

**Read his save first:** a per-person log on a copy of his autosave (Day 7, Spring, Year 1; four founders, no house, three
sites waiting on logs) — all four `Resting` for every frame of 150-odd ticks, and the painter drawing **none**. A villager
with nothing to do rests (`Decide` → `Resting`), and before the first house is up they rest **in the open** at the camp —
their card says *"resting in the open — no house yet"*. Call 3's *"indoors not drawn"* was written as `Resting` = indoors,
so a new village with nothing to do vanished entirely (the moment's flash he saw is not explained — no frame of my logs, every tenth over 150-odd ticks, had anyone out of `Resting`).
**Not A130–A132's:** the rule is A93's, and the dots (tick off) always drew them.
**Fixed:** `ShownAsAPerson(state, housed)` — at school and teaching indoors as before; resting indoors only when the
household has a house; `ShownOnTheMap` is the one answer the drawing and the click test ask (the household asked only of
someone resting). Seen in a render of his save: the four founders standing fanned out at their camp.
**Guarded:** `painted villagers:` — resting, housed, indoors; resting with no house drawn; and asked of the new game's own
first founder, set resting for the question and put back. **Red-check: 3 mutants, 3 red** (resting always indoors; the
map asking as if housed; resting never indoors).
- **Measured.** By the gown's colour alone **5 of 171 trade pairs look alike** (ΔE < 10): woodcutter/marketer,
  marketer/carpenter, quarrier/labourer, miner/herdsman, miller/baker (both in linen aprons) — every pair apart by the
  hat or the tool; the probe holds it at 5. **A painting is 3.95 ms** warm in Release (a console bench of the same file),
  **10.5 ms** in the probe (the Debug build, the first paintings JIT-compiled) — four a frame is the budget. **The draw**
  on Hollowmere (the in-game readout, one paused frame each — a rough instrument): summer closest 13.2 ms before, 12.8
  after; winter 14.3, 14.0. No rise seen. ⚠️ Memory: about 58 KB a kept person-look (with mipmaps); a few hundred kept is
  tens of MB — the 600-frame letting-go is what bounds it. ⚠️ *Since A130 the shadow is no longer in a person's painting:
  it is painted once on its own and drawn under them, so they can step and lean above it (`villager-animation.md §8`).*
- **Renders of the build** on Joe's saves: `D:\Projects\brushes\villagers\built_people.jpg` (the game's own drawing, 2×).
- **The four checks** (A93): the suite 2048 passed, 0 failed, 5 skipped, 17 m 10 s; the view build 0 warnings, 0 errors; the probe every line ✅ (no ⛔ anywhere), bar height 151, tile centres ✅, new game ✅, `done.`; the goldens 418 lines, and no change in `src/Bclone.Sim`, `tests/` or `data/`.

## 9. ✍️ Cloaks dyed by household — mocked (A142), ⏸️ Joe's calls

Joe, 2026-11-21: *"clothes should be available in different colors so villagers aren't all green. maybe. what do you think?"*
Today every coat is one moss cloak (`PersonPainter.Coat`, `#56703f`), drawn whenever `CoatDays > 0` — summer too — over the eight
tunic pigments; by mid-game nearly everyone owns a coat and the village is green.

- **Proposed:** the cloak dyed **by household**, from seven muted dyes a valley could make — madder `#9a4a3c`, woad `#4a6488`,
  weld `#b8a050`, walnut `#6e5238`, undyed `#9a948a`, moss `#56703f`, a dull purple `#6c5070` — so a family reads at a glance
  (Non-Negotiable 4: people, not a spreadsheet). View only.
- **Mocked** on his Ravenscar Y33 with a scratch hack (reverted): `D:\Projects\brushes\villagers\cloaks\cloaks_today_vs_household.png`
  — today above, dyed below.
- **To build:** the dye goes on `FigureKey` (a pose is painted once per key, A93), picked in founding order so neighbours differ
  rather than by a hash (nine households and seven dyes collide by chance in the mock).

**⏸️ His calls:** (1) by household (recommended) or by person; (2) the seven dyes, or others; (3) the cloak drawn only in the cold
seasons, or whenever owned as today (recommended: as today — the sim says nothing about wearing it).

