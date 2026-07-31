# Handoff — bclone, 2026-07-31

Read `CLAUDE.md`, `DESIGN.md` and `METHODOLOGY.md` in full first, then the spec for
whatever you are about to touch. This file is the shortcut, not a substitute.

---

## State of play

- Branch `phase/2-wood-fuel-and-tools`, **41 commits ahead of `main`**, working tree
  clean, everything pushed, **green at every commit**.
- **355 tests green.** The suite takes ~3m30s; the long 300-year runs dominate.
- Latest commit: `a392682`.
- Phase 0 and Phase 1 are merged to `main` (PRs #1, #2). Everything since is on this
  branch, unmerged on Joe's standing call: Phase 2's Definition of Done is not met, and
  merging now would make `main` a checkpoint rather than a completed phase.
- **One side branch: `wip/idle-winter`.** Unpushed, one red test. See §"The one thing
  that is broken" — it is the next task.

**Run it:**
```
D:\Projects\Godot\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64.exe --path src/Bclone.Game
```

---

## The trap that will bite you first

**`bclone.sln` does not contain the Godot project.** `dotnet build bclone.sln` compiles
the sim and the tests only. A build menu was once written, wired up, and silently never
appeared, because the assembly Godot ran was a day old.

- Building the view: `dotnet build src/Bclone.Game/Bclone.Game.csproj`
- CI has a separate step for it.
- **Always rebuild the view explicitly before launching to check a change.** The
  tell-tale that you are looking at a stale build is text on screen you know you changed.
- **Nothing in `src/Bclone.Game` can be unit-tested at all** (D11 puts it outside the
  solution). View changes are verified by running the game and by nothing else. Say so
  when you report one.

---

## What landed this session (7 commits)

**The building selection panel.** Click anything on the map and it says what it is.
Selection is a **tile**, not a building id — three occupants live in three lists with
three id spaces, and the market is deliberately both a store and a workplace at one
position (D36's seam), so asking "what is here?" describes it correctly. Every branch
ends in something actionable: a hut with no logs says so rather than showing zeroes.

**The timber leak (D48), and it is the third time.** `UnloadAtHome` dropped carried logs
into the household larder, and **nothing in the sim can spend a log in a larder** — the
only reader of `Household.Stockpile.Logs` is the state hash. 240 logs frozen in two
houses for twenty years while a site wanted 28 and a couple wanted 30. Fixed with an
*invariant* rather than a fourth patch.

**Thirty-day seasons and an hour-long life (D49).** Full re-derivation; see the numbers
in the decision entry. Pacing is now stated as *a life takes 60 real minutes at 4×*
rather than as a year length.

**Building capacities were derived too, and had been outgrown (D50).** The shipped file
had 3 woodcutter seats where the economy required 8.

**A dead worker's job is filled at once (D47)**, which is what makes D46's three-year
reshuffle affordable. Plus a header alert for work the village wants that nobody is doing.

**Player staffing (D51).** The player sets *how many*, never *who*. Default is "let the
village decide". D15's guard narrowed from name-matching to signature-matching.

---

## The one thing that is broken

**`wip/idle-winter` — the idle winter fix regresses the market.**

The bug it fixes is real and confirmed: `LabourQuota.For` never asked what season it
was, so in winter the food floor was still staffed and `foragers += free` dumped every
spare hand onto berry patches with nothing on them. `BehaviorSystem` then sent them all
home. **A quarter of the working year, idle, for whoever held the commonest job.**

The branch fixes that — no foragers in winter, spare hands to the woods while the sheds
have room — and breaks `TheMarketShortensTheWalkForFood`. Measured over 100 years:

| | population | fetch trips |
|---|---|---|
| with a market | 23 | 5,702 |
| without | 33 | 3,044 |

**Worse per head as well as in total**, so it is not a metric artifact. Unverified
hypothesis: winter frees a lot of hands, `MarketersWanted` is funded last out of
whatever is spare, so the market is fully staffed all winter for the first time and
churns goods nobody needed moved — adjacent to D36's recorded expensive mistake.

**The agreed fix, not yet built:** bound `MarketersWanted` by *the work there actually
is* — households genuinely below target, goods genuinely stranded — rather than by spare
hands. D51 gives the *player* an override but does not change the default, so this is
still open. Needs a 300-year measurement to trust.

**Also worth fixing while in there:** that test's metric is a raw aggregate and passed
for the wrong reason before. Per-capita is the honest form. Same shape as D34's lesson —
an assertion about a window is not an assertion about a system.

---

## Next up, in order (Joe's call)

1. **The market regression**, as above. Unblocks `wip/idle-winter`.
2. **Shelter and exposure (D45)** — fully specified, unbuilt. Cold becomes positional:
   **15 days outdoors unclothed, 25 days sheltered without a fire, reset by a burning
   one** (60 and 100 ticks). Replaces `HearthSystem`'s household accounting. Note 25 days
   is *less* than a 30-day winter, so an unheated house can still kill within one season —
   `CauseOfDeath.Cold` stays live. `freezing_ticks` goes 80 → 100 and splits in two.
   **Must be survivable with no clothing in the game.**
3. **Clothing** — leather/wool/cotton, gated behind D19/D39's production tier. It removes
   the outdoor danger, and so is **what unlocks winter as a working season**.
4. **Work-in-place instead of round trips.** Joe's idea and the biggest payoff on the
   board — see the open decision in DESIGN.md §5. Villagers travel to work and *stay*,
   eating on site, so distance becomes a one-off commute rather than a per-unit tax.
   That kills the 7-tile home-to-work fence and lets the village finally use the
   120×80 valley it occupies twelve tiles of. **Re-derives the food economy a fourth
   time — give it a fresh session and the no-op-first discipline.**
5. **The seasonal yield curve** (`specs/environment-and-seasons.md` §5.1), last and on
   its own terms.

---

## How Joe wants to work

- **Keep the remote branch green.** Hold WIP in local commits or a side branch; push once
  a slice is green. Done twice this session and it was right both times.
- **Report at meaningful transitions, don't grind. When a measurement contradicts the
  plan, stop and say so.** This worked well this session — twice.
- **End every message with the explicit ask.**
- Spec-first for anything non-trivial; record decisions in DESIGN.md §7 rather than
  leaving reasoning in chat.
- Joe finds real bugs by *playing*, and by asking why something is the way it is. Two of
  the best findings this session came from his questions, not from the plan.
- **Push back when a design choice is wrong** — he asked for this explicitly, and D51 is
  the example: his first shape for player staffing would have made micromanagement
  mandatory, the flag was welcome, and the agreed version is better than either.

---

## Lessons that would be expensive to rediscover

- **A village that stops growing while its stores are full is a distribution bug until
  proven otherwise.** D48 presented as a demographic wave — nobody starved, nobody froze,
  the granary was full — and so did D34. That disguise has now cost two long
  investigations.
- **Derive, don't tune** (D16), and *"meets the target"* is not *"is the derived value"*.
  The guards only assert "enough".
- **The derivation has an order** — food before fuel. And **capacities are part of it**
  (D50); every previous re-derivation moved yields and forgot buildings.
- **A longer year is mostly self-cancelling.** D49: an adult eats twice as much and gets
  twice the trips, so `gather_yield` barely moved. The exception is winter, where trips
  do not help because there is nothing to gather — which is why `stockpile_target`
  doubled and nothing else did.
- **`VillageFixtures.Village` and `data/sim.config.json` diverge, and bugs live in the
  gap.** D50 lived there entirely; D48 was four times worse in the shipped file. Assert
  invariants against **both**.
- **Measure, do not pattern-match.** Every diagnosis this session that was reasoned from
  precedent was wrong, and every one that came from a probe was right.
- **An assertion about a window is not an assertion about a system**, and a raw aggregate
  is not a rate. The market test passed for the wrong reason for months.
- **Prefer readers to writers.** D47 asks the world "is anyone dead still holding a job?"
  rather than keeping a flag: nothing to hash, nothing that can be set and not cleared.
  The recurring bug here is code reading state from where it used to live, and a
  bookkeeping flag is that shape.
- **When a test fails, ask whether the test was right.** `HandingItBackRestores...`
  demanded a stand be re-staffed within a few years; there are two stands and the village
  only wants loggers when it wants wood, so the test was asserting the quota was broken.
- **PowerShell:** write commit messages to a file and use `git commit -F`; `-m` with long
  text gets mangled. Do not round-trip source through `Get-Content`/`Set-Content` — it
  corrupts the em-dashes this codebase is full of.
