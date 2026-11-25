# The gold seal for new items (A255)

> **Status: ✍️ PAINTED AND MOCKED (A256, 2026-11-25); ⏸️ FIVE CALLS JOE'S (§6); NOTHING WIRED.** Art lane (CLAUDE.md *Two tracks*),
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

## 6. Calls

1. **Which seal:** ⭐ B on ribbons (recommended — the only one that reads as a seal, not a coin, at 15 px); A; C.
2. **Where:** ⭐ on the icon's upper right (recommended); or after the word.
3. **The swash:** ⭐ goes — the seal takes its place, the gold word stays (recommended; D894 kept it *"until the art lane's seal"*); or both.
4. **Size:** ⭐ three quarters of the icon's side (recommended); or 60 %.
5. **His OK** for the systems-lane files in §5.
