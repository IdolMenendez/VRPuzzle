Shader "Personally Puzzled/Selection/Collectible PBR Lite V06"
{
    Properties
    {
        _BaseMap("Base Color", 2D) = "white" {}
        _BaseColor("Base Tint", Color) = (1,1,1,1)
        [Normal] _NormalMap("Normal", 2D) = "bump" {}
        _NormalScale("Normal Scale", Range(0,2)) = 0.8
        _MetallicSmoothnessMap("Metallic Smoothness", 2D) = "black" {}
        _Metallic("Metallic", Range(0,1)) = 0.18
        _Smoothness("Smoothness", Range(0,1)) = 0.34
        _Ambient("Controlled Ambient", Range(0,1)) = 0.48
        _KeyStrength("Warm Key Strength", Range(0,2)) = 0.72
        _CoolFill("Cool Fill Strength", Range(0,1)) = 0.16
        _RimStrength("Gold Rim Strength", Range(0,1)) = 0.14
        _Exposure("Texture Exposure", Range(0.5,2)) = 1.05
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_MetallicSmoothnessMap);
            SAMPLER(sampler_MetallicSmoothnessMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _NormalScale;
                half _Metallic;
                half _Smoothness;
                half _Ambient;
                half _KeyStrength;
                half _CoolFill;
                half _RimStrength;
                half _Exposure;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                half4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 tangentWS : TEXCOORD2;
                half3 bitangentWS : TEXCOORD3;
                float2 uv : TEXCOORD4;
                half fogFactor : TEXCOORD5;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = normal.normalWS;
                output.tangentWS = normal.tangentWS;
                output.bitangentWS = normal.bitangentWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo = baseSample.rgb * _BaseColor.rgb;
                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv),
                    _NormalScale);
                half3x3 tangentToWorld = half3x3(
                    normalize(input.tangentWS),
                    normalize(input.bitangentWS),
                    normalize(input.normalWS));
                half3 normalWS = normalize(TransformTangentToWorld(normalTS, tangentToWorld));
                half3 viewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 keyDirection = normalize(half3(-0.42h, 0.76h, -0.50h));
                half3 fillDirection = normalize(half3(0.72h, 0.26h, -0.64h));
                half key = saturate(dot(normalWS, keyDirection));
                half fill = saturate(dot(normalWS, fillDirection));
                half3 halfDirection = normalize(keyDirection + viewDirection);
                half4 metalSample = SAMPLE_TEXTURE2D(
                    _MetallicSmoothnessMap,
                    sampler_MetallicSmoothnessMap,
                    input.uv);
                half metallic = saturate(max(_Metallic, metalSample.r));
                half smoothness = saturate(max(_Smoothness, metalSample.a));
                half specularPower = lerp(18.0h, 112.0h, smoothness);
                half specularTerm = pow(saturate(dot(normalWS, halfDirection)), specularPower);
                half3 specularColor = lerp(half3(0.055h, 0.050h, 0.042h), albedo, metallic);
                half rim = pow(1.0h - saturate(dot(normalWS, viewDirection)), 3.0h);
                half3 warmKey = half3(1.00h, 0.72h, 0.43h) * key * _KeyStrength;
                half3 coolFill = half3(0.42h, 0.58h, 0.90h) * fill * _CoolFill;
                half3 lighting = _Ambient.xxx + warmKey + coolFill;
                half3 color = albedo * lighting * _Exposure;
                color += specularColor * specularTerm * 0.42h;
                color += half3(1.00h, 0.57h, 0.16h) * rim * _RimStrength;
                color = MixFog(color, input.fogFactor);
                return half4(color, baseSample.a * _BaseColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            ZWrite On
        }
    }
}
