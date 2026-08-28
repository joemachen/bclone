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

**Probe a mechanic before building it.** The cheapest place to find out a design is wrong is before it exists. A throwaway measurement against the live sim costs ten minutes and has repeatedly overturned a decision that would have cost a day — D53's cold model was measured to kill nobody before a line of it was written, D56's clothing turned out to be a no-op the same way, and **D178 wrote a soil algorithm into a spec, probed it, and found it made the number it existed to raise *worse*.** If a change is supposed to move a number, measure the number first.

**⭐ AND THAT APPLIES TO THE TOOLING, NOT ONLY TO THE VILLAGE (D179).** The suite ran for **nineteen minutes** and the plan was to tag the long acceptance runs as "slow" and split it. **Measuring first said the plan was wrong**: the suite was already ~9.5× parallel, so throughput was never the cost. Following the slowest test down — nine minutes to read one integer off fifty villagers — led to `SimFactory.CreatePhase0` at **4,069 ms a world**, of which map generation was **1 ms**, and from there to an O(n²) Dijkstra in `TerrainCostField` doing 92 million iterations per flow field. **One `for` loop; the suite is 2m30s now.** *The horizons everybody suspected were never the problem, and shortening them would have cost the guards their teeth for nothing.*

**⚠️ A full run is for a verdict, not for discovery.** D178 ran the whole suite four times in one slice, twice to learn something that was already knowable — a golden was always going to move, and running nineteen minutes to be told so is nineteen minutes. **Use `--filter` while iterating; spend the full run when you need to be able to say the number.**

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
- **Single source of version truth** — the `VERSION` file, and ✅ **it is finally read** (2026-08-16, Phase 2's merge). `Directory.Build.props` loads it into `$(Version)`, so every assembly in the repo carries it — including the Godot view, which MSBuild reaches even though D11 keeps it out of the solution. The shell prints it beside the seed and the log path, because a bug report quoting those two is worth much less if nobody can say which build produced them. **`VersionTests` fails the build if the wiring comes undone**, which matters more than it sounds: an unwired build does not error, it silently reports .NET's default `1.0.0.0`, and nobody would notice until a release shipped the wrong number. *This bullet said "nothing reads it yet" for five phases — a single source of truth with no consumers is a text file.*
- **Release notes:** `CHANGELOG.md` in [Keep a Changelog](https://keepachangelog.com/) style, stamped with the version + date at release time.
  - **⚠️ It is DORMANT until the first tag, and its `[Unreleased]` section is written in one pass then — not accumulated as we work** (Joe, 2026-08-07). This section used to promise the opposite, and practice had quietly diverged from it for a dozen commits, which is the doc-versus-reality drift D48, D49 and D50 were each an instance of. **Saying what we actually do is the point of writing it down.**
  - **The reason is that they are not the same document.** `DESIGN.md §7` answers *why we chose this, and what we measured*, for us and for the next session; a changelog answers *what changed since the version you had*, for somebody who downloaded a build. There is no such person yet, which is exactly why nobody was writing it. Maintaining both by hand means writing every slice up three times — commit, decisions log, changelog — and **the third copy is the one that rots.**
  - **So it is generated at the tag**, from the commit log and `DESIGN.md §6`, and rewritten to be *player-facing* rather than engineering-facing. That is half an hour at release time and produces something the decisions log never will.
- **Version bump = a deliberate step:** write `CHANGELOG.md`'s new section, bump the version source, commit, then tag `vX.Y.Z`. The tag is what triggers the release build.
- **⛔ The per-version screenshot rule is withdrawn (D160, Joe's call).** It used to read *"every version gets a screenshot… take it with the hook in §6 rather than by hand, so the framing is repeatable"* — and the hook it depended on is deleted, because it fast-forwarded an **unattended** founding and D110 plus D143 make that a dead valley by construction. `screenshots/ss001` and `ss002` stay as the record of what the game looked like on 2026-08-01; nothing is obliged to add to them. If a release wants a picture, somebody takes one by playing.
- **✅ The release blockers are cleared** (2026-08-16, Phase 2's merge — `DESIGN.md §4` DoD item 4). Two of the three were as recorded; **the third was found while clearing them and nobody had it on a list.**
  - ✅ `VERSION` is read — see above.
  - ✅ `src/Bclone.Game/export_presets.cfg` is committed, and **verified as far as a machine without export templates allows**: running the workflow's exact `--export-release "Windows Desktop"` line locally gets past preset lookup and parsing and stops only at the missing templates, which rules out the two things that could have been wrong in it (a name not matching the workflow's literal string, and a malformed file).
  - ⛔ **AND THAT IS HOW THE THIRD ONE TURNED UP:** `chickensoft-games/setup-godot`'s **`include-templates` input defaults to `false`**, so `release.yml` would have failed at the export step *even with a perfect preset* — with exactly the error reproduced locally. Fixed in the workflow. **It could only ever have surfaced at the first `v*` tag**, because nothing else in the repo runs an export.
  - ⚠️ **Still unverified end to end, and honestly so:** nobody has run a real export, so the .exe itself is untested. `release.yml` carries a status note saying which parts are proved and which are not.
- **There are still no tags**, and that is unchanged: the workflow is dormant until the first `v*` push.

---

## 5a. Analyzers — and the flag that did nothing for a year

- **⛔⛔ `.editorconfig` IS LOAD-BEARING, NOT STYLE (D246).** `Directory.Build.props` has set
  `EnforceCodeStyleInBuild=true` since the first commit, and **there was no `.editorconfig` in the
  repository until 2026-08-28.** Without severity entries every `IDEnnnn` analyzer stays at
  `silent`, so `TreatWarningsAsErrors=true` — which this project does have — **had nothing to
  promote.** A codebase that fails the build on warnings accumulated dead code for a year because
  nothing could report it.
- **⭐ The rules are deliberately few: only dead-or-wrong code.** `IDE0051` (unused private
  member), `IDE0052` (assigned and never read), `IDE0005` (unnecessary using), `IDE0060` (unused
  parameter). *A linter that also has opinions about formatting is a build failure people learn
  not to read* — and the house style here is carried by the surrounding code and `CLAUDE.md`.
- **⚠️ `src/Bclone.Game` is exempt and always will be** — it sets `TreatWarningsAsErrors=false`
  because Godot's source generators emit code nobody controls. **It reports these and does not
  fail on them, so its build output has to be read rather than trusted to be silent.** A
  write-only field warned `CS0414` there for months unnoticed.
- **⭐ `IDE0060` is the one worth having.** Unused parameters cannot be found by search at all —
  a three-agent audit said so explicitly — and the analyzer found four in one build.

---

## 6. CI / GitHub Actions

- **On every push & PR:** build + run the full test suite (including the determinism test). `main` must stay green.
- **The Godot view is built separately, and it must be.** `Bclone.Game` is deliberately not in `bclone.sln` (D11 — a root Godot project globs `**/*.cs` and would swallow the sim and the tests into the game build). The cost is that `dotnet build bclone.sln` does **not** compile the view, so CI has its own step for it. Found the hard way: a build menu was written, wired up and never appeared, because nothing had compiled it and the assembly Godot ran was a day old. **If you are checking a view change by running the game, build `src/Bclone.Game/Bclone.Game.csproj` explicitly first** — a green solution build says nothing about it.
- **Running the game.** `run.bat` builds the view and launches Godot; set `GODOT` if your editor lives somewhere other than the path it defaults to.
- **⚠️ THE VIEW HAS NO AUTOMATED VERIFICATION OF ANY KIND** (D11, and D160 took the last of it). There are no tests, and the `BCLONE_SCREENSHOT` hook is gone — it stepped an *unattended* founding, which since D110 raises nothing and since D143 is **supposed** to die out, so it had been photographing a corpse. Teaching it to play would mean moving `PlayTheOpening` out of the test project into shipped code, which is a real cost for a convenience. **So looking at the view is the verification, and Joe's eyes are the test.** This is a known, accepted hole rather than an oversight — if a view regression ships, this is why.
- **⭐ THE ONE THING THE VIEW CAN BE MEASURED FOR IS ITS OWN WIDTH: `BCLONE_PROBE_WIDTHS`** (D169). Set it and the game walks the control tree, prints what every panel and every inspector row is claiming as a **minimum width**, and quits — headless, in about two seconds:

  ```bash
  # ⭐ WHERE THE BINARY IS. It is NOT on C: — a session searched there, concluded there was no
  # Godot on the machine, and shipped two unverifiable UI guesses before checking `run.bat`,
  # which has named the path all along. It must be the **mono/.NET** build (the project is
  # `Godot.NET.Sdk/4.7.1` and `config/features` lists "C#"); a standard build cannot run it.
  export GODOT="/d/Projects/Godot/Godot_v4.7.1-stable_mono_win64/Godot_v4.7.1-stable_mono_win64.exe"

  BCLONE_PROBE_WIDTHS=1 "$GODOT" --headless --path src/Bclone.Game
  ```

  **⭐ IT MEASURES THE BOTTOM CONTROL BAR TOO, SINCE 2026-08-27** (D242) — the one part of the UI
  that **grows during play**, when the library button appears, and the part nothing measured. It
  prints, per row, *what the row demands* against *how much room it has*, and flags a row that
  wants more than it has. **Both of that day's bugs are one line each in this output**: a row
  wider than the bar walks off the screen, and a bar far narrower than the window is a flow
  container with nothing to wrap inside.

  ⚠️ **`--resolution` is ignored here and the numbers are always 1280 wide**, because
  `project.godot` sets `stretch/mode="canvas_items"` at a 1280×800 viewport: **the UI is laid out
  at 1280 logical pixels and then scaled to whatever window the player has.** So a row that wraps
  in this output wraps on every monitor — *there is no "it will fit on a bigger screen".*

  **This exists because the question has now been asked three times and hand-rolled twice** (D149, then D169), and because it is genuinely un-guessable from the layout code: **a column can never be narrower than its widest child**, so `ColumnWidthFor` hands out 27% of the window and Godot overrules it. D149 found six stock-limit rows at 438 pinning a column at 450; D169 found the inspector's idle row wanting **733** on a 267-pixel column. **It is a measurement, not a hook that plays the game** — which is the distinction D160 drew when it deleted `BCLONE_SCREENSHOT` — and it is verified as of 2026-08-22 rather than assumed, having been run four times that day.
- **On version tag (`v*`):** `.github/workflows/release.yml` builds a **Windows `.exe`**, packages it, and attaches it to a GitHub Release with the changelog as the body. The Godot steps are written but have **never run**, because there has never been a tag. ⚠️ **This used to say "see §5 for the missing export preset" and §5 says the opposite** — `src/Bclone.Game/export_presets.cfg` is committed and its `[preset.0] name="Windows Desktop"` matches the literal the workflow passes. *Corrected 2026-08-28; the file contradicted itself for as long as both halves existed.*
  - ⚠️ **ONE THING TO CHECK BEFORE THE FIRST TAG, WHICH NOBODY CAN CHECK WITHOUT CUTTING ONE.** The export step passes `../../dist/bclone.exe` while the `mkdir dist` before it runs in the **repo root**. If Godot resolves that path against `--path src/Bclone.Game` rather than the process working directory, the binary lands two levels *above* the workspace and the `Compress-Archive` step zips an empty directory. `export_path` is empty in the preset, so **the CLI argument is load-bearing with no fallback.**

---

## 7. AI-assisted development notes

- Every session: Claude Code reads `CLAUDE.md` → `DESIGN.md` → relevant spec before coding.
- Small, reviewable changes over giant diffs — easier to QA and to revert.
- When Claude Code proposes a system, the **spec comes first**, then tests, then implementation — same discipline as any other contributor.
