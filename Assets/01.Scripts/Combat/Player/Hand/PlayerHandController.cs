using System;
using UnityEngine;

/// <summary>
/// 좌우 팔에 달린 것을 만들고, 입력을 <b>슬롯별로 고정 배분</b>한다.
///
/// 좌클릭은 왼팔, 우클릭은 오른팔. 이 배분표는 여기에만 있다.
/// 구독하는 쪽이 각자 "내가 나설 차례인가"를 판단하면 조건이 흩어지고,
/// 결국 둘 다 반응하거나 아무도 반응하지 않는 상태가 생긴다.
///
/// <b>컨트롤러는 하나다.</b> 팔마다 컴포넌트를 붙이면 Agent가 타입당 하나만 등록하므로
/// (<see cref="Agent"/>) 한쪽이 통째로 조회되지 않는다. 손을 안에서 배열로 들고 간다.
///
/// 공급원은 <see cref="EquipmentController"/>다. 핫바는 없다 — 들고 들어간 것만 쓴다.
/// 개체 상태는 ItemInstance에 있으므로 오브젝트를 부수고 다시 만들어도 유지된다.
/// </summary>
[RequireAgentComponent(typeof(EquipmentController))]
public class PlayerHandController : MonoBehaviour, IAgentComponent
{
    [Serializable]
    private struct ArmMount
    {
        public EquipSlotType slot;

    }

    [Tooltip("이 기체가 가진 팔 슬롯. 붙을 자리는 코어 소켓이 정한다.")]
    [SerializeField] private ArmMount[] _mounts =
    {
        new() { slot = EquipSlotType.ArmLeft },
        new() { slot = EquipSlotType.ArmRight },
    };

    /// <summary>(슬롯, 새로 달린 것). 떼어내면 두 번째가 null.</summary>
    public event Action<EquipSlotType, HandActionBase> OnHandChangedEvent;

    private Player _player;
    private EquipmentController _equipment;
    private MechaAssembler _assembler;

    // _mounts와 같은 순서다. 슬롯으로 찾는 비용을 피하려고 인덱스를 맞춰 둔다.
    private HandActionBase[] _hands;
    private uint[] _itemIds;
    private uint[] _instanceIds;

    public HandActionBase Get(EquipSlotType slot)
    {
        int index = IndexOf(slot);

        return index >= 0 ? _hands[index] : null;
    }

    public HandActionBase Left => Get(EquipSlotType.ArmLeft);
    public HandActionBase Right => Get(EquipSlotType.ArmRight);

    public void Initialize(Agent owner)
    {
        _player = owner as Player;

        _hands = new HandActionBase[_mounts.Length];
        _itemIds = new uint[_mounts.Length];
        _instanceIds = new uint[_mounts.Length];
    }

    public void AfterInitialize()
    {
        _equipment = _player.GetCompo<EquipmentController>();
        _assembler = _player.GetCompo<MechaAssembler>();

        if (_equipment != null)
            _equipment.OnEquipChangedEvent += HandleEquipChanged;

        // 코어가 바뀌면 소켓도 바뀐다. 달려 있던 무기를 새 자리로 옮겨야 한다.
        if (_assembler != null)
            _assembler.OnAssembledEvent += HandleAssembled;

        if (_player.Input != null)
        {
            _player.Input.OnPrimaryEvent += HandlePrimary;
            _player.Input.OnSecondaryEvent += HandleSecondary;
        }

        UIInputBlocker.OnBlockedChangedEvent += HandleInputBlocked;

        // 구독보다 먼저 장착된 것이 있으면 이벤트를 놓친다. 현재 상태를 한 번 훑는다.
        RefreshAll();
    }

    public void Dispose()
    {
        if (_equipment != null)
        {
            _equipment.OnEquipChangedEvent -= HandleEquipChanged;
            _equipment = null;
        }

        if (_assembler != null)
        {
            _assembler.OnAssembledEvent -= HandleAssembled;
            _assembler = null;
        }

        if (_player != null && _player.Input != null)
        {
            _player.Input.OnPrimaryEvent -= HandlePrimary;
            _player.Input.OnSecondaryEvent -= HandleSecondary;
        }

        UIInputBlocker.OnBlockedChangedEvent -= HandleInputBlocked;

        for (int i = 0; i < _mounts.Length; i++)
            DestroyHand(i);
    }

    private int IndexOf(EquipSlotType slot)
    {
        for (int i = 0; i < _mounts.Length; i++)
        {
            if (_mounts[i].slot == slot) return i;
        }

        return -1;
    }

    #region 손 만들기

    private void RefreshAll()
    {
        if (_equipment == null) return;

        for (int i = 0; i < _mounts.Length; i++)
            Refresh(i, _equipment.GetStack(_mounts[i].slot));
    }

    private void HandleEquipChanged(EquipSlotType slot, ItemStack stack)
    {
        int index = IndexOf(slot);
        if (index < 0) return;

        Refresh(index, stack);
    }

    private void Refresh(int index, ItemStack stack)
    {
        HandActionBase prefab = ResolvePrefab(stack);

        if (prefab == null)
        {
            DestroyHand(index);
            return;
        }

        // 같은 개체를 다시 받은 것이면 그대로 둔다. 매번 부수면 열·차지 같은 게 날아간다.
        if (_hands[index] != null
            && _itemIds[index] == stack.itemId
            && _instanceIds[index] == stack.instanceId)
        {
            return;
        }

        DestroyHand(index);
        Create(index, prefab, stack);
    }

    /// <summary>이 아이템을 달면 무엇이 만들어지는가. 달 수 없으면 null.</summary>
    private HandActionBase ResolvePrefab(ItemStack stack)
    {
        ItemDataSO data = stack.Resolve();
        if (data == null) return null;

        return data is IHandItemData handItem ? handItem.HandPrefab : null;
    }

    /// <summary>
    /// 이 팔이 달릴 자리. 조립된 코어의 소켓이 먼저고, 없으면 인스펙터 폴백이다.
    /// 코어 프리팹이 아직 없는 동안에도 손으로 만든 기체에 무기가 달려야 한다.
    /// </summary>
    private Transform ResolveRoot(int index)
    {
        if (_assembler == null) return null;

        MechaPartView core = _assembler.Core;

        return core != null ? core.GetSocket(_mounts[index].slot) : null;
    }

    /// <summary>코어가 바뀌어 소켓이 옮겨갔다. 달려 있는 것을 새 자리로 옮긴다.</summary>
    private void HandleAssembled()
    {
        for (int i = 0; i < _hands.Length; i++)
        {
            if (_hands[i] == null) continue;

            Transform root = ResolveRoot(i);
            if (root == null || _hands[i].transform.parent == root) continue;

            _hands[i].transform.SetParent(root, false);
            _hands[i].transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }
    }

    private void Create(int index, HandActionBase prefab, ItemStack stack)
    {
        Transform root = ResolveRoot(index);
        if (root == null)
        {
            Debug.LogError($"[Hand] {_mounts[index].slot}을 붙일 자리가 없습니다. " +
                           "코어를 달았는지, 그 프리팹에 이 슬롯 소켓이 있는지 확인하세요.", this);
            return;
        }

        HandActionBase hand = Instantiate(prefab, root);
        hand.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

        hand.Initialize(_player, _mounts[index].slot, stack);
        hand.OnEquipped();

        _hands[index] = hand;
        _itemIds[index] = stack.itemId;
        _instanceIds[index] = stack.instanceId;

        OnHandChangedEvent?.Invoke(_mounts[index].slot, hand);
    }

    private void DestroyHand(int index)
    {
        _itemIds[index] = 0;
        _instanceIds[index] = 0;

        HandActionBase hand = _hands[index];
        if (hand == null) return;

        _hands[index] = null;

        hand.OnUnequipped();

        // 개체 상태는 건드리지 않는다. 오브젝트가 사라져도 남아야 한다.
        Destroy(hand.gameObject);

        OnHandChangedEvent?.Invoke(_mounts[index].slot, null);
    }

    #endregion

    #region 입력 배분

    private void HandlePrimary(bool pressed) => Route(EquipSlotType.ArmLeft, pressed);

    private void HandleSecondary(bool pressed) => Route(EquipSlotType.ArmRight, pressed);

    private void Route(EquipSlotType slot, bool pressed)
    {
        HandActionBase hand = Get(slot);
        if (hand == null) return;

        // 인벤토리를 열어둔 채로 누른 것은 무시한다.
        // 떼는 것은 막지 않는다 — 막힌 동안 뗀 것을 무시하면 계속 눌린 상태로 남는다.
        if (pressed && UIInputBlocker.IsBlocked) return;

        if (pressed) hand.OnUsePressed();
        else hand.OnUseReleased();
    }

    /// <summary>
    /// 막히기 시작하면 누르고 있던 것을 놓아준다.
    /// 안 그러면 연사가 인벤토리를 연 채로 계속 돌고, 차지가 걸린 채로 남는다.
    /// </summary>
    private void HandleInputBlocked(bool blocked)
    {
        if (!blocked) return;

        for (int i = 0; i < _hands.Length; i++)
        {
            if (_hands[i] != null)
                _hands[i].OnUseReleased();
        }
    }

    #endregion
}
