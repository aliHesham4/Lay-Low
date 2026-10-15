Shader "Custom/AoETarget"
{
    Properties
    {
        _AoeColor ("AoE Color", Color) = (0, 0.5, 1, 0.5) // Defaulted to blue for your marker
        _Width ("Width", Range(0.001, 0.1)) = 0.01
        _FillValue ("Fill Value", Range(0.0, 1.0)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _AoeColor;
                float _Width;
                float _FillValue;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float d = distance(IN.uv, float2(0.5, 0.5));
                half alpha = 0.0;

                // External circle
                if (d >= 0.5 - 2.0 * _Width && d <= 0.5)
                    alpha = _AoeColor.a;

                // Internal circle
                if (d < _FillValue * 0.5)
                    alpha = _AoeColor.a;

                return half4(_AoeColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}