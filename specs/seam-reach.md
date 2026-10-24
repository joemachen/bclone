# Spec: A quarry or a mine stands near its seam

**Decision:** D539 (Joe, 2026-10-09), D540 (his calls; built), D541 (4 → 2 tiles), D561 (painting: the whole seam it
stands by). **Status:** ✅ **placement within 2 tiles built, played and merged (D540, D541, D545); ✅ D561 — a quarry or a
mine paints the whole seam it stands by) — built, played and merged (D562).** ✅ **§8 (D613): claimed, not painted — a quarry or a mine takes the
whole seam it stands by when it is finished, drawn round with a palisade; the brush goes. SPECCED AND BUILT (D613), unplayed.** `SeamReachTests` (8)
green and red-checked: D540's 7 mutants / 12 reds, D561's 4 mutants / 7 reds, no zeros left. No golden moved.

---

## 1. Why this exists

Joe: *"quarries and mines should need to be within a certain distance from a stone or iron node or else the
building can't be placed. same way fishing hut is for water."*

Today nothing ties a quarry or a mine to rock. `CanBuildAt` asks neither, and `CanPaintWorkGround` refuses only
the wrong *kind* of ground (`FaceOf(workplace)`: *"A quarry works rock — this is grass."*), never the *distance*.
So a quarry can stand in a meadow and paint a seam across the valley, and its quarriers walk the whole way for
every stint. The building reads as decoration, not as the place the stone comes out of.

**Pillars:** §1.1 *legibility* (the building stands where the work is, so the map explains the walk), §2.5
*terrain dictates viability* (the ground decides where a trade can be, as the river decides the fishery).

---

## 2. What already exists to copy

| Building | Rule | Field | Refusal |
|---|---|---|---|
| fishing hut | must **touch** water | `BuildingRow.MustTouch` | *"A fishing hut has to stand against water, and nothing beside that tile is water."* |
| hunter's lodge | a tree within a **reach** | `BuildingRow.HuntingRadius` (12), `SimWorld.ForestTilesWithin` (a diamond) | *"A hunter's lodge needs woods to hunt, and there is not a tree within 12 tiles of there."* |

Joe said *"within a certain distance"*, which is the lodge's shape (a reach), not the fishery's (a touch). The
seam is already named on the **job** row: `works_face` is `Rock` for the quarrier and `IronDeposit` for the miner
(D449). So the building only needs a number.

---

## 3. The rule

> ⭐ **As built (D540): not a third reach field but a placement rule in `MustTouch`'s family.** `Building.cs` warns
> that two "how far this building reaches" fields already exist (`gathering_radius`, `hunting_radius`) and *"a third
> would be a smell rather than a feature"*. So the row carries **`must_be_near: { terrain, tiles }`** (`NearRule`):
> *"must stand within N tiles of terrain T the village can walk to"*, which any building may state. The quarry row
> names rock and the mine an iron seam, both at **`face_reach_tiles` 2** (one config key; 4 until D541, Joe: *"update
> from 4 tiles distance to 2 tiles. i want it even closer."*); a guard holds each to its
> trade's `works_face`. Everything below that says `face_reach` means that rule. The fishery's `MustTouch` and the
> lodge's `HuntingRadius` are untouched; folding them into `must_be_near` is a possible later cleanup.
> ⚠️ **One wording changed while building**: ground the village cannot walk to may be walled in by buildings or
> fences as well as across the river, so the sentence is *"The rock there is cut off — across the water or walled
> in — and the quarriers could not reach it."*

- **A new building-row field, `face_reach`** (tiles; 0 = no rule). The quarry and the mine set it; nothing else.
  The ground it asks for is the face its trade works (`JobsCatalog.WorksFace` of the row's trade), so a modded face
  trade gets the rule by setting one number.
- **`CanBuildAt` refuses** a quarry or a mine with no tile of its face within `face_reach` of the building's tile
  (the lodge's diamond, `|dx| + |dy| ≤ reach`), **counting only face tiles the village can walk to**
  (`TravelCost.CanReach`), the D110/D111 lesson: a seam across the river is not near, it is unreachable, and the
  sentence says which.
- **The order and the words**, after the reachability check, beside the fishery and lodge rules:
  - none at all within reach: *"A quarry has to stand near rock, and there is none within 2 tiles of there."*
  - only across water: *"The rock near there is across the water — the quarriers could not reach it."*
- **Moving a building** (`relocate`) goes through `CanBuildAt`, so the rule holds there too.
- **A building already standing is never refused after the fact**: a save from before this slice keeps a quarry in
  a meadow working as before.
- **The ghost says it before the click**: the view already shows `CanBuildAt`'s sentence for a refused ghost (the
  fishery's and the lodge's work this way), so nothing new is drawn.
- **Cost:** asked at placement and as the ghost moves, at most `(2r+1)²/2` tiles, a reach check only on a face
  tile. Never per tick.

---

## 4. Guards (TDD, red first, red-checked and counted)

1. A quarry beside rock is allowed; the same quarry moved past `face_reach` from every rock is refused, with the
   sentence naming rock and the reach.
2. The same for a mine and iron (and a mine beside stone, with no iron near, is refused: the face is the trade's).
3. Rock only across the river is refused with the *across the water* sentence.
4. A building with `face_reach` 0 (every other row) is untouched: the fishery and the lodge rules still decide theirs.
5. The shipped rows carry the reach (`QuarryReach`, `MineReach` pins), and a modded face row gets the rule from data.
6. Existing quarry and mine tests still find a place (they search outward from the seam: `SomewhereAQuarryFits`).
7. No golden moves (no golden village marks a quarry or a mine); `git diff` says so.

---

## 5. Measured before typed

Over the 64 shipped seeds `quarry.md §6.1` used (scratch `ZzSeamReach`, deleted): every reachable seam, and
whether any tile within the reach of it passes `CanBuildAt` for its building, at founding.

| Reach | Stone seams with a legal quarry site | Iron seams with a legal mine site |
|---|---|---|
| 1 | 555 of 555 | 172 of 172 |
| 2, 3, 4, 6, 8 | 555 of 555 | 172 of 172 |

**No reach strands a seam**, even one tile: seams lie in open ground with room beside them. So Q1 is a call
about feel, not survival. ⚠️ Measured on an empty valley; a seam later hemmed in by the player's own buildings
can lose its sites, and the refusal then says so like any other.

---

## 6. Joe's calls — answered (D540, 2026-10-09): *"1. yes, 4 tiles 2. yes 3. yes"*; then (D541, 2026-10-10) *"update from 4 tiles distance to 2 tiles. i want it even closer."*

**At 2 (D541):** §5's measurement already covered it (every reachable seam keeps a legal site at 1). Three poses
followed the number: `QuarryTests.GiveItRock` and `MineTests.GiveItIron` paint only what the paint rule allows (their
nearest-by-walk six lay past 2), and **the two river guards pose at a reach of 4** (`SeamReachTests.RiverReach`): at 2
no site in the fixture valley has dry ground across the water within reach — the river is wider — so at the shipped
number *cut off* is a walled-in seam far more often than a river. The one-past-the-reach paint check is now asserted,
not `if`-guarded. Same 7 mutants, same 12 reds.

- **Q1 — How far?** Recommended **4 tiles** for both: a quarry at the edge of its rock, the face a few steps from
  the door. 2 is nearly the fishery's *touch*; 8 lets it stand back in the meadow.
> ⭐ **Q2 WIDENED (D561, Joe, 2026-10-11): the whole seam it stands by.** Played: *"can you review the volume of
> give-able ground for iron and stone nodes? it doesn't seem to line up very well with the map and the stone one is very
> small."* The answer below had made the placement diamond the painting limit too: 2 tiles of the building's one tile, so a
> quarry beside an outcrop of thirty tiles could paint four of them. Measured, 100 shipped valleys (`ZzSeamGround`): **rock** 3,116 seams, size median 8 / p90 18 / max 66, and the old diamond reached a median 4 (p90 6) of them even from the best site — on seams of 12 or more, 6 of 16; **iron** 992 seams, median 8 / p90 17 / max 38, the diamond 4 (p90 6) — against a face
> allowance of 12 (quarry) and 6 (mine). His call: placement keeps the 2-tile rule; **painting takes any tile of a seam that
> has a tile within it** — the rock (or iron) joined to it through any of its eight neighbours, rock and iron never one seam
> — and still nothing of a seam the building does not stand by (Q2's reason holds). Built as an index on the ground's own
> counter (`SimWorld.Seams.cs`, `_seamPatch`, rebuilt only when `TerrainGeneration` moves and somebody asks; never hashed
> or saved). The refusal: *"Not the seam the quarry stands by — it works the rock within 2 tiles of it, and all the rock
> joined to that."* Guards: `AQuarryPaintsTheWholeSeamItStandsBy`, `AQuarryDoesNotReachThroughIronToTheRockBeyond`,
> `ASeamClearedInTwoIsTwoSeams`, `ASeamJoinedCornerToCornerIsOneSeam`; mutants — the index never rebuilt 2 (⚠️ first posed
> so it did not build: zero, re-posed), four neighbours instead of eight 1 (⚠️ **zero** at first — no guard posed a seam
> joined at a corner; now one does), rock and iron one seam 1, the old diamond restored 3.
> ⭐ **And the drawing was measured, not changed.** Joe also chose to tighten the drawn seam to its tiles; measured first
> (`ValleyTexture.SeamsHugTheirTiles`, the probe's `seams drawn:` line), the founding valley already draws **99.8 % of its
> stone and iron within half a tile of its own seam** (83 % strictly on its own tile) at every rock wander tried — 0.55,
> 0.30, 0.15, 0.08 — and 91 % of seam tiles as themselves at their centre. The half-tile is the valley's own warp and
> smoothing, river and woods alike; rock's wander never moved it, so it stays 0.55 and the look is unchanged. The line
> guards the reading (floors 99.5 % / 85 %; a wander of 3.0 drowns the seam's own tiles to 71 % and fails it).

- **Q2 — Should a quarry's painted faces also have to be within that reach?** Recommended **yes**. Otherwise the
  rule is a formality: stand beside one stray rock, then paint the big seam across the valley, which is exactly
  the walk the rule exists to stop. *"No"* keeps painting as it is and limits only where the building goes.
- **Q3 — Seams across the river don't count** (recommended, D110). Confirm or overrule.

---

## 7. Definition of Done

- [x] Joe's calls answered and written into §3.
- [x] §5 measured; the number typed into `data/sim.config.json` with the measurement in its comment.
- [x] Guards §4 written red first (`SeamReachTests`, 6: rows, quarry, mine, across the river, painting in reach,
      painting across the water); 7 mutants, 12 reds, no zeros (rule removed 3; reach ignored 3; reachability 1;
      paint distance 1; paint off by one 1; paint reachability 1; the mine's row naming rock 2).
- [x] The four checks: suite 1559 / 0 / 5; view 0 warnings; probe 42 ✅ (`faces:` green), bar height 151; no golden
      moved.
- [ ] Played by Joe: placing a quarry and a mine near and far from their seams, reading the ghost's sentence.
- [ ] `quarry.md`, `iron-mine.md`, `DESIGN.md` §6/§7 and `HANDOFF.md` in the same commit; this status line made true.

---

## 8. ⭐ CLAIMED, NOT PAINTED — a quarry or a mine takes the seam it stands by (D613, Joe 2026-10-19)

**Status:** ✅ **SPECCED AND BUILT (D613) on `slice/d607-notes` — unplayed.** `SeamClaimTests` (6) and the re-posed demand
guards: 7 mutants, 11 reds; probe `quarry claim:` 2 mutants, 2 reds. ⚠️ *As built:* a seam joined only at a corner (one
seam, D561's eight neighbours) is staked as one ring round each edge-joined piece — the palisade goes round what is drawn.

Joe, playing D607: *"for mines and quarries im not sure if i like having to paint the nodes. can we change it so when the
player places a mine or quarry near its ore node, the building 'smart draws' a type of mining/fence structure around the
nearest node? kind of marking it visually as 'this node is being mined/quarried'"*. Asked how much it claims (D608): **the
whole seam it stands by** — his own D561 reach, where today the brush may paint any of it.

### 8.1 The rule

- **Which seam.** Of the face tiles inside the building's placement diamond (`|dx| + |dy| ≤ face_reach_tiles`, the
  diamond `CanBuildAt` already asks — §3), the one **nearest the building's tile by that same distance**, ties broken by
  map order, that the village can walk to (`TravelCost.CanReach`) and that **no other building holds**. Its seam is the
  one claimed: every tile joined to it through its eight neighbours, of the same face (`SimWorld.SeamPatchOf`, D561 — rock
  and iron never one seam). Never an `Rng` draw, so the same quarry on the same ground claims the same seam every run.
- **What is claimed.** Every tile of that seam that `CanPaintWorkGround` would accept for this building today — so the
  rules that already hold for paint hold for the claim: not another building's ground, not a yard, not cut off. The
  ground is written through `Zones.SetWorkGround` as whole tiles (all four quarters), exactly as a painted tile is held,
  so `NextFace`, `IsFace`, the worked floor, the cut-down lumps, the harvest brush's refusal (*"That rock is the
  quarry's."*) and the labour quota all read it unchanged. **Hashed as painted ground always was** (`WorkGroundSub`).
- **When.** **On completion** (`SimWorld.Complete` → `RaiseFinished`), because the work ground belongs to the finished
  workplace's id and a site has another. ⭐ But it is **shown from the moment of placing**: the ghost draws the seam it
  would claim, and a standing site draws it faint (§8.3), from the same pure query (`SimWorld.SeamToClaim(kind, tile)`),
  so the player sees *"this node will be quarried"* before the first log is carried.
- **A seam a building already holds is not shared.** `CanBuildAt` refuses a quarry or a mine where every seam within
  reach is held: *"The rock there is quarry 1's — a quarry needs a seam of its own."* (the mine's names iron).
  *Settled without asking — Joe's to overrule.*
- **Moved** (`relocate`): the old claim is released and the seam at the new place claimed when the move finishes.
  **Pulled down:** released, as painted ground always was (`ReleaseWorkGround`).
- **A face never runs out** (`quarry.md §3.4`), so the claim is for the building's life.

### 8.2 What goes

- **The brush for a quarry or a mine.** *Give ground* and *Take back* leave their cards (`KeepsWorkGround` stays true —
  they keep work ground; nobody paints it). The card says what it works instead: *"Works the seam beside it — 23 tiles
  of rock."*
- **The overstretched note for a claimed seam** (Joe's call: *"…enough for 6. The rest will stand idle."* would fire on
  every seam over six tiles, and a face never runs out — the extra tiles are where a quarrier stands, not work undone).
  `WhyTheFaceIsIdle` keeps its other sentences.
- ⚠️ **What stays:** the placement rule (§3, within 2 tiles of a reachable face), the face allowances as numbers in the
  config (`quarry_tiles_per_worker`, `mine_tiles_per_worker` — read by nothing for a claimed seam; kept for a modder's
  face trade that paints), and every rule of §6 about which seam is *the seam it stands by*.

### 8.3 The look

- **A palisade round the claimed seam**: the seam's tiles traced and rounded the way a yard's outline is
  (`ZoneOutline.Trace(tiles, 1, ∞)`), drawn in a stake colour with a post at each tile's length along it, over the work
  ground's wash. **View only — no walls**: quarriers and miners walk in and out anywhere, so the line never blocks the
  face it marks. Faint while the building is a site and on the ghost; full once it stands.
- Cached in `TraceTheZonesIfTheyMoved` on the counters it already keys on (`Zones.Edits`, `TerrainGeneration`,
  `BuildingGeneration`); the ghost's preview is asked once per tile the ghost moves to, never per frame.

### 8.4 The index (Joe's rule: nothing derivable rebuilt per ask)

- **Each seam's tiles, listed** beside `_seamPatch` and rebuilt with it — only when `TerrainGeneration` has moved and
  somebody asks. Never hashed, never saved (D335). `SeamTilesOf(patch)` returns the list; the claim and the ghost read it.

### 8.5 Saves and the village that already exists

- A quarry or a mine standing in a save keeps the ground it was painted — a load changes nothing, and nothing claims for
  it. Its card loses the brush buttons with every other; its ground goes when it is pulled down, as before.
- No golden village raises a quarry or a mine (D541), so no golden should move; the harness never marks one either, so
  survival is not what to measure — **the seam sizes are** (D561's: rock median 8 / p90 18 / max 66 tiles; iron median 8
  / p90 17 / max 38), and they are the claim's sizes now.

### 8.6 Guards (red first, red-checked and counted)

1. A finished quarry beside a seam holds every tile of that seam it may hold, and nothing of another seam in reach.
2. The nearest seam is the one claimed when two are in reach; a tie goes to map order.
3. Tiles another building holds, and tiles cut off, are left out of the claim.
4. A quarry by a seam another quarry holds is refused, in words; a mine and a quarry by one rock-and-iron outcrop each
   claim their own face.
5. A quarry pulled down releases its seam; a quarry moved claims the seam where it lands and lets the old one go.
6. `SeamToClaim` for the ghost is what completion claims, tile for tile.
7. The card has no *Give ground* / *Take back* for a quarry or a mine, and its sentence counts the claimed tiles.
8. The overstretched note never speaks for a claimed seam.
9. Save → load → hash with a claimed seam; a year after loading matches a village that never stopped.
10. Probe `quarry claim:` — the palisade drawn round a posed claimed seam, closed, within half a tile of its tiles,
    faint for a site.

### 8.7 Joe's to overrule, settled without asking

(a) claimed on completion, shown from placement; (b) a seam is never shared — the second building is refused; (c) the
palisade is a drawing, not walls; (d) the brush buttons go for both trades; (e) existing quarries keep their paint.
