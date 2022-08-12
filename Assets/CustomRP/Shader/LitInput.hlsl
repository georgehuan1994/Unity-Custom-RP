#ifndef CUSTOM_LIT_INPUT_INCLUDED
#define CUSTOM_LIT_INPUT_INCLUDED

TEXTURE2D(_BaseMap);        // 基础纹理
TEXTURE2D(_MaskMap);        // mods纹理
SAMPLER(sampler_BaseMap);   // 采样器，这两个变量不能逐实例提供，应放在全局域中

TEXTURE2D(_EmissionMap);    // 自发光纹理

TEXTURE2D(_DetailMap);      // 细节纹理
SAMPLER(sampler_DetailMap);

// 将属性放入常量缓冲区，并定义名为 "UnityPerMaterial" 的 buffer，优先使用 SRP Batch，然后是 GPU 实例
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    // 定义具有指定类型和名称的每实例着色器属性
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _DetailMap_ST)
    UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
    UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
    UNITY_DEFINE_INSTANCED_PROP(float, _Occlusion)
    UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
    UNITY_DEFINE_INSTANCED_PROP(float, _Fresnel)
    UNITY_DEFINE_INSTANCED_PROP(float, _DetailAlbedo)
    UNITY_DEFINE_INSTANCED_PROP(float, _DetailSmoothness)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)

float3 GetEmission(float2 baseUV)
{
    float4 map = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, baseUV);
    float4 color = INPUT_PROP(_EmissionColor);
    return map.rgb * color.rgb;
}

float2 TransformBaseUV(float2 baseUV)
{
    float4 baseST = INPUT_PROP(_BaseMap_ST);
    return baseUV * baseST.xy + baseST.zw;
}

float4 GetDetail(float2 detailUV)
{
    float4 map = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, detailUV);
    // 从 [0,1] 映射到 [-1,1]
    return map * 2.0 - 1.0;
}

float4 GetMask(float2 baseUV)
{
    return SAMPLE_TEXTURE2D(_MaskMap, sampler_BaseMap, baseUV);
}

float4 GetBase(float2 baseUV, float2 detailUV = 0.0)
{
    float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV);
    float4 color = INPUT_PROP(_BaseColor);

    // 细节纹理 r 通道中储存 albedo 值，从 [0,1] 映射到 [-1,1]
    float detail = GetDetail(detailUV).r * INPUT_PROP(_DetailAlbedo);
    // mods 纹理 b 通道中储存遮罩值
    float mask = GetMask(baseUV).b;
    
    // 如果该值小于 0，则在 base 颜色 和 黑色之间插值，降低亮度
    // 如果该值大于 0，则在 base 颜色 和 白色之间插值，提升亮度
    // 在 gamma 空间中进行插值能更好的匹配视觉灰度分布，所以先开方，然后再平方回来
    map.rgb = lerp(sqrt(map.rgb), detail < 0.0 ? 0.0 : 1.0, abs(detail) * mask);
    map.rgb *= map.rgb;
    
    return map * color;
}

float GetCutoff(float2 baseUV)
{
    return INPUT_PROP(_Cutoff);
}

float GetMetallic(float2 baseUV)
{
    float metallic = INPUT_PROP(_Metallic);
    metallic *= GetMask(baseUV).r;
    return metallic;
}

float GetSmoothness(float2 baseUV, float2 detailUV = 0.0)
{
    // base mods 光滑度
    float smoothness = INPUT_PROP(_Smoothness);
    smoothness *= GetMask(baseUV).a;

    // detail 光滑度
    float detail = GetDetail(detailUV).b * INPUT_PROP(_DetailSmoothness);
    float mask = GetMask(baseUV).b;
    smoothness = lerp(smoothness, detail < 0.0 ? 0.0 : 1.0, abs(detail) * mask);
    
    return smoothness;
}

float GetOcclusion(float2 baseUV)
{
    float strength = INPUT_PROP(_Occlusion);
    float occlusion = GetMask(baseUV).g;
    occlusion = lerp(occlusion, 1.0, strength);
    return occlusion;
}

float2 TransformDetailUV(float2 detailUV)
{
    float4 detailST = INPUT_PROP(_DetailMap_ST);
    return detailUV * detailST.xy + detailST.zw;
}

float GetFresnel(float2 baseUV)
{
    return INPUT_PROP(_Fresnel);
}

#endif