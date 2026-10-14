Shader "PersonallyPuzzled/Stage6V06RestrainedEdgeGhost"
{
    Properties
    {
        _BaseColor ("Interior Tint", Color) = (1.0, 0.42, 0.04, 1.0)
        _EdgeColor ("Edge Tint", Color) = (1.0, 0.72, 0.18, 1.0)
        _InteriorAlpha ("Interior Alpha", Range(0, 0.12)) = 0.025
        _EdgeAlpha ("Edge Alpha", Range(0, 0.55)) = 0.32
        _FresnelPower ("Edge Tightness", Range(1, 8)) = 3.2
        _GhostStrength ("Ghost Strength", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+30"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "RestrainedSocketEdgeGhost"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirectionWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                half _InteriorAlpha;
                half _EdgeAlpha;
                half _FresnelPower;
                half _GhostStrength;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirectionWS = GetWorldSpaceViewDir(positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirectionWS = normalize(input.viewDirectionWS);
                half fresnel = pow(
                    1.0h - saturate(abs(dot(normalWS, viewDirectionWS))),
                    max(1.0h, _FresnelPower));
                half alpha = lerp(_InteriorAlpha, _EdgeAlpha, fresnel) *
                    saturate(_GhostStrength);
                half3 color = lerp(_BaseColor.rgb, _EdgeColor.rgb, fresnel);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
