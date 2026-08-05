using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장착할 수 있는 아이템의 베이스. 무기·도구·방어구가 여기서 파생된다.
/// EquipmentController는 이 타입만 알면 되므로, 새 장비 종류가 생겨도 컨트롤러를 고치지 않는다.
/// </summary>
public abstract class EquipableItemDataSO : ItemDataSO
{
    [Header("Equip")]
    [Tooltip("들어갈 수 있는 슬롯. 여러 개면 어느 쪽에든 장착된다.")]
    [SerializeField] private List<EquipSlotType> _allowedSlots = new();

    public IReadOnlyList<EquipSlotType> AllowedSlots => _allowedSlots;

    public bool CanEquipTo(EquipSlotType slot)
        => slot != EquipSlotType.None && _allowedSlots.Contains(slot);

    /// <summary>지정 없이 장착할 때 쓸 기본 슬롯.</summary>
    public EquipSlotType DefaultSlot
        => _allowedSlots.Count > 0 ? _allowedSlots[0] : EquipSlotType.None;
}
