# Spec: The market stand, learned by the long walk (W14)

> Status: **✅ EVERY CALL ANSWERED (D852, 2026-11-22) — Joe: *"a market stand should have its own marketer - only 1 though.
> everything else i agree with"* — calls 1, 3, 4, 5 as recommended, call 2 his (§5.3). 🔨 Building, §7's order: ✅ step 1, a marketer's own
> counter first (D853); ✅ steps 2–3, the stall and the long walk (D854) — ⏸️ step 4, the view, waits on Joe closing his game.**
> *(Read "🔨 Building … nothing built yet" until D853, "✅ step 1" until D854.)* *(Read "📏 MEASURED AND SPECCED (D851) — ⏸️ FIVE CALLS JOE'S (§9); NOTHING BUILT" until D852.)* Claimed on
> `slice/w14-market-stand` (Joe: *"start the market stand"*). Built from D839, his shape and his unlock (*"The long walk"*).
> The instrument is `tools/harness/ZzMarketStand.cs` (§4), run on the newest save of each of his 182 folders (a copy) and on
> 16 unattended valleys. **⚠️ The measurement found that a stand built on today's market code is not stocked** (§4.3), so
> call 1 is a rule the build needs before anything else works.
>
> Phase 6, W14 of the workplan (`systems-complete.md §5.1`, queued after W3 by D839).
>
> Neighbours: `logistics.md §5.5–5.7` (D669–D671: one market a village, the shopping walk, `market_detour_ticks`),
> `storage-and-distribution.md §14.9` (D372 the market is a shop; D612 its room and limits), `handcart.md §13` (D692–D698:
> the market's cart, the first leg), `workshop-and-the-carpenter.md §4.1a` (D676: a learned flag, hashed only when true),
> `house-tiers.md` (a latch learned from the village's own history), `morale.md` (W2 — the shopping walk is **not** one of
> its causes, so a stand moves no one's contentment).

---

## 1. Why

Every one of Joe's established villages has **one market**, placed early while the housing paint spreads (D669). The
homes painted later end up far from it, and from the granaries beside it. A second market is already buildable, but
nobody builds one into the housing: it is 2×2, two seats, 35 logs and 30 stone, and it reads as the village's shop
rather than as a corner one.

Joe (D839): *"a smaller version of a market that staffs one marketer and holds half of what a market usually does, that a
player can place in a tighter location to serve villagers' needs."* Learned when the far houses have walked far enough
for long enough that **the village itself asks for it**, which is §2.7's *unlock by doing* in a sentence the log can say.

## 2. Pillars and non-negotiables

- **§2.2 Smart labour, distribution is a job** — the stand is the market's own building at a smaller size: one marketer,
  hired as any marketer is, restocking from storage (D372), and never delivering to homes (deliveries stay deleted).
- **§1.1 Legibility** — the unlock names a person, the good and the days she walked; the stand's card says who keeps it,
  how far it is from the storehouse it fills from, and when nobody keeps it, why (§5.5).
- **§1.2 Meditative pace** — nothing to babysit: placed once, kept by the village. ⚠️ Call 2 is the one place it could
  ask the player for a second step (the professions number).
- **§0.1 Cozy but challenging** — a convenience, not a lifeline: switching a stand off costs the far houses some walking,
  never lives (D143's promise for the market, extended). §4.3's numbers say how big that convenience is.

## 3. What exists today, read off the code (2026-11-22)

- **The market is a row** in `SimConfig`'s building list: `BuildingKind.Market`, `Stores = StoreKind.Market`,
  `StoreCapacity` 8,000 (`market_store_capacity`), `Seats` 2 (`market_capacity`), **2×2** (D382; D839's *"3×3"* is
  corrected here), 35 logs, 30 stone, 50 work ticks.
- **Every rule a shopper or a marketer follows reads `StoreKind.Market`, never `BuildingKind.Market`**, so a second
  building kind whose row stores as a market is treated as a market everywhere: `NearestShopHolding` (D372, D671),
  `CounterShortOf`, `OfferMarketRestock`, `NearestCounterWithRoomFor`, `MarketersWanted`, the cart (`IsACartOwner`, D692)
  and the first leg (D696). **That is what "the market's code, not a new kind of shop" means in practice.**
- **A market's limits** (D612): an unset good keeps `market_starting_limit` (1,000), shared out of the room in the
  catalogue's order (`StoreBuilding.Keeps`). `StartingLimit` is handed to every market by `SimWorld.KnowItsGoods` from the
  one config number, so a stand needs its own number there.
- **Where a marketer's load goes:** `OfferMarketRestock` chooses the good on the **emptiest shelf of any market** (D601),
  fetched from the storehouse nearest the marketer; `StoreForTheLoad` then puts it on **the counter nearest where the
  marketer stands**, with room under its limit (`NearestCounterWithRoomFor`). The errand never says which counter asked
  for it. With one market a village, the two are the same counter. With two, they are not (§4.3).
- **Who keeps a market:** `LabourQuota` wants a marketer per errand (`MarketersWanted`: a short counter, a dead larder, a
  buffer), capped by the seats and, when the player has typed one, by **the professions number** (`JobLimits`, D51,
  `Asked`). The sim never raises a number the player typed.
- **A learned flag** has one shape (D676): a statistic, unhashed, counted where the state changes; the flag set from it once,
  never per tick; hashed as a tag byte when true; saved only when set.

## 4. Measured — `tools/harness/ZzMarketStand.cs`

Kept in `tools/harness/`, copied into `tests/` to run, deleted (D420). A **trip** is one villager's spell from setting out
`FetchingFromStore` to arriving home again; its store is the one they turned at, its good what they carried. Two readings
of its length: **elapsed** (the ticks it took) and **planned** (twice the home → store walk in the one cost field — what
the sim can compute at the counter with no new state). A day is 4 ticks; a year 480 (30-day seasons).

**Saves:** the newest `.save` of each of Joe's 182 folders, copied to the scratchpad. **112 open** (70 are older formats,
refused in words), **40 are lived in** (a household with a home). Each stepped 5 years. **Control:** the harness valley
(shipped config, the played opening, a granary and a warehouse at Year 3), seeds 1–15 and 12345, 50 years — the
golden-shaped village.

### 4.1 How long the trips are

| homes | villages | year (median) | round trip p50 | p90 | longest (median) | longest of all |
|---|---|---|---|---|---|---|
| 1–4 | 28 | 5 | 15 ticks (4 days) | 22 | 26 | 53 |
| 5–9 | 7 | 22 | 18 (4½ days) | 30 | 36 | 70 |
| 10–19 | 4 | 40 | 23 (6 days) | 31 | 45 | 83 |
| 20+ | 1 | 84 | 25 | 34 | 46 | 46 |

**Trips of 10 days or more (elapsed)** come only once a village has homes far out: none at 1–4 homes in 84 village-years
(6% have one of 8+ days), 28% of village-years at 5–9 homes, 50% at 10–19, both of 20+'s. **12+ days:** 2%, 8%, 44%, 0%.
One village makes most of them — **Oakhaven-sumac-sumac-89** (Year 41, 18 homes, 38 adults): 52 trips a year, 11.8 of them
10+ days and 7.5 of them 12+, from 5–7 different households a year. Its longest: *Cortis, 15.8 days for firewood*, from the
west end to the warehouse in the east.

**Planned and elapsed agree** to within a trip or two a year at every threshold. The planned walk is slightly shorter
(no shelter breaks, no waiting) and, unlike elapsed, never counts a trip that was long because the walker stopped.

### 4.2 When "the long walk" would be learned

The unlock counts **households, not trips** (D839: *"enough households have made a very long shopping trip"*): one far
family shopping often must not unlock it, several far families must. Within the 5 stepped years, the number of saves where
the count of different households reaches N, by threshold:

| a trip of at least | N = 3 | N = 5 | N = 8 |
|---|---|---|---|
| 8 days, planned | 5 of 40 (homes 3, 7, 18, 18, 23) | 4 (7, 18, 18, 23) | 3 (18, 18, 23) |
| **10 days, planned** | 3 (7, 18, 18) | **3 (7, 18, 18)** | 0 |
| 12 days, planned | 3 (7, 18, 18) | 1 (18) | 0 |
| 10 days, elapsed | 4 (7, 18, 18, 23) | 3 (7, 18, 18) | 1 |

- **10 days planned, five households** fires in the three saves that have far houses: Ashbourne-yarrow-clover-89 (7 homes,
  Year 25), Oakhaven-sumac-sumac-89 (18 homes, Year 41) and Hollowmere-rook-hare-85 (18 homes, Year 69). It fires in **none
  of the 28 villages under five homes**. The window is 5 years from each save, so in play it lands earlier than the save's
  year: about when the far houses are built, not at the founding.
- **The control never crosses it:** in 16 unattended valleys × 50 years **no household makes a planned 10-day trip**.
  By elapsed time one valley (seed 9) reaches 3 households by Year 37 and 4 by Year 50, so **planned is the safer reading for the goldens** as
  well as the honest one.
- Hollowmere-honey-harrow-82 (23 homes, Year 84) has five homes whose median trip is 8+ days and still only one household
  past 10 planned: a big village laid out compactly does not ask for a stall, which is right.

### 4.3 The open question — do far households switch to a stand near them?

**Posed:** in every lived save with at least two homes whose median trip is 8+ days (four saves), the save opened again and
a finished market **raised** as near those homes' medoid as one would stand, told D839's shape (4,000 room, 500 a good,
one seat, a new cart), then stepped the same 5 years. Four arms, each against the save untouched:

| far homes' walking, 5 years (ticks) | untouched | **today's code**, professions +1 | **+ own counter** (call 1) | always stocked (the ceiling) |
|---|---|---|---|---|
| Ashbourne-yarrow-clover-89 | 2,221 | 875 | 1,113 | 923 |
| Hollowmere-honey-harrow-82 | 2,485 | 1,650 | 1,590 | 1,532 |
| Oakhaven-sumac-sumac-89 | 4,129 | **4,364** | 3,002 | 2,149 |
| Millbrook-bay-larch-19 (a dead valley) | 760 | 663 | 663 | 495 |

*"Professions +1"*: the save's typed marketer number raised by one, as a player would (Oakhaven's, Hollowmere's and
Ashbourne's had one typed; in a two-year trial without the raise **Oakhaven's stand stood with its seat 65% empty and a
mean of 3 on its shelf**). *"Own counter"*: a TEMPORARY patch (`tools/harness/market_stand_own_counter.py`, applied for the run, reverted, never
committed) — a marketer stocks the counter of the market they work at first. *"Always stocked"*: the shelf topped up to its
limits each tick **out of the storehouses** (no food made), the most a stand could do.

- **⭐ Yes — far households switch, when the shelf holds the good.** With the shelf stocked, 31 of 37 far trips in
  Oakhaven's two-year trial turned at the stand, the median far round trip fell 37 → 20 ticks and only 2 of 92 far trips in
  five years walked past it. D372's rule does the work unchanged: the nearest market stocked for the trip wins, and the stand
  is the nearest.
- **⛔ But today's code does not stock it.** In Oakhaven the stand's marketer fetches from a granary 20 tiles away and puts
  the load on **the counter nearest the granary — the big market** — so the stand's shelf held a mean of 101 and was short
  of a cart-load 2,093 of 2,400 watched ticks; far households walked past it 69 times in 82 and **walked more than with no
  stand** (4,129 → 4,364). It only worked where the stand happened to stand nearer the source granary than the market did
  (Ashbourne, Hollowmere). **That is call 1.**
- **With the own-counter rule** the stand fills in all three living villages: Oakhaven's far walking 4,129 → 3,002 (the
  ceiling is 2,149), the stand's shelf held a mean of 164, and 41 of 102 far trips turned there.
- **⚠️ The walk moves onto the keeper, as D839 said — and the keeper walks about twice what the far families save.** The
  stand's marketer spent ~85% of their ticks collecting and stocking in every village, about **400 ticks a year**, fetching
  from storehouses as far from the stand as the far homes are. Far households saved **180–225 ticks a year** in the three
  living villages (the always-stocked ceiling: 190–400). **So the stand does not make the village more efficient; it gives
  the far families their time back and spends more than that of one adult's doing it.** That is the shape of a market already (D143: a
  marketer produces nothing, and switching it off costs convenience, never lives), and it is said on the card (§5.5).
- Alive after five years, stand against untouched: 25 / 26, 77 / 77, 55 / 59 (own counter): within the noise of a
  perturbed run (D777's method was not run here; nothing is claimed for or against).

### 4.3a Measured after — the real stall (D855, §7 step 5)

`ZZ_REAL=1`: the real `BuildingKind.MarketStall`, learned and raised by its own row at the far homes' medoid (2×1, so it
stood 5–8 ticks off where the 2×2 pose stood 6–13), left as built — step 1's rule alone (no patch), its own marketer outside
the professions number (Joe's call 2), **the typed number not raised**. Same saves, same 5 years, the same far homes:

| far homes' walking, 5 years (ticks) | untouched | D851's pose (both patch halves, number +1) | **the real stall** | always stocked |
|---|---|---|---|---|
| Ashbourne-yarrow-clover-89 | 2,221 | 1,113 | **1,327** (48 of 64 far trips at the stall) | 923 |
| Hollowmere-honey-harrow-82 | 2,485 | 1,590 | **1,581** (27 of 64) | 1,532 |
| Oakhaven-sumac-sumac-89 | 4,129 | 3,002 | **3,183** (34 of 104) | 2,149 |

- **⭐ The seat is kept without the player's help:** 100 %, 100 %, 99 % of the watched ticks with the typed number as Joe left
  it (D851: 35 % in Oakhaven until it was raised). Call 2 doing what he asked.
- **The far walking falls 23–40 %**, from 1 % better to 19 % worse than the pose that had both patch halves and a raised number; the
  difference is the patch's load half (D853), which this build does not have.
- **The keeper still walks** — 80–90 % of their ticks collecting and stocking, as §4.3 said. Alive after five years, stall
  against untouched: 26 / 26, 75 / 77, 58 / 59 — inside the noise of a perturbed run, nothing claimed.
- Millbrook-bay-larch-19 is a dead valley (nobody alive): its stall had nobody to seat, and its row is left out.

### 4.4 What the numbers are not

- **Not Joe's biggest villages.** The D669 villages with 36 homes and 21–27-day trips are format 8–11 and refused; the
  largest lived save today has 23 homes. The rule is set where his current villages are, and §9 call 3's number is a knob.
- **Not a 2×1 stand.** The pose raised a 2×2 market (the only market there is), which stands up to 13 ticks from the far
  homes' medoid where a smaller footprint would sit closer. The ceiling column is a floor on what a tighter stand gets.
- **Not the near homes.** Near homes shop where they did; the instrument counts only the far homes' trips for the walking
  table.

## 5. The rule, as proposed

### 5.1 The stand — a building row — ✅ built (D854)

`BuildingKind.MarketStall` (*"market stall"* — the code's word; *stand* and *stall* are one building), appended at 33.
⭐ **The marketer's trade works at two buildings**, which nothing did before: `JobRow.AlsoWorksAt` (on the job, so the relation
is still stated in one place and `BuildingsCatalog.EmployedBy` indexes both columns), and **`Workplace.RaisedAs`** — the
building a workplace stands in when it is not its trade's own, the house tiers' precedent (`Household.HomeKind`) — read
through one door, `SimWorld.BuildingKindOf(Workplace)`, at the seven places that derived a workplace's building from its
trade (its row for wear, its door, its seam, its name on the map, the refounding count, the must-be-near rule, `KindOf`).
*As specced:* `BuildingKind.MarketStand`, appended. Its row: `Stores = StoreKind.Market`, **`Seats` 1**, **`StoreCapacity` 4,000**,
**a starting limit of 500 a good**, footprint **2×1** (call 4), half the market's price (call 4). Knobs in
`data/sim.config.json`: `market_stand_capacity` 1, `market_stand_store_capacity` 4000, `market_stand_starting_limit` 500,
`market_stand_logs`, `market_stand_stone`, `market_stand_work_ticks`. The store remembers it is a stand so
`KnowItsGoods` hands it the stand's starting limit.

- **What it keeps:** every good a market keeps, at 500 each, shared out of its 4,000 in the catalogue's order (D612's
  rule, untouched). **Today a market counts six goods** (forage, firewood, fish, meat, wheat, barley — read off Oakhaven),
  so a stand keeps 3,000 of its 4,000. With bread and flour learned it counts eight and fills it; a ninth good keeps
  nothing at a stand until the player lowers another limit, and its row on the card says so (D612's caption).
- **A handcart like the market's** — no new rule: `IsACartOwner` reads the trade, so the stand owns one, the carpenter is
  asked for it (`HandcartsWanted`), and its keeper walks to it first (D696).

### 5.2 How it is stocked — call 1

**✅ Answered A (D852): a marketer stocks the counter of the market they work at first — ✅ built (D853).** In
`OfferMarketRestock`, when the marketer's own counter wants stocking, only its shelves are offered; when it is full to its
limits, every market's are, as today. **The plan is the whole rule:** the load already goes to their own counter —
`StockingTheMarket` walks to their workplace and `PutItInTheMarket` puts it on the counter they stand at.

- ⛔ **The first cut also changed `StoreForTheLoad`** (the measurement patch's second half) **and moved a one-market
  village**: that path is a marketer's OTHER loads — a buffer, a dead larder — and it takes the nearest *store* of any kind
  with room, so sending them to the marketer's own counter instead changed where an unattended village's buffers went
  (`MarketTests.TheMarketKeepsLardersFromRunningDry`: a 205-tick dry spell). Ablated half by half: the plan half alone left it
  green, the load half alone turned it red. Deleted, and written into the rule's comment. ⚠️ So §4.3's own-counter column
  was measured with both halves; step 5 re-measures with the rule as built.

- **With one market a village — every golden and every save Joe has — this changes nothing**: the marketer's own counter is
  the only counter (§6).
- It makes the card's sentence true: *"kept by Alys"* means Alys fills it.
- **(B) leave it** — the stand works only where it stands nearer the source storehouse than the market does (§4.3's
  Oakhaven made the far walk longer). **(C) a stand restocked from the market** — refused by D372 (*"a market is not a source
  for a market"*), recorded so it is not re-proposed.

### 5.3 Who keeps it — call 2, Joe's (D852): *"a market stand should have its own marketer - only 1 though"*

**⭐ As answered: each stall has its own marketer — one — outside the professions number.** The player's typed number for
marketers counts **the market's** marketers; a stall's one seat is wanted on top of it whenever that stall's counter wants
stocking (D36's rule — bounded by errands, never by spare hands: a stall with every shelf at its limit asks for nobody), and
it is matched **before** the market's seats, so the stall's keeper is never the hand the market took. The keeper is a
marketer — the same trade, colour, skill and card — and still has to come out of the free hands like any trade: in a
village with nobody spare, the stall's card says so in the allocator's own words.

- The professions panel's marketer row says the stalls apart: *"2 — and 1 at the stall"*, so the count on the row never
  reads past the number typed.
- *Why not the recommendation:* the measurement (§4.3) left the stall's seat 65% empty under Oakhaven's typed 2, and Joe's
  answer is that a stall that has to be asked for twice is not the corner shop he meant.

*The call as put (D851), kept for its reasoning:* The stand's seat is a marketer's seat. When the player has typed a
professions number for marketers, the village hires no more than that number (D51), and the market's two seats are nearer
most hands than the stand's one. Measured: Oakhaven's typed 2 left the stand's seat empty 65% of the time.

**Recommended (A): leave the player's number alone, and say it.** The stand's card, with its seat empty: *"Nobody keeps this
stall — the professions panel asks for 2 marketers and both are at the market."* The log says it once when the stand opens
with the number full. **(B)** raising a stand raises a typed marketer number by one, said in the log — fewer clicks, but
the sim would be changing a number the player typed, which nothing in the game does today.

### 5.4 The long walk — call 3 — ✅ built (D854): `SimWorld.MarketStallLearned`, `HouseholdsThatWalkedFar`, `Household.WalkedFar`; knobs `market_stall_walk_ticks` 40, `market_stall_households` 5; the Lore node *"The market stall"* (`TechCondition.LongWalk`)

**Counted at the counter, not per tick:** when a shopper collects at a store (`CollectFromStore`), and
`2 × TicksBetween(their home, the store)` is at least **`market_stand_walk_ticks` 40** (10 days), their household is marked
as having **walked far** (`Household.WalkedFar`, once; a bit, so a household counts once however often it walks). The
village's count (`SimWorld.HouseholdsThatWalkedFar`, a statistic) rises by one. When it reaches
**`market_stand_households` 5**, `SimWorld.MarketStandLearned` is set, once, and the log says:

> *"Hilde walked ten days for fish; the far houses ask for a stall."*

The name is the walker whose trip was the fifth household's, the good is what they collected, and the days are that trip's
planned round trip, rounded down. Any store counts (a granary as well as a market): the walk is the household's, whatever it
walked to.

- **Before it is learned**, the build bar's stand says *"Not yet — 3 of 5 households have walked ten days to shop"* (the
  workshop's progress line, D676's shape).
- A village loaded with a lowered knob learns it at the next qualifying collection, not at load (the workshop's rule).

### 5.5 What the player sees

- **The build bar:** *market stall* beside the market, its tooltip the progress line until it is learned.
- **The stand's card** is the market's card (its limits panel, its cart line, its keeper), with two lines of its own:
  *"Serves the homes nearer it than the market"* and, with its seat empty, why (§5.3: nobody spare, or nothing to stock). ⚠️ Not a walk length on the
  villager card (D432): the home card already says where a household shops and how far (D744), and a stand changes that line
  by itself.
- **The log:** the learning sentence once; *"The far houses have a stall"* when the first one opens.

## 6. Determinism, the hash, saves, goldens

- **Hashed:** `MarketStallLearned` as a tag byte when true (D676; `0x8B`), and a workplace's `RaisedAs` sparsely under `0xF4` (D854). The stand itself is a building and a store, hashed as
  every building and store already is. **Not hashed:** `Household.WalkedFar` and `HouseholdsThatWalkedFar` — statistics the
  flag is set from, `LogsEverFelled`'s shape; the gap (two villages at 2 and 4 households hash alike) is asserted on purpose
  by a guard, as D676's was.
- **Saved, each only when set:** `market_stall_learned` (the world; specced as `market_stand_learned`), `walked_far` (a
  household), `households_that_walked_far` (the world), and `raised_as` on a stall's workplace (D854). **The format stays 13**: a save without them reads as nothing learned and nobody
  walked far, the house tiers' and contentment's precedent (`save-load.md` table). `EveryFieldIsSavedOrNamed` names them.
- **No golden should move, for four reasons, each checked at the build:** (1) no golden run learns the stand — the control
  never makes a planned 10-day trip in 16 valleys × 50 years (§4.2); (2) the statistics are unhashed; (3) call 1's rule
  changes nothing in a village with at most one market — the build greps every golden's pose for a second market first;
  (4) a new building row appended changes no hashed list unless one enumerates the catalogue — checked by reading the hash
  for every loop over `BuildingKind` before typing the row. **A golden that moves is read, not re-pinned**, and its cause
  named by ablation (D697's method).

## 7. Build order — one step a commit, TDD

1. **Call 1's rule** alone, behind nothing, with its guards (a two-market village: each marketer fills their own counter;
   a one-market village unchanged, byte for byte against `main`) — the step a golden would move at, so first and alone.
2. **The stand's row and knobs**, ungated: the store's own starting limit, the seat, the footprint; guards that it is a
   market to every rule (a shopper takes it, its keeper fills it, its cart is fetched).
3. **The long walk:** the household bit, the count, the flag, the gate, the sentence; guards for each, red-checked and
   counted (D326).
4. **The card, the build bar's line, the log lines**; probe lines for the card.
5. ✅ **Measured after (D855, §4.3a)** on the same saves (`ZzMarketStand`, the stand raised by the real row): the far walking against §4.3's
   own-counter column; then the four checks; then Joe's play.

## 8. For the art lane (D774) — to HANDOFF's 🅿️ ART section, as a request

- **The market stall**, 2×1 (call 4): a painted building in the market's family but plainly smaller — an awning and a
  counter, not a hall.
- **Its build-bar icon**, beside the market's.
- No new villager state (the keeper is a marketer and wears the marketer's colour) and no new good.

## 9. Joe's calls — ✅ answered 2026-11-22 (D852)

> Joe: *"a market stand should have its own marketer - only 1 though."* / *"everything else i agree with"* — **calls 1, 3,
> 4 and 5 as recommended; call 2 his: each stall has its own one marketer, outside the professions number** (§5.3).

### The calls as put on 2026-11-22 (D851)

1. **How it is stocked.** As the code stands, a marketer puts what they fetch on the counter nearest the storehouse, so a
   stall out among the far houses stays nearly empty and the far families walk past it (in Oakhaven they walked *more* than
   with no stall). **Recommended A: a marketer fills the counter of the market they work at first** — the stall's keeper
   fills the stall. Changes nothing in a village with one market. *(B: leave it; C: fill it from the market — refused by D372.)*
2. **The keeper and your professions number.** If you have typed how many marketers the village wants, a new stall gets
   nobody until you raise it. **Recommended A: leave your number alone and have the stall's card say so** (*"Nobody keeps
   this stall — the professions panel asks for 2 marketers and both are at the market"*). *(B: raising a stall raises your
   number by one, said in the log.)*
3. **The long walk's numbers.** **Recommended: a shopping trip of 10 days there and back, by the walk from the home to the
   store, made by 5 different households** — measured to light in your villages that have far houses (7–18 homes) and in
   none under five homes, and never in an unattended valley. *(Fewer households or shorter trips light it in smaller
   villages; 12 days lights it only in Oakhaven-sumac.)*
4. **Its size and price.** **Recommended: 2×1, 18 logs and 15 stone, 25 work ticks** — half the market's price as it is half
   the market. *(Or 1×1, which fits between any two houses; the counter and the awning would be one tile.)*
5. **Is it still wanted, knowing the trade?** Measured: with a stall, the far families get back about 50 days of walking a
   year between them (180–225 ticks), and its keeper spends about 100 days of theirs (about 400 ticks) fetching to fill it. It
   is a convenience the far houses notice, not a saving the village banks, and the card will say that its keeper produces
   nothing. **Recommended: build it** — it is the market's
   own bargain at a smaller size, and it gives the player a placed, legible answer to the far houses. *(Or park W14 and go
   to W4, the cemetery.)*

## 10. Acceptance (Definition of Done)

1. This spec current, its status line true against §7 and the suite.
2. Every rule in §5 with a guard, red-checked and the reds counted (D326); a zero written down, never hidden.
3. The determinism test green; no golden moved, or each mover named by ablation and agreed with Joe.
4. The four checks (CLAUDE.md) on the slice's final tree.
5. §7 step 5's after-measurement written into §4.
6. Joe's play: a stall placed among his far houses, its keeper filling it, the far families' home cards naming it.
