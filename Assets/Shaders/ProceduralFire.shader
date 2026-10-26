// Additive, procedural flame for camera-facing / crossed fire cards (no texture needed).
// The flame shape is driven by WORLD height so it works whatever the card UVs or rotation are.
// _FireBottom / _FireHeight are set per renderer by FireplaceFX (MaterialPropertyBlock).
Shader "LayLow/ProceduralFire"
{
    Properties
    {
        [HDR] _ColorCore ("Core colour", Color) = (1.0, 0.85, 0.45, 1)
        [HDR] _ColorMid  ("Mid colour",  Color) = (1.0, 0.42, 0.06, 1)
        [HDR] _ColorEdge ("Edge colour", Color) = (0.55, 0.07, 0.02, 1)
        _Intensity  ("Intensity", Float) = 2.2
        _FireBottom ("Fire bottom (world Y)", Float) = 0
        _FireHeight ("Fire height (m)", Float) = 0.3
        _NoiseScale ("Noise scale (per metre)", Float) = 22
        _Speed      ("Rise speed (m/s)", Float) = 0.35
        _EdgeSoftness ("Card edge softness", Range(0.01, 0.5)) = 0.18
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Fire"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorCore;
                float4 _ColorMid;
                float4 _ColorEdge;
                float _Intensity;
                float _FireBottom;
                float _FireHeight;
                float _NoiseScale;
                float _Speed;
                float _EdgeSoftness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float2 uv : TEXCOORD1; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv;
                return o;
            }

            float hash3(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float noise3(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(hash3(i + float3(0,0,0)), hash3(i + float3(1,0,0)), f.x),
                                 lerp(hash3(i + float3(0,1,0)), hash3(i + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(hash3(i + float3(0,0,1)), hash3(i + float3(1,0,1)), f.x),
                                 lerp(hash3(i + float3(0,1,1)), hash3(i + float3(1,1,1)), f.x), f.y), f.z);
            }

            float fbm(float3 p)
            {
                float v = 0.0, a = 0.5;
                for (int k = 0; k < 4; k++) { v += a * noise3(p); p = p * 2.03 + 11.7; a *= 0.5; }
                return v;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float h = saturate((i.positionWS.y - _FireBottom) / max(_FireHeight, 0.001));

                // noise that rises over time (sample further down as time goes on)
                float3 p = i.positionWS;
                p.y -= _Time.y * _Speed;
                float n = fbm(p * _NoiseScale);

                // tongues of flame: dense at the bottom, breaking into licks towards the top
                float shape = n * (1.25 - h) - h * 0.45;

                // hide the hard rectangle of each card
                float2 uv = i.uv;
                float edge = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
                float edgeMask = smoothstep(0.0, _EdgeSoftness, edge);

                float a = smoothstep(0.08, 0.38, shape) * edgeMask;

                float3 col = lerp(_ColorEdge.rgb, _ColorMid.rgb, smoothstep(0.12, 0.42, shape));
                col = lerp(col, _ColorCore.rgb, smoothstep(0.42, 0.8, shape));

                return half4(col * a * _Intensity, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
