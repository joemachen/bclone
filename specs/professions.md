# Spec: Professions — the shape every job shares

**Decisions:** **D107** (this document), and the ones it settles. Neighbours: D14, D29, D30,
D43, D51, D62, D63, D64, D66, D84, D86, D87, D102–D106.
**Status:** ⚠️ **Alignment document, agreed with Joe. Nothing here is built except where the
table says so.**

---

## 1. Why this exists

Joe, 2026-08-07, listing nine roles: *"Before we proceed, I want to make sure we're aligned on
how the roles will work in general. This is not an exhaustive list."*

**It is not a feature request, it is a shape.** Five professions exist today and were each built
on their own terms; nine are coming and three of them are new kinds of thing (a building sited
by terrain, a producer with a local buffer, a processing chain fed by an animal). **Writing the
shared shape down once is what stops the tenth profession being a tenth special case** — and
this codebase has a standing record of what that costs: `StoreKind` ran to five instalments
before anybody named the seam (D76).

This document is the model. Each profession still gets its own spec when its slice comes up.

---

## 2. Which pillars this serves

- **§2.2 villagers as agents.** A profession is a livelihood somebody holds, not a slot. The
  player says *how many*; the sim still says *who* (D51, D62, D106 — all three halves of that
  rule already exist).
- **§1.1 legibility.** One shape means one set of sentences. *"Nobody works here"*, *"nothing to
  work with"*, *"the ground is still being cleared"* should read the same at every hut.
- **§1.6 traceable over clever.** Nine near-identical buildings beat nine bespoke ones.

---

## 3. ⭐ The shared shape

**Every profession is the same five things.** Where a role needs a sixth, that is the
interesting part of its slice and it gets argued there.

| # | The part | What it means | Machinery |
|---|---|---|---|
| 1 | **A `JobKind`** | The work itself. Append-only — the enum is hashed by position (`Workplace.cs:38`). | exists |
| 2 | **A building the player places** | Its hut. Gives the job a position, a catchment and a name — *"a building is a livelihood the player sited"* (D84). | exists (`BuildingKind`, `Mark`, `Complete`) |
| 3 | **Seats** | `Capacity`, staffed per-building (D104) or village-wide (D106). | exists |
| 4 | **A local store with a stated cap** | Output accumulates at the hut, then is carried to a village store. | ⚠️ **`Workplace.Store` exists and is dead — see §5** |
| 5 | **A destination for its output** | Food → granary, materials → shed. Asked by good, never by building (D76). | exists (`StoreForTheLoad`) |

Optional sixth parts, each already with a precedent:

- **Owned ground** — a painted area belonging to the building, priced in workers (D86, `ZoneMap`
  work ground). The forester has it; the gatherer and hunter probably want it.
- **A placement condition** — *must be beside water*, *must be in forest*. **No precedent in
  code:** `CanBuildAt` (`SimWorld.cs:1196`) has no adjacency concept and never branches on kind.
  `buildings-plan.md:130` wants terrain to bite this way (the mill is *"one building, sited by
  terrain"*), and `tech-tree.md:83` attaches a condition: **an unavailable branch must be stated
  at map selection, not discovered in year thirty.**
- **A mode toggle** — the forester's plant/harvest. New UI concept; sim side is a field.

### 3.0 ⭐ Staffing: one number per profession, two views of it (D109)

**Joe:** *"Global professions panel and per-building should be linked. Staffing changes made in
the global professions panel should also be made automatically in the related buildings…
So each building has a 'workers associated with this building' number and a 'global workers in
this profession' number."*

There is **one** number per profession. The panel and the building show it from two ends.

| The player does | What happens |
|---|---|
| Sets **2 builders** globally, one hut exists | both go to that hut |
| Sets **2 builders** globally, two huts exist | one each — round-robin, capped by each hut's `Capacity` |
| Removes the worker from hut 2 | it moves to hut 1 if hut 1 has room; **the global holds**. Only if no hut of that kind has room does the global drop |
| Adds a worker at a hut | the global **rises** by one |
| Fills a hut to its max | more of that profession needs another hut. `Capacity` is the per-hut max |

**⭐ And there is no "let the village decide" any more (D109, Joe).** Every workplace carries an
explicit number. A finished building arrives at **0** and does nothing until the player staffs
it. The founders arrive as laborers.

- **Joe's reason is debuggability, not fidelity:** *"I think manual for now will make debugging
  the core game easier."* A village that only moves when the player moves it has one source of
  truth for who is working; today there are two and they argue (D103 is that argument).
- **`LabourQuota`'s derived demand survives as advice, not as a decision.** It still computes
  what the village *would* want and the panel still shows it — *"the village suggests 3"* — which
  is §1.1 working: the game explains itself and the player decides. It simply no longer binds.
- **It makes D103 moot rather than solved.** Building was unreachable because it was funded from
  leftover hands; now it is funded because the player said so.
- **⚠️ It is explicitly provisional.** Joe: *"can we go with full manual now and re-evaluate how
  to integrate 'let the village decide' in a later phase?"* Auto-staffing is deferred, not
  refused. Nothing here should make it hard to put back — which is why the derived demand stays
  alive rather than being deleted.

**When a worker dies or ages out** (Joe): a laborer takes the empty seat if one is free.
**If none is,** the building's number *and* the profession's global number drop by one, and the
timeline says so — *"X, the woodcutter, died and no laborer was available to replace them."*
Silence there would be a profession quietly draining away, which is the untraceable outcome
§1.1 forbids.

**⚠️ The cost, named: the two guards that have caught the most now decay by design.** The
12-seed × 200-year arm and the 300-year acceptance run both play themselves. An unattended
village grows, never adds gatherers, and starves — correctly.

**✅ Joe has declined a scripted player for now** — *"I'll debug manually for now. We can script
a player later if we want."* So those runs are **re-pointed rather than replaced**: they stop
asserting *"still standing at 300 years"* and start asserting something true about a village
nobody is managing — that it decays gracefully, that nothing throws, that the log explains it.
**The long-horizon net is genuinely thinner until a player is scripted**, and that is accepted
rather than overlooked. D34, D79 and D89 were each caught by exactly these runs.

### 3.1 Laborer is not one of them

**A laborer is the absence of a job, and that is load-bearing** (D66). `Villager.IsLaborer` is a
reader — `CanWork && !HasJob` — deliberately not a `JobKind`, because a job kind names a place
and a laborer has none. Children who come of age become laborers because nothing has claimed
them yet, not because anything assigned them.

**So the panel reads laborers and does not set them** (D106), and — measured — the way to *make*
laborers is to cap the gatherers, because the quota's last act is *everyone still spare forages*.

What laborers do: **clear painted ground** (D87, built), **tidy goods left lying about** (D96,
built), and **carry materials from stores to construction sites** (D93 — ⛔ specced, built and
reverted three times, killing the village each time).

---

## 4. The roles

Joe's list, with what is true today. **Status is about the code, not the design.**

| Role | Building | Placement needs | Produces → goes to | Local store | Status |
|---|---|---|---|---|---|
| **Laborer** | none, by design | — | clears & hauls | — | ✅ clearing, tidying. ⛔ carry-to-site (D93) |
| **Builder** | builder's hut | — | raises buildings; later roads, bridges, fences | — | ✅ **hut built (D110)**. Free and instant; seats derived; a site is an errand, not a seat |
| **Forester** | forester's hut | owned ground | logs → shed | 50 logs | ⚠️ job exists (D96 rename); hut, ground and worker-pricing built and waiting (D86, C3c) |
| **Woodcutter** | woodcutter's hut | — | firewood → shed | 50 firewood | ✅ built. Local store new. |
| **Gatherer** | gatherer's hut | forest nearby | food → granary | 100 food | ⚠️ job exists as map-placed forage sites. **Blocked — §6.1** |
| **Fisherman** | fishing hut | **beside water** | food → granary | 100 food | ❌ new |
| **Hunter** | hunter's lodge | **in forest** | food → granary; leather → shed | 50 + 50 | ❌ new |
| **Tailor** | tailor's | — | clothing → shed | 50 clothing | ❌ new; `clothing.md` blocked on its input |
| **Market worker** | market | — | moves goods to homes | large | ✅ built (D14, D36) |

**Not exhaustive** (Joe). `buildings-plan.md §4` carries the fuller catalogue — herdsman,
quarry worker, miner, blacksmith, brewer, teacher, physician, cleric — and each lands on §3's
shape or argues why not.

### 4.1 New goods

`Goods` is append-only (`StoreBuilding.cs:176`). This model needs **`Leather`** and
**`Clothing`**, both materials, both accepted by the shed.

**Fish and meat are `Goods.Food`, not goods of their own** (Joe). Nothing yet distinguishes them
mechanically, `food-catalog.md §7` warns explicitly against building a recipe tree, and appending
`Goods.Fish` later is safe. **The cost of the other choice is what settled it:** every reader of
*how much food has the village got* — `TotalFood`, the birth gate, the quota, stock limits,
granary capacity — would have to ask a capability question instead of naming a good. That is
D76's seam, on the one axis where the whole economy is derived.

---

## 5. The local store, and what it changes

**`Workplace.Store` already exists (`Workplace.cs:155`), is uncapped, and is completely dead** —
nothing in the sim ever writes to it. Its own comment describes *"the buffer at the point of
production (D30)"*, which was never wired up: producers carry output straight to a
`StoreBuilding`. The panel has a branch for it (`Main.cs:713`) that can never be true.

Wiring it up is the first real slice of this model, and it means:

1. `Workplace.Store` becomes `{ get; init; }` so a capacity can be set, matching
   `StoreBuilding.Store`.
2. A producer deposits into its hut, and hauls to a village store when the buffer fills or the
   day ends.
3. **⚠️ The numbers are content, not derivation.** 50 / 50 / 100 / 100 / 50 / 50 are Joe's, and
   D16's derive-don't-type rule does not bite because nothing in `VillageEconomy` depends on a
   buffer's size — the same class as `cart_capacity`. **A hut's SEATS are a different matter and
   must be derived**: `woodcutter_hut_capacity` is the recorded case where yields were
   re-derived and capacities were not, and thirty-six people froze.
4. **It is already counted as supply.** `SimWorld.AllStores()` includes workplace stores, so
   `TotalFood`/`TotalLogs` will start seeing hut buffers the moment anything lands in one. That
   is correct — a hut is reachable — but it moves the goldens and is the thing to measure.

---

## 6. What is blocked, and by what

### 6.1 ⛔ The gatherer's hut waits for natural regrowth (Joe)

Two reasons, and the second is the harder one.

**It removes the anchor the whole economy is derived from.** `MaxHomeToWorkTiles` is
`forage_site_ring_tiles + 2 × site_jitter_tiles` = 7 (`VillageEconomy.cs:172`), and everything
hangs off it: `RoundTripTicks` → `TripsPerYear` → `RequiredGatherYield` (which *is*
`gather_yield: 46`), and `MaxHomeToVillageTiles` → the timber and firewood budgets → granary
capacity → the population ceiling. Player-placed huts have no ring radius. **This is DESIGN.md
§6's "7-tile bound — the biggest payoff on the board and the largest re-derivation."**

**And *no forest, no food* is a starvation trap while the valley cannot come back.** The harvest
brush can clear every tree; natural regrowth is not built. A player who clears their woods would
lose their food supply with no way to restore it — **the one genuinely uncozy state §0.1 rules
out**, and precisely what D88 promoted natural regrowth above planting to prevent.

**So: natural regrowth first, then the gatherer's hut**, and the re-derivation gets its own
session. Map-placed forage sites stay until then.

### 6.2 ~~⛔ The forester plants only once it is earned~~ — ✅ OVERTURNED (Joe, 2026-08-08)

> **⚠️ SUPERSEDED. Planting ships ungated** (`specs/forests-and-gathering.md`, D112). Asked
> directly what should become of §2.7's node if planting arrives early, Joe chose *planting
> ships, node gets new content.*
>
> **What moved it is that the gatherer's hut made planting load-bearing.** Once food comes from
> the trees in a hut's ring and the thickets are gone, **a felled ring is a village with no
> food** — so the recovery has to exist before the pressure does. That is §0.1's *recoverable by
> design*, and it is the same argument D88 made when it promoted natural regrowth above planting;
> with planting ungated, **planting is the recovery** and regrowth becomes optional rather than
> load-bearing.
>
> **The cost, taken on purpose:** §2.7 names *"log a forest for two generations → managed
> forestry"* as its headline unlock-by-doing example, and planting is what that node unlocked.
> §2.7 is otherwise unbuilt, so the tech tree currently has **no designed content left**. The
> node survives with different content — faster growth, larger rings, planting off your own
> ground.
>
> The original reasoning is kept below, because it is what the change had to answer.

### 6.2 ⛔ The forester plants only once it is earned (Joe)

The hut can fell and tend its ground from day one. **Planting stays gated behind the
managed-forestry node**, which is D43's loop and §2.7's headline example of unlock-by-doing:
*log a stand for two generations → managed forestry*. Removing any part of that loop breaks it —
you may only fell what you paint, over-clearing is a visible mistake, natural regrowth makes it
survivable, and planting makes it recoverable *on purpose*.

**The plant toggle therefore ships greyed, with the reason on it.** A control that is visible and
explains why it is unavailable is legible; one that appears from nowhere two generations later is
not.

### 6.3 ⛔ Tailor waits for leather

`clothing.md` is blocked twice over: its inputs need a production tier that does not exist, and
clothing's payoff was **measured as a no-op** — 0% of winter is spent working outdoors, so a
perfectly clothed village is 300 years of identical numbers. **The hunter changes that**, because
hunting is a non-livestock leather source (`food-catalog.md:53`) — so hunter before tailor, and
clothing's own spec needs rewriting around a reason rather than an unlock.

### 6.4 ~~⛔ Laborers carrying to sites is the thrice-reverted one~~ — ✅ CLOSED, AND THE ANSWER IS NO (Joe, D135)

D93. Three attempts, all dying at 0 alive / 4 frozen. The cause was measured and is not the
wiring: **it inserts a hop** into a chain that had sixty ticks of slack. There is more slack now
(~112 ticks) so it may well survive — but it is attempt four and wants the cold-start ticks
measured before and after.

**⭐ There will be no attempt four.** Joe settled it as a rule rather than as a tuning question:
*"let's make builders 100% responsible for transporting construction material to the
construction site. Laborers only bring materials to the storage building."*

So the division is now stated, and it is the one the code already had:

| leg | who | ends at |
|---|---|---|
| world → store | **laborer** — felling painted ground, tidying heaps, hauling | a store, always |
| store → site | **builder**, and nobody else | the construction site |

`WorkTheSite` has exactly one caller, gated on `JobKind.Builder`, and it is the only way into
`FetchingMaterials` or `Building`. That is what makes the rule enforceable by inspection rather
than by measurement, and it is why D93's fourth attempt is not worth taking: the thing it kept
trying to add is the thing Joe has now ruled out.

**A builder with nothing to fetch and nothing to build harvests instead of idling** — Joe's
*"if there are no materials available, the builder should harvest materials and take them to
storage"*. **Painted ground only, confirmed by Joe.** D87 is his own rule that the brush is the
only way a tree comes down, and a builder felling unpainted woodland to unblock itself would be
the one actor in the game allowed to reshape the valley uninstructed.

**And the queue governs materials, not labour (D135).** A builder who cannot advance the head of
the queue works the next site that *can* be advanced, rather than standing beside one waiting for
timber that does not exist. Fetching still serves the head, so the player's priority still decides
where scarce logs go — which is the half of D102 that must not be lost.

---

## 7. Build order

Each step leaves the suite green and is measured against the cold start's five ticks.

1. **The builder's hut** — Joe's call. Builders are the one job with no building at all, and
   D103's starvation is the live complaint. Its own plan is written.
   - ✅ **The hut and sites-as-errands are in (D110).** It is **free and instant** on cleared
     ground like the pile, its **seats are derived** (`VillageEconomy.BuilderHutCapacity` — the
     hands left once the village has fed and heated itself, eight on both configs), and
     `construction_site_capacity` is deleted rather than zeroed. **There is no fallback: no
     hut, nothing is raised**, and the village says so the first time something is marked with
     nobody to raise it — because the alternative is the silent stall D93 forbids.
   - **The queue is what decides which site the crew walks to**, so D102's *player before
     village* survives as marking order (D104/D105) and the reorder controls still move hands.
   - **⚠️ `Workplace.IsSite` is the seam this created**, and it wants watching as §5's local
     store lands: a workplace that nobody can be posted to is a new shape, and seven readers
     plus four test guards had to learn about it in one slice.
   - ⏳ Still to come in this slice: buildings finishing at **0 workers** with founders arriving
     as laborers, §3.0's linked staffing, then the unstaffed alert and `Demolish(Workplace)`.
2. **The local store**, proved on the forester and woodcutter — the two professions that already
   exist. Everything after lands on a pattern that works.
3. **The fisherman** — the cleanest new profession, and the one that proves terrain-conditioned
   placement without touching the food derivation.
4. **The forester's hut** — its ground and worker-pricing are already built (C3c).
5. **Natural regrowth**, then **the gatherer's hut** and the 7-tile re-derivation.
6. **The hunter**, then **the tailor**.

---

## 8. Failure modes to design against

- **A tenth special case.** The reason this document exists.
- **A profession whose demand is discontinuous.** `clothing.md:158`: a job that is wanted in
  bursts *"is not a livelihood anybody holds"* — the trap `LoggersWanted` fell into (D22). Every
  new role must answer *what does this person do on an ordinary Tuesday?*
- **An optional food source the economy comes to depend on.** `livestock.md:93`: *"An optional
  source the economy depends on is not optional; it is a mandatory source with a brush."* Fish
  and meat are slack on top of the food equation, never terms in it.
- **A terrain gate discovered late.** `tech-tree.md:83`. If a valley has no water, the player
  learns that when they choose it.
- **Silent stalls.** Every hut says why it is idle. `BehaviorSystem` already has the sentences;
  a new role that does not write one is not finished.
- **Store lookups multiplying.** `buildings-plan.md:246` asks how many stores a village can have
  before nearest-store lookups become the cost. Nine local buffers is the first real test.

---

## 9. How this is tested

Not a slice, so no guards of its own. What it commits future slices to:

1. **A new profession moves no golden until somebody places its building.** The D86 pattern —
   the transition's first step is provably a no-op, which is what makes the next ones safe.
2. **The cold-start five ticks** (builder funded / logs delivered / hut standing / staffed /
   first firewood, against winter at t360) are measured before and after every slice here.
3. **The eleven-seed arm** (`MapGenerationTests.EverySeedProducesAValleyAVillageSurvivesIn`)
   is the guard that catches a change which helps ten villages and kills one — D103's case.
4. **Each slice's own spec** carries its guards. This document carries none.

---

## 10. Definition of Done

Alignment, so: **this document current, and D107 in `DESIGN.md` §7.** The build order in §7 is
the commitment; each slice's Definition of Done is its own.
