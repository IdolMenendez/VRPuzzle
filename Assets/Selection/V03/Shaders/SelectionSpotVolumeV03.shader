Shader "Hidden/PersonallyPuzzled/SelectionSpotVolumeV03"
{
    Properties
    {
        _BaseColor ("Warm museum volume", Color) = (1.0, 0.66, 0.28, 0.028)
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
            Name "SelectionSpotVolume"
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

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
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half verticalFeather = smoothstep(0.0h, 0.14h, input.uv.y) * (1.0h - 0.42h * input.uv.y);
                half pulse = 0.90h + 0.10h * sin(input.uv.y * 19.0h);
                return half4(_BaseColor.rgb, _BaseColor.a * verticalFeather * pulse);
            }
            ENDHLSL
        }
    }
}
