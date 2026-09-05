# Plan: Buildings — the foundational catalogue

> Status: **draft, deliberately loose — catalogue brought current 2026-08-24 (D205).** This is a planning document, not a spec. Every number is absent on purpose and every entry is expected to move during QA. Owner: Joe + Claude.
>
> ✅ **AND §10 IS RECONCILED WITH REALITY, 2026-08-28 (D249, Joe: *"go with reality"*).** It had said knowledge was step 8 of 11 while `DESIGN.md §4` called it Phase 4 — **D159's two-roadmaps failure, live for six weeks and named in this very paragraph without being fixed.** ⛔ *A document that says "these two disagree and that is a design call" is a document deferring its own correction; it stayed deferred until somebody asked what it meant.* The catalogue in §4 is unchanged — **eleven** buildings and three zones now, with the library.
> Companion to `specs/building-placement.md`, which governs *how* buildings get put down. This governs *which ones exist and why*.

---

## 1. What this document is for

Per the standing habit: **catalogues before code.** This exists so that when a building lands, it lands with a settled answer to four questions rather than an invented one:

1. **What class is it** — placed, zoned, or terrain (`building-placement.md §5`)
2. **How is it unlocked** — founding, by-doing, by-knowledge, or civic scale (`DESIGN.md §2.7`)
3. **Is it knowledge-bearing** — does the last worker dying re-lock it
4. **What does it actually do** — in one sentence, or it does not belong here

If an entry cannot answer all four, it is content, not design, and it should be cut or deferred.

---

## 2. The two resource rules

These are prior to the catalogue, because they decide what half the buildings are *for*.

### 2.1 Surface resources are finite in place

Trees, soil, game, fish. **Spatial, exhaustible, recoverable.** They have a location, a quantity, and a regrowth rate. Over-use them and you can see where you did it. This is already the harvest brush's model (`building-placement.md §12`), and it should extend unchanged to soil, game and fish.

- Forest → recedes where felled, regrows on a generational clock, plantable once managed forestry is earned.
- Soil → fields lose fertility with continuous cropping; fallow and rotation restore it. Rotation is a knowledge node.
- Game and fish → local populations thin under sustained pressure and recover when left alone. **No new mechanic** — it is the same curve as forest, applied to a different terrain layer.

### 2.2 Subsurface resources are finite in effort

Ore, stone, coal, clay. **The seam does not empty. It gets harder.**

Yield per worker-year decays toward a floor as the workings deepen — longer haul to the face, water ingress, more shoring. A mine is never abandoned, because it is never dead; it is only worse than it was. The player's answers are to *invest in it* (drainage, pit props, deep shafts — all tech nodes) or to *open another one*.

**Why this rule rather than depletion:**

- **It cannot produce a dead artifact.** A worked-out pit that can never be moved or reused is a permanent scar on a map the player has to keep looking at, and it is a punishment with no play in it. There is no zero here to hit.
- **It couples the two rules.** Mining consumes timber for props. Over-clear the valley and the mine slows — an escalating pressure that back-traces to a decision, which is precisely §2.3's requirement.
- **It arrives in the right order.** The pressure is felt for a generation before the node that answers it exists. Same shape as managed forestry, which §2.7 already names as the model.
- **It is legible in one sentence.** *"The Redhill mine yields half what it did in your grandfather's day, and every year the water rises higher."*

**Consequence to design for:** the decay curve must be visible on the building, not discovered in a spreadsheet. A mine should be able to say what it yields now against what it yielded when it opened.

---

## 3. Classification vocabulary

**Placement class** (from `building-placement.md §5`):

| | Meaning |
|---|---|
| **Placed** | The player sites it. Many allowed. |
| **Singleton** | The player sites it. One per village (D38). |
| **Zoned** | The player paints intent; the village builds/works inside it as needed. |
| **Terrain** | Found, not built. |

**Unlock mechanism** (`DESIGN.md §2.7`):

| | Meaning |
|---|---|
| **Founding** | The exiles arrive knowing how. |
| **By doing** | Practice earns it — logging for two generations, baking across enough winters. |
| **By knowledge** | A person developed it, and it dies with them unless taught or written. |
| **Civic** | Requires a settlement of a certain size or permanence. Unlocks the *option*; knowledge unlocks the *quality*. |

**Knowledge-bearing test.** A building is knowledge-bearing if **its product could not be made by a stranger who watched the job done once.** Foraging, hauling, felling, building, farmhand work — not knowledge-bearing. Smelting, smithing, milling, medicine, masonry, brewing, copying — knowledge-bearing. That rule is worth keeping as the test rather than assigning the flag building by building, because it stays honest as the catalogue grows.

---

## 4. The catalogue

### 4.1 Tier 0 — Founding

What the exiles arrive able to do. **Ten of these are built.**

> **⭐ BROUGHT CURRENT 2026-08-24 (D205), AND IT HAD DRIFTED BADLY.** This table claimed **six**
> built and **had no row of any kind** for the builder's hut, the forester's hut, the farmhouse,
> the storage pile or the work-ground zone — **four of the ten `BuildingKind`s in the game, plus a
> whole zone layer.** Two more (the forager's hut, the harvest zone) were listed and simply never
> ticked. **A catalogue missing 40% of what is already built will generate content that duplicates
> it**, which is why this was fixed before the content pass rather than during it.
> *Nothing here was invented — every ✅ below names a thing that exists in the sim today.*

| Building | Class | Knowledge | Does what |
|---|---|---|---|
| Home | Zoned | — | Shelter, household, birth. Built inside the residential brush. ✅ |
| Granary | Placed | — | Food store; total capacity is the population ceiling (D33). ✅ |
| Storage warehouse | Placed | — | Raw and refined materials. ✅ |
| **Storage pile** | Placed | — | Cleared ground with goods stacked on it — **the one building that costs nothing**, and the first the player places (D76). ✅ |
| Market | Placed | — | Manned redistribution within a catchment (D14). ✅ |
| Woodcutter's hut | Placed | — | Logs → firewood. ✅ |
| Gatherer's hut | Placed | — | Berries, mushrooms, wild herbs. Seasonal. ✅ |
| **Forester's hut** | Placed | — | Fells and plants over its own painted work ground, in two modes — *plant only* and *fell and plant* (D87, D125). ✅ |
| **Builder's hut** | Placed | — | Funds the Builder job from spare hands; nothing stands until the work is done (D38). ✅ |
| **Farmhouse** | Placed | — | Owns painted field ground; sows in spring, reaps in autumn (D162). **This is §8.1's *zone plus a small steading*, already resolved and shipped.** ✅ |
| **Hunter's hut** | Placed | — | Meat, hides, tallow. Thins local game; recovers when rested. |
| **Fishing hut / staithe** | Placed | — | Species by water type — river, lake, coast yield differently. No minigame; the variance is seasonal and spatial. |
| **Cemetery** | Placed | — | The dead accumulate somewhere visible (D35). |
| Residential zone | Zoned | — | Where homes may go. **Global — the village owns it**, so the player picks the neighbourhood and the sim picks the tile (D86). ✅ |
| Harvest zone | Zoned | — | Which forest may be felled. ✅ |
| **Work-ground zone** | Zoned | — | Which ground a workplace may work. **Owned by a building rather than by the village** (D86) — the second of the two zone shapes, and the one any new zone has to choose between. ✅ |

**Note on food breadth.** D19's finding is that a binding catchment radius kills outlying households when there is only one raw food source. Hunter and fisher are therefore not content — they are the prerequisite for §2.2's central rule being survivable rather than merely cruel. They belong at T0.

**Note on the cemetery.** It is the cheapest building in the game and possibly the highest-value one. It is placed, it grows without further input, it needs no labour beyond burial, and it is the only object that renders a century of generational time in one glance. It is also the natural home for the epitaph text the life log already produces.

### 4.2 Tier 1 — Settlement

Earned by practice, or by the village simply being a village.

| Building | Class | Unlock | Knowledge | Does what |
|---|---|---|---|---|
| **Crop zone** | Zoned | Founding | — | ⚠️ **Partly built** (D162): painted field ground, sowing in spring and reaping in autumn all ship, owned by the farmhouse at T0. **What is NOT built is the plural** — one crop exists where this row lists six — and **fertility declining under continuous cropping.** Soil is generated and read by the farm (D178) but never depletes. |
| **Pasture zone** | Zoned | Founding | — | Cows, sheep, goats, pigs, fowl. Overgrazing thins the sward. |
| **Root cellar** | Placed | By doing | — | Slows spoilage of roots and grain. The cheapest preservation. |
| **Smokehouse** | Placed | By doing | — | Meat and fish keep through winter. |
| **Sawpit** | Placed | By doing | — | Logs → planks, for buildings that need better than round timber. |
| **Charcoal burner** | Placed | By doing | ✓ | Logs → charcoal. The only smelting fuel until coal. |
| **Tannery** | Placed | By doing | ✓ | Hides → leather. Sited away from homes, ideally. |
| **Weaver** | Placed | By doing | ✓ | Wool and flax → cloth. |
| **Tailor** | Placed | By doing | ✓ | Cloth and leather → clothing. Absorbs the cobbler; boots are not a building. |
| **Butcher** | Placed | Founding | — | Livestock → meat, hides, tallow. Feeds the tannery. |
| **Brewery** | Placed | By doing | ✓ | Barley → ale. Honey → mead. |
| **Herbalist's cottage** | Placed | By knowledge | ✓ | Herb garden and treatment of ailments. The early, one-person form of medicine. |
| **Quarry** | Placed | Civic | — | Stone. Effort-limited per §2.2. |
| **Clay pit** | Placed | By doing | — | Clay. Effort-limited. |
| **Kiln** | Placed | By doing | ✓ | Bricks, and crocks — which are what preservation is stored in. |
| **Mason's yard** | Placed | By knowledge | ✓ | Stone → blocks. Gates every durable building. |
| **Orchard** | Zoned | By doing | — | Fruit. Slow to mature — a planting whose payoff is a generation out, which is §1.5 in miniature. |
| **Chapel** | Placed | Civic | — | The small form of the church. Funerals, and the reason the cemetery has a shape. |
| **Bridge** | Placed | By knowledge | — | (D40.) Crossing changes catchment more than any other single placement. |

### 4.3 Tier 2 — Town

Requires scale, accumulated knowledge, or both.

| Building | Class | Unlock | Knowledge | Does what |
|---|---|---|---|---|
| **Mill** | Placed | By knowledge | ✓ | Grain → flour. **One building, sited by terrain** — on a river it is a watermill and faster; on open ground a windmill and slower. Not two buildings. |
| **Bakery** | Placed | By doing | ✓ | Flour → bread. §2.7's own worked example of unlock-by-doing. |
| **Creamery** | Placed | By knowledge | ✓ | Milk → butter, cheese. The point of cheese is that it keeps. |
| **Iron mine** | Placed | Civic | — | Ore. Effort-limited; consumes props. |
| **Smelter** | Placed | By knowledge | ✓ | Ore + charcoal → iron. |
| **Blacksmith** | Placed | By knowledge | ✓ | Iron + wood → **tools**, and fittings for better buildings. Tools raise yields, which is §2.1's hook. |
| **Tool warehouse** | Placed | Civic | — | Tools are real, stockpiled, auto-distributed. Reconcile with the existing store set. |
| **Scriptorium** | Placed | By knowledge | ✓ | **Writes knowledge down.** See §5 — this is load-bearing. |
| **School** | Placed | Civic | ✓ | ⭐ **Specified 2026-08-24 (D209), and it is the first T2 building with a full design behind it.** A **teacher** works it; **children attend 12–16** if there are open slots, and **another school is built when there are not**; graduates enter work **more proficient**. ⛔ **It is not free** — `adult_age` is 12, so every year at school is a working year the village gives up. `specs/school-and-education.md` |
| **Trading post / dock** | Placed | Civic | — | §2.4, currently unbuilt in any form. Dock if on navigable water; post if not. |
| **Church** | Singleton | Civic | — | The grown chapel. |
| **Tavern** | Placed | Civic | — | Consumes ale, wine, food. Happiness and, later, the vector along which news and disease both travel. |
| **Town hall** | Singleton | Civic | ✓ | Records, census, lineage. **Not a stats screen** — the place where the village's memory is kept. **⭐ Given its job by D176:** it holds the **knowledge screen** (`tech-tree.md §8`) and the **collections** — every crop, tree, fish, technique and first master the village has met, including the ones it has since lost. ⛔ **It gates the screen, not the tree**: the village learns by doing with or without one, and the log narrates as it happens. *Anecdote → archive.* The charts (`DESIGN.md §4`) live here for the same reason — this is the building whose product is information about yourself. |
| **Vineyard + press house** | Zoned + Placed | By knowledge | ✓ | Grapes → wine; apples → cider. One press, two inputs. |
| **Apiary** | Placed | By doing | — | Honey and wax. Also pollination, if orchards are worth linking to it. |
| **Physician's house / monastery** | Placed | By knowledge | ✓ | The late, bundled form of medicine — infirmary plus herb garden plus copying. Arrives *after* the herbalist, not instead of. |

### 4.4 Tier 3 — Branches, not a climb

Mutually exclusive-ish, per §2.7's *broad not tall*. Nobody gets all of these in one lifetime.

> **⛔⛔ THE FIRST ROW ALREADY SHIPPED, AND IT SHIPPED UNGATED (found 2026-08-24, D205).**
> `DESIGN.md §2.7` names managed forestry as **the tech tree's first concrete node** — *"what the
> node unlocks is the **planting brush**: the ability to put trees back… unlock-by-doing in its
> purest form"*. **The planting brush is built** (`WorkMode.PlantOnly` and `FellAndPlant`, D125)
> and **nothing gates it.** *The tree's own worked example is gone*, which matters for a content
> pass: **the exemplar everybody reasons from is no longer available to be the first node**, and
> §10's step 8 ordering assumed it still was.

| Node | Answers |
|---|---|
| ~~**Managed forestry** (planting brush)~~ ✅ **BUILT AND UNGATED** — see the note above | A cleared valley |
| **Crop rotation / fallow** | Soil exhaustion |
| **Deep shaft, drainage, pit props** | The mine's declining yield curve |
| **Coal mine** | Charcoal competing with fuel and timber |
| **Stone bridge, stone road, stone granary** | Impermanence; the civic tier generally |
| **Selective breeding** | Herd yields |
| **Better oven, better kiln, better mill** | The §2.7 "unlock by doing improves quality" pattern |

---

## 4.5 ⭐⭐ Reconciliation with `TECH-EXAMPLE.md` (Joe, 2026-08-24 — D206)

Joe's content pass wrote a **four-tier tree of 45 buildings** with construction costs, worker
counts and a named tech prerequisite each. **This section maps it onto the catalogue above rather
than replacing it**, because the two were written for different purposes: §4 says *which buildings
exist and why*, and `TECH-EXAMPLE.md` says *what they cost and what unlocks them*. **Neither is
redundant.**

⚠️ **Its tiers are offset by one from this document's.** Joe's T1 (*Pioneer Survival*) is this
document's **T0 Founding**; his T2 is roughly T1; his T3 spans T1–T2; his T4 is **beyond anything
here** and is genuinely new ground.

### 4.5a What it confirms — already in this catalogue, sometimes renamed

| Joe's name | This catalogue |
|---|---|
| Wooden Cabin | Home ✅ |
| Woodcutter's Hut · Forester's Lodge · Fisherman's Hut | Woodcutter's hut ✅ · Forester's hut ✅ · Fishing hut |
| Hunter's Lodge & Kennels | Hunter's hut *(+ hunting dogs, new)* |
| Market Square · Village Chapel · Tavern & Inn | Market ✅ · Chapel · Tavern |
| Sawmill · Quarry & Slate Works · Clay Pit & Brick Kiln | Sawpit · Quarry · Clay pit + Kiln |
| Gristmill & Bakery · Dairy House · Brewery & Cider Mill | Mill + Bakery · Creamery · Brewery |
| Slaughterhouse & Butchery · Tannery · Weaver's Cottage | Butcher · Tannery · Weaver |
| Smokehouse & Salting Warehouse · Root Cellar | Smokehouse · Root cellar |
| Smelter & Foundry · Blacksmith Forge · Deep Shaft Mine | Smelter · Blacksmith · Iron mine |
| Scriptorium · Town Hall · Trading Post / Dock | Scriptorium · Town hall · Trading post |
| Apothecary & Infirmary | Herbalist's cottage → Physician's house |
| Stone Cathedral | Church *(the grown chapel)* |

### 4.5b ⭐ What it genuinely adds

**Nine buildings this catalogue never had**, and most of them earn their place:

| New | Why it matters |
|---|---|
| **Stone Cottage** · **Insulated Manor** | ⭐⭐ **The house-upgrade ladder on a fuel axis** — wood hut *(high burn)* → stone *(50% less)* → insulated *(80% less)*. **This closes `DESIGN.md §5`'s open decision**, which asked for exactly this in exactly these words |
| **Compost Pit** | Manure and spoiled food → fertiliser. **Makes the herd branch feed the ground branch** — the web-not-columns shape §7a argues for |
| **Basic Well** | ⚠️ **Answers `§9`'s open question 1 — water IS a resource.** But Joe makes it *default unlocked*, where `tech-tree.md §9.7` had clean water taught by an outbreak |
| **Cartwright Warehouse** | Wagons and handcarts. The first thing in this game that would change **how much one person can carry**, which is `carry_capacity` — a load-bearing number |
| **Glassworks** · **Paper Mill & Ink Workshop** | ⭐ **Supply chains under things that were previously switches.** Writing now needs paper and ink to be *made*; the glasshouse needs panes. Good — it gives literacy a material cost rather than a flag |
| **Cooperage** · **Oil Rendering Station** · **Soapery** | Barrels, lamp oil, soap and candles from tallow and beeswax |
| **Pigeon Aviary & Sericulture** | Silk, guano, postal birds. ⚠️ The most speculative entry in the document |
| **Tier 4 entirely** — Glasshouse, Boiler House, Aqueduct, Blast Furnace, Great Library | **Beyond anything previously designed.** A late game made of *thermal automation* rather than more of the same |
| **Barn tiers** (Lean-To → Timber → Stone Homestead) | The *tier of warehouse* pattern §6 already blessed, applied to livestock |

### 4.5c ⛔ What it omits — and several are things its own costs require

**`TECH-EXAMPLE.md` spends goods that nothing in it produces.** Recorded because a catalogue that
consumes what it does not make is the gap that bites at build time:

| Missing | Why it is needed |
|---|---|
| ⛔ **The granary** | **It is the population ceiling (D33)**, and it is the literacy route's own source (§7a — *a keeper who has tallied stores for nineteen winters*). Almost certainly an oversight |
| ✅ ~~**The school**~~ | **CLOSED THE SAME DAY (D209).** Flagged here as *"the biggest single gap"*, and Joe specified it within hours: a **teacher**, a **school** with slots, children **12–16**, and graduates who work better. `specs/school-and-education.md` |
| ⛔ **Charcoal burner** | *"4 Charcoal per day"* is a stated fuel cost across the glasshouse and boiler. **Nothing makes charcoal** |
| ⛔ **Mason's yard** | *Cut Stone* appears in ~15 construction costs. The quarry extracts; nothing dresses |
| ⛔ **Cemetery** | D35, and this document calls it *"possibly the highest-value building in the game"* |
| ⛔ **Builder's hut, warehouse, storage pile, forager's hut, farmhouse, all three zones** | **All built and shipping.** Joe's document is a forward plan, not an inventory — noted so nobody reads the omission as a deletion |
| Pasture zone, tailor, orchard, bridge, vineyard | In this catalogue, absent from his |

### 4.5d ⚠️ Four §6 cuts are re-proposed, and a cut that comes back should be re-decided

**Not overwritten — flagged.** §6 exists so a rejected idea is not silently re-adopted a year later:

| §6 cut | Re-proposed as | Standing |
|---|---|---|
| *"Separate library — not a building"* | **Imperial Great Library** | ⛔ **`tech-tree.md §7c` also treats it as a building with shelf caps and fire, and D196 has a woodcutter learning *in* it.** Two of three say building. **The cut looks like the outlier** |
| *"Warehouse — a tier of warehouse, not a new building"* | **Vaulted Warehouse** | Consistent with the cut if it ships as a warehouse tier |
| *"Chandler / candles — flavour with no mechanic"* | **Soapery & Candle Workshop** | ⚠️ Still needs a mechanic. *There is no night work, because there is no twitch play* |
| *"Gold, gems, jewellery — nothing for them to attach to"* | *"highest gold value per weight unit"* | ⚠️ **Trade arriving may legitimately reopen this**, but it is a reversal |

⚠️ **And the watch-list held:** §6 warned that apiary, cider and wine were *"three of one too many."*
`TECH-EXAMPLE.md` proposes **all three plus ale and mead**. Now that morale is real (D207) they have
a mechanic to attach to — **which is the argument the watch-list said it wanted to hear.**

---

## 5. ~~The scriptorium is structural~~ ⚠️ SUPERSEDED IN PART BY D204

> **⛔ THIS SECTION DESCRIBES A PLAN THAT IS NO LONGER THE PLAN, and it is annotated rather than
> deleted because its *reasoning* is still the best statement of why records matter.**
>
> **D204 (Joe, 2026-08-24): recording is AUTOMATIC AT MASTERY** — no scribe, no literacy
> prerequisite, no seasons of dictation. The technique enters the records the moment its master
> reaches mastery. **So the scriptorium is off the critical path**, and §10's *"the one hard
> dependency in that list"* — item 8 before any re-locking — **is satisfied by a different route**
> rather than by this building.
>
> ⚠️ **What that costs, carried here as well as in `tech-tree.md §11`:** the guard against *"the
> library is mandatory"* rested on **three** costs — the scriptorium's opportunity cost, the hard
> shelf cap, and tacit nodes. **Automatic recording removes the first**, so the cap is carrying the
> guard nearly alone and must be built as though it is. A full library **refuses the record and says
> so**, naming what is on its shelves.
>
> **The scriptorium and literacy are deferred, not deleted.** Written down so a later session does
> not read the gap as an oversight and restore it without knowing why it went. ⭐ **And
> `TECH-EXAMPLE.md` strengthens the building's case for coming back**: it puts a **Paper Mill & Ink
> Workshop** underneath writing, which gives literacy a supply chain rather than a flag.

It deserves its own section because it is the only building that changes how a *rule* works rather than how a resource flows.

§2.7's stated failure mode is that **re-locking on death feels unfair if unforeseeable.** Apprenticeship is one answer: teach a youth and the knowledge survives. But apprenticeship is fragile in exactly the way the design wants it to be — a plague year takes master and apprentice together.

**The scriptorium is the second answer.** A technique written down survives a generation in which nobody knew it. That makes the knowledge system have two failure modes with two different costs, rather than one cliff:

- Lost with no record → gone, re-learnable only from scratch.
- Lost but written → **dormant, not gone.** A book can be read by someone who never met its author. Slower and worse than being taught, but recoverable.

Two consequences:

1. **Sequencing.** The scriptorium should exist before knowledge re-locking is punishing, or §2.7 ships its own named failure mode as a feature.
2. **It is not free.** Books need parchment or paper, which needs hides or hemp, and a person who can write, who is themselves knowledge-bearing. The safety net is a building the player has to choose to fund — which is the right amount of decision for something this important.

---

## 6. Superfluous — the cut list

Cutting is the more useful half of a catalogue. Each of these was considered and should not be built, with the reason recorded so it does not get re-proposed in a year.

| Cut | Why |
|---|---|
| **Bathhouse / laundry** | A third wellbeing building doing what church and tavern already do. Disease has better levers: crowding, water source, trade contact. |
| **Chandler / candles** | Flavour with no mechanic behind it. There is no night work, because there is no twitch play. |
| **Ropewalk** | One more chain step for a good with one consumer. Fold rope into the weaver or drop it. |
| **Distillery / spirits** | Duplicates ale and wine's role. One vice ladder, not three. |
| **Tobacco and cigars** | Same. If a luxury is wanted for *trade* rather than happiness, that is a different argument and should be made on those terms. |
| **Cobbler** | Folded into the tailor. Boots are a product, not a building. |
| ~~**Separate library**~~ ⛔ **REVERSED — it IS a building, and it shipped** (D226; annotated 2026-08-28) | ~~The library is the room the scriptorium's output lives in. Not a building.~~ `BuildingKind.Library` exists, is placeable, holds a hard shelf cap, is gifted once and can be demolished. `content-inventory.md` finding 3 named the three-way disagreement and `phase-4-the-tech-tree.md §2.2` resolved it **on recency**. ⚠️ The scriptorium it was cut in favour of is itself deferred (D204), so the cut outlived its own premise. |
| **Windmill *and* watermill** | One mill, sited by terrain. This makes the mill a *placement decision*, which is worth more than a second entry in a menu. |
| **Warehouse** | The warehouse already exists. A bigger one is a **tier of warehouse**, not a new building. |
| **City center** | The town hall. Pick one name. |
| **Gold, gems, jewellery** | No combat, no score, no wealth condition. Nothing for them to attach to. |
| **Fishing minigame** | ✅ Already dropped. Recorded here so it stays dropped. |

**Watch-list rather than cut:** apiary, orchard-cider, vineyard-wine. Each is defensible individually, but all three are "a pleasant good that makes people happier," and three of those is one too many. If the tavern's happiness input turns out to be satisfiable by ale alone, two of these become flavour.

---

## 7. Gaps this closes

Things absent from every list so far that the design already promised:

- **The cemetery.** D35, and the strongest generational object available.
- **Trading post.** §2.4 has nothing behind it at all.
- **The school.** §2.1 currently has only person-to-person apprenticeship; there is no civic-scale form of teaching.
- **Preservation as a category.** Winter is the pressure and nothing yet answers it except quantity. Root cellar, smokehouse, creamery, salt, crocks.
- **Salt.** Enables preservation, is a natural trade good, and is a terrain feature rather than a building — a spring or a seam.
- **Farms and pastures as zones.** See §8.1.
- **Water.** Undecided whether it is a resource at all. See §9.

---

## 8. Two structural recommendations

### 8.1 Crop fields and pastures should be brushes

The brush pattern in `building-placement.md §12` is better than a farm building with a radius, and farming should use it rather than reinventing the Banished model beside it.

> *The player paints intent over an area. The village acts on it when, and only when, it has a reason to.*

A crop zone painted over land the village then ploughs, sows and reaps has three advantages over a farm building:

- **Soil depletion becomes spatial.** Fertility is a property of tiles, so an exhausted field is a place you can see, exactly as a cleared forest is. That is the surface rule (§2.1) applied consistently rather than twice in two different ways.
- **Fallow becomes a player verb.** Un-painting land is resting it. No new UI, no policy slider — the same brush, used differently.
- **It costs no new mechanic.** Residential, harvest, plant, crop and pasture are five uses of one system.

The counter-argument worth hearing: a farm building gives farmhands a workplace with a catchment, and the labour allocator is built around workplaces. Likely resolution — **a zone plus a small barn/steading that is the workplace**, with the zone defining the work's extent. Worth deciding deliberately rather than by default.

### 8.2 Every building should be sayable in one sentence

Not a documentation standard — a design filter. If a building's purpose needs two clauses joined by "and," it is probably two buildings, or one building and a piece of flavour. The monastery in the original list failed this (hospital *and* herb garden *and* copying), which is why it is split here into herbalist → scriptorium → monastery-as-late-bundle.

---

## 9. Open questions

1. **Is water a resource?** Bread needs it, brewing needs it, disease travels through it. Options: ambient (rivers and wells are terrain, no resource), or a real good that is hauled and stored. Ambient is cheaper and probably right; a well then becomes a *placement* affecting where homes want to be, which is more interesting than a hauled bucket.
2. **Do tools deplete?** If tools are stockpiled and auto-distributed, they either wear out — which makes the blacksmith a permanent livelihood — or they do not, which makes it a one-off. Wearing out is more in keeping with everything else here, but it is a maintenance treadmill and needs a rate that does not become a chore.
3. **Where does salt come from?** Terrain feature (spring, seam) or trade-only? Trade-only makes §2.4 load-bearing early, which may be a feature.
4. **Does the harvest brush generalise to hunting and fishing grounds?** A "do not hunt here" brush is the same mechanic and would make game recovery a player decision rather than a passive curve. Possibly one brush too many.
5. **How many stores can one village reasonably have** before the nearest-store lookups become the cost the singleton seam was worried about? A performance question, but it bounds the catalogue.
6. **Does the tavern want more than one drink?** See the watch-list in §6.

---

## 10. Roadmap order — what shipped, and what is left

> **⚠️ REWRITTEN AGAINST REALITY 2026-08-28 (D249, Joe: *"go with reality"*).** This was a
> *suggested* order written before most of it existed, and the game then took a different route.
> **It is now a record of what happened followed by a plan for what has not**, because a roadmap
> that disagrees with the built game is worse than no roadmap: `DESIGN.md §4` said knowledge was
> Phase 4 while this said step 8 of 11, and **that contradiction stood for six weeks** — D159's
> failure, which cost this project six weeks the first time.
>
> ⛔ **The old numbering is kept beside each entry**, because five documents cite *"step 8"* and
> similar by number, and silently renumbering would break every one of those references.

Each step is a thing you could stop after, in the house style.

### ✅ Done, and not in this order

- ✅ **~~5.~~ Forestry pair — DONE**, and the second half did not land the way this line expected.
  The harvest brush shipped, and **planting shipped with it as an ordinary work mode, not as the
  tech tree's first node** (D125). See §4.4: *the tree's own worked example is already spent.*
- ✅ **~~8.~~ Knowledge — DONE, as Phase 4 (D225, D226), and by a different route than this
  proposed.** ⛔ **This step said *"scriptorium, then school"* and NEITHER exists**: D204 took the
  scriptorium off the path (recording is automatic at mastery) and the school is specified in
  `school-and-education.md` and unbuilt. **What shipped instead is techniques and the library** —
  a technique makes an existing trade better, and a library keeps it past its knower's death.
  - ⭐ **Its stated hard dependency held, by accident rather than by sequencing.** *"Knowledge
    before re-locking is punishing"* — re-locking is live, and the library shipped **with** it in
    the same phase rather than before it, which is what makes losing a technique fair.
- ◐ **~~6.~~ Stone — HALF DONE, and the half that shipped is the one this step did not name.**
  Stone is quarried from seams by the harvest brush, carried, stored, limited, and **spent: a
  granary costs 40 logs and 10 stone** (D213–D215). ⛔ **The quarry and the mason do not exist** —
  the material chain arrived without the buildings, so *"first use of the subsurface effort
  rule"* is done and *"gates the civic tier"* is not.

### What is left, in the order it now makes sense

0. **THE TOWN HALL, AND IT IS NOW FIRST (D252, 2026-08-29).** Out of step 8 below and to the front, because Joe settled its trigger and it unblocks Phase 4's slice 3. **A gift, not a purchase** — the last founder dies and the village raises a hall in their name. Spec: `specs/town-hall.md`. *The rest of the civic layer — church, tavern — stays at step 8; only the town hall moved.*

1. **~~1.~~ Food breadth** — hunter's hut, fishing hut. D19's prerequisite; makes catchment
   survivable. ⭐ **Joe chose this as what follows Phase 4** (2026-08-26).
2. **~~6.~~ Stone's buildings** — quarry and mason, to finish the half above and gate the civic
   tier the way the tier table assumes.
3. **~~7.~~ Iron and tools** — mine, charcoal burner, smelter, blacksmith. Tools multiply yields,
   which is where §2.1's skill pillar gets something to bite on. ⚠️ Iron is **mined and stored and
   spent by nothing** today, exactly as stone was before D213.
4. **~~2.~~ Cemetery** — cheap, placeable, immediate generational payoff. D35.
5. **~~3.~~ Preservation** — root cellar, smokehouse. Gives winter an answer other than quantity,
   and the processing chain a second instance.
6. **~~4.~~ Crop and pasture zones** — extends the brush; §2.3 gets a second axis. ⚠️ Soil is
   already generated, hashed and **read** since D178, so the surface-resource half of this is done.
7. **~~9.~~ Trade** — post or dock. §2.4 finally gets a floor.
8. **~~10.~~ Civic layer** — town hall, church, tavern. ⭐ **The town hall is now load-bearing
   rather than flavour**: `tech-tree.md §8` makes the knowledge screen its interior, so Phase 4's
   unbuilt slice 3 is waiting on this.
9. **~~11.~~ Branches** — the T3 table. By this point the pressures they answer have all been felt.

**⛔ The hard dependency that remains** is the town hall before the knowledge screen — and unlike
the old one, it is *unsatisfied*: the screen is specified and cannot be built diegetically until
the building exists. Everything else can reorder freely as QA dictates.
