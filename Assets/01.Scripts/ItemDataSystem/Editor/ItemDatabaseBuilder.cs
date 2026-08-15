using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 프로젝트의 ItemDataSO를 모아 ItemDatabase를 갱신한다.
/// 인스펙터와 Item Creator 창이 함께 쓴다.
///
/// Id는 타입별 대역에서 뽑는다. 대역마다 커서를 따로 들고 가므로,
/// 무기를 하나 지워도 그 번호가 탄약에게 넘어가는 일이 없다.
/// </summary>
public static class ItemDatabaseBuilder
{
    private const string ItemsField = "_items";
    private const string NextIdField = "_nextId";
    private const string RangesField = "_idRanges";
    private const string RangeNextIdField = "nextId";
    private const string RangeStartField = "start";
    private const string IdField = "_id";

    public static ItemDatabaseSO FindDatabase()
    {
        string[] guids = AssetDatabase.FindAssets($"t:{nameof(ItemDatabaseSO)}");
        if (guids.Length == 0) return null;

        return AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    public static List<ItemDataSO> LoadAllItemAssets()
    {
        string[] guids = AssetDatabase.FindAssets($"t:{nameof(ItemDataSO)}");
        List<ItemDataSO> assets = new(guids.Length);

        foreach (string guid in guids)
        {
            ItemDataSO asset = AssetDatabase.LoadAssetAtPath<ItemDataSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null)
                assets.Add(asset);
        }

        return assets;
    }

    /// <summary>대역 표가 비었을 때 기본값을 채운다. 이미 뭔가 있으면 아무것도 하지 않는다.</summary>
    public static void SeedDefaultRanges(ItemDatabaseSO database)
    {
        if (database == null || database.IdRanges.Count > 0) return;

        SerializedObject serialized = new(database);
        SerializedProperty ranges = serialized.FindProperty(RangesField);

        ItemIdRange[] defaults = ItemDatabaseSO.DefaultRanges;
        ranges.arraySize = defaults.Length;

        for (int i = 0; i < defaults.Length; i++)
            WriteRange(ranges.GetArrayElementAtIndex(i), defaults[i]);

        serialized.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();

        Debug.Log($"[ItemDatabase] 기본 Id 대역 {defaults.Length}개를 채웠습니다.", database);
    }

    /// <summary>
    /// 목록을 다시 만들고 Id가 없는 아이템에 새 Id를 부여한다.
    /// 이미 등록된 아이템의 Id는 절대 바꾸지 않는다. (세이브 호환)
    /// </summary>
    public static void Rebuild(ItemDatabaseSO database)
    {
        if (database == null)
        {
            Debug.LogError("[ItemDatabase] 갱신할 데이터베이스가 없습니다.");
            return;
        }

        List<ItemDataSO> assets = LoadAllItemAssets();
        Dictionary<uint, ItemDataSO> owners = BuildOwners(database);

        // 지금 게임이 그 Id로 해석하는 에셋이 자기 번호를 먼저 차지하게 한다.
        // 에셋을 Ctrl+D로 복제하면 Id까지 복사되는데, 이 순서면 원본이 Id를 지키고 복제본이 새로 받는다.
        assets.Sort((a, b) =>
        {
            int aOwner = IsOwner(owners, a) ? 0 : 1;
            int bOwner = IsOwner(owners, b) ? 0 : 1;

            if (aOwner != bOwner) return aOwner - bOwner;

            return string.CompareOrdinal(AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b));
        });

        IReadOnlyList<ItemIdRange> ranges = database.IdRanges;

        // 커서를 로컬로 꺼내 돌린 뒤 한 번에 되쓴다.
        uint[] cursors = new uint[ranges.Count];
        for (int i = 0; i < ranges.Count; i++)
            cursors[i] = ranges[i].Cursor;

        uint legacyNext = database.NextId == 0 ? 1u : database.NextId;

        HashSet<uint> usedIds = new();
        StringBuilder problems = new();
        int assigned = 0;

        foreach (ItemDataSO asset in assets)
        {
            int rangeIndex = database.FindRangeIndex(asset.GetType());

            if (asset.IsRegistered && usedIds.Add(asset.Id))
            {
                KeepExistingId(asset, rangeIndex, ranges, cursors, ref legacyNext, problems);
                continue;
            }

            // Id가 없거나(0), 다른 에셋이 이미 쓰고 있는 Id다.
            if (!TryAssignId(asset, rangeIndex, ranges, cursors, usedIds, ref legacyNext, problems))
                continue;

            assigned++;
        }

        // 목록은 Id 순으로 정렬해서 diff가 안정적이게 한다.
        assets.Sort((a, b) => a.Id.CompareTo(b.Id));

        SerializedObject serialized = new(database);

        SerializedProperty items = serialized.FindProperty(ItemsField);
        items.arraySize = assets.Count;
        for (int i = 0; i < assets.Count; i++)
            items.GetArrayElementAtIndex(i).objectReferenceValue = assets[i];

        SerializedProperty rangesProperty = serialized.FindProperty(RangesField);
        for (int i = 0; i < rangesProperty.arraySize && i < cursors.Length; i++)
        {
            rangesProperty.GetArrayElementAtIndex(i)
                .FindPropertyRelative(RangeNextIdField).uintValue = cursors[i];
        }

        serialized.FindProperty(NextIdField).uintValue = legacyNext;
        serialized.ApplyModifiedProperties();

        AssetDatabase.SaveAssets();

        Debug.Log($"[ItemDatabase] 아이템 {assets.Count}개를 등록했습니다. (새 Id {assigned}개 부여)", database);

        if (problems.Length > 0)
            Debug.LogWarning($"[ItemDatabase] 확인이 필요합니다.\n{problems.ToString().TrimEnd()}", database);
    }

    /// <summary>
    /// 모든 Id를 지우고 대역에 맞춰 처음부터 다시 부여한다.
    ///
    /// <b>Id 불변 규칙을 깨는 유일한 경로다.</b> 세이브 파일이나 외부에 나간 Id가 있으면
    /// 전부 엉뚱한 아이템을 가리키게 된다. 부르는 쪽에서 반드시 확인을 받을 것.
    /// </summary>
    public static void ReassignAll(ItemDatabaseSO database)
    {
        if (database == null)
        {
            Debug.LogError("[ItemDatabase] 갱신할 데이터베이스가 없습니다.");
            return;
        }

        List<ItemDataSO> assets = LoadAllItemAssets();
        foreach (ItemDataSO asset in assets)
            SetId(asset, 0);

        // 커서도 대역 시작으로 되돌린다. 안 그러면 새 번호가 옛 커서 뒤에서 시작한다.
        SerializedObject serialized = new(database);
        SerializedProperty ranges = serialized.FindProperty(RangesField);

        for (int i = 0; i < ranges.arraySize; i++)
        {
            SerializedProperty element = ranges.GetArrayElementAtIndex(i);
            element.FindPropertyRelative(RangeNextIdField).uintValue =
                element.FindPropertyRelative(RangeStartField).uintValue;
        }

        serialized.FindProperty(NextIdField).uintValue = 1;
        serialized.ApplyModifiedProperties();

        Rebuild(database);

        Debug.LogWarning($"[ItemDatabase] 아이템 {assets.Count}개의 Id를 전부 다시 부여했습니다. " +
                         "이전 Id를 들고 있던 세이브나 외부 데이터는 더 이상 맞지 않습니다.", database);
    }

    /// <summary>이미 Id가 있는 에셋. 번호는 그대로 두고 커서만 밀어준다.</summary>
    private static void KeepExistingId(
        ItemDataSO asset, int rangeIndex, IReadOnlyList<ItemIdRange> ranges,
        uint[] cursors, ref uint legacyNext, StringBuilder problems)
    {
        if (rangeIndex < 0)
        {
            if (asset.Id >= legacyNext)
                legacyNext = asset.Id + 1;

            return;
        }

        ItemIdRange range = ranges[rangeIndex];

        if (!range.Contains(asset.Id))
        {
            // 대역 밖의 Id는 옛 번호이거나 표를 고친 흔적이다.
            // 그냥 두면 동작은 하지만 번호로 종류를 알 수 없는 아이템이 남는다.
            problems.AppendLine(
                $"- {asset.name}: Id {asset.Id}가 '{range.DisplayName}' 대역({range.start}~{range.end}) 밖입니다.");
            return;
        }

        if (asset.Id >= cursors[rangeIndex])
            cursors[rangeIndex] = asset.Id + 1;
    }

    /// <summary>새 Id를 부여한다. 대역이 꽉 찼으면 부여하지 않고 false.</summary>
    private static bool TryAssignId(
        ItemDataSO asset, int rangeIndex, IReadOnlyList<ItemIdRange> ranges,
        uint[] cursors, HashSet<uint> usedIds, ref uint legacyNext, StringBuilder problems)
    {
        if (rangeIndex < 0)
        {
            // 대역 표에 없는 타입. ItemDataSO 대역이 있으면 여기까지 오지 않는다.
            problems.AppendLine(
                $"- {asset.name}: {asset.GetType().Name}에 맞는 대역이 없어 {legacyNext}번을 줬습니다. " +
                "대역 표에 추가하세요.");

            while (usedIds.Contains(legacyNext))
                legacyNext++;

            SetId(asset, legacyNext);
            usedIds.Add(legacyNext);
            legacyNext++;
            return true;
        }

        ItemIdRange range = ranges[rangeIndex];
        uint cursor = Math.Max(cursors[rangeIndex], range.start);

        while (cursor <= range.end && usedIds.Contains(cursor))
            cursor++;

        if (cursor > range.end)
        {
            // 조용히 옆 대역으로 넘어가면 번호 체계가 무너진다. 부여하지 않고 알린다.
            problems.AppendLine(
                $"- {asset.name}: '{range.DisplayName}' 대역({range.start}~{range.end})이 가득 차 Id를 주지 못했습니다.");

            cursors[rangeIndex] = cursor;
            return false;
        }

        SetId(asset, cursor);
        usedIds.Add(cursor);
        cursors[rangeIndex] = cursor + 1;
        return true;
    }

    /// <summary>
    /// Id마다 "지금 그 번호를 대표하는 에셋"을 고른다.
    ///
    /// 같은 Id를 가진 에셋이 여럿일 때 누구를 원본으로 볼지 정하는 기준이다.
    /// ItemDatabaseSO.Map과 같은 규칙(목록 순서상 첫 번째)을 쓴다 —
    /// 게임이 지금 실제로 해석하고 있는 쪽을 그대로 두는 것이 가장 덜 놀랍다.
    /// 파일 수정 시각 같은 건 git 체크아웃 한 번에 뒤바뀌므로 쓰지 않는다.
    /// </summary>
    public static Dictionary<uint, ItemDataSO> BuildOwners(ItemDatabaseSO database)
    {
        Dictionary<uint, ItemDataSO> owners = new();
        if (database == null) return owners;

        foreach (ItemDataSO item in database.All)
        {
            if (item == null || !item.IsRegistered) continue;

            owners.TryAdd(item.Id, item);
        }

        return owners;
    }

    private static bool IsOwner(Dictionary<uint, ItemDataSO> owners, ItemDataSO asset)
        => asset.IsRegistered && owners.TryGetValue(asset.Id, out ItemDataSO owner) && owner == asset;

    /// <summary>Id를 지워 다음 스캔에서 새로 받게 한다. 충돌 해결용.</summary>
    public static void ClearId(ItemDataSO asset)
    {
        if (asset != null)
            SetId(asset, 0);
    }

    private static void WriteRange(SerializedProperty element, in ItemIdRange range)
    {
        element.FindPropertyRelative("typeName").stringValue = range.typeName;
        element.FindPropertyRelative("label").stringValue = range.label;
        element.FindPropertyRelative(RangeStartField).uintValue = range.start;
        element.FindPropertyRelative("end").uintValue = range.end;
        element.FindPropertyRelative(RangeNextIdField).uintValue = range.nextId;
    }

    private static void SetId(ItemDataSO asset, uint id)
    {
        SerializedObject serialized = new(asset);
        serialized.FindProperty(IdField).uintValue = id;
        serialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(asset);
    }
}
