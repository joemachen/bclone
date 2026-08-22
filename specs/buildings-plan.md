# Plan: Buildings — the foundational catalogue

> Status: **draft, deliberately loose.** This is a planning document, not a spec. Every number is absent on purpose and every entry is expected to move during QA. Owner: Joe + Claude.
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

What the exiles arrive able to do. Several of these are built.

| Building | Class | Knowledge | Does what |
|---|---|---|---|
| Home | Zoned | — | Shelter, household, birth. Built inside the residential brush. ✅ |
| Granary | Placed | — | Food store; total capacity is the population ceiling (D33). ✅ |
| Storage shed | Placed | — | Raw and refined materials. ✅ |
| Market | Placed | — | Manned redistribution within a catchment (D14). ✅ |
| Woodcutter's hut | Placed | — | Logs → firewood. ✅ |
| Gatherer's hut | Placed | — | Berries, mushrooms, wild herbs. Seasonal. |
| **Hunter's hut** | Placed | — | Meat, hides, tallow. Thins local game; recovers when rested. |
| **Fishing hut / staithe** | Placed | — | Species by water type — river, lake, coast yield differently. No minigame; the variance is seasonal and spatial. |
| **Cemetery** | Placed | — | The dead accumulate somewhere visible (D35). |
| Harvest zone | Zoned | — | Which forest may be felled. |
| Residential zone | Zoned | — | Where homes may go. ✅ |

**Note on food breadth.** D19's finding is that a binding catchment radius kills outlying households when there is only one raw food source. Hunter and fisher are therefore not content — they are the prerequisite for §2.2's central rule being survivable rather than merely cruel. They belong at T0.

**Note on the cemetery.** It is the cheapest building in the game and possibly the highest-value one. It is placed, it grows without further input, it needs no labour beyond burial, and it is the only object that renders a century of generational time in one glance. It is also the natural home for the epitaph text the life log already produces.

### 4.2 Tier 1 — Settlement

Earned by practice, or by the village simply being a village.

| Building | Class | Unlock | Knowledge | Does what |
|---|---|---|---|---|
| **Crop zone** | Zoned | Founding | — | Wheat, barley, rye, roots, cabbage, flax. Fertility declines under continuous cropping. |
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
| **Tool shed** | Placed | Civic | — | Tools are real, stockpiled, auto-distributed. Reconcile with the existing store set. |
| **Scriptorium** | Placed | By knowledge | ✓ | **Writes knowledge down.** See §5 — this is load-bearing. |
| **School** | Placed | Civic | ✓ | Transmits knowledge to more than one apprentice at a time. The civic-scale form of §2.1. |
| **Trading post / dock** | Placed | Civic | — | §2.4, currently unbuilt in any form. Dock if on navigable water; post if not. |
| **Church** | Singleton | Civic | — | The grown chapel. |
| **Tavern** | Placed | Civic | — | Consumes ale, wine, food. Happiness and, later, the vector along which news and disease both travel. |
| **Town hall** | Singleton | Civic | ✓ | Records, census, lineage. **Not a stats screen** — the place where the village's memory is kept. **⭐ Given its job by D176:** it holds the **knowledge screen** (`tech-tree.md §8`) and the **collections** — every crop, tree, fish, technique and first master the village has met, including the ones it has since lost. ⛔ **It gates the screen, not the tree**: the village learns by doing with or without one, and the log narrates as it happens. *Anecdote → archive.* The charts (`DESIGN.md §4`) live here for the same reason — this is the building whose product is information about yourself. |
| **Vineyard + press house** | Zoned + Placed | By knowledge | ✓ | Grapes → wine; apples → cider. One press, two inputs. |
| **Apiary** | Placed | By doing | — | Honey and wax. Also pollination, if orchards are worth linking to it. |
| **Physician's house / monastery** | Placed | By knowledge | ✓ | The late, bundled form of medicine — infirmary plus herb garden plus copying. Arrives *after* the herbalist, not instead of. |

### 4.4 Tier 3 — Branches, not a climb

Mutually exclusive-ish, per §2.7's *broad not tall*. Nobody gets all of these in one lifetime.

| Node | Answers |
|---|---|
| **Managed forestry** (planting brush) | A cleared valley |
| **Crop rotation / fallow** | Soil exhaustion |
| **Deep shaft, drainage, pit props** | The mine's declining yield curve |
| **Coal mine** | Charcoal competing with fuel and timber |
| **Stone bridge, stone road, stone granary** | Impermanence; the civic tier generally |
| **Selective breeding** | Herd yields |
| **Better oven, better kiln, better mill** | The §2.7 "unlock by doing improves quality" pattern |

---

## 5. The scriptorium is structural

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
| **Separate library** | The library is the room the scriptorium's output lives in. Not a building. |
| **Windmill *and* watermill** | One mill, sited by terrain. This makes the mill a *placement decision*, which is worth more than a second entry in a menu. |
| **Warehouse** | The storage shed already exists. A bigger one is a **tier of shed**, not a new building. |
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

## 10. Suggested roadmap order

Each step is a thing you could stop after, in the house style.

1. **Food breadth** — hunter's hut, fishing hut. D19's prerequisite; makes catchment survivable.
2. **Cemetery** — cheap, placeable, immediate generational payoff. D35.
3. **Preservation** — root cellar, smokehouse. Gives winter an answer other than quantity, and gives the processing chain a second instance.
4. **Crop and pasture zones** — extends the brush; introduces soil as a surface resource; §2.3 gets a second axis.
5. **Forestry pair** — harvest brush (already slice 4), then planting as the tech tree's first node (slice 5).
6. **Stone** — quarry, mason. First use of the subsurface effort rule; gates the civic tier.
7. **Iron and tools** — mine, charcoal burner, smelter, blacksmith. Tools multiply yields, which is where §2.1's skill pillar gets something to bite on.
8. **Knowledge** — scriptorium, then school. **Before** re-locking is punishing (§5).
9. **Trade** — post or dock. §2.4 finally gets a floor.
10. **Civic layer** — town hall, church, tavern.
11. **Branches** — the T3 table. By this point the pressures they answer have all been felt.

**The one hard dependency in that list** is 8 before any re-locking. The rest can reorder freely as QA dictates.
