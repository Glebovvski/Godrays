Shader "Hidden/ScreenSpaceGodRaysBuiltIn"
{
    Properties
    {
        _MainTex("Source", 2D) = "white" {}

        [HideInInspector] _GodRaySamples("God Ray Samples", Float) = 3
        [HideInInspector] _DustEnabled("Enable Dust", Float) = 1

        _Noise("Noise", 2D) = "white" {}
        _NoiseStrength("Noise Strength", Range(0, 2)) = 1.0
        _NoiseThreshold("Noise Threshold", Range(0, 1)) = 0.7
        _NoiseSoftness("Noise Softness", Range(0.001, 0.5)) = 0.1

        _DustDistance("Dust Distance", Float) = 20.0
        _NoiseScale("Noise Scale", Float) = 0.5
        _NoiseSpeed("Noise Speed", Vector) = (0.02, 0.01, 0, 0)

        _DustMinStrength("Dust Min Strength", Range(0, 2)) = 0.03
        _DustMaxStrength("Dust Max Strength", Range(0, 5)) = 1.5
        _DustAngleStart("Dust Angle Start", Range(0, 1)) = 0.5
        _DustAngleEnd("Dust Angle End", Range(0, 1)) = 0.95
        _DustAnglePower("Dust Angle Power", Range(0.25, 8)) = 2.0

        _RayColor("Ray Color", Color) = (1.0, 0.9, 0.65, 1.0)
        _Intensity("Intensity", Range(0, 5)) = 1.0

        _Density("Density", Range(0.1, 1.5)) = 0.9
        _Decay("Decay", Range(0.5, 1.0)) = 0.94
        _Weight("Weight", Range(0.01, 1.0)) = 0.15
        _Exposure("Exposure", Range(0.01, 5.0)) = 1.0

        _MaxRayDistance("Max Ray Distance", Range(0.1, 20.0)) = 1.0
        _DepthThreshold("Depth Threshold", Range(0.9, 1.0)) = 0.995
        _DepthSoftness("Depth Softness", Range(0.00001, 0.05)) = 0.004
    }

    CGINCLUDE

    #include "UnityCG.cginc"
    #include "Assets/Shaders/Common/GodRaysCommon.hlsl"

    #if defined(_GODRAYSAMPLES_8)
        #define GOD_RAY_SAMPLES 8
    #elif defined(_GODRAYSAMPLES_12)
        #define GOD_RAY_SAMPLES 12
    #elif defined(_GODRAYSAMPLES_30)
        #define GOD_RAY_SAMPLES 30
    #elif defined(_GODRAYSAMPLES_100)
        #define GOD_RAY_SAMPLES 100
    #else
        #define GOD_RAY_SAMPLES 60
    #endif

    float4 _SunDirection;
    float4 _SunScreenPosition;
    float _SunVisible;

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;

    sampler2D _CameraDepthTexture;
    sampler2D _Noise;

    half _NoiseStrength;
    half _NoiseThreshold;
    half _NoiseSoftness;

    float _DustDistance;
    float _NoiseScale;
    float4 _NoiseSpeed;

    half _DustMinStrength;
    half _DustMaxStrength;
    half _DustAngleStart;
    half _DustAngleEnd;
    half _DustAnglePower;

    half4 _RayColor;
    half _Intensity;
    half _Density;
    half _Decay;
    half _DecaySunAngleKoef;
    half _Weight;
    half _Exposure;
    half _MaxRayDistance;

    float _DepthThreshold;
    float _DepthSoftness;

    float2 GetDepthUV(float2 uv)
    {
    #if UNITY_UV_STARTS_AT_TOP
        if (_MainTex_TexelSize.y < 0)
            uv.y = 1.0 - uv.y;
    #endif
        return uv;
    }

    float3 GetViewDirectionWS(float2 uv)
    {
        float2 ndc = uv * 2.0 - 1.0;
        float3 directionVS = float3(ndc.x / unity_CameraProjection._m00, ndc.y / unity_CameraProjection._m11, -1.0);
        return normalize(mul((float3x3)unity_CameraToWorld, normalize(directionVS)));
    }

    fixed4 OcclusionMaskFragment(v2f_img input) : SV_Target
    {
        float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, GetDepthUV(input.uv));
        float linearDepth = Linear01Depth(rawDepth);
        half mask = smoothstep(_DepthThreshold, _DepthThreshold + _DepthSoftness, linearDepth);

        return half4(mask, mask, mask, 1.0h);
    }

    fixed4 GodRaysFragment(v2f_img input) : SV_Target
    {
        if (_SunVisible < 0.5)
            return 0;

        float2 uv = input.uv;
        float2 sunUV = _SunScreenPosition.xy;
        float2 directionToSun = sunUV - uv;
        float distanceToSun = GodRaysAspectDistance(directionToSun, _ScreenParams.x / _ScreenParams.y);

        if (distanceToSun > _MaxRayDistance)
            return 0;

        float2 stepUV = directionToSun * (_Density / GOD_RAY_SAMPLES);
        float2 sampleUV = uv;

        half accumulated = 0.0h;
        half illuminationDecay = 1.0h;

    #if defined(_DUST_ON)
        float3 sunDirectionWS = normalize(_SunDirection.xyz);
    #endif

        [loop]
        for (int i = 0; i < GOD_RAY_SAMPLES; i++)
        {
            sampleUV += stepUV;

            half inside = GodRaysInsideScreen(sampleUV);
            float2 safeUV = saturate(sampleUV);

            half mask = tex2D(_MainTex, safeUV).r;

            accumulated += mask * inside * illuminationDecay * _Weight * _DecaySunAngleKoef;

        #if defined(_DUST_ON)
            float random = GodRaysRand((i + 0.5) / GOD_RAY_SAMPLES);

            float distance;
            float noiseScale;
            float speedRandom;
            GodRaysDustLayer(random, _DustDistance, _NoiseScale, distance, noiseScale, speedRandom);

            float3 viewDirectionWS = GetViewDirectionWS(safeUV);
            float3 dustPositionWS = _WorldSpaceCameraPos + viewDirectionWS * distance;

            float2 noiseUV = dustPositionWS.xz * noiseScale;
            noiseUV += _Time.y * _NoiseSpeed.xy * speedRandom;

            half dustStrength = GodRaysDustStrength(viewDirectionWS, sunDirectionWS, _DustMinStrength, _DustMaxStrength, _DustAngleStart, _DustAngleEnd, _DustAnglePower) * _NoiseStrength;
            half noise = tex2D(_Noise, noiseUV).r;
            half dust = smoothstep(_NoiseThreshold, _NoiseThreshold + _NoiseSoftness, noise);

            dust *= dustStrength * mask * inside * illuminationDecay * _Weight * _DecaySunAngleKoef;
            accumulated += dust;
        #endif

            illuminationDecay *= _Decay;
        }

        half rays = accumulated * _Exposure;
        return half4(rays, rays, rays, 1.0h);
    }

    fixed4 CompositeFragment(v2f_img input) : SV_Target
    {
        half rays = tex2D(_MainTex, input.uv).r;
        return half4(rays * _RayColor.rgb * _Intensity * _SunVisible, 0.0h);
    }

    ENDCG

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "Occlusion Mask"

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment OcclusionMaskFragment
            ENDCG
        }

        Pass
        {
            Name "God Rays"

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment GodRaysFragment
            #pragma shader_feature_local _DUST_ON
            #pragma shader_feature_local _GODRAYSAMPLES_8 _GODRAYSAMPLES_12 _GODRAYSAMPLES_30 _GODRAYSAMPLES_60 _GODRAYSAMPLES_100
            ENDCG
        }

        Pass
        {
            Name "Composite"
            Blend One One

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment CompositeFragment
            ENDCG
        }
    }

    CustomEditor "ScreenSpaceGodRaysShaderGUI"
    Fallback Off
}