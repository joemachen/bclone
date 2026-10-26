"""
The grass look's checks and reference images, from lab_render.gd's reference set (`specs/grass-look.md`, D632).

    python -X utf8 -I tools/grass/lab_check.py parity RENDER_DIR
    python -X utf8 -I tools/grass/lab_check.py shots RENDER_DIR art/grass

parity  — each parity_s<season>.png (the lab at the approved look, about one mask texel a pixel, grain 0) against
          grass_antitile_ref.py at the same look. The look is read from looks/grass_approved.tres itself, so this file
          holds no second copy of a number. D632 measured ≤ 2/255, ≤ 3 px of 1,024,000 off by more than 1.
shots   — the reference JPEGs: four seasons at the widest and the gameplay zoom, and the seasons strip (each season
          into the next at 0/25/50/75/100 %), JPEG like the rest of art/.
"""
from __future__ import annotations

import pathlib
import re
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = pathlib.Path(__file__).resolve().parent
LOOK = HERE.parent.parent / "src" / "Bclone.Game" / "looks" / "grass_approved.tres"
sys.path.insert(0, str(HERE))
import grass_antitile_ref as ref  # noqa: E402

SEASONS = ("spring", "summer", "fall", "winter")


def approved() -> dict:
    """The approved look's values, from its .tres (`Name = value` lines)."""
    values = {}
    for line in LOOK.read_text(encoding="utf-8").splitlines():
        m = re.fullmatch(r"(\w+) = (.+)", line.strip())
        if m and m.group(1) != "script":
            raw = m.group(2)
            values[m.group(1)] = raw.strip('"') if raw.startswith('"') else (raw == "true" if raw in ("true", "false") else float(raw))
    return values


def parity(render_dir: str) -> int:
    look = approved()
    masks, atlas = ref.load()
    worst = 0
    for s in range(4):
        lab = ref.Lab(season=float(s), jitter=look["Jitter"], strength=look["Strength"], px_per_tile=33.0,
                      tiles_per_repeat=look["TilesPerRepeat"], shown=look["Masks"],
                      macro_on=look["MacroOn"], macro_cycle_tiles=look["MacroCycleTiles"],
                      macro_strength_amount=look["MacroStrengthAmount"], macro_season_amount=look["MacroSeasonAmount"],
                      hex_on=look["HexOn"], hex_sharpness=look["HexSharpness"], height_blend=look["HeightBlend"],
                      mix_on=look["MixingOn"], mix_region_tiles=look["MixRegionTiles"], mix_width_tiles=look["MixWidthTiles"])
        want = np.clip(np.round(ref.render(lab, masks, atlas, 800, 1280) * 255.0), 0, 255).astype(np.int16)
        got = np.asarray(Image.open(f"{render_dir}/parity_s{s}.png").convert("RGB")).astype(np.int16)
        d = np.abs(got - want).max(axis=2)
        worst = max(worst, int(d.max()))
        print(f"parity, {SEASONS[s]}: max {d.max()}/255, mean {d.mean():.3f}; off by > 1: {(d > 1).sum()} px, "
              f"> 4: {(d > 4).sum()} px of {d.size:,}")
    ok = worst <= 5
    print(f"parity: {'OK' if ok else 'FAILED'} (worst {worst}/255; hex at turned positions allows a few)")
    return 0 if ok else 1


def shots(render_dir: str, out_dir: str) -> int:
    out = pathlib.Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    for s, season in enumerate(SEASONS):
        for zoom in ("widest", "gameplay"):
            im = Image.open(f"{render_dir}/ref_s{s}_{zoom}.png").convert("RGB")
            im.save(out / f"grass-approved-{s}-{season}-{zoom}.jpg", quality=95, subsampling=0)

    try:
        font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 18)
    except OSError:
        font = ImageFont.load_default()
    cell, pad, left, top = 256, 6, 150, 36
    pcts = (0, 25, 50, 75, 100)
    sheet = Image.new("RGB", (left + len(pcts) * (cell + pad), top + 4 * (cell + pad)), (244, 240, 228))
    draw = ImageDraw.Draw(sheet)
    for j, pct in enumerate(pcts):
        draw.text((left + j * (cell + pad) + cell // 2 - 18, 8), f"{pct}%", fill=(40, 40, 40), font=font)
    for s, season in enumerate(SEASONS):
        draw.text((8, top + s * (cell + pad) + cell // 2 - 12), f"{season} > {SEASONS[(s + 1) % 4]}", fill=(40, 40, 40), font=font)
        for j, pct in enumerate(pcts):
            im = Image.open(f"{render_dir}/strip_{s}_{pct:03d}.png").convert("RGB")
            box = ((im.width - cell) // 2, (im.height - cell) // 2, (im.width + cell) // 2, (im.height + cell) // 2)
            sheet.paste(im.crop(box), (left + j * (cell + pad), top + s * (cell + pad)))
    sheet.save(out / "grass-approved-seasons-strip.jpg", quality=95, subsampling=0)
    print(f"shots: 8 views and the seasons strip in {out}")
    return 0


if __name__ == "__main__":
    if len(sys.argv) >= 3 and sys.argv[1] == "parity":
        sys.exit(parity(sys.argv[2]))
    if len(sys.argv) >= 4 and sys.argv[1] == "shots":
        sys.exit(shots(sys.argv[2], sys.argv[3]))
    print(__doc__)
    sys.exit(2)
