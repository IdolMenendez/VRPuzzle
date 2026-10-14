Shader "PersonallyPuzzled/V10SemanticModuleClip"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Metallic ("Metallic", Range(0,1)) = 0.35
        _Smoothness ("Smoothness", Range(0,1)) = 0.62
        _ClipMin ("Clip Min", Vector) = (-100,-100,-100,0)
        _ClipMax ("Clip Max", Vector) = (100,100,100,0)
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "SemanticModuleForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Metallic;
                half _Smoothness;
                float4 _ClipMin;
                float4 _ClipMax;
            CBUFFER_END
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                if (any(input.positionOS < _ClipMin.xyz) || any(input.positionOS > _ClipMax.xyz))
                    discard;
                half4 sample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 normalWS = normalize(input.normalWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half diffuse = saturate(dot(normalWS, light.direction));
                half3 ambient = max(SampleSH(normalWS), half3(0.09h, 0.075h, 0.06h));
                half3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                half3 halfDirection = normalize(light.direction + view);
                half specular = pow(saturate(dot(normalWS, halfDirection)), lerp(12.0h, 96.0h, _Smoothness));
                half attenuation = light.distanceAttenuation * light.shadowAttenuation;
                half3 color = sample.rgb * (ambient + light.color * diffuse * attenuation);
                color += light.color * specular * attenuation * lerp(0.04h, 0.48h, _Metallic);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
