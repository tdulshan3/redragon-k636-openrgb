// Hourglass - OpenRGB Effects "Shaders" effect, laid out for the "dexmap"
// Visual Map (keyboard bottom-left, mouse beside it, RAM sticks and the D_LED
// ring to the upper right).
//
// A ten-minute sand timer across your desk. The RAM sticks are the upper bulb:
// golden sand sinks slowly out of them, LED by LED. A thin stream pours down
// onto the keyboard, where it builds a soft mound that rises row by row while
// loose grains slide down its sides. The D_LED ring is the dial: one more of
// its LEDs warms up as each slice of time passes. When the sand runs out, the
// glass turns over gently and it starts again.
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
#define RING_R     6.4

// ---- tuning ------------------------------------------------------------------
#define DRAIN_SEC  600.0                // how long the sand takes to run through
#define FLIP_SEC   20.0                 // how long turning the glass over takes
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

    float tau  = mod(t, DRAIN_SEC + FLIP_SEC);
    float u    = clamp(tau / DRAIN_SEC, 0.0, 1.0);                 // 0 full top .. 1 empty top
    float flip = smoothstep(DRAIN_SEC, DRAIN_SEC + FLIP_SEC, tau);  // 0 .. 1 while turning over
    float flowing = (1.0 - smoothstep(0.97, 1.0, u)) * (1.0 - flip);

    vec3 glass = vec3(0.025, 0.028, 0.045);
    vec3 sandA = vec3(0.80, 0.55, 0.20);
    vec3 sandB = vec3(0.62, 0.40, 0.13);
    float grain = noise(p * 1.3);
    vec3 sand = mix(sandB, sandA, grain) * 0.8;
    vec3 col = vec3(0.012, 0.011, 0.018);

    // ---- upper bulb: the RAM sticks, sand level sinking (refills while flipping)
    float dx = min(abs(p.x - RAM_L_X), abs(p.x - RAM_R_X));
    if (dx < 2.0 && p.y > RAM_TOP - 1.0 && p.y < RAM_BOT + 1.0)
    {
        float level = mix(1.0 - u, 1.0, flip);
        float top = RAM_BOT - level * (RAM_BOT - RAM_TOP);
        float s = smoothstep(top - 1.0, top + 1.0, p.y);
        float shimmer = 0.85 + 0.25 * noise(vec2(p.x, p.y * 0.8 - t * 1.5)) * smoothstep(top + 3.0, top, p.y);
        col = mix(glass, sand * shimmer, s);
        col += vec3(0.25, 0.15, 0.05) * flowing * exp(-(RAM_BOT - p.y) / 2.0);     // the neck glowing
    }

    // ---- lower bulb: the keyboard, a mound growing from the bottom row
    if (p.y > KB_MIN.y - 1.0 && p.x < KB_MAX.x + 1.0)
    {
        float cx = 0.5 * (KB_MIN.x + KB_MAX.x);
        float H = KB_MAX.y - KB_MIN.y;
        float peak = KB_MAX.y - (0.15 + 0.95 * u) * H;
        float surf = peak + 0.42 * abs(p.x - cx) + 0.6 * noise(vec2(p.x * 0.4, 3.0));
        float mound = smoothstep(surf - 0.8, surf + 0.8, p.y) * (1.0 - flip);
        col = mix(glass, sand, mound);

        // the falling stream above the peak, and its splash
        float stream = exp(-(p.x - cx) * (p.x - cx) / 0.9) * (1.0 - smoothstep(peak - 0.5, peak + 1.5, p.y));
        float pour = 0.65 + 0.35 * noise(vec2(p.x * 2.0, p.y * 0.9 - t * 6.0));
        col += stream * pour * flowing * vec3(0.95, 0.70, 0.30) * 0.8;
        col += exp(-dot(p - vec2(cx, peak), p - vec2(cx, peak)) / 3.0) * flowing * vec3(0.30, 0.20, 0.08);

        // loose grains sliding down both slopes
        for (int i = 0; i < 6; i++)
        {
            float fi = float(i);
            float side = mod(fi, 2.0) < 0.5 ? -1.0 : 1.0;
            float k = fract(t * 0.09 + fi * 0.37);
            float run = k * 18.0;
            vec2 g = vec2(cx + side * run, peak + 0.42 * run - 0.4);
            float on = flowing * smoothstep(0.0, 0.1, k) * (1.0 - smoothstep(0.75, 1.0, k)) * (1.0 - smoothstep(KB_MAX.y - 2.5, KB_MAX.y - 0.5, g.y));
            col += on * exp(-dot(p - g, p - g) / 1.2) * vec3(0.55, 0.38, 0.12);
        }
    }

    // ---- the mouse catches a little light from the stream
    col += exp(-dot(p - MOUSE_POS, p - MOUSE_POS) / 10.0) * (glass + flowing * vec3(0.10, 0.06, 0.02));

    // ---- dial: the D_LED ring fills clockwise from the top as time passes
    vec2 d = p - RING_POS;
    float onRing = 1.0 - smoothstep(1.2, 2.5, abs(length(d) - RING_R));
    if (onRing > 0.0)
    {
        float a = fract(atan(d.x, -d.y) / 6.2831853 + 1.0);           // 0 at the top, clockwise
        float lit = smoothstep(a - 0.03, a + 0.03, u) * (1.0 - flip);
        float edge = exp(-(a - u) * (a - u) / 0.002) * flowing * (0.6 + 0.4 * sin(t * 1.2));
        col = mix(col, glass + lit * sandA * 0.55 + edge * vec3(0.45, 0.30, 0.10), onRing);
    }

    fragColor = vec4(clamp(col * BRIGHTNESS, 0.0, 1.0), 1.0);
}
