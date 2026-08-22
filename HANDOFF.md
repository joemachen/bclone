# Handoff — bclone: **Phase 2 is one QA pass from done, and it is Joe's**

Read `CLAUDE.md`, then **`DESIGN.md` §0–§5 in full, §6, and §7 from D165 back to D142**, then
`METHODOLOGY.md`. `specs/crops-and-orchards.md` is now a **record** rather than a job — read §12
if you touch the crop numbers, and nothing else there needs you.
`specs/phase-2-the-village-you-can-play.md` is the checklist waiting on him.

---

## Where things are

**Branch `phase/2-wood-fuel-and-tools`.** The farm (D162) is in.

**Suite: see the D165 run.** Green (was 589 / 0 / 2 of 591). Both skips
are rulings, not debt (D143's unattended village; D134's granary cap). **The full run is 12–17
minutes.**

**The Godot view builds** — `dotnet build src/Bclone.Game/Bclone.Game.csproj`. The solution build
does **not** cover it (D11), and since D160 the view has **no automated verification of any
kind**: looking at it is the test.

---

## ⛔ THE JOB: one item, and it is not yours.

**`DESIGN.md §4`'s Definition of Done — four of five are closed (D162–D165).**

1. ✅ **Crops** — D162. Built, guarded, documented.
2. ✅ **A golden over a village that clears ground** — `FarmGoldenTests`, which does it and the
   crop seam in one run. D157's hole is closed.
3. ⛔ **A QA playthrough against a written checklist.** ✅ The checklist is written —
   `specs/phase-2-the-village-you-can-play.md`, 45 checks in the order the game is played.
   **⛔ JOE WALKS IT. Not you.** Since D160 the view has no automated verification of any kind,
   and his eyes are the test. **This is the only thing standing between the branch and PR #3.**
4. ✅ **The release blockers** — and clearing them found a third nobody had listed. See D164.
5. ✅ **`CHANGELOG.md`'s header** reconciled; the stale body kept and labelled.

**Then merge to `main` via PR #3.** Nothing else in the DoD is waiting on code.

### If Joe's walk turns up findings

That is the expected outcome, not a setback — **the last four bugs that mattered (D154, D157,
D162's leak, D163's jitter) were three-from-playing and one-from-reading, and none from the
suite.** Fix what he names, then merge. Do not start Phase 3 on an unmerged branch (D161).

---

## ⭐ Open, and all of these are Joe's calls

### 0. Still outstanding

- **Whether the jitter looks fixed on screen.** The log says it is; nobody has watched it.
- **⭐ Whether a 26-tile field and a two-seat farm read right in the hand.** The numbers are
  consistent and measured; whether a farm *feels* like a farm is a chair question.

*("Village decides" is closed — gone from the game entirely, D165.)*

### ✅ What the farm slice left open — closed by D165

**The field was not over-derived; the code was walking twice.** `HaulTheHarvest` asked the farm
store `IsFull` instead of whether it had room for the load, so every reap made two long walks.
Fixed, a farmer reaps the 13 tiles the derivation always promised, and `crop_yield_per_tile`
stays at 67. **The farmhouse has two seats**, stated in data rather than derived.

⚠️ **The guard that would have caught it first time now exists** —
`AFarmerCanActuallyReapTheFieldTheDerivationGivesThem`. Every other farm guard asked whether the
sums were self-consistent; none asked whether they described the village.

---

## ⚠️ Traps this session met, in the order they will cost you

- **⭐⭐ CHECK EVERY GUARD RED, AND COUNT THE REDS.** Three times this session, and **the third
  time it caught a guard that proved nothing.** The demand arm: **3 of 5** with it disabled,
  **2 of 5** with only the seasonality removed — the two that stay green both times are the
  anti-vacuity guards that assert zero. Then D163's jitter: two guards written, both green, and
  disabling the fix produced **one red of fifteen**. The emergent one read zero against the
  broken code too and was deleted. **Running says green; counting says which half of your
  green means anything.**
- **⭐ A LONG RUN IS THE WRONG INSTRUMENT FOR A COINCIDENCE** (D154, and D163 again). The jitter
  needs somebody cold *and* holding logs at the instant they arrive; the fixture village is warm
  and well stocked, so a whole winter of it never produces the case. Pose it through a seam.
- **⭐ `SimLoop` runs the systems and *then* advances the tick.** Third and fourth instances this
  week. The seam golden's first run reported *"0 lost to winter, 344 vanished unexplained"* — a
  harvest apparently being eaten by the harvest brush — and the bug was that the harness read
  `Clock.Season` one tick to the right of the event. **An off-by-one in a harness reads exactly
  like a broken feature**, and the temptation is to go and fix the feature.
- **⭐ A GREEN GOLDEN CAN MEAN "NOT COVERED"** (D157) — and it did again. The plan said the two
  50-year goldens were *supposed* to move once a farmer sowed. **They did not, because neither
  village ever places a farmhouse.** Say which of the two it is, measured, before you believe
  either.
- **A derivation that reads a number derived from itself is not a derivation.** *"Enough yield
  that a farm's seats feed a household"* produced a farmhouse with fourteen seats and 173 food
  from one tile. State the target as a **comparison** against something already derived.
- **⭐⭐ AND THE MIRROR OF THIS PROJECT'S USUAL RULE, WHICH COST A ROUND TRIP TO LEARN (D165).**
  *Measure, do not reason from the code* is right — but **a measurement that disagrees with a
  derivation has found a bug in one of them, and it is worth knowing which before rewriting the
  other.** A farmer measured at 5 tiles against 13 budgeted looked like a bad budget; it was
  `HaulTheHarvest` asking `IsFull` instead of *room for the load* and walking twice per tile.
  The budget was rewritten to fit the bug, produced 216 food from one tile, and had to be put
  back. **Ask what would have to be true for the derivation to be right, and go and look.**
- **A control tested at its predicate and never at its deposit is a control nobody has tested**
  (D144). The market's widened reach needed the *loading* branch as well as the *choosing* one,
  or traders walk to the farm and stand there.
- **A new deposit path means a new leak.** `RetireWorkplace` had ignored `Workplace.Store` for
  five phases — correctly, because nothing wrote to one. The farm's buffer made demolition
  destroy up to 100 food silently. **Found by reading the method, not by a failing test.**
- **⭐ THE AUDIT TRAIL IS EVIDENCE AND THE SUITE IS NOT.** D154, D157 and now **D163** all came
  out of `src/Bclone.Game/logs/`. Joe's log path is in his header in every screenshot, and the
  files are on disk — **read them.** D163 took four lines of one to diagnose a bug that had been
  in the game since D45 and that the whole suite had never once noticed.
- **A comment promising the code is general, over code that names a kind.** The farm shipped
  with no work-ground brush because `Main.cs` read `Kind: JobKind.Forester` under a comment
  saying *"so the next one needs no line here"*. **When you add a thing, grep for the comments
  that claim they already cover it.**
- **`python` string edits die on this repo's CRLF *and* its emoji** (`UnicodeDecodeError:
  charmap`), and `python` is not even on PATH here. Use the Edit tool.
- **`dotnet test` buffers stdout when redirected**, so a background run looks frozen at zero
  lines for a quarter of an hour. `Get-Process testhost` and look at CPU to tell working from
  hung.
- **⚠️ AND DO NOT WRITE A WAIT-LOOP FOR A RUN THAT IS ALREADY IN THE BACKGROUND.** It is
  redundant — the completion notification arrives by itself — and it adds a failure mode that
  cost this session two shells spinning for thirteen hours. The loops waited for `Passed!`
  against a file produced with `--logger "console;verbosity=detailed"`, which ends with
  **`Test Run Successful.`** instead. **The two output formats end with different strings**, so
  the condition could never be true. *A wait-loop whose condition cannot be met is a vacuous
  guard wearing a different hat: it looks like it is watching something and it is watching
  nothing.*
- **⭐ A DEFAULT YOU DID NOT SET IS STILL A DECISION SOMEBODY MADE.** `setup-godot`'s
  `include-templates` defaults to **false**, so `release.yml` would have failed at the export step
  even with a perfect preset — and only at the first `v*` tag, because nothing else runs an
  export. **Found by running the export locally instead of trusting that committing the preset
  was enough.** When you clear a blocker, try the thing it was blocking.
- **The full suite is 12–17 minutes.** Background it, and **do not start a second one** — the
  first holds a lock on `Bclone.Sim.dll` and no test project will build until it exits, so a
  second run fails on the copy step and wastes the wait.

---

## Working with Joe

Technical, not a game/systems programmer. Casual, direct; **push back honestly**. **End every
message with the explicit ask**, or he cannot tell who is blocking whom.
