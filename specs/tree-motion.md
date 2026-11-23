# Spec: Tree motion — a tree felled and falling, a young stand growing into a wood

**Status:** ✍️ **SPECCED AND MOCKED (A196, 2026-11-22); ⏸️ SIX CALLS JOE'S (§8). NOTHING BUILT.** Art lane, branch `art`.
The art queue's items (j) and (k) in HANDOFF's 🅿️ ART section, taken together at Joe's pick (via the coordinator: *"tree
growth/felling in a new nested sessions"*). His request (A173, 2026-11-22): *"animated transition for trees 1) maturing
from saplings to full trees and 2) being felled/falling"*. Mocks in `D:\Projects\brushes\trees\motion\`. Owner: Joe +
Claude Code.

---

## 1. What this is, and what it is not

Today a tree changes **in one frame**. A forester's stint ends and the tile's one or two painted trees are simply gone; a
young stand's three saplings a tile are replaced, between one frame and the next, by the grown wood. Both are true to the
sim, which changes a tile in one tick, and both are invisible unless you were looking at the right tile at the right
moment.

This piece gives each of those moments **a few seconds of motion, starting at the tick the sim changed the tile**:

- **(k) the fall:** the felled tile's trees tip over and lie in the open for a moment, then fade.
- **(j) the growing:** the saplings fill out and give way to the grown trees, which rise to their size.
- and, if Joe wants it (§8, call 5), **saplings rising in** where a forester plants or a wood seeds itself.

It is **view only**. It reads the terrain the sim already exposes and never writes anything back.

**Not in this piece:** the chopping before a fall (that is the villager's own swing, A130, already built); trees swaying in
the wind (item (i), its own piece); stumps on a felled tile (the sim has none, and §2's rule refuses them); a felled tree
that stays lying until its logs are carried (the sim's logs are a heap or a load, not a trunk); falling leaves, sawdust, or
sound; anything with the watercolour tick off (§6).

## 2. What the sim says, and must keep saying

**A tree is a tile** (`tree-sprites.md §2`). The sim knows `Forest`, `Sapling` and the rest, and changes a tile only through
`SimWorld.SetTerrain` (D85), in one tick. The view learns of every change one way: **`VillageMap.RefreshTheScenery` diffs a
shadow copy of the terrain when `SimWorld.TerrainGeneration` moves**, and rebuilds the chunks that changed. That diff
already knows, for each changed tile, what it was and what it is now. This piece reads exactly that and nothing else.

Every way a tree comes or goes, read off the sim (2026-11-22), and what each draws:

| the sim | was → is | what plays |
|---|---|---|
| `Harvest`, a forester `Cutting` or a labourer `Clearing` | `Forest` → `Grass` | **the fall** |
| `Harvest` on a farm's ground, ploughed at once (D491) | `Forest` → `Field` (in one tick) | **the fall** |
| `Destroy`, the player's destroy brush | `Forest` → `Grass` | **the fall** |
| `ThinTheRingOf`, a forager thinning the wood (D220) | `Forest` → `Sapling` | **the fall**, and the saplings rise in (call 5) |
| `RegrowthSystem`, a young tile's second period | `Sapling` → `Forest` | **the growing** |
| `RegrowthSystem`, grass beside a wood; `Plant`, a forester | `Grass` → `Sapling` | **saplings rise in** (call 5) |
| `Plough`, `Destroy`, `AfterTakingGround` on a young tile | `Sapling` → `Field` / `Grass` | the saplings fade (call 5) |

**One rule covers the lot:** *a tile that had grown trees and has none now plays the fall; a tile that had young trees and
now has grown ones plays the growing.* The view does not ask *why* — a felled tree and a destroyed one look the same, as
they do in the sim.

⛔ **Never a growth the sim does not have.** The growing plays **at** the tick the sim made the tile a wood, for a few
seconds of the player's time. Before that tick the tile shows saplings and nothing between; there is no slow swelling over
the 72 days.

⚠️ **The regrowth sweep is rolling, not all at once** (`RegrowthSystem`: a slice of the valley each tick, row by row, the
whole valley once a period). So a young stand does not become a wood in one moment: **its tiles turn a row at a time, a
few ticks apart** — on the shipped 120 × 80 valley the sweep takes 33 tiles a tick (a third of a row), so the next row
of a stand turns about 3.6 ticks later, near five seconds at 1×, quicker at speed. The growing plays per
tile as each turns, so a stand fills in as a soft wave across it. The mock shows five tiles turning at once for the look; in
the game they would follow one another.

## 3. The fall (k)

**At the tick a wooded tile loses its trees**, each of its painted trees (1–2 on a grown tile, `PaintedTreesOn`) is drawn
once more, from exactly where the mesh drew it, and falls:

1. **It tips, about one second**, slow at first and quicker as it goes (eased in, as a falling thing does). Seen straight
   down, a tree tipping over is **its crown sliding out from its foot** along the fall line, to about 1.5 tree-halves away,
   with **its trunk showing** as an ink-and-bark line from the foot to the crown: the one moment the game shows a trunk,
   because a standing tree's is hidden under its crown (A31).
2. **It lands, about a quarter of a second**: a small settle back along the line (6 % of its reach) and forward again.
   No bounce beyond that, and no dust (the meditative pace, §1.2).
3. **It lies, about a second**, then **fades, about a second**. Gone 3.4 seconds after the tick.

**The crown is never turned** (A31: the light is painted in, up-left). It is only slid along the fall line and squashed
along it by up to 18 % as it lies, so its lit side stays up-left and its painted shadow down-right. The trunk is a plain
line in the A27 ink (`#3a2d20`) round bark brown, 0.15 tiles wide at the foot.

**Two trees on a tile** fall a beat apart (0.3 s) and a little splayed (about 20° between them), so they read as two
trees and not one tree drawn twice.

**Which way** (call 3). ⭐ **Recommended: into the open.** The fall line points to where the tile's eight neighbours are
not wood, so a tree never falls on top of the standing wood, and a forester eating a wood from its edge drops each tree into
the clearing he has made: the felling reads as the wood's edge moving. **Never onto the feller:** if a villager's drawn
point stands within a tile and within 60° of that line, the line turns away from them to the open side. A tile with no open
neighbour (rare: the inside of a painted patch felled out of order) falls by a direction that is a `static` of the tile.
The alternative is *away from the feller* (the line from the nearest villager through the tile), which needs a feller in
sight and can drop a tree into the standing wood.

**Over or under the wood.** The falling tree is drawn just after the canopy, so it lies over the trees beside it. With the
fall into the open that is rare and brief.

## 4. The growing (j)

**At the tick a young tile becomes a wood**, its three saplings (A51) and its 1–2 grown trees are both drawn for a moment
from their own places:

- **The saplings fill out and give way, 1.5 s:** each grows to 1.45× its size as it fades out.
- **The grown trees rise, 3 s:** each from half its size to its full size (eased out: quick at first, settling), fading in
  over the first 1.2 s.

At the end the grown trees stand exactly where and as the rebuilt mesh draws them, and the motion lets go. The alternative
(call 2, B) is a plain 2-second crossfade from the young set to the grown one, which the mock shows reads as two trees
ghosted on one another halfway.

## 5. Saplings rising in (call 5)

A tile that gains saplings (a forester's planting, a wood seeding its edge, a forager's thinning) draws its three saplings
**rising in over 1.5 s**, from a third of their size and transparent to their own. A tile that loses its saplings to the
plough or the destroy brush fades them out over a second. Recommended because it is the same mechanism and costs nothing
more; without it the saplings simply appear, as today.

## 6. How the game draws it

**Where it starts.** In `RefreshTheScenery`'s diff (`VillageMap.Meshes.cs`), the loop that already compares the shadow copy
with the map tile by tile: where it finds a change it hands `(tile, was, is)` to the motion before it updates the shadow.
A handful of lines. ⛔ **Not on a fresh map** (a load, a new game, the probe's rebuild): those build every chunk and play
nothing — the A169 rule, *no fade on a load*.

**What it keeps.** `VillageMap.TreeMotion.cs` (new): a short list of the motions under way, each its tile, its kind (fall,
grow, rise, fade), the moment it began, its fall line, and the trees to draw — **worked out once, when it starts**, by the
same `CanopyOn`, `PaintedTreeOn` and `KindOf` the mesh uses (D337: a `static` of the tile), so the first frame of a fall is
the standing tree to the pixel and the last frame of a growing is the grown tree to the pixel. Nothing is re-derived per
frame but each tree's place along its motion. A motion is dropped when it ends.

**Its clock.** Real seconds, **still while the village is paused** (call 4), counted in the draw from the frame's own delta,
as the walk-in's clock is (D573). At 5× or 20× a fall still takes its 3.4 seconds; more of them are under way at once.

**The draw.** Straight after the canopy (`DrawThePaintedCanopy`), only for motions on screen: one small mesh a frame of the
motions' quads and trunk lines, drawn with the season's atlas at the canopy's own share, and the next season's over it while
a season turns (A41), so a tree felled in autumn falls gold and in winter falls bare. A frame with no motion under way draws
nothing and costs nothing.

**A safety cap: 512 trees in motion at once.** Past that (a destroy stroke across a whole wood) the rest change at once, as
today. Far above anything a village fells; it keeps a mass stroke from being a frame-time cliff.

**Behind the ticks, as every tree:** only with the watercolour tick on and the *draw the trees* tick on. With the
watercolour tick off the inked discs change as today and the renders compare byte for byte with the build before.

**The files.** New: `VillageMap.TreeMotion.cs` and `VillageMap.TreeMotionProbe.cs` (the precedent: `VillageMap.Bridges.cs`,
A175). ⚠️ **Outside the art lane's table, and Joe's OK asked (call 6):** a few lines in `VillageMap.Meshes.cs` (the diff
hands each change on; the canopy draw calls the motion's draw) and one line in `VillageMap.Watercolour.cs` (the probe's
`tree motion:` line rides on `painted trees:`, art trap 73). No sim file, no `data/` row, no test file, no new texture.

## 7. Verification

- **The probe's `tree motion:` line**, every expectation named from this spec: a tile felled for the check plays a fall of
  its 1–2 trees; **its first frame is the standing tree to the pixel** (place, cell, face, size) and nothing is left after
  3.4 s; the fall line points into the open, and turns off a feller posed on it; two trees a beat apart; a young tile made a
  wood plays the growing and **its last frame is the mesh's grown trees to the pixel**; saplings rise in on a tile planted
  (if call 5 is yes); nothing on a fresh map, nothing with the watercolour or the trees tick off; the clock still while
  paused; the cap holds at 512. Red-checked, the reds counted and the zeroes written down (D326).
- **Renders:** movie mode (`--write-movie`, `--fixed-fps 20`, art trap 59) on a copy of a save of Joe's, a temporary hook
  felling and maturing tiles in view, removed before the commit; with `--screen 1`.
- **Watercolour off:** windowed renders at four zooms byte-identical to the build before.
- **The GPU:** `measure4k.gd`, A/B, a few hundred trees in motion against none, in one sitting.
- **The four checks** in `CLAUDE.md` (the suite by D846's rule: no sim, data or test file will change, so it is skipped
  with its line written); Joe's play; merged only at his word.

## 8. Joe's calls, in plain words, each with a recommendation

The mocks: `D:\Projects\brushes\trees\motion\tree_fall_mock.gif`, `tree_grow_mock.gif`, `tree_sprout_mock.gif` (each
looping, at the game's closest zoom, today beside the options), and `…_frames.jpg` for the same at twice the size, frame by
frame. They are composited from the game's own tree paintings over a real render's grass, so the trees, sizes and ground are
the game's; the motion is the proposal.

1. **How a felled tree goes.** ⭐ **Recommended: A — it tips over into the open, its trunk showing, lies a second, and
   fades** (3.4 s in all). Or **B — it fades where it stood** (1 s), quieter but easy to miss. Or as today, gone at once.
2. **How a young stand becomes a wood.** ⭐ **Recommended: A — the saplings fill out and fade as the grown trees rise to
   their size** (3 s). Or **B — a plain crossfade** (2 s), which looks like two trees ghosted on one another halfway. Or as
   today, a switch.
3. **Which way a tree falls.** ⭐ **Recommended: into the open** (never onto the standing wood, never onto the feller), so a
   forester's felling reads as the wood's edge moving back. Or **away from the feller**.
4. **The clock.** ⭐ **Recommended: real seconds, held while paused** — a fall takes the same 3.4 s at any speed, and a
   paused game is a still picture with the tree mid-fall. Or **game time**, so at 20× a fall is over in a blink.
5. **Saplings rising in.** ⭐ **Recommended: yes** — where a forester plants or a wood seeds its edge, the saplings rise
   in over a second and a half, and they fade when ploughed. The same mechanism, so it costs nothing more. Or **no**: they
   appear as today.
6. **His OK for the two map files** — a few lines in `VillageMap.Meshes.cs` and one in `VillageMap.Watercolour.cs`, both
   outside the art lane's table. ⭐ **Recommended: yes**; the motion's own code goes in two new files.

**What stays as it is, and why:** the trees beside a felled tile can change kind at that moment (a wood's new edge grows
birches, A36's `KindOf`), and that still switches in one frame — the fall covers it, and fading it would mean keeping a
chunk's old mesh alive. Said here so it is not a surprise in play.
