/* ShaderLibrary/Common.hlsl */

#ifndef CUSTOM_COMMON_INCLUDED
#define CUSTOM_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "UnityInput.hlsl"

// SpaceTransforms.hlsl 中并没有定义矩阵变量，而是通过宏定义获取
// 因为要把这些矩阵都存到 CBUFFER 中，所以使用 #define 把它们转换为变量

#define UNITY_MATRIX_M unity_ObjectToWorld          // Model-View-Projection
#define UNITY_MATRIX_I_M unity_WorldToObject        // Model-View-Projection 逆矩阵
#define UNITY_MATRIX_V unity_MatrixV                // View
#define UNITY_MATRIX_VP unity_MatrixVP              // View-Projection
#define UNITY_MATRIX_P glstate_matrix_projection    // 变换信息

// UnityInstancing.hlsl 的作用就是重定义这些宏以访问实例数据的数组
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

float Square (float v) {
    return v * v;
}

#endif