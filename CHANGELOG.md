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

- **Phase 2 — wood as fuel, and goods that live somewhere.**
- **Wood is two resources, and the first processing chain** (`specs/wood-fuel-and-tools.md`,
  D17/D29): a **logger** fells **logs** at the tree stand, a **woodcutter** turns logs
  into **firewood** at a hut. Logs build; only firewood burns. The woodcutter is the
  first workplace that can stand idle for want of an *input* rather than a worker, so
  it says which of the two is stopping it — the shape every later chain inherits.
- **Winter kills on a second axis.** Firewood burns per household per day of winter,
  and running out is fatal (`CauseOfDeath.Cold`). `freezing_ticks` is deliberately
  longer than `starvation_ticks`: fuel comes from a two-step chain, so the village
  needs time to notice and put hands back on the hut. An epitaph names which of cold
  and hunger killed someone **and reports the other**, which is the legibility
  condition D17 attached to reversing Phase 0's ban on a second death system.
- **Goods live in buildings** (`specs/storage-and-distribution.md`, D30/D32): a
  **granary** for food, a **storage shed** for materials, small buffers at the
  workplaces that made them, and a working larder in every home so meals stay
  instant (D10). Goods move only by trips people make — producers carry their
  loads, households fetch — and `carry_capacity` limits an armful, which is what
  makes distance to the granary a real cost rather than a formality.
- **Storage has a capacity, and the granary is what decides how big the village gets**
  (D33). A granary holds a winter's store for `granary_feeds_people`; the quantity and
  the resulting **population ceiling** are both derived from that one stated number
  rather than typed in. Births are gated on the granary holding a share of what
  everyone alive would want, so capping the building caps the settlement — growth stops
  at what the buildings support instead of overshooting and falling back. Measured over
  200 years: **24–35 people, against 24–86 with the cap removed.** Capacity is total
  across goods, not per good: a shed packed with logs has nowhere to stack firewood.
- `StateHash.MixStore` — one shared way to hash a store, so a store on a new kind of
  building cannot be silently left out of the determinism fingerprint. Stores went
  from one-per-household to one per household, workplace and building in a single
  slice, and one missed would have desynced in silence. Anti-vacuity guarded (D7).

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
- **The village is tested for a *stable size*, not for growth** (D31). A test
  asserting the population grows was quietly asserting the wrong thing: failure has
  to stay possible. Measured over 150 years the village holds between 19 and 28 for
  a century, with old age the usual way to go — fifty-four deaths against six from
  starvation and none from cold.
- `max_household_size` 4 → 7. Four meant two parents and two children: bare
  replacement, before anyone dies young.

### Removed
- **The two sharing policies, and the two village-wide sweeps** — `ShareFood`
  (seasonal), `ShareFirewood` (daily), `SimWorld.TryTakeLogsFromTheVillage`, and
  `TryTakeBuildingTimber`'s village-wide sweep. All four existed because there was
  nowhere to put things, and all four moved goods by a rule the world enforced from
  nowhere. Each is now a building somebody walks to. D14 named them placeholders the
  day they were written; `specs/storage-and-distribution.md §6` made deleting all
  four the condition for the work counting as done.

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
- **Births required almost nothing.** `birth_food_threshold` was an absolute 45
  while a household's food target scales with its members, so a family of seven
  with a target of 462 had a child at a tenth of a full larder — the cause of the
  boom-bust. As a percentage of the household's own target it is self-limiting:
  as the village approaches what its sites can feed, households stop reaching
  their targets and births slow *before* anyone starves rather than after.
- **`LabourQuota` counted goods in households** after goods had moved into
  buildings, so every household read zero and the village believed it had no
  timber at all. It spent a century cutting wood it already had, finishing with
  5000 firewood and six people ever born. (The recurring shape: code that still
  reads state from where it used to live.)
- **Foragers stopped the moment their own larder was full**, so the granary never
  filled and the household with no forager starved beside neighbours resting on
  300 food. There are two reasons to work now — my family is short, or the village
  is — which is the whole argument for a granary.
- **The fuel quota was a thermostat that switched on after the house was cold.**
  Including the annual burn makes it proportional, so the store settles in a band
  above target instead of oscillating through it.
- **THE DEAD WERE TAKING UP ROOM IN THE HOUSE** (D34) — and this is what had been
  killing every village since Phase 1. A household's member list keeps everyone who
  ever lived there (`RemoveMember` runs when somebody moves out, never when somebody
  dies), and the birth check read its length as "how many live here". A household that
  had seen `max_household_size` people pass through it was permanently barred from
  having another child, with a young couple in it and every other condition met.
  Households ratcheted one way into sterility and **every settlement died out around
  year 180**, whatever its food, fuel or storage were doing. Every other occupancy
  question in the sim already asked `LivingMembersOf`; this was the one place that did
  not.
- **`TargetFoodForTheGranary` was answering two questions.** "Could we feed another
  mouth through a winter?" has to stay unbounded — that is what makes the ceiling — but
  "should anyone go out and work today?" was reading the same number, and above the
  ceiling it is unreachable by construction, so the answer was permanently yes. Every
  hand stayed on the berry patches forever and nobody was spared for the woodcutter's
  hut. Split into `FoodTheGranaryHasRoomFor`; the same mistake D21 is a record of.
- **The fuel quota counted firewood nobody could reach.** It compared all the firewood
  anywhere against what every home wanted, so a surplus in one house cancelled a
  shortage in another — but a household can only fetch from the shed, and wood stacked
  in a neighbour's home is not supply. The comment justifying it cited a sharing policy
  that storage slice 3 had already deleted. Demand is now counted per home and supply
  is the shed alone.

### Changed (tests)
- **The acceptance run watches 300 years, not 150, and asserts the village is still
  standing at the end.** The old window stopped one generation before the collapse
  completed: at year 150 the village was at twenty-three and falling, which satisfied
  "never dropped below the founding four" and read as the tail of a population wave
  rather than the middle of an extinction. It also now asserts a *band* rather than
  mere survival, since holding a stable size is what D31 actually asks for. 150 had
  never been chosen against anything.

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
