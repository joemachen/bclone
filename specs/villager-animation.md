# The villagers move: a walk, the work, a word, the cold (A129)

> **Status: ✍️ specced and mocked (A129, 2026-11-20); ✅ B picked, every call as recommended (A130, *"B, all as
> recommended"*); 🔨 BUILT on `art` (A130, §8), not merged — ⏸️ his eyes, then his word.** Joe, 2026-11-20: *"Can we
> work on villager animations next?"* The painted people (A93, mock E, `villager-art.md §8`) stand in one painted pose
> and glide from tick to tick; `villager-art.md §5` left *"walk cycles or swing animations"* out of that piece. This is
> that piece.
> Owner: Joe + Claude Code · art lane (`art`, A-series) · View only: nothing in `Bclone.Sim`, `data/` or `tests/` changes,
> and no golden can move. Non-negotiables §1.1 (legibility) and §1.2 (meditative pace) are the bars.
>
> Neighbours: `villager-art.md` (the people and `PersonPainter`), `trades-visibly-work.md` (*"you read work from where a
> person stands"*, D384), `gridless.md §6` (the steady pace: a walker covers 1, 1.11 or 1.25 tiles a tick), D525 (the
> crowd fan), D531 (the meal bowl's clock), D573 (the walk-in's clock: *"a paused village is a still picture"*), A46 (the
> river keeps the game's speed). Mocks: `D:\Projects\brushes\villagers\anim\` (§4).

---

## 1. What moves, and what each motion says

Every motion has to **say what the person is doing** (§1.1), slowly enough to watch without strain (§1.2). Nothing
moves for decoration alone; the one quick motion in the game is the shiver of someone cold, because it is a warning.

| Who | Motion | What it says |
|---|---|---|
| Anyone walking | **a step**: the body rises and settles once a step; with poses, the feet alternate and the free arm swings | going somewhere; the pace is the ground's |
| Carrying (logs, a sack, a basket, fish…) | the same step, **the bob a half deeper** | it is heavy |
| Pushing a cart (`handcart.md`) | the step, deeper; the cart is painted with them and rides with them | a cart is being pushed |
| An elder | a shorter step (0.85 of an adult's), the stick | old |
| A child | a child's step (0.62 of an adult's), so quicker feet for the same ground | small |
| The woodcutter, quarrier, miner (axe, pick) | **raise, hold, strike** — every 1.6 s | chopping, digging stone |
| The builder, smith (hammer) | a shorter **raise and strike** — every 1.2 s | building, forging |
| The farmer (fork) | **pitch and lift** — every 2.4 s | the field's work |
| The forester (spade) | **push down, lift** — every 2.4 s | planting, digging |
| The fisher (rod) | the rod's tip **dips and lifts**, slowly — every 4.8 s | waiting on the line |
| The forager, marketer (basket) | **stoop and reach**, the basket lowered — every 2.4 s | picking |
| The carpenter (saw) | **push and pull** — every 0.96 s | sawing |
| The baker (peel) | **into the oven and out** — every 2.4 s | baking |
| The hunter (bow) | **raise the bow, draw** — every 2.4 s | hunting |
| The herdsman, miller, tailor, merchant, labourer at work (no tool drawn) | **lean into it and back** — every 3.2 s | at work |
| Standing idle, talking (the raised palm, E) | the palm **raised and lowered**, every 2.4 s; a slow sway (4.8 s) | talking |
| Standing still otherwise | the slow sway only | waiting |
| Cold, going in (`SeekingShelter`) | **a small shiver**, side to side, 2.5 times a second, on top of the walk | cold — why they stopped work |

People indoors stay undrawn (A90 call 3), so the village's night is still an empty map.

## 2. How it is drawn

A person is a painted picture (`PersonPainter`, 4 ms a painting warm, kept by everything the picture depends on —
`FigureKey`). There are two ways to make a picture move, and the three looks (§4) are how much of each:

1. **Moving the picture as it is drawn** — lift it (the step's bob), turn it a degree or two about the feet (a sway, a
   lean), squash it a little toward the feet (a stoop), shake it (the cold). One `DrawSetTransform` before the
   picture's `DrawTextureRect`; **no new painting at all**. The shadow is not moved — it stays on the ground, so the
   bob reads against it.
2. **Painting more than one pose** — the other foot forward, the tool raised or struck, the palm up or down. Each pose
   is one more painting of the same person, kept like the first. `FigureKey` gains a **`Beat`** (0–3): which pose.

**Sizes.** An adult is 0.72 of a tile tall: 34.5 px at the closest zoom, 22 at the middle, 14 at the far (never under
11). The poses are worth their paintings down to the middle zoom (a raised axe is a third of a person's height); **at
the far zoom, under 16 px tall, only the first pose is painted and the picture is moved (way 1) — the extra poses would
differ by a pixel.** That is also where most people are on screen at once.

## 3. What drives it — the clocks

**View only.** Everything below reads the sim's state as `DrawThePeople` already does (`State`, the leg, the load, the
coat, the cart) and the view's own glide; nothing writes to the sim, nothing is hashed, no golden can move. The view
already keeps real-time clocks (the meal bowl's, the walk-in's, the river's); this adds one.

- **A step is paid for by distance.** Each villager keeps how far their drawn point has moved (a `float` beside their
  glide, in tiles). The step's phase is that distance over the step length (0.36 tile for an adult): so at 1× the feet
  keep to the ground (0.75 tiles a second, 2.1 steps a second), **a paused village stands mid-step**, and a person who
  stops stops stepping on the spot. A snap (more than √2 tiles in a tick — a birth, a move of house) is not walked.
- **⭐ The cadence is capped at 2.4 steps a second, whatever the game's speed** (§1.2). At 4× a villager covers 3
  tiles a second; paid by distance that is 8 steps a second, a scurry. Above the cap the stride lengthens instead, and
  the feet slide a little — at 4× nobody is watching a foot, and a calm village at speed is the point.
- **The work, the talk and the sway run on the view's clock**: real seconds × the game's speed (the river's pace, A46,
  `FlowPace`, already set by `Main` every frame), **capped at 1.5×**, and **stopped while paused** (D573's rule). A crew
  of woodcutters at 10× swings a little quicker, never frantically.
- **Nobody moves in step with anybody.** Every beat starts at a phase drawn from the villager's id (`Hash01`, as their
  face is), so two woodcutters at one stand chop out of time, and a household walking home together does not march.
  (D28 measured the sim's lockstep at 99.9 %; the view must not paint it back in.)
- **The pose follows the beat; the picture does not wait.** If a person's next pose is not painted yet (over the
  frame's budget, §5), they hold the pose they have — the motion degrades to way 1 for a frame, never to a dot.

## 4. The three looks mocked

On Joe's Hollowmere (Year 63, summer, closest zoom — A90's render of it, `D:\Projects\brushes\villagers\shots\summer`),
and as a catalogue of every motion. The mocks paint with `illum.py`, the script `PersonPainter.cs` was ported from line
for line (A93), so a pose in the mock is the pose the game would paint. Walkers walk their own heading at 1×; every
loop is 4.8 s.

| | Look | Paintings a person needs | What it gives |
|---|---|---|---|
| **A** | **Breathing** — the picture moved only (§2, way 1): a bob each step, a slight side-to-side tilt (1.3°) for the feet nobody sees move, a lean into the work and back, the sway, the shiver | **1**, as today | everyone alive, nothing new to paint; but the work is a rocking body — an axe never rises |
| **B** | **Two poses** — A's bob and sway, plus a second painting for each motion: a stride and a passing step (the other foot, front and back), **the tool up and the tool struck** (held up 62 % of the beat, then the stroke), the palm raised and lowered | **2** while walking, working or talking; 1 standing | the work says itself — an axe comes down, a fork lifts — and the walk alternates feet. The manuscripts' own way: figures are poses, not in-betweens |
| **C** | **Full cycle** — four paintings a motion: contact, passing, contact, passing with the arm swinging; rest, raise, strike, follow-through | **4** | the smoothest walk; at the closest zoom the difference from B is a frame of a foot; at the middle zoom none |

Files (outside git): `anim_scene_summer.gif` (today, A, B, C side by side, the village), `anim_scene_summer_2x.gif` (the
river bank enlarged: two fishers at their rods, walkers on the bridge and the track), `anim_motions_large.gif` (every
motion of §1, each look, drawn large, walkers on the spot), `anim_motions_closest.gif` and `anim_motions_far.gif` (the
same at the game's sizes). Tool: `anim.py` (patches `illum.py`'s legs, arms and tools by pose; moves the picture as
§2 says).

## 5. The cost, at Joe's village sizes

The rule (CLAUDE.md): **nothing derivable is rebuilt per frame.** Every pose is painted once and kept by its key, as
A93's people are; a frame hands each person a transform and a texture. What a frame does per person is what it does
today, plus one transform and a beat chosen from a float.

- **Kept state, maintained where it changes:** one `float` per villager (distance walked, advanced in the glide) and
  the beat clock (one `double`, advanced in `_Process`). Dropped with the villager, as `_lastPersonKey` is.
- **Paintings.** At most 4 a frame (A93's budget), ~4 ms each. Today a person costs one painting whenever their look
  changes (a load picked up, a turn, a coat). **B makes that two for a walker or worker; C four.** Opening a village of
  60 people out of doors fills in A93's first second; B's second poses arrive over the next second (they hold their first
  pose meanwhile); C's take four.
- **Memory**: ~58 KB a kept painting with mipmaps (A93). 60 people out of doors: today ~3.5 MB; **B ~6 MB; C ~12 MB**;
  the 600-frame letting-go bounds it as it does now.
- **At the far zoom** every look costs what A costs (§2), which is where a big village is mostly seen.
- **Measured at the build, not assumed:** a probe line counting paintings a second on Hollowmere at 1× and 4× before and
  after (the churn of looks is what multiplies), and `_Draw` timed against today on his summer and winter saves, A93's
  way. Bar: no rise a player can see; if B's churn at 4× holds people on their first pose more than a frame in ten,
  the budget is revisited before merging, not after.

## 6. ✅ Joe's calls — ALL AS RECOMMENDED (A130, 2026-11-20: *"B, all as recommended"*)

His answer also covers the two `VillageMap.cs` places §7 named (the message it answered asked for them in the same breath).
In the end the build needed only one of them — the line in `_Process`; the distance is taken in `VillageMap.People.cs`.

1. **Which look: A, B or C?** *(see `anim_scene_summer_2x.gif`, then `anim_motions_large.gif`)*
   ⭐ **Recommended: B, two poses.** It is the only one where the work is readable — an axe goes up and comes down, a
   fork pitches, a fisher's rod dips — which is what a moving villager is for (legibility). A makes everyone breathe
   but a woodcutter just rocks. C is a little smoother up close and costs twice B's painting and memory for a
   difference you see only at the closest zoom. Two held poses is also how the manuscripts he chose draw people.
2. **A calm pace at any speed?** Feet match the ground at 1×; above that the steps stay at about two a second and the
   stride lengthens, and the work speeds up to at most one and a half times. ⭐ **Recommended: yes** — at 4× and 10× the
   village moves quickly across the map but nobody scurries.
3. **Paused, everyone stands still mid-step?** ⭐ **Recommended: yes**, as the walk-in does now — a paused village is a
   still picture. *(The river runs on while paused today; people would not.)*
4. **The cold shiver** — the one quick motion: a small side-to-side shake on someone walking in to get warm.
   ⭐ **Recommended: keep it** — it is a warning, and the blue marks over their head already say the same thing; the
   shake makes it seen from across the map.

## 7. The build, once he picks (one piece, on `art`)

Files — ⚠️ **`VillageMap*.cs` sit in the systems lane's column of HANDOFF's table; asked before touching** (A126's
shape):

- `src/Bclone.Game/PersonPainter.cs` (the art lane's, A93): `FigureKey` gains `Beat`; `Legs`, `Arms`, `ToolFront` and
  `ToolBehind` take it (the poses in §1, from `anim.py`).
- `src/Bclone.Game/VillageMap.People.cs` (the art lane's, A93): `PersonKeyOf` adds the beat; `DrawThePeople` sets the
  transform before `DrawTextureRect` and falls back to a held pose; the per-villager distance.
- `src/Bclone.Game/VillageMap.cs`: **one line in `_Process`** (the people's clock, beside the meal bowl's) and **the
  distance added where `DrawnTile` glides** — the only two places in that file.
- A new `src/Bclone.Game/VillageMap.PeopleMotion.cs`: the clocks, the beat, the motion table of §1.
- `src/Bclone.Game/VillageMap.PeopleProbe.cs`: probe lines — the beat from distance (paused: no step; a snap: no step;
  4×: the cap); two crew members out of step; a pose not yet painted held, not dotted; the far zoom paints one pose;
  tick off, the dots to the byte; red-checked and the reds counted.
- No `Main.cs` change: the game's speed already reaches the map (`SetFlowPace`, A46).

Then the four checks, renders of the build on his saves, and his eyes (`.\run.bat`).

## 8. ✅ As built (A130) — B, at Joe's word: *"B, all as recommended"*

- **`PersonPainter.cs`** — `FigureKey` gains `Beat` (default 0, A93's picture). Walking: beat 1 is the other step — from
  the side the passing step (the far leg under, the near knee lifted) with the free arm hanging; toward or away the right
  foot lifted instead of the left. Talking: beat 1 the palm lowered to the chest. At work facing us with a tool that
  swings: beat 1 the tool raised, beat 2 the stroke — the hand moved and the tool turned about it (`WorkPose`, `anim.py`'s
  frames 1 and 2 to the number; the canvas turns points about the hand while the tool is drawn). `SwingSeconds` and
  `StoopOf` give each tool its beat and its bend (§1). ⭐ **The shadow is its own painting now** (`PaintShadow`, an adult's,
  on an adult's canvas): until A130 it was baked into every person's picture, so a step's rise or a lean would have lifted
  it off the ground. A person's painting no longer computes it, which also makes each painting a little cheaper.
- **`VillageMap.PeopleMotion.cs`** (new) — the people's clock (`TickThePeopleClock`: real seconds × the game's speed,
  capped at 1.5×, nothing while paused); each person's walked distance (`_walked`, advanced where the drawn point moves,
  capped at 2.4 steps' worth of the frame's real seconds, a jump not walked, dropped with the villager); `MotionOf` — pure:
  the beat, the rise, the turn, the squash and the cold's shake, phased by the villager's id; the shadow kept.
- **`VillageMap.People.cs`** — each frame a person's key takes the motion's beat (a pose not yet painted holds the one
  they had, as A93's budget already did); the shadow drawn at their feet unmoved, then the person under
  `DrawSetTransform` (feet + rise, turn, squash) and the transform reset. Under 16 px tall, beat 0 only.
- **`VillageMap.cs`** — one line in `_Process` (the people's clock beside the meal bowl's). The distance turned out to be
  best taken in `DrawThePeople` (`DrawnTile` and the walk-in's place, read there), so `DrawnTile` is untouched.
- **Probe line `people moving:`** (`VillageMap.PeopleProbe.cs`, printed with `painted villagers:` so `Main.cs`'s probe
  list is untouched): a 1× frame walks the ground's distance; paused and a jump walk nothing; a second at 4× is 2.40
  steps; the clock 0.50 / 0.75 / 0.00 s for half a second at 1×, 10× and paused; two steps alternating toward us and from
  the side; the rise 0.022, 0.032 under a load; a woodcutter's stroke 38 % of the beat, a crew out of step; far out one
  pose, the walk tilting and the swing leaning; a tailor leaning; only the cold shaking; five poses each a different
  picture, and the legs themselves moved; the shadow its own painting and out of a person's.
  **Red-checked: 14 mutants, 14 red** — ⚠️ two scored **zero** first (the side step's legs unmoved; the toward step's
  right foot unmoved): *"a different picture"* was satisfied by the arm or the other foot. Fixed by counting the feet's own
  pixels (`FeetPixelsDiffering`), then both red alone.
- **Renders of the build** on his Hollowmere (Y63 summer), in Godot's movie mode at a fixed 20 frames a second so a GIF
  is real time: `D:\Projects\brushes\villagers\anim\built_1x_close.gif` (1×, enlarged 2×), `built_1x.gif` (the whole
  view), `built_4x_close.gif` (4×). ⚠️ Not seen in the renders: a worker swinging (the summer save's few workers are off
  that view) — the motions are the probe's and the mock catalogue's; Joe's eyes are the check.

### 8a. ✅ After play (A131) — Joe: *"sometimes some villager walking animations dont work. their legs look stationary"*

*"I havent noticed a pattern. it happened right at the beginning of the game, after pausing, and then intermittently to
seemingly random villagers."* **Measured, not guessed** (each trade's two steps painted, shrunk to the game's sizes and
composited on grass; screen pixels that visibly change between them): **from the side 50–107 at the closest zoom;
walking up or down the screen only 19, 6 in an ankle-length gown (the merchant, tailor, teacher) and 4 for an elder.**
The pattern was the heading: anyone walking more up or down the screen than across it barely moved their legs, and which
way a person walks looks random. **Fixed:** toward or away the lifted foot rises 0.05 (was 0.03), a long gown's shoes
alternate too, the hem swings toward the stepping leg (±0.03), the hand opposite the stepping foot comes up (0.045), and
the body tilts foot to foot (1.3° a step, as far out). **Now 77–162 at the closest zoom** (long gowns 96–102, elders
70–81) before the tilt. ⚠️ Beat 0 of a walker toward or away is no longer A93's picture.
**And "after pausing":** a pose not drawn for 600 frames is let go, so in a long pause (one pose shown for minutes) every
walker's other step went and the village repainted them when it ran again, holding the old pose meanwhile. **Fixed:** a
person's other poses are kept while any one of them is drawn (`KeepTheOtherPoses`). *(The beginning of a game still
paints everyone in their first second or two, A93's budget of four a frame; a person holds the pose they have until then.)*
**Guarded** (`people moving:`): a tilt toward us and none from the side; a long gown's foot moved; the other pose kept
through 700 unseen frames; ⭐ **the step toward us SEEN at the closest zoom** — shrunk, laid on grass and counted, as the
bench did (`VisibleChange`): 84 px for a labourer, 85 in a merchant's gown, 70 for an elder; the bar is 40, as from the side.
**Red-check: 7 mutants, 5 red** — no tilt, the other pose let go, no hem swing, and the whole fix reverted (17, 7 and 4 px:
A131's measurement of the bug, reproduced) went red; ⚠️ **two zeros kept and written down:** a long gown's right shoe left
still, and the old low step — each alone leaves the step visible through the other parts, which is what the guard asks.
Before the visibility guard, the first three painter mutants had all scored zero against the feet-pixel check, which is
HANDOFF art trap 60 in one run.
