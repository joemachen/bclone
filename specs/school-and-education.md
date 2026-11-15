# Spec: The school — what four years of childhood buys

> Status: **specced (D209); 🔨 ON `slice/school` (D752, 2026-11-14, Joe: *"Start the school"*) — RE-READ AGAINST TODAY'S
> CODE (§11); EVERY CALL ANSWERED (D753 — what school gives, two universal benefits, §11.4; D754 — calls 1, 4 and 5 as
> recommended); §12 THE ORDER OF WORK; ✅ STEP 1 BUILT (D755, §12.1); ✅ STEP 2 BUILT AT SIZE NOUGHT (D756, §12.2); ✅ STEP 3 MEASURED (D757,
> §12.3); 📏 A TWO-YEAR TERM, 11–13, MEASURED (D758, §12.4); ✅ FULLER LOADS AND A GENTLER OLD AGE BUILT AT SIZE
> NOUGHT AND MEASURED (D761, §12.5); ✅ SHIPPED AT D762's MIX (D763, Joe: *"Yes, ship it"*) — TWO YEARS 11–13, LOADS
> 25 %, OLD AGE +20, B 80; ✅ WARM IN WINTER, BURNING FIREWOOD (D764, Joe: *"Warm, burns firewood"*, §12.6); ✅ STEP 4, THE
> VIEW (D765, §12.7); ✅ STEP 5, SAVES AND THE HASH (D766, §12.8) — NO GOLDEN MOVED. ⏳ JOE PLAYS IT.** Owner: Joe + Claude Code · Pillar: `DESIGN.md §2.1`, §2.7
> Format per `METHODOLOGY.md §2`.
>
> Neighbours: `skills-catalog.md` (**§6.6 is amended by this spec — see §5**), `tech-tree.md §7d`
> (the school's strategic role), `buildings-plan.md`, and **D156**, which reserved the dial this
> whole system pulls.

---

## 1. Goal

**A schooled child starts working later and is worth more when they do.**

That sentence is not new. **D156 wrote it in August**, when `adult_age` went from 15 to 12, and
recorded *why* it was worth recording rather than just editing:

> *"An uneducated child can work at twelve, which mirrors real life and is the honest state of a
> game with no schooling in it. **When education lands, the trade is that a schooled child starts
> LATER and is worth more when they do** — so this is the dial that pair of choices will pull
> against, not a number that stays put."*

**⭐ The design arrived months before the system.** Joe's 2026-08-24 model — *"a teacher profession
who works at a school building that children can attend from ages 12–16 if there are open slots,
after which the working adults at age 16 are more proficient villagers overall"* — **is that
reserved trade, cashed.**

---

## 2. Which pillars, and which non-negotiables

- **§2.1 Villagers as agents** — *"knowledge transfer is load-bearing."* Apprenticeship carries
  knowledge **between two people**; the library carries it **across a gap in people**; **the school
  carries it to many at once.**
- **Non-negotiable 5: Generational time is the core loop.** ⭐ **The school is that pillar expressed
  as one building.** You pay four years of a child's labour now and collect on it for the forty
  years after — *the payoff arrives after the player who started it has moved on.*
- **§0.1 cozy but challenging** — *the challenge is in the planning.* Schooling is a **decision with
  a real cost**, not a free upgrade.
- **Non-negotiable 1: Legibility.** Why a villager is better must be readable: *"Wren went to
  school."*

---

## 3. ⭐ The model (Joe, 2026-08-24 — D209)

| Piece | Rule |
|---|---|
| **Building** | A **school**. Multi-instance — ⭐ *"if there are no open slots, another school needs to be built to accommodate the demand."* |
| **Worker** | A **teacher**. A new profession, staffed like any other — the player sets *how many*, never *who* (D51) |
| **Who attends** | Children **aged 12 to 16** |
| **Capacity** | **Slots.** A school holds N pupils; demand above N wants another school |
| **Payoff** | At 16 they enter work **more proficient overall** |

### 3.1 ⭐⭐ The cost is already built, and it is the best thing about this design

**`adult_age` is 12 in the shipped config.** So a child aged 12–16 is *already a working adult
today* — which means:

> **⛔ Every year in school is a year of labour the village gives up.**

The village pays **four working years per pupil**, plus a teacher who is a working adult producing
nothing, *plus* a building. **Nothing about this is free**, and that is what makes it a decision
rather than an upgrade. `tech-tree.md §7d` already named both halves — *"teachers are your best
people, removed from production"* — and the pupil's half is the one D156 reserved.

⚠️ **Do NOT implement this by moving `adult_age` to 16.** That would make schooling universal and
mandatory, delete the choice, and force a re-derivation of the whole food economy against four
fewer working years for everybody. **The child stays an adult at 12; going to school is what they
do instead of working.**

### 3.2 What "more proficient" is allowed to mean

**Three candidates. This spec does not pick — it bounds.**

| Candidate | Standing |
|---|---|
| **(a) A starting proficiency floor** — a graduate begins their trade with some years already banked | ⭐ **The most likely reading of Joe's words**, and the one §5 amends the rules to permit. **Bounded: a floor, never a tier** — see §5 |
| **(b) Faster growth for life** — a graduate accrues proficiency at a better rate | ⚠️ **This is the soft ratchet D196 flagged at +5% and Joe just deleted at +50%.** If it ships at all it wants measuring first |
| **(c) Literacy** — a graduate can read records | ⛔ **Has no machinery**, because D204 deferred literacy when recording became automatic at mastery |

⛔ **(D753: superseded — Joe: *"i want to come up with a more innovative and novel solution for what school gives - it
should provide some kind of universal benefit(s)/buffs for all who attend"*, then *"A and B"*. School gives two
benefits to every pupil whatever trade they take — §11.4. Candidates (a)–(c) are kept below as the August reasoning.)**

**⭐ Recommendation: (a), and only (a), for the first landing.** It is the one that reads as a
sentence — *"Wren spent four years at school and knows her letters and her numbers"* — and the one
whose cost the player can see on the same screen as its benefit.

---

## 4. ⛔ The failure modes

| Failure | Symptom | Guard |
|---|---|---|
| **Schooling is always correct** | Every child goes; it is not a decision | **Four working years per pupil, plus a teacher, plus a building.** If it is still always correct, the floor in §3.2(a) is too generous — *measure it* |
| **Schooling is never correct** | Nobody uses it | The mirror failure, and just as real. The floor has a **width** and it wants a probe |
| **The ratchet** | Once schooled, the village never regresses | Schooled villagers **still die**, and every generation pays again. ⭐ **It is a recurring investment, not a permanent unlock** — which is exactly why it does not break §2.3 |
| **Mastery gated by schooling** | An unschooled villager cannot become a master | ⛔ **Forbidden absolutely** — `skills-catalog.md §6.7`: *mastery-the-tier is always reachable by time on the task.* **A school makes the road shorter; it may never close it** |
| **The invisible graduate** | The player cannot tell schooled from unschooled | It says so on the villager panel and in the life log (§6) |
| **A child conscripted** | The player micromanages who attends | Attendance follows the allocator's shape — **the player sets how many seats, never which child** (D51, D15) |

---

## 5. ⚠️ This spec AMENDS `skills-catalog.md §6.6`, deliberately and on the record

**§6.6 as written forbids what Joe just asked for**, and it names the school explicitly:

> *"**Proficiency is NEVER restorable from a record.** No node, no library, **no school** and no
> policy can hand anybody years they did not work. A school produces *readers* and a record
> produces *method*; only a life produces proficiency."*

**⭐ The amendment is principled rather than a carve-out, and the principle is *payment*:**

| | What it costs the recipient | Verdict |
|---|---|---|
| **A record / library grant** | **Nothing.** You open a book | ⛔ Never confers proficiency. Rule unchanged |
| **A school** | **Four years of their life, and four years of the village's labour** | ✅ Permitted — *the years are real and they were spent* |

**So the rule becomes: nothing hands anybody years they did not SPEND.** A record hands over
*method* for free and therefore never confers proficiency; **a schooled child genuinely spent four
years, and the village genuinely went without their work.**

**⭐⭐ AND THIS IS EXACTLY WHY JOE DELETED THE LIBRARY'S +50% IN THE SAME BREATH AS ADDING THE
SCHOOL (D209).** The two moves look opposite and are the same judgement: *education you pay for is
legitimate; education that falls out of a building is a ratchet.* **The anti-ratchet rule is
sharpened by this change, not weakened by it.**

⚠️ **What is NOT amended, and must never be:** `tech-tree.md §3a` (*a record preserves the method,
not the proficiency*), and `skills-catalog.md §6.7` (*mastery is always reachable by time on the
task; no knowledge state may gate it*).

---

## 6. Legibility

- **On the villager panel** — the sentence: *"Wren spent four years at the school."* ⛔ Never a
  percentage; `proficiency 73` is the spreadsheet §7 of `skills-catalog.md` rejects by name.
- **In the life log** — attending and finishing are both life events, like apprenticeship.
- **On the school's panel** — *"14 of 20 places taken"*, in the vocabulary D148 gave the professions
  rows (*"2 working of 3 seats"*).
- **⭐ When the schools are full** — the village asks for another **by name**, the way it already
  does when it runs out of room to build houses (D42). *That is the demand signal, and it exists.*

---

## 7. Where it plugs in

| Thing | Status |
|---|---|
| `adult_age = 12` | ✅ **Shipped, and D156 reserved it for exactly this** |
| `LifeStage.Child` / `Adult` | ✅ Exists; `AgeingSystem` is the single reader |
| Staffing a workplace by seats | ✅ Exists — the teacher is an ordinary job |
| Capacity → *build another* | ✅ The pattern exists (housing, granary) |
| `SkillProgress` | ✅ Exists — a starting floor writes here |
| `JobKind.Teacher`, `BuildingKind.School` | ⛔ **New enum values** — ⚠️ and see `content-inventory.md` finding 8: this is exactly the moment to ask **enum value or data row** |
| A school requiring a library in catchment | ⚠️ `tech-tree.md §7e` requires it. **Joe's model does not mention a library** — see §9 |

---

## 8. Determinism

- **Integer only** (D2); hashed in a stated order, **sparsely** — a village with no school hashes as
  it does today.
- **Seeded, never random per-tick.** Which child takes a free slot is decided by the allocator's
  existing cost-first, deterministic pass, not by a roll.
- ⚠️ **The goldens will move once**, when the first schooled villager works — taken last, one
  commit, one stated reason (D152).

---

## 9. Open — for Joe

1. ✅ **ANSWERED (D754, Joe: *"Yes, all three"*) — as recommended in §11.3.** No library-in-catchment rule. *(As asked:)* **Does a school still require a library in catchment?** `tech-tree.md §7e` makes this *"the one
   place adjacency is structural rather than bonus"*, with a red marker and a refused placement.
   **Joe's model does not mention it**, and D204 already deferred literacy. **Simplest reading: no —
   the school teaches children, it does not copy books.**
2. **How much is four years worth?** The width of the floor is the whole feel of the system, and
   §4's first two failure modes are its two edges. **Wants a probe, not a guess.**
3. ✅ **ANSWERED (D754, Joe: *"Yes, all three"*) — as recommended in §11.3.** The village fills the places. *(As asked:)* **Is schooling per-child opt-in, or does the village simply fill the seats it has?** Filling the
   seats is consistent with §2.2 and needs no new control.
4. ✅ **ANSWERED (D754, Joe: *"Yes, all three"*) — as recommended in §11.3.** Credit for the years served. *(As asked:)* **What happens to a pupil when the school is demolished, or the teacher dies mid-cohort?**
   Partial credit for years served is the obvious answer and matches *time on the task*.
5. ✅ **ANSWERED (D754, Joe: *"Yes, all three"*) — as recommended in §11.3.** A full meal, as today. *(As asked:)* **Does a schooled child eat a dependant's share or a full one?** They are `Adult` by age today,
   so they currently eat a full meal (D156, D191) — worth confirming that stays true while at
   school, since it makes the cost slightly higher again.

---

## 11. Re-read against today's code (D752, 2026-11-14)

The spec was written on 2026-08-24, before goods, trades and buildings became rows, before the tech-tree map, nomads,
clothing, house tiers and the Phase 5 chains. Read against `main` at D751:

### 11.1 Still true

- **`adult_age` is 12** (`data/sim.config.json`), and D156's note beside it still reserves the dial for this.
  `leave_home_age` and `fertility_min_age` (18) are separate, so schooling touches neither pairing nor births.
- **A 12-year-old eats a full meal** (only those under `adult_age` eat `dependant_food_share_percent`) — so §9.5's
  *"a pupil eats a full share"* is what the code already does.
- **`SkillProgress`** holds `Ticks` (the honest calendar — *"seventeen years in the woods"*) and `Work` (the weighted,
  never-shown number mastery is measured against, D183). **A floor belongs in `Work` and never in `Ticks`**: a graduate
  has not spent years in the woods, and the sentence must not say so.
- **Enum or row** (§7's question) is answered: goods, trades and buildings are rows with enum aliases since D210, D218 and
  D222 — the school and the teacher are added as the tailor and the workshop were (`BuildingKind.School` = 30,
  `JobKind.Teacher` = 17), both rows in `SimConfig`.

### 11.2 What the code changes about the spec

1. **The size of the cost, measured** on Joe's Hollowmere: Year 29 has **no** 12–15-year-olds; Years 44 and 66 have **6**
   each, **every one working** — **16–21 % of the village's working adults**. A school that took them all would take a
   fifth of the labour for four years in five. §3.1's *real cost* is real.
2. **⛔ Seven trades have no skill row** — fisher, hunter, smith, quarrier, miner, herdsman, merchant (D391's debt; 10
   rows for 17 trades). A floor in `Work` does nothing for them: a schooled child who becomes a fisher gains nothing.
   Not in the spec, because in August most of those trades did not exist.
3. **⛔ The tech tree already has a school node, and it says something else.** `TechTree.DefaultNodes`' *"school"* is
   fogged (`TechCondition.NotYet`), `Requires = library`, and reads *"Where what the library keeps becomes what the young
   know"* — `tech-tree.md §7d`'s *readers* framing, which that spec annotates as superseded by Joe's *more proficient
   workers* (D209). **Nothing says how the school is learned** — §3 never asked.
4. **§3.2(a)'s floor does not say *which* skill.** A pupil at 15 has no trade yet. Either the floor lands in **every**
   skill row at graduation, or in the **first** trade they take up.
5. **`tech-tree.md §7e`** (a school refused unless a library is in catchment, a red marker) **still stands in that spec**
   and is the question §9.1 asks.

### 11.3 ⏸️ The calls, for Joe — each with a recommendation

1. ✅ **ANSWERED (D754, Joe: *"Yes, all three"*) — as recommended in §11.3.** *(As recommended:)* **When does the school appear?** ⭐ **Recommended: once the library stands** — the tree's node already requires it,
   and the library is the literacy gift at about Years 15–19, so the first pupils are the children of the village's
   first settled generation. **No *library nearby* placement rule** (§9.1, `tech-tree.md §7e` annotated): it teaches
   children, it does not copy books.
2. ✅ **ANSWERED (D753): two universal benefits, §11.4 — not a trade head start.** *(The recommendation as it stood:)*
   **What does school give?** ⭐ **Recommended: a head start in every trade that has a skill** — at graduation the
   hidden `Work` of every skill row gets the same floor, so whatever trade a graduate takes, they start some years
   ahead; mastery is still reached by time on the task (§4, `skills-catalog.md §6.7`). Said as a sentence (*"Wren spent
   four years at the school"*), never a number. **How many years ahead is measured, not typed** (§9.2): harness
   villages over a century, no school against a full one, at several floors — the floor that makes it *a decision, not
   an upgrade* (§4's first two rows), brought back as numbers before it is set.
3. ✅ **MOOT (D753): both benefits are the person's, not the trade's, so a fisher graduate gains as much as a farmer.**
   *(As it stood:)* **What about the seven trades with no skill?** ⭐ **Recommended: accept it for the first landing** — school helps
   the ten skilled trades, and the panel says so — and record the seven rows as their own later piece (D391's debt), so
   this slice does not grow into re-deriving seven trades.
4. ✅ **ANSWERED (D754, Joe: *"Yes, all three"*) — as recommended in §11.3.** *(As recommended:)* **Who goes?** ⭐ **Recommended: the player sets how many teachers (staffing, D51); each teacher brings a fixed number
   of places (a data row — 6 to start, measured); the village fills the places from its 12–15-year-olds by itself, the
   allocator's deterministic pass, never a pick of which child** (§9.3, D15). To school fewer, staff fewer teachers.
5. ✅ **ANSWERED (D754, Joe: *"Yes, all three"*) — as recommended in §11.3.** *(As recommended:)* **The small ones, as the spec already leans:** a pupil whose school closes or loses its teacher keeps the share of
   the floor for the years served (§9.4, *time on the task*); pupils eat a full meal (§9.5, as today); a teacher needs
   no skill of their own for the first landing; at school, pupils are under a roof by the same rule as any indoor
   workplace. *(⛔ void — there is no such rule: every workplace is open ground for the cold; measured D757, §12.3.)*

*Once these are answered: the decisions logged, §3–§9 amended in place and annotated, a §12 order of work, and the
measurement for call 2 before any number is typed.*

### 11.4 ✅ What school gives — two universal benefits (D753, Joe: *"A and B"*)

Joe asked for *"a more innovative and novel solution … some kind of universal benefit(s)/buffs for all who attend"*.
Four were offered against the rules every benefit must pass: **no invisible multiplier** (D207 refused them by name),
**no ratchet** (every generation pays again — D196, D209), **never a gate on mastery** (`skills-catalog.md §6.7`), and
**seen in what a villager does, said as a sentence** (§1.1). He chose two. Both belong to the **person**, not the trade.

**A — "She learned to look after herself": a graduate keeps her full strength longer.** *(⛔ reshaped D761 — measured weak
(D757: a 33-year-old is already ~96 % strong) and replaced, at Joe's *"Fuller loads + old age"*, by **a gentler old age**:
full strength still ends at 30, the fall after is shallower to a higher floor; §12.5. `school_vigour_years` is gone.)*
- Today vigour is full until `vigour_full_until_age` (30), then falls in a straight line to `vigour_min_percent` (55)
  in the final year. **A graduate is at full strength until 30 + `school_vigour_years`**, then falls the same way to
  the same floor; her **lifespan is unchanged** (school buys strong years, not more years — no story becomes one of
  schooled immortals).
- Seen: a graduate of 34 walks and works as a young adult does. Said: *"Still at full strength at 34 — she learned at
  school how to keep well."*
- **Generational by construction:** four years paid at 12–15, collected at 30–40, after the player who built the
  school has moved on (Non-negotiable 5).

**B — "She reads the year": a graduate sees trouble coming.**
- **(i) Built with the school — she judges the walk to the fire.** Everybody now goes in at `seek_shelter_percent`
  (50) of the way to freezing, wherever they are (the config's own trade: *"lower and nobody works through a winter;
  higher and the walk to the fire is itself what kills them"*). **A graduate goes in when the cold she will gather on
  the walk to the nearest fire would take her past that line** — sooner from the far wood, later beside the village —
  so she is caught out less **and** works more of the winter, not less. Seen: a graduate leaves the far wood while the
  others work on. Said: *"Wren left the far wood early — it is a long walk to a fire."*
- **(ii) A hook for Phase 6's pressures, not built now:** drought, fire and blight are foretold (Phase 6's gate item
  6); **a village with living graduates hears the warning sooner** — the size of *sooner* is each pressure's spec's to
  set. Recorded here so those specs do not forget it.

**Sized by measurement, never typed** (§9.2's rule, kept): `school_vigour_years` and B(i)'s effect are measured on
harness centuries — no school against a full one, at several sizes — to find the size at which a school is worth
building **and** schooling every child is not automatically right (§4's first two rows). Numbers come back to Joe
before either is set.

**Refused, with the reason, so it is not proposed again:** a graduate's children needing fewer years of school —
appealing, but it makes schooling cheaper every generation, which is the ratchet. **Offered and not chosen:** C, a
graduate keeping a technique alive after its last master dies (the knowledge panel: *"kept by Wren, who went to
school"*); D, a graduate's first year in any new trade counting double (helps only the ten skilled trades).

## 12. The order of work (D754)

One slice, `slice/school`; each step its own commit with its docs; tests before code (METHODOLOGY §2).

1. **The school in the sim, without its benefits.** `BuildingKind.School` (30) and `JobKind.Teacher` (17) as rows, the
   tailor's and workshop's shape — the costs typed and said to be typed, never claimed as measured. **Learned once a
   library stands** (the *school* node's condition; its fogged *"readers"* line rewritten to Joe's model). Each staffed
   teacher brings `school_places_per_teacher` places (6 to start); **the village fills them by itself** from its
   villagers aged `adult_age` to `school_leaving_age` − 1 (12–15) who are still within their term, by the allocator's
   deterministic pass — never a pick of a child (D15, D51). A pupil works at nothing else, is under a roof by the
   indoor-workplace rule, eats a full meal, and counts the years served; at `school_leaving_age` (16), or when there is
   no place, they go to work with **credit for the years served**. Life-log lines for starting and finishing.
   Guards: places from staffing, filling without a pick, the term, the credit, a school-less village unchanged.
2. **A and B(i), behind sizes in the data** (§11.4) — A: full strength until 30 + `school_vigour_years`, scaled by the
   years served of the term, lifespan untouched; B(i): a graduate goes in from the cold when the chill of her walk to
   the nearest fire would carry her past `seek_shelter_percent`, never at a fixed point. Guards that each is *seen* — a
   graduate's vigour at 34, a graduate leaving the far wood before an ungraduated neighbour and reaching the fire
   less cold. **The sizes stay at zero until step 3.**
3. **⭐ The measurement — the numbers before anything is typed** (§9.2). Harness centuries on several seeds, posed so a
   library and a school stand: no school, a full school, and a half one, across sizes of A and B. What a school costs
   in work and what it returns in strong years and winter safety; **the size at which it is worth building and
   schooling every child is not automatically right** (§4's first two rows). Brought to Joe in plain words; **he sets
   the sizes.**
4. **The view.** The school on the build bar (once learned), its card (*"14 of 20 places taken"*), the teacher on the
   professions panel, the villager card's sentences (*"Wren spent four years at the school"*, A's and B's lines), the
   tree's node, and the village asking for another school by name when the places run out (§6). A probe line for each.
5. **Saves, the hash, the goldens** — the new state saved and hashed sparsely (a village with no school is
   byte-identical); the goldens taken last, once, with the reason (D152); the four checks; then Joe plays it.

*B(ii), the Phase 6 pressures' sooner warning, is not on this list — it is each pressure's spec's to build (§11.4).*

### 12.1 ✅ Step 1, as built (D755)

- **Rows:** `BuildingKind.School` (30) — 30 logs, 12 stone, 40 work ticks, two teachers' seats, a 2 × 2 footprint, all
  ⏳ typed; `JobKind.Teacher` (17) — no skill, no tool, nothing made. Config: `school_places_per_teacher` 6,
  `school_leaving_age` 16, `school_lesson_ticks` 4 (validated: places ≥ 1, leaving age above `adult_age`, a lesson ≥ 1).
- **Learned:** `SimWorld.SchoolLearned`, a latch set where a library is raised (`LearnTheSchool`, said once in the
  Discovery log) and, for a save from before the school, from the libraries it holds; kept if the library comes down.
  The gate is `WhyNotYet` (*"Nobody here teaches yet — a school comes once the village has a library"*), read by the
  build bar and `Mark`. The tree's *school* node unlocks the school on `TechCondition.LibraryStands`, its line
  rewritten from *readers* to Joe's model.
- **Teachers** are only ever the player's number: the quota's want is nobody (`needed` 0), `Asked` and pins as every
  trade. ⭐ **A teacher is past school age** — the allocator never offers the seat to a villager under
  `school_leaving_age` (a step-1 rule, so a twelve-year-old does not teach the class they would sit in).
- **Pupils** (`SchoolRoll`, run by both labour passes **after** the match, so places count only seated teachers): a
  school's places are its teachers at work × `school_places_per_teacher`; when they fall short, the least served leave
  first, keeping every tick. Free places go to the living, able, unpinned 12–15-year-olds who have not sat a full term —
  ⭐ **the youngest first** (the most years before `school_leaving_age`), then the most term still to serve, then the
  shortest walk from home, then the lowest id: a total order, never a pick. *(The spec had said cost-first; the age key
  is this build's, because a place is worth most at twelve — Joe's to overrule.)* A pupil keeps the place from pass to
  pass, holds no job (one held is given up on taking the place), is **not** a hand (`CanTakeAJob`, read by
  `AbleAdults`, the quota and the candidates) and not a laborer, but still runs the household's errands and takes a coat.
- **The term** (`SchoolSystem`, after the skill system): a tick served for each tick holding a place; at a full
  `SchoolTermTicks` (four years) or `school_leaving_age` they leave *"for work"*. Credit is kept for life.
- **Seen:** a pupil walks to school, sits a lesson of `school_lesson_ticks` (*"at school"*), walks home; a teacher the
  same (*"teaching at the school"*). Life-log lines for starting and leaving.
- **Saved and hashed:** `school_id` and `school_ticks` on the villager (read as 0 when absent), `school_learned` on the
  world. The villager's two are hashed sparsely now (no unattended village builds a school). ⚠️ **The latch is not yet
  hashed**: every golden village raises the library gift around Year 15, so hashing it now would move every golden for a
  flag nothing unattended reads — it joins the fingerprint at step 5 with the goldens taken once (HANDOFF's debt). *(⛔ void — hashed at step 5 and no golden moved, D766: an unattended village never raises its gifted library, so no golden village learns the school.)*
- **Guards:** `SchoolTests` (9): learned once a library stands and not before (the gate, the log line, the tree's node);
  places from staffing and the youngest first, a pupil no hand and no laborer; no teacher, no places; a teacher past
  school age; the term ends with every tick served and is never sat twice; credit kept when the teacher goes; a pupil
  seen at the school and a teacher teaching; a pupil and the latch back from a save with the hash equal; a village with
  no school never sees a pupil.

### 12.2 ✅ Step 2, as built (D756) — both benefits, at size nought

- **A:** `school_vigour_years` (0 shipped). `AgeingSystem.StrongYearsFromSchool` — the knob scaled by the term served,
  never more than a full term's — moves the end of full strength from `vigour_full_until_age` to that plus the years,
  never past the year before the last; the decline after runs to the same floor in the same final year, so **the
  lifespan is untouched**.
- **B(i):** `school_heed_percent` (0 shipped; validated 0–95). A villager **reads the year** with the knob on and at
  least **half the term** served. The one rule both kinds go in by is `BehaviorSystem.GoesInForWarmth`: everybody at
  `seek_shelter_percent` of the way to freezing; a reader when her cold **plus the chill of her walk to the nearest
  fire** (its ticks by the one cost field, at her open-ground rate, a coat's if she wears one) reaches
  `school_heed_percent`. Her line is her own, so beside the village she works on past halfway, and from the far wood
  she leaves before it. ⭐ **Her words are said when she goes** — *"Wren heads in early — it is a long walk to a fire"*
  short of halfway, *"Wren goes in to get warm, the fire near enough to have worked on until now"* past it — and the
  halfway warning (*"…is dangerously cold … going in to get warm"*) is not said of her, because it would be untrue.
- **At nought nothing changes:** a non-reader's decision is the old one, line for line (no golden moved).
- **Guards:** `AGraduateKeepsFullStrengthLonger` (the scale, the cap, full at 34, the same floor at the end, nought
  inert) and `AGraduateJudgesTheWalkToTheFire` (on near home, early from the far wood, half a term needed, nought
  inert). **4 mutants, 4 red** — one first scored zero because it did not compile (an unused parameter is an error
  here); rewritten to compile, red.

### 12.3 ✅ Step 3, the measurement (D757) — ⏸️ two calls for Joe

`tools/harness/ZzSchool.cs` + `school.py`: the shipped cold start, the opening played, a granary and a warehouse at
Year 3; in the school arms the school posed learned at Year 10, marked beside the founding (its stone given — no harness
village holds stone), one teacher asked for once it stands; a century on seeds 200–299. **Strength-weighted work** is the
able adults' vigour summed — the work a village's strength is actually worth.

| Arm (100 seeds, a century each) | Dead valleys | Alive at 100 | Starved | Froze | Pupil-years | Graduates | Strong years | Shelter days | Work-years | Strength-weighted work |
|---|---|---|---|---|---|---|---|---|---|---|
| no school | 12 | 10.3 | 15.4 | 12.9 | 0 | 0 | 3.3 | 1,116 | 1,015 | 870 |
| school, sizes 0 | 13 | 10.7 | 15.6 | 13.3 | 44.6 | 11.4 | 3.3 | 2,164 | 950 | 809 |
| A 3 | 14 | 11.0 | 15.1 | 13.3 | 45.5 | 11.6 | 16.6 | 2,175 | 954 | — |
| A 5 | 14 | 10.8 | 15.1 | 13.3 | 45.0 | 11.5 | 29.0 | 2,201 | 951 | 814 |
| A 8 | 14 | 11.1 | 14.8 | 13.3 | 44.8 | 11.4 | 47.2 | 2,173 | 953 | — |
| A 15 | 13 | 11.1 | 15.0 | 13.3 | 45.4 | 11.6 | 85.6 | 2,184 | 958 | 830 |
| B 70 | 14 | 10.7 | 15.4 | 13.3 | 44.9 | 11.4 | 3.3 | 2,038 | 951 | — |
| B 80 | 13 | 10.9 | 15.6 | 13.3 | 44.4 | 11.3 | 3.3 | 1,968 | 950 | — |
| B 90 | 13 | 11.0 | 15.0 | 13.3 | 43.7 | 11.1 | 3.2 | 1,922 | 939 | — |
| A 5 + B 80 | 13 | 11.0 | 15.2 | 13.3 | 45.1 | 11.5 | 29.1 | 1,967 | 952 | 815 |

**What it says, in plain words:**

1. **The cost is real and measured:** one teacher and six places take **61 strength-weighted work-years a century** —
   about 7 % of the village's work — for ~11 graduates. Dead valleys 12 → 13–14 (inside the noise).
2. **⛔ A gives back very little work, at any size.** A 33-year-old who never went to school is already ~96 % strong
   (the decline runs from 30 to the lifespan), so extra strong years past 30 are worth little: **A 5 returns 5 of the 61;
   A 15 returns 21.** Nobody's survival moves.
3. **B shortens the winter's shelter time** — 6 % at 70, **9 % at 80**, 11 % at 90 — and saves no lives (the frozen
   are the unhoused and the hearthless, whom reading the year cannot help); at 90 the days actually caught out rise.
4. **⛔ So at every size tried the school is §4's second failure mode — *never correct* — on the economy alone.**
5. **⛔ A school in winter is open ground.** Only a home gives shelter (`SimWorld.ShelterAt`); **every workplace is
   outdoors** for the cold, so the teacher, standing in it all winter, logs ~800–860 shelter days a century, and the
   pupils 150–850 — the school **doubles** the village's shelter days by itself. ⚠️ §11.3 call 5's *"a roof by the
   indoor-workplace rule"* was wrong: there is no such rule (void inline in §11.3).

**⏸️ The calls (Joe's):** whether a school that is a story more than an investment is acceptable, or the benefit must
grow (a bigger A, a slower decline rather than a later one, or C/D back on the table) or the cost shrink (a shorter
term); and what a school is in winter (closed — a holiday at home by the family's fire; warm — it burns firewood like a
house; or cold, as now).

### 12.4 📏 A two-year term, 11 to 13, and where a village's day goes (D758) — ⏸️ Joe's

Joe, on §12.3: *"yeah, we need to improve the value. let's test 2 years in school from age 11-13. and what can we do to make
a material change to the benefits of attending school? short- and long-term?"*

**Built for it:** `school_starting_age` (shipped **12**, today's game, until Joe sets it). The term is the years from it to
`school_leaving_age`; a place is taken **by age alone**, so below `adult_age` a pupil is a child — who works at nothing
anyway, and so costs the village no labour. Guard `ATermThatStartsBeforeWorkTakesAChild`; **3 mutants, 3 red** (the
first scored zero until the fixture's pose set the life stage with the age — §12.4's trap, in HANDOFF).

| Arm (100 seeds, a century each) | Dead valleys | Alive at 100 | Starved | Froze | Pupil-years | Graduates | Strong years | Shelter days | Work-years | Strength-weighted work | Against no school |
|---|---|---|---|---|---|---|---|---|---|---|---|
| no school (D757's, reproduced to the decimal) | 12 | 10.3 | 15.4 | 12.9 | 0 | 0 | 3.3 | 1,116 | 1,015 | 870.0 | — |
| four years, 12–16, sizes 0 (D757) | 13 | 10.7 | 15.6 | 13.3 | 44.6 | 11.4 | 3.3 | 2,164 | 950 | 809 | **−61** |
| **two years, 11–13, sizes 0** | 12 | 11.0 | 14.9 | 12.8 | 23.3 | 12.4 | 3.2 | 2,039 | 990 | 847.7 | **−22** |
| two years, A 5 | 13 | 10.3 | 15.7 | 12.8 | 23.0 | 12.3 | 28.1 | 2,016 | 980 | 843.4 | −27 |
| two years, A 15 | 12 | 10.3 | 15.3 | 12.8 | 22.7 | 12.1 | 82.0 | 2,003 | 981 | 854.0 | −16 |
| two years, B 80 | 12 | 10.9 | 15.6 | 12.8 | 23.7 | 12.6 | 3.3 | 1,847 | 984 | 843.3 | −27 |
| two years, A 15 + B 80 | 12 | 10.9 | 15.9 | 12.8 | 23.5 | 12.5 | 84.8 | 1,805 | 986 | 860.0 | −10 |

| What the able adults were doing (no school, 100 centuries) | Share of their ticks |
|---|---|
| Resting (the day's rest, and nothing wanted) | 68.1 % |
| Walking home | 11.3 % |
| Carrying to a store | 6.5 % |
| Fetching from a store | 4.3 % |
| Walking to food | 3.6 % |
| The work itself — gathering, clearing, tidying, building, firewood, materials | 5.2 % |
| Going in for warmth | 0.8 % |

**What it says, in plain words:**

1. **The cost falls to about a third** — 22 strength-weighted work-years a century against 61. The year at 11 is a
   child's and costs nothing; only the year at 12 and the teacher are paid for. **Graduates barely move (12.4 against
   11.4): the village's children, not the places, are what limit how many** — a harness village has about two
   12-year-olds a decade.
2. **A and B together come within 10 of paying** (A 15 + B 80: −10). Survival still does not move in any arm.
3. **The break-even, as arithmetic:** a graduate costs about **1.8 strength-weighted work-years**; a working life is about
   45. **A graduate must be ~4 % more useful for life to pay for her schooling, and ~10 % to be plainly worth it.**
4. **⭐ The time budget says why A is weak and survival never moves:** able adults **rest two thirds of the time**, and of
   the rest **four fifths is walking and carrying** — the work itself is 5 % of the day. **A harness village is short
   of trips, not of muscle**: labour is slack, so neither school's cost nor a strength bonus reaches who lives. ⚠️ The
   harness is not a player (D447): a played village of fifty with its chains running is busier, and there the cost
   bites harder — and so would a benefit to the walking.

**⏸️ The calls (Joe's):** which material benefit to add or reshape — the options in D758, short-term (fuller loads,
banking the fire, the trade head start) and long-term (a gentler old age, C back) — each measured before it is set; and
the school in winter (§12.3), still open. *(✅ D761: Joe, *"Fuller loads + old age"* — §12.5.)*

### 12.5 ✅ Fuller loads and a gentler old age — built at size nought, measured (D761) — ⏸️ the sizes are Joe's

Joe: *"Fuller loads + old age"*.

- **Fuller loads:** `school_carry_percent` (0 shipped; 0–100). `SchoolGives.Load` — the knob scaled by the term served,
  never more than a full term's — is the one reader at **every place a villager's load is what she can carry**: a
  gatherer's basket and a woodcutter's cut (on what her strength brought, so an old graduate is both), the household's
  fetch and the works' rounds, building materials, tidying and clearing, the dig at a face, hay, and the trading post.
  **Not a catch** — fishing and hunting are what was caught, not what was carried. At nought every load is the old one
  exactly.
- **A gentler old age — A reshaped:** D753's A (strong years past 30) measured weak (D757) and is **gone**, with
  `school_vigour_years`. `school_old_age_percent` (0 shipped): the vigour points a full-term graduate keeps above
  `vigour_min_percent` at the end of her life, by the term served. **Full strength still ends at 30, as everyone's**; the
  fall after is shallower to a higher floor; the lifespan is untouched. ⚠️ A higher floor is also a later elderhood
  (it is a vigour band), so a graduate eats a full meal longer — counted in the measurement, not hidden.
- **Guards:** `AGraduateAgesMoreGently`, `AGraduateCarriesAFullerLoad`, `AGraduateBringsMoreHomeFromAGather` (Phase 0's
  ruler, food per trip, ≥ 1.4× at 50 %). **5 mutants: 4 red; the fifth — the household's store fetch reverted — scored
  zero and is kept: 13 of the 14 load sites are guarded only through the one helper** (HANDOFF's D761 trap).

| Arm (100 seeds, a century; the two-year term, 11–13, unless *no school*) | Dead valleys | Alive at 100 | Peak | Starved | Froze | Born | Graduates | Work-years | Strength-weighted work | Against no school | Walk-years |
|---|---|---|---|---|---|---|---|---|---|---|---|
| no school | 12 | 10.3 | 19.2 | 15.4 | 12.9 | 41.7 | 0 | 1,015 | 870.0 | — | 266.5 |
| school, sizes 0 | 12 | 11.0 | 18.9 | 14.9 | 12.8 | 41.6 | 12.4 | 990 | 847.7 | −22 | 319.8 |
| **fuller loads 25 %** | 13 | 11.8 | 20.4 | 15.5 | 12.8 | 43.7 | 13.2 | 1,040 | **891.1** | **+21** | 318.6 |
| **fuller loads 50 %** | 12 | 11.8 | 21.1 | 15.9 | 12.8 | 44.4 | 13.5 | 1,072 | **918.5** | **+48** | 318.1 |
| old age +15 | 12 | 11.0 | 18.9 | 15.1 | 12.8 | 41.6 | 12.5 | 987 | 855.2 | −15 | 318.6 |
| old age +30 | 12 | 10.5 | 18.8 | 16.2 | 12.8 | 41.7 | 12.4 | 976 | 854.9 | −15 | 318.9 |
| fuller loads 50 % + old age +30 | 12 | 12.0 | 21.1 | 16.5 | 12.9 | 44.9 | 14.0 | 1,084 | **951.9** | **+82** | 321.6 |
| ⭐ **the recommended mix (D762): loads 25 % + old age +20 + B 80** | 12 | 11.6 | 20.1 | 15.5 | 12.8 | 43.3 | 13.2 | 1,041 | **905.6** | **+36** | 319.4 |

**What it says, in plain words:**

1. **⭐ Fuller loads is the first benefit that makes a school pay.** At 25 % a two-year school is **+21** over no school
   (+43 over a school that gives nothing); at 50 %, **+48**. The villages are bigger — peak 19 → 20–21, born 42 → 44,
   alive at the century 10.3 → 11.8.
2. **It does not save walking: the village spends the saving on growing.** Walk-years are flat (318 against 320) —
   the trips it saves come back as more food a trip, more children, more people. And it does not save the starving or
   the frozen (dead valleys 12–13 throughout): those are harness failures of planning, not of carrying (D447).
3. **A gentler old age barely shows inside a century:** +7 over the bare school at +15 and at +30 alike — no step
   between the sizes, so it is inside the noise. A graduate's decline mostly falls after the century, or after an early
   death in a harness village. With fuller loads it adds +33 (more graduates live to be old). It is the generational
   one: it lands forty to sixty years after school.
4. A school adds about 53 walk-years of its own (not chased).
5. **§4's two failure modes:** at 25 % the school pays about 2½ % on the century — worth building, paid for up front, the
   return ten to twenty years later — so it is a decision, not an upgrade. At 50 % it is closer to *always correct*.

**⏸️ Joe's:** the sizes — recommended **fuller loads 25 %, a gentler old age +20, B at 80**, with **the two-year term,
11–13**, measured together before they are typed; and the school in winter (§12.3).

**📏 The mix, measured (D762, Joe: *"Yes, measure that"*):** **+36 over no school** — more than loads 25 % alone (+21);
peak 20.1, alive at the century 11.6, born 43.3, dead valleys 12; B trims the winter's shelter days 2,214 → 2,009 (−9 %),
as it did alone. About 4 % of the village's strength-weighted work: a school worth building, paid for up front, never
free. ⏸️ **Joe's: type it as shipped, and the winter call.** ✅ **Typed (D763, Joe: *"Yes, ship it"*):**
`school_starting_age` 11, `school_leaving_age` 13, `school_carry_percent` 25, `school_old_age_percent` 20,
`school_heed_percent` 80 in `data/sim.config.json`; the C# defaults stay at nought and 12–16, which the guards pose. The
tree's line no longer says *"four years"*. ⏸️ The winter call is still his. *(✅ D764 — warm, §12.6.)*

### 12.6 ✅ A warm school — it burns firewood like a house (D764, Joe: *"Warm, burns firewood"*)

D757 found the school open ground in the cold: only a home gave shelter, so the teacher stood in the weather all winter
and the school doubled the village's shelter days. Joe chose **warm**, over closed for winter and cold as now.

- **A school is a roof, and a staffed school with wood in its pile is a fire** (`SimWorld.ShelterAt` — a reader, as for
  a home: *"a home with any firewood is a lit hearth"*). Unstaffed, or with its pile empty, it is a roof — better than
  open ground, as a hut is. **A pupil or a teacher at a warm school thaws there**, as at a home.
- **It burns `school_winter_firewood` a winter** (12 — a cottage's, typed; ⏳ measured below), spread over the house's
  beats (`HearthSystem.BurnOnBeat`) and only while a teacher is seated.
- **Its pile is kept from the nearest store that gives firewood** — at each beat, after the burn, topped up to the next
  beat's (never below one, so it stays lit to the winter's end). Not walked: the forge's precedent (it takes its
  iron from the nearest store when it works, D391), so the school adds no errand and no new walking.
- **⭐ The homes come first.** The pile is topped up only when the homes are covered —
  `LabourQuota.HomesFirewoodShortfall` is nought — the rule the forge already keeps (*"the village needs its firewood
  for the winter"*). A school that cannot be kept says so once when its last wood goes: *"The school is cold — the
  homes need the village's firewood first."*
- **The woodcutters cut for it:** each staffed school's winter is in `LabourQuota.FirewoodShortfall`, the homes' winters'
  neighbour, so the quota staffs the hands for it rather than the school living off whatever the homes leave.
- **Saved and hashed already:** the pile is the school's `Workplace.Store`. No school, nothing changes — no golden moves.
- Guards: `AStaffedSchoolIsWarmInWinter` (a roof before wood, a fire in winter, its winter burned, a roof unstaffed),
  `TheHomesComeFirstForTheSchoolsFire`, `TheSchoolsWinterIsInTheWoodcuttersWant`. **6 mutants, 6 red** — the homes-first
  one first scored zero: posed with a few logs in the store, a household fetched them before the school's beat and the
  gate was never asked; re-posed one log short with the store full.

| Arm (the shipped mix: two years 11–13, loads 25 %, old age +20, B 80; 100 seeds, a century) | Dead valleys | Alive at 100 | Peak | Starved | Froze | Strength-weighted work | Against no school | Shelter days |
|---|---|---|---|---|---|---|---|---|
| no school | 12 | 10.3 | 19.2 | 15.4 | 12.9 | 870.0 | — | 1,116 |
| the school cold (D762) | 12 | 11.6 | 20.1 | 15.5 | 12.8 | 905.6 | +36 | 2,009 |
| **the school warm (D764)** | 13 | **12.0** | 20.3 | **14.8** | 12.8 | **908.7** | **+39** | **1,356** |

**In plain words:** a warm school takes **a third off the winter's shelter time** (2,009 → 1,356 — most of what the
school had added over no school's 1,116) and the village keeps the work (+39 against +36), a few more alive at the
century and fewer starved. Nobody more or less freezes (12.8): the frozen are still the unhoused and the hearthless.

### 12.7 ✅ Step 4, the view, as built (D765) — unplayed

| §12 step 4 asked | As built | Its check |
|---|---|---|
| The school on the build bar, once learned | step 1 (D755): `_schoolButton`, `EarnedYet`'s arm, the Knowledge tab | probe `filters:` / `bar height:` |
| Its card — *"14 of 20 places taken"* | `SimWorld.SayTheSchool`: places taken, who waits, the stove in winter (*"The stove is lit."* / *"…is cold — the homes need the village's firewood first."*), and *"Nobody teaches here…"* unstaffed; cells **pupils · places** and **children waiting** (`WriteSchoolCard`, the barn's facts-then-write shape) | `TheSchoolSaysItsPlacesAndAsksForAnotherOnce`; probe `school:` |
| The teacher on the professions panel | the row appears when the school is learned (`TradeKnown` reads `EarnedYet`, step 1) | probe `trades:` (teacher hidden at the founding) |
| The villager card's sentences | a **SCHOOL** section: `SimWorld.SayTheSchooling` — *"At Northfield school — in the first of two years."*; *"Spent two years at the school — carries a fuller load, will age more gently, and goes in from the cold by the length of the walk to a fire."* — each gift named only while its size is on; *"…before the place went"* for a part term; none for anyone who never went. ⭐ **This sentence is what keeps fuller loads from being an invisible multiplier (D207).** | `ASchoolingIsSaidInWords`; probe `school:` |
| The tree's node | the node's state is the sim's own reader (`TechTree.StateOf`, step 1); the view draws it as any node | `TheSchoolIsLearnedOnceALibraryStands`; probe `tech tree:` (every node fits) |
| The village asking for another school by name | `SimWorld.AskForAnotherSchoolIfFull`, run by the roll: once when children of school age are first left without a place — *"…another teacher at Northfield school would bring 6 more, or build another school"* while a seat is empty, *"every school is full. Build another"* when none is — and again only after every child has had one (D42's edge, `SchoolsAreFull`, saved, not hashed) | `TheSchoolSaysItsPlacesAndAsksForAnotherOnce` |

**Mutants: sim 4 / 4 red; probe 2 / 2 faults** (the places cell, the SCHOOL section hidden).
`ChildrenWaitingForSchool` is set by each labour pass, not saved: after a load the card says nobody waits until the
next pass (≤ 120 ticks).

### 12.8 ✅ Step 5, saves and the hash (D766) — no golden moved

- **Saved:** a villager's `school_id` and `school_ticks` (step 1), the world's `school_learned` (step 1) and
  `schools_are_full` (step 4), the school's woodpile as its `Workplace.Store` (as every workplace's). `SaveLoadTests`'
  every-field guard names each.
- **Hashed:** the villager's two, sparsely (step 1); the woodpile (every workplace store); and now **the latch,
  `SchoolLearned`** — a tag byte, only when learned. Guard `ALearnedSchoolIsInTheFingerprint`, red-checked.
  `SchoolsAreFull` is narration bookkeeping and `ChildrenWaitingForSchool` a cache: neither is mixed.
- **⭐ No golden moved — and the reason it was deferred was wrong.** D754 held the latch back because *"every golden
  village raises the library gift around Year 15"*; it does not — an unattended village never raises its gifted library
  (the harness had to pose the school learned for exactly that reason). The claim is void inline where it stood.

**⏳ Joe's walk (what to look for):**
1. Raise the library (gifted around Year 15–19): the log says *"somebody thinks to teach the young"*, and the school is
   on the Knowledge tab and the teacher's row on the professions panel.
2. Build a school and ask for a teacher: the 11- and 12-year-olds take places by themselves; a pupil's card reads
   *"At … school — in the first of two years."*; the school's card *"N of 6 places taken"*.
3. A winter: the school's card says *"The stove is lit."* and the teacher does not keep breaking off to get warm.
4. Two years on: a graduate's card says what school gave her, in words.
5. More children than places: the log asks once — another teacher, or another school.
6. Save and load in the middle of a term: the pupils are still pupils, with their years.

## 10. Definition of Done

1. This spec current and reconciled with what was built.
2. Content in **data files** — seats, ages, the floor (`DESIGN.md §3`).
3. Unit tests passing; determinism test still green.
4. **Anti-vacuity:** a village that never builds a school must be measurably worse off over a
   century than one that does — **and a village that schools everybody must not be strictly
   better**, or §4's first failure mode has shipped.
5. **Mastery guard:** an unschooled villager still reaches master by time on the task
   (`skills-catalog.md §6.7`), asserted directly.
6. **Legibility:** the panel and the life log both say who went to school.
7. Manual QA against a written checklist; no new errors across a clean 200-year playthrough.
8. `DESIGN.md` Progress Tracker + Decisions Log updated.
