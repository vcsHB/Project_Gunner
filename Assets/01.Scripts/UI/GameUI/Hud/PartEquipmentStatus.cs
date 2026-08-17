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

    private void RefreshAmmoText()
    {
        WeaponAmmoController ammo = Ammo;

        SetText(_textCurrentAmmo, ammo != null && ammo.UsesAmmo
            ? $"{ammo.Loaded}/{ammo.MagazineSize}"
            : _noAmmoText);

        // 탄창이 비면 탄종도 없다. 빈 탄창에 탄종만 남아 있으면 UI가 거짓말을 한다.
        AmmoDataSO loaded = ammo != null ? ammo.LoadedAmmo : null;
        SetText(_textAmmoType, loaded != null ? loaded.DisplayName : string.Empty);
    }

    private static void SetText(TextMeshProUGUI target, string value)
    {
        if (target == null) return;

        target.text = value;
    }
}
