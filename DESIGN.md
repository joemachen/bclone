# DESIGN.md — Working Design Document

> This is the single source of truth for the project. Read it fully before writing code.
> It is a **living document**: update the Progress Tracker and Decisions Log as work proceeds.
> If a request conflicts with the Non-Negotiables below, **stop and flag it** rather than silently implementing it.

---

## 0. What this is

A generational village-builder / survival-sim — a ground-up spiritual successor to *Banished*. You start with a handful of exiled travelers and grow a town across generations, managing food, warmth, tools, labor, and knowledge against a world that pushes back. No combat. No traditional "win." The game is watching a lineage survive.

The design intent, in one line: **vanilla Banished tells resource stories ("the winter killed 34 people"); this game tells people stories ("old Mabel trained her granddaughter as herbalist before the fever took her, and that's the only reason the town survived the plague year").** Every system exists to serve that shift.

---

## 1. Non-Negotiables (the soul — do not break these)

These are constraints on *every* feature. If a proposed system violates one, it's the wrong design.

1. **Legibility above all.** The player must always be able to trace *why* something happened. When the town dies, they understand the causal chain. New systems must stay traceable — no hidden black-box outcomes.
2. **Meditative pace.** Slow, calm, low-micro. No twitch mechanics. Systems should reduce babysitting, not add it.
3. **No combat.** The pressure is environmental, systemic, and social — never military.
4. **Stories come from people, not spreadsheets.** Villagers are agents with histories, not fungible labor units. Emergent narrative is the goal.
5. **Generational time is the core loop**, not a side feature. Aging, inheritance, and knowledge transfer are load-bearing.
6. **Slow and traceable > clever and opaque.** When in doubt, choose the version the player can read off the screen.

---

## 2. Design Pillars (the systems)

Each pillar has a one-line thesis, the mechanic, and the failure mode to design against.

### 2.1 Villagers as agents (skill + knowledge transfer)
- **Thesis:** A villager is an agent with a growing, *transferable* skill — not a headcount.
- **Mechanic:** Skill grows with time-on-task. A farmer with 20 years in the fields is meaningfully better than a fresh laborer. Crucially, that skill **dies with the person unless an elder apprentices a youth.** Losing an old master is losing knowledge, not just labor.
- **Ties into:** the tech tree (2.7) — knowledge literally lives in people.
- **Failure mode:** punishing the player for losses they couldn't foresee. Knowledge-at-risk must be **visible and actionable** (see 2.7).

### 2.2 Smart labor (no manual building assignment)
- **Thesis:** Stop slotting N workers into a building and teleporting their brains there.
- **Mechanic:** Workplaces have a **labor demand** and a **catchment radius**. Villagers take jobs by proximity, skill, and household, driven by **policy** rather than per-building micromanagement.
- **Kills:** the villager who walks across the map for one log.
- **Distribution is a job, not a slider.** Moving goods around the village should itself be work someone does. A **market or food stall is manned** like any other workplace, and redistributes food and goods evenly across its catchment — so a well-placed, well-staffed market is what stops one household starving beside a full neighbour. Policy sliders for sharing are a placeholder; the diegetic version is a building with a person in it. (See Decisions Log D14.)
- **Failure mode:** opacity. The player must be able to inspect *why* a given villager took a given job.

### 2.3 Systemic escalating pressure (fix the dead late game)
- **Thesis:** A well-run town shouldn't plateau into "nothing left but RNG."
- **Mechanic:** Replace random events as the *primary* threat with **systemic pressures that scale with the player's own choices**: soil depletion over decades, resource radii exhausting and forcing expansion, climate drift (runs of brutal winters), disease that spreads along the social and trade network.
- **Note:** Random events (fire, etc.) can still exist as *flavor*, but are not the main source of difficulty.
- **Failure mode:** pressure that isn't traceable to a decision. Every escalating problem should be back-traceable to something the player did.

### 2.4 Living region (you are a node, not an island)
- **Thesis:** The trading post shouldn't be a vending machine with a boat.
- **Mechanic:** A small surrounding economy — a few neighboring settlements with their own stocks and needs, prices that move, rivers and roads as real logistics. Trade is a system, not a menu.
- **Failure mode:** complexity that outruns legibility. Keep the region small and readable.

### 2.5 Environment with teeth
- **Seeded map generation (planned).** The world should be *generated from a seed*, not hand-placed: terrain, the river, forest stands, forage sites, soil quality. Two consequences make this load-bearing rather than cosmetic. First, it is what makes a second playthrough a different place rather than the same place played again — which is the same argument §2.7 makes for a broad tech tree. Second, the sim is already fully seeded and deterministic, so map generation belongs to the *same* seed as everything else: quoting one number should reproduce the whole run, world included. Recorded as D18. Not built yet; the current map is a handful of hand-placed positions in config.
- **Thesis:** Seasons should have mechanical weight; terrain should matter.
- **Mechanic:** Biome variety; terrain dictates viability (river valley vs. highland vs. coast); seasons with real teeth, not just "it's colder now."
- **Failure mode:** difficulty that reads as arbitrary rather than environmental.

### 2.6 Desire-path roads
- **Thesis:** The player paves what the town already proved matters — they never place roads blind.
- **Mechanic:**
  - Every tile accumulates a **trample value** as agents walk it; the value **decays slowly** over time.
  - Crossing thresholds shifts the tile visually (grass → worn dirt → packed trail) and **lowers pathfinding cost**, creating a reinforcement loop.
  - Player upgrades an emerged path (dirt → gravel → cobblestone), spending resources to reinforce a route the sim has *proven* is high-traffic. Road-building becomes a **late optimization**, not an upfront chore.
  - Optional **auto-pave policy**: "auto-pave paths above X traffic," spending stockpiled stone. Gate behind a civic tier.
  - **Paving fossilizes the path:** trample value stops decaying and the pathing bonus locks in, so old roads keep pulling traffic even after the town's layout shifts. This is how an old town *feels* old — it carries the ghost of past decisions.
- **Failure modes:**
  - **Lock-in** (feedback too strong): the first route becomes a permanent superhighway even if it's dumb. Tune decay + discount; **cap the discount** so a much longer worn path never beats a short unworn one.
  - **No paths** (feedback too weak): faint everywhere, grooves nowhere. A lone forager shouldn't scar the map; daily housing↔granary churn should.
- **Critical integration:** desire paths and smart-labor catchment (2.2) **must read the same cost field**, or they'll fight. A warehouse near a worn superhighway effectively has a bigger catchment — that's a *feature*, but only if both systems share one source of truth for travel cost.

### 2.7 Knowledge-based tech tree
- **Thesis:** Progression, yes — but **not** a Civ-style menu of abstract research points. That's the exact menu-driven abstraction the rest of the design deletes. Unlocks must be diegetic and legible.
- **Three unlock mechanisms, all diegetic:**
  1. **Knowledge as currency (lives in people).** An advance is unlocked because *a person knows it.* Your master farmer develops crop rotation after 25 years. **If she dies without apprenticing someone, the node re-locks and the town can regress a tier.** The tech tree and the population pyramid become the same object.
  2. **Unlock by doing.** Practice-based advances: bake enough bread across enough winters → better oven; log a forest for two generations → managed forestry. Unlocks emerge from how the town lives, not from clicking ahead.
  3. **Civic gating by scale.** Some things (granary systems, stone infrastructure, the auto-pave policy) just require a settlement of a certain size/permanence. Population/longevity milestones unlock the *option*; knowledge or practice unlocks the *quality*.
- **Shape:** **broad, not tall.** Wide and branching with mutually-exclusive tradeoffs (intensive agriculture vs. forestry+trade develop down different branches). You can't have it all in one lifetime → a second playthrough is a genuinely different path, not the same climb faster. A linear climb to a final node re-creates the dead late game — avoid it.
- **Why it fits the soul:** a known technique is a **fragile inheritance** one plague year can erase — just as a paved road is a fossilized decision. Both are the town's memory made mechanical.
- **Failure mode:** re-locking on death feels unfair if unforeseeable. Apprenticeship state must be **visible and actionable** ("Mabel is 68 and the only soul who knows herbalism" = a surfaced, warned state, not a funeral surprise).

---

## 3. Architecture

- **ECS, data-oriented.** Thousands of agents ticking; lay out components for cache-friendly iteration, not a deep OOP hierarchy. This is not negotiable given the agent counts.
- **Deterministic, fixed-timestep tick loop, fully decoupled from rendering.** The renderer interpolates between ticks. Determinism buys reproducible debugging, clean saves/replays, and leaves the door open for co-op later.
  - No wall-clock time in sim logic. Seeded RNG only. No float nondeterminism in sim-critical paths (decide fixed-point vs. carefully-controlled float early — see Open Decisions).
- **Data-driven from day one.** Buildings, resources, jobs, recipes, biomes, tech nodes — all defined in **data files**, not hardcoded. 
- **First-class modding API from the start.** Banished is alive in 2026 *because of its mods.* Bake moddability in as a first principle; don't make the community reverse-engineer it. Content defined in data + documented hooks.
- **Single shared cost field** for pathfinding and labor catchment (see 2.6 integration note).

---

## 4. Build Order (do NOT build pillars in parallel)

The temptation is to build all seven pillars at once and drown. Don't. Build a spine, prove the feeling, then bolt systems onto it.

### Phase 0 — Vertical slice: one villager, one resource loop
The whole game, shrunk to a single soul. **Gather food → eat → survive a winter → age → die.** Deliver:
- Agent sim (one agent, needs, tasks)
- Deterministic fixed-timestep tick loop
- Season/time system
- A UI **legible enough to read why that one villager lived or died**

**Success test:** watching one villager live and die actually *means* something. If the spine doesn't give a chill with one villager, no amount of economy sim saves it. **Do not proceed to Phase 1 until this test passes.**

### Phase 1+ — bolt systems onto the spine (rough order, re-order as learning dictates)
1. Multiple agents + households + smart labor (2.2)
2. Environment/seasons depth + biomes (2.5)
3. Desire-path roads (2.6) — needs the shared cost field in place
4. Skill growth + apprenticeship/knowledge transfer (2.1)
5. Knowledge-based tech tree (2.7) — depends on 2.1
6. Systemic pressure: soil, resource exhaustion, climate drift, disease (2.3)
7. Living region + trade economy (2.4)

Each phase should ship in a playable, legible state before the next begins.

### v1 milestone (release)
`v1.0.0` is the first real public release — declared when the game is a coherent, stable, enjoyable whole (not tied to a specific pillar count; it's a judgment call made with Joe). At that point the release machinery activates: `CHANGELOG.md` gets stamped, the version source is bumped, and pushing a `vX.Y.Z` tag triggers the GitHub Actions build of a Windows `.exe` release. Process details live in `METHODOLOGY.md §5–6`. Nothing about releases needs to run before v1 — the workflow is tag-gated and dormant until then.

> **Process note:** engineering standards for every phase — spec-first, unit tests, the determinism test, QA/Definition of Done, and error logging — are defined in `METHODOLOGY.md`. Follow it alongside this build order.

---

## 5. Open Decisions (resolve early, record in Decisions Log when settled)

- [x] **Engine / language / tech stack.** ✅ Resolved 2026-07-25 → **C# (.NET 8) + Godot 4**, sim in a Godot-free class library. See Decisions Log D1.
- [x] **Determinism strategy for floats** ✅ Resolved 2026-07-25 → **integer-only sim state; fixed-point `Fixed` (Q32.32) introduced when a system genuinely needs fractional math.** See D2.
- [x] **Data file format** for content/modding ✅ Resolved 2026-07-25 → **JSON** via `System.Text.Json`, comments and trailing commas allowed. See D3.
- [ ] **Trample/decay tuning values** (thresholds, decay rate, discount cap) — will need iteration in Phase 3.

---

## 6. Progress Tracker

> Update this section as work proceeds. Keep it honest — it's how we both know where we are.

**Current phase:** **Phase 1 — multiple agents + households + smart labour (§2.2)**

**Phase 0: ✅ COMPLETE.** Success Test passed 2026-07-25 — the gate is cleared, so Phase 1 work may begin.

**Done:**
- Tech stack resolved: C# (.NET 8) + Godot 4, sim as a Godot-free class library (D1).
- Float-determinism strategy resolved: integer-only sim state (D2).
- Data format resolved: JSON with comments (D3).
- `specs/tick-loop.md` written (spec-first, per METHODOLOGY §2).
- Project scaffolded: `bclone.sln`, `src/Bclone.Sim`, `tests/Bclone.Sim.Tests`, `data/sim.config.json`, `VERSION`.
- **Deterministic fixed-timestep tick loop** — `SimLoop` (tick counts, never durations) + `FixedTimestepDriver` (owns the only clock read, integer-nanosecond accumulator).
- Determinism primitives: `DeterministicRandom` (PCG32, verified against the reference vector) and `StateHash` (FNV-1a).
- Minimal tick-stamped structured logger (METHODOLOGY §4).
- **Determinism test green** — plus anti-vacuity guards proving it can actually fail. 92 tests passing.
- Build-time determinism enforcement via banned-API analyzer (verified firing).
- CI (`ci.yml`) building + testing on every push; `release.yml` moved to `.github/workflows/` and filled in for Godot.

- **Phase 0 sim**: clock/seasons, hunger, foraging behaviour, starvation and old-age death, life log. 131 tests green.
- **Phase 0 view**: `src/Bclone.Game` Godot 4.7.1 shell — clock, villager state, hunger bar, stockpile, scrolling life log, speed controls. Verified running.
- Pacing resolved: a full life runs 9.2–11.6 minutes (see spec §11).

- **Ageing with mechanical weight** (D12): declining vigour scales foraging yield, so a life has a shape — easy middle years, a visibly tightening old age, then death. Resolved the flat-middle finding.
- **Phase 0 Success Test passed** (Joe, watching at 4×). All six Definition-of-Done items met; 148 tests green.
- Final pacing: one in-game year = 60 real seconds at 4×, lifespan 40–50 years. Speeds: pause / 1× / 2× / 4× / 10×.

**In progress:**
- **Phase 1 spec** (`specs/phase-1-households-and-labour.md`) — spec before code, per METHODOLOGY §2.

**Next up:**
- Phase 1 implementation: multiple villagers, households, and the smart-labour system (§2.2) — no manual building assignment, ever.
- Childhood and dependency, deferred from Phase 0, finally has somewhere to live.

---

## 7. Decisions Log

> Append-only. When an Open Decision is resolved or a significant architectural choice is made, record it here with a one-line rationale so future sessions inherit the reasoning.

- **D1 · 2026-07-25 · Tech stack = C# (.NET 8) + Godot 4**, with the sim in a standalone class library holding *zero* Godot references (`src/Bclone.Sim`) and Godot as a thin render/UI/input shell. Chosen over Rust + Bevy because Bevy's breaking-release cadence is a recurring migration tax on a codebase where a silent determinism regression is a P0 bug, Rust's learning curve is the main risk to solo momentum, and Godot's Control-node UI is materially better for the legibility deliverable — while the perf need (thousands of agents at a low fixed tick rate) sits comfortably inside C#'s reach. Keeping the sim engine-free is the hedge: it stays headlessly testable and the shell is replaceable.
- **D2 · 2026-07-25 · No floats in sim state.** Integer-only for now; a fixed-point `Fixed` (Q32.32) struct gets introduced at the first system that genuinely needs fractional math, not before. Resolves the float-determinism open decision. C# gives no compiler help here, so the rule is enforced by a banned-API analyzer plus review.
- **D3 · 2026-07-25 · Content/data format = JSON** via `System.Text.Json`, with comments and trailing commas enabled. Chosen for modder accessibility and diffability, and because it keeps `Bclone.Sim` free of any engine-specific resource format (Godot `.tres` would have coupled the sim to the shell).
- **D4 · 2026-07-25 · Playback speed scales tick *count*, never tick *size*.** Pause/1×/2×/4× change how many ticks run per real second; a tick is always identical. Scaling a delta into the sim would make each speed a different simulation and destroy reproducibility. Tested by `PlaybackSpeed_DoesNotAffectState`.
- **D5 · 2026-07-25 · System execution order is part of the determinism contract.** Systems run single-threaded in registration order, snapshotted at construction; reordering them is a behavioural change, not a tidy-up.
- **D6 · 2026-07-25 · The driver accumulates time in whole nanoseconds, not floats.** The natural `while (acc >= secondsPerTick) acc -= secondsPerTick;` loop loses ticks to binary rounding (2.5s at 10 ticks/s yields 24, not 25) and the error compounds every frame, so the game clock drifts behind real time. Found by the test suite during implementation; guarded by `WholeSecondDeltas_YieldExactTickCounts`.
- **D8 · 2026-07-25 · The life log is the `INFO` view of the sim log, not a separate system.** Same sink, same tick-stamping, same ordering — so the story the player reads and the log an engineer debugs from are one artifact, and they can never disagree.
- **D9 · 2026-07-25 · Individual gathers are `DEBUG`; the life log summarises per season.** A fifty-year life is ~600 foraging trips, and narrating each one buries the handful of lines that carry the story. "Spring of Year 3 — foraged 4 times" is a season; 600 receipts is a spreadsheet, which is the thing this game is defined against (§1.4).
- **D10 · 2026-07-25 · Eating preempts any action.** A round trip to the food source is longer than the gap between meals, so finish-your-action-first made the villager starve mid-gather beside a full store. A survival game may kill you for bad decisions, never for a scheduling artifact.
- **D11 · 2026-07-25 · The Godot project lives in `src/Bclone.Game`, not the repo root.** A root Godot project globs `**/*.cs` into one assembly, which would swallow `Bclone.Sim` and the xunit tests into the game build and destroy the engine-free separation D1 exists to protect.
- **D18 · 2026-07-26 · The map will be generated from the run's seed, and it is the same seed as the sim.** Terrain, water, forest stands, and forage sites get generated rather than hand-placed, so a second playthrough is a different *place*. Because the sim is already deterministic and seeded, map generation must draw from the same seeded RNG in a fixed order — then a single number reproduces an entire run including its world, which is what makes bug reports and shared seeds work. Positions currently live in config as literal coordinates; those become generator output. Not built yet.
- **D17 · 2026-07-26 · Wood serves all three purposes: winter fuel, building material, and tools.** Joe's call. Fuel makes winter bite on a second axis; building material gates new houses, which ties household formation to labour so the village can only spread as fast as it can build; tools raise gather yield and feed the skill pillar (§2.1) later. Three consumers is what makes "forage or cut timber?" a genuinely contested decision rather than a cosmetic one — a village that only forages can never grow.
  - **Tension to respect:** Phase 0 explicitly rejected warmth as "a second overlapping death system" (`specs/phase-0-vertical-slice.md §3`). Reintroducing fuel-as-survival is a deliberate reversal, defensible now that there are households to insulate and a labour system to trade off against — but the Phase 0 reasoning still applies to *legibility*. A death must never be ambiguous between cold and hunger; the log has to name which one killed someone.
- **D15 · 2026-07-26 · Labour assignment is a ranked list of plain conditions, and there is no public API to assign a worker.** The rule in order: able to work, workplace wants someone, within catchment (measured in shared travel cost), nearest home wins, ties break by villager id. A weighted score could be computed but not *explained*, and the player must be able to click a villager and get one sentence — so every assignment records its reason, naming the runner-up. The absence of an assignment API is asserted by a reflection test: the Banished pattern this deletes should be unexpressible, not merely discouraged.
- **D16 · 2026-07-26 · The village economy is derived from a stated target, not tuned.** `VillageEconomy` states it: one adult at minimum vigour with no partner must feed themselves and two children — the widowed-parent case that was killing every household. `gather_yield` and `stockpile_target` are computed from it and asserted by tests, so a later change to hunger, travel, or vigour that breaks the target fails the build rather than the village. Deriving it immediately exposed that homes placed in a line put outlying families three times further from food than the first, which no amount of global tuning could have found.
- **D14 · 2026-07-25 · Food is stored per household, and distribution eventually becomes a manned building rather than a policy slider.** Per-household stores make one family starving beside a thriving neighbour possible, which is where inequality stories come from. Phase 1 ships a visible sharing policy as a placeholder; the intended long-term form is a **market/food stall that a villager works at**, redistributing evenly within its catchment — because that turns distribution into the same "a person does this job somewhere" pattern as §2.2 rather than an abstract menu setting. Recorded now so the placeholder is not mistaken for the design.
- **D12 · 2026-07-25 · Ageing carries mechanical weight: vigour declines with age and scales foraging yield.** Ageing that only triggers a death event is a hollow reading of "generational time is the core loop" (§1.5) — it made every year of a life identical, so the death landed but the life did not. Vigour is full until 30, then declines to 55% in the final year. Tuning constraint: decline must make old age *hard*, never fatal, or the starvation and old-age arcs stop reading differently and Phase 0 loses its point.
- **D13 · 2026-07-25 · No childhood frailty in Phase 0.** A frail child is the honest mirror of a frail elder, but with one villager and nobody to depend on it is just an unsurvivable opening. Dependency and age-gated capability belong with households in Phase 1.
- **D7 · 2026-07-25 · Determinism tests must carry anti-vacuity guards.** A determinism test that cannot fail stays green forever and buys false confidence, so the suite includes tests asserting that different seeds *do* diverge and that the state hash *does* change with state. Verified by mutation: neutering `StateHash` turns 7 tests red.
