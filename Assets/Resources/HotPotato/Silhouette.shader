Shader "HotPotato/Silhouette"
{
    Properties { _Color ("Color", Color) = (1,1,1,0.55) }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color;
            float4 vert(float4 vertex : POSITION) : SV_POSITION { return UnityObjectToClipPos(vertex); }
            fixed4 frag() : SV_Target { return _Color; }
            ENDCG
        }
    }
}
