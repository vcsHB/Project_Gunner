using UnityEngine;

/// <summary>
/// 분해. 칸에서 하나를 꺼내 그 재료(<see cref="ItemDataSO.Composition"/>)를 같은 적재함에 넣는다.
///
/// 적재함이 모자라면 <b>넘친 만큼 발밑에 떨군다</b> — 분해를 막지 않는다. 버린 것과 같이 그 사람은 한 번 멀어져야 다시 줍는다.
/// 먼저 빈자리를 계산해 막는 방식은 재료가 여러 종류면 "몇 칸이 필요한가"가 겹침에 따라 달라져 틀리기 쉽다.
/// </summary>
public static class ItemDisassembly
{
    public static bool TryDisassemble(Inventory inventory, int slot, Agent owner)
    {
        if (inventory == null) return false;

        ItemStack stack = inventory[slot];
        ItemDataSO data = stack.Resolve();
        if (data == null || !data.CanDisassemble) return false;

        if (!inventory.RemoveAt(slot, 1)) return false;

        var composition = data.Composition;
        for (int i = 0; i < composition.Count; i++)
        {
            ItemAmount part = composition[i];
            if (!part.IsValid) continue;

            int left = inventory.Add(part.item.Id, part.count);
            if (left <= 0) continue;

            if (owner == null)
            {
                Debug.LogWarning($"[Disassembly] {part.item.EditorName} {left}개를 둘 곳이 없어 잃었습니다.");
                continue;
            }

            WorldItem dropped = WorldItemSpawner.Spawn(new ItemStack(part.item.Id, left), owner.transform.position);
            InventoryController picker = owner.GetCompo<InventoryController>();

            if (dropped != null && picker != null)
                dropped.IgnorePickupBy(picker.transform, picker.PickupRadius + 0.3f);
        }

        return true;
    }
}
