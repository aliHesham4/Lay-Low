Shader "Custom/DistractionOutlineUI"
{
    Properties
    {
        _MainTex ("Mask", 2D) = "black" {}
        _Color ("Outline colour", Color) = (1, 0.9, 0.1, 1)
        _Width ("Outline width (px)", Float) = 4
        _Fill ("Fill alpha inside the shape", Range(0, 1)) = 0.12
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float _Width;
            float _Fill;
            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                float m = tex2D(_MainTex, i.uv).r;
                float edge = 0;
                // two rings of samples so thick outlines stay solid
                for (int k = 0; k < 16; k++)
                {
                    float a = k * 0.3926991; // 2*pi/16
                    float2 d = float2(cos(a), sin(a)) * _MainTex_TexelSize.xy;
                    edge = max(edge, tex2D(_MainTex, i.uv + d * _Width).r);
                    edge = max(edge, tex2D(_MainTex, i.uv + d * _Width * 0.5).r);
                }
                float outline = edge * (1 - m);
                float alpha = max(outline, m * _Fill);
                return fixed4(_Color.rgb, alpha * _Color.a);
            }
            ENDCG
        }
    }
}
