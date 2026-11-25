// THE FIELDS over the painted valley (A126, `specs/painted-paths.md §3.6`): the crops' paintings, the fields' edge wash and their
// snow, drawn on their own layer between the valley and the map, with the worn tracks cut out of the crop.
//
// Joe, 2026-11-16: *"the paths on/in crop fields need to be given the proper styling like the rest of the paths in the game"*.
// The valley's shader paints every track into the ground (A72, A82) — under a field too, where the crop covered it, and D512's
// flat overlay stood in: one colour, a ruled edge, the pale smooth band in his Year 35 and 38 screenshots. ⭐ So the crop is
// cut away where a track runs, and the valley's own painted track shows through: ONE track, one shader, the same on and off a
// field. Lightly worn, the crop is thinned rather than cleared (a trampled lane through the wheat, sheet 08's tier II); packed,
// it is cleared to the track's edge and the pen beside it. The crop's cut edge pools a little darker, as the fields' own edges
// do (A61).
//
// ⛔ The cut must be the track's own edge, to the pixel: the path field, the half-width and the wander are read exactly as
// valley_terrain.gdshader reads them (the same texel, the same noise and salt), from the same tile at the same pixel.
// Everything drawn on this layer is the fields' — a texture's colour times the draw's colour, as the map drew it.
shader_type canvas_item;

#include "res://shaders/grass_antitile.gdshaderinc"

// Where the valley is, in the layer's pixels, and how near — the valley layer's own values, handed over each frame.
uniform vec2 valley_origin_px = vec2(0.0);
uniform vec2 valley_size_px = vec2(1.0);
uniform float px_per_tile = 16.0;
uniform vec2 ground_map_tiles = vec2(1.0);
// The before/after line (A25), as the valley's layer has it: left of it the valley is today's bake, which paints no track, so
// the crop is not cut there.
uniform float split_u = -1.0;

// The valley layer's path field (VillageMap.PathField) and its range; off until a field is handed over.
uniform bool paths_on = false;
uniform sampler2D path_map : hint_default_black, filter_linear, repeat_disable;
uniform vec2 path_field_range = vec2(-0.25, 0.6);
uniform float path_wobble_tiles = 0.035;
// The paths' fade, as the valley's layer blends it (`path-crossfade.md`) — the same include, so the cut moves with the track.
#include "res://shaders/path_fade.gdshaderinc"

// How much of the crop a track takes: x lightly worn, y packed (1 clears it).
uniform vec2 field_cut = vec2(0.7, 1.0);
// How far past a packed track's edge the crop is cleared, screen pixels: to where the pen's ink is still full (the map sets it).
uniform float field_cut_pen_px = 0.3;
// The crop's cut edge pooled darker: x how much, y how far onto the crop (tiles), the colour the fields' edges pool in.
uniform vec2 field_pool = vec2(0.22, 0.06);
uniform vec3 field_pool_srgb = vec3(0.357, 0.29, 0.2);

varying vec2 local_px;

void vertex() {
    local_px = VERTEX;
}

// ⭐ The night view (A249, `specs/night-view.md §4`): the whole layer multiplied by one colour, its own colour first taken part way to grey.
// The defaults are daylight and change no pixel; the map hands the night's over.
uniform vec3 night_tint = vec3(1.0);
uniform float night_grey = 0.0;

vec3 night_of(vec3 c) {
    return mix(c, vec3(dot(c, vec3(0.299, 0.587, 0.114))), night_grey) * night_tint;
}

void fragment() {
    // Godot hands the fragment the texture's colour already times the draw's (canvas_item COLOR).
    vec4 c = COLOR;
    // ⛔ valley_terrain.gdshader's tile, corner, path_s and wander, line for line.
    vec2 uv = (local_px - valley_origin_px) / valley_size_px;
    if (paths_on && uv.x >= split_u) {
        vec2 tile = (uv - 0.5) * ground_map_tiles;
        vec2 corner = tile + 0.5 * ground_map_tiles;
        vec3 pf = path_field_faded(corner / ground_map_tiles, tile);
        float path_s = mix(path_field_range.x, path_field_range.y, pf.r);
        float reach = field_pool.y + path_wobble_tiles + field_cut_pen_px / max(px_per_tile, 1e-3);
        if (path_s < reach) {
            path_s += path_wobble_tiles * (2.0 * at_value_noise(tile / 0.9, 130u) - 1.0);
            float packed = pf.b;
            float aa = 0.6 / max(px_per_tile, 1e-3);
            // A packed track's crop is cleared past its edge by the pen's half-width, so the pen is not under the wheat.
            float edge = path_s - packed * field_cut_pen_px / max(px_per_tile, 1e-3);
            float inside = 1.0 - smoothstep(-aa, aa, edge);
            float pool = (1.0 - smoothstep(0.0, max(field_pool.y, aa), edge)) * (1.0 - inside);
            c.rgb = mix(c.rgb, field_pool_srgb, field_pool.x * pool * mix(0.6, 1.0, packed));
            c.a *= 1.0 - inside * mix(field_cut.x, field_cut.y, packed);
        }
    }

    c.rgb = night_of(c.rgb);
    COLOR = c;
}
