using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 키 × 언어를 표로 놓고 편집하는 창.
///
/// 기본 인스펙터로는 키를 찾을 수도, 어느 칸이 비었는지 볼 수도 없습니다.
/// 번역 작업은 "빠진 칸 찾기"가 대부분이라 그게 안 보이면 도구가 아닙니다.
///
/// 구글 시트와는 TSV로 오갑니다. 시트에서 범위를 복사해 붙여넣거나,
/// 여기서 복사해 시트에 붙이면 그대로 들어갑니다.
/// </summary>
public class LocalizationTableWindow : EditorWindow
{
    private const string EntriesField = "_entries";
    private const string KeyField = "key";
    private const string ValuesField = "values";

    private const float RowHeight = 20f;
    private const float DeleteWidth = 22f;
    private const float KeyWidthRatio = 0.34f;

    private static readonly LanguageType[] Languages =
        (LanguageType[])Enum.GetValues(typeof(LanguageType));

    private LocalizationTableSO _table;
    private SerializedObject _serialized;

    private Vector2 _scroll;
    private string _search = string.Empty;
    private bool _onlyMissing;

    private readonly List<int> _visible = new();

    [MenuItem("Tools/Localization/표 편집기")]
    public static void Open()
    {
        LocalizationTableWindow window = GetWindow<LocalizationTableWindow>("Localization");
        window.minSize = new Vector2(640f, 320f);
    }

    private void OnEnable() => AcquireTable();

    private void AcquireTable()
    {
        if (_table == null)
            _table = LocalizationEditorUtil.FindTable();

        _serialized = _table != null ? new SerializedObject(_table) : null;
    }

    private void OnGUI()
    {
        DrawTablePicker();

        if (_serialized == null)
        {
            EditorGUILayout.HelpBox(
                "LocalizationTable 에셋이 없습니다. SO/Localization/LocalizationTable로 만들어 " +
                "Resources 폴더에 두세요.", MessageType.Warning);
            return;
        }

        _serialized.Update();

        SerializedProperty entries = _serialized.FindProperty(EntriesField);

        DrawToolbar(entries);
        DrawFilter();

        BuildVisible(entries);

        DrawHeader();
        DrawRows(entries);

        DrawFooter(entries);

        _serialized.ApplyModifiedProperties();
    }

    #region Header

    private void DrawTablePicker()
    {
        EditorGUI.BeginChangeCheck();
        _table = (LocalizationTableSO)EditorGUILayout.ObjectField(
            "표", _table, typeof(LocalizationTableSO), false);

        if (EditorGUI.EndChangeCheck())
            _serialized = _table != null ? new SerializedObject(_table) : null;
    }

    private void DrawToolbar(SerializedProperty entries)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("키 순 정렬", EditorStyles.toolbarButton))
            SortByKey(entries);

        if (GUILayout.Button("TSV 복사", EditorStyles.toolbarButton))
            CopyTsv(entries);

        if (GUILayout.Button("TSV 붙여넣기", EditorStyles.toolbarButton))
            PasteTsv(entries);

        GUILayout.FlexibleSpace();
        GUILayout.Label($"{entries.arraySize}행", EditorStyles.miniLabel);

        EditorGUILayout.EndHorizontal();
    }

    private void DrawFilter()
    {
        EditorGUILayout.BeginHorizontal();

        _search = EditorGUILayout.TextField("검색", _search);
        _onlyMissing = GUILayout.Toggle(_onlyMissing, "빠진 칸만", EditorStyles.miniButton, GUILayout.Width(80f));

        EditorGUILayout.EndHorizontal();
    }

    private void DrawHeader()
    {
        Rect rect = GUILayoutUtility.GetRect(0f, RowHeight, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.15f));

        float keyWidth = rect.width * KeyWidthRatio;
        float valueWidth = (rect.width - keyWidth - DeleteWidth) / Languages.Length;

        EditorGUI.LabelField(new Rect(rect.x, rect.y, keyWidth, rect.height), "Key", EditorStyles.miniBoldLabel);

        for (int i = 0; i < Languages.Length; i++)
        {
            Rect cell = new(rect.x + keyWidth + valueWidth * i, rect.y, valueWidth, rect.height);
            EditorGUI.LabelField(cell, Languages[i].ToString(), EditorStyles.miniBoldLabel);
        }
    }

    #endregion

    #region Rows

    private void BuildVisible(SerializedProperty entries)
    {
        _visible.Clear();

        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty element = entries.GetArrayElementAtIndex(i);
            string key = element.FindPropertyRelative(KeyField).stringValue;

            if (!string.IsNullOrEmpty(_search)
                && key.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            if (_onlyMissing && !HasMissingValue(element)) continue;

            _visible.Add(i);
        }
    }

    private static bool HasMissingValue(SerializedProperty element)
    {
        SerializedProperty values = element.FindPropertyRelative(ValuesField);

        for (int i = 0; i < Languages.Length; i++)
        {
            if (i >= values.arraySize) return true;
            if (string.IsNullOrEmpty(values.GetArrayElementAtIndex(i).stringValue)) return true;
        }

        return false;
    }

    /// <summary>
    /// 보이는 줄만 그린다. 키가 수백 개가 되면 전부 그리는 순간 창이 버벅인다.
    /// </summary>
    private void DrawRows(SerializedProperty entries)
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        Rect area = GUILayoutUtility.GetRect(0f, RowHeight * _visible.Count, GUILayout.ExpandWidth(true));

        int first = Mathf.Max(0, Mathf.FloorToInt(_scroll.y / RowHeight) - 1);
        int last = Mathf.Min(_visible.Count, first + Mathf.CeilToInt(position.height / RowHeight) + 2);

        int removeIndex = -1;

        for (int i = first; i < last; i++)
        {
            Rect row = new(area.x, area.y + i * RowHeight, area.width, RowHeight);

            if (DrawRow(row, entries.GetArrayElementAtIndex(_visible[i])))
                removeIndex = _visible[i];
        }

        EditorGUILayout.EndScrollView();

        if (removeIndex >= 0)
            entries.DeleteArrayElementAtIndex(removeIndex);
    }

    /// <summary>지우기를 눌렀으면 true.</summary>
    private static bool DrawRow(Rect rect, SerializedProperty element)
    {
        SerializedProperty key = element.FindPropertyRelative(KeyField);
        SerializedProperty values = element.FindPropertyRelative(ValuesField);

        // 언어를 뒤에 추가하면 옛 행의 배열이 짧다. 그리기 전에 늘려둔다.
        if (values.arraySize < Languages.Length)
            values.arraySize = Languages.Length;

        float keyWidth = rect.width * KeyWidthRatio;
        float valueWidth = (rect.width - keyWidth - DeleteWidth) / Languages.Length;

        key.stringValue = EditorGUI.TextField(
            new Rect(rect.x, rect.y + 1f, keyWidth - 2f, rect.height - 2f), key.stringValue);

        for (int i = 0; i < Languages.Length; i++)
        {
            Rect cell = new(rect.x + keyWidth + valueWidth * i, rect.y + 1f, valueWidth - 2f, rect.height - 2f);
            SerializedProperty value = values.GetArrayElementAtIndex(i);

            // 빈 칸은 눈에 띄어야 한다. 번역 작업은 이걸 찾는 일이 대부분이다.
            if (string.IsNullOrEmpty(value.stringValue))
                EditorGUI.DrawRect(cell, new Color(1f, 0.4f, 0.3f, 0.15f));

            value.stringValue = EditorGUI.TextField(cell, value.stringValue);
        }

        Rect deleteRect = new(rect.xMax - DeleteWidth, rect.y + 1f, DeleteWidth - 2f, rect.height - 2f);
        return GUI.Button(deleteRect, "X", EditorStyles.miniButton);
    }

    private void DrawFooter(SerializedProperty entries)
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("행 추가", GUILayout.Width(80f)))
        {
            entries.arraySize++;

            SerializedProperty created = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            created.FindPropertyRelative(KeyField).stringValue = string.Empty;

            SerializedProperty values = created.FindPropertyRelative(ValuesField);
            values.arraySize = Languages.Length;

            for (int i = 0; i < values.arraySize; i++)
                values.GetArrayElementAtIndex(i).stringValue = string.Empty;
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label($"보이는 행 {_visible.Count}", EditorStyles.miniLabel);

        EditorGUILayout.EndHorizontal();
    }

    #endregion

    #region TSV

    private void SortByKey(SerializedProperty entries)
    {
        List<(string key, string[] values)> rows = ReadAll(entries);
        rows.Sort((a, b) => string.CompareOrdinal(a.key, b.key));

        WriteAll(entries, rows);
    }

    /// <summary>
    /// 시트에 그대로 붙여넣을 수 있는 형태로 복사한다.
    /// 값 안의 줄바꿈은 \n으로 바꾼다 — TSV는 줄바꿈이 곧 행 구분이라 그대로 두면 표가 깨진다.
    /// </summary>
    private void CopyTsv(SerializedProperty entries)
    {
        StringBuilder builder = new();

        builder.Append("key");
        foreach (LanguageType language in Languages)
            builder.Append('\t').Append(language);
        builder.AppendLine();

        foreach ((string key, string[] values) in ReadAll(entries))
        {
            builder.Append(key);
            for (int i = 0; i < Languages.Length; i++)
                builder.Append('\t').Append(Escape(values[i]));

            builder.AppendLine();
        }

        EditorGUIUtility.systemCopyBuffer = builder.ToString();
        Debug.Log($"[Localization] {entries.arraySize}행을 TSV로 복사했습니다.");
    }

    /// <summary>
    /// 클립보드의 TSV를 합친다. 있는 키는 갱신하고 없는 키는 추가한다.
    /// <b>지우지는 않는다</b> — 시트에서 일부만 복사해 온 경우 나머지가 통째로 날아간다.
    /// </summary>
    private void PasteTsv(SerializedProperty entries)
    {
        string clipboard = EditorGUIUtility.systemCopyBuffer;
        if (string.IsNullOrEmpty(clipboard))
        {
            Debug.LogWarning("[Localization] 클립보드가 비어 있습니다.");
            return;
        }

        List<(string key, string[] values)> rows = ReadAll(entries);
        Dictionary<string, int> byKey = new(rows.Count);
        for (int i = 0; i < rows.Count; i++)
            byKey[rows[i].key] = i;

        int added = 0;
        int updated = 0;

        foreach (string line in clipboard.Split('\n'))
        {
            string trimmed = line.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            string[] columns = trimmed.Split('\t');
            string key = columns[0].Trim();

            // 헤더 줄은 건너뛴다.
            if (string.IsNullOrEmpty(key) || key.Equals("key", StringComparison.OrdinalIgnoreCase)) continue;

            string[] values = new string[Languages.Length];
            for (int i = 0; i < Languages.Length; i++)
                values[i] = i + 1 < columns.Length ? Unescape(columns[i + 1]) : string.Empty;

            if (byKey.TryGetValue(key, out int index))
            {
                rows[index] = (key, values);
                updated++;
            }
            else
            {
                byKey[key] = rows.Count;
                rows.Add((key, values));
                added++;
            }
        }

        if (added == 0 && updated == 0)
        {
            Debug.LogWarning("[Localization] 붙여넣을 행을 찾지 못했습니다. 탭으로 구분된 표인지 확인하세요.");
            return;
        }

        WriteAll(entries, rows);
        Debug.Log($"[Localization] TSV 병합 — 추가 {added}행, 갱신 {updated}행.");
    }

    private static string Escape(string value)
        => string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\t", " ").Replace("\r", string.Empty).Replace("\n", "\\n");

    private static string Unescape(string value)
        => string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\n", "\n");

    private static List<(string key, string[] values)> ReadAll(SerializedProperty entries)
    {
        List<(string, string[])> rows = new(entries.arraySize);

        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty element = entries.GetArrayElementAtIndex(i);
            SerializedProperty values = element.FindPropertyRelative(ValuesField);

            string[] copied = new string[Languages.Length];
            for (int lang = 0; lang < Languages.Length; lang++)
            {
                copied[lang] = lang < values.arraySize
                    ? values.GetArrayElementAtIndex(lang).stringValue
                    : string.Empty;
            }

            rows.Add((element.FindPropertyRelative(KeyField).stringValue, copied));
        }

        return rows;
    }

    private static void WriteAll(SerializedProperty entries, List<(string key, string[] values)> rows)
    {
        entries.arraySize = rows.Count;

        for (int i = 0; i < rows.Count; i++)
        {
            SerializedProperty element = entries.GetArrayElementAtIndex(i);
            element.FindPropertyRelative(KeyField).stringValue = rows[i].key;

            SerializedProperty values = element.FindPropertyRelative(ValuesField);
            values.arraySize = Languages.Length;

            for (int lang = 0; lang < Languages.Length; lang++)
                values.GetArrayElementAtIndex(lang).stringValue = rows[i].values[lang];
        }
    }

    #endregion
}
