// THE SCENE's ground (`specs/seasonal-grass.md §5`): parchment, with the seasonal grass multiplied over it.
//
// Everything stays sRGB: with hdr_2d off Godot blends 2D in sRGB, and Multiply on sRGB values IS Photoshop's
// Multiply — so the snippet's grass_srgb_to_linear is deliberately not called (D628, §6.2). The snippet is
// included unchanged; it declares grass_masks, grass_season_atlas, grass_season and grass_jitter.
shader_type canvas_item;

#include "res://shaders/grass_seasons.gdshaderinc"

// The ground, sRGB-encoded. No source_color hint: the value is used exactly as written.
uniform vec3 ground_srgb = vec3(0.902, 0.839, 0.690);

// The rect's size in screen pixels, so UV can be turned back into tiles.
uniform vec2 size_px = vec2(1280.0, 800.0);
uniform float px_per_tile = 24.0;
uniform float tiles_per_repeat = 32.0;

// 0 = bare parchment, 1 = the pack as painted: the grass multiply blended toward white (D630).
uniform float grass_strength = 1.0;

void fragment() {
    // Tiles from the rect's centre, as the placeholder village counts them, so the two zoom together.
    vec2 tile_uv = (UV - 0.5) * size_px / px_per_tile / tiles_per_repeat;
    vec4 m = texture(grass_masks, tile_uv);
    vec3 grass = grass_recolor_srgb(m, tile_uv, grass_season, grass_jitter);
    COLOR = vec4(ground_srgb * mix(vec3(1.0), grass, grass_strength), 1.0);
}
