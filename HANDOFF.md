# Handoff — bclone: **Joe's five, and Phase 2 waiting on one QA walk**

Read `CLAUDE.md`, then **`DESIGN.md` §0–§5 in full, §6, and §7 from D167 back to D142**, then
`METHODOLOGY.md`. `specs/crops-and-orchards.md` is a **record** rather than a job now — read §12
only if you touch the crop numbers, and **do not touch them** (see below).

---

## Where things are

**Branch `phase/2-wood-fuel-and-tools`**, eleven commits, tree clean, **not pushed**. Head is
`567028f` — *"The farm stops throwing away most of its own crop"*.

**⚠️ SUITE STATE IS UNCONFIRMED AND THAT IS THE FIRST THING TO DO.** The run before the last
commit was **621 passing / 3 failing / 2 skipped of 626**, and the three failures were the three
goldens — which were then **re-taken and verified green individually** (both arms of
`AVillagePlayedWithoutLimits` and all of `FarmGoldenTests`, 5 of 5). A confirming full-suite run
was started and **produced no result** — its output stops after two lines, so the process ended
without reporting. **Nothing is being claimed from it.**

```bash
dotnet test bclone.sln --nologo -v q
```

**Expect 626 passing / 0 failing / 2 skipped.** If anything else is red it is from D166/D167 —
the fetch bar, the mixed armful, the farm sowing cap, reapers returning to the rows — and the
audit trail is the place to look, not the code.

**The Godot view builds separately** — `dotnet build src/Bclone.Game/Bclone.Game.csproj` (D11).
Since D160 the view has **no automated verification of any kind**: looking at it is the test.

---

## JOE'S FIVE, from 2026-08-22. Three are open.

### 1. ⛔ THE JITTER IS STILL THERE — *"a little bit"*, and he has now named the exact case

> *"when a farmer is sowing/harvesting a field it seems to 'bounce' between two tiles for a few
> ticks. same with a forester planting/harvesting trees."*

**This is the third distinct cause, and the first two are genuinely fixed** — do not re-fix them:

| | cause | fixed in |
|---|---|---|
| 1 | a cold villager holding logs was flipped out of the house the tick after arriving | D163 |
| 2 | a household fetch from a store one tile from the door | D166 |
| 3 | **a worker on painted ground bouncing between two tiles — OPEN** | — |

**READ THE NEW LOG FIRST: `src/Bclone.Game/logs/bclone-20260822-000011.log`.** It is the run from
his screenshot and it is on disk. **D163's mistake was diagnosing from four lines and declaring it
*the* jitter** — sweep the whole file, count how often each shape occurs, and only then believe
one. The extraction that found cause 2 is written out in `DESIGN.md` D166 and is worth reusing:
scan the `DEBUG behavior` lines for a villager whose position returns to where it was two entries
ago within three ticks, then group the hits by what they were doing.

**Standing hypothesis, unverified.** `NextGroundToWork` (forester) and `NextFieldToWork` (farmer)
both pick *the nearest owned tile from where the villager is standing*. A forester in
`FellAndPlant` fells the last tree at A, walks to B, finds nothing to fell there, and is sent back
to plant A — genuinely alternating between two adjacent tiles. **A sower carries nothing and
should simply walk a row, so if a sower bounces there is a second thing going on** — and the
sowing case is the cleaner one to diagnose, because it has no hauling in it at all.

### 2. ⛔ THE FARM'S BRUSH SHOULD SAY WHEN THE FIELD IS TOO BIG FOR ITS FARMERS

> *"it would be helpful if, when painting the farm size, the UI told the user 'at this size you
> dont have enough farmers to utilize the land - add more farmers or make your field smaller'
> (which the user can choose to ignore and 'waste' land if they want)."*

**Most of this exists already, which makes it a small job.** `SimWorld.PaintWorkGround` returns a
`PlacementVerdict.Yes(...)` carrying a sentence once tiles exceed `WorkGroundAllowanceFor`, and
D42's rule is that a brush speaks **once per stroke**. What is missing is that the sentence is
written for a forester — *"…and 1 pair of hands to keep them — enough for 24. The rest will go
untended."*

**Give it a farm-shaped wording naming the two remedies**, as he asked. It must stay a **warning
and never a refusal** (D86, D43) — he said so explicitly: *"the user can choose to ignore and
waste land."* The panel line (`Ground — 33 tiles, enough hands for 0`) also reads badly at zero
hands and is worth a sentence rather than a fragment.

### 3. ⛔ A UI REFACTOR — the right-hand columns are far too wide

> *"we're going to have to do another UI refactor. look at the attached screenshot to see how
> dumb the width of the windows on the right side of the screen are"*

**In his screenshot the right column takes over half the window** while the map is a narrow strip
in the middle — and D149 set each column to **27% of the window, floored and capped**. So either
that share is not reaching the right column, or **a wide child is holding it open**, which is
D149's own finding: *"a column's minimum width is its widest child's, and a probe over the control
tree found six stock-limit rows at 438 holding it at 450."*

**Start with the same probe D149 used** — walk the control tree and print every child's minimum
width — rather than guessing at the layout. Likely culprits are the village-log lines and the
*Who they are, and why* sentences, which are long and may not be wrapping.

**D55 and D149 are both records of this exact area biting.** Read them before touching `BuildUi`.

### 4. ✅ MODS — Joe went further than §3 does

> *"modders should be able to add buildings, essentially add anything to the game."*

Recorded in `DESIGN.md §4`. **This is stronger than the existing promise** and it changes what
data-driven has to mean: `BuildingKind`, `JobKind`, `Goods` and `Terrain` are **C# enums, hashed
by position and pinned by every golden**, so today a modder can change numbers and cannot add a
building. `crops-and-orchards.md §4` is the one place done right — *one crop, in a model shaped
for many*, with the crop id in data — **and that is the template.**

**Nothing to do now.** Standing discipline: **when you add a new kind of thing, ask whether it
wants to be an enum value or a row in a data file.** Cheap at the time; retrofitting means
touching the hash, the goldens and every call site at once.

### 5. ✅ THE QA CHECKLIST IS GOOD — *"QA checklist is good"*

`specs/phase-2-the-village-you-can-play.md`, 45 checks. ⚠️ **It is not clear whether he has WALKED
it or approved the document.** DoD item 3 is the walk, so **ask him plainly** and do not tick the
item off on this evidence.

---

## What landed this session (D162–D167)

- **The farm** (D162) — farmhouse, painted field, sow and reap, the first live `Workplace.Store`,
  the hauling, the market's widened reach, and the crops-by-brush golden that also closed D157's
  open hole.
- **The jitter, twice** (D163, D166) — see the table above.
- **Phase 2's Definition of Done** (D164) — QA checklist written, `VERSION` wired into the build
  and shown in the shell, `export_presets.cfg` committed, **and a third release blocker nobody had
  listed**: `setup-godot`'s `include-templates` defaults to false, so `release.yml` would have
  failed at the export step at the first tag.
- **"Village decides" gone from the game** (D165), and the farmhouse at two seats.
- **The farm stops rotting its crop** (D167) — about 30% brought in, now 93%.

**⛔ THE CROP NUMBERS ARE LOCKED** — Joe: *"farmer is good now - dont change the yield."* Do not
re-derive `crop_yield_per_tile`, `sow_ticks`, `reap_ticks`, `farmhouse_seats` or `farm_store_cap`
without him.

---

## Phase 2's Definition of Done — one item

`DESIGN.md §4`. Items 1, 2, 4 and 5 are closed. **Item 3 is a QA playthrough walked by Joe**, and
it is the only thing between this branch and **PR #3**. Do not open Phase 3 on an unmerged branch
(D161).

---

## Traps, in the order they will cost you

- **⭐⭐ CHECK EVERY GUARD RED, AND COUNT THE REDS.** Five times this session, and **twice it caught
  a guard that proved nothing.** D163's emergent jitter guard read zero against the broken code too
  (1 red of 15, not the 2 expected) and was deleted. D166's first bar guard was aimed at **food**,
  which has a stronger gate, and scored **0 reds of 2** until it was re-aimed at firewood.
  *Running says green; counting says which half of your green means anything.*
- **⭐⭐ A MECHANISM CAN BE CORRECT AT EVERY STEP AND WRONG AS A YEAR** (D167). Every farm guard
  asked *does sowing work? does reaping work? does the store fill?* — all green — while the farm
  threw away two thirds of its food every autumn. **Assert the outcome over a cycle, not only the
  steps.**
- **⭐⭐ A MEASUREMENT THAT DISAGREES WITH A DERIVATION HAS FOUND A BUG IN ONE OF THEM** (D165), and
  it is worth knowing which before rewriting the other. A farmer measured at 5 tiles against 13
  budgeted looked like a bad budget; it was one word in `HaulTheHarvest`. The budget was rewritten
  to fit the bug, produced 216 food from a single tile, and had to be put back.
- **⭐⭐ FINDING A CAUSE IS NOT FINDING THE CAUSE** (D163, then D166, still open). Diagnosing from
  one excerpt and declaring it solved has cost two rounds of Joe re-reporting the same symptom.
- **⭐ THE AUDIT TRAIL IS EVIDENCE AND THE SUITE IS NOT.** D154, D157, D163, D166 and D167 all came
  out of `src/Bclone.Game/logs/`. The files are on disk and the path is in his header.
- **A comment promising the code is general, over code that names a kind.** The farm shipped with
  no work-ground brush because `Main.cs` read `Kind: JobKind.Forester` under a comment saying *"so
  the next one needs no line here"*.
- **A new deposit path means a new leak.** `RetireWorkplace` ignored `Workplace.Store` for five
  phases — correctly, until the farm wrote to one.
- **⭐ A DEFAULT YOU DID NOT SET IS STILL A DECISION SOMEBODY MADE** — `include-templates: false`.
  When you clear a blocker, **try the thing it was blocking**.
- **`python` is not on PATH**, and string edits die on this repo's CRLF and its emoji. Use
  `perl -0777`, or the Edit/Write tools for anything with quoting in it — a heredoc containing an
  `awk` one-liner cost this session two attempts.
- **`dotnet test` buffers stdout when redirected**, so a background run looks frozen at zero lines.
  `Get-Process testhost` and watch CPU to tell working from hung.
- **⚠️ DO NOT START A SECOND SUITE RUN WHILE ONE IS GOING.** The first holds a lock on
  `Bclone.Sim.dll` and **no test project will build until it exits** — this session lost time to
  it twice, the second time after writing the warning down.
- **⚠️ DO NOT WRITE A WAIT-LOOP FOR A BACKGROUNDED RUN.** It is redundant, the completion
  notification arrives by itself, and two such loops spun for thirteen hours here waiting for
  `Passed!` in a file written by `--logger console;verbosity=detailed`, which ends
  `Test Run Successful.` instead.
- **⚠️ IF A NUMBER GOES INTO A DOCUMENT, IT COMES FROM A RUN.** Three suite counts were written
  from arithmetic before the run finished this session; all three were wrong.

---

## Two directions Joe set — `DESIGN.md §4` has the detail, neither is scheduled

- **GRIDLESS, like Foundation** — any-angle placement, no tile map, paths that bend. **The largest
  architectural statement anybody has made about this project.** It touches the tile map, the one
  shared cost field, all three zone layers, the state hash and every unit the economy is derived
  in — and the real constraint is **determinism**: integer-only sim state (D2) is currently
  guaranteed *by* the grid. Fixed-point `Fixed` (Q32.32) is the door left open for it.
  **Desire-path roads (§2.6) is the one pillar that would gain outright.** Do not start it inside a
  phase; the first question when it is taken is whether the *sim* goes continuous or only the
  *presentation*, and those are very different costs.
- **MODS THAT CAN ADD ANYTHING** — see item 4 above.

---

## Working with Joe

Technical, not a game or systems programmer. Casual, direct; **push back honestly**. **End every
message with the explicit ask**, or he cannot tell who is blocking whom. **His play is the best
bug-finder this project has** — of the last six bugs that mattered, five came from him playing and
one from reading code. None came from the suite.
