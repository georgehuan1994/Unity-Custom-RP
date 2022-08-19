/* Shader/UnityInput.hlsl */

#ifndef CUSTOM_UNITY_INPUT_INCLUDED
#define CUSTOM_UNITY_INPUT_INCLUDED

// 把这些常用的矩阵和属性，定义在名为 UnityPerDraw 的常量缓冲区中，以使用 SRP Batching
CBUFFER_START(UnityPerDraw)
    float4x4 unity_ObjectToWorld;
    // 下面这些矩阵暂时用不上，但也要加上才能编译
    // 声明在 CBUFFER 外的话也可以通过编译，但不能正常使用 SRP Batching
    float4x4 unity_WorldToObject;
    float4 unity_LODFade;
    float4 unity_WorldTransformParams;

    real4 unity_LightData;          // y 分量为每物体光源数量
    real4 unity_LightIndices[2];    // 两个 Vector4 共 8 个每物体的光源索引

    // 光照探针中的遮挡数据
    float4 unity_ProbesOcclusion;

    float4 unity_SpecCube0_HDR;

    // 2UV 变换作为 UnityPerDraw 缓冲区的一部分传递给 GPU
    float4 unity_LightmapST;
    float4 unity_DynamicLightmapST;

    // 红光、绿光、蓝光 三阶多项式分量
    float4 unity_SHAr;
    float4 unity_SHAg;
    float4 unity_SHAb;
    float4 unity_SHBr;
    float4 unity_SHBg;
    float4 unity_SHBb;
    float4 unity_SHC;

    float4 unity_ProbeVolumeParams;
    float4x4 unity_ProbeVolumeWorldToObject;
    float4 unity_ProbeVolumeSizeInv;
    float4 unity_ProbeVolumeMin;
CBUFFER_END

float3 _WorldSpaceCameraPos;
float4x4 unity_MatrixV;
float4x4 unity_MatrixVP;
float4x4 glstate_matrix_projection;

#endif