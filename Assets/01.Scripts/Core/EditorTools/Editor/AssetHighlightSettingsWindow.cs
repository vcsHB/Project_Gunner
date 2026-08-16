using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 색 팔레트를 늘리고 줄이는 창. 지금 칠해둔 에셋 목록도 여기서 본다.
///
/// 팔레트는 편집용 복사본을 들고 있다가 바뀔 때만 저장소로 넘깁니다.
/// 저장소를 직접 건드리면 리스트를 그리는 도중에 항목 수가 달라집니다.
/// </summary>
public class AssetHighlightSettingsWindow : EditorWindow
{
    private const float SwatchWidth = 60f;
    private const float ButtonWidth = 22f;

    /// <summary>그리는 도중에는 목록을 건드리지 않고 여기에 적어둔다.</summary>
    private enum PendingKind
    {
        None,
        Remove,
        MoveUp,
        MoveDown,
        ClearAsset,
    }

    private readonly List<AssetHighlightStore.PaletteEntry> _palette = new();
    private readonly List<KeyValuePair<string, Color>> _assignments = new();

    private Vector2 _scroll;
    private PendingKind _pendingKind;
    private int _pendingIndex;

    [MenuItem("Tools/Asset Highlight Settings")]
    public static void Open()
    {
        AssetHighlightSettingsWindow window = GetWindow<AssetHighlightSettingsWindow>("Asset Highlight");
        window.minSize = new Vector2(360f, 320f);
    }

    private void OnEnable()
    {
        Reload();
        AssetHighlightStore.OnChangedEvent += Reload;
    }

    private void OnDisable()
    {
        AssetHighlightStore.OnChangedEvent -= Reload;
    }

    private void Reload()
    {
        _palette.Clear();

        foreach (AssetHighlightStore.PaletteEntry entry in AssetHighlightStore.Palette)
            _palette.Add(new AssetHighlightStore.PaletteEntry(entry.Name, entry.Color));

        AssetHighlightStore.CollectAssignments(_assignments);
        Repaint();
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawPalette();
        EditorGUILayout.Space(12f);
        DrawAssignments();

        EditorGUILayout.EndScrollView();

        RunPending();
    }

    private void DrawPalette()
    {
        EditorGUILayout.LabelField("팔레트", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "프로젝트 창에서 에셋을 고르고 Ctrl+/ 를 누르면 이 목록이 뜹니다.",
            EditorStyles.miniLabel);

        EditorGUILayout.Space(4f);

        bool edited = false;

        for (int i = 0; i < _palette.Count; i++)
        {
            AssetHighlightStore.PaletteEntry entry = _palette[i];

            EditorGUILayout.BeginHorizontal();

            // 버튼까지 ChangeCheck로 묶으면 ▲▼X를 눌러도 저장이 돈다. 입력 칸만 감시한다.
            EditorGUI.BeginChangeCheck();

            string name = EditorGUILayout.TextField(entry.Name);
            Color color = EditorGUILayout.ColorField(GUIContent.none, entry.Color, false, false, false,
                GUILayout.Width(SwatchWidth));

            if (EditorGUI.EndChangeCheck())
            {
                entry.Name = name;
                entry.Color = color;
                edited = true;
            }

            using (new EditorGUI.DisabledScope(i == 0))
            {
                if (GUILayout.Button("▲", GUILayout.Width(ButtonWidth))) SetPending(PendingKind.MoveUp, i);
            }

            using (new EditorGUI.DisabledScope(i == _palette.Count - 1))
            {
                if (GUILayout.Button("▼", GUILayout.Width(ButtonWidth))) SetPending(PendingKind.MoveDown, i);
            }

            if (GUILayout.Button("X", GUILayout.Width(ButtonWidth))) SetPending(PendingKind.Remove, i);

            EditorGUILayout.EndHorizontal();
        }

        if (edited) AssetHighlightStore.SetPalette(CopyPalette());

        EditorGUILayout.Space(4f);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("색 추가"))
        {
            _palette.Add(new AssetHighlightStore.PaletteEntry($"색 {_palette.Count + 1}", Color.gray));
            AssetHighlightStore.SetPalette(CopyPalette());
        }

        if (GUILayout.Button("기본값으로"))
        {
            AssetHighlightStore.ResetPalette();
            Reload();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawAssignments()
    {
        EditorGUILayout.LabelField($"칠해둔 에셋 ({_assignments.Count})", EditorStyles.boldLabel);

        for (int i = 0; i < _assignments.Count; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(_assignments[i].Key);
            bool missing = string.IsNullOrEmpty(path);

            EditorGUILayout.BeginHorizontal();

            Rect swatch = GUILayoutUtility.GetRect(14f, 14f, GUILayout.Width(14f), GUILayout.ExpandWidth(false));
            swatch.y += 2f;
            EditorGUI.DrawRect(swatch, _assignments[i].Value);

            string label = missing ? "(없어진 에셋)" : path;

            if (GUILayout.Button(label, EditorStyles.linkLabel) && !missing)
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(path));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("X", GUILayout.Width(ButtonWidth))) SetPending(PendingKind.ClearAsset, i);

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("없어진 에셋 정리"))
        {
            int removed = AssetHighlightStore.PruneMissing();
            Debug.Log($"[AssetHighlight] 없어진 에셋 {removed}개를 정리했습니다.");
        }

        if (GUILayout.Button("전체 지우기") &&
            EditorUtility.DisplayDialog("에셋 하이라이트", "칠해둔 색을 전부 지웁니다.", "지우기", "취소"))
        {
            AssetHighlightStore.ClearAll();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void SetPending(PendingKind kind, int index)
    {
        _pendingKind = kind;
        _pendingIndex = index;
    }

    private void RunPending()
    {
        if (_pendingKind == PendingKind.None) return;

        PendingKind kind = _pendingKind;
        int index = _pendingIndex;
        _pendingKind = PendingKind.None;

        switch (kind)
        {
            case PendingKind.Remove:
                _palette.RemoveAt(index);
                AssetHighlightStore.SetPalette(CopyPalette());
                Reload();
                return;

            case PendingKind.MoveUp:
                Swap(index, index - 1);
                return;

            case PendingKind.MoveDown:
                Swap(index, index + 1);
                return;

            case PendingKind.ClearAsset:
                AssetHighlightStore.Clear(new[] { _assignments[index].Key });
                return;
        }
    }

    private void Swap(int a, int b)
    {
        (_palette[a], _palette[b]) = (_palette[b], _palette[a]);
        AssetHighlightStore.SetPalette(CopyPalette());
    }

    /// <summary>저장소에 편집용 리스트를 그대로 넘기면 같은 객체를 양쪽에서 잡게 된다.</summary>
    private List<AssetHighlightStore.PaletteEntry> CopyPalette()
    {
        List<AssetHighlightStore.PaletteEntry> copy = new(_palette.Count);

        foreach (AssetHighlightStore.PaletteEntry entry in _palette)
            copy.Add(new AssetHighlightStore.PaletteEntry(entry.Name, entry.Color));

        return copy;
    }
}
