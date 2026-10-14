Shader "PersonallyPuzzledVR/SelectionOutlineV62"
{
    Properties
    {
        _BaseColor("Outline Color", Color) = (0.20, 0.90, 1.0, 1.0)
        _EmissionColor("Emission Color", Color) = (0.20, 0.90, 1.0, 1.0)
        _OutlineWidth("Outline Width", Range(0.0005, 0.025)) = 0.006
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+20"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Selection Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EmissionColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 expanded = input.positionOS.xyz +
                    normalize(input.normalOS) * _OutlineWidth;
                output.positionCS = TransformObjectToHClip(expanded);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(max(_BaseColor.rgb, _EmissionColor.rgb), 1.0);
            }
            ENDHLSL
        }
    }
}
