// The title's watercolour, pass 2 of 2 — THE PAINT (A233, `specs/title-snapshot.md §5b`; Joe: "lean into the watercolor style
// even more - get rid of the white paper surrounding and have the game map fully go to the edges"). On the title's backdrop,
// every frame, over the washes pass 1 drew once.
//
// In order: each wash's dried rim and the pigment fading in from it (read off the washes); the marks that do not depend on
// the picture — glazes, drift, mottling, strokes, backruns, dry-brush skips, splatters — baked as one affine map
// (col * M + paper * b, `watercolour-wash.png`); a touch more colour and the paper's light; granulation in the tooth; the
// game's own ink lines back on top; the paper's relief.
//
// `strength` 1 is (e), 0 is the plain picture: Continue runs it down to 0 and the live map takes over on that frame (§6a).
shader_type canvas_item;

uniform sampler2D picture : filter_linear_mipmap, repeat_disable;
uniform sampler2D washes : filter_linear, repeat_disable;
uniform sampler2D marks : filter_linear, repeat_disable;
uniform sampler2D finish : filter_linear, repeat_disable;
uniform sampler2D paper : filter_linear, repeat_enable;
uniform float strength : hint_range(0.0, 1.5) = 1.0;

const vec3 LUM = vec3(0.299, 0.587, 0.114);
const vec3 PAPER = vec3(0.968, 0.950, 0.912);
const float M_SCALE = 1.3;

float rim_at(vec2 uv, vec2 ps) {
    float lx = dot(texture(washes, uv + vec2(1.5, 0.0) * ps).rgb - texture(washes, uv - vec2(1.5, 0.0) * ps).rgb, LUM);
    float ly = dot(texture(washes, uv + vec2(0.0, 1.5) * ps).rgb - texture(washes, uv - vec2(0.0, 1.5) * ps).rgb, LUM);
    return clamp(length(vec2(lx, ly)) / 3.0 * 9.0, 0.0, 1.0);
}

void fragment() {
    float k = strength;
    vec2 size = vec2(textureSize(picture, 0));
    vec2 ps = 1.0 / size;
    vec3 pic = texture(picture, UV).rgb;
    float f = texture(finish, UV).r;
    vec3 tex = texture(paper, UV * size / 512.0).rgb;   // tooth, fine tooth, relief
    vec3 sheet = PAPER * (0.97 + 0.04 * tex.r);

    // the washes, their dried rims, a halo of pigment fading in from them
    vec3 col = mix(pic, texture(washes, UV).rgb, clamp(k, 0.0, 1.0));
    float rim = rim_at(UV, ps);
    float reach = 6.0 + 3.0 * (1.0 - f);
    float halo = 0.0;
    for (int i = 0; i < 8; i++) {
        float a = float(i) * 0.785398;
        halo += rim_at(UV + vec2(cos(a), sin(a)) * reach * ps, ps);
    }
    halo = clamp(halo / 8.0 * 2.5, 0.0, 1.0);
    col *= 1.0 - (0.30 * rim + 0.16 * halo) * k;

    // the baked marks
    vec4 wm = texture(marks, UV);
    col = col * mix(vec3(1.0), wm.rgb * M_SCALE, k) + sheet * wm.a * k;

    // transparent pigment: a touch more colour, the paper's light through it
    float l = dot(col, LUM);
    col = mix(col, vec3(l) + (col - vec3(l)) * 1.10, k);
    col = mix(col, col * 0.95 + sheet * 0.05, k);

    // granulation: pigment settles in the tooth, more in the heavier washes
    float pig = clamp(1.0 - dot(col, LUM), 0.0, 1.0);
    col *= 1.0 - (0.07 * (tex.r - 0.5) * 2.0 + 0.03 * (tex.g - 0.5) * 2.0) * k * (0.3 + pig);

    // the game's own ink lines, lifted off the picture, back over the washes
    float l0 = dot(pic, LUM);
    float ink = clamp((dot(textureLod(picture, UV, 1.8).rgb, LUM) - l0 - 0.05) * 5.0, 0.0, 1.0) * (0.55 + 0.35 * f);
    col *= 1.0 - 0.75 * ink * k;

    // the paper's relief, lit from the upper left
    col *= 1.0 + (tex.b - 0.5) * 0.04 * k;

    COLOR = vec4(col, 1.0);
}
