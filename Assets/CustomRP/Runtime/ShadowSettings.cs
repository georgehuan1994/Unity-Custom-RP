using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ShadowSettings
{
    /// <summary>
    /// 贴图尺寸
    /// </summary>
    public enum TextureSize
    {
        _256 = 256, _512 = 512, _1024 = 1024, _2048 = 2048, _4096 = 4096, _8192 = 8192
    }
    
    [Min(0f)] public float maxDistance = 100f;  // 最大距离
    
    /// <summary>
    /// 平行光阴影结构
    /// </summary>
    [System.Serializable]
    public struct Directional
    {
        public TextureSize atlasSize;
    }

    public Directional directional = new Directional {atlasSize = TextureSize._1024};
    
    
}
