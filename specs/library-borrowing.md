# Spec: The library's borrowing — letters to kin (and the almanac)

> Status: **✅ EVERY CALL ANSWERED (D865, 2026-11-23) — Joe: *"All five calls as recommended. Defaults accepted, with two additions"* (§9, §5.3a, §5.4, §5.4a); 🔨 BUILDING, §7's order.** *(Read "📏 MEASURED AND 📝 SPECCED (D864) — ⏸️ FIVE CALLS JOE'S (§9); NOTHING BUILT" until D865.)* Claimed on
> `slice/library-borrowing` (D863), cut from `main` (`d3fa78a`). Joe's pick (2026-11-22, his brief, kept outside git and
> recorded in D863): *"Go with letters to kin. Cap it: an arrival needs a spare bed in that household, and each household
> gets at most one per generation. We can tune from there. Also do the almanac because why not?"* The instrument is
> `tools/harness/ZzLibrary.cs` (§4), run on the newest save of each of his 185 folders (a copy) and on 100 unattended
> valleys. **⚠️ Two findings change the brief:** letters held only by his two caps starve small villages (§4.4), so call 4
> adds the birth gate's food test; and **the almanac as briefed changes nothing** — every home already has its wood in
> before winter, and nobody dies of cold (§4.5–4.6) — so call 5 recommends not building it now.
>
> Neighbours: `tech-tree.md §7c` (the library: shelves and records; D196, D226), `tavern.md §5.4b` (the evening walked from
> home within `tavern_reach_tiles` in the one cost field — the shape borrowing copies; D843's card count), `nomads.md §4`
> (the other arrival: a hashed clock, a band a quarter of the village), `house-tiers.md §4.4` (D718–D719: the generation,
> 22–25 years apart), `school-and-education.md §11.4` (D753 B: a graduate *reads the year* and goes in from the cold by the
> walk to the fire — the almanac's second half, already built), `morale.md` (W2: the cold-winter cause; a comfort is the
> tavern's), `housing-and-density.md` / D153 (the house is what limits a family; the birth gate's food half, D387).

---

## 1. Why

Joe, playing: *"library should give some kind of benefit… that would make it more beneficial to build a library near
homes."* Today a library keeps the village's technique records on five shelves (`tech-tree.md §7c`, D226). It is the
village's gift after fifteen years of a kept granary, and where it stands makes no difference to anybody: nothing walks to
it, nothing comes of it from day to day.

**What he chose is borrowing.** In spare time a villager walks from home to the library and carries something home — seen
on the map, someone walking there and back with paper in hand. The walk is from the home, in the one cost field (§2.6), as
the tavern's reach is. So a library near homes is borrowed from more, which is the reason to build it there.

**Letters to kin** is what the borrowing is for: a household writes to family in the lowlands, and now and then a letter
is answered and **one named relative comes up the valley to join that household.** The village grows by its own families'
ties, not only by strangers knocking (nomads) and children.

## 2. Pillars and non-negotiables — and what it must not be

- **§1.1 Legibility.** Every letter is a walk you can watch; every answer is a line in the log naming who wrote and who
  came; every refusal says why (no bed, no food to spare). The library's card counts the homes within reach (D843's lesson).
- **§1.4 People, not spreadsheets.** One named person, kin of a named household — *"Tamsin has come up the valley to live
  with the Haywards."*
- **§1.5 Generational time.** One a household **a generation** (Joe's cap): a family's ties to the lowlands are a thing it
  uses once in a lifetime, not a tap.
- **§2.6 One cost field.** *Within reach* is the home's walk, never a ring (D120).
- ⛔ **Not a contentment comfort** (refused on the way, D863): it would compete with the tavern and empty it within a
  generation of graduates. Nothing here touches `contentment_causes` or `contentment_comforts`.
- ⛔ **Not teaching** (D209: *"the library keeps records; it does not teach"*). Proficiency, apprenticeship, the school:
  untouched.
- ⛔ **Not a yield that climbs with a count** (D196's ratchet, D34's building that does everything). A relative is a person
  who eats, works, pairs and dies like anybody.

## 3. What exists today, read off the code (2026-11-23)

- **The library** (`World/Technique.cs`, `Library`): a position, a footprint, `Shelves` (`library_shelves` 5), `Records`,
  `Closed` (fallen in). No id (a card finds it by its index, D396), no seat, no keeper. Learned as a gift when the granary's
  count has been kept `literacy_years` (15) — `KnowledgeSystem.NoticeIfAnybodyLearnedToWrite`; further libraries are bought.
- **The evening at the tavern — the shape to copy** (`BehaviorSystem.TryGoToTheTavern`, `SimWorld.Tavern.cs`): offered
  where a rest at home begins (after the water and the tend), to a `CanWork` adult at home who has not been for
  `tavern_every_days` (5), whose home is within `tavern_reach_tiles` (12) of an open tavern by
  `TravelCost.Cost(home, tavern)` — **`WalkToTheTavern` is the one copy of "within reach"**, read by the offer and the card's
  count (*"N of M homes are within a 12-tile walk"*, D843). An errand, never a rest; the walk home is the ordinary one.
- **The other arrival, the nomads** (`SimWorld.Nomads.cs`): a band knocks on a spring turn at a year hashed from the seed —
  the first at Year 8–12, then every 6–10 years (4–8 once the town hall stands); a quarter of the village, 2–8 people (two
  more with the hall), one or two families of new surnames, settling roofless with `nomads_food_each` (20) food a head;
  `ArrivedAtTick`, `Generation` 1. Never a draw from the RNG.
- **A bed** is the house's: `IsReadyForAChild` refuses a child while the household's living members reach
  `HouseholdCapacity` (`max_household_size` 5 for every kind of house today, D153). Then **the birth gate's food half**:
  the village holds what a birth needs (`FoodTheVillageHolds() ≥ FoodABirthNeeds(Population)`, `birth_food_percent` 100)
  and last year's harvest fed everyone with a child's share to spare (`TheHarvestFeedsOneMore`, D387). Those two are what
  stopped D153's *"the village bred to ninety-two … and thirty-three people starved on the way back down."*
- **A single adult** (18+, unpaired) seeks a partner in another household and a home of their own
  (`HouseholdSystem.FormNewHouseholds`): so a relative who comes does not stay in the house for long.
- **A generation** (`Villager.Generation`, D718–D719): 1 for a founder, a nomad, anyone new to the valley; a child is one
  past the elder parent. Measured: the 3rd generation is first born at Year 20–25, the 4th at 43–51 — **22–25 years apart.**
- **Firewood at home** (`BehaviorSystem.WhatTheHouseholdIsNearlyOutOf`, the fetch): a household fetches firewood **only in
  Fall and Winter**, topping up to `FirewoodWantedFor` (a winter's burn × `winter_buffer_percent` 180) once it falls below
  `fetch_below_share_percent` (50).
- **The cold** (`BehaviorSystem.TrySeekWarmth`, `HearthSystem.ChillTheUnsheltered`): everybody goes in to get warm at
  `seek_shelter_percent` (50) of the way to freezing. ⭐ **A school graduate already "reads the year"** (D753 B(i),
  `school_heed_percent` 80): she weighs the walk to the nearest fire and goes in by it — early from the far wood, later
  beside the village. A day at or past the 50 % line counts toward the cold-winter cause (six days, `morale.md`).
- **There is no cold snap and no first frost** in the sim: winter is one season, and it is cold everywhere outdoors.

## 4. Measured — `tools/harness/ZzLibrary.cs`

### 4.1 The instrument

On the newest save of each of Joe's 185 folders (a copy): **115 open** (70 are older formats, refused), **15 are lived in**
(Year ≥ 10, three or more homes — the rest are one- and two-year starts; `barley-test-with-farm` and `willowdale-elder-yarrow-15` read identically in every arm, one village under two names), **10 of those have a library.** At load it reads
where the library stands from each home and how many homes have a spare bed; then it steps each save **10 years on**,
unattended, once per arm, and reads arrivals, births, deaths by cause, firewood on Fall's and Winter's first days, the cold
days and goings-in of every working adult's winter. **The letters are posed from the test side** (the rule does not exist
yet): at each spring turn a home with a spare bed and a living adult, that qualifies by the arm's rule, receives an
unmarried adult of 18–30 carrying 20 food (the nomads' share), **once a household in the run** (ten years is less than a
generation, so one a generation and one a run are the same thing here). Arms: the reach (12 tiles' walk to a standing
library, or every home as if the library stood beside it), the wait (answered the first spring the home qualifies, or a
hashed 0–6 years later), the birth gate's food half (§3) on or off, and whether a relative's own new home may write again.

Also 100 unattended valleys (seeds 200–299, the nomads harness's opening, a granary and a warehouse at Year 3), fifty
years, letters posed from Year 15 (when the library gift comes) at every home, once a household each 24 years.

⚠️ **The harness is not a player** (D447): it builds no new farm when the village grows, so a famine here is a village that
outgrew food nobody planted for. It says what a rule can do to a village left alone — which is what a player who is not
watching meets — not how it plays.

### 4.2 Where his libraries stand

Of the 86 homes in his 10 villages with a library, the walk to the nearest library:

| ≤ 8 tiles | 9–12 | 13–16 | 17–24 | > 24 |
|---|---|---|---|---|
| 23 | 16 | 19 | 18 | 10 |

**39 of 86 (45 %) are within 12 tiles** — the tavern's reach (D847, *"Keep 12"*). Libraries were placed with no reason to
stand near anyone, and it shows: half the homes are past a tavern's walk. That is the decision this slice gives him.

### 4.3 Spare beds

**65 of the 101 lived-in homes (64 %) have a spare bed** (fewer than five living). Joe's bed cap binds on a third of homes,
so it is a real cap but not the main one.

### 4.4 Letters against the nomads

Ten years on, his 15 lived-in villages (366 people at load):

| Arm | Relatives | Nomads | Births | Starved | People at the end |
|---|---|---|---|---|---|
| Nothing (today) | — | 65 | 104 | **0** | 507 |
| Within 12 tiles, answered at once, his two caps | 64 | 71 | 131 | **27** | 576 |
| Within 12 tiles, answered over 0–6 years, his two caps | 50 | 67 | 116 | **15** | 554 |
| ⭐ Within 12, over 0–6 years, **and the birth gate's food test** | **35** | 66 | 112 | **0** | **553** |
| — the same, a relative's own new home not writing again | 30 | 66 | 113 | 0 | 549 |
| Every home (the library beside each), at once, food test | 150 | 71 | 149 | 76 | 638 |
| Every home, over 0–6 years, food test | 83 | 67 | 113 | 31 | 576 |

- **Held only by his two caps, letters starve small villages.** Larkspur (17 people) takes 6–10 relatives in a few years
  and 14–18 starve; Ravenscar (26) loses 9. Every big village absorbs them. A bed is a room's question; whether the village
  can feed one more is a different one, and D153 learned it the hard way for births.
- **With the birth gate's food test, nobody starves** and letters bring **35 relatives in ten years against 66 nomads** —
  about half the nomads' rate, in the villages that have a library, and more where more homes stand near it. The village
  ends 9 % bigger than it would have.
- **The answer must be spread out.** At once, every home in reach writes the spring the library is built and the kin all
  come together (64 in the first years); over 0–6 years the same homes bring 50, and the village has time to grow its food.
- **A relative's own new home writing again** (the relative pairs, the couple has a home of their own, it writes) adds 5 in
  ten years with the food test. Small; it follows Joe's caps to the letter (a new household is a household).
- **Where the library stands is the lever, and it works**: every home at once brings 150 against 64 for the homes within 12.

### 4.5 The almanac's firewood — already in

On **Winter's first day**, across 1,251 home-winters on his saves: **1,242 (99.3 %) hold a whole winter's burn; none holds
nothing.** On **Fall's** first day 879 of 1,227 (72 %) already do — the rest fetch through the autumn. Over the ten years
**9 home-days** in winter had the fire out, in fifteen villages. **Every home has its wood in before the first frost
today**, so *"lays in firewood earlier"* would change nothing. (W2's own measurement said the same: a fire out *"never seen
in 184,000 adult-seasons"*, `morale.md §13.1`.)

### 4.6 The almanac's cold — nobody dies of it, and the school already does it

Across 3,134 working-adult winters: **0 cold deaths** in ten years. Goings-in to get warm: 1,317 in 2,733 adult-winters
for most people (about one in two winters), 77 in 401 for graduates who read the year. So *"goes in from the cold
sooner"* would save nobody's life. What it would do:

- **It is the school's benefit B(i), already built** (D753): a graduate goes in by the walk to the fire. An almanac that
  did the same would give a household the school's gift by borrowing a book — *"education that falls out of a building"*
  (D209).
- **Its only other effect is on contentment.** Going in sooner means fewer days at the get-warm line, so fewer people hold
  the cold-winter cause — a contentment ease by another door, which is exactly the comfort refused (D863), and it would
  cost work: every early going-in is time not worked.

⚠️ **A side finding, not this slice's** (written to HANDOFF for Joe): **graduates hold the cold-winter cause four times as
often as others** — 79 of 401 adult-winters (20 %) against 132 of 2,733 (4.8 %). Reading the year lets them stay out
toward `school_heed_percent` (80) beside the village, and every day past 50 % counts toward the cause. The school's gift
B(i) is working against W2's cold-winter count.

### 4.7 Unattended valleys

Seeds 200–299, fifty years, letters at every home from Year 15 (the library beside each — the ceiling), answered over
0–6 years, one a household each 24 years:

| Arm | Relatives (valleys) | Starved (valleys) | Dead valleys | Alive at 50 | Nomads |
|---|---|---|---|---|---|
| Nothing (today) | — | 635 (54) | 16 | 1,292 | 1,910 |
| His two caps | 996 (89) | **1,834 (91)** | 16 | 1,290 | 2,100 |
| ⭐ His two caps and the food test | 144 (36) | 749 (54) | 16 | 1,291 | 1,914 |

- **The unattended valley already starves** (635 in 54 valleys, the harness's own — it never plants for growth, D447).
- **With only his two caps, letters nearly triple it** and leave the village no bigger at fifty: a thousand relatives come
  and starve out again.
- **With the food test the gate mostly says no** (144 relatives in 36 valleys) and starvation rises 18 % — the same
  bargain the birth gate already strikes for children in these valleys (D155: *growth arrives and starvation is still a
  minority of deaths*). No valley dies that would not have.

### 4.8 What the numbers are not

- **Ten years is less than a generation**, so the generation cap was never reached on the saves; its length (call 2) is a
  reading of D719, not a measurement here.
- **No real borrowing walk was measured** — the letters were posed at the spring turn. The walk's cost (an adult's spare
  rest, a 12-tile walk there and back every season) is the tavern's evening at a sixth of its rate.
- **15 lived-in villages is a small sample**, and two of them carry the whole starvation signal. The food test is
  recommended on the arithmetic of D153, not on two villages alone.

## 5. The rule, as proposed

### 5.1 Borrowing — the walk (calls 1 and 3)

Offered **where a rest at home begins** (the tavern evening's spot, after the water, the tend and the tavern — a need
before a comfort before an errand of the family's), to a `CanWork` adult **at home** (call 1: any adult), when:

- the household's home is within **`library_reach_tiles` (12)** of a standing, open library by the one cost field —
  **one copy of "within reach"** (`WalkToTheLibrary`), read by the offer and the card, the tavern's shape;
- the household **is writing** (§5.2: a spare bed, its generation open) and has not borrowed for **`letter_every_days`
  (30 — a season)** (call 3);
- nobody of the household is already on the walk.

`WalkingToTheLibrary` → at the library for `library_sit_ticks` (2) → home with **paper in hand** (not a good: never in
`Carried`, so no store or larder can take it — the D513 trap) → at the door the letter is written: the household's
`LettersWritten` + 1. The walk wears the ground like any walk, so a library used for a generation has a path to it.
⛔ Never instead of work (asked only where the day has already said *home*); never a child; never in famine (a need before
an errand: the food fetch and the water come first by order).

### 5.2 Letters to kin — the answer (calls 2, 3 and 4)

A household **is writing** while its home has a spare bed (living < `HouseholdCapacity`) and **no kin has come to it within
`letters_generation_years` (24)** (call 2). Each letter it sends is counted. **The answer comes with the letter numbered
`hash(seed, household, generation) mod letters_answered_within + 1`** — `letters_answered_within` **24** letters, so within
six years of a season's letter, a different letter for every household and generation: **a hash, never a draw** (the nomads'
clock; nothing here touches `SimWorld.Rng`). Then, on the answering letter's arrival home:

1. **A bed** — the household still has one (Joe's cap).
2. **Food to spare** — the birth gate's food half (§3), asked of the village as a child's birth is (**call 4**).
3. Both hold → **kin come** (§5.3). Either fails → the answer **stands, said once**, and kin come at the first season's turn
   both hold (*"A letter came for the Haywards: Tamsin will come when there is a bed for her"* / *"… when the village has
   food to spare."*). Writing stops while an answer stands.

After kin come, the household's `KinCameYear` is set; it writes again `letters_generation_years` later, with a fresh hash.
A couple that moves into a house standing empty is a new family there: the count and the year are cleared (`MoveInTogether`).

### 5.3 Who comes

**One unmarried adult of 18–30** (hashed, as a nomad's age), kin to the household — **a surname of their own from the
lowlands** (the nomads' rule: one no living villager carries, D558's one-surname village answered a second way), a first
name by the nomads' rule, a lifespan and a rhythm by the nomads' hashes, `Generation` 1 and `ArrivedAtTick` set (new to the
valley, like a nomad), carrying `nomads_food_each` (20) into the household's larder (`FoodEverBroughtIn`, the
conservation guard's ledger). They appear at the household's door; the view walks them in from the valley's edge (the
nomads' walk-in, D573 — view only). From there the village does what it already does: they work, they pair, they move out to
a home of their own.

⚠️ **Not a call, said so:** kin come whether the Nomads switch is on or off — the switch keeps strangers out (D570, D572),
and a household writing to its own family is the player's doing by where the library stands. (Joe's word changes it.)
✅ **Accepted (D865); and since D865 the player has a switch of their own for letters** (§5.4a) — so a Nomads-off village
that wants no arrivals at all turns letters off too.

#### 5.3a Kin in the family, and marriage — Joe's addition 1 (D865)

*Joe: "spec states whether kin are recorded as relatives in the family tree and whether they can marry into the host
household."* Read off the code first:

- **There is no family tree of who is related to whom.** A villager carries a `Generation` number (D718, the house tiers'
  count) and a household — no parents, no siblings, no kin links. The *"family tree"* the game speaks of is that count.
- **So kin are recorded as kin of the household, not in a tree:** `Villager.CameByLetterOf` holds the id of the household
  whose letter brought them — saved, hashed sparsely — and the person's card says *"Came up the valley in Year 43, at the
  Haywards' letter — kin of the Haywards."* For the generation count they are **generation 1**, new to the valley, as a
  nomad is: they were not born here, and a child of theirs is generation 2 here. Nothing else reads `CameByLetterOf`.
- **Marriage into the host household: not while they share the house, and nothing else bars it.** The partner rule
  (`HouseholdSystem.FindPartner`) refuses only someone of the same household — the one bar there is, which today is also
  what stops two siblings at home. So kin living with the Haywards cannot pair with a Hayward under that roof; once either
  has moved out to a home of their own, they can, exactly as two siblings who have both left home can today. **No new bar
  is built:** a kinship bar wants the parent links the sim does not keep. ⚠️ Logged as a known gap (HANDOFF ⏸️): marriage
  between kin is not prevented anywhere in the game, letters or no letters.

### 5.4 What the player sees

- **The walk**: someone leaves home, walks to the library, walks back with paper in hand (art lane, §8).
- **The log** — the first letter of a generation, once: *"Hilde Hayward wrote to her cousin in the lowlands. Winter, Year 40."*
  The answer: *"Tamsin Brook has come up the valley to live with the Haywards, at Hilde's letter. Spring, Year 43."* A
  refusal, once: *"A letter came for the Haywards: Tamsin will come when there is a bed for her."*
- **The library's card** (D843's lesson, `SayTheLibrary`): *"Lends paper to 7 of 9 homes within a 12-tile walk — 3 are
  writing to kin."* With none in reach: *"No home is within a 12-tile walk, so nobody comes to borrow. A library draws the
  homes around it: build it near them."*
- **The household's card**: *"Writing to kin in the lowlands — 5 letters so far."* / *"Tamsin came in Year 43, at Hilde's
  letter; kin may come again from Year 67."* / the refusal sentence while an answer stands.
- **The person's card**: *"Came up the valley in Year 43, at the Haywards' letter — kin of the Haywards."*
- ⭐ **Letters waiting on an answer, on the library's card** (Joe's addition, D865): *"3 homes are writing to kin — 14 letters
  out, none answered yet."* And while an answer stands: *"… 1 answer waits on a bed."* / *"… on food to spare."* Counted
  from the households when the card is drawn, never stored (D335).

#### 5.4a Letters on and off — Joe's addition 2 (D865)

*Joe: "add a letters on/off on the library card if cheap, otherwise log it as a known gap for Nomads-off players."*
**It is cheap, so it is built:** a village-wide switch, `SimWorld.LettersEnabled`, the Nomads switch's shape (D570, D572) —
started from `letters_enabled` (true) in the data file, the village's own, saved, **hashed only when off**; a toggle button
on every library's card, *"Letters to kin: on"* / *"off"* (the card's `Marker: ON` precedent). Off: nobody fetches paper,
no letter is counted, and no kin come — an answer already standing waits until it is back on, and the card says *"Letters
to kin are off — nobody writes, and nobody comes."* One switch for the village rather than one a library: a library has no
id (D396), and a family writes to its kin, not to a building. Not on the new-game screen unless Joe asks — the card is
where the library is.

### 5.5 The almanac — not built now (call 5)

Measured to change nothing (§4.5–4.6): the wood is in already, nobody dies of the cold, its *come in sooner* is the
school's B(i) and its only other effect would be a contentment ease. **Recommended: not built in this slice; kept as a
candidate for W8** (drought, then fire and blight), where a *foretold* dry year is a real thing for an almanac to tell a
household — and where D753's B(ii) (*"graduates hear drought sooner"*) already waits. W8's spec weighs the two. With no
almanac, *"one book at a time or both"* is moot: the only book is paper.

## 6. Determinism, the hash, saves, goldens

- **No draw.** The answering letter is `NameHash.Mix(seed, householdId, LetterSalt, generationIndex)`; the relative's
  name, surname, age, lifespan and rhythm are keyed `3_000_000 + household × 64 + generation` (clear of the nomads'
  `1_000_000 + …` keys and every villager id).
- **New state** — on the household: `LettersWritten`, `LastLetterDay`, `KinCameYear`, `AnswerStanding` (none / bed / food);
  on the villager: the two walk states and `CameByLetterOf` (the household whose letter brought them, for the card).
  **Hashed sparsely** (nought when none, `MixSparse`), **saved** (absent when nought) — the save format stays 13 (D806's
  precedent: a new field a format-13 save lacks reads nought).
- **Not hashed**: the library's card count (derived, D335).
- **Goldens**: a golden moves only where a library stands within 12 tiles of a home with a spare bed. The shipped goldens'
  villages place the library gift only if the pose does — checked at the build, and each one that moves is proven by
  ablation (the rule off, the golden back) before it is re-taken.

## 7. Build order — one step a commit, TDD

1. **The walk** — `library_reach_tiles`, `letter_every_days`, `library_sit_ticks`, `letters_enabled` (validated); the switch; `WalkToTheLibrary`, the
   offer, the two states, paper home, `LettersWritten`; the library's card count and its letters-waiting line. Guards: an adult in reach walks there and
   back and the count rises; out of reach, a full house, a closed library, a child — nobody goes; the card counts.
2. **The answer** — `letters_answered_within`, `letters_generation_years`; the hashed letter; the bed and food gates; kin
   come (§5.3); a standing answer said once and kept; the generation clock; cleared on a new family. Guards: an answer on
   its hashed letter; refused by a full house, then by an empty larder, then honoured; never twice in a generation; the same
   seed the same relative; the conservation guard sees the 20 food come in; save and load mid-answer.
3. **The view** — paper in hand (a stand-in until the art lane's), the household and person card lines, the library card's
   switch, the walk-in. The
   probe gains a `library:` line (the card's sentence posed).
4. **Measured again on his saves with the real code** (D855's shape): relatives, nomads, starved, people.
5. **Joe plays it** — his Millbrook or Hollowmere (a library, homes near and far): watch a walk with paper, read the log
   line, a relative arrive; then the merge at his word.

## 8. For the art lane (D774) — to HANDOFF's 🅿️ ART section, as a request

- **Paper in hand** — a villager walking home from the library carrying a letter or a few sheets (the cask's precedent, W3).
- **The library**, if its drawing is a stand-in still — a building people now walk to; nothing new is required of it.
- No new building, good or trade.

## 9. Joe's calls — ✅ answered 2026-11-23 (D865)

Joe: *"All five calls as recommended. Defaults accepted, with two additions: (1) spec states whether kin are recorded
as relatives in the family tree and whether they can marry into the host household; (2) add a letters on/off on the library
card if cheap, otherwise log it as a known gap for Nomads-off players. Also show pending unanswered letters on the card."*
So: 1A, 2A (a relative's own new home may write), 3 as recommended, 4A, 5A; every default below; the additions in §5.3a,
§5.4 and §5.4a.

### The calls as put on 2026-11-23 (D864)

1. **Who can borrow.** **(A) Any adult at home — recommended**; (B) only school graduates (*readers*). A: the library comes
   fifteen years into a village and the school after it, so B leaves the library doing nothing for a decade after its gift,
   and ties two buildings into one benefit. The walk is the cost either way.
2. **"One per generation."** **(A) 24 years from when kin last came to that household — recommended**: D719 measured
   generations 22–25 years apart, a fixed number reads plainly on the card (*"kin may come again from Year 67"*), and a
   couple moving into an empty house starts afresh. (B) until a child born in that house since comes of age: a childless
   household would never write again, and it reads as a rule about children rather than about kin.
   ↳ And a relative's own new home may write at once, like any new household (Joe's cap to the letter; measured +5 in ten
   years). Say if you want it barred.
3. **How often.** **A letter a season (every 30 days) while the household is writing, answered within 24 letters (six
   years) on a hashed letter — recommended.** Measured: answered at once, every home in reach brings kin the spring the
   library opens and small villages starve; spread over 0–6 years the village grows its food in time. Faster (a letter every
   5 days, the tavern's rate) is more walking to watch and the same arrivals.
4. **⭐ New — a third cap: food to spare.** **(A) Kin come only when a child could be born: the village holds a birth's
   food and last year's harvest fed one more — recommended.** (B) Your two caps alone. Measured on your saves: with your two
   caps alone 15 of your people starve in ten years (14 of them Larkspur's, a 17-person village taking 6 relatives); with the
   food test, 0, and letters still bring about half the nomads' number. It is the birth gate's own question, so the card
   says it the same way: *"… when the village has food to spare."*
5. **The almanac.** **(A) Not now — kept for W8, where a foretold dry year is real — recommended.** Measured: every home
   already has a whole winter's wood in on winter's first day (99.3 %); nobody died of cold in 3,134 adult-winters; *"come in
   from the cold sooner"* is the school's *reads the year* (D753) already, and its only other effect would ease the
   cold-winter worry — a contentment comfort by another door. (B) Build it anyway as briefed, knowing it does nothing you
   would see. (C) Something else for it to do — your idea.

**Decided unless you say otherwise:** the reach is the tavern's 12 tiles; kin carry a surname of their own (new blood); kin
come with the Nomads switch off too; the relative is an unmarried adult of 18–30 carrying 20 food.

## 10. Acceptance (Definition of Done)

- The four checks (`CLAUDE.md`), every new guard red-checked and the reds counted.
- §7 step 4's measurement on his saves: relatives about half the nomads' number, nobody starved who would not have.
- Joe plays it and approves; the merge at his word.
- `tech-tree.md §7c` names borrowing; `content-inventory.md` and DESIGN §6 agree; the milestone map gains *"Kin up the
  valley — a library near homes"*.
