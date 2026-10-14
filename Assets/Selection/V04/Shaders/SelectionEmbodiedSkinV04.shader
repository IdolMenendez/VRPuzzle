Shader "Hidden/PersonallyPuzzled/SelectionEmbodiedSkinV04"
{
    Properties
    {
        [MainTexture] _BaseMap ("Meshy skin atlas", 2D) = "white" {}
        _BaseColor ("Warm embodied skin", Color) = (0.69, 0.36, 0.27, 1)
        _RimColor ("Warm subsurface rim", Color) = (1.0, 0.42, 0.23, 1)
        _FormContrast ("Form contrast", Range(0, 1)) = 0.58
        _RimStrength ("Rim strength", Range(0, 0.25)) = 0.075
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
            Name "SelectionEmbodiedSkin"
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _RimColor;
                half _FormContrast;
                half _RimStrength;
            CBUFFER_END
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                half3 viewWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionHCS = positionInputs.positionCS;
                output.normalWS = normalize(normalInputs.normalWS);
                output.viewWS = SafeNormalize(GetWorldSpaceViewDir(positionInputs.positionWS));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewWS = normalize(input.viewWS);
                half3 keyDirection = normalize(half3(-0.42h, 0.70h, -0.56h));
                half key = saturate(dot(normalWS, keyDirection));
                half sky = saturate(normalWS.y * 0.5h + 0.5h);
                half facing = saturate(dot(normalWS, viewWS));
                half rim = pow(1.0h - facing, 2.8h);
                half wrappedKey = saturate((dot(normalWS, keyDirection) + 0.34h) / 1.34h);
                half form = lerp(0.72h, 0.42h + wrappedKey * 0.40h + sky * 0.18h, _FormContrast);
                half3 atlas = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half3 color = atlas * _BaseColor.rgb * form;
                color += _RimColor.rgb * rim * _RimStrength;
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
