// Lava Lamp - OpenRGB Effects "Shaders" effect, laid out for the "dexmap"
// Visual Map (keyboard bottom-left, mouse beside it, RAM inside the D_LED ring).
//
// Your two RAM sticks are two tall lava lamps, the D_LED ring is a round lamp
// around them, and the keyboard is a wide one. Warm wax melts off a pool along
// the bottom row, rises slowly through dark violet liquid, merges with other
// blobs, lingers at the top and sinks back. Nothing blinks; everything moves at
// lava-lamp speed.
//
// Apply it to the "dexmap" virtual controller only, not to the member devices.
// GLSL 1.10. One second of iTime is one real second at the default Speed (1000).

// ---- your Visual Map layout (grid units from the dexmap file, y grows down) --
#define MAP_SIZE   vec2(64.0, 64.0)
#define KB_MIN     vec2( 0.2, 43.2)     // keyboard top-left corner
#define KB_MAX     vec2(38.5, 56.8)     // keyboard bottom-right corner
#define MOUSE_POS  vec2(43.5, 49.8)
#define RAM_L_X    43.7                 // centre line of the left RAM stick
#define RAM_R_X    47.3                 // centre line of the right RAM stick
#define RAM_TOP    9.8
#define RAM_BOT    29.4
#define RING_POS   vec2(56.7, 19.3)     // centre of the D_LED ring
#define RING_R     6.4                  // radius of the D_LED ring

// ---- tuning ------------------------------------------------------------------
#define PALETTE    0                    // 0 amber in violet, 1 rose in deep blue, 2 lime in teal
#define LAVA_SPEED 1.0                  // >1 = faster blobs
#define BRIGHTNESS 1.0
// ------------------------------------------------------------------------------

float hash11(float p)
{
    p = fract(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return fract(p);
}

// Height of a blob through its cycle: slow rise, linger at the top, slow sink,
// rest in the pool. 0 = bottom, 1 = top.
float lavaHeight(float t, float period, float seed)
{
    float c = fract(t * LAVA_SPEED / period + seed);
    return smoothstep(0.05, 0.47, c) * (1.0 - smoothstep(0.58, 0.95, c));
}

// Soft blob: wax inside about 0.8 r; two blobs closer than ~2 r melt together.
float blob(vec2 p, vec2 c, float r, float stretch)
{
    vec2 d = p - c;
    d.y /= stretch;
    return 2.0 * exp(-dot(d, d) / (r * r));
}

void palette(out vec3 liquid, out vec3 hot, out vec3 cool)
{
#if PALETTE == 1
    liquid = vec3(0.02, 0.04, 0.16); hot = vec3(1.00, 0.55, 0.62); cool = vec3(0.85, 0.20, 0.45);
#elif PALETTE == 2
    liquid = vec3(0.01, 0.10, 0.11); hot = vec3(0.80, 1.00, 0.30); cool = vec3(0.35, 0.80, 0.25);
#else
    liquid = vec3(0.085, 0.020, 0.13); hot = vec3(0.95, 0.55, 0.12); cool = vec3(0.85, 0.22, 0.10);
#endif
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = fragCoord.xy / iResolution.xy;
    vec2 p = vec2(uv.x, 1.0 - uv.y) * MAP_SIZE;      // Visual Map grid coordinates
    float t = iTime;

    float f = 0.0;        // wax field, wax where f > 1
    float h01 = 0.0;      // 0 at the bottom of the lamp this pixel belongs to, 1 at the top

    // ---- keyboard lamp: 7 blobs spread along it, pool along the bottom row
    if (p.y > KB_MIN.y - 6.0 && p.x < KB_MAX.x + 4.0)
    {
        h01 = clamp((KB_MAX.y - p.y) / (KB_MAX.y - KB_MIN.y), 0.0, 1.0);
        for (int i = 0; i < 7; i++)
        {
            float fi = float(i);
            float s  = hash11(fi * 3.7 + 1.0);
            float s2 = hash11(fi * 5.3 + 2.0);
            float per = mix(24.0, 36.0, s);
            float hgt = lavaHeight(t, per, s2);
            float x = mix(KB_MIN.x + 3.0, KB_MAX.x - 3.0, (fi + 0.5) / 7.0) + 3.0 * sin(t * 0.04 * LAVA_SPEED + fi * 2.3);
            float y = mix(KB_MAX.y + 1.0, KB_MIN.y + 1.2, hgt);
            float r = mix(2.2, 2.9, s2) * (1.0 - 0.12 * hgt);      // blobs shrink a little as they cool
            f += blob(p, vec2(x, y), r, 1.0 + 0.3 * sin(3.14159 * hgt));
        }
        float wav = 0.85 + 0.3 * sin(p.x * 0.35 + t * 0.12) * sin(p.x * 0.13 - t * 0.07);
        f += 3.4 * exp(-(KB_MAX.y - p.y) / 1.08) * wav;              // the warm pool on the bottom row
    }

    // ---- the mouse: a tiny lamp with one blob
    {
        float hm = lavaHeight(t, 27.0, 0.3);
        f += blob(p, MOUSE_POS + vec2(0.0, mix(5.0, -4.0, hm)), 2.2, 1.2);
    }

    // ---- RAM sticks: two tall narrow lamps, two blobs each
    if (abs(p.y - 0.5 * (RAM_TOP + RAM_BOT)) < 12.0 && abs(p.x - 0.5 * (RAM_L_X + RAM_R_X)) < 3.8)
    {
        h01 = clamp((RAM_BOT - p.y) / (RAM_BOT - RAM_TOP), 0.0, 1.0);
        for (int i = 0; i < 4; i++)
        {
            float fi = float(i);
            float s  = hash11(fi * 7.1 + 11.0);
            float x  = i < 2 ? RAM_L_X : RAM_R_X;
            float hgt = lavaHeight(t, mix(22.0, 33.0, s), hash11(fi * 2.9 + 4.0));
            float y = mix(RAM_BOT + 1.0, RAM_TOP + 1.6, hgt);
            f += blob(p, vec2(x, y), mix(1.5, 1.8, s), 1.5);
        }
        float dx = min(abs(p.x - RAM_L_X), abs(p.x - RAM_R_X));
        f += 3.4 * exp(-(RAM_BOT - p.y) / 1.6) * (1.0 - smoothstep(1.4, 2.2, dx));   // pools at the bottom
    }

    // ---- D_LED ring: a round lamp, wax climbing up each side of the circle
    float ringD = abs(length(p - RING_POS) - RING_R);
    if (ringD < 3.0)
    {
        h01 = clamp((RING_POS.y + RING_R - p.y) / (2.0 * RING_R), 0.0, 1.0);
        for (int i = 0; i < 2; i++)
        {
            float fi = float(i);
            float side = i == 1 ? -1.0 : 1.0;
            float hgt = lavaHeight(t, 27.0 + 6.0 * fi, 0.43 * fi + 0.1);
            float a = 1.5708 + side * 3.14159 * hgt;                  // bottom -> up one side
            vec2 c = RING_POS + RING_R * vec2(cos(a), sin(a));
            f += blob(p, c, 1.6, 1.0);
        }
        f += 2.2 * exp(-length(p - (RING_POS + vec2(0.0, RING_R))) / 1.4);   // pool at the bottom
    }

    vec3 liquid; vec3 hot; vec3 cool;
    palette(liquid, hot, cool);

    float wax = smoothstep(0.80, 1.20, f);
    vec3 waxCol = mix(hot, cool, smoothstep(0.1, 0.9, h01));
    waxCol *= 0.70 + 0.25 * smoothstep(1.2, 2.2, f);                  // thicker wax glows more
    vec3 liq = liquid * (1.0 + 0.9 * (1.0 - h01));                      // bulb light from below
    liq += 0.10 * hot * smoothstep(0.35, 0.85, f) * (1.0 - wax);        // glow around the wax
    vec3 col = mix(liq, waxCol, wax);

    fragColor = vec4(clamp(col * BRIGHTNESS, 0.0, 1.0), 1.0);
}
