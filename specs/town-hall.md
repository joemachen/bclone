# Spec: The town hall — the monument the village raises to the people who started it

> Status: **✅ SLICE 1 IS BUILT AND GREEN, 2026-08-29** — the trigger, the gift, the tribute, the
> building, and the view that reaches it. **880 passing, 0 failing, 1 skipped of 881.**
> ✅ **PLAYED AND SIGNED OFF BY JOE (2026-08-29): the moment fired at Year 58, the hall was placed
> and built, and the founders' panel reads right — *"looks good!"* — with a 111-year run standing
> in for the 200-year check.**
> ⏸️ **Slices 2–4 (founders tab, collections, knowledge, charts) are not started.**
> Owner: Joe + Claude Code · Pillar: `DESIGN.md §2.1` (people, not a spreadsheet) and **§1's
> generational time as the core loop**. Format per `METHODOLOGY.md §2`.
>
> **⚠️ Check this status line against the suite, not against itself** — five specs in this repo
> claimed "not started" for systems that had shipped (D159).
>
> **⭐ WHAT SLICE 1 ACTUALLY DOES, so nobody re-reads the plan as the state:** the last founder
> dies → a stopping moment naming all four → a Civic button appears with a ★ → the player places
> it → the crew raise it (materials free, work owed) → it stands, is drawn, and clicking it lists
> the founders. **There are no tabs.** `SimWorld.TownHall`, `Villager.Founder`,
> `CivicSystem`, `BuildingKind.TownHall = 11`, `BuildingRow.Civic` / `.Singleton`.
>
> Neighbours: `tech-tree.md §7f` (what it is), **`tech-tree.md §8` (the knowledge screen — one tab
> of this building, and Phase 4's unbuilt slice 3)**, `buildings-plan.md` (the catalogue row),
> `building-placement.md §D38` (**singleton** — this is *the* example of a build-once building),
> `DESIGN.md §5`'s nomads entry (**what this building triggers**), `specs/morale.md §3.1` (the
> distribution view lives here).

---

## 1. Goal

**A building whose entire output is information about yourself** — and the first one the village
raises for a reason that is not production.

It arrives **as a tribute to the founders, on the death of the last of them** (D252, Joe), and what
it becomes afterwards is the village's administrative self-awareness: the collections, the charts,
the knowledge roster, and the door nomads knock on.

> **⛔ IT IS NOT A KNOWLEDGE BUILDING WITH EXTRAS** (D251). Knowledge is roughly a fifth of it.
> A session that builds "the knowledge screen, in a building" has built the wrong thing.

---

## 2. Which pillars, and which non-negotiables

- **§1 generational time as the core loop.** This is the only building in the game triggered by a
  *generation ending*. **A funeral is the only generational moment this game has**, and D252 spends
  it here rather than inventing a threshold.
- **§1 legibility / no unlock the player cannot account for.** The player watched all four founders
  die, one line each in the village log. **Nothing about the arrival is a surprise they could not
  have seen coming** — they have been reading the obituaries for forty years.
- **§1 people, not a spreadsheet.** ⚠️ **This is the non-negotiable most at risk here**, because
  charts and itemised collections are *literally* a spreadsheet. **The tribute framing is the
  defence**: the first thing the building says is four names, and every tab under it is a record of
  what people did. *A collections entry is memory, not machinery* (`tech-tree.md §7f.1`).
- **§1 meditative pace.** The gift **stops** the game (`Moment.Stops`), like the library's — the
  player must place it, and at 4× an unpaused panel slides past unread (D232).

---

## 3. The trigger

> **THE LAST FOUNDER DIES, AND SOMEBODY IS STILL ALIVE.**

- **A founder** is one of the villagers the world is founded with — the four who arrive with the
  cart. **Explicitly marked at founding**, `init`-only, never written again. ⛔ *Not derived from
  `BirthYear`*: D195's rule is that derived state is state you cannot pose, and a tribute that
  cannot name who it is for is a stats screen with a plaque on it.
- **The moment fires on the tick the last living founder stops being alive**, checked after
  `MortalitySystem` has run.
- **⛔ The guard: at least one villager is still alive.** The last founder dying in an empty valley
  is *the village ending*, not the village outgrowing anybody — and D143 says an unattended valley
  is supposed to die out. **A monument raised for a village that no longer exists is a message to a
  corpse.**
- **It fires exactly once, ever.** A flag on the world, hashed sparsely.

### 3.1 When this actually happens, and the cost that was accepted

> **✅ MEASURED 2026-08-29, from a run rather than from the arithmetic:**
> **the shipped config is owed its hall in YEAR 58, with 35 souls alive.** The suite's fixture
> village reaches it in **year 30 with 14 alive** — and *that difference is why exactly two goldens
> moved for slice 1 and the shipped pair did not*: at the fifty-year mark the fixture's founders
> are gone and the shipped config's are not. Guarded by
> `TownHallTests.TheShippedVillageIsGivenItsHallLateAndIntoAVillageThatGrew`, **as properties
> rather than as the number** — one seed is not a trend (D200), so the floor is year 25.

`founder_age` is 20 and `lifespan_years_base` is 67 ± 12, so **the ordinary case is year 35–59** —
comfortably after the library (≈ year 15 via literacy) and well into the mid-game gap D161 names.
The measurement lands at the top of that range.

⚠️ **An unlucky founding fires it early.** Four founders lost to one hard winter in year 6 is a
valid trigger. **That was accepted deliberately** (D252): the building is *for the founders*, so it
arriving when they are gone is correct whether the village is thriving or reeling. The forgiving
alternative — *founders outnumbered by the native-born* — protected the pacing at the cost of the
meaning, and crosses in silence on an arbitrary tick besides.

### 3.2 What it is NOT gated on

- **⛔ Not literacy** (D251, Joe). *"A granary is necessary and cheap"* and will usually come first
  anyway. **Expected, not enforced** — the honest way round.
  - **⭐⭐ AND BUILDING IT PROVED THAT RULING RIGHT BY ACCIDENT (2026-08-29).** A guard written to
    assert *"the hall arrives after the library"* went red with `literacy in year 0`: **the
    unattended shipped village never learns to write at all, in fifty-eight years.** A granary is
    player-placed, nobody places one in an unattended run, so `FirstGranaryTick` stays zero and
    literacy never starts counting. **Had literacy been a prerequisite, that village could never
    have been given a hall.** *Found by measurement, not by argument.*
- **⛔ Not a population threshold, a building count, or any resource.** The village does not buy it.

---

## 4. The gift

**Identical in shape to the library's** (D232), and deliberately so — one gift mechanism, not two.

| | Library | Town hall |
|---|---|---|
| What earns it | Fifteen years of a granary's count becoming writing | The last founder dies |
| What is given | The **materials**; the crew still raise it | The same |
| Who chooses the spot | **The player** (Joe, from play) | The same |
| Stops the game | Yes — a gift must be acted on | Yes |
| How many | **Exactly one**; every further library costs | **Exactly one, ever** — it is a singleton (D38) |

- **Materials free, work still owed.** *"A building that appeared finished would be the only one in
  the game nobody built."*
- **Singleton.** ⛔ The button refuses a second one, and says why. This is the first singleton in
  the game, so **the refusal is a new sentence, not an existing one.**
- ⚠️ **If the player demolishes it**, the gift is not re-offered. *The founders only die once.* The
  building can be **moved** (D229) like any other, which is the answer to "I put it in the wrong
  place" without re-opening the gift.

### 4.1 The tribute — what the moment actually says

**This is the half that is not mechanism, and it is why the building is free.** The moment names
**every founder**: their name, their trade, and how long they lived here. Dead villagers stay in
`world.Villagers`, so the names are recoverable at the moment they are needed.

> *"Wendell was the last of the four who came here with a cart and no roof. He is buried beside
> Mabel, Corin and Ysolde. The village they founded has raised a hall in their name — put it
> wherever you like, and it will cost you nothing."*

⛔ **No characters, nobody hands it over** — the half of SimCity's version worth keeping (D232).

---

## 5. What is inside it — the tabs

**Four, and slice 1 builds none of them.** Named here so the scope of each later slice is settled
in advance rather than argued at the time.

| Tab | Content | Source | Slice |
|---|---|---|---|
| **Founders** | The four, permanently. Names, trades, years lived here, cause of death. **The first entry in the collections and the reason the building exists.** | `world.Villagers`, founder-marked | 2 |
| **Collections** | Every crop, tree, fish, animal, technique, building and **first master** the village has ever met — *"in a collectors' sort of way"* (Joe). **Includes what it has since lost** — *we knew this once*. | Catalogues + what has been seen | 2 |
| **Knowledge** | `tech-tree.md §8`'s roster — which techniques the village has, who holds each, who is the best knower, what is written and where, what is at risk. **This is Phase 4's slice 3.** | `KnowledgeSystem`, `SimWorld.KnowledgeAtRiskNote` | 3 |
| **Charts** | Population, food produced, food consumed, with 1/5/10/20-year lookbacks (`DESIGN.md §4`). ⭐ **D250 made this mean something**: 26% of able-adult ticks are rest, so *how much of my village's time is spare* is a readable line. | A new sampled history | 4 |

**⛔ The collections grant nothing** (`tech-tree.md §7f.1`). The day an entry confers a bonus this
becomes the ratchet §11 exists to prevent. Written down in three places on purpose.

---

## 6. Slices

> Each is mergeable on its own, and **each ends with something the player can see** — the rule
> seven features in this repo have broken (*a sim feature is not done until something in the view
> calls it*).

1. **✅ DONE 2026-08-29 — the trigger, the gift, and the building.** A founder marker; a `CivicSystem` that watches
   for the last founder's death; the moment with the tribute in it; `BuildingKind.TownHall` as a
   catalogue row; the singleton refusal; a build button that appears only when the gift is owed or
   the hall stands; a colour on the map; an inspector row. **No tabs.** Standing in the village, it
   says what it is and who it is for.
2. **Founders + collections.** The first two tabs, and the screen itself.
3. **Knowledge** — Phase 4's slice 3, which now has its front door.
4. **Charts** — needs a sampled history, which does not exist and is the only slice here that adds
   *new* state rather than surfacing existing state. ⚠️ **A per-year history is hashed state**, so
   this one moves goldens.

⏸️ **Explicitly NOT in any of these, named so it is not smuggled in:** nomads (D251 makes the town
hall what triggers them; the arrivals themselves are their own feature and need communal housing),
building decay, a keeper trade, and any effect the building has on the economy. **It produces no
food, no goods and no labour.**

---

## 7. Data model

- `BuildingKind.TownHall = 11` — **appended, never renumbered**, the rule `Goods`, `JobKind` and
  the buildings catalogue all carry.
- A catalogue row: a name, a recipe, work ticks. ⛔ **It has a real price** even though the first
  one is free — a modder or a re-founded village should be able to pay for one, and a row with no
  cost would read as *free and instant* to `Mark` (D108).
- `SimWorld.ATownHallIsOwed` — the gift, hashed sparsely (false mixes nothing), mirroring
  `AFreeLibraryIsOwed` exactly.
- `SimWorld.SaidTheFoundersAreGone` — fires-once, hashed sparsely.
- `Villager.Founder` — `init`-only bool. **⛔ NOT hashed, and that is a deliberate exception worth
  reading twice.** The standing rule is *state the sim reads and the hash cannot see is two runs
  that read identical and are not*. **It does not bite here**, because `Founder` is not evolving
  state: it is set at founding for the same four villagers in every run of a config and is never
  written again. ⭐ **Its only consequence — `SaidTheFoundersAreGone` — IS hashed**, so two runs
  that somehow disagreed about who founded the village would diverge on a hashed latch anyway.
  ⚠️ **Hashing it would mix a byte into every village that has ever existed and move every golden
  for a feature no golden reaches**, which is the mistake the library's hash block records making.

---

## 8. Edge cases & failure modes

| Case | What happens | Why |
|---|---|---|
| Last founder dies, **nobody left alive** | **Nothing.** No moment, no gift. | §3's guard. The village has ended. |
| Last founder dies, only **children** left | The moment fires. | They are the village. It is theirs. |
| All four die in **year 3** | The moment fires. | §3.1 — accepted deliberately. |
| Player never places it | The gift stays owed for ever, the button keeps its ★. | The library does the same. |
| Player demolishes it | Not re-offered; it can be re-built at full price. | The founders only die once. |
| Player tries a **second** one | Refused, with a sentence. | Singleton (D38). |
| A **modded** config with no town-hall row | The gift is never owed and nothing breaks. | Asked of the row, not of the kind — D108's rule. |
| A village founded with **`founding_buildings: true`** | Unchanged — the founders are the four people, not the buildings. | The marker is on the villager. |

**⚠️ The failure mode this spec is most worried about** is the one `DESIGN.md §1` names: **the
building becomes a spreadsheet with a plaque.** The mitigation is ordering — the Founders tab is
built *before* the collections and the charts, so the first thing anybody sees inside is four
people.

---

## 9. How it is tested

- **The trigger fires on the right tick.** A village stepped until the last founder dies; the
  moment exists that tick and not the tick before.
- **⛔ The empty-valley guard, red-checked.** A village where the founders are the last four alive:
  killing them raises **no** moment. *This is the one that will be green for the wrong reason if
  the fixture happens to keep a child alive* — pose it so it cannot.
- **It fires once.** Step a century past it; exactly one moment, one log line.
- **The gift is spent by the first town hall marked**, and the second costs full price.
- **A second town hall is refused** while one stands.
- **The tribute names every founder**, and it still does when they died in different years.
- **Determinism:** same seed, same state hash. **A village that never reaches the trigger is
  byte-identical to before** — the sparse-hash rule (`StateHash`), and the reason five goldens
  should not move for this slice.
- ⚠️ **The red check that matters most:** break the founder marker (mark nobody) and the trigger
  guard must go red. A trigger that fires on an empty predicate is D157's green-and-blind.

---

## 10. Definition of Done (slice 1)

1. ✅ This spec and `tech-tree.md §7f` current and reconciled with what was built.
2. ✅ The row is **data** — a modder can change what a town hall costs (`DESIGN.md §3`), and
   `ModdedBuildingTests` now defines a *"moot hall"* in real JSON with `civic` and `singleton` set,
   at an id listed **sixth of twelve in a descending list** so position cannot pass for id (D218).
3. ◐ **Unit tests passing (880/881, one long-standing skip); determinism green — and TWO GOLDENS
   MOVED, which the original wording of this item forbade.** ⛔ *That wording was wrong, and it is
   corrected here rather than quietly met.* **A village whose founders have all died IS a different
   village**: it is owed a hall, and `Mark` reads that. The honest claim, and the one that was
   checked:
   - **A village that never reaches the trigger is byte-identical.** ⭐ **Measured, not assumed** —
     taking the two new bools back out of `StateHash` returns *both* moved goldens to their old
     values exactly, which is what says the fingerprint's shape moved and the village did not
     (D211's method).
   - **The two that moved are both fixture 50-year arms** (`StockLimitTests.FixtureFiftyYearHash`,
     `SkillTests`' `false` arm). **The shipped pair, the map golden, both farm goldens and all
     three per-site arms held** — because the shipped village's founders are alive at year 50.
     *The goldens that do NOT move are the result, not the leftovers* (D223).
   - Enumerated by **literal** (`grep -rn "[0-9]\{15,\}"`), and each value taken from its **own
     separate run** so no arm got another arm's number.
4. ✅ **Something in the view calls it**, in the same commit: a Civic build button that appears only
   once the founders are gone and carries a ★ while the gift is unspent; a colour and a slightly
   larger footprint on the map; an inspector panel listing the founders by name; and the stopping
   moment. ⛔ *Seven features in this repo have shipped unreachable.*
   - ⚠️ **`SomethingStandsAt` needed the fifth line its own comment asked for in advance** —
     *"a new kind of building is a new line here or it can be built on top of."* Guarded.
   - ⭐ **And the width probe was extended to pose the bar with every category the village will
     ever unlock**, because it was still only ever measuring a young village — D242's blind spot,
     left half-open in the tool built to close it. **Measured: the Civic group costs 83px on a row
     that already wrapped, and the bar's height is unchanged at 189.**
5. ✅ `DESIGN.md` §6 and §7 updated.
6. ✅ **SIGNED OFF AT 111 YEARS BY JOE, 2026-08-29** — *"I just did a 111 year run which is good
   enough. not much will change at year 200 as long as i keep adding granaries and
   foragers/farmers."* ⚠️ **Recorded as his call rather than ticked as 200**, which is what D203
   and D248 did for the QA walks. **The reasoning is his and it is sound**: past the town hall the
   village is in its steady state, and the two hundredth year differs from the hundredth by
   arithmetic rather than by anything new happening.

> **✅✅ AND THE VIEW HAS BEEN LOOKED AT — Joe played it (2026-08-29): the moment fired at Year 58,
> he placed the hall, it was built, and the founders' panel names all four with their years and
> winters.** *"looks good!"* ⭐ **That closes the one thing this spec could not verify** (D160: the
> view has no automated verification of any kind), and it is the second slice in a row whose
> player-facing half was checked by the only test that can check it.

---

## 11. Open, and Joe's to answer

- **What does a town hall cost** once it is not free? A guess is in slice 1 so the row is complete;
  `TECH-EXAMPLE.md` prices it at *80 Stone, 50 Planks, 20 Iron*, which is three goods the village
  cannot make yet. **The shipped number is a placeholder and is marked as one.**
- **Does the hall employ anybody?** Slice 1 says no. A clerk/keeper is the kind of trade that
  arrives with record decay, not before it (the library's keeper is deferred on the same grounds).
