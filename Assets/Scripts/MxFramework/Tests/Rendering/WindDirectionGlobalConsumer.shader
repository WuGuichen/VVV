Shader "Hidden/MxFramework/Tests/Rendering/WindDirectionGlobalConsumer"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _MxWindDirection;

            fixed4 frag(v2f_img input) : SV_Target
            {
                return fixed4(saturate(_MxWindDirection.xyz), 1.0);
            }
            ENDCG
        }
    }
}
