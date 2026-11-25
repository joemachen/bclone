# Spec: The cemetery and the gravedigger (W4)

> Status: **📏 MEASURED AND 📝 SPECCED (D891, 2026-11-24) — ⏸️ SEVEN CALLS JOE'S (§10); NOTHING BUILT.** Claimed on
> `slice/w4-cemetery` (D890), cut from `main` (`941082a`); the docs merged into `main` and the branch deleted (D892), re-cut when he answers. The instrument is `tools/harness/ZzCemetery.cs` + `cemetery.py` (§4),
> run on copies of Joe's saves and on 100 unattended valleys.
>
> Phase 6, W4 of the workplan (`systems-complete.md §5.1`). The shape is Joe's (D395); its place in contentment is his (D797).
>
> Neighbours: `morale.md §4.1` (the two causes), `tavern.md §5.4a` (a building with one keeper), `tools-and-the-smith.md §9.1`
> (a gift that unlocks), `house-tiers.md` (a latch set by the village's own history), `gridless.md` / D336 (quarter-tile paint),
> `hud.md §9.2` (D887: a trade has a Professions row as soon as its building can be built), `building-condition.md` (what wears).

---

## 1. Why

A death is the village's commonest story (§1.4), and today it leaves one line in the log. Joe, D35: *"for now a death removes
the person from the village's living concerns and nothing marks the spot"* — a cemetery is *the town's memory made visible*,
*somewhere a name can persist after the person*. `buildings-plan.md` calls it possibly the highest-value building in the game.

## 2. What is already decided, and by whom

| Decision | Where | What it fixes here |
|---|---|---|
| Until a cemetery exists the dead simply go; it wants a place, a catchment and work someone does | D35 | a building, a walk in the one cost field, a trade |
| The first is free on the first death, 4×5, and holds N headstones by its size; that unlocks a cemetery painted at any size; **one gravedigger a cemetery**; every one full at a death is a happiness hit | D395 (Joe) | §5, §6, §7 |
| Kept as he described it | D789, call 6 | — |
| *The village's dead have a place*: one rule, two cases, a data row each — **none at all** once the cemetery can be built and people have died; **every one full**. Before it can be built the dead cost nobody anything | D797 (Joe), `morale.md §4.1` | §7 |
| Contentment does two things only (leaving, a longer wait for a child); never work, never sickness | D207, D798 | §7 |
| A trade has a Professions row as soon as its building can be built | D887 (Joe) | §6.1 |
| Nothing is built on steep ground | D814 (Joe) | §5.2 |

⚠️ **`buildings-plan.md` disagrees with D395 in two places, and D395 is Joe's and newer.** It says the cemetery is *placed* and
*grows without further input* — D395 says the first is placed and the rest are **painted**, so it grows when the player paints.
It says it needs *no labour beyond burial* — D395 gives it **a gravedigger of its own**; how much of a year that takes is call 3.
Its rows are annotated, not reworded.

**Read off the code (2026-11-24):**
- The dead **stay in `SimWorld.Villagers`** with `Alive` false, `DiedAtTick`, `CauseOfDeath`, their name, birthday, generation,
  trade skills, household and partner — and their **`Position`, where they fell**. Every fact a stone needs is already saved.
- The epitaph is a sentence said to the log and not kept; the log is not in the save. So the stone's words are **derived when read**.
- `ContentmentCause.DeathInTheFamily` exists (held 4 seasons); a person is unsettled at **two** causes (`contentment_unsettled_at`).
- Painted ground owned by a building is `ZoneMap`'s work ground. ⚠️ Three rules do **not** read it today and must for a grave:
  `SimWorld.CanBuildAt` (a building may be set on somebody's painted ground), the home-siting, and `RegrowthSystem` (a tree may seed).
- Gifts: `AFreeSmithyIsOwed` — materials free, the work still owed, the spot the player's, exactly one.
- An idle builder's hut is **not staffed** (*nothing marked, nobody building* — D52's make-work); a smith with a cold forge
  **keeps the seat and helps as a laborer** (`TryTidyGround`, `TryHelpWithHarvest`). Both precedents exist; call 3 is which.
- Next free ids: `BuildingKind` 34, `JobKind` 20, `ContentmentCause` 6 and 7. Save format 13.

## 3. Pillars, non-negotiables — and what would break them

- **§1.4 people, not spreadsheets** — the point of the slice: a stone has a name, an age and how they died; a person lies beside
  their partner. ⚠️ **Flagged: a village-wide happiness hit for one family's dead would break it** — measured (§4.4), one unburied
  stranger makes six times as many people unhappy as today and empties villages, and *"why did Otto leave?"* is answered by somebody
  he is not related to. Call 1 recommends the family.
- **§1.2 meditative pace** — ⚠️ **flagged: a popup at every death, a body that rots, a timer or a disease would each break it.**
  None is proposed. One moment ever (the first death); every later death and burial is a log line. The dead wait as long as they
  wait. A full cemetery is said ahead (three graves left), and in played villages comes decades apart (§4.2).
- **§1.1 legibility** — one sentence on the person's card, one on the cemetery's, each naming who and what to do.
- **§0.1** — no death spiral: a famine's dead are not a cause while there is room for them (§7.2), a family with nobody left
  holds nothing, and the gravedigger is stood down when the village is short of food.
- **§1.5 generational time** — *the only object that renders a century in one glance*. Nothing under a stone is ever removed.

## 4. Measured — `tools/harness/ZzCemetery.cs`

### 4.1 The instrument

The newest save in each of Joe's 198 folders (a copy): 70 refused (older formats), 110 with fewer than 12 people, **18 lived-in
villages** (Year 13–84, 12–70 people), read at load and stepped 20 and 60 years on; and seeds 200–299 unattended for 80 years.
Read-only: the causes are added on paper, season by season, over the village's own (the tavern's ease and the couple rule
honoured); nobody is removed, so a leaver's own later seasons drop out and nothing else moves.

⚠️ **The stepped years are a harness, not a player** (D447): his villages left to themselves for 60 years hit famines he would not
(worst: 32 dead in one season). The **played** numbers are the dead already in the saves.

### 4.2 How many die, and when

| | played (the dead in the 18 saves) | his saves 20 years on | unattended valleys |
|---|---|---|---|
| dead | **30, every one of old age**, 55–77 | 93 old age / 111 starved / 68 froze | 1,047 / 1,257 / 993 |
| first death | **Year 24–40** in the 6 villages that have had one; the other 12 (to Year 38) have not | 20–40, median 34 | median Year 30; 10 % in Year 1 |
| a year | Hollowmere (70 people): 12 in 45 years; Hollowmere (56): 11 in 38 — **0.3 a year** | 0.2–0.6 a year at 40–79 people | 0.3–0.6 |
| who is left | an adult of the household or the partner in 28 of 30 | 62 % | 42 % |

- **So the first cemetery comes at Year 24–40**, and in his villages the first dead are the founders: it arrives just before the
  town hall (the last founder's death). An early hard winter brings it early.
- **How long 4×5 lasts at one grave a tile (20 graves):** his two old villages are at 12 and 11 after 45 and 38 years and fill at
  about **Year 100** and **Year 82**; a village steady at 60–80 people fills another 20 in 35–45 years. **Two a tile (40)** was
  filled in 1 of 18 villages in 20 years, by a famine (and in all 18 over 60 unattended years, famines again): in a village that is
  played, the full cemetery D395 asks for would almost never happen.

### 4.3 Where they lie, the walk, and the gravedigger's year

- **10 of the 30 lie at their own door; 20 where they were working or walking**, up to 20 tiles from home (median of those, 8–9).
  ⇒ a burial needs a fetch, or the dead need to be carried home first (call 4).
- A posed 4×5 of clear, flat, unowned ground stands a **median 18 ticks (4½ days) one way** from the homes and from where people
  fall (p90 28–30, worst 43). None of the 1,023 dead of the 60-year run on his saves was unreachable. *(13 of the 100 valleys had no
  home at Year 3 to pose a plot by — they die out and are re-founded over and over, 862 of the valleys' 3,297 dead; every dead person in the
  other 87 was reachable.)*
- **One burial**: dig 8 ticks (2 days), walk there and carry back 36, lay to rest 4 — **48 ticks, 12 days** (p90 68). At 0.3–0.8
  deaths a year that is **3–11 % of a year** (with the walk from home, 5–17 %). The worst famine's 32 dead are three years' digging.
- **Hands to spare:** laborers are 38 % of adult-years on his saves; 47 of 360 village-years have none.

### 4.4 What the two causes would do

| The extra cause, on top of the village's own | his saves, 20 years: adult-seasons unsettled | thinking of leaving | would go | couples waiting longer | 60 years: would go | valleys: would go |
|---|---|---|---|---|---|---|
| none (today) | 2.3 % | 41 | — *(9 really left)* | 3.4 % | — *(17)* | — *(9)* |
| **every adult, for as long as there is no place** | **14.1 %** | 332 | **108** | 18.3 % | 316 | 867 |
| every adult, for a year after each death | 6.1 % | 163 | 14 | 7.5 % | 110 | 170 |
| the dead person's household and partner, a year | 2.8 % | 51 | 0 | 3.5 % | 2 | 0 |
| the same, two years | 2.9 % | 56 | 0 | 3.7 % | 29 | 29 |
| **the same, until they are buried** *(never buried here)* | **3.6 %** | 74 | **8** | 4.7 % | 121 | 161 |

- **Village-wide is not a nudge**: with nothing else wrong, every adult is one worry from unsettled for ever, and 108 of them go in 20 years.
- **The family, until buried**, is felt at once — the year's grief is already one cause, so *a death with no grave leaves that family
  unsettled for the year* — and lost people only where the player built no cemetery for decades (8 in 360 village-years, 121 in 1,080).
  Answered, it costs nobody anything. That is both halves of `morale.md §11` item 4. A year's hold loses nobody: decorative.

## 5. The shape — the cemetery

### 5.1 Learned at the first death, and free

- **Learned when somebody dies.** Derived (*has anybody of this village ever died?* — a count kept where `Kill` runs), so no latch and
  no hash. Before it: *"Nobody has died here yet — the village has no need of a cemetery."* A Lore node, *The cemetery*, says the same.
- **The first death raises one moment that waits to be dismissed** (the gift's shape): *"Mabel Ashford is dead at 64 — the first this
  village has lost. They have cleared a plot for her: put it wherever you like, and it will cost you nothing."* Raised in `Kill` on
  the tick the count goes 0 → 1 (D313's shape: the transition cannot repeat, so no *said it* flag).
- **The gift:** materials free, the work still owed, the player's spot, exactly one. *Spent* is the one new latch (hashed when true).
- **A save from before this slice with dead in it** (6 of his 18) is learned and owed the gift on load, with no moment; the gold *new*
  mark and the owed tint are on the bar. One with nobody dead waits for its first death like any other (call 6).

### 5.2 The building and its ground

- **One building, `BuildingKind.Cemetery` (34): a 1×1 gate, with ground of its own** — the farm's and the forester's mechanism
  (`ZoneMap` work ground), not a second kind of cemetery. One seat (D395).
- **The free first one** is placed as a **4×5 plot**: the ghost shows the twenty tiles and the gate at the middle of one end, all
  twenty must be takeable, and the ground is given when the gate is raised. **It can be painted bigger afterwards** (call 5).
- **Every later one** is a gate (call 5's price) and the brush: any shape, any size.
- **One grave a tile** (call 2), `graves_per_tile` in the data file. Holds = tiles of clear ground. 20 graves in the first.
- **Takes:** grass, not steep, not a seam, not water, not somebody's yard or ground. **Trees on it stay** and hold no grave until felled (a
  wooded churchyard is the player's to keep); the card says so when they are what is missing.
- **Keeps:** nothing is built on it, no home is sited on it, no tree seeds on it, no other brush takes it — **the three holes §2 found
  are closed for cemetery ground.** People walk across it; nothing blocks.
- **A tile with a grave is for ever**: the brush will not take it back, the gate with anybody lying behind it cannot be pulled down or
  moved, and it never wears (`wears_in_years` 0). An empty one is pulled down like anything else.

### 5.3 The stone

- **Where somebody lies:** a free tile next to their partner's grave, else next to one of their household's, else the free tile
  nearest the gate (ties: map order). So families lie together without anybody planning it.
- **A stone costs nothing** — no good is ever owed for a death.
- **Click it: the stone's card.** *"Otto Ashford · 64. Born Summer 12, Year 3; died in Winter of Year 67, of old age. Came into this valley with a
  cart. A master farmer. Beside Mira, his wife of forty years"* — each clause derived from the person's own record, so it is the same in a hundred years.
- **The cemetery's card:** how many lie there and how many graves are free; who keeps it; who waits; the names, newest first, each
  opening its stone.
- **The log:** *"Otto Ashford was buried beside Mira. Spring, Year 68."* One line a burial, under Deaths. And once, when three graves are
  left across every cemetery: *"There is room for three more in the cemetery — paint it bigger, or mark another."*

## 6. The shape — the gravedigger

### 6.1 The trade

- **`JobKind.Gravedigger` (20), one a cemetery** (D395), no skill (W5's to add), no tool. Nearest hand by the one cost field, as any trade.
- **Asked for after the keepers** (a comfort, out of what feeding, warming, building and trade leave), and **stood down while the village is
  short of food** — the tavern keeper's rule. The Professions row appears when the cemetery can be built (D887).
- **With nobody to bury, they keep the seat and help as a laborer** (the smith with a cold forge) — call 3. So the village always has
  *a gravedigger*, by name, and loses at most a trade's hand, never a pair of idle ones.

### 6.2 A burial, step by step

1. **The oldest of the waiting** (by the tick they died, then id) that can be walked to.
2. The gravedigger walks to the plot and **digs** (`grave_dig_ticks` 8), outdoors, in any season; meals and cold break it off as any work, and a dig begun again begins again.
3. Walks to where they lie, takes them up, **carries them back** (no load in the arms besides).
4. **Lays them to rest and fills the grave** (`grave_fill_ticks` 4). `BuriedAtTick` and the grave's tile are set; the stone stands; the line is said.
5. If the gravedigger dies, is called away or freezes on the road, whoever they carried **lies where they were set down** and waits again.

### 6.3 Where the dead wait, and who comes (call 4)

- **They lie where they fell**, under a cloth, drawn small and plain; at their own door they are *laid out at home* and nothing is drawn. **Nothing
  rots, nothing spreads, nobody is harmed by it, and there is no clock** — at the median it is about a week.
- The household's card says it: *"Mabel lies at home, waiting for a grave."*
- **The family comes.** For 30 days from the moment somebody is taken up, each adult of their household, and the partner, walks to the grave once when a
  rest at home would begin (the tavern's errand, asked before it), stands a day, and walks home. Never instead of work, never waited for. **It has no
  effect** — it is there to be seen. Held by `Villager.LastGraveVisitTick` against the burial's tick; hashed sparsely.

### 6.4 When nobody keeps it, or nobody can walk there

- **No gravedigger:** the dead wait; the card: *"Nobody to dig — two lie waiting"*, and why (short of hands / the number is 0 / the village is short of food).
- **Cannot be walked to** (the plot, or where somebody lies): that one is passed over and the next is buried; the card names them.
- **Full:** the dead wait; the card and the placement aid say so.

## 7. Contentment — two rows, one rule

### 7.1 Who holds it (call 1, recommended)

**The dead person's own**: every adult of the household they died in, and their partner, **for as long as they lie unburied with no place to go**. Read once a
season like every cause; a person moved away keeps it only as the partner.

| `contentment_causes` row | Held when one of theirs lies unburied and… | Card |
|---|---|---|
| `DeadInTheOpen` (6), weight 1 | there is **no cemetery standing** (or every one's gravedigger number is set to 0) | *"the Ashfords' dead lie in the open"* |
| `CemeteryFull` (7), weight 1 | one stands and **none has a free grave** | *"there is no room left in the cemetery for Mabel"* |

Never both for the same person.

### 7.2 What never raises it

- **Waiting your turn**: there is a free grave and somebody to dig it — a famine's queue, a long walk, the gravedigger stood down for food.
- A dead person nobody living is kin to; somebody who left the valley (not a death); a village that cannot build one yet.

## 8. Determinism, the hash, saves, goldens

- No draw anywhere; every choice is by tick, id, cost, then map order. Integers only.
- **New saved state:** `Villager.BuriedAtTick`, the grave's tile, `CarryingTheDead` (an id), `LastGraveVisitTick`; `SimWorld`'s gift-spent latch. Each written only when
  set, so **the format stays 13** and an older save opens; each named in `SaveLoadTests`' table. **Hashed sparsely**, so a village nobody has buried in hashes as today.
- **Derived, kept where it changes, never hashed, rebuilt on load** (CLAUDE.md's rule): the waiting list (joined in `Kill`, left at burial), tile → who lies there, the free
  graves of each cemetery.
- **Goldens:** steps 1–4 move none (no golden village marks a cemetery — to be measured, not assumed). **Step 5 moves them once**: an unattended village never answers
  the cause, so families with dead wait longer for a child and some go (§4.4, the valleys). Each proved by ablation. A **posed golden** that buries people is the gate's (item 2).

## 9. Build order — one step a commit, TDD, red-checked

1. **The waiting list and the fields** — who is dead and unburied, kept at `Kill`, rebuilt on load; the fields saved and hashed sparsely. *Tests: the list equals a scan over a
   century with deaths; round trip; two worlds differing only in a burial hash apart.*
2. **Learned at the first death, and the gift** — `WhyNotYet`, the moment, the latch, the Lore node. *Tests: locked before, learned after one death and not after a departure; one moment ever; the gift free once and
   the second one priced; an old save with dead is learned on load.*
3. **The gate and its ground** — the rows, the 4×5 ghost, the brush, capacity, every refusal in §5.2. *Tests: a refusal each (steep, seam, water, yard, another's ground, a tree holds none);
   nothing built, sited, seeded or painted on it; a tile with a grave never given up; `CanPaint` ≡ `Paint`.*
4. **The gravedigger and a burial** — the seat, the quota's place, the states, which grave, the log line, the laborer's fallback. *Tests: oldest first; beside the partner, else the household, else the gate;
   the dead set down where the carrier fell; no seat in a famine; an unreachable one passed over; a year with nobody to bury is a laborer's year; determinism.*
5. **The family at the grave, and the two causes** — measured again with the real code (§4.4's arms, on his saves) before the goldens are re-pinned. *Tests: each row of §7; the queue raises nothing; both sides of the anti-vacuity.*
6. **The view** — the stones, the cloth, the two cards, the bar, the Professions row, the probe lines.
7. **Joe plays it; then the posed golden, the milestone-map row, the merge at his word.**

## 10. Joe's calls — ⏸️ open (D891)

Each in plain words, with what you would see and what I recommend.

1. **Who minds when there is no grave?** (a) ⭐ *The dead person's own family and partner, until they are buried.* A family with a death and no cemetery is unhappy that year, and stays a step from it until you give them a place.
   (b) *Everybody in the village.* Measured, this makes six times as many people unhappy as today and would send 108 away in 20 years across your villages, against 8.
2. **How many graves in the first plot?** (a) ⭐ *Twenty, one a tile* — lasts about two generations in your villages, and you will see it fill. (b) *Forty, two a tile* — it would almost never fill.
3. **What does the gravedigger do the rest of the year?** A death comes about once in one to three years. (a) ⭐ *Keeps the job by name, and helps as a laborer between burials.* (b) *Nobody holds the job until there is somebody to bury.* (c) *Tends the graves all year, nothing else.*
4. **Before and at the burial.** (a) ⭐ *They lie where they fell under a plain cloth (or at home, not drawn) until the gravedigger fetches them — about a week — and each adult of the family walks to the grave once and stands a day. It changes no number.*
   (b) *The same, without the family.* (c) *Nothing drawn; the gravedigger digs and the stone appears.*
5. **Can the first one grow, and what does a later one cost?** (a) ⭐ *Yes, paint it bigger any time, free; a new one elsewhere is a gate at 10 logs and 5 stone and free ground.* (b) *The first stays 4×5 for ever; more graves means a new cemetery.*
6. **Your old villages.** (a) ⭐ *Everybody already dead is buried too, oldest first, once you place it — 12 in your oldest.* (b) *Only those who die from now on.*
7. **A cemetery you build and then set to no gravediggers.** (a) ⭐ *Counts as no place, so the family minds.* (b) *Counts as a place; the dead just lie waiting.*

**As specced unless you say otherwise:** you can never pull down or move one with people in it; a stone costs nothing; families lie side by side; it never wears; the one popup is the first death.

## 11. For the art lane (D774) — to HANDOFF's 🅿️ ART section, asked and not drawn

- **The cemetery's ground** — mown grass with a worn line, its edge (a low hedge or a few posts); the four seasons; snow on it.
- **The gate** (1×1; a lychgate) and its **building icon**; the **gravedigger's** icon and trade colour.
- **Graves:** a fresh mound, an older grassed one, and the stone — a few shapes so a row is not a stamp; does a founder's differ?
- **The dead waiting** — a small plain shape under a cloth. ⚠️ Cozy, never grim: Joe's eyes before it ships.
- **Figures:** the gravedigger with a spade; *digging*, *carrying* (a bier on the shoulder), *burying*; **mourners** standing, heads down.
- **The Lore node's picture.** Until each lands: a stand-in, and the probe says which.

## 12. Acceptance (Definition of Done)

1. This spec current; `morale.md`'s two rows, `buildings-plan.md` and the milestone map agree with it.
2. Every test in §9 written first; every guard red-checked and the reds counted; determinism green.
3. On a copy of his oldest village: the cemetery placed, the 12 buried oldest first, partners side by side, nobody newly unsettled a year on.
4. A village with no cemetery loses people to it over generations; a kept one loses nobody to it (both ways).
5. A clean played run, no `ERROR`; Joe's play; the four verification lines.
