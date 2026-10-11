Shader "MxFramework/UI/PointerRipple"
{
    Properties { _MainTex("FairyGUI texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            struct Attributes
            {
                float4 position : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 ring : TEXCOORD1;
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 ring : TEXCOORD1;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.color = input.color;
                output.uv = input.uv;
                output.ring = input.ring;
                return output;
            }
            float4 Frag(Varyings input) : SV_Target
            {
                float radius = length(input.uv);
                float distance = radius - input.ring.x;
                // One broad, continuous band. Pixel feathering softens its silhouette at native size.
                float pixel = max(fwidth(distance), .00001);
                float halfWidth = max(input.ring.y * .5, pixel);
                float feather = max(pixel * .8, input.ring.y * .18);
                float coverage = 1 - smoothstep(max(0, halfWidth - feather), halfWidth + feather, abs(distance));
                return float4(.38, .68, .78, coverage * input.color.a);
            }
            ENDCG
        }
    }
}
