# Handoff — bclone: **Phase 2 merged, the skills catalogue written. Next is per-site yield.**

Read `CLAUDE.md`, then **`DESIGN.md` §0–§5 in full, §6, and §7 from D177 back to D142**, then
`METHODOLOGY.md`.

⭐ **The town hall is designed now** (D176, `specs/tech-tree.md §7f`) — the fourth knowledge
building, holding the knowledge screen and the collections. **It gates the screen, not the tree.**

---

## Where things are

**On `main`, and `main` is Phase 2.** Merged 2026-08-22 via
[PR #4](https://github.com/joemachen/bclone/pull/4) — 248 commits, all five Definition-of-Done
items met, the last of them Joe's QA walk against
`specs/phase-2-the-village-you-can-play.md`.

⚠️ **It is #4, not #3.** Number 3 went to the closed screenshot-hook PR D160 rescued, and every
document in the repo said #3 for a day before anyone checked.

**`phase/2-wood-fuel-and-tools` is deleted**, local and remote, on Joe's call and after checking
it had **0 commits not on `main`**. Its tip was `9b9f410` if it is ever wanted back.

**SUITE, FROM A RUN:**

```
630 passed, 0 failed, 2 skipped of 632 — about 17–19 minutes
```

The two skips are rulings, not unfinished work: **D143** (an unattended village is *supposed* to
die out) and **D134** (the granary stopped being the binding cap; the timber shed is).

```bash
dotnet test bclone.sln --nologo -v q
```

**Background it and wait for the notification. Do not start a second run while one is going** —
and **check `Get-Process testhost` first**: one session found the *previous* session's run still
alive on thirteen cores, holding the lock on `Bclone.Sim.dll`, with nobody left to read its
output. Note the CPU figure is summed across cores, so a healthy run shows far more CPU-seconds
than wall-clock.

**The Godot view builds separately** — `dotnet build src/Bclone.Game/Bclone.Game.csproj` (D11) —
and has **no automated verification of any kind** (D160). Looking at it is the test.

---

## ⭐ What to do next — `DESIGN.md §4`'s queue, in its order

1. ✅ Phase 2 merged, and the branch deleted (it was fully merged; tip was `9b9f410`).
2. ✅ **`specs/skills-catalog.md` WRITTEN** (D173–D177), docs-only on `main`. **Nothing in it is
   built** and its status line says so. What to know before touching Phase 3:
   - **⭐ §3.2 (D174): today's behaviour is the NOVICE FLOOR.** **Nobody is ever worse than
     today**, mastery is headroom above. **The derivation is a survival floor (§2.2) and the
     novice is that floor**, so no derived number moves.
   - **⭐ Mastery is twenty years on the task**, narrated in the village log when it happens —
     Joe asked for that line by name, and it is the first thing here the player will feel.
   - **⭐ §3.2c + §3.5 (D175): the founders arrive as a MIX OF TIERS, and every villager gets a
     SEEDED RHYTHM at birth.** Together they **close D28 at the opening** instead of a century
     later, and they make the four founders people rather than units.
   - **⛔ THE TRAP IN IT:** §3.2 says the *floor* does not move; **it does not say the founding
     does not move.** A party with a master in it starts **above** the floor, so `cold-start.md`
     is re-measured and the goldens move once. **The byte-identical guard belongs to a *posed
     all-novice village*** — a guard aimed at the real opening will fail, and the temptation will
     be to weaken it rather than pose it properly.
   - **⚠️ Unmeasured**: does a master gatherer make the opening trivial? Probe first.
   - **✅ §5.4 (D176): WHAT CAN ACTUALLY BE LOST.** *Mastery* was one word doing two jobs. Split
     into **proficiency** (one person's years — dies with them, always, never writable),
     **technique** (the village's, re-locks exactly as `tech-tree.md` already said) and **a
     record of achievement** (permanent, **grants nothing**), the question dissolves.
     **Mastery-the-tier is not a node and cannot be taken from the village.**
   - **✅ MASTERY BITES IN PHASE 3, GATED BY NOTHING (D177).** A later node may raise the ceiling
     further; none permits it. **That is what keeps Phase 3 from shipping D56's shape** — a
     system that accrues, is visible and changes nothing.
   - **⭐ PHASE 3 LANDS IN THREE PIECES AND ONLY THE FIRST IS A NO-OP** (§11): the **substrate**
     (goldens unmoved), then **mastery biting** (moves them), then **the mixed founding and the
     seeded rhythm together** (moves them once more). *Landing them apart is what makes a
     regression attributable.*
   - **✅ Milestones are LOG LINES in Phase 3** (D177) and gain their collections home in the
     town hall in Phase 4. **There is no milestones panel.**
   - **§6 is a contract** with `tech-tree.md`, whose header now points back at it, and §6.6–6.7
     are guarantees Phase 4 may rely on: **no record ever restores proficiency**, and **no
     knowledge state may gate mastery**.
   - **⚠️ What is left in §12 is tuning, not design**: the width between novice and master, the
     founding party's composition, the tier names, and whether skill scales yield as well as
     duration. **Every one wants a probe before an implementation.**
3. **⭐ NEXT: Per-site yield, and retiring the 7-tile bound** (D58, D172, Joe: *"per-site yield
   behind skills-catalog"*). **This is the next thing to do.** See below.
4. **Phase 3 — skill and apprenticeship** (§2.1), which is also the real answer to the mid-game
   gap (D161). Its success test is already written: *play years 1–16 at normal speed, without
   fast-forwarding, and want to keep watching.*
5. **Phase 4 — the tech tree** (§2.7).

**Two directions Joe set, neither scheduled, both in `DESIGN.md §4`:** **gridless** — the largest
architectural statement anybody has made about this project, and the first question when it is
taken is whether the *sim* goes continuous or only the *presentation* — and **mods that can add
anything** (`BuildingKind`, `JobKind`, `Goods` and `Terrain` are four C# enums hashed by
position; `crops-and-orchards.md §4` is the template for doing it right). Standing discipline for
the second: **when you add a new kind of thing, ask whether it wants to be an enum value or a
data row.**

---

## ⛔⭐⭐ The one known-open bug, and why it is queue item 3 rather than a fix

**A farm's harvest falls off sharply with distance from its store** (D170, D171). Measured, ten
years at each distance:

| farm → granary | brought in | with a 13-armful buffer |
|---|---|---|
| next door | 93–96% | — |
| 6 ticks | 52% | 59% |
| **10 ticks** | **46%** | 46% |
| 22 ticks | 25% | 30% |

Joe's own village landed exactly on the ten-tick row. **The cause is that
`FieldTilesOneFarmerKeeps` is one number for every farm in the valley**, so a distant farm sows
what a near one could reap and rots the difference every autumn.

**⛔ DO NOT REACH FOR `farm_store_cap`.** Measured: one armful against thirteen moves the harvest
by **nought to seven points**. The buffer is not the lever, distance is. It stays on Joe's locked
list *on evidence*, not deference.

**✅ What did land** — Joe's design, and `crops-and-orchards.md §3.2a`: the **market now runs the
farm's buffer dry**, which ruling 1 has promised since the farm shipped and nothing ever did. A
third marketer errand, offered against every other leg on travel cost, gated on the derived
condition that the buffer can no longer take a whole armful. **Worth +4 points — real, and not
the fix.**

**⚠️ And it owes the player a sentence when per-site yield lands.** D167 made the rot line mean
*you over-painted* or *you lost a farmer*. **Distance is a third cause the game cannot yet say**,
and a rot line nobody can act on is the weather D167 spent a decision deleting.

---

## Tools this project has that you would not guess

- **`BCLONE_PROBE_WIDTHS`** (METHODOLOGY §6). Walks the control tree headless in two seconds and
  prints what every panel and inspector row claims as a **minimum width**, including the rows
  posed with their worst-case sentence. **A column is never narrower than its widest child**, so
  every width in `Main.BuildUi` is a *request* — three sessions have asked this question and two
  hand-rolled the same throwaway before it was kept.
- **`HaulTheHarvest` writes its reason** — free space, both costs, which store won — so *"why did
  the farmer walk past the buffer?"* is one grep rather than an afternoon:
  `grep "food from the field" src/Bclone.Game/logs/<newest>.log`
- **The audit trail** at `src/Bclone.Game/logs/`. Almost every bug that mattered came out of it.

---

## Traps, in the order they will cost you

- **⭐⭐ CHECK EVERY GUARD RED, AND COUNT THE REDS.** Repeatedly this has caught a guard that
  proved nothing. **And the guard that catches a bug is often not the obvious one** — *"the
  farm's sentence says farmer"* passes against a generic template with the farm's name in it;
  the one that works reads both sentences with the names masked out and requires them to differ.
- **⭐⭐ A GUARD CAN BE GREEN AND BLIND.** `AFarmBringsInMostOfWhatItSows` reports 93% while the
  played village was at 46%, and it is not wrong — it sites its farm a step from the stores.
  **Unmoved because it does not cover the case** (D157, three times now). Ask what a guard's
  fixture *makes impossible* before trusting its number.
- **⭐⭐ THE INSTRUMENT IS AS LIKELY TO BE WRONG AS THE CODE.** In one session: a probe reported a
  farm reaping 60 of 60 tiles because its *"reaped"* column counted winter rot as harvest, and a
  guard was written claiming an untested happy path when the guard for it was ten lines above.
- **⭐⭐ THE SIM'S AUDIT TRAIL IS EVIDENCE ABOUT THE SIM AND SAYS NOTHING ABOUT THE VIEW.** Two
  sessions hunted a rendering bug in `BehaviorSystem`. **Ask which half the symptom lives in
  before opening the log.**
- **⭐ FINDING A CAUSE IS NOT FINDING THE CAUSE** (D163, D166, D169 — three rounds on one symptom).
- **⭐ THE HELPER YOU NEED MAY ALREADY EXIST.** `Main.Wrapped` had been doing exactly the right
  thing on five labels for two UI rebuilds while every sentence in the inspector went into a bare
  `Label` in an `HBox`. Grep before writing.
- **⚠️ IF A NUMBER GOES INTO A DOCUMENT, IT COMES FROM A RUN.** Four for four, the fourth being a
  handoff's own warning about it.
- **⚠️ CHECK A DOCUMENT'S REFERENCES AGAINST THE THING.** Every file said "PR #3" for a day.
- **`python` is not on PATH**, and string edits die on this repo's CRLF and its emoji. Use
  `perl -0777`, or the Edit/Write tools for anything with quoting in it.
- **⚠️ Goldens go last, one commit, one stated reason** (D152). The seam golden moves when a
  village that farms changes; the two fifty-year goldens do not, because **neither village ever
  places a farmhouse** — silent about what they do not reach, loud about what they do.

---

## Working with Joe

Technical, not a game or systems programmer. Casual, direct; **push back honestly**. **End every
message with the explicit ask**, or he cannot tell who is blocking whom. **His play is the best
bug-finder this project has** — and the clearest case is the jitter: he reported one symptom
three times across three sessions, and it took all three to get past the two real bugs it was
hiding to the one actually on his screen.
