Shader "Hidden/MxFramework/SceneLightingCapture"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        float4x4 _CaptureVP, _CaptureInvVP, _CaptureView;
        float4 _CaptureOrigin, _CaptureViewport, _CaptureRange;
        struct Attr { float3 position:POSITION; float4 color:COLOR; };
        struct Var { float4 position:SV_POSITION; float3 world:TEXCOORD0; nointerpolation float id:TEXCOORD1; };
        float2 Oct(float3 d) { d/=abs(d.x)+abs(d.y)+abs(d.z); return d.z>=0?d.xy:(1-abs(d.yx))*float2(d.x>=0?1:-1,d.y>=0?1:-1); }
        Var Vert(Attr a) { Var o; o.world=a.position; o.position=mul(_CaptureVP,float4(a.position,1)); o.id=a.color.r; return o; }
        float4 Capture(Var i,bool front:SV_IsFrontFace):SV_Target
        { float3 d=i.world-_CaptureOrigin.xyz; return float4(front?i.id:-i.id,length(d),Oct(normalize(d))); }
        float Shadow(Var i):SV_Target { return (-mul(_CaptureView,float4(i.world,1)).z-_CaptureRange.x)*_CaptureRange.y; }
        struct FS { float4 position:SV_POSITION; };
        FS Fullscreen(uint id:SV_VertexID) { FS o; o.position=GetFullScreenTriangleVertexPosition(id); return o; }
        float4 Sky(FS i):SV_Target
        {
            float2 uv=(i.position.xy-_CaptureViewport.xy)/_CaptureViewport.zw;
            float3 world=ComputeWorldSpacePosition(uv,0.5,_CaptureInvVP);
            return float4(0,_CaptureRange.z,Oct(normalize(world-_CaptureOrigin.xyz)));
        }
        ENDHLSL
        Pass
        {
            Cull Off ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Capture
            ENDHLSL
        }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Fullscreen
            #pragma fragment Sky
            ENDHLSL
        }
        Pass
        {
            Cull Off ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Shadow
            ENDHLSL
        }
    }
}
