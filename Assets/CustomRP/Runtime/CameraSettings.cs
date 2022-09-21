using System;
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

    public bool overridePostFX = false;

    public PostFXSettings postFXSettings = default;

    public FinalBlendMode finalBlendMode = new FinalBlendMode
    {
        source = BlendMode.One,
        destination = BlendMode.Zero
    };
}