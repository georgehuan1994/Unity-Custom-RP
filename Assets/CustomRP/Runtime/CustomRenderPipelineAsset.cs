using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public partial class CustomRenderPipelineAsset : RenderPipelineAsset
{
    [SerializeField] private CameraBufferSettings cameraBuffer = new CameraBufferSettings
    {
        allowHDR = true,
        renderScale = 1f,
        fxaa = new CameraBufferSettings.FXAA
        {
            fixedThreshold = 0.0833f,
            relativeThreshold = 0.166f,
            subpixelBlending = 0.75f
        }
    };
    
    [SerializeField] private bool useDynamicBatching = true;
    [SerializeField] private bool useGPUInstancing = true;
    [SerializeField] private bool useSRPBatcher = true;
    [SerializeField] private bool useLightsPerObject = true;
    
    [SerializeField] private ShadowSettings shadowSettings = default;
    
    [SerializeField] private PostFXSettings postFXSettings = default;
    
    public enum ColorLUTResolution
    {
        _16 = 16, _32 = 32, _64 = 64
    }

    [SerializeField] private ColorLUTResolution colorLutResolution = ColorLUTResolution._32;

    [SerializeField] private Shader cameraRendererShader = default;
    
    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(
            cameraBuffer, useDynamicBatching, useGPUInstancing, useSRPBatcher, useLightsPerObject, 
            shadowSettings, postFXSettings, (int)colorLutResolution,
            cameraRendererShader);
    }
}
