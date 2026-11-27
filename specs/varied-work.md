# Varied work, trade by trade — the quarrier first (A280)

> **Status: the quarrier and the miner ✅ BUILT (A282, 2026-11-26), played and merged into `main` at Joe's word (A283, *"Played it, merge"*, `59ec81a`); pushed at his word (A284, *"Push main"*). Joe: *"B, as recommended"*. Mocked A281. The woodcutter ✅ BUILT (A292), played and merged into `main` at Joe's word (A293, *"Played it, merge"*, `60d5df8`); pushed at his word (A294, *"Push main"*). Joe: *"B, as recommended"*. The forester ✅ BUILT (A296) on `art`, UNPLAYED — Joe: *"just power through all these"*, the recommended option taken without a mock. The forager, builder, farmer, hunter and herdsman ✅ BUILT (A297) on `art`, UNREVIEWED. The indoor trades (baker, carpenter, smith, miller, tailor, brewer) are not drawn at work and are not in this piece.** Art lane (CLAUDE.md *Two tracks*), branch
> `art`; the art queue's piece 3, *Animations*, after fishing (`fishing-animation.md`). View only: nothing in `Kith.Sim`, `data/`
> or `tests/` changes. Mock: `D:\Projects\brushes\animations\quarry_options.gif` (and `quarry_contact.jpg`).

## 1. What was asked

Joe's play note (c), 2026-11-20 (A136, HANDOFF's words): **every trade and action with varied work animations — stone gathering
named.** Then, 2026-11-26: *"Start the quarrier"*. One trade at a time, each mocked before it is built.

## 2. What the sim gives the view

A quarrier works a face (`VillagerState.Quarrying`, `BehaviorSystem`): a dig is `quarry_dig_ticks` (4 ticks, about 5 s at 1×) and
ends with `stone_per_dig` (10) put into **their own load**; they dig again while the next fits their arms and the stint
(`digs_per_stint`, 4) has digs left, then carry the lot to a store. The miner runs the same code with iron (6 ticks a dig, 8 digs).
The view reads the villager: their state, their load, and the dig's countdown (`ActionTicksRemaining`).

⚠️ **Found — what the game draws today:** from the second dig on, the quarrier holds stone, and stone is not a shoulder load, so the
painter (`PersonKeyOf`) gives them **the load in the arms and no pick**. Three of a stint's four digs are a person hugging stone and
swaying. The miner the same with iron.

## 3. The options mocked

On Joe's nettle-beck Elderwood quarry, the game's closest zoom (3×, a 1:1 inset in each panel), one stint of four digs, with the
mock painter that matches `PersonPainter` (A129's `anim.py`).

- **Today** — as above.
- **A · the fix only:** the pick in hand all stint, today's raise and strike; the stone they hold stacked at their side, a block for
  each dig (the load the sim says they carry), picked up when they leave with it.
- ⭐ **B · A, and varied:** each dig ends with the cut block lifted and set on the stack (stoop, lift, set down — the moment the sim
  puts the stone in their load); the digging shuffled by the person and the dig between the pick and a **wedge and mallet**
  (stooped, a quicker tap); now and then **a breather** (straightened, a hand to the back) at the start of a dig.
- **C · B, with chips and a puff of dust** at each strike.

## 4. Calls — answered: Joe, 2026-11-26, *"B, as recommended"* (A282)

B; the miner too; the woodcutter next.

1. **Which:** ⭐ B; A, the fix alone; C, B with chips and dust.
2. **The miner too:** ⭐ yes, the same with iron ore (darker blocks), since the sim runs them alike; or the quarrier only.
3. **Then the next trade:** ⭐ the woodcutter (the other one he sees most), unless he names another.

## 5. Wiring, when he answers — every file the art lane's

| File | Change |
|---|---|
| `VillageMap.People.cs` | at a face, the pick stays in hand whatever they hold; the stack drawn at their side from their load |
| `VillageMap.PeopleMotion.cs` / a new `VillageMap.FaceWork.cs` | the dig's beats from its countdown: strikes, the lift at its end; the version (pick or wedge, a breather) by the person and the dig |
| `PersonPainter.cs` | the mallet and wedge pose, the lift (the block in the arms, stooped), the breather |

The probe gains a `face work:` line: a quarrier holding stone keeps the pick, the stack shows their load, the lift falls at the
dig's end, the shuffle is the same on a second ask.

## 6. As built (A282) — the quarrier and the miner

- **The pick stays in hand** (`VillageMap.People.cs`, `PersonKeyOf`): at a face (`AtAFace`: `Quarrying`, `Mining`) the load is
  not painted on the figure, so the hold stays the tool; the map draws the load as the stack.
- **The dig** (`VillageMap.FaceWork.cs`): pure `DigAt` — the lift in the dig's last 1.5 s, a breather in the first 1.6 s of one
  dig in six, the wedge and mallet two digs in five, chosen by the person and the dig's start tick (the same on a reload); the
  strikes keep the people's clock, as every swing does. The dig's length is the face row's (`face_dig_ticks`) as `WorkTicksFor`
  gives it, and how far in is the countdown read back. ⭐ **Quick:** a dig under 2 real seconds (4× and up) is digging only; a
  short dig gives the lift 45 % and the breather 40 % of itself. ⚠️ First built at 3.5 s: the film showed a quarrier with a tool
  and mastery (2-tick digs, 2.7 s at 1×) never lifting a block; fixed before commit, and the probe asks it now.
  `StackOf`: a block for each dig's worth they hold (`face_per_dig`), at most six. The lift: the block in the arms (the stone
  or ore load key), stooped to take it, upright, stooped to set it down, the stack a block more at its end.
- **The painter** (`PersonPainter.cs`): `MalletRaise`, `MalletStrike` (the hammer's own two poses, the pick swapped for the
  mallet) and `Breather` (the pick lowered); the map stoops the wedge's body and leans the breather back.
- **The map** draws the stack at their left and, in a wedge dig, the wedge in the stone at their right, through `AtNight`;
  the miner's blocks are the ore's red-brown.
- **Probe `face work:`** (`VillageMap.FaceWorkProbe.cs`, printed with the people's lines): red-checked, 10 mutants, 9 red (the load back in the arms; the stack rounding down; no lift, before and after the threshold's fix; quick digs off; never the wedge; the shuffle blind to the dig; the lift not stooping; the first build's 3.5 s threshold, a skilled quarrier never lifting). The 10th, the mallet not swapped in for the pick, scored 0: the line compares the mallet's pose with the raised pick, not the tool painted — kept, and the zero written down.
- Filmed on a scratch copy of his nettle-beck Elderwood with the stone limit raised (the quarry had stopped at it):
  `D:\Projects\brushes\animations\q2\`, `quarry_built.gif`.

## 7. The woodcutter — mocked (A291); ✅ Joe: *"B, as recommended"* — B, at the block, the forester next (A292)

**What the sim gives:** the woodcutter splits logs at the hut (`MakingFirewood`): a split is `split_ticks` (4, about 5 s at 1×),
`logs_per_split` (6) out of the warehouse beside the hut and `firewood_per_split` (12) back into it, `splits_per_stint` (4);
nothing is carried. ⚠️ **Where they stand today: on the hut's own spot — its roof** (the sim's place; `Travel` to the hut's
position). The yard already paints a chopping block, a log on a sawhorse, the log pile while it works and chips (A86).

Mocked on his spruce-mill Ashbourne at the closest zoom (`D:\Projects\brushes\woodcutter\woodcutter_options.gif`), one stint
of four splits:

- **Today** — on the roof, the axe's raise and strike.
- **A** — at the chopping block, the same swing.
- ⭐ **B** — at the block, each split: the round set on the block (a stoop), the strokes, the split — the halves fall either
  side and lie until the next round; shuffled by the person and the split: a clean one-stroke split, a knotty round taking two
  or three, or a wedge and maul (the quarrier's mallet beat); a breather now and then — the face's rules, one trade over.
- **C** — B, with chips flying at each stroke.

**Calls — Joe's:** (1) ⭐ B; A; C. (2) ⭐ standing at the block while splitting (view only; the sim keeps them at the hut, as
the fisher's dock). (3) Next: ⭐ **the forester** — found while reading: the forester fells trees on its ground (`Cutting`) with
the painter's spade, so today a tree is felled with a spade; or the builder.

**Wiring, when he answers — every file the art lane's:** `VillageMap.Yards.cs` (keep the block's place when the yard is laid,
as a door's step is kept), `VillageMap.Indoors.cs` (`AtTheDoor`: a splitting woodcutter at the block), `VillageMap.FaceWork.cs`
(the split's beats from its countdown, beside the dig's), `PersonPainter.cs` (the maul is the mallet's pose), `VillageMap.People.cs`
(the round, the halves, the wedge). The probe's `face work:` line widened to the woodcutter.

### 7a. As built (A292) — the woodcutter

- **At the block** (`VillageMap.Indoors.cs`, `AtTheDoor`): a splitting woodcutter (`MakingFirewood`) stands `FromTheBlock` (0.21,
  0.17 tiles on screen) from the yard's chopping block. The block's place is kept as the yard is laid (`KeepTheBlock`, beside
  `KeepTheStep`, by the roof's key — the hut's position rounded as roofs round it) and forgotten with its roof
  (`ForgetGoneRoofs`); a yard not laid yet keeps them where the sim has them.
- **The split** (`VillageMap.Splitting.cs`): pure `SplitAt` from the sim's countdown and length (`split_ticks` through
  `WorkTicksFor`) — a breather opening one split in six (at most 1.6 s or 30 %), the round set (at most 0.9 s or 20 %), the
  strokes fitted to what is left (one two in five, two a quarter, three a fifth, the wedge and maul's two a fifth), then the
  split and the halves apart for the rest; by the person and the split's start tick; under 2 s strokes only.
- **The painter:** the axe takes the pick's breather pose; the maul is the mallet's pose, swapped in for the axe as for the pick.
- **The map:** the round on the block, the wedge in it for the maul, the two halves falling either side (`DrawTheRound`).
- **Probe `woodcutter:`** (`VillageMap.SplittingProbe.cs`): red-checked, 7 mutants, 7 red (the woodcutter not stood at the block; the block not kept as the yard is laid — 0 at first, the probe asking KeepTheBlock directly instead of the game's KeepTheStep, fixed and red; no round set; every split one stroke; quick splits off; no breather pose for the axe; the maul not swapped in).
- Filmed on his spruce-mill Ashbourne (`D:\Projects\brushes\woodcutter\woodcutter_built.gif`): the woodcutter walks up, works
  at the block, and walks off as the stint ends. ⚠️ Seen in passing, not this piece's: as a stint ends they are drawn a moment
  on the hut's own spot (the next state's place, as it always was) before walking away.

## 8. The forester — built (A296), no mock: Joe's *"just power through all these, im going to approve them all"*

**What the sim gives:** one state, `Cutting`, at a tile of the hut's work ground — felling while the tile is forest, planting
when it is not (`IsPlantingErrand`); a fell is `cut_ticks` (4), a planting `VillageEconomy.PlantTicks` (three times that), both
through `WorkTicksFor`. The tree's fall and the young sapling are already drawn when the tile changes (A196–A198). ⚠️ **Until now
both were done with the painter's spade, the forester standing in the tree's crown.**

**Built (the recommended option):**

- **Felling** — **the axe** (`FellRaise`, `FellStrike`, `FellRest`: the axe swapped in for the spade, the axe's own poses),
  standing `BeforeTheTrunk` (0.06, 0.34 tiles on screen) from the tree's middle (`AtTheDoor`: a `Cutting` villager on a forest
  tile); strokes on the people's clock, a breather opening one fell in six.
- **Planting** — **the spade**: the dig for the first 60 %, stooped to set the sapling for the next 25 %, treading it in for the
  rest. Under 2 real seconds, strokes only.
- `VillageMap.Forestry.cs` (`ForestAt`, `ForestryOf`, `ForestKeyAndMotion`); the probe's `forester:` line
  (`VillageMap.ForestryProbe.cs`), red-checked 6 mutants, 6 red (not before the trunk; no axe; no breather; no stoop to set;
  quick off; the stone load back in the arms).
- ⚠️ **Found filming, and fixed:** his Willowdale's forester Verren planted with 24 stone in the load (a seam on the ground,
  D211) and stood hugging it, no spade — the quarrier's bug one trade over. While `Cutting` the load is not drawn in the arms
  (`PersonKeyOf`, beside the face's rule); it is drawn again the moment they haul it.
- Filmed: his willowdale finch-grove (year 6), planting — the spade's dig, the saplings coming up, the stone carried off (`D:\Projects\brushes\forester\forester_built.gif`); his nettle-beck Elderwood caught only a felling's end (the fall, A198), the axe's strokes not seen in the game.

## 9. The other outdoor trades — built (A297), no mock: Joe's *"just power through all these"*

The forager (`Gathering`), the builder (`Building`), the farmer (`Sowing`, `Reaping`, `Tending`), the hunter (`Hunting`) and the
herdsman (`CuttingHay`, `FeedingTheHerd`) each had one two-beat motion on the people's clock (A130). **Built (the recommended
option), `VillageMap.Rhythm.cs`:** each works in **phrases** on the people's clock — a breather opening about one phrase in six,
a run of strokes, and a motion of its own:

| Trade at | Strokes | Its own motion |
|---|---|---|
| the forager, gathering | stoop and reach ×3 | every phrase: straightened, the pickings dropped in the basket |
| the builder, building | the hammer ×5 | two phrases in five: stooped for a plank and up with it |
| the farmer, sowing | **the seed basket** in the fork's place, reached into and **tossed** — the seed drawn in the air | — |
| the farmer, reaping or tending | the fork ×4 | — |
| the hunter, hunting | the bow raised and drawn ×2 | every phrase: drawn, **loosed** (an arrow drawn away a moment), lowered — no animal: the sim has none (`hunting.md §9`) |
| the herdsman, at hay or feeding | **a fork** in the crook's place, lifted and pitched ×4 | — |

Each person's phrases start at their own offset, so two never keep step. Culling and the herdsman's other work keep A130's lean.
The painter: `SowReach`, `SowToss` (the basket swapped in for the fork), `HayLift`, `HayPitch` (the fork for the crook).
**Probe `outdoor work:`** (`VillageMap.RhythmProbe.cs`): red-checked, 5 mutants, 5 red (no breathers; no motions of their own;
everyone in step; the sower's fork; a phrase leaking to the woodcutter). ⚠️ Not filmed: no save catches these trades mid-task;
his eyes on it at review.
