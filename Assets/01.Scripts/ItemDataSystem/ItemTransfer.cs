using UnityEngine;

/// <summary>
/// 칸에서 칸으로 아이템을 옮긴다. 병합·스왑·분할 규칙이 전부 여기 한 곳에 있다.
///
/// 어느 단계에서 실패하든 원래 자리로 되돌린다. 드래그 도중 아이템이 사라지는 게 제일 나쁘다.
/// </summary>
public static class ItemTransfer
{
    /// <summary>실제로 옮기지 않고 가능한지만 본다. 호버 피드백용.</summary>
    public static bool CanMove(IItemSlotContainer from, int fromKey,
        IItemSlotContainer to, int toKey, int count, out string reason)
    {
        reason = string.Empty;

        if (from == null || to == null)
        {
            reason = "컨테이너가 없습니다.";
            return false;
        }

        if (ReferenceEquals(from, to) && fromKey == toKey) return false;

        ItemStack source = from.Peek(fromKey);
        if (source.IsEmpty)
        {
            reason = "빈 칸입니다.";
            return false;
        }

        count = Mathf.Clamp(count, 1, source.count);

        if (!to.CanAccept(toKey, source, out reason)) return false;

        ItemStack target = to.Peek(toKey);
        if (target.IsEmpty || target.CanMergeWith(source)) return true;

        // 다른 아이템이 있으면 자리를 바꿔야 하는데, 일부만 들고 있으면 남은 게 갈 곳이 없다.
        if (count < source.count)
        {
            reason = "나눠서 옮길 수 없는 자리입니다.";
            return false;
        }

        return from.CanAccept(fromKey, target, out reason);
    }

    /// <summary>
    /// count만큼 옮긴다. 전부 못 옮겨도 옮긴 게 있으면 true.
    /// </summary>
    public static bool Move(IItemSlotContainer from, int fromKey,
        IItemSlotContainer to, int toKey, int count)
    {
        if (!CanMove(from, fromKey, to, toKey, count, out _)) return false;

        ItemStack source = from.Peek(fromKey);
        count = Mathf.Clamp(count, 1, source.count);

        ItemStack target = to.Peek(toKey);
        bool needSwap = !target.IsEmpty && !target.CanMergeWith(source);

        ItemStack moving = from.Take(fromKey, count);
        if (moving.IsEmpty) return false;

        return needSwap
            ? Swap(from, fromKey, to, toKey, moving, target)
            : Merge(from, fromKey, to, toKey, moving);
    }

    /// <summary>전량 이동 + 자리 교환.</summary>
    private static bool Swap(IItemSlotContainer from, int fromKey,
        IItemSlotContainer to, int toKey, ItemStack moving, ItemStack target)
    {
        ItemStack swapped = to.Take(toKey, target.count);

        ItemStack rejected = to.Place(toKey, moving);
        if (!rejected.IsEmpty)
        {
            // 넣기 실패. 양쪽 다 원래대로 되돌린다.
            to.Place(toKey, swapped);
            from.Place(fromKey, rejected);
            return false;
        }

        ItemStack returned = from.Place(fromKey, swapped);
        if (returned.IsEmpty) return true;

        // CanMove에서 확인했으므로 정상적으로는 오지 않는다.
        Debug.LogError($"[ItemTransfer] 교환한 아이템을 되돌릴 자리가 없습니다. (남은 수량 {returned.count})");
        return true;
    }

    private static bool Merge(IItemSlotContainer from, int fromKey,
        IItemSlotContainer to, int toKey, ItemStack moving)
    {
        ItemStack leftover = to.Place(toKey, moving);
        if (leftover.IsEmpty) return true;

        // 칸이 꽉 차서 일부만 들어갔다. 나머지는 원래 자리로.
        ItemStack returned = from.Place(fromKey, leftover);
        if (!returned.IsEmpty)
            Debug.LogError($"[ItemTransfer] 남은 아이템을 되돌리지 못했습니다. (수량 {returned.count})");

        // 하나도 못 옮겼으면 실패로 본다.
        return leftover.count < moving.count;
    }

    /// <summary>우클릭 드래그로 반씩 떼는 기본 규칙.</summary>
    public static int GetHalfCount(ItemStack stack)
        => stack.IsEmpty ? 0 : Mathf.Max(1, Mathf.CeilToInt(stack.count * 0.5f));
}
