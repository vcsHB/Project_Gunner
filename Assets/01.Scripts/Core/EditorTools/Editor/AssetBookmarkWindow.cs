using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// 자주 여는 에셋·폴더를 탭별로 모아두는 창. Project 창에서 끌어다 놓으면 지금 탭에 들어간다.
///
/// 항목은 ObjectField로 그립니다 — 한 번 클릭하면 Project 창에서 위치를 짚고(ping), 두 번 클릭하면 연다.
/// 창 어디에 놓든 <b>OnGUI 맨 앞에서 드래그를 가로챕니다.</b> 그러지 않으면 항목의 ObjectField가
/// 드롭을 받아서 기존 항목이 새 에셋으로 바뀌어버립니다.
/// </summary>
public class AssetBookmarkWindow : EditorWindow
{
    private const string RenameControl = "AssetBookmarkRename";
    private const float PathGap = 6f;
    private const float MinPathWidth = 40f;

    private ReorderableList _list;
    private List<AssetBookmarkStore.Entry> _listSource;

    private SearchField _searchField;
    private string _search = string.Empty;
    private bool _searchAllTabs;

    private bool _renaming;
    private bool _focusRename;
    private string _renameBuffer;

    private bool _dragHover;
    private Vector2 _scroll;

    /// <summary>GUID+로컬Id → 에셋. 매 프레임 로드하지 않으려고 둔다. 프로젝트가 바뀌면 비운다.</summary>
    private readonly Dictionary<string, Object> _cache = new();

    private GUIStyle _missingStyle;

    [MenuItem("Tools/Asset Bookmarks")]
    private static void Open()
    {
        AssetBookmarkWindow window = GetWindow<AssetBookmarkWindow>();
        window.titleContent = new GUIContent("Bookmarks", EditorGUIUtility.IconContent("Favorite Icon").image);
        window.Show();

        // 떠 있는 창이 저장된 위치 그대로 화면 밖(모니터 배치가 바뀐 경우 등)에 열리면 찾을 수가 없다.
        // 메인 창과 전혀 겹치지 않으면 메인 창 가운데로 데려온다. 도킹돼 있으면 건드리지 않는다.
        if (!window.docked)
        {
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            if (!main.Overlaps(window.position))
            {
                Vector2 size = new(Mathf.Max(window.position.width, 320f), Mathf.Max(window.position.height, 400f));
                window.position = new Rect(main.center - size * 0.5f, size);
            }
        }

        window.Focus();
    }

    private void OnEnable()
    {
        _searchField = new SearchField();
        EditorApplication.projectChanged += OnProjectChanged;
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= OnProjectChanged;
    }

    private void OnProjectChanged()
    {
        _cache.Clear();
        Repaint();
    }

    private void OnGUI()
    {
        _missingStyle ??= new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.9f, 0.35f, 0.35f) } };

        HandleDragAndDrop();

        DrawTabToolbar();
        DrawSearchToolbar();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        if (string.IsNullOrEmpty(_search)) DrawTabList();
        else DrawSearchResults();

        DrawDropHint();

        EditorGUILayout.EndScrollView();

        DrawDragOverlay();
    }

    // ───────────────────────── 툴바 ─────────────────────────

    private void DrawTabToolbar()
    {
        using var _ = new EditorGUILayout.HorizontalScope(EditorStyles.toolbar);

        if (_renaming)
        {
            DrawRenameField();
            return;
        }

        IReadOnlyList<AssetBookmarkStore.Tab> tabs = AssetBookmarkStore.Tabs;
        string[] names = new string[tabs.Count];

        // 팝업은 '/'를 하위 메뉴로 쪼갠다. 이름에 '/'를 써도 한 줄로 보이게 비슷한 글자로 바꿔 보여준다.
        for (int i = 0; i < tabs.Count; i++)
            names[i] = $"{tabs[i].Name.Replace('/', '∕')}  ({tabs[i].Entries.Count})";

        int selected = AssetBookmarkStore.SelectedIndex;
        int picked = EditorGUILayout.Popup(selected, names, EditorStyles.toolbarPopup, GUILayout.MinWidth(120));

        if (picked != selected)
        {
            AssetBookmarkStore.SelectedIndex = picked;
            GUI.FocusControl(null);
        }

        if (GUILayout.Button(new GUIContent("+", "새 탭"), EditorStyles.toolbarButton, GUILayout.Width(24)))
        {
            AssetBookmarkStore.AddTab(null);
            BeginRename();
        }

        if (GUILayout.Button(EditorGUIUtility.IconContent("_Menu"), EditorStyles.toolbarButton, GUILayout.Width(24)))
            ShowTabMenu();

        GUILayout.FlexibleSpace();
    }

    private void DrawRenameField()
    {
        // 텍스트 필드가 Enter·Esc를 먹기 전에 먼저 본다.
        Event e = Event.current;

        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
            {
                CommitRename();
                e.Use();
                return;
            }

            if (e.keyCode == KeyCode.Escape)
            {
                CancelRename();
                e.Use();
                return;
            }
        }

        GUILayout.Label("이름", GUILayout.Width(30));

        GUI.SetNextControlName(RenameControl);
        _renameBuffer = GUILayout.TextField(_renameBuffer, EditorStyles.toolbarTextField, GUILayout.ExpandWidth(true));

        if (_focusRename)
        {
            EditorGUI.FocusTextInControl(RenameControl);
            _focusRename = false;
        }

        if (GUILayout.Button("확인", EditorStyles.toolbarButton, GUILayout.Width(40))) CommitRename();
        if (GUILayout.Button("취소", EditorStyles.toolbarButton, GUILayout.Width(40))) CancelRename();
    }

    private void DrawSearchToolbar()
    {
        using var _ = new EditorGUILayout.HorizontalScope(EditorStyles.toolbar);

        _search = _searchField.OnToolbarGUI(_search) ?? string.Empty;
        _searchAllTabs = GUILayout.Toggle(_searchAllTabs, new GUIContent("모든 탭", "모든 탭에서 찾는다"),
            EditorStyles.toolbarButton, GUILayout.Width(52));
    }

    private void ShowTabMenu()
    {
        AssetBookmarkStore.Tab tab = AssetBookmarkStore.SelectedTab;
        GenericMenu menu = new();

        menu.AddItem(new GUIContent("새 탭"), false, () =>
        {
            AssetBookmarkStore.AddTab(null);
            BeginRename();
        });
        menu.AddItem(new GUIContent("이름 바꾸기"), false, BeginRename);
        menu.AddItem(new GUIContent("탭 삭제"), false, () => DeleteTab(tab));
        menu.AddSeparator(string.Empty);
        menu.AddItem(new GUIContent("없는 항목 정리"), false, () =>
        {
            int removed = AssetBookmarkStore.PruneMissing(tab);
            ShowNotification(new GUIContent($"{removed}개 정리"));
        });
        menu.AddItem(new GUIContent("탭 비우기"), false, () =>
        {
            if (tab.Entries.Count == 0) return;
            if (!EditorUtility.DisplayDialog("탭 비우기", $"'{tab.Name}'의 항목 {tab.Entries.Count}개를 지웁니다.", "비우기", "취소")) return;

            tab.Entries.Clear();
            AssetBookmarkStore.Save();
            Repaint();
        });

        menu.ShowAsContext();
    }

    private void DeleteTab(AssetBookmarkStore.Tab tab)
    {
        if (tab.Entries.Count > 0 &&
            !EditorUtility.DisplayDialog("탭 삭제", $"'{tab.Name}' 탭과 항목 {tab.Entries.Count}개를 지웁니다.", "삭제", "취소"))
            return;

        AssetBookmarkStore.RemoveTab(tab);
        _list = null;
        Repaint();
    }

    private void BeginRename()
    {
        _renaming = true;
        _focusRename = true;
        _renameBuffer = AssetBookmarkStore.SelectedTab.Name;
        Repaint();
    }

    private void CommitRename()
    {
        AssetBookmarkStore.RenameTab(AssetBookmarkStore.SelectedTab, _renameBuffer);
        CancelRename();
    }

    private void CancelRename()
    {
        _renaming = false;
        GUI.FocusControl(null);
        Repaint();
    }

    // ───────────────────────── 목록 ─────────────────────────

    private void DrawTabList()
    {
        AssetBookmarkStore.Tab tab = AssetBookmarkStore.SelectedTab;
        if (tab.Entries.Count == 0) return;

        EnsureList(tab);
        _list.DoLayoutList();
    }

    private void EnsureList(AssetBookmarkStore.Tab tab)
    {
        if (_list != null && _listSource == tab.Entries) return;

        _listSource = tab.Entries;
        _list = new ReorderableList(tab.Entries, typeof(AssetBookmarkStore.Entry), true, false, false, true)
        {
            multiSelect = true,
            elementHeight = EditorGUIUtility.singleLineHeight + 4f,
            drawElementCallback = (rect, index, _, _) => DrawEntry(rect, _listSource[index], null),
            onReorderCallback = _ => AssetBookmarkStore.Save(),
            onRemoveCallback = RemoveSelected,
        };
    }

    private void RemoveSelected(ReorderableList list)
    {
        List<int> indices = new(list.selectedIndices);
        if (indices.Count == 0 && list.index >= 0) indices.Add(list.index);

        indices.Sort();

        for (int i = indices.Count - 1; i >= 0; i--)
        {
            if (indices[i] < _listSource.Count) _listSource.RemoveAt(indices[i]);
        }

        list.ClearSelection();
        AssetBookmarkStore.Save();
    }

    /// <summary>검색 중엔 걸러진 목록이라 순서를 바꿀 수 없다. 끌어 옮길 자리가 원래 목록의 어디인지 정할 수 없어서다.</summary>
    private void DrawSearchResults()
    {
        IReadOnlyList<AssetBookmarkStore.Tab> tabs = AssetBookmarkStore.Tabs;
        AssetBookmarkStore.Tab current = AssetBookmarkStore.SelectedTab;
        int shown = 0;

        for (int t = 0; t < tabs.Count; t++)
        {
            AssetBookmarkStore.Tab tab = tabs[t];
            if (!_searchAllTabs && tab != current) continue;

            for (int i = 0; i < tab.Entries.Count; i++)
            {
                AssetBookmarkStore.Entry entry = tab.Entries[i];
                if (!MatchesSearch(entry)) continue;

                Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 2f);
                rect.xMin += 4f;
                DrawEntry(rect, entry, _searchAllTabs ? tab.Name : null);
                shown++;
            }
        }

        if (shown == 0) EditorGUILayout.HelpBox("찾는 항목이 없습니다.", MessageType.None);
    }

    private bool MatchesSearch(AssetBookmarkStore.Entry entry)
    {
        string path = AssetDatabase.GUIDToAssetPath(entry.Guid);
        if (path.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;

        Object obj = Resolve(entry);
        return obj != null && obj.name.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void DrawEntry(Rect rect, AssetBookmarkStore.Entry entry, string tabName)
    {
        rect.y += 1f;
        rect.height = EditorGUIUtility.singleLineHeight;

        Object obj = Resolve(entry);
        string path = AssetDatabase.GUIDToAssetPath(entry.Guid);

        float fieldWidth = Mathf.Min(rect.width, Mathf.Max(140f, rect.width * 0.5f));
        Rect fieldRect = new(rect.x, rect.y, fieldWidth, rect.height);
        Rect pathRect = new(fieldRect.xMax + PathGap, rect.y, rect.width - fieldWidth - PathGap, rect.height);

        if (obj == null)
        {
            string label = string.IsNullOrEmpty(path) ? $"(없음) {entry.Guid}" : $"(없음) {path}";
            EditorGUI.LabelField(fieldRect, new GUIContent(label, "에셋을 찾을 수 없습니다. ☰ > 없는 항목 정리"), _missingStyle);
        }
        else
        {
            // 피커(◎)로 다른 에셋을 고르면 그 항목을 바꾼다. 이미 있는 에셋이면 무시한다.
            EditorGUI.BeginChangeCheck();
            Object picked = EditorGUI.ObjectField(fieldRect, obj, typeof(Object), false);

            if (EditorGUI.EndChangeCheck() && picked != null &&
                AssetBookmarkStore.TryCreateEntry(picked, out AssetBookmarkStore.Entry replaced) &&
                !AssetBookmarkStore.Contains(AssetBookmarkStore.SelectedTab, replaced))
            {
                entry.Guid = replaced.Guid;
                entry.LocalId = replaced.LocalId;
                AssetBookmarkStore.Save();
            }
        }

        if (pathRect.width < MinPathWidth) return;

        string shownPath = tabName != null ? $"[{tabName}]  {path}" : path;
        EditorGUI.LabelField(pathRect, new GUIContent(shownPath, path), EditorStyles.miniLabel);
    }

    private Object Resolve(AssetBookmarkStore.Entry entry)
    {
        string key = entry.Guid + ":" + entry.LocalId;
        // 없는 에셋(null)도 캐시한다. 서브에셋은 찾을 때마다 파일 전체를 읽어서다. 프로젝트가 바뀌면 다시 찾는다.
        if (_cache.TryGetValue(key, out Object obj)) return obj;

        obj = AssetBookmarkStore.Resolve(entry);
        _cache[key] = obj;
        return obj;
    }

    // ───────────────────────── 드래그 앤 드롭 ─────────────────────────

    private void DrawDropHint()
    {
        bool empty = AssetBookmarkStore.SelectedTab.Entries.Count == 0 && string.IsNullOrEmpty(_search);

        GUILayout.FlexibleSpace();

        if (empty)
            EditorGUILayout.HelpBox("Project 창에서 에셋이나 폴더를 이 창으로 끌어다 놓으면 지금 탭에 들어갑니다.", MessageType.Info);
    }

    private void HandleDragAndDrop()
    {
        Event e = Event.current;

        if (e.type == EventType.DragExited)
        {
            _dragHover = false;
            Repaint();
            return;
        }

        if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;
        if (!HasDroppableAsset(DragAndDrop.objectReferences)) return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        _dragHover = true;

        if (e.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            _dragHover = false;

            int added = AssetBookmarkStore.AddAssets(AssetBookmarkStore.SelectedTab, DragAndDrop.objectReferences);
            ShowNotification(new GUIContent(added > 0 ? $"{added}개 추가" : "이미 있습니다"));
        }

        e.Use();
        Repaint();
    }

    private static bool HasDroppableAsset(Object[] objects)
    {
        if (objects == null) return false;

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null && AssetDatabase.Contains(objects[i])) return true;
        }

        return false;
    }

    private void DrawDragOverlay()
    {
        if (!_dragHover || Event.current.type != EventType.Repaint) return;

        Rect full = new(0f, 0f, position.width, position.height);
        EditorGUI.DrawRect(full, new Color(0.29f, 0.55f, 0.95f, 0.12f));

        GUIStyle style = new(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
        GUI.Label(full, $"'{AssetBookmarkStore.SelectedTab.Name}'에 추가", style);
    }
}
