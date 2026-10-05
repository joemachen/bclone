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
