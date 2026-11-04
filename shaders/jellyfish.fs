// Jellyfish - OpenRGB Effects "Shaders" effect, laid out for the "dexmap"
// Visual Map (keyboard bottom-left, mouse beside it, RAM inside the D_LED ring).
//
// Deep water. Glowing jellyfish rise slowly through the keyboard, past the RAM
// sticks and through the D_LED ring. Each one squeezes its bell, glows a little brighter, drifts up,
// and relaxes, trailing soft tentacles. Faint sunbeams slant down from the
// surface over the RAM and the D_LED ring.
//
// Apply it to the "dexmap" virtual controller only, not to the member devices.
// GLSL 1.10. One second of iTime is one real second at the default Speed (1000).

// ---- your Visual Map layout (grid units from the dexmap file, y grows down) --
#define MAP_SIZE   vec2(64.0, 64.0)
#define KB_MIN     vec2( 0.2, 43.2)     // keyboard top-left corner
#define KB_MAX     vec2(38.5, 56.8)     // keyboard bottom-right corner
#define RAM_X      45.5                 // between the two RAM sticks
#define RING_X     56.7                 // centre of the D_LED ring

// ---- tuning ------------------------------------------------------------------
#define JELLY_SPEED 1.0                 // >1 = jellies pulse and rise faster
#define SUNBEAMS    1.0                 // light from the surface (0 = off)
#define BRIGHTNESS  1.0
// ------------------------------------------------------------------------------

float hash11(float p)
{
    p = fract(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return fract(p);
}

float hash12(vec2 p)
{
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}

float noise(vec2 p)
{
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash12(i), hash12(i + vec2(1.0, 0.0)), f.x),
               mix(hash12(i + vec2(0.0, 1.0)), hash12(i + vec2(1.0, 1.0)), f.x), f.y);
}

// 0 relaxed, 1 squeezed: quick squeeze, slow relax.
float squeeze(float f)
{
    return smoothstep(0.0, 0.12, f) * (1.0 - smoothstep(0.12, 0.65, f));
}

vec3 jelly(vec2 p, float t, float i)
{
    float s  = hash11(i * 4.1 + 2.0);
    float s2 = hash11(i * 7.7 + 5.0);
    float per = mix(2.8, 3.8, s);
    float ph = t * JELLY_SPEED / per + s2 * 10.0;
    float f  = fract(ph);
    float rise = (floor(ph) + smoothstep(0.05, 0.6, f)) * 1.8;       // each squeeze pushes it up

    // three jellies live in the keyboard water, two up around the RAM
    float y0 = 62.0; float y1 = 35.0;
    float xc = mix(KB_MIN.x + 5.0, KB_MAX.x - 5.0, (i + 0.5) / 3.0);
    float xr = 4.0;
    if (i > 2.5) { y0 = 38.0; y1 = -1.0; xc = i < 3.5 ? RAM_X : RING_X; xr = 2.0; }
    float span = y0 - y1;
    vec2 c = vec2(xc + xr * sin(t * 0.045 + s * 6.28), y0 - mod(rise + s * span, span));

    float sq = squeeze(f);
    vec2 size = vec2(3.0 * (1.0 - 0.18 * sq), 2.1 * (1.0 + 0.14 * sq));
    vec2 q = p - c;
    vec2 e = q / size;
    float ed = length(vec2(e.x, e.y < 0.0 ? e.y : e.y * 3.0));       // dome with a flat underside
    float bell = 0.45 * (1.0 - smoothstep(0.7, 1.05, ed)) + 1.0 * exp(-(ed - 0.88) * (ed - 0.88) / 0.035);
    bell *= 1.0 - smoothstep(0.25, 0.55, e.y);

    // trailing tentacles, swaying
    float ty = q.y - 0.3 * size.y;
    float tx = q.x + 0.5 * sin(ty * 0.9 - t * 1.4 + i * 2.0);
    float ten = 0.0;
    for (int k = -1; k <= 1; k++)
    {
        float kx = float(k) * 1.1 * size.x / 3.0;
        ten += exp(-(tx - kx) * (tx - kx) / 0.25);
    }
    ten *= 0.28 * smoothstep(0.0, 0.8, ty) * exp(-max(ty, 0.0) / 4.0);

    vec3 hue = s2 < 0.34 ? vec3(0.95, 0.38, 0.80) : (s2 < 0.67 ? vec3(0.30, 0.85, 1.00) : vec3(0.62, 0.50, 1.00));
    return hue * (bell * (0.8 + 0.4 * sq) + ten);
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = fragCoord.xy / iResolution.xy;
    vec2 p = vec2(uv.x, 1.0 - uv.y) * MAP_SIZE;      // Visual Map grid coordinates
    float t = iTime;

    // deep at the keyboard, lighter toward the surface above the RAM
    float up = 1.0 - smoothstep(0.0, 56.0, p.y);
    vec3 col = mix(vec3(0.008, 0.028, 0.075), vec3(0.020, 0.075, 0.170), up);

    // slanted sunbeams near the surface
    float beam = noise(vec2(p.x * 0.22 + p.y * 0.10 + t * 0.04, t * 0.02));
    col += SUNBEAMS * smoothstep(0.55, 0.9, beam) * up * up * vec3(0.03, 0.08, 0.10);

    for (int i = 0; i < 5; i++)
        col += jelly(p, t, float(i));

    col = 1.0 - exp(-col * 1.1);
    fragColor = vec4(clamp(col * BRIGHTNESS, 0.0, 1.0), 1.0);
}
