using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

[CanEditMultipleObjects]
[CustomEditorForRenderPipeline(typeof(Light), typeof(CustomRenderPipelineAsset))]
public class CustomLightEditor : LightEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        RenderingLayerMaskDrawer.Draw(settings.renderingLayerMask, renderingLayerMaskLabel);
        
        // 检查是否只选择了聚光灯
        if (!settings.lightType.hasMultipleDifferentValues && (LightType)settings.lightType.enumValueIndex == LightType.Spot)
        {
            settings.DrawInnerAndOuterSpotAngle();
            // settings.ApplyModifiedProperties();
        }
        
        settings.ApplyModifiedProperties();
        
        var light = target as Light;
        if (light.cullingMask != -1)
        {
            EditorGUILayout.HelpBox(
                light.type == LightType.Directional ?
                "Culling Mask only affects shadows." :
                "Culling Mask only affects unless Use Light Per Objects is on."
                , MessageType.Warning);
        }
    }

    private static GUIContent renderingLayerMaskLabel = new GUIContent(
        "Rendering Layer Mask",
        "Functional version of above property.");
}
