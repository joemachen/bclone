# Kith's seal — the logo and the app icon

**Status (A222, 2026-11-23): ✅ in the game on `art` — Joe took all three calls as recommended (*"Yes, as recommended"*);
✅ the icon seen in his title bar. **A223: the seal on the title too** (§7, Joe: *"A, above the name"*). **A224: the red primary button in wax** (§8, *"Yes, build it"*). ✅ Seen and approved (A225, *"Looks right"*); ✅ merged into `main` at his word (A226, `ed8fb89`), pushed (A227).** ⛔ **A242: the wax button is gone** (§9, *"i kind of hate the button after sitting with it for a while"*, then *"A, red words"*, *"Yes, build both"*) — on `art`, ✅ seen and approved (A243, *"Looks right"*), ⏸️ the merge at his word. The seal and the icon stand. Claimed A220, painted A221.

Joe, via the coordinator: *"for art, i want to create a logo for the game"*; after the review, *"Yes, as recommended"* —
**a K pressed into the game's own painted seal**, not a new one.

## 1. What it is

The blank wax seal (`textures/ui/screens/seal.png`, A104, `ui-assets.md §5`) with the title's **K** pressed into its
well. A monogram, not a wordmark: a mark that still reads at 16 px. The blank seal was always meant to carry the name;
the icon has waited on it since A104.

- **The seal is the game's, unchanged.** Painted by the same script (`D:\Projects\brushes\ui\ps\p2_chrome.jsx`, item
  `seal-k`) with the blank seal's seed. Outside the letter the logo matches `seal.png` to 2/255 (measured, premultiplied).
  Today's script repaints the blank seal to within 2/255 of the committed file, so the pipeline has not drifted since A104.
- **The K is the title's:** Grenze Gotisch, the variable font at weight 400 — its default, and what the title sets
  (`ParchmentLook.cs`, `TitleScreen.cs`, no variation). Photoshop cannot see the font (it is not installed), so the
  glyph is rendered from the font file (`brushes\logo\k_mask.py`, at 4× then down) and used in Photoshop only as a
  selection. No font install needed.
- **Pressed, lit as the seal is lit (from the upper left):** the K's floor darkened, its upper-left inner wall in
  shade, its lower-right inner wall catching the light (`K` in the script: depth 6 px, floor `#2a0604` at 80 %, shade
  `#220503` at 80 %, light `#f2a088` screened at 62 % — the seal's own rim-light colour).
- **Colours:** the seal's own: wax `#9a2418`, light `#e8866a`, shade `#2e0804`, pressed face `#7e1a10`, ink `#2a2018`.
  No new colour.
- **Placement:** the K's box centred on the well (256, 250 of 512), its farthest pixel 95 px from the centre (the
  well's face is 117.6 px, its shaded ring ~14 px): 130 × 146 px.

**Measured (the acceptance bar from the outside chat's script, met on the paint, luminance 0–255):**

| Check | Measured |
|---|---|
| K centred in the well | box centre (256.0, 250.0) = the well's centre |
| reads as debossed: lit edge on the K's lower-right inner wall | lower-right wall 104, upper-left wall 26, floor 24 |
| highlights on the seal's upper-left rim, shade lower right | rim upper left 92, lower right 31 |

## 2. Files

| File | Size | What |
|---|---|---|
| `textures/ui/screens/seal-k.png` | 512 × 512 | **the logo** — beside the blank seal, which stays for anything that wants it bare |
| `icon.png` (project root) | 512 × 512 | the logo cut to its own bounds and squared: Godot's `config/icon`, the window icon |
| `icon.ico` (project root) | 16, 32, 48, 256 | the Windows icon (`config/windows_native_icon`): Windows picks the frame for the size it draws |
| `brushes\logo\png\seal-k-small.png` | 512 × 512 | **the small cut**: the same seal and seed, no pressed ring, the K out to the wax's edge (reach 150 px) — the 16 and 32 frames only; outside git |

PSDs in `D:\Projects\brushes\logo\psd\`. Rebuild: `logo.ps1` (paints both), `make_icon.py` (the icon), `sheet.py`
(the mock). The outside chat's flat-seal script is kept there too (`build-kith-seal.ps1`) — reference only.

## 3. Small sizes

`D:\Projects\brushes\logo\logo_a220.jpg`: the seals large, and the icon at 16, 32 and 48 px on parchment, a dark
taskbar and a light one, each also blown up.

- **48 px:** the logo reads — the seal, its ring, a K.
- **32 px:** the logo's K reads, small; the small cut's is plain.
- **16 px:** **the logo's K goes to mush** — about 6 px tall inside the ring, a smudge in a red disc. The small cut
  (the K about 9 px tall, no ring) reads as a dark K on red wax. Hence the icon's two sources (§2).
- **The K's double stem** (Grenze Gotisch's K has a hairline slit down its stem) reads as "lK" at 32–48. It is the
  title's own K, so it is kept; a single-stem K would be another face.

## 4. Wiring (A222, done)

`project.godot`: `config/icon="res://icon.png"` and `config/windows_native_icon="res://icon.ico"`; the Godot logo
`icon.svg` (and its `.import`) removed — nothing else names it. All in the art lane's files (HANDOFF's lane table:
*"`project.godot` (… the icon), `icon.svg`"*). There is no `boot_splash` or `export_presets.cfg` today; an export preset's
icon is the `.ico` when one is made.

## 5. Joe's calls — ✅ all three as recommended (A222)

1. **The look** (A on the sheet) — as painted.
2. **The icon's small sizes** — recommended: 16 and 32 from the small cut, 48 and 256 from the logo (row 2), against
   the logo at every size (row 1).
3. **`icon.svg`** — recommended: replaced by `icon.png` and `icon.ico`, the Godot logo deleted.

## 6. Next, not built

- **A wordmark lockup** — "Kith" in Grenze Gotisch beside the seal, for store pages and trailers. At Joe's word.
- **The splash** (`screens/splash.png`, A104) carries the blank seal, *"the name goes on when it is chosen"*. Not wired,
  so not repainted here.

## 7. On the title (A223) — Joe: *"i also want to see it here in the main menu somewhere"*, then *"A, above the name"*

Mocked two ways on his own screenshot (`D:\Projects\brushes\logo\title_a223.jpg`: A above the name, B pressed onto the
rule under it; a third, beside the name, was the wordmark lockup and left off). Built as A, in `TitleScreen.cs` (outside
the lane's table; his pick was the OK, as it was put to him — the art lane built this panel in A180 and A190):

- **A child of the name, outside the column's layout,** so the name and the menu stand exactly where they stood (the
  capture's capitals are on the same line with and without it). `seal-k.png` at a 118 px box — its wax 88 px — centred
  on the name, the wax ending **20 px above the capitals**, as the mock had it. The blackletter at 96 px carries a 46 px
  empty band above its capitals (`NameCapTop`), so the first build's foot left 45 px between them and was moved down.
- **Hidden, never cut, on a short window:** shown only while the paper above the name holds the wax and 8 px clear
  (`SealFits`, on the name's move — never per frame). It goes with the menu on the Settings page.
- **Probe** (`title:` line, `TitleScreen.Probe.cs`): the picture is `seal-k.png` at 512, shown at the probe's window,
  its box 118 square and centred on the name within 1 px, its wax inside the sheet's margin and 12–30 px above the
  capitals, and the fit rule's two sides. **Red-checked (D326): the foot moved onto the capitals (43 → 80) — 1 ❌, the
  wax line, and nothing else**; restored by reverse `sed`.
- Captures (the game's own, 1280 × 800 at 1.8 × on his second screen): `D:\Projects\brushes\logo\title_capture\`.

## 8. ⛔ Superseded by §9 (A242) — the primary button in wax (A224) — Joe: *"can we make the button look 'wax-y' too? or at least mock it"*, then *"Yes, build it"*

He asked first whether Continue was the seal's colour: nearly — the button was the flat rubric `#9b2d1f`, the wax is poured
`#9a2418` and painted lit and shaded, so it reads darker (about `#72231a` on his screen). Mocked by the game itself (a
throwaway theme change, put back), then built:

- **Painted** by the seal's recipe on a long pour (`D:\Projects\brushes\logo\wax_button.jsx`, seed 224,
  PSDs in `button\psd\`): wax `#9a2418`, light screened upper left, shade multiplied lower right, Sketch Paper and
  the mottle, lumpy ends, drips off the foot, and the word in a pressed field like the seal's ring. Five files in
  `textures/ui/controls/` (`ui-assets.md §3`): normal, hover (the wax warmed), pressed (its light turned over, as a seal
  pressed in), focus (the slab's edge inked heavier **alone** — Godot draws the focus box over the state, so it never
  covers the word), disabled (45 %).
- **Every red primary** wears it — the title's and the new valley's *Found*, one style (`ParchmentLook.cs`, outside the
  lane's table; his *"Yes, build it"* to the plan that named it). A180's ink box round the focused title button is gone:
  it sat square round a lumpy slab.
- **9-patch:** margins 150 / 100 / 150 / 100 master px hold every lump, drip, the glint and the field's round ends, so a
  wider button stretches plain wax; it reaches 8 px past its box. The word keeps 22 px from the ends, 9 above and below:
  at 16 / 6, *Found* (40 px tall) squeezed the field until "village" crossed its rim and its F met the field's curve.
  The title's button is unchanged (its 46 px minimum); *Found* grew to 46, and 12 px wider — **the new valley's column
  wants 383 px, was 371** (the probe's `new game:` line, still ✅).
- **The words:** paper `#f6ecd8` on the wax — the lightest tenth of the field where the word sits measures 6.3 : 1 on the
  capture, 8.9 typical (the flat rubric was 6.4).
- **Probe** (`title:` line): every state a painted patch of its own file at 1064 × 248 with mipmaps, reaching 8 px; the
  focus painting clear where the word sits; the word read against the lightest tenth of the painted field in the
  normal, hover and pressed wax (`WaxGround`, measured off the painting, not a constant). **Red-checked: focus given the
  whole slab — 1 ❌ (its file); ink words on the wax — 1 ❌ (1.70 : 1).** Each restored by reverse `sed`.
- Captures: `D:\Projects\brushes\logo\button\capture4\` (the title over his save, focused and not; the new valley).

## 9. The wax comes off: Continue in red words, Found in flat red (A241–A242)

Joe: *"i kind of hate the button after sitting with it for a while"*; four mocked in the real title by a run-time theme change
(`D:\Projects\brushes\logo\button2\button_options.jpg`), he took *"A, red words"*; its states and *Found* mocked (`button_states.jpg`), then *"Yes, build both"*.
Why the wax failed: it copied the seal directly above it, nobody pours wax into a bar, and it was the loudest thing on a page
of pen lettering. The seal is the page's only wax.

- **Continue** (New village with no save), `TitlePrimary`: a `TitleItem` at 28 px (the rest 22), `RubricInk` at rest, no box.
  Under the pointer the items' faint rubric wash; pressed, the theme's swash.
- **The keyboard's mark is a painted stroke** (`controls/underline.png`) under every title item and the settings page's *Back*,
  as long as the word and 18 px, 9 px tall under the primary and 5 under the rest — in place of A180's straight 2 px line. The
  look draws it across the box; `TitleScreen.StrokeUnder` gives each item its own copy, drawn in to its word. ⚠️ Made at
  scale 4: it has no fixed ends, so the scale only picks the filter, and at 0.25 the GPU read a half-size mipmap for its
  length and it went soft (the first capture).
- **Found** (the new valley's one button), `PrimaryButton`: filled rubric, a 2 px edge, a 1 px paper line 4 px inside it at 55 %
  (`PenFrame`, a `StyleBoxFlat` and a second line); hover lightened 8 %, pressed `RubricInk`, disabled 45 %; with the keyboard
  the line alone at full strength, 2 px, over whichever state. Content 18 / 9: **the new valley's column 383 → 375 px** (371
  before the wax).
- **Out:** `controls/button-wax-*.png` (5), `WaxButton`, `WaxMargins`, `WaxOutset`, `WaxGround`, `TheWaxButton`. The painting
  scripts and PSDs stay in `D:\Projects\brushes\logo\`.
- **Probe** (`title:` line): the primary red at rest, no box, larger and redder than every other item; each stroke the painted
  one, its word's length and band; Found's three live states the red box with the line, its words 4.5 : 1 or more on the fill,
  its focus a line alone. **Red-checked, 7 mutants, 7 caught:** the primary at 22, the stroke unfitted, Found's words in ink
  (2.12 : 1), Found's focus filled; the primary in ink, Found's hover line at 20 %, a thick stroke under every item. Restored by
  reverse `sed`, compared byte for byte.
- Captures: `button_built.jpg`. ⚠️ The pointer's wash is posed there (the hover box shown as the resting one).
