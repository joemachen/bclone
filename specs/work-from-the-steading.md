# Spec: Work from the steading — farmhands rest at the farm, and tend it in summer

**Decisions:** D511 (this document). Built on: D355 (Joe's call — *the look*, rebuilt on `RestingPoint`,
the cost re-measured and accepted), D186 (the 2026-08-22 attempt, `slice/work-from-the-steading`,
`e12b20f` — the record, never merged), D148 (one name for two questions), D15 (nearest home wins),
D45/D53 (exposure), D10 (a meal is takeable where you stand), D194 (the self-fulfilling cap, and *a
ledger, not a hypothesis*), D354 (`Point`s; a villager going home stands ON it), D384 (trades visibly
work — the hash-picked tile), D385 (the rest flicker), D427 (the water trip).
**Status:** ✍️ **specified (2026-10-05); Joe's §8 calls answered (D512); building on this branch — nothing built yet.** Branch `slice/steading`
off `main`. Owner: Joe + Claude Code.

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
- **When — only in place of a rest.** Tending is asked at the two points where a summer farmhand
  would otherwise begin a rest at the steading: the farmer branch's `GoHome`, after
  `TryTidyGround` and `TryHelpWithHarvest` have both said no; and the last line of `Decide`. **It
  never takes a hand away from work they do today**, which is what makes it the look only. If tending
  sat above the chores, a summer farmhand would stop tidying and helping, and the economy would move
  for a cosmetic reason.
- **How long.** `tend_ticks` (new, `data/`, proposed **4** — a day), on the tile. Then
  `TravelingHome` to the steading for an ordinary rest spell (`rest_ticks`), and then `Decide` again.
  The rhythm is *tend, walk back, rest, tend*. That is enough to read as tending, and the walk is the
  look.
- **What it touches.** Nothing but position and state:
  - no yield,
  - no skill gain (it is not `BeginWork` — skill comes from sowing and reaping),
  - no tool wear,
  - no change to `Terrain`.
- **A new `VillagerState.Tending`.** The card reads *"tending the wheat at {farm}"*. The state needs
  an errand tile, which reuses `ErrandX`/`ErrandY` as the sow and reap legs do, so no new field
  is added.

## 6. Guards (tests first — each one red-checked, reds counted)

1. **The two methods agree.** `RestingPoint(v).ToTile() == RestingPlaceOf(v)` for every living
   villager on every tick of a seam run. *Red-check:* `RestingPlaceOf` returns the home tile.
2. **A farmhand rests at the steading in spring and autumn, and is home every winter tick.** Census
   the `Resting` ticks by tile. *Red-check:* drop the winter clause. Expect a farmhand resting at the
   farm in winter, and `Cold` rising there.
3. **The allocator costs from home (D148).** A farmhand whose home moves nearer another farm is moved
   by the next reshuffle. *Red-check:* `CostBetween` reads `RestingPlaceOf`.
4. **Tending is summer only, on this farm's sown tiles only, and the harvest is unchanged.** The seam
   run with tending switched off must reap the same tiles over the same ticks. *Red-check:* tending
   calls `BeginWork`, or picks with `Rng`.
5. **Tending never takes a job.** In the seam run, summer `TryTidyGround` and `TryHelpWithHarvest`
   counts for farmhands are equal with and without tending. *Red-check:* tending asked above the chores.
6. **Existing guards stay green.** `SomebodyWhoHoldsAJobRestsInSpellsToo` (D385's flicker) and the
   exposure tests. `FarmLedgerTests.AndItIsNotIdleThroughIt` stays green too, and its reading will
   move: autumn Resting goes from *at home* to *at the farm*. The reading is written down.

## 7. Measurement (filled in when built)

Today's code, both arms, the same fixtures:

- `FarmLedgerTests` distance arms (10 / 16 / 22 ticks out): tiles reaped, the farm's learned field per
  hand, brought-in %, and the autumn census by state **and by where**.
- The seam golden village, twenty years: wheat brought in, farmer-ticks moved, and births and deaths.
- Water trips per farmhand household per year (§8 call 2).
- Field tiles whose wear crosses the *worn* line in a summer (§8 call 1).

⛔ **Every delta gets a named cause from the census**, not a story. The 2026-08 attempt measured −13 %
and stopped there, and this investigation has already produced three causal stories that measurement
then rejected (D194). Goldens are re-taken with a dated *"before"* line each.

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
