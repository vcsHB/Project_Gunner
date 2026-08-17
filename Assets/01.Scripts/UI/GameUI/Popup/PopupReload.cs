using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 재장전 진행을 보여주는 팝업. 재장전 중에만 뜬다.
///
/// 진행률을 이벤트로 받지 않고 매 프레임 읽는 이유는, 진행이 연속 값이기 때문이다.
/// 게이지 한 칸마다 이벤트를 쏘면 이벤트가 프레임 수만큼 나간다.
/// </summary>
public class PopupReload : WeaponPanelBase
{
    [SerializeField] private TextMeshProUGUI _textReloadRemainTime;
    [SerializeField] private Image _reloadGaugeFill;

    [Tooltip("남은 시간 표시 형식. {0}에 초가 들어간다.")]
    [SerializeField] private string _remainTimeFormat = "{0:0.0}s";

    protected override void Awake()
    {
        base.Awake();

        // 시작하자마자 떠 있으면 안 된다. 재장전이 시작될 때만 띄운다.
        Hide();
    }

    protected override void OnWeaponAttached(PlayerRangedWeapon weapon)
    {
        weapon.Ammo.OnReloadStateChangedEvent += HandleReloadStateChanged;

        // 무기를 바꿔 들어도 재장전은 이어지지 않는다(OnUnequipped에서 취소된다).
        // 그래도 현재 상태를 한 번 맞춰두는 편이 안전하다.
        HandleReloadStateChanged(weapon.Ammo.IsReloading);
    }

    protected override void OnWeaponDetached(PlayerRangedWeapon weapon)
    {
        weapon.Ammo.OnReloadStateChangedEvent -= HandleReloadStateChanged;
    }

    protected override void OnWeaponCleared() => Hide();

    private void HandleReloadStateChanged(bool reloading)
    {
        if (reloading)
        {
            Refresh();
            Show();
            return;
        }

        Hide();
    }

    private void Update()
    {
        if (!IsShown) return;

        Refresh();
    }

    private void Refresh()
    {
        WeaponAmmoController ammo = Ammo;
        if (ammo == null) return;

        if (_reloadGaugeFill != null)
            _reloadGaugeFill.fillAmount = ammo.ReloadProgress01;

        if (_textReloadRemainTime != null)
            _textReloadRemainTime.text = string.Format(_remainTimeFormat, ammo.ReloadRemainTime);
    }
}
