using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기체에 무엇이 달렸고, 그게 무슨 효과인가. <b>장비 슬롯이 실제 효과를 갖는 유일한 지점이다.</b>
///
/// 두 가지를 한 컴포넌트에 둔다 — 진입할 때 <b>채우고</b>, 그 변화를 스탯에 <b>반영한다.</b>
/// 나누지 않은 이유는 같은 이벤트의 생산자와 소비자이기 때문이다. 둘 다
/// <see cref="EquipmentController"/> 없이는 의미가 없고, 한쪽만 붙은 기체는 성립하지 않는다.
///
/// <b>회전은 여기 없다.</b> 그건 표현이고(<see cref="MechaRotationController"/>),
/// 회전만 필요한 유닛이 실제로 생긴다.
///
/// 비주얼(프리팹 생성)도 여기서 하지 않는다. 스탯과 프리팹 생성을 한 클래스가 들면,
/// 프리팹이 없는 파츠에서 스탯까지 같이 빠진다.
/// </summary>
[RequireAgentComponent(typeof(AgentStatus))]
[RequireAgentComponent(typeof(EquipmentController))]
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

    /// <summary>진입할 때 기체를 채우는 설정. 인스펙터에서 한 블록으로 접힌다.</summary>
    [Serializable]
    public class LoadoutSettings
    {
        [Tooltip("격납고에서 넘어온 구성이 없을 때 쓸 기본값. 디버그용이다.")]
        public MechaLoadoutSO defaultLoadout;

        [Tooltip("새 판으로 취급해 월드 상태를 되돌린다. " +
                 "이걸 켠 것이 씬에 둘 이상이면 서로의 아이템 인스턴스를 지운다.")]
        public bool resetWorldState = true;

        [Tooltip("켜면 매번 같은 시드로 시작한다. 재현이 필요한 테스트에 쓴다.")]
        public bool useFixedSeed;

        public int seed;
    }

    /// <summary>
    /// 격납고가 정한 구성. 있으면 에셋 기본값보다 우선한다.
    ///
    /// 정적인 것은 <b>임시다.</b> 로컬 1인 기준이라 지금은 문제가 없지만,
    /// 플레이어별 상태를 전역에 두면 서버 권한 구조로 못 옮긴다. (RULES.md 멀티플레이 대비)
    /// 플레이어 스폰 흐름이 생기면 스폰하는 쪽이 SetLoadout으로 넘기도록 바꿀 것.
    /// </summary>
    public static MechaLoadoutSO PendingLoadout { get; set; }

    [SerializeField] private LoadoutSettings _loadout = new();

    private readonly Dictionary<EquipSlotType, SlotOrigin> _origins = new();

    private Agent _owner;
    private AgentStatus _status;
    private EquipmentController _equipment;

    /// <summary>지금 달려 있는 코어. 조준점·조준 거리를 읽을 쪽이 쓴다. 없으면 null.</summary>
    public CoreDataSO Core => _equipment != null ? _equipment.Get<CoreDataSO>(EquipSlotType.Core) : null;

    /// <summary>지금 적용된 구성. 없으면 null.</summary>
    public MechaLoadoutSO AppliedLoadout { get; private set; }

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

    #region 스탯 반영

    private void SyncAll()
    {
        foreach (EquipSlotType slot in Enum.GetValues(typeof(EquipSlotType)))
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

    #endregion

    #region 진입할 때 채우기

    /// <summary>
    /// AfterInitialize가 아니라 Start에서 적용한다.
    /// 컴포넌트 초기화 순서는 보장되지 않아서, AfterInitialize에서 장착하면
    /// 아직 구독하지 않은 컴포넌트가 장착 이벤트를 통째로 놓친다.
    /// Start는 모든 Awake(= Agent의 초기화 전체)가 끝난 뒤다.
    /// </summary>
    private void Start()
    {
        ApplyLoadout(PendingLoadout != null ? PendingLoadout : _loadout.defaultLoadout);
    }

    public void SetLoadout(MechaLoadoutSO loadout) => ApplyLoadout(loadout);

    private void ApplyLoadout(MechaLoadoutSO loadout)
    {
        if (_equipment == null)
        {
            Debug.LogError("[Loadout] EquipmentController가 없어 구성을 적용할 수 없습니다.", this);
            return;
        }

        if (_loadout.resetWorldState)
            ResetWorldState();

        AppliedLoadout = loadout;
        if (loadout == null) return;

        IReadOnlyList<MechaLoadoutSO.Entry> entries = loadout.Entries;
        for (int i = 0; i < entries.Count; i++)
            Equip(entries[i], loadout);

        LoadCargo(loadout);
    }

    /// <summary>
    /// 적재함을 채운다. 장착보다 <b>나중</b>이어야 한다 —
    /// 적재 칸 수가 파츠 스탯(InventorySize)을 따라가므로, 먼저 실으면 칸이 모자란다.
    /// </summary>
    private void LoadCargo(MechaLoadoutSO loadout)
    {
        IReadOnlyList<MechaLoadoutSO.Cargo> cargo = loadout.CargoItems;
        if (cargo.Count == 0) return;

        InventoryController controller = _owner.GetCompo<InventoryController>();
        if (controller == null || controller.Inventory == null)
        {
            Debug.LogWarning("[Loadout] 적재할 인벤토리가 없습니다.", this);
            return;
        }

        for (int i = 0; i < cargo.Count; i++)
        {
            if (!cargo[i].IsValid) continue;

            Load(controller.Inventory, cargo[i], loadout);
        }
    }

    private void Load(Inventory inventory, MechaLoadoutSO.Cargo cargo, MechaLoadoutSO loadout)
    {
        // 개체 상태가 있는 아이템은 겹쳐지지 않으므로 한 칸에 하나씩 넣는다.
        if (cargo.item.RequiresInstance)
        {
            for (int i = 0; i < cargo.count; i++)
            {
                ItemStack stack = ItemInstanceRegistry.CreateStack(cargo.item);

                if (stack.IsEmpty || !inventory.AddStack(stack))
                {
                    Debug.LogWarning($"[Loadout] {cargo.item.DisplayName}을 다 싣지 못했습니다.", loadout);
                    return;
                }
            }

            return;
        }

        int left = inventory.Add(cargo.item.Id, cargo.count);

        if (left > 0)
            Debug.LogWarning($"[Loadout] 적재 칸이 모자라 {cargo.item.DisplayName} {left}개를 못 실었습니다.", loadout);
    }

    private void Equip(MechaLoadoutSO.Entry entry, MechaLoadoutSO loadout)
    {
        if (!entry.IsValid) return;

        if (!entry.item.CanEquipTo(entry.slot))
        {
            Debug.LogWarning(
                $"[Loadout] {entry.item.DisplayName}은 {entry.slot} 슬롯에 들어가지 않습니다.", loadout);
            return;
        }

        // 무기처럼 개체 상태가 있는 아이템은 인스턴스를 만들어야 모딩·내구도가 붙는다.
        // CreateStack이 RequiresInstance를 보고 알아서 갈라준다.
        ItemStack stack = ItemInstanceRegistry.CreateStack(entry.item);
        if (stack.IsEmpty)
        {
            Debug.LogWarning(
                $"[Loadout] {entry.item.DisplayName}을 만들지 못했습니다. " +
                "ItemDatabase에 등록되어 있는지 확인하세요.", loadout);
            return;
        }

        _equipment.Equip(entry.slot, stack);
    }

    /// <summary>
    /// 이전 판의 흔적을 지운다. ItemInstanceRegistry는 정적이라
    /// 플레이 모드를 나가도 살아남는다.
    ///
    /// <b>여기가 "새 판 시작" 지점이다.</b> 죽음·리스폰 흐름이 생기면 그쪽으로 옮길 것.
    /// </summary>
    private void ResetWorldState()
    {
        ItemInstanceRegistry.Clear();
        GameRandom.Reseed(_loadout.useFixedSeed ? _loadout.seed : Environment.TickCount);
    }

    #endregion
}
