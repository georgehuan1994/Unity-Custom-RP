/* ShaderLibrary/Light.hlsl */

#ifndef CUSTOM_LIGHT_INCLUDED  // 引用保护
#define CUSTOM_LIGHT_INCLUDED

#define MAX_DIRECTIONAL_LIGHT_COUNT 4   // 定义最大平行光数量

CBUFFER_START(_CustomLight)
    int _DirectionalLightCount;
    float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
    float4 _DirectionalLightDirections[MAX_DIRECTIONAL_LIGHT_COUNT];
    float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];
CBUFFER_END

struct Light
{
    float3 color;
    float3 direction;
    float attenuation;
};

// 获取平行光数量
int GetDirectionalLightCount()
{
    return _DirectionalLightCount;
}

// 获取平行光源的阴影数据
DirectionalShadowData GetDirectionalShadowDate(int lightIndex, ShadowData shadowData)
{
    DirectionalShadowData data;
    data.strength = _DirectionalLightShadowData[lightIndex].x; //* shadowData.strength;
    data.tileIndex = _DirectionalLightShadowData[lightIndex].y + shadowData.cascadeIndex;
    data.normalBias = _DirectionalLightShadowData[lightIndex].z;
    return data;
}

// 获取平行光源
Light GetDirectionLight(int index, Surface surfaceWS, ShadowData shadowData)
{
    Light light;
    light.color = _DirectionalLightColors[index].rgb;
    light.direction = _DirectionalLightDirections[index].xyz;

    // 获取阴影数据 (强度、阴影贴图图集索引)
    DirectionalShadowData dirShadowData = GetDirectionalShadowDate(index, shadowData);
    // light.attenuation = shadowData.cascadeIndex * 0.25;
    light.attenuation = GetDirectionalShadowAttenuation(dirShadowData, shadowData, surfaceWS);
    return light;
}

#endif