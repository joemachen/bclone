// THE PATHS' FADE (art piece (a), `specs/path-crossfade.md`): when the paths' network changes at a season's turn, the field
// the valley paints from is blended from the old network's to the new one's over a stretch of game time, so a track wears in
// (or grows back) rather than stepping in one frame. Included by valley_terrain.gdshader and valley_fields.gdshader AFTER
// their own `path_map`, so the crop's cut and the painted track blend the same, to the pixel.
//
// The blend is of the field's values — the signed distance to the edge, the half-width, packed — not of two pictures, so a
// new lane widens out from its own line and a fading one narrows back into it; worn to packed, the trodden grass turns to
// bare dirt and the pen comes in. `path_fade_patch` lets the moment vary from place to place (a world-anchored noise), so
// the change soaks in by patches as a wash does, rather than evenly along the whole lane.
uniform sampler2D path_map_from : hint_default_black, filter_linear, repeat_disable;
// 0 the old network's field, 1 the new one's (and then `path_map_from` is not read at all).
uniform float path_fade = 1.0;
// 0 the whole map changes together; toward 1, places start in turn by a noise (each still ends by `path_fade` 1).
uniform float path_fade_patch = 0.0;

// The field at a texel coordinate, `tile` the valley's tile for the noise: the new field, or mid-fade the blend.
vec3 path_field_faded(vec2 at, vec2 tile) {
    vec3 now = textureLod(path_map, at, 0.0).rgb;
    if (path_fade >= 1.0) {
        return now;
    }

    vec3 was = textureLod(path_map_from, at, 0.0).rgb;
    // Past the field's reach in both (most of the valley): no track near, before or after — nothing to blend, no noise.
    if (min(was.r, now.r) >= 0.999) {
        return now;
    }

    float when = path_fade_patch * at_value_noise(tile / 1.6, 139u);
    float t = clamp((path_fade - when) / max(1.0 - path_fade_patch, 1e-3), 0.0, 1.0);
    return mix(was, now, t * t * (3.0 - 2.0 * t));
}
