using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 어느 슬롯에 무엇이 끼워져 있는지만 관리한다.
/// 실제 오브젝트 생성(무기 프리팹 등)은 이벤트를 구독하는 쪽이 한다.
///
/// SO가 아니라 ItemStack을 들고 있는 이유는 개체 상태(instanceId)를 잃지 않기 위해서다.
/// </summary>
public class EquipmentController : MonoBehaviour, IAgentComponent
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

    public void UnequipAll()
    {
        // 순회 중에 컬렉션이 바뀌므로 슬롯 목록을 먼저 뜬다.
        EquipSlotType[] slots = new EquipSlotType[_equipped.Count];
        _equipped.Keys.CopyTo(slots, 0);

        for (int i = 0; i < slots.Length; i++)
            Unequip(slots[i]);
    }
}
