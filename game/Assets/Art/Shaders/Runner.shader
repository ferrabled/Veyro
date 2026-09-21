Shader "Veyro/Runner"
{
    Properties { _AtlasMode("Atlas",Float)=0 _BaseMap("Character skin", 2D) = "white" {} _OutfitColor("Outfit", Color) = (1,0.55,0.15,1) _Metallic("Chrome",Range(0,1))=0 _Glow("Glow",Range(0,1))=0 _Prism("Prism",Range(0,1))=0 }
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
            struct Varyings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float2 uv:TEXCOORD1; half fog:TEXCOORD2; float3 positionWS:TEXCOORD3; };
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; half4 _OutfitColor; half _AtlasMode; half _Metallic; half _Glow; half _Prism;
            CBUFFER_END
            Varyings vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.normalWS=TransformObjectToWorldNormal(v.normalOS); o.positionWS=TransformObjectToWorld(v.positionOS.xyz); o.uv=v.uv; o.fog=ComputeFogFactor(o.positionCS.z); return o; }
            half4 frag(Varyings i):SV_Target
            {
                half3 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
                // Explicit fabric regions in each source atlas keep eyes, skin and hair out of the dye mask.
                half shirt=0;
                if((_AtlasMode<0.5 && i.uv.x>0.149 && i.uv.x<0.480 && i.uv.y<0.522) ||
                   (_AtlasMode>0.5 && _AtlasMode<1.5 && i.uv.x>0.5 && i.uv.y<0.5) ||
                   (_AtlasMode>1.5 && (i.uv.x<0.25 || i.uv.x>0.75) && i.uv.y>0.5 && i.uv.y<0.75))
                {
                    // Replace the stock shirt graphic with a clean sports top; folds remain visible.
                    half shade=c.r>0.65 ? 0.94 : 0.78;
                    shirt=1;
                    half3 tint=lerp(_OutfitColor.rgb,0.65+0.35*cos(_Time.y*1.6+float3(0,2,4)),_Prism);
                    half rim=1-saturate(dot(normalize(i.normalWS),normalize(GetWorldSpaceViewDir(i.positionWS))));
                    half band=pow(saturate(0.5+0.5*i.normalWS.y),10);
                    c=lerp(tint*shade,tint*(0.4+rim*0.55)+band*0.75,_Metallic);
                }
                Light light=GetMainLight();
                half ndl=saturate(dot(normalize(i.normalWS),light.direction));
                half3 lit=c*(half3(0.48,0.53,0.52)+ndl*light.color*0.66);
                lit+=shirt*c*_Glow;
                return half4(MixFog(lit,i.fog),1);
            }
            ENDHLSL
        }
    }
}
