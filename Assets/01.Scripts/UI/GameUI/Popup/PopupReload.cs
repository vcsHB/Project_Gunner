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

    [Tooltip("남은 시간 문구의 로컬라이즈 키. 표에는 포맷 문자열이 들어간다.")]
    [SerializeField] private string _remainTimeKey = "ui.popupreload.remaintime";

    [Tooltip("표에 없을 때 쓸 포맷. {0}에 남은 초가 들어간다.")]
    [SerializeField] private string _remainTimeFormat = "{0:0.0}";

    // 표시 단위가 0.1초라 매 프레임 문자열을 다시 만들 이유가 없다.
    // TMP는 text에 대입할 때마다 메시를 다시 만든다.
    private int _shownTenths = -1;

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

    // 언어가 바뀌면 캐시를 버려서 다음 프레임에 새 문구로 다시 만들게 한다.
    protected override void RefreshLocalization() => _shownTenths = -1;

    private void HandleReloadStateChanged(bool reloading)
    {
        if (reloading)
        {
            _shownTenths = -1;
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

        RefreshRemainTime(ammo.ReloadRemainTime);
    }

    private void RefreshRemainTime(float remainSeconds)
    {
        if (_textReloadRemainTime == null) return;

        int tenths = Mathf.CeilToInt(remainSeconds * 10f);
        if (tenths == _shownTenths) return;

        _shownTenths = tenths;
        _textReloadRemainTime.text = Localization.Format(_remainTimeKey, _remainTimeFormat, tenths * 0.1f);
    }
}
