# Handoff — bclone: **Phase 2 is done. The only thing left is PR #3, and it is Joe's call.**

Read `CLAUDE.md`, then **`DESIGN.md` §0–§5 in full, §6, and §7 from D169 back to D142**, then
`METHODOLOGY.md`. `specs/crops-and-orchards.md` is a **record** rather than a job — read §12 only
if you touch the crop numbers, and **do not touch them**.

---

## Where things are

**Branch `phase/2-wood-fuel-and-tools`**, thirteen commits, tree clean, **not pushed**. Head is
`0dd19c1` — *"The farm's brush speaks farming, and the panels stop eating the map"*.

**SUITE IS CONFIRMED GREEN, FROM A RUN:**

```
627 passed, 0 failed, 2 skipped of 629 — 12m12s
```

⚠️ **The previous handoff said "expect 626 passing" and 626 was the *total*.** Fourth time this
project has put a count in a document without a run behind it. `dotnet test bclone.sln --nologo
-v q` takes about **twelve and a half minutes**; background it and wait for the notification.
**Do not start a second run while one is going** — and check `Get-Process testhost` first: this
session found the *previous* session's run still alive on thirteen cores, holding the lock, with
nobody left to read its output.

**The Godot view builds separately** — `dotnet build src/Bclone.Game/Bclone.Game.csproj` (D11).
It has **no automated verification** (D160); looking at it is the test.

---

## ⭐⭐ PHASE 2'S DEFINITION OF DONE IS MET (D169)

All five items. Joe walked the QA checklist on 2026-08-22 — *"I've walked the QA checklist and
approved the document"* — which was the last one open. `DESIGN.md §4`, `§6`, and
`specs/phase-2-the-village-you-can-play.md` all say so now.

### ⛔ WHAT IS BLOCKING, AND IT IS NOT A TASK

**`PR #3` has NOT been opened and the branch has NOT been pushed.** That is deliberate: **all
three fixes this session are view changes, and the view has no tests at all.** The numbers behind
them are real and measured, but *nobody has looked at the game*. **Joe looking is the last check
before the merge**, and it is the same bar D160 set.

**What to ask him to look at** — the three things he reported, in the order he reported them:

1. **The jitter** — watch a farmer sow and reap a field, and a forester work painted ground, at
   4× and 10×. That is the exact case he named.
2. **The farm's brush** — paint a field bigger than its farmers can work. He should get a
   sentence naming *fallow* and both remedies, on the stroke and again on the farm's own panel.
   Painting must still be allowed.
3. **The panel widths** — select a building and watch the right-hand column. It should no longer
   grow when a long sentence appears in the inspector.

If he says yes: push, open PR #3, merge. **Do not open Phase 3 on an unmerged branch** (D161).

---

## What landed this session (D169)

### 1. ✅ The jitter was in the VIEW, and it was never in the sim

`VillageMap` advanced its glide bookkeeping on `_alpha >= 0.999`, and
`FixedTimestepDriver.Alpha` is *the accumulator remainder in `[0,1)`, sampled once a frame* — a
condition that is essentially never true. So the glide's start point froze on a tile from tens of
seconds ago, and anybody standing **within one tile of it** was drawn snapping back to it at every
tick boundary. That is a bounce between two tiles, once a tick, for as long as they stand still —
**which is a farmer on a field and a forester on painted ground**, the two cases Joe named. It
watches `World.Tick` now.

**⭐ The handoff's standing hypothesis was wrong and the sweep is why we know.** Scanning the whole
of `logs/bclone-20260822-000011.log` (10,476 ticks, 4,629 behaviour transitions) for a villager
returning to a tile they had just left finds **15**, of which **one** is anybody in a field. A
sower walks a clean serpentine and turns round once at the end of a row. `NextFieldToWork` is not
the problem. **D163's and D166's fixes were both real and both stand** — three rounds, three
causes, and only the third was on the screen.

### 2. ✅ The farm's brush speaks farming and names both remedies

`SimWorld.OverstretchedNote` — **one door**, so the brush (once per stroke, D42) and the panel
(for as long as the state lasts, D86) cannot disagree. A field that outruns its farmers **lies
fallow**, which is true since D167 capped sowing at what the hands can bring in; *untended* was
the forester's word. Still a warning, never a refusal. **3 reds of 3** against the old wording,
eight existing allowance guards green throughout.

### 3. ✅ The panels stopped deciding how wide the window is

D149 gave each column 27% and **Godot overruled it**, because a column is never narrower than its
widest child. Measured with `BCLONE_PROBE_WIDTHS` (see below): the inspector's idle row wanted
**733 px** on Joe's 267-px column; ground 548, queue 459, staffing 365. Every row is
**caption-above, controls-flowing-below** now — wrapped text plus an `HFlowContainer`, whose
minimum width is its widest single child rather than the sum. **Every row wants 120.** The right
column's floor drops from 733 to 262 (the minimap).

### ⭐ And a tool that is kept: `BCLONE_PROBE_WIDTHS`

```bash
BCLONE_PROBE_WIDTHS=1 "$GODOT" --headless --path src/Bclone.Game
```

Walks the control tree, prints every minimum width including the inspector rows posed with their
worst-case sentence, and quits. Two seconds. **Three sessions have asked this question and two
hand-rolled the same throwaway** (D149, D169). METHODOLOGY §6 has it. It is a *measurement*, not a
hook that plays the game — the distinction D160 drew when it deleted `BCLONE_SCREENSHOT`.

---

## ⛔ THE CROP NUMBERS ARE LOCKED

Joe: *"farmer is good now - dont change the yield."* Do not re-derive `crop_yield_per_tile`,
`sow_ticks`, `reap_ticks`, `farmhouse_seats` or `farm_store_cap` without him.

---

## The queue after the merge (`DESIGN.md §4`, unchanged)

1. **`specs/skills-catalog.md`** — catalogues before code, and the prerequisite `tech-tree.md`
   silently assumes.
2. **Phase 3 — skill and apprenticeship** (§2.1), which is also the real answer to the mid-game
   gap (D161). Its success test is already written: *play years 1–16 at normal speed, without
   fast-forwarding, and want to keep watching.*
3. **Phase 4 — the tech tree** (§2.7).

**Two directions Joe set, neither scheduled, both in `DESIGN.md §4`:** **gridless** (the largest
architectural statement anybody has made about this project — the first question when it is taken
is whether the *sim* goes continuous or only the *presentation*), and **mods that can add
anything** (`BuildingKind`, `JobKind`, `Goods` and `Terrain` are four C# enums hashed by position;
`crops-and-orchards.md §4` is the template for doing it right). Standing discipline for the second:
**when you add a new kind of thing, ask whether it wants to be an enum value or a data row.**

---

## Traps, in the order they will cost you

- **⭐⭐ CHECK EVERY GUARD RED, AND COUNT THE REDS.** Three times this session's predecessors it
  caught a guard that proved nothing. This session: 3 of 3 on the brush wording, and the guard
  that actually catches the bug is not the obvious one — *"the farm's sentence says farmer"*
  passes against a generic template with the farm's name in it. **The one that works reads both
  sentences with the names masked out and requires them to differ.**
- **⭐⭐ THE SIM'S AUDIT TRAIL IS EVIDENCE ABOUT THE SIM AND SAYS NOTHING ABOUT THE VIEW.** Two
  sessions hunted a rendering bug in `BehaviorSystem` because the trail is the best instrument
  this project has and it was pointed at the wrong half of the codebase. **Ask which half the
  symptom lives in before opening the log.**
- **⭐⭐ FINDING A CAUSE IS NOT FINDING THE CAUSE** (D163, D166, D169 — three rounds).
  **Sweep the whole trail and count the shapes** before believing any one of them.
- **⭐ A LAYOUT YOU CAN READ IS NOT A LAYOUT YOU CAN PREDICT.** A column is never narrower than
  its widest child, so every width in `Main.BuildUi` is a *request*. **Probe, do not reason.**
- **⭐ THE HELPER YOU NEED MAY ALREADY EXIST.** `Main.Wrapped` had been doing exactly the right
  thing on five labels for two UI rebuilds while every sentence in the inspector went into a bare
  `Label` in an `HBox`. Grep before writing.
- **⚠️ IF A NUMBER GOES INTO A DOCUMENT, IT COMES FROM A RUN.** Four for four now, the fourth
  being the previous handoff's own warning about it.
- **`python` is not on PATH**, and string edits die on this repo's CRLF and its emoji. Use
  `perl -0777`, or the Edit/Write tools for anything with quoting in it.
- **`dotnet test` buffers stdout when redirected**, so a background run looks frozen at zero
  lines. `Get-Process testhost` and watch CPU to tell working from hung — and note the CPU figure
  is summed across cores, so a healthy run shows *far* more CPU-seconds than wall-clock.
- **⚠️ DO NOT START A SECOND SUITE RUN WHILE ONE IS GOING**, and **check for an orphan from the
  last session first**. It holds the lock on `Bclone.Sim.dll` and no test project will build.
- **⚠️ DO NOT WRITE A WAIT-LOOP FOR A BACKGROUNDED RUN.** The completion notification arrives by
  itself.

---

## Working with Joe

Technical, not a game or systems programmer. Casual, direct; **push back honestly**. **End every
message with the explicit ask**, or he cannot tell who is blocking whom. **His play is the best
bug-finder this project has** — and D169 is the sharpest case yet: he reported one symptom three
times across three sessions, and it took all three to get from the two real bugs it was hiding to
the one that was actually on his screen.
