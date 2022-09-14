/* Shadows.cs */

using UnityEngine;
using UnityEngine.Rendering;

public class Shadows  // 在 Lighting 实例化并持有
{
    private static int _dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");
    private static int _dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices");

    private static int _otherShadowAtlasId = Shader.PropertyToID("_OtherShadowAtlas");
    private static int _otherShadowMatricesId  = Shader.PropertyToID("_OtherShadowMatrices");
    private static int _otherShadowTilesId  = Shader.PropertyToID("_OtherShadowTiles");

    private static int _cascadeCountId = Shader.PropertyToID("_CascadeCount");
    private static int _cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres");
    private static int _cascadeDataId = Shader.PropertyToID("_CascadeData");
    
    private static int _shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize");
    private static int _shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");
    private static int _shadowPancakingId = Shader.PropertyToID("_ShadowPancaking");

    private static string[] _shadowMaskKeywords =
    {
        "_SHADOW_MASK_ALWAYS",      // 使用 ShadowMask 模式
        "_SHADOW_MASK_DISTANCE"     // 使用 ShadowMask Distance 模式
    };

    private static string[] _directionalFilterKeywords =
    {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7",
    };

    private static string[] _cascadeBlendKeywords =
    {
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER"
    };

    private static string[] _otherFilterKeywords =
    {
        "_OTHER_PCF3",
        "_OTHER_PCF5",
        "_OTHER_PCF7",
    };
    
    private const int MAXShadowedDirectionalLightCount = 4; // 平行投影灯的数量
    private const int MAXShadowedOtherLightCount = 16;      // 非平行投影灯的数量
    
    private int _shadowedDirectionalLightCount = 0;         // 计数器
    private int _shadowedOtherLightCount = 0;
    
    private const int MAXCascades = 4;                      // 最大级联数
    
    // 阴影矩阵：将着色点从【世界空间】转换到【光源空间】的变换矩阵
    private static Matrix4x4[] _dirShadowMatrices = new Matrix4x4[MAXShadowedDirectionalLightCount * MAXCascades];
    private static Matrix4x4[] _otherShadowMatrices = new Matrix4x4[MAXShadowedOtherLightCount];

    // 级联剔除球体，xyz-位置，w-半径的平方 
    private static Vector4[] _cascadeCullingSpheres = new Vector4[MAXCascades];
    
    // 级联数据
    private static Vector4[] _cascadeData = new Vector4[MAXCascades];
    
    // 非平行光 Tile 数据
    private static Vector4[] _otherShadowTiles = new Vector4[MAXShadowedOtherLightCount];
    
    private const string BufferName = "Shadows";

    private CommandBuffer _buffer = new CommandBuffer
    {
        name = BufferName
    };

    private ScriptableRenderContext _context;   // 持有 context，方便在此 ExecuteCommandBuffer

    private CullingResults _cullingResults;

    private ShadowSettings _settings;

    /// <summary>
    /// 平行投影灯参数结构体
    /// </summary>
    private struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
    }
    
    /// <summary>
    /// 非平行投影灯参数结构
    /// </summary>
    private struct ShadowedOtherLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float normalBias;
        public bool isPoint;
    }

    // 平行投影灯参数数组
    private ShadowedDirectionalLight[] _shadowedDirectionalLights =
        new ShadowedDirectionalLight[MAXShadowedDirectionalLightCount];

    // 非平行投影灯参数数组
    private ShadowedOtherLight[] _shadowedOtherLights =
        new ShadowedOtherLight[MAXShadowedOtherLightCount];

    // 是否要应用 ShadowMask
    private bool _useShadowMask;

    // 阴影图集尺寸
    // x: 平行光 size
    // y: 平行光 1/size
    // z: 非平行光 size
    // w: 非平行光 1/size
    private Vector4 _atlasSizes;
    
    
    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings settings)
    {
        _context = context;
        _cullingResults = cullingResults;
        _settings = settings;
        _shadowedDirectionalLightCount = 0;
        _shadowedOtherLightCount = 0;
        _useShadowMask = false;
    }

    /// <summary>
    /// 为灯光的阴影贴图保留空间，储存渲染所需信息
    /// </summary>
    /// <param name="light">灯光</param>
    /// <param name="visibleLightIndex">索引</param>
    /// <returns>x-阴影强度，y-图集偏移，z-法线偏差，w-阴影遮罩通道</returns>
    public Vector4 ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        // 如果当前的平行投影灯数量没有到达上限
        // 且光源的阴影模式不为 None
        // 且光源阴影强度不为 0
        if (_shadowedDirectionalLightCount < MAXShadowedDirectionalLightCount &&
            light.shadows != LightShadows.None &&   
            light.shadowStrength > 0f)
        {
            float maskChannel = -1;
            
            LightBakingOutput lightBaking = light.bakingOutput;
            
            // 如果灯光的烘焙类型为 Mixed，且混合光照模式为 ShadowMask
            if (lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
                lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask)
            {
                _useShadowMask = true;  // 启用 ShadowMask
                maskChannel = lightBaking.occlusionMaskChannel;
            }
            
            // 获取光源的剔除结果，如果在最大阴影距离内没有可被投射的物体，则取负的阴影强度，关闭实时级联阴影和 Bias
            if (!_cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
            {
                // 阴影强度取负值，配合着色过程的判断，不启用实时阴影，但能保留烘焙阴影
                return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
            }
            
            // 将灯光存入数组，作为平行投影光
            _shadowedDirectionalLights[_shadowedDirectionalLightCount] = 
                new ShadowedDirectionalLight
                {
                    visibleLightIndex = visibleLightIndex,
                    slopeScaleBias = light.shadowBias,
                    nearPlaneOffset = light.shadowNearPlane
                };
            
            return new Vector4(
                light.shadowStrength, 
                _settings.directional.cascadeCount * _shadowedDirectionalLightCount++,
                light.shadowNormalBias,
                maskChannel);
        }

        return new Vector4(0, 0, 0, -1);
    }

    /// <summary>
    /// 保留非平行光阴影
    /// </summary>
    /// <param name="light"></param>
    /// <param name="visibleLightIndex"></param>
    /// <returns>x-阴影强度，y-图集偏移，z-法线偏差，w-阴影遮罩通道</returns>
    public Vector4 ReserveOtherShadows(Light light, int visibleLightIndex)
    {
        if (light.shadows == LightShadows.None || light.shadowStrength <= 0f)
        {
            return new Vector4(0f, 0f, 0f, -1f);
        }

        float maskChannel = -1f;

        LightBakingOutput lightBaking = light.bakingOutput;
        if (light)
        {
            if (lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
                lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask)
            {
                _useShadowMask = true;
                maskChannel = lightBaking.occlusionMaskChannel;
            }
        }

        // 点光源需要占用六个 Tile
        bool isPoint = light.type == LightType.Point;
        int newLightCount = _shadowedOtherLightCount + (isPoint ? 6 : 1);
        
        // 检查灯光数量是否超过最大值，是否有可投影的物体
        if (newLightCount >= MAXShadowedOtherLightCount ||
            !_cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
        {
            // 返回负的阴影强度
            return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
        }

        _shadowedOtherLights[_shadowedOtherLightCount] = new ShadowedOtherLight
        {
            visibleLightIndex = visibleLightIndex,
            slopeScaleBias = light.shadowBias,
            normalBias = light.shadowNormalBias,
            isPoint = isPoint
        };
        
        Vector4 data = new Vector4(light.shadowStrength, _shadowedOtherLightCount, isPoint ? 1f :0f, maskChannel);

        _shadowedOtherLightCount = newLightCount;
        
        return data;
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

        if (_shadowedOtherLightCount > 0)
        {
            RenderOtherShadows();
        }
        else
        {
            // 如果没有非平行光，使用平行光的阴影遮罩作为虚拟纹理
            _buffer.SetGlobalTexture(_otherShadowAtlasId, _dirShadowAtlasId);
        }
        
        _buffer.BeginSample(BufferName);
        
        SetKeywords(_shadowMaskKeywords, _useShadowMask ? 
                QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1 : -1);
        
        // 将级联计数发送到 GPU
        _buffer.SetGlobalInt(_cascadeCountId, _shadowedDirectionalLightCount > 0 ? 
            _settings.directional.cascadeCount : 0);
        
        // 将最大阴影距离发送到 GPU
        float f = 1f - _settings.directional.cascadeFade;
        _buffer.SetGlobalVector(_shadowDistanceFadeId,
            new Vector4(
                1f / _settings.maxDistance,
                1f / _settings.distanceFade, 
                1f / (1f - f *f)));
        
        // 将平行光阴影图集尺寸发送到 GPU
        _buffer.SetGlobalVector(_shadowAtlasSizeId, _atlasSizes);
        
        _buffer.EndSample(BufferName);
        
        ExecuteBuffer();
    }

    /// <summary>
    /// 渲染平行光阴影
    /// </summary>
    private void RenderDirectionalShadows()
    {
        // 创建阴影贴图
        int atlasSize = (int) _settings.directional.atlasSize;
        _atlasSizes.x = atlasSize;
        _atlasSizes.y = 1f / atlasSize;
        
        // 创建临时 RenderTarget
        _buffer.GetTemporaryRT(_dirShadowAtlasId, atlasSize, atlasSize,
            32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        
        // 将其设置为活动 RenderTarget，并将储存到 GPU RAM
        _buffer.SetRenderTarget(_dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        
        // 清理 RenderTarget
        _buffer.ClearRenderTarget(true, false, Color.clear);
        
        // 设置 _ShadowPancaking 开闭状态
        _buffer.SetGlobalFloat(_shadowPancakingId, 1f);
        
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
        
        // 将剔除球体信息发送到 GPU
        _buffer.SetGlobalVectorArray(_cascadeCullingSpheresId, _cascadeCullingSpheres);
        
        // 将级联数据发送到 GPU
        _buffer.SetGlobalVectorArray(_cascadeDataId, _cascadeData);
        
        // 将阴影矩阵发送到 GPU
        _buffer.SetGlobalMatrixArray(_dirShadowMatricesId, _dirShadowMatrices);
        
        SetKeywords(_directionalFilterKeywords, (int) _settings.directional.filter - 1);
        SetKeywords(_cascadeBlendKeywords, (int) _settings.directional.cascadeBlend - 1);
        
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
        var shadowSettings = new ShadowDrawingSettings(_cullingResults, light.visibleLightIndex)
        {
            useRenderingLayerMaskTest = true
        };

        int cascadeCount = _settings.directional.cascadeCount;
        int tileOffset = index * cascadeCount;
        Vector3 ratios = _settings.directional.CascadeRatios;
        
        float cullingFactor = Mathf.Max(0f, 0.8f - _settings.directional.cascadeFade);
        
        for (int i = 0; i < cascadeCount; i++)
        {
            // 获取 viewMatrix、projMatrix、ShadowSplitData
            _cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex, i, cascadeCount, ratios, tileSize, light.nearPlaneOffset,
                out Matrix4x4 viewMatrix,
                out Matrix4x4 projMatrix,
                out ShadowSplitData splitData);

            splitData.shadowCascadeBlendCullingFactor = cullingFactor;
            shadowSettings.splitData = splitData;
            
            // 只需要处理第一盏灯，因为灯光的方向不会影响剔除球体
            if (index == 0)
            {
                SetCascadeData(i, splitData.cullingSphere, tileSize);
                
                // 将 w 分量储存为 w^2，这样就不用在着色器中计算了
                Vector4 cullingSphere = splitData.cullingSphere;
                cullingSphere.w *= cullingSphere.w;
                _cascadeCullingSpheres[i] = cullingSphere;
            }

            int tileIndex = tileOffset + i;
            float tileScale = 1f / split;
            
            // 分割图集，并计算 阴影矩阵 = 投影矩阵 × 视图矩阵
            _dirShadowMatrices[tileIndex] =
                ConvertToAtlasMatrix(projMatrix * viewMatrix, SetTileViewport(tileIndex, split, tileSize), tileScale);

            // 应用视图和投影矩阵
            _buffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
            
            // 深度偏差
            _buffer.SetGlobalDepthBias(0, light.slopeScaleBias);
            
            ExecuteBuffer();
        
            // 为单个光源应用标签名为 ShadowCaster 的着色器 Pass
            _context.DrawShadows(ref shadowSettings);
            
            _buffer.SetGlobalDepthBias(0, 0f);
        }
    }
    
    /// <summary>
    /// 渲染非平行光阴影
    /// </summary>
    private void RenderOtherShadows()
    {
        // 创建阴影贴图
        int atlasSize = (int) _settings.other.atlasSize;
        _atlasSizes.z = atlasSize;
        _atlasSizes.w = 1f / atlasSize;
        
        // 创建临时 RenderTarget
        _buffer.GetTemporaryRT(_otherShadowAtlasId, atlasSize, atlasSize,
            32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        
        // 将其设置为活动 RenderTarget，并将储存到 GPU RAM
        _buffer.SetRenderTarget(_otherShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        
        // 清理 RenderTarget
        _buffer.ClearRenderTarget(true, false, Color.clear);
        
        _buffer.BeginSample(BufferName);
        ExecuteBuffer();
        
        // 按级联数和灯光数分割图集
        int tiles = _shadowedOtherLightCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;
        
        for (int i = 0; i < _shadowedOtherLightCount;)
        {
            if (_shadowedOtherLights[i].isPoint)
            {
                RenderPointShadows(i, split, tileSize);
                i += 6;
            }
            else
            {
                RenderSpotShadows(i, split, tileSize);
                i += 1;
            }
        }
        
        // 将阴影矩阵发送到 GPU
        _buffer.SetGlobalMatrixArray(_otherShadowMatricesId, _otherShadowMatrices);
        
        // 将 Tile 数据发送到 GPU
        _buffer.SetGlobalVectorArray(_otherShadowTilesId, _otherShadowTiles);
        
        // 设置 Filter 关键字
        SetKeywords(_otherFilterKeywords, (int) _settings.other.filter - 1);
        
        // 设置 _ShadowPancaking 开闭状态
        _buffer.SetGlobalFloat(_shadowPancakingId, 0f);
        
        _buffer.EndSample(BufferName);
        ExecuteBuffer();
    }
    
    /// <summary>
    /// 渲染聚光灯实时阴影
    /// </summary>
    /// <param name="index"></param>
    /// <param name="split"></param>
    /// <param name="tileSize"></param>
    private void RenderSpotShadows(int index, int split, int tileSize)
    {
        ShadowedOtherLight light = _shadowedOtherLights[index];
        var shadowSettings = new ShadowDrawingSettings(_cullingResults, light.visibleLightIndex)
        {
            useRenderingLayerMaskTest = true
        };
        
        _cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(light.visibleLightIndex, 
            out Matrix4x4 viewMatrix,
            out Matrix4x4 projMatrix,
            out ShadowSplitData splitData);
        
        shadowSettings.splitData = splitData;

        // Normal Bias
        float texelSize = 2f / (tileSize * projMatrix.m00);
        float filterSize = texelSize * ((float)_settings.other.filter + 1f);
        float bias = light.normalBias * filterSize * 1.4142136f;

        Vector2 offset = SetTileViewport(index, split, tileSize);
        float tileScale = 1f / split;
        SetOtherTileData(index, offset, tileScale, bias);
        
        _otherShadowMatrices[index] =
            ConvertToAtlasMatrix(projMatrix * viewMatrix, offset, tileScale);
        
        _buffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
        _buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
        
        ExecuteBuffer();
        
        _context.DrawShadows(ref shadowSettings);
        _buffer.SetGlobalDepthBias(0f, 0f);
    }
    
    private void RenderPointShadows(int index, int split, int tileSize)
    {
        ShadowedOtherLight light = _shadowedOtherLights[index];
        var shadowSettings = new ShadowDrawingSettings(_cullingResults, light.visibleLightIndex)
        {
            useRenderingLayerMaskTest = true
        };

        // Normal Bias
        float texelSize = 2f / tileSize;
        float filterSize = texelSize * ((float)_settings.other.filter + 1f);
        float bias = light.normalBias * filterSize * 1.4142136f;
        float tileScale = 1f / split;

        float fovBias = Mathf.Atan(1f + bias + filterSize) * Mathf.Rad2Deg * 2f - 90f;
        
        for (int i = 0; i < 6; i++)
        {
            _cullingResults.ComputePointShadowMatricesAndCullingPrimitives(light.visibleLightIndex, 
                (CubemapFace)i, 
                fovBias,
                out Matrix4x4 viewMatrix,
                out Matrix4x4 projMatrix, 
                out ShadowSplitData splitData);
            
            // viewMatrix.m11 = -viewMatrix.m11;
            // viewMatrix.m12 = -viewMatrix.m12;
            // viewMatrix.m13 = -viewMatrix.m13;
            
            shadowSettings.splitData = splitData;

            int tileIndex = index + i;
            Vector2 offset = SetTileViewport(tileIndex, split, tileSize);
            SetOtherTileData(tileIndex, offset, tileScale, bias);
        
            _otherShadowMatrices[tileIndex] =
                ConvertToAtlasMatrix(projMatrix * viewMatrix, offset, tileScale);
        
            _buffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
            _buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
        
            ExecuteBuffer();
        
            _context.DrawShadows(ref shadowSettings);
            _buffer.SetGlobalDepthBias(0f, 0f);
        }
    }

    /// <summary>
    /// 设置级联数据
    /// </summary>
    /// <param name="index"></param>
    /// <param name="cullingSphere"></param>
    /// <param name="tileSize"></param>
    private void SetCascadeData(int index, Vector4 cullingSphere, float tileSize)
    {
        float texelSize = 2f * cullingSphere.w / tileSize;
        float filterSize = texelSize * ((float) _settings.directional.filter + 1f);
        cullingSphere.w -= filterSize;
        cullingSphere.w *= cullingSphere.w;
        _cascadeCullingSpheres[index] = cullingSphere;
        // texel 是正方形的，也就是说最差的情况就相当于沿正方形的对角线偏移，才能使之分离
        _cascadeData[index] = new Vector4(1f / cullingSphere.w, filterSize * 1.4142136f);

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
        Vector2 offset = new Vector2(index % split, index / split);
        _buffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
        return offset;
    }

    
    private void SetOtherTileData(int index, Vector2 offset, float scale, float bias)
    {
        float border = _atlasSizes.w * 0.5f;
        
        Vector4 data;
        data.x = offset.x * scale + border;
        data.y = offset.y * scale + border;
        data.z = scale - border - border;
        data.w = bias;
        
        _otherShadowTiles[index] = data;
    }

    private void SetKeywords(string[] keywords, int enabledIndex)
    {
        for (int i = 0; i < keywords.Length; i++)
        {
            if (i == enabledIndex)
            {
                _buffer.EnableShaderKeyword(keywords[i]);
            }
            else
            {
                _buffer.DisableShaderKeyword(keywords[i]);
            }
        }
    }

    /// <summary>
    /// 按照阴影图集的分割获取阴影矩阵
    /// </summary>
    /// <param name="m">阴影矩阵</param>
    /// <param name="offset">偏移</param>
    /// <param name="scale">分割数</param>
    /// <returns>将着色点从【世界空间】转换到【光源空间】的变换矩阵</returns>
    private Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, float scale)
    {
        // 如果 Z-Buffer 是反向的，那么反转 Z
        if (SystemInfo.usesReversedZBuffer)
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }
        
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
        
        if (_shadowedOtherLightCount > 0)
        {
            _buffer.ReleaseTemporaryRT(_otherShadowAtlasId);
        }
        
        ExecuteBuffer();
    }
    
    private void ExecuteBuffer()
    {
        _context.ExecuteCommandBuffer(_buffer);
        _buffer.Clear();
    }
}
