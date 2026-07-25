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

### Changed
- `release.yml` moved from the repo root to `.github/workflows/`, where the
  README and METHODOLOGY already said it lived, and its Godot/C# build steps
  filled in. Still tag-gated and dormant until `v1.0.0`.
- `.gitignore` trimmed to the chosen stack; `export_presets.cfg` is now tracked,
  since the release export needs it present in a clean checkout.
- `test.bat` wired to `dotnet test`.

### Fixed
- _(nothing yet — pre-release)_

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
