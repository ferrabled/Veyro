Shader "Veyro/CosmeticEffect"
{
    Properties { _BaseMap("Sprite",2D)="white"{} _BaseColor("Tint",Color)=(1,1,1,1) _Ribbon("Ribbon",Float)=0 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor; half _Ribbon;
            CBUFFER_END
            V vert(A i) { V o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=i.uv; o.color=i.color*_BaseColor; return o; }
            half4 frag(V i):SV_Target
            {
                half alpha=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).a;
                half3 color=i.color.rgb;
                if(_Ribbon>0.5)
                {
                    half edge=min(i.uv.x,1-i.uv.x);
                    alpha=smoothstep(0,0.12,edge);
                    color*=lerp(0.6,1,smoothstep(0,0.22,edge));
                }
                // Source sprites already carry opacity in alpha; do not dim their tint twice.
                return half4(color,alpha*i.color.a);
            }
            ENDHLSL
        }
    }
}
