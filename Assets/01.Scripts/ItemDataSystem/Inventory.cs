using System;
using UnityEngine;

/// <summary>
/// 칸 기반 인벤토리. MonoBehaviour가 아니라 순수 로직이라 테스트와 저장이 쉽다.
/// UI는 OnSlotChangedEvent만 구독하면 된다.
/// </summary>
public class Inventory : IItemSlotContainer
{
    public event Action<int> OnSlotChangedEvent;

    private readonly ItemStack[] _slots;

    public Inventory(int capacity)
    {
        _slots = new ItemStack[Mathf.Max(1, capacity)];
    }

    public int Capacity => _slots.Length;

    public ItemStack this[int index] => IsValidIndex(index) ? _slots[index] : ItemStack.Empty;

    /// <summary>
    /// 개체 상태를 가진 아이템을 넣는다. 겹칠 수 없으므로 빈 칸이 하나 필요하다.
    /// 인스턴스가 없는 스택을 넘기면 Add로 넘긴다.
    /// </summary>
    public bool AddStack(ItemStack stack)
    {
        if (stack.IsEmpty) return false;

        if (!stack.HasInstance)
            return Add(stack.itemId, stack.count) == 0;

        for (int i = 0; i < _slots.Length; i++)
        {
            if (!_slots[i].IsEmpty) continue;

            _slots[i] = new ItemStack(stack.itemId, 1, stack.instanceId);
            OnSlotChangedEvent?.Invoke(i);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 넣을 수 있는 만큼 넣고 <b>넣지 못하고 남은 개수</b>를 반환한다.
    /// 0이면 전부 들어간 것이다.
    /// </summary>
    public int Add(uint itemId, int count)
    {
        if (itemId == 0 || count <= 0) return count;

        // 개체 상태가 있는 아이템은 인스턴스를 잃어버리게 되므로 이 경로로 들어오면 안 된다.
        if (RequiresInstance(itemId))
        {
            Debug.LogError($"[Inventory] Id {itemId}는 개체 상태를 갖는 아이템입니다. AddStack을 쓰세요.");
            return count;
        }

        int maxStack = GetMaxStack(itemId);

        // 이미 있는 칸부터 채워야 칸이 헤프게 소모되지 않는다.
        for (int i = 0; i < _slots.Length && count > 0; i++)
        {
            if (_slots[i].itemId != itemId || _slots[i].count >= maxStack) continue;

            int space = maxStack - _slots[i].count;
            int moved = Mathf.Min(space, count);

            _slots[i].count += moved;
            count -= moved;
            OnSlotChangedEvent?.Invoke(i);
        }

        for (int i = 0; i < _slots.Length && count > 0; i++)
        {
            if (!_slots[i].IsEmpty) continue;

            int moved = Mathf.Min(maxStack, count);

            _slots[i] = new ItemStack(itemId, moved);
            count -= moved;
            OnSlotChangedEvent?.Invoke(i);
        }

        return count;
    }

    /// <summary>제거하지 못하고 남은 개수를 반환한다. 0이면 전부 제거된 것이다.</summary>
    public int Remove(uint itemId, int count)
    {
        if (itemId == 0 || count <= 0) return count;

        // 뒤에서부터 빼야 앞쪽 칸의 배치가 덜 흔들린다.
        for (int i = _slots.Length - 1; i >= 0 && count > 0; i--)
        {
            if (_slots[i].itemId != itemId) continue;

            int moved = Mathf.Min(_slots[i].count, count);

            _slots[i].count -= moved;
            count -= moved;

            if (_slots[i].count <= 0)
                _slots[i] = ItemStack.Empty;

            OnSlotChangedEvent?.Invoke(i);
        }

        return count;
    }

    public bool RemoveAt(int index, int count)
    {
        if (!IsValidIndex(index) || _slots[index].IsEmpty || count <= 0) return false;
        if (_slots[index].count < count) return false;

        _slots[index].count -= count;
        if (_slots[index].count <= 0)
            _slots[index] = ItemStack.Empty;

        OnSlotChangedEvent?.Invoke(index);
        return true;
    }

    public bool SetAt(int index, ItemStack stack)
    {
        if (!IsValidIndex(index)) return false;

        _slots[index] = stack.count <= 0 ? ItemStack.Empty : stack;
        OnSlotChangedEvent?.Invoke(index);
        return true;
    }

    public int CountOf(uint itemId)
    {
        int total = 0;
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].itemId == itemId)
                total += _slots[i].count;
        }

        return total;
    }

    public bool Has(uint itemId, int count = 1) => CountOf(itemId) >= count;

    /// <summary>넣을 여유가 있는지. 실제로 넣지는 않는다.</summary>
    public bool CanAdd(uint itemId, int count)
    {
        if (itemId == 0 || count <= 0) return false;

        int maxStack = GetMaxStack(itemId);
        int space = 0;

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].IsEmpty)
                space += maxStack;
            else if (_slots[i].itemId == itemId)
                space += maxStack - _slots[i].count;

            if (space >= count) return true;
        }

        return false;
    }

    public bool Swap(int a, int b)
    {
        if (!IsValidIndex(a) || !IsValidIndex(b) || a == b) return false;

        (_slots[a], _slots[b]) = (_slots[b], _slots[a]);

        OnSlotChangedEvent?.Invoke(a);
        OnSlotChangedEvent?.Invoke(b);
        return true;
    }

    public void Clear()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].IsEmpty) continue;

            _slots[i] = ItemStack.Empty;
            OnSlotChangedEvent?.Invoke(i);
        }
    }

    #region IItemSlotContainer

    public ItemStack Peek(int slotKey) => this[slotKey];

    public bool CanAccept(int slotKey, ItemStack stack, out string reason)
    {
        reason = string.Empty;

        if (!IsValidIndex(slotKey))
        {
            reason = "없는 칸입니다.";
            return false;
        }

        if (stack.IsEmpty)
        {
            reason = "빈 아이템입니다.";
            return false;
        }

        // 일반 인벤토리 칸은 종류를 가리지 않는다.
        return true;
    }

    public ItemStack Take(int slotKey, int count)
    {
        if (!IsValidIndex(slotKey) || count <= 0) return ItemStack.Empty;

        ItemStack slot = _slots[slotKey];
        if (slot.IsEmpty) return ItemStack.Empty;

        // 개체 상태가 있는 아이템은 쪼갤 수 없다. 인스턴스가 하나뿐이기 때문이다.
        count = slot.HasInstance ? slot.count : Mathf.Clamp(count, 1, slot.count);

        ItemStack taken = new(slot.itemId, count, slot.instanceId);

        slot.count -= count;
        _slots[slotKey] = slot.count <= 0 ? ItemStack.Empty : slot;
        OnSlotChangedEvent?.Invoke(slotKey);

        return taken;
    }

    public ItemStack Place(int slotKey, ItemStack stack)
    {
        if (!IsValidIndex(slotKey) || stack.IsEmpty) return stack;

        ItemStack slot = _slots[slotKey];
        int maxStack = GetMaxStack(stack.itemId);

        if (slot.IsEmpty)
        {
            int moved = stack.HasInstance ? 1 : Mathf.Min(maxStack, stack.count);

            _slots[slotKey] = new ItemStack(stack.itemId, moved, stack.instanceId);
            OnSlotChangedEvent?.Invoke(slotKey);

            stack.count -= moved;
            return stack.count <= 0 ? ItemStack.Empty : stack;
        }

        // 다른 아이템이거나 개체 아이템끼리면 겹칠 수 없다.
        if (!slot.CanMergeWith(stack)) return stack;

        int space = maxStack - slot.count;
        if (space <= 0) return stack;

        int merged = Mathf.Min(space, stack.count);

        slot.count += merged;
        _slots[slotKey] = slot;
        OnSlotChangedEvent?.Invoke(slotKey);

        stack.count -= merged;
        return stack.count <= 0 ? ItemStack.Empty : stack;
    }

    #endregion

    private bool IsValidIndex(int index) => index >= 0 && index < _slots.Length;

    // 등록되지 않은 아이템은 겹치지 않는 것으로 본다.
    private static int GetMaxStack(uint itemId)
    {
        if (ItemDatabaseSO.Instance == null) return 1;

        return ItemDatabaseSO.Instance.TryGet(itemId, out ItemDataSO data) ? data.MaxStackCount : 1;
    }

    private static bool RequiresInstance(uint itemId)
    {
        if (ItemDatabaseSO.Instance == null) return false;

        return ItemDatabaseSO.Instance.TryGet(itemId, out ItemDataSO data) && data.RequiresInstance;
    }
}
