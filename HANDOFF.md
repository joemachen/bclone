# Handoff — bclone: **Phase 3 is complete and merged. Phase 4 — the tech tree — is next.**

Read `CLAUDE.md`, then **`DESIGN.md` §0–§5 in full, §6, and §7 from D203 back to D142**, then
`METHODOLOGY.md`.

> **⛔ WHEN YOU HAND OFF: EDIT THIS FILE, DO NOT REPLACE IT.** The trap list at the bottom is
> accumulated from sessions that each paid for one entry. Rewriting it wholesale drops them
> silently — that happened on 2026-08-22 and cost an hour and three quarters within the same
> session. **Rewrite "where things are"; carry the traps forward.**

⭐ **Three things to know before you touch anything:**
1. **⭐⭐ D28 IS DISCHARGED** (D190) — the lockstep Joe watched at 4× in Phase 1 is gone, and
   **he confirmed it in play**: the four founders read as distinct people and no longer move as
   pairs. Identical hunger between two adults of one household went **100% → 0%**.
2. **✅ THE FARM IS UNPARKED (D194)** — the cap was **self-fulfilling**, and the ledger proved it
   after four hypotheses could not. **⛔ But Joe's thirteen tiles were never available**: thirteen
   tiles ten ticks from a store needs ~230 ticks of a 120-tick autumn. Read the section below
   before re-opening it, and **do not propose `farm_store_cap` — it is dead twice over.**
3. **✅ PHASE 3 IS COMPLETE AND MERGED** (D202, D203). Skill TRANSFERS now — a youth beside a
   master of the same trade at the same workplace learns twice as fast. ⚠️ **Its QA walk was
   WAIVED on Joe's call, not performed** (D203): if a Phase 3 regression ships, that is where it
   got through. **Phase 4 — the tech tree and the town hall — is next, and D196 has Joe's library
   model waiting for it.**

---

## Where things are

**Phase 3 is merged to `main`.** Its Definition of Done is met **with one item waived and written
down rather than ticked** (D203): METHODOLOGY §3's manual QA walk. Joe played the build
repeatedly through the phase and signed off the paint overlay, the market, the staffing cadence
and the whole — **but the phase was never walked end to end against a list, and Phase 3 has no
checklist at all.** *That is an unpaid debt Phase 4 should not inherit.*

**⭐⭐ WHAT PHASE 3 LANDED, in the order it landed:**

1. ✅ **The proficiency substrate** (D181, D183). `Villager.Skills` accrues time on the task,
   hashed sparsely in id order; six skills are **rows in config, not enum values**; the panel
   says *"Sixteen years as a farmer"*; **the mastery line fires** in the village log.
   **Nothing ever takes proficiency away**, and a tick out on the job is worth 1.5 of a tick
   waiting for one. Ages at mastery: **34–55, median 39.**
2. ✅ **Mastery bites** (D187) — **a master takes half the ticks over an action, rounded up.**
   ⚠️ **Below 34% the feature is literally a no-op**: durations are 3 and 4 ticks, so a bonus
   that does not round to a whole tick buys **nothing** — a village at 25% produced population
   and food *identical* to one with the feature off. `AMasterIsFasterAtEveryTrade` fails the
   build if it ever rounds away again.
3. ✅ **The mixed founding and the seeded rhythm** (D190) — a master, a journeyman and two
   novices with **seeded trades**, and a rhythm drawn at birth. **D28 discharged.**

4. ✅ **The at-risk line** (D195) — §11's last outstanding Definition-of-Done item. *"Wendell is
   48 and the only soul in the village who has mastered foraging. Put somebody beside them to
   learn it, or it goes with them."* One method (`SimWorld.KnowledgeAtRiskNote`), read by the
   village log **once on the edge** and by the villager's panel **while it is true**. Both halves
   of the condition are derived — `LifeStage.Elder`, and *the only living master*.

5. ✅ **APPRENTICESHIP** (D202) — §2.1's actual claim. **A youth beside a master of the same
   trade at the same workplace learns twice as fast.** Nobody is assigned to anybody; the master
   pays nothing; there is no dial. §10's anti-vacuity guard is green — **masters alive after a
   century go 3 → 6, 4 → 8, 8 → 10** against a village that never teaches.
   - ⛔ **IT REACHES ONLY 2–3 TRADES OF 5, AND THAT IS RECORDED RATHER THAN PAPERED OVER.**
     Forager and marketer always pair, forester sometimes; **woodcutting and building never do**,
     because they are one-seat trades with nobody to learn from. **The trades most likely to die
     with their last holder are exactly the ones apprenticeship cannot reach** — which is what
     **D196's library** is for, and why that answer is worth more than it looked.
   - ⚠️ **200% was too far**: seed 42 ends the century with **zero food**. A hundred leaves it at
     1,485 against 1,513 with the feature off. *The width is measured, not picked.*

⚠️ **The phase PR is #4, not #3.** Number 3 went to the closed screenshot-hook PR D160 rescued,
and every document in the repo said #3 for a day before anyone checked.

**Merged slice branches are deleted on Joe's standing preference**, each after checking it had
**0 commits not on `main`**. Tips if ever wanted back: `phase/3-skill-and-apprenticeship`
`028f4fc`, `phase/2-wood-fuel-and-tools` `9b9f410`, `slice/per-site-yield` `b2cb718`,
`slice/faster-cost-field` `daec8fd`.

⚠️ **`main` is 26 commits ahead of `origin/main` and NOTHING HAS BEEN PUSHED.** Phase 3 was
merged locally; Phase 2 went up as [PR #4](https://github.com/joemachen/bclone/pull/4), so if
the same shape is wanted for Phase 3 it is a push and a PR away.

**SUITE, FROM A RUN:**

```
740 passed, 0 failed, 2 skipped of 742 — about 2m30s (was 18m52s before D179)
```

The two skips are rulings, not unfinished work: **D143** (an unattended village is *supposed* to
die out) and **D134** (the granary stopped being the binding cap; the timber shed is).

```bash
dotnet test bclone.sln --nologo -v q
```

**It is fast enough to run in the foreground now** (D179 took it from nineteen minutes to two and a half). **Do not start a second run while one is going** —
and **check `Get-Process testhost` first**: one session found the *previous* session's run still
alive on thirteen cores, holding the lock on `Bclone.Sim.dll`, with nobody left to read its
output. Note the CPU figure is summed across cores, so a healthy run shows far more CPU-seconds
than wall-clock.

**The Godot view builds separately** — `dotnet build src/Bclone.Game/Bclone.Game.csproj` (D11) —
and has **no automated verification of any kind** (D160). Looking at it is the test.

---

## ⭐ What to do next — `DESIGN.md §4`'s queue, in its order

1. ✅ Phase 2 merged, and the branch deleted (it was fully merged; tip was `9b9f410`).
2. ✅ **`specs/skills-catalog.md` WRITTEN** (D173–D177), docs-only on `main`. **Nothing in it is
   built** and its status line says so. What to know before touching Phase 3:
   - **⭐ §3.2 (D174): today's behaviour is the NOVICE FLOOR.** **Nobody is ever worse than
     today**, mastery is headroom above. **The derivation is a survival floor (§2.2) and the
     novice is that floor**, so no derived number moves.
   - **⭐ Mastery is twenty years on the task**, narrated in the village log when it happens —
     Joe asked for that line by name, and it is the first thing here the player will feel.
   - **⭐ §3.2c + §3.5 (D175): the founders arrive as a MIX OF TIERS, and every villager gets a
     SEEDED RHYTHM at birth.** Together they **close D28 at the opening** instead of a century
     later, and they make the four founders people rather than units.
   - **⛔ THE TRAP IN IT:** §3.2 says the *floor* does not move; **it does not say the founding
     does not move.** A party with a master in it starts **above** the floor, so `cold-start.md`
     is re-measured and the goldens move once. **The byte-identical guard belongs to a *posed
     all-novice village*** — a guard aimed at the real opening will fail, and the temptation will
     be to weaken it rather than pose it properly.
   - **⚠️ Unmeasured**: does a master gatherer make the opening trivial? Probe first.
   - **✅ §5.4 (D176): WHAT CAN ACTUALLY BE LOST.** *Mastery* was one word doing two jobs. Split
     into **proficiency** (one person's years — dies with them, always, never writable),
     **technique** (the village's, re-locks exactly as `tech-tree.md` already said) and **a
     record of achievement** (permanent, **grants nothing**), the question dissolves.
     **Mastery-the-tier is not a node and cannot be taken from the village.**
   - **✅ MASTERY BITES IN PHASE 3, GATED BY NOTHING (D177).** A later node may raise the ceiling
     further; none permits it. **That is what keeps Phase 3 from shipping D56's shape** — a
     system that accrues, is visible and changes nothing.
   - **⭐ PHASE 3 LANDS IN THREE PIECES AND ONLY THE FIRST IS A NO-OP** (§11): the **substrate**
     (goldens unmoved), then **mastery biting** (moves them), then **the mixed founding and the
     seeded rhythm together** (moves them once more). *Landing them apart is what makes a
     regression attributable.*
   - **✅ Milestones are LOG LINES in Phase 3** (D177) and gain their collections home in the
     town hall in Phase 4. **There is no milestones panel.**
   - **§6 is a contract** with `tech-tree.md`, whose header now points back at it, and §6.6–6.7
     are guarantees Phase 4 may rely on: **no record ever restores proficiency**, and **no
     knowledge state may gate mastery**.
   - **⚠️ What is left in §12 is tuning, not design**: the width between novice and master, the
     founding party's composition, the tier names, and whether skill scales yield as well as
     duration. **Every one wants a probe before an implementation.**
3. ✅ **Per-site yield — DONE and merged** (D58, D178; `specs/per-site-yield.md`).
   - **⛔ Half of it had already shipped under other names, which is why it was smaller than
     §4 claimed.** The 7-tile bound stopped being a fence in **D120**; gathering has had per-site
     yield since **D112**. What was missing was **the farm**, and D58's second half — *distant
     sites pay better*, which nothing rewarded.
   - **Soil is regional now** (value noise, lattice 8) **and the farm reads it.** The sowing cap
     asks each farm's own haul. A farm ten ticks out went **46% → 96% brought in**, and still
     reaps **59 tiles against a near farm's 144** — the rot is gone *and* distance still costs.
   - **⛔ `crop_yield_per_tile` and `farm_store_cap` are untouched and stay locked.** Soil is a
     multiplier around `ReferenceSoil`, so **average ground yields exactly what it always did** —
     the locked 67 now means *the yield on average ground*.
   - **⚠️ The player can see the ground** — the **Ground: off** button on the control bar. Without
     it the whole slice is an invisible multiplier (§1.1, D67).
4. 🔨 **Phase 3 — skill and apprenticeship** (§2.1), the real answer to the mid-game gap (D161).
   **Landing 1 is built** (D181). Its success test is unchanged and unmet: *play years 1–16 at
   normal speed, without fast-forwarding, and want to keep watching.* What a new session needs:
   - **⛔ THE SPEC'S OWN DoD CONTAINED AN IMPOSSIBLE GUARD AND IT HAS BEEN CORRECTED IN PLACE.**
     §11.2.1 required landing 1 to be a *"provable no-op: goldens unmoved"*. **The goldens are
     full state hashes and proficiency is hashed state that grows from tick one — mutually
     exclusive.** The spec had reasoned by analogy from `crops-and-orchards.md`, where the map
     golden genuinely held because *the generator never produces the new terrain values*.
     **`StateHash.ComputeIgnoringSkills` is what shipped instead**, and it is byte-identical to
     all three goldens' pre-slice values. ⭐ **In landing 2 it must MOVE** — that is the
     anti-vacuity guard for mastery actually biting.
   - **⭐ A tick counts while somebody HOLDS the trade, not only while mid-action** (§3.6). The
     tight reading is tempting and §3.3b's arithmetic rules it out, *and* it would make a master
     accrue more slowly the better they got once landing 2 shortens the action.
   - **⭐⭐ SKILL GIVES AND NEVER TAKES (D183, Joe: *"let's give to the player, not punish or
     decay"*).** Decay was built, measured and deleted inside one phase — it took **37% of
     everything one forager earned**, so she held foraging longer than mastery requires and
     never mastered it, which is the trap §3.4 itself forbids. **§3.4's premise — *"a
     fifty-year-old who did six jobs is a master of six"* — is arithmetically impossible**, and
     that is what licensed deleting it. ⚠️ **Two wrong causes were published before the right
     one** (D182): a winter *headcount* read as *availability*, and a derivation that measured
     *demand*. **A tick out on the job is worth 1.5 of a tick waiting for one**, and the walk
     counts as work. **Ages at mastery: 34–55, median 39** — §3.3b's promise, untuned.
   - **⚠️ The reshuffle leaves the whole village jobless for exactly one tick** (Day 1, Spring).
     Harmless at 0.02%, but it is why landing 1's guards sample mid-season — see the trap list.
   - **⚠️ THE SIM CAN ONLY EXPRESS TWO WORKING SPEEDS** (D187), because `sow_ticks` and
     `reap_ticks` are 3 and `cut_ticks`/`split_ticks` are 4. **Four tier names sit over two
     behaviours** — an apprentice works exactly as fast as a novice, and a journeyman past the
     step exactly as fast as a master. Joe accepted this knowingly. It only becomes four if the
     durations grow.
   - **⭐ WHAT TO DO NEXT: apprenticeship (§5), and nothing else is outstanding.** The at-risk
     line (§7, DoD item 7) landed in **D195**, and the order was reversed on Joe's call because
     the probe showed the two are one loop — the warning is what tells the player to staff the
     second hand that makes teaching possible at all. **Joe's three calls for the slice are made:
     teaching is free, there is no policy dial, automatic only.** See "Where things are" above.
5. 🔨 **PHASE 4 — THE TECH TREE (§2.7) AND THE TOWN HALL (D176). THIS IS WHAT IS NEXT.**
   - **⭐⭐ JOE'S LIBRARY MODEL IS ALREADY RECORDED AND IT IS CONCRETE (D196).** A master
     woodcutter works out *"splitting lumber in a way that gives more cords — +15% firewood per
     log, +5% mastery"*; **the technique enters the library's records when he reaches mastery**;
     when he dies **his proficiency dies with him** but the technique does not, and **the next
     woodcutter spends idle time in the library learning it.** Where a trade has more than one
     worker the master also passes it to his apprentice directly.
   - **⭐ IT LANDS EXACTLY ON D176's SPLIT WITHOUT HAVING BEEN ASKED TO**, which is the strongest
     sign that split was right: **technique** is the village's and writable, **proficiency** is
     one person's and never writable. **The anti-ratchet holds** — `tech-tree.md §3a`'s *"a record
     preserves the method, not the proficiency"* is what stops §2.3's dead late game.
   - ⚠️ **The one part to measure before it ships:** a technique granting *"+5% mastery gain"* is
     **a soft ratchet on proficiency itself**, one level up. Bounded and probably fine, but it is
     the only piece of the model that touches the rule rather than sitting beside it.
   - **⭐ AND IT IS THE ANSWER TO APPRENTICESHIP'S HOLE**: one-seat trades have nobody to learn
     from, so the library is what carries their knowledge **across a gap in people** where
     apprenticeship carries it **between** people.
   - ⛔ **The list of techniques is deliberately NOT invented yet** (Joe: *"we don't have to come
     up with the full list… eventually they will all have a number of them"*) — `tech-tree.md
     §12`'s refusal of false precision.
   - ⚠️ **WRITE PHASE 4 A QA CHECKLIST.** Phase 3 shipped without one and its walk was waived
     (D203); **that debt should not compound.**
6. **Also on the board, unscheduled**, all recorded with Joe's rulings: **nomads and the
   dead-village revival** (§5, and it needs **building decay**, which reopens D65's *"repair after
   damage, no decay on a timer"*); **house upgrades and the 60–80 firewood target** (§5 — ⚠️ a
   6–8× change to a derived burn, **not a dial**); **foods with different nutritional values**;
   and the **steading slice**, still unmerged on `slice/work-from-the-steading`.

---

## ✅ THE FARM IS UNPARKED (D194) — and here is what is settled, so nobody re-opens it

**The ledger the section below asked for was built, and it answered in one sitting.** Kept as
`FarmLedgerTests` so the numbers can be **re-taken rather than trusted**.

**⛔⛔ THE CAP WAS SELF-FULFILLING, AND `ReapableShareAt` IS DIMENSIONALLY WRONG.** It scaled a
farm's field by `budgeted ÷ haul` — `budgeted` is a **round trip inside the field** (4 ticks),
`haul` is a **one-way walk to a store** (10). *The ratio is not a share of anything.* Measured,
one hand, ten years, committed ground posed at each level:

| farm → store | the cap sowed | what it can actually bring in | autumn spent **idle** at the cap |
|---|---|---|---|
| 10 ticks | 5 | **6** | **27%** |
| 16 ticks | 3 | **5** | **45%** |
| 22 ticks | 2 | **4** | **55%** |

**The cap cut the field, the farmer then had nothing to do, and the idleness read back as proof
the field had been too big.** After: **72 tiles reaped against 51 at ten ticks, idleness 6%.**

**⛔⛔ AND THE THING TO CARRY FORWARD: THIRTEEN TILES TEN TICKS OUT IS PHYSICALLY IMPOSSIBLE.**
Autumn is **120 ticks**; thirteen tiles at that distance needs about **230**. Joe's farmer was
short of **one or two** tiles, not eight. **The lever for thirteen is the walk** — the same
farmer beside a granary commits the whole field. §4.3's placement warning and the farm's own
panel now both say so.

**The fix is memory, not a better formula, and "no formula fits" is a finding.** The true ceiling
depends on the market's drain rate, the painted ground's shape, the granary's fullness and the
hands that turned up; `season ÷ (reap + walk)` wants a different constant at every distance,
moving the *wrong way* with distance. **A farm sows what it has already brought in** — a
high-water mark, per hand, clamped to `FieldTilesOneFarmerKeeps`, re-reckoned when the walk
changes. It converges on **6, 5 and 4** without being told them.

**⛔ CAUSES NOW DEAD — five proposed, all rejected by measurement. Do not add a sixth by
reasoning.**

| proposed cause | what killed it |
|---|---|
| the granary haul | removing it entirely still left the farm at ~7 tiles |
| the daily commute | travel is **11%** of a farmhand's ticks |
| resting outdoors getting cold | farmhands' cold is **zero, always** |
| the buffer (`farm_store_cap`) | raising it gave 13 tiles and **52% brought in** — the rot came back |
| **the buffer, again (D194)** | an **8.7× buffer** moved the ceiling from **6 tiles to 6** at ten ticks and **5 to 5** at sixteen. It still took only **23 of 72 loads** — it fills once and the market cannot keep it drained. **Two independent measurements now.** |

⚠️ **`crop_yield_per_tile` is NOT the lever** and Joe proposed it: raising it would inflate a
derived number to paper over a bug and leave well-sited farms at ~2.5× gathering.

**Still open:** the steading slice (farmhands staying at the farm through the working seasons) is
committed but **unmerged** on `slice/work-from-the-steading` — an economic no-op that costs ~13%
of the harvest, kept for the look, and its cost is still unexplained.

**Two directions Joe set, neither scheduled, both in `DESIGN.md §4`:** **gridless** — the largest
architectural statement anybody has made about this project, and the first question when it is
taken is whether the *sim* goes continuous or only the *presentation* — and **mods that can add
anything** (`BuildingKind`, `JobKind`, `Goods` and `Terrain` are four C# enums hashed by
position; `crops-and-orchards.md §4` is the template for doing it right). Standing discipline for
the second: **when you add a new kind of thing, ask whether it wants to be an enum value or a
data row.**

---

## ⛔⭐⭐ THE TRAP THAT WILL NOT ANNOUNCE ITSELF — read this before building roads

**The travel-cost field is a breadth-first sweep since D179**, and that is correct **only while
every passable tile costs the same to cross.** It replaced an O(n²) Dijkstra that was costing
**four seconds per world** and very nearly the entire test suite.

**§2.6 desire-path roads say crossing thresholds *"lowers pathfinding cost, creating a
reinforcement loop."*** The day a worn path is cheaper than grass, **BFS silently returns wrong
answers** — it keeps the first route it finds rather than the cheapest, and nothing throws.

- **Then, and only then, go back to a priority queue** — `PriorityQueue<int, long>` keyed on
  `((long)cost << 20) | index`, which keeps the tie-break and stays O(E log V).
  **Never back to the scan.**
- ⚠️ **No guard in the suite would catch it.** Every one describes a valley where the uniform-cost
  rule still holds. The symptom would be villagers taking scenic routes for a phase.

Written in three places on purpose: here, `TerrainCostField` itself, and
`pathfinding-and-water.md`'s header.

---

## Tools this project has that you would not guess

- **`BCLONE_PROBE_WIDTHS`** (METHODOLOGY §6). Walks the control tree headless in two seconds and
  prints what every panel and inspector row claims as a **minimum width**, including the rows
  posed with their worst-case sentence. **A column is never narrower than its widest child**, so
  every width in `Main.BuildUi` is a *request* — three sessions have asked this question and two
  hand-rolled the same throwaway before it was kept.
- **`grep "food from the field"`** — `HaulTheHarvest` writes its reason — free space, both costs, which store won — so *"why did
  the farmer walk past the buffer?"* is one grep rather than an afternoon:
  `grep "food from the field" src/Bclone.Game/logs/<newest>.log`
- **The audit trail** at `src/Bclone.Game/logs/`. Almost every bug that mattered came out of it.

---

## Traps, in the order they will cost you

- **⭐⭐ CHECK EVERY GUARD RED, AND COUNT THE REDS.** Repeatedly this has caught a guard that
  proved nothing. **And the guard that catches a bug is often not the obvious one** — *"the
  farm's sentence says farmer"* passes against a generic template with the farm's name in it;
  the one that works reads both sentences with the names masked out and requires them to differ.
- **⭐⭐ A SPEC CAN ASK FOR A GUARD THAT CANNOT EXIST, AND THE DoD IS WHERE IT HIDES (D181).**
  `skills-catalog.md §11.2.1` required *"provable no-op: goldens unmoved"* for a slice whose
  entire content is **new hashed state that grows from tick one.** It was reasoned by analogy
  from a slice where the analogy held, it sat in a Definition of Done for a week, and it would
  have been "met" by quietly not hashing proficiency — which would have cost the determinism
  guarantee and moved the goldens twice later instead of once. **Ask what a DoD item would look
  like if it were satisfied *before* you try to satisfy it.** The fix was to restate the claim in
  a vocabulary that can be true (*nothing anybody DOES changed*), not to weaken the guard.
- **⭐⭐⭐ AND THE BREAK THAT TURNS UP *NOTHING* IS THE ONE THAT CHANGES THE DESIGN (D194).** Two
  drafts of the farm's memory had it commit `learned + 1` a year and latch once a tile rotted.
  **Deleting both turned no guard red** — settled memory and tiles reaped identical at all three
  distances. The mechanism was redundant because `HarvestOneFarmCanBringIn` multiplies by the
  hands standing in the field *at that moment*, so **a farm with two hands in spring and one by
  autumn already over-commits on its own.** *The village probes without being asked.* The probe
  was **deleted rather than guarded** — a fifth invisible no-op after D56, D177 and D187. **Zero
  reds is a result, not a formality passed.**
- **⭐⭐ BREAKING YOUR OWN GUARDS FINDS THE BLIND ONES — DO IT, AND EXPECT A SURPRISE (D181).**
  Nine reds across seven deliberate breaks, and break #2 turned a guard red **for a reason
  unrelated to what it tested**: `LeavingATradeStopsTheClockOnItThatTick` sampled on a year edge,
  so *"the number did not move"* was two effects cancelling — no growth, and no decay only
  because the floor happened to protect a first-year worker. **The red check is not a formality;
  it is the only thing that reads your fixture for you.**
- **⚠️ THE VILLAGE IS BRIEFLY JOBLESS ON THE YEAR EDGE, AND IT WILL BAFFLE YOU (D181).** At
  *Day 1, Spring* the reshuffle has torn every allocation down and not yet rebuilt it: **0 of 4
  able adults hold a job on that exact tick.** Any guard that samples "who is working?" at
  `TicksPerYear * n` is sampling that hole. **Step half a season in.** (Winter is the other one:
  D44 unstaffs seasonal trades, so mid-winter is 1 of 4.)
- **⭐⭐⭐ AND SOME STATE CANNOT BE POSED AT ALL, BECAUSE IT IS DERIVED — TWO REDS TO FIND (D195).**
  An elder cannot be posed. Writing `LifeStage` lasts **one tick** (`AgeingSystem` recomputes it
  from vigour); writing `AgeYears` lasts **one tick** (`ClockSystem` recomputes it as
  `year - BirthYear`) — the guard **watched a 51-year-old turn 21** between the first tick and the
  second and read the resulting silence as a broken feature. `BirthYear` is `init`-only, which was
  the model saying so all along. **The honest fixture steps the sim until somebody genuinely grows
  old**, and it is barely slower. *Before posing a value, ask whether anything recomputes it.*
- **⭐⭐ AND A *FIXTURE* CAN FIGHT THE MECHANISM IT IS TESTING (D194).** Three guards for the
  farm's memory posed *"a clean autumn"* as **one sown tile** — so the farm brought in one tile,
  correctly recorded that one tile was what it had managed, and **the guards failed for the
  feature working.** The memory is a high-water mark, so a posed field *smaller* than the
  building's own commitment is a **worse** year, not an easier one. **Ask what your pose means to
  the system, not just what it means to you.**
- **⭐⭐ A GUARD CAN BE GREEN AND BLIND.** `AFarmBringsInMostOfWhatItSows` reports 93% while the
  played village was at 46%, and it is not wrong — it sites its farm a step from the stores.
  **Unmoved because it does not cover the case** (D157, three times now). Ask what a guard's
  fixture *makes impossible* before trusting its number.
- **⭐⭐ THE INSTRUMENT IS AS LIKELY TO BE WRONG AS THE CODE.** In one session: a probe reported a
  farm reaping 60 of 60 tiles because its *"reaped"* column counted winter rot as harvest, and a
  guard was written claiming an untested happy path when the guard for it was ten lines above.
- **⭐⭐ THE SIM'S AUDIT TRAIL IS EVIDENCE ABOUT THE SIM AND SAYS NOTHING ABOUT THE VIEW.** Two
  sessions hunted a rendering bug in `BehaviorSystem`. **Ask which half the symptom lives in
  before opening the log.**
- **⭐⭐⭐ THE INSTRUMENT WAS WRONG TWICE IN ONE SESSION, AND BOTH TIMES IT NEARLY CHANGED A LOCKED
  NUMBER (D189).** *"Gathering brings in five times what farming does"* came from a probe counting
  food into the **farm's own store** — which a reaper hauling to the granary never touches.
  **Counting reaps instead flipped the answer to "farming wins by 28%".** The wrong number would
  have justified raising `crop_yield_per_tile`, which is derived and locked. **Before a
  measurement justifies a change, ask what the instrument cannot see.**
- **⭐⭐ A DERIVATION THAT AVOIDS STATING A NUMBER STILL STATES ONE (D192).** The thaw rate was
  *derived* by mirroring the outdoor rate, on the explicit grounds that mirroring *"needs no
  number of its own"* — true, and it quietly chose **fifteen days to thaw**, half a winter, which
  nobody noticed until Joe played it. **Check what a derivation came out as, not just that it is
  principled.**
- **⭐⭐ A SMALL-RANGE RNG DRAW AT A FIXED STRIDE CORRELATES, AND THE FOUNDING IS FOUR SUCH DRAWS
  (D190).** Both founding pairs drew the **same** personal rhythm — 1, 1, 2, 2 — so the fix for
  D28 did nothing. **The RNG is not at fault:** forty raw `NextInt(0, 4)` draws come out 9/11/8/12.
  It is the *stride* at the start of the stream. **A generator can be sound and still be the wrong
  tool for four draws that must differ from each other** — deal or rotate, do not draw.
- **⚠️ HUNGER IS A PURE FUNCTION OF TICKS SINCE THE LAST MEAL (D190).** Two villagers who eat on
  the same tick stay in step for ever, **however differently they walk** — so a stagger that
  offsets only movement leaves *identical hunger at 100%*. Anything meant to desynchronise people
  has to touch the hunger clock too.
- **⭐ FINDING A CAUSE IS NOT FINDING THE CAUSE** (D163, D166, D169 — three rounds on one symptom).
  - **⛔⛔ AND THE FOURTH ROUND PUT TWO WRONG CAUSES INTO DOCUMENTS BEFORE THE RIGHT ONE (D182).**
    *Why does a forager take 32 calendar years to reach 20 years on the task?* **Wrong once:**
    *"winter stands the work down"* — the evidence was **1 of 4 able adults hold a job in
    mid-winter**, a **headcount**, read as **availability**. Foraging is worked in all four
    seasons; there are just fewer people on it. **A number that is true can still be evidence
    for the wrong claim.** **Wrong twice:** *"derive each trade's mastery from the share of a
    year it is staffed"* — that measures **demand**, which is the player's business, and would
    have pinned woodcutting at five years because this village wants one occasionally.
    **Right:** decay, taking **37% of everything a career earns.**
  - **⭐ The thing that caught both was building the measurement needed to ACT on the claim.**
    The first survived a probe because the probe answered a different question; the second died
    the moment its own numbers were printed next to what they implied. **If a finding is about to
    become a config number, measure the number — not the story.**
- **⭐ THE HELPER YOU NEED MAY ALREADY EXIST.** `Main.Wrapped` had been doing exactly the right
  thing on five labels for two UI rebuilds while every sentence in the inspector went into a bare
  `Label` in an `HBox`. Grep before writing.
- **⚠️ IF A NUMBER GOES INTO A DOCUMENT, IT COMES FROM A RUN.** Four for four, the fourth being a
  handoff's own warning about it.
- **⚠️ CHECK A DOCUMENT'S REFERENCES AGAINST THE THING.** Every file said "PR #3" for a day.
- **⭐⭐ MEASURE THE TOOLING TOO, NOT JUST THE VILLAGE (D179).** The suite ran nineteen minutes and
  the obvious fix — tag the long acceptance runs as slow — was **wrong**: it was already 9.5×
  parallel, so throughput was never the cost. The real culprit was an **O(n²) Dijkstra nobody had
  ever timed**, four seconds a world. **It is 2m30s now.** *The thing everybody suspects is not
  the thing costing the time — and Joe had to say "measure it first" to stop the wrong fix.*
- **⚠️ A FULL RUN IS FOR A VERDICT, NOT FOR DISCOVERY.** One slice here burned four full runs,
  twice to learn what was already knowable. **Use `--filter` while iterating.**
- **⛔⛔ DO NOT WRITE A WAIT-LOOP FOR A RUN THAT IS ALREADY IN THE BACKGROUND.** It is redundant —
  **the completion notification arrives by itself** — and it adds a failure mode that has now
  cost this project two sessions.
  - **2026-08-16:** two shells spun **thirteen hours** waiting for `Passed!` against a file
    written with `--logger "console;verbosity=detailed"`, which ends `Test Run Successful.`
    instead. *The two output formats end with different strings.*
  - **2026-08-22:** two more spun **an hour and three quarters** waiting for `Passed!` against a
    file that was the output of `dotnet test | grep … | head -30` — **the summary line had been
    filtered out before it ever reached the file.** *Grepping a file for a line you already
    grepped away.*
  - **The rule: a wait-loop whose condition cannot be met is a vacuous guard that costs wall
    time instead of passing silently.** If you truly must poll, poll for something the file is
    *guaranteed* to contain — and prefer just waiting for the notification.
- **⛔⭐ AND DO NOT REWRITE THIS TRAP LIST FROM SCRATCH — CARRY IT FORWARD.** The warning directly
  above was written on 2026-08-16 by the session that lost thirteen hours to it. **I deleted it
  on 2026-08-22 while tidying the handoff after the Phase 2 merge, and walked into the identical
  trap ninety minutes later.** *A handoff rewritten wholesale silently drops exactly the
  hard-won warnings it exists to carry* — which is D159's drift running the other way: the
  document losing knowledge the code never had. **Edit this file; do not replace it.**
- **⭐ AND WHEN A SPEC AND A MEASUREMENT DISAGREE, THE SPEC IS THE ONE THAT IS WRONG.** D178 wrote
  a soil algorithm into a spec, probed it, and found it made the number it existed to raise
  *worse* — and separately inferred the founding ground was "already ordinary" from a fact about
  draw order that turned out to imply the opposite. **Both were caught by ten-minute probes.**
- **`python` is not on PATH**, and string edits die on this repo's CRLF and its emoji. Use
  `perl -0777`, or the Edit/Write tools for anything with quoting in it.
- **⚠️ Goldens go last, one commit, one stated reason** (D152). The seam golden moves when a
  village that farms changes; the two fifty-year goldens do not, because **neither village ever
  places a farmhouse** — silent about what they do not reach, loud about what they do.

---

## Working with Joe

Technical, not a game or systems programmer. Casual, direct; **push back honestly**. **End every
message with the explicit ask**, or he cannot tell who is blocking whom. **His play is the best
bug-finder this project has** — and the clearest case is the jitter: he reported one symptom
three times across three sessions, and it took all three to get past the two real bugs it was
hiding to the one actually on his screen.
