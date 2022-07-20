/* Shadows.cs */

using UnityEngine;
using UnityEngine.Rendering;

public class Shadows  // 在 Lighting 实例化并持有
{
    private static int _dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");
    private static int _dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices");
    
    private const int MAXShadowedDirectionalLightCount = 4; // 平行投影灯的数量
    private int _shadowedDirectionalLightCount = 0;         // 计数器
    
    private const int MAXCascades = 4;                      // 最大级联数
    
    // 阴影矩阵：将着色点从【世界空间】转换到【光源空间】的变换矩阵
    private static Matrix4x4[] _dirShadowMatrices = new Matrix4x4[MAXShadowedDirectionalLightCount * MAXCascades];
    
    private const string BufferName = "Shadows";

    private CommandBuffer _buffer = new CommandBuffer
    {
        name = BufferName
    };

    private ScriptableRenderContext _context;   // 持有 context，方便在此 ExecuteCommandBuffer

    private CullingResults _cullingResults;

    private ShadowSettings _settings;

    /// <summary>
    /// 平行投影灯结构体
    /// </summary>
    private struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
    }

    // 平行投影灯数组
    private ShadowedDirectionalLight[] _shadowedDirectionalLights =
        new ShadowedDirectionalLight[MAXShadowedDirectionalLightCount];

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings settings)
    {
        _context = context;
        _cullingResults = cullingResults;
        _settings = settings;
        _shadowedDirectionalLightCount = 0;
    }

    /// <summary>
    /// 为灯光的阴影贴图保留空间，储存渲染所需信息
    /// </summary>
    /// <param name="light">灯光</param>
    /// <param name="visibleLightIndex">索引</param>
    /// <returns>x-阴影强度，y-阴影偏移</returns>
    public Vector2 ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        // 如果当前的平行投影灯数量没有到达上限
        // 且光源的阴影模式不为 None
        // 且光源阴影强度不为 0
        // 获取光源的剔除结果，如果在最大阴影距离内没有可被投射的物体，则忽略此光源
        if (_shadowedDirectionalLightCount < MAXShadowedDirectionalLightCount &&
            light.shadows != LightShadows.None &&   
            light.shadowStrength > 0f &&
            _cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
        {
            // 将灯光存入数组，作为平行投影光
            _shadowedDirectionalLights[_shadowedDirectionalLightCount] = 
                new ShadowedDirectionalLight
                {
                    visibleLightIndex = visibleLightIndex
                };
            
            return new Vector2(
                light.shadowStrength, 
                _settings.directional.cascadeCount * _shadowedDirectionalLightCount++);
        }
        return Vector2.zero;
    }
    
    /// <summary>
    /// 渲染阴影
    /// </summary>
    public void Render()
    {
        if (_shadowedDirectionalLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else
        {
            // 在不需要阴影时创建 1×1 虚拟纹理，从而避免额外的着色器变体
            _buffer.GetTemporaryRT(_dirShadowAtlasId, 1, 1,
                32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        }
    }

    /// <summary>
    /// 渲染平行光阴影
    /// </summary>
    private void RenderDirectionalShadows()
    {
        // 创建阴影贴图
        int atlasSize = (int) _settings.directional.atlasSize;
        _buffer.GetTemporaryRT(_dirShadowAtlasId, atlasSize, atlasSize,
            32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        // 将其设置为活动 Render Target，并将储存到 GPU RAM
        _buffer.SetRenderTarget(_dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        // 清理 Render Target
        _buffer.ClearRenderTarget(true, false, Color.clear);
        
        _buffer.BeginSample(BufferName);
        ExecuteBuffer();
        
        // 按级联数和灯光数分割图集
        int tiles = _shadowedDirectionalLightCount * _settings.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;
        
        for (int i = 0; i < _shadowedDirectionalLightCount; i++)
        {
            RenderDirectionalShadows(i, split, tileSize);
        }
        
        // 将阴影矩阵发送到 GPU
        _buffer.SetGlobalMatrixArray(_dirShadowMatricesId, _dirShadowMatrices);
        
        _buffer.EndSample(BufferName);
        ExecuteBuffer();
    }

    /// <summary>
    /// 渲染平行光阴影
    /// </summary>
    /// <param name="index">平行投影灯的索引</param>
    /// <param name="split">分割数</param>
    /// <param name="tileSize">尺寸</param>
    private void RenderDirectionalShadows(int index, int split, int tileSize)
    {
        ShadowedDirectionalLight light = _shadowedDirectionalLights[index];
        
        // 使用剔除结果构造上下文所需的 DrawShadowsSettings
        var shadowSettings = new ShadowDrawingSettings(_cullingResults, light.visibleLightIndex);

        int cascadeCount = _settings.directional.cascadeCount;
        int tileOffset = index * cascadeCount;
        Vector3 ratios = _settings.directional.CascadeRatios;

        for (int i = 0; i < cascadeCount; i++)
        {
            // 获取 viewMatrix、projMatrix、ShadowSplitData
            _cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex, i, cascadeCount, ratios, tileSize, 0f,
                out Matrix4x4 viewMatrix,
                out Matrix4x4 projMatrix,
                out ShadowSplitData splitData);

            shadowSettings.splitData = splitData;

            int tileIndex = tileOffset + i;
            
            // 分割图集，并计算 阴影矩阵 = 投影矩阵 × 视图矩阵
            _dirShadowMatrices[tileIndex] =
                ConvertToAtlasMatrix(projMatrix * viewMatrix, SetTileViewport(tileIndex, split, tileSize), split);

            // 应用视图和投影矩阵
            _buffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
            ExecuteBuffer();
        
            // 为单个光源应用标签名为 ShadowCaster 的着色器 Pass
            _context.DrawShadows(ref shadowSettings);
        }
    }
    
    /// <summary>
    /// 拆分图集
    /// </summary>
    /// <param name="index">索引</param>
    /// <param name="split">分割数</param>
    /// <param name="tileSize">尺寸</param>
    /// <returns></returns>
    private Vector2 SetTileViewport(int index, int split, float tileSize)
    {
        Vector2 offset = new Vector2(index % split, (float)index / split);
        _buffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
        return offset;
    }

    /// <summary>
    /// 按照阴影图集的分割获取阴影矩阵
    /// </summary>
    /// <param name="m">阴影矩阵</param>
    /// <param name="offset">偏移</param>
    /// <param name="split">分割数</param>
    /// <returns>将着色点从【世界空间】转换到【光源空间】的变换矩阵</returns>
    private Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split)
    {
        // 如果 Z-Buffer 是反向的，那么反转 Z
        if (SystemInfo.usesReversedZBuffer)
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }
        
        float scale = 1f / split;
        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);
        
        return m;
    }
    
    /// <summary>
    /// 清理
    /// </summary>
    public void CleanUp()
    {
        _buffer.ReleaseTemporaryRT(_dirShadowAtlasId);
        ExecuteBuffer();
    }
    
    private void ExecuteBuffer()
    {
        _context.ExecuteCommandBuffer(_buffer);
        _buffer.Clear();
    }
}
