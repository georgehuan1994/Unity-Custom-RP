#ifndef CUSTOM_FXAA_PASS_INCLUDE
#define  CUSTOM_FXAA_PASS_INCLUDE

// x: 对比度绝对阈值
// y: 对比度相对阈值
// z: FXAA 强度
float4 _FXAAConfig; 

float GetLuma(float2 uv, float uOffset = 0.0, float vOffset = 0.0)
{
    uv += float2(uOffset, vOffset) * GetSourceTexelSize().xy;
    
    #if defined(FXAA_ALPHA_CONTAINS_LUMA)
        return GetSource(uv).a;                 // ColorGrading 的透明通道，保存了 Gamma 2.0 亮度
    #else
        return GetSource(uv).g;                 // 绿色通道
        return sqrt(Luminance(GetSource(uv)));  // Gamma 2.0 亮度
        return Luminance(GetSource(uv));        // 线性空间亮度
    #endif
}

struct LumaNeighborhood
{
    float m, n, e, s, w;    // middle, north, east, south, west
    float ne, se, sw, nw;   // north-east, south-east, south-west, north-west
    float hightest, lowest; // 局部最高亮度，局部最低亮度
    float range;            // 亮度范围
};

LumaNeighborhood GetLumaNeighborhood(float2 uv)
{
    LumaNeighborhood luma;
    luma.m = GetLuma(uv);
    luma.n = GetLuma(uv, 0.0, 1.0);
    luma.e = GetLuma(uv, 1.0, 0.0);
    luma.s = GetLuma(uv, 0.0, -1.0);
    luma.w = GetLuma(uv, -1.0, 0.0);
    luma.ne = GetLuma(uv, 1.0, 1.0);
    luma.se = GetLuma(uv, 1.0, -1.0);
    luma.sw = GetLuma(uv, -1.0, -1.0);
    luma.nw = GetLuma(uv, -1.0, 1.0);
    luma.hightest = max(max(max(max(luma.m, luma.n), luma.e), luma.s), luma.w);
    luma.lowest = min(min(min(min(luma.m, luma.n), luma.e), luma.s), luma.w);
    luma.range = luma.hightest - luma.lowest;
    return luma;
}

bool CanSkipFXAA(LumaNeighborhood luma)
{
    return luma.range < _FXAAConfig.y * luma.hightest;
    return luma.range < _FXAAConfig.x;
}

float GetSubpixelBlendFactor(LumaNeighborhood luma)
{
    // 低通滤波
    float filter = 2.0 * (luma.n + luma.e + luma.s + luma.w);
    filter += luma.ne + luma.nw + luma.se + luma.sw;
    filter *= 1.0 / 12.0;
    // 高通滤波
    filter = abs(filter - luma.m);
    // 归一化
    filter = saturate(filter / luma.range);
    // 平方平滑
    filter = smoothstep(0, 1, filter);
    return filter * filter * _FXAAConfig.z;
    
    return filter;
}

bool IsHorizontalEdge(LumaNeighborhood luma)
{
    float horizontal =
        2.0 * abs(luma.n + luma.s - 2.0 * luma.m) +
        abs(luma.ne + luma.se - 2.0 * luma.e) +
        abs(luma.nw + luma.sw - 2.0 * luma.w);
    
    float vertical =
        2.0 * abs(luma.e + luma.w - 2.0 * luma.m) +
        abs(luma.ne + luma.nw - 2.0 * luma.n) +
        abs(luma.se + luma.sw - 2.0 * luma.s);
    
    return horizontal > vertical;
}

struct FXAAEdge
{
    bool isHorizontal;
    float pixelStep;    // 步长
    float lumaGradient, otherLuma;
};

FXAAEdge GetFXAAEdge(LumaNeighborhood luma)
{
    FXAAEdge edge;
    float lumaP, lumaN;
    edge.isHorizontal = IsHorizontalEdge(luma);
    if (edge.isHorizontal)
    {
        edge.pixelStep = GetSourceTexelSize().y;
        lumaP = luma.n;
        lumaN = luma.s;
    }
    else
    {
        edge.pixelStep = GetSourceTexelSize().x;
        lumaP = luma.e;
        lumaN = luma.w;
    }
    float gradientP = abs(lumaP - luma.m);
    float gradientN = abs(lumaN - luma.m);

    if (gradientP < gradientN)
    {
        edge.pixelStep = -edge.pixelStep;
        edge.lumaGradient = gradientN;
        edge.otherLuma = lumaN;
    }
    else
    {
        edge.lumaGradient = gradientP;
        edge.otherLuma = lumaP;
    }
    
    return edge;
}

// #define EXTRA_EDGE_STEPS 3
// #define EDGE_STEP_SIZES 1.0, 1.0, 1.0
// #define LAST_EDGE_STEP_GUESS 1.0

#if defined(FXAA_QUALITY_LOW)
    #define EXTRA_EDGE_STEPS 3
    #define EDGE_STEP_SIZES 1.5, 2.0, 2.0
    #define LAST_EDGE_STEP_GUESS 8.0
#elif defined(FXAA_QUALITY_MEDIUM)
    #define EXTRA_EDGE_STEPS 8
    #define EDGE_STEP_SIZES 1.5, 2.0, 2.0, 2.0, 2.0, 2.0, 2.0, 4.0
    #define LAST_EDGE_STEP_GUESS 8.0
#else
    #define EXTRA_EDGE_STEPS 10
    #define EDGE_STEP_SIZES 1.0, 1.0, 1.0, 1.0, 1.5, 2.0, 2.0, 2.0, 2.0, 4.0
    #define LAST_EDGE_STEP_GUESS 8.0
#endif

static const float edgeStepSizes[EXTRA_EDGE_STEPS] = { EDGE_STEP_SIZES };

float GetEdgeBlendFactor(LumaNeighborhood luma, FXAAEdge edge, float2 uv)
{
    float2 edgeUV = uv;
    float2 uvStep = 0.0;
    
    if (edge.isHorizontal)
    {
        edgeUV.y += 0.5 * edge.pixelStep;
        uvStep.x = GetSourceTexelSize().x;
    }
    else
    {
        edgeUV.x += 0.5 * edge.pixelStep;
        uvStep.y = GetSourceTexelSize().y;
    }

    float edgeLuma = 0.5 * (luma.m + edge.otherLuma);
    float gradientThreshold = 0.25 * edge.lumaGradient;

    float2 uvP = edgeUV + uvStep;
    // float lumaGradientP = abs(GetLuma(uvP) - edgeLuma);
    // bool atEndP = lumaGradientP >= gradientThreshold;
    float lumaDeltaP = GetLuma(uvP) - edgeLuma;
    bool atEndP = abs(lumaDeltaP) >= gradientThreshold;

    UNITY_UNROLL
    for (int i = 0; i < EXTRA_EDGE_STEPS && !atEndP; i++)
    {
        uvP += uvStep * edgeStepSizes[i];
        // lumaGradientP = abs(GetLuma(uvP) - edgeLuma);
        // atEndP = lumaGradientP >= gradientThreshold;
        lumaDeltaP = GetLuma(uvP) - edgeLuma;
        atEndP = abs(lumaDeltaP) >= gradientThreshold;
    }

    if (!atEndP)
    {
        uvP += uvStep * LAST_EDGE_STEP_GUESS;
    }

    float2 uvN = edgeUV - uvStep;
    // float lumaGradientN = abs(GetLuma(uvN) - edgeLuma);
    // bool atEndN = lumaGradientN >= gradientThreshold;
    float lumaDeltaN = GetLuma(uvN) - edgeLuma;
    bool atEndN = abs(lumaDeltaN) >= gradientThreshold;
    
    UNITY_UNROLL
    for (int j = 0; j < EXTRA_EDGE_STEPS && !atEndN; j++)
    {
        uvN -= uvStep * edgeStepSizes[j];
        // lumaGradientN = abs(GetLuma(uvN) - edgeLuma);
        // atEndN = lumaGradientN >= gradientThreshold;
        lumaDeltaN = GetLuma(uvN) - edgeLuma;
        atEndN = abs(lumaDeltaN) >= gradientThreshold;
    }

    if (!atEndN)
    {
        uvN -= uvStep * LAST_EDGE_STEP_GUESS;
    }

    float distanceToEndP, distanceToEndN;
    
    if (edge.isHorizontal)
    {
        distanceToEndP = uvP.x - uv.x;
        distanceToEndN = uv.x - uvN.x;
    }
    else
    {
        distanceToEndP = uvP.y - uv.y;
        distanceToEndN = uv.y - uvN.y;
    }

    float distanceToNearestEnd;
    bool deltaSign;
    if (distanceToEndP <= distanceToEndN)
    {
        distanceToNearestEnd = distanceToEndP;
        deltaSign = lumaDeltaP >= 0;
    }
    else
    {
        distanceToNearestEnd = distanceToEndN;
        deltaSign = lumaDeltaN >= 0;
    }

    
    if (deltaSign == (luma.m - edgeLuma >= 0))
    {
        return 0.0;
    }
    else
    {
        return 0.5 - distanceToNearestEnd / (distanceToEndP + distanceToEndN);
        return 10.0 * distanceToNearestEnd;
    }

    return 10.0 * distanceToNearestEnd;
    
    return 10.0 * distanceToEndP;
    return atEndP;
    return edge.lumaGradient;
}

float4 FXAAPassFragment(Varyings input) : SV_TARGET
{
    LumaNeighborhood luma = GetLumaNeighborhood(input.screenUV);

    if (CanSkipFXAA(luma))
    {
        return GetSource(input.screenUV);
        return 0.0;
    }

    FXAAEdge edge = GetFXAAEdge(luma);

    // float blendFactor = GetSubpixelBlendFactor(luma);
    // float blendFactor = GetEdgeBlendFactor(luma, edge, input.screenUV);
    float blendFactor = max(GetSubpixelBlendFactor(luma), GetEdgeBlendFactor(luma, edge, input.screenUV)) ;
    
    // return blendFactor;
    
    float2 blendUV = input.screenUV;
    if (edge.isHorizontal)
    {
        blendUV.y += blendFactor * edge.pixelStep;
    }
    else
    {
        blendUV.x += blendFactor * edge.pixelStep;
    }
    return GetSource(blendUV);

    if (edge.isHorizontal)
    {
        return edge.pixelStep > 0.0 ?
            float4(1, 0, 0, 0) :
            float4(0, 0, 1, 0);
    }
    else
    {
        return edge.pixelStep > 0.0 ?
            float4(1, 1, 0, 0) :
            float4(0, 1, 1, 0);
    }
    return GetSubpixelBlendFactor(luma);
    
    return edge.isHorizontal ? float4(0, GetSubpixelBlendFactor(luma), 0, 0) : GetSubpixelBlendFactor(luma);

    return GetSubpixelBlendFactor(luma);
    return luma.range;
    return luma.lowest;
    return luma.hightest;
    return luma.m;
    return GetLuma(input.screenUV);
    return GetSource(input.screenUV);
}

#endif
