// Bamboo Rain (Shishi-odoshi) - OpenRGB Effects "Shaders" effect, laid out for
// the "dexmap" Visual Map (keyboard bottom-left, RAM sticks and the D_LED ring
// to the upper right).
//
// A quiet Japanese moss garden in the rain. Clear spring water trickles down a
// bamboo spout on the left RAM stick into a pivoted bamboo rocker on the right
// RAM stick, filling it LED by LED with jade-cyan water. Every nine seconds
// the full bamboo tube tips over in a rush, strikes a wet stone at the base,
// and sends a bright splash and concentric water ripples rolling across the
// dark mossy pond on the keyboard, while raindrops dimple the keys and a stone
// lantern on the D_LED ring breathes with a warm candle flame.
//
// Apply it to the "dexmap" virtual controller only, not to the member devices.
// GLSL 1.10. One second of iTime is one real second at the default Speed (1000).

// ---- your Visual Map layout (grid units, 64x64 normalised space, y down) -----
#define MAP_SIZE   vec2(64.0, 64.0)
#define KB_MIN     vec2( 0.6, 45.4)     // keyboard top-left corner
#define KB_MAX     vec2(38.9, 58.9)     // keyboard bottom-right corner
#define RAM_L_X    43.1                 // left RAM stick = water spout
#define RAM_R_X    47.3                 // right RAM stick = tipping bamboo tube
#define RAM_TOP    8.8
#define RAM_BOT    32.9
#define RING_POS   vec2(56.8, 19.0)     // D_LED ring = stone garden lantern
#define RING_R     5.6                  // radius of the D_LED ring

// ---- tuning ------------------------------------------------------------------
#define CYCLE_SEC   9.0                 // seconds to fill and tip the bamboo tube
#define BAMBOO_SWAY 1.0                 // swaying bamboo shadow/leaf shimmer
#define LANTERN     1.0                 // warm candle in the stone lantern ring
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

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = fragCoord.xy / iResolution.xy;
    vec2 p = vec2(uv.x, 1.0 - uv.y) * MAP_SIZE;
    float t = iTime;

    float tau   = mod(t, CYCLE_SEC);
    float fillT = CYCLE_SEC - 1.6;                          // filling phase, then tip & splash
    float fill  = clamp(tau / fillT, 0.0, 1.0);
    float tip   = smoothstep(fillT, fillT + 0.30, tau) * (1.0 - smoothstep(fillT + 0.30, CYCLE_SEC, tau));
    float sinceSplash = mod(t - fillT + CYCLE_SEC, CYCLE_SEC);

    vec3 mossDark = vec3(0.008, 0.032, 0.022);
    vec3 mossLit  = vec3(0.035, 0.140, 0.075);
    vec3 jade     = vec3(0.12, 0.72, 0.64);
    vec3 foam     = vec3(0.70, 0.98, 0.92);
    vec3 col      = mossDark;

    // ---- mossy garden pond on the keyboard + splash wave from the bamboo strike
    if (p.y > KB_MIN.y - 2.0 && p.x < KB_MAX.x + 2.0)
    {
        // swaying bamboo stalks & dappled forest light over dark moss/water
        float sway = p.x + BAMBOO_SWAY * 1.6 * sin(p.y * 0.22 + t * 0.50);
        float canopy = noise(vec2(sway * 0.18, p.y * 0.25 - t * 0.10));
        col = mix(mossDark, mossLit, smoothstep(0.32, 0.78, canopy));

        // mossy stones along the bottom-left edge
        float stone = smoothstep(KB_MAX.y - 3.2, KB_MAX.y - 0.8, p.y + 1.2 * sin(p.x * 0.22));
        col = mix(col, vec3(0.05, 0.18, 0.08), 0.55 * stone);

        // splash origin at the top-right corner of the keyboard (below the RAM spout)
        vec2 splashPos = vec2(KB_MAX.x - 2.5, KB_MIN.y + 1.5);
        float d = length(p - splashPos);
        float waveR = sinceSplash * 7.2;

        // primary and secondary concentric waves rolling across the entire keyboard
        float r1 = exp(-(d - waveR) * (d - waveR) / 3.2);
        float r2 = 0.65 * exp(-(d - (waveR - 5.0)) * (d - (waveR - 5.0)) / 2.8) * step(5.0, waveR);
        float decay = exp(-sinceSplash / 5.2) * smoothstep(0.0, 0.10, sinceSplash);
        float splashCore = exp(-d * d / 16.0) * exp(-sinceSplash / 0.9);

        col += (r1 + r2) * decay * jade * 1.05;
        col += (r1 * 0.60 + splashCore) * decay * foam * 0.90;

        // raindrop rings dimpling the pond between bamboo strikes
        for (int k = 0; k < 4; k++)
        {
            float fk = float(k);
            float per = 3.4 + fk * 0.9;
            float tk = t / per + fk * 0.37;
            float id = floor(tk);
            float age = fract(tk) * per;
            vec2 dp = mix(KB_MIN + vec2(3.0, 2.0), KB_MAX - vec2(4.0, 2.0),
                          vec2(hash11(id * 2.3 + fk * 7.1), hash11(id * 5.7 + fk * 3.3)));
            float rd = length(p - dp) - age * 4.0;
            float ring = exp(-rd * rd / 1.1) + 0.35 * exp(-dot(p - dp, p - dp) / 2.5) * exp(-age * 2.5);
            col += ring * exp(-age / 1.6) * mix(jade, foam, 0.35) * 0.65;
        }
    }

    // ---- RAM sticks: left = trickling water spout, right = filling & tipping bamboo rocker
    float dxL = abs(p.x - RAM_L_X);
    float dxR = abs(p.x - RAM_R_X);
    if (min(dxL, dxR) < 2.2 && p.y > RAM_TOP - 1.5 && p.y < RAM_BOT + 1.5)
    {
        float h01 = clamp((RAM_BOT - p.y) / (RAM_BOT - RAM_TOP), 0.0, 1.0); // 0 bottom .. 1 top
        vec3 bamboo = vec3(0.04, 0.11, 0.045);

        if (dxL < dxR)
        {
            // left RAM: steady stream of water droplets trickling down the spout
            float stream = 0.45 + 0.55 * sin(p.y * 0.95 - t * 6.5);
            float pulse  = 0.5 + 0.5 * sin(p.y * 0.45 - t * 3.2);
            float active = 1.0 - 0.6 * tip;
            col = bamboo + active * (jade * (0.35 + 0.45 * stream) + foam * 0.25 * pulse * stream);
        }
        else
        {
            // right RAM: fills from bottom to top, then dumps downward in a bright rush
            float level = fill * (1.0 - smoothstep(fillT, fillT + 0.45, tau));
            float inWater = smoothstep(level + 0.06, level - 0.06, h01);
            float meniscus = exp(-(h01 - level) * (h01 - level) / 0.008) * (1.0 - tip);

            // when tipping, a bright waterfall rushes down the bottom of the stick
            float rushPos = 1.0 - clamp((tau - fillT) / 0.55, 0.0, 1.0);
            float dump = tip * exp(-(h01 - rushPos) * (h01 - rushPos) / 0.06);

            col = bamboo + inWater * jade * 0.85 + meniscus * foam * 0.75 + dump * foam * 1.1;
        }
    }

    // ---- D_LED ring: mossy stone lantern with a warm breathing candle flame
    vec2 rd = p - RING_POS;
    float onRing = 1.0 - smoothstep(1.2, 2.6, abs(length(rd) - RING_R));
    if (onRing > 0.0 && LANTERN > 0.0)
    {
        float ang = atan(rd.y, rd.x + 1e-4);
        // mossy stone rim at top & bottom, warm paper window glow on the sides
        float window = 0.35 + 0.65 * pow(cos(ang), 2.0);
        // candle flickers gently, and jumps slightly when the bamboo strikes the stone
        float strikeJolt = exp(-sinceSplash / 0.5) * sin(sinceSplash * 22.0);
        float breath = 0.78 + 0.16 * sin(t * 1.8) + 0.08 * sin(t * 4.7) + 0.20 * strikeJolt;
        vec3 candle = mix(vec3(0.85, 0.38, 0.08), vec3(1.00, 0.78, 0.35), 0.5 * window) * window * breath;
        vec3 stoneRim = mix(vec3(0.03, 0.10, 0.04), candle, 0.80);
        col = mix(col, stoneRim, onRing);
    }

    fragColor = vec4(clamp(col * BRIGHTNESS, 0.0, 1.0), 1.0);
}
