Shader "PersonallyPuzzled/V10SemanticModuleClipGhost"
{
    Properties
    {
        _EdgeColor ("Edge Tint", Color) = (0.04,0.82,1,1)
        _InteriorAlpha ("Interior Alpha", Range(0,0.2)) = 0.035
        _EdgeAlpha ("Edge Alpha", Range(0,0.8)) = 0.58
        _FresnelPower ("Edge Tightness", Range(1,8)) = 2.4
        _ClipMin ("Clip Min", Vector) = (-100,-100,-100,0)
        _ClipMax ("Clip Max", Vector) = (100,100,100,0)
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+30" "RenderType"="Transparent" }
        Pass
        {
            Name "CyanSemanticModuleGhost"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 positionOS:TEXCOORD0;
                float3 normalWS:TEXCOORD1;
                float3 viewWS:TEXCOORD2;
            };
            CBUFFER_START(UnityPerMaterial)
                half4 _EdgeColor;
                half _InteriorAlpha;
                half _EdgeAlpha;
                half _FresnelPower;
                float4 _ClipMin;
                float4 _ClipMax;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = GetWorldSpaceViewDir(positionWS);
                return output;
            }

            half4 Frag(Varyings input):SV_Target
            {
                if (any(input.positionOS < _ClipMin.xyz) || any(input.positionOS > _ClipMax.xyz))
                    discard;
                half fresnel = pow(1.0h - saturate(abs(dot(normalize(input.normalWS), normalize(input.viewWS)))), _FresnelPower);
                half alpha = lerp(_InteriorAlpha, _EdgeAlpha, fresnel);
                half3 color = lerp(_EdgeColor.rgb * 0.28h, _EdgeColor.rgb, fresnel);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
