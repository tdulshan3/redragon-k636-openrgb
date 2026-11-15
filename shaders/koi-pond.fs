// Koi Pond - OpenRGB Effects "Shaders" effect, laid out for the "dexmap"
// Visual Map (keyboard bottom-left, mouse beside it, RAM inside the D_LED ring).
//
// The whole desk is a pond seen from above. Sunlight ripples over the pond
// floor, koi glide in long lazy loops across the keyboard, a small koi patrols
// up and down the RAM sticks and another circles the D_LED ring around them.
// Now and then a raindrop lands and a soft ring spreads over the keys. Two
// lily pads drift so slowly you only notice they moved after a few minutes.
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
#define RAM_MID_Y  20.8                 // vertical middle of the RAM sticks
#define RING_POS   vec2(56.8, 19.0)     // centre of the D_LED ring
#define RING_R     5.6                  // radius of the D_LED ring

// ---- tuning ------------------------------------------------------------------
#define FISH_SPEED 1.0                  // multiplies every koi's swimming speed
#define CAUSTICS   1.0                  // light on the pond floor (0 = off)
#define RAIN       1.0                  // raindrop ripples (0 = off)
#define LILY_PADS  1                    // 0 = no lily pads
#define BRIGHTNESS 1.0
// ------------------------------------------------------------------------------

float hash11(float p)
{
    p = fract(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return fract(p);
}

vec2 hash21(float p)
{
    vec3 p3 = fract(vec3(p) * vec3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.xx + p3.yz) * p3.zy);
}

vec2 hash22(vec2 p)
{
    vec3 p3 = fract(vec3(p.xyx) * vec3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.xx + p3.yz) * p3.zy);
}

// Soft network of light on the pond floor: edges of slowly morphing cells.
float caustics(vec2 p, float t)
{
    p += 1.6 * vec2(sin(p.y * 0.21 + t * 0.23), cos(p.x * 0.17 + t * 0.19));
    p /= 7.5;
    vec2 i = floor(p);
    vec2 f = fract(p);
    float d1 = 8.0;
    float d2 = 8.0;
    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            vec2 o = vec2(float(x), float(y));
            vec2 h = hash22(i + o);
            vec2 pt = o + 0.5 + 0.42 * sin(t * (0.22 + 0.18 * h) + 6.2831 * h);
            float d = length(pt - f);
            if (d < d1) { d2 = d1; d1 = d; }
            else if (d < d2) { d2 = d; }
        }
    }
    return 1.0 - smoothstep(0.0, 0.30, d2 - d1);
}

// Closed swimming lane: two straights joined by half circles, walked by arc length.
vec2 stadium(float s, float hl, float r)
{
    float per = 4.0 * hl + 6.2831853 * r;
    s = mod(s, per);
    if (s < 2.0 * hl) return vec2(-hl + s, r);
    s -= 2.0 * hl;
    float semi = 3.14159265 * r;
    if (s < semi) { float a = s / r; return vec2(hl + r * sin(a), r * cos(a)); }
    s -= semi;
    if (s < 2.0 * hl) return vec2(hl - s, -r);
    s -= 2.0 * hl;
    float a2 = s / r;
    return vec2(-hl - r * sin(a2), -r * cos(a2));
}

// Where koi number `id` is when it has swum `s` grid units along its lane.
vec2 lane(int id, float s, float t)
{
    if (id == 0)        // big kohaku, loops across the whole keyboard
    {
        vec2 q = stadium(s, 13.5, 3.8);
        q.x *= 1.0 + 0.12 * sin(t * 0.021);
        q.y += 0.9 * sin(s * 0.29);
        return 0.5 * (KB_MIN + KB_MAX) + vec2(2.5 * sin(t * 0.017), 0.5 * sin(t * 0.013)) + q;
    }
    if (id == 1)        // gold ogon, wider loop that sometimes reaches the mouse
    {
        vec2 q = stadium(s, 15.0, 3.3);
        q.x *= 1.0 + 0.10 * sin(t * 0.019 + 2.0);
        q.y += 1.0 * sin(s * 0.23 + 1.0);
        return vec2(24.0 + 4.0 * sin(t * 0.011 + 1.0), 50.2) + q;
    }
    if (id == 2)        // small orange koi, down one RAM stick and up the other
    {
        float r = 0.5 * (RAM_R_X - RAM_L_X);
        vec2 q = stadium(s, 7.0, r);
        return vec2(0.5 * (RAM_L_X + RAM_R_X), RAM_MID_Y) + vec2(q.y, q.x);
    }
    float a = s / (RING_R - 0.4);    // silver koi circling along the D_LED ring
    float rr = RING_R - 0.2 + 0.4 * sin(s * 0.12);
    return RING_POS + rr * vec2(cos(a), sin(a));
}

vec2 segDist(vec2 p, vec2 a, vec2 b)
{
    vec2 pa = p - a;
    vec2 ba = b - a;
    float h = clamp(dot(pa, ba) / max(dot(ba, ba), 1e-6), 0.0, 1.0);
    return vec2(length(pa - ba * h), h);
}

// Half-width of a koi at u (0 = nose, 1 = tail tip), for a 9-unit fish.
float koiWidth(float u)
{
    float body = 1.30 * sin(3.14159 * pow(clamp(u / 0.86, 0.0, 1.0), 0.72));
    float tail = 0.95 * smoothstep(0.80, 1.0, u);
    return max(body, tail) + 0.12;
}

vec3 koiColour(int id, float u)
{
    if (id == 0)        // kohaku: white with red patches and a red head
    {
        float patch = smoothstep(-0.10, 0.60, sin(u * 10.5 + 1.2));
        patch = max(patch, 1.0 - smoothstep(0.14, 0.24, u));
        patch *= 1.0 - smoothstep(0.72, 0.82, u);
        return mix(vec3(0.82, 0.78, 0.72), vec3(0.95, 0.24, 0.05), patch);
    }
    if (id == 1) return mix(vec3(1.00, 0.64, 0.16), vec3(0.95, 0.80, 0.45), smoothstep(0.75, 1.0, u));
    if (id == 2) return mix(vec3(1.00, 0.40, 0.06), vec3(0.85, 0.80, 0.74), smoothstep(0.70, 0.95, u));
    return mix(vec3(0.74, 0.80, 0.86), vec3(0.95, 0.70, 0.30), 1.0 - smoothstep(0.10, 0.25, u));
}

// Draws one koi: rgb = colour, a = coverage. `shadow` = its shadow on the floor.
vec4 koi(int id, vec2 p, float t, out float shadow)
{
    float len  = 9.0;
    float spd  = 3.0;
    float off  = 0.0;
    if (id == 1) { len = 8.0; spd = -2.6; off = 40.0; }
    if (id == 2) { len = 6.5; spd = 2.1;  off = 5.0; }
    if (id == 3) { len = 5.5; spd = -1.8; off = 17.0; }

    float dir  = sign(spd);
    float head = FISH_SPEED * spd * t + off;
    float seed = float(id) * 1.37;
    float k    = len / 9.0;
    vec2  so   = vec2(1.1, 0.9);              // light from the upper left

    float best  = 1e3;
    float bestS = 1e3;
    vec3  csum  = vec3(0.0);
    float wsum  = 0.0;
    float fin   = 1e3;
    vec2  prev  = vec2(0.0);
    float u0    = 0.0;

    for (int j = 0; j <= 8; j++)
    {
        float u = float(j) / 8.0;
        float s = head - dir * u * len;
        vec2 a = lane(id, s + 0.3 * dir, t);
        vec2 b = lane(id, s - 0.3 * dir, t);
        vec2 d = normalize(a - b + 1e-5);
        vec2 n = vec2(-d.y, d.x);
        float wig = 0.5 * u * u * sin(u * 5.5 - t * 2.4 + seed * 6.2831);
        vec2 cur = 0.5 * (a + b) + n * wig;
        if (j > 0)
        {
            vec2 sd = segDist(p, prev, cur);
            float uu = mix(u0, u, sd.y);
            float e = sd.x - koiWidth(uu) * k;
            best = min(best, e);
            float wgt = 1.0 - smoothstep(-0.7, 0.7, e);     // blend the pattern between segments
            csum += wgt * koiColour(id, uu);
            wsum += wgt;
            float fw = 1.05 * k * exp(-pow((uu - 0.30) / 0.07, 2.0));
            fin = min(fin, sd.x - (koiWidth(uu) * k + fw));
            vec2 ss = segDist(p - so, prev, cur);
            bestS = min(bestS, ss.x - koiWidth(mix(u0, u, ss.y)) * k);
        }
        prev = cur;
        u0 = u;
    }

    float body = 1.0 - smoothstep(-0.45, 0.45, best);
    float fins = 0.35 * (1.0 - smoothstep(-0.4, 0.4, fin));
    float cover = max(body, fins);

    // koi slowly sink and rise, so they fade a little in and out of view
    float vis = 0.82 + 0.18 * smoothstep(-0.4, 0.4, sin(t * 0.045 + seed * 4.0));
    shadow = (1.0 - smoothstep(-0.6, 1.4, bestS)) * vis;

    vec3 c = wsum > 1e-4 ? csum / wsum : koiColour(id, 0.5);
    c *= 0.82 + 0.18 * (1.0 - smoothstep(-1.2, 0.0, best));     // brighter along the spine
    return vec4(c, cover * vis);
}

float ripples(vec2 p, float t)
{
    float acc = 0.0;
    for (int k = 0; k < 3; k++)
    {
        float fk  = float(k);
        float per = 4.2 + 1.3 * fk;
        float tk  = t / per + 0.31 * fk;
        float id  = floor(tk);
        float age = fract(tk) * per;
        vec2 h  = hash21(id * 1.37 + fk * 11.1);
        vec2 r2 = hash21(id * 2.71 + fk * 5.3);
        if (h.y < 0.3) continue;                  // this slot skips a drop
        vec2 pos = mix(KB_MIN, KB_MAX, r2);
        if (h.x > 0.72) pos = vec2(mix(RAM_L_X - 2.0, RING_POS.x + RING_R, r2.x), RAM_MID_Y + (r2.y - 0.5) * 18.0);
        if (h.x > 0.90) pos = MOUSE_POS + (r2 - 0.5) * 3.0;
        float rad = 0.5 + age * 4.2;
        float d  = length(p - pos) - rad;
        float d2 = d + 2.0;
        float ring = exp(-d * d / 0.8) - 0.55 * exp(-d2 * d2 / 0.8);
        acc += ring * exp(-age * 1.0) * smoothstep(0.0, 0.5, age);
    }
    return acc;
}

vec4 lilyPad(vec2 p, float t, float i)
{
    vec2 c = KB_MIN + (KB_MAX - KB_MIN) * vec2(0.5 + 0.44 * sin(t * 0.0093 + i * 2.4),
                                               0.5 + 0.30 * sin(t * 0.0141 + i * 1.7));
    vec2 q = p - c;
    float r = 2.5 - 0.35 * i;
    float d = length(q);
    float a = atan(q.y, q.x + 1e-4) - (t * 0.02 + i * 2.0);
    a = mod(a + 3.14159, 6.28318) - 3.14159;
    float notch = smoothstep(0.16, 0.30, abs(a));
    float pad = (1.0 - smoothstep(r - 0.5, r + 0.4, d)) * mix(1.0, notch, smoothstep(0.25, 0.9, d / r));
    vec3 col = mix(vec3(0.07, 0.24, 0.07), vec3(0.03, 0.14, 0.04), smoothstep(0.0, r, d));
    if (i < 0.5)    // a lotus flower sits on the first pad
    {
        float f = 1.0 - smoothstep(0.5, 1.3, length(q - vec2(0.6, -0.4)));
        col = mix(col, vec3(0.85, 0.36, 0.52), f);
        pad = max(pad, f);
    }
    return vec4(col, pad);
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = fragCoord.xy / iResolution.xy;
    vec2 p = vec2(uv.x, 1.0 - uv.y) * MAP_SIZE;      // Visual Map grid coordinates
    float t = iTime;

    float c = caustics(p, t);
    vec3 col = vec3(0.02, 0.12, 0.14) + CAUSTICS * c * vec3(0.04, 0.14, 0.13);

    float s0; float s1; float s2; float s3;
    vec4 k0 = koi(0, p, t, s0);
    vec4 k1 = koi(1, p, t, s1);
    vec4 k2 = koi(2, p, t, s2);
    vec4 k3 = koi(3, p, t, s3);
    col *= 1.0 - 0.45 * max(max(s0, s1), max(s2, s3));
    float lit = 0.85 + 0.35 * c * CAUSTICS;
    col = mix(col, k1.rgb * lit, k1.a);
    col = mix(col, k0.rgb * lit, k0.a);
    col = mix(col, k2.rgb * lit, k2.a);
    col = mix(col, k3.rgb * lit, k3.a);

    col += RAIN * ripples(p, t) * vec3(0.05, 0.15, 0.15);

#if LILY_PADS
    vec4 lp = lilyPad(p, t, 0.0);
    col = mix(col, lp.rgb, lp.a);
    lp = lilyPad(p, t, 1.0);
    col = mix(col, lp.rgb, lp.a);
#endif

    fragColor = vec4(clamp(col * BRIGHTNESS, 0.0, 1.0), 1.0);
}
