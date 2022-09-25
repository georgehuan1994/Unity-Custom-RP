#ifndef CUSTOM_POST_FX_PASSES_INCLUDED
#define CUSTOM_POST_FX_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

TEXTURE2D(_PostFXSource);
TEXTURE2D(_PostFXSource2);

float4 _PostFXSource_TexelSize;

bool _BloomBicubicUpsampling; // 是否使用双三次采样 (升采样过程)

float4 _BloomThreshold;

float _BloomIntensity;

float4 _ColorAdjustments;
float4 _ColorFilter;

float4 _WhiteBalance;
float4 _SplitToningShadows;
float4 _SplitToningHighlights;
float4 _ChannelMixerRed;
float4 _ChannelMixerGreen;
float4 _ChannelMixerBlue;
float4 _SMHShadows, _SMHMidtones, _SMHHighlights, _SMHRange;

float4 _ColorGradingLUTParameters;

bool _ColorGradingLUTInLogC;

TEXTURE2D(_ColorGradingLUT);

bool _CopyBicubic;

float4 GetSourceTexelSize()
{
    return _PostFXSource_TexelSize;
}

float Luminance(float3 color, bool useACES)
{
    return useACES ? AcesLuminance(color) : Luminance(color);
}

struct Varyings
{
    float4 positionCS_SS : SV_POSITION;
    float2 screenUV : VAR_SCREEN_UV;
};

Varyings DefaultPassVertex(uint vertexID : SV_VertexID)
{
    Varyings output;

    // vertex0：(-1, -1, 0)
    // vertex1：(-1, 3, 0)
    // vertex2：(3, -1, 0)

    output.positionCS_SS = float4(
        vertexID <= 1 ? -1.0 : 3.0,
        vertexID == 1 ? 3.0 : -1.0,
        0.0, 1.0);

    output.screenUV = float2(
        vertexID <= 1 ? 0.0 : 2.0,
        vertexID == 1 ? 2.0 : 0.0);

    if (_ProjectionParams.x < 0.0)
    {
        // DirectX 的 uv 坐标原点在左上角，需要反转一下
        output.screenUV.y = 1.0 - output.screenUV.y;
    }

    return output;
}

float4 GetSource(float2 screenUV)
{
    // return SAMPLE_TEXTURE2D(_PostFXSource, sampler_linear_clamp, screenUV);
    return SAMPLE_TEXTURE2D_LOD(_PostFXSource, sampler_linear_clamp, screenUV, 0);
}

float4 CopyPassFragment(Varyings input) : SV_TARGET
{
    // return float4(input.screenUV, 0.0, 1.0);
    return GetSource(input.screenUV);
}

float4 BloomHorizontalPassFragment(Varyings input) : SV_TARGET
{
    float3 color = 0.0;

    float offsets[] = {
        -4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0
    };

    float weights[] = {
        0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
        0.19459459, 0.12162162, 0.05405405, 0.01621622
    };

    for (int i = 0; i < 9; i++)
    {
        float offset = offsets[i] * 2.0 * GetSourceTexelSize().x;
        color += GetSource(input.screenUV + float2(offset, 0.0)).rgb * weights[i];
    }

    return float4(color, 1.0);
}

float4 BloomVerticalPassFragment(Varyings input) : SV_TARGET
{
    float3 color = 0.0;

    // float offsets[] = {
    //     -4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0
    // };
    //
    // float weights[] = {
    //     0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
    //     0.19459459, 0.12162162, 0.05405405, 0.01621622
    // };
    //
    // for (int i = 0; i < 9; i++)
    // {
    //     float offset = offsets[i] * 2.0 * GetSourceTexelSize().y;
    //     color += GetSource(input.screenUV + float2(0.0, offset)).rgb * weights[i];
    // }

    float offsets[] = {
        -3.23076923, -1.38461538, 0.0, 1.38461538, 3.23076923
    };

    float weights[] = {
        0.07027027, 0.31621622, 0.22702703, 0.31621622, 0.07027027
    };

    for (int i = 0; i < 5; i++)
    {
        float offset = offsets[i] * GetSourceTexelSize().y;
        color += GetSource(input.screenUV + float2(0.0, offset)).rgb * weights[i];
    }

    return float4(color, 1.0);
}

float4 GetSource2(float2 screenUV)
{
    return SAMPLE_TEXTURE2D_LOD(_PostFXSource2, sampler_linear_clamp, screenUV, 0);
}

float4 GetSourceBicubic(float2 screenUV)
{
    return SampleTexture2DBicubic(TEXTURE2D_ARGS(_PostFXSource, sampler_linear_clamp), screenUV,
                                  _PostFXSource_TexelSize.zwxy, 1.0, 0.0);
}

float4 BloomCombinePassFragment(Varyings input) : SV_TARGET
{
    float3 lowRes;
    if (_BloomBicubicUpsampling)
    {
        lowRes = GetSourceBicubic(input.screenUV).rgb;
    }
    else
    {
        lowRes = GetSource(input.screenUV).rgb;
    }
    float4 highRes = GetSource2(input.screenUV);
    return float4(lowRes * _BloomIntensity + highRes.rgb, highRes.a);
}

float3 ApplyBloomThreshold(float3 color)
{
    float brightness = Max3(color.r, color.g, color.b);
    float soft = brightness + _BloomThreshold.y;
    soft = clamp(soft, 0.0, _BloomThreshold.z);
    soft = soft * soft * _BloomThreshold.w;
    float contribution = max(soft, brightness - _BloomThreshold.x);
    contribution /= max(brightness, 0.00001);
    return color * contribution;
}

float4 BloomScatterPassFragment(Varyings input) : SV_TARGET
{
    float3 lowRes;
    if (_BloomBicubicUpsampling)
    {
        lowRes = GetSourceBicubic(input.screenUV).rgb;
    }
    else
    {
        lowRes = GetSource(input.screenUV).rgb;
    }
    float3 highRes = GetSource2(input.screenUV).rgb;
    return float4(lerp(highRes, lowRes, _BloomIntensity), 1.0);
}

float4 BloomScatterFinalPassFragment(Varyings input) : SV_TARGET
{
    float3 lowRes;
    if (_BloomBicubicUpsampling)
    {
        lowRes = GetSourceBicubic(input.screenUV).rgb;
    }
    else
    {
        lowRes = GetSource(input.screenUV).rgb;
    }
    float4 highRes = GetSource2(input.screenUV);
    lowRes += highRes.rgb - ApplyBloomThreshold(highRes.rgb);
    return float4(lerp(highRes.rgb, lowRes, _BloomIntensity), highRes.a);
}

float4 BloomPrefilterPassFragment(Varyings input) : SV_TARGET
{
    float3 color = ApplyBloomThreshold(GetSource(input.screenUV).rgb);
    return float4(color, 1.0);
}

float4 BloomPrefilterFirefliesPassFragment(Varyings input) : SV_TARGET
{
    float3 color = 0.0;
    float weightSum = 0.0;
    
    float2 offsets[] = {
        float2(0.0, 0.0),
        float2(-1.0, -1.0), float2(-1.0, 1.0), float2(1.0, -1.0), float2(1.0, 1.0)
        //, float2(-1.0, 0.0), float2(1.0, 0.0), float2(0.0, -1.0), float2(0.0, 1.0)
    };
    
    for (int i = 0; i < 5; i++)
    {
        float3 c = GetSource(input.screenUV + offsets[i] * GetSourceTexelSize().xy * 2.0).rgb;
        c = ApplyBloomThreshold(c);
        float w = 1.0 / (Luminance(c) + 1.0);
        color += c * w;
        weightSum += w;
    }

    color /= weightSum;
    return float4(color, 1.0);
}

float3 ColorGradePostExposure(float3 color)
{
    return color * _ColorAdjustments.x;
}

float3 ColorGradeWhiteBalance(float3 color)
{
    color = LinearToLMS(color);
    color *= _WhiteBalance.rgb;
    return LMSToLinear(color);
}

float3 ColorGradingContrast(float3 color, bool useACES)
{
    // return (color - ACEScc_MIDGRAY) * _ColorAdjustments.y + ACEScc_MIDGRAY;
    
    color = useACES ? ACES_to_ACEScc(unity_to_ACES(color)) : LinearToLogC(color);
    color = (color - ACEScc_MIDGRAY) * _ColorAdjustments.y + ACEScc_MIDGRAY;
    return useACES ? ACES_to_ACEScg(ACEScc_to_ACES(color)) : LogCToLinear(color);
}

float3 ColorGradeColorFilter(float3 color)
{
    return color * _ColorFilter.rgb;
}

float3 ColorGradeSplitToning(float3 color, bool useACES)
{
    color = PositivePow(color, 1.0 / 2.2);
    float t = saturate(Luminance(saturate(color), useACES) + _SplitToningShadows.w);
    float3 shadows = lerp(0.5, _SplitToningShadows.rgb, 1-t);
    float3 highlights = lerp(0.5, _SplitToningHighlights.rgb, t);
    color = SoftLight(color, shadows);
    color = SoftLight(color, highlights);
    return PositivePow(color, 2.2);
}

float3 ColorGradingHueShift(float3 color)
{
    color = RgbToHsv(color);
    float hue = color.x + _ColorAdjustments.z;
    color.x = RotateHue(hue, 0.0, 1.0);
    return HsvToRgb(color);
}

float3 ColorGradingSaturation(float3 color, bool useACES)
{
    float luminance = Luminance(color, useACES);
    return (color - luminance) * _ColorAdjustments.w + luminance;
}

float3 ColorGradingChannelMixer(float3 color)
{
    return mul(float3x3(_ChannelMixerRed.rgb,_ChannelMixerGreen.rgb,_ChannelMixerBlue.rgb), color);
}

float3 ColorGradingShadowsMidtonesHighlights(float3 color, bool useACES)
{
    float luminance = Luminance(color, useACES);
    float shadowWeight = 1.0 - smoothstep(_SMHRange.x, _SMHRange.y, luminance);
    float highlightWeight = smoothstep(_SMHRange.z, _SMHRange.w, luminance);
    float midtonesWeight = 1.0 - shadowWeight - highlightWeight;
    return color * _SMHShadows.rgb * shadowWeight +
            color * _SMHMidtones.rgb * midtonesWeight +
            color * _SMHHighlights.rgb * highlightWeight;
}


float3 ColorGrade(float3 color, bool useACES = false) : SV_TARGET
{
    // color = min(color, 60.0);
    color = ColorGradePostExposure(color);
    color = ColorGradeWhiteBalance(color);
    color = ColorGradingContrast(color, useACES);
    color = ColorGradeColorFilter(color);
    color = ColorGradeSplitToning(color, useACES);
    color = ColorGradingChannelMixer(color);
    color = max(color, 0.0);
    color = ColorGradingShadowsMidtonesHighlights(color, useACES);
    color = ColorGradingHueShift(color);
    color = ColorGradingSaturation(color, useACES);
    return max(useACES ? ACEScg_to_ACES(color) : color, 0.0);
}

float3 GetColorGradeLUT(float2 uv, bool useACES = false)
{
    // float3 color = float3(uv, 0.0);
    float3 color = GetLutStripValue(uv, _ColorGradingLUTParameters);
    // return ColorGrade(color, useACES);
    return ColorGrade(_ColorGradingLUTInLogC ? LogCToLinear(color) : color, useACES);
}

float4 ColorGradeNonePassFragment(Varyings input) : SV_TARGET
{
    float3 color = GetColorGradeLUT(input.screenUV);
    return float4(color, 1.0);
}

float4 ColorGradeReinhardPassFragment(Varyings input) : SV_TARGET
{
    float3 color = GetColorGradeLUT(input.screenUV);
    color.rgb /= color.rgb + 1.0;
    return float4(color, 1.0);
}

float4 ColorGradeNeutralPassFragment(Varyings input) : SV_TARGET
{
    float3 color = GetColorGradeLUT(input.screenUV);
    color.rgb = NeutralTonemap(color.rgb);
    return float4(color, 1.0);
}

float4 ColorGradeACESPassFragment(Varyings input) : SV_TARGET
{
    float3 color = GetColorGradeLUT(input.screenUV, true);
    color.rgb = AcesTonemap(color.rgb);
    return float4(color, 1.0);
}

float3 ApplyColorGradingLUT(float3 color)
{
    return ApplyLut2D(TEXTURE2D_ARGS(_ColorGradingLUT, sampler_linear_clamp),
        saturate(_ColorGradingLUTInLogC ? LinearToLogC(color) : color),
        _ColorGradingLUTParameters.xyz);
}

float4 ApplyColorGradingPassFragment(Varyings input) : SV_TARGET
{
    float4 color = GetSource(input.screenUV);
    color.rgb = ApplyColorGradingLUT(color.rgb);
    return color;
}

float4 ApplyColorGradingWithLumaPassFragment(Varyings input) : SV_TARGET
{
    float4 color = GetSource(input.screenUV);
    color.rgb = ApplyColorGradingLUT(color.rgb);
    color.a = sqrt(Luminance(color.rgb));
    return color;
}

float4 FinalPassFragmentRescale(Varyings input) : SV_TARGET
{
    if (_CopyBicubic)
    {
        return GetSourceBicubic(input.screenUV);
    }
    else
    {
        return GetSource(input.screenUV);
    }
}

#endif
