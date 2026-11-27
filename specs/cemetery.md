# Spec: The cemetery and the groundskeeper (W4)

> Status: **📏 MEASURED AND 📝 SPECCED (D891), EVERY CALL JOE'S ANSWERED (D905–D906, 2026-11-25). 🔨 ✅ Built and merged into `main` at his word (D917, *"Yes, merge"*, `6d03eab`), ⏳ not pushed; `slice/w4-cemetery` deleted** *(read "🔨 being built on `slice/w4-cemetery`" until D917)* (re-cut from `main` at `e6aca39`, D905; the first cut, D890–D891, merged as docs at D892).
> **Built: step 1 (§9) — who lies unburied, `BurialsFromTick`, `BuriedAtTick` (D907); steps 2–3 — the three rows, learned at the first death,
> the small one free (D908); step 4 — the groundskeeper, a burial, tending (D909); step 5 — the two causes and the family at the grave (D910); step 6 — it wears, and its groundskeeper keeps it (D911); step 7 — the view (D912). 🐞 Joe played it (D915): the idle groundskeeper is a laborer between, §6.1; ✅ played again, *"looks good!"*, and the golden that buries people and the milestone-map row (D916). ✅ Merged into `main` at his word (d917, *"yes, merge"*, `6d03eab`), ⏳ not pushed; `slice/w4-cemetery` deleted.** The instrument is
> `tools/harness/ZzCemetery.cs` + `cemetery.py` (§4), run on copies of Joe's saves and on 100 unattended valleys.
>
> Phase 6, W4 of the workplan (`systems-complete.md §5.1`). The shape is Joe's (D395, D905); its place in contentment is his (D797).
>
> Neighbours: `morale.md §4.1` (the two causes), `tavern.md §5.4a` (a building with one keeper), `tools-and-the-smith.md §9.1`
> (a gift that unlocks), `house-tiers.md` (a latch set by the village's own history), `building-condition.md` (what wears, and who mends),
> `buildings-as-obstacles.md` (walked to, not through), `hud.md §9.2` (D887: a trade has a Professions row as soon as its building can be built).

---

## 1. Why

A death is the village's commonest story (§1.4), and today it leaves one line in the log. Joe, D35: *"for now a death removes
the person from the village's living concerns and nothing marks the spot"* — a cemetery is *the town's memory made visible*,
*somewhere a name can persist after the person*. `buildings-plan.md` calls it possibly the highest-value building in the game.

## 2. What is already decided, and by whom

| Decision | Where | What it fixes here |
|---|---|---|
| Until a cemetery exists the dead simply go; it wants a place, a catchment and work someone does | D35 | a building, a walk in the one cost field, a trade |
| The first is free on the first death; **one keeper a cemetery**; every one full at a death is a happiness hit | D395 (Joe) | §5, §6, §7 |
| ~~The first is 4×5 and unlocks a cemetery **painted** at any size~~ → **every cemetery is placed like a building, in one of three sizes; none is painted, and the first is still free** | D395 → **D905 (Joe: *"there should be small/medium/large size options maybe?"* … *"yes first one is still free"*)** | §5 |
| **The groundskeeper**: the job's name; buries the dead and tends the cemetery all year; no laborer's fallback *(⛔ reversed by Joe's play note — a laborer between, D915: "Laborer between")*. *"The family does not look after the grave. The groundskeeper does. gravedigger = groundskeeper"* | D905 (Joe) | §6 |
| **Who minds:** *"only the dead person's family and partner mind, until a cemetery exists"* | D905 (Joe; call 1 a) | §7 |
| **The dead from before:** *"the dead from before do not get buried too. they are gone."* | D905 (Joe; call 6 b) | §5.1, §8 |
| The dead lie under a cloth until fetched; the family walks to the grave once; a cemetery set to no groundskeepers counts as no place | D905 (Joe: *"The rest stays as you recommended"*; calls 4 a, 7 a) | §6.3, §7 |
| *The village's dead have a place*: one rule, two cases, a data row each — **none at all** once the cemetery can be built and people have died; **every one full**. Before it can be built the dead cost nobody anything | D797 (Joe), `morale.md §4.1` | §7 |
| Contentment does two things only (leaving, a longer wait for a child); never work, never sickness | D207, D798 | §7 |
| A trade has a Professions row as soon as its building can be built | D887 (Joe) | §6.1 |
| Nothing is built on steep ground | D814 (Joe) | §5.2 |

⚠️ **`buildings-plan.md` disagrees with both D395 and D905, and they are Joe's and newer.** It says the cemetery *grows without
further input* and needs *no labour beyond burial*. D905 says it is placed at a size and does not grow, and its groundskeeper tends it all
year. Its rows are annotated, not reworded. **D905 reverses D395's painted cemetery** — annotated there too.

**Read off the code (2026-11-24, re-read 2026-11-25):**
- The dead **stay in `SimWorld.Villagers`** with `Alive` false, `DiedAtTick`, `CauseOfDeath`, their name, birthday, generation,
  trade skills, household and partner — and their **`Position`, where they fell**. Every fact a stone needs is already saved.
- The epitaph is a sentence said to the log and not kept; the log is not in the save. So the stone's words are **derived when read**.
- `ContentmentCause.DeathInTheFamily` exists (held 4 seasons); a person is unsettled at **two** causes (`contentment_unsettled_at`).
- **A building's extent is its catalogue row's** (`ExtentOf`); the largest today is 3×2 (the town hall, the barn). Every standing footprint
  is an obstacle to the cost field — **walked to, not through** (`IObstacles`, D383) — and refuses anything built, sited or seeded on it.
  ⇒ Placed like a building, a cemetery needs none of the painted ground's three missing rules (D891 found them: `CanBuildAt`, home siting
  and `RegrowthSystem` do not read work ground). They stay missing, and are nobody's problem here.
- Wear: `RaisedAtTick` / `RepairedAtTick`, `Condition.Of`, a repair site raised at the season's turn for the builders (`building-condition.md §4`).
- Gifts: `AFreeSmithyIsOwed` — materials free, the work still owed, the spot the player's, exactly one.
- Next free ids: `BuildingKind` 34–36, `JobKind` 20, `ContentmentCause` 6 and 7. Save format 13.

## 3. Pillars, non-negotiables — and what would break them

- **§1.4 people, not spreadsheets** — the point of the slice: a stone has a name, an age and how they died; a person lies beside
  their partner. A village-wide happiness hit for one family's dead would have broken it (§4.4) — Joe chose the family (call 1).
- **§1.2 meditative pace** — ⚠️ **a popup at every death, a body that rots, a timer or a disease would each break it.** None is in.
  One moment ever (the first death); every later death and burial is a log line. The dead wait as long as they wait. A full cemetery is
  said ahead (three graves left), and in played villages comes decades apart (§4.2, §4.5).
- **§1.1 legibility** — one sentence on the person's card, one on the cemetery's, each naming who and what to do.
- **§0.1** — no death spiral: a famine's dead are not a cause while there is room for them (§7.2), a family with nobody left
  holds nothing, and the groundskeeper is stood down when the village is short of food.
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

### 4.3 Where they lie, the walk, and the groundskeeper's year

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
- ⭐ **Measured again on the real code (D910)** — the same 18 save copies, 20 unattended years, no cemetery built (`ZZ_NOW4=1` takes the two rows
  out for the other arm): **adult-seasons unsettled 2.30 % → 3.79 %** (the paper said 3.6 %), people ever thinking of leaving 41 → 103, and
  **really gone 9 → 17** (the paper said 8 more). The paper's arm held.

### 4.5 The three sizes (D905) — how fast a village fills one, and whether it fits

- **Deaths of old age by the size of the village** (his 18 saves stepped 60 years, old age only, so a famine's dead are left out — the
  harness's, not his): 0.16 a year at 10–19 people, 0.19 at 20–29, **0.27 at 30–39**, 0.38–0.40 at 40–59, **0.7–0.9 at 60–99** — about **one a
  year for every hundred living**, which is a lifespan of 67 read backwards. The played history agrees from below: his two oldest villages
  (56 and 70 people now, smaller for most of their lives) lost 0.3 a year. A hard winter or a famine adds to it — up to 32 in one season, unplayed.
- **So a grave a tile fills:** a small (20) in about 75 years at 30–40 people and 25 at 60–80; a medium (42) in about 150 and 50; a large (80) in
  about a century at 60–80.
- **Does it fit?** The same pose as §4.3 (clear, flat, unowned grass, either way round, within 30 tiles of the homes' middle, the cheapest mean walk
  from the lived-in homes), on yesterday's copies: **4×5 in all 18, a median walk 18 ticks; 6×7 in all 18, 23 ticks; 8×10 in 16 of 18, 31 ticks**
  (one at 76) — the other two have no flat clear 8×10 that near. The largest building today is 3×2.

## 5. The shape — the cemetery

### 5.1 Learned at the first death, and free

- **Learned when somebody dies.** Derived (*has anybody of this village ever died?* — a count kept where `Kill` runs, restored on load
  from the dead in the save), so no latch and no hash. Before it: *"Nobody has died here yet — the village has no need of a cemetery."*
  A Lore node, *The cemetery*, says the same.
- **The first death raises one moment that waits to be dismissed** (the gift's shape): *"Mabel Ashford is dead at 64 — the first this
  village has lost. They will need somewhere to lie: a small cemetery is yours to place, and it will cost you nothing."* Raised in `Kill` on
  the tick the count goes 0 → 1 (D313's shape: the transition cannot repeat, so no *said it* flag).
- **The gift:** the free size's materials (call 8 — the small, D906), the work still owed, the player's spot, exactly one. *Spent* is the one
  new latch (hashed when true). The other two sizes are priced from the first.
- **A save from before this slice with dead in it** (6 of his 18) is learned on load and owed the gift, with no moment (call 10, D906 — Joe:
  *"unlock on load as recommended"*); the gold seal is on
  the bar. **Its dead are gone (call 6): never buried, never waited for, held by nobody** — §8's `BurialsFromTick`. One with nobody dead
  waits for its first death like any other.

### 5.2 Three sizes, placed like any building

| Row | Footprint | Graves | Cost (call 8, D906) | Work | Fills, in a village of 30–40 · 60–80 people (§4.5) |
|---|---|---|---|---|---|
| `CemeterySmall` (34) | 4×5 | **20** | free the first time; then 10 logs, 5 stone | 20 | about 75 years · about 25 |
| `CemeteryMedium` (35) | 6×7 | **42** | 20 logs, 10 stone | 40 | about 150 · about 50 |
| `CemeteryLarge` (36) | 8×10 | **80** | 30 logs, 20 stone | 60 | — · about 100 |

- **One row each in the catalogue**, one BUILD button each under Civic, all three earned together at the first death.
- **Placed like a building:** the ghost, the snap, the turn, every refusal any building has (steep, water, a seam, another footprint, a
  yard); its ground cleared by the builders like any site's. **It does not grow** — more graves is another cemetery.
- **Walked to, not through** — it is fenced, like a pasture. The groundskeeper and the family go in by the gate (its door, on a short end).
- **Graves are numbered places inside it** (0 … graves − 1), laid out row by row from the gate in the cemetery's own frame, so a
  turned cemetery's rows turn with it. One a tile; the count is the row's `graves`, which the validator holds to width × height.
- **A cemetery with anybody in it** cannot be pulled down or moved. An empty one is pulled down like anything else.
- **It wears, as one building** (call 9, D906 — Joe: *"Yes, as you put it"*): on a timber building's clock, `wears_in_years` 8 / 12 / 20 on each
  row — **worn** 8 years untended, **overgrown** at 12 (the condition's *failing*), **wild** at 20 (its *ruin*). ⚠️ *Years of mild weather*: a
  winter wears half again (`winter_wear_extra_percent` 50), so in the calendar **worn in its 8th year, overgrown in its 11th, wild in its
  18th** (D911, tested). Drawn so, and the card says the state and
  how far, as a building's does: *Overgrown · 63 % — nobody has kept it for 13 years.*
- **Its groundskeeper restores it, with no logs**: at the season's turn (`TendWhatStands`), a cemetery whose groundskeeper tended it on
  `cemetery_tended_days_a_season` days (10) that season is stamped `RepairedAtTick` — sound again. ⭐ **As built (D911): a groundskeeper keeps it
  all year** — after a day there the day is asked again from the top (hunger, cold, rest first, as anyone's), not the walk home: the tavern
  keeper's day-then-home spent a far cemetery's season on the road (3–4 days kept of 30, measured on a cemetery ten tiles out) and it wore
  regardless; kept all year, 15–24 days a season, 6–8 in winter *(⛔ until D915: now kept the days it needs, after a year's wear, §6.1)*. **No repair site is raised for it**: it is not the
  builders' work. A tended cemetery never shows wear; it shows only where nobody keeps it.
- **It is never closed, and no grave is ever lost**: wild still holds its dead, takes new ones when it has a keeper, and counts as a place (§7).
  Neglect upsets nobody — D797's *no place* stays the only cost.

### 5.3 The stone

- **Where somebody lies:** the free place next to their partner's grave, else next to one of their household's, else the lowest
  free number — so families lie together without anybody planning it. *Next to* is the same row, one either side, then the rows
  in front and behind.
- **A stone costs nothing** — no good is ever owed for a death.
- **Click it: the stone's card.** *"Otto Ashford · 64. Born Summer 12, Year 3; died in Winter of Year 67, of old age. Came into this valley with a
  cart. A master farmer. Beside Mira, his wife of forty years"* — each clause derived from the person's own record, so it is the same in a hundred years.
- **The cemetery's card:** how many lie there and how many graves are free; who keeps it; who waits; the names, newest first, each
  opening its stone.
  > ⭐ **As built (D912):** the card's sentence is the sim's (`SayTheCemetery`: *"Kept by Anna. One lies waiting for a grave."*, *"Nobody
  > keeps it — raise Groundskeepers in Professions."*, *"Full — every grave is taken."*); three numbers (lie here · graves free · waiting); the
  > names newest first with their age, **each name's stone in its tooltip** (`TheStoneSays`: *"Otto Ashford · 64. Died in Winter, Year 67, of
  > old age. Beside Mira."*) — ⚠️ **not yet a stone's own card on a click on the grave**; that and the map's grave picking are left for Joe's
  > play to ask for. Its wear line is a cemetery's (*Kept · Untidy · Overgrown · Wild — a season's keeping makes it sound again*), never the
  > builders'. The household's card says *"Mabel lies at home / where they fell, waiting for a grave."* or *"… is being carried to the
  > cemetery."*
- **The log:** *"Otto Ashford was buried beside Mira. Spring, Year 68."* One line a burial, under Deaths. And once, when three graves are
  left across every cemetery: *"There is room for three more in the cemetery — build another before it fills."*

## 6. The shape — the groundskeeper

### 6.1 The trade

- **`JobKind.Groundskeeper` (20), one a cemetery** (D395), no skill (W5's to add), no tool. Nearest hand by the one cost field, as any trade.
- **Asked for after the keepers** (a comfort, out of what feeding, warming, building and trade leave), and **stood down while the village is
  short of food** — the tavern keeper's rule. The Professions row appears when the cemetery can be built (D887).
- **They keep the cemetery all year** (D905): between burials they are at work inside it — the work state *tending*, at the graves, through the
  working day, broken off for meals and the cold like any work. **No laborer's fallback**: a groundskeeper is a groundskeeper. Tending is what keeps
  it sound (§5.2, call 9). *(⛔ reversed by Joe's play note — a laborer between, D915: "Laborer between")*
- ⭐ **A laborer between (D915, Joe's play note, 2026-11-26).** He played it: *"the cemetery groundskeeper just stands there the whole time doing
  nothing while there are no graves to tend is weird."* Asked, he chose *"Laborer between"*, put to him as: *"When nobody needs burying and nothing is
  worn, they go and haul and build with everyone else. They walk back when someone dies, or when the cemetery starts to look worn, the way builders
  come back to repair."* **As built:**
  - **The seat stays theirs.** They keep the job by name, Professions still counts them, and the labour pass does not take it back.
  - **A cemetery wants keeping** (`SimWorld.TheCemeteryWantsKeeping`, derived from the stamps) once it has taken `cemetery_tend_after_seasons` (4,
    new key) seasons' wear since it was last kept — about a year, well before it looks untidy in its 8th — and only until
    `cemetery_tended_days_a_season` (10) days are kept that season. At the turn it is sound again, and the next year's wear starts.
  - **Otherwise, with nobody to bury, they fall through to a spare hand's day** — the same path a laborer takes, below every trade: the painted
    ground to clear, a load lying about, then a spell at home with its errands (water, the grave, the tavern). ⚠️ *"Haul and build"* in his words
    is loose: in this game a laborer clears and tidies, and building is the builders' trade (`stock-limits-and-laborers.md §5`). Their card says
    *"Nobody to bury, and the cemetery is kept — helping out until it wants keeping again."*
  - **A death calls them back** at their next decision, as before: a burial outranks everything a spare hand does.
  - Before, a fresh cemetery's keeper spent 15–24 days a season standing in it, where 10 days in one season a year now keep it sound.

### 6.2 A burial, step by step

> ⭐ **As built (D909): the fetch comes before the grave.** The spec first had the grave dug, then the fetch; built, the groundskeeper
> fetches first and digs and fills at the cemetery in one action — the same twelve days at the median, one state fewer, and no half-dug
> grave to save. The dead lie by the gate while it is dug.

1. **The oldest of the waiting** (by the tick they died, then id) that nobody carries and the cemetery can be walked to from —
   while it has a free place (`NextToBury`).
2. The groundskeeper walks to where they lie and **takes them up** (`CarryingTheDead`; nothing else in the arms).
3. **Carries them to the cemetery**, then **digs and fills the grave** (`grave_dig_ticks` 8 + `grave_fill_ticks` 4), outdoors, in any season.
4. **Lays them to rest** in the place §5.3 gives: `BuriedAtTick`, the grave (`GraveAt`, `GravePlace`); the line is said, under Deaths:
   *"Otto Ashford was buried beside Mira. Spring, Year 68."* If the cemetery filled meanwhile, they are set down at the gate and wait.
5. If the groundskeeper dies, or no longer keeps a cemetery, whoever they carried **lies where they were set down** and waits again.

A village with two cemeteries: each groundskeeper buries into their own; whichever asks first takes the oldest one waiting (villager id
order within a tick), and a full one's keeper only keeps it.

⚠️ **A founding has no hands to spare for a comfort** (D909, measured in the tests): the quota takes the groundskeeper after the tavern
keeper, out of what food, warmth, building and trade leave, and in a young village that is nobody. **The player's Professions number seats
one** — as it does a tavern keeper — and a number typed there overrides the famine stand-down, which governs only the village's own want
(`LabourQuota.Asked`, the tavern keeper's rule exactly).

### 6.3 Where the dead wait, and who comes (call 4, as recommended)

- **They lie where they fell**, under a cloth, drawn small and plain; at their own door they are *laid out at home* and nothing is drawn. **Nothing
  rots, nothing spreads, nobody is harmed by it, and there is no clock** — at the median it is about a week.
- The household's card says it: *"Mabel lies at home, waiting for a grave."*
- **The family comes.** For 30 days from the burial, each adult of the household, and the partner, walks to the grave once when a
  rest at home would begin (the tavern's errand, asked before it), stands a day, and walks home. Never instead of work, never waited for. **It has no
  effect** — it is there to be seen. The family does not tend the grave; the groundskeeper does (D905). Held by `Villager.LastGraveVisitTick`
  against the burial's tick; hashed sparsely.

### 6.4 When nobody keeps it, or nobody can walk there

- **No groundskeeper:** the dead wait; the card: *"Nobody to keep it — two lie waiting"*, and why (short of hands / the number is 0 / the village is short of food).
- **Cannot be walked to** (the cemetery, or where somebody lies): that one is passed over and the next is buried; the card names them.
- **Full:** the dead wait; the card and the placement aid say so.

## 7. Contentment — two rows, one rule

### 7.1 Who holds it (call 1 a, D905)

> ⭐ **As built (D910):** read once a season over the waiting list (`Unburied`) and the cemeteries (`TheDeadHaveAPlace`), never per tick.
> *A place* is a standing cemetery while the player's Groundskeepers number is not 0 (call 7 a); *room* is one of those with a free grave. A
> cemetery nobody is keeping yet, with the number not typed, is a place — they are waiting their turn. The card says *"their dead lie in the
> open, with nowhere to be buried"* and *"there is no room left in the cemetery for Mabel"*.

**The dead person's own**: every adult of the household they died in, and their partner, **while one of theirs lies unburied and the village
has no place for them**. Read once a season like every cause; a person moved away keeps it only as the partner. The dead from before the
slice hold nothing (§5.1).

| `contentment_causes` row | Held when one of theirs lies unburied and… | Card |
|---|---|---|
| `DeadInTheOpen` (6), weight 1 | there is **no cemetery standing** (or every one's groundskeeper number is set to 0 — call 7 a) | *"the Ashfords' dead lie in the open"* |
| `CemeteryFull` (7), weight 1 | one stands and **none has a free grave** | *"there is no room left in the cemetery for Mabel"* |

Never both for the same person.

### 7.2 What never raises it

- **Waiting your turn**: there is a free grave and a groundskeeper's seat — a famine's queue, a long walk, the groundskeeper stood down for food.
- A dead person nobody living is kin to; somebody who left the valley (not a death); a village that cannot build one yet; the dead from before.
- **A worn, overgrown or wild cemetery** (call 9): neglect upsets nobody — D797's *no place* is the only cost.

## 8. Determinism, the hash, saves, goldens

- No draw anywhere; every choice is by tick, id, cost, then place number. Integers only.
- **`SimWorld.BurialsFromTick`** — the dead who died before it are gone (call 6). **0 in a new village. Written in every save from this slice
  on; a save without it is older, and on load it is set to that save's tick**, so all its dead are gone and its later dead are buried. Hashed
  only when not 0, so a new village hashes as today.
- **New saved state:** `Villager.BuriedAtTick`, the grave (cemetery and place), `CarryingTheDead` (an id), `LastGraveVisitTick`; `SimWorld`'s
  gift-spent latch. Each written only when set, so **the format stays 13** and an older save opens; each named in `SaveLoadTests`' table.
  **Hashed sparsely**, so a village nobody has buried in hashes as today.
- **Derived, kept where it changes, never hashed, rebuilt on load** (CLAUDE.md's rule): the waiting list (joined in `Kill`, left at burial), who lies
  in each place, the free graves of each cemetery, the count of the dead.
- **Goldens:** ~~steps 1–4 move none~~ — *(void — six moved at step 2, D908: the gift is owed at any village's first death, and the owed
  latch is hashed, so every golden village that has had a death moved once, for that reason only, each proven by ablation)*. Steps 3–4 move none
  beyond it (no golden village builds a cemetery — to be measured, not assumed). **Step 5 moves them once**: an unattended village
  never answers the cause, so families with dead wait longer for a child and some go (§4.4, the valleys). Each proved by ablation. A **posed golden** that
  buries people is the gate's (item 2).

## 9. Build order — one step a commit, TDD, red-checked

1. **The waiting list and the fields** — `BurialsFromTick`; who is dead and unburied, kept at `Kill`, rebuilt on load; the fields saved and hashed sparsely.
   *Tests: the list equals a scan over a century with deaths; an old save's dead are gone and its next death waits; round trip; two worlds differing only
   in a burial hash apart, and a new village hashes as before.* — ✅ **built (D907).**
2. **Learned at the first death, and the gift** — `WhyNotYet`, the moment, the latch, the Lore node. ✅ **Built with step 3 (D908).** `DeathsEver`
   (derived, kept in `ADeath` from `Kill`, rebuilt on load); `AFreeCemeteryIsOwed` (saved, hashed sparsely, owed on load of an older save with dead);
   the moment raised once, at the count's 0 → 1; the gift spent in `Mark` on the row with the fewest graves; the Lore node *The cemetery*
   (`TechCondition.SomebodyDied`, never introducing the map). *Tests: locked before, learned after one death and not after a departure; one moment ever;
   the small free once and every other priced; the gift saved and hashed; an older save with dead learned and owed on load with no moment, one without
   waiting for its first death.*
3. **The three rows** — `BuildingKind.CemeterySmall`/`Medium`/`Large` (34–36), `JobKind.Groundskeeper` (20, at all three by `AlsoWorksAt`), the row's
   `graves` column (the validator holds it to width × height), the nine keys in `data/sim.config.json`. ✅ **Built (D908).** The validator's *employs
   nobody* now counts a trade's second buildings — it had passed the marketer's stall only because a stall stores. Placement and its refusals are any
   building's, unchanged. *Tests: each size's extent, graves, seat, trade and wear; the prices are the data's; graves that do not fill the ground refused.*
   ⏩ **Moved to step 4, because they need a grave to exist:** no pulling down with anybody in it; a turned one's places turn with it; walked around and
   entered by the gate.
4. **The groundskeeper, a burial and tending** — ✅ **built (D909).** the seat, the quota's place, the states, which grave, the log line. *Tests: oldest first; beside the partner,
   else the household, else the lowest place; the dead set down where the carrier fell; no seat in a famine; an unreachable one passed over; a year with nobody
   to bury is a year of tending; two cemeteries; determinism.*
5. **The family at the grave, and the two causes** — ✅ **built (D910)**; measured on the real code (§4.4) and six goldens re-pinned, each by ablation — measured again with the real code (§4.4's arms, on his saves) before the goldens are re-pinned. *Tests:
   each row of §7; the queue raises nothing; the dead from before raise nothing; both sides of the anti-vacuity.*
6. **Wear** (call 9, D906) — ✅ **built (D911)** — the row's clock, a season's tending stamps `RepairedAtTick`, no repair site, never closed. *Tests: untended it is worn at 8 years,
   overgrown at 12, wild at 20; tended it stays sound; wild still buries and still counts as a place; no logs are ever owed.*
7. **The view** — ✅ **built (D912)**, stand-ins drawn in code until the art lane's land — the stones, the cloth, the two cards, the three buttons, the Professions row, the probe lines.
8. **Joe plays it; then the posed golden, the milestone-map row, the merge at his word.** ✅ **Played again, *"looks good!"*; the golden and the row built (D916)**: `CemeteryTests.TheGoldenThatBuriesPeople` — the fixture's founding, its first death learning the cemetery, the free small one by the founding, a groundskeeper asked for and one adult pinned, the labour pass on, thirty years with the eldest dying every ten — 6 burials, 6 visits, 27 seasons kept back to sound, the keeper helping out between, 9 alive at Year 31. 🐞 Played first (D915): the idle groundskeeper a laborer
   between (§6.1); *tests: a fresh cemetery's keeper helps out and keeps the seat; a year's wear calls them back for the days a season needs and no
   more; a keeper helping out comes back for a burial, and goes back to helping out after it; one who arrives to find it kept helps out again.*

## 10. Joe's calls

### Answered — D905 (relayed by the coordinating session with the reading it said back to him, which he confirmed) and D906

1. Who minds: **(a) the dead person's family and partner, until there is a cemetery.**
2. ~~Twenty graves in the first plot~~ → **three sizes, every one placed like a building; none painted** (*"i dont agree with 20 graves to a cemetery"*).
3. **The groundskeeper** — buries and tends all year; no laborer's fallback (call 3 c, renamed). *(⛔ reversed by Joe's play note — a laborer between, D915: "Laborer between")*
4. **(a) the cloth, and the family's visit.**
5. ~~The first painted bigger~~ → **the first is still free** (*"yes first one is still free"*); which size is call 8.
6. **(b) only those who die after this slice is in your game** — the dead from before are gone.
7. **(a) set to no groundskeepers counts as no place.**
8. **The three sizes as recommended, the small free** (D906; Joe, typed in this session: *"all as recommended, first free"*): small 4×5, 20 graves,
   free the first time and then 10 logs and 5 stone; medium 6×7, 42 graves, 20 logs and 10 stone; large 8×10, 80 graves, 30 logs and 20 stone (§5.2, §4.5).
9. **It wears** (D906; Joe, to *"so graves degrade like buildings and groundskeepers restore them like builders?"* put back to him as below: *"Yes, as you put it"*):
   the whole cemetery as one building; its groundskeeper keeps it up; a neglected one looks overgrown and its card says so; no grave is ever lost; neglect
   upsets nobody. The clock (8 / 12 / 20 years, restored by tending with no logs) is this spec's, inside his answer — §5.2.
10. **An older save with dead in it unlocks the cemetery on load** (D906; Joe, typed in this session, asked *"did we agree on what unlocks/triggers
   the cemetery?"* and answered: *"unlock on load as recommended"*): learned and owed the free small, no moment; its dead from before stay gone. The
   trigger itself is D395's, his: the village's first death.

**As specced unless you say otherwise:** you can never pull down or move one with people in it; a stone costs nothing; families lie side by side; the one popup is
the first death; it is fenced, so people walk around it, not through.

## 11. For the art lane (D774) — to HANDOFF's 🅿️ ART section, asked and not drawn

- **The cemetery** at three sizes — mown grass with a worn line, its fence or low hedge, the gate on a short end (a lychgate); the four seasons; snow on it;
  and worn, overgrown and wild (call 9).
- Its **three building icons** (or one at three sizes); the **groundskeeper's** icon and trade colour.
- **Graves:** a fresh mound, an older grassed one, and the stone — a few shapes so a row is not a stamp; does a founder's differ?
- **The dead waiting** — a small plain shape under a cloth. ⚠️ Cozy, never grim: Joe's eyes before it ships.
- **Figures:** the groundskeeper with a spade; *digging*, *carrying* (a bier on the shoulder), *burying*, *tending*; **mourners** standing, heads down.
- **The Lore node's picture.** Until each lands: a stand-in, and the probe says which.

## 12. Acceptance (Definition of Done)

1. This spec current; `morale.md`'s two rows, `buildings-plan.md` and the milestone map agree with it.
2. Every test in §9 written first; every guard red-checked and the reds counted; determinism green.
3. On a copy of his oldest village: the free small placed, the next death buried in it, partners side by side, its dead from before left gone, nobody newly unsettled.
4. A village with no cemetery loses people to it over generations; a kept one loses nobody to it (both ways).
5. A clean played run, no `ERROR`; Joe's play; the four verification lines.
