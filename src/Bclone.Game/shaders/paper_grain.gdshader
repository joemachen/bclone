// Paper grain for THE SCENE (`specs/seasonal-grass.md §5`, D630): a rough multiply over everything drawn under it,
// because grain knocks the contrast down and changes how loud the grass reads (Joe).
//
// Three octaves of value noise — fibres, tooth, and a faint cloud. No image asset. Drawn only when amount > 0, so at 0
// it is not there at all rather than a multiply by one.
//
// Two ways to anchor it, behind `world_on` (`specs/terrain-shader.md §3.1`, A3):
//   off — the placeholder, in screen pixels (D630). The paper stays still while the land pans under it.
//   on  — in tiles, so the paper moves with the land: integer hashes (the sine hash is not the same on every GPU),
//         the finest octave `grain_scale_tiles` a cycle and the others at the placeholder's ratios. Each octave fades to
//         its own mean as its cycle nears a pixel, so zooming out loses fibres rather than aliasing, and the page's
//         tone does not move with the zoom.
shader_type canvas_item;
render_mode blend_mul;

#include "res://shaders/grass_antitile.gdshaderinc"

uniform float amount = 0.15;

// World-anchored (A3), off at opening. size_px, px_per_tile and view_tile are the grass's own, so the two agree on
// where a tile is.
uniform bool world_on = false;
uniform float grain_scale_tiles = 0.08;
uniform vec2 size_px = vec2(1280.0, 800.0);
uniform float px_per_tile = 24.0;
uniform vec2 view_tile = vec2(0.0);

float grain_hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float grain_noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(grain_hash(i), grain_hash(i + vec2(1.0, 0.0)), u.x),
               mix(grain_hash(i + vec2(0.0, 1.0)), grain_hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

// One octave `cycle` tiles a cycle: whole from two screen pixels a cycle up, gone to its mean (0.5) at one.
float grain_world_octave(vec2 tile, float cycle, uint salt) {
    float keep = smoothstep(1.0, 2.0, cycle * px_per_tile);
    return mix(0.5, at_value_noise(tile / cycle, salt), keep);
}

void fragment() {
    float n;
    if (world_on) {
        vec2 tile = (UV - 0.5) * size_px / px_per_tile + view_tile;
        float s = grain_scale_tiles;
        n = 0.5 * grain_world_octave(tile, s, 61u) + 0.3 * grain_world_octave(tile, s * (5.0 / 1.3), 62u)
            + 0.2 * grain_world_octave(tile, s * (37.0 / 1.3), 63u);
    } else {
        vec2 px = FRAGCOORD.xy;
        n = 0.5 * grain_noise(px / 1.3) + 0.3 * grain_noise(px / 5.0) + 0.2 * grain_noise(px / 37.0);
    }
    COLOR = vec4(vec3(1.0 - amount * n), 1.0);
}
