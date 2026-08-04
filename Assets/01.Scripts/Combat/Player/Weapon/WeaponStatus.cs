using System;

/// <summary>
/// 무기 인스턴스 하나의 런타임 스탯.
/// SO는 여러 무기가 공유하므로 기본값만 읽어와 인스턴스마다 따로 만든다.
/// 파츠는 자기 자신을 origin으로 modifier를 걸고, 탈착 시 그것만 걷어간다.
/// </summary>
public class WeaponStatus
{
    private static readonly int StatCount = Enum.GetValues(typeof(WeaponStatType)).Length;

    // WeaponStatType 값을 그대로 인덱스로 쓴다. [0]은 None이라 비어있다.
    private readonly Status<float>[] _stats;

    public WeaponStatus(WeaponStatDefaults defaults)
    {
        _stats = new Status<float>[StatCount];

        for (int i = 1; i < StatCount; i++)
            _stats[i] = new Status<float>(defaults.Get((WeaponStatType)i));
    }

    #region 자주 쓰는 스탯

    public Status<float> Damage => _stats[(int)WeaponStatType.Damage];
    public Status<float> AttackCooltime => _stats[(int)WeaponStatType.AttackCooltime];
    public Status<float> Range => _stats[(int)WeaponStatType.Range];
    public Status<float> CriticalRate => _stats[(int)WeaponStatType.CriticalRate];

    #endregion

    public Status<float> Get(WeaponStatType type)
    {
        if (type == WeaponStatType.None) return null;

        return _stats[(int)type];
    }

    public float Value(WeaponStatType type) => Get(type)?.TotalValue ?? 0f;

    /// <summary>파츠 탈착용. 그 origin이 건 modifier만 전부 걷어낸다.</summary>
    public void RemoveModifiers(object origin)
    {
        for (int i = 1; i < StatCount; i++)
            _stats[i].RemoveModifier(origin);
    }
}
