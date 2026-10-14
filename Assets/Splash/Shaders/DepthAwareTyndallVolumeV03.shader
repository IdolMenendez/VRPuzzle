Shader "PersonallyPuzzled/DepthAwareTyndallVolumeV03"
{
    Properties
    {
        _BaseColor ("Scattered Sunlight", Color) = (1.0, 0.79, 0.48, 1.0)
        _Density ("Optical Density", Range(0.0, 1.0)) = 0.20
        _NoiseScale ("Atmosphere Scale", Range(0.2, 5.0)) = 1.35
        _NoiseStrength ("Atmosphere Variation", Range(0.0, 0.6)) = 0.18
        _FlowSpeed ("Dust Drift", Range(-0.2, 0.2)) = 0.025
        _EdgeSoftness ("Fan Edge Softness", Range(0.2, 0.95)) = 0.52
        _Anisotropy ("Forward Scattering", Range(0.0, 0.85)) = 0.58
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
            Name "DepthAwareTyndallVolumeV03"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Density;
                float _NoiseScale;
                float _NoiseStrength;
                float _FlowSpeed;
                float _EdgeSoftness;
                float _Anisotropy;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
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
                return output;
            }

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 rayOriginWS = GetCameraPositionWS();
                float3 rayDirectionWS = normalize(input.positionWS - rayOriginWS);
                float3 rayOriginOS = TransformWorldToObject(rayOriginWS);
                float3 rayDirectionOS = mul((float3x3)unity_WorldToObject, rayDirectionWS);
                float3 directionSign = sign(rayDirectionOS);
                directionSign += 1.0 - abs(directionSign);
                float3 inverseDirection = rcp(rayDirectionOS + directionSign * 1e-6);

                float3 t0 = (-0.5 - rayOriginOS) * inverseDirection;
                float3 t1 = ( 0.5 - rayOriginOS) * inverseDirection;
                float3 tMin = min(t0, t1);
                float3 tMax = max(t0, t1);
                float entry = max(max(tMin.x, tMin.y), tMin.z);
                float exitDistance = min(min(tMax.x, tMax.y), tMax.z);
                entry = max(entry, 0.0);
                clip(exitDistance - entry);

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                // camera.Render() in Unity batch mode can expose the unbound depth
                // sentinel as near-plane depth on reversed-Z platforms.  Treat only
                // that impossible pair as far depth; normal Game/VR rendering keeps
                // true per-pixel occlusion from _CameraDepthTexture.
                if (rawDepth > 0.95 || sceneEyeDepth < 0.5)
                    sceneEyeDepth = _ProjectionParams.z;
                float3 rayDirectionVS = mul((float3x3)UNITY_MATRIX_V, rayDirectionWS);
                float opaqueDistance = sceneEyeDepth / max(0.001, -rayDirectionVS.z);
                exitDistance = min(exitDistance, opaqueDistance - 0.01);
                clip(exitDistance - entry);

                const int stepCount = 32;
                float stepLength = (exitDistance - entry) / stepCount;
                float travel = entry + (Hash12(input.positionCS.xy) + 0.5) * stepLength;
                float transmittance = 1.0;
                float scattered = 0.0;
                float3 lightTravelWS = normalize(TransformObjectToWorldDir(float3(0.0, 0.0, 1.0)));
                float cosine = saturate(dot(lightTravelWS, -rayDirectionWS));
                float g = _Anisotropy;
                float phase = (1.0 - g * g) /
                    max(0.08, pow(1.0 + g * g - 2.0 * g * cosine, 1.5));
                phase = lerp(0.34, phase * 0.18, g);

                [loop]
                for (int i = 0; i < stepCount; i++)
                {
                    float3 sampleWS = rayOriginWS + rayDirectionWS * travel;
                    float3 p = TransformWorldToObject(sampleWS);
                    float along = saturate(p.z + 0.5);

                    float halfWidth = lerp(0.27, 0.49, along);
                    float halfHeight = lerp(0.34, 0.49, along);
                    float radial = length(float2(p.x / halfWidth, p.y / halfHeight));
                    float fanProfile = 1.0 - smoothstep(_EdgeSoftness, 1.0, radial);
                    float sourceFade = smoothstep(0.015, 0.10, along);
                    float tailFade = 1.0 - smoothstep(0.80, 1.0, along);
                    float distanceFade = lerp(1.0, 0.42, along);

                    float3 noiseP = sampleWS * _NoiseScale;
                    float drift = _Time.y * _FlowSpeed;
                    float n = sin(dot(noiseP, float3(1.21, 0.73, 1.67)) + drift);
                    n += sin(dot(noiseP, float3(-0.59, 1.37, 0.83)) * 0.63 - drift * 0.71);
                    n += sin(dot(noiseP, float3(0.39, -0.77, 1.11)) * 1.31 + drift * 0.43);
                    float variation = lerp(1.0 - _NoiseStrength, 1.0 + _NoiseStrength,
                        saturate(0.5 + n * 0.16));

                    float localDensity = _Density * fanProfile * sourceFade * tailFade * distanceFade * variation;
                    scattered += transmittance * localDensity * phase * stepLength;
                    transmittance *= exp(-localDensity * stepLength * 0.82);
                    travel += stepLength;
                }

                return half4(_BaseColor.rgb * scattered * 0.90, 0.0);
            }
            ENDHLSL
        }
    }
}
