using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline
{
    private CameraRenderer _renderer = new CameraRenderer();
    
    private bool _useDynamicBatching;
    private bool _useGPUInstancing;
    private bool _useLightsPerObject;
    
    private ShadowSettings _shadowSettings;

    protected PostFXSettings _postFXSettings;
    
    /// <summary>
    /// Unity 每帧调用一次此方法来渲染每个可见的场景视图或游戏视图
    /// </summary>
    /// <param name="context">渲染上下文</param>
    /// <param name="cameras">相机列表</param>
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach (Camera camera in cameras) 
        {
            _renderer.Render(context, camera, _useDynamicBatching, _useGPUInstancing, _useLightsPerObject, _shadowSettings, _postFXSettings);
        }
    }
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="useDynamicBatching">是否使用 Dynamic Batching</param>
    /// <param name="useGPUInstancing">是否使用 GPU Instancing</param>
    /// <param name="useSRPBatcher">是否使用静态批处理</param>
    public CustomRenderPipeline(
        bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher, bool userLightsPerObject,
        ShadowSettings shadowSettings, PostFXSettings postFXSettings)
    {
        _useDynamicBatching = useDynamicBatching;
        _useGPUInstancing = useGPUInstancing;
        _shadowSettings = shadowSettings;
        _useLightsPerObject = userLightsPerObject;
        _postFXSettings = postFXSettings;
        
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        GraphicsSettings.lightsUseLinearIntensity = true;
        InitializeForEditor();
    }
}
