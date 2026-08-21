using System;
using UnityEngine;

/// <summary>
/// 핫바에서 고른 것을 손에 만들고, 주/보조 입력을 <b>그것에게만</b> 넘긴다.
///
/// 입력의 의미는 여기서 정하지 않는다. 좌클릭이 발사인지 던지기인지, 우클릭이 정조준인지
/// 사용인지는 손에 든 것(<see cref="HandActionBase"/>)이 정한다.
/// 구독하는 쪽이 각자 "내가 나설 차례인가"를 판단하면 조건이 흩어지고,
/// 결국 둘 다 반응하거나 아무도 반응하지 않는 상태가 생긴다.
///
/// 장착 슬롯을 따로 보지 않는다. "핫바에 넣는 것 = 드는 것"이라 선택 칸만 따라가면 된다.
/// 개체 상태는 ItemInstance에 있으므로 오브젝트를 부수고 다시 만들어도 유지된다.
/// </summary>
public class PlayerHandController : MonoBehaviour, IAgentComponent
{
    [SerializeField] private Transform _handRoot;

    [Tooltip("손에 들 프리팹이 지정되지 않은 소모품이 쓸 기본 손. 없으면 그런 아이템은 쓸 수 없다.")]
    [SerializeField] private HandActionBase _defaultConsumableHand;

    [Tooltip("재장전 키를 이 시간 이상 누르고 있으면 재장전 대신 탄종을 넘긴다.")]
    [SerializeField, Min(0.05f)] private float _ammoCycleHoldTime = 0.35f;

    /// <summary>손에 든 것이 바뀌었을 때. 없으면 null.</summary>
    public event Action<HandActionBase> OnHandChangedEvent;

    private Player _player;
    private PlayerHotbar _hotbar;

    // 같은 아이템이 그대로면 다시 만들지 않기 위한 비교용
    private uint _currentItemId;
    private uint _currentInstanceId;

    // 짧게 누름(재장전) / 길게 누름(탄종 전환) 구분.
    // PlayerInput은 눌림/뗌만 알려준다. 시간 판정은 게임 규칙이라 여기서 한다.
    private bool _reloadHeld;
    private float _reloadHeldTime;
    private bool _ammoCycleFired;

    public HandActionBase Current { get; private set; }

    public void Initialize(Agent owner)
    {
        _player = owner as Player;

        if (_handRoot == null)
            _handRoot = transform;
    }

    public void AfterInitialize()
    {
        _hotbar = _player.GetCompo<PlayerHotbar>();

        if (_hotbar != null)
            _hotbar.OnSelectedChangedEvent += HandleSelectedChanged;

        if (_player.Input != null)
        {
            _player.Input.OnPrimaryEvent += HandlePrimary;
            _player.Input.OnSecondaryEvent += HandleSecondary;
            _player.Input.OnReloadEvent += HandleReload;
        }

        UIInputBlocker.OnBlockedChangedEvent += HandleInputBlocked;

        RefreshHand();
    }

    public void Dispose()
    {
        if (_hotbar != null)
            _hotbar.OnSelectedChangedEvent -= HandleSelectedChanged;

        if (_player != null && _player.Input != null)
        {
            _player.Input.OnPrimaryEvent -= HandlePrimary;
            _player.Input.OnSecondaryEvent -= HandleSecondary;
            _player.Input.OnReloadEvent -= HandleReload;
        }

        UIInputBlocker.OnBlockedChangedEvent -= HandleInputBlocked;

        DestroyCurrent();
    }

    #region 손 만들기

    private void HandleSelectedChanged(int selectedIndex) => RefreshHand();

    private void RefreshHand()
    {
        ItemStack stack = _hotbar != null ? _hotbar.SelectedStack : ItemStack.Empty;
        HandActionBase prefab = ResolvePrefab(stack);

        if (prefab == null)
        {
            DestroyCurrent();
            return;
        }

        // 같은 개체를 다시 고른 것이면 그대로 둔다. 매번 부수면 재장전 상태 같은 게 날아간다.
        if (Current != null && _currentItemId == stack.itemId && _currentInstanceId == stack.instanceId)
            return;

        DestroyCurrent();
        Create(prefab, stack);
    }

    /// <summary>
    /// 이 아이템을 들면 무엇이 만들어지는가. 손에 들 수 없는 아이템이면 null.
    /// </summary>
    private HandActionBase ResolvePrefab(ItemStack stack)
    {
        ItemDataSO data = stack.Resolve();
        if (data == null) return null;

        if (data is IHandItemData handItem && handItem.HandPrefab != null)
            return handItem.HandPrefab;

        // 프리팹을 따로 만들지 않은 소모품이 대부분이다. 그런 것은 공용 손으로 처리한다.
        return data is ConsumableDataSO ? _defaultConsumableHand : null;
    }

    private void Create(HandActionBase prefab, ItemStack stack)
    {
        HandActionBase hand = Instantiate(prefab, _handRoot);
        hand.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

        hand.Initialize(_player, stack);
        hand.OnEquipped();

        Current = hand;
        _currentItemId = stack.itemId;
        _currentInstanceId = stack.instanceId;

        OnHandChangedEvent?.Invoke(Current);
    }

    private void DestroyCurrent()
    {
        _currentItemId = 0;
        _currentInstanceId = 0;

        if (Current == null) return;

        HandActionBase hand = Current;
        Current = null;

        hand.OnUnequipped();

        // 개체 상태는 건드리지 않는다. 오브젝트가 사라져도 남아야 한다.
        Destroy(hand.gameObject);

        OnHandChangedEvent?.Invoke(null);
    }

    #endregion

    #region 입력 라우팅

    private void HandlePrimary(bool pressed) => Route(pressed, true);

    private void HandleSecondary(bool pressed) => Route(pressed, false);

    private void Route(bool pressed, bool primary)
    {
        if (Current == null) return;

        // 인벤토리를 열어둔 채로 누른 것은 무시한다.
        // 떼는 것은 막지 않는다 — 막힌 동안 뗀 것을 무시하면 계속 눌린 상태로 남는다.
        if (pressed && UIInputBlocker.IsBlocked) return;

        if (primary)
        {
            if (pressed) Current.OnPrimaryPressed();
            else Current.OnPrimaryReleased();

            return;
        }

        if (pressed) Current.OnSecondaryPressed();
        else Current.OnSecondaryReleased();
    }

    /// <summary>
    /// 막히기 시작하면 누르고 있던 것을 놓아준다.
    /// 안 그러면 연사가 인벤토리를 연 채로 계속 돌고, 차지와 정조준이 걸린 채로 남는다.
    /// </summary>
    private void HandleInputBlocked(bool blocked)
    {
        if (!blocked) return;

        _reloadHeld = false;
        _ammoCycleFired = false;

        if (Current == null) return;

        Current.OnPrimaryReleased();
        Current.OnSecondaryReleased();
    }

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
        if (_ammoCycleFired || Current == null) return;

        Current.OnReloadRequested();
    }

    private void Update()
    {
        if (!_reloadHeld || _ammoCycleFired) return;

        _reloadHeldTime += Time.deltaTime;
        if (_reloadHeldTime < _ammoCycleHoldTime) return;

        _ammoCycleFired = true;

        if (Current != null)
            Current.OnAmmoCycleRequested(1);
    }

    #endregion
}
