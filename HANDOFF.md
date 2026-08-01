# Handoff — bclone

Read `CLAUDE.md`, then **`DESIGN.md` §6 (Progress Tracker) and §7 (Decisions Log)**, then
`METHODOLOGY.md`, then the spec for whatever you are about to touch.

**This file is deliberately short.** It used to carry a progress list, a queue, and a
lessons section, all of which drifted out of step with `DESIGN.md` — at one point the two
files held "next up" lists with no items in common, both labelled "Joe's call". §6 owns
what is done and what is next; §7 owns why. What is left here is the handful of things
that live nowhere else.

---

## State of play

- Branch `phase/2-wood-fuel-and-tools`. Phases 0 and 1 are merged to `main` (PRs #1, #2).
  Everything since is on this branch, unmerged on Joe's standing call: **Phase 2's
  Definition of Done is not met**, and merging now would make `main` a checkpoint rather
  than a completed phase.
- The suite takes about four minutes; the 200- and 300-year acceptance runs dominate.
  **Time it twice before believing a regression** — one measurement showed a 38% slowdown
  that was 3% on a quiet machine.

**Run it:** `run.bat`. Set `GODOT` first if your editor is not at
`D:\Projects\Godot\Godot_v4.7.1-stable_mono_win64\`.

---

## The trap that will bite you first

**`bclone.sln` does not contain the Godot project.** `dotnet build bclone.sln` compiles the
sim and the tests only. A build menu was once written, wired up, and silently never
appeared, because the assembly Godot ran was a day old.

- Building the view: `dotnet build src/Bclone.Game/Bclone.Game.csproj`. CI has a separate
  step for it, and `run.bat` does it before launching.
- **Nothing in `src/Bclone.Game` can be unit-tested at all** (D11 puts it outside the
  solution). View changes are verified by running the game and by nothing else. Say so
  when you report one.
- **You can do better than "I ran it and it looked fine."** `BCLONE_SCREENSHOT` writes the
  window to a PNG and quits; `BCLONE_SCREENSHOT_YEARS` runs the sim forward first, so the
  shot shows a village that has lived. METHODOLOGY §6 has the invocation. To check that
  something *stopped moving*, a `GD.Print` of a node's rect whenever it changes turns "does
  the map still jump?" into a number — and **check the thing you are measuring actually
  varies during the run**, or the guard is watching a case that never happens.

---

## How Joe wants to work

- **Keep the remote branch green.** Hold WIP in local commits or a side branch; push once a
  slice is green.
- **Report at meaningful transitions, don't grind. When a measurement contradicts the plan,
  stop and say so.**
- **End every message with the explicit ask.**
- Spec-first for anything non-trivial; record decisions in `DESIGN.md` §7 rather than
  leaving reasoning in chat, and update §6 as work lands.
- Joe finds real bugs by *playing*, and by asking why something is the way it is. Several
  of the best findings in this project came from his questions rather than from the plan.
- **Push back when a design choice is wrong** — he asked for this explicitly. D51 is the
  worked example: his first shape for player staffing would have made micromanagement
  mandatory, the flag was welcome, and the agreed version is better than either starting
  position.

---

## Lessons that would be expensive to rediscover

The decisions themselves live in §7. These are the ones that generalise past the decision
that produced them.

- **A village that stops growing while its stores are full is a distribution bug until
  proven otherwise.** D34 and D48 both presented as a demographic wave — nobody starved,
  nobody froze, the granary was full. That disguise has cost two long investigations.
- **Measure, do not pattern-match.** Every diagnosis reasoned from precedent in the D52
  session was wrong, and every one that came from a probe was right. And **probe a mechanic
  before you build it** — D53's cold model and D56's clothing were both measured as no-ops
  before a line of either was written.
- **Numbers in a handoff are hearsay until re-measured.** The predecessor to this file
  recorded fetch counts off by 3× and populations taken at the wrong instant, and both were
  quoted forward with confidence.
- **A comparative test has two villages, and either can be the broken one.** D52: the
  market test failed and a whole session went looking inside the market. The control had
  collapsed. Check the control's health before believing the comparison, and never read a
  per-head figure off an end-of-run count.
- **An assertion about a window is not an assertion about a system**, and a raw aggregate
  is not a rate.
- **Derive, don't tune** (D16), and *"meets the target"* is not *"is the derived value"*.
  The guards only assert "enough". **The derivation has an order** — food before fuel — and
  **capacities are part of it** (D50).
- **`VillageFixtures.Village` and `data/sim.config.json` diverge, and bugs live in the
  gap.** Now a standing rule in METHODOLOGY §3.
- **Prefer readers to writers.** Ask the world a question rather than maintaining a flag:
  nothing to hash, nothing that can be set and not cleared. The recurring bug in this
  project is code reading state from where it used to live, and a bookkeeping flag is that
  shape.
- **When two fixes fight each other, the thing they are both working around is the bug.**
  D55: the map jumping and the sentence clipping were one bug, and the second fix deleted
  the first.
- **When a test fails, ask whether the test was right.** Several have been.
- **A forward-looking comment is a promise to come back** (D57). *"Until building placement
  exists"*, *"water is generated but nothing reads it yet"* — all true when written, all
  lies on a specific commit, none of them noticed.
- **PowerShell:** write commit messages to a file and use `git commit -F`; `-m` with long
  text gets mangled. Do not round-trip source through `Get-Content`/`Set-Content` — it
  corrupts the em-dashes this codebase is full of.
