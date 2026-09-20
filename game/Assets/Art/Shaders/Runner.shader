Shader "Veyro/Runner"
{
    Properties { _BaseMap("Kenney skin", 2D) = "white" {} _OutfitColor("Outfit", Color) = (1,0.55,0.15,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float2 uv:TEXCOORD1; half fog:TEXCOORD2; };
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; half4 _OutfitColor;
            CBUFFER_END
            Varyings vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.normalWS=TransformObjectToWorldNormal(v.normalOS); o.uv=v.uv; o.fog=ComputeFogFactor(o.positionCS.z); return o; }
            half4 frag(Varyings i):SV_Target
            {
                half3 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
                // The shirt's atlas rectangle only: skin, hair, shoes and trousers retain their colors.
                if(i.uv.x>0.149 && i.uv.x<0.480 && i.uv.y<0.522)
                {
                    // Replace the stock shirt graphic with a clean sports top; folds remain visible.
                    half shade=c.r>0.65 ? 0.94 : 0.78;
                    c=_OutfitColor.rgb*shade;
                }
                Light light=GetMainLight();
                half ndl=saturate(dot(normalize(i.normalWS),light.direction));
                half3 lit=c*(half3(0.48,0.53,0.52)+ndl*light.color*0.66);
                return half4(MixFog(lit,i.fog),1);
            }
            ENDHLSL
        }
    }
}
