/* Lighting.cs */

using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Lighting
{
    private const int MAXDirectionLightCount = 4;
    private const int MAXOhterLightCount = 64;
    
    private const string BufferName = "Lighting";

    private CommandBuffer _buffer = new CommandBuffer
    {
        name = BufferName,
    };

    private static int _dirLightCountId = Shader.PropertyToID("_DirectionalLightCount");
    private static int _dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors");
    private static int _dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections");
    private static int _dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");

    private static int _otherLightCountId = Shader.PropertyToID("_OtherLightCount");
    private static int _otherLightColorsId = Shader.PropertyToID("_OtherLightColors");
    private static int _otherLightPositionsId = Shader.PropertyToID("_OtherLightPositions");
    private static int _otherLightDirectionsId = Shader.PropertyToID("_OtherLightDirections");  // 聚光灯方向
    private static int _otherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles");  // 聚光灯外角
    private static int _otherLightShadowDataId = Shader.PropertyToID("_OtherLightShadowData");
    
    private static Vector4[] _dirLightColors = new Vector4[MAXDirectionLightCount];
    private static Vector4[] _dirLightDirections = new Vector4[MAXDirectionLightCount];
    private static Vector4[] _dirLightShadowData = new Vector4[MAXDirectionLightCount];

    private static Vector4[] _otherLightColors = new Vector4[MAXOhterLightCount];
    private static Vector4[] _otherLightPositions = new Vector4[MAXOhterLightCount];
    private static Vector4[] _otherLightDirections = new Vector4[MAXOhterLightCount];   // 聚光灯方向
    private static Vector4[] _otherLightSpotAngles = new Vector4[MAXOhterLightCount];   // 聚光灯外角
    private static Vector4[] _otherLightShadowData = new Vector4[MAXOhterLightCount];
    
    private CullingResults _cullingResults;

    private Shadows _shadows = new Shadows();
    
    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings)
    {
        _cullingResults = cullingResults;
        _buffer.BeginSample(BufferName);
        _shadows.Setup(context, cullingResults, shadowSettings);    // 在 SetupLight 前，先 SetupShadow
        SetupLights();
        _shadows.Render();
        _buffer.EndSample(BufferName);
        
        context.ExecuteCommandBuffer(_buffer);
        _buffer.Clear();
    }

    /// <summary>
    /// 设置光源信息，将光源信息发送个 GPU，Light.hlsl -> CBUFFER _CustomLight
    /// </summary>
    private void SetupLights()
    {
        // 通过剔除结果，检索所需的数据
        NativeArray<VisibleLight> visibleLights = _cullingResults.visibleLights;
        
        int dirLightCount = 0;
        int otherLightCount = 0;
        
        for (int i = 0; i < visibleLights.Length; i++)
        {
            VisibleLight visibleLight = visibleLights[i];

            // 检查灯光类型
            switch (visibleLight.lightType)
            {
                case LightType.Spot:
                    if (otherLightCount < MAXOhterLightCount)
                    {
                        SetupSpotLight(otherLightCount++, ref visibleLight);
                    }
                    break;
                case LightType.Directional:
                    if (dirLightCount < MAXDirectionLightCount)
                    {
                        SetupDirectionalLight(dirLightCount++, ref visibleLight);
                    }
                    break;
                case LightType.Point:
                    if (otherLightCount < MAXOhterLightCount)
                    {
                        SetupPointLight(otherLightCount++, ref visibleLight);
                    }
                    break;
                case LightType.Area:
                    break;
                case LightType.Disc:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        _buffer.SetGlobalInt(_dirLightCountId, dirLightCount);
        if (dirLightCount > 0)
        {
            _buffer.SetGlobalVectorArray(_dirLightColorsId, _dirLightColors);
            _buffer.SetGlobalVectorArray(_dirLightDirectionsId, _dirLightDirections);
            _buffer.SetGlobalVectorArray(_dirLightShadowDataId, _dirLightShadowData);
        }
        
        _buffer.SetGlobalInt(_otherLightCountId, otherLightCount);
        if (otherLightCount > 0)
        {
            _buffer.SetGlobalVectorArray(_otherLightColorsId, _otherLightColors);
            _buffer.SetGlobalVectorArray(_otherLightPositionsId, _otherLightPositions);
            _buffer.SetGlobalVectorArray(_otherLightDirectionsId, _otherLightDirections);
            _buffer.SetGlobalVectorArray(_otherLightSpotAnglesId, _otherLightSpotAngles);
            _buffer.SetGlobalVectorArray(_otherLightShadowDataId, _otherLightShadowData);
        }
    }
    
    /// <summary>
    /// 设置平行光 (使用剔除结果) 信息
    /// </summary>
    /// <param name="index">索引</param>
    /// <param name="visibleLight">可见灯光</param>
    private void SetupDirectionalLight(int index, ref VisibleLight visibleLight)
    {
        _dirLightColors[index] = visibleLight.finalColor;
        _dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        _dirLightShadowData[index] = _shadows.ReserveDirectionalShadows(visibleLight.light, index);
    }
    
    /// <summary>
    /// 设置平行光 (使用场景主光) 信息
    /// </summary>
    private void SetupDirectionalLight()
    {
        // 获取场景主光 Window > Rendering > Lighting > Sun Source
        Light light = RenderSettings.sun;
        
        // 用 CommandBuffer.SetGlobalVector 将灯光数据发送到 GPU
        _buffer.SetGlobalVector(_dirLightColorsId, light.color.linear * light.intensity);
        _buffer.SetGlobalVector(_dirLightDirectionsId, -light.transform.forward);
    }

    /// <summary>
    /// 设置点光源信息
    /// </summary>
    /// <param name="index"></param>
    /// <param name="visibleLight"></param>
    private void SetupPointLight(int index, ref VisibleLight visibleLight)
    {
        _otherLightColors[index] = visibleLight.finalColor;
        
        // 在 position.w 中储存半径平方反比
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        _otherLightPositions[index] = position;
        _otherLightSpotAngles[index] = new Vector4(0f, 1f);

        Light light = visibleLight.light;
        _otherLightShadowData[index] = _shadows.ReserveOtherShadows(light, index);
    }

    /// <summary>
    /// 设置聚光灯信息
    /// </summary>
    /// <param name="index"></param>
    /// <param name="visibleLight"></param>
    private void SetupSpotLight(int index, ref VisibleLight visibleLight)
    {
        _otherLightColors[index] = visibleLight.finalColor;

        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        _otherLightPositions[index] = position;
        _otherLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);

        Light light = visibleLight.light;
        float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
        float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
        float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
        _otherLightSpotAngles[index] = new Vector4(angleRangeInv, -outerCos * angleRangeInv);
        
        _otherLightShadowData[index] = _shadows.ReserveOtherShadows(light, index);
    }

    /// <summary>
    /// 清理
    /// </summary>
    public void Cleanup()
    {
        _shadows.CleanUp();
    }
}
