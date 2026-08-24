# Spec: Per-site yield — ground that is worth going to

**Decisions:** **D58** (the mechanism, settled — per-site yield, not work-in-place), D178 (this
slice's scope and shape), D16 (numbers are derived, not picked), D2 (integer-only sim state),
D67 (**seams, not scatter** — the argument this spec leans on hardest), D112 (per-site yield for
the gatherer, already shipped), D120 (the fence came down), D171 (the farm's measured distance
bug), D152 (goldens last).
Neighbours: `crops-and-orchards.md` (the farm this changes), `seeded-map-generation.md` (the draw
order, which is the seed contract), `forests-and-gathering.md §3.2` (where the bound became a
budget), `environment-and-seasons.md` (soil depletion's eventual home).
**Status:** ✅ **BUILT and merged to `main`** (D178), **and its visibility half repaired** (D180).
Soil is regional and read by the farm; the sowing cap asks each farm's own haul; the player can
see the ground **and now read it in words**. **644 passing, 0 failing, 2 skipped of 646**, all
four goldens unmoved by D180. Proved by `PerSiteYieldTests` and
`FarmTests.AFarmsHarvestFallsOffWithDistanceFromItsStore`.

> **🔨 A FOLLOW-ON SLICE IS IN PROGRESS ON `phase/3-skill-and-apprenticeship` — §4.2a and §4.3
> (D194), which UNPARK THE FARM.** §4.2's sowing cap shipped as a **prediction**, and the ledger
> says it is short by **one to two tiles at every distance** and leaves a distant farmhand idle
> for **27–55% of the autumn it is supposedly too busy for**. §4.2a replaces the prediction with
> the farm's own memory; §4.3 tells the player at placement that a farm far from a store halves
> its own harvest. **⛔ And the ledger also settles what the farm can never do: thirteen tiles ten
> ticks out is physically impossible** — read §4.2a before proposing a fifth cause.

> **⛔ §5 SHIPPED BROKEN AND NOBODY NOTICED FOR A DAY (D180).** The **Ground** button's label was
> written inside the *routes* button's handler, so pressing it flipped the overlay and left the
> button insisting *"Ground: off"*. Joe: *"it stays as 'off' regardless… I can't really tell
> which areas are good or bad."* The overlay had worked the whole time. **A control whose only
> feedback contradicts what it did is D103's unreachable feature arriving through the label** —
> and this spec's own §5 argues the slice is nothing without it. See §5.4.

> **⚠️ This status line is load-bearing. Update it the day the slice merges** — D159 found five
> specs claiming "not started" for systems that had shipped.

---

## 1. ⭐ What this actually is, because the queue was wrong about its size

`DESIGN.md §4` calls this *"the biggest payoff on the board and the largest re-derivation."*
**An audit of the code says otherwise: over half of D58 has already shipped under other names.**

| D58's parts | State |
|---|---|
| Retire the 7-tile bound **as a fence** | ✅ **Done (D120).** `Household.ChooseSite` uses it as a score term and a search radius. *"A home beyond it is a family the village feeds less well, not one it refuses to house."* |
| Per-site yield for **gathering** | ✅ **Done (D112).** `SimWorld.GatherYieldAt` is `gather_yield × wooded tiles ÷ ring tiles`. A hut in thick wood genuinely pays more. |
| Per-site yield for **farming** | ⛔ **Not done.** `FieldTilesOneFarmerKeeps` is one number for every farm in the valley. |
| **"Distant sites pay better"** | ⛔ **Not done, and it is the important half.** Nothing in the sim rewards distance; `VillageEconomy` does not read travel cost at all. |

**D58's own warning is what makes the last row the point:**

> *It is two halves and both are budgeted: distance costs, **and** distant sites pay better.
> Every forage site currently sits on a ring with the work in the middle, so per-site yield on
> its own is **a tax on sprawl that buys nothing** — inequality with no reason to accept it. The
> design is **the frontier homestead beside a rich patch: isolated, and eating well for it.**
> That is D32's interesting inequality.*

**So this slice is not a re-derivation. It is: make soil real, and give the farm per-site yield
on both axes.** Gathering is left alone — it already works.

---

## 2. Which pillars

- **§2.3 Systemic escalating pressure** — *"resource radii exhausting and forcing expansion."*
  Ground that differs is what makes expansion a decision rather than a chore.
- **§2.2 Smart labour** — D32's *interesting inequality*: a household that settles far genuinely
  eats differently, and now it can eat **better** as well as worse.
- **§1.1 Legibility** — ⛔ **soil the player cannot see is an invisible multiplier**, which this
  project has rejected three times (D37's spoilage, the seasonal yield curve,
  `skills-catalog.md §1.1`). §7 is not decoration.
- **§0.1 The niche** — *"the challenge is in the planning, never in the punishment."* Good ground
  you can see and choose to walk to is planning. Good ground you cannot see is a lottery.

---

## 3. ⭐⭐ Soil becomes regions, not noise — and D67 is why

**Soil already exists and is the right axis.** `MapGenerator` step 5 fills `soil[]` with
`rng.NextInt(soil_quality_min, soil_quality_max + 1)` per tile — **generated, hashed, in the seed
contract, and read by nothing**, laid down early *"so that when soil depletion lands it does not
have to change the DRAW ORDER."* That foresight is what makes this slice cheap.

**⛔ But per-tile uniform noise is the wrong shape, and D67 already said so about ore:**

> *SEAMS, NOT SCATTER … you can see a seam, so going after it is a **decision** rather than a
> lottery. **Scattered ore would be texture.***

**Per-tile soil noise is texture.** A field is thirteen tiles; over thirteen uniform samples every
site in the valley averages the same. **Uniform noise gives per-tile variance and zero per-site
variance, which is precisely the one thing this slice needs.** Soil must be *regional* — good
ground and poor ground you can point at.

### 3.1 ⭐ How, without moving one byte of any seed's layout

**Keep the draw count identical and reshape afterwards.** Step 5 goes on drawing exactly
`width × height` values from the seeded stream; a **deterministic** pass then turns that noise
into regions, consuming **no draws**.

> **⛔⭐⭐ AND THE FIRST ALGORITHM THIS SPEC PROPOSED WAS WRONG — the probe said so before a line
> shipped, which is the entire argument for METHODOLOGY §3.** The draft said *smooth the noise*.
> **Smoothing makes site-to-site variance worse**, because averaging noise regresses everything
> toward the mean: it destroys amplitude rather than creating structure. Measured on the shipped
> valley, spread across 104 candidate 13-tile fields:
>
> | | p90 ÷ p10 | best ÷ worst |
> |---|---|---|
> | raw per-tile noise | 134% | 181% |
> | smoothed ×2 | 123% | 142% |
> | smoothed ×8 | **113%** | 130% |
> | **value noise, lattice 8** | **200%** | **308%** |
>
> *An hour of probe against a slice built on a mechanism that reduced the number it existed to
> raise.*

**The algorithm is value noise: sample the already-drawn array on a coarse lattice and interpolate
between the samples.** Lattice points keep the full drawn amplitude — which is exactly what
smoothing throws away — and the interpolation supplies the structure.

- **`soil_region_scale: 8`**, measured. Finer (4) averages out across a 13-tile field; coarser
  (24) leaves too few distinct regions. **8 is a couple of fields across — a region you can see
  and walk to**, which is D67's *seam* rather than its *scatter*.
- **Integer bilinear interpolation** (D2). One divide per tile, no floats.
- Measured at lattice 8: **p10 82, median 124, p90 164** — a genuine two-to-one between poor and
  good ground.

**Why this matters more than it sounds.** Draw order is the seed contract
(`seeded-map-generation.md §1`). Clumping soil by drawing region centres would consume a different
number of values, which shifts **step 6 — stone and iron — for every seed ever written down**, and
D91 took explicit care to append those precisely so nothing moved. **With the count preserved, the
RNG state entering step 6 is identical**, so:

- ✅ Terrain, river, woodland, forage sites, the founding site, stone and iron: **byte-identical**.
- ⚠️ **The soil bytes change**, and `StateHash.MixMap` mixes them — so **the map golden moves, and
  nothing else does.** One golden, one stated reason (D152).

**Integer-only** (D2): the smoothing is an integer box average over the neighbours, applied a
stated number of passes (`soil_smoothing_passes`, data). No floats anywhere near it.

### 3.2 ⭐ The founders settle where they can survive, not where the ground is best

**Half of this is already true and nobody wrote it down.** `ChooseFoundingSite` is **step 4** and
soil is **step 5** — so the founding site is chosen *"the biggest piece of walkable ground"* with
**no knowledge of soil whatsoever.**

> **⛔⭐ AND THE PROBE KILLED THE INFERENCE DRAWN FROM THAT.** This spec first argued the founding
> ground was therefore *"already safe-not-rich by construction"*. **It is not.** Chosen without
> knowledge of soil means the founding ground's percentile is **uniformly random**, and across
> eight seeds at lattice 8 it came out:
>
> | seed | 12345 | 1 | 2 | 3 | 7 | 11 | 42 | 99 |
> |---|---|---|---|---|---|---|---|---|
> | founding-ground percentile | 22% | 11% | **99%** | **93%** | **83%** | 34% | 11% | **91%** |
>
> **Half the seeds hand the founders ground in the top fifteen per cent** — seed 2 puts the best
> field in the valley on their doorstep. **That deletes D58's frontier payoff outright** in half
> of all games, which is precisely the failure this slice exists to prevent.
>
> ✅ **The good news from the same run: the best ground is always worth going to.** Across all
> eight seeds it sits **12 to 50 tiles out**, at **177–193** against a valley median of 99–134.
> *The frontier homestead beside a rich patch is there in every seed* — it just has to stop being
> outshone by the doorstep.

**So damping is required rather than optional**, and it is a stated rule inside a small radius of
the founding site.

**⭐ The rule is a CAP, not a reduction, and the cap is the reference itself** (§4.1): soil within
`founding_ordinary_radius` of the founding site is **capped at the valley mean**.

- **It can only ever take away, never add** — ground that is already poor is untouched, so this
  cannot quietly make a hard seed easier.
- **⭐⭐ And it gives the cold start a property worth having: the founders' fields yield *at most*
  exactly `crop_yield_per_tile`** — the locked number, which §4.1 defines as the yield on average
  ground. **The opening can therefore never be *better* than today's**, which is the direction
  that matters, since today's opening is the one `cold-start.md` measured and Joe played.
- **It is diegetic, which is the test this project applies:** *the exiles settled where they could
  live through the first winter, not where the ground was best.* `tech-tree.md §3b` is right that
  the best game rules are also true ones.
- **It is a local cap, not a global gradient** — soil does not "improve with distance", which
  would read as a game rule rather than a place.
- **It costs no draws and no draw-order change**, because the founding site is already known when
  step 5 runs.
- ⚠️ **What it does NOT guarantee is that the opening is unchanged.** A seed whose founding ground
  was *below* the mean now farms below-reference ground, and the opening gets harder there.
  **That is why §9.5 re-measures the cold start from a run rather than asserting it** — and if the
  measurement says so, the cap gains a floor. **Decided by measurement, not here.**

---

## 4. ⭐ The farm gets per-site yield on two axes

**Both of D58's halves, one axis each.**

### 4.1 Soil — how good the ground is (*distant sites can pay better*)

**⛔ `crop_yield_per_tile` is on Joe's locked list and this must not re-derive it.** So soil is a
**multiplier around** the locked value, never a replacement:

```
crop = crop_yield_per_tile × (soil ÷ reference soil) × vigour ÷ 100
```

- **The reference is the valley's mean soil**, derived from `(soil_quality_min + soil_quality_max)
  ÷ 2`, not typed. **A field on average ground yields exactly what it yields today** — so the
  locked 67 is untouched and *acquires a precise meaning it never had*: **67 is the yield on
  average ground.**
- **This is `skills-catalog.md §3.2`'s shape one system over**, and for the same reason: a
  multiplier that averages to one leaves every derived number standing, where a multiplier above
  one silently inflates the whole economy.
- **Good ground pays more; poor ground pays less.** That is the decision.

### 4.2 Distance — how far the harvest has to go (*distance costs*)

**This is D171's measured bug and it is a per-site problem wearing a farm's clothes.**
`FieldTilesOneFarmerKeeps` charges *"a round trip to the steading"* in its own words, and is one
number for every farm — so a farm ten ticks from its store **sows what a farm next door could
reap** and rots the difference. Measured, ten years each:

| farm → granary | brought in |
|---|---|
| next door | 93–96% |
| 6 ticks | 52% |
| **10 ticks** | **46%** |
| 22 ticks | 25% |

**The fix is that the sowing cap asks *this* farm's real haul**, not the average farm's. The
derivation keeps its meaning — *what a well-sited farm manages* — and a poorly-sited one commits
less ground rather than committing the same ground and rotting it.

**⭐ And it makes the rot line honest, which is the whole of D167's argument.** D167 made rot mean
*you over-painted* or *you lost a farmer*; **distance is a third cause the game cannot currently
say**, and a rot line the player cannot act on is the weather D167 spent a decision deleting.

⛔ **`farm_store_cap` is not the lever and must not be touched** — measured at nought to seven
points across one armful to thirteen (D171). **⭐ Confirmed a second time by a different route
(D194):** an **8.7× buffer** (871 against 100 — thirteen armfuls instead of one and a half)
moved the ceiling from **6 tiles to 6** at ten ticks and **5 to 5** at sixteen. The haul ledger
says why: with all that room the buffer still took only **23 of 72 loads**, because it fills
once and the market cannot keep it drained. *Two independent measurements now say the same
thing; stop proposing it.*

### 4.2a ⭐⭐ THE CAP STOPS PREDICTING AND STARTS REMEMBERING (D194)

> **⛔ §4.2 SHIPPED A PREDICTION THAT WAS WRONG BY 20–100% IN THE DIRECTION THAT MAKES ITSELF
> TRUE.** `ReapableShareAt` scales a farm's field by `budgeted ÷ haul`, and **those two are not
> the same kind of quantity** — `budgeted` is a *round trip inside the field* (4 ticks) and
> `haul` is a *one-way walk to a store* (10). The ratio is not a share of anything. That it
> lands near the right answer at ten ticks is arithmetic coincidence.

**The ledger, at last, rather than a fifth hypothesis** (one hand, ten years, shipped config,
the committed ground posed at each level so the curve is visible):

| farm → store | the cap sows | what the farm can actually bring in | autumn spent **idle** at the cap |
|---|---|---|---|
| 10 ticks | 5 | **6** | **27%** |
| 16 ticks | 3 | **5** | **45%** |
| 22 ticks | 2 | **4** | **55%** |

**⭐ The cap is self-fulfilling, and this is the attribution rather than the inference.** It cuts
the field, the farmer then has nothing left to do, and the idleness is read back as proof that
the field was too big. *A guard that says "distant farms reap fewer tiles" was measuring the cap,
not a physical limit.*

**⛔⛔ AND THIRTEEN TILES TEN TICKS OUT IS PHYSICALLY IMPOSSIBLE, WHICH IS THE ANSWER TO THE
COMPLAINT THAT OPENED THIS.** Joe: *"a farmer plants 5 tiles of the 13."* Autumn is **120 ticks**
and thirteen tiles at that distance needs about **230**. The farmer is not being cheated of eight
tiles; they are being cheated of **one or two**. **The lever that actually buys thirteen is the
walk, not the cap** — the same farmer beside a granary reaps 13 and measures out at 21 before the
ground runs out. That is §4.3's warning, and it is why the two land together.

#### The rule

**A farm sows what it has already brought in.** Nothing is predicted; the farm's own best
autumn is the number.

```
at the turn of autumn:
    sownThisYear = tiles standing on this farm's ground
    handsThisYear = hands in the field right now

at the turn of winter, BEFORE the rot sweep:
    if (sownThisYear == 0) nothing to learn — a year the farm did not sow
    record = (sownThisYear - whatever is still standing) / handsThisYear
    learned = max(learned, min(record, FieldTilesOneFarmerKeeps))
    walkWhenLearned = the walk to the nearest store that takes food

next spring it commits:
    hands × (learned > 0 ? learned : the opening guess)
```

**⭐ A high-water mark, and nothing else — no probe, no settling back.** What a farm brought in
once it can bring in again; a thin year is about the hands that turned up, not about the ground.
That is D183's *give, never take* one system over, and it is what stops one short-staffed autumn
becoming a permanent verdict on a field.

> **⛔⛔ TWO DRAFTS OF THIS SECTION HAD A DELIBERATE PROBE AND THE RED CHECK DELETED IT.** The
> farm committed `learned + 1` a year and latched once a tile rotted. **Breaking the probe turned
> nothing red**: the settled memory and the tiles reaped came out *identical* at ten, sixteen and
> twenty-two ticks — **6/5/4 learned and 72/60/48 reaped either way.**
>
> **⭐ The reason is worth keeping, because it is not obvious.** `HarvestOneFarmCanBringIn`
> multiplies the per-hand number by the hands standing in the field *at that moment*, and
> `NextFieldToWork` re-asks it before every tile — so **a farm with two hands in spring and one
> by autumn already commits ground for two.** D86's live-allowance rule was always going to
> over-reach; the memory only has to notice. *The village probes on its own.*
>
> **And the failure modes settle it.** With no probe the worst a farm can do is sit on its
> opening guess — **exactly today's behaviour**. With one, the worst it can do is rot a tile
> every year, which is the weather D167 spent a decision deleting. A probe that changes nothing
> measurable is the invisible no-op this project has rejected four times (D56, D177, D187).

**⚠️ `sownThisYear` is the gate and it is not optional.** A farm held by a met stock limit
(`MaySow` false) sows nothing, ends autumn with nothing standing, and would read that as *"I
cleared my field"* — climbing to the cap over a few idle years and then over-committing the
moment the limit lifts. **A year with no crop teaches nothing.**

**⚠️ And the hands are taken at the START of autumn, not at its end.** D44 stands seasonal trades
down at winter, so a farm can be *empty* on the very tick the lesson is read — dividing that
autumn's harvest by the one straggler still standing there would make a two-handed farm's record
look twice what it was.

- **⭐ Why memory beats arithmetic here, stated so nobody re-derives the formula.** The true
  ceiling depends on the buffer's drain rate, the field's geometry, how full the granary is and
  how many hands turned up. **The measured curve fits no closed form** — solving
  `season ÷ (reap + walk)` wants a different constant at every distance, and the constant moves
  the wrong way with distance. *A spring-time formula is a guess by construction.* This is the
  refusal to be clever, not a clever thing.
- **⭐ It converges in two or three years and then brings in 100%**, because a farm that has
  proved six tiles sows six tiles.
- **⭐ The record is re-reckoned when the walk changes** — the player builds a granary by the
  fields, or demolishes the near one — because that is exactly when the old answer stopped being
  true. It takes the better of what it knows and what the fresh walk suggests, so a store beside
  the fields raises the field at once rather than a tile a year. **A memory that cannot be
  revised by the player is a scar.**
- **Per hand, like `WorkGroundAllowanceFor`.** Losing a farmhand in summer halves next spring's
  field and leaves the memory intact, so a farm does not forget what it knows because somebody
  died.
- **⛔ Bounded above by `FieldTilesOneFarmerKeeps` and never past it.** A well-sited farm's
  physical ceiling measured **21**; the derivation says **13**; **13 wins.** That number is the
  survival floor the whole economy is solved against (D16, D189) and a memory that could raise it
  would inflate a derived value from the far end. **Nobody is ever worse than today, and the
  derivation is never better.**
- **The opening guess is `ReapableShareAt`**, which is demoted from a ruling to a first year's
  optimism-free start. A brand-new farm behaves exactly as it does today, then learns.

#### What it costs and what it buys

**+20% of the harvest at ten ticks, +67% at sixteen, +100% at twenty-two**, and the idle quarter
to half of a distant farmhand's autumn goes away. **Two ints and a flag per farm, hashed** (§6).

### 4.3 ⭐ AND THE PLAYER IS TOLD, AT THE MOMENT THEY CAN STILL MOVE IT (D194)

**Nothing in the game says that a farmhouse far from a store will halve its own harvest**, and
that is the single largest legible consequence in the farm. There is already a distance warning
at placement — `MaxHomeToVillageTiles`, *"people will spend their days walking to it"* — and it
measures the wrong walk for a farm. **A farm's binding walk is to the nearest store that takes
food**, because that is where every armful past the first goes.

> *"That farmhouse is 10 tiles from the nearest granary. Its harvest will be about half what one
> beside a store brings in — build a store near the fields."*

**Warned, never refused** (D43, D86). A distant farm is a legal decision with a stated
consequence, which is this project's standing shape, and it is the difference between *"the game
cheated me"* and *"I put it in the wrong place."*

---

## 5. Legibility — the player must be able to see the ground

**A soil multiplier the player cannot see is exactly the invisible number §1.1 refuses.** If this
ships without §5, it is a lottery rather than a decision, and D67's argument applies to it.

- **⭐ A soil overlay on the map**, in the same family as the work-ground and harvest washes that
  already exist (D118). Off by default, one control — the player asks *where is the good ground?*
  and the valley answers.
- **On the farm's panel, a sentence rather than a number** (D147's rule, and the vocabulary
  `crops-and-orchards.md` already uses): *"Rich ground — a tile here is worth about a fifth more
  than average."* / *"Thin ground — about a fifth less."*
- **On the brush**, once per stroke (D42): painting a field on poor ground says so.
- ⚠️ **A soil *number* on the panel is the failure mode**, not the fix. `proficiency 73` was
  rejected in `skills-catalog.md §7` for the same reason.

### 5.4 ⛔ What actually shipped in D178, and what D180 had to add

**Only the first bullet was built, and it did not work.** The overlay drew correctly from the
day it landed; **the button that switched it never changed its label**, because
`_soilButton.Text` was written inside `CycleDetail` — the *routes* button's handler. Pressing
Ground flipped the wash in silence and left the control reading *"Ground: off"*; pressing Routes
afterwards would suddenly relabel it. Joe found it in one sitting of play.

**The second bullet — the sentence on the farm's panel — had never been written at all**, and
neither had anything else in the game that states soil in words. **The wash was the only channel
this feature had**, which is why a broken toggle made the whole slice invisible rather than
merely awkward. D180 adds it, plus the same sentence on any tile the player clicks.

- ⚠️ **The wording departs from this section's own proposal, deliberately.** §5 suggested *"worth
  about a fifth more than average"*; what shipped quotes the share — *"Rich ground — a field here
  reaps 134% of what ordinary ground gives."* **The reason is the sentence directly above it in
  the same panel**, which has said *"a trip brings back 41 food — 62% of what this hut would
  yield in full woodland"* since D112. Two adjacent lines describing the same idea in two
  conventions is worse than either convention. **The `proficiency 73` failure is a bare number
  with no referent**; a share of a stated thing is what this panel already speaks.
- ⚠️ **The third bullet — the brush saying so once per stroke — is still not built.** Named here
  so it is not lost.
- **The bands come from a run** (`PerSiteYieldTests.TheValleysSoilSpreadIsWideEnoughToName`):
  seed 12345 over 9,360 dry tiles gives **p10 70%, median 101%, p90 135%, min 32%, max 165%**, so
  *rich* at 115 and *thin* at 85 name **31% / 44% / 25%** of the valley. **The prediction that
  preceded the probe was wrong** — bilinear interpolation was expected to regress the typical
  tile toward the middle and leave the wash faint everywhere but the region cores, and it does
  not. **The palette therefore needs no retune on amplitude grounds**, which is a colour slice
  that would otherwise have been written on a guess.

---

## 6. Determinism and the state hash

- **Integer only** (D2), including the smoothing.
- **Draw count preserved** (§3.1), so every layout is byte-identical and **only the map golden
  moves**.
- **Soil is already hashed** — `StateHash.MixMap` walks `map.Soil`, so nothing new enters the
  hash and no sparse-hash trick is needed.
- ⚠️ **Two village goldens will move as well**, because a farm's yield changes: any village that
  farms has a different history. **Both, once, last, one stated reason** (D152). The 50-year
  goldens place no farmhouse (D162), so they should *not* move — **and if they do, that is a
  finding, not a nuisance.**

**§4.2a adds hashed state, which is the one thing in this slice that could cost determinism:**

- **Four fields on `Workplace`** — what it sowed this year, the hands that were in the field when
  autumn opened, the tiles it has learned it can bring in **per hand**, and the store walk that
  answer was learned at.
  **Hashed with the workplace, in id order**, exactly as every other workplace field is, and
  **sparsely — silent until a farm has actually sown something**, which is the shape the queue
  rank, the work mode and the store filters all use. *A village with no farmhouse in it must hash
  exactly as it did before this existed*, and that is what keeps the two fifty-year goldens still.
- **⭐ Nothing reads wall-clock, nothing draws from the RNG.** The memory is a pure function of
  what the sim already did, updated once a year on the turn of winter — the same boundary
  `CropSystem` already works on, so there is no new place for the year to be counted from.
- ⚠️ **The seam golden moves** — it is the only village in the suite that plants a farmhouse.
  **The two fifty-year goldens must not**, and if they do the memory has leaked into a village
  with no farm in it, which is a bug rather than a re-base.

---

## 7. What is deliberately NOT here

- **⛔ Gathering is untouched.** It already has per-site yield (D112) and is not broken. Re-opening
  `gather_yield` reopens `MaxHomeToWorkTiles` and the fuel budget together, **and D122 froze
  nineteen people the last time that chain moved — by one tile.**
- **⛔ Soil depletion** (§2.3, `crops-and-orchards.md §7`) stays deferred. **This slice makes soil
  *matter*; depletion makes it *change*.** Two systems, and the second is uninteresting until the
  first exists.
- **⛔ No new refusal.** Poor ground is warned about and never forbidden (D43, D86). Farming bad
  ground on purpose is a decision the player is allowed to make.
- **⛔ `crop_yield_per_tile` and `farm_store_cap` are not re-derived.**

---

## 8. Testing

- **Determinism** — same seed, 200 years, identical state. P0.
- **⭐⭐ The layout does not move** (§3.1) — terrain, river, woodland, forage sites, founding site,
  stone and iron are **byte-identical across the change** for a sample of seeds. **This is the
  guard that says the draw order survived**, and it is the one that would catch the smoothing
  being implemented with an extra draw in it.
- **⭐ Soil is regional, not noise** — the variance *between* well-separated sites is materially
  larger than it is under uniform noise. *Without this the smoothing could do nothing and every
  other guard would still pass.*
- **⭐ Average ground yields exactly today's number** (§4.1) — the locked `crop_yield_per_tile`
  still means what it meant, so nothing downstream moved.
- **⭐⭐ Rich ground beats poor ground, measured over a year** — not at the predicate. D144's rule:
  *a rule tested where it is decided and never where it is used is a rule nobody has tested.*
- **⭐⭐ A distant farm no longer sows what it cannot reap** (§4.2) — the brought-in percentage at
  ten ticks rises materially from the measured **46%**, and **the rot falls**. Assert the year,
  not the step (D167).
- **⭐ The founding stays survivable** (§3.2) — `cold-start.md`'s five ticks re-measured from a
  run, and the twelve-seed arm green. **Damped is not ruined.**
- **Shipped config, not only the fixture** (METHODOLOGY §3).
- **Every guard checked red and counted.**

---

## 8a. ✅ What it measured, once built

| | before | after |
|---|---|---|
| Site-to-site soil spread (p90÷p10) | 134% (per-tile noise) | **182–198%** |
| Founding ground percentile | 99th / 93rd / 91st / 83rd in 4 of 8 seeds | **capped at the reference, every seed** |
| A farm 10 ticks from its store, brought in | **46%** | **96%** |
| …tiles it reaps in ten years | — | **59, against a near farm's 144** |

**Both halves of D58, in the last two rows.** The distant farm stops rotting what it sowed **and
still feeds fewer people** — *distance costs, and distant sites pay better.* A fix that made the
far farm equal to the near one would have deleted the decision.

⚠️ **`AFarmsHarvestFallsOffWithDistanceFromItsStore` had to be re-based**, and went red for the
best possible reason: it was written to *characterise* the bug and said so in as many words. **A
guard that outlives the rule it was written for looks exactly like a regression** (D150).

---

## 9. Definition of Done

1. This spec current and its status line true.
2. **Probe first** (METHODOLOGY §3): measure soil's per-site spread before and after smoothing, and
   the founding-neighbourhood damping, **before any yield reads it.** If regions do not actually
   produce site-to-site variance, the rest of the slice is pointless and better known early.
3. Guards above written and **checked red and counted**.
4. **The layout-unmoved guard green**, which is what licenses the map golden to move alone.
5. `cold-start.md` re-measured from a run.
6. The player can *see* the ground (§5) — *a feature the player cannot reach does not exist* (D103).
7. Goldens re-taken **last**, one commit, one stated reason each (D152).
8. `DESIGN.md §4`, §5 and §6 updated — including **correcting the queue entry's claim about this
   slice's size**, which is how this spec started.

### 9a. The follow-on slice (§4.2a + §4.3, D194)

1. ✅ **The ledger run before anything is built** — the handoff's own instruction, and it is what
   killed the fifth hypothesis before it was written. Kept as `FarmLedgerTests` so the numbers in
   §4.2a can be re-taken rather than trusted.
2. A guard that **a distant farm stops idling**: at ten ticks out its autumn is spent working,
   not resting, and it reaps more than the old cap allowed.
3. A guard that **a thin year never lowers what the farm has already proved** — the high-water
   rule, which is what stops one short-staffed autumn becoming a permanent verdict.
4. A guard that **a well-sited farm is unmoved**, and that the memory can never exceed
   `FieldTilesOneFarmerKeeps`.
5. A guard that **the record is re-reckoned when the walk changes**, so a granary built by the
   fields lets the farm try again.
6. **Both halves of D58 still hold** — `AFarmsHarvestFallsOffWithDistanceFromItsStore` stays
   green on both assertions, or is re-based with a stated reason rather than relaxed.
7. **The determinism guard green**, and the two fifty-year goldens **unmoved**.
8. Every guard **checked red and the reds counted** — ✅ **14 reds across 7 deliberate breaks**,
   and **⭐⭐ the break that turned up ZERO is the one that changed the design**: removing the
   probe changed nothing measurable, so the probe was deleted rather than guarded. *The red check
   is not a formality; it is the only thing that reads your design for you.*
9. The placement warning reachable in the view (§4.3) — *a feature the player cannot reach does
   not exist* (D103).

---

## 10. Open

- **✅ ANSWERED BY THE PROBE (§9.2, run before anything was built):** the region algorithm is
  **value noise at lattice 8**, not smoothing — measured **p90/p10 200%** against smoothing's
  113%, which was *worse than doing nothing*. And **damping is required**, because founding
  ground came out at the 99th, 93rd, 91st and 83rd percentile in four of eight seeds.
- **⚠️ Is a two-to-one spread too much?** p10 82 against p90 164 means a tile worth **46 against
  92** on a reference of 67. That is a big swing, and it is the number most likely to want
  tuning after Joe plays it. **The lever is `soil_quality_min`/`max`, not the algorithm.**
- **Does the founding cap need a floor** (§3.2)? Only the cold-start measurement can say.
- **Does the forester want soil too?** Trees on good ground growing back faster is the obvious
  extension and is **deliberately out of scope** here — `forests-and-gathering.md`'s regrowth is
  where it would live.
