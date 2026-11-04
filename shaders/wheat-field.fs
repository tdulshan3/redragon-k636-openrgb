// Wheat Field - OpenRGB Effects "Shaders" effect, laid out for the "dexmap"
// Visual Map (keyboard bottom-left, mouse beside it, RAM sticks and the D_LED
// ring to the upper right).
//
// Golden hour over a field of wheat. The keyboard is the field: gusts of wind
// roll across it in soft, curving waves, the bent stalks catching the low sun
// as each gust passes, hazy and bright far away, deeper gold close to you. The
// RAM sticks are the sky, orange near the horizon fading to violet above, with
// thin clouds drifting by, and the D_LED ring is the glow around the sun.
//
// Apply it to the "dexmap" virtual controller only, not to the member devices.
// GLSL 1.10. One second of iTime is one real second at the default Speed (1000).

// ---- your Visual Map layout (grid units from the dexmap file, y grows down) --
#define MAP_SIZE   vec2(64.0, 64.0)
#define KB_MIN     vec2( 0.2, 43.2)     // keyboard top-left corner
#define KB_MAX     vec2(38.5, 56.8)     // keyboard bottom-right corner
#define RAM_TOP    9.8
#define RAM_BOT    29.4
#define RING_POS   vec2(56.7, 19.3)     // centre of the D_LED ring = the sun
#define RING_R     6.4
#define FIELD_Y    38.0                 // everything below this is field

// ---- tuning ------------------------------------------------------------------
#define WIND       1.0                  // >1 = faster, more frequent gusts
#define GUSTS      1.0                  // how strongly gusts brighten the field
#define BRIGHTNESS 1.0
// ------------------------------------------------------------------------------

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

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = fragCoord.xy / iResolution.xy;
    vec2 p = vec2(uv.x, 1.0 - uv.y) * MAP_SIZE;      // Visual Map grid coordinates
    float t = iTime * WIND;

    vec3 col;
    if (p.y < FIELD_Y)
    {
        // ---- sunset sky: orange at the bottom of the RAM sticks, violet at the top
        float h = clamp((RAM_BOT - p.y) / (RAM_BOT - RAM_TOP), 0.0, 1.0);
        col = mix(vec3(0.85, 0.40, 0.10), vec3(0.55, 0.20, 0.25), smoothstep(0.0, 0.55, h));
        col = mix(col, vec3(0.16, 0.09, 0.30), smoothstep(0.45, 1.0, h));
        float cloud = noise(vec2(p.x * 0.10 - iTime * 0.05, p.y * 0.30));
        col = mix(col, col * vec3(0.7, 0.55, 0.75) + vec3(0.06, 0.03, 0.05), smoothstep(0.55, 0.8, cloud) * 0.7);

        // the sun's glow on the D_LED ring
        float sr = length(p - RING_POS);
        float sun = exp(-max(sr - 2.0, 0.0) / 4.5);
        col = mix(col, vec3(1.00, 0.62, 0.20), 0.85 * sun);
        col *= 0.9 + 0.1 * sin(iTime * 0.25 + 1.0);
    }
    else
    {
        // ---- the field: hazy gold far away, deep gold near, gusts rolling through
        float near = clamp((p.y - KB_MIN.y) / (KB_MAX.y - KB_MIN.y), 0.0, 1.0);
        vec3 far = vec3(0.34, 0.20, 0.06);
        vec3 deep = vec3(0.19, 0.10, 0.025);
        col = mix(far, deep, near);

        // gusts: broad curving bands travelling left to right
        vec2 g = vec2(p.x * 0.07 - t * 0.33, p.y * 0.12 + 0.9 * sin(p.x * 0.05 + t * 0.07));
        float gust = noise(g) * 0.7 + noise(g * 2.3 + 5.0) * 0.3;
        gust = smoothstep(0.50, 0.78, gust);
        float ripple = 0.5 + 0.5 * sin(p.x * 0.9 - t * 2.6 + p.y * 0.4);    // stalks swaying inside a gust
        col *= 1.0 + GUSTS * gust * (1.2 + 0.4 * ripple);
        col = mix(col, vec3(0.95, 0.80, 0.50) * 0.75, GUSTS * gust * 0.28);
        col *= 0.92 + 0.12 * noise(vec2(p.x * 0.6, p.y * 1.5));              // stalk texture
    }

    fragColor = vec4(clamp(col * 0.85 * BRIGHTNESS, 0.0, 1.0), 1.0);
}
