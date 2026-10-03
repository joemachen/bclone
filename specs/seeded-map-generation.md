# Spec: Seeded map generation — the valley is generated, not typed in

> Status: **✅ built — slices 1 and 2 of 3 (see §11); slice 3 is BRIDGES and is not started.
> §13, per-stage seeds (the shell's first step, D473), is specced and 🔨 being built on
> `slice/per-stage-seeds` — not on `main`.** · Owner: Joe + Claude Code
>
> ⚠️ *Corrected 2026-08-28: this said the third slice was "the harvest brush", which contradicted its own §11 (bridges) and was doubly wrong because the harvest brush shipped anyway (D87, D112–D130).*
> Format per `METHODOLOGY.md §2`. Implements decision **D18**.

---

## 1. Goal

Generate the world from the run's seed: terrain, water, forest stands and forage sites, into the bounds `SimConfig` already declares. Positions that are literal coordinates in `data/sim.config.json` today become **generator output**.

Two things make this structural rather than cosmetic, and D18 names both:

1. **A second playthrough becomes a different place**, rather than the same place played again — the same argument §2.7 makes for a broad tech tree.
2. **The sim is already fully seeded and deterministic**, so the world belongs to the *same* seed as everything else. Quoting one number should reproduce an entire run, world included. That is what makes shared seeds and bug reports work.

And a third that has become concrete since D18 was written:

3. **Homes are placed on a fixed spiral that knows nothing about where the work is.** `Household.PlacementFor` walks a square spiral around the origin regardless of what is nearby, which is what stops `forager_catchment_tiles` being tightened below ten (`specs/labour-allocation.md §8`). A generated map is where "build near the work" becomes expressible.

---

## 2. Which pillars / non-negotiables this serves

- **§2.5 Environment with teeth** — this is the pillar. Terrain has to *exist* before it can dictate viability.
- **§2.2 Smart labour** — catchment can only bind meaningfully when the map decides what is near what.
- **§2.6 Desire paths** — the trample field needs real terrain to run over, and water is the first thing that will make one route genuinely better than another.
- **Non-negotiable 1: legibility** — a player who dies to a bad valley must be able to see that it was a bad valley.
- **Architecture: determinism.** The generator draws from the run's seeded stream in a fixed order. Reordering a draw is a behavioural change, exactly as D5 says of system order.

---

## 3. The hard problem: the economy is derived from distances

This is the part that will break if it is not designed for, and it is the same shape as every previous economy mistake here (**D16**).

`VillageEconomy` derives `gather_yield`, `stockpile_target`, `firewood_per_split` and the workplace capacities from **how far the worst-placed home is from its nearest site**. Those distances are read straight out of config today. Generate the map and they vary per seed — so either the economy varies per seed, or the map must be generated to fit the economy.

| | How | Cost |
|---|---|---|
| **Derive per world** | Run the generator first, then derive the economy from the map it produced. | The economy stops being a property of the config and becomes a property of the run. Every test fixture that reads a derived constant has to build a world first. Harder to reason about: two seeds have genuinely different physics. |
| **Generate to a budget** ✅ **chosen (Joe, 2026-07-27)** | The generator is given the distance budget the economy is derived for, and **must produce a valley that fits inside it**. Reject and redraw otherwise. | The generator can fail, so it needs a bounded retry and a loud error if it cannot. |

**Chosen: generate to a budget.** It keeps one economy for all seeds, which is what makes a shared seed comparable and keeps `VillageEconomy`'s stated targets meaningful. More importantly it turns *"is this map survivable?"* into a **property test across many seeds** rather than a hope — which is exactly the guarantee a generated world needs and a hand-placed one never did.

The budget is not a new number: it is `VillageEconomy.RoundTripTicks` and its siblings, which already exist and are already asserted.

---

## 4. What gets generated

In draw order, which is part of the seed contract:

1. **The river.** One watercourse along the valley's long axis, wandering. Water is the first terrain that is not merely decoration — see §10.1.
2. **Forest stands.** Clusters, not scatter — a stand is a place you go to, and `JobKind.Logger` already assumes one.
3. **Forage sites.** Spread the way D24 requires: a ring at roughly settlement width plus a couple further out. **This is a constraint the generator inherits, not a free choice** — D24 is a record of what happens when sites cluster in one place.
4. **The founding site** — where the first homes, granary and warehouse go. Chosen by the generator as a spot that meets the budget in §3.
5. **Soil quality** — named here so the field exists in the data model, unused until §2.3's soil depletion lands.

**Not generated yet:** biome variety. One valley archetype, generated differently each time. See §10.3.

---

## 5. Data model

```
MapGenerator
    Generate(SimConfig, DeterministicRandom) -> GeneratedMap

GeneratedMap
    Terrain      : Terrain[width * height]     // Grass | Water | Forest
    ForageSites  : GridPos[]
    TreeStands   : GridPos[]
    FoundingSite : GridPos
    SoilQuality  : byte[]                      // reserved, unused
```

`SimWorld` takes a `GeneratedMap` instead of reading coordinates from config. The config keys those coordinates live in today (`food_source_x`, `extra_forage_sites`, `tree_stand_x`, …) become **generator parameters** — how many sites, how far out, how big a stand — which is the honest data-driven form and keeps a modder in control of the *rules* rather than the *outcomes*.

---

## 6. Determinism

- The generator draws from **the run's `DeterministicRandom`**, before any system ticks.
- **Draw order is the contract.** Adding a draw in the middle shifts every subsequent value and silently invalidates every saved seed and golden test — the same hazard `SimWorld.FoundVillage` already carries a warning about.
- The generator must be **pure**: same seed and same config ⇒ byte-identical map. Tested directly, not inferred from the sim's determinism test.
- Retries (§3) consume draws and that is fine, as long as the retry rule is itself deterministic.
- ⭐ **Ask "can this be a hash instead of a draw?" before adding generated content** (D344). The
  river's width, the clumps' outlines and — from D434 (`quarry.md §3.1`) — every stone and iron
  seam past the first four and two, and every seam's growth, take no draws: their jitter hashes
  the generator's stream state *read, not advanced*. So the woodland after them reads the same
  numbers and every seed keeps its forests. The goldens still move (the map changed); the valleys
  stay recognisable.
- ⛔ **But today no stage's draws can shift another's (D435):** `DeterministicRandom` is a
  `struct` and `MapGenerator` passes it to every helper **by value**, so each draws on a copy — the
  seams share one jitter, the river and the soil start from the same numbers. The rule above is
  about the design this section describes; the code does not keep it yet. Per-stage seeds fix it
  — **§13 replaces "draw order is the contract" with "a stage's draws are its own".**

---

## 7. Failure modes to design against

- **An unsurvivable valley.** The one that matters. Covered by generating to a budget (§3) plus a property test across many seeds.
- **A boring valley.** The opposite failure and much harder to test. If every seed produces the same shape with the furniture moved, this has cost effort and bought nothing. Worth a human looking at a contact sheet of maps rather than trusting a number.
- **Draw-order drift.** A refactor that reorders generation is a silent save-breaking change. Guarded by a golden test: a known seed produces a known map hash.
- **Water that is only paint.** If terrain does not affect travel cost, the river is a texture and §2.5 has not started. See §10.1.
- **The generator quietly relaxing its own budget** to avoid failing. If it cannot make a valley, it must say so loudly rather than shipping a marginal one — the METHODOLOGY §4 rule about never swallowing errors, applied to worldgen.

---

## 8. Testing

- **Same seed ⇒ identical map**, and different seeds ⇒ different maps (anti-vacuity, D7).
- **Every seed in a large sample produces a survivable valley** — the village still holds a stable size over 300 years. This is the property test that replaces hand-placement's implicit guarantee.
- **Every seed meets the economy's distance budget**, asserted directly rather than via survival, so a failure says *which* constraint broke.
- **Forage sites stay spread** (D24) — no seed may cluster them all in one place.
- **The founding site is reachable**: every founding home can reach a forage site, a stand, and the stores within catchment.
- **Golden map test** — a known seed hashes to a known map, so draw-order drift fails the build.
- **Nothing generates outside the valley bounds.**

---

## 9. Definition of Done

Standard DoD (`METHODOLOGY.md §3`), plus:

> **Positions are no longer typed into config, a stated seed reproduces the whole world, and a large sample of seeds all produce valleys a village survives 300 years in — with a human having looked at a set of them and found them worth playing twice.**

---

## 10. Questions — all resolved (Joe, 2026-07-27)

### 10.1 Does water block movement? ✅ **Yes — and crossing it is a technology (D40).**

Water is impassable. **The village learns to build bridges — wood first, stone as a later upgrade — and then builds one.**

This is the best fit for the design the project has found: it lands on four pillars at once. Terrain *dictates viability* (§2.5) instead of decorating it. The tech tree attaches to something you can point at on the map (§2.7). Desire paths get a genuine funnel (§2.6), because every crossing on that side of the river runs through one tile. And it is a placement decision whose value the player can see before committing. A river you can stroll across is scenery; a river you must go **round** until you can afford not to is the map arguing with you.

**Two consequences, both real:**

- **This needs actual pathfinding, and that is its own slice.** `TravelCostField.Cost` is Manhattan distance and `GridPos.StepToward` walks straight; neither knows terrain exists. The field is read by labour catchment, market errands and the economy's distance budget — the things that decide who eats — and §2.6 will later layer trample costs onto it. See §11.
- **Until bridges exist the generator must not cut the village off from its work.** A constraint on generation, not a hope, and it folds naturally into the budget in §3.

### 10.2 Who chooses the founding site? ✅ **The generator, for now.** Revisit when placement lands; choosing where to settle is a real decision but it belongs with the placement UI rather than blocking worldgen.

### 10.3 One archetype or several biomes? ✅ **One valley archetype**, built so a second can be added without restructuring. Three shallow biomes are worse than one properly habitable valley.

### 10.4 May a seed change the difficulty? ✅ **All seeds survivable, none equally comfortable.** Vary how much slack the valley gives, never whether it can be lived in — so failure stays attributable to the player, which is the whole of non-negotiable 1, while a seed is still worth talking about.

---

## 11. Sequencing

The fuel chain and storage both taught the same lesson: slices, each green before the next. Two hard things here — generation and pathfinding — and they must not land together, or they fail together and neither can be diagnosed.

1. **The generator, with water as terrain that nothing reads yet.** Terrain, river, stands, forage sites, founding site, all drawn from the seed in a fixed order and generated to the economy's budget (§3). Positions leave config. Golden map hash. **The village behaves exactly as it does today** — this slice is judged on determinism and on the property test across seeds, not on new behaviour.
2. **Real pathfinding in `TravelCostField`**, with water impassable. The slice that makes the river mean something. Expect the economy's distance budget to move: a path *round* water is longer than a straight line, and `VillageEconomy` must be re-derived rather than patched (D16). Watch the cost of the query itself — catchment and market errands call it constantly, so this will need precomputation rather than a search per call.
3. **Bridges** — the technology, then the building. Needs the tech tree (§2.7) and placement, so it lands after both. Until then the generator's guarantee from §10.1 is what keeps the village viable.

**Slice 1 is what this spec covers.** Slices 2 and 3 get their own specs; they are recorded here so the shape of the whole is visible.

---

## 12. Measured while building slice 1 (2026-07-27) — homes are the missing half

The generator works, is deterministic, and is in the state hash. The economy was re-derived for the new geometry (`gather_yield` 60 → 86, `firewood_per_split` 36 → 141). **But the village now holds a stable size for about 200 years and then dies**, against 300 before, and the measurement says why.

```
worst home -> nearest forage site: 10 tiles;  economy budgets 10
```

Zero margin — and the collapse is the classic shape: **people starve with 1,745 food in the granary and 2,269 in the homes**, households dying whole rather than the village thinning evenly.

**Tuning the ring radius does not fix it, and the way it fails is the diagnosis.** Tightening the ring from 5 tiles to 4 made the worst home *further* from its nearest site (12 tiles, not 10), because pulling the sites inward leaves the outlying homes with nothing near them. The relationship is not monotonic, so there is no radius that is simply "right" — which is the signal that this is structural rather than a number.

**The structural cause is the one §1.3 of this spec already named and slice 1 did not touch: homes are still placed on a blind square spiral.** `Household.PlacementFor` walks outward from the founding site knowing nothing about where the forage sites are, so a generated valley just gives that spiral a new set of sites to ignore. Hand-placed coordinates hid it, because the sites had been positioned around the spiral by hand until it worked.

**So slice 1 was not done.** It needed the other half: **new households choose where to build with regard to the work.** That is what makes a generated valley habitable by construction rather than by a lucky radius, and it is the same claim §2.2 makes about catchment — distance to work is not flavour, it is whether you eat.

### 12.1 ✅ Resolved — homes are placed, not spiralled

`Household.ChooseSite` replaced the spiral. The rule is **the two trips a household actually makes**: out to work, and over to the store. A site is scored on the sum of those, so a home sits between its livelihood and its larder rather than optimising one and paying daily with the other — one sentence a player can be told, which is the §2.2 test.

**~~The distance to work is a hard bound rather than part of the score.~~ ⛔ Superseded: D120 made it a score term and D381 removed the last refusal (the search box).** `VillageEconomy.MaxHomeToWorkTiles` states the budget and the economy is derived from it; `ChooseSite` scores against it and never refuses for distance. *Original:* `ChooseSite` refuses to build beyond it, and the economy is derived from it. That inverts what the derivation used to be: it scanned where a spiral *happened* to drop twenty homes and took the worst, so the budget was whatever the layout gave it. **Now the budget is a promise the village keeps rather than a measurement it discovers**, which is what lets one economy serve every seed.

A couple with nowhere left to build stays where they are and the village says so — a legible constraint (the valley is full), not an error, and the timber goes back in the warehouse rather than evaporating.

**Result: 291 tests green, and every one of twelve seeds holds a village for 200 years.** The economy re-derived to `gather_yield` 86 and `firewood_per_split` 141.

### 12.2 A bug it flushed out on the way

With one full berry patch and a tree stand the village wanted nobody at, three idle villagers were told *"the village has all the hands it needs — 4 foraging"* while exactly one of them was foraging. The refusal reported the *nearest* reachable workplace, and the stand happened to be a tile closer than the full patch. It now reports the nearest place that is **full and still wanted** — the one the player can act on by building another — and falls back to "enough hands" only when no such place exists. A sentence that contradicts itself in its own second clause is non-negotiable 1 failing.

### 12.3 Noted for slice 2

On some seeds the river runs straight through the settlement. Harmless today, since nothing reads water — and **exactly the case the generator will have to guarantee against** once it is impassable and before bridges exist (§10.1).

---

## 13. Per-stage seeds — each stage of the valley draws on a stream of its own (D473)

*The shell's first unbuilt step (DESIGN §4, Phase 4.5): error boundary ✅ → **per-stage seeds** →
the new-game screen → settings persistence → save/load → title and pause. Joe, 2026-10-03:
"shell first, then D395 threads."*

### 13.1 Why

1. **The contract §6 states is not what the code does (D435).** `DeterministicRandom` is a
   `struct`, and `MapGenerator` hands it to `CarveRiver`, `DrawJitter`, `DrawRingPosition`,
   `PaintSeams` and `PaintWoodland` **by value**. Each helper draws on a copy, so the caller's
   stream never moves: every drawn seam in a valley sits at the same offset from its slot (iron's
   always on the diagonal), the founding jitter's x and y are one draw, the river's wander and
   the soil start on the same numbers, and the seams' jitter is the woodland's first draw.
2. **The sim's stream mirrors the river.** `SimWorld` passes its own `Rng` by value too, so the
   first numbers the founding draws (names before D466, rhythms, lifespans) are the numbers the
   river drew. Harmless, but not independent.
3. **The new-game screen wants options that generate things** (a lake, an island, a cliff —
   Joe's D344 ambition). With one stream, each option would reshuffle every seed and every golden
   once. With a stream per stage, a new stage moves **no other stage's valley**.
4. **D466 already reshuffled every seed** (names), so this reshuffle costs Joe no valley he still
   remembers.

### 13.2 The rule

- The valley is generated in **stages**, each with a stated id that is **never renumbered**:

  | Id | Stage | Draws |
  |---|---|---|
  | 1 | River | its start row, then −1/0/+1 a column |
  | 2 | Founding | the founding jitter's x, then y |
  | 3 | Stone seams | x, y jitter per drawn seam; its seed keys the hashed seams |
  | 4 | Iron seams | the same, for iron |
  | 5 | Woodland | x, y per clump |

  ⛔ **Soil is not a stage.** Ground quality is removed (D395, built as D470); the
  soil draw, its array, its regions, the founders' cap and its three config keys go with this.
- A stage's stream is `new DeterministicRandom(StageSeed(seed, id))`, held as a local in
  `Generate` and handed to helpers **by `ref`**, so draws within a stage advance that stage.
- `StageSeed(seed, id)` = splitmix64's finaliser over `seed + 0x9E3779B97F4A7C15 × (id + 1)` — the
  same fold `HashJitter` and `NameHash` already use, in one shared `SplitMix64.Fold`.
  ⛔ **Never `DeterministicRandom`'s `stream` parameter**: D344 measured small adjacent stream ids
  six times deadlier (6 dead valleys of 24 against 1).
- **A new stage appends a new id.** Draws added *inside* a stage still move that stage (and
  whatever later reads its terrain — woodland paints only over grass, so a new river shape moves
  trees), but never another stage's draws.
- The hashed seams (`quarry.md §3.1`) key their jitter on **their own stage's seed** rather than
  on `rng.State` read mid-generation.
- `MapGenerator.Generate(SimConfig, ulong seed)` takes the seed, not a stream. `SimWorld.Rng`
  stays `new DeterministicRandom(seed)` and worldgen never touches it, so the sim's own draws are
  unchanged by this slice; only the valley moves.

### 13.3 What it costs, and how it is measured

- **Every golden with a map in it moves once**, in one commit, for this reason alone — proven by
  putting the shared stream back and getting the old hashes.
- **Measured before any golden is re-taken**, against the commit before it: the dead-valley rate
  on the 24-seed survival guards, D420's 55 villages and the 100 shipped seeds (`ZzBase`,
  `ZZ_WIDE=1`). D344's splitmix arm read 2 dead of 24 against 1; that is the number to beat or
  explain.
- ⚠️ **The shipped seed (12345) may land on a valley that starves** (D344's first attempt did
  exactly that). If it does, the work stops and Joe picks a new one from three or four
  candidates with their numbers — it is his to choose.

### 13.4 Guards (each red-checked, the reds counted — D326)

1. **Two drawn seams of one kind do not share an offset from their slots** across a sample of
   seeds (red: hand the stream to the seams by value again).
2. **Changing one stage's config moves no earlier stage's tiles**: woodland coverage changed ⇒
   river, founding and seams byte-identical; seam counts changed ⇒ river and founding identical
   (red: let woodland draw on the river's stream).
3. **Stage seeds are pairwise distinct and none is the run's seed** (red: `StageSeed` returning
   `seed`).
4. The golden map hash and the per-seed terrain fingerprints, re-taken once.
