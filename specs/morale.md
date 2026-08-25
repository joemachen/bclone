# Spec: Morale — why a villager stays, and why a village grows

> Status: **specced, not started.** Owner: Joe + Claude Code · Pillar: `DESIGN.md §2.1` and §0.1
> Format per `METHODOLOGY.md §2`. Written the day the decision was made (D207) so the constraints
> are recorded while the reasoning is fresh.
>
> Neighbours: `specs/shelter-and-exposure.md` (the model this copies — **a need held per person,
> with a legible cause**), `specs/housing-and-density.md`, `tech-tree.md §9.10` (the branch that
> exists to give the player levers), `DESIGN.md §5`'s nomads entry (**arrivals are the other half
> of departures**).

---

## 1. Goal

Give the village a reason to be **a place people want to live**, not merely one they can survive
in — and make *"why did Otto leave?"* a question with a readable answer.

**⭐ It arrives because the content pass demanded it.** `TECH-EXAMPLE.md` gave the chapel, the
tavern and the cathedral jobs that were all some version of *"raises morale"* against a game with
**no morale model at all**. Rather than let four buildings ship as flavour, Joe made it real
(D207).

---

## 2. Which pillars, and which non-negotiables

- **§2.1 Villagers as agents** — a person with a history who can be *unhappy about something
  specific* is the pillar; a settlement-wide contentment bar is the spreadsheet it refuses.
- **§0.1 the niche** — *cozy but challenging.* Morale is the clearest place the two pull apart, and
  §0.1's resolution governs: **the challenge is in the planning, never in the punishment.**
- **Non-negotiable 1: Legibility.** ⛔ **A morale system is the single most likely thing in this
  design to become an invisible multiplier**, which this project has rejected twice already (D37's
  spoilage, `environment-and-seasons.md §5.1`'s yield curve).
- **Non-negotiable 4: Stories from people, not spreadsheets.**

---

## 3. ⭐ The model — Joe's three calls, 2026-08-24 (D207)

Asked directly, because each answer changes what gets built:

### 3.1 It is held **per villager**

Like hunger and like cold. **Not a village-wide number.**

Two adults of one household can differ, which matters twice over: it is §1.4's *stories from
people* holding, and it is **a third independent source of the variation D28 spent four weeks
buying** — after personal time-on-task (D181) and the seeded rhythm (D190).

⛔ **There is no village morale figure anywhere in sim state.** The town hall may show a
*distribution* (§7), derived on read. **Two sources of truth for one fact is D148's bug and D76's
seam**, and an average would be exactly that.

### 3.2 It does **two things, and only two**

| Effect | Why it is safe |
|---|---|
| **People leave the village** | Visible, traceable, and it costs you hands and whatever proficiency they carried. **Pairs with the nomads sketch** — people arrive and people go |
| **Households have fewer children** | Fits the generational core loop directly, and **the births gate already works this way** for food, so there is a precedent to follow rather than a mechanism to invent |

### 3.3 ⛔ And two things it explicitly does **NOT** do

**Both were offered and both were declined, and the reasons are worth keeping** so they are not
"improved" back in later:

| Refused | Why refusing it was right |
|---|---|
| **Work slows or stops** | This is **the invisible multiplier §1.1 forbids.** A village that is quietly 20% slower for a reason nobody can see is the yield curve this project already deleted once |
| **Sickness and shorter lives** | **This is the death spiral §0.1 refuses.** *"A mistake should cost a generation of progress and be visible on the map — never end the game, and never be unrecoverable before you understood it."* Misery that kills compounds; misery that makes people leave does not |

**⭐ The second refusal is the load-bearing one.** Leaving is a **release valve**: an unhappy
village gets smaller, and a smaller village is easier to feed, warm and please. **The system
self-corrects instead of running away.** That is what makes morale addable to this game at all.

---

## 4. What moves it

**Deliberately unenumerated here** — the inputs are content and belong with the buildings that
supply them (`tech-tree.md §9.10`). What this spec fixes is the **shape**:

- **Every contribution is a named, readable cause.** No aggregate score arrives from nowhere.
- **Causes are things the player can act on** — a home, a hearth, food that is not the same thing
  every day, a tavern within reach, not being cold for a season, not walking two hours to work.
- ⚠️ **Nothing may contribute that the player cannot see or change.** A villager unhappy for
  reasons with no lever is `DESIGN.md §2.3`'s *pressure that isn't traceable to a decision*.

---

## 5. ⛔ The failure modes this must design against

| Failure | Symptom | Guard |
|---|---|---|
| **The invisible multiplier** | The village is worse and nobody can say why | §3.3 — morale never touches output. Only leaving and births |
| **The death spiral** | Unhappiness kills, which makes it worse, which kills more | §3.3 — nothing lethal. Leaving shrinks the problem |
| **The happiness slot machine** | Player builds one of everything to max a bar | Every effect is per-person and legible; **there is no bar to max** |
| **Babysitting** | Morale needs tending tick by tick | Non-negotiable 2. It should move on the scale of **seasons**, like proficiency and unlike hunger |
| **A second cold system** | Morale and exposure become two models of the same misery | Cold is a **place you are standing** (D45/D53). Morale is about **a life**, not a tile |

---

## 6. Legibility — what the player actually sees

Following D147's rule exactly, which is the model this project has settled on: **`IdleNote` returns
the sentence or nothing**, so the marker and the panel cannot disagree.

- **On the villager panel** — the sentence, never the number. *"Otto has been cold three winters
  running and there is nowhere in the village to sit."*
- **On the edge, in the village log** — when somebody **leaves**, and it names why. That is the
  event worth screenshotting, and it belongs in `tech-tree.md §10`'s milestone list.
- ⛔ **Not an always-on alert.** D42, D123 and D147 all settled that an always-on warning is one the
  player stops reading.
- **In the life log** — leaving is a life event, like apprenticeship.

⚠️ **`Villager.CommuteNote` is the precedent to copy, and possibly to reuse** — a walk that eats the
working day already says so on the villager. **Grep before writing** (the helper you need may
already exist).

---

## 7. Where it plugs in

| Thing | Status |
|---|---|
| Births gate | ✅ **Exists** — scales with food against a household target. §3.2's second effect follows it |
| `Villager` need fields | ✅ Hunger and cold are the shape to copy |
| Departure | ⛔ **Nothing in the game removes a living villager.** This is the one genuinely new mechanism |
| Arrivals / nomads | ⛔ Sketched in `DESIGN.md §5`, unbuilt. **Departures without arrivals is a one-way valve** — worth sequencing together |
| The town hall | ⛔ Phase 4. The distribution view (§3.1) lives there |

---

## 8. Determinism and the state hash

- **Integer only** (D2), hashed in a stated order, **sparsely** — a village where nothing has moved
  hashes as it does today, which is what let proficiency land before its behaviour did.
- **Seeded, never random per-tick.** Any threshold or draw comes from the seeded stream in a fixed
  order (D15 — *an unordered tie is a desync waiting to happen*).
- ⚠️ **The goldens will move once**, when departures begin — taken last, one commit, one stated
  reason (D152).

---

## 9. What is deliberately not here

- ⛔ **No village-wide morale number** (§3.1).
- ⛔ **No output effect of any kind** (§3.3).
- ⛔ **No illness or mortality effect** (§3.3).
- ⛔ **No list of what raises it** — that is content, and it belongs with the buildings (§4).
- ⛔ **No numbers.** Every rate, threshold and weight wants a probe before an implementation
  (METHODOLOGY §3 — *probe a mechanic before building it*).

---

## 10. Open — for Joe

1. **Does morale become a numbered pillar (`DESIGN.md §2.8`)?** It is currently a spec plus a §5
   entry. **Promoting it is one line and is not mine to do.**
2. **Do departures need arrivals first?** A village that can only lose people is a one-way valve.
   Sequencing morale behind — or with — the nomads entry may be the honest order.
3. **Can a villager leave who has nowhere to go?** The nomads sketch has arrivals *"looking for
   homes"*; the mirror question is whether leaving is emigration to a real elsewhere (§2.4) or
   simply an exit.
4. **What is the slowest acceptable clock?** §5 says seasons rather than ticks; the exact scale
   wants a run.

---

## 11. Definition of Done

1. This spec current and reconciled with what was built.
2. Node content and contributing causes in **data files**, not code (`DESIGN.md §3`).
3. Unit tests passing; determinism test still green.
4. **Anti-vacuity:** a village that ignores morale entirely must actually lose people. *If nothing
   is ever lost, the system is decorative* — the guard `tech-tree.md §13` and `skills-catalog.md
   §10` both use.
5. **Legibility:** every departure emits exactly one narrative line naming the person and the
   reason.
6. Manual QA against a written checklist; no new errors across a clean 200-year playthrough.
7. `DESIGN.md` Progress Tracker + Decisions Log updated.
