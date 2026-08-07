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

**Assert against the shipped config, not only the fixture.** `VillageFixtures.Village` derives its numbers; `data/sim.config.json` is typed in by hand and is what the game actually loads. The two drift, and the gap is where bugs live — it has produced D48 (a timber leak four times worse in the shipped file), D50 (three woodcutter seats where the economy needed eight), and D49 (thirty-day seasons that reached the game and not the tests, for four commits). `ShippedConfigTests` runs a real village on the real file; anything the economy depends on gets a guard there as well as against the fixture.

**Probe a mechanic before building it.** The cheapest place to find out a design is wrong is before it exists. A throwaway measurement against the live sim costs ten minutes and has twice now overturned a decision that would have cost a day — D53's cold model was measured to kill nobody before a line of it was written, and D56's clothing turned out to be a no-op the same way. If a change is supposed to move a number, measure the number first.

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
- **The game writes a full audit trail every run.** Two sinks, fanned out by `CompositeLogSink`: the on-screen village log stays at `INFO` (the story — D8/D9), and `logs/bclone-<timestamp>.log` takes everything down to `DEBUG`. That file is the answer to *"what actually happened?"* — every state change, every load carried, every job taken and every refusal, each stamped with a tick and attributed to a subsystem. The path is shown in the header beside the seed, because together they are what reproduces and explains a run.
- **Guard `DEBUG` lines with `world.Logs(level)`.** The trail is detailed enough that building its messages unconditionally costs real time in the 300-year acceptance runs, where the sink discards them all — string interpolation happens before the sink gets a say. `LogVillager` guards itself; anything building a string by hand should check first.
- **Villager events are logged from one place**, not per branch. `BehaviorSystem.Execute` takes a before-and-after of each villager's state, position and load, so a new branch is covered the day it is written rather than the day somebody notices the gap while debugging it.
- **Context on every entry:** timestamp, level, subsystem (sim/render/pathing/economy/etc.), and the current **sim tick** — so any log line can be tied back to an exact simulation state. This is the legibility non-negotiable applied to the codebase.
- **Never swallow exceptions silently.** Catch → log with context → handle or fail loudly.
- **Sim assertions:** in debug builds, assert invariants (no negative resources, population counts reconcile, no orphaned jobs). A tripped assertion logs full context.

---

## 5. Versioning & Releases (active from v1)

- **Semantic Versioning** (`MAJOR.MINOR.PATCH`). Pre-1.0 the game is in-development; `v1.0.0` is the first real release.
- **Single source of version truth** — the `VERSION` file. **Nothing reads it yet**; wiring it into the build is part of the first real tag (see below).
- **Release notes:** `CHANGELOG.md` in [Keep a Changelog](https://keepachangelog.com/) style, stamped with the version + date at release time.
  - **⚠️ It is DORMANT until the first tag, and its `[Unreleased]` section is written in one pass then — not accumulated as we work** (Joe, 2026-08-07). This section used to promise the opposite, and practice had quietly diverged from it for a dozen commits, which is the doc-versus-reality drift D48, D49 and D50 were each an instance of. **Saying what we actually do is the point of writing it down.**
  - **The reason is that they are not the same document.** `DESIGN.md §7` answers *why we chose this, and what we measured*, for us and for the next session; a changelog answers *what changed since the version you had*, for somebody who downloaded a build. There is no such person yet, which is exactly why nobody was writing it. Maintaining both by hand means writing every slice up three times — commit, decisions log, changelog — and **the third copy is the one that rots.**
  - **So it is generated at the tag**, from the commit log and `DESIGN.md §6`, and rewritten to be *player-facing* rather than engineering-facing. That is half an hour at release time and produces something the decisions log never will.
- **Version bump = a deliberate step:** write `CHANGELOG.md`'s new section, bump the version source, commit, then tag `vX.Y.Z`. The tag is what triggers the release build.
- **Every version gets a screenshot**, committed to `screenshots/` and named `ssNNN-<date>.png` — `ss001-aug1-2026.png`, and up. A changelog says what changed; a screenshot says what it *looked* like, and a generational village-builder is a thing you watch. The README shows the most recent one. Take it with the hook in §6 rather than by hand, so the framing is repeatable.
- **Not yet wired up, and deliberately so.** `VERSION` is read by nothing, there are no tags, and `src/Bclone.Game/export_presets.cfg` does not exist — which is a hard blocker on `release.yml` ever succeeding, since it exports the "Windows Desktop" preset from a clean checkout. All three are Phase 2's merge to deal with (Joe's call), and they are recorded here so the first tag is not a surprise.

---

## 6. CI / GitHub Actions

- **On every push & PR:** build + run the full test suite (including the determinism test). `main` must stay green.
- **The Godot view is built separately, and it must be.** `Bclone.Game` is deliberately not in `bclone.sln` (D11 — a root Godot project globs `**/*.cs` and would swallow the sim and the tests into the game build). The cost is that `dotnet build bclone.sln` does **not** compile the view, so CI has its own step for it. Found the hard way: a build menu was written, wired up and never appeared, because nothing had compiled it and the assembly Godot ran was a day old. **If you are checking a view change by running the game, build `src/Bclone.Game/Bclone.Game.csproj` explicitly first** — a green solution build says nothing about it.
- **Running the game, and screenshotting it.** `run.bat` builds the view and launches Godot; set `GODOT` if your editor lives somewhere other than the path it defaults to. **The view has no tests at all** (D11), so looking at it is the verification — and `BCLONE_SCREENSHOT` makes looking repeatable:

  ```
  set BCLONE_SCREENSHOT=D:\Projects\bclone\screenshots\ss002-aug1-2026.png
  set BCLONE_SCREENSHOT_YEARS=45
  <godot> --path src/Bclone.Game --resolution 1640x1050
  ```

  It runs the sim forward the given number of years, draws, writes the PNG and quits. The years matter: a village at tick 3 is four people on a doorstep and an empty log, which is a true picture and a useless one. This is also how the per-version screenshot in §5 is taken.
- **On version tag (`v*`):** `.github/workflows/release.yml` builds a **Windows `.exe`**, packages it, and attaches it to a GitHub Release with the changelog as the body. The Godot steps are written but have **never run** — see §5 for the missing export preset.

---

## 7. AI-assisted development notes

- Every session: Claude Code reads `CLAUDE.md` → `DESIGN.md` → relevant spec before coding.
- Small, reviewable changes over giant diffs — easier to QA and to revert.
- When Claude Code proposes a system, the **spec comes first**, then tests, then implementation — same discipline as any other contributor.
