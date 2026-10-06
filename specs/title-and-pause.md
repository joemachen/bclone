# Spec: The title and pause screens — a door in, and a door out

**Decisions:** D353 (the shell's order: error boundary → per-stage seeds → new-game screen → settings
persistence → save/load → **title and pause**), D477/D479 (the new-game screen — `new-game-screen.md`), D504
(settings persistence — the probe never touches the player's files), D507–D510 (save/load — `save-load.md`:
*Continue* and *Load…* on the new-game screen *"until the title screen exists"*, *Save* in Settings *"until the
pause screen exists"*), D364 (the error boundary — a stopped village is not saved), **D516 (Joe's calls for this
screen, 2026-10-05 — §2)**.
**Status:** 🔨 **BUILT (D517, 2026-10-05) on `slice/title-and-pause` — not yet played by Joe.** View only; nothing in
`Bclone.Sim` changed and no golden moved. Probe `title:` and `pause:` green, **9 mutants, 10 reds, no zeros**. ⚠️ What
the probe cannot press (§8): *Continue* and the backdrop from a real save, *Quit to title*, a pause-screen *Load…*,
*Quit to desktop* — they wait for §10.5. Owner: Joe + Claude Code.

---

## 1. Why this exists

The shell's last two doors. Today the game opens straight onto *A new valley*, with *Continue* and *Load…*
tucked under its *Found* button; and once a village is running there is no way out of it but the window's ✕,
and *Save* lives in Settings beside the share-out tick. Save/load put both there **until these screens exist**
(`save-load.md §8`, §11.3) — this slice moves them where they belong and adds the two exits a game is expected
to have: back to the title, and to the desktop.

## 2. Joe's calls (D516, asked before the spec)

1. **The name on the title screen: "bclone" for now** — the codename, as Godot's window title already says.
   One string in one place, so the day it has a name it changes once.
2. **Behind the title: your latest valley** — the valley of the newest save that opens, painted by the game's
   own `ValleyTexture` (the new-game preview's painter); a fresh random valley when there are no saves.
3. **The pause screen holds all four**: *Resume* · *Save* (moved from Settings) · *Load…* (the title's list;
   loading another village autosaves this one first) · *Settings* · *Quit to title* (autosaves first) · *Quit to
   desktop* (autosaves first).

## 3. The title screen (`TitleScreen.cs`)

What the game opens on. ⭐ **Its rules are Main's and the sim's; the screen only lays them out** — the
new-game screen's shape (`NewGameScreen`, which raises events and Main acts on them).

- **The backdrop**: the newest save that opens, opened with `SaveFile.Open` (14–31 ms, `save-load.md §3`) and
  baked whole by `ValleyTexture.BakeAfresh` — the real valley, with its clearings, fields and worn paths — then
  the opened loop is dropped. ⛔ **Continue does not reuse that loop**: a village comes in from disk by one door
  (`Main.OpenTheVillage`), and a second door is how the two come to differ. No saves (or the newest refused):
  a valley from a rolled seed, the new-game screen's own dice. Under the probe there are never saves
  (`save-load.md §9`), so the probe always sees the rolled valley.
- **The words, over a dimmed band on the left**: **bclone** large; the build under it small (`v0.0.1` — the
  same `BuildVersion` the save list shows).
- **The buttons, in this order:**
  - **Continue** — opens the newest save that opens. Its tooltip names it: *"Ashford — Autumn, Year 12 ·
    autosave1"* (the load list's line). With none: disabled, tooltip *"No saved village yet."*
  - **New village** — the new-game screen (§4).
  - **Load…** — the popup that sits on the new-game screen today, moved here unchanged (every save, newest
    first, a refused one greyed with its refusal as the tooltip). Disabled when there are no saves.
  - **Quit** — closes the game. Nothing to save: no village is open.
- **A refused save says so here**: *Continue* or *Load…* on a save that will not open leaves the title up with
  the refusal sentence under the buttons (`save-load.md §7`'s words — never a half-loaded village).

## 4. The new-game screen

- **Loses *Continue* and *Load…*** (they are the title's now) — `BuildTheSaves` goes, with its `Saves`
  property and `LoadAsked` event.
- **Gains *Back***, left of *Default settings*: to the title. Nothing is founded; the rows the player moved are
  not remembered (they are remembered when a village is founded, D504 — unchanged).

## 5. The pause screen (`PauseScreen.cs`, inside Main)

- **Esc opens it** when there is nothing nearer to close: a tool in hand goes down first, then *What's here*
  closes (D327, D390 — unchanged), and only then does Esc pause. ⛔ A moment panel still takes Esc to dismiss
  itself (its branch returns before the switch). **Esc or *Resume* closes it.**
- **It stops the village while it is up**: the speed it found is kept and given back on *Resume* — so a player
  at 4× comes back to 4×, and one who had paused with Space comes back paused. While it is up the keyboard does
  nothing else (no speed keys, no Tab, no B) and the map takes no clicks — the screen is a full-window dim with
  a centred panel that stops the mouse.
- **The panel, top to bottom:**
  - **Paused** (heading), and *"{village} — {Day N, Season, Year N}"* under it.
  - **Resume**.
  - **Save**: the name box and *Save* button, as they were in Settings (an empty name saves as `year-N`), with
    the caption *"It also saves itself every spring and when you quit — the last 3 are kept."* The result is
    said in the village log as today (*"Saved as …"*, or the refusal).
  - **Load…** — the title's popup. Picking a save: this village is autosaved (*"before loading another"*), then
    the chosen one opens — see §6 for why that order needs care.
  - **Settings** — closes the pause screen and opens the Settings window. ⚠️ **The village stays paused** (the
    kept speed is not given back), because the player went to Settings, not back to the village; Space or a
    speed key resumes as always.
  - **Quit to title** — autosaves (*"on quit to title"*), then the title screen.
  - **Quit to desktop** — autosaves (*"on quit"*), then closes the game.
- **On a village stopped by an error** (`_halted`): *Save* is refused in the log exactly as today; the two
  quits go without saving — `Autosave` already returns on a halted village — and their tooltip says so:
  *"The village stopped on an error and will not be saved — the last autosave is from Year N."*
- **Settings loses its Save row** and its caption. The share-out tick stays where it is.

## 6. Leaving a village — the scene is reloaded, never torn down

⭐ **Quit to title and Load… from the pause screen both reload the scene** (`GetTree().ReloadCurrentScene()`),
and the fresh `Main` opens on the title — or straight into the chosen save. Main builds its UI once, from
`StartTheVillage`, and nothing anywhere takes it down; a teardown would be a second path through every panel,
cache and signal in 23,000 lines of view, written to be wrong once. A reload cannot leak the old village into
the new one. Checked before choosing it: **the view has no mutable statics** (only `static readonly` colours
and one style box), and `_ExitTree` already disposes the audit log.

- **What crosses the reload** is one static, `Main.OpenOnStart` — the path of a save to open, or null. Set just
  before the reload; read and cleared by `_Ready`. A refused open there shows the title with the refusal (§3).
- ⛔⛔ **THE AUTOSAVES ROTATE BY RENAMING** (`SaveFile.WriteAutosave`: `autosave1` → `2` → `3`, the oldest
  deleted). So *"autosave this village, then open the save the player picked"*, done in that order, opens the
  wrong file when the pick is one of this village's own autosaves — and **a deleted one** when it was the
  oldest. **The chosen file is copied aside first** (`user://saves/.opening` + the extension), then this village
  is autosaved, then the copy is opened by the one door and deleted; the village log names the file the player
  chose, not the copy.
- **Quit to desktop** autosaves and calls `GetTree().Quit()` — which does **not** raise
  `NotificationWMCloseRequest`, so the autosave is explicit, never left to the ✕'s handler.
- The window's ✕ is unchanged: autosave on quit, from a running village only.

## 7. The words

| Where | Words |
|---|---|
| Title | **bclone** · `v0.0.1` · **Continue** · **New village** · **Load…** · **Quit** |
| Continue, none | *No saved village yet.* |
| New-game screen | **Back** |
| Pause | **Paused** · *{village} — {Day N, Season, Year N}* · **Resume** · **Save** · **Load…** · **Settings** · **Quit to title** · **Quit to desktop** |
| Pause, halted | *The village stopped on an error and will not be saved — the last autosave is from Year N.* |
| Log | *Autosaved (on quit to title) to …* · *Autosaved (before loading another) to …* |

## 8. Verification

Nothing in the sim moves, so the suite and every golden must be byte-identical; the guards are the probe's.
Two new lines, each red-checked (D326) by breaking the thing it claims and counting the ❌:

1. **`title:`** — the probe now meets the title first. ✅ when: the backdrop has a valley on it; *Continue* is
   disabled (the probe has no saves) and *New village* is enabled; the words column ends inside the window. Then
   the probe presses *New village* and the `new game:` line runs as it always has.
2. **`pause:`** — on the running village: Esc with nothing in hand opens the pause screen and the driver reads
   paused; Esc again closes it and gives back the speed it found (posed at 4×); Settings has no Save row; the
   pause screen has one. It never presses Save, Load or a quit (the probe never touches the player's saves, and
   a quit would end the probe).
- `bar height` stays **151**, `tile centres`, `new game:` and `save:` stay ✅, and `done.` prints.
- **Red-checked (D517)** — each mutant's landing confirmed by a single exact match before its probe ran:

  | Mutant | Broke | Read |
  |---|---|---|
  | T1 | the backdrop never set | `title:` ❌ no valley |
  | T2 | *Continue* always enabled | `title:` ❌ offered with no save |
  | T3 | the band 3000 px wide | `title:` ❌ ends at 3000 of 1280 |
  | P1 | Esc's third step removed | `pause:` ❌ did not open |
  | P2 | the village not stopped | `pause:` ❌ runs at 4× **and** a speed key reached it (2 reds) |
  | P3 | the keyboard gate only for Esc | `pause:` ❌ a speed key reached the village |
  | P4 | *Resume* gives back 0× | `pause:` ❌ gave back 0× |
  | P5 | the Save row back in Settings | `pause:` ❌ Settings still has Save |
  | P6 | no Save row on the pause screen | `pause:` ❌ no Save |

  **9 mutants, 10 reds, no zeros.** ⚠️ The ways out (*Quit to title*, *Load…* through the copy aside, *Quit to
  desktop*) and *Continue* are not probed: a probe that pressed them would end itself or touch the player's saves.
- **Joe plays it** (§10).

## 9. Not in this slice

- **A full settings screen** (D508) — the autosave cadence and whatever else gathers there. *Settings* on the
  pause screen opens today's window.
- **Ironman mode** (D507) — its name is Joe's.
- **A real name and a title treatment** — *"bclone" for now*.
- **Deleting or renaming saves** from the list.

## 10. Order of work, and what Joe looks at

1. This spec, D516 in §7, §6's line — committed first.
2. `TitleScreen.cs`; `_Ready` opens it; the new-game screen's saves move to it and it gains *Back*; the
   `title:` probe line.
3. `PauseScreen.cs`; Esc's third step; the Save row moves; *Quit to title*, *Quit to desktop*, *Load…* through
   `OpenOnStart` and the copy aside; the `pause:` probe line.
4. The four verification lines (CLAUDE.md); goldens by `git diff`; this status line true.
5. **Joe plays** (`run.bat` on the branch):
   1. Launch: the title over the valley of his newest save; *Continue*'s tooltip names it. *Continue* opens it.
   2. In the village, Esc (nothing in hand) pauses it under the dim; Esc again resumes at the speed he had.
   3. Save under a name from the pause screen; *Load…* an autosave of the **same** village — it opens the one he
      picked (§6's copy aside), and the log says *"Autosaved (before loading another)"*.
   4. *Quit to title*: back at the title, and *Continue* now names the autosave just written. *New village* →
      *Back* returns to the title.
   5. *Quit to desktop*, relaunch: *Continue* opens where he left.
