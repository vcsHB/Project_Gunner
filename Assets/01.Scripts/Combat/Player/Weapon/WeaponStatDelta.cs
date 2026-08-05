using System;
using UnityEngine;

/// <summary>
/// 스탯 하나에 대한 변화량. 개량과 파츠가 공용으로 쓴다.
/// 값이 음수면 깎는다. (개량은 올리는 대신 무언가를 떨구는 게 기본)
/// </summary>
[Serializable]
public struct WeaponStatDelta
{
    public WeaponStatType type;
    public ModifierMode mode;

    [Tooltip("Percent 모드에서 0.2는 +20%, -0.1은 -10%.")]
    public float value;

    public bool IsValid => type != WeaponStatType.None;

    public string ToDisplayString()
    {
        string sign = value >= 0f ? "+" : string.Empty;

        return mode == ModifierMode.Percent
            ? $"{type} {sign}{value * 100f:0.#}%"
            : $"{type} {sign}{value:0.##}";
    }
}
