using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무기에 끼우는 파츠. 대부분은 스탯만 바꾸므로 데이터만으로 끝난다.
/// 동작이 필요한 파츠(유탄 발사기 추가 등)만 behaviourPrefab을 채운다.
/// </summary>
[CreateAssetMenu(menuName = "SO/Item/WeaponPartData")]
public class WeaponPartDataSO : ItemDataSO
{
    [Header("Part Settings")]
    [SerializeField] private WeaponPartSlotType _slot = WeaponPartSlotType.None;

    [Tooltip("비워두면 모든 무기에 호환된다. 스코프처럼 특정 무기 전용이면 여기에 지정.")]
    [SerializeField] private List<PlayerWeaponCategory> _allowedCategories = new();

    [Header("Stats")]
    [SerializeField] private List<WeaponStatDelta> _deltas = new();

    [Header("Fire Mode")]
    [Tooltip("이 파츠가 추가로 열어주는 발사 모드. 점사 전환 파츠 같은 것.")]
    [SerializeField] private List<WeaponFireMode> _addFireModes = new();

    [Header("Behaviour")]
    [Tooltip("동작이 필요한 파츠만 채운다. 비어있으면 스탯만 적용된다.")]
    [SerializeField] private WeaponPartBehaviour _behaviourPrefab;

    public WeaponPartSlotType Slot => _slot;
    public IReadOnlyList<WeaponStatDelta> Deltas => _deltas;
    public IReadOnlyList<WeaponFireMode> AddFireModes => _addFireModes;
    public WeaponPartBehaviour BehaviourPrefab => _behaviourPrefab;

    public bool IsCompatible(PlayerWeaponDataSO weapon)
    {
        if (weapon == null || _slot == WeaponPartSlotType.None) return false;
        if (!weapon.HasSlot(_slot)) return false;

        // 비어있으면 카테고리 제한 없음
        return _allowedCategories.Count == 0 || _allowedCategories.Contains(weapon.Category);
    }

    /// <summary>UI에 왜 못 끼우는지 보여주기 위한 것.</summary>
    public string GetIncompatibleReason(PlayerWeaponDataSO weapon)
    {
        if (weapon == null) return "무기가 없습니다.";
        if (_slot == WeaponPartSlotType.None) return "슬롯이 지정되지 않은 파츠입니다.";
        if (!weapon.HasSlot(_slot)) return $"이 무기에는 {_slot} 슬롯이 없습니다.";

        if (_allowedCategories.Count > 0 && !_allowedCategories.Contains(weapon.Category))
            return $"{weapon.Category} 무기에는 끼울 수 없습니다.";

        return string.Empty;
    }
}
