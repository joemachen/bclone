# Spec: The handcart — a household's cart, and the workshop that makes it

**Decision:** `DESIGN.md §5`, *LOGISTICS — THE MECHANIC* (D669), candidate **(a)**: Joe, 2026-11-01 — *"hardcart (workshop
and carpenter) — where are unused handcarts stored at — at the warehouse? what do they look like on the map? lets just
think through what this means across all aspects first."* This is that thinking, written down so it outlives the chat.
**Status:** 💭 **DESIGN SKETCH — NOTHING BUILT (D670; ALL SIX CALLS MADE AS PROPOSED, D672, 2026-11-01).** ⛔ Blocked on the workshop + carpenter slice (D553
call 6, owed by `building-condition.md §4.6`, no spec yet — §5 here redefines it). **✅ Joe made all six calls as proposed (D672: *"Yes, all
six"*; §10), so the design is settled; every number below is still a proposal to be measured on `ZzLogistics` before it
is typed.** Candidate (d), *nearby* given a
meaning, is the near-term rule and is built separately (`logistics.md §5.6`, `storage-and-distribution.md §14.9.7`).

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
(`trade.md §4.7`, which this unblocks); not a porter (D357 — the Marketer is the porter); not a bigger armful alone (D537).

---

## 2. What is already on the record

- **The founders' cart** is a store: `StoreKind.Cart`, food and tools only (D64, D90, D215), no door (D625), drawn as a
  store quad in `MarketColour` — `art-inventory.md` lists *"a cart"* as a gap. A handcart must not borrow its name.
- **Tools are the model for an item a person takes**: a good with `tool_uses` (stone 150, iron 250), stored in the
  Warehouse / Cart / Pile — *the market does not stock tools* (`tools-and-the-smith.md §3.5`) — fetched from the nearest
  store holding one (`BehaviorSystem.TryFetchATool`), worn one use per action, buried with the villager. A tool hangs off
  the **villager** (D30) because the skill does.
- **Coats are the model for a fetched item with a life in time** (`BehaviorSystem.TryFetchACoat`, `clothing.md`).
- **The armful** is 80 for every errand, one meaning (D474, D536, D537); `sim.config.json`'s own comment on
  `carry_capacity` ends *"The Cartwright Warehouse, when it exists, raises it from here."*
- **Paths**: D587 call 8 is Joe's rule already — *one pass in a direction by a cart makes a path*; the tile is trodden
  all the way at once, drawn at once, priced into the one field at the next spring (D358), **never a re-price per pass**
  (`trade.md §4.7`). `PathWear.Tread(tile, amount)` exists.
- ⚠️ **The shared cost field cannot price anything dearer than grass** (`TerrainCostField.cs`: an entry cost is between 1
  and the base). The precedent for *slower* is the failing bridge deck (`failing_deck_slow_percent` 150): **the walk
  slows, not the route.** One shared cost field stays (`DESIGN.md §2.6`).
- **The workshop slice owed** (D553 call 6): *the builder's hut growing into a workshop with a carpenter trade and the
  builder's hammer* — no spec, nothing built. ⚠️ **D617/D618 since gave every hands-on adult, builders included, a tool
  once a smithy stands**, so the hammer is half-delivered as a generic tool and the slice needs redefining (§5).
- The tech tree is built on people-carried techniques (D225 onward); its node list is proposals (`tech-tree.md`: *every
  ★ number is a PROPOSAL, not a measurement*).

---

## 3. What a cart is in the model — ✅ Joe (call 1, D672)

**Proposed: a good, `Goods.Handcart`, owned by a household and parked at its home.**

- **Made** by the carpenter at the workshop (§5), **stored where tools are** until a household takes it: the Warehouse,
  and the founders' cart and the Pile while they stand — so *"at the warehouse?"* is right by the tools' own rule, and
  the market never stocks one.
- **Fetched** by a household the way a coat is fetched: one per household, when a store holds one and the household has
  none; the fetcher walks it home and it lives there. From then on **the shopper takes it from the doorstep** — no
  detour, which matters, because a cart kept anywhere but at the start of the trip costs a walk to get it.
- **Hangs off the household, not the villager.** A tool follows the person because the skill does; a cart serves the
  larder, which is the household's. A household that dies out leaves its cart at the home for the next family (a
  household's larder already does this).
- **Alternative, not recommended for the first cart:** a positioned entity like the trade boat or a nomad band — a new
  kind of thing to save, step and draw, when nothing moves on the map today but villagers. A later wagon with an ox may
  want it; a handcart does not.

---

## 4. Who pushes it and what it buys

The measured share is the **household's** shop walk, so the household's cart is the one that matters.

- A fetch today brings the shortfall capped at one armful (`ATripsWorth`, 80). **With a cart the cap is
  `cart_armfuls` × `carry_capacity`.** Three armfuls (240) fills a four-person larder's half in one trip where today it is
  two to five. By arithmetic the trips fall ~2.5–3×, and the Fetch share from ~20 % toward ~7–8 %; the unattended
  control, whose homes stand 4–6 ticks from their store, spends 3–13 %.
- **The marketer's cart is not in the first slice.** The Market bucket was small in every village measured; a marketer
  with a cart stocks faster, but nobody's shopping walk shortens. ✅ Joe (call 6, D672): *later*, not in slice H — when it comes it is one more
  owner of the same good, and the marketer is a carrying trade that takes no tool (`tools-and-the-smith.md §10.1`).
- Builders' and producers' hauls (the *Deliver* bucket) are out of scope; they were not the number.
- **Honest costs:** one walk to the warehouse per cart's life; the slower walk off-path if §6 is taken; and the cart's
  logs, which a small village spends on roofs.

---

## 5. Making it — the workshop slice, redefined — ✅ Joe (calls 2 and 3, D672)

**Proposed:**

- **The Workshop is a new building in the smithy's shape** (~25 logs, 12 stone, one or two seats — the smithy's numbers,
  to be measured), not the builder's hut *growing*. The hut stays as it is; nothing is lost and no building changes kind.
- **The Carpenter is a new trade** (`JobKind`), with a *Makes:* dropdown as the smith's *Forges:* — **handcart** first;
  later planks or furniture, which house tiers (D206) will want a maker for. The recipe is logs, measured; the carpenter's
  own tool wears as every trade's does.
- **A `handcarts` stock limit**, like tools' 30, so he stops; the demand is one a household, which the player can read
  off the limit's row.
- **The hammer is dropped as a separate item**, written down here rather than quietly: D617 gave the builder a tool, a
  third faster, iron doubling the work. A hammer would be a second tool for the same hands.
- **The gate is the building, not a knowledge node.** A workshop is a build like the smithy. *Heavy Haulage* stays the
  draught-animal node for the wagon later, which is what Joe's pass put in Tier 2.
- **Alternatives:** the hut grows into the workshop (the original sentence; a kind change for no gain); a technique
  carried by a master carpenter gates the cart (a new trade has no masters for twenty years — the gate would gate
  nothing, D205's lesson).

---

## 6. Capacity, speed and wear — ✅ Joe (call 5, D672)

- **`cart_armfuls`: 3** (§5 of DESIGN said *three or four*; the sweep with 2 / 3 / 4 on `ZzLogistics` picks it).
- **Speed:** *"a cart slower off-road is what to measure."* Proposed `cart_off_path_slow_percent` 150 on grass, 100 on
  worn and packed tiles, applied to the leg's ticks as the failing deck is — **the route unchanged**, so the cart takes
  the walker's path and the cost field stays one. Legible in a sentence: *the cart is slow on grass and quick on the
  path.* ✅ Joe (call 5, D672): the slower cart on grass, as proposed — still measured with and without before the
  number is typed.
- **Wear:** like a tool — a good with uses, **one use a round trip** (say 150: a few years of shopping), then the
  household fetches another. No repair: the condition system (D552) mends buildings, and a cart is a good.

---

## 7. Paths — ✅ Joe (call 4, D672)

D587 call 8 already rules it: a cart's first trip to the market wears its line to a path, drawn at once, priced in
spring. With a cart at every doorstep, **within a season every home is joined to the market by a track.** That is §2.6's
loop made literal — *the player paves what the town already proved matters* — and it is also a village webbed with cart
lines, and if every line is a path, the desire-path signal the player paves by flattens.

- **Measure** the trodden share of tiles and the number of distinct paths with carts against without, on the harness.
- **If it is too much:** a cart pass wears to *worn*, not *packed* (D587 says *all the way*, so this is a change to his
  sentence and is his); or only a **loaded** pass counts (the trip home), so the line is one-directional until it is driven
  both ways.

---

## 8. What it looks like, and what it says

- **Pushing:** the villager's dot with a small rectangle trailing it, in a **cart colour of its own** — the founders'
  cart borrows `MarketColour` today (`art-inventory.md`) and gets the new colour too. State text through
  `Villager.DescribeState`: *"pushing the cart to the market"*, *"pushing a load home"*.
- **Parked:** a chip beside the home in the heap chips' geometry (`VillageMap.HeapRectOf`), so a street of homes with
  carts reads as one at a glance. **Spares:** a crate in the warehouse's stock drawing like any good, and a row on its card.
- **The household card:** *"a handcart, N trips left"*. ⚠️ **Not a distance on the villager card** — D432 removed tile
  distances from villager cards; the legibility sentence D668 asked for (*how far the shops are*) belongs to the **home**
  card and the housing brush, and D201's service-area count already shows a market's homes.
- **Probe:** `AVillagerDrawsWhereTheyStand` poses a `Villager` by hand and must pose any new field; a line *"a cart is
  seen"* beside `AMealIsSeen`. The bar stays 151.

---

## 9. Save, determinism, goldens, acceptance

- Save format bump: a household's cart (good, uses left), a villager's *pushing* flag or the cart riding in `Carried`.
- The shipped-config harness builds no workshop, so the goldens should hold; if the opening ever builds one, they move
  and are re-pinned with the cause named.
- **Acceptance is `ZzLogistics` on Joe's saves once he has a workshop and carts:** the Fetch share toward the control's
  3–13 %, trips a household-year down ~2.5×, the median trip unchanged (the walk is the same walk, there are fewer).

---

## 10. Order, and Joe's calls

1. **Slice W — the workshop and the carpenter**, its own spec in `tools-and-the-smith.md`'s shape. Nothing to measure but
   that he works, stops at the limit, and the suite and probe stay green.
2. **Slice H — the handcart**: the good, the household fetch, the armful multiplier, wear, the drawing and the cards, and
   D587's cart path (`Tread` exists, so it rides along). Measured on `ZzLogistics` before and after.
3. Then `trade.md §4.7`'s caravan unblocks, and *Heavy Haulage* proper (oxen, wagons) is the knowledge node after.

**✅ Joe's calls — all six made as proposed, 2026-11-01 (D672): *"Yes, all six"*.** Calls 5 and 6 were put as
either/or; *as proposed* reads (5) as the spec's own proposal in §6, the slower cart on grass, and (6) as §4's, the
marketer's cart later — written here so a wrong reading is one line to correct.

1. A cart is a household's good, parked at home, spares in the warehouse (§3) — ✅ yes.
2. The workshop a new building and the hut left alone; the hammer dropped as an item (§5) — ✅ yes.
3. The handcart gated on the building alone, no knowledge node (§5) — ✅ yes.
4. Every cart line becomes a path on its first pass, D587's sentence (§7) — ✅ yes, as written; the trodden share
   with carts against without is measured in slice H before anything softens it.
5. A cart slower on grass than on the path (§6) — ✅ slower on grass, measured with and without.
6. The marketer's cart in slice H, or later (§4)? — ✅ later.
