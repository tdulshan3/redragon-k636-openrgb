// Star Trails - OpenRGB Effects "Shaders" effect, laid out for the "dexmap"
// Visual Map (keyboard bottom-left, mouse beside it, RAM sticks and the D_LED
// ring to the upper right).
//
// A long-exposure night sky. The pole star sits in the middle of the D_LED
// ring and the whole sky turns slowly around it, so every star draws a fading
// arc: tight little circles on the ring, long curves over the RAM sticks, and
// slanting streaks crossing the top rows of the keyboard. Below them a dark
// hillside with a few warm windows fills the bottom rows, and every so often a
// shooting star slips across.
//
// Apply it to the "dexmap" virtual controller only, not to the member devices.
// GLSL 1.10. One second of iTime is one real second at the default Speed (1000).

// ---- your Visual Map layout (grid units from the dexmap file, y grows down) --
#define MAP_SIZE   vec2(64.0, 64.0)
#define KB_MIN     vec2( 0.2, 43.2)     // keyboard top-left corner
#define KB_MAX     vec2(38.5, 56.8)     // keyboard bottom-right corner
#define MOUSE_POS  vec2(43.5, 49.8)
#define RING_POS   vec2(56.7, 19.3)     // centre of the D_LED ring = the pole star
#define HILL_Y     49.5                 // where the hills start (keyboard row 3)

// ---- tuning ------------------------------------------------------------------
#define TURN_SEC   300.0                // seconds for the sky to turn once
#define TRAIL      7.0                  // trail length (grid units of arc)
#define METEOR_SEC 40.0                 // average seconds between shooting stars
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

vec3 trails(vec2 p, float t)
{
    vec2 d = p - RING_POS;
    float r = length(d);
    float a = atan(d.y, d.x + 1e-4);
    float rot = 6.2831853 * t / TURN_SEC;
    float bw = 2.4;                                   // width of each ring of stars
    vec3 acc = vec3(0.0);
    for (int db = -1; db <= 1; db++)
    {
        float b  = floor(r / bw) + float(db);
        if (b < 0.0) continue;
        float rc = (b + 0.5) * bw;
        float n  = max(1.0, floor(6.2831853 * rc / 9.0));          // stars per ring
        float cell = 6.2831853 / n;
        float ar = a - rot;                                          // angle in the turning sky
        float c0 = floor(ar / cell);
        for (int k = 0; k < 5; k++)                                  // stars just behind, and those ahead whose trails reach here
        {
            float c = c0 - 1.0 + float(k);
            float cid = mod(c, n);
            float h  = hash12(vec2(b, cid));
            float keep = mix(0.6, 0.28, smoothstep(10.0, 40.0, rc));   // sparser far from the pole
            if (h > keep) continue;
            float h2 = hash12(vec2(cid, b) + 7.0);
            float sa = (c + 0.2 + 0.6 * h2) * cell;                  // star's angle
            float sr = rc + (h / keep - 0.5) * 1.4;                  // star's radius
            float behind = (sa - ar) * max(r, 1.0);                  // arc length behind the head
            float head = exp(-behind * behind / 0.8);
            float tail = exp(-max(behind, 0.0) / TRAIL) * 0.42 * smoothstep(-1.0, 1.0, behind);
            float radial = exp(-(r - sr) * (r - sr) / 0.45);
            float mag = 0.35 + 0.65 * hash11(h * 91.0);
            vec3 tint = h2 > 0.8 ? vec3(0.95, 0.72, 0.48) : vec3(0.58, 0.66, 0.92);
            acc += (head + tail) * radial * mag * tint;
        }
    }
    return acc;
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = fragCoord.xy / iResolution.xy;
    vec2 p = vec2(uv.x, 1.0 - uv.y) * MAP_SIZE;      // Visual Map grid coordinates
    float t = iTime;

    vec3 col = vec3(0.006, 0.010, 0.040);
    col += vec3(0.05, 0.028, 0.035) * exp(-max(HILL_Y - p.y, 0.0) / 4.0);   // town glow on the horizon
    col += trails(p, t) * 0.8;

    // ---- shooting star, now and then
    float mt = t / METEOR_SEC;
    float mid = floor(mt);
    float mage = fract(mt) * METEOR_SEC;
    if (hash11(mid * 3.7) < 0.8 && mage < 1.4)
    {
        vec2 h = vec2(hash11(mid * 5.1), hash11(mid * 8.3));
        vec2 start = h.x < 0.5 ? vec2(mix(8.0, 36.0, h.y), 38.0) : vec2(mix(40.0, 52.0, h.y), 4.0);
        vec2 dir = normalize(vec2(-0.8, 0.6));
        vec2 head = start + dir * mage * 14.0;
        vec2 q = p - head;
        float along = dot(q, dir);
        float side = dot(q, vec2(-dir.y, dir.x));
        float streak = exp(-side * side / 0.5) * (along < 0.0 ? exp(along / 3.5) : exp(-along * along / 0.6));
        col += streak * sin(3.14159 * mage / 1.4) * vec3(0.70, 0.75, 0.90);
    }

    // ---- dark hills with a few warm windows on the bottom rows and the mouse
    float hill = HILL_Y + 2.0 * sin(p.x * 0.09 + 1.0) + 1.0 * sin(p.x * 0.23);
    float land = smoothstep(hill - 0.6, hill + 0.6, p.y);
    vec2 wc = floor(p / vec2(2.25, 2.25));
    float window = step(0.93, hash12(wc + 3.0)) * step(hill + 1.5, p.y);
    float glow = 0.75 + 0.25 * sin(t * 0.2 + hash12(wc) * 6.28);
    vec3 ground = vec3(0.008, 0.010, 0.018) + window * glow * vec3(0.42, 0.24, 0.07);
    col = mix(col, ground, land);

    fragColor = vec4(clamp(col * BRIGHTNESS, 0.0, 1.0), 1.0);
}
