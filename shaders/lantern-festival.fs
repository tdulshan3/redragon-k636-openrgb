// Lantern Festival - OpenRGB Effects "Shaders" effect, laid out for the
// "dexmap" Visual Map (keyboard bottom-left, RAM sticks and the D_LED ring
// to the upper right).
//
// Night by a quiet river during a lantern festival. On the keyboard, paper
// lanterns float slowly from left to right along dark water lanes, bobbing and
// trailing soft golden reflections below them. Every few seconds a sky lantern
// lifts off, climbs the two RAM sticks swaying gently as its flame catches, and
// drifts into the D_LED ring, where lanterns circle slowly into the night sky.
//
// Apply it to the "dexmap" virtual controller only, not to the member devices.
// GLSL 1.10. One second of iTime is one real second at the default Speed (1000).

// ---- your Visual Map layout (grid units, 64x64 normalised space, y down) -----
#define MAP_SIZE   vec2(64.0, 64.0)
#define KB_MIN     vec2( 0.6, 45.4)     // keyboard top-left corner
#define KB_MAX     vec2(38.9, 58.9)     // keyboard bottom-right corner
#define RAM_L_X    43.1                 // centre line of the left RAM stick
#define RAM_R_X    47.3                 // centre line of the right RAM stick
#define RAM_TOP    8.8
#define RAM_BOT    32.9
#define RING_POS   vec2(56.8, 19.0)     // centre of the D_LED ring
#define RING_R     5.6                  // radius of the D_LED ring

// ---- tuning ------------------------------------------------------------------
#define DRIFT_SPEED 1.0                 // >1 = river & sky lanterns move faster
#define RISE_SPEED  1.0                 // >1 = lanterns climb the RAM faster
#define REFLECTION  1.0                 // water shimmer under floating lanterns
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

// Warm paper lantern palette: bright candle core to deep amber-rose paper edge.
vec3 lanternGlow(float core, float halo, float flicker, float tint)
{
    vec3 inner = vec3(1.00, 0.86, 0.52);
    vec3 paper = mix(vec3(0.96, 0.44, 0.10), vec3(0.92, 0.28, 0.14), tint);
    return (inner * core + paper * halo) * (0.85 + 0.15 * flicker);
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = fragCoord.xy / iResolution.xy;
    vec2 p = vec2(uv.x, 1.0 - uv.y) * MAP_SIZE;
    float t = iTime;

    // ---- deep indigo night sky above, dark river water on the keyboard
    float inRiver = smoothstep(KB_MIN.y - 3.0, KB_MIN.y + 1.0, p.y);
    vec3 sky   = mix(vec3(0.008, 0.012, 0.042), vec3(0.016, 0.022, 0.065), smoothstep(6.0, 38.0, p.y));
    float ripple = sin(p.x * 0.65 - t * 0.8 + sin(p.y * 1.1 + t * 0.4));
    vec3 river = vec3(0.008, 0.018, 0.052) + 0.006 * ripple * vec3(0.2, 0.4, 0.8);
    vec3 col   = mix(sky, river, inRiver);

    // ---- floating river lanterns drifting left -> right across the keyboard
    if (p.y > KB_MIN.y - 3.0 && p.x < KB_MAX.x + 4.0)
    {
        float spanX = (KB_MAX.x - KB_MIN.x) + 12.0;
        for (int i = 0; i < 6; i++)
        {
            float fi = float(i);
            float h1 = hash11(fi * 4.13 + 1.2);
            float h2 = hash11(fi * 7.71 + 3.4);
            float spd = mix(1.4, 2.2, h1) * DRIFT_SPEED;
            float lx = KB_MIN.x - 6.0 + mod(t * spd + fi * (spanX / 6.0) + h2 * 5.0, spanX);
            float laneY = mix(KB_MIN.y + 1.8, KB_MAX.y - 2.2, (mod(fi, 3.0) + 0.5) / 3.0);
            float ly = laneY + 0.65 * sin(t * (0.7 + 0.3 * h1) + fi * 2.1);

            vec2 d = p - vec2(lx, ly);
            float core = exp(-dot(d, d) / 1.6);
            float halo = exp(-dot(d, vec2(d.x * 0.7, d.y)) / 6.5);
            float flk  = sin(t * (3.2 + h2 * 2.0) + fi * 4.0);

            // rippled water reflection below each lantern
            float ry = max(p.y - ly, 0.0);
            float rx = (p.x - lx) + 0.55 * sin(p.y * 2.1 - t * 1.8 + fi);
            float refl = REFLECTION * exp(-rx * rx / 2.2) * exp(-ry / 4.2) * smoothstep(0.0, 0.8, ry);

            col += lanternGlow(core, 0.55 * halo + 0.45 * refl, flk, h2);
        }
    }

    // ---- rising sky lanterns climbing the two RAM sticks
    float dxL = abs(p.x - RAM_L_X);
    float dxR = abs(p.x - RAM_R_X);
    if (min(dxL, dxR) < 3.0 && p.y > RAM_TOP - 3.0 && p.y < RAM_BOT + 3.0)
    {
        float spanY = (RAM_BOT - RAM_TOP) + 10.0;
        for (int k = 0; k < 4; k++)
        {
            float fk = float(k);
            float stickX = mod(fk, 2.0) < 0.5 ? RAM_L_X : RAM_R_X;
            float h = hash11(fk * 5.3 + 2.0);
            float spd = mix(2.2, 3.1, h) * RISE_SPEED;
            float ly = RAM_BOT + 5.0 - mod(t * spd + fk * 9.5, spanY);
            float lx = stickX + 0.45 * sin(t * 1.1 + fk * 2.5);

            vec2 d = p - vec2(lx, ly);
            float core = exp(-(d.x * d.x / 2.2 + d.y * d.y / 2.6));
            float halo = exp(-(d.x * d.x / 4.5 + d.y * d.y / 9.0));
            // warm draft trail below the ascending lantern
            float wake = exp(-d.x * d.x / 2.5) * (p.y > ly ? exp(-(p.y - ly) / 5.5) : 0.0);
            float flk  = sin(t * 4.0 + fk * 3.1);
            col += lanternGlow(core, 0.65 * halo + 0.25 * wake, flk, h);
        }
    }

    // ---- lanterns circling slowly around the D_LED ring in the high sky
    vec2 rd = p - RING_POS;
    float onRing = 1.0 - smoothstep(1.3, 2.8, abs(length(rd) - RING_R));
    if (onRing > 0.0)
    {
        float ang = atan(rd.y, rd.x + 1e-4);
        vec3 ringAcc = vec3(0.0);
        for (int m = 0; m < 3; m++)
        {
            float fm = float(m);
            float a0 = t * (0.28 + 0.04 * fm) * DRIFT_SPEED + fm * 2.094395;
            float da = mod(ang - a0 + 3.14159265, 6.2831853) - 3.14159265;
            float core = exp(-da * da / 0.14);
            float trail = da < 0.0 ? exp(da / 0.65) : exp(-da * da / 0.18);
            float flk = sin(t * 3.5 + fm * 2.3);
            ringAcc += lanternGlow(core, 0.55 * trail, flk, fm * 0.4);
        }
        col += onRing * ringAcc;
    }

    col = 1.0 - exp(-col * 1.15);
    fragColor = vec4(clamp(col * BRIGHTNESS, 0.0, 1.0), 1.0);
}
