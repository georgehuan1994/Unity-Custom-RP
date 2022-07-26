#ifndef CUSTOM_GI_INCLUDED
#define CUSTOM_GI_INCLUDED

#if defined(LIGHTMAP_ON)
    #define GI_ATTRIBUTE_DATA float2 lightMapUV : TEXCOORD1;
    #define GI_VARYINGS_DATA float2 lightMapUV : VAR_LIGHT_MAP_UV;
    #define TRANSFER_GI_DATA(input, output) output.lightMapUV = input.lightMapUV * unity_LightmapST.xy + unity_LightmapST.zw;
    #define GI_FRAGMENT_DATA(input) input.lightMapUV
#else
    #define GI_ATTRIBUTE_DATA                   // 作为顶点数据
    #define GI_VARYINGS_DATA                    // 作为片元数据
    #define TRANSFER_GI_DATA(input, output)     // 从顶点着色器传递到片元着色器
    #define GI_FRAGMENT_DATA(input) 0.0         // 获取 Lightmap UV
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"

TEXTURE2D(unity_Lightmap);                      // 光照贴图
SAMPLER(samplerunity_Lightmap);                 // 采样器

struct GI
{
    float3 diffuse;
};

// 采样
float3 SampleLightMap(float2 lightMapUV)
{
    #if defined(LIGHTMAP_ON)
    return SampleSingleLightmap(
        TEXTURE2D_ARGS(unity_Lightmap, samplerunity_Lightmap),  // 纹理和采样器状态
        lightMapUV,                             // uv
        float4(1.0, 1.0, 0.0, 0.0),             // 缩放和平移变换
        #if defined(UNITY_LIGHTMAP_FULL_HDR)    // 启用 HDR，光照贴图未被压缩
            false,
        #else                                   // 未启用 HDR，光照贴图被压缩
            true,
        #endif
        float4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0, 0.0)    // 解压指令
        );
    #else
        return 0.0;
    #endif
}

// 给定 光照贴图 uv，获取 GI 结构
GI GetGI(float2 lightMapUV)
{
    GI gi;
    gi.diffuse = SampleLightMap(lightMapUV);
    
    // gi.diffuse = float3(lightMapUV, 0.0);
    return gi;
}

#endif