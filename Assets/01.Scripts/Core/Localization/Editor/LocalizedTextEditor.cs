using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// LocalizedText 인스펙터. 키를 만들고 표에 등록하는 것을 여기서 다 끝낸다.
/// 표 에셋을 따로 열어 손으로 행을 추가하게 두면 반드시 빠뜨린다.
/// </summary>
[CustomEditor(typeof(LocalizedText))]
[CanEditMultipleObjects]
public class LocalizedTextEditor : Editor
{
    /// <summary>
    /// TMP 컴포넌트의 ⋮ 메뉴에서 바로 붙인다.
    /// TMP 인스펙터를 통째로 덮어쓰면 TMP가 업데이트될 때마다 깨지므로 이 방식을 쓴다.
    /// </summary>
    [MenuItem("CONTEXT/TextMeshProUGUI/Localize")]
    private static void AddLocalizedText(MenuCommand command)
    {
        TextMeshProUGUI text = (TextMeshProUGUI)command.context;
        if (text == null) return;

        if (text.TryGetComponent(out LocalizedText existing))
        {
            EditorGUIUtility.PingObject(existing);
            return;
        }

        LocalizedText localized = Undo.AddComponent<LocalizedText>(text.gameObject);

        // Reset이 원문을 잡아가고, 키는 여기서 계층 경로로 채운다.
        localized.SetKey(LocalizationEditorUtil.BuildKeyFromHierarchy(text.transform));
        EditorUtility.SetDirty(localized);
    }

    [MenuItem("CONTEXT/TextMeshProUGUI/Localize", true)]
    private static bool AddLocalizedTextValidate(MenuCommand command)
        => command.context is TextMeshProUGUI text && !text.TryGetComponent<LocalizedText>(out _);

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        LocalizationTableSO table = LocalizationEditorUtil.FindTable();
        if (table == null)
        {
            EditorGUILayout.HelpBox(
                "LocalizationTable 에셋이 없습니다. SO/Localization/LocalizationTable로 만들어 " +
                "Resources 폴더에 두세요.", MessageType.Warning);
            return;
        }

        DrawTableState(table);
        DrawActions(table);
    }

    private void DrawTableState(LocalizationTableSO table)
    {
        if (targets.Length != 1) return;

        LocalizedText localized = (LocalizedText)target;

        if (string.IsNullOrEmpty(localized.Key))
        {
            EditorGUILayout.HelpBox("키가 없습니다. 원문이 그대로 표시됩니다.", MessageType.Info);
            return;
        }

        using (new EditorGUI.DisabledScope(true))
        {
            foreach (LanguageType language in System.Enum.GetValues(typeof(LanguageType)))
            {
                string value = LocalizationEditorUtil.Peek(table, localized.Key, language);
                EditorGUILayout.TextField(language.ToString(), value ?? "(표에 없음)");
            }
        }
    }

    private void DrawActions(LocalizationTableSO table)
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("경로에서 키 만들기"))
            RebuildKeys();

        if (GUILayout.Button("원문을 표에 등록"))
            RegisterAll(table);

        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("표 에셋 열기"))
        {
            Selection.activeObject = table;
            EditorGUIUtility.PingObject(table);
        }
    }

    private void RebuildKeys()
    {
        foreach (Object each in targets)
        {
            if (each is not LocalizedText localized) continue;

            Undo.RecordObject(localized, "Rebuild Localization Key");
            localized.SetKey(LocalizationEditorUtil.BuildKeyFromHierarchy(localized.transform));
            EditorUtility.SetDirty(localized);
        }
    }

    private void RegisterAll(LocalizationTableSO table)
    {
        int written = 0;

        foreach (Object each in targets)
        {
            if (each is not LocalizedText localized) continue;

            // 원문이 비어 있으면 행을 만들지 않는다. 번역할 게 없는 빈 행은 시트를 어지럽힐 뿐이다.
            if (string.IsNullOrEmpty(localized.SourceText)) continue;

            if (LocalizationEditorUtil.Write(table, localized.Key, LanguageType.Korean, localized.SourceText))
                written++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Localization] {written}개를 표에 등록했습니다.", table);
    }
}
