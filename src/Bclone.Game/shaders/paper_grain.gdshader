// Paper grain for THE SCENE (`specs/seasonal-grass.md §5`, D630): a rough multiply over everything drawn under it,
// because grain knocks the contrast down and changes how loud the grass reads (Joe).
//
// Three octaves of value noise in screen pixels — fibres, tooth, and a faint cloud. No image asset. Drawn only when
// amount > 0, so at 0 it is not there at all rather than a multiply by one.
shader_type canvas_item;
render_mode blend_mul;

uniform float amount = 0.15;

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

void fragment() {
    vec2 px = FRAGCOORD.xy;
    float n = 0.5 * grain_noise(px / 1.3) + 0.3 * grain_noise(px / 5.0) + 0.2 * grain_noise(px / 37.0);
    COLOR = vec4(vec3(1.0 - amount * n), 1.0);
}
