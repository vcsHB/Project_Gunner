using System;
using UnityEngine;

/// <summary>
/// 핫바에서 고른 아이템이 무기면 손에 만들어 준다.
///
/// 장착 슬롯을 따로 보지 않는다. "핫바에 넣는 것 = 드는 것"이라 선택 칸만 따라가면 된다.
/// 파츠·개량 상태는 WeaponItemInstance에 있으므로 오브젝트를 부수고 다시 만들어도 유지된다.
/// 그래서 살아있는 무기 오브젝트는 한 번에 하나만 두면 충분하다.
/// </summary>
public class PlayerWeaponController : MonoBehaviour, IAgentComponent
{
    [SerializeField] private Transform _weaponHandleRoot;

    [Tooltip("재장전 키를 이 시간 이상 누르고 있으면 재장전 대신 탄종을 넘긴다.")]
    [SerializeField, Min(0.05f)] private float _ammoCycleHoldTime = 0.35f;

    public event Action<PlayerWeaponBase> OnWeaponChangedEvent;

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

    public PlayerWeaponBase Current { get; private set; }

    public void Initialize(Agent owner)
    {
        _player = owner as Player;

        if (_weaponHandleRoot == null)
            _weaponHandleRoot = transform;
    }

    public void AfterInitialize()
    {
        _hotbar = _player.GetCompo<PlayerHotbar>();

        if (_hotbar != null)
            _hotbar.OnSelectedChangedEvent += HandleSelectedChanged;

        if (_player.Input != null)
        {
            _player.Input.OnAttackEvent += HandleAttack;
            _player.Input.OnReloadEvent += HandleReload;
        }

        RefreshWeapon();
    }

    public void Dispose()
    {
        if (_hotbar != null)
            _hotbar.OnSelectedChangedEvent -= HandleSelectedChanged;

        if (_player != null && _player.Input != null)
        {
            _player.Input.OnAttackEvent -= HandleAttack;
            _player.Input.OnReloadEvent -= HandleReload;
        }

        DestroyCurrent();
    }

    private void HandleSelectedChanged(int selectedIndex) => RefreshWeapon();

    private void RefreshWeapon()
    {
        ItemStack stack = _hotbar != null ? _hotbar.SelectedStack : ItemStack.Empty;
        PlayerWeaponDataSO data = stack.Resolve<PlayerWeaponDataSO>();

        if (data == null)
        {
            DestroyCurrent();
            return;
        }

        // 같은 개체를 다시 고른 것이면 그대로 둔다. 매번 부수면 재장전 상태 같은 게 날아간다.
        if (Current != null && _currentItemId == stack.itemId && _currentInstanceId == stack.instanceId)
            return;

        DestroyCurrent();
        Create(data, stack);
    }

    private void Create(PlayerWeaponDataSO data, ItemStack stack)
    {
        if (data.playerWeaponPrefab == null)
        {
            Debug.LogError($"[PlayerWeapon] {data.DisplayName}에 프리팹이 없습니다.", data);
            return;
        }

        PlayerWeaponBase weapon = Instantiate(data.playerWeaponPrefab, _weaponHandleRoot);
        weapon.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

        weapon.Initialize(data, _player, stack.ResolveInstance<WeaponItemInstance>());
        weapon.OnEquipped();

        Current = weapon;
        _currentItemId = stack.itemId;
        _currentInstanceId = stack.instanceId;

        OnWeaponChangedEvent?.Invoke(Current);
    }

    private void DestroyCurrent()
    {
        _currentItemId = 0;
        _currentInstanceId = 0;

        if (Current == null) return;

        PlayerWeaponBase weapon = Current;
        Current = null;

        weapon.OnUnequipped();

        // 파츠를 떼지 않는다. 개체 상태는 오브젝트가 사라져도 남아야 한다.
        Destroy(weapon.gameObject);

        OnWeaponChangedEvent?.Invoke(null);
    }

    private void HandleAttack(bool pressed)
    {
        if (Current == null) return;

        if (pressed)
            Current.OnAttackPressed();
        else
            Current.OnAttackReleased();
    }

    private void HandleReload(bool pressed)
    {
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
}
