Shader "Skybox/God Rays Built-In"
{
    Properties
    {
        _TopColor("Top Color", Color) = (0.15, 0.35, 0.65, 1)
        _BottomColor("Bottom Color", Color) = (0.8, 0.65, 0.45, 1)
        _HorizonColor("Horizon Color", Color) = (0.9, 0.65, 0.4, 1)
        _HorizonSharpness("Horizon Sharpness", Range(0.1, 10)) = 2

        _SunColor("Sun Color", Color) = (1, 0.9, 0.65, 1)
        _SunSize("Sun Size", Range(0.0001, 0.05)) = 0.005
        _SunGlowPower("Sun Glow Power", Range(1, 256)) = 32
        _SunGlowIntensity("Sun Glow Intensity", Range(0, 10)) = 1
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
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"

            float4 _SunDirection;

            half4 _TopColor;
            half4 _BottomColor;
            half4 _HorizonColor;
            half _HorizonSharpness;

            half4 _SunColor;
            half _SunSize;
            half _SunGlowPower;
            half _SunGlowIntensity;

            struct Attributes
            {
                float4 vertex : POSITION;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 directionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.directionWS = mul((float3x3)unity_ObjectToWorld, input.vertex.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 directionWS = normalize(input.directionWS);
                float3 sunDirectionWS = normalize(_SunDirection.xyz);

                half skyT = saturate(directionWS.y * 0.5 + 0.5);
                half4 sky = lerp(_BottomColor, _TopColor, skyT);

                half horizon = 1.0h - saturate(abs(directionWS.y));
                horizon = pow(horizon, _HorizonSharpness);
                sky = lerp(sky, _HorizonColor, horizon);

                half sunDot = saturate(dot(directionWS, sunDirectionWS));
                half sunDisk = smoothstep(1.0h - _SunSize, 1.0h - _SunSize * 0.15h, sunDot);
                half sunGlow = pow(sunDot, _SunGlowPower) * _SunGlowIntensity;

                sky.rgb += _SunColor.rgb * (sunDisk + sunGlow);

                return half4(sky.rgb, 1.0h);
            }
            ENDCG
        }
    }

    Fallback Off
}