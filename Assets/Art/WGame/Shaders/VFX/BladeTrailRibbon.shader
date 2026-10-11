Shader "MxFramework/Vfx/BladeTrailRibbon"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.97,0.98,1,1)
        _GlowStrength ("Glow Strength", Range(0,4)) = 1.7
        _RootFade ("Root Fade", Range(0,0.8)) = 0.62
        _ArcWidth ("Arc Width", Range(0.025,0.2)) = 0.045
        [Enum(ThinBlade,0,SoftRibbon,1,DoubleBlade,2,Energy,3)] _TrailStyle ("Trail Style", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "BladeTrail"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _GlowStrength;
                half _RootFade;
                half _ArcWidth;
                half _TrailStyle;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _Tint; output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Follow the actual blade tip and keep the current blade connected to its trail.
                // Age alpha and UV x feather history, while the newest endpoint stays visible.
                half blade = saturate(input.uv.y);
                half root = smoothstep(_RootFade, min(0.95h, _RootFade + 0.22h), blade);
                half tip = 1.0h - smoothstep(0.997h, 1.0h, blade);
                half distanceToArc = (blade - 0.99h) / max(0.025h, _ArcWidth);
                half core = exp2(-2.0h * distanceToArc * distanceToArc) * root;
                half3 styleTint = half3(1.0h, 1.0h, 1.0h);
                half glow = _GlowStrength;
                half wisp = 0.03h;
                if (_TrailStyle > 0.5h && _TrailStyle < 1.5h)
                {
                    half ribbonDistance = (blade - 0.94h) / 0.16h;
                    core = max(core * 0.65h, exp2(-2.0h * ribbonDistance * ribbonDistance) * root * 0.55h);
                    wisp = 0.1h;
                    glow *= 0.8h;
                }
                else if (_TrailStyle > 1.5h && _TrailStyle < 2.5h)
                {
                    half innerDistance = (blade - 0.81h) / max(0.02h, _ArcWidth * 0.7h);
                    core += exp2(-2.0h * innerDistance * innerDistance) * root * 0.7h;
                }
                else if (_TrailStyle > 2.5h)
                {
                    half energyDistance = (blade - 0.94h) / 0.09h;
                    half pulse = 0.85h + 0.15h * sin(input.uv.x * 30.0h + blade * 12.0h);
                    core = max(core, exp2(-2.0h * energyDistance * energyDistance) * root * 0.65h) * pulse;
                    styleTint = half3(0.28h, 0.78h, 1.0h);
                    wisp = 0.08h;
                    glow *= 1.5h;
                }
                half tail = smoothstep(0.0h, 0.3h, input.uv.x);
                half shape = (wisp * root + core) * tip * tail;
                half3 color = lerp(input.color.rgb * styleTint, half3(0.97h, 0.98h, 1.0h) * styleTint, saturate(core) * 0.65h);
                return half4(color * glow, input.color.a * shape);
            }
            ENDHLSL
        }
    }
}
