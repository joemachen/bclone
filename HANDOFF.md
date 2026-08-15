# Handoff — bclone

Read `CLAUDE.md`, then **`DESIGN.md` §6 (Progress Tracker) and §7 (Decisions Log), D158 back to
D142**, then `METHODOLOGY.md`, then the spec for whatever you are about to touch. The spec that
matters most for what comes next is `specs/goods-on-the-ground.md` **§5.3 and §5.3a**.

---

## Where things are

**Branch `phase/2-wood-fuel-and-tools`. Step C is merged and `wip/step-c-retire-thickets` is
done with** — 53 commits, merged `--no-ff` as *"Step C: the thickets retire and the valley stops
feeding you"*. The tree is clean.

**Suite: 570 passing, 0 failing, 2 skipped of 572. Green.** It opened the session at
533 / 13 / 9 of 555. **Both remaining skips are rulings, not debt** — an unattended village is
supposed to die out (D143), and D134 retired the granary as the binding cap in favour of the
timber shed. Both skip reasons now say the right thing (D159 fixed the one that did not).

**The Godot view builds** (`dotnet build src/Bclone.Game/Bclone.Game.csproj` — the solution
build does *not* cover it, D11).

**Phase 2's Definition of Done is still not met**, which is why this branch is unmerged to
`main`. The headline (environment and seasons) wants its last slice; see §6.

---

## ⛔ START HERE

**Write the golden that covers a village clearing ground.** It is the one hole this session
opened and Joe's call was to take it on this side of the merge (D158).

D157 measured that **neither existing 50-year golden paints a single tile** — `HarvestTiles` is
**0 on all 24,000 ticks** of both the fixture and the shipped run, because both start from an
*established* village whose houses already stand, so nothing is ever marked on wooded ground.
The clearing path therefore has **no drift guard at all**, and it is the mechanic step C is
built on. Do not re-take the two existing goldens — they are correct and unmoved; add a third
over an opening that actually clears.

---

## What happened this session, in one line each

| | |
|---|---|
| **D157** | ⛔⭐⭐ The village promised to clear a marked building's ground and, for anything further out than its own coppice, **never did**. Cost every village that sited a hut in real woodland. |
| **D158** | Joe's three rulings: hunger reads as **pressure**; `firewood_per_split` **stays at 50**; the missing golden waits for the next slice. |

**D157 in full, because it is the shape to remember.** D100 paints a marked building's footprint
for harvest and promises *the village clears the ground, the player does not have to*. D127 then
made harvest paint a **standing** instruction whose wood grows back. `NearestHarvest` is
cost-first. Put together: between the village and a footprint eight tiles out there is *always*
nearer coppice, and it always regrows before the laborers exhaust it — **so the footprint is
never the nearest tile and is never cleared at all.** A gatherer's hut marked in the best
woodland was still standing on `Forest` at year forty, with the panel saying *"the ground it
stands on is still being cleared"* the whole time, and all four founders frozen in winter 1.

**Neither decision was wrong. Nobody was standing on the seam between them.**

Fixed as a **priority, not a scope change**: a blocked footprint is taken before any coppice —
free buildings first in marking order, then construction sites in **the build queue's own order,
rank then id** (Joe: *"clearing order should defer to the build queue"*), so clearing defers to
building instead of inventing a second ordering over one list.

---

## The cold start, re-measured — these are the current numbers

Shipped config, founding site `(-1,-1)`, forty years. **Quote these, not D119's.**

| | D119 | now |
|---|---|---|
| gatherer's hut stands | t224 | **t161** |
| staffed | t241 | t241 |
| first firewood | t253 | **t133** |
| first house | — | t297 |
| winter 1 | t360 | t360 |
| alive at year 20 | 4 | **12** |
| alive at year 40 | — | **22**, six homes |

Nobody starved, nobody froze. **D119's stall — *"four people, forever"* — is gone**, and that is
D153/D155/D156 rather than D157. Joe's original stated risk (no food until the hut stands) still
does not happen; the cart covers it.

---

## Open with Joe — do not decide alone

1. **⛔ The mid-game gap is still the real design problem**, in his words: *"how to keep the game
   interesting between stabilising the village and the first children becoming laborers."* That
   window is about **twelve years**. §2.3's dead-late-game question arriving early. Nothing has
   been done about it.
2. **His notes for later are in `DESIGN.md` §6**, recorded but not designed: a Banished-style
   **town hall** (he has more detail to give); an Animal-Crossing-style **museum**; **real-time
   charts** with 1/5/10/20-year lookbacks, gated behind the town hall; **variety** of fish, crops,
   trees, game and livestock; **nomads**.
3. **Two questions closed this session — do not reopen them.** Hunger at 10–21% of deaths *is*
   the intent (so a later change pushing it well past a fifth is a regression against a stated
   target, not a matter of taste), and `firewood_per_split: 50` is settled.

---

## ⚠️ Traps, carried forward and added to

- **⭐⭐ AN ARM THAT VARIES ONE THING AND DIES BOTH WAYS HAS RULED THAT THING OUT AND SAID
  NOTHING ABOUT THE CAUSE.** This is the session's own lesson and it is D142's and D151's for the
  third time. `HousesAreBuiltTests`' skip ran a control, correctly wrote *"the granary is
  innocent"*, and then wrote *"what kills them is the opening needing a reacting player"* —
  **which it had not tested at all.** That wrong cause sat in the skip text of seven guards for
  two days. The right cause was in the builder's own `WorkNote`, in English, in the audit trail.
- **⭐ THE AUDIT TRAIL IS EVIDENCE AND THE SUITE IS NOT** (carried from last session, paid for
  again). D157 was found by printing villager `State` and `WorkNote` every thirty ticks, not by
  a test. **Ask Joe for the log path from his header; it is in every screenshot.**
- **⭐ Anti-vacuity is not optional.** Six of the seven restored guards were checked **red**
  against the fix by stubbing the priority pass out. Do not believe a guard you have not seen
  fail.
- **A green golden can mean "not covered" rather than "no-op".** D157 expected both goldens to
  move, and they did not — so the reason was *measured* rather than assumed, and it was that
  neither reaches the code. **If a change should have moved a number and did not, find out why.**
- **`python` string replaces silently no-op on CRLF files, and now also die on the emoji** —
  `UnicodeDecodeError: 'charmap' codec`. Use the Edit tool. This cost a run again.
- **`dotnet test` buffers stdout when redirected to a file**, so a background run looks frozen at
  ten lines for eleven minutes. Check `testhost`'s CPU (`Get-Process testhost`) to tell working
  from hung — it was at 6,187s when it looked dead.
- **The full suite is ~11–13 minutes.** Background it. Do not trust `tail` to capture the failure
  list.
- **Fixture-vs-shipped divergence, now seven times.** METHODOLOGY §3 exists because of it.

---

## Working with Joe

Technical, not a game/systems programmer. Casual, direct; push back honestly — his call on
build-queue ordering was better than my first cut, which had invented a second ordering over one
list. **End every message with the explicit ask**, or he cannot tell who is blocking whom.
