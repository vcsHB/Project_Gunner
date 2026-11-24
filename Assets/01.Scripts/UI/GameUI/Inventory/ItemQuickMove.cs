using System;

/// <summary>
/// Shift+클릭으로 "반대편"에 보내기. 반대편이 어디인지는 지금 떠 있는 화면만 안다 — 인벤토리 패널이 <see cref="Resolver"/>를 채운다.
/// 루팅 중이면 내 적재함 ↔ 잔해 적재함. 반대편이 없으면 아무 일도 없다.
/// </summary>
public static class ItemQuickMove
{
    /// <summary>(보내는 쪽) → 받는 적재함. null이면 보낼 곳이 없다.</summary>
    public static Func<IItemSlotContainer, Inventory> Resolver;

    /// <summary>받는 쪽이 꽉 차서 하나도 못 보냈다. (받는 적재함, 이유)</summary>
    public static event Action<Inventory, string> OnFailedEvent;

    public static bool TryMove(CellInventorySlot cell)
    {
        if (cell == null || cell.IsEmpty || Resolver == null) return false;

        Inventory target = Resolver(cell.Container);
        if (target == null) return false;

        if (ItemTransfer.QuickMove(cell.Container, cell.SlotKey, target, out string reason)) return true;

        OnFailedEvent?.Invoke(target, reason);
        return false;
    }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        Resolver = null;
        OnFailedEvent = null;
    }
}
