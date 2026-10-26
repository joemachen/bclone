## Ground (from ground.json)

| season | p0.1 | p1 | p50 | p99 | p99.9 |
|---|---|---|---|---|---|
| spring | L\* 67.7 `#9cab6f` | L\* 71.4 `#a8b57a` | L\* 77.6 `#bac594` | L\* 82.6 `#dccda8` | L\* 83.8 `#dfd0aa` |
| spring into summer | L\* 61.8 `#909b61` | L\* 66.8 `#95ab6b` | L\* 76.1 `#bdbf92` | L\* 82.6 `#dccca8` | L\* 83.8 `#dfd0ab` |
| summer | L\* 56.5 `#888b57` | L\* 61.8 `#9d9761` | L\* 74.5 `#c1b88f` | L\* 82.5 `#dccca8` | L\* 83.8 `#dfd0ab` |
| summer into fall | L\* 55.3 `#8e8552` | L\* 60.6 `#a4915b` | L\* 74.0 `#b9b984` | L\* 82.5 `#dccca8` | L\* 83.8 `#dfd0ab` |
| fall | L\* 54.5 `#96804d` | L\* 59.7 `#999250` | L\* 73.5 `#c6b386` | L\* 82.5 `#dccca8` | L\* 83.8 `#dfd0ab` |
| fall into winter | L\* 54.8 `#8f8356` | L\* 59.8 `#9a9162` | L\* 73.5 `#c4b38c` | L\* 82.5 `#dccca8` | L\* 83.8 `#dfd0ab` |
| winter | L\* 55.5 `#938364` | L\* 60.0 `#9f8f6d` | L\* 73.6 `#c3b48d` | L\* 82.5 `#dccca8` | L\* 83.8 `#dfd0ab` |
| winter into spring | L\* 61.8 `#9a976a` | L\* 65.8 `#ae9f74` | L\* 75.5 `#c0bb93` | L\* 82.5 `#dccca8` | L\* 83.8 `#dfd0ab` |

Darkest common ground: fall p1, L\* 59.7. Lightest: spring p99, L\* 82.6. Rarest dark: fall p0.1, L\* 54.5.
**3:1 below the darkest common ground needs L\* ≤ 29.3; nothing can reach 3:1 above the lightest** (white is 1.58:1).

## Failures — must read at 3:1, 57 of 60

Each needs its fill at L\* ≤ 29 or an ink outline. L\* is the colour as drawn (over the darkest ground, when it is translucent); *worst* is its lowest contrast against any season's p1, p50 or p99 ground.

| colour | drawn as | value | L\* | worst | against |
|---|---|---|---|---|---|
| `BerryColour` | forager's hut | `#5aa04a` | 60 | 1.00:1 | fall p1 |
| `Goods.Firewood` | firewood heap / crate | `#e08c3d` | 66 | 1.00:1 | winter into spring p1 |
| `ChildColour` | child | `#8fc7e8` | 78 | 1.00:1 | spring p50 |
| `MealMark` | meal mark | `#f5c76b @0.95` | 81 | 1.00:1 | fall into winter p99 |
| `ResidentialEdge` | housing zone edge | `#b98a52 @0.55` | 60 | 1.00:1 | summer into fall p1 |
| `Goods.Wheat` | wheat heap / crate | `#dbb852` | 76 | 1.00:1 | spring into summer p50 |
| `ArrivalMark` | arrival chevron | `#c9b48a` | 74 | 1.00:1 | summer into fall p50 |
| `FullStoreColour` | full-store ring | `#e8a13c` | 72 | 1.00:1 | spring p1 |
| `GhostWarned` | placement ghost: warned | `#e0b755` | 76 | 1.01:1 | spring into summer p50 |
| `JobKind.Quarrier` | quarrier workplace | `#a89e8a` | 65 | 1.01:1 | winter into spring p1 |
| `HomeColour` | house | `#b98a52` | 61 | 1.01:1 | summer into fall p1 |
| `Goods.Tools` | tools heap / crate | `#b8c2d1` | 78 | 1.01:1 | spring p50 |
| `GhostFine` | placement ghost: fine | `#7fd48a` | 78 | 1.01:1 | spring p50 |
| `Goods.Stone` | stone heap / crate | `#9e9ea3` | 65 | 1.02:1 | winter into spring p1 |
| `Goods.Fish` | fish heap / crate | `#6199c7` | 61 | 1.02:1 | summer into fall p1 |
| `StoneBridgeColour` | stone bridge | `#a8a49b` | 67 | 1.02:1 | spring into summer p1 |
| `TownHallColour` | town hall | `#b9b2a6` | 73 | 1.02:1 | fall into winter p50 |
| `FisheryColour` | fishery | `#6badc7` | 67 | 1.02:1 | spring into summer p1 |
| `WorkGroundEdge` | work ground edge | `#4a9ba8 @0.65` | 59 | 1.02:1 | fall into winter p1 |
| `IdleWorkplaceColour` | idle-workplace ring | `#7fb2d9` | 70 | 1.03:1 | spring p1 |
| `JobKind.Miller` | miller workplace | `#dbd1b2` | 84 | 1.04:1 | spring p99 |
| `HarvestEdge` | clear zone edge | `#d8892f @0.7` | 62 | 1.04:1 | summer p1 |
| `ElderColour` | elder | `#d9a05b` | 70 | 1.05:1 | spring p1 |
| `SiteColour` | building site | `#8f9aa8` | 63 | 1.05:1 | summer p1 |
| `GranaryColour` | granary | `#d8c56a` | 79 | 1.05:1 | spring p50 |
| `SelectedRing` | selection ring | `#f2c14e` | 80 | 1.06:1 | fall into winter p99 |
| `MarketColour` | market | `#c98f4a` | 64 | 1.07:1 | winter into spring p1 |
| `GhostRefused` | placement ghost: refused | `#d4685f` | 57 | 1.10:1 | fall p1 |
| `Goods.Bread` | bread heap / crate | `#b87a3d` | 57 | 1.11:1 | fall p1 |
| `JobKind.Baker` | baker workplace | `#b87a3d` | 57 | 1.11:1 | fall p1 |
| `WellColour` | well | `#8d8577` | 56 | 1.14:1 | fall p1 |
| `CattleColour` | cattle | `#e6dcc4` | 88 | 1.16:1 | spring p99 |
| `LibraryColour` | library | `#6a7fc9` | 55 | 1.20:1 | fall p1 |
| `Goods.Flour` | flour heap / crate | `#ebe0c7` | 90 | 1.21:1 | spring p99 |
| `DemolishColour` | coming down | `#b5714a` | 54 | 1.21:1 | fall p1 |
| `AdultColour` | adult | `#e8e2d4` | 90 | 1.22:1 | spring p99 |
| `Goods.Produce` | produce heap / crate | `#d15961` | 54 | 1.23:1 | fall p1 |
| `SailColour` | sail | `#efe6d2` | 92 | 1.27:1 | spring p99 |
| `WarehouseColour` | warehouse | `#8a7a63` | 52 | 1.30:1 | fall p1 |
| `Goods.Leather` | leather heap / crate | `#99734c` | 51 | 1.34:1 | fall p1 |
| `JobKind.Miner` | miner workplace | `#a36647` | 49 | 1.44:1 | fall p1 |
| `HutColour` | woodcutter | `#9a6b3f` | 49 | 1.44:1 | fall p1 |
| `JobKind.Smith` | smith workplace | `#6b7080` | 47 | 1.54:1 | fall p1 |
| `BridgeColour` | wooden bridge | `#8a6a48` | 47 | 1.55:1 | fall p1 |
| `GameColour` | wild game | `#8a6a3f` | 47 | 1.56:1 | fall p1 |
| `JobKind.Merchant` | merchant workplace | `#5f6f8c` | 47 | 1.59:1 | fall p1 |
| `Goods.Iron` | iron heap / crate | `#8c5c4c` | 44 | 1.75:1 | fall p1 |
| `FenceEdge` | household fence | `#7a5a3a @0.85` | 44 | 1.76:1 | fall p1 |
| `Goods.Meat` | meat heap / crate | `#a84247` | 42 | 1.85:1 | fall p1 |
| `TreeColour` | forester's lodge | `#2f6b3a` | 40 | 2.00:1 | fall p1 |
| `HullColour` | boat hull | `#7a5636` | 40 | 2.04:1 | fall p1 |
| `JobKind.Herdsman` | herdsman workplace | `#8a4b32` | 39 | 2.10:1 | fall p1 |
| `DestroyEdge` | destroy zone edge | `#a8202a @0.85` | 39 | 2.12:1 | fall p1 |
| `Goods.Logs` | logs heap / crate | `#735433` | 38 | 2.15:1 | fall p1 |
| `DestroyTone` | destroy ink | `#a8202a` | 37 | 2.26:1 | fall p1 |
| `PalisadeEdge` | seam palisade | `#5e4a36 @0.9` | 36 | 2.36:1 | fall p1 |
| `HeapEdge` | heap / crate outline | `#000000 @0.45` | 34 | 2.56:1 | fall p1 |

Passing at 3:1 (3): `CattleEdge` 3.4:1, `DoorColour` 3.6:1, `HullEdge` 3.7:1

## Washes — must be seen (CIE76 ΔE ≥ 5 against the middle ground, every season)

| colour | drawn as | value | ΔE | verdict |
|---|---|---|---|---|
| `DestroyColour` | destroy zone | `#a8202a @0.34` | 24.2 | ✅ |
| `HarvestColour` | clear zone | `#d8892f @0.26` | 10.3 | ✅ |
| `HarvestWaitingColour` | clear zone, waiting | `#d8892f @0.07` | 2.7 | ❌ too faint |
| `ResidentialColour` | housing zone | `#b98a52 @0.14` | 2.7 | ❌ too faint |
| `WearWash` | wear overlay | `#e0a868 @0.7` | 14.1 | ✅ |
| `WorkGroundColour` | work ground | `#4a9ba8 @0.16` | 7.4 | ✅ |
| `WorkGroundMine` | selected work ground | `#5fc8d8 @0.3` | 14.6 | ✅ |

## Ground kinds — the rule gives the ground L\* 50 and up (the grass spans 60–83)

Today's values, picked against slate: `Ground` itself is replaced by parchment × the grass, and the rest are what the terrain shader re-picks.

| colour | drawn as | value | L\* | contrast with the grass (mid) | in the band? |
|---|---|---|---|---|---|
| `Beyond` | outside the valley | `#202625` | 15 | 7.46:1 | ❌ in the things band — reads as a thing |
| `FieldColour` | ploughed field | `#4a3a2b` | 26 | 5.28:1 | ❌ in the things band — reads as a thing |
| `ForestColour` | woodland ground | `#2a3d2c` | 24 | 5.65:1 | ❌ in the things band — reads as a thing |
| `GridLine` | tile grid | `#343d3d` | 25 | 5.42:1 | ❌ in the things band — reads as a thing |
| `Ground` | grass ground (now parchment × the grass) | `#2b3332` | 20 | 6.28:1 | ❌ in the things band — reads as a thing |
| `IronColour` | iron seam | `#7a4a33` | 37 | 3.57:1 | ❌ below the band — neither ground nor thing |
| `MineFloor` | mine floor | `#9a6a52` | 49 | 2.24:1 | ❌ below the band — neither ground nor thing |
| `PackedPath` | packed trail | `#6e5843 @0.9` | 43 | 2.84:1 | ❌ below the band — neither ground nor thing |
| `QuarryFloor` | quarry floor | `#a39b8e` | 64 | 1.33:1 | ✅ |
| `RipeColour` | ripe field | `#b8933f` | 63 | 1.40:1 | ✅ |
| `RockColour` | stone seam | `#6b6459` | 43 | 2.84:1 | ❌ below the band — neither ground nor thing |
| `SaplingColour` | young wood | `#3d5433` | 33 | 4.05:1 | ❌ below the band — neither ground nor thing |
| `ShallowsColour` | river's edge | `#4c86a0` | 53 | 1.95:1 | ✅ |
| `SownColour` | sown field | `#4a5c33` | 37 | 3.55:1 | ❌ below the band — neither ground nor thing |
| `ValleyEdge` | valley border | `#485453` | 35 | 3.82:1 | ❌ below the band — neither ground nor thing |
| `WaterColour` | river | `#2f5f7a` | 38 | 3.35:1 | ❌ below the band — neither ground nor thing |
| `WornPath` | footpath | `#5b4b38 @0.6` | 50 | 2.18:1 | ✅ |

## Scenery — reported, not failed

| colour | drawn as | value | L\* | worst |
|---|---|---|---|---|
| `Boulder` | boulder | `#8d857a` | 56 | 1.14:1 |
| `OreLump` | ore lump | `#5c3424` | 26 | 3.33:1 |
| `SaplingCanopy` | sapling canopy | `#4f6b41` | 42 | 1.87:1 |
| `TreeCanopy` | tree canopy | `#3c5a3e` | 35 | 2.41:1 |

## The value-band rule, tested

**Darker only:** every building fill moved to L\* 29 (hue kept, chroma cut to the gamut). Of 231 pairs of building colours, **11 are within ΔE 10 today and 29 would be after** — at that lightness the gamut leaves little chroma, so hue stops telling buildings apart:
- town hall / well: ΔE 17 → 2
- stone bridge / town hall: ΔE 6 → 2
- smith workplace / building site: ΔE 16 → 2
- house / market: ΔE 9 → 3
- herdsman workplace / miner workplace: ΔE 11 → 3
- quarrier workplace / well: ΔE 10 → 3
- stone bridge / well: ΔE 12 → 4
- quarrier workplace / warehouse: ΔE 14 → 4
- baker workplace / market: ΔE 8 → 4
- miller workplace / warehouse: ΔE 32 → 4
- miller workplace / quarrier workplace: ΔE 19 → 5
- quarrier workplace / town hall: ΔE 9 → 5
- house / woodcutter: ΔE 13 → 5
- coming down / miner workplace: ΔE 7 → 5
- house / baker workplace: ΔE 10 → 6
- coming down / herdsman workplace: ΔE 16 → 6
- warehouse / well: ΔE 8 → 7
- quarrier workplace / stone bridge: ΔE 7 → 7
- woodcutter / market: ΔE 19 → 7
- woodcutter / baker workplace: ΔE 13 → 8
- miller workplace / well: ΔE 29 → 8
- town hall / warehouse: ΔE 22 → 8
- merchant workplace / smith workplace: ΔE 9 → 9
- woodcutter / miner workplace: ΔE 9 → 9

**Ink outline:** `#2a2018` (L\* 13) is 4.98:1 against the darkest common ground and 4.16:1 against the rarest dark (p0.1) — every outlined thing passes whatever its fill.
Fills that pass on their own today: 0 of 22.

## Out of scope — UI (11)

`Hand`, `JobKind.Builder`, `JobKind.Farmer`, `JobKind.Fisher`, `JobKind.Forager`, `JobKind.Forester`, `JobKind.Hunter`, `JobKind.Marketer`, `JobKind.Woodcutter`, `Unchosen`, `Undo`
