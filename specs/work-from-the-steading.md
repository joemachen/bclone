# Spec: Work from the steading — farmhands rest at the farm, and tend it in summer

**Decisions:** D511 (this document), D512 (Joe's §8 calls), D513 (built). Built on: D355 (Joe's call — *the look*, rebuilt on `RestingPoint`,
the cost re-measured and accepted), D186 (the 2026-08-22 attempt, `slice/work-from-the-steading`,
`e12b20f` — the record, never merged), D148 (one name for two questions), D15 (nearest home wins),
D45/D53 (exposure), D10 (a meal is takeable where you stand), D194 (the self-fulfilling cap, and *a
ledger, not a hypothesis*), D354 (`Point`s; a villager going home stands ON it), D384 (trades visibly
work — the hash-picked tile), D385 (the rest flicker), D427 (the water trip).
**Status:** 🔨 **built (2026-10-05, D513), sim and view, on `slice/steading` — UNPLAYED.** Suite 1520 / 0 / 5 of 1525
(3m58); view 0 warnings; probe green (`field lanes:` new), bar height 151; the two seam goldens moved, proven
to move for this rule only. Branch `slice/steading` off `main`, not pushed. Owner: Joe + Claude Code.

---

## 1. Why this exists

Joe, 2026-09-11 (D355): *"what happened to working from the steading for farmers and herdsmen?"* — and
before that, on the 2026-08-22 branch: *"of course farmhands should go home sometimes, but they don't
have to go home all the frickin time."* He chose **the look**: farmhands live out at the farm through
the working year and come home for winter. D507 put it next after save/load (*"then i want to try
steading again"*), and on 2026-10-05 he asked to revisit *"workers actually working at their place of
work."*

Two calls this session, asked before the spec was written:

- **Farmhands only** — D355's scope. Herdsmen inherit it when livestock lands (D61). ⛔ Not D58's
  work-in-place for every trade, which is still a later item behind clothing.
- **In summer they tend the field** — look only, no yield. Not just standing at the farm.

## 2. What the 2026-08-22 attempt got right, and what it could not have seen

`e12b20f` is 450 commits behind `main` and predates `Point`s. It is a record to read, not a branch to
merge. Reviewed on 2026-10-05:

**Kept:**

- **Two questions, two methods.** *Where do they stop right now* (`RestingPlaceOf`) and *where do they
  live* (`HomePlaceOf`) are different questions. `LabourAllocator` measures distance to work from
  where somebody **lives**. If the farm answered both, a farmhand's cost to their own farm would be
  zero, the allocator would find them unbeatable for that seat, and it would never move them. That is
  D148's bug in a new place.
- **Winter sends everybody home**, and that is what keeps it safe. It is still true on `main`: cold
  does not accumulate outside winter (`HearthSystem`, `IsHeatingSeason` — *"Spring resets
  everyone"*), and `ShelterAt` knows nothing about a steading. Staying out in spring, summer and
  autumn adds no way to die.
- **The guards.** Nobody stays out who cannot work, holds no job, works at a site or works at a
  non-farm. Nobody stays out for a farm with no painted ground.

**Verified this time, where the commit only asserted it:**

- **Nobody walks home to eat.** `TryEat` takes from the household larder wherever the villager stands
  (D10). A farmhand at the steading eats exactly as one at home does.
- **Summer seats are kept.** `FarmerSeatsWithGroundToWork` wants hands while a crop stands, on purpose
  (`crops-and-orchards.md §5`). That is why a summer farmhand exists to tend.

**What it could not have seen, and the rebuild must handle:**

1. **There are two resting methods now.** `RestingPlaceOf` returns a `GridPos` and `RestingPoint`
   returns a `Point` (D354). `GoHome` sends people to the Point and checks arrival against the tile
   (`BehaviorSystem.GoHome`, the end of `Decide`, the stranded check in `Travel`). If the two ever
   disagree, the result is D385's flicker or a villager who never arrives. **Invariant: `RestingPoint(v).ToTile() ==
   RestingPlaceOf(v)`, always.** One private rule answers both.
2. **The cost was measured and never explained.** −13 % of the harvest, with no cause attached. The
   code it measured is gone: D194's cap, D399's larders, D412's homes and clock B have all changed
   since. `FarmLedgerTests.AndItIsNotIdleThroughIt` now reads the farm ten ticks out as **17 % of its
   autumn Resting, every point of it at home** (16 and 22 ticks out: 4 % and 5 %; 145 tiles reaped at
   ten). A farmhand resting *at the farm* might well reap **more** now. It is unknown, and §7 says how
   it gets known.
3. **Callers that did not exist then.** These are classified in §4.

## 3. The rule

| Season | A farmhand with ground to work… |
|---|---|
| Spring | sows; between tasks, **rests at the steading** |
| Summer | **tends** the field (§5) when they would otherwise rest; rests at the steading between spells |
| Autumn | reaps; between tasks, **rests at the steading** |
| Winter | **home**, like everybody |

**A farmhand**, for this rule, is a villager who:

- `CanWork`,
- `HasJob`,
- works at a farm that is not a site,
- and that farm has painted ground (`Zones.WorkGroundTiles > 0`).

Anyone else rests at home, unchanged.

**The steading is `farm.Position`**: the point a farmhand already stands on to drop wheat at the farm
(D373). Two farmhands share it the way a household shares its doorstep (`HomePosition`). That is not a
new problem, so it gets no new concept.

**It changes where they stop, never whether.** Eating, warmth and the emergency restock run above
`Decide` and are untouched. Every trade, chore and fetch is still asked before the rest.

## 4. Home, or where they stop now — every caller

| Caller | Asks | After |
|---|---|---|
| `LabourAllocator` cost loop, `CostBetween`, the *"no way to walk from home"* sentence | where they **live** | `HomePlaceOf` |
| `VillageMap` commute line (home → work) | where they **live** | `HomePlaceOf` |
| `BehaviorSystem.GoHome`, end of `Decide`, `TravelingHome`, the stranded check | where they **stop** | `RestingPlaceOf` / `RestingPoint` (unchanged names, new answer) |
| `FetchingFromStore` / `ClearingAStore` fall-back destination | where they **stop** | unchanged: the fall-back is "go and rest" |
| `TryDrawWater` (D427) | **at home** — a household errand, and its lane runs door to well | `HomePlaceOf` — see §8 call 2 |
| `HouseholdSystem.MoveIn`, the founding comment | comments only | reworded to name the right method |

New, beside the old ones:

- `HomePlaceOf(v)` / `HomePoint(v)` — the old bodies, word for word.
- `RestingPoint(v)` — the steading Point when the rule holds, `HomePoint(v)` otherwise.
- `RestingPlaceOf(v)` ≡ `RestingPoint(v).ToTile()`. It is derived, so the two cannot disagree.

**It is derived state. No new field is stored, hashed or saved.** That keeps
`SaveLoadTests.EveryFieldIsSavedOrNamed` and the hash untouched.

## 5. Tending (summer, look only)

- **Where.** One of *this farm's own* `Sown` tiles, picked by a hash of (villager id, tick) over the
  farm's owned tiles in index order — D384's `AGatheringTileFor` shape. ⛔ **Never an `Rng` draw**: a
  look must not reshuffle every seed's history. A tile nobody can walk to is passed over. With no
  sown tile, they rest at the steading.
- **When — only in place of a rest.** Tending is asked beside the water trip, at the two points a
  rest spell would begin — `GoHome`'s already-there arm and the last line of `Decide` — so every trade,
  chore and fetch has already said no; and only **as a rest spell ends** (`State == Resting`). **It
  never takes a hand away from work they do today**, which is what makes it the look only. If tending
  sat above the chores, a summer farmhand would stop tidying and helping, and the economy would move
  for a cosmetic reason.
- **How long.** `tend_ticks` (new, `data/`, **4** — a day; 0 switches it off), on the tile. Then
  `WalkingBackToTheSteading` — ⛔ **not `TravelingHome`, which the skill clock counts as work**
  (`SkillSystem.OutOnTheWork`): a summer of tends walked back that way grows farming skill, and skill
  bites yield. Arriving back begins a full rest spell (`rest_ticks`); without it the arrival is a
  spell-less rest that ends next tick, and they would walk straight out again.
  The rhythm is *tend, walk back, rest, tend*. That is enough to read as tending, and the walk is the
  look.
- **What it touches.** Nothing but position and state:
  - no yield,
  - no skill gain (it is not `BeginWork` — skill comes from sowing and reaping),
  - no tool wear,
  - no change to `Terrain`.
- **Three new states, none of them work:** `WalkingOutToTend`, `Tending`, `WalkingBackToTheSteading`
  (appended to the enum). The card reads *"tending the wheat at {farm}"*. The errand tile reuses
  `ErrandX`/`ErrandY` as the sow and reap legs do, so no new field is added or saved.

### 5a. A load goes home (found building it)

Arriving to rest runs `UnloadAtHome`, which posts to the household's larder **wherever the villager
stands**. With the steading as the resting place, a farmhand who fetched the family's supper and walked
it "home" would have filled a cupboard across the valley from the farm — the teleport D30 and D45 each
closed once. So `RestPointFor` sends anybody **carrying** to the house and empty arms to where they rest,
and arriving at the steading never runs `UnloadAtHome`. For everybody but a farmhand in the working year
the two places are the same, so neither line changes anybody else (the goldens prove it).

## 6. Guards — built (`SteadingTests`, seven), red-checked: 11 mutants, 21 reds, no zeros

Each mutant was applied by exact text (matched once), built, run against the seven, and reverted;
a mutant that does not build scores nothing (D420) and none failed to.

| Guard | What it holds |
|---|---|
| `TheTwoRestingMethodsAlwaysAgree` | `RestingPoint(v).ToTile() == RestingPlaceOf(v)` for every villager every tick of two years — 5,044 asked, 1,438 of them resting away from home (refuses to pass on zero) |
| `AFarmhandRestsAtTheSteadingThroughTheWorkingYearAndAtHomeInWinter` | every rest **spell begun**, counted in the season it was decided in: 80 at the steading in spring–autumn, 38 at home in winter, 0 anywhere else |
| `TheAllocatorCostsAFarmhandFromWhereTheyLive` | `CostBetween` = the walk from **home**, and above zero, on every one of 720 ticks the farmhand rested at the farm |
| `TendingIsSummerOnlyOnTheFarmsOwnSownTiles` | 122 tending ticks over three summers, every one in summer, on the farm's own `Sown` ground, over 12 tiles |
| `TendingTakesNothingGrowsNothingAndDrawsNothing` | two villages in lockstep, `tend_ticks` 4 and 0: one hash on the eve of summer; at its end the same `Rng` state, the same crop on every tile, the same skill |
| `ALoadGoesHomeAndTheSteadingIsNotTheLarder` | an armful arriving at the steading stays in the arms (larder 156 → 156); walked "home", it goes to the house |
| `TendingNeverTakesAChoreFromAFarmhand` | posed: a summer farmhand's rest ending at the steading with a load on the ground — they fetch it |

| Mutant | Red |
|---|---|
| M1 `RestingPlaceOf` answers home | 3 — agree, allocator, load |
| M2 no winter clause | 2 — winter, summer-only |
| M3 the allocator costs from where they rest | 1 — allocator |
| M4 tending counts as work | 1 — takes nothing |
| M5 the walk back is `TravelingHome` | 2 — takes nothing, summer-only |
| M6 the tile is picked with the `Rng` | 1 — takes nothing |
| M7 tending asked above the chores | 2 — never takes a chore, summer-only |
| M8 tending in any working season | 2 — summer-only, takes nothing |
| M9 no rest spell after a tend | 1 — summer-only |
| M10 the steading unloads into the larder | 1 — load |
| M11 a load walks to the steading | 2 — load, summer-only |

⚠️ **Two guards were wrong when first written, and the run said so** (the trap in HANDOFF):
`world.Clock` after a step is the **next** tick's, so a census read after `StepOnce` put a rest decided
on autumn's last tick into winter; and `StepToTheStartOf` steps one tick **into** a season, so the
lockstep hash was compared after the first tend had happened. Both re-posed to measure what they claim.

**The view's line** — probe `field lanes:` (§8a): three worn tiles sown for a moment are drawn over the
field (210 vertices) and gone once the ground is put back. Red-checked: with the overlay reading only
bare `Field`, ⛔ *0 of 3* — 1 of 1.

**Existing guards moved, each with its reason written beside it:**

- `FarmGoldenTests` — the seam village (the only golden that reaches a farm), both hashes.
  ⭐ **Proven to be the only reason**: with `RestsAtTheSteading` false for everybody, the old values pass.
- `FarmMemoryTests.AFarmWithAutumnToSpareTriesOneMoreFieldAndStepsBackIfItRots` — the first pose that
  probes is now seed 12345 at nine ticks, year 2. Its hands-off autumn saw a store's fill move the haul
  walk, and `FieldTilesThisFarmCommitsPerHand` re-reckoned 7 → 10 with nothing reaped (D142's door);
  the lesson stepped back to 9. The guard now reads the step back from what the farm knew on the eve of
  the lesson.
- `FoodConservationTests` — seed 4 froze in its first winter; re-picked to seed 14, which lives on both
  arms (§7). The ledger held to the unit on every seed.

## 7. Measurement — main and the slice, the same scratch tests, one sitting (2026-10-05)

**The farm, one pinned farmhand, twelve years, no limits set** (`FarmLedgerTests`' fixture):

| Ticks out | Sown | Reaped | Wheat | Learned a hand | Autumn resting: where | Fetch trips · ticks a trip |
|---|---|---|---|---|---|---|
| 10 — main | 235 | 191 (81 %) | 13,429 | 8 | home 51 | 100 · 6.2 |
| 10 — slice | 217 | 197 (91 %) | 13,669 (**+2 %**) | 8 | farm 43, home 33 | 96 · 6.6 |
| 16 — main | 107 | 102 (95 %) | 7,375 | 4 | home 99 | 108 · 6.6 |
| 16 — slice | 117 | 110 (94 %) | 7,788 (**+6 %**) | 4 | farm 59, home 36 | 98 · 9.2 |
| 22 — main | 74 | 64 (86 %) | 4,887 | 3 | home 371 (13 % of autumn) | 127 · 7.1 |
| 22 — slice | 133 | 89 (67 %) | 6,379 (**+31 %**) | 4 | farm 52, home 30 (2 %) | 103 · 10.5 |

**The causes, from the census:**

- ⭐ **The harvest goes UP, against 2026-08's −13 %.** On main a distant farmhand rests at home between
  tasks — 13 % of autumn at twenty-two ticks out — and every rest ends in a walk back out. At the
  steading a rest ends beside the field. The further the farm, the more of the autumn that gives back.
  D194's self-fulfilling idleness, from the other side.
- **The far farm learns a bigger field, and rots more of it.** With autumn to spare, D361's probe climbs
  3 → 4 a hand at twenty-two ticks: it sows 80 % more, reaps 39 % more, and brings in a smaller share
  (86 → 67 %), because the extra tile a hand is the probe testing its edge. This is the farm memory
  working, not a cost.
- **Fetch trips are fewer and longer.** Over twelve years, 4–19 % fewer trips, each 7–48 % longer: a
  trip now starts at the farm and ends at the house. ⚠️ **Not attributed:** within that, autumn's
  share rises (13 → 16, 14 → 25, 17 → 21 trips) while the year's falls. Measured, with no cause named.
- **Water stays the household's** (§8 call 2): the farmhand draws less (22 → 9 trips at ten ticks out)
  and housemates take it up — 60 → 61 trips for the household at ten ticks, 40 → 53 at twenty-two.
- **The field wears** (§8 call 1): 9–10 of the farm's 42–49 tiles reach *packed* on the slice, against
  1–2 on main. Tending walks from the farm to the field and back, and every step treads. With §8a these
  are visible lanes through the crop. ⚠️ **That is the look to judge in play.** When *trampled fields*
  comes, a packed lane through a field is what it would charge, and tending's own steps are the
  question §8 call 1 leaves for it.

**Every source, seventeen valleys × fifty years** (`FoodConservationTests`' founding — lodge,
fishery and farm):

| | Valleys alive at fifty | People alive |
|---|---|---|
| main | 6 (one with 2 people) | 82 |
| slice | 8 | 124 |

Nine to eleven of seventeen die on either arm, mostly in the first winter: no woodcutter exists until
the farm's seats close for the year, and the first split races the cold. Seeds 4 and 16 flipped to
dead and 5, 9, 10, 11 and 12345 to alive. Seed 4 was traced on both: on main its first firewood landed
at t381 with three villagers near death at cold ~4,900; on the slice it landed thirty ticks later,
after a chore the history had moved (the histories part in the first summer, t211). A coin over seeds,
not a mechanism (D418). ⚠️ **The first-winter coin is main's, and it is worth Joe's attention on its
own** — it is not this slice's to fix.

**Tending, seen:** 39 tending ticks in the fixture's first summer; 122 over three summers, on 12 tiles.

## 8. Joe's calls (answered 2026-10-05, D512)

1. **Tending wears paths into the field: *"absolutely. so should villagers walking through
   fields."*** The sim already does this. Every step treads (D358, D424), with no exemption for a
   field. **What is missing is the picture.** Trails are drawn *under every field* (D358), so a lane
   worn through the wheat cannot be seen. Asked which he meant, Joe chose **show it now, the yield
   later.** This slice draws a worn trail through the crop (§8a), and tending treads like any walk.
   ⛔ **A sown tile under a path yielding less is NOT this slice.** That is his *trampled fields*
   (2026-08, *"forcing paths and limiting my wheat yield"*, with a player fence as the answer), and
   it stays its own later slice. When it comes, it must decide whether tending's own steps count
   against the field, because a look must not cost the harvest it is tending.
2. **The water trip stays a home errand: *"yes."*** A household whose only adults are farmhands
   draws water in winter only.
3. **A harvest cost worse than 2026-08's −13 % comes back to Joe before merging: *"yes."***
4. **The card wording stands: *"yes."*** *"tending the wheat at {farm}"*, and *"resting at {farm}"*.

### 8a. The trail through the crop (the view)

The trail mesh is drawn straight onto the ground before `DrawWorkedGround` (VillageMap's valley
pass), so the field's fill covers it. The change: **on a field tile, the trail draws over the
field's fill and under its stalks**, so a lane reads as a path walked through the wheat. Off the
fields, nothing moves: trails stay under the trees and the zone washes.

- It reads the same `PriceClassAt` grades the trail mesh already reads (D362). The picture and the
  walking stay the same paths.
- It is collected once a season with the trails (D366). ⛔ **Never per frame** (CLAUDE.md, D338).
- The probe's `trails:` line stays ✅. A probe check is added that a posed lane through a sown field
  is drawn.

## 9. Definition of Done

- The guards in §6 are green and red-checked, with the reds counted.
- §7 is filled in with causes.
- The goldens are re-taken with reasons.
- This spec's status line is true.
- DESIGN §4 (Phase 5 entry), §6 and a §7 entry are updated, and HANDOFF is updated (traps carried
  forward, one added).
- CLAUDE.md's four verification lines are run and read: the suite, the view build's warnings, the
  probe (bar height 151, `done.`), and the goldens diffed.
- Joe plays it with `run.bat`. Merge happens only at his word, and `slice/work-from-the-steading` is
  deleted only at his word.
