# The gold seal for new items (A255)

> **Status: ✅ BUILT (A257, 2026-11-25) on `art`, UNPLAYED — Joe looks before it merges.** His five calls (§6): *"All as recommended"*. Art lane (CLAUDE.md *Two tracks*),
> branch `art`. The *when* is built and stays as it is (D880, D894); this is the seal's picture and where it sits. Pictures:
> `D:\Projects\brushes\gold-seal\` (`seal_1_designs.jpg`, `seal_2_bar.jpg`, `seal_3_options.jpg`).

## 1. What was asked

Joe's play note 2, 2026-11-24: *"new items main categories should also highlight and add a gold seal to the main category until clicked
and add the same gold seal to the new building until clicked"*. The highlight and the category rule were the systems lane's and are built:
a category is new while a building in it is new and not yet seen in its flyout (D894); the word in gold and the gold-leaf swash under it
(D880, A216's `underline-gold`). D894: *"The gold swash stays until the art lane's seal."* After the night view: *"Yes, start it"*.

## 2. What is there today

- **New:** the word in gold ink (`GoldInk` `#553e0c`) and the gold-leaf swash under it, on a category's button and a building's.
- **Owed** (A106, D103/D252) — a free building waiting: a small gold coin at the icon's **lower right** (`<name>-owed.png`). A different
  fact, and it keeps its picture. The seal must not read as it.
- The red wax seal (title, checkboxes) and the K (the logo) are taken; Joe asked for a *gold* seal.
- The icons are 20 logical px; at the dial's 100 % the HUD draws at 0.75, so **15 px on screen**; at 150 %, 22.5.

## 3. The seals — three originals

Painted in Photoshop by script (`gold-seal\goldseal.jsx`, the checkbox seals' own pen and wax from `p2_controls.jsx`; every point typed,
nothing traced, cropped or copied). 64 px masters from 256 px paintings; PSDs in `gold-seal\psd`.

- **A — wax seal:** the checkbox seal's shape in gold leaf over gold wax (`#c99a32`, a pale leaf lit upper left, umber-gold shade lower
  right, an umber edge so it stands off a yellow roof), an eight-pointed star pressed in.
- ⭐ **B — on ribbons:** A on two short rubric ribbons, swallow-tailed. **The ribbons are what tells it from the owed coin at 15 px**: at
  that size every gold mark is a gold spot, and only B's red says *seal*.
- **C — rosette:** a gilt rosette of twelve petals round a boss.

## 4. Mocked on the real bar

The art build itself, unchanged, on a copy of his Millbrook at 1920 × 1080, the dial at 100 % and 150 %: a scratch script
(`gold-seal\seal.gd`) dresses Knowledge and Civic (closed) and the Hunter's lodge and the Brewery (in Food's open flyout) as new, as
`Main.NewMarks` does, and lays the seal over each icon. `seal_2_bar.jpg`: today and A, B, C at both sizes. `seal_3_options.jpg`: B with
and without the swash, beside the word instead of on the icon, at 60 % of the icon instead of 75 %. `seal_1_designs.jpg`: each at 20 and
40 px beside today's swash, and with the owed coin on the same icon.

- On the icon's **upper right**, three quarters of the icon's side, it stands clear of the owed coin (lower right) and of the word.
- Beside the word it reads as a footnote and pushes nothing, but it is far from the picture it marks.
- With the swash too, a new item wears three marks (gold word, swash, seal); without it, two, and the seal is the one that catches the eye.

## 5. Wiring, when he answers

| File | Change | Lane |
|---|---|---|
| `textures/ui/controls/gold-seal.png` + `.import` | the picked seal, 64 px master, mipmaps on | art |
| `ParchmentLook.cs`, `UiLook.cs` | the seal as a theme texture beside `new_swash` (`new_seal`); the swash dropped if call 3 | ⛔ call 5 |
| `Main.NewMarks.cs` | `WearNew` lays and lifts the seal over the button's icon; the `new:` probe line asserts it | ⛔ call 5 |
| `Main.UiLookProbe.cs` | `ui colours:` checks the seal loads at its master size with mipmaps, as the swash | ⛔ call 5 |

No file under `src/Kith.Sim`, `data` or `tests`: the seen set and the rule are as they are.

## 6. Calls — answered: Joe, 2026-11-25, *"All as recommended"* (A257)

B on ribbons; on the icon's upper right; the swash goes and the gold word stays; three quarters of the icon; his OK for §5's files.

1. **Which seal:** ⭐ B on ribbons (recommended — the only one that reads as a seal, not a coin, at 15 px); A; C.
2. **Where:** ⭐ on the icon's upper right (recommended); or after the word.
3. **The swash:** ⭐ goes — the seal takes its place, the gold word stays (recommended; D894 kept it *"until the art lane's seal"*); or both.
4. **Size:** ⭐ three quarters of the icon's side (recommended); or 60 %.
5. **His OK** for the systems-lane files in §5.

## 7. As built (A257)

- `textures/ui/controls/gold-seal.png` — seal B, 64 px, mipmaps on. `underline-gold.png` deleted: nothing wears it now.
- `ParchmentLook`: the theme's `new_seal` icon in place of `new_swash`; `UiLook.NewSeal` in place of `NewSwash`.
- `Main.NewMarks.WearNew`: the gold word, and a `TextureRect` named `NewSeal` laid on the button, anchored to its top middle where the button
  lays its icon — its left edge 0.45 of the seal short of the icon's right edge, its top 0.3 of the seal above the icon's, three quarters of
  the icon's side (`SealShare`) — so it holds through a resize. Laid once when the button turns new, lifted when it clears; a relight that
  finds it there lays no second one. A glyph with no painted icon takes it at 14 px.
- Seen in the game, not mocked: a copy of his Millbrook with three buildings taken out of its seen set (the brewery, the hunter's lodge,
  the library), the dial at 100 % and 150 % (`gold-seal\real100`, `real150`): Food and Knowledge gold with the seal, the Hunter's lodge and the
  Brewery gold with it in Food's flyout; Food plain once opened.
- ⚠️ **A false alarm worth keeping:** the first real shots showed the seal with the word in plain ink. The scratch script's own `undress()`
  stripped every gold font before each shot. `main` showed the same until the script was fixed; the game keeps the gold.
- The probe: `new:` asks the seal with the gold (and no swash) everywhere it asked the gold, the seal gone wherever it asked plain, and
  its place and size on Knowledge and the Library; `ui colours:` loads `gold-seal.png` at 64 × 64 with mipmaps in the swash's place.
  Red-checked: 6 mutants, 6 reds (no seal laid; the seal left on a cleared button; on the upper left; the icon's size; none in the look; the swash kept).
