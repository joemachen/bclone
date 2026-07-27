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
- **Food comes from many kinds of work, not one patch.** Planned workplaces: **fishing huts, gathering huts, hunting huts**, and then **secondary processing** (a smokehouse, a mill — turning a raw yield into something better or longer-keeping). Two reasons this is structural rather than content. First, a catchment radius can only bind if a distant household has *something nearby* to work; with a single food source, a real catchment just starves the outskirts. Second, processing is where the tech tree (§2.7) attaches to daily life — "bake enough bread across enough winters" needs an oven someone works at. Recorded as D19; the first step is simply more than one raw food source.
- **Failure mode:** opacity. The player must be able to inspect *why* a given villager took a given job.

### 2.3 Systemic escalating pressure (fix the dead late game)
- **Thesis:** A well-run town shouldn't plateau into "nothing left but RNG."
- **Mechanic:** Replace random events as the *primary* threat with **systemic pressures that scale with the player's own choices**: soil depletion over decades, resource radii exhausting and forcing expansion, climate drift (runs of brutal winters), disease that spreads along the social and trade network.
- **Note:** Random events (fire, etc.) can still exist as *flavor*, but are not the main source of difficulty.
- **Failure mode:** pressure that isn't traceable to a decision. Every escalating problem should be back-traceable to something the player did.

### 2.4 Living region (you are a node, not an island)
- **Thesis:** The trading post shouldn't be a vending machine with a boat.
- **Mechanic:** A small surrounding economy — a few neighboring settlements with their own stocks and needs, prices that move, rivers and roads as real logistics. Trade is a system, not a menu.
- **First named tradeable: firewood** (D29). Not yet designed — recorded here so the resource is built as a first-class thing rather than a household counter. A processed good the village makes a surplus of is a natural first export, and it means the trading post arrives with something to sell rather than needing content invented for it.
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
   - **Carries a Phase 1 debt: make time-on-task personal (D28).** Vigour and skill should change *how long* a job takes, not only how much it yields. Villagers currently move in perfect lockstep — measured at 99.9% — because nothing distinguishes two people with the same home and the same job. Deliberately scheduled here rather than in Phase 1, because it is a §2.1 change and it forces the food economy to be re-derived.
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
- [x] **How villagers stop moving in lockstep.** ✅ Resolved 2026-07-26 → **make time-on-task personal; deferred to Phase 4 with the skill pillar.** See D28. Kept below for the measurement and the reasoning.
  - Joe watched the village at 4× and saw people travelling as duos and trios rather than as individuals. **Measured, and it is near-total:** two adults of the same household holding the same job are on the same tile **99.9%** of ticks, with identical hunger 100% of the time and doing the same thing 99.9% of the time (30-year run, shipped config).
  - **It will not resolve by itself, and that is the important part.** This is not missing variability, it is *symmetry*: they start from the same home tile, walk to the same site by the same fixed `StepToward` rule, gather for a constant `gather_ticks`, and gain hunger at a uniform rate — so they even stop to eat on the same tick. Two founders in a household run the same deterministic program on the same inputs for their entire lives; the only thing that ever separates them is which one dies first.
  - Nor do the planned systems fix it incidentally. Desire paths (§2.6) share one cost field, so they *increase* synchrony. Vigour scales yield but not time, so a frail elder still walks in step with a young adult. Multiple workplaces (D19) help only when capacity or proximity happens to split a household.
  - **The candidates**, roughly in order of how diegetic they are:
    1. **Time-on-task becomes personal** — let vigour, and later skill (§2.1), scale *how long* a job takes as well as how much it yields. Most diegetic, does double duty for the skill pillar, and deepens D12: an old villager would not just bring back less, they would be out longer. Cost: `VillageEconomy` derives trips-per-year from a fixed round trip, so the derivation has to move to the worst case.
    2. **A seeded personal rhythm** — each villager gets a small offset, drawn once at birth from the seeded stream, before they set off. Cheap, deterministic, and true to life; people do not all get up at the same moment. Treats the symptom rather than the cause.
    3. **View-only stagger** — cheapest, changes nothing real. They would still arrive and leave together.
  - *Recommendation: (1), with (2) as a stopgap if the lockstep grates before the skill pillar lands.*
- [ ] **Fan-out variability on the map (view only).** Joe's note: people grouped on a tile currently sit on a perfect ring at a fixed radius, which reads as arranged rather than gathered. Vary both the radius and the angle a little — deterministically, from villager id — so a crowded tile looks like a crowd. Small, and purely cosmetic.

---

## 6. Progress Tracker

> Update this section as work proceeds. Keep it honest — it's how we both know where we are.

**Current phase:** **Phase 2 (branch `phase/2-wood-fuel-and-tools`)**, re-ordered by Joe's call — §4 invites that. Wood-as-fuel (D17/D29) was taken before the environment work because winter needed a second axis to bite on, and storage (D30/D32/D33) after it because every goods bug so far has been "the right stuff in the wrong place". Environment/seasons depth + biomes (§2.5) is still the phase's headline and is not started.

**Phase 0: ✅ COMPLETE.** Success Test passed 2026-07-25.

**Phase 1: ✅ COMPLETE.** Success Test passed 2026-07-26 (Joe, watching at 4×) — *"they do stay legible"*. All six Definition-of-Done items met; 250 tests green. Passed conditional on D28 (make time-on-task personal) being addressed in Phase 4, which is recorded in the build order.

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

- **Village labour allocation shipped** (`specs/labour-allocation.md`, D21–D25). The blocking item is done: labour demand is split into a *local capacity* per workplace and a *village quota* per job kind, matched by a single deterministic cost-first pass, reshuffled yearly (D20). Several forage sites, spread the way the homes are. 246 tests green.
- **Definition of Done met:** `forager_catchment_tiles` lowered from 12 to **10**, where no home reaches every workplace — and the village still runs 150 years, ending at **24 alive in 34 households** from the founding four. Catchment binds for the first time.

- **A map you can read** (D26, D27). A 120×80 valley with a real camera — WASD pan, wheel zoom about the cursor, clamped to the bounds. People standing on the same tile are fanned apart so a household reads as a household. One control cycles how much explanation is drawn: off, the selected villager, or everyone.
- **Phase 1 Success Test passed**, and the phase's QA checklist written down in its spec so it is repeatable rather than remembered. A clean 150-year playthrough logging no warnings or errors is now a test (`CleanPlaythroughTests`) rather than a manual promise nobody kept.
- **Phase 1 merged to `main`** via PR #2 (Phase 0 went via PR #1).

*Phase 2 so far — both taken out of build order, deliberately:*
- **Wood as fuel, and the first processing chain** (`specs/wood-fuel-and-tools.md`, D17/D29). Wood is two resources: a **logger** fells **logs** at the stand, a **woodcutter** turns logs into **firewood** at a hut. Firewood burns per household per day of winter and running out kills, with `CauseOfDeath.Cold`. The epitaph names which of cold and hunger killed someone *and reports the other* — the condition D17 attached to reversing Phase 0's ban on a second death system. The woodcutter is the first workplace that can be idle for want of an **input** rather than a worker, which is the shape every later processing chain inherits.
- **The village holds a stable size** (D31), measured over 150 years: population between 19 and 28 for a century, zero deaths from cold, six from starvation against fifty-four from old age. The boom-bust was a births gate reading an absolute food threshold against a household target that scales with its members; as a percentage, births slow *before* anyone starves rather than after.
- **Goods live in buildings** (`specs/storage-and-distribution.md`, D30/D32, slices 1–3 of 5). A **granary** holds food, a **shed** holds materials, workplaces hold small buffers, homes keep a working larder (D10 — meals stay instant). Goods move only by trips people make: producers carry loads, households fetch, and `carry_capacity` is what stops a fetch being a teleport with extra steps. **All four placeholder workarounds the spec required deleting are gone** — `ShareFood`, `ShareFirewood`, `TryTakeLogsFromTheVillage`, and the village-wide timber sweep. 266 tests green.

- **Storage capacity, and the flat line** (D33, D34; spec slice 5, taken ahead of the market on Joe's call). The granary decides how big the village gets: one stated number — how many people a granary feeds — and the ceiling is derived from it. **The village now holds 24–35 people for 300 years**, against 24–86 unbounded. Getting there turned up the bug that had been killing every settlement since Phase 1: the dead were never removed from their household's member list, and the birth check read that list as "how many live here", so households ratcheted into permanent sterility and every village died out around year 180. The 150-year acceptance window stopped one generation short of showing it. **The acceptance run is now 300 years** and asserts the village is still standing at the end. 272 tests green.

**In progress:**
- Nothing.

**Next up:**
- **`specs/storage-and-distribution.md` slice 4 — the manned market**, the last slice of D30 and the building half of D14. Joe's sequencing: capacity first, then this.
- **Revisit `granary_feeds_people`** (currently 30) once there is a reason to. It is now the answer to "how big can my village get", so it is a design number rather than a tuning one — and it stops being a config line at all the moment building placement lands.
- **Seeded map generation** (D18) — and it now has a concrete job to do beyond variety: homes are placed on a fixed spiral that knows nothing about where the work is, which is what stops catchment being tightened below ten (see the spec's §8).
- More workplace kinds (D19) — fishing, gathering and hunting huts, then secondary processing.
- **Environment/seasons depth + biomes** (§2.5) — the phase's actual headline, including the spoilage question D32 parked here.
- **Tools** (D17) — still deferred until there is a workshop to make them at.

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
- **D34 · 2026-07-27 · The dead were taking up room in the house, and that is what had been killing every village.** A household's member list keeps everyone who ever lived there — `RemoveMember` is called when somebody *moves out* and never when somebody *dies* — and the birth check read that list's length as "how many live here". So a household that had seen `max_household_size` people pass through it was permanently barred from having another child, with a young couple in it and every other condition met, even once all seven were in the ground. **Households ratcheted one way into sterility, so every settlement died out about a century in regardless of its food, its fuel, or its storage.** Found by running 300 years instead of 150 and asking, per household per year, which condition was refusing a birth. It hid for two phases because it looks exactly like the population wave it coincides with — a slow demographic decline is what you would *expect* to see. Every other occupancy question in the sim already asked `LivingMembersOf`; this was the one place that did not.
  - **The test window was the reason it survived.** The acceptance run watched 150 years, and the collapse completed at about year 180: the village was at twenty-three and falling when the test stopped looking, which read as the tail of a wave rather than the middle of an extinction. 150 had never been chosen against anything. **The acceptance run is now 300 years and asserts the village is still there at the end**, plus that it held a *band* rather than merely surviving — because "never dropped below the founding four" was satisfied for a century and a half by a village in the process of dying.
  - **Corollary worth keeping: an assertion about a window is not an assertion about a system.** Every long-run test here should be asked what it would do if the run continued.
- **D33 · 2026-07-27 · Storage capacity is what decides how big the village gets, and one stated number sets it.** Slice 5 of `specs/storage-and-distribution.md`, taken ahead of the market on Joe's call. A granary holds a winter's store for `granary_feeds_people`; `VillageEconomy` derives the quantity from that and the **population ceiling** from the quantity, so the ceiling is a consequence rather than a setting (D16). Births are already gated on the granary holding a share of what everyone alive would want — a demand that grows with the village and used to be unbounded — so capping the building caps the settlement. **Measured over 200 years: the village holds 24–35 people, against 24–86 with the cap removed.** That is the flat line Joe asked for, and the mechanism is the one he predicted: growth stops at what the buildings support instead of overshooting them and falling back.
  - **How big a granary is counts as content, not economy** — a fact about the building, like how many hands fit at a berry patch — so it lives in the config where a modder can change it. When placement lands, this is the moment storage starts paying the player back: the answer to "we have outgrown the granary" becomes *build another one*, and **where** it goes is the first decision in the game that storage makes interesting. Until then the pressure is real and the response to it is a config line, which is the same gap `RequiredWoodcutterSeats` already carries.
  - **Capacity is total across goods, not per good.** A shed packed with logs has nowhere to stack firewood, and being made to choose is the point; three independent shelves that never compete would be bookkeeping wearing a constraint's clothes. Measured in both directions — an over-eager fuel quota packed the shed with six hundred firewood, logs could not be deposited, no house was ever raised again, and the village dwindled to three people without a soul freezing.
  - **Two bugs it flushed out, both the same shape as the ones already logged.** `TargetFoodForTheGranary` was answering two questions — *can we feed another mouth?* (must stay unbounded; that IS the ceiling) and *should anyone go out and work?* (must be bounded by what fits, or the answer is yes forever) — which is D21 again. And `WoodcuttersWanted` counted firewood stacked in *other people's homes*, which no household can fetch, so a surplus in one house cancelled a shortage in another; the justification in the comment was a sharing policy that slice 3 had already deleted.
- **D32 · 2026-07-27 · Food gets its own building — a granary — separate from the shed that holds materials.** Joe's call, and it settles the question D30 was going to have to face: whether one undifferentiated store would quietly delete the inequality D14 exists to create. It would have — *"one family starving beside a thriving neighbour"* is the story per-household food was introduced for, and a single village-wide pile makes it unexpressible. Two buildings keep it, and **change what inequality is made of, for the better**: it stops being about whose larder it is, which is an accident of which house a forager was born in, and becomes about **distance and hands** — a household far from the granary, or with nobody spare to send, eats worse than its neighbours. That is spatial, watchable, and it ties straight into catchment (§2.2) and desire paths (§2.6) instead of sitting off to one side. A story about a family on the wrong end of the valley beats one about a family with the wrong surname. It is also just the honest division: food spoils and timber does not, which is why no village has ever kept them in the same shed.
  - **Named for later, not now:** if granary food never spoils, the village has a bank, and a village with a bank has permanently solved winter — which undoes much of §2.5. Spoilage is the counterweight and belongs with Phase 2's environment work. Recorded because it changes what the food economy is derived against, and that derivation should not be redone twice.
- **D31 · 2026-07-26 · The village should survive at a *stable size*, not by growing.** Joe's call, and it settles what the acceptance tests are actually for. Failure has to remain possible — §2.3 is built on pressure and §1.1 promises the player understands the town's death — so a test asserting "the population grows" was quietly asserting the wrong thing. What the baseline must show is that an *unpressured* village holds steady; a village that dies should die because of something, and right now there is no player to blame, so a dying baseline can only mean the constants are wrong. **The corollary worth remembering when the player gains real choices: at that point a collapse becomes a legitimate outcome, and these tests should be re-read rather than defended.**
- **D30 · 2026-07-26 · Goods live in buildings, not only in households.** Joe's call, and it supersedes the per-household stores for *materials*. A small amount of firewood sits at the woodcutter's workshop; the rest goes to a **general-purpose storage shed** (stone, logs, lumber, cloth, firewood), to the **marketplace**, and to villagers' homes. Three things follow. It is the concrete form of D14's "distribution is a building, not a policy slider" — the seasonal food and daily firewood sharing policies are placeholders that storage plus a market should delete together. It gives §2.6's desire paths something real to be about, because hauling between buildings is *traffic*. And it fixes by design a bug that has now been found twice: goods piling up where they cannot be spent (logs in the logger's house, D25; firewood in the woodcutter's house, D29) — a shed is the answer both times. **Not built yet**; it needs its own spec, and it should probably land alongside D19's extra workplaces since both are about buildings having jobs and contents.
  - **Status, appended 2026-07-27:** built, slices 1–3 of `specs/storage-and-distribution.md`. All four workarounds named above are deleted. It did *not* land alongside D19 — the extra workplaces are still pending, and that turned out not to matter. The market (slice 4) and capacity (slice 5) remain.
- **D29 · 2026-07-26 · Wood is a two-stage chain: a logger fells **logs**, a woodcutter turns logs into **firewood**, and firewood is the fuel.** Joe's call, following *Banished*. Logs build; firewood burns. **This is the project's first secondary processing chain, and that is the significant part** — §2.2 already calls processing structural rather than content ("processing is where the tech tree attaches to daily life"), and every workplace so far has been a pure producer. A workplace that *consumes an input to make an output* can be idle for want of logs rather than for want of a worker, which needs a new refusal reason and a **two-stage quota** where demand propagates back down the chain. Doing that on something as simple as firewood is the point: every processing chain after this one inherits the shape. Note the naming cuts against everyday usage — colloquially a woodcutter fells trees — so the tree-stand job is renamed `Logger`.
  - **Firewood is shared between households**, on the same cadence and the same keep-your-own-floor rule as food. Cold is deliberately *parallel* to hunger rather than harsher than it, so the player keeps one mental model instead of two.
  - **Forward-looking, recorded so it is not lost:** firewood is eventually **traded at the trading post** (§2.4 — trade has not been designed yet) and **distributed by the market building** (D14). Both are reasons to keep firewood a first-class resource rather than a counter on a household, and the seasonal sharing policy shipped with it is the *same placeholder* the food sharing is — the market should delete both together.
  - **Tools wait for a workshop to be made at** (Joe's call). A tool materialising from a household's woodpile by policy is the same abstraction as the food-stall slider D14 replaces and the worker-slot §2.2 deletes. When they land they hang off the villager, not the household, so §2.1 can attach skill without a migration.
- **D28 · 2026-07-26 · Time-on-task becomes personal — but in Phase 4, with the skill pillar.** Joe's call: vigour, and later skill (§2.1), should change *how long* a job takes and not only how much it yields. That is the fix for villagers moving in lockstep (measured at 99.9% — see §5), and it is the most diegetic of the options because it makes ageing something you watch rather than read: an old villager would not just bring back less, they would be *out longer*, deepening D12. **Deferred deliberately**, for two reasons. It is a §2.1 change and METHODOLOGY §1 forbids building pillars in parallel; and it forces `VillageEconomy` to re-derive trips-per-year from a worst-case round trip, which would reopen a food economy that has only just been stabilised — on a phase that has just passed its Success Test. The cheap stopgap, if the duos start to grate first, is a seeded per-villager rhythm drawn once at birth; that treats the symptom, and it can be pulled forward on its own.
- **D27 · 2026-07-26 · No camera rotation until the view has depth.** Joe asked for middle-mouse rotate alongside pan and zoom, and it was deferred once it was clear what it would actually do: the view is flat top-down, so rotating it spins the map like paper on a table rather than orbiting it the way *Banished* does. That is a different feature, and building it now would mean building it twice. Middle mouse is deliberately left unbound so the binding is free when the view gains an angle.
- **D26 · 2026-07-26 · The world has bounds — a 120×80 valley — and the map has a camera.** The view used to auto-fit every workplace each frame, which was survivable with one berry patch and became useless with seven: the settlement rendered as a smudge three tiles across in the middle of an empty panel, and the villagers, all standing on the same few tiles, drew as one dot per house. So framing became something you do rather than something that happens: WASD pans, the wheel zooms about the cursor, and both stop at the valley's edge. **Bounds live in `SimConfig`, not in the view**, because seeded map generation (D18) will generate terrain into exactly this rectangle — at which point it stops being a drawing hint and becomes world state. Wide rather than square, for the river valley in §2.5.
  - **Corollary: people on the same tile are fanned apart on a small ring**, by rank within the tile, so four adults resting at one house read as four people. View-only; sim positions never move. This is the phase's Success Test in miniature — "watching twelve villagers is still legible" cannot be answered if twelve villagers draw as three dots.
  - **Corollary: one control decides how much explanation is on screen** — off, the selected villager, or everyone — governing both the home-to-work route lines and the catchment rings. The route line is the visual counterpart of `JobReason`: the sentence says *"the tree stand was nearer at 2 tiles, but the village has all the woodcutters it needs"*, and the line shows the same thing without a click. Seven catchment rings drawn permanently were most of the clutter and none of the meaning.
- **D25 · 2026-07-26 · A new house is paid for by the whole village, the two parent households first.** Drawing only from the parents looked right — families provide for their children — but timber is cut by whoever lives nearest the stand, who is very often nobody's parent. So the village cut wood year after year, piled it in the woodcutter's own house where it could not be spent, and no home was ever built: every settlement stalled at a handful of houses and aged out *without a single villager starving*. Raising a house is communal work, and the store it comes out of is the village's. (This is not a retreat from D14 — food stays per household, because starving beside a full neighbour is the story D14 exists to make possible. Nobody starves for want of timber.)
- **D24 · 2026-07-26 · Forage sites are spread the way the homes are, and catchment drops to 10 tiles.** D19 said "more than one food source"; that turned out not to be sufficient on its own. Putting all the extra sites out at the map edges left every home near the *middle* of the village competing for the one original berry patch, so tightening catchment did not restrict outlying households — it left central ones idle beside a full patch, and they starved. A ring of sites at roughly the width of the settlement, plus two further out, is what lets catchment bind survivably. Ten tiles is the shipped radius: at twelve a central home could reach nearly everything, so the rule constrained nothing.
- **D23 · 2026-07-26 · The work the village needs least of is allocated first.** A single cost-sorted pass over every workplace — the spec's design — lets everyone near the tree stand take an even nearer berry patch first, so the one timber job falls to whoever is left over, who is by construction the most remote person in the village and often cannot reach the stand at all. The job went unfilled and the village stopped building. Filling scarce work first hands it to whoever genuinely lives nearest, costs food nothing (the quota has already decided what can be spared), and is the more explainable of the two: "Elias cuts timber because he lives nearest the stand" beats "because he was the last one left".
- **D22 · 2026-07-26 · Timber demand is derived from what wood is for, plus a standing woodpile.** Sparing every hand food did not need put *half* a founding village on the tree stand — one cutter yields enough for several houses a year, and wood is simply much cheaper than food. So the timber quota is asked the same question as the forager quota: how much does the village need? Answer: the homes couples are waiting for, **plus one more kept in store**. That last clause is not padding — cutting only what is currently needed turns timber from a job into an errand (a hand goes to the stand at the new year, cuts thirty logs by midspring, and is taken off again), and it halved the village's growth rate. A village that has been through a winter keeps a woodpile.
- **D21 · 2026-07-26 · Labour demand is two questions, not one: local capacity and a village quota.** `Workplace.LabourDemand` became `Workplace.Capacity` — how many hands physically fit — and the village-level question ("how many foragers does this settlement need?") moved to `LabourQuota`, where it can actually be answered. Four different values of the old single field each broke the village in a different way (`specs/labour-allocation.md §3`); none could have worked, because no local field can express a global constraint. The forager quota is a **floor**, not a ceiling: a ceiling leaves able adults idle, and with food stored per household (D14) an idle adult is not a spare resource, they are a household producing nothing.
- **D20 · 2026-07-26 · Labour reshuffles periodically rather than being pinned by rules.** Joe's call, following *Banished*: the village re-runs its labour allocation on a cadence, so workers drift toward jobs near where they live. Chosen over a hard "one forager per household" floor because a reshuffle is a *behaviour the player can watch*, whereas a floor is a rule they must be told; and because it self-corrects the cases a floor cannot — a forager dying, or a family moving house. Implication: the allocator must be re-runnable from scratch rather than incremental. Constraint: a job change must state its own reason, or the player sees people inexplicably swapping jobs.
- **D19 · 2026-07-26 · Food will come from several kinds of workplace, plus secondary processing.** Fishing, gathering and hunting huts as raw sources; smokehouse/mill-style processing above them. Not a content wishlist: the measured finding is that a *binding* catchment radius kills outlying households when there is only one food source, so multiple sources are the prerequisite for §2.2's central rule working at all. Processing is also where §2.7's "unlock by doing" gets somewhere to live. First step is just more than one raw source.
- **D18 · 2026-07-26 · The map will be generated from the run's seed, and it is the same seed as the sim.** Terrain, water, forest stands, and forage sites get generated rather than hand-placed, so a second playthrough is a different *place*. Because the sim is already deterministic and seeded, map generation must draw from the same seeded RNG in a fixed order — then a single number reproduces an entire run including its world, which is what makes bug reports and shared seeds work. Positions currently live in config as literal coordinates; those become generator output. Not built yet.
- **D17 · 2026-07-26 · Wood serves all three purposes: winter fuel, building material, and tools.** Joe's call. Fuel makes winter bite on a second axis; building material gates new houses, which ties household formation to labour so the village can only spread as fast as it can build; tools raise gather yield and feed the skill pillar (§2.1) later. Three consumers is what makes "forage or cut timber?" a genuinely contested decision rather than a cosmetic one — a village that only forages can never grow.
  - **Clarified (Joe, 2026-07-26): "building material" means every building, not just homes**, and tools are in rather than possible. That matters to `LabourQuota.WoodcuttersWanted`, which derives timber demand from what the wood is *for* — currently one term (homes for couples waiting) plus a standing woodpile. Each new consumer is another term in the same sum, and together they are what turns cutting from an occasional errand into a livelihood somebody holds. **Measured symptom to fix, not a prediction:** with homes as the only consumer, a village with a full woodpile correctly staffs nobody at the stand, so §2.2's "trees do not stop in winter" advantage currently has almost nothing to bite on (see `specs/labour-allocation.md §4a`).
  - **Tension to respect:** the `+1` woodpile reserve in the timber quota is a stand-in for continuous demand. When the other consumers land, check whether it is still earning its place or has become double-counting.
  - **Tension to respect:** Phase 0 explicitly rejected warmth as "a second overlapping death system" (`specs/phase-0-vertical-slice.md §3`). Reintroducing fuel-as-survival is a deliberate reversal, defensible now that there are households to insulate and a labour system to trade off against — but the Phase 0 reasoning still applies to *legibility*. A death must never be ambiguous between cold and hunger; the log has to name which one killed someone.
- **D15 · 2026-07-26 · Labour assignment is a ranked list of plain conditions, and there is no public API to assign a worker.** The rule in order: able to work, workplace wants someone, within catchment (measured in shared travel cost), nearest home wins, ties break by villager id. A weighted score could be computed but not *explained*, and the player must be able to click a villager and get one sentence — so every assignment records its reason, naming the runner-up. The absence of an assignment API is asserted by a reflection test: the Banished pattern this deletes should be unexpressible, not merely discouraged.
- **D16 · 2026-07-26 · The village economy is derived from a stated target, not tuned.** `VillageEconomy` states it: one adult at minimum vigour with no partner must feed themselves and two children — the widowed-parent case that was killing every household. `gather_yield` and `stockpile_target` are computed from it and asserted by tests, so a later change to hunger, travel, or vigour that breaks the target fails the build rather than the village. Deriving it immediately exposed that homes placed in a line put outlying families three times further from food than the first, which no amount of global tuning could have found.
- **D14 · 2026-07-25 · Food — and now firewood (D29) — is stored per household, and distribution eventually becomes a manned building rather than a policy slider.** Per-household stores make one family starving beside a thriving neighbour possible, which is where inequality stories come from. Phase 1 ships a visible sharing policy as a placeholder; the intended long-term form is a **market/food stall that a villager works at**, redistributing evenly within its catchment — because that turns distribution into the same "a person does this job somewhere" pattern as §2.2 rather than an abstract menu setting. Recorded now so the placeholder is not mistaken for the design.
- **D12 · 2026-07-25 · Ageing carries mechanical weight: vigour declines with age and scales foraging yield.** Ageing that only triggers a death event is a hollow reading of "generational time is the core loop" (§1.5) — it made every year of a life identical, so the death landed but the life did not. Vigour is full until 30, then declines to 55% in the final year. Tuning constraint: decline must make old age *hard*, never fatal, or the starvation and old-age arcs stop reading differently and Phase 0 loses its point.
- **D13 · 2026-07-25 · No childhood frailty in Phase 0.** A frail child is the honest mirror of a frail elder, but with one villager and nobody to depend on it is just an unsurvivable opening. Dependency and age-gated capability belong with households in Phase 1.
- **D7 · 2026-07-25 · Determinism tests must carry anti-vacuity guards.** A determinism test that cannot fail stays green forever and buys false confidence, so the suite includes tests asserting that different seeds *do* diverge and that the state hash *does* change with state. Verified by mutation: neutering `StateHash` turns 7 tests red.
