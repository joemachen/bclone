# Spec: Building placement — the first thing the player actually does

> Status: **draft — open questions for Joe in §11** · Owner: Joe + Claude Code
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
| Homes | **Open** — see §11.1 | The big question |
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
- **Refusals named in words, not just a red square** — *"the ground is under water"*, *"the shed is 14 tiles away; the village budgets 7"*, *"you have 12 logs of the 30 this needs"*. Same standard as `JobReason`.
- The preview should show **what this building would be near**: nearest work, nearest store, how many homes fall inside its catchment. That is the information a placement decision is actually made on, and it is what turns *"where do I put the market?"* from a guess into a judgement.
- **Pausing while placing is fine and probably right.** §1.2 is about not being rushed.

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

### 11.1 Does the player place homes, or do villagers keep choosing?

**The big one**, because it decides whether this system is calm or busy.

- **Villagers keep choosing** *(recommended)*. `Household.ChooseSite` already does it well, and it keeps `MaxHomeToWorkTiles` a real guarantee, so the economy stays derived rather than hoped-for. The player shapes *where* homes go indirectly, by placing the work and the stores they cluster around — which is a more interesting decision than clicking twenty houses, and it is §2.2's philosophy applied to building: you set conditions, people respond.
- **Player places homes** — the Banished model, and it is what most players will expect. But it is the main click-farm risk (§8), and it hands away the bound §3 is about.
- **Both**: villagers choose by default, the player may override or reserve a site.

*Recommendation: villagers keep choosing, for now.* It is much less work, it preserves a guarantee the economy leans on, and it can be revisited once the rest of placement is real. **But it cuts against player expectation, so it is your call rather than mine.**

### 11.2 Does construction take labour and time, or is it instant?

*Recommendation: labour and time* (§6) — it matches D14 and D29, and it makes building compete with eating, which is the trade-off that makes placement mean something. Costs a builder job and a construction-site state.

### 11.3 Can buildings be demolished or moved?

*Recommendation: demolish yes, move no.* Demolition returns some materials and is how a player corrects a mistake; moving is a second system pretending to be a convenience. **A demolished granary should be able to strand its contents**, which is a consequence worth having.

### 11.4 Should placement be free-form, or snapped to what the village can reach?

Free-form anywhere in the valley invites buildings nobody can use. *Recommendation: free-form, with the reachability warning of §7* — consistent with "warn and allow", and it keeps the map honest rather than fencing the player into a blessed zone.

### 11.5 What does the *first* playable version need?

If this is too big for one slice, the smallest version that is genuinely worth playing is: **§4's seam, plus placeable granaries and sheds, materials hauled and built by hand.** That alone delivers D33 — build another granary, grow past your ceiling — which is the clearest cause-and-effect the game has ever offered the player.
