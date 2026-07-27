# Spec: Seeded map generation — the valley is generated, not typed in

> Status: **draft — open questions for Joe in §10** · Owner: Joe + Claude Code
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
| **Generate to a budget** *(recommended)* | The generator is given the distance budget the economy is derived for, and **must produce a valley that fits inside it**. Reject and redraw otherwise. | The generator can fail, so it needs a bounded retry and a loud error if it cannot. |

**Recommendation: generate to a budget.** It keeps one economy for all seeds, which is what makes a shared seed comparable and keeps `VillageEconomy`'s stated targets meaningful. More importantly it turns *"is this map survivable?"* into a **property test across many seeds** rather than a hope — which is exactly the guarantee a generated world needs and a hand-placed one never did.

The budget is not a new number: it is `VillageEconomy.RoundTripTicks` and its siblings, which already exist and are already asserted.

---

## 4. What gets generated

In draw order, which is part of the seed contract:

1. **The river.** One watercourse along the valley's long axis, wandering. Water is the first terrain that is not merely decoration — see §10.1.
2. **Forest stands.** Clusters, not scatter — a stand is a place you go to, and `JobKind.Logger` already assumes one.
3. **Forage sites.** Spread the way D24 requires: a ring at roughly settlement width plus a couple further out. **This is a constraint the generator inherits, not a free choice** — D24 is a record of what happens when sites cluster in one place.
4. **The founding site** — where the first homes, granary and shed go. Chosen by the generator as a spot that meets the budget in §3.
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

## 10. Open questions (for Joe)

### 10.1 Does water block movement, or is it scenery for now?

The load-bearing one, because it decides whether this touches the shared cost field (§2.6).

- **Scenery** — the river is drawn, and villagers walk over it. Cheap, honest as a first step, and the river becomes real later.
- **Impassable** *(recommended)* — water costs infinity in the one shared `TravelCostField`, so a route has to go round. This is what makes terrain *dictate viability* rather than decorate it, it is the first real test of the shared-cost-field decision, and it is what gives desire paths something worth wearing a groove toward (a ford, a bridge). **Cost:** the generator must guarantee no home is cut off from its work, which is a real constraint on generation, and the cost field stops being a straight-line calculation.

*Recommendation: impassable.* A river you can stroll across is not an environment with teeth, and doing it later means re-deriving every distance in the economy a second time.

### 10.2 Does the generator choose where the village starts, or does the player?

Joe has just confirmed **building placement is in**, which changes this. Options: the generator picks a good founding site (as today, invisibly); or it picks a few candidate valleys and the player chooses where to settle.

*Recommendation: generator picks it for now*, and revisit when placement lands — choosing a starting site is a real decision but it belongs with the placement UI rather than blocking worldgen.

### 10.3 One valley archetype, or several biomes?

§2.5 names "river valley vs. highland vs. coast". That is a much bigger piece of work and each archetype needs its own economy sanity-checking.

*Recommendation: one archetype now*, built so a second can be added without restructuring. Shipping three shallow biomes is worse than one that is properly habitable.

### 10.4 How much should the map vary the *difficulty*?

If every seed is equally survivable, seeds are cosmetic. If they are not, some runs are unwinnable through no fault of the player, which §1.1 hates.

*Recommendation: all seeds survivable, but not equally comfortable* — vary how much slack the valley gives, never whether it can be lived in. That keeps failure attributable to the player, which is the whole of non-negotiable 1, while still making a seed worth talking about.
