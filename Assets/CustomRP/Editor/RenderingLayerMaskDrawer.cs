using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[CustomPropertyDrawer(typeof(RenderingLayerMaskFieldAttribute))]
public class RenderingLayerMaskDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        Draw(position, property, label);
    }

    public static void Draw(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        
        int mask = property.intValue;
        bool isUint = property.type == "uint";
        if (isUint && mask == int.MaxValue)
        {
            mask = -1;
        }

        // 用于绘制 Mask 字段的 API，下拉列表中第一个 Item 为 Nothing；第二个 Item 为 Everything
        // 选择 Nothing(0)：mask = int.MinValue
        // 选择 Everything(-1)：mask = int.MaxValue
        mask = EditorGUI.MaskField(position, label, mask, GraphicsSettings.currentRenderPipeline.renderingLayerMaskNames);

        if (EditorGUI.EndChangeCheck())
        {
            property.intValue = isUint && mask == -1 ? int.MaxValue : mask;
        }

        EditorGUI.showMixedValue = false;
    }

    public static void Draw(SerializedProperty property, GUIContent label)
    {
        Draw(EditorGUILayout.GetControlRect(), property, label);
    }
}
