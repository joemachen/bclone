# Content: Food & Production Catalog

> Status: **reference / partly implemented — Tier 0 `Fish` shipped 2026-09-03.**
> ⭐ **The first entry in `§3` is real**: a fishing hut on the bank, four seats, a 300-catch buffer a
> marketer runs dry, and `Fish` as a `GoodRow` with `Nutrition` (D277, D282–D284). ⚠️ **Every edible
> good must share one nutrition value and the config refuses to load otherwise** — the survival
> floor in `VillageEconomy` is derived from a single figure, so `§0`'s nutrition axis is still
> genuinely absent. The rest of this catalogue remains unbuilt.
> ⛔ Original status, kept: ⚠️ Written before D37 cut spoilage and D39 confirmed it: the preservation-versus-spoilage axis this leans on is **not** part of the design. The rest — the raw sources and the processing tier above them — is still the roadmap. · Owner: Joe + Claude Code
> This is a **content catalog**, not a behavioral system spec. It defines *what* foods and
> production chains exist. The *mechanics* (spoilage rates, preservation math, exact building
> requirements, yields) belong to a future system spec — likely something like
> `specs/food-and-preservation-system.md` — written when this content is actually scheduled
> for a build phase. Don't implement against this doc directly; write that spec first,
> using this as the ingredient list.

---

## 0. ⭐ The axis this catalogue does not have yet — nutrition (Joe, 2026-08-23)

> **Joe:** *"When we introduce food types (fish, meat, wheat, corn, apples, chicken — and
> prepared meals) they will have different 'nutritional' value which will help vary villagers'
> rhythms depending on what food they eat to sate their hunger."*

**This document lists *what foods exist* and says nothing about what any of them is worth**, which
is the gap that idea fills. Today there is one `Goods.Food` and one `food_per_meal`, so every
mouthful in the game is interchangeable.

**⭐ Why it is worth more than flavour: it is a third source of the thing D28 needed.** Phase 3
had to *manufacture* variation between villagers — a seeded personal rhythm drawn at birth
(D190), because two adults of one household ate on the same tick for ever otherwise. **Foods with
different nutritional values produce that variation for free and for a reason the player can
see**: two people eat different things, so they get hungry again at different times. *A mechanic
that falls out of content beats one that has to be injected*, and this is the version of §3.5 that
would not need a switch.

**⚠️ It lands on a derivation, not on a blank page.** `VillageEconomy` solves the survival floor
against `food_per_meal` — one number for one food. A catalogue of nutritional values means the
floor is solved against **the worst food a village might be living on**, or the derivation has to
change shape. That is the question to answer before any numbers get typed.

**Also open, and asked in the same breath (see `DESIGN.md §5`):** children eat half an adult's
meal today and **elders eat a full one**.

---

## 1. Purpose

A full catalog of raw, farmed, raised, and processed foods for the game, organized by
production tier. Exists so food/farming/husbandry/preservation work has a settled content
list to build against instead of being invented ad hoc mid-implementation.

## 2. Which pillars this serves

- **Systemic escalating pressure (`DESIGN.md §2.3`):** preservation (smoking, curing, salting,
  pickling) is what lets a stockpile actually survive a long winter — spoilage vs. preserved
  goods is a natural lever for the "town's own choices create the pressure" philosophy.
- **Living region / trade (`§2.4`):** salt is deliberately **not** foraged or farmed — it's a
  trade/mining good, giving the region economy something concrete to hinge on.
- **Knowledge-based tech tree (`§2.7`):** processed goods (cheese-aging, brewing, curing) are
  natural **"unlock by doing"** advances — practice-based, not menu-clicked.
- **Villager skill/knowledge transfer (`§2.1`):** a master brewer, cheesemaker, or butcher is a
  believable "knowledge lives in a person" case, same shape as the farmer/crop-rotation example
  in DESIGN.md.
- **Legibility / meditative pace (non-negotiables):** see the scope caution in §6 — this catalog
  is intentionally not a cooking-recipe tree, because that would fight both.

---

## 3. Tier 0 — Wild (foraging / hunting / fishing)

No farming or husbandry required; needs only a gather/hunt/fish action near a wild source.

| Item | Method | Notes |
|---|---|---|
| Berries (mixed) | Foraging | Feeds Tier 2 wine, jam |
| Mushrooms | Foraging | |
| Nuts (acorns, chestnuts, walnuts) | Foraging | Biome-flavor item; candidate for oil-pressing later |
| Wild greens / roots | Foraging | Variety filler |
| Wild herbs | Foraging | Ties to herbalist knowledge (see tech tree, `§2.7`) |
| Venison (deer) | Hunting | |
| Boar | Hunting | |
| Rabbit | Hunting | |
| Wildfowl (duck/pheasant) | Hunting | |
| Fish | Fishing | Split freshwater/coastal later if biome variety (`§2.5`) supports it |

> **Hunting is a real gap vs. the current design and worth building** — it's in vanilla Banished,
> fits the "no combat but nature bites back" theme, and gives a non-livestock hide/leather source.

---

## 4. Tier 1 — Farmed raw (crops)

| Item | Category | Notes |
|---|---|---|
| Wheat | Grain | → flour → bread; → beer |
| Barley | Grain | Dedicated beer grain if wheat is reserved for bread |
| Oats / Rye | Grain | Optional — denser/peasant bread variant |
| Corn | Grain/Vegetable | |
| Squash | Vegetable | |
| Potatoes | Vegetable | |
| Turnips | Vegetable | |
| Carrots | Vegetable | |
| Onions | Vegetable | |
| Cabbage | Vegetable | |
| Peas | Vegetable | |
| Beans | Vegetable | |
| Cherry | Orchard | |
| Apple | Orchard | → cider |
| Pear | Orchard | → perry |

> Vegetable variety exists mainly to feed the **food-variety-affects-health** mechanic
> (already present conceptually in Banished) — not for individual recipe depth.

---

## 5. Tier 1 — Raised (livestock)

| Animal | Outputs | Notes |
|---|---|---|
| Cattle | Meat (beef), milk, hides | |
| Pigs | Meat (pork) | |
| Chickens | Eggs, meat | |
| Sheep | Meat (mutton), milk, wool | Wool isn't food but closes the clothing loop later |
| Goats | Meat, milk | Hardier than cattle — candidate for rougher biomes (`§2.5`) |
| Apiary (bees) | Honey, wax | Wax isn't food; honey feeds mead, jam, sweetener use |

---

## 6. Tier 2 — Processed

The richest layer, and the one most tied to the preservation/pressure theme.

| Process | Input → Output | Notes |
|---|---|---|
| Milling | Grain → flour/meal | Precedes baking |
| Baking | Flour → bread | |
| Dairy | Milk → cheese, butter | Cheese ages/stores well — good "generational cellar" flavor |
| Butchery | Livestock/game → cuts of meat + tallow/lard | Lard = cooking fat + trade good |
| Smoking / curing | Meat or fish + (salt, for curing) → preserved product | **The** winter-survival tech |
| Pickling | Vegetables + vinegar/salt → preserved veg | Variety + shelf life |
| Preserves | Berries + honey → jam | Sweet, long shelf life, honey sink |

**Salt** is deliberately excluded from Tiers 0/1 — it should be a **mined or traded** resource,
gating curing/pickling and feeding the living-region trade system (`§2.4`).

### Booze
| Drink | Input | Notes |
|---|---|---|
| Beer | Wheat or barley | |
| Wine | Berries | |
| Cider | Apple | |
| Perry | Pear | Free pairing — orchards already exist |
| Mead | Honey | Second honey sink beyond jam |

---

## 7. Tier 3 — Meals (scope caution — read before building)

**Do not build a full cooking-recipe tree** (e.g. "stew = meat + vegetable + herb," each with its
own unlock). That directly fights two non-negotiables: **legibility** (recipe trees are exactly
the abstraction-heavy, hard-to-read system this project avoids) and **meditative pace** (it turns
food into a crafting minigame the player has to babysit).

**Recommended shape instead:** meals are abstract. *Any* prepared food eaten at a kitchen/tavern
(as opposed to eaten raw/plain) gives a flat variety/happiness bonus. No named recipes, no
recipe-specific unlocks. This keeps the complexity budget on the actual differentiators —
knowledge transfer, desire paths, the tech tree — rather than competing with them for the
player's attention.

---

## 8. Open questions (resolve when this becomes a real system spec)

- Spoilage: which raw goods spoil, at what rate, and does preservation reset or just slow decay?
- Are preserved goods a straight "better version" of a raw good, or a distinct item with its own
  storage/trade value (matters for the living-region trade system, `§2.4`)?
- Building requirements per process (smokehouse, mill, brewery, apiary, dairy, etc.) — likely a
  1:1 building-per-process-family rather than one mega "kitchen."
- Where salt comes from (mined locally in some biomes vs. always-traded) — ties into biome
  variety (`§2.5`) and the region economy (`§2.4`).
- Whether hunting depletes local wildlife over time (a natural tie-in to the systemic-pressure
  pillar, `§2.3`) or is an infinite renewable source.
