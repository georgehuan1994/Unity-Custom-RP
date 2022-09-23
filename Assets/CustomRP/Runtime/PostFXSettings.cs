using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
public class PostFXSettings : ScriptableObject
{
    [SerializeField] private Shader shader = default;

    [System.NonSerialized] private Material _material = null;

    // --------------------------------------------

    [System.Serializable]
    public struct BloomSettings
    {
        public bool ignoreRenderScale;
        [Range(0f, 16f)] public int maxIterations;
        [Min(1f)] public int downscaleLimit;
        public bool halfRes;
        public bool bicubicUpsampling;
        [Min(0f)] public float threshold;
        [Range(0f, 1f)] public float thresholdKnee;
        [Min(0f)] public float intensity;
        public bool fadeFireflies;

        public enum Mode
        {
            Additive,
            Scattering
        }

        public Mode mode;

        [Range(0.05f, 0.95f)] public float scatter;
    }

    [SerializeField] private BloomSettings bloom = new BloomSettings
    {
        scatter = 0.7f,
    };

    public BloomSettings Bloom => bloom;

    // --------------------------------------------

    [Serializable]
    public struct ColorAdjustmentsSettings
    {
        public float postExposure;

        [Range(-100f, 100f)] public float contrast;
        [ColorUsage(false, true)] public Color colorFilter;
        [Range(-180f, 180f)] public float hueShift;
        [Range(-100f, 100f)] public float saturation;
    }

    [SerializeField] private ColorAdjustmentsSettings colorAdjustmentsSettings = new ColorAdjustmentsSettings
    {
        colorFilter = Color.white,
    };

    public ColorAdjustmentsSettings ColorAdjustments => colorAdjustmentsSettings;
    
    // --------------------------------------------

    [Serializable]
    public struct WhiteBalanceSettings
    {
        [Range(-100f, 100f)] public float temperature;
        [Range(-100f, 100f)] public float tint;
    }

    [SerializeField] private WhiteBalanceSettings whiteBalance = default;

    public WhiteBalanceSettings WhiteBalance => whiteBalance;

    // --------------------------------------------
    
    [Serializable]
    public struct SplitToningSettings
    {
        [ColorUsage(false)] public Color shadows;
        [ColorUsage(false)] public Color highlights;
        [Range(-100f, 100f)] public float balance;
    }

    [SerializeField] private SplitToningSettings splitToning = new SplitToningSettings
    {
        shadows = Color.gray,
        highlights = Color.gray
    };

    public SplitToningSettings SplitToning => splitToning;
    
    // --------------------------------------------
    
    [Serializable] public struct ChannelMixerSettings
    {
        public Vector3 red, green, blue;
    }

    [SerializeField] private ChannelMixerSettings channelMixer = new ChannelMixerSettings
    {
        red = Vector3.right,
        green = Vector3.up,
        blue = Vector3.forward
    };

    public ChannelMixerSettings ChannelMixer => channelMixer;
    
    // --------------------------------------------
    
    [Serializable] public struct ShadowsMidtonesHigtlightsSettings
    {
        [ColorUsage(false, true)] public Color shadows, midtones, highlights;
        [Range(0f, 2f)] public float shadowsStart, shadowsEnd, highlightStart, highLightsEnd;
    }

    [SerializeField] public ShadowsMidtonesHigtlightsSettings shadowsMidtonesHigtlights =
        new ShadowsMidtonesHigtlightsSettings
        {
            shadows = Color.white,
            midtones = Color.white,
            highlights = Color.white,
            shadowsEnd = 0.3f,
            highlightStart = 0.55f,
            highLightsEnd = 1f
        };

    public ShadowsMidtonesHigtlightsSettings ShadowsMidtonesHigtlights => shadowsMidtonesHigtlights;
    
    // --------------------------------------------

    [SerializeField] private ToneMappingSettings toneMapping = default;

    [System.Serializable]
    public struct ToneMappingSettings
    {
        public enum Mode
        {
            None,
            ACES,
            Neutral,
            Reinhard
        }

        public Mode mode;
    }

    public ToneMappingSettings ToneMapping => toneMapping;

    public Material Material
    {
        get
        {
            if (_material == null && shader != null)
            {
                _material = new Material(shader);
                _material.hideFlags = HideFlags.HideAndDontSave;
            }

            return _material;
        }
    }
}