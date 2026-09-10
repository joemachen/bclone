# CLAUDE.md — Working Agreement

You are helping build a ground-up, generational village-builder / survival sim (a spiritual successor to *Banished*). This file is your standing instructions for **how** to work. The **what** lives in `DESIGN.md`; the engineering **standards** live in `METHODOLOGY.md`.

## Before anything
1. **Read `DESIGN.md` §0–§5 in full at the start of every session** — what this is, the
   Non-Negotiables, the pillars, the architecture, the build order, the open decisions. That is
   the design, and it is short enough to actually read.
   - **Then §6 (Progress Tracker)** for where the project is, and **the most recent ~20 entries
     of §7 (Decisions Log)** for how it got there lately.
   - **⚠️ §7 is 150+ entries of append-only history and is meant to be *searched*, not read front
     to back** (D159 — the old instruction said "in full", and the doc outgrew it months ago).
     When you touch a system, grep §7 for it: the reasoning behind almost every number in this
     game is in there, and re-deriving it from scratch is how a decision gets quietly reversed.
2. **Read `METHODOLOGY.md`** — it governs phasing, spec-first work, testing/QA, error logging, versioning, and CI. Follow it.
3. **`§4` is the live roadmap and `§6` is the live status.** If they disagree, that is a bug in
   the docs — say so. They disagreed for six weeks before D159 noticed.
4. For any non-trivial system, read (or write) its spec in `specs/` before coding — spec first, then tests, then implementation (METHODOLOGY §2).
   - **⚠️ Check the spec's status line against the suite, not just against itself.** Five specs
     claimed "not started" or "in progress" for systems that had shipped, one of them for the
     slice merged that morning (D159). A spec that lies about its own status is worse than no
     spec, because it is read at exactly the moment a session is trying to orient.

## The prime directive
The game has six **Non-Negotiables** (DESIGN.md §1) — legibility, meditative pace, no combat, people-not-spreadsheet stories, generational time as core loop, traceable-over-clever. **These are constraints on every feature.**

If a request — from Joe or your own inference — would violate a Non-Negotiable, **stop and flag it before implementing.** Example: implementing a Civ-style abstract-research-points tech tree would violate legibility/diegesis; the correct design is the knowledge-based tree in DESIGN.md §2.7. When in doubt, ask.

## How to build
- **Respect the build order (DESIGN.md §4).** Do NOT build pillars in parallel. Phase 0 is a single-villager vertical slice, and it must pass its success test before Phase 1 begins. Resist the urge to scaffold everything at once.
- **Determinism is architectural, not optional.** Fixed-timestep tick loop, sim fully decoupled from rendering, no wall-clock time in sim logic, seeded RNG only, no uncontrolled float nondeterminism in sim-critical paths. Write a determinism test early (same seed + same inputs ⇒ identical state) and keep it green.
- **Data-driven from day one.** No hardcoded content — buildings, resources, jobs, recipes, biomes, and tech nodes live in data files. Assume a modder will want to touch them.
- **One shared cost field** for pathfinding and labor catchment (DESIGN.md §2.6). Do not build two competing travel-cost systems.
- **Test sim logic** (METHODOLOGY §3). Sim code is pure and deterministic — prefer TDD: failing test from the spec, then implement. The determinism test stays green forever; a regression there is a P0 bug. No phase merges without its Definition of Done met.
- **Log richly** (METHODOLOGY §4). Structured, leveled, tick-stamped. Never swallow exceptions — catch, log with context, then handle or fail loudly.
- **Keep it legible in code, too.** Favor clear, inspectable systems over clever ones, matching the game's own philosophy. Small reviewable changes over giant diffs.
- **⛔ NOTHING DERIVABLE INCREMENTALLY MAY BE REBUILT PER TICK OR PER FRAME. Keep the index and
  maintain it where the state changes.** *Joe's rule, 2026-09-10.* This is the one that would have
  caught **every** performance regression this project has had, and each was found by accident:
  `Footprint.Covers` building the whole tile list to answer about one tile (**suite 4m55s → 9m**,
  D329); `IsHarvest` scanning 9,600 tiles inside a per-trip loop (**six minutes to eleven**, D179);
  the zone wash stepping every visible quarter-tile when a byte per tile already said whether there
  was anything there (**80% of the frame**, D338). ⭐ The shape to copy is already here three
  times: `ZoneMap._groundByOwner` / `_tilesByOwner`, `ZoneMap.Edits`, and `SimWorld.TerrainGeneration`
  — *a monotonic counter is the cheapest honest answer to "has this changed since I last
  looked?"* ⚠️ **A derived index is never hashed** (D335): it restates the state, it is not
  a second fact about the village. ⚠️ **This is not a zero-allocation rule and must not be
  read as one.** The sim ticks at `target_ticks_per_second: 0.75`; GC pressure has never been the
  problem here and pooled buffers shared across ticks are a determinism hazard, which is a far worse
  bug than the one they would prevent.

## Verification — before saying anything is done

⛔ **All four, every time. "The tests passed" is not a verdict on this project.**

```bash
dotnet test bclone.sln --nologo -v q          # the suite, including determinism
dotnet build src/Bclone.Game/Bclone.Game.csproj
BCLONE_PROBE_WIDTHS=1 "$GODOT" --headless --path src/Bclone.Game
grep -rn "[0-9]\{15,\}" tests/Bclone.Sim.Tests/*.cs   # the goldens, before and after
```

- ⛔ **`dotnet build bclone.sln` does NOT compile the game** (D11), and `dotnet test` already builds
  the solution — so the second line is the one that matters and it is not optional.
- ⚠️ **READ the view's build output; do not trust its silence.** `src/Bclone.Game` sets
  `TreatWarningsAsErrors=false` deliberately, because Godot's generators emit code nobody controls
  (METHODOLOGY §5a) — **it reports and does not fail.** A `CS0414` warned there for months unseen.
  Everything else in the repo fails the build on a warning, and `.editorconfig` promotes dead code
  (`IDE0051`, `IDE0052`, `IDE0005`, `IDE0060`) with it. *Do not restate those as prose rules; the
  build already enforces them. Do delete dead code outright rather than commenting it out.*
- ⛔ **Do not run `test.bat`.** It ends in `pause` and will block until the session times out. It is
  a convenience wrapper around the first line above.
- ⚠️ **The probe is the only view verification that exists** (D11, D160). Read all its lines —
  `bar height` must stay **161**, `tile centres` must stay ✅, and **if `done.` is missing a headless
  Godot is still running**: `taskkill //PID n //F`, then confirm with `tasklist`.
- **"No golden moved" is a `git diff`, not a passing suite.** A golden is a 19-to-20-digit number
  however it is spelled — `const`, `InlineData`, or an argument.
- ⛔ **Red-check every new guard, and count the reds** (D326). Verify the break landed where you
  aimed it — by line number, `grep -c` the changed text — before believing a green. **A guard that
  scores zero is kept and the zero is written down**, never quietly presented as evidence.

⚠️ **The suite's own wall-clock is an instrument.** It went 4m55s → 9m in one commit with everything
green (D329), and nothing but the clock reported it. If it moves a lot, find out why before
committing.

## Update protocol (do this — it's load-bearing)
- After each meaningful chunk of work, **update DESIGN.md §6 (Progress Tracker)**: move items between Done / In progress / Next up, and update the Current phase.
- When you resolve an Open Decision (§5) or make a significant architectural choice, **append a one-line entry to DESIGN.md §7 (Decisions Log)** with the rationale, so future sessions inherit the reasoning.
- If you discover a new pillar-level idea or a design tension, add it to DESIGN.md rather than only mentioning it in chat — chat is ephemeral, the doc is not.
- **⛔ DOCS MOVE IN THE SAME COMMIT AS THE CODE.** If a mechanic, a data structure or a JSON schema
  changes, `specs/`, `DESIGN.md` and `data/` change with it — not in a follow-up. **And a spec's
  status line must be true when you write it** (D159): five specs claimed *"not started"* for
  systems that had shipped, one of them for the slice merged that morning. *A spec that lies about
  its own status is worse than no spec, because it is read at the moment a session is orienting.*
- **⛔ LOG DEBT AND TRAPS IN `handoff.md` BEFORE FINISHING.** ⚠️ **But its trap list is NOT a to-do
  list and must never be "resolved" or rewritten.** Each entry is a lesson a session paid for;
  clearing them drops that knowledge silently, which happened on 2026-08-22 and cost an hour and
  three quarters. **Edit the file, carry the traps forward, add one for what this session learned.**
  The list that *is* actionable is the separate **⏸️ OPEN, AND JOE'S TO CALL** section — and it is
  his, not yours.

## Before large moves
- **The stack is settled** (D1): C# (.NET 8) + Godot 4, with the sim in a Godot-free class library. Anything that would couple `Bclone.Sim` to the engine is a design change, not a detail — raise it.
- Flag any architecture deviation from DESIGN.md §3 before committing to it.

## Working with Joe
- Joe is technical (marketing/data engineering background — GA4/BigQuery/GTM, works with developers) but is **not assumed to be a professional systems/game programmer.** Explain systems-level and language-specific choices when they matter; don't assume deep prior knowledge of the engine internals. Keep him in the loop on load-bearing decisions rather than burying them.
- Casual, grounded, direct tone is welcome. Push back honestly when a design choice is wrong — that's more useful than agreement.
