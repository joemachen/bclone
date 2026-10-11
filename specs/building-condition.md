# Spec: Building condition and maintenance — wear on a derived clock, the builders repair

**Decision:** D65 (2026-08-01, *repair after damage, no decay* — **reversed by this spec**, as D353 scheduled and §4/§5
already say), D353 (2026-09-11, Phase 5 names *decay and maintenance*, condition *derived from built-at and
last-repaired ticks*), D546 (2026-10-10, calls 5 and 6: *bridges first, wear later*; an unrepaired wooden deck *slows,
then closes*), D552 (2026-10-10, this spec and its measurement).
**Status:** 🔨 **the sim is built — commits 1 and 2 of 3 on `slice/maintenance` (D554, D555, 2026-10-10/11); the view
(commit 3: worn fills, card lines, the probe's `worn:` line) is next.** Commit 1: the stamps, `ConditionRules`, the rows'
three years and the six keys, the hash and save format 5. Commit 2: the season's walk, repair sites, *closed* per kind,
the wooden bridge's slow and close (§4.3–4.5 *as built*). Joe's seven calls answered as recommended (D553: *"yes to
all"*). Guards: commit 1's 11 mutants / 13 reds / no zeros; commit 2's **22 mutants / 31 reds / one zero, equivalent and
written down** (§9). The goldens moved twice, each proven (§5, §9). Measured first (§8) and on the build (§8.5): **dead
valleys 21 → 21** of 100, and not one ruin in 43 valleys × 50 years. **Not yet played by Joe.**

---

## 1. Why this exists, and why it reverses D65

Joe (§5 nomads, 2026-09): *"we need to eventually add that the building profession also takes care of building
maintenance and add a slow decay function to all buildings with visible differences in decay levels."* And D64:
builders *"build and maintain everything"*; D546 call 6 for bridges: *"slows, then closes"*.

Three things wait on it, in §4 and §5:
- **Wooden bridges wearing** — stone's second payoff (*"it lasts"*, D546 call 2) does not exist until wood wears.
- **House tiers** (D206): winter-accelerated decay is the axis the Wooden Cabin → Stone Cottage ladder is priced on.
- **Nomads and the dead-village revival** (§5): *ruins you inherit* need a decay model to be ruins.

**D65 refused decay on D37's argument** — *a decay timer is a tax the player answers with babysitting rather than with a
decision* (§1.2, §2.3) — and this spec has to beat that argument, not wave at it. It does so with three rules:

1. **Nobody clicks.** Repair is the builders' own errand (D64: *"gives the hut work between projects"*). The player's
   levers are the ones that already exist — builders, logs, the build queue's order, demolition — and no new button
   (§1.2: *systems should reduce babysitting, not add it*).
2. **Nothing is lost that the player did not watch wear for years.** A building is *worn* (drawn so, said on its card,
   repair queued) for years before it *fails*, and failing for years more before it is a *ruin*. §0.1: *visible on the
   map, never unrecoverable before you understood it*.
3. **No hidden multiplier.** Four named states, and the only effects are the ones a sentence can say: a failing
   wooden deck slows; a ruin is closed (§1.1, D357's refusal of *passive home-tick benefits*).

**Pillars:** §1.1 legibility (a worn roof is a thing on the map with a reason on its card), §1.5 generational time (a
granary your grandparents raised shows it), §2.3 pressure traceable to a decision (a village that never fells enough
wood watches its buildings fail, slowly), §0.1 no unforeseeable punishment.

---

## 2. What already exists to copy

| Need | Already here | Where |
|---|---|---|
| a built-at tick | `Bridge.RaisedAtTick` (hashed, saved as `raised_at`; a stone rebuild resets it), `TownHall.RaisedAtTick` (saved, **not hashed**) | `World/Bridge.cs`, `World/TownHall.cs:86`, `Core/SimWorld.Bridges.cs:395–402` |
| a job the builders walk out to, with materials and work ticks, that is not a new building | `ConstructionSite` with `Demolishing` / `MovingFrom` / `RebuildsBridge` — one site class, four meanings | `World/Construction.cs:548` |
| the queue and who works it | `BuildQueue()` (rank, then id; demolitions first), `NextSiteToServe`, `BehaviorSystem.WorkTheSite` | `Core/SimWorld.cs:9003`, `Systems/BehaviorSystem.cs:1442` |
| builders hired only while something is unfinished | `LabourQuota.BuildersWanted` | `World/LabourQuota.cs:1507` |
| a fraction of a recipe | `demolition_work_percent` 50, `demolition_returns_percent` 50, `RefundFor(recipe)` | `Core/SimWorld.cs:9620` |
| a per-season sim hook | `ClockSystem` narrates the season's turn and closes the bridge year | `Systems/ClockSystem.cs:67, 91` |
| *never strands* | `WhatLosingTheDeckWouldCutOff` (the wall-off sweep asked with the deck removed) | `Core/SimWorld.Bridges.cs:429–449` |
| a derived index keyed on a generation counter | `_deckCost` / `RebuildTheBridgeIndexIfStale` on `StandingGeneration` | `Core/SimWorld.Bridges.cs:104–154` |
| the calendar in integers | `TicksPerSeason`, `TicksPerYear`, `SimClock.IsWinter` | `Config/SimConfig.cs:5808–5832` |
| a card line from a tick | the town hall's card turns `RaisedAtTick` into a year | `Main.Cards.cs:1875` |

⛔ **Not the shape to copy: tool wear.** `Villager.ToolUses` is counted down one use at a time (`SimWorld.BeginWork`).
§4's rule for buildings is the opposite — *derived from built-at and last-repaired ticks, never a counter decremented
per tick* — and CLAUDE.md's rule says why (nothing derivable is rebuilt per tick).

---

## 3. Joe's calls — ANSWERED (asked and answered 2026-10-10, D553: *"yes to all. commit"*)

Every recommendation below is the decision. Kept in the asked form so the alternatives stay on record.

1. **The reversal, and its shape.** Decay on a derived clock, repaired automatically by the builders from the hut, no
   click (§1's three rules). **Recommended: yes.** Alternative: keep D65 for buildings and give bridges alone a wear rule
   (the nomads entry then stays blocked).
2. **What wears.** **Recommended: everything with timber in it** — homes, workplaces, stores, the library, wells,
   wooden bridges; **the town hall exempt** (civic, a singleton, 120 stone) and **the stone bridge never** (D546 call
   2). Alternative: homes exempt (a family keeps its own roof) — but a home is the one building whose wear the cold
   model can say something about, and *"the roof is worn"* is the most legible sentence in the set.
3. **The rates** (the measurement says they are about feel, not survival — §8.4). **Recommended: worn at 8 years,
   failing at 12, a ruin at 20 unrepaired; a winter wears half a season extra** (`winter_wear_extra_percent` 50, so a
   wooden thing is worn at about 7 calendar years). The first homes, raised in Year 1, turn worn around Year 8 — well
   past the founding race (D538) and about when the village first holds 10 (D547).
4. **What failing and a ruin do.** **Recommended: *worn* and *failing* change nothing but the look, the card and the
   queue; a *ruin* is closed** — a workplace takes no workers, a store takes nothing in (what is inside can still be
   fetched), a home is no warmer than outdoors, a bridge is not walkable — and **a ruin stays on the map** until
   repaired or pulled down (nomads). The bridge alone has a graded middle, because Joe already called it: a failing
   deck **slows** (§4.4). Alternative: graded penalties on buildings (fewer seats, a colder home) — §1.1 argues against
   the invisible kind, and *closed* is a sentence where *works at 73 %* is not.
5. **The repair's place and price.** **Recommended: a repair site enters the build queue behind every funded new site
   and ahead of nothing the player marked**, at **25 % of the recipe's logs** (rounded up, at least 1) and **25 % of its
   work ticks**; **no stone** — wear is the timber's, repairs are carpentry, and the unattended valleys hold **zero stone
   for fifty years** (§8.3), so a repair that wanted stone would never be served there. A ruin is repaired at the same
   price (it was queued when it turned worn; the queue never lost it). Alternative: repairs ahead of new sites (a
   village that mends before it grows).
6. **Who repairs.** **Recommended: the builders, from the hut, in this slice**; the builder's hut growing into a
   *workshop* with a carpenter trade and the builder's hammer (D391 deferred the tool because its bonus would fall on a
   site's ticks) is **a follow-on slice** with its own spec. Alternative: all of it at once.
7. **A ruin's demolition.** **Recommended: pulling down a ruin returns nothing** (`demolition_returns_percent` applies to
   what still stands, and a ruin's timber is gone). Alternative: the normal 50 %.

Settled without asking, stated so he can overrule: condition is never a per-tick counter; stamps are hashed; a save
format bump (no migrations, D507); the view draws worn in steps rather than with cracks or smoke; a worn or failing
thing narrates once per level, never per season.

---

## 4. The rule

### 4.1 Two stamps, one derived number

Every standing thing that wears carries **`RaisedAtTick`** (the tick it was finished — a stone rebuild of a bridge already
resets it) and **`RepairedAtTick`** (0 = never). Its **wear age** at tick `now` is

```
since      = max(RaisedAtTick, RepairedAtTick)
age        = now − since                                       (ticks)
winters    = winter seasons begun in (since, now]              (integer, from the calendar)
wear ticks = age + winters × TicksPerSeason × winter_wear_extra_percent / 100
```

in integers, closed-form from `TicksPerSeason` and `TicksPerYear` — nothing is accumulated and nothing is stored but
the two stamps (D335: a level restates the stamps; it is not a second fact). `Condition.Of(raisedAt, repairedAt, now,
row, config)` is a pure function the sim, the tests, the probe and the view all call.

### 4.2 Four states, from the row

The building row says how it wears, in **years of mild weather**: **`wears_in_years`**, **`fails_in_years`**,
**`ruined_in_years`**, each a threshold on wear ticks (`× TicksPerYear`); **0 = never wears** (the stone bridge, the town
hall). The validator refuses a row whose three are not `0 ≤ wears < fails < ruined` or all 0.

| State | Reached when | The map | The card | The sim |
|---|---|---|---|---|
| **sound** | — | as today | *raised Spring, Year 3* | — |
| **worn** | wear ≥ `wears_in_years` | fill one step darker and duller | *worn — the builders will see to it; fails in about N years without repair* | **a repair site is raised** (§4.3); narrated once |
| **failing** | wear ≥ `fails_in_years` | two steps | *failing — closes in about N seasons without repair* | a wooden bridge **slows** (§4.4); narrated once |
| **ruin** | wear ≥ `ruined_in_years` | three steps, the edge broken | *fallen in — closed until repaired or pulled down* | **closed** (§4.5); narrated once |

Repair sets `RepairedAtTick = now`: the thing is sound again and the clock starts over. Defaults for the shipped rows
(data, overrulable): every timber building 8 / 12 / 20; the stone bridge and the town hall 0 / 0 / 0.

### 4.3 The repair is a site, and the builders' errand

At each **season turn** (`ClockSystem`, where the bridge year already closes) the sim walks what stands — a dozen to a
few dozen things, four times a year, never per tick — and for each one whose state *this* season differs from its
state *a season ago* (both derived, nothing stored) does the state's one thing. For *worn*: **raise a repair site** on
the building's tile — a `ConstructionSite` with **`Repairing`** set, the row's materials × `repair_cost_percent` 25 in
**logs only**, the row's work × the same, no clearing (the ground is the building's), **not an obstacle** (a bridge
site's rule: the building keeps working while it is mended). It enters `BuildQueue()` with the rank of a site marked
now (behind every funded site, ahead of nothing the player ranked later), `BuildersWanted` hires for it as for any
unfinished site, and `WorkTheSite` serves it the way it serves everything: fetch logs from the nearest store, walk,
work. **Finishing it stamps `RepairedAtTick`** and the site retires. One repair site per building at a time; a
building pulled down or moved takes its repair site with it (the demolition path already cancels a relocation's).

- **A repair nobody can fund** (no logs anywhere) waits like any site waiting on materials and holds no hands —
  `SomethingIsMarkedAndNobodyCanRaiseIt`'s existing rule; the card says *waiting on 7 logs*.
- **The player may still ▲ Sooner / ▼ Later** a repair site like any other (D105), and may cancel it (it is re-raised
  next season if the thing is still worn — the sim does not nag, it re-asks once a season at most).
- **A ruin's repair is the same site** (it has stood in the queue since *worn*); finishing it reopens the building.

> ⭐ **As built (D555, commit 2):**
> - **The repair site is the demolition site's shape, raised by `SimWorld.TendWhatStands()` from `ClockSystem` on the
>   season's turn** (`Core/SimWorld.Condition.cs`), comparing this season's derived state with last season's — nothing
>   stored, nothing per tick (`ConditionWalks` counts the walks; a guard reads four a year).
> - **It takes the building's exact position and facing** (a house's from `HomeFootprintOf`, whose origin is the anchor,
>   not the front point the household keeps), **and it is not an obstacle** — `StandingShapes` and `SomethingOverlaps`
>   skip it as they skip a bridge's site. Listed as one, it took over the tiles' owner in the occupancy index and the
>   path opener opened the site's shape instead of the building's: a granary under repair was unreachable to everyone
>   fetching food from it (`TheMarketKeepsLardersFromRunningDry` read a 15-tick dry spell against a round trip of −2).
> - **Finishing it mends everything standing on the tile**, not the first thing found: the market is a store and a
>   workplace on one tile, and stamping only the store left its workplace half worn — a fresh repair every season.
> - **A repair whose logs are in store spares one hand when half the spare hands rounds to nobody** — D453's exception
>   for a begun site, extended (`LabourQuota`, `SimWorld.AServableRepairSite`). Measured in the fixture's market-off
>   village: at two people, `free / 2` rounded to zero builders for thirty years while eleven repairs waited with sixty
>   logs in store, and the last two froze in their fallen-in house fifteen years before they would have aged out. A
>   repair owes a tenth of a hut; one spare hand mends it and goes back. The food gate still comes first.
> - A demolition, a relocation or a bridge's pulling-down cancels the repair on its tile; a repair site never waits on
>   clearing (`FirstTileStandingUnder`), so a bridge's repair on a wooded bank is served.

### 4.4 The wooden bridge: slows, then closes (D546 call 6)

- **Failing: slows.** ⚠️ The deck byte cannot do it: `deck_tile_cost` is validated to 1..`BaseTileCost` (10) and the
  wooden deck is already 10, so there is no slower byte. **Recommended (decided while speccing, to prove in the build):
  the leg planner charges a leg over a failing deck `failing_deck_slow_percent` 150 of its ticks** — routes do not
  change (nobody re-routes round a bridge they can still cross; wood is already grass's price), the walker visibly
  slows on the deck, and the tick count is the one thing the eye sees. The field's `_entryCost` is the paths' price and
  stays theirs.
- **Ruin: closes.** The deck byte goes to 0 for its water tiles — not walkable — through `StandingChanged()` so routes
  are forgotten (the rule every deck change already follows). **Except:**
  - **never with anybody on it** — a deck with a villager on its tiles stays open and the closing is tried again next
    season's walk (call 6: *never closes with anybody on it*);
  - **never stranding** — if `WhatLosingTheDeckWouldCutOff` names a building or home, the bridge stays open at the
    failing price until repaired; the card says *failing — the only way to the Ashfords' house, so it stays open*.
- **Stone never wears** (row 0 / 0 / 0). A wooden bridge rebuilt in stone resets `RaisedAtTick` already and becomes a
  row that does not wear — Joe's call 2, now true.

### 4.5 What *closed* means, per kind

| Kind | Closed is | Already-existing shape |
|---|---|---|
| workplace | `Places` reads 0; `IdleNote` says *fallen in — waiting on the builders*; workers released | `StaffingOverride`'s path |
| store | accepts nothing; what is held can still be fetched; `NearestStoreAccepting` skips it | `Stocking` closed |
| home | the household stays; the roof shelters nobody — their cold is outdoors' (`exposure_days_outdoors`) and the fire is not lit | `Household.HasHome` stays true; the shelter check reads condition |
| library | records are not written or read (keeper decay is `tech-tree.md §7c`, not this) | — |
| well | draws no water | — |
| bridge | §4.4 | `_deckCost` 0 |

A ruin is **not** demolished by the sim: it stands until the player pulls it down (returning nothing, call 7) or its
repair is served. That is the nomads' inheritance.

> ⭐ **As built (D555): *closed* is a flag the sim sets at the transition, not a per-read derivation.** A bridge's closing
> has exceptions judged at the season's turn (somebody on it, the only way somewhere), so the outcome is a decision made at
> a moment, like `RepairedAtTick` — a fact, hashed sparsely (tags `0x60`–`0x65`) and saved, cleared by the mend. Every kind
> closes the same way so one word means one thing: `Workplace.Closed` (its `Places` read 0, its workers released, the
> idle note says *fallen in*), `StoreBuilding.Closed` (`Accepts` false; what is held is still given), `Household.HomeClosed`
> (`ShelterAt` is *Outdoors*, the hearth lights no fire), `Well.Closed` (`NearestWell` skips it), `Library.Closed` (no record
> written or read), `Bridge.Closed` (its water is water again). ⚠️ **Closing a bridge is a standing change of its own**: a
> bridge posed a ruin twenty years back has no transition this season, and without it the deck index went on selling a
> deck that was no longer there.
>
> ⭐ **A closed store still gives** (D553 call 4): `StoreBuilding.Gives(goods)` is `Accepts` without the ruin, and every
> path that asks *who holds this* — the village's counts (`InStores`, `AnyStoreHolding`), `NearestStoreHolding`, the
> builder's pickup, the firewood shop, `FoodItWillGiveUp` — reads it. They had all asked `Accepts`, so the first build
> hid a fallen-in granary's food from the people beside it; its guard had checked `Stockpile.TryTake` and passed. The
> player's `Stocking.Closed` still keeps what it has, as it always did.

### 4.6 Not in this slice, said so

- The **workshop, the carpenter and the builder's hammer** — follow-on (call 6).
- **Fire, storm, blight** — §4's own sentence (*telegraphed, legible, answerable*) before any of it.
- **The library's record decay and keeper** — `tech-tree.md §7c`, lands with its own spec.
- **House tiers** — read `wears_in_years` per row when they come (a stone cottage wears slower); their own spec.
- **Mild-weather vs winter on the house's firewood** — D206's 6–8× burn axis, untouched.

---

## 5. What changes meaning

| Where | Today | After |
|---|---|---|
| `Workplace`, `StoreBuilding`, `Library`, `Well`, `Household` (its home), `Bridge`, `TownHall` | no raise stamp (bridge, town hall excepted) | `RaisedAtTick`, `RepairedAtTick` — set in `RaiseFinished` / `Complete` / `FinishABridge` / the home's raise, and in the warm start (t0) |
| `BuildingRow` | — | `wears_in_years`, `fails_in_years`, `ruined_in_years`; validated |
| `ConstructionSite` | `Demolishing`, `MovingFrom`, `RebuildsBridge` | + `Repairing`; recipe = row × `repair_cost_percent`, logs only |
| `ClockSystem` season turn | narrates, closes the bridge year | + the condition walk (§4.3) |
| `TravelCostField` / `PlanLeg` | a deck prices at its byte | + a failing deck's leg at 150 % |
| `IsWalkable` / `DeckOn` | a standing bridge's water is a deck | a *ruined* bridge's is not, unless occupied or stranding |
| `Places`, `NearestStoreAccepting`, the shelter check, the library, the well | — | read `Condition.Of` for *ruin* |
| `StateHash` | bridges mix `RaisedAtTick` | every stamp mixed, **appended at the end of its block** (the hash's own rule), the town hall's included |
| `SaveGame.Format` | 4 | **5** |
| `data/sim.config.json` | — | `winter_wear_extra_percent`, `repair_cost_percent`, `failing_deck_slow_percent`, the rows' three years |

**Goldens move once** — every village stamps its workplaces — proved by outcomes on the fixture and the survival arm
(§8.5) before they are re-taken, as D463 did. Nothing else about today's villages changes until Year 7–8, so the
fixture's 50-year hashes are the ones to watch and explain.

> ⭐ **As built (D554, commit 1):** the six live goldens (`FarmGoldenTests` ×2, `StockLimitTests` ×2, `SkillTests` ×2)
> moved for the stamps' mix and nothing else — **proven by switching the thirteen new `Mix` lines off: all 22 golden
> guards passed on the old values.** Nothing in the sim reads a stamp yet, so commit 2's behaviour (the first repair at
> Year ~7) will move the fifty-year hashes a second time, with its own reason. The stamps are **defaulted `ulong`s, not
> `required`** — `required` would have broken 20 object initialisers in the tests and 12 in `src`, and `Bridge.RaisedAtTick`
> already has this shape. The three years live on every shipped row through `SimConfig.WithTheShippedWear`, one place
> fed by three keys, exempting the stone bridge, the town hall and the two free instant rows (the stockpile and the
> builder's hut); a modder's `buildings` array is taken as typed and wears only where it says so.

---

## 6. Saved, hashed, drawn, told

- **Save:** `raised_at` and `repaired_at` on every kind above (the bridge's `raised_at` exists); a repair site saves
  `repairing`. Format **5**; older saves refused in words.
- **Hash:** the stamps, at the end of each kind's block; `Condition` itself never (derived, D335).
- **Drawn** (view, `DrawFootprint`'s fill): three steps of darker-and-duller per state, constants beside the colours;
  a ruin's edge broken (dashed). A failing deck's planks thin. Nothing animates, nothing is random (the fan's rule).
- **Told:** every card gets one condition line (§4.2's words); a repair site's card is a site's (*repairing the
  granary — waiting on 7 logs*); a stop narrates each level once per building (*"The granary is showing its age — the
  builders will see to it when they can."* / *"The wooden bridge is failing — it slows the crossing, and closes in about
  2 years without repair."* / *"The old granary has fallen in."*), `LogCategory.Building`.
- **Probe:** `worn:` poses `Condition.Of` at the four thresholds, a winter's extra, a repair's reset, and a never-wears
  row (the view has no tests, D11) — and the card's line for each state through `TheCardsHoldTheirShape`.

---

## 7. Edge cases and failure modes

- **A per-tick walk** — refused by construction: the condition walk is on the season turn; the view and cards read the
  pure function when they draw. ⚠️ The suite's clock is the instrument (CLAUDE.md): measured before and after.
- **A repair that cannot be funded holds a builder** — must not: it is a site waiting on materials, and the existing
  rule leaves it. Guarded.
- **Repair materials deducted from a building's own stock** — a woodcutter's hut holds logs in its yard; the repair
  fetches from stores like a build does, and never from the building's own local store (it is the thing being mended).
- **A worn building the player moves** (`MovingFrom`): the stamps travel with it (it is the same timber); a stone
  rebuild of a bridge is a new deck and resets.
- **A home's ruin and the cold model** — a ruined home is *no shelter*, and the founding race (D538) is Years 1–2; the
  first home cannot be a ruin before Year ~20. Measured anyway (§8.5).
- **A bridge closing under somebody / stranding** — §4.4; both guarded; the strand case stays open and says so.
- **Two sites on one tile** — a repair site and a demolition or relocation of the same building: demolition wins and
  cancels the repair; a relocation carries the stamps and cancels the repair (it is re-raised if still worn).
- **The warm start and fixtures** — buildings raised free at t0 stamp 0; the fixture's 50-year villages are the first
  to show wear in a golden.
- **Determinism** — the walk is in the sim's tick order over the standing lists in id order; the only new RNG use is
  none.
- **Loading** — format 5 only; a format-4 save is refused in words (D507: no migrations).
- **Found while building (D555):** the market's two halves on one tile (mend everything on the tile); the repair site as
  an obstacle taking over the index's owner (not an obstacle); a two-person village never affording a builder (one hand
  for a servable repair); a bridge closing without a transition (a close is a standing change); a house's repair site
  posed at the front point instead of the anchor (the house's own footprint); a closed store hiding what it held, because
  the fetch paths asked `Accepts` for *who holds this* (`Gives`). Each has a guard.

---

## 8. Measured before typed (D552, 2026-10-10) — `tools/harness/ZzMaintenance.cs`, and Joe's saves

### 8.1 What an unattended village stands, and how old it is

43 valleys (30 `shipped`, 13 `every` — the lodge, the fishery and a farm raised free), the played opening, fifty years.
A building's age is the first year the harness saw it standing (nothing in the sim stamps a raise yet).

| | Year 20 | Year 30 | Year 50 |
|---|---|---|---|
| **shipped** — people (median, living valleys 26 of 30) | 9 | 9 | 8 |
| standing buildings (median / p90) | 5 / 6 | 7 / 7 | 7 / 8 |
| logs in those recipes (median) | 140 | 200 | 200 |
| share of buildings ≥ 8 / ≥ 12 / ≥ 20 years old | 98 % / 97 % / 0 % | 88 % / 76 % / 76 % | 99 % / 99 % / 97 % |
| **every** — people (median, 9–10 of 13 living) | 10 | 14 | 19 |
| standing buildings (median / p90) | 8 / 8 | 10 / 11 | 11 / 13 |
| logs in those recipes (median) | 230 | 290 | 320 |
| share ≥ 8 / ≥ 12 / ≥ 20 years old | 100 % / 100 % / 0 % | 90 % / 80 % / 80 % | 95 % / 94 % / 87 % |

At Year 50 the shipped valleys stand homes (98 over 26 valleys), a gatherer's and a woodcutter's hut each, and a
warehouse or a granary — **almost everything a village stands is older than 8 years by Year 20, and older than 20 by
Year 50.** With no maintenance, by Year 30 three quarters of every village is past 20 years.

### 8.2 What Joe's played villages stand (20 saves, the latest autosave of each)

| village | year | people | standing | of which homes | logs ever felled |
|---|---|---|---|---|---|
| stonebridge-sparrow-dale-71 | 18 | 8 | 9 | 2 | 519 |
| millbrook-thorn-elm-91 | 27 | 10 | 11 (2 bridges) | 2 | 717 |
| hollowmere-poppy-ivy-34 | 28 | 10 | 16 (a bridge) | 2 | 714 |
| ravenscar-loam-harrow-77 | 44 | 8 | 18 | 3 | 1,065 |
| fernhollow-ember-lantern-38 | 101 | 17 | 24 | 5 | 1,322 |
| millbrook-amber-ivy-69 | 118 | 27 | 25 | 8 | 1,610 |

A played village stands about twice what the harness does — 16–18 buildings by Year 30–45, 24–25 by Year 100+ — and
it is the number the rates are priced against (D447: a harness is not a player).

### 8.3 The wood flow — and the trap in reading it

| | Years 1–5 | Years 10–20 | Years 20–30 | Years 40–50 |
|---|---|---|---|---|
| logs felled a year (shipped, median; p10–p90) | **90** (88–97) | 6 (3–8) | 16 (5–20) | 9 (4–16) |
| logs felled a year (every) | **92** (84–102) | 3 (2–9) | 18 (16–20) | 14 (7–21) |
| logs in store (shipped, median) | 202 | 200 | 204 | 202 |
| stone in store (both arms, every year) | **0** | 0 | 0 | 0 |

⚠️ **Felling is limit-driven, not capacity-driven.** From Year 5 the logs sit at their 200 limit and the woodcutter
idles, so *"repair demand as a share of logs felled"* (37–168 % at the candidate rates) reads as alarming and means
nothing: one woodcutter fells **~90 logs a year** when something wants them (the opening), and a village that wants 5–20
more a year for repairs simply fells them. The honest comparison is to that capacity. ⚠️ **No unattended valley holds
any stone, ever** — a repair priced in stone would stall for fifty years there and turn every building into a ruin,
which is why call 5 recommends logs only.

### 8.4 Repair demand at the candidate rates (steady state: logs in standing recipes × cost ÷ wears-in years)

| village | logs standing | 8 yrs / 25 % | 10 yrs / 25 % | 15 yrs / 25 % | 10 yrs / 50 % |
|---|---|---|---|---|---|
| harness shipped, Year 50 (7 buildings) | 200 | 6 / yr | 5 | 3 | 10 |
| harness every, Year 50 (11 buildings) | 320 | 10 | 8 | 5 | 16 |
| Joe's Year-44 village (18 buildings, ~550 logs) | ~550 | 17 | 14 | 9 | 28 |
| Joe's Year-118 village (25 buildings, ~700 logs) | ~700 | 22 | 18 | 12 | 35 |

Against one woodcutter's ~90 logs a year, **the recommended rate (8 years, 25 %) costs a mature played village about a
fifth of one woodcutter's year**, and the builders about 2–3 repairs a year at 10 work ticks each (a hut's 40 × 25 %)
plus the walk — a tenth of one builder's year. **So the rate is a call about feel — how often the player sees a worn
roof and a builder mending it — not about survival.** 50 % of materials doubles it and is still under half a
woodcutter; 15 years makes wear a thing seen twice in a lifetime.

### 8.5 To measure in the build, before any number is typed into `data/sim.config.json`

- **Survival:** 100 shipped valleys × 50 years, lost valleys and people alive at 50, before and after (D16's budgets,
  the D420 harness `arms.sh`); the acceptance line is the survival guard's 25 % dead, and *no new Year-1 death*.
- **Logs spent on repair a year and builder ticks on repair a year**, by decade, on the same valleys.
- **How many things are worn / failing / ruined at Year 30 and 50** — in an unattended village the answer should be
  *almost none ruined* (the builders mend), or the queue rule is wrong.
- **The suite's clock**, back to back (D426).

> ⭐ **Measured on the build (D555, 2026-10-11).**
>
> **Survival** (`tools/harness/arms.sh base`, 100 shipped valleys × 50 years, commit 1's tree and this one back to back):
>
> | | alive at 50 | dead valleys | peak | starved | froze |
> |---|---|---|---|---|---|
> | before (D554 — nothing wears) | 612 | 21 | 1,007 | 347 | 52 |
> | after (D555) | **590** | **21** | 996 | 370 | 55 |
>
> Dead valleys unchanged — the acceptance line holds; −3.6 % alive. *No new Year-1 death*: nothing wears before Year 7.
> The cost is in the tail of a dying village: valley `every 10` died by Year 40 before and by Year 30 after (its last logs
> went to repairs with no woodcutter to replace them); a village down to two at Year 110+ can no longer keep its roof
> (D143's endgame, a little earlier). The twelve-seed timber-gate scan at 120 years, gated / free alive, reads 37 / 46
> against D429's 36 / 28 — the gate kills nothing; seed 1's free arm flipped, so `TimberGatesGrowthWithoutStoppingIt`
> moved to seed 9 (10 / 13).
>
> **The maintenance itself** (`ZzMaintenance.cs`, 30 `shipped` + 13 `every` valleys, medians over every valley):
>
> | | Year 10 | Year 20 | Year 30 | Year 40 | Year 50 |
> |---|---|---|---|---|---|
> | mended in the decade (shipped / every) | 5 / 7 | 5 / 8 | 10 / 13 | 8 / 13 | 8 / 13 |
> | fallen in, ever | 0 | 0 | 0 | 0 | 0 |
> | closed now | 0 | 0 | 0 | 0 | 0 |
> | repairs waiting (max over valleys) | 0 | 1 | 8 † | 8 † | 8 † |
>
> † one dead valley's leftovers (`every 10`, empty from Year 30: the walk runs only while somebody lives). **Not one
> ruin in 43 valleys × 50 years: the builders keep up**, so the queue rule is right (*"almost none ruined"* was the bar).
> Felling rises with it — the shipped median 6 → ~10 logs a year in Years 10–20 and 16 → 23 in Years 20–30 — the
> woodcutter working instead of idling at the limit, as §8.3 predicted.
>
> **The suite's clock moved, and it is the villages, not the code.** Back to back: commit 1 **4m24**, commit 2 **7m27**
> (1601 → 1621 tests). Per test, the long village runs carry it (`NoHouseholdEverHoldsLogs` 29 s → 92 s in the parallel
> run). Taken apart on that test: with the season's walk short-circuited it runs **8.4 s against commit 1's 8 s** — the
> new code costs nothing per tick; `AServableRepairSite` is asked **16 times in 100 years**; `DeckIsSlow` switched off
> changes nothing. The difference is behaviour: commit 1's fixture village dies out around Year 190 and commit 2's
> **lives all 300 years** (its roofs mended), so the run simulates a living village for a century longer, and in every
> run builders and woodcutters now work where they idled. One new guard was dear for itself (`ARuinedDeckIsNotClosed
> WhenItIsTheOnlyWaySomewhere`, 46 s, a `CanBuildAt` over every tile across the river) and asks open grass first now.

---

## 9. Guards (TDD, red first, red-checked and counted — D326)

> ⭐ **Commit 1 (D554) — `ConditionTests` (9), `MaintenanceTests` (5).** Mutants, each the one line it names: winter's extra
> dropped **1 red**; `RepairedAtTick` not read **2** (⚠️ the first pose left the parameter unused and did not build — a
> mutant that does not build scores zero, D450; re-posed as `repairedAt * 0`); the worn threshold `>` for `>=` **1**; a well's
> stamp not set at the raise **1**; a workplace's raise left out of the hash **1**; a home's repair left out of the hash **1**;
> the save format not bumped **1**; the shipped rows not given the years **2**; the row check removed **1**; the key check
> removed **1** (⚠️ scored **zero** at first: the rows the keys write refuse the same village with a sentence that also says
> *rising*, so the guard now asserts the key's name); a workplace's stamps not saved **1**. **11 mutants, 13 reds, no zeros.**

> ⭐ **Commit 2 (D555) — `RepairTests` (13), `BridgeConditionTests` (6), `HousesAreBuiltTests` (+1), `ObstacleTests`' union.**
> Mutants (`scratchpad/mutate2.py`), each one line: the walk per tick **4 reds**; a repair priced in stone **6**; a repair
> ahead of a funded site **1**; a ruin still staffed **1**; a deck closed with somebody on it **1**; the strand check
> skipped **1**; a workplace's `Closed` out of the hash **1**; a home's **1**; the labour exception removed **1** (⚠️
> **zero** at first — the targeted classes never posed a village that rounds to nobody; now
> `OneSpareHandIsKeptForARepairWhoseLogsAreInStore`); the site left in the occupancy index **1**; `Mend` stopping at the
> first thing **1** (⚠️ **zero** at first; now `AMendedMarketIsMendedWhole`); a failing deck not slowed **1**; a closed deck
> still in the index **2**; a closed home still shelters **1**; a closed store still accepts **1**; `Mend` leaving the flag
> **1**; a store's `Closed` not saved **1** (⚠️ **zero** at first — the save pose closed only the home; now all three); a
> demolition leaving the repair **1**; a ruin never closed **2**; the village's count reading `Accepts` **1**;
> `AnyStoreHolding` reading `Accepts` **1**. **22 mutants, 31 reds, one zero kept:** `SomethingOverlaps` not skipping a
> repair site scores **0** and is **equivalent** — a repair site's shape is the building's own, so whatever overlaps it
> overlaps the building, and a bridge's repair stands on a bank `SomethingStandsAt` already refuses. The skip is there to
> agree with `StandingShapes`, whose own mutant scores 1. ⭐ **The goldens moved a second time, for the walk alone:** with
> `TendWhatStands` short-circuited the six old values pass (65 of 65 golden and determinism guards green).

`ConditionTests` (pure): the thresholds in wear ticks; a winter adds exactly its extra and a summer none; a repair
resets to sound; a 0 / 0 / 0 row never wears at a thousand years; the validator refuses `wears ≥ fails`.
`MaintenanceTests` (sim): a worn hut raises exactly one repair site at the season turn, behind a funded new site, pricing
25 % of its logs and none of its stone; a builder fetches, works and finishes it, and the hut is sound with
`RepairedAtTick` set; a repair with no logs anywhere holds no builder; a ruined workplace has no places and an
`IdleNote`; a ruined store accepts nothing and still gives; a ruined home shelters nobody; a demolition cancels the
repair; the stamps are hashed (red with one unhashed) and saved (format 5; save → load → hash equal; a year on matches).
`BridgeTests` (extended): a failing wooden deck's leg takes 150 %, the route unchanged; a ruined deck is not walkable;
not closed with a villager on it; not closed when it would strand, and the card says so; stone at a thousand years is
sound. The probe's `worn:` line; `TheCardsHoldTheirShape` poses the four states.
Mutants to count (each must redden something): the walk per tick instead of per season (a clock guard); winter's extra
dropped; `RepairedAtTick` not read; repairs priced in stone; a repair ahead of a funded site; a ruin still staffed; a
deck closed with somebody on it; the strand check skipped; a stamp left out of the hash; the format not bumped.

---

## 10. Definition of Done

- [x] Joe's seven calls answered (D553: *"yes to all"*) — §3 and §4 as written.
- [x] §8.5 measured on the build (D555): dead valleys 21 → 21, no ruin in 43 valleys; the keys' comment cites it.
- [x] §9's guards; mutants listed with their red counts, zeros written down — commit 1 ✅ 11 / 13 / none; commit 2 ✅ 22 /
      31 / one equivalent zero, written down.
- [x] Goldens moved once for the stamps (D554) and once for the walk (D555), each proven by switching its cause off; the
      determinism test green.
- [ ] The four checks (CLAUDE.md): ✅ for commits 1 and 2 (suite, view build read, probe, goldens diffed); commit 3 adds
      the probe's `worn:` ✅.
- [x] `DESIGN.md` §4 (the Phase 5 bullet, and D65's §5 item marked reversed-and-built), §6, §7; `specs/bridges.md` §3.1;
      `HANDOFF.md`; this status line made true — in the same commit as the code. *(Commit 3 repeats it for the view.)*
- [ ] Played by Joe: a village of twenty years at 4×, watching a roof turn worn, a builder mend it, and a wooden
      bridge left to fail.
