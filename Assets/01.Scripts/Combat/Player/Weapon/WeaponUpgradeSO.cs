using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[Serializable]
public class WeaponUpgradeLevel
{
    [Tooltip("이 단계에서의 최종 변화량. 아래 단계의 값을 누적하지 않는다.")]
    public List<WeaponStatDelta> deltas = new();

    public string BuildDescription()
    {
        StringBuilder builder = new();

        for (int i = 0; i < deltas.Count; i++)
        {
            if (!deltas[i].IsValid) continue;

            if (builder.Length > 0) builder.Append(", ");
            builder.Append(deltas[i].ToDisplayString());
        }

        return builder.ToString();
    }
}

/// <summary>
/// 무기 개량 경로 하나. 파츠와 달리 갈아끼우는 게 아니라 무기 자체를 단계별로 손보는 것이다.
///
/// 각 단계의 deltas는 "그 단계에서의 최종 값"이다. 누적이 아니다.
/// 2단계를 적용하면 1단계 효과는 걷히고 2단계 값만 남는다.
/// (누적으로 하면 3단계쯤에서 실제 수치가 얼마인지 아무도 모르게 된다)
///
/// 어떤 무기가 어떤 개량을 할 수 있는지는 PlayerWeaponDataSO가 정한다.
/// </summary>
[CreateAssetMenu(menuName = "SO/Item/WeaponUpgrade")]
public class WeaponUpgradeSO : ScriptableObject
{
    [SerializeField] private string _upgradeName;
    [SerializeField, TextArea] private string _description;
    [SerializeField] private List<WeaponUpgradeLevel> _levels = new();

    public string UpgradeName => string.IsNullOrEmpty(_upgradeName) ? name : _upgradeName;
    public string Description => _description;

    public int MaxLevel => _levels.Count;

    /// <summary>단계는 1부터. 범위를 벗어나면 null.</summary>
    public WeaponUpgradeLevel GetLevel(int level)
        => level >= 1 && level <= _levels.Count ? _levels[level - 1] : null;
}
