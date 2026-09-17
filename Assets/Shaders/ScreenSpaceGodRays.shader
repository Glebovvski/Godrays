Shader "Hidden/ScreenSpaceGodRays"
{
    Properties
    {
        _RayColor("Ray Color", Color) = (1.0, 0.9, 0.65, 1.0)

        _Intensity("Intensity", Range(0, 5)) = 1.0

        _Density("Density", Range(0.1, 1.5)) = 0.9
        _Decay("Decay", Range(0.5, 1.0)) = 0.94
        _Weight("Weight", Range(0.01, 1.0)) = 0.15
        _Exposure("Exposure", Range(0.01, 5.0)) = 1.0

        _MaxRayDistance("Max Ray Distance", Range(0.1, 2.0)) = 1.0

        _DepthThreshold("Depth Threshold", Range(0.9, 1.0)) = 0.995
        _DepthSoftness("Depth Softness", Range(0.00001, 0.05)) = 0.004
    }

    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    #define GOD_RAY_SAMPLES 100

    float4 _SunScreenPosition;
    float _SunVisible;

    TEXTURE2D_X(_GodRaysTexture);

    CBUFFER_START(UnityPerMaterial)

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


    // =========================================================
    // PASS 0
    // DEPTH -> OCCLUSION MASK
    //
    // white = sky
    // black = geometry
    // =========================================================

    half4 OcclusionMaskFragment(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float rawDepth = SampleSceneDepth(input.texcoord);

        float linearDepth = Linear01Depth(rawDepth, _ZBufferParams);

        half mask = smoothstep(_DepthThreshold, _DepthThreshold + _DepthSoftness, linearDepth);

        return half4(mask, mask, mask, 1.0h);
    }


    // =========================================================
    // PASS 1
    // LOW RES OCCLUSION MASK -> LOW RES GOD RAYS
    // =========================================================

    half4 GodRaysFragment(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        if (_SunVisible < 0.5)
            return 0;

        float2 uv = input.texcoord;
        float2 sunUV = _SunScreenPosition.xy;

        float2 directionToSun = sunUV - uv;

        // Aspect-correct distance test.
        float2 aspectDirection =  directionToSun;

        aspectDirection.x *= _ScreenParams.x / _ScreenParams.y;

        float distanceToSun = length(aspectDirection);

        // Don't ray march pixels too far away.
        if (distanceToSun > _MaxRayDistance)
            return 0;

        float2 stepUV = directionToSun * (_Density / GOD_RAY_SAMPLES);

        float2 sampleUV = uv;

        half accumulated = 0.0h;
        half illuminationDecay = 1.0h;

        [unroll]
        for (int i = 0; i < GOD_RAY_SAMPLES; i++)
        {
            sampleUV += stepUV;

            // Avoid branches inside the loop.
            float2 lower =  step(float2(0.0, 0.0), sampleUV);

            float2 upper = step(sampleUV, float2(1.0, 1.0));

            half inside = (half)(lower.x * lower.y * upper.x * upper.y);

            float2 safeUV = saturate(sampleUV);

            half mask = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, safeUV).r;

            accumulated += mask * inside * illuminationDecay * _Weight*_DecaySunAngleKoef;
            
            illuminationDecay *= _Decay;
        }

        half rays = accumulated * _Exposure;

        return half4(rays, rays, rays, 1.0h);
    }


    // =========================================================
    // PASS 2
    // FULL RES CAMERA + LOW RES GOD RAYS
    // =========================================================

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
}