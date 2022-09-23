using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    private ScriptableRenderContext _context;

    private Camera _camera;
    
    private const string BufferName = "Render Camera";

    private CommandBuffer _commandBuffer = new CommandBuffer
    {
        name = BufferName
    };
    
#if UNITY_EDITOR
    private string SampleName { get; set; }
#else
    private const string SampleName = BufferName;
#endif

    private CullingResults _cullingResults;

    private static ShaderTagId _unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
    private static ShaderTagId _litShaderTagId = new ShaderTagId("CustomLit");

    private Lighting _lighting = new Lighting();

    protected PostFXStack _postFXStack = new PostFXStack();
    
    private static int _bufferSizeId = Shader.PropertyToID("_CameraBufferSize");
    
    // private static int _frameBufferId = Shader.PropertyToID("_CameraFrameBuffer");
    private static int _colorAttachmentId = Shader.PropertyToID("_CameraColorAttachment");
    private static int _depthAttachmentId = Shader.PropertyToID("_CameraDepthAttachment");
    private static int _colorTextureId = Shader.PropertyToID("_CameraColorTexture");
    private static int _depthTextureId = Shader.PropertyToID("_CameraDepthTexture");
    private static int _sourceTextureId = Shader.PropertyToID("_SourceTexture");

    private static int _srcBlendId = Shader.PropertyToID("_CameraSrcBlend");
    private static int _dstBlendId = Shader.PropertyToID("_CameraDstBlend");
    
    private bool _useHDR;
    private bool _useScaleRendering;        // 是否使用缓冲区缩放
    private bool _useColorTexture;          // 是否使用单独的颜色纹理
    private bool _useDepthTexture;          // 是否使用单独的深度纹理
    private bool _useIntermediateBuffer;    // 是否使用中间帧缓冲

    private Vector2Int _bufferSize;         // 缓冲纹理分辨率

    private int _colorLUTResolution;

    private static CameraSettings _defaultCameraSettings = new CameraSettings();
    
    private Material _material;
    private Texture2D _missingTexture;

    private static bool _copyTextureSupported = SystemInfo.copyTextureSupport > CopyTextureSupport.None;

    /// <summary>
    /// CameraRenderer 构造函数
    /// </summary>
    /// <param name="shader"></param>
    public CameraRenderer(Shader shader)
    {
        _material = CoreUtils.CreateEngineMaterial(shader);
        _missingTexture = new Texture2D(1, 1)
        {
            hideFlags = HideFlags.HideAndDontSave,
            name = "Missing"
        };
        _missingTexture.SetPixel(0, 0, Color.white * 0.5f);
        _missingTexture.Apply(true, true);
    }

    public void Dispose()
    {
        CoreUtils.Destroy(_material);
        CoreUtils.Destroy(_missingTexture);
    }
    
        public void Render(
        ScriptableRenderContext context, Camera camera, CameraBufferSettings cameraBufferSettings,
        bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject,
        ShadowSettings shadowSettings, PostFXSettings postFXSettings, int colorLutResolution)
    {
        _context = context;
        _camera = camera;

        var crpCamera = _camera.GetComponent<CustomRenderPipelineCamera>();
        CameraSettings cameraSettings = crpCamera ? crpCamera.Settings : _defaultCameraSettings;

        // _useDepthTexture = true;

        if (camera.cameraType == CameraType.Reflection)
        {
            _useColorTexture = cameraBufferSettings.copyColor;
            _useDepthTexture = cameraBufferSettings.copyDepthReflection;
        }
        else
        {
            _useColorTexture = cameraBufferSettings.copyColor && cameraSettings.copyColor;
            _useDepthTexture = cameraBufferSettings.copyDepth && cameraSettings.copyDepth;
        }

        if (cameraSettings.overridePostFX)
        {
            postFXSettings = cameraSettings.postFXSettings;
        }

        float renderScale = cameraBufferSettings.renderScale;
        _useScaleRendering = renderScale < 0.99f || renderScale > 1.01f;
        
        PrepareBuffer();
        PrepareForSceneWindow();
        
        if (!Cull(shadowSettings.maxDistance)) 
            return;  // 为什么是先剔除，再配置相机参数？顺序无关，有机会 return 就先 return

        _useHDR = cameraBufferSettings.allowHDR && camera.allowHDR;
        if (_useScaleRendering)
        {
            _bufferSize.x = (int)(camera.pixelWidth * renderScale);
            _bufferSize.y = (int)(camera.pixelHeight * renderScale);
        }
        else
        {
            _bufferSize.x = camera.pixelWidth;
            _bufferSize.y = camera.pixelHeight;
        }
        
        _commandBuffer.BeginSample(SampleName);
        _commandBuffer.SetGlobalVector(_bufferSizeId, new Vector4(
            1f / _bufferSize.x, 1f / _bufferSize.y, _bufferSize.x, _bufferSize.y));
        ExecuteBuffer();
        _lighting.Setup(context, _cullingResults, shadowSettings, useLightsPerObject,
            cameraSettings.maskLights ? cameraSettings.renderingLayerMask : -1);  // 灯光设置
        _postFXStack.Setup(context, camera, _bufferSize, postFXSettings, _useHDR, colorLutResolution, cameraSettings.finalBlendMode);    // 后处理设置
        _commandBuffer.EndSample(SampleName);
        
        Setup();    // 相机设置
        
        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing, useLightsPerObject, cameraSettings.renderingLayerMask);
        
#if UNITY_EDITOR
        DrawUnSupportShaders();
        DrawGizmoBeforeFX();
#endif
        if (_postFXStack.IsActive)
        {
            // _postFXStack.Render(_frameBufferId);
            _postFXStack.Render(_colorAttachmentId);
        }
        else if(_useIntermediateBuffer)
        {
            // Draw(_colorAttachmentId, BuiltinRenderTextureType.CameraTarget);
            DrawFinal(cameraSettings.finalBlendMode);
            ExecuteBuffer();
        }
        
#if UNITY_EDITOR
        DrawGizmoAfterFX();
#endif
        
        Cleanup();
        Submit();
    }
        
    
    /// <summary>
    /// 设置渲染相关配置
    /// </summary>
    private void Setup()
    {
        // 通过 SetupCameraProperties 方法将相机参数 (render target，view/projection matrices，per-camera built-in shader variables) 传递给 context
        _context.SetupCameraProperties(_camera);

        // 获取相机的 clearFlags
        CameraClearFlags flags = _camera.clearFlags;
        
        // 使用中间帧缓冲
        _useIntermediateBuffer = _useScaleRendering || _useColorTexture || _useDepthTexture || _postFXStack.IsActive;
        
        if (_useIntermediateBuffer)
        {
            // 除非相机的 clearFlags 为 CameraClearFlags.Skybox = 1，否则清除 颜色缓冲 和 深度缓冲
            if (flags > CameraClearFlags.Color)
            {
                flags = CameraClearFlags.Color;
            }

            // 获取颜色缓冲
            _commandBuffer.GetTemporaryRT(
                _colorAttachmentId, _bufferSize.x, _bufferSize.y, 0,
                FilterMode.Bilinear,
                _useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            
            // 获取深度缓冲
            _commandBuffer.GetTemporaryRT(
                _depthAttachmentId, _bufferSize.x, _bufferSize.y, 32,
                FilterMode.Point,
                RenderTextureFormat.Depth);
            
            // 将颜色缓冲和深度缓冲合并，并将其设置为 RenderTarget，为处于激活状态的 Post FX Stack 提供源纹理
            _commandBuffer.SetRenderTarget(
                _colorAttachmentId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                _depthAttachmentId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        }
        
        // 向 command buffer 写入 清理指令
        // 向 command buffer 写入 采样指令
        _commandBuffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth,
            flags == CameraClearFlags.Color,
            flags == CameraClearFlags.Color ? _camera.backgroundColor.linear : Color.clear); // _cb.ClearRenderTarget(true, true, Color.clear);
        _commandBuffer.BeginSample(SampleName);
        _commandBuffer.SetGlobalTexture(_depthTextureId, _missingTexture);
        ExecuteBuffer();
    }

    /// <summary>
    /// 剔除
    /// </summary>
    /// <returns>剔除是否成功</returns>
    private bool Cull(float maxShadowDistance)
    {
        if (_camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            // 设置最大阴影距离
            // 比相机看到的更远的阴影是没有意义的，所以取最大阴影距离和相机远剪辑平面中的最小值
            p.shadowDistance = Mathf.Min(maxShadowDistance, _camera.farClipPlane);
            _cullingResults = _context.Cull(ref p);
            return true;
        }

        return false;
    }
    
    /// <summary>
    /// 绘制可见几何体
    /// </summary>
    /// <param name="useDynamicBatching">是否使用动态批处理</param>
    /// <param name="useGPUInstancing">是否使用 GPU 实例化</param>
    /// <param name="useLightsPerObject"></param>
    /// <param name="renderingLayerMask"></param>
    private void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject,
        int renderingLayerMask)
    {
        PerObjectData lightsPerObjectFlags = useLightsPerObject ? 
                PerObjectData.LightData | PerObjectData.LightIndices : 
                PerObjectData.None;
        
        // 渲染排序设置：不透明物体排序，与摄像机的距离从近到远
        var sortingSettings = new SortingSettings(_camera) {criteria = SortingCriteria.CommonOpaque};
        var drawingSettings = new DrawingSettings(_unlitShaderTagId, sortingSettings)
        {
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useGPUInstancing,
            perObjectData = PerObjectData.ReflectionProbes |
                            PerObjectData.Lightmaps | PerObjectData.ShadowMask | 
                            PerObjectData.LightProbe | PerObjectData.OcclusionProbe | 
                            PerObjectData.LightProbeProxyVolume | PerObjectData.OcclusionProbeProxyVolume |
                            lightsPerObjectFlags
        };
        
        // Custom Lit Pass
        drawingSettings.SetShaderPassName(1, _litShaderTagId);
        
        // 仅渲染不透明队列
        var filteringSettings =
            new FilteringSettings(RenderQueueRange.opaque, renderingLayerMask: (uint)renderingLayerMask);
        
        // 使用剔除结果作为参数，调用上下文的 DrawRenderers 方法
        _context.DrawRenderers(_cullingResults, ref drawingSettings, ref filteringSettings);
        
        // 绘制天空盒
        _context.DrawSkybox(_camera);

        if (_useColorTexture || _useDepthTexture)
        {
            // 复制 Attachment 作为临时纹理
            CopyAttachments();
        }
        
        // 渲染排序设置：透明物体排序，与摄像机的距离从远到近
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        
        // 仅渲染透明队列
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        
        _context.DrawRenderers(_cullingResults, ref drawingSettings, ref filteringSettings);
    }

    /// <summary>
    /// 复制 Attachments 为临时纹理
    /// </summary>
    private void CopyAttachments()
    {
        // 如果启用了颜色纹理复制
        if (_useColorTexture)
        {
            _commandBuffer.GetTemporaryRT(_colorTextureId, _bufferSize.x, _bufferSize.y, 0,
                FilterMode.Bilinear, _useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);

            if (_copyTextureSupported)
            {
                _commandBuffer.CopyTexture(_colorAttachmentId, _colorTextureId);
            }
            else
            {
                Draw(_colorAttachmentId, _colorTextureId);
            }
        }
        
        // 如果启用了深度纹理复制
        if (_useDepthTexture)
        {
            _commandBuffer.GetTemporaryRT(_depthTextureId, _bufferSize.x, _bufferSize.y, 32,
                FilterMode.Point, RenderTextureFormat.Depth);
            
            if (_copyTextureSupported)  // 图形 API 是否支持复制功能
            {
                _commandBuffer.CopyTexture(_depthAttachmentId, _depthTextureId);
            }
            else // 不支持的话就通过着色器在相机的帧缓冲区中绘制
            {
                Draw(_depthAttachmentId, _depthTextureId, true);
            }
        }

        if (!_copyTextureSupported)
        {
            _commandBuffer.SetRenderTarget(
                _colorAttachmentId, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                _depthAttachmentId, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
        }
        
        ExecuteBuffer();
    }
    
    private void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, bool isDepth = false)
    {
        // 从标识符为 from 的 RenderTarget 中获取纹理，复制到标识符为 _SourceTexture 的纹理
        _commandBuffer.SetGlobalTexture(_sourceTextureId, from);
        // 将标识符为 to 的 RenderTarget 作为绘制目标
        _commandBuffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        // 使用相机材质在 RenderTarget 上绘制一个很大的三角形
        _commandBuffer.DrawProcedural(Matrix4x4.identity, _material,  isDepth ? 1 : 0, MeshTopology.Triangles, 3);
    }
    
    private void DrawFinal(CameraSettings.FinalBlendMode finalBlendMode)
    {
        _commandBuffer.SetGlobalFloat(_srcBlendId, (float)finalBlendMode.source);
        _commandBuffer.SetGlobalFloat(_dstBlendId, (float)finalBlendMode.destination);
        
        // _CameraColorAttachment -> _SourceTexture
        _commandBuffer.SetGlobalTexture(_sourceTextureId, _colorAttachmentId);
        _commandBuffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, 
            finalBlendMode.destination == BlendMode.Zero ? RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load,
            RenderBufferStoreAction.Store);
        _commandBuffer.SetViewport(_camera.pixelRect);
        _commandBuffer.DrawProcedural(Matrix4x4.identity, _material,  0, MeshTopology.Triangles, 3);
        
        _commandBuffer.SetGlobalFloat(_srcBlendId, 1f);
        _commandBuffer.SetGlobalFloat(_dstBlendId, 0f);
    }

    /// <summary>
    /// 向图形 API 提交上下文，执行预定的命令
    /// </summary>
    private void Submit()
    {
        _commandBuffer.EndSample(SampleName);  // 向 command buffer 写入 profiler 结束采样指令
        ExecuteBuffer();
        _context.Submit();
    }

    /// <summary>
    /// 注册命令到上下文并清理缓冲区
    /// </summary>
    private void ExecuteBuffer()
    {
        _context.ExecuteCommandBuffer(_commandBuffer);
        _commandBuffer.Clear();
    }

    protected void Cleanup()
    {
        _lighting.Cleanup();    // 提交之前释放阴影贴图
        
        if (_useIntermediateBuffer)
        {
            // 释放临时的 Render Texture
            // _commandBuffer.ReleaseTemporaryRT(_frameBufferId);
            _commandBuffer.ReleaseTemporaryRT(_colorAttachmentId);
            _commandBuffer.ReleaseTemporaryRT(_depthAttachmentId);

            if (_useColorTexture)
            {
                _commandBuffer.ReleaseTemporaryRT(_colorAttachmentId);
            }
            
            if (_useDepthTexture)
            {
                _commandBuffer.ReleaseTemporaryRT(_depthTextureId);
            }
        }
    }
}
