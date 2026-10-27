"""The parchment ground's colour range, season by season, at the approved grass look (`specs/world-legibility.md`).

The ground is not one colour: it is parchment multiplied by the grass, varied by macro noise, grained. A thing drawn on
it sits on its darkest and its lightest patches alike, so this samples the approved look through the Python mirror
(`tools/grass/grass_antitile_ref.py`) over a wide stretch of world — enough macro cycles to reach both ends — with the
lab's paper grain applied, and writes the percentiles that `check.py` tests every world colour against.

    python -X utf8 -I -B tools/legibility/ground.py            # writes tools/legibility/ground.json
"""
from __future__ import annotations

import json
import pathlib
import sys

import numpy as np

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "grass"))
import grass_antitile_ref as ref  # noqa: E402
import lab_check  # noqa: E402  (its `approved()` reads the .tres)

OUT = HERE / "ground.json"

# A season holds for 20 days and blends into the next over 10 (seasonal-grass.md §6.4), so every value between two
# whole seasons is shown at some point: the whole seasons and the halfway points of each blend cover it.
SEASONS = [0.0, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5]
NAMES = {0.0: "spring", 1.0: "summer", 2.0: "fall", 3.0: "winter"}

WINDOWS = 48        # 256 x 256 px windows a season, scattered over +-240 tiles
WINDOW = 256
PX_PER_TILE = 33.0  # about one mask texel a pixel (63 tiles x 33 px ~ 2048): the pooled darks resolved, not averaged
PERCENTILES = [0.1, 1, 5, 50, 95, 99, 99.9]


def grain(px: np.ndarray, py: np.ndarray, amount: float) -> np.ndarray:
    """`shaders/paper_grain.gdshader`'s multiply — three octaves of value noise on screen pixels."""
    def h(x, y):
        return np.modf(np.sin(x * 127.1 + y * 311.7) * 43758.5453)[0] % 1.0

    def noise(x, y):
        ix, iy = np.floor(x), np.floor(y)
        fx, fy = x - ix, y - iy
        ux, uy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
        a, b = h(ix, iy), h(ix + 1, iy)
        c, d = h(ix, iy + 1), h(ix + 1, iy + 1)
        return (a + (b - a) * ux) + ((c + (d - c) * ux) - (a + (b - a) * ux)) * uy

    n = 0.5 * noise(px / 1.3, py / 1.3) + 0.3 * noise(px / 5.0, py / 5.0) + 0.2 * noise(px / 37.0, py / 37.0)
    return 1.0 - amount * n


def lstar(rgb: np.ndarray) -> np.ndarray:
    """CIE L* (D65) of sRGB 0..1."""
    c = np.where(rgb <= 0.04045, rgb / 12.92, ((rgb + 0.055) / 1.055) ** 2.4)
    y = c @ np.array([0.2126, 0.7152, 0.0722])
    return np.where(y > 216 / 24389, 116 * np.cbrt(y) - 16, y * 24389 / 27)


def main() -> int:
    look = lab_check.approved()
    masks, atlas = ref.load()
    rng = np.random.default_rng(633)
    offsets = rng.integers(-240 * int(PX_PER_TILE), 240 * int(PX_PER_TILE), size=(WINDOWS, 2))
    gy, gx = np.mgrid[0:WINDOW, 0:WINDOW] + 0.5
    g = grain(gx, gy, look["PaperGrain"])[..., None]

    result = {"look": look, "px_per_tile": PX_PER_TILE, "windows": WINDOWS, "window": WINDOW, "seasons": []}
    for season in SEASONS:
        lab = ref.Lab(season=season, jitter=look["Jitter"], strength=look["Strength"], px_per_tile=PX_PER_TILE,
                      tiles_per_repeat=look["TilesPerRepeat"], shown=look["Masks"],
                      macro_on=look["MacroOn"], macro_cycle_tiles=look["MacroCycleTiles"],
                      macro_strength_amount=look["MacroStrengthAmount"], macro_season_amount=look["MacroSeasonAmount"],
                      hex_on=look["HexOn"], hex_sharpness=look["HexSharpness"], height_blend=look["HeightBlend"],
                      mix_on=look["MixingOn"], mix_region_tiles=look["MixRegionTiles"], mix_width_tiles=look["MixWidthTiles"])
        pixels = np.concatenate([
            (ref.render(lab, masks, atlas, WINDOW, WINDOW, int(x0), int(y0)) * g).reshape(-1, 3)
            for x0, y0 in offsets])
        ls = lstar(pixels)
        order = np.argsort(ls)
        picks = {}
        for p in PERCENTILES:
            i = order[min(len(order) - 1, int(round(p / 100 * (len(order) - 1))))]
            picks[str(p)] = {"rgb": [round(float(v), 5) for v in pixels[i]], "L": round(float(ls[i]), 2)}
        entry = {"season": season, "name": NAMES.get(season, f"{NAMES[float(int(season))]} into "
                                                              f"{NAMES[float((int(season) + 1) % 4)]}"),
                 "percentiles": picks}
        result["seasons"].append(entry)
        print(f"{entry['name']:>18}: L* " + "  ".join(f"p{p}={picks[str(p)]['L']:5.1f}" for p in PERCENTILES))
    OUT.write_text(json.dumps(result, indent=1), encoding="utf-8")
    print(f"wrote {OUT.relative_to(HERE.parent.parent)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
