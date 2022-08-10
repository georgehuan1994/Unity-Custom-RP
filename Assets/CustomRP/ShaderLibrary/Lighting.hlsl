/* ShaderLibrary/Lighting.hlsl */

#ifndef CUSTOM_LIGHTING_INCLUDED  // 引用保护
#define CUSTOM_LIGHTING_INCLUDED

// #include "../ShaderLibrary/GI.hlsl"
// #include "../ShaderLibrary/Surface.hlsl"

// 计算给定表面的入射光量 (颜色)
float3 IncomingLight(Surface surface, Light light)
{
    return saturate(dot(surface.normal, light.direction) * light.attenuation) * light.color;
}

// 表面入射光量 (颜色) * 物体表面颜色 BRDF (漫反射 + 高光反射)
float3 GetLighting(Surface surface, BRDF brdf, Light light)
{
    return IncomingLight(surface, light) * DirectBRDF(surface, brdf, light);
}

// 表面入射光量 (颜色) * 物体表面颜色 BRDF (漫反射 + 高光反射) - 所有可见光源
float3 GetLighting(Surface surfaceWS, BRDF brdf, GI gi)
{
    ShadowData shadowData = GetShadowData(surfaceWS);
    
    shadowData.shadowMask = gi.shadowMask;
    // return gi.shadowMask.shadows.rgb;
    
    // float3 color = gi.diffuse * brdf.diffuse;
    // float3 color = IndirectBRDF(surfaceWS, brdf, gi.diffuse, 1.0);
    float3 color = IndirectBRDF(surfaceWS, brdf, gi.diffuse, gi.specular);
    
    for (int i = 0; i < GetDirectionalLightCount(); i++)
    {
        Light light = GetDirectionLight(i, surfaceWS, shadowData);
        color += GetLighting(surfaceWS, brdf, light);
    }
    return color;
}

#endif