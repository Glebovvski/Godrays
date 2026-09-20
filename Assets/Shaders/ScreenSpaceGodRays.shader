Shader "Hidden/ScreenSpaceGodRays"
{
    Properties
    {
        [HideInInspector] _GodRaySamples("God Ray Samples", Float) = 3

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

    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

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

    TEXTURE2D_X(_GodRaysTexture);
    TEXTURE2D(_Noise);
    SAMPLER(sampler_Noise);

    CBUFFER_START(UnityPerMaterial)
    float4 _Noise_ST;
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
    CBUFFER_END

    half4 OcclusionMaskFragment(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float rawDepth = SampleSceneDepth(input.texcoord);
        float linearDepth = Linear01Depth(rawDepth, _ZBufferParams);
        half mask = smoothstep(_DepthThreshold, _DepthThreshold + _DepthSoftness, linearDepth);

        return half4(mask, mask, mask, 1.0h);
    }

    float3 GetViewDirectionWS(float2 uv)
    {
    #if UNITY_REVERSED_Z
        float rawDepth = 0.0;
    #else
        float rawDepth = 1.0;
    #endif

        float3 farPositionWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
        return normalize(farPositionWS - _WorldSpaceCameraPos);
    }

    half ComputeSchlickPhase(half cosTheta, half g)
    {
        half g2 = g * g;
        half denom = 1.0 + g * cosTheta;
        return (1.0 - g2) / (4.0 * 3.14159265 * denom * denom);
    }

    float Rand(float seed)
    {
        return frac(sin(seed * 12.9898) * 43758.5453);
    }

    half4 GodRaysFragment(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        if (_SunVisible < 0.5)
            return 0;

        float2 uv = input.texcoord;
        float2 sunUV = _SunScreenPosition.xy;
        float2 directionToSun = sunUV - uv;

        float2 aspectDirection = directionToSun;
        aspectDirection.x *= _ScreenParams.x / _ScreenParams.y;

        float distanceToSun = length(aspectDirection);

        if (distanceToSun > _MaxRayDistance)
            return 0;

        float2 stepUV = directionToSun * (_Density / GOD_RAY_SAMPLES);
        float2 sampleUV = uv;

        half accumulated = 0.0h;
        half illuminationDecay = 1.0h;

        float3 sunDirectionWS = normalize(_SunDirection.xyz);

        [unroll]
        for (int i = 0; i < GOD_RAY_SAMPLES; i++)
        {
            sampleUV += stepUV;

            float2 lower = step(float2(0.0, 0.0), sampleUV);
            float2 upper = step(sampleUV, float2(1.0, 1.0));
            half inside = (half)(lower.x * lower.y * upper.x * upper.y);

            float2 safeUV = saturate(sampleUV);
            half mask = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, safeUV).r;

            half rayContribution = mask * inside * illuminationDecay * _Weight * _DecaySunAngleKoef;

            float noiseStep = (i + 0.5) / GOD_RAY_SAMPLES;
            float random = Rand(noiseStep);

            float distance = _DustDistance * lerp(0.15, 1.0, random);
            float noiseScale = _NoiseScale * lerp(0.01, 1.5, random);
            float speedRandom = lerp(0.5, 1.5, random);

            float3 viewDirectionWS = GetViewDirectionWS(safeUV);
            float3 dustPositionWS = _WorldSpaceCameraPos + viewDirectionWS * distance;

            float2 noiseUV = dustPositionWS.xz * noiseScale;
            noiseUV += _Time.y * _NoiseSpeed.xy * speedRandom;

            float angleAlignment = saturate(dot(viewDirectionWS, sunDirectionWS));
            float angleStrength = smoothstep(_DustAngleStart, _DustAngleEnd, angleAlignment);
            angleStrength = pow(angleStrength, _DustAnglePower);

            float dustStrength = lerp(_DustMinStrength, _DustMaxStrength, angleStrength) * _NoiseStrength;

            half noise = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, noiseUV).r;
            half dust = smoothstep(_NoiseThreshold, _NoiseThreshold + _NoiseSoftness, noise);
            dust *= dustStrength * mask * inside * illuminationDecay * _Weight * _DecaySunAngleKoef;

            accumulated += rayContribution + dust;
            illuminationDecay *= _Decay;
        }

        half rays = accumulated * _Exposure;
        return half4(rays, rays, rays, 1.0h);
    }

    half4 CompositeFragment(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float2 uv = input.texcoord;
        half3 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
        half rays = SAMPLE_TEXTURE2D_X(_GodRaysTexture, sampler_LinearClamp, uv).r;

        sceneColor += rays * _RayColor.rgb * _Intensity * _SunVisible;
        return half4(sceneColor, 1.0h);
    }

    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Occlusion Mask"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment OcclusionMaskFragment
            ENDHLSL
        }

        Pass
        {
            Name "God Rays"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment GodRaysFragment
            #pragma shader_feature_local_fragment _GODRAYSAMPLES_8 _GODRAYSAMPLES_12 _GODRAYSAMPLES_30 _GODRAYSAMPLES_60 _GODRAYSAMPLES_100
            ENDHLSL
        }

        Pass
        {
            Name "Composite"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CompositeFragment
            ENDHLSL
        }
    }

    CustomEditor "ScreenSpaceGodRaysShaderGUI"
}