Shader "WGame/ShallowWaterDroplets"
{
    Properties { _BaseMap("Droplet glint", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+15" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            Varyings Vert(Attributes i)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(i.positionOS);
                o.uv=float2(i.uv.y, .5+(i.uv.x-.5)*.18); o.color=i.color; return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half3 glint=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
                half mask=max(glint.r,max(glint.g,glint.b));
                return half4(i.color.rgb, saturate(mask*1.35h)*i.color.a);
            }
            ENDHLSL
        }
    }
}
