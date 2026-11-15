// Grandfather Clock - OpenRGB Effects "Shaders" effect, laid out for the
// "dexmap" Visual Map (keyboard bottom-left, RAM sticks and the D_LED ring
// to the upper right).
//
// Inside a dark walnut and brass longcase clock at night. Across the keyboard,
// a heavy polished brass pendulum bob swings smoothly left and right in a calm
// two-second period, leaving a warm phosphor afterglow and catching the carved
// wooden ribs of the case as it passes. The two RAM sticks are the hanging
// brass weights on their chains, one slowly descending while the other rises,
// with each second's tick sending a faint glint down the rods. The D_LED ring
// is the escapement wheel and clock face: a bright pallet tooth ticks around
// the ring once a second, and once every minute a warm chime ripple expands
// across the whole clock.
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
#define RING_POS   vec2(56.8, 19.0)     // centre of the D_LED ring = escapement
#define RING_R     5.6                  // radius of the D_LED ring

// ---- tuning ------------------------------------------------------------------
#define SWING_SEC  2.0                  // full left-right-left period (seconds)
#define CHIME_SEC  60.0                 // seconds between chime ripples
#define TRAIL      1.0                  // warm afterglow behind the bob
#define BRIGHTNESS 1.0
// ------------------------------------------------------------------------------

// Smooth step-with-recoil for a mechanical escapement tick once per second.
float tickStep(float t)
{
    float s = floor(t);
    float f = fract(t);
    // quick advance in the first 0.18 s with a slight anchor recoil
    float adv = smoothstep(0.0, 0.16, f) + 0.08 * sin(f * 3.14159 / 0.22) * (1.0 - smoothstep(0.12, 0.28, f));
    return s + adv;
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = fragCoord.xy / iResolution.xy;
    vec2 p = vec2(uv.x, 1.0 - uv.y) * MAP_SIZE;
    float t = iTime;

    vec3 brass  = vec3(0.98, 0.72, 0.26);
    vec3 copper = vec3(0.72, 0.36, 0.10);
    vec3 wood   = vec3(0.024, 0.012, 0.006);
    vec3 col    = wood;

    // ---- pendulum chamber across the keyboard
    if (p.y > KB_MIN.y - 2.0 && p.x < KB_MAX.x + 2.0)
    {
        float cx = 0.5 * (KB_MIN.x + KB_MAX.x);
        float cy = 0.5 * (KB_MIN.y + KB_MAX.y) + 1.2;
        float amp = 0.42 * (KB_MAX.x - KB_MIN.x);

        float omega = 6.2831853 / SWING_SEC;
        float phase = omega * t;
        float s = sin(phase);
        float c = cos(phase);

        // bob position along a gentle circular arc
        float bx = cx + amp * s;
        float by = cy - 2.4 * (1.0 - s * s);
        vec2 bob = vec2(bx, by);

        // carved wooden vertical ribs dimly lit when the bob swings near
        float rib = 0.5 + 0.5 * cos((p.x - cx) * 1.4);
        float caseGlow = exp(-(p.x - bx) * (p.x - bx) / 55.0);
        col = wood * (0.8 + 0.4 * rib) + caseGlow * copper * 0.16;

        // pendulum rod slanting down from a pivot high above the keyboard
        vec2 pivot = vec2(cx, KB_MIN.y - 26.0);
        vec2 dir = normalize(bob - pivot);
        vec2 qp = p - pivot;
        float along = dot(qp, dir);
        float perp  = abs(dot(qp, vec2(-dir.y, dir.x)));
        float rodLen = length(bob - pivot);
        float rod = exp(-perp * perp / 0.85) * smoothstep(0.0, rodLen, along) * (1.0 - smoothstep(rodLen - 1.5, rodLen + 0.5, along));
        col += rod * mix(copper, brass, 0.5) * 0.55;

        // warm phosphor wake trailing behind the moving bob
        float behind = (bx - p.x) * sign(c);
        float wakeY = abs(p.y - by);
        float wake = smoothstep(-0.8, 0.6, behind) * exp(-max(behind, 0.0) / (6.5 * abs(c) + 0.8)) * exp(-wakeY * wakeY / 4.5);
        col += TRAIL * wake * copper * 0.55;

        // the heavy brass bob itself: bright specular core + warm rim
        vec2 db = p - bob;
        float d2 = dot(db, db);
        float rim  = exp(-d2 / 9.5);
        float core = exp(-d2 / 2.4);
        col += rim * copper * 0.75 + core * brass * 0.95;

        // subtle tick flash at the two extreme turning points
        float turnFlash = pow(abs(s), 16.0);
        col += turnFlash * exp(-d2 / 18.0) * brass * 0.25;
    }

    // ---- hanging brass weights & chains on the two RAM sticks
    float dxL = abs(p.x - RAM_L_X);
    float dxR = abs(p.x - RAM_R_X);
    if (min(dxL, dxR) < 2.2 && p.y > RAM_TOP - 1.5 && p.y < RAM_BOT + 1.5)
    {
        bool isLeft = dxL < dxR;
        float cycle = fract(t / 180.0);
        // left weight descends while right weight ascends
        float wFrac = isLeft ? mix(0.18, 0.82, cycle) : mix(0.82, 0.18, cycle);
        float wy = mix(RAM_TOP + 2.0, RAM_BOT - 2.0, wFrac);

        // brass chain above the weight, with a tick glint travelling down each second
        float chain = step(p.y, wy);
        float tickGlint = exp(-pow(fract((p.y - RAM_TOP) * 0.12 - fract(t)), 2.0) / 0.04);
        vec3 stickCol = wood + chain * copper * (0.20 + 0.22 * tickGlint);

        // heavy cylindrical brass weight (covers ~2 RAM LEDs)
        float dy = abs(p.y - wy);
        float weightBody = 1.0 - smoothstep(2.6, 4.2, dy);
        float weightCore = exp(-dy * dy / 5.0);
        stickCol += weightBody * copper * 0.65 + weightCore * brass * 0.65;
        col = stickCol;
    }

    // ---- escapement wheel & clock dial on the D_LED ring
    vec2 rd = p - RING_POS;
    float onRing = 1.0 - smoothstep(1.2, 2.6, abs(length(rd) - RING_R));
    if (onRing > 0.0)
    {
        float ang = fract(atan(rd.x, -rd.y) / 6.2831853 + 1.0);   // 0 at top, clockwise
        float stepPos = mod(tickStep(t) / 12.0, 1.0);             // 12 escapement teeth around ring
        float da = abs(mod(ang - stepPos + 0.5, 1.0) - 0.5);

        // minute progress arc filling smoothly around the dial
        float minArc = fract(t / CHIME_SEC);
        float inArc = smoothstep(ang - 0.03, ang + 0.03, minArc);

        // 12 softly glowing gear teeth + bright active pallet tooth
        float teeth = 0.5 + 0.5 * cos(ang * 12.0 * 6.2831853);
        float activeTooth = exp(-da * da / 0.0035);
        vec3 dial = wood + copper * (0.12 + 0.18 * teeth + 0.30 * inArc) + brass * activeTooth * 0.95;
        col = mix(col, dial, onRing);
    }

    // ---- once-a-minute golden chime wave expanding from the dial across the desk
    float chimeAge = mod(t, CHIME_SEC);
    if (chimeAge < 4.5)
    {
        float rChime = chimeAge * 14.0;
        float dc = abs(length(p - RING_POS) - rChime);
        float ringWave = exp(-dc * dc / 5.0) * (1.0 - chimeAge / 4.5);
        col += ringWave * brass * 0.55;
    }

    fragColor = vec4(clamp(col * BRIGHTNESS, 0.0, 1.0), 1.0);
}
