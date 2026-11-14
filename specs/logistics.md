# Spec: Logistics — are villagers losing real time on fetch walks?

**Decision:** `DESIGN.md §4`, Phase 5: *"Logistics only if measured … a measured pass on long fetch walks is welcome
if play shows them."* Phase 5 gate item 1, third of Joe's four (D656: doors → clothing → **logistics** → house tiers).
**Status:** 📏 **MEASURED (D668, 2026-10-31); THE SLICE MERGED INTO `main` (D673, 2026-11-01). ⚖️ JOE'S VERDICT (D669): *"i say look
at changing the game"* — outcome 3, a mechanic; the candidates are a design thread in `DESIGN.md §5`, each to
be measured here before it is typed. ▶️ HIS PICKS (D670, 2026-11-01): (d) *nearby* given a meaning — swept in §5.6,
`market_detour_ticks` 4, built as D671; and (a) the handcart, thought through across every aspect in `specs/handcart.md`
(all six calls made as proposed — D672 — ✅ built and merged: the household's cart, Slice H, D686–D690; the trades' carts, D692–D695; the cart's night and the restock knob, D700–D707, merged D710).** This spec is the premise and the instrument; §5 holds the numbers (ten of Joe's
played villages against five unattended ones, the instrument red-checked 2 of 2; §5.5 says where the trips go). **No
line of `src/` changed.** Gate item 1's logistics entry is decided; what it becomes is the §5 decision. *History: the Marketer (D14, D36, D171) and the laborer's hauling (D63, D96)
were built before this question existed; a "porter" trade was refused as already built (D357); §4 has carried this entry
as "only if measured" since D353.*

---

## 1. Goal

Answer one question in words Joe can read off a table: **how much of an adult's working year is spent walking to
move goods — not to get to work, but to fetch, haul or deliver — and is any class of that walk long enough to be worth
a rule?** Then one of three things happens (§6), and the gate item closes either way.

⚠️ **What this is not.** Not a feature. Not a porter trade (refused, D357: the Marketer *is* the porter), not a cart,
not a second cost field. A fetch walk is already a thing the player can read on a card (*"fetching supplies for home"*,
*"carrying a load to the store"*); the question is only whether the sum of them is a cost worth a design.

---

## 2. Which pillars this serves

- **§2.2 smart labour.** The pillar's own failure mode is *"the villager who walks across the map for one log"*. D112
  made that walk legible (`Villager.CommuteNote`) rather than forbidden. This measures whether the *fetch* walks — which
  no note covers — are the same story.
- **§1.2 meditative pace / §1.1 legibility.** A village that looks busy and produces little is the opaque failure.
  If a third of the hands are walking armfuls about, the player should be able to see that, and the game should have
  an opinion about it. If it is a twentieth, nothing needs saying.
- **§0.1 the lens.** *Cozy, but challenging*: walking goods is cozy to watch and only a problem if it quietly starves
  the production pipeline the core loop (§0) is about.

---

## 3. What already exists — so nothing is built twice

Every one of these moves goods, and each was measured when it shipped. The instrument in §4 reads them as they are.

| Walk | State(s) | Who | Decision |
|------|----------|-----|----------|
| The household's shopping | `FetchingFromStore`, then `TravelingHome` carrying | whichever household member decides first; one fetcher a household | D30, D166, D372 (half a larder, then to target), D385 (the spare hand goes first) |
| The water trip | `WalkingToTheWell`, `DrawingWater` | a household member, every 20 days | D427 |
| A coat, a tool | `FetchingACoat`, `FetchingATool` | any adult / a tool trade | D660, D391 |
| The producer's load to a store | `HaulingToStore`, `HaulingToFarm` | the producer | D30, D385 (every load to a store, never home) |
| The market | `CollectingForMarket`, `StockingTheMarket` | the Marketer | D14, D171, D372 (a shop), D601 (the emptiest shelf first) |
| Clearing a hut's buffer, a closing store, a heap | `ClearingABuffer`, `ClearingAStore`, `TidyingGround` | laborers and idle hands | D63, D96, D389 |
| Building materials | `FetchingMaterials` | builders | D43 |
| The post and the herd | `CollectingForThePost`, `StockingThePost`, `FetchingFromThePost`, `FetchingHay` | merchant, herdsman | D593, D594 |

**The yardsticks the game already has** — not new numbers:
- `VillageEconomy.MaxHomeToWorkTiles` (the gatherer's ring, 8 tiles) is what the economy budgets a commute at, and
  `SimWorld.ShareOfTheDayThatIsWork` turns a travel cost into *"they bring back about N % of what a pair of hands at the
  door would"*. A day is `ticks_per_day` **4** ticks (HANDOFF's trap, D427 and D594: it is still four).
- The armful (`carry_capacity`, D537): a fetch moves one armful, so the trips a household makes a year is its food
  wanted over an armful (D535: `fetches ÷ hhyears`).
- D589's split of an adult's ticks (`livestock.md §10.1`): winter is 75 % at a fire and 2 % outdoor work; summer is
  55–63 % at home. **Those are the shares a fetch share is read against.**

---

## 4. The instrument — `tools/harness/ZzLogistics.cs`

Kept in `tools/harness/`, copied into `tests/`, run, deleted (the harness rule). Two arms:

1. **Joe's saves** (`ZZ_SAVES`, a *copy* of `%APPDATA%/Godot/app_userdata/bclone/saves/`, one `autosave-1.save` a
   village, run on `ZZ_YEARS` 5). The saves are the villages the question is about — §4 says *"if play shows them"*,
   and only a played village has the long shelves, the far lodges and the market a harness never raises (D601, D617).
2. **The harness control** (`ZZ_SEEDS`, the shipped config, the played opening, a granary and a warehouse at year 3,
   fifty years) — the unattended valley, so a number on Joe's saves can be told from a number every village has.

**One `ZZG` line a year per village**, every living adult's ticks in one of eight buckets, by `VillagerState`:

| Bucket | States |
|--------|--------|
| **work** | gathering, cutting, splitting, building, clearing, sowing, reaping, fishing, hunting, forging, quarrying, mining, sewing, grinding, baking, drawing water, tending, cutting hay, feeding the herd, culling |
| **commute** | every `TravelingTo…` work walk, `WalkingOutToTend`, `WalkingBackToTheSteading`, and `TravelingHome` **empty-handed** |
| **fetch (home)** | `FetchingFromStore`, `FetchingACoat`, `FetchingATool`, `WalkingToTheWell`, and `TravelingHome` **carrying** — so a shopping round trip is one spell |
| **market** | `CollectingForMarket`, `StockingTheMarket` |
| **haul** | `HaulingToStore`, `HaulingToFarm`, `ClearingABuffer`, `ClearingAStore`, `TidyingGround`, `FetchingMaterials`, `FetchingHay`, the post's three |
| **shelter** | `SeekingShelter` |
| **home** | `Resting`, `Idle` |
| **other** | anything else (`Dead` never counts; children never count) |

**Then the walks themselves.** A *spell* is a run of consecutive ticks in one walking bucket (commute, fetch, market,
haul). Per bucket the run reports spells, spells per adult-year, median, p90 and longest in ticks, and the share longer
than a day (4), two days (8) and four days (16 — the ring's round trip at the base price). `ZZG long` names the five
longest fetch and haul spells: who, from which tile to which, in which year — so a long one can be opened in the game.

**Red check on the instrument itself:** with the fetch states classified as *other* the fetch column must fall to zero
and *other* rise by the same number; with `TravelingHome` carrying classified as commute, fetch must fall and commute
rise. Both are counted in §5 before any number is believed (D326's rule applies to a measurement as much as a guard).

---

## 5. The numbers (D668, 2026-10-31)

**The sample.** Joe's 56 saves are eight save formats deep and a build reads only its own (D510): 3 are format 9 (this
build) and 13 format 8 (D590–D658), so the format-8 villages were run in a worktree at a9c6fde (D658) with the same
instrument less the three clothing states — the sim's fetch rules are identical either side of clothing. 40 older saves
(formats 1–7) were not run. Of the 16 that opened, 4 are Year-1 saves whose four founders die unattended within the five
years (a harness is not a player, D447; excluded) and 2 are tiny (2 and 4 adults; shown, not counted). **Ten established
played villages remain, 17–70 adults, Years 37–69 → 42–74**, five years each; the control is six unattended valleys of
the shipped config for fifty years, of which one died (seed 3, excluded) and one ended with every store empty (seed 6).
**The instrument was red-checked:** fetch states classed as *other* moved 2,482 ticks from fetch to other and nothing
else; the carrying walk home classed as commute moved 1,047 from fetch to commute and nothing else. **2 reds of 2.**

### 5.1 Where an adult's year goes (share of living adults' ticks)

| | work | commute | deliver | **fetch** | market | haul | shelter | home |
|---|---|---|---|---|---|---|---|---|
| **Joe's ten** (range) | 2.3–6.0 % | 10.8–20.1 % | 2.7–5.1 % | **11.9–22.4 %** | 2.4–9.7 % | 2.9–9.8 % | 1.4–3.6 % | 37–52 % |
| **Joe's ten** (median) | 3.7 % | 15.9 % | 4.0 % | **19.8 %** | 5.6 % | 5.3 % | 3.0 % | 41 % |
| **Control** (5 valleys, 50 y) | 2.6–3.7 % | 9.5–12.6 % | 5.3–7.9 % | **3.4–13.0 %** | 0 (no market) | 0.9–2.5 % | 0.3–1.0 % | 64–73 % |

**A fifth of every adult's ticks in a played village is the household's shopping** — more than work, deliver and haul
together (≈ 13 %), and two to three times the unattended valley's share. The ten agree closely: eight of the ten sit
between 15.9 % and 22.4 %; the two under that (Ravenscar 11.9 %, Elderwood-sparrow 14.4 %) have the shortest shop
walks below. *Home* at 37–52 % is D589's summer figure (55–63 %) pulled down by the walking.

### 5.2 The walks themselves (spells, per village)

| | fetch spells an adult-year | median round trip | p90 | longest | over 4 days (16 ticks) |
|---|---|---|---|---|---|
| **Joe's ten** | 3.9–5.8 | **12–18 ticks** (3–4½ days) | 24–51 | 38–108 | **27–54 %** |
| **Control** | 2.6–3.5 | 5–9 (seed 6: 24) | 18–32 | 20–72 | 13–61 % |

Market trips: 0.6–2.3 an adult-year, median **16–29 ticks**, 52–88 % over four days — the marketer's shuttle is as long
as the shoppers' and there are one or two of them. Commute spells (the walk to work) are 11–18 ticks at the median in
Joe's villages against 4–8 in the control — the same spread, one cause.

### 5.3 Why: where the homes stand

| | shops holding food | lived-in homes | home → nearest shop, median | p90 |
|---|---|---|---|---|
| **Joe's ten** | 2–5 | 7–36 | **8–18 ticks** | 12–29 |
| **Control** | 1 | 5–7 | 4–6 | 8–12 |

Willowdale-dove-flint-67: 36 homes, 3 shops, a median 16 ticks to the nearest and a p90 of 29 — the five longest
shopping trips in the sample (75–108 ticks, 19–27 days) are its. The chooser sites a home **wherever the player painted**
(D381, D120: *"a far home is scored, chosen if it is the best there is, and costs the food it costs"*), the economy
budgets a home 8 tiles from its work (`MaxHomeToWorkTiles`), and the player's housing paint spreads as the village
grows while the shops stay where they were raised. **And the one warning the brush gives measures the wrong walk:**
`CanPaintResidential` says *"That corner is N tiles from the nearest food … Families there will go hungry"* reading
the nearest **forage** — but since D385 (every load to a store, never home) a household eats from the nearest **shop**,
and nothing on the brush, the card or the log says how far that is.

### 5.5 Where the trips go — one market a village (D669)

Since D372 a household shops at **the nearest market holding the good, anywhere reachable**, and at a storehouse only
when no market holds it (Joe: *"only go to the granary if there is no market nearby"* — built with *nearby* meaning
*reachable*, no radius, D201's reasoning). So the instrument was taught where each shopping spell turned round (the
nearest store within two tiles of its far point):

| village | markets | homes | home → market, median | home → storehouse, median | market trips, median | storehouse trips, median | short spells (the well) |
|---|---|---|---|---|---|---|---|
| Ashbourne-fallow-finch-66 | 1 | 11 | 13 | 11 | 219 × **31** | 256 × 17 | 328 × 7 |
| Ashbourne-rye-rush-18 | 1 | 18 | 18 | 19 | 165 × 32 | 381 × 31 | 510 × 9 |
| Elderwood-sparrow-brook-12 | 1 | 12 | 8 | 15 | 134 × 17 | 169 × 29 | 259 × 5 |
| Greyford-brook-lark-24 | 1 | 7 | 12 | 9 | 13 × 18 | 214 × 22 | 226 × 6 |
| Millbrook-ash-willow-36 | 1 | 13 | 12 | 21 | 110 × 27 | 268 × 31 | 334 × 7 |
| Oakhaven-ash-otter-53 | 1 | 11 | 14 | 13 | 149 × 24 | 176 × 28 | 323 × 7 |
| Oakhaven-hazel-pebble-34 | 1 | 17 | 19 | 12 | 3 × 5 | 473 × 29 | 512 × 8 |
| Ravenscar-sloe-oat-43 | 1 | 8 | 10 | 13 | 133 × 13 | 49 × 23 | 162 × 11 |
| Willowdale-dove-flint-67 | 1 | 36 | 18 | 16 | 198 × 32 | 489 × 29 | 1,097 × 8 |
| Willowdale-flint-thrush-27 | 1 | 15 | 13 | 11 | 159 × 31 | 205 × 27 | 262 × 10 |

**Every established village has exactly one market**, for 7 to 36 homes, and it stands no nearer the homes than the
granaries do: the median gap between the walk to the market and the walk to the nearest storehouse is 0–7 ticks either
way. **So D372's market-first rule is not the cause** — it costs a tick or two in four villages and saves as many in
four others. The cause is the one before it: one market, placed once, while the housing paint spreads. A real shopping
trip to either kind of store is a **17–32-tick round trip at the median, four to eight days**; the short spells (a
median 5–11 ticks, roughly thirty a home over the five years) are the well trips, every twenty days a household (D427),
and are not the problem. The five longest trips in the sample are 82–108 ticks (21–27 days), all from the far ends of
Willowdale-dove-flint-67 and Oakhaven-hazel-pebble-34, 16–29 tiles out.

### 5.4 What the number is not

- Not the armful (D537 measured `carry_capacity`; the trips an adult-year, 3.9–5.8, are the armful doing its job).
- Not the control's famine: seed 6's 13 % and 24-tick median are a valley whose stores stood empty at the end, so
  its shoppers walked to shops with nothing — the far end of the same mechanism, not a different one.
- Not winter alone: the shares are year-round; D589's winter is 75 % at a fire, and shelter is 1.4–3.6 % here.

### 5.6 The sweep for *nearby* — `market_detour_ticks` (D670)

Joe picked candidate (d) — *"Shop at the granary when the market is more than X ticks further"* — and (a) to think
through (`handcart.md`). Before X was typed the instrument was taught to answer, from each village's end state and
without a sim change, **how many lived-in homes would switch to the storehouse at each X, and how many one-way ticks each
would save** (`ZZG … detour`; a home counts only when it can reach both a market and a storehouse holding food):

| village | homes | market nearer | X = 0 | X = 2 | X = 4 | X = 6 | X = 8 | X = 12 |
|---|---|---|---|---|---|---|---|---|
| Ashbourne-fallow-finch-66 | 11 | 2 | 8 × 4 | 6 × 4 | — | — | — | — |
| Ashbourne-rye-rush-18 | 18 | 12 | — | — | — | — | — | — |
| Elderwood-sparrow-brook-12 | 12 | 9 | 2 × 5 | 1 × 5 | 1 × 5 | — | — | — |
| Greyford-brook-lark-24 | 7 | 0 | 7 × 3 | 5 × 4 | — | — | — | — |
| Millbrook-ash-willow-36 | 13 | 13 | — | — | — | — | — | — |
| Oakhaven-ash-otter-53 | 11 | 3 | 8 × 3 | 4 × 4 | 1 × 6 | — | — | — |
| Oakhaven-hazel-pebble-34 | 17 | 4 | 13 × 7 | 10 × 10 | 10 × 10 | 7 × 10 | 6 × 11 | 1 × 15 |
| Ravenscar-sloe-oat-43 | 8 | 7 | 1 × 1 | — | — | — | — | — |
| Willowdale-dove-flint-67 | 36 | 5 | 29 × 3 | 22 × 3 | — | — | — | — |
| Willowdale-flint-thrush-27 | 15 | 6 | 8 × 11 | 6 × 11 | 6 × 11 | 5 × 11 | 5 × 11 | 2 × 17 |
| **all ten** | **148** | **61** | **76** | **54** | **18** | **12** | **11** | **3** |

*homes that switch × median one-way ticks saved; a day is 4 ticks.*

**The gaps come in two kinds.** In six villages most homes have the market **3–4 ticks further** than a storehouse — a
market standing beside the granary, the pair's footprints apart, which is the layout D372 was written for and meant the
market to win (*"bum rushing the granary"*). In two villages (Willowdale-flint-thrush-27, Oakhaven-hazel-pebble-34) and a
home or two elsewhere, a storehouse is **5–17 ticks nearer** — a second granary built out among the homes while the one
market stayed where it was placed. **X = 4 is the knee:** below it the rule is nearest-first again for the pair layout
(54 homes switch, 36 of them to save 3–4 ticks past a market they can see from the granary's door); at 4, **18 homes in
four villages switch and save 5–15 one-way ticks, 10–30 on a 29–31-tick round trip**, and the market beside the granary
still takes every trip. At 6 and beyond the saving thins (12, 11, 3 homes) without a reason to prefer it. **So
`market_detour_ticks` 4**: a round-trip detour of a full two days is where a market stops being *nearby*. By the table's
arithmetic it takes ~8 % off the shopping ticks of the ten villages (X = 2 would take ~17 %, at the price above); the
live re-run with the rule is §5.7. ⚠️ What this does not touch: the household's trips are still consumption ÷ armful
(D537), and this moves only *where* some of them turn round — the handcart (`handcart.md`) is the candidate that moves
*how many*.

### 5.7 The rule, live (D671)

`market_detour_ticks` 4 is built: one clause in `BehaviorSystem.NearestShopHolding`, the knob validated, two guards
(`storage-and-distribution.md §14.9.7`), red-checked — the sweep's `detour` line 2 of 2 (a mutant that forgets the storehouse
walk moves both villages' X = 4 count from 0 to 10 and 7), the clause 2 of 2 (deleted, and `>` for `>=` at the edge; one
red each, the new guard both times).

- **⛔ The first draft had D372's scrap hole on the other shop.** The storehouse tier counts a store holding *any at all*, and
  the margin was written against it — so a granary out among the homes holding three fish would have taken every trip from a
  full market four ticks further, the exact failure D372 fixed at the counter. The margin applies to a storehouse **stocked for
  the trip**; the scrap case is in the guard.
- **Live on the saves this build can open** (format 9: Ashbourne-fallow-finch-66, Greyford-brook-lark-24): Fetch 20.2 → 19.7 %
  and 15.9 → 15.9 %, trips to the market 219 → 236 and 13 → 13 — noise, as §5.6 said: both switch zero homes at 4. The two
  villages that would show it (Willowdale-flint-thrush-27, Oakhaven-hazel-pebble-34) are format 8 and the sweep's estimate
  stands for them (6 and 10 homes, 10–11 one-way ticks each). **The unattended control is untouched**: its markets hold no
  food (no marketer is ever hired there), so the rule never fires; seeds 1 and 2 read 6.9 % and 8.0 %, as D668.
- **⚠️ The five-valley ledger went red** (`ShippedConfigTests.TheShippedVillageSurvivesBeingAskedToBuild`: two granaries and two
  warehouses marked at Year 15 must not cost the village more than D625's one person against nothing marked): **103 against
  106.** Two marked granaries out among the homes are the rule's own case. Swept over the margin: marked peaks **105** at
  1000 (the bar's own trajectory), **105** at 0, **100** at 2, **103** at 4, **102** at 8 — no order in it. Ten valleys at 4
  against 1000: **marked 222 → 219** (seed 1 −4, seed 12345 +2, seed 5 −1, seven unchanged), **nothing marked 222 → 222**;
  **at 2 the unmarked valleys lose eight** (222 → 214), the pair layout turning nearest-first, which is the second reason 4
  is the number. Read as reshuffling, not a price: two valleys move in opposite directions and eight hold. **The bar is
  re-pinned at the measured price, three, with D671 named** — D625's own shape — and flagged for Joe (HANDOFF ⏸️): the
  ledger is a tripwire for any rule that moves where an unattended village shops, and it fired as designed.
- Goldens: 392, none moved — the shipped seeds' homes stand 4–6 ticks from a market beside its granary.

---

## 6. What the numbers can mean — Joe's call

Three honest outcomes; the gate item closes on any of them. **§5 rules the first out on the number and names the class
the second asks for; which of the three it is remains his.**

1. **The fetch share is small** against D589's shares (a few per cent of adult ticks, spells at or under a day) →
   **dropped, in Joe's words.** ⚠️ §5.1 says a fifth, not a few per cent — so this outcome is now *"a fifth of the day
   shopping is the village I want to watch"*, which is a legitimate cozy call (§0.1) and his to make.
2. **One named class of walk is long** → **one rule, measured against §5 before it is typed**, in the shape of the ones
   already here (the emptiest shelf first, D601; the spare hand goes first, D385). §5.3 names the class: **the household's
   shop walk, long because the homes stand 8–18 ticks from the nearest shop and nothing tells the player so.** The
   smallest honest rule is legibility, D112's shape — a fence you can see: the housing brush's warning reads the nearest
   **shop** (and says *"N tiles to the nearest shop — families there will spend their days walking for food"*), the
   villager's card says it on the fetch (*"fetching supplies for home — N tiles to the nearest shop"*), and the market's
   own sentence (D372: a shop near the homes) is the answer the game already has. Measured by the same instrument after:
   the shares would not move (a sentence moves nothing) — what moves is where Joe paints next, and that is his play to
   report.
3. **It is large and diffuse** → that is a design conversation (carts, a second store tier, a delivery job the Marketer
   does not do, a bigger armful for a longer walk), not a slice, and it goes to §5 of `DESIGN.md` as an open decision
   with the numbers attached. §5 argues it is large and **not** diffuse — one cause, the shop walk — but a mechanic
   (say the marketer delivering to far homes, D171's old shape) is a design call, not a rule, and it is his.

**⚖️ Joe's verdict (D669, 2026-10-31): *"i say look at changing the game"* — outcome 3.** The candidates, with what each
would cost the pillars and what `ZzLogistics` would have to show before any is typed, are the open decision
**LOGISTICS — THE MECHANIC** in `DESIGN.md §5`. The legibility rule of outcome 2 is not an alternative to it: whatever
mechanic lands, the brush and the card still owe the player the sentence (D112), and it is listed there as the
companion. §7's Definition of Done closes at item 3 with this verdict; the mechanic gets its own spec when he picks one.
**He picked two (D670): (d) now — the sweep is §5.6, the rule `storage-and-distribution.md §14.9.7` — and (a) to think
through first, which is `handcart.md`.**

### 6.1 The verdict on the mechanic — Slice H built and measured (D686–D689, 2026-11-04)

The handcart is built (`handcart.md`: a household's good, fetched like a coat, parked at the door, three armfuls a trip,
worn a trip, slow on grass, a path on its first pass) and this instrument measured it **before and after on the same
villages** — the harness with forty carts racked at Year 3 against none, and Joe's Hollowmere (`hollowmere-plover-rowan-45`,
his D682 save: Year 25, 24 adults in 7 housed households, a workshop and nine carts) five years on against the same save
with every cart taken out and the carpenter stopped:

| | Fetch share | shopping trips a household-year | fetch spells an adult-year | fetch spell median | over four days | path tiles |
|---|---|---|---|---|---|---|
| **Hollowmere, no carts** | **17.6 %** | **5.69** | 5.0 | 16 | 49.5 % | 1.31 % |
| **Hollowmere, carted** | **11.3 %** | **3.49** | 4.0 | 8 | 34.7 % | 1.32 % |
| four unattended valleys × 50 y, no carts | 6.4 % (3.4–8.0) | 5.93 | 3.1 | 8.5 | — | 0.62 % |
| the same, carted | 5.0 % (2.3–6.9) | 4.25 | 2.5 | 10 | — | 0.66 % |

**A third of a played village's shopping walk is gone (17.6 → 11.3 %), on 1.6× fewer trips; the walk that is left is the
same walk** (the pushed trip's median is 29 ticks, the from-home trip it always was — fewer of them, not shorter). The
unattended control, whose households are couples 4–6 ticks from their store, moves little, as §5 predicted it would. The
~2.5× of `handcart.md §1` was the four-person larder's arithmetic; a couple's half-larder fitted one armful already. The
cart's track adds a tile or two of path a village — the shoppers' lines were paths before the carts came — and the slow
on grass costs nothing a village can measure, because a cart's line is a path after its first pass. ⚠️ Joe's Fernhollow
save dies unattended under every arm and is excluded (D447). **Still open, D668 outcome 2's companion:** the legibility
sentence — the housing brush and the home card naming the shop walk — is not built; whatever the cart buys, nothing on the
brush yet says how far the shop is. The rest of what the number can mean is now Joe's to see in play.

⛔ **Whatever the outcome, the rule stays:** no fence the player cannot see (D112). A fetch that is too long is said on
the card and in the log before it is forbidden anywhere.

---

## 7. Definition of Done (this slice)

1. `ZzLogistics` runs on a copy of Joe's saves and on the harness control; its red checks are counted.
2. §5 holds the numbers; `DESIGN.md §7` has one entry and `§6` one block; `HANDOFF.md`'s banner says what Joe is asked.
3. Joe picks one of §6's three, in his words, and §4's logistics entry records which.
4. Nothing in `src/`, `tests/` or `data/` changed; the four checks are the same numbers as D667's.
