using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장착된 파츠의 <b>비주얼을 조립한다.</b> 코어를 만들고, 코어의 소켓에 추진체를 붙인다.
///
/// 기체는 전부 프리팹에서 나온다. <b>씬에 손으로 만들어 둔 몸을 쓰지 않는다</b> —
/// 그런 폴백을 두면 "코어를 바꿨는데 안 바뀌는" 상태와 "프리팹과 씬 중 뭐가 진짜인가"가 생긴다.
/// 코어가 없으면 몸도 없는 것이 맞다.
///
/// 책임 경계 — 조립기는 <b>뼈대</b>(무엇이 어디에 놓이나)를 만들고,
/// <see cref="PlayerHandController"/>는 <b>무장 오브젝트</b>를 만든다.
/// 팔은 동작이 붙어야 해서 무장 쪽이 만들고, 붙일 자리만 여기서 받아간다.
/// 둘 다 팔을 만들면 무기가 두 개 생긴다.
///
/// 스탯은 <see cref="MechaPartController"/>가 따로 본다. 비주얼과 스탯을 한 곳에서 하면
/// 프리팹이 없는 파츠에서 스탯까지 같이 빠진다.
/// </summary>
[RequireAgentComponent(typeof(EquipmentController))]
public class MechaAssembler : MonoBehaviour, IAgentComponent
{
    [Tooltip("조립된 기체가 들어갈 자리. 이 아래가 통째로 갈아끼워진다.")]
    [SerializeField] private Transform _bodyRoot;

    /// <summary>조립이 바뀌었을 때. 소켓·콜라이더를 캐시해 둔 쪽이 다시 물어봐야 한다.</summary>
    public event Action OnAssembledEvent;

    private readonly Dictionary<EquipSlotType, MechaPartView> _spawned = new();

    private Agent _owner;
    private EquipmentController _equipment;

    /// <summary>지금 붙어 있는 코어 비주얼. 코어를 안 달았으면 null.</summary>
    public MechaPartView Core => Get(EquipSlotType.Core);

    public void Initialize(Agent owner)
    {
        _owner = owner;

        if (_bodyRoot == null)
            _bodyRoot = transform;
    }

    public void AfterInitialize()
    {
        _equipment = _owner.GetCompo<EquipmentController>();

        if (_equipment == null)
        {
            Debug.LogError("[Assembler] EquipmentController가 없습니다.", this);
            return;
        }

        _equipment.OnEquipChangedEvent += HandleEquipChanged;

        SyncAll();
    }

    public void Dispose()
    {
        if (_equipment != null)
            _equipment.OnEquipChangedEvent -= HandleEquipChanged;

        foreach (MechaPartView view in _spawned.Values)
        {
            if (view != null)
                Destroy(view.gameObject);
        }

        _spawned.Clear();
    }

    private MechaPartView Get(EquipSlotType slot)
        => _spawned.TryGetValue(slot, out MechaPartView view) ? view : null;

    /// <summary>
    /// 이 슬롯이 달릴 자리. 코어에 그 소켓이 없으면 null —
    /// 몸통 한가운데에 팔을 달아두고 "왜 여기 붙었지"를 나중에 찾게 두지 않는다.
    /// </summary>
    public Transform GetMountPoint(EquipSlotType slot)
    {
        MechaPartView core = Core;

        return core != null ? core.GetSocket(slot) : null;
    }

    private void SyncAll()
    {
        // 코어를 먼저 세운다. 나머지는 코어 소켓에 붙으므로 순서가 뒤집히면 자리가 없다.
        Rebuild(EquipSlotType.Core, _equipment.GetStack(EquipSlotType.Core));

        foreach (EquipSlotType slot in Enum.GetValues(typeof(EquipSlotType)))
        {
            if (slot == EquipSlotType.None || slot == EquipSlotType.Core) continue;

            ItemStack stack = _equipment.GetStack(slot);
            if (stack.IsEmpty) continue;

            Rebuild(slot, stack);
        }

        RaiseAssembled();
    }

    private void HandleEquipChanged(EquipSlotType slot, ItemStack stack)
    {
        if (!Rebuild(slot, stack)) return;

        // 코어가 바뀌면 소켓이 통째로 갈린다. 거기 붙어 있던 것을 다시 세운다.
        if (slot == EquipSlotType.Core)
            RebuildAttachments();

        RaiseAssembled();
    }

    private void RebuildAttachments()
    {
        foreach (EquipSlotType slot in Enum.GetValues(typeof(EquipSlotType)))
        {
            if (slot == EquipSlotType.None || slot == EquipSlotType.Core) continue;

            ItemStack stack = _equipment.GetStack(slot);
            if (stack.IsEmpty) continue;

            Rebuild(slot, stack);
        }
    }

    /// <summary>비주얼이 실제로 바뀌었으면 true.</summary>
    private bool Rebuild(EquipSlotType slot, ItemStack stack)
    {
        // 팔은 무장 쪽이 만든다. 여기서 만들면 무기가 두 개 생긴다.
        if (slot == EquipSlotType.ArmLeft || slot == EquipSlotType.ArmRight) return false;

        PartDataSO part = stack.Resolve<PartDataSO>();
        MechaPartView prefab = part != null ? part.ViewPrefab : null;

        MechaPartView current = Get(slot);

        if (prefab == null && current == null) return false;

        if (current != null)
        {
            Destroy(current.gameObject);
            _spawned.Remove(slot);
        }

        if (prefab == null)
        {
            if (part != null)
            {
                Debug.LogWarning(
                    $"[Assembler] {part.DisplayName}에 비주얼 프리팹이 없습니다. 스탯만 적용됩니다.", part);
            }

            return true;
        }

        Transform parent = slot == EquipSlotType.Core ? _bodyRoot : GetMountPoint(slot);

        if (parent == null)
        {
            // 코어 자체가 없으면(해제·정리 중) 붙일 곳이 없는 게 정상이다. 코어는 있는데 소켓이 없을 때만 알린다.
            if (Core != null)
                Debug.LogWarning($"[Assembler] {slot}을 붙일 소켓이 코어에 없습니다.", this);

            return true;
        }

        MechaPartView view = Instantiate(prefab, parent);
        view.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        view.Bind(part);

        _spawned[slot] = view;

        return true;
    }

    /// <summary>
    /// 몸이 갈렸으면 몸에 걸려 있던 것들도 다시 봐야 한다.
    /// 히트박스가 코어 프리팹에 있으므로, 알려주지 않으면 피격 판정이 통째로 빠진다.
    /// </summary>
    private void RaiseAssembled()
    {
        _owner.GetCompo<TargetBase>()?.RefreshColliders();

        OnAssembledEvent?.Invoke();
    }
}
