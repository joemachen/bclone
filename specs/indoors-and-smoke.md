# People going indoors, and chimney smoke

> **Status: ✍️ specced and mocked (A138, 2026-11-20); ✅ his five calls answered — *"1. Smoke B please. 2. okay 3. okay 4. instant 5. permission granted"* — and 🔨 BUILT on `art`
> (A139, §7); ✅ MERGED INTO `main` at his word (A140, *"Looks right, merge"*, `976212f`); not pushed. ✅ **Errands into a store go in too (A141, §7a), merged into `main` at his word (A144, `31a6682`); not pushed.** Claimed on `art` (A137).
> Joe, from play, 2026-11-20: *"if they dont already, villagers should go inside buildings not just appear on them (like
> when they are cold)"* and *"buildings burning firewood should have smoke plums coming from their chimnies as long as
> they are burning firewood"*. His order, A137: these two first. **View only** — it reads the sim and writes nothing.

## 1. What he sees today (read off the code, and his Ravenscar)

- **A person reaching a house vanishes over its roof, and comes out of it the same way.** The sim walks them to a tile the
  house is *filed under* (`Household.HomeTile`, inside the footprint), and the view glides them there — across the wall,
  onto the roof. The painted people (A93) hide anyone indoors (resting at home, at school, teaching — `ShownAsAPerson`),
  so the moment the state turns they are gone: no door, no walk in. Waking, they appear on the roof and walk off it.
- **Someone cold goes to the nearest fire and stands on it.** `SeekingShelter` walks them to the nearest household's
  hearth that has firewood (`BehaviorSystem.NearestFire`: its `HomeTile`) and they **stay in that state on arrival**
  until warm (`ArriveAt`, D45) — drawn standing, hugging themselves, on the roof.
- **Dropping goods at a store, the market or the well, they stand on its roof.** A survey of his Ravenscar (`year-13.save`
  run through a summer and into winter, every person's state and what stands under them logged every half second):
  standing still on a building — *HaulingToStore* on the market, the granary and the fishing hut, *StockingTheMarket*,
  *DrawingWater* at the well, *Fishing* at the hut.
- **No smoke anywhere** (`art-inventory.md` B11). The roofs already paint chimneys (`building-roofs.md` §4): the cabin,
  cottage, smithy, bakery, tailor, farmhouse and workshop one, the manor two.

## 2. What burns firewood (the sim's own facts — smoke must be true to them)

| Where | When | Read from |
|---|---|---|
| **A home** | **winter**, while its larder holds firewood — burned on the hearth's beat (`HearthSystem.BurnADaysFirewood`); a hearth that runs out goes cold and says so | `Household.Stockpile.Firewood > 0`, the season |
| **The school** | winter, while its store holds firewood (`HearthSystem.KeepTheSchoolsWarm`) | its store, the season — ⚠️ its roof has **no chimney** today |
| **The smithy** | while someone is *Forging* there (`firewood_per_tool`; never the firewood the homes still want — the forge's rule) | a smith in `Forging` at it |
| **The bakery** | while someone is baking — the oven lit once a stint (`firewood_per_firing`) | a baker in `WorkingABatch` at it |

Not the tailor, the farmhouse or the workshop: chimneys, but no fire in the sim. **A chimney with no smoke is the truth
there**, and the player reading smoke as "this house is warm" is the point (legibility, Non-Negotiable 1).

## 3. Going indoors — proposed

1. **Through the door.** Every roofed building has one (`SimWorld.DoorOf`, D619–D621; the roofs already paint its step).
   A person whose walk ends **inside** a building with a door is drawn walking **to its door**, not across the wall: the
   last stretch of the glide is bent to the door's step. Leaving, they come **out of the door** and walk on.
2. **In, they fade.** Reaching the door in an indoors state, the figure fades over **0.3 s** in the doorway; coming out it
   fades in there. No pop.
3. **Indoors states** — today's three, plus **someone warming by a fire** (*SeekingShelter*, once arrived): they are in by
   the hearth, and the house's smoke says the fire is lit. The trades that work under a roof are **call 3**.
4. **At a store, the market, the well:** a person dropping goods off or drawing water stands **at the door** (the well's
   rim — no door, D624's list), facing it, not on the roof.
5. **Drawing and clicking agree** — one answer (`DrawnCentre`), as A93 and A133 kept it. ⚠️ That method and `DrawnTile`
   are in `VillageMap.cs`, the systems lane's file: **call 5**.

## 4. Smoke — three looks, mocked

Mocked on his Ravenscar in winter (a render of `year-13.save` run on to the first snow), three houses, a 4-second loop:
`D:\Projects\brushes\smoke\smoke_A.gif`, `smoke_B.gif`, `smoke_C.gif`, and `smoke_sheet.png` (each look across a second).

- **A — ink curls.** One pen line rising from the chimney in loops that swell as they climb, drifting with the wind and
  fading at the top. The river's ripples are pen marks; this is their kin.
- **B — watercolour puffs.** Soft grey washes with a pooled edge (the game's washes), rising, swelling and thinning.
  ⚠️ **Measured on the mock: on snow — which is when homes burn — a pale wash nearly vanishes**; it was darkened twice
  and still reads weakest of the three.
- **C — puffs, pen-edged.** B's puffs with a short pen arc over each one's top — a cloud as a manuscript draws it.
  ⭐ **Recommended**: it reads on snow and on grass, and it is the illuminated-manuscript hand the people are drawn in.

**Common to all three:** each chimney its own phase (nobody's smoke in step — D28's rule for the people); drift down the
same wind the trees will sway in (piece (i), later); the plume's life runs on the people's clock (game speed, capped at
1.5×, still when paused — A130's call 3); **a house that lights up starts smoking over a second, one that goes cold
stops the same way** (no pop); the bakery and the smithy smoke only while the work is on.

## 5. Guards (the probe; nothing is drawn headless — D340)

- `indoors:` — a walk ending inside a building ends at its door; a person warming by a fire is indoors; at a store they
  stand at the door; drawing and the click test give one answer; a fade, not a pop.
- `smoke:` — a home smokes in winter with firewood and not without, not in summer; the smithy only while forging, the
  bakery only while baking; the tailor, farmhouse and workshop never; a still frame while paused; two chimneys out of step.
- Red-checked, every guard; the reds counted (D326).

## 6. ⏸️ Joe's calls

1. **The smoke's look:** A ink curls, B watercolour puffs, **C pen-edged puffs (recommended)**.
2. **The school:** give its roof a chimney so its winter fire shows (recommended), or no smoke there.
3. **Who works indoors:** the smith, baker, miller, tailor and carpenter go in and work out of sight — the smoke and the
   yard show the work — **or they stand at the door working** (recommended for now: in, because piece (c), varied work
   animations, can bring them out to a bench later if he wants to watch them).
4. **The fade at the door:** 0.3 s (recommended), or an instant step through.
5. **`VillageMap.cs`:** bending the glide to the door touches `DrawnTile`/`DrawnCentre` there (the systems lane's file) —
   his OK to edit it, as A130's *"B, all as recommended"* gave for the animations.

**✅ Answered (A139):** *"1. Smoke B please. 2. okay 3. okay 4. instant 5. permission granted"* — **B** (not C, the recommendation: his eye over the mock's measure; B was darkened once more
in the build, `#b0a89e` with a `#787068` pooled edge), the school's chimney, the forge and the batch trades indoors, **no
fade** (gone on reaching the door), and `VillageMap.cs`.

## 7. ✅ As built (A139)

- **`VillageMap.Indoors.cs`** — `Indoors(state, housed, inADooredBuilding)`: at school and teaching always; resting with a
  house (A133); warming by a fire (*SeekingShelter*), *Forging* and *WorkingABatch* once inside a building with a door.
  `ShownOnTheMap` (drawing and the click test, one answer) hides them **once their glide is within 0.15 tiles of the door**
  (`AtTheDoorAlready` — call 4, no slow last shuffle). `AtTheDoor` draws anyone on a doored building's tile **at its door**,
  feet on the step: the painted step (kept per door tile as each roof is laid, `_paintedSteps`, dropped with the roof),
  or before the roof is laid the middle of the door's wall, just outside (`DoorWallOf`). A fisher stays at the water
  (piece (b)'s). The dots (tick off) unchanged.
- **`VillageMap.cs`** (his OK, call 5) — `DrawnTile` bends both ends of the glide through `AtTheDoor`; `AdvanceInterpolation`
  notes the fires once a tick (`NoteTheFires`); the home and workplace painters pass `burning`.
- **`VillageMap.Smoke.cs`** — `PuffAt`: six puffs a chimney, a 4 s life on the people's clock (capped 1.5×, still when
  paused), rising 1.6 tiles, drifting 0.65 down the wind, 0.1 → 0.42 tiles across, each chimney at its own phase;
  `SmokeOn` lights and puts out over a second, held while paused; one puff painted once (a wash with a pooled edge).
  **True to the sim:** `HearthBurns` (a home: winter, lived in, firewood in the larder); the school (winter, staffed, wood in
  its store); a smith *Forging* (`firewood_per_tool` > 0); a batch trade at work whose batch is fired (the oven).
- **`VillageMap.Roofs.cs`** — each cached roof keeps its flues (`FluesOf`: the chimney's dark mouth) and its smoke level;
  **the school has a chimney** (call 2).
- **Seen in a render of his Ravenscar** (`year-13.save` run into the snow): smoke over all three houses, swelling and
  thinning down the wind; a woman walking home up the path stops with her feet on the painted step and is gone.
- **Probe `indoors and smoke:`** (appended to `painted villagers:`): who is indoors and when, the door's wall, reached
  within a step, a home smoking only in winter, lived in, with firewood; on and off over a second and held while paused;
  a puff rising and swelling from nothing; two chimneys out of step; flues 1 / 1 / 2 / 0 (smithy, school, manor,
  forager's hut). **Red-check: 12 mutants, 8 red.** ⚠️ **Four score zero, written down:** the fisher kept off the hut's door, the forge's fire (`NoteTheFires`), the glide bent to the door, and the painted step kept — each needs a living village, which the probe's new game does not have; the last two were seen working in the render of his save, the first two were not.

## 7a. 🔨 Errands into a store go in (A141)

Joe, after playing A140: *"they still dont go inside the granary or the warehouse. is that intentional?"* It was — §3.4
had a person dropping goods off **stand at the door** — and his answer was *"Yes, step inside"*.

- **The moment is the sim's own.** A walk that reaches its tile mid-tick arrives on the next (`BehaviorSystem.Travel`), so a
  hauler stands one tick on the store's tile before the load goes in; that tick is drawn inside.
- **`GoesInByTheDoor(state)`** — one list, asked by the pure `Indoors` and by the villager's own answer (`ShownOnTheMap`):
  A139's three (warming by a fire, the forge, the batch trades) and every errand into a building — `HaulingToStore`,
  `HaulingToFarm`, `StockingTheMarket`, `StockingThePost`, `FetchingFromStore`, `FetchingMaterials`, `FetchingATool`,
  `FetchingACoat`, `FetchingAHandcart`, `FetchingHay`, `FetchingFromThePost`, `CollectingForMarket`, `CollectingForThePost`,
  `ClearingAStore`, `WalkingToTheMarket`.
- **Only with a door.** A pile, the well, a barn and a farmhouse have none (D624): the same errands stand there in the open.
- **Seen** in a traced render of his Ravenscar (1×, ~75 s): 9 haulers, 2 fetches, 5 market stockers and a marketer's
  collection went in at a door, each gone about a tick, then out of the same door.
- **Probe** (`indoors and smoke:`): a hauler, a fetch, a builder's materials and the market's stocking in at a door; a hauler on
  the road or at a pile seen; drawing water, walking home and cutting never in. **Red-check 5 mutants, 4 red; one zero written
  down** — the villager's own answer keeping the old three needs a living village; the traced render is its evidence.
- ⚠️ At 4× the tick inside is a third of a second: whether that reads as a blink is his eye's.

## 7b. ✅ The smoke darker, with the watercolour in it (A145; merged at his word, A146, `71212d5`)

Joe, relayed by the systems lane, 2026-11-21: *"smoke from chimnies needs a bit more watercolor touch and a bit darker"*.

- Four puff paintings (`PaintThePuff`) in place of one disc, each its own: a ragged edge (`PuffEdgeAt`, three slow waves), a
  pooled rim, granulation (value noise on a 3-px lattice), a paler bloom where the water ran back, warm and cool greys.
- Darker: wash `#8e857a` / `#7f8189`, rim `#4f473f` (A139's `#b0a89e` / `#787068`). The rise, drift, life and fading are A139's.
- Before / after on his Ravenscar's first snow: `D:\Projects\brushes\smoke\smoke_a145_before_after.png` (A139 above).
- Probe (`indoors and smoke:`): darker than A139's by 0.1 in tone; four paintings; each edge's reach varies over 12 %; the wash
  steps over 3 of 255 a pixel in its middle. Red-check in DESIGN §7, A145.

