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

bool RenderingLayersOverlap(Surface surface, Light light)
{
    return (surface.renderingLayerMask & light.renderingLayerMask) != 0;
}

// 表面入射光量 (颜色) * 物体表面颜色 BRDF (漫反射 + 高光反射) - 所有可见光源
float3 GetLighting(Surface surfaceWS, BRDF brdf, GI gi)
{
    ShadowData shadowData = GetShadowData(surfaceWS);
    shadowData.shadowMask = gi.shadowMask;

    float3 color = IndirectBRDF(surfaceWS, brdf, gi.diffuse, gi.specular);

    // 平行光
    for (int i = 0; i < GetDirectionalLightCount(); i++)
    {
        Light light = GetDirectionLight(i, surfaceWS, shadowData);
        if (RenderingLayersOverlap(surfaceWS, light))
        {
            color += GetLighting(surfaceWS, brdf, light);
        }
    }

    // 非平行光
    #if defined(_LIGHTS_PER_OBJECT)
        for (int j = 0; j < min(unity_LightData.y, 8); j++)
        {
            int lightIndex = unity_LightIndices[(uint)j/4][(uint)j%4];
            Light light = GetOtherLight(lightIndex, surfaceWS, shadowData);
            if (RenderingLayersOverlap(surfaceWS, light))
            {
                color += GetLighting(surfaceWS, brdf, light);
            }
        }
    #else
        for (int j = 0; j < GetOtherLightCount(); j++)
        {
            Light light = GetOtherLight(j, surfaceWS, shadowData);
            if (RenderingLayersOverlap(surfaceWS, light))
            {
                color += GetLighting(surfaceWS, brdf, light);
            }
        }
    #endif

    return color;
}

#endif
