// Effets (SlimeFx) et bocal (SlimeBackdrop) : quadrilatères et bandes, un seul maillage par composant.
// TEXCOORD0 xy = coordonnées locales (-1..1), z = type, w = paramètre ; TEXCOORD1 = couleur (peut dépasser 1 : lueur) ;
// TEXCOORD2 x = part additive (0 = mélange normal, 1 = s'ajoute à la lumière).
// Types : 0 goutte de gelée, 1 étincelle, 2 onde (anneau), 3 confetti, 4 halo lumineux, 5 paroi de verre, 6 point, 7 aplat,
// 8 bocal (param = fêlures), 9 barre arrondie (param = longueur / épaisseur).
Shader "Slime/Fx"
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
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 local : TEXCOORD0;
                float4 color : TEXCOORD1;
                float4 extra : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.local = v.uv0;
                o.color = v.uv1;
                o.extra = v.uv2;
                return o;
            }

            float Fill(float d)
            {
                float w = max(fwidth(d), 1e-5);
                return saturate(0.5 - d / w);
            }

            float4 frag(Varyings i) : SV_Target
            {
                float2 q = i.local.xy;
                int type = (int)round(i.local.z);
                float param = i.local.w;
                float3 c = i.color.rgb;
                float a = 0.0;
                float d = length(q);

                if (type == 0)
                {
                    a = Fill(d - 1.0);
                    c *= lerp(1.0, 0.7, smoothstep(0.55, 1.0, d)) * (0.9 + 0.2 * q.y);
                    c = lerp(c, float3(1.4, 1.4, 1.4), Fill(length(q - float2(-0.35, 0.38)) - 0.28) * 0.85);
                }
                else if (type == 1)
                {
                    float s = exp(-d * 4.0) * 0.8 + exp(-abs(q.x) * 28.0 - abs(q.y) * 2.6) + exp(-abs(q.y) * 28.0 - abs(q.x) * 2.6);
                    a = saturate(s) * saturate(1.0 - d);
                }
                else if (type == 2)
                {
                    float th = max(param, 0.01);
                    float x = abs(d - (1.0 - th)) / th;
                    a = saturate(1.0 - x * x) * Fill(d - 1.0);
                }
                else if (type == 3)
                {
                    a = Fill(max(abs(q.x), abs(q.y) * 2.0) - 1.0);
                    c *= 0.75 + 0.25 * param;
                }
                else if (type == 4)
                {
                    float s = saturate(1.0 - d);
                    a = s * s;
                }
                else if (type == 5)
                {
                    float edge = smoothstep(0.55, 1.0, abs(q.y));
                    a = (0.22 + 0.6 * edge) * Fill(abs(q.y) - 1.0);
                    c *= 0.55 + 0.75 * edge;
                }
                else if (type == 6)
                {
                    a = Fill(d - 1.0) * (1.0 - 0.4 * smoothstep(0.3, 1.0, d));
                }
                else if (type == 8)
                {
                    // Bocal : bulle de verre (bord épais, intérieur presque clair), reflets, fêlures selon param (0 = intact, 1 = brisé).
                    float inside = Fill(d - 1.0);
                    float rim = smoothstep(0.8, 0.97, d);
                    float ang = atan2(q.y, q.x);
                    float shine = Fill(abs(d - 0.76) - 0.05) * saturate(1.0 - abs(ang - 2.2) / 0.6);
                    shine += Fill(length(q - float2(0.45, 0.42)) - 0.07);
                    float crack = 0.0;
                    if (param > 0.0)
                    {
                        float2 o = float2(0.2, -0.25);
                        [unroll] for (int k = 0; k < 7; k++)
                        {
                            float t = k * 0.8975979 + 0.35;
                            float2 dir = float2(cos(t), sin(t));
                            float2 n = float2(-dir.y, dir.x);
                            float len = (0.55 + 0.45 * frac(sin(k * 12.9898 + 1.7) * 43758.5453)) * saturate(param * 2.0 - k * 0.12);
                            float2 p = q - o;
                            float along = clamp(dot(p, dir), 0.0, len);
                            float wob = 0.05 * sin(along * 15.0 + k * 3.0) * saturate(along * 8.0);
                            crack = max(crack, Fill(length(p - dir * along - n * wob) - 0.035));
                        }
                        crack *= inside;
                    }
                    a = inside * (0.1 + 0.8 * rim);
                    c = lerp(c, float3(1.4, 1.4, 1.4), saturate(shine));
                    a = max(a, saturate(shine) * inside * 0.85);
                    c = lerp(c, float3(1.0, 1.0, 1.0), crack);
                    a = max(a, crack);
                }
                else if (type == 9)
                {
                    // Barre arrondie (pales du moulin) : capsule de demi-longueur param × demi-épaisseur, rayures de sucre d'orge,
                    // éclairée par le haut, cernée d'encre.
                    float k = max(param, 1.0);
                    float2 p = float2(q.x * k, q.y);
                    float dd = length(p - float2(clamp(p.x, 1.0 - k, k - 1.0), 0.0)) - 1.0;
                    a = Fill(dd);
                    float stripe = smoothstep(0.45, 0.55, frac((p.x + p.y * 0.9) * 0.3));
                    float3 body = lerp(c, lerp(c, float3(1.0, 1.0, 1.0), 0.8), stripe);
                    body *= 0.8 + 0.3 * saturate(0.5 + 0.5 * q.y);
                    c = lerp(body, float3(0.08, 0.04, 0.12), Fill(-(dd + 0.25)));
                }
                else
                {
                    float streak = smoothstep(0.9, 1.0, sin(q.x * 1.7 + q.y * 0.15) * 0.5 + 0.5);
                    a = 1.0 + streak * 1.5;
                }

                a *= i.color.a;
                return float4(c * a, a * (1.0 - i.extra.x));
            }
            ENDHLSL
        }
    }
}
