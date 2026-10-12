# Spec: Nomads — bands that knock, the longhouse, and a dead valley re-founded

**Decision:** D251 (2026-08-28, the town hall *triggers nomads* — **amended by call 1 below: the hall widens them**),
D313 (2026-09-06, the empty-village banner does not stop the game *because nomads may come back*), D353 (Phase 6 names
them), D465/D558 (2026-10-03/11, surnames by hash; the one-surname village *left for the nomads*), D554–D556 (building
condition — the hard prerequisite, built), D563 (2026-10-11, Joe's four calls), D564 (this spec and its measurement).
**Status:** 🔨 **commit 1 of 3 built — the band (D565): sim and view, on `slice/nomads`, local, not played.** Bands
knock, wait at the pile as a huddle, and settle or move on from the card's two buttons; `NomadTests` (23) green and
red-checked (§9); the six fifty-year goldens moved once, proven the knock's alone (§5); save format **6**; the probe's
`nomads:` and `huddle:` lines ✅. Commits 2 (the longhouse) and 3 (re-founding) are next. Joe's four calls answered (§3);
the rest is mine, stated so he can overrule. Measured first (§8) on 100 shipped valleys and Joe's 27 saves: a knock at
Year 10 meets eight people in two housed families (every share rule sends a couple; Joe's villages are one surname from
Year 50); a band of two or four is housed the same year wherever the player has painted, and its cost is food, never a
roof; a re-founding's first winter turns on whether a house stands.

---

## 1. Why this exists

Joe's sketch (DESIGN.md §5, 2026-08-23): *"They come every X years (random with a floor) with Y nomads looking for homes.
User can allow or deny. No harm no foul if denied. If they are allowed, they are immediately unhoused villagers and will
freeze without houses of their own."* Communal housing: *"they do not partner or have children while in it … the communal
house needs its own food and firewood larder."* And the best idea in it: *"If a village completely dies out, and the
player doesn't quit and just waits a few years, a nomad group can come in and revive the village"* — a special foursome
inheriting what stands, *"with the library decayed and some buildings crumbled."*

Three things have been waiting on it:
- **The lull** (Joe, 2026-09-15): *"should the first nomads arrive around year 10? that could be a way to introduce a new
  mechanic and give needed growth at a time when it may start to lull."* The milestone map carries the row at ~10.
- **The one-surname village** (D558). Two founding names and nothing ever brings a third; Joe's Year-88 village was all
  Wright, and every one of his saves past Year 50 is down to one name (§8.2). His call: *wait for nomads*.
- **The dead valley** (D143, D313). An unattended village is *supposed* to die out, and the banner deliberately does not
  stop the game so that *"nomads have something to come back to"*. Until now nothing came.

**Pillars:** §1.4 stories from people (a family with a name walks in and asks), §1.5 generational time (a second
founding), §0.1 cozy-and-challenging in one control (saying yes costs food and fuel now and buys hands; saying no costs
nothing), §0.1 recoverable by design (the valley's death becomes an event in a longer story), §1.1 legibility (a band is
people on the map with a card, never a counter).

---

## 2. What already exists to copy

| Need | Already here | Where |
|---|---|---|
| a system that fires on a village-wide moment, after the deaths, touching no RNG | `CivicSystem` — "nomads land here next" | `Systems/CivicSystem.cs:13–18` |
| a stop the player reads, and a passing banner | `SimWorld.RaiseMoment(title, body, stops, category)`; `Moment` is **verb-free by design** | `Core/SimWorld.cs:91`, `World/Moment.cs:23–27` |
| a player action that changes the sim | public methods on `SimWorld` — `Mark`, `SetStocking`, `Demolish`; no input queue, saves are snapshots (D507) | `Core/SimWorld.cs:8849` |
| making a villager and a household | `FoundVillage` (founders), `HouseholdSystem.TryBirth` / `Pair` (ids are max+1, never reused) | `Core/SimWorld.cs:11514`, `Systems/HouseholdSystem.cs:381, 529, 796` |
| names that cost no draw | `NameHash` (salts 1–6: prefix, suffix, surname, birthday, child day, meal phase); `NameFor`, `FoundingSurnameFor` skip living names | `World/NameHash.cs`, `Core/SimWorld.cs:11792, 11840` |
| a roofless family housed by the sim | `HouseTheRoofless` — an empty standing house first (**the family keeps its id and surname**), else a site in the paint, else `NeedsMoreResidentialLand` | `Systems/HouseholdSystem.cs:139–222` |
| no children without a roof | `IsReadyForAChild` (D71) | `Systems/HouseholdSystem.cs:607` |
| where a roofless villager sleeps, and how cold it is there | `HomePoint` → the cart / the founding site; `ShelterAt` → `Outdoors`; `HearthSystem.ChillTheUnsheltered` | `Core/SimWorld.cs:13369, 12879`, `Systems/HearthSystem.cs:140` |
| a dwelling with a larder, a hearth, stamps and *closed* | `Household` — a home is the household itself, not a building object | `World/Household.cs:104–128, 1237` |
| a building learned by a village fact | `WhyNotYet(kind)` — one switch read by the bar and by `Mark` | `Core/SimWorld.cs:3725` |
| the most people ever held (the bridge's unlock) | `MostEverAlive` — ⚠️ assumes arrival = founding or birth | `Core/SimWorld.Bridges.cs:523–558` |
| a 3×1 building nothing else needs | `BuildingKind.Longhouse = 14`, a warehouse that lies along the ground | `World/Construction.cs:257–273`, `Config/SimConfig.cs:3575–3606` |
| a flag hashed sparsely and saved | `SaidTheFoundersAreGone`, `IronToolsEverForged`, `Household.HomeClosed` (D555's commit is the template) | `Determinism/StateHash.cs:568–771`, `SaveLoadTests.Fields` |
| a per-stage seed that moves no other draw | `MapGenerator.StageSeed` = `SplitMix64.Fold(seed, stage)` | `World/MapGenerator.cs:50–64` |
| what stands and what state it is in | `ConditionOf(…)` per kind; `TendWhatStands` on the season turn — ⚠️ skipped while nobody is alive | `Core/SimWorld.Condition.cs`, `Systems/ClockSystem.cs:57` |

⛔ **Not the shape to copy: a Moment with buttons in it.** `Moment` carries no verbs on purpose — *a modal with a decision
in it is a modal that must be understood before it can be dismissed* — and D313 says a modal is exactly what a dead
valley must not have. The decision lives on a card (§4.3), where every other decision in this game lives.

---

## 3. Joe's calls — ANSWERED (asked and answered 2026-10-11, D563)

Every answer below is the decision. Kept in the asked form so the alternatives stay on record.

1. **What brings the first nomads?** D251 made the town hall their trigger, but the hall arrives at Year 30–60 and Joe
   asked for a first knock around Year 10. **Answered: their own clock, and the hall widens it** — the first band knocks
   in a spring around Year 10, then every few years; the town hall does not gate them, but once it stands the village is
   *known* and bands come larger and a little more often. Alternatives: the hall only, as D251 says (the lull and the
   surnames unanswered for decades); the hall irrelevant.
2. **How big is a band, and who is in it?** **Answered: one or two families, scaled to the village** — a share of the
   village, 2–8 people, each family a **new surname**, adults with a child or two, tiers mixed like the founding, a few
   days' food, no tools; the numbers measured before typed (§8). Alternatives: a fixed four; a third to a half.
3. **The communal house — when can the player build it?** **Answered: learned at the first knock, accepted or not — and
   it is the existing longhouse, repurposed.** Joe: *"just repurpose the long house that's already in game. there's no
   other need for it."* Alternatives: in the menu from Year 1; skip it this slice.
4. **Re-founding a dead valley — how does the foursome arrive?** **Answered: automatically, a few years on** — no
   question asked, nobody is left to speak for the village. Alternatives: they knock like any band; not this slice.

**Settled without asking, stated so he can overrule:** the knock is checked on the spring turn only; a band waits through
spring and summer and leaves at the autumn turn if unanswered; the band is not villagers until it settles (a huddle at the
pile, a card with two buttons, a bar button while it waits); the moment stays verb-free; nomads bring no techniques; the
longhouse is the one dwelling placed by a button (D42's exception, §5); a save format bump (no migrations, D507).

---

## 4. The rule

### 4.1 The clock — a hash, never a draw

A **knock** is checked once, on the tick the clock turns to Spring, Day 1, and only with somebody alive (an empty valley
is §4.6's business). The year of the next knock is a function of the seed and how many bands have knocked:

```
firstKnockYear   = nomads_first_year_floor   + Fold(seed, 0, KnockSalt) % (nomads_first_year_spread + 1)      → 8–12
nextKnockYear(n) = lastKnockYear + between     + Fold(seed, n, KnockSalt) % (nomads_years_between_spread + 1)
                   between = TownHall standing ? nomads_hall_years_between_floor : nomads_years_between_floor   → 4–8 / 6–10
```

A floor of `0` means *never* (the Phase 0 fixture's village of one). The hash is `SplitMix64.Fold` as `NameHash` and
`MapGenerator.StageSeed` use it — **no `Rng` draw anywhere in this feature**, so a village's main stream is byte for byte
the stream it had, and a knock cannot reshuffle a birth. ⚠️ Random with a floor is Joe's phrase; the "random" is the
seed's, which is what makes *"this valley's travellers come in Year 11"* a fact a share code reproduces.

State: **`BandsEverKnocked`** (an int, hashed sparsely when non-zero, saved) and **`BandKnockedAtTick`** (0 = no band
waiting; hashed sparsely, saved). The band's composition is **derived** from (seed, `BandsEverKnocked`, the population at
the knock) and re-derived wherever it is read — it is never stored twice (CLAUDE.md: a derived index is never hashed).
The population at the knock is stored with it (`BandMetAPopulationOf`, saved and hashed with the tick) so the derivation
has what it needs after a load.

> ⭐ **As built (D565): the band is STORED, not re-derived, and the reason is the names.** Its people's names are picked
> against the roster *at the knock* (`NameFor`'s rule, keyed a million past any villager id) and a birth during the wait
> would change a re-derivation, so the card and the settling could disagree about who was standing there. `SimWorld.Band`
> is a `NomadBand` — the knock tick, the population met, its families and people — hashed as sim state (ages, lifespans,
> rhythms, the trade; **never the names**, D465's rule) and saved. `NextKnockYear` is stored only once a band has come
> and gone; **the first knock's year is derived** (`FirstKnockYear()`), so a village nobody has knocked on hashes byte for
> byte as it did — `AVillageHashesAsBeforeUntilItsFirstKnock` guards it, and the `Rng` state is asserted equal across the
> knock.

### 4.2 The band — who walks in

```
people   = clamp(populationAtTheKnock / nomads_share_divisor, nomads_band_min, nomads_band_max)   (+ a family if the hall stands)
families = people ≤ 4 ? 1 : 2
```

The candidate numbers are **a quarter, 2–8** (§8.1 says why a fifth is the minimum everywhere a knock lands). Each
family: two adults aged 18–40 (hashed), the rest children aged 2–11; **one journeyman in the band** (a hashed pick from
`FoundingSkills()`, three-quarters of mastery as the founding does), everybody else novice, nobody a master (mastery is
twenty years on *this* village's work, D174); lifespan `LifespanYears ± variance` and the personal rhythm **by hash**
(new `NameHash` salts), a meal phase as a birth gets; `Founder = false`; **`ArrivedAtTick` = now**. First names from
`NameFor`; **a new surname per family** from the surname pool, skipping every living household's and the other family's.
They carry `nomads_food_each` food (20 — a few days), which goes into the family's larder on settling; no tools, no logs.

### 4.3 The knock, and the decision

On the knock the village **stops** (`RaiseMoment`, waits, `Discovery`): *"Travellers at the pile — the Hales and the
Dunns, seven souls: four grown and three children, carrying a few days' food. They have come up the valley asking for a
place to settle, and will camp by the pile until the leaves turn. Click them to decide."* The moment carries no verbs
(§2); the **band's card** does. The band is drawn as a huddle of dots at the pile's doorstep (the view, from the derived
composition); clicking it, or the bar's **Travellers (7)** button (shown only while a band waits), opens the card: who
they are by name, age and trade, what they carry, where they would sleep tonight (a longhouse with room, or *"in the open
at the pile — and this winter would kill them"*), and two buttons — **Let them settle** → `SimWorld.LetTheBandSettle()`,
**Send them on** → `SimWorld.SendTheBandOn()`. Unanswered through spring and summer, **they move on at the autumn turn**
(`nomads_wait_seasons` 2) with a passing line: *"The travellers moved on."* No harm, no foul — the clock just advances.
The unattended paths — the harness, the goldens, a headless run — are *unanswered*.

> ⭐ **As built (D565).** The moment: *"Travellers at the pile — the Hales, 2 souls, 2 grown and 0 children, carrying a
> few days' food — have come up the valley asking for a place to settle. They will camp by the pile until the leaves turn.
> The village holds 812 food, 101 a head. Click them to decide."* The huddle: a ring of dun dots inside the camp's tile
> (`VillageMap.HuddleOffsets`, children smaller), clicked before anything under it, ringed while its card is up. The card
> (`CardKind.Band`): title *Travellers — the Hales*, a status sentence that says where they would sleep tonight (and, from
> autumn, *this winter would kill them there*), souls / food carried / food a head here, the people by name, age, grown
> or child and trade, a caption naming the turn they leave at, and in its fold the row **Travellers: Let them settle ·
> Send them on**. The band gone, the card closes like a demolished store's. The bar's **Travellers (N)** button shows
> while a band waits. Probe lines `nomads:` (the card posed from plain facts, both buttons wired) and `huddle:` (2–8 stand
> in a ring inside the tile, nobody on anybody else).

### 4.4 Settling — roofless, and then what the village already does

`LetTheBandSettle()` makes each family a `Household` (`NextHouseholdId`, the surname, `DayForAChild` hashed, the larder
holding the carried food, `HomePosition = null`) and adds its villagers at the pile, then calls `NoticeTheVillageSize`
(the wooden bridge is learned by the most ever held, and arrivals count — `MostEverAlive` reads `ArrivedAtTick`, §5).
⭐ **As built (D565): the two adults are paired on settling**, as the founding pairs its founders — unpaired, they would
pair across the band on the very tick they arrived and take over a dead household whole (§8's trap).
Log, `Life`: *"The Hales and the Dunns have settled — 7 more souls, and no roof yet. Spring, Year 11."*

From there nothing is new: they are roofless families, so `HouseTheRoofless` moves them into an empty standing house the
next day if one stands, else marks a house in the residential paint (a builder raises it), else says the village has
nowhere to build; until then they sleep at the pile, `Outdoors`, and a winter there kills (Joe's rule — the card said so
before the click). They work any job the allocator gives them from the day they arrive. No children without a roof (D71).

### 4.5 The longhouse — the communal house (call 3)

`BuildingKind.Longhouse = 14` keeps its 3×1 footprint and stops being a warehouse. It is **a communal `Household`**
(`HouseholdKind.Communal`): no members of its own, a larder, a hearth, the two stamps, `HomeClosed` — raised through
`RaiseFinished` like a well, pulled down like any building. The row: priced as **two houses** from the house keys (no
`longhouse_logs`, D16), `HouseCapacity` = `longhouse_capacity` 8, `LocalStoreCap` twice a house's, `Stores = None`,
wears like every timber row.

**Lodging.** A roofless family is **lodged** (`Household.LodgedIn` = the longhouse's household id) in a longhouse with
room — on settling, and daily in `HouseTheRoofless` *after* the own-house check — and **moves out the day its own house is
raised** (a house of its own is always first; the longhouse is the stopgap). While lodged:
- `HomePoint` is the longhouse's doorstep; `ShelterAt` its tile is `Fire` while its larder has firewood, else `Roof`,
  and `Outdoors` when `HomeClosed` (a ruin).
- **One larder, the longhouse's** (Joe): a lodger's meals come from it and a lodger's food and firewood fetches fill it —
  the household-larder seam (`LarderOf(villager)`) in `BehaviorSystem.PlanFetch`, the meal, and `HearthSystem`'s burn
  reads the lodging when there is one. The family's own larder keeps what it carried and comes with them when they leave.
- **Nobody partners while lodged** (`IsSeekingAHome` excludes them) and nobody is born there (no home of their own, D71).
- The card says *lodging at the longhouse* and the longhouse's card lists its lodgers, its larder and its condition.

**Unlock:** `WhyNotYet(Longhouse)` → *"No travellers have asked to stay yet."* until `BandsEverKnocked > 0` — learned at
the first knock whether the band settled or not, so it is in the menu for the next one. A longhouse with lodgers that is
pulled down turns them out (a log line); a longhouse fallen in (`HomeClosed`) shelters nobody, and its lodgers are
roofless again until it is mended.

### 4.6 Re-founding — a dead valley, a few years on (call 4)

With nobody alive, the tick the last villager died is **derived** (the greatest `DiedAtTick`). On the spring turn
`refound_years_floor` 2 + `Fold(seed, BandsEverKnocked, RefoundSalt) % (refound_years_spread + 1)` years after it,
**four travellers walk in**: two households of two adults, the founding's shape — `Founder = true` (**a second founding
generation**), new surnames, the founding's tier mix (a master, a journeyman, two novices) — all by hash. The cart, if
it still stands, is restocked with `cart_food` and `cart_tools`; if it was pulled down it is raised again at the founding
site. Stores keep what they hold (a ruined store still *gives*, D555). The next day `HouseTheRoofless` moves them into
the standing empty houses, **keeping their own names** — the house is a house, not a lineage.

The moment (waits, `Discovery`): *"Four travellers have found the empty valley — the Thorns and the Ashes. They walked past
3 roofs still sound, 4 worn, and 2 fallen in, and the granary still holds 600 food. The valley is theirs now. Spring, Year
31."* The empty-village banner (D313) is untouched: it is a passing line and the game runs on, which is what let this
happen.

**The empty valley keeps time.** `ClockSystem` gates the season's *narration* on somebody being alive but
**`TendWhatStands` runs regardless**, so a ruin closes in the year it is due rather than all at once the day somebody
returns; **no repair site is raised while nobody is alive** (a site nobody can serve is noise in the queue), and the
first season turn after the re-founding raises repairs for everything worn. `CivicSystem`: if the hall was never given
(the first founders died with nobody left — D252's guard), it is owed on the last of *these* founders; `TributeTo` says
which founding it mourns and names both generations, and never raises a second hall.

### 4.7 Not in this slice, said so

A walk-in from the valley's edge (the band appears at the pile; a walk from the map's edge is a cosmetic later);
morale's departures (`morale.md` — arrivals now exist, so the valve has two ends when it comes); techniques carried by
nomads; a band that asks for anything but a place; the longhouse as a trade or a store; livestock or goods in the cart.

---

## 5. What changes meaning

- **D42's rule gains one exception.** Housing is painted with the brush and never placed by a button; the longhouse is
  placed by a button *and is never a family's home* — a family lodges there and leaves it for a house the brush sited.
- **D251 is amended, not retired.** The town hall *widens* the nomads (call 1); it no longer triggers them. The hall's
  spec, `Construction.cs:206` and `CivicSystem.cs:16` say so.
- **`Moment` stays verb-free.** The first decision the player makes in answer to a stop lives on a card, like every
  other decision (§2).
- **`MostEverAlive` reads arrivals.** An adult who arrives at tick T is counted from T (`ArrivedAtTick`), not from 0 or
  from a birth they had elsewhere; founders and the born keep their old reading, so the bridge's unlock in a village
  nobody joins is byte for byte what it was.
- **The empty valley's season turn.** `TendWhatStands` runs with nobody alive (§4.6). A valley that never empties is
  unchanged.
- **The longhouse stops storing.** Three test files used it as *the* multi-tile store (`FootprintTests`,
  `TileFindersAgreeTests`, `ModdedBuildingTests`); they move to the 2×2 warehouse. The footprint geometry they guard is
  unchanged — the longhouse is still the 3×1.
- **Goldens.** The six fifty-year goldens move **once**, in commit 1: bands knock from Year 8–12 in the fixture and the
  shipped seed, and `BandsEverKnocked` is hashed even when the band goes unanswered. Proven by setting
  `nomads_first_year_floor` to 0 and watching the old values pass. The map golden and every pose before Year 8 hold.
  ✅ **Done (D565): exactly six moved** (`FarmGoldenTests` ×2, `StockLimitTests` ×2, `SkillTests` ×2), and with the floor
  at 0 in both the shipped file and the fixture all five that run first passed on their old values before any was
  re-taken; the sixth (`SeamBeforeAnybodyGotBetter`) surfaces only once the first is green, as it did in D555.

---

## 6. Saved, hashed, drawn, told

| What | Hashed | Saved | Drawn | Told |
|---|---|---|---|---|
| `BandsEverKnocked`, `BandKnockedAtTick`, `BandMetAPopulationOf` | sparsely, tagged, after everything else | `bands_ever_knocked`, `band_knocked_at`, `band_met` | the huddle at the pile; the bar button | the knock moment; *moved on* |
| `Villager.ArrivedAtTick` | sparsely (non-zero) | `arrived_at` | — | the card: *came to the valley in Year 11* |
| `Household.Kind`, `Household.LodgedIn` | sparsely (non-default) | `kind`, `lodged_in` | the longhouse (a long house, the house tone); *lodging at* on the card | *settled*, *lodged*, *moved out* |
| the longhouse's stamps, larder, `HomeClosed` | as a household's | as a household's | weathered like every building | the card's condition line |
| `SaveGame.Format` | — | **6** — older saves refused in words | — | the title screen's refusal |

`SaveLoadTests.Fields` names every new field; `EverySavedFieldComesBackAsItWas` and the year-after guard cover them.

---

## 7. Edge cases and failure modes

| Case | Rule |
|---|---|
| The knock year arrives with nobody alive | No band. The valley is on §4.6's clock instead; the knock clock resumes after the re-founding, counted from it |
| A band waits and everybody dies | The band moves on at its deadline as usual; §4.6's clock starts from the last death |
| Settle in autumn | Allowed; the card said *this winter would kill them*. The player chose it (§0.1: a decision with a consequence, not an error) |
| No residential paint, no longhouse | They settle roofless; `NeedsMoreResidentialLand` says so once; the card says *in the open at the pile* |
| The longhouse is full | The ninth sleeps rough until a house is raised or a lodger moves out; its card says *full* |
| The longhouse falls in with lodgers | `HomeClosed`; they are `Outdoors` there until it is mended — the same rule a ruined house has |
| The longhouse is pulled down with lodgers | Allowed; they are roofless again; a log line names them |
| A lodged family's house is raised | They move out that day; the longhouse's room frees |
| A refused band and the longhouse | Still learned — the knock is the lesson, not the yes |
| Save mid-band, load | The band is re-derived from the saved tick, count and population; its deadline is the saved tick's |
| Load an older format | Refused in words (D507, no migrations) |
| Re-founders find no house standing | They are roofless at the founding site, as the first founders were; the paint, if any, still stands |
| The hall was given before the village died | `SaidTheFoundersAreGone` is set; the second generation's deaths raise nothing |
| The first founders died with nobody left (no hall) | The hall is owed on the last of the second generation, and the tribute names both |
| A knock while a band waits | Cannot happen: the next knock year is set when a band leaves or settles |
| Nomads and the surname pool run dry | `FoundingSurnameFor`'s attempts loop as today; with 60 names and ≤ 14 living households it never does |

---

## 8. Measured before typed (D564, 2026-10-11) — `tools/harness/ZzNomads.cs`, and Joe's saves

The arrivals were **posed from the test side** before a line of them existed (a `Household` and its `Villager`s built by
hand, as `TownHallTests` does): 100 shipped valleys (seeds 200–299), the played opening, a granary and a warehouse marked
at Year 3, fifty years unattended; `python tools/harness/nomads.py` summarises. ⚠️ A harness is not a player (D447): it
measures that a setting can be lived in, not how it plays — nobody paints more ground for the extra mouths.

### 8.1 What a knock meets — the `knock` arm, Spring, Day 1 of Years 8, 10 and 12 (86 valleys alive; 14 died in Year 1)

| | alive p10 / med / p90 | households | all housed | empty houses | food a head p10 / med / p90 | nowhere to build | band at a fifth | at a quarter |
|---|---|---|---|---|---|---|---|---|
| Year 8 | 6 / 8 / 8 | 2 | 86 of 86 | 1 | 93 / 103 / 232 | 0 | 2 (max 2) | 2 (max 2) |
| Year 10 | 8 / 8 / 9 | 2 | 86 of 86 | 1 | 90 / 103 / 212 | 0 | 2 | 2 |
| Year 12 | 8 / 9 / 10 | 2 | 86 of 86 | 1 | 77 / 91 / 183 | 0 | 2 | 2 |

An unattended village at the first knock is **eight people in two housed families with a hundred food a head and room
in the paint** — so every rule in reach sends the minimum band of two, and **the "fifth or quarter" question is decided
by Joe's villages, not these** (§8.2). The opening's 9×9 of residential paint is why nobody is ever *nowhere to build*.

### 8.2 What a knock meets — Joe's played villages (27 saves, the latest autosave of each; a few earlier ones)

| Year | alive | households (all housed) | empty houses | surnames alive | a fifth | a quarter |
|---|---|---|---|---|---|---|
| 8–15 (5 villages) | 8–10 | 2 | 0 | 2 | 2 | 2 |
| 23 | 8 | 3 | 0 | 2 | 2 | 2 |
| 50–51 | 17 | 4 | 1 | **1** | 3 | 4 |
| 59–60 | 25–27 | 7 | 1 | **1** | 5 | 6 |
| 84–92 (3 villages) | 38–49 | 11–14 | 0–2 | **1** | 7–8 | 8 (the cap) |

Two things decide the numbers. **Every played village past Year 50 is one surname** — D558 was not one unlucky valley —
so a knock at Year 8–12 with a *new name* is the cure, whatever its size. And **a fifth of a village of eight is two**:
the first band in a played village is a couple whatever the share says, so the share only matters from Year ~50, where
a quarter sends six against a fifth's five. ⭐ **A quarter, 2–8** is the candidate (`nomads_share_divisor` 4): the hall
(Year 30–60) adds a family on top, and Joe asked for *needed growth*. The share is a knob either way.

### 8.3 A band of two settled at Spring, Year 10 — the `settle` arm, 86 valleys, 172 people in 86 families

| | housed in Year 10 | later | never | died by Year 50 | of them in Years 10–11 | alive at Year 50 |
|---|---|---|---|---|---|---|
| the settlers | **85 of 86 families** | 1 | 0 | 42 (all starved, none froze) | 2 | 130 of 172 |

| the valleys | alive at 50 | dead valleys | peak | starved | froze |
|---|---|---|---|---|---|
| unanswered (`knock`) | 595 | 20 | 893 | 197 | 61 |
| settled, 2 at Year 10 | 601 | 18 | 987 | 306 | 60 |

**Nobody freezes**: the opening's paint and the logs in store house a family the same year (the sim's own roofless
queue), which is the sentence Joe's rule depends on — *they freeze without houses of their own*, and a player who has
painted has given them one. The extra mouths then deepen the unattended boom-bust (D385 §12.3: peak +11 %, starved
+55 %, alive at 50 +1 %) because nothing paints more harvest ground for them — the harness playing badly, not the band.
Two fewer dead valleys. ⚠️ The couple is posed *paired*, as the founding pairs its founders; unpaired, the two re-paired
with natives on the spring tick and the first run's numbers were about the wrong households (the trap in `HANDOFF.md`).
*(The band-of-four arm: §8.3a, below.)*

### 8.3a A band of four at Spring, Year 10 — `ZZ_SETTLE=4` (one family: a couple and two children), 86 valleys, 344 people

| | housed in Year 10 | later | never | died by Year 50 | of them in Years 10–11 | alive at Year 50 |
|---|---|---|---|---|---|---|
| the settlers | **85 of 86 families** | 1 | 0 | 157 (155 starved, 2 froze) | 15 | 187 of 344 |

| the valleys | alive at 50 | dead valleys | peak | starved | froze |
|---|---|---|---|---|---|
| unanswered (`knock`) | 595 | 20 | 893 | 197 | 61 |
| settled, 4 at Year 10 | 620 | 18 | 1,133 | 417 | 58 |

The same shape, twice as loud: housed the same year, nobody freezes, the peak a quarter higher and twice the starving —
the unattended economy cannot feed the growth it is handed, and the valley ends about where it started (+4 %). **What
the two arms settle:** the band's cost is *food*, never a roof, wherever the player has painted; the card's warning
sentence is about the larder as much as the winter, and the knock's line (§4.3) should say what the village holds a
head.

### 8.4 What stands N years after a valley dies — the 20 dead valleys of the `knock` arm

Died in Year 1: 13 valleys (D538's lost foundings); then Years 4, 19, 28, 30, 45, 49, 50. Standing at the death: 62
buildings in all — a Year-1 valley leaves the builder's hut and the woodcutter's hut; a Year-45 one its whole village.

| years on | sound | worn | failing | ruin | food in stores (med) | firewood (med) |
|---|---|---|---|---|---|---|
| 0 | 50 | 4 | 4 | 4 | 300 | 0 |
| 2 | 40 | 14 | 4 | 4 | 300 | 0 |
| 4 | 30 | 19 | 5 | 8 | 300 | 0 |
| 6 | 27 | 11 | 16 | 8 | 300 | 0 |

Wear keeps its clock in an empty valley (the states are derived from the stamps), so **"some buildings crumbled"
is true at 4–6 years** — a fifth to a quarter of what stood is a ruin or failing, the rest worn — and *"the library
decayed"* follows the same curve (the one library in the set turned worn at +4). ⚠️ `TendWhatStands` is skipped with
nobody alive today, so none of these ruins is **closed**: §4.6 runs the tending in an empty valley so the flag follows
the state. The food is the cart's 300 — the builder's hut never wears (`WithTheShippedWear` skips it) and the cart is
not a building, so a Year-1 valley keeps both.

### 8.5 Four re-founders, three years on — the `refound` arm, the 20 dead valleys (two households of two adults, paired, the cart restocked with `cart_food`)

| what stood when they came | valleys | housed | first winter | five years on | twenty years on |
|---|---|---|---|---|---|
| no house — a valley that died in Year 1 left the builder's hut and the woodcutter's hut | 11 | 2 raised one themselves (Year 4–5) | **9 of 11 foursomes froze, all four** | the 2 housed lived to old age | 0 |
| two to four houses, sound to failing | 9 | 9 of 9, the year they came (the sim's own roofless queue) | 8 of 9 came through (one froze in a failing house with no firewood) | 7 of 9 alive | 6 of 9 alive, 29 people |

Over all twenty: alive a year on in 9 valleys, five years on in 7, twenty years on in 6 — and then D143 (an unattended
foursome is not a player; most starved in the thirties and sixties). **The one number that matters is the first winter:
a roof standing is the difference between nine of eleven freezing and eight of nine living.** So §4.6 as written holds —
re-founders take the standing houses the next day, keeping their names — and the moment must say what stands (*3 roofs
sound, 4 worn, 2 fallen in*) because a valley that died in its first year offers two huts and a cart, which is the cold
start again with the player's season of warning. The cart took the full `cart_food` (1,200) in every valley; nobody
starved before Year 9.

### 8.6 To measure on the build, before any number is typed into `data/sim.config.json`

- `tools/harness/arms.sh base` before and after each commit, same sitting: dead valleys and alive at 50 on the unanswered
  path must read as today's (the acceptance line is D555's 21 of 100), because an unanswered band changes nothing but a
  count.
- The suite's clock back to back (D426).
- On commit 1, the `settle` arm run through the sim's own `LetTheBandSettle()` rather than the harness's pose, and the
  knock years of the hundred valleys (p10 / med / p90 of the first knock, and of the second).
- On commit 3, the `refound` arm through the sim's own re-founding: first-winter survival with and without a roof
  standing, and the years to the second founding.

---

## 9. Guards (TDD, red first, red-checked and counted — D326)

**Commit 1 — `NomadTests`:** the knock year is a function of the seed and never before the floor; a knock lands on the
spring turn only, with somebody alive; floor 0 = never (the anti-vacuity twin: a village that never meets a band hashes
as today); the band's size scales and clamps; the hall widens (floor and a family); every surname is new (none living,
none shared within the band); no `Rng` draw (the stream's state is untouched across a knock); an unanswered band leaves
at the deadline and the next knock year is set; sending on leaves everything but the count as it was; settling adds
roofless households holding their food, `MostEverAlive` counts them (a bridge learned by arrivals), a settled family is
housed when the paint has room and freezes at the pile when not, no birth while roofless; sparse hash before the first
knock; save → load → hash == live mid-band, and the year after matches.

> ⭐ **Commit 1 (D565) — `NomadTests` (30).** Mutants, each the one line it names: the knock a tick late **13 red**; the
> floor ignored **2**; the first year drawn from `Rng` instead of hashed **15** (the `Rng`-state assertion and every
> knock-year guard); a living surname reused — ⚠️ **zero** against the shipped pool of sixty, so
> `ABandTakesTheOneSurnameNobodyLivingHolds` poses a pool of three over five seeds: **3**; the deadline a season late
> **1**; arrivals not counted by the bridge — ⚠️ **zero** where the peak was the present, so `AnArrivalCountsFromTheTickItCame`
> poses a village three below its peak: **1**; the couple left unpaired **1**; the food carried lost **1**; a knock on an
> empty valley **1**; two families from five **1**; the hall not widening **1**; the civic hook gone **14**; the band not
> saved **1**; the count's hash block dropped — ⚠️ the first mutant (`if (false)`) **did not build** (D450), re-posed as
> `> 1000` and caught by the sent-on guard's silent twin **1**; `ArrivedAtTick` not loaded — ⚠️ **zero** with the only
> save pose made while the band still waited, so `ASettledFamilySurvivesASaveAndItsYearMatches`: **1**. **15 mutants,
> 57 reds, no zeros.** Found while building: the fixture village inherited Phase 0's floor of 0 and no band ever knocked
> in the first run — `VillageFixtures.Village` puts the shipped knock back, as it does the wear (D555's shape); and
> `SimLoop` computes a tick before it advances, so a band raised on the knock tick is there one step past it.

**Commit 2 — `LonghouseTests`:** locked until the first knock; lodges up to capacity and the ninth sleeps rough; the
larder is fetched to and burned from by lodgers; shelter follows the larder; a lodged family moves out the day its house
is raised; nobody lodged partners; a fallen-in longhouse shelters nobody; pulling it down turns lodgers out; the three
re-pointed store guards still pass on the warehouse; hash/save.

**Commit 3 — `RefoundTests`:** nothing for the floor years; the four arrive on a spring turn; founders marked, names
new; standing houses taken, names kept; the cart restocked (and raised again when gone); stores kept; ruins close on time
while empty and no repair site is raised; repairs raised at the first turn after; the hall owed on the last of them when
never given, never twice; the empty-village banner still passing (`ColdStartTests` stays green); hash/save.

Mutants to count (each must redden something): the knock a tick early / late; the floor ignored; the hash replaced by an
`Rng` draw; a surname reused; the deadline off by a season; `ArrivedAtTick` left out of `MostEverAlive`; a lodger's meal
from the wrong larder; a lodged villager pairing; move-out skipped; `TendWhatStands` still gated on the living; a repair
raised in an empty valley; a second hall; a field left out of the hash or the save.

---

## 10. Definition of Done

- [x] Joe's four calls answered (D563).
- [ ] §8 measured before any number is typed into `data/sim.config.json`, and §8.6 on the build.
- [◐] §9's guards; mutants listed with their reds; any zero kept and written down — **commit 1 done (§9)**.
- [x] Goldens moved once, in commit 1, proven by the floor at 0.
- [◐] The four checks (CLAUDE.md) on every commit: suite, view build read, probe (`nomads:` ✅, bar height 151, `done.`),
      goldens diffed — **commit 1: 1661 / 0 / 5 of 1666 (7m01); view 0 warnings; probe 84 lines, no ⛔; six goldens.**
- [ ] `DESIGN.md` §4 (Phase 6, the milestone map), §5 (the nomads entry), §6, §7; `town-hall.md`, `names-and-birthdays.md`,
      `morale.md`, `content-inventory.md`, `footprints.md`, `save-load.md`; `HANDOFF.md`; this status line made true — in
      the same commit as the code.
- [ ] Played by Joe: a new village to the first knock (~Year 10), the huddle, the card's two buttons, a settled family
      housed or freezing, the longhouse in the strip after the knock, lodgers moving out; a village left to die and found
      again a few years on.
