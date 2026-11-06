# Spec: The handcart — a household's cart, parked at its door — Slice H

**Decisions:** D668 (the measurement this exists to move), D669 (Joe: *"i say look at changing the game"*), D670 (the
cart thought through across every aspect), **D672 (Joe: *"Yes, all six"* — every call in §10 stands as proposed)**,
D678 / D682 / D684 (Slice W: the Workshop, the Carpenter and `Goods.Handcart` on a shelf — `specs/workshop-and-the-carpenter.md`),
D537 (the armful, one meaning), D587 call 8 (a cart's pass makes a path), D30 (a tool follows the person; a cart serves the
larder), D372 (the shop walk), D510 (an older save is refused in words), D291 (the hash is sparse over goods).
**Status:** ✅ **SLICE H BUILT, on `claude/slice-handcart-b0230a` (2026-11-04, D686–D689), PLAYED (Joe, 2026-11-05: *"the cart stuff seems pretty good"*) AND MERGED INTO `main` (D690) on 2026-11-05 — five pieces, each its
own commit with its guards (`tests/Bclone.Sim.Tests/HandcartTests.cs`, 19 facts), red-checked and counted (§11: 36 mutants, 39
reds, three zeros written down); every number measured on `ZzLogistics` before typed; no golden moved. The verdict against
D668's measurement is `logistics.md §6`: Hollowmere's Fetch share 17.6 → 11.3 %, trips a household-year 5.69 → 3.49. Merged at his
word; the marketer's and merchant's carts are the next slice (his call, 2026-11-05).** ✅ Piece 1 — the household's cart: owned,
fetched like a coat, walked home, parked, taken from the doorstep, hashed, saved (format 11), said (§3, §4, §9). ✅ Piece 2 —
seen: the chip beside the home, the rectangle behind the pusher, the card's line, the founders' cart in the cart colour,
two probe lines (§8). ✅ Piece 3 — what it buys: `cart_armfuls` 3, swept 2/3/4 on the harness and Joe's Hollowmere before typed
(§5; Hollowmere's Fetch share 17.6 → 11.3 %, trips a household-year 5.69 → 3.49). ✅ Piece 4 — wear: one use a round trip at
the door, gone at nought and another fetched; `handcart_uses` 150 measured as ~46 years of shopping and kept as Joe's number
with the 20/40 arms put to him (§6).
✅ Piece 5 — speed and tracks: slow on grass (150, the failing deck's shape; measured to cost nothing a village can see, because
a cart's line is a path after its first pass), every tile the wheels cross a path at once; the path tiles moved 0.62 → 0.66 % of
the valley, 1.31 → 1.32 % in Hollowmere — the village is not webbed, nothing softened (§6, §7).**
Candidate (d), *nearby* given a meaning, is built separately (`logistics.md §5.6`, `storage-and-distribution.md §14.9.7`, D671).

> **Where this sits.** D668 measured a fifth of every adult's ticks in Joe's established villages as the household's
> shopping walk. Joe chose to change the game (D669), picked two mechanics (D670) and took all six of this cart's calls as
> proposed (D672). Slice W built the maker — a Workshop, a Carpenter and a handcart racked where tools are — and Joe played
> it twice (D682, D684). **This slice is the cart itself:** the thing the household owns, fetches, pushes, wears out and
> leaves a track behind. The marketer's cart (call 6) is later; oxen, wagons, roads and the caravan are not here.

---

## 1. Why a cart

D668 measured **a fifth of every adult's ticks in Joe's established villages as the household's shopping** — a
17–32-tick round trip at the median, because each village has one market, placed once, for 7–36 homes while the housing
paint spreads (`logistics.md §5`). The trips are consumption ÷ armful (D537): a household of four wants 380 food, fetches
at half a larder (D372), and brings 80 a trip, so a top-up is two to five trips. **A cart changes the only term that is
not the village's to change — how much one person carries** (`buildings-plan.md:230`: *the first thing in this game that
would change `carry_capacity`*). It is also a thing on the map, which is what the legibility pillar wants of a mechanic.

⚠️ **What this is not.** Not oxen or wagons (*Heavy Haulage* proper, the draught-animal node — `tech-tree.md §9.3`, Tier 2
in `TECH-EXAMPLE.md:206`); not roads as a build (paving, `DESIGN.md §2.6`, open in §5); not the overland caravan
(`trade.md §4.7`, which this unblocks); not a porter (D357 — the Marketer is the porter); not a bigger armful alone (D537);
not the marketer's cart (Joe, call 6: later). ⭐ **Joe's calls after playing H (2026-11-05):** the marketer's and the
merchant's carts are **the next slice** — *"one more owner of the same good"*: the market and the trading post each own a
cart parked at the building, their worker fetches it from the shelf, three armfuls a trip on the restock and the post's runs,
the same wear and track; and **the ox cart stays the *Heavy Haulage* node**, later, after livestock — the handcart is the
household's and does not upgrade.

## 2. What was already on the record — and what this slice reads

- **The founders' cart** is a store: `StoreKind.Cart`, food and tools only (D64, D90, D215), no door (D625). It drew in
  `MarketColour` until piece 2 gave it the cart colour. A handcart must not borrow its name: `Goods.Handcart`, never
  `Goods.Cart`.
- **Tools are the model for an item a person takes**: a good with `tool_uses`, stored in the Warehouse / Cart / Pile —
  *the market does not stock tools* — fetched from the nearest store holding one (`BehaviorSystem.TryFetchATool`), worn one
  use per action. A tool hangs off the **villager** (D30) because the skill does.
- **Coats are the model for a fetched item with a life in time** (`BehaviorSystem.TryFetchACoat`, `clothing.md §3.3`). The
  cart's fetch is written in its shape, one owner over.
- **The armful** is 80 for every errand, one meaning (D474, D536, D537); `carry_capacity` is not touched by this slice.
- **Paths**: D587 call 8 is Joe's rule — *one pass in a direction by a cart makes a path*; drawn at once, priced into the
  one field at the next spring (D358), never a re-price per pass (`trade.md §4.7`). `PathWear.Tread(tile, amount)` is the
  seam (piece 5).
- ⚠️ **The shared cost field cannot price anything dearer than grass** (`TerrainCostField.cs`: an entry cost is between 1
  and the base). The precedent for *slower* is the failing bridge deck (`failing_deck_slow_percent` 150,
  `BehaviorSystem.WalkedOver`): **the walk slows, not the route.** One shared cost field stays (`DESIGN.md §2.6`).

## 3. What a cart is in the model — ✅ Joe (call 1, D672); ✅ built (piece 1)

**A good, `Goods.Handcart`, owned by a household and parked at its home.**

- **Made** by the carpenter at the workshop (Slice W), **racked where tools are** — the Warehouse, the founders' cart and
  the Pile — never the market (the row's `stored_by`, to the letter).
- **`Household.HandcartUses`** — the trips left in the household's cart, **0 for no cart**. `Villager.ToolUses`' shape,
  one owner over: filled from a store's count by a member who walks it home, never conjured; saved as `handcart_uses`;
  **hashed sparsely under its own tag (`0x70`)**, so a village with no cart hashes byte for byte as before (D291's rule;
  the goldens are the guard, §9).
- **Fetched like a coat** (`BehaviorSystem.TryFetchAHandcart`): a **housed** household with `HandcartUses` 0 sends one
  free member (`CanWork`, empty-handed, not already pushing) to the nearest store holding a handcart; **one fetcher a
  household** (`SomebodyIsFetchingTheCart` — a housemate walking for it or pushing one home); a roofless family has no
  door to park one at and sends nobody. Asked in `Decide` after the tool and the coat, before the work — a coat is the
  person's, a cart the household's convenience, and both come before the job like the tool. Once in a cart's life, so it
  is not the household's half-larder errand and the market does not stock it.
- **At the store** (`TakeAHandcart`): one cart off the shelf, `HandcartUses` = `handcart_uses`, and the fetcher pushes it
  home — `VillagerState.PushingAHandcart`, `Villager.PushingACart` true — to the door, never the steading
  (`RestPointFor` sends a pusher home as it sends a load home). At the door (`ArriveAt`, the rest that unloads the arms)
  the cart is parked: the flag clears. A refusal at the shelf (the household carted meanwhile, the roof gone) ends the
  errand with `GoHome`, ⛔ never `Decide` — a refusal beside a shelf that still holds the cart would ask for it again in
  the same tick and recurse until the stack went (the red-check's mutant did exactly that, §11).
- **Hangs off the household, not the villager.** A tool follows the person because the skill does; a cart serves the
  larder, which is the household's. **A household that dies out leaves its cart at the home for the next family**, as it
  leaves its larder; a pusher who dies (`MortalitySystem.Kill`) or moves house (`HouseholdSystem.MoveIn`) drops the flag
  and the cart counts as home — the walk that would find it is not stepped. *Written down rather than modelled: a cart is
  a good, not a positioned thing.*
- **The carpenter's stop and the professions panel stay honest:** `LabourQuota.HandcartsWanted` is the housed, living
  households **with `HandcartUses` 0**; `HandcartsShortfall` and the handcarts chip follow it. A carted village wants
  none and the bench stops at the spare (`workshop-and-the-carpenter.md §4.4`).
- **Not built, by Joe's call:** a positioned entity like the trade boat — a new kind of thing to save, step and draw.

## 4. Who pushes it — ✅ built (piece 1)

The measured share is the **household's** shop walk, so the household's cart is the one that matters.

- **The shopper takes it from the doorstep** (`BehaviorSystem.TakeTheCartFromTheDoorstep`), as a shopping trip begins —
  `Decide`'s fetch and the emergency restock, never a re-plan mid-walk — **only when they stand at the home's tile**, the
  household has a cart, and nobody else is out with it (**one pusher a household**). A trip begun at work or at a store
  goes without it: *a cart kept anywhere but the start of the trip costs a walk to get it*, and that walk is the one this
  slice exists to remove. `Villager.PushingACart` is true to the shop and home again (`FetchingFromStore` / `TravelingHome`
  with the flag); parked at the door.
- **Saved** as `pushing_a_cart`; **hashed sparsely** under tag 9 after the coat, so a village with no cart hashes as before.
- **Sentences** (`Villager.DescribeState`): *"fetching a handcart from a store"*, *"pushing the new handcart home"*,
  *"pushing the cart to the shops"*, *"pushing a load home"* / *"pushing the cart home"*; the log's register
  (`BehaviorSystem.Describe`): *"fetching a handcart from a store"*, *"pushing a handcart home"*. Neither state is work
  (`SkillSystem.OutOnTheWork` false, the coat's ruling); `ZzLogistics` classes both as **fetch**.
- **The marketer's cart is not in this slice** — ✅ Joe (call 6, D672): *later*; when it comes it is one more owner of the
  same good. Builders' and producers' hauls (the *Deliver* bucket) are out of scope; they were not the number.

## 5. What it buys — `cart_armfuls` 3 — ✅ built and measured (piece 3, D687)

A fetch brought the shortfall capped at one armful (`ATripsWorth`, 80; `CollectFromStore`'s `load`). **With the cart in
the shopper's hands the cap is `cart_armfuls` × `carry_capacity`** (`CollectFromStore`), and **the trip's worth the plan
asks a shop to be stocked for is the cart's too** (`ArmfulsFor`: pushing already, or about to take it from the doorstep —
`CanTakeTheCartFromTheDoorstep`, one predicate for the take and the plan — so D372's scrap rule asks for the load the shopper
will actually carry: a market stocked for an armful is not stocked for three, and the storehouse that is wins that trip).
`carry_capacity` itself is untouched (D537, one meaning); the arms are still the arms.

**Measured before typed — `tools/harness/ZzLogistics.cs`, the cart arms (`ZZ_CARTS` racks carts in the founders' cart at Year
3, since no unattended valley raises a workshop; `ZZ_ARMFULS`; `ZZ_NOCARTS` on a played save takes every cart out AND stops
the carpenter with a handcarts limit of 0 — ⚠️ the first "before" forgot the carpenter and he re-made seven in five years).**
Four living unattended valleys (seeds 1, 2, 4, 5; seed 7 dies), fifty years; and Joe's Hollowmere (`hollowmere-plover-rowan-45`,
Year 25, 24 adults in 7 housed households, a workshop and 9 carts racked — D682's save), five years on. His Fernhollow save dies
unattended under every arm and is excluded (a harness is not a player, D447).

| arm | Fetch share (4 valleys, mean · range) | shopping trips a household-year | pushed trips (median ticks) | on foot (median) | Hollowmere Fetch | Hollowmere trips a hh-year | Hollowmere pushed (median) |
|---|---|---|---|---|---|---|---|
| no carts | 6.4 % (3.4–8.0) | **5.93** | — | 5,474 (8) | **17.6 %** | **5.69** | — (on foot median 27) |
| `cart_armfuls` 2 | 5.2 % (2.5–6.8) | 4.48 | 3,315 (10) | 892 (5) | 13.2 % | 4.26 | 91 (29) |
| **`cart_armfuls` 3** | **5.0 % (2.2–6.1)** | **4.08** | 3,085 (10) | 656 (6.5) | **11.3 %** | **3.49** | 91 (29) |
| `cart_armfuls` 4 | 5.0 % (2.2–6.1) | 4.08 | 3,085 (10) | 656 (6.5) | 11.6 % | 3.49 | 89 (29) |

**Reading.** Three and four are identical on every valley and within noise on Hollowmere: a half-larder shortfall never
exceeds three armfuls (a four-person larder's half is 190), so the fourth armful is never carried. Two is measurably worse.
**So `cart_armfuls` 3.** The payoff is Hollowmere's: **the Fetch share 17.6 → 11.3 %, a third of the shopping walk gone; trips
a household-year 5.69 → 3.49 (1.6× fewer)** — short of §1's arithmetic (~2.5×), because a couple's half-larder shortfall (75)
fitted one armful already and the cart buys them nothing; the big households are where it bites. The unattended control moves
little (6.4 → 5.0 %) for the same reason: its households are couples 4–6 ticks from the store. ⚠️ **The pushed trips are the
long ones** (median 29 ticks in Hollowmere against 27 for every trip before; the trips still made on foot are the short ones
begun away from the door, median 16–20) — the walk is the same walk, there are fewer of them, which is what §1 asked. These
are the numbers **before the slow grass walk (§6, piece 5)**; the verdict in `logistics.md §6` is re-measured after it.

## 6. Wear, speed — ✅ piece 4 (wear, D688) and piece 5 (speed, D689) (✅ Joe, call 5, D672)

- **Wear — ✅ built (piece 4):** one use a round trip, taken at the door (`ParkTheCart`) when the pusher arrives home
  **from a shopping trip** (`villager.State == TravelingHome`) — the walk home with a NEW cart (`PushingAHandcart`) wears
  nothing, so a fresh cart parks with every trip in it. At 0 the cart is gone (a DEBUG line, *"The Bramble handcart is worn
  through after its last trip"*), the card stops reading it, `HandcartsWanted` counts the household again and it fetches
  another. No repair: the condition system (D552) mends buildings, and a cart is a good.

  **`handcart_uses` 150 — Joe's number, kept; measured, and the measurement is put to him.** The harness (`ZZ_USES`, 200
  carts racked so no household waits), four living valleys × fifty years, and Hollowmere five years on:

  | `handcart_uses` | carts taken (4 valleys × 50 y) | a cart lasts (at ~3.2 pushed trips a household-year) | household-ticks carted | logs a valley-year | Fetch share | trips a household-year |
  |---|---|---|---|---|---|---|
  | 20 | 201 | ~6 years | 97.5 % | ~10 (a quarter of an unattended valley's 30–45 felled) | 5.2 % | 4.21 |
  | 40 | 108 | ~12 years | 97.8 % | ~5 | 5.0 % | 4.08 |
  | **150** | 44 | **~46 years** | 97.8 % | ~2 | 4.9 % | 4.05 |

  Hollowmere (7 households, ~3.5 trips a year) replaced no cart in five years at any number — the window is too short to
  see wear. **Reading:** the households stay carted whatever the number because the spare is on the shelf and the fetch is a
  short walk; the number buys only how often the carpenter works and how many logs a village spends on carts. **At 150 a
  cart outlives its household** — the carpenter is a one-burst trade and the wear mechanic is, in play, dormant. *"A few
  years of shopping"* is 20–40. ⏸️ **His call (HANDOFF, open): 150 as he said, or 40 for a dozen years and a handful of logs
  a year.** Nothing else in the slice depends on it.
- **Speed — ✅ built (piece 5, D689):** `cart_off_path_slow_percent` **150 on grass, 100 on worn and packed**, applied to the
  leg's ticks as the failing deck is — `WalkedOver(route, furthest, pushing)` multiplies a grass tile's entry cost (one the
  field charges `BaseTileCost` for) while the walker pushes; a worn or packed tile pays what it always did; the grass count
  the lane rule (D414) reads is unchanged; the last fraction of a tile in `Travel` pays the same price. **The route is the
  walker's route and the cost field stays one** (`DESIGN.md §2.6`; CLAUDE.md: never two). Legible in a sentence and in a
  guard: *the cart is slow on grass and quick on the path* — eight grass tiles take **12 ticks under a cart against 8 on foot**
  (`ACartsPassMakesAPathWhereAWalkersLeavesAFootstep`). **Measured with and without (the `ZZ_SLOW` arm):** on four unattended
  valleys over fifty years and on Hollowmere the slow moves nothing the instrument can see — Fetch 5.0 % at 150 and at 100,
  Hollowmere 11.3 % both, the pushed trip's median 10 / 29 ticks both ways — **because a cart's line is a path after its
  first pass (§7)**, so the slow is paid once a route, on fresh grass. 150 stands: it is the failing deck's number, it is
  legible on a new route, and it costs the village nothing it can measure.

## 7. Paths — ✅ built and measured (piece 5, D689; ✅ Joe, call 4, D672)

D587 call 8 rules it, as written: **a cart's pass treads every tile its wheels cross to `path_worn_at` at once** —
`Travel`'s footprint loop, under `Villager.PushingACart`, treads each footprint tile by its shortfall to the line (or the
ordinary `path_wear_per_step` once past it; a worn tile is never reset to the line); a bridge deck takes no wear as ever.
**Drawn at the next season's sweep like every path** (the class moves at the sweep, D362 — *"at once"* in D587 means not
waiting for the spring re-price), **priced at the next spring, never a re-price per pass** (every cost change clears every
flow field, and carts drive daily — D358). Guard: `ACartsPassMakesAPathWhereAWalkersLeavesAFootstep` — a walker's pass over
fresh grass leaves 3 a tile, a cart's leaves every tile at 30 or more, a tile already at 50 gains 3.

**Measured — the trodden share and the path tiles, carts against without (§5's arms, the slice as built):**

| | trodden tiles (of 9,600) | path tiles (worn or packed by class) |
|---|---|---|
| four unattended valleys, fifty years, no carts | 0.65 % | 0.62 % |
| the same with carts (armfuls 3, uses 150, slow 150, the tread) | 0.68 % | 0.66 % |
| Hollowmere, five years on, no carts | 1.3 % (129) | 1.31 % (126) |
| Hollowmere with carts | 1.4 % (134) | 1.32 % (127) |

**Reading: the village is not webbed.** The cart follows the walker's route, and the household's line to its shop was a
path already — walked for food, water and firewood every few days (Joe's Fernhollow lanes sit at the wear ceiling, D680).
The cart's tread adds a tile or two of path a village: the shoppers' lines were paths before the carts came. §2.6's loop
is literal and quiet. **Nothing to soften, and nothing softened**: *worn not packed* and *a loaded pass only* stay in this
section as the two levers if a played village ever shows otherwise — Joe's call, not this slice's.

## 8. What it looks like, and what it says — ✅ built (piece 2)

- **The colour, one answer:** `VillageMap.HandcartColour` — a painted cart's slate blue-green (`#6b8a94`), chosen at D678
  in `GoodsPalette` and moved to the map the day it had three drawings; `GoodsPalette.ColourOf(Goods.Handcart)` hands it on,
  and **the founders' cart (`StoreKind.Cart`) draws in it** instead of borrowing the market's (`art-inventory.md`'s gap).
  Under the three-band rule (`tools/legibility/check.py`: `HandcartColour` a fill judged by its ink outline, 4.98:1; 0 of 41
  fail).
- **Parked:** `VillageMap.DrawParkedCarts` — for every household with a cart and nobody pushing it, a chip beside the
  home in the heap chips' geometry (`CartChipRectOf` = `HeapRectOf(door)` a chip's width further down, so a heap at the same
  door never sits on it): the cart colour, inked round, its axle across. Who is pushing is read off the villagers once a
  frame — derived, never kept. Joe's ask in his words: *"i would like to see the house tile to show when a house has a
  cart."*
- **Pushing:** `DrawTheCartBehind` — a small rectangle trailing the dot against the leg's heading (`LegFrom` → `LegTo`;
  below the dot when they stand), as long as the dot is wide and a little narrower, clear of it by two pixels, drawn
  under the dot so the person is never hidden. `CartQuad` is pure, for the probe.
- **The household card:** a CART line in the door line's shape — *"a handcart, 37 trips left"*, *"a handcart, 37 trips
  left — out with Alys"*, *"a handcart, its last trip left"*; hidden with no cart (`Main.Cards.CartSentence`, one copy
  for the card and the probe). ⚠️ Not a distance on the villager card — D432 removed tile distances from villager cards;
  the shop-walk sentence belongs to the home card and the housing brush (`logistics.md §6`).
- **Probe lines:** **`cart:`** (`VillageMap.ACartIsSeen`) — the pushed cart trails the dot against three headings and
  never covers it, a standing pusher's hangs below, the parked chip sits beside a door outside the building and within a
  tile and never on the heap chip, the three drawings and the palette are one colour; **`cart card:`**
  (`Main.Cards.TheCardSaysTheCart`) — the four sentences above, posed and put back. `AVillagerDrawsWhereTheyStand` poses
  `PushingACart` by hand. The bar stays **151**.

## 9. Save, determinism, goldens

- **Save format 10 → 11:** `handcart_uses` on every household, `pushing_a_cart` on every villager; every format-10 save
  (the Slice W builds, 2026-11-01 to 11-04) is refused in words (D510). `SaveLoadTests.Fields` names both as saved;
  `save-load.md §4` and §9 carry the row.
- **Hash:** both sparse (§3, §4). A village with no cart hashes byte for byte as before — **the shipped goldens are the
  guard**: the unattended harness raises no workshop, so no cart ever exists there and **no golden moves** for any piece
  of this slice. The format bump touches no hash.
- **Determinism:** nothing here reads the clock or draws from `Rng`; the fetch and the push are walks through `Travel`.
- **Acceptance is `ZzLogistics`** — ✅ **measured, the slice as built against the same villages without a cart
  (`logistics.md §6` carries the verdict):** Hollowmere (Joe's Year-25 save, 7 households) five years on — **Fetch share
  17.6 → 11.3 %, shopping trips a household-year 5.69 → 3.49, fetch spells an adult-year 5.0 → 4.0, spells over four days
  49.5 → 34.7 %**; four unattended valleys over fifty years — Fetch 6.4 → 5.0 %, trips 5.93 → 4.25. The pushed trip's median
  (29 ticks in Hollowmere) is the long trip from the door it always was: the walk is the same walk, there are fewer. The
  ~2.5× of §1 was the four-person larder's arithmetic; a couple's half-larder fitted one armful already, and the measured
  village is a mix. ⚠️ Joe's Fernhollow save dies unattended under every arm (a harness is not a player, D447) and is
  excluded. His format-10 saves were read for the measurement by a TEMPORARY, uncommitted format tolerance, restored and
  checked by `git diff` before each commit.

## 10. Joe's calls — ✅ all six made as proposed, 2026-11-01 (D672: *"Yes, all six"*)

1. A cart is a household's good, parked at home, spares in the warehouse (§3) — ✅ yes. **Built, piece 1.**
2. The workshop a new building and the hut left alone; the hammer dropped as an item — ✅ yes. **Built, Slice W (D678).**
3. The handcart gated on the building alone, no knowledge node — ✅ yes. **Built, Slice W.**
4. Every cart line becomes a path on its first pass, D587's sentence (§7) — ✅ yes, as written; measured in piece 5
   before anything softens it.
5. A cart slower on grass than on the path (§6) — ✅ slower on grass, measured with and without (piece 5).
6. The marketer's cart in slice H, or later (§4)? — ✅ later.

## 11. How it is tested — `tests/Bclone.Sim.Tests/HandcartTests.cs`

TDD, each guard red-checked by a mutant and the reds counted (D326); a zero is written down, never hidden. The filter for
every score: `HandcartTests`, `LegibilityTests`, `EveryVillagerStateIsDeliberatelyClassified` (16 tests).

### 11.1 Piece 1 — the household's cart (13 guards, 21 mutants, 23 reds, 2 zeros written down)

| Guard | What it holds |
|---|---|
| `AHouseholdWithoutACartFetchesOneAndWalksItHome` | three carts on the shelf, everybody stood at their door: carts leave the shelf only into households, every take is logged, the fetch is a walk, the cart is pushed home, at most one member out per household and none while carted |
| `OneFetcherAHouseholdAndNoneOnceCarted` | posed directly: a housemate out for it holds the second back; nobody out, the second goes; a carted household walks for none with carts still on the shelf |
| `ARooflessFamilyFetchesNoCart` | the shipped founding, stood off the cart's shelves so a fetch would be a walk: nobody fetches, the shelf keeps its two |
| `ACartIsFetchedFromWhereToolsAreAndNeverTheMarket` | two carts put straight into a market's stockpile: nobody walks for them |
| `ADeadHouseholdLeavesItsCartAtTheHome` | the last member dies of old age with the cart in hand: the flag clears with them, the cart stays the household's a year on, the dead household counts for no cart |
| `AHouseholdWithACartWantsNoneFromTheCarpenter` | `HandcartsWanted` and the shortfall fall by one per carted household |
| `TheShopperTakesTheCartFromTheDoorstepAndParksItOnReturn` | a carted household at half a larder: the cart is taken on the tick a shopper leaves the door, pushed to the shop and home, parked at the door; one pusher at a time; nobody else's household pushes |
| `OnePusherAHousehold` | posed directly: one member out with it, the second sets off shopping on foot |
| `AShopperSettingOutFromElsewhereTakesNoCart` | a shopper deciding at the granary collects and goes home without the cart |
| `TheHashCoversTheCartAndThePusher` | both fields move the fingerprint |
| `ACartAndItsPusherSurviveTheRoundTripAndAFormatTenSaveIsRefused` | format 11; both fields read back, hash to hash and a year on; a format-10 save refused naming the build |
| `TheCartStatesHaveSentences` | both registers for both states and the pushing flag; neither state is work |
| `TheCartKnobsAreAsShipped` | `handcart_uses` 150 in the data and the C# default; 0 refused at load |

| Mutant | Reds |
|---|---|
| the cart fetch never asked in `Decide` | 1 |
| a carted household fetches another (`HandcartUses` unread in `TryFetchAHandcart`) | 2 — ⚠️ **0 until the pose stood everybody at their door**: beside the shelves the take is instant and the refusal recursed (see §3) |
| two fetchers a household (`SomebodyIsFetchingTheCart` false) | 1 — ⚠️ 0 until posed directly, for the same reason |
| a roofless family takes one at the shelf (`HasHome` unread in `TakeAHandcart`) | **0, written down**: the walk is refused before it (`TryFetchAHandcart`'s own check); kept as the store-side check for a roof lost on the way, un-posed |
| a roofless family walks for one (`HasHome` unread in `TryFetchAHandcart`) | 1 — ⚠️ 0 until the founders were stood off the shelves: a fetch begun and refused inside one tick leaves no state to see |
| the row's stores ignored (a market's cart counts) | 1 |
| the doorstep not required | 1 |
| the cart never taken at `Decide` | 1 |
| the cart never parked | 1 |
| two pushers a household (`SomebodyIsPushingTheCart` false) | 1 — ⚠️ 0 until posed directly: a season's run made one trip |
| `HandcartsWanted` ignores the cart | 2 |
| the household's cart unhashed · the pusher unhashed | 1 · 1 |
| `handcart_uses` not saved · `pushing_a_cart` not saved · the format left at 10 | 1 · 1 · 1 |
| the two sentences gone · the pushing sentence gone | 2 · 1 |
| the pusher rests at the steading (`RestPointFor`'s clause gone) | **0, written down**: no fixture shopper rests at a steading; the clause keeps the cart's home its own |
| the dead keep pushing | 1 |
| the fetch counted as work | 1 |

**View (by hand, the probe):** the `cart:` and `cart card:` lines ✅, `bar height: 151`, `tile centres` ✅, `new game:` ✅,
`done.`; `check.py` 0 of 41 fail. **The four checks (D686):** suite 1923 / 1 / 5 of 1929 in 14m39 — the one red a stale format pin
(*"reads 10"*), fixed and green under its filter; view 0 warnings; probe 106 lines, 70 ✅, 0 ⛔; goldens 398 → 398.

### 11.2 Piece 3 — what it buys (2 guards + a knob check, 5 mutants, 5 reds)

| Guard | What it holds |
|---|---|
| `ACartedShopperBringsCartArmfulsHomeAndAWalkerOne` | at an armful of 40 (D537's pose: a couple's 150 fits a shipped cart whole): the same shopper collects 40 on foot and `cart_armfuls` × 40 with the cart, never past the larder's room; the shipped armful is still 80 |
| `ThePlanAsksForTheCartsWorth` | a market holding an armful and a little, a granary holding plenty: on foot the market wins; carted at the door the granary wins; the market stocked for the cart's trip wins again |
| `TheCartKnobsAreAsShipped` | `cart_armfuls` 3 in the data and the C# default; 0 refused at load |

| Mutant | Reds |
|---|---|
| `CollectFromStore`'s load not multiplied | 1 |
| `ATripsWorth` not multiplied | 1 |
| `ArmfulsFor` reads the flag only (the doorstep take not foreseen) | 1 |
| `cart_armfuls` 0 accepted | 1 |
| the C# default 2 (the json governs; the guard reads both) | 1 |

### 11.3 Piece 4 — wear (2 guards, 3 mutants, 4 reds)

| Guard | What it holds |
|---|---|
| `ARoundTripWearsTheCartOneTrip` | a cart of 9 trips: nothing is worn on the road, 8 when the shopper is back at the door |
| `AWornOutCartIsGoneAndAnotherIsFetched` | a cart on its last trip, two on the shelf, every other household carted: it wears to 0 at the door, the log says so once, the household fetches another (150) and one is left on the shelf |

| Mutant | Reds |
|---|---|
| no wear | 2 |
| the walk home with a new cart wears it too | 1 (`AHouseholdWithoutACartFetchesOneAndWalksItHome`: a new cart parks with every trip in it) |
| the worn-through line silent | 1 |

### 11.4 Piece 5 — speed and tracks (2 guards + a knob check, 7 mutants, 7 reds, 1 zero written down)

| Guard | What it holds |
|---|---|
| `ACartIsSlowOnGrassAndQuickOnThePath` | seven grass tiles: 70 on foot, 105 under a cart, the grass count the same; the same tiles packed and repriced: the cart pays what the walker pays, and less than it paid on grass |
| `ACartsPassMakesAPathWhereAWalkersLeavesAFootstep` | a walker's pass over eight fresh grass tiles leaves 3 a tile in 8 ticks; a cart's pass leaves every tile at 30 or more in 12 ticks, and a tile already at 50 gains 3 |
| `TheCartKnobsAreAsShipped` | `cart_off_path_slow_percent` 150 in the data and the C# default; 99 refused at load |

| Mutant | Reds |
|---|---|
| the grass slow not applied in `WalkedOver` | 1 |
| the slow applied to every tile, path too | 1 |
| `PlanLeg` forgets the pusher | 1 — ⚠️ 0 until the tread guard counted the walk's ticks: the slow guard calls `WalkedOver` directly |
| the cart's pass treads a footstep | 1 |
| the cart's pass resets a worn tile to the line | 1 |
| `cart_off_path_slow_percent` 99 accepted | 1 |
| the last fraction of a tile in `Travel` forgets the cart | **0, written down**: `TravelForTest` walks to a tile's centre, so the fraction leg to a `standAt` point is never planned in a guard; the price is the same expression as `WalkedOver`'s and is read there |

**The slice's red-check in all: 36 mutants, 39 reds, three zeros written down** (§11.1 two, §11.4 one).

## 12. Definition of Done

`METHODOLOGY.md §3`'s six, with §8 and §11 as the manual checklist, and: pieces 1–5 built, measured and guarded; the four
checks green; no golden moved (the shipped harness builds no workshop); `logistics.md §6` carries the before-and-after;
`DESIGN.md §4`, §5 (candidate (a)), §6, §7 and `HANDOFF.md` in the final commit. Joe plays it, then merges at his word.

---

## 13. The trades' carts — the market's and the trading post's (Joe, 2026-11-05: *"one more owner of the same good"*)

**Status: 🔨 IN PROGRESS on `claude/elastic-dewdney-d55a19` (2026-11-05), off `main` 6a2f4fe (D691) — ✅ piece 1 built (D692: the owners, the fetch, the quota, save format 12); pieces 2 and 3 next. Three pieces, each its own
commit with its guards (`tests/Bclone.Sim.Tests/HandcartTests.cs`, the §13 facts), red-checked and counted (§13.8); measured on
`ZzLogistics` before typed (§13.6). Unplayed, unmerged.** The status line is made true at each commit.

> **Where this sits.** Joe, after playing Slice H (2026-11-05): *"does the marketer use a cart to move goods around? and what
> about the trading post worker?"* — and his pick: build it next, as **one more owner of the same good**. Call 6 of D672 had put the
> marketer's cart *later*; this is later. The ox cart (Heavy Haulage, the draught-animal node), the caravan and the laborers'
> hauls are not here; **nothing about the household's cart changes** (§3–§7 stand as built, their numbers untouched).

### 13.1 Why the trades, and what the numbers said first (§13.6 has the table)

The household's shop walk was the measured number (D668) and the household's cart moved it (D689). The marketer's walk is the
other half of the same distribution: every unit a household takes off the counter the marketer carried there, **one armful a
trip** (`LoadForTheMarket`, `carry_capacity`), from a granary that in Joe's villages stands beside the market or a walk away. On
Joe's Oakhaven (format 11, Year 31, two marketers) the Market bucket is **9.1 % of every adult's ticks — 18.3 counter trips a
marketer-year at 79 units a trip**, more than the village's whole hauling and nearly as much as its shopping. The merchant's two
runs (`BehaviorSystem.Trade.cs`) are the same shape one building over: an armful down to the post, an armful home from it.

### 13.2 What a trade's cart is — the household's model, one owner over

- **The owner is the workplace.** `Workplace.HandcartUses` — the trips left in the building's cart, **0 for no cart** — on a
  **standing** market (`JobKind.Marketer`) or trading post (`JobKind.Merchant`); every other workplace stays at 0 for ever. The
  same reasoning as §3's: the cart serves the *counter*, which is the building's, as the household's serves the larder. Saved as
  `handcart_uses` on the workplace (**save format 12**; every format-11 save — the Slice H builds', 2026-11-04 to 11-05 — is
  refused in words, which Joe accepts), **hashed sparsely under its own tag (`0xF2`, after the forge's `0xF0` and the workshop's
  `0xF1`)**, so a village whose trades have no cart hashes byte for byte as before.
- **Who has it in their hands** — `Villager.CartForTheWorks`: true while a worker is walking for the building's cart, pushing it
  to the building, or out on an errand with it; **`PushingACart` stays the one flag that says a cart is in their hands** (the slow
  grass walk, the tread, the rectangle behind the dot read it unchanged), and `CartForTheWorks` says *whose*. Saved as
  `cart_for_the_works`, hashed sparsely under tag 10 (after the pusher's 9). Invariant: pushing the works' cart ⇒ `PushingACart`.
  ⚠️ **The household's predicates read *the household's* cart from now on** — `PushingACart && !CartForTheWorks` — in
  `ArmfulsFor`, `CollectFromStore`, `SomebodyIsPushingTheCart` and the arrival at the door (`ParkTheCart`), so a marketer out
  with the market's cart neither borrows it for the family's shopping nor parks it at home; today, with no works' cart, every one
  of them reads as it did.
- **Fetched like the household's** (`TryFetchTheWorksCart`, asked in the trade's own branch — `WorkTheMarket`, `TryTrade` —
  *when the building has none and the worker is free*: `CanWork`, empty-handed, not pushing): one worker of the building walks to
  the nearest store racking a handcart (the row's stores — the Warehouse, the founders' Cart, the Pile — **never the market**),
  **one fetcher a building** (`SomebodyIsFetchingTheWorksCart`: a worker of it walking for one or pushing one back). The same
  states (`FetchingAHandcart`, `PushingAHandcart`) and the same log sentences; the villager's sentence says where the new cart
  is going (*"pushing the new handcart to work"* — the state has no world to name the building). At the shelf `TakeAHandcart` fills the **workplace's** count to
  `handcart_uses` and the fetcher pushes it to the building, where it is parked (no wear, as a new cart's walk home wears none);
  a refusal at the shelf (the seat lost on the way, the building carted meanwhile) ends the errand with `GoHome`, ⛔ never
  `Decide` (§3's recursion).
- **Taken at the building's door** (`TakeTheWorksCart`): when the worker **stands on the building's tile**, the building has a cart
  and no worker of it has it out (**one pusher a building**), as a carrying errand begins — the marketer's every errand from the
  market (`WorkTheMarket`: the restock, a dead larder, a buffer), the merchant's collection from the post
  (`CollectingForThePost`) and the haul the merchant begins at the post with what a boat left (`FetchingFromThePost`'s arrival).
  An errand begun anywhere else goes on foot (§4's rule: a cart kept anywhere but the start of the trip costs a walk to get it).
- **Parked at the building's door** (`ParkTheWorksCart`): whenever the pusher arrives at the building — the counter stocked
  (`PutItInTheMarket`), the post stocked (`StockingThePost`'s arrival), the post reached for a boat's goods
  (`FetchingFromThePost`'s arrival), the new cart pushed in — the flags clear and **the cart wears one trip** (§6's wear, one use
  a round trip, taken at the door), except for a new cart's own walk in. At nought the cart is gone (a DEBUG line), `HandcartsWanted`
  counts the building again and a worker fetches another.
- **The cart comes back.** The marketer's buffer and larder legs and the merchant's haul from the post end at a *storehouse*, not
  at the building; the household never has this problem because a shopping trip is out and back. So: **a worker holding the
  works' cart away from the building puts the building's work first** — `Decide` runs the trade's branch before the household's
  errands while `CartForTheWorks` is set — and **with nothing left to carry, takes the cart back** before going home, tidying ground
  or helping with a harvest: the empty leg is `StockingTheMarket` / `StockingThePost` with empty arms (*"pushing the market's
  handcart back"*), and the per-tick rule that an empty-armed stocker re-decides (D192's) excepts a pusher. A chain of errands
  (a lodge cleared to the granary, then the granary's food to the counter) keeps the cart in hand and parks it once; a chain is
  one round trip from the door's point of view and wears one.
- **A seat lost, a death, a move.** A pusher who no longer holds a seat at a market or a post (the allocator moved them) and a
  pusher who dies: the flags clear and **the cart counts as back at its building** — §3's rule for the household's (*written down
  rather than modelled: a cart is a good, not a positioned thing*). A worker who moves house keeps pushing the works' cart
  (`HouseholdSystem.MoveIn` clears only the household's flag; the cart was never the household's).
- **The carpenter's stop and the professions panel:** `LabourQuota.HandcartsWanted` counts **a standing market and a standing post
  without a cart as an owner each (+1)** beside the housed, living households without one — so Oakhaven, every household carted and
  the shelves bare, wants one more and the carpenter makes it (and the spare).

### 13.3 What it buys — the same `cart_armfuls`

With the works' cart in hand the trip's load is **`cart_armfuls` × `carry_capacity`** (240 as shipped) in place of one armful:
`LoadForTheMarket` (whole armfuls only, as many as the counter's room under its limit allows — D165's rule, the cart's size),
`TakeTheArmfulThatSentThem` (the buffer's food or the hides the test named), the dead larder's take, the merchant's
`CollectingForThePost` armful (still never below *keep N*, still within the post's room) and `FetchingFromThePost`'s. `carry_capacity`
and `cart_armfuls` themselves are untouched (D537, D687): the household's numbers stand. A load of three armfuls hauled to the
stores goes by `HaulOrSetDown` as every load does — the nearest store that takes the good and has room, the rest set down where
they stand (D96) — nothing new is built for it.

### 13.4 Speed, track

The pusher's flag is `PushingACart`, so the works' cart is **slow on grass and quick on the path** (§6, `cart_off_path_slow_percent`)
and **its pass makes a path** (§7, D587 call 8) by the same two lines of `Travel` — no code is added for either. The marketer's
line from the granary to the counter is a path already in every played village (§7's finding); the merchant's line down to the
river bank is the one new line a works' cart can draw, and it is drawn once.

### 13.5 What it looks like, what it says

- **Parked:** `VillageMap.DrawParkedCarts` draws the chip beside a market or a post that owns a cart nobody has out — the household
  chip's geometry on the building's tile (`CartChipRectOf`), the same colour. ⚠️ The set of households whose cart is out now reads
  *the household's* pushers (`PushingACart && !CartForTheWorks`), so a home's chip stays while its marketer pushes the market's cart.
- **Pushing:** the rectangle behind the dot, unchanged.
- **The card:** the market's and the post's cards get the CART line in the household card's shape — *"a handcart, 37 trips left"*,
  *"a handcart, 37 trips left — out with Alys"*, *"a handcart, its last trip left"*; hidden with none (`Main.Cards.WorksCartSentence`,
  one copy for the card and the probe). The household card's *"— out with"* reads the household's pushers only.
- **Sentences** (`Villager.DescribeState`): *"pushing the new handcart to work"* on the fetch's walk in; *"pushing the market's handcart back"* / *"pushing the post's handcart back"* on the empty leg; the loaded legs keep
  their words (*"carrying goods to the market"*, *"carrying goods down to … to sell"*) with the rectangle behind the dot saying the
  rest. `ZzLogistics` classes the empty leg where its state already sits (Market, Haul).
- **Probe:** `cart card:` extended — the market's card posed with a cart, out and not, and with none (a market is raised for the
  pose when the founding has none, the workshop probe's shape); `cart:` extended — the chip beside a workplace's tile, outside its
  square and clear of its heap chip. The bar stays **151**.

### 13.6 Measured before typed — `ZzLogistics`, the Market bucket and the trades' trips

The instrument learned three lines for this slice (`tools/harness/ZzLogistics.cs`): **`market:`** — the marketer's loads put on the
counter and carried out of a buffer, units a load, how many under the cart, trips a marketer-year; **`post:`** — the merchant's
loads down and up the same way; **`workscarts`** — which standing markets and posts own a cart at the end. And two arms:
`ZZ_MARKET=1` **raises a finished market** near the founding at Year 3 (⚠️ the harness valley raises none of its own — the
opening marks a granary and a warehouse, and a *marked* market was never built in fifty years; the first run proved it), `ZZ_POST=1`
raises a finished post with a keep order of 400 firewood (the limit is 600, so the woodcutter refills what the merchant carries
down). The household's arms (`ZZ_CARTS` 40 etc.) stand. **Before** is today's code on the same seeds and the same save.

*(The table is filled as each arm runs: before on 2026-11-05, after at piece 2.)*

| | Market share of adult ticks | counter trips a marketer-year (units a trip) | buffer trips a marketer-year | post: trips down a merchant-year (units a trip) | post: trips home |
|---|---|---|---|---|---|
| **Joe's Oakhaven (Year 31 → 36, 2 marketers, no post), before** | **9.1 %** | **18.3 (79)** | 0 (the hunters carry, D683) | — | — |
| four unattended valleys × 50 y, a market and a post raised at Year 3, before | *(running)* | | | 10.7–17.3 (60–71) | 0 (no boat buys: no buy order) |

### 13.7 Save, determinism, goldens

- **Save format 11 → 12:** `handcart_uses` on every workplace, `cart_for_the_works` on every villager; every format-11 save is
  refused in words (D510). `SaveLoadTests.Fields` names both; `save-load.md §4` and §9 carry the row.
- **Hash:** both sparse (§13.2). **The shipped goldens are the guard:** no golden run has a cart on a shelf (no golden raises a
  workshop with a carpenter at it), so no market can fetch one and **no golden moves**. `HandcartsWanted` rising by one for a
  standing market changes `HandcartsShortfall`, which only a carpenter's seat count and the view's chip read — nothing a golden
  run without a workshop steps. Any mover is named with its cause and proven (the owner switched off, the old value passes), never
  silently re-pinned.
- **Determinism:** nothing here reads the clock or draws from `Rng`.

### 13.8 How it is tested — `HandcartTests`, the §13 facts

TDD, each guard red-checked by a mutant and the reds counted (D326); a zero is written down. The filter for every score:
`HandcartTests`, `WorkshopTests.WithNoLimitSet…`, `WorkshopTests.AnEmptyHouse…`, `TradeRowsTests.TheFormat…`,
`MaintenanceTests.AFormat…`, `SaveLoadTests.EveryFieldIsSavedOrNamed` (32 tests).

#### 13.8.1 Piece 1 — the owners (8 guards + 4 Slice H guards re-aimed, 21 mutants, 31 reds, 2 zeros written down)

| Guard | What it holds |
|---|---|
| `AMarketWithoutACartSendsItsMarketerForOneAndParksItThere` | two carts put straight into the market's own stockpile: nobody walks for them; two racked in the warehouse, every household carted, a pinned marketer stood at home: a fetch for the works is seen walking and pushing, the market's count fills to `handcart_uses`, one cart is left, the log names the market, nobody is left holding the works' cart, and every household keeps its own |
| `OneFetcherAMarketAndNoneOnceCarted` | posed directly with two seated marketers: a colleague out for it holds the second back; nobody out, the second goes for the works; a carted market with carts on the shelf sends nobody |
| `APostWithoutACartSendsItsMerchantForOne` | a post raised, a pinned merchant: the post's count fills, one cart is left, nobody is left holding it, and the unmanned market still waits |
| `TheQuotaCountsAStandingMarketAndPostAsOwners` | `HandcartsWanted` is the housed households plus one for the fixture's market; a carted market drops out; a marked market site is not an owner; a raised post is; the shortfall follows |
| `APusherWhoLosesTheSeatOrDiesLeavesTheWorksCartAtTheBuilding` | the seat gone at `Decide`, and a death on the road: both flags clear, the market's trips stand, the household's cart is untouched |
| `TheHashCoversTheWorksCartAndItsPusher` | both fields move the fingerprint, the flag beyond the pusher's own |
| `TheWorksCartSurvivesTheRoundTripAndAFormatElevenSaveIsRefused` | format 12; both fields read back, hash to hash and a year on; a format-11 save refused naming the build |
| `TheWorksCartStatesHaveSentences` | *"pushing the new handcart to work"*, *"pushing the market's handcart back"*, *"pushing the post's handcart back"*; a loaded leg keeps its words; the household's sentence unchanged |
| *(re-aimed)* `AHouseholdWithoutACartFetchesOneAndWalksItHome`, `ADeadHouseholdLeavesItsCartAtTheHome`, `AHouseholdWithACartWantsNoneFromTheCarpenter`; `WorkshopTests.WithNoLimitSetTheWorkshopStopsAtOneAHouseholdAndASpare`, `AnEmptyHouseWantsNoCart` | the market is an owner too: the fixture's marketer may take one of the three racked carts, and every count of carts wanted adds the standing market |

| Mutant | Reds |
|---|---|
| the market's fetch never asked in `WorkTheMarket` | 2 |
| a carted market fetches another (`HandcartUses` unread in `TryFetchTheWorksCart`) | 1 |
| two fetchers a market (`SomebodyIsFetchingTheWorksCart` false) | 1 |
| the market's own counter counts as a shelf (any store holding a cart) | 2 |
| the take fills the household's count, not the works' | 3 |
| the new cart's walk goes home, not to the building | 2 |
| the market's cart never parked (`PutItInTheMarket`) | 1 |
| the quota ignores the trades | 5 |
| a site is an owner (`!IsSite` dropped from `IsACartOwner`) | **0, written down**: a site has no seats, so `Places > 0` already refuses it; the clause is kept as the stated rule, belt over braces |
| the works' cart unhashed · the works' pusher unhashed | 1 · 1 |
| `handcart_uses` not read on the workplace · `cart_for_the_works` not read · the format left at 11 | 1 · 1 · 4 |
| the new cart's sentence unchanged · the empty leg's sentence gone | 1 · 1 |
| the dead keep the works' cart · a lost seat keeps it | 1 · 1 |
| the post's fetch never asked in `TryTrade` · the post's cart never parked | 1 · 1 |
| moving house drops the works' cart (`MoveIn`'s clause gone) | **0, written down**: no guard poses a marriage mid-errand; the clause keeps the cart the works' |

### 13.9 Definition of Done

Pieces 1–3 built, measured and guarded; the four checks green; no golden moved (or each mover proven); §13.6's table filled before
and after; `storage-and-distribution.md §14`, `trade.md §4.5`, `save-load.md`, `content-inventory.md`, `DESIGN.md §4`, §6, §7 and
`HANDOFF.md` in the same commits as the code. Joe plays it, then merges at his word.
