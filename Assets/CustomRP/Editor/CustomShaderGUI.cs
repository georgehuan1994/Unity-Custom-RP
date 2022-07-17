using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class CustomShaderGUI : ShaderGUI
{
    private MaterialEditor _materialEditor;
    
    // 相同材质可以同时编辑
    private Object[] _materials;
    private MaterialProperty[] _properties;

    private bool _showPresets;

    private bool Clipping
    {
        set => SetProperty("_Clipping", "_CLIPPING", value);
    }
    
    private bool PremultiplyAlpha {
        set => SetProperty("_Premultiply_Alpha", "_PREMULTIPLY_ALPHA", value);
    }
    
    private BlendMode SrcBlend {
        set => SetProperty("_SrcBlend", (float)value);
    }

    private BlendMode DstBlend {
        set => SetProperty("_DstBlend", (float)value);
    }

    private bool ZWrite
    {
        set => SetProperty("_ZWrite", value ? 1f : 0f);
    }
    
    private RenderQueue RenderQueue 
    {
        set 
        {
            foreach (Material m in _materials) 
            {
                m.renderQueue = (int)value;
            }
        }
    }
    
    /// <summary>
    /// 是否存在属性
    /// </summary>
    /// <param name="name">属性名</param>
    /// <returns></returns>
    bool HasProperty(string name) => FindProperty(name, _properties, false) != null;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        base.OnGUI(materialEditor, properties);
        
        _materialEditor = materialEditor;
        _materials = materialEditor.targets;
        _properties = properties;
        
        EditorGUILayout.Space();
        
        _showPresets = EditorGUILayout.Foldout(_showPresets, "Presets", true);
        if (_showPresets) 
        {
            OpaquePreset();
            ClipPreset();
            FadePreset();
            TransparentPreset();
        }
    }

    /// <summary>
    /// 启用或禁用关键字
    /// </summary>
    /// <param name="keyword"></param>
    /// <param name="enable"></param>
    private void SetKeyword(string keyword, bool enable)
    {
        if (enable)
        {
            foreach (Material material in _materials)
            {
                material.EnableKeyword(keyword);
            }
        }
        else
        {
            foreach (Material material in _materials)
            {
                material.DisableKeyword(keyword);
            }
        }
    }

    /// <summary>
    /// 设置材质属性 (浮点型)
    /// </summary>
    /// <param name="name">属性名</param>
    /// <param name="value"></param>
    /// <returns></returns>
    private bool SetProperty(string name, float value)
    {
        MaterialProperty materialProperty = FindProperty(name, _properties, false);
        if (materialProperty != null)
        {
            materialProperty.floatValue = value;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 设置属性和关键字的值
    /// </summary>
    /// <param name="name">属性名</param>
    /// <param name="keyword">关键字</param>
    /// <param name="value"></param>
    private void SetProperty(string name, string keyword, bool value)
    {
        if (SetProperty(name, value ? 1f : 0))
        {
            SetKeyword(keyword, value); 
        }
    }
    
    bool PresetButton(string name) 
    {
        if (GUILayout.Button(name)) 
        {
            _materialEditor.RegisterPropertyChangeUndo(name);
            return true;
        }
        return false;
    }
    
    void OpaquePreset() 
    {
        if (PresetButton("Opaque")) 
        {
            Clipping = false;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.Geometry;
        }
    }
    
    void ClipPreset() 
    {
        if (PresetButton("Clip")) 
        {
            Clipping = true;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.AlphaTest;
        }
    }
    
    void FadePreset() 
    {
        if (PresetButton("Fade"))
        {
            Clipping = false;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.SrcAlpha;
            DstBlend = BlendMode.OneMinusSrcAlpha;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
        }
    }
    
    private bool HasPremultiplyAlpha => HasProperty("_Premultiply_Alpha");
    
    void TransparentPreset() 
    {
        if (HasPremultiplyAlpha && PresetButton("Transparent")) 
        {
            Clipping = false;
            PremultiplyAlpha = true;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.OneMinusSrcAlpha;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
        }
    }
}
