# METHODOLOGY.md — Engineering Standards

How we build, so quality stays high and the project stays maintainable across a long solo build. `DESIGN.md` says *what* we build; this says *to what standard*.

---

## 1. Phasing discipline

Development follows the phased build order in `DESIGN.md §4`. The rules:

- **One phase at a time.** Do not build multiple pillars in parallel. Each phase ships in a playable, legible state before the next begins.
- **Phase 0 (single-villager vertical slice) is a gate.** Its success test — *watching one villager live and die actually means something* — must pass before any Phase 1 work starts.
- **Each phase gets a branch** (`phase/<n>-<name>`) and ends with a merge to `main` only when its Definition of Done is met.

---

## 2. Spec-first

No non-trivial system gets built without a short spec written **before** the code.

- Specs live in `specs/` as markdown, one file per system (e.g. `specs/desire-path-roads.md`).
- A spec is short and covers: **goal**, **which pillar/non-negotiable it serves**, **inputs/outputs**, **data model**, **edge cases & failure modes**, **how it's tested**, and **Definition of Done**.
- The spec is a living doc — update it if reality diverges, don't let it rot.
- For pillar systems, the failure modes are often already named in `DESIGN.md §2` (e.g. path lock-in, tech re-lock feeling unfair) — carry those into the spec.

---

## 3. Testing & QA

Testing is not optional and not an afterthought.

- **Unit tests** for all sim logic — resource math, aging, skill growth, pathfinding cost, trample/decay, tech unlock/re-lock, labor assignment. Sim logic is pure and deterministic, which makes it highly testable; exploit that.
- **Determinism test (critical):** same seed + same inputs ⇒ byte-identical state after N ticks. Write this early in Phase 0 and keep it green forever. A determinism regression is a P0 bug.
- **Golden/replay tests:** record an input sequence + expected end-state; replay to catch behavioral drift.
- **Prefer TDD for sim systems:** write the failing test from the spec, then implement.
- **QA pass per phase:** before merging a phase, do a manual playthrough against a written QA checklist for that phase (does it stay legible? can you read *why* things happen? does it hold the meditative pace?). Legibility is a QA criterion, not just a design goal.
- Tests run locally via `test.bat` and in CI on every push/PR.

**Definition of Done (per phase/feature):**
1. Spec written and current.
2. Unit tests written and passing.
3. Determinism test still green.
4. Manual QA checklist passed.
5. No new errors in the log during a clean playthrough.
6. `DESIGN.md` Progress Tracker + Decisions Log updated.

---

## 4. Error logging

Lots of logging, structured and leveled — this is a first-class feature, not a debug afterthought.

- **In-app structured logger** is the primary mechanism (the `.bat` log capture is just a convenience wrapper).
- **Levels:** `TRACE` / `DEBUG` / `INFO` / `WARN` / `ERROR`. Configurable minimum level; verbose in dev, quieter in release.
- **Write to a timestamped file** under `logs/` **and** to console in dev builds.
- **Context on every entry:** timestamp, level, subsystem (sim/render/pathing/economy/etc.), and the current **sim tick** — so any log line can be tied back to an exact simulation state. This is the legibility non-negotiable applied to the codebase.
- **Never swallow exceptions silently.** Catch → log with context → handle or fail loudly.
- **Sim assertions:** in debug builds, assert invariants (no negative resources, population counts reconcile, no orphaned jobs). A tripped assertion logs full context.

---

## 5. Versioning & Releases (active from v1)

- **Semantic Versioning** (`MAJOR.MINOR.PATCH`). Pre-1.0 the game is in-development; `v1.0.0` is the first real release.
- **Single source of version truth** (a `VERSION` file or the project/manifest file) — CI reads it; don't hand-edit in multiple places.
- **Release notes:** maintain `CHANGELOG.md` in [Keep a Changelog](https://keepachangelog.com/) style — an `## [Unreleased]` section accumulates entries as you work, and gets stamped with the version + date at release time.
- **Version bump = a deliberate step:** update `CHANGELOG.md`, bump the version source, commit, then tag `vX.Y.Z`. The tag is what triggers the release build.

---

## 6. CI / GitHub Actions

- **On every push & PR:** build + run the full test suite (including the determinism test). `main` must stay green.
- **The Godot view is built separately, and it must be.** `Bclone.Game` is deliberately not in `bclone.sln` (D11 — a root Godot project globs `**/*.cs` and would swallow the sim and the tests into the game build). The cost is that `dotnet build bclone.sln` does **not** compile the view, so CI has its own step for it. Found the hard way: a build menu was written, wired up and never appeared, because nothing had compiled it and the assembly Godot ran was a day old. **If you are checking a view change by running the game, build `src/Bclone.Game/Bclone.Game.csproj` explicitly first** — a green solution build says nothing about it.
- **On version tag (`v*`):** `.github/workflows/release.yml` builds a **Windows `.exe`**, packages it, and attaches it to a GitHub Release with the changelog section as the body.
- The release workflow is **tag-gated**, so it stays dormant until you push your first `v1.0.0` tag. **Its build steps are placeholders until the stack is chosen** — fill in the Godot export command or `cargo build --release` at that point (comments in the file show both).

---

## 7. AI-assisted development notes

- Every session: Claude Code reads `CLAUDE.md` → `DESIGN.md` → relevant spec before coding.
- Small, reviewable changes over giant diffs — easier to QA and to revert.
- When Claude Code proposes a system, the **spec comes first**, then tests, then implementation — same discipline as any other contributor.
