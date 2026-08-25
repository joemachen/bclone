# Master Game Design Specification: Agriculture, Animal Husbandry & Settlement Infrastructure

> **Status: JOE'S ORIGINAL CONTENT PASS, 2026-08-24 — kept unedited as the record of the
> direction.** His framing: *"not 100% what I want — changes will need to be made — but it is
> directionally how i want the tech tree and buildings and whatnot to go."*
>
> ⛔ **THIS IS THE INPUT, NOT THE DESIGN.** It has been reconciled into the live documents and
> **they win where the two differ.** Read this for intent; read those for what the game does.
>
> | Reconciled into | What it holds |
> |---|---|
> | `specs/tech-tree.md §9` | The **39 named techniques**, mapped to the eight unlock mechanisms, plus **two new trunks** |
> | `specs/tech-tree.md §9a` | **What did not come across cleanly** — still open, still Joe's |
> | `specs/buildings-plan.md §4.5` | The four tiers mapped onto the existing catalogue: what it confirms, adds, omits |
> | `specs/morale.md` | Morale as a **real per-villager system** (D207) |
> | `specs/school-and-education.md` | The **school** (D209) |
> | `specs/livestock.md` | The animal content — ⛔ **still blocked by D61** |
> | `DESIGN.md §7` | Decisions **D206–D209** |
>
> **⚠️ Three things here are already superseded, and are NOT edited out of the tables below:**
> - **The Imperial Great Library's *"+50% apprentice XP"* is REMOVED** (Joe, D209).
> - **The fodder rot at *"5% per day"* is REFUSED** (D208) — D37 cut spoilage and it stands. Hay and
>   silage exist because **grass stops growing**, not because anything decays.
> - **The *"Tech Prerequisite"* column is NOT a research menu** (D206) — every entry is **diegetic**
>   and emerges; *"Masonry & Stonecutting"* is what a mason knows once he has mastered the trade.
>
> ⚠️ **Every number below is a proposal that has never been run.** *If a number goes into a
> document, it comes from a run* — none of these has had one.

---

## 1. Tier 4 Thermal Agriculture & Crop Systems

Tier 4 thermal agriculture bypasses seasonal freeze cycles entirely, enabling year-round cultivation of staple crops and exotic flora at the cost of steep fuel and material maintenance.

### Building & Operational Mechanics

**Tier 4 Glasshouse**

* **Environmental Immunity:** 100% immune to external snow, early frosts, blizzards, and summer droughts. Maintains an internal microclimate that grants a baseline **+25% growth speed buff** to all planted crops.
* **Thermal Failure Penalty:** Requires continuous heating during cold seasons. If fuel reserves drop to zero when ambient temperatures fall below 0°C (32°F), all glasshouse crops freeze and rot within 24 hours.
* **Building Requirements:** 40 Cut Stone, 30 Iron Ingots, 50 Glass Panes (smelted from Quartz Sand & Soda Ash at the Glassworks), 20 Timber. *Prerequisites:* 1 Master Builder and 1 Master Glazier.
* **Seasonal Fuel Consumption (Per Glasshouse):**
* *Summer:* 0 Fuel/day
* *Spring & Autumn:* 4 Charcoal (or 8 Firewood) per day
* *Winter & Blizzards:* 12 Charcoal (or 24 Firewood / 6 Mineral Coal) per day



**Heated Aqueducts & Geothermal Boiler House**

* **Subterranean Soil Warming:** A network of underground copper/iron pipes connected to a central Boiler House. Hot water and steam circulate beneath Glasshouse flooring and adjacent outdoor fields, preventing soil freezing and eliminating winter crop dormancy.
* **Hydration Synergy:** Provides automated sub-surface irrigation, keeping field soil at 100% moisture and reducing farmer watering tasks by 40%.
* **Boiler House Requirements:** 60 Fired Bricks, 40 Mortar, 20 Iron/Copper Pipes, 10 Steel Ingots. (Serves up to 4 connected Glasshouses or outdoor 10x10 field modules).
* **Pipe Line Cost (Per 5x5 Field Tile):** 3 Iron Pipes, 5 Mortar, 10 Brick.
* **Boiler Fuel Consumption:** 15 Mineral Coal or 25 Charcoal per day while operational during sub-zero months.

### Crop Specification Matrix

| Crop Type | Growth Cycle | Temperature Requirement | Primary Utility & Trade Value |
| --- | --- | --- | --- |
| **Citrus (Oranges & Lemons)** | Continuous Perennial | Warm (20°C+) | Eliminates winter scurvy; high barter value with northern traders |
| **Tomatoes & Bell Peppers** | 20 Days | Moderate (18°C+) | Morale-boosting fresh produce for Inns; reduces tavern meal prep costs |
| **Medicinal Belladonna** | 15 Days | Moderate (15°C+) | Core ingredient for Apothecary surgical anesthetics and pain relief |
| **Year-Round Winter Grain** | 25 Days | Mild (10°C+) | Provides non-stop flour production to survive multi-year blizzards |
| **Long-Staple Cotton** | 30 Days | Warm (22°C+) | Premium textile fiber processed into Fine Damask & Velvet clothing |
| **Rare Spices (Vanilla/Saffron)** | 40 Days | Warm (24°C+) | Luxury consumable; yields highest gold value per weight unit |

---

## 2. Animal Systems (Work, Farmed & Wild Game)

### Work & Transport Animals

| Animal | Primary Role | Special Work Mechanic | Resource Outputs |
| --- | --- | --- | --- |
| **Ox** | Heavy Draft | Doubles field plowing speed; hauls heavy timber and stone transport wagons | Beef, Heavy Hides, Bones |
| **Horse** | Fast Transport | Accelerates merchant trade caravans and scout map exploration speed | Leather, Horsehair |
| **Donkey** | Rugged Hauling | Low fodder requirement; carries heavy pack panniers through narrow mines and steep mountain paths | Raw Hide, Meat |
| **Hunting Dog** | Hunting Support | Accompanies hunters; tracks hidden game, retrieves waterfowl, and flushes wild birds | N/A (Maintained with meat rations) |

### Domestic Livestock (Farmed & Barn)

| Animal | Primary Output | Secondary Output / Utility | Farming Advantage |
| --- | --- | --- | --- |
| **Cow** | Milk / Cream | Beef & Heavy Hides | High Manure output for crop fertilizer |
| **Pig** | Pork & Lard | Tallow (Soap/Candles) | Consumes food waste and spoiled crops; fastest breeding rate |
| **Sheep** | Raw Wool | Mutton | Grazing maintains pasture health and cuts fallow recovery time |
| **Goat** | High-Fat Milk | Goat Cheese & Tough Leather | Thrives on low-grade forage and steep, rocky terrain |
| **Chicken** | Eggs | Poultry Meat & Feathers | Forages field insects, reducing crop pest events |
| **Duck & Goose** | Down Feathers | Fatty Meat & Eggs | Water-resistant down for high-tier winter clothing |
| **Alpaca / Llama** | Fine Alpaca Wool | Light Mountain Pack Transport | Premium insulation wool for high-value trade garments |
| **Yak** | Heavy Milk | Cold-Resilient Fleece | Immune to blizzard temperatures; ideal for high-altitude farms |
| **Pigeon** | Postal Transport | Guano (Fertilizer/Saltpeter) | Speeds up distant trade notifications and emergency alerts |
| **Bees (Apiary)** | Honey & Beeswax | Crop Yield Buff (+15%) | Supplies candle workshops, apothecaries, and cider/mead breweries |
| **Silkworms** | Raw Silk Thread | Fine Luxury Fabrics | Requires Mulberry tree groves; yields top-tier luxury export goods |

### Wild Game (Hunted & Trapped)

| Animal | Category | Primary Output | Harvesting Method |
| --- | --- | --- | --- |
| **Deer** | Standard Big Game | Venison, Leather, Small Antlers | Bow Hunting / Stalking |
| **Elk & Moose** | Large Forest Game | High-Volume Meat, Large Antlers, Heavy Hides | Team Hunting with Dogs |
| **Caribou / Reindeer** | Migratory Herds | Thick Winter Pelts, Venison, Sinew | Seasonal Autumn/Spring Migration Hunts |
| **Bison** | Plains Herds | Prime Leather, Massive Meat Yield, Horns | Open-Field Plains Hunting |
| **Mountain Ibex** | Alpine Game | Mutton, Horns, High-Agility Pelts | High-Altitude Bow Hunting |
| **Wild Boar** | Forest Game | Pork, Tusks, Heavy Lard | Forest Hunting |
| **Rabbit** | Small Game | Tender Meat & Soft Fur | Snare Trapping / Dog Flushing |
| **Beaver & Muskrat** | Water Furbearers | Water-Resistant Pelts, Castoreum | Riverbank Traps |
| **Fox & Ermine** | Furbearers | Luxury Winter Furs | Baited Box Traps |
| **Pheasant & Grouse** | Wild Fowl | Game Meat, Decorative Feathers | Flushing with Hunting Dogs |

---

## 3. Pasture, Fodder, Breeding & Barn Management

### Pasture Sizing & Grazing Dynamics

Pasture viability depends on **Tile Capacity** and **Grass Regeneration Rates**. Overgrazing strips the land down to mud, stopping grass growth and triggering health penalties until the soil recovers.

| Livestock Class | Pasture Tiles / Head | Daily Grass Drain | Rotation Buff / Soil Effect |
| --- | --- | --- | --- |
| **Small Fowl** (Chicken, Duck) | 0.5 Tiles | Very Low | Scatters manure; controls pests (+5% neighbor crop yield) |
| **Small Ruminants** (Sheep, Goat) | 2 Tiles | Low | Trims weeds; converts fallow cropland into high-potency topsoil |
| **Medium Draft** (Donkey, Alpaca) | 3 Tiles | Medium | Minimal soil compaction; light manure yield |
| **Large Ruminants** (Cow, Yak, Ox) | 5 Tiles | High | High manure output; heavy trampling requires frequent pasture resting |
| **Heavy Equine** (Horse) | 6 Tiles | High | Requires high-quality grass; slow pasture recovery rate |

* **Pasture Rotation:** Dividing pastures into two halves and toggling access every 30 days grants a **+20% Grass Regeneration Speed** and prevents soil degradation.
* **Winter Dormancy:** Grass stops growing when ambient temperatures fall below 5°C. Unharvested pasture grass dies back into dry thatch, forcing animals to rely on barn fodder.

### Winter Fodder & Feed Storage

Animals cannot graze during winter or blizzards. Herders must stockpile feed in specialized storage structures before autumn ends.

* **Hay (Basic Fodder):** Mowed from summer fields and stored in **Hay Lofts** (built on top of Barns to save space). Standard feed for sheep, goats, horses, and cattle.
* **Silage (Nutritive Fodder):** Chopped corn, grain, and root crops fermented inside a **Masonry Silo**. Increases winter milk yield by +25% and reduces winter weight loss.
* **Grain & Slop:** Pigs require grain (Barley/Corn) or kitchen waste/spoiled food slop.
* **Consumption Rates & Spoilage:**
* Large animals (Cows, Yaks, Horses) consume **2 Feed Units/day**.
* Small animals (Sheep, Goats, Pigs) consume **1 Feed Unit/day**.
* Uncovered fodder stored outside rots at 5% per day during autumn rain. Indoors or in Silos, spoilage drops to 0%.



### Livestock Breeding & Herd Management

Herd growth relies on maintaining adult male-to-female ratios, barn space, and nutrition levels.

* **Gestation & Litter Sizes:**
* **Pigs:** 30-day gestation | 4–6 Piglets per litter | 40-day maturation.
* **Sheep / Goats:** 45-day gestation | 1–2 Lambs/Kids | 60-day maturation.
* **Cows / Yaks / Horses:** 90-day gestation | 1 Calf/Foal | 120-day maturation.


* **Male-to-Female Ratios:** 1 Bull/Ram/Boar can service up to 10 Females. Excess males consume feed without contributing to birth rates.
* **Auto-Slaughter Thresholds:** Players can set automated barn limits (e.g., *"Maintain 12 Female Cows, 1 Male Cow"*). Excess mature animals are automatically routed to the Slaughterhouse when offspring reach adulthood, yielding Meat, Hides, Tallow, and Bone.

### Barn Tiers, Sanitation & Disease

Poor housing conditions lead to *Livestock Murrain* (epidemic outbreaks), reducing milk output and causing animal deaths.

| Barn Tier | Capacity | Warmth Rating | Construction Requirements | Special Features |
| --- | --- | --- | --- | --- |
| **Tier 1: Livestock Lean-To** | 6 Small / 3 Large | Unheated (High Risk) | 30 Logs, 20 Straw | Outdoor trough; no winter insulation |
| **Tier 2: Timber Barn** | 16 Small / 8 Large | Insulated (+10°C) | 50 Planks, 20 Cut Stone, 15 Rope | Built-in Hay Loft; reduces fodder waste by 15% |
| **Tier 3: Stone Homestead Barn** | 40 Small / 20 Large | Heated (-20°C Proof) | 80 Bricks, 40 Mortar, 30 Iron Bars | Automated Troughs, Heated Waterers, Integrated Silo |

* **Manure & Cleanliness Metric:**
* Each animal generates **Manure** daily. As manure builds up, the Barn Cleanliness rating drops.
* Below **50% Cleanliness**: Milk and wool yields drop by 30%.
* Below **20% Cleanliness**: High risk of Disease Spreading (Foot-and-Mouth disease, Rot).


* **Herdsman Tasks:** Herders automatically shovel manure from barn floors and move it to the **Compost Pit**. After 60 days of decomposition, compost is spread onto crop fields to restore +35% Soil Nitrogen.

---

## 4. Comprehensive 4-Tier Settlement Building Progression Tree

### Tier 1: Pioneer Survival

| Building | Construction Cost | Worker Capacity | Tech Prerequisite | Primary Function / Utility |
| --- | --- | --- | --- | --- |
| **Wooden Cabin** | 15 Wood, 10 Thatch | 0 (Housing for 4) | *Default Unlocked* | Basic shelter for villagers; high winter fuel burn. |
| **Woodcutter’s Hut** | 10 Wood | 2 Woodcutters | *Default Unlocked* | Harvests nearby trees for logs and firewood. |
| **Forester's Lodge** | 15 Wood | 2 Foresters | *Default Unlocked* | Replants saplings and manages forest density. |
| **Hunter’s Lodge & Kennels** | 15 Wood, 5 Rope | 2 Hunters, 1 Master | *Default Unlocked* | Stalks game, sets snares, and breeds hunting dogs. |
| **Fisherman’s Hut** | 12 Wood, 4 Rope | 2 Fishermen | *Default Unlocked* | Gathers fish from river and coastal tiles. |
| **Basic Well** | 10 Wood, 10 Cut Stone | 0 (Passive Effect) | *Default Unlocked* | Provides fresh water and basic fire suppression. |
| **Root Cellar** | 20 Wood, 10 Cut Stone | 1 Cellarhand | Primitive Preservation | Underground insulated storage for harvested food. |
| **Compost Pit** | 15 Wood, 10 Straw | 1 Herdsman | Basic Sanitation | Processes animal manure/spoiled food into nitrogen fertilizer. |

### Tier 2: Settlement Expansion

| Building | Construction Cost | Worker Capacity | Tech Prerequisite | Primary Function / Utility |
| --- | --- | --- | --- | --- |
| **Stone Cottage** | 20 Cut Stone, 15 Planks | 0 (Housing for 5) | Masonry & Stonecutting | Durable housing with 50% reduced winter fuel burn. |
| **Sawmill** | 25 Logs, 10 Cut Stone | 2 Sawyers | Mechanical Carpentry | Mills raw logs into construction planks and staves. |
| **Timber Barn** | 50 Planks, 20 Stone, 15 Rope | 2 Herders | Animal Husbandry | Shelters livestock with built-in hay loft storage. |
| **Clay Pit & Brick Kiln** | 30 Wood, 20 Cut Stone | 3 Pitmen, 2 Burners | Kiln Firing | Harvests raw clay and fires construction bricks. |
| **Tannery** | 20 Planks, 15 Cut Stone | 2 Tanners | Leather Working | Processes raw hides into durable leather. |
| **Gristmill & Bakery** | 30 Cut Stone, 20 Planks | 2 Millers, 2 Bakers | Crop Milling | Grinds grain into flour and bakes bread/hardtack. |
| **Market Square** | 40 Planks, 20 Cut Stone | 4 Market Vendors | Organized Commerce | Centralized food/goods distribution hub. |
| **Village Chapel** | 40 Cut Stone, 30 Planks | 1 Priest | Community & Faith | Elevates morale and buffers against harsh winter distress. |
| **Cartwright Shed** | 25 Planks, 10 Iron Parts | 2 Wheelwrights | Heavy Haulage | Builds and maintains transport wagons and handcarts. |
| **Quarry & Slate Works** | 40 Logs, 20 Rope | 4 Miners | Stone Excavation | Extracts structural stone blocks and roof slate. |
| **Slaughterhouse & Butchery** | 30 Planks, 15 Cut Stone | 2 Butchers | Livestock Processing | Converts livestock into meat, tallow, hides, and bone. |
| **Dairy House** | 25 Planks, 15 Cut Stone | 2 Dairy Hands | Dairy Processing | Processes raw milk into butter and storable cheeses. |
| **Smokehouse & Salting Shed** | 20 Cut Stone, 15 Planks | 2 Smokers / Salters | Curing Methods | Cures meat and fish using salt and hardwood sawdust. |
| **Brewery & Cider Mill** | 35 Planks, 20 Cut Stone | 2 Brewers | Fermentation | Ferments grain, apples, and berries into ale and cider. |

### Tier 3: Industrial Power & Knowledge

| Building | Construction Cost | Worker Capacity | Tech Prerequisite | Primary Function / Utility |
| --- | --- | --- | --- | --- |
| **Insulated Manor** | 30 Stone, 20 Bricks, 10 Mortar | 0 (Housing for 8) | Advanced Joinery | High-density housing with 80% reduced fuel burn & +25% Morale. |
| **Smelter & Foundry** | 50 Bricks, 30 Mortar, 20 Pipes | 4 Smelters | Pyrometallurgy | Smelts iron, copper, and coal into industrial ingots. |
| **Blacksmith Forge** | 30 Bricks, 10 Iron Ingots | 2 Smiths | Metalworking | Forges tools, iron hoops, hardware, and weapons. |
| **Town Hall** | 80 Stone, 50 Planks, 20 Iron | 3 Administrators | Civil Civic Governance | Enables tax policies, immigration control, and town overview. |
| **Paper Mill & Ink Workshop** | 40 Planks, 20 Bricks, 5 Parts | 3 Papermakers, 2 Inkmen | Scholastic Engineering | Manufactures fine paper sheets and iron gall ink vials. |
| **Scriptorium** | 40 Stone, 30 Planks, 10 Glass | 4 Scribes | Manuscript Illumination | Compiles research codexes, blueprints, and records. |
| **Glassworks** | 50 Bricks, 30 Mortar, 10 Steel | 3 Glassblowers | Vitrification & Soda Ash | Melts quartz sand into glass panes and apothecary vials. |
| **Vaulted Warehouse** | 50 Planks, 30 Stone, 15 Mortar | 4 Logistics Haulers | Logistics Management | Secure high-capacity storage for refined materials. |
| **Trading Post / Dock** | 60 Planks, 40 Stone, 20 Hoops | 2 Merchants | Maritime & Overland Trade | Facilitates bulk import/export trade with foreign merchants. |
| **Deep Shaft Mine** | 60 Timber, 40 Iron Parts | 6 Miners | Subterranean Mining | Extracts iron ore, copper ore, coal, and quartz sand. |
| **Soapery & Candle Workshop** | 25 Bricks, 15 Planks | 2 Soapmakers | Chemical Rendering | Combines tallow, rendered oil, and beeswax into soap/candles. |
| **Weaver’s Cottage & Mill** | 35 Planks, 20 Cut Stone | 3 Weavers | Advanced Textiles | Weaves wool, cotton, alpaca fiber, and silk into fabrics. |
| **Cooperage** | 30 Planks, 15 Iron Hoops | 2 Coopers | Container Fabrication | Manufactures barrels and casks for liquid and food storage. |
| **Oil Rendering Station** | 30 Bricks, 20 Cut Stone | 2 Renderers | Rendering & Distillation | Extracts lamp oil, tallow, and bone ash from fish/offal. |
| **Pigeon Aviary & Sericulture** | 25 Planks, 15 Cut Stone | 2 Keepers | Avian & Insect Culture | Produces postal transport, saltpeter guano, and raw silk. |
| **Apothecary & Infirmary** | 40 Cut Stone, 25 Planks | 2 Apothecaries | Herbal Medicine | Treats injuries and disease with remedies and anesthetics. |
| **Tavern & Inn** | 50 Planks, 30 Cut Stone | 3 Innkeepers | Hospitality & Service | Serves ale and hot meals; hosts travelers to boost morale. |

### Tier 4: Metropolis & Thermal Automation

| Building | Construction Cost | Worker Capacity | Tech Prerequisite | Primary Function / Utility |
| --- | --- | --- | --- | --- |
| **Tier 4 Glasshouse** | 40 Stone, 30 Iron, 50 Glass, 20 Timber | 3 Master Farmers | Thermal Horticulture | Climate-proof greenhouse for year-round exotic crops. |
| **Geothermal Boiler House** | 60 Bricks, 40 Mortar, 20 Pipes, 10 Steel | 2 Boiler Engineers | Hydronic Heat Distribution | Pumps hot water/steam through underground pipe networks. |
| **Heated Aqueduct Network** | 3 Iron Pipes, 5 Mortar, 10 Bricks (per module) | 0 (Automated System) | Subterranean Engineering | Eliminates soil freezing and automates 100% field hydration. |
| **Imperial Great Library** | 100 Stone, 60 Glass, 40 Fine Damask | 6 Scholars | Master Archival Science | Grants global mastery research storage and +50% apprentice XP. |
| **Blast Furnace Foundry** | 80 Bricks, 50 Mortar, 30 Steel Ingots | 6 Blast Smelters | Industrial Metallurgy | Industrial-scale steel and alloy smelting hub. |
| **Stone Cathedral** | 120 Cut Stone, 40 Glass, 30 Mortar | 3 High Priests | Monumental Architecture | Provides town-wide morale immunity during severe disasters. |