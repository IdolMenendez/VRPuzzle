Shader "Hidden/PersonallyPuzzled/ReferenceCaptureGradeV07"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _ChannelMultiply ("Channel multiply", Vector) = (1.30, 1.02, 0.68, 1)
        _Contrast ("Contrast", Range(0.5, 2.0)) = 1.22
        _Pivot ("Contrast pivot", Range(0.0, 1.0)) = 0.22
        _BlurPixels ("Photographic softness", Range(0.0, 3.0)) = 1.25
        _BeamColor ("Golden beam", Color) = (1.0, 0.44, 0.10, 1)
        _BeamStrength ("Golden beam strength", Range(0.0, 0.2)) = 0.045
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float4 _ChannelMultiply;
            float _Contrast;
            float _Pivot;
            float _BlurPixels;
            float4 _BeamColor;
            float _BeamStrength;

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                float2 offset = _MainTex_TexelSize.xy * _BlurPixels;
                float4 source = tex2D(_MainTex, input.uv) * 0.40;
                source += tex2D(_MainTex, input.uv + float2(offset.x, 0)) * 0.15;
                source += tex2D(_MainTex, input.uv - float2(offset.x, 0)) * 0.15;
                source += tex2D(_MainTex, input.uv + float2(0, offset.y)) * 0.15;
                source += tex2D(_MainTex, input.uv - float2(0, offset.y)) * 0.15;
                float2 beamStart = float2(0.00, 0.84);
                float2 beamEnd = float2(0.70, 0.22);
                float2 beamAxis = beamEnd - beamStart;
                float t = saturate(dot(input.uv - beamStart, beamAxis) / dot(beamAxis, beamAxis));
                float distanceToBeam = length((input.uv - beamStart) - beamAxis * t);
                float beam = pow(saturate(1.0 - distanceToBeam / 0.34), 2.0);
                beam *= smoothstep(0.0, 0.10, t) * (1.0 - smoothstep(0.86, 1.0, t));
                float sourceLuma = dot(source.rgb, float3(0.2126, 0.7152, 0.0722));
                source.rgb += _BeamColor.rgb * beam * _BeamStrength * (1.0 - saturate(sourceLuma * 1.4));
                float3 perceptual = LinearToGammaSpace(saturate(source.rgb * _ChannelMultiply.rgb));
                perceptual = (perceptual - _Pivot.xxx) * _Contrast.xxx + _Pivot.xxx;
                float3 graded = GammaToLinearSpace(saturate(perceptual));
                return float4(graded, source.a);
            }
            ENDCG
        }
    }
}
