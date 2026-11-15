// Deep Sonar - OpenRGB Effects "Shaders" effect, laid out for the "dexmap"
// Visual Map (keyboard bottom-left, RAM sticks and the D_LED ring to the
// upper right).
//
// A submarine control room in dark phosphor green. The D_LED ring is the
// rotary sonar scope: a crisp green sweep arm turns steadily around the ring,
// lighting up contacts that fade in its phosphor wake. The two RAM sticks are
// vertical depth and hydrophone level meters that pulse and climb whenever an
// echo returns. Across the keyboard, periodic sonar ping rings expand outward
// over faint ocean-floor contour ridges, illuminating slow-moving contact blips
// that flare bright emerald when the ping wave crosses them and fade over time.
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
#define RING_POS   vec2(56.8, 19.0)     // centre of the D_LED ring = rotary scope
#define RING_R     5.6                  // radius of the D_LED ring

// ---- tuning ------------------------------------------------------------------
#define PING_SEC    6.5                 // seconds between sonar pings on keyboard
#define SWEEP_SEC   4.0                 // seconds per rotation on the D_LED ring
#define CONTACTS    1.0                 // 0 = hide contact blips
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

    vec3 darkBg = vec3(0.003, 0.022, 0.014);
    vec3 phos   = vec3(0.08, 0.88, 0.42);
    vec3 hot    = vec3(0.55, 1.00, 0.72);
    vec3 col    = darkBg;

    float pingCycle = mod(t, PING_SEC);
    float pingPhase = pingCycle / PING_SEC;

    // ---- keyboard: ocean-floor contour chart, expanding ping ring, and contact blips
    if (p.y > KB_MIN.y - 2.0 && p.x < KB_MAX.x + 2.0)
    {
        // emitter origin shifts gently near the left-centre of the keyboard
        vec2 origin = vec2(KB_MIN.x + 6.0 + 3.0 * sin(t * 0.07), 0.5 * (KB_MIN.y + KB_MAX.y));
        float dist = length(p - origin);
        float pingR = pingCycle * 8.2;

        // expanding sonar wavefront with a soft trailing phosphor decay
        float behind = pingR - dist;
        float front = exp(-(dist - pingR) * (dist - pingR) / 1.4);
        float wake  = smoothstep(-0.5, 0.5, behind) * exp(-max(behind, 0.0) / 5.5);
        float env   = 1.0 - smoothstep(0.78, 1.0, pingPhase);

        // submarine terrain contour lines revealed as the ping sweeps over them
        float n = noise(p * 0.16 + vec2(t * 0.015, 0.0));
        float contour = 1.0 - smoothstep(0.0, 0.08, abs(fract(n * 5.0) - 0.5));
        col += phos * (0.04 * contour + (0.65 * front + 0.22 * wake * (0.45 + 0.55 * contour)) * env);
        col += hot * front * env * 0.35;

        // 4 slow-moving underwater contacts that light up when the ping wave hits them
        for (int i = 0; i < 4; i++)
        {
            float fi = float(i);
            float h1 = hash11(fi * 3.7 + 1.0);
            float h2 = hash11(fi * 6.1 + 2.0);
            vec2 cpos = vec2(
                mix(KB_MIN.x + 7.0, KB_MAX.x - 3.0, fract(h1 + t * (0.012 + 0.005 * fi))),
                mix(KB_MIN.y + 1.8, KB_MAX.y - 1.8, 0.5 + 0.42 * sin(t * 0.09 + fi * 2.1 + h2 * 6.28))
            );
            float cdist = length(cpos - origin);
            float sinceHit = mod(pingCycle - cdist / 8.2 + PING_SEC, PING_SEC);
            float blipGlow = exp(-sinceHit / 2.8) * smoothstep(0.0, 0.08, sinceHit);
            vec2 dc = p - cpos;
            float dotCore = exp(-dot(dc, dc) / 1.3);
            float dotRing = exp(-abs(length(dc) - 1.6) / 0.45) * exp(-sinceHit / 1.2);
            col += CONTACTS * blipGlow * (dotCore * hot + 0.45 * dotRing * phos);
        }
    }

    // ---- RAM sticks: left = depth gauge, right = hydrophone echo bar graph
    float dxL = abs(p.x - RAM_L_X);
    float dxR = abs(p.x - RAM_R_X);
    if (min(dxL, dxR) < 2.2 && p.y > RAM_TOP - 1.5 && p.y < RAM_BOT + 1.5)
    {
        float h01 = clamp((RAM_BOT - p.y) / (RAM_BOT - RAM_TOP), 0.0, 1.0); // 0 bottom .. 1 top
        if (dxL < dxR)
        {
            // left RAM stick: slow diving depth marker gliding up and down
            float depthPos = 0.5 + 0.38 * sin(t * 0.11);
            float marker = exp(-(h01 - depthPos) * (h01 - depthPos) / 0.015);
            float scaleTicks = 0.12 + 0.28 * step(h01, depthPos);
            col = darkBg + phos * scaleTicks + hot * marker * 0.85;
        }
        else
        {
            // right RAM stick: hydrophone signal meter spiking on ping + echo return
            float p1 = exp(-pingCycle / 1.1);
            float echoT = mod(pingCycle - 2.2 + PING_SEC, PING_SEC);
            float p2 = 0.75 * exp(-echoT / 1.4);
            float level = clamp(0.15 + 0.80 * max(p1, p2) + 0.08 * sin(t * 3.1), 0.0, 1.0);
            float lit = smoothstep(level + 0.04, level - 0.04, h01);
            float peak = exp(-(h01 - level) * (h01 - level) / 0.008);
            col = darkBg + lit * mix(phos * 0.55, hot, smoothstep(0.6, 1.0, h01)) + peak * hot * 0.7;
        }
    }

    // ---- D_LED ring: rotary radar/sonar sweep with fading phosphor trail
    vec2 rd = p - RING_POS;
    float onRing = 1.0 - smoothstep(1.2, 2.6, abs(length(rd) - RING_R));
    if (onRing > 0.0)
    {
        float ang = atan(rd.y, rd.x + 1e-4);
        float beamA = 6.2831853 * t / SWEEP_SEC;
        // angle behind the clockwise sweep arm (0 at arm .. 2*pi just ahead of arm)
        float trailA = mod(beamA - ang, 6.2831853);
        float head = exp(-trailA * trailA / 0.05);
        float tail = exp(-trailA / 1.85);

        // two radar targets on the ring that flare when the sweep passes them
        float t1 = mod(ang - (1.2 + t * 0.03) + 3.14159, 6.2831853) - 3.14159;
        float t2 = mod(ang - (-1.7 - t * 0.02) + 3.14159, 6.2831853) - 3.14159;
        float blip = (exp(-t1 * t1 / 0.06) + exp(-t2 * t2 / 0.06)) * exp(-trailA / 3.2);

        vec3 scope = darkBg + phos * (0.55 * tail + 0.75 * blip) + hot * (0.95 * head + 0.45 * blip);
        col = mix(col, scope, onRing);
    }

    fragColor = vec4(clamp(col * BRIGHTNESS, 0.0, 1.0), 1.0);
}
