// ⭐ The falling snow (A76, `specs/snowfall.md` §3.3; Joe: "should there be some kind of non-vision obscuring
// precipitation?", then "Snowfalls"). A see-through layer over the map, shown only while a snowfall is falling.
// ⭐ A77 (Joe: "its snowing for the player, not the village" ... "apply our existing parchment/watercolor style to the
// snowflakes and design them and vary them"): the map is seen straight down (A31), so a flake falls AWAY from the eye, not
// down the screen. Each flake lives a fall and a settle: born high, it is drawn larger and softer, its shadow on the ground
// thrown off toward the lower right by the valley's own light (the terrain's light, from the upper left); as it falls it
// shrinks and sharpens, drifts a little on the wind, and its shadow closes in under it; where they meet it has landed, and it
// fades into the snow lying. Two kinds: sparse CRYSTALS — six-armed, painted as the map is painted: a paper-white body with
// a cool wash pooled at its edge, a thin cool ink line that swells and breaks, the paper's tooth — in six designs after the
// old crystal plates (stellar, fern, tipped, sectored plate, plate and star, broad-branched), each one jittered; and many fine
// SPECKS between them. World-anchored, sized in tiles within a pixel range, faded out where the valley is small on screen.
// No particles and no CPU: the clock is the only thing that changes. Every value is the look file's (Snowflake*); the
// uniforms' defaults are placeholders.

shader_type canvas_item;

#include "res://shaders/grass_antitile.gdshaderinc"

// The crystals: a cell a crystal (tiles); radius in tiles, least and most px; the share of cells that hold one at the
// heaviest fall; strength.
uniform float crystal_cell_tiles = 2.4;
uniform vec3 crystal_size = vec3(0.3, 5.0, 12.0);
uniform float crystal_share = 0.3;
uniform float crystal_alpha = 0.9;
// The specks: a cell a speck (tiles), radius px least and most, share, strength.
uniform float speck_cell_tiles = 0.9;
uniform vec2 speck_size_px = vec2(1.0, 1.6);
uniform float speck_share = 0.45;
uniform float speck_alpha = 0.7;
// A life: x a crystal's fall and y its settle, z a speck's fall and w its settle, seconds of the flakes' clock. Each life
// must go a whole number of times into the clock's wrap (VillageMap.SnowClockPeriod), or every flake would jump there.
uniform vec4 flake_life = vec4(4.5, 1.5, 3.5, 1.0);
// The wind, tiles a second; how much larger a flake is at the top of its fall (x) and how far its shadow lies from it there,
// tiles (y); how fast a crystal turns as it falls, radians a second.
uniform vec2 flake_wind = vec2(0.1, 0.04);
uniform vec2 flake_rise = vec2(0.5, 0.7);
uniform float flake_spin = 0.6;
// The paint: the body, the wash pooled at its edge, the ink (alpha its strength) and its width, the shadow (alpha its
// strength at the ground).
uniform vec3 flake_srgb = vec3(0.98, 0.98, 1.0);
uniform vec3 flake_wash_srgb = vec3(0.8, 0.86, 0.94);
uniform vec4 flake_ink = vec4(0.3, 0.36, 0.47, 0.8);
uniform float flake_ink_px = 0.8;
uniform vec4 flake_shadow = vec4(0.42, 0.5, 0.62, 0.22);
uniform float flake_fade_px = 8.0;

// Each frame, from the map: how hard it is snowing (0–1), the zoom, where tile (0, 0)'s centre is on screen, the flakes' clock.
uniform float falling = 0.0;
uniform float px_per_tile = 32.0;
uniform vec2 tile_origin_px = vec2(0.0);
uniform float clock_s = 0.0;

// The valley's light comes from the upper left (valley_terrain.gdshader's normalize(-0.62, -0.62, 0.48)), so a shadow falls
// toward the lower right.
const vec2 FALL_SHADOW_DIR = vec2(0.70710678, 0.70710678);
// The specks' hashes far from the crystals', so no life of one kind repeats another's numbers.
const uint SPECK_SALT = 100000u;

varying vec2 local_px;

void vertex() {
    local_px = VERTEX;
}

float seg_px(vec2 p, vec2 a, vec2 b) {
    vec2 pa = p - a;
    vec2 ba = b - a;
    float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
    return length(pa - ba * h);
}

// A hexagon's signed distance, r from its centre to a side; its corners lie on the x axis, along a crystal's arms.
float hex_px(vec2 p, float r) {
    const vec3 k = vec3(-0.866025404, 0.5, 0.577350269);
    p = abs(p);
    p -= 2.0 * min(dot(k.xy, p), 0.0) * k.xy;
    p -= vec2(clamp(p.x, -k.z * r, k.z * r), r);
    return length(p) * sign(p.y);
}

// A point folded into one twelfth of a six-armed crystal: an arm along +x, the point on its upper side.
vec2 fold_six(vec2 p) {
    float sector = 1.0471976;
    float a = atan(p.y, p.x);
    a = abs(mod(a + 0.5 * sector, sector) - 0.5 * sector);
    return length(p) * vec2(cos(a), sin(a));
}

// A side branch from the arm at `at`, `len` long, at sixty degrees — kept short of the twelfth's edge.
float branch_px(vec2 q, float at, float len) {
    len = min(len, 0.9 * at);
    return seg_px(q, vec2(at, 0.0), vec2(at, 0.0) + len * vec2(0.5, 0.8660254));
}

// ⭐ The six designs, after the old crystal plates Joe sent: the body's signed distance (px) at a folded point q, and in
// `line` the distance to the lines inked inside it (rings, ridges). R the arm's length; j four jitters, 0 to 1.
float crystal_px(vec2 q, float R, int kind, vec4 j, out float line) {
    line = 1e3;
    float w = R * (0.07 + 0.03 * j.x);
    float d = seg_px(q, vec2(0.0), vec2(R, 0.0)) - w;
    if (kind == 0) {
        // Stellar: a small hexagon, one pair of branches past the middle.
        d = min(d, hex_px(q, R * (0.15 + 0.06 * j.y)));
        d = min(d, branch_px(q, R * (0.55 + 0.15 * j.z), R * (0.22 + 0.1 * j.w)) - 0.7 * w);
    } else if (kind == 1) {
        // Fern: three pairs of branches, shorter toward the tip.
        for (int i = 0; i < 3; i++) {
            float at_ = R * (0.32 + 0.21 * float(i) + 0.05 * (j.y - 0.5));
            d = min(d, branch_px(q, at_, R * (0.3 - 0.07 * float(i)) * (0.85 + 0.3 * j.z)) - 0.6 * w);
        }

        d = min(d, hex_px(q, R * 0.11));
    } else if (kind == 2) {
        // Tipped: a plate at each arm's end, a ring in the middle.
        d = min(d, hex_px(q - vec2(R * 0.9, 0.0), R * (0.13 + 0.05 * j.y)));
        d = min(d, hex_px(q, R * (0.25 + 0.06 * j.z)));
        line = abs(hex_px(q, R * (0.14 + 0.03 * j.z)));
    } else if (kind == 3) {
        // Sectored plate: a broad hexagon, its ridges inked down each arm.
        float plate = R * (0.6 + 0.1 * j.y);
        d = min(d, hex_px(q, plate));
        line = q.x < plate ? abs(q.y) : 1e3;
    } else if (kind == 4) {
        // Plate and star: a hexagon with stubby arms, a ring and a small star inked inside.
        float plate = R * (0.48 + 0.08 * j.y);
        d = min(seg_px(q, vec2(0.0), vec2(R * 0.92, 0.0)) - 1.6 * w, hex_px(q, plate));
        line = min(abs(hex_px(q, plate * 0.6)), q.x < plate * 0.6 ? abs(q.y) : 1e3);
    } else {
        // Broad-branched: a wide pair near the middle, a short pair toward the tip.
        d = min(d, branch_px(q, R * (0.4 + 0.08 * j.y), R * (0.34 + 0.06 * j.z)) - 0.9 * w);
        d = min(d, branch_px(q, R * 0.74, R * (0.16 + 0.06 * j.w)) - 0.6 * w);
        d = min(d, hex_px(q, R * 0.13));
    }

    return d;
}

// A flake's life at this clock: where it is now (tiles, xy), how high (z: 1 born, 0 landed), how much of it is left (w: 1
// while it falls, to 0 as it settles), and which life it is in (`life_salt`).
vec4 flake_now(ivec2 c, float cell, vec2 life, uint salt, out uint life_salt) {
    float whole = life.x + life.y;
    float t = clock_s + at_rand(c, salt) * whole;
    float k = floor(t / whole);
    float u = t - k * whole;
    life_salt = salt + 32u * uint(k) + 1u;
    float high = clamp(1.0 - u / life.x, 0.0, 1.0);
    float left = u < life.x ? 1.0 : 1.0 - (u - life.x) / life.y;
    vec2 land = (vec2(c) + 0.15 + 0.7 * vec2(at_rand(c, life_salt), at_rand(c, life_salt + 1u))) * cell;
    vec2 across = vec2(-flake_wind.y, flake_wind.x) / max(length(flake_wind), 1e-4);
    vec2 at_ = land - flake_wind * (high * life.x)
        + across * 0.1 * high * sin(u * 1.7 + 6.2831853 * at_rand(c, life_salt + 2u));
    return vec4(at_, high, left);
}

// Premultiplied colour `src` laid over `acc`.
vec4 over(vec4 src, vec4 acc) {
    return src + acc * (1.0 - src.a);
}

void fragment() {
    float seen = smoothstep(flake_fade_px * (2.0 / 3.0), flake_fade_px, px_per_tile);
    if (seen <= 0.0 || falling <= 0.0) {
        COLOR = vec4(0.0);
    } else {
        vec2 tile = (local_px - tile_origin_px) / px_per_tile;
        vec4 shadows = vec4(0.0);
        vec4 flakes = vec4(0.0);

        // The specks.
        {
            ivec2 home = ivec2(floor(tile / speck_cell_tiles));
            for (int j = -1; j <= 1; j++) {
                for (int i = -1; i <= 1; i++) {
                    ivec2 c = home + ivec2(i, j);
                    float pick = at_rand(c, SPECK_SALT + 7u);
                    float on = smoothstep(pick, pick + 0.04, speck_share * falling);
                    if (on <= 0.0) {
                        continue;
                    }

                    uint ls;
                    vec4 f = flake_now(c, speck_cell_tiles, flake_life.zw, SPECK_SALT, ls);
                    float r = mix(speck_size_px.x, speck_size_px.y, at_rand(c, ls + 3u)) * (1.0 + flake_rise.x * f.z);
                    float soft = 0.5 + 1.2 * f.z;
                    float d = length(tile - f.xy) * px_per_tile;
                    float a = (1.0 - smoothstep(r - soft, r + soft, d)) * speck_alpha * on * f.w * mix(1.0, 0.7, f.z);
                    // White, with the wash's cool rim so a speck reads on the snow lying as on grass.
                    vec3 col = mix(flake_srgb, flake_wash_srgb, smoothstep(r - 1.0, r + 0.3, d));
                    flakes = over(vec4(col * a, a), flakes);
                    float sd = length(tile - f.xy - FALL_SHADOW_DIR * flake_rise.y * f.z) * px_per_tile;
                    float sa = (1.0 - smoothstep(0.3 * r, r + soft, sd)) * flake_shadow.a * 0.6 * on * f.w * (1.0 - 0.5 * f.z);
                    shadows = over(vec4(flake_shadow.rgb * sa, sa), shadows);
                }
            }
        }

        // The crystals, only where one is big enough on screen to be seen as one.
        float least = crystal_size.y / max(crystal_size.x, 1e-3);
        float seen_c = smoothstep(0.75 * least, least, px_per_tile);
        if (seen_c > 0.0) {
            ivec2 home = ivec2(floor(tile / crystal_cell_tiles));
            for (int j = -1; j <= 1; j++) {
                for (int i = -1; i <= 1; i++) {
                    ivec2 c = home + ivec2(i, j);
                    float pick = at_rand(c, 7u);
                    float on = smoothstep(pick, pick + 0.04, crystal_share * falling);
                    if (on <= 0.0) {
                        continue;
                    }

                    uint ls;
                    vec4 f = flake_now(c, crystal_cell_tiles, flake_life.xy, 0u, ls);
                    float R = clamp(crystal_size.x * px_per_tile, crystal_size.y, crystal_size.z)
                        * (0.8 + 0.35 * at_rand(c, ls + 4u)) * (1.0 + flake_rise.x * f.z);
                    float fade = on * f.w * seen_c;

                    // Its shadow: a soft cool blot, wide and faint while the crystal is high, small and closer as it falls.
                    float sd = length(tile - f.xy - FALL_SHADOW_DIR * flake_rise.y * f.z) * px_per_tile;
                    float sr = R * (0.6 + 0.3 * f.z);
                    float sa = (1.0 - smoothstep(0.35 * sr, 1.1 * sr, sd)) * flake_shadow.a * fade * (1.0 - 0.4 * f.z);
                    shadows = over(vec4(flake_shadow.rgb * sa, sa), shadows);

                    vec2 p = (tile - f.xy) * px_per_tile;
                    if (length(p) > R + 3.0) {
                        continue;
                    }

                    // Turning as it falls, still once it lands.
                    float turn = 6.2831853 * at_rand(c, ls + 5u) + flake_spin * (1.0 - f.z) * flake_life.x
                        * (at_rand(c, ls + 6u) < 0.5 ? -1.0 : 1.0);
                    p = mat2(vec2(cos(turn), sin(turn)), vec2(-sin(turn), cos(turn))) * p;
                    vec2 q = fold_six(p);
                    int kind = int(floor(at_rand(c, ls + 7u) * 6.0));
                    vec4 jit = vec4(at_rand(c, ls + 8u), at_rand(c, ls + 9u), at_rand(c, ls + 10u), at_rand(c, ls + 11u));
                    float line;
                    float d = crystal_px(q, R, kind, jit, line);

                    // Soft while it is high (nearer the eye than the land is), crisp as it lands.
                    float soft = 0.55 + 1.4 * f.z;
                    float body = 1.0 - smoothstep(-soft, soft, d);
                    // The paper's tooth in the body, at the paper's scale on screen.
                    body *= 1.0 - 0.3 * at_value_noise(local_px / 2.5, 181u);
                    // The wash gathered at the body's edge as it dried.
                    float wet = 1.0 - smoothstep(0.0, 1.8, -d);
                    vec3 paint = mix(flake_srgb, flake_wash_srgb, 0.75 * wet);
                    // The pen: the outline and the lines inside, swelling and lifting as the bank's ink does.
                    float half_w = 0.5 * flake_ink_px;
                    float pen = 1.0 - smoothstep(half_w - 0.4, half_w + 0.6 + 0.5 * soft, abs(d));
                    pen = max(pen, 0.8 * (1.0 - smoothstep(half_w - 0.4, half_w + 0.6 + 0.5 * soft, line)));
                    float pressure = at_value_noise(q * (3.0 / max(R, 1.0)) + vec2(float(kind) * 5.0), ls + 12u);
                    pen *= smoothstep(0.12, 0.3, pressure) * mix(0.7, 1.0, pressure) * (1.0 - 0.6 * f.z);
                    // A faint bloom of the wash round it, the paint's edge on wet paper.
                    float bloom = 0.12 * (1.0 - smoothstep(0.0, 2.5 + 2.0 * f.z, d)) * step(0.0, d);

                    float strength = crystal_alpha * fade * mix(1.0, 0.75, f.z);
                    float ba = max(body, bloom) * strength;
                    vec4 c_body = vec4(mix(flake_wash_srgb, paint, body / max(max(body, bloom), 1e-4)) * ba, ba);
                    float ia = pen * flake_ink.a * strength;
                    flakes = over(over(vec4(flake_ink.rgb * ia, ia), c_body), flakes);
                }
            }
        }

        vec4 all_ = over(flakes, shadows) * seen;
        COLOR = all_.a > 1e-4 ? vec4(all_.rgb / all_.a, all_.a) : vec4(0.0);
    }
}
