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
                // mask: rgb = each object's own outline colour, a = 'object here'
                fixed4 here = tex2D(_MainTex, i.uv);
                float m = here.a;
                float edge = 0;
                fixed3 edgeCol = _Color.rgb;
                // two rings of samples so thick outlines stay solid
                for (int k = 0; k < 16; k++)
                {
                    float a = k * 0.3926991; // 2*pi/16
                    float2 d = float2(cos(a), sin(a)) * _MainTex_TexelSize.xy;
                    fixed4 s1 = tex2D(_MainTex, i.uv + d * _Width);
                    fixed4 s2 = tex2D(_MainTex, i.uv + d * _Width * 0.5);
                    if (s1.a > edge) { edge = s1.a; edgeCol = s1.rgb / max(s1.a, 0.001); }
                    if (s2.a > edge) { edge = s2.a; edgeCol = s2.rgb / max(s2.a, 0.001); }
                }
                // m and edge carry each object's fade (1 = full strength); 'inside' is just whether an object is here
                float inside = saturate(m * 20);
                float outline = edge * (1 - inside);
                fixed3 col = outline > m * _Fill ? edgeCol : here.rgb / max(m, 0.001);
                float alpha = max(outline, m * _Fill);
                return fixed4(col, alpha * _Color.a);
            }
            ENDCG
        }
    }
}
