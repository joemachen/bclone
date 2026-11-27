# Varied work, trade by trade — the quarrier first (A280)

> **Status: ✍️ MOCKED (A281, 2026-11-26) on `art`, ⏸️ JOE'S PICK — nothing built.** Art lane (CLAUDE.md *Two tracks*), branch
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

## 4. Calls — Joe's

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
