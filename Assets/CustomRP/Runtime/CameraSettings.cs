using System;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable]
public class CameraSettings
{
    public bool copyColor = true;
    public bool copyDepth = true;

    [Serializable]
    public struct FinalBlendMode
    {
        public BlendMode source, destination;
    }
    
    [RenderingLayerMaskField]
    public int renderingLayerMask = -1;

    public bool maskLights = false;
    
    public enum RenderScaleMode
    {
        Inherit, Multiply, Override
    }

    public RenderScaleMode renderScaleMode = RenderScaleMode.Inherit;

    [Range(0.1f, 2f)]
    public float renderScale = 1f;

    public bool overridePostFX = false;

    public PostFXSettings postFXSettings = default;

    public FinalBlendMode finalBlendMode = new FinalBlendMode
    {
        source = BlendMode.One,
        destination = BlendMode.Zero
    };

    public float GetRenderScale(float scale)
    {
        return
            renderScaleMode == RenderScaleMode.Inherit ? scale :
            renderScaleMode == RenderScaleMode.Override ? renderScale :
            scale * renderScale;
    }
}