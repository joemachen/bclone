# Spec: Health — diet first, then fever, the herbalist and the infirmary (W6)

> Status: **📏 MEASURED AND 📝 SPECCED (D934, 2026-11-27) ON `slice/w6-health`; ✅ EVERY CALL JOE'S ANSWERED (D935, §10): all as
> recommended, poultry its own group, the meal rule kept for later (§5.7).** 🔨 **Piece 1 on `slice/w6-health`: ✅ step 1 (D938), the five groups on the goods rows.** Measured with `tools/harness/ZzDiet.cs` (§4), on copies of Joe's saves and on 21 unattended valleys.
> Found on the way, and fixed on this slice as its own commit (D933): **every save Joe had was refused by `main`** (§4.1).
>
> Phase 6, W6 of the workplan (`systems-complete.md §5.1`). Its blocker is answered (§4.1 there; D520). It comes before W8,
> drought, whose answer reads health.
>
> Neighbours: `morale.md` (contentment — a life; health is the body, and they share no cause, D797), `food-chain.md §3`
> (what a meal is worth, and the plate, D522–D523), `food-catalog.md §4` (food variety feeds health), `cemetery.md` (learned
> at the first death — the precedent for learning herbalism at the first illness), `batch-trade.md` (the row the remedy is
> made on), `forests-and-gathering.md` (the gatherer's sites, where herbs grow), `tech-tree.md §9.7` (herbalism, tacit).

---

## 1. Why

DESIGN's own opening sentence is a health story: *"old Mabel trained her granddaughter as herbalist before the fever took
her, and that's the only reason the town survived the plague year."* Today nobody in Kith is ever ill. People die of three
things — hunger, cold and old age — and what they eat matters only as how much.

D357 placed health's first content: **food groups, and an immunity that a one-food diet erodes over years, said before it
bites.** D797 gave health the varied diet outright when contentment was built: *one cause, one system.*

## 2. What is already decided, and by whom

| Decision | Where | What it fixes here |
|---|---|---|
| Diet is health's first content: food groups (grain, meat, fish, fruit/herbs), an immunity a one-food diet erodes over years, **said on the panel before it bites** | D357 (Joe: *"I completely agree"*) | §5 |
| The survival floor is solved at nutrition 1, the least any food is worth, so it holds for every diet — **health's stated blocker was already answered** | D520, found by the review (D788) | nothing waits |
| **Health and contentment share no cause.** A varied diet is health's, struck from contentment's row; contentment never makes anyone sick or die sooner | D797, D798 (Joe: *"Yes keep both"*) | §3 |
| Contentment refused *work slows* (§1.1's invisible multiplier) and *sickness and shorter lives* (§0.1's death spiral) | D207 (Joe) | §3 — health is held to both |
| Every pressure telegraphed, legible on the map, and answerable — its warning sentence in its spec before the code | Phase 6 gate item 6 (D751), D656 | §5.4, §6.4 |
| A plate is the best food first, then a share of each food worth the same in proportion to the pile | D522 §9 call 4 (Joe: *"yes"*), D523 | §5.6 — ⚠️ it makes a baking village eat only bread |
| Herbalism is **tacit — unwritable**; *health knowledge should be the most fragile thing the village holds* | `tech-tree.md §9.7` (D206's list, Joe's) | §6.3 |
| A building learned at the event that wants it: the cemetery at the first death | D395, D905 (Joe) | §6.3 |
| The fishery, the hunter's lodge and the farm can be built on Day 1 | the code (`SimWorld.WhyNotYet`) | a second food is a plan from the start |

## 3. Pillars, non-negotiables — and what would break them

- **§1.1 Legibility.** Every person's health is a word on their card and a sentence that says why (*"has eaten nothing but
  forage for two years"*). Every illness and every death from one names its cause. ⛔ **No hidden number**: there is no
  immunity percentage anywhere the player cannot read, and no effect that is not a visible state.
- **§1.2 Meditative pace.** The answer to a one-food diet is a *second food*, which is planning, not babysitting. Nothing
  here asks the player to click on a sick person.
- **§1.4 People, not spreadsheets.** A diet is per person — a household that eats from a fisher's arms eats differently from
  the one next door. The epitaph is a person's: *"Mabel died of fever at 68, in her bed. She had eaten nothing but forage for
  six years, and nobody tended her."*
- **§1.6 Slow and traceable.** Seasons, not ticks: a diet is read at the season's turn, like contentment.
- ⛔ **D207's two refusals, held here too:**
  - **Work never slows.** A run-down person works at full pace. A sick person is *in bed* — visibly absent, a state on the map
    and the card, never a percentage off someone's work. That is the line §1.1 draws: an absence you can see, not a
    multiplier you cannot.
  - **No death spiral.** Contentment refused *sickness and shorter lives* because misery that kills compounds. Health is the
    system that *is* allowed to make people ill — so it carries its own brakes: (a) a diet erodes from **variety, never
    quantity** — a hungry village is not made sicker by being hungry, so hunger and fever cannot feed each other; (b) illness
    is staggered by a seeded draw, so the founders, who all go run down in the same season, never take to their beds on the
    same day; (c) in piece 1 nobody dies of illness at all; (d) in piece 2 fever kills only the run-down, the old and the
    very young, and only those nobody tends — and the herbalist who tends ships *in the same piece* as the fever (§0.1: a
    pressure only once its answer exists).
- **One cause, one system (D797).** Health reads what people eat and who is ill; it reads no contentment cause, and
  contentment reads nothing of health. A death from fever is a death — contentment's *death in the family* hears it as it hears
  every death, the existing consequence, not a shared cause.

## 4. Measured — `tools/harness/ZzDiet.cs` (D933)

What a person ate is not kept anywhere in the sim, so the harness reads it off the meal: it copies every villager's arms and
larder each tick, steps, and for each villager whose hunger fell takes the plate `SimWorld.FoodToTake` would take from the copy.
**Groups: grain (wheat, barley, bread) · fish · meat · forage.**

### 4.1 ⛔ First: every save Joe had was refused by `main`

All 198 newest saves (one per folder) were refused — 70 because they are older formats (refused on purpose since D507), and
**128 with *"This save holds a different list of techniques, which this game no longer has"***. W5 appended seven techniques
inside save format 13 and the loader demanded the count exactly (`SimWorld` load, `knowledge.Count != KnowledgeStates.Length`),
so every save made before W5 — **his newest, Hollowmere at Year 89 among them** — could not be opened. W5's spec meant old saves
to load (*"an old save reads 0"* for `beside_a_master`); the knowledge list was missed. **Fixed on this slice (D933)**: a save
with fewer techniques than the data opens with the newcomers unknown, as the building list has done since D598; one with more is
still refused. With it, all 128 open and run a year clean.

### 4.2 Joe's villages — the newest save of each, five years on

Of the 128 that open, **32 are lived-in** (Year 2 or later, five people or more); the other 96 are founding-day saves, which an
unattended harness starves (D447: that is the harness, not the design).

| | villages | what they eat (share of every unit) | per person-year: the top food's share |
|---|---|---|---|
| **Young, one food** (Year 2–7, 5–10 people) | 7 | **forage 97–100 %** | 90–100 % in nearly every person-year |
| **Two foods** (Year 6–17; fish and forage, one forage and grain) | 9 | about half and half (Ashbourne stone-ridge 85 % forage) | 50–75 % |
| **Three or four foods** (Year 13–89, 12–83 people) | 16 | no group above 55 %; the big villages 20–40 % each | under 50 % in most |

- **Every village starts on one food and stays on it for years.** Read across every save he kept that opens (322 of 547, at load):
  the second food arrived between **Year 1–4** (Thornfield, otter-spruce), **3–5**, **3–6**, **4–7**, **5–9**, **~8**, and
  **16–17** (Ashbourne, stone-ridge: twelve people on forage alone for sixteen years). Six young villages were still on forage
  alone at their newest save (Year 4, 4, 6, 6, 7 and 2).
- **Once a village has a second food, the plate already mixes it.** D523's *a share of each in proportion to the pile* means a
  fish-and-forage village eats close to half of each, every season — almost no person-year in those nine villages reached 90 % one food (Ashbourne stone-ridge, 85 % forage, is the exception).
- **A few households eat one food in a village that eats four — and it is fish, not bread.** Hollowmere honey-harrow: 48
  person-years of 407 at 90 % or more; Elderwood thistle-copse 15 of 230; Ashbourne yarrow-clover 10 of 109. Read by season
  (§4.4): of the plain seasons in his villages of three or four foods, **fish 226, forage 66, grain 11** (bread, one village),
  meat 2 — and only 5–18 % of it came out of the eater's own arms. It is the larder: a household fetches from the store
  nearest it (D523), and by the river that store holds fish.

### 4.4 The rule as specced, run on his saves (five years from each newest save; counted from the load)

Plain at 90 % one group, a season read at six meals or more; plain +1, varied −2; told at 4, run down at 12 (§5.3).

| | villages | told | run down within five years |
|---|---|---|---|
| One food (Year 2–18) | 20 with people in them | everyone, in every one that lived a year | **in 13 of the 20**, 3–6 years after the load (median 3.5); the other 7 died out first, unattended |
| Two foods | 9 | **nobody**, but Ashbourne stone-ridge (8 told — 85 % forage) | nobody |
| Three or four foods | 16 | 1–9 a village in six of them — fish households | **Hollowmere honey-harrow: 4 of 85 people** |
| Unattended harness, a lodge, a fishery and a farm | 15 that lived, 40 years | **nobody** (0.5 % of seasons plain) | nobody |

So at three years the rule leaves every village with two food trades alone, catches every village with one, and in his
biggest village finds four people in fish-only households — the one place it bites that is not the opening. That is a real
story (*the households by the river eat nothing but fish*) and its answer is already in the game: a market stall or a store
nearer them holding something else.

### 4.3 The unattended harness — 21 valleys, 40 years, a lodge, a fishery and a farm raised free

The 15 that lived ate **grain 20 %, fish 24 %, meat 26 %, forage 27 %**, within a point or two in every valley; 95 % of
person-years had all four groups at 10 % or more, and none was one food. The six that died early ate forage 85–100 %.
**So a diet rule at 90 % one food never touches a village with two food trades, and touches every village with one.**

## 5. Piece 1 — the diet (its own playable, mergeable step)

### 5.1 Food groups — a column on the good

`GoodRow.FoodGroup` (`food_group` in the data file): **`grain`, `meat`, `poultry`, `fish`, `forage`** — five, poultry its own
(Joe, D935: *"poultry is its own group"*). Every edible good names one; an edible good without one is refused at load (the D277
shape: a fact about the good lives on the good, so a modder's new food lands in its group without code). Wheat, barley and bread
are **grain**; forage stands for fruit, nuts and herbs (D357's fourth group); the hunter's and the herd's meat are **meat**.
**Nothing is poultry yet** — the group waits for chickens, pheasants or wildfowl, so today a village has four to vary among.
⭐ A group is not a nutrition: beef and venison, if they ever part, are both *meat* and may be worth different amounts.

### 5.2 What a person ate this season

Each villager keeps **what they ate this season, by group** (four counts, units). Every meal adds to it — `TakeAMealFrom` already
returns the plate. At the season's turn it is read, then cleared. A season is **plain** when one group made **90 % or more** of
it (§4: no mixed village comes near it; every one-food village is at 97–100 %). A season with fewer than a few meals (a birth, an
arrival) is not read.

### 5.3 Run down — the immunity, as a count of seasons

`Villager.PlainSeasons` — how many plain seasons this person has behind them:

- a **plain** season adds one;
- a **varied** season takes **two** off (never below nought) — getting better is twice as quick as wearing down, so a village
  that answers it sees its people recover within the year that follows.

| Seasons | The card says | What it does |
|---|---|---|
| 0–3 | *Well* | nothing |
| 4–11 | ***Plain diet*** — *"has eaten nothing but forage for a year. A body wants more than one kind of food."* | nothing yet — **the warning** |
| 12+ | ***Run down*** — *"…for three years, and it's telling. She'll fall ill now and then until she eats fish, meat or grain too."* | may fall ill (§5.5) |

The thresholds are **Joe's call 4** (§10). At *told at 1 year, run down at 3*: by §4, a village that has a fishery or a lodge
going by about Year 3 never sees *Run down*; his slower openings (a second food at Year 5–9) would, for a few years; Ashbourne
stone-ridge would have for thirteen.

### 5.4 ⭐ Said before it bites (gate item 6)

- **The person's card** — the needs row gains a sixth pill beside *Fed · Warm · Housed · Water · Content*: **Well / Plain diet / Run
  down / Sick**, the sentence as its tooltip, the food named (*"nothing but forage"*).
- **The village, at the season's turn when anyone first passes 4**: one log line and one notice — *"Nine people in Fernhollow
  have eaten nothing but forage for a year. In two more they will start falling ill — a fishery, a hunter's lodge or a farm
  would give them something else."* Said again, once, at the turn anyone first goes *Run down*. Never every season.
- **A count on the village panel** (*"2 run down"*), so a glance says whether it is one person or the village.

### 5.5 Falling ill (piece 1: never fatal)

A run-down person falls ill on a day with a seeded chance of **1 in 120** — about once a year, and never the whole village on
one day. **Sick in bed** is a new state: they go home and lie there for **5 days** (a homeless one lies where they
sleep), eat as anyone does, do no work, then get up *Run down* still. Their job is held, as for a rest. A child or an elder falls ill
the same way; a child never works anyway. **In piece 1 nobody dies of illness.** The cost is a few days' work a year for each
run-down person (about 1.4 %), shown as a person in bed and a line in the log: *"Ada has taken to her bed — run down after
three years of nothing but forage."*

### 5.6 The plate — bread first stays (Joe's call 7)

Since D522 a plate takes **the best food first, all of it that is needed**, so a household whose larder holds bread eats bread
alone while it lasts — which, with diet counted, could make a baking village's plenty a plain diet. **Measured (§4.2): it
barely does** — 11 plain seasons of grain in his villages against 226 of fish. **Proposed: leave the plate as it is** (his D522
call 4 stands, no golden moves for it), and revisit only if play shows bread-only households. The alternative, kept for him to
choose: a share of every group first, the best within each (meal and fetch together, D523). ✅ **Joe: bread first stays for now
(D935), on condition that §5.7's rule comes in later.**

### 5.7 ⭐ The meal rule meals will follow — kept, built later (Joe, D935: *"as long as we include the meal rule at some point"*)

Once foods are worth different amounts and diet counts, a plate eats **what does the eater most good**, in two rules a card
can say: **(1) the group they have had least of this season first; (2) within it, the most filling food first** (bread before
wheat). *"Ate fish — she had had none this season."* ⛔ Never a hidden score weighing fullness against variety (§1.1). Meal and
fetch together (D523). **Built at the latest with the first food whose nutrition differs from its group's** (Phase 7's variety,
or the cook's hot meal, whichever comes first); today it would change almost nothing (§4.2) and move every golden.

## 6. Piece 2 — fever, herbs and the herbalist (direction now, numbers measured in the piece)

**Shipped together, because the fever may kill and §0.1 admits a pressure only with its answer.** The numbers below are
starting points, measured before they are typed, as piece 1's are.

### 6.1 Fever

- **Its season is winter.** A sick person in a house can pass it to the people under that roof (a seeded chance a day). A
  run-down person, an elder or a child under five catches it more readily; a well adult rarely.
- **It kills only the run-down, the old and the very young, and only untended** — a chance a day while they lie sick and
  nobody has tended them. A well adult always gets up. `CauseOfDeath.Fever`, its epitaph naming the diet and whether anyone
  tended them.
- **Foretold**: at autumn's turn, a village with run-down people and no herbalist is told — *"Fever comes with the winter, and
  seven people here are run down. A herbalist could see them through it."*

### 6.2 Herbs and the remedy

- **Herbs** — a good, not food, gathered by the herbalist at the **gatherer's sites** (`mutable-terrain.md`: *berries,
  mushrooms, herbs*), spring to autumn as the forager does.
- **Remedy** — made at the herbalist's hut from herbs, on W1's batch row (`batch-trade.md`: the herbalist was named there as
  its next user). Kept at the hut.
- So the year has a shape: **autumn's stock of remedies is the winter's answer**, the way the granary is.

### 6.3 The herbalist and the hut

- **The herbalist's hut** — placed like a building, one herbalist (a keeper's shape, `tavern.md §5.4a`). **Learned at the
  village's first illness** (piece 1's, which is never fatal), as the cemetery is at the first death — so the answer is in the
  bar before fever can kill anyone.
- **Tending** — the herbalist walks to a sick person's home with a remedy and sits with them: a tended person gets up in half
  the time and **never dies of it**. Several sick: nearest first, the run-down and the old before the well.
- **A skill** (`skills-catalog.md` row): a master herbalist tends in half the time. **Herbalism is unwritable** (§9.7): it lives
  in herbalists and their apprentices, and the library cannot keep it.

## 7. Piece 3 — the infirmary (later; its own spec section when piece 2 is played)

A larger house of beds where the sick are carried and one healer tends several at once, and fever stops passing round a home —
*quarantine practice*, `tech-tree.md §9.7`'s CRISIS node, learned from the village's first outbreak (several ill at once).
Specced after piece 2 is played, because what it is for depends on how fever plays.

## 8. Determinism, the hash, saves, goldens

- **Hashed:** `Villager.PlainSeasons`, the season's eaten-by-group counts, the sick state and its days, the illness draw's RNG
  use. Saved the same, sparse (an old save reads 0 everywhere: everyone *Well*, nobody sick).
- **Derived, never hashed** (D335): the village's run-down count, the panel's pill counts.
- **The draw** uses the world's seeded RNG at a fixed point in the tick order (after needs, before mortality), villagers in id
  order — no wall-clock, no float.
- **Goldens will move.** Piece 1 alone: every golden whose village eats one food for 12+ seasons gains illnesses (the founders'
  forage years); the hash moves in every golden because the new counts are hashed. Each move proven by ablation, as
  D522 and D928 did.

## 9. Build order — piece 1, one step a commit, TDD, red-checked

1. `food_group` on the goods rows; refused at load without one. *(no golden moves)*
2. The season's plate by group, and `PlainSeasons` at the season's turn; saved and hashed. *(goldens move: the hash)*
3. The card's pill and sentence, the village's two warnings, the panel count. *(view; sim sentences tested)*
4. Falling ill: the draw, *sick in bed*, the job held, the log line; the save. *(goldens move)*
5. *(Only if call 7 goes the other way:)* the plate a share of every group first, meal and fetch together. *(goldens move)*
6. Measured on his saves and the harness: who goes run down and when, days in bed a year, survival unmoved (the guard's 25 %).
7. The view: the bed (a stand-in), the state's word. Played by Joe.

## 10. Joe's calls — each with a recommendation — ✅ ANSWERED (D935, 2026-11-27)

**Joe:** *"poultry is its own group … i agree on the ten W6 calls as long as we include the meal rule at some point"*, then
*"yes, merge it"* (call 10 a). So: all ten as recommended, **call 2 his — five groups, poultry its own**; call 7 bread first
stays, with §5.7 kept for later.


1. **Split W6 into three pieces, each played and merged before the next:** (1) the diet — people wear down on one food and fall
   ill now and then, never fatally; (2) fever, herbs and the herbalist, together, because fever can kill; (3) the infirmary.
   **Recommended: yes.**
2. **Four kinds of food:** grain (wheat, barley and bread together), fish, meat, and forage (fruit, nuts, herbs). Two kinds of
   grain don't count as variety. **Recommended: yes, as D357 had it.**
3. **What "only one food" means:** a season where one kind is 90 % or more of what someone ate. His mixed villages never come
   near it; his one-food villages are at 97–100 %. **Recommended: 90 %.**
4. **How long before it bites:** told after a year on one food, and run down — falling ill now and then — after **(a) two
   years, (b) three, or (c) five.** At three, a village with a fishery or lodge by Year 3 never sees it; about half of his
   openings would have, for a few years. **Recommended: (b) three.**
5. **How quickly they get better:** a season eating two or more kinds takes two seasons off the count, so a village that
   answers it sees people well again within about a year. **Recommended: yes.**
6. **What "run down" does in piece 1:** about once a year a run-down person takes to their bed for five days, then gets up,
   still run down. Nobody dies of it until piece 2 brings fever and the herbalist together. **Recommended: yes.**
7. **Bread first stays:** a household eats its bread alone while it lasts (his D522 call). I expected that to wear baking
   villages down; on his saves it barely happens (11 bread-only seasons against 226 fish-only). **Recommended: leave it** —
   change it only if you see bread-only households in play.
8. **Where it shows:** a sixth pill on the person's card (*Well / Plain diet / Run down / Sick*), a count on the village panel,
   and the village told twice — once at a year, once when the first person goes run down. **Recommended: yes.**
9. **Piece 2's direction (numbers later):** fever comes in winter, passes under a roof, kills only the run-down, the old and the
   very young who nobody tends; the herbalist's hut is learned at the first illness; herbs from the gatherer's sites, a remedy
   made at the hut, the herbalist walking to the sick. **Recommended: yes, as the direction to measure.**
10. **The save fix (§4.1):** every save he made before W5 is refused on `main` today. The fix is on this slice. **(a) merge it to
    `main` now, on its own, so his saves open again — recommended;** or (b) let it ride with piece 1.

## 11. For the art lane (D774) — to HANDOFF's 🅿️ ART section, asked and not drawn

Piece 1: **a person in bed** (sick in bed — at home, seen through the roof the way *indoors* is drawn, or a figure lying by the
door), the card's health pill's four words. Piece 2: **the herbalist's hut** (its icon and building), **herbs** and **remedy**
(goods icons), the herbalist *gathering herbs* and *tending* (kneeling at a bedside). Piece 3: **the infirmary**. Stand-ins
until then: the existing house roof, a lying figure from the dead-under-a-cloth pose without the cloth, the gatherer's motion.

## 12. Acceptance (piece 1's Definition of Done)

- Spec current; every step's tests written first and red-checked (the reds counted, a zero written down).
- The four checks (CLAUDE.md), the goldens moved only as §8 says and each proven.
- Measured: on his saves and the harness, the one-food villages warned before anyone goes run down; survival unmoved.
- Played by Joe: a forage-only opening, the card's *Plain diet* at a year, the warning, a fishery built, people recovering.
- DESIGN §6 and §7, HANDOFF's trap, the art lane's list.
