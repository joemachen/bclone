# Spec: Hunting — the food that works in winter, and the first hide

**Decisions:** D19, D253, D262, D277, D286, D288 — and **D3057(b)**, which chose hunting over
livestock and is the argument for everything below.
**Phase:** food breadth, immediately after fishing (D253: *"after town hall is fishing and hunting"*).
**Status:** ✅ **ALL THREE SLICES SHIPPED** (slice 2 was built and then deleted on Joe's call — D297). ✅ **And the
stalled lodge is fixed (D683, 2026-11-04; merged into `main` D685, unplayed): under a met meat
limit a swollen lodge keeps every one of its seats and the hunters carry it in, all at once** — §6a.
The lodge is placeable, staffable and worked; `Meat` and `Leather` are real; the buffer holds three hunts and a marketer runs it dry. **Available game is a function of standing woodland and nothing else** (§4). **The animals are drawn** (§9) — derived from forest tiles, no sim state, so felling a wood visibly empties it. `meat_yield` is **600**.
**Owner:** Joe + Claude Code

---

## 1. Goal

**Joe, 2026-09-02:** *"Hunting ultimately yields more food, but it takes longer and isn't
instantaneous. 3 hunters per hunting lodge. Different types of game meat. And leather."*
And, separately: *"I want the user to see animals/game roaming the forest… just models of the
game existing in the forest, moving around. I want a visual representation of them on the map."*

So the totem pole Joe has stated, bottom to top: **foraging → fishing → hunting.**

> ⭐ **Revised by Joe (D474, 2026-10-03):** over six valleys a fisher out-earns a hunter on food per tick
> worked (886 against 1,299 on `main`; a hunter walks to game, a fisher casts beside the hut). Joe: *"i think
> its fine because hunting also produces leather whereas fishing only produces food."* **On food alone the
> ladder is foraging → hunting → fishing; the lodge's leather is what puts hunting level.** Guarded:
> `AHunterOutEarnsAForagerPerTickWorked` (700 against 401 over six valleys) and
> `FishingTests.AFisherOutEarnsAForagerPerTickWorked`.

---

## 2. Why this is not content

D3057 chose hunting over livestock and listed exactly what it buys, and every line still holds:

- **Year-round outdoor work for the 86%-idle winter.** ⭐ *This is the point.* Foraging stops in
  winter (D44). Fishing does not, and now hunting does not either — but hunting is the one that
  makes winter a season with a **job** rather than a season with a **store**.
- **Hides for clothing.** ~~`specs/clothing.md` is blocked twice over and one of the blocks is
  "there is no leather"~~ — the leather block fell with this slice (found D533), and the tailor is in
  build (D659). The chain is **hunting → leather → tailor → clothing** (D2753's build
  order says *hunter → tailor* in those words).
- **An additive food source**, which D19 makes a prerequisite rather than a luxury: a binding
  walk-distance kills outlying households when there is only one raw food source.
- **Depletion as a §2.3 pressure**, reusing the forest-exhaustion machinery rather than inventing
  a second one.

---

## 3. ⛔ THE TRAP, NAMED BEFORE IT IS STEPPED IN

**A hunting lodge must NOT have a `GatheringRadius`.**

`SharersOf` asks `GatheringRadius > 0` and deliberately **never asks `JobKind`**, so that *"a
modder's building is in the rule the day it exists"* (D260). That is the right rule for a
gathering ring — and it means **any building given a ring immediately starts competing for
TREES.** The fishing spec already carries this warning in its own guard:

> a fishing hut given a ring would silently start competing with FORAGER huts over TREES.

Game is not wood. A lodge with a `GatheringRadius` would halve a forager's yield by standing
near them, and nothing would say so.

⭐ **Therefore: `BuildingRow.HuntingRadius`, a second and separate reach.** Two lodges whose
ranges overlap share their game by the same D260 shared-count rule; a lodge and a forager's hut
do not interact at all.

⚠️ **The honest cost of this decision, recorded now:** the game will then have *two* radius
fields that mean "how far this building reaches", and a third would be a smell. If a fourth
harvesting building ever appears, the right move is to make a ring carry **the resource it
competes for** and collapse both fields into it. *That refactor is cheap today and expensive
later; it is not taken now only because two is not yet a pattern.*

---

## 4. Where game comes from

**Game lives in the forest, and the standing wood is the whole of it** (Joe, 2026-09-05):

> *"Foraging food should only deplete if the woodland around the hut is felled. The volume of
> mature trees in the vicinity of the forager's hut should dictate the volume of gatherable food,
> right? Similar for hunting and animals. More trees = max available animals, less trees = less
> available animals."*

- `HuntYieldAt` is `meat_yield × ForestTilesWithin / TilesInRing` — the forager's shape one
  building over. **Where the player sites a lodge is the decision**, and a lodge in thin scrub is
  honestly worth less.
- **Felling is the only thing that reduces it; regrowth is the only thing that restores it.** Fell
  a wood and the trip AND the hunt both fall; let it come back and both rise. One rule for two
  trades, and the tension it creates is **wood against food** — which the player already feels
  every time they cut for a building.
- ⛔ **It counts trees WHOLE where the forager divides them** (D260's shared-count rule). Nobody
  fells anything here, and a forager picking berries under the same trees takes nothing a hunter
  wanted, so there is nothing to share.

⛔⛔ **THERE IS NO SEPARATE DEPLETION, AND THIS SECTION USED TO SAY THERE SHOULD BE.** It called for
depletion *"stored sparsely"* recovering in `RegrowthSystem`, *"reusing the forest-exhaustion
machinery"*. **That was built (D295) and then deleted (D297) on Joe's call**, and two things were
wrong with it: it was a second clock the player could not see, and the machinery it named is
`ThinTheRingOf`, which turns forest into sapling — **it fells the tree**, so hunting through it
would have made a hunter a logger and undone §3 in one line. *The spec was written before the
machinery was read.* `FellingTheWoodTakesTheFoodAndTheGameWithIt` is the guard that now carries
this section.

## 5. The goods

| Good | Nutrition | Notes |
|---|---|---|
| `Meat` | **1** | ⛔ **Must equal every other edible.** D277's validator refuses a config whose edible goods disagree, because the survival floor is derived from a single figure. |
| `Leather` | 0 | Inedible. Its only consumer is the tailor (`clothing.md`, D659–D664). **It leaves the lodge on the carrier the *worth a walk* test named** (D664, `clothing.md §3.5a`): an armful, or a coat's worth when a standing shop is short of it. **Since D420 it goes into the lodge with the meat** (meat first) and leaves with whoever clears the buffer; until then it rode in the hunter's arms from hunt to hunt, where no stock limit could see it (`stock-limits-and-laborers.md §4.5`). |

⚠️ **"Different types of game meat" is deferred to a data-only follow-up, deliberately.** Every
meat would be a `GoodRow` with nutrition 1, which is cheap — but each is also a column in every
store panel and a branch in every "what counts as food" loop that D283 has just finished
teaching. **One `Meat` first, varieties once the loop is proven.** *This is the D50 lesson: ship
the mechanism, then the content.*

---

## 6. The work

- **Hunter's lodge, 3 seats** (Joe). Placement: must have forest within its reach.
- **A hunt takes longer than a cast** — `hunt_ticks` above `fish_ticks` (10). It is *"not
  instantaneous"* by design.
- ✅ **And it happens in the woods (D384, `trades-visibly-work.md §4`):** the hunter walks to a
  forest tile within `hunt_walk_tiles` (6) of the lodge, hunts there, and carries the catch back
  to the lodge; until D384 they hunted standing on the lodge and re-armed in place. The rig's
  on-the-job ticks count the walk and the carry-back; `meat_yield` is still the rig's alone.
- ⭐⭐ **A HUNT ANSWERS THE VILLAGE, NEVER A LARDER (D398, Joe's call (a) on D397's audit; ✅ and since D399 the forager hears the same rule, so it is every food trade — `storage-and-distribution.md §14.13`).** The
  decision every food trade shares (`BehaviorSystem`'s `needsFood`) has two halves — *the village
  wants food* and *my own household's larder is short* — and the second one sent a hunter into the
  woods whenever their cupboard was a little low, which with no limit set and the stores full is
  most days. **A forager's answer to that question is an armful; a hunter's is 900 meat**, into a
  lodge the village has no reason to clear, whose overflow goes down at a full store's door (D370).
  Twelve fixture seeds with a lodge and a fishery carried **3,671,184 food on the ground** after
  fifty years. The hunter and the fisher now read the village's half alone
  (`theVillageNeedsFood`); the forager keeps both, because there the household half costs an
  armful and D385 measured that removing it cost the village people. **A short larder still gets
  its answer** — the fetch errand, which is what a granary is for. Measured, twelve seeds × fifty
  years with a lodge and a fishery: alive/peak/starved **190 / 221 / 11 → 198 / 205 / 9**, the
  ground **3,671,184 → 0**, produced 4.6M → 873k; **without a lodge, byte-identical** (no golden
  moved anywhere — no shipped fixture has one). Guards:
  `AHunterWithAShortLarderFetchesFromTheGranaryInsteadOfHunting`,
  `AFisherWithAShortLarderDoesNotCast`, and `FoodConservationTests`' ground bound.
- **Year-round.** ⛔ Not season-gated. `IsForaging` must **not** include hunting, or winter will
  march the hunter home — *this is D281 exactly, and it will happen again if a state is reused.*
- The lodge holds a local buffer a marketer runs dry, exactly as the farm and the fishery do — and the hunter drains it themselves when it cannot take another kill (D370; `fishing.md` has the rule). **And whoever clears it stands on the lodge for the tick they load** (D373): the load and the first step of the haul used to happen in one tick, so nobody was ever drawn at the lodge — Joe: *"they stop a few pixels before actually going to it and then turn around."* Guard: `AHunterClearingTheLodgeIsSeenStandingOnIt`.
  (`BuildingRow.LocalStoreCap`).

### 6a. ⭐⭐ THE STALLED LODGE — UNDER A MET LIMIT THE HUNTERS CARRY IT IN (D683, Joe's call)

**Joe's Hollowmere (`Hollowmere-plover-rowan-45`, Year 25):** the lodge held **2,184 meat** with **636 on the shelves**, the
meat row set to 2,500, one hunter of three seated, two laborers idle, and the card reading *Working — 2,184 waiting to be
carried in*. Joe: *"why is the hunter's lodge so backed up? just not enough hunters?"* Given three options he picked
**"Hunters carry it in."**

**The stall, traced:** the limit counts every buffer (D420, `stock-limits-and-laborers.md §4.5`), so the lodge's own meat
met the row; a met row stood the hunters down (`LabourQuota`, D409); nobody but the producer and the marketer clears a
buffer (D370), the marketer reaches a lodge only after every counter is stocked and every dead larder emptied
(`PlanMarketErrand`), and D370's one claim per hut let one carrier at a time take an armful a trip. **So the stall is
self-sustaining:** the uncollected meat keeps the limit met, the met limit keeps the hunters off, and one carrier drained
2,264 meat 40 at a time over years. The quota's own comment had named this seat as *"the lever, if a played village ever
shows a lodge draining too slowly under a met limit"* (D370) — it did.

**The rule (three pieces, one lever):**

1. **The quota keeps every seat of a swollen lodge under a met limit** (`SimWorld.SeatsKeptToDrainSwollenBuffers`; a
   lodge is swollen at or above half its room, D394's own word for *a buffer the hands cannot keep up with*). Every seat,
   not one and not *the seats held now*: the quota derives what the village wants from its state (D16), and reading who
   happens to be seated would make the want depend on what the allocator last did — and read zero in exactly the stalled
   state. *One carrier per hunt waiting* (`ceil(food / meat_yield)`) was the other candidate; on a three-seat lodge it
   differs by one seat over a third of the swollen band, which no harness can tell apart, and it leaves a hunter standing
   as a laborer beside a lodge whose card says the hands cannot keep up. **Below half the seats fall to nought as before**
   and the marketer takes the rest (D370). ⛔ **The carriers are asked at the marketer's rank, not the hunter's.** Asked at
   the hunter's — first of every trade — they outranked firewood and houses: on the harness's four-founder re-foundings
   beside a full lodge, three of the four carried meat the village already had and nobody built a roof, and the band froze
   in its first winter (frozen 4 → 16 in nearly every `every`-arm valley). The village has its meat; carrying it in is the
   marketer's errand (*"when they have nothing more pressing"*), so it ranks where the marketer does, out of the hands
   nothing more pressing wants — and is never zeroed by the food floor, since what it carries is food on its way to a shelf.
2. **A seated hunter under a met limit carries instead of hunting** — the path that existed (`TryClearOwnBuffer`, every
   armful once the village has its meat) — **and every seated hand may set off at once** (`SimWorld.EveryHandMayCarry`):
   D370's one claim per hut was written for a buffer of about one armful, where a second walker finds it empty, and is not
   asked while the buffer is swollen *and* holds an armful for every seat filled, so nobody walks for nothing. The
   marketer's single claim in `PlanMarketErrand` is unchanged; a farm's one-load buffer (D171) seldom qualifies by
   arithmetic. ⚠️ The claim is on the walk *to* the hut, not the haul away from it — so even before D683 three hunters
   could be *carrying* at once; what the rule changes is that they may *set off* together.
3. **The card says why** (`SimWorld.SwollenBufferNote`): *"The village has its 2,500 meat (2,820 stored), so the hunters
   are carrying it in rather than hunting — 2,184 waiting, 55 armfuls."* And a lodge nobody is posted at says so
   (`SimWorld.NobodyPostedNote`: *"Nobody is posted here — the meat limit of 2,500 is met. The marketer carries in what is
   left — 1,200 waiting."*) where it used to read *Working — 0 of 3 at it* — D147 kept the map's marker quiet for an empty
   building the village has no use for and promised the inspector that sentence; the card never carried it. `IdleNote`
   and the marker are untouched; the sentence is the quota's own (`WhyTheVillageWantsNone`), never restated.

**Guards** (`LodgeCarriersTests`, five; posed with eight founders because the carriers come after the foragers and the
woodcutter the derivation wants, and four hands leave one): a lodge at 2,000 meat under a met row keeps all three seats,
all three carry, two set off together (the third was mid-errand when the pass seated them), the lodge is below half within
two seasons (115 ticks) and the seats are released after; a lodge below half under a met row still stands the hunters down
and says *the meat limit of 500 is met*; three typed hunters clear a half-empty lodge one at a time (D370 holds below
swollen); the two sentences. **Red-checked, 8 mutants, 12 reds, none kept at zero:** the carriers' arm gone 1; the many-hands
exemption gone 2 (1 aimed; the other is the pose's history moving — D418); the claim never asked 2 (likewise); seats kept
for an unswollen lodge 3; the trade still reported as capped 1; the note silent on the limit 1; the nobody-posted note
silent 1 (⚠️ its first pose did not compile and scored a false zero — D420's trap — and was re-posed); `Needed` nought 1.

**Measured before it was typed** (`tools/harness/ZzLodge.cs`, fifty years, a lodge raised free at t0, before → after; the
untouched tree run from a detached worktree the same sitting):

| arm | valleys | alive | starved | frozen | years a lodge sat above half (sum) | stall ticks: met row, swollen lodge, nobody seated | meat carried out of the lodges | hunts |
|---|---|---|---|---|---|---|---|---|
| `lodge x1` — the played opening + a lodge, the shipped limits (**Joe's state**) | 30 | **331 → 703** | 528 → 358 | 196 → 235 | 1,406 → 471 | 637,117 → 15,428 (29 → 10 valleys) | 99,637 → 1,639,606 | 1,695 → 9,670 |
| `every x1` — + a fishery and a farm | 13 | 370 → 403 | 23 → 0 | 210 → 191 | 598 → 174 | 283,016 → 38,477 | 2,480 → 145,354 | 356 → 1,176 |
| `lodge x10` — limits ×10 at Year 5 | 30 | 646 → 796 | 39 → 0 | 783 → 672 | 1,378 → 1,066 | 234,236 → 17,455 | 617,159 → 1,505,917 | 6,854 → 8,915 |
| `every x10` | 13 | 278 → 278 | 0 → 0 | 359 → 365 | 598 → 428 | 140,450 → 48,610 | 66,918 → 126,436 | 978 → 1,183 |

- **What the baseline says about the stall:** in every unattended valley the lodge fills in Year 1 (three hunts, 2,700),
  meets the 2,500 row by itself, and sits swollen for the whole fifty years with nobody seated — 23,820 of 24,000 ticks in
  valley after valley — because the unattended village has no market and so no marketer ever comes (D601). 528 starved beside
  a full lodge across the `lodge x1` arm is D362's disaster, in the harness. ⚠️ **The harness is not a player (D447):** Joe's
  village has a marketer and the stall there took years rather than for ever; the arm measures the rule's shape, not his game.
- **What the fix does there:** the lodge is swollen a third as long, the stall is a hundredth of what it was (what is left is
  the slack pass's cadence — up to 120 ticks between the quota asking and the allocator seating), fourteen times the meat
  reaches a shelf, the hunters hunt five times as often (the limit un-meets as the shelves are eaten down), and alive doubles
  in Joe's arm (up in 24 valleys of 30, down in 2). The `x10` arms, where no lodge alone can meet the row, move because a full
  lodge now clears with every hand rather than one (`EveryHandMayCarry`).
- **Read per seed before the mean (D664's trap):** `every`'s founders freeze roofless in Winter 1 in every valley (D648's
  finished-farm shape) and what lives is a re-founding, so its arms are coin flips a change re-rolls; three valleys are dead in
  both arms of every row. In Joe's arm two small valleys that lived on the baseline died after (seeds 105 and 115, 16 and 5
  alive, both frozen) and none revived; in `lodge x10` three died and five revived. **The first take of this rule ranked the
  carriers at the hunter's rank and read `every x1` alive 370 → 305, frozen 210 → 303** — the `ZZD` lines said why (the
  re-founders carried meat instead of building a roof), which is how the carriers came to be asked at the marketer's rank.

---

## 7. ⭐ What it must be worth, and how that is settled

**Per hour worked, measured with demand held open** — never per load. D286 and D288 are the
whole argument: the retired fishing guard compared one cast to one trip and called fishing a
winner while it was making **311 against a forager's 721**.

The rig exists: `HungryForever` plus ticks-on-the-job, in `FishingTests`. **It is the instrument
for this slice too**, and the target is stated as a ranking rather than a number:

| | food per 100 ticks worked | measured |
|---|---|---|
| Forager | **721** | 2026-09-03 |
| Fisher | **830** | 2026-09-03, at `fish_yield` 300 |
| **Hunter** | **must exceed 830** | to be set by the same rig |

**Re-measured with desire paths (D358, 2026-09-11):** a forager walks the village's worn lanes and
reads **892**; a fisher at `fish_yield` **400** reads **1,048**; a hunter at `meat_yield` **800**
reads **1,266** (1.21×, D293's step). `hunter_lodge_store_cap` **2,400** — three hunts.

**Re-measured under clock B (D361, 2026-09-12):** forager **892** (a row walk; unchanged), fisher at
`fish_yield` **280** reads **1,088** (1.22×), hunter at `meat_yield` **900** reads **1,319** (1.26×).
`hunter_lodge_store_cap` **2,700**, `fishing_hut_store_cap` **840** — three loads each. ⚠️ *Every
change to the walk's clock re-levels this ladder; the ratios are the target, the numbers are not.*

⚠️ **Set `meat_yield` from the rig, and only from the rig.** Do not reason from per-hunt numbers;
they have now been wrong twice.

---

## 8. Slices

1. **The lodge works.** `HuntingRadius`, placement rule, 3 seats, the quota and allocator arms
   (D279: *a trade the labour system does not know about is a building that cannot work*), the
   hunt action, `Meat` and `Leather`, the buffer, the marketer. Yield set by the rig.
2. **Game thins and recovers.** Sparse depletion, `RegrowthSystem` arm, the pressure D3057 wanted.
3. **You can see them.** Roaming game drawn on the map.

---

## 9. ⭐ Slice 3 is a view of a number, not a herd of entities

Joe wants to see animals moving in the forest. **The sim will not grow animal entities for it.**

✅ **Shipped 2026-09-05, and it covers BERRIES too.** The same argument applies to forage:
gatherable food is a function of standing woodland exactly as game is (D297), so berry
patches are drawn from the forest tiles as well — **still**, where the animals roam, which is
the cheapest way to tell the two apart. Felling a wood empties it of both.

The abundance in a range is already a number; the animals the player sees are **drawn from it** —
count and rough positions derived from the tile's abundance and a seed, moved by view-only
interpolation the way villagers already glide. So:

- **No sim state, no determinism risk, no state-hash surface, no per-entity pathfinding.**
- The picture cannot lie about the sim, because it *is* the sim's number rendered.
- Hunting a range thinner visibly empties it of animals, for free.

✅ **Joe, 2026-09-03: *"no models is fine for now — but we're going to have to add models in eventually."*** So this is a **staging decision, not the end state**, and it is recorded here because slice 3's whole architecture depends on which it is. ⭐ **Drawing game from the abundance number survives the change**: when models arrive they replace the SHAPE, not the source — the thing being drawn is still a number the sim already keeps, so nothing about determinism, the state hash, or pathfinding changes when the rectangles become animals. *A herd of entities would have had to be torn out; a view of a number does not.*

⚠️ **Until then, there are no models.** The whole game is coloured rectangles
and circles (`DrawRect`/`DrawCircle`, zero textures). Game will read as small moving shapes in
the trees — which is what every other living thing in this village looks like.
