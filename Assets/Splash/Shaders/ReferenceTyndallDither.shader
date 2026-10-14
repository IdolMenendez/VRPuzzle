Shader "PersonallyPuzzled/ReferenceTyndallDither"
{
    Properties
    {
        _BaseColor ("Golden Scattering", Color) = (0.72, 0.31, 0.055, 1.0)
        _Density ("Screen-door Density", Range(0.0, 0.5)) = 0.14
        _EdgeSoftness ("Feathered Edge", Range(0.1, 0.95)) = 0.50
    }

    SubShader
    {
        Tags
        {
            "Queue"="AlphaTest+24"
            "RenderType"="TransparentCutout"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "ReferenceTyndallDither"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Density;
                float _EdgeSoftness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float radial = max(abs(input.positionOS.x), abs(input.positionOS.y)) * 2.0;
                float feather = 1.0 - smoothstep(_EdgeSoftness, 1.0, radial);
                float longitudinal = 0.62 + 0.38 * saturate(0.5 - input.positionOS.z);
                float coverage = _Density * feather * longitudinal;
                float2 pixel = floor(input.positionCS.xy);
                float threshold = frac(dot(pixel, float2(0.754877666, 0.569840296)));
                clip(coverage - threshold);
                return half4(_BaseColor.rgb * 0.13, 1.0);
            }
            ENDHLSL
        }
    }
}
