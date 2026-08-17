using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 로컬라이즈 키를 만들고 표에 써 넣는 에디터 공용 기능.
/// 인스펙터 버튼과 Tools 메뉴가 같은 길을 쓰도록 여기 모은다.
/// </summary>
public static class LocalizationEditorUtil
{
    public const string UiPrefix = "ui";

    private const string EntriesField = "_entries";
    private const string KeyField = "key";
    private const string ValuesField = "values";

    private static readonly int LanguageCount = Enum.GetValues(typeof(LanguageType)).Length;

    /// <summary>
    /// 오브젝트 계층에서 키를 만든다. Canvas까지 거슬러 올라가 경로를 이어붙인다.
    ///
    /// <b>버튼을 누른 그 순간에만 계산한다.</b> 매번 경로에서 계산하면
    /// 오브젝트를 옮기거나 이름을 바꾸는 순간 시트 행이 조용히 고아가 된다.
    /// </summary>
    public static string BuildKeyFromHierarchy(Transform target)
    {
        if (target == null) return string.Empty;

        List<string> segments = new();

        for (Transform current = target; current != null; current = current.parent)
        {
            string segment = Sanitize(current.name);
            if (!string.IsNullOrEmpty(segment))
                segments.Add(segment);

            // 캔버스가 화면의 경계다. 그 위쪽 경로는 어느 문구인지 구분하는 데 도움이 안 된다.
            if (current != target && current.GetComponent<Canvas>() != null) break;
        }

        segments.Reverse();

        return segments.Count == 0 ? UiPrefix : $"{UiPrefix}.{string.Join(".", segments)}";
    }

    /// <summary>키에 쓸 수 있는 문자만 남기고 소문자로. 아이템 키 규칙과 같은 기준이다.</summary>
    public static string Sanitize(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        StringBuilder builder = new(raw.Length);
        foreach (char c in raw)
        {
            if (char.IsLetterOrDigit(c))
                builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    public static LocalizationTableSO FindTable()
    {
        string[] guids = AssetDatabase.FindAssets($"t:{nameof(LocalizationTableSO)}");
        if (guids.Length == 0) return null;

        return AssetDatabase.LoadAssetAtPath<LocalizationTableSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    /// <summary>표에 적힌 값. 없으면 null.</summary>
    public static string Peek(LocalizationTableSO table, string key, LanguageType language)
        => table != null && table.TryGet(key, language, out string value) ? value : null;

    /// <summary>
    /// 키를 표에 등록한다. 행이 없으면 만들고, 있으면 그 언어 칸만 덮어쓴다.
    /// 다른 언어 칸은 건드리지 않는다 — 번역해 둔 것이 날아가면 안 된다.
    /// </summary>
    public static bool Write(LocalizationTableSO table, string key, LanguageType language, string value)
    {
        if (table == null || string.IsNullOrEmpty(key)) return false;

        SerializedObject serialized = new(table);
        SerializedProperty entries = serialized.FindProperty(EntriesField);

        SerializedProperty element = FindOrCreateEntry(entries, key);
        SerializedProperty values = element.FindPropertyRelative(ValuesField);

        // 언어를 뒤에 추가하면 기존 행의 배열이 짧다. 읽기 전에 늘려둔다.
        if (values.arraySize < LanguageCount)
            values.arraySize = LanguageCount;

        values.GetArrayElementAtIndex((int)language).stringValue = value;

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(table);
        return true;
    }

    private static SerializedProperty FindOrCreateEntry(SerializedProperty entries, string key)
    {
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty element = entries.GetArrayElementAtIndex(i);
            if (element.FindPropertyRelative(KeyField).stringValue == key)
                return element;
        }

        entries.arraySize++;

        SerializedProperty created = entries.GetArrayElementAtIndex(entries.arraySize - 1);
        created.FindPropertyRelative(KeyField).stringValue = key;

        SerializedProperty values = created.FindPropertyRelative(ValuesField);
        values.arraySize = LanguageCount;

        // 복제된 마지막 항목의 값이 남아 있을 수 있다. 새 행은 비워서 시작한다.
        for (int i = 0; i < values.arraySize; i++)
            values.GetArrayElementAtIndex(i).stringValue = string.Empty;

        return created;
    }
}
