/* ShaderLibrary/Shadows.hlsl */

#ifndef CUSTOM_SHADOWS_INCLUDED  // 引用保护
#define CUSTOM_SHADOWS_INCLUDED

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4  // 最大平行投影光源数量
#define MAX_CASCADE_COUNT 4                     // 最大级联数量

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);                    // 非 bilinear 模式，针对深度数据的线性插值模式

CBUFFER_START(_CustomShadow)
    float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
CBUFFER_END

// 每个光源的阴影数据
struct DirectionalShadowData
{
    float strength;
    int tileIndex;
};

// 对光源的阴影深度图进行采样
float SampleDirectionalShadowAtlas(float3 positionSTS)
{
    return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

// 根据采样结果，返回阴影衰减 (修正后的阴影强度)
float GetDirectionalShadowAttenuation(DirectionalShadowData data, Surface surfaceWS)
{
    if (data.strength <= 0) return 1.0;

    // 将着色点转换到光源空间 (阴影纹理空间)
    float4 positionSTS = mul(_DirectionalShadowMatrices[data.tileIndex], float4(surfaceWS.position, 1.0));

    // 采样结果表示有多少光到达了着色点：0 表示完全被阴影覆盖，1 表示完全没有阴影
    float result = SampleDirectionalShadowAtlas(positionSTS.xyz);
    
    return lerp(1.0, result, data.strength);
}

#endif