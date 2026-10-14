Shader "PersonallyPuzzled/SoftTyndallBeam"
{
    Properties
    {
        _BaseColor ("Sunshaft Color", Color) = (1.0, 0.78, 0.42, 0.05)
        _EdgeSoftness ("Edge Softness", Range(0.05, 0.48)) = 0.24
        _NoiseScale ("Dust Noise Scale", Range(0.2, 8.0)) = 2.6
        _NoiseStrength ("Dust Noise Strength", Range(0.0, 0.8)) = 0.24
        _FlowSpeed ("Dust Flow Speed", Range(-1.0, 1.0)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent+20"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "SoftTyndallBeam"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
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
                float _EdgeSoftness;
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
                float edgeA = smoothstep(0.0, _EdgeSoftness, input.uv.y);
                float edgeB = smoothstep(0.0, _EdgeSoftness, 1.0 - input.uv.y);
                float startFade = smoothstep(0.0, 0.10, input.uv.x);
                float endFade = 1.0 - smoothstep(0.70, 1.0, input.uv.x);

                float phaseA = dot(input.positionWS, float3(1.73, 2.19, 1.11)) * _NoiseScale + _Time.y * _FlowSpeed;
                float phaseB = dot(input.positionWS, float3(-1.21, 0.83, 2.47)) * (_NoiseScale * 0.63) - _Time.y * _FlowSpeed * 0.71;
                float noise = saturate(sin(phaseA) * 0.5 + sin(phaseB) * 0.5);
                float density = lerp(1.0 - _NoiseStrength, 1.0, noise);
                float alpha = _BaseColor.a * edgeA * edgeB * startFade * endFade * density;
                return half4(_BaseColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
