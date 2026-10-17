# Spec: Trade — the living region, a post on the river, and barter by standing order

**Decisions:** D29 (2026-07-26, firewood is the first named tradeable), D61 (2026-08-01, *"I don't want animals available
to the user until they trade for it"* — livestock is blocked on this), D252 (2026-08-29, the town hall is the last
founder's death), `buildings-plan.md §6` (gold cut: *"no combat, no score, no wealth condition"*), D546–D551 (bridges —
a deck over the river), D553 call 6 (the workshop, carpenter and hammer: the follow-on carts wait for), D563–D584
(nomads — the clock-by-hash and the walk-in from the valley's edge this copies), **D586 (Joe's calls for this spec,
2026-10-16 — §3.1)**, **D587 (his answers to the nine calls, the same day — §3.2)**.
**Status:** 🔨 **COMMIT 1 OF §11 BUILT (D590): THE ROWS AND THE STATE, NOTHING BEHAVES; EVERY CALL ANSWERED (D586–D587); on `slice/trade`.** Measured first (§8,
`tools/harness/ZzTrade.cs`). ⭐ **Trade and the herd are built together (call 5)**; the build is planned commit by
commit in **§11 (D588)**, with `livestock.md` re-read against today's code (its §0). Owner: Joe + Claude Code.

---

## 0. Out of turn, said so

§2.4 is the **last** pillar of the long-run arc (DESIGN §4: skill → knowledge → pressure → *living region and trade*),
and Phase 5 still has open items (soil, house tiers, drought, fire and blight, the workshop). Joe picked trade next on
2026-10-16, as he picked the nomads out of turn (D563). §4's roadmap and D586 say so; nothing here pretends it is in
order.

## 1. Why this exists

- **§2.4's thesis:** *"The trading post shouldn't be a vending machine with a boat … Trade is a system, not a menu."*
  The village is a node in a small region, not an island.
- **A surplus with nowhere to go.** Today a valley's spare goods pack the stores to their limits and nothing in the game
  wants them (D61's note: *"nothing in the game wants anything"*). §8.2a: every played village at the hall's era holds
  nearly every good it makes at its limit.
- **Valleys that lack something.** Seams vary from valley to valley (D542–D543); a valley short of stone or iron has no
  answer today but bridges and luck. Trade is an answer the player chooses.
- **D61's blocker.** Animals arrive only by trade, so `livestock.md` (pasture, herd, hay, butchering) cannot start until
  this does.

## 2. What already exists to copy

| Need | Already built | Where |
|---|---|---|
| A building that must stand by water | the fishing hut's `must_touch: water` row column | `Building.MustTouch`, `SimConfig` fishing hut row |
| A visitor on a clock, never a draw | the nomads' knock: a year from a hash of the seed and a count | `SimWorld.Nomads.cs` `FirstKnockYear` / `NextKnockYearAfter` |
| Arriving from the valley's edge, view only | the walk-in (D573, D575) | `VillageMap.WatchForArrivals`, `ArrivalsSince` |
| Per-good numbers the player sets on a card | stock limits; the market's per-good shelf limits | `StockLimits`, `SimWorld.MarketStockLimit` |
| A worker who carries goods to a building and stocks it | the marketer | `VillagerState.CollectingForMarket` / `StockingTheMarket` |
| A gift that unlocks a building | the library, the town hall, the smithy | `CivicSystem`, `ATownHallIsOwed` |
| Names for places | `town_names` | `SimConfig.TownNames` |
| A moment that does not stop the village, and its log line | the passing banner | `Moment.WaitsToBeDismissed = false` |

## 3. Joe's calls

### 3.1 Answered (2026-10-16, D586)

1. **Scope: the full living region**, not a minimal post. A few neighbouring settlements with their own stocks and
   needs, rates that move, and the river (later a road) as real logistics. Built in slices (§9), each playable.
2. **Barter, no coin.** Goods for goods at a stated rate. `buildings-plan.md §6`'s cut of gold stands, and
   `tech-tree.md §9a`'s *"gold and barter value"* row is answered: barter.
3. **Standing orders.** On the post's card the player sets, per good, *keep N, sell the rest* and *buy up to M*. The
   village carries the goods; each visit settles; the player is told what changed hands. No per-visit trade screen
   (D587 call 6 keeps it so: the post's window shows the boat in dock and can send it on, nothing more).
4. **The unlock, in Joe's words:**
   - **The civic permission:** the town hall gift (the last founder's death, D252) — *"the village transitioning from
     a fragile homestead into an official, recognized settlement … granting the conceptual authorization for foreign
     trade."*
   - **The physical condition:** once the town hall stands, the trading post can be built, **only on a navigable water
     edge** (a river bank; a lake shore once lakes exist).
   - **Tier 2:** once carts exist (the workshop + carpenter + hammer slice), *"another wheeled caravan type-trader
     should show up from off-map, and connect a major cart path from off-map leading to the town hall."*

### 3.2 The nine calls — ANSWERED (2026-10-16, D587)

Asked with recommendations (D586); his answers, and what each means for the build. Four needed one more question,
asked the same day; those answers are in the last column.

| # | Question | Joe's answer | So |
|---|---|---|---|
| 1 | Post learned when the hall is owed, or raised? | **Raised** (*"yes"*) | Not on the build menu until the town hall stands |
| 2 | How many neighbours? | **3** (*"yes"*) — upriver, downriver, overland | The overland one waits for carts (tier 2) |
| 3 | How often does the boat come? | ***"every 2-3 years"*** | A clock by hash: the next visit 2 or 3 years on (§4.4). Rare, so each visit is an event |
| 4 | Who works the post? | **A trader job** (*"yes"*) | The marketer's machinery (§4.5) |
| 5 | What can be bought? | ***"a random assortment that includes everything that is or will be in the game - build trade and herd together"*** | Each boat brings an assortment from **every good the game has** — and each new good joins it the day its system is built (asked: *only goods in the game*, never a good that does nothing yet). **Trade and `livestock.md`'s first slice are one build**, so the first boat can bring animals (D61). "Random" is seeded: decided by hash, the same for the same seed (§4.3) |
| 6 | How is a visit told and seen? | *"trade boat visibly sails down river from one direction or the other (should be able to come from both directions), passing banner and log line. boat stops at trading post for a reasonable amount of time to be noticed by player (will likely have to test) and then a log line announcing it is leaving and it sails down the river off-screen. Or the user can dismiss it in the trade window/screen and it will sail away sooner."* | The boat **sails on the river from either edge**, a passing banner and a log line when it arrives, **docks long enough to be noticed** (a number found in play), a log line as it leaves, and it sails off the far edge. **The post's window** (asked: the post's own window, not a per-visit screen) shows the boat in dock and has **Send the boat on** (§4.4) |
| 7 | Do neighbours change? | (asked again, plainer) **Neighbours with reasons** | Each boat belongs to a neighbour with its own stock and needs; what it carries is what it has plenty of, its rates follow what it is short of, and the region panel says why (§4.2–4.3) |
| 8 | The cart road | ***"carts will eventually automatically create pathways as they are used (like 100% immediate heatmap pathing vs. whatever the villagers have) so one pass in a direction by a cart makes a path. so the trading cart drives in from off screen and makes a path as it goes."*** | **No road sites.** A cart's single pass wears its tiles all the way to a path. Asked when it speeds walking: **drawn at once, faster at the next spring re-pricing**, the rule every path follows (D358) (§4.7) |
| 9 | Keep trade at the hall (Year 51 at the median)? | **Yes** | Trade, and so animals, are late-game |

## 4. The rule

### 4.1 The permission and the post

- The trading post is a **building row** (never a kind check): `must_touch: water` (the fishery's column), a store with
  its own room (`stores`), one worker's seat, priced in logs and stone (numbers measured at build, §8.3).
- **Learned when the town hall is raised** (call 1). Until then it is not on the build menu and not in the tree.
- **Navigable means any river tile.** §8.1: in all 400 measured valleys the river runs to at least two edges of the map,
  so every bank leads off the map both ways. **A bridge never blocks the river** — a deck is high enough for a boat;
  said so in the post's card the first time a bridge stands downstream.
- *"Lake shore"* waits for a lake stage in the map generator; none exists today (one river, edge to edge).

### 4.2 The region

- **Three neighbours** (call 2): one upriver, one downriver, one overland (tier 2). **Neighbours are rows** (`data/`),
  so a modder can add one: a name (from `town_names`, by hash), a direction, **what it makes** (goods it has plenty of)
  and **what it wants** (goods it lacks), each with a size.
- ⭐ **Between them the three make every good the game has** (call 5: *"everything that is or will be in the game"*),
  and animals. A good added later is given to a neighbour's row the day its system is built, so the region grows with
  the game. ⛔ A good with no system yet is never offered (asked, D587).
- **Each neighbour keeps a stock per good**, in integers, **stepped once at each season turn** — its makes add, its wants
  take away, clamped to its size. ⛔ Never per tick (CLAUDE.md: nothing derivable is rebuilt per tick, and nothing in the
  region changes between season turns).
- **Hashed and saved** (save format **8**). A neighbour's stock is a fact about the world; the rate (§4.3) is derived
  from it and **not hashed** (D335).
- **Read on a region panel** (a town hall tab, when the tabs land; a window until then): each neighbour, what it has
  plenty of, what it is short of, and its rates in words.

### 4.3 Barter rates

- A neighbour **gives what it has plenty of for what it is short of**. The rate for a pair is derived from the two
  stocks against the neighbour's size, clamped to a band (e.g. 1:4 to 4:1), in integers.
- **Said in words, with the reason:** *"Ashford is short of firewood after the winter: 3 firewood buys 1 stone."*
- Rates change only at season turns, so the region panel can be read and planned against long before a boat comes.
- ⭐ **What a boat carries — Joe's *"random assortment"* (call 5).** A boat brings a share of **what its neighbour has
  plenty of on the day it sets out**, the share and the mix picked **by hash** of the seed, the neighbour and the visit
  count — so it reads as chance, and the same seed brings the same boat. ⛔ Never an `Rng` draw: a cosmetic-looking
  choice that drew from the stream would reshuffle every seed (D344's rule).

### 4.4 The boat (tier 1)

- **A clock by hash, never a draw** (the knock's shape): **one boat every 2 or 3 years** (call 3), the gap and the day
  from a hash of the seed and the visit count, landing in spring, summer or autumn. ⚠️ The season window is decided for
  Joe, to overrule in play. The first boat comes 2–3 years after the post stands.
- **From either direction** (call 6): each visit's neighbour, upriver or downriver, is picked by the same hash, so a
  village sees boats from both.
- **It sails.** A boat is **sim state with three ticks** — it appears at its neighbour's map edge, docks at the post,
  leaves — and its place on the water between them is **derived** from the ticks and the river's course (nothing is
  stepped per tick). The view draws it sailing the river from its edge to the post, and on past the post off the far
  edge when it leaves.
- **It is told** (call 6): a passing banner and a log line when it comes into view; a log line when it leaves.
- **It docks long enough to be noticed** (call 6), a number Joe finds in play: `trade_boat_dock_days` in
  `data/sim.config.json`. It settles the standing orders the day it docks.
- **Send the boat on** (call 6): a button in the post's window lets it leave sooner — a player input like any other,
  so it is in the sim and the hash (its leaving tick moves), never a view-only trick.
- Nothing is lost if the post is empty: the boat says it called and found nothing to take.

### 4.5 Standing orders and the trader

- **The post's card, per good:** *keep N* (sell what the village holds above N) and *buy up to M* (buy until the village
  holds M). Hashed, saved, the village's own (like stock limits, never the player's settings file).
- **The trader** (call 4) carries goods above *keep N* to the post before a visit and carries bought goods to stores
  after — the marketer's two states, re-used. A load to the post is a load **to a store** (D385).
- **At the visit:** the boat takes what is at the post, up to its hold, at the day's rate, and leaves what the
  orders asked for, as far as its neighbour has plenty. Both halves are integers; a remainder stays at the post.
- **Told** (call 6): what went and what came is the docking log line: *"The boat from Ashford took 60 firewood and left
  20 stone and two oxen."*
- **The post's window** (call 6): the standing orders, the boat in dock and what it carries, when the next boat is due
  as far as the post knows (*"no boat is expected before the spring after next"*), and **Send the boat on**.

### 4.6 What is traded (call 5)

- **Out:** anything the village makes. Firewood is the first named (D29); §8.2a finds a played village at the hall's era
  holding nearly every good at its limit, so there is always something to sell.
- **In:** whatever the boat carries (§4.3) — any good the game has, **and animals**. ⭐ **Trade and the herd are one
  build** (call 5): `livestock.md`'s first slice (pasture, herd, hay and the barn) ships with trade's first slice, so
  D61's *"until they trade for it"* is answered by the first boat that can bring a beast. A bought animal is landed at
  the post and walks to the pasture; with no pasture painted, a *buy* order for animals cannot be set and the card
  says why.
- ⛔ **No good arrives without the system that uses it** (asked, D587): salt, wool and the rest join the region the day
  their systems do.

### 4.7 Tier 2 — carts, the caravan and the cart path

- ⛔ **Blocked on carts**, which need the workshop + carpenter + hammer slice (D553 call 6), unbuilt.
- ⭐ **A cart makes its path as it drives** (call 8: *"one pass in a direction by a cart makes a path"*). A cart's step
  treads its tile **all the way to a path at once** — the wear a villager's thousand steps would give. No road sites.
- ⚠️ **Drawn at once, faster next spring** (asked, D587): the path shows the moment the cart passes; its cost enters the
  one field at the next spring re-pricing (D358), the same rule as every path. ⛔ Never a re-price per pass: every cost
  change clears every flow field, and village carts will drive daily.
- When carts are learned: **the overland neighbour's caravan** drives in from off the map, on the boat's clock, to the
  town hall, and **its pass lays the path** from the edge to the hall. ⚠️ Routes change, so the economy moves and the
  goldens move once, deliberately, measured first.

### 4.8 Not in this spec

- Coin, gold, a wealth score — cut (§3.1 call 2).
- Raids, bandits, piracy, tolls by force — **no combat** (Non-Negotiable 3).
- A haggling screen or per-visit choices — standing orders only (§3.1 call 3); the post's window can only send a boat on.
- Sending a youth away to learn (`tech-tree.md`'s IMPORT route) — a later slice that can ride on the region.
- Lakes, docks on a lake, sea trade.

## 5. What changes meaning

- **A stock limit stops meaning "the most the village will ever hold"** for a good with a *keep N* order: the trader
  moves the excess out. The limit still governs production; *keep N* governs selling. The card says both.
- **The town hall becomes a door**, not only a tribute (D252).
- **A valley's seams stop being fate**: stone and iron can be bought.

## 6. Saved, hashed, drawn, told

| Thing | Hashed | Saved | Drawn | Told |
|---|---|---|---|---|
| The post (a building) | ✅ | ✅ | ✅ | card |
| Standing orders | ✅ | ✅ | — | the post's card |
| Each neighbour's stock | ✅ | ✅ | — | region panel |
| Rates | ⛔ derived | ⛔ | — | region panel, in words |
| The next visit's day | ⛔ derived from the clock | ⛔ | — | the post's window |
| A boat (neighbour, appear / dock / leave ticks) | ✅ | ✅ | — | banner + log on coming and going |
| A boat's place on the river | ⛔ derived from its ticks | ⛔ | ✅ | — |
| *Send the boat on* | ✅ (the leave tick moves) | ✅ | — | the post's window |
| A visit's settlement | ✅ (the goods moved) | ✅ | — | the docking log line |
| The herd (with `livestock.md`) | ✅ | ✅ | ✅ | its own spec |

## 7. Edge cases and failure modes

- **The post is cut off** (no route from the village): refused in words, as every building is.
- **Winter:** no boat sets out; orders wait. **A village paused** waits too — a visit is a tick, not a clock.
- **A boat in dock when the village is saved:** saved with its ticks, and sails on from where it was when loaded.
- **The village dies:** the post stands as a ruin like everything else (D555); re-founders (D567) inherit it and its
  orders reset.
- **A neighbour has nothing to give:** the boat takes nothing either. No debt, no credit.
- **Nothing at the post:** the boat calls, says so in the log, and leaves.
- **The trader dies or is reassigned mid-carry:** the load goes to the nearest store, as any carried load does.
- ⚠️ **The trap D61 named:** a market that buys everything makes every surplus worth the same and the stores
  meaningless. The band on rates (§4.3) and the neighbours' limited wants are the guard; measured at build.

## 8. Measured before typed (D586, 2026-10-16) — `tools/harness/ZzTrade.cs`

### 8.1 Where the river runs — `River`, 100 seeds × the four courses (pure map generation)

| Course | River reaches | Bank tiles on the founding's side (min / median / max) | Walk from the founding site to the nearest bank (p10 / median / p90 / max) |
|---|---|---|---|
| west–east | 2 edges in 100 of 100 | 55 / 79 / 112 | 6 / 9 / 19 / 29 |
| north–south | 2 edges in 100 of 100 | 30 / 54 / 80 | 6 / 14 / 26 / 30 |
| NW–SE | 2 edges in 91, 3 in 9 | 49 / 80 / 105 | 6 / 6 / 20 / 27 |
| SW–NE | 2 edges in 90, 3 in 10 | 43 / 76.5 / 107 | 6 / 6 / 17 / 29 |

**Every valley has a navigable bank on the village's side, and its river leaves the map both ways** — the boat
always has two edges to come from, and the post never needs a bridge to stand. (A bank tile here is grass that
touches water orthogonally, the fishery's rule; the founding site keeps at least 6 tiles from the water by
construction.)

### 8.2 When trade opens — `Village`, seeds 200–299, the played opening, unattended (D447: a harness is not a player)

- **The last founder dies in Year 51 at the median** (p10 22, p25 42, p75 56, p90 59; earliest 13, latest 60) in 90
  valleys of 100. The other 10 lost every founder in Year 1 (9) or Year 4 (1) and never get a hall — the founding race
  (D538), not this.
- **The village then is small:** a median of 8 people (p10 2, p90 15). Twenty years on, 9.
- **What it has spare is thin, because an unattended village builds almost nothing:** logs at their limit in 48 of 90
  (median 204 of 200), firewood at its limit in 9 (median 262 of 400), forage far under its limit (median 555 of
  2,000); no stone, iron, fish, meat, leather, wheat, flour or bread in any of them (the harness never paints a seam
  or marks a farm, a fishery or a lodge). **The harness can say when trade opens; it cannot say what a played village
  has to trade.**

### 8.2a What a played village has when trade opens — copies of Joe's saves (the newest of each of 40 villages)

The four that reached the hall's era (Joe's own play, read from copies, never the originals):

| Village | Year | People | Hall | At or above its limit | Under |
|---|---|---|---|---|---|
| fernhollow-ember-lantern-38 | 51 | 17 | standing | logs, firewood, fish, wheat, flour, bread | forage 727/2,400, stone 120/200, iron 112/200, iron tools 18/25 |
| millbrook-amber-ivy-69 | 60 | 27 | owed | logs, firewood, stone, iron, wheat, iron tools, flour | forage 187/2,400, bread 112/1,000 |
| willowdale-flint-moth-39 | 85 | 38 | standing | logs, firewood, stone, iron, fish, leather (557/200), flour, bread | wheat 1,707/2,000, meat 626/2,500 |
| greyford-rook-rook-66 | 92 | 49 | standing | logs, firewood, stone, iron, fish, iron tools, flour | wheat 1,196/1,500, bread 0/1,000 |

⭐ **A played village at the hall's era holds nearly everything it makes at its limit.** So trade has a great deal to
**sell** — and, if it may only **buy** goods the game already has (call 5), almost nothing worth buying: the village
already has them, and the limit stops production of each. **The import side's payoff is new goods, and the first new
good with a waiting system is animals (D61, `livestock.md`).** That is what call 5 turned on; Joe answered it (D587): trade and the herd are one build (§3.2).

The 36 younger villages (Years 1–23) lean the same way at a smaller size: from about Year 7 most sit at or near their
limits for forage, logs and firewood, and those with a fishery for fish.

### 8.3 To measure on the build, before any number is typed

- The post's price and the trader's walk (logs, stone, seats).
- The boat's hold, and the rate band, against §8.2's surpluses — a boat that empties the stores every visit is the
  trap in §7.
- Survival with trade on and every order empty (must equal off), and with firewood sold down to its *keep N*.
- The cart road's effect on routes (tier 2), on the shipped seeds, before the goldens are re-taken.

## 9. Slices (each playable; each its own commit, red-first, red-checked and counted — D326)

⭐ Call 5 makes the first build **trade and the herd together**. It is still built as separate commits, each green on
its own, and played as one:

1. **The post and the boat.** The row, the unlock on the raised hall, standing orders on the card, the trader, the two
   river neighbours with their stocks and rates, the boat's clock (every 2–3 years, either direction), its three
   ticks, settlement, *Send the boat on*, the banner and log lines, the post's window. Sim and view; save format 8.
2. **The boat on the water.** The view draws it sailing from its edge to the post and on off the far edge.
3. **The herd** — `livestock.md`'s first slice as re-read against today's code (`livestock.md §0`, D588): one barn,
   one kind of animal, meat and leather from the cull, with animals in the region's goods.
4. **The region panel** — each neighbour, its plenty and its wants, its rates in words.
5. **Tier 2** — after the workshop slice: carts, the caravan, and paths made by a cart's pass.
6. **New goods join the region** as each of their systems lands (salt, wool and the rest).

Slices 1–4 are played together before `slice/trade` merges.

## 10. Definition of Done (per slice)

The four checks (CLAUDE.md): the suite including determinism; the game build with its warnings read; the probe with
every line read (a `trade:` line per slice); the goldens before and after, any that move proven to move for this slice
alone. Docs in the same commit. Joe plays it before it merges.

## 11. The first build — trade and the herd, commit by commit (D588)

Joe's calls for it (D588): **one kind of animal first**, **the herd gives meat and leather**, **one barn**. The herd's
half is `livestock.md` as re-read in its §0. Every commit is red-first, red-checked and counted (D326), and passes the
four checks on its own. All on `slice/trade`.

### 11.1 The rule every commit keeps: a village without a barn or a post is today's village

⭐ **All 362 goldens stay where they are** until a test raises a barn or a post. Everything new is hashed **sparsely**
(as goods, work ground and the well already are), hay gets **no starting stock limit** (D522 moved the shipped golden by
adding two), and **the region sleeps until a post stands** — the neighbours' stocks start stepping the season the post
is raised, not at the founding. That makes the goldens themselves the guard for *herding is optional* (`livestock.md
§5`) and *trade changes nothing until it exists*. Any golden that moves is a bug until it is proven otherwise.

### 11.2 The commits

| # | What | Sim / view | Its guards (red first) |
|---|---|---|---|
| 0 ✅ D589 | **Measure** — done, `livestock.md §10.1` (`tools/harness/ZzHerd.cs`, copies of Joe's saves): summer spare hands and winter work at a fire / outdoors on today's code (D533's baseline, re-taken); how far a played village's homes stand from a fire in winter (the freeze risk §7 of `livestock.md` used to wave away); the hay a tile gives against a day's walk; Joe's saves for pasture-able ground near a hall-era village | — | — (numbers go into `livestock.md §10` before anything is typed) |
| 1 ✅ D590 | **Rows and state, nothing behaves** — done. The prices and the neighbours' sizes are provisional and labelled so; the view hides both buildings until the hall stands (`Main.EarnedYet`). `Goods.Hay` 13; `BuildingKind.Barn` 23 and `TradingPost` 24; `JobKind.Herdsman` 13 and `Trader` 14; the animal row (one kind); neighbour rows; the herd on the barn; standing orders; the boat's three ticks; the neighbours' stocks. Saved (**format 8**) and hashed sparsely. Both buildings in `WhyNotYet`: *not until the town hall stands* | sim | every new field saved or named derived (the reflection guard); save → load → hash equal; **all 362 goldens unchanged** |
| 2 | **The herd lives.** Growth by an integer accumulator at the day turn, capped at what the pasture holds (tiles ÷ tiles a beast); free grazing spring–autumn; hay eaten each winter day from the barn; a beast lost for each day's hay short; the cull above capacity into meat and leather in the barn's buffer | sim | a herd with its winter's hay survives 300 winters on both configs; an over-stocked one loses beasts (anti-vacuity, D7); growth stops at capacity; the cull's goods appear; all goldens unchanged |
| 3 | **The herdsman.** Growing seasons: cut hay on the pasture, carry it to the barn (the farm's reap-and-carry). Winter: carry hay out and feed — outdoors. The cull. The quota asks for herdsmen from the hay the herd will need (derived, never the barn's room — D52's lesson) | sim | the quota wants herdsmen only for a herd; **a herdsman never freezes** over the shipped seeds; winter outdoor work rises above the re-taken baseline (`livestock.md §9.5–9.6`); a village with no herd unchanged |
| 4 | **The post and the region.** The trader carries what is above *keep N* to the post and bought goods out (the marketer's two states); neighbours wake when the post stands and step at each season turn; rates derived, in words | sim | the region sleeps before the post; stocks step only at season turns; rates stay in the band; the trader never takes a good below *keep N* |
| 5 | **The boat.** Its clock by hash (every 2–3 years, spring–autumn, either direction); appears / docks / leaves; settles the orders at the day's rate, integers conserved, the remainder kept at the post; animals only to a barn with room; *Send the boat on* moves the leaving tick; the passing banner and the log lines | sim | gaps of 2–3 years; both directions over a run; never in winter; same seed, same boats; a save with a boat in dock loads and sails on; a year run after loading matches one that never stopped |
| 6 | **View I.** The barn and its pasture paint (trees kept), the animals grazing (a hash scatter, like the trees), the barn's card (*N head, hay for N days, why*); the post's window (orders, the boat in dock, *Send the boat on*, when the next is due); the herdsman's and trader's states in words | view | probe `herd:`, `post:` |
| 7 | **View II.** The boat sailing: the water path from its edge to the post and on off the far edge, **found once per post** (the river never changes — CLAUDE.md's rule), the boat's place derived from its ticks; the region panel | view | probe `boat:`, `region:` |

### 11.3 Numbers — measured before typed

`livestock.md §10`'s targets stand: hay cut in a growing year feeds the herd through one winter with the margin the
larder and the woodpile carry (`winter_buffer_percent`); a pasture must be affordable in the season it costs. Typed
only after commit 0 and the build's own arms: tiles a beast, hay a beast a winter day, the breeding time, a culled
beast's meat and leather, hay a tile, the barn's and the post's prices, the boat's hold and the rate band.
**`trade_boat_dock_days` is Joe's, found in play.**

### 11.4 Said now, so nobody is surprised later

- ⚠️ **Save format 8 refuses every save Joe has**, as D571's format 7 did (no migrations before v1). His hall-era
  villages are already older formats.
- ⚠️ **Playing it means reaching the town hall** — Year 51 at the median. A new village and *Skip 10y* until the last
  founder dies is the way in; nothing here adds a shortcut.
- ⚠️ **The suite's clock is an instrument** (CLAUDE.md): 300-winter herd guards are long runs. Kept to the fewest seeds
  that bite, and timed against the D585 suite (8m11) at every commit.
