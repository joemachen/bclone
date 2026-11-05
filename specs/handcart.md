# Spec: The handcart — a household's cart, parked at its door — Slice H

**Decisions:** D668 (the measurement this exists to move), D669 (Joe: *"i say look at changing the game"*), D670 (the
cart thought through across every aspect), **D672 (Joe: *"Yes, all six"* — every call in §10 stands as proposed)**,
D678 / D682 / D684 (Slice W: the Workshop, the Carpenter and `Goods.Handcart` on a shelf — `specs/workshop-and-the-carpenter.md`),
D537 (the armful, one meaning), D587 call 8 (a cart's pass makes a path), D30 (a tool follows the person; a cart serves the
larder), D372 (the shop walk), D510 (an older save is refused in words), D291 (the hash is sparse over goods).
**Status:** ▶️ **SLICE H IN BUILD, on `claude/slice-handcart-b0230a` (2026-11-04), piece by piece, each its own commit with its
guards (`tests/Bclone.Sim.Tests/HandcartTests.cs`), red-checked and counted (§11). ✅ Piece 1 — the household's cart: owned,
fetched like a coat, walked home, parked, taken from the doorstep, hashed, saved (format 11), said (§3, §4, §9). ✅ Piece 2 —
seen: the chip beside the home, the rectangle behind the pusher, the card's line, the founders' cart in the cart colour,
two probe lines (§8). ✅ Piece 3 — what it buys: `cart_armfuls` 3, swept 2/3/4 on the harness and Joe's Hollowmere before typed
(§5; Hollowmere's Fetch share 17.6 → 11.3 %, trips a household-year 5.69 → 3.49). ✅ Piece 4 — wear: one use a round trip at
the door, gone at nought and another fetched; `handcart_uses` 150 measured as ~46 years of shopping and kept as Joe's number
with the 20/40 arms put to him (§6).
⏳ Piece 5 — speed and tracks (§6, §7).** Every number is a proposal until its piece's harness table is filled.
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
not the marketer's cart (Joe, call 6: later).

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

## 6. Wear, speed — ✅ piece 4 (wear, D688); ⏳ piece 5 (speed) (✅ Joe, call 5, D672)

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
- **Speed — ⏳ piece 5:** `cart_off_path_slow_percent` **150 on grass, 100 on worn and packed**, applied to the leg's ticks
  as the failing deck is (`WalkedOver`) — **the route unchanged**, so the cart takes the walker's path and the cost field
  stays one. Legible in a sentence: *the cart is slow on grass and quick on the path.* Measured with and without before typed.

## 7. Paths — ⏳ piece 5 (✅ Joe, call 4, D672)

D587 call 8 rules it: **a cart's pass treads every tile it crosses to `path_worn_at` at once** — drawn at once, priced at
the next spring, never a re-price per pass. With a cart at every doorstep, within a season every home is joined to the
market by a track: §2.6's loop made literal, and a village webbed with cart lines. **Measured before anything softens
it:** the trodden share of tiles and the path tiles with carts against without, on the harness; if it is too much, a cart
pass wears to *worn* and not *packed*, or only a loaded pass counts — Joe's call, not this slice's.

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
- **Acceptance is `ZzLogistics`** (pieces 3–5): on the harness with carts against without, and on Joe's format-10
  saves where a workshop stands (Hollowmere, Fernhollow): the Fetch share toward the control's 3–13 %, trips a
  household-year down ~2.5×, the median trip unchanged.

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

### 11.4 Piece 5 — ⏳

Written when built.

## 12. Definition of Done

`METHODOLOGY.md §3`'s six, with §8 and §11 as the manual checklist, and: pieces 1–5 built, measured and guarded; the four
checks green; no golden moved (the shipped harness builds no workshop); `logistics.md §6` carries the before-and-after;
`DESIGN.md §4`, §5 (candidate (a)), §6, §7 and `HANDOFF.md` in the final commit. Joe plays it, then merges at his word.
