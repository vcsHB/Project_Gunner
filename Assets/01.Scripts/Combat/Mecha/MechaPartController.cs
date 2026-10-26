using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장착한 파츠를 기체 스탯에 반영한다. <b>장비 슬롯이 실제 효과를 갖는 유일한 지점이다.</b>
///
/// <see cref="EquipmentController"/>는 "어느 슬롯에 무엇이 끼워져 있는가"만 알고,
/// 그게 무슨 의미인지는 구독자가 정한다. 여기가 그 구독자다.
///
/// 비주얼(프리팹 생성)은 여기서 하지 않는다. 스탯과 비주얼을 한 클래스가 들면,
/// 프리팹이 없는 파츠에서 스탯까지 같이 빠진다.
/// </summary>
public class MechaPartController : MonoBehaviour, IAgentComponent
{
    /// <summary>
    /// modifier의 origin. <b>슬롯마다 다른 객체여야 한다.</b>
    ///
    /// PartDataSO 자신을 origin으로 쓰면 같은 파츠를 두 슬롯에 달았을 때
    /// 한쪽을 빼는 순간 양쪽 modifier가 같이 걷힌다. origin은 참조 동일성으로 비교되기 때문이다.
    /// </summary>
    private sealed class SlotOrigin
    {
        private readonly EquipSlotType _slot;

        public SlotOrigin(EquipSlotType slot) => _slot = slot;

        public override string ToString() => $"MechaPart:{_slot}";
    }

    private readonly Dictionary<EquipSlotType, SlotOrigin> _origins = new();

    private Agent _owner;
    private AgentStatus _status;
    private EquipmentController _equipment;

    /// <summary>지금 달려 있는 코어. 조준점·조준 거리를 읽을 쪽이 쓴다. 없으면 null.</summary>
    public CoreDataSO Core => _equipment != null ? _equipment.Get<CoreDataSO>(EquipSlotType.Core) : null;

    public void Initialize(Agent owner)
    {
        _owner = owner;
    }

    public void AfterInitialize()
    {
        _status = _owner.GetCompo<AgentStatus>();
        _equipment = _owner.GetCompo<EquipmentController>();

        if (_status == null || _equipment == null)
        {
            Debug.LogError("[MechaPart] AgentStatus 또는 EquipmentController가 없습니다.", this);
            return;
        }

        _equipment.OnEquipChangedEvent += HandleEquipChanged;

        // 컴포넌트 초기화 순서는 보장되지 않는다. 구독보다 먼저 장착된 것이 있으면
        // 이벤트를 놓치므로 현재 상태를 한 번 훑는다.
        SyncAll();
    }

    public void Dispose()
    {
        if (_equipment != null)
            _equipment.OnEquipChangedEvent -= HandleEquipChanged;

        if (_status != null)
        {
            foreach (SlotOrigin origin in _origins.Values)
                _status.RemoveModifiers(origin);
        }

        _origins.Clear();
    }

    private void SyncAll()
    {
        foreach (EquipSlotType slot in System.Enum.GetValues(typeof(EquipSlotType)))
        {
            if (slot == EquipSlotType.None) continue;

            ItemStack stack = _equipment.GetStack(slot);
            if (stack.IsEmpty) continue;

            HandleEquipChanged(slot, stack);
        }
    }

    private void HandleEquipChanged(EquipSlotType slot, ItemStack stack)
    {
        SlotOrigin origin = GetOrigin(slot);

        // 해제든 교체든 이전 것을 먼저 걷어낸다. 교체에서 빼먹으면 조용히 중첩된다.
        _status.RemoveModifiers(origin);

        PartDataSO part = stack.Resolve<PartDataSO>();
        if (part == null) return;

        IReadOnlyList<AgentStatDelta> deltas = part.Deltas;
        for (int i = 0; i < deltas.Count; i++)
            ApplyDelta(deltas[i], origin, part);
    }

    private SlotOrigin GetOrigin(EquipSlotType slot)
    {
        if (_origins.TryGetValue(slot, out SlotOrigin origin)) return origin;

        origin = new SlotOrigin(slot);
        _origins[slot] = origin;

        return origin;
    }

    private void ApplyDelta(AgentStatDelta delta, SlotOrigin origin, PartDataSO part)
    {
        if (!delta.IsValid) return;

        StatusBase status = _status.GetStatus(delta.type);

        switch (status)
        {
            case Status<float> floatStatus:
                floatStatus.AddModifier(origin, delta.value, delta.mode);
                break;

            case Status<int> intStatus:
                // Status<int>의 modifier 값이 int라서 비율이 0으로 잘린다. 조용히 틀리느니 거부한다.
                if (delta.mode == ModifierMode.Percent)
                {
                    Debug.LogWarning(
                        $"[MechaPart] {part.DisplayName}의 {delta.type}에 Percent를 쓸 수 없습니다. " +
                        "정수 스탯이라 비율이 잘립니다. Additive로 주세요.", part);
                    return;
                }

                intStatus.AddModifier(origin, Mathf.RoundToInt(delta.value), delta.mode);
                break;

            default:
                Debug.LogWarning(
                    $"[MechaPart] {part.DisplayName}의 {delta.type}은 수치 스탯이 아니라 적용할 수 없습니다.", part);
                break;
        }
    }
}
