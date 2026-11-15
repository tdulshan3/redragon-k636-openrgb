// Embers - OpenRGB Effects "Shaders" effect, laid out for the "dexmap"
// Visual Map (keyboard bottom-left, mouse beside it, RAM sticks and the D_LED
// ring to the upper right).
//
// The last hour of a fire. The keyboard is a bed of coals: patches glow and
// fade at their own slow pace like breathing, with brighter veins between
// them. Now and then a spark lifts off and floats away. The RAM sticks are the
// chimney, warm at the bottom, with sparks drifting up them, and the D_LED
// ring is a slow swirl of warm air. The mouse is one last ember.
//
// Apply it to the "dexmap" virtual controller only, not to the member devices.
// GLSL 1.10. One second of iTime is one real second at the default Speed (1000).

// ---- your Visual Map layout (grid units from the dexmap file, y grows down) --
#define MAP_SIZE   vec2(64.0, 64.0)
#define KB_MIN     vec2( 0.6, 45.4)     // keyboard top-left corner
#define KB_MAX     vec2(38.9, 58.9)     // keyboard bottom-right corner
#define MOUSE_POS  vec2(43.5, 49.8)
#define RAM_L_X    43.1                 // centre line of the left RAM stick
#define RAM_R_X    47.3                 // centre line of the right RAM stick
#define RAM_TOP    8.8
#define RAM_BOT    32.9
#define RING_POS   vec2(56.8, 19.0)     // centre of the D_LED ring
#define RING_R     5.6

// ---- tuning ------------------------------------------------------------------
#define HEAT       1.0                  // overall glow of the coals
#define BREATH     1.0                  // >1 = coals breathe faster
#define SPARKS     1.0                  // 0 = no sparks
#define BRIGHTNESS 1.0
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

// Glow colour of an ember at heat h (0 dark .. 1 hottest).
vec3 emberColour(float h)
{
    vec3 c = mix(vec3(0.020, 0.004, 0.002), vec3(0.30, 0.025, 0.005), smoothstep(0.0, 0.3, h));
    c = mix(c, vec3(0.75, 0.13, 0.015), smoothstep(0.25, 0.6, h));
    c = mix(c, vec3(1.00, 0.40, 0.06), smoothstep(0.55, 0.85, h));
    return mix(c, vec3(1.00, 0.62, 0.20), smoothstep(0.85, 1.0, h));
}

// A spark rising from `base`, slot k: returns brightness at p.
float spark(vec2 p, float t, float k, vec2 base, vec2 spread, float rise, float per)
{
    float tk = t / per + hash11(k * 3.3);
    float id = floor(tk);
    float age = fract(tk) * per;
    if (hash11(id * 1.7 + k * 9.1) < 0.35) return 0.0;               // some slots rest
    vec2 h = vec2(hash11(id * 2.3 + k), hash11(id * 4.1 + k * 2.0));
    vec2 sp = base + (h - 0.5) * spread;
    sp.y -= age * rise;
    sp.x += 1.2 * sin(age * 1.7 + h.x * 6.28);
    float life = smoothstep(0.0, 0.3, age) * (1.0 - smoothstep(1.2, 2.6, age));
    vec2 d = p - sp;
    return life * exp(-dot(d, d) / 1.1);
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = fragCoord.xy / iResolution.xy;
    vec2 p = vec2(uv.x, 1.0 - uv.y) * MAP_SIZE;      // Visual Map grid coordinates
    float t = iTime * BREATH;

    vec3 col = vec3(0.012, 0.004, 0.003);

    // ---- the coal bed on the keyboard
    if (p.y > KB_MIN.y - 2.0 && p.x < KB_MAX.x + 2.0)
    {
        float lumps = noise(p * 0.22 + 4.0) * 0.65 + noise(p * 0.5 + 9.0) * 0.35;
        float pace  = 0.35 + 0.5 * noise(p * 0.08 + 2.0);                 // each patch has its own pace
        float breath = 0.5 + 0.5 * sin(t * pace + 6.28 * noise(p * 0.11 + 7.0));
        float drift = noise(p * 0.15 + vec2(t * 0.020, -t * 0.013));
        float veins = 1.0 - abs(2.0 * noise(p * 0.18 + vec2(-t * 0.01, 3.0)) - 1.0);
        float h = 0.55 * lumps + 0.30 * drift + 0.25 * smoothstep(0.75, 1.0, veins);
        h = smoothstep(0.42, 0.95, h) * (0.50 + 0.50 * breath) * HEAT * 0.9;
        col = emberColour(h);
    }

    // ---- the mouse: one ember breathing on its own
    float mb = 0.5 + 0.5 * sin(t * 0.45 + 1.0);
    col += exp(-dot(p - MOUSE_POS, p - MOUSE_POS) / 9.0) * emberColour(0.45 + 0.35 * mb);

    // ---- chimney: the RAM sticks, warm at the bottom, sparks drifting up
    float dx = min(abs(p.x - RAM_L_X), abs(p.x - RAM_R_X));
    if (dx < 2.2 && p.y > RAM_TOP - 1.5 && p.y < RAM_BOT + 1.5)
    {
        float warm = exp(-(RAM_BOT - p.y) / 6.0) * (0.85 + 0.15 * sin(t * 0.4));
        col = emberColour(0.55 * warm) + vec3(0.02, 0.006, 0.004);
    }

    // ---- the D_LED ring: warm air turning slowly
    vec2 d = p - RING_POS;
    float onRing = 1.0 - smoothstep(1.2, 2.5, abs(length(d) - RING_R));
    float swirl = 0.5 + 0.5 * cos(atan(d.y, d.x + 1e-4) - t * 0.3);
    col = mix(col, emberColour(0.12 + 0.33 * swirl * swirl), onRing);

    // ---- sparks: off the coals, and up the chimney
    float s = 0.0;
    for (int k = 0; k < 5; k++)
        s += spark(p, t, float(k), vec2(0.5 * (KB_MIN.x + KB_MAX.x), KB_MAX.y - 4.0), vec2(KB_MAX.x - KB_MIN.x - 4.0, 4.0), 3.0, 3.6);
    for (int k = 5; k < 8; k++)
        s += spark(p, t, float(k), vec2(0.5 * (RAM_L_X + RAM_R_X), RAM_BOT), vec2(RAM_R_X - RAM_L_X, 0.0), 6.0, 4.4);
    col += SPARKS * s * vec3(1.00, 0.55, 0.15);

    fragColor = vec4(clamp(col * BRIGHTNESS, 0.0, 1.0), 1.0);
}
