# Spec: Systems complete — the review of everything owed, and the workplan that finishes it

> Status: **📝 WRITTEN (D788, 2026-11-16) on `slice/systems-complete` — the review and its workplan, docs only; nothing built.
> ✅ JOE'S TEN CALLS ANSWERED, ALL AS RECOMMENDED (D789, 2026-11-17, *"All as recommended"*) — the cut line in §3 holds, and
> §5.1's W1–W13 is Phase 6's list (DESIGN §4). ✅ Merged into `main` and pushed at his word (D790, 2026-11-17). ✅ W1, the batch trade as a row (D791–D795, `batch-trade.md`), no golden moved — merged into `main` and pushed at his word (D796). ▶️ Next for systems: W2, morale.**
> *(Read "⏸️ Joe's calls in §6, each with a recommendation; the workplan (§5) is a proposal until he says so" until D789.)*
> Phase 6, between water (D783) and morale (D750's order, D776). Format per `METHODOLOGY.md §2`, loosely: this is a review,
> not a mechanic, so its "tests" are the exit gate's item 7 and the checks in §8.
>
> Neighbours: `content-inventory.md` (the code's inventory; its Part A corrected with this, §2.1), `buildings-plan.md` and
> `TECH-EXAMPLE.md` (the catalogues), `tech-tree.md §9` (the branch catalogue), `phase-5-exit-gate.md` (the same shape, one
> phase back), Joe's tracker `bclone-tracker.xlsx` (outside git; read, never written — at D772/A89 when read).

---

## 1. Why this exists

Joe, 2026-11-15 (D774): *"i want to add something to phase 6: systems complete. all in-game systems are built and
functioning and approved. that means a review of all backlog systems, buildings, professions, etc and a prioritized
workplan for them, including upgrade tiers, unlocked techs, etc"*

D776 placed it (*"All as recommended"*): **Phase 6's exit gate, item 7** — the phase ends only when everything on the
reviewed list is built, working and approved by him, or moved out by him in words with a §7 entry saying where; **the
review after water and before morale**, because its order decides what the rest of the phase is; **the town hall's tabs
last**, after the last content the workplan adds; **warm clothing Phase 6's** unless he moves it out here.

So this document is two things: **the review** (§2–§4: what exists, what is owed, what is half-done) and **the workplan**
(§5: what Phase 6 builds, in what order, how big each piece is). **§6 is what only Joe can decide.**

## 2. How the review was made

Read off the code and data on `main` at D787 (`a09c389`), not off the documents' claims — five specs lied about their own
status in August (D159), and this review found fifteen more (§4.4):

- **The code:** `BuildingKind`, `JobKind`, `Goods` and their rows (`BuildingRow`, `JobRow`, `GoodRow`), `SimConfig`,
  `TechTree.DefaultNodes`, `SimConfig.Techniques`, `SimConfig.Skills`, `SimWorld.WhyNotYet`, the systems in
  `src/Bclone.Sim/Systems`, and `data/sim.config.json`.
- **Every spec in `specs/`** (101) and `TECH-EXAMPLE.md`, each status line checked against the code and §7.
- **Joe's asks:** HANDOFF's ⏸️ OPEN list, DESIGN §4–§5, §7 searched by theme, and **his tracker, every tab** — Dashboard,
  Roadmap 17 rows, Food 69, Buildings 86, Professions 43, Resources 46, Systems 84, Techniques 77, Unlocks 15, Unlock
  Routes 8, Open Decisions 15.

The working inventories (every row, with file and line) are outside the repo in the session's notes folder —
`inventory-code.md`, `inventory-specs.md`, `inventory-asks.md`, `tracker-dump.md`. What matters from them is below.

### 2.1 What the village has today (read off the code, D787)

| | Count | Notes |
|---|---|---|
| Buildings | **30** rows of 31 ids | id 14, the longhouse, retired (D571) and ships no row; plus the founders' cart, a store nobody builds |
| Trades | **18**, plus laborers | the teacher (17) is the newest; **the teacher's count is only ever the player's** (D754), every other trade's is derived |
| Goods | **16** | every good has a maker and a use — nothing made and never spent, nothing defined and never made |
| Skills | **10** | ⚠️ **eight trades have no skill row**: fisher, hunter, smith, quarrier, miner, herdsman, merchant, teacher (§4.2) |
| Techniques | **4** | splitting lumber, coppicing, crop rotation, tended patches |
| Tech-map nodes | **15** | fourteen unlock a building; **the mason's yard is fogged for ever** (`TechCondition.NotYet`, `TechTree.cs:148`) and unlocks nothing |
| Crops · animals · neighbours | **1 · 1 · 3** | wheat · cattle · upriver, downriver, overland |
| Needs that can kill | **hunger, cold** (and old age) | water is a need that is *said*, never a death (D776 call 3) |

**Learned or given (16 gated buildings):** the library (gift, 15 years of a kept granary), the town hall (gift, the last
founder's death), the smithy (gift, 50 iron dug), the quarry (200 stone dug), the iron mine (the first iron tool), the mill
and bakery (5,000 wheat reaped), the tailor's shop (50 leather), the workshop (1,500 logs felled), the school (a library
stands), the wooden bridge (10 people), the stone bridge (1,000 crossings), the stone cottage (3rd generation and 20
people), the insulated manor (5th and 45), the barn and the trading post (the town hall standing).

**The ladders (upgrade tiers) that exist:** houses — cabin → cottage → manor (D716–D724); bridges — wood → stone (D546);
tools — stone → iron (D446); the household's carrying — armful → handcart (D686), and the market's and post's carts (D692);
paths — grass → worn → packed (D358, walked into being, never built). **The coat has one tier** (leather, D659).

**Shipped switched off, on purpose:** `gathers_per_thinned_tile` 0 (Joe, D297 — foraging thins nothing); `founding_buildings`
false (a pre-built village exists only for test fixtures).

## 3. ⭐ What "systems complete" should mean — the cut line (Joe's call 1, §6)

**The backlog is three different sizes of thing, and the word "systems" only fits one of them.** Counted across his tracker
and the catalogues there are about **50 buildings, 23 trades, 35 resources and 57 techniques** not built — most of them
`TECH-EXAMPLE.md`'s four-tier tree (D206), which runs to blast furnaces, boiler houses, glassworks and an imperial library.
Of its 44 buildings, 16 are built in some form. **Building all of it is not a phase; at this project's pace it is most of a
year.**

| Kind | What it is | Examples | Recommendation |
|---|---|---|---|
| **A. A system** | a mechanism the game does not have, that changes how a village lives or fails | morale, health and diet, drought, fire and blight, the town hall's tabs, paving, a second herd for the wool coat | **Phase 6 builds these. Item 7 means these.** |
| **B. Content on a built system** | another row of something that already works | another crop, another animal, another processing chain (smokehouse, tannery, sawmill), another technique | **Phase 6 builds the few a system needs** (§5.1: beer for the tavern, a second crop for blight, sheep for the wool coat, herbs for health); **the rest is Phase 7's**, made cheap first (§5.2) |
| **C. Wishlist breadth** | the industrial and luxury tiers | TECH-EXAMPLE Tiers 3–4: smelter and foundry, glassworks, paper mill, soapery, cooperage, boiler house, aqueduct, blast furnace, the great library | **Out of Phase 6** — after v1, or the modders' (Phase 7's promise: *"modders should be able to add anything"*) |

**⭐ The argument for the line:** item 7's words are *"all in-game systems are built and functioning"*. A tannery adds a row to
a chain shape the mill, the bakery, the tailor and the carpenter already work in; morale or drought adds a way the village
can live or fail that the player has never seen. **If kind B and C stay on item 7, Phase 6 has no end that anybody can
see** — which is what D640 wrote Phase 5's gate to stop (*"its list grows by Joe's picks … and nothing says when it is
finished"*).

⚠️ **What the line costs, said plainly:** Joe's 2026-08-15 note asked for *"variety of fish, crops, trees, game and
livestock"*. Under this line Phase 6 gives one more crop and one more animal, and the rest of the variety waits for Phase 7.

## 4. The review — what is owed, by kind

Each row: what it is in a sentence, where the ask comes from, whether a spec exists, and the kind (§3).

### 4.1 Systems not built (kind A)

| System | The ask | Spec | Notes |
|---|---|---|---|
| **Morale** — people leave, households have fewer children (D207's two effects; *work slows* and *sickness* refused) | D207, Phase 6 item 3 | ✅ `morale.md`, specced not started; four calls in its §10 | its causes are *"things the player can act on"* (§4 there), so it wants at least one lever building — the tavern (§5, W3) |
| **Health — diet first, then sickness, the herbalist and the infirmary** | D357, Phase 6 item 5 | ⛔ none — *"specced against D207's two refusals first"* | ⭐ **its stated blocker is already answered**: §4 says it waits on *"§5's survival-floor derivation redone across Phase 5's foods"*, and D520 says *"§5's derivation question is answered, and the answer was already true"* — the floor is solved at nutrition 1, the least any food is worth. Four food groups already exist (grain, meat, fish, forage) |
| **Drought** — foretold in spring, a yield modifier on the map, answered by the well | D656, Phase 6 item 6 | ⛔ none | gate item 6's sentence goes in its spec first |
| **Fire and blight** — telegraphed, legible, answerable (a well or firebreak; rotation) | D656, Phase 6 item 6 | ⛔ none | **blight on a one-crop village has one answer — not to be one-crop** (W7) |
| **The town hall's tabs** — founders and collections (Joe's *"museum"*), the knowledge screen (Phase 4's slice 3), charts with 1/5/10/20-year lookbacks | D251, D252, Phase 6 item 7 | ✅ `town-hall.md` slices 2–4 | charts add a sampled history that is hashed — every golden moves once |
| **Warm clothing — a second herd (sheep), shearing, wool, the wool coat** | D723, Phase 6 item 8, D776 | ⛔ none (*"a spec first"*) | the herd machinery is cattle-shaped (`livestock.md`); a second species is the real work |
| **Paving — the player's half of desire paths** (dirt → gravel → cobble; *"a built path cheaper still"*) | §2.6, D357 | 🔸 a paragraph in `desire-paths.md` | **the one pillar mechanic §2 names that was never built**; the art lane's cobbles wait on it (A72) |
| **The cemetery and the gravedigger** — free 4×5 on the first death, then painted; a morale hit when every cemetery is full | D35, D395 (Joe's call) | 🔸 a row (`buildings-plan.md` T0) | the morale hit *"waits on morale"* (D395) |
| **The overland caravan and a cart road from the map's edge to the hall** | D586, D587 (*"once carts exist"*) | 🔸 `trade.md §4.7` | its blocker (carts) shipped D686–D710; unbuilt |

### 4.2 Built, but not finished (half-built)

| What | What is missing | Source |
|---|---|---|
| **Skill — §2.1's pillar** | **seven working trades never get better at their work** (fisher, hunter, smith, quarrier, miner, herdsman, merchant); the teacher has none by Joe's call (D754). A fisher of forty years fishes like a novice | `skills-catalog.md`; D391 |
| **Techniques — §2.7** | four, one per original skilled trade; the trades since have none. Joe's *"a crop that stands into winter"* is a technique nobody has written (§5) | `tech-tree.md §9`, §5 |
| **Unlock routes** | PEOPLE, DOING and SCALE exist; SEREN (luck), IMPORT, ADJ (combination), CRISIS and TERRAIN do not — §2.7 itself names only the first three | `tech-tree.md §4` |
| **The tech map's mason's yard** | a node fogged for ever, unlocking nothing (the yard was taken out, D434's quarry entry) | `TechTree.cs:148` |
| **The town hall's gate** | gated only by the view's build bar (`Main.EarnedYet`); `SimWorld.WhyNotYet` has no arm for it, so the sim would take a paid hall on day one. The code says so itself and calls it *"their own change"* | `SimWorld.cs:4066` |
| **Content rows live in C#, not in the data file** | `data/sim.config.json` holds no goods, trades, buildings, skills, techniques, nodes, crops or animals — they are `SimConfig` defaults; ~60 numbers (every fishing-hut, lodge, library and hall cost, `literacy_years`, `fish_yield`, `meat_yield`) exist only in code. **A modder cannot add a building through the data file today** | Phase 7's promise |
| **Each processing trade is hand-written** | the mill, bakery, tailor and carpenter share a batch shape, but each has its own walk and work states, its own `ErrandKind` arm (D281's lesson, *"a ninth time"*), across ~16 files and ~40 sites | `BehaviorSystem.cs:700–830` |

### 4.3 Content on built systems (kind B) — owed, not systems

From the tracker and the catalogues; **none needs a new mechanism**, only rows and art:

- **Crops:** a second crop and up to eleven more (barley, oats, rye, corn, squash, potatoes, turnips, carrots, onions,
  cabbage, peas, beans — the crop id has been data since `crops-and-orchards.md §4`); orchards (cherry, apple, pear).
- **Animals:** pigs and chickens (D44: *"winter is eventually a herding and slaughtering season"*), goats, ducks, geese,
  yaks, alpacas, bees, silkworms; draught animals and the ox cart (D690, *Heavy Haulage*).
- **Wild food:** venison, boar, rabbit, wildfowl and Joe's game list (elk, moose, caribou, bison, ibex, beaver, fox,
  pheasant); nuts, roots, herbs; **fish variety**.
- **Processing chains:** smokehouse and salting, tannery, butchery, dairy (cheese, butter), root cellar, sawmill (planks),
  clay pit and kiln (bricks), mason's yard (cut stone), charcoal burner, smelter; cider, perry, wine, mead.
- **Materials tier:** planks, cut stone, bricks, mortar, rope, thatch, slate, glass — and the buildings priced in them.
- **Better tools on yield** (D395: *"eventually on yield when user unlocks upgrades for it"*); building upgrades for more
  seats (D274); gems and precious metals (D63, D67).

### 4.4 Docs that lie about the code (fixed at the gate, item 4 — listed so nobody trusts them first)

`content-inventory.md` Part A (29 buildings, 17 trades, no school, water *"no good yet"* — **corrected in this slice**, its
Parts B–C still dated); `professions.md` (no teacher row; fish and meat as `Goods.Food`); `buildings-plan.md` (the trading
post *"unbuilt in any form"*); `food-catalog.md` (the chain *"specced … not built"*); `gridless.md` (*"slices 1–4"*);
`logistics.md §6.1` (the shop-walk sentence *"not built"* — built D744); `trade.md §4.7` (the caravan *"blocked on carts"*);
`new-game-screen.md` and `seeded-map-generation.md` (heights *"specced, not built"*); `tick-loop.md` (`Fixed` *"not
built"*); `housing-and-density.md §8`; `phase-5-golden.md`'s first word; `skills-catalog.md` (*"six skills"*); `ui-art.md`;
`morale.md §7` (the town hall *"Phase 4"*); `forests-and-gathering.md` (a branch that does not exist). **And one the player
reads:** the villagers panel's *"Not here yet"* list (`Main.cs:4707`) still offers *Clothes* and *Students* — both shipped.
DESIGN §5 calls D700 *"unmerged"*; it is merged (`9e08a33`), and its branch `worktree-slice-cart-home` was never deleted.

### 4.5 Built, merged, and never confirmed played

The marketer's first leg and the starting limits (D696–D698); the trades' carts (D692–D695); the stalled lodge (D683, D685);
`market_detour_ticks` 4 (D671, measured, not played); the lane-bridge in Fernhollow (D680); the winter herd (D647); the
school's walk steps 4–6 (D773 reached 1–3). **Joe's Phase 5 walk (D747, *"All fine"*) came after all of them** and probably
covered most.

### 4.6 Not systems — the shell, the view, and modes

Full settings (D508); ironman mode and its name (D507); camera rotation (D395 — the map is a straight-down 2D drawing since
D725); tooltips on the build icons (D395); audio, HUD, winter shaders, the release workflow (Phases 7–8); the city-centre
fountain and the Shining Rock as gifts (D395, D362 — civic content, a sentence each); the bell tower (parked by Joe); a named
villager kept on a trade (§5 — a deliberate reopening of D15, reflection-guarded); the kitchen garden (D357, *"later"*).

## 5. The workplan

### 5.1 What Phase 6 builds, in order

Sizes from this project's own clock: **S** a day or less (the Established line, D784); **M** one to three days, a spec and
four to eight steps with a measurement (water D775–D783, the school D752–D769, clothing D659–D667, house tiers D716–D724);
**L** about a week, several slices (trade and the herd D586–D608, nomads D563–D584, the handcart D670–D707). Every piece is
one `slice/`, spec first, his calls before code, played before merge — as every Phase 6 piece has been.

| # | Piece | Kind | Size | Why here | New for the art lane (D774) |
|---|---|---|---|---|---|
| **W1** | **The batch trade as a row** — one walk-and-work shape for every input→output trade, the mill, bakery, tailor and carpenter moved onto it; **no golden moves**, proven | enabler | M | before beer, wool and herbs, each of which would otherwise be a fifth, sixth and seventh hand-written copy across ~16 files | nothing |
| **W2** | **Morale** (`morale.md`) — causes the village already has: home tier, a lit hearth, days cold, a dry jar, a varied diet, the walk to work, a death in the family; departures and fewer children | A | L | Joe's order (D750); the nomads it pairs with are built | a *leaving* state and its walk off the map |
| **W3** | **Beer and the tavern** — wheat → beer at a brewery (W1's row), drunk at a tavern; **morale's first building** | B for A | M | D656: beer waits on morale; a morale with no lever to pull is §1.1's invisible weather | brewery, tavern, ale; *brewing*, *at the tavern* |
| **W4** | **The cemetery and the gravedigger** (D395 as Joe called it) | A | M | a death is the village's commonest story (§1.4); *"every cemetery full"* is a morale cause | cemetery (painted), graves, gravedigger; *digging*, *burying* |
| **W5** | **Skill for the seven trades, and their techniques** — a row each; a technique each for the trades with a master's craft, **including Joe's crop that stands into winter** | A (finishes §2.1/§2.7) | M | the pillars' own claim is false for seven of eighteen trades; independent of everything else, so it can move | nothing (lines on cards) |
| **W6** | **Health — diet first** (food groups, an immunity a one-food diet erodes, said before it bites), **then sickness, the herbalist and the infirmary**, specced against D207's refusals | A | L | its blocker is answered (§4.1); before drought, whose answer reads health | herbalist's hut, infirmary, herbs, remedy; *gathering herbs*, *sick in bed*, *tending* |
| **W7** | **A second crop** — a `CropRow` and a *"grow…"* control on the farm (which crop is Joe's) | B for A | S–M | blight's answer and diet's second grain or vegetable | the crop in four stages, sown and ripe |
| **W8** | **Drought, then fire and blight** — each foretold, on the map, answerable (the well, a firebreak, rotation and the second crop) | A | M + M | last of the pressures (§0.1 admits a pressure once its answer exists); gate item 6 | dry ground, a burning building and its ruin, a blighted field |
| **W9** | **Paving** — the player paints over an emerged path to pave it in stone (gravel, then cobble), cheaper than packed earth and fossilised (§2.6); auto-pave stays a later policy | A | M | the last unbuilt §2 mechanic; the art lane is waiting for it | gravel and cobble paths; *paving* |
| **W10** | **Warm clothing** — sheep a second herd on the barn and pasture, shearing, wool, the wool coat at the tailor (no chill outdoors, none under a fireless roof; children still coatless, D659) | A | L | D776's default; after W1, so the coat is a row | sheep, fleece, wool, wool coat; *shearing* |
| **W11** | **The caravan and the cart road** (`trade.md §4.7`) — in, at Joe's call 4 (D789) | A | M–L | its blocker is gone; trade is §2.4's pillar | caravan, cart road; *trading at the road* |
| **W12** | **The two gifts** — the fountain (`Home` recentres on it) and the Shining Rock (a monument found after some ore is mined, in the collections, no effect on the economy — call 7, D789) | civic | S + S | before the tabs, so the collections list them | fountain, the Shining Rock |
| **W13** | **The town hall's tabs** — founders and collections, the knowledge screen, charts | A | L | last, by D776: it lists everything | the tabs' art (the UI lane's) |
| — | **The gate**: items 2–5 (a golden reaching the Phase 6 systems, the QA walk, the docs true, the four checks) | gate | M | — | — |

**Housekeeping that rides along, each S, none worth a slice of its own:** the town hall's gate moved into
`SimWorld.WhyNotYet` (with W12 or W13); the *"Not here yet"* list made true (`Main.cs`, after the art lane's UI hook-in
lets go of it — or the art lane's); the docs in §4.4 at the gate; the mason's yard node either kept as the materials tier's
horizon or taken off the map (§6 call 2).

**⭐ The whole of it, honestly sized:** thirteen pieces and the gate — roughly **six to eight weeks** at the pace of the last
two months (one M every two to three days, one L a week, his plays between). **W5 and W9 depend on nothing and can move
anywhere in the list**; W1 must come before W3, W6 and W10; W8 after W6 and W7; W13 last.

### 5.2 What moves to Phase 7 — "mods and content" (§3's kind B)

Phase 7 is already *"`JobKind` and `BuildingKind` stop being C# enums and become rows"* (§4). **This review's finding is that
the rows already exist and live in the wrong place** — they are C# defaults, and the data file holds none (§4.2). So Phase 7
becomes two steps, in order: **(1) the rows move into the data file** (goods, trades, buildings, skills, techniques, nodes,
crops, animals, and the ~60 code-only numbers) — after which a crop, an animal or a W1-shaped chain is a row and its art;
**(2) the content**, from §4.3, Joe choosing how much of it v1 carries. Kind C stays after v1.

### 5.3 What moves out of Phase 6 to the shell (Phase 8, release)

Full settings and ironman mode (D507–D508, Phase 4.5's leftovers); camera rotation and the build icons' tooltips (view work,
the art lane's UI pass if anybody's); audio, HUD, winter shaders, the release workflow (already Phase 8's).

## 6. ✅ Joe's calls — in plain words, each with a recommendation

> ✅ **All ten answered as recommended (D789, 2026-11-17, *"All as recommended"*).** So: the cut line holds; the mason's
> yard stays on the map as the horizon and its card is to say *"later"* (housekeeping, §5.1); warm clothing, the caravan,
> paving and the cemetery are in, as W10, W11, W9 and W4; the Shining Rock is a monument with no effect on the economy, the
> fountain is in, the bell tower stays parked; the named-villager lock, the kitchen garden, the ox cart and the five extra
> unlock routes are out of Phase 6, full settings and ironman go to Phase 8; the order is W1–W13; `worktree-slice-cart-home`
> is deleted (D789), and the Phase 6 QA walk covers §4.5's merged-but-unplayed pieces. *(Heading read "⏸️" until D789.)*

1. **Where "done" is.** Phase 6 ends when the *systems* are in: the new ways a village can live or fail (morale, health,
   drought, fire, paving, the wool coat, the hall's tabs). More crops, more animals, more workshops — the variety — come
   in Phase 7, once adding one is a line in a file. **You would see:** one new crop and one new animal this phase, not
   twelve. **Recommended: yes, the cut line in §3.** *(Item 7 then covers §4.1, §4.2 and §5.1, and the rest moves out in
   your words.)*
2. **The mason's yard on the tech map** — the fogged circle that never opens. **Recommended: leave it as the horizon** —
   it is where Phase 7's planks-and-stone tier will hang — and have its card say *"later"* rather than nothing.
3. **Warm clothing — in Phase 6 or out?** It is a second herd (sheep), the biggest piece on the list after morale and the
   tabs. **Recommended: in**, as D776 left it — it is your *"fewer to none going in to get warm"*.
4. **The caravan and the cart road** (your D587, *"once carts exist"*). Carts exist now. **Recommended: in, as W11** — it is
   the only §2.4 piece left. Out is fine too: trade works without it.
5. **Paving** — a stone path you lay over a worn one, faster to walk. **Recommended: in, as W9** — §2.6 says the player
   paves what the town proved, and today the player cannot.
6. **The cemetery as you described it in D395** (a free plot on the first death, then painted, one gravedigger).
   **Recommended: in, as W4, beside morale.**
7. **The Shining Rock — what does it do?** **Recommended:** a monument found after some ore is mined, listed in the
   collections, with no effect on the economy — the town hall's tribute shape. And **the fountain in, the bell tower stays
   parked.**
8. **Out of Phase 6, in your words:** a named villager kept on a trade (it reopens D15), the kitchen garden, the ox cart,
   the five extra unlock routes (luck, import, combination, crisis, terrain), full settings and ironman (to Phase 8).
   **Recommended: all out** — to the later list, or Phase 8 for the two settings. Pull any back in by name.
9. **The order** — §5.1's W1–W13. **Recommended as written**; W5 (skills for seven trades) and W9 (paving) can go wherever you
   like.
10. **Housekeeping, yes or no:** (a) delete the merged branch `worktree-slice-cart-home` (`git branch -d`, safe — it is in
    `main`); (b) let the Phase 6 QA walk cover the merged-but-unplayed pieces in §4.5, rather than a play each.
    **Recommended: yes to both.**

## 7. What the art lane will draw (D774's rule)

The workplan's new buildings, goods and villager states are in §5.1's last column — **about ten buildings** (brewery, tavern,
cemetery, herbalist's hut, infirmary, a burnt ruin, fountain, the Shining Rock, a caravan and its road, gravel and cobble
paths), **one animal** (sheep), **a crop** (four stages), and **about a dozen villager states**. Each piece's own spec names
them again in its own section before it is built.

## 8. Acceptance — how item 7 is checked

- ✅ **Joe answers §6** — answered, all as recommended (D789) — (the cut line first — nothing else in §5 is settled without it), and the answers go into §7 and into
  DESIGN §4's Phase 6 list, which becomes this workplan.
- **Item 7 holds when** every row of §5.1 that he keeps is built, played and merged, and §4.2's half-built rows are finished
  or moved out in his words — each with its §7 entry.
- **Gate item 4 re-reads this document** against the code at the end, as this one re-read `content-inventory.md`.
