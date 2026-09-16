Shader "Maid'n'Mate/SunSky"
{
    Properties
    {
        _TopColor("Top Color", Color) = (0.2, 0.5, 1, 1)
        _BottomColor("Bottom Color", Color) = (0.8, 0.9, 1, 1)

        _SunColor("Sun Color", Color) = (1, 0.9, 0.6, 1)

        _SunSize("Sun Size", Range(0.0001, 0.1)) = 0.01
        _SunSoftness("Sun Softness", Range(0.0001, 0.05)) = 0.002

        _SunGlowSize("Sun Glow Size", Range(0.001, 0.5)) = 0.08
        _SunGlowStrength("Sun Glow Strength", Range(0, 5)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDir : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)

            float4 _TopColor;
            float4 _BottomColor;

            float4 _SunColor;
            float4 _SunDirection;

            float _SunSize;
            float _SunSoftness;

            float _SunGlowSize;
            float _SunGlowStrength;

            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;

                output.positionCS = TransformObjectToHClip(input.positionOS);

                float3 worldPosition = TransformObjectToWorld(input.positionOS);
                output.viewDir = worldPosition - _WorldSpaceCameraPos;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 viewDir = normalize(input.viewDir);
                float3 sunDir = normalize(_SunDirection.xyz);

                // Sky gradient.
                float gradient = saturate(viewDir.y * 0.5 + 0.5);
                float3 skyColor = lerp(
                    _BottomColor.rgb,
                    _TopColor.rgb,
                    gradient);

                // Angular distance from the sun.
                float sunDistance = 1.0 - saturate(dot(viewDir, sunDir));

                // Solid sun disk.
                float sun = 1.0 - smoothstep(
                    _SunSize,
                    _SunSize + _SunSoftness,
                    sunDistance);

                // Larger soft glow.
                float glow = 1.0 - smoothstep(
                    0.0,
                    _SunGlowSize,
                    sunDistance);

                float3 color = skyColor;

                color += _SunColor.rgb * sun;
                color += _SunColor.rgb * glow * _SunGlowStrength;

                return half4(color, 1.0);
            }

            ENDHLSL
        }
    }
}