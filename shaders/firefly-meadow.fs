// Firefly Meadow - OpenRGB Effects "Shaders" effect, laid out for the "dexmap"
// Visual Map (keyboard bottom-left, mouse beside it, RAM inside the D_LED ring).
//
// Night. Your keyboard is a meadow and the D_LED ring is the moon. Fireflies
// drift over the keys and glow on and off, each at its own pace, so a few are
// always lit somewhere. The far rows are smaller and dimmer, like a field
// running away from you. Wind moves through the grass in soft waves, thin
// clouds slide across the moon, and its halo fades across the RAM sticks
// beside it. A couple of fireflies wander up there too. About every two minutes the fireflies flash together in a slow
// wave from one end of the keyboard to the other.
//
// Apply it to the "dexmap" virtual controller only, not to the member devices.
// GLSL 1.10. One second of iTime is one real second at the default Speed (1000).

// ---- your Visual Map layout (grid units from the dexmap file, y grows down) --
#define MAP_SIZE   vec2(64.0, 64.0)
#define KB_MIN     vec2( 0.6, 45.4)     // keyboard top-left corner
#define KB_MAX     vec2(38.9, 58.9)     // keyboard bottom-right corner
#define MOUSE_POS  vec2(43.5, 49.8)
#define RING_POS   vec2(56.8, 19.0)     // centre of the D_LED ring = centre of the moon
#define RAM_POS    vec2(45.2, 20.8)     // middle of the two RAM sticks
#define MOON_R     6.2                  // moon radius (the ring LEDs sit just inside its rim)

// ---- tuning ------------------------------------------------------------------
#define BLINK_SPEED 1.0                 // >1 blinks more often
#define WANDER      1.0                 // how far fireflies drift from home
#define SYNC_EVERY  120.0               // seconds between group flashes (large = rare)
#define WIND        1.0                 // wind waves in the grass (0 = still)
#define CLOUDS      1.0                 // clouds over the moon (0 = clear sky)
#define BRIGHTNESS  1.0
// ------------------------------------------------------------------------------

float hash12(vec2 p)
{
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}

vec2 hash21(float p)
{
    vec3 p3 = fract(vec3(p) * vec3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.xx + p3.yz) * p3.zy);
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
    for (int i = 0; i < 4; i++)
    {
        v += a * noise(p);
        p = p * 2.03 + 17.0;
        a *= 0.5;
    }
    return v;
}

// Soft flash: quick rise, short hold, slow fade. tau = seconds since it began.
float flash(float tau)
{
    return smoothstep(0.0, 0.35, tau) * (1.0 - smoothstep(0.55, 2.2, tau));
}

vec3 fireflies(vec2 p, float t)
{
    vec3 acc = vec3(0.0);
    float cyc = mod(t, SYNC_EVERY) - (SYNC_EVERY - 11.0);   // last 11 s of each cycle
    for (int i = 0; i < 16; i++)
    {
        float fi = float(i);
        vec2 h  = hash21(fi * 7.31 + 1.7);
        vec2 h2 = hash21(fi * 3.17 + 9.1);

        // 13 spread evenly along the keyboard, one by the mouse, two up by the moon
        vec2 home = vec2(mix(KB_MIN.x + 2.0, KB_MAX.x - 2.0, (fi + 0.5) / 13.0 + (h.x - 0.5) * 0.05),
                         mix(KB_MIN.y + 1.0, KB_MAX.y - 1.0, h.y));
        if (i == 13) home = MOUSE_POS + vec2(-3.0, 0.5);
        if (i >= 14) home = RAM_POS + (h - 0.5) * vec2(6.0, 16.0);

        vec2 pos = home + WANDER * vec2(
            4.5 * sin(t * (0.10 + 0.07 * h2.x) + fi * 1.3) + 2.0 * sin(t * (0.21 + 0.09 * h.y) + fi * 4.1),
            1.5 * sin(t * (0.12 + 0.06 * h2.y) + fi * 2.7) + 0.8 * sin(t * (0.29 + 0.10 * h.x) + fi));

        float per = mix(4.5, 8.5, h2.x) / BLINK_SPEED;
        float f = flash(mod(t + h2.y * per, per));

        if (cyc > 0.0)    // group flash: a slow wave sweeps left to right, twice
        {
            float local = cyc - 1.5 - (pos.x - KB_MIN.x) * 0.12;
            float g = max(flash(local), 0.7 * flash(local - 3.4));
            f = mix(f, g, smoothstep(0.0, 1.5, cyc) * (1.0 - smoothstep(9.5, 11.0, cyc)));
        }

        // far rows (top of the keyboard) are smaller and dimmer
        float near = clamp((pos.y - KB_MIN.y) / (KB_MAX.y - KB_MIN.y), 0.0, 1.0);
        float size = mix(0.75, 1.2, near);
        float bright = mix(0.65, 1.0, near);
        if (i >= 14) { size = 0.9; bright = 0.8; }

        vec2 d = p - pos;
        float d2 = dot(d, d);
        float core = exp(-d2 / (1.5 * size * size));
        float glow = exp(-d2 / (7.0 * size * size));
        acc += f * bright * (core * vec3(0.80, 1.00, 0.30) + glow * vec3(0.18, 0.28, 0.04));
    }
    return acc;
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = fragCoord.xy / iResolution.xy;
    vec2 p = vec2(uv.x, 1.0 - uv.y) * MAP_SIZE;      // Visual Map grid coordinates
    float t = iTime;

    // ---- meadow: bluish far away, greener close to you, wind waves rolling through
    float near = clamp((p.y - KB_MIN.y) / (KB_MAX.y - KB_MIN.y), 0.0, 1.0);
    vec3 meadow = mix(vec3(0.030, 0.065, 0.115), vec3(0.020, 0.085, 0.050), near);
    float wind = noise(vec2(p.x * 0.11 - t * 0.32, p.y * 0.22 + t * 0.04));
    meadow += WIND * smoothstep(0.45, 0.85, wind) * vec3(0.012, 0.040, 0.030);

    // ---- night sky with the moon on the RAM and its halo on the D_LED ring
    vec2 mq = p - RING_POS;
    float mr = length(mq);
    float cl = fbm(vec2(p.x * 0.085 + t * 0.040, p.y * 0.15 - t * 0.006));
    float cloud = CLOUDS * smoothstep(0.42, 0.72, cl);
    float maria = 0.6 * noise(mq * 0.22 + 3.0) + 0.4 * noise(mq * 0.5 + 9.0);
    vec3 moon = vec3(0.37, 0.39, 0.45) * (0.80 + 0.28 * smoothstep(0.35, 0.70, maria));
    moon *= (1.0 - 0.25 * pow(clamp(mr / MOON_R, 0.0, 1.0), 3.0)) * (1.0 - 0.62 * cloud);
    float halo = exp(-max(mr - MOON_R, 0.0) / 5.0);
    vec3 sky = vec3(0.012, 0.020, 0.070) + vec3(0.10, 0.12, 0.25) * halo * (1.0 - 0.55 * cloud);
    sky += cloud * halo * vec3(0.035, 0.040, 0.060);                 // moonlit cloud edges
    sky = mix(sky, moon, 1.0 - smoothstep(MOON_R - 0.6, MOON_R + 0.6, mr));

    float ground = smoothstep(KB_MIN.y - 4.0, KB_MIN.y - 1.0, p.y);
    vec3 col = mix(sky, meadow, ground);

    col += fireflies(p, t);
    col = 1.0 - exp(-col * 1.15);                                    // soft highlight roll-off

    fragColor = vec4(clamp(col * BRIGHTNESS, 0.0, 1.0), 1.0);
}
