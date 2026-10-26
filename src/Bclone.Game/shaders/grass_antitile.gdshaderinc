// Anti-tiling for the grass lab (`specs/seasonal-grass.md §5.3`, D631). LAB ONLY.
//
// Mirrored step for step by tools/grass/grass_antitile_ref.py — keep the two in sync; the lab's parity render
// compares them. Pure functions of world position: no uniforms, no TIME, so a place always looks the same.
// The pack's recolour (grass_seasons.gdshaderinc) is untouched: these decide how the masks are SAMPLED and what
// season and strength each place is given, never how a density becomes a colour.

// ---- Hashing: integer, so the GPU and numpy agree bit for bit -------------------------------------------------

// PCG2D (Jarzynski & Olano 2020), the first component.
uint at_hash(uvec2 v) {
    v = v * 1664525u + 1013904223u;
    v.x += v.y * 1664525u;
    v.y += v.x * 1664525u;
    v = v ^ (v >> 16u);
    v.x += v.y * 1664525u;
    v.y += v.x * 1664525u;
    v = v ^ (v >> 16u);
    return v.x;
}

// A number in [0, 1) for a lattice cell and a salt — 24 bits, exact in a float.
float at_rand(ivec2 c, uint salt) {
    return float(at_hash(uvec2(c) + uvec2(salt, salt * 7u)) >> 8u) / 16777216.0;
}

// Value noise in [0, 1]: one lattice, smoothstep between its corners.
float at_value_noise(vec2 p, uint salt) {
    vec2 i = floor(p);
    vec2 f = p - i;
    vec2 u = f * f * (3.0 - 2.0 * f);
    ivec2 c = ivec2(i);
    float a = at_rand(c, salt);
    float b = at_rand(c + ivec2(1, 0), salt);
    float d = at_rand(c + ivec2(0, 1), salt);
    float e = at_rand(c + ivec2(1, 1), salt);
    return mix(mix(a, b, u.x), mix(d, e, u.x), u.y);
}

// ---- 1. Macro variation ----------------------------------------------------------------------------------------

// Two octaves at one cycle per `cycle_tiles` and a little over twice that: x for strength, y for the season.
vec2 at_macro(vec2 tile, float cycle_tiles) {
    vec2 p = tile / cycle_tiles;
    float s = 0.65 * at_value_noise(p, 11u) + 0.35 * at_value_noise(p * 2.03 + 17.0, 12u);
    float t = 0.65 * at_value_noise(p, 21u) + 0.35 * at_value_noise(p * 2.03 + 29.0, 22u);
    return vec2(s, t);
}

// ---- 2. Version mixing -----------------------------------------------------------------------------------------

// Regions are the cells of a jittered grid one region across, with the position warped first so a border wiggles
// at two scales (a third of a region, and about a tenth) instead of running straight.
vec2 at_region_warp(vec2 p) {
    vec2 coarse = vec2(at_value_noise(p * 2.5, 31u), at_value_noise(p * 2.5 + 7.0, 32u)) - 0.5;
    vec2 fine = vec2(at_value_noise(p * 11.0, 33u), at_value_noise(p * 11.0 + 5.0, 34u)) - 0.5;
    return p + 0.44 * coarse + 0.12 * fine;
}

// The nearest two region cells to `p` (region units), the version (0..4) each holds, and how far `p` is from the
// border between them, in regions (the exact distance to their bisector).
void at_regions(vec2 p, out int va, out int vb, out float border) {
    ivec2 base = ivec2(floor(p));
    float d1 = 1e9;
    float d2 = 1e9;
    ivec2 c1 = base;
    ivec2 c2 = base;
    vec2 p1 = vec2(0.0);
    vec2 p2 = vec2(0.0);
    for (int y = -1; y <= 1; y++) {
        for (int x = -1; x <= 1; x++) {
            ivec2 c = base + ivec2(x, y);
            vec2 pt = vec2(c) + vec2(at_rand(c, 41u), at_rand(c, 42u));
            float d = length(p - pt);
            if (d < d1) {
                d2 = d1; c2 = c1; p2 = p1;
                d1 = d; c1 = c; p1 = pt;
            } else if (d < d2) {
                d2 = d; c2 = c; p2 = pt;
            }
        }
    }
    va = int(at_hash(uvec2(c1) + uvec2(43u, 301u)) % 5u);
    vb = int(at_hash(uvec2(c2) + uvec2(43u, 301u)) % 5u);
    border = (d2 * d2 - d1 * d1) / (2.0 * max(length(p2 - p1), 1e-6));
}

// ---- 3. Hex tiling ---------------------------------------------------------------------------------------------

// The triangle grid of Heitz & Neyret (2018) / Mikkelsen's hex tiling (2022): the three lattice vertices whose
// hexagons cover `p`, and their barycentric weights.
void at_tri_grid(vec2 p, out vec3 w, out ivec2 v1, out ivec2 v2, out ivec2 v3) {
    vec2 s = vec2(p.x - 0.57735027 * p.y, 1.15470054 * p.y);
    ivec2 base = ivec2(floor(s));
    vec2 f = s - floor(s);
    float z = 1.0 - f.x - f.y;
    if (z > 0.0) {
        w = vec3(z, f.y, f.x);
        v1 = base;
        v2 = base + ivec2(0, 1);
        v3 = base + ivec2(1, 0);
    } else {
        w = vec3(-z, 1.0 - f.y, 1.0 - f.x);
        v1 = base + ivec2(1, 1);
        v2 = base + ivec2(1, 0);
        v3 = base + ivec2(0, 1);
    }
}

// A vertex's own transform of the repeat: a turn (any angle — the washes have no light direction) and an offset.
mat2 at_turn(ivec2 v) {
    float a = at_rand(v, 51u) * 6.28318530718;
    float c = cos(a);
    float s = sin(a);
    return mat2(vec2(c, s), vec2(-s, c));
}

vec2 at_shift(ivec2 v) {
    return vec2(at_rand(v, 52u), at_rand(v, 53u));
}

// ---- Blending samples ------------------------------------------------------------------------------------------

// How much of a stamp a sample holds: its densest group.
float at_height(vec4 m) {
    return max(max(m.r, m.g), max(m.b, m.a));
}

// LINEAR keeps the weights. HEIGHT lifts each weight by its sample's height and keeps only those within
// AT_DEPTH of the tallest — so where two layouts meet, each stamp is drawn whole or not at all, rather than both
// at part strength (the ghost). Weights in, weights out, summing to one.
const float AT_DEPTH = 0.15;

vec3 at_blend(vec3 w, vec3 h, bool height) {
    if (!height) {
        return w / max(w.x + w.y + w.z, 1e-6);
    }
    vec3 a = w + h;
    float top = max(max(a.x, a.y), a.z);
    vec3 k = max(a - (top - AT_DEPTH), vec3(0.0)) * step(vec3(1e-6), w);
    return k / max(k.x + k.y + k.z, 1e-6);
}
