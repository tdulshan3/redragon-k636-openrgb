// Shoreline - OpenRGB Effects "Shaders" effect, laid out for the "dexmap"
// Visual Map (keyboard bottom-left, mouse beside it, RAM inside the D_LED ring).
//
// Your PC is out at sea and your keyboard is the beach. Long swells roll in
// past the RAM sticks and the D_LED ring and break on the keyboard: foam
// rushes up over the bottom rows, slides back, and leaves the sand dark and
// wet for a moment before it dries. Every wave has its own size and edge, and
// a big swell you see passing the RAM is the big wave that lands a few
// seconds later.
//
// Apply it to the "dexmap" virtual controller only, not to the member devices.
// GLSL 1.10. One second of iTime is one real second at the default Speed (1000).

// ---- your Visual Map layout (grid units from the dexmap file, y grows down) --
#define MAP_SIZE    vec2(64.0, 64.0)
#define SHORE_Y     51.1      // resting waterline, grid y (keyboard row 3 of 0..5)

// ---- tuning ------------------------------------------------------------------
#define WAVE_SEC    9.0       // seconds between waves
#define SWELL_SPEED 4.5       // swell travel speed toward the beach, grid units/s
#define RUNUP_MIN   2.3       // smallest run-up past the resting line (a key is ~2.25)
#define RUNUP_MAX   6.5       // biggest run-up
#define FOAM        1.0       // foam amount (0 = none)
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

float shoreY(float x)
{
    return SHORE_Y + 0.9 * sin(x * 0.11 + 0.6) + 0.5 * sin(x * 0.27 + 2.1);
}

// 0 -> 1 -> 0 over one wave: quick rush up the sand, slow slide back, rest.
float swash(float ph)
{
    if (ph < 0.2) { float u = 1.0 - ph / 0.2; return 1.0 - u * u; }
    return 1.0 - smoothstep(0.2, 0.82, ph);
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = fragCoord.xy / iResolution.xy;
    vec2 p = vec2(uv.x, 1.0 - uv.y) * MAP_SIZE;      // Visual Map grid coordinates
    float t = iTime;

    float rest = 1.5;
    float ys   = shoreY(p.x);
    float tw   = t - p.x * 0.035;                    // waves land slightly at an angle
    float wv   = tw / WAVE_SEC;
    float k    = floor(wv);
    float ph   = fract(wv);
    float A    = mix(RUNUP_MIN, RUNUP_MAX, hash11(k * 1.618 + 3.0));
    float edge = 0.7 * sin(p.x * 0.42 + hash11(k + 7.0) * 6.2831)
               + 0.4 * sin(p.x * 0.90 + hash11(k + 3.0) * 6.2831);
    float up   = swash(ph);
    float yW   = ys - rest + (A + rest + 0.6 * edge) * up;   // current waterline
    float D    = ys - p.y;                                     // depth, + out at sea
    float dE   = yW - p.y;                                     // distance behind the waterline

    // ---- water colour by depth
    vec3 wc = mix(vec3(0.06, 0.33, 0.37), vec3(0.03, 0.20, 0.33), smoothstep(0.0, 6.0, D));
    wc = mix(wc, vec3(0.025, 0.14, 0.36), smoothstep(6.0, 18.0, D));
    wc = mix(wc, vec3(0.015, 0.065, 0.25), smoothstep(18.0, 40.0, D));

    // ---- swells rolling in from the deep water
    float phi   = wv + (ys - rest - p.y) / (SWELL_SPEED * WAVE_SEC);
    float crest = pow(0.5 + 0.5 * cos(6.2831853 * phi), 4.0);
    float Ac    = mix(RUNUP_MIN, RUNUP_MAX, hash11(floor(phi + 0.5) * 1.618 + 3.0));
    float Arel  = (Ac - RUNUP_MIN) / (RUNUP_MAX - RUNUP_MIN);
    wc += crest * (0.35 + 0.65 * Arel) * vec3(0.05, 0.15, 0.22) * smoothstep(3.0, 9.0, D);

    // ---- breaking: the incoming crest turns to foam in the last few units
    float sharp = pow(0.5 + 0.5 * cos(6.2831853 * phi), 10.0);
    float breakZone = smoothstep(rest - 0.6, rest + 1.2, D) * (1.0 - smoothstep(3.0, 7.5, D));
    float foam = sharp * breakZone * (0.5 + 0.5 * Arel);

    // ---- foam on the leading edge of the swash, thinning as it slides back
    float front = exp(-(dE - 0.6) * (dE - 0.6) / 1.4) * step(-0.8, dE);
    foam += front * smoothstep(0.0, 0.05, ph) * (1.0 - smoothstep(0.08, 0.6, ph))
          * (0.55 + 0.45 * (A - RUNUP_MIN) / (RUNUP_MAX - RUNUP_MIN));

    // ---- lace of old foam drifting in the shallows
    float lace = noise(p * vec2(0.30, 0.55) + vec2(t * 0.06, -t * 0.25));
    foam += 0.30 * smoothstep(0.55, 0.85, lace) * smoothstep(-0.5, 1.0, dE) * (1.0 - smoothstep(2.0, 6.0, dE));

    foam *= 0.55 + 0.6 * noise(p * vec2(0.55, 0.9) + vec2(t * 0.15, -t * 0.35));
    foam = clamp(foam * FOAM, 0.0, 0.85);

    // ---- sand: dark where the last wave reached, drying out behind it
    float reach  = ys - rest + (A + rest + 0.6 * edge) * (ph < 0.2 ? up : 1.0);   // furthest point so far
    float wetNow = (1.0 - smoothstep(reach - 0.3, reach + 0.7, p.y)) * (1.0 - smoothstep(0.25, 1.0, ph));
    float damp   = 1.0 - smoothstep(ys - 0.3, ys + 2.4, p.y);
    float wet    = max(wetNow, damp);
    vec3 sand = mix(vec3(0.40, 0.30, 0.17), vec3(0.17, 0.13, 0.09), wet);
    sand *= 0.9 + 0.18 * noise(p * 0.18);
    sand += vec3(0.03, 0.06, 0.08) * exp(-(dE + 0.6) * (dE + 0.6) / 1.5) * wet;   // wet sheen

    float water = smoothstep(-0.45, 0.45, dE);
    vec3 col = mix(sand, wc, water);
    col = mix(col, vec3(0.70, 0.80, 0.83), foam);

    fragColor = vec4(clamp(col * BRIGHTNESS, 0.0, 1.0), 1.0);
}
