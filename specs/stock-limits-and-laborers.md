# Spec: Stock limits and laborers — the player says how much, and the spare hands haul

**Decisions:** **D62**, **D63**, D64. Builds on D51 (staffing overrides) and D42 (zoning).
**Slice:** A of Joe's confirmed A → B → C. **Status:** specced, not started.

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

---

## 5. Laborers

`JobKind.Laborer` — **a fallback, not a sixth job competing for hands.** A villager who can
work and holds no job is a laborer; nothing is ever *allocated* to it, and it has no quota,
no capacity and no catchment. That is what makes it cheap and what keeps it out of
`LabourAllocator`'s cost-first pass.

**Slice A gives them hauling only.** Two errands, both of which are widenings of built
machinery rather than new systems:

1. **Workplace buffer → store.** A producer's buffer fills and the goods sit there until the
   producer next walks. A laborer clears it. `VillagerState.HaulingToStore` already exists.
2. **Store → construction site.** `JobKind.Builder` already hauls materials from the nearest
   shed to a site (D38, D43). A laborer does the same errand without holding the builder's
   job.

**Raw gathering is slice B**, and deliberately: it needs stone, and stone is neither a
`Goods` value nor placed by the map generator.

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
5. **Laborers exist and haul.** Set limits low enough to free hands, and measure hauled loads
   per person-tick — **a rate, not a raw aggregate** (D52's lesson, and the handoff's).
6. **Anti-vacuity (D7): laborers must actually appear during the window** the guards watch,
   and a run with no limits set must produce *some* too, or the fallback is watching a case
   that never happens.
7. **The idle winter is now the player's to fill.** With a raised wood limit, winter idleness
   must fall from the measured **86% baseline** (D59's probe, 12.7 spare of 14.7 able adults,
   300 years, both configs). This is the slice's headline acceptance number, and it is the
   same measurement livestock was going to be judged on.
8. **No new warnings or errors in a clean 300-year playthrough.**

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
