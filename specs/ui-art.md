# The UI — the parchment-and-wood interface (art lane)

> **Status: ✍️ specced and mocked on `art` (A96, 2026-11-15); ⏸️ four calls Joe's (§6); NOTHING BUILT — no theme, no
> font, no code.** Joe's order (A90): *"i want to work on villagers for the next art piece. And then the UI"*; the
> villagers are done, merged and pushed (A93–A95). The interface was put after the map by his call in A65 (*"After the
> map"*). Mocks outside git: `D:\Projects\brushes\ui\` (§5).
>
> **Sources (style only, A80):** `art/concept art/11. UI style frame.jpg` and `12. Readability test.jpg`; and Joe's own
> concept over his game, `D:\Projects\brushes\concept\joe-concept-2026-11-13.jpg` — parchment panels with aged edges, ink
> text, a wax-seal logo, small windows on wooden signboards.
>
> **⚠️ It reverses a recorded call if A or B is picked:** D635 kept *"the UI stays slate"* when the ground moved to
> parchment, and D637's B7 row says the chrome was *decided slate*. A65 and the concept since point the other way.
> Whichever look he picks is recorded as B7's answer (`art-inventory.md §1`).

## 1. Legibility first (Non-Negotiable §1.1)

The panels are where the game explains itself — the card's *why*, the log, the professions table. A look that costs
one point of reading is the wrong look however good it is. So every look here is measured before it is offered (§5.2),
and the build is held to these, as probe lines where a probe can see them:

1. **Body text ≥ 4.5 : 1 against its own panel**, measured on the painted surface (WCAG AA); headings and numbers in
   colour ≥ 4.5 too; only marks that are not read (grips, a disabled tab) may sit at today's 3 : 1.
2. **No text dimmed by transparency.** Today ~70 labels are white at 55 % opacity (`Main.Muted`) — on slate that is a
   grey of 6 : 1; on parchment, ink at 55 % is about 3 : 1. Every dimmed role becomes a named colour (§3).
3. **Every warning keeps its meaning by colour AND by word or mark** (the ⚠, *Cold*, the amber number) — the warning
   hue changes with the surface (amber is invisible on parchment) and must be re-picked, never carried over.
4. **A panel is told from the map behind it in every season** — by its fill where it can, by its edge where it cannot:
   parchment over snow is 87 against 84 L\* (§5.2), so A and B carry it with the aged rim, the ink rule, and a shadow.
5. **No size moves silently.** A new font moves every measured row: the probe's `bar height` (151 today) is re-measured
   and the new number written down in the same commit, as the lane rule has always said of a font (HANDOFF 🅿️ ART).

## 2. What the UI is today (read off `art` at 65818ff)

- **No theme and no font.** No `[gui]` section in `project.godot`, no `.tres` theme, no font file; every control is
  Godot's default theme and built-in sans. `TechTreeView`'s bold does nothing.
- **One slate skin, hard-coded.** `Main.PanelSkin` (`Main.cs:7124`) — `#14171a`, a 1 px khaki edge `#948766` at 55 %,
  radius 3, margin 6 — skins 14 panels: the two top bars, *The village*, *Professions*, *Stock limits*, *Build queue*,
  *The valley* (minimap), *Village log*, *Settings*, the bar (at 0.72, the one translucent one, D305's note), and every
  card through `CardSkin` (`Main.Cards.cs:344`, cached, **re-applied on every refresh** — §5.1's trap). The title band,
  title floor, pause panel and new-game backdrop have their own dark `StyleBoxFlat`s (`TitleScreen.cs:91/163`,
  `PauseScreen.cs:45`, `NewGameScreen.cs:143`).
- **Text is white, dimmed by `Modulate`:** `Muted()` (`Main.cs:8878`) — 12 px at 55 %; faint marks at 35 %; the
  card's own text at 70 %. **Warnings and states are colours picked for dark:** `LightStopped` `#ffc759` (amber),
  `LightWorking` `#7fbf7a` (green), `CardSelectedEdge` `#ffd76a` (`Main.Cards.cs:212–214`), the owed-gift buttons'
  `(1, 0.85, 0.4)` (`Main.cs:2281–2602`), the held tab's gold (`Main.cs:8211`), the log's category colours
  (`#d96666` deaths, `#9eb8d9` born, `#8ccc8c` learned, `#e8c469` warnings, `#999e99` building).
- **Counted in the live tree** (Hollowmere Y63, a villager card open — `today/summer/tree.txt`): 141 labels, 116
  buttons, 9 checkboxes, 5 progress bars, 1 roster `Tree`, 1 `RichTextLabel` (the log), 1 `OptionButton`, separators,
  scrollbars. Overrides in the view: 59 `font_size`, 18 stylebox, 6 `font_color`, ~28 `Modulate` sites, 43 `new Color(`
  in the UI files.
- **The world things in the UI already sit on parchment chips with an ink edge** (D637's rule): goods chips, the bar's
  glyphs, the cards' portraits. They need nothing from this piece.
- Also on the screen: the debug readout (*"56 fps · …"*, `Main.cs:8681`) is white at 45 % over the map — illegible on
  parchment already (HANDOFF's 🅿️ list); the title backdrop is the flat bake, not the watercolour.

## 3. The look's parts (whichever is picked)

| Part | Carries | Built as |
|---|---|---|
| **Panel** | every window and card | a painted 9-patch (`StyleBoxTexture`), the look's surface |
| **Selected card** | the card you are working with | the same, its rule in the look's accent (A: rubric red; B: gilt; C: bright gilt) |
| **Bar** | the build bar strip | its own 9-patch; still the one translucent-able panel (D305) |
| **Button / tab** | every button; held = pressed | flat box (A, C) or wooden plaque (B); a held tab in the accent |
| **Text roles** | text, muted, faint, heading, warn, ok, selected; the log's five categories | **named colours per surface** — never alpha |
| **Headings** | *WORK*, *TOOL*, *TYPE …* | rubricated (A, B: manuscript red) or gilt (C), the heading face |
| **Display face** | card titles, window titles, the title screen | a second face, titles only |
| **Checkbox, arrow, scrollbar, separator, progress bar, roster, tooltip, popup** | the defaults | theme entries |
| **Wordmark** | the title screen | a seal is the mocks' placeholder; a real wordmark (and a name) is B1/§12's row, later |

## 4. Where the line falls between the lanes

**A theme and a font are the art lane's** (`project.godot [gui]`, a `.tres`, the font files — HANDOFF's lane table).
**Alone, they restyle about half of what you see:** the buttons, checkboxes, the roster, scrollbars, popups, tooltips,
separators, the default panels (moment modal, passing banner, tech nodes) and the default font. **They cannot reach the
other half**, because `Main.cs` and `Main.Cards.cs` hard-code it over any theme: the slate of all 14 panels, the white
text at 55 %, and the amber and green states. A parchment theme over today's code would put dark-themed buttons on
slate panels with ink-coloured defaults nobody sees.

**So the look needs one seam in the systems lane's files, once:** the hard-coded skin and colours read from the theme
instead of from constants — `PanelSkin`/`CardSkin` become theme styleboxes (*Panel*, *CardSelected*, *Bar*), `Muted`
and the 35 %/70 % sites become label variations, `LightStopped`/`LightWorking`/`CardSelectedEdge`, the owed-gift gold
and the log's category colours become named theme colours. **No behaviour changes**: with the shipped theme set to
today's values the screen is byte-identical (a probe line can assert the seam's default reproduces slate). About 45
sites, mostly in four helpers. **After it, every look change is art-lane files only.** The title, pause and new-game
screens are not on the systems lane's list; they take the same seam in the same commit.

**Who makes the seam is Joe's call (§6, call 3).** Recommended: the art lane, on `art`, as its own commit, **after water
(`slice/water-need`, open now with uncommitted work) merges into `main`**, so the two never edit `Main.cs` at once.

## 5. The mocks — Joe's own saves, the real game, three looks

### 5.1 How they were made (all outside git, `D:\Projects\brushes\ui\`)

The game itself rendered headless (Godot 4.7.1, 1920 × 1080, his UI scale 75 %) in a scratch `APPDATA` holding copies
of his saves (trap 35): Hollowmere Y63 summer, Hollowmere Y85 winter, Ashbourne Y20 — Continue pressed, panels kept,
watercolour on, a villager's card opened. **Then, in the scratch render script only, the running game was dressed:**
`reskin.gd` sets a theme on the root window (what a `.tres` could carry) and, just before each frame is drawn, swaps the
hard-coded slate panels and alpha-dimmed text for the look's — i.e. **it does by hand exactly what §4's seam would do
in code**, so every panel, word and number in the mocks is the game's own. `textures.py` paints the 9-patches
(parchment, oak, leather, the seal), `measure.py` the numbers, `sheets.py` the sheets. **Fonts are stand-ins:**
Palatino for text, Garamond Bold for titles (Windows system fonts, used locally only) — Joe's `fonts/` folder was not
touched (call 2).

⚠️ **Trap paid for:** the game re-applies its cached card skin on every refresh, after a swap; a swapped card painted
dark again. The mock clears the game's skin and paints the look *behind* the card instead. The build has no such
problem — the seam makes the game's own skin the look.

### 5.2 Measured (`measure.txt`; contrast on the painted panel, the log's interior)

| Text role | Today | A | B | C |
|---|---|---|---|---|
| text | 18.0 | 11.5 | 11.6 | 12.0 |
| muted (*Fisher · 33*, the hint line) | 6.1 | 6.2 | 6.3 | 7.0 |
| faint (grips, a disabled tab) | 3.2 | 3.3 | 3.4 | 3.9 |
| heading | 6.1 | 5.4 | 5.5 | 7.0 |
| warn | 11.6 | 5.4 | 5.5 | 6.5 |
| ok (needs met) | 8.3 | 4.6 | 4.6 | 8.7 |
| log: born / warnings | — | 5.6 / **4.1** | 5.6 / **4.1** | 8.6 / 9.2 |

- **Panel against the map, L\*:** today 7.5 (45–48 from the summer map); A 87.3 and B 87.7 (31–35); C 14.6 (38–41).
- **⚠️ Winter: snow is L\* 84 — A and B's fill is 3–4 from it.** The rim and the ink rule (A) or the oak frame (B) are
  what tell the panel from the snow (`ui_winter.jpg`); the build adds a soft shadow to A. C is unaffected.
- **B's bar:** cream text on oak 7.4, its muted hint line **4.3** — lift it in a build.
- **⚠️ The stand-in serif reads smaller than today's sans at the same size** (`ui_closeup.jpg`): a book face has a
  smaller x-height. The build steps body text up a size (12 → 13 or 14), which moves panel sizes and the probe's bar
  height — measured and written down (§1.5).
- Fixed for any build: the log's *warnings* colour darkened to ≥ 4.5 on parchment; the debug readout given its backing.

### 5.3 The sheets (sent to Joe)

`ui_options.jpg` (Hollowmere summer, today/A/B/C), `ui_closeup.jpg` (the card and the professions at his own size — the
reading test), `ui_winter.jpg` (parchment over snow), `ui_young.jpg` (Ashbourne), `ui_bar.jpg` (the build bar),
`ui_titles.jpg` (the title screen).

- **A — Parchment leaves (recommended).** Every panel a leaf of parchment with an aged rim and an inked double rule;
  ink text; headings rubricated in manuscript red, as the villagers' illuminations are (A92); buttons as faint parchment
  tabs with an ink edge; a held tab and the selected card in red. Closest to his concept and the style frame; the
  lightest on the screen.
- **B — Parchment in an oak frame.** The same parchment inside a carved oak frame with iron studs; the bar a wooden
  beam with plaque buttons in cream lettering. More *"wood"*, reads well, separates best over snow; heavier on a screen
  that already spends its edges on panels, and ~5 px more each side.
- **C — Leather and gilt (dark kept).** D635's slate kept in spirit: umber leather, a gilt double rule, cream text, gilt
  headings. The safest over any map and season and the least change; not the concept he showed.

## 6. Joe's calls (plain words in HANDOFF's ⏸️ list)

1. **Which look — A, B or C?** *Recommended: A.* It is his concept; body text reads at 11.5 : 1; it lifts the panels off
   the green map. It costs a stronger edge in winter, which the build gives it.
2. **May the art lane use his `fonts/` folder (Cardo, Eagle Lake, Grenze Gotisch, MedievalSharp, Metamorphous)?**
   *Recommended: yes — a plain book face for everything you read (Cardo, tried a size up beside today's at his UI size),
   one display face for card and window titles only, and no blackletter for anything read twice.* The font moves every
   measured row; that is expected and re-measured.
3. **The seam in `Main.cs` / `Main.Cards.cs` (§4) — who makes it?** *Recommended: the art lane, on `art`, its own
   commit, after water merges into `main`; byte-identical with today's values in the theme before any look goes in.*
   The alternative is a small systems slice.
4. **The flourishes from the style frame** — the seal as the logo, a round inked frame and compass on the minimap,
   ribbon banners for window titles. *Recommended: not in the first build* — the theme and the seam first, played; the
   flourishes a second piece, each mocked.

## 7. The build, once he picks (not started)

1. The seam (§4), default = today; probe line `ui theme:` (the seam's default reproduces slate; every panel reads its
   skin from the theme). Red-checked.
2. The theme `.tres`, the 9-patches and the font(s) in the art lane's files; `[gui] theme/custom`. Probe: `bar height`
   re-measured and written down; a contrast line over the theme's colours against the painted surface means (§1.1–3).
3. Joe plays it (`.\run.bat`), merges at his word.
4. Later: the town hall's tabs and the knowledge screen (`art-inventory.md §15` item 7) are drawn in the picked look.
