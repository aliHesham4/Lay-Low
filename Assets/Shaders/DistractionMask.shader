Shader "Custom/DistractionMask"
{
    Properties
    {
        _Color ("Outline colour of this object", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; };
            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); return o; }
            fixed4 _Color;
            // the object's own outline colour (premultiplied); alpha = 'object here' x its fade (1 = full)
            fixed4 frag(v2f i) : SV_Target { float f = max(_Color.a, 0.004); return fixed4(_Color.rgb * f, f); }
            ENDCG
        }
    }
}
