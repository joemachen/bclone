# Spec: Shelter and exposure — cold becomes a place you are standing

**Decision:** D45. **Slice:** `specs/environment-and-seasons.md §11`, slice 3.
**Status:** ✅ **built** (D53). §9 resolved by Joe — **(c) a fire thaws rather than
resets, and (d) the woodpile is the thing that fails.** §8b is the measurement that forced
that question, and it is a *pre-build probe* rather than a description of the shipped
system; read it before changing any number here.

---

## 1. Goal

Replace `HearthSystem`'s household bookkeeping with a **positional** model of cold.
Today a villager freezes because a number attached to their household reached a
threshold. After this they freeze because of **where they have been standing and
whether a fire was burning there** — which is a thing you can watch happen, and a thing
you can act on.

The whole model, from D45:

| Where they are | In danger within |
|---|---|
| Outdoors, no clothing | **15 days** — half a winter |
| Sheltered, no fire burning | **25 days** |
| Sheltered, fire burning | never — and the count **falls back** |

At `ticks_per_day: 4`, that is **60 ticks** and **100 ticks**.

**The last row is the one that moved** (§9.4, Joe): a fire *thaws* rather than resetting
to zero. D45 as first written zeroed the count at any hearth, and §8b measured what that
does — villagers spend **76% of winter standing at a lit fire**, so the count was wiped
constantly and **nobody ever froze**. Thawing keeps the sentence true (a fire is safety;
you never freeze while one is burning) without letting a single warm minute erase a
fortnight in the snow.

**Clothing is not in this slice** (it waits on D19/D39's production tier), so the
village must be survivable without it. That is the acceptance bar, not a nice-to-have.

---

## 2. Which pillars / non-negotiables this serves

- **§1.1 legibility.** The shipped system produces a death out of an invisible counter
  on a household. This one is *watchable*: you can see somebody get cold, go inside,
  and warm up. That is the picture doing the work instead of the log.
- **§1.4 people, not spreadsheets.** "Elias froze because he was cutting timber on the
  far side of the river and his hearth went out while he walked home" is a story. "The
  Bregan household's `TicksCold` exceeded 80" is a row.
- **§2.5 environment with teeth**, and the organising rule that spec states: *a season
  with teeth is one the player prepares for.* Firewood, houses near the work, and later
  clothing are all preparations. A multiplier on a damage number is not.
- **§2.7 unlock by doing**, downstream. Clothing removing the outdoor danger is what
  turns winter into a working season, and it arrives out of a survival mechanic rather
  than a tech menu.

---

## 3. What stays, and it is most of `HearthSystem`

**The burning stays exactly as it is.** Every occupied household burns
`firewood_per_winter_day` per day of winter, and the narration when the last log goes on
the fire stays. That half is the fuel economy D17/D29 are derived from, and
`LabourQuota.WoodcuttersWanted` reads it. **This slice must not move a single number in
the derivation chain** — if `gather_yield`, `stockpile_target` or `firewood_per_split`
change, something has been done wrong.

**The chilling is what gets replaced.** `ChillTheUnheated` asks one question — *does this
villager's household have firewood?* — and it asks it of everyone, everywhere,
identically. That is the household bookkeeping D45 rejects.

---

## 4. Data model

### 4.1 One accumulator, two rates — and this is the load-bearing choice

The obvious implementation is **two counters**, one per row of the table. It is wrong,
and the failure is not subtle: a villager who alternates — fourteen days out, then a
spell indoors by a dead fire, then out again — never trips either counter, and is
immortal in conditions that should kill them. Partial exposure has to *add up* across
the two states or the model has a hole in the middle of its ordinary case.

So: **one integer per villager, two rates of accrual.**

```
threshold      = ticksOutdoors * ticksSheltered      (60 * 100 = 6000)
rate outdoors  = threshold / ticksOutdoors  = 100    per tick
rate sheltered = threshold / ticksSheltered =  60    per tick
rate at a fire =                             -100    per tick, clamped at 0
```

**Thawing is the mirror of the worst exposure, and that is the derivation** — *a day by
the fire undoes a day outdoors.* One sentence, no new config number, and it is the only
rate that makes coming home worth exactly what going out cost. Slower and a hearth is
not really safety; faster and it is the reset again wearing a delay.

`Cold` is clamped to `[0, threshold]` — it cannot go negative, so sitting by a fire all
autumn does not bank credit against the winter.

The product is used rather than a lowest common multiple so that **both rates are exact
integers for any pair of day-counts a config can state** — no gcd, no rounding, no
float (D2). The numbers are large and meaningless on their own, which is fine: nothing
displays them raw — divide by the threshold for anything a person sees.

`Villager.TicksCold` is **renamed to `Cold`** and changes units. It is already hashed; the
hash contract is unchanged in shape, and every recorded seed's outcome changes, which is
expected and is the point of the slice.

### 4.2 What "sheltered" means

**Standing on a building's tile.** Buildings occupy exactly one tile in this game, so
this is a position equality against the homes, the stores and the workplaces — the same
three lists the building selection panel already reconciles.

Consequences, stated because they are the design and not an accident:

- A **woodcutter's hut**, a **market** and a **shed** have roofs. Working at one is
  sheltered.
- A **berry patch** and a **tree stand** do not. Foraging and logging are outdoor work,
  which is exactly the asymmetry clothing later removes.
- **Walking is outdoors**, always. Most of a villager's winter is spent walking, so the
  thaw at a burning hearth is what actually keeps people alive — see §7.

### 4.3 What thaws

**Any occupied home with firewood in it, not only your own.** A neighbour with a fire lit
does not turn a freezing man away, and the alternative encodes a cruelty the player
cannot act on: two houses side by side, one warm, one not, and the sim insists you freeze
in the correct doorway. It also keeps this off the household-accounting road D45 is
getting off.

Workplaces and stores are shelter but have **no fire**, so they slow the count rather
than reversing it. That is the middle row of the table and it is what makes the middle
row exist.

### 4.4 What the model is now FOR — the woodpile is the thing that fails

The other half of §9.4 (d), stated forward rather than discovered backward later.

§8b measured the real constraint: **no household in 120 years went more than 15 days
without firewood**, and the shipped model kills only because its window is 10. So the
question *"why did somebody freeze?"* has exactly one honest answer in this game, and it
is **the woodpile ran out** — not bad luck, not weather, not a threshold nobody could
see. Every death this system produces should be traceable to a fuel chain that failed:
too few woodcutters, a shed too far, a winter longer than the pile.

That is the pressure the player answers, and it is why (b) — moving the day-counts until
a body count comes out right — was refused. **The day-counts describe a human being in
the cold. The death rate is a property of the economy**, and if cold is too rare the
thing to look at is `WoodcuttersWanted`, not this file.

### 4.5 Config

Replaces `freezing_ticks` outright — it is deleted, not deprecated.

| Key | Value | Meaning |
|---|---|---|
| `exposure_days_outdoors` | 15 | Days outdoors, unclothed, before danger |
| `exposure_days_sheltered` | 25 | Days under a roof with no fire before danger |
| `seek_shelter_percent` | *open — §9.3* | Share of the way to death at which work is abandoned |

Zero on either day-count switches that half off, per the `market_capacity: 0` pattern —
so the village can be tested against a world where outdoor cold does not exist, which is
the world clothing eventually creates.

---

## 5. Behaviour: breaking off to get warm

D45 states that villagers *"break off and seek shelter at a stated threshold, but still
have to go out for food and work."* That is a `BehaviorSystem` branch, and its position
in the if-chain is the whole of its meaning:

1. **Eat**, always first (D10 — nobody starves holding dinner).
2. **Get warm**, if `Cold` is past `seek_shelter_percent` of the threshold and the
   nearest fire is reachable.
3. **Work**, as today.

Above hunger would starve people in a warm house; below work would mean the rule never
fires for anyone with a job, which is everyone it is meant to protect.

**It is a walk home, not a teleport to safety**, and the walk is outdoors — so breaking
off early is worth more than breaking off late, which is the decision the threshold is
there to create.

---

## 6. Epitaphs and the D17 promise

`MortalitySystem` compares how far past its own threshold each of hunger and cold has
run, so the two must stay comparable. `Cold >= threshold` replaces
`TicksCold >= FreezingTicks`, and the overrun share is computed the same way against the
new threshold. **No change to the promise**: a death is never ambiguous between cold and
hunger, and the epitaph still names the other affliction when there was one.

The Cold epitaph currently reads *"the household had been without firewood for N days"*,
which stops being true — the household is no longer the unit. It becomes a statement
about **the person**: how long they were out, and where they were when it got bad.

---

## 7. Failure modes to design against

- **Winter becomes unsurvivable without clothing.** The headline risk. Everyone walks,
  walking is outdoors, and 60 ticks is half a winter. Guard: the 300-year acceptance
  runs, and a specific assertion that **cold deaths do not rise** against the shipped
  model. If they do, the numbers are wrong, not the tests.
- **Cold stops killing anyone at all**, which is the opposite failure and the likelier
  one, because a villager who goes home to eat resets constantly. Guard: anti-vacuity
  (D7) — `CauseOfDeath.Cold` must still occur across a long run. **D45 chose 25 days
  precisely so an unheated house can kill inside a 30-day winter; if it never does, the
  model has quietly gone dormant and D17's reversal with it.**
- **Shelter-seeking eats the working day.** If people spend winter shuttling home, the
  idle winter comes back wearing a coat. Guard: measure hand-ticks spent travelling
  home in winter, before and after.
- **The counter carries into spring.** Today `ClearTheCold` thaws everyone at the season
  boundary. **Keep it.** Cold is a winter condition; a villager still dying of February
  in May is neither survivable nor explicable.
- **Two hard things at once.** Clothing is not in this slice and neither is
  shoulder-season heating (§5.3 of the seasons spec). D42's lesson, twice recorded.

---

## 8. Testing

- **Positional, asserted positionally.** Same villager, same tick, same household — one
  standing in a doorway and one in a field accrue at different rates. This is the test
  that proves the model is about place.
- **A fire resets and a roof only slows**, both asserted directly.
- **Alternating exposure adds up** — the hole the two-counter design would have left.
  Fourteen days out, a spell inside by a dead fire, then out again, and they die.
- **Anti-vacuity (D7):** cold deaths still happen in a 300-year run; and they happen for
  a villager whose household had firewood at some point, or the positional model is
  doing nothing the old one did not.
- **The derivation chain is untouched** — `gather_yield`, `stockpile_target`,
  `firewood_per_split` byte-identical before and after.
- **Determinism green**, and the hash must change (the accumulator changed units), so
  the golden-seed expectations move deliberately and are re-recorded once.
- `ShippedConfigTests` gets the new keys, against the real `data/sim.config.json` — the
  fixture-versus-shipped gap has now produced D48, D50 and D49's half-landing.
- **Survivable with no clothing**, on twelve seeds, which is D45's stated condition.

---

## 8b. MEASURED BEFORE IMPLEMENTING — and it blocks the slice

The model above was run against the live village as a probe, without building it: 120
years, the shipped calendar, D45's own numbers and D45's own accumulator.

| | |
|---|---|
| Winter villager-ticks at a lit hearth | **76%** |
| Under a roof with no fire | 14% |
| Outdoors | 9% |
| Longest unbroken spell outdoors, ever | **11 ticks — under 3 days** (D45 kills at 60) |
| Longest spell without reaching a fire, ever | **60 ticks — 15 days** (D45 kills at 100) |
| Worst the accumulator ever reached | **3,600 of 6,000 — 60% of the way to freezing** |
| Villagers who would have frozen in 120 years | **0** |

**D45 as specified never kills anybody.** Not marginally — the peak is 60% and it is
never approached again. `CauseOfDeath.Cold` goes dormant, which is §7's second failure
mode, and it is the one D45's own note explicitly set out to avoid: *"25 days is less
than a 30-day winter, so an unheated house can still kill you within one season... so
fuel stays a live death axis the whole way through."*

**Why the reasoning missed it.** "25 days in an unheated house" assumes somebody *stays*
in the unheated house. They do not — they walk to work, to the granary, to the market,
and the fuel economy is good enough that **no household in 120 years went more than 15
days without firewood.** The binding constraint on freezing is not the cold model at
all; it is `WoodcuttersWanted` keeping the woodpile stocked. The model this replaced killed
because `freezing_ticks` was **10 days** at the fixture, and households do go 15 without
fuel. D45 more than doubles that window, so the deaths stopped — which is what §9.4 (c)
and (d) are the answer to. (`freezing_ticks` no longer exists.)

This is a design decision, not a number to quietly tune — see §9.4.

---

## 9. Questions — resolved (Joe, 2026-07-31)

### 9.1 Does a workplace count as shelter? ✅ **Yes.**

Per §4.2: huts, markets and stores have roofs; berry patches and tree stands do not. It
makes *outdoor work* a meaningful category the moment before clothing exists to fix it,
and it gives the middle row of D45's table something to describe.

### 9.2 Does any home's fire count, or only your own? ✅ **Any occupied home with firewood.**

§4.3 has the argument: the alternative is a cruelty the player cannot act on, and it
walks straight back onto the household-accounting road this slice exists to leave.

### 9.3 What is the seek-shelter threshold? ✅ **50%.**

Halfway to dying is where the shipped system already puts its *"you are cold"* warning,
so the player has been trained on that line and it now means something instead of merely
being said.

### 9.4 Cold stops killing anyone — which way out? ✅ **(c) and (d), Joe.**

§8b is the measurement. Four ways out were put, and they were genuinely different games:

- **(a) Accept it.** Cold becomes a rare emergency rather than a routine killer — you
  only freeze when the woodpile fails badly. *Against:* D17 allowed a second death
  system on the condition it stayed unambiguous and live, and D45's own note says fuel
  stays a live death axis. This quietly repeals that.
- **(b) Keep the model, move the numbers.** 25 days becomes something under 15, because
  15 is the longest any household actually goes without fuel. *Against:* it is tuning to
  hit a death rate, which is what D16 forbids — though the honest version is to
  **derive** the sheltered threshold *from* the fuel economy rather than pick it.
- **(c) A fire thaws rather than resets.** The counter falls at a rate by the hearth
  instead of zeroing, so somebody cold for most of a winter still dies even though they
  came home to eat. *For:* it is the more physical model and it is why the reset is
  doing so much work — 76% of winter is spent standing at a fire. *Against:* it
  contradicts D45's table as written.
- **(d) Make the woodpile the thing that fails.** Leave cold exactly as D45 states and
  accept that it fires only when fuel does — then the pressure to build is on
  `WoodcuttersWanted`, not on the cold model. This is (a) with the intent stated
  forward instead of backward.

**Chosen: (c) and (d) together.** Thawing at a rate makes the model do what D45 says in
the cases D45 was imagining, without picking a death rate; and it keeps the cause of a
freeze where the player can act on it — the woodpile. §4.1 has the thaw rate and its
derivation, §4.4 has what (d) commits this system to.

**(b) was refused explicitly**: it sets a number so that a body count comes out right,
which is precisely the habit this project keeps paying to unlearn. **D45's table is
amended in its last row and nowhere else** — the two day-counts stand exactly as Joe
stated them, because they describe a person in the cold rather than a difficulty dial.

---

## 10. Definition of Done

1. This spec current, §9 answered.
2. `HearthSystem`'s chilling replaced; its burning untouched.
3. Unit tests per §8, including the alternating-exposure case.
4. Determinism green; golden seeds re-recorded once, deliberately.
5. Derivation chain provably unmoved.
6. 300-year acceptance green on twelve seeds, **with no clothing in the game**.
7. Cold still kills somebody, and never ambiguously (D17).
8. A clean playthrough logs no warnings or errors.
9. `DESIGN.md` §6 and §7 updated.
