"""Every world colour against the parchment ground, every season (`specs/world-legibility.md`).

Reads the colours straight from the view's source, so a colour added later is checked (or reported unclassified) without
anyone remembering to list it, and the ground from `ground.json` (`ground.py`). Prints the failures first, then the
evaluation of the value-band rule, as Markdown.

    python -X utf8 -I -B tools/legibility/check.py
"""
from __future__ import annotations

import itertools
import json
import pathlib
import re
import sys

import numpy as np

HERE = pathlib.Path(__file__).resolve().parent
GAME = HERE.parent.parent / "src" / "Bclone.Game"
GROUND = json.loads((HERE / "ground.json").read_text(encoding="utf-8"))

# ---- The inventory: where each colour is drawn ------------------------------------------------------------------
#
# Tiers say what a colour must do on the ground, not how important it is:
#   read  — a thing the player finds and clicks, or a mark that reports on one, or a line that bounds a zone:
#           3:1 against every patch of ground it can sit on (WCAG 2.2 SC 1.4.11, non-text contrast).
#   wash  — a translucent tint over ground: must be seen (CIE76 dE >= 5 against the ground under it) without hiding it.
#   ground— a kind of ground (the valley bake, worked ground, paths): judged against the value band, not against itself.
#   quiet — scenery that dresses the ground (canopies, boulders): reported, never failed.
#   ui    — panels, bar and card glyphs: out of scope (they stay on slate).

WORLD = {
    # ground kinds — the bake and the worked-ground pass (`ValleyTexture`, `ColourOf(Terrain)`)
    "Ground": "ground", "WaterColour": "ground", "ShallowsColour": "ground", "ForestColour": "ground",
    "SaplingColour": "ground", "RockColour": "ground", "IronColour": "ground", "FieldColour": "ground",
    "SownColour": "ground", "RipeColour": "ground", "QuarryFloor": "ground", "MineFloor": "ground",
    "WornPath": "ground", "PackedPath": "ground", "Beyond": "ground", "ValleyEdge": "ground", "GridLine": "ground",
    "Parchment": "ground",
    # things with an area — the fill carries identity and is judged by the outline drawn round it (D637): "fill:<edge>"
    "HomeColour": "fill:Ink", "GranaryColour": "fill:Ink", "WarehouseColour": "fill:Ink", "MarketColour": "fill:Ink",
    "HutColour": "fill:Ink", "LibraryColour": "fill:Ink", "TownHallColour": "fill:Ink", "WellColour": "fill:Ink",
    "BridgeColour": "fill:Ink", "StoneBridgeColour": "fill:Ink", "FisheryColour": "fill:Ink", "BerryColour": "fill:Ink",
    "TreeColour": "fill:Ink", "SiteColour": "fill:SiteEdge", "DemolishColour": "fill:DemolishEdge",
    "CattleColour": "fill:CattleEdge", "HullColour": "fill:HullEdge", "SailColour": "fill:HullEdge",
    # the people and the wild game: dots, dark
    "AdultColour": "read", "ChildColour": "read", "ElderColour": "read", "GameColour": "read",
    # lines and marks
    "Ink": "read", "SiteEdge": "read", "DemolishEdge": "read",
    "FenceEdge": "read", "PalisadeEdge": "read", "CattleEdge": "read", "HullEdge": "read", "HeapEdge": "read",
    "DoorColour": "read", "FullStoreColour": "read", "IdleWorkplaceColour": "read", "SelectedRing": "read",
    "MealMark": "read", "ArrivalMark": "read",
    "ResidentialEdge": "read", "HarvestEdge": "read", "DestroyEdge": "read", "WorkGroundEdge": "read",
    "GhostFine": "read", "GhostWarned": "read", "GhostRefused": "read",
    # washes
    "ResidentialColour": "wash", "HarvestColour": "wash", "HarvestWaitingColour": "wash", "DestroyColour": "wash",
    "WorkGroundColour": "wash", "WorkGroundMine": "wash", "WearWash": "wash",
    # scenery
    "TreeCanopy": "quiet", "SaplingCanopy": "quiet", "Boulder": "quiet", "OreLump": "quiet",
}

# Trades and goods drawn ON the map (`VillageMap.ColourOf(JobKind)` hands these to TradeGlyph; heaps and crates use
# GoodsPalette). The same colours are the bar's and the cards' icons — shared, which is a finding of its own.
WORLD_TRADES = {"Smith", "Quarrier", "Miner", "Miller", "Baker", "Herdsman", "Merchant"}
# BuildingGlyph / ToolGlyph — the bar's icons only; DestroyTone is the red brush's glyph and the base the destroy wash is
# made from, never drawn on the valley by itself (its edge is DestroyEdge).
UI = {"Unchosen", "Hand", "Undo", "Fire", "DestroyTone"}

WHAT = {
    "Ground": "grass ground (now parchment × the grass)", "WaterColour": "river", "ShallowsColour": "river's edge",
    "ForestColour": "woodland ground", "SaplingColour": "young wood", "RockColour": "stone seam",
    "IronColour": "iron seam", "FieldColour": "ploughed field", "SownColour": "sown field", "RipeColour": "ripe field",
    "QuarryFloor": "quarry floor", "MineFloor": "mine floor", "WornPath": "footpath", "PackedPath": "packed trail",
    "Beyond": "outside the valley", "ValleyEdge": "valley border", "GridLine": "tile grid",
    "HomeColour": "house", "GranaryColour": "granary", "WarehouseColour": "warehouse", "MarketColour": "market",
    "HutColour": "woodcutter", "LibraryColour": "library", "TownHallColour": "town hall", "WellColour": "well",
    "BridgeColour": "wooden bridge", "StoneBridgeColour": "stone bridge", "FisheryColour": "fishery",
    "BerryColour": "forager's hut", "TreeColour": "forester's lodge", "SiteColour": "building site",
    "DemolishColour": "coming down", "AdultColour": "adult", "ChildColour": "child", "ElderColour": "elder",
    "CattleColour": "cattle", "GameColour": "wild game", "HullColour": "boat hull", "SailColour": "sail",
    "FenceEdge": "household fence", "PalisadeEdge": "seam palisade", "CattleEdge": "cattle outline",
    "HullEdge": "hull outline", "HeapEdge": "heap / crate outline", "DoorColour": "door notch",
    "FullStoreColour": "full-store ring", "IdleWorkplaceColour": "idle-workplace ring", "SelectedRing": "selection ring",
    "MealMark": "meal mark", "ArrivalMark": "arrival chevron", "ResidentialEdge": "housing zone edge",
    "HarvestEdge": "clear zone edge", "DestroyEdge": "destroy zone edge", "WorkGroundEdge": "work ground edge",
    "GhostFine": "placement ghost: fine", "GhostWarned": "placement ghost: warned",
    "GhostRefused": "placement ghost: refused", "DestroyTone": "destroy ink",
    "ResidentialColour": "housing zone", "HarvestColour": "clear zone", "HarvestWaitingColour": "clear zone, waiting",
    "DestroyColour": "destroy zone", "WorkGroundColour": "work ground", "WorkGroundMine": "selected work ground",
    "WearWash": "wear overlay", "TreeCanopy": "tree canopy", "SaplingCanopy": "sapling canopy", "Boulder": "boulder",
    "OreLump": "ore lump",
}

INK = (0x2A / 255, 0x20 / 255, 0x18 / 255)  # a candidate ink: dark sepia, the art direction's line colour


# ---- Reading the source -----------------------------------------------------------------------------------------

def hexrgb(h: str):
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


def read_colours():
    named, ui = {}, {}
    sources = sorted(GAME.glob("VillageMap*.cs")) + [GAME / f for f in ("TradeGlyph.cs", "GoodsPalette.cs",
                                                                      "BuildingGlyph.cs", "ToolGlyph.cs")]
    for path in sources:
        text = path.read_text(encoding="utf-8")
        for m in re.finditer(r'Color (\w+) = new(?:Color)?\("#([0-9a-fA-F]{6})"(?:, ([0-9.]+)f)?\);', text):
            named[m.group(1)] = (hexrgb(m.group(2)), float(m.group(3) or 1), path.name)
        for m in re.finditer(r'Color (\w+) = new(?:Color)?\(([0-9.]+)f, ([0-9.]+)f, ([0-9.]+)f(?:, ([0-9.]+)f)?\);', text):
            named[m.group(1)] = ((float(m.group(2)), float(m.group(3)), float(m.group(4))), float(m.group(5) or 1),
                                 path.name)
        for m in re.finditer(r'Color (\w+) = (\w+) with \{ A = ([0-9.]+)f \};', text):
            base = named[m.group(2)]
            named[m.group(1)] = (base[0], float(m.group(3)), path.name)
        for m in re.finditer(r'Color (\w+) = ([A-Z]\w+);', text):  # an alias: `Ground = Parchment`, `HeapEdge = Ink`
            named[m.group(1)] = named[m.group(2)][:2] + (path.name,)
        for m in re.finditer(r'(JobKind|Goods)\.(\w+) => new Color\((?:"#([0-9a-fA-F]{6})"|([0-9.]+)f, ([0-9.]+)f, ([0-9.]+)f)\)', text):
            rgb = hexrgb(m.group(3)) if m.group(3) else (float(m.group(4)), float(m.group(5)), float(m.group(6)))
            key = f"{m.group(1)}.{m.group(2)}"
            if m.group(1) == "Goods" or m.group(2) in WORLD_TRADES:
                named[key] = (rgb, 1.0, path.name)
            else:
                ui[key] = (rgb, 1.0, path.name)
    for k in list(named):
        if k in UI:
            ui[k] = named.pop(k)
    return named, ui


# ---- Colour science ---------------------------------------------------------------------------------------------

def linear(c):
    c = np.asarray(c, np.float64)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def lum(c) -> float:
    return float(linear(c) @ np.array([0.2126, 0.7152, 0.0722]))


def contrast(a, b) -> float:
    la, lb = lum(a), lum(b)
    return (max(la, lb) + 0.05) / (min(la, lb) + 0.05)


M = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]])
WHITE = np.array([0.95047, 1.0, 1.08883])


def lab(c):
    xyz = (M @ linear(c)) / WHITE
    f = np.where(xyz > 216 / 24389, np.cbrt(xyz), (24389 / 27 * xyz + 16) / 116)
    return np.array([116 * f[1] - 16, 500 * (f[0] - f[1]), 200 * (f[1] - f[2])])


def from_lab(L, a, b):
    fy = (L + 16) / 116
    fx, fz = fy + a / 500, fy - b / 200
    xyz = np.array([fx ** 3 if fx ** 3 > 216 / 24389 else (116 * fx - 16) / (24389 / 27),
                    fy ** 3 if L > 8 else L / (24389 / 27),
                    fz ** 3 if fz ** 3 > 216 / 24389 else (116 * fz - 16) / (24389 / 27)]) * WHITE
    rgb = np.linalg.solve(M, xyz)
    return np.where(rgb <= 0.0031308, rgb * 12.92, 1.055 * np.power(np.clip(rgb, 0, None), 1 / 2.4) - 0.055)


def lstar(c) -> float:
    return float(lab(c)[0])


def de(a, b) -> float:
    return float(np.linalg.norm(lab(a) - lab(b)))


def over(c, alpha, g):
    return tuple(alpha * np.asarray(c) + (1 - alpha) * np.asarray(g))


def hexs(c, alpha=1.0) -> str:
    s = "#" + "".join(f"{int(round(v * 255)):02x}" for v in np.clip(c, 0, 1))
    return s if alpha >= 1 else f"{s} @{alpha:g}"


# ---- The ground ---------------------------------------------------------------------------------------------------

ENDS = ("1", "50", "99")  # the darkest, the middle and the lightest patches a thing commonly sits on
GROUND_FLOOR = 50.0  # the rule's band for ground kinds: L* 50 and up (the grass itself spans ~55-84)
grounds = [(s["name"], p, tuple(s["percentiles"][p]["rgb"]), s["percentiles"][p]["L"])
           for s in GROUND["seasons"] for p in ENDS]
dark_end = min(grounds, key=lambda g: g[3])
light_end = max(grounds, key=lambda g: g[3])
extreme_dark = min(((s["name"], s["percentiles"]["0.1"]) for s in GROUND["seasons"]), key=lambda x: x[1]["L"])


def worst_against_ground(rgb, alpha):
    worst = None
    for name, p, g, _ in grounds:
        c = contrast(over(rgb, alpha, g), g)
        if worst is None or c < worst[0]:
            worst = (c, name, p)
    return worst


def darkest_target(ratio=3.0) -> float:
    """The L* a fill must be at or below to reach `ratio` against the darkest common ground (p1, every season)."""
    y = (lum(dark_end[2]) + 0.05) / ratio - 0.05
    return y_to_lstar(y) if y > 0 else 0.0


def y_to_lstar(y: float) -> float:
    return float(116 * np.cbrt(y) - 16 if y > 216 / 24389 else y * 24389 / 27)


def darkened(rgb, L):
    """`rgb` moved to lightness L, keeping its hue, losing chroma only as far as the gamut needs."""
    _, a, b = lab(rgb)
    for k in np.linspace(1, 0, 101):
        c = from_lab(L, a * k, b * k)
        if np.all(c >= -1e-6) and np.all(c <= 1 + 1e-6):
            return tuple(np.clip(c, 0, 1))
    return (L / 100,) * 3


# ---- The report -----------------------------------------------------------------------------------------------

def main() -> int:
    named, ui = read_colours()
    unclassified = sorted(k for k in named if k not in WORLD and not k.startswith(("Goods.", "JobKind.")))
    p = print

    p("## Ground (from ground.json)\n")
    p("| season | p0.1 | p1 | p50 | p99 | p99.9 |\n|---|---|---|---|---|---|")
    for s in GROUND["seasons"]:
        q = s["percentiles"]
        p(f"| {s['name']} | " + " | ".join(f"L\\* {q[k]['L']:.1f} `{hexs(q[k]['rgb'])}`" for k in ("0.1", "1", "50", "99", "99.9")) + " |")
    target = darkest_target()
    p(f"\nDarkest common ground: {dark_end[0]} p1, L\\* {dark_end[3]:.1f}. Lightest: {light_end[0]} p99, "
      f"L\\* {light_end[3]:.1f}. Rarest dark: {extreme_dark[0]} p0.1, L\\* {extreme_dark[1]['L']:.1f}.")
    p(f"**3:1 below the darkest common ground needs L\\* ≤ {target:.1f}; nothing can reach 3:1 above the lightest** "
      f"(white is {contrast((1, 1, 1), light_end[2]):.2f}:1).\n")

    rows = {"read": [], "fill": [], "wash": [], "ground": [], "quiet": []}
    for key, (rgb, alpha, src) in sorted(named.items()):
        # Heaps and crates are inked round with HeapEdge; a trade's workplace is a building, inked round like any.
        tier = ("fill:HeapEdge" if key.startswith("Goods.") else "fill:Ink" if key.startswith("JobKind.")
                else WORLD.get(key))
        if tier is None:
            continue
        what = WHAT.get(key, key.split(".")[-1].lower() + (" heap / crate" if key.startswith("Goods.") else " workplace"))
        g_mid = [g for g in grounds if g[1] == "50"]
        d = min(de(over(rgb, alpha, g[2]), g[2]) for g in g_mid)
        if tier.startswith("fill:"):
            edge = tier.split(":")[1]
            # The thing reads by its outline: the outline's contrast against the ground, whatever the fill.
            w = worst_against_ground(*named[edge][:2])
            rows["fill"].append((key, what, rgb, alpha, src, w, d, edge))
            continue
        w = worst_against_ground(rgb, alpha)
        rows[tier].append((key, what, rgb, alpha, src, w, d))

    fill_fails = [r for r in rows["fill"] if r[5][0] < 3.0]
    p(f"## Things with an area — judged by their outline, {len(fill_fails)} of {len(rows['fill'])} fail\n")
    p("| colour | drawn as | fill | outline | outline's worst |\n|---|---|---|---|---|")
    for key, what, rgb, alpha, src, w, _, edge in sorted(rows["fill"], key=lambda r: r[5][0]):
        p(f"| `{key}` | {what} | `{hexs(rgb, alpha)}` | `{edge}` | {w[0]:.2f}:1{'' if w[0] >= 3 else ' ❌'} |")
    p("")

    fails = [r for r in rows["read"] if r[5][0] < 3.0]
    p(f"## Failures — must read at 3:1, {len(fails)} of {len(rows['read'])}\n")
    p(f"Each needs its fill at L\\* ≤ {target:.0f} or an ink outline. L\\* is the colour as drawn (over the darkest "
      f"ground, when it is translucent); *worst* is its lowest contrast against any season's p1, p50 or p99 ground.\n")
    p("| colour | drawn as | value | L\\* | worst | against |\n|---|---|---|---|---|---|")
    for key, what, rgb, alpha, src, w, _ in sorted(fails, key=lambda r: r[5][0]):
        p(f"| `{key}` | {what} | `{hexs(rgb, alpha)}` | {lstar(over(rgb, alpha, dark_end[2])):.0f} | {w[0]:.2f}:1 | {w[1]} p{w[2]} |")
    passes = [r for r in rows["read"] if r[5][0] >= 3.0]
    p(f"\nPassing at 3:1 ({len(passes)}): " + ", ".join(f"`{r[0]}` {r[5][0]:.1f}:1" for r in sorted(passes, key=lambda r: r[5][0])))

    p("\n## Washes — must be seen (CIE76 ΔE ≥ 5 against the middle ground, every season)\n")
    p("| colour | drawn as | value | ΔE | verdict |\n|---|---|---|---|---|")
    for key, what, rgb, alpha, src, w, d in rows["wash"]:
        p(f"| `{key}` | {what} | `{hexs(rgb, alpha)}` | {d:.1f} | {'✅' if d >= 5 else '❌ too faint'} |")

    p(f"\n## Ground kinds — the rule gives the ground L\\* {GROUND_FLOOR:.0f} and up (the grass spans "
      f"{dark_end[3]:.0f}–{light_end[3]:.0f})\n")
    p("`Ground` is drawn as flat parchment until the terrain shader lays the grass over it; the rest are what "
      "the terrain shader re-picks.\n")
    p("| colour | drawn as | value | L\\* | contrast with the grass (mid) | in the band? |\n|---|---|---|---|---|---|")
    for key, what, rgb, alpha, src, w, d in rows["ground"]:
        mid = [g for g in grounds if g[1] == "50"]
        c = min(contrast(over(rgb, alpha, g[2]), g[2]) for g in mid)
        L = lstar(over(rgb, alpha, mid[2][2]))
        p(f"| `{key}` | {what} | `{hexs(rgb, alpha)}` | {L:.0f} | {c:.2f}:1 | {'✅' if L >= GROUND_FLOOR else ('❌ in the things band — reads as a thing' if L <= target else '❌ below the band — neither ground nor thing')} |")

    p("\n## Scenery — reported, not failed\n")
    p("| colour | drawn as | value | L\\* | worst |\n|---|---|---|---|---|")
    for key, what, rgb, alpha, src, w, d in rows["quiet"]:
        p(f"| `{key}` | {what} | `{hexs(rgb, alpha)}` | {lstar(rgb):.0f} | {w[0]:.2f}:1 |")

    # ---- the rule ----
    p("\n## The value-band rule, tested\n")
    fills = [r for r in rows["fill"] if r[0] in ("HomeColour", "GranaryColour", "WarehouseColour", "MarketColour",
             "HutColour", "LibraryColour", "TownHallColour", "WellColour", "FisheryColour", "BerryColour", "TreeColour",
             "SiteColour", "DemolishColour", "BridgeColour", "StoneBridgeColour") or r[0].startswith("JobKind.")]
    pairs_now = pairs_dark = 0
    close = []
    for a, b in itertools.combinations(fills, 2):
        now = de(a[2], b[2])
        dark = de(darkened(a[2], target), darkened(b[2], target))
        pairs_now += now < 10
        pairs_dark += dark < 10
        if dark < 10:
            close.append((a[1], b[1], now, dark))
    p(f"**Darker only:** every building fill moved to L\\* {target:.0f} (hue kept, chroma cut to the gamut). Of "
      f"{len(fills) * (len(fills) - 1) // 2} pairs of building colours, **{pairs_now} are within ΔE 10 today and "
      f"{pairs_dark} would be after** — at that lightness the gamut leaves little chroma, so hue stops telling buildings apart:")
    for a, b, now, dark in sorted(close, key=lambda x: x[3])[:24]:
        p(f"- {a} / {b}: ΔE {now:.0f} → {dark:.0f}")
    ink_worst = min(contrast(INK, g[2]) for g in grounds)
    ink_extreme = contrast(INK, extreme_dark[1]["rgb"])
    p(f"\n**Ink outline:** `{hexs(INK)}` (L\\* {lstar(INK):.0f}) is {ink_worst:.2f}:1 against the darkest common ground "
      f"and {ink_extreme:.2f}:1 against the rarest dark (p0.1) — every outlined thing passes whatever its fill.")
    fill_ok = sum(1 for r in fills if worst_against_ground(r[2], r[3])[0] >= 3.0)
    p(f"Fills that would pass without their outline: {fill_ok} of {len(fills)} — the outline is what carries them.")

    p(f"\n## Out of scope — UI ({len(ui)})\n")
    p(", ".join(f"`{k}`" for k in sorted(ui)))
    if unclassified:
        p(f"\n⚠️ **Unclassified ({len(unclassified)}) — new colours this tool has not been told about:** "
          + ", ".join(f"`{k}`" for k in unclassified))
    return 0


if __name__ == "__main__":
    sys.exit(main())
