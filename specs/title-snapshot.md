# The title's background: your newest village, aged like a painting (A228, A229)

> **Status: ✍️ SPECCED AND MOCKED (A229, 2026-11-23); round 1's looks turned down (*"the parchment looks like a quilt"*),
> round 2 mocked (A230, §5a: (c) a manuscript page, (d) deeper watercolour) and turned down (*"no, i hate them both"*);
> round 3 mocked (A231, §5b: (e) full-bleed watercolour, at two strengths); *Continue* lifting the paint into the live map
> specced (A232, §6a). ✅ **Joe chose (e) and the lift (A233): *"Yes, (e) and the lift"*.** 🔨 **The look built as two
> shaders and three baked textures (A233, §5b).** ✅ **Calls 2–4: *"Yes to all three"* (A234).** 🔨 **`TitleScreen` shows the
> newest village's picture painted, or the four seasons painted, and `TitleSnapshot` takes the picture (A234), on `art`,
> UNPLAYED.** 🔨 **The HUD merged (D885): the picture is taken at every save and way out, and *Continue* lifts the paint into
> the live map (A236); ✅ PLAYED by Joe: *"looks super cool"*, with three changes, built (A237). ✅ MERGED INTO `main` AND PUSHED at his word, *"merge art and push"* (A238); 🔨 the loading words on a pause-screen *Load…* and F7 too (A239), merged and pushed (A240);** ✅ **call 6 (§9): *"Yes, every save"* (A235).** Art lane (CLAUDE.md *Two tracks*),
> claimed A228. Joe's brief: `D:\Projects\brushes\title-snapshot\brief.md`. Mocks drawn by the game itself from a copy of his
> newest lived-in save: `D:\Projects\brushes\title-snapshot\` (`title_snapshot_looks.jpg`, `title_snapshot_detail.jpg`,
> `title_snapshot_founding.jpg`). Follows `title-screen-art.md` (A178–A191) and `logo.md` (A220–A227). View only: nothing in the
> sim, `data/` or `tests/` changes. **The save-time hook is in the systems lane's files (§7); the art lane made it, as call 4 said.**

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

**Kept as it is:** the parchment panel, *Kith*, the seal above it, the wax primary (A180, A223, A224 — red words since A242). Only the picture behind
them changes.

## 2. What the title shows

- **The picture of the newest village that has one** (✅ call 2): the title walks its save list (it already has it, newest first —
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
- **✅ Cost, measured (A234)** on his Thornfield at 2560 × 1440 (scratch `measure.gd`, vsync off, a plain frame 15 ms, an F5
  save under 24 ms but its first at 37 and 414): a second map from nothing held **one frame for 1.7 s**, almost all of it the
  ground's bake. **It now borrows what the live map has made** (`VillageMap.BorrowTheGroundOf`: the ground, the woods, the lanes
  and their field, the painted land's shapes, the buildings, the figures), renders once and is encoded off the main thread:
  **two frames of about 41 and 28 ms.** Against today's saves that is one or two frames longer, so **it is over the budget
  below, by about 25 ms** — ✅ taken at every save all the same (call 6, A235). The borrowed picture is the unborrowed one but for the villagers, whom the first build
  read before their figures were painted (mean 0.04 / 255 apart); it is now drawn until every figure is. The ground and the
  woods are shared rather than copied; the live map draws first in the frame. ⚠️ **No probe line guards the borrowing or the
  picture** (headless draws nothing): the hook's line is owed one (§8).
- **⚠️ Cost — as specced, before it was measured:** a second map's first draw bakes its own valley (the ground, the
  watercolour layers) and that may take hundreds of ms. **The budget: a year's-turn autosave hitches no more than today's**
  (measured on his Thornfield, before and after, the frame log's `[frame]` lines). If the second map is over it, the picture is
  taken only at the quits and the named saves — they already pause the game — and the folder's picture then lags the year's-turn
  autosaves by at most a session. That fallback would be put to Joe, not chosen silently.
- **Where: one picture a village, `title.jpg` in the village's own save folder** (`saves/<village>-<seed>/`), rewritten at each
  save (✅ call 3). The brief says *"each save and autosave keeps a picture beside it"*; one a folder is the same answer for the
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

## 5a. Round 2 (A230, 2026-11-24) — Joe: *"i dont really like any of them. the parchment looks like a quilt."*

He asked: *"how to make it look more like a freeze-frame of the village captured in an ancient manuscript? Or, lean even deeper
into the watercolor style?"*, with nine references: four illuminated leaves (a historiated initial on gold, a framed text page,
a page in acanthus borders, a vellum leaf with penwork sprays) and five watercolours (a harbour, a valley town, a castle on a
crag, a loose cottage vignette). **Round 1's (a) and (b) are withdrawn**: both laid an *effect* over a full-bleed map. Both new
directions change **what the picture is**. Painted by `D:\Projects\brushes\title-snapshot\look2.py` (seed 230); nothing is
copied from his references. Sheets: `title_snapshot_round2.jpg`, `title_snapshot_round2_detail.jpg`.

### (c) A manuscript page — the village as its miniature

- **The screen is a vellum leaf**: warm cream, a soft mottle, the skin's low cockle lit from the upper left, a sparse speckle of
  follicles, the outer margin a shade darker from handling. The title's parchment panel lies on it as a second sheet.
- **The village is a framed miniature** right of the panel (34–95 % of the width, 8–92 % of the height), the snapshot cropped
  round the heart, a touch richer in colour, the leaf's grain through it. **The frame**, outside in: an ink line, a band of
  burnished gold leaf (worn in places to the red bole), a lapis band with white tracery (a zigzag and dots), a vermilion fillet,
  an ink line. **Corner bosses**: gold squares, each with a lapis quatrefoil and a vermilion heart.
- **The margins: penwork** — an inked stem undulating along the top, bottom and right, a tendril spiralling off at every crest
  ending in a flower, a leaf or a gold dot, leaves in verdigris, lapis, vermilion and rose, hairline sprigs with gold dots between.
- **Built as:** the page, frame and margins are **one static picture** (`textures/ui/title/page-manuscript.png`), the snapshot
  set into its window. ⚠️ The mock's penwork is procedural and reads a little mechanical up close; the built margins would be
  **painted in Photoshop by script**, as every UI control is (`p2_chrome.jsx`'s way), with the mock's layout.

### (d) Deeper watercolour — finished at the heart, loose to the paper

- **Cold-press paper**, warm white, its tooth just visible.
- **The village painted as a vignette**: the snapshot as it is at the heart (the houses, roads and people crisp); outward it
  loosens into **flat washes** (the map brought down, its detail taken out by a median, so shapes keep their edges and lose their
  texture), each wash's edge a shade darker where the pigment ran, the colour diluted toward the paper. **The edge** is a
  ragged wet edge in places (dark where the pigment pooled as it dried) and feathered into the paper in others, shorter on the
  panel's side so paper shows between them. **Granulation** in the paper's tooth, a few pale backruns, splatters of the village's
  own colours flicked past the edge.
- **Built as:** a canvas shader on the backdrop (`shaders/title_watercolour.gdshader`): the washes from the picture's own mip
  levels, the edge, dilution and granulation from a baked mask and paper (`textures/ui/title/watercolour-mask.png`, paper tile).
  Unlike (c), the washes depend on the picture, so they cannot be one static image. ⚠️ The mock's median washes are a
  stand-in; the shader's will differ in detail and want his eyes again.

**Both keep §2–§4 as written**: the picture taken at save time, one `title.jpg` a village, framed on the homes' middle, the
season the save's own. The fallback (no picture) is the four seasons under the same treatment.

## 5b. Round 3 (A231, 2026-11-24) — Joe: *"no, i hate them both."*

He went on: *"Instead of either, i want you to lean into the watercolor style even more - get rid of the white paper
surrounding and have the game map fully go to the edges. just make the watercolor style more intense."* **(c) and (d) are
withdrawn.** Painted by `D:\Projects\brushes\title-snapshot\look3.py` (seed 231); sheets `title_snapshot_round3.jpg` (behind the
real title) and `title_snapshot_round3_detail.jpg` (1:1).

### (e) Full-bleed watercolour — the whole map painted, edge to edge

- **No border and no vignette**: the map fills the screen as it does today, painted all over. The heart is painted in finer
  washes than the outskirts, but nothing fades out.
- **Flat washes**: the map with its texture taken out by a median at three sizes (finer at the heart), so the shapes keep their
  edges. Each wash has **a dark rim where it dried**, with a halo of pigment fading inward from it.
- **Glazes**, the mark that reads most as watercolour: two broad, darker washes and one lift laid over the first wash, each a
  lobed shape. Where the paper was dry the glaze has a **hard edge with its pigment pooled unevenly along it**; where it was damp
  the edge is soft. They are quieter over the village.
- **Wet-in-wet colour drift** (each channel wanders a little over broad patches), **value mottling**, **brush strokes** at two
  angles, **backruns** (pale blooms with frilled dark rims), **granulation** in the cold-press tooth, a very few dry-brush skips
  in the outskirts, **the paper's relief** lit from the upper left, and splatters of pigment and of clear water.
- **The game's own ink lines** are lifted off the map before it is washed and laid back on top, so it reads as **line and
  wash**: houses, trees and the river bank stay drawn.
- **Two strengths**: **(e)** and **(e+)** (the same marks about half as strong again: darker glazes, more backruns, more
  splatter). The build takes one strength, or a strength between them.
- **Built as (A233), two passes:**
  - **`shaders/title_watercolour_wash.gdshader` — the washes, drawn ONCE** into a `SubViewport` the picture's size when the
    title opens. It is a generalised Kuwahara: eight overlapping sectors of a disc round each pixel, each a weighted mean,
    and the flatter a sector the more it counts. It reads the picture's own mips at a level set by the finish map: 0.9 at
    the heart, 1.8 between, 2.8 in the outskirts, set by eye against the mock. ⚠️ The square four-quadrant Kuwahara was
    tried first and **left blocks along the edges of the broad washes**. The round sectors with soft weights have none.
  - **`shaders/title_watercolour.gdshader` — the paint, every frame**, as the backdrop's material, about 45 taps a pixel:
    - each wash's dried rim (the washes' gradient) and an eight-tap halo of pigment fading in from it;
    - the baked marks;
    - a touch more colour and 5 % of the paper's light;
    - granulation in the tooth, more in the heavier washes;
    - the game's ink lines (the picture's luminance against its own mip 1.8), put back on top;
    - the paper's relief.

    **`strength`** runs 1 = (e), 0 = the plain picture: the lift (§6a) is that uniform run down.
  - **The baked textures** (`textures/ui/title/`, lossless, painted by `D:\Projects\brushes\title-snapshot\bake_wc.py`):
    - **`watercolour-wash.png`** (2560 × 1440 RGBA, 2.4 MB) holds every mark that does not read the picture: the glazes,
      the wet-in-wet drift, mottling, brush strokes, backruns, dry-brush skips and splatters. Each of these is an affine map
      of the colour under it (`col × M + paper × b`), so they are baked once as M (rgb ÷ 1.3) and b (alpha), by running them
      over black and over white. They are drawn from look3's own random stream, so **they fall exactly where the mock's
      did**.
    - **`watercolour-finish.png`** (640 × 360): how finished each place is.
    - **`watercolour-paper.png`** (512 × 512, tiles): the tooth, the fine tooth and the relief.
  - **Proved through the real title** (the scratch `title_wc.gd`, the shaders set on the backdrop from outside) on his
    Thornfield, at strength 1, ½ and 0:
    - at 1 it is the mock, a shade more painterly in the outskirts;
    - at 0 it is the plain picture: a mean difference of 1.1 / 255, all of it the 3840 capture scaled through mips, which
      a 2560 `title.jpg` shown 1:1 will not have;
    - building pass 1 and both materials takes 220 ms.
  - **Wired (A234):** `TitleScreen.ShowTheVillage` picks the picture, `PaintIt` lays the two passes over it. A village's picture
    is 2560 across where the look was tuned on a 3840 capture, so both shaders now measure every reach and mip in a 3840-wide
    picture's texels, whatever its size: the wired title over a 2560 `title.jpg` is **0.88 / 255** from the shot he approved.
    ⚠️ The fallback four seasons go through the same two passes, but their picture is not framed on a heart, so the finish
    map's fine heart lands on the valley picture's (0.64, 0.50), which is where its village is already set (A189).

**§2–§4 hold**: the picture taken at save time, one `title.jpg` a village, framed on the homes' middle, the season the save's own.
The fallback (no picture) is the four seasons under the same treatment.

## 6. What Joe will see

`.\run.bat`: the title opens over **his newest village with a home**, framed on its houses right of the panel, in the season he
left it, aged; *Continue* opens his newest save. Play a year and quit to the title: the picture has moved on a year (and to the
next season). Found a new village and quit at once: the title still shows the last village with a home. Rename the saves folder:
the four seasons, aged. **The title opens no slower** than today (one JPG, no save read).

## 6a. Continue: the paint lifts into the game (A232; ✅ call 5; 🔨 built, A236)

Joe: *"if we do E, then, when the player clicks continue, can we transition the background image smoothly to the playable
version?"* **Yes.** The picture *is* the save: it was drawn by the same map, from the same state, at the moment that save was
written. So the live map can open on exactly the frame the title shows, and the handoff can be invisible.

- **What he would see (~2 s):**
  1. He clicks *Continue*. The panel slides off to the left and fades.
  2. The save loads behind the still picture, so a slow load is just a held picture, never a frozen animation. Nothing moves
     until the village is ready.
  3. **The paint lifts.** The shader's strength runs from (e) down to nothing over about a second. The glazes, washes and blooms
     dry away and leave the plain snapshot underneath.
  4. The live map takes over **on the same frame**: same centre, same zoom, same season, the people where they stood. A
     short crossfade covers what is not in the picture (animation, the markers).
  5. *(as specced; the ease dropped, A237)* The camera **eases from the title's framing** (the village right of where the panel was) **to the play framing**, and the
     HUD fades in. Then it is the game.
- **What the map needs:** a way to open on the picture's frame instead of `CentreOnTheVillage()`. `LookAt(centre, ppt)` already
  exists (A150) and takes both. The centre is the homes' middle shifted by the picture's 0.64. The zoom is 32 px a tile × the
  cover scale of the player's window. Both are worked out from the loaded village and the window, and **nothing new goes in the
  save**: `slice/hud`'s view section holds no camera and does not need one. The ease to the play framing is the same
  `LookAt`, stepped each frame.
- **When it falls back to a plain fade:**
  - *Load…* opens another save.
  - *Continue* opens a save that is not the picture's village, because the newest save is a founding (call 2).
  - There is no picture yet, so the title shows the four seasons.
  In each case the paint still lifts, then the picture fades to the opened village.
- **Who:** the transition lives in `TitleScreen` and in the load path in `Main.Saves.cs`, the same systems-lane file as
  the save-time hook (call 4's terms; the HUD merged, D885).
- **Cost:** none at play time. The shader runs only while the title or the lift is on screen.
- **Built as (A236):** `TitleScreen.Lift.cs`, a list of steps stepped each frame, a hitched frame counting as 1/30 s:
  - **the panel away** (0.6 s), then the save is opened on a still frame; a save that will not open brings the panel back;
  - **held** until the map has drawn at least 4 frames, the last without a hitch, every figure painted (150 frames at most);
  - **the paint off** (2.2 s), **the picture off** (0.6 s), **the panels in** (1.8 s). The player has the game from the moment
    the picture is gone. *(A237, Joe: "slow down the transition by about half - i want the user to see it" — every part twice
    A236's.)*
  - ✅ **The view stays the snapshot's** (A237, Joe: *"dont zoom into the village. leave the view as is in the snapshot"*): the
    camera's ease to the play framing is gone. *Home* or *Centre* frames the village, as ever.
  - ✅ **"Painting the village…"** (A237, Joe: *"maybe 'painting village' or something"*) on a slip of the HUD's parchment, low in the
    middle, up before the save opens and gone as the paint starts to lift. **And on a pause-screen *Load…* and F7** (A239, Joe: *"yes
    do"*): over the village being left, the village stopped, drawn before anything is saved or reloaded (`SayItIsPainting`), since
    Godot holds that frame until the new village's first (3.0 s on Thornfield). A second pick is not a second load.
  - **The picture is of the save** when the opened save is its folder's newest and the picture was written after it
    (`PictureIsOf`) — so *Load…* on that save matches too. Otherwise the paint lifts and the picture fades to the usual framing.
  - **A key or a click hurries it** once the paint is coming off; none reaches the village under it. A camera the player moves
    once the picture has gone is theirs.
  - **Measured on Thornfield at 2560 × 1440:** his click to the whole game 8.2 s (A237), the last 1.8 of it playable under
    the panels coming in: the panel 0.6, the load about 2–3 (held still under the words, as long as it was before), the rest 4.6. Houses, roads and people fall on the same pixel in the picture and the map. What differs, under
    the crossfade: the markers and washes the player has on, and the ink. ⚠️ **The picture's ink is finer than the game's**:
    the game draws on a 1422-wide canvas scaled 1.8 times, the picture at 2560 unscaled, as the look was approved. Left as approved.
  - It stands in `OpenTheVillage` (`Main.Saves.cs`) and `LiftTheTitle` (`Main.Title.cs`).

## 7. Who builds which part

| Part | Files | Lane |
|---|---|---|
| The surface: layers, shader, painting script | `textures/ui/title/aged-*.png` (+ `.import`), `shaders/title_aged.gdshader`, `tools/title-aged/` | ✅ art's own |
| The title reads the newest picture and lays the surface | `TitleScreen.cs`, `TitleScreen.Probe.cs` | art built them (A180, A190, A223), outside the lane's table — ⏸️ call 4 |
| Drawing and writing the picture | a new `TitleSnapshot.cs` (the second map, the framing, the JPG) and a new `VillageMap.Still.cs` (what it borrows, which only the map can hand over) — no existing file changes | ✅ art (new view files) — call 4, A234 |
| **The save-time hook**: call `TitleSnapshot` after each save and autosave, and at the quits; the lift's one line | **`Main.Saves.cs`** (`Autosave`, `SaveUnderAName`, the quit paths, `OpenTheVillage`) and a new `Main.Title.cs` | ✅ art, call 4 (A236) |
| The probe's new line | `Main.cs`, one `GD.Print` | ✅ art (A236) |

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
  beside its save. *(As built: headless draws nothing, so the new `title picture:` line asks what was asked for — below.)*
- **Built (A234):** the `title:` line's own — the paint over the picture (each shader declaring every uniform it is handed, the
  washes once and the picture's size, full strength), which picture it picks over posed saves on a scratch folder it deletes, a
  file that is not a picture, a heart's place in its picture, a founding with no heart. **Red-check: 12 mutants, 12 red.** The
  three loud `ERROR` lines about `probe-title\broken\title.jpg` in a probe are that check's, meant.
- **Built (A236):** `title picture:` (`Main.Title.cs`): an autosave, a named save and F5 each ask for one picture beside the save, a
  village stopped on an error none; a way out waits for the picture and goes once; Godot does not close the window itself.
  And the lift, on `title:`: `PictureIsOf` over a scratch folder, the frame at three windows, the camera's way, and whole lifts
  stepped by hand (refused, over its own picture, over another, hurried, moved). **Red-check: 23 mutants, 22 red** (19 at
  first; three checks tightened). **The zero, written down:** the lift not waiting for every figure, which a headless map
  cannot pose. **A237:** the words, the view kept and the new lengths, 14 mutants, 14 red. **A239:** `loading words:`, 6 mutants, 5 red and one that
  reloads the scene under the probe, which then never says `done.`; F7 and *Load…* themselves driven in a window. ⚠️ **Not guarded, driven by hand in a window instead:** the picture itself, *Quit to title*, F7, the ✕ (0.15 s to close
  with its picture), the ✕ minimised (closes at once, the last picture stands).
- Red-checked, the reds counted (D326). The four checks as `CLAUDE.md` says; **the suite by D846's rule** — this piece changes no
  sim, data or test file, so it is skipped and the record says so.

## 9. ⏸️ Joe's calls

1. ✅ **ANSWERED (A233): *"Yes, (e) and the lift"*.** **The look — now §5b (A231):** ⭐ **(e) full-bleed watercolour at the first strength**, which is clearly painted and keeps the village readable at a glance. **(e+)**, the stronger one, goes muddier in the forests. *Or a strength between them.* Round 2 answered (A231): *"no, i hate them both"*, and round 2's text is kept below for the record. ~~**The look.**~~ **Answered (A230): neither** — *"i dont really like any of them. the parchment looks like a quilt."* **Now the look of §5a:** ⭐ **(d) deeper watercolour** — it is the game's own medium carried further, the village stays the subject and fills the screen, and the paper's white sets off the parchment panel rather than repeating it. *Or:* **(c) a manuscript page** — the most "ancient", a frame and margins painted once and reused for every village, but the village is smaller inside its frame, and the panel is then a second parchment beside a vellum page. *(Round 1, kept for the record:)* ⭐ **(b) Aged parchment**, over the snapshot and over the four-seasons fallback. It is the map's own medium, so the
   picture and the parchment panel read as one object; its marks sit at the edges and folds, leaving the village clear. The oil's
   crack web lies over everything, and its dark lines are the same weight as the map's own ink (paths, fences, contours), so it
   competes with them, most in the village itself (`title_snapshot_detail.jpg`). *Or:* **(a) oil craquelure** — the closer match
   to KCD2 and the more dramatic of the two.
2. ✅ **ANSWERED (A234): *"Yes to all three"*.** **Which village.** ⭐ **The newest village with a home standing** — a founding writes no picture, so the title keeps showing
   his last real village. *Or:* the newest save, whatever it holds (tonight: an empty valley at tick 0).
3. ✅ **ANSWERED (A234): yes.** **One picture a village** (`title.jpg` in its save folder, rewritten at each save), not one per save file. ⭐ **Yes** — the same
   picture on the title, and no change to the sim's save code.
4. ✅ **ANSWERED (A234): yes.** **His OK for the files outside the lane** (§7): `TitleScreen.cs` / `.Probe.cs` and a new `TitleSnapshot.cs` — ⭐ **yes**, the art
   lane builds them now; and **the save-time hook in `Main.Saves.cs`** after `slice/hud` merges — ⭐ **the art lane makes it, on
   `art`, as its own commit, after the HUD's merge**, since it is ~10 lines and the piece is otherwise whole. *Or:* the systems lane
   makes it as its next commit after the HUD.
5. ✅ **ANSWERED (A233): yes, the lift.** **Continue lifts the paint into the game** (§6a, his idea, A232). ⭐ **Yes, as §6a**: about two seconds, the live map opening on the picture's own frame, a plain fade wherever the picture and the opened save differ. It goes in the load path with the hook in call 4, after the HUD's merge. *Or:* a plain crossfade from the picture to the game, with no lift and no matched frame.
6. ✅ **ANSWERED (A235): *"Yes, every save"*.** **The year's turn (A234).** A picture costs two frames of about 41 and 28 ms on Thornfield (§3), over the budget by about
   25 ms. ⭐ **Take it at every save all the same**, the year's turn included, as his brief says. *Or:* only when he quits,
   saves by name or presses F5, so the year's turn costs what it does today; a crash would then leave last session's picture.

## 10. Order of work

1. ⏸️ His four calls. 2. The surface baked, the shader, `TitleScreen` showing the newest picture with the surface over it, the
fallback aged — the four checks — ⏸️ his look in the game (`.\run.bat`, the four seasons aged until a picture exists). 3.
`TitleSnapshot.cs` with its cost measured against today's autosave on his Thornfield. 4. After `slice/hud` merges: `main` into
`art`, the hook (call 4) and the paint lifting on *Continue* (call 5, §6a), the four checks — ⏸️ his play, then the merge at his word.

## 11. Not in this piece

The pause screen; the new-valley screen; motion on the title; a picture chosen by the player; re-aging the panel itself.
