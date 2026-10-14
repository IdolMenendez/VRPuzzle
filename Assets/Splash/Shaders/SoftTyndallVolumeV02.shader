Shader "PersonallyPuzzled/SoftTyndallVolumeV02"
{
    Properties
    {
        _BaseColor ("Scattered Sunlight", Color) = (1.0, 0.84, 0.56, 0.05)
        _ProfilePower ("Soft Profile", Range(0.6, 4.0)) = 1.7
        _NoiseScale ("Atmosphere Scale", Range(0.2, 6.0)) = 1.75
        _NoiseStrength ("Atmosphere Variation", Range(0.0, 0.6)) = 0.22
        _FlowSpeed ("Dust Drift", Range(-0.3, 0.3)) = 0.035
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent+18"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "SoftTyndallVolumeV02"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _ProfilePower;
                float _NoiseScale;
                float _NoiseStrength;
                float _FlowSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float transverse = pow(saturate(sin(input.uv.y * PI)), _ProfilePower);
                float sourceFade = smoothstep(0.10, 0.27, input.uv.x);
                float tailFade = 1.0 - smoothstep(0.68, 1.0, input.uv.x);
                float distanceFalloff = lerp(1.0, 0.32, input.uv.x);

                float3 p = input.positionWS * _NoiseScale;
                float time = _Time.y * _FlowSpeed;
                float n1 = sin(dot(p, float3(1.31, 0.77, 1.93)) + time);
                float n2 = sin(dot(p, float3(-0.63, 1.47, 0.91)) * 0.61 - time * 0.73);
                float n3 = sin(dot(p, float3(0.41, -0.82, 1.17)) * 1.37 + time * 0.41);
                float atmosphere = saturate(0.5 + (n1 + n2 + n3) * 0.115);
                float densityVariation = lerp(1.0 - _NoiseStrength, 1.0, atmosphere);

                float density = _BaseColor.a * transverse * sourceFade * tailFade * distanceFalloff * densityVariation;
                return half4(_BaseColor.rgb * density, 0.0);
            }
            ENDHLSL
        }
    }
}
