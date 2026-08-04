using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 프로젝트의 ItemDataSO를 모아 ItemDatabase를 갱신한다.
/// 인스펙터와 Item Creator 창이 함께 쓴다.
/// </summary>
public static class ItemDatabaseBuilder
{
    private const string ItemsField = "_items";
    private const string NextIdField = "_nextId";
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
        HashSet<ItemDataSO> known = new(database.All);

        // 이미 등록되어 있던 것이 자기 Id를 먼저 차지하게 한다.
        // 에셋을 Ctrl+D로 복제하면 Id까지 복사되는데, 이 순서면 원본이 Id를 지키고 복제본이 새로 받는다.
        assets.Sort((a, b) =>
        {
            int aRegistered = known.Contains(a) ? 0 : 1;
            int bRegistered = known.Contains(b) ? 0 : 1;

            if (aRegistered != bRegistered) return aRegistered - bRegistered;

            return string.CompareOrdinal(AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b));
        });

        uint nextId = database.NextId == 0 ? 1u : database.NextId;
        HashSet<uint> usedIds = new();
        int assigned = 0;

        foreach (ItemDataSO asset in assets)
        {
            if (asset.IsRegistered && usedIds.Add(asset.Id))
            {
                if (asset.Id >= nextId)
                    nextId = asset.Id + 1;

                continue;
            }

            // Id가 없거나(0), 다른 에셋이 이미 쓰고 있는 Id다.
            SetId(asset, nextId);
            usedIds.Add(nextId);
            nextId++;
            assigned++;
        }

        // 목록은 Id 순으로 정렬해서 diff가 안정적이게 한다.
        assets.Sort((a, b) => a.Id.CompareTo(b.Id));

        SerializedObject serialized = new(database);

        SerializedProperty items = serialized.FindProperty(ItemsField);
        items.arraySize = assets.Count;
        for (int i = 0; i < assets.Count; i++)
            items.GetArrayElementAtIndex(i).objectReferenceValue = assets[i];

        serialized.FindProperty(NextIdField).uintValue = nextId;
        serialized.ApplyModifiedProperties();

        AssetDatabase.SaveAssets();
        Debug.Log($"[ItemDatabase] 아이템 {assets.Count}개를 등록했습니다. (새 Id {assigned}개 부여)", database);
    }

    private static void SetId(ItemDataSO asset, uint id)
    {
        SerializedObject serialized = new(asset);
        serialized.FindProperty(IdField).uintValue = id;
        serialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(asset);
    }
}
