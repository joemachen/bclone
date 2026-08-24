# Inventory: what the village has, what the documents promise, and where they disagree

> Status: **an audit, current as of 2026-08-24.** Not a spec and not a plan — it invents nothing
> and decides nothing. Owner: Joe + Claude.
> Companion to `buildings-plan.md` (which governs *which buildings exist and why*),
> `skills-catalog.md` (*what a skill is*) and `tech-tree.md` (*how knowledge is held and lost*).

**Why this exists.** Joe, 2026-08-24, stopping a Phase 4 plan: *"I think we might be getting ahead
of ourselves with the tech tree. It isn't fully thought out by me. I need to spend time thinking
about all of the tech and buildings and skills first."*

Three documents already hold most of that thinking. **They were written weeks apart, nobody had
checked them against each other or against the code, and they disagree in seven places** — one of
which is D159's exact failure mode (two roadmaps that disagree; it cost six weeks last time).

**⭐ Finding 1 is already fixed** (the catalogue was missing four of the ten buildings that exist);
the rest are recorded rather than resolved, because they are design calls and not editing ones.
**Finding 7 is the one to read first if you read only one** — the tech tree's own worked example
already shipped, ungated.

**⛔ Every number in Part A was read off the code, not off a document's claim about the code.**
That rule exists because this project has now broken it four times, the fourth being a handoff's
own warning about it.

---

## Part A — what the village actually has today

| | Count | Members | Declared in |
|---|---|---|---|
| `BuildingKind` | **10** | Granary, Shed, Market, WoodcutterHut, Pile, Home, BuilderHut, GathererHut, ForesterHut, Farmhouse | `World/Construction.cs:12` |
| `JobKind` | **6** | Forager, Forester, Woodcutter, Marketer, Builder, Farmer | `World/Workplace.cs:21` |
| `Goods` | **6** | Food, Logs, Firewood, Stone, Tools, Iron | `World/StoreBuilding.cs:292` |
| `Terrain` | **9** | Grass, Water, Forest, Rock, IronDeposit, Sapling, Field, Sown, Ripe | `World/GeneratedMap.cs:20` |
| Skills | **6** | foraging, forestry, woodcutting, farming, building, trading | `Config/SimConfig.cs:1273` |
| Zones | **3** | residential, work ground, harvest | `World/ZoneMap.cs:43` |

⭐ **The zones already come in two shapes, and it matters for any new one** (D86): **residential is
global** — the village owns it, the player picks the neighbourhood and the sim picks the tile —
while **work ground is owned by a building**. `buildings-plan.md §8.1` proposes crop and pasture
zones as brushes and leaves the shape open; **D162 already answered it for the farm** — a painted
zone plus a small steading that is the workplace, because the labour allocator is built entirely
around workplaces with a catchment and a global zone has no workplace in it.

**Four of these are C# enums hashed by position** (`BuildingKind`, `JobKind`, `Goods`, `Terrain`)
— D168's standing note, and the reason *"enum value or data row?"* is the question to ask every
time a new kind of thing arrives. **Skills are the one thing that got it right** (`SkillRow`), and
crops are the other (`crops-and-orchards.md §4`).

### What is real but has nothing spending it

| Thing | Produced by | Consumed by |
|---|---|---|
| `Goods.Stone` | ✅ quarried from `Terrain.Rock` via the harvest brush | **nothing** |
| `Goods.Iron` | ✅ mined from `Terrain.IronDeposit` via the harvest brush | **nothing** |
| `Goods.Tools` | ⛔ only the founders' cart (`SimWorld.cs:5794`) | **nothing** |

The economy says so itself, honestly, at `World/VillageEconomy.cs:1421`:

> *"No floor, because nothing spends them yet — a survival floor is derived from consumption, and
> neither has any. Named rather than left to the default so that the day stone becomes what a
> building costs, this is the line that is obviously wrong instead of quietly right."*

**So the mining half of the material chain is built and the spending half is not.** That is a
larger asset than it looks: seams are generated, visible, finite, brush-harvestable and hashed.

### Skills, in full

All six are `Recordable: true`; **none sets `MasteryYears`**, so all inherit `mastery_years: 20`.

| Id | Name | Grown by | Years phrase |
|---|---|---|---|
| 1 | foraging | Forager | *as a forager* |
| 2 | forestry | Forester | *as a forester* |
| 3 | woodcutting | Woodcutter | *as a woodcutter* |
| 4 | farming | Farmer | *as a farmer* |
| 5 | building | Builder | *as a builder* |
| 6 | trading | Marketer | *as a marketer* |

⚠️ **One skill per job, exactly 1:1 today** — which `skills-catalog.md §4.3` explicitly warns the
model must not assume is permanent.

---

## Part B — what the three documents promise

### `buildings-plan.md §4` — the catalogue

**53 rows**: 46 buildings and zones across three tiers, plus 7 branch nodes at T3.

| Tier | Rows | What earns it |
|---|---|---|
| T0 — Founding | 11 | What the exiles arrive able to do |
| T1 — Settlement | 19 | Practice, or the village simply being a village |
| T2 — Town | 16 | Scale, accumulated knowledge, or both |
| T3 — Branches | 7 | *Broad, not tall* — nobody gets all of these in one lifetime |

**18 rows carry a "Knowledge ✓"** — a promise only the tech tree can cash.

**`§6` is the cut list — 12 entries, each with its reason.** The more useful half of a catalogue,
and the part most likely to be silently re-proposed in a year.

### `skills-catalog.md §4`

Six skills; **§4.1**'s rule that a skill is *a row, not an enum value*; **§4.3**'s rule that a
skill is not permanently attached to one job.

### `tech-tree.md`

- **§4 — eight unlock mechanisms**: PEOPLE, DOING, SCALE, SEREN, IMPORT, ADJ, CRISIS, TERRAIN.
  **This is the vocabulary content can be hung on**, and it is probably the most immediately useful
  thing in the three documents for a content pass.
- **§9 — eight branches**, sketched: Ground, Woods, Herd, Fire and materials, Keeping, Building and
  ground works, Bodies, Knowing.
- **⛔ No technique list, deliberately** (§12, and D196 — Joe: *"we don't have to come up with the
  full list… eventually they will all have a number of them"*).

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

### 2. ⛔⛔ Every building costs logs and only logs

`BuildingRecipe` is `(int Logs, int WorkTicks)` (`World/Construction.cs:171`) — **one material
slot, for the whole catalogue.**

But `buildings-plan.md §4.2` says the mason's yard *"gates every durable building"*, §4.3 puts
stone behind the civic tier, and the whole T1→T2 climb assumes materials other than timber. **That
tier structure cannot exist against a one-material recipe.** Widening it is not large, but it is
**structural rather than content**, and it touches every recipe, the hauling, the build queue and
the goldens at once. Worth knowing *before* designing tiers that depend on it.

### 3. The library is a building in two documents and cut in a third

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

### 5. ⛔ Two roadmaps that disagree — D159, again, in a different pair of files

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
`buildings-plan.md §10`'s ordering assumed it was still ahead. **The first node will have to be
something else, and nothing currently proposes what.**

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
