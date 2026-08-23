# Handoff — bclone: **Queue items 1–3 are all done and merged. Next is Phase 3.**

Read `CLAUDE.md`, then **`DESIGN.md` §0–§5 in full, §6, and §7 from D179 back to D142**, then
`METHODOLOGY.md`.

⭐ **Three things landed since Phase 2 and each is worth knowing before you touch anything:**
the **town hall is designed** (D176 — it gates the knowledge *screen*, not the tree); **ground is
worth going to** (D178 — soil is regional and the farm reads it); and **the suite runs in two and
a half minutes instead of nineteen** (D179).

---

## Where things are

**On `main`, and everything is merged.** Phase 2 went in via
[PR #4](https://github.com/joemachen/bclone/pull/4) — 248 commits, all five Definition-of-Done
items met, the last being Joe's QA walk. Per-site yield (D178) and the cost-field rewrite (D179)
followed as two slices, fast-forwarded onto `main`.

⚠️ **The phase PR is #4, not #3.** Number 3 went to the closed screenshot-hook PR D160 rescued,
and every document in the repo said #3 for a day before anyone checked.

**Merged slice branches are deleted on Joe's standing preference**, each after checking it had
**0 commits not on `main`**. Tips if ever wanted back: `phase/2-wood-fuel-and-tools` `9b9f410`,
`slice/per-site-yield` `b2cb718`, `slice/faster-cost-field` `daec8fd`.

**SUITE, FROM A RUN:**

```
641 passed, 0 failed, 2 skipped of 643 — about 2m30s (was 18m52s before D179)
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
4. **⭐ NEXT: Phase 3 — skill and apprenticeship** (§2.1), which is also the real answer to the
   mid-game gap (D161). **Its spec is written and every design question in it is answered** — what
   remains is tuning, and all of it wants a probe first. Its success test is already written:
   *play years 1–16 at normal speed, without fast-forwarding, and want to keep watching.*
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
