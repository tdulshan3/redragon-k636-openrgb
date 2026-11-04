// Snowfall - OpenRGB Effects "Shaders" effect, laid out for the "dexmap"
// Visual Map (keyboard bottom-left, mouse beside it, RAM sticks and the D_LED
// ring to the upper right).
//
// A quiet snowy night. Flakes of three sizes drift down past the RAM sticks,
// the D_LED ring and over the keyboard, swaying, now and then pushed sideways
// by a slow gust. Snow piles up on the keyboard from the bottom row over a few
// minutes, the tops of the RAM sticks and the mouse slowly turn white, then it
// all melts away and the next snowfall starts.
//
// Apply it to the "dexmap" virtual controller only, not to the member devices.
// GLSL 1.10. One second of iTime is one real second at the default Speed (1000).

// ---- your Visual Map layout (grid units from the dexmap file, y grows down) --
#define MAP_SIZE   vec2(64.0, 64.0)
#define KB_MIN     vec2( 0.2, 43.2)     // keyboard top-left corner
#define KB_MAX     vec2(38.5, 56.8)     // keyboard bottom-right corner
#define MOUSE_POS  vec2(43.5, 49.8)
#define RAM_TOP    9.8
#define RAM_LED    3.26                 // height of one RAM LED
#define KEY        2.25                 // keyboard key pitch

// ---- tuning ------------------------------------------------------------------
#define FALL_SPEED 1.0
#define DENSITY    0.32                 // chance of a flake per cell (0..1)
#define PILE_SEC   360.0                // seconds for the snow to pile up
#define MELT_SEC   120.0                // seconds for it to melt again
#define PILE_ROWS  2.6                  // how many keyboard rows it buries
#define BRIGHTNESS 1.0
// ------------------------------------------------------------------------------

vec2 hash22(vec2 p)
{
    vec3 p3 = fract(vec3(p.xyx) * vec3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.xx + p3.yz) * p3.zy);
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

// One layer of falling flakes: cell size cs, fall speed v, flake radius r.
float flakes(vec2 p, float t, float cs, float v, float r, float seed)
{
    float drift = 0.5 * t + 9.0 * sin(t * 0.031 + seed);            // slow gusts push sideways
    vec2 q = vec2(p.x - drift * (0.6 + 0.2 * seed), p.y - t * v * FALL_SPEED);
    vec2 cell = floor(q / cs);
    float acc = 0.0;
    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            vec2 c = cell + vec2(float(x), float(y));
            vec2 h = hash22(c + seed * 17.0);
            if (h.x > DENSITY) continue;
            vec2 h2 = hash22(c * 1.7 + seed * 5.0);
            vec2 fp = (c + 0.15 + 0.7 * h2) * cs;
            fp.x += 1.3 * sin(t * (0.5 + 0.4 * h.y) + h2.x * 6.28);    // sway
            vec2 d = q - fp;
            acc += exp(-dot(d, d) / (r * r));
        }
    }
    return acc;
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = fragCoord.xy / iResolution.xy;
    vec2 p = vec2(uv.x, 1.0 - uv.y) * MAP_SIZE;      // Visual Map grid coordinates
    float t = iTime;

    // ---- how deep the snow is right now (0..1)
    float cyc = mod(t, PILE_SEC + MELT_SEC);
    float depth = cyc < PILE_SEC ? smoothstep(0.0, PILE_SEC, cyc) : 1.0 - smoothstep(PILE_SEC, PILE_SEC + MELT_SEC, cyc);

    // ---- night: deep blue sky, the ground a little lighter
    vec3 col = mix(vec3(0.012, 0.022, 0.065), vec3(0.020, 0.032, 0.075), smoothstep(30.0, 50.0, p.y));

    // ---- snow lying on the keyboard, a lumpy surface rising from the bottom row
    float surf = KB_MAX.y - depth * PILE_ROWS * KEY
               - min(depth * 3.0, 1.0) * (0.9 * noise(vec2(p.x * 0.25, 1.0)) + 0.4 * sin(p.x * 0.6));
    float lying = smoothstep(surf - 0.6, surf + 0.6, p.y) * step(KB_MIN.y - 1.0, p.y) * step(p.x, KB_MAX.x + 1.0);
    vec3 snowCol = vec3(0.26, 0.30, 0.38) * (0.85 + 0.25 * noise(p * 0.7));
    col = mix(col, snowCol, lying * smoothstep(0.0, 0.05, depth));

    // ---- caps of snow on the RAM sticks' top LED and on the mouse
    float ramCap = (1.0 - smoothstep(RAM_TOP + RAM_LED - 0.5, RAM_TOP + RAM_LED + 0.5, p.y)) * step(RAM_TOP - 1.0, p.y) * step(40.0, p.x);
    float mouseCap = exp(-dot(p - MOUSE_POS, p - MOUSE_POS) / 12.0);
    col = mix(col, snowCol, clamp(max(ramCap, mouseCap) * depth * 0.9, 0.0, 1.0));

    // ---- falling flakes, far layer first
    float f = 0.35 * flakes(p, t, 6.0, 1.3, 0.75, 1.0)
            + 0.65 * flakes(p, t, 8.0, 1.8, 0.95, 2.0)
            + 1.00 * flakes(p, t, 10.0, 2.4, 1.15, 3.0);
    col += clamp(f, 0.0, 1.2) * vec3(0.62, 0.68, 0.80);

    fragColor = vec4(clamp(col * BRIGHTNESS, 0.0, 1.0), 1.0);
}
