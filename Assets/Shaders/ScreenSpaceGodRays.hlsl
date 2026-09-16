#ifndef SCREEN_SPACE_GOD_RAYS_INCLUDED
#define SCREEN_SPACE_GOD_RAYS_INCLUDED

float4 _SunDirection;

#define GOD_RAY_SAMPLES 120


// ============================================================
// HELPERS
// ============================================================

float GodRayAspectDistance(float2 a, float2 b)
{
    float2 delta = a - b;
    delta.x *= _ScreenParams.x / _ScreenParams.y;

    return length(delta);
}

float GodRaySkyMask(float2 uv, float depthThreshold, float depthSoftness)
{
    float rawDepth = SHADERGRAPH_SAMPLE_SCENE_DEPTH(uv);
    float linearDepth = Linear01Depth(rawDepth, _ZBufferParams);

    return smoothstep(
        depthThreshold,
        depthThreshold + max(depthSoftness, 0.000001),
        linearDepth);
}

float GodRaySunMask(float2 uv, float2 sunUV, float radius, float softness)
{
    float distanceToSun = GodRayAspectDistance(uv, sunUV);

    return 1.0 - smoothstep(
        radius,
        radius + max(softness, 0.000001),
        distanceToSun);
}


// ============================================================
// SUN -> SCREEN POSITION
// ============================================================

void GetSunScreenData_float(
    out float2 SunUV,
    out float SunVisible)
{
#ifdef SHADERGRAPH_PREVIEW

    SunUV = float2(0.5, 0.5);
    SunVisible = 1.0;

#else

    float3 sunDirectionWS = normalize(_SunDirection.xyz);

    float3 sunPositionWS =
        _WorldSpaceCameraPos +
        sunDirectionWS * 1000.0;

    float4 clipPosition =
        TransformWorldToHClip(sunPositionWS);

    float safeW = max(abs(clipPosition.w), 0.00001);

    float2 ndc = clipPosition.xy / safeW;

    SunUV = ndc * 0.5 + 0.5;

    if (_ProjectionParams.x < 0.0)
        SunUV.y = 1.0 - SunUV.y;

    SunVisible = step(0.00001, clipPosition.w);

#endif
}


// ============================================================
// DEPTH OCCLUSION MASK
// 1 = sky
// 0 = geometry
// ============================================================

void DepthOcclusionMask_float(
    float2 UV,
    float DepthThreshold,
    float DepthSoftness,
    out float Mask)
{
#ifdef SHADERGRAPH_PREVIEW

    Mask = 1.0;

#else

    Mask = GodRaySkyMask(
        UV,
        DepthThreshold,
        DepthSoftness);

#endif
}


// ============================================================
// SUN MASK
// ============================================================

void SunMask_float(
    float2 UV,
    float2 SunUV,
    float Radius,
    float Softness,
    out float Mask)
{
    Mask = GodRaySunMask(
        UV,
        SunUV,
        Radius,
        Softness);
}


// ============================================================
// DEPTH + SUN MASK
// ============================================================

void GodRaySourceMask_float(
    float2 UV,
    float2 SunUV,
    float SunVisible,
    float DepthThreshold,
    float DepthSoftness,
    float SunRadius,
    float SunSoftness,
    out float Mask)
{
#ifdef SHADERGRAPH_PREVIEW

    Mask = 0.0;

#else

    if (SunVisible < 0.5)
    {
        Mask = 0.0;
        return;
    }

    float skyMask = GodRaySkyMask(
        UV,
        DepthThreshold,
        DepthSoftness);

    float sunMask = GodRaySunMask(
        UV,
        SunUV,
        SunRadius,
        SunSoftness);

    Mask = skyMask * sunMask;

#endif
}


// ============================================================
// RADIAL GOD RAYS DIRECTLY FROM DEPTH
// ============================================================

void RadialGodRaysDepth_float(
    float2 UV,
    float2 SunUV,
    float SunVisible,
    float Density,
    float Decay,
    float Weight,
    float Exposure,
    float DepthThreshold,
    float DepthSoftness,
    float SunRadius,
    float SunSoftness,
    out float Rays)
{
#ifdef SHADERGRAPH_PREVIEW

    Rays = 0.0;

#else

    if (SunVisible < 0.5)
    {
        Rays = 0.0;
        return;
    }

    float2 stepUV =
        (SunUV - UV) *
        Density /
        GOD_RAY_SAMPLES;

    float2 sampleUV = UV;

    float illuminationDecay = 1.0;
    float accumulated = 0.0;

    [unroll]
    for (int i = 0; i < GOD_RAY_SAMPLES; i++)
    {
        sampleUV += stepUV;

        bool insideScreen =
            sampleUV.x >= 0.0 &&
            sampleUV.x <= 1.0 &&
            sampleUV.y >= 0.0 &&
            sampleUV.y <= 1.0;

        if (insideScreen)
        {
            float skyMask = GodRaySkyMask(
                sampleUV,
                DepthThreshold,
                DepthSoftness);

            float sunMask = GodRaySunMask(
                sampleUV,
                SunUV,
                SunRadius,
                SunSoftness);

            accumulated +=
                skyMask *
                sunMask *
                illuminationDecay *
                Weight;
        }

        illuminationDecay *= Decay;
    }

    Rays = accumulated * Exposure;

#endif
}


// ============================================================
// COMPOSITE
// ============================================================

void CompositeGodRays_float(
    float3 SceneColor,
    float Rays,
    float3 RayColor,
    float Intensity,
    out float3 Color)
{
    Color =
        SceneColor +
        RayColor *
        Rays *
        Intensity;
}

#endif