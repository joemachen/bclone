# Spec: Skill for every trade, and their techniques — W5

**Decisions:** D919 (claimed). Builds on D174–D202 (`skills-catalog.md`: skill is time on the task, mastery at twenty years
bites as half the ticks, a youth beside a master learns faster), D225–D226 (`tech-tree.md`: a technique is a row, the
village's while a master lives, kept by a library shelf), D300 and Joe's note at DESIGN §5 (*"unless the user 'unlocks' a
buff that allows crops to last into winter days"*), D391/D392 (the founding draws from `founding_trades`, never the
catalogue), D754 (the teacher has no skill).
**Workplan:** `systems-complete.md §5.1` W5 — *"a row each; a technique each for the trades with a master's craft,
including Joe's crop that stands into winter."* Kind A, size M, independent of everything else.
**Status:** 📝 **specced; ✅ every call answered (D921–D922, §7a); 🔨 building on `slice/w5-skills` — ✅ step 1 (D923): the nine
skill rows, burying through `BeginWork`; ✅ steps 2–3 (D924): techniques 4–9, tempering, the hay through `YieldFor`; ✅ step 3b (D925): reading the water
unwritable, carried by five years beside a master.** Steps 5–9 to come (§9).

---

## 1. What is true today, read off the code (not the docs)

**21 trades** (`JobKind` 0–20), **11 skill rows** (`SimConfig.Skills`, ids 1–11), **4 techniques** (`SimConfig.Techniques`,
ids 0–3). The review counted 18 trades and seven without skill (D788); two trades have arrived since.

| Trade | Skill row? | What mastery changes today | Technique? |
|---|---|---|---|
| forager, forester, woodcutter, farmer | ✅ | half the ticks over the action | ✅ one each |
| miller, baker, tailor, carpenter, brewer | ✅ | half the ticks over a batch | ❌ |
| **builder** | ✅ | ⚠️ **nothing** — see §6 | ❌ |
| **marketer** | ✅ | ⚠️ **nothing** — see §6 | ❌ |
| **fisher** | ❌ | — | ❌ |
| **hunter** | ❌ | — | ❌ |
| **smith** | ❌ | — | ❌ |
| **quarrier** | ❌ | — | ❌ |
| **miner** | ❌ | — | ❌ |
| **herdsman** | ❌ | — | ❌ |
| **merchant** (the trading post) | ❌ | — | ❌ |
| **tavern keeper** (W3) | ❌ | — | ❌ |
| **groundskeeper** (W4) | ❌ | — | ❌ |
| teacher | ❌ by Joe's call (D754) | — | — |

**So a fisher of forty years fishes like a novice, and nobody in the village ever says so.** §2.1's claim — *"a farmer
with 20 years in the fields is meaningfully better than a fresh laborer"* — is false for nine working trades.

**What makes this cheap:** skill is already generic. Every action begins at one door, `SimWorld.BeginWork`, which asks
`SkillGrownBy(trade)` for the trade's row and shortens the action by mastery's share. **A skill row for the fisher is all
it takes for a master fisher to cast in half the time** — the accrual (`SkillSystem`), the tiers, the mastery line, the
card's *"N years as a fisher"*, apprenticeship and the at-risk line all key off the row, not the trade. Techniques are
generic the same way: `YieldFor` adds the village's technique to any trade whose row has one.

**And what makes it safe:** the founders, the nomads and a re-founding draw their trades from `founding_trades` (D392),
never from the catalogue, so new rows move nobody's founding. A novice is today's villager to the tick (§3.2 of
`skills-catalog.md`), so the survival floor `VillageEconomy` derives is untouched.

## 2. The skill rows — nine, appended at ids 12–20

| Id | Skill | Grown by | What a master does | Card line | The mastery line (proposed words) |
|---|---|---|---|---|---|
| 12 | fishing | fisher | casts in half the time: 10 ticks → 5 | *as a fisher* | *{0} has fished this river for {1} years. The water is read before the line goes in.* |
| 13 | hunting | hunter | a hunt in about half: 15 → 8 | *as a hunter* | *{0} has hunted these woods for {1} years. The game is found where it was always going to be.* |
| 14 | smithing | smith | forges in half: 4 → 2 | *as a smith* | *{0} has worked the village's forge for {1} years. The iron is struck at the right heat, every time.* |
| 15 | quarrying | quarrier | digs in half: 4 → 2 | *as a quarrier* | *{0} has cut the village's stone for {1} years. The rock splits where it is asked to.* |
| 16 | mining | miner | digs in half: 6 → 3 | *as a miner* | *{0} has worked the mine for {1} years. The ore is followed by feel in the dark.* |
| 17 | herding | herdsman | cuts hay, feeds and culls in half: 3 → 2, 3 → 2, 8 → 4 | *as a herdsman* | *{0} has kept the herd for {1} years. Every beast is known by its walk.* |
| 18 | bartering | merchant | nothing faster (§3) | *as a merchant* | *{0} has kept the trading post for {1} years. Every boat's master is an old acquaintance.* |
| 19 | tavern keeping | tavern keeper | nothing faster (§3) | *as a tavern keeper* | *{0} has kept the tavern for {1} years. Every regular's cup is filled before they ask.* |
| 20 | grave keeping | groundskeeper | buries in half the time: 12 → 6 | *as a groundskeeper* | *{0} has kept the cemetery for {1} years. Every grave is known, and whose it is.* |

- **Ids appended, never renumbered** (`SkillRow.Id`'s rule). Rows in `SimConfig` like the eleven, so `data/` gains none
  (the rows-into-data move is Phase 7's, `systems-complete.md §5.2`).
- **The tool still stacks** (D429, *multiply, one rounding*): a master quarrier with an iron pick digs in 1 tick.
- **The groundskeeper's burying goes through `BeginWork`** — it sets its ticks directly today (`BehaviorSystem.cs:6242`),
  the one action in the game that does. That is the only behaviour line this part changes.
- **No `founding_trades` change** (call 6): nobody arrives a master fisher with no fishing hut in the valley.
- **The lines are pronoun-free**, like the eleven (villagers have names and no sex).

## 3. What skill means where there is nothing to do faster

The merchant waits on boats and keeps standing orders; the tavern keeper keeps a tavern open. Neither has an action with
a length. **Recommended (call 2): they still get the row** — the years on their card, *apprentice → journeyman → master*,
the mastery line in the log, the at-risk line when the last of them is old — **and nothing faster, said honestly**: the card
never claims a speed it does not have. A trade where twenty years are recorded but change nothing is better than a trade
that is not a trade at all on the card, and it is the shape the marketer has had since Phase 3 (§6).

⛔ **Not invented for them here:** better barter terms, more cups poured. Each would be a new mechanic on a trade W5 exists
to finish, not to redesign.

## 4. The techniques — one each for the trades that make something, plus Joe's crop

**A technique is the village's, not the person's** (D225): once a master here works it out, every worker of that trade
does it the better way, and the village loses it when its last master dies unwritten. Discovery and loss lines,
library shelving and the at-risk line are already generic. Numbers are proposals until a run argues (§9).

| Id | Technique | Trade | What it does | Why this effect |
|---|---|---|---|---|
| 4 | **reading the water** | fishing | **+15 % fish a cast** (280 → 322) | ⭐ the design's own example of knowledge that cannot be written down — *"knowing when the fish run"* (`tech-tree.md §3b`); call 4 |
| 5 | **tracking** | hunting | **+15 % meat a kill** (900 → 1,035) | the existing yield shape |
| 6 | **tempering** | smithing | **a tool taken up lasts a quarter longer** (stone 150 uses → 187, iron 250 → 312) | a forge makes **one** tool, and 15 % of one is nothing — a yield bonus there would be a technique that does nothing (the D56 shape this project refuses) |
| 7 | **reading the bed** | quarrying | **+10 % stone a dig** (10 → 11) | the existing shape; 10 % is the smallest step a dig of 10 can show |
| 8 | **following the vein** | mining | **+20 % iron a dig** (5 → 6) | a dig is 5 iron, so 15 % rounds to nothing; 20 % is one more |
| 9 | **good hay** | herding | **+15 % hay a tile cut** (67 → 77) | hay is the herd's winter; the herd's yield site moves from `WithTool` to `YieldFor` so the technique reaches it |
| 10 | **standing into winter** | farming (its second) | **a crop not reaped by winter's first day stands on five more days before it rots** | Joe's note — §5 |

**Who gets none, and why** (call 3):
- **The merchant, the tavern keeper, the groundskeeper** — nothing they make more of. A technique needs an output.
- **The builder and the marketer** — the same, and their skill does nothing yet either (§6).
- **The five batch trades** (miller, baker, tailor, carpenter, brewer) — they have skill already and no technique.
  ⚠️ Two of them make **one** thing a batch (a coat, a cart), so a percentage rounds to nothing there too and they would
  want a different effect each. **Recommended: not in W5** — the review's cut line puts *another technique* on a built
  trade in Phase 7's content (`systems-complete.md §3`, kind B), and W5's line is the trades with **no** skill.

**Two new columns on `TechniqueRow`**, both inert at their defaults so the four existing rows mean exactly what they mean:
- `tool_uses_bonus_percent` (0) — tempering's effect, on the uses a villager's tool starts with when it is taken up
  (`BehaviorSystem.cs:7104`; uses live on the villager, not the good — so the village's better way reaches every tool taken
  while it is known, the cart's too).
- `stands_into_winter_days` (0) — §5's effect.

And, if call 4 is yes, a third: `recordable` (true) — false refuses a library shelf. `SkillRow.Recordable` has been an inert
column since Phase 3 and nothing reads it; a technique is what a library actually writes, so the flag belongs on the
technique.

## 5. ⭐ Joe's crop that stands into winter

**Today (D300):** on the first tick of winter, every field still standing rots — *"Winter took 6 fields of unreaped crop."*
Joe confirmed that rule and added the exception: *"unless the user 'unlocks' a buff that allows crops to last into winter
days."* DESIGN §5 recorded it as **a buff to the deadline, not the yield**: the crop still rots eventually; the player gets
more of the winter to notice.

**What the player sees with it known:**
1. A line in the log when a master farmer works it out: *"Wren has farmed these fields for thirty years, and has learned
   to leave a standing crop stooked against the frost. What is not reaped by winter will keep a few days longer now."*
2. At winter's turn, instead of the rot: *"6 fields still standing — the frost will take them in five days. Winter, Year 41."*
3. Farmers keep reaping into winter's first five days, and whatever is still standing on the fifth day rots with today's
   line. The farm already keeps its seats while a crop stands, in any season (`FarmerSeatsWithGroundToWork`); ⚠️ **the reap
   itself is gated to autumn** (`SeasonRules.IsReaping`, read at six sites in `BehaviorSystem`), so those sites ask one new
   question instead — *may this crop be reaped today?* — true in autumn, and in winter's standing days once it is known.

**What it is for:** D300's *storage on the stalk* — a village near its food limit leaves the crop standing and reaps it as
the store falls — gets a few more days to come in under the limit before the frost decides. A late-founded farm or a short
autumn loses less. Nothing else moves: the yield, the sowing, the seasons and the food floor are as they are.

- **Five days, a third of winter** (15 days a season, 4 ticks a day) — recommended (call 5). Long enough to show, short of
  turning winter into a fourth harvest season.
- **Both crops** — wheat and barley stand alike; the technique is the farmer's, not the crop's.
- **Lost like any technique:** if the last master farmer who knows it dies with no shelf, the next winter's first day rots
  the fields again, and the lost line says who took it.

**Who works it out (call 5's second half).** Farming already has crop rotation, worked out the moment a farmer first
masters the trade here. Recommended: **a master farmer who has farmed thirty years** — ten past mastery, the same person or
another. *"Thirty years in the fields"* is a sentence the player can read, and it spaces the two farming discoveries a
decade apart rather than landing both in one moment. One new column, `after_years` (null: at mastery, as today). Measured
median age at mastery is 39 against a lifespan of 55–79, so most masters who stay on the land live to thirty years.

## 6. ⚠️ Found on the way — the builder's and the marketer's skill do nothing

`skills-catalog.md §4.2` says a master builder *"raises a frame in fewer ticks"* and a master marketer *"picks better legs"*.
**Neither is true.** A builder's work is one unit of site work a tick (`BehaviorSystem.cs:2154`) plus the tool's extra
(D617), and mastery never reaches it — `BeginWork(Builder, 1)` returns 1 whatever the skill. The marketer has no action
at all. Their years accrue, the card counts them, the mastery line fires — and nothing changes.

**Recommended (call 7): not fixed in W5, recorded and said true in the catalogue.** A master builder doing twice the site
work would halve every build in a village whose founders include a master builder (building is a founding trade), so it
moves the cold start — a measured change of its own, not a line in this one. W5 corrects `skills-catalog.md §4.2`'s two
rows to what the code does, and HANDOFF carries the builder as an open item.

## 7. ⏸️ Joe's calls — plain words, a recommendation each

1. **Which trades get a skill.** The nine above — fisher, hunter, smith, quarrier, miner, herdsman, merchant, tavern keeper,
   groundskeeper. The teacher stays without (your D754). *You'd see "12 years as a fisher" on a fisher's card, and the log
   line when one becomes a master.* ⭐ **Recommended: all nine.**
2. **The merchant and the tavern keeper have nothing to do faster.** Give them the years, the tiers and the mastery line
   anyway, with no speed? Or leave them without a skill? ⭐ **Recommended: give them the row, no speed.**
3. **Which trades get a technique.** Fishing, hunting, smithing, quarrying, mining, herding — plus your winter crop on
   farming. Not the merchant, tavern keeper, groundskeeper, builder or marketer (nothing to make more of), and **not the five
   batch trades** (mill, bakery, tailor, carpenter, brewery) — those wait for Phase 7's content with the other new
   techniques. ⭐ **Recommended: those seven, batch trades later.**
4. **Can the fishers' technique be written down?** Your tech tree has always had "knowing when the fish run" as the example
   of knowledge only a person can carry. If it can't be shelved, the village keeps it only by always having a master fisher
   alive — the fishing hut seats four, so a youth beside a master is easy to arrange. *You'd see the library refuse it by
   name: "reading the water can't be written down — it lives in Ada's hands."* ⭐ **Recommended: unwritable — the first one.**
5. **Your crop that stands into winter.** Fields not reaped by winter's first day stand **five more days** (a third of
   winter) before they rot, and it's worked out by **a master farmer with thirty years in the fields**. ⭐ **Recommended:
   five days, thirty years.** (Other shapes: three or seven days; worked out at mastery together with crop rotation.)
6. **The founders.** Nobody arrives a master fisher, smith or herdsman — the founders' trades stay the six they are today,
   since the buildings for the new ones don't exist at the start. ⭐ **Recommended: unchanged.**
7. **The builder's and marketer's skill do nothing** (§6). Fix the builder later as its own measured piece, and correct the
   catalogue now? ⭐ **Recommended: later, recorded.**

### 7a. ⏸️ Joe's answers (D921) and what is still open

✅ **Calls 1, 2, 3, 5, 6 and 7 as recommended.** *"i agree with all except:"*

⏸️ **Call 4 became a question:** *"if there are people working alongside the master fisher, do they learn it from the master?"*
Today, no: a learner beside a master learns the **trade** twice as fast, but a technique lives only in **masters** — the
learner carries it only once they master fishing themselves. Proposed back: for a technique that cannot be written down, a
villager who has worked beside a master who knows it for some years carries it too, before they master the trade (stored
per villager, sparse, hashed). His yes and the years first.

⏸️ **The shelf note became a building:** *"the full library (we'll need to come up with a better name) should unlock after the
3rd library has been built when the 11th technique has been learned - whether or not any other libraries have been built."*
⚠️ §7's *"a full library"* meant a library whose shelves are full, not a building — his idea is new and worth having: a
bigger library, unlocked when the village has learned every technique (eleven, the number three five-shelf libraries
would be needed for). Read back to him before it is specced: which condition unlocks it, what it holds, whether it is a
gift or built, and its name. It adds a building row and an icon for the art lane.

✅ **Both read-backs confirmed (D922, Joe: *"Yes to both"*):**
- **Fishing's technique is carried by the people beside the master.** A technique that cannot be written down
  (`recordable: false` — *reading the water* only) is **carried** by a villager who has worked **five years** at the same
  workplace as a living master who knows it, before they master the trade themselves. Carried is stored on the villager
  (sparse, hashed, saved), never lost while they live; their card names it and who it was learned beside. The technique
  stays known while any master *or carrier* of it lives. Written techniques are unchanged: masters hold them, a shelf keeps them.
- **The great library** (working name, Joe's to change) — a second kind of library: **a gift when the village has learned
  every technique there is** (eleven today; read off the catalogue, never typed), however many libraries stand. It holds **a
  shelf for every technique**. A building row and an icon for the art lane. ⚠️ Once it stands nothing written is lost again;
  what stays mortal is every person's years and the one technique that cannot be written.

**And one that is yours but needs no answer now:** the library holds **five** techniques a building. With seven new ones the
village can know eleven, so a full library refusing a shelf becomes a real moment — the reason its own note gave for the
number (`library_shelves`: *"the shelf is a decision again the day the tree passes five"*). ⭐ **Recommended: five stays**;
the measurement in §9 says how often a second library is wanted, and the number moves only on that.

## 8. The art lane's list (D774)

**Nothing new to draw.** New lines on cards and in the log only. Two notes for the lane:
- **A master fisher's cast is half as long**, and `VillageMap.Fishing` already reads its length from `WorkTicksFor`
  (A275–A277), so the cast animation shortens for a master without a change. Worth a look in play.
- **A ripe field in winter's first five days** draws as the ripe field it is, under the winter wash. No stooks-in-snow
  picture is asked; if Joe wants one it is an art piece of its own.

## 9. Build order, tests and the measurement

**Order (one commit each, docs with each):**
1. The nine skill rows (§2); the groundskeeper's burying through `BeginWork`.
2. Technique rows 4, 5, 7, 8, 9 (yield) and the herd's hay through `YieldFor`.
3. Tempering — `tool_uses_bonus_percent`, read where a villager takes up a tool.
3b. *Carried beside a master* — the five years for an unwritable technique (D922).
4. `recordable` on `TechniqueRow` and the library's refusal (if call 4 is yes).
5. The crop that stands into winter — `after_years`, `stands_into_winter_days`, `CropSystem` rotting on the day, and the
   reap's autumn gate asked as *may this crop be reaped today?*
5b. The great library — its row, the gift at every technique, a shelf for each (D922).
6. The card lines checked in the view (the probe; Joe's eyes).
7. **The measurement** on harness centuries: masters reached per new trade and the age they reach it, techniques
   discovered and lost, how often a library is full, and food, stone, iron and hay against `main` — survival should not move
   outside noise, because a novice is today's villager and stock limits cap what masters make.
8. **The goldens, once, last** (D152), each move explained by ablation (the rows out, the goldens back).

**Tests first, red before green, each break counted (D326):**
- A posed master of each of the six action trades and the groundskeeper takes mastery's ticks; a novice takes today's,
  to the tick.
- A merchant and a tavern keeper accrue years and reach mastery and the line fires; nothing they do changes length.
- Each technique, once known, raises its trade's yield by its row (or a forged tool's uses), and nothing before.
- `founding_trades` unchanged → every seed's founders the same (`NoFoundingMovesWithTheNewSkillRows`' shape).
- Reading the water refuses a shelf by name, and is lost with its last master.
- The crop that stands: known → a ripe field survives winter's first day and rots on the sixth; unknown → today's rot,
  byte-identical; the narration at the turn; a farmer reaps in those days; `after_years` discovery at thirty, not at mastery.
- A determinism run with every new row in play stays green.

**Definition of done:** all four checks; this spec's status line true; `skills-catalog.md §4.2` and `tech-tree.md` lists
current; `content-inventory.md`'s skill and technique counts; Joe plays it; merged and pushed at his word, typed in this
session.
