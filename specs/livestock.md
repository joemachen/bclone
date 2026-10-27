# Spec: Livestock — the herd, the hay, and what winter is for

**Decisions:** D19, D39, D44, D52, **D59**, **D60**, **D61**. **Phase:** 2, after the
shelter-and-exposure slice.
**Status:** ✅ **BUILT WITH TRADE, PLAYED AND MERGED (D608) — the herd, the herdsman, the barn and the fenced
pasture (§0.2).** *The rest of this line is the build's history, kept:* 🔨 **UNBLOCKED BY TRADE AND RE-READ AGAINST TODAY'S CODE (D588); MEASURED (D589); THE ROWS AND THE STATE BUILT, NOTHING BEHAVES (D590: hay, the barn, the herdsman, `cattle`, a barn's herd — saved, hashed sparsely). ✅ **The herd lives (D592)**: grazing, winter hay, starvation a beast a short day, growth to the pasture, the cull into meat and leather — ✅ **And it is kept (D593)**: the herdsman cuts and carries hay, feeds it out in winter, culls what the pasture cannot hold; the barn's room is derived (§0.1).** Animals come
only by trade (D61), and trade is specced (D586–D587, `trade.md`); **Joe: trade and the herd are one build** (D587).
**§0 below is the re-read** — what this spec said in August, what is true now, and what the build does; where §0 and a
later section disagree, §0 wins. The build, commit by commit, is `trade.md §11`.

## 0. Re-read against today's code (D588) — read this first

Written 2026-08-01. Since then: gridless and buildings as obstacles, plots, painted work ground per owner, the farm's
seasons, building condition, the armful, the food chain, the clothing re-measure, nomads and trade. Joe's three calls
for the build (D588): **one kind of animal first**, **the herd gives meat and leather**, **one barn**.

| § | What it said | What is true now | So the build |
|---|---|---|---|
| 3, 12.1 | Blocked: no way to get a beast (D61) | Trade is specced; a boat every 2–3 years brings an assortment that can include animals (D587) | **Animals arrive by boat only**, landed at the post, walking to a barn with room |
| 4 | Two buildings: pasture as a zone, a barn as a store | One building is enough: today a workplace **owns painted work ground** (`ZoneMap.SetWorkGround` / `WorkGroundOf`, the farmhouse's field, the forester's ground) | **One barn** (Joe): the herdsman's workplace, the hay store, and its painted ground is the pasture |
| 6.1 | `ZoneMap` grows a second layer beside `_residential` | Owned work ground already is that layer, sparse in the hash | The pasture is the barn's work ground; **trees stay** (D490) |
| 6.2 | One herd per contiguous pasture, a `Herd` class | A workplace already carries per-site state (the farm's field memory) | **The herd lives on the barn**: head count, a growth accumulator, hay eaten — integers, hashed sparsely |
| 6.3 | `Stockpile.Hay`, its own store | Goods are rows in a catalogue (`GoodRow`) and an enum, hashed **sparsely** (a new row moves no golden unless it gets a starting limit) | **`Goods.Hay` = 13**, held in the barn's own store; no starting limit, so no golden moves for it |
| 6.4 | `JobKind.Herdsman`, two seasonal faces | The farmer already works by season (sow, tend, reap; rest at the steading, D513) | **`JobKind.Herdsman` = 13**: cut and carry hay in the growing seasons, feed the herd in winter, cull above capacity |
| 7 | *"Nobody can freeze doing it at the 7-tile bound"* | ⛔ **No longer true:** the bound went in D120; D533 measured outdoor winter freezes in played valleys (7 in its runs, 2 with a coat) | **Re-measured before the build**, and a guard: a herdsman never freezes (§9.6 kept, made a hard guard) |
| 9.5 | Baseline: 86 % idle in winter, 12.7 of 14.7 | D533 re-measured: winter ~71 % at a fire, ~1 % outdoor work | **The baseline is D533's**, re-taken in the same sitting as the arm |
| 10 | Numbers derived, the barn's capacity derived | Still the rule | Measured first (`trade.md §11`, commit 0): tiles a beast, hay a winter day, the breeding time, the cull's yield |
| 11 | Slice 1 has no output; butchering is slice 2 | Meat and leather exist (hunting) | **Joe: the herd gives meat and leather now** — the herdsman culls above what the pasture holds |
| 12.2 | One generic beast | Joe's `TECH-EXAMPLE.md` names eleven | **Joe: one kind first**, a data row, so the rest are added without restructuring |
| — | — | ⚠️ *Decided for Joe, to overrule in play* | **The barn is learned with the trading post** (once the town hall stands): there is nothing to keep in it before a boat can bring a beast |

> **⭐ THE CONTENT IT WAS MISSING NOW EXISTS (D206, `TECH-EXAMPLE.md`) — BUT THE BLOCKER IS
> UNTOUCHED, AND JOE'S OWN DOCUMENT CONTRADICTS HIS OWN RULING.**
>
> **What arrived:** eleven domestic species with primary and secondary outputs, four work and
> transport animals, ten wild game species, **pasture sizing by tiles-per-head with grazing drain
> and rotation**, winter fodder (hay and silage) with storage buildings, gestation and litter
> figures, male-to-female ratios, **auto-slaughter thresholds**, and **three barn tiers** with
> capacity, warmth and sanitation. §12.2 asked *one animal or several* and recommended one generic
> beast; **Joe has answered with eleven.**
>
> ⛔ **But D61 stands: *"I don't want animals available to the user until they trade for it."***
> `TECH-EXAMPLE.md` places the **Timber Barn in its T2** and the **Trading Post / Dock in its T3** —
> so **the herd arrives two tiers before the only sanctioned way to get one.** That is a
> contradiction inside the new document, not a change of ruling, and **resolving it is Joe's**:
> either trade moves earlier, or the barn moves later, or D61 is deliberately reversed.
>
> **✅ Two things absorb cleanly right now, blocker or no blocker:**
> - **Auto-slaughter thresholds are `StockLimits` (D62) one noun over** — *"maintain 12 female cows,
>   1 male"* is the same control as *"200 wood, 2000 food"*, and it should reuse that machinery
>   rather than grow a parallel one.
> - **Pasture-with-overgrazing is already twice-argued**: `buildings-plan.md §2.1`'s *surface
>   resources are finite in place*, and §8.1's *pasture should be a brush*. **D162 settled the
>   shape** — a painted zone plus a small steading that is the workplace, because the labour
>   allocator is built entirely around workplaces with a catchment.
>
> ⚠️ **And one thing to carry:** `TECH-EXAMPLE.md` has fodder rotting *"5% per day"* uncovered.
> **D208 refused that** — hay and silage exist because **grass stops growing**, not because anything
> decays. *A seasonal fact, not a rot tax.* D37 stands.

## 0.1 The herdsman — commit 3 of `trade.md §11` (D593)

Joe: *"the herdsman — cutting and carrying hay, feeding it out in winter, the cull as work, the quota, the freeze guard,
the barn's room derived."*

- ⭐ **The herd eats only hay put out on the pasture** (`Workplace.HayPutOut`), no longer straight from the barn's store as
  commit 2 had it. So the woodpile's sentence holds whole: *the herd starved because nobody fed it out* is as true a
  failure as *the barn ran dry*, and feeding is real winter work, outdoors.
- **Cutting (spring to autumn):** while the barn holds less than its winter's hay — what its pasture's full herd eats,
  with `winter_buffer_percent` — a herdsman walks to the next uncut pasture tile, cuts it (`reap_ticks`, tool and vigour as
  the farmer's), and carries the armful home with the ordinary haul errand (hay is barn-only, so the barn is where it
  goes). Each tile once a year: a counter, `HayCutThisYear`, with the tiles taken in the pasture's own order — never a
  per-tile memory. `hay_per_tile` 67, wheat's, until measured otherwise.
- **Feeding (winter):** while less than `hay_feed_ahead_days` of the herd's hay is out, a herdsman fetches an armful from
  the barn and puts it out on the pasture (`hay_feed_ticks`, outdoors — D53's exposure applies).
- **The cull, as work:** a beast born to a full pasture waits; a herdsman walks to the barn and culls it
  (`cull_ticks`): 900 meat and 10 leather in the barn's buffer. ⚠️ **With nobody seated at the barn, the overgrazed extra
  starves instead** — a beast a grazing day, no yield (*nobody is keeping the herd*), the same sentence as hay short.
- **The quota:** herdsmen are asked for only where a herd stands — one in winter while there is a herd to feed; in the
  growing seasons, while hay is short, as many as the pasture's full herd needs (`head one herdsman keeps`), up to the
  barn's seats; at least one when a cull is owed. Cutting yields to *food first*, as every non-food trade does
  (§10: the summer hands come out of what the food floor leaves spare).
- **The barn's room is derived** (§10), never typed: its seats × the head one herdsman keeps × a head's winter hay with
  the buffer. One herdsman keeps `FieldTilesOneFarmerKeeps` × `hay_per_tile` ÷ (`hay_per_head_per_winter_day` × days of
  winter × `winter_buffer_percent`) head — 8 today.
- **A herdsman never freezes** (§7, a hard guard): posed with a barn and pasture by the village on shipped valleys
  through winters of feeding; the margin is 30 tiles from the work to a lit hearth (§10.1.3).

**What building it taught (D593, traced, not guessed):**
- ⚠️ **`hay_feed_ahead_days` is 5, and the carrying starts that many days before winter.** At 3 a traced herd lost a beast
  on winter's first day (the first armful still on the road) and another between refills: a herdsman finishes his rest
  before he re-decides and spends winter going in to get warm. Five days of a full herd is one armful.
- ⚠️ **A tile painted once may since be unworkable** — a house on it, rock, out of reach. The cut cursor passes over it
  (`SimWorld.IsWorkablePasture`); before that a herdsman stalled a whole autumn on one tile and the herd starved.
- ⚠️ **A cull is long work, and a household nearly out of food sends its herdsman to fetch mid-cull**, as it sends
  anybody (`TryEmergencyRestock`). The cull stays owed and he comes back to it.
- **A calf born on the year's last grazing day goes into winter one beyond the pasture**: a herdsman culls it then (a cull
  comes first in any season); with nobody keeping the herd it eats its share.
- The tile scan runs only when the herdsman decides; the quota asks the scan-free `HayIsShort` every tick (CLAUDE.md).

---


## 0.2 The fenced pasture (D597 calls; D598 spec) — a barn's pasture locked in, fenced, gated

**Status:** ✅ **BUILT (D598 the sim, D599 the view), PLAYED (D600: *"the process goes pretty well"*) AND MERGED (D608).** *Fence the pasture*, the lock, the
fence's site, its walls, cancel and take-down, the barn taking its fence with it, saved and hashed (`SimWorld.Pasture.cs`,
`ZoneMap.RaiseAPastureFence`, `PastureFenceTests`); the barn card's fence row (its one button follows the state), the
fence drawn from its walls (faint while a site, the gate a gap), the brush's rubber refusing a fenced pasture, the probe's
`fence:` (`Main.Trade.cs`). ⚠️ Not probed: the button's press against a real barn, the fence's draw call, the rubber's
colour — Joe's eyes. ✅ **Played (D600); drawn round since D610** (Joe: *"barns that have round fences should be round,
not stairstep"*): the fenced tiles traced and rounded as a yard's outline is, the gate's span cut out at its posts —
the walls, the tiles and the gate unchanged, the line within half a tile of every wall (probed). Unplayed.

### 0.2.1 Joe's calls (2026-10-17)

*"when the player places/gives the ground for a barn, it should trigger a builder to build a fence around that area with
an entrance/exit. once painted it has to be completely unpainted (and demolished) and then repainted to created new/bigger
pastures."* Asked the same day:
- **A *Fence the pasture* button** on the barn's card: paint and repaint freely; the button **locks** the painted ground and
  queues a fence site for the builders.
- **The fence blocks walking except at its gate** — D404's walls, in the **one shared cost field** (§2.6).
- **Logs by the fence's length**, measured before typed (§0.2.5).
- **To change a pasture, take the fence down** — the ground is unpainted with it — then repaint and fence again.

### 0.2.2 What already exists (and is reused, not rebuilt)

- **D404's wall layer** (`specs/fences-as-walls.md`): `ZoneMap._walls`, one bit an edge, written only through
  `ZoneMap.Wall`; read by `TerrainCostField` / `TravelCostField` (rebuilt on `WallGeneration`) and
  `LineOfSight.ClearOfWalls`. ⭐ **Its §10 was written for exactly this** — *"a player-built fence … will write through the
  same method; the field asks is there a wall here? and never whose?"* — and fixed the hashing rule in advance: **hash the
  source, never the layer.** This fence is that source.
- **`ZoneMap.FenceEdges(tiles, lane, house, out gate)`** — the edges round a set of tiles, the first edge onto a *lane*
  tile left open as the gate. The pasture passes its fenced tiles, its gate's outside tile as the lane, and no house.
- **`SimWorld.WhatThisWouldWallOff`** and its trial fences — the refusal for a fence that shuts somebody in.
- **The bridge's site** (`MarkBridge`, `BridgeRecipe`): a construction that is not a footprint, priced per length,
  finished by its own branch in `Complete`.

### 0.2.3 The rules

1. **Fence the pasture** (a player input on the barn): refused, with the reason, when —
   - the barn has **no pasture painted**, or a fence already stands or is being built;
   - **no way in**: no fenced edge whose outside tile is free, walkable and reached from the village (the gate needs one);
   - it would **wall somebody off** — `WhatThisWouldWallOff` with the fence as a trial (a building inside the pasture is
     fine: it is reached through the gate).
   Otherwise: **a pasture in pieces is fenced round its main piece** (the largest; ties to the one nearest the barn) and
   the tiles apart from it are **given back** — said on the button before it is pressed and in the log after. ⚠️ First
   written as a refusal; measured on Joe's own pasture — **100 tiles and 2 apart** — it would have refused his barn. The
   main piece is **frozen** as the fenced tiles (whole tiles, `ZoneMap.WorkGroundOf` — never the quarter-tile curve), the
   **gate** is chosen, and a **fence site** is queued at the gate's outside tile.
2. **The gate** is the fenced edge whose outside tile is nearest the barn (taxicab to the barn's position; ties by the
   stated tile order, then N/E/S/W) among those whose outside tile is free, walkable and reached. One gate, as a yard's.
3. **Locked from the button on.** While a fence stands or is being built: the barn's ground can be neither painted nor
   erased (`CanPaintWorkGround` / `CanEraseWorkGround` say *"The pasture is fenced — take the fence down to change it."*),
   no building may be marked on a fenced tile or on the gate's outside tile, and the barn cannot be moved.
4. **The site** is a builder's site like a bridge's: `BuildingKind.PastureFence`, one tile at the gate's outside tile,
   **logs × the fence's edges** and **work × its edges** (the gate edge is not fenced, so not paid for). The walls go up
   **when it is finished** — as a bridge's deck opens at the end — so the builders never fence themselves in.
5. **Finished:** the fence's edges are walled (`ZoneMap.Wall`), the site retires, and the log says so. From then on
   everybody — the herdsman cutting hay, hay put out, a laborer at a tree — goes in and out by the gate.
6. **Cancelled while a site:** the builders' delivered logs come back (`CancelConstruction`), the ground unlocks and
   **stays painted** (nothing was built).
7. **Taken down when it stands** — ✅ **by the builders (Joe, D600: *"builders should demolish it like they do with other
   buildings"*; it was instant, as a bridge is pulled down)**: a demolition site at the gate owing the demolition share of
   the fence's work; the walls stand and the ground stays locked until they finish, and *Keep the fence* calls it off.
   Then the walls come down, **half its logs** come back to the nearest store, and **the ground is unpainted** (Joe:
   *"completely unpainted"*). The barn and its
   herd stay; with no pasture the herd starves at the next feeding until a new one is painted and fenced — said in the log.
   ⚠️ Taking a wall down must **put back every other fence's wall on a shared edge** — a yard's or another pasture's.
   (`ZoneMap.PullTheFenceDown` today puts back only *plots'* walls; both pulls are taught the other.)
8. **The barn pulled down** takes its fence with it (walls down, no extra refund) and cancels a fence site.
9. **Nothing walks the fence.** The herd is a number on the barn (D592) and is drawn on its own tiles (`HerdSpots`); the
   fence keeps villagers' paths out, not animals in.

### 0.2.4 The data

- **On the barn (a fact — saved, hashed sparsely under `0x7A`):** `FencedTiles` (tile indices, sorted), `FenceGate`
  (the gate's outside tile), `FenceStands`. *Empty* = no fence; tiles and not standing = a site; standing = built.
- **On the site (saved, hashed sparsely under `0x7B`, as a bridge site's far bank is):** `FencesBarnId`.
- **Derived, never hashed (D335):** the walls; `ZoneMap`'s per-barn edge list (its own index, keyed by **workplace id**
  — ⛔ never the yards' `_fenceByOwner`, which is keyed by household id and saved inside `plots`). Rebuilt on load from
  the barns.
- **Save:** read tolerantly (`HasValue`), so format-8 saves still open — no format bump.
- **Config:** `pasture_fence_logs_per_edge` **1** and `pasture_fence_work_ticks_per_edge` **2** (§0.2.5); the
  `PastureFence` building row prices one edge, as the bridge's prices one tile.

### 0.2.5 Measured (D598, `ZzFenceMeasure`, thrown away)

| Pasture | Tiles | Fence edges | Logs at 1 an edge | Work at 2 an edge |
|---|---|---|---|---|
| **Joe's barn 1** (willowdale-flint-thrush-27, Year 70; box 17 × 13) | 102 | **46** | 46 | 92 ticks (23 days, one builder) |
| painted by the brush round a barn, established valley | 20 / 40 / 80 / 120 | 34 / 56 / 88 / 118 | 34–118 | 68–236 |

Joe's village held **207 logs** (limit 200) with two builders. A log an edge — the yard fence's own price
(`fence_logs_per_tile`, D388) — makes his fence about a barn (40 logs): felt, not a tax. A fence round a brush-painted
pasture runs longer than a square's, because the brush skips rough ground; a compact pasture is the cheaper one, and
that is the right lesson. ⚠️ Both numbers Joe's to tune in play.

### 0.2.6 Guards (red first, counted)

Sim (`PastureFenceTests`): the refusals (none painted, in pieces, no way in, walls someone off, already fenced); the lock
(paint, erase, build on a fenced tile and on the gate front, move the barn); the site's recipe is the edges × the
prices; **no wall until it is finished**; finished, every fenced edge but the gate is a wall and the pasture is reached
only through the gate (a walk from outside to an inside tile crosses the gate); cancel unlocks and keeps the paint and
refunds; take-down unlocks, unpaints, refunds half and **leaves a neighbouring yard's wall standing on a shared edge**;
the barn pulled down takes its fence; save → load → hash equal with a site and with a standing fence; a village with no
fence hashes as before (all 362 goldens).
View (probe `fence:`): the fence drawn ~~on its edges~~ **as one rounded line within half a tile of its walls (D610)**, with
the gate open at its posts — on a disc and on a block gated mid-side; faint while a site; the barn card's button
reads the state (*Fence the pasture* / *Cancel the fence* / *Take the fence down*).

## 1. Goal

A **herd of animals on painted pasture**, kept alive across generations by hay cut the
summer before. It gives the village a second food source, the hides and wool clothing is
made of, and — the part that matters most — **the work that winter has never had**.

**Herding is optional** (D59, Joe): one food source among several the village chooses
between as it scales, not a tier every village climbs. A village that never paints a
pasture must remain exactly as viable as it is today.

---

## 2. Which pillars this serves

- **§2.2 smart labour, "food comes from many kinds of work".** The second raw source D19
  and D39 have been asking for, and the first one that is *land* rather than a point.
- **§2.5 environment with teeth**, and the organising rule of the seasons spec — *a season
  with teeth is one the player prepares for*. Hay is preparation you can point at, and it
  is cut in the season when hands are scarcest.
- **§2.3 systemic escalating pressure.** A herd that outgrows its hay is a pressure the
  player built themselves, by painting the pasture.
- **§1.5 generational time.** A herd is an inheritance. Forty years of building one up and
  a single bad winter that takes it is a story the game cannot currently tell about
  anything except people.
- **§2.7, at one remove.** It is what makes clothing possible, and clothing is *unlock by
  doing* arriving out of a survival mechanic.

### The non-negotiables, checked

- **Legibility.** Every loss traces: *the herd starved because the hay ran out because
  nobody was spared to cut it because the village was short of food that summer.* That is
  one sentence and it names a decision at every link.
- **Meditative pace — the real risk, and §8 is about it.** The player must never be made to
  click *"feed the animals"*. Fodder demand goes through `LabourQuota` like every other
  kind of work; the player's decisions are **how much pasture to paint** and **whether to
  build a barn**, and nothing else.
- **No combat.** Butchery is husbandry, resolved as a workplace producing goods. Nothing is
  depicted and there is no violence mechanic; the pillar is about military pressure and
  this is not that.

---

## 3. The shape, in one paragraph

**Grazing is free in the three growing seasons; winter is not.** Animals feed themselves on
painted pasture from spring to autumn and cost nothing but the tending. In winter there is
nothing on the ground, so they eat **hay** — which had to be cut, carried to a store, and
carried back out. Hay runs out and the herd starves.

That is deliberately **the woodpile's shape** (D53): the honest answer to *"why did the
cattle die?"* is **the hay chain failed**, in the same way the honest answer to *"why did
somebody freeze?"* is **the fuel chain failed**. One proven shape, twice, rather than a
second invention.

> ⚠️ **Re-measured (D589, §10.1.2): today's village has spare hands in summer too** (55–63 % of summer at home against
> 77–81 % of winter), so the inversion below is gentler than written. The design stands; the claim's size does not.

**And the seasonal inversion is the whole design.** Hay is cut in **summer**, when the
probe says the village has **0.7 spare hands**. It is spent in **winter**, when it has
**12.7**. So a pasture is a bet: labour borrowed from the season that has none, repaid in
the season that has nothing to do. That is the decision this slice exists to create, and
it is why livestock is not simply "a berry patch that also works in winter".

---

## 4. Inputs and outputs

| | |
|---|---|
| **Player inputs** | Paint pasture (a zone, D42's brush). Build a barn (D43's build menu). Optionally override herdsman staffing (D51). |
| **Sim inputs** | Painted pasture tiles, hay in the barn, herd size, season, spare hands. |
| **Outputs, slice 1** | A herd that grows, is fed, and starves when it is not. Outdoor winter labour. |
| **Outputs, slice 2** | Meat (`Goods.Food`) and hides, from butchery. |
| **Never an output** | Anything the food derivation depends on — see §5. |

---

## 5. The rule that constrains everything: the derivation may not depend on it

`VillageEconomy.RequiredGatherYield` solves for one weakest adult feeding themselves and
`RequiredDependants` children **by foraging** (D16, corrected by D57). Meat must be **slack
on top of that equation, never a term in it.**

Two reasons, and the second is the load-bearing one:

1. **Herding is optional** (D59). An optional source the economy depends on is not
   optional; it is a mandatory source with a brush.
2. **It is what makes this slice safe to take before D58.** The 7-tile bound is the one
   piece of work that reopens the derivation chain. Livestock being purely additive means
   the two do not have to be sequenced against each other, and the guard in §9 is what
   keeps that true rather than merely intended.

**Consequence to watch:** meat still reaches the granary, and the granary gates births
(D33). So livestock raises the population ceiling by the food it adds. That is intended —
D39's *"the winter buffer is priced, not capped"* — but it means `EconomyHorizonHouseholds`
and the warehouse and hut capacities D50 is a record of may need re-checking once meat flows.
**Check it; do not assume it.**

---

## 6. Data model

All integer, all hashed, no floats anywhere near it (D2).

### 6.1 Pasture — a zone, not a building

`ZoneMap` grows a second layer beside `_residential`. Same pattern, same reasons: painted
intent that the village acts on when it has a reason to, sim state, hashed, part of the
seed contract.

- **Pasture capacity is derived from the painted area**, not typed in:
  `animals = tiles / config.pasture_tiles_per_animal`. Painting more grass is how a player
  says "a bigger herd", which is the D42 pattern — you paint the neighbourhood, the sim
  picks the tile.
- Pasture may only be painted on **reachable, non-water, non-built** ground, reusing
  `CanBuildAt`'s occupancy question (the one D57 had to fix for being written twice).
- **Woodland is pasture too, and its trees stay** (Joe, 2026-10-04, recorded by D490 —
  `destroy-brush.md §3.4`): *"animals can live in the woods."* Unlike a farm's ground, a
  pasture painted over forest is not marked for clearing.

### 6.2 The herd

One herd per contiguous painted pasture, held as a `Herd` with:

| Field | Meaning |
|---|---|
| `Animals` | Head of livestock. Integer. |
| `GrowthAccumulator` | Integer accumulator; one animal born per `breeding_ticks_per_animal` animal-ticks, capped at pasture capacity. |
| `HayEaten` / `HayWanted` | For the winter's feeding, and for the panel to explain itself. |

**An accumulator rather than a rate**, for the reason D53 records: an integer accumulator
with a stated threshold has no rounding rule to get wrong and nothing to hash badly.

### 6.3 Hay

A new good, `Stockpile.Hay`, counted in `Held` like everything else.

**It gets its own store — a barn — and does not share the warehouse.** D52 is the argument: the
warehouse is one room, and packing it with logs left firewood nowhere to go and cost the village
a third of its population for a century. Adding a third good to the same room re-runs that
with an extra way to fail, and the player could not tell which shortage was biting —
non-negotiable 1. A barn is a `StoreBuilding` with a capacity, which is machinery that
already exists.

### 6.4 Jobs

`JobKind.Herdsman` — one job, two seasonal faces, which is new and worth stating plainly:

- **Growing seasons:** cut hay at the pasture, carry it to the barn. Structurally identical
  to a logger (`home → stand → warehouse → home`), so it costs what a logger costs and can be
  derived on the same basis.
- **Winter:** carry hay from the barn out to the pasture and feed the herd. **Outdoors**, so
  `Shelter.Outdoors` applies and D53's break-off rule bites — see §7.

Whether that is one `JobKind` or two is an implementation call; one is the recommendation,
because a villager whose job changes its shape with the season is truer than a villager who
is fired every autumn, and because the reshuffle cadence is three years (D46), not seasonal.

---

## 7. What this does to cold, and why clothing comes next

Herding is the first **outdoor winter work in the game**. `specs/clothing.md §5.1` measures
what the shipped cold model permits: `TrySeekWarmth` breaks a villager off at half the
exposure threshold and holds them at a fire until they are warm again, so an unclothed
herdsman works **about a third of winter** and spends the rest walking in and thawing.

**That is the intended state, not a bug to fix here.** It means:

- Livestock ships **survivable without clothing**, which is D45's stated condition and
  §7 of the clothing spec.
- Clothing then has a measurable payoff for the first time — roughly **3× the winter
  labour** — and it is an *unlock* rather than a tax.
- **Nobody can freeze doing it** at the current 7-tile bound: freezing needs 60 unbroken
  ticks outdoors and the walk to a fire is at most 7. That changes on its own when D58
  spreads the village out, with no number touched.

So the herd may starve in this slice. **The herdsman may not.**

---

## 8. Failure modes to design against

- **Babysitting.** If the player is ever required to act to keep animals alive, this slice
  has failed non-negotiable 2 regardless of what else it does. Feeding is `LabourQuota`
  demand; the only inputs are the brush and the barn.
- **A quota that does not ask.** The probe's finding is that winter idleness is the *quota*
  having nothing to want, not a shortage of hands (86% idle, 12.7 of 14.7). If hay demand
  is not written into `LabourQuota.For`, the pasture sits there and the slice measures as a
  no-op — the exact failure `specs/clothing.md §5` records.
- **Make-work in the other direction.** D52 deleted a winter fill that was bounded by
  *"is any warehouse not yet full?"* rather than by any real demand. Hay demand must be bounded
  by **what the herd will eat**, derived, not by barn space.
- **Livestock becoming load-bearing.** §5. Guarded, not hoped for.
- **The herd as an unloseable ratchet.** If animals can never die, the pasture is a
  free-money button and §2.3 gets nothing. Hence Joe's call: **the herd starves.**
- **Starvation that is not the player's fault.** The counterweight to the above. A herd
  sized to its pasture, with a barn built and hands available, must survive an ordinary
  winter — every time, for 300 years. A pressure the player cannot answer is not a
  pressure, it is a leak (D53's own framing).
- **Two hard things at once.** Livestock and clothing are separate slices, and slice 2 is
  separate from slice 1. Recorded three times now (D42, `specs/clothing.md §7`, here).

---

## 9. How it is tested

Per METHODOLOGY §3, and against **both** `VillageFixtures.Village` **and the shipped
`data/sim.config.json`** — the gap between them is where D48, D49 and D50 all lived.

1. **Determinism stays green.** Herd, hay and the pasture layer are all in the state hash.
   Same seed, same painting, same herd, byte-identical.
2. **The village is unchanged without a pasture.** 300 years, no pasture painted, and the
   population band and death counts match today's. This is §5's guard and it is the most
   important test in the slice.
3. **A well-run herd never starves.** Pasture sized to its barn, 300 years, zero animals
   lost to hunger.
4. **An over-painted pasture does.** Paint more grass than the village can cut hay for and
   animals must actually die — the anti-vacuity half (D7), because a starvation rule that
   never fires is a rule nobody has tested.
5. **Winter idleness measurably falls.** The baseline is measured and recorded: **86% of
   the workforce idle in winter, 12.7 spare hands of 14.7 able adults**, over 300 years, on
   both configs. With a pasture painted, that must drop. **Stated as a share, not a raw
   count** — two runs do not hold the same number of people, and D52 is a record of what
   reading a raw aggregate as a rate costs.
6. **Somebody is actually outdoors in winter.** The anti-vacuity guard for §7: at least one
   herdsman must be at the pasture during winter for a meaningful share of it, or every
   claim about winter work and everything clothing is later measured against is watching a
   case that never happens.
7. **The control's health is asserted before any comparison it is the control for** (D52).
8. **No new warnings or errors in a clean 300-year playthrough** (`CleanPlaythroughTests`).

---

## 10. Numbers, and where they come from

Derived, never tuned (D16). The stated targets:

> **Hay cut in one growing year must feed the herd through one winter, with the same margin
> the larder and the woodpile already carry** (`winter_buffer_percent`). Reusing that margin
> rather than inventing a third one is deliberate — it is the same question, asked about a
> third store.

> **A pasture must be affordable in the season it costs.** The summer hands cutting its hay
> come out of what the food floor leaves spare, so the derivation has to answer *how much
> pasture the village can actually keep*, the way `RequiredFirewoodPerSplit` answers how
> much fuel it can afford.

Content numbers that live in the config where a modder can reach them: how many tiles feed
one animal, how much hay one animal eats a winter day, how long a beast takes to raise, what
a butchered animal yields. **Consequences that must never be typed in:** the pasture the
village can sustain, the barn's capacity, and the herdsmen the quota asks for.

### 10.1 Measured on today's code — commit 0 of `trade.md §11` (D589, 2026-10-16)

`tools/harness/ZzHerd.cs`, `tools/harness/ZzClothing.cs` re-run, and copies of Joe's saves. Unattended harness villages
are not players (D447): they answer *how the village spends its time*, not *what a player builds*.

**1. Winter, today — D533's baseline re-taken** (`ZzClothing`, `played`, the shipped cold, 30 valleys × 50 years):
working-age villagers spend winter **74.7 % at a fire**, 17.8 % walking, 2.3 % going in to get warm, **2.1 % working
outdoors** and 0.7 % under a roof (D533 read ~71 % and ~1 %). Winter is idle; the herd's feeding is the first real work
it gets. ⚠️ *Side finding, recorded and not chased:* 196 cold deaths, every one in 5 valleys whose founders froze in
Year 1 (the founding race, D538) and which were then **re-founded every 4–6 years by parties that froze in turn** — 49
parties of four. An unattended valley never paints houses for its re-founders; a played one would. Harness, not player.

**2. ⚠️ The seasonal inversion is weaker than §3 says.** Share of working-age villager-ticks at home or a fire
(`ZzHerd.Seasons`, the same 30 valleys, 60 years):

| Years | Spring | Summer | Autumn | Winter |
|---|---|---|---|---|
| 1–20 | 47.5 % | 54.6 % | 54.7 % | 77.4 % |
| 21–40 | 57.1 % | 63.1 % | 60.2 % | 81.5 % |
| 41–60 | 57.1 % | 62.1 % | 59.6 % | 80.2 % |

§3 was written against a probe that read **0.7 spare hands in summer and 12.7 in winter**. Today's village has spare
hands in summer too — its stock limits are met and the quota asks for nothing (`trade.md §8.2a` found the same in Joe's
saves). **So hay will find hands, and a pasture's summer cost is smaller than §3 promised: the bet is gentler.** The
pressure the herd adds is the winter's (the feeding, the cold) and the hay chain's, not a scarce summer. Not a reason to
change the design; a reason not to sell it as *"labour borrowed from the season that has none"*.

**3. The cold margin — §7's claim, re-taken** (`ZzHerd.Cold`, the shipped numbers): outdoors in winter a villager
**breaks off for a fire after 30 ticks** (7½ days) and **freezes at 60**; a grass tile is 1 tick's walk, so the margin
is **30 tiles** from wherever the work is to a lit home. A herdsman freezes only if the pasture lies more than ~30 tiles'
walk from any hearth with firewood. The guard in commit 3 poses exactly that edge (a barn within the margin never freezes
its herdsman), and the barn's card warns when it stands beyond it.

**4. Ground for a pasture when the hall comes.** Harness (`ZzHerd`, 26 of 30 valleys reached the hall, median Year 52,
median 7 people): free grass or forest — not painted, not residential, not a plot, nothing standing — **within 10 tiles'
walk of the founding site: median 145 (87–185); within 20: 617 (413–848)**; the nearest such tile is 0–4 tiles from a
home. Joe's four hall-era villages (copies; straight-line from the hall, buildings not subtracted): **118–210 within 10
tiles, 565–822 within 20**, his biggest village (49 people) the tightest. Pasture is never short and never far.

**5. Anchors for hay — not yet typed.** A farmer keeps **13** field tiles (`VillageEconomy.FieldTilesOneFarmerKeeps`, the
biggest diamond one pair of hands reaps in an autumn, walking each armful home); a tile reaps **67** wheat; winter is
**30 days** (120 ticks); `winter_buffer_percent` is **180**. ⭐ **A hay field is that same question asked of the barn**,
so the herdsman's field comes from the same derivation, not a new one. *Illustration only:* hay at wheat's 67 a tile
gives one herdsman ~870 a summer; at 2 hay a beast a winter day with the buffer, that feeds about 8 head. The real
numbers come from commit 2's arms.

**6. Commit 2's numbers, and what they add up to (D592).** Joe's own (`TECH-EXAMPLE.md §3`) where he gave one, the hunt's
where he did not, on the `animals` row:

| Number | Value | From |
|---|---|---|
| `pasture_tiles_per_head` | 5 | Joe: *"Large Ruminants (Cow) — 5 Tiles"* |
| `hay_per_head_per_winter_day` | 2 | Joe: *"Large animals (Cows) consume 2 Feed Units/day"* |
| `head_days_per_birth` | 240 | Joe's cow: 90-day gestation, 1 calf, 120 days to maturity — about a calf a cow a year; half the herd cows; 90 grazing days of 120 |
| `meat_per_head` / `leather_per_head` | 900 / 10 | the hunt's `meat_yield` / `leather_yield` — one animal is one animal |

⭐ **Three independent numbers agree.** One herdsman's cut — `FieldTilesOneFarmerKeeps` 13 tiles at wheat's 67 a tile, if
hay yields as wheat does (commit 3 measures it) — is ~871 hay; a head's winter with the buffer is 2 × 30 × 180 % = 108;
so one herdsman keeps **8 head**, which is exactly Joe's timber barn (*"16 Small / 8 Large"*). At capacity 8 head add 8 ×
90 ÷ 240 = **3 calves a year, culled: 2,700 meat and 30 leather a herdsman-year**, beside the winter's work. Against a
hunter's ~1,319 meat per hundred ticks worked (D361) that is a modest food source and a real winter job — which is what
D59 asked of herding (*"an optional food source"*). ⚠️ **Derived, not yet played**: commit 3 puts a herdsman on it and
measures a played village's herd; the barn's room is derived there too, from the hay a herdsman can cut (it needs hay a
tile, which needs cutting).

---

## 11. Slices

Joe's call, 2026-08-01. Each ships playable and legible before the next starts.

1. **Pasture, herd, hay.** The brush layer, the barn, the herdsman's two seasons, growth to
   capacity, and starvation when the hay runs out. **No output yet** — this slice is bought
   entirely for the winter work and the pressure. Its acceptance bar is §9.5.
2. **Butchering.** A workplace turning animals into meat and hides. Meat is additive slack
   (§5); hides go nowhere yet and that is fine.
3. **Clothing** — `specs/clothing.md`, its own slice, its own DoD.

---

## 12. Open questions for Joe

1. ⛔ **Where the first animals come from — ANSWERED, and it blocks the slice (D61).** Joe:
   *"I don't want animals available to the user until they trade for it."* The
   founding-stock placeholder proposed here is refused, and rightly — it was the one link in
   the chain with nothing diegetic underneath it. **The consequence is that livestock now
   depends on §2.4, which does not exist**, and §2.4 is build-order item 7. Nothing in
   slices 1–3 can start until there is a way to buy a beast. See D61 for the routes out.
2. **One animal or several.** Recommendation: **one generic livestock** to start, defined in
   data so a modder or a later slice adds sheep and goats without restructuring. The
   argument is §2.5's own about biomes — *three shallow ones are worse than one properly
   habitable valley* — and wool can wait for slice 3 to need it.
3. **Whether a herd can be deliberately culled** by the player, or only butchered on the
   quota's judgement. Leaning: the quota decides, per §2.2, with the pasture brush as the
   real control.

---

## 13. Definition of Done — slice 1

1. This spec current.
2. Unit tests for the herd, the hay chain and the quota; the nine guards in §9 green.
3. Determinism test green.
4. Manual QA: paint a pasture, watch a winter, and be able to read off the screen why the
   herd is the size it is.
5. No new warnings or errors in a clean 300-year run.
6. `DESIGN.md` §6 and §7 updated.
