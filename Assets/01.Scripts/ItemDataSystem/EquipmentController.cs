using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 어느 슬롯에 무엇이 끼워져 있는지만 관리한다.
/// 실제 오브젝트 생성(무기 프리팹 등)은 이벤트를 구독하는 쪽이 한다.
///
/// SO가 아니라 ItemStack을 들고 있는 이유는 개체 상태(instanceId)를 잃지 않기 위해서다.
///
/// <b>필드에서는 잠겨 있다</b>(<see cref="IsLocked"/>). 무장은 진입할 때 들고 간 것만 쓰고, 교체는 안전지대에서만 한다.
/// 잠금은 <b>UI 경로(<see cref="IItemSlotContainer"/>)만</b> 막는다 — 드래그로 빼거나 끼우지 못한다.
/// 코드가 부르는 <see cref="Equip(EquipSlotType, ItemStack)"/> / <see cref="Unequip"/>(출격 로드아웃, 죽음, 툴)는 그대로 통한다.
/// 격납고(M5)가 생기면 그 Panel이 열려 있는 동안 <see cref="SetLocked"/>(false).
/// </summary>
public class EquipmentController : MonoBehaviour, IAgentComponent, IItemSlotContainer
{
    /// <summary>(슬롯, 새 장비). 해제되면 두 번째가 ItemStack.Empty.</summary>
    public event Action<EquipSlotType, ItemStack> OnEquipChangedEvent;

    private readonly Dictionary<EquipSlotType, ItemStack> _equipped = new();

    public Agent Owner { get; private set; }

    /// <summary>드래그로 장비를 바꿀 수 없다. 필드 = true, 안전지대 = false.</summary>
    public bool IsLocked { get; private set; } = true;

    public event Action<bool> OnLockChangedEvent;

    public void SetLocked(bool locked)
    {
        if (IsLocked == locked) return;

        IsLocked = locked;
        OnLockChangedEvent?.Invoke(locked);
    }

    /// <summary>잠겨 있을 때 화면에 보일 이유.</summary>
    public static string LockedReason => Localization.Get("ui.equip.locked", "안전지대에서만 교체할 수 있습니다");

    public void Initialize(Agent owner)
    {
        Owner = owner;
    }

    public void AfterInitialize()
    {
    }

    public void Dispose()
    {
        UnequipAll();
    }

    public ItemStack GetStack(EquipSlotType slot)
        => _equipped.TryGetValue(slot, out ItemStack stack) ? stack : ItemStack.Empty;

    public EquipableItemDataSO Get(EquipSlotType slot) => GetStack(slot).Resolve<EquipableItemDataSO>();

    public T Get<T>(EquipSlotType slot) where T : EquipableItemDataSO => GetStack(slot).Resolve<T>();

    public bool IsEquipped(EquipSlotType slot) => !GetStack(slot).IsEmpty;

    public bool CanEquip(EquipSlotType slot, ItemStack stack)
    {
        EquipableItemDataSO item = stack.Resolve<EquipableItemDataSO>();

        return item != null && item.CanEquipTo(slot);
    }

    /// <summary>아이템이 허용하는 첫 번째 슬롯에 장착한다.</summary>
    public bool Equip(ItemStack stack)
    {
        EquipableItemDataSO item = stack.Resolve<EquipableItemDataSO>();

        return item != null && Equip(item.DefaultSlot, stack);
    }

    public bool Equip(EquipSlotType slot, ItemStack stack)
    {
        if (!CanEquip(slot, stack))
        {
            ItemDataSO data = stack.Resolve();
            string name = data != null ? data.DisplayName : $"Id {stack.itemId}";

            Debug.LogWarning($"[Equipment] {name}을 {slot} 슬롯에 장착할 수 없습니다.", this);
            return false;
        }

        ItemStack current = GetStack(slot);
        if (current.itemId == stack.itemId && current.instanceId == stack.instanceId) return true;

        _equipped[slot] = stack;
        OnEquipChangedEvent?.Invoke(slot, stack);
        return true;
    }

    public bool Unequip(EquipSlotType slot)
    {
        if (!_equipped.Remove(slot)) return false;

        OnEquipChangedEvent?.Invoke(slot, ItemStack.Empty);
        return true;
    }

    #region IItemSlotContainer

    // slotKey는 (int)EquipSlotType 이다.

    public ItemStack Peek(int slotKey) => GetStack((EquipSlotType)slotKey);

    public bool CanAccept(int slotKey, ItemStack stack, out string reason)
    {
        reason = string.Empty;

        if (IsLocked)
        {
            reason = LockedReason;
            return false;
        }

        EquipSlotType slot = (EquipSlotType)slotKey;
        if (slot == EquipSlotType.None)
        {
            reason = "없는 슬롯입니다.";
            return false;
        }

        EquipableItemDataSO item = stack.Resolve<EquipableItemDataSO>();
        if (item == null)
        {
            reason = "장착할 수 없는 아이템입니다.";
            return false;
        }

        if (!item.CanEquipTo(slot))
        {
            reason = $"{slot} 칸에 맞지 않습니다.";
            return false;
        }

        return true;
    }

    public bool CanTake(int slotKey, out string reason)
    {
        reason = IsLocked ? LockedReason : string.Empty;
        return !IsLocked;
    }

    /// <summary>장비는 항상 통째로 빠진다. count는 무시한다. 잠겨 있으면 Empty.</summary>
    public ItemStack Take(int slotKey, int count)
    {
        if (IsLocked) return ItemStack.Empty;

        EquipSlotType slot = (EquipSlotType)slotKey;

        ItemStack stack = GetStack(slot);
        if (stack.IsEmpty) return ItemStack.Empty;

        Unequip(slot);
        return stack;
    }

    public ItemStack Place(int slotKey, ItemStack stack)
    {
        if (!CanAccept(slotKey, stack, out _)) return stack;

        EquipSlotType slot = (EquipSlotType)slotKey;

        // 이미 차 있으면 여기서 밀어내지 않는다. 교환은 ItemTransfer가 순서를 맞춰서 처리한다.
        if (!GetStack(slot).IsEmpty) return stack;

        return Equip(slot, stack) ? ItemStack.Empty : stack;
    }

    #endregion

    public void UnequipAll()
    {
        // 순회 중에 컬렉션이 바뀌므로 슬롯 목록을 먼저 뜬다.
        EquipSlotType[] slots = new EquipSlotType[_equipped.Count];
        _equipped.Keys.CopyTo(slots, 0);

        for (int i = 0; i < slots.Length; i++)
            Unequip(slots[i]);
    }
}
