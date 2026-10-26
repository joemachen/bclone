# Spec: World colours on parchment — what fails, and the rule that fixes it

**Decision:** D635 (Joe, 2026-10-26: *"§6.0: Yes, move the game's ground to parchment."*); this slice D636.
**Status:** 📏 **MEASURED, NOTHING CHANGED — the failure list and a recommendation; the rule is Joe's call (§7).** No colour
in the game moved. The tools are `tools/legibility/` (§8); the full table they print is `tools/legibility/report.md`.
Owner: Joe + Claude Code.

Built on `grass-look.md` (the approved ground) and `seasonal-grass.md §6.0` (why the ground has to move).

---

## 1. Scope — world colours only; the UI stays slate

**In:** everything `VillageMap` and its partials draw on the valley, plus `ValleyTexture`'s ground kinds — 60 colours
that must be readable, 7 washes, 17 ground kinds, 4 kinds of scenery. Of those 60: 41 `VillageMap` colours, 7 trades'
colours from `TradeGlyph` (the barn, the post, the smithy… drawn as their trade, D449/D596) and 12 goods' colours from
`GoodsPalette` (heaps and crates).
**Out:** panels, the bar, cards, the tech tree, menus — and the UI-only colours: `BuildingGlyph.Unchosen`, `ToolGlyph`'s
two, and nine trades' icon colours that never reach the map.

⚠️ **The two sets are not separate in the code today, and that is the first finding.** UI code reads about 30 world
colours: the bar's building glyphs, the cards' portraits (D376), `MomentGlyph`, `TradeGlyph`'s own icons, and the
minimap (`BuildingGlyph.cs`, `Main.Cards.cs`, `Main.Trade.cs`, `Main.cs`, `Minimap.cs`, `MomentGlyph.cs`, `ToolGlyph.cs`,
`TradeGlyph.cs`). That sharing was deliberate: one palette, so the card and the map agree (the minimap's comment says so).
Once the world's colours move to suit parchment, every UI picture of a world thing breaks on slate. **Separating them
means deciding what a UI picture of a world thing sits on** (§7 call 3), not giving the UI a second palette that drifts.

## 2. The ground

`tools/legibility/ground.py` renders the approved look (`grass_approved.tres`) through the Python mirror at about one
mask texel a pixel (33 px a tile), in 48 windows of 256² scattered over ±240 tiles (so macro variation reaches both its
ends), with the lab's paper grain. That covers the four seasons and the halfway point of each blend.

| season | p0.1 | p1 | p50 | p99 | p99.9 |
|---|---|---|---|---|---|
| spring | L\* 67.7 | 71.4 | 77.6 | 82.6 | 83.8 |
| spring into summer | 61.8 | 66.8 | 76.1 | 82.6 | 83.8 |
| summer | 56.5 | 61.8 | 74.5 | 82.5 | 83.8 |
| summer into fall | 55.3 | 60.6 | 74.0 | 82.5 | 83.8 |
| fall | **54.5** | **59.7** | 73.5 | 82.5 | 83.8 |
| fall into winter | 54.8 | 59.8 | 73.5 | 82.5 | 83.8 |
| winter | 55.5 | 60.0 | 73.6 | 82.5 | 83.8 |
| winter into spring | 61.8 | 65.8 | 75.5 | 82.5 | 83.8 |

(The hex values are in `report.md`.) **The ground fills L\* ~55–84 in every season; its common span (p1–p99) is 60–83.**
The light end is parchment under grain and never moves; the dark end is pooled grass, darkest in fall.

Two facts decide everything below:

- **Nothing can reach 3:1 against the ground by being lighter than it.** Pure white is 1.58:1 against the lightest ground.
- **To reach 3:1 against the darkest common ground (fall, p1) by being darker, a colour needs L\* ≤ 29.**

## 3. The bar each colour is held to (mine — say if you want it set differently)

| tier | what | bar |
|---|---|---|
| **read** | things you find and click (buildings, people, beasts, boats, heaps), the marks that report on them (rings, the meal and arrival marks), the lines that bound something (zone edges, fences, palisades, ghosts, outlines) | **3:1** against every season's p1, p50 and p99 ground — WCAG 2.2 SC 1.4.11, *non-text contrast* |
| **wash** | a translucent tint over ground (zones, wear) | seen: CIE76 **ΔE ≥ 5** against the middle ground |
| **ground** | the kinds of ground (water, woods, seams, fields, paths, floors) | inside the ground's band (**L\* ≥ 50**), so it never competes with a thing |
| **quiet** | scenery that dresses the ground (canopies, boulders, ore) | reported, never failed |

A textured ground means a colour inside the ground's own lightness band always lands on *some* patch that matches it
(contrast ~1:1). That is why so many rows below bottom out at 1.0x.

## 4. The failure list — before anything changes

**Read tier: 57 of 60 fail.** Only three pass, all dark outlines that already exist: `CattleEdge` 3.4:1, `HullEdge` 3.7:1,
and `DoorColour` 3.6:1 (which sits on a building, not on the ground). In groups (worst contrast in brackets; the full
table is `report.md`):

- **Every building fill fails** (1.0–2.1:1): house, granary, warehouse, market, woodcutter, library, town hall, well, both
  bridges, fishery, forager's hut, forester's lodge, site, coming-down, and the seven trades.
- **Every villager fails:** adult 1.22 (L\* 90, near parchment, as D630 saw), child 1.00, elder 1.05. So do cattle (1.16),
  game (1.56), the boat's hull (2.04) and its sail (1.27).
- **Every heap and crate fails:** all twelve goods, 1.0–2.15:1, and the heap's own edge (black at 45 %) is only 2.56:1.
- **Every mark fails:** full-store ring 1.00, idle-workplace ring 1.03, selection ring 1.06, meal mark 1.00, arrival
  chevron 1.00.
- **Every zone edge and ghost fails:** housing edge 1.00, clear 1.04, work ground 1.02, destroy 2.12; ghost fine 1.01,
  warned 1.01, refused 1.10; the destroy ink 2.26.
- **Fences fail:** the household fence 1.76, the seam palisade 2.36.

**Washes: 2 of 7 too faint.** The housing zone (ΔE 2.7) and the waiting clear zone (ΔE 2.7). Destroy 24, wear 14,
selected work ground 15, clear 10 and work ground 7 are seen.

**Ground kinds: 13 of 17 sit below the band.** These are the slate-era darks: woods L\* 24, ploughed field 26, sapling 33,
sown 37, river 38, iron 37, rock 43, packed trail 43, mine floor 49, plus `Beyond`, the valley edge, the grid and slate
`Ground` itself. On parchment each would read as a *thing*, a dark shape, not as land. In the band already: quarry floor
64, ripe field 63, shallows 53, footpath 50.

**Scenery, reported:** the tree canopy L\* 35 (2.41:1), the sapling canopy 42 (1.87:1), the boulder 56 (1.14:1), the
ore lump 26 (3.33:1).

## 5. The proposed rule, tested

> *The ground owns the light-to-mid value band; interactive things sit clearly darker or carry an ink outline.*

**The first half isn't a choice; the numbers force it.** The ground spans L\* 55–84, and nothing lighter than it can
reach 3:1 (§2). Its only workable form: **the ground and everything that is ground at L\* ≥ 50; "clearly darker" means
L\* ≤ 29**. L\* 29–50 is no-man's land: a fill there reads as neither.

**"Clearly darker" tested on the building fills.** Each of the 22 fills was moved to L\* 29, keeping its hue and cutting
chroma only as far as the gamut needs. **Pairs within ΔE 10 of each other go from 11 of 231 today to 29.** At that
lightness the gamut leaves too little chroma for hue to tell buildings apart. Town hall / well and stone bridge / town
hall go to ΔE 2; smith / site 2; house / market 3; herdsman / miner 3. **Darker alone fails for things with an area,
whose colour is their identity.**

**Darker tested on lines and marks.** It works. Zone edges, rings and ghosts keep their hues at L\* 29 (ΔE 38–80 between
most pairs), and so do the three kinds of villager (adult / child 30, adult / elder 34, child / elder 64). **Two pairs
collapse:** the housing and clear-zone edges (ΔE 24 → 5) and the full-store and selection rings (ΔE 15 → 8). Both were
the closest pairs on slate already; they need a second difference, such as a dash or a width.

**The ink outline tested.** Dark sepia `#2a2018` (L\* 13) is **4.98:1 against the darkest common ground and 4.16:1 against
the rarest (fall p0.1)**, and 11:1 against bare parchment. **An outlined thing passes whatever its fill**, so the fill is
free to carry identity at any lightness.

**⭐ Recommendation: adopt the rule, made exact, in three bands.**

1. **Ground: L\* ≥ 50.** The grass already sits there. Every ground kind is re-picked into it by the terrain shader:
   woods, seams, fields, the river, paths, floors, the valley's edge and outside.
2. **Things with an area — buildings, heaps, beasts, boats — wear an ink outline**, and their fills keep their hues.
   Darker-only would make 29 building pairs confusable.
3. **Lines, marks and the villager dots go dark: L\* ≤ 29, hue kept.** They are thin or tiny, so an outline would leave
   no fill; darkened, they still separate by hue. Re-separate the two collapsing pairs by line style.

The rule as stated says *interactive*; I'd extend it to **"no ground-kind may sit below the band"**. Otherwise the woods
(L\* 24 today) would be the biggest dark mass on the map and read as the most important thing on it.

**Alternatives considered:**
- **A light halo** round things: unavailable, since even white is 1.58:1 on the light end.
- **Darker only**: fails identity (above).
- **Narrowing the ground** by lowering the grass strength: that changes the look you approved in D632, and the light
  end is the parchment itself, so it would not help light things anyway.

## 6. What the ground band means for snow

Snow is lighter than parchment (L\* ~95 against 86), so the ground's band rises in winter. Dark things gain contrast and
light ones lose it: a reason more for the dark and outlined bands. How the terrain shader draws snow is
`terrain-shader.md §2`.

## 7. Open — Joe's calls

1. **The rule:** as recommended in §5 (three bands), as you stated it, or something else?
2. **The bar:** 3:1 (WCAG non-text) for the read tier, and the washes' ΔE 5. The waiting clear zone was *faint on
   purpose* (D343; its edge stays at full strength), so it may stay faint. The housing zone's 2.7 probably should not.
3. **What a UI picture of a world thing sits on.** My recommendation: a **parchment chip**. The bar's glyphs, the cards'
   portraits and the minimap draw world things on a small parchment ground, so the world palette stays one palette
   (D376, the minimap's "one palette and not two"). The alternative is a second, slate-tuned copy of ~30 colours, which
   drifts. *Either way the UI's own chrome stays slate.*
4. **Trees.** Canopies are scenery, reported and not failed, but on parchment they decide how the woods look. One
   option is ink-outlined canopies over an in-band green ground (the hand-drawn references in `art/`); another is
   canopies as mid-tone washes without lines. Your eye, before the terrain shader.
5. **D68 still names *Foundation*** (a painted 3D look) as the art direction's destination. Parchment and `art/`'s
   hand-drawn references point at a 2D drawn map. D68's two observations still hold (intent drawn as painted regions;
   a thin HUD). Whether its destination is retired is yours to say; I have not changed it.

## 8. Re-running

```bash
python -X utf8 -I -B tools/legibility/ground.py   # ~2 min; writes tools/legibility/ground.json from grass_approved.tres
python -X utf8 -I -B tools/legibility/check.py > tools/legibility/report.md
```

`check.py` reads every colour from the view's source (`VillageMap*.cs`, `TradeGlyph.cs`, `GoodsPalette.cs`,
`BuildingGlyph.cs`, `ToolGlyph.cs`). A colour it has not been told about prints as **unclassified**, so a new one cannot
slip past unchecked. That was red-checked: a colour planted in a copy of `VillageMap.cs` was reported as unclassified
(1 of 1), and the real source reports none. Re-run `ground.py` whenever `grass_approved.tres` changes.
