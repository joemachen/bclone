# Changelog

All notable changes to **bclone** are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> **Pre-1.0:** the game is in active development. `v1.0.0` will be the first
> real public release (see `METHODOLOGY.md §5`). Until then, log everything
> under **[Unreleased]** as you go; at release time, move those entries under
> a stamped `## [X.Y.Z] - YYYY-MM-DD` heading and start a fresh Unreleased block.

Categories (only include the ones you use): **Added**, **Changed**,
**Deprecated**, **Removed**, **Fixed**, **Security**.

---

## [Unreleased]

### Added
- Project scaffolding: `DESIGN.md` (vision, pillars, architecture, build order),
  `CLAUDE.md` (AI working agreement), `METHODOLOGY.md` (engineering standards).
- Repo tooling: `.gitignore`, `README.md` with setup directions,
  `run.bat` / `test.bat` local runners with timestamped logging.
- Tag-gated `release.yml` GitHub Actions workflow (dormant until the first `v*` tag).
- **Tech stack settled** — C# (.NET 8) + Godot 4, with the simulation in a
  Godot-free class library. See `DESIGN.md` §7 decisions D1–D3.
- `specs/tick-loop.md` — spec for the deterministic tick loop, written before
  the code (METHODOLOGY §2).
- `bclone.sln` with `src/Bclone.Sim` (simulation core) and
  `tests/Bclone.Sim.Tests`; shared build settings in `Directory.Build.props`;
  `VERSION` as the single source of version truth.
- **Deterministic fixed-timestep tick loop**: `SimLoop` advances by tick *count*
  and never sees a duration; `FixedTimestepDriver` owns the only clock read and
  converts elapsed real time into whole ticks.
- Determinism primitives: `DeterministicRandom` (PCG32 with explicit,
  serializable state) and `StateHash` (FNV-1a fingerprint of sim state).
- Minimal structured logger — leveled, subsystem-tagged, and tick-stamped so any
  line ties back to an exact sim state (METHODOLOGY §4).
- Data-driven config (`data/sim.config.json`) parsed with comments and trailing
  commas allowed, so content files can explain themselves to modders.
- Test suite (92 tests) including the P0 determinism test, anti-vacuity guards
  that prove it can fail, and a PCG32 known-answer vector taken from the
  reference implementation.
- Build-time determinism enforcement: banned-API analyzer rejecting
  `System.Random`, wall-clock types, and thread-based parallelism in the sim.
- `ci.yml` — build + full test suite on every push and PR.
- **Phase 0 vertical slice**: one villager, one resource loop. Seasons with winter
  as the pressure, hunger, foraging behaviour, starvation and old-age death, and a
  narrative life log.
- **Ageing carries mechanical weight**: vigour is full until 30 then declines to
  55% in the final year, scaling what a foraging trip brings home. A life now has
  a shape — easy middle years, a visibly tightening old age, then death.
- Godot 4.7.1 view shell (`src/Bclone.Game`): clock, villager state, hunger bar,
  vigour, stockpile, scrolling life log, and pause/1x/2x/4x speed controls.

- **Phase 1 — households and smart labour.** A village rather than a villager.
- **Households**: several villagers living together, births conditional on a food
  surplus, childhood as real dependency (children eat and cannot work), and
  household formation — grown adults pairing across households and founding homes
  of their own, carrying a dowry from both parents' larders.
- **Food is stored per household, not in one village pile**, with a seasonal
  sharing policy so a family can go hungry beside a thriving neighbour and be
  seen doing it. The sharing policy is an explicit placeholder for a manned
  market (D14).
- **A derived food economy** (`VillageEconomy`): the village states its target in
  one sentence — a single adult at minimum vigour must feed themselves and three
  children — and `gather_yield` and `stockpile_target` are computed from it rather
  than tuned. Tests assert the shipped config still meets it, so a later change to
  hunger, travel, or vigour fails the build rather than the village.
- **Timber** (D17, first slice): a tree stand, a woodcutter job that works
  year-round when foraging cannot, and new homes that must be built before a
  couple can move into one.
- **Village labour allocation** (`specs/labour-allocation.md`): `LabourQuota`
  answers how much of each kind of work the village needs, `LabourAllocator`
  answers who does it and where, in one deterministic cost-first pass re-run from
  scratch each year so workers drift toward the jobs near where they live.
  Every assignment and every refusal states its own reason in plain language.
- **Several forage sites**, spread the way the homes are, which is what makes a
  binding catchment radius survivable.
- **A map with a camera**: a bounded 120x80 valley, WASD panning, wheel zoom about
  the cursor, and people on the same tile fanned apart so a household reads as a
  household. One control cycles how much explanation is drawn on top — off, the
  selected villager, or everyone — governing both home-to-work route lines and
  catchment rings.
- `CleanPlaythroughTests` — a 150-year run asserting the log carries no warnings
  or errors, turning Definition-of-Done item 5 from a manual check into a test.

### Changed
- `release.yml` moved from the repo root to `.github/workflows/`, where the
  README and METHODOLOGY already said it lived, and its Godot/C# build steps
  filled in. Still tag-gated and dormant until `v1.0.0`.
- `.gitignore` trimmed to the chosen stack; `export_presets.cfg` is now tracked,
  since the release export needs it present in a clean checkout.
- `test.bat` wired to `dotnet test`.

- **`Workplace.LabourDemand` split into two things.** It became
  `Workplace.Capacity` — how many hands physically fit at a site — and the
  village-level question moved to `LabourQuota`. One field could not carry both
  meanings; four different values of it each broke the village a different way.
  Config keys renamed to match: `forager_demand` is now `forage_site_capacity`,
  `woodcutter_demand` is now `tree_stand_capacity`.
- **`forager_catchment_tiles` lowered from 12 to 10** — the first radius at which
  no home reaches every workplace, so the "nobody walks across the map for one
  log" rule finally constrains something.
- The food economy is derived from the **worst walk any home in a village this
  size has to make**, rather than from the first home or from a single patch.
- A new house is paid for by the **whole village**, the two parent households
  first, rather than by the parents alone.

### Fixed
- **Villagers were invisible on the map.** People standing on the same tile drew
  at the same point, so four adults resting at one house rendered as one dot —
  which made the phase's own Success Test ("watching twelve villagers is still
  legible") unanswerable.
- **The map framed itself around every workplace, every frame.** Survivable with
  one berry patch; with seven it left the settlement a smudge three tiles across
  in an empty panel.
- The village log no longer claims everyone is "walking to the berry patch" when
  there are six patches and they are walking to a different one.

---

<!--
Release template — copy this block above and fill it in when cutting a version:

## [X.Y.Z] - YYYY-MM-DD
### Added
### Changed
### Fixed
-->

<!-- Link references (uncomment and set once the repo has tags):
[Unreleased]: https://github.com/joemachen/bclone/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/joemachen/bclone/releases/tag/v1.0.0
-->
