# Spec: The brush — one tool, three layers, a shape you choose

**Decisions:** D42, D86, D87, D92, D198, D221, D327, D332. **Commit B** of the gridless stretch.
**Status:** ✅ **BOTH SLICES BUILT.** **B1** (2026-09-07, D327) — size, shape, right-drag, Escape, one
shape function, and the harvest brush's missing sentence. **B2** (2026-09-08, D332) — the smooth
painted outline, one outline round the brushful, and a switch for the grid lines. **§4.3** (2026-09-11, D350) — a farm's stroke lays whole tiles with a straight border, Joe's call from play.
**1047 passing, 0 failing, 2 skipped of 1049; no golden moved** by either. Bar height still 161 on
every tab × filter, and `zone outlines: ✅` in the probe. ⭐ **Joe confirmed the sizing gesture in
play — *"alt +scroll is perfect"*** — and approved the rest of B1's walk as described. ⚠️ **B2 is
unplayed by him as of this line**: the view has no automated verification of any kind and his eyes
are the only test there is (D11, D160).

⚠️ **This line contradicted itself for one commit** — it claimed both slices built and then said B2
was not started, because the update edited the head and left the tail. **That is D159's exact
failure, in the file that warns about it.** *Fixed in the commit after, and recorded rather than
quietly corrected: a status line is edited as a whole or not at all.*

---

## 1. Goal

The brush is how the player says *where*. It paints three separate layers through one gesture:
**residential land** (D42), **ground given to one building** (D86) and **what the village means to
take** (D87, D92). It has never had a spec — it was described in `building-placement.md §12` and
`build-bar.md §5` from two different angles, and the shape it paints lived as a copy-pasted `for`
loop in two places.

B1 makes it a tool you can aim: **left paints, right takes back, alt+wheel sizes it, and it is a
square or a round.** B2 makes what you painted legible from across the map.

## 2. Which pillar it serves

- **§1.2 no click-farms.** The whole reason zoning exists is that a neighbourhood is a shape you
  draw, not forty clicks (D42). A brush you cannot size is a click-farm again the moment the area
  is bigger or smaller than 5×5.
- **§1.1 legibility.** The preview is a promise about what the next click does. **A preview that
  disagrees with the paint is worse than no preview** — which is why §4 exists.
- **§1.6 traceable over clever.** The shape is one integer predicate, in one function, called by
  both loops.

---

## 3. The gestures

| gesture | what it does |
|---|---|
| **left click / left drag** | paints the held brush's own direction |
| **right click / right drag** | **takes back**, whatever brush is held |
| **alt+wheel, while a brush is held** | resizes the brush |
| **wheel** | zooms, always |
| **`B`, or the shape button** | square ⇄ round |
| **`Esc`** | puts down whatever is in hand — every tool, not just the brush |
| **right click, no brush held** | still cancels, as it always has |

⭐ **The rule a player can be told in one sentence: left paints, right takes back.** It does not
depend on which button was last pressed. With an erase brush already in hand both buttons erase,
which is harmless and never surprising — the alternative (*right does the opposite of what is
held*) makes the right button mean a different thing depending on state, which is the
mode-dependence §1.1 exists to refuse.

⛔ **Right-click had to stop being the universal cancel for brushes, and something had to replace
it.** Seven announce sentences ended *"Right-click to stop."* `Escape` was free — its only use is
dismissing a moment panel, which early-returns above the key switch — so it becomes the cancel for
**everything**, and right-click keeps cancelling the tools that are not brushes (building,
demolish, move, empty), where the gesture has meant that since D43.

⚠️ **The hint lives in the brush's own announce sentence, not in the permanent footer.** D323
measured adding one clause to the footer at **181px against 161** — a contextual hint costs nothing
when it is not needed.

---

## 4. ⛔ The shape is ONE function, and that is the point of this slice

`BrushStroke.TilesUnder(centre, radius, shape)` in `src/Bclone.Sim/World/BrushStroke.cs`. Pure,
integer-only, Godot-free, and **in the library that has tests** — `tests/` has no project
referencing `Bclone.Game` at all, so a shape predicate left in the view is untestable by
construction. Precedent: `HarvestBrush` is a brush concept that already lives in the sim.

**Both loops call it and neither keeps a `for`:**

| caller | file |
|---|---|
| the paint | `VillageMap.PaintAround` |
| the preview | `VillageMap.DrawTheBrushful` |

⛔ **This is the whole structural content of B1.** The two loops were a literal copy-paste of each
other, comment block included — down to a doc-comment reading *"The diamond, not a square"* three
lines above an inline comment reading **SQUARE, NOT A DIAMOND**. `BrushPreviewTests` (D198) asserts
`CanPaint*` ≡ `Paint*` **per tile** and is shape-agnostic, so **it could never have caught a shape
divergence.** One function is what turns *"both loops changed together"* from a comment into a
guarantee.

### 4.1 The two shapes

- **Square** — `max(|dx|, |dy|) <= radius`. `(2r+1)²` tiles. The default, and today's behaviour.
- **Round** — `dx*dx + dy*dy <= radius * (radius + 1)`. Integer only, symmetric under all four
  reflections, and **21 tiles at radius 2**.

⚠️ **Round is not the old diamond and must not become it.** `Abs(dx) + Abs(dy) <= radius` is
Manhattan distance, which paints a rhombus: Joe rejected it in D221 because *"the diamond left
corners unpainted and made a dragged stroke scallop along its edges."* The `r(r+1)` term is what
makes a circle look like a circle rather than a plus sign — plain `dx²+dy² <= r²` at radius 2 gives
back the same 13 tiles the diamond did.

### 4.2 Size

`MinRadius = 0` (a single tile, for precision work) to `MaxRadius = 6` (13×13). Default **2**,
which is the 5×5 that has shipped since D221.

⚠️ **The radius and the shape are NOT touched by `SetTool`.** They are settings that outlive the
tool in hand — picking up a different brush must not silently resize it. `SetTool` remains the only
writer of `_building`, `_harvestMode`, `_groundFor`, `_brush` and the rest (`build-bar.md §5.1`,
three historical bugs); the two new fields are deliberately outside that set.

### 4.3 ⭐ The farm's stroke is WHOLE tiles (D350, 2026-09-11)

`BrushStroke.TilesMostlyUnder(centre, subRadius, shape)` — the tiles with **at least half** their
sixteen quarters under the brush, row-major. Both loops read it through one door
(`VillageMap.CellsUnderTheBrush`), so the paint and its preview stay one shape.

**Why a third function rather than the same quarters for everyone.** Paint is quarter-tiles
(`sub-tile-zones.md`); a plough is a tile. A field hanging off quarter-tile paint stuck out of it by up
to half a tile on every ragged edge, and Joe's screenshot showed it a full tile out (a quarter of paint
was ploughing the whole tile — the sim half of the same bug). For a farm the **paint itself is laid in
tiles**, the border is traced **unrounded**, and the furrows fill what the line encloses: *a ploughed
field is man-made and reads as man-made precisely because its edges are straight* (D342). ⭐ **Joe
chose this over keeping quarter-tile farm paint and clipping the drawn field to it.**

- The threshold is D335's own — at least half — applied at the brush instead of after it, so the tiles
  a stroke lays down and the tiles the farm holds are the same set.
- Housing and the forester's ground keep the quarter-tile stroke and the curve. A home needs a whole
  painted tile (`Household.ChooseSite`); a treeline is ragged.
- ⚠️ A round brush on a farm gives a blocky disc. That is what a round field looks like.

---

## 5. Edge cases & failure modes

- ⛔ **The work-ground arm aborts the whole stroke when its hut is gone**, and that is load-bearing:
  it used to `continue`, so every remaining tile fell through to the residential arm and **painted
  housing land the player never asked for** (`build-bar.md §5.1`, bug 3). It must survive the
  direction refactor unchanged.
- **The preview must follow the gesture, not the tool.** During a right-drag the preview shows the
  erase colour, or the promise is broken in the one moment it is being tested.
- **One sentence per stroke, never one per tile.** A drag across mixed ground is *meant* to skip
  what the mode does not take, and forty refusals would bury the one that matters (D42, D92).
  Unchanged by this slice, and easy to break while touching the loop.
- ⚠️ **A wrapped placement label grows the bar past its own pin and nothing catches it.**
  `PinTheBarHeight` reserves `_placementLabel` at a bare `"\n"` rather than posing a real sentence,
  because the messages come from the map *and* from the sim's refusals and there is no list to take
  a longest from. B1 makes the brush sentences longer, so the probe learns to pose them (§6).
- ⛔⛔ **THE PLAIN WHEEL MUST NEVER MEAN TWO THINGS.** It sized the brush for one commit, gated on
  whether a brush happened to be in hand, and Joe called it: *"i dont want the brush sizing action
  to compete with zoom function. i find it confusing."* **The player is holding a brush precisely
  when they most want to zoom in and look at the ground.** A modifier is a promise that the plain
  gesture never changes meaning; overloading on invisible state is the opposite promise.
- **The alt+wheel branch is guarded on `_brush != 0`, not `IsPlacing`** — alt+wheel over a building
  ghost falls through and zooms rather than doing nothing.

---

## 6. How it is tested

- **`BrushShapeTests`** (new) — the counts, the symmetry, the subset relation, the clamp, and the
  stated enumeration order. **Round at radius 2 is pinned at 21 tiles**, so the shape cannot drift.
- **`BrushPreviewTests`** (D198) gains a fourth: the preview and the paint enumerate the **same
  tiles**, which is expressible for the first time now that both go through one function. Its class
  doc records that the other three are shape-agnostic.
- ⛔ **Every guard red-checked, and the reds counted** (D326): a red check is itself an experiment,
  and a pattern-based edit can revert the wrong line and leave a guard looking blind. Verify by
  line number and `grep -c` the changed text before believing a green.
- **The view is Joe's eyes** (D11, D160). The one thing that can be measured is the width probe,
  and B1 extends it to **pose the brush sentences in the placement label and print what they
  render to** — D255's rule, *print what the control will show before believing a string
  transform.*

---

## 7. Definition of Done

### B1

| # | Item |
|---|---|
| 1 | This spec written, and its status line true |
| 2 | One shape function; **neither loop keeps a `for`** |
| 3 | Left paints, right takes back, on click and on drag, for all three layers |
| 4 | The preview follows the gesture, including mid-right-drag |
| 5 | Alt+wheel resizes while a brush is held; the plain wheel always zooms |
| 6 | Square ⇄ round, from a button and from `B`, with the current setting named in the sentence |
| 7 | `Esc` puts down every tool; the seven sentences say what is true |
| 8 | Guards written, passing, and **red-checked with the reds counted** |
| 9 | **No golden moves** — stated as a `git diff` over the five golden files, not as "the tests passed" |
| 10 | Bar height still **161** on every tab × filter, and no brush sentence wraps |
| 11 | `DESIGN.md` §6 and §7 updated |

### B2 — ✅ MET (2026-09-08, D332)

| # | Item | State |
|---|---|---|
| 1 | The painted region has a **smooth border**, not a staircase | ✅ `ZoneOutline` — traced, straightened, corner-cut twice |
| 2 | Work ground is traced **per owner** | ✅ or two farms whose fields touch would read as one |
| 3 | The **brush preview is one outline**, not 25 boxes | ✅ traced from `BrushStroke.TilesUnder`, so the outline *is* the paint |
| 4 | The per-tile **fill stays** | ✅ the harvest brush's filter means a drag across mixed ground is genuinely green on the trees and red on the stone (D198) |
| 5 | Cached, and its counter **is not hashed** | ✅ `ZoneMap.Edits`; `grep Edits StateHash.cs` is 0 |
| 6 | **No golden moves** | ✅ view only |
| 7 | Guarded despite living where there are no tests | ✅ `ZoneOutline.SelfCheck` runs in the width probe, including a **ring round a hole** |

⭐ **And the grid lines got a switch** — they had been drawn unconditionally above 6px/tile since the
first commit, with no way to turn them off. *The most literal answer to Joe's "why is everything
still in a grid?", and two lines.*

⏸️ **Terrain, forests and the river are the next slice and are not this one.**

*The original scoping, kept:* The smooth painted outline. Contour tracing per zone layer in tile space, corner-cut, cached, drawn
over the existing wash. ⛔ Its invalidation counter **must not enter `StateHash`**, or every golden
moves for a number that is not state. Its own DoD, when it is taken.
