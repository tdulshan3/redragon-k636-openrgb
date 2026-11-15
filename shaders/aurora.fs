// Aurora - OpenRGB Effects "Shaders" effect, laid out for the "dexmap"
// Visual Map (keyboard bottom-left, mouse beside it, RAM inside the D_LED ring).
//
// The RAM sticks and the D_LED ring are the northern sky. Curtains of aurora
// fold and drift across it, green at the hem and violet higher up, with slow
// rays shimmering through them and the occasional quiet surge. The keyboard is
// a frozen lake that mirrors the sky in soft, rippled reflections, with a snowy
// bank along the bottom row that picks up the aurora's glow.
//
// Apply it to the "dexmap" virtual controller only, not to the member devices.
// GLSL 1.10. One second of iTime is one real second at the default Speed (1000).

// ---- your Visual Map layout (grid units from the dexmap file, y grows down) --
#define MAP_SIZE   vec2(64.0, 64.0)
#define KB_MIN     vec2( 0.6, 45.4)     // keyboard top-left corner
#define KB_MAX     vec2(38.9, 58.9)     // keyboard bottom-right corner
#define HORIZON_Y  40.0                 // horizon line, just above the keyboard

// ---- tuning ------------------------------------------------------------------
#define AURORA_SPEED 1.0
#define INTENSITY    1.0                // aurora strength
#define REFLECTION   0.9                // how strongly the lake mirrors it
#define BRIGHTNESS   1.0
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

float fbm(vec2 p)
{
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 3; i++)
    {
        v += a * noise(p);
        p = p * 2.07 + 13.0;
        a *= 0.5;
    }
    return v;
}

// Aurora light at horizontal position x and height h above the horizon.
vec3 aurora(float x, float h, float t)
{
    float ts = t * AURORA_SPEED;
    vec3 acc = vec3(0.0);
    for (int i = 0; i < 2; i++)
    {
        float fi = float(i);
        float xs = x - ts * (0.9 + 0.5 * fi);                  // folds drift sideways
        float hem = 15.0 + 8.0 * fi + 12.0 * (fbm(vec2(xs * 0.045 + fi * 7.0, ts * 0.05 + fi * 3.0)) - 0.5);
        float d = h - hem;                                     // + above the hem
        float edge = smoothstep(-1.8, 0.6, d);
        float fade = exp(-max(d, 0.0) / (7.0 + 3.0 * fi));
        float rays = 0.45 + 0.55 * noise(vec2(xs * 0.5 + fi * 11.0, ts * 0.12));
        float surge = 0.45 + 0.55 * smoothstep(0.3, 0.75, noise(vec2(ts * 0.04 + fi * 5.0, xs * 0.03)));
        vec3 c = mix(vec3(0.10, 0.85, 0.40), vec3(0.05, 0.55, 0.55), smoothstep(0.0, 6.0, d));
        c = mix(c, vec3(0.42, 0.18, 0.66), smoothstep(5.0, 16.0, d));
        acc += c * edge * fade * rays * surge * (1.0 - 0.4 * fi);
    }
    return acc * INTENSITY * 0.85;
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = fragCoord.xy / iResolution.xy;
    vec2 p = vec2(uv.x, 1.0 - uv.y) * MAP_SIZE;      // Visual Map grid coordinates
    float t = iTime;

    vec3 col;
    if (p.y < HORIZON_Y)
    {
        // ---- sky
        float h = HORIZON_Y - p.y;
        col = mix(vec3(0.012, 0.03, 0.06), vec3(0.006, 0.012, 0.045), smoothstep(0.0, 30.0, h));
        col += aurora(p.x, h, t);
    }
    else
    {
        // ---- frozen lake: rippled mirror image of the sky
        float depth = p.y - HORIZON_Y;
        float rx = p.x + 0.9 * sin(p.y * 1.1 + t * 0.6) + 0.5 * sin(p.y * 2.3 - t * 0.4);
        float h = depth * 2.2 + 6.0 + 0.8 * sin(p.x * 0.7 + t * 0.5);
        col = vec3(0.012, 0.03, 0.07) + REFLECTION * aurora(rx, h, t) * vec3(0.8, 0.95, 1.1);

        // ---- snow bank along the bottom row, lit by the aurora overhead
        float snow = smoothstep(KB_MAX.y - 2.6, KB_MAX.y - 1.6, p.y + 0.6 * sin(p.x * 0.5));
        vec3 glow = aurora(p.x, 18.0, t);
        col = mix(col, vec3(0.10, 0.12, 0.18) + 0.35 * glow, snow);
    }

    fragColor = vec4(clamp(col * BRIGHTNESS, 0.0, 1.0), 1.0);
}
