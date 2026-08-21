using UnityEngine;

/// <summary>
/// "지금 손에 든 무기"를 따라가는 UI 패널의 공통 베이스.
///
/// 무기는 핫바를 넘길 때마다 <b>파괴되고 다시 만들어집니다.</b>
/// 그래서 무기에 직접 건 구독은 매번 갈아끼워야 하고, 한 번이라도 빠뜨리면
/// 죽은 무기의 이벤트를 붙들고 있거나 새 무기의 갱신을 못 받습니다.
/// 그 갈아끼우기를 여기 한 곳에 모읍니다.
///
/// 파생 클래스는 <see cref="OnWeaponAttached"/>/<see cref="OnWeaponDetached"/> 짝만 채우면 됩니다.
/// </summary>
public abstract class WeaponPanelBase : UIPanelBase
{
    private PlayerHandController _weaponController;

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

        _weaponController.OnHandChangedEvent += HandleWeaponChanged;
        HandleWeaponChanged(_weaponController.Current);
    }

    public void Unbind()
    {
        if (_weaponController != null)
        {
            _weaponController.OnHandChangedEvent -= HandleWeaponChanged;
            _weaponController = null;
        }

        HandleWeaponChanged(null);
    }

    protected virtual void OnDestroy() => Unbind();

    private void HandleWeaponChanged(HandActionBase hand)
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
