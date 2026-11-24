# The title's background: your newest village, aged like a painting (A228, A229)

> **Status: ✍️ SPECCED AND MOCKED (A229, 2026-11-23); ⏸️ FOUR CALLS JOE'S (§9); NOTHING BUILT.** Art lane (CLAUDE.md *Two tracks*),
> claimed A228. Joe's brief: `D:\Projects\brushes\title-snapshot\brief.md`. Mocks drawn by the game itself from a copy of his
> newest lived-in save: `D:\Projects\brushes\title-snapshot\` (`title_snapshot_looks.jpg`, `title_snapshot_detail.jpg`,
> `title_snapshot_founding.jpg`). Follows `title-screen-art.md` (A178–A191) and `logo.md` (A220–A227). View only: nothing in the
> sim, `data/` or `tests/` changes. **⚠️ The save-time hook is in the systems lane's files (§7) and waits for `slice/hud`.**

## 1. What Joe asked

Joe, 2026-11-23: *"instead of the seasonal slice background image, i would like the background to be a snapshot of the latest
save's village, with the KCD2 cracked texture applied."* His reference was five loading screens from *Kingdom Come: Deliverance 2*,
oil paintings with craquelure: the fine web of cracks in an old varnish, a faint canvas weave, the cracks catching light and grime.
Of the coordinator's four points he said *"i agree with everything you raised"*:

1. **Our own texture**, painted procedurally by the art lane. Nothing is taken from KCD2. Craquelure is a general painting effect.
2. **Both looks mocked on a real capture of his village**, and he picks by eye: **(a) oil craquelure**, **(b) aged parchment**
   (fold creases, foxing, worn edges — how parchment ages; Kith's map is watercolour on parchment).
3. **The picture taken at save time, never at the menu.** Each save keeps a picture and the menu shows the newest. **With no save**
   (a first launch) the title shows today's four seasons.
4. **Framed on the village's heart, in the season that save was in.**

**Kept as it is:** the parchment panel, *Kith*, the seal above it, the wax primary (A180, A223, A224). Only the picture behind
them changes.

## 2. What the title shows

- **The picture of the newest village that has one** (⏸️ call 2): the title walks its save list (it already has it, newest first —
  `TheSavesOnDisk` reads each save's head for *Continue* and *Load…*, D805) and takes the first save whose village folder holds
  a picture. **No save is opened, and no extra save head is read.** One JPG decoded on the title's open (about 0.9 MB at
  2560 × 1440) and held for as long as the title is up.
- **No picture anywhere** (a first launch, or only saves from before this piece): **today's four seasons** (`valley_seasons.jpg`,
  A190) stay as the fallback, unchanged.
- **The surface over both** (⏸️ call 1): the picked look lies over the snapshot and over the four-seasons fallback, so the title is
  always the same kind of object.
- **A picture that will not decode** is logged loudly and the fallback stands. The title never refuses to open over a picture.
- Laid as today's is: covering the window, centred (`KeepAspectCovered`), so the heart stays right of the panel from 16:10 to 21:9.

**⚠️ What "newest" meant on his disk tonight** (`title_snapshot_founding.jpg`): his two newest saves are **foundings at tick 0**,
autosaved on quit a moment after founding (Stonebridge, 23:30; Hollowmere, 22:42), and four of his ten newest are. Read
literally, the brief would put an empty valley and a cart behind the title most evenings. Hence call 2: **a picture is written
only for a village with a home standing**, so the title shows his newest village that has one (tonight, Thornfield, Year 40).
*Continue* still opens the newest save, whichever that is; its tooltip names it, as today.

## 3. The snapshot — when, how big, where

- **When:** at every save the game writes: the autosave at the turn of the year, the autosave on *Quit to title*, *Quit to
  desktop* and the window's ✕, a named save from the pause screen, and F5's quicksave. A picture that fails to write is logged
  (`shell`, as `CouldNotSave` does) and **never fails the save**: the save is written first, the picture after.
- **What is drawn:** the live world at that moment, **by a second map** — a `VillageMap` in an offscreen `SubViewport`, handed
  the world once (`Present`), its camera set (`LookAt`, the new-valley preview's door, A150), drawn once, read back, freed. So
  **the player's screen never changes**: no HUD to hide, no camera to move, no flash. It is the capture tool's way of drawing
  (A189) without a window of its own. ⚠️ It must not be a *preview* (`IsAPreview` draws nothing a village puts down): a still of
  the village, its markers and overlays off, the watercolour on whatever the player's own tick says (a picture is painted).
- **The season** is the world's own — the save's — through the season the map already draws; nothing is overridden.
- **Size: 2560 × 1440** (16:9, his screen), **JPG at quality 90: 0.92 MB** for Thornfield (85: 0.73 MB). Encoding it took 12 ms
  in the mock's script.
- **⚠️ Cost — owed by the build, measured before it is wired:** a second map's first draw bakes its own valley (the ground, the
  watercolour layers) and that may take hundreds of ms. **The budget: a year's-turn autosave hitches no more than today's**
  (measured on his Thornfield, before and after, the frame log's `[frame]` lines). If the second map is over it, the picture is
  taken only at the quits and the named saves — they already pause the game — and the folder's picture then lags the year's-turn
  autosaves by at most a session. That fallback would be put to Joe, not chosen silently.
- **Where: one picture a village, `title.jpg` in the village's own save folder** (`saves/<village>-<seed>/`), rewritten at each
  save (⏸️ call 3). The brief says *"each save and autosave keeps a picture beside it"*; one a folder is the same answer for the
  title, which only ever shows the newest, and it needs no change to the sim's save code: autosaves rotate by renaming inside
  `SaveFile.WriteAutosave` (`Kith.Sim`), so a picture per save file would have to rotate with them there — a sim edit that runs the
  suite, for pictures the title never shows.
- **⛔ The probe never writes one** — the same rule as the saves (`_saveFolder` is `null` under it).

## 4. The framing

- **The village's heart = the middle of its lived-in homes**, the map's own framing rule (`CentreOnTheVillage`: the box round the
  homes of households with someone living). With no home standing there is no picture (call 2).
- **The heart at (0.64, 0.50) of the picture** — the middle of the area right of the title's panel (28 % of a 16:9 window), as the
  four seasons' framing put it (A190).
- **The zoom: 32 px a tile at 2560** — the four seasons' 48 px a tile at 4K, as his 2560 screen shows it. A house reads; the
  village and its surroundings fit (the mock: Thornfield's 40 years in the right-hand two thirds, the river beside it).

## 5. The two surfaces (mocked; ⏸️ call 1 picks one)

Both are **three layers over the picture**, the shape a shader takes:

`out = desaturate(mix(picture, paper, FADE), SAT) × TINT + LIGHT`

- `FADE` — pigment worn back to the bare sheet (a crease's ridge, a rubbed edge, a fold's crossing);
- `TINT` — what darkens and colours it (the varnish, the grime in a crack, foxing, an edge's browning);
- `LIGHT` — what catches the light (a crack's far lip, a crease's ridge, an island's cupped rim), from the upper left.

Painted by `D:\Projects\brushes\title-snapshot\age.py` (seed 228), every size given at 2560 wide.

### (a) Oil craquelure

- **The varnish:** a warm amber glaze, uneven in large soft patches, deeper toward the edges and corners where hands and smoke reach;
  the colours a little dulled (`SAT` 0.80).
- **The canvas:** a plain weave a little under 5 px a thread, its threads not quite even, ±3 % — seen up close, not across a room.
- **The cracks:** islands about 60 px across, their walls wandering and jagged, slightly longer across than down (a canvas's
  grain); a finer web inside about half of them; hairlines in about a third. Some walls never parted. Each crack dark with grime
  (umber), its far lip catching the light, each island's rim cupped up a little.
- **Grime** settled in the low places and the corners.

### (b) Aged parchment

- **The sheet yellowed** and the paint faded back toward it a little (`FADE` 0.10–0.16, `SAT` 0.88); a fibre grain, ±2.5 %.
- **Folds:** a map folded in four across and in three down — the folds are the sheet's (at ¼, ½, ¾ of the width and ⅓, ⅔ of the
  height), **so the heart at (0.64, 0.5) is never on one**. Along each: the paint rubbed back to the paper on the ridge, a dark
  line in the valley beside it, the sheet bending either side; worn through to bare paper where two cross.
- **Worn edges:** the paint rubbed off the outer edge by handling, the corners most; the edge browned, raggedly, ~95 px in.
- **Foxing:** rust-brown spots, each a soft blot with a darker rim, clustered and thicker toward the edges; fine flecks.
- **One old water stain**, top right: a pale tide with a brown line where it dried.

### Built as

**The picked look baked from `age.py` itself**, so what Joe picks is exactly what ships: its layers at 2560 × 1440 into
`textures/ui/title/` (`aged-<look>.png`, `TINT` in RGB and `LIGHT` in alpha — **5.9 MB for the oil, 3.4 MB for the parchment**,
plus an 0.2 MB `FADE` mask for the parchment; only the picked look is committed), and a ~20-line canvas shader,
`shaders/title_aged.gdshader`, on the backdrop's `TextureRect`, compositing them over whichever picture is shown. The painting
script moves into `tools/title-aged/` (outside the exported game and the suite) so the layers can be re-baked. A procedural
shader (no layers) was weighed: crisper at 4K, no asset, but a port of the picked look rather than the look itself — it would need
his eyes again. The bake stands at 2560; a 4K window draws it at 1.5×.

## 6. What Joe will see

`.\run.bat`: the title opens over **his newest village with a home**, framed on its houses right of the panel, in the season he
left it, aged; *Continue* opens his newest save. Play a year and quit to the title: the picture has moved on a year (and to the
next season). Found a new village and quit at once: the title still shows the last village with a home. Rename the saves folder:
the four seasons, aged. **The title opens no slower** than today (one JPG, no save read).

## 7. Who builds which part

| Part | Files | Lane |
|---|---|---|
| The surface: layers, shader, painting script | `textures/ui/title/aged-*.png` (+ `.import`), `shaders/title_aged.gdshader`, `tools/title-aged/` | ✅ art's own |
| The title reads the newest picture and lays the surface | `TitleScreen.cs`, `TitleScreen.Probe.cs` | art built them (A180, A190, A223), outside the lane's table — ⏸️ call 4 |
| Drawing and writing the picture | a new `TitleSnapshot.cs` (the second map, the framing, the JPG) — no existing file changes | art (a new view file) — call 4 |
| **The save-time hook**: call `TitleSnapshot` after each save and autosave, and at the quits | **`Main.Saves.cs`** (`Autosave`, `SaveUnderAName`, the three quit paths) — ~10 lines | ⛔ **systems** |
| The `title:` probe line's new checks | `Main.cs`'s `ProbeTheTitle` asks `TitleScreen.Probe.cs`; ~3 lines if a new check needs a door | ⛔ **systems** |

**⛔ `slice/hud` is open in the main checkout and edits both `Main.cs` and `Main.Saves.cs`** (its step 7 writes a view section into
`CaptureTheVillage`; `git diff main...slice/hud`). So the hook **cannot be built until the HUD merges**, and then only as a
systems commit, or by the art lane on `art` with Joe's OK (call 4). Systems merges first.

**What can go ahead now, at his word on the look:** the layers, the shader, `TitleSnapshot.cs`, and the title showing the newest
picture with the surface (with no hook yet, it finds none and shows the four seasons aged — the fallback is the first thing built,
and Joe can play it).

## 8. The probe and the checks

- **`title:`** (in `TitleScreen.Probe.cs`, riding the existing line, art trap 73): over no save, the four seasons and the surface's
  material on the backdrop, its shader compiled (art trap 6: a material holds whatever it is told — the check reads Godot's
  `SHADER ERROR`-free compile, not a uniform read back); over a posed save listing whose scratch folder holds a `title.jpg`, that
  picture shown; a folder with none, the next older that has one; a picture that is not a JPG, the fallback and no throw.
- **`TitleSnapshot`'s pure parts** — the heart from a set of homes, the camera from the heart, "a home standing" — probed from a
  posed world, no disk: the heart at (0.64, 0.50) ± 1 px at 2560 × 1440, 32 px a tile; no home, no picture.
- **The hook** (systems, after the HUD): under the probe nothing is written; the `save:` line's scratch folder gets a `title.jpg`
  beside its save.
- Red-checked, the reds counted (D326). The four checks as `CLAUDE.md` says; **the suite by D846's rule** — this piece changes no
  sim, data or test file, so it is skipped and the record says so.

## 9. ⏸️ Joe's calls

1. **The look.** ⭐ **(b) Aged parchment**, over the snapshot and over the four-seasons fallback. It is the map's own medium, so the
   picture and the parchment panel read as one object; its marks sit at the edges and folds, leaving the village clear. The oil's
   crack web lies over everything, and its dark lines are the same weight as the map's own ink (paths, fences, contours), so it
   competes with them, most in the village itself (`title_snapshot_detail.jpg`). *Or:* **(a) oil craquelure** — the closer match
   to KCD2 and the more dramatic of the two.
2. **Which village.** ⭐ **The newest village with a home standing** — a founding writes no picture, so the title keeps showing
   his last real village. *Or:* the newest save, whatever it holds (tonight: an empty valley at tick 0).
3. **One picture a village** (`title.jpg` in its save folder, rewritten at each save), not one per save file. ⭐ **Yes** — the same
   picture on the title, and no change to the sim's save code.
4. **His OK for the files outside the lane** (§7): `TitleScreen.cs` / `.Probe.cs` and a new `TitleSnapshot.cs` — ⭐ **yes**, the art
   lane builds them now; and **the save-time hook in `Main.Saves.cs`** after `slice/hud` merges — ⭐ **the art lane makes it, on
   `art`, as its own commit, after the HUD's merge**, since it is ~10 lines and the piece is otherwise whole. *Or:* the systems lane
   makes it as its next commit after the HUD.

## 10. Order of work

1. ⏸️ His four calls. 2. The surface baked, the shader, `TitleScreen` showing the newest picture with the surface over it, the
fallback aged — the four checks — ⏸️ his look in the game (`.\run.bat`, the four seasons aged until a picture exists). 3.
`TitleSnapshot.cs` with its cost measured against today's autosave on his Thornfield. 4. After `slice/hud` merges: `main` into
`art`, the hook (call 4), the four checks — ⏸️ his play, then the merge at his word.

## 11. Not in this piece

The pause screen; the new-valley screen; motion on the title; a picture chosen by the player; re-aging the panel itself.
