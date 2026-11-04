// Rainy Window - OpenRGB Effects "Shaders" effect, laid out for the "dexmap"
// Visual Map (keyboard bottom-left, mouse beside it, RAM sticks and the D_LED
// ring to the upper right).
//
// Looking out through a rainy window at night. Behind the glass the city is
// out of focus: soft amber and blue lights drift slowly past and fade. On the
// glass, drops gather, then run down in fits and starts, leaving a wet trail
// that fades behind them: across the keyboard rows, all the way down the RAM
// sticks, and through the D_LED ring. Each running drop lights up as it lenses
// the city behind it.
//
// Apply it to the "dexmap" virtual controller only, not to the member devices.
// GLSL 1.10. One second of iTime is one real second at the default Speed (1000).

// ---- your Visual Map layout (grid units from the dexmap file, y grows down) --
#define MAP_SIZE   vec2(64.0, 64.0)
#define KB_MIN     vec2( 0.2, 43.2)     // keyboard top-left corner
#define KB_MAX     vec2(38.5, 56.8)     // keyboard bottom-right corner
#define RAM_L_X    43.7                 // centre line of the left RAM stick
#define RAM_R_X    47.3                 // centre line of the right RAM stick
#define RAM_TOP    9.8
#define RAM_BOT    29.4
#define RING_POS   vec2(56.7, 19.3)     // centre of the D_LED ring
#define RING_R     6.4

// ---- tuning ------------------------------------------------------------------
#define RAIN       1.0                  // >1 = more running drops
#define CITY       1.0                  // brightness of the city lights behind
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

// Out-of-focus city lights: soft discs drifting sideways and fading in and out.
vec3 bokeh(vec2 p, float t)
{
    vec3 acc = vec3(0.0);
    for (int i = 0; i < 14; i++)
    {
        float fi = float(i);
        float h1 = hash11(fi * 3.17 + 0.5);
        float h2 = hash11(fi * 7.31 + 1.5);
        float h3 = hash11(fi * 1.93 + 2.5);
        float speed = (h3 - 0.5) * 1.6;
        float x = mod(h1 * 80.0 + t * speed, 80.0) - 8.0;
        float y = mix(6.0, 60.0, h2);
        float r = mix(3.0, 5.5, h3);
        float fade = 0.5 + 0.5 * sin(t * (0.10 + 0.08 * h1) + fi * 2.0);
        vec2 d = p - vec2(x, y);
        float disc = 1.0 - smoothstep(r * 0.6, r, length(d));
        vec3 c = h1 > 0.45 ? vec3(0.85, 0.50, 0.16) : vec3(0.18, 0.40, 0.80);
        acc += disc * fade * c * 0.45;
    }
    return acc * CITY;
}

// Running drops in vertical lanes `laneW` wide between top and bottom.
// Returns x = drop head, y = wet trail.
vec2 runs(vec2 p, float t, float x0, float laneW, float top, float bottom, float seed)
{
    float lane = floor((p.x - x0) / laneW + 0.5);
    float cx = x0 + lane * laneW;
    float per = mix(7.0, 13.0, hash11(lane * 1.7 + seed));
    float tk = t * RAIN / per + hash11(lane * 4.3 + seed);
    float id = floor(tk);
    float k = fract(tk);
    if (hash11(id * 2.9 + lane * 1.3 + seed) < 0.35) return vec2(0.0);
    float kk = k + 0.035 * sin(k * 28.0 + lane);                  // stop-and-go
    float hy = mix(top - 3.0, bottom + 3.0, kk);
    float hx = cx + 0.5 * sin(hy * 0.5 + lane);
    vec2 d = p - vec2(hx, hy);
    float head = exp(-dot(d, d) / 0.9);
    float trail = exp(-(p.x - hx) * (p.x - hx) / 0.35) * (p.y < hy ? exp(-(hy - p.y) / 4.0) : 0.0);
    trail *= 1.0 - smoothstep(0.85, 1.0, k);
    return vec2(head, trail * 0.6);
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = fragCoord.xy / iResolution.xy;
    vec2 p = vec2(uv.x, 1.0 - uv.y) * MAP_SIZE;      // Visual Map grid coordinates
    float t = iTime;

    vec3 city = bokeh(p, t);
    vec3 col = vec3(0.016, 0.020, 0.034) + city;

    // drops resting on the glass: faint, slowly appearing and drying
    vec2 bc = floor(p / 2.25);
    float bh = hash12(bc);
    float bead = step(0.82, bh) * (0.5 + 0.5 * sin(t * 0.15 + bh * 40.0));
    col += bead * (0.04 + 0.5 * city);

    // running drops: keyboard lanes, one lane per RAM stick, lanes through the ring
    vec2 r = vec2(0.0);
    if (p.y > KB_MIN.y - 4.0) r = runs(p, t, KB_MIN.x + 1.1, 3.4, KB_MIN.y, KB_MAX.y, 1.0);
    else if (p.x < 0.5 * (RAM_R_X + RING_POS.x - RING_R)) r = runs(p, t, RAM_L_X, RAM_R_X - RAM_L_X, RAM_TOP, RAM_BOT, 2.0);
    else r = runs(p, t, RING_POS.x - RING_R, 3.2, RING_POS.y - RING_R - 1.0, RING_POS.y + RING_R + 1.0, 3.0);

    col += r.y * (vec3(0.05, 0.06, 0.08) + 0.8 * city);
    col += r.x * (vec3(0.45, 0.48, 0.55) + 1.4 * city);

    fragColor = vec4(clamp(col * BRIGHTNESS, 0.0, 1.0), 1.0);
}
