/* ShaderLibrary/Shadows.hlsl */

#ifndef CUSTOM_SHADOWS_INCLUDED  // 引用保护
#define CUSTOM_SHADOWS_INCLUDED

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4

TEXTURE2D(_DirectionalShadowAtlas);
SAMPLER(sampler_DirectionalShadowAtlas);

CBUFFER_START(_CustiomShadow)
    float4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT];
CBUFFER_END

#endif