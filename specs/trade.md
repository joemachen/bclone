# Spec: Trade — the living region, a post on the river, and barter by standing order

**Decisions:** D29 (2026-07-26, firewood is the first named tradeable), D61 (2026-08-01, *"I don't want animals available
to the user until they trade for it"* — livestock is blocked on this), D252 (2026-08-29, the town hall is the last
founder's death), `buildings-plan.md §6` (gold cut: *"no combat, no score, no wealth condition"*), D546–D551 (bridges —
a deck over the river), D553 call 6 (the workshop, carpenter and hammer: the follow-on carts wait for), D563–D584
(nomads — the clock-by-hash and the walk-in from the valley's edge this copies), **D586 (Joe's calls for this spec,
2026-10-16 — §3)**.
**Status:** ✍️ **SPECCED, NOT BUILT (D586), on `slice/trade`.** Measured first (§8, `tools/harness/ZzTrade.cs`). **§3.2's
calls are Joe's and open; no sim code until he answers them.** Owner: Joe + Claude Code.

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
   village carries the goods; each visit settles; the player is told what changed hands. No trade screen.
4. **The unlock, in Joe's words:**
   - **The civic permission:** the town hall gift (the last founder's death, D252) — *"the village transitioning from
     a fragile homestead into an official, recognized settlement … granting the conceptual authorization for foreign
     trade."*
   - **The physical condition:** once the town hall stands, the trading post can be built, **only on a navigable water
     edge** (a river bank; a lake shore once lakes exist).
   - **Tier 2:** once carts exist (the workshop + carpenter + hammer slice), *"another wheeled caravan type-trader
     should show up from off-map, and connect a major cart path from off-map leading to the town hall."*

### 3.2 Open — Joe's to call (each with a recommendation)

| # | Question | Recommended | Why |
|---|---|---|---|
| 1 | Is the post learned when the hall is **owed** (the founder dies) or when it is **raised**? | **Raised** | Joe said *"once the Town Hall is built"*; a village that never places its hall has not become a recognised settlement |
| 2 | How many neighbours? | **3** — two by the river (one upriver, one down), one overland that waits for tier 2 | §2.4's *"keep the region small and readable"*; one per direction a trader can come from |
| 3 | How often does the boat come? | **Once in each of spring, summer and autumn; never in winter** | Seasonal rhythm the player learns (§0's *"winter is a rhythm"*); winter is already the season with nothing coming in |
| 4 | Who works the post? | **A trader job (1 seat, 2 later)** that carries the goods to sell to the post before a visit and the bought goods to stores after — the marketer's machinery | §2.2: labour, not a click; a person the player can see doing it |
| 5 | What can be bought first? ⭐ §8.2a: a played village at the hall's era already holds nearly every good at its limit | **Slice 1 buys goods the game already has** (useful to a village short of a seam or a harvest, and the cheapest way to prove the machinery); **the first new good is animals, built with `livestock.md`'s first slice right after** (D61's beast, bought) | Nothing bought may do nothing (§1.1), and an import of what the village already holds at its limit does nothing. The alternative is to build trade and the herd together as one slice — larger, but the first boat brings something new |
| 6 | How is a visit told? | **A passing banner and a log line**, never a waiting pop-up: *"The boat from Ashford took 60 firewood and left 20 stone."* | Three visits a year at 4× would be a pop-up every few minutes; D578 brings every alert to 1× |
| 7 | Do neighbours change over generations? | **Slowly: their wants drift with the seasons and the years they had; no events** | Traceable (§2.3): a rate moves because a neighbour's stock moved, never because a die was rolled |
| 8 | The cart road (tier 2): laid at once, or **built** by the builders as a line of sites? | **Built**, over seasons, from the map's edge to the hall | Work the player watches; a road that appears is the sort of thing this project deletes |
| 9 | Trade opens late (§8.2: the last founder dies in **Year 51** at the median, 22–59 for most valleys; Joe's played halls stand at Years 51, 85, 92). Keep it there? | **Keep it** — trade is a late-game answer to a full village | It is Joe's own design; the cost is that animals (D61) wait as long, about two generations. Said here so it is chosen, not discovered |

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

- **Neighbours are rows** (`data/`), so a modder can add one: a name (from `town_names`, by hash), a direction
  (*upriver*, *downriver*, *overland*), **what it makes** (goods it has plenty of) and **what it wants** (goods it lacks),
  each with a size.
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
- Rates change only at season turns, so a player who reads them in spring can plan the summer boat.

### 4.4 The boat (tier 1)

- **A clock by hash, never a draw** (the knock's shape): a boat from each river neighbour once in each of spring,
  summer and autumn (call 3), on a day from a hash of the seed, the neighbour and the visit count.
- **It comes along the river from the neighbour's edge** — upriver from one edge, downriver from the other. The sim
  decides the docking day and settles there; **the view draws the boat on the water from the edge to the post, view
  only**, as the walk-in draws the nomads (D573).
- **It docks for a day, settles the standing orders, and leaves.** Nothing is lost if the post is empty: the boat says
  it called and found nothing to take.

### 4.5 Standing orders and the trader

- **The post's card, per good:** *keep N* (sell what the village holds above N) and *buy up to M* (buy until the village
  holds M). Hashed, saved, the village's own (like stock limits, never the player's settings file).
- **The trader** (call 4) carries goods above *keep N* to the post before a visit and carries bought goods to stores
  after — the marketer's two states, re-used. A load to the post is a load **to a store** (D385).
- **At the visit:** the boat takes what is at the post, up to its hold, at the day's rate, and leaves what the
  orders asked for, as far as its neighbour has plenty. Both halves are integers; a remainder stays at the post.
- **Told** (call 6): a passing banner and a log line naming the neighbour, what went and what came.

### 4.6 What is traded first (call 5)

- **Out:** anything the village makes. Firewood is the first named (D29); §8.2a finds a played village at the hall's era
  holding nearly every good at its limit, so there is always something to sell.
- **In (slice 1):** goods the game already uses — the answer for a valley short of a seam or a harvest.
- **In (next):** ⛔ **no new good arrives without the system that uses it** — animals with `livestock.md`'s pasture and
  barn first (D61), then salt with salting, wool with weaving. Call 5 decides whether animals ride in slice 1.

### 4.7 Tier 2 — carts, the caravan and the cart road

- ⛔ **Blocked on carts**, which need the workshop + carpenter + hammer slice (D553 call 6), unbuilt.
- When carts are learned: **the overland neighbour's caravan** comes from the map edge its road meets, on the boat's
  clock, to the town hall; and **a cart road** is laid from that edge to the hall — **built by the builders as a line of
  road sites** (call 8), a road tile costing less than packed ground **in the one cost field** (§2.6's paving half;
  D358's 9 / 8 against grass's 10). ⚠️ Routes change, so the economy moves and the goldens move once, deliberately,
  measured first.

### 4.8 Not in this spec

- Coin, gold, a wealth score — cut (§3.1 call 2).
- Raids, bandits, piracy, tolls by force — **no combat** (Non-Negotiable 3).
- A haggling screen or per-visit choices — standing orders only (§3.1 call 3).
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
| The next visit's day | ⛔ derived from the clock | ⛔ | — | the post's card: *"the Ashford boat is due in summer"* |
| The boat on the river | ⛔ view only | ⛔ | ✅ | — |
| A visit's settlement | ✅ (the goods moved) | ✅ | — | banner + log |

## 7. Edge cases and failure modes

- **The post is cut off** (no route from the village): refused in words, as every building is.
- **Winter:** no boat; orders wait. **A village paused** waits too — a visit is a tick, not a clock.
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
good with a waiting system is animals (D61, `livestock.md`).** That is why call 5 is open, not settled (§3.2).

The 36 younger villages (Years 1–23) lean the same way at a smaller size: from about Year 7 most sit at or near their
limits for forage, logs and firewood, and those with a fishery for fish.

### 8.3 To measure on the build, before any number is typed

- The post's price and the trader's walk (logs, stone, seats).
- The boat's hold, and the rate band, against §8.2's surpluses — a boat that empties the stores every visit is the
  trap in §7.
- Survival with trade on and every order empty (must equal off), and with firewood sold down to its *keep N*.
- The cart road's effect on routes (tier 2), on the shipped seeds, before the goldens are re-taken.

## 9. Slices (each playable; each its own commit, red-first, red-checked and counted — D326)

1. **The post and one boat.** The row, the unlock on the raised hall, standing orders on the card, the trader, one
   river neighbour with fixed rates, the boat's clock and settlement, the banner. Sim and view; save format 8.
2. **The region.** Three neighbours, stocks stepped at season turns, rates that move, the region panel.
3. **The boat on the water.** The view draws it from the edge to the post (the walk-in's shape).
4. **Tier 2** — after the workshop slice: the caravan and the cart road.
5. **New goods with their systems** — animals with `livestock.md` (D61), then salt, wool and the rest as each system
   lands.

## 10. Definition of Done (per slice)

The four checks (CLAUDE.md): the suite including determinism; the game build with its warnings read; the probe with
every line read (a `trade:` line per slice); the goldens before and after, any that move proven to move for this slice
alone. Docs in the same commit. Joe plays it before it merges.
