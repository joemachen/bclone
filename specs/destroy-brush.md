# Spec: The destroy brush, and farms painted over trees

**Decisions:** D490 (this document, B4), D491 (Joe took every recommendation in §8; slice 1 built). Joe's notes and answers: the D483 banner (2026-10-04) — *a red
(destructive) brush; everything painted is destroyed and trees do not grow back; a farmer may paint
fields over trees but cannot farm a tile until its trees are gone; farm "give land" must not paint stone
or iron seams*, and his four answers: (a) destroyed ground grows trees again **only if a forester replants
it**; (b) the brush **destroys stone and iron too, and the village loses the goods** (*"useful if a player
wants to clear nodes but doesn't have anywhere to store the goods or have a use for them"*); (c) **animals
can live in the woods** — a pen keeps its trees; (d) **laborers** clear the trees from a farm painted over
them. Neighbours: D43 (only painted forest is felled), D67 (seams are seen, never rolled), D84 (a deposit
is finite), D85 (terrain changes through one door, `SetTerrain`), D92 (brush modes are a filter), D100 and
D138 (the village clears a site's ground, not the player), D126 (the valley grows back where wood has
stood), D127 (harvest paint is a standing instruction), D157 (footprints cleared in build-queue order),
D162 (the farm), D220 (a planted sapling waits a period), D347 (a seam shrinks tile by tile), D452
(forester planting counts Grass only), D61 (livestock is blocked behind trade).
**Status:** 🔨 **SLICE 1 (§3.3, farms over trees and seams) BUILT (2026-10-04, D491), UNPLAYED. Slices 2–3 (the
destroy brush) NOT STARTED.** Spec written D490; Joe took every recommendation in §8 (D491). Owner: Joe + Claude Code.

---

## 1. Why this exists

Two of Joe's notes from playing the new-game screen, filed as B4:

1. **A destroy brush.** There is no way to get rid of what stands on a tile without taking its goods: the
   harvest brush fells and digs, and the village carries the yield home. A player who wants a meadow, or
   a seam out of the way with nowhere to store the stone, has no tool. He asked for a red one.
2. **Farms over trees.** A farm may be given forested ground today, and the trees simply stay there. He
   wants the laborers to clear them, the way they clear a building's site — and he does not want farm
   ground painted on a stone or iron seam at all.

## 2. What the game does today — measured, not read (trap 151)

Measured on 2026-10-04 with a throwaway harness test (the fixture valley, a farm raised outright, a
farmhand pinned, its 9 × 9 square posed as forest, saplings, rock, iron and grass in rotation, then
painted as its ground and run three years; then one rock and one forest tile harvested and run two
more). Traced in the code beside each.

| Claim | Measured | Where |
|---|---|---|
| Farm ground may be painted over Forest, Sapling, Rock and IronDeposit | **Yes.** 8 of 12 forest, 12 of 12 saplings, 10 of 12 rock, 7 of 11 iron accepted; every refusal was ground another building or a yard already held, never the terrain | `CanPaintWorkGround`, `SimWorld.cs` ~5032–5076: no terrain test but Water |
| Only Grass is ploughed | **Yes.** Grass → Field at once; nothing else changed | `Plough`, ~4030; `AfterGivingGround`, ~5152 |
| Trees on farm ground stay | **Yes, for three years.** 11 of 12 forest tiles still Forest at year 3. The one felled was **not the farm's**: traced (D491), it was one of the four tiles refused because a forester's hut already held it, and the forester felled its own ground | — |
| Saplings on farm ground grow up | **Yes — all 12 were Forest by year 1.** A farm painted over saplings becomes a farm with a wood on it | `RegrowthSystem`: work ground is not excluded |
| Nothing clears a farm's trees | **Yes.** Nothing marks them; the comment at `AfterGivingGround` assumes laborers will, and nothing does | ~5150–5167 |
| A harvested tile becomes Grass, its goods brought in | **Yes.** Rock: 12 stone. Forest: 12 logs | `Harvest`, ~5826 |
| A cleared tile on farm ground joins the field | **Yes.** Both harvested tiles were Field two years on | the farm re-ploughs Grass |
| Cleared ground regrows only where wood has stood | Rock tile: `HasEverBeenWooded` false, so it can never regrow. Forest tile: ploughed before it could | `RegrowthSystem.CanGrowHere` |
| How much wood a cleared tile needs beside it to regrow | **One mature forest neighbour of four.** ⚠️ The method's own summary still says *"two neighbours, not one"* — stale since the bound moved to `HasEverBeenWooded`; its closing comment is right | `RegrowthSystem.TouchesWood` |
| Something stops a tile ever regrowing | **No such flag.** `_everWooded` is the nearest thing; nothing clears it, and it is not hashed | `GeneratedMap.cs` ~236–240, ~444–450 |
| A forester's planting sets the regrowth bound again | **Yes.** Any tile becoming Forest or Sapling sets `_everWooded` | `GeneratedMap.cs` ~406–409 |
| Herdsmen and pens | **Do not exist.** No `Herd`/`Pen` in the sim; `livestock.md` is blocked by D61 | — |

## 3. The shape

### 3.1 The destroy brush

- **A red brush on the Removal tab, beside Demolish, Take back and Empty.** The same stroke as every other
  brush (`BrushStroke.SubTilesUnder`; left paints, right takes back, alt + wheel sizes it), coloured red so
  it cannot be mistaken for the harvest brush's orange.
- **It marks; it does not erase.** Painting a tile **marks it to be destroyed**, and the village does the
  work (§8, Q1 — Joe's call; this is the recommendation). That is D100's rule — *the village clears the
  ground, not the player* — and it keeps destruction legible: the player sees the red paint, sees a laborer
  walk out to it, and sees the tile go bare. An instant eraser would be the one tool in the game that
  changes the valley with nobody in it.
- **What it takes:** Forest, Sapling, Rock and IronDeposit. Each becomes Grass. (The harvest brush cannot
  take a sapling — it yields nothing; this one can.)
- **What it refuses:** Water, a tile something stands on, a quarry's or mine's working face (a face is a
  building's ground — take it back from the building first), and ground with nothing on it. Field, Sown
  and Ripe are farm ground — refused; taking a farm's ground back already returns it to Grass.
- **The goods are lost** (Joe, b). Nothing is carried and nothing is left on the ground. The stone does
  **not** count toward `StoneEverDug`, nor iron toward the smithy's count, nor logs toward `LogsEverFelled`
  — the quarry and the smithy are earned by digging, and a brush that unlocked them by deletion would be a
  shortcut around the knowledge tree (Non-Negotiable 1, DESIGN §2.7).
- **The last-rock warning applies** (`WithTheLastRockWarning`, quarry.md §3.8): destroying the last stone
  or iron the village can reach warns, and does not refuse.
- **The mark comes off when the work is done.** Unlike harvest paint (D127, a standing instruction), a
  destroy mark is an order: once the tile is bare there is nothing left to destroy, and the paint retiring
  is how the player sees the job finished.

### 3.2 Destroyed ground does not grow back — until a forester plants it

- **A new per-tile fact: the tile was laid bare.** `RegrowthSystem.CanGrowHere` refuses a laid-bare tile.
  Destroying a tile sets it; nothing else does.
- **A forester undoes it, and only a forester** (Joe, a). Planting a sapling on a laid-bare tile clears
  the fact, and from then on the tile is ordinary wooded ground and regrows as D126 says. This falls out
  of one line: whatever makes a tile Forest or Sapling clears the fact, as it already sets `_everWooded`
  (`GeneratedMap.cs` ~406–409). A forester only plants on its own ground (`IsGroundToPlant`), so the
  player chooses where the wood comes back by where they give the forester ground.
- **It is hashed** — sparse, mixing only the tiles that have it, the young-sapling way (`StateHash.MixMap`).
  It is state the player chose and it decides when a tile becomes wood, so it is in the hash (P0 if not);
  and a village that never destroys anything mixes nothing, so **no golden moves**.

### 3.3 Farms painted over trees

- **Forest and saplings may be painted as farm ground, and the laborers clear them** (Joe, d). Painting a
  farm's ground over a Forest tile harvest-marks it, the way placing a building marks its anchor (D100,
  `Mark` ~8021–8034). The laborers fell it through the harvest path they already walk (`NearestHarvest`),
  **after building footprints** (D157) **and after wood a building is waiting on** (D215 — the buildings come
  first), and before ordinary harvest paint. **It is cleared whatever the log limit says** — a field
  waiting on its ground is a footprint's case (D212's exception), not production. **The logs are the
  village's** — this is clearing, not destroying. Taking the ground back unmarks a tree still standing.
- **A sapling on farm ground is grubbed up, not waited for.** Measured: left alone, every sapling on a farm
  was a tree within a year. So a farm's ground stops regrowth (no sapling grows up there, no grass seeds),
  and **the plough takes a sapling as it takes grass** — a farmer pulls up seedlings. No yield; there is
  none to take.
- **The field shows at once; the uncleared tiles say why.** The farm's ground outline covers the trees
  from the stroke. Its card's ground cell counts them: **"18" over "3 to clear"** (measured: *"tiles · 999 to clear"* clips the cell; of six wordings only this fits at three digits) (the caption reads *tiles
  of ground* once none are left). A felled farm tree is ploughed **the moment it is felled** — the field takes
  the tile while the player watches, not the next spring.
- **The clearing mark retires when the tile is cleared**, as `RetireTheClearingMark` does for a building,
  so a farm does not leave orange paint over its own field.
- **Rock and iron are refused on farm ground** (Joe's note). The stroke paints round a seam and the drag's
  message says so: *"Not on a seam — clear it first."* Once the seam is dug or destroyed, the tile is Grass
  and the brush takes it. **Today it is accepted** (§2): this is a refusal added, and it moves no farm
  that exists in a saved game, because there are no saved games.

### 3.4 Pens keep their trees — recorded for livestock

There are no pens (§2; D61 blocks livestock behind trade). **Joe's rule is recorded here and in
`livestock.md §6.1` so the livestock slice inherits it:** a pasture may be painted over woodland, and its
trees stay — *animals can live in the woods.* Nothing is built for it now.

## 4. Data

- `data/sim.config.json`: **`destroy_ticks_per_tile`** — how long a laborer spends destroying one tile.
  ⚠️ **No number is proposed here** (trap: *a number in a design sentence is a claim about the
  generator*). Slice 1 measures what felling and digging take today and proposes it beside them.
- No new good, building or job. The brush is a tool, so it is in the view's tool list, not in data.

## 5. Architecture

- **Sim:** a destroy layer in `ZoneMap`, sub-tile like the harvest layer (`SetHarvest` is the shape to
  copy), hashed sparse; `CanPaintDestroy` / `PaintDestroy` / `EraseDestroy` beside the harvest trio;
  `SimWorld.Destroy(tile)` through `SetTerrain` (D85's one door) setting the laid-bare fact; the work joins
  `TryHelpWithHarvest`'s fallback as its own branch of `NearestHarvest`, so one search answers *"what
  ground needs a hand?"* rather than two.
- **The laid-bare fact** lives beside `_youngSapling` in `GeneratedMap` (one bool per tile; set by
  `Destroy`, cleared by any tile becoming Forest or Sapling).
- **View:** a `MapTool.Destroying` / `Undestroying` pair, a red zone colour and red ghost, a Removal-tab
  button with its own `ToolMark` glyph, the drag's one-message rule (`VillageMap` ~1464).
- ⛔ **Nothing rebuilt per tick** (CLAUDE.md). The destroy layer keeps a count of marked tiles beside its
  bits, the way `ZoneMap` keeps its other indices, so *"is anything marked to destroy?"* is one read.

## 6. Slices (one commit each, Joe plays each)

1. **The farm's trees and seams** (§3.3): the seam refusal, the auto-mark, the sapling grub, regrowth off
   farm ground, the clearing mark retiring, the card's line. Smallest, and it fixes the thing he hit.
2. **The destroy brush in the sim** (§3.1, §3.2): the layer, the laid-bare fact, the labour, the hash.
3. **The destroy brush in the view**: the button, the colour, the probe.

## 7. Guards

Each red-checked, and the reds counted (D326):

- Farm ground on Rock and IronDeposit is refused; on Forest and Sapling accepted (one test, four poses).
- A farm's forest is cleared by laborers with nobody painting it, and the logs reach a store.
- A sapling on farm ground never becomes Forest; the plough turns it to Field.
- A destroyed forest, rock and iron tile is Grass, and no store gained anything; `StoneEverDug` unchanged.
- A destroyed tile beside a wood is still Grass after three regrowth periods.
- A forester planting on a destroyed tile: the sapling matures, and its neighbours regrow again.
- The destroy mark retires when the tile is bare.
- Determinism: same seed, same strokes ⇒ same hash; a run with no destroy strokes hashes as today
  (`git diff` of the goldens: unchanged).
- Probe: the Removal tab's button, its colour, and bar height 151 held.

## 8. Joe's calls — ✅ ANSWERED (2026-10-04, D491): *"go with your recommendations"*

Q1 laborers destroy; Q2 bare grass refused; Q3 a forester's own ground left as is; Q4 farms first. Kept
below as asked.

1. **Q1 — Instant, or the village does it?** Recommended: **laborers do it** (§3.1), as they clear a site.
   The alternative is an eraser that empties the tile the moment it is painted — faster, but the only tool
   that changes the valley with nobody in it.
2. **Q2 — Bare grass under the brush.** Recommended: **refused** (nothing to destroy). The alternative: the
   brush may lay bare open grass too, so a player can keep a meadow from ever being wooded.
3. **Q3 — Forester ground.** If the player destroys a wood on a forester's own ground, the forester will
   replant it (that is how a forester works, D137). Recommended: **leave it** — that is answer (a) working
   as written.
4. **Q4 — The order of the slices** (§6): farms first, then the brush?
