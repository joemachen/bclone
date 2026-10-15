# Spec: Path shapes — why the village's paths run in a grid, and what would change it

**Decisions:** D568 (Joe's note 1, 2026-10-12, from a screenshot: *the paths make an orthogonal lattice round the market
and through the housing block*), **D579 (this spec and its measurement, 2026-10-13)**. Neighbours: D41 (one cost
field), D122 (the last time the route chain moved by a tile it froze nineteen people), D358 / D414 / D415 / D424
(`desire-paths.md`), D383 (buildings are obstacles).
**Status:** 📏 **MEASURED (D579). ✅ OPTION A BUILT (D580); Joe: *"still looks like a grid. proceed with C."* ⛔ C
NOT BUILT — his newest save showed it would not reach what he saw (§4.E); his call: E. ✅ OPTION E BUILT (D581) — view
only, `TrailNetwork` (`desire-paths.md` slice 2 says how). ⏸️ Joe plays it. C stays here for villages of many homes.** `tools/harness/ZzPathShape.cs` + `pathshape.py`. The two sim prototypes in §3 lived
in a throwaway worktree and are gone.
Owner: Joe + Claude Code. Format per `METHODOLOGY.md §2`.

---

## 1. The note

Joe, playing the nomads (D568): the worn paths in the village run as straight north–south and east–west lanes that
meet at right angles — a lattice — where the paths out in the woods curve. He asked for a fix; the handoff reviewed the
cause as three design decisions deep and said: spec it and bring the options before the field is touched.

## 2. How a path gets its shape, today

1. **The route** (`TravelCostField.RouteFrom`) is a staircase on the four-way cost field. Where two steps cost the same,
   `TerrainCostField.StepFrom` takes them in a fixed order — **east, west, south, north** — so on open ground every route
   runs all the way along one axis first and then the other: an L.
2. **The walk** is already a straight line (clock B, D361): `PlanLeg` pulls the string to the furthest route tile in
   sight. On bare grass that is the chord, whatever the route's shape.
3. **But once a path exists, a walker keeps to it** (D414, `path_shortcut_grass_allowance` 0): a leg may not tread more
   grass than the route it replaces. That rule is what ended the *blobs* (Joe, 2026-09-26) — and it holds walkers to
   whatever shape the route found.
4. **Buildings are obstacles by the tile** (D383, fences D404). A building may face any angle (5.6° steps), but it
   blocks whole tiles, so the ground between buildings is corridors of whole tiles.
5. **The view** (`VillageMap.LayTrails`) draws each worn tile as a ribbon through its centre, joined to all eight
   neighbours, an L's corner pulled to the corner the walk passed through and bent (D368), a two-wide corridor drawn as
   one lane (D415). A staircase already draws as a slant; a straight run of tiles draws as a ruled line.

## 3. Measured — Joe's own villages (D579)

The seven of Joe's saves this build opens (format 7, 12–13 October), read from **copies** (`SaveFile.Open` keeps a
damaged file as `.bad` beside it). Each was scored as he left it (`now`), then run on ten years unattended under each
arm. *Lanes* = the share of the village's path tiles (within three tiles of a building) lying in a straight
north–south or east–west run of five or more; *beside* = the share with a building on one of its four sides.

| village (year) | path tiles | lanes now | beside | +10 y: base | ties | allow 1 | allow 2 | allow 4 |
|---|---|---|---|---|---|---|---|---|
| ashbourne (8) | 48 | 83 % | 66 % | 76 % | 67 % | 86 % | 88 % | 88 % |
| fernhollow (12) | 74 | 95 % | 54 % | 91 % | 92 % | 98 % | 95 % | 95 % |
| larkspur (11) | 47 | 89 % | 53 % | 71 % | 75 % | 70 % | 76 % | 75 % |
| oakhaven (13) | 87 | 58 % | 47 % | 58 % | 56 % | 59 % | 75 % | 82 % |
| thornfield-grove (11) | 72 | 81 % | 47 % | 82 % | 76 % | 91 % | 95 % | 83 % |
| thornfield-thrush (9) | 70 | 90 % | 50 % | 81 % | 81 % | 89 % | 83 % | 82 % |
| willowdale (21) | 59 | 69 % | 59 % | 76 % | 74 % | 77 % | 75 % | 71 % |

- **58–95 % of the village's worn ground is straight axis lanes, and half of it (47–66 %) runs along a building's
  wall.** Out in the woods these villages have almost no worn ground at all (0–12 tiles).
- **`allowN` — loosening D414 — makes it no better and often worse** (oakhaven 58 → 82 %): walkers freed from the lane
  cut chords that wear *new* straight rows beside the buildings. It would also bring the blobs back.
- **`ties` — the route breaks a tie toward the axis with further to go**, so an open-ground route is a straight run and
  then a 45° stair (the shape an eight-way field would give) — **moves nothing that matters** (76 → 67, 91 → 92, 71 → 75,
  58 → 56 …). Costs, reach and catchment are untouched by it; only which equal staircase a route takes.
- ⚠️ Thin evidence where it is thin: the villages are 8–25 people; run on unattended, three of seven died out
  (paths fade with nobody walking them). The `now` column — Joe's own play — is the reliable one, and it says the same.

**The finding: the lattice is the village's own shape.** The paths are the corridors between rectangles, and the
rectangles sit on the tile grid. A rule about routes or walks cannot unbend a corridor.

## 4. The options

### A. Draw paths the way they are walked — the view only ⭐ recommended first
Lanes that wobble gently inside their own tiles, thin where only worn and fuller where packed, tapering at a dead end.
Each worn tile's trail point is nudged across the lane by a smooth, seeded offset (value noise over tile coordinates,
at most ~0.2 of a tile, so a drawn lane never leaves the tiles that are worn); the ribbon's width follows the grade;
an end with one neighbour narrows to a point. **Where the paths go does not change — only that a ruled line becomes a
footpath.** Rebuilt on `Paths.Generation` as the mesh is today (never per frame); deterministic, so the same village
draws the same lanes. No sim change, no golden, nothing saved. Probe: a drawn point never leaves its tile, a straight
row of worn tiles is no longer drawn straight, two rebuilds draw the same mesh, and the frame cost stays where it is.
**Cost: small.** Risk: it looks fussy — Joe's eye decides, and it is one constant to turn down.

### B. An eight-way cost field — true diagonal lanes
The field the handoff named: diagonal steps at cost 14, Dial's ring widened to 15 buckets, corners not cut past
buildings or fences, `StepFrom` and `RouteFrom` eight-way, catchment and the leg clock re-derived. Every golden moves,
the Phase 0 pins move, the economy's reach changes diagonally (a hut reaches ~40 % further along a diagonal). **The
measurement says it would barely change Joe's villages**: the `ties` prototype gives open-ground routes this shape
already and moved nothing, because the lattice is the corridors, not the open ground. **Cost: large (D122's chain).
Not recommended for this note.**

### C. A village that is not laid out on a ruled grid
Homes the sim sites (`ChooseSite`, the plots of `organic-housing.md`) set at varied offsets and small angles on their
plots, so the gaps between them stagger instead of lining up — a staggered gap wears a stair, and the view already draws
a stair as a slant. This changes the thing the paths are the shape of. Touches the house-siting rules (D411–D413,
D497's seams, D331's overlap test, every reachable-door guarantee), moves goldens, and is a design call about how a
village should look as much as about paths. **Cost: medium–large. Worth its own spec if A is not enough.**

### D. Leave the shape; it is honest
A planned village's lanes do run along its walls. Joe saw a grid because the village *is* one. Do nothing, or do A only.

### ⛔ Not an option: loosening D414
Measured worse (§3), and it is the rule that ended the blobs.

## 5. What any option must keep

- **Legibility (§1.1):** a drawn path is worn ground. Nothing drawn where nobody walked; a lane may wobble inside its
  tiles, never outside them.
- **One cost field (D41):** no second travel system — option B replaces the field, it does not add one.
- **Determinism:** the view's wobble is seeded from tile coordinates, not from frame time or a random draw.
- **Nothing incremental rebuilt per frame** (CLAUDE.md): A rebuilds on the paths' generation counter, as today.

## 6. Order of work, once Joe has chosen

A: one commit, view only (`VillageMap.Meshes.cs` `LayTrails` / `TrailPointOf`, a `paths:` probe line), then Joe plays it.
B or C: a spec of its own, measured on the same seven saves and the 100-valley harness before a line of the field moves.

## 4.E The network, not the tiles (D581) — what Joe's screenshots actually showed

Joe played A (D580) and sent two screenshots: *"still looks like a grid. proceed with C."* Read before C was spec'd: his
newest save (`thornfield-crow-birch-49`, read-only) holds **14 buildings, 2 of them homes** — the grid runs through the
cluster of stores and workplaces he placed round the cart, where C (the sim's *house* siting) reaches nothing. What the
screenshots show is the per-tile drawing: **ladders** (two lanes a tile apart; every walk crossing between them drawn as
a rung), **square rings** round a hut's outline (the `#` knots), and the **hub's packed yard**. Put to him with that
evidence; **his call: E** — the view reads the worn tiles as a shape and draws its centre lines (`TrailNetwork`: bridge
one-tile gaps, thin, merge touching junction pixels, trace, place on the middle of the worn ground, wander, smooth).
⛔ **The textbook clean-up was measured and refused:** pruning short dead ends off a junction cut the three-tile lane up
to a store's door on Joe's own village and changed nothing in any drawn shape. ⭐ *The lesson: a player's description of
a fix ("proceed with C") is a hypothesis about the cause — read the village in the screenshot before building it.*
