# CLAUDE.md — Working Agreement

You are helping build a ground-up, generational village-builder / survival sim (a spiritual successor to *Banished*). This file is your standing instructions for **how** to work. The **what** lives in `DESIGN.md`; the engineering **standards** live in `METHODOLOGY.md`.

## Before anything
1. **Read `DESIGN.md` in full at the start of every session.** It is the source of truth for the design.
2. **Read `METHODOLOGY.md`** — it governs phasing, spec-first work, testing/QA, error logging, versioning, and CI. Follow it.
3. Check the **Progress Tracker** (DESIGN.md §6) to see where we are before proposing work.
4. For any non-trivial system, read (or write) its spec in `specs/` before coding — spec first, then tests, then implementation (METHODOLOGY §2).

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

## Update protocol (do this — it's load-bearing)
- After each meaningful chunk of work, **update DESIGN.md §6 (Progress Tracker)**: move items between Done / In progress / Next up, and update the Current phase.
- When you resolve an Open Decision (§5) or make a significant architectural choice, **append a one-line entry to DESIGN.md §7 (Decisions Log)** with the rationale, so future sessions inherit the reasoning.
- If you discover a new pillar-level idea or a design tension, add it to DESIGN.md rather than only mentioning it in chat — chat is ephemeral, the doc is not.

## Before large moves
- **The stack is settled** (D1): C# (.NET 8) + Godot 4, with the sim in a Godot-free class library. Anything that would couple `Bclone.Sim` to the engine is a design change, not a detail — raise it.
- Flag any architecture deviation from DESIGN.md §3 before committing to it.

## Working with Joe
- Joe is technical (marketing/data engineering background — GA4/BigQuery/GTM, works with developers) but is **not assumed to be a professional systems/game programmer.** Explain systems-level and language-specific choices when they matter; don't assume deep prior knowledge of the engine internals. Keep him in the loop on load-bearing decisions rather than burying them.
- Casual, grounded, direct tone is welcome. Push back honestly when a design choice is wrong — that's more useful than agreement.
