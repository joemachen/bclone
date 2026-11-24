// The title's watercolour, pass 1 of 2 — THE WASHES (A233, `specs/title-snapshot.md §5b`). Drawn ONCE, into a SubViewport the
// picture's size, when the title opens (a Kuwahara is ~50 taps a pixel; once is free, every frame is not).
//
// The picture's texture taken out and its shapes kept: a generalised Kuwahara (eight overlapping sectors of a disc round the
// pixel, each a weighted mean; the flatter a sector, the more it counts), read off the picture's own mips, so the same disc of
// taps covers a wider wash where the map is looser. Round sectors and soft weights, because the square four-quadrant filter
// left blocks at the broad washes' edges (A233). Fine at the village's heart, broad in the outskirts, by the baked finish map —
// the mock's median at three sizes.
shader_type canvas_item;

uniform sampler2D picture : filter_linear_mipmap, repeat_disable;
uniform sampler2D finish : filter_linear, repeat_disable;

const int R = 4;
const int N = 8;

void fragment() {
    float f = texture(finish, UV).r;
    // lod 0.9 at the heart (~7 px washes), 1.8 between (~14 px), 2.8 in the outskirts (~28 px), by eye against the mock
    float lod = 2.8 - 1.0 * smoothstep(0.15, 0.55, f) - 0.9 * smoothstep(0.55, 0.95, f);
    vec2 px = exp2(lod) / vec2(textureSize(picture, 0));
    vec3 m[N];
    vec3 s[N];
    float w[N];
    for (int k = 0; k < N; k++) { m[k] = vec3(0.0); s[k] = vec3(0.0); w[k] = 0.0; }
    for (int j = -R; j <= R; j++) {
        for (int i = -R; i <= R; i++) {
            vec2 d = vec2(float(i), float(j));
            float r2 = dot(d, d) / float(R * R);
            if (r2 > 1.0) { continue; }
            vec3 c = textureLod(picture, UV + d * px, lod).rgb;
            vec3 cc = c * c;
            float g = exp(-2.0 * r2);
            vec2 dir = r2 > 0.0 ? normalize(d) : vec2(0.0);
            for (int k = 0; k < N; k++) {
                float a = float(k) * 0.785398;
                float lobe = r2 > 0.0 ? max(0.0, dot(dir, vec2(cos(a), sin(a)))) : 1.0;
                float wk = g * lobe * lobe;
                m[k] += c * wk; s[k] += cc * wk; w[k] += wk;
            }
        }
    }
    vec3 sum = vec3(0.0);
    float total = 0.0;
    for (int k = 0; k < N; k++) {
        vec3 mean = m[k] / w[k];
        vec3 var = abs(s[k] / w[k] - mean * mean);
        float sigma = sqrt(var.r + var.g + var.b) * 255.0;
        float alpha = 1.0 / (1.0 + pow(sigma, 8.0) * 1e-8);
        sum += mean * alpha; total += alpha;
    }
    COLOR = vec4(sum / total, 1.0);
}
