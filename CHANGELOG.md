# Changelog

All notable changes to **bclone** are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> **⛔ Pre-1.0: THIS FILE IS DORMANT, and nothing is written here as we work**
> (Joe, 2026-08-07 — `METHODOLOGY.md §5`). It used to say the opposite, and
> practice had quietly diverged from it for a dozen commits, which is the
> doc-versus-reality drift D48, D49 and D50 were each an instance of.
>
> **The reason is that this and `DESIGN.md §7` are not the same document.**
> The decisions log answers *why we chose this, and what we measured*, for us
> and for the next session; a changelog answers *what changed since the version
> you had*, for somebody who downloaded a build. There is no such person yet,
> which is exactly why nobody was writing it. Maintaining both by hand means
> writing every slice up three times — commit, decisions log, changelog — and
> **the third copy is the one that rots.**
>
> **So it is generated at the first tag**, in one pass, from the commit log and
> `DESIGN.md §6`, and rewritten to be *player-facing* rather than
> engineering-facing. That is half an hour at release time and produces
> something the decisions log never will.

Categories, for when it is written (only the ones used): **Added**,
**Changed**, **Deprecated**, **Removed**, **Fixed**, **Security**.

---

## [Unreleased]

> **⚠️ WHAT FOLLOWS IS A PARTIAL RECORD THAT STOPPED BEING MAINTAINED ON
> 2026-08-07**, when the practice above was withdrawn. It covers Phase 0 and
> the early part of Phase 1 and then simply stops; everything after it —
> storage, markets, map generation, pathfinding, building placement, the cold
> start, the builder's hut, stock limits, forests and gathering, two UI
> rebuilds and crops — is **not** here.
>
> **It is kept rather than deleted because it is a record of what was written,
> not because it is accurate as a summary of the project.** It will be replaced
> wholesale at the first tag rather than extended. Do not add to it, and do not
> read it as a list of what the game does — `DESIGN.md §6` is that.

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
  vigour, stockpile, scrolling life log, and pause/1x/2x/4x/10x speed controls.

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
  and running out is fatal (`CauseOfDeath.Cold`). An epitaph names which of cold and
  hunger killed someone **and reports the other**, which is the legibility condition D17
  attached to reversing Phase 0's ban on a second death system. *(The `freezing_ticks`
  counter this shipped with was later replaced by the positional model — see Changed.)*
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
- **A full audit trail, written every run.** `logs/bclone-<timestamp>.log` now takes
  everything down to `DEBUG` — every villager state change, every load carried, every
  job taken and every refusal, tick-stamped and attributed to a subsystem — while the
  on-screen village log stays the sparse `INFO` story it has always been (D8, D9). A
  new `CompositeLogSink` fans one entry out to both, because the two want opposite
  things and duplicating the call sites is how they would drift apart.
  - **Villager events are recorded from one place**: `BehaviorSystem.Execute` takes a
    before-and-after of each villager's state, position and load. A line inside each of
    the twenty-odd branches would have been wrong within a week — somebody adds a
    branch and the trail has a hole in exactly the case they were debugging.
  - `DEBUG` lines are guarded by `world.Logs(level)` so a village logging at `INFO`
    pays nothing to build strings it discards. The 300-year runs are unchanged.
  - The log path sits in the header beside the seed: together they are what reproduces
    and explains a run.
- **The residential brush** (D42; placement slice 3) — the player paints where the
  village may live, and the village builds inside it **when it has a reason to**. A
  painted area with no housing shortage produces nothing, and that is the brush working
  rather than failing.
  - **Zones are sim state**: hashed, deterministic, part of the seed contract, because
    a zone is a decision somebody made and two runs given the same decisions must
    produce the same village.
  - `Household.ChooseSite` is unchanged except that it only looks at painted land — so
    **the player picks the neighbourhood and the sim still picks the tile**. That is how
    placement was handed over without giving up `MaxHomeToWorkTiles`, the bound the
    whole food economy is derived against.
  - The distance warning fires **once per brush stroke**, not once per house — the
    entire reason zoning beat placing houses one at a time.
  - **The village asks by name** when a couple wants a home and there is nowhere to put
    one, and the header carries the request until somebody moves out.
  - Erasing land says where the village may build *next*; houses already standing stay
    put.
- **The player can mark out a building, and the village raises it** (D43; placement
  slice 2, sim half — no UI yet). **The first system in the game that answers to
  somebody**: everything before this happened *to* the player.
  - A building does **not** appear when it is paid for. A site is marked, materials are
    hauled to it from the nearest shed with logs, and a `Builder` raises it — funded
    from spare hands and **first to yield**, so a village short of hands feeds itself
    before it builds (§4a). Marking six buildings is a decision, not six purchases.
  - Construction sites are `Workplace`s of kind `Builder`, so they inherit labour
    allocation, catchment and refusal reasons instead of growing a parallel system —
    and the job disappears the moment the building exists.
  - `CanBuildAt` is **pure**, so a view can call it under the cursor every frame.
    Refusals are sentences — *"the ground there is under water"*, *"there is no route to
    there from the village"* — to the same standard `JobReason` already holds. A site
    that is merely far is **allowed and warned about**, not refused.
  - **Demolition returns half the logs and loses whatever was inside, out loud**:
    *"the granary was pulled down — 20 logs recovered, and the 1465 goods inside it were
    lost."* Goods vanishing with no line in the log is the untraceable outcome §1.1
    forbids. An abandoned site gives its delivered logs back in full.
  - Building costs live in `sim.config.json` as two numbers each — logs to *have* and
    work to *spend* — because a building dear in one and cheap in the other is a
    different decision from one dear in both.
- **A village may have more than one of a store** (D38; placement slice 1). No
  player-facing change — the village still founds itself with one of each, and a test
  asserts the new plural helpers give exactly the answers the old singular ones did.
  What changed is that a **second** granary, shed or market would no longer be silently
  ignored, which it would have been the day placement shipped.
  - `SimWorld.Granary`/`.StorageShed`/`.Market` were **deleted** rather than kept
    alongside the plural API, so the compiler enumerated all fifteen call sites and each
    one got a decision rather than a rename. The worst of them was the birth gate: a
    second granary it could not see would have been a building the player paid for that
    did nothing, and "the village stopped growing for no stated reason" is the least
    debuggable symptom there is.
  - *"Is there food?"* now means **all** granaries; *"where do I deposit?"* means the
    **nearest with room**, skipping any that cannot be reached; *"can we build a house?"*
    draws from **every** shed, since a house is paid for by the whole village (D25); and
    the population ceiling derives from **total** granary capacity, so a larger granary
    unlocked through the tech tree raises it exactly as a second ordinary one does.
  - The woodcutter's refusal changed with it — *"the storage shed has no logs"* was
    unverifiable once there could be more than one shed, so it now names the batch and
    says no shed within reach of the hut has it.
- **Water is impassable, and routes go round it** (D40, D41) — the change that makes the
  generated river mean something. Catchment, market errands, household placement and the
  economy's distance budget all inherited it for free, because they have always shared
  one cost field (§2.6).
  - Implemented as a **Dijkstra flow field per building** rather than a path search per
    call, which works because of a property of this game: *every travel query has a
    building at one end*. One field answers both questions an agent has — how far, and
    which way to step — as array lookups, with no stored routes and nothing to
    invalidate. Ties break by tile order rather than by a priority queue, because a
    heap's internal shuffling would make two runs of one seed take different equally
    short routes and diverge the state hash.
  - **Unreachable is a distinct answer, not a large number.** A sentinel that takes part
    in arithmetic silently wins nearest-thing searches and sends villagers on errands
    they can never finish.
  - The generator now owes the village **a valley it can live in**: the founding site is
    chosen as the land mass holding the most work, and every building is placed on
    ground that is *reachable*, not merely dry.
- **The valley is generated from the run's seed** (D18) — terrain, a wandering river,
  forest stands, forage sites and the founding site, drawn in a fixed order from the
  same seeded stream as everything else, and covered by the state hash. **Quoting one
  number now reproduces an entire run, world included.** The literal coordinates left
  `sim.config.json` and became *rules* — how many sites, how far out, how wide the
  river — so a modder controls a valley rather than a layout. A golden map hash makes
  a reordered draw fail the build, since that would silently invalidate every seed
  anyone had written down.
  - **Generation is bounded rather than checked**: sites are drawn within radii the
    economy already reads, so the distance budget holds by construction instead of by
    a reject-and-redraw loop. One economy serves every seed, which is what makes a
    shared seed comparable.
  - **Water is generated but nothing reads it yet**, deliberately. Making the river
    impassable needs real pathfinding in the travel-cost field — the field that decides
    who eats — so it is its own slice, and crossing it will need a bridge (D40).
- **Homes are placed, not spiralled.** `Household.ChooseSite` scores a site on the two
  trips a household actually makes — out to work, and over to the store — with the
  distance to work a hard bound rather than part of the score. `Household.PlacementFor`
  walked a square spiral that knew nothing about where the work was, which hand-placed
  coordinates had hidden for two phases: the sites had been positioned around that
  spiral by hand until it worked, so generating them just gave the spiral a new set to
  ignore. The village starved out at year 200 with a full granary until this landed.
- **The manned market** (D14, D36) — `JobKind.Marketer`, and the first job in the game
  that **produces nothing**: a marketer only ever moves what already exists. Several
  traders work it. They deliver from the stores to households below their target, and
  they are the only thing in the village that can reach a **dead family's larder** —
  goods stranded in empty houses fall by **98%** (1,618 goods-years against 81,846
  without a market). Fetch trips shorten by 6%, which is modest and honest: the shipped
  market stands a couple of tiles from the granary, so that number is a *placement*
  question rather than a market one.
  - **"If the distances make sense" needed no threshold.** A marketer never walks
    empty-handed and every leg is chosen cost-first from wherever they are standing, so
    "pick up food from the granary on the way back" falls out rather than being a
    special case — after a delivery near the granary, the granary is simply the
    cheapest next stop.
  - **The market is the lowest-priority job in the village and yields first.** Fetching
    is untouched, so an unstaffed market means longer walks and stranded goods, never a
    household that cannot eat — asserted by a 300-year run with the market switched off.
- `StateHash` now covers **what is in someone's arms** and a marketer's errand. Carried
  goods are the goods that exist between two buildings, and the hash had never read
  them: a village could have desynced by exactly the amount somebody was holding.
- `StateHash.MixStore` — one shared way to hash a store, so a store on a new kind of
  building cannot be silently left out of the determinism fingerprint. Stores went
  from one-per-household to one per household, workplace and building in a single
  slice, and one missed would have desynced in silence. Anti-vacuity guarded (D7).

### Changed
- **The view is laid out like Banished** (D55): the valley fills the window and the
  panels float on top of it — village status top-left, log top-right, selection
  below that, roster bottom-left, controls along the bottom. Standing alerts wrap
  and can be read to the end; nothing shares a layout with the map, so a panel
  that grows a line no longer moves the world under the player.
- **Cold is a place you are standing, not a number on your household** (D45, D53;
  `specs/shelter-and-exposure.md`). `Villager.Cold` rises fastest on open ground,
  more slowly under a roof with no fire, and falls beside a burning hearth — any
  occupied home with firewood in it, including a neighbour's. Two people of one
  family now get cold at different rates. Villagers break off work at halfway to
  freezing and walk to the nearest fire. Epitaphs say where somebody was when it
  killed them rather than how long their family had been out of firewood.
- **A fire thaws rather than resetting**, because the model was measured before it
  was built: villagers spend 76% of winter at a lit hearth, so a reset would have
  killed nobody in 120 years. A day by the fire undoes a day outdoors.
- The tests run the **shipped calendar**. `VillageFixtures` had inherited fifteen-day
  seasons from Phase 0 while `data/sim.config.json` moved to thirty (D49), so every
  village test ran a different year from the game for four commits.

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

- **The acceptance run watches 300 years, not 150, and asserts the village is still
  standing at the end.** The old window stopped one generation before the collapse
  completed: at year 150 the village was at twenty-three and falling, which satisfied
  "never dropped below the founding four" and read as the tail of a population wave
  rather than the middle of an extinction. It also now asserts a *band* rather than
  mere survival, since holding a stable size is what D31 actually asks for. 150 had
  never been chosen against anything.

---


- **Labour reshuffles every three years, not every year** (D46). A yearly reshuffle
  churns jobs faster than a player can read the reason for holding one; three years is
  a rhythm a person notices. Affordable because the urgent cases no longer wait for it.
- **A dead worker's job is filled at once** (D47), rather than standing empty until the
  next reshuffle — and where nobody can be spared, the header says which work the
  village wants that nobody is doing. Both are computed from world state on demand
  rather than kept as flags: nothing to hash, nothing that can be set and not cleared.
- **The player may say how many hands a workplace gets — never who** (D51). The default
  is "let the village decide", exactly the derived quota; an override sets a *count*,
  and proximity, household and catchment still choose the person.
- **Thirty-day seasons, and pacing stated as a life rather than a year** (D49). An
  average life takes 60 real minutes at 4x. `stockpile_target` doubled and `gather_yield`
  barely moved — a longer year doubles both what an adult eats and the trips available
  to gather it, and those cancel everywhere except winter.
- **Buildings are big enough for the village the economy is budgeted for** (D50). Every
  previous re-derivation moved the yields and left the capacities alone; the shipped
  file had three woodcutter seats where the economy required eight.
- **Winter no longer sends every spare hand to the woods** (D44, D52). The labour quota
  knows what season it is, so no berry patch is staffed while there is nothing on it —
  but the hands that frees are not given make-work. Cutting timber nobody wanted packed
  the sheds with logs, crowded out the firewood the birth gate reads, and cost the
  village a third of its population.
### Removed
- `freezing_ticks`, replaced by `exposure_days_outdoors` and
  `exposure_days_sheltered` — stated in days, because they describe a person in the
  cold rather than a tick rate.

- **Spoilage, from the plan** (D37). Joe's call — *"it's not fun."* It was a tax that
  arrives as a number going down for no decision the player took, punishing a well-run
  town as hard as a careless one, and it added a chore to a game whose second
  non-negotiable is *reduce babysitting*. The danger it was proposed against — a
  granary that never rots is a bank, and a village with a bank has permanently solved
  winter — is answered by **price rather than impossibility** (D39, correcting the
  reasoning here): the player can build as many granaries as they can afford, so the
  buffer is not capped at all. A village that has put the labour into ten granaries has
  genuinely solved winter and should feel like it.
- **The two sharing policies, and the two village-wide sweeps** — `ShareFood`
  (seasonal), `ShareFirewood` (daily), `SimWorld.TryTakeLogsFromTheVillage`, and
  `TryTakeBuildingTimber`'s village-wide sweep. All four existed because there was
  nowhere to put things, and all four moved goods by a rule the world enforced from
  nowhere. Each is now a building somebody walks to. D14 named them placeholders the
  day they were written; `specs/storage-and-distribution.md §6` made deleting all
  four the condition for the work counting as done.

- `food_source_x`/`_y`, `extra_forage_sites`, `tree_stand_x`/`_y` — all became generator
  output (D18). Deleted rather than left in place: a config key nobody reads is a trap,
  because a modder edits it, nothing happens, and the file gives no hint that it is
  decoration.

### Fixed
- **Logs stopped vanishing into household larders** (D48, and the third time this class
  of bug has been fixed). `UnloadAtHome` dropped carried logs into a household's
  stockpile, and nothing in the sim can spend a log from there — 240 logs frozen in two
  houses for twenty years while a building site waited on 28. Fixed with an invariant
  asserted against both the fixture and the shipped config rather than a fourth patch.
  **It presented as a demographic wave, which is how it survived: nobody starved, nobody
  froze, and the granary was full.**
- **No two places in the valley share a name** (D56). Bearings only had eight
  values against six forage sites, so 44 valleys in 50 named two places alike —
  and every site past the first was called a *thicket* whether it was foraged or
  felled, so "the southern eastern thicket" could not tell you whether the village
  was short of food or of timber. Thickets are foraged, woods are felled, bearings
  are hyphenated, and where two places share one the nearer keeps the plain name.

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

- **The village's own buildings could be founded in the river.** They were placed at
  fixed offsets from the founding site with no terrain check, so on one seed the shed
  and the woodcutter's hut both came down in the water: no logs could be stored, no
  firewood made, and all four founders froze in the first winter. Nothing in the log
  said "your shed is in the river" — it said they were cold.
- **Founding homes were still spiralled** while every home built afterwards was placed
  with regard to the work. They now go through the same rule, so the founders cannot be
  handed a start their own descendants would never choose.
- **`CouplesLeaveHomeWithFoodToStartOn` was checking the wrong field** — `LifetimeGathered`,
  which a dowry deliberately does not touch, since `Stockpile.Receive` exists precisely
  so goods changing hands are not counted as production. It was really asserting "has
  foraged at some point since", and failed the first time a household was founded near
  the end of the run. It now checks the larder at the tick of formation.
- **A refusal that contradicted itself.** With one full berry patch and a tree stand
  the village wanted nobody at, three idle villagers were told *"the village has all the
  hands it needs — 4 foraging"* while exactly one of them was foraging: the reason
  reported the *nearest* reachable workplace, and the stand happened to be a tile
  closer than the full patch. It now reports the nearest place that is **full and still
  wanted** — the one the player can act on by building another.
- **The economy derived itself from wherever a spiral happened to put twenty homes.**
  It is now derived from a bound the village *keeps* (`MaxHomeToWorkTiles`), which is
  what lets one economy serve every generated seed.

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
