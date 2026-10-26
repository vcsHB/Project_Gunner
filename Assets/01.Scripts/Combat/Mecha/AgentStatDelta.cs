using System;
using UnityEngine;

/// <summary>
/// 기체 스탯 하나에 대한 변화량. 파츠가 쓴다.
///
/// <see cref="WeaponStatDelta"/>의 쌍둥이다. 하나로 합치지 않은 이유는 건드리는 대상이
/// 다르기 때문이다 — 이쪽은 <see cref="StatusType"/>(기체), 저쪽은 WeaponStatType(무기 한 정).
/// 제네릭으로 묶으면 유니티가 직렬화하지 못한다.
///
/// <b>int 스탯(InventorySize)에는 Percent를 쓰지 마세요.</b> Status&lt;int&gt;의 modifier 값이
/// int라서 0.2 같은 비율이 0으로 잘립니다. MechaPartController가 경고를 내고 건너뜁니다.
/// </summary>
[Serializable]
public struct AgentStatDelta
{
    public StatusType type;
    public ModifierMode mode;

    [Tooltip("Percent 모드에서 0.2는 +20%, -0.1은 -10%.")]
    public float value;

    /// <summary>값이 0이면 걸 이유가 없다. StatusType에는 None이 없어서 값으로 판단한다.</summary>
    public bool IsValid => !Mathf.Approximately(value, 0f);

    public string ToDisplayString()
    {
        string sign = value >= 0f ? "+" : string.Empty;

        return mode == ModifierMode.Percent
            ? $"{type} {sign}{value * 100f:0.#}%"
            : $"{type} {sign}{value:0.##}";
    }
}
