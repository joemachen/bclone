# W3's cask and the brewer's and keeper's figures (A203)

> **Status: 🔨 BUILT ON `art` (A204, 2026-11-23) — Joe: *"All as recommended"* on §6's five calls; ✅ played, MERGED INTO
> `main` at his word — *"merge"* (A205, merge `aa43220`); ✅ pushed at his word (A206). Specced and mocked A203.** Art lane (CLAUDE.md
> *Two tracks*), branch `art`. View only: nothing in `src/Kith.Sim`, `data/` or `tests/` changes. Mock:
> `D:\Projects\brushes\tavern\figures\w3_figures_mock.jpg`, painted by the game's own `PersonPainter` — today's file and a
> proposed copy (`PersonPainter.cand.cs`, made by `cand.py`), run outside the game by a scratch harness (`harness/`), so
> every figure on the sheet is the pixels the game would draw.

## 1. What was asked

Joe, via the coordinator, 2026-11-23: *"cask and figures"*. The rest of W3's request after the two buildings (A199–A202):
`tavern.md §9` — *carrying ale (the keeper with a cask — the one new carried look)*; HANDOFF's ⏸️ *ART LANE — W3'S TAVERN
AND BREWERY* — the keeper's **cask** in the arms, the **brewer's and keeper's figures** (stand-ins in `PersonPainter`), their
trade colours in `TradeGlyph` (stand-ins too).

## 2. What the map draws today

- **The brewer** (`PersonPainter.LookOf`, marked *"W3's stand-ins"*): the brewer's amber gown, a linen coif, a linen apron,
  no tool — **the baker in all but a shade** (the baker: a coif, a linen apron, the crust's brown, the peel). Their trade
  colours sit 12.5 ΔE apart, and the painter pushes both toward the same ochre pigment; at play size the two cannot be
  told apart (the mock's lineup).
- **The keeper:** the keeper's wine red, bareheaded (a coif or red cap on some, by the person), a linen apron, calf length,
  no tool. It already reads — the only wine-red gown with a white apron — but it is marked a stand-in.
- **Ale in the arms:** `GoodColour` has no row for ale, so it falls to the default — **a grey oval in the arms**, the look
  of any good nobody has drawn.
- **Where they are seen:** the brewer at work is indoors (`WorkingABatch` in a building with a door, A139), so the brewer is
  seen walking — to the brewery, and carrying barley and ale. The keeper keeping the tavern is indoors (`Indoors`), so the
  keeper is seen fetching ale (empty-handed) and **carrying ale** back (`CarryingAle`) — the cask is the keeper's look. Any
  porter hauling ale between stores carries it too.

## 3. The look (mock: `w3_figures_mock.jpg`)

### 3.1 The brewer — call 1

- **A leather apron** (`#6a4a34`, the smith's, on the brewer's amber) in place of the linen one — the one thing that parts
  the brewer from the baker at every zoom: dark against white.
- **Bareheaded**, as most trades are (a coif or red cap on some, by the person, the painter's rule), not the baker's coif.
- **A mash oar** — a long pole taller than the head with a slotted wooden blade at its foot — held on the way to the
  brewery (`TravelingToABatch`, *the tool carried along*), as the baker carries the peel. No working poses: at work the
  brewer is indoors.

### 3.2 The keeper — call 2

Two touches, the rest kept: **the gown to the ankle** (a townsperson's, as the merchant's and the teacher's — a different
outline from the calf-length trades beside it) and **a linen cloth over one shoulder**, the tavern keeper's mark — on the
front, the back and the side.

### 3.3 The cask — call 3

**A small oak cask held upright in both arms** — bellied, two iron hoops, its lid showing, the hands on its belly — in the
yard's casks' wood and iron (`#8a6440`, `#4b4e54`, A200), for **anyone carrying ale** (the keeper, a porter, the brewer).
Front and side; walking away it is hidden by the body, as every armful is (stone, bread, cloth). Held in the arms as asked,
not on the shoulder.

### 3.4 Their colours — call 4

**Keep D845's two** — the brewer's ale amber `#9a5e14` and the keeper's wine red `#8a2f3e` — as chosen, not stand-ins. They
are already on the two icons (A181–A183), the two roofs and the plain map (A200). Nearest trades: the brewer 12.5 ΔE from
the baker (parted by the apron, §3.1), 19.1 from the marketer; the keeper 22.1 from the herdsman, 23.0 from the forager.
Only the two *"stand-in"* comments change (`TradeGlyph.cs`, `PersonPainter.cs`); no colour moves.

## 4. Code (after the calls)

| File | Change | Lane |
|---|---|---|
| `PersonPainter.cs` | the two rows of `LookOf`; `Tool.Oar` drawn; `TradeLook.Cloth` and the cloth drawn; the cask in `LoadFront` | ⛔ call 5 |
| `VillageMap.PeopleProbe.cs` | the probe line (§5) riding on the people's line (art trap 73) | ⛔ call 5 |
| `TradeGlyph.cs` | the two *"stand-ins"* comments made true (call 4) — no colour changes | ⛔ call 5 |

Nothing derivable is rebuilt per frame: a figure is painted once per `FigureKey` and kept, as today; the new looks only add
to what a key paints. No new texture, so no import.

## 5. Probe

A new line, `brewer and keeper:` — the brewer's figure differs from the baker's (dark apron pixels where the baker's are
white), the oar drawn only with the tool out and above the head; the keeper's gown reaching the ankle and the cloth's linen
at the shoulder front, back and side, none on a child; a figure carrying ale has the cask's oak and hoop pixels at the chest
front and side, none walking away; the two trade colours as §3.4.

## 6. Joe's calls (⏸️, each recommended)

1. **The brewer as mocked** — leather apron, bareheaded, the mash oar carried to the brewery. ⭐ *Recommended.*
2. **The keeper's two touches** — the gown to the ankle, a linen cloth over the shoulder. ⭐ *Recommended* (or leave the
   keeper as today, which already reads).
3. **The cask** — an oak cask upright in both arms, for anyone carrying ale. ⭐ *Recommended.*
4. **Keep the two colours** (amber `#9a5e14`, wine red `#8a2f3e`) as chosen. ⭐ *Recommended.*
5. **His OK for the three files in §4**, outside the lane's table (`PersonPainter.cs` was the lane's at A93 and A130; the
   other two as A200). The systems lane (`slice/library-borrowing`) has touched none of them. ⭐ *Recommended.*

## 7. Not in this piece

A brewer's working pose (they are unseen at work), ale's colour in `GoodsPalette` (`#9a5e14` since D845, still marked
*"a stand-in until the art lane takes its colour from the painting"*), a jug or tankard in the keeper's hand, the cask on the shoulder.

## 8. As built (A204)

Joe, 2026-11-23: *"All as recommended"* — the five calls of §6 as written.

- **Code:** `PersonPainter.cs` — the brewer's and keeper's rows of `LookOf` (the leather apron, `Tool.Oar`; the ankle and
  `TradeLook.Cloth`), the oar in `ToolBehind`, `ShoulderCloth` after the cloak, the cask in `LoadFront` (`Goods.Ale`), the
  cask's oak and iron the yard's; `TradeGlyph.cs` — the two *"stand-ins"* comments made true, no colour moved;
  `VillageMap.TavernFiguresProbe.cs` (new) — the probe line, hooked onto `painted villagers:` by one line of
  `VillageMap.PeopleProbe.cs` (art trap 73).
- **The mock is the build:** the built `PersonPainter.cs` differs from the mock's copy only by comments, so every figure
  on `w3_figures_mock.jpg` is what the game paints.
- **Probe** `brewer and keeper:` — colours are matched within 22 a channel: the painter's wash darkens a fill by ~15
  (linen `#f0ebdf` paints `#e1dcd0`). The cask hidden walking away and no grey oval are compared with the same figure
  carrying nothing, not counted. Red-check 19 mutants, 19 red, each a ⛔ on the part it aims at, none a crash; ⚠️ one sub-check scored zero on its first run and was fixed: *no grey oval* matched the grey within 8 a channel, which the wash puts out of reach, so the break was caught by the oak count instead. Matched within 22 and that mutant re-run alone: red (115 grey px against 43 with empty arms).
