# Handoff — bclone: **▶️ WHEAT IS CONFIRMED; THE PAINT'S EDGE HAS A RULE — JOE HAS NOT PLAYED THE FIX YET**

> **⭐⭐ START HERE. WHERE THINGS ACTUALLY ARE, 2026-09-11 (evening).**
> **1082 passing, 0 failing, 2 skipped of 1084** — run locally on `main`, **2m41s**.
> **The decision log runs to D350**; read D338–D350 in `DESIGN.md §7` for the last two days —
> thirteen decisions, every one of them from Joe playing a build.
>
> **✅ WHAT JOE HAS PLAYED AND SIGNED OFF (D338–D349):** the riverbank on the river, the frame
> readout, clicking a building anywhere it is drawn, the three overlay toggles, grid and snap off by
> default, Settings opening centred, the field renderer, round forest clumps, the wider river, smooth
> painted borders AND fills, the brush's refused ground as a curve inside the ring, boulders on the
> seams — **and wheat, end to end**: *"confirmed: wheat is farmed, harvested by farmer, makes it to
> granary and home larders. and villagers consume the wheat."*
>
> **⚠️ WHAT JOE HAS NOT PLAYED YET (D350):** the five fixes from his own five bug reports.
> **His walk, one thing at a time:**
> 1. Paint a farm with the **round** brush — the border is square-edged, the furrows stop at it,
>    and the preview ring already shows the tiles the stroke will take.
> 2. **Right-drag a strip off a field** — it is grass again, with no furrows left behind. Do it over
>    a sown or ripe strip and the bar says how many tiles of crop were given up.
> 3. Place a **forager's hut in a wood, snap off** — the site is drawn **orange** at its true point
>    and angle, and there is no ring beside it. It turns grey once the laborers have cleared it.
> 4. Paint a **thin housing stroke** (one tile wide, off-grid) — no house appears on it; widen it to
>    cover whole tiles and one does, fully inside the line.
> 5. Click the **market** — *"Holding: 312 produce, 40 firewood…"* is the second line, with no
>    scrolling. The panel is as tall as its text now.
>
> **⛔⛔ THE THING THIS SESSION LEARNED:** **D335's half rule said what a painted TILE is and said
> nothing about what the EDGE should look like.** Every whole-tile consumer of quarter-tile paint —
> a home, a plough, a felling — sticks out of the paint by up to half a tile unless its edge rule is
> stated, and one of them (the plough) was worse than the rule: it fired on the first quarter. Three
> edge rules are written down in `sub-tile-zones.md §3.1`; **the next whole-tile consumer needs a
> fourth, on the day it is built.**
>
> ⭐ **Joe plays every build and files precise bugs.** Thirteen of the last thirteen decisions came
> from him playing for ten minutes. **Ask him one question at a time, with the measurement in it.**

## ⛔ FIRST: THE ONE BUILD COMMAND THAT MATTERS

**`dotnet build` at the root does NOT compile the game.** It builds `Bclone.Sim` and the tests
only — the Godot project is built by Godot.

```
dotnet build src/Bclone.Game/Bclone.Game.csproj      # the view actually compiles
BCLONE_PROBE_WIDTHS=1 "$GODOT" --headless --path src/Bclone.Game   # the only view "test"
```

⭐ **GODOT IS INSTALLED ON THIS MACHINE:**
`D:\Projects\Godot\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64.exe` (`run.bat`
names it). The probe takes seconds. ⛔ **A session once declared the view unverifiable because ITS
container had no toolchain and nearly made Joe compile by hand** — check `run.bat`, and check CI,
before ever saying the view cannot be built.

⭐ **THE PROBE IS THE ONLY VIEW TEST AND IT NOW CHECKS FIVE THINGS.** Read all five lines:
`roster:`/`vlog:` (held content **beside** drawn height), `fold:` (every panel shrinks),
`bar height:` (**must stay 161** — the footer has wrapped and cost 20px twice now, so anything
touching it gets re-measured), and `done.` — **if `done.` is missing, Godot did not quit and you
have left a process running.**

⚠️ **A failed local build looks exactly like no change at all.** Godot runs the last assembly that
*did* build, with no error dialog. **The tell is in the build output, never on screen.**
## What landed since the last handoff — gridless, D317 → D326

**✅ THE NUMBER TYPES ARE BUILT AND GUARDED.** `Fixed` (Q32.32) and `Angle` (16-bit binary angle),
both in `src/Bclone.Sim/Core/`. ⭐ **Rounding is FLOOR everywhere** — truncation folds the bucket at
zero to twice the width of every other, and the valley straddles its own origin. **Overflow
throws**; wrap would be silently wrong *identically on both machines*, so the determinism suite
would stay green while a villager teleports. ⭐ **`Angle` needs no overflow policy at all**: a
`ushort` rolling over at 65536 **is** a circle closing at 360°, so the whole `if (angle > 2π)`
class of bug does not exist. *That is the payoff of the representation Joe chose.*

**⛔⛔ `Math.Sin` MAY NEVER BE CALLED FROM SIM CODE.** IEEE-754 mandates correct rounding for
`+ - * /` and **square root** and *not* for the transcendentals, so sine can differ across
platforms — **in the one way no test on a single machine can detect.** `Angle.Sin()` reads a
checked-in 257-entry quarter-wave table; worst error 20,213 raw against a theoretical `h²/8` of
20,212. ⭐ **`Math.Sqrt` is NOT banned** and `specs/tick-loop.md §6` says so out loud — *an
over-broad rule gets ignored.*

**✅ BUILDINGS HAVE A FOOTPRINT AND A FACING, IN EVERY STATE.** Ghost, construction site, finished
building and demolition all draw the true extent at the true angle. **Middle-drag turns what is in
your hand** (shift = fine), R steps a sixty-fourth, shift+R a quarter. ⭐ **The rule a player could
be told: a building covers the tiles whose CENTRES it stands on** — not every tile it clips, which
would make a turned building creep outward unpredictably. **A 1×1 covers its own tile at every one
of 65,536 angles**, which is why facing landed on every building and moved no golden.

**✅ THE LONGHOUSE (3×1) IS THE FIRST BUILDING THAT IS NOT ONE TILE.** A storehouse rather than a
dwelling, because housing is brush-painted and never placed by a button (D42, D102). It exists so
extent and facing are **content** rather than machinery with nothing behind it.

**⛔⛔ AND THE MESSAGE THAT COST A VILLAGE (D322).** Joe marked six longhouses across two runs and
nothing was built — correctly, because **a village with no builder's hut raises nothing**. But
`BuildersWanted` returns `anythingToBuild ? seats : 0`, which is zero for *nothing marked* **and**
for *nowhere to build from*, and all three consumers reported the first. He was told *"there is
nothing marked to build"*, went hunting a placement bug, and everybody starved. **The one line that
would have saved him — "needs 1, build a builder's hut" — was structurally dead**, because
`Needed(Builder)` came from the same seat-capped number. *Fixed, and both halves are guarded.*

## ✅ SETTLED — do not re-open or re-ask

- **The window work** (D306, D307) — Joe: *"Tested - they're great!"*
- **Move and Empty's tabs**, the bar height (161 on every tab and filter), the mid-build longhouse,
  the amber "No builder's hut stands…" warning, and **every tile of a longhouse selecting,
  naming and demolishing** — all confirmed by him in play.
- **⭐ Two more confirmed by Joe in play on 2026-09-07:** **a rotated stockpile stays rotated**
  (D326's free-building path, which drew square while its ghost drew turned), and **the professions
  panel is folded under *The village***. *Both from the chair, which is the only place they could
  be checked.*
- **The cold start is fine as it is.** Joe, having lost a village: *"i just wasnt paying attention
  and playing at 20x. pebcak."* ⛔ **Do not soften the opening.**

## ✅ COMMIT B1 IS BUILT — the brush is a tool you can aim (D327)

**Left paints, right takes back, ALT+wheel sizes it, and it is a square or a round.** `specs/brush.md`
is the spec, written first. **1023 / 0 / 2 of 1025, and no golden moved** — stated as a `git diff`:
nothing under `tests/`, `src/Bclone.Sim/` or `data/` was modified at all. ⚠️ **JOE HAS NOT PLAYED
IT.** The view has no automated verification of any kind (D11, D160) and his eyes are the test — see
the six-step walk at the bottom of this section.

- ⭐⭐ **The structural half: `BrushStroke.TilesUnder` is the one shape function**, in `Bclone.Sim`
  because nothing in `tests/` references `Bclone.Game`. The two loops it replaces carried the same
  pasted comment warning that they must change together — **and one copy had already gone stale.**
- ⛔ **`Escape` cancels every tool now.** Right-click still cancels everything that is not a brush.
- ⛔⛔ **The harvest brush had never had an announce sentence of its own** and said *"Drag to paint
  where the village may build homes."* Found while rewriting them; `build-bar.md §5.1a`.
- ⚠️ **Size and shape are NOT written by `SetTool`** — deliberately, they outlive the tool in hand.
- ⚠️ **`B` cycles the shape**, and a *"Brush: square/round"* button sits on the **filter row**, not
  the tool strip: the strip already wraps on BUILD + ALL. **Bar height still 161 everywhere**,
  measured.

- ⛔⛔ **THE PLAIN WHEEL ALWAYS ZOOMS.** Sizing is **alt+wheel**. It was the bare wheel for one
  commit and Joe rejected it in review: *"i dont want the brush sizing action to compete with zoom
  function. i find it confusing."* **Do not re-propose overloading the wheel** — the player is
  holding a brush exactly when they most want to zoom in and look. ✅ **Confirmed in play:
  *"alt +scroll is perfect"*.**

**✅ Joe approved the walk** (2026-09-07): right-drag takes back with an amber preview; `B` and the
button both swap the shape; a building ghost still zooms and still right-click-cancels; `Esc` puts
down every tool; the bar holds its height across the tabs. ⚠️ **Approved as DESCRIBED, except the
sizing gesture, which he confirmed in play.** *The difference is worth keeping: a described gesture
is not the view test, and this is the project with no automated view verification at all.*

## ✅ GRIDLESS 2c IS BUILT — the anchor is continuous (D329)

⛔⛔ **AND THEN JOE PLAYED IT AND IT WAS BROKEN — SEE D331 BELOW.** The walk here is still the walk;
what changed is that step 1 now works.

▶️▶️ **THE VIEW LET GO (D330).** Snap is a Settings toggle, **on by
default**, so a player who never finds it sees exactly what they saw before. **The walk:** place a
granary with snap on (lands on a tile centre as always); turn snap off and place one between two
tiles (it stands where you put it, and the tinted coverage shows which tiles it claimed); turn a
longhouse mid-placement with snap off (the refusal and the ghost agree); move one and demolish
another (both keep their true angle and position); and **check nothing has shifted half a tile.**

- ⛔ **The float never crosses into the sim.** The cursor becomes an exact rational —
  `Fixed.FromRatio(round(tiles × 4096), 4096)` — at one boundary. D2 stays whole.
- ⭐ **The snap rounds the point BEFORE `Mark` sees it**, so it is an input-layer setting and the
  sim believes exactly what is drawn. *Not a facade, which §7.2 refuses by name.*
- ⚠️ **Nothing persists settings anywhere in this project**, so snap returns to on at every launch.

- ⛔⛔ **THE HALF-TILE SEAM IS THE THING TO KNOW BEFORE TOUCHING THE VIEW.** The view draws tile
  `(x, y)` **centred** on `ToScreen(x, y)` — which is why its grid lines are at −0.5 — while the sim
  says that tile's centre is `(x+½, y+½)`. **Both are internally consistent and they are half a tile
  apart.** One conversion carries it (`VillageMap.ToScreen(Point)`) and the probe now asks the
  question outright: `tile centres: ✅`. **Red-checked at 33.94px, 0.71 of a tile.**
- ⭐ `Position` is the `Point`; **`Tile` is derived** and is what every cost-field and terrain
  lookup asks. The 19 tile-keyed economy entries are untouched, which is the whole of option C.
- ⭐ **Everything the SIM places stays on a tile centre** (Joe's call) — homes, the founding layout,
  the two free gifts. The `GridPos` overloads of `Mark`/`CanBuildAt`/`FootprintOf` say so out loud.
- ⚠️ **`Covers` was rebuilt because the CLOCK caught it, not a test.** A continuous origin widened
  `CoveredTiles`' scan and **the suite went 4m55s → 9m**; `Covers` was answering about one tile by
  building the whole list. Asked directly it is **2m02s**. *Watch that number.*

## ⛔⛔ THE P0 JOE FOUND, AND WHAT IT MEANS FOR THE NEXT PERSON (D331)

**He placed a woodcutter's hut in the middle of four squares, turned. It claimed ZERO tiles.**
Unselectable by every finder, and **no builder could ever raise it** — `SiteAt` reads the site from
the tile the builder is standing on (D108), so the crew arrived and there was nothing there. The log
said it in one line: *marked out at tick 0, never mentioned again in 348 ticks*, while a builder
stood idle with 130 logs.

- ⭐⭐ **A unit square reliably contains a point of a unit lattice only while it is AXIS-ALIGNED.**
  Turned 45° its reach is 1/√2 ≈ 0.707. **The grid hid this for the whole project's life**, because
  every building sat on a tile centre; free placement reached it the same day it shipped.
- ⛔ **`CanBuildAt` did not object, and the reason is worth carrying:** its refusal loop *iterates*
  the covered tiles, so an empty list means the loop body never runs. **A check that iterates a set
  says nothing about the empty set** — and it reads as thorough in review.
- ⭐ **Two questions, two rules now.** *Which ground does it claim?* — the centre rule, with a floor:
  **a building always stands on at least the tile its centre is in.** *May I build here?* — a
  **separating-axis test between the rectangles**, which is what §7.3 promised and what tile
  occupancy cannot do once two buildings straddle one boundary.
- ⛔⛔ **TOUCHING IS APART (`>=`) AND BACKWARDS WOULD BREAK EVERY VILLAGE.** Two 1×1s on adjacent
  tile centres are exactly one apart with radii summing to exactly one.

## ▶️ NEXT, IN ORDER

**Nothing is queued by Joe beyond his own open items below.** The things this stretch named and
did not build, in the order they are likely to matter:

1. ~~**⚠️ JOE PLAYS WHEAT (D348–D349)**~~ ✅ **Confirmed in play, 2026-09-11.** What he found
   instead was the paint's edge (D350) — **and the D350 fixes are the thing he has not played.**
   The walk is in the box above; wait for it before touching the brush, the field or the inspector.
2. **The food chain, when he asks:** wheat → flour → bread, wheat → beer
   (`food-catalog.md §6`). ⛔ **This is where raw wheat stops being edible** — today it is
   edible at the shared nutrition because the config refuses two values and the survival floor is
   derived on farms feeding people (D277, D348). *Deriving a diet is the precondition, not the
   milling.* A second crop (barley, corn) is a `CropRow` and nothing else — but the farm cannot
   choose which yet (`CropsCatalog.TheOne` is the lowest id); a *"grow…"* control on the
   farmhouse is where that goes.
3. **The new-game screen (Joe's stated ambition, D344):** archetype, sliders, a seed string, a live
   preview. ⛔ **Read the OPEN item on RNG streams first** — every generation stage a new
   option adds reshuffles every seed unless worldgen gets per-stage seeds, and the naive way to do
   that (`DeterministicRandom`'s `stream` parameter with small ids) measurably breaks valleys.
   ⭐ The preview is nearly free: `MapGenerator.Generate` is pure and `ValleyTexture` bakes a map
   to an image.
4. ⏸ **`gridless.md §8` slice 3 — villagers hold a `Point`.** Buildings are continuous;
   people still step tile to tile. Unscheduled since D330.
5. ⏸ **Housing packing** (Joe's call: *pack nicely as households form*) — the scoping from the
   previous stretch still holds: `ChooseSite` is `score = toWork + toStore`, and packing is a third
   term in that sum. ⚠️ `MarkHome` raises every home at `Angle.Zero`.

✅ **Closed by Joe and not to be re-opened:** the build strip wrapping to two rows on BUILD + ALL
(*"fine for now"*); harvest marks staying on felled ground (D127, reaffirmed D343: *keep it
standing, draw it quieter*); a hard valley being a legitimate roll (D344).

## ⛔⛔ THE TRAPS THIS SESSION PAID FOR (D350) — THE EDGE OF THE PAINT, AND A FOLD WITH NO BAR

**Joe played wheat, confirmed it, and filed five bugs. Four were one fact and the fifth was a fold.**

1. **⛔⛔ A QUARTER OF PAINT PLOUGHED A WHOLE TILE.** `PaintWorkGround(SubTile)` called
   `AfterGivingGround` → `Plough` on the *first* quarter painted, so every tile a farm's brush grazed
   became a `Field` while the farm held none of it — furrows a full tile past the paint on every
   edge. ⭐ **The shape to look for: a whole-tile side effect wired to a sub-tile write.** The hold
   threshold (`ZoneMap.Holds`, ≥ 8 of 16) is the gate now, both ways.
2. **⛔⛔ NOTHING HAD EVER UN-PLOUGHED.** `EraseWorkGround` and `ReleaseWorkGround` freed the paint
   and left `Field/Sown/Ripe` standing for ever — the brush's take-back and a farmhouse's demolition
   both. *A state machine with a way in and no way out is a state machine that leaks onto the map.*
   `AfterTakingGround` is the way out; `RetireWorkplace` snapshots the held tiles BEFORE the release,
   because the release is what forgets them.
3. **⚠️ D335'S HALF RULE SAID NOTHING ABOUT THE EDGE.** A home on a tile residential at 8 of 16 is
   drawn on the whole tile and stands 0.4 past the border. Three edge rules are stated in
   `sub-tile-zones.md §3.1` (home: whole tile; farm: whole-tile paint, straight border; forester and
   harvest: half rule, curve). **The next whole-tile consumer of paint needs its own row.**
4. **⚠️ A LONE PAINTED TILE IS A CIRCLE, AND IT IS DRAWN AT THE TILE.** D334 rounds one-tile runs as
   staircase steps, so D100's self-painted clearing mark under a site became a ring — beside a hut
   that stands at its free-placed point (D330). The mark is real; the picture of it is now the site,
   orange while its ground is busy. ⭐ **The trace cache keys on `BuildingGeneration`** (the
   `TerrainGeneration` shape) because a site can arrive on already-marked ground with no paint edit.
5. **⛔⛔ A SCROLL WITH NO VISIBLE BAR IS A CUT, AND THE PANEL SAID NOTHING.** The market is a
   workplace AND a store, so its description was the longest in the game and `Holding:` was line
   eleven of a 140px `RichTextLabel`. Joe: *"the market doesn't tell how many units…"* — **it did.**
   `FitContent` now; the probe poses a market-sized text and prints content vs minimum
   (`inspector:` line — must stay ✅). *D311's lesson from the other direction: a panel can hold its
   content, draw some of it, and give no sign there is more.*
6. **⚠️ A THROW INSIDE THE PROBE LEAVES GODOT RUNNING.** `AnyStoreOf(Granary)` throws on the cold
   start (a cart, no granary) — the exception went to the log, `done.` never printed, and a headless
   Godot sat for five minutes until killed. **Read the log for `Exception` before assuming a hang is
   a loop**, and pose against `StoreBuildings[0]`, not a kind that may not exist.
7. **⚠️ ONE RED CHECK SCORED ZERO AND IS WRITTEN DOWN** — `TheFarmsTilesComeBackInAStatedOrder`
   stays green with the sort deleted, because a `Dictionary` with no removals enumerates in insertion
   order. The sort is contract, the guard is a ratchet, the test's remarks say so (D326).

## ⛔⛔ THE TRAP THIS STRETCH PAID FOR — A PANEL CAN HOLD ITS CONTENT AND DRAW NONE OF IT

**The roster and the village log were both `288x0` for two commits** (D311). Joe sent a screenshot
reading *"6 villagers in 2 households"* beside a blank panel.

⭐ **A `ScrollContainer` lays its content out at the content's MINIMUM height** — that is what makes
scrolling possible — and **an `ItemList` and a `ScrollFollowing` `RichTextLabel` each report a
minimum of zero**, because each scrolls itself. `SizeFlagsVertical = ExpandFill` buys nothing
inside a scroll: there is no spare space to expand into. **A self-scrolling control needs a
`CustomMinimumSize`, not a wrapper.**

⛔⛔ **AND IT WAS D306'S OWN FIX THAT CAUSED IT.** D306 gave every panel its own `ScrollContainer`
because panels used to borrow the deleted column's — and **the two panels whose content already
scrolled are the two it broke.** *A fix aimed at a container class will hit the members that did
not need it.*

⚠️ **The empty log is also why the previous stretch read as "the game starts with no villagers"** —
four founders froze in Winter Year 1 and every line saying so rendered into nothing.

## ⏸️ OPEN, AND JOE'S TO CALL

- ~~⭐ **NEXT: A FIELD LOOKS LIKE A FIELD (Commit L of the wheat plan).**~~ ✅ D349. Bare/furrowed `Field`, sparse green `Sown`, dense gold `Ripe` — marks from a hash like the trees, **not** overhanging (a field's edge is a fence line). Ripe stalks in the wheat chip's colour so map and Overview agree. View only.
- ~~⭐⭐ **NEXT: WHEAT AS THE FIRST REAL FOOD, NOT A RENAME (Joe, 2026-09-11: "option 2").**~~ ✅ D348, **confirmed in play by Joe the same day** (D350). `Goods.Produce` is the food umbrella (index 0, hashed since D82, 56 call sites) and `food-catalog.md §` already has Wheat as a *Grain* with a chain (→ flour → bread; → beer). **The farm's crop becomes a real new good; `Produce` stays what foragers fill.** A proper slice: goods catalog row, farm/crop system, stores, sentences, goldens move. Read `food-catalog.md` and `goods-catalog.md` first.
- ⭐⭐ **PER-STAGE RNG STREAMS, DEFERRED TO THE NEW-GAME SCREEN (D344, Joe's call).** Worldgen threads **one** generator through river → founding → soil → seams → woodland, so **draw order is the seed contract** and *every future map option that generates something — a lake, an island, a cliff — reshuffles every seed and re-takes every golden, once per option.* ⭐ One stream per stage fixes that for good. ⛔ **Build it with splitmix64 per-stage SEEDS, not `DeterministicRandom`'s `stream` parameter** — small adjacent ids correlate badly (measured: 6 dead valleys of 24 against 1). ⚠️ **And expect to re-pick the shipped seed**: the reshuffle put 12345 on a valley that starves.
- ⚠️ **The stone and iron seams are still Manhattan diamonds, and D347 left them so on purpose** — the boulders hide the shape, and changing it changes ore. *Revisit only with the economy in view.* (Originally: left because they were the subject of the next slice (*"give the stone and iron deposits the same treatment we just gave forests and trees"*) and changing their shape changes how much ore a valley holds. *`InsideTheClump` is sitting there ready for them.*
- ⛔⛔ **THE FOREST CLUMPS ARE MANHATTAN DIAMONDS AND ONLY THE GENERATOR CAN FIX IT (D342, measured by trying).** `PaintForest` drops diamonds of tiles; the new field renderer nibbles their edges by about a tile, which on a nine-tile diamond leaves a soft diamond. *The river is transformed by the same machinery because it is two tiles wide — the difference is the ratio of the jitter to the feature.* ⭐ **The fix is a few lines in `MapGenerator.PaintForest`** — a noisy disc instead of a Manhattan ball — **and it moves every golden** (D152: one commit, one stated reason). **Joe's call, and his since D337.**
- ⭐⭐ **NEXT SLICE, ALREADY CHOSEN BY HIM (2026-09-10):** *"we should give the stone and iron deposits the same treatment we just gave forests and trees. including showing depletion over time. same with farming (and this default crop we're using now 'produce' — which we should change to wheat when we work on this)."* He deferred it explicitly in favour of the fixes and UI that became D338–D340. **Three parts: deposits get marks and visible depletion, farming gets the same, and `produce` → `wheat`.** ⚠️ *The rename touches `data/`, the specs and every sentence the village says — it is not a string swap.*
- ⏸ **`gridless.md §8` slice 3 — villagers hold a `Point`** — is still unscheduled. Buildings are continuous; people still step tile to tile.
- ⚠️ **`PaintForest` drops Manhattan DIAMONDS of tiles**, so a wood's outline is chunky *and* faintly diagonal, and **no amount of smoothing the render hides it** (D337). That is a generator question and it is his.
- **The food limit gates the HARVEST, not the sowing** (D300, his call). ⚠️ It reverses a recorded
  decision: `crops-and-orchards.md §5.1` wanted use-it-or-lose-it to *"punish inattention rather
  than obedience"*, and unreaped crop now rots at winter. **He accepted the rot** and floated a
  future tech unlock letting crops stand into winter.
  - **⏸️ THE THIRD OPTION, RESTATED FOR HIM ON 2026-09-06 AND STILL HIS: GATE NEITHER.** Farmers
    sow in spring *and* reap in autumn regardless of the cap. ⭐ **For it:** the limit already stops
    foragers, fishers and hunters through `foodIsEnough`, so a farm finishing what it *started* is
    not the village ignoring him — and it deletes rot-by-obedience entirely, which is the one
    failure `crops-and-orchards.md §5.1` explicitly does not want. ⛔ **Against it:** a harvest
    lands in a lump, so the cap stops meaning *"stop at N"* for the one trade that can overshoot by
    a whole field. **He has not answered.** If he takes it, it is the harvest arm plus
    `FarmerSeatsWithGroundToWork`, and it wants its own §7 entry.
- **`gathers_per_thinned_tile` stays 0 for ever** (D297) but is kept as a modder dial.
- **`ApprenticeshipTests` stays skipped** (D287, his explicit call). *Do not "fix" it* — restoring
  it means reversing D227.
- ⛔ **THE DEMOLITION OF A ROTATED BUILDING DRAWS AT THE RIGHT ANGLE NOW (D325), BUT ONLY BECAUSE
  IT IS ASKED WHILE THE BUILDING STILL STANDS.** Nothing records a facing once a building has
  become a demolition site, so if that lookup ever moves later in the sequence it silently draws
  zero again. *Named rather than guessed at.*
- ⚠️ **`specs/gridless.md §10` carries four more open items**, including the locale guard that
  scores zero on its red check and is kept knowingly as a ratchet, and the fact that **there is no
  error boundary in `SimLoop`** — `Fixed` overflow joins three existing in-tick throw sites and
  nothing catches any of them. **What the game should DO when a tick throws is unanswered.**

---

## The stretch before this one — kept because its lessons are still live

> **⛔⛔ THE ECONOMY BUG THAT DEFINED THIS STRETCH (D236–D239), because its lessons outlive it.** Joe played to Year 44
> and reported *"I painted stone deposits in year 25 and they never harvested, which upheld the
> building of my 2nd granary."* **That was the smallest visible corner of a stalled economy.**
> His audit trail says: **clearing runs 40 / 16 / 4 times in Years 1–3 and then once, in Year 31**;
> **granary 2 was marked out in Winter Year 23 and was still an unbuilt site at Year 44**, twenty-one
> years on, while three houses went up around it; and the spare labour force made about **fifteen
> thousand** round trips fetching loads it never picked up — **1,439 to one tile in the last 900
> ticks, by fourteen villagers**, one with no goods line anywhere in the log.
>
> **⭐⭐ THE CAUSE WAS TWO FINDER FUNCTIONS THAT DISAGREED ABOUT WHERE A GOOD MAY GO.**
> `NearestStore` matched on kind and fullness and **never asked `Accepts`**; every other finder
> asks. Fixed, along with the three things that made it invisible: a **stalled site now says what
> it waits for**, a **met limit stops the job and keeps the seat**, and the **limits panel measures
> what the sim decides on**. The forager's hut is also **called what its workers are called** at
> last (D240), and a discovery is now a **celebratory banner that does not pause the game** (D241).
> The **bottom bar wraps** instead of running off both edges once the library button appears
> (D242), and a **villager's panel lists the techniques they carry** (D243).
> **855 passing, 0 failing, 2 skipped of 857.**
>
> **⭐⭐⭐ THE LESSON WORTH MORE THAN THE FIX: IT WAS FOUND IN THE LOG, NOT IN THE CODE.** Nobody
> reading `StoreForTheLoad` had spotted it in months. Twenty minutes of
> `grep`/`awk` over `src/Bclone.Game/logs/` — counting state transitions per year, per villager —
> found it, sized it, and proved it. ⭐ **Bucket the behaviour histogram before forming a
> hypothesis**; "clearing per year" collapsing to zero is a sentence no test in this suite can say.

> **✅✅ MERGED TO `main` AND PUSHED (2026-08-28, Joe: *"then merge to main"*).** A clean
> fast-forward — `main` had no commits of its own — so **`main`, `origin/main` and
> `phase/4-the-tech-tree` are all the same commit.** D217's trap is finally not live: the build
> Joe plays and the remote are one thing again.
>
> ⚠️⚠️ **IT WENT TO `main` WITH PHASE 4'S DEFINITION OF DONE UNMET, AND THAT IS RECORDED RATHER
> THAN TICKED.** **Slice 3 (the knowledge screen) is unbuilt and the §5 QA walk has never been
> performed** — item 4 of the phase's own DoD, the one it says it is *not allowed to waive*.
> **This is Joe's call, asked and answered three times**, and it is exactly what D203 did for
> Phase 3. ⛔ **Do not treat the merge as evidence the phase is finished; it is evidence he
> wanted the work on `main`.** *If a Phase 4 regression ships, this is where it got through.*

> **⭐⭐ WHAT PHASE 4 HAS: techniques (D225), the library (D226), the pacing fixes his play forced
> (D227, D232, D233), and the whole build/undo loop rebuilt around them (D228–D231, D234, D235).**
> **⛔ WHAT IT DOES NOT HAVE: slice 3, the knowledge screen** — `phase-4-the-tech-tree.md §3`. And
> its **QA checklist (§5) has never been walked**, which is the debt Phase 3 left and this phase
> wrote a checklist specifically to avoid repeating.

> **⭐ READ `specs/phase-4-the-tech-tree.md` FIRST** — it is the phase plan, the four design calls
> and the QA checklist in one document, and it is current.

Read `CLAUDE.md`, then **`DESIGN.md` §0–§5 in full, §6, and §7 from D326 back to D300** (and
`specs/gridless.md` in full — it is the live direction), then
`METHODOLOGY.md`. **Then `specs/content-inventory.md`** — it is the audit of what actually exists
against what the documents claim, and it is the shortest route to being oriented.

> **⚠️ THE MOST EXPENSIVE THING THAT HAPPENED IN THIS STRETCH WAS NOT A BUG — IT WAS AN UNMERGED
> BRANCH.** Eleven commits of finished, green work sat in a worktree while `main` had none of it.
> **Joe played `main`, saw *"villagers harvest stone but the pile shows 0"*, and reported a bug
> that was real on his build and already fixed on the branch he was not running** (D217). Then the
> merge itself was left half-finished — two unresolved conflicts, nothing committed — and a fresh
> session had to find that before it could review anything.
> **Merge it, or say plainly that it is not merged. A branch nobody merges is a branch that lies
> to whoever plays the game.**

> **⛔ WHEN YOU HAND OFF: EDIT THIS FILE, DO NOT REPLACE IT.** The trap list at the bottom is
> accumulated from sessions that each paid for one entry. Rewriting it wholesale drops them
> silently — that happened on 2026-08-22 and cost an hour and three quarters within the same
> session. **Rewrite "where things are"; carry the traps forward.**

⭐ **Four things to know before you touch anything:**
1. **▶️ PHASE 4 IS OPEN — D205's hold was lifted by Joe on 2026-08-26 with one word: *"start"*.**
   ⛔ **Do not re-litigate whether to build it.** `specs/phase-4-the-tech-tree.md §1` already
   answers `content-inventory.md` finding 5's objection, and §2 records four design calls he has
   since confirmed or overruled in play.
2. **⭐⭐ HIS PLAY IS THE BEST BUG-FINDER THIS PROJECT HAS, AND THIS STRETCH PROVED IT SEVEN TIMES.**
   D227, D232, D233, D234 and D235 all came from him playing for ten minutes. ⛔ **Three of them
   were features that existed only in the sim** — the library was invisible, then Move and Empty
   had no buttons. **A sim feature is not done until something in the view calls it, and no test
   in this suite can tell you that.** *Build the button in the same commit as the feature.*
3. **✅ THE BUILD/UNDO LOOP IS PEOPLE-SHAPED NOW** (D228–D231). Demolition is **reverse
   construction** — a builder's job costing half the building's own work. Housing is the **brush's
   business in both directions**: unpaint to mark, repaint to call it off. **Any building can be
   moved**, and **a store can be emptied on request.** ⛔ *Nothing in the village happens by a click
   any more* — demolition was the last exception, unnoticed because it was the player's own hand.
4. **⚠️ THE FARM IS UNPARKED (D194)** and **Joe's thirteen tiles were never available** — thirteen
   tiles ten ticks from a store needs ~230 ticks of a 120-tick autumn. Read the farm section below
   before re-opening it, and **do not propose `farm_store_cap` — it is dead twice over.**

---

## ⭐⭐ What landed while Phase 4 stayed held (D206–D218, 2026-08-24/25)

**The hold was never idleness.** Joe's content pass arrived and the infrastructure it needs got
built underneath it — none of which is Phase 4, and all of which Phase 4 will stand on.

- **⭐ Joe wrote the technique list** (D206, `TECH-EXAMPLE.md`): 45 buildings over four tiers,
  **39 named techniques**, 25 animal species. **They are diegetic, not a research menu** — asked
  directly, because the document reads like one. Mapped into `tech-tree.md §9` with two new trunks.
- **⭐ Morale is real** (D207) — per villager, doing **exactly two things: people leave, and
  households have fewer children.** *Work slows* and *sickness* were both offered and **declined**,
  which is what keeps it clear of §1.1's invisible multiplier and §0.1's death spiral.
  `specs/morale.md`.
- **⛔ Spoilage was re-proposed and refused again** (D208). Winter feed is a seasonal fact, not a
  rot timer. **D37 stands.**
- **✅ The school** (D209) — a teacher, slots for children **12–16**, graduates who work better,
  **paid for with four working years per pupil** because `adult_age` is 12. It cashes a dial
  **D156 reserved in August in the same words.**
- **✅ A good is a ROW, and the set is open to 62** (D210, `specs/goods-catalog.md`). Every switch
  on a good is gone from the sim. **Four ceilings nobody had counted** were found and lifted —
  six goods, thirty goods, stock limits, and the villager's arms.
- **✅⭐⭐ STONE IS REAL END TO END** (D211–D217). A villager **could not carry stone** — the arms
  were the one stockpile D82 never reached, so a cleared seam's yield **stopped existing**.
  Red-checked before the fix: *eight seams cleared in two years, zero stone anywhere.* Then
  **multi-material building costs** (a granary is 40 logs and 10 stone), the stone taken off the
  map rather than out of the cart, and the food limit finally reaching the forager.

⚠️ **Five goldens moved, once, deliberately** — the arms are hashed by index now. **Measured, not
assumed:** restoring the old three-line mix makes every one byte-identical, which is what says the
hash's *shape* moved and the village did not.

- **✅ A TRADE IS A ROW TOO** (D218, `specs/jobs-catalog.md`). `content-inventory.md` finding 8's
  second half. ⛔ **Its red check is the most useful thing either catalogue produced:** *eight of
  nine new guards passed a break they should have caught*, because the test's own JSON listed rows
  in id order and so could not tell **id** from **position**. D157's green-and-blind, third
  instance. The cure is a fixture where the two differ — a file listing the trades backwards.
- **✅ THE GRANARY IS A BOX** (D219). `granary_feeds_people: 30` → **`granary_capacity: 2500`**,
  Joe: *"it's fine if the granary feeds a different number of people. The user should build more
  granaries — and will need to!"* **Deriving the box meant a village that ate more got a bigger
  granary for free**, which is the opposite of a pressure. ⚠️ **The one place D16 does not apply**,
  stated in `VillageEconomy` so nobody restores the derivation.
- **✅ A PLANTED TREE TAKES AS LONG AS A SEEDED ONE** (D220), and **Joe found it by playing**:
  *"trees planted by the forester [are] ready to fell very quickly."* Planted ground matured
  **three times faster** than seeded — near-instant at worst. ⭐ **The cause was a comment that was
  TRUE, for one path**: *"a sapling seen by a sweep has stood for one period"* holds for saplings
  the sweep seeded itself and **was never checked against the forester's.**
- **✅ Player-facing fixes** (D221 and the batch before it): the game **starts paused**; the brush
  is **square**; the stock-limit panel **means the numbers it shows** (food 2000, firewood 400 —
  ⚠️ *above* the village's own 360 target, because a default below it would freeze the village by a
  control nobody touched); the stockpile **refuses food** so it stays on the cart until a granary
  stands; demolition **warns before destroying** what is inside; the comfortable-walk ring is
  **gone**; and **saplings now have a colour and a sentence** — they had *neither*, drawn as mature
  woodland and described as *"open ground"*.

---

## Where things are

**Phase 3 is merged to `main`.** Its Definition of Done is met **with one item waived and written
down rather than ticked** (D203): METHODOLOGY §3's manual QA walk. Joe played the build
repeatedly through the phase and signed off the paint overlay, the market, the staffing cadence
and the whole — **but the phase was never walked end to end against a list, and Phase 3 has no
checklist at all.** *That is an unpaid debt Phase 4 should not inherit.*

**⭐⭐ WHAT PHASE 3 LANDED, in the order it landed:**

1. ✅ **The proficiency substrate** (D181, D183). `Villager.Skills` accrues time on the task,
   hashed sparsely in id order; six skills are **rows in config, not enum values**; the panel
   says *"Sixteen years as a farmer"*; **the mastery line fires** in the village log.
   **Nothing ever takes proficiency away**, and a tick out on the job is worth 1.5 of a tick
   waiting for one. Ages at mastery: **34–55, median 39.**
2. ✅ **Mastery bites** (D187) — **a master takes half the ticks over an action, rounded up.**
   ⚠️ **Below 34% the feature is literally a no-op**: durations are 3 and 4 ticks, so a bonus
   that does not round to a whole tick buys **nothing** — a village at 25% produced population
   and food *identical* to one with the feature off. `AMasterIsFasterAtEveryTrade` fails the
   build if it ever rounds away again.
3. ✅ **The mixed founding and the seeded rhythm** (D190) — a master, a journeyman and two
   novices with **seeded trades**, and a rhythm drawn at birth. **D28 discharged.**

4. ✅ **The at-risk line** (D195) — §11's last outstanding Definition-of-Done item. *"Wendell is
   48 and the only soul in the village who has mastered foraging. Put somebody beside them to
   learn it, or it goes with them."* One method (`SimWorld.KnowledgeAtRiskNote`), read by the
   village log **once on the edge** and by the villager's panel **while it is true**. Both halves
   of the condition are derived — `LifeStage.Elder`, and *the only living master*.

5. ✅ **APPRENTICESHIP** (D202) — §2.1's actual claim. **A youth beside a master of the same
   trade at the same workplace learns twice as fast.** Nobody is assigned to anybody; the master
   pays nothing; there is no dial. §10's anti-vacuity guard is green — **masters alive after a
   century go 3 → 6, 4 → 8, 8 → 10** against a village that never teaches.
   - ⛔ **IT REACHES ONLY 2–3 TRADES OF 5, AND THAT IS RECORDED RATHER THAN PAPERED OVER.**
     Forager and marketer always pair, forester sometimes; **woodcutting and building never do**,
     because they are one-seat trades with nobody to learn from. **The trades most likely to die
     with their last holder are exactly the ones apprenticeship cannot reach** — which is what
     **D196's library** is for, and why that answer is worth more than it looked.
   - ⚠️ **200% was too far**: seed 42 ends the century with **zero food**. A hundred leaves it at
     1,485 against 1,513 with the feature off. *The width is measured, not picked.*

⚠️ **The phase PR is #4, not #3.** Number 3 went to the closed screenshot-hook PR D160 rescued,
and every document in the repo said #3 for a day before anyone checked.

**Merged slice branches are deleted on Joe's standing preference**, each after checking it had
**0 commits not on `main`**. Tips if ever wanted back: `phase/3-skill-and-apprenticeship`
`028f4fc`, `phase/2-wood-fuel-and-tools` `9b9f410`, `slice/per-site-yield` `b2cb718`,
`slice/faster-cost-field` `daec8fd`, `slice/the-market-that-never-gets-staffed` `48ab7e5`.

⛔ **ONE BRANCH IS GENUINELY UNMERGED: `slice/work-from-the-steading` (`e12b20f`, 1 commit).**
Farmhands staying at the farm through the working seasons — **an economic no-op that costs ~13%
of the harvest**, kept for the look, and its cost is still unexplained. ⚠️ **It predates D194's
rewrite of the sowing cap, so it will not merge cleanly and its measurements are stale.**

✅ **Phase 3 is merged AND PUSHED** — `main` and `origin/main` are level.
⚠️ **It went straight to `main` rather than through a PR**, on Joe's call (*"push"*), where Phase
2 went up as [PR #4](https://github.com/joemachen/bclone/pull/4). **There is no PR #5** — do not
go looking for one.

⚠️ **Do not write a commit hash into this file for anything that keeps moving.** The line above
named one and was stale within the minute.


**SUITE, FROM A RUN (2026-08-29, after the town hall's slice 1):**
```
880 passed, 0 failed, 1 skipped of 881 - 2m45s (was 18m52s before D179)
```

**The one remaining skip is a ruling, not unfinished work: D143** — an unattended village is
*supposed* to die out, and that guard measures an empty valley rather than three-century
stability. ⚠️ **Its stated fix — give it `PlayTheOpening` and assert the peak and the causes of
death — is unblocked and has design content in it** (what peak?), so it is a small slice rather
than a housekeeping edit.
> ⭐ **There were two until 2026-08-28.** The other was skipped because *"the timber shed is the
> binding cap at 343/343"* and *"restore when D134's open question is answered"* — **both halves
> had expired** (`ShedCapacity` is floored on the granary's; D143 answered D134) and it passes.
> **A skip is a claim about the world and nothing re-reads it.**

```bash
dotnet test bclone.sln --nologo -v q
```

**It is fast enough to run in the foreground now** (D179 took it from nineteen minutes to two and a half). **Do not start a second run while one is going** —
and **check `Get-Process testhost` first**: one session found the *previous* session's run still
alive on thirteen cores, holding the lock on `Bclone.Sim.dll`, with nobody left to read its
output. Note the CPU figure is summed across cores, so a healthy run shows far more CPU-seconds
than wall-clock.

**The Godot view builds separately** — `dotnet build src/Bclone.Game/Bclone.Game.csproj` (D11) —
and has **no automated verification of any kind** (D160). Looking at it is the test.

---

## ⭐ What to do next — `DESIGN.md §4`'s queue, in its order

> **⭐ THE LIVE CALLS AS OF 2026-08-29 ARE IN THE BANNER AT THE TOP OF THIS FILE.** What follows
> is the record of how the queue emptied — kept for the reasoning, not as a to-do list.
>
> 0. **⭐⭐ JOE SHOULD REPLAY SEED 12345 BEFORE ANYTHING ELSE.** D236–D239 changed how the village
>    works, and **the acceptance criteria are in his log, not in the suite**: clearing must not
>    collapse to zero after Year 3, and fetch trips without pickups must not run to thousands.
>    Re-run the two commands in the audit-trail trap below against a fresh log and compare.
> 0b. **✅ SIX OF HIS SEVEN ARE DONE; ONE IS BLOCKED ON HIM** (2026-08-27/28, he
>    chose "fix the economy first, alone"):
>    - ✅ **The *"gatherer's hut"* naming is DONE** (D240) — it is *"forager's hut"*, and the two
>      hand-written view sentences now read from the catalogues rather than holding the word.
>    - ⏸️ **A UI PASS IS OWED AND DEFERRED ON HIS CALL** (2026-08-28: *"the bar looks better for
>      now. still lots of overlap between menus, but we'll fix that in a later UI pass"*). The
>      bottom bar is fixed and measured; **the panels still overlap** — the left column draws over
>      the speed buttons. ⛔ **Do not start it piecemeal, he has said when.** Two things to fold
>      in when it happens: the panels have **no z-order or reserved regions**, so every new panel
>      is a new overlap; and *"Builder — nobody working of 21 seats"* in a village of four adults
>      is **`BuilderHutCapacity` derived from the economy horizon (D16) and not a bug**, but it
>      reads as one.
>    - ✅ **The technique-discovery modal is DONE** (D241) — a non-pausing celebratory banner,
>      coloured in the log, saying whether the village can keep the technique. The
>      no-library-at-all case, which had no sentence anywhere, now has two (one for a village
>      that cannot write yet).
>    - ✅ **The village log's colours and category filters are DONE** (D244) — seven categories
>      decided at the source, one switch each, and the switch doubles as the legend. **Seasons
>      alone are 42% of a sixty-year log**, so one click does most of the noise reduction.
>      ⚠️ *Warning fires 214 times in sixty years — a warning that frequent is furniture, and is
>      worth a look in the UI pass.*
>    - ✅ **PINNING A VILLAGER TO A TRADE IS DONE** (D247) — **and the guard never forbade it.**
>      `NoPublicApiLetsACallerAssignAVillagerToAWorkplace` blocks naming a person into a
>      *building*; `SetPinnedTrade(Villager, JobKind?)` names a *trade* and passes untouched.
>      ⭐ **Joe offered to overrule it and it turned out not to need overruling** — worth
>      remembering the next time a guard looks like it is in the way. ⚠️ It needed **five**
>      mechanisms and read *0 of 4,311 ticks* through four of them; the last is that **a pin
>      outranks cost in the candidate sort.**
>    - ✅ **ALL SEVEN OF JOE'S ITEMS ARE NOW DONE**, and the QA walk with them (D248).
>
> 1. **✅ BOTH ARE ANSWERED: PUSHED (2026-08-27) AND MERGED TO `main` (2026-08-28).** ⛔ **Do not
>    re-ask either.** ⚠️ **The phase's DoD is still unmet** — slice 3 unbuilt, the QA walk never
>    walked — and that is written into the banner at the top rather than quietly ticked.
> 2. **⛔ SLICE 3 — THE KNOWLEDGE SCREEN — IS THE REST OF PHASE 4.**
>    `phase-4-the-tech-tree.md §3`. It mostly *surfaces* what exists: which techniques the village
>    has, who holds each one, and how close the last knower is to dying.
>    `SimWorld.KnowledgeAtRiskNote` (D195) already answers the third and is on the villager panel.
>    - **⛔⛔ IT IS BLOCKED ON A DESIGN CALL NOBODY HAS MADE, FOUND 2026-08-27 AND WRITTEN INTO THE
>      SPEC AT SLICE 3.** `tech-tree.md §8` says the knowledge screen **is the town hall's interior
>      and is reachable only once one stands** — and the same phase spec's ⏸️ list puts **the town
>      hall explicitly out of Phase 4**. **No `TownHall` exists in `src/` or `data/`** (checked).
>      *The slice asks for a screen whose front door is out of scope.* Three ways out are written
>      out in the spec — ship it ungated, pull a minimal town hall in, or pause the phase here —
>      and **it is a legibility call, so it is Joe's. Do not pick one silently.**
>    - ⭐ **This does not block the QA walk: 21 of the 22 checks cover slices 1 and 2.** Only check
>      21 needs the screen.
> 3. **⛔⛔ THE QA CHECKLIST HAS NEVER BEEN WALKED** — `phase-4-the-tech-tree.md §5`, 22 checks.
>    **Phase 3's walk was waived and this phase wrote its checklist on day one specifically so that
>    debt would not compound.** *Walking it is a Definition-of-Done item, not a formality, and it
>    is the one item this phase is not allowed to waive.*
> 4. ⚠️ **THEN FISHING AND HUNTING** — `buildings-plan.md §10` step 1, food breadth, and **Joe
>    chose it as what comes after Phase 4 pauses** (2026-08-26). Phase 4 does not have to be
>    *finished* first; he agreed to pause it at a clean point.
>
> ⭐ **Recently settled and NOT open, so they are not re-argued:** the build menu becoming
> catalogue-driven is **deferred on his call** (D223); the forester's regrowth pace is **settled by
> play** (D224); the ageing/technique interaction is **fine by him** (D225); `demolition_work_percent`
> at **50% is confirmed** (2026-08-26: *"half the build time is fine"*).
1. ✅ **Phases 0–3 are all merged to `main`.** Phase 2 went up as
   [PR #4](https://github.com/joemachen/bclone/pull/4); **Phase 3 went straight to `main`
   (D203), so there is no PR #5.** Branches are deleted after checking each had 0 commits not on
   `main` — tips recorded above.
2. ✅ **`specs/skills-catalog.md` IS BUILT, and its status line says so** (D181–D202). Read it
   before touching skills — but **read it as a record, not a plan.** Its §12 still holds the
   tuning questions nobody has answered.
3. ✅ **`specs/per-site-yield.md` §4.2a and §4.3** (D194) — the farm remembers what it brought in.
   **Read the farm section below before reopening any of it.**
4. ✅ **`specs/storage-and-distribution.md` §14.8–§14.9** (D197, D199, D201) — the marketer stocks
   the market, storage buildings are separate from it, and its service area is a **count, not a
   ring**.
5. ⏸️ **PHASE 4 — THE TECH TREE (§2.7) AND THE TOWN HALL (D176). HELD, NOT STARTED (D205).**
   - **⛔⛔ THE BLOCKING ITEM IS JOE'S CONTENT PASS, AND IT IS NOT YOURS TO CLEAR.** Do not
     "helpfully" start on the substrate while he thinks — that is precisely the getting-ahead he
     stopped. **Ask him where the content pass got to before proposing anything.**
   - **⭐ WHAT THE AUDIT FOUND, because it changes what Phase 4 even is** (`specs/content-inventory.md`):
     - ⛔ **`buildings-plan.md` is missing four of the ten buildings that exist** — BuilderHut,
       ForesterHut, Farmhouse, Pile — and the work-ground zone. Its ✅ marks claim six built.
       **A catalogue missing 40% of what is built will generate content that duplicates it.**
     - ⛔ **`BuildingRecipe` is `(int Logs, int WorkTicks)` — one material slot for the whole
       catalogue**, against a tier system where the mason's yard *"gates every durable building"*.
       **Stone and iron are already quarried, mined, stored and hashed; nothing spends them.**
       That is structural, not content, and it touches every recipe, the hauling, the build queue
       and the goldens at once.
     - ⛔ **18 catalogue rows carry a knowledge flag and none of those 18 buildings exist**, so a
       tech tree built today would have almost nothing to gate.
   - **✅ D204 SETTLED ONE THING WHILE THE PLAN WAS BEING DRAWN: recording is AUTOMATIC AT
     MASTERY** (Joe), not the seasons-long scriptorium project `tech-tree.md §7b` describes.
     ⚠️ **The consequence to carry: §11's guard against *"the library is mandatory"* rested on
     three costs and this deletes one**, so **the hard shelf cap is carrying it nearly alone** —
     which makes *a full library refuses the record and says so* load-bearing rather than polish.
     **The scriptorium and literacy are deferred, not deleted.**
   - **⭐⭐ JOE'S LIBRARY MODEL IS ALREADY RECORDED AND IT IS CONCRETE (D196).** A master
     woodcutter works out *"splitting lumber in a way that gives more cords — +15% firewood per
     log, +5% mastery"*; **the technique enters the library's records when he reaches mastery**;
     when he dies **his proficiency dies with him** but the technique does not, and **the next
     woodcutter spends idle time in the library learning it.** Where a trade has more than one
     worker the master also passes it to his apprentice directly.
   - **⭐ IT LANDS EXACTLY ON D176's SPLIT WITHOUT HAVING BEEN ASKED TO**, which is the strongest
     sign that split was right: **technique** is the village's and writable, **proficiency** is
     one person's and never writable. **The anti-ratchet holds** — `tech-tree.md §3a`'s *"a record
     preserves the method, not the proficiency"* is what stops §2.3's dead late game.
   - ⚠️ **The one part to measure before it ships:** a technique granting *"+5% mastery gain"* is
     **a soft ratchet on proficiency itself**, one level up. Bounded and probably fine, but it is
     the only piece of the model that touches the rule rather than sitting beside it.
   - **⭐ AND IT IS THE ANSWER TO APPRENTICESHIP'S HOLE**: one-seat trades have nobody to learn
     from, so the library is what carries their knowledge **across a gap in people** where
     apprenticeship carries it **between** people.
   - ⛔ **The list of techniques is deliberately NOT invented yet** (Joe: *"we don't have to come
     up with the full list… eventually they will all have a number of them"*) — `tech-tree.md
     §12`'s refusal of false precision.
   - ⚠️ **WRITE PHASE 4 A QA CHECKLIST.** Phase 3 shipped without one and its walk was waived
     (D203); **that debt should not compound.**
6. **Also on the board, unscheduled**, all recorded with Joe's rulings: **nomads and the
   dead-village revival** (§5, and it needs **building decay**, which reopens D65's *"repair after
   damage, no decay on a timer"*); **house upgrades and the 60–80 firewood target** (§5 — ⚠️ a
   6–8× change to a derived burn, **not a dial**); **foods with different nutritional values**;
   and the **steading slice**, still unmerged on `slice/work-from-the-steading`.

---

## ✅ THE FARM IS UNPARKED (D194) — and here is what is settled, so nobody re-opens it

**The ledger the section below asked for was built, and it answered in one sitting.** Kept as
`FarmLedgerTests` so the numbers can be **re-taken rather than trusted**.

**⛔⛔ THE CAP WAS SELF-FULFILLING, AND `ReapableShareAt` IS DIMENSIONALLY WRONG.** It scaled a
farm's field by `budgeted ÷ haul` — `budgeted` is a **round trip inside the field** (4 ticks),
`haul` is a **one-way walk to a store** (10). *The ratio is not a share of anything.* Measured,
one hand, ten years, committed ground posed at each level:

| farm → store | the cap sowed | what it can actually bring in | autumn spent **idle** at the cap |
|---|---|---|---|
| 10 ticks | 5 | **6** | **27%** |
| 16 ticks | 3 | **5** | **45%** |
| 22 ticks | 2 | **4** | **55%** |

**The cap cut the field, the farmer then had nothing to do, and the idleness read back as proof
the field had been too big.** After: **72 tiles reaped against 51 at ten ticks, idleness 6%.**

**⛔⛔ AND THE THING TO CARRY FORWARD: THIRTEEN TILES TEN TICKS OUT IS PHYSICALLY IMPOSSIBLE.**
Autumn is **120 ticks**; thirteen tiles at that distance needs about **230**. Joe's farmer was
short of **one or two** tiles, not eight. **The lever for thirteen is the walk** — the same
farmer beside a granary commits the whole field. §4.3's placement warning and the farm's own
panel now both say so.

**The fix is memory, not a better formula, and "no formula fits" is a finding.** The true ceiling
depends on the market's drain rate, the painted ground's shape, the granary's fullness and the
hands that turned up; `season ÷ (reap + walk)` wants a different constant at every distance,
moving the *wrong way* with distance. **A farm sows what it has already brought in** — a
high-water mark, per hand, clamped to `FieldTilesOneFarmerKeeps`, re-reckoned when the walk
changes. It converges on **6, 5 and 4** without being told them.

**⛔ CAUSES NOW DEAD — five proposed, all rejected by measurement. Do not add a sixth by
reasoning.**

| proposed cause | what killed it |
|---|---|
| the granary haul | removing it entirely still left the farm at ~7 tiles |
| the daily commute | travel is **11%** of a farmhand's ticks |
| resting outdoors getting cold | farmhands' cold is **zero, always** |
| the buffer (`farm_store_cap`) | raising it gave 13 tiles and **52% brought in** — the rot came back |
| **the buffer, again (D194)** | an **8.7× buffer** moved the ceiling from **6 tiles to 6** at ten ticks and **5 to 5** at sixteen. It still took only **23 of 72 loads** — it fills once and the market cannot keep it drained. **Two independent measurements now.** |

⚠️ **`crop_yield_per_tile` is NOT the lever** and Joe proposed it: raising it would inflate a
derived number to paper over a bug and leave well-sited farms at ~2.5× gathering.

**Still open:** the steading slice (farmhands staying at the farm through the working seasons) is
committed but **unmerged** on `slice/work-from-the-steading` — an economic no-op that costs ~13%
of the harvest, kept for the look, and its cost is still unexplained.

**Two directions Joe set, neither scheduled, both in `DESIGN.md §4`:** **gridless** — the largest
architectural statement anybody has made about this project, and the first question when it is
taken is whether the *sim* goes continuous or only the *presentation* — and **mods that can add
anything** (`BuildingKind`, `JobKind`, `Goods` and `Terrain` are four C# enums hashed by
position; `crops-and-orchards.md §4` is the template for doing it right). Standing discipline for
the second: **when you add a new kind of thing, ask whether it wants to be an enum value or a
data row.**

---

## ⛔⭐⭐ THE TRAP THAT WILL NOT ANNOUNCE ITSELF — read this before building roads

**The travel-cost field is a breadth-first sweep since D179**, and that is correct **only while
every passable tile costs the same to cross.** It replaced an O(n²) Dijkstra that was costing
**four seconds per world** and very nearly the entire test suite.

**§2.6 desire-path roads say crossing thresholds *"lowers pathfinding cost, creating a
reinforcement loop."*** The day a worn path is cheaper than grass, **BFS silently returns wrong
answers** — it keeps the first route it finds rather than the cheapest, and nothing throws.

- **Then, and only then, go back to a priority queue** — `PriorityQueue<int, long>` keyed on
  `((long)cost << 20) | index`, which keeps the tie-break and stays O(E log V).
  **Never back to the scan.**
- ⚠️ **No guard in the suite would catch it.** Every one describes a valley where the uniform-cost
  rule still holds. The symptom would be villagers taking scenic routes for a phase.

Written in three places on purpose: here, `TerrainCostField` itself, and
`pathfinding-and-water.md`'s header.

---

## Tools this project has that you would not guess

- **⭐⭐ `Skip 1y` / `Skip 10y` IN THE CONTROL BAR — debug builds only (D254).** Ten years in about
  **a quarter of a second**, against 10.7 real minutes at 1x. **Use it to reach a late-game QA
  item instead of leaving the game running.** It steps the same `SimLoop.Step` everything else
  does, so a skipped village is byte-identical to a played one; it **stops early on a moment**, and
  leaves the game paused. ⛔ *Debug-gated deliberately — `DESIGN.md §1`'s meditative pace. It is a
  tool, not a speed.*
  - ⚠️ **And know the arithmetic before adding another speed button:** `target_ticks_per_second` is
    **0.75** and a year is **480 ticks**, so 1x is **10.7 minutes a year** and 10x is **64 seconds
    a year**. The sim itself does **58 years in 1.53 seconds** and the spiral guard does not bite
    until **~20,000x**. *The speed buttons are a pacing choice; nothing about them is a limit.*
- **`BCLONE_PROBE_WIDTHS`** (METHODOLOGY §6). Walks the control tree headless in two seconds and
  prints what every panel and inspector row claims as a **minimum width**, including the rows
  posed with their worst-case sentence. **A column is never narrower than its widest child**, so
  every width in `Main.BuildUi` is a *request* — three sessions have asked this question and two
  hand-rolled the same throwaway before it was kept.
  - ⭐ **It now poses the control bar with every category the village will ever unlock** (D242's
    blind spot, which had been left open inside the tool built to close it), and prints **the sum
    of each row's items** beside `wants` — because *an `HFlowContainer`'s minimum is its widest
    single child* and `wants` cannot see a new category at all.
  - ⭐⭐ **AND IT PRINTS WHAT THE VILLAGE LOG WILL ACTUALLY RENDER** (`[log]` lines, D255), through
    the real `LogMarkup` with the BBCode stripped back off. **It caught a sentence left hanging on
    an em dash on its first run**, which nothing in the code would have shown. *Print what the
    control will show before believing a string transform.*
    ⚠️ It **steps the sim twelve years**, which every other probe deliberately does not — safe only
    because it runs one line before `GetTree().Quit()`. **Anything added after it has to move.**
- **`grep "food from the field"`** — `HaulTheHarvest` writes its reason — free space, both costs, which store won — so *"why did
  the farmer walk past the buffer?"* is one grep rather than an afternoon:
  `grep "food from the field" src/Bclone.Game/logs/<newest>.log`
- **The audit trail** at `src/Bclone.Game/logs/`. Almost every bug that mattered came out of it.

---

## ⏸️ ANSWERED, AND IT IS JOE'S TO OVERRULE: **DOES THE GRID NEED TO BE FINER?**

He asked it while looking at a clumsy painted circle: *"do we need a much finer underlying grid? is
the current one too coarse? that will change building scale, well, everything scale."*

**My answer is no, and the evidence is that the shapes were being destroyed by the SMOOTHING rather
than by the resolution** (D333) — he was looking at a 5×5 square rendered as a sixteen-sided figure.
With the corner-cut capped, a square reads as a square and a round brush reads as round.

⛔ **What a finer grid would actually cost, with the numbers rather than a shrug:**
- **The map is 120×80 = 9,600 tiles.** Halving the tile size makes it **38,400** — four times the
  draw loop (already walked six times a frame), four times every `TerrainCostField` Dijkstra, and
  D179 records that field being the entire suite's bottleneck at the *current* size.
- **Nineteen config entries are stated in tiles**, all anchored on `gatherer_hut_ring_tiles = 8`
  through `VillageEconomy.MaxHomeToWorkTiles`. ⛔ **D122 froze nineteen people when that chain moved
  by ONE tile.** `gridless.md §6` calls re-deriving it *"the single biggest risk"* in the whole
  direction — *"not arithmetic, it is a re-balance."*
- **Every golden moves**, and the D211 byte-identical proof is unavailable, because the village
  genuinely would be different.

⭐ **AND THERE IS A CHEAP ANSWER TO THE REAL COMPLAINT UNDERNEATH IT, WHICH IS THAT EVERY BUILDING IS
ONE SQUARE.** `ExtentWidth`/`ExtentHeight` are **already per-row in the catalogue and already work** —
the longhouse is 3×1 and has been since D321. **Making a granary 2×2 is a data edit**, costs no
economy change, moves no derived number, and is the thing that would actually make buildings look
like buildings rather than tokens. *If "everything is the same size and that size is a square" is the
complaint, that is where to spend the effort.*

## Traps, in the order they will cost you

- **⚠️ A DELTA ON A VILLAGE-WIDE TOTAL CANNOT TELL ONE TRADE FROM ANOTHER (2026-09-11, D348).** A guard compared village produce before and after a farm's autumn and called any rise "the reap writing produce" — the fixture village has foragers, who raised it by two. *Read the instrument that only the thing under test can move* (the farm's own store's produced counter).
- **⛔ A NEW ENUM VALUE TAKES THE NEXT ID, AND A TEST FIXTURE MAY ALREADY BE SITTING ON IT (2026-09-11, D348 — and the same fixture said "6 stopped being free the day Fish shipped").** `ModdedGoodTests` keeps its modded good one above the last built-in; every new good bumps it. The loader says so plainly — read the message before assuming the new code is wrong.
- **⚠️ A RESTATED GUARD CAN BE UNCHECKABLE IN THE FIXTURE IT RUNS IN (2026-09-11, D348).** The umbrella test sums produce + wheat, but the unattended fixture never farms, so wheat is 0 and dropping it from the sum is invisible. Recorded as a zero rather than dressed up.

- **⛔⛔⛔ TWO GUARDS CAN BE BLIND TO THE SAME DEFECT FOR DIFFERENT REASONS, AND AGREEING WITH EACH OTHER PROVES NOTHING (2026-09-10, D345).** The perimeter guard read 1.00 on a twenty-sided polygon (a chord polygon has a circle's perimeter); the turning-angle guard read 7° on it (it skipped every corner beside a duplicate point). **Both green, Joe counting the sides on screen.** ⭐ *When the player can see something two guards cannot, dump the actual data and look at it* — the 209 raw points showed the chords and the duplicates in thirty seconds.
- **⛔⛔ CUTTING A STAIRCASE AT EXACTLY HALF A STEP STRAIGHTENS IT INTO CHORDS (2026-09-10, D345).** The midpoints of a regular staircase are collinear. D333's *"never more than half a run"* cap is the right rule for not turning inside out and the wrong rule for smoothing; the rounding is a second stage with a quarter cut. *Measured, not reasoned: quarter-cut alone reads 1.10, half-cut alone reads 18°.*
- **⚠️ A ZERO-LENGTH SEGMENT YOU SKIP TAKES ITS NEIGHBOURS' TURNS WITH IT (2026-09-10, D345).** Skipping the degenerate segment at index i also skipped the angle at i-1 and i+1, which is where the real corner was. **Dedupe first, then measure.**
- **⚠️ A GUARD THAT COMPARES BOX CORNERS FIRES ON A SYMMETRIC SHRINK, WHICH IS NOT WHAT IT WAS FOR (2026-09-10, D345).** The seam guard exists to catch a translation; rounding a cell into a curve moved every edge 2px inward and reddened it. *Compare centres for translation, sizes for scale, and know which one you are guarding.*
- **⭐ HOLES IN A FILL DO NOT NEED KEY-HOLING (2026-09-10, D345).** Delaunay over all loop points, then keep triangles whose centroid is on painted ground. Concavity and rings fall out of the one rule. `DrawPolygon` would re-triangulate every frame and refuse a hole; `RenderingServer.CanvasItemAddTriangleArray` takes the cached triangles as they are.

- **⛔⛔⛔ DO NOT COMPARE RARE EVENTS ON SMALL SAMPLES (2026-09-10, D344).** Two twelve-seed runs each showed one dead village, so I told Joe the rate was *"about one in thirteen, and pre-existing"*. **It was one in four, and it was mine.** It took 48 seeds to see it, and a **median-peak** comparison — 17.8 against 17.5 — to establish the fix was neutral, because *a rare-event count has enormous variance and a distribution statistic has almost none.* ⭐ **When the thing you are measuring is rare, measure something common that moves with it.**
- **⛔⛔ `DeterministicRandom`'s `stream` PARAMETER IS NOT A SAFE WAY TO GET INDEPENDENT SEQUENCES (2026-09-10, D344).** Streams 1–5 give PCG increments 3, 5, 7, 9, 11, and the resulting valleys were **six times deadlier** (6 dead of 24 against 1). Re-seeding each consumer through splitmix64's finaliser fixed it. *The sequences are independent only if the ids are well spread, and nothing in the type says so.*
- **⭐⭐ A WORLDGEN SHAPE CHANGE CAN BE DRAW-NEUTRAL, AND THE DIFFERENCE IS 27 RED TESTS AGAINST 9 (2026-09-10, D344).** Hashing the river's width from the column and a clump's outline from its centre takes **no draws**, so every seed keeps its founding site, soil and seams and only the shapes move. **Drawing for them instead reshuffles every valley — including the shipped one, which landed on ground where the village starves.** *Ask "can this be a hash instead of a draw?" before touching `MapGenerator`.*
- **⚠️ A DERIVED COUNT MUST FOLLOW THE SHAPE IT COUNTS (2026-09-10, D344).** `ClumpArea` returned the area of a Manhattan diamond and was used to decide **how many** clumps to drop. Changing the clump to a disc without it would have put a fifth more woodland in every valley — *a balance change hiding inside a worldgen change, which is what `PaintSeams`' own remarks warn about.*
- **⛔⛔ A GUARD NAMED "EVERY" THAT SAMPLES TWELVE FIXED CASES IS ASSERTING "THESE TWELVE" (2026-09-10, D344).** `EverySeedProducesAValleyAVillageSurvivesIn` was green for its whole life while **4 of 48 valleys die and 7 never grow** on the committed generator. *It was not wrong about the twelve; it was wrong about what twelve can tell you.* ⭐ It states a rate over a larger sample now — but the per-seed invariants that are genuinely absolute (a house nobody can walk to) stayed absolute.

- **⛔⛔⛔ AREA CANNOT SEE JAGGEDNESS — PERIMETER CAN (2026-09-10, D343).** Every `ZoneOutline` self-check measured **area**, and area was green for the whole time the smoothing was running at a quarter strength, because *a staircase encloses the same area as the curve through it.* ⭐ **Perimeter over that of the circle with the same area is scale-free and reads 1.00 smoothed, 1.07 stepped.** *When a complaint is about SHAPE, find a measure that changes with shape.*
- **⚠️ A TOLERANCE PICKED BY EYE CAN BE WIDER THAN THE DEFECT — SECOND TIME (2026-09-10, D343, after D334).** The new smoothness threshold was set to 1.10 by eye; **the bug measures 1.07, so the red check scored zero.** *Measure the broken state and the fixed state, then put the band between them.* It took two extra probe runs and it is the difference between a guard and a decoration.
- **⭐⭐ GIVE A ONE-SIDED METRIC A CONTROL THAT MUST NOT MOVE (2026-09-10, D343).** "Smoother is better" would pass a rounding rule that dissolved every square the player painted — which is the bug D334 exists for. **A 6-tile square reads 1.13 whether the smoothing is right or wrong**, so it is asserted two-sided. *A guard that can only fail in one direction is half a guard.*
- **⛔⛔ BEFORE "FIXING" SOMETHING THE PLAYER CANNOT ACCOUNT FOR, CHECK WHETHER THEY DECIDED IT (2026-09-10, D343).** Joe asked to delete a harvest blob that *"shouldn't even show up"*. **It was D127, his own call, and un-painting cleared ground had already been tried and rejected** — because with regrowth a bare painted tile is work waiting, not work finished. *§7 is searchable and one grep would have been cheaper than the reversal.* ⭐ The answer was presentation, not state.

- **⛔⛔⛔ A GUARD THAT COMPARES TWO PATHS SHARING A BUG TESTS CONSISTENCY, NOT CORRECTNESS (2026-09-10, D342).** *"An incremental re-bake must equal a full one"* is the right property and it **passed with the reach set to half what it needed**, because both bakes take the same flat-tile fast path and a shared error cancels. ⭐ *The arm that works compares the FAST path against the SLOW one* — bake with the optimisation disabled and require byte-identical. **It went red on its first run, twice, on two different real defects.** Ask what your two sides have in common before believing they agree.
- **⛔⛔ C# RUNS STATIC INITIALISERS IN DECLARATION ORDER, AND A DERIVED CONSTANT ABOVE ITS INPUT READS ZERO (2026-09-10, D342).** `TilesOfInfluence = ceil(Kernel + Warp)` sat above `WarpTiles` and came out as **1 instead of 2**. It printed itself in the probe and the guard of the day passed anyway. *No warning, no error, and the number looked plausible.*
- **⭐⭐ THE PROBE CANNOT DRAW, BUT IT CAN WRITE A FILE (2026-09-10, D342).** D340 measured that `VillageMap._Draw` never runs headless and concluded the view is unverifiable. **Half true.** The valley bake is saved as a PNG and read back — and **all three tunings of the terrain warp were a look at that image rather than a guess**, with two of them obvious failures that would have cost a play session each. *When a render has an intermediate you can serialise, serialise it.*
- **⚠️ A WARP OCTAVE'S WAVELENGTH MUST EXCEED THE NARROWEST FEATURE IT MAY TOUCH (2026-09-10, D342).** Warp finer than a feature is wide and the feature's two sides get independent displacements: **a two-tile river came apart into islands.** Warp far coarser and it is a pure translation that disguises nothing. *Both failures look like "the noise amount is wrong" and neither is.*
- **⚠️ HAND-SETTING A DERIVED NUMBER "FOR SAFETY" COST 35% OF THE BAKE (2026-09-10, D342).** `TilesOfInfluence = 3` changed **not one pixel** and took the bake from 1051 ms to 1415 ms, because the same constant is also the flatness radius and widening it shrinks the fast path. *If it is derivable, derive it — a conservative guess is still a guess and it has a price.*

- **⛔⛔⛔ A FIXTURE THAT ONLY BUILDS THE EASY CASE TESTS THE EASY CASE (2026-09-10, D341).** `EveryKindOfBuildingCanBeClickedOn` raises everything through `Mark(kind, GridPos)`, so **every building in it sits on a tile centre at facing zero** — where a building's `Origin` and its tile's centre are the same point and its rotation is identity. **Joe's actual bug, injected deliberately, left it green.** ⭐ *Free placement has existed for four commits and no test fixture had ever used it.* When a feature makes a new STATE possible, check whether any fixture produces that state before trusting a guard about it.
- **⚠️ WHEN A GEOMETRY RULE CHANGES, EVERY READER OF THAT GEOMETRY CHANGES WITH IT (2026-09-10, D341).** D339 taught the CLICK that a building is a rectangle and left the SELECTION OUTLINE drawing a tile square. **The half-fix was more visible than the original bug**, because it put both answers on screen at once. *Ask who else reads this, and answer it in the same commit.*
- **⚠️ A SPOT THAT FITS AN AXIS-ALIGNED BUILDING DOES NOT FIT THE SAME BUILDING TURNED (2026-09-10, D341).** Obvious written down; not obvious when a test helper called `SomewhereALonghouseFits` is sitting right there. *Ask `CanBuildAt` about the shape you are actually going to place.*

- **⛔⛔⛔ `VillageMap._Draw` NEVER RUNS UNDER `--headless`, SO THE PROBE CANNOT CHECK ANYTHING THAT IS DRAWN (2026-09-10, D340, measured).** The zone pass reports **zero rectangles** in a probe run. **Every view guard this project has is about layout, geometry or state** — widths, fold heights, tile centres, the outline seam, whether two defaults agree. *Whether a colour appears on screen is Joe's eyes and nothing else.* ⚠️ **Do not propose a rendering assertion without checking this first**; a red check that scores zero for this reason is not a weak guard, it is an impossible one.
- **⚠️ A VIEW TOGGLE IS WRITTEN DOWN TWICE AND NOTHING CONNECTED THE HALVES (2026-09-10, D340).** A field on `VillageMap` and a `ButtonPressed` on a `CheckBox`, wired one way only — so changing one default leaves the other **lying**, and the player has to click the box twice before it means anything. ⭐ `[widths] map toggles` compares all four now. *Whenever a fact has to be stated in two places, the cheapest guard in the world is the one that reads both and asks whether they match.*
- **⚠️ A FLAG THAT MEANS "THE PLAYER MOVED IT" MUST NOT BE SET BY THE CODE THAT MOVES IT (2026-09-10, D340).** Settings centres itself on open *unless dragged*, and both go through `MovePanel` — so setting the flag there would have marked the panel dragged the first time it opened and **the feature would have worked exactly once.** *Set intent at the gesture, not at the mechanism.*

- **⛔⛔ THE VIEW'S WIRING IS UNGUARDED AND A RED CHECK PROVED IT (2026-09-10, D339).** Deleting the building-under-the-point lookup from the click handler outright leaves **the suite green, the game building and the probe green.** The sim half has a real test; *the line that calls it has nothing.* ⭐ **This is D11/D160 with a number on it** — when a fix lives in `VillageMap._GuiInput`, the honest statement is "Joe playing it is the verification", not "tests pass".
- **⚠️ A RED CHECK THAT SCORES ZERO IS SOMETIMES TELLING YOU THE PROPERTY IS STRUCTURAL — AND SOMETIMES THAT NOBODY EVER POSED THE CASE (2026-09-10, D339).** Flipping `<=` to `<` in `Footprint` reddened **nothing**, for two reasons at once: `Covers(Point)` and `StandsOn` are one function so they cannot disagree (good), **and no test or building in the game is even-width and square to the grid**, which is the only shape that reaches the boundary (a hole). ⭐ *Ask which of the two it is before congratulating yourself.* A 2-wide building claims **three** tiles — pinned now.

- **⛔⛔⛔ A SELF-CHECK TESTS ITS OWN SIDE OF A SEAM AND SAYS NOTHING ABOUT THE SEAM (2026-09-10, D338).** `ZoneOutline.SelfCheck` was green while the riverbank was drawn **half a tile off the river** and the zone borders an eighth of a tile inside their own wash. It was right: the tracer closed its loops and kept their area throughout. **The bug was in what three call sites believed the tracer's UNITS were** — and the spec had argued explicitly that a shoreline check would be *"a second copy of the same assertion"*. ⭐ *When two pieces of code have to agree about a coordinate space, the guard goes ON the agreement:* the outline's bounding box must equal the rectangle the FILL draws for the same cell.
- **⚠️ MEASURE THE THING THAT IS ACTUALLY INVARIANT — SECOND TIME IN TWO COMMITS (2026-09-10, D338).** The new seam guard scored **24px on correct code** because it required every traced point to be a rectangle corner, and the tracer **cuts corners**: a lone cell comes back as a rounded octagon whose points sit *along* the edges. *D337's overhang guard failed the same way, measuring canopy centres instead of branches.* **A guard that is right that something is wrong and wrong about what is the most expensive kind of red**, because the obvious fix is to the wrong thing.
- **⛔⛔ A DERIVED SUMMARY IS NOT A SKIP TEST IF IT HAS A THRESHOLD IN IT (2026-09-10, D338).** `ZoneMap.IsResidential` / `IsHarvest` mean **"at least half the sixteen sub-tiles"** — the threshold the economy asks in. Using them to decide whether a tile is worth drawing would have **silently erased every tile under a quarter painted**, which is precisely the ragged edge sub-tiles were built for. ⭐ *The raw COUNT is the honest question; the threshold is a different question wearing the same words.*
- **⚠️ THE 3m0s SUITE READING FROM 2026-09-09 WAS THE MACHINE — RESOLVED, AND THE TRAP ABOVE IT CAN STAY AS WRITTEN.** It read **1m52s** the next day with the sim changed. *Recorded because the previous entry left the question open on purpose.*

- **⛔⛔⛔ A GUARD THAT SAMPLES ONE FRAME CANNOT SEE A BUG THAT NEEDS TWO (2026-09-09, D337).** The tree scatter's determinism guard computed the wood twice and compared — **both passes in one call, at one tick**, so a seed drifting frame to frame would have matched itself every time. ⭐ *The fix was not a better test: `CanopyOn` became a `static` taking only a tile and an index, so it CANNOT read the tick.* **When a guard scores zero, ask whether the property can be made impossible instead of asserted.**
- **⚠️ MEASURE THE THING THAT IS ACTUALLY OVER THE LINE (2026-09-09, D337).** The overhang guard reported *"nothing overhangs"* while measuring canopy **centres**. A canopy centred at 0.45 with a radius of 0.20 has already crossed the tile edge at 0.5. *The guard was right that something was wrong and wrong about what — which is the most expensive kind of red, because the obvious fix is to the wrong thing.*
- **⚠️ THE SUITE'S WALL-CLOCK MOVED 1m46s → 3m0s ON A VIEW-ONLY COMMIT, AND IT IS THE MACHINE (2026-09-09).** Reproducible across two runs — but **the only modified files were `Main.cs` and `VillageMap.cs`, and `Bclone.Game` is deliberately not in `bclone.sln` (D11), so the suite never compiles them.** ⭐ *Proving it was NOT the code took one `git status` and one fact about the solution.* ⚠️ **If a later session sees ~1m50s again, this was thermal or background load after a long session; if it stays at 3m, something real happened between D336 and then.**

- **⛔⛔⛔ `git checkout <file>` TO UNDO A RED CHECK DESTROYS THE UNCOMMITTED WORK IN THAT FILE (2026-09-09, D336).** I used it to revert a deliberate break and it took a guard I had written twenty minutes earlier with it — in the same file, uncommitted. ⭐ *The habit that works is the one used everywhere else in this session:* `cp file /tmp/x.bak` before the break, `cp /tmp/x.bak file` after. **Never reach for git to undo an edit in a file that has work in it.**
- **⚠️ AN INSTRUMENT THAT KEEPS A CONSTANT AFTER THE CONSTANT CHANGES MEANING MEASURES THE PAST (2026-09-09, D336).** The width probe posed the brush at `MaxRadius` — correct when radii were tiles, and a quarter of the real ceiling once they became quarter-tiles. **It was measuring a brush four times narrower than a spun wheel produces**, and nothing said so. *Fourth instance of the pose-the-wrong-state family: D242, D326, D332, now this.*
- **⚠️ A THROTTLE KEYED ON A COARSER GRID THAN THE THING IT THROTTLES BECOMES A LAG (2026-09-09, D336).** The hover recompute was gated on the cursor changing TILE. Once the brush moved in quarter-tiles that left the preview up to four steps behind the cursor — *a throttle is only a throttle while it is finer than what it is guarding.*

- **⭐⭐ A GOLDEN MOVE PROVES ITSELF FOR FREE IF YOU LAND THE SUBSTRATE FIRST (2026-09-09, D335).** D211's method is to move the goldens and then *reconstruct* the old mix to show the village did not change. **Better: land the storage change with the hash untouched and run the suite.** Green means the village provably did not change; only then move the mix. *There is nothing to reconstruct, because the "before" was a real run.* ⚠️ It only works when the two halves can be separated — but they usually can, and it is worth arranging that they are.
- **⚠️ "IT IS THE RENDERING, NOT THE RESOLUTION" WAS HALF RIGHT AND I SAID IT AS IF IT WERE WHOLE (2026-09-09, D333 → D335).** The square being drawn as a circle was smoothing. The round brush not looking round at five tiles across is **genuinely resolution** — a 5×5 grid holds a diamond, a bitten square or a square, and none is a circle. ⭐ *When a complaint has two causes, answering the one you can fix reads as answering both.*

- **⛔⛔⛔ A TOLERANCE CHOSEN BY EYE CAN BE EXACTLY THE SIZE OF THE BUG (2026-09-09, D334).** The
  guard on the smoothed square's area was set to 2%, and disabling the rule it guards costs the
  square **exactly 2%** — so the red check came back **green** with nothing to say. ⭐ *The quantity
  was right and the band was wrong.* The square is **exactly** 25 by construction when its corners
  are kept, so the band only ever needed to cover float noise. **Ask what the break actually costs
  before choosing the tolerance, not after.**
- **⚠️ TWO KINDS OF CORNER LIVE IN ONE OUTLINE AND THEY WANT OPPOSITE TREATMENT (2026-09-09, D334).**
  A **real corner** is two long runs meeting — the player painted it. A **staircase step** is a
  one-tile jog where a line was approximated by squares — nobody chose it. *Rounding both is what
  makes a deliberate square look apologetic;* the length of the adjacent runs is the whole test.

- **⛔⛔⛔ AN OVERDRAW THAT HIDES A SEAM IN OPAQUE PAINT DRAWS A GRID IN TRANSLUCENT PAINT
  (2026-09-09, D333).** Every per-tile wash drew its rect **2% oversized** — correct for terrain,
  where it stops sub-pixel gaps at fractional zoom — and **the 2% band where two translucent rects
  lap gets blended twice**: alpha 0.14 came out at 0.26 in a line at every tile boundary. **Joe saw
  a grid with the grid lines switched off.** ⭐ *Snap the rect to whole pixels instead: a tile's right
  edge and its neighbour's left edge are the same coordinate, so rounding them lands them on the
  same pixel — no overlap AND no gap.* ⚠️ Four passes had inherited the trick from the one that
  needed it.
- **⛔⛔ A PROPORTIONAL RULE CANNOT TELL A BIG SHAPE FROM A SMALL ONE (2026-09-09, D333).** Chaikin
  cuts a corner at **a quarter of the side**, and a 5×5 square straightens to exactly **four**
  vertices — so two passes make it a sixteen-sided figure. **A square, rounded proportionally, is a
  circle**, and Joe read it straight off the screen: *"that 'square' brush is the round brush."*
  Cap the cut by an absolute distance. *The same fraction is a rounded corner on a hundred-tile zone
  and a total rewrite of a five-tile one.*
- **⛔ A GUARD THAT CHECKS THE SHAPE CLOSED SAYS NOTHING ABOUT WHETHER IT IS STILL THE SHAPE
  (2026-09-09, D333).** D332's probe asserted every traced loop closed. **It did, throughout, while a
  square was being rendered as a circle.** ⭐ *Area is what tells them apart* — a circle inscribed in
  a 5×5 square is 19.6 against 25 — and the broken code measured **21.1**. **When you smooth
  something, guard the property smoothing can destroy, not the one it cannot.**

- **⛔⛔ A STATUS LINE EDITED AT ONE END CONTRADICTS ITSELF AT THE OTHER (2026-09-08, D332).**
  `specs/brush.md`'s header was updated to say both slices were built and its *tail still said B2 was
  not started* — shipped that way for one commit. **That is D159's exact failure, in the file that
  warns about it**, and it happened because the edit targeted the phrase that was wrong rather than
  the paragraph that was stale. ⭐ *A status line is rewritten whole or not at all.*

- **⛔⛔ TWO DIFFERENT THINGS WERE BOTH CALLED "THE GRID", AND ONLY ONE OF THEM WAS A DECISION
  (2026-09-08, D332).** Joe asked why a gridless game still looks gridded. **The sim being
  tile-indexed is his own closed call** (`gridless.md §10.2`); **the tiles being VISIBLE was nobody's
  call at all** — the grid lines had been drawn unconditionally since the first commit and had no
  switch. ⭐ *Before answering "that is by design", check whether the thing being complained about is
  the design or an artefact nobody ever chose.*
- **⚠️ CORNER-CUTTING A STAIRCASE GIVES YOU A RIPPLE, NOT A CURVE (2026-09-08, D332).** Chaikin on a
  polygon whose every side is half a tile long rounds each step and the result wobbles. **Merge the
  collinear runs first**, then cut: long edges stay straight and only the corners soften. *The
  smoothing step is not the step that makes it look smooth.*
- **⚠️ A RING IS THE SHAPE THAT TELLS YOU A TRACER IS WRONG (2026-09-08, D332).** A painted region
  with a hole in it has **two** borders. A tracer that returns one has swallowed the hole — and that
  failure looks like success, because the outer loop is perfect. *Every contour guard should include
  a shape with a hole.*

- **⛔⛔⛔ A CHECK THAT ITERATES A SET SAYS NOTHING ABOUT THE EMPTY SET (2026-09-08, D331).**
  `CanBuildAt` refuses a placement by looping over the tiles the building would cover and objecting
  to each — so when the footprint covered **nothing**, it objected to nothing and allowed a building
  that could never be selected or built. **The code reads as thorough**; the hole is the case where
  there is nothing to be thorough about. *Ask what your loop does over an empty collection.*
- **⚠️ A GEOMETRIC RULE CAN BE TOTAL ON A GRID AND PARTIAL OFF IT (2026-09-08, D331).** *"A building
  covers the tiles whose centres it stands on"* is exact, legible, and **guarantees nothing** — a
  unit square only reliably contains a lattice point while it is axis-aligned. The rule was correct
  for two years because the grid made the failing case unreachable. **When you remove a constraint,
  re-ask what the rules that lived under it were quietly relying on.**

- **⛔⛔⛔ AN INSTRUMENT THAT MEASURES "WHATEVER STATE THE THING IS IN" MEASURES THE DEFAULT — AND
  THE DEFAULT IS THE CASE THAT WORKS (2026-09-07, D330).** The width probe posed the placement
  sentence once, and snapping starts ON, so it measured the short one and reported 47px spare. The
  **free-placement** sentence was **1348 against the window's 1280 — it would have wrapped**, which
  grows the bar past the single line `PinTheBarHeight` reserves. It poses **both modes** now.
  ⭐ *Third instance of one rule: D242 (the bar measured young), D326 (the fold probe measured
  whatever state each panel was in), and now this. **Pose every state a control can be in, not the
  one it starts in.***

- **⛔⛔⛔ TWO DOCUMENTS DISAGREED ABOUT WHICH SLICE CAME NEXT, AND ONE OF THEM WAS THE SPEC
  (2026-09-07, D329).** `gridless.md §8` numbered slice 3 as *"villagers hold a `Point`"*; this file
  called free placement "slice 3". **Neither was right**: §7.3 had always said option C means
  buildings *"gain an Extent and a Facing **and are placed at a Point**"*, and 2b shipped the first
  two — `Point.cs` said so in as many words. **So the list said the next slice was villagers while
  half of the previous one was unbuilt.** *Renumbered as 2c. When §4 and §6 disagree it is a bug in
  the docs (CLAUDE.md) — so is a spec disagreeing with a handoff.*
- **⛔⛔ THE PERFORMANCE REGRESSION WAS CAUGHT BY THE CLOCK AND NOTHING ELSE (2026-09-07, D329).**
  The suite went **4m55s → 9m** and every test was green. A continuous origin widened
  `Footprint.CoveredTiles`' candidate scan by a tile in each direction, and `Covers` was answering a
  question about **one** tile by building the whole list and searching it — in the hottest path the
  sim has. Asked directly: **2m02s, faster than before the slice.** ⭐ *D179 from the other side:
  the suite's own duration is an instrument, and it is the only one that reported this.*
- **⚠️ A COMPILER-DRIVEN RENAME WILL PATCH THE LINE, NOT THE MEANING (2026-09-07, D329).** Driving
  `.Position` → `.Tile` off the error list rewrote **every** `.Position` on an erroring line, so
  `villager.Position` became `villager.Tile` wherever a villager and a building shared a line —
  which then failed differently and had to be walked back. *Fine as a way to enumerate the sites;
  never as a way to decide them.*

- **⛔⛔⛔ A PROBE CAN MEASURE THE RIGHT THING AT THE WRONG WIDTH, AND THEN IT PASSES EVERYTHING
  (2026-09-07, D327).** A new probe posed each placement sentence in `_placementLabel` and read
  `Size.Y` — **every one came back "18 tall, 1 line", including a 209-character one.** It was
  measuring at **120 pixels**, which is `WrappedTextMinWidth`: *a hidden label is never laid out,
  and `QueueSort` + `ForceUpdateTransform` does not reflow the container within the same call.*
  ⭐ **The tell was printing the width beside the verdict** — the answer was obviously impossible the
  moment both numbers were on one line. It measures through the font now, against the **window**
  rather than the bar (the bar is content-sized at **1657** against a 1280 window, so "fits the bar"
  can still mean running off the screen), and it is red-checked with a 300-character sentence.
  ⚠️ **It earned its keep immediately: the longest sentence sat at 1274 of 1280** and one added
  clause would have wrapped it. *Print the denominator, not only the numerator.*
- **⛔⛔ A FALL-THROUGH ARM CLAIMS EVERY CASE NOBODY WROTE A BRANCH FOR (2026-09-07, D327).**
  `Announce` tested `_groundFor`, `_moving`, `_emptying` and then `_brush` — **never
  `_harvestMode`** — so the harvest brush, a whole tab of the bar, announced *"Drag to paint where
  the village may build homes"* for its entire life. **The fields were correct; the reading of them
  was written as if residential were the only brush.** ⭐ *It survived because the wrong sentence was
  plausible enough over a map that nobody read it twice.*
- **⚠️ TWO COPIES OF A COMMENT SAYING "THESE MUST CHANGE TOGETHER" IS THE EVIDENCE THAT THEY WILL
  NOT (2026-09-07, D327).** The paint loop and the preview loop each carried the same pasted block
  warning that *"a preview that disagrees with the paint is worse than no preview"* — and one of the
  two had already gone stale, saying *"The diamond, not a square"* three lines above an inline
  comment saying **SQUARE, NOT A DIAMOND**. *A comment is not a mechanism. One function is.*
- **⛔⛔⛔ A RED CHECK THAT EDITS BY PATTERN CAN EDIT THE WRONG LINE, AND THEN GREEN MEANS NOTHING
  (2026-09-07, D326).** Reverting `StoreAt` to exact-position left its guard green and the guard
  looked blind. It was innocent: the `perl` substitution matched the **first** of two identical
  lines and had been quietly reverting a different method. **Verified by LINE NUMBER instead, it
  reddened instantly.** ⭐ *A red check is itself an experiment; confirm the break landed where you
  aimed it — `grep -c` the changed text — before believing a green.*
- **⛔⛔ A SAFETY PROVIDED TWICE IS A SAFETY NO TEST CAN HOLD YOU TO (2026-09-06, D318).** A comment
  called the `folded == 16384` branch *"the off-by-one the whole design turns on"*. Deleting it left
  **all twenty-seven guards green** — a separate `step == 0` fast path was covering for it, and only
  deleting **both** produced the fault. *Dead code presented as load-bearing.* The fast path is
  gone; the guard now reddens alone.
- **⭐⭐ A THING WITH THREE LIFECYCLE STATES NEEDS A GUARD ON ALL THREE (2026-09-07, D324).** Ghost,
  construction site, finished building. The ghost drew from the row and the finished branch was
  tested; **nobody asked what the site in between looked like**, and Joe found a longhouse drawn as
  one square in the default orientation for the years it took to build. **The untested state is the
  one somebody sees.**
- **⛔ A GUARD CAN STOP ONE CALL SHORT OF THE STATE THE PLAYER REACHES (2026-09-06, D321).** The
  multi-tile guard MARKED a longhouse and never RAISED one — and a marked building is a
  construction *site*, which is a `Workplace`, which already worked. It was **blind by a single
  `world.Complete(site)`** while the bug Joe hit — a finished store — went untouched.
- **⚠️ AN INSTRUMENT THAT ASSUMES A DEFAULT BREAKS WHEN THE DEFAULT MOVES (2026-09-07, D326).** The
  fold probe measured whatever state each panel was in. That was fine while every panel started
  open, and became a **false positive** the moment one started folded: its "open" height was already
  its folded height, so it duly "failed to shrink". It unfolds everything first now and restores
  the original states.
- **⛔⛔ ONE NUMBER ANSWERING TWO QUESTIONS WILL REPORT THE WRONG ONE (2026-09-07, D322).**
  `BuildersWanted` returns `anythingToBuild ? seats : 0` — zero for *nothing marked* and for
  *nowhere to build from* — and three consumers all said "nothing marked". **It cost a village.**
  *D182 said this exactly, about a headcount read as availability: a number that is true can still
  be evidence for the wrong claim. The fix is never to make the number lie less; it is to stop
  asking one number two questions.*
- **⚠️ AND THE REMEDY LINE CAN BE STRUCTURALLY DEAD.** The panel prints *"needs N, build another X"*
  when `Needed > seats`. For **builders only**, `Needed` came from the same seat-capped number, so
  it was `0 > 0` — false forever, for the one trade whose demand IS its seat count. **Every other
  trade got the sentence.** *When one row of a table never says what the others say, ask why.*
- **⛔ A COMMIT MESSAGE CAN OVERSTATE, AND THE NEXT SESSION WILL BELIEVE IT (2026-09-07, D325).**
  D321's message said the occupancy conversion was *"whole now"*. **Two of five collections had been
  converted.** Harmless while the other three were one tile, and false as written — caught only
  because Joe asked whether the treatment reached every building.
- **⚠️ HALF-CONVERTING A LOOKUP HAS NOW HAPPENED THREE TIMES.** `SomethingStandsAt`, then
  `FacingOfWhatStandsAt`, then twelve sibling methods. **It happens because every site owns its own
  loop.** One finder per collection, and every caller goes through it. ⭐ **And one of the twelve
  compared `.X`/`.Y` component-wise** — the same defect in a different spelling, **invisible to a
  grep for `Position ==`.**
- **⛔ A `return;` CAN CUT OFF THE PROBE'S OWN `GetTree().Quit()` — AND THE PROCESS RUNS FOR EVER.**
  A local function placed above the probe's tail left a headless Godot running. ⚠️ **`pkill -f
  Godot` DOES NOTHING ON WINDOWS**, and I did not check it, so it ran **18 hours**. Use
  `taskkill //PID n //F` and **then confirm with `tasklist`**. *A cleanup you did not verify is a
  cleanup that did not happen.*
- **⚠️ `perl -0777` NEEDS `\r?\n` IN THIS REPO.** Line endings are mixed — some files CRLF, some LF,
  occasionally within one file. A pattern with bare `\n` silently matches nothing, and
  `grep -c` on the replacement is the only way to know. The `Edit` tool fails outright on stale
  content, which is safer but slower.


- **⛔⛔ THE SESSION CONTAINER MAY HAVE NO TOOLCHAIN — AND CI IS A TOOLCHAIN (2026-09-06, D308).**
  No `dotnet`, no Godot, and `dotnet-install.sh` refused at the proxy;
  `builds.dotnet.microsoft.com` is denied by the environment's egress policy. **Three commits of
  view work were written without a single local compile, declared unverified in four documents, and
  Joe was asked to build them by hand.**
  - **⭐⭐ AND `ci.yml` HAD ALREADY DONE IT.** It runs `on: push: branches: ['**']`, and its last
    step is `Build the Godot view` — added precisely because *"a broken view sailed through a green
    CI"*. **The answer was sitting in a workflow log the whole time, and this session had read
    `ci.yml` earlier that hour to check the SDK version.** *Check the run before asking a human to
    be your compiler:* `actions_list` → `list_workflow_runs` filtered to the branch, then
    `get_job_logs`.
  - ⚠️ **Know what CI does NOT cover, or the correction becomes its own overclaim.** It installs no
    Godot by design (*"Bclone.Sim is deliberately engine-free"*), so **`BCLONE_PROBE_WIDTHS` and
    looking at the thing are still Joe's** — which is D11/D160's hole, unchanged.
  - ⛔ **A status line still must not read as verified past what was actually run.** D159's reason: a
    spec that lies about its own status is worse than no spec, because it is read at the moment a
    session is orienting. **"Compiles, unmeasured" is a different sentence from both "unproven" and
    "done", and it is the true one here.**

- **⛔⛔ A GUARD WITH A BUILT-IN'S ID TYPED INTO IT BREAKS THE DAY A BUILT-IN IS ADDED, AND THE
  FAILURE MESSAGE BLAMES THE WRONG THING (2026-08-29).** Adding `BuildingKind.TownHall = 11` turned
  **eight guards red across two files** and not one of them was testing anything that had changed.
  Three said `Id = 11` and failed as *"buildings[12] repeats id 11"* — **a true sentence about the
  fixture that says nothing about the code.** Five more were modded-catalogue fixtures that now
  omitted a built-in id.
  - **⭐ The cure is one line: derive it.** `NextFreeId(config)` reads the highest id in the
    catalogue and adds one, so the *next* built-in costs nobody an afternoon. **Read the numbers
    out of the fixture rather than writing them into it** — the same rule D231/D233 wrote for
    positions and quantities, applied to ids.
  - ⭐ **And the repair is worth more than the break was:** the reordering fixture now carries the
    new row **sixth of twelve in a descending list**, so position genuinely cannot pass for id
    (D218's finding, given a sharper fixture).
- **⛔⛔ A GOLDEN THAT *MUST* MOVE IS NOT A BUG — BUT PROVE IT IS THE FINGERPRINT AND NOT THE
  VILLAGE, AND DO IT THE D211 WAY (2026-08-29).** Slice 1's own Definition of Done said **"no
  golden moves"** and two moved. ⛔ **The DoD was wrong**, and it was corrected in place rather
  than quietly met: *a village whose founders have all died IS a different village* — it is owed a
  hall, and `Mark` reads that.
  - **⭐ THE MEASUREMENT THAT SETTLES IT TAKES TWO MINUTES: delete the new mixes from `StateHash`
    and re-run.** Both moved values came back **byte-identical to their old numbers**, which is
    what says the hash's *shape* moved and the village did not. *Assumption would have been
    indistinguishable from a real regression.*
  - ⭐ **And which goldens HELD is the result:** the shipped 50-year pair, the map golden, both
    farm goldens and all three per-site arms. **The two that moved were both *fixture* arms**,
    because the fixture village loses its founders by year 30 and the shipped one has not by
    year 50. *Ask why the ones that held, held.*
- **⚠️ `if (false)` IS NOT A DELIBERATE BREAK — IT DOES NOT COMPILE HERE (2026-08-29).** The trap
  further down says *"write breaks that compile — flip a bool, set a bound to zero"*, and the
  obvious way to disable a block trips `CS0162 unreachable code` against D246's
  `TreatWarningsAsErrors`. Same for dropping a clause from a condition: the now-unused local trips
  `CS0219`. **Delete the block outright, or add `_ = theLocal;`** — and back the file up first.
- **⛔⛔ THE WIDTH PROBE WAS STILL ONLY EVER MEASURING A YOUNG VILLAGE — INSIDE THE TOOL BUILT TO
  STOP THAT (2026-08-29).** D242's whole lesson is *"every look anybody takes at the UI is a look
  at a young village"*, and the bar probe measured the bar **as it starts**, with the conditional
  Knowledge and Civic groups hidden. It poses them visible now.
  - **⭐⭐ AND `wants` COULD NEVER HAVE SEEN THE PROBLEM ANYWAY: an `HFlowContainer`'s minimum
    width is its WIDEST SINGLE CHILD.** Adding a whole category moved `wants` by **exactly zero**.
    The number that decides wrapping is the **sum of the row's items**, which the probe now prints
    beside it. *That is D242's collapse-into-a-corner property read from the other side.*
  - **Measured: the Civic group costs 83px on a row that already wrapped (10 items/1261px →
    11 items/1344px against 1240 available), and the bar's height is unchanged at 189.**
    ⚠️ **That row is the one to watch** — it is the crowded one, and it is where the next category
    will land.
- **⛔⛔⛔ A GREEN RED-CHECK IS A CLAIM ABOUT YOUR FIXTURE BEFORE IT IS A CLAIM ABOUT THE CODE
  (2026-08-27).** The guard for D236 **passed against the live bug on its first run.** Posed with
  **firewood** — which the *market* also holds — a marketer's leg quietly rescued every load: 622
  in the market, none on the ground. **Logs are held by the shed and the pile and nothing else**,
  which was Joe's own case and leaves no third party to save it. Re-posed, it went red instantly:
  300 logs on the ground beside an empty pile.
  - **The rule: when a red check comes back green, interrogate the pose before you doubt the bug.**
    Ask *what else in this village could be quietly solving the problem for me?*
  - ⭐ **Corollary that paid twice more the same afternoon:** the stalled-site guard failed on its
    own pose (a fresh valley has no logs either, so the sentence correctly said *"40 logs"* when
    the test demanded *"stone"*), and the food-limit guard asserted **zero gathering over two
    years and measured 327** — *which was the feature working*, because stores fall back through
    the limit and foraging resumes. **Three fixture bugs, one code bug, in one session.**
- **⛔⛔ A GUARD FORBIDS WHAT IT ASSERTS, NOT WHAT IT IS FILED UNDER (D247, 2026-08-28).** Joe
  explicitly overruled `NoPublicApiLetsACallerAssignAVillagerToAWorkplace` so a villager could be
  pinned to a trade — **and it never forbade that.** It blocks a public method taking a `Villager`
  **and a `Workplace`**: naming a person into a *building*. A method taking a `Villager` and a
  `JobKind` passes untouched, and §2.2 survives whole. ⭐ **Read what a guard actually says before
  spending permission to break it** — the answer was better than the overrule.
  - ⚠️ **And the feature still needed FIVE mechanisms**, reading *0 of 4,311 ticks* through four of
    them. The last is the unobvious one: **a pin has to outrank COST in the candidate sort**, or
    displacing the incumbent just lets the cost sort hire him straight back for living nearer.
- **⛔⛔ A CONTROL THAT ACCEPTS INPUT IT CAN NEVER ACT ON IS A BUTTON YOU CANNOT PRESS, FROM THE
  OTHER SIDE (2026-08-29).** The professions panel let Joe ask for **six jobs against four able
  adults**, and three rows sat reading *"asked 1 · nobody working of 0 seats"* — numbers he had
  typed that could never come true. ⭐ **The distinction worth keeping:** asking for more foragers
  than the *village wants* is a real instruction the sim can honour later (D106, correctly
  ceiling-less); asking for more *people than exist* is arithmetic, not a preference.
- **⛔⛔ A COST THAT LOOKS SMALL ON THE AVERAGE VILLAGE IS A CLIFF ON THE ONE ALREADY STRETCHED
  (D250, 2026-08-28).** The rest spell took a farm **ten ticks from its store** from 88% of what it
  sowed down to **74%**, while the farm **beside** its store stayed at 95%. **A 120-tick autumn
  has no slack to give**, so the tax came straight out of the harvest — and D178 had spent a whole
  slice making that distant farm work.
  - **⭐ Measure the MARGINAL case, not the median one.** The average village absorbed this
    invisibly. If a change costs time, find the configuration that had none spare.
  - ⚠️ **And the dial was NOT monotonic**: `rest_ticks` of 2 cost that farm *more* than 3 did
    (80% against 86%). **One sample per value is not a curve** — say "best measured", not
    "optimum".
- **⛔⛔⛔ ASK THE COMPILER BEFORE YOU BELIEVE A GREP — AND CHECK THAT YOUR ENFORCEMENT IS ACTUALLY
  ENFORCING (D246, 2026-08-28).** `Directory.Build.props` has set `EnforceCodeStyleInBuild=true`
  since the first commit and **there was no `.editorconfig`**, so every `IDEnnnn` analyzer sat at
  `silent` and `TreatWarningsAsErrors=true` had nothing to promote. **A project that fails the
  build on warnings quietly accumulated dead code for a year.**
  - **⭐ A three-agent audit missed things one config file found in thirty seconds:** four unused
    parameters (they need dataflow, not search), a duplicate `RepoRoot` one line below the shared
    one, and 43 redundant usings against an estimate of 65. ⚠️ **The audit said outright it could
    not detect unused parameters. It was right, and the answer was to turn the rule on.**
  - ⚠️ **`src/Bclone.Game` is exempt** (`TreatWarningsAsErrors=false`, for Godot's generators), so
    it **reports and does not fail**. **Read its build output.** A write-only field warned CS0414
    there for months and nobody saw it.
  - ⭐ **And a SKIP is a claim about the world that nothing re-reads.** One of the two had a reason
    where *both halves* had expired; un-skipped, it passes. **Re-read skip reasons the way you
    re-read a status line.**
- **⛔⛔ A LAYOUT THAT IS CORRECT AT STARTUP AND WRONG LATER IS INVISIBLE TO EVERY CHECK ANYBODY
  MAKES (D242, 2026-08-27).** The bottom bar ran off **both** edges — *"Pause"* clipped to
  *"use"* — but only **after the village learned to write**, because the Knowledge group is
  hidden until then and the row grows by a whole category mid-play. **Every look anybody takes at
  the UI is a look at a young village.**
  - **⭐ The general rule: ask what this panel looks like in year forty, not year one.** Things
    that appear on a condition — the library button, a second granary's row, a modded building —
    are exactly the ones no screenshot will ever show you.
  - ⭐ **`HBoxContainer` has no graceful failure**: one line, and anything that does not fit
    leaves the screen. **Prefer `HFlowContainer` for any bar that can grow.** ⚠️ Flow containers
    read `h_separation`/`v_separation`; the plain `separation` an HBox uses is **silently
    ignored**, so a straight swap quietly loses all your spacing.
  - **⛔⛔⛔ AND SWAPPING THE CONTAINER ALONE MADE IT WORSE — JOE CAUGHT IT IN ONE LOOK:** *"i think
    you messed it up. its tall and wide on the right side."* **The bar is a `Floating(...)` panel
    with width 0, so it is sized BY ITS CONTENTS.** An `HBoxContainer`'s minimum width is the
    **sum** of its children, which is what had been holding the bar open (and then dragging it off
    the left edge — the original bug). A flow container's minimum width is its **widest single
    child**, so the panel collapsed to one button wide and wrapped everything into a tall column
    in the corner.
    - **⭐ THE RULE: a wrapping container cannot decide WHERE to wrap unless something else
      decides HOW WIDE it is. Flow containers consume width; they never create it.** The fix is
      `Floating(..., spanWidth: true)` — anchors pinned to both sides — plus
      `SizeFlagsHorizontal = ExpandFill` on the rows. **The two changes are one change, and
      shipping either alone is a different bug.**
    - ⚠️ **Every other floating panel is deliberately content-sized and hangs off one corner.**
      `spanWidth` exists for the control bar alone; do not spread it to the columns.
  - **⛔⛔⛔ AND THE WORST PART: I DECIDED THERE WAS NO GODOT ON THE MACHINE AND THERE WAS.** I
    searched `C:\`, found nothing, said so, and shipped **two** unverifiable UI guesses — the
    second of which Joe had to catch. **`run.bat` has named the path all along**, three lines
    from the top:
    ```bash
    export GODOT="/d/Projects/Godot/Godot_v4.7.1-stable_mono_win64/Godot_v4.7.1-stable_mono_win64.exe"
    BCLONE_PROBE_WIDTHS=1 "$GODOT" --headless --path src/Bclone.Game
    ```
    - **⭐ THE RULE: BEFORE CONCLUDING A TOOL IS MISSING, GREP THE REPO FOR ITS NAME.** This one
      is configured, documented and used by the script Joe runs every day. *"Not on `C:`" is not
      "not installed"* — and a wrong "I cannot verify this" is more expensive than a slow check,
      because everything downstream of it becomes a guess.
  - ⭐ **The probe measures the control bar now**, which it did not, and both bugs are one line
    each in its output. ⚠️ **`--resolution` is ignored and the numbers are always 1280 wide** —
    `stretch/mode="canvas_items"` lays the UI out at 1280 logical pixels and scales it, so
    **a row that wraps in the probe wraps on every monitor.** There is no "it will fit on a
    bigger screen".
  - ⚠️ **A taller bar silently ate the columns' clearance.** `ControlsReserve` is a *measured*
    constant at 160; the wrapped bar is **189**, so the columns ran underneath it — **the wrap
    fix created the exact bug that constant exists to prevent.** It reads the bar's real height
    now, which is safe because the dependency runs one way: the bar's height depends on the
    window and its own contents, never on the columns.
- **⛔⛔ THE AUDIT TRAIL FINDS WHAT READING THE CODE DOES NOT — AND NOBODY HAD MINED IT LIKE THIS
  (2026-08-27).** D236 sat in `StoreForTheLoad` for months, read past by several sessions. What
  found it was arithmetic on the log:
  ```bash
  # every state transition, most common first
  grep -oE "DEBUG behavior [A-Za-z]+ #[0-9]+: [a-z ]+ -> [a-z ]+" "$L" \
    | sed -E 's/[A-Za-z]+ #[0-9]+: //' | sort | uniq -c | sort -rn | head -20
  # any activity, bucketed by year (480 ticks/year)
  grep -oE "^\[t +[0-9]+\].*-> clearing painted ground" "$L" | grep -oE "[0-9]+" \
    | awk '{printf "%d\n", ($1/480)+1}' | sort -n | uniq -c
  ```
  - **⭐ The tell was a state transition with no matching `goods` line** — villagers "fetching a
    load" thousands of times who never picked anything up. **Cross-reference the two streams**:
    an action with no consequence is a loop.
  - ⚠️ **And check what a suspicious line actually means before reporting it.** `carrying -13 food`
    looks like a catastrophe and is a **delta** (`+40 … now 360`, `-280 … now` empty). *Nearly
    filed as a bug.*


- **⛔⛔⛔ A SIM FEATURE IS NOT DONE UNTIL SOMETHING IN THE VIEW CALLS IT — SEVEN INSTANCES NOW, THREE
  OF THEM IN ONE WEEK.** The library was built, tested, red-checked and **invisible** (no draw call,
  no inspector row, no demolish path). Then **Move and Empty shipped with no buttons at all.** Every
  one had passing guards. **No test in this suite can catch it, and Joe finds it in ten minutes of
  play, every time.**
  - **The rule: write the button in the SAME COMMIT as the feature**, and if you cannot, say in the
    handoff that the feature is unreachable. *"Placeable is not reachable" was written down after
    the library and the next two features shipped unreachable anyway.*
- **⛔⛔ WHEN A CHANGE MAKES AN ACTION REVERSIBLE, GO BACK AND DELETE THE CONFIRMATION IT USED TO
  NEED (D235).** D228 made unpainting *level* a house, so a second-stroke confirmation was correct.
  **D230 made unpainting only MARK one, with repainting cancelling it — and the gate survived one
  commit past its reason.** To Joe it read as *"it wouldn't let me unpaint the land."*
  - **Friction that outlives its justification is indistinguishable from a bug**, and the two
    commits were both right on their own. *Ask what a safety is protecting against after every
    change to the thing it guards.*
- **⛔⛔ `git checkout -- <file>` DESTROYED UNCOMMITTED WORK AGAIN (D232), IN THE SESSION THAT
  RE-READ D194's WARNING ABOUT IT.** A red-check break failed to compile and I reverted the file
  instead of restoring from the scratchpad — losing three methods. ⚠️ **I had backed up ONE of the
  two files I was about to touch**, which is the exact half-measure the trap warns about.
  - **⭐ AND THE CHEAPER LESSON: a break that does not compile is not a red check, it is an edit to
    undo.** Write breaks that compile — flip a bool, set a bound to zero — and the temptation to
    reach for `git checkout` never arrives.
- **⛔ `grep -c` RETURNS EXIT 1 WHEN THE COUNT IS ZERO, so `grep -c "â" file && git commit` silently
  skips the commit.** Cost one confusing "why did that not land?" It is the encoding check this
  project runs constantly — **put it after the commit, or terminate it with `|| true`.**
- **⚠️ AN INSTRUMENT THAT ASSUMES A SIMPLER WORLD MEASURES SOMETHING ELSE — three times in one
  stretch.** A guard placed its granary by scanning from the map's *corner*, so nobody walked to it
  and *"emptied after three years"* measured the distance (D231). A guard assumed the founders' cart
  was empty and asserted on "40" against a wagon holding 200 (D233). A guard painted a block around
  the founding site that **re-painted the very tile it had just erased**, so the family rebuilt in
  the spot the test had turned them out of (D228).
  - **Read the numbers out of the fixture rather than writing them into it**, and when a guard needs
    people to walk somewhere, put the building where a player would.
- **⭐⭐ AND ONE TICK IS NOT A TREND, ANY MORE THAN ONE SEED IS (D227).** `ApprenticeshipTests` read
  *masters alive at exactly tick N* and a change turned one seed from 8→10 into **8→8**, which
  looked like Phase 3's pillar dying. **Two hypotheses died to a probe** — saturation, then
  *apprenticeship never fired* — before the answer: it is a **spot reading of a fluctuating stock**.
  Averaged over twenty years the same seed has the **widest margin of the three**. *The guard got
  stronger and the fallback plan was not needed.*
- **⛔⛔⛔ `perl -0777 -pi -e` WITH A WIDE CHARACTER IN THE REPLACEMENT DOUBLE-ENCODES THE WHOLE FILE
  (2026-08-26).** This handoff recommends `perl -0777` *because* of the repo's emoji — and that is
  exactly how it bites. Perl reads the file as **latin-1 bytes**; if the replacement string contains
  any code point above 255 (a literal ⭐, or a `\x{2b50}` escape), perl upgrades the entire output
  string and **re-encodes every byte in the file as UTF-8 a second time.** `Construction.cs` came
  back with `â` where every `—` had been, top to bottom.
  - **The tell is a one-line warning you will scroll past: `Wide character in print at -e line 1`.**
    Nothing fails. The build still succeeds. The damage is in 400 lines you did not touch.
  - **The rule: perl is fine for ASCII-only substitutions — a rename, a type change, deleting a
    line. The moment the replacement text contains an emoji or a dash, use Edit/Write instead.**
  - **And back up before you find out**: the recovery was `cp` to the scratchpad, then
    `git checkout --` on a file whose only uncommitted changes were two edits worth redoing. *That
    is the good case.*
- **⭐⭐⭐ A BREAK THAT REDDENS *NOTHING* IS THE MOST VALUABLE RESULT A RED CHECK CAN GIVE, AND IT
  HAPPENED AGAIN (D222).** Renaming the granary in the catalogue — **the word in the
  village log, in the placement sentence and on the panel** — turned **zero** tests red across 786.
  **D108 spent a decision fixing exactly those words** (*"the default arm called every unrecognised
  building a woodcutter's hut, in the log, in the panel, and in every placement sentence"*) **and
  nothing has ever guarded them.**
  - **⭐ The cure is a PAIR of guards, and the pairing is the point:** one proves the catalogue holds
    the word, one proves the code that writes the sentence *uses* it. D108's bug was a naming path
    ignoring the right answer, not a wrong answer stored somewhere — **a guard on the data alone
    would have been green through the original bug.**
  - **Ask, of any slice: which of these strings does the player actually read, and does anything
    check that they arrive?** Fourth in the family after D56, D177, D187 and D194.
- **⛔⛔ AND YOUR OWN SPEC IS A HYPOTHESIS TOO (D222).** `buildings-catalog.md §2.1` said, in bold,
  that `JobRow.WorksAt` **must** stop being an enum or the slice closes nothing. **Changing it
  reddened six `ModdedJobTests` in one run**: their JSON reads `"works_at": "GathererHut"`, a word.
  The enum was an *alias for the first N ids* all along — `ModdedGoodTests` had been casting
  `(Goods)6` since D210 — so what was missing was never the type, only a catalogue to resolve
  against. **The wrong version would also have made every row read `"works_at": 7`.**
  - *Written between reading the code and writing it, and wrong by the time the tests ran. **A spec
    sentence with "must" in it is the one to check first**, not the one to trust.*
- **⛔⛔ COUNT THE GOLDEN *VALUES*, NOT THE FAILING *TESTS* (2026-08-25).** Five tests reddened and
  I re-took five numbers. **`FarmGoldenTests` asserts two** — a full state hash and a
  skills-ignoring one — so fixing the first merely let the test reach the second, and the suite
  came back red again for what looked like the same failure.
  - **The rule: `grep "private const ulong"` across the affected files before replacing anything**,
    and expect parameterised arms (`[InlineData(...)]`) to hold values too.
  - **⭐ And pair each value to its arm by running the tests SEPARATELY.** Reading four `Actual:`
    lines out of one interleaved log and matching them to four arms by eye is how you write the
    fixture's hash into the shipped slot. *"Check every guard red, and count the reds" — counting
    the tests is not counting the reds.*
  - **⛔⛔ AND THE `grep "private const ulong"` RULE IS NOT ENOUGH — IT MISSED ONE ON ME (D223).**
    That grep found five goldens; **`SkillTests` holds its two as bare `InlineData` literals with
    no `const` anywhere**, so the grep never saw them and the enumeration was wrong before the
    first value was replaced. *The line above already said "expect parameterised arms to hold
    values too" — it was read, and still under-applied, because a rule that names one grep invites
    you to run that grep and stop.*
    - **⭐ THE RELIABLE ENUMERATION GREPS FOR THE LITERALS, NOT FOR THE DECLARATION:**
      `grep -rn "[0-9]\{15,\}" tests/Bclone.Sim.Tests/*.cs`. A golden is a 19-to-20-digit number
      however it is spelled — `const`, `InlineData`, or an argument. **It also turns up the history
      comments, which is a feature: those are where you write the old value.**
  - **⭐⭐ AND THE GOLDENS THAT *DO NOT* MOVE ARE THE RESULT, NOT THE LEFTOVERS (D223).** Bringing
    the fixture to `food_per_meal: 4` moved four values and held four — and **every held one runs
    the SHIPPED config** (`ShippedFiftyYearHash`, `SkillTests`' true arm, `GoldenMapHash`, all
    three `PerSiteYieldTests` arms). *That is what proves a fixture change stayed inside the
    fixture. Check it deliberately with a `git diff` on those lines; do not just notice they were
    green.*
- **⭐⭐⭐ A COMMENT THAT IS TRUE OF ONE PATH IS THE HARDEST BUG IN THIS REPO TO SEE (D220).**
  `RegrowthSystem` said *"a sapling seen by a sweep is a sapling that has stood for one period,
  because the sweep visits every tile exactly once per period."* **Perfectly true — for saplings
  the sweep seeded itself.** A forester plants at an arbitrary tick, so the next visit might be the
  very next one, and planted trees matured **three times faster** than seeded ones for as long as
  that comment stood.
  - **It read so plainly that nobody thought to test it**, which is what makes this class worse
    than a wrong comment: a wrong one invites checking. **D200 found the same shape** in
    `LabourSystem`'s *"never moves someone who already has a job."*
  - **The tell: a sentence that explains WHY it is true.** *"…because the sweep visits every tile
    once per period"* is a proof sketch, and a proof sketch names its assumptions. **Ask which
    paths satisfy them.**
- **⛔⛔ FINISH THE MERGE, OR SAY OUT LOUD THAT IT IS NOT MERGED (2026-08-25).** Eleven commits of
  finished, green work sat on a branch in a worktree while `main` had none of it. **Joe played
  `main`, saw *"villagers harvest stone but the pile shows 0 stone"*, and filed a bug that was
  entirely real on his build and entirely fixed on the branch he was not running** (D217). The
  session that had done the work spent its reply diagnosing a build rather than a village.
  - **⚠️ AND THE MERGE WAS THEN LEFT HALF-DONE** — two unresolved conflicts, nothing committed,
    `git log` still showing an older tip. A fresh session asked to *"review what's in"* had to
    discover that **nothing was in** before it could review anything.
  - **The rule: a branch is not done when the tests pass, it is done when it is on `main`.** If it
    cannot be merged yet, **the handoff must say so in the first paragraph** — because the person
    playing the game has no way to tell which build they are on.
  - ⭐ **When two sessions have both edited the docs, expect conflicts and expect BOTH sides to be
    true.** All three here were documentation where each session had updated the same line about
    its own half; the resolution was *and*, never *either*. **Take both, then check the arithmetic:**
    761 + 11 = 772 is what proved neither side's guards were dropped.

- **⛔⛔⛔ `git checkout -- <file>` DESTROYS UNCOMMITTED WORK AND THERE IS NO UNDO — I DID IT TO
  MYSELF (D194).** Mid-slice, wanting to revert *one deliberate break* in `SimWorld.cs`, I ran
  `git checkout --` on it and **reverted the entire slice's uncommitted implementation.** A
  backup taken minutes earlier saved it. Later in the same session I deleted two untracked test
  files with `rm` while splitting a commit, **had no backup of those**, and had to rewrite both
  from scratch.
  - **The rule: before reverting or deleting anything you have not committed, copy it to the
    scratchpad first — every file, not just the ones you think are involved.** A deliberate break
    for a red check is exactly when this bites, because you are *trying* to throw work away and
    it is easy to throw away more than you meant.
  - **⭐ And prefer `perl -0777 -pi -e` to revert a break**, since it undoes precisely what it
    did. `git checkout` cannot tell your break from your feature.
- **⛔⛔ `dotnet test --filter FullyQualifiedName~Foo` MATCHES THE CLASS NAME, NOT THE FILE (D198).**
  Breaking the harvest brush's mode filter appeared to turn **nothing** red, and I nearly recorded
  a coverage hole that does not exist — the guard lives in class `HarvestBrushModeTests` **inside
  `SeamsTests.cs`**, and `~SeamsTests` never ran it. **It reddens three times.** *A surprising
  green is a claim about your filter before it is a claim about the code.*
- **⭐⭐⭐ WRITE THE GUARD FOR A CLAIM THE DOCS MAKE, AND YOU MAY FIND THE CLAIM WAS ALREADY FALSE
  (D200).** `LabourSystem` had said for phases that its slack pass *"never moves someone who
  already has a job."* **It does** — `ShedSurplus` releases somebody and `Match` re-places them in
  the same pass, **67–83 times over fifty years at the cadence that sentence was written for.**
  The behaviour was right and the sentence was wrong. *A long-standing comment is a hypothesis
  nobody has tested.*
- **⚠️ ONE SEED IS NOT A TREND, AND I READ ONE AS A TREND (D200).** Firewood fell 156 → 131 → 91
  as a cadence quickened and I called it a real cost. **Across three seeds it goes down, up, and
  down-then-up.** It was noise. *A spot reading of a fluctuating stock is not a trend — and this
  nearly became the reason not to ship a change.*
- **⚠️ CHANGE A TIMING AND FIXTURES BREAK THAT ARE NOT REGRESSIONS (D200).** One config key moved
  and **three guards went red, none of them a bug**: a life-log guard matched the bare word
  *"foraged"* and flagged the **mastery line** (*"has foraged these woods for 18 years"* — a
  statement about a life, not about this winter); an at-risk guard killed the two masters it had
  posed and **had not noticed the village grows its own**, 15–19 a century; and it picked the
  first frail villager rather than the one with most life left, so **a warning that stopped
  because the person died read exactly like a warning that stopped working.** *Ask what a fixture
  quietly depends on before calling its red a regression.*
- **⭐⭐ THE BUG IS OFTEN A NUMBER RATHER THAN A MECHANISM (D197).** The market restock leg looked
  wrong — distribution effort rose 24–79%. **The mechanism was fine; the target was
  `market_stock_per_household × economy_horizon_households` = 800**, so a village of five homes
  needing forty apiece had a marketer hauling stock for twenty households. *Before rewriting a
  mechanism, check what number it is aiming at.*
- **⭐⭐ CHECK EVERY GUARD RED, AND COUNT THE REDS.** Repeatedly this has caught a guard that
  proved nothing. **And the guard that catches a bug is often not the obvious one** — *"the
  farm's sentence says farmer"* passes against a generic template with the farm's name in it;
  the one that works reads both sentences with the names masked out and requires them to differ.
- **⭐⭐ A SPEC CAN ASK FOR A GUARD THAT CANNOT EXIST, AND THE DoD IS WHERE IT HIDES (D181).**
  `skills-catalog.md §11.2.1` required *"provable no-op: goldens unmoved"* for a slice whose
  entire content is **new hashed state that grows from tick one.** It was reasoned by analogy
  from a slice where the analogy held, it sat in a Definition of Done for a week, and it would
  have been "met" by quietly not hashing proficiency — which would have cost the determinism
  guarantee and moved the goldens twice later instead of once. **Ask what a DoD item would look
  like if it were satisfied *before* you try to satisfy it.** The fix was to restate the claim in
  a vocabulary that can be true (*nothing anybody DOES changed*), not to weaken the guard.
- **⭐⭐⭐ AND THE BREAK THAT TURNS UP *NOTHING* IS THE ONE THAT CHANGES THE DESIGN (D194).** Two
  drafts of the farm's memory had it commit `learned + 1` a year and latch once a tile rotted.
  **Deleting both turned no guard red** — settled memory and tiles reaped identical at all three
  distances. The mechanism was redundant because `HarvestOneFarmCanBringIn` multiplies by the
  hands standing in the field *at that moment*, so **a farm with two hands in spring and one by
  autumn already over-commits on its own.** *The village probes without being asked.* The probe
  was **deleted rather than guarded** — a fifth invisible no-op after D56, D177 and D187. **Zero
  reds is a result, not a formality passed.**
- **⭐⭐ BREAKING YOUR OWN GUARDS FINDS THE BLIND ONES — DO IT, AND EXPECT A SURPRISE (D181).**
  Nine reds across seven deliberate breaks, and break #2 turned a guard red **for a reason
  unrelated to what it tested**: `LeavingATradeStopsTheClockOnItThatTick` sampled on a year edge,
  so *"the number did not move"* was two effects cancelling — no growth, and no decay only
  because the floor happened to protect a first-year worker. **The red check is not a formality;
  it is the only thing that reads your fixture for you.**
- **⚠️ THE VILLAGE IS BRIEFLY JOBLESS ON THE YEAR EDGE, AND IT WILL BAFFLE YOU (D181).** At
  *Day 1, Spring* the reshuffle has torn every allocation down and not yet rebuilt it: **0 of 4
  able adults hold a job on that exact tick.** Any guard that samples "who is working?" at
  `TicksPerYear * n` is sampling that hole. **Step half a season in.** (Winter is the other one:
  D44 unstaffs seasonal trades, so mid-winter is 1 of 4.)
- **⭐⭐⭐ AND SOME STATE CANNOT BE POSED AT ALL, BECAUSE IT IS DERIVED — TWO REDS TO FIND (D195).**
  An elder cannot be posed. Writing `LifeStage` lasts **one tick** (`AgeingSystem` recomputes it
  from vigour); writing `AgeYears` lasts **one tick** (`ClockSystem` recomputes it as
  `year - BirthYear`) — the guard **watched a 51-year-old turn 21** between the first tick and the
  second and read the resulting silence as a broken feature. `BirthYear` is `init`-only, which was
  the model saying so all along. **The honest fixture steps the sim until somebody genuinely grows
  old**, and it is barely slower. *Before posing a value, ask whether anything recomputes it.*
- **⭐⭐ AND A *FIXTURE* CAN FIGHT THE MECHANISM IT IS TESTING (D194).** Three guards for the
  farm's memory posed *"a clean autumn"* as **one sown tile** — so the farm brought in one tile,
  correctly recorded that one tile was what it had managed, and **the guards failed for the
  feature working.** The memory is a high-water mark, so a posed field *smaller* than the
  building's own commitment is a **worse** year, not an easier one. **Ask what your pose means to
  the system, not just what it means to you.**
- **⭐⭐ A GUARD CAN BE GREEN AND BLIND.** `AFarmBringsInMostOfWhatItSows` reports 93% while the
  played village was at 46%, and it is not wrong — it sites its farm a step from the stores.
  **Unmoved because it does not cover the case** (D157, three times now). Ask what a guard's
  fixture *makes impossible* before trusting its number.
- **⭐⭐ THE INSTRUMENT IS AS LIKELY TO BE WRONG AS THE CODE.** In one session: a probe reported a
  farm reaping 60 of 60 tiles because its *"reaped"* column counted winter rot as harvest, and a
  guard was written claiming an untested happy path when the guard for it was ten lines above.
- **⭐⭐ THE SIM'S AUDIT TRAIL IS EVIDENCE ABOUT THE SIM AND SAYS NOTHING ABOUT THE VIEW.** Two
  sessions hunted a rendering bug in `BehaviorSystem`. **Ask which half the symptom lives in
  before opening the log.**
- **⭐⭐⭐ THE INSTRUMENT WAS WRONG TWICE IN ONE SESSION, AND BOTH TIMES IT NEARLY CHANGED A LOCKED
  NUMBER (D189).** *"Gathering brings in five times what farming does"* came from a probe counting
  food into the **farm's own store** — which a reaper hauling to the granary never touches.
  **Counting reaps instead flipped the answer to "farming wins by 28%".** The wrong number would
  have justified raising `crop_yield_per_tile`, which is derived and locked. **Before a
  measurement justifies a change, ask what the instrument cannot see.**
- **⭐⭐ A DERIVATION THAT AVOIDS STATING A NUMBER STILL STATES ONE (D192).** The thaw rate was
  *derived* by mirroring the outdoor rate, on the explicit grounds that mirroring *"needs no
  number of its own"* — true, and it quietly chose **fifteen days to thaw**, half a winter, which
  nobody noticed until Joe played it. **Check what a derivation came out as, not just that it is
  principled.**
- **⭐⭐ A SMALL-RANGE RNG DRAW AT A FIXED STRIDE CORRELATES, AND THE FOUNDING IS FOUR SUCH DRAWS
  (D190).** Both founding pairs drew the **same** personal rhythm — 1, 1, 2, 2 — so the fix for
  D28 did nothing. **The RNG is not at fault:** forty raw `NextInt(0, 4)` draws come out 9/11/8/12.
  It is the *stride* at the start of the stream. **A generator can be sound and still be the wrong
  tool for four draws that must differ from each other** — deal or rotate, do not draw.
- **⚠️ HUNGER IS A PURE FUNCTION OF TICKS SINCE THE LAST MEAL (D190).** Two villagers who eat on
  the same tick stay in step for ever, **however differently they walk** — so a stagger that
  offsets only movement leaves *identical hunger at 100%*. Anything meant to desynchronise people
  has to touch the hunger clock too.
- **⭐ FINDING A CAUSE IS NOT FINDING THE CAUSE** (D163, D166, D169 — three rounds on one symptom).
  - **⛔⛔ AND THE FOURTH ROUND PUT TWO WRONG CAUSES INTO DOCUMENTS BEFORE THE RIGHT ONE (D182).**
    *Why does a forager take 32 calendar years to reach 20 years on the task?* **Wrong once:**
    *"winter stands the work down"* — the evidence was **1 of 4 able adults hold a job in
    mid-winter**, a **headcount**, read as **availability**. Foraging is worked in all four
    seasons; there are just fewer people on it. **A number that is true can still be evidence
    for the wrong claim.** **Wrong twice:** *"derive each trade's mastery from the share of a
    year it is staffed"* — that measures **demand**, which is the player's business, and would
    have pinned woodcutting at five years because this village wants one occasionally.
    **Right:** decay, taking **37% of everything a career earns.**
  - **⭐ The thing that caught both was building the measurement needed to ACT on the claim.**
    The first survived a probe because the probe answered a different question; the second died
    the moment its own numbers were printed next to what they implied. **If a finding is about to
    become a config number, measure the number — not the story.**
- **⭐ THE HELPER YOU NEED MAY ALREADY EXIST.** `Main.Wrapped` had been doing exactly the right
  thing on five labels for two UI rebuilds while every sentence in the inspector went into a bare
  `Label` in an `HBox`. Grep before writing.
- **⚠️ IF A NUMBER GOES INTO A DOCUMENT, IT COMES FROM A RUN.** Four for four, the fourth being a
  handoff's own warning about it.
- **⚠️ CHECK A DOCUMENT'S REFERENCES AGAINST THE THING.** Every file said "PR #3" for a day.
- **⭐⭐ MEASURE THE TOOLING TOO, NOT JUST THE VILLAGE (D179).** The suite ran nineteen minutes and
  the obvious fix — tag the long acceptance runs as slow — was **wrong**: it was already 9.5×
  parallel, so throughput was never the cost. The real culprit was an **O(n²) Dijkstra nobody had
  ever timed**, four seconds a world. **It is 2m30s now.** *The thing everybody suspects is not
  the thing costing the time — and Joe had to say "measure it first" to stop the wrong fix.*
  - **⭐ AND IT APPLIES TO A GUARD YOU JUST WROTE (D198).** A new sweep built **a fresh world per
    tile** — 28 seconds an arm, **2.8 minutes for one file**. One world was enough *and was a
    truer test*, since the preview and the paint are then asked of the same world in the same
    state. **0.8 seconds now, over nine times as much ground.** *If a new guard is slow, the
    fixture is usually doing something the claim never needed.*
- **⚠️ A FULL RUN IS FOR A VERDICT, NOT FOR DISCOVERY.** One slice here burned four full runs,
  twice to learn what was already knowable. **Use `--filter` while iterating.**
- **⛔⛔ DO NOT WRITE A WAIT-LOOP FOR A RUN THAT IS ALREADY IN THE BACKGROUND.** It is redundant —
  **the completion notification arrives by itself** — and it adds a failure mode that has now
  cost this project two sessions.
  - **2026-08-16:** two shells spun **thirteen hours** waiting for `Passed!` against a file
    written with `--logger "console;verbosity=detailed"`, which ends `Test Run Successful.`
    instead. *The two output formats end with different strings.*
  - **2026-08-22:** two more spun **an hour and three quarters** waiting for `Passed!` against a
    file that was the output of `dotnet test | grep … | head -30` — **the summary line had been
    filtered out before it ever reached the file.** *Grepping a file for a line you already
    grepped away.*
  - **The rule: a wait-loop whose condition cannot be met is a vacuous guard that costs wall
    time instead of passing silently.** If you truly must poll, poll for something the file is
    *guaranteed* to contain — and prefer just waiting for the notification.
- **⛔⭐ AND DO NOT REWRITE THIS TRAP LIST FROM SCRATCH — CARRY IT FORWARD.** The warning directly
  above was written on 2026-08-16 by the session that lost thirteen hours to it. **I deleted it
  on 2026-08-22 while tidying the handoff after the Phase 2 merge, and walked into the identical
  trap ninety minutes later.** *A handoff rewritten wholesale silently drops exactly the
  hard-won warnings it exists to carry* — which is D159's drift running the other way: the
  document losing knowledge the code never had. **Edit this file; do not replace it.**
- **⭐ AND WHEN A SPEC AND A MEASUREMENT DISAGREE, THE SPEC IS THE ONE THAT IS WRONG.** D178 wrote
  a soil algorithm into a spec, probed it, and found it made the number it existed to raise
  *worse* — and separately inferred the founding ground was "already ordinary" from a fact about
  draw order that turned out to imply the opposite. **Both were caught by ten-minute probes.**
- **`python` is not on PATH**, and string edits die on this repo's CRLF and its emoji. Use
  `perl -0777`, or the Edit/Write tools for anything with quoting in it.
- **⚠️ Goldens go last, one commit, one stated reason** (D152). The seam golden moves when a
  village that farms changes; the two fifty-year goldens do not, because **neither village ever
  places a farmhouse** — silent about what they do not reach, loud about what they do.

---
