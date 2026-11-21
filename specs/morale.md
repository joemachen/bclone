# Spec: Contentment — why a villager stays, and why a village grows

> Status: **📝 specced on `slice/morale` (W2, D797, 2026-11-19); ✅ Joe's six calls all as recommended (D798, §10);
> ✅ step 1 measured (D799, §13.1), its five calls as recommended (D800, §14); ✅ step 2 built — the causes as rows and the
> season's read, saved, not hashed, nothing acting on it (D801); ✅ step 3 built — the card's *Content / Unsettled* and its
> sentence (D802); ✅ step 4 built — an unsettled couple waits twice the interval, contentment hashed, eight goldens re-taken
> (D804); ✅ step 5a built — warned after a year, gone after two, the card and the log (D806); ✅ step 6's guards and golden
> (D808); ✅ step 5b built — leavers walk out to the edge (D809), ✅ played: *"yep they walked away and faded"* (2026-11-21).**
> *(Read "▶️ 5b, the walk out" at D806–D808.)*
> *(Read "▶️ step 5, leaving" at D804–D805, "▶️ step 4, fewer children" at D802–D803, "▶️ step 3, the card" at D801, "▶️ step 1" at D798,
> "⏸️ six calls Joe's" at D797.)* Specced as *morale* (D207, 2026-08-24); **the player's word is *contentment* since D797**
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
| **The home** — a roof at all ~~, and its tier (cabin, cottage, manor)~~ *(the tier is not a cause, D800)* | Contentment | ✅ `HomePosition`, house tiers (D716–D724) |
| **A lit hearth** — the household's firewood through the winter | Contentment | ✅ `HearthSystem`, the household's firewood |
| **Winters spent cold** — a person who was cold through a winter, counted by winters | Contentment | ⚠️ `Villager.Cold` is a tick-level counter; a per-winter tally is new |
| **The walk to work** | Contentment | ✅ `Villager.CommuteNote` (D112) — the sentence already exists |
| **The jar run dry** | Contentment (call 6, D798) | ✅ `Household.Water`, said since D780; harms nobody today |
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
  running, and the walk to the quarry eats his day."* **✅ Built (D802):** *"Otto is unsettled — they have no roof of
  their own and the walk to work is a long one."* — the last season's read, so the walk is *to work*, not to a place
  the job may since have left.
- **On the edge, in the village log** — when somebody **leaves**, and it names why. That is the event worth
  screenshotting, and it belongs in `tech-tree.md §10`'s milestone list.
- ⛔ **Not an always-on alert.** D42, D123 and D147 all settled that an always-on warning is one the player stops
  reading.
- **In the life log** — leaving is a life event, like apprenticeship.
- **The shell's *"Not here yet"* row** *(Happiness — not modelled)* is deleted when this ships (`Main.cs`, *"Delete a row
  when it ships"*). ⚠️ Found on the way: *Students — no school* (the school shipped, D769) and *Clothes — waiting on cloth
  and leather* (the coat shipped, D667) are already stale — fixed on this slice as their own commit.
  **✅ All three gone:** *Happiness* at D802, *Students* and *Clothes* at D803.

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
  reason (D152). Until then the causes and the pill are read-only and no golden moves. **✅ Step 4 (D804):** contentment
  hashed sparsely with the longer wait; eight goldens moved, six by the hash alone and the posed Phase-5 village's two by
  the births as well — each proved by ablation. Step 5 moves them once more. **Step 5a (D806) moved none:** `LeftAtTick`
  hashed sparsely, and no golden village has a leaver by its last tick; step 6's posed golden reaches one.

---

## 9. What is deliberately not here

- ⛔ **No village-wide contentment number** (§3.1).
- ⛔ **No output effect of any kind** (§3.3).
- ⛔ **No illness or mortality effect** (§3.3), and **no cause shared with health** (§4.1).
- ⛔ **No numbers.** Every weight and threshold wants a probe before an implementation (METHODOLOGY §3 — *probe a
  mechanic before building it*): how many villagers would be unsettled, and how many would leave, in Joe's saved
  villages and the shipped seeds, before a number is typed.

---

## 10. Joe's calls (D797) — ✅ ALL SIX AS RECOMMENDED (D798, *"All as recommended"*)

*The four calls written in 2026-08 are answered or moved:* ~~(2) do departures need arrivals first?~~ **answered —
the nomads are built (D563–D584)**; (1), (3) and (4) are restated below against today's code.

1. ✅ **Is it a numbered pillar, `DESIGN.md §2.8`?** *Yes — written (D798).* It is a per-villager system with two effects and its own failure
   modes, and Phase 6's gate counts it. **Recommended: yes**, a short §2.8 *Contentment* in the pillars' shape (thesis,
   mechanic, failure mode). One line of yours; not mine to mint (D207).
2. ✅ **Who leaves — a person, or the family?** Contentment is held per person, but a family is what owns a house.
   **Recommended:** an adult with no partner leaves alone (a grown child at home, a widow); **a couple leaves only when
   both are unsettled, and takes their children with them**; a child never leaves alone. So nobody is orphaned or
   split by it, and a couple where one is content stays — which is the story (*"she stayed for him"*).
   *Alternative:* anyone unsettled leaves alone, partners included.
3. ✅ **How slow, and how warned?** **Recommended:** read once a season; a person is *Unsettled* while their causes add
   past a line; **after a full year unsettled the card says *"… is thinking of leaving"*** and the log says it once;
   **after a second year they go**, at the turn of a season. Two years from the first sign to the door — the
   number measured on the probe before it is typed, but the warning year is the shape. *Alternative:* no warning
   stage, the pill alone.
4. ✅ **"Fewer children" — how?** **Recommended:** an unsettled couple **waits twice the interval** (8 years in place of
   4), said on the household card (*"The Ashfords are not ready for another child — Otto is unsettled"*). No draw,
   and the gate's other terms untouched. *Alternative:* no child at all while either partner is unsettled.
   ✅ **Built (D804):** either partner unsettled is enough; `contentment_birth_interval_percent` 200. ⚠️ **Found in the
   building: the interval only ever counted from a child**, so a couple's *first* child does not wait on contentment —
   built as written (*"the gate's other terms untouched"*) and put to Joe. ✅ **Joe, 2026-11-20: *"No, go on to step 5"*** —
   the first child does not wait (D806).
5. ✅ **Where do they go?** **Recommended: off the map and out of the game** — they walk to the valley's edge (the way
   nomads come in) and are gone, their house left empty for the next family. ⛔ Not to one of trade's three neighbours:
   that is a second system (a neighbour's population), for no story a player sees.
6. ✅ **The jar run dry — contentment's or health's?** Today an empty jar harms nobody (D780, *"Say it, don't rush"*).
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

---

## 13. Build order (D798) — one step a commit, TDD, measured before typed

1. ✅ **Measure** (D799, §13.1; `tools/harness/ZzContentment.cs`, copied in to run and deleted before a commit, D420). Over 100 valleys × 50 years and Joe's saves: per adult a season, how often each W2 cause is met — no roof, a cabin, a fireless or cold winter, a commute note, a dry jar, a death in the family within N years. ⭐ The question it answers is the shape of the line: **where a well-run village loses nobody and a neglected one does** (§11 item 4, both ways). No sim code.
2. ✅ **The causes as rows, the read once a season** (D801) — `contentment_causes` in `data/sim.config.json`, a weight a row; `Villager.ContentmentCauses` (the season's read, a bit a cause), `SeasonsUnsettled`, `ColdDaysThisWinter` (the per-winter tally `Cold` cannot give) *(named `Villager.Contentment` and `WintersCold` here until D802 — the plan's names, not the code's)*; saved; ⛔ **not hashed until step 4**, when something first reads them — so the goldens move once (§8). Nothing acts on it yet (`NothingActsOnItYet`). ⚠️ *No roof* is read at the season's turn, not on any day of it as D799 counted. *(Read "hashed sparsely … whether the tally alone moves a golden is measured" until D801: nothing hashed, nothing moved.)*
3. ✅ **The card** (D802) — the *Content / Unsettled* pill in the needs row and the sentence (`ContentmentNoteFor`, the `WaterNoteFor` shape); the probe's line; *"Not here yet"* loses *Happiness*. Built: an adult's pill only; *Unsettled* in amber; the tooltip says what the last season's read found, the causes in the data file's order, grief naming who was lost; *"… is content here."* with nothing held.
4. ✅ **Fewer children** (D804) — an unsettled couple waits twice `birth_interval_years`; the household card's sentence. Goldens move — proved by ablation, re-pinned, stated. Built: `contentment_birth_interval_percent` 200, either partner; *"Not ready for another child — Otto is unsettled."* on the household's status; the eight fields hashed; eight goldens re-taken (six by the hash alone, Phase-5's two by the births as well).
5. **Leaving** — *"thinking of leaving"* after a year unsettled (card + one log line), gone after two at a season's turn: a fate beside death (alive, gone), the walk to the map's edge (the nomads' walk-in reversed), the house, goods and seats let go, one log line naming the person and the reason; ~~save format bumped~~ *(the format stays 13 — §13.2 item 8)*. Goldens move once more. **Two commits (§13.2):** ✅ **5a** the sim, the card and the log (D806); ✅ **5b** the walk out, view only, as D573's walk-in is (D809).
6. ✅ **The guards and the gate** (D808) — the anti-vacuity pair (a neglected village loses people; a kept one loses nobody), a posed golden that reaches a departure (Phase 6 gate item 2), the four checks, and Joe's play. Built: `AVillageThatIgnoresContentmentLosesPeople` (the unattended fixture, seeds 1–12, fifteen years — 4 adults gone, each with two causes held two years); `AKeptVillageLosesNobodyItKeeps` (the water answered, fifty years — nobody with a roof leaves); `TheGoldenThatSeesADeparture` (seed 9, Year 15, four gone in Year 13). ⛔ Not the no-house pose: it is a death test (all twelve froze out, nobody left). ⚠️ The fire-out cause needs a fire to keep (`firewood_per_winter_day` > 0) — Phase 0's village of one held it from a fixture with no fuel. ⏸️ Joe's play: §15.

*Owed beside them, its own commit:* the stale *Students* and *Clothes* rows in *"Not here yet"* (D797). ✅ **Deleted (D803).**

### 13.1 ✅ Step 1, measured (D799) — `ZzContentment.cs` + `contentment.py`

Seven of Joe's saves (Hollowmere Y69, Thornfield-ridge Y39, Ashbourne Y20 and four young villages; 120 adults; Willowdale's
format-8 save refused) run on ten years, and 100 unattended valleys (seeds 200–299, fifty years, 2,704 adults). Per adult a
season:

| Cause, as first read | His villages | Valleys | What it says |
|---|---|---|---|
| No roof, any day | 0.9 % | 10.2 % | rare where the player builds houses; the harness waits for them |
| The walk — `CommuteNote` said (from 8 tiles) | **64.4 %** | 1.2 % | ⛔ the note starts at the old forage ring (`MaxHomeToWorkTiles`); **most of his adults are past it.** 8–11 tiles 23 %, 12–15 23 %, 16–23 13 %, **24+ 4.3 %** |
| A cold winter — days at the get-warm line (`SeekShelterPercent`) | of adult-winters: 0 days 58 %, 1–2 13 %, 3–5 19 %, **6–10 8.5 %, 11+ 1.2 %** | 0 days 73 %, 6+ 1.7 % | `Cold > 0` at the day's turn (the first arm) was 66 % of winters — a chill, not a hardship |
| The fire out, a winter day | **0.0 %** | **0.0 %** | never, in 184,000 adult-seasons — a row worth keeping for the crisis it names, not a cause that moves anyone today |
| The jar dry, any day | 15.6 % | 10.6 % | ⛔ the water trip's ordinary rhythm (D780: harmless, refilled at the next rest) |
| A death in the household, last four seasons | 0.7 % | 1.0 % | rare |
| *(The cabin — recorded, not a cause)* | 44 % | 91 % | §4's lever rule: the cottage's gate is the family tree |

**The shape a streak of consecutive seasons gives, and why it is not the rule:** a cause that is true every season (the
walk, no roof) builds a streak; one that comes and goes (a cold winter, a death) is forgotten by spring, so *"cold three
winters running"* can never add up. A plain rolling year (every cause summed over four seasons) went the other way — the
standing causes counted four times — and was harsher at every line (21–71 % of his adults gone).

**⭐ The rule measured as recommended** — a cause is **held**: the roof and the walk (from **24 tiles**) while true; **a cold
winter (6+ days at the get-warm line) and a winter with the fire out held for the year after**; a death for four seasons; the
jar at **3+ dry days in a season**. *Unsettled* at **two causes held at once**; *thinking of leaving* after a year of it;
gone after two (D798).

| Line | His villages (120 adults, 10 years) | Valleys (2,704 adults, 50 years) |
|---|---|---|
| one cause held | ⛔ 25 leave (21 %), in 6 of 7 | ⛔ 495 (18 %), in 72 of 100 |
| **two causes held** ✅ | **4 leave (3.3 %), all in Hollowmere (51 adults, Y69–79)** | **4 (0.1 %), in 3 of 100** |
| three causes held | nobody | nobody |

⚠️ **What it means:** with only today's causes W2 is **quiet** — the release valve opens a few times a decade in a big,
spread village and almost never in a small one. That is the cozy reading of D207 (a valve, not a pressure); the system gets
its teeth from the causes W3 and W4 add (the tavern's absence, the dead with nowhere to lie) and the anti-vacuity guard
(§11 item 4) is a **posed** neglected village, not the unattended harness. ⏸️ These numbers, and the cabin, are Joe's (§14).

### 13.2 Step 5's rule, read against the code (2026-11-20, before a line of it)

1. **Two keys:** `contentment_thinking_of_leaving_seasons` **4** and `contentment_leaving_seasons` **8** (D798 call 3: warned
   after a year, gone after two). `leaving` 0 turns leaving off; otherwise `0 < thinking < leaving`, validated.
2. **At the season's read, after every adult has been read** (`ContentmentSystem`, last in the tick order): an adult whose
   `SeasonsUnsettled` has just reached `thinking` gets **one** `Warning` line — *"Otto Ashford is thinking of leaving the
   valley — {the causes}."* — once a spell, since a settled season puts the count back to 0. An adult at `leaving` or past
   it is **ready**.
3. **Who goes** (call 2), household by household in id order (D15): a ready adult **with a living partner who is not
   ready stays** (*"she stayed for him"*); a ready couple goes together; a ready adult with no living partner goes alone.
   **The household's children go with a couple that leaves, or with the last adult to leave the house** — while an adult
   stays, a lone leaver's children stay with them. A child never leaves alone. *(There are no parent links to read: a
   household is a couple, their children, and grown children not yet paired — so the couple are the parents.)*
4. **What leaving is — a fate beside death:** `Alive` false, `State` **`Left`** (a new state, last in the enum), **`LeftAtTick`**
   set, `DiedAtTick` null and `CauseOfDeath` None — so every system that asks *"alive?"* lets them go as it lets the dead
   go (the job and the school seat refilled, the house to the next couple or roofless family by `FindAnEmptyHome`, its
   larder with it, as after a death), and **grief reads only the dead**. What they carry goes with them; a cart counts as
   home, as at a death. Their last read's causes stay on them — the reason they went.
5. **The readers that meant *died* read *gone*** (`Villager.GoneAtTick`, `DiedAtTick ?? LeftAtTick`): `MostEverAlive`'s
   peak, `EmptiedAtTick` (the re-founding's clock), and the founders' tribute (*"… and has left the valley"* where it said
   *"is dead at 63"*). A departure that empties the valley raises the same unstopping moment a last death does.
6. **One line a party** (§11 item 5), `Life`, naming them and why — *"Otto Ashford has left the valley — they have no roof
   of their own and the walk to work is a long one. Spring, Year 12."*; a couple *"Otto and Mira Ashford have left the
   valley, taking the two children — …"*. The causes are the card's own phrases (one copy), every leaver's, in the data
   file's order, each said once.
7. **The card** (`ContentmentNoteFor`): from `thinking`, *"Otto is thinking of leaving — …"*; ready and held by a partner,
   *"Otto would leave, but Mira is staying — …"*.
8. **Saved and hashed:** `left_at` written only when set; `LeftAtTick` hashed sparsely (tag 20); the state is hashed already.
   ⛔ **The format stays 13**: a format-13 save has no leaver and reads as it is — the rule since D721 and D723 is that a
   format moves when an old save cannot be read, and moving it here would refuse all 439 of Joe's saves for nothing.
9. ✅ **5b, the walk out** (view only, D573's shape reversed; built D809, `VillageMap.WalkOut.cs`): the map watches for
   anybody who left in the frame's ticks (the walk-in's window), walks them from where they stood to the edge tile the one
   cost field reaches most cheaply — **one edge a household**, from its first member, each on their own route to it, a
   file gap apart — the chevron above, and they fade over the last second at the edge. Nothing in the sim waits on it;
   it holds neither the speed nor a moment, and a leaver walking out is not clicked.
10. ✅ **5a built as above (D806)**, with what the building found: **Phase 0's village of one left at four** — no roof and
    no firewood by its fixture, two causes with no lever — so `Phase0Fixtures.Plenty` turns leaving off beside its wear and
    nomads, and `VillageFixtures.Village` puts 8 back; **no village golden moved** (nobody in a golden village has left by
    its last tick), so step 6's posed golden is the one that shows a departure.

## 14. Joe's calls from step 1 (D799) — ✅ ALL FIVE AS RECOMMENDED (D800, *"all as recommended"*)

1. **The line — two causes at once, held two years.** ✅ *Recommended:* 4 of his 120 adults leave in ten years, all in
   Hollowmere; nobody in a young village. *Alternative:* one cause — a fifth of every village leaves, which is a pressure, not
   a valve.
2. **The walk counts from 24 tiles**, not the card's 8 — 4 % of his adults' seasons, against 64 %. ⚠️ The *card's* walk note
   is untouched (it says the food budget, D112). *Alternative:* 16 tiles (18 %).
3. **A cold winter is six days of going in to get warm**, remembered for the year after — 10 % of his adults' winters.
4. **The jar counts at three dry days in a season**, not one — one dry day is the trip's rhythm.
5. **The cabin is not a cause in W2** (§4.1 listed *the home and its tier*): a roof at all counts; living in a cabin is 44 %
   of his adults' seasons and the cottage waits on the family tree, which the player cannot hurry — §4's lever rule. A tier
   above the cabin may *raise* contentment later, with the tavern's rows (W3).

## 15. Joe's play — the checklist (step 6, D808)

In his own village from the main checkout (`.\run.bat`), at 4× or a skip of a few years. What he should see, in order:

1. A person's card: the NEEDS row's last pill reads **Content**, or **Unsettled** in amber; its tooltip says why, in
   words, never a number.
2. A household holding off a child: its card's status ends *"Not ready for another child — Otto is unsettled."*
3. A year unsettled: **one** amber line in the village log — *"… is thinking of leaving the valley — …"* — and the
   card's sentence says *thinking of leaving*.
4. Two years: one line *"… have left the valley …"* naming them and why; they walk out to the valley's edge, the chevron
   above, and fade there (5b); gone from the roster and the professions at once; their house stands empty for the next couple.
5. A couple where one is ready and one is not: the ready one's card says *"… would leave, but … is staying"*.
6. Answering a cause (a well, a house, firewood): the pill goes back to **Content** within a season or two.
7. The log reads the departure; **no `ERROR`** in the run's log (gate item 5).

⚠️ **W2 is quiet with today's causes** (§13.1: 4 of his 120 adults in ten years, all Hollowmere). A young village may
see nothing past item 1; an old, spread one with no well is where leaving shows.
