// See-through panel over a doorway while a player is choosing where to use the door lock.
// Drawn on top of everything (visible through walls), brighter frame at the edges, gentle pulse.
Shader "LayLow/DoorwayHighlight"
{
    Properties
    {
        _Color ("Colour", Color) = (0.2, 0.55, 1, 0.3)
        _Hover ("Hovered", Float) = 0
        _Border ("Border width (UV)", Range(0.01, 0.3)) = 0.08
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Hover;
                float _Border;
            CBUFFER_END

            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            V vert (A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.uv = i.uv; return o; }

            half4 frag (V i) : SV_Target
            {
                float edge = min(min(i.uv.x, 1.0 - i.uv.x), min(i.uv.y, 1.0 - i.uv.y));
                float border = 1.0 - smoothstep(_Border * 0.6, _Border, edge);
                float pulse = 0.85 + 0.15 * sin(_Time.y * (_Hover > 0.5 ? 9.0 : 4.0));
                float a = _Color.a * lerp(0.45, 1.0, border) * pulse;
                float3 c = _Color.rgb * (1.0 + border * 0.6 + _Hover * 0.4);
                return half4(c, saturate(a));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
