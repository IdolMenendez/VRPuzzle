Shader "Hidden/PersonallyPuzzled/SelectionOpticalGlassV03"
{
    Properties
    {
        _BaseColor ("Optical glass tint", Color) = (0.78, 0.86, 0.92, 0.038)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }
        Pass
        {
            Name "SelectionOpticalGlass"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half rim : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                half3 viewDirection = SafeNormalize(GetWorldSpaceViewDir(positionInputs.positionWS));
                output.positionHCS = positionInputs.positionCS;
                output.rim = pow(1.0h - saturate(abs(dot(normalInputs.normalWS, viewDirection))), 2.5h);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half alpha = _BaseColor.a * (0.52h + input.rim * 1.8h);
                half3 color = lerp(_BaseColor.rgb, half3(1.0h, 0.96h, 0.82h), input.rim * 0.28h);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
