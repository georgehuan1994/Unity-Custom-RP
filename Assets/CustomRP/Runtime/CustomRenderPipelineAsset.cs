using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public class CustomRenderPipelineAsset : RenderPipelineAsset
{
    [SerializeField] private bool allowHDR = true;
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

    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(
            allowHDR,useDynamicBatching, useGPUInstancing, useSRPBatcher, useLightsPerObject, 
            shadowSettings, postFXSettings, (int)colorLutResolution);
    }
}
