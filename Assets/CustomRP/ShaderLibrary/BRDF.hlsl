/* ShaderLibrary/BRDF.hlsl */

#ifndef CUSTOM_BRDF_INCLUDED  // 引用保护
#define CUSTOM_BRDF_INCLUDED

// #include "../ShaderLibrary/Surface.hlsl"

struct BRDF
{
    float3 diffuse;     // 漫反射颜色
    float3 specular;    // 高光反射颜色
    float roughness;    // 粗糙度
    float perceptualRoughness;  // 感官粗糙度
    float fresnel;      // 菲涅尔反射系数
};

// 最小反射率
#define MIN_REFLECTIVITY 0.04

// 将反射率的范围钳制在 0 ~ 0.96 之间
float OneMinusReflectivity(float metallic)
{
    float range = 1.0 - MIN_REFLECTIVITY;
    return range - metallic * range;
}

// 根据表面数据获取 BRDF 信息
BRDF GetBRDF(Surface surface, bool applyAlphaToDiffuse = false)
{
    BRDF brdf;

    // 通常金属仅通过镜面反射来反射所有光，即没有漫反射
    // 所以用 1 - metallic 得到漫反射系数，当金属度为 1 时，物体完全没有漫反射
    float oneMinusReflectivity = OneMinusReflectivity(surface.metallic);
    brdf.diffuse = surface.color * oneMinusReflectivity;

    if (applyAlphaToDiffuse)
    {
        // 预乘 Alpha 的漫反射颜色
        brdf.diffuse *= surface.alpha;
    }

    // 根据能量守恒：镜面反射颜色 = 表面颜色 - 漫反射颜色，但这忽略了金属会影响镜面反射的颜色的事实
    // brdf.specular = surface.color - brdf.diffuse;
    // 电介质表面的镜面反射颜色应该是白色
    // 所以可以使用 metallic 在最小反射率和表面颜色之间进行插值来实现
    brdf.specular = lerp(MIN_REFLECTIVITY, surface.color, surface.metallic);

    // 迪士尼 BDRF 光照模型 roughness = pow((1.0 - surface.metallic), 2);
    // float perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);
    // brdf.roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
    
    brdf.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);
    brdf.roughness = PerceptualRoughnessToRoughness(brdf.perceptualRoughness);

    // 菲涅尔
    brdf.fresnel = saturate(surface.smoothness + 1.0 - oneMinusReflectivity);
    return brdf;
}

// 计算高光反射强度
float SpecularStrength(Surface surface, BRDF brdf, Light light)
{
    float3 h = SafeNormalize(light.direction + surface.viewDirection);   // 半角向量
    float nh2 = Square(saturate(dot(surface.normal, h)));                   // 法线与半角向量点乘的平方
    float lh2 = Square(saturate(dot(surface.viewDirection, h)));            // 视线与半角向量点乘的平方
    float r2 = Square(brdf.roughness);                                        // 粗糙度的平方
    float d2 = Square(nh2 * (r2 - 1.0) + 1.00001);
    float normalization = brdf.roughness * 4.0 + 2.0;
    return r2 / (d2 * max(0.1, lh2) * normalization);
}

// 平行光的 BRDF (漫反射 + 高光反射) 颜色值
float3 DirectBRDF(Surface surface, BRDF brdf, Light light)
{
    return brdf.diffuse + SpecularStrength(surface, brdf, light) * brdf.specular;
}

// 间接光的 BRDF
float3 IndirectBRDF(Surface surface, BRDF brdf, float3 diffuse, float3 specular)
{
    float fresnelStrength = Pow4(1.0 - saturate(dot(surface.normal, surface.viewDirection)));
    float reflection = specular * brdf.specular;
    // 使用粗糙度散射这些反射，除以粗糙度的二次方，加 1 是为了防止分母为 0
    reflection /= brdf.roughness * brdf.roughness + 1.0;
    return diffuse * brdf.diffuse + reflection;
}

#endif