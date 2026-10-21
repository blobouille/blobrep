// Fond d'écran (SlimeBackdrop) : dégradé, bulles floues qui montent lentement, pulsation colorée des fusions, vignette.
// TEXCOORD0 = coordonnées écran (0..1).
Shader "Slime/Backdrop"
{
    Properties
    {
        _Top ("Haut", Color) = (0.16, 0.11, 0.33, 1)
        _Bottom ("Bas", Color) = (0.04, 0.13, 0.22, 1)
        _Bokeh ("Bulles", Color) = (0.55, 0.8, 1, 0.08)
        _Pulse ("Pulsation", Color) = (0, 0, 0, 0)
        _Aspect ("Largeur / hauteur", Float) = 0.5625
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _Top, _Bottom, _Bokeh, _Pulse;
            float _Aspect;

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv = v.uv;
                return o;
            }

            float Hash(float n) { return frac(sin(n * 91.3458) * 47453.5453); }

            float4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                float t = _Time.y;
                float3 col = lerp(_Bottom.rgb, _Top.rgb, smoothstep(0.0, 1.0, uv.y));
                col += _Top.rgb * 0.35 * exp(-dot(uv - float2(0.3, 0.85), uv - float2(0.3, 0.85)) * 6.0);

                float2 p = float2(uv.x * _Aspect, uv.y);
                for (int k = 0; k < 14; k++)
                {
                    float h = Hash(k + 1.0);
                    float2 c = float2(Hash(k + 11.0) * _Aspect, frac(Hash(k + 23.0) + t * (0.004 + 0.01 * h)) * 1.3 - 0.15);
                    c.x += 0.02 * sin(t * 0.3 + k);
                    float rad = 0.03 + 0.08 * Hash(k + 37.0);
                    float d = length(p - c);
                    float disc = smoothstep(rad, rad * 0.7, d) * (0.5 + 0.5 * smoothstep(rad * 0.6, rad, d));
                    col += _Bokeh.rgb * _Bokeh.a * disc * (0.5 + h);
                }

                float2 v = uv - 0.5;
                col += _Pulse.rgb * _Pulse.a * (1.0 - 0.8 * length(v));
                col *= 1.0 - 0.55 * dot(v, v);
                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
