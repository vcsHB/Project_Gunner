using UnityEditor;
using UnityEngine;

/// <summary>
/// 기본 ImageEditor는 Image Type이 Filled일 때만 fillAmount를 보여준다.
/// 이 컴포넌트는 type을 쓰지 않으므로 필요한 것만 직접 그린다.
/// </summary>
[CustomEditor(typeof(SlicedFilledImage))]
[CanEditMultipleObjects]
public class SlicedFilledImageEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawIfExists("m_Sprite", "Sprite");
        DrawIfExists("m_Color", "Color");
        DrawIfExists("m_Material", "Material");

        EditorGUILayout.Space();

        DrawIfExists("m_RaycastTarget", "Raycast Target");
        DrawIfExists("m_RaycastPadding", "Raycast Padding");
        DrawIfExists("m_Maskable", "Maskable");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Fill", EditorStyles.boldLabel);

        DrawIfExists("_fillDirection", "Direction");
        DrawIfExists("m_FillAmount", "Fill Amount");
        DrawIfExists("m_FillCenter", "Fill Center");
        DrawIfExists("m_PixelsPerUnitMultiplier", "Pixels Per Unit Multiplier");

        DrawBorderWarning();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawIfExists(string propertyName, string label)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null) return;

        EditorGUILayout.PropertyField(property, new GUIContent(label));
    }

    private void DrawBorderWarning()
    {
        SlicedFilledImage image = target as SlicedFilledImage;
        if (image == null || image.sprite == null) return;

        if (image.sprite.border.sqrMagnitude > 0f) return;

        EditorGUILayout.HelpBox(
            "스프라이트에 9슬라이스 테두리가 없습니다. 일반 Image와 결과가 같습니다.\n" +
            "스프라이트 에디터에서 Border를 지정하세요.", MessageType.Info);
    }
}
