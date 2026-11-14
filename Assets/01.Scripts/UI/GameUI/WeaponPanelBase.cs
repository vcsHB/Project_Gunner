using UnityEngine;

/// <summary>
/// 팔 하나에 달린 무기를 따라가는 UI 패널의 공통 베이스.
///
/// 좌우 무장이 동시에 활성이라 패널도 둘입니다. <b>어느 팔을 볼지는 `_slot`이 정하고,</b>
/// 다른 팔의 변화는 여기서 걸러집니다. 그 판단이 파생 클래스로 흩어지면
/// 양쪽 패널이 같은 무기를 그리는 상태가 생깁니다.
///
/// 무기는 장비를 바꿀 때 <b>파괴되고 다시 만들어집니다.</b>
/// 그래서 무기에 직접 건 구독은 매번 갈아끼워야 하고, 한 번이라도 빠뜨리면
/// 죽은 무기의 이벤트를 붙들고 있거나 새 무기의 갱신을 못 받습니다.
/// 그 갈아끼우기를 여기 한 곳에 모읍니다.
///
/// 파생 클래스는 <see cref="OnWeaponAttached"/>/<see cref="OnWeaponDetached"/> 짝만 채우면 됩니다.
/// </summary>
public abstract class WeaponPanelBase : UIPanelBase
{
    [Tooltip("이 패널이 따라갈 팔. 좌우 무장이 동시에 활성이라 하나를 지정해야 한다.")]
    [SerializeField] private EquipSlotType _slot = EquipSlotType.ArmLeft;

    private PlayerHandController _weaponController;

    /// <summary>이 패널이 보고 있는 팔.</summary>
    protected EquipSlotType Slot => _slot;

#if UNITY_EDITOR
    /// <summary>검증용. 두 패널이 같은 팔을 보고 있지 않은지 HUD가 확인한다.</summary>
    public EquipSlotType EditorSlot => _slot;
#endif

    /// <summary>지금 든 원거리 무기. 맨손이거나 근접이면 null.</summary>
    protected PlayerRangedWeapon Weapon { get; private set; }

    protected WeaponAmmoController Ammo => Weapon != null ? Weapon.Ammo : null;

    // HUD와 같은 방식. Agent의 컴포넌트 초기화가 Awake에서 끝나므로 Start에서 붙는다.
    private void Start()
    {
        if (_weaponController == null)
            Bind(FindAnyObjectByType<Player>());
    }

    public void Bind(Player player)
    {
        Unbind();

        if (player == null) return;

        _weaponController = player.GetCompo<PlayerHandController>();
        if (_weaponController == null) return;

        _weaponController.OnHandChangedEvent += HandleHandChanged;
        Apply(_weaponController.Get(_slot));
    }

    public void Unbind()
    {
        if (_weaponController != null)
        {
            _weaponController.OnHandChangedEvent -= HandleHandChanged;
            _weaponController = null;
        }

        Apply(null);
    }

    protected virtual void OnDestroy() => Unbind();

    // 다른 팔의 변화는 무시한다. 이 판단은 여기 한 곳에만 있어야 한다.
    private void HandleHandChanged(EquipSlotType slot, HandActionBase hand)
    {
        if (slot != _slot) return;

        Apply(hand);
    }

    private void Apply(HandActionBase hand)
    {
        if (Weapon != null)
        {
            OnWeaponDetached(Weapon);
            Weapon = null;
        }

        // 근접 무기나 소모품은 PlayerRangedWeapon이 아니라 여기서 걸러진다.
        Weapon = hand as PlayerRangedWeapon;

        if (Weapon != null)
            OnWeaponAttached(Weapon);
        else
            OnWeaponCleared();
    }

    /// <summary>새 무기를 들었다. 무기에 거는 구독은 여기서 시작한다.</summary>
    protected abstract void OnWeaponAttached(PlayerRangedWeapon weapon);

    /// <summary>무기를 놓았다. OnWeaponAttached에서 건 것을 여기서 정확히 되돌린다.</summary>
    protected abstract void OnWeaponDetached(PlayerRangedWeapon weapon);

    /// <summary>들고 있는 원거리 무기가 없어졌다. 보통 패널을 숨긴다.</summary>
    protected virtual void OnWeaponCleared() { }
}
