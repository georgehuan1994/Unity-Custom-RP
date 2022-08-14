/* ShaderLibrary/Surface.hlsl */

#ifndef CUSTOM_SURFACE_INCLUDED  // 引用保护
#define CUSTOM_SURFACE_INCLUDED

struct Surface
{
    float3 position;
    float3 normal;
    float3 interpolateNormal;
    float3 viewDirection;
    float depth;    // 视图空间深度
    float3 color;
    float alpha;
    float metallic;
    float occlusion;
    float smoothness;
    float fresnelStrength;
    float dither;   // 抖动值
};

#endif