# Handoff — bclone, 2026-07-31

Read `CLAUDE.md`, `DESIGN.md` and `METHODOLOGY.md` in full first, then the spec for
whatever you are about to touch. This file is the shortcut, not a substitute.

---

## State of play

- Branch `phase/2-wood-fuel-and-tools`, working tree clean, everything pushed, **green at
  every commit**.
- **357 tests green.** The suite takes ~4m20s; the long 200- and 300-year runs dominate.
- Phase 0 and Phase 1 are merged to `main` (PRs #1, #2). Everything since is on this
  branch, unmerged on Joe's standing call: Phase 2's Definition of Done is not met, and
  merging now would make `main` a checkpoint rather than a completed phase.
- **Nothing is broken.** `wip/idle-winter` is superseded and can be deleted — its work
  landed on the branch with D52.

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

## The idle winter — closed, and the diagnosis in the last handoff was wrong

**`wip/idle-winter` is superseded. Its work is on the branch, D52 records it.** Read that
entry before touching the labour quota.

The bug it fixed was real: `LabourQuota.For` never asked what season it was, so in winter
the food floor was still staffed and `foragers += free` dumped every spare hand onto
berry patches with nothing on them. That half is kept.

The half that was invented — **spare winter hands to the woods, bounded by whether any
shed still had room** — is deleted. It bounded the shed, not the work. Demand for timber
is already answered twice in the same method and funded first, so every hand it added was
cutting logs the village had no use for. The sheds then sat at 617–703 of 717 capacity for
the whole run; the shed is one room (D33), so firewood had nowhere to land, and the birth
gate reads a household's own firewood. **Mean population 14 where it now holds 22 — full
larder, full shed, nobody starving, nobody freezing.**

**It was never a market bug.** `MarketersWanted` has counted errands, not spare hands,
since D36 — the previous handoff's hypothesis was wrong, and so was its fix. What
actually happened: marketers shuttled the scarce firewood out to the homes, so **the
market arm was the healthy one (mean 21) and the CONTROL collapsed.**
`TheMarketShortensTheWalkForFood` failed because the village it measures against had
died down to a size where it barely walked anywhere.

The previous handoff's numbers (5,702 / 3,044 fetch trips) do not reproduce; the measured
values were 17,995 / 9,705, and the populations quoted were end-of-run rather than mean.
**Do not carry a number forward without re-measuring it.**

**Winter is still mostly idle, and that is now the honest answer** rather than a bug: with
the food floor gone the village wants no extra hands, because it already has its logs.
D44's own forward note is the fix — winter wants herding and slaughtering (D39), not
make-work in the woods. That is a design gap, and it is Joe's call when to take it.

---

## Next up, in order (Joe's call)

1. **Shelter and exposure (D45)** — fully specified, unbuilt. Cold becomes positional:
   **15 days outdoors unclothed, 25 days sheltered without a fire, reset by a burning
   one** (60 and 100 ticks). Replaces `HearthSystem`'s household accounting. Note 25 days
   is *less* than a 30-day winter, so an unheated house can still kill within one season —
   `CauseOfDeath.Cold` stays live. `freezing_ticks` goes 80 → 100 and splits in two.
   **Must be survivable with no clothing in the game.**
2. **Clothing** — leather/wool/cotton, gated behind D19/D39's production tier. It removes
   the outdoor danger, and so is **what unlocks winter as a working season**.
3. **Work-in-place instead of round trips.** Joe's idea and the biggest payoff on the
   board — see the open decision in DESIGN.md §5. Villagers travel to work and *stay*,
   eating on site, so distance becomes a one-off commute rather than a per-unit tax.
   That kills the 7-tile home-to-work fence and lets the village finally use the
   120×80 valley it occupies twelve tiles of. **Re-derives the food economy a fourth
   time — give it a fresh session and the no-op-first discipline.**
4. **The seasonal yield curve** (`specs/environment-and-seasons.md` §5.1), last and on
   its own terms.
5. **Winter work that is not the woods** (D44's forward note, D39's roadmap). Not
   scheduled, and named here because D52 turned it from "a fix I can make" into "a thing
   the game does not have yet". Herding, slaughtering, fishing.

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
- **A comparative test has TWO villages, and either one can be the broken one.** D52: the
  market test failed and a whole session went looking inside the market. The market arm
  was healthy; the *control* had collapsed. **Check the control's population before
  believing what the comparison says**, and never read a per-head figure off an
  end-of-run count — that is a phase of an oscillation, not a mean.
- **A gate on a store is not a gate on the work.** *"Is there room for one more?"* answers
  a question about the shed. The question the quota has to ask is *"does anything in the
  village want this?"* — D52, and D33 before it in the opposite direction.
- **Numbers in a handoff are hearsay until re-measured.** D52's predecessor recorded fetch
  trips that were off by 3× and populations taken at the wrong instant, and both were
  quoted back with confidence.
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
