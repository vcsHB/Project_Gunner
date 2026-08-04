using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(LayerAttribute))]
public class LayerAttributeDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.Integer)
        {
            EditorGUI.LabelField(position, label.text, "[Layer]는 int에만 쓸 수 있습니다.");
            return;
        }

        EditorGUI.BeginProperty(position, label, property);

        EditorGUI.BeginChangeCheck();
        int layer = EditorGUI.LayerField(position, label, property.intValue);
        if (EditorGUI.EndChangeCheck())
            property.intValue = layer;

        EditorGUI.EndProperty();
    }
}
