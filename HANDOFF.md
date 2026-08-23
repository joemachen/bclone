# Handoff — bclone: **Phase 3 has started. Landing 1 of 3 is built; landing 2 is next and is not optional.**

Read `CLAUDE.md`, then **`DESIGN.md` §0–§5 in full, §6, and §7 from D181 back to D142**, then
`METHODOLOGY.md`.

> **⛔ WHEN YOU HAND OFF: EDIT THIS FILE, DO NOT REPLACE IT.** The trap list at the bottom is
> accumulated from sessions that each paid for one entry. Rewriting it wholesale drops them
> silently — that happened on 2026-08-22 and cost an hour and three quarters within the same
> session. **Rewrite "where things are"; carry the traps forward.**

⭐ **The two things to know before you touch anything:** **proficiency exists now** (D181 — it
accrues, is hashed, is visible, and **nothing reads it**), and **the ground the player can read**
(D180 — a toggle that lied for a day, plus the sentences D178's spec asked for and never got).
Behind those: the **town hall is designed** (D176), **ground is worth going to** (D178), and **the
suite runs in two and a half minutes instead of nineteen** (D179).

---

## Where things are

**Phase 2 is merged; Phase 3 is in progress on `phase/3-skill-and-apprenticeship`.** Phase 2 went
in via [PR #4](https://github.com/joemachen/bclone/pull/4) — 248 commits, all five
Definition-of-Done items met, the last being Joe's QA walk. Per-site yield (D178), the cost-field
rewrite (D179) and the ground-legibility slice (D180) are all fast-forwarded onto `main`.

**⭐⭐ PHASE 3 LANDS IN THREE PIECES AND ONLY THE FIRST IS BUILT** (`skills-catalog.md §11`):
1. ✅ **The proficiency substrate** (D181). `Villager.Skills` accrues time on the task, hashed
   sparsely in id order; six skills are **rows in config, not enum values**; the villager panel
   says *"Nineteen years in the fields"*; **the mastery line fires** in the village log.
2. **⛔ NEXT, AND NOT OPTIONAL: mastery bites** — duration first, yield second (§3.3). **A system
   that accrues, is visible and changes nothing is D56's clothing**, which was measured as a
   no-op over 300 years and blocked for it. Landing 1 is that shape until landing 2 lands.
3. **The mixed founding (§3.2c) and the seeded rhythm (§3.5), together in one commit.** This is
   what discharges D28.

⚠️ **The phase PR is #4, not #3.** Number 3 went to the closed screenshot-hook PR D160 rescued,
and every document in the repo said #3 for a day before anyone checked.

**Merged slice branches are deleted on Joe's standing preference**, each after checking it had
**0 commits not on `main`**. Tips if ever wanted back: `phase/2-wood-fuel-and-tools` `9b9f410`,
`slice/per-site-yield` `b2cb718`, `slice/faster-cost-field` `daec8fd`.

**SUITE, FROM A RUN:**

```
656 passed, 0 failed, 2 skipped of 658 — about 2m00s (was 18m52s before D179)
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
   - **⚠️⚠️ MEASURED, AND LANDING 2 HAS TO ANSWER TO IT: twenty years on the task is 32–34
     calendar years for seasonal trades.** D44 unstaffs them in winter — **1 of 4 able adults
     hold a job in mid-winter against 4 of 4 in summer** — so a farmer masters on schedule and a
     forager takes half again as long, with nothing on screen saying why. **Three possible
     shapes are written up in §12 and none should be taken without Joe.**
   - **⚠️ The reshuffle leaves the whole village jobless for exactly one tick** (Day 1, Spring).
     Harmless at 0.02%, but it is why landing 1's guards sample mid-season — see the trap list.
   - **What is still unbuilt:** apprenticeship and teaching (§5), the at-risk line (§7), the
     workplace panel's *"how practised are they"* (deliberately landing 2's — until skill bites,
     a hut is never slow *for that reason*), and everything in §12.
5. **Phase 4 — the tech tree** (§2.7), plus the **town hall** (D176).

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
- **⭐ FINDING A CAUSE IS NOT FINDING THE CAUSE** (D163, D166, D169 — three rounds on one symptom).
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
