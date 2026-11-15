// Sakura - OpenRGB Effects "Shaders" effect, laid out for the "dexmap"
// Visual Map (keyboard bottom-left, mouse beside it, RAM sticks and the D_LED
// ring to the upper right).
//
// Dusk under a cherry tree. The RAM sticks are its branches and the D_LED ring
// is a round cluster of blossom, both swaying gently in the breeze. Petals let
// go, drift past the mouse and float across the keyboard, fluttering and
// turning as they fall, and a soft pink carpet slowly gathers along the bottom
// rows before the wind sweeps it away.
//
// Apply it to the "dexmap" virtual controller only, not to the member devices.
// GLSL 1.10. One second of iTime is one real second at the default Speed (1000).

// ---- your Visual Map layout (grid units from the dexmap file, y grows down) --
#define MAP_SIZE   vec2(64.0, 64.0)
#define KB_MIN     vec2( 0.6, 45.4)     // keyboard top-left corner
#define KB_MAX     vec2(38.9, 58.9)     // keyboard bottom-right corner
#define MOUSE_POS  vec2(43.5, 49.8)
#define RAM_L_X    43.1                 // centre line of the left RAM stick
#define RAM_R_X    47.3                 // centre line of the right RAM stick
#define RAM_TOP    8.8
#define RAM_BOT    32.9
#define RING_POS   vec2(56.8, 19.0)     // centre of the D_LED ring
#define RING_R     5.6

// ---- tuning ------------------------------------------------------------------
#define WIND       1.0                  // >1 = petals blow across faster
#define PETALS     1.0                  // 0 = no falling petals
#define CARPET_SEC 300.0                // seconds for the carpet to gather
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

float noise(vec2 p)
{
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash12(i), hash12(i + vec2(1.0, 0.0)), f.x),
               mix(hash12(i + vec2(0.0, 1.0)), hash12(i + vec2(1.0, 1.0)), f.x), f.y);
}

// One petal on its way from `start`, moving `vel` per second for `life` seconds.
float petal(vec2 p, float t, float k, vec2 start, vec2 spread, vec2 vel, float life, float rest)
{
    float per = life + rest;
    float tk = t / per + hash11(k * 5.7);
    float id = floor(tk);
    float age = fract(tk) * per;
    vec2 h = vec2(hash11(id * 3.1 + k), hash11(id * 7.3 + k * 3.0));
    vec2 pos = start + (h - 0.5) * spread + vel * age * WIND;
    pos += vec2(0.7 * sin(age * 1.3 + h.x * 6.28), 1.0 * sin(age * 2.1 + h.y * 6.28));   // flutter
    float turn = 0.55 + 0.45 * sin(age * 2.4 + h.x * 9.0);                                // catching the light
    float on = smoothstep(0.0, 1.0, age) * (1.0 - smoothstep(life - 1.5, life, age));
    vec2 d = p - pos;
    return on * turn * exp(-dot(d, d) / 2.6);
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = fragCoord.xy / iResolution.xy;
    vec2 p = vec2(uv.x, 1.0 - uv.y) * MAP_SIZE;      // Visual Map grid coordinates
    float t = iTime;

    vec3 pink  = vec3(0.95, 0.50, 0.62);
    vec3 pale  = vec3(0.98, 0.78, 0.82);

    // ---- dusk: violet sky behind the tree, blue-grey ground under it
    vec3 col = mix(vec3(0.07, 0.05, 0.15), vec3(0.035, 0.035, 0.075), smoothstep(30.0, 44.0, p.y));

    // ---- the tree: blossom on the RAM branches and the round cluster on the ring
    float dx = min(abs(p.x - RAM_L_X), abs(p.x - RAM_R_X));
    float onRam = (1.0 - smoothstep(1.6, 2.4, dx)) * step(RAM_TOP - 1.5, p.y) * step(p.y, RAM_BOT + 1.5);
    float onRing = 1.0 - smoothstep(1.2, 2.5, abs(length(p - RING_POS) - RING_R));
    float sway = noise(vec2(p.x * 0.18 - t * 0.25, p.y * 0.22 + 0.6 * sin(t * 0.3)));
    float bloom = smoothstep(0.25, 0.75, sway);
    vec3 tree = mix(vec3(0.10, 0.04, 0.05), mix(pink, pale, smoothstep(0.6, 0.95, sway)) * 0.6, 0.35 + 0.65 * bloom);
    col = mix(col, tree, max(onRam, onRing));

    // ---- petal carpet gathering on the bottom rows, swept away now and then
    float cyc = mod(t, CARPET_SEC + 30.0);
    float amount = smoothstep(0.0, CARPET_SEC, cyc) * (1.0 - smoothstep(CARPET_SEC, CARPET_SEC + 30.0, cyc));
    float carpet = smoothstep(KB_MAX.y - 5.5, KB_MAX.y - 1.0, p.y) * step(p.x, KB_MAX.x + 1.0) * step(KB_MIN.y, p.y);
    carpet *= smoothstep(1.0 - amount, 1.0 - amount + 0.25, noise(p * 0.45 + 3.0));
    col = mix(col, pink * 0.38, carpet * 0.9);

    // ---- falling petals: from the tree, past the mouse, across the keyboard
    float s = 0.0;
    for (int k = 0; k < 9; k++)
        s += petal(p, t, float(k), vec2(KB_MAX.x + 6.0, KB_MIN.y + 1.0), vec2(6.0, 6.0), vec2(-2.3, 0.45), 20.0, 3.0 + 4.0 * hash11(float(k)));
    for (int k = 9; k < 12; k++)
        s += petal(p, t, float(k), vec2(0.5 * (RAM_L_X + RING_POS.x), RAM_TOP + 2.0), vec2(14.0, 6.0), vec2(-0.6, 1.6), 11.0, 4.0);
    col += PETALS * min(s, 1.2) * pale * 0.9;

    fragColor = vec4(clamp(col * BRIGHTNESS, 0.0, 1.0), 1.0);
}
