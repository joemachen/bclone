# Handoff — bclone, 2026-07-30

Read `CLAUDE.md`, `DESIGN.md` and `METHODOLOGY.md` in full first, then the spec
for whatever you are about to touch. This file is the shortcut, not a substitute.

---

## State of play

- Branch `phase/2-wood-fuel-and-tools`, **35 commits ahead of `main`**, working tree
  clean, everything pushed, **CI green on every push**.
- **342 tests green.** Suite takes ~2m20s (the long property runs dominate).
- Latest commit: `0cf03a8`.
- Phase 0 and Phase 1 are merged to `main` (PRs #1, #2). Everything since is on this
  branch. Joe's call, twice: **leave it unmerged** — Phase 2's Definition of Done is not
  met, so merging now would make `main` a checkpoint rather than a completed phase.

**Run it:**
```
D:\Projects\Godot\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64.exe --path src/Bclone.Game
```

---

## The trap that will bite you first

**`bclone.sln` does not contain the Godot project.** `dotnet build bclone.sln` compiles
the sim and the tests only. For most of a session I reported "Build succeeded" while the
view had not been compiled at all — a build menu was written, wired up, and silently
never appeared, because the assembly Godot ran was a day old.

- Building the view: `dotnet build src/Bclone.Game/Bclone.Game.csproj`
- CI now has a separate step for it (added this session).
- **Always rebuild the view explicitly before launching the game to check a change.**
- Tell-tale that you are looking at a stale build: text on screen that you know you
  changed. That is how this was caught.

---

## What landed this session (23 commits)

**Storage finished (D30 closed).** Capacity as a real constraint, and the manned market.
The granary's capacity is the village's population ceiling — build another and the
village grows past its old one. The marketer is the first job that *produces nothing*.

**Seeded map generation (D18).** Terrain, a wandering river, forest stands, forage sites
and the founding site all come from the run's seed in a fixed draw order, hashed, with a
golden map test. Literal coordinates left the config and became *rules*.

**Homes are placed, not spiralled.** `Household.ChooseSite` scores a site on the two
trips a household makes — out to work, over to the store — with distance-to-work a hard
bound the economy is derived against.

**Water is impassable (D40).** Real pathfinding via a Dijkstra flow field per building,
which works because *every travel query in this game has a building at one end*.

**Building placement (D43), slices 1–3.** The first systems that answer to the player:
- stores went plural (D38) — 15 call sites, each a decision not a rename;
- mark a building and the village hauls materials and raises it, competing for hands;
- **the residential brush** (D42) — paint where the village may live, it builds inside
  when it has a reason to, and asks by name when it runs out.

**A full audit log.** Every run writes `logs/bclone-<timestamp>.log` with everything down
to DEBUG — every state change, load carried, job and refusal, tick-stamped. Path is shown
in the header next to the seed.

**Hunger slowed** to a meal every 2.8 days (was 2.0), economy re-derived.

---

## Next up, in order

1. **Phase 2's headline: environment/seasons depth + biomes (§2.5).** *Push for this.*
   We are seven systems into a placement detour that has been worth it, but the phase's
   actual subject is untouched and the tracker has said so since before the map generator.
2. **The harvest brush** (D42/D43, placement slice 4) — which forest to fell. Makes
   terrain **mutable**, which forces the flow-field cache invalidation D41 predicted, and
   gives §2.3 (resource exhaustion) its first real machinery.
3. **The planting brush** (slice 5) — gated behind managed forestry, so it waits for the
   tech tree (§2.7), whose first concrete node it becomes.
4. **Bridges** (D40) — technology then building; needs the tech tree and placement.
5. **Make the river matter** (`specs/pathfinding-and-water.md §12.3`) — water is
   impassable and provably so, but the generator steers every village onto the side of
   the valley with all the work on it, so *nothing ever needs to cross*. Belongs with
   bridges.

`specs/food-catalog.md` is a content catalog Joe wrote (foods and production chains); it
is reference material, not an implemented system, and its mechanics want their own spec.

---

## Open questions for Joe

- **Does 2.8 days between meals feel right?** 6/tick (3.2 days) is available and the cost
  is known and stated: the population band widens from 17–32 to 12–31.
- **Is the residential brush radius (2 tiles) right?** And does the village's request for
  more land land, or is it easy to miss?
- **What is missing from the audit log?** Construction and market events get state changes
  but no dedicated lines; nothing logs *why* a particular store was chosen for a fetch.

---

## How Joe wants to work

- **Keep the remote branch green.** Hold WIP in local commits; push once a slice is green.
  (Two WIP commits this session were held back deliberately and pushed once fixed.)
- **Report at meaningful transitions, don't grind.** This has been said more than once and
  I have still overrun it. When a measurement contradicts the plan, stop and say so.
- **End every message with the explicit ask.**
- Spec-first for anything non-trivial (METHODOLOGY §2); record decisions in DESIGN.md §7
  rather than leaving reasoning in chat.
- Joe finds real bugs by *playing*. Two of the worst this session were caught that way,
  not by tests. When he reports something odd, reproduce it in a test before theorising.

---

## Lessons that would be expensive to rediscover

- **Derive, don't tune** (D16) — and note that *"meets the target"* and *"is the derived
  value"* are different claims. Leaving `gather_yield` at an old higher number passed
  every test and would have made food a non-constraint.
- **The derivation has an order.** `firewood_per_split` derives from the spare hands the
  *food* economy leaves, so re-deriving it before `gather_yield` produces nonsense.
- **Measure, do not pattern-match.** The population-wave diagnosis in
  `storage-and-distribution.md §12` was coherent, cited a real precedent in this codebase,
  and was wrong. The real cause was a plain bug (D34: the dead were never removed from
  their household, so households ratcheted into sterility).
- **An assertion about a window is not an assertion about a system.** The acceptance test
  ran 150 years; the collapse completed at 180. Ask every long-run test what it would do
  if the run continued.
- **The recurring bug shape is "the right stuff in the wrong place"**, and its cousin,
  code reading state from where it used to live. This session it appeared as: fetching
  food from the market and coming home with firewood; buildings placed in the river;
  a builder quota that wanted the whole village.
- **Tests use `VillageFixtures.Village`; the game loads `data/sim.config.json`.** They can
  diverge. `ShippedConfigTests` now runs the real file — add to it rather than letting
  that gap reopen.
- **Anti-vacuity guards** (D7) on anything that could silently stop testing what it claims.
- **Godot/PowerShell:** write commit messages to a file and use `git commit -F`;
  here-strings and `-m` with long text get mangled. Do not round-trip source files through
  `Get-Content`/`Set-Content` — it corrupts the em-dashes the codebase is full of.
