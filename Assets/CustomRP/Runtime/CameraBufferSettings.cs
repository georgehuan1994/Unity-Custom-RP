using UnityEngine;

[System.Serializable]
public struct CameraBufferSettings
{
    public bool allowHDR;
    public bool copyColor;
    public bool copyColorReflection;
    public bool copyDepth;
    public bool copyDepthReflection;
    
    [Range(0.1f, 2f)] public float renderScale;
    public enum BicubicRescalingMode { Off, UpOnly, UpAndDown }
    public BicubicRescalingMode bicubicRescalingMode;
}
