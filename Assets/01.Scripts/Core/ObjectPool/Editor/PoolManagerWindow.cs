using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public class PoolManagerWindow : EditorWindow
{
    private const string GroupsField = "_groups";
    private const string GroupNameField = "_name";
    private const string GroupItemsField = "_items";

    private const string ItemIdField = "_id";
    private const string ItemEnumNameField = "_enumName";
    private const string ItemObjectNameField = "_objectName";
    private const string ItemPrefabField = "_prefab";
    private const string ItemPrewarmField = "_prewarmCount";

    private const float PrefabWidth = 190f;
    private const float EnumNameWidth = 150f;
    private const float ObjectNameWidth = 150f;
    private const float PrewarmWidth = 60f;
    private const float DeleteWidth = 24f;

    [MenuItem("Tools/Pool Manager")]
    public static void Open()
    {
        PoolManagerWindow window = GetWindow<PoolManagerWindow>("Pool Manager");
        window.minSize = new Vector2(760f, 420f);
    }

    [SerializeField] private PoolListSO _poolList;
    [SerializeField] private int _selectedGroup;
    [SerializeField] private Vector2 _scroll;

    private SerializedObject _serialized;
    private SerializedProperty _groups;

    private bool _enumUpToDate;
    private double _nextEnumCheckTime;

    private void OnEnable()
    {
        if (_poolList == null)
            _poolList = FindPoolList();

        Bind();
    }

    private void Bind()
    {
        if (_poolList == null)
        {
            _serialized = null;
            _groups = null;
            return;
        }

        _serialized = new SerializedObject(_poolList);
        _groups = _serialized.FindProperty(GroupsField);
    }

    private void OnGUI()
    {
        DrawAssetField();

        if (_poolList == null)
        {
            EditorGUILayout.HelpBox("PoolListSO가 없습니다. 새로 만들거나 위에서 지정하세요.", MessageType.Info);
            if (GUILayout.Button("PoolListSO 새로 만들기"))
                CreatePoolList();

            return;
        }

        _serialized.Update();

        DrawTabs();
        EditorGUILayout.Space(4f);
        DrawItems();

        GUILayout.FlexibleSpace();
        DrawFooter();

        _serialized.ApplyModifiedProperties();
    }

    #region Asset

    private void DrawAssetField()
    {
        EditorGUI.BeginChangeCheck();
        PoolListSO next = (PoolListSO)EditorGUILayout.ObjectField("Pool List", _poolList, typeof(PoolListSO), false);
        if (EditorGUI.EndChangeCheck())
        {
            _poolList = next;
            _selectedGroup = 0;
            Bind();
        }

        EditorGUILayout.Space(4f);
    }

    private static PoolListSO FindPoolList()
    {
        string[] guids = AssetDatabase.FindAssets("t:PoolListSO");
        if (guids.Length == 0) return null;

        return AssetDatabase.LoadAssetAtPath<PoolListSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private void CreatePoolList()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "PoolListSO 만들기", "PoolListSO", "asset", "저장할 위치를 고르세요.");

        if (string.IsNullOrEmpty(path)) return;

        PoolListSO asset = CreateInstance<PoolListSO>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();

        _poolList = asset;
        _selectedGroup = 0;
        Bind();
    }

    #endregion

    #region Tabs

    private void DrawTabs()
    {
        int groupCount = _groups.arraySize;

        // 탭이 없을 때 Toolbar를 쓰면 클릭이 없어도 0을 돌려줘서 "+"가 눌린 것으로 오인된다.
        if (groupCount == 0)
        {
            if (GUILayout.Button("+ 탭 추가", GUILayout.Height(24f)))
                AddGroup();

            return;
        }

        string[] tabs = new string[groupCount + 1];
        for (int i = 0; i < groupCount; i++)
        {
            string name = _groups.GetArrayElementAtIndex(i).FindPropertyRelative(GroupNameField).stringValue;
            tabs[i] = string.IsNullOrEmpty(name) ? $"Group {i + 1}" : name;
        }

        tabs[groupCount] = "+";

        int clicked = GUILayout.Toolbar(
            Mathf.Clamp(_selectedGroup, 0, groupCount - 1), tabs, GUILayout.Height(24f));

        if (clicked == groupCount)
        {
            AddGroup();
            return;
        }

        _selectedGroup = clicked;

        EditorGUILayout.BeginHorizontal();
        SerializedProperty group = _groups.GetArrayElementAtIndex(_selectedGroup);
        EditorGUILayout.PropertyField(group.FindPropertyRelative(GroupNameField), new GUIContent("탭 이름"));

        if (GUILayout.Button("탭 삭제", GUILayout.Width(70f)))
            RemoveGroup(_selectedGroup);

        EditorGUILayout.EndHorizontal();
    }

    private void AddGroup()
    {
        _groups.arraySize++;
        SerializedProperty group = _groups.GetArrayElementAtIndex(_groups.arraySize - 1);

        // 배열을 늘리면 마지막 항목이 복제되므로 명시적으로 초기화한다.
        group.FindPropertyRelative(GroupNameField).stringValue = $"Group {_groups.arraySize}";
        group.FindPropertyRelative(GroupItemsField).ClearArray();

        _selectedGroup = _groups.arraySize - 1;
    }

    private void RemoveGroup(int index)
    {
        SerializedProperty items = _groups.GetArrayElementAtIndex(index).FindPropertyRelative(GroupItemsField);
        if (items.arraySize > 0 &&
            !EditorUtility.DisplayDialog("탭 삭제", $"항목 {items.arraySize}개가 함께 삭제됩니다. 계속할까요?", "삭제", "취소"))
            return;

        _groups.DeleteArrayElementAtIndex(index);
        _selectedGroup = Mathf.Clamp(index, 0, Mathf.Max(0, _groups.arraySize - 1));
    }

    #endregion

    #region Items

    private void DrawItems()
    {
        if (_groups.arraySize == 0)
        {
            EditorGUILayout.HelpBox("+ 를 눌러 탭을 먼저 만드세요.", MessageType.Info);
            return;
        }

        SerializedProperty items = _groups
            .GetArrayElementAtIndex(_selectedGroup)
            .FindPropertyRelative(GroupItemsField);

        DrawItemHeader();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        int removeIndex = -1;
        for (int i = 0; i < items.arraySize; i++)
        {
            if (DrawItemRow(items.GetArrayElementAtIndex(i)))
                removeIndex = i;
        }

        EditorGUILayout.EndScrollView();

        if (removeIndex >= 0)
            items.DeleteArrayElementAtIndex(removeIndex);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("항목 추가", GUILayout.Width(90f)))
            AddItem(items, null);

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        DrawDropArea(items);
    }

    private static void DrawItemHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Prefab", EditorStyles.miniBoldLabel, GUILayout.Width(PrefabWidth));
        GUILayout.Label("Enum 이름", EditorStyles.miniBoldLabel, GUILayout.Width(EnumNameWidth));
        GUILayout.Label("오브젝트 이름", EditorStyles.miniBoldLabel, GUILayout.Width(ObjectNameWidth));
        GUILayout.Label("Prewarm", EditorStyles.miniBoldLabel, GUILayout.Width(PrewarmWidth));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    /// <returns>삭제 버튼이 눌렸으면 true</returns>
    private bool DrawItemRow(SerializedProperty item)
    {
        SerializedProperty prefab = item.FindPropertyRelative(ItemPrefabField);
        SerializedProperty enumName = item.FindPropertyRelative(ItemEnumNameField);
        SerializedProperty objectName = item.FindPropertyRelative(ItemObjectNameField);
        SerializedProperty prewarm = item.FindPropertyRelative(ItemPrewarmField);

        EditorGUILayout.BeginHorizontal();

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(prefab, GUIContent.none, GUILayout.Width(PrefabWidth));
        if (EditorGUI.EndChangeCheck())
            AutoFillEnumName(prefab, enumName);

        EditorGUILayout.PropertyField(enumName, GUIContent.none, GUILayout.Width(EnumNameWidth));

        // 비어있으면 프리팹 이름이 쓰인다는 걸 placeholder로 보여준다.
        string placeholder = prefab.objectReferenceValue != null ? prefab.objectReferenceValue.name : "(프리팹 이름)";
        DrawWithPlaceholder(objectName, placeholder, ObjectNameWidth);

        EditorGUILayout.PropertyField(prewarm, GUIContent.none, GUILayout.Width(PrewarmWidth));

        GUILayout.FlexibleSpace();
        bool remove = GUILayout.Button("X", GUILayout.Width(DeleteWidth));

        EditorGUILayout.EndHorizontal();

        return remove;
    }

    private static void DrawWithPlaceholder(SerializedProperty property, string placeholder, float width)
    {
        Rect rect = GUILayoutUtility.GetRect(width, EditorGUIUtility.singleLineHeight, GUILayout.Width(width));
        EditorGUI.PropertyField(rect, property, GUIContent.none);

        if (!string.IsNullOrEmpty(property.stringValue)) return;

        GUIStyle style = new(EditorStyles.label) { fontStyle = FontStyle.Italic };
        style.normal.textColor = Color.gray;

        Rect textRect = new(rect.x + 3f, rect.y, rect.width - 6f, rect.height);
        EditorGUI.LabelField(textRect, placeholder, style);
    }

    // 오브젝트 이름은 비워두면 런타임에 프리팹 이름을 쓰므로 여기서 채우지 않는다.
    private static void AutoFillEnumName(SerializedProperty prefab, SerializedProperty enumName)
    {
        Object obj = prefab.objectReferenceValue;
        if (obj == null) return;

        if (string.IsNullOrEmpty(enumName.stringValue))
            enumName.stringValue = PoolEnumGenerator.Sanitize(obj.name);
    }

    private void AddItem(SerializedProperty items, PoolableMono prefab)
    {
        items.arraySize++;
        SerializedProperty item = items.GetArrayElementAtIndex(items.arraySize - 1);

        item.FindPropertyRelative(ItemIdField).intValue = NextId();
        item.FindPropertyRelative(ItemEnumNameField).stringValue =
            prefab != null ? PoolEnumGenerator.Sanitize(prefab.name) : string.Empty;
        item.FindPropertyRelative(ItemObjectNameField).stringValue = string.Empty;
        item.FindPropertyRelative(ItemPrefabField).objectReferenceValue = prefab;
        item.FindPropertyRelative(ItemPrewarmField).intValue = 8;
    }

    /// <summary>이미 쓰인 적 없는 Id를 준다. 한 번 준 Id는 재사용하지 않는다.</summary>
    private int NextId()
    {
        int max = 0;

        for (int g = 0; g < _groups.arraySize; g++)
        {
            SerializedProperty items = _groups.GetArrayElementAtIndex(g).FindPropertyRelative(GroupItemsField);
            for (int i = 0; i < items.arraySize; i++)
                max = Mathf.Max(max, items.GetArrayElementAtIndex(i).FindPropertyRelative(ItemIdField).intValue);
        }

        return max + 1;
    }

    private void DrawDropArea(SerializedProperty items)
    {
        Rect area = GUILayoutUtility.GetRect(0f, 38f, GUILayout.ExpandWidth(true));
        GUI.Box(area, "PoolableMono 프리팹을 여기에 드래그해서 추가", EditorStyles.helpBox);

        Event evt = Event.current;
        if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform) return;
        if (!area.Contains(evt.mousePosition)) return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (evt.type != EventType.DragPerform) return;

        DragAndDrop.AcceptDrag();

        foreach (Object dragged in DragAndDrop.objectReferences)
        {
            if (dragged is not GameObject go) continue;

            PoolableMono poolable = go.GetComponent<PoolableMono>();
            if (poolable == null)
            {
                Debug.LogWarning($"[Pool Manager] {go.name}에 PoolableMono가 없어 건너뜁니다.", go);
                continue;
            }

            AddItem(items, poolable);
        }

        evt.Use();
    }

    #endregion

    #region Footer

    private void DrawFooter()
    {
        string problems = Validate();
        if (!string.IsNullOrEmpty(problems))
            EditorGUILayout.HelpBox(problems, MessageType.Error);

        // 검증/생성은 저장된 값을 봐야 하므로 편집 내용을 먼저 반영한다.
        _serialized.ApplyModifiedProperties();

        bool upToDate = IsEnumUpToDate();
        if (!upToDate)
            EditorGUILayout.HelpBox("PoolType이 목록과 다릅니다. Generate Enum을 눌러주세요.", MessageType.Warning);

        using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(problems)))
        {
            GUI.backgroundColor = upToDate ? Color.white : Color.yellow;
            if (GUILayout.Button("Generate Enum", GUILayout.Height(28f)))
            {
                EditorUtility.SetDirty(_poolList);
                AssetDatabase.SaveAssetIfDirty(_poolList);
                PoolEnumGenerator.Generate(_poolList);
                _nextEnumCheckTime = 0d;
            }

            GUI.backgroundColor = Color.white;
        }
    }

    /// <summary>
    /// 파일을 매 프레임 읽지 않도록 Layout 이벤트에서만, 그것도 0.5초에 한 번만 확인한다.
    /// (Layout에서만 갱신해야 Repaint와 레이아웃이 어긋나지 않는다)
    /// </summary>
    private bool IsEnumUpToDate()
    {
        if (Event.current.type == EventType.Layout && EditorApplication.timeSinceStartup >= _nextEnumCheckTime)
        {
            _enumUpToDate = PoolEnumGenerator.IsUpToDate(_poolList);
            _nextEnumCheckTime = EditorApplication.timeSinceStartup + 0.5d;
        }

        return _enumUpToDate;
    }

    private string Validate()
    {
        StringBuilder problems = new();
        HashSet<string> usedNames = new();
        HashSet<int> usedIds = new();

        for (int g = 0; g < _groups.arraySize; g++)
        {
            SerializedProperty group = _groups.GetArrayElementAtIndex(g);
            string groupName = group.FindPropertyRelative(GroupNameField).stringValue;
            SerializedProperty items = group.FindPropertyRelative(GroupItemsField);

            for (int i = 0; i < items.arraySize; i++)
            {
                SerializedProperty item = items.GetArrayElementAtIndex(i);
                string enumName = item.FindPropertyRelative(ItemEnumNameField).stringValue;
                int id = item.FindPropertyRelative(ItemIdField).intValue;
                string label = $"[{groupName}] {i}번";

                if (item.FindPropertyRelative(ItemPrefabField).objectReferenceValue == null)
                    problems.AppendLine($"- {label}: prefab이 비어있습니다.");

                if (string.IsNullOrEmpty(enumName))
                    problems.AppendLine($"- {label}: enum 이름이 비어있습니다.");
                else if (!PoolEnumGenerator.IsValidName(enumName))
                    problems.AppendLine($"- {label}: '{enumName}'은 enum 이름으로 쓸 수 없습니다. (권장: {PoolEnumGenerator.Sanitize(enumName)})");
                else if (!usedNames.Add(enumName))
                    problems.AppendLine($"- {label}: enum 이름 '{enumName}'이 중복입니다.");

                if (id <= 0)
                    problems.AppendLine($"- {label}: Id가 없습니다. 항목을 지우고 다시 추가하세요.");
                else if (!usedIds.Add(id))
                    problems.AppendLine($"- {label}: Id {id}가 중복입니다.");
            }
        }

        return problems.ToString().TrimEnd();
    }

    #endregion
}
