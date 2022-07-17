/* Shader/LitPass.hlsl */

#ifndef CUSTOM_LIT_PASS_INCLUDED  // 引用保护
#define CUSTOM_LIT_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"
#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

TEXTURE2D(_BaseMap);        // 基础纹理
SAMPLER(sampler_BaseMap);   // 采样器，这两个变量不能逐实例提供，应放在全局域中

// 将属性放入常量缓冲区，并定义名为 "UnityPerMaterial" 的 buffer，优先使用 SRP Batch，然后是 GPU 实例
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    // 定义具有指定类型和名称的每实例着色器属性
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
    UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
    UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

struct Attributes
{
    float3 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 baseUV : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID  // 将对象的索引添加到顶点着色器输入结构中
};

struct Varyings
{
    float3 positionWS : VAR_POSITION;
    float4 positionCS : SV_POSITION;
    float3 normalWS : VAR_NORMAL;   // VAR_NORMAL 没有特别的语义，只是一个自定义的标识
    float2 baseUV : VAR_BASE_UV;    // VAR_BASE_UV 没有特别的语义，只是一个自定义的标识
    UNITY_VERTEX_INPUT_INSTANCE_ID  // 将对象的索引添加到顶点着色器输出结构中
};

Varyings LitPassVertex (Attributes input)
{
    Varyings output;

    // 允许着色器函数访问实例 ID
    UNITY_SETUP_INSTANCE_ID(input);
    // 将实例 ID 从输入结构复制到顶点着色器中的输出结构
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    
    output.positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(output.positionWS);

    // 访问实例化常量缓冲区中的每个实例着色器属性
    float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
    output.baseUV = input.baseUV * baseST.xy + baseST.zw;

    // 将法线坐标转换到世界空间下
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    
    return output;
}

float4 LitPassFragment (Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);

    // 使用采样器 sampler_BaseMap，从 _BaseMap 中采样
    float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
    float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
    float4 base = baseMap * baseColor;
    
    #if defined(_CLIPPING)
    clip(base.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff));
    #endif
    
    // base.rgb = input.normalWS;
    // base.rgb = abs(length(input.normalWS) - 1.0) * 10.0; // 没有归一化
    // base.rgb = normalize(input.normalWS);
    // base.rgb = abs(length(normalize(input.normalWS)) - 1.0) * 10.0; // 纯黑色
    // return base;
    
    Surface surface;
    surface.normal = normalize(input.normalWS);
    surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
    surface.color = base.rgb;
    surface.alpha = base.a;
    surface.metallic = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic);
    surface.smoothness = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness);

    #if defined(_PREMULTIPLY_ALPHA)
    BRDF brdf = GetBRDF(surface, true);
    #else
    BRDF brdf = GetBRDF(surface);
    #endif
    
    float3 color = GetLighting(surface, brdf);
    return float4(color, surface.alpha);
}

#endif