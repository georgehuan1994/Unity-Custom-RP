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
    float4 positionCS : SV_POSITION;
    float3 normalWS : VAR_NORMAL;   // VAR_NORMAL 没有特别的语义，只是一个自定义的标识
    float4 tangentWS : VAR_TANGENT;
    float2 baseUV : VAR_BASE_UV;    // VAR_BASE_UV 没有特别的语义，只是一个自定义的标识
    float2 detailUV : VAR_DETAIL_UV;
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
    output.positionCS = TransformWorldToHClip(output.positionWS);

    // 访问实例化常量缓冲区中的每个实例着色器属性
    output.baseUV = TransformBaseUV(input.baseUV);
    output.detailUV = TransformDetailUV(input.baseUV);
    
    // 将顶点法线转换到世界空间下
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);

    // 获取着色点在世界空间下归一化的切线方向
    output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
    
    return output;
}

float4 LitPassFragment (Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    ClipLOD(input.positionCS.xy, unity_LODFade.x);
    
    float4 base = GetBase(input.baseUV, input.detailUV);
    
    #if defined(_CLIPPING)
    clip(base.a - GetCutoff(input.baseUV));
    #endif
    
    Surface surface;
    surface.position = input.positionWS;
    // surface.normal = normalize(input.normalWS);
    surface.normal = NormalTangentToWorld(GetNormalTS(input.baseUV, input.detailUV), input.normalWS, input.tangentWS);
    surface.interpolateNormal = input.normalWS;
    surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
    surface.depth = -TransformWorldToView(input.positionWS).z;
    surface.color = base.rgb;
    surface.alpha = base.a;
    surface.metallic = GetMetallic(input.baseUV);
    surface.occlusion = GetOcclusion(input.baseUV);
    surface.smoothness = GetSmoothness(input.baseUV, input.detailUV);
    surface.fresnelStrength = GetFresnel(input.baseUV);
    surface.dither = InterleavedGradientNoise(input.positionCS.xy, 0);

    #if defined(_PREMULTIPLY_ALPHA)
    BRDF brdf = GetBRDF(surface, true);
    #else
    BRDF brdf = GetBRDF(surface);
    #endif

    GI gi = GetGI(GI_FRAGMENT_DATA(input), surface, brdf);
    float3 color = GetLighting(surface, brdf, gi);
    color += GetEmission(input.baseUV);
    return float4(color, surface.alpha);
}

#endif