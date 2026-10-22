// Décor de la carte en papier découpé (SlimeMap), façon Paper Mario / Candy Crush / Don't Starve : chaque élément est une carte
// plate face à la caméra, posée au sol par son pied, dessinée par distances signées (aplats, ombre et reflet francs, contour
// encré teinté, liseré de papier blanc autour, grain de papier). Sommets : position = pied de la carte dans le monde ;
// couleur = teinte principale ; TEXCOORD0 xy = coin (m, depuis le pied), z = sorte, w = graine ; TEXCOORD1 rgb = teinte
// secondaire, a = épaisseur du trait (m) ; TEXCOORD2 x = balancement, y = variante / bande de nuage, z = sens de sortie
// (nuages), w = échelle (les formes sont dessinées en unités d'échelle). Même courbure du monde que Slime/Map.
Shader "Slime/MapCard"
{
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _SlimeMapBend, _SlimeMapReveal, _SlimeMapFog, _SlimeMapFogRange, _SlimeMapMist;
            float _SlimeMapTime;

            // Sortes (SlimeMap.Card).
            #define K_TREE 1
            #define K_PINE 2
            #define K_BUSH 3
            #define K_FLOWER 4
            #define K_TUFT 5
            #define K_MUSHROOM 6
            #define K_ROCK 7
            #define K_LAMP 8
            #define K_HOUSE 9
            #define K_WINDMILL 10
            #define K_BARN 11
            #define K_HAY 12
            #define K_GATE 13
            #define K_SIGN 14
            #define K_HILL 15
            #define K_CLOUD 16
            #define K_BUTTERFLY 17

            static const float3 PAPER = float3(1.0, 0.985, 0.94);

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float4 p : TEXCOORD0;      // xy forme (unités d'échelle), z sorte, w graine
                float4 second : TEXCOORD1; // rgb teinte 2, a trait (m)
                float4 extra : TEXCOORD2;  // x échelle, y variante, z brume, w brouillard
            };

            float3 Bend(float3 w)
            {
                float dz = max(0.0, w.z - _SlimeMapBend.y);
                w.y -= _SlimeMapBend.x * dz * dz;
                return w;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float kind = v.uv0.z;
                float t = _SlimeMapTime;
                float3 base = TransformObjectToWorld(v.positionOS);
                float2 local = v.uv0.xy;
                float scale = v.uv2.w;
                float mistAllowed = 1.0;
                if (v.uv2.x > 0.0) local.x += v.uv2.x * local.y * sin(t * 1.6 + base.x * 0.9 + base.z * 0.7);
                if (abs(kind - K_CLOUD) < 0.5)
                {
                    float k = saturate(_SlimeMapReveal.y - v.uv2.y);
                    float e = k * k * (3.0 - 2.0 * k);
                    base += float3(v.uv2.z * 8.0 * e, 1.2 * e + 0.08 * sin(t * 0.7 + base.x * 1.3 + base.z), 0.0);
                    local *= 1.0 - e;
                    mistAllowed = 0.0;
                }
                if (abs(kind - K_BUTTERFLY) < 0.5)
                {
                    float s = v.uv0.w * 6.2831;
                    base += float3(sin(t * 0.6 + s) * 0.9, 0.35 + 0.2 * sin(t * 1.7 + s * 2.0), sin(t * 0.45 + s * 1.3) * 0.7);
                }
                float3 b = Bend(base);
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                float3 w = b + right * local.x + up * local.y;
                o.positionCS = TransformWorldToHClip(w);
                o.color = v.color;
                o.p = float4(v.uv0.xy / scale, kind, v.uv0.w);
                o.second = v.uv1;
                float mist = saturate((base.z - _SlimeMapReveal.x) / max(0.01, _SlimeMapReveal.z)) * mistAllowed;
                float dist = distance(_WorldSpaceCameraPos, w);
                float fog = saturate((dist - _SlimeMapFogRange.x) / max(0.01, _SlimeMapFogRange.y - _SlimeMapFogRange.x));
                o.extra = float4(scale, v.uv2.y, mist, fog * fog);
                return o;
            }

            // ---- Distances signées ----

            float Hash(float n) { return frac(sin(n * 127.1 + 311.7) * 43758.5453); }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Circle(float2 p, float2 c, float r) { return length(p - c) - r; }

            float Box(float2 p, float2 c, float2 b, float r)
            {
                float2 d = abs(p - c) - b + r;
                return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - r;
            }

            float Ellipse(float2 p, float2 c, float2 r) { return (length((p - c) / r) - 1.0) * min(r.x, r.y); }

            float Seg(float2 p, float2 a, float2 b, float r)
            {
                float2 pa = p - a, ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h) - r;
            }

            float Tri(float2 p, float2 p0, float2 p1, float2 p2)
            {
                float2 e0 = p1 - p0, e1 = p2 - p1, e2 = p0 - p2;
                float2 v0 = p - p0, v1 = p - p1, v2 = p - p2;
                float2 q0 = v0 - e0 * saturate(dot(v0, e0) / dot(e0, e0));
                float2 q1 = v1 - e1 * saturate(dot(v1, e1) / dot(e1, e1));
                float2 q2 = v2 - e2 * saturate(dot(v2, e2) / dot(e2, e2));
                float s = sign(e0.x * e2.y - e0.y * e2.x);
                float2 d = min(min(float2(dot(q0, q0), s * (v0.x * e0.y - v0.y * e0.x)),
                                   float2(dot(q1, q1), s * (v1.x * e1.y - v1.y * e1.x))),
                                   float2(dot(q2, q2), s * (v2.x * e2.y - v2.y * e2.x)));
                return -sqrt(d.x) * sign(d.y);
            }

            float Trapezoid(float2 p, float2 c, float r1, float r2, float he)
            {
                p -= c;
                float2 k1 = float2(r2, he), k2 = float2(r2 - r1, 2.0 * he);
                p.x = abs(p.x);
                float2 ca = float2(p.x - min(p.x, p.y < 0.0 ? r1 : r2), abs(p.y) - he);
                float2 cb = p - k1 + k2 * saturate(dot(k1 - p, k2) / dot(k2, k2));
                float s = (cb.x < 0.0 && ca.y < 0.0) ? -1.0 : 1.0;
                return s * sqrt(min(dot(ca, ca), dot(cb, cb)));
            }

            float Smin(float a, float b, float k)
            {
                float h = max(k - abs(a - b), 0.0) / k;
                return min(a, b) - h * h * k * 0.25;
            }

            float2 Rot(float2 p, float a)
            {
                float c = cos(a), s = sin(a);
                return float2(c * p.x - s * p.y, s * p.x + c * p.y);
            }

            // ---- Calques (alpha prémultiplié) ----

            float Cover(float d)
            {
                float w = max(fwidth(d), 1e-5);
                return saturate(0.5 - d / w);
            }

            float4 Over(float4 dst, float4 src) { return src + dst * (1.0 - src.a); }

            // Morceau encré : aplat dedans, trait de largeur 'ink' autour.
            float4 Part(float d, float3 fill, float3 ink, float w)
            {
                float a = Cover(d - w);
                return float4(lerp(ink, fill, Cover(d)) * a, a);
            }

            // Liseré de papier blanc autour de toute la découpe (derrière tout le reste).
            float4 Paper(float d, float w) { float a = Cover(d - w * 2.1); return float4(PAPER * a, a); }

            float3 Ink(float3 c) { return c * 0.3 + float3(0.02, 0.01, 0.04); }
            float3 Light(float3 c) { return min(c * 1.18 + 0.07, 1.0); }
            float3 Shade(float3 c) { return c * 0.74; }

            // ---- Éléments ----

            float TreeCanopy(float2 p, float seed, float2 o)
            {
                p -= o;
                float j = Hash(seed) - 0.5, k = Hash(seed + 3.1) - 0.5;
                float d = Circle(p, float2(0.0, 1.35), 0.6);
                d = Smin(d, Circle(p, float2(-0.43 + 0.06 * j, 1.1), 0.42), 0.06);
                d = Smin(d, Circle(p, float2(0.44, 1.13 + 0.05 * k), 0.43), 0.06);
                d = Smin(d, Circle(p, float2(-0.17, 1.8 + 0.04 * k), 0.4), 0.06);
                d = Smin(d, Circle(p, float2(0.23, 1.72), 0.38 + 0.04 * j), 0.06);
                return d;
            }

            float4 Tree(float2 p, float3 c, float3 c2, float seed, float variant, float w)
            {
                float trunk = Trapezoid(p, float2(0.0, 0.42), 0.14, 0.09, 0.42) - 0.02;
                float can = TreeCanopy(p, seed, 0.0);
                float up = TreeCanopy(p, seed, float2(-0.07, 0.13));
                float3 fill = lerp(c, Shade(c), 1.0 - Cover(up));
                float hi = min(Circle(p, float2(-0.25, 1.82), 0.13), Circle(p, float2(-0.52, 1.28), 0.07));
                fill = lerp(fill, Light(c), Cover(hi));
                float3 bark = float3(0.58, 0.37, 0.23);
                float3 barkFill = lerp(bark, Shade(bark), step(0.03, p.x));
                float4 acc = Paper(min(trunk, can), w);
                acc = Over(acc, Part(trunk, barkFill, Ink(bark), w));
                acc = Over(acc, Part(can, fill, Ink(c), w));
                if (variant > 0.5)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        float a = Hash(seed + i * 1.7) * 6.2831;
                        float r = 0.25 + 0.3 * Hash(seed + i * 2.3);
                        float2 fc = float2(0.0, 1.35) + float2(cos(a), sin(a) * 0.8) * r;
                        float f = Circle(p, fc, 0.075);
                        acc = Over(acc, Part(f, lerp(c2, Light(c2), Cover(Circle(p, fc + float2(-0.025, 0.025), 0.025))), Ink(c2), w * 0.7));
                    }
                }
                return acc;
            }

            float4 Pine(float2 p, float3 c, float seed, float w)
            {
                float trunk = Box(p, float2(0.0, 0.2), float2(0.08, 0.22), 0.02);
                float t0 = Tri(p, float2(-0.62, 0.32), float2(0.62, 0.32), float2(0.0, 1.2)) - 0.04;
                float t1 = Tri(p, float2(-0.5, 0.82), float2(0.5, 0.82), float2(0.0, 1.62)) - 0.04;
                float t2 = Tri(p, float2(-0.37, 1.28), float2(0.37, 1.28), float2(0.0, 2.0)) - 0.04;
                float3 bark = float3(0.5, 0.33, 0.22);
                float4 acc = Paper(min(min(trunk, t0), min(t1, t2)), w);
                acc = Over(acc, Part(trunk, bark, Ink(bark), w));
                float3 f0 = lerp(c, Shade(c), step(0.04, p.x));
                acc = Over(acc, Part(t0, f0, Ink(c), w));
                acc = Over(acc, Part(t1, f0, Ink(c), w));
                acc = Over(acc, Part(t2, lerp(f0, Light(c), Cover(Circle(p, float2(-0.08, 1.72), 0.06))), Ink(c), w));
                return acc;
            }

            float4 Bush(float2 p, float3 c, float3 c2, float seed, float variant, float w)
            {
                float j = Hash(seed) - 0.5;
                float d = Circle(p, float2(-0.28, 0.3), 0.3);
                d = Smin(d, Circle(p, float2(0.12 + 0.05 * j, 0.38), 0.36), 0.06);
                d = Smin(d, Circle(p, float2(0.45, 0.25), 0.24), 0.06);
                d = max(d, -p.y + 0.02);
                float up = Smin(Smin(Circle(p, float2(-0.35, 0.42), 0.3), Circle(p, float2(0.05, 0.5), 0.36), 0.06), Circle(p, float2(0.38, 0.37), 0.24), 0.06);
                float3 fill = lerp(c, Shade(c), 1.0 - Cover(up));
                fill = lerp(fill, Light(c), Cover(Circle(p, float2(-0.05, 0.58), 0.09)));
                float4 acc = Paper(d, w);
                acc = Over(acc, Part(d, fill, Ink(c), w));
                if (variant > 0.5)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        float2 bc = float2(-0.35 + 0.25 * i + 0.05 * Hash(seed + i), 0.25 + 0.2 * Hash(seed + i * 3.1));
                        acc = Over(acc, Part(Circle(p, bc, 0.055), lerp(c2, Light(c2), Cover(Circle(p, bc + float2(-0.02, 0.02), 0.018))), Ink(c2), w * 0.7));
                    }
                }
                return acc;
            }

            float4 Flower(float2 p, float3 c, float3 c2, float seed, float w)
            {
                float stem = Seg(p, float2(0.0, 0.0), float2(0.01, 0.24), 0.012);
                float leaf = Ellipse(Rot(p - float2(0.05, 0.1), -0.6), 0.0, float2(0.05, 0.022));
                float3 green = float3(0.3, 0.62, 0.25);
                float2 h = float2(0.01, 0.27);
                float petals = 1e5;
                for (int i = 0; i < 5; i++)
                {
                    float a = i * 1.2566 + seed * 6.0;
                    petals = min(petals, Circle(p, h + float2(cos(a), sin(a)) * 0.048, 0.04));
                }
                float center = Circle(p, h, 0.03);
                float4 acc = Paper(min(min(stem, leaf), petals), w);
                acc = Over(acc, Part(min(stem, leaf), green, Ink(green), w));
                acc = Over(acc, Part(petals, lerp(c, Light(c), Cover(Circle(p, h + float2(-0.03, 0.04), 0.03))), Ink(c), w));
                acc = Over(acc, Part(center, c2, Ink(c2), w));
                return acc;
            }

            float4 Tuft(float2 p, float3 c, float seed, float w)
            {
                float j = Hash(seed) - 0.5;
                float d = Seg(p, float2(-0.05, 0.0), float2(-0.13 + 0.03 * j, 0.17), 0.016);
                d = min(d, Seg(p, float2(0.0, 0.0), float2(0.01, 0.25), 0.018));
                d = min(d, Seg(p, float2(0.05, 0.0), float2(0.13, 0.16 + 0.03 * j), 0.016));
                float3 fill = lerp(Shade(c), Light(c), saturate(p.y / 0.22));
                float4 acc = Paper(d, w);
                return Over(acc, Part(d, fill, Ink(c), w));
            }

            float4 Mushroom(float2 p, float3 c, float w)
            {
                float stem = Box(p, float2(0.0, 0.08), float2(0.045, 0.085), 0.03);
                float cap = max(Ellipse(p, float2(0.0, 0.15), float2(0.16, 0.12)), -(p.y - 0.13));
                float3 cream = float3(0.99, 0.95, 0.86);
                float4 acc = Paper(min(stem, cap), w);
                acc = Over(acc, Part(stem, cream, Ink(float3(0.6, 0.5, 0.4)), w));
                float3 fill = lerp(c, Shade(c), step(0.05, p.x - (p.y - 0.13) * 0.3));
                float dots = min(min(Circle(p, float2(-0.07, 0.19), 0.03), Circle(p, float2(0.04, 0.23), 0.025)), Circle(p, float2(0.09, 0.17), 0.02));
                fill = lerp(fill, float3(1.0, 1.0, 1.0), Cover(dots));
                return Over(acc, Part(cap, fill, Ink(c), w));
            }

            float4 Rock(float2 p, float3 c, float seed, float w)
            {
                float j = Hash(seed) - 0.5;
                float d = Smin(Ellipse(p, float2(-0.08, 0.16), float2(0.24, 0.17)), Ellipse(p, float2(0.14 + 0.05 * j, 0.12), float2(0.18, 0.13)), 0.08);
                d = max(d, -p.y);
                float3 fill = lerp(c, Shade(c), step(0.0, p.x * 0.6 - (p.y - 0.15)));
                fill = lerp(fill, Light(c), Cover(Ellipse(p, float2(-0.12, 0.25), float2(0.07, 0.035))));
                float4 acc = Paper(d, w);
                return Over(acc, Part(d, fill, Ink(c), w));
            }

            float4 Lamp(float2 p, float3 c, float3 c2, float w)
            {
                float pole = Box(p, float2(0.0, 0.6), float2(0.035, 0.6), 0.02);
                float foot = Trapezoid(p, float2(0.0, 0.06), 0.11, 0.06, 0.06);
                float head = Circle(p, float2(0.0, 1.32), 0.12);
                float cap = Trapezoid(p, float2(0.0, 1.47), 0.06, 0.13, 0.05) - 0.01;
                float stripes = step(0.5, frac((p.y + p.x * 1.5) * 6.0));
                float3 poleFill = lerp(float3(1.0, 1.0, 1.0), c, stripes);
                float4 acc = Paper(min(min(pole, foot), min(head, cap)), w);
                acc = Over(acc, Part(foot, c, Ink(c), w));
                acc = Over(acc, Part(pole, poleFill, Ink(c), w));
                float glow = 0.6 + 0.4 * sin(_SlimeMapTime * 2.0 + p.x);
                acc = Over(acc, Part(head, lerp(c2 * 1.3, float3(1.4, 1.35, 1.1), Cover(Circle(p, float2(-0.03, 1.36), 0.05))) * (0.9 + 0.2 * glow), Ink(c2), w));
                acc = Over(acc, Part(cap, c, Ink(c), w));
                return acc;
            }

            float4 House(float2 p, float3 c, float3 c2, float w)
            {
                float walls = Box(p, float2(0.0, 0.45), float2(0.62, 0.45), 0.04);
                float roof = Trapezoid(p, float2(0.0, 1.12), 0.8, 0.12, 0.3) - 0.05;
                float chimney = Box(p, float2(0.4, 1.38), float2(0.09, 0.2), 0.02);
                float door = min(Box(p, float2(-0.22, 0.24), float2(0.15, 0.24), 0.0), Circle(p, float2(-0.22, 0.48), 0.15));
                float win = Circle(p, float2(0.3, 0.52), 0.15);
                float3 wall = float3(0.99, 0.93, 0.8);
                float4 acc = Paper(min(min(walls, roof), chimney), w);
                acc = Over(acc, Part(chimney, float3(0.75, 0.45, 0.38), Ink(float3(0.6, 0.3, 0.25)), w));
                float3 wf = lerp(wall, Shade(wall), step(0.35, p.x));
                acc = Over(acc, Part(walls, wf, Ink(float3(0.6, 0.45, 0.3)), w));
                float shingles = step(0.5, frac(p.y * 7.0 + step(0.5, frac(p.x * 3.0)) * 0.5));
                float3 rf = lerp(c, Shade(c), shingles * 0.4 + step(0.15, p.x) * 0.3);
                acc = Over(acc, Part(roof, rf, Ink(c), w));
                acc = Over(acc, Part(door, float3(0.6, 0.36, 0.22), Ink(float3(0.6, 0.36, 0.22)), w));
                acc = Over(acc, Part(Circle(p, float2(-0.12, 0.25), 0.025), float3(1.0, 0.85, 0.3), Ink(float3(0.6, 0.36, 0.22)), w * 0.5));
                float3 glass = lerp(c2, Light(c2), step(p.y - 0.52, -(p.x - 0.3)) * 0.6);
                acc = Over(acc, Part(win, glass, Ink(float3(0.5, 0.35, 0.25)), w));
                acc = Over(acc, Part(min(Box(p, float2(0.3, 0.52), float2(0.15, 0.012), 0.0), Box(p, float2(0.3, 0.52), float2(0.012, 0.15), 0.0)), float3(1.0, 1.0, 1.0), float3(1.0, 1.0, 1.0), 0.0));
                return acc;
            }

            float4 Windmill(float2 p, float3 c, float3 c2, float w)
            {
                float tower = Trapezoid(p, float2(0.0, 0.8), 0.48, 0.3, 0.8) - 0.03;
                float roof = Tri(p, float2(-0.42, 1.55), float2(0.42, 1.55), float2(0.0, 2.15)) - 0.04;
                float door = min(Box(p, float2(0.0, 0.2), float2(0.13, 0.2), 0.0), Circle(p, float2(0.0, 0.4), 0.13));
                float win = Circle(p, float2(0.0, 1.0), 0.1);
                float2 hub = float2(0.0, 1.55);
                float2 q = Rot(p - hub, -_SlimeMapTime * 0.8);
                float sails = 1e5, arms = 1e5;
                for (int i = 0; i < 4; i++)
                {
                    float2 r = Rot(q, i * 1.5708);
                    arms = min(arms, Box(r, float2(0.0, 0.62), float2(0.03, 0.62), 0.01));
                    sails = min(sails, Box(r, float2(0.13, 0.75), float2(0.1, 0.45), 0.02));
                }
                float hubD = Circle(p, hub, 0.07);
                float3 stone = float3(0.98, 0.94, 0.86);
                float4 acc = Paper(min(min(tower, roof), min(sails, arms)), w);
                acc = Over(acc, Part(tower, lerp(stone, Shade(stone), step(0.12, p.x)), Ink(float3(0.55, 0.45, 0.35)), w));
                acc = Over(acc, Part(door, float3(0.55, 0.34, 0.22), Ink(float3(0.55, 0.34, 0.22)), w));
                acc = Over(acc, Part(win, float3(0.6, 0.85, 1.0), Ink(float3(0.5, 0.4, 0.3)), w));
                acc = Over(acc, Part(roof, lerp(c, Shade(c), step(0.0, p.x)), Ink(c), w));
                float lattice = step(0.5, frac(dot(Rot(q, 0.0), float2(7.0, 7.0))));
                acc = Over(acc, Part(sails, lerp(c2, Shade(c2), lattice * 0.15), Ink(float3(0.55, 0.42, 0.3)), w));
                acc = Over(acc, Part(arms, float3(0.6, 0.4, 0.28), Ink(float3(0.6, 0.4, 0.28)), w));
                acc = Over(acc, Part(hubD, float3(0.5, 0.32, 0.22), Ink(float3(0.5, 0.32, 0.22)), w));
                return acc;
            }

            float4 Barn(float2 p, float3 c, float w)
            {
                float walls = Box(p, float2(0.0, 0.5), float2(0.65, 0.5), 0.03);
                float roof = Trapezoid(p, float2(0.0, 1.18), 0.82, 0.3, 0.26) - 0.04;
                float door = Box(p, float2(0.0, 0.33), float2(0.24, 0.33), 0.02);
                float cross = min(Seg(p, float2(-0.2, 0.05), float2(0.2, 0.6), 0.025), Seg(p, float2(-0.2, 0.6), float2(0.2, 0.05), 0.025));
                float4 acc = Paper(min(walls, roof), w);
                acc = Over(acc, Part(walls, lerp(c, Shade(c), step(0.4, p.x)), Ink(c), w));
                acc = Over(acc, Part(roof, float3(0.45, 0.33, 0.32), Ink(float3(0.45, 0.33, 0.32)), w));
                acc = Over(acc, Part(door, Shade(c), float3(1.0, 1.0, 1.0), w * 1.2));
                acc = Over(acc, Part(cross, float3(1.0, 1.0, 1.0), float3(1.0, 1.0, 1.0), 0.0));
                return acc;
            }

            float4 Hay(float2 p, float3 c, float w)
            {
                float d = Circle(p, float2(0.0, 0.3), 0.3);
                float r = length(p - float2(0.0, 0.3));
                float rings = step(0.5, frac(r * 14.0));
                float4 acc = Paper(d, w);
                return Over(acc, Part(d, lerp(c, Shade(c), rings * 0.35 + step(0.1, p.x) * 0.15), Ink(c), w));
            }

            float4 Gate(float2 p, float3 c, float3 c2, float w)
            {
                float pillars = min(Box(p, float2(-0.95, 0.75), float2(0.2, 0.75), 0.04), Box(p, float2(0.95, 0.75), float2(0.2, 0.75), 0.04));
                float arch = max(abs(length(p - float2(0.0, 1.3)) - 0.9) - 0.15, -(p.y - 1.3));
                float balls = min(Circle(p, float2(-0.95, 1.62), 0.13), Circle(p, float2(0.95, 1.62), 0.13));
                float flag = Tri(p, float2(0.0, 2.2), float2(0.0, 2.55), float2(0.4, 2.38)) - 0.02;
                float pole = Box(p, float2(0.0, 2.2), float2(0.02, 0.38), 0.0);
                float4 acc = Paper(min(min(pillars, arch), min(balls, flag)), w);
                float bricks = step(0.92, frac(p.y * 5.0)) + step(0.94, frac(p.x * 3.0 + floor(p.y * 5.0) * 0.5));
                acc = Over(acc, Part(min(pillars, arch), lerp(c, Shade(c), saturate(bricks)), Ink(c), w));
                acc = Over(acc, Part(balls, float3(1.0, 0.8, 0.3), Ink(float3(1.0, 0.8, 0.3)), w));
                acc = Over(acc, Part(pole, float3(0.5, 0.35, 0.25), Ink(float3(0.5, 0.35, 0.25)), w * 0.6));
                acc = Over(acc, Part(flag, c2, Ink(c2), w));
                return acc;
            }

            float4 Sign(float2 p, float3 c, float3 c2, float w)
            {
                float post = Box(p, float2(0.0, 0.35), float2(0.05, 0.35), 0.01);
                float board = Box(p, float2(0.0, 0.68), float2(0.38, 0.2), 0.05);
                float face = Circle(p, float2(0.0, 0.66), 0.11);
                float4 acc = Paper(min(post, board), w);
                acc = Over(acc, Part(post, Shade(c), Ink(c), w));
                acc = Over(acc, Part(board, lerp(c, Light(c), step(0.0, p.y - 0.76)), Ink(c), w));
                acc = Over(acc, Part(max(face, 0.6 - p.y), c2, Ink(c2), w * 0.8));
                float eyes = min(Circle(p, float2(-0.04, 0.7), 0.017), Circle(p, float2(0.04, 0.7), 0.017));
                acc = Over(acc, Part(eyes, Ink(c2), Ink(c2), 0.0));
                return acc;
            }

            float4 Hill(float2 p, float3 c, float seed, float w)
            {
                float j = Hash(seed) - 0.5;
                float d = max(Ellipse(p, float2(0.0, 0.0), float2(1.0, 0.62)), -p.y - 0.05);
                d = Smin(d, max(Ellipse(p, float2(0.45 + 0.2 * j, 0.0), float2(0.6, 0.45)), -p.y - 0.05), 0.08);
                float up = max(Ellipse(p, float2(-0.06, 0.06), float2(0.98, 0.6)), -p.y);
                float3 fill = lerp(c, Shade(c), 1.0 - Cover(up));
                float strokes = 1e5;
                for (int i = 0; i < 4; i++)
                {
                    float2 sc = float2(-0.6 + 0.4 * i + 0.1 * Hash(seed + i), 0.18 + 0.25 * Hash(seed + i * 2.7));
                    strokes = min(strokes, Seg(p, sc, sc + float2(0.03, 0.07), 0.01));
                }
                fill = lerp(fill, Shade(c) * 0.9, Cover(strokes));
                fill = lerp(fill, Light(c), Cover(Ellipse(p, float2(-0.35, 0.5), float2(0.18, 0.04))));
                float4 acc = Paper(d, w);
                return Over(acc, Part(d, fill, Ink(c), w));
            }

            float4 Cloud(float2 p, float3 c, float seed, float w)
            {
                float d = 1e5;
                for (int i = 0; i < 5; i++)
                {
                    float x = -0.75 + 0.375 * i + 0.1 * (Hash(seed + i) - 0.5);
                    float r = 0.32 + 0.22 * Hash(seed + i * 1.9) + (i == 2 ? 0.12 : 0.0);
                    d = Smin(d, Circle(p, float2(x, 0.25 + r * 0.6), r), 0.1);
                }
                d = Smin(d, Box(p, float2(0.0, 0.18), float2(0.9, 0.18), 0.17), 0.1);
                float3 shade = c * float3(0.82, 0.82, 0.95);
                float3 fill = lerp(shade, c, smoothstep(0.25, 0.5, p.y));
                fill = lerp(fill, float3(1.0, 1.0, 1.0), Cover(Circle(p, float2(-0.3, 0.75), 0.12)));
                float4 acc = Paper(d, w);
                return Over(acc, Part(d, fill, float3(0.6, 0.64, 0.84), w));
            }

            float4 Butterfly(float2 p, float3 c, float seed, float w)
            {
                float flap = 0.3 + 0.7 * abs(sin(_SlimeMapTime * 11.0 + seed * 20.0));
                float2 q = float2(abs(p.x) / max(0.15, flap), p.y);
                float wings = min(Ellipse(q, float2(0.08, 0.12), float2(0.08, 0.065)), Ellipse(q, float2(0.06, 0.05), float2(0.06, 0.045)));
                float body = Ellipse(p, float2(0.0, 0.08), float2(0.015, 0.06));
                float4 acc = Part(wings, lerp(c, Light(c), Cover(Circle(q, float2(0.09, 0.13), 0.02))), Ink(c), w);
                return Over(acc, Part(body, float3(0.25, 0.15, 0.2), float3(0.25, 0.15, 0.2), 0.0));
            }

            float4 frag(Varyings i) : SV_Target
            {
                float2 p = i.p.xy;
                int kind = (int)(i.p.z + 0.5);
                float seed = i.p.w;
                float s = i.extra.x;
                float w = i.second.a / s;
                float3 c = i.color.rgb, c2 = i.second.rgb;
                float variant = i.extra.y;
                float4 col = 0.0;
                if (kind == K_TREE) col = Tree(p, c, c2, seed, variant, w);
                else if (kind == K_PINE) col = Pine(p, c, seed, w);
                else if (kind == K_BUSH) col = Bush(p, c, c2, seed, variant, w);
                else if (kind == K_FLOWER) col = Flower(p, c, c2, seed, w);
                else if (kind == K_TUFT) col = Tuft(p, c, seed, w);
                else if (kind == K_MUSHROOM) col = Mushroom(p, c, w);
                else if (kind == K_ROCK) col = Rock(p, c, seed, w);
                else if (kind == K_LAMP) col = Lamp(p, c, c2, w);
                else if (kind == K_HOUSE) col = House(p, c, c2, w);
                else if (kind == K_WINDMILL) col = Windmill(p, c, c2, w);
                else if (kind == K_BARN) col = Barn(p, c, w);
                else if (kind == K_HAY) col = Hay(p, c, w);
                else if (kind == K_GATE) col = Gate(p, c, c2, w);
                else if (kind == K_SIGN) col = Sign(p, c, c2, w);
                else if (kind == K_HILL) col = Hill(p, c, seed, w);
                else if (kind == K_CLOUD) col = Cloud(p, c, seed, w);
                else col = Butterfly(p, c, seed, w);
                clip(col.a - 0.01);
                float grain = Hash21(floor(p * s * 90.0)) - 0.5;
                col.rgb *= 1.0 + grain * 0.05;
                col.rgb = lerp(col.rgb, _SlimeMapMist.rgb * col.a, i.extra.z * _SlimeMapMist.a);
                col.rgb = lerp(col.rgb, _SlimeMapFog.rgb * col.a, i.extra.w);
                return col;
            }
            ENDHLSL
        }
    }
}
