# Spec: Water as a household need — the well's clock becomes the larder's

> Status: **📝 SPECCED (D775, 2026-11-15, Joe: *"water is next for systems"*); ✅ ALL FOUR CALLS AS RECOMMENDED (D776, *"All as recommended"*)
> — 🔨 ON `slice/water-need` (Joe played it, D782: *"looks good! proceed!"*; what his log showed fixed first, *"Fix it first"*); ✅ STEP 1 (D428's trace, D777, §2a): NO MEASURABLE COST; ✅ STEP 2 (D778, §2b): WATER AT HOME — THE JAR, THE DRINK, THE TRIP THAT FILLS IT; ✅ STEP 3 (D779, §2c): THE RIVER BANK; ✅ CALL 4, JOE'S JAR OF 120 (D779); ✅ STEP 4 (D780, §2d): RUNNING DRY IS SAID, NOT RUSHED (Joe); ✅ STEP 6 (D781, §2e): THE VIEW — THE CARD'S LINE, NEEDS, THE PAIL; ✅ PLAYED, AND A JAR FILLED BEFORE IT RUNS DRY (D782, §2f, Joe: *"any time at home"*); THE MERGE AT HIS WORD.** *(Read "JOE PLAYS IT NEXT" at D781.)* *(Read "STEP 6, THE VIEW, NEXT" at D780.)* *(Read "STEP 4, RUNNING DRY, NEXT" at D779.)* *(Read "STEP 3 NEXT" at D778.)* *(Read "STEP 2 NEXT" at D777.)* *(Read "STEP 1 … UNDER WAY" at D776.)* *(Read "⏸️ FOUR CALLS JOE'S … NOTHING BUILT" at D775.)* Owner: Joe + Claude Code · Pillar: `DESIGN.md §2.1` (needs), §2.6 (one cost field)
> · Phase 6, the ordered list's item 2 (D750). Format per `METHODOLOGY.md §2`.
>
> Neighbours: `organic-housing.md §9.12` (the well, D427 — **the baseline this is measured against is §9.12f**),
> `clothing.md` (the last need-shaped errand: a coat fetched from a store), `pathfinding-and-water.md` (the river is
> impassable and generated once), and Phase 6's later pieces that read this one: **drought** (its answer is the well,
> D656) and **health** (*"the well is health's door to water"*, §4).

---

## 1. Goal

Joe, 2026-09-29 (D427), when the well went in on a fixed clock: *"the water function isnt even in the game yet - the
household larder and home water needs will determine how often villagers go to the well, but this data (every 20 days)
helps with that when the time comes."*

So: **a household keeps water at home and drinks it; somebody goes for more when it runs low.** The walk is the
whole cost, and it is visible, the way the shopping walk is (D668). The well's 20-day clock goes.

## 2. What is there today (re-read against the code, 2026-11-15) *(⚠️ before step 2 — §2b is what changed, D778)*

- **The well** (`BuildingKind.Well`, `SimWorld.Wells`, D427): optional; with none, nothing is tried and nothing hashed.
- **The trip** (`BehaviorSystem.TryDrawWater`): offered only where a rest at home would begin, to an adult with empty
  hands, when `Household.WaterDrawnOnDay` is `water_trip_every_days` (20) old and no housemate is out, and **only to a
  well within `home_well_reach_tiles` (12)**. `WalkingToTheWell` → `DrawingWater` (`well_draw_ticks` 2) → home.
  **Nothing is carried and nothing is drunk.**
- **Hashed:** `WaterDrawnOnDay`, when set. Saved: `water_drawn_on_day`.
- ⚠️ **Open since D428, and first in the order of work:** *"why the water trip costs survival at higher frequencies —
  trace it before Phase 6 makes water a need."* Every 10 days cost 21 shipped alive of 169 (§9.12f); the `Decide`-only
  ablation read 145, so the cost is not the job-holder's idle moment, and its mechanism is still unproven. **A need
  that sends people more often than every 20 days inherits that cost unexplained.**

### 2a. ✅ Step 1 — D428's trace (D777)

| arm (100 valleys, 50 years, seeds 200–299) | alive | starved | froze | trips a run | Δ alive against no well, paired (± s.e.) | rest, adult-years | going in to get warm |
|---|---|---|---|---|---|---|---|
| no well | 13.4 | 6.2 | 6.4 | 0 | — | 326.7 | 0.8 % |
| noise (every stock limit +1) | 13.3 | 6.2 | 6.4 | 0 | −0.06 ± 0.25 (47 differ) | 329.0 | — |
| a well, trips off (one each) | 13.3 | 5.9 | 6.4 | 7 | −0.06 ± 0.33 (55 differ) | 322.8 | — |
| a well, every 20 days (shipped) | 13.1 | 6.1 | 6.6 | 948 | −0.29 ± 0.30 (62 differ) | 291.8 | — |
| a well, every 10 days | 13.1 | 5.9 | 6.4 | 1,804 | −0.32 ± 0.35 (61 differ) | 257.9 | 1.0 % |
| a well, every 5 days | 13.1 | 6.0 | 6.6 | 3,352 | −0.27 ± 0.32 (62 differ) | 200.2 | 1.6 % |

**D428's question answered (D777): on today's code the trip costs nothing measurable.** Every arm is within the noise
of a perturbed run; dead valleys 12 in every arm. **The hours come out of rest and nothing else**: the able adults' shares of
hauling, fetching, gathering and building are the same to the tenth of a per cent with and without trips, because the trip
is offered only where a rest would begin. **The one side effect is the winter**: walks to the well in the cold double the
time spent going in to get warm (0.8 → 1.6 % at every 5 days), with no rise in cold deaths past the noise. §9.12f's 21 was
30 valleys with one noise arm, not a spread; it does not reproduce. ⚠️ A harness village rests 69 % of its adult time (D447:
a harness is not a player), so in a busy played village the trip has less rest to come out of, and still never displaces
work by structure. `tools/harness/ZzWell.cs` + `well.py`.

### 2b. ✅ Step 2 — water at home, built (D778)

- **`Household.Water`**, the jar (call 1A: a number, not a good). Filled to **`water_jar` (40)** when a household's drawer
  finishes at the well — where the drawing ends, so a drawer the cold breaks off fills nothing and somebody goes again.
- **The drink:** on each day's first tick (`NeedsSystem`), every living member drinks **`water_per_person_per_day` (1)**,
  never below empty. An empty jar costs nothing yet — step 4 (call 3A).
- **When somebody goes:** where a rest at home would begin, as before, once the jar is empty or holds less than another
  day for the living (`BehaviorSystem.WaterIsLow`). **The trip under way is the claim** — no day is stored.
- **Retired:** `water_trip_every_days` and `Household.WaterDrawnOnDay`. **Saved** as `water` (null for an empty jar, as
  `keeps_its_house` is) **with no format step** — a save from before has no `water` key and loads with every jar empty,
  so Joe's format-13 saves still load (with a well, each household goes at its next rest, as the old clock's first trip
  did). **Hashed** only when there is water in it, so a village with no well hashes as before.
- **The price, at the shipped sizes** *(⛔ step 2's jar of 40 — 120 since D779, §2c)*: a household of two every 20 days, today's clock (`AHouseholdOfTwoGoesEveryTwentyDaysAsTheClockDid`).
  Measured on the fixture village over twelve years, days between a household's trips by how many lived there:
  **2 → 24.5, 3 → 15.7, 4 → 13.8, 5 → 13.0.** ⚠️ **A big household goes less often than its jar asks** (five drink 40
  in eight days, and go every thirteen): the trip waits for an adult resting at home with empty hands, and a busy big
  household rests less. It sits dry for days, which costs nothing yet — **step 4's trip ahead of work is the answer, and
  this is the measurement it is checked against.**
- **Goldens:** **one moved, the Phase 5 village's** (`PhaseFiveGoldenTests`, both constants): its pose has a well. Its coverage test went red too (no cattle landed), and that was the pose, not water: on the old code its herd landed only at a trip every 20 or 21 days, not 15, 19 or 25 — one boat in Year 14 bought it only if the post held the firewood to pay — so the pose's firewood keep went 300 → 100, where cattle, calves and leather came in all 27 arms (4 intervals on the old code, 5 jars on the new; `phase-5-golden.md`). Every other golden is unmoved — none of their villages has a well.

### 2c. ✅ Step 3 — the river bank, built; ✅ call 4, Joe's jar of 120 (D779)

- **A bank tile** is land with the river on one of its four sides and nothing standing on it (`SimWorld.IsBank`). **The bank a
  home uses is the one cheapest to walk home from, on the one cost field** (`SimWorld.NearestBank`): every candidate priced on
  the field *to the home*, which every walk home has already built — no field per bank tile, no second distance. The bank list
  is derived from the terrain (`TerrainGeneration`), never hashed or saved.
- **Which:** a well within `home_well_reach_tiles` whose walk is no longer than the bank's; otherwise the bank. So a well's payoff
  is walking saved, and one built further off than the river is not used. ⚠️ The well's reach (12) still bounds it: a well 15 out
  with the river 30 out is not used — the siting term and the card read the same reach, and this slice did not move them.
- **Two new states, at the end of the list** (no state already saved or hashed changes its number): `WalkingToTheRiver`, then
  `DrawingAtTheRiver` (`well_draw_ticks`, then home). A bank built on while they walk sends them home; the jar is still low.
- ⛔ **The price at a jar of 40 was too dear, and that is arithmetic.** A day is four ticks and a villager walks about a tile a
  tick; most fixture founding sites are 6 to 29 tiles from the bank (seeds 1–40), so a household with no well spent ten days on
  a trip its jar of 40 bought thirteen days of, for three. Measured:

| arm (100 valleys, 50 years, seeds 200–299; paired against D777's no-well base) | Δ alive (± s.e.) | starved | adult time on water | rest, adult-years |
|---|---|---|---|---|
| before water (D777's base) | — | 6.2 | 0 | 326.7 |
| the river, jar 40 (step 2's size) | **−0.59 ± 0.30** | 7.0 | 6.2 % | 262.1 |
| the river, jar 120 | −0.27 ± 0.34 | 6.2 | 2.6 % | 303.8 |
| the river, jar 400 | −0.03 ± 0.29 | 6.2 | 0.9 % | 314.1 |
| a well at Year 3, jar 40 | −0.42 ± 0.38 | 6.6 | 4.5 % | 280.2 |
| a well at Year 3, jar 120 | −0.31 ± 0.34 | 6.2 | 1.8 % | 304.2 |

| `FarmTests` (six fixture valleys, ten years) | before the river | jar 40 | 80 | 120 | 200 |
|---|---|---|---|---|---|
| brought in, all six (bar 75 %) | 79 % | **66 %** | **71 %** | 75 % | 75 % |
| the near farm | 84 % | **71 %** | 86 % | 89 % | 87 % |
| the far farm | 86 % | 87 % | 96 % | 95 % | 91 % |

  **⭐ Joe's pick, 2026-11-15 (call 4): *"120 (Recommended)"*** — two go every sixty days, three every forty, five every
  twenty-four; within the noise at fifty years. The river at a jar of 40 is the one arm outside it.
- **Guards the river moved, and why** (all poses, none of them the rule they guard): `HerdsmanTests` keeps the herdsman's jar
  full beside his larder (D601's own reason — the household's errand beat the herd's); `LodgeCarriersTests` stands down a hunter
  already out when the pose sets its rows (an errand under way is finished; which tick the pose lands on decided whether one was
  out), and its cap guard sets the row at a third of the meat, not a half (at a half it sat exactly on the cap, and a season's
  eating crossed it — D714's rule then sends the hunter out, as it says).
- **Goldens:** **eight constants in five goldens and one pin moved, each proven by ablation**: the farm golden (both), the two skill fifty-years, the two stock-limit fifty-years, the walking-clock pin (48 → 49 trips) — all six back to their old values with the bank never offered — and the Phase 5 golden (both), back to its old pair with the bank never offered and the data jar at 40 (it has a well). ⚠️ The farm golden's second value hid behind its first assert until the first was re-taken (D735's trap).

### 2d. ✅ Step 4 — running dry: said, not rushed (D780; Joe: *"Say it, don't rush"*)

**Built as call 3A was answered first, and measured.** A dry household sent its first able adult with empty hands at once,
from work, ahead of everything but eating, warmth and the restock — and said so. Over 100 valleys at Joe's jar:

| a dry household (100 valleys, 50 years, jar 120) | Δ alive against step 3 (± s.e.) | starved | born |
|---|---|---|---|
| somebody leaves their work at once (call 3A as answered) | **−0.56 ± 0.32** | 6.2 → 6.6 | 18.1 → 17.3 |
| somebody resting or idle goes at once, no job dropped | −0.37 ± 0.30 | 6.0 | 16.9 |
| nobody rushes; the next rest at home fills it (✅ Joe's pick) | **+0.00** — no valley differs | 6.2 | 18.1 |
| the rush, and the founders' jars empty | −1.16 ± 0.39 | 7.7 | 17.6 |

**An empty jar harms nobody** (no thirst, call 3A), **so the rush bought nothing and took work time** — D777's lesson the other
way round: a trip that comes out of rest is free, one that comes out of work is not. Put to Joe: *"Say it, don't rush
(Recommended)"*. **What ships:**

- **The day a jar runs dry the log says so, once** (`NeedsSystem`, the drink that empties it): *"The Bramble household has run
  out of water — somebody will go for it at their next rest at home."* A jar already empty is never read again, so once a
  spell by construction.
- **Nobody rushes.** It is filled at the next rest at home, as every trip is (measured: 41 ticks, on the fixture's pose).
- **The card's sentence** (`SimWorld.WaterNoteFor`): nothing while there is water; *"No water — somebody will go at their next
  rest at home"*; *"No water — Mabel has gone to the river"* once somebody has. The sim's half; the card draws it at step 6.
- **The founders start with a full jar** (and a refounding) — costs nothing measurable (the no-rush arm above starts full and
  no valley differs), and a new valley does not open dry. A household formed later starts empty and fetches at its first rest.
- **Saved always** — a save without the key is from before water and reads full, so a dry jar and an old save are told apart.

**Two old guards, widened and re-barred** (Joe: *"Widen and re-set bars (Recommended)"*): on six and twelve valleys they
passed or failed by the draw; on twenty-four —

| guard, on 24 valleys | bar | before water (D778) | the river (D779) | ✅ now (D780) |
|---|---|---|---|---|
| `FarmTests.AFarmBringsInMostOfWhatItSows` — brought in | 75 % → **70 %** | 79 % | 74 % | 75 % |
| `DesirePathTests.TheFoundingHubWearsLanesNotABlock` — the lanes' block against the corner-cutters' | 0.7 → **0.8** | 0.82 | 0.74 | 0.76 |

The lanes guard **was never met on a wide sample** — 0.82 before water — and is its own ⏸️ item: why the founding hub wears a
block at all. **Goldens:** **four constants in two goldens and one pin moved, each proven by ablation**: the two skill fifty-years, the two stock-limit fifty-years and the walking-clock pin (20 / 141 / 494 → 507) — all back to their old values with the founding's jars started empty (the dry sentence changes no state). The farm and Phase 5 goldens did not move.

### 2e. ✅ Step 6 — the view (D781)

**Water is on screen, and nothing in the sim moved for it.**

- **The household's card** has a water line under the handcart's: *"A jar of water, 87 of 120 — enough for 29 days."* while it
  lasts; *"… — less than a day's; somebody will fill it at their next rest at home."* when it is low enough to send somebody
  *(since D782, §2f: "… — 3 days' left; somebody will fetch more the next time one of them is home.")*;
  and, once it is dry, **the sim's own sentence in amber** (`SimWorld.WaterNoteFor`, §2d): *"No water — somebody will go at
  their next rest at home."* *(since D782: "… the next time one of them is home.")* / *"No water — Mabel has gone to the river."* Hidden for a household nobody lives in.
- **A person's NEEDS** has a fourth pill: *Water*, or *No water* in amber, with the household line as its tooltip. The jar is
  the household's, so every member reads the same.
- **The pail** (the art lane's to draw properly, §4 — this is the stand-in, in the map's ink and its water colour): an empty pail
  at the villager's side on the way to the well or the river, a full one while drawing and on the way home, put down at home.
  **Remembered by the view, not the sim**: water is not a good (call 1A), so nothing is carried in the sim's terms; the map
  watches the states a tick at a time (`VillageMap.CarryTheWater`, pure) — walked out, drew, walking home. Given up on the way
  with nothing drawn (a well pulled down, a bank gone under), the pail is put down, never shown full. A save loaded mid-walk
  shows no pail on that one walk: never a wrong one.
- **The probe's `water:` line** reads the household card full and dry (and amber), the pill both ways with its tooltip, the
  widest line against the card's width (the longest name gone to the river; a jar in the thousands), and the pail's whole walk
  posed — out, drawn, home, given up, dead. the `water:` line red-checked, four mutants, every one red where aimed: the dry line not amber **1**, the pill's words swapped **2** (dry and full), an empty pail carried home after giving up **1**, a draw that leaves the pail empty **2**; all reverted by marker, the diff read back.

### 2f. ✅ Played, and a jar filled before it runs dry (D782; Joe: *"looks good! proceed!"*, *"Fix it first"*, *"any time at home"*)

**Joe played it and it looked right — his game log said otherwise.** His two families ran dry in the first year and **stayed dry five years**, until a well; after that every jar ran fully dry before anybody went (19 dry spells for two families). Two faults in the rule, neither on screen:

1. **"Less than a day's" was only ever empty.** The jar of 120 divides by every family size from one to six and is drunk a whole day at a time, so it steps 6, 3, 0 and is never *less than a day's* until it is empty. **Now:** low at **`water_low_days` (5) of the family's drink or fewer** — data, measured.
2. **The trip was asked only where a rest began** — when an adult had run out of work, which in a young village no adult does: his came home thirty to fifty times each in those five years and every time went straight back out. **Now:** asked **first in `Decide`, whenever an adult stands at their own door with empty hands** — still never ahead of eating, warmth or the restock, never off a job under way.

**Measured** (`tools/harness/ZzWell.cs` with a dry-day count, D780's 100 valleys, paired; and his own save run on). Put to Joe with the price — about one more starved a valley in fifty unattended years, borderline, survivors unchanged, none on his village — against keeping the ask where a rest begins (his village 17 % dry); **he chose *"any time at home"*.**

| 100 valleys, 50 years, jar 120 | dry family-days | water time | Δ alive (± s.e.) | Δ starved (± s.e.) |
|---|---|---|---|---|
| D781 — empty only, asked where a rest began | **14.45 %** | 2.56 % | — | — |
| any moment at home, empty only | 8.74 % | 2.75 % | −0.43 ± 0.37 | +0.69 ± 0.60 |
| 5 days left, where a rest began | 8.61 % | 2.86 % | +0.31 ± 0.31 | +0.45 ± 0.51 |
| **5 days left, any moment at home (✅ Joe's pick)** | **1.59 %** | 3.10 % | **−0.10 ± 0.26** | **+1.20 ± 0.59** |
| 10 days left, any moment at home | 0.43 % | 3.48 % | −0.48 ± 0.28 | +0.67 ± 0.52 |

| Joe's village (his year-5 save, ten years on) | dry family-days | trips | water time | alive |
|---|---|---|---|---|
| D781 | **508 of 2,400 (21 %)** | 59 | 2.66 % | 11 |
| any moment at home, empty only | 208 (9 %) | 67 | 2.98 % | 11 |
| 5 days left, where a rest began | 406 (17 %) | 68 | 3.02 % | 11 |
| **5 days left, any moment at home (✅)** | **9 (0.4 %)** | 78 | 3.41 % | 11 |
| 10 days left, any moment at home | 0 | 96 | 4.22 % | 11 |

**The sentences follow the rule:** *"… has run out of water — somebody will go for it the next time one of them is home."*, *"No water — somebody will go the next time one of them is home"*, and the card inside the margin: *"A jar of water, 9 of 120 — 3 days' left; somebody will fetch more the next time one of them is home."* **A family with no house is told the truth** (Joe, Year 19: *"It shows these people (oakland underwood) 'resting at home' in the field?"*). His save says the Underwoods had no house — their "home" was the founding site (1, 0), the cart long gone — and their jar was empty with no way to fill it (a trip sets out from a door). The card now reads *"resting in the open — no house yet"* / *"walking back to where they sleep — no house yet"*, and their dry jar *"No water — somebody will go once they have a house"* (the log line too; low, *"nobody can fetch more until they have a house"*). Words only: no state, no golden; the probe's `relief:` line accepts *"climbing back to where they sleep"* as well as *"climbing home"*, since its posed founder has no house yet.

**The lanes bar 0.8 → 0.85** (`TheFoundingHubWearsLanesNotABlock`, at Joe's pick, as put to him): families fetching before the jar is dry wear the hub to **510 block tiles against 637 — 0.80**, four tiles over 0.8; still just over what the game does, the hub's block still his ⏸️ item.

**And the pail on the painted people.** The art lane's people (A93) reached `main` while this slice was open; on the default look they are drawn instead of the dots and returned before the dots' pail, so a plain merge would have hidden it. `main` was merged into the slice (904b658) and `DrawThePeople` hangs the same pail at a person's hip. ⚠️ No probe sees that draw call: the hook scores zero, written down — Joe's eyes.

**Guards:** `AnAdultAtHomeGoesForALowJarBeforeWork`, `TheJarIsLowAtItsLastDays` (the margin's exact edge), `AFamilyWithNoHouseIsToldTheTruth`; `TheJarSaysWhenToGo` at the margin; `EveryDrySpellIsSaidOnce` reads a tick's sentences before its refills (a jar can now be said dry and filled on one tick). **sim, 5 mutants, every one red where aimed:** the at-home ask removed **2**, the margin's edge back to *less than* **1**, the margin back to one day **1**, the unhoused dry note ignoring the house **1**, the unhoused rest ignoring it **1**; **view, 1:** Demolish back to the rounded tile **1** (`demolish:`). All restored by bytes, 0 markers.

## 3. ✅ Joe's four calls — all as recommended (D776, *"All as recommended"*)

**Answered:** 1A (a number the household keeps, not a good), 2A (the nearer of a well or the river bank), 3A (nobody dies of
thirst; a dry household goes at once and says so), 4 (the jar and the drink measured, then his pick). The options as put to
him stand below.

### Call 1 — What water is

- **A (recommended): a number the household keeps at home**, beside its larder: filled at the well or the river,
  drunk by every member each day. **Not a good**: never hauled to a warehouse, stocked at the market, traded or
  limited on the stock bar. It needs none of that machinery, and none of it would mean anything for water.
- B: a good (`Goods.Water`) in the larder. It brings the whole goods machinery with it, and a stock-limit row nobody
  would ever set.

### Call 2 — Where water comes from

- **A (recommended): the nearer of a well or the river bank.** Every valley has its river (D547). With no well, a
  household walks to the bank. **The well's payoff becomes walking saved**, the market's shape: build one near the
  houses and the trips shorten. Drought can later lower the river and leave the well, which is the answer D656 asked
  for. ⚠️ **Every golden moves**: every village, well or no well, now walks for water.
- B: only a well. Then a village without one either has no water (and call 3 must punish it) or the need does
  nothing until one is built. A need that does nothing before a building is a building's tax, not a need.

### Call 3 — What running dry does — ✅ A, and then *"Say it, don't rush"* (Joe, D780, §2d): no trip ahead of work

- **A (recommended): nobody dies of thirst; a household that runs dry goes at once.** It sends somebody ahead of work,
  like the emergency restock, and **says so**: the household card reads *"No water — Dovlow has gone to the river"*,
  and the log says it once per dry spell. The cost is the hours walked, and you can see whose. Health (Phase 6, item 5)
  can later read *where* the water came from: the well is its door.
- B: thirst like hunger, which can kill. A second way to die, which §0.1 admits only if foretold and answerable. It
  would be answerable (a well), but it is a death spiral D207 refused for morale, and it lands before health exists to
  explain it.

### Call 4 — The numbers, measured before his pick — ✅ `water_jar` 120 (Joe, D779: *"120 (Recommended)"*, §2c)

`water_per_person_per_day` and `water_jar` (how much a home holds), and so how often a household goes: a family of two
with a jar of 40 at one a person a day goes every 20 days (today's clock), and a family of five every 8. **Measured
first, as D427 was:** villages alive at 50 and 100 years at three or four jar sizes, against §9.12f's baseline and
against today's main. **After step 1's trace**, so the number is not picked on a cost nobody understands.

## 4. What the art lane will draw (D774's rule: every systems spec names it)

Under the recommended calls:

| Kind | What | New? |
|---|---|---|
| Buildings | the well | exists (D427); no new building |
| Goods | none: water is the household's, not a good (call 1A) | — |
| Villager states | `WalkingToTheWell`, `DrawingWater` | exist |
| Villager states | **walking to the river, drawing water at the river bank** | ✅ new (call 2A): `WalkingToTheRiver`, `DrawingAtTheRiver` (D779) |
| Villager states | **carrying water home** (a pail, or a yoke and two pails) | new — ✅ a stand-in pail drawn by the map (D781, §2e: empty going, full coming home); the art lane's to draw |
| Map | the lane from the houses to the bank, worn by the feet | the path wear that exists; no new paint |

Under call 1B the stock bar would also want a water icon.

## 5. The order of work (after his calls)

1. ✅ **Trace D428's open question (D777, §2a): it does not reproduce — the trip comes out of rest; winter walks chill.**
2. ✅ **Water at home, drunk, at size nought (D778, §2b).** The household's water and its daily drink, the trip filling it. Shipped
   at sizes that reproduce today's 20-day clock for a family of two, so the change is the mechanism, not the price.
3. ✅ **The river bank** (call 2A; D779, §2c): the nearest walkable tile beside the river by the shared cost field (§2.6, never a
   second distance), a well preferred when nearer.
4. ✅ **Running dry** (call 3; D780, §2d): ~~the trip ahead of work,~~ *(built, measured at −0.56 alive, and taken out at Joe's
   word)* the card line (the sim's sentence; drawn at step 6), the log sentence.
5. ✅ **Measured** (call 4) at three or four jar sizes; Joe picks. *(Done at step 3, D779, because the river made step 2's jar too dear
   to ship: river arms at 40, 120 and 400 and wells at 40 and 120 over 100 valleys, the farm guard at 40, 80, 120 and 200 —
   Joe: 120. The 100-year reading is not taken.)*
6. ✅ **The view** (D781, §2e): the card's line and NEEDS, the carried pail (the stand-in, until the art lane's), the probe line.
7. **Saves and the hash**: ~~the household's water saved and hashed; `water_drawn_on_day` retired with a save-format
   step;~~ **done at step 2 (D778), with no format step** — the jar is an optional key, and a save without it reads empty; the
   goldens re-taken in one commit, the diff read *(step 2 re-took those a well reaches, §2b; ✅ step 3 the rest, §2c — every one
   proven by ablation)*.

## 6. Acceptance

1. A household's trips follow its size: five go more often than two, at the same jar.
2. A well nearer than the river shortens the trips; built far off, it is not used.
3. Running dry is said before anybody walks, and nobody dies of it (call 3A).
4. Hunger and cold still come first by structure, as D427 guarded.
5. Villages at 50 and 100 years within the noise of §9.12f's baseline at Joe's numbers, or the cost explained.

## 7. Definition of Done

This spec current; content in data (`water_per_person_per_day`, `water_jar`); the guards red-checked and counted; the
determinism golden re-taken in one commit and the diff read; the four checks; Joe plays it; `DESIGN.md §6`, §7,
`HANDOFF.md` and `content-inventory.md` updated.
