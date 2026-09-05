# Spec: Multi-material construction — a building can ask for more than one thing

> Status: **✅ BUILT (D213 → D215, 2026-08-25), in four commits: the machinery as a provable
> no-op, the store prices, the huts on Joe's call, then the stone taken back out of the cart.** Owner: Joe + Claude Code · Pillar: `DESIGN.md §3` (data-driven), `§2.3`
> (systemic pressure traceable to a player decision) · Format per `METHODOLOGY.md §2`.
>
> Neighbours: `content-inventory.md` **finding 2** (which scheduled this),
> `goods-catalog.md §4.0` (the carried-load fix that made stone reachable at all),
> `stock-limits-and-laborers.md` (the ceiling on how much stone the village takes),
> `buildings-plan.md §4.2–4.3` (the mason's yard and the civic tier this unblocks).

---

## 1. Goal

**Let a building cost more than one material**, because every tier of content the project has
written down assumes it and none of it could exist against one slot.

Joe, 2026-08-25: *"as a basic, stone should be used for construction in addition to logs."*

`BuildingRecipe` was `(int Logs, int WorkTicks)` — **one material slot, for the whole
catalogue** — while `TECH-EXAMPLE.md` prices all 45 of Joe's buildings in **two to four goods**,
from *"10 Wood, 10 Cut Stone"* on the first well to *"80 Stone, 50 Planks, 20 Iron"* on the town
hall. `content-inventory.md` finding 2 closed the question in D206's own words: **no longer a
question, only a schedule.**

---

## 2. The shape

```
MaterialCost   (Goods, int Amount)

BuildingRecipe
  Materials    IReadOnlyList<MaterialCost>   sorted by good id, zeros dropped
  WorkTicks    int
  Of(goods)    int                           0 for a good it does not want
  TotalMaterials
  Describe(catalogue) → "40 logs and 10 stone"
```

**A list of pairs rather than an array indexed by good**, and the reason is stated so it is not
"tidied" later: a building costs one to four materials against a catalogue of up to 62 goods, so
a per-good array is almost entirely zeros — and it would have to be sized from *the run's* goods
catalogue, which `BuildingRecipe.For(kind, config)` does not have and should not need.

⭐ **Sorted by good id and carrying no zeros**, so iteration is deterministic (D5's ordering rule,
one type over) and *a building priced at zero stone has exactly the recipe it had before any of
this existed* — which is what let the machinery ship as a no-op.

`ConstructionSite` counts deliveries **parallel to the recipe's own list**: a site can only ever
receive what its recipe asks for, so the recipe's ordering is the natural index.

⛔ **A good the site never asked for is refused rather than swallowed**, so the load stays in the
carrier's arms and walks back to a store — D96's conservation rule, which a second material makes
reachable for the first time.

---

## 3. ⭐⭐ Which buildings pay, and it was measured rather than chosen

**Everything the player marks pays. The two free buildings and the house do not.**

| Building | Logs | Stone | Why |
|---|---|---|---|
| Granary | 40 | **10** | The player marks it. A durable civic store — `buildings-plan.md` lists *"stone granary"* under the civic tier |
| Warehouse | 30 | **8** | Same |
| Market | 35 | **10** | Same |
| Gatherer's / woodcutter's / forester's hut, farmhouse | 25 | **3** | Joe, D214: *"a nominal amount"*. One seam tile is 12 stone, so clearing a single rock buys four huts |
| Home | 30 | **0** | ⛔⛔ The one building the **village** decides to raise (D42). A stone price here gates growth on a resource an unattended valley never gathers |
| Pile, builder's hut | 0 | 0 | Free, and must stay free (D96, D108) — the circle |
| **The founders' cart** | — | **nothing** | ⛔ Joe, D215: *"only food and tools"* — see §3.2 |

### 3.1 ⚠️⚠️ The measurement that was wrong, and why it is recorded rather than deleted

The first probe said pricing the huts took the founding from **24 alive to 7**, and that number
decided the original split. **It was wrong, and instructively so:** it ran *before*
`SimWorld.NextSiteToServe` existed, so what it measured was §4's **starved-head stall** — a site
blocked on stone freezing every site behind it — and not the price at all.

Re-measured on the fixed build, fifty years of the shipped opening:

| hut stone | seam painted | alive @50y | huts standing | sites unfinished |
|---|---|---|---|---|
| 0 | — | 24 | 2 gatherer, 2 woodcutter | 0 |
| **3** | no | **24** | 1 gatherer, 1 woodcutter | 2 |
| **3** | yes | 24 | 2 gatherer, 2 woodcutter | 0 |
| 5 | no | 24 | 1 gatherer, 1 woodcutter | 2 |

**Full population either way.** The cost of never painting a seam is that the village builds
*fewer huts* — a legible, recoverable pressure rather than a death.

⭐ **A number is only as good as the build it was taken on.** This is the same lesson D179 records
about the suite's own runtime: *the horizons everybody suspected were never the problem.*

### 3.2 ⛔⛔ The cold start bricked — and the first fix for it was wrong

On the **real** cold start (`founding_buildings: false`) the founders have no stone, and the two
huts they eat and heat out of cannot be paid for: **0 alive, 4 frozen, not one berry ever reaching
a store.** The §3.1 re-measurement missed it because it ran on a **warm start**, which already has
its huts and has **no cart at all** — *the fixture and the shipped opening are two different games*
(METHODOLOGY §3, arriving through a starting condition rather than a config key).

**The first answer was `cart_stone: 12` — founders arriving with a pile of stone — and Joe threw it
out:**

> *"There should already be stone on the map for the user to ask the laborers to harvest. I don't
> want stone in the cart. Only food and tools."*

⚠️⚠️ **He was right, and the reason it was ever proposed is the part worth keeping: a unit was
misread.** The probe measuring *"how far is the nearest reachable stone"* reported **120** on every
seed and it was read as **120 tiles** — so the seams looked unreachable. `TravelCostField.Cost`
returns **cost units** and `BaseTileCost` is **10**: the real answer is **twelve tiles**, on every
seed, and `MapGenerator` says so out loud — *"STONE NEAR, IRON FAR… a valley whose ore sits in the
far woods plays differently from one where it is on the doorstep"* — with `stone_seam_ring_tiles`
set to **14**.

**The founding was never short of stone. The test fixture simply never painted a seam.** ⭐ *A
number in the wrong unit bought a starting resource the game did not need* — the same shape as
§3.1, where a probe measured a stall and was read as measuring a price. **Two corrections in one
slice, both of them a measurement believed too quickly.**

✅ **So the opening paints a seam**, exactly as it already paints trees, and the cart carries food
and tools and nothing else.

**The safety property, stated as the test that holds it:** a granary the village cannot pay for
**waits**, the settlement carries on out of its pile, and the site says what it is short of.
`DESIGN.md §0.1` — *the challenge is in the planning, never in the punishment*, and a mistake must
never be unrecoverable before it was understood.

### 3.3 ⛔⛔ And painting the seam was not enough: nearest-first never walks to a rock

**Painting it changed almost nothing**, and the measurement is the finding:

| the opening paints | seams cleared in 5y | stone in store | firewood | alive |
|---|---|---|---|---|
| a seam, **no trees** | 4 of 4 | 48 | 0 | 0 *(no timber — they froze)* |
| a seam **and trees** | **1 of 4** | **0** | 0 | **0** |

`NearestHarvest` takes the **cheapest painted tile**, the village paints its trees on its doorstep,
and **the wood grows back** (D126) — so there is always something nearer than a fourteen-tile ring,
for ever. The huts that needed three stone stayed sites and everybody froze.

✅ **What a building is waiting on comes before what is merely nearest.** This is
`NextFootprintToClear`'s exception one good over, and **its comment had already written the
reason** — *"nearest-first never gets to it"*.

- ⚠️ **Only when nothing in store can serve it.** If a warehouse already holds the stone, a builder
  fetches it and this is not clearing work at all. The rule is *"the village cannot otherwise get
  this"*, not *"a site wants this"* — the latter would send laborers to the rock every time a
  granary was marked.
- **Still cheapest-first among its own kind**, so the village goes to the *closest* rock.

**Measured after:** a seam and trees both painted → **4 of 4 cleared, 42 stone, 119 firewood, and
the village alive.**

---

## 4. ⛔⛔ What it surfaced: D135's bug, arriving through a second material

**The most important thing in this slice, and it was not in the plan.**

D135 gave a builder somewhere to go when the head of the build queue was starved — *"the builder
shouldn't just sit at the building waiting"* — and the question it asked, `NextBuildableSite`,
only ever answers with a site that **already has every material** and is merely short of *work*.

**That was nearly always true while timber was the only material**, because the village makes logs:
a starved head was rare and short-lived. **Stone is made by nothing** until the player paints a
seam, so *"the head wants something the village has not got"* became the **normal** state of a
fresh village — and the head then blocked every site behind it for ever.

Measured before the fix:

- a played founding: **0 alive, 4 frozen, no house ever built**
- a century of the shipped village asked to build four things: **0 alive**, four sites queued,
  **116 logs in store**

**The houses were affordable the whole time and nobody could reach them.**

✅ **`SimWorld.NextSiteToServe()`** walks the same queue order and answers *the first site a
builder can actually advance* — everything delivered, or short of something the village actually
holds.

- ⚠️ **The queue is not reordered**, which is D102's line and it holds. The head gets first
  refusal whenever it can be advanced at all; only an otherwise-idle builder's time moves.
- ⭐ **Asked by `WorkTheSite` and `LoadMaterials` both**, so the site somebody walks to a store for
  and the site they carry the load back to cannot disagree — D157's rule that two orderings over
  one list is the shape of half the bugs in this project.

---

## 5. Slices

### ✅ Slice 1 — the machinery, as a provable no-op

`MaterialCost`, the new `BuildingRecipe`, per-good delivery on `ConstructionSite`, a builder that
fetches whatever the site is next short of, delivery of everything in their arms the site wants,
and refunds of every material on demolition and abandonment.

⭐ **Every stone key shipped at `0`, and every golden was byte-identical.** D82's shape, and the
reason it says *"the refactor moved nothing, and that is what made it safe to build on"* — the
balance change that followed could not hide inside it.

### ✅ Slice 2 — the prices, and the stall they exposed

The three stores priced, §4's queue fix, and the guards in `StoneCostsTests`. **No golden moved
for this either**, because no golden run marks a store.

### ✅ Slice 3 — the huts, on Joe's call (D214)

Huts and the farmhouse at **3** each. ⚠️ **`AFoundingThatPaintsNoSeamStillLives` is the guard this
slice turns on** — the claim is that a nominal price costs the village *buildings* and never its
life.

### ✅ Slice 4 — the stone comes off the map, not out of the cart (D215)

`cart_stone` deleted; **the opening paints a seam** (four tiles ≈ 48 stone, against the 6 the two
huts cost); and §3.3's priority rule, without which painting it achieved nothing.

**No golden moved for any of the four**, and the reason stays checkable: every golden village is a
warm start with its huts already standing, and none of them has a site waiting on a material no
store holds. *Silent about what they do not reach, loud about what they do* (D157, D162).

---

## 6. ⛔ Failure modes

| Failure | Guard |
|---|---|
| **A cost that cannot be paid ends a run** | `StoneCostsTests.AStoreWithNoStoneWaitsRatherThanKillingTheVillage` — the site waits, the village lives, the note says what is missing |
| **A blocked site starves the queue** | §4. `NextSiteToServe`, and the founding numbers above are the anti-vacuity |
| **The refusal is silent** | Every site writes what it is short of, by name, through `DescribeWhatIsMissing` — METHODOLOGY §4 |
| **A cost nobody can ever pay** | ⚠️ **Open.** Nothing validates at load that a priced good has a source. Stone does (the brush); a future *"20 planks"* would not |
| **Two vocabularies for one building** | The village log, the inspector and the map ring all read `Recipe.Describe` / `TotalMaterials` — D148 and D188's finding |
| **A material is destroyed at the door** | `Deliver` refuses a good the recipe never asked for and returns what it took; the remainder stays in the arms (D96) |

---

## 7. Determinism

- **Integer only** (D2), and materials are sorted by **good id** so two runs iterate identically.
- **Nothing new is hashed.** A site's delivered counts are derived from the recipe and the goods
  that were carried — both already hashed — and the goldens were byte-identical across slice 1,
  which is the evidence rather than the claim.

---

## 8. ⚠️ Still open

1. **One config key per (building, good) is a stopgop and is recorded as one.** 45 buildings × 4
   materials is not a flat key list. **`BuildingKind` becoming a row** is the real answer and is
   its own axis — `content-inventory.md` finding 1, `goods-catalog.md §9`.
2. **Nothing dresses stone.** `TECH-EXAMPLE.md` prices ~15 buildings in *Cut Stone* and notes the
   gap itself: *"the quarry extracts; nothing dresses."* Raw `Goods.Stone` is the material today.
3. **No load-time validation that a priced good is obtainable.** See §6.
4. ~~**Should the huts pay?**~~ ✅ **Answered by Joe, D214: yes, a nominal amount.** 3 each, and
   the stone comes off the map (D215) — see §3.1 and §3.2 for the two measurements that had to be
   re-taken, and §3.3 for the priority rule that made a painted seam actually get worked.
5. ⚠️ **§3.3 changes what laborers prioritise, and it is worth watching in play.** A village with a
   site waiting on an unobtainable good will send its spare hands past nearer work to get it. That
   is the intent, but it is a real change to where people walk.
