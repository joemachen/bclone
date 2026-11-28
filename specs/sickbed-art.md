# W6's pictures, piece 1 — a person sick in bed, and the card's health pill (A309)

> **Status: ✅ THE AMBER PILL AND THE THREE FACES BUILT (A314, 2026-11-27), MERGED INTO `main` AT JOE'S WORD (A315, *"Yes, merge"*, `b6a3a40`), PUSHED (A316); THE WARNINGS' TWO FACES (A317, §8a) MERGED (A318, *"Yes, merge"*, `68adfe6`), PUSHED (A319). ✅ SICK ON THE MAP BUILT ON `art` (A321, §10), UNMERGED — Joe: *"Yes, as recommended"*; ⏸️ HIS PLAY. Joe: *"Yes, as recommended"*. ⏸️ Sick on the map and the home strewn wait for W6 piece 1's sick state (§8).** Mocked A310–A313. Claimed A309. Art lane (CLAUDE.md *Two tracks*),
> branch `art`. W6's ask of the lane (`health.md §11`, D774), piece 1 only: the herbalist, herbs, remedy and the infirmary
> (pieces 2 and 3) wait for their own specs. Mocks: `D:/Projects/brushes/sickbed/` (`sickbed_pick.jpg`; `compose.py` lays the
> beds on the real render, `harness/` paints the figure with the game's own `PersonPainter`, `mock_card.py` and `card.sh` render
> the real card from a scratch copy of the game).

## 1. What is there today, and what piece 1 brings

- **Nothing of health is in the sim yet.** W6 piece 1 (`health.md §5`, §9) adds *run down* and **sick in bed**: about once a
  year a run-down person goes home and lies there five days, eats, does no work, then gets up still run down; nobody dies of it.
  Its stand-ins (§11): the house roof (a person at home is not drawn — they fade at the door, `indoors-and-smoke.md §7`) and a
  lying figure from the dead-under-a-cloth pose without the cloth. **No lying figure exists in `PersonPainter`** yet.
- **The card** (`Main.Cards.cs`, the systems lane's): five need pills, *Fed · Warm · Housed · Water · Content*, each in one of two
  painted skins (`ParchmentLook`: `pill_met`, a green ribbon; `pill_unmet`, a red label). Piece 1 adds a sixth: **Well / Plain
  diet / Run down / Sick** (§5.4). With six the row wraps to a second line at the card's width.

## 2. The mock

On a copy of his honey-harrow Hollowmere (it opens on `main` since D933): Vorith the smith at his cabin, at the game's closest
zoom, the copy changed only so he is resting indoors; the figure painted by `PersonPainter` at the map's figure scale.

- **A — at home, seen through the roof.** Over the bed the roof is washed back in a soft oval (the floor's warm paper, a faint
  ring), and inside it the bed side-on, as people are drawn: a low wooden frame, a pillow, him lying on it under a blue wool
  cover with the sheet turned down. True to the sim (he is home), unchanged by snow, and **the bedside piece 2 needs** — the
  herbalist kneeling *at* a bed is this window with a second figure in it; the infirmary is the same window, several beds.
- **B — lying by the door.** A straw pallet on the ground beside the step, the same figure under the same cover. Plain and
  legible, with nothing over the roof; but it puts a sick man outdoors in every season (snow included), where the sim has him
  in his bed.
- **The pill, two ways, on his real card:** **1** as the other needs — *Well* green, the other three the painted red; **2** the
  same, but ***Plain diet* in an amber ribbon** (the red label's painting, hue-turned to amber): it does nothing yet (§5.3), and
  in red it would look like *Cold*, which harms. Red then means *it is biting*, amber *you have a year*.

## 3. Calls — ⏸️ Joe's

1. **The bed:** ⭐ **A** (through the roof) or B (by the door).
2. **The pill:** 1 (two looks) or ⭐ **2** (amber for *Plain diet*).
3. **His OK for the files outside the lane's table:** for the bed, `VillageMap.Indoors.cs` (the sick are indoors, but drawn),
   `VillageMap.cs` (the home painter's hook for the window, and the click test, so clicking the bed opens the card), and a new
   `VillageMap.Sickbed.cs`; `PersonPainter.cs` gains a lying pose. For pill 2: a new painted `pill-warn.png` and one theme line in
   `ParchmentLook.cs` with `UiLook.Pill`'s third look — **`Main.Cards.cs` is not touched by the art lane**: piece 1's own pill
   asks `UiLook` for the warning look on *Plain diet* (the seam, to be agreed with the systems lane before either side builds it).
4. **When:** the pill's amber skin can be built now (nothing waits on the sim); the bed is built **after W6 piece 1 merges**, on
   its *sick in bed* state — until then there is nobody to draw.

## 4. ✅ Joe's answer to the first mock (A311)

*"i like pill 2, i dont like the bed. Is there a more novel and innovative way to show a down/sick villager? is there a way to
decorate the home? a sick face icon above the villager that persists even when they are in buildings (i.e. in their home being
sick)"* — **pill 2 is his** (*Plain diet* in an amber ribbon, *Run down* and *Sick* in the painted red); **both beds are out.**

## 5. The second mock (A311) — a mark that follows the person, and the home that shows it

Same cabin, same Vorith (`D:/Projects/brushes/sickbed/sickbed_pick2.jpg`, `compose2.py`).

- **The mark:** a parchment seal with a little tail, in the icons' pen and colours, over the person — **over their house while
  they lie sick indoors** (at the door), over their head when out. Three faces for it: ⭐ **F1** a fevered face — pale, eyes shut,
  flushed, a damp cloth on the brow; **F2** a face in a kerchief tied round the head; **F3** a bowl of broth (care, not illness).
- **The home that shows it, in the world:** **H** — straw strewn before the door (the old way of quieting a sickroom), a sprig of
  herbs hung by the door, and the fire kept in, its smoke in any season while someone lies sick.
- **Run down:** a smaller mark with an amber edge, out and about — offered, ⭐ **not recommended**: *run down* lasts years, so it
  would hang over half a forage village all that time (nagging, §1.2); the amber pill and the panel's count say it.

### 5a. Calls — ⏸️ Joe's

1. **The mark's face:** ⭐ F1, F2 or F3 — or none.
2. **The home (H):** ⭐ yes, with the mark; or alone; or no.
3. **A mark for *Run down*:** ⭐ no; or the small amber one.
4. **His OK for the files outside the lane's table:** `VillageMap.Indoors.cs` (who is indoors, and in which building — the mark
   sits over it), `VillageMap.People.cs` (the mark over a person out of doors), a new `VillageMap.SickMark.cs`, and the home's
   straw and sprig in `VillageMap.Yards.cs` (the home yard) with the smoke's rule in `VillageMap.Smoke.cs`; the mark itself
   painted as an icon (`p3_icons.jsx`, a new `textures/ui/icons/marks/sick.png`). For pill 2: `pill-warn.png`, a theme line in
   `ParchmentLook.cs`, `UiLook.Pill`'s third look. `Main.Cards.cs` stays piece 1's (§3's seam).
5. **When:** pill 2's skin now; the mark and the home after W6 piece 1 merges (nobody is sick until it does).

## 6. ✅ Joe on the second mock, and the third (A312) — illuminated tiles, painted in Photoshop

*"hate the icons. Use the attached as inspiration to build our own version in photoshop"* — three references of his: gilded,
jewelled manuscript tiles with a roundel and a Latin ribbon (a bowl of pottage held in two hands; a fevered face with flames; a
queasy green face). **The F-marks (§5) are out.** Built as our own, never traced (`D:/Projects/brushes/sickbed/ps/tiles.jsx`,
`health_tiles_pick.jpg`): a worn parchment rim, a gold field mottled with the wash stamps, a double rule, filigree spirals in
the corners with a light edge, eight set stones (ruby, sapphire, emerald) with pearls at the edges, a gold-ringed roundel, an
arched ribbon carrying the **pill's own word** in Cormorant Garamond (English, for §1.1; Latin is a word away if he wants it), all
in the game's pen (drybrush, about 1/40 of the side), light from the upper left. One tile for each state that is not *Well*:

- **Sick** — a flushed face, eyes shut, sweat, flames licking up behind, on deep blue.
- **Run down** — a queasy green face, eyes squeezed, mouth wavering, the head swimming, on dusk violet.
- **Plain diet** — a clay bowl of plain pottage held in two hands, steaming, on deep green.

At the map's 40 px the tile reads as a gold square with its subject (the ribbon's word does not). ⭐ Recommended: the **Sick**
tile over the person (over their house while in bed), with **the home strewn** (§5's H); **Run down** and **Plain diet** not on
the map (they last years — a sign over half a village), but as the pictures on the village's two warnings (§5.4) and beside
the pill on the card. A map cut without the ribbon (the subject larger) is offered.

### 6a. Calls — ⏸️ Joe's

1. **The tiles' look:** as painted, or what to change.
2. **Where each goes:** ⭐ Sick on the map (and the home strewn); Run down and Plain diet on the warnings and the card — or all
   three on the map.
3. **The map cut:** the full tile, or ⭐ a cut without the ribbon at map size.
4. **Files**, as §5a 4, with the tiles as `textures/ui/icons/health/*.png`; **when**, as §5a 5.

## 7. ✅ Joe on the tiles, and the fourth mock (A313) — just the faces

*"redo them and pare them down to just the faces. not the border/background, not the banner with text. just the faces (like
emojis)"*, and *"is there a better visual representation of that than a bowl of broth?"* — **the tiles are out; the bowl is out.**
Painted in Photoshop (`D:/Projects/brushes/sickbed/ps/faces.jsx`, `health_faces_pick.jpg`): three round faces alone, one family
getting worse — **Plain diet** fed up (pale wheat, heavy lids, one brow cocked, the mouth flat and pulled aside: *the same food
again*; it works whatever the one food is, and the card's tooltip names it), **Run down** queasy (green, eyes squeezed, the
mouth wavering, a bead of sweat), **Sick** fevered (flushed red, eyes shut, brows up, sweat). The game's heavy pen, lit from the
upper left. They read at the map's 32 px.

### 7a. Calls — ⏸️ Joe's

1. **The three faces:** as painted, or what to change.
2. **Where each goes:** ⭐ Sick on the map (over the person; over their house while in bed) with the home strewn (§5 H); Run down
   and Plain diet beside the pill on the card and on the village's two warnings, not over heads (they last years) — or all three
   over heads.
3. **Files and when**, as §5a 4–5, the faces as `textures/ui/icons/health/*.png`.

## 8. ✅ Built (A314) — the amber pill and the three faces; the map's mark after piece 1

Joe: *"Yes, as recommended"* — the three faces as painted; Sick on the map (over the person, over their house while in bed) with
the home strewn; Run down and Plain diet beside the pill on the card and on the village's two warnings, never over heads; the
files OK'd.

- **The amber pill:** `textures/ui/controls/pill-warn.png`, painted by `p2_controls.jsx`'s own pill code in a scratch copy
  (`D:/Projects/brushes/sickbed/ps/p2_controls_warn.jsx`, item `pillwarn`): the unmet label, drawn on the unmet's seed so its frays
  are the same, in amber `#c98a1e` with an umber edge. The theme's `pill_warn` (`ParchmentLook`), `UiLook.PillWarned()` (the third
  look; a flat amber pill on the slate look), its words `UiLook.Warned` — the theme's `warned`, the log's ochre ink, in
  `ParchmentLook.Words` so the contrast probe measures it.
- **The faces:** `textures/ui/icons/health/plain-diet.png`, `run-down.png`, `sick.png`, 128 × 128, mipmapped, from
  `D:/Projects/brushes/sickbed/ps/faces.jsx`.
- **The seam (sent to the W6 session):** piece 1's pill (step 3, `Main.Cards.cs`, the systems lane's) wears `Pill(met: true)` for
  *Well*, `PillWarned()` with `Warned` words for *Plain diet*, `Pill(met: false)` for *Run down* and *Sick*, and the face beside it;
  the village's two warnings carry the face. **The map's Sick mark and the home strewn are this lane's, after piece 1 merges**:
  the view needs the sick-in-bed state and the home they lie in.
- **Probe `ui colours:`** wants the amber pill at its 96 × 64 master with mipmaps, the theme's warned pill wearing it with ochre
  words, and the three faces at 128 with mipmaps; `ui contrast:` now reads 22 roles. **Red-checked: 5 mutants, 5 red** (the
  warned pill in the unmet painting; its words rubric; no warned pill in the theme; its words too pale — on `ui contrast:`; the
  sick face missing).

### 8a. The sim's names (W6, D938–D941) and the warnings' faces (A317)

From the W6 session: `VillagerState.SickInBed` (in bed; `Villager.SickDays` > 0 is ill, on the way home or in bed);
`SimWorld.HealthOf(v)` → `HealthState.Well / PlainDiet / RunDown / Sick`; where they lie, `world.RestingPlaceOf(v)` (a housed
family's door tile), the house `world.HouseholdOf(v).HomePosition` (null with no roof — they lie where they sleep); the view
already draws `SickInBed` indoors when housed (`VillageMap.Indoors`). The card's sentence `HealthNoteFor(v)`; the count
`RunDownCount()`. The two warnings are moments *"A plain diet"* and *"Run down"* with `MomentMark.PlainDiet` / `.RunDown`
(appended sim-side by W6) — their pictures `icons/moments/plain-diet.png` and `run-down.png` (A317, 104 master). **Sick on the map
and the home strewn** build on these once piece 1 merges.

## 9. The map's mock (A320) — Sick over the house, the home strewn, on a real sick villager

W6 piece 1 merged (D944, `a92da93`), so somebody can be sick. A copy of his honey-harrow Hollowmere run forward until a housed
person took to bed (`D:/Projects/brushes/sickbed/ZzSickSave.cs`, copied into the tests to run and deleted): **Orwin, Year 94,
winter, in bed at home with 4 days to go** — the other five saves tried had nobody housed fall ill in 8 years. Rendered by the
built game; Joe's painted `icons/health/sick.png` and the straw and herbs laid on it (`mock320.py`, `sick_map_pick.jpg`).

- ⛔ **Found: the view drew Orwin standing at his door.** `VillageMap.ShownOnTheMap` asks whether someone is housed only while
  `Resting`, so `Indoors(SickInBed, housed: false)` is false and a person sick in bed at home is drawn outside it. The one-line
  fix (`Resting or SickInBed`) puts him indoors — measured: the only pixels that change between the two renders are his
  (`VillageMap.Indoors.cs`, a file Joe OK'd for this, §5a 4). Folded into the build, with a probe guard.
- **The face, over the roof** (⭐), centred on the house, about a tile across, a soft shadow under it; or **small, by the door**.
  Out of doors (ill and walking home, or no roof) it rides over the head. ⭐ It keeps its size on screen at every zoom, so a
  sick house reads from three steps out (panel 5).
- **The home strewn:** a bed of straw before the door, a sprig of herbs hung beside it. Drawn while somebody in the house lies
  sick; gone when they are up.
- ⚠️ **The smoke — taken back.** §5's *"the fire kept in, its smoke in any season"* would put smoke over a house that burns no
  wood: the sim does not light a fire for the sick, and since A139 smoke means firewood burning (`indoors-and-smoke.md §2`,
  *true to the sim*). In winter Orwin's hearth smokes anyway. ⭐ Recommended: no smoke of its own.

### 9a. Calls — ⏸️ Joe's

1. **Where the face sits:** ⭐ over the roof (panel 2), or small by the door (panel 3).
2. **The smoke:** ⭐ dropped (smoke stays the firewood's), or kept in any season as first offered.
3. **The straw and herbs** as shown, or what to change.

## 10. ✅ Built (A321) — Sick on the map, as recommended

Joe: *"Yes, as recommended"* — the face over the roof, no smoke of its own, the straw and herbs as shown.

- **The fix:** `VillageMap.ShownOnTheMap` asks for the house when `AsksForAHouse(state)` — resting **or lying sick** — so a
  person in bed at home is indoors (the house's lamp by night already followed `Indoors`).
- **New `VillageMap.SickMark.cs`:** whose home somebody lies sick in, noted once a tick where the states change
  (`NoteTheSick`, called from `NoteTheFires` beside the homes' lamps; `_homesSick`, never hashed). `SickMarkOf` (pure): in bed at
  home → over the house; ill out of doors (walking home with `SickDays` set, or in bed with no roof) → over the head; well →
  none. Joe's `icons/health/sick.png` at **30 px on screen at every zoom**, a soft shadow under it, over the house's centre and
  over the people (`DrawTheSickFacesOverHouses`); over a head beside the other marks. **The straw** (`PaintTheStraw`, 96 px,
  painted once and kept): a soft gold bed and 170 stalks, thickest by the wall, laid outward from the door's painted step at
  0.95 tiles; **the sprig** (`PaintTheSprig`, 48 × 64): thirteen stems in three greens, tied in red, hung on the wall beside
  the door — both over the houses, under the people (`DrawTheSickHomes`). No smoke of its own: a home smokes as it always did.
- **Hooks, in the files Joe OK'd (§5a 4):** `VillageMap.Indoors.cs` (the fix; the probe's line chained after `indoors and
  smoke:`), `VillageMap.Smoke.cs` (the note), `VillageMap.People.cs` (the three draws). `VillageMap.Yards.cs` was not needed.
- ⚠️ **Under the canopy:** drawn in the people's pass, so a house under a painted tree has its face under the leaves too, as a
  person in the woods is (A36). A hook after the canopy would be `VillageMap.cs`, the systems lane's — not asked.
- **Probe `sick on the map:`** (its own line): the fix, where the face goes, the face there and its size, the straw gold and
  thickest by the wall, the sprig green tied in red, no smoke out of winter. **Red-checked: 10 mutants, 8 red (the face missing caught by `ui colours:` first); 2 zero, written down: the live call reverted (`ShownOnTheMap`) and no sick homes noted (`NoteTheSick`) — both need a world with somebody sick, which the probe has not; the render on Orwin is their evidence.**
- **Seen:** rendered on Orwin's copy at three zooms (`D:/Projects/brushes/sickbed/out/b_z0.png`, `b_z-3.png`, `b_z-7.png`):
  he is indoors, the face over his roof, the straw and sprig at his door; from far out the face still marks the house.
  The face over a head out of doors is the probe's and the mock's until play.

