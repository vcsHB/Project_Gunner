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
public class PlayerHandController : MonoBehaviour, IAgentComponent
{
    [Serializable]
    private struct ArmMount
    {
        public EquipSlotType slot;

        [Tooltip("이 팔의 것이 생성될 위치. 코어 아래에 있어야 기체가 돌 때 같이 돈다.")]
        public Transform root;
    }

    [Tooltip("팔 슬롯과 장착 위치. 보통 ArmLeft / ArmRight 둘이다.")]
    [SerializeField] private ArmMount[] _mounts =
    {
        new() { slot = EquipSlotType.ArmLeft },
        new() { slot = EquipSlotType.ArmRight },
    };

    [Tooltip("재장전 키를 이 시간 이상 누르고 있으면 재장전 대신 탄종을 넘긴다.")]
    [SerializeField, Min(0.05f)] private float _ammoCycleHoldTime = 0.35f;

    /// <summary>(슬롯, 새로 달린 것). 떼어내면 두 번째가 null.</summary>
    public event Action<EquipSlotType, HandActionBase> OnHandChangedEvent;

    private Player _player;
    private EquipmentController _equipment;

    // _mounts와 같은 순서다. 슬롯으로 찾는 비용을 피하려고 인덱스를 맞춰 둔다.
    private HandActionBase[] _hands;
    private uint[] _itemIds;
    private uint[] _instanceIds;

    // 짧게 누름(재장전) / 길게 누름(탄종 전환) 구분.
    // PlayerInput은 눌림/뗌만 알려준다. 시간 판정은 게임 규칙이라 여기서 한다.
    private bool _reloadHeld;
    private float _reloadHeldTime;
    private bool _ammoCycleFired;

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

        for (int i = 0; i < _mounts.Length; i++)
        {
            if (_mounts[i].root == null)
                Debug.LogError($"[Hand] {_mounts[i].slot}의 장착 위치가 비어 있습니다.", this);
        }
    }

    public void AfterInitialize()
    {
        _equipment = _player.GetCompo<EquipmentController>();

        if (_equipment != null)
            _equipment.OnEquipChangedEvent += HandleEquipChanged;

        if (_player.Input != null)
        {
            _player.Input.OnPrimaryEvent += HandlePrimary;
            _player.Input.OnSecondaryEvent += HandleSecondary;
            _player.Input.OnReloadEvent += HandleReload;
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

        if (_player != null && _player.Input != null)
        {
            _player.Input.OnPrimaryEvent -= HandlePrimary;
            _player.Input.OnSecondaryEvent -= HandleSecondary;
            _player.Input.OnReloadEvent -= HandleReload;
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

    private void Create(int index, HandActionBase prefab, ItemStack stack)
    {
        Transform root = _mounts[index].root;
        if (root == null) return;

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

        _reloadHeld = false;
        _ammoCycleFired = false;

        for (int i = 0; i < _hands.Length; i++)
        {
            if (_hands[i] != null)
                _hands[i].OnUseReleased();
        }
    }

    /// <summary>
    /// 재장전은 <b>양쪽에 같이</b> 보낸다. 필요 없는 쪽은 스스로 무시한다.
    /// 키가 하나뿐이라 어느 팔인지 고를 수 없고, 고르게 만들면 키가 하나 더 필요해진다.
    ///
    /// 과열이 들어오면 이 입력은 통째로 사라진다. 그때까지만 쓰는 임시 경로다.
    /// </summary>
    private void HandleReload(bool pressed)
    {
        if (pressed && UIInputBlocker.IsBlocked) return;

        if (pressed)
        {
            _reloadHeld = true;
            _reloadHeldTime = 0f;
            _ammoCycleFired = false;
            return;
        }

        _reloadHeld = false;

        // 홀드로 이미 탄종을 넘겼으면 뗄 때 재장전까지 겹치지 않게 한다.
        if (_ammoCycleFired) return;

        for (int i = 0; i < _hands.Length; i++)
        {
            if (_hands[i] != null)
                _hands[i].OnReloadRequested();
        }
    }

    private void Update()
    {
        if (!_reloadHeld || _ammoCycleFired) return;

        _reloadHeldTime += Time.deltaTime;
        if (_reloadHeldTime < _ammoCycleHoldTime) return;

        _ammoCycleFired = true;

        for (int i = 0; i < _hands.Length; i++)
        {
            if (_hands[i] != null)
                _hands[i].OnAmmoCycleRequested(1);
        }
    }

    #endregion
}
