/* ShaderLibrary/Light.hlsl */

#ifndef CUSTOM_LIGHT_INCLUDED  // 引用保护
#define CUSTOM_LIGHT_INCLUDED

#define MAX_DIRECTIONAL_LIGHT_COUNT 4   // 定义最大平行光数量

CBUFFER_START(_CustomLight)
    int _DirectionalLightCount;
    float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
    float4 _DirectionalLightDirections[MAX_DIRECTIONAL_LIGHT_COUNT];
CBUFFER_END

struct Light
{
    float3 color;
    float3 direction;
};

// 获取平行光数量
int GetDirectionalLightCount()
{
    return _DirectionalLightCount;
}

// 获取光源信息
Light GetDirectionLight(int index)
{
    Light light;
    light.color = _DirectionalLightColors[index].rgb;
    light.direction = _DirectionalLightDirections[index].xyz;
    return light;
}

#endif