/* Shader/UnlitPass.hlsl */

#ifndef CUSTOM_UNLIT_PASS_INCLUDED  // 引用保护
#define CUSTOM_UNLIT_PASS_INCLUDED

struct Attributes
{
    float3 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID  // 将对象的索引添加到顶点着色器输入结构中
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 baseUV : VAR_BASE_UV;    // VAR_BASE_UV 没有特别的语义，只是一个自定义的标识
    UNITY_VERTEX_INPUT_INSTANCE_ID  // 将对象的索引添加到顶点着色器输出结构中
};

Varyings UnlitPassVertex (Attributes input)
{
    Varyings output;
    // 允许着色器函数访问实例 ID
    UNITY_SETUP_INSTANCE_ID(input);
    // 将实例 ID 从输入结构复制到顶点着色器中的输出结构
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    
    float3 positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(positionWS);

    // 访问实例化常量缓冲区中的每个实例着色器属性
    // float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
    // output.baseUV = input.baseUV * baseST.xy + baseST.zw;
    output.baseUV = TransformBaseUV(input.baseUV);
    
    return output;
}

float4 UnlitPassFragment (Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);

    // 使用采样器 sampler_BaseMap，从 _BaseMap 中采样
    // float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
    // float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
    // float4 base = baseMap * baseColor;
    
    InputConfig config = GetInputConfig(input.baseUV);
    float4 base = GetBase(config);
    
    #if defined(_CLIPPING)
    clip(base.a - GetCutoff(config));
    #endif
    
    return float4(base.rgb, GetFinalAlpha(base.a));
}

#endif