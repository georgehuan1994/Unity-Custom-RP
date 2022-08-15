#ifndef CUSTOM_UNLIT_INPUT_INCLUDED
#define CUSTOM_UNLIT_INPUT_INCLUDED

TEXTURE2D(_BaseMap);        // 基础纹理
SAMPLER(sampler_BaseMap);   // 采样器，这两个变量不能逐实例提供，应放在全局域中

// 将属性放入常量缓冲区，并定义名为 "UnityPerMaterial" 的 buffer，优先使用 SRP Batch，然后是 GPU 实例
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    // 定义具有指定类型和名称的每实例着色器属性
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)

struct InputConfig {
    float2 baseUV;
};

InputConfig GetInputConfig (float2 baseUV) {
    InputConfig c;
    c.baseUV = baseUV;
    return c;
}

float2 TransformBaseUV (float2 baseUV) {
    float4 baseST = INPUT_PROP(_BaseMap_ST);
    return baseUV * baseST.xy + baseST.zw;
}

float2 TransformDetailUV (float2 detailUV) {
    return 0.0;
}

float4 GetMask (InputConfig c) {
    return 1.0;
}

float4 GetDetail (InputConfig c) {
    return 0.0;
}
float4 GetBase (InputConfig c) {
    float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, c.baseUV);
    float4 baseColor = INPUT_PROP(_BaseColor);
    return baseMap * baseColor;
}

float3 GetNormalTS (InputConfig c) {
    return float3(0.0, 0.0, 1.0);
}

float3 GetEmission (InputConfig c) {
    return GetBase(c).rgb;
}

float GetCutoff (InputConfig c) {
    return INPUT_PROP(_Cutoff);
}

float GetMetallic (InputConfig c) {
    return 0.0;
}

float GetSmoothness (InputConfig c) {
    return 0.0;
}

float GetFresnel (InputConfig c) {
    return 0.0;
}

#endif