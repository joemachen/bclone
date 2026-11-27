# The great library's pictures — its roof on the map and its build-bar icon (A304)

> **Status: ✅ BUILT (A306, 2026-11-27) on `art`, UNPLAYED — Joe: *"1. B 2. yes 3. Photoshop is ready"* (roof B, icon B, his OK for the files, Photoshop clear). ⏸️ His play, then the merge at his word.** Mocked A305; claimed A304. Art lane (CLAUDE.md *Two tracks*),
> branch `art`. W5's ask of the lane (`skills-for-every-trade.md §8`, D922, D927). Mocks: `D:\Projects\brushes\great-library\`
> (`great_library_pick.jpg`; the renders in `out\`, the scratch game in `game\`, `mock.py` and `VillageMap.MockGreatLibrary.cs`
> patch it, `patch.py` makes the save copy, `icons_sketch.py` the icon sketches).

## 1. What is there today

- **The roof:** a stand-in (`VillageMap.Roofs.cs`, W5): the library's own look — slate, the library's blue, its paved forecourt
  with two benches and a sundial — with an upper storey and a taller shadow (the manor's two extras, without its chimneys), on a
  3 × 3 footprint where the library is 2 × 2. Next to an ordinary library it reads as *a bigger library*, not as *the* library.
- **The icon:** none painted. `buildings/great-library` (and `-owed`) sit on `Main.UiIconsProbe.ArtAwaited`; the bar falls back
  to `BuildingGlyph`'s open book, shared with the library.
- **The bar for "great":** the manor against the cottage — the same family (slate and walls of its tier), more of it: a storey,
  two chimneys, a lift, a bigger yard. The great library should stand to the library the same way: its blue and its slate, its
  book, and a shape nothing else on the map has.

## 2. The mock

**The roof**, drawn by the map's own painter (`RoofDrawing`, the same faces, pen, hatching and shadows) in a scratch copy of the
game, on a copy of his honey-harrow Hollowmere with a great library placed on open ground below the town hall, a few tiles from his
library; the middle zoom and the closest. ⚠️ His saves from before W5 do not open on W5's build (*"a different list of techniques"*:
4 saved, 11 now); the copy had the seven new techniques appended unknown, as a new game starts them. Nothing of his was touched.

- **A — a cross hall with a copper lantern.** A long slate roof and a transept over it, an octagonal verdigris lantern at the
  crossing. Keeps the library's forecourt. Reads civic and grand; the lantern is the map's only copper.
- ⭐ **B — a cloister round a garden court.** Four slate ranges round a square garth: lawn, a cross of paving, the library's sundial
  in the middle, its two benches on the paths, a small tree in one quarter; a gatehouse over the door's range. Uses the whole
  3 × 3 (no forecourt — the yard is inside). The one roof on the map with a hole in it: nothing else reads like it at any zoom.
- **C — a hall between two pavilions.** A long hall with a clerestory, a square hipped pavilion at each end, standing taller, a
  copper knob on each. Keeps the forecourt. The closest to the manor's language; at the middle zoom it reads as a big house.

**The icon**, three options in the icons' own woodcut (ink edge, flat world colour — the library's walls `#6a7fc9`, slate
`#3d4a7a`, the book `#f0e6d0` — the shade side hatched), shown big and at the bar's 20 px beside the library's own. ⚠️ **Sketched
in Pillow from the same shapes**, because Photoshop (`p3_icons.jsx`, which paints every icon) stalled on something on screen that
the session could not see; the built one is painted by `p3_icons.jsx` like the rest, its owed medallion placed by measuring.

- **A — a hall with a lantern:** a long low hall under a hipped roof, a copper dome on a drum, the book over an arched door.
- ⭐ **B — a gate tower and cloister:** the library's tall book gable as a gate tower, an arcaded range either side.
- **C — a hall between two towers:** two spired towers with copper knobs, the book gable between.

Each icon pairs with the roof of its letter; mixing is fine.

## 3. Calls — ⏸️ Joe's

1. **The roof:** A, ⭐ B or C.
2. **The icon:** A, ⭐ B or C.
3. **His OK for the files outside the lane's table:** `VillageMap.Roofs.cs` (the great library's row and its drawing, and its
   probe line), `VillageMap.Yards.cs` (only if B: the court's yard kind and its layout), `Main.UiIconsProbe.cs` (taking
   `buildings/great-library` off `ArtAwaited`). Nothing in `data/` or the sim: the suite stays skipped by D846.
4. **Photoshop:** the painter needs it unblocked to paint the icon (a dialog he may need to close; the session was refused
   control of it, rightly).

## 4. W5's two notes, checked

- **A master fisher's cast is half as long** with no art change: `VillageMap.Fishing` takes the cast's length from
  `WorkTicksFor` (10 ticks, 5 for a master: about 7 s at 1×, still above the quick-cast floor of 5 s, so the throw, wait and
  catch all play). ✅ By the code; worth a look in play when a fisher masters.
- **A ripe field in winter's first five days** paints ripe: `CropStageAt` gives a ripe tile *"ripe"* in every quarter and the snow
  is laid over it. ✅ By the code. No stooks-in-snow picture is asked.

## 5. Built (A306) — roof B and icon B

His answers, said back: **roof B, the cloister; icon B, the gate tower; his OK for `VillageMap.Roofs.cs` and `Main.UiIconsProbe.cs`**
(`VillageMap.Yards.cs` was not needed: the court is drawn by the roof, the yard kind stays the library's so its night and
its card read as the library's); **Photoshop clear**.

- **The roof:** `RoofForm.Cloister`, the great library's row in `RoofOf`, drawn by `Cloister()` in `VillageMap.Roofs.cs` with the
  map's own pieces — the shadow and the step as any roof; the court (lawn `#9fb07a`, a cross of the yard's paving, a round bed
  with the library's sundial, its two benches, a small tree in a quarter chosen by the seed); the ranges' shadow into the court as
  two strips on the walls the light comes over, worked out in the building's own frame so it holds at any turn; the four ranges,
  each a gable (`RoofFaces`), a fifth of the footprint deep (0.36 × the half-side); the gatehouse, a square hip a little lighter,
  over the middle of the range the door is in. The whole footprint: no forecourt.
- **The icon:** `buildings/great-library` and `-owed`, painted by `p3_icons.jsx`'s shapes (`D:/Projects/brushes/great-library/p3_icons_gl.jsx`, the shared painter untouched), 80 × 80, mipmapped like the rest; the owed medallion at (213, 208) on the
  256 grid, measured the way `medallions.json` was (the library's 202, 208 and the town hall's 205, 208 come out of the same
  measure). Taken off `ArtAwaited`, so `ui icons:` names only the cemetery's four.
- **The probe:** `painted buildings:` now wants the great library a cloister — four ranges, nothing of a roof over the court's
  middle, the court inside the ranges, one gatehouse on the door's side — at four turns and two doors (8 of 8), and no court on
  the library. Red-checked: see A306 in DESIGN §7.
- **To play it:** his pre-W5 saves do not open on W5's build, so two labelled copies of his honey-harrow Hollowmere, made
  openable, in his saves folder: `great-library-to-place` (the gift owed: the button starred with the gold medal, placed free)
  and `great-library-standing` (already standing below the town hall).

