# The measurement harness (D420)

How a number in `data/sim.config.json` or a new-game row's end gets **measured before it is typed**
(CLAUDE.md, METHODOLOGY §3; the tables in `specs/new-game-screen.md §7` and D480 came from this).

- `ZzBase.cs` — an xUnit theory that founds a village per seed, plays the opening the way
  `ColdStartTests` does, marks a granary and a warehouse at year 3, runs fifty years unattended and
  prints one `ZZB …` line (alive, peak, starved, froze, final hash). Arms are environment variables:
  `ZZ_ROW=id=value` (a new-game row), `ZZ_WIDE=1|2` (seeds 200–299 / 300–399), `ZZ_FIX=1` (50 fixture
  valleys), and the older `ZZ_SPEED`, `ZZ_YIELD`, `ZZ_USES`, `ZZ_CART`, `ZZ_STONEX`, `ZZ_PAINT`,
  `ZZ_SMITHY`, `ZZ_IRON`, and `ZZ_QUARRY` (`quarry_unlock_stone`, B6). The `ZZB` line ends with
  `dug N learned T at100 T at200 T tpy N` — the stone dug by hand, and the first ticks the quarry was
  learned and 100 / 200 stone had been dug (−1 never).
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
