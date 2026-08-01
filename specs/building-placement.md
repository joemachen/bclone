# Spec: Building placement — the first thing the player actually does

> Status: **✅ slices 1–3 built** (the singleton seam, place-a-building, the residential brush). The harvest brush and the planting brush remain — see §13. · Owner: Joe + Claude Code
> Format per `METHODOLOGY.md §2`. Implements **D38**; the payoff decisions D33, D35 and D36 have been waiting for.

---

## 1. Goal

Let the player choose **where buildings go, and how many of them there are.**

Everything in the game so far happens *to* the player. The village founds itself at a spot the generator picked, with one granary, one shed, one market and one hut at fixed offsets, and then it lives or dies on constants. **This is the system that turns the game from a simulation you watch into one you play**, and several decisions are already queued behind it:

- **D33** — granary capacity is the village's population ceiling, and today it is a config line. Placement turns *"how big can my village get"* into *"how much granary have you built"*.
- **D36** — the market shortens fetch trips by only 6%, because it stands two tiles from the granary. Where it goes is the whole point of it.
- **D35** — cemeteries are waiting for this.
- **D40** — so are bridges, which are the most interesting placement decision in the game and the one with a technology behind it.
- **D38** — most buildings are multi-instance; a few (a town hall) are singletons.

---

## 2. Which pillars / non-negotiables this serves

- **§1.1 Legibility, hardest.** The player is now making decisions, so every refusal (*why can't I build here?*) and every consequence (*why is that family starving?*) must be traceable to a choice they made. This is the first system where a death can honestly be the player's fault, which is what §2.3 has been building toward.
- **§1.2 Meditative pace.** Placement must not become a click-farm. Placing a building is a considered, occasional act — not a per-villager chore.
- **§2.2 Smart labour** — where you put a workplace decides who can reach it. Catchment stops being a config number and becomes a thing you draw on the map.
- **§2.6 Desire-path roads** — placement is what makes traffic patterns yours rather than the generator's.
- **§2.3 Systemic pressure** — *"every escalating problem should be back-traceable to something the player did."* Until now, nothing was.

---

## 3. The hard problem: derived guarantees become player choices

This is the part that will break quietly if it is not designed for.

`VillageEconomy` currently rests on a promise the *sim* keeps: **`MaxHomeToWorkTiles` — no home is ever built further than this from its work** (D18). `Household.ChooseSite` enforces it, so the food economy is derived against a bound that always holds.

**Hand placement to the player and that promise is theirs to break.** A house on the far side of the valley is a family the economy was never budgeted for, and they starve.

That is not automatically wrong — *"a village that dies should die because of something"* (D31), and a badly-placed house is a something. But it has to be **visible before it is fatal**, or it fails §1.1. Three ways, roughly in order of how diegetic they are:

| | How | Cost |
|---|---|---|
| **Warn and allow** *(recommended)* | The site preview shows the walk to the nearest work and the nearest store, and says plainly when it is beyond what the village can support. The player may build anyway. | The player can still doom a family — which is the point. Needs the warning to be unmissable rather than a number in a corner. |
| **Refuse** | Placement is rejected outside the bound. | Safe, and it makes the map feel arbitrary: *"why not there?"* has no good answer beyond a hidden constant. |
| **Allow silently** | Build anywhere; the sim sorts it out. | Fails §1.1 outright. The family starves and the player cannot trace why. |

**Recommendation: warn and allow.** It is the only option that lets the player learn the rule by seeing it, and it keeps the economy's derivation honest by making the bound *visible* rather than secret.

> **✅ Superseded for homes by §11.1 (Joe, 2026-07-28), and for the better.** Homes are not placed individually at all — the player paints a residential *zone* and the village builds inside it. So `MaxHomeToWorkTiles` stays a guarantee the sim keeps (`ChooseSite` simply picks the best spot **within the zone**), and the warning happens **once, when the zone is painted**, instead of on every house. The table above still governs the buildings the player does place directly.

---

## 4. The D38 seam, which must be fixed first

**The code assumes one of each store.** `SimWorld.Granary`, `.StorageShed` and `.Market` each return *the first* building of their kind, and there are **13 call sites** relying on it — the birth gate, the fuel quota, house-building timber, every producer's deposit, every fetch.

`VillageEconomy.PopulationCeiling` derives from *a* granary's capacity. With two granaries the village should support twice the people; today the second one would be ignored by the gate that decides whether anyone is born.

**None of this is hard, but all of it has to happen before a second granary can exist**, and each call site needs a decision rather than a rename:

- *"Is there food in the granary?"* → across **all** granaries.
- *"Where do I take this load?"* → the **nearest** granary that has room.
- *"How big can the village get?"* → derived from **total** granary capacity.
- *"The shed has no logs"* → the **nearest reachable** shed, and the refusal should name it.

**This is the slice that must land first**, and it is worth doing even if placement stalled afterwards: the singleton assumption is a bug the moment anything creates a second store.

---

## 5. What is placeable

| | Placeable | Notes |
|---|---|---|
| Granary, storage shed, market | **Yes**, many | The D33/D36 payoff |
| Woodcutter's hut | **Yes**, many | Its position is a real trade-off — near the shed for logs, near homes for the worker |
| Homes | **Not placed — zoned.** See §11.1, §12 | The player paints a residential area; the village builds inside it as needed |
| Forage sites, tree stands | **No** | They are terrain. You find them, you do not put them there |
| Bridges | Later (D40) | Needs the tech tree |
| Cemetery | Later (D35) | Needs this system first |
| Town hall | Later, **singleton** (D38) | The example of a build-once building |

---

## 6. Construction

A building should not appear the instant it is paid for — that would make placement a menu transaction, and D14's whole argument is that things happening in this village are **work somebody does**.

The shape that matches everything already built (D29's processing chain, D36's marketer):

1. The player marks a **site**. Nothing exists yet but a footprint and an intention.
2. Materials are **hauled to it** — the same trips the market already makes, from the shed.
3. Somebody **builds** it. That is labour, out of the same quota as everything else, and it competes.
4. It becomes a building.

This gets three things for free: a half-built granary is legible on the map, building competes with foraging exactly as timber does, and §2.6 gets a burst of traffic to a spot the player chose.

**Simplification available if that is too much for one slice:** materials deducted at placement, and a build *time*, with no hauling. Less good, and it re-creates the teleporting-goods problem D30 deleted. Recorded as the fallback rather than the plan.

---

## 7. Interaction (the Godot half)

- A **build menu** listing what can be built and its cost.
- A **ghost** following the cursor, showing the footprint, and coloured by validity.
- **Refusals named in words, not just a red square** — *"the ground is under water"*, *"there is no route to there from the village"*. Same standard as `JobReason`.
- ~~The preview should show **what this building would be near**: nearest work, nearest store, how many homes fall inside its catchment.~~ ✅ **Cut (Joe, 2026-07-28).** It is more screen furniture than judgement, and the map already shows what is near — the player can see the granary and the thicket without being told their distances. If it turns out to be missed after a few placements, it can come back knowing exactly what was wanted.
- ~~Pausing while placing is probably right.~~ ✅ **No pause (Joe, 2026-07-28).** The village carries on while you decide. Pausing would make placement a modal act — the world stopping and waiting on you — which is the opposite of the unhurried thing §1.2 asks for. **Nothing here is urgent enough to stop the clock for**, and that is worth saying out loud: it is a claim about what kind of decision building is.

---

## 8. Failure modes to design against

- **The click-farm.** If a growing village needs constant placement, this becomes the micro §1.2 forbids. Homes are the risk — a village of forty needs a lot of houses. See §11.1.
- **A doomed building the player could not have known about.** Every constraint must be visible *at placement time*, not discovered a winter later.
- **Silent economy breakage.** §3. The derived bounds stop being guarantees the moment the player can place; if the sim keeps assuming them, villages die for reasons the player cannot see.
- **The singleton seam.** §4. A second granary that is silently ignored is worse than no second granary.
- **Unreachable placement.** Water is impassable now (D40) — a building across the river is legal, looks fine, and is useless. It must be refused, or at minimum warned about, in the same words §7 uses.
- **Blocking the village in.** Enough buildings in a ring could wall off a household's only route to work. A placement that would strand somebody has to be caught.

---

## 9. Testing

- **The seam first**: a village with two granaries counts both — for the birth gate, for the population ceiling, and for where a forager deposits.
- **Every refusal names its reason** (the `JobReason` standard, applied to placement).
- **A building is never placed on water, on another building, or somewhere unreachable.**
- **Placing a second granary raises the population ceiling**, measurably, over a long run — the D33 payoff asserted rather than assumed.
- **A well-placed market beats a badly-placed one** on total household travel — D36's 6% should become a number the player can move.
- **No placement can strand a household** from its work.
- **Determinism**: the same placements in the same order produce the same village.
- **The village still holds a stable size for 300 years** with the generator's default layout, i.e. placement changes nothing until the player uses it.

---

## 10. Definition of Done

Standard DoD (`METHODOLOGY.md §3`), plus:

> **The player can build a second granary and watch the village grow past its old ceiling; every refusal says why in words; and a badly-placed building produces a consequence the player can trace back to the moment they placed it.**

---

## 11. Open questions (for Joe)

### 11.1 Does the player place homes? ✅ **Resolved (Joe, 2026-07-28): the player paints a ZONE, and the village builds inside it.**

The *Foundation* model, and it is better than either option I offered. **The player paints a residential area with a brush; villagers build individual homes inside it, positioned and oriented by the sim, and only when a home is actually needed.** No need, no houses. When the village needs more room than the painted area allows, **the player is prompted to paint another one.**

Joe extends the same brush to **which forest to cut** and **where to plant trees**. See §12 — that is a bigger idea than a control scheme and it deserves its own section.

**Why this is the right answer and not just a nicer one:**

- **It kills the click-farm without taking the decision away.** Paint once, get twenty houses over fifty years. That is §1.2 satisfied by design rather than by restraint.
- **It keeps `MaxHomeToWorkTiles` a real guarantee**, which is what §3 was worried about. `Household.ChooseSite` survives intact — it is simply *constrained to the zone* instead of the whole valley. The sim still picks the best spot it can; the player decides the neighbourhood.
- **It moves the warning to a far better moment.** §3 recommended warn-and-allow on each building, which meant repeating a warning the player would learn to click past. Now the check happens **once, when the zone is painted**: *"homes here would be 14 tiles from the nearest work; the village budgets 7."* One considered decision, warned once, rather than a nag per house.
- **The prompt is a legibility win.** *"The village needs somewhere to build"* is the game telling the player a decision is due, rather than expecting them to notice. That is §1.2 again — reduce babysitting, do not add it.
- **It is the same philosophy as §2.2.** The player sets conditions; people respond. Painting a residential zone is exactly the shape of "workplaces have a catchment and villagers take jobs by proximity" — you shape the field, you do not command the agents.

**What it changes in this spec:** §3's whole table is superseded for homes. The bound survives, the warning moves to the zone, and "the player can doom a family by clicking" becomes "the player can paint a bad neighbourhood and be told so at the time".

### 11.2 Does construction take labour and time? ✅ **Yes (Joe, 2026-07-28).**

§6's four steps stand: a site is marked, materials are hauled to it, somebody builds it, it becomes a building. It needs a **`JobKind.Builder`** and a construction-site state, and building competes for hands in the same quota as everything else — which is the trade-off that makes placement mean something rather than being a purchase.

**Consequence worth stating: a village can now be short of hands in a way it chooses.** Marking six buildings at once does not queue six purchases, it competes with foraging — and per §4a's standing policy, *a village short of hands feeds itself before it builds*. Construction should be the first thing to yield, alongside house-raising.

### 11.3 Can buildings be demolished or moved? ✅ **Demolish, yes.** Move — see note.

Demolition is how a player corrects a mistake, and it returns some of the materials. **A demolished store should be able to strand its contents** — that is a consequence worth having, and it is the same lesson D34 taught about a dead family's larder.

✅ **Demolish only. No moving (Joe, 2026-07-28).** A building comes down and a new one goes up; relocation is not a separate verb. That keeps a real system out of the game for the price of some materials, and it means a badly-sited granary costs the player something to correct — which is the right amount of consequence for a decision the game warned them about.

### 11.4 Free-form placement, or fenced? ✅ **Free-form, with warnings (Joe, 2026-07-28).**

Anywhere in the valley, with §7's refusals and warnings doing the work. Consistent with warn-and-allow, and it keeps the map honest rather than fencing the player into a blessed zone. **Hard refusals stay hard** — water, on top of another building, off the map — because those are not judgement calls, they are impossibilities.

### 11.5 What does the *first* playable version need?

If this is too big for one slice, the smallest version that is genuinely worth playing is: **§4's seam, plus placeable granaries and sheds, materials hauled and built by hand.** That alone delivers D33 — build another granary, grow past your ceiling — which is the clearest cause-and-effect the game has ever offered the player.

---

## 12. Zones — the brush, and what it turns out to be for

Joe's answer to §11.1 named three brushes: **residential**, **which forest to cut**, and **where to plant trees**. They are one mechanic, and together they are more than a placement convenience.

### 12.1 The pattern

> **The player paints intent over an area. The village acts on it when, and only when, it has a reason to.**

That is the same contract as everything else here: the player sets conditions, agents respond, and the response is legible because the conditions are visible on the map. A residential zone with no housing shortage produces nothing, and that is not the brush failing — it is the brush working.

### 12.2 The three, and why the forestry pair is the interesting one

| Zone | The village does | Because |
|---|---|---|
| **Residential** | Builds a home inside it when a couple needs one and the timber exists | §11.1 |
| **Harvest** | Fells trees **here** rather than at an abstract "tree stand" | Makes forestry spatial |
| **Plant** | Plants trees here when there are hands to spare | The payoff of managed forestry |

**The harvest brush replaces a placeholder nobody had noticed was one.** `JobKind.Logger` currently works at "the tree stand" — a workplace with an inexhaustible supply, standing in a forest the generator draws but which nothing consumes. With a harvest zone, a logger fells *the forest tiles you designated*, and the forest recedes. Which means:

- **Forest becomes a resource with a location and a quantity**, not a workplace with infinite yield. That is §2.3's *"resource radii exhausting and forcing expansion"* made real, and it is currently the pillar with the least behind it.
- **Deforestation becomes visible on the map** — the clearest possible example of §2.3's *"every escalating problem should be back-traceable to something the player did."* You can see the bald patch you made.

**And the planting brush is the answer to it**, which is what makes the pair a system rather than a chore. It is also already promised: DESIGN.md §2.7 names *"log a forest for two generations → managed forestry"* as its example of **unlock by doing**. Planting is what that node unlocks. **The tech tree gets its first concrete node from this**, and it arrives from the direction §2.7 says it should — out of how the town has lived, not from a menu.

### 12.3 What this costs, and the one that matters

**Terrain becomes mutable**, and D41 predicted exactly this:

> *"Cached with no invalidation protocol, deliberately, because `GeneratedMap` is immutable… When terrain becomes mutable — a felled stand, a paved road, a bridge — that stops being true and the cache needs a way to be dropped."*

Felling a forest tile does not change what is *walkable*, so travel costs survive — but only by luck, and planting or bridges will not be so kind. **The flow-field cache needs an invalidation path before the first mutable tile ships**, and it should be built when terrain first changes rather than the first time somebody notices a stale route.

Also:

- `GeneratedMap` currently exposes terrain read-only and is hashed wholesale. Mutable terrain must stay in the state hash and must not become a per-tick hashing cost.
- **Zones are player intent and therefore sim state** — saved, hashed, and part of the determinism contract.
- Tree growth needs a rate, which is a new kind of clock: slow enough that a cleared valley is a generational mistake, fast enough that planting is worth doing. That is a §1.5 question — generational time as the core loop — and it should be derived from a stated target, not picked (D16).

### 12.4 Sequencing note

**The residential brush is the small one and should come first**, because it is needed for placement to be playable at all and it changes no terrain. **The forestry pair is a separate slice** — it wants mutable terrain, cache invalidation, a growth rate, and it reaches into §2.3 and §2.7. Doing them together would be two hard things landing at once, which this project has now learned about twice.

### 12.5 Resolved (Joe, 2026-07-28)

1. **Unpainted forest is not cut.** The harvest brush is the only way to fell. Maximal player control, and it makes the brush meaningful rather than advisory.
2. **Slow natural regrowth, plus faster deliberate planting.**
3. **Planting is gated behind the managed-forestry unlock.**

### 12.6 The three answers interlock — and one of them is load-bearing

Read together they form a loop, and it is worth spelling out because **removing any one breaks it**:

> You may only fell what you paint. Felling is therefore always a decision. A valley cleared too eagerly is a mistake you can see. **Natural regrowth is what makes that mistake survivable** rather than terminal — and deliberate planting, once earned, is what makes it *recoverable on purpose* instead of by waiting.

Pure planting-only regrowth would make an early over-clearing unrecoverable before the player had any way to learn the lesson — and since planting is gated behind an unlock that takes generations, they would be locked out of the fix at exactly the moment they needed it. **Natural regrowth is what keeps the gate fair.** That interaction is the reason all three answers have to move together.

### 12.7 Two consequences that need handling, not just noting

**1. A founding village with nothing painted cannot survive.** If unpainted forest is never cut, then a fresh game fells no logs → makes no firewood → freezes in its first winter, and builds no homes ever. Options:

- **Found the village with a starter harvest zone and residential zone already painted** *(recommended)* — the exiles arrive having already decided where to cut and where to live, which is both plausible and a gentle tutorial: the player sees what a zone looks like before being asked to paint one.
- Prompt at tick zero and pause. Correct but a cold open, and it makes the first thing the game asks of a player a decision they have no basis for.

**2. Planting cannot ship until the tech tree exists.** It is gated behind managed forestry, and there is no tech tree yet (§2.7 is unbuilt). So the forestry work splits again:

- **Harvest brush** — buildable now. Mutable terrain, cache invalidation, natural regrowth.
- **Planting brush** — waits for §2.7, and becomes that pillar's first real node.

That is not a delay so much as the right order: the harvest brush is what *creates the problem* managed forestry solves, and a tech node that answers a pressure the player has actually felt is worth far more than one that arrives before the pressure does.

### 12.8 Numbers that must be derived, not picked (D16)

- **Natural regrowth rate.** Stated target rather than a number: *a valley cleared by a village of thirty should take about a generation to come back on its own.* That makes deforestation a **generational** mistake, which is §1.5's core loop, and it gives managed forestry something concrete to beat.
- **Felling yield per forest tile**, against the existing `cut_yield` — today a tree stand is inexhaustible, so this is a new quantity and the whole timber economy is derived against it.

---

## 13. Slices

Small and green before the next, as with the fuel chain and storage. Each is a thing you could stop after.

**1. ~~The singleton seam (§4).~~ ✅ Done 2026-07-28.** No player-facing change: the village founds itself and behaves exactly as it did, and a test asserts the plural helpers give the same answers as the old singular ones on a one-store village. What changed is that a *second* store would no longer be ignored.
   - `SimWorld.Granary`/`.StorageShed`/`.Market` were **deleted rather than kept alongside** the plural API, so the compiler enumerated all fifteen call sites and each got a decision rather than a rename. `AnyStoreOf(kind)` remains for naming and tests, deliberately awkward to call.
   - The decisions: *"is there food?"* → **all** granaries. *"Where do I deposit?"* → **nearest with room**, by travel cost, skipping unreachable ones (a granary across the river is not a long walk, it is no walk at all). *"Can we build a house?"* → drawn from **every** shed, a little from each, since a house is paid for by the whole village (D25). *"How big can the village get?"* → `CeilingForCapacity`, from **total** granary capacity, so a bigger granary unlocked through the tech tree raises it the same way a second ordinary one does (D39).
   - The woodcutter's refusal changed with it. *"The storage shed has no logs"* was unverifiable once more than one shed could exist — **which** shed? — so it now says no shed within reach of the hut has a batch.

**2. Placing a building. ✅ Built 2026-07-28, both halves** — the sim, then a build menu, a ghost under the cursor and `CanBuildAt`'s refusals shown as words.** Granary, shed, market, woodcutter's hut. Construction sites are `Workplace`s of kind `Builder`, so they inherit allocation, catchment and refusal reasons rather than growing a parallel system — and the job disappears when the building exists. Materials are hauled from the nearest shed that has logs; work cannot start until they arrive. Building is funded from spare hands and **yields first**, alongside cutting logs for houses.
   - `CanBuildAt` is **pure** — asks questions, changes nothing — so the view can call it under the cursor every frame and show the answer before anybody commits. Refusals are sentences (*"the ground there is under water"*, *"there is no route to there from the village"*); a site that is merely far is **allowed and warned about**, which is D43's position.
   - Demolition returns half the logs and **loses whatever was inside, out loud** — measured: *"the granary was pulled down — 20 logs recovered, and the 1465 goods inside it were lost."* An abandoned site gives its delivered logs back in full.
   - Measured end to end: a granary marked in year 12 was standing a year later. **This is D33 paying off** — the village can now be told to grow past its old ceiling.

**3. ~~The residential brush.~~ ✅ Done 2026-07-28.** Zones are sim state — hashed, deterministic, part of the seed contract, because a zone is a decision somebody made. `Household.ChooseSite` is unchanged except that it only looks at painted land: **the player picks the neighbourhood, the sim still picks the tile**, since it knows the walk to work and the walk to the store and a cursor does not.
   - The distance warning fires **once, when the area is painted** — one message per brush stroke, not one per house. That is the whole reason zoning beat per-house placement (§11.1).
   - **The village asks.** When a couple wants a home and there is nowhere to put one, it says so — once, by name — and the header carries the request until somebody moves out. The game says when a decision is due rather than expecting the player to notice a couple quietly not moving out (§1.2).
   - Erasing land says where the village may build *next*; houses already standing stay put. Pulling homes down because somebody adjusted a brush would be a cruel reading of an undo.
   - **Starter zone measured, not chosen.** At radius 3, ten of eleven seeds held and the eleventh died out — and the way it died is the interesting part: a village that cannot spread cannot form new households, so every home fills to `max_household_size`, births stop, and the settlement ages out. That is D34's failure arriving by a different road. At radius 4 a village nobody helps behaves exactly as it always has, and the brush becomes necessary when the player builds more granaries and grows past it — which is when they are paying attention.

**4. The harvest brush.** Mutable terrain, and therefore the flow-field cache invalidation D41 predicted. Forest becomes finite and located; felling recedes it; natural regrowth returns it on a derived, generational timescale. This is the slice that makes §2.3 real.

**5. The planting brush.** Waits for the tech tree (§2.7), whose first node it becomes.

Bridges (D40) land after 2, since they are a placeable building with a technology behind them — and they are also what makes `specs/pathfinding-and-water.md §12.3` worth fixing, because a river is only interesting once crossing it is a decision.
