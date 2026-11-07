// The terrain shader's layers (`specs/terrain-shader.md`, D700). LAB ONLY until the valley is wired (§5).
//
// Pure functions of place: no TIME, no clock. Every layer is behind its own switch in grass_lab.gdshader, and with every
// switch off that shader draws the approved look byte for byte. The uniform defaults here are "off", not a look: the
// starting values are in res://looks/terrain_lab.tres.

// The fake coverage map (GrassLabGround.cs), weights: R grass, G wood, B stone, A water. Data, so no source_color.
// R is the channel ValleyTexture's bake will write.
uniform sampler2D ground_map : hint_default_white, filter_linear, repeat_disable;
uniform vec2 ground_map_tiles = vec2(120.0, 80.0);

// The ground kinds, sRGB-encoded, from VillageMap's palette. No source_color: used exactly as written.
uniform vec3 kind_wood = vec3(0.5);
uniform vec3 kind_stone = vec3(0.5);
uniform vec3 kind_water = vec3(0.5);
uniform vec3 kind_shallows = vec3(0.5);

// How the weights are sharpened into one kind: at this power a kind's edge is the bake's hard contour.
const float TERRAIN_HARD = 48.0;

// The map's weights at a tile, tiles counted from the map's centre. Off the map the clamp reads its edge: grass.
vec4 terrain_weights(vec2 tile) {
    return texture(ground_map, tile / ground_map_tiles + 0.5);
}

// Each kind's share once the weights are raised to `sharpness`. The top weight is scaled to one first, so the power
// never underflows where every kind is small.
vec4 terrain_splat(vec4 w, float sharpness) {
    float top = max(max(w.x, w.y), max(w.z, w.w));
    vec4 p = pow(max(w / max(top, 1e-4), vec4(1e-3)), vec4(sharpness));
    return p / (p.x + p.y + p.z + p.w);
}

// The ground under the grass. The grass's own ground is the parchment; the river's depth is ValleyTexture.Dressed's,
// shallows at the bank, read off the same weight as the fill, so the two cannot disagree (D338).
vec3 terrain_ground(vec4 k, float water_weight, vec3 parchment) {
    vec3 water = mix(kind_shallows, kind_water, smoothstep(0.45, 0.95, water_weight));
    return parchment * k.x + kind_wood * k.y + kind_stone * k.z + water * k.w;
}

// How much grass a place takes: none where grass does not win, rising from the contour inward, as ValleyTexture.Winner's
// confidence does. So grass never lands on the water or a wood.
float terrain_coverage(float grass_weight, float fade) {
    return smoothstep(0.5, 0.5 + max(fade, 1e-3), grass_weight);
}

// ---- Snow (`specs/terrain-shader.md §2`, D703) -------------------------------------------------------------------
// Its own layer with its own blend, over the grass and under the paper grain: Multiply only darkens, so it cannot make
// a white. ⚠️ Uses grass_antitile.gdshaderinc's at_value_noise, so that file is included first.

// Where snow lies first: a threshold per place, two octaves of the macro variation's integer-hash noise at their own
// salts. Thin snow is therefore always in the same places.
float terrain_snow_threshold(vec2 tile, float patch_tiles) {
    vec2 p = tile / patch_tiles;
    return 0.65 * at_value_noise(p, 71u) + 0.35 * at_value_noise(p * 2.03 + 13.0, 72u);
}

// How white a place is: `smoothstep(t - s, t + s, amount)`, the amount stretched by the patchiness first so that 0 is
// bare everywhere and 1 white everywhere, whatever the threshold.
float terrain_snow_cover(float threshold, float amount, float patchiness) {
    float s = max(patchiness, 1e-3);
    return smoothstep(threshold - s, threshold + s, mix(-s, 1.0 + s, amount));
}

// Snow does not lie on the river: none wherever water is drawn, so the snow's edge is the bank's own contour. ⚠️ The
// water's DRAWN share (terrain_splat's .a), never its raw weight: where three kinds meet, water wins below 0.5, and a
// ramp on the weight laid half-strength snow on visible water there (measured, D703).
float terrain_snow_off_water(float water_drawn) {
    return 1.0 - water_drawn;
}

// The three blends §2 compares: 0 Lerp (covers), 1 Screen (lifts, the darks show through), 2 Lighten (the brighter wins).
vec3 terrain_snow(vec3 ground, vec3 snow, float cover, int blend) {
    if (blend == 1) {
        return 1.0 - (1.0 - ground) * (1.0 - snow * cover);
    } else if (blend == 2) {
        return max(ground, snow * cover);
    }
    return mix(ground, snow, cover);
}

// ---- Splat blending and edge bleed (`specs/terrain-shader.md §3.2`, D704) ----------------------------------------

// The land kinds' shares among themselves (grass, wood, stone) at `sharpness`: terrain_splat without the water.
vec3 terrain_splat_land(vec3 w, float sharpness) {
    float top = max(max(w.x, w.y), w.z);
    // ⚠️ Deep in the river there is no land at all: every share would be (1e-3)^sharpness, which underflows to 0, and
    // 0 / 0 drew the river as NaN (measured, D704). The share is multiplied by the land's total, 0 here, so any will do.
    if (top < 1e-4) {
        return vec3(1.0, 0.0, 0.0);
    }
    vec3 p = pow(max(w / max(top, 1e-4), vec3(1e-3)), vec3(sharpness));
    return p / (p.x + p.y + p.z);
}

// ⛔ The water keeps its contour (D342): its drawn share comes from the UNWARPED weights at the bake's HARD power,
// and the land kinds split whatever is left by their own (perhaps bled) weights at `sharpness`. So no slider can move
// or soften the bank: splat and bleed run between land kinds only. (D704: a soft water edge at sharpness 1 washed
// the river a tile into the land and drew it 22 % smaller; the bank is where a villager cannot walk.) With `land` the
// unwarped weights and `sharpness` the hard power this is terrain_splat exactly, up to rounding.
vec4 terrain_splat_keeping_water(vec4 plain, vec3 land, float sharpness) {
    float water = terrain_splat(plain, TERRAIN_HARD).a;
    return vec4(terrain_splat_land(land, sharpness) * (1.0 - water), water);
}

// The land kinds' weights read a little way off, so a land edge wanders like pigment past the line on damp paper. The
// warp is one octave of value noise at its own salts, `reach` tiles at most, `scale` tiles a cycle. Where the warped
// point falls in the river there is no land to read, so the place keeps its own.
vec3 terrain_bleed_land(vec2 tile, vec3 plain, float reach, float scale) {
    vec2 p = tile / max(scale, 1e-3);
    vec2 warp = (vec2(at_value_noise(p, 81u), at_value_noise(p + 37.0, 82u)) * 2.0 - 1.0) * reach;
    vec3 land = terrain_weights(tile + warp).rgb;
    return (land.x + land.y + land.z) > 1e-3 ? land : plain;
}
