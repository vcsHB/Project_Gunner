using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 팔 하나의 상태를 보여주는 HUD 조각. <b>좌우로 두 개가 뜬다.</b>
/// 어느 팔을 볼지는 <see cref="WeaponPanelBase.Slot"/>이 정한다.
///
/// 원거리 무기를 달았을 때만 뜨고, 비었거나 근접이면 숨는다.
/// </summary>
public class PartEquipmentStatus : WeaponPanelBase
{
    [Header("무기")]
    [SerializeField] private Image _weaponPreview;
    [SerializeField] private TextMeshProUGUI _textCurrentAmmo;
    [SerializeField] private TextMeshProUGUI _textAmmoType;

    [Tooltip("탄약을 쓰지 않는 무기일 때 잔탄 자리에 표시할 문자열.")]
    [SerializeField] private string _noAmmoText = "-";

    [Header("과열")]
    [Tooltip("열 게이지. 과열로 잠기면 남은 시간이 줄어드는 것으로 바뀐다.")]
    [SerializeField] private SimpleGauge _heatGauge;

    [Tooltip("열이 오를수록 게이지가 이 색으로 간다.")]
    [SerializeField] private Image _heatFill;

    [SerializeField] private Color _heatCoolColor = new(0.45f, 0.7f, 1f);
    [SerializeField] private Color _heatHotColor = new(1f, 0.45f, 0.2f);
    [SerializeField] private Color _overheatColor = new(1f, 0.2f, 0.15f);

    [Tooltip("과열로 잠긴 동안에만 켜둘 것. 'OVERHEAT' 같은 글자나 테두리.")]
    [SerializeField] private GameObject _overheatMark;

    [Tooltip("열을 쓰지 않는 무기일 때 게이지를 통째로 숨길지. 빈 게이지가 남아 있으면 고장난 것처럼 보인다.")]
    [SerializeField] private GameObject _heatRoot;

    // 잠긴 동안에는 열이 안 변해서 이벤트가 오지 않는다. 남은 시간은 직접 세야 한다.
    private bool _locked;

    protected override void OnWeaponAttached(PlayerRangedWeapon weapon)
    {
        weapon.Ammo.OnAmmoChangedEvent += HandleAmmoChanged;

        if (weapon.Heat != null)
        {
            weapon.Heat.OnHeatChangedEvent += HandleHeatChanged;
            weapon.Heat.OnOverheatChangedEvent += HandleOverheatChanged;
        }

        SetPreview(weapon.Data);
        RefreshAmmoText();
        RefreshHeat(weapon);

        Show();
    }

    protected override void OnWeaponDetached(PlayerRangedWeapon weapon)
    {
        weapon.Ammo.OnAmmoChangedEvent -= HandleAmmoChanged;

        if (weapon.Heat != null)
        {
            weapon.Heat.OnHeatChangedEvent -= HandleHeatChanged;
            weapon.Heat.OnOverheatChangedEvent -= HandleOverheatChanged;
        }

        SetOverheat(false);
    }

    protected override void OnWeaponCleared()
    {
        SetPreview(null);
        SetOverheat(false);
        Hide();
    }

    #region 무기·탄약

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

    #endregion

    #region 과열

    /// <summary>
    /// 잠긴 동안에만 돈다. 열은 이벤트로 오지만 <b>남은 락 시간은 아무도 알려주지 않는다</b> —
    /// 열이 용량에 멈춰 있어서 변경 이벤트가 나지 않기 때문이다.
    /// </summary>
    private void Update()
    {
        if (!_locked) return;

        PlayerRangedWeapon weapon = Weapon;
        if (weapon == null || weapon.Heat == null) return;

        // 1에서 0으로 간다. 기다리면 풀린다는 것이 보여야 한다.
        SetGauge(weapon.Heat.LockRatio01, _overheatColor);
    }

    private void RefreshHeat(PlayerRangedWeapon weapon)
    {
        WeaponHeatController heat = weapon != null ? weapon.Heat : null;
        bool uses = heat != null && heat.IsEnabled;

        // 열을 쓰지 않는 무기에 빈 게이지가 남아 있으면 고장난 것처럼 보인다.
        if (_heatRoot != null)
            _heatRoot.SetActive(uses);

        SetOverheat(uses && heat.IsLocked);

        if (!uses) return;

        if (!heat.IsLocked)
            SetGauge(heat.Ratio01, Color.Lerp(_heatCoolColor, _heatHotColor, heat.Ratio01));
    }

    private void HandleHeatChanged(float heat, float capacity)
    {
        // 잠긴 동안에는 Update가 남은 시간을 그린다. 여기서 덮으면 둘이 싸운다.
        if (_locked) return;

        float ratio = capacity > 0f ? Mathf.Clamp01(heat / capacity) : 0f;

        SetGauge(ratio, Color.Lerp(_heatCoolColor, _heatHotColor, ratio));
    }

    private void HandleOverheatChanged(bool locked) => SetOverheat(locked);

    private void SetOverheat(bool locked)
    {
        _locked = locked;

        if (_overheatMark != null)
            _overheatMark.SetActive(locked);
    }

    private void SetGauge(float ratio, Color color)
    {
        if (_heatGauge != null)
            _heatGauge.UpdateProgress(ratio);

        if (_heatFill != null)
            _heatFill.color = color;
    }

    #endregion
}
