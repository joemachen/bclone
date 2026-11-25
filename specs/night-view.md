# The night view, with lit houses (A249)

> **Status: ✅ BUILT (A252, 2026-11-25) on `art`, UNPLAYED — Joe looks before it merges.** His seven calls (A251, *"All as recommended"*, §8);
> `slice/play-notes-2` merged first (D902) and `main` came into `art`. What was built and how it was checked: §9. One file his OK did not
> name is left alone and asked about (§9: the newcomer's chevron).** Art lane
> (CLAUDE.md *Two tracks*), branch `art`. A view toggle only: nothing in `src/Kith.Sim`, `data/` or `tests/` changes, nothing is
> hashed. Mocks drawn by the game itself, a scratch copy run on copies of Joe's saves: `D:\Projects\brushes\night-view\`
> (`night_1_seasons.jpg` … `night_5_villages.jpg`); the build's own pictures in `night-view\build\`.

## 1. What was asked

Joe, 2026-11-24: *"i do want to add night time. not a day/night cycle, but a button toggle on the main screen that uses
unique-to-this-game created-by-you-in-photoshop versions of sun/moon from medieval examples attached. basically lights on/lights
off."* And play note 11: *"houses should show lights on when people are home the same way the tavern does"*. *"main screen is the
playing screen"*. *"do night view first, then goal seal"*. Brief and references: `D:\Projects\brushes\night-view\`.

## 2. How often a home has somebody in it — measured

Copies of every save that opens with three homes or more (21 villages, 156 homes, 144,087 home-ticks, two years each), the view's
own rule for *inside* (`VillageMap.Indoors`), the family's own people (`night-view\measure\`). A day is four ticks.

| | all 21 | the 7 villages with five homes or more |
|---|---|---|
| somebody home | **81 %** | **75 %** |
| of those, by an adult of the family | 52 % | 44 % |
| spring / summer / fall / winter | 77 / 81 / 82 / 86 % | 71 / 72 / 77 / 81 % |
| by the quarter of the day | flat, within 1 % | flat, within 1 % |
| every home lit at once | 43 % of ticks (nearly all of them three-home villages) | 2 % |
| a tavern, for comparison | 13 % | 16 % |

- **By family:** with children at home 96 % — small children rest at home all day; elders alone 41 %; a couple with no children 64 %.
- **The median home is lit 98 % of the time**; a tenth of homes under 28 %. So a window says *a family with children lives here*
  most of the time, and *the house is empty* about a quarter of the time in a grown village, never in a three-home one.
- **There is no evening.** The sim has no hours (the quarters are flat), so the light cannot come on at dusk. Lit means home, not bedtime.
- **Flicker:** 60 % of the lit stretches are **one tick** (1.3 s at 1×): somebody in and out with a load. The tavern's one-second fade would
  blink at each, so a home's lamp comes up and goes down over four seconds — a one-tick visit never reaches a full glow.

## 3. What the map does now, and at night

| Now | At night |
|---|---|
| The tavern's lamplight while a drinker sits inside (A199) | the same rule; drawn as §5's light |
| Chimney smoke (A139), steam (A199) | under the tint with the rest; unchanged otherwise |
| The forge, the ovens: smoke only, no light | **lit while they burn** — the forge orange, the bakery's oven warm (`FireLitAt`, unchanged) |
| Falling snow (A76) | the flakes come half way back to white, so they read as moonlit. ⚠️ Not mocked: no save of his is mid-snowfall; looked at in the build |
| Homes: no light | **lit while one of the family is inside** |
| The title's picture at each save (A228) | day (call 7) |
| The minimap, the HUD, every card | unchanged — paper and ink |

## 4. The night

- **One tint over the painted valley.** Every layer is multiplied by one colour: the ground's, the fields' and the flakes' shaders by a
  uniform (`night_tint`, with `night_grey` taking the ground's colour part way to grey first), and everything the map draws by its own
  `SelfModulate`. No re-bake, no extra pass, no per-frame rebuild; a second's fade moves the two values and nothing else.
- **What is lifted back.** A draw that should not darken is drawn as its colour ÷ the tint, which the multiply returns exactly:
  **people** to 80 % of daylight; the player's **marks** wholly.
- **The marks need night colours.** Today's are dark inks, chosen for parchment; at night they vanish. Each has a pale partner of the same hue
  (may build `#8fe0a4`, unwise `#f2cf6a`, refused `#ff8f86`, selection `#cda4ff`, housing's edge `#e8bd7c`, work ground's `#7fd6e0`,
  harvest's `#ffb765`, destroy's `#ff8a94`, idle `#93cdea`, full `#f0bd6e`, sites and demolition, the plain map's people dots). Measured on the
  ground as rendered (grass, snow, water, woods, housing ground), least contrast of nine marks:

| | grass | snow | water | woods |
|---|---|---|---|---|
| day, as today | 2.7 | 5.6 | 2.6 | 2.3 |
| **A** moonlit (tint `0.55, 0.62, 0.78`, grey 25 %) | 3.5 | **2.0** | 3.5 | 4.0 |
| **B** deep night (tint `0.36, 0.43, 0.60`, grey 35 %) | 5.2 | 3.4 | 5.2 | 5.6 |

  **B is the one recommended: no mark falls below 3.4, where daylight's fall to 2.3.** A's marks drop to 2.0 on snow — a light-blue ground and
  pale marks — and would need a darker set of their own.
- ⚠️ **The zones' outlines keep their colour from when they were traced**, so a toggle must resolve the colour at the draw (a role, not a
  colour, on each cached outline). Found mocking: the first render had daytime edges at night.
- Painted land's washes come 35 % of the way back; at 60 % the housing ground read as a grey slab on snow.
- Works with the watercolour on and off (the plain map's bake is drawn by the map itself, so the same tint reaches it).

## 5. The light

- **Where:** a home's door and one front window (cabin), two (cottage), two and both sides' pairs (manor), with side windows; the tavern as
  it is; the forge and bakery at their door. Pieces laid with the roof (`lamplight …`, `IsLamplight`), kept in the roof's cache, drawn at the
  cache's `Light` level.
- **Two looks mocked:** (1) ⭐ **soft painted light** — a pool with an uneven dried edge under the eaves and before the door, the windows'
  bright slits over it; (2) **the tavern's daylight fans**, longer. The fans read as stage lights in a row on a cottage; the pools sit in the
  watercolour.
- **By day:** the tavern's fans stay as they are. Houses by day are mocked too (`night_2_lights.jpg`): a faint fringe hardly seen, lit three
  times in four — it would say little. Recommended: houses light at night only.
- An empty house (family dead or gone) and a house nobody is in stay dark. Somebody else's family warming at a neighbour's fire does not light it
  (§2: it moves the number by 0.2 %).

## 6. The button

- **It shows the sky you are in** — a sun by day, a moon by night; its tooltip says what a click does (*Night — n*, *Day — n*).
- **Three originals**, painted in Photoshop by script (`sunmoon.jsx`, the game's own pen; PSDs in `night-view\psd`), every point typed in the
  script, nothing traced, cropped or copied: ⭐ **A woodcut** — a faced sun in ten gold flames with ten red behind, a gold crescent holding a
  blue face in profile; **B painted** — straight red and gold rays, a whole blue face in a red and gold rim; **C roundels** — a gold ring, the sun
  or a pale half-face and stars in it. Shown at 40, 24 and 20 px and on the real strip. A's two outlines differ most at 20 px.
- **Where:** ⭐ after the speeds on the strip; or first on the strip, before the season; or the valley map's corner (lost if that window is shut).
- **Key:** ⭐ `n` (free today). Also a tick in Settings, *On the map*, where the watercolour's is.
- **Remembered** per player in `settings.json`'s `toggles` as `night`, default off. That list takes any name, so **no sim file changes**.

## 7. Files

| File | Change | Lane |
|---|---|---|
| `shaders/valley_terrain`, `valley_fields`, `snowfall` `.gdshader` | ✅ built (A251): `night_tint` (and `night_grey`), defaults daylight; a day picture moves no further than two runs of the old shaders differ | art |
| `textures/ui/icons/time/day.png`, `night.png` + `.import` | ✅ built (A251): the woodcut pair, 160 px masters, mipmaps on as the season icons' are; not yet worn by anything | art |
| `VillageMap.Night.cs`, `VillageMap.NightProbe.cs` (new) | ✅ A252: the toggle, the fade, the lift, the marks' night colours, who is home, the lamps and the pools, the probe's lines | call 6 ✅ |
| `VillageMap.Tavern.cs`, `.Roofs.cs`, `.Smoke.cs`, `.People.cs`, `.Watercolour.cs`, `.TavernProbe.cs` | ✅ A252: the lamplight's draw by day and night; the lamps laid beside the roof; who is home noted with the fires; people lifted; the uniforms handed over. `.Snowfall.cs` needed nothing (the flakes' uniform is handed with the ground's) | call 6 ✅ |
| `VillageMap.cs` | ✅ A252: the clock; each mark answered at its draw; the washes lifted | call 6 ✅ |
| `Main.Strip.cs` | ✅ A252: the button, and the speeds closer together (§9) | call 6 ✅ |
| `Main.cs`, `Main.Settings.cs` | ✅ A252: the key, the tick, the probe's lines | call 6 ✅ |
| `VillageMap.WalkIn.cs` | ⏸️ the newcomer's chevron, `ArrivalMark` `#4e3f1b`, dark on the dark valley — its night partner `#efd9a0` waits for his OK (not in call 6's list) | ⏸️ |

`PlayerSettings.cs` is **not** needed. No file under `src/Kith.Sim`, `data` or `tests`, so the suite's diff stays empty. Probe lines at the build:
the tint and every mark's pair and contrast, nobody-home and who-is-home cases, the lamps' count a tier, the slow lamp, default day, remembered,
the key and the button's picture, the strip still one line at 1,422 at the founding (`bars:`), and the title's picture still day.
Frame cost is measured then, on his saves, day against night.

## 8. Calls — answered: Joe, 2026-11-25, *"All as recommended"* (A251)

Each as its ⭐ / *recommended* below: B; 1; night only; A; after the speeds, key `n`; **his OK for §7's files once `slice/play-notes-2` has merged**; always day.

1. **How dark:** B deep night (recommended) or A moonlit.
2. **The light:** 1 soft painted light (recommended) or 2 the tavern's fans.
3. **Houses by day too?** No — at night only (recommended).
4. **The sun and moon:** A woodcut (recommended), B or C.
5. **The button's place and key:** after the speeds, key `n` (recommended); or first on the strip; or the valley map's corner.
6. **His OK** for the systems-lane files in §7, once `slice/play-notes-2` has merged.
7. **The title's picture at each save:** always day (recommended — it is one of four seasons' looks, and a dark title hides the village), or as
   the player was looking.

## 9. As built (A252, 2026-11-25)

- **The switch.** One tick, *show the valley by night*, in Settings' *On the map*, off by default; the strip's sun or moon after the speeds and
  the key `n` press it, so there is one answer and it is saved with the rest (`toggles.night`). The picture is the sky you are in; its tooltip
  says the other (*Night — n*, *Day — n*). ⚠️ **The speeds sit closer** (the group's gap 6 → 4, between the speed buttons 6 → 1): the button's
  24 px took a busy village's strip at 1,280 wide to three lines, and `bars:` refused it. Now two, as before; one at 1,422 at the founding.
- **The fade.** A second, in real seconds, paused or not. A village that opens with the night on is night from its first frame.
- **The tint and the marks** as §4. A mark is answered at its draw by its daytime colour (`AtNight`), so a cached outline is never traced
  again; what is not a mark (ink, fences, walls, the palisade's stakes) goes dark with the valley. The seam a mine in hand would claim has
  its own pale partner `#e0b98c`. Routes, the paths' overlay and a gathering ring come wholly back; the land's washes 35 %.
- **The lamps.** Laid beside the roof in its cache (`LampsOf`), not in `RoofDrawing`, so what a building is drawn with by day is the list it
  was: a cabin 3 (the door, a front window, a side), a cottage 5, a manor 7, the forge and the oven 1 at the door, none in a ruin. By night each
  lit building has a pool at each window, one before the door and a faint one round them all, drawn under its roof; the slits over it; the
  tavern's daytime fans fade out as the night comes. A home is lit while one of its family is inside it (noted once a tick with the fires);
  its lamp comes and goes over four seconds of the people's clock; a lamp first drawn is as it is.
- **Not built:** the falling snow by night was not seen (no save of his is mid-snowfall); its tint is the flakes' uniform, untested by eye.
- **Frame cost**, his Hollowmere at 2560 × 1440, the same view by day and night three times over and close up twice: the GPU 4.3–4.5 ms both
  ways, the CPU's render 1.3 ms both ways, the frame's process within its own noise. No measurable cost.
- **The probe:** `night:` (the switch, the picture, the key, saved, where it stands) and `night view:` (by day every mark to the bit; the
  fade; each mark seen as its partner and the least 3.2 off a night ground by the shaders' own sum — #ff8a94 on snow; 3.4 as rendered in A250;
  ink and fences left dark; people at 0.89; the three shaders told the night and day again; the lamps a kind; out of the runs; who lights a
  home; the slow lamp; the pool), and `homes lit:` (posed on the founding: a house with one of its family resting in it lit, walking dark, no
  house none). Red-checked: 16 mutants, 14 caught for 15 reds; two zeros written down, both breaks that cannot land — M4 (the outer night check in `AtNight` removed: the inner one gives the same colour by day) and M14 (the sky button given the keyboard: `RefuseKeyboardFocus` on the strip takes it back after); M16 scored zero first (people's lift unguarded at the draw), so the draw and the probe now share `PersonTint` and it scores 1.
