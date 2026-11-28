# Move, revamped — ask first, show the outline, let it turn

> **Status: ✅ BUILT AND PLAYED; TWO PLAY NOTES FIXED (D948), ⏸️ HIS SECOND PLAY — on `slice/move-revamp` (D945 claim,
> D947 build, 2026-11-27), cut from `main` at `a92da93`.** Every call Joe's answered (D947: *"Yes, as recommended"* — all
> eleven). He played D947: *"otherwise everything is good"* (§6). Then the merge at his word.

Joe's play note, 2026-11-27: *"I do want to revamp the 'move' function. 1. build in 'are you sure' safeguard prompts
before moving a building 2. add a 'construction outline' thing to show the placement of the new spot, and allow
rotating, just like a fresh construction."*

## 1. What Move does today (read from the code, 2026-11-27)

Built as D229, given its button at D233. Sim: `SimWorld.MarkRelocation` (SimWorld.cs ~8393). View: the `_moving` arm of
`VillageMap.PlaceOrPullDown` (VillageMap.cs ~2008).

- **Two clicks, nothing in between.** Click a building, the hint says *"Moving the granary — click where it should
  stand"*, click a spot, and the move is marked. **No outline follows the cursor** (`DrawTheGhost` returns at once when
  `_building` is null, and a move never sets it). **No turning**: R and the middle-drag only act on `_building`. **No
  question asked.**
- **It keeps the facing it had** (D328). A move cannot change which way a building faces.
- **It is a builder's job, not a teleport** (D229). A site "*X (being moved)*" goes up at the new spot. It owes the
  building's **full raise work and no materials**. **The building keeps working where it stands until the builders
  finish**, and then the *same* building jumps: its name, store filter, workers, painted ground (the ground stays where
  it was painted), records and a workplace's stock all go with it.
- **Refused, with a sentence:** a house (*"unpaint the ground…"*, D228), a cemetery with graves (W4), a fenced barn
  (D598), a store with goods in it (*"There are 40 goods inside… Empty it first"*), and any spot `CanBuildAt` refuses.
- **Called off** by pointing Demolish at the "(being moved)" site: it is cancelled at once and nothing is lost, since no
  materials were delivered.

### 1.1 Three faults found while reading. They are the slice's first work, each test first.

1. **⛔ A move skips every "is this spot clear?" check, not only the one against itself.** `CanBuildAt(alreadyStanding:
   true)` skips the overlap test, the doorstep test, the wall-off test **and the whole covered-tile loop**. It is meant to
   stop a moved building from refusing *itself*. It also means a granary can be moved **on top of another building**, in
   front of someone's door, or, for a 2×2 or larger, with its far end **in the river or off the map**. *A red-or-green
   outline drawn from this verdict would show green over all of those, which is the D328 lie again.* The fix is to leave
   out **only the building's own footprint**, not every check.
2. **⚠️ Nothing stops a second move of a building already on the move.** Two sites, two lots of builder work. The first
   to finish moves it. The second then says *"there was nothing left to move"*, and its work is wasted.
3. **⚠️ A wrong sentence.** Pointing Move at a construction site says *"There is nothing there to move."* (`WhatStandsAt`
   only knows standing buildings.) That is the D229 sentence-bug shape again: the answer is right, the reason is false.

## 2. What the revamp does

### 2.1 The outline is the fresh-construction ghost, not a second one

When the player clicks a building with Move, **the view holds that building's kind as `_building`** and remembers
`_moveFrom`. Everything a new building's ghost already does then applies **with no second placement system**:

- `DrawTheGhost`: the footprint at its real size and angle, green, amber or red; the door and its doorstep; the seam
  it would claim (D613); a market's rings (D201); the tiles under it when the grid is on (D330/D345).
- **R** turns it a sixty-fourth of a turn, **shift+R** to the next quarter, **middle-drag** turns it a degree per
  pixel (shift: a tenth). The same keys, the same code (`TurnTheGhost`, `TurnTheGhostBy`).
- Snap on or off (D330) works as it does for a new building. `WhereItWouldStand` already looks at the moved kind.

What differs from a fresh build:

- **It starts at the facing it has now** (call 4), with `_ghostTurned = true`, so it does not swing round to face the
  nearest store as a new building's ghost does (`doors.md §12.3`).
- **The verdict comes from a new sim preview**, `SimWorld.CanMoveTo(from, to, facing)`. It runs every refusal
  `MarkRelocation` runs (house, graves, fence, full store, already moving) **and then** `CanBuildAt`, with only the
  building's own footprint left out. So the outline is red **before** the click, and the hint line gives the reason.
  `MarkRelocation(from, to, facing)` becomes `CanMoveTo` plus raising the site. *One rule, asked in two places, the way
  `Mark` and the ghost share `CanBuildAt` (D321).*
- **The building being moved is marked where it stands**: the selection's outline, so you can see what you have picked
  up.

### 2.2 The "are you sure?" box

On the click that would mark the move (the outline is green or amber), **the outline stays where it is and a small box
opens beside it** (call 1). Mock: `move-revamp-mock.html`, shown to Joe from the session's notes folder and not tracked (no mock in `specs/` is).

> **Move the granary here?**
> The builders will take it down and put it up again. That is the same work as building it, and no materials.
> It keeps working where it stands until they finish. Then its workers, its name and what it holds go with it.
> *[amber: the verdict's own warning, e.g. "That is 9 tiles from the village; it budgets 8."]*
> *[a building with painted ground: "Its fields stay where they are painted."]*
> **[ Move it ]   [ Not here ]**

- **Move it** (or Enter) marks it. The tool stays in hand for the next move, as it does today.
- **Not here** (or Esc, or a right-click) closes the box and **leaves the building in your hand**, so the player can
  try another spot without picking it up again.
- **The game keeps running** while the box is open. It is a question about one click, not a moment (D232 keeps the pause
  for gifts).
- **A refused spot never opens the box.** The click just says the reason, as it does today.

### 2.3 The smaller fixes riding with it

- **Right-click lets go of the building first** (call 6). The first right-click puts the building down, so you are back
  to *"click a building to move"*. A second right-click puts the tool down. That is the bridge's rule (D549's
  `_bridgeFrom`).
- **A building already on the move is refused by name** (call 7): *"The granary is already being moved. Point Demolish
  at its outline to call that off."*
- **A construction site gets a true sentence** (call 8): *"That is still being built. Demolish calls it off and gives
  back what was delivered; then place it again."*
- **The hint line** for Move gains the turning keys while a building is held: *"Click where it should stand. R turns it
  (shift: square), middle-drag: fine. Right-click lets go."*, or the sim's reason over a refused spot.
- **Refused at the pick-up** (D947, built): what no spot could change — a house, graves, a fenced barn, a full store, a
  site, a building already on the move, a bridge — is said on the first click (`SimWorld.WhyItStaysPut`), so the
  player is not handed a building only to be told on the second.
- ⚠️ **A bridge is not moved** (D947, built; not one of the eleven calls, so it is named here for Joe's eye): it is dragged
  bank to bank (D549) and has no outline to move by. Before, Move asked `CanBridgeAt` of a single point and refused with a
  bridge-laying sentence; now it says *"A bridge is not moved. Pull it down and build another where you want it."*
- ⚠️ **The outline is judged again after a turn, for a fresh building too** (D947, built): the verdict waited for the next
  mouse motion, so a ghost turned into the river by R stayed green until the cursor moved. A move's outline is drawn from
  that verdict, which made the lag worth fixing for both.

## 2.4 Joe's play of D947, and what changed (D948)

- *"calling off the move eventually worked, but it needed a lot of click attempts. it felt unresponsive."* — **Fixed.** The
  card's refresh hid every row and let each writer show its own again, every refresh; a button hidden and shown between the
  press and the release loses the click. The move's row is now set once, after the writers, and only when it changes.
  Probe line `[widths] call off the move:`; red-checked (the per-refresh hide put back: 12 changes over six refreshes, ⛔).
- *"i also dont like having to right-click to get to that window when previously it was a left click."* — read back to him
  as: with Move still in hand after *Move it*, a left-click on the building tried to pick it up again instead of opening its
  card. **His call: *"Yes, put it down"*** — the tool goes down once a move is under way, so the next left-click opens the
  card. A second move is Move again. Red-checked (the tool kept in hand: `move:` ⛔).

## 3. What is NOT changing

- What a move costs and does (D229): the full work, no materials, the same building arriving.
- Houses still move by the land brush (D228). Graves and fences still hold a building down.
- No save change: a site already stores its facing and `MovingFrom`, and every building already saves its own facing.
  No new data rows. **No golden moved**: 463 golden-length numbers in the tests before and after, and the diff touches
  none of them (D947).

## 4. ✅ Joe's calls — all eleven as recommended (D947, *"Yes, as recommended"*)

| # | The question | Recommended | The other way |
|---|---|---|---|
| 1 | **What form does "are you sure?" take?** | **A small box beside the outline, with Move it / Not here. The game keeps running.** | A pop-up that pauses the game, like the library gift. Or no box: "click the same spot again to confirm", which is lighter but easy to do by accident. |
| 2 | **What does it say?** | **The draft in §2.2:** the building's name, that it costs builder work and no materials, that it keeps working until the move is done, and any warning about the new spot. | Shorter: just *"Move the granary here?"* and the two buttons. |
| 3 | **Every move, or can it be turned off?** | **Every move, nothing to turn off.** A move is a few clicks a session; one question each is cheap. | A "don't ask again" tick in Settings. |
| 4 | **Which way does the outline face when you pick a building up?** | **The way the building faces now.** A plain move keeps its turn, and you turn it if you want. | Like a new building: it swings to face the nearest store until you turn it. |
| 5 | **Can you turn a building where it stands** (move it onto its own spot, turned)? | **Yes.** The outline may cover its own old ground, and nothing else. It still costs the builders the full move. | No: a move has to go somewhere else. |
| 6 | **Right-click while holding a building**: let go of it, or put the tool down? | **Let go of it first; a second right-click puts the tool down** (the bridge's rule). | One right-click puts it all down, as today. |
| 7 | **Moving a building that is already being moved** | **Refuse, and say how to call the first move off.** | The second move replaces the first. |
| 8 | **Moving something still under construction** | **Say the true sentence and point at Demolish** (it gives back what was delivered). | Let Move pick up a site and set it down again, with delivered materials walked over. That is a bigger change and would need its own spec. |
| 9 | **A store with goods in it** | **Keep refusing, as now**, with the sentence that says to empty it. | Offer "Empty it, then move it" in one go. That is later work and would be a queued move. |
| 10 | **The hole in §1.1 (a move can land on another building, a doorstep, or in the river)** | **Fix it first.** Moves that "worked" onto bad ground will be refused from now on. | Leave it alone. Not recommended, because the new outline would show green over ground the sim never checked. |
| 11 | **While a building is on the move, does its card say so?** | **Yes: "Being moved — Call off the move", a button on its card.** It is cheaper to find than the outline. | No: Demolish on the outline stays the only way to call it off. |

## 5. Build plan — ✅ built (D947)

1. ✅ **Claim (D945)**: `slice/move-revamp` from `main`. HANDOFF's ⏸️ line *"🔨 being built on `slice/move-revamp`"*, the next free
   D, and this file moved into `specs/move-revamp.md`. `building-placement.md §11.3` is annotated: its *"No moving
   (Joe, 2026-07-28)"* has been void since D229 and still says so.
2. ✅ **Sim, test first** (`RelocateTests.cs`, eight guards). `AMoveLandsOnlyWhereAFreshBuildingCould` sweeps 29×29 tiles
   round the village at two facings and holds a move to a fresh granary's verdict wherever the outline does not touch its
   own ground: **1,652 spots, 160 on a building, 254 in the water, 0 that disagree** (it stands in for the three planned
   *"cannot land on a building / in the river / on a doorstep"* guards, and reaches the wall-off too).
   `AMoveOntoAnotherBuildingIsRefusedAndRaisesNoSite`, `ABuildingMayTurnWhereItStands`, `ABuildingMayStepOntoItsOwnDoorstep`,
   `AMoveArrivesAtTheFacingAskedFor`, `ABuildingAlreadyOnTheMoveIsRefusedByName`, `ASiteIsToldItIsStillBeingBuilt`,
   `AMoveCanBeCalledOffFromTheBuilding`. Built: `CanMoveTo`, `WhyItStaysPut`, `MarkRelocation(from, to, facing)`,
   `MoveUnderWay`, `CallOffTheMove`; `CanBuildAt`'s `itself` (the only shape a move may overlook — in the overlap, the
   doorstep, the wall-off and its doors; the covered-tile loop asks a move too); every building's `MoveTo(to, facing)`.
   **Red-checked, eleven mutants, every one biting** (D326): overlap skipped 2 red; covered tiles skipped 1; doorstep
   skipped 1; wall-off skipped 1; a second move allowed 1; the facing dropped 1; its own footprint not exempt 2; the site
   sentence gone 1; call-off a no-op 1; its own door not exempt 1; its own doorstep not exempt 1. ⚠️ **Two scored ZERO
   first, and that is written down:** the facing test asked for the facing the granary already had (a fresh one faces the
   nearest store), and the own-door exemption had no test that reached it — both fixed, then red. Two mutants did not
   compile at first (an unused parameter; a value compared with itself) and were rewritten until they did.
3. ✅ **View** (`VillageMap.cs`, `Main.MoveQuestion.cs`, `Main.Cards.cs`, `Main.cs`): the picked-up kind held as
   `_building`; the old spot outlined pale; the ghost, R, shift+R and the middle-drag as for a fresh building; the box
   beside the outline (Enter / Esc); right-click and Esc step back one thing at a time (`LetGoFirst`); the card's
   *Being moved — n% of the work done · Call off the move*. Probe line `[widths] move:` (`VillageMap.MoveProbe.cs`): ✅ —
   a posed library (the probe's valley has nothing standing that may move), picked up at its own facing, turned by R,
   refused with no question, asked with the box up and the outline still, *Not here* keeping it in hand, *Move it* marking a
   site at the turn (called off), right-click letting go and then putting the tool down. **Red-checked twice:** the kind not
   held, and right-click putting the tool down at once — each turns the line ⛔.
3. **View**: hold the kind, draw the ghost, wire the turn keys, add the box, the right-click rule, the hint, the card
   button if call 11 is yes. A probe line, `[widths] move:`: picking up a granary holds its kind and facing; R turns it;
   a refused spot opens no box; a green one does; Not here keeps it in hand; Move it marks a site at the turned facing.
4. ✅ **All four verification lines (D947):** suite 2419 / 0 / 5 of 2424 in 20m04s (after one rename in the save table);
   view build 0 warnings; probe 183 ✅, 0 ⛔, bar 110; goldens 463 before and after, none moved. ⏸️ Then Joe plays it with
   `.\run.bat`. Merge and push only at his word.
