using System.Collections.Generic;

/// <summary>
/// 무기 한 정이 인벤토리에 들어가 있어도 유지되어야 하는 상태.
/// 장착 중에는 PlayerWeaponBase가 진짜 상태를 들고 있고, 바뀔 때마다 여기로 옮겨 적는다.
/// </summary>
public class WeaponItemInstance : ItemInstance
{
    private readonly Dictionary<WeaponPartSlotType, WeaponPartDataSO> _parts = new();
    private readonly Dictionary<WeaponUpgradeSO, int> _upgrades = new();

    public IReadOnlyDictionary<WeaponPartSlotType, WeaponPartDataSO> Parts => _parts;
    public IReadOnlyDictionary<WeaponUpgradeSO, int> Upgrades => _upgrades;

    /// <summary>장착 중인 무기의 현재 상태를 그대로 옮겨 적는다.</summary>
    public void CaptureFrom(PlayerWeaponBase weapon)
    {
        if (weapon == null || weapon.Data == null) return;

        _parts.Clear();
        foreach (KeyValuePair<WeaponPartSlotType, WeaponPartDataSO> pair in weapon.Mods.Parts)
            _parts[pair.Key] = pair.Value;

        _upgrades.Clear();
        IReadOnlyList<WeaponUpgradeSO> allowed = weapon.Data.AllowedUpgrades;
        for (int i = 0; i < allowed.Count; i++)
        {
            int level = weapon.Upgrades.GetLevel(allowed[i]);
            if (level > 0)
                _upgrades[allowed[i]] = level;
        }
    }

    /// <summary>새로 만든 무기 오브젝트에 저장된 상태를 되돌린다.</summary>
    public void ApplyTo(PlayerWeaponBase weapon)
    {
        if (weapon == null) return;

        foreach (KeyValuePair<WeaponUpgradeSO, int> pair in _upgrades)
            weapon.Upgrades.SetLevel(pair.Key, pair.Value);

        foreach (KeyValuePair<WeaponPartSlotType, WeaponPartDataSO> pair in _parts)
            weapon.Mods.Attach(pair.Value);

        // 파츠가 열어주는 발사 모드가 있으므로 마지막에 한 번 정리한다.
        weapon.ValidateFireMode();
    }
}
