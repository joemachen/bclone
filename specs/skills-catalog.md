# Spec: Skills — the catalogue, and the model underneath it

**Decisions:** D172 (scheduled this), D28 (make time-on-task personal — this discharges it),
D16 (numbers are derived, not picked), D2 (integer-only sim state), D3 (content is data),
D51/D62/D106 (the player says how many, the sim says who), D107 (`professions.md`'s role model),
D156 (an uneducated child works at twelve), D168 (a new kind of thing should be a data row).
Neighbours: **`tech-tree.md` (this is its missing substrate — §6)**, `professions.md §4` (the
roles a skill attaches to), `labour-allocation.md` (who gets the job), `clothing.md` and
`livestock.md` (parked, and both add skills when they land).
**Status:** 📝 **WRITTEN, NOT BUILT.** Nothing in this document exists in the sim. `Villager` has
`Vigour` and `VigourStage` and **no concept of skill at all**; there is no apprenticeship, no
proficiency and no teaching. This is `DESIGN.md §4` queue item 2, ahead of per-site yield and
Phase 3.

> **⚠️ This status line is load-bearing. Update it the day the slice merges** — D159 found five
> specs claiming "not started" for systems that had shipped, and `CLAUDE.md` now requires a
> spec's status to be checked against the suite rather than against itself.

---

## 1. Why this exists, and why it is written before anything is built

**`tech-tree.md` is written entirely on top of a proficiency model that does not exist.** Its
load-bearing rule is §3a:

> **A record preserves the method, not the proficiency.** When the last master smith dies with
> the technique written down, the village does not lose steel. But the next person to open that
> record starts near-zero and needs years at the forge to reach where she was.

**That rule is what stops the tech tree becoming a ratchet**, which is what stops it re-creating
the dead late game §2.3 exists to fix. It is the single most load-bearing sentence in that
document — and **it cannot be implemented against nothing**. "Starts near-zero" needs a zero to
start near; "where she was" needs a *there*.

So this is a catalogue *and* a model, in that order of importance. `buildings-plan.md` and
`food-catalog.md` established the habit: **write the list before writing the code**, because a
list you can read is a design you can argue with, and the tenth entry is the one that shows the
shape was wrong.

**⚠️ What this document is not.** It is not Phase 3. It decides what a skill *is*, what the
catalogue *contains*, and what the sim owes the tech tree. **The slice that builds it is Phase 3**
and it will need its own measurements — this spec deliberately leaves every number that wants a
running sim to §12, because inventing them here would be the false precision `tech-tree.md §12`
already refuses.

---

## 2. Which pillars, and which non-negotiables

- **§2.1 Villagers as agents** — the whole of it. *"A villager is an agent with a growing,
  transferable skill, not a headcount"*, and *"that skill dies with the person unless an elder
  apprentices a youth."* This spec is that pillar's data model.
- **§2.7 Knowledge-based tech tree** — §6 is the contract.
- **§1.1 Legibility above all.** A skill the player cannot read off the screen is an invisible
  multiplier, which is the same objection `crops-and-orchards.md §1` raised against the seasonal
  yield curve: *a number going up where the player cannot watch it*. §7 is not decoration.
- **§1.2 Meditative pace.** ⛔ **Skill must not add babysitting.** If the player ends up assigning
  apprenticeships every few years to avoid losing things, the design is wrong — see §5.3.
- **§1.4 Stories from people, not spreadsheets.** *"Old Mabel trained her granddaughter as
  herbalist before the fever took her"* is DESIGN.md's opening sentence about what this game is
  for. **Skill is the mechanic that sentence is made of.**
- **§1.6 Slow and traceable over clever and opaque.** §3.1 refuses experience points for the same
  reason §2.7 refuses research points.

---

## 3. ⭐ The model — what a skill is

### 3.1 Time on the task, counted in ticks. Not experience points.

**A villager's proficiency in a skill is the time they have spent doing it.** Not a score that
work adds to — the time itself, accumulated in sim ticks, converted to a proficiency by a stated
curve.

**Why the distinction matters rather than being pedantry.** An XP number is a thing the player
learns to farm: it invites "what gives the most XP?", which is a question this game should never
be able to answer. Time-on-task can only be answered one way — *she did the work* — and it is
the same argument §2.7 makes when it refuses a Civ-style research bar. It is also, as
`tech-tree.md §3b` says of tacit knowledge, **true**, which is the best kind of game rule.

**It is already how the game talks.** D120's `CommuteNote`, D147's `IdleNote` and D148's staffing
rows all say what somebody *did*; a villager panel reading *"nineteen years in the fields"* is the
same voice.

### 3.2 ⭐⭐ TODAY'S BEHAVIOUR IS THE NOVICE FLOOR — nobody is ever worse, and mastery is headroom above it

**This is the most important decision in the document. Joe's call, 2026-08-22 (D174):**

> *"Can today's behaviour be the novice floor? i.e. the founders are novices? And we introduce
> mastery — skill improvement numbers — along with a node of the tech tree?"*

**Yes, and it is a better answer than the draft it replaced.** This spec first proposed skill as
a *spread* around today's behaviour, with the reference at a working life's average. That kept the
economy still, but it bought the stillness by making **a village of novices poorer than today's**
— so the founding got harder and `cold-start.md` had to be re-derived. **The floor model deletes
that cost outright.**

**The rule: a novice behaves exactly as villagers behave today. Nobody is ever worse.**

- **A villager who has never done the work is today's villager**, to the tick and the unit.
- Proficiency only ever makes somebody **better than that**.

> ⚠️ **This says the *floor* does not move. It no longer says the *founding* does not move** —
> **the founders are a mix of tiers (§3.2c, Joe), so some of them start above the floor.** The
> byte-identical guard therefore belongs to a **synthetic all-novice village**, which is the
> thing that actually tests this rule, and the cold start is re-measured rather than asserted
> unchanged. See §10.

#### ⭐ Why this is not the "multiplier above one" this document warned against

That warning was aimed at a *silent* raise: skill quietly inflating output, the birth gate
opening sooner, and every derived number wrong with no event anybody could point at.

**A floor plus a gated unlock is a different object, and `DESIGN.md §2.2` already draws the
line it sits on:**

> *`VillageEconomy` goes on deriving the **survival floor** — what the village must produce not
> to die — and the player sets the **ceiling** above it. Derived floor, player ceiling.*

**The derivation is a floor, and the novice is the floor.** So it stays exactly as true as it is
today: it still answers *what must one pair of hands bring back so the village does not die*,
about the least skilled person in the valley — which is the honest worst case a survival number
should be solved against. **Mastery is headroom, and headroom above a floor is what progression
*is*.**

#### ⭐⭐ And the surplus has somewhere to go, which is what stops it inflating

A masterful village does not produce infinite food, because **stock limits already stop
production at what the player asked for** (D62, D128, D141). So mastery cashes out as **the same
output from fewer hands** — and the hands it frees become laborers (D63, D66).

**That is D161's mid-game answer arriving through the front door.** Joe's own framing of the gap
was *"stop treating those years as time to fill and start treating them as the years the founders
become worth learning from"* — and this is that sentence with a mechanism under it: the founders
get good, the village needs fewer of them on food, and the spare hands are what the player builds
with.

#### 3.2b ⚠️ Three things this creates that the spread model did not

**Named here rather than discovered in Phase 3.**

1. **✅ THE FOUNDING WOULD HAVE STAYED IN LOCKSTEP — AND JOE'S NEXT TWO ANSWERS FIX IT.** If every
   founder started at the floor they would start *identical*, and D28's lockstep is a symmetry
   problem rather than a variability one (§5's measurement: same tile 99.9% of ticks, identical
   hunger 100%). Skill alone would break it only over decades, leaving the opening — the thing he
   was watching at 4× — unchanged. **Both halves of the fix are now decided: a seeded personal
   rhythm at birth (§3.5) and a founding party of mixed tiers (§3.2c).** Together they mean **no
   two founders run the same program from tick 0**, which is what D28 actually asked for.
2. **⚠️ A WHOLE PHASE OF A SYSTEM WITH NO MECHANICAL EFFECT.** If mastery is gated behind a Phase
   4 node, then Phase 3 ships proficiency that **accrues, is visible, and changes nothing** —
   and **this project has shipped a decorative system before and only found out by measuring**
   (D56: clothing, a perfectly-clothed village being 300 years of identical numbers). It is
   survivable *only* because the legibility half is real from day one — the years on the panel,
   the mastery line in the log (§7), the at-risk warning — but **the anti-vacuity guard in §10
   must be written knowing that, or it will assert something that cannot be true until Phase 4.**
   The alternative is to let mastery bite in Phase 3 and have the node raise the ceiling
   *further*. **Joe's call — §12.**
3. **⚠️ IF MASTERY IS GATED BY A TECH NODE, THAT NODE CAN GO BACKWARDS — and the question is
   whether it should.** Spelled out, because the short version of it was unreadable:
   - **In `tech-tree.md §3`, every node is `Unknown`, `Known` or `Established`.** `Known` means
     *at least one living person knows it*, and **it goes back to `Unknown` when the last knower
     dies without having taught anyone.** That backwards step is called **re-locking**, and it is
     deliberate — it is what stops the tech tree being a ratchet where the village only ever
     gains. `Established` (written down, or held by enough people) is the state that cannot be
     lost to one funeral.
   - **So: if "the village can produce masters" is an ordinary `Known` node, then one badly-timed
     death could switch mastery off for the whole village** — including people who already spent
     twenty years earning it. **That is a cliff**, and it is exactly the *unfair because
     unforeseeable* failure §2.1 names.
   - **⚠️ AND THE QUESTION MAY REST ON MISREADING JOE.** *"We introduce mastery along with a node
     of the tech tree"* reads two ways: **(a)** a node *gates* mastery, or **(b)** the mastery
     numbers simply *ship in the same slice as* some node. **(b) has no re-lock problem at all**,
     and is the smaller change. **Joe's call, and §12 asks it as the plain question rather than
     this one.**

### 3.2c ⭐⭐ THE FOUNDERS ARRIVE AS A MIX OF TIERS — and it is the best idea in this document

**Joe, 2026-08-22 (D175):**

> *"Maybe the founders could be a mix of masters, mids — whatever that is — and novices? Could be
> a master woodcutter or gatherer or apprentice forester, know what I mean?"*

**Yes, and it does three things at once.**

1. **⭐ It makes the four founders *people* at tick 0.** Today they are four identical units with
   different names. *"Otto, master woodcutter. Agnes, apprentice forester. Hattie and Wendell,
   who have never done any of this"* is **§1.4 — stories from people, not spreadsheets — arriving
   in the first frame of a new game**, before the player has done anything at all.
2. **⭐ It gives the opening a shape to read and a decision to make.** A party with a master
   woodcutter and nobody who can farm is *a different opening* from the reverse — which is the
   same argument §2.5 makes for seeded maps: **a second playthrough is a different place, not the
   same place played again.**
3. **⭐⭐ It finishes the lockstep fix.** Founders at different tiers do different amounts of work
   in different numbers of ticks **from the first day**, so the symmetry D28 describes never
   forms. Combined with §3.5's seeded rhythm, **no two founders run the same program from tick
   0** — which is what D28 asked for and what four sessions of watching at 4× kept noticing.

#### The tiers, because the player reads words and not numbers

Joe's own vocabulary — *"masters, mids, novices"*, *"apprentice forester"* — is already tiered,
and that is the right instinct: **`master woodcutter` is a sentence and `proficiency 73` is a
spreadsheet** (§1.1). The historical ladder fits the game's register exactly:

| Tier | Roughly | Reads as |
|---|---|---|
| **Novice** | no time on the task | *"Wendell has never swung an axe."* |
| **Apprentice** | learning, under somebody if there is anybody | *"Agnes is learning the wood."* |
| **Journeyman** | competent, unremarkable — **Joe's "mid"** | *"Otto knows his trade."* |
| **Master** | twenty years (§3.3b) | *"There is nothing about this ground she does not know."* |

**"Journeyman" is the word for the middle**, and it is worth having because it is the one tier
with no obvious plain-English name — which is presumably why Joe wrote *"whatever that is"*.
**Names are Joe's call; the ladder's shape is the proposal.**

⚠️ **Tiers are a *reading* of proficiency, not a second stored thing.** One integer, four names
over it — anything else is two sources of truth for one fact, which is D148's bug and D76's seam.

#### ⛔ The honest cost, and it is a real one

**The founding is no longer today's founding.** §3.2 says the *floor* does not move; a party with
a master in it starts **above** the floor, so:

- **The cold start gets easier, not harder** — the opposite of the risk the spread model carried,
  but still a change. `cold-start.md`'s five measured ticks **will move**, and they are
  re-measured rather than asserted (§10, §11).
- **The goldens move.** Expected, once, taken last (D152).
- **⚠️ A master gatherer could make the opening trivial**, and nobody knows yet whether it does.
  **This wants a probe before it wants an implementation** (METHODOLOGY §3).

#### ⚠️ Fixed composition, seeded assignment — the recommendation

**The party's *shape* should be fixed and its *trades* seeded.** Always (say) one master, one
journeyman and two novices; **which trades they hold comes from the run's seed.**

**Why not a fully random roll:** a seed that hands you four novices and a seed that hands you two
masters are not two playthroughs, they are a good run and a bad one — and **whether you survive
should not be decided before you press play.** Fixed composition gives variety *in what you can
do* without variety *in whether you can live*, which is §0.1's *"the challenge is in the planning,
never in the punishment."*

**Joe's call**, and the numbers are §12's.

### 3.3 What skill changes: **time first, yield second** — which discharges D28

D28 has been an open Phase 1 debt since 2026-07-26, and it says this in its own words:

> *Let vigour, and later skill, scale **how long** a job takes as well as how much it yields.
> Most diegetic, does double duty for the skill pillar, and deepens D12: an old villager would
> not just bring back less, they would be out longer.*

**Today only yield is scaled**, in six places, all of the form `x * villager.Vigour / 100`.
Nothing anywhere scales duration, and that is precisely why **two adults of one household holding
one job are on the same tile 99.9% of ticks** (§5's measurement). They run the same deterministic
program on the same inputs for their whole lives.

**So skill scales the action's duration** — `gather_ticks`, `cut_ticks`, `sow_ticks`,
`reap_ticks`, `split_ticks`, `PlantTicks` — and that alone breaks the lockstep, because two
people who take different numbers of ticks to do the same thing stop arriving together within a
season and never re-synchronise.

**⚠️ Duration is not a free axis either, and the spec must say so.** `VillageEconomy` derives
trips-per-year from a fixed round trip; a faster master takes *more* trips. **That is exactly why
§3.2's reference rule is load-bearing** — the derivation moves to the reference villager and the
spread cancels across a village. The Phase 3 slice must assert that, not assume it.

**Yield may also be scaled, and it is the second lever rather than the first**, for a legibility
reason: a villager who is *out longer* is visible on the map, and one who *brings back less* is
only visible in a panel.

### 3.3b ⭐ Mastery is twenty years on the task, and the village says so out loud

**Joe's call, 2026-08-22 (D174): *"twenty years sounds good, and it should be noted in the event
log when someone achieves mastery."***

**Twenty years**, against a working life of about **fifty-five** — twelve (`adult_age`, D156) to
a lifespan of 55–79 (`lifespan_years_base: 67`, variance 12). **A bit over a third of a career**,
which means:

- **A founder who sticks to one trade masters it, and is a master for the back half of their
  life.** That is §2.1's own example — *"a farmer with 20 years in the fields is meaningfully
  better than a fresh laborer"* — meeting the sim's own lifespan numbers rather than a number
  picked to fit.
- **A generation is the unit.** A child born in year 1 works at twelve and masters at
  thirty-two — so **mastery and the first grandchildren arrive together**, which is the
  generational loop (§1.5) doing the pacing rather than a timer.
- **It is content, not derivation** — the class `farmhouse_seats` and `granary_feeds_people` are
  in (D165's split: *a stated fact about the world, with the consequence derived*). It goes in
  `data/`, per §4.1, and a modder can move it.

**⭐ AND IT IS A LIFE EVENT, NARRATED WHEN IT HAPPENS** — Joe asked for this by name, and it is
the sentence `DESIGN.md`'s opening paragraph promises the game will produce:

> *"Hattie has farmed these fields for twenty years. There is nothing about this ground she does
> not know."*

**One line, in the village log, on the edge** — the shape D123 settled and D147 restated: narrated
when it changes, never a standing banner. **It is the first thing in this design the player will
feel**, and it works from the day the substrate lands, whether or not mastery is doing anything
mechanical yet (§3.2b, point 2).

### 3.5 ⭐ A seeded personal rhythm, drawn once at birth — D28's stopgap, taken

**Joe, 2026-08-22 (D175): *"I would like a seeded personal rhythm at birth."*** Approved and in.

D28 listed three candidates for the lockstep and ranked this second: *"each villager gets a small
offset, drawn once at birth from the seeded stream, before they set off. Cheap, deterministic,
and true to life; people do not all get up at the same moment. Treats the symptom rather than the
cause."*

**It was called a stopgap because skill was expected to be the cure. Under §3.2's floor it is
not** — skill breaks symmetry over *decades*, so without this the opening stays synchronised.
**So the stopgap is not a stopgap any more; it is the half of the fix that works on day one**,
and §3.2c is the other half.

- **Drawn once, at birth, from the seeded stream, in a fixed order** (D15 — *an unordered tie is a
  desync waiting to happen*). Not re-rolled, not per-tick.
- **Small.** It is a person not getting up at the same instant, not a person who works a different
  amount. **If it changes how much anybody produces over a year, it is too big** — that would put
  a second, invisible hand on the economy §3.2 has just been so careful about.
- **It is on the villager, so it is inspectable**, and it costs nothing to say: two people leaving
  a house a few ticks apart is legible without a word of UI.

⚠️ **It moves the goldens**, because every history downstream differs from the first tick. Once,
last, one stated reason (D152) — and it should land in the *same* commit as the mixed founding, so
one golden move covers both rather than two.

### 3.4 Skill decays — slowly, and only off the task

A villager who leaves a trade loses ground in it. **Not to zero, and not fast.**

**Why it has to exist at all:** without decay, a fifty-year-old who did six jobs is a master of
six, and *"knowledge lives in people"* becomes *"old people are simply better"* — which flattens
back into vigour and deletes the reason apprenticeship is interesting. **Decay is what makes a
career a choice.**

**Why it must be gentle:** D46's reshuffle moves people between jobs every three years, and the
player moves them with the professions panel whenever they like. **A decay rate that punishes
either would make the labour allocator feel like a trap**, and the player would start fighting a
system that exists to save them work — §1.2, and D51's whole argument.

Rate is §12's, and it should be **derived against the reshuffle cadence** rather than picked.

---

## 4. The catalogue

### 4.1 ⭐ Skills are rows in a data file, not values in an enum

**This is D168's standing discipline applied at the first opportunity since it was written.**
Joe, 2026-08-22: *"modders should be able to add buildings, essentially add anything to the
game."* `BuildingKind`, `JobKind`, `Goods` and `Terrain` are four C# enums hashed by position and
pinned by every golden — **a modder can change their numbers and cannot add one** — and
`crops-and-orchards.md §4` is the one place this project got it right, with the crop id in data
rather than in the enum.

**A skill is a row: an id, a name, the work that grows it, and whether it can be written down.**
Nothing in the sim should switch on a skill by name.

**The cost of the other choice is known and quoted:** retrofitting an enum means touching the
state hash, every golden and every call site at once. Cheap now, expensive later — and skills are
the one kind of thing this design *guarantees* will grow, because every profession in
`professions.md §4` that is still ❌ brings one.

**⚠️ The id enters the state hash in a stated order**, like the crop id and for the same reason:
*same seed + same content ⇒ same history* is the contract a mod API has to respect (§4 of
`DESIGN.md`'s modding audit). **Hashed sparsely**, so a village where nobody has any proficiency
hashes exactly as it does today — which is the no-op contract D165, D112 and D87 have all used to
land a system without moving a golden.

### 4.2 The skills that exist on day one

**One per job that exists, and no more.** Every ❌ profession in `professions.md §4` brings its
own when it lands; inventing them now would be a catalogue of things nobody can hold.

| Skill | Grown by | What mastery looks like | Recordable? |
|---|---|---|---|
| **Foraging** | `JobKind.Forager` | Knows which ground is worth walking to and works a ring faster | ✅ |
| **Forestry** | `JobKind.Forester` | Fells and plants quicker; the wood recovers around them | ✅ |
| **Woodcutting** | `JobKind.Woodcutter` | More firewood from the same logs, and faster | ✅ |
| **Farming** | `JobKind.Farmer` | Sows and reaps quicker — **the visible one**, because a field is a place you watch | ✅ |
| **Building** | `JobKind.Builder` | Raises a frame in fewer ticks | ✅ |
| **Trading** | `JobKind.Marketer` | Picks better legs; less walking for the same delivery | ✅ |

**⛔ Laborers hold no skill, and that is deliberate.** D66 refused `JobKind.Laborer` on the
grounds that a laborer is *"the villagers no job currently wants"* rather than a trade — a
position in the priority order, not a profession (D87). **A skill in being spare is a
contradiction**, and it would quietly make the fallback a career.

**⚠️ Tacit skills exist in the model and none are in this table.** `tech-tree.md §3b` needs
`Recordable: false` to be a real column so apprenticeship is never obsoleted by the school — *a
midwife's hands, an eye for soil, knowing when the fish run*. **The first genuinely tacit skill
arrives with the physician or the herbalist**, and the column ships empty-but-honoured rather
than being retrofitted.

### 4.3 What a skill is *not* attached to

- **Not to a building.** A forester who moves to a different hut is the same forester. Skill is
  on the person; `professions.md §3`'s five elements are about the workplace.
- **Not to a household.** Inheritance is apprenticeship (§5) and nothing else — *knowledge lives
  in people* means it does not quietly flow down a family tree for free.
- **Not to a `JobKind` one-to-one, forever.** The table above happens to be 1:1 today. **The
  model must not assume it**, because a skill that two jobs grow (a smith and a farrier) is
  obviously coming, and because `JobKind` is an enum and a skill is a row.

---

## 5. Transfer — how skill outlives a person

### 5.1 Apprenticeship is the mechanism, and it is the pillar's whole point

**§2.1: *skill dies with the person unless an elder apprentices a youth.*** An experienced
villager working alongside an inexperienced one in the same trade **speeds the youth's growth**;
without it, the youth grows at the ordinary rate and the master's years die with them.

**⚠️ Working alongside, not a menu.** The strong version of §2.2 applies here: the player says
*how many* and the sim says *who* (D51, D62, D106). **If apprenticeship becomes a per-pair
assignment screen, this design has grown a slotting UI on the one axis the whole game refuses
it.** The lever is a *policy* — see §5.3.

### 5.2 What a record gives, and what it does not — the tech-tree contract

Restating `tech-tree.md §3a` in this document's terms, because this is the side that has to
implement it:

| | The method | The proficiency |
|---|---|---|
| **A living knower dies, node written** | ✅ kept — node stays `Established` | ⛔ **lost with them** |
| **A living knower dies, node unwritten** | ⛔ lost — node re-locks | ⛔ lost |
| **An apprentice was trained** | ✅ kept | ✅ **partly carried** — the apprentice has real years |

**That middle column is the entire anti-ratchet.** A library makes a catastrophe into a setback;
**only a person makes it into continuity.**

### 5.3 ⛔ The failure mode this must design against

§2.1 names it: *"punishing the player for losses they couldn't foresee. Knowledge-at-risk must be
**visible and actionable**."*

**Visible** is §7. **Actionable** is the harder half, and it constrains the design: if the only
remedy is *"assign an apprentice to Mabel before she dies"*, then the player must be watching
every elder in the village, forever — **which is babysitting, and §1.2 forbids it.**

**So the default must be safe and the lever must be coarse.** A village that is left alone should
apprentice *by itself* wherever an elder and a youth already work the same trade — the player's
control is a policy (*how much of the village's labour goes into teaching*), not a pairing. **The
player's job is to notice a trade with one old holder and no youths, and to put somebody there.**
That is a decision about staffing, which is a control that already exists.

> ⚠️ **AND THE ANTI-VACUITY GUARD IS THE ONE THAT DECIDES WHETHER ANY OF THIS IS REAL**
> (`tech-tree.md §13`, and D143's lesson): **a run with no apprenticeships must actually lose
> something.** If a village that never teaches ends up where a village that does ends up, the
> whole pillar is decoration — and this project has shipped a decorative system before and only
> found out by measuring (D56's clothing, a no-op over 300 years).

---

## 6. ⭐ The contract with `tech-tree.md`

What Phase 4 may assume exists, once Phase 3 lands. **Written as a contract because the tree is
already specced against it**, and a promise made in one document and read in another is exactly
where D159 found five specs lying.

1. **A per-villager, per-skill proficiency** that is an integer, hashed, deterministic, and
   readable from the tree's own code.
2. **A stated reference level** (§3.2), so *"starts near-zero"* and *"where she was"* both name
   something.
3. **A years-in-practice figure per villager per skill**, because four of the tree's eight unlock
   mechanisms need it: **PEOPLE** (*"long enough in the work"*), **SEREN** (*"available only to
   someone already deep in the practice"*), **ADJ** (*"two knowers"*) and **DOING** at the
   village scale.
4. **A `Recordable` flag on every skill** (§4.2), so the scriptorium can refuse tacit ones
   without the tree hard-coding a list.
5. **A "who still knows this, and how old are they" query**, which is what the at-risk warning
   (§7) and the tree's re-lock rule are both made of.

**⛔ What the tree must NOT assume:** that a skill maps to exactly one `JobKind` (§4.3), that
proficiency is bounded above by anything the tree knows, or that a record can restore
proficiency (§5.2).

---

## 7. Legibility — what the player actually sees

**A skill the player cannot read is an invisible multiplier**, and this project has rejected that
shape twice already (D37's spoilage, `environment-and-seasons.md §5.1`'s yield curve).

- **On the villager panel** — the sentence, not the number: *"Hattie · farmer · nineteen years in
  the fields."* The years are the diegetic fact; a percentage is the spreadsheet.
- **On the workplace panel** — who works here and how practised they are, in the same vocabulary
  D148 gave the professions rows (*"2 working of 3 seats"*), because that is the panel a player
  looks at when they want to know why a hut is slow.
- **⭐ The at-risk line, and it is the one §2.1 demands** — *"Mabel is 68 and the only soul who
  knows herbalism."* **Narrated on its edges, in the village log**, not shown permanently in the
  Overview: D42, D123 and D147 all settled that an always-on alert is one the player stops
  reading, and D147's rule is the model — `IdleNote` returns *the sentence or nothing*, so the
  marker and the panel cannot disagree.
- **⭐⭐ The mastery line, which Joe asked for by name** (§3.3b) — *"Hattie has farmed these fields
  for twenty years. There is nothing about this ground she does not know."* **One line, on the
  edge, when it happens.** It is the first thing in this whole design the player will *feel*, and
  it works from the day the substrate lands whether or not mastery is doing anything mechanical
  yet — which is most of what makes §3.2b's decorative-phase risk survivable.
- **In the life log** — apprenticeship is a life event. *"Mabel took Wren to the fields."* This
  is the sentence DESIGN.md's opening paragraph promises the game will produce.

---

## 8. Determinism and the state hash

- **Integer only** (D2). Proficiency is an integer; any curve is integer arithmetic. **No floats
  in sim-critical paths**, and the banned-API analyzer already enforces it at build time.
- **Hashed, in a stated order, sparsely** (§4.1). A village with no proficiency anywhere hashes
  as it does today — which is what lets the substrate land before the behaviour, the way
  `Terrain.Field/Sown/Ripe` did in `crops-and-orchards.md`.
- **Seeded, never random per-tick.** Apprenticeship pairing and any SEREN-style roll draw from
  the seeded stream in a fixed order (D15 — *an unordered tie is a desync waiting to happen*).
- **⚠️ The goldens will move**, and once: the moment duration varies by person, every history
  downstream differs. That is D163's shape and it is expected — **taken last, one commit, one
  stated reason** (D152).

---

## 9. What is deliberately not here

- **⛔ No skill for laborers** (§4.2).
- **⛔ No talent, aptitude or birth-luck.** A villager is what they have done. Rolling a "born
  gifted" stat would make the most important thing about a person something the player cannot
  see coming or act on — which is §2.3's failure mode (*pressure that isn't traceable to a
  decision*) wearing a character sheet.
- **⛔ No skill-gated job refusal.** The allocator stays cost-first (D15, D23, D120). **Skill may
  make somebody a better choice; it must never make them an ineligible one** — D120 deleted the
  last fence in this game and traded it for a consequence, and this must not quietly rebuild one.
- **⛔ No literacy, schooling or records.** Those are `tech-tree.md`'s and they need this first.
- **⛔ No numbers that want a running sim** — they are §12, per `tech-tree.md §12`'s own refusal
  of false precision.

---

## 10. Testing

Sim logic is pure and deterministic; exploit it (METHODOLOGY §3).

- **Determinism** — same seed, 200 years, identical proficiency state for every living villager.
  A regression here is P0.
- **The no-op contract** — with the substrate in and nothing growing, both fifty-year goldens are
  **byte-identical**. Landing it any other way means the substrate and the behaviour cannot be
  told apart when something breaks.
- **⭐ Growth is time-on-task and nothing else** — a villager moved off a trade stops gaining in
  it the same tick, and one who never holds it never gains.
- **⭐⭐ THE FLOOR IS EXACTLY TODAY (§3.2)** — a **synthetic all-novice village**, with the mixed
  founding switched off and the seeded rhythm switched off, produces **byte-identically** to
  today's village over fifty years. **This is the guard that decides whether the economy still
  stands**, and the floor model makes it a *hash* comparison rather than a tolerance.
  ⚠️ **It must be posed rather than played**, because the shipped founding is no longer all
  novices (§3.2c) — **a guard that tries to assert this about the real opening will fail, and the
  temptation will be to weaken it instead of to pose it properly.**
- **⭐ THE SHIPPED FOUNDING IS RE-MEASURED, NOT ASSERTED** (§3.2c) — `cold-start.md`'s five ticks
  come from a run and are written down, and the arm nobody has measured is **whether a master
  gatherer makes the opening trivial**. Probe before implementing (METHODOLOGY §3).
- **⭐ Fixed composition, seeded trades (§3.2c)** — every seed gets the same *shape* of party, and
  **no seed gets a party that cannot live.** Assert across the twelve-seed arm, which is the guard
  that has caught this class of thing before (D103's seed 11).
- **⭐ The rhythm is small (§3.5)** — a village with the personal rhythm on and one with it off
  produce **within a stated tolerance** over fifty years. *If the offset changes what anybody
  produces across a year, it is a second invisible hand on the economy rather than a stagger.*
- **⭐⭐ Anti-vacuity (§5.3)** — a run with no apprenticeships **loses** proficiency the village
  had, measurably, against a run with them. *If nothing is ever lost, the pillar is decorative.*
  ⚠️ **Write this knowing §3.2b point 2:** if mastery is gated behind a Phase 4 node, this guard
  **cannot pass in Phase 3** and must either be written against the *proficiency* that is lost
  rather than its effect, or land with the node. **A guard that cannot be true yet is worse than
  no guard**, because it will be "fixed" by weakening it.
- **Decay is gentle** — a villager moved by D46's three-year reshuffle and moved back has not
  lost a career.
- **⭐⭐ Lockstep (D28) — and it must be asserted about the OPENING, which is where Joe saw it.**
  Two adults of one household holding one job are on the same tile **99.9%** of ticks today, with
  identical hunger 100% of the time. **With §3.5's rhythm and §3.2c's mixed founding, that must
  fall in the first years and not merely across a century** — the whole point of taking both is
  that the fix works from tick 0. **The number to beat is on record**, which is what makes this
  falsifiable rather than a vibe, and **checking it red means running with both switched off.**
- **Legibility** — every apprenticeship, every mastery (§3.3b) and every at-risk transition emits
  **exactly one** narrative line naming the person. **Mastery is the one Joe asked for by name**,
  and it must fire on the edge rather than every tick the condition holds (D123, D147).
- **Shipped config, not only the fixture** (METHODOLOGY §3) — `ShippedConfigTests` runs the real
  file, and the drift between the two has produced D48, D49 and D50.

---

## 11. Definition of Done

1. This spec current, and its status line true.
2. **The proficiency substrate lands as a provable no-op** — goldens unmoved, determinism green.
   ⚠️ **The mixed founding (§3.2c) and the seeded rhythm (§3.5) are the opposite of a no-op and
   must land in their own commit, together**, so one golden move covers both.
3. Growth, decay and **the floor rule** guarded, each **checked red and counted** — the standing
   rule, and it has caught a vacuous guard four times.
4. **⭐ The floor proved against a posed all-novice village** (§3.2), and **the shipped founding
   re-measured from a run** (§3.2c) — including the arm nobody has measured yet: *does a master
   gatherer make the opening trivial?*
5. **⭐⭐ D28 DISCHARGED, AND ASSERTED ABOUT THE OPENING** — the 99.9% falls **in the first years**,
   not merely across a century, because §3.5 and §3.2c are both meant to work from tick 0.
   Checked red with both switched off.
6. **⭐ The mastery line fires** (§3.3b, Joe's ask) — once, on the edge, naming the person, and
   visible in the village log without the player going looking.
7. The at-risk warning reachable by the player, because *a feature the player cannot reach does
   not exist* (D103).
8. `DESIGN.md §6` and §7 updated; goldens re-taken last, one commit, one stated reason (D152).

---

## 12. Open — Joe's calls, and the things that want a running sim

**✅ Answered by Joe, 2026-08-22 (D174, D175):**

- **✅ Where the baseline sits — today's behaviour is the NOVICE FLOOR** (§3.2), not a mid-career
  reference. Nobody is ever worse than today, and mastery is headroom above. **This deleted the
  draft's biggest stated risk**: nothing in the economy gets poorer.
- **✅ Mastery is twenty years on the task** (§3.3b), against a working life of about fifty-five.
  Content, not derivation, and it lives in `data/`.
- **✅ It is narrated when it happens** — one line in the village log, on the edge (§7).
- **✅ A seeded personal rhythm, drawn once at birth** (§3.5) — D28's second candidate, taken.
  **It stops being a stopgap and becomes half the fix**, because under the floor model skill
  alone breaks symmetry only over decades.
- **✅ The founders arrive as a mix of tiers** (§3.2c) — *"a master woodcutter or gatherer or
  apprentice forester"*. **The other half of the lockstep fix**, and the thing that makes the
  four founders people rather than units. ⚠️ **It also means the founding is no longer today's
  founding**, so `cold-start.md` is re-measured and the goldens move once.

**⭐ Still open:**

- **⛔ Does mastery bite in Phase 3, or wait for its tech node?** (§3.2b, point 2.) Gating it
  behind Phase 4 means Phase 3 ships proficiency that accrues, is visible and **changes
  nothing** — and D56's clothing is this project's record of shipping a system that measured as a
  no-op. The alternative is that skill bites immediately and the node raises the ceiling
  *further*. **The legibility half is real either way**, which is what makes waiting survivable
  rather than pointless.
- **⛔ Did *"along with a node of the tech tree"* mean the node GATES mastery, or that mastery
  simply SHIPS ALONGSIDE a node?** (§3.2b, point 3.) **If it gates**, that node can *re-lock* —
  `tech-tree.md`'s rule that a technique known by only living people is lost when the last of
  them dies untaught — and a village could lose every master at once, including people who spent
  twenty years earning it. **If it merely ships alongside, there is no such problem** and the
  change is smaller. **The plain question: should anything be able to take mastery away from the
  village as a whole, or only from the person who dies?**
- **⭐ The founding party's composition** (§3.2c) — how many masters, journeymen and novices, and
  **fixed shape with seeded trades** (recommended) versus a fully seeded roll. *Whether you
  survive should not be decided before you press play* (§0.1).
- **⚠️ The tier names** (§3.2c) — *novice / apprentice / journeyman / master* is the proposal;
  "journeyman" is the word for Joe's *"mid"*, which is the one tier with no plain-English name.
- **Does skill scale yield as well as duration** (§3.3), or duration alone in the first slice?
- **Should the apprenticeship policy be a village-wide slider or per-workplace?** §5.3 argues
  coarse; the professions panel is where a village-wide one would live.

**Tuning, which wants a running sim and must not be guessed:**

- **The growth curve's shape between the novice floor and mastery at twenty years — and how much
  better a master actually is.** §3.2 fixed the bottom end; this is the *width*, and it is the
  whole feel of the pillar. Narrow makes skill a footnote; wide makes a mature village so much
  richer than a young one that the founding reads as a punishment for being new.
- The decay rate, derived against `labour_reshuffle_years: 3`.
- How much an apprentice's growth is accelerated, and whether the master pays for it in output.
- **The founding party's exact composition, and the size of the personal rhythm** (§3.2c, §3.5).
  Both want a probe before an implementation, and the rhythm has a hard bound: **if it changes
  what anybody produces across a year, it is too big.** The composition's unmeasured arm is
  whether **a master gatherer makes the opening trivial.**
- Whether proficiency retained from a record is zero or a small floor
  (`tech-tree.md §12` asks the same question from the other side — **they must be answered
  together, or the two documents will disagree**).
