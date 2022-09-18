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

#if defined(_SHADOW_MASK_ALWAYS) || defined(_SHADOW_MASK_DISTANCE)
    #define SHADOWS_SHADOWMASK
#endif

// UnityInstancing.hlsl 的作用就是重定义这些宏以访问实例数据的数组
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

SAMPLER(sampler_linear_clamp);
SAMPLER(sampler_point_clamp);

bool IsOrthographicCamera()
{
    return unity_OrthoParams.w;
}

float OrthographicDepthBufferToLinear(float rawDepth)
{
    #if UNITY_REVERSED_Z
        rawDepth = 1.0 - rawDepth;
    #endif

    // (远平面 - 近平面) * 裁剪空间深度 + 近平面
    return (_ProjectionParams.z - _ProjectionParams.y) * rawDepth + _ProjectionParams.y;
}

#include "Fragment.hlsl"

float Square (float v) {
    return v * v;
}

float DistanceSqure(float3 pA, float3 pB)
{
    return dot(pA - pB, pA - pB);
}

void ClipLOD(Fragment fragment, float fade)
{
    #ifdef LOD_FADE_CROSSFADE
        float dither = InterleavedGradientNoise(fragment.positionSS, 0);
        clip(fade + (fade < 0.0 ? dither : -dither));
    #endif
}

float3 DecodeNormal(float4 sample, float scale)
{
    #ifdef UNITY_NO_DXT5nm
        return UnpackNormalRGB(sample, scale);
    #else
        return UnpackNormalmapRGorAG(sample, scale);
    #endif
}

float3 NormalTangentToWorld(float3 normalTS, float3 normalWS, float4 tangentWS)
{
    float3x3 tangentToWorld = CreateTangentToWorld(normalWS, tangentWS.xyz, tangentWS.w);
    return TransformTangentToWorld(normalTS, tangentToWorld);
}

#endif