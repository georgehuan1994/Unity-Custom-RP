/* ShaderLibrary/Light.hlsl */

#ifndef CUSTOM_LIGHT_INCLUDED  // 引用保护
#define CUSTOM_LIGHT_INCLUDED

#define MAX_DIRECTIONAL_LIGHT_COUNT 4   // 最大平行光数量
#define MAX_OTHER_LIGHT_COUNT 64        // 最大非平行光数量

CBUFFER_START(_CustomLight)
    int _DirectionalLightCount;
    float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
    float4 _DirectionalLightDirectionsAndMasks[MAX_DIRECTIONAL_LIGHT_COUNT];
    float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];

    int _OtherLightCount;
    float4 _OtherLightColors[MAX_OTHER_LIGHT_COUNT];
    float4 _OtherLightPositions[MAX_OTHER_LIGHT_COUNT];
    float4 _OtherLightDirectionsAndMasks[MAX_OTHER_LIGHT_COUNT];
    float4 _OtherLightSpotAngles[MAX_OTHER_LIGHT_COUNT];
    float4 _OtherLightShadowData[MAX_OTHER_LIGHT_COUNT];
CBUFFER_END

struct Light
{
    float3 color;
    float3 direction;
    float attenuation;
    uint renderingLayerMask;
};

// 获取平行光数量
int GetDirectionalLightCount()
{
    return _DirectionalLightCount;
}

// 获取非平行光数量
int GetOtherLightCount()
{
    return _OtherLightCount;
}

// 获取平行光源的阴影数据
DirectionalShadowData GetDirectionalShadowDate(int lightIndex, ShadowData shadowData)
{
    DirectionalShadowData data;
    data.strength = _DirectionalLightShadowData[lightIndex].x; //* shadowData.strength;
    data.tileIndex = _DirectionalLightShadowData[lightIndex].y + shadowData.cascadeIndex;
    data.normalBias = _DirectionalLightShadowData[lightIndex].z;
    data.shadowMaskChannel = _DirectionalLightShadowData[lightIndex].w;
    return data;
}

// 获取非平行光阴影参数
OtherShadowData GetOtherShadowData(int lightIndex)
{
    OtherShadowData data;
    data.strength = _OtherLightShadowData[lightIndex].x;
    data.tileIndex = _OtherLightShadowData[lightIndex].y;
    data.isPoint = _OtherLightShadowData[lightIndex].z;
    data.shadowMaskChannel = _OtherLightShadowData[lightIndex].w;
    data.lightPositionWS = 0.0;
    data.lightDirectionWS = 0.0;
    data.spotDirectionWS = 0.0;
    return data;
}

// 获取平行光源数据
Light GetDirectionLight(int index, Surface surfaceWS, ShadowData shadowData)
{
    Light light;
    light.color = _DirectionalLightColors[index].rgb;
    light.direction = _DirectionalLightDirectionsAndMasks[index].xyz;
    light.renderingLayerMask = asuint(_DirectionalLightDirectionsAndMasks[index].w);

    // 获取阴影数据 (强度、阴影贴图图集索引)
    DirectionalShadowData dirShadowData = GetDirectionalShadowDate(index, shadowData);
    light.attenuation = GetDirectionalShadowAttenuation(dirShadowData, shadowData, surfaceWS);
    return light;
}

// 获取非平行光源数据
Light GetOtherLight(int index, Surface surfaceWS, ShadowData shadowData)
{
    Light light;
    light.color = _OtherLightColors[index].rgb;

    float3 position = _OtherLightPositions[index].xyz;

    float3 ray = position - surfaceWS.position;
    light.direction = normalize(ray);

    float distanceSqr = max(dot(ray, ray), 0.00001);
    float3 spotDirection = _OtherLightDirectionsAndMasks[index].xyz;
    light.renderingLayerMask = asuint(_OtherLightDirectionsAndMasks[index].w);
    
    float rangeAttenuation = Square(saturate(1.0 - Square(distanceSqr * _OtherLightPositions[index].w)));
    float4 spotAngles = _OtherLightSpotAngles[index];
    float spotAttenuation = Square(saturate(dot(spotDirection, light.direction.xyz) * spotAngles.x + spotAngles.y));

    OtherShadowData otherShadowData = GetOtherShadowData(index);
    otherShadowData.lightPositionWS = position;
    otherShadowData.lightDirectionWS = light.direction;
    otherShadowData.spotDirectionWS = spotDirection;
    light.attenuation = GetOtherLightShadowAttenuation(otherShadowData, shadowData, surfaceWS) * spotAttenuation * rangeAttenuation / distanceSqr;

    return light;
}

#endif