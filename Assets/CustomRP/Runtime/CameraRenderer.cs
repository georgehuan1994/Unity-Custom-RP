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

    private static int _frameBufferId = Shader.PropertyToID("_CameraFrameBuffer");

    private bool _useHDR;
    
    public void Render(
        ScriptableRenderContext context, Camera camera, bool allowHDR,
        bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject,
        ShadowSettings shadowSettings, PostFXSettings postFXSettings)
    {
        _context = context;
        _camera = camera;

        PrepareBuffer();
        PrepareForSceneWindow();
        
        if (!Cull(shadowSettings.maxDistance)) 
            return;  // 为什么是先剔除，再配置相机参数？顺序无关，有机会 return 就先 return

        _useHDR = allowHDR && camera.allowHDR;
        
        _commandBuffer.BeginSample(SampleName);
        ExecuteBuffer();
        _lighting.Setup(context, _cullingResults, shadowSettings, useLightsPerObject);  // 灯光设置
        _postFXStack.Setup(context, camera, postFXSettings);    // 后处理设置
        _commandBuffer.EndSample(SampleName);
        
        Setup();    // 相机设置
        
        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing, useLightsPerObject);
        
#if UNITY_EDITOR
        DrawUnSupportShaders();
        DrawGizmoBeforeFX();
#endif
        if (_postFXStack.IsActive)
        {
            _postFXStack.Render(_frameBufferId);
        }
        
#if UNITY_EDITOR
        DrawGizmoAfterFX();
#endif
        
        Cleanup();
        Submit();
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
    /// 设置渲染相关配置
    /// </summary>
    private void Setup()
    {
        // 通过 SetupCameraProperties 方法将相机参数 (render target，view/projection matrices，per-camera built-in shader variables) 传递给 context
        _context.SetupCameraProperties(_camera);

        // 获取相机的 clearFlags
        CameraClearFlags flags = _camera.clearFlags;
        
        if (_postFXStack.IsActive)
        {
            // 除非相机的 clearFlags 为 CameraClearFlags.Skybox = 1，否则清除 颜色缓冲 和 深度缓冲
            if (flags > CameraClearFlags.Color)
            {
                flags = CameraClearFlags.Color;
            }
            
            // 获取 _CameraFrameBuffer 相机的中间帧缓冲 (intermediate frame buffer)
            // 并将其设置为 RenderTarget，为处于激活状态的 Post FX Stack 提供源纹理
            _commandBuffer.GetTemporaryRT(_frameBufferId, _camera.pixelWidth, _camera.pixelHeight, 32,
                FilterMode.Bilinear,
                _useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            _commandBuffer.SetRenderTarget(_frameBufferId, RenderBufferLoadAction.DontCare,
                RenderBufferStoreAction.Store);
        }
        
        // 向 command buffer 写入 清理指令
        // 向 command buffer 写入 采样指令
        _commandBuffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth,
            flags == CameraClearFlags.Color,
            flags == CameraClearFlags.Color ? _camera.backgroundColor.linear : Color.clear); // _cb.ClearRenderTarget(true, true, Color.clear);
        _commandBuffer.BeginSample(SampleName);
        ExecuteBuffer();
    }
    
    /// <summary>
    /// 绘制可见几何体
    /// </summary>
    /// <param name="useDynamicBatching">是否使用动态批处理</param>
    /// <param name="useGPUInstancing">是否使用 GPU 实例化</param>
    private void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject)
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
            perObjectData = PerObjectData.Lightmaps | PerObjectData.ShadowMask | 
                            PerObjectData.LightProbe | PerObjectData.OcclusionProbe | 
                            PerObjectData.LightProbeProxyVolume | PerObjectData.OcclusionProbeProxyVolume |
                            PerObjectData.ReflectionProbes |
                            lightsPerObjectFlags
        };
        
        // Custom Lit Pass
        drawingSettings.SetShaderPassName(1, _litShaderTagId);
        
        // 仅渲染不透明队列
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
        
        // 使用剔除结果作为参数，调用上下文的 DrawRenderers 方法
        _context.DrawRenderers(_cullingResults, ref drawingSettings, ref filteringSettings);
        
        // 绘制天空盒
        _context.DrawSkybox(_camera);
        
        // 渲染排序设置：透明物体排序，与摄像机的距离从远到近
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        
        // 仅渲染透明队列
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        
        _context.DrawRenderers(_cullingResults, ref drawingSettings, ref filteringSettings);
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
        
        if (_postFXStack.IsActive)
        {
            // 释放临时的 Render Texture
            _commandBuffer.ReleaseTemporaryRT(_frameBufferId);
        }
    }
}
