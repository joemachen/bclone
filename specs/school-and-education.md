# Spec: The school — what four years of childhood buys

> Status: **specced (D209); 🔨 ON `slice/school` (D752, 2026-11-14, Joe: *"Start the school"*) — RE-READ AGAINST TODAY'S
> CODE (§11); CALLS 2 AND 3 ANSWERED (D753 — what school gives is two universal benefits, §11.4); ⏸️ CALLS 1, 4 AND 5
> WITH JOE; NOTHING BUILT.** Owner: Joe + Claude Code · Pillar: `DESIGN.md §2.1`, §2.7
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

1. **Does a school still require a library in catchment?** `tech-tree.md §7e` makes this *"the one
   place adjacency is structural rather than bonus"*, with a red marker and a refused placement.
   **Joe's model does not mention it**, and D204 already deferred literacy. **Simplest reading: no —
   the school teaches children, it does not copy books.**
2. **How much is four years worth?** The width of the floor is the whole feel of the system, and
   §4's first two failure modes are its two edges. **Wants a probe, not a guess.**
3. **Is schooling per-child opt-in, or does the village simply fill the seats it has?** Filling the
   seats is consistent with §2.2 and needs no new control.
4. **What happens to a pupil when the school is demolished, or the teacher dies mid-cohort?**
   Partial credit for years served is the obvious answer and matches *time on the task*.
5. **Does a schooled child eat a dependant's share or a full one?** They are `Adult` by age today,
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

1. **When does the school appear?** ⭐ **Recommended: once the library stands** — the tree's node already requires it,
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
4. **Who goes?** ⭐ **Recommended: the player sets how many teachers (staffing, D51); each teacher brings a fixed number
   of places (a data row — 6 to start, measured); the village fills the places from its 12–15-year-olds by itself, the
   allocator's deterministic pass, never a pick of which child** (§9.3, D15). To school fewer, staff fewer teachers.
5. **The small ones, as the spec already leans:** a pupil whose school closes or loses its teacher keeps the share of
   the floor for the years served (§9.4, *time on the task*); pupils eat a full meal (§9.5, as today); a teacher needs
   no skill of their own for the first landing; at school, pupils are under a roof by the same rule as any indoor
   workplace.

*Once these are answered: the decisions logged, §3–§9 amended in place and annotated, a §12 order of work, and the
measurement for call 2 before any number is typed.*

### 11.4 ✅ What school gives — two universal benefits (D753, Joe: *"A and B"*)

Joe asked for *"a more innovative and novel solution … some kind of universal benefit(s)/buffs for all who attend"*.
Four were offered against the rules every benefit must pass: **no invisible multiplier** (D207 refused them by name),
**no ratchet** (every generation pays again — D196, D209), **never a gate on mastery** (`skills-catalog.md §6.7`), and
**seen in what a villager does, said as a sentence** (§1.1). He chose two. Both belong to the **person**, not the trade.

**A — "She learned to look after herself": a graduate keeps her full strength longer.**
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
