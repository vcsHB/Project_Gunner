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

    [Tooltip("기체에 부위로 붙었을 때의 체력(팔·추진체). 0이면 부서지지 않는 부위다 — 맞은 피해는 본체로 간다. " +
             "코어는 이 값을 쓰지 않는다 — 코어 체력은 AgentStatus.Health(Deltas)다.")]
    [SerializeField, Min(0f)] private float _partHealth;

    public IReadOnlyList<EquipSlotType> AllowedSlots => _allowedSlots;

    /// <summary>부위 체력. 판마다 가득 찬 채로 시작한다 — 아이템 내구도(<see cref="ItemInstance"/>)와는 별개다.</summary>
    public float PartHealth => _partHealth;

    public bool CanEquipTo(EquipSlotType slot)
        => slot != EquipSlotType.None && _allowedSlots.Contains(slot);

    /// <summary>지정 없이 장착할 때 쓸 기본 슬롯.</summary>
    public EquipSlotType DefaultSlot
        => _allowedSlots.Count > 0 ? _allowedSlots[0] : EquipSlotType.None;
}
