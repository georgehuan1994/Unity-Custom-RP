/* Shader/LitPass.hlsl */

#ifndef CUSTOM_LIT_PASS_INCLUDED  // 引用保护
#define CUSTOM_LIT_PASS_INCLUDED

#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

struct Attributes
{
    float3 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 baseUV : TEXCOORD0;
    GI_ATTRIBUTE_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID  // 将对象的索引添加到顶点着色器输入结构中
};

struct Varyings
{
    float3 positionWS : VAR_POSITION;
    float4 positionCS_SS : SV_POSITION;
    float3 normalWS : VAR_NORMAL;   // VAR_NORMAL 没有特别的语义，只是一个自定义的标识
    #if defined(_NORMAL_MAP)
    float4 tangentWS : VAR_TANGENT;
    #endif
    float2 baseUV : VAR_BASE_UV;    // VAR_BASE_UV 没有特别的语义，只是一个自定义的标识
    #if defined(_DETAIL_MAP)
    float2 detailUV : VAR_DETAIL_UV;
    #endif
    GI_ATTRIBUTE_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID  // 将对象的索引添加到顶点着色器输出结构中
};

Varyings LitPassVertex (Attributes input)
{
    Varyings output;

    // 允许着色器函数访问实例 ID
    UNITY_SETUP_INSTANCE_ID(input);
    // 将实例 ID 从输入结构复制到顶点着色器中的输出结构
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    // 传递 GI 数据
    TRANSFER_GI_DATA(input, output);
    
    output.positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS_SS = TransformWorldToHClip(output.positionWS);

    // 访问实例化常量缓冲区中的每个实例着色器属性
    output.baseUV = TransformBaseUV(input.baseUV);
    #if defined(_DETAIL_MAP)
    output.detailUV = TransformDetailUV(input.baseUV);
    #endif
    
    // 将顶点法线转换到世界空间下
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);

    #if defined(_NORMAL_MAP)
    // 获取着色点在世界空间下归一化的切线方向
    output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
    #endif
    
    return output;
}

float4 LitPassFragment (Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    
    InputConfig config = GetInputConfig(input.positionCS_SS, input.baseUV);
    // return float4(config.fragment.depth.xxx / 20.0, 1.0);
    ClipLOD(config.fragment, unity_LODFade.x);
    
    #if defined(_MASK_MAP)
    config.useMask = true;
    #endif

    #if defined(_DETAIL_MAP)
    config.detailUV = input.detailUV;
    config.useDetail = true;
    #endif
    
    float4 base = GetBase(config);
    
    #if defined(_CLIPPING)
    clip(base.a - GetCutoff(config));
    #endif
    
    Surface surface;
    surface.position = input.positionWS;
    
    #if defined(_NORMAL_MAP)
    surface.normal = NormalTangentToWorld(GetNormalTS(config), input.normalWS, input.tangentWS);
    surface.interpolateNormal = input.normalWS;
    #else
    surface.normal = normalize(input.normalWS);
    surface.interpolateNormal = surface.normal;
    #endif
    
    surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
    surface.depth = -TransformWorldToView(input.positionWS).z;
    surface.color = base.rgb;
    surface.alpha = base.a;
    surface.metallic = GetMetallic(config);
    surface.occlusion = GetOcclusion(config);
    surface.smoothness = GetSmoothness(config);
    surface.fresnelStrength = GetFresnel(config);
    surface.dither = InterleavedGradientNoise(config.fragment.positionSS, 0);
    surface.renderingLayerMask = asuint(unity_RenderingLayer.x);

    #if defined(_PREMULTIPLY_ALPHA)
    BRDF brdf = GetBRDF(surface, true);
    #else
    BRDF brdf = GetBRDF(surface);
    #endif

    GI gi = GetGI(GI_FRAGMENT_DATA(input), surface, brdf);
    float3 color = GetLighting(surface, brdf, gi);
    color += GetEmission(config);
    return float4(color, GetFinalAlpha(surface.alpha));
}

#endif