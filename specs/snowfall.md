# Spec: Snowfalls — snow you can see falling, lying on the land as it falls

**Status:** ✍️ **SPECCED AND 🔨 BUILT on `art` (A76, 2026-11-14)** at Joe's *"Build it"* on a mock; **reworked at his first
look in his own game (A77, the same day)** — the flakes fall into the valley, are drawn crystals, and the snow lies by the
land's shape; **✅ MERGED into `main` at his word (A78, *"Yes, merge it"*, merge `77a9ee5`); not pushed.** Art lane, view only. Neighbours: `terrain-shader.md` (the snow's first build, A4, A9, A23, A41 — §5.3's
*"falling snow, not in this slice"* is this), `green-ground.md` (the land the drifts read), `painted-paths.md` (the tracks
the snow lies thin on).

---

## 1. Why

Joe: *"can we work on snow next? i hate the way it 'shows up' is that a systems thing? should there be some kind of
non-vision obscuring precipitation?"*

**It is not a systems thing:** the sim has no snow and no weather. The snow was the calendar drawn: one amount ramping 0 → 1
over autumn's last 10 days (40 ticks, about 50 s at 1×), whole through winter's first 20, gone over its last 10, laid where it
passed a fixed noise threshold — 3-tile patches with a hard edge. Asked what bothered him, he named all four:

- **blotches popping in** (hard-edged white patches spreading like mould);
- **nothing explains it** (no snow falls);
- **too sudden**;
- **it ignores the land** (the same blobs every year, blind to the hills, paths and buildings).

## 2. Joe's calls

| # | The question | His answer |
|---|---|---|
| 1 | When should snow fall? Snowfalls (a few a year) / only while it lays / gently all winter | ***"Snowfalls"*** (recommended) |
| 2 | Rain too? | ***"Snow first, rain later"*** (recommended): rain is its own later piece on the same layer |
| 3 | The mock (`D:\Projects\brushes\snow\snow_options.jpg`, `snowfall.gif`): today against the proposal on his Hollowmere | ***"Build it"*** |
| 4 | A76 in his own game (A77) | ***"its snowing for the player, not the village"*** (the flakes slid down the screen); ***"apply our existing parchment/watercolor style to the snowflakes and design them and vary them somewhat"*** (with a plate of crystal drawings); ***"the snow on the map should 'contour' the map heights/depths in a way that shows it settling in a 'natural' way"*** |

⛔ **Stays out:** anything that hides the village (§1.1, legibility); weather in the sim; the trees' winter look (still the
calendar's crossfade, A41 — a later piece if it reads out of step with the falls).

## 3. What it draws (with the watercolour tick on)

### 3.1 When it snows

**Three or four snowfalls a year**, between autumn's day 18 and winter's day 20, each two to four days long, one in each
equal slot of that window. Each fall whitens the ground by its own share; the shares make the winter whole by the window's
end. **Between falls the ground holds.** **The thaw** runs from winter's day 22 and is over by winter's end, so spring and the
founding are never snowy. The days, lengths and shares are a hash of the world's seed and the year: every year differs, and a
replayed save snows on the same days. A pure function of the tick; the sim is untouched.

### 3.2 How it lies — settling by the land's shape (A77)

- **A frost first:** an even veil over the whole valley (whole by 0.45 of the winter's snow, at 0.22–0.32 strength), so the
  first fall dusts everything rather than dropping blobs.
- **Then drifts, read off the land** — each place's threshold:
  - **the snow line (A77):** lower on the valley's high ground and higher on its low ground (`SnowLand` x, across the green
    ground's height span), so the heights whiten first, the snow's edge follows the land's contours down toward the river,
    and the thaw draws it back uphill;
  - lower in a hollow (the green ground's crest channel below zero), higher on a crest;
  - lower on a slope turned from the light, higher on a lit one;
  - **higher on a steep bank (A77):** the banks shed it (`SnowLand` y), so the hachures and lips read through;
  - the year's patch noise only a small part now (0.12, from 0.35), shifted each year.
- **The drift's edge is painted (A77):** a few pixels wide on its contour at every zoom (`SnowLand` z, w), feathered into the
  paper's fibres as the pen's edge is, over A76's soft bleed; and **the ground's wash pools just outside it** (`SnowRelief`
  z) — the pigment gathered at a wash's edge as it dried — so where the snow stops is drawn as a line along the land.
- **The land's relief on the snow (A77):** a slope turned from the light washes toward the snow's cool shadow colour with its
  tilt (`SnowRelief` x, at A41's `SnowShadowStrength`), and the brush along the contours shows through the snow (`SnowRelief`
  y). ⛔ A mix toward a colour, never a grey (A67). **A41's cool lee is replaced:** it was read off the patch noise, so it lay
  as cloud-shaped blotches unrelated to the land; the same colour and strength now come from the land.
- **The peak** stays A26's *patchy* (`SnowPeak` 0.75): at mid-winter the lowest ground by the river and the sunniest crests can
  still show.
- **Kept:** the tracks lie thinner (A72), the river's winter rim and darkening (by the snow lying), the fields whiten with the
  ground (by the snow lying, not the calendar).

### 3.3 What falls — into the valley, not down the screen (A77)

The map is seen straight down (A31), so **a flake falls away from the eye**, not down the screen (A76's flakes slid down
it: *"snowing for the player, not the village"*). A see-through layer over the map, shown only while a fall is falling.

- **A flake's life:** born high, it falls (5 s for a crystal, 3.5 s for a speck, on the flakes' clock), then settles (1 s),
  fading into the snow lying, and is drawn anew somewhere else in its cell. High, it is drawn larger (×1.6) and softer, and
  **its shadow lies on the ground 1.3 tiles toward the lower right** — the valley's own light comes from the upper left. As
  it falls it shrinks and sharpens, drifts a little on the wind (0.1 tiles a second, eastward), turns slowly, and **its shadow
  closes in under it**; where they meet it has landed. The shadow is a soft cool blot, faint and wide while the flake is high,
  firmer as it lands.
- **Crystals, painted as the map is painted:** sparse (at most 30 % of 2.4-tile cells), six-armed, in **six designs after the
  old crystal plates Joe sent** — stellar, fern (three pairs of branches), tipped (a plate at each arm's end), sectored plate,
  plate and star, broad-branched — each jittered in its arm width, branches and plates. A paper-white body with the paper's
  tooth, a cool wash pooled at its edge and a faint bloom of it round the crystal, a thin cool ink line round it and along its
  inner ridges and rings that swells and lifts as the bank's pen does. Arm 0.3 tiles, held between 5 and 12 px on screen; under
  5 px's worth a tile (about 17 px a tile) the crystals fade out and only specks fall.
- **Specks between them:** many fine soft dots (at most 45 % of 0.9-tile cells, radius 1–1.6 px), white with the wash's cool
  rim, each with its own small shadow.
- **World-anchored:** they move with the map when it pans. **Their clock** runs at the game's speed but at most twice real
  time, so at 10–20× the flakes never streak (a fall then lasts about a second); paused, they hold still. Each life goes a
  whole number of times into the clock's hour-long wrap, so nothing jumps there. Faded out when the valley is small on screen
  (under 8 px a tile).

## 4. How

- **The schedule:** `VillageMap.SnowAt(tick, ticksPerSeason, seed, look)` → `(Falling, Lying)` and `SnowFallsOf(year, seed,
  look)`, in `VillageMap.Snowfall.cs`; splitmix64's finaliser over the seed, the year and a salt. Its numbers in the look
  file (`SnowFallsPerYear`, `SnowFallWindow`, `SnowFallSeasons`, `SnowThaw`, in seasons into the year).
- **The ground:** `snow_amount` is now the snow lying; `valley_terrain.gdshader`'s snow block computes the frost and the
  drift threshold (`snow_dust`, `snow_drift`, `snow_drift_gain`, `snow_year`). `terrain_lab.gdshaderinc` is untouched, so the
  Grass Lab is as it was. `TheYearAt` still drives the grass's season and the trees' crossfade; its snow number is the lab's
  and the probe's `watercolour:` year check, no longer the valley's.
- **The flakes:** `shaders/snowfall.gdshader` on a full-rect `Control` over the map, built with the watercolour layer;
  uniforms each frame from the valley's turn of `_Draw` (how hard, the zoom, where the land is, the clock). Visible only
  with the tick on and snow falling, so it costs nothing otherwise. Its clock steps in `TickTheFlowClock`.
- **Values:** `looks/valley_watercolour.tres` (`Snow*`, `Snowflake*`, `SnowfallPaceCap`). First values, for Joe's eyes.
  The flakes' values reach the layer from one list, `VillageMap.FlakeValues`, which the probe asks too (A77).

## 5. Limits

- **Legibility:** flakes small, sparse and see-through; never over the interface; gone at the widest zooms.
- **Cost:** the flake layer only while falling; the ground's change is a few noise reads. Measured paired at 4K.
- **Determinism:** nothing in the sim reads it; no time but the view's clock, no unseeded random.

## 6. What proves it

- The probe's `snowfall:` line: over 12 years and two seeds, three or four falls a year; nothing before autumn's day 18 or
  falling after winter's day 20; the ground never thinner before the thaw and still between falls; whole as the thaw starts,
  clear by the year's end; every year's schedule its own; the same tick the same answer. The flakes' layer shows mid-fall with
  the tick on, not with nothing falling, not with the tick off, and is hidden the moment the tick goes off; the lying and the
  falling handed over; every flake uniform declared by the compiled shader; the look's values on both layers (A77: every
  `FlakeValues` entry, and `snow_land`, `snow_relief`); each flake's life a whole number of times into the clock's wrap.
- Watercolour off byte-identical at four zooms against the build before.
- Renders of his Hollowmere through a winter beside the mock he picked, and a snowfall in motion.
- Red-check: every guard, the reds counted, the zeros written down.

## 7. As built

**A76, 2026-11-14.** New: `VillageMap.Snowfall.cs` (the schedule, the flakes' layer and clock, the hand-over, the probe's
`snowfall:` line), `shaders/snowfall.gdshader`. Changed: `valley_terrain.gdshader` (the snow block: frost and drifts, four
uniforms), `VillageMap.Watercolour.cs` (the snow handed over from the schedule; the fields by the snow lying; the flakes'
clock stepped and their layer hidden with the tick; the uniform list), `ValleyWatercolourLook.cs` with
`looks/valley_watercolour.tres` (16 values), `Main.cs` (the probe's print). Nothing in `Bclone.Sim`, `tests/` or `data/`.

**As the mock, with one difference:** the flakes' clock is capped at twice real time (the mock had no game speed).

**Verified.** Tick off byte-identical to the build before at four zooms, spring and winter, on his Hollowmere (readout
masked). Renders through a winter beside the mock, and a snowfall in motion (`D:\Projects\brushes\snow\snow_built.jpg`,
`snowfall_built.gif`). Probe 118 ✅ 0 ⛔, `snowfall:` ✅, bar 151, tile centres and new game ✅, `done.`. View build 0 warnings.
**Four checks:** suite 2024 passed / 0 failed / 5 skipped in 17m42; view build 0 warnings; the probe as above; goldens 418
lines, `src/Bclone.Sim`, `data` and `tests` identical to `main` (no golden moved).

**Cost** (RTX 3070, 4K SubViewport, his Hollowmere winter, paired): the ground +0.2–0.5 ms against the build before; the
flakes while falling +0.1–0.5 ms at the opening zoom, nothing measurable zoomed out (they fade there).

**Red-check:** 17 mutants, **15 red**. Two guards strengthened on the way: the shares not summing to one first scored zero
(the clamp hid it — every fall must now add snow), and a mutant that changed nothing (falling past a fall's end, already
zero) was replaced by one that does. **Two zeros, draw-time:** the fields whitening on the calendar, the drifts blind to the
land; the renders are their evidence.

**Not done, and known:**
- The trees' winter look still turns on the calendar (A41), so they can go bare before a late first fall whitens the ground.
- Rain: his *"later"*, on the same layer.
- Every `Snow*` value is a first value, for his eyes.

**A77, 2026-11-14 — reworked at his first look** (§2 call 4). Changed: `shaders/snowfall.gdshader` rewritten (the flakes'
lives, their shadows, the six crystal designs painted in ink and wash, the specks); `valley_terrain.gdshader` (the snow line,
the banks shedding, the painted drift edge and its pool, the land's relief on the snow; A41's noise-read lee removed; two
uniforms); `VillageMap.Snowfall.cs` (`FlakeValues`, one list for the hand-over and the probe; the life check);
`VillageMap.Watercolour.cs` (the uniform list); `ValleyWatercolourLook.cs` with `looks/valley_watercolour.tres` (18
`Snowflake*` values for A76's 8; `SnowLand`, `SnowRelief`; `SnowDrift`, `SnowDust`, `SnowShadowStrength` retuned). Nothing in
`Bclone.Sim`, `tests/` or `data/`.

**Verified.** Probe 118 ✅ 0 ⛔, `snowfall:` ✅, bar 151, tile centres and new game ✅, `done.`; view build 0 warnings. Tick off
byte-identical to A76 at four zooms, spring and winter. Renders on his Hollowmere (`D:\Projects\brushes\snow\snow_settles.jpg`,
`crystals_close.jpg`, `crystals_falling.gif`). Suite 2024 / 0 / 5 in 17m08; goldens 418 lines; sim, data, tests identical to
`main`. **Red-check:** 11 mutants, 8 red of 8 guards; three draw-time zeros written down (no snow line, the shadow never
leaving its flake, every crystal one design) — the renders are their evidence. **Cost** (4K, paired): the flakes while falling
about +1 ms at the opening zoom, +0.4 ms closer (A76's were 0.1–0.5); nothing while none falls.
