"""
Reference maths for the grass lab's anti-tiling (`specs/seasonal-grass.md §5.3`, D631). LAB ONLY.

Mirrors src/Bclone.Game/shaders/grass_antitile.gdshaderinc and the fragment of grass_lab.gdshader step for step —
keep the three in sync. The recolour itself is Joe's: this file calls his pack's grass_ref.py (jitter) and checks its
own per-pixel-season recolour against grass_ref.recolor before it is trusted (`--selfcheck`).

Where the pack lives: $BCLONE_GRASS_PACK, else D:/Projects/brushes/seasons.

    python -X utf8 -I tools/grass/grass_antitile_ref.py --selfcheck

Mask sampling here is bilinear at mip level 0, so a comparison with a render is only fair where one mask texel lands
on about one pixel (the lab at 32 px a tile and 64 tiles a repeat).
"""
from __future__ import annotations

import os
import sys
from dataclasses import dataclass

import numpy as np

PACK = os.environ.get("BCLONE_GRASS_PACK", "D:/Projects/brushes/seasons")
sys.path.insert(0, PACK)
import grass_ref  # noqa: E402  (Joe's pack: the recolour this lab never changes)

U32 = np.uint32
MUL = U32(1664525)
ADD = U32(1013904223)
PARCHMENT = np.array([0xE6, 0xD6, 0xB0], np.float64) / 255.0
HEX_PER_REPEAT = 2.0
AT_DEPTH = 0.15


# ---- Hashing ----------------------------------------------------------------------------------------------------

def _u32(a) -> np.ndarray:
    return (np.asarray(a, dtype=np.int64) & 0xFFFFFFFF).astype(np.uint32)


def at_hash(vx: np.ndarray, vy: np.ndarray) -> np.ndarray:
    """PCG2D, the first component — uint32 arithmetic wraps exactly as the GPU's does."""
    with np.errstate(over="ignore"):
        vx = vx * MUL + ADD
        vy = vy * MUL + ADD
        vx = vx + vy * MUL
        vy = vy + vx * MUL
        vx = vx ^ (vx >> U32(16))
        vy = vy ^ (vy >> U32(16))
        vx = vx + vy * MUL
        vy = vy + vx * MUL
        vx = vx ^ (vx >> U32(16))
    return vx


def at_rand(cx, cy, salt: int) -> np.ndarray:
    with np.errstate(over="ignore"):
        h = at_hash(_u32(cx) + U32(salt), _u32(cy) + U32(salt) * U32(7))
    return (h >> U32(8)).astype(np.float64) / 16777216.0


def at_value_noise(px: np.ndarray, py: np.ndarray, salt: int) -> np.ndarray:
    ix, iy = np.floor(px), np.floor(py)
    fx, fy = px - ix, py - iy
    ux, uy = fx * fx * (3.0 - 2.0 * fx), fy * fy * (3.0 - 2.0 * fy)
    cx, cy = ix.astype(np.int64), iy.astype(np.int64)
    a = at_rand(cx, cy, salt)
    b = at_rand(cx + 1, cy, salt)
    d = at_rand(cx, cy + 1, salt)
    e = at_rand(cx + 1, cy + 1, salt)
    top = a + (b - a) * ux
    bottom = d + (e - d) * ux
    return top + (bottom - top) * uy


# ---- 1. Macro variation -----------------------------------------------------------------------------------------

def at_macro(tx: np.ndarray, ty: np.ndarray, cycle_tiles: float):
    px, py = tx / cycle_tiles, ty / cycle_tiles
    s = 0.65 * at_value_noise(px, py, 11) + 0.35 * at_value_noise(px * 2.03 + 17.0, py * 2.03 + 17.0, 12)
    t = 0.65 * at_value_noise(px, py, 21) + 0.35 * at_value_noise(px * 2.03 + 29.0, py * 2.03 + 29.0, 22)
    return s, t


# ---- 2. Version mixing ------------------------------------------------------------------------------------------

def at_region_warp(px: np.ndarray, py: np.ndarray):
    cx = at_value_noise(px * 2.5, py * 2.5, 31) - 0.5
    cy = at_value_noise(px * 2.5 + 7.0, py * 2.5 + 7.0, 32) - 0.5
    fx = at_value_noise(px * 11.0, py * 11.0, 33) - 0.5
    fy = at_value_noise(px * 11.0 + 5.0, py * 11.0 + 5.0, 34) - 0.5
    return px + 0.44 * cx + 0.12 * fx, py + 0.44 * cy + 0.12 * fy


def at_regions(px: np.ndarray, py: np.ndarray):
    """(va, vb, border): the two nearest cells' versions and the distance to their bisector, in regions."""
    bx, by = np.floor(px).astype(np.int64), np.floor(py).astype(np.int64)
    d1 = np.full(px.shape, 1e9)
    d2 = np.full(px.shape, 1e9)
    c1x, c1y, c2x, c2y = bx.copy(), by.copy(), bx.copy(), by.copy()
    p1x, p1y, p2x, p2y = (np.zeros(px.shape) for _ in range(4))
    for y in (-1, 0, 1):
        for x in (-1, 0, 1):
            cx, cy = bx + x, by + y
            ptx = cx + at_rand(cx, cy, 41)
            pty = cy + at_rand(cx, cy, 42)
            d = np.hypot(px - ptx, py - pty)
            nearer = d < d1
            second = ~nearer & (d < d2)
            # Shift the old nearest down to second where this one is nearer.
            d2 = np.where(nearer, d1, np.where(second, d, d2))
            c2x = np.where(nearer, c1x, np.where(second, cx, c2x))
            c2y = np.where(nearer, c1y, np.where(second, cy, c2y))
            p2x = np.where(nearer, p1x, np.where(second, ptx, p2x))
            p2y = np.where(nearer, p1y, np.where(second, pty, p2y))
            d1 = np.where(nearer, d, d1)
            c1x, c1y = np.where(nearer, cx, c1x), np.where(nearer, cy, c1y)
            p1x, p1y = np.where(nearer, ptx, p1x), np.where(nearer, pty, p1y)
    with np.errstate(over="ignore"):
        va = (at_hash(_u32(c1x) + U32(43), _u32(c1y) + U32(301)) % U32(5)).astype(np.int64)
        vb = (at_hash(_u32(c2x) + U32(43), _u32(c2y) + U32(301)) % U32(5)).astype(np.int64)
    border = (d2 * d2 - d1 * d1) / (2.0 * np.maximum(np.hypot(p2x - p1x, p2y - p1y), 1e-6))
    return va, vb, border


# ---- 3. Hex tiling ----------------------------------------------------------------------------------------------

def at_tri_grid(px: np.ndarray, py: np.ndarray):
    """Weights (3, ...) and vertices [(x, y)] * 3 of the triangle grid around p."""
    sx = px - 0.57735027 * py
    sy = 1.15470054 * py
    bx, by = np.floor(sx).astype(np.int64), np.floor(sy).astype(np.int64)
    fx, fy = sx - np.floor(sx), sy - np.floor(sy)
    z = 1.0 - fx - fy
    up = z > 0.0
    w = np.stack([np.where(up, z, -z), np.where(up, fy, 1.0 - fy), np.where(up, fx, 1.0 - fx)])
    v1 = (np.where(up, bx, bx + 1), np.where(up, by, by + 1))
    v2 = (np.where(up, bx, bx + 1), np.where(up, by + 1, by))
    v3 = (np.where(up, bx + 1, bx), np.where(up, by, by + 1))
    return w, (v1, v2, v3)


def at_turn_shift(v, ux: np.ndarray, uy: np.ndarray):
    """The vertex's turn applied to uv, plus its shift: mat2(vec2(c, s), vec2(-s, c)) * uv + shift."""
    a = at_rand(v[0], v[1], 51) * 6.28318530718
    c, s = np.cos(a), np.sin(a)
    return c * ux - s * uy + at_rand(v[0], v[1], 52), s * ux + c * uy + at_rand(v[0], v[1], 53)


# ---- Blending ---------------------------------------------------------------------------------------------------

def at_height(m: np.ndarray) -> np.ndarray:
    return m.max(axis=-1)


def at_blend(w: np.ndarray, h: np.ndarray, height: bool) -> np.ndarray:
    """w, h: (k, ...) -> weights (k, ...) summing to one, LINEAR or HEIGHT exactly as the include."""
    if not height:
        return w / np.maximum(w.sum(axis=0), 1e-6)
    a = w + h
    top = a.max(axis=0)
    k = np.maximum(a - (top - AT_DEPTH), 0.0) * (w >= 1e-6)
    return k / np.maximum(k.sum(axis=0), 1e-6)


# ---- Sampling and recolour --------------------------------------------------------------------------------------

def bilinear(mask: np.ndarray, ux: np.ndarray, uy: np.ndarray) -> np.ndarray:
    """GPU-style bilinear at mip 0 with repeat, texel-centre addressing. mask: (H, W, 4) uint8 -> float 0..1."""
    h, w = mask.shape[:2]
    m = mask.astype(np.float64) / 255.0
    x = ux * w - 0.5
    y = uy * h - 0.5
    x0, y0 = np.floor(x), np.floor(y)
    fx, fy = (x - x0)[..., None], (y - y0)[..., None]
    x0, y0 = x0.astype(np.int64) % w, y0.astype(np.int64) % h
    x1, y1 = (x0 + 1) % w, (y0 + 1) % h
    top = m[y0, x0] * (1 - fx) + m[y0, x1] * fx
    bottom = m[y1, x0] * (1 - fx) + m[y1, x1] * fx
    return top * (1 - fy) + bottom * fy


def recolor(m: np.ndarray, atlas: np.ndarray, season: np.ndarray, jitter: float, ux, uy) -> np.ndarray:
    """grass_recolor_srgb with a season per pixel — the snippet's maths; `--selfcheck` holds it to grass_ref.recolor."""
    a = atlas.astype(np.float64) / 255.0
    s = np.mod(season, 4.0)
    s0 = np.floor(s).astype(np.int64) % 4
    s1 = (s0 + 1) % 4
    t = (s - np.floor(s))[..., None]
    out = np.ones(m.shape[:-1] + (3,))
    for g in range(4):
        d = m[..., g]
        x = np.clip(d, 0.0, 1.0) * 255.0
        i0 = np.floor(x).astype(np.int64)
        i1 = np.minimum(i0 + 1, 255)
        f = (x - i0)[..., None]

        def row(r):
            return a[r, i0] * (1.0 - f) + a[r, i1] * f

        c0 = row(s0 * 4 + g)
        c1 = row(s1 * 4 + g)
        c = c0 + (c1 - c0) * t
        if jitter > 0.0:
            nh, nv = grass_ref.jitter_fields(ux, uy, g)
            c = grass_ref.apply_jitter(c, nh.astype(np.float64), nv.astype(np.float64), jitter)
        out = out * c
    return out


# ---- The lab's fragment -----------------------------------------------------------------------------------------

@dataclass
class Lab:
    season: float = 1.0
    jitter: float = 0.15
    strength: float = 1.0
    px_per_tile: float = 32.0
    tiles_per_repeat: float = 64.0
    shown: str = "v06"
    macro_on: bool = False
    macro_cycle_tiles: float = 100.0
    macro_strength_amount: float = 0.3
    macro_season_amount: float = 0.1
    mix_on: bool = False
    mix_region_tiles: float = 48.0
    mix_width_tiles: float = 1.5
    hex_on: bool = False
    hex_sharpness: float = 4.0
    height_blend: bool = False


MIXED = ("v02", "v03", "v04", "v05", "v06")


def render(lab: Lab, masks: dict, atlas: np.ndarray, height: int, width: int, x0: int = 0, y0: int = 0, debug=None):
    """The lab's colour, sRGB 0..1, for a height x width window of a (virtual) 1280 x 800 rect from pixel (x0, y0)."""
    full_w, full_h = 1280.0, 800.0
    xs = np.arange(x0, x0 + width) + 0.5
    ys = np.arange(y0, y0 + height) + 0.5
    px, py = np.meshgrid(xs, ys)
    tx = (px / full_w - 0.5) * full_w / lab.px_per_tile
    ty = (py / full_h - 0.5) * full_h / lab.px_per_tile
    ux, uy = tx / lab.tiles_per_repeat, ty / lab.tiles_per_repeat

    va = np.zeros(px.shape, np.int64)
    vb = np.zeros(px.shape, np.int64)
    wa = np.ones(px.shape)
    if lab.mix_on:
        wx, wy = at_region_warp(tx / lab.mix_region_tiles, ty / lab.mix_region_tiles)
        va, vb, border = at_regions(wx, wy)
        if lab.mix_width_tiles > 1e-4:
            wa = np.where(va != vb, np.clip(0.5 + border * lab.mix_region_tiles / lab.mix_width_tiles, 0.0, 1.0), 1.0)

    kept = {}  # the final blend weights, for the ghosting measurement (debug only)

    def lab_masks(sx, sy):
        if not lab.mix_on:
            return bilinear(masks[lab.shown], sx, sy), None
        stack = np.stack([bilinear(masks[v], sx, sy) for v in MIXED])  # (5, H, W, 4)
        a = np.take_along_axis(stack, va[None, ..., None], axis=0)[0]
        b = np.take_along_axis(stack, vb[None, ..., None], axis=0)[0]
        k = at_blend(np.stack([wa, 1.0 - wa]), np.stack([at_height(a), at_height(b)]), lab.height_blend)
        blended = a * k[0][..., None] + b * k[1][..., None]
        kept["k"] = np.where((wa >= 1.0)[None], np.stack([np.ones_like(wa), np.zeros_like(wa)]), k)
        out = np.where((wa >= 1.0)[..., None], a, blended)
        return out, (a, b)

    sources = None
    hex_top = np.ones(px.shape)
    if lab.hex_on:
        w, verts = at_tri_grid(ux * HEX_PER_REPEAT, uy * HEX_PER_REPEAT)
        w = np.power(w, lab.hex_sharpness)
        w = w / np.maximum(w.sum(axis=0), 1e-6)
        hex_top = w.max(axis=0)
        samples = [lab_masks(*at_turn_shift(v, ux, uy))[0] for v in verts]
        k = at_blend(w, np.stack([at_height(s) for s in samples]), lab.height_blend)
        m = sum(s * k[i][..., None] for i, s in enumerate(samples))
        kept["k"] = k
        sources = samples
    else:
        m, pair = lab_masks(ux, uy)
        sources = list(pair) if pair is not None else None

    season = np.full(px.shape, lab.season)
    strength = np.full(px.shape, lab.strength)
    if lab.macro_on:
        ns, nt = at_macro(tx, ty, lab.macro_cycle_tiles)
        strength = strength * (1.0 - lab.macro_strength_amount * ns)
        season = season + lab.macro_season_amount * (2.0 * nt - 1.0)

    grass = recolor(m, atlas, season, lab.jitter, ux, uy)
    colour = PARCHMENT * (1.0 + (grass - 1.0) * strength[..., None])
    if debug is not None:
        debug.update(m=m, sources=sources, k=kept.get("k"), wa=wa, hex_top=hex_top, band=((wa > 0.0) & (wa < 1.0)) | (hex_top < 0.98))
    return colour


def load(pack: str = PACK):
    from PIL import Image
    masks = {v: np.asarray(Image.open(f"{pack}/out/grass_{v}_masks.png").convert("RGBA")) for v in MIXED}
    atlas = np.asarray(Image.open(f"{pack}/out/grass_seasons.png").convert("RGB"))
    return masks, atlas


def selfcheck() -> int:
    """This file's per-pixel-season recolour must equal Joe's grass_ref.recolor wherever the season is one number."""
    masks, atlas = load()
    m8 = masks["v06"][:256, :256]
    u, v = np.meshgrid((np.arange(256) + 0.5) / 2048.0, (np.arange(256) + 0.5) / 2048.0)
    worst = 0.0
    for season, jitter in ((1.0, 0.0), (2.6, 0.15), (3.5, 0.0), (0.0, 0.15)):
        theirs = grass_ref.recolor(m8, atlas, season, jitter, uv=(u, v)).astype(np.float64)
        ours = recolor(m8.astype(np.float64) / 255.0, atlas, np.full(u.shape, season), jitter, u, v)
        worst = max(worst, float(np.abs(theirs - ours).max()) * 255.0)
    ok = worst <= 0.5
    print(f"selfcheck: per-pixel recolour vs grass_ref.recolor, worst {worst:.4f}/255 — {'OK' if ok else 'FAILED'}")
    return 0 if ok else 1


if __name__ == "__main__":
    if "--selfcheck" in sys.argv:
        sys.exit(selfcheck())
    print(__doc__)
