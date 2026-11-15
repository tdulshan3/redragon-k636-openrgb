// Northern Train - OpenRGB Effects "Shaders" effect, laid out for the "dexmap"
// Visual Map (keyboard bottom-left, RAM sticks and the D_LED ring to the
// upper right).
//
// A night sleeper express gliding through a snowy northern pine forest. Every
// eighteen seconds the train crosses the keyboard from right to left: first the
// locomotive's warm headlight cone sweeping along the steel rails, then a long
// row of cosy amber compartment windows, and finally the red tail-lamps fading
// into the dark while snowy embankments glow below. Above, puffs of silver
// steam rise up the two RAM sticks as the engine passes, and the D_LED ring
// glows as a railway signal arch that shifts from green to amber when the
// train goes through.
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
#define RING_POS   vec2(56.8, 19.0)     // centre of the D_LED ring = signal arch
#define RING_R     5.6                  // radius of the D_LED ring

// ---- tuning ------------------------------------------------------------------
#define PASS_SEC    18.0                // seconds between train passages
#define TRAIN_SPEED 4.6                 // train speed across keyboard (grid units/s)
#define STEAM       1.0                 // rising steam on the RAM sticks
#define BRIGHTNESS  1.0
// ------------------------------------------------------------------------------

float hash11(float p)
{
    p = fract(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return fract(p);
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = fragCoord.xy / iResolution.xy;
    vec2 p = vec2(uv.x, 1.0 - uv.y) * MAP_SIZE;
    float t = iTime;

    vec3 nightSky = vec3(0.008, 0.015, 0.045);
    vec3 snowBlue = vec3(0.025, 0.045, 0.095);
    vec3 warmWin  = vec3(1.00, 0.72, 0.24);
    vec3 headCol  = vec3(1.00, 0.92, 0.68);
    vec3 tailRed  = vec3(0.96, 0.12, 0.08);
    vec3 col      = nightSky;

    float tau    = mod(t, PASS_SEC);
    float passId = floor(t / PASS_SEC);
    // locomotive nose starts just to the right of the keyboard and travels left
    float headX    = (KB_MAX.x + 4.0) - tau * TRAIN_SPEED;
    float trainLen = 30.0;
    float tailX    = headX + trainLen;
    float trackY   = 0.5 * (KB_MIN.y + KB_MAX.y) + 0.4;

    // ---- keyboard: snowy embankment, steel rails, headlight, carriage windows, tail-lamp
    if (p.y > KB_MIN.y - 2.0 && p.x < KB_MAX.x + 2.0)
    {
        // snowy ground on bottom rows, dark pine forest above
        float embankment = smoothstep(trackY + 1.5, KB_MAX.y, p.y);
        col = mix(nightSky, snowBlue, embankment);

        // faint steel rail glint along bottom rows
        float rail = exp(-(p.y - (trackY + 2.6)) * (p.y - (trackY + 2.6)) / 0.8);
        float railLit = exp(-max(headX - p.x, 0.0) / 14.0) * step(p.x, tailX);
        col += rail * (0.05 + 0.30 * railLit) * vec3(0.55, 0.68, 0.90);

        // locomotive headlight cone sweeping left ahead of headX
        float ahead = headX - p.x;
        if (ahead > -2.0 && ahead < 18.0)
        {
            float spread = 1.6 + 0.24 * max(ahead, 0.0);
            float dy = p.y - trackY;
            float beam = exp(-(dy * dy) / (spread * spread)) * smoothstep(-2.0, 0.5, ahead) * exp(-max(ahead, 0.0) / 9.0);
            col += beam * headCol * 1.05;
        }

        // carriage body & lit compartment windows between headX and tailX
        float along = p.x - headX;
        if (along >= 0.0 && along <= trainLen)
        {
            // windows spaced every 4.5 units (~2 keys) so each window cleanly lights a key
            float carGap  = smoothstep(0.08, 0.18, abs(fract(along / 15.0) - 0.5));
            float winCell = floor(along / 4.5);
            float winF    = fract(along / 4.5);
            float winMask = smoothstep(0.10, 0.22, winF) * (1.0 - smoothstep(0.78, 0.90, winF)) * carGap;
            float winY    = exp(-(p.y - (trackY - 0.3)) * (p.y - (trackY - 0.3)) / 4.2);

            float h = hash11(winCell * 3.7 + passId * 11.0);
            float bright = h < 0.14 ? 0.30 : mix(0.78, 1.0, h);
            vec3 wCol = mix(vec3(0.98, 0.54, 0.16), warmWin, h);

            col += winMask * winY * bright * wCol * 1.05;

            // warm spill onto the snowy embankment below the carriages
            float snowSpill = smoothstep(trackY + 1.0, KB_MAX.y, p.y) * exp(-(p.y - trackY) / 5.0);
            col += snowSpill * (0.35 + 0.65 * winMask) * warmWin * 0.35;
        }

        // red tail-light at tailX with a warm ruby afterglow along the rails
        vec2 dt = p - vec2(tailX, trackY);
        float tailCore = exp(-dot(dt, dt) / 2.8);
        float tailWake = (p.x > tailX ? exp(-(p.x - tailX) / 6.5) : 0.0) * exp(-(p.y - trackY) * (p.y - trackY) / 3.5);
        col += (tailCore * 1.05 + tailWake * 0.45) * tailRed;
    }

    // ---- RAM sticks: puffs of silver-white locomotive steam rising and dissipating
    float dxL = abs(p.x - RAM_L_X);
    float dxR = abs(p.x - RAM_R_X);
    if (min(dxL, dxR) < 2.2 && p.y > RAM_TOP - 1.5 && p.y < RAM_BOT + 1.5 && STEAM > 0.0)
    {
        float stickIdx = dxL < dxR ? 0.0 : 1.0;
        vec3 steamAcc = vec3(0.0);
        for (int k = 0; k < 3; k++)
        {
            float puffT = mod(tau - stickIdx * 0.7 - float(k) * 2.2 + PASS_SEC, PASS_SEC);
            if (puffT < 9.5)
            {
                float py = RAM_BOT - puffT * 3.0;
                float radius = 2.4 + puffT * 0.55;
                float dy = p.y - py;
                float density = exp(-(dy * dy) / (radius * radius)) * smoothstep(0.0, 0.4, puffT) * exp(-puffT / 4.5);
                // warm firebox glow at the base of the puff, cool silver-blue moonlight higher up
                float fireGlow = exp(-puffT / 1.6) * (1.0 - smoothstep(RAM_BOT - 9.0, RAM_TOP, p.y));
                vec3 puffCol = mix(vec3(0.50, 0.60, 0.80), vec3(1.00, 0.58, 0.22), fireGlow * 0.70);
                steamAcc += density * puffCol;
            }
        }
        col += steamAcc * 0.95;
    }

    // ---- D_LED ring: railway signal arch & moonlit steam halo
    vec2 rd = p - RING_POS;
    float onRing = 1.0 - smoothstep(1.2, 2.6, abs(length(rd) - RING_R));
    if (onRing > 0.0)
    {
        float ang = atan(rd.y, rd.x + 1e-4);
        // signal is emerald when the track is clear, shifts to amber while the train passes
        float trainPresent = smoothstep(0.1, 1.0, tau) * (1.0 - smoothstep(9.5, 12.5, tau));
        vec3 sigClear = vec3(0.10, 0.82, 0.45);
        vec3 sigBusy  = vec3(1.00, 0.60, 0.14);
        vec3 sigCol   = mix(sigClear, sigBusy, trainPresent);

        float aspect = 0.38 + 0.62 * exp(-pow(sin(ang), 2.0) / 0.28);
        float steamWash = 0.22 * trainPresent * (0.5 + 0.5 * sin(ang * 2.0 - t * 1.4));
        vec3 ringCol = nightSky + sigCol * aspect * 0.78 + vec3(0.48, 0.56, 0.75) * steamWash;
        col = mix(col, ringCol, onRing);
    }

    fragColor = vec4(clamp(col * BRIGHTNESS, 0.0, 1.0), 1.0);
}
