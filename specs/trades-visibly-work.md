# Spec: Trades visibly work — a woodcutter's stint, a forager in the ring, a hunter in the woods

**Decisions:** D384 (this document). Neighbours: D282 (`fish_ticks` 3 → 10, a pacing change measured on the rig), D288/D293 (the yield rigs), D355 (the steading, the look only), D361/D363 (the food ladder and its floor), D373 (a building visit lasts a tick), D383 (obstacles).
**Status:** ⏳ **in progress (2026-09-16)** — §2 the woodcutter's stint: ✅ built (D384 part 1, unplayed). §3 the forager in the ring: not started. §4 the hunter in the woods: not started. Owner: Joe + Claude Code.

---

## 1. Why this exists

Joe, 2026-09-15: *"im not sure that hunters spend any time at the hunting lodge or in the forest
actually 'hunting', same with foragers … woodcutters should be at the hut cutting wood for a period
of time. foresters should be pruning/maintaining their marked forests. builders will eventually be
maintaining buildings … most professions will have this aspect to their role."* Recorded as Phase
5's *Trades visibly work* (`DESIGN.md §4`, D374) with its rule: **every walk into the ring moves
that trade's yield rig, so each trade lands measured, one at a time, and the look ships with the
number that keeps the food ladder (D363's floor) — never the look alone.**

What the sim did before this spec, traced in `BehaviorSystem`:

| Trade | Worked standing on | For | Then |
|---|---|---|---|
| Forager | the hut's own point | `gather_ticks` (3) | home or the granary; never in the ring (`forests-and-gathering.md §3.3` said so and left the door open) |
| Hunter | the lodge tile | `hunt_ticks` (15), re-armed in place until the lodge was full | the ring was only a yield multiplier |
| Woodcutter | the hut | `split_ticks` (4), **once** | always home — one split per trip |
| Forester | a marked tile | fell (4) or plant (12) | **already in its ground** |
| Farmer | a field tile | sow / reap | already in its field |

The view draws no state — a villager is a circle coloured by life stage; only the card says what
they do — so **the look is where a person stands and for how many ticks** (D373: an arrival that
departs the same tick is invisible).

## 2. The woodcutter works a stint at the hut (§1 of the slice)

**Rule.** After a split, if the yard still holds a batch of logs and the firewood limit is not met,
the woodcutter splits again where they stand — up to `splits_per_stint` (**4**, a day at four ticks
a day) before walking home. Hunger still pre-empts a split (`TryEat` runs first every tick); an
emptied yard or a met limit ends the stint early, as before.

**What it costs, and where it is derived.** `FirewoodRoundTripTicks` priced one walk per split;
a stint is one walk per `splits_per_stint` splits. `VillageEconomy.FirewoodStintTicks` = the walk
there and back + stint × `split_ticks`; `FirewoodMadePerYearAtWorst` counts stints × splits — a
woodcutter's year is 2.7× what it was (44 splits against 16). The consequence, in the order the
derivation runs (food, then fuel): the fuel target's floor for `firewood_per_split` fell 53 → 40;
the shipped 53 stays (Joe's deliberate 50 was raised to the D383 floor; above the floor a bigger
batch is cheaper in logs, and moving it back re-founds every fixture forester's ground for
nothing).

⛔ **And the stint ends when the sheds hold what the homes want** (`LabourQuota.FirewoodShortfall`),
not only at the player's limit: a hand that splits four times as fast turns the village's logs
into firewood nobody asked for, the warehouse fills with it while the builders wait for timber
(trap 29's shape), and the no-seam founding fell 24 → 16 and the market-off village to one soul
before that line.

⛔ **Two derivations were leaning on the old timing** (D361's lesson, one system over):
`RequiredForesterSeats` and `HandsNeededForFuel` sized the loggers from *woodcutter capacity ×
logs per woodcutter-year* — demand only while one woodcutter's year was about one seat's worth of
heat. With the stint, two seats of capacity became three thousand firewood against a horizon
village's seven hundred, and the founding forester's hut was sized for logs nobody would burn:
it claimed every wooded tile near the founding and four guards found no woodland left to give a
second hut. Both now derive from the logs the homes' firewood takes; `LoggersToFeedTheHuts`
(the live quota) likewise from the shortfall. `LogsConsumedPerYearAtWorst` is deleted.

**State.** `Villager.SplitsThisStint` (hashed) — reset when a stint begins, counted at each
completion, reset when they leave.

**Legibility.** The card's sentence carries the stint: *"splitting logs into firewood, the 3rd
split of the day"*.

**Guard.** `AWoodcutterSplitsAStintBeforeWalkingHome` (`FirewoodTests`): with a full yard and no
limit, a run of splitting lasts at least two splits' ticks and no more than a day's four plus the
arrival tick and two meals. ⛔ Red with the re-arm off (five ticks against eight — the first draft's
"more than one split" went green on the arrival tick, and the red check caught it). Existing: the fuel target, `AWinterBurnsExactlyWhatTheEconomyBudgets`, the firewood conservation guard.

## 3. The forager gathers in the ring (§2 of the slice) — not started

Home → a wooded tile of the hut's ring within `gather_walk_tiles` (3) of the hut, nearest first and
rotated by a hash of (villager, trips) — ⛔ never an `Rng` draw — → `gather_ticks` there → home or
the granary. `RoundTripTicks` gains the ring walk; `gather_yield` re-derives up; the rigs re-read.

## 4. The hunter hunts in the woods (§3 of the slice) — not started

Lodge → a forest tile within `hunt_walk_tiles` (6) of the lodge → `hunt_ticks` there → back to the
lodge with the meat (one visible tick on it) → the next hunt, or home when the lodge is full.
`meat_yield` set by the rig and only by the rig (`hunting.md §7`).

## 5. Stated, not built

- **The forester already tends in its ground** — walks to a marked tile, plants a bare one (12
  ticks) or fells a wooded one (4). *Pruning* has nothing to act on until mature trees exist (a
  later view slice); nothing to build here.
- **Builders maintaining** is Phase 5's condition-and-maintenance item (the D65 reversal).

## 6. Measurement

Twelve fixture seeds × fifty years (alive / peak / starved) after each trade, against D383's
**154 / 202 / 36**; the rigs (`AFisherOutEarnsAForagerPerTickWorked`, `AHunterOutEarnsAFisherPerTickWorked`)
re-read after §3 and §4; the pins re-pinned with the reason. Filled in per commit.

| | alive | peak | starved | notes |
|---|---|---|---|---|
| D383 | 154 | 202 | 36 | |
| §2 woodcutter stint | 155 | 202 | 42 | the stint alone read 158 / 204 / 25; ending it at the shortfall (the honest rule) 155 / 202 / 42 — ±10 across twelve seeds is the noise band trap 51 records |

## 7. Definition of Done

Each trade: its rule in `BehaviorSystem`, its derivation moved in `VillageEconomy` with the shipped
number it moves, its guard red-checked, its card sentence, its measurement in §6, and the docs
(`forests-and-gathering.md §3.3`, `hunting.md §6–7`, `wood-fuel-and-tools.md §5`, `DESIGN.md §4/§6/§7`,
handoff) in the same commit. A windowed shot of a forager in the ring and a hunter in the woods,
looked at once; Joe plays and calls the look and the ladder.
