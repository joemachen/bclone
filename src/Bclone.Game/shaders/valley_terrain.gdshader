// THE VALLEY through the terrain shader (`specs/terrain-shader.md §5`, A22–A25): the lab's approved look on the real
// ground's mix, drawn by VillageMap's valley layer behind the map, behind the *paint the valley in watercolour* tick.
//
// One rect over the whole map. Outside the valley it is the map's Beyond; left of the before/after line it is the
// valley exactly as the bake draws it today; right of it, the new look: Joe's grass (grass_approved.tres) Multiply over
// the parchment and the painted ground kinds, the edges splatted and bled at A7's values, the snow on the season (A23),
// and the paper's grain on the ground only (A23, call 3 — paths, fields and everything standing are drawn over it).
//
// ⭐ THE LOOK IS NOT HERE. Every value comes from res://looks/grass_approved.tres and res://looks/terrain_lab.tres via
// VillageMap; the defaults below are placeholders, nothing more. The functions are the lab's own (terrain_lab and
// grass_antitile includes, unedited), so the valley draws what Joe approved in the lab.
//
// ⚠️ LEAN (Joe, A23): a painted kind is sampled only where its share can move a byte (VALLEY_SEEN), the snow's noise
// only while there is snow, and the before side of the line pays for one texture read.
shader_type canvas_item;

#include "res://shaders/grass_seasons.gdshaderinc"
#include "res://shaders/grass_antitile.gdshaderinc"
#include "res://shaders/terrain_lab.gdshaderinc"

// Where the valley is on the layer, in its own pixels: the rect ValleyTexture bakes, half a tile past the outermost
// tile centres (D338).
uniform vec2 valley_origin_px = vec2(0.0);
uniform vec2 valley_size_px = vec2(1.0);
uniform float px_per_tile = 16.0;

// Today's valley, the bake — drawn left of the line, and nowhere else.
uniform sampler2D valley_colour : hint_default_black, filter_linear, repeat_disable;
// The before/after line, as a fraction of the valley's width: left of it is the bake. Below 0 there is no line.
uniform float split_u = -1.0;

// The young share of the wood (R) and the iron share of the seam (G), beside the mix (ValleyTexture, A24).
uniform sampler2D ground_map_shares : hint_default_black, filter_linear, repeat_disable;

// The map's colours, sRGB-encoded as written.
uniform vec3 beyond_srgb = vec3(0.77, 0.71, 0.56);
uniform vec3 ground_srgb = vec3(0.902, 0.839, 0.690);

// The grass look (grass_approved.tres). Version mixing and the linear blend were rejected (D632) and are not here.
uniform float tiles_per_repeat = 63.0;
uniform float grass_strength = 0.8;
uniform bool macro_on = true;
uniform float macro_cycle_tiles = 85.0;
uniform float macro_strength_amount = 0.37;
uniform float macro_season_amount = 0.1;
uniform bool hex_on = true;
uniform float hex_sharpness = 8.5;
uniform bool height_blend = true;

// The terrain look (terrain_lab.tres; A7, A9). The coverage's fade is the lab's fake map's, not approved (§5.3).
uniform float coverage_fade = 0.25;
uniform float splat_sharpness = 5.5;
uniform float bleed_reach_tiles = 0.33;
uniform float bleed_scale_tiles = 0.3;
uniform vec3 snow_srgb = vec3(1.0);
uniform int snow_blend = 0;
uniform float snow_amount = 0.0;
uniform float snow_patchiness = 0.03;
uniform float snow_patch_tiles = 3.0;
uniform float grain_amount = 0.12;
uniform float grain_scale_tiles = 0.065;

// The ink and the pooling (A27–A28, `terrain-shader.md §7`; valley_watercolour.tres): a pen stroke on the river's bank —
// ⛔ only there: woods and seams are not outlined (Joe, A28: "the trees themselves have an outline, but the ground isn't
// outlined") — and the river's wash darker at its own edge. Off at 0 strength.
uniform vec3 ink_srgb = vec3(0.23, 0.18, 0.13);
uniform float ink_strength = 0.0;
uniform float ink_width_px = 1.8;
// The pen's pressure: how far the width swells and thins along the stroke (0 even, 1 from nothing to double), and how
// long one swell is, in tiles.
uniform float ink_pressure = 0.0;
uniform float ink_pressure_tiles = 2.5;
// Where the pressure is lowest the pen lifts: the share of the pressure's range under which the stroke breaks.
uniform float ink_lifts = 0.0;
// How far the ink's edge creeps into the paper's fibres, in screen pixels.
uniform float ink_feather_px = 0.0;
uniform float pool_strength = 0.0;
uniform float pool_width_px = 3.0;

// Hex lattice vertices per repeat: the grass's own (grass_lab.gdshader's HEX_PER_REPEAT).
const float VALLEY_HEX_PER_REPEAT = 2.0;
// A share below this moves no byte of the colour (under half a level of 255), so the kind's painting is not read.
const float VALLEY_SEEN = 0.002;

varying vec2 local_px;

void vertex() {
    local_px = VERTEX;
}

// A share sharpened at the splat's power, the way every land edge meets (A22: young wood and iron are shares, not
// the lab's pick, so they meet their neighbour as a splatted edge rather than a hard line).
float valley_share(float s, float k) {
    float a = pow(s, k);
    float b = pow(1.0 - s, k);
    return a / max(a + b, 1e-6);
}

// How far this pixel lies, in screen pixels, from where a share crosses one half — that share's own contour, which is
// where the colour changes kind. The share's slope on screen turns its distance from a half into pixels, so a line drawn
// at a fixed distance is the same width at every zoom.
float valley_px_from_half(float share) {
    vec2 slope = vec2(dFdx(share), dFdy(share));
    return abs(share - 0.5) / max(length(slope), 1e-5);
}

// ⭐ An ink stroke on a contour (A28, Joe: "i want it to look like actual ink"), how dark it lies at this pixel, 0 to 1.
// Three things a pen does that a vector line does not: its PRESSURE swells and thins the stroke along its length, and the
// ink is a touch denser where it is laid thick; where the pressure is lightest the pen LIFTS and the stroke breaks; and
// its edge FEATHERS into the paper's fibres, ragged at a pixel's scale. All three are world-anchored noise, so the stroke
// holds still on the land as the view pans, and is the same stroke every time a place is seen.
float valley_ink_stroke(vec2 tile, float px_away) {
    float pressure = at_value_noise(tile / max(ink_pressure_tiles, 1e-3), 91u);
    float width = ink_width_px * max(0.0, 1.0 + ink_pressure * (2.0 * pressure - 1.0));
    float lifted = smoothstep(ink_lifts, ink_lifts + 0.12, pressure);
    // Fibres about two and a half screen pixels across, wherever the zoom: the paper's scale, not the land's.
    float fibre = (at_value_noise(tile * px_per_tile / 2.5, 92u) - 0.5) * 2.0 * ink_feather_px;
    float edge = 0.5 * width;
    float body = 1.0 - smoothstep(edge - 0.5, edge + 0.5 + ink_feather_px, px_away + fibre);
    return body * lifted * mix(0.8, 1.0, pressure);
}

// Pigment pooled at a wash's edge: whole at the contour, gone `reach` pixels inside — and nothing outside the wash.
float valley_pool(float share, float px_away, float reach) {
    return share > 0.5 ? 1.0 - smoothstep(0.0, max(reach, 1e-3), px_away) : 0.0;
}

// One octave of the paper's world-anchored grain (paper_grain.gdshader's, A3): whole from two pixels a cycle, faded
// to its own mean at one, so zooming out loses fibres without lightening the page.
float valley_grain_octave(vec2 tile, float cycle, uint salt) {
    float keep = smoothstep(1.0, 2.0, cycle * px_per_tile);
    return mix(0.5, at_value_noise(tile / cycle, salt), keep);
}

void fragment() {
    vec2 uv = (local_px - valley_origin_px) / valley_size_px;
    // Tiles from the valley's centre — where the mix's own UV is 0.5 (terrain_weights).
    vec2 tile = (uv - 0.5) * ground_map_tiles;
    vec2 tile_uv = tile / tiles_per_repeat;
    // Derivatives first, where every pixel runs, before any branch.
    vec2 gx = dFdx(tile_uv);
    vec2 gy = dFdy(tile_uv);
    vec2 tx = dFdx(tile);
    vec2 ty = dFdy(tile);

    if (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0) {
        COLOR = vec4(beyond_srgb, 1.0);
    } else if (uv.x < split_u) {
        COLOR = vec4(texture(valley_colour, uv).rgb, 1.0);
    } else {
        // The grass masks: hex-tiled with the height blend, as approved (D632), or the plain repeat.
        vec4 m;
        if (hex_on) {
            vec3 w;
            ivec2 v1;
            ivec2 v2;
            ivec2 v3;
            at_tri_grid(tile_uv * VALLEY_HEX_PER_REPEAT, w, v1, v2, v3);
            w = pow(w, vec3(hex_sharpness));
            w = w / max(w.x + w.y + w.z, 1e-6);
            mat2 r1 = at_turn(v1);
            mat2 r2 = at_turn(v2);
            mat2 r3 = at_turn(v3);
            vec4 s1 = textureGrad(grass_masks, r1 * tile_uv + at_shift(v1), r1 * gx, r1 * gy);
            vec4 s2 = textureGrad(grass_masks, r2 * tile_uv + at_shift(v2), r2 * gx, r2 * gy);
            vec4 s3 = textureGrad(grass_masks, r3 * tile_uv + at_shift(v3), r3 * gx, r3 * gy);
            vec3 k = at_blend(w, vec3(at_height(s1), at_height(s2), at_height(s3)), height_blend);
            m = s1 * k.x + s2 * k.y + s3 * k.z;
        } else {
            m = textureGrad(grass_masks, tile_uv, gx, gy);
        }

        float season = grass_season;
        float strength = grass_strength;
        if (macro_on) {
            vec2 n = at_macro(tile, macro_cycle_tiles);
            strength *= 1.0 - macro_strength_amount * n.x;
            season += macro_season_amount * (2.0 * n.y - 1.0);
        }

        // The ground under the grass: the mix, its land edges bled and splatted, the water's contour kept (§3.2).
        vec4 weights = terrain_weights(tile);
        vec3 land = terrain_bleed_land(tile, weights.rgb, bleed_reach_tiles, bleed_scale_tiles);
        vec4 kinds = terrain_splat_keeping_water(weights, land, splat_sharpness);
        float grass_weight = land.x / max(land.x + land.y + land.z, 1e-4) * (1.0 - weights.a);
        vec2 shares = texture(ground_map_shares, tile / ground_map_tiles + 0.5).rg;
        float young = valley_share(shares.r, splat_sharpness);
        float iron = valley_share(shares.g, splat_sharpness);

        // The bank's contour, in screen pixels — taken here, outside every branch below, because it is a derivative (A27).
        // The water's weight is unwarped (D342), so the stroke lies on the bank the villagers cannot cross.
        // ⚠️ From the RAW weight, not the drawn (hard) share (A28): the drawn share goes from land to water in about three
        // pixels and is flat either side, so its slope says nothing past them — a stroke or a pooling measured from it could
        // never be wider than three pixels, whatever the file asked (seen: 1.2 and 2.6 px drew the same line). The raw
        // weight changes over a tile, and crosses a half on the drawn bank wherever two kinds meet.
        // ⚠️ Only the bank (A28): A27 also inked the woods' and seams' edges (on the unbled edge, after the bled one drew A7's
        // fray as a ragged line), and Joe ruled the ground is not outlined — the trees are.
        float from_water = valley_px_from_half(weights.a);

        vec3 wood = kind_wood;
        vec3 seam = kind_stone;
        vec3 deep = kind_water;
        vec3 shallows = kind_shallows;
        if (kinds.y > VALLEY_SEEN) {
            wood = terrain_painted(ground_wood, tile, tx, ty, hex_sharpness)
                * mix(vec3(1.0), kind_young / max(kind_wood, vec3(1e-3)), young);
        }
        if (kinds.z > VALLEY_SEEN) {
            vec3 stone = iron < 1.0 - VALLEY_SEEN ? terrain_stone(tile, tx, ty, hex_sharpness) : vec3(0.0);
            vec3 ore = iron > VALLEY_SEEN ? terrain_iron(tile, tx, ty, hex_sharpness) : vec3(0.0);
            seam = mix(stone, ore, iron);
        }
        if (kinds.w > VALLEY_SEEN) {
            deep = terrain_water(tile, tx, ty, hex_sharpness);
            shallows = terrain_shallows(deep);
        }
        vec3 ground = terrain_ground(kinds, weights.a, ground_srgb, wood, seam, deep, shallows);
        strength *= terrain_coverage(grass_weight, coverage_fade);

        vec3 grass = grass_recolor_srgb(m, tile_uv, season, grass_jitter);
        vec3 colour = ground * mix(vec3(1.0), grass, strength);

        // Pooling (A27): the river's wash darker at its own edge, as pigment gathers where a wash dries. Only the river's
        // since A28 — on the woods and seams a darker rim read as the outline Joe ruled out. Under the snow.
        if (pool_strength > 0.0) {
            colour *= 1.0 - pool_strength * valley_pool(weights.a, from_water, pool_width_px);
        }

        // Snow on the season's amount (A23), in the approved blend (Lerp, A9): its noise is asked only while there is snow.
        if (snow_amount > 0.0) {
            float cover = terrain_snow_cover(terrain_snow_threshold(tile, snow_patch_tiles), snow_amount, snow_patchiness);
            colour = terrain_snow(colour, snow_srgb, cover * terrain_snow_off_water(kinds.a), snow_blend);
        }

        // The ink (A27–A28): a pen stroke on the river's bank — over the snow, so the map still reads in winter, and under
        // the paper's grain, which is the paper's tooth under the pen.
        if (ink_strength > 0.0) {
            colour = mix(colour, ink_srgb, ink_strength * valley_ink_stroke(tile, from_water));
        }

        // The paper's grain on the ground (A23, call 3), over the snow (§2), world-anchored at A9's scale.
        float s = grain_scale_tiles;
        float n = 0.5 * valley_grain_octave(tile, s, 61u) + 0.3 * valley_grain_octave(tile, s * (5.0 / 1.3), 62u)
            + 0.2 * valley_grain_octave(tile, s * (37.0 / 1.3), 63u);
        colour *= 1.0 - grain_amount * n;

        COLOR = vec4(colour, 1.0);
    }
}
