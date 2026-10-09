# Spec: Clothing — the thing that makes winter workable

**Decision:** D45's third clause. **Slice:** `specs/environment-and-seasons.md §11`, slice 4.
**Status:** ⏸️ **specced and NOT started. The input is no longer blocked** (leather has come from
hunting since D291, D69's road); **the payoff is still narrow, re-measured on 2026-10-08 (D533, §5.2)**,
and **Joe parked it (D534)** until winter has work worth wanting.

---

## 1. Goal

A manufactured garment — from leather, wool or cotton — that **removes the outdoor cold
danger entirely** (D45). That is the mechanic. The *point* is what it unlocks: with
clothing, sending somebody out to work in winter is a normal day; without it, it is
dangerous. That is §2.7's *unlock by doing* arriving out of a survival mechanic rather
than a tech menu, which is why D45 called it "the consequence that makes it structural".

---

## 2. Which pillars this serves

- **§2.7 knowledge-based tech tree.** The unlock is a thing you *build*, and the reason
  you wanted it is a thing you *felt*.
- **§2.5 environment with teeth**, and the organising rule of the seasons spec: a season
  with teeth is one the player prepares for. Clothing is preparation you can point at.
- **§2.3 systemic escalating pressure.** A village that grows needs more winter labour,
  which needs more clothing, which needs more animals or more land.

---

## 3. What it would take, mechanically

Small, once its inputs exist:

- A `Goods.Clothing` (or per-material variants), stored and hauled like everything else.
- A **tailor** workplace: D29's processing shape, third of its kind after the woodcutter
  and the market. Consumes hides/wool/fibre, produces garments.
- A per-villager `HasClothing`, hashed, and one branch in `HearthSystem`:
  `Shelter.Outdoors` costs nothing to somebody who is dressed.
- Demand in `LabourQuota`, derived the way every other quota is: *how many villagers are
  unclothed?*, not a share of the population.
- Garments wear out, or the demand is a one-off and the tailor is an errand rather than a
  livelihood — the same trap `LoggersWanted` fell into and D22 records.

**None of that is the hard part.**

---

## 4. What it needs first, and none of it exists

Clothing is made of leather, wool or cotton. The village has **no animals and no crops**:
`JobKind` is Forager, Logger, Woodcutter, Marketer, Builder. So the chain below clothing
is entirely unbuilt, and it is the largest unbuilt thing on the roadmap (D19, D39):

| Needs | Comes from | Built? |
|---|---|---|
| Leather | hunting (D69's road, no trade needed) | ✅ since D291: `leather_yield` 10 a catch, unspent, limit 200 |
| Wool | herding (sheep), shearing | ❌ |
| Cotton / flax | farming | ❌ |

That is D39's named food roadmap — *farming crops, herdsmen and butchering, gathering,
fishing* — which is a phase of work, not a slice.

---

## 5. ⛔ And the payoff does not exist either — measured

Clothing removes the outdoor danger. `exposure_days_outdoors: 0` **already models a
perfectly clothed village**, so the whole benefit can be measured before building any of
it. Over 300 years on the village fixture:

| | mean pop | froze | starved | worst cold | winter spent outdoors | **winter spent at work** |
|---|---|---|---|---|---|---|
| As it ships | 21 | 0 | 0 | 56% | 7% | **0%** |
| Everyone clothed | 21 | 0 | 0 | 0% | 7% | **0%** |

**Nothing changes.** Not the population, not the deaths, not even how much of winter is
spent outdoors — because the 7% that is spent outdoors is *walking*, and villagers walk
exactly as much either way.

**The 0% is the finding.** Nobody works outdoors in winter at all, so there is no danger
for clothing to remove. And the reason nobody works is not the cold — it is that **the
labour quota has nothing it wants doing** (D52): the village already has its logs, there
is nothing to forage, and cutting more timber was the make-work D52 deleted.

So clothing today would remove a danger that does not bite, in order to unlock work that
is not wanted. It would measure as a no-op, exactly like the fire-resets rule D53 probed
before building.

### 5.1 What a second probe added (D59) — the payoff is labour, not lives

Measured again while sizing livestock, and it **sharpens this section rather than
overturning it.** Two findings, both structural:

- **Winter has the hands.** A mean of **12.7 spare adults out of 14.7** through every
  winter, over 300 years, on both the shipped config and the fixture — against 0.7 spare
  in summer. The `0%` above is not a shortage of people. It is the labour quota having
  nothing it wants doing (D52). Give winter a job and it will be taken.
- **But clothing can never save a life at the seven-tile bound.**
  `BehaviorSystem.TrySeekWarmth` breaks a villager off at 50% of the threshold and holds
  them at the fire until they are back to zero. For a fire `d` travel-ticks away the
  steady cycle is *work 30−d, walk d, thaw 30+d, walk d*:

  | distance to the nearest fire | share of winter actually worked |
  |---|---|
  | 0 (adjacent) | 50% |
  | 4 | 41% |
  | 7 (`MaxHomeToWorkTiles`) | ~34% |

  Freezing outright needs **60 unbroken ticks outdoors**; break-off fires at 30 and the
  walk home is at most 7. Nobody working can ever reach it.

**So the acceptance bar in §7 is right and its metric is not deaths — it is winter work
done.** Clothing takes an outdoor winter job from ~34% duty to 100%, which is roughly a
**3× on winter labour**. Three consequences worth carrying into the build:

1. **This is better than what D45 imagined, not worse.** A clothing that saved lives would
   be a clothing the village must have; a clothing that triples winter output is an
   *unlock*. §7's "clothing becoming mandatory" failure mode is now satisfied by
   construction rather than by tuning.
2. **The stakes escalate on their own.** Once D58's per-site yield lets homes and work
   spread past 30 travel-ticks from a fire, the same garment quietly becomes life-or-death
   with no number changed. That is §2.3 arriving out of two systems built for other
   reasons.
3. **If lives are wanted sooner, the file to open is `seek_shelter_percent`, not the
   day-counts** — the same refusal D53 recorded for option (b).

### 5.2 Re-measured on today's code (D533, 2026-10-08): no longer a pure no-op, and still narrow

§5 was measured on 2026-08-01, before hunting in the woods, the quarry and the mine, stock limits,
laborers and fences. `tools/harness/ZzClothing.cs` re-takes it: 30 shipped valleys (the played
opening, ZzBase's) and 13 every-source valleys (lodge, fishery and farm free at t0), fifty years,
in three cold worlds: **bare** (today, 15 days outdoors / 25 under a roof), **coat** (outdoors chills
only as fast as a roof does, 25 / 25: a garment as a roof you wear), **off** (`exposure_days_outdoors`
0, §5's upper bound). Shares are of working-age villagers' winter ticks.

**At the shipped stock limits, nothing after the founding changes:**

| | at a fire | working outdoors | break-offs / valley | cold deaths, Year 1 | cold deaths, Years 2–50 |
|---|---|---|---|---|---|
| played: bare / coat / off | 71 / 71 / 72 % | 1.0 / 1.0 / 1.1 % | 61 / 16 / 0 | 16 / 16 / 0 | **0 / 0 / 0** |
| every: bare / coat / off | 71 / 76 / 70 % | 1.4 / 1.2 / 1.9 % | 50 / 20 / 0 | 28 / 13 / 0 | **0 / 0 / 0** |

- **Every cold death is a whole founding party in Year 1, before a home has a fire.** In the played
  opening they die *under a roof with no fire*, which no garment touches. In the every-source opening
  it is D515's free-farm race, which Joe closed as a fixture effect.
- **Hunters do not hunt in winter, with or without cold.** In 37 of 39 every-source runs no hunter
  held the job in any winter: the quota hires none. The likely cause is meat already at its limit
  (`meatIsEnough`, `LabourQuota.cs` ~462), but that is **not traced**. The code's comment says the
  lodge *"is what winter is for"*; in these villages winter never uses it.

**When the player asks for more** (every limit except tools ×10 at Year 5, the posed version of
D62's argument that limits give clothing its reason):

| | working outdoors | break-offs / valley | cold deaths, Year 3+ (outdoors / roof) | alive at 50 |
|---|---|---|---|---|
| played: bare / coat / off | 6.2 / 5.9 / 6.0 % | 73 / 26 / 0 | 7+7 / 2+0 / 0 | 158 / 171 / 179 |
| every: bare / coat / off | 8.2 / 8.2 / 8.7 % | 56 / 28 / 0 | 5+14 / 2+18 / 0 | 98 / 164 / 221 |

- **Winter fills with outdoor work** (1% → 6–8%, almost all laborers digging and hauling), so the cold
  starts to cost something: about seventy break-offs a valley, and a few people frozen after the founding.
- **A coat removes about two-thirds of the break-offs and nearly all the *outdoor* freezes** (played 7 → 2).
- **It does not raise the work done** (6.2 → 5.9%, 8.2 → 8.2%). The limit bounds the work, not the
  cold: a laborer who goes in to get warm comes back and finishes the same job later. §5.1's "3× on
  winter labour" assumed demand without end, and no village has that.
- **It does nothing for the roof deaths**, a household out of firewood, which are the larger share
  in the every-source arm. That is fuel, not clothing.
- ⚠️ Raising the limits ×10 **from day one** kills all 13 every-source foundings in Year 1 (labour drawn
  off the first firewood). That is the harness playing badly (D447), not a finding about clothing.

**Reading it:** clothing is now a real but *small* unlock: fewer *"going in to get warm"* lines and
fewer outdoor freezes in a village that pushes winter work. It is still not D45's *"winter as a
working season"*, because winter still has almost nothing the village wants doing. §6 stands: **the
thing that makes clothing matter is winter work worth wanting**, and today the nearest candidate is
the lodge the quota never staffs in winter.

---

## 6. What makes clothing matter, and it is one thing

**Winter needs work worth doing before it needs a coat.**

That work is already named, twice, by two decisions that arrived from different
directions and turn out to be the same answer:

- **D44's forward note** — *"winter is eventually a herding and slaughtering season"*,
  Joe's own answer to "what is winter work" and the reason D52 refused to make everyone
  a logger.
- **D39's roadmap** — herdsmen and butchering, as a food source.

**Herding is both clothing's input and clothing's reason.** Animals give winter something
to do that is genuinely outdoors, genuinely wanted, and genuinely year-round — and the
same animals give the hides and wool that clothing is made of. Build it and clothing stops
being a no-op the same day it becomes possible.

That is the recommendation: **take livestock next, and clothing directly after it.**

---

## 7. Failure modes to design against — for when it is built

- **A no-op that ships.** The measurement in §5 is the acceptance bar and it is
  comparative: with the winter work in place, a clothed village must do measurably more
  winter work than an unclothed one, or clothing is decoration (the same standard
  `specs/storage-and-distribution.md §8` sets for the market).
- **Clothing becoming mandatory rather than an unlock.** Every slice before it must stay
  survivable without it — D45's stated condition, and it holds today: with outdoor cold
  on, nobody freezes.
- **A tailor who is an errand.** See §3. Demand has to be continuous or the job is not a
  livelihood anybody holds (D22).
- **Two hard things at once.** Livestock and clothing are separate slices. D42's lesson,
  recorded twice already.

---

## 8. Definition of Done

Deliberately unwritten. This spec exists to record **why clothing is not the next slice**
and what would change that; the DoD gets written when the thing below it exists.
