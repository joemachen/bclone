# Spec: Environment and seasons — the year stops being binary

> Status: **✅ four of five slices built.** The building selection panel, the calendar
> (D49), the buildings the economy had outgrown (D50), the idle winter (D44/D52) and
> shelter-and-exposure (D45/D53) have all shipped; **clothing is blocked** (see
> `specs/clothing.md`) and the **seasonal yield curve** is the last slice.
>
> §10 was resolved by Joe 2026-07-31 and the answers moved the centre of gravity: the
> yield curve is no longer first, and winter severity was replaced wholesale by the
> shelter-and-exposure model. §5.2 is superseded — read §10 and §11 before §5.
>
> Phase 2's headline (DESIGN.md §2.5). Everything else in Phase 2 so far — fuel,
> storage, the map, placement — was taken out of order deliberately. This is the
> subject the phase was named for.

---

## 1. Goal

Make the *year* a shape the player plays against, rather than a switch that flips
once and back.

Today the sim knows two seasons: **winter**, and **not winter**. Three lines is the
whole of it:

- `FoodSource.IsGatherable(season) => season != Winter` — foraging stops.
- `HearthSystem.IsHeatingSeason(season) => season == Winter` — firewood burns.
- `BehaviorSystem:411` — households start fetching firewood in autumn.

Spring, summer and autumn are byte-identical to each other. A villager cannot tell
them apart and neither can the player. §2.5 asks for *"seasons with real teeth, not
just 'it's colder now'"*, and what we have is closer to "it's colder now" than the
line was meant to allow.

---

## 2. Which pillars / non-negotiables this serves

- **§2.5 Environment with teeth** — the pillar itself.
- **§1.1 Legibility** — the organising rule below is a legibility rule, not a
  weather rule.
- **§1.2 Meditative pace** — a year with a shape is *less* micro, not more: the
  player prepares once in autumn rather than watching a gauge.
- **§1.5 Generational time** — "the winter of Year 34" is only a thing anyone
  remembers if winters differ from one another.
- **§2.2 Smart labour** — a seasonal yield curve is what finally gives *"trees do
  not stop in winter"* something to bite on (D17 records that it currently has
  almost nothing).

---

## 3. The organising rule

**A season with teeth is one the player prepares for. A season that merely damages
is weather.**

This is D37's lesson stated positively. Spoilage was cut because it was *"a tax that
arrives as a number going down for no reason the player took a decision about"* — and
a hard winter that simply burns more fuel is exactly the same object wearing a coat.
It punishes the well-run town as hard as the badly-run one, and it fails §1.1 because
the causal chain ends at a dice roll.

So every mechanic in this spec has to answer: **what did the player get to do about
it, and when did they find out?** If the answer is "nothing" or "when it killed
them", it is the wrong mechanic.

The corollary is that **the forecast is not UI polish — it is the mechanic.** A hard
winter announced at the turn of autumn is a decision (cut more wood, hold the granary
back, delay the house). The same winter unannounced is a coin flip. Build the
announcement in the same slice as the severity, never after.

---

## 4. The hard problem: the economy is derived from a per-tick constant

`VillageEconomy` derives the whole food economy from a stated target (D16) — one
adult at minimum vigour, no partner, feeding themselves and two children. Every
number downstream (`gather_yield`, `stockpile_target`, the winter store, the hands
that can be spared) falls out of that.

It does so assuming **a gather is worth the same every time it happens**. A seasonal
yield curve breaks that assumption directly: the derivation has to move from *"what
does one trip yield"* to *"what does a year of trips yield"*.

Three things follow, and all three are lessons this project has already paid for:

1. **The derivation has an order** (HANDOFF). `firewood_per_split` derives from the
   spare hands the *food* economy leaves. Re-derive the food economy first or the
   fuel numbers are nonsense.
2. **"Meets the target" is not "is the derived value."** A curve whose average
   happens to clear the bar while every individual season is wrong will pass every
   test we have. The tests must assert the *derived* value, not merely a survivable
   one.
3. **The worst case moves.** Today the worst case is a distant home at minimum
   vigour. With a curve it becomes a distant home at minimum vigour *in the leanest
   season*, and the winter store has to be sized against the leanest **run** of
   seasons rather than the leanest single one.

This is the same shape as the cost D28 records for making time-on-task personal, and
it is the reason that change was deferred rather than smuggled in. Treat this one the
same way: it gets its own slice, measured, before anything else in this spec lands.

---

## 5. What changes — in order of how much they earn

### 5.1 A seasonal yield curve (the one that carries the spec)

Forage yield varies by season instead of being flat-then-zero:

| Season | What it is | Yield |
|---|---|---|
| Spring | last year's berries gone, this year's not ripe | lean |
| Summer | steady | normal |
| Autumn | the harvest — this is what fall is *for* | best |
| Winter | nothing grows | zero (unchanged) |

**Why this one first.** It is the cheapest change that makes all four seasons
mechanically distinct, it needs no new UI (a villager simply comes home with more in
autumn), and it creates three further things for free:

- **Autumn becomes the season the granary is filled in**, which gives the winter
  store a *source* rather than just a size, and gives the forecast in §5.2 something
  to be a forecast *about*.
- **Winter becomes a logging season.** Logging has no curve — trees do not stop —
  so when foraging is worth nothing, the marginal hand is worth more at the stand.
  That is §2.2's stated advantage arriving as a consequence rather than as a rule,
  and it is the thing D17 says has nothing to bite on today.
- **Spring becomes the lean season it always should have been.** The village that
  ate its store in a mild winter finds out in April, not February.

**The numbers must be derived, not picked (D16).** Proposed stated target:

> *A household working normally through spring, summer and autumn fills its winter
> store by the first day of winter — with the leanest of the three still leaving it
> able to eat that season.*

The curve's shape then falls out of that plus the existing annual food need, rather
than being three multipliers somebody liked the look of.

### 5.2 Winter severity, forecast in autumn — ❌ **superseded by D45**

Kept for the argument, not the mechanic. Joe replaced it with shelter-and-exposure
(§10.2), which is better for a reason worth keeping: severity-as-a-multiplier is still
a number going down that the player cannot act on — the D37 objection to spoilage,
wearing a coat. Shelter and clothing are both things they *build*. The forecast idea
survives in spirit: D45's threshold, where a villager breaks off work to get inside,
is the same "tell them before it hurts" instinct expressed as behaviour rather than
as a log line.

### 5.2b (superseded text follows)

A per-year winter severity drawn from the run's seed — mild / ordinary / hard —
which scales `firewood_per_winter_day`.

**And announced at the turn of autumn**, in the life log, by name: *"The geese went
early. This will be a hard winter."* That sentence is the feature; the multiplier is
its implementation. Per §3, they ship together or neither ships.

**Severity, not length.** Varying how *long* winter lasts is the obvious alternative
and it is a trap: `SimClock` is **derived, not stored** — a pure function of tick and
config, correct by construction, with no rollover counters to drift and nothing to
get wrong in a save. Making season length vary per year means either storing the
calendar or making `FromTick` iterate over past years, and it would put a
year-dependent branch inside the one piece of state the whole determinism contract
rests on. Not worth it. Severity gets the same feeling for none of the cost.

### 5.3 Shoulder-season heating

`HearthSystem.IsHeatingSeason` is winter-only, with a comment already saying shoulder
seasons are a config change away. Burning fuel at a lower rate in late autumn and
early spring turns fuel from a spike into a continuous demand.

**Worth doing because of what it lets us delete.** D17 records a tension: the `+1`
standing woodpile in `LabourQuota.WoodcuttersWanted` is *"a stand-in for continuous
demand"*, and D22 records that removing it turned timber from a livelihood into an
errand. Real continuous demand is the thing the stand-in was standing in for. When
this lands, check whether the `+1` is still earning its place or has become
double-counting — D17 asks for exactly that check.

### 5.4 Biomes — deferred, and this is a re-affirmation not a punt

`specs/seeded-map-generation.md §10.3` already resolved it: **one valley archetype**,
built so a second can be added without restructuring, because *"three shallow biomes
are worse than one properly habitable valley."* Nothing since has changed that, and
§2.5's biome clause is the part of the pillar with the least to say right now — the
valley we have is not yet interesting enough to be worth having two of.

Soil quality is generated and hashed but unread. It stays unread here: it is reserved
for §2.3's soil depletion, which is Phase 6. **Do not spend it on seasons** — a
system reaching for the nearest unused field is how pillars get built in parallel.

---

## 6. Data model

Nothing new in world state if §5.1 and §5.3 are all that land — both are pure
functions of `Season`, which is already derived.

§5.2 needs a per-year severity. Two options, and the choice matters:

- **Derived from the seed and the year number** (`hash(seed, year) → severity`).
  Stateless, no save format change, no draw-order risk, and the whole climate of a
  run is a function of its seed — which is the property D18 spent a slice buying.
- **Drawn from the RNG stream at each year boundary.** Stateful, and it inserts a
  draw into the seeded stream, which changes every subsequent draw and invalidates
  every seed anyone has written down.

**Recommend the first.** It is strictly cheaper and it keeps the climate reproducible
from the seed alone, exactly as the map is.

Config additions (rules, not outcomes — the D18 form):

- `forage_yield_percent_by_season` — four values, spring/summer/autumn/winter.
- `winter_severity_mild_percent` / `_hard_percent` — how far severity moves fuel burn.
- `shoulder_heating_percent` — fuel burn in the shoulder seasons, 0 to switch off.

Zero must be a supported value for each, so every part of this spec can be turned off
and the village still tested against it — the `market_capacity: 0` pattern.

---

## 7. Failure modes to design against

- **Weather instead of pressure** (§3). The player is damaged and could not have
  acted. Guard: every severity is announced before it applies, and the announcement
  is asserted by a test.
- **The economy becomes a property of the weather.** If a hard winter can kill a
  well-run village, failure stops being attributable and §1.1 goes with it. Guard:
  **the derivation must be against the worst climate a seed can produce, not the
  average one.** All seeds survivable, none equally comfortable — the same rule map
  gen already holds (§10.4 there).
- **Seasonal micro.** If the right play is to re-assign labour four times a year by
  hand, the pace non-negotiable is gone. Guard: the labour allocator responds to the
  season on its own, or the season does not change what labour should do.
- **D20 collides with this, and it is a real conflict.** Labour reshuffles *once a
  year* on purpose: *"a seasonal reshuffle churns jobs fast enough that the stated
  reason for holding one goes stale before the player reads it."* But §5.1's whole
  payoff is that winter is a logging season — which the village cannot act on if it
  only reconsiders in the spring. **This needs resolving before §5.1 ships**, and it
  is question §10.3.
- **The lean spring kills the founding village.** Four founders arriving in a lean
  season with no store is a different opening from the one every test has run
  against. Guard: run the acceptance suite across all twelve seeds *and* across each
  possible founding season.

---

## 8. Testing

- Every existing 300-year acceptance run, unchanged in intent: the village still
  holds a band and is still standing at the end (D34's window lesson).
- **Twelve seeds × the hardest climate the config can produce.** Not the average.
- **Anti-vacuity** (D7): a test that the seasons genuinely differ — same villager,
  same site, different yield — so a curve accidentally flattened to 100% everywhere
  cannot pass silently.
- **The forecast is asserted**, not just the severity: a hard winter must produce its
  autumn line before the first day of winter.
- **Determinism unchanged.** If §6 takes the derived-from-seed option, the state hash
  and every existing golden seed must be **byte-identical** to today for a config
  with the curve flat and severity off. That is the test that proves the change is
  opt-in.
- `ShippedConfigTests` runs the real `data/sim.config.json` — add the new targets
  there rather than only to `VillageFixtures.Village` (HANDOFF: they diverge).

---

## 9. Definition of Done

1. This spec current, §10 resolved.
2. All four seasons mechanically distinct, and a player can say what each is *for*.
3. Economy re-derived (D16), in the right order (food before fuel), with the derived
   values asserted rather than merely met.
4. 300-year acceptance green on twelve seeds at the hardest climate.
5. Determinism test green; flat-config runs byte-identical to today.
6. A clean playthrough logs no warnings or errors.
7. `DESIGN.md` §6 and §7 updated.

---

## 10. Questions — resolved (Joe, 2026-07-31)

### 10.1 Is the seasonal yield curve the right centre of gravity? ❌ **No — the idle winter is (D44).**

Asking the question turned up the finding that answers it. `BehaviorSystem:144` sends
a forager home the moment the season turns, and `LabourAllocator` contains **no
reference to season at all** — so every forager in the village sits at home for the
whole of winter, and no reshuffle ever notices. A quarter of the working year, idle,
for whoever holds the most common job.

**That invalidates the argument §5.1 was built on.** The curve was justified partly on
making winter a logging season; the mechanism for that is the labour pass, not the
yield, and with a three-year reshuffle (§10.3) it would not have happened at all. The
idle winter is true today at flat yields, does not touch the derivation chain, and is
worth more. Taken first.

The curve is not cancelled — it is demoted to after the cold work, where it can be
done on its own terms rather than as a carrier for something else.

### 10.2 How hard should a hard winter be? ❌ **Wrong question — replaced by D45.**

Joe's model: **cold is about shelter and exposure, not household accounting.**

| State | A healthy adult is in danger within |
|---|---|
| Outdoors, no clothing | **~2 weeks** |
| Sheltered, no fire burning | **~6 weeks** |
| Sheltered, fire burning | never — the count **falls back** (D53; *resets to zero* as first written, which was measured to kill nobody) |

Villagers break off and seek shelter at a stated threshold, but still have to go out
for food and work. **Clothing** (leather, wool or cotton) removes the outdoor danger
entirely — and is therefore what unlocks winter as a working season, which is
§2.7's *unlock by doing* arriving out of a survival mechanic. Clothing waits on
D19/D39's production tier, so **every slice before it must be survivable without it.**

**Open sub-question, and it blocks implementation.** ✅ **Resolved by D49** — thirty-day
seasons — and then **by D52's successor**, because D49 landed in `data/sim.config.json`
and nowhere else, so the tests kept a fifteen-day winter and the numbers still could not
be expressed. Final: **15 days outdoors, 25 sheltered.** See
`specs/shelter-and-exposure.md`, and its §8b for the measurement that changed the third
row of the table above from *reset* to *thaw*.

### 10.3 Does labour reconsider at the turn of the season? ❌ **The opposite — every three years (D46).**

Joe: yearly is already too often. D20's objection scales — a reshuffle whose stated
reason goes stale before it is read is worse than no reshuffle.

**The cost is real and gets its own answer rather than a shrug:** a slower cadence
means the village limps longer after a worker dies. So **D47** — on a death, an
unassigned adult takes the vacant job immediately; if there is nobody spare, the UI
says so and marks the building unmanned or undermanned. The slow cadence stops
mattering once the urgent case is handled the moment it happens.

### 10.4 The three carried-over questions

- **Hunger at 2.8 days:** ✅ **good as it is** (Joe). `hunger_per_tick` stays 7, which
  locks step 1 of the derivation chain.
- **Residential brush radius / the village's request:** still open, not blocking.
- **Audit log gaps:** being addressed alongside the building panel — *why this store
  was chosen for a fetch* is exactly what the panel needs to show anyway.

### 10.5 Raised and recorded elsewhere

- **"Worst walk should be whole map — why are we limiting?"** → DESIGN.md §5, new open
  decision. Short answer: the worst case is not a check, it is a tax on everybody,
  because one global `gather_yield` is solved against it. The bound is scaffolding and
  has to go, but not while cold is being reworked.
- **Winter as a herding and slaughtering season** → D44's forward note. The honest
  answer to "what is winter work" is not *everyone becomes a logger*.

---

## 11. Slices

Re-sequenced after §10. No two hard things together — the lesson D42 records this
project learning twice.

1. **The building selection panel** (view only, no sim change). Click a building, see
   what it is, what it holds, who works there and why it is idle. Taken first on Joe's
   call: it is the instrument the next three slices are read through, and it costs the
   sim nothing.
2. **The idle winter** (D44). Give a forager something to do when there is nothing to
   forage. Sim-side, no derivation change. Includes the D47 death-and-vacancy handling
   and the move to a three-year reshuffle (D46), because all three are the labour pass.
3. **Shelter and exposure** ✅ (D45), minus clothing. `specs/shelter-and-exposure.md`.
   §10.2's calendar question was resolved by D49 — and then found to have landed only in
   the shipped config, which is what actually blocked this slice. `HearthSystem`'s
   household accounting is replaced by a positional model; the burning is untouched.
   **Survivable with no clothing**, and cold now fires when the fuel chain fails rather
   than on a per-household timer (§9.4 (c) and (d)).
4. **Clothing**, once D19/D39's production tier exists. The slice that turns winter
   from survivable into workable.
5. **The seasonal yield curve** (§5.1), on its own terms — economy to an annual basis
   as a byte-identical no-op first, then the curve turned on.

Biomes are not a slice here (§5.4). Winter severity is gone (§5.2).
