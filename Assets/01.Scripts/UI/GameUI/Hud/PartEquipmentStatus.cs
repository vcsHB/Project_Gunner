using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 지금 든 무기와 잔탄을 보여주는 HUD 조각.
/// 원거리 무기를 들었을 때만 뜨고, 맨손이거나 근접이면 숨는다.
/// </summary>
public class PartEquipmentStatus : WeaponPanelBase
{
    [SerializeField] private Image _weaponPreview;
    [SerializeField] private TextMeshProUGUI _textCurrentAmmo;
    [SerializeField] private TextMeshProUGUI _textAmmoType;

    [Tooltip("탄약을 쓰지 않는 무기일 때 잔탄 자리에 표시할 문자열.")]
    [SerializeField] private string _noAmmoText = "-";

    protected override void OnWeaponAttached(PlayerRangedWeapon weapon)
    {
        weapon.Ammo.OnAmmoChangedEvent += HandleAmmoChanged;

        SetPreview(weapon.Data);
        RefreshAmmoText();

        Show();
    }

    protected override void OnWeaponDetached(PlayerRangedWeapon weapon)
    {
        weapon.Ammo.OnAmmoChangedEvent -= HandleAmmoChanged;
    }

    protected override void OnWeaponCleared()
    {
        SetPreview(null);
        Hide();
    }

    private void SetPreview(PlayerWeaponDataSO data)
    {
        if (_weaponPreview == null) return;

        _weaponPreview.sprite = data != null ? data.IconSprite : null;

        // 스프라이트가 없는데 Image를 켜두면 흰 사각형이 남는다.
        _weaponPreview.enabled = _weaponPreview.sprite != null;
    }

    private void HandleAmmoChanged(AmmoDataSO ammo, int count) => RefreshAmmoText();

    // 탄종 이름이 언어를 탄다. 잔탄 숫자는 안 타지만 같이 다시 그려도 비용이 없다.
    protected override void RefreshLocalization() => RefreshAmmoText();

    /// <summary>
    /// 탄창이 없으므로 "X/Y"가 아니라 <b>적재함 재고 하나</b>를 보여준다.
    /// 분모를 억지로 만들면(최대 적재량 등) 플레이어가 재장전을 기대하게 된다.
    /// </summary>
    private void RefreshAmmoText()
    {
        WeaponAmmoController ammo = Ammo;

        SetText(_textCurrentAmmo, ammo != null && ammo.UsesAmmo
            ? ammo.Loaded.ToString()
            : _noAmmoText);

        AmmoDataSO loaded = ammo != null ? ammo.LoadedAmmo : null;
        SetText(_textAmmoType, loaded != null ? loaded.DisplayName : string.Empty);
    }

    private static void SetText(TextMeshProUGUI target, string value)
    {
        if (target == null) return;

        target.text = value;
    }
}
