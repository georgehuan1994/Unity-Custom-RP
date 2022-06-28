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
CBUFFER_END

float4x4 unity_MatrixV;
float4x4 unity_MatrixVP;
float4x4 glstate_matrix_projection;

#endif