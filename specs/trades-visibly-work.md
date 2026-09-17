# Spec: Trades visibly work — a woodcutter's stint, a forager in the ring, a hunter in the woods

**Decisions:** D384 (this document). Neighbours: D282 (`fish_ticks` 3 → 10, a pacing change measured on the rig), D288/D293 (the yield rigs), D355 (the steading, the look only), D361/D363 (the food ladder and its floor), D373 (a building visit lasts a tick), D383 (obstacles).
**Status:** ✅ **built (2026-09-16, D384, three commits)** — §2 the woodcutter's stint, §3 the forager in the ring, §4 the hunter in the woods; suite 1170 passing, 0 failing, 2 skipped of 1172. **Unplayed by Joe as of this line.** Owner: Joe + Claude Code.

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

## 3. The forager gathers in the ring (§2 of the slice)

**Rule.** A trip goes home → **a wooded tile of the hut's ring within `gather_walk_tiles` (3) of
the hut** → `gather_ticks` there → home or the granary. `SimWorld.AGatheringTileFor(hut, villager)`
lists the forest tiles of that diamond in row order that nothing stands on, and takes the one a
hash of (villager id, trip count) points at — so one forager spreads over the near ring trip by
trip and two at one hut do not walk in step — passing over a candidate nobody can walk to for the
next in order. ⛔ Never an `Rng` draw: a look must not reshuffle every seed. The hut itself when
the near ring is bald (a bald ring yields nothing anyway), and then the forager still stands on
the hut's own point (D354). The tile is remembered as the errand so the leg is not re-decided
mid-walk (a forester's rule). The yield is still the ring's (`GatherYieldAt`): the tile is where
the forager is seen, not a per-tile larder.

**What it costs, and where it is derived.** `RoundTripTicks` prices the walk into the ring:
(`WalkBudgetTiles` + `gather_walk_tiles`) × 2 + `gather_ticks` = 27 ticks, was 21, so a year holds
12 trips, not 15, and the same target — a gatherer feeds themselves and one dependant — derives
**`gather_yield` 90 → 112** (the fixture derives its own; the shipped file is written at the
derived number). The rigs re-read: forager 649 / fisher 1,034 / hunter 2,010 per hundred ticks
worked against D383's 527 / 820–898 / 1,785 — the rungs 1.6× and 1.9× either side, so `fish_yield`
and `meat_yield` are untouched (trap 30: the ratios are the target).

**Three, not eight.** The ring is eight; a walk to its far edge doubles the trip and re-bases the
floor hard. The key is Joe's to widen.

**Legibility.** *"walking out to the woods near forager's hut 1"*, *"gathering berries in the
woods"*.

**Guard.** `AForagerGathersInTheRingNotOnTheHut` (`TradesVisiblyWorkTests`): over three fixture
years every gathering tick stands on a forest tile within `gather_walk_tiles` of the hut, more of
them off the hut than on it, and on more than one tile (124 ticks on 15 tiles). ⛔ Red with the
walk off: 140 ticks, all on the hut.

## 4. The hunter hunts in the woods (§3 of the slice)

**Rule.** A hunt goes lodge → **a forest tile of the range within `hunt_walk_tiles` (6, half the
range) of the lodge** → `hunt_ticks` there → back to the lodge with the meat and the hide in the
hunter's arms → the meat into the lodge's store, one visible tick on the building (D373) → the
next hunt, or clearing the lodge when it is full (D370, unchanged), or home when the larder
wants what the lodge would not take. `SimWorld.AGameTileFor(lodge, villager)` is the forager's
rule one trade over — the range's forest tiles in row order, the one taken picked by a hash of
the villager and the tick the hunt is decided, ⛔ never an `Rng` draw; the lodge itself when no
woods are in reach. The carry-back reuses `HaulingToFarm` (*carrying a load to my own
workplace's buffer*), dispatched by the workplace's kind on arrival; the hide stays in the arms
until they pass a store, as it always did. The yield is still the range's (`HuntYieldAt`).

**What it costs, and where it is measured.** The hunter is priced by the rig and only by the
rig (`hunting.md §7`); the rig's on-the-job ticks count the carry-back. Re-read: hunter 1,975 per
hundred ticks worked against the fisher's 956 — 2.07× (D383: 1.99×) — so `meat_yield` stays.
A hunter is seen hunting 66 ticks in two years where the re-arm on the lodge gave 185: the walk
out and back is the look.

**Legibility.** *"walking out to the woods"*, *"hunting in the woods"*, *"carrying the meat back
to hunter's lodge 1"*.

**Guard.** `AHunterHuntsAtAForestTileAndBringsTheMeatToTheLodge` (`TradesVisiblyWorkTests`): over
two years every hunting tick stands on a forest tile within `hunt_walk_tiles` of the lodge, none
on it, on more than one tile, and the lodge's store rises only with a hunter standing on it.
⛔ Red with the walk off: 185 ticks, all on the lodge, and the store rising four times with nobody
there.

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
| §3 forager in the ring | 161 | 210 | 34 | the yield derived for the longer trip keeps the floor; the rigs' rungs held (1.6× / 1.9×) |
| §4 hunter in the woods | 161 | 210 | 34 | the fixture has no lodge; the rig read 2.07× the fisher (1.99× at D383) |

## 7. Definition of Done

Each trade: its rule in `BehaviorSystem`, its derivation moved in `VillageEconomy` with the shipped
number it moves, its guard red-checked, its card sentence, its measurement in §6, and the docs
(`forests-and-gathering.md §3.3`, `hunting.md §6–7`, `wood-fuel-and-tools.md §5`, `DESIGN.md §4/§6/§7`,
handoff) in the same commit. ✅ All three built. ⚠️ **The windowed shot was not had:** the only
warm village the game can open is the shipped config forced warm, and it seats nobody (the
loose thread from D383 — four laborers at their homes on Day 8 of Summer); the look is Joe's to
see in play, where he raises the huts himself.
