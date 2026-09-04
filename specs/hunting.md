# Spec: Hunting — the food that works in winter, and the first hide

**Decisions:** D19, D253, D262, D277, D286, D288 — and **D3057(b)**, which chose hunting over
livestock and is the argument for everything below.
**Phase:** food breadth, immediately after fishing (D253: *"after town hall is fishing and hunting"*).
**Status:** ◡ **slice 1 SHIPPED 2026-09-03; slices 2 and 3 not started.**
The lodge is placeable, staffable and worked; `Meat` and `Leather` are real; the buffer holds three hunts and a marketer runs it dry; `meat_yield` is **600**, set by §7's rig (D293). ⛔ **Game does not deplete yet** — §4's thinning and recovery is slice 2, so a range is inexhaustible today and that is a known gap rather than the design.
**Owner:** Joe + Claude Code

---

## 1. Goal

**Joe, 2026-09-02:** *"Hunting ultimately yields more food, but it takes longer and isn't
instantaneous. 3 hunters per hunting lodge. Different types of game meat. And leather."*
And, separately: *"I want the user to see animals/game roaming the forest… just models of the
game existing in the forest, moving around. I want a visual representation of them on the map."*

So the totem pole Joe has stated, bottom to top: **foraging → fishing → hunting.**

---

## 2. Why this is not content

D3057 chose hunting over livestock and listed exactly what it buys, and every line still holds:

- **Year-round outdoor work for the 86%-idle winter.** ⭐ *This is the point.* Foraging stops in
  winter (D44). Fishing does not, and now hunting does not either — but hunting is the one that
  makes winter a season with a **job** rather than a season with a **store**.
- **Hides for clothing.** `specs/clothing.md` is blocked twice over and one of the blocks is
  "there is no leather". The chain is **hunting → leather → tailor → clothing** (D2753's build
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

**Game lives in the forest** (Joe's call). Abundance in a lodge's range derives from the wooded
share of it — the same shape as `WoodedShareAround`, which is already asked of a ring and already
correct about map edges and non-forest tiles.

- A lodge in deep woods is worth hunting from. A lodge in a meadow is not, and **says so at
  placement** the way the fishing hut says *"it must touch water"* (D43: two different mistakes
  must get two different sentences).
- ⛔ **Hunting thins the game, and game recovers.** This is the opposite of fishing, which was
  deliberately given *no* depletion (*"a consistent source of food that does not run out"*).
  **The contrast is the design**: fishing is reliable and modest, hunting is rich and exhaustible.
- Depletion is **stored sparsely** and recovers in `RegrowthSystem`. ⚠️ **Sparse-hash rule:** a
  village that never builds a lodge must hash byte-identically to one in a world without hunting.

---

## 5. The goods

| Good | Nutrition | Notes |
|---|---|---|
| `Meat` | **1** | ⛔ **Must equal every other edible.** D277's validator refuses a config whose edible goods disagree, because the survival floor is derived from a single figure. |
| `Leather` | 0 | Inedible. Its only consumer is the tailor, which does not exist yet — so it accumulates, and that is correct and visible. |

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
- **Year-round.** ⛔ Not season-gated. `IsForaging` must **not** include hunting, or winter will
  march the hunter home — *this is D281 exactly, and it will happen again if a state is reused.*
- The lodge holds a local buffer a marketer runs dry, exactly as the farm and the fishery do
  (`BuildingRow.LocalStoreCap`).

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
