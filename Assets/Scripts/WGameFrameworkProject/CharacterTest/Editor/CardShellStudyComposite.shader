Shader "Hidden/MxFramework/CardShellStudyComposite"
{
    Properties { _MainTex("Background",2D)="white"{} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex, _Card0, _Card1, _Card2;
        float4 _MainTex_TexelSize, _Rect0, _Rect1, _Rect2;
        float _BackdropMode;
        float4 Blur(v2f_img i):SV_Target
        {
            float2 d = abs(_MainTex_TexelSize.xy) * 6;
            float4 c = tex2D(_MainTex,i.uv)*.20;
            c += tex2D(_MainTex,i.uv+float2(d.x,0))*.12;
            c += tex2D(_MainTex,i.uv-float2(d.x,0))*.12;
            c += tex2D(_MainTex,i.uv+float2(0,d.y))*.12;
            c += tex2D(_MainTex,i.uv-float2(0,d.y))*.12;
            c += tex2D(_MainTex,i.uv+d)*.08;
            c += tex2D(_MainTex,i.uv-d)*.08;
            c += tex2D(_MainTex,i.uv+float2(d.x,-d.y))*.08;
            c += tex2D(_MainTex,i.uv+float2(-d.x,d.y))*.08;
            return c;
        }
        float4 Layer(float4 b,sampler2D card,float4 r,float2 uv)
        {
            float2 q=(uv-r.xy)/r.zw;
            float inside=step(0,q.x)*step(q.x,1)*step(0,q.y)*step(q.y,1);
            float4 c=tex2D(card,saturate(q));
            // RenderTextures contain premultiplied RGB after transparent blending.
            b.rgb=c.rgb*inside+b.rgb*(1-c.a*inside);
            return b;
        }
        float4 Composite(v2f_img i):SV_Target
        {
            float4 c=tex2D(_MainTex,i.uv);
            if(_BackdropMode>.5) c=float4(.085,.105,.135,1);
            c=Layer(c,_Card0,_Rect0,i.uv);
            c=Layer(c,_Card1,_Rect1,i.uv);
            c=Layer(c,_Card2,_Rect2,i.uv);
            c.a=1;
            return c;
        }
        ENDCG
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Blur
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Composite
            ENDCG
        }
    }
}
