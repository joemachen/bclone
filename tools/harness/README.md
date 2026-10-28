# The measurement harness (D420)

How a number in `data/sim.config.json` or a new-game row's end gets **measured before it is typed**
(CLAUDE.md, METHODOLOGY §3; the tables in `specs/new-game-screen.md §7` and D480 came from this).

- `ZzBase.cs` — an xUnit theory that founds a village per seed, plays the opening the way
  `ColdStartTests` does, marks a granary and a warehouse at year 3, runs fifty years unattended and
  prints one `ZZB …` line (alive, peak, starved, froze, final hash). Arms are environment variables:
  `ZZ_ROW=id=value` (a new-game row), `ZZ_WIDE=1|2` (seeds 200–299 / 300–399), `ZZ_FIX=1` (50 fixture
  valleys), `ZZ_CARRY` / `ZZ_HUNGER` / `ZZ_MEAL` (`carry_capacity`, `hunger_per_tick`, `food_per_meal`; D535's armful
  and meal arms), and the older `ZZ_SPEED`, `ZZ_YIELD`, `ZZ_USES`, `ZZ_CART`, `ZZ_STONEX`, `ZZ_PAINT`,
  `ZZ_SMITHY`, `ZZ_IRON`, and `ZZ_QUARRY` (`quarry_unlock_stone`, B6). The `ZZB` line ends with
  `dug N learned T at100 T at200 T tpy N fetches N meals N hhyears N` — the stone dug by hand, and the first ticks the quarry was
  learned and 100 / 200 stone had been dug (−1 never); then household food fetches begun, meals eaten and
  household-years (D535: fetches ÷ hhyears is a household's food trips a year).
- `quarry.py` — per arm: how many valleys learned to quarry and when (p10 / median / p90 years), and
  when 100 and 200 stone had been dug. The harness never marks a quarry, so the crossings are the same
  whatever `ZZ_QUARRY` says; only `learned` follows it (D500).
- `arms.sh` — copies `ZzBase.cs` into the test project, runs the arms, deletes it, prints `dead.py`.
- `dead.py` — per arm: runs, alive, **dead valleys**, peak, starved, froze. The survival guard's line
  is 25 % dead.
- `summ2.py` — the older per-group summary (shipped / fixture / every).
- `ZzB5.cs` — B5's measurement (D496): every house's footprint tiles at mark, raise and the end (`ZZB5H`), and
  where cleared wood regrew (`ZZB5S`), over three arms (played / homes over a wood / over a seam); `ZZ_YEARS`.
  `Placed` poses a granary and a woodcutter's hut over trees. Copy in, `--filter ZzB5`, delete.

⛔ **`ZzBase.cs` is never committed under `tests/`** — it is slow and it is not a guard.
⚠️ **A harness is not a player (D447):** it measures that a setting can be lived in, not how it plays.
⚠️ Baselines move whenever the sim does: run `base` in the same sitting as the arm, never compare
against a number from an older commit.
- `ZzLimits.cs` + `limits.py` — D611's measurement of the starting stock limits: `ZZ_LIMITS=old|new` (both typed in the file,
  so an arm cannot move with `sim.config.json`), `ZZ_OPEN=played|established|every` (⚠️ only `every` — a lodge, a fishery and
  a farm raised free — ever makes fish or meat; it carries D515's first-winter race), `ZZ_ONLY=<good>` (one good's new number
  alone), `ZZ_SEEDS`, `ZZ_TRACE=1` (a ZZY line each spring and winter: people, held goods, trades, deaths). Copy into a
  **worktree**, build **with a check for `0 Error(s)`**, `-c Release --filter ZzLimits`, delete; `limits.py` prints each arm and the
  per-seed spread (D603). ⭐ `ZZ_LABEL=old|new` names the arm, so two builds of the code are measured on the same limits in two
  worktrees (D612's market, D617's tools); the ZZL line carries the marketers' carrying ticks, the counters' stock, tools
  forged and taken, the tool-trade hands' share of ticks without a tool, and site-ticks. ⚠️ No harness village raises a
  smithy: a tools rule measured here is measured on the founders' twenty (D617).
- `ZzDoors.cs` — D619's premise for `specs/doors.md §3`: on a folder of saves (`ZZ_SAVES`, a *copy* of Joe's), for
  every standing building, whether the tile in front of its facing is walkable, empty, unwalled and reached, whether
  another side is open if not, and the walk to it against the walk to the building. Reads, never steps. Copy in,
  `--filter ZzDoors`, delete.
- `ZzDoorWalk.cs` — D623's measurement of what a door costs a walk: on twenty fixture villages ten years on, every walk
  (a passage in from a free tile, a leg between buildings, a farm's haul) priced through the doors and again with
  `SimWorld.DoorsOnEverySide`; one ZZW line a village and walk kind. Copy in, `-c Release --filter ZzDoorWalk`, delete.
- `ZzDoorSaves.cs` — D623's fourth arm: Joe's own saves (`ZZ_SAVES`, a *copy*), stepped `ZZ_YEARS` (20) on, one ZZS line
  a save — alive at load and after, deaths by cause, births. Run in two worktrees with `ZZ_LABEL`, as `ZzLimits`.
- `ZzReplay.cs` — D507's measurement for save/load: what a *replay* save would cost to load — the founding plus
  fifty shipped years, timed, over four arms (unattended / played / every / established), ten seeds each, with
  one `StateHash` timed at the end — and, since D509, the snapshot saved and opened again (hash checked) with its
  size. Copy in, `dotnet test tests/Bclone.Sim.Tests -c Release --filter ZzReplay
  --logger "console;verbosity=detailed"`, delete. ⚠️ No harness village passes ~19 people, so its times are a floor.
- `ZzFirstWinter.cs` — D515's measurement of the first-winter firewood race: five arms (`everyfix` = `FoodConservationTests`'
  founding with its free t0 lodge, fishery and farm; `every` the same on the shipped config; `played`; `farm0` / `farm2` a
  farmhouse **marked**, not raised, at t0 / Year 2), seeds 1–16 + 12345, fifty years. One `ZZW` line a run: when the farm
  and the woodcutter's hut stood, the first split, firewood in reach / the burn on each winter's first day (Years 1–5),
  houses standing and logs felled at Winter, Year 1, cold deaths by year, and the woodcutters wanted / given and farmers
  given at each season turn (Years 1–3). `ZZ_ARMS`, `ZZ_SEEDS` narrow it. Copy in, `--filter ZzFirstWinter`, delete.
- `ZzFoodChain.cs` + `foodchain.py` — D521's anchors for the food chain (`specs/food-chain.md §8.1`), measured before
  any of it exists: wheat and forage per seat-year held, when cumulative reaped wheat reaches 1k/2k/5k/10k, wheat held
  in stores, and the lockstep share (two adults of one household carrying the same hunger). Arms: `every` (free farm,
  lodge, fishery), `farm0s` / `farm2sw` (a farmhouse marked at t0 / Year 2, its field painted **once it stands**, staffed
  with `SetJobLimit`), trailing `w` lifts the wheat limit alone, `x2` marks two. `ZZ_WHY=1` prints each farm's card a
  season (that is how the wheat stall was found). Copy in, `--filter ZzFoodChain`, delete; then
  `python tools/harness/foodchain.py <output>`. ⚠️ A field painted on a **site** never reaps — ZzFirstWinter's `farm0`
  did that, so it never measured a harvest. ⚠️ Food production is `SimWorld.FoodEverProducedOf(good)`; summing
  `Stockpile.Produced` over arms and stores double-counts every carried load.
  ⭐ **Since D522, the chain itself:** a trailing `B` marks the mill the tick it is learned and the bakery the tick
  that is (one hand each); `R` raises them free (unattended builders raised the mill in 1 valley of 17 — 24 stone);
  `ZZ_MILL=n` sets `mill_unlock_wheat`. The ZZF line then ends with `mill`/`bakery` years, flour ground, bread baked,
  wheat reaped a year once milling, flour and bread a seat-year. ⛔ **Build with a check** — a harness that did not
  compile ran the previous DLL under `--no-build` and a whole arm was read off the wrong code.
- `ZzSkip.cs` — D525's measurement of the movement skip (`gridless.md` slice 7): per villager per tick, a position move
  over √2 tiles (the view teleports), a walking-speed histogram, a stutter (a walker standing a tick mid-journey, and
  how many were a meal on the road), and the crowd fan's instant jumps by size — the view's own rule restated — plus
  D525's rule modelled (`glides-D525`: re-ranks among the standing, now slides). Arm `played` (shipped config, played
  opening), ZZ_SEEDS / ZZ_YEARS. Copy in, `-c Release --filter ZzSkip`, delete.
- `ZzLockstep.cs` — D528's measurement of the all-at-once skip (`gridless.md` slice 7b): per tick, how many walkers
  **stalled** (moved over half a tile, then nothing, same errand) and how many of the stalls were meals
  (`Villager.JustAte`), so a tick where several stop together is counted. `ZZ_TABLE=n` prints the first n ticks
  (each villager's move and state, `*` a meal) — that table is how the four-way stop was seen. Arms: `played`,
  `unattended`, and `played+ticks` / `played+spread`, which restagger hunger at t0 **in the harness only** (D190's
  rhythm as ticks' worth of hunger; founders evenly over the meal cycle). ZZ_ARMS, ZZ_SEEDS, ZZ_DAYS (120). Copy in,
  `-c Release --filter ZzLockstep`, delete. ⚠️ D525's "10–16 stalls per 1,000" was over all villager-ticks, standing
  ones included, across twenty years — **per walking tick in the founding it is 113–130**, and that is the shape the
  eye sees.
- `ZzClothing.cs` — D533's re-take of `specs/clothing.md §5` (`§5.2`): per run, working-age villagers' winter ticks
  split into work outdoors / work under a roof / walking / seeking shelter / at a fire, cold break-offs (entering
  `SeekingShelter`), and outdoor/roof winter work by trade (`ZZC`); a `ZZD` line per cold death (year, shelter, state,
  job). Cold worlds `ZZ_COLDS` = `bare` (shipped), `coat` (`exposure_days_outdoors` = the roof's days), `off` (0);
  openings `ZZ_ARMS` = `played` (30 shipped seeds) / `every` (13). `ZZ_LIMITX=k` multiplies every limit but the tools'
  at Year 5; `ZZ_HUNT=1` prints a hunter state histogram (`ZZH`). Copy in, `-c Release --filter ZzClothing`, delete.
  ⚠️ `off` is not a coat: the threshold is outdoor ticks × roof ticks, so 0 switches **roof** cold off too, and it
  is an upper bound. ⚠️ `ZZ_LIMITX` applied at t0 kills every every-source founding in Year 1 (D447).
- `ZzBridges.cs` — D547's measurements for bridges (`specs/bridges.md §8`). `Valley` (pure map generation, 100 seeds × the
  four river courses, `ZZV`): the founding's land by flood fill and what lies across the river (land, woods, rock, iron),
  and every straight cardinal run of water with land at both ends and the founding on one side — the spans a one-wide
  deck could cross (`le3`…`le8`, `min`, `max`). `Growth` (`ZZG`, the `shipped` and `every` arms, fifty years): the first
  year a village holds 10 / 12 / 15 / 20 / 25 people. `Crossings` (`ZZX`): a played opening, a wooden bridge raised free at
  Year 5 at the crossing nearest the founding, a forager's hut marked on the far bank and an 8×8 patch painted for homes
  beyond it; crossings per year, Years 5–35. Copy in, `-c Release --filter ZzBridges.<method>`, delete.
- `ZzPathShape.cs` + `pathshape.py` — D579's measurement of the paths' gridding (`specs/path-shapes.md`): opens each save
  in `ZZ_SAVES` (⚠️ a COPY of Joe's folder — `SaveFile.Open` keeps a damaged file as `.bad` beside it), runs it `ZZ_YEARS`
  (10) more under each of `ZZ_ARMS` (`now` = as saved, `base`, `allowN` = `path_shortcut_grass_allowance` N; a code arm
  is the same harness on a tree carrying the change), and prints `ZZP`: the village's path tiles, the share in straight
  axis lanes of five or more, sharp corners, diagonal links, the share beside a building, and the same outside the
  village. `ZZ_OUT` writes each village as a character grid; `python -X utf8 -I tools/harness/pathshape.py OUT.png
  A.grid B.grid …` draws them side by side (PIL). Copy in, `-c Release --filter ZzPathShape`, delete.
- `ZzBridgeKnock.cs` — D576's measurement: the first year each village held 10 / 12 / 15 / 20 people with nomads on, and
  whether that tick was a knock (`K`). Needs `ZzBridges.cs` copied in beside it for the seed list. Copy both in,
  `-c Release --filter ZzBridgeKnock`, delete both.
  ⚠️ Without the hut across, every village crossed **zero** times in thirty years: the unattended chooser never sites a
  home on the far bank while near-side paint is left, so a bridge nothing needs is an idle bridge, not a measurement.
  ⚠️ Joe's own saves are the better number for growth (three played villages first held 10 at Years 11, 12, 11) — they
  are gzipped JSON under `%APPDATA%/Godot/app_userdata/bclone/saves/`; a founder's `birth_tick` is negative, so read it signed.
- `ZzMaintenance.cs` — D552's measurement for building condition (`specs/building-condition.md §8`): one `ZZM` line per
  village per sampled year (5, 10, 20, 30, 40, 50): people alive, every standing building by kind with its age (first
  year the harness saw it — nothing in the sim stamps a raise but bridges and the town hall), the logs and stone those
  recipes cost, logs ever felled / split / in store, stone and firewood in store, and the felling since the last sample.
  Since D555 also `mended` and `fell-in` (counted off the narration), `closed-now`, `repairs-waiting` and the logs already
  on those waiting repairs (`building-condition.md §8.5`).
  Arms `shipped` (30 seeds) and `every` (13); `ZZ_SEEDS`, `ZZ_YEARS`. Copy in, `-c Release --filter ZzMaintenance`,
  delete. ⚠️ **Felling is limit-driven**: logs sit at their 200 limit from Year 5, so *demand ÷ logs felled* reads as
  alarming and means nothing — compare to the ~90 a year one woodcutter fells in the opening when something wants them.
  ⚠️ No unattended valley holds any stone in any year; a cost priced in stone can never be served there.
- `ZzNomads.cs` + `nomads.py` — D564's measurement for the nomads (`specs/nomads.md §8`), the arrivals **posed from the
  test side** before any of it existed: `ZZK` what a knock meets at Spring of Years 8 / 10 / 12 (alive, households, housed,
  empty houses, food a head, residential paint, the band at a fifth and a quarter); arm `settle` (`ZZS`): `ZZ_SETTLE` people
  (default the fifth rule's) arrive roofless at Year 10 in one or two families with 20 food each — housed when, died of
  what; `ZZD` what stands 0 / 2 / 4 / 6 years after a valley dies, by condition state; arm `refound` (`ZZR`): `ZZ_REFOUND`
  years (3) after the last death four adults in two households arrive and the cart is restocked — alive 1 / 5 / 10 / 20
  years on. `ZZ_ARMS`, `ZZ_SEEDS`, `ZZ_YEARS`. Copy in, `-c Release --filter ZzNomads`, delete; `python tools/harness/nomads.py
  <output>`. ⚠️ Since D572 the sim settles bands itself, so `ZzNomads` runs with `nomads_enabled` off and its posed
  arrivals stand alone; `arms.sh base` is the measurement of the sim's own. ⚠️ The first `refound` run posed one family of two adults and two children — the founding's shape is two
  households of two adults, and a measurement of the wrong pose is a number about nothing. ⚠️ An unattended valley at
  Year 10 is eight people in two housed families, so every share rule sends the minimum band; Joe's saves are the number.
- `ZzSeamGround.cs` — D561's measurement for the quarry's and the mine's ground (`specs/seam-reach.md §6`): one `ZZG` line per
  seam in 100 shipped valleys (seeds 200–299) — its kind, its size (what a building standing by it may paint since D561) and
  the most a building could paint under D541's diamond. Copy in, `-c Release --filter ZzSeamGround`, delete.
- `ZzTrade.cs` — D586's measurements for trade (`specs/trade.md §8`). `River` (pure map generation, 100 seeds × the four
  courses, `ZZR`): which map edges the river touches, the bank tiles on the founding's side (grass touching water, the
  fishery's rule) and the walk from the founding site to the nearest. `Village` (seeds 200–299, the played opening,
  unattended, `ZZH` / `ZZB`): the year the last founder died (the town hall owed) and every good held in stores against
  its limit then and 10 and 20 years on. ZZ_SEEDS, ZZ_YEARS (100). Copy in, `-c Release --filter ZzTrade`, delete.
  `Region` (D594, `ZZM`, needs `TradeTests` in the project): every neighbour left as a boat would leave it at its worst,
  stepped 4, 8 and 12 seasons — how far its stocks and rates come back between boats (`trade.md §8.4`).
  ⚠️ An unattended village builds almost nothing, so its surplus is thin; what a played village holds came from copies
  of Joe's saves (`trade.md §8.2a`).
- `ZzHerd.cs` — D589's measurements for the herd (`specs/livestock.md §10.1`, commit 0 of `trade.md §11`). `Cold` (`ZZK`,
  once): the shipped exposure numbers as ticks — break-off, freeze, and the tiles a walk covers between them — plus the
  farm's field anchors. `Seasons` (ZzClothing's `played` valleys, 60 years): working-age villager-ticks per season in three
  twenty-year windows, split work / walking / at home or a fire / other (`ZZS`); and at the year the last founder dies, free
  grass or forest within 10 / 20 / 30 tiles' walk of the founding site and the nearest from a home (`ZZP`). Copy in,
  `-c Release --filter ZzHerd`, delete. ⚠️ Its "walking" bucket is any `Travel*` or `-ing` state not resting — hauling
  counts as walking, so read work + walking as *busy*.
- `ZzGoldens.cs` — D642's audit for Phase 5's gate item 2 (`specs/phase-5-golden.md`): replays the five years-long goldens
  (`fixture50`, `shipped50`, both again without skills, `farm20`) exactly as their tests build them, **asserts each final hash
  against the golden's own constant**, and samples once a game day: workplaces standing by job and days worked, stores by
  kind, every good held, bridges, herd head, boat, bands, repairs, wells, homes and their doors (`ZZG` lines). 18 s in Release
  for all five. Copy in, `-c Release --filter ZzGoldens`, delete. ⚠️ When a golden is re-taken, its constant here moves too.
- `ZzPose.cs` — D643's measurement of `specs/phase-5-golden.md §5`: the posed Phase 5 village on the shipped data. It poses
  the farm, mill, bakery, quarry, mine, smithy, well, post and barn at tick 0, plus a pasture and trade orders; the bridge and a
  well across the river at the tick the village has held 10; and a herdsman pinned when cattle first stand. One `ZZP` line a
  year: what each chain made, and the villager-ticks in each chain's working state. `ZZL` lines group the herd and boat
  narrations. `ZZ_YEARS` (default 50); 45 s in Release. Copy in, `-c Release --filter ZzPose`, delete. Needs the `internal`
  helpers in `MineTests` and `QuarryTests`.
