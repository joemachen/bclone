# Spec: Contentment — why a villager stays, and why a village grows

> Status: **📝 being re-specced on `slice/morale` (W2, D797, 2026-11-19); nothing built. ⏸️ Six calls Joe's (§10),
> each with a recommendation.** Specced as *morale* (D207, 2026-08-24); **the player's word is *contentment* since D797**
> — the file keeps its old name because the log and eighteen links point at it. Owner: Joe + Claude Code · Pillar:
> `DESIGN.md §2.1` and §0.1 · Format per `METHODOLOGY.md §2`.
>
> Neighbours: `specs/shelter-and-exposure.md` (the model this copies — **a need held per person, with a legible
> cause**), `specs/nomads.md` (**arrivals, the other half of departures** — built, D563–D584), `specs/house-tiers.md`,
> `specs/water-need.md`, `tech-tree.md §9.10` (the branch that exists to give the player levers), and W6, health
> (`systems-complete.md §5.1`, no spec yet), which **shares no cause with this one** (§4.1).

---

## 0. The word (D797)

Joe, 2026-11-19: *"i want to weigh the pros and cons between it being "morale," "health," or "happiness."* Put to him
with a fourth, *contentment*; he kept health a separate system (*"Yes keep both"*) and wrote *"contentment should
get…"*.

| Word | Why not, or why |
|---|---|
| *Health* | ⛔ W6's word, and the one thing this system must never do is make anyone sick (§3.3) |
| *Morale* | ⛔ for the player: troops, a workforce, and *low morale* reads as slower work — §3.3's first refusal |
| *Happiness* | the runner-up: cozy, Joe's own word (D395), and the shell's *"Not here yet"* row already reads *Happiness*; but every city-builder has a happiness meter to max, which is §5's slot machine |
| **Contentment** ✅ | names what it does — **whether a person is settled enough to stay and raise a family** — and has no meter to chase |

- **What the player reads:** a pill on the person's card beside *Fed · Warm · Housed* — **Content** when met,
  **Unsettled** when not (*Unsettled* says what may come next); the sentence that says why; *"… has left the valley"* in
  the village log; and *"makes people content"* where the content pass wrote *"raises morale"* (the tavern, the chapel).
- **The code follows the word** — `Villager.Contentment`, `contentment_*` keys — because nothing in the code was named
  yet, so it is free, and **a code word that differs from the player's is D188/D240's half-rename** (*gatherer* and
  *forager*, a year apart). ⚠️ Recommended and not objected to; Joe's to overturn. History keeps *morale*: the §7
  entries, `TECH-EXAMPLE.md` (his content pass, kept unedited) and the workplan's *W2 morale* are not reworded.

---

## 1. Goal

Give the village a reason to be **a place people want to live**, not merely one they can survive in — and make
*"why did Otto leave?"* a question with a readable answer.

**⭐ It arrives because the content pass demanded it.** `TECH-EXAMPLE.md` gave the chapel, the tavern and the cathedral
jobs that were all some version of *"raises morale"* against a game with **no model at all**. Rather than let four
buildings ship as flavour, Joe made it real (D207).

---

## 2. Which pillars, and which non-negotiables

- **§2.1 Villagers as agents** — a person with a history who can be *unsettled by something specific* is the pillar;
  a settlement-wide contentment bar is the spreadsheet it refuses.
- **§0.1 the niche** — *cozy but challenging.* This is the clearest place the two pull apart, and §0.1's resolution
  governs: **the challenge is in the planning, never in the punishment.**
- **Non-negotiable 1: Legibility.** ⛔ **This system is the single most likely thing in this design to become an
  invisible multiplier**, which this project has rejected twice already (D37's spoilage,
  `environment-and-seasons.md §5.1`'s yield curve).
- **Non-negotiable 4: Stories from people, not spreadsheets.**

---

## 3. ⭐ The model — Joe's three calls, 2026-08-24 (D207)

### 3.1 It is held **per villager**

Like hunger and like cold. **Not a village-wide number.**

Two adults of one household can differ, which matters twice over: it is §1.4's *stories from people* holding, and it
is **a third independent source of the variation D28 spent four weeks buying** — after personal time-on-task (D181)
and the seeded rhythm (D190).

⛔ **There is no village contentment figure anywhere in sim state.** The town hall may show a *distribution* (W13),
derived on read. **Two sources of truth for one fact is D148's bug and D76's seam**, and an average would be exactly
that.

### 3.2 It does **two things, and only two**

| Effect | Why it is safe |
|---|---|
| **People leave the village** | Visible, traceable, and it costs you hands and whatever proficiency they carried. **Pairs with the nomads** — people arrive and people go |
| **Households have fewer children** | Fits the generational core loop directly, and **the births gate already works this way** for food, so there is a precedent to follow rather than a mechanism to invent |

### 3.3 ⛔ And two things it explicitly does **NOT** do

**Both were offered and both were declined, and the reasons are worth keeping** so they are not "improved" back in
later:

| Refused | Why refusing it was right |
|---|---|
| **Work slows or stops** | This is **the invisible multiplier §1.1 forbids.** A village that is quietly 20% slower for a reason nobody can see is the yield curve this project already deleted once |
| **Sickness and shorter lives** | **This is the death spiral §0.1 refuses.** *"A mistake should cost a generation of progress and be visible on the map — never end the game, and never be unrecoverable before you understood it."* Misery that kills compounds; misery that makes people leave does not |

**⭐ The second refusal is the load-bearing one.** Leaving is a **release valve**: an unsettled village gets smaller,
and a smaller village is easier to feed, warm and please. **The system self-corrects instead of running away.** That
is what makes it addable to this game at all — and it is why health (W6) is a **separate system** (D797), never a
second effect of this one.

---

## 4. What moves it

**The shape is fixed here; the weights and thresholds are not** (§9 — measured first). Every cause:

- **is a named, readable cause.** No aggregate score arrives from nowhere.
- **is something the player can act on** — a home, a hearth, a winter kept warm, a tavern within reach, not walking
  two hours to work.
- ⚠️ **Nothing may contribute that the player cannot see or change.** A villager unsettled for reasons with no lever is
  `DESIGN.md §2.3`'s *pressure that isn't traceable to a decision*.

### 4.1 ⭐ One cause, one system (Joe, D797: *"Yes keep both"*)

**Every cause belongs to exactly one of contentment and health, never both.** If both read the same thing, one cause
has two effects through two systems, and *"why did Otto leave?"* and *"why is Otto sick?"* get the same answer — §5's
*second cold system*, arriving from diet. **Health takes the body; contentment takes a life.**

| Cause | System | Exists today? |
|---|---|---|
| **A varied diet** (food groups, an immunity a one-food diet erodes) | **Health (W6)** — ⛔ *not* contentment; `systems-complete.md` W2's row listed it and is annotated | food values (D522), no groups |
| Sickness, herbs, the infirmary | **Health (W6)** | no |
| **The home** — a roof at all, and its tier (cabin, cottage, manor) | Contentment | ✅ `HomePosition`, house tiers (D716–D724) |
| **A lit hearth** — the household's firewood through the winter | Contentment | ✅ `HearthSystem`, the household's firewood |
| **Winters spent cold** — a person who was cold through a winter, counted by winters | Contentment | ⚠️ `Villager.Cold` is a tick-level counter; a per-winter tally is new |
| **The walk to work** | Contentment | ✅ `Villager.CommuteNote` (D112) — the sentence already exists |
| **The jar run dry** | Contentment — call 6 | ✅ `Household.Water`, said since D780; harms nobody today |
| **A death in the family** | Contentment | ✅ `Alive`, `DiedAtTick`, `HouseholdId` — derived on read |
| **The dead with nowhere to lie** — people have died and there is no cemetery, once the cemetery can be built (Joe, D797: *"similar to a full cemetery"*) | Contentment — **built with W4**, the cemetery | no (W4) |
| **Every cemetery full** (D395) | Contentment — **built with W4** | no (W4) |
| **The tavern within reach**, ale (W3); the chapel (later) | Contentment — **its levers**, built with their buildings | no (W3) |

- **W2 ships the causes that exist today** (home, hearth, cold winters, the walk, the jar, a death in the family); the
  cemetery's two arrive with W4 and the tavern with W3, each in its own slice's spec, each a row in the data file.
- ⚠️ **W2 ships with no building that raises it**, only causes that lower it and are already in the player's hands
  (build homes and raise their tier, keep the woodpile, place work near homes). The tavern is W3, next — the review's
  reason for the order (`systems-complete.md §5.1`).
- **The cemetery's two causes are one rule with two cases**: *the village's dead have a place.* None at all once the
  cemetery is learnable, or every one full — the same sentence on the card either way (*"the Ashfords have buried
  their dead in the open"* / *"… there is no room left in the cemetery"*). Before the cemetery can be built, the dead
  cost nobody anything: a cause the player cannot yet answer is §4's forbidden kind.

---

## 5. ⛔ The failure modes this must design against

| Failure | Symptom | Guard |
|---|---|---|
| **The invisible multiplier** | The village is worse and nobody can say why | §3.3 — contentment never touches output. Only leaving and births |
| **The death spiral** | Unhappiness kills, which makes it worse, which kills more | §3.3 — nothing lethal. Leaving shrinks the problem |
| **The happiness slot machine** | Player builds one of everything to max a bar | Every effect is per-person and legible; **there is no bar to max** |
| **Babysitting** | It needs tending tick by tick | Non-negotiable 2. It moves on the scale of **seasons**, like proficiency and unlike hunger |
| **A second cold system** | Contentment and exposure become two models of the same misery | Cold is a **place you are standing** (D45/D53). Contentment is about **a life**, not a tile — so it counts *winters* cold, never ticks |
| **A second health system** (D797) | Diet makes people both sick and leave | §4.1 — one cause, one system |
| **Leaving without warning** | A family is gone and the player never saw it coming | §0.1 — *unsettled* is on the card for seasons first, and *"thinking of leaving"* before anyone goes (call 3) |

---

## 6. Legibility — what the player actually sees

Following D147's rule exactly, which is the model this project has settled on: **a note returns the sentence or
nothing**, so the marker and the panel cannot disagree.

- **On the person's card** — the **Content / Unsettled** pill in the needs row (D431's row: *Fed · Warm · Housed*,
  *"morale and water join it later"*), and the sentence, never the number. *"Otto has been cold three winters
  running, and the walk to the quarry eats his day."*
- **On the edge, in the village log** — when somebody **leaves**, and it names why. That is the event worth
  screenshotting, and it belongs in `tech-tree.md §10`'s milestone list.
- ⛔ **Not an always-on alert.** D42, D123 and D147 all settled that an always-on warning is one the player stops
  reading.
- **In the life log** — leaving is a life event, like apprenticeship.
- **The shell's *"Not here yet"* row** *(Happiness — not modelled)* is deleted when this ships (`Main.cs`, *"Delete a row
  when it ships"*). ⚠️ Found on the way: *Students — no school* (the school shipped, D769) and *Clothes — waiting on cloth
  and leather* (the coat shipped, D667) are already stale — fixed on this slice as their own commit.

⚠️ **`Villager.CommuteNote` is the precedent to copy, and the walk's cause reuses it.**

---

## 7. Where it plugs in — read against the code, 2026-11-19 (D797)

| Thing | Status today |
|---|---|
| **Births gate** | ✅ `HouseholdSystem.IsReadyForAChild`: a roof, room in the house, `birth_interval_years` (4) since the last child, the village's food and last year's harvest. **No draw** — a ready couple has a child. §3.2's second effect is a longer interval (call 4), not a chance |
| **`Villager` need fields** | ✅ `Hunger`, `Cold`, `FullFor`; the card's needs row reads them (`Main.Cards.cs`, D431) |
| **Departure** | ⛔ **Nothing in the game removes a living villager.** The dead stay in `Villagers` with `Alive` false and a `CauseOfDeath`. Leaving is the one genuinely new mechanism: a fate beside death (alive, gone), a walk to the map's edge — **the nomads' walk-in reversed** (D573) — and the household's house, goods and seats let go |
| **Arrivals / nomads** | ✅ Built, played and merged (D563–D584) — **the valve has its other end** (§10's old call 2, answered) |
| **The cemetery** | ⛔ W4 (D395). Its two causes ride with it (§4.1) |
| **The tavern, ale** | ⛔ W3, next. Contentment's first lever |
| **The town hall** | ✅ Slice 1 (the gift). The distribution view (§3.1) is W13, the hall's tabs |

---

## 8. Determinism and the state hash

- **Integer only** (D2), hashed in a stated order, **sparsely** — a village where nothing has moved hashes as it does
  today, which is what let proficiency land before its behaviour did.
- **Seeded, never random per-tick.** Any threshold or draw comes from the seeded stream in a fixed order (D15 — *an
  unordered tie is a desync waiting to happen*). ⭐ Recommended: **no draw at all** — the births gate has none, and a
  person who leaves because a threshold was crossed is easier to explain than one who rolled badly.
- ⚠️ **The goldens will move once**, when departures and the longer interval begin — taken last, one commit, one stated
  reason (D152). Until then the causes and the pill are read-only and no golden moves.

---

## 9. What is deliberately not here

- ⛔ **No village-wide contentment number** (§3.1).
- ⛔ **No output effect of any kind** (§3.3).
- ⛔ **No illness or mortality effect** (§3.3), and **no cause shared with health** (§4.1).
- ⛔ **No numbers.** Every weight and threshold wants a probe before an implementation (METHODOLOGY §3 — *probe a
  mechanic before building it*): how many villagers would be unsettled, and how many would leave, in Joe's saved
  villages and the shipped seeds, before a number is typed.

---

## 10. Open — for Joe (D797)

*The four calls written in 2026-08 are answered or moved:* ~~(2) do departures need arrivals first?~~ **answered —
the nomads are built (D563–D584)**; (1), (3) and (4) are restated below against today's code.

1. **Is it a numbered pillar, `DESIGN.md §2.8`?** It is a per-villager system with two effects and its own failure
   modes, and Phase 6's gate counts it. **Recommended: yes**, a short §2.8 *Contentment* in the pillars' shape (thesis,
   mechanic, failure mode). One line of yours; not mine to mint (D207).
2. **Who leaves — a person, or the family?** Contentment is held per person, but a family is what owns a house.
   **Recommended:** an adult with no partner leaves alone (a grown child at home, a widow); **a couple leaves only when
   both are unsettled, and takes their children with them**; a child never leaves alone. So nobody is orphaned or
   split by it, and a couple where one is content stays — which is the story (*"she stayed for him"*).
   *Alternative:* anyone unsettled leaves alone, partners included.
3. **How slow, and how warned?** **Recommended:** read once a season; a person is *Unsettled* while their causes add
   past a line; **after a full year unsettled the card says *"… is thinking of leaving"*** and the log says it once;
   **after a second year they go**, at the turn of a season. Two years from the first sign to the door — the
   number measured on the probe before it is typed, but the warning year is the shape. *Alternative:* no warning
   stage, the pill alone.
4. **"Fewer children" — how?** **Recommended:** an unsettled couple **waits twice the interval** (8 years in place of
   4), said on the household card (*"The Ashfords are not ready for another child — Otto is unsettled"*). No draw,
   and the gate's other terms untouched. *Alternative:* no child at all while either partner is unsettled.
5. **Where do they go?** **Recommended: off the map and out of the game** — they walk to the valley's edge (the way
   nomads come in) and are gone, their house left empty for the next family. ⛔ Not to one of trade's three neighbours:
   that is a second system (a neighbour's population), for no story a player sees.
6. **The jar run dry — contentment's or health's?** Today an empty jar harms nobody (D780, *"Say it, don't rush"*).
   **Recommended: contentment's** — it is a household's daily life, and health's water, if W6 ever wants one, is
   *clean* water, a different cause. *Alternative:* leave water out of both until W6.

---

## 11. Definition of Done

1. This spec current and reconciled with what was built.
2. Causes and their weights in **data files**, not code (`DESIGN.md §3`) — a row each, so the tavern and the cemetery
   add rows rather than code paths.
3. Unit tests passing; determinism test still green.
4. **Anti-vacuity:** a village that ignores contentment entirely must actually lose people. *If nothing is ever lost,
   the system is decorative* — the guard `tech-tree.md §13` and `skills-catalog.md §10` both use. ⚠️ And **its mirror**:
   a village kept warm, housed and near its work loses nobody — the release valve must not empty a well-run valley.
5. **Legibility:** every departure emits exactly one narrative line naming the person and the reason.
6. Manual QA against a written checklist; no new errors across a clean 200-year playthrough.
7. `DESIGN.md` Progress Tracker + Decisions Log updated.

---

## 12. For the art lane (D774)

- **No new building, good or tool** in W2.
- **Villager states:** *leaving* — a person (or a family, call 2) walking to the valley's edge with what they carry;
  the nomads' walk-in marker, reversed.
- **The card:** a *Content / Unsettled* pill in the needs row, in the row's existing style (`UiLook`'s need pill).
