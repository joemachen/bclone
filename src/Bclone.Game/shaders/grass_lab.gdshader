// THE SCENE's ground (`specs/seasonal-grass.md §5`): parchment, with the seasonal grass multiplied over it.
//
// Everything stays sRGB: with hdr_2d off Godot blends 2D in sRGB, and Multiply on sRGB values IS Photoshop's
// Multiply — so the snippet's grass_srgb_to_linear is deliberately not called (D628, §6.2). The snippet is
// included unchanged; it declares grass_masks, grass_season_atlas, grass_season and grass_jitter.
//
// The anti-tiling options (D631, §5.3) are each behind their own switch; they change how the masks are sampled and the
// season and strength each place is given — the recolour itself stays the snippet's.
//
// ⭐ THE LOOK IS NOT HERE (D632): every value the lab uses comes from res://looks/grass_approved.tres (GrassLook.cs,
// specs/grass-look.md). The uniform defaults below are the pack as painted with every option off, nothing more.
shader_type canvas_item;

#include "res://shaders/grass_seasons.gdshaderinc"
#include "res://shaders/grass_antitile.gdshaderinc"

// The ground, sRGB-encoded. No source_color hint: the value is used exactly as written.
uniform vec3 ground_srgb = vec3(0.902, 0.839, 0.690);

// The rect's size in screen pixels, so UV can be turned back into tiles.
uniform vec2 size_px = vec2(1280.0, 800.0);
uniform float px_per_tile = 24.0;
uniform float tiles_per_repeat = 32.0;

// 0 = bare parchment, 1 = the pack as painted: the grass multiply blended toward white (D630).
uniform float grass_strength = 1.0;

// 1. Macro variation: strength and season vary by region, one cycle per `macro_cycle_tiles`.
uniform bool macro_on = false;
uniform float macro_cycle_tiles = 100.0;
uniform float macro_strength_amount = 0.3;
uniform float macro_season_amount = 0.1;

// 2. Version mixing: v02..v06 by region, a narrow band between them.
// ⛔ REJECTED (D632), kept switchable and off: blending one layout into another drew stamps of both at part strength
// (the ghosts §5.3 measured), and the mixed layouts read as camouflage.
uniform bool mix_on = false;
uniform float mix_region_tiles = 48.0;
uniform float mix_width_tiles = 1.5;
uniform sampler2D grass_v0 : hint_default_black, filter_linear_mipmap, repeat_enable;
uniform sampler2D grass_v1 : hint_default_black, filter_linear_mipmap, repeat_enable;
uniform sampler2D grass_v2 : hint_default_black, filter_linear_mipmap, repeat_enable;
uniform sampler2D grass_v3 : hint_default_black, filter_linear_mipmap, repeat_enable;
uniform sampler2D grass_v4 : hint_default_black, filter_linear_mipmap, repeat_enable;

// 3. Hex tiling: each hexagon of the repeat turned and shifted; sharpness narrows the blend between them.
uniform bool hex_on = false;
uniform float hex_sharpness = 4.0;

// How samples are blended, for 2 and 3 both: linear weights, or height (each stamp whole or not at all).
// ⛔ Linear (false) is REJECTED (D632) for the same ghosting; Height is the approved blend.
uniform bool height_blend = false;

// Tints where samples are being blended, so the seams can be found and looked at.
uniform bool mark_transitions = false;

// Hex lattice vertices per repeat: each hexagon is about half a repeat across.
const float HEX_PER_REPEAT = 2.0;

vec4 lab_version(int v, vec2 uv, vec2 gx, vec2 gy) {
    if (v == 0) {
        return textureGrad(grass_v0, uv, gx, gy);
    } else if (v == 1) {
        return textureGrad(grass_v1, uv, gx, gy);
    } else if (v == 2) {
        return textureGrad(grass_v2, uv, gx, gy);
    } else if (v == 3) {
        return textureGrad(grass_v3, uv, gx, gy);
    }
    return textureGrad(grass_v4, uv, gx, gy);
}

// The masks at `uv`: the shown version, or the two versions of this place's regions blended by `wa`.
vec4 lab_masks(vec2 uv, vec2 gx, vec2 gy, int va, int vb, float wa) {
    if (!mix_on) {
        return textureGrad(grass_masks, uv, gx, gy);
    }
    vec4 a = lab_version(va, uv, gx, gy);
    if (wa >= 1.0) {
        return a;
    }
    vec4 b = lab_version(vb, uv, gx, gy);
    vec3 k = at_blend(vec3(wa, 1.0 - wa, 0.0), vec3(at_height(a), at_height(b), 0.0), height_blend);
    return a * k.x + b * k.y;
}

void fragment() {
    // Tiles from the rect's centre, as the placeholder village counts them, so the two zoom together.
    vec2 tile = (UV - 0.5) * size_px / px_per_tile;
    vec2 tile_uv = tile / tiles_per_repeat;
    vec2 gx = dFdx(tile_uv);
    vec2 gy = dFdy(tile_uv);

    int va = 0;
    int vb = 0;
    float wa = 1.0;
    if (mix_on) {
        float border;
        at_regions(at_region_warp(tile / mix_region_tiles), va, vb, border);
        if (va != vb && mix_width_tiles > 1e-4) {
            wa = clamp(0.5 + border * mix_region_tiles / mix_width_tiles, 0.0, 1.0);
        }
    }

    vec4 m;
    float hex_top = 1.0;
    if (hex_on) {
        vec3 w;
        ivec2 v1;
        ivec2 v2;
        ivec2 v3;
        at_tri_grid(tile_uv * HEX_PER_REPEAT, w, v1, v2, v3);
        w = pow(w, vec3(hex_sharpness));
        w = w / max(w.x + w.y + w.z, 1e-6);
        hex_top = max(max(w.x, w.y), w.z);
        mat2 r1 = at_turn(v1);
        mat2 r2 = at_turn(v2);
        mat2 r3 = at_turn(v3);
        vec4 s1 = lab_masks(r1 * tile_uv + at_shift(v1), r1 * gx, r1 * gy, va, vb, wa);
        vec4 s2 = lab_masks(r2 * tile_uv + at_shift(v2), r2 * gx, r2 * gy, va, vb, wa);
        vec4 s3 = lab_masks(r3 * tile_uv + at_shift(v3), r3 * gx, r3 * gy, va, vb, wa);
        vec3 k = at_blend(w, vec3(at_height(s1), at_height(s2), at_height(s3)), height_blend);
        m = s1 * k.x + s2 * k.y + s3 * k.z;
    } else {
        m = lab_masks(tile_uv, gx, gy, va, vb, wa);
    }

    float season = grass_season;
    float strength = grass_strength;
    if (macro_on) {
        vec2 n = at_macro(tile, macro_cycle_tiles);
        strength *= 1.0 - macro_strength_amount * n.x;
        season += macro_season_amount * (2.0 * n.y - 1.0);
    }

    vec3 grass = grass_recolor_srgb(m, tile_uv, season, grass_jitter);
    vec3 colour = ground_srgb * mix(vec3(1.0), grass, strength);
    if (mark_transitions && ((wa > 0.0 && wa < 1.0) || hex_top < 0.98)) {
        colour = mix(colour, vec3(0.85, 0.1, 0.6), 0.45);
    }
    COLOR = vec4(colour, 1.0);
}
