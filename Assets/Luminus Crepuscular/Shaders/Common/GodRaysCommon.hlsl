#ifndef GOD_RAYS_COMMON_INCLUDED
#define GOD_RAYS_COMMON_INCLUDED

#define GOD_RAYS_PI 3.14159265

float GodRaysRand(float seed)
{
    return frac(sin(seed * 12.9898) * 43758.5453);
}

half GodRaysInsideScreen(float2 uv)
{
    float2 lower = step(float2(0.0, 0.0), uv);
    float2 upper = step(uv, float2(1.0, 1.0));
    return (half)(lower.x * lower.y * upper.x * upper.y);
}

float GodRaysAspectDistance(float2 direction, float aspect)
{
    direction.x *= aspect;
    return length(direction);
}

half GodRaysAngleStrength(float3 viewDirectionWS, float3 sunDirectionWS, half angleStart, half angleEnd, half anglePower)
{
    half alignment = saturate(dot(viewDirectionWS, sunDirectionWS));
    half strength = smoothstep(angleStart, angleEnd, alignment);
    return pow(strength, anglePower);
}

half GodRaysDustStrength(float3 viewDirectionWS, float3 sunDirectionWS, half minStrength, half maxStrength, half angleStart, half angleEnd, half anglePower)
{
    half angleStrength = GodRaysAngleStrength(viewDirectionWS, sunDirectionWS, angleStart, angleEnd, anglePower);
    return lerp(minStrength, maxStrength, angleStrength);
}

void GodRaysDustLayer(float random, float dustDistance, float noiseScale, out float distance, out float scale, out float speed)
{
    distance = dustDistance * lerp(0.15, 1.0, random);
    scale = noiseScale * lerp(0.01, 1.5, random);
    speed = lerp(0.5, 1.5, random);
}

half ComputeSchlickPhase(half cosTheta, half g)
{
    half g2 = g * g;
    half denom = 1.0 + g * cosTheta;
    return (1.0 - g2) / (4.0 * GOD_RAYS_PI * denom * denom);
}

#endif