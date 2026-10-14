Shader "Hidden/PersonallyPuzzled/Stage5ExactSocketRimV06"
{
    Properties
    {
        _BaseColor ("Exact cyan socket", Color) = (0.02, 0.78, 1.0, 1)
        [HideInInspector] _Color ("Exact cyan socket alias", Color) = (0.02, 0.78, 1.0, 1)
        _FillAlpha ("Restrained interior fill", Range(0, 0.2)) = 0.04
        _RimAlpha ("Readable edge alpha", Range(0, 1)) = 0.74
        _RimPower ("Edge falloff", Range(1, 8)) = 3.4
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent+40"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "ExactSocketRim"
            Blend SrcAlpha One
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _Color;
                half _FillAlpha;
                half _RimAlpha;
                half _RimPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                half3 viewWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(input.normalOS);
                output.positionHCS = positionInputs.positionCS;
                output.normalWS = normalize(normalInputs.normalWS);
                output.viewWS =
                    SafeNormalize(GetWorldSpaceViewDir(positionInputs.positionWS));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half facing = saturate(
                    abs(dot(normalize(input.normalWS), normalize(input.viewWS))));
                half rim = pow(1.0h - facing, _RimPower);
                half alpha = saturate(_FillAlpha + rim * _RimAlpha);
                half luminance = 0.52h + rim * 0.74h;
                return half4(_BaseColor.rgb * luminance, alpha);
            }
            ENDHLSL
        }
    }
}
