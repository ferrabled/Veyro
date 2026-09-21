Shader "Veyro/Park Sky"
{
    Properties { _Top("Top", Color) = (0.36,0.67,0.72,1) _Horizon("Horizon", Color) = (0.84,0.92,0.86,1) }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                half4 _Top, _Horizon;
            CBUFFER_END
            Varyings vert(Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.direction = v.positionOS.xyz; return o; }
            half4 frag(Varyings i) : SV_Target
            {
                float3 direction = normalize(i.direction);
                half3 sky = lerp(_Horizon.rgb, _Top.rgb, pow(saturate(direction.y), 0.65));
                float sun = dot(direction, normalize(float3(-0.42,0.32,1)));
                sky = lerp(sky, half3(1,0.92,0.70), smoothstep(0.996,0.998,sun));
                return half4(sky,1);
            }
            ENDHLSL
        }
    }
}
