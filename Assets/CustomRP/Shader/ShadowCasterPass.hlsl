/* Shader/ShadowCasterPass.hlsl */

#ifndef CUSTOM_SHADOW_CASTER_PASS_INCLUDED  // 引用保护
#define CUSTOM_SHADOW_CASTER_PASS_INCLUDED

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

bool _ShadowPancaking;  // 是否使用 ShadowPancaking

Varyings ShadowCasterPassVertex(Attributes input)
{
    Varyings output;

    // 允许着色器函数访问实例 ID
    UNITY_SETUP_INSTANCE_ID(input);
    // 将实例 ID 从输入结构复制到顶点着色器中的输出结构
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    
    float3 positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(positionWS);

    if (_ShadowPancaking)
    {
        #if UNITY_REVERSED_Z
        output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
        #else
        output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
        #endif 
    }
    
    output.baseUV = TransformBaseUV(input.baseUV);
    
    return output;
}

void ShadowCasterPassFragment(Varyings input)
{
    UNITY_SETUP_INSTANCE_ID(input);

    ClipLOD(input.positionCS.xy, unity_LODFade.x);
    
    InputConfig config = GetInputConfig(input.baseUV);
    float4 base = GetBase(config);
    
    #if defined(_SHADOWS_CLIP)
        clip(base.a - GetCutoff(config));
    #elif defined(_SHADOWS_DITHER)
        // 根据给定的屏幕空间 XY 坐标生成随机值，第二个参数即是否需要动起来，这里置为 0
        float dither = InterleavedGradientNoise(input.positionCS.xy, 0);
        clip(base.a - dither);
    #endif
}

#endif