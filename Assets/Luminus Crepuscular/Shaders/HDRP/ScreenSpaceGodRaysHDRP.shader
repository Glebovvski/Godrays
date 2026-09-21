Shader "Custom/HDRP/ScreenSpaceGodRays"
{
    Properties
    {
        [HDR] _RayColor ("Ray Color", Color) = (1, 0.95, 0.8, 1)
        _Exposure ("Ray Exposure", Range(0, 10)) = 1
        _Weight ("Ray Weight", Range(0, 5)) = 1
        _Density ("Ray Length", Range(0, 1)) = 0.95
        _Decay ("Decay", Range(0, 1)) = 0.96
        [HideInInspector] _GodRaySamples ("Samples", Float) = 3
        _SunSourceRadius ("Sun Source Radius", Range(0.01, 2)) = 0.5
        _SunSourcePower ("Sun Source Falloff", Range(0.1, 8)) = 1.5
        _ExposureWeight ("HDRP Exposure Weight", Range(0, 1)) = 0

        [HideInInspector] _Dust ("Enable Dust", Float) = 0
        _DustIntensity ("Dust Intensity", Range(0, 20)) = 4
        _DustScale ("Dust Cells Per Meter", Range(0.1, 20)) = 2
        _DustSize ("Dust Radius In Cell", Range(0.01, 0.2)) = 0.08
        _DustDensity ("Dust Cell Occupancy", Range(0, 1)) = 0.3
        _DustDistance ("Dust Distance", Range(1, 100)) = 12
        _DustDepthFade ("Dust Depth Fade", Range(0.01, 5)) = 0.5
        _DustAnisotropy ("Dust Forward Scattering", Range(0, 0.95)) = 0.8
        _DustVelocity ("Dust Velocity In Meters Per Second", Vector) = (0.03, 0.02, 0, 0)
    }

    HLSLINCLUDE
    #pragma target 4.5
    #pragma vertex Vert
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"
    #include "Assets/Luminus Crepuscular/Shaders/Common/GodRaysCommon.hlsl"

    TEXTURE2D_X(_OcclusionTexture);
    TEXTURE2D_X(_GodRaysTexture);

    CBUFFER_START(UnityPerMaterial)
        float4 _RayColor;
        float4 _DustVelocity;
        float _Exposure;
        float _Weight;
        float _Density;
        float _Decay;
        float _GodRaySamples;
        float _SunSourceRadius;
        float _SunSourcePower;
        float _ExposureWeight;
        float _Dust;
        float _DustIntensity;
        float _DustScale;
        float _DustSize;
        float _DustDensity;
        float _DustDistance;
        float _DustDepthFade;
        float _DustAnisotropy;
    CBUFFER_END

    // Supplied per draw by GodRaysHDRPCustomPass, never by Camera.main.
    float4 _SunDirection;
    float _SunVisible;
    float _DecaySunAngleKoef;
    float _GodRaysPassIntensity;
    float4 _GodRaysTargetSize;       // xy = viewport size, zw = inverse size.
    float4 _OcclusionTextureScale;  // xy = viewport / allocation, zw = allocation texel size.
    float4 _GodRaysTextureScale;

    float2 GodRaysBufferUV(float2 uv, float4 scale)
    {
        float2 halfTexel = scale.zw * 0.5;
        return clamp(uv * scale.xy, halfTexel, scale.xy - halfTexel);
    }

    float GodRaysIsSky(float rawDepth)
    {
        return rawDepth == UNITY_RAW_FAR_CLIP_VALUE ? 1.0 : 0.0;
    }

    float GodRaysLoadDepth(float2 uv)
    {
        uint2 pixel = min(uint2(uv * _ScreenSize.xy), uint2(_ScreenSize.xy) - 1);
        return LoadCameraDepth(pixel);
    }

    float2 GodRaysSunScreenPosition(out float visible)
    {
        // w = 0 projects a direction at infinity. Camera translation is excluded.
        // HDRP supplies the current camera/eye's GPU matrices, including TAA jitter.
        float4 sunCS = mul(UNITY_MATRIX_VP, float4(_SunDirection.xyz, 0.0));
        visible = sunCS.w > 0.0001 ? _SunVisible : 0.0;
        float2 uv = sunCS.xy / max(sunCS.w, 0.0001);
        #if UNITY_UV_STARTS_AT_TOP
            uv.y = -uv.y;
        #endif
        return uv * 0.5 + 0.5;
    }

    // HDRP dust keeps its original calculations. Distinct names avoid the URP
    // dust helpers and opposite Schlick sign convention in GodRaysCommon.hlsl.
    float3 GodRaysHash3(float3 p)
    {
        float3 q = float3(dot(p, float3(127.1, 311.7, 74.7)),
                          dot(p, float3(269.5, 183.3, 246.1)),
                          dot(p, float3(113.5, 271.9, 124.6)));
        return frac(sin(q) * 43758.5453);
    }

    float GodRaysHDRPDustLayer(float3 positionWS)
    {
        float3 p = (positionWS - _Time.y * _DustVelocity.xyz) * max(_DustScale, 0.001);
        float3 cell = floor(p);
        float3 center = 0.25 + GodRaysHash3(cell) * 0.5;
        float distanceToParticle = length(frac(p) - center);
        float radius = clamp(_DustSize, 0.01, 0.2);
        float edge = max(fwidth(distanceToParticle), 0.001);
        float particle = 1.0 - smoothstep(radius - edge, radius + edge, distanceToParticle);
        float occupied = GodRaysHash3(cell + 19.19).x < saturate(_DustDensity) ? 1.0 : 0.0;
        return particle * occupied;
    }

    float GodRaysHDRPComputeSchlickPhase(float cosTheta, float k)
    {
        float denominator = max(1.0 - k * cosTheta, 0.001);
        return (1.0 - k * k) / (12.5663706144 * denominator * denominator);
    }

    float GodRaysHDRPAngleStrength(float3 rayDirectionWS)
    {
        float k = clamp(_DustAnisotropy, 0.0, 0.95);
        float cosTheta = clamp(dot(rayDirectionWS, _SunDirection.xyz), -1.0, 1.0);
        // Normalize the forward peak so narrowing the lobe doesn't blow out the dust.
        return GodRaysHDRPComputeSchlickPhase(cosTheta, k) / GodRaysHDRPComputeSchlickPhase(1.0, k);
    }

    float GodRaysHDRPDustStrength(float2 uv)
    {
        float3 cameraRWS = GetCurrentViewPosition();
        float3 pointRWS = ComputeWorldSpacePosition(uv, 0.5, UNITY_MATRIX_I_VP);
        float3 rayDirectionWS = normalize(pointRWS - cameraRWS);
        float3 cameraWS = GetAbsolutePositionWS(cameraRWS);
        float rawDepth = GodRaysLoadDepth(uv);
        float sceneDistance = _DustDistance + _DustDepthFade;

        if (GodRaysIsSky(rawDepth) < 0.5)
        {
            float3 sceneRWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
            sceneDistance = length(sceneRWS - cameraRWS);
        }

        float dust = 0.0;
        [unroll]
        for (int layer = 0; layer < 4; layer++)
        {
            float distanceAlongRay = (layer + 0.5) * (_DustDistance * 0.25);
            float3 positionWS = cameraWS + rayDirectionWS * distanceAlongRay;
            float depthFade = saturate((sceneDistance - distanceAlongRay) / max(_DustDepthFade, 0.001));
            dust += GodRaysHDRPDustLayer(positionWS) * depthFade;
        }

        return dust * GodRaysHDRPAngleStrength(rayDirectionWS) * _DustIntensity;
    }

    float4 OcclusionPass(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.positionCS.xy * _GodRaysTargetSize.zw;
        float visible;
        float2 sunUV = GodRaysSunScreenPosition(visible);
        float distanceToSun = GodRaysAspectDistance(uv - sunUV, _ScreenSize.x / _ScreenSize.y);
        float source = saturate(1.0 - distanceToSun / max(_SunSourceRadius, 0.0001));
        source = pow(source, max(_SunSourcePower, 0.01));
        float mask = GodRaysIsSky(GodRaysLoadDepth(uv)) * source * visible;
        return float4(mask, 0.0, 0.0, 0.0);
    }

    float4 RaysPass(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        #if defined(_GODRAYSAMPLES_8)
            #define GODRAYS_SAMPLE_COUNT 8
        #elif defined(_GODRAYSAMPLES_12)
            #define GODRAYS_SAMPLE_COUNT 12
        #elif defined(_GODRAYSAMPLES_30)
            #define GODRAYS_SAMPLE_COUNT 30
        #elif defined(_GODRAYSAMPLES_100)
            #define GODRAYS_SAMPLE_COUNT 100
        #else
            #define GODRAYS_SAMPLE_COUNT 60
        #endif

        float2 uv = input.positionCS.xy * _GodRaysTargetSize.zw;
        float visible;
        float2 sunUV = GodRaysSunScreenPosition(visible);
        float2 stepUV = (sunUV - uv) * saturate(_Density) / GODRAYS_SAMPLE_COUNT;
        float2 sampleUV = uv + stepUV * 0.5;
        float decayPerStep = pow(saturate(_Decay), 60.0 / GODRAYS_SAMPLE_COUNT);
        float illumination = 1.0;
        float weightSum = 0.0;
        float sum = 0.0;

        [loop]
        for (int sampleIndex = 0; sampleIndex < GODRAYS_SAMPLE_COUNT; sampleIndex++)
        {
            float2 textureUV = GodRaysBufferUV(sampleUV, _OcclusionTextureScale);
            float mask = SAMPLE_TEXTURE2D_X_LOD(_OcclusionTexture, s_linear_clamp_sampler, textureUV, 0).r;
            sum += mask * GodRaysInsideScreen(sampleUV) * illumination;
            weightSum += illumination;
            illumination *= decayPerStep;
            sampleUV += stepUV;
        }

        float rays = sum / max(weightSum, 0.0001);
        rays *= max(_Weight, 0.0) * _DecaySunAngleKoef * visible;
        return float4(rays, 0.0, 0.0, 0.0);
    }

    float4 CompositePass(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.positionCS.xy * _ScreenSize.zw;
        float2 textureUV = GodRaysBufferUV(uv, _GodRaysTextureScale);
        float rays = SAMPLE_TEXTURE2D_X_LOD(_GodRaysTexture, s_linear_clamp_sampler, textureUV, 0).r;

        #if defined(_DUST_ON)
            // Dust inherits both the ray mask and _DecaySunAngleKoef from the rays.
            rays *= 1.0 + GodRaysHDRPDustStrength(uv);
        #endif

        float exposure = lerp(1.0, GetCurrentExposureMultiplier(), saturate(_ExposureWeight));
        float3 color = rays * _RayColor.rgb * max(_Exposure, 0.0) * _GodRaysPassIntensity * exposure;
        return float4(color, 0.0);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "Occlusion"
            Blend Off
            HLSLPROGRAM
                #pragma fragment OcclusionPass
            ENDHLSL
        }

        Pass
        {
            Name "Rays"
            Blend Off
            HLSLPROGRAM
                #pragma fragment RaysPass
                #pragma multi_compile_local_fragment _ _GODRAYSAMPLES_8 _GODRAYSAMPLES_12 _GODRAYSAMPLES_30 _GODRAYSAMPLES_60 _GODRAYSAMPLES_100
            ENDHLSL
        }

        Pass
        {
            Name "Composite"
            Blend One One
            ColorMask RGB
            HLSLPROGRAM
                #pragma fragment CompositePass
                #pragma multi_compile_local_fragment _ _DUST_ON
            ENDHLSL
        }
    }
    CustomEditor "Luminus.Editor.HDRP.ScreenSpaceGodRaysHDRPShaderGUI"
    Fallback Off
}
