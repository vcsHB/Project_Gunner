using UnityEngine;

/// <summary>
/// 칸에서 꺼내 바닥에 떨군다. 드래그로 패널 밖에 놓을 때, 우클릭 메뉴의 "버리기", <see cref="ItemDropArea"/>가 모두 여기를 쓴다 —
/// 버리는 경로가 갈라지면 한쪽만 실패 시 되돌리기를 빠뜨린다.
/// </summary>
public static class ItemDiscard
{
    /// <summary>
    /// count만큼 꺼내 <paramref name="dropper"/> 발밑에 월드 아이템으로 떨군다. 스폰이 실패하면 원래 칸으로 되돌린다.
    /// 못 꺼내면(필드의 장비 칸 등) false.
    ///
    /// 버린 사람은 줍기 반경을 한 번 벗어나야 다시 주울 수 있다(<see cref="WorldItem.IgnorePickupBy"/>) — 안 그러면 바로 다시 주워진다.
    /// </summary>
    public static bool Discard(IItemSlotContainer container, int slotKey, int count, Agent dropper)
    {
        if (container == null || count <= 0 || dropper == null) return false;
        if (!container.CanTake(slotKey, out _)) return false;

        ItemStack taken = container.Take(slotKey, count);
        if (taken.IsEmpty) return false;

        WorldItem item = WorldItemSpawner.Spawn(taken, dropper.transform.position);
        if (item != null)
        {
            InventoryController picker = dropper.GetCompo<InventoryController>();
            if (picker != null)
                item.IgnorePickupBy(picker.transform, picker.PickupRadius + 0.3f);

            return true;
        }

        // 꺼낸 다음에 스폰이 실패하면 아이템이 사라진다.
        container.Place(slotKey, taken);
        return false;
    }
}
