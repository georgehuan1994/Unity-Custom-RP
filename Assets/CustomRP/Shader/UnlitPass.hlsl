/* Shader/UnlitPass.hlsl */

#ifndef CUSTOM_UNLIT_PASS_INCLUDED  // 引用保护
#define CUSTOM_UNLIT_PASS_INCLUDED

struct Attributes
{
    float3 positionOS : POSITION;
    float4 color : COLOR;           // 顶点颜色
    
    #if defined(_FLIPBOOK_BLENDING)
        float4 baseUV : TEXCOORD0;          // UV2
        float flipbookBlending : TEXCOORD1; // Flipbook 混合因子
    #else
        float2 baseUV : TEXCOORD0;
    #endif
    
    UNITY_VERTEX_INPUT_INSTANCE_ID  // 将对象的索引添加到顶点着色器输入结构中
};

struct Varyings
{
    float4 positionCS_SS : SV_POSITION; // 屏幕空间位置
    
    #if defined(_VERTEX_COLORS)
        float4 color : VAR_COLOR;   // 如果使用了顶点色，将其传递给片元着色器
    #endif

    float2 baseUV : VAR_BASE_UV;    // VAR_BASE_UV 没有特别的语义，只是一个自定义的标识

    #if defined(_FLIPBOOK_BLENDING)
        float3 flipbookUVB : VAR_FLIPBOOK;  // UV2 和 Flipbook Blending Factory
    #endif

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
    output.positionCS_SS = TransformWorldToHClip(positionWS);
    #if defined(_VERTEX_COLORS)
        output.color = input.color;
    #endif
    
    output.baseUV.xy = TransformBaseUV(input.baseUV.xy);
    #if defined(_FLIPBOOK_BLENDING)
        output.flipbookUVB.xy = TransformBaseUV(input.baseUV.zw);
        output.flipbookUVB.z = input.flipbookBlending;
    #endif
    
    return output;
}

float4 UnlitPassFragment (Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    
    InputConfig config = GetInputConfig(input.positionCS_SS, input.baseUV);
    // return float4(config.fragment.depth.xxx / 20.0, 1.0);
    // return GetBufferColor(config.fragment, 0.05);
    
    #if defined(_VERTEX_COLORS)
        config.color = input.color;
    #endif
    
    #if defined(_FLIPBOOK_BLENDING)
        config.flipbookUVB = input.flipbookUVB;
        config.flipbookBlending = true;
    #endif

    #if defined(_NEAR_FADE)
        config.nearFade = true;
    #endif

    #if defined(_SOFT_PARTICLES)
        config.softParticles = true;
    #endif
    
    float4 base = GetBase(config);
    
    #if defined(_CLIPPING)
        clip(base.a - GetCutoff(config));
    #endif

    #if defined(_DISTORTION)
        // float2 distortion = GetDistortion(config);
        // base.rgb = GetBufferColor(config.fragment, distortion).rgb;
    
        // float2 distortion = GetDistortion(config) * base.a;
        // base.rgb = GetBufferColor(config.fragment, distortion).rgb;
    
        float2 distortion = GetDistortion(config) * base.a;
        base.rgb = lerp(GetBufferColor(config.fragment, distortion).rgb, base.rgb,saturate(base.a - GetDistortionBlend(config)));
    #endif
    
    return float4(base.rgb, GetFinalAlpha(base.a));
}

#endif