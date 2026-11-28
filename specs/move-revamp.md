# Move, revamped — ask first, show the outline, let it turn

> **Status: 📝 SPECCED, NOTHING BUILT — 🔨 claimed on `slice/move-revamp` (D945, 2026-11-27), cut from `main` at
> `a92da93`.** Drafted while `slice/w6-health` was open (D710: one systems branch at a time), and moved here in the claim
> commit once W6 merged (D944). ⏸️ **Eleven calls are Joe's (§4); no code is written until he answers.**

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
- **The hint line** for Move gains the turning keys: *"Click a building to move it. R turns it, as a new building does.
  …"*

## 3. What is NOT changing

- What a move costs and does (D229): the full work, no materials, the same building arriving.
- Houses still move by the land brush (D228). Graves and fences still hold a building down.
- No save change: a site already stores its facing and `MovingFrom`. No new data rows. **No golden is expected to
  move**, because no golden is known to run a relocation. *Not yet checked: the goldens grep and a `git diff` decide it,
  not this sentence.*

## 4. ⏸️ Joe's calls, in plain words

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

## 5. Build plan, once the calls are in (each its own commit)

1. ✅ **Claim (D945)**: `slice/move-revamp` from `main`. HANDOFF's ⏸️ line *"🔨 being built on `slice/move-revamp`"*, the next free
   D, and this file moved into `specs/move-revamp.md`. `building-placement.md §11.3` is annotated: its *"No moving
   (Joe, 2026-07-28)"* has been void since D229 and still says so.
2. **Sim, test first** (`RelocateTests.cs`): `AMoveCannotLandOnAnotherBuilding`, `AMoveCannotPutALonghousesFarEndInTheRiver`,
   `AMoveCannotStandOnADoorstep`, `ABuildingMayTurnWhereItStands`, `ABuildingAlreadyOnTheMoveIsRefusedByName`,
   `ASiteIsToldItIsStillBeingBuilt`, `AMoveArrivesAtTheFacingAskedFor`, `CanMoveToAgreesWithMarkRelocation`. Each one
   red-checked, with the reds counted (D326). Then `CanMoveTo`, the self-only exemption, and the `facing` parameter.
3. **View**: hold the kind, draw the ghost, wire the turn keys, add the box, the right-click rule, the hint, the card
   button if call 11 is yes. A probe line, `[widths] move:`: picking up a granary holds its kind and facing; R turns it;
   a refused spot opens no box; a green one does; Not here keeps it in hand; Move it marks a site at the turned facing.
4. **All four verification lines.** The suite runs, since `src/Kith.Sim` changes. Then the goldens grep, the view build
   read line by line, and the probe with `bar height` at 110. Then Joe plays it with `.\run.bat`. Merge and push only at
   his word.
