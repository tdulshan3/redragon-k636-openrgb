// Lighthouse - OpenRGB Effects "Shaders" effect, laid out for the "dexmap"
// Visual Map (keyboard bottom-left, mouse beside it, RAM inside the D_LED ring).
//
// Night on the coast. The RAM sticks are the striped lighthouse tower with the
// lantern room glowing at the top, and the D_LED ring beside it is the turning
// lamp. Every few seconds one of its two beams sweeps across the dark sea
// on your keyboard, lighting up the swells as it passes. Out on the water, the
// mouse is a small boat whose lantern bobs with the waves.
//
// Apply it to the "dexmap" virtual controller only, not to the member devices.
// GLSL 1.10. One second of iTime is one real second at the default Speed (1000).

// ---- your Visual Map layout (grid units from the dexmap file, y grows down) --
#define MAP_SIZE   vec2(64.0, 64.0)
#define MOUSE_POS  vec2(43.5, 49.8)
#define RAM_L_X    43.1                 // centre line of the left RAM stick
#define RAM_R_X    47.3                 // centre line of the right RAM stick
#define RAM_TOP    8.8
#define RAM_LED    4.01                 // height of one RAM LED
#define RING_POS   vec2(56.8, 19.0)     // centre of the D_LED ring = the lamp
#define RING_R     5.6
#define SEA_Y      38.0                 // everything below this is sea

// ---- tuning ------------------------------------------------------------------
#define TURN_SEC   20.0                 // seconds per full turn of the lamp
#define BEAM_WIDTH 0.40                 // beam half-width on the lamp (radians)
#define SEA_BEAM   0.20                 // narrower beam where it crosses the sea
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
    float t = iTime;

    // ---- the two beams, opposite each other
    float theta = 6.2831853 * t / TURN_SEC;
    vec2 d = p - RING_POS;
    float ang = atan(d.y, d.x + 1e-4);
    float a1 = mod(ang - theta + 3.14159265, 6.2831853) - 3.14159265;
    float a2 = mod(ang - theta, 6.2831853) - 3.14159265;
    float bw = BEAM_WIDTH * BEAM_WIDTH;
    float beam = exp(-a1 * a1 / bw) + exp(-a2 * a2 / bw);
    vec3 warm = vec3(1.00, 0.82, 0.52);

    vec3 col = vec3(0.006, 0.012, 0.04);              // night sky

    // ---- lamp on the D_LED ring: glass glows where a beam faces
    float onRing = 1.0 - smoothstep(1.5, 3.5, abs(length(d) - RING_R));
    col += onRing * (vec3(0.04, 0.03, 0.02) + 0.85 * beam * warm);

    // ---- tower on the RAM sticks: red and white bands, lantern on top
    float dx = min(abs(p.x - RAM_L_X), abs(p.x - RAM_R_X));
    if (dx < 2.0 && p.y > RAM_TOP - 1.0 && p.y < RAM_TOP + 6.0 * RAM_LED + 1.0)
    {
        float band = floor((p.y - RAM_TOP) / RAM_LED);
        vec3 tower = mod(band, 2.0) < 0.5 ? vec3(0.20, 0.20, 0.23) : vec3(0.30, 0.04, 0.03);
        tower *= 0.85 + 0.35 * beam;                  // the beam spills onto the tower
        if (band < 0.5) tower = warm * (0.65 + 0.15 * beam);
        col = tower;
    }

    // ---- the sea on the keyboard
    if (p.y > SEA_Y)
    {
        float swell = 0.5 + 0.5 * sin(p.y * 0.9 - t * 0.7 + 1.5 * sin(p.x * 0.12 + t * 0.1));
        vec3 sea = vec3(0.008, 0.028, 0.075) + swell * vec3(0.008, 0.022, 0.045);
        float dist = length(d);
        float reach = 1.0 / (1.0 + (dist / 32.0) * (dist / 32.0));
        float glint = 0.45 + 0.75 * noise(vec2(p.x * 0.55, p.y * 1.1 - t * 0.6));
        float sb = SEA_BEAM * SEA_BEAM;
        float seaBeam = exp(-a1 * a1 / sb) + exp(-a2 * a2 / sb);
        sea += seaBeam * reach * glint * (0.45 + 0.30 * swell) * vec3(1.00, 0.66, 0.28);

        // the boat: a small lantern on the mouse, bobbing
        float boat = exp(-dot(p - MOUSE_POS, p - MOUSE_POS) / 8.0);
        sea += boat * (0.55 + 0.25 * sin(t * 0.85)) * vec3(0.95, 0.55, 0.20);
        col = sea;
    }

    fragColor = vec4(clamp(col * BRIGHTNESS, 0.0, 1.0), 1.0);
}
