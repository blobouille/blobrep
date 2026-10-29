// Sol de la carte (SlimeMap), peint à plat façon papier : herbe à taches, chemin de grandes dalles cerclé d'encre, jetons
// des niveaux (disques de carton : couleur selon l'état, reflet, numéro), mare. Monde courbé au loin, brume de l'horizon et
// brume de ce qui n'est pas découvert. Sommets : couleur = teinte ; TEXCOORD0 x = balancement, y = sorte (0 sol, 1 chemin,
// 3 jeton, 5 eau), zw = paramètres ; TEXCOORD1 xyz = pivot. Réglages globaux posés par SlimeMap (_SlimeMap*).
Shader "Slime/Map"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _SlimeMapBend;      // x courbure, y z de départ
            float4 _SlimeMapFog;
            float4 _SlimeMapFogRange;  // x début, y fin (m depuis la caméra)
            float4 _SlimeMapMist;      // rgb, a = force
            float4 _SlimeMapReveal;    // x z du début de la brume, y nuages écartés, z longueur de la brume
            float4 _SlimeMapNodes[32]; // x état (0 verrouillé, 1 ouvert, 2 fini), y étoiles, z actuel, w instant du saut
            float4 _SlimeMapStone, _SlimeMapMortar, _SlimeMapEdge;
            float4 _SlimeMapLocked, _SlimeMapOpen, _SlimeMapDone;
            float4 _SlimeMapGrass, _SlimeMapGrassLight, _SlimeMapGrassDark;
            float4 _SlimeMapInfo;      // x demi-largeur du chemin, y numéro du premier niveau - 1, z rayon des jetons
            float _SlimeMapTime;

            static const float TAU = 6.2831853;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float3 world : TEXCOORD0;
                float4 p : TEXCOORD1;
                float4 q : TEXCOORD2;
                float3 bent : TEXCOORD3;
            };

            float3 Bend(float3 w)
            {
                float dz = max(0.0, w.z - _SlimeMapBend.y);
                w.y -= _SlimeMapBend.x * dz * dz;
                return w;
            }

            // Saut d'un jeton qui change d'état : écrasé puis rebond amorti.
            float PopScale(float dt)
            {
                if (dt < 0.0 || dt > 0.7) return 1.0;
                float x = dt / 0.7;
                return 1.0 + 0.35 * sin(x * PI * 3.0) * (1.0 - x);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 w = TransformObjectToWorld(v.positionOS);
                float3 pivot = v.uv1.xyz;
                if (abs(v.uv0.y - 3.0) < 0.5)
                {
                    float4 node = _SlimeMapNodes[(int)(v.uv0.z + 0.5)];
                    float t = _SlimeMapTime;
                    float s = PopScale(t - node.w) * (1.0 + node.z * 0.05 * (0.5 + 0.5 * sin(t * 4.5)));
                    w = pivot + (w - pivot) * float3(s, 2.0 - s, s);
                }
                float3 b = Bend(w);
                o.positionCS = TransformWorldToHClip(b);
                o.world = w;
                o.bent = b;
                o.color = v.color;
                o.p = v.uv0;
                o.q = v.uv1;
                return o;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), f.x), lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), f.x), f.y);
            }

            float Cover(float d)
            {
                float w = max(fwidth(d), 1e-5);
                return saturate(0.5 - d / w);
            }

            // ---- Chiffres arrondis (distance au trait), boîte x 0..0.6, y 0..1 ----

            float Seg(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a, ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h);
            }

            float Arc(float2 p, float2 c, float r, float a0, float a1)
            {
                float2 d = p - c;
                float a = atan2(d.y, d.x);
                float t = a0 + fmod(a - a0 + 4.0 * TAU, TAU);
                float ac = t <= a1 ? t : ((t - a1) < (a0 + TAU - t) ? a1 : a0);
                return length(p - (c + r * float2(cos(ac), sin(ac))));
            }

            float Ring(float2 p, float2 c, float r) { return abs(length(p - c) - r); }

            float Six(float2 p)
            {
                float d = Ring(p, float2(0.3, 0.3), 0.22);
                d = min(d, Seg(p, float2(0.08, 0.3), float2(0.08, 0.6)));
                return min(d, Arc(p, float2(0.36, 0.6), 0.28, 0.9, PI));
            }

            float Digit(float2 p, uint n)
            {
                float d = 1e5;
                if (n == 0u) d = min(min(Arc(p, float2(0.3, 0.68), 0.22, 0.0, PI), Arc(p, float2(0.3, 0.32), 0.22, PI, TAU)),
                                     min(Seg(p, float2(0.08, 0.32), float2(0.08, 0.68)), Seg(p, float2(0.52, 0.32), float2(0.52, 0.68))));
                else if (n == 1u) d = min(Seg(p, float2(0.34, 0.06), float2(0.34, 0.94)), Seg(p, float2(0.34, 0.94), float2(0.14, 0.76)));
                else if (n == 2u)
                {
                    float2 e = float2(0.3, 0.7) + 0.22 * float2(cos(-0.75), sin(-0.75));
                    d = min(min(Arc(p, float2(0.3, 0.7), 0.22, -0.75, PI), Seg(p, e, float2(0.08, 0.06))), Seg(p, float2(0.08, 0.06), float2(0.54, 0.06)));
                }
                else if (n == 3u) d = min(Arc(p, float2(0.3, 0.73), 0.2, -PI * 0.5, 2.6), Arc(p, float2(0.3, 0.3), 0.23, -2.6, PI * 0.5));
                else if (n == 4u) d = min(min(Seg(p, float2(0.44, 0.94), float2(0.06, 0.34)), Seg(p, float2(0.06, 0.34), float2(0.58, 0.34))), Seg(p, float2(0.44, 0.94), float2(0.44, 0.06)));
                else if (n == 5u) d = min(min(Seg(p, float2(0.54, 0.94), float2(0.12, 0.94)), Seg(p, float2(0.12, 0.94), float2(0.12, 0.52))), Arc(p, float2(0.3, 0.33), 0.24, -2.55, 2.35));
                else if (n == 6u) d = Six(p);
                else if (n == 7u) d = min(Seg(p, float2(0.06, 0.94), float2(0.56, 0.94)), Seg(p, float2(0.56, 0.94), float2(0.22, 0.06)));
                else if (n == 8u) d = min(Ring(p, float2(0.3, 0.73), 0.19), Ring(p, float2(0.3, 0.3), 0.23));
                else d = Six(float2(0.6, 1.0) - p);
                return d;
            }

            // Nombre centré sur l'origine, hauteur 1.
            float Number(float2 p, uint n)
            {
                uint tens = n / 10u, units = n - tens * 10u;
                if (tens == 0u) return Digit(p + float2(0.3, 0.5), units);
                float2 q = p + float2(0.67, 0.5);
                return min(Digit(q, tens), Digit(q - float2(0.74, 0.0), units));
            }

            float3 Grass(float2 w)
            {
                float n = Noise(w * 0.32) * 0.65 + Noise(w * 0.9 + 7.3) * 0.35;
                float patch = Cover(0.62 - n);
                float spot = Cover(Noise(w * 0.75 + 31.0) - 0.74);
                float3 col = lerp(_SlimeMapGrass.rgb, _SlimeMapGrassLight.rgb, 1.0 - patch);
                col = lerp(col, _SlimeMapGrassDark.rgb, spot * 0.8);
                float2 cell = floor(w * 2.2);
                float2 f = frac(w * 2.2) - 0.5;
                float h = Hash21(cell);
                float tick = length((f - (float2(Hash21(cell + 3.1), Hash21(cell + 7.7)) - 0.5) * 0.6) * float2(1.0, 0.45)) - 0.05;
                col = lerp(col, _SlimeMapGrassDark.rgb * 0.92, Cover(tick) * step(0.72, h));
                return col;
            }

            float3 PathColor(float across, float s)
            {
                const float cw = 0.56, cl = 0.5, gap = 0.04, r = 0.12;
                float a = across * _SlimeMapInfo.x;
                float row = floor(s / cl);
                float2 cell = float2(a / cw + fmod(row, 2.0) * 0.5, s / cl);
                float2 id = floor(cell);
                float2 local = (frac(cell) - 0.5) * float2(cw, cl);
                float2 q = abs(local) - (float2(cw, cl) * 0.5 - gap - r);
                float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
                float h = Hash21(id);
                float3 col = _SlimeMapStone.rgb * (0.95 + 0.07 * h);
                float bevel = length(max(abs(local - float2(-0.02, 0.025)) - (float2(cw, cl) * 0.5 - gap - r), 0.0)) - r;
                col = lerp(col * 0.92, col, Cover(bevel));
                col = lerp(_SlimeMapMortar.rgb, col, Cover(d));
                float edge = abs(across) * _SlimeMapInfo.x;
                float hw = _SlimeMapInfo.x;
                col = lerp(col, _SlimeMapEdge.rgb, Cover((hw - 0.13) - edge));
                col = lerp(col, _SlimeMapEdge.rgb * 0.45, Cover((hw - 0.045) - edge));
                return col;
            }

            float3 Token(float3 world, float3 pivot, float idx, float part, float3 vcol)
            {
                float4 node = _SlimeMapNodes[(int)(idx + 0.5)];
                float t = _SlimeMapTime;
                float state = t < node.w ? max(0.0, node.x - 1.0) : node.x;
                float3 c = state < 0.5 ? _SlimeMapLocked.rgb : (state < 1.5 ? _SlimeMapOpen.rgb : _SlimeMapDone.rgb);
                float3 ink = c * 0.3 + float3(0.02, 0.01, 0.04);
                if (part < 0.5) return lerp(ink, c * 0.62, vcol.r);
                float2 local = (world.xz - pivot.xz) / _SlimeMapInfo.z;
                float r = length(local);
                float shade = 1.0 - Cover(length(local - float2(-0.08, 0.1)) - 0.83);
                float light = 1.0 - Cover(length(local - float2(0.08, -0.1)) - 0.83);
                float3 col = lerp(c, c * 0.8, shade);
                col = lerp(col, c * 1.15 + 0.06, light);
                col = lerp(col, float3(1.0, 1.0, 1.0), Cover(length((local - float2(-0.45, 0.42)) * float2(1.0, 1.6)) - 0.12) * (state < 0.5 ? 0.35 : 0.9));
                col += node.z * 0.12 * (0.5 + 0.5 * sin(t * 4.5));
                float2 dp = float2(local.x, local.y * 0.78) / 0.58;
                float d = Number(dp, (uint)(idx + 1.0) + (uint)(_SlimeMapInfo.y + 0.5));
                float3 text = state < 0.5 ? c * 1.3 : float3(1.0, 1.0, 1.0);
                col = lerp(col, ink, Cover(d - 0.16));
                col = lerp(col, text, Cover(d - 0.085));
                col = lerp(col, ink, Cover(0.92 - r));
                return col;
            }

            float3 Water(float3 world, float3 pivot, float2 radii)
            {
                float2 local = (world.xz - pivot.xz) / radii;
                float r = length(local);
                float t = _SlimeMapTime;
                float3 deep = float3(0.25, 0.55, 0.95), light = float3(0.55, 0.82, 1.0);
                float3 col = lerp(light, deep, smoothstep(0.2, 0.9, r * 0.8 + local.y * 0.2));
                for (int i = 0; i < 3; i++)
                {
                    float2 c = float2(sin(i * 2.1 + 0.5) * 0.45, cos(i * 1.7) * 0.35);
                    float rr = frac(t * 0.25 + i * 0.33);
                    float ring = abs(length((local - c) * float2(1.0, 1.4)) - rr * 0.5) - 0.025;
                    col = lerp(col, light * 1.1, Cover(ring) * (1.0 - rr));
                }
                col = lerp(col, float3(1.0, 1.0, 1.0), Cover(length((local - float2(-0.3, 0.35)) * float2(1.0, 2.5)) - 0.12) * 0.8);
                col = lerp(col, float3(0.62, 0.86, 1.0), Cover(0.88 - r));
                col = lerp(col, float3(0.12, 0.22, 0.45), Cover(0.96 - r));
                return col;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float kind = i.p.y;
                float3 col;
                if (kind < 0.5) col = Grass(i.world.xz);
                else if (kind < 1.5) col = PathColor(i.p.z, i.p.w);
                else if (kind < 3.5) col = Token(i.world, i.q.xyz, i.p.z, i.p.w, i.color.rgb);
                else if (kind < 5.5) col = Water(i.world, i.q.xyz, i.p.zw);
                else
                {
                    float r = length((i.world.xz - i.q.xz) / i.p.zw);
                    col = lerp(i.color.rgb, i.color.rgb * 0.3, Cover(0.86 - r));
                    col = lerp(col, i.color.rgb * 1.15, Cover(length((i.world.xz - i.q.xz) / i.p.zw - float2(-0.25, 0.3)) - 0.3) * 0.5);
                }
                col *= 1.0 + (Hash21(floor(i.world.xz * 90.0)) - 0.5) * 0.04;

                float mist = saturate((i.world.z - _SlimeMapReveal.x) / max(0.01, _SlimeMapReveal.z));
                col = lerp(col, _SlimeMapMist.rgb, mist * _SlimeMapMist.a);
                float dist = distance(_WorldSpaceCameraPos, i.bent);
                float fog = saturate((dist - _SlimeMapFogRange.x) / max(0.01, _SlimeMapFogRange.y - _SlimeMapFogRange.x));
                col = lerp(col, _SlimeMapFog.rgb, fog * fog);
                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
