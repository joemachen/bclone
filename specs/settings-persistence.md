# Spec: Settings persistence — what the player set stays set

**Decisions:** D353 (the shell's order: error boundary → per-stage seeds → new-game screen → **settings
persistence** → save/load → title and pause), D330 (*"nothing persists settings anywhere in this
project"*), `new-game-screen.md §10` (remembering the screen's last rows parked for this step), **D504
(Joe's calls for this slice, 2026-10-05 — §2)**. Neighbours: D340 (*set intent at the gesture, not at
the mechanism*), D380 (*a tick reads the state; it does not only write it*), D335 (a derived value is
never hashed).
**Status:** ✍️ **SPECCED (D504). ✅ BUILT (D505) on `slice/settings-persistence` — the sim half and the
view, guards §8 red-checked; no golden moved. ⚠️ UNPLAYED: Joe's play (§10.4) is the only check that a real
launch writes the file and a drag is remembered** — the probe is hermetic by design (§7) and cannot drag.
Owner: Joe + Claude Code.

---

## 1. Why this exists

Every launch forgets everything. Joe turns the UI down to the size he likes, switches the grid on, rolls
the roster up, drags Professions out of the way — and the next launch puts all of it back. A player who
has to re-make the same twelve choices every session is babysitting the furniture (§1.2), and a
control that does not stay where you put it is the complaint D242 and D340 were both answers to:
*"the panels move when I haven't asked them to."* The new-game screen has the same gap one level up:
its rows open at the config's values, not the valley the player last chose.

## 2. Joe's calls (D504, 2026-10-05)

- **Remember all three groups:** the view's preferences, the windows' places and folds, and the
  new-game screen's rows. **The seed still rolls fresh** each launch, and the village name is never
  remembered — both belong to one valley, not to the player.
- **The file lives in Godot's user directory**: `user://settings.json`, which on Windows is
  `%APPDATA%\Godot\app_userdata\bclone\settings.json` — it survives a repo clean and a shipped `.exe`
  in a read-only folder can still write it. Its path is shown under *Settings → About this run*.

## 3. ⛔ The line: a setting is the player's, never the village's

**A setting is a preference about how the player sees and drives the game. It is never sim state,
the sim never reads it, and it never enters the hash** — so this slice moves no golden.

⛔ **The *share the work out* tick is NOT a setting**, though it sits in the Settings panel:
`SimWorld.VillageSharesOutWork` is hashed (`StateHash`), it changes what the village *does*, and it is
filed under its own heading there for exactly that reason. It is the village's, so it travels with
**save/load**, and a fresh village starts with it on as today.

*Snap to the grid* is a setting even though it changes placement: it acts on the input before the sim
sees it (D330), so nothing about the village depends on it.

## 4. What is remembered

| Group | What | Key |
|---|---|---|
| View | UI size (55–115 %, steps of 5; default 75) | `ui_scale_percent` |
| View | Routes: `off` / `selected` / `all` | `routes` |
| View | the *Paths* overlay and every *On the map* tick (grid, snap, the two markers, the three shaded grounds, animals, berries) | `toggles.<id>` |
| Windows | each window's Settings tick (*shown*) and whether it is rolled up (*open*) | `windows.<title>.shown/open` |
| Windows | where the player **dragged** it | `windows.<title>.side/x/y` |
| New game | each row's value from the last valley founded | `new_game.<row id>` |

- **Every key is optional**; an absent key is the view's own default. A toggle or window added later is
  remembered the day it is written, because both come out of the registries that already build the
  Settings panel (`_mapToggles`, `_windows`) — never a hand-kept list.
- ⚠️ **A window is keyed by its title.** Renaming one forgets its place and fold, which falls back to
  the default: harmless, and said here so nobody calls it a bug.
- ⭐ **A place is stored only once the player has dragged that window** — intent at the gesture
  (D340). The default arrangement, Settings' centring and *What's here* placing itself by its tile all
  move windows too; none of them may write a place, or the default would be frozen in the file and a
  later change to the layout would never reach the player.
- **A place is measured from the window's own side**: `x` is the drawn corner's distance from the left
  edge for a left-docked window, and the drawn right edge's distance from the right edge for a
  right-docked one; `y` from the top. A window kept on the right in a 1280-wide game is still on the
  right at 1920. Anything off screen is brought back by `KeepWindowsOnScreen`, as today.
- **Reset window positions** forgets every stored place, as well as arranging the windows.
- **New-game rows are written when a valley is founded**, not as the sliders move: the screen opens on
  *the valley you last founded, with a new seed*. A remembered value is kept only if its row still
  exists and `NewGame.IsAllowed` still accepts it — so a modder's changed rows, or a range a later build
  narrows, fall back to the config's default rather than founding something refused. *Default
  settings* on the screen still means the config's own.
- **Not remembered:** the speed (a village opens as it does today), cards (they belong to a selection),
  the camera, the seed, the name, and *share the work out* (§3).

## 5. The file

```json
{
  "version": 1,
  "ui_scale_percent": 75,
  "routes": "selected",
  "toggles": { "grid": false, "paths": false, "snap": false },
  "windows": {
    "Professions": { "shown": true, "open": true, "side": "left", "x": 420, "y": 96 }
  },
  "new_game": { "forest_cover": "60" }
}
```

- snake_case JSON, comments and trailing commas allowed (D3), written with keys sorted so a hand-edit
  or a diff reads cleanly.
- **No floats anywhere** — the scale is a whole percent and a place is whole logical pixels. (The sim
  library's public API is float-free, guarded by `FloatBanTests`, and this record lives beside
  `NewGame` in `Bclone.Sim.Config`.)
- **Written atomically**: to `settings.json.tmp`, then moved over `settings.json`. A crash mid-write
  leaves the old file, never half of a new one.
- **Written on each change, never per frame**: a tick, the size dial, Routes, Paths, a window's tick or
  ✕, a fold, the **release** of a drag, Reset window positions, and founding a valley.

## 6. Edge cases and failure modes (METHODOLOGY §4 — nothing is swallowed)

| What | What happens |
|---|---|
| No file (first launch) | Defaults. Nothing said: nothing is wrong. |
| Not JSON / not an object | Defaults. The file is **copied to `settings.json.bad` first**, so the next save cannot destroy what the player may want to read; Godot's error stream and the audit log say which file and why. |
| `ui_scale_percent` out of range or off the step | Clamped to 55–115 and rounded to the nearest 5; a warning says what was read and what is used. |
| `routes` not one of the three | Default; a warning. |
| A value of the wrong type (`"grid": "yes"`) | That key is default; a warning. The rest of the file still counts. |
| Unknown keys; a newer `version` | Ignored, with one warning naming them; what is known is read. |
| A window or toggle id the game does not have | Ignored silently — it is an older build's, or a renamed window's (§4). |
| The file cannot be written (permissions, disk) | Godot's error stream and the audit log say so; the game carries on. A preference is not worth halting a village for. |

Problems found while reading are held until the village's log exists and written there once, beside
an INFO line *"Settings read from …"* — the audit trail is where a player's report is answered.

## 7. ⛔ The probe never reads the player's file

The probe (`BCLONE_PROBE_WIDTHS`) asserts the **defaults**: `bar height 151` is the bar at 75 %, the
`windows` line requires *Stock limits* to start hidden and *Professions* open, the `map toggles` line
reads every tick. **If the probe read Joe's own file, his UI size would move the bar height and his
ticks would redden the probe on his machine and not on anybody else's.** So under the probe the settings
path is null: nothing is read and nothing is written. The probe's own round trip (§8.2) uses a scratch
file it deletes.

## 8. Guards

**Sim (`PlayerSettingsTests`, `NewGameScreenTests`):**
1. No file → defaults, no problem.
2. A record with every field set survives save → load unchanged.
3. Not JSON → defaults, one problem naming the file, and `settings.json.bad` holds the original bytes.
4. Scale 30 / 200 / 77 → 55 / 115 / 75, each with a warning.
5. An unknown `routes` → default, a warning.
6. A wrong-typed value → that key default, the others read, a warning.
7. Unknown keys and `version: 99` → what is known is read, a warning.
8. Saving leaves no `.tmp` behind and replaces an existing file.
9. Remembered rows: an unknown row is dropped, a refused value is the config's default, an allowed one
   is kept; and the share code of the remembered screen equals the same values chosen by hand.

**View (probe line `settings:`):**
1. Under the probe the player's file was not read.
2. A posed record — every toggle flipped, 100 %, Routes all, every shown window rolled up and moved —
   goes through a scratch file, is applied, and is captured back **equal** (places to the pixel); then
   the founding's own capture is applied and comes back equal. This is what catches a toggle or window
   that apply or capture forgets.

## 9. Order of work

1. ✅ **This spec** (D504).
2. ✅ **The sim half** (D505) — `PlayerSettings`, `PlayerSettingsFile` (`Bclone.Sim/Config/PlayerSettings.cs`),
   `NewGame.Remembered`; `PlayerSettingsTests`, two cases in `NewGameScreenTests`. **10 of 10 mutants red
   (15 reds)** — one first failed to build (an unused `using`) and was re-posed so it reached the tests.
3. ✅ **The view** (D505, `Main.Settings.cs`) — read before the new-game screen, applied after the panels
   are built, written at the end of a frame that changed something, the path under *About this run*,
   probe line `settings:`. **9 of 9 mutants red on the final line.** ⚠️ Two zeros, kept and written
   down: the line's first draft built its pose *from the capture*, so a tick the capture forgot was
   missing from both sides (D419's trap) — fixed, the pose now comes from the registries and the
   capture must name every one; and a place that forgets the UI scale comes back right at 100 %, so the
   place pose runs at 90. ⚠️ **Unguarded by any automated check**: the release of a drag writing a place
   (headless cannot drag) and the file actually being written (the probe never writes) — §10.4.
4. Joe plays it (§10).

## 10. Definition of Done

1. This spec current, status line true.
2. Guards §8 written, red-checked, the reds counted; determinism green; **no golden moved**.
3. View builds with 0 warnings; probe green — `bar height` 151, `new game:`, `map toggles`, `windows`
   and `settings:` ✅, `done.` printed.
4. Joe has played it: sets the size, a tick, Routes, a window off, a fold, a drag, founds a valley with
   a row moved — quits, relaunches, and finds all of it; *Reset window positions* stays reset across a
   relaunch; a hand-broken file opens on defaults, says why, and keeps `settings.json.bad`.
5. DESIGN §4, §6 and §7, and `HANDOFF.md`, updated in the same commits as the code.
