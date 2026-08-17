using UnityEditor;
using UnityEngine;

/// <summary>파생 클래스(PlayerWeaponDataSO 등)에도 함께 적용된다.</summary>
[CustomEditor(typeof(ItemDataSO), true)]
public class ItemDataSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        ItemDataSO item = (ItemDataSO)target;

        if (item.IsRegistered)
            EditorGUILayout.LabelField("Id", item.Id.ToString(), EditorStyles.boldLabel);
        else
            EditorGUILayout.HelpBox("아직 Id가 없습니다. ItemDatabase에서 스캔을 실행하세요.", MessageType.Warning);

        EditorGUILayout.Space();

        // _id는 의도적으로 숨긴다. 손으로 고치면 세이브 호환이 깨진다.
        DrawPropertiesExcluding(serializedObject, "m_Script", "_id");
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        DrawLocalization(item);
    }

    /// <summary>
    /// 이름과 설명이 표에 들어가 있는지 여기서 바로 보여준다.
    /// 표 창을 따로 열어 키를 검색해야 하면 등록을 빠뜨린다.
    /// </summary>
    private static void DrawLocalization(ItemDataSO item)
    {
        EditorGUILayout.LabelField("로컬라이즈", EditorStyles.boldLabel);

        if (string.IsNullOrEmpty(item.LocalizationKey))
        {
            EditorGUILayout.HelpBox(
                "키가 없습니다. Tools > Item Creator에서 '로컬라이즈 키 일괄 채우기'를 실행하세요.",
                MessageType.Warning);
            return;
        }

        LocalizationTableSO table = LocalizationEditorUtil.FindTable();
        if (table == null)
        {
            EditorGUILayout.HelpBox(
                "LocalizationTable 에셋이 없습니다. SO/Localization/LocalizationTable로 만들어 Resources에 두세요.",
                MessageType.Warning);
            return;
        }

        DrawKeyRow(table, "이름", item.NameKey);
        DrawKeyRow(table, "설명", item.DescriptionKey);

        if (GUILayout.Button("이름·설명을 표에 등록"))
            Register(table, item);
    }

    private static void DrawKeyRow(LocalizationTableSO table, string label, string key)
    {
        EditorGUILayout.LabelField(label, key, EditorStyles.miniLabel);

        using (new EditorGUI.DisabledScope(true))
        using (new EditorGUI.IndentLevelScope())
        {
            foreach (LanguageType language in System.Enum.GetValues(typeof(LanguageType)))
            {
                string value = LocalizationEditorUtil.Peek(table, key, language);
                EditorGUILayout.TextField(language.ToString(), value ?? "(표에 없음)");
            }
        }
    }

    private static void Register(LocalizationTableSO table, ItemDataSO item)
    {
        // 표를 거친 DisplayName이 아니라 원문을 넣는다.
        // 영어로 보고 있는 중이면 영어가 한국어 칸에 들어간다.
        LocalizationEditorUtil.Write(table, item.NameKey, LanguageType.Korean, item.FallbackName);

        if (!string.IsNullOrEmpty(item.RawDescription))
            LocalizationEditorUtil.Write(table, item.DescriptionKey, LanguageType.Korean, item.RawDescription);

        AssetDatabase.SaveAssets();
        Debug.Log($"[Localization] {item.name}의 이름·설명을 표에 등록했습니다.", table);
    }
}
