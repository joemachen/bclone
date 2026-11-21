# Spec: The new-game screen — choose a valley before you found a village

**Decisions:** D344 (Joe's ambition: *a new-game screen with a preview and a re-roll*; *"a hard roll
is legitimate"*), D353 (the shell's order: error boundary → per-stage seeds → **the new-game screen**
→ settings persistence → save/load → title and pause), D473 (per-stage seeds: a new stage appends an
id and moves no other stage's draws), D475 (`MapGenerator.SeamsOf`), **D477 (Joe's calls for this
screen, 2026-10-03 — §2), D481 (his calls after playing it, 2026-10-04 — §2)**. Neighbours: D18 (*quoting one seed reproduces the whole run*), §10.3 of
`seeded-map-generation.md` (one valley archetype), §10.4 (*all seeds survivable, none equally
comfortable*), D335 (a derived value is never hashed), D466 (splitmix64 over a name).
**Status:** ✍️ **SPECCED (D477). ✅ THE SIM HALF (D478), THE VIEW (D479), THE MEASURED RANGES (D480)
AND JOE'S PLAY NOTES (D481) BUILT** — guards §8.1–8.9 red-checked; no golden moved. ✅ **Merged to
`main` and pushed (D482, 2026-10-04); Joe replayed D481's changes and approved them all (D483,
2026-10-04).** ⚠️ Forest cover at 0 % and 100 % founds (guarded, §8.8) but is unmeasured in play —
information for Joe, not a gate.
**🔨 THE RESTYLE (A147–A148, 2026-11-21, art lane) — §12: the map fills the screen, one parchment panel docked right,
the settings grouped. Specced A147; Joe: *"All as recommended"*; STEP 1 BUILT ON `art` (A148, §12.9), ⏸️ HIS PLAY,
then step 2's choice (§12.5).** §3's drawing is the screen before the restyle; §12.2's is the screen now.
Owner: Joe + Claude Code.

---

## 1. Why this exists

Today the only way to choose a valley is to edit `"seed"` in `data/sim.config.json` — Joe does exactly
that to play-test, and it is why that line is uncommitted on his machine right now. The game launches
straight into a founding on whatever number is in the file. A player has no way to see a valley
before living in it, to share one, or to ask for a wider river.

§2.5 says what seeded generation is for: *a second playthrough is a different place, not the same
place played again.* The screen is where that becomes something the player does.

## 2. Joe's calls (D477, 2026-10-03)

- **One valley archetype for now.** The *Valley* row shows **River valley** and nothing else; a real
  second archetype is its own generator stage and its own slice.
- **The rows:** river width · **river direction** · forest cover · stone amount · iron amount · seam
  scatter. **Room for more** — *"ultimately there will be other resources on the map"* — so a row is
  data, and a new row is a config key and a line of json, never a new screen. ✅ The first more: the
  **Nomads** row (D570), the switch's starting position — a row because a setting is never sim state
  (`settings-persistence.md §3`) and a row is remembered and shared.
- **River direction: W–E, N–S and the two diagonals**, plus *Any* (the seed picks). W–E is the
  default, so every valley anybody has seen is unchanged.
- **The seed is any text; the dice roll words.** The screen opens on a fresh roll.
- **On the screen:** stats under the preview, an editable village name, the settings in the share
  code, and a **Default settings** button (every row back to the shipped config's value).
- **After playing it (D481, 2026-10-04):** river width shows **no number** — its ends are named
  *narrow ◂——▸ wide* (`min_label` / `max_label`; the share code keeps the number). Stone and iron
  levels are **Sparse · Moderate · Rich** (the code value too: `moderate`). **Seam scatter is off the
  screen** — the three `seam_*_scatter_percent` keys stay config, a modder's dial. **Forest cover runs
  0–100 %** — past the measured line (§7) on purpose: a bare or a solid valley is a hard setting he
  offers knowingly. **The diagonals' cost is accepted** (§7), and **the preview's redraw speed is
  fine** (§3). The button reads **Found a new village**.
- ⏸️ **Hills, height, slope, mesas, buttes, promontories — after the whole shell** (his call). *(Height and slope ✍️ specced, not built: `specs/heights.md`, D735.)* The
  sim has no height; it is a new layer of the world whose first question is what height *does*
  (walk cost in the one cost field? building and farming on a slope? the view's shading?). Its own
  spec, its own stage id, and then one more row on this screen.

## 3. What the player sees

```
┌──────────────────────────────────────────────┬───────────────────────────────┐
│                                              │ Seed  [mossy-lantern-41   ][⚄]│
│                                              │ Village name [Fernhollow     ]│
│        the valley, baked as in play          │ Valley       [River valley ▾] │
│        (ValleyTexture over a fresh world)    │ River width narrow ─●──── wide│
│        the founding marked                   │ River runs    [W–E ▾]         │
│                                              │ Forest cover  ──●──────  35%  │
│                                              │ Stone         [Moderate ▾]    │
├──────────────────────────────────────────────┤ Iron          [Moderate ▾]    │
│ Wooded 35% · Stone 12 seams · Iron 4 seams · │                               │
│ River 3 wide · Founded west of the river     │                               │
│                                              │ Share code [mossy-lantern-41#…]│
│                                              │ [Default settings] [Found a new village] │
└──────────────────────────────────────────────┴───────────────────────────────┘
```

- **The preview is the real valley**, not a sketch: `SimWorld.Create(config′, seed)` — measured at
  **1.3 ms a world** (20 shipped seeds, 2026-10-03; `MapGenerator.Generate` alone 1.1 ms) — painted by
  the same `ValleyTexture` the game uses, with the founding's buildings on it. ⚠️ **The bake is the
  cost, not the world** (16 px a tile, value noise per pixel): measure it first; if it is over ~50 ms
  the preview bakes at a lower `PixelsPerTile` while a slider is dragged and at full resolution when
  it is let go. Never a second painter — one look, or the preview lies about the valley.
- **Stats are read off the generated valley**, never restated from the sliders (`NewGame.Summarise`):
  wooded share of the land, stone seams and tiles, iron seams and tiles, the river's width and which
  way it runs (`any` resolved). ⚠️ The wooded share is *of the land* as generated, so it reads below
  the forest-cover setting (29 % at 35 on one seed) — the setting is what the generator aims at
  across the valley, and seams and the founding take ground from it.
- **The founders' camp is ringed** on the preview (`starting_residential_radius`).
- ✅ **Built (D479): `NewGameScreen`** — the screen is the game's first node; *Found the village*
  hands `Main.FoundTheVillage` the applied config and the share code, which runs what `_Ready` used
  to. **The preview bakes at 8 px a tile** (`ValleyTexture(pixelsPerTile)` — the same field sampled
  at fewer points, the game's 16-a-tile bake byte-identical, its self-check green) and **waits
  0.12 s after the last change** before it bakes, so a dragged slider bakes once. Measured in the
  probe: **~320 ms a bake** with the machine busy running §7's harness (the game's own whole-valley
  bake read 1,090 ms in the same run) — a pause after letting go, not a stutter while dragging.
- **Found** creates the world from exactly the settings on screen and starts the game. The header's
  seed line becomes the **share code** (§5), beside the version and the log path — together they
  are what reproduces and explains a run (METHODOLOGY §4).

## 4. The seed

`SeedText.ToSeed(string text) → ulong`, in `Kith.Sim` (pure, pinned by tests — a seed somebody
wrote on a forum must give the same valley forever):

1. **Normalised:** trimmed; runs of whitespace become one space; lower-cased (invariant culture).
   *"Mossy Lantern"* and *"mossy  lantern "* are one valley — a seed is read aloud and retyped.
2. **A plain number stays that number** — all digits and fits a `ulong` → that value. So **12345 is
   still the shipped valley and 41219 is still Joe's**, and every seed in `§7` still means what it says.
3. **Anything else is hashed:** `state = 0`; for each UTF-8 byte `b`: `state = SplitMix64.Fold(state, b)`;
   then `state = SplitMix64.Fold(state, byteCount)`. D466's shape; the one copy of `Fold`.
4. **Empty is refused** in words: *"Type a seed, or roll one."*
5. `#` cannot be part of a seed — it starts the settings (§5).

**The dice** roll `word-word-NN` from `seed_words` (content in the config, like `town_names`; a
modder replaces the list). ⚠️ The roll is the **view's** randomness (`System.Random`) choosing an
*input*; it never reaches the sim, which only ever sees the text. `BannedSymbols.txt` binds the sim,
not the view.

**The village name** defaults to the seed's pick (`SimWorld.Name`, unchanged) and may be typed over:
`village_name` (null = the seed's pick), trimmed, 1–24 characters, otherwise the seed's pick. ⛔ A
label, **never hashed** and never in the share code — the household-rename rule (D376): a name
changes nothing that happens.

## 5. The rows, and the share code

**A row is data** — `new_game_options`, a config list (`NewGameRow`): the code's defaults are the six
below (`NewGame.DefaultRows`), and a json's list replaces them wholesale, as `town_names` does. One
entry per row:

| id | label | kind | what it sets | default |
|---|---|---|---|---|
| `river` | River width | range, ends named *narrow* / *wide* | `river_width_tiles` | shipped (3) |
| `flow` | River runs | choice | `river_course`: `we` · `ns` · `nwse` · `swne` · `any` | `we` |
| `hills` | Hilliness | levels | `hill_scale_percent`: Gentle 50 · Rolling 100 · Hilly 200 (D735, `heights.md §5`) | Rolling = shipped |
| `woods` | Forest cover | range, 0–100 % in 5s | `forest_coverage_percent` | shipped (35) |
| `stone` | Stone | levels | `stone_seam_count` + `extra_stone_seams` per level: Sparse · Moderate · Rich | Moderate = shipped |
| `iron` | Iron | levels | `iron_seam_count` + `extra_iron_seams` per level | Moderate = shipped |
| `nomads` | Nomads | levels | `nomads_enabled`: *Come to the valley* (`on`) · *Never come* (`off`) — where `SimWorld.NomadsEnabled` starts (D570, `nomads.md §4.1a`) | on = shipped |

- ~~`scatter` · Seam scatter~~ — **off the screen (D481).** Joe asked what it did (how far the stone and
  iron outcrops stray from an even, compass-like layout; 0 % is D475's refused cross) and chose to
  drop it. The `scale` kind stays for modders (`AScaleRowScalesEachOfItsKeys`).
- A `range` or `scale` row may name its ends — `min_label` and `max_label`, both or neither — and then
  the screen shows the words at the slider's ends and no number; the share code keeps the number.

- A row names **config keys**, a range (min, max, step) or its levels, and nothing else. Applying the
  screen is **overriding those keys on the loaded config serialised to json, and loading the result**
  (`NewGame.Apply` → `SimConfigLoader.Parse`) — the same `SimConfig.Validate` as any config, so a
  modder's row and a shipped row are one mechanism. ⛔ **A row naming a key the config does not have
  is refused** at validation (the serialiser ignores an unknown key, so it would be a slider that
  moves nothing). A `levels` row whose levels all differ from the config reads `custom` and sets
  nothing. ⚠️ Measure the apply against the bake in the view slice.
- **Default settings** puts every row back to the value in the loaded config file — *the shipped
  valley* is the defaults plus whatever seed is in the box.
- ⛔ **The ranges and levels are measured, never typed** (§7). §10.4's rule binds every end of every
  row: a setting may make a valley leaner, never make valleys that cannot be lived in at a rate the
  survival guard would refuse.

**The share code** is the seed text, `#`, then **every row** as `id=value`, comma-separated, in the
rows' order:

```
mossy-lantern-41#river=3,flow=we,woods=35,stone=moderate,iron=moderate,nomads=on
```

- **Every row, always** — not only the ones that differ — so a code still reproduces its valley after
  a later build moves a default. (A different build may still generate differently; the version
  sits beside the code in the header, which is why.)
- **Pasting a code into the seed box sets the rows.** Text with no `#` is a bare seed and leaves the
  rows alone. An unknown id, a value outside a row's range, or a missing `=` is refused in a sentence
  naming the part (*"`woods=105` — forest cover goes from 0 to 100"*), and nothing changes.
- **One code is one valley:** `SeedText` and the row values are all the generator reads that the
  config file does not, so code + build + config file ⇒ the same valley, byte for byte (guarded, §8).

## 6. River direction — the one generator change

`river_course` is read by **the River stage only** (id 1, D473). Its draws are unchanged in number and
order for `we`, so **W–E is today's river to the tile and no golden moves** (guarded).

- **`ns`** is the same carve with the axes swapped: the start column drawn in the middle band of the
  width, the course wandering −1/0/+1 a row, the width wandering across.
- **`nwse` / `swne`** carve the course along a diagonal through the middle band, its start offset
  drawn as W–E's start row is. ⛔ **A diagonal river has no ford.** The route field steps east,
  west, south and north only, and `LineOfSight` refuses a leg that squeezes through the corner where
  two water tiles meet — so a staircase is already uncrossable to both; the guard (§8.4) asks the
  real cost field and the real line of sight rather than trusting that sentence. The width is
  measured **across** the course, so a diagonal 3 is as wide to the eye as a W–E 3.
- **`any`** chooses one of the four by **hash of the River stage's seed** — not a draw, so `any`
  moves no draw either; a valley's direction under `any` is a fact about its seed.
- The founding (stage 2) is unchanged: it already settles on the biggest land mass, on dry ground
  (D472), whatever shape the water is. **Seams on the far bank stay where they are** — as with a W–E
  river today, some outcrops lie across the water until the village bridges it (D546, `bridges.md`).

## 7. Measured before a range is typed

On D420's harness (`ZzBase` + `summ2.py`, `ZZ_WIDE=1` for 100 shipped seeds, `ZZ_FIX=1` for 50 fresh
fixture valleys), **each row's candidate ends, one row at a time, everything else at default**,
against the shipped settings at fifty years:

| Row | Candidate range | What decides the end |
|---|---|---|
| River width | 0 (no river) – 6 | dead valleys and alive; 0 is a supported valley today |
| River runs | each of the four | dead valleys and alive per direction; the founding never on a bank |
| Forest cover | 20 – 50 % → **35 – 50 %** → **0 – 100 % (Joe, D481)** | food and timber — the economy derives from this key (`VillageEconomy`) |
| Stone | Sparse 4+4 → **4+6** · Usual (Moderate, D481) 4+8 · Rich 4+12 | three stone seams in reach in every valley (`quarry.md §3.1`) |
| Iron | Sparse 2+0 · Usual (Moderate, D481) 2+2 · Rich 2+4 | an iron seam ≥ 50 in every valley (the smithy's gift, D444) |
| Seam scatter | 0 – 100 % | 0 is the stamped cross Joe refused (D475) — offered, but it is his to keep or cut → **cut (D481)** |

- An end that kills valleys at a rate `MostSeedsProduceAValleyAVillageCanLiveIn` would refuse is
  pulled in until it does not, and the table of what was measured goes in this spec beside the
  number typed.

**✅ Measured (D480)** — 100 shipped seeds (200–299) × fifty years, unattended, one row at a time;
*dead* is a valley with nobody left. The survival guard's line is 25 % dead (18 of 24 must live).

| Arm | Alive | Dead valleys | Peak | Starved | Froze | Typed |
|---|---|---|---|---|---|---|
| shipped (baseline = D475's, to the person) | 533 | 22 | 948 | 355 | 65 | |
| river 0 (no river) | 566 | 24 | 972 | 334 | 73 | **0** ✓ |
| river 6 | 490 | 24 | 912 | 370 | 69 | **6** ✓ |
| runs N–S | 541 | 26 | 933 | 316 | 76 | ✓ |
| runs NW–SE | 429 | 30 | 851 | 360 | 77 | ✓ ⚠️ |
| runs SW–NE | 567 | 24 | 936 | 297 | 66 | ✓ ⚠️ |
| *second sample, seeds 300–399:* shipped | 539 | 21 | 944 | 372 | 48 | |
| *… NW–SE* | 450 | 25 | 928 | 388 | 64 | |
| *… SW–NE* | 505 | 28 | 946 | 350 | 88 | |
| forest 20 % | 235 | **53** | 668 | 255 | 166 | ✗ |
| forest 25 % | 356 | **42** | 789 | 314 | 117 | ✗ |
| forest 30 % | 471 | **30** | 885 | 325 | 85 | ✗ |
| forest 50 % | 642 | 15 | 1,063 | 391 | 32 | **50** ✓ |
| stone sparse (4 + 4) | 530 | 23 | 956 | 356 | 65 | ✗ — see below |
| stone rich (4 + 12) · iron sparse (2 + 0) · iron rich (2 + 4) | 533 | 22 | 948 | 355 | 65 | ✓ |
| seam scatter 0 % | 566 | 26 | 956 | 357 | 56 | **0** ✓ |

- **Forest cover was typed 35–50 % (D480); Joe opened it to 0–100 % (D481).** Below the shipped 35
  the unattended valleys die past the guard's line (30 % loses 30 of 100, 20 % loses 53), and nothing
  below 20 or above 50 was measured. ⚠️ **He chose it knowingly** — a lean or bare valley is a hard
  setting, the harness is not a player (D447). What 0 % and 100 % do in play is unmeasured; a session
  that measures them writes the arms here.
- ⚠️ **The diagonals cost about five valleys in a hundred** — pooled over 200 seeds, NW–SE 55 dead and
  SW–NE 52 against west–east's 43 (27 % / 26 % against 21.5 %), just past the guard's line. The first
  sample's NW–SE 30 against SW–NE 24 was noise (the second sample reverses it). Kept, because Joe
  asked for them; ✅ **he accepted the cost (D481).** The likely reason is shape: a diagonal
  through the middle leaves the founders a triangle of valley, not a half.
- **Stone and iron levels do not move an unattended village** — rich stone and both iron levels read
  the shipped numbers *to the person* while every one of the hundred final state hashes differs: the
  valleys changed, but an unattended village never walks to its second ring of stone or to its iron.
  What decides those rows is the generator's promises, now guarded at **every row end**
  (`SeamsTests.EveryIronSeamHoldsFifty` / `EveryValleyKeepsThreeStoneSeamsInReach`, ten poses each):
  - **Sparse stone at 4 + 4 broke "three stone seams in reach"** (seed 46: two). **4 + 6** keeps it —
    the leanest of 64 valleys has four (red: back to 4 + 4, **1**).
  - **A wide or diagonal river drowned an iron seam below 50** (river 6, seed 5: **8**; NW–SE, seed
    25: 48). A seam still short after its three growth steps may now take up to three more; every
    seam that held within three grows exactly as before — **no golden and no terrain fingerprint
    moved** (red: back to three steps, **2**).
- ⚠️ **A harness is not a player (D447).** These measure that a setting can be lived in, not how it
  plays; Joe plays the ends.

## 8. Guards (each red-checked, the reds counted — D326)

*Built (D478) in `NewGameScreenTests` (44 cases); the reds are at the foot of this section.*

1. **Seeds are forever:** a stated list of texts → pinned `ulong`s (`"mossy-lantern-41"`, `"Joe"`,
   `"  joe "` equal to `"joe"`, `"12345"` → 12345, a multi-byte UTF-8 name). Red: drop the lower-casing;
   drop the length fold; parse digits as text.
2. **Defaults reproduce the config valley:** the screen's settings at default + seed 12345 ⇒ the same
   `GoldenMapHash` as `SimWorld.Create(shipped)`. Red: a row's default read from the code instead of
   the file.
3. **W–E is today's river:** `river_course` `we` ⇒ byte-identical terrain on the per-seed
   fingerprints. Red: carve W–E through the new code path with one extra draw.
4. **A diagonal river has no ford:** for every seed in a sample and both diagonals, the founding's
   land mass (the cost field's reachable set) holds no tile on the far bank, and no string-pulled leg
   between two reachable tiles touches water. Red: carve the diagonal with a gap a tile wide.
5. **The founders never settle on a bank, in any direction** (D472's guard, run per direction).
6. **`any` takes no draw:** the River stage's draw count is the same under `any` as under the
   direction it chose. Red: pick `any` with `rng.NextInt(4)`.
7. **The share code round-trips:** settings → code → settings, for every row at both ends; a code
   with an unknown id / an out-of-range value / no `=` is refused with a sentence and changes nothing.
8. **Every row's ends validate:** each row at its min and max loads through `SimConfig.Validate`.
9. **The probe** (D169): `new game:` — the screen's right column fits 1280 at its widest pose (the
   longest error sentence showing), the preview is baked and non-empty, and the screen is skipped
   into a default founding so every existing probe line still runs. `bar height` stays 151 *(183 today —
   CLAUDE.md carries the live number, A120)*.

**The reds (D478), one mutant at a time:** §8.1 no lower-casing **4**, no length fold **6**, digits
hashed as text **5**; §8.2 a row's default from the code instead of the config **3**; §8.3 one extra
draw in the west–east carve **4** (`DrawOrderIsTheContract` and three terrain fingerprints — guard 3
*is* the existing goldens); §8.4 a gap of **one** line across a diagonal **0** — *written down: a
one-line gap is not a ford; the land either side meets only at corners, which neither the route
field nor `LineOfSight` will pass* — and a gap of two lines **2**; §8.5 the dry rank removed **3**;
§8.6 `any` spending a draw **1**; §8.7 an unknown id ignored **1**, the range unchecked **2**, the
code dropping rows at their default **1**; §8.8 a river row reaching −1 **0** and a forest row
reaching 150 % **1** — ⛔ **both loaded without a word: `SimConfig.Validate` bounded neither key**,
though a comment above the seam checks said forest cover *"has its own guard below"* and it had none.
Bounded now (forest 0–100, river widths not negative; zero stays legal, the generator tests' bare and
riverless controls): **1** and **2**. Also: a key the config lacks allowed **1**, the typed name
ignored **1**, the scale row ignored **1**.

## 9. Order of work

1. ✅ **This spec** (D477).
2. ✅ **The sim half** (D478) — `SeedText`, `seed_words`, `village_name`, `new_game_options`, the share code,
   `river_course` (W–E untouched), guards 1–8. **No golden should move**; `git diff` says so.
3. ✅ **The measurements** (§7, D480), the ranges typed with their tables.
4. ✅ **The view** (D479) — the screen at launch, the preview, the header's share code, guard 9.
5. Joe plays it: rolls, types a seed he remembers (*41219*), drags every row to both ends, pastes a
   code, founds a valley.

## 10. Out of scope, on purpose

- **Hills and height** — after the shell (Joe, D477), a stage and a row of their own. *(✍️ Specced, not built: `specs/heights.md`, D735 — the row is *Hilliness — Gentle / Rolling / Hilly*, default *Rolling*.)*
- **A second archetype** — the Valley row shows one.
- **Remembering the last settings** — ✅ done by *settings persistence* (D504–D505,
  `settings-persistence.md §4`): the screen opens on the rows of the valley last founded, each through
  `NewGame.IsAllowed`, with a fresh roll of the seed; *Default settings* still means the config's.
- **A title screen** — ✅ built (D517, `title-and-pause.md`): the game opens on the title, and *New village*
  brings this screen up; it gained *Back* and gave *Continue* and *Load…* to the title.
- **Save/load** — will want the share code (and the name) in the save; this spec only makes sure
  there is one string that names the valley.

## 11. Definition of Done

1. This spec current, with §7's tables filled in.
2. Guards 1–9 written, red-checked, the reds counted; determinism green; no golden moved.
3. View builds with 0 warnings; probe green, `bar height` 151, `new game:` ✅.
4. Joe has played it (§9.5).
5. DESIGN §6 and §7, and `handoff.md`, updated in the same commits as the code.

---

## 12. The restyle — the map fills the screen, one parchment panel (A147, 2026-11-21; ✍️ proposed, ⏸️ Joe's yes)

**Joe:** *"Yes, queue it"*, *"New valley first"*, *"Start it"* — a layout from a claude.ai mockup
(`queued-new-valley/mockup.jpg`, outside git), amended with him before it reached this lane. **The mockup is
inspiration for layout, grouping and mood, not a pixel spec; where it disagrees with the in-game look, the game
wins.**

### 12.1 What does not change (and how each is held)

| Held | How |
|---|---|
| Every setting, its options, defaults and ranges | the rows are `new_game_options` exactly as today (§5); the screen lays them out and nothing else |
| World generation and determinism | no file under `src/Kith.Sim/World/` is touched; the goldens grep is identical before and after |
| The share code (an old code loads the same valley) | the code is still the rows in their order (§5) — grouping is display only; §8.7's round trip stays green and gains a case for an old code from before the restyle |
| Roll, Copy, Back, Defaults, Found | the same handlers; *Default settings* only changes its words to **Defaults** |
| The preview's picture | step 1 changes where the bake is drawn, not the bake: `ValleyTexture` untouched, 8 px a tile, 0.12 s after the last change |

### 12.2 What the player sees (step 1)

```
┌──────────────────────────────────────────────────────────┬─────────────────────────┐
│                                                          │  A new valley           │
│                                                          │  Seed                   │
│                                                          │  [flax-oak-93    ][Roll]│
│          the valley, as the game paints it,              │  Village name           │
│          filling the screen behind the panel             │  [Greyford             ]│
│                                                          │  ─────────────────────  │
│                     ◎  ← the founding, in the middle     │  The land               │
│                         of the room left of the panel    │  Valley      [River ▾]  │
│                                                          │  River width ──●──      │
│                                                          │              narrow wide│
│                                                          │  River runs  [W–E ▾]    │
│                                                          │  Hilliness   [Rolling ▾]│
│                                                          │  ─────────────────────  │
│                                                          │  Its riches  (… scrolls)│
│ ┌──────────────────────────────────────────────────────┐ │ ═════ pinned ═════════  │
│ │ Wooded 29% of the land · Stone 12 seams, … · River 3 │ │  Share code             │
│ │ wide, south-west to north-east (the seed's choice)   │ │  [flax-oak-93#river…][⎘]│
│ └──────────────────────────────────────────────────────┘ │  Back  Defaults [FOUND] │
└──────────────────────────────────────────────────────────┴─────────────────────────┘
```

- **The map fills the screen.** The bake is drawn so the valley's full height fills the window (at 16:9 a 120 × 80
  valley is wider than the room left of the panel, so its sides run under the panel or off the screen). Where the
  window shows past the valley's edge, the parchment shows (the theme's paper colour), never black.
- **The camera is offset so the founding (the red ring and dot) sits in the middle of the room left of the
  panel, never under it** — ✅ call 3: the camera stops at the valley's edge, so the founding sits as near the
  middle as the valley allows.
- **One panel, docked right, full height, 28 % of the window** (inside Joe's 25–30 %), never narrower than its
  widest row — and never past his 30 % (the probe's line): at 1280 the widest row, *River runs* with *North-west to
  south-east*, takes it to 371 px, 29 %: the in-game window sheet (`UiLook.Panel()` — A113's painted sheet, A116/A118's inked edge, A124's
  paper), ink words (A113), the rubric accent (`ParchmentLook.Rubric`).
- **The rows: label left, control right**, except **Seed** and **Village name**: label above, the field the
  panel's full width (Roll beside the seed). The refusal sentence stays under the seed, in rubric ink.
- **Groups, a thin rule between them** (the theme's own `HSeparator`, `controls/rule-h.png`):
  - *(no heading)* Seed + Roll, Village name
  - **The land** — Valley, River width, River runs, Hilliness
  - **Its riches** — Forest cover (and its %), Stone, Iron
  - **Its people** — Nomads
  - **Footer, pinned:** the share code + Copy; then Back, Defaults, **Found a new village**.

  ⭐ That order is the rows' own order (river, flow, hills, woods, stone, iron, nomads), so the screen reads in
  the share code's order. ✅ Call 2: a row's group is data — `NewGameRow.Group`, `"group"` in its json.
- **A slider's named ends** (River width's *narrow* / *wide*) sit under the slider's two ends, small, instead of
  either side of it — the panel is ~360 px at 1280, and the words beside it would leave the slider ~90 px.
- **The sliders are skinned** in the theme (today they are Godot's grey): a thin ink groove, darker where it is set,
  and an ink disc to hold. (The in-game bar's painted `bar-track` was tried first and read as nothing on the sheet at
  1280.) ⚠️ This reaches the only other sliders, the grass lab's.
- **Found a new village is the one primary button:** filled rubric, paper-coloured words, larger (20 px against the
  panel's 16). **Back** and **Defaults** are the theme's quiet ink buttons.
- **The share code** stays the panel's normal size and is cut with an ellipsis where it runs out — never shrunk;
  its tooltip and *Copy* carry all of it.
- **At 1280 × 720 the settings scroll and the footer stays pinned:** the title, then a scroll of the seed and the
  groups, then the footer, which never scrolls.

### 12.3 Type

- **Grenze Gotisch (blackletter) for the title and the three group headings only.** (In the game Metamorphous is
  the header face, A120; on this screen Joe's ask puts the blackletter on the headings.)
- **Everything else in a face that reads clearly, dark ink on parchment** — ⏸️ call 1, his pick from a label set in
  each candidate (`D:\Projects\brushes\ui\new-valley\fonts-16px.png`, `fonts-22px.png`). Metamorphous is decorative
  too and is shown as a candidate, not assumed. **On this screen only**: the game's own windows keep A120's pair (a
  new body face in the game moves the bar's measured height, 183, and is its own piece).

### 12.4 The summary strip

Today's line under the preview moves to a small parchment slip at the map's bottom-left (the theme's `top_box`,
A124's long slip). **It still reports what was generated, read off the world** (`NewGame.Summarise`), never restated
from the settings. **When River runs is *Any way (the seed's)*, it says which way the seed chose**, and that it was
the seed's: *"River 3 wide, south-west to north-east (the seed's choice)"*. A valley with no river says *"no
river"*.

⏸️ Call 2: the sentence moves into the sim's `NewGame` (pure, beside `Summarise`) so a test can pin it.

### 12.5 Step 2 — the map's look (after Joe plays step 1)

Put to him plainly after step 1, not before: **(a) today's painter as it is** — what you see is what you get;
**(b) map marks over the game's own picture** — hill hachures, stone and iron glyphs, drawn over the bake, the bake
unchanged; **(c) a hand-drawn cartographer's map** — a second painter to keep in step with the ground art as it
changes. Whichever: stone and iron distinguishable at a glance, and every setting visibly changing the map. The bake
is re-measured before any threading is proposed.

### 12.6 ✅ Joe's calls — ALL AS RECOMMENDED (A148, 2026-11-21, *"All as recommended"*)

1. **The body face** (§12.3) — from the sample sheet. *Recommended: Cardo* (a book face; plain capitals and lining
   figures, reads at 16 px). It is in the untracked `fonts/` folder; picked, it is copied into
   `src/Kith.Game/fonts/Cardo/` with its OFL licence, as A119–A120's were.
2. **The files outside the art lane** (HANDOFF 🅿️ ART's table): `NewGameScreen.cs` (the screen itself — needed);
   `src/Kith.Sim/Config/NewGame.cs` and `tests/Kith.Sim.Tests/NewGameScreenTests.cs` for the strip's sentence (§12.4)
   and a `group` on each row — *recommended: the group is data* (a row's `"group": "The land"`, the heading's own
   words; rows gather under their heading in the rows' order; a modder's row picks its heading in its line of json,
   D477's *"a new row is a config key and a line of json, never a new screen"*); and **one line in `Main.cs`**, the
   `new game:` probe calling the screen's own layout checks (§12.7). `ValleyTexture.cs` is **not** touched in step 1.
   ⚠️ *As built it is two lines:* the probe runs at 1280 × 800 (`project.godot`), so one line poses the screen at
   720 high (frame 5) and the other reads its checks (frame 20).
3. **A founding near the valley's edge** — *recommended: (a)* the camera stops at the valley's edge, so no bare
   paper ever shows; the founding then sits as near the middle as the valley allows (the probe checks its ring stays left of the panel).
   **(b)** always the exact middle, and a band of plain parchment shows past the valley's edge when the founding
   is off-centre.
4. **Step 2** waits for his play of step 1 (§12.5) — nothing to answer now.

### 12.7 Guards (red-checked, the reds counted — D326)

- **Unit (`NewGameScreenTests`):** the strip's sentence — a W–E river, *any* resolved and named as the seed's
  choice, no river; the shipped rows' groups in order (land: river, flow, hills; riches: woods, stone, iron;
  people: nomads); **an old share code** (today's exact text, pinned) loads the same settings and the same map
  hash after the restyle (its own pinned hash, `OldCodesMapHash` — the same with every group stripped, so the
  headings are display only).
- **Probe, `new game:`** keeps its claim (the panel ends inside the window, the longest refusal showing, a preview
  baked) and adds (`NewGameScreen.LayoutFaults`): at 1280 × 720 the footer's *Found* button is wholly on screen
  with the settings scrolled to their top, and not inside the scroll; the founding's ring, under the camera the map
  was placed with, lies wholly left of the panel; a founding at the valley's east edge would be marked left of it
  too; and the panel is no wider than 30 % (the share code never widens it). `bar height` stays 183, `done.`
  printed.

### 12.8 Not followed from the mockup, and why

- **The hand-drawn map, compass rose, hachures and stone glyphs** — step 2's choice (§12.5), not step 1's.
- **The close-up view** — the mockup shows about a third of a valley; the whole valley's height is kept, so a
  player judging a valley sees all its stone and iron.
- **The strip's words** (*"Stone sparse · Iron rich · River wide"*) restate the settings; the game's strip reads the
  generated valley (seams and tiles), as Joe asked to keep.
- **The order inside the groups** (the mockup has River runs before River width, Forest cover last) — Joe's list,
  which is also the share code's order.
- **The illuminated initial on the title** and **the die on Roll** — no painted initial or die exists; plain words
  until one is painted.
- **The small share code** — normal size, cut with an ellipsis (Joe: never shrink it).
- **The two-line *Default settings*** — one word, *Defaults* (Joe's rename).

### 12.9 🔨 Step 1 as built (A148, 2026-11-21; Joe: *"All as recommended"*), ⏸️ his play

- **`NewGameScreen.cs`**, its layout rewritten; every handler (Roll, Copy, Back, Defaults, Found, the seed box, the
  rows) is as it was, and every rule still the sim's. The map: the bake at 8 px a tile in a `TextureRect` placed by
  `Camera` — the valley's height fills the window (9 px a tile at 1280 × 720), the founding moved to the middle of
  the room left of the panel and stopped at the valley's edge. The ring is drawn under the camera the map was placed
  with (`RingOfTheFounding`), so a camera bug cannot leave the ring on the right spot and the map on the wrong one.
  Nothing is placed per frame: `PlaceEverything` runs on a resize and when the valley or the strip's words change.
- **The panel:** `UiLook.Panel()`, 28 % (371 px at 1280, 29 %: *River runs* with *North-west to south-east* is the
  widest row), a scroll between the title and the pinned footer; one label column across every group (measured once
  the panel is in the tree — before that a label measures in the default face). The village name's placeholder (the
  seed's own name) is inked like a caption; the theme's hint was near-invisible on the field.
- **The sim:** `NewGameRow.Group` (`"group"`), set on the seven shipped rows; `NewGame.Describe`, the strip's
  sentence. Display only — the share code, `Apply` and the generator are untouched.
- **The theme (`ParchmentLook.NewValley`):** `backdrop` (the paper base `#e8dbc3`), `reading_font` (Cardo, copied
  with its OFL into `fonts/Cardo/`), `blackletter`, the `PrimaryButton` variation (rubric fill, paper words 6.4 : 1,
  8.5 held; 20 px), and the sliders (an ink groove, an ink disc grip). `UiLook.ReadingFace` / `Blackletter` /
  `PrimaryButton`; on slate each is null or unnamed, so the screen falls back to today's look.
- **`Main.cs`, two lines** (§12.6): `PoseTheSmallestWindow()` at frame 5, `LayoutFaults()` read at frame 20.
- **The bake, re-measured** (the probe, beside the suite): **408–650 ms** a bake (the spec's ~320 was with the
  harness running). Unchanged code; it is the number step 2 starts from.
- **Guards, red-checked (D326), one mutant at a time:**

  | Mutant | Reds |
  |---|---|
  | the strip's *(the seed's choice)* dropped | **2** (`TheStripSaysWhatTheValleyHas`, `UnderAnyTheStripNamesTheSeedsCourse`) |
  | the strip names the setting's course, not the valley's | **2** |
  | *Forest cover* under *The land* | **1** (`TheRowsGatherUnderTheirHeadingsInTheShareCodesOrder`) |
  | the share code's separator `,` → `;` | **3** (the old-code test among them, alone: 1) |
  | *Hilly* 200 → 190 (the generator moves) | **2** (the old-code test among them, alone: 1) |
  | probe: the footer inside the scroll | **1** line — *"Found scrolls with the settings"*, *"Found runs 743–783px in a 720px window"* |
  | probe: the camera centres on the whole window, not the room | **1** — *"a founding at the valley's east edge is marked at 1022px, under the panel at 909"* |
  | probe: the share code's `ClipText` removed | **0** — written down: `TrimEllipsis` alone already lets the label narrow, so the clip is belt and braces |
  | probe: the share code's clip and ellipsis both removed | **1** — *"the panel is 805px, past 384 (30%)"* |

- **Renders** (outside git): `D:\Projects\brushes\ui\new-valley\step1-1280-*.png`, `step1-1920-*.png` — the
  shipped seed and an *Any* code, at 1280 × 720 and 1920 × 1080.
