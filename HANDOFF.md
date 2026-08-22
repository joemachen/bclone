# Handoff — bclone: **Phase 2 is one QA pass from done, and it is Joe's**

Read `CLAUDE.md`, then **`DESIGN.md` §0–§5 in full, §6, and §7 from D167 back to D142**, then
`METHODOLOGY.md`. `specs/crops-and-orchards.md` is now a **record** rather than a job — read §12
if you touch the crop numbers, and nothing else there needs you.
`specs/phase-2-the-village-you-can-play.md` is the checklist waiting on him.

---

## Where things are

**Branch `phase/2-wood-fuel-and-tools`.** The farm (D162) is in.

**Suite: 618 passing, 0 failing, 2 skipped of 620. Green** (was 589 / 0 / 2 of 591). Both skips
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

- **✅ THE FARM STOPPED ROTTING ITS OWN CROP** (D167). Joe: *"2x farmers planted 20 fields in
  the spring, and harvested only 9 in the fall."* His trail said every year was ~17 sown / ~5
  reaped. Two causes — nothing capped the sowing, and reapers walked home between every tile.
  **150 sown / 140 reaped now, 93% against ~30%.** Yield untouched, per his instruction.
- **✅ THE JITTER IS ANSWERED — but by the third fix, not the first.** D163 fixed a real bug
  (a cold villager with full arms could never warm up) that was **not** what Joe was watching.
  **D166 measured the actual one**: a household fetch from a store one tile from the door,
  four to six flips every thirty-odd ticks. Two changes: a fetch now fills the armful (food
  first, then firewood with what is left — one trip in three was pure waste), and
  `fetch_worth_this_share_percent: 25` stops anybody walking out for a trivial amount.
  **Measured: fetch legs 153 → 81, tile flips 211 → 143, nobody starving or freezing.**
  ⛔ **Not confirmed on screen** — Joe has said twice that it still looked wrong, so the only
  evidence that counts is him watching it again.

*(The farm is settled — Joe, 2026-08-16: "farmer is good now - dont change the yield." **The
crop numbers are locked; do not re-derive them without him.**)*

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

## ⭐⭐ Two directions Joe set, neither scheduled — `DESIGN.md §4` has the detail

- **GRIDLESS, like Foundation** — buildings placed at any angle, no tile map, paths that bend.
  **The largest architectural statement anybody has made about this project.** It touches the
  tile map, the one shared cost field, all three zone layers, the state hash and every unit the
  economy is derived in — and the real constraint is **determinism**: integer-only sim state
  (D2) is currently guaranteed *by* the grid. Fixed-point `Fixed` (Q32.32) is the door left
  open for it. **Do not start this inside a phase**; the first question when it is taken is
  whether the *sim* goes continuous or only the *presentation*, and those are very different
  costs.
- **MODS** — §3 has promised this since day one. The audit: content *values* are data ✅, but
  buildings, jobs, goods and terrain are **C# enums, hashed by position and pinned by goldens**,
  so a modder can change numbers and not add a building. **The standing discipline meanwhile:
  when you add a new kind of thing, ask whether it wants to be an enum value or a row in a data
  file.** The crop id is the one done right (*one crop, in a model shaped for many*), and
  retrofitting the others means touching the hash, the goldens and every call site at once.

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
- **⭐⭐ A MECHANISM CAN BE CORRECT AT EVERY STEP AND WRONG AS A YEAR** (D167). Every farm guard
  asked *does sowing work? does reaping work? does the store fill?* — all yes, all green — while
  the farm threw away two thirds of its food every autumn for ever. **Assert the outcome over a
  cycle, not only the steps.** `AFarmBringsInMostOfWhatItSows` is that guard.
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
- **⭐ THE AUDIT TRAIL IS EVIDENCE AND THE SUITE IS NOT.** D154, D157, D163 and D166 all came out
  of `src/Bclone.Game/logs/`. Joe's log path is in his header in every screenshot, and the files
  are on disk — **read them.**
- **⭐⭐ BUT READ THE WHOLE FILE, NOT THE FIRST MATCH (D166).** D163 diagnosed the jitter from
  four log lines, fixed a real bug, and declared it *the* cause — and Joe still saw the jitter,
  because the thing he was watching was a household fetch somewhere else in the same file.
  **Finding a cause is not finding the cause.** Sweep for the pattern across the run and count
  how often each shape occurs before believing any of them.
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
