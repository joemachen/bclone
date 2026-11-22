# Painted bridges — a wooden bridge, and its stone rebuild, with the buildings' treatment (A174, A175)

> **Status: ✅ MERGED INTO `main` (A176, 2026-11-22, merge `df134f6`) at Joe's *"merge it"*, after his play; not pushed.
> Built A175 at his *"B, all as recommended"* — every call as §8 recommends.** §9 is what was built. Specced and mocked A174. Art lane (CLAUDE.md
> *Two tracks*), piece (h) of his play notes (HANDOFF, *"JOE'S PLAY NOTES, 2026-11-20 … SORTED BY LANE"*), at his *"next for
> art: bridges"* (2026-11-22), claimed A173. View only: nothing in the sim, `data/` or `tests/` changes. Mocks, drawn by the
> game itself from copies of his saves (his folder untouched): `D:\Projects\brushes\bridges\bridges_looks.jpg` (today and
> the three wooden looks, on two of his bridges at two zooms), `bridges_wear.jpg` (the four conditions) and
> `bridges_stone.jpg` (the stone rebuild).

## 1. What Joe asked

*"An authentic wooden bridge with the buildings' treatment (today a deck quad, a grey glyph)."* The buildings were
painted in A85 (`building-roofs.md`): each drawn straight down in its material, lit from the upper left whatever its turn,
a shadow to the lower right, the pen drawing the material over a watercolour wash. The bridge was left out of that piece
(*"Kept as they were: … bridges"*, `building-roofs.md §2`) and is the last standing thing on the map still drawn flat.

⚠️ **"A grey glyph" is out of date:** the Works-bar icon and the card's buttons are painted since the UI art (A98–A107,
`textures/ui/icons/buildings/wooden-bridge.png`, `stone-bridge.png` and their `-owed` forms; `art-inventory.md` row 156
still says *grey square*). What is still flat is **the bridge on the map** and **the card's picture** (a coloured block, as
every building's card picture is — `building-roofs.md §7`).

## 2. What the sim gives the view (`specs/bridges.md`, `building-condition.md §4.4`) — unchanged by this piece

- **A bridge is a straight line between two points** (`Bridge.Near`, `Far`), dragged at **any angle**, both ends on dry land,
  **up to 12 tiles** end to end (D550), one tile wide. The tiles under it stay `Terrain.Water`; the deck is an overlay.
- **Two kinds:** `BuildingKind.Bridge` (wood) and `BuildingKind.StoneBridge`. Stone is a rebuild in place (a click on a
  wooden one with the stone bridge in hand) or dragged fresh; **the wooden deck carries traffic until the stone one is
  finished**, then the bridge is stone and takes a stone name.
- **Wood wears; stone never does** (`building-condition.md §4.4`, rows 8 / 12 / 20 years): `world.ConditionOf(bridge)` is
  `Sound`, `Worn`, `Failing` (*slows*: a leg over it takes 150 % of its ticks) or `Ruin`; a ruin is `Bridge.Closed` (not
  walkable) **unless** it is the only way somewhere (`KeptOpenBecause`) or somebody is on it. Builders mend it from the near
  bank.
- **A site** is one tile of near bank carrying the planned far end (`ConstructionSite.BridgeTo`); the map draws it as a band
  along the whole span filling from the near bank (D546).
- **Nothing finer:** no per-plank state, no flow direction for the river under it, no height. Whatever the picture shows
  beyond this is the painter's, laid by a hash of the bridge so it is the same every frame.

**What draws it today** (`VillageMap.DrawBridges`, `VillageMap.cs` — the systems lane's file): a quad 0.7 tile wide from end
to end in `BridgeColour #8a6a48` (stone `#a8a49b`), weathered by `Weathered` (D560), a 2 px ink edge (dashed when closed,
D556), and a pen line across it three times a tile (stone: once); a failing deck drops every other line (D556). On his
Hollowmere (year 68, 2,298 crossings) it reads as a strip of the same planks as the fishing hut's jetty beside it.

## 3. The look (all three wooden looks share it; mocked)

Each bridge, with the watercolour tick on, is drawn in its own frame — *along* the span from `Near` to `Far`, *across* it —
so it is the same picture at any angle, and **lit from the upper left whatever its turn**, as the roofs are:

1. **Its shadow on the water**, toward the lower right: the deck's rectangle moved (0.13, 0.17) tiles × its *lift* (0.75
   for timber, 1.0 for stone), three nested washes (the roofs' `RoofShadow`, stronger over water: alpha 0.16).
2. **A footing at each bank**: a few stones showing either side of the deck's end, where it sits on the bank.
3. **Its supports**, spaced evenly across the water only (the tiles under the span read from the map), their ends showing
   past the deck, **a ring of pale foam** round each where it meets the water:
   - **A, B** — a **bent** every ~1.5 tiles: a cap beam across, its ends 0.13 tile past the deck, a round pile under each end.
   - **C** — a **crib** every ~2 tiles: a square of four logs crossed at the corners.
4. **The deck, board by board**: boards across the span ~0.16 tile wide (C: split logs ~0.19, their ends ragged), each its
   own slice of a painted wash (`deck_planks.png` / `deck_logs.png`, the roofs' convention: a tone map with mean 0.8 multiplied
   by the bridge's colour, 12 % toward the paper), the pen between boards. Under the boards, seen only where boards are gone,
   two stringers.
5. **Its edges**: a darker kerb along both sides and the pen (2 px at the closest zoom); a sill across each end.
6. **B only — a rail each side**: a post every 0.75 tile and at both ends, a rail along their tops, its upper-left side lit,
   a hair of shadow to the lower right.

**Its conditions** (wood; mocked on B): **sound** as above; **worn** — the timber silvered (today's `Weathered` step); **failing**
— some of the boards over the water gone (a quarter in the mock; a sixth in the build), the stringers and the river showing through, one run of rail gone (the same
boards every frame, by hash); **fallen in** (`Ruin`) — the span between the two supports nearest the middle gone, boards broken
off ragged at both sides of the gap, the piles left standing in the water. A ruin kept open (`KeptOpenBecause`) draws as
failing, because people are still crossing it.

**Stone** (one look, mocked): a deck of dressed setts in courses (`deck_setts.png`), a **parapet wall** each side (its top
lit by the way it faces, a joint every block, inked), **piers with a pointed cutwater both sides** every ~2.6 tiles across the
water, foam at each point, stone footings at both banks, the stronger shadow of a higher deck. It never wears.

**Kept as they are:** the drag ghost (a line coloured by the sim's verdict, D549 — it is a tool, not a thing); the site's
band filling from the near bank (every building's site stays a band, `building-roofs.md §2`); the card's picture (as every
building's; the card pictures in the new look are their own piece, `building-roofs.md §7`); the bar's icons (painted).
**With the tick off nothing changes**, to the byte.

**What the mock showed, to carry into the build:** failing at a quarter of the boards reads as a row of windows — a
sixth is enough (the build's number, red-checked against the probe's count); the fallen-in pose has people standing in
the gap because the condition is posed on a living village — in play a ruin closes only with nobody on it; the foam rings
at the stone piers read as small circles and want to be arcs on the point's two sides; the stone sits close to the paper's
value at play zoom and keeps its ink edge for that reason.

## 4. Why these three looks

- **A — planks on trestles.** The plainest honest timber bridge: what the sim's one-wide, straight, any-length deck most
  simply is. Its weakness is the jetty: a plank rectangle on piles is also what the fishing hut has beside the water.
- **B — A with a post-and-rail each side.** ⭐ The rails are what say *bridge* rather than *jetty* at a glance, at play zoom
  as well as close, and the posts give the timber the built, joined look the roofs' pen gives a building.
- **C — split logs on log cribs.** The rustic, earlier bridge: rounder, rougher, fewer and heavier supports. It reads well
  close and as a ribbed band from further out; it also makes the step to stone larger, which the story of the rebuild likes.

## 5. Code (on his answers)

- **`VillageMap.Bridges.cs`** (new, the art lane's): `BridgeDrawing(...)` — a pure function returning the pieces a bridge is
  drawn with (fills, textured faces, pen lines; the roofs' `RoofPiece`), which the map draws and the probe reads, so they
  cannot disagree — and `DrawPaintedBridge`, which draws them only with the tick on and says no otherwise.
- **`VillageMap.cs`, one call** (the systems lane's file — ⏸️ call 5): in `DrawBridges`, before today's quad,
  `if (DrawPaintedBridge(world, bridge, i, worn)) { continue; }`.
- **⛔ Nothing laid per frame (CLAUDE.md).** A bridge's pieces are laid once, in tile space, into the roofs' runs and meshes
  (`building-roofs.md §5`), keyed by its two ends with a signature of what they were laid from — the kind, the condition,
  closed, kept open, the look. While it holds, a frame hands the meshes over with one transform; the pen is laid again only
  when the zoom changes. Entries for bridges no longer standing are dropped when `BuildingGeneration` moves (to confirm in the
  build that raising, rebuilding and pulling down a bridge each move it; if one does not, `StandingGeneration` does). The
  water under a span is read from the map once, when it is laid. Neither the cache nor its counter is hashed or saved (D335).
- **Textures** `textures/buildings/deck_<material>.png`, 1536 × 96 (along × across; 128 px a tile, the longest span),
  lossless, mipmapped, mean 0.8, each with its `.import` (trap 2); made by `D:\Projects\brushes\bridges\make_deck_textures.py`.
- **Cost** measured at 4K on his Hollowmere, the tick on, this build against the one before in one sitting (art trap 76).

## 6. Probe: `painted bridges:` (named from this spec, not from the code's lists)

The textures load at 1536 × 96, mipmapped, drawn with mipmaps, mean 0.8 ± 0.03; posed bridges at four turns (along an axis,
a diagonal, a shallow slant, end-on to the light) of 3 and 12 tiles: the deck's boards tile the span end to end with no gap
or overlap, inside the 0.7 width; supports only where the map has water, none on the bank, evenly spaced; the shadow to the
lower right of the deck at every turn; (B) posts at both ends and rails on both sides; failing loses boards over the water only
and the same ones when laid twice; a ruin's gap between two supports with both ends of the deck still drawn; a kept-open ruin
drawn as failing; stone never weathered and with a cutwater both sides of each pier; the cache keeps a bridge asked twice and
lays it again when its condition changes; with the tick off nothing painted. ⚠️ Draw-time only, so a render's evidence: the
wash's look, the foam, the shadow's softness.

## 7. Not in this piece

The foam moving with the river (a later piece with (i), the trees' wind, if he wants water that moves round things); the
construction stages of a bridge (its band, as every site); snow on the deck; the card's picture; a dock or jetty in the same
look (the fishing hut's is its yard, `building-yards.md`); boats passing under (the trade boat sails the river, `trade.md`).

## 8. ✅ Joe's calls — *"B, all as recommended"* (A175, 2026-11-22)

1. **Which wooden bridge — A, B or C?** ⭐ **B**, planks with a rail each side.
2. **How it wears:** ⭐ painted in the same three steps as today — worn silvers the timber, failing loses boards and a run of
   rail, fallen in loses the middle span with its piles left standing. *Or:* colour only, as the buildings wear.
3. **Stone in the same piece:** ⭐ **yes** — otherwise a rebuilt bridge goes back to a grey band. *Or:* wood now, stone later.
4. **What stays as it is:** ⭐ the drag ghost, the construction band, the card's picture and the bar's icons.
5. **His OK for one line in `VillageMap.cs`** (`DrawBridges`, the systems lane's file), the drawing itself in a new
   art-lane file. ⭐ **Yes.**

## 9. 🔨 Built (A175, 2026-11-22)

- **`VillageMap.Bridges.cs`** (new): `BridgeDrawing(signature, isWater)` — pure, the pieces in the view's tiles and in order
  (§3's eight steps, B's rails and stone's parapets and piers); `DrawnCondition` (stone never wears, closed is fallen in, a ruin
  kept open is failing); `BridgeSignatureOf`, its seed from where the bridge stands, so the same boards are lost every frame and
  every load; `CachedBridge` and `DrawPaintedBridge`. The pieces become the roofs' runs (`RoofRuns`, `building-roofs.md §5`) —
  for a wooden bridge one mesh of fills and pen under the deck, the deck one textured mesh, one mesh over it — drawn through the
  frame's one transform; the pen laid again only when the zoom moves. Kept per bridge by its near end; forgotten, when
  `StandingGeneration` moves (raising, setting in stone and pulling down each call `StandingChanged`), if not drawn lately.
  The decks' paintings are not roof materials — a value added to `RoofMaterial` would be walked by every roof check — so a
  bridge's textured run is drawn in its own deck (`DrawBridgeRuns`).
- **`VillageMap.cs`, the one line** (Joe's OK, call 5): `DrawBridges` asks `DrawPaintedBridge` first and draws today's deck
  when it says no.
- **Failing loses a sixth of the boards over the water** (`BridgeBoardsLostWhenFailing`), not the mock's quarter.
- **Textures**: `textures/buildings/deck_planks.png`, `deck_setts.png`, 1536 × 96, lossless, mipmapped, mean 0.8, with their
  `.import`s; made by `D:\Projects\brushes\bridges\make_deck_textures.py` (its `logs` deck, look C's, is not shipped).
- **Probe** `painted bridges:` (`VillageMap.BridgesProbe.cs`), riding on `painted buildings:`'s return (art trap 73), each
  line in its own `try`: §6 as written, on twelve bridges posed at six turns (0°, 30°, 45°, 90°, 135°, 200°) of 3 and 12
  tiles over a river of the probe's own. **Red-check: 19 mutants, 19 red. The first run scored 18 with one zero written down: the map's own reading of *closed* (`BridgeSignatureOf` passed `false`), unseen because the probe posed signatures — the probe now reads a real `Bridge`, closed and open, and that mutant re-run alone is red. One red is by a throw, not its own message: with the tick-off guard gone the probe's empty world is read (`NullReferenceException`), ⛔ either way (`redcheck175.py`, `red175.log`, `red175b.log` in `D:\Projects\brushes\bridges\`).**
- **Renders** of the build from copies of his saves: `D:\Projects\brushes\bridges\built\` (Hollowmere rook-hare and Oakhaven, at
  the closest zoom and further out).
- **Cost** at 4K (RTX 3070) on his Hollowmere with his bridge in view, today's deck and the painted one alternated inside one run (four rounds of 60 frames a zoom, `measure_ab.gd`): GPU 4.98 against 4.98 ms at the closest zoom and 5.08 against 5.00 three steps out, inside the rounds' own spread (4.56–5.19); render CPU 1.48 / 1.48 and 1.55 / 1.55 ms. The main thread's time swung 15–30 ms in both and is not a reading.
- **The four checks:** suite 2206 / 0 / 5 of 2211 in 23m14 (no sim, data or test file changed, so the clock is not this piece: 17m11 at D833, 21m58 at A170 with a systems run beside it; this one began on a quiet machine, a windowed Godot open alongside); view build 0 warnings; probe 178 lines, 140 ✅, 0 ⛔, bar 183, tile centres ✅, new game ✅, `done.`; goldens 457, none moved (no test file touched).
