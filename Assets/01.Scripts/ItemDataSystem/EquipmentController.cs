using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 어느 슬롯에 무엇이 끼워져 있는지만 관리한다.
/// 실제 오브젝트 생성(무기 프리팹 등)은 이벤트를 구독하는 쪽이 한다.
///
/// SO가 아니라 ItemStack을 들고 있는 이유는 개체 상태(instanceId)를 잃지 않기 위해서다.
/// </summary>
public class EquipmentController : MonoBehaviour, IAgentComponent, IItemSlotContainer
{
    /// <summary>(슬롯, 새 장비). 해제되면 두 번째가 ItemStack.Empty.</summary>
    public event Action<EquipSlotType, ItemStack> OnEquipChangedEvent;

    private readonly Dictionary<EquipSlotType, ItemStack> _equipped = new();

    public Agent Owner { get; private set; }

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

    /// <summary>장비는 항상 통째로 빠진다. count는 무시한다.</summary>
    public ItemStack Take(int slotKey, int count)
    {
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
