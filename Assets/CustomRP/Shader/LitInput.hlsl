#ifndef CUSTOM_LIT_INPUT_INCLUDED
#define CUSTOM_LIT_INPUT_INCLUDED

TEXTURE2D(_BaseMap);        // 基础纹理
TEXTURE2D(_MaskMap);        // mods纹理
SAMPLER(sampler_BaseMap);   // 采样器，这两个变量不能逐实例提供，应放在全局域中

TEXTURE2D(_EmissionMap);    // 自发光纹理

TEXTURE2D(_DetailMap);      // 细节纹理
SAMPLER(sampler_DetailMap);

TEXTURE2D(_NormalMap);      // 法线纹理
TEXTURE2D(_DetailNormalMap);

// 将属性放入常量缓冲区，并定义名为 "UnityPerMaterial" 的 buffer，优先使用 SRP Batch，然后是 GPU 实例
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    // 定义具有指定类型和名称的每实例着色器属性
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _DetailMap_ST)
    UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
    UNITY_DEFINE_INSTANCED_PROP(float, _ZWrite)
    UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
    UNITY_DEFINE_INSTANCED_PROP(float, _Occlusion)
    UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
    UNITY_DEFINE_INSTANCED_PROP(float, _Fresnel)
    UNITY_DEFINE_INSTANCED_PROP(float, _DetailAlbedo)
    UNITY_DEFINE_INSTANCED_PROP(float, _DetailSmoothness)
    UNITY_DEFINE_INSTANCED_PROP(float, _DetailNormalScale)
    UNITY_DEFINE_INSTANCED_PROP(float, _NormalScale)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)

struct InputConfig
{
    float2 baseUV;
    float2 detailUV;
    bool useMask;
    bool useDetail;
};

InputConfig GetInputConfig(float2 baseUV, float2 detailUV = 0.0)
{
    InputConfig config;
    config.baseUV = baseUV;
    config.detailUV = detailUV;
    config.useMask = false;
    config.useDetail = false;
    return config;
}

float3 GetEmission(InputConfig config)
{
    float4 map = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, config.baseUV);
    float4 color = INPUT_PROP(_EmissionColor);
    return map.rgb * color.rgb;
}

float2 TransformBaseUV(float2 baseUV)
{
    float4 baseST = INPUT_PROP(_BaseMap_ST);
    return baseUV * baseST.xy + baseST.zw;
}

float4 GetDetail(InputConfig config)
{
    if (config.useDetail)
    {
        float4 map = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, config.detailUV);
        // 从 [0,1] 映射到 [-1,1]
        return map * 2.0 - 1.0;
    }
    return 0.0;
}

float4 GetMask(InputConfig config)
{
    if (config.useMask)
    {
        return SAMPLE_TEXTURE2D(_MaskMap, sampler_BaseMap, config.baseUV);
    }
    return 1.0;
}

float4 GetBase(InputConfig config)
{
    float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, config.baseUV);
    float4 color = INPUT_PROP(_BaseColor);

    if (config.useDetail)
    {
        // 细节纹理 r 通道中储存 albedo 值，从 [0,1] 映射到 [-1,1]
        float detail = GetDetail(config).r * INPUT_PROP(_DetailAlbedo);
        // mods 纹理 b 通道中储存遮罩值
        float mask = GetMask(config).b;
    
        // 如果该值小于 0，则在 base 颜色 和 黑色之间插值，降低亮度
        // 如果该值大于 0，则在 base 颜色 和 白色之间插值，提升亮度
        // 在 gamma 空间中进行插值能更好的匹配视觉灰度分布，所以先开方，然后再平方回来
        map.rgb = lerp(sqrt(map.rgb), detail < 0.0 ? 0.0 : 1.0, abs(detail) * mask);
        map.rgb *= map.rgb;
    }
    
    return map * color;
}

float GetCutoff(InputConfig config)
{
    return INPUT_PROP(_Cutoff);
}

float GetMetallic(InputConfig config)
{
    float metallic = INPUT_PROP(_Metallic);
    metallic *= GetMask(config).r;
    return metallic;
}

float GetSmoothness(InputConfig config)
{
    // mods 平滑度
    float smoothness = INPUT_PROP(_Smoothness);
    // mods 纹理 a 通道中储存的平滑度
    smoothness *= GetMask(config).a;
    
    if (config.useDetail)
    {
        // detail 平滑度
        float detail = GetDetail(config).b * INPUT_PROP(_DetailSmoothness);
        // mods 纹理 b 通道中储存的遮罩值
        float mask = GetMask(config).b;
        smoothness = lerp(smoothness, detail < 0.0 ? 0.0 : 1.0, abs(detail) * mask);
    }
    
    return smoothness;
}

float GetOcclusion(InputConfig config)
{
    float strength = INPUT_PROP(_Occlusion);
    float occlusion = GetMask(config).g;
    occlusion = lerp(occlusion, 1.0, strength);
    return occlusion;
}

float2 TransformDetailUV(float2 detailUV)
{
    float4 detailST = INPUT_PROP(_DetailMap_ST);
    return detailUV * detailST.xy + detailST.zw;
}

float GetFresnel(InputConfig config)
{
    return INPUT_PROP(_Fresnel);
}

float3 GetNormalTS(InputConfig config)
{
    float4 map = SAMPLE_TEXTURE2D(_NormalMap, sampler_BaseMap, config.baseUV);
    float scale = INPUT_PROP(_NormalScale);
    float3 normal = DecodeNormal(map, scale);
    
    if (config.useDetail)
    {
        map = SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailMap, config.detailUV);
        float mask = GetMask(config).b;
        scale = INPUT_PROP(_DetailNormalScale) * mask;
        float3 detail = DecodeNormal(map, scale);
        normal = BlendNormal(normal, detail);
    }
    
    return normal;
}

float GetFinalAlpha (float alpha) {
    return INPUT_PROP(_ZWrite) ? 1.0 : alpha;
}

#endif