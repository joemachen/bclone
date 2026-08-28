# Spec: Stock limits and laborers — the player says how much, and the spare hands haul

**Decisions:** **D62**, **D63**, D64. Builds on D51 (staffing overrides) and D42 (zoning).
**Slice:** A of Joe's confirmed A → B → C. **Status:** ✅ **BUILT.** The limits, the laborers who
haul when production stops, and the per-building store filter all ship (D128 repaired the limits
after they were found not reaching the forester; D141 added the filter; D144 found it obeyed by
the predicate and ignored by two deposit paths). Guarded by `StockLimitTests` and
`StoreFilterTests`. Marked *"not started"* for a fortnight after it shipped; see D159.
> **⭐ AND TWO MORE LANDINGS THIS SPEC WAS SILENT ABOUT UNTIL 2026-08-28, BOTH ITS OWN SUBJECT:**
> **D238 — a met limit now stops the JOB and keeps the SEAT** (Joe's call over shrinking the
> quota, because proficiency accrues per trade). D216 had wired only the *village is short* arm;
> the *my family is short* arm kept a forager gathering into capped, full stores.
> **D239 — the limits panel measures what the sim decides on.** `HeldFor` fell to `_ => 0` for
> stone, tools and iron, so a village holding 300 stone read *"stop at 100 · have 0"*, and Food
> read `FoodInGranaries()` where every sim decision reads `FoodTheVillageHolds()`.
> ⚠️ *A spec silent on its own subject for a day is the mild form of what D159 found.*

---

## 1. Goal

Two halves of one control:

- **The player sets a target stock per good.** *"200 wood, 200 firewood, 2000 food."* When
  the village has that much, the work stops.
- **The hands that frees become laborers** — villagers no workplace currently wants, who
  **haul** rather than stand still.

Either half alone is broken. A limit with nowhere for the spare hand to go is a village
standing still, which is the idle winter (D44, D52) in a new hat. A laborer with no limit
to create them barely ever exists.

---

## 2. Which pillars this serves

- **§2.2 smart labour.** *The player says how much; the sim still says who.* A limit states
  a **goal**, never a person — proximity, household and catchment continue to choose, so
  every *"why is Elias at the stand?"* sentence stays true. **D51 says how many hands; a
  limit says until when.** Two halves of one control, and neither is slotting a named worker
  into a building, which is the line §2.2 actually holds.
- **§0's core loop (D62).** *"Maintaining the production pipeline while scaling the village."*
  This is the control that makes a pipeline a thing you can maintain rather than watch.
- **§1.1 legibility.** *"Nobody is cutting wood because you asked for 200 and the village has
  214"* is a complete answer to a question the game currently cannot answer at all.
- **§2.3 systemic pressure.** Every shortage becomes traceable to a number the player typed.

---

## 3. The architecture, in one line

> **`VillageEconomy` derives the floor. The player sets the ceiling.**

That split is what keeps D16 intact. The economy goes on deriving what the village must
produce **not to die** — `ForagersToFeedEveryone`, `WoodcuttersWanted`'s winter burn — and
the limit governs everything **above** it. Nothing derived is deleted or overridden; a limit
is a cap applied after the derivation, not instead of it.

**A limit set below the floor is allowed and warned about**, never silently obeyed and never
rejected. That is D43's pattern for a building site that is merely far: the village says
*"you have asked for 40 food; twenty people need 1,900 to see the winter"* and then does as
it is told. A game that refuses the player's number is a game arguing with them; a game that
obeys it silently is one that killed them without saying so.

---

## 4. Data model

```
StockLimits : one nullable int per Goods value
```

- **Null is the default and must stay the default.** Same argument as `StaffingOverride`
  (D51): a game that opens with six numbers to manage is the spreadsheet game §1.2 deletes,
  whatever the numbers say. Null means *"let the village decide"* — exactly today's derived
  behaviour, byte for byte (§8.2).
- **Null and zero are different states, and both are hashed.** Null is no opinion; zero is
  *"stop making this, I mean it"*. Conflating them lets a determinism test pass across a real
  divergence — D51 records this exact trap.
- **Player intent, therefore sim state:** hashed, deterministic, part of the seed contract,
  exactly as zones and staffing overrides are.
- **Enumerated over the goods list, not a fixed six.** Three goods exist today — food, logs,
  firewood. Stone, tools and clothes are on Joe's list and none of them are built (slice B
  and beyond). The control has to describe what the game *has*.

### 4.1 ⚠️ Which stock the limit counts, and this is the subtle bug

**It must read the same supply the quota reads, or the two will disagree.**

`WoodcuttersWanted` deliberately counts **only firewood in the shed** — not firewood
anywhere — because *"firewood stacked in somebody else's home is not supply; there is no
errand that reaches it"*. D29 records what the other reading cost: the village believed
itself stocked, staffed one woodcutter, and froze to extinction with 180 firewood sitting in
homes and an empty shed.

A limit that counts *all firewood everywhere* would re-run that precisely. **So each good's
limit reads the same total its demand function reads**, and the spec says so here rather
than leaving it to be rediscovered. Where a good has no demand function yet, the limit reads
village stores.

### 4.2 ✅ AND THE HARVEST BRUSH READS IT NOW (D212) — it was a box that did nothing

**`StockLimits.Kinds` is the whole goods enum**, so the panel has always shown a row per good.
**The sim read three of them** — food, logs and firewood, each at the workplace that produces it.
**Clearing painted ground read none**, and the brush is the only source of stone and iron the game
has. So *"keep 100 stone"* was a number the village could not see: the player typed it, watched
every seam in the valley come out of the ground, and got no explanation. §1.1's failure, and
D145's *"a control needs one door"* on the good the door was never cut for.

- **`SimWorld.MayTake(goods)` is that door**, and **`MayFell` reads it** rather than naming the
  Logs limit. Byte-identical: `InStores(Goods.Logs)` and `LogsInSheds()` are the same sum over the
  same stores, because `store.Logs` *is* `store[Goods.Logs]`.
- **The tile is skipped, never un-painted** — D127's standing instruction. A seam the village is
  currently full of is *work that is waiting*, and it comes back when the stores are spent down.
- ⛔⛔ **The footprint branch is exempt and there is a test that says so.** `Mark` paints the
  ground a building will stand on (D100); a limit applied there deadlocks the village on its own
  instruction — the site waits on the ground, the ground waits on the limit, and the limit waits
  on nothing at all.
- ⭐ **The refusal writes its own sentence** (METHODOLOGY §4). A laborer standing about because a
  limit is met reads exactly like a laborer with nothing to do — D146's finding, one control over.

**Measured:** a limit of 24 against eight painted seams clears **3** and stops at **36** — one
tile of overshoot, because a tile is spent whole. Capped at zero, the same village still fells its
painted trees (2,662 → 2,650). **No golden moved:** `null` is the default and means *"the player
has not said"*.

⚠️ **`StockLimits.Kinds` still reads `Enum.GetValues<Goods>()` rather than the run's catalogue**,
so a mod-added good cannot be limited. It is a `static readonly` and the array is sized from it,
which is the *"a mutable static was never the fix"* problem `goods-catalog.md` §5 records — the
suite runs ~9.5× parallel with a world per test. Left open deliberately.

### 4.3 ⛔⛔ AND THE FOOD LIMIT NEVER REACHED THE FORAGER (D216) — the same shape again

**One week after §4.2, on the good the whole economy is derived from.** Joe, playing: *"if there
are trees marked for harvest, foragers will gather trees even though the food limit is not yet met
[set to 2000]."*

**Measured: a limit of 2000 and no limit at all produced byte-identical behaviour** — 959 forager
ticks gathering and 871 clearing in both arms. `FoodTheVillageHasRoomFor`, the work gate's only
reader, asked `TargetFoodForTheGranary()` — a **derived** number — and never `StockLimits`.

⭐ **The priority was never wrong, and that is worth stating**, because the symptom reads as a
priority bug and is not one: the harvest branch sits below every job in `Decide` (D87), so a
forager who reaches it has already declined their own work. **What was invisible is *why*.**

✅ **`wanted = StockLimits.For(Goods.Food) ?? TargetFoodForTheGranary()`** — §4's *derived floor,
player ceiling*, finally wired on the work side.

- ⚠️ **The floor half is deliberately untouched.** `TargetFoodForTheGranary` is what the **birth**
  gate reads and stays derived (D153). *The player's number governs work; the derived number
  governs children.*
- ⛔ **Still capped by room**, which is D33 and D76: *a village cannot want more food than it has
  somewhere to put.* Asking for 2000 with granaries for 900 is a request for granaries.
- ⭐ **And the forager says which of the two stopped them** (`WhyTheVillageWantsNoMoreFood`),
  because *raise the limit* and *build a granary* are opposite answers and neither was on screen.

**After: clearing 871 → 220 forager ticks, food held 1077 → 1652.** No golden moved — `null` is
still the default.

---

## 5. Laborers

`JobKind.Laborer` — **a fallback, not a sixth job competing for hands.** A villager who can
work and holds no job is a laborer; nothing is ever *allocated* to it, and it has no quota,
no capacity and no catchment. That is what makes it cheap and what keeps it out of
`LabourAllocator`'s cost-first pass.

**A laborer is a reader, not a `JobKind`** — `Villager.IsLaborer => CanWork && !HasJob`.
Joe's definition is a question the world can already answer, so asking it beats maintaining
a flag: nothing to hash, nothing to set and fail to clear, and no way for the roster and the
reality to disagree. A `JobKind.Laborer` would need a phantom workplace to hang off, and
`LabourAllocator` would then need teaching not to allocate to it — a rule invented purely to
undo a type that should not have existed.

### 5.2 ⛔ And the hauling errands do not exist — measured

> **⚠️ THIS MEASUREMENT PREDATES THE FARM AND IS STALE (D185).** *"Workplace buffers holding
> anything: 0.0% of ticks"* was taken when **no workplace in the game had a store anything ever
> wrote to** — the farm arrived with D161/D162, and its buffer is precisely the case this section
> concluded did not exist. **Do not quote the 0.0% as current.**
>
> **What it concluded is still right, and for a better reason now.** Joe asked whether idle
> laborers should haul farm food to the granary, and answered it himself: *"it should be the
> vendor's job before the laborers job."* It is — `PlanMarketErrand`'s third leg (D171) — and as
> of D185 the village actually **staffs** somebody to run it, which it never did before. So the
> errand exists, and it belongs to a trade rather than to the fallback. **A laborer arm would now
> be a second answer to a question that has one**, which is the shape §5.1 warns about.
>
> ⭐ **Re-measure before building anything here.** If a farm's buffer is still found standing
> full with a marketer available, that is a real gap and laborer hauling becomes real work.

This section proposed two errands. **Both were probed before being built, per METHODOLOGY §3,
and both occur on 0.0% of ticks.** A hundred years, shipped config:

| | measured |
|---|---|
| Workplace buffers holding anything | **0.0% of ticks** (mean 0.00 goods, worst 0) |
| A construction site short of materials | **0.0% of ticks** |
| Able adults holding no job | **24.5% of able-adult-ticks** |

**Why, and it is the existing design working rather than failing.** A producer carries its
own output in the same trip, so a workplace buffer is a pass-through and never a store —
that is D30's *"goods move only by trips people make"* holding exactly. And `WorkTheSite`
has builders fetch their own materials before working, which is D43's *"making them fetch it
is what stops construction being a purchase"*. Neither leaves a gap for a third party.

**So there is no hauling for a laborer to do, and inventing some would be D52's make-work
with a new name** — the failure §5.1 warns about, one paragraph above where it was about to
be committed.

**The laborers are real; the work is not.** A quarter of able-adult-ticks are jobless, so the
people exist and are correctly idle (§5.1, and §0's core loop — a village at rest is the game
working). What they lack is somewhere to be useful, and that is **slice B**: gathering raw
materials off the map, which is the first task on Joe's own list and needs stone to exist.

**Joe's other laborer task — hauling to and from building sites — becomes real later, not
never.** Two things create it: **D64's builder's hut**, which keeps builders at a workplace
so someone else must carry, and **slice C's cold start**, where the player places buildings
before there is anyone to staff them. It is correctly specced and merely early.

### 5.1 The trap this must not fall into

**D52 deleted a winter labour fill that was bounded by *"is any shed not yet full?"*** —
a bound on the shed rather than on the work — and it cost the village a third of its
population for a century. **Laborer hauling must be bounded by errands that exist**, exactly
as `MarketersWanted` is bounded by errands and never by spare hands (D36). A laborer with
nothing to haul is **idle, and that is a correct state**, not a gap to fill. §0's core loop
says winter is a rhythm, not a crisis; a village with its stores full and its people resting
is the game working.

---

## 6. What the player sees

- The limit control lists the goods that exist, each with a number and an empty state that
  reads *"the village decides"*.
- A workplace idle because of a limit says so, in the sentence the selection panel already
  writes: *"Nobody is splitting logs — you asked for 200 firewood and the village has 214."*
  That is the panel's existing job (D57 fixed it for villager states) applied to a new
  reason.
- A limit below the derived floor warns **once, when it is set**, naming the floor. Once when
  set, not once per tick — the D42 rule about the distance warning firing per brush stroke
  rather than per house.

---

## 7. Failure modes to design against

- **Silent starvation.** A limit under the floor that nobody is told about. §3, and it gets a
  test.
- **The right stuff in the wrong place, a fourth time.** §4.1. This project's most repeated
  bug class (D25, D29, D30, D48) and a limit reading the wrong total is a fresh way to have it.
- **Make-work for laborers.** §5.1.
- **Six numbers on the opening screen.** Null default, §4.
- **A limit that fights the derivation instead of sitting above it.** If a limit can reduce
  production below what survival needs without the player having said so, the split in §3 has
  been implemented wrong.
- **Laborers competing for hands they should not have.** A laborer is what is *left*; if
  `LabourAllocator` ever assigns *to* it, a laborer can outbid a woodcutter and the fuel
  chain starves.

---

## 8. How it is tested

Against **both** `VillageFixtures.Village` **and the shipped `data/sim.config.json`** — the
gap between them is where D48, D49 and D50 all lived.

1. **Determinism green.** Limits are hashed; null and zero hash differently.
2. **⭐ With no limits set, the run is byte-identical to today.** The single most important
   guard in the slice: the whole control must be a no-op by default, so a player who never
   opens it plays exactly the village that exists now. State-hash equality over 300 years.
3. **A limit binds.** Set firewood to N and the village's firewood settles at N rather than
   climbing past it, and woodcutters go idle rather than producing into a full store.
4. **A limit below the floor warns and does not silently kill.** The warning fires; the
   village obeys; the death, if it comes, is preceded by a sentence naming the cause.
5. **~~Laborers exist and haul.~~** ⛔ **Cut by §5.2's measurement** — there is no hauling to
   do. `Villager.IsLaborer` is guarded as a reader instead: an able adult with no workplace
   is a laborer, a child never is, and the count reconciles against the roster.
6. **~~The idle winter is now the player's to fill.~~** ⛔ **Deferred to slice B with the
   work.** Raising a wood limit cannot lower winter idleness below the **86% baseline**
   (D59's probe) while the only winter work is the logging the village already does; the
   number moves when laborers can gather. Kept here because it stays the right acceptance
   bar for the slice that earns it.
7. **No new warnings or errors in a clean 300-year playthrough.**

---

## 9. Definition of Done

1. This spec current.
2. Unit tests written and passing; the eight guards in §8 green.
3. Determinism test still green.
4. Manual QA: set a limit, watch a workplace stop, and be able to read *why* off the screen.
5. No new errors in the log during a clean playthrough.
6. `DESIGN.md` §6 and §7 updated.

---

## 10. Deliberately not in this slice

- **Raw gathering** — slice B, needs stone.
- **The builder's hut** (D64) — wants to land near this one, since it is the same
  conversation about who does what work, but it changes what a `Builder` *is* and that is its
  own slice.
- **The cold start** — slice C.
- **Clothes and tools as limited goods** — they are on Joe's example list and neither is
  built. The control enumerates what exists (§4).
