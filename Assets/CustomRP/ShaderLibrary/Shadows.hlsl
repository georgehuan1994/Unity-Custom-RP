/* ShaderLibrary/Shadows.hlsl */

#ifndef CUSTOM_SHADOWS_INCLUDED  // 引用保护
#define CUSTOM_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

#if defined(_DIRECTIONAL_PCF3)
    #define DIRECTIONAL_FILTER_SAMPLES 4
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
    #define DIRECTIONAL_FILTER_SAMPLES 9
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
    #define DIRECTIONAL_FILTER_SAMPLES 16
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#if defined(_OTHER_PCF3)
    #define OTHER_FILTER_SAMPLES 4
    #define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_OTHER_PCF5)
    #define OTHER_FILTER_SAMPLES 9
    #define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_OTHER_PCF7)
    #define OTHER_FILTER_SAMPLES 16
    #define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4  // 最大平行投影光源数量
#define MAX_SHADOWED_OTHER_LIGHT_COUNT 16       // 最大非平行投影光源数量
#define MAX_CASCADE_COUNT 4                     // 最大级联数量

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);      // 平行光阴影图集
TEXTURE2D_SHADOW(_OtherShadowAtlas);            // 非平行光阴影图集

#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);                    // 非 bilinear 模式，针对深度数据的线性插值模式

CBUFFER_START(_CustomShadow)
    int _CascadeCount;
    float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
    float4 _CascadeData[MAX_CASCADE_COUNT];
    float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
    float4x4 _OtherShadowMatrices[MAX_SHADOWED_OTHER_LIGHT_COUNT];
    float4 _OtherShadowTiles[MAX_SHADOWED_OTHER_LIGHT_COUNT];
    float4 _ShadowAtlasSize;
    float4 _ShadowDistanceFade;
CBUFFER_END

// ShadowMask 结构
struct ShadowMask
{
    bool always;
    bool distance;
    float4 shadows;
};

// 级联阴影数据
struct ShadowData
{
    int cascadeIndex;   // 级联等级索引
    float strength;     // 级联强度系数
    float cascadeBlend;
    ShadowMask shadowMask;
};

// 每个平行光的阴影参数
struct DirectionalShadowData
{
    float strength;
    int tileIndex;
    float normalBias;
    int shadowMaskChannel;
};

// 非平行光阴影参数
struct OtherShadowData
{
    float strength;
    int tileIndex;
    bool isPoint;
    int shadowMaskChannel;
    float3 lightPositionWS;
    float3 lightDirectionWS;
    float3 spotDirectionWS;
};

static const float3 pointShadowPlane[6] =
{
    float3(-1.0, 0.0, 0.0),
    float3(1.0, 0.0, 0.0),
    float3(0.0, -1.0, 0.0),
    float3(0.0, 1.0, 0.0),
    float3(0.0, 0.0, -1.0),
    float3(0.0, 0.0, 1.0)
};


float FadeShadowStrength(float distance, float scale, float fade)
{
    return saturate((1.0 - distance * scale) * fade);
}

// 获取阴影数据：级联、ShadowMask
ShadowData GetShadowData(Surface surfaceWS)
{
    ShadowData data;
    data.shadowMask.always = false;
    data.shadowMask.distance = false;
    data.shadowMask.shadows = 1.0;
    
    data.cascadeBlend = 1.0;
    data.strength = FadeShadowStrength(surfaceWS.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y);

    // 判断着色器应该使用哪个等级
    int i;
    for (i = 0; i < _CascadeCount; i++)
    {
        float4 sphere = _CascadeCullingSpheres[i];
        float distanceSqr = DistanceSqure(surfaceWS.position, sphere.xyz);
        if (distanceSqr < sphere.w)
        {
            float fade = FadeShadowStrength(distanceSqr, _CascadeData[i].x, _ShadowDistanceFade.z);
            if (i == _CascadeCount - 1)
            {
                data.strength *= fade;
            }
            else
            {
                data.cascadeBlend = fade;
            }
            break;
        }
    }
    
    // 当启用平行光级联阴影，且超出最后一个球体范围，则不渲染阴影
    if (i == _CascadeCount && _CascadeCount > 0)
    {
        data.strength = 0.0;
    }
    #if defined(_CASCADE_BLEND_DITHER)
    else if (data.cascadeBlend < surfaceWS.dither) {
        i += 1;
    }
    #endif
    
    #if !defined(_CASCADE_BLEND_SOFT)
        data.cascadeBlend = 1.0;
    #endif
    
    data.cascadeIndex = i;
    return data;
}

// 对平行光的阴影深度图进行采样
float SampleDirectionalShadowAtlas(float3 positionSTS)
{
    return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

// 实时平行光阴影过滤器
float FilterDirectionalShadow(float3 positionSTS)
{
    #if defined(DIRECTIONAL_FILTER_SETUP)
        real weights[DIRECTIONAL_FILTER_SAMPLES];
        real2 positions[DIRECTIONAL_FILTER_SAMPLES];
        float4 size = _ShadowAtlasSize.yyxx;
        DIRECTIONAL_FILTER_SETUP(size, positionSTS.xy, weights, positions);
        float shadow = 0;
        for (int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; i++)
        {
            shadow += weights[i] * SampleDirectionalShadowAtlas(float3(positions[i].xy, positionSTS.z));
        }
        return shadow; 
    #else
        return SampleDirectionalShadowAtlas(positionSTS);
    #endif
}

// 对非平行光的阴影深度图进行采样
float SampleOtherShadowAtlas(float3 positionSTS, float3 bounds)
{
    positionSTS.xy = clamp(positionSTS.xy, bounds.xy, bounds.xy + bounds.z);
    return SAMPLE_TEXTURE2D_SHADOW(_OtherShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

// 实时非平行光阴影过滤器
float FilterOtherShadow(float3 positionSTS, float3 bounds)
{
    #if defined(OTHER_FILTER_SETUP)
    real weights[OTHER_FILTER_SAMPLES];
    real2 positions[OTHER_FILTER_SAMPLES];
    float4 size = _ShadowAtlasSize.wwzz;
    OTHER_FILTER_SETUP(size, positionSTS.xy, weights, positions);
    float shadow = 0;
    for (int i = 0; i < OTHER_FILTER_SAMPLES; i++)
    {
        shadow += weights[i] * SampleOtherShadowAtlas(float3(positions[i].xy, positionSTS.z), bounds);
    }
    return shadow; 
    #else
    return SampleOtherShadowAtlas(positionSTS, bounds);
    #endif
}

// 获取级联阴影贴图中的光照衰减值
float GetCascadeShadow(DirectionalShadowData directional, ShadowData global, Surface surfaceWS)
{
    // 法线偏移
    float3 normalBias = surfaceWS.interpolateNormal * (directional.normalBias * _CascadeData[global.cascadeIndex].y);

    // 将着色点转换到光源空间 (阴影纹理空间)
    float3 positionSTS = mul(_DirectionalShadowMatrices[directional.tileIndex], float4(surfaceWS.position + normalBias, 1.0)).xyz;

    // 采样结果表示有多少光到达了着色点：0 表示完全被阴影覆盖，1 表示完全没有阴影
    float result = FilterDirectionalShadow(positionSTS);

    if (global.cascadeBlend < 1.0)
    {
        normalBias = surfaceWS.interpolateNormal * (directional.normalBias * _CascadeData[global.cascadeIndex + 1].y);
        positionSTS = mul(_DirectionalShadowMatrices[directional.tileIndex + 1], float4(surfaceWS.position + normalBias, 1.0)).xyz;
        result = lerp(FilterDirectionalShadow(positionSTS), result, global.cascadeBlend);
    }

    return result;
}

// 获取阴影遮罩贴图中的光照衰减值
float GetBakedShadow(ShadowMask mask, int channel)
{
    // 0 表示完全被阴影覆盖，1 表示完全没有阴影
    // 0 表示完全没有光照，1 表示光照不受影响
    float shadow = 1.0;
    
    // 如果启用了距离模式，使用 r 通道作为光照衰减值
    if (mask.always || mask.distance)
    {
        if (channel >= 0)
        {
            shadow = mask.shadows[channel];
        }
    }
    
    // 没有启用则返回 1
    return shadow;
}

// 获取阴影遮罩贴图中的光照衰减系数
float GetBakedShadow(ShadowMask mask, int channel, float strength)
{
    // 如果启用了距离模式，使用 r 通道作为光照衰减值
    if (mask.always || mask.distance)
    {
        // 并使用平行光的阴影强度在 1 和 光照衰减值 之间做插值
        return lerp(1.0, GetBakedShadow(mask, channel), strength);
    }
    
    // 没有启用则返回 1
    return 1.0;
}

// 混合阴影遮罩贴图和实时阴影贴图的光照衰减系数
float MixBakedAndRealtimeShadows(ShadowData global, float shadow, int channel, float strength)
{
    // 阴影遮罩贴图的光照衰减值，默认为 1
    float baked = GetBakedShadow(global.shadowMask, channel);

    // 如果是 always shadowmask 模式
    if (global.shadowMask.always)
    {
        // 使用级联强度系数进行约束
        shadow = lerp(1.0, shadow, global.strength);
        // 阴影遮罩贴图的光照衰减值 与 实时光照衰减 的最小值
        shadow = min(baked, shadow);
        // 使用阴影强度进行约束
        return lerp(1.0, shadow, strength);
    }
    
    // 如果启用了距离模式
    if (global.shadowMask.distance)
    {
        // 使用级联阴影的强度，使用 级联强度系数 在 阴影遮罩贴图的光照衰减值 和 级联阴影贴图的光照衰减值 之间做插值
        // 0 表示完全被阴影覆盖，1 表示完全没有阴影
        shadow = lerp(baked, shadow, global.strength);

        // 最后使用平行光的阴影强度在 1 和 最终的光照衰减值 之间做插值
        return lerp(1.0, shadow, strength);
    }
    
    // 没有启用则将使用实时阴影
    // return lerp(1.0, shadow, strength * global.strength); // 实时模式，传进来的 shadow 不为 0，这样写会漏光
    return lerp(0.0, shadow, strength * global.strength);
}

// 根据采样结果，返回光照衰减系数 (修正后的阴影强度)
float GetDirectionalShadowAttenuation(DirectionalShadowData directional, ShadowData global, Surface surfaceWS)
{
    #if !defined(_RECEIVE_SHADOWS)
        return 1.0;
    #endif
  
    float shadow;

    if (directional.strength * global.strength <= 0.0)
    {
        // 阴影强度组合 <= 0
        // 如果启用了距离模式，不使用实时阴影，直接返回烘焙贴图上的衰减值
        shadow = GetBakedShadow(global.shadowMask, directional.shadowMaskChannel,abs(directional.strength));
    }
    else
    {
        // 阴影强度组合 > 0
        // 获取实时级联阴影贴图的光照衰减值
        shadow = GetCascadeShadow(directional, global, surfaceWS);
        // 混合阴影遮罩贴图和实时阴影贴图的光照衰减值
        shadow = MixBakedAndRealtimeShadows(global, shadow, directional.shadowMaskChannel, directional.strength);
    }

    return shadow;
}

// 获取着色点的非平行光阴影
float GetOtherShadow(OtherShadowData other, ShadowData global, Surface surfaceWS)
{
    float tileIndex = other.tileIndex;
    float3 lightPlane = other.spotDirectionWS;
    
    if (other.isPoint)
    {
        float faceOffset = CubeMapFaceID(-other.lightDirectionWS);
        tileIndex += faceOffset;
        lightPlane = pointShadowPlane[faceOffset];
    }
    
    float4 tileData = _OtherShadowTiles[tileIndex];
    
    float3 surfaceToLight = other.lightPositionWS - surfaceWS.position;
    float distanceToLightPlane = dot(surfaceToLight, lightPlane);
    float3 normalBias = surfaceWS.interpolateNormal * (distanceToLightPlane * tileData.w);
    float4 positionSTS = mul(_OtherShadowMatrices[tileIndex], float4(surfaceWS.position + normalBias, 1.0));

    return FilterOtherShadow(positionSTS.xyz / positionSTS.w, tileData.xyz);
}

// 采样获取着色点的非平行光光照衰减系数
float GetOtherLightShadowAttenuation(OtherShadowData other, ShadowData global, Surface surfaceWS)
{
    #if !defined(_RECEIVE_SHADOWS)
        return 1.0;
    #endif

    float shadow = 0.0;

    // 光源阴影强度 * 级联阴影强度 来判读是否要跳过采样
    if (other.strength * global.strength <= 0.0)
    {
        shadow = GetBakedShadow(global.shadowMask, other.shadowMaskChannel, abs(other.strength));
    }
    else
    {
        shadow = GetOtherShadow(other, global, surfaceWS);
        shadow = MixBakedAndRealtimeShadows(global, shadow, other.shadowMaskChannel, other.strength);
    }
    
    return shadow;
}


#endif