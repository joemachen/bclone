# Spec: Livestock — the herd, the hay, and what winter is for

**Decisions:** D19, D39, D44, D52, **D59**, **D60**, **D61**. **Phase:** 2, after the
shelter-and-exposure slice.
**Status:** ⛔ **specced and STILL BLOCKED — animals may only be acquired by trade (D61), and
there is no trade.** Everything below is still the design; §12.1 is the blocker. Slices
in §11.

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

---

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
