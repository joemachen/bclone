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
