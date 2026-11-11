# Spec: The river drawn — echo lines, a darker channel, ripples that drift downstream

**Status:** ✍️ **SPECCED (A44, 2026-11-11) — JOE'S *"The river drawn"*; ✅ HIS FOUR CALLS ANSWERED, ALL AS RECOMMENDED (A45,
*"All as recommended"*); 🔨 BUILT (A45, §9): THE TABLE, THE CHANNEL, THE ECHO LINES, THE RIPPLES, THE CLOCK, THE *moving water*
TICK. 🔁 CALL 1 REVERSED AT HIS WORD (A46, *"Keep the game's speed"*, after a side-by-side trial): THE RIVER NOW RUNS AT THE
GAME'S SPEED. ⏸️ HIS EYES.** Art lane,
branch `art`. It is `terrain-shader.md §7` piece 3, and A26's brief is what it is judged against: *"Water drawn, not
filled — inked bank, two echo lines, a darker channel, flow strokes drifting downstream."* The inked bank is built
(A27–A28); this is the rest. Owner: Joe + Claude Code.

---

## 1. What this is, and what it is not

Joe, A26: *"i would like the game to animate this in some way to show flowing water."* And his call 2 there: **the water
flows only while time runs — pause looks paused.**

Today, with the watercolour tick on, the river is a flat painted blue with lighter shallows along each bank and A28's pen
stroke on the bank. In the concept (`art/art-direction-2d-hand-drawn-color.jpg`) the water is drawn: short broken pen
marks just inside each bank that follow its turns, a few loose ripple arcs out in the stream, and the blue deeper down the
middle than at the edges. This spec draws those three things and sets the ripples moving downstream while the game runs.

- **Behind the watercolour tick**, like every §7 piece. With the tick off the map is byte for byte what it is today.
- **Rendering only.** `git diff main -- src/Bclone.Sim data tests` stays empty; no golden can move.
- **Not in this spec:** relief (§7 piece 5, though §3's table is built so it can reuse it); calmer ground (piece 2);
  fish, boats, the fishing hut's water; a frozen river — **the river never freezes** (`fishing.md §10.3`), so the
  ripples run all year and stay off A41's icy rim.

## 2. What the sim says, and must keep saying

- **The river is its tiles.** `Terrain.Water` tiles, cut once by `MapGenerator.CarveRiver` along one of four courses:
  west to east, north to south, north-west to south-east, south-west to north-east (`river_course`, the new-game
  screen's *River runs*). **Each names which way is downstream**, so the drawing never has to guess.
- **No call to `SetTerrain` makes or unmakes water** (checked by grep, 2026-11-11): the river does not change in play.
  The drawing of it is built once, when the valley's mix is built, and never per frame (CLAUDE.md's rule).
- ⛔ **The bank stays where the villagers cannot cross** (D342). Nothing here moves the bank's contour or A28's stroke on it.
  Everything new lies inside the water.

## 3. The river's own coordinates — one small table, built once

To lay marks *along* a river that wanders, the shader needs to know, at any pixel, how far down the river it is and how
far from its middle. A carved river has one run of water per step along its course (that is how it is carved), so this is
a short table, **one row per step down the course**:

| column | what it is |
|---|---|
| centre | the middle of that step's water, across the course, in tiles (smoothed over ±4 steps, so a wander is a bend, not a stair) |
| half-width | half that step's water, in tiles (smoothed the same way; a diagonal's measured across, as the carve does) |
| distance down | how far the river has run to this step along its smoothed middle, in tiles |

- **Built by a pure function of the map's water tiles and the course** (`MapGenerator.CourseOf`, D477), so the same
  valley always gives the same table and the probe can feed it made-up rivers. A few hundred rows, a 1-D float texture
  of a few kilobytes beside the mix.
- **The shader reads one row a pixel**, only where water is drawn: it turns the pixel's tile into *down* (the distance
  down plus how far along the step) and *across* (from the centre, in tiles). Every mark below is drawn in those two
  numbers, so it bends with the river and never shears.
- **Not hashed, not saved** (D335): it restates the map.
- ⭐ **Relief can reuse it.** Piece 5 asks for *"height above the river"*; on land that is how far a tile is from the
  table's bank (`|across| − half-width`), so piece 5 does not need a second river field.

## 4. What is drawn

All in A28's ink (`#3a2d20`), over the wash, over the snow, under the paper's grain, the way the bank's stroke is. Every
value below is a first value in `looks/valley_watercolour.tres`, for Joe's eyes, and **nothing is approved until he says
so.** Each part is off at 0 strength, and with all three at 0 the river draws byte for byte as it does today.

### 4.1 Echo lines — two, still

The concept's broken marks just inside each bank. Two lines that follow the bank, **0.15 and 0.32 tiles** inside it,
drawn with the bank's own stroke (`valley_ink_stroke`: pressure, lifts, feathered edge) but **thinner (0.55 of the bank's
width) and lighter (0.55 strength)**, and **broken far more often**: the pen lifts over about half of each line, so they
read as dashes, not a second and third outline. Each line has its own noise, so their breaks do not line up.

- **Measured from the same raw weight as the bank and A41's icy rim** (the pixel's distance into the water). ⚠️ That
  weight's slope runs out somewhere inside the bank (HANDOFF art trap 22). The build measures how far in it holds. If 0.32
  tiles is past that, the echo lines take their distance from §3's table instead.
- **They hold still.** They are the bank's echo, not the current (call 3).
- **They fade out when zoomed out**, wherever an echo would lie closer than 3 screen pixels to the bank, so at the widest
  zoom the bank stays one clean stroke rather than a smudge three lines thick.

### 4.2 The darker channel

The blue deepens toward the middle of the river: the wash is multiplied darker by **0.12 at the middle**, easing to
nothing at the bank (`1 − |across| / half-width`, eased). This sits on top of the shallows that are already drawn. Those
change within half a tile of the bank, while the channel changes across the whole width, so in a wide reach the middle
reads as deep water.

- In winter, A41's 20 % darkening is applied on top, as it is today.
- At the widest zoom it is kept: it is a wash, so it reads at any size.

### 4.3 Ripples — the moving part

Short pen marks out in the stream, lying along the current, each one a slight arc like the concept's. They drift
downstream.

- **Where:** laid on a grid in the river's own coordinates, cells **1.6 tiles down × 0.6 across**. About **45 % of
  cells** hold a ripple (call 2), placed at random in its cell. Each ripple is **0.5–0.9 tiles long**, **1.1 screen px**
  thick at its middle, tapering to nothing at both ends, and bowed 0.06 tiles to one side or the other. Strength **0.5**.
- **They keep 0.35 tiles off each bank**, which puts them inside the echo lines and off the winter ice. So a river under
  about a tile across has echo lines but no ripples.
- **They drift downstream at 0.35 tiles a second**, about a tile every three seconds (call 1). The whole grid moves with
  the water.
- **They come and go.** Each ripple lives about **6 seconds**: it fades in over the first third and out over the last.
  It comes back drawn anew (a new place in its cell, a new length, a new bow). So the river never shows a conveyor belt
  of the same marks, and a ripple never pops on or off.
- **They fade out when zoomed out**, below about **12 px a tile**, where a ripple would be a few pixels long and read as
  noise. Zoomed out, the river is the bank, its echoes and the channel.

### 4.4 The clock

- **The ripples run on their own clock**, `flow_seconds`, kept by the map and handed to the shader each frame. It
  **advances only while the game is running**: paused, every ripple holds exactly where it is (A26 call 2). It is a
  *view* clock and never touches the sim, so determinism is untouched.
- ~~**By call 1 it advances at the same rate at every game speed**: at 1× and at 20× the water moves the same gentle
  amount. The other answer ties it to game time, so at 20× the ripples race.~~ *(void — call 1 reversed, A46.)*
- 🔁 **A46 — it keeps the game's speed** (Joe: *"Keep the game's speed"*, after seeing both as a trial tick): a real
  second gives the game's speed multiplier in flow seconds — one at 1×, twenty at 20×. `Main`'s refresh hands the map
  the speed every frame (`SetFlowPace`), and a pause still stops it by `Paused`. ⚠️ At 10× and 20× each ripple lives a
  fraction of a second (6 s ÷ the speed) and drifts 3.5–7 tiles a second, so it flickers more than it flows; Joe chose
  it having seen that. If it ever reads wrong, the middle way is a pace that follows the speed but tops out at 2–3×.
- **It wraps without a jump.** The pattern repeats along the river every few hundred tiles and over about twenty minutes,
  and the clock wraps with that period, so a long sitting never loses a float's precision. Wrapped this way, a frame
  after the wrap is the frame before it plus one step, which the probe checks.
- **At founding it is 0**, so a render paused at tick 0 (HANDOFF art trap 21) is the same picture in any build, ripples
  included. This is what lets the renders below compare byte for byte with the river on.

### 4.5 A Settings tick (call 4)

*Moving water*, on by default, beside *draw the trees*. With it off, the ripples are drawn but hold still, as if paused.
It is for a player who finds motion at the edge of their eye a distraction. One line in `Main.cs`'s view ticks, as the
trees' tick was (A36).

## 5. Cost

**One table read and one cell's worth of hashing per river pixel**, only where water is drawn; no extra frames (the map
already redraws every frame — the game has no low-processor mode). Measured in a 4K SubViewport, alternating rounds,
A/B against today in one sitting (traps 8, 21, 22). ⚠️ **§7's bar is the thing to watch:** A28 measured the watercolour
switch close to A23's 3.5 ms at 4K and said *"the cost split (§5.4 piece 3) comes before the river and winter add
more."* Winter added nothing measurable (A41). **If the river takes the switch over 3.5 ms, the cost split is built
first and the river waits on it.**

## 6. Joe's calls (A44), in plain words, each with a recommendation

✅ **Answered (A45): *"All as recommended"*** — ~~the same drift at every game speed~~ *(call 1 reversed, A46: the river
keeps the game's speed)*; sparse; the echo lines still; a
*moving water* tick, on by default.

1. **How fast the water moves.**
   - **(a) The same gentle drift at every game speed, about a tile every three seconds** *(recommended)*. At 20× the
     village rushes but the river does not. Water that speeds up with the clock reads as a clock, not as water.
   - (b) Faster with the game speed: at 20× the ripples race downstream.
2. **How busy the river is.**
   - **(a) Sparse, like the concept: a ripple every couple of tiles, the blue showing between them** *(recommended)*.
   - (b) Busy: ripples all over, about twice as many.
3. **The echo lines just inside the banks.**
   - **(a) They hold still; only the ripples out in the stream move** *(recommended)*. They are the bank drawn again,
     and a bank that slides reads as the land moving.
   - (b) They drift downstream too.
4. **A tick to stop the water moving.**
   - **(a) Yes: *Moving water* in Settings beside *draw the trees*, on by default** *(recommended)*. Steady motion at the
     edge of the eye bothers some players. With it off the river is still drawn, just still.
   - (b) No tick.

**Settled without asking:** the ripples run all year, because the river never freezes; zoomed out, the ripples fade and
the echo lines merge into the bank; the table is built once, from the tiles; everything new is behind the watercolour
tick and off at 0.

## 7. How it is proven

- **Tick off:** renders byte-identical to the build before, at four distinct zooms (art trap 25: the game opens at its
  closest, so check that the shots differ from each other).
- **Tick on, the three new strengths at 0:** byte-identical to the build before at the same four zooms, so the river's
  new code changes nothing until it is asked to.
- **Tick on, at the file's values:** the renders differ only on water pixels (the diff's mask lies inside the bake's
  water, within 1 px), and the river's land side is untouched, which proves the bank has not moved.
- **The probe, a `river drawn:` line:**
  - the table, from the founding valley: one row a step; every water tile inside its row's span; the centre and
    half-width within half a tile of the tiles before smoothing; the distance down only ever growing.
  - the table, from four made-up rivers, one a course: downstream is the course's way each time, named in the probe
    (trap 17), not read from the code's list.
  - the clock: still while paused; the same rate at 1× and at 20× (call 1a); a wrap that does not jump.
  - the new uniforms: declared by the compiled shader (trap 6), and the look file's values reaching the layer.
- **Red-check every guard and count the reds** (D326), each mutant scored on the line its guard writes (trap 28).
- **GPU** as in §5. **The four checks** for the commit.
- **Joe's eyes:** `.\run.bat`, tick the watercolour, watch the river at 1× and 20×, then pause.

## 8. Pieces

**One piece**: the table, the three marks and the clock together, because none of it reads as a river without the rest.
Built after Joe's four calls, then it stops for his eyes.

## 9. As built (A45, 2026-11-11)

**Where it lives.** `RiverTable.cs` (the table: `RiverTable.Of`, a pure function of the water tiles and the course, and
`ToTexture`, one RGBA-float texel a row); `VillageMap.Watercolour.cs` (`HandTheRiverOver`, once a world;
`TickTheFlowClock`; `BankWeightAt`; the probe's `river drawn:` line); `valley_terrain.gdshader` (`valley_pen`, the bank's
stroke made a function of its width, pressure, lifts and salt; `valley_ripples`; the channel, the echoes, the ripples);
eighteen values in `looks/valley_watercolour.tres`; the *moving water* tick in `Main.cs` beside *draw the trees*.

**Two things changed from §3–§4 on the way, both found by measuring:**
- ⚠️ **The ripples keep off the bank by the water's own weight, not by the table.** The table's middle is smoothed over
  nine steps, and at the founding valley's sharpest wander it rounds the bend **1.11 tiles** outside that step's water.
  Measured from the table, a ripple there could reach the bank. So the ripples keep to water weighed above what a
  straight bank weighs **0.35 tiles** in (`ripple_bank_level`, 0.93 by `BankWeightAt`). That follows the drawn bank step
  for step. The table now only steers them and darkens the channel. The probe holds the middle within **1.5 tiles** of its
  step's water (not §7's half a tile, which was written before the smoothing was measured).
- **The echo lines are contours of the raw water weight at the weight a straight bank has at their depths:** **0.675**
  at 0.15 tiles and **0.886** at 0.32. `BankWeightAt` reproduces the bake's kernel and averages along the bank. The weight
  still slopes there; it reaches 1 at about 0.42 tiles. Along a real bank the kernel's 3 × 3 sampling makes a contour
  wander about ±0.04 tiles a tile. That wander is the line's hand-drawn wobble.

**Measured, 2026-11-11.**

| | |
|---|---|
| tick off | byte-identical to the build before (`442ff69`) at four distinct zooms |
| tick on, the three strengths at 0 | byte-identical to the build before at the same four zooms — which also proves `valley_pen` draws the bank's stroke unchanged |
| tick on, the file's values | every changed pixel is water or the bank's own pen stroke (at most 4 levels on the stroke, where the drawn water share fades); the bank has not moved |
| the probe | `river drawn:` ✅ — the founding valley (`we`, 120 rows wet) one run a row, the middle within 1.11 tiles; four made-up rivers downstream east, south, south-east and north-east; the clock; the values. 105 ✅ 0 ⛔ |
| the motion | running, the ripples move **east** (downstream for this valley) about 0.28–0.29 tiles a second across the screen. The river's own length bends, so that is under the 0.35 down it. Paused through the game's own Space key, **0 pixels** change outside the debug readout, against 2,293 a second while running |
| GPU, 4K, the 3070, three states a round in order | the river **+0.04 ms** (the clean floors: 1.790 → 1.827 ms); the whole switch about 1.83 ms against the 3.5 ms bar, so the cost split is not needed. ⚠️ A noisy sitting: another project's Godot was rendering on the card, and mid-run the card fell into its twice-slower state (art trap 22), so only rounds paired in order were read, by their floors |
| red-check | **15 mutants, 13 red** (each scored on the line its guard writes: 14 on `river drawn:`, the shader's on `watercolour:`). **Two zeros, kept and written down:** R14, the founding valley's water read with its rows shifted — the probe reads the water through the same `TheWaterTiles` as the code, so it is blind to that reader; R15, no *moving water* tick in Settings — a Settings tick has no probe line, and it is Joe's eyes. The harness's first run hung: R1's off-by-one row threw out of the probe line, which then never reached `done.` and held a headless Godot for 15 minutes. The line now catches a table that throws and says what threw (HANDOFF trap 29) |
