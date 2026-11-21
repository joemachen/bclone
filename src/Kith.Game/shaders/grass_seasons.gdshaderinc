// grass_seasons.gdshaderinc - seasonal grass recolour for Godot 4
//
// Mirrors seasons/grass_ref.py step for step; keep the two in sync (build.py --godot checks it).
// Only the grass recolour lives here, not the terrain shader.
//
// Usage in a terrain .gdshader:
//     #include "res://shaders/grass_seasons.gdshaderinc"
//     ...
//     vec4 m = texture(grass_masks, tile_uv);                  // tile_uv repeats every grass tile
//     vec3 g = grass_recolor_srgb(m, tile_uv, grass_season, grass_jitter);
//     ALBEDO = ground_albedo * grass_srgb_to_linear(g);       // Multiply over the ground, in linear
//
// grass_masks   = grass_vNN_masks.png. DATA, never sRGB: no source_color hint. Import: Compress
//                 Lossless, Mipmaps on, Fix Alpha Border OFF, Premult Alpha OFF, Normal Map off.
//                 R olive, G green, B ochre, A details; value / 255 = group density.
// grass_season_atlas = grass_seasons.png, 256 x 16. Read raw with texelFetch (no source_color),
//                 Import: Compress Lossless, Mipmaps OFF (rows would bleed). Row = season*4 + group;
//                 seasons spring, summer, fall, winter; groups olive, green, ochre, details.
// grass_season  : 0 spring, 1 summer, 2 fall, 3 winter, 4 = spring again (cyclic, fractions blend).
// grass_jitter  : 0 = exact palettes. ~0.08-0.12 stands in for the per-stamp colour jitter of the
//                 original Photoshop passes (low-frequency, tile-periodic).
//
// All grass maths is done on sRGB-encoded values, exactly like Photoshop's Multiply; convert the
// product to linear once at the end for ALBEDO.

uniform sampler2D grass_masks : hint_default_black, filter_linear_mipmap, repeat_enable;
uniform sampler2D grass_season_atlas : hint_default_white, filter_nearest, repeat_disable;
uniform float grass_season = 1.0;
uniform float grass_jitter : hint_range(0.0, 0.3) = 0.0;

const int GRASS_COLS = 256;
const float GRASS_TAU = 6.28318530718;

// Colour of one atlas row at density d: lerp between texel centres floor(d*255) and the next.
vec3 grass_row(int row, float d) {
    float x = clamp(d, 0.0, 1.0) * float(GRASS_COLS - 1);
    int i0 = int(floor(x));
    int i1 = min(i0 + 1, GRASS_COLS - 1);
    float f = x - float(i0);
    vec3 c0 = texelFetch(grass_season_atlas, ivec2(i0, row), 0).rgb;
    vec3 c1 = texelFetch(grass_season_atlas, ivec2(i1, row), 0).rgb;
    return c0 * (1.0 - f) + c1 * f;
}

// Tile-periodic smooth fields in [-1, 1] for group g: x = hue tilt, y = value.
vec2 grass_jitter_fields(vec2 uv, int g) {
    float fg = float(g);
    float u = uv.x, v = uv.y;
    float nh = (sin(GRASS_TAU * (3.0 * u + 1.0 * v) + 0.7 + 1.3 * fg)
              + sin(GRASS_TAU * (-1.0 * u + 2.0 * v) + 2.1 + 1.3 * fg)
              + sin(GRASS_TAU * (2.0 * u - 3.0 * v) + 4.4 + 1.3 * fg)) / 3.0;
    float nv = (sin(GRASS_TAU * (1.0 * u - 2.0 * v) + 1.1 + 0.9 * fg)
              + sin(GRASS_TAU * (2.0 * u + 1.0 * v) + 3.3 + 0.9 * fg)
              + sin(GRASS_TAU * (-3.0 * u - 1.0 * v) + 5.0 + 0.9 * fg)) / 3.0;
    return vec2(nh, nv);
}

vec3 grass_apply_jitter(vec3 c, vec2 n, float jitter) {
    vec3 A = (1.0 - c) * (1.0 + jitter * 0.5 * n.y);
    A.r *= 1.0 - jitter * 0.6 * n.x;
    A.b *= 1.0 + jitter * 0.6 * n.x;
    return 1.0 - clamp(A, 0.0, 1.0);
}

// Grass over white, sRGB-encoded. masks = the RGBA mask sample; uv = tile-space UV (for jitter).
vec3 grass_recolor_srgb(vec4 masks, vec2 uv, float season, float jitter) {
    float s = mod(season, 4.0);
    int s0 = int(floor(s)) % 4;
    int s1 = (s0 + 1) % 4;
    float t = s - floor(s);
    vec3 result = vec3(1.0);
    for (int g = 0; g < 4; g++) {
        float d = masks[g];
        vec3 c0 = grass_row(s0 * 4 + g, d);
        vec3 c1 = grass_row(s1 * 4 + g, d);
        vec3 c = c0 + (c1 - c0) * t;
        if (jitter > 0.0) {
            c = grass_apply_jitter(c, grass_jitter_fields(uv, g), jitter);
        }
        result *= c;
    }
    return result;
}

vec3 grass_srgb_to_linear(vec3 c) {
    return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(vec3(0.04045), c));
}
