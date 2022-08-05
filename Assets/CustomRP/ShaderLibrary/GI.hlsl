#ifndef CUSTOM_GI_INCLUDED
#define CUSTOM_GI_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"

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

TEXTURE2D(unity_Lightmap);                      // 光照贴图
SAMPLER(samplerunity_Lightmap);                 // 采样器

TEXTURE2D(unity_ShadowMask);                    // 阴影贴图
SAMPLER(samplerunity_ShadowMask);               // 采样器

TEXTURE3D_FLOAT(unity_ProbeVolumeSH);           // 3D float texture
SAMPLER(samplerunity_ProbeVolumeSH);            

struct GI
{
    float3 diffuse;
    ShadowMask shadowMask;
};

// 采样 Lighting Map
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

// 采样 Light Probes
float3 SampleLightProbe(Surface surfaceWS)
{
    #if defined(LIGHTMAP_ON)
        return 0.0;
    #else
        if (unity_ProbeVolumeParams.x)
        {
            return SampleProbeVolumeSH4(
                    TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
                    surfaceWS.position, surfaceWS.normal,
                    unity_ProbeVolumeWorldToObject,
                    unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
                    unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz
                );
        }
        else
        {
            float4 coefficients[7];
            coefficients[0] = unity_SHAr;
            coefficients[1] = unity_SHAg;
            coefficients[2] = unity_SHAb;
            coefficients[3] = unity_SHBr;
            coefficients[4] = unity_SHBg;
            coefficients[5] = unity_SHBb;
            coefficients[6] = unity_SHC;
            // 返回 0 和 SampleSH9 的最大值
            return max(0.0, SampleSH9(coefficients, surfaceWS.normal));
        }
    #endif
}

// 采样阴影贴图
float4 SampleBakedShadows(float2 lightMapUV, Surface surfaceWS)
{
    #if defined(LIGHTMAP_ON)
        return SAMPLE_TEXTURE2D(unity_ShadowMask, samplerunity_ShadowMask, lightMapUV);
    #else
        if (unity_ProbeVolumeParams.x)
        {
            return SampleProbeOcclusion(
                TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
                surfaceWS.position, unity_ProbeVolumeWorldToObject,
                unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
                unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz
            );
        }
        else
        {
            return unity_ProbesOcclusion; 
        }       
    #endif
}

// 给定 光照贴图 uv，获取 GI 结构
GI GetGI(float2 lightMapUV, Surface surfaceWS)
{
    GI gi;
    gi.diffuse = SampleLightMap(lightMapUV) + SampleLightProbe(surfaceWS);
    gi.shadowMask.distance = false;
    gi.shadowMask.shadows = 1.0;

    #if defined(_SHADOW_MASK_DISTANCE)
        gi.shadowMask.distance = true;
        gi.shadowMask.shadows = SampleBakedShadows(lightMapUV, surfaceWS);
    #endif

    return gi;
}

#endif