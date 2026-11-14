// ⭐ The falling snow (A76, `specs/snowfall.md` §3.3; Joe: "should there be some kind of non-vision obscuring
// precipitation?", then "Snowfalls"). A see-through layer over the map, shown only while a snowfall is falling: one hashed
// flake a cell in two depths, small and constant on screen, pale with a faint cool rim so a flake reads on snow as on grass,
// sparse enough never to hide a building, a villager or a mark. World-anchored: the cells drift with the wind across the
// land, so the flakes move with the map when it pans. No particles and no CPU: the clock is the only thing that changes.
// Every value is the look file's (`looks/valley_watercolour.tres`, Snowflake*); the uniforms' defaults are placeholders.

shader_type canvas_item;

#include "res://shaders/grass_antitile.gdshaderinc"

uniform vec2 flake_cell_tiles = vec2(1.6, 1.1);
uniform vec2 flake_size_px = vec2(1.2, 1.9);
uniform vec2 flake_alpha = vec2(0.55, 0.75);
uniform float flake_share = 0.55;
uniform vec2 flake_drift = vec2(0.14, 0.55);
uniform vec3 flake_srgb = vec3(0.98, 0.98, 1.0);
uniform vec4 flake_rim = vec4(0.55, 0.62, 0.72, 0.35);
uniform float flake_fade_px = 8.0;

// Each frame, from the map: how hard it is snowing (0–1), the zoom, where tile (0, 0)'s centre is on screen, the flakes' clock.
uniform float falling = 0.0;
uniform float px_per_tile = 32.0;
uniform vec2 tile_origin_px = vec2(0.0);
uniform float clock_s = 0.0;

varying vec2 local_px;

void vertex() {
    local_px = VERTEX;
}

// One depth of flakes at this pixel: how much body (x) and rim (y), 0 to 1. A flake keeps to its own cell, nudged by a
// slow sway; the eight cells round are asked too, since a flake near its cell's edge can reach a neighbour's pixels.
vec2 flakes(vec2 tile, float cell, float size_px, vec2 drift, uint salt) {
    vec2 moved = tile - drift * clock_s;
    ivec2 home = ivec2(floor(moved / cell));
    float body = 0.0;
    float rim = 0.0;
    for (int j = -1; j <= 1; j++) {
        for (int i = -1; i <= 1; i++) {
            ivec2 c = home + ivec2(i, j);
            if (at_rand(c, salt) >= flake_share * falling) {
                continue;
            }

            vec2 at_ = (vec2(c) + 0.15 + 0.7 * vec2(at_rand(c, salt + 1u), at_rand(c, salt + 2u))) * cell;
            at_.x += 0.08 * sin(clock_s * 2.0 + at_rand(c, salt + 3u) * 6.2831853);
            float d = length(moved - at_) * px_per_tile;
            float b = 1.0 - smoothstep(size_px - 0.6, size_px + 0.6, d);
            float r = (1.0 - smoothstep(size_px + 0.2, size_px + 1.2, d)) - b;
            body = max(body, b);
            rim = max(rim, r);
        }
    }

    return vec2(body, max(rim, 0.0));
}

void fragment() {
    float seen = smoothstep(flake_fade_px * (2.0 / 3.0), flake_fade_px, px_per_tile);
    if (seen <= 0.0 || falling <= 0.0) {
        COLOR = vec4(0.0);
    } else {
        vec2 tile = (local_px - tile_origin_px) / px_per_tile;
        vec2 far = flakes(tile, flake_cell_tiles.x, flake_size_px.x, flake_drift * 0.7, 160u) * flake_alpha.x;
        vec2 near = flakes(tile, flake_cell_tiles.y, flake_size_px.y, flake_drift, 170u) * flake_alpha.y;
        // The rim under the body, the near flakes over the far.
        float rim_a = max(far.y, near.y) * flake_rim.a;
        float body_a = max(far.x, near.x);
        vec3 colour = mix(flake_rim.rgb, flake_srgb, body_a / max(body_a + rim_a, 1e-4));
        COLOR = vec4(colour, clamp(body_a + rim_a, 0.0, 1.0) * seen);
    }
}
