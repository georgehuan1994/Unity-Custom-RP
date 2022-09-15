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
        // DrawRenderingLayerMask();
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

    private void DrawRenderingLayerMask()
    {
        SerializedProperty property = settings.renderingLayerMask;
        
        // 多选时显示 Mixed..
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        
        EditorGUI.BeginChangeCheck();
        int mask = property.intValue;
        
        // 如果序列化属性的 int 值为 int.MaxVlaue，即选择了所有的 mask
        if (mask == int.MaxValue)
        {
            mask = -1;
        }
        
        // 专门用于绘制 Mask 字段的 API，下拉列表中第一个 Item 为 Nothing；第二个 Item 为 Everything
        // 选择 Nothing(0)：mask = int.MinValue
        // 选择 Everything(-1)：mask = int.MaxValue
        mask = EditorGUILayout.MaskField(
            renderingLayerMaskLabel, mask,
            GraphicsSettings.currentRenderPipeline.renderingLayerMaskNames);
        
        if (EditorGUI.EndChangeCheck())
        {
            property.intValue = mask == -1 ? int.MaxValue : mask;
        }

        EditorGUI.showMixedValue = false;
    }
}
