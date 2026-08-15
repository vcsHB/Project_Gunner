using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 아이템 SO가 새로 들어올 때 Id가 이미 쓰이고 있는지 본다.
///
/// Ctrl+D 복제는 어떤 코드도 거치지 않아서, 이게 없으면 Id가 겹친 걸
/// 한참 뒤에 "인벤토리가 엉뚱한 아이템을 꺼낸다"로 알게 된다.
///
/// <b>고치지는 않는다.</b> 임포트 중에 에셋을 말없이 바꾸면 대량 리임포트 때
/// 무슨 일이 벌어졌는지 알 수 없게 된다. 알리기만 하고 정리는 사람이 누른다.
/// </summary>
public class ItemIdConflictWatcher : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets, string[] deletedAssets,
        string[] movedAssets, string[] movedFromAssetPaths)
    {
        if (importedAssets.Length == 0) return;

        List<ItemDataSO> imported = null;

        for (int i = 0; i < importedAssets.Length; i++)
        {
            if (!importedAssets[i].EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase)) continue;

            ItemDataSO item = AssetDatabase.LoadAssetAtPath<ItemDataSO>(importedAssets[i]);

            // Id가 0이면 아직 안 받은 것이라 겹칠 수가 없다.
            if (item == null || !item.IsRegistered) continue;

            imported ??= new List<ItemDataSO>();
            imported.Add(item);
        }

        if (imported == null) return;

        ItemDatabaseSO database = ItemDatabaseBuilder.FindDatabase();
        if (database == null) return;

        WarnIfConflicting(database, imported);
    }

    private static void WarnIfConflicting(ItemDatabaseSO database, List<ItemDataSO> imported)
    {
        Dictionary<uint, ItemDataSO> owners = ItemDatabaseBuilder.BuildOwners(database);

        for (int i = 0; i < imported.Count; i++)
        {
            ItemDataSO item = imported[i];

            // 그 번호의 주인이 자기 자신이면 그냥 저장된 것뿐이다.
            if (!owners.TryGetValue(item.Id, out ItemDataSO owner) || owner == item) continue;

            Debug.LogWarning(
                $"[ItemDatabase] '{item.name}'이 '{owner.name}'과 같은 Id {item.Id}를 쓰고 있습니다. " +
                "복제한 에셋은 Id까지 복사됩니다. ItemDatabase 인스펙터에서 'Id 충돌 해결'을 누르세요.",
                item);
        }
    }
}
