# Spec: Phase 5's exit gate — closing the phase on five written items

**Decisions:** D640 (proposed), D641 (accepted as written), D656 (Joe's verdicts on the open items), D743 (Joe: *"go
with phase 5 exit gate"*), D744 (this spec). Neighbours: D169 (Phase 2 closed the same way), D159 / D640 (status lines
checked against the suite, not themselves), D642–D649 (`phase-5-golden.md`, item 2), METHODOLOGY §3 (QA and the
played log).
**Status:** 🔨 **ON `slice/phase5-gate` (D744, 2026-11-14), UNMERGED.** Item 2 ✅ (before this slice); item 1 ✅ but for
piece 5's play; item 3 📝 written, ⏸️ **not walked**; item 4 ✅; item 5 ⏸️ the four checks on the merged tree and Joe's
walk's log. **Phase 5 ends when all five hold, and not before** (D641) — **what is left is Joe's walk.**

---

## 1. Why this exists

Phase 5 has been "current" since D386 (2026-09-17). D640 gave it a written end and Joe accepted it as written (D641):
five items, `DESIGN.md §4`, the end of Phase 5. On 2026-11-14, with all five of the art lane's systems asks built,
merged and pushed (D725–D742), Joe chose the gate as the systems lane's next piece: *"go with phase 5 exit gate"*.

The art (🅿️ D639, the art lane's since D699/D710) and Phase 4.5's leftovers (full settings, ironman) are **not** on the
gate, on purpose (D640, D641).

## 2. The five items, and where each stands

| # | Item (D641) | Where it stands | What closes it |
|---|---|---|---|
| 1 | Every Phase 5 item has a verdict | ✅ the legibility sentence built (§4.1); ⏸️ house tiers' piece 5 (merged unplayed at Joe's word, D724) | Piece 5 played in the QA walk (§5) |
| 2 | A golden that reaches the Phase 5 chains | ✅ **Done** — `PhaseFiveGoldenTests`, a posed village 20 years on seed 12345, and a coverage test that every chain turned over (D648–D649; re-taken since, last D737) | — |
| 3 | A QA checklist Joe walks | 📝 `specs/phase-5-qa-checklist.md` written (D744); ⏸️ not walked | **Only Joe's walk closes it** (D164/D168) |
| 4 | The docs true at the gate | ✅ done (§6.1) | — |
| 5 | The four checks on the merged tree, and a played run with no `ERROR` in its log | The baseline below (§7); the run is Joe's | The four checks on the merged tree; Joe's QA walk *is* the played run, and its log is scanned (§7) |

## 3. Item 1 — a verdict on every Phase 5 item

Every entry on §4's Phase 5 list, and every piece built in the phase since (§4's *"Built in Phase 5 and not on this
list"*, and the five art-lane asks of D725–D742). **Verdicts are Joe's words or a play of his**, cited.

| Item | Verdict | Where |
|---|---|---|
| Organic housing — plots and lanes, the well | ✅ built, played, merged | D386–D388, D427–D428 |
| Every load to a store | ✅ built, played (*"i played - it all feels fine"*), pushed | D385 |
| Trades visibly work | ✅ built, played, pushed | D384 |
| Bridges — wooden, then stone | ✅ built, played, merged, pushed | D546–D551, D702–D705 |
| Building condition and maintenance | ✅ built, played, merged, pushed | D554–D560 |
| Tools — wear per use; the smith | ✅ built, played, merged | D391, D429–D433 |
| The quarry | ✅ built, played, merged, pushed | D434–D447 |
| The iron mine | ✅ built, played, merged | D449–D451 |
| Clothing | ✅ built, played (*"seems to work"*), merged | D659–D667 |
| Soil depletion | ⛔ **dropped from the game** (*"remove soil depletion from the game completely"*) | D656 |
| House tiers | ✅ pieces 1–4 built, played and merged; **piece 5's two switches merged unplayed at Joe's word** (*"Merge it now"*) — ⏸️ **played in the QA walk** (§5) | D716–D724 |
| Drought | ➡️ **moved to Phase 6** | D656 |
| Logistics | ✅ *nearby* (D671), the household's handcart (played, D690), the trades' carts (D692–D698), the cart's night and the restock knob (D700, D707); ⏸️ **the legibility sentence still owed** (D668 outcome 2, D670: *"the brush and the card still owe the player the sentence"*) — **built here (§4)** | D668–D707 |
| Fire and blight | ➡️ **moved to Phase 6** | D656 |
| The food chain (bread) | ✅ built, played, merged | D520–D527 |
| Beer | ➡️ **moved to Phase 6** (waits on morale) | D656 |
| Work from the steading | ✅ built, played, merged | D513–D514 |
| Trade and the herd | ✅ built, played, merged | D586–D608 |
| The armful and the meal | ✅ built, played, merged | D537–D538 |
| The seams (reach, valley-wide, claimed) | ✅ built, played, merged | D539–D545, D561, D613, D701 |
| The destroy brush | ✅ built, played, merged | D490–D495 |
| The paths drawn as walked | ✅ built, played, merged | D579–D607 |
| Doors | ✅ built, played, merged | D619–D626, D657–D658 |
| Free buildings on wooded ground drawn | ✅ built, played (*"approved"*), merged, pushed | D725–D727 |
| No forester planting in winter | ✅ built, merged (*"merge"*), pushed | D728–D729 |
| The build queue panel | ✅ built, merged (*"Merge"*), pushed | D730–D731 |
| The nomads' moment at the slow-down | ✅ built, merged (*"Merge"*), pushed | D732–D733 |
| Real heights | ✅ built, played (*Hilly*, *Rolling*), merged (*"okay merge then"*), pushed — **its look is the art lane's**, off the gate | D734–D742 |

⚠️ Three of the art-lane asks (forester winter, the build queue panel, the nomads' moment) were merged at Joe's word
without a play reported in this session; they are walked in the QA checklist like everything else, so the walk covers
them.

## 4. The legibility sentence (logistics' last piece)

**Owed since D668 outcome 2; D670 made it the companion of whichever mechanic landed:** *"the brush and the card still
owe the player the sentence (D112)"* — no fence the player cannot see. Today the housing brush warns only about the
walk to the nearest *forage*, and nothing says how far a home is from the shop it buys at.

*(The plan, as written before building; §4.1 is what was built and where it differs.)*

- **The home card** says the household's shop walk, in the sim's own words: *"Shops at Market 1 — 14 ticks' walk."* It
  reads the same rule the household shops by (market first within `market_detour_ticks`, else the nearest granary —
  `storage-and-distribution.md §14.9.7`, D671), so the card and the walk never disagree.
- **The housing brush** says the walk from the painted ground to the nearest shop, beside its forage warning, while
  painting.
- **A long walk is said, never forbidden** (D112): over a threshold the sentence says so (*"a long walk — a market
  nearer the homes would shorten it"*). The threshold is measured on Joe's saves (`ZzLogistics`), not typed.
- View and a read-only sim sentence; nothing in the sim's behaviour changes, so no golden moves.

### 4.1 Built (D744)

- **One rule, two readers.** `BehaviorSystem.FoodShopFrom(world, from, aTripsWorth)` is the shopper's own choice
  (D671: a stocked market, else a stocked storehouse nearer by more than `market_detour_ticks`, else any store with
  food, else a market with scraps), now named so the card can ask it; the shopper calls it unchanged.
  `SimWorld.ShopOf(household)` asks it from the home tile for a trip's worth — the household's target larder, by its
  cart's armfuls or its arms — and falls back to the nearest store that gives any food when nothing is stocked.
- **In tiles, not ticks.** The walk is said in tiles by the one cost field (§2.6, slopes and worn paths priced in),
  rounded — *"Granary 1, 6 tiles away"*. Ticks would change with the speed a player never sees.
- **The threshold is derived, not measured on saves** (a change from §4's plan): `VillageEconomy.MaxHomeToWorkTiles`,
  the economy's own budget for a walk from a home to its work (8 on the shipped config) — the same number the
  housing brush already used for the forage, so the brush and the card agree on what *far* means. Past it: *"— a long
  walk"*.
- **The card**, every fed line: *"Fed and warm — larder at 80%; they shop at Granary 1, 6 tiles away, when it falls to
  half."* / *"Fed — larder at 60%, topping it up from …"* / *"Nearly out of food — larder at 0%; somebody is going to
  …"*. A cold house says the cold, not the shop.
- **The brush** warns by the walk to the nearest shop once the village has one: *"That corner is N tiles from the
  nearest shop; the village budgets 8. Families there will spend their days walking for food."* — and by the forage,
  as before, until it has none. It warns and never forbids (D112).
- **Read-only:** nothing moves a villager; no golden moved.
- **Guards:** `ShopWalkTests` (4 — the card's shop is the shopper's, a long walk past the budget, the brush by the shop,
  the brush by the forage with no shop) and `ResidentialZoneTests`' warning in the shop's words; probe `shop walk:` —
  every home card read through the real view; ⚠️ the founding probe world houses nobody, so one family is **posed**
  beside the first store and put back, and the line refuses a pass where every card read was cold (it would have
  checked nothing — the first build of it did exactly that, its only card *"Nearly out of food"*, which did not then
  name the shop).

## 5. Item 3 — the QA checklist

`specs/phase-5-qa-checklist.md`: in the order a run meets the content (the milestone map's order), each line *what to
do* and *what you should see*, written in plain words for Joe. **Only his walk closes item 3** (D164/D168: the document
and the walk are different items). His walk is also item 5's played run (§7) and piece 5's play (§3).

## 6. Item 4 — the docs true

- **Every spec's status line** checked against §7 and the suite (D159, D640), not against itself. Findings listed in §6.1
  when done: each false claim and what it now says.
- **`content-inventory.md`** — Parts A and B are `main` at D526; refreshed to `main` now.
- **§4 and §6 agree**; **§6's *Current phase* line** true (it has been stale six times — D668's note).
- **The milestone map** gains a row for every Phase 5 milestone it lacks: clothing (the tailor at 50 leather handled),
  the workshop and the handcart (1,500 logs felled), the cottage and the manor (the family tree), the iron mine (the
  first iron tool).

### 6.1 Done (D744)

- **Status lines:** every systems spec checked against §7 D641–D743 and the branches (the art lane's specs left to it).
  **Nine false**, made true: `title-and-pause.md` and `seam-reach.md` (*"unmerged, unplayed"* — merged and pushed at
  D710); `environment-and-seasons.md` (clothing *"in build"* — merged D667); `workshop-and-the-carpenter.md` (*"Next:
  Joe plays on"* — merged D684); `logistics.md` (the handcart *"blocked on the workshop slice"* — built D690–D710);
  `handcart.md` (the trades' carts *"the next slice"* — merged D695); `professions.md` (the carpenter *"unplayed, 2,500
  logs"*, both new goods *"none shipped"* — played D682, 1,500, 50 to begin with D697); `per-site-yield.md` (a slice
  *"in progress"* on a branch long gone); and `content-inventory.md`'s own header (the cart's night *"unmerged"*).
- **`content-inventory.md`** Parts A and B read off the code on `main` at D743: 16 goods, 29 buildings, 17 trades, 10
  skills, 15 tech-tree nodes, the ground's height.
- **§4 and §6 agree:** §6's *Current phase* line was stale an eighth time (`main` = D699, logistics open, from D700 to
  D743) — rewritten.
- **The milestone map** gains five rows: the workshop and the handcart (~Year 20), the stone cottage (25–32), the
  manor (70–86), the iron mine (after the first iron tool), the tailor's shop (after 50 leather).

## 7. Item 5 — the four checks, and a played run with no `ERROR`

- **The four checks** (CLAUDE.md) on the merged tree, at the merge.
- **The played run:** Joe's QA walk, long enough to reach the content on the checklist. The game writes its own log to
  `src/Bclone.Game/logs/bclone-<date>-<time>.log` (every level, `ERROR` spelt in capitals); `run.bat` writes Godot's to
  `logs/run_<date>_<time>.log` (UTF-16). Both are scanned for `ERROR` after the walk.
- **Baseline, measured 2026-11-14 before anything else:** Joe's eight longest sessions since 2026-11-01 (7,400–153,000
  lines, the longest 14 MB) hold **0 `ERROR` and 0 `WARN`** in the game's log; `run.bat`'s six largest hold **0 `ERROR`**,
  and every one ends with the same two engine `WARNING`s at exit (*"1 RID of type CanvasItem was leaked"*, *"2 ObjectDB
  instances were leaked"*) — the family A45 belongs to, recorded here, not gating (the gate's bar is `ERROR`).
- ⚠️ **A45 in the probe (D744):** the headless probe dies in the .NET finalizer (*"FATAL: gchandle.is_released()"*, exit
  132) on **about three runs in four**, around the `deposits:` / `scenery:` lines — the same with and without this
  slice's six image disposals (3 of 4 without, 4 of 5 with), so the disposals are kept as correct and **D739's *"very
  likely fixed"* was wrong**. None of Joe's played logs holds it. The four checks take a run that reaches `done.`; the
  crash is HANDOFF's to chase, not the gate's.
