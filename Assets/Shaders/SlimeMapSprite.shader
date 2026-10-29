// Éléments plats de la carte (SlimeMap), dessinés par distance signée : ciel (dégradé, petits nuages), étoiles des niveaux finis
// (face à la caméra), ombre de l'avatar et halo du niveau actuel (posés au sol). Sommets : position = centre dans le monde ;
// TEXCOORD0 xy = coin (-1..1), z = sorte (0 ciel, 1 étoile, 2 ombre, 3 halo) ; TEXCOORD1 xy = demi-taille (m), z = niveau,
// w = place de l'étoile ; TEXCOORD2 xy = décalage face à la caméra (m). Même courbure du monde que Slime/Map.
Shader "Slime/MapSprite"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _SlimeMapBend;
            float4 _SlimeMapNodes[32];
            float4 _SlimeMapSkyTop, _SlimeMapSkyHorizon;
            float _SlimeMapTime;

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
                float4 p : TEXCOORD0;
                float4 q : TEXCOORD1;
            };

            float3 Bend(float3 w)
            {
                float dz = max(0.0, w.z - _SlimeMapBend.y);
                w.y -= _SlimeMapBend.x * dz * dz;
                return w;
            }

            float Pop(float dt)
            {
                if (dt < 0.0) return 0.0;
                float x = saturate(dt / 0.35);
                const float c = 2.4;
                x -= 1.0;
                return 1.0 + (c + 1.0) * x * x * x + c * x * x;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float kind = v.uv0.z;
                float2 corner = v.uv0.xy;
                o.color = v.color;
                o.p = float4(corner, kind, 0.0);
                o.q = v.uv1;
                if (kind < 0.5)
                {
                    o.positionCS = float4(corner.x, corner.y * _ProjectionParams.x, 0.5, 1.0);
                    o.p.w = corner.y * 0.5 + 0.5;
                    return o;
                }
                float3 center = TransformObjectToWorld(v.positionOS);
                float3 w;
                if (kind < 1.5)
                {
                    float4 node = _SlimeMapNodes[(int)(v.uv1.z + 0.5)];
                    float slot = v.uv1.w;
                    float scale = node.x > 1.5 ? Pop(_SlimeMapTime - node.w - 0.25 - slot * 0.2) : 0.0;
                    o.p.w = slot < node.y ? 1.0 : 0.0;
                    float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                    w = Bend(center) + right * v.uv2.x + up * v.uv2.y + (right * (corner.x * v.uv1.x) + up * (corner.y * v.uv1.y)) * scale;
                }
                else
                {
                    w = Bend(center + float3(corner.x * v.uv1.x, 0.0, corner.y * v.uv1.y));
                    if (kind > 2.5)
                    {
                        float4 node = _SlimeMapNodes[(int)(v.uv1.z + 0.5)];
                        o.p.w = node.z;
                    }
                }
                o.positionCS = TransformWorldToHClip(w);
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

            // Étoile à 5 branches (Inigo Quilez).
            float Star(float2 p, float r, float rf)
            {
                const float2 k1 = float2(0.809016994, -0.587785252);
                const float2 k2 = float2(-k1.x, k1.y);
                p.x = abs(p.x);
                p -= 2.0 * max(dot(k1, p), 0.0) * k1;
                p -= 2.0 * max(dot(k2, p), 0.0) * k2;
                p.x = abs(p.x);
                p.y -= r;
                float2 ba = rf * float2(-k1.y, k1.x) - float2(0, 1);
                float h = clamp(dot(p, ba) / dot(ba, ba), 0.0, r);
                return length(p - ba * h) * sign(p.y * ba.x - p.x * ba.y);
            }

            float Fill(float d)
            {
                float w = max(fwidth(d), 1e-5);
                return saturate(0.5 - d / w);
            }

            float4 frag(Varyings i) : SV_Target
            {
                float kind = i.p.z;
                float2 c = i.p.xy;
                if (kind < 0.5)
                {
                    float y = i.p.w;
                    float3 col = lerp(_SlimeMapSkyHorizon.rgb, _SlimeMapSkyTop.rgb, smoothstep(0.45, 1.0, y));
                    float2 uv = float2(c.x * 2.0 + _SlimeMapTime * 0.015, y * 7.0);
                    float clouds = Noise(uv * float2(1.0, 0.6)) * 0.6 + Noise(uv * 2.3 + 4.1) * 0.4;
                    col = lerp(col, float3(1.0, 1.0, 1.0), smoothstep(0.55, 0.8, clouds) * 0.45 * smoothstep(0.4, 0.8, y));
                    return float4(col, 1.0);
                }
                if (kind < 1.5)
                {
                    float d = Star(c, 0.82, 0.48) - 0.07;
                    float edge = Fill(d - 0.08);
                    float inner = Fill(d + 0.03);
                    float3 fill = i.p.w > 0.5 ? lerp(float3(1.0, 0.62, 0.08), float3(1.0, 0.95, 0.5), saturate(c.y * 0.6 + 0.55)) * 1.15 : float3(0.3, 0.25, 0.42);
                    float3 outline = i.p.w > 0.5 ? float3(0.5, 0.22, 0.04) : float3(0.12, 0.08, 0.18);
                    float a = i.p.w > 0.5 ? 1.0 : 0.7;
                    float3 col = lerp(outline, fill, inner);
                    return float4(col * edge * a, edge * a);
                }
                float r = length(c);
                if (kind < 2.5)
                {
                    float a = (1.0 - smoothstep(0.3, 1.0, r)) * i.color.a;
                    return float4(i.color.rgb * a, a);
                }
                float pulse = 0.5 + 0.5 * sin(_SlimeMapTime * 4.5);
                float ring = smoothstep(0.55, 0.8, r) * (1.0 - smoothstep(0.85, 1.0, r));
                float a = ring * i.p.w * (0.35 + 0.35 * pulse);
                return float4(float3(1.0, 1.0, 0.85) * a, a * 0.6);
            }
            ENDHLSL
        }
    }
}
