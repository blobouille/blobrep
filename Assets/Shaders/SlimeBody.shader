// Corps des slimes (SlimeSkin) : un seul maillage pour tous les slimes, reconstruit à chaque image. Tout est dans les sommets :
// TEXCOORD0 xy = repère du décor (rayon 1, suit la déformation), z = partie, w = bord ; TEXCOORD1 xy = repère de la lumière (même
// forme, tourné vers le haut du monde), z = éclair, w = motif ; TEXCOORD2 = regard, clignement, expression ; TEXCOORD3 = ombre +
// style de visage ; TEXCOORD4 = accent + lueur ; TEXCOORD5 = graine, taille du visage, bord. Parties : 0 corps (motif + visage),
// 1 liseré du contour, 2 accessoire, 3 anneau lumineux. Couleurs > 1 = lueur (bloom).
Shader "Slime/Body"
{
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                float4 uv3 : TEXCOORD3;
                float4 uv4 : TEXCOORD4;
                float4 uv5 : TEXCOORD5;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float4 deco : TEXCOORD0;
                float4 light : TEXCOORD1;
                float4 face : TEXCOORD2;
                float4 shade : TEXCOORD3;
                float4 accent : TEXCOORD4;
                float4 misc : TEXCOORD5;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.color = v.color;
                o.deco = v.uv0;
                o.light = v.uv1;
                o.face = v.uv2;
                o.shade = v.uv3;
                o.accent = v.uv4;
                o.misc = v.uv5;
                return o;
            }

            static const float TAU = 6.2831853;

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 Hash22(float2 p)
            {
                float n = Hash21(p);
                return float2(n, Hash21(p + n * 17.17));
            }

            // Couverture anticrénelée d'une distance signée (< 0 dedans).
            float Fill(float d)
            {
                float w = max(fwidth(d), 1e-5);
                return saturate(0.5 - d / w);
            }

            float SdEllipse(float2 p, float2 r)
            {
                float k0 = length(p / r);
                float k1 = length(p / (r * r));
                return k0 * (k0 - 1.0) / max(k1, 1e-5);
            }

            float SdSegment(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a, ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h);
            }

            // Arc centré sur l'axe +y (ouverture 'aperture' rad de chaque côté), rayon ra, épaisseur rb.
            float SdArc(float2 p, float aperture, float ra, float rb)
            {
                float2 sc = float2(sin(aperture), cos(aperture));
                p.x = abs(p.x);
                return ((sc.y * p.x > sc.x * p.y) ? length(p - sc * ra) : abs(length(p) - ra)) - rb;
            }

            float3 Voronoi(float2 x)
            {
                float2 n = floor(x), f = frac(x);
                float md = 8.0, md2 = 8.0, id = 0.0;
                for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    float2 g = float2(i, j);
                    float2 r = g + Hash22(n + g) - f;
                    float d = dot(r, r);
                    if (d < md) { md2 = md; md = d; id = Hash21(n + g); }
                    else if (d < md2) md2 = d;
                }
                return float3(sqrt(md), sqrt(md2) - sqrt(md), id);
            }

            float3 Hue(float h)
            {
                return saturate(abs(frac(h + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0);
            }

            // Étincelles en grille : 'scale' cellules par unité, une étoile à 4 branches par cellule, qui scintille.
            float Glitter(float2 p, float scale, float density, float seed, float t)
            {
                float2 g = p * scale;
                float2 cell = floor(g);
                float h = Hash21(cell + seed);
                if (h > density) return 0.0;
                float2 q = frac(g) - 0.25 - 0.5 * Hash22(cell + seed * 3.1);
                float tw = pow(saturate(sin(t * (2.0 + 3.0 * h) + h * 40.0)), 6.0);
                float s = exp(-length(q) * 14.0) + exp(-abs(q.x) * 70.0 - abs(q.y) * 9.0) + exp(-abs(q.y) * 70.0 - abs(q.x) * 9.0);
                return tw * s;
            }

            float Hex(float2 p)
            {
                p = abs(p);
                return max(dot(p, normalize(float2(1.0, 1.7320508))), p.x);
            }

            // Motif dans le repère du décor. Renvoie la couleur modifiée.
            float3 Pattern(int pattern, float2 p, float3 col, float3 shade, float3 accent, float glow, float seed, float faceMask, float t)
            {
                float r = length(p);
                if (pattern == 1) // bulles qui montent
                {
                    for (int i = 0; i < 6; i++)
                    {
                        float h = Hash21(float2(i, seed * 7.0));
                        float2 c = float2(h * 1.3 - 0.65, frac(Hash21(float2(seed, i)) + t * (0.06 + 0.05 * h)) * 2.0 - 1.0);
                        float rad = 0.05 + 0.06 * frac(h * 13.0);
                        float d = length(p - c);
                        float fade = smoothstep(1.0, 0.6, abs(c.y)) * smoothstep(0.95, 0.7, length(c));
                        float ring = Fill(abs(d - rad) - 0.014) * 0.7 + Fill(d - rad) * 0.15 + Fill(length(p - c - rad * float2(-0.35, 0.35)) - rad * 0.3);
                        col = lerp(col, accent, saturate(ring) * fade * 0.8);
                    }
                }
                else if (pattern == 2) // pois
                {
                    float2 g = p * 3.2 + seed;
                    float2 cell = floor(g);
                    float2 q = frac(g) - 0.5 - 0.3 * (Hash22(cell) - 0.5);
                    float on = step(0.3, Hash21(cell + 3.7));
                    float rad = 0.17 + 0.1 * Hash21(cell + 1.3);
                    col = lerp(col, accent, Fill(length(q) - rad) * on * lerp(0.25, 0.6, faceMask));
                }
                else if (pattern == 3) // ondes lumineuses
                {
                    float w = sin(r * 15.0 - t * 3.5) * 0.5 + 0.5;
                    float band = smoothstep(0.65, 0.95, w) * smoothstep(1.0, 0.25, r);
                    float core = exp(-r * r * 9.0);
                    col = lerp(col, accent, band * 0.45 * lerp(0.4, 1.0, faceMask));
                    col += accent * (core * 0.6 + band * 0.25) * glow;
                }
                else if (pattern == 4) // alvéoles de miel
                {
                    float2 g = p * 4.2 + seed;
                    float2 s = float2(1.0, 1.7320508);
                    float2 a = (frac(g / s) - 0.5) * s;
                    float2 b = (frac((g - s * 0.5) / s) - 0.5) * s;
                    float2 q = dot(a, a) < dot(b, b) ? a : b;
                    float edge = 0.5 - Hex(q);
                    float cellEdge = Fill(edge - 0.035);
                    col = lerp(col, accent, cellEdge * 0.35 * lerp(0.35, 1.0, faceMask));
                    col += accent * glow * 0.15 * (1.0 - cellEdge) * smoothstep(0.2, -0.6, p.y);
                }
                else if (pattern == 5) // rayures du tigre, du bord vers le centre
                {
                    float a = atan2(p.x, p.y);
                    float s = sin(a * 9.0 + sin(r * 7.0 + seed) * 0.8 + seed) * 0.5 + 0.5;
                    float thr = lerp(0.25, 1.05, smoothstep(0.95, 0.4, r));
                    float belly = smoothstep(-0.55, -0.2, p.y + 0.15 * abs(p.x));
                    float stripe = smoothstep(thr, thr + 0.06, s) * belly;
                    col = lerp(col, accent, stripe * 0.9);
                }
                else if (pattern == 6) // fissures de braise
                {
                    float3 v = Voronoi(p * 3.3 + seed);
                    float crack = 1.0 - smoothstep(0.015, 0.075, v.y);
                    float pulse = 0.65 + 0.35 * sin(t * 3.0 + v.z * 6.0 + r * 4.0);
                    col = lerp(col, shade, smoothstep(0.35, 0.0, v.y) * 0.35);
                    col = lerp(col, accent * (1.0 + glow * pulse), crack * lerp(0.5, 1.0, faceMask));
                    col += accent * glow * 0.12 * pulse * exp(-r * r * 3.0);
                }
                else if (pattern == 7) // paillettes
                {
                    float s = Glitter(p, 7.0, 0.45, seed, t) + Glitter(p + 0.37, 11.0, 0.35, seed + 2.0, t * 1.3) * 0.6;
                    col += accent * s * (1.0 + glow);
                }
                else if (pattern == 8) // facettes de cristal
                {
                    float3 v = Voronoi(p * 2.6 + seed);
                    col *= 0.88 + 0.24 * v.z;
                    col = lerp(col, accent, (1.0 - smoothstep(0.0, 0.05, v.y)) * 0.35);
                    float sheen = smoothstep(0.92, 1.0, sin((p.x + p.y) * 3.0 - t * 1.6) * 0.5 + 0.5);
                    col += accent * sheen * 0.35 * (1.0 + glow);
                }
                else if (pattern == 9) // galaxie
                {
                    float a = atan2(p.y, p.x);
                    float arms = sin(a * 2.0 - r * 7.0 + t * 0.7 + seed) * 0.5 + 0.5;
                    float neb = smoothstep(0.35, 1.0, arms) * smoothstep(1.0, 0.1, r);
                    col = lerp(col, accent, neb * 0.45 * lerp(0.5, 1.0, faceMask));
                    col += accent * glow * 0.25 * exp(-r * r * 10.0);
                    col += float3(1.0, 1.0, 1.0) * (Glitter(p, 10.0, 0.3, seed, t) + Glitter(p, 17.0, 0.25, seed + 5.0, t * 0.8) * 0.5) * (1.0 + glow);
                }
                else if (pattern == 10) // arc-en-ciel
                {
                    float h = frac(atan2(p.y, p.x) / TAU + r * 0.35 - t * 0.12);
                    float3 rainbow = lerp(Hue(h), 1.0, 0.35);
                    col = lerp(col, col * rainbow * 1.15, 0.8);
                    col += float3(1.0, 1.0, 1.0) * Glitter(p, 8.0, 0.35, seed, t) * (1.0 + glow);
                }
                return col;
            }

            // Visage : 'q' en unités du visage (centré, repère du décor / taille du visage).
            float3 Face(float2 q, int style, int expr, float2 look, float blink, float3 col, float3 shade, float t, float blushOn)
            {
                if (abs(q.x) > 0.75 || abs(q.y) > 0.6) return col;
                float3 ink = float3(0.12, 0.06, 0.10);
                float3 white = float3(1.0, 1.0, 1.0);
                float side = q.x < 0.0 ? -1.0 : 1.0;
                float2 e = float2(abs(q.x), q.y) - float2(0.29, 0.07) - float2(look.x * side, look.y);

                // Joues roses
                if (blushOn > 0.5 || expr == 1)
                {
                    float b = SdEllipse(float2(abs(q.x), q.y) - float2(0.43, -0.09), float2(0.09, 0.045));
                    col = lerp(col, float3(1.0, 0.45, 0.55), saturate(0.5 - b / 0.04) * 0.45);
                }

                float eye = 0.0, glint = 0.0;
                bool closedArc = expr == 1 || (expr == 0 && style == 4);
                if (closedArc)
                {
                    // ^ ^ (heureux) ou ‿ ‿ (serein)
                    float2 a = expr == 1 ? e - float2(0.0, -0.05) : float2(e.x, -e.y) - float2(0.0, -0.06);
                    eye = Fill(SdArc(a, 1.0, 0.09, 0.024));
                }
                else if (expr == 3)
                {
                    // > <
                    float2 c = float2(e.x * side, e.y);
                    float d = min(SdSegment(c, float2(-0.07 * side, 0.06), float2(0.06 * side, 0.0)),
                                  SdSegment(c, float2(-0.07 * side, -0.06), float2(0.06 * side, 0.0)));
                    eye = Fill(d - 0.022);
                }
                else
                {
                    float2 r = expr == 2 ? float2(0.12, 0.14) : float2(0.1, 0.13);
                    float open = 1.0 - blink;
                    float2 er = float2(r.x, max(r.y * open, 0.012));
                    float d = SdEllipse(e, er);
                    if (style == 5 && expr == 0) d = max(d, e.y - 0.035); // paupières mi-closes (roi)
                    eye = Fill(d);
                    if (open > 0.3)
                    {
                        float2 g = e - float2(-0.035, 0.05) * open;
                        float star = style == 3 ? exp(-abs(g.x) * 60.0 - abs(g.y) * 9.0) + exp(-abs(g.y) * 60.0 - abs(g.x) * 9.0) : 0.0;
                        float tw = style == 3 ? 0.7 + 0.3 * sin(t * 5.0 + side) : 1.0;
                        glint = saturate(Fill(length(g) - 0.035) + star * tw) + Fill(length(e - float2(0.035, -0.045)) - 0.016);
                        glint *= eye;
                    }
                }
                col = lerp(col, ink, eye);
                col = lerp(col, white * 1.2, glint);

                // Sourcils (féroce)
                if (style == 2 && expr != 1)
                {
                    float2 c = float2(abs(q.x), q.y);
                    float d = SdSegment(c, float2(0.19, 0.26), float2(0.38, 0.31));
                    col = lerp(col, ink, Fill(d - 0.025));
                }

                // Bouche
                float2 m = q - float2(0.0, -0.14);
                float mouth = 0.0, tongue = 0.0;
                if (expr == 1 || (expr == 0 && style == 2))
                {
                    float d = max(SdEllipse(m - float2(0.0, 0.02), float2(0.13, 0.11)), m.y - 0.02);
                    mouth = Fill(d);
                    tongue = Fill(length(m - float2(0.0, -0.11)) - 0.06) * mouth;
                }
                else if (expr == 2)
                {
                    float d = SdEllipse(m - float2(0.0, -0.03), float2(0.06, 0.075));
                    mouth = Fill(d);
                    tongue = Fill(length(m - float2(0.0, -0.1)) - 0.045) * mouth;
                }
                else if (expr == 3)
                {
                    float y = 0.02 * sin(m.x * 60.0);
                    mouth = Fill(max(abs(m.y - y) - 0.016, abs(m.x) - 0.1));
                }
                else if (style == 1)
                {
                    float2 w = float2(abs(m.x) - 0.045, m.y + 0.01);
                    mouth = Fill(SdArc(float2(w.x, -w.y), 1.2, 0.045, 0.016));
                }
                else if (style == 5)
                {
                    float2 w = m - float2(0.03, 0.02);
                    mouth = Fill(SdArc(float2(w.x, -w.y), 0.8, 0.09, 0.018));
                }
                else
                {
                    float2 w = m - float2(0.0, 0.08);
                    mouth = Fill(SdArc(float2(w.x, -w.y), 0.75, 0.12, 0.02));
                }
                col = lerp(col, ink, mouth);
                col = lerp(col, float3(1.0, 0.42, 0.5), tongue);
                return col;
            }

            float4 frag(Varyings i) : SV_Target
            {
                int part = (int)round(i.deco.z);
                float e = i.deco.w;
                float flash = i.light.z;
                float t = _Time.y;
                float3 body = i.color.rgb;
                float3 shade = i.shade.rgb;
                float3 accent = i.accent.rgb;
                float glow = i.accent.w;

                if (part == 1)
                {
                    // Liseré du contour : s'assombrit jusqu'à la surface, puis 1,5 px d'anticrénelage.
                    float ew = i.misc.w;
                    float a = smoothstep(0.0, ew, e);
                    a = a * a * (1.0 - smoothstep(ew, 1.0, e));
                    float2 L = i.light.xy;
                    float lit = saturate(dot(normalize(L + 1e-5), normalize(float2(-0.45, 0.9))));
                    float3 rim = shade * lerp(0.55, 0.8, saturate(L.y * 0.5 + 0.5));
                    rim = lerp(rim, lerp(body, 1.0, 0.45) * 1.15, pow(lit, 4.0) * 0.7);
                    rim = lerp(rim, float3(1.6, 1.6, 1.6), flash);
                    return float4(rim * a, a);
                }
                if (part == 2)
                {
                    // Accessoire : couleur des sommets, bord plus sombre, reflet, pointe lumineuse.
                    float a = 1.0 - saturate(e - 1.0);
                    float3 c = i.color.rgb;
                    c = lerp(c, shade, smoothstep(0.72, 1.0, e) * 0.55);
                    float2 lp = i.light.xy;
                    c += 0.22 * smoothstep(-0.2, -0.6, lp.x) * smoothstep(0.1, 0.5, lp.y);
                    c *= 1.0 + i.light.w * smoothstep(0.55, 1.0, lp.y) * (0.75 + 0.25 * sin(t * 4.0));
                    c = lerp(c, float3(1.6, 1.6, 1.6), flash);
                    return float4(c * a, a);
                }
                if (part == 3)
                {
                    // Anneau lumineux (auréole) : à moitié additif.
                    float a = smoothstep(0.0, 0.35, e) * smoothstep(1.0, 0.65, e);
                    float3 c = i.color.rgb * (1.0 + i.light.w * (0.8 + 0.2 * sin(t * 3.0)));
                    return float4(c * a, a * 0.55);
                }

                float2 p = i.deco.xy;
                float2 L = i.light.xy;
                float r = length(L);
                int pattern = (int)round(i.light.w);
                float seed = i.misc.x;
                float faceScale = i.misc.y;
                float2 fq = (p - float2(0.0, -0.02)) / faceScale;
                float faceMask = smoothstep(0.42, 0.6, length(fq / float2(1.15, 0.85)));

                // Gelée : plus claire en haut, ombre en bas, cœur lumineux, bord assombri.
                float3 col = lerp(shade, body, smoothstep(-0.95, 0.55, L.y));
                col += body * 0.22 * exp(-dot(L - float2(0.05, -0.2), L - float2(0.05, -0.2)) * 3.0);
                col = lerp(col, shade * 0.85, smoothstep(0.7, 1.0, r) * 0.45);

                col = Pattern(pattern, p, col, shade, accent, glow, seed, faceMask, t);
                col = Face(fq, (int)round(i.shade.w), (int)round(i.face.w), i.face.xy, i.face.z, col, shade, t, i.misc.z);

                // Reflets : grand reflet doux en haut à gauche, petit point vif, lumière renvoyée en bas à droite.
                float2 h = L - float2(-0.36, 0.44);
                float2 hr = float2(h.x * 0.866 + h.y * 0.5, -h.x * 0.5 + h.y * 0.866);
                float spec = 1.0 - smoothstep(0.55, 1.0, length(hr / float2(0.3, 0.17)));
                col = lerp(col, float3(1.3, 1.3, 1.3), spec * 0.75);
                col = lerp(col, float3(1.5, 1.5, 1.5), Fill(length(L - float2(-0.62, 0.12)) - 0.055) * 0.9);
                float bounce = smoothstep(0.78, 0.97, r) * smoothstep(0.1, 0.7, dot(L / max(r, 1e-4), normalize(float2(0.6, -0.8))));
                col += lerp(body, 1.0, 0.5) * bounce * 0.35;

                col = lerp(col, float3(1.6, 1.6, 1.6), flash);
                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
