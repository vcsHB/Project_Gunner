using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무기 인스턴스 하나의 개량 현황. 무기 데이터가 허용한 개량만 걸 수 있다.
/// modifier의 origin은 WeaponUpgradeSO 자신이라, 단계를 올릴 때 그 경로 것만 걷어내고 다시 건다.
/// </summary>
public class WeaponUpgradeState
{
    public event Action<WeaponUpgradeSO, int> OnUpgradeChangedEvent; // (경로, 새 단계)

    private readonly PlayerWeaponDataSO _data;
    private readonly WeaponStatus _status;
    private readonly Dictionary<WeaponUpgradeSO, int> _levels = new();

    public WeaponUpgradeState(PlayerWeaponDataSO data, WeaponStatus status)
    {
        _data = data;
        _status = status;
    }

    public IReadOnlyList<WeaponUpgradeSO> Available => _data.AllowedUpgrades;

    public bool IsAllowed(WeaponUpgradeSO upgrade)
        => upgrade != null && _data.CanUpgrade(upgrade);

    /// <summary>걸려있지 않으면 0.</summary>
    public int GetLevel(WeaponUpgradeSO upgrade)
        => upgrade != null && _levels.TryGetValue(upgrade, out int level) ? level : 0;

    public bool CanLevelUp(WeaponUpgradeSO upgrade)
        => IsAllowed(upgrade) && GetLevel(upgrade) < upgrade.MaxLevel;

    public bool TryLevelUp(WeaponUpgradeSO upgrade)
        => CanLevelUp(upgrade) && SetLevel(upgrade, GetLevel(upgrade) + 1);

    /// <summary>단계를 직접 지정한다. 0이면 해제. 세이브 복원에도 이걸 쓴다.</summary>
    public bool SetLevel(WeaponUpgradeSO upgrade, int level)
    {
        if (!IsAllowed(upgrade))
        {
            Debug.LogWarning($"[WeaponUpgrade] {_data.DisplayName}은 {upgrade?.UpgradeName} 개량을 할 수 없습니다.");
            return false;
        }

        level = Mathf.Clamp(level, 0, upgrade.MaxLevel);
        if (GetLevel(upgrade) == level) return true;

        // 단계는 누적이 아니라 교체다. 이전 단계 효과를 먼저 전부 걷어낸다.
        _status.RemoveModifiers(upgrade);

        if (level == 0)
            _levels.Remove(upgrade);
        else
        {
            _levels[upgrade] = level;
            ApplyLevel(upgrade, level);
        }

        OnUpgradeChangedEvent?.Invoke(upgrade, level);
        return true;
    }

    public void ResetAll()
    {
        foreach (WeaponUpgradeSO upgrade in _levels.Keys)
            _status.RemoveModifiers(upgrade);

        _levels.Clear();
    }

    private void ApplyLevel(WeaponUpgradeSO upgrade, int level)
    {
        WeaponUpgradeLevel data = upgrade.GetLevel(level);
        if (data == null) return;

        for (int i = 0; i < data.deltas.Count; i++)
        {
            WeaponStatDelta delta = data.deltas[i];
            if (!delta.IsValid) continue;

            _status.Get(delta.type)?.AddModifier(upgrade, delta.value, delta.mode);
        }
    }
}
