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

// Winter with structure (A41, `terrain-shader.md §7` piece 4, as A26 call 3 settled it): the snow's peak below a blanket so
// it lies in patches; cool shadows on the drifts' down-right flanks; the river darker in its channel with an icy rim inside
// the bank. All scaled by the year's snow (snow_amount), so nothing here shows outside winter.
uniform float snow_peak = 1.0;
uniform vec3 snow_shadow_srgb = vec3(1.0);
uniform float snow_shadow_strength = 0.0;
uniform vec3 ice_srgb = vec3(1.0);
uniform float ice_width_tiles = 0.0;
uniform float ice_strength = 0.0;
uniform float river_winter_darken = 0.0;

// The river drawn (A45, `specs/river-drawn.md`): echo lines just inside the bank, the channel darker down the middle,
// ripples drifting downstream. Off at 0 strength, each. The river's own coordinates come from RiverTable — one row a
// step down its course (middle, half-width, distance down, lean), read once a water pixel.
uniform bool river_on = false;
// The hills (A66, `hill-edges.md`): baked once a valley at four texels a tile — R the height (water filled from the land
// beside it), G 1 where the sim calls a tile steep (`slope_steep_at`) and 0 elsewhere, both softened over half a tile so a
// line drawn from them is smooth. Light and shade from the upper left on every slope; steep banks darker; an inked lip
// along the top of each steep bank, on the steep mask's halfway line.
uniform bool hills_on = false;
uniform sampler2D heights_map : hint_default_black, filter_linear, repeat_disable;
uniform float hill_full_tilt_levels = 16.0;
// ⭐ The green ground (A68, `specs/green-ground.md`, Joe's C on a mock): the grass's own ground a green wash by height
// instead of the parchment, its hue turned by the land's shape and the light, the brush and the tuft marks laid along
// the contours, hachures down the steep banks. Placeholders, as every value here: the look is in valley_watercolour.tres.
uniform bool green_on = false;
uniform vec3 green_deep = vec3(0.5);
uniform vec3 green_low = vec3(0.5);
uniform vec3 green_mid = vec3(0.5);
uniform vec3 green_high = vec3(0.5);
uniform vec3 green_crest = vec3(0.5);
uniform vec3 green_warm = vec3(0.5);
uniform vec3 green_cool = vec3(0.5);
uniform vec3 green_spring = vec3(1.0);
uniform vec3 green_summer = vec3(1.0);
uniform vec3 green_autumn = vec3(1.0);
uniform vec3 green_winter = vec3(1.0);
uniform vec2 green_levels = vec2(0.0, 255.0);
uniform float green_flat_level = 90.0;
uniform float green_wander = 0.0;
uniform vec2 green_crest_turn = vec2(0.0);
uniform float green_hollow = 0.0;
uniform vec2 green_warm_turn = vec2(0.0);
uniform float green_cool_turn = 0.0;
uniform float green_steep = 0.0;
uniform float green_kinds_share = 0.0;
uniform vec2 green_wood = vec2(0.0, 1.0);
uniform float green_grass_share = 1.0;
uniform float brush_strength = 0.0;
uniform vec2 brush_tiles = vec2(0.9, 0.07);
uniform float tuft_strength = 0.0;
uniform float tuft_cell_tiles = 1.0;
uniform vec2 tuft_length_tiles = vec2(0.22, 0.42);
uniform float tuft_share = 0.7;
uniform float tuft_width_px = 1.2;
uniform float tuft_fade_px = 14.0;
// The hachures (A68): one stroke a texel, baked once a world (VillageMap.HillHachures) - RG where it starts, from the
// texel's own centre, in quarter-texels; B its direction; A its length, 0 where no stroke is near. Read unfiltered.
uniform sampler2D hachure_map : hint_default_black, filter_nearest, repeat_disable;
uniform float hachure_texels_per_tile = 8.0;
uniform float hachure_strength = 0.0;
uniform float hachure_width_share = 0.7;
uniform float hachure_fade_px = 10.0;
uniform float hill_lip_strength = 0.8;
uniform float hill_lip_width_share = 0.8;
uniform sampler2D river_table : hint_default_black, filter_linear, repeat_disable;
uniform float river_rows = 1.0;
uniform vec2 river_along = vec2(1.0, 0.0);
uniform vec2 river_across = vec2(0.0, 1.0);
uniform float river_offset = 0.0;
uniform float channel_strength = 0.0;
uniform float echo_strength = 0.0;
uniform float echo_width_share = 0.5;
// The raw water weight at each echo's depth on a straight bank (VillageMap.BankWeightAt), and the depths, in tiles.
uniform vec2 echo_levels = vec2(0.7, 0.9);
uniform vec2 echo_tiles = vec2(0.15, 0.32);
uniform float echo_lifts = 0.5;
uniform float echo_dash_tiles = 1.2;
uniform float echo_fade_px = 3.0;
uniform float ripple_strength = 0.0;
uniform float ripple_width_px = 1.0;
uniform vec2 ripple_cell_tiles = vec2(1.6, 0.6);
uniform float ripple_share = 0.45;
uniform vec2 ripple_length_tiles = vec2(0.5, 0.9);
uniform float ripple_bow_tiles = 0.06;
uniform float ripple_bank_tiles = 0.35;
// The raw water weight a straight bank has ripple_bank_tiles inside it (VillageMap.BankWeightAt): the ripples keep to
// water weighed above it, so they keep off the drawn bank itself, step for step, whatever the table's smoothed middle does.
uniform float ripple_bank_level = 0.93;
uniform float ripple_drift = 0.35;
uniform float ripple_life_s = 6.0;
uniform float ripple_fade_px = 12.0;
uniform int ripple_repeat_cells = 256;
// The view's clock (never the sim's): real seconds while the game runs, still while it is paused (A26 call 2), the same
// at every speed (A45 call 1), wrapped at flow_period_s — a whole number of lives and of cells' drift, so no jump.
uniform float flow_seconds = 0.0;
uniform float flow_period_s = 1170.0;

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
// The pen itself, for the bank and its echoes (A45): a width, how long a swell of pressure is, where it lifts, and a salt
// so each line breaks in its own places.
float valley_pen(vec2 tile, float px_away, float width_px, float pressure_tiles, float lifts, uint salt) {
    float pressure = at_value_noise(tile / max(pressure_tiles, 1e-3), salt);
    float width = width_px * max(0.0, 1.0 + ink_pressure * (2.0 * pressure - 1.0));
    float lifted = smoothstep(lifts, lifts + 0.12, pressure);
    // Fibres about two and a half screen pixels across, wherever the zoom: the paper's scale, not the land's.
    float fibre = (at_value_noise(tile * px_per_tile / 2.5, 92u) - 0.5) * 2.0 * ink_feather_px;
    float edge = 0.5 * width;
    float body = 1.0 - smoothstep(edge - 0.5, edge + 0.5 + ink_feather_px, px_away + fibre);
    return body * lifted * mix(0.8, 1.0, pressure);
}

float valley_ink_stroke(vec2 tile, float px_away) {
    return valley_pen(tile, px_away, ink_width_px, ink_pressure_tiles, ink_lifts, 91u);
}

// ⭐ The ripples (A45, `river-drawn.md §4.3`), how dark at this pixel, 0 to 1: short bowed pen marks laid on a grid in the
// river's own coordinates — `down` the river and `across` it, in tiles — that drifts downstream with the clock. Each cell
// holds a ripple for a life of `ripple_life_s`, fading in over its first third and out over its last, drawn anew each
// life, so the water is never a conveyor belt. One cell is asked: a ripple never leaves its own.
float valley_ripples(float down, float across, float water) {
    float u = down - ripple_drift * flow_seconds;
    float ci = floor(u / ripple_cell_tiles.x);
    float cj = floor(across / ripple_cell_tiles.y);
    vec2 local = vec2(u - ci * ripple_cell_tiles.x, across - cj * ripple_cell_tiles.y);
    // The cell's place down the river, wrapped at ripple_repeat_cells: the clock's wrap moves the grid that many cells.
    float repeat = float(ripple_repeat_cells);
    int im = int(ci - repeat * floor(ci / repeat));
    ivec2 cell = ivec2(im, int(cj));
    float age = flow_seconds / ripple_life_s + at_rand(cell, 81u);
    int lives = max(1, int(round(flow_period_s / ripple_life_s)));
    int life = int(floor(age)) % lives;
    ivec2 drawn = ivec2(im + ripple_repeat_cells * life, int(cj));
    if (at_rand(drawn, 82u) >= ripple_share) {
        return 0.0;
    }

    float len = mix(ripple_length_tiles.x, ripple_length_tiles.y, at_rand(drawn, 83u));
    float cu = mix(0.5 * ripple_length_tiles.y, ripple_cell_tiles.x - 0.5 * ripple_length_tiles.y, at_rand(drawn, 84u));
    float margin = ripple_bow_tiles + 0.1;
    float cw = mix(margin, ripple_cell_tiles.y - margin, at_rand(drawn, 85u));
    float bow = at_rand(drawn, 86u) < 0.5 ? -ripple_bow_tiles : ripple_bow_tiles;
    float x = (local.x - cu) / (0.5 * len);
    if (abs(x) >= 1.0) {
        return 0.0;
    }

    float rise = 1.0 - x * x;
    float px_away = abs(local.y - (cw + bow * rise)) * px_per_tile;
    float edge = 0.5 * ripple_width_px * sqrt(rise);
    float body = 1.0 - smoothstep(edge - 0.5, edge + 0.5, px_away);
    float f = fract(age);
    float lived = smoothstep(0.0, 1.0 / 3.0, f) * (1.0 - smoothstep(2.0 / 3.0, 1.0, f));
    float off_bank = smoothstep(ripple_bank_level, 1.0, water);
    return body * lived * off_bank;
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

// ⭐ The brush (A68): faint streaks in the wash, long along `dir` and thin across it. One streak noise read at four
// fixed turns, an eighth of a circle apart, and mixed by how near `dir` lies to each (a direction and its opposite are
// the same stroke, so the weight is the cosine of twice the angle between them): at most two are ever read.
// World-anchored, and gone before a streak is thinner than a pixel. -1 to 1.
const vec2 VALLEY_TURNS[4] = vec2[](vec2(1.0, 0.0), vec2(0.70710678, 0.70710678), vec2(0.0, 1.0), vec2(-0.70710678, 0.70710678));

float valley_brush(vec2 tile, vec2 dir) {
    float keep = smoothstep(1.0, 2.0, brush_tiles.y * px_per_tile);
    if (keep <= 0.0) {
        return 0.0;
    }

    float sum = 0.0;
    float weights = 0.0;
    for (int k = 0; k < 4; k++) {
        vec2 u = VALLEY_TURNS[k];
        float along = dot(dir, u);
        float w = max(0.0, 2.0 * along * along - 1.0);
        if (w > 0.0) {
            vec2 at_ = vec2(dot(tile, u) / brush_tiles.x, dot(tile, vec2(-u.y, u.x)) / brush_tiles.y);
            float streak = 0.65 * at_value_noise(at_, 100u + uint(k)) + 0.35 * at_value_noise(at_ * vec2(1.7, 2.3), 104u + uint(k));
            sum += w * (2.0 * streak - 1.0);
            weights += w;
        }
    }

    return keep * sum / max(weights, 1e-4);
}

// ⭐ The tuft marks (A68): the reference's short paired pen dashes over the meadows, one hashed mark a cell, lying
// along `dir`. A mark may sit anywhere in its cell, so the four cells nearest are asked. How dark at this pixel, 0 to 1.
float valley_tufts(vec2 tile, vec2 dir) {
    float seen = smoothstep(tuft_fade_px * (2.0 / 3.0), tuft_fade_px, px_per_tile);
    if (seen <= 0.0) {
        return 0.0;
    }

    vec2 across = vec2(-dir.y, dir.x);
    ivec2 first = ivec2(floor(tile / tuft_cell_tiles - 0.5));
    float mark = 0.0;
    for (int j = 0; j < 2; j++) {
        for (int i = 0; i < 2; i++) {
            ivec2 cell = first + ivec2(i, j);
            if (at_rand(cell, 110u) >= tuft_share) {
                continue;
            }

            vec2 centre = (vec2(cell) + vec2(at_rand(cell, 111u), at_rand(cell, 112u))) * tuft_cell_tiles;
            float len = mix(tuft_length_tiles.x, tuft_length_tiles.y, at_rand(cell, 113u));
            vec2 rel = tile - centre;
            float a = dot(rel, dir);
            float b = dot(rel, across);
            float pressed = mix(0.55, 1.0, at_rand(cell, 114u));
            // The first dash, and for most marks a second, shorter, beside and a little along it.
            float x = a / (0.5 * len);
            if (abs(x) < 1.0) {
                float edge = 0.5 * tuft_width_px * sqrt(1.0 - x * x);
                mark = max(mark, pressed * (1.0 - smoothstep(edge - 0.5, edge + 0.5, abs(b) * px_per_tile)));
            }

            if (at_rand(cell, 115u) < 0.6) {
                float x2 = (a - 0.25 * len) / (0.4 * len);
                if (abs(x2) < 1.0) {
                    float edge = 0.5 * tuft_width_px * sqrt(1.0 - x2 * x2);
                    mark = max(mark, pressed * (1.0 - smoothstep(edge - 0.5, edge + 0.5, abs(b - 0.4 * len) * px_per_tile)));
                }
            }
        }
    }

    return seen * mark;
}

// ⭐ The hachures (A68): pen strokes from a steep bank's lip down it. The bake names, for each texel, the one stroke
// nearest it; the distance to that stroke is worked out here, so the pen is as thin on screen at every zoom as the
// bank's own. A stroke tapers toward its tail and bows a touch, as a hand's does. `corner` is tiles from the valley's
// corner. How dark at this pixel, 0 to 1.
float valley_hachures(vec2 corner) {
    float seen = smoothstep(hachure_fade_px * (2.0 / 3.0), hachure_fade_px, px_per_tile);
    if (seen <= 0.0) {
        return 0.0;
    }

    vec2 texel = floor(corner * hachure_texels_per_tile);
    vec4 s = round(texelFetch(hachure_map, ivec2(texel), 0) * 255.0);
    if (s.a < 0.5) {
        return 0.0;
    }

    vec2 start = (texel + 0.5 + (s.rg - 128.0) * 0.25) / hachure_texels_per_tile;
    float angle = s.b * (6.28318530718 / 256.0);
    vec2 d = vec2(cos(angle), sin(angle));
    float len = s.a / 128.0;
    vec2 rel = corner - start;
    float x = dot(rel, d) / len;
    if (x < 0.0 || x > 1.0) {
        return 0.0;
    }

    ivec2 id = ivec2(round(start * hachure_texels_per_tile * 4.0));
    float bow = (at_rand(id, 120u) - 0.5) * 0.12 * len;
    float away = abs(dot(rel, vec2(-d.y, d.x)) - bow * 4.0 * x * (1.0 - x)) * px_per_tile;
    float edge = 0.5 * ink_width_px * hachure_width_share * mix(1.0, 0.3, x);
    float pressed = mix(0.7, 1.0, at_rand(id, 121u));
    return seen * pressed * (1.0 - smoothstep(edge - 0.5, edge + 0.5, away));
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
        // The water weight's slope on screen, for the echo lines' own contours (A45) — a derivative, so taken here.
        float water_slope = max(length(vec2(dFdx(weights.a), dFdy(weights.a))), 1e-5);

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
        // The hills' field (A66, `hill-edges.md §4`): the height, the steep mask and (A68) the crest at this pixel, and the
        // first two's slopes from a tile's width either side.
        vec2 corner = tile + 0.5 * ground_map_tiles;
        vec2 hill_slope = vec2(0.0);
        vec2 steep_slope = vec2(0.0);
        vec3 hill = vec3(green_flat_level, 0.0, 0.0);
        if (hills_on) {
            vec2 at_ = corner / ground_map_tiles;
            vec2 ex = vec2(0.5, 0.0) / ground_map_tiles;
            vec2 ey = vec2(0.0, 0.5) / ground_map_tiles;
            vec2 east = textureLod(heights_map, at_ + ex, 0.0).rg, west = textureLod(heights_map, at_ - ex, 0.0).rg;
            vec2 south = textureLod(heights_map, at_ + ey, 0.0).rg, north = textureLod(heights_map, at_ - ey, 0.0).rg;
            hill_slope = vec2(east.r - west.r, south.r - north.r);
            steep_slope = vec2(east.g - west.g, south.g - north.g);
            hill = textureLod(heights_map, at_, 0.0).rgb;
        }

        // ⭐ The green ground (A68, `specs/green-ground.md §3`). The grass's own ground: a green by height, the same green
        // for the same height in every valley; a crest turned toward the crest's yellow and a hollow toward the deep green;
        // a slope turned warmer facing the light and cooler facing away; steep ground the coolest. ⛔ Every step is a mix
        // toward a colour, never a multiply toward black: four grey washes were refused as shadows (A67).
        vec3 own = ground_srgb;
        vec3 turn = vec3(1.0);
        vec2 brush_dir = vec2(1.0, 0.0);
        if (green_on) {
            vec3 normal = normalize(vec3(-hill_slope, hill_full_tilt_levels));
            vec3 light = normalize(vec3(-0.62, -0.62, 0.48));
            float lit = dot(normal, light) - light.z;
            float t = (hill.r - green_levels.x) / max(green_levels.y - green_levels.x, 1.0);
            t = clamp(t + green_wander * (2.0 * at_value_noise(tile / 14.0, 70u) - 1.0), 0.0, 1.0);
            vec3 plain = mix(green_deep, green_low, clamp(t / 0.2, 0.0, 1.0));
            plain = mix(plain, green_mid, clamp((t - 0.2) / 0.4, 0.0, 1.0));
            plain = mix(plain, green_high, clamp((t - 0.6) / 0.4, 0.0, 1.0));
            vec3 g = mix(plain, green_crest, min(max(hill.b, 0.0) * green_crest_turn.x, green_crest_turn.y));
            g = mix(g, green_deep, clamp(-hill.b * green_hollow, 0.0, 1.0));
            g = mix(g, green_warm, min(max(lit, 0.0) * green_warm_turn.x, green_warm_turn.y));
            g = mix(g, green_cool, clamp(-lit * green_cool_turn, 0.0, 1.0));
            g = mix(g, green_cool, green_steep * hill.g);
            // The seams keep their own paintings and take a share of the same turn (§3.7).
            turn = mix(vec3(1.0), g / max(plain, vec3(1e-3)), green_kinds_share);
            // The season's tint over the summer's green (§3.6), turning as the grass's season turns.
            float of_year = mod(season, 4.0);
            vec3 from_tint = of_year < 1.0 ? green_spring : of_year < 2.0 ? green_summer : of_year < 3.0 ? green_autumn : green_winter;
            vec3 to_tint = of_year < 1.0 ? green_summer : of_year < 2.0 ? green_autumn : of_year < 3.0 ? green_winter : green_spring;
            own = g * mix(from_tint, to_tint, fract(of_year));
            strength *= green_grass_share;
            // The woods' floor (§3.7): the same green a little deeper, carrying its own painting's light and dark (and the
            // young wood's lift), so a wood stands on the land's colour and not on a pale clearing.
            wood = mix(wood * turn, own * green_wood.y * wood / max(kind_wood, vec3(1e-3)), green_wood.x);
            seam *= turn;

            // The brush's direction (§3.5): along the contour where the land slopes, the painter's own wandering hand
            // where it is flat. Mixed as doubled angles, where a direction and its opposite are one, so it never flips.
            float tilt = length(hill_slope) / 16.0;
            float wander = (at_value_noise(tile / 9.0, 71u) - 0.5) * 1.2;
            vec2 doubled = vec2(cos(2.0 * wander), sin(2.0 * wander));
            if (tilt > 1e-4) {
                vec2 contour = vec2(-hill_slope.y, hill_slope.x) / length(hill_slope);
                vec2 contour2 = vec2(contour.x * contour.x - contour.y * contour.y, 2.0 * contour.x * contour.y);
                doubled = mix(doubled, contour2, smoothstep(0.12, 0.42, tilt));
            }

            float lie = 0.5 * atan(doubled.y, doubled.x);
            brush_dir = vec2(cos(lie), sin(lie));
        }

        vec3 ground = terrain_ground(kinds, weights.a, own, wood, seam, deep, shallows);
        float covered = terrain_coverage(grass_weight, coverage_fade);
        strength *= covered;

        vec3 grass = grass_recolor_srgb(m, tile_uv, season, grass_jitter);
        vec3 colour = ground * mix(vec3(1.0), grass, strength);
        if (green_on) {
            // The brush in the wash, on all the land; the tuft marks on the grass only, off the steep banks (the
            // hachures' ground), under the snow.
            colour *= 1.0 + brush_strength * (1.0 - kinds.a) * valley_brush(tile, brush_dir);
            if (tuft_strength > 0.0) {
                float marks = valley_tufts(tile, brush_dir) * covered * (1.0 - smoothstep(0.2, 0.5, hill.g));
                colour = mix(colour, ink_srgb, tuft_strength * marks);
            }
        }

        // The river's own coordinates (A45, `river-drawn.md §3`): one row of the table, read only where water is drawn.
        // `across` is square to the river (the lean undoes the course's square), `down` how far it has run.
        float river_down = 0.0;
        float river_across_tiles = 0.0;
        float river_half = 0.0;
        bool on_river = river_on && kinds.a > VALLEY_SEEN;
        if (on_river) {
            vec2 corner = tile + 0.5 * ground_map_tiles;
            vec4 row = textureLod(river_table, vec2((dot(corner, river_along) + river_offset) / river_rows, 0.5), 0.0);
            river_across_tiles = (dot(corner, river_across) - row.x) * row.w;
            river_half = row.y * row.w;
            river_down = row.z;

            // The channel (§4.2): the wash darker toward the middle, easing to nothing at the bank.
            if (channel_strength > 0.0) {
                float deep = smoothstep(0.0, 1.0, 1.0 - clamp(abs(river_across_tiles) / max(river_half, 1e-3), 0.0, 1.0));
                colour *= 1.0 - channel_strength * deep * kinds.a;
            }
        }

        // Pooling (A27): the river's wash darker at its own edge, as pigment gathers where a wash dries. Only the river's
        // since A28 — on the woods and seams a darker rim read as the outline Joe ruled out. Under the snow.
        if (pool_strength > 0.0) {
            colour *= 1.0 - pool_strength * valley_pool(weights.a, from_water, pool_width_px);
        }

        // Snow on the season's amount (A23), in the approved blend (Lerp, A9): its noise is asked only while there is snow.
        if (snow_amount > 0.0) {
            // The river in winter (A41): darker in its channel, and an icy rim just inside the bank, before the snow (which
            // keeps off the water) and under the ink.
            if (kinds.a > 0.0) {
                colour *= 1.0 - river_winter_darken * snow_amount * kinds.a;
                float rim = weights.a > 0.5 ? 1.0 - smoothstep(0.0, max(ice_width_tiles * px_per_tile, 1e-3), from_water) : 0.0;
                colour = mix(colour, ice_srgb, ice_strength * snow_amount * rim * kinds.a);
            }

            // The snow, peaking at snow_peak of a blanket (A26 call 3: patchy), so the ground shows between its drifts.
            float threshold = terrain_snow_threshold(tile, snow_patch_tiles);
            float cover = terrain_snow_cover(threshold, snow_amount * snow_peak, snow_patchiness) * terrain_snow_off_water(kinds.a);
            colour = terrain_snow(colour, snow_srgb, cover, snow_blend);

            // Cool shadows in the snow (A41): a drift is deep where the threshold is low, so where the field rises toward the
            // light (up-left) the snow's surface falls away from it — the drift's lee, washed blue-grey.
            if (snow_shadow_strength > 0.0) {
                float lee = clamp((threshold - terrain_snow_threshold(tile - vec2(0.45), snow_patch_tiles)) * 6.0, 0.0, 1.0);
                colour = mix(colour, colour * snow_shadow_srgb, snow_shadow_strength * lee * cover);
            }
        }

        // The ink (A27–A28): a pen stroke on the river's bank — over the snow, so the map still reads in winter, and under
        // the paper's grain, which is the paper's tooth under the pen.
        // The river drawn (A45): the echo lines and the ripples, in the same ink, under the bank's own stroke.
        if (on_river && weights.a > 0.5) {
            if (echo_strength > 0.0) {
                // Two contours of the raw water weight, each at the weight a straight bank has at its depth (§4.1), held
                // still; each fades out where it would lie within echo_fade_px of the bank (the wide zoom).
                vec2 seen = smoothstep(vec2(echo_fade_px), vec2(2.0 * echo_fade_px), echo_tiles * px_per_tile);
                float near = valley_pen(tile, abs(weights.a - echo_levels.x) / water_slope, ink_width_px * echo_width_share,
                    echo_dash_tiles, echo_lifts, 93u);
                float far = valley_pen(tile, abs(weights.a - echo_levels.y) / water_slope, ink_width_px * echo_width_share,
                    echo_dash_tiles, echo_lifts, 94u);
                colour = mix(colour, ink_srgb, echo_strength * seen.x * near * kinds.a);
                colour = mix(colour, ink_srgb, echo_strength * seen.y * far * kinds.a);
            }

            if (ripple_strength > 0.0) {
                float seen = smoothstep(ripple_fade_px * (2.0 / 3.0), ripple_fade_px, px_per_tile);
                if (seen > 0.0) {
                    float ripple = valley_ripples(river_down, river_across_tiles, weights.a);
                    colour = mix(colour, ink_srgb, ripple_strength * seen * ripple * kinds.a);
                }
            }
        }

        // ⭐ The pen on the hills: A66's lip, and (A68) the hachures down the bank from it. A66's light, shade and darker
        // bank are gone (A67: Joe, *"cloud shadows"*); the green ground above tells the slope instead.
        if (hills_on) {
            float land = 1.0 - kinds.a;
            // The lip: the steep mask's halfway line, on the uphill side of the band only (going uphill the mask falls, so
            // its gradient and the height's point opposite ways). ⚠️ The mask, not the steepness itself: a Rolling valley's
            // banks are only just steep, so a line where the steepness crosses 8 was a faint, uncertain thing; the mask goes
            // 0 to 1 across a bank's edge however steep the bank. Its distance in screen pixels is taken from the mask's own
            // slope, sampled a tile across as the light is (⚠️ not screen derivatives: they work in 2 × 2 blocks and inked a
            // square wherever the line hovered over a patch). A lone steep tile softens below a half and draws no lip.
            float f = hill.g - 0.5;
            float across_tile = length(steep_slope);
            float f_px = abs(f) / max(across_tile, 1e-3) * px_per_tile;
            float top = step(dot(steep_slope, hill_slope), 0.0);
            float crisp = smoothstep(0.08, 0.25, across_tile);
            float lip = valley_pen(tile, f_px, ink_width_px * hill_lip_width_share, ink_pressure_tiles, ink_lifts, 95u);
            colour = mix(colour, ink_srgb, hill_lip_strength * lip * top * crisp * land);
            if (hachure_strength > 0.0) {
                colour = mix(colour, ink_srgb, hachure_strength * valley_hachures(corner) * land);
            }
        }

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
