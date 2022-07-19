/* Lighting.cs */

using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Lighting
{
    private const int MAXDirectionLightCount = 4;
    
    private const string BufferName = "Lighting";

    private CommandBuffer _buffer = new CommandBuffer
    {
        name = BufferName,
    };

    private static int _dirLightCountId = Shader.PropertyToID("_DirectionalLightCount");
    private static int _dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors");
    private static int _dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections");
    private static int _dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");

    private static Vector4[] _dirLightColors = new Vector4[MAXDirectionLightCount];
    private static Vector4[] _dirLightDirections = new Vector4[MAXDirectionLightCount];
    private static Vector4[] _dirLightShadowData = new Vector4[MAXDirectionLightCount];

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
    /// 设置光源信息
    /// </summary>
    private void SetupLights()
    {
        // 通过剔除结果，检索所需的数据
        NativeArray<VisibleLight> visibleLights = _cullingResults.visibleLights;
        int dirLightCount = 0;
        for (int i = 0; i < visibleLights.Length; i++)
        {
            VisibleLight visibleLight = visibleLights[i];
            // 检查灯光类型是否为平行光
            if (visibleLight.lightType == LightType.Directional)
            {
                SetupDirectionalLight(dirLightCount++, ref visibleLight);
                if (dirLightCount >= MAXDirectionLightCount)
                {
                    break;
                }
            }
        }
        
        _buffer.SetGlobalInt(_dirLightCountId, visibleLights.Length);
        _buffer.SetGlobalVectorArray(_dirLightColorsId, _dirLightColors);
        _buffer.SetGlobalVectorArray(_dirLightDirectionsId, _dirLightDirections);
        _buffer.SetGlobalVectorArray(_dirLightShadowDataId, _dirLightShadowData);
    }
    
    /// <summary>
    /// 设置平行光 (使用剔除结果)
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
    /// 设置平行光 (使用场景主光)
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
    /// 清理
    /// </summary>
    public void Cleanup()
    {
        _shadows.CleanUp();
    }
}
