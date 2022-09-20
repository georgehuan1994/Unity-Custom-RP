using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

public partial class CameraRenderer // Editor Only
{
    private static ShaderTagId[] legacyShaderTagId =
    {
        new ShaderTagId("Always"),
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM"),
    };
    
    private static Material _errMaterial;
    
    /// <summary>
    /// 绘制不支持的着色器
    /// </summary>
    private void DrawUnSupportShaders()
    {
        if (_errMaterial == null)
        {
            _errMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
        }
        
        var drawingSettings = new DrawingSettings(legacyShaderTagId[0], new SortingSettings(_camera))
        {
            overrideMaterial = _errMaterial,
        };
        for (int i = 1; i < legacyShaderTagId.Length; i++)
        {
            drawingSettings.SetShaderPassName(i, legacyShaderTagId[i]);
        }
        var filteringSettings = FilteringSettings.defaultValue;
        
        _context.DrawRenderers(_cullingResults, ref drawingSettings, ref filteringSettings);
    }

    /// <summary>
    /// 绘制 Gizmo (在 Post-FX Render Texture 之前)
    /// </summary>
    private void DrawGizmoBeforeFX()
    {
        if (Handles.ShouldRenderGizmos())
        {
            if (_useIntermediateBuffer)
            {
                Draw(_depthAttachmentId, BuiltinRenderTextureType.CameraTarget, true);
                ExecuteBuffer();
            }
            _context.DrawGizmos(_camera, GizmoSubset.PreImageEffects);  // 用于指定应在后处理之前渲染的辅助图标
            
        }
    }
    
    /// <summary>
    /// 绘制 Gizmo (在 Post-FX Render Texture 之前)
    /// </summary>
    private void DrawGizmoAfterFX()
    {
        if (Handles.ShouldRenderGizmos())
        {
            _context.DrawGizmos(_camera, GizmoSubset.PostImageEffects); // 用于指定应在后处理之后渲染的辅助图标
        }
    }

    /// <summary>
    /// 在场景视图中绘制 UI
    /// </summary>
    private void PrepareForSceneWindow()
    {
        if (_camera.cameraType == CameraType.SceneView)
        {
            // 将 UI 几何形状发射到 Scene 视图以进行渲染
            ScriptableRenderContext.EmitWorldGeometryForSceneView(_camera);
        }
    }

    /// <summary>
    /// 配置缓冲区
    /// </summary>
    private void PrepareBuffer()
    {
        Profiler.BeginSample("Editor Only");
        _commandBuffer.name = SampleName = _camera.name;
        Profiler.EndSample();
    }
}
