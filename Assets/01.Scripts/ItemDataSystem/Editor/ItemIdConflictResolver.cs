using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ctrl+D로 복제한 SO의 Id 충돌을 찾아서 정리한다.
///
/// 아이템을 만드는 가장 흔한 방법이 기존 것을 복제하는 것이고,
/// 복제하면 <b>Id까지 그대로 복사됩니다.</b> 그대로 두면 ItemDatabase가
/// 한 Id에 여러 에셋을 물고 있게 되어, 인벤토리가 엉뚱한 아이템을 꺼냅니다.
///
/// 스캔만으로도 Rebuild가 알아서 정리하지만, 그것만으로는
/// <b>무엇이 어떻게 바뀌었는지 안 보입니다.</b> 여기는 그걸 먼저 보여주기 위한 것입니다.
/// </summary>
public static class ItemIdConflictResolver
{
    /// <summary>같은 Id를 쓰고 있는 에셋 묶음.</summary>
    public sealed class Conflict
    {
        public uint Id;

        /// <summary>이 Id를 그대로 지킬 에셋. 게임이 지금 그 번호로 해석하는 쪽이다.</summary>
        public ItemDataSO Keeper;

        /// <summary>새 Id를 받아야 할 에셋들.</summary>
        public readonly List<ItemDataSO> Duplicates = new();
    }

    /// <summary>Id가 겹치는 묶음을 찾는다. 없으면 빈 목록.</summary>
    public static List<Conflict> Scan(ItemDatabaseSO database)
    {
        List<Conflict> result = new();

        List<ItemDataSO> assets = ItemDatabaseBuilder.LoadAllItemAssets();
        Dictionary<uint, List<ItemDataSO>> byId = new();

        foreach (ItemDataSO asset in assets)
        {
            // Id가 없는 것은 충돌이 아니라 그냥 아직 안 받은 것이다.
            if (asset == null || !asset.IsRegistered) continue;

            if (!byId.TryGetValue(asset.Id, out List<ItemDataSO> group))
            {
                group = new List<ItemDataSO>();
                byId.Add(asset.Id, group);
            }

            group.Add(asset);
        }

        Dictionary<uint, ItemDataSO> owners = ItemDatabaseBuilder.BuildOwners(database);

        foreach (KeyValuePair<uint, List<ItemDataSO>> pair in byId)
        {
            if (pair.Value.Count < 2) continue;

            // 경로 순으로 고정해야 볼 때마다 순서가 바뀌지 않는다.
            pair.Value.Sort((a, b) =>
                string.CompareOrdinal(AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b)));

            Conflict conflict = new() { Id = pair.Key };

            conflict.Keeper = owners.TryGetValue(pair.Key, out ItemDataSO owner) && pair.Value.Contains(owner)
                ? owner
                : pair.Value[0];

            foreach (ItemDataSO asset in pair.Value)
            {
                if (asset != conflict.Keeper)
                    conflict.Duplicates.Add(asset);
            }

            result.Add(conflict);
        }

        result.Sort((a, b) => a.Id.CompareTo(b.Id));
        return result;
    }

    /// <summary>
    /// 충돌을 정리한다. 원본은 Id를 지키고 복제본만 자기 타입 대역에서 새 Id를 받는다.
    /// 바뀐 에셋 수를 반환한다.
    /// </summary>
    public static int Resolve(ItemDatabaseSO database)
    {
        if (database == null)
        {
            Debug.LogError("[ItemDatabase] 갱신할 데이터베이스가 없습니다.");
            return 0;
        }

        List<Conflict> conflicts = Scan(database);
        if (conflicts.Count == 0)
        {
            Debug.Log("[ItemDatabase] 겹치는 Id가 없습니다.", database);
            return 0;
        }

        int changed = 0;

        foreach (Conflict conflict in conflicts)
        {
            foreach (ItemDataSO duplicate in conflict.Duplicates)
            {
                // 0으로 비워두면 Rebuild가 타입 대역에서 새 번호를 뽑아준다.
                // 여기서 직접 번호를 고르면 대역 배정 규칙이 두 곳으로 갈린다.
                ItemDatabaseBuilder.ClearId(duplicate);
                changed++;
            }
        }

        ItemDatabaseBuilder.Rebuild(database);

        Debug.Log($"[ItemDatabase] Id 충돌 {conflicts.Count}건을 정리했습니다.\n{BuildReport(conflicts)}", database);
        return changed;
    }

    /// <summary>정리 결과를 사람이 읽을 수 있게. Rebuild 뒤에 부르면 새 Id가 찍힌다.</summary>
    private static string BuildReport(List<Conflict> conflicts)
    {
        StringBuilder report = new();

        foreach (Conflict conflict in conflicts)
        {
            report.AppendLine($"- Id {conflict.Id}: {NameOf(conflict.Keeper)} 유지");

            foreach (ItemDataSO duplicate in conflict.Duplicates)
                report.AppendLine($"    {NameOf(duplicate)} → {duplicate.Id}");
        }

        return report.ToString().TrimEnd();
    }

    private static string NameOf(ItemDataSO item) => item != null ? item.name : "(없음)";
}
