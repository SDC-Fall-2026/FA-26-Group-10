Shader "HotPotato/Clay"
{
    Properties { _Color ("Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 position : SV_POSITION; float3 normal : TEXCOORD0; };
            fixed4 _Color;
            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float diffuse = saturate(dot(normalize(i.normal), normalize(float3(-0.45, 0.85, -0.6))));
                return fixed4(_Color.rgb * (0.69 + diffuse * 0.31), 1);
            }
            ENDCG
        }
    }
}
