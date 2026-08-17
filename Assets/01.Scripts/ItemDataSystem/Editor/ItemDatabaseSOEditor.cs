using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ItemDatabaseSO))]
public class ItemDatabaseSOEditor : Editor
{
    private const string RangesField = "_idRanges";
    private const string NextIdField = "_nextId";
    private const string ItemsField = "_items";

    private static readonly GUIContent TildeLabel = new("~");

    private bool _showRanges = true;
    private HashSet<string> _knownTypeNames;

    // 충돌 검사는 프로젝트의 모든 에셋을 읽으므로 매 리페인트마다 돌리면 안 된다.
    private List<ItemIdConflictResolver.Conflict> _conflicts;

    // 에셋을 고치면 그려야 할 항목 수가 달라진다. 그리는 도중에 바꾸면 레이아웃이 어긋나므로
    // 버튼은 할 일을 여기 적어두기만 하고, 실제 실행은 한 프레임을 다 그린 뒤에 한다.
    private Action _pendingAction;

    private void OnEnable() => RefreshConflicts();

    private void RefreshConflicts() => _conflicts = ItemIdConflictResolver.Scan((ItemDatabaseSO)target);

    public override void OnInspectorGUI()
    {
        ItemDatabaseSO database = (ItemDatabaseSO)target;

        EditorGUILayout.HelpBox(
            $"등록된 아이템 {database.All.Count}개", MessageType.None);

        DrawConflicts(database);
        DrawActions(database);
        DrawRanges(database);
        DrawValidation(database);

        EditorGUILayout.Space();

        // Id와 목록이 툴 밖에서 바뀌면 세이브가 깨지므로 읽기 전용으로만 보여준다.
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(NextIdField));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(ItemsField), true);
        }

        RunPendingAction();
    }

    private void RunPendingAction()
    {
        if (_pendingAction == null) return;

        Action action = _pendingAction;
        _pendingAction = null;

        action();
        RefreshConflicts();
        Repaint();
    }

    /// <summary>
    /// Ctrl+D로 복제하면 Id까지 복사된다. 아이템을 만드는 가장 흔한 방법이라 자주 생기고,
    /// 그대로 두면 인벤토리가 엉뚱한 아이템을 꺼낸다. 그래서 맨 위에 크게 띄운다.
    /// </summary>
    private void DrawConflicts(ItemDatabaseSO database)
    {
        if (_conflicts == null || _conflicts.Count == 0) return;

        int duplicateCount = 0;
        foreach (ItemIdConflictResolver.Conflict conflict in _conflicts)
            duplicateCount += conflict.Duplicates.Count;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.HelpBox(
                $"Id가 겹치는 아이템이 있습니다. ({_conflicts.Count}개 번호 / 재부여 대상 {duplicateCount}개)\n" +
                "복제한 에셋이 원본의 Id를 그대로 들고 있습니다. 이대로 두면 한 번호에 여러 아이템이 물립니다.",
                MessageType.Error);

            foreach (ItemIdConflictResolver.Conflict conflict in _conflicts)
                DrawConflictGroup(conflict);

            EditorGUILayout.Space(2f);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Id 충돌 해결", GUILayout.Height(26f)))
                _pendingAction = () => ItemIdConflictResolver.Resolve(database);

            // RunPendingAction이 항상 다시 검사하므로 빈 동작이면 검사만 돈다.
            if (GUILayout.Button("다시 검사", GUILayout.Height(26f), GUILayout.Width(90f)))
                _pendingAction = () => { };

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();
    }

    private static void DrawConflictGroup(ItemIdConflictResolver.Conflict conflict)
    {
        EditorGUILayout.LabelField($"Id {conflict.Id}", EditorStyles.boldLabel);

        using (new EditorGUI.IndentLevelScope())
        {
            DrawConflictRow("유지", conflict.Keeper);

            foreach (ItemDataSO duplicate in conflict.Duplicates)
                DrawConflictRow("재부여", duplicate);
        }
    }

    private static void DrawConflictRow(string tag, ItemDataSO item)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(tag, GUILayout.Width(60f));

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.ObjectField(item, typeof(ItemDataSO), false);

        EditorGUILayout.EndHorizontal();
    }

    private void DrawActions(ItemDatabaseSO database)
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("프로젝트 스캔 & 갱신", GUILayout.Height(28f)))
            _pendingAction = () => ItemDatabaseBuilder.Rebuild(database);

        if (GUILayout.Button("Item Creator 열기", GUILayout.Height(28f), GUILayout.Width(140f)))
            ItemCreatorWindow.Open();

        EditorGUILayout.EndHorizontal();

        if (database.IdRanges.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Id 대역이 비어 있습니다. 지금은 아이템이 만든 순서대로 1, 2, 3번을 받습니다.",
                MessageType.Warning);

            if (GUILayout.Button("기본 대역 채우기"))
                _pendingAction = () => ItemDatabaseBuilder.SeedDefaultRanges(database);

            return;
        }

        DrawReassignAll(database);
    }

    /// <summary>
    /// Id 불변 규칙을 깨는 유일한 버튼이라 확인을 두 번 받는다.
    /// 세이브가 붙기 전에만 쓸 수 있는 마이그레이션 경로다.
    /// </summary>
    private void DrawReassignAll(ItemDatabaseSO database)
    {
        EditorGUILayout.Space();

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("마이그레이션", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "모든 아이템의 Id를 지우고 대역에 맞춰 다시 부여합니다.\n" +
                "이전 Id를 들고 있는 세이브·외부 데이터는 전부 어긋나게 됩니다.",
                MessageType.Warning);

            if (!GUILayout.Button("전체 Id 재부여")) return;

            _pendingAction = () =>
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "전체 Id 재부여",
                    $"아이템 {database.All.Count}개의 Id를 전부 다시 부여합니다.\n\n" +
                    "되돌릴 수 없습니다. 세이브 시스템이 붙기 전에만 안전합니다.\n\n" +
                    "계속할까요?",
                    "재부여", "취소");

                if (confirmed)
                    ItemDatabaseBuilder.ReassignAll(database);
            };
        }
    }

    private void DrawRanges(ItemDatabaseSO database)
    {
        serializedObject.Update();

        SerializedProperty ranges = serializedObject.FindProperty(RangesField);

        _showRanges = EditorGUILayout.Foldout(_showRanges, $"Id 대역 ({ranges.arraySize})", true);
        if (!_showRanges)
        {
            serializedObject.ApplyModifiedProperties();
            return;
        }

        EnsureKnownTypeNames();

        using (new EditorGUI.IndentLevelScope())
        {
            int removeIndex = -1;

            for (int i = 0; i < ranges.arraySize; i++)
            {
                if (DrawRangeElement(database, ranges.GetArrayElementAtIndex(i)))
                    removeIndex = i;
            }

            if (removeIndex >= 0)
                ranges.DeleteArrayElementAtIndex(removeIndex);

            if (GUILayout.Button("대역 추가", GUILayout.Width(120f)))
                ranges.arraySize++;
        }

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>지우기를 눌렀으면 true.</summary>
    private bool DrawRangeElement(ItemDatabaseSO database, SerializedProperty element)
    {
        SerializedProperty typeName = element.FindPropertyRelative("typeName");
        SerializedProperty label = element.FindPropertyRelative("label");
        SerializedProperty start = element.FindPropertyRelative("start");
        SerializedProperty end = element.FindPropertyRelative("end");
        SerializedProperty nextId = element.FindPropertyRelative("nextId");

        bool remove;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(label, GUIContent.none, GUILayout.Width(110f));
            EditorGUILayout.PropertyField(typeName, GUIContent.none);
            remove = GUILayout.Button("X", GUILayout.Width(24f));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(start, GUIContent.none, GUILayout.Width(80f));
            GUILayout.Label(TildeLabel, GUILayout.Width(14f));
            EditorGUILayout.PropertyField(end, GUIContent.none, GUILayout.Width(80f));

            // 커서를 손으로 되돌리면 Id가 겹친다. 보여주기만 한다.
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.LabelField($"다음 {nextId.uintValue}", GUILayout.Width(90f));

            GUILayout.Label(BuildUsageText(database, start.uintValue, end.uintValue, nextId.uintValue));
            EditorGUILayout.EndHorizontal();

            string warning = BuildRangeWarning(typeName.stringValue, start.uintValue, end.uintValue);
            if (!string.IsNullOrEmpty(warning))
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }

        return remove;
    }

    private static string BuildUsageText(ItemDatabaseSO database, uint start, uint end, uint nextId)
    {
        if (end < start) return string.Empty;

        uint capacity = end - start + 1;

        int used = 0;
        foreach (ItemDataSO item in database.All)
        {
            if (item != null && item.Id >= start && item.Id <= end)
                used++;
        }

        return nextId > end
            ? $"{used}개 · 대역이 가득 찼습니다"
            : $"{used}개 · 남은 번호 {end - Math.Max(nextId, start) + 1}/{capacity}";
    }

    private string BuildRangeWarning(string typeName, uint start, uint end)
    {
        if (start == 0) return "시작이 0입니다. 0은 '등록 안 됨'을 뜻하므로 1 이상이어야 합니다.";
        if (end < start) return "끝이 시작보다 작습니다.";

        // 타입 없이 자리만 잡아둔 대역은 정상이다. 클래스를 만들면 이름을 채우면 된다.
        if (string.IsNullOrEmpty(typeName)) return string.Empty;

        return _knownTypeNames.Contains(typeName)
            ? string.Empty
            : $"'{typeName}' 타입이 프로젝트에 없습니다. 클래스 이름이 바뀌었는지 확인하세요.";
    }

    private void EnsureKnownTypeNames()
    {
        if (_knownTypeNames != null) return;

        _knownTypeNames = new HashSet<string> { nameof(ItemDataSO) };

        foreach (Type type in TypeCache.GetTypesDerivedFrom<ItemDataSO>())
            _knownTypeNames.Add(type.Name);
    }

    private static void DrawValidation(ItemDatabaseSO database)
    {
        StringBuilder problems = new();

        DrawOverlapValidation(database, problems);

        // 키가 겹치면 두 아이템이 시트의 같은 행을 보게 되어 이름이 하나로 합쳐진다.
        Dictionary<string, ItemDataSO> keyOwners = new();

        foreach (ItemDataSO item in database.All)
        {
            if (item == null || string.IsNullOrEmpty(item.LocalizationKey)) continue;

            if (!keyOwners.TryAdd(item.LocalizationKey, item))
            {
                problems.AppendLine(
                    $"- {item.name}: 로컬라이즈 키 '{item.LocalizationKey}'가 {keyOwners[item.LocalizationKey].name}과 겹칩니다.");
            }
        }

        foreach (ItemDataSO item in database.All)
        {
            if (item == null)
            {
                problems.AppendLine("- 목록에 빈 항목이 있습니다. 스캔을 다시 실행하세요.");
                continue;
            }

            if (!item.IsRegistered)
                problems.AppendLine($"- {item.name}: Id가 없습니다.");

            if (item.ItemCategory == ItemCategoryType.None)
                problems.AppendLine($"- {item.name}: 카테고리가 None 입니다.");

            // 키가 없으면 시트에 행이 있어도 영영 못 찾는다. Item Creator에서 일괄로 채울 수 있다.
            if (string.IsNullOrEmpty(item.LocalizationKey))
                problems.AppendLine($"- {item.name}: 로컬라이즈 키가 없습니다.");

            // 월드 스프라이트는 비어있으면 아이콘으로 되돌아가므로, 아이콘이 없으면 양쪽 다 없는 것이다.
            if (item.IconSprite == null)
                problems.AppendLine($"- {item.name}: UI 아이콘이 없습니다."
                    + (item.HasDedicatedWorldSprite ? " (월드 스프라이트만 있습니다)" : string.Empty));

            if (item.IsRegistered
                && database.TryGetRange(item.GetType(), out ItemIdRange range)
                && !range.Contains(item.Id))
            {
                problems.AppendLine(
                    $"- {item.name}: Id {item.Id}가 '{range.DisplayName}' 대역({range.start}~{range.end}) 밖입니다.");
            }
        }

        if (problems.Length > 0)
            EditorGUILayout.HelpBox(problems.ToString().TrimEnd(), MessageType.Warning);
    }

    private static void DrawOverlapValidation(ItemDatabaseSO database, StringBuilder problems)
    {
        IReadOnlyList<ItemIdRange> ranges = database.IdRanges;

        for (int i = 0; i < ranges.Count; i++)
        {
            if (!ranges[i].IsValid) continue;

            for (int j = i + 1; j < ranges.Count; j++)
            {
                if (!ranges[j].IsValid) continue;

                // 겹치면 같은 번호를 두 대역이 나눠주게 되어 Id가 충돌한다.
                if (ranges[i].start <= ranges[j].end && ranges[j].start <= ranges[i].end)
                {
                    problems.AppendLine(
                        $"- 대역 '{ranges[i].DisplayName}'와 '{ranges[j].DisplayName}'가 겹칩니다.");
                }
            }
        }
    }
}
