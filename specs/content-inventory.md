# Inventory: what the village has, what the documents promise, and where they disagree

> Status: **an audit. ✅ PARTS A AND B REFRESHED 2026-10-07 (D526, Joe: *"refresh the inventory on main"*)**
> — read off the code on `main` at D519, with the food chain marked 🔨 on `slice/food-chain` (D520–D524); **the food
> chain merged to `main` at D527 (2026-10-07) and is ✅ below.** Part C (the disagreements) is the August audit, kept as history through D223.
> Not a spec and not a plan — it invents nothing and decides nothing. Owner: Joe + Claude.
>
> **Key:** ✅ built on `main` · 🔨 built on an unmerged branch · 📝 a spec exists, not built · 💭 named in
> `buildings-plan.md`, `food-catalog.md`, `tech-tree.md §9` or `TECH-EXAMPLE.md`, no spec yet.
> Companion to `buildings-plan.md` (which governs *which buildings exist and why*),
> `skills-catalog.md` (*what a skill is*) and `tech-tree.md` (*how knowledge is held and lost*).

**Why this exists.** Joe, 2026-08-24, stopping a Phase 4 plan: *"I think we might be getting ahead
of ourselves with the tech tree. It isn't fully thought out by me. I need to spend time thinking
about all of the tech and buildings and skills first."*

Three documents already hold most of that thinking. **They were written weeks apart, nobody had
checked them against each other or against the code, and they disagreed in seven places** — one of
which is D159's exact failure mode (two roadmaps that disagree; it cost six weeks last time).

> **⭐⭐ UPDATED 2026-08-24, LATER THE SAME DAY — THE CONTENT PASS LANDED AND ANSWERED THREE OF
> THEM.** Joe wrote `TECH-EXAMPLE.md` (D206): a four-tier tree of 45 buildings, 39 named
> techniques, 25 animal species, and pasture, fodder and breeding systems.
>
> | Finding | Standing |
> |---|---|
> | **1** — catalogue missing four built buildings | ✅ **Fixed** |
> | **2** — one material slot for every building | ✅ **Answered** — the new content cannot fit in one, so it is a schedule, not a question |
> | **7** — the tree's first node already spent | ✅ **Answered** — 39 techniques replace it |
> | **3, 4, 5, 6** | ⛔ **Still open** — design calls, not editing ones |
> | *(the school, flagged as the biggest gap in Joe's document)* | ✅ **Specified within hours** (D209) — teacher, slots, ages 12–16, `specs/school-and-education.md` |
> | **8** — ~70 goods against 6, all enums | ⛔ **New, and it is now the gating one** |
>
> **Finding 8 is the one to read first.** The content is settled enough to build toward; **the data
> model underneath it is not.**

**⛔ Every number in Part A was read off the code, not off a document's claim about the code.**
That rule exists because this project has now broken it four times, the fourth being a handoff's
own warning about it.

---

## Part A — what the village actually has today (refreshed 2026-10-07, D526)

**Read off the code, not off a document's claim about it** — `SimConfig.DefaultGoods`, `BuildingKind`,
`JobKind`, `SimConfig.Skills`, `SimConfig.Techniques`, `TechTree.DefaultNodes`. Goods, trades and buildings
have been **rows** since D210, D218 and D222; the enums are aliases for the built-in ids.

| | Count on `main` | Members |
|---|---|---|
| Goods | **13** | forage, logs, firewood, stone, stone tools, iron, fish, meat, leather, wheat, iron tools, flour, bread |
| Buildings | **21** | granary, warehouse, market, woodcutter's hut, stockpile, house, builder's hut, forager's hut, forester's hut, farmhouse, library, town hall, fishing hut, hunter's lodge, longhouse, smithy, well, quarry, iron mine, mill, bakery — and the founders' cart, a store that is not built |
| Trades | **13**, plus laborers | forager, forester, woodcutter, marketer, builder, farmer, fisher, hunter, smith, quarrier, miner, miller, baker. **Laborers** are everyone without a job: they clear painted ground and carry heaps |
| Skills | **8** | foraging, forestry, woodcutting, farming, building, trading, milling, baking. ⚠️ The fisher, hunter, smith, quarrier and miner have **no skill row** (D391) |
| Techniques | **4** | splitting lumber, coppicing, crop rotation, tended patches — see the knowledge table below |
| Crops | **1** | wheat (`CropRow` — a second crop is a row plus a *"grow…"* control) |
| Terrain | **9** | grass, water, forest, rock, iron seam, sapling, field, sown, ripe |
| Painted ground | **4 shapes** | housing land (the village's), harvest marks (the village's), work ground (a building's), house plots (a household's, D386) — plus the red remove brush (D494) |

### Food — the systems

| Food | Status | How |
|---|---|---|
| Forage | ✅ | Forager's hut, gathered in its wooded ring; nothing in winter; **thins only when the wood is felled** (D297) |
| Fish | ✅ | Fishing hut, which must touch water; never runs out; works in winter |
| Meat (+ leather) | ✅ | Hunter's lodge, hunts in the woods within reach |
| Wheat | ✅ | Farmhouse and its painted field — sown in spring, reaped in autumn, **rots if left standing into winter** |
| Flour → bread | ✅ | The mill grinds wheat (never while the village is hungry); the bakery bakes flour, lighting its oven once a stint (never with the winter's firewood). **Bread sates longer** (`Villager.FullFor`). Both learned together at 5,000 wheat reaped |
| A varied diet | ✅ | Meals and fetches take the best food first, then a share of each in proportion (D523) — before it, **meals took forage first and a forage-fed village never ate its wheat** (D521 §8.1) |
| The machinery | ✅ | Hunger and meals, granaries and larders, the market, a stock limit per food, the birth gate on stored food, dependants eating half, *"Food"* as the sum of every food (D298) |

### The other resources

| Group | ✅ On `main` | What spends it |
|---|---|---|
| Building materials | logs (felled), stone (dug by hand or quarried), iron (dug by hand or mined) | logs and stone raise buildings; stone and iron make tools |
| Fuel | firewood (split from logs) | hearths in winter; the smith's iron tools; the bakery's oven |
| Tools | stone tools (the founders' cart), iron tools (the smithy) | worn one use per action; a third quicker and a quarter more |
| Animal products | leather (from hunting) | ⚠️ **nothing yet** |
| Water | the well — households walk to it | no water good yet (Phase 6) |

### Knowledge — what the village can know, and how

| Thing | Status | How it is gained · how it is lost |
|---|---|---|
| **Skill and mastery** | ✅ | Time on the trade, per villager per skill; **mastered at 20 years** (`mastery_years`); an action's ticks fall as they learn, to **half at mastery** (`mastery_speed_bonus_percent` 50). A learner beside a master learns **twice as fast** (`apprentice_learning_bonus_percent` 100). Lost with the person |
| **Splitting lumber** (woodcutting) | ✅ | +15 % firewood a split. **PEOPLE**: worked out by someone who masters woodcutting *here* |
| **Coppicing** (forestry) | ✅ | +12 % timber. PEOPLE, from mastering forestry |
| **Crop rotation** (farming) | ✅ | +15 % harvest. PEOPLE, from mastering farming |
| **Tended patches** (foraging) | ✅ | +10 % forage. PEOPLE, from mastering foraging |
| *How a technique lives* | ✅ | **Unknown → Known** when someone masters the trade here (a founder's mastery does not count); **kept** while any master is alive; **Established** once written in a library, which is **automatic at mastery** if a shelf is free (D204; `library_shelves` 5); **lost** when the last master dies and no library holds it. An elder who is the only master is flagged *at risk* |
| **The library** (literacy) | ✅ | A **gift** after **15 years of a kept granary** (`literacy_years`) — free materials, the work owed |
| **The town hall** | ✅ | A **gift** when the **last founder dies** (D252) |
| **The quarry** | ✅ | Learned by doing: **200 stone dug by hand** — paid for |
| **The smithy** | ✅ | A **gift** after **50 iron dug by hand** (D444) |
| **The iron mine** | ✅ | Learned when the smith forges the **first iron tool** (D449) — paid for |
| **The mill and the bakery** | ✅ | Learned together at **5,000 wheat reaped** (D523, D524) — paid for |
| **The tech-tree map** | ✅ | Introduced by the first thing learned by doing (D440). Nodes: quarry, smithy, library, town hall, iron mine, mill, bakery, and a fogged horizon — **mason's yard → stone cottage, school** |

---

## Part B — what the documents promise (refreshed 2026-10-07)

### Food, planned

- 📝 **Livestock** (`livestock.md`) — a herd on painted pasture, hay for winter, butchery for meat and hides.
  ⛔ Blocked by D61: animals come by trade. `TECH-EXAMPLE.md` adds goats, ducks and geese, alpaca, yak, bees and silkworms.
- 📝 **Diet and health** (Phase 6) — food groups, and an immunity a one-food diet wears down.
- 📝 **Beer / ale** — its shape is recorded (`food-chain.md §13`), waiting on morale and the tavern.
- 💭 **Wild kinds** — nuts, roots, herbs; venison, boar, rabbit, wildfowl (`food-catalog.md §3`); `TECH-EXAMPLE.md` adds
  elk, moose, caribou, bison, ibex, beaver, fox and pheasant.
- 💭 **More crops** — barley, oats/rye, corn, squash, potatoes, turnips, carrots, onions, cabbage, peas, beans.
- 💭 **Orchards** — cherry, apple, pear (deferred: a generation to pay off, `crops-and-orchards.md §8`).
- 💭 **Processed** — cheese and butter (creamery), smoked and cured meat and fish, pickles, jam, hardtack; the root cellar.
- 💭 **Drinks** — cider, perry, wine, mead.
- 💭 **Glasshouse exotics** (`TECH-EXAMPLE.md` Tier 4) — citrus, tomatoes, peppers, winter grain, spices.
- 💭 **Meals** — kept abstract on purpose (`food-catalog.md §7`): one "a cooked meal" bonus at a tavern, no recipe tree.

### Buildings, planned (`buildings-plan.md §4`, 💭 unless marked)

- **T0:** cemetery.
- **T1:** pasture zone, butcher, root cellar, smokehouse, sawpit, charcoal burner, tannery, weaver, tailor, brewery,
  herbalist's cottage, clay pit, kiln, mason's yard, orchard, chapel, bridge.
- **T2:** creamery, smelter, tool warehouse, scriptorium (deferred, D204), 📝 school, trading post or dock, church,
  tavern, vineyard and press, apiary, physician's house.
- **T3 (branches):** crop rotation / fallow (✅ crop rotation shipped as a technique), deep shaft and drainage, coal
  mine, stone bridge / road / granary, selective breeding, better oven / kiln / mill.
- **From `TECH-EXAMPLE.md`'s 45** (`buildings-plan.md §4.5`): stone cottage and insulated manor (the house ladder,
  D206), timber and stone barns, compost pit, cartwright, glassworks, paper mill, soapery and candles, cooperage,
  oil renderer, pigeon aviary, apothecary and infirmary, inn, deep shaft mine, blast furnace, glasshouse, boiler
  house, heated aqueduct, a great library.

### Trades, planned

📝 herdsman, teacher · 💭 tailor, weaver, tanner, butcher, brewer, dairy hand, smoker or salter, cellarhand, sawyer,
charcoal burner, potter or brick burner, mason, carpenter (repairs, once buildings decay — Phase 5), cartwright,
cooper, glassblower, papermaker, scribe, soapmaker, renderer, herbalist or apothecary, physician, priest,
innkeeper, merchant, beekeeper, administrator.

### Resources, planned

| Group | Planned |
|---|---|
| Building materials | 💭 planks, cut stone, clay, bricks, mortar, iron ingots, steel, glass, rope, thatch, slate, iron parts, pipes, hoops — ⚠️ `TECH-EXAMPLE.md` names ~14 materials with 23 spellings (Part C finding 8) |
| Fuel | 💭 charcoal (the only smelting fuel until coal), coal, lamp oil |
| Tools | 💭 the builder's hammer (with the workshop); better tools per trade |
| Animal products and textiles | 📝 clothing (`clothing.md` — makes outdoor winter work possible; needs leather, wool or cotton) · 💭 wool, cloth, hides, tallow, bone, feathers, fur, silk, cotton, hay and silage |
| Water and health | 📝 water as a need; herbalists, an infirmary, disease (Phase 6) |
| Goods and trade | 💭 soap, candles, paper, ink, barrels, salt (mined or traded), luxury goods for trade (§2.4) |

### Knowledge, planned (`tech-tree.md`)

- **The eight unlock mechanisms (§4):** only **PEOPLE** is built. 💭 DOING, SCALE, SEREN (a seeded roll for someone
  deep in the practice), IMPORT (a migrant or a youth sent away), ADJ (two knowers together), CRISIS (the failure
  teaches it), TERRAIN (only thinkable in some places).
- **The branch catalogue (§9), ten trunks:**
  - **Ground** — ✅ tended patches, ✅ crop rotation, 💭 sowing, manuring (herder + farmer), fallowing, drainage,
    basic sanitation (compost), crop milling (✅ built as the mill's unlock by reaped wheat), subterranean
    engineering, thermal horticulture, hydronic heat.
  - **Woods** — ✅ coppicing, ✅ the planting brush (shipped ungated, D125), 💭 charcoal burning, orchard,
    mechanical carpentry (sawmill), container fabrication (cooperage).
  - **Herd** — 💭 trapping, penning, husbandry (blocked by D61), dairying, draught animals, livestock processing,
    avian and insect culture.
  - **Fire and materials** — ✅ the forge and iron tools (built ungated as the smithy's gift, D391/D444), 💭 kiln and
    pottery, lime burning, smelting, steel, subterranean mining, vitrification (glass), chemical rendering (soap),
    rendering and distillation (oil).
  - **Keeping** — ✅ the granary, 💭 drying, salting, smoking (a CRISIS node), root cellar, icehouse, fermentation,
    logistics management.
  - **Building and ground works** — 💭 masonry and stonecutting (the stone cottage), mortar, stone excavation
    (✅ shipped as the quarry's unlock), watermill (a TERRAIN gate), bridge, paving, advanced joinery (the manor),
    monumental architecture.
  - **Bodies** — 💭 midwifery and herbal medicine (both *unwritable*), clean water (✅ the well, free since D427),
    quarantine.
  - **Knowing** — ✅ the library (as the literacy gift), ✅ the town hall (civic governance, the founders' gift),
    📝 the school, 💭 tally-keeping, letters, paper and ink, the scriptorium (deferred, D204), a great library,
    formal apprenticeship, contracts and regional trade.
  - **Cloth and hide** — 💭 leather working, advanced textiles, fine fabrics.
  - **Gathering-in** — ✅ organized commerce (the market), 💭 community and faith, hospitality (the tavern),
    maritime and overland trade.
- ✅ **Splitting lumber** is the one built technique with no §9 row — it was §1's *first content* (D225).
- ⏸️ **Still Joe's** (§9a): the forge's *"and weapons"* (no combat), gold and barter value, crop cycles of 20–40 days,
  the barn before trade.

---

## Part C — ⛔ where they disagree

Stated as *what each side says* and *what it costs to get wrong*. **No recommendations** except
where a recorded decision already implies one.

### 1. ✅ FIXED — the catalogue was missing four of the ten buildings that already exist

**As found:** `buildings-plan.md` had **no row of any kind** for **BuilderHut, ForesterHut,
Farmhouse or Pile**, and none for the **work-ground zone**. Its ✅ marks claimed **6 built**; the
game has **10 building kinds and 3 zones**. Two more (gatherer's hut, harvest zone) were listed and
never ticked.

**✅ Brought current 2026-08-24 on Joe's call**, because it is the document a content pass starts
from and *a catalogue missing 40% of what is built will generate content that duplicates it*. Five
rows added, two ticked, the farm recorded as **§8.1's zone-plus-steading already resolved**, and
the crop zone marked **partly built** — field ground, sowing and reaping ship; the *plural* of
crops and *fertility decline* do not.

⛔ **Fixing the ticks did not fix the roadmap.** §10 still puts knowledge at step 8 of 11 against
`DESIGN.md §4`'s Phase 4 — see finding 5. *A spec that lies about its own status is worse than no
spec* (D159); this closed the status half only.

### 8. 🔨 ~70 goods and ~40 worker roles against 6 and 6 — GOODS SOLVED, THE REST NOT

> **✅ THE GOODS HALF IS BUILT (D210, `specs/goods-catalog.md`).** A good is a **row** now, the set
> is **open to 62**, and **nothing in the sim switches on a good by name.** Three ceilings nobody
> had counted were found and lifted on the way — see the box below.
>
> ✅ **And it is PROVEN, not asserted:** `ModdedGoodTests.cs` defines a seventh good in **real
> JSON** and drives it through storing, acceptance, stock limits and the state hash — **11 guards,
> red-checked** by reinstating the exact pre-fix bug and confirming two went red.
>
> ✅ **AND JOBS FOLLOWED (D218, `specs/jobs-catalog.md`).** A trade is a row, the quota is indexed,
> and `ModdedJobTests` proves a seventh in real JSON. **⛔ Its red check is the most useful thing
> either slice produced:** eight of nine new guards passed a break they should have caught, because
> the test's own catalogue listed rows in id order and so could not tell *id* from *position*.
> **D157's green-and-blind finding, third instance.**
>
> ✅ **AND BUILDINGS FOLLOWED (D222, 2026-08-26, `specs/buildings-catalog.md`) — THIS FINDING IS
> CLOSED IN THE SIM.** A building is a row, nine surfaces read it, and `ModdedBuildingTests` proves
> an eleventh in real JSON **staffed by a modded trade through `works_at`** — the seam
> `jobs-catalog.md §3` recorded. **804 passing of 806, every golden byte-identical.**
>
> ⛔ **What is still open is the VIEW, and it is a real hole:** `Main.BuildUi` is ten hand-written
> buttons in four categories, so **a modded building exists and the player cannot reach it.** This
> project's own rule — *a feature the player cannot reach does not exist* — and its fifth instance
> (D221). ⚠️ **The menu's categories are presentation rather than sim**, so whether it becomes
> catalogue-driven is Joe's call.
>
> ⚠️ **And one thing did NOT move, deliberately:** the per-building recipe keys (`granary_logs`,
> `hut_stone`, …) still price the built-in ten. **`logs_per_house` is an economy anchor the recipe
> happens to spend** — the warehouse's capacity, the stockpile's and the timber quota all derive against
> it — so folding it in is a re-derivation rather than a move.
>
> *Original text, for the record:*
> - ~~**`BuildingKind` has had no equivalent** — **~45 buildings against 10**, and it is where the
>   content pass actually needs the headroom. `BuildingRecipe.For` is still a switch with per-kind
>   config keys, and `JobRow.WorksAt` points straight at the enum, so **a modded trade can only
>   staff a building that already exists.**~~
> - ⭐ **One correction carried forward for that slice** (Joe, 2026-08-25): *"why does the granary
>   capacity have to be derived? it should be a set number."* **He is right — the set number already
>   exists**, `granary_feeds_people: 30`; the derivation only converts people into food so the
>   building's meaning does not drift when `food_per_meal` moves. The market is likewise two stated
>   numbers. **So capacity is mostly data**, and only the warehouse, stockpile, builder's hut and
>   gatherer's hut are genuinely derived from other systems.


**Added 2026-08-24 (D206), from `TECH-EXAMPLE.md`.** This is the finding that decides *when* the
content can be built, rather than what it is.

| | Today | Required | How counted |
|---|---|---|---|
| `Goods` | **6** | **~70** | ⚠️ **An estimate from a read**, not an enumeration — foods, animal products, textiles, fuels, consumables and construction materials together |
| `JobKind` | **6** | **~40** | Named worker roles in the four tier tables |
| `BuildingKind` | **10** | **45** | Counted: 8 + 14 + 17 + 6 |
| Construction materials alone | **1** (logs) | **14** | Counted from the cost column, after normalising aliases |

**⛔ AND COUNTING THEM TURNED UP THIS PROJECT'S RECURRING BUG, THIRD INSTANCE.** The cost column
names **23 distinct material strings for about 14 actual materials**: *Wood / Timber / Logs*,
*Stone / Cut Stone*, *Iron / Iron Ingots*, *Steel / Steel Ingots*, *Pipes / Iron Pipes*,
*Hoops / Iron Hoops*, *Parts / Iron Parts*.

**That is D148's finding and D188's, arriving a third time** — *"the view calls the same job two
different things, in two panels"*, and before that the site names. ⚠️ **It matters more here than
it looks**: these become `Goods` ids, and *Wood* and *Timber* resolving to the same id is a
decision somebody has to make deliberately rather than discover when two recipes disagree. **Cheap
to settle now, in the document; expensive once fourteen recipes are typed against it.**

`Goods`, `JobKind` and `BuildingKind` are **C# enums hashed by position**, pinned by every golden.
Seventy goods cannot be hand-added as enum values without touching the hash and re-taking the
goldens repeatedly — **and a modder still could not add one**, which is the promise D168 records:
*"modders should be able to add buildings, essentially add anything to the game."*

**⭐ This is D168's standing discipline arriving at the scale where the answer is forced** — *when
you add a new kind of thing, ask whether it wants to be an enum value or a data row.* **`SkillRow`
and the crop id are the two places this project already did it right**, and they are the templates.

### ⛔⛔ Two hard ceilings found while building the catalogue (D210) — neither was on any list

**The game cannot hold more than six goods today, and could not hold more than thirty even after
the obvious fix.** Both now fail at load with a sentence rather than at an index in the middle of a
run:

| Ceiling | Where | Why it is not a one-line fix |
|---|---|---|
| **6 goods** | `Stockpile.Kinds` is `Enum.GetValues<Goods>().Length`, read in a **field initializer** | Households, store buildings and workplaces all default their stockpile with `= new()`, so the count has to be **threaded**, not set. ⛔ **A mutable static is unusable** — the suite runs ~9.5× parallel with a world per test, so it would be a cross-test race and a determinism hazard |
| **30 goods** | `StoreBuilding.AllowedGoods` is an **`int` bitmask** with the `Spoken` sentinel at **bit 30** | Good 30 sets the sentinel — *a store the player never touched reporting that they had.* Widening it changes a hashed field, so the goldens move once, deliberately |

⚠️ **The second one would have been a spectacular silent bug**: not a crash, but a store filter
that switches itself on. *It is the kind of thing found by counting rather than by reasoning* —
`AllowedGoods` had never been read as *"how many goods can this game have?"* before.

⚠️ **The order matters.** Doing the rows first costs one migration; discovering it at building
thirty costs the hash, the goldens and every call site **at a point where there is far more of all
three.** *Written down, not scheduled.*

### 2. ✅✅ ANSWERED, AND NOW BUILT — every building costs logs and only logs

`BuildingRecipe` is `(int Logs, int WorkTicks)` (`World/Construction.cs:171`) — **one material
slot, for the whole catalogue.**

But `buildings-plan.md §4.2` says the mason's yard *"gates every durable building"*, §4.3 puts
stone behind the civic tier, and the whole T1→T2 climb assumes materials other than timber. **That
tier structure cannot exist against a one-material recipe.** Widening it is not large, but it is
**structural rather than content**, and it touches every recipe, the hauling, the build queue and
the goldens at once. Worth knowing *before* designing tiers that depend on it.

**✅ ANSWERED 2026-08-24 by `TECH-EXAMPLE.md` (D206), which settles it by simply assuming it.**
**Every one of Joe's 45 buildings costs two to four goods** — *"50 Planks, 20 Cut Stone, 15 Rope"*,
*"40 Stone, 30 Iron, 50 Glass, 20 Timber"*. There is no version of that content that fits one
material slot. **So multi-material `BuildingRecipe` is no longer a question, only a schedule** —
and it is the natural first slice whenever building resumes, because **it unblocks the entire stone
tier and the house-upgrade ladder together.**

**✅✅ BUILT 2026-08-25 (D213), on Joe'''s call — `specs/multi-material-construction.md`.** A recipe
holds N materials, the granary, warehouse and market cost stone, and **which buildings pay was measured
rather than chosen** (stone on the huts took a founding from 24 alive to 7). ⛔ **And it surfaced
a live stall nobody had counted:** D135'''s starved-head escape asked for a site that *already had
every material*, which was nearly always true while timber was the only one — with stone in play a
blocked head froze the whole queue, killing a played founding outright.

### 3. ~~The library is a building in two documents and cut in a third~~ — ✅ RESOLVED (D226), annotated 2026-08-28

> **Resolved on recency by `phase-4-the-tech-tree.md §2.2`, and then it shipped.** `BuildingKind.Library`
> is placeable, gifted once (D232), holds a hard shelf cap and can be demolished. `buildings-plan.md`'s
> cut list has been struck. ⭐ **This finding did its job**: it named a three-way disagreement, and
> the phase that resolved it cited the finding by number.

| Source | Says |
|---|---|
| `tech-tree.md §7c` | Its own building — hard shelf capacity, upgrade tiers, a keeper, and it burns |
| **D196 (Joe)** | *"the next woodcutter can spend idle time in the library learning it"* |
| `buildings-plan.md §6` cut list | *"Separate library — the library is the room the scriptorium's output lives in. **Not a building.**"* |

⚠️ **Two of the three are Joe's own words at different times.** The cut is the older one.

### 4. The scriptorium's premise is dead, and that is one day old

`buildings-plan.md §5` is titled *"The scriptorium is structural"*, and §10 names it **the one hard
dependency in the entire roadmap**: it must exist before knowledge re-locking is punishing, *"or
§2.7 ships its own named failure mode as a feature."*

**Joe's ruling of 2026-08-24 — recording is automatic at mastery — takes the scriptorium off the
path entirely.** The dependency is satisfied by a different route (recording no longer needs a
scribe, literacy, or a building beyond somewhere to put the record), so §5 is not *wrong* so much
as **describing a plan that is no longer the plan.**

⚠️ **One consequence to carry:** `tech-tree.md §11`'s guard against *"the library is mandatory"*
listed three costs — the scriptorium's opportunity cost, the hard shelf cap, and tacit nodes.
**Automatic recording removes the first**, so the cap is carrying that guard nearly alone.

### 5. ~~⛔ Two roadmaps that disagree — D159, again, in a different pair of files~~ — ✅ RESOLVED 2026-08-28 (D249)

> **Joe: *"go with reality."*** `buildings-plan.md §10` is rewritten as *what shipped, then what is
> left*, and `DESIGN.md §4` is unchanged because it was the one describing what actually happened.
> **Knowledge shipped as Phase 4** — and by a different route than §10 proposed, which is the part
> worth keeping: §10's step 8 was *"scriptorium, then school"* and **neither exists**.
>
> ⭐ **The finding was right and outlived its own argument.** It said a tech tree built then would
> have almost nothing to gate — **true, and Phase 4 answered it by not building a gate at all.**
> The first content is a technique that makes an existing trade better (`phase-4-the-tech-tree.md
> §1`), which needs no buildings to exist. *The objection was sound; the resolution was to change
> what the tree was, not when it arrived.*
>
> ⚠️ **It stood for six weeks after it was written**, and the disagreement was restated in
> `buildings-plan.md`'s own header as *"a design call rather than an editing one"* — which is a
> document deferring its own correction. **It stayed deferred until somebody asked what §10 was.**

*The original finding, for the record:*

| Source | Where knowledge sits |
|---|---|
| `DESIGN.md §4` | **Phase 4 — next.** The tech tree and the town hall |
| `buildings-plan.md §10` | **Step 8 of 11** — behind food breadth, cemetery, preservation, crop and pasture zones, forestry, stone, and iron and tools |

**This is the substantive argument for the pause.** The tech tree's job is *gating*, 18 catalogue
rows carry a knowledge flag, and **none of those 18 buildings exist.** A tech tree built now would
gate 10 buildings and 6 jobs — most of which `buildings-plan.md` classifies as Founding-tier and
therefore ungated by design.

⚠️ **Both roadmaps are live documents and neither is marked as superseding the other.** That is
precisely the state D159 spent a session unpicking.

### 6. Skills are *"rows in a data file"* that are not in the data file

`skills-catalog.md §4.1`: *"Skills are rows in a data file, not values in an enum."* True in the
model — `SkillRow` is a real row type. But **all six live as C# defaults at
`Config/SimConfig.cs:1273`, and `data/sim.config.json` contains no `skills` key at all.**

A modder *can* override them, because the list deserializes. **But the exemplar of D168's
discipline is invisible to exactly the people it was written for** — a modder reading the data file
would conclude the game has no skills.

---

### 7. ⛔⛔ The tech tree's own first concrete node already shipped, and it shipped ungated

Found while bringing the catalogue current, and **it is the finding that bears most directly on a
content pass.**

`DESIGN.md §2.7` names managed forestry as **the tree's first real content**, and makes an argument
out of it:

> *"What the node unlocks is the **planting brush**: the ability to put trees back. That is
> unlock-by-doing in its purest form, and it means the tech tree's first real content comes out of
> a system built for another reason entirely — which is the sign it was the right shape."*

**The planting brush is built** — `WorkMode.PlantOnly` and `WorkMode.FellAndPlant`, live in
`SimWorld` and `BehaviorSystem` since D125 — **and nothing gates it.** It shipped as an ordinary
mode toggle on the forester's hut, for the good reason that a valley nobody was managing felled its
own gatherer's ring and starved.

**Why it matters beyond bookkeeping:** the tree's worked example is **spent**. Every argument about
what a first node looks like has been reasoning from a node that is now free, and
`buildings-plan.md §10`'s ordering assumed it was still ahead. ~~**The first node will have to be
something else, and nothing currently proposes what.**~~

**✅ ANSWERED THE SAME DAY (D206).** `TECH-EXAMPLE.md` proposes **39 named techniques**, each
attached to a building that wants it — and **Joe confirmed they are diegetic, not a research menu**:
*"Masonry & Stonecutting"* is what a mason knows once he has mastered the trade. They are mapped
into `tech-tree.md §9` against §4's eight mechanisms, with two new trunks (**cloth and hide**, and
**commerce, faith and hospitality**) for content that did not fit the eight.

⚠️ **The struck-through sentence is left visible on purpose.** It was true for about six hours, and
*the gap being filled that fast is the strongest argument that holding Phase 4 for a content pass
was the right call.*

---

## Part D — the questions already on the board

**Gathered, not answered.** Everything here is recorded elsewhere; the value is seeing them at once.

### From `buildings-plan.md §9`

1. **Is water a resource?** Ambient terrain, or a real hauled good? (Ambient is cheaper and
   probably right — a well then becomes a *placement* decision.)
2. **Do tools deplete?** Wearing out makes the blacksmith a permanent livelihood; not wearing out
   makes it a one-off. ⚠️ Bears directly on Part C item 2.
3. **Where does salt come from?** Terrain feature, or trade-only? Trade-only makes §2.4
   load-bearing early.
4. **Does the harvest brush generalise to hunting and fishing grounds?**
5. **How many stores can one village reasonably have** before nearest-store lookups cost?
6. **Does the tavern want more than one drink?** (The apiary / cider / wine watch-list.)

### From `DESIGN.md §5`, bearing on content

- **Foods with different nutritional values** — ⭐ *a third source of D28's variation arriving from
  content instead of machinery.* ⚠️ Lands on a derivation: `VillageEconomy` solves the survival
  floor against one `food_per_meal`.
- **House upgrades, and firewood is the axis** — wood hut → stone house. ⚠️ **Needs Part C item 2**
  (a building that costs stone), and the 60–80 target is a **6–8× change to a derived burn**.
- **Nomads and the dead-village revival** — ⛔ hard prerequisite on **building decay**, which
  reopens D65 (*repair after damage, no decay on a timer*).
- **The job-name split** (D188) — `Gatherer`/`Vendor` versus `forager`/`marketer`, three places that
  must agree. **Cheap, and Joe's to choose**, because it changes words the player reads everywhere.

### The one question two documents ask from opposite sides

**Is proficiency retained from a record zero, or a small floor?** Asked in `tech-tree.md §12` and
`skills-catalog.md §12`, and **both say explicitly it must be answered in both places at once or
they will disagree.** Bounded by D176: **at most a floor, never restoration.**

---

## What this document is not

It does not propose a building, a skill, a technique, a tier or a number. Every one of those is
Joe's call, and `tech-tree.md §12` plus D196 refuse false precision on node lists **deliberately**
— *"we don't have to come up with the full list."*

**What it is for:** so the content pass starts from what is true rather than from three drafts and
a codebase nobody had diffed them against.
