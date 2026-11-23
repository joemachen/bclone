# W3's brewery and tavern, drawn as real buildings (A199)

> **Status: 🔨 BUILT ON `art` (A200, 2026-11-23) — Joe: *"All as recommended"* on §6's six calls; ✅ MERGED INTO `main` AT HIS WORD
> — *"merge it"* (A201, merge `98dc56b`); not pushed. Specced and mocked A199.** Art lane
> (CLAUDE.md *Two tracks*), branch `art`. View only: nothing in `src/Kith.Sim`, `data/` or `tests/` changes. Mock:
> `D:\Projects\brushes\tavern\buildings\w3_buildings_mock.jpg` (script `mock.py` beside it, the A86 yards painter).

## 1. What was asked

Joe, via the coordinator, 2026-11-23: *"art: tavern and brewery real buildings"*. W3's request (`tavern.md §9`, HANDOFF's
⏸️ *ART LANE — W3'S TAVERN AND BREWERY*): the **brewery**, *a workshop with a copper and a chimney*; the **tavern**, *a homely
hall with a sign, lit windows while anybody sits inside*. Their UI icons are in (A181–A183). **Not in this piece:** the
keeper's cask in the arms and the brewer's and keeper's figures (still asked, still stand-ins in `PersonPainter`).

## 2. What the map draws today

- **Watercolour on:** `RoofOf(JobKind)` gives each a roof and a chimney (`VillageMap.Roofs.cs`: the brewery tile, the tavern
  thatch), and **no yard** — so each is one roof over its whole drawn footprint, the generic workshop look.
- **Colour:** `VillageMap.ColourOf(JobKind)` has no row for either, so both fall to `MarketColour` (`#c98f4a`) — the
  market's orange, on and off the watercolour. Their trades' own colours exist (`TradeGlyph`: brewer `#9a5e14`, keeper
  `#8a2f3e`, A182, taken on W3 at D845) and the icons wear them.
- **Smoke:** neither chimney ever smokes. The brewery burns no firewood (D836–D837), so that is true. The tavern **is
  wrong**: the sim burns the school's stove there in winter while a keeper is seated and its pile holds wood
  (`HearthSystem`, `ShelterAt`, `tavern.md §5.4b`), and the view's `FireLitAt` knows only the school.
- **Indoors:** `VillageMap.Indoors` already hides drinkers (`AtTheTavern`) and the keeper (`KeepingTheTavern`) once they
  reach the door, with the comment *"inside, the lit window the art lane draws"*.

## 3. The look (mock: `w3_buildings_mock.jpg`)

Both are drawn by A86's yards (`building-yards.md`) — a roof smaller than the footprint, the rest a yard of trodden earth
with the building's things on it, every thing a `RoofPiece` in the cached runs, in pen and wash.

### 3.1 The brewery (2 × 1) — the bakery's layout

- **Roof:** on the door's end, 1.25–1.3 × 0.8 (as the smithy, bakery, tailor); **shingle** (the stand-in's tile is the
  bakery's, and the two sit 12.5 ΔE apart — cousins), one chimney, in the brewer's colour `#9a5e14`.
- **Dressing, always (what it *is*):** the **copper** — a round copper vessel in a ring of brick, its signature as the red
  oven is the bakery's: the one round orange-brown thing on the map; the **mash tun**, an open tub of staves with an iron
  hoop; **empty casks racked** on a timber stillage.
- **While brewing** (`StockReadOf` → *while working*, the bakery's rule — a batch trade keeps no stock of its own, D522; the
  barley comes from a store and the ale goes to one): **the copper open**, the dark amber wort in it and a paddle across,
  where it is otherwise shut under a wooden lid; **barley sacks** (barley's pale straw, `barley.md §7`) by the tun. This is
  `tavern.md §9`'s *"brewing — the building's own look"*.
- **Steam off the open copper while it brews** — call 2. Pale and thin, near white, rising from the copper (not the chimney);
  never the smoke's grey (`VillageMap.Smoke`'s colours), because smoke on this map means firewood burning (A139) and the
  brewery burns none.

### 3.2 The tavern (2 × 2) — the library's layout

- **Roof:** at the back, the front strip a yard; **thatch**, one chimney, in the keeper's colour `#8a2f3e` (22 ΔE from the
  barn's red, which is tile).
- **Dressing, always:** the **sign** — an iron arm out from the front wall beside the door, the board in the keeper's red
  with a gold tankard; a **trestle table with a bench either side**; the door step.
- **Stock (§2 of `building-yards.md`, Joe's rule):** **casks of ale by the door, by level** — the tavern's own
  `Store[Ale]` (`tavern_stock` 20 at most, so levels 1–3: one, two or three casks). Out of ale, none — and the tavern is shut
  then too (`TavernIsOpen`).
- **Lit** — call 3. While **anybody sits inside** — a villager `AtTheTavern` there who has reached the door (the drinkers;
  ⛔ **not the keeper alone**: the keeper is inside all the working day, so the keeper's state would light it from morning to
  night and say nothing) — **lamplight falls out of the windows and the open door onto the ground**: a bright slit at each
  window under the eave, a warm fan of light on the ground before it, front and both sides. It comes and goes over a second,
  as the smoke does (`SmokeFadeSeconds`), never a pop, and is held while paused.
  - ⚠️ The valley has no night: the map is drawn in daylight at every hour. The light has to read on green grass in the day,
    so it is a fan on the ground and a bright slit, not a dark building with lit windows; measured on the mock at both zooms.
- **Smoke in winter** — call 5: the tavern's chimney smokes while its stove burns, by the sim's own rule (winter, a keeper
  seated, firewood in its pile) — the school's line in `FireLitAt`, one building over.

### 3.3 Colour — call 4

The two take their **trades' own colours** (`TradeGlyph.ColourOf`, as the smithy, bakery and the others do since D449) in
place of the market's orange: one row in `VillageMap.ColourOf(JobKind)`. ⚠️ This changes the **plain map** (watercolour off)
for these two kinds as well — their rectangles turn amber-brown and wine red. That is the point (a tavern that draws as a
market is a legibility fault, §1.1), but it is a change outside the painted look, so it is asked.

## 4. Code (after the calls)

| File | Change | Lane |
|---|---|---|
| `VillageMap.Roofs.cs` | `RoofOf(JobKind)`: Brewer → shingle, `Yard: YardKind.Brewery`; TavernKeeper → `Yard: YardKind.Tavern`; the light drawn after the roof's runs, its level carried on `RoofCache` beside `Smoke` | ⛔ call 6 |
| `VillageMap.Yards.cs` | `YardKind.Brewery`, `YardKind.Tavern`; `StockReadOf` (brewery while working; tavern `StockRead.Ale`); the two layouts (the bakery's, the library's); the copper, tun, stillage, sign, trestle, casks | ⛔ call 6 |
| `VillageMap.Smoke.cs` | once a tick beside the fires: the taverns with someone sat inside; `FireLitAt` takes the tavern's stove; the steam (call 2) | ⛔ call 6 |
| `VillageMap.YardsProbe.cs` | the probe lines (§5) | ⛔ call 6 |
| `VillageMap.cs` | one row of `ColourOf(JobKind)` (call 4) | ⛔ call 6 |

Nothing derivable is rebuilt per frame: the yard is laid again only when `YardState` moves (working, the ale level); who is
inside is counted once a tick where the fires are noted; the light's level is a float carried frame to frame on the cache.
No new texture, so no import.

## 5. Probe

On `building yards:` (named from this spec, art traps 17 and 25): the brewery's copper, tun and stillage always; the copper
open with the wort and the paddle and barley sacks only while working, shut without them otherwise; the tavern's sign,
trestle and step always; its casks 0 / 1 / 2 / 3 by `YardLevel` of its ale, none at zero; every thing inside its building's
ground; lit — the light level rises toward 1 with a drinker inside and not with the keeper alone, falls to 0 when they
leave, held while paused; the tavern's fire lit in winter with a keeper and wood, cold without either; the two kinds'
colours their trades'; tick off, nothing of it drawn.

## 6. Joe's calls (⏸️, each recommended)

1. **The look as mocked** — the brewery's copper, mash tun and racked casks, opened while it brews; the tavern's sign, table
   and benches and casks of ale by the door. ⭐ *Recommended.*
2. **Steam off the open copper while it brews** (pale, thin, never the smoke's grey). ⭐ *Recommended* — brewing then shows
   at play zoom, where the open copper alone is a few pixels.
3. **Lamplight while anybody sits inside** (the drinkers, not the keeper alone), coming and going over a second.
   ⭐ *Recommended.*
4. **Their own colours** (amber-brown, wine red) in place of the market's orange, on the plain map too. ⭐ *Recommended.*
5. **The tavern's chimney smokes in winter while its stove burns** (the sim already burns the wood). ⭐ *Recommended.*
6. **His OK for the five map files in §4**, outside the lane's table (the stall's, the trees' and the yards' precedent).
   The systems lane (`slice/library-borrowing`) has touched none of them. ⭐ *Recommended.*

## 7. Not in this piece

The keeper's cask in the arms (*carrying ale*), the brewer's and keeper's figures, roofs in snow, the card's picture.

## 8. As built (A200)

Joe, 2026-11-23: *"All as recommended"* — the six calls of §6 as written.

- **Code:** `VillageMap.Tavern.cs` (new: the two yards, the copper, mash tun, stillage, casks, trestle and sign; the rules
  `LightsTheTavern`, `SteamsTheCopper`, `HasAStove`; who is lit and what steams, noted once a tick; the lamplight and the
  steam drawn each frame at a level carried on the roof's cache) and `VillageMap.TavernProbe.cs` (new: the probe line). At his
  OK (call 6): `VillageMap.Roofs.cs` (the two rows of `RoofOf(JobKind)`; the cache keeps the lamplight out of the runs and
  keeps the copper's middle; the light and steam drawn after the smoke), `VillageMap.Yards.cs` (`YardKind.Brewery`,
  `YardKind.Tavern`, `StockRead.Ale`, the two layouts — the tavern's carries where its door is, `YardLayout.DoorAt`),
  `VillageMap.Smoke.cs` (the two sets cleared and filled where the fires are noted; `FireLitAt` asks `HasAStove`; the puff
  painter takes its colours, so the steam is the smoke's painting near white), `VillageMap.YardsProbe.cs` (§2's table gains
  the two kinds; the new line rides on its return), `VillageMap.cs` (one row of `ColourOf(JobKind)`).
- **The light** is five fans — a window each side of the door, the door, a window in each side wall — from the eave outward,
  `#ffc552` at 0.8 at the wall fading to nothing (`LampReach` 0.45 tile in front, 0.3 at the sides), and a lit slit at each
  window, `#ffd37a`. Drawn with ordinary blending, not added light: a material on the map would shade every draw of it (art
  trap 19). The first strength (0.5) all but vanished on his grass in daylight and was raised after the first renders.
- **Probe** `tavern and brewery:` (riding on `building yards:`, trap 73). Red-check 30 mutants, 29 red, each a ⛔ line on the line it aims at, none a crash; ⚠️ one zero written down: the steam painted in the smoke's own colours at the draw (`TheSteamPuffs`) — the probe paints the steam's colours itself, so the renders are its evidence.
- **Renders** on copies of his Millbrook (year 36), Elderwood (year 33) and Oakhaven (year 41) — the lamplight and an open
  copper **posed** with a scratch switch (never committed), as no autosave was taken with a drinker inside or a batch on:
  `D:\Projects\brushes\tavern\buildings\render\w3_buildings_built.jpg`.
- **Draw-time zeros, written down:** the light's and steam's fade is the smoke's own `SmokeOn` (probed), but whether the frame
  calls it for a tavern and a brewery, and the posed renders, are its evidence; the winter smoke is the rule (`HasAStove`,
  probed) — no winter save with a kept tavern was rendered.
