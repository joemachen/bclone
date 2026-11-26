# The new item's mark in red (A270)

> **Status: ✅ BUILT (A272, 2026-11-26) on `art`, UNPLAYED — Joe: *"2, files OK"*. Mocked A271.** Art lane (CLAUDE.md *Two tracks*), branch `art`.
> Replaces the picture of the gold seal (`specs/gold-seal.md`, A255–A258); the *when* is the systems lane's and stays as it is (D880,
> D894). Pictures: `D:\Projects\brushes\red-mark\` (`red_1_food_open.jpg`, `red_2_bar_closed.jpg`, `redseal_sheet.png`).

## 1. What was asked

Joe played the gold seal, 2026-11-26: *"Gold badge on new items doesnt read. make it red badge instead. or use the red underline that
is used on main menu"*. Taken before the art queue's animations.

## 2. Why the gold did not read

Measured (`red-mark\contrast.py`, against the bar's paper as a shot draws it, `#d9c39f`):

- **The gold word is nearly the ink's colour.** `GoldInk` `#553e0c` was darkened to be legible on the paper (A116, A117); it is a dark
  brown beside the plain ink's brown-black `#2a2018` — 1.58 : 1 between them, and both brown — the rubric red is as dark, 1.59 : 1, but a different hue, which is what the eye catches. A new word looks like every other word.
- **The gold seal is a tan spot on tan paper.** Its mean colour stands 2.9 : 1 off the paper; the same seal in red, 5.5 : 1, and red is
  the one hue on the bar nothing else wears. At his size (2560 × 1440, the dial at 100 %) the seal is about 15 px across.
- **Night changes nothing here.** The night view leaves the HUD as paper and ink (`night-view.md`); every mark looks the same at night as
  by day, and the shots show it.

## 3. The options, mocked

The art build unchanged, on a copy of his newest save the art build can open (Elderwood, `autosave-2`; his Thornfield holds W4's
Groundskeeper) with the brewery, the hunter's lodge and the library taken out of its seen set, so the game itself lights Food and
Knowledge, and the Hunter's lodge and the Brewery in Food's flyout. His own `settings.json` (UI 100 %, his ticks, his windows), the
window 2560 × 1440, the night tick off and on. A scratch script (`red-mark\mark.gd`) swaps the mark on every button the game marked new.
The sheets are crops at 1:1.

- **Today** — the gold word and the gold seal.
- **1 · the seal in red** — seal B's own painting, its gold wax turned to red wax (`red-mark\redseal.py`: each pixel keeps its lightness,
  the ribbons a step darker so they part from the wax). Same place and size as the gold one. The word stays gold.
- **1b** — the red seal, and the word in the rubric's red (`#7b2318`).
- ⭐ **2 · the title menu's red stroke** — `underline.png`, the painted swash under the title menu's chosen item, across the foot of the
  button in a 4 px band (the open category's lapis stroke is the same swash at 3 px), the word in red, no seal.

**Seen:** 1 is a red dot beside a word that still blends in. 1b and 2 both read at a glance. 2 reads from the word itself, at any
UI size, and keeps the icon clear for the owed coin (lower right, A106). It is the game's own mark for *this one* on the title menu.
⚠️ It shares a shape with the open category's stroke — blue there, red here. A new category clears when its flyout opens, so no button
wears both at once.

## 4. Wiring, when he answers

| File | Change | Lane |
|---|---|---|
| `textures/ui/controls/gold-seal.png` | deleted (2) or replaced by `red-seal.png` (1, 1b) | art |
| `ParchmentLook.cs`, `UiLook.cs` | the theme's `new` colour to `RubricInk` (1b, 2); `new_stroke` from `underline.png` in place of `new_seal` (2), or `new_seal` the red picture (1, 1b) | ⛔ call 2 |
| `Main.NewMarks.cs` | `WearNew` lays the stroke on the button's `normal` and `hover` in place of the seal (2); unchanged for 1, 1b | ⛔ call 2 |
| `Main.UiLookProbe.cs` | `ui colours:` loads the new texture; `new:` asks the new mark where it asked the seal | ⛔ call 2 |

No file under `src/Kith.Sim`, `data` or `tests`.

## 5. Calls — answered: Joe, 2026-11-26, *"2, files OK"* (A272)

Option 2, the red stroke and the red word; his OK for the four files in §4.

1. **Which:** ⭐ 2, the red stroke and the red word (recommended); 1b, the red seal and the red word; 1, the red seal alone.
2. **His OK** for the four systems-lane files in §4 (the same four he passed for the gold seal).

## 6. As built (A272)

- `ParchmentLook`: the theme's `new` colour is `RubricInk`; `new_stroke` is `NewStroke()` — `underline.png` (the title menu's red
  swash) as a `PaintedPatch` at 0.25, ends 24 px, the held button's content margins, in a `NewStrokeBand` = 4 px band at the foot.
  `UiLook.NewStroke` in place of `NewSeal`. `gold-seal.png` deleted: nothing wears it now.
- `Main.NewMarks.WearNew`: the red word, and the stroke as the button's `normal` and `hover` override (as D880's swash was); a cleared
  button loses both only where they are the stroke (`StrokeOn`). The seal's `TextureRect` and its placement are gone.
- ⚠️ **Red is not only a warning now.** `hud.md §10` gave rubric to *something is wrong* and gold to *new*; Joe chose red for new
  by eye, as the title menu already uses it for its chosen item (A242). The table says so.
- The probe: `new:` asks the red word and the stroke at rest and under the pointer wherever it asked the gold and the seal, none
  wherever it asked plain, and on Knowledge and the Library the rubric's red over `underline.png` at 4 px with no seal left over;
  `ui colours:` loads `underline.png` at 256 × 12 with mipmaps in the seal's place. The probe method keeps its name
  (`WhatIsNewIsGoldUntilClicked`): `Main.cs` calls it and was not among his four files.
- Red-checked: 7 mutants, 7 reds on `new:` (no stroke laid; not under the pointer; left on a cleared button; the word in gold; the lapis swash; the held button's 3 px band; none in the look); `ui colours:` scored 0 of them — none was aimed at its `underline.png` entry, kept and noted.
- Seen in the game on the Elderwood copy (three buildings unseen), day and night: `red-mark\built\`, `red_3_built.jpg`.
