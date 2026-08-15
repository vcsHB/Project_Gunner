/// <summary>
/// 무기 스탯 종류. 모든 무기가 전부 갖고 있고, 안 쓰는 값은 그냥 참조하지 않는다.
/// 파츠와 개량이 어느 스탯을 건드릴지 지목하는 키로 쓴다.
///
/// WeaponStatus가 이 값을 배열 인덱스로 쓰므로 값이 0부터 빈틈없이 이어져야 한다.
/// 중간에 끼워 넣지 말고 뒤에 추가할 것.
/// </summary>
public enum WeaponStatType
{
    // 기본값이 조용히 Damage가 되지 않도록 0은 비워둔다.
    None = 0,

    Damage,
    AttackCooltime,
    Range,
    MaxTargetCount,
    CriticalRate,

    MagazineSize,
    ReloadTime,

    Recoil,
    Spread,
    BurstCount,

    ProjectileCount,
    ProjectileSpeed,
    Penetration,
    ChargeTime,

    ExplosionRadius,
    Knockback,

    // 점사 안에서 발과 발 사이 간격. AttackCooltime은 점사 한 묶음이 끝난 뒤의 쿨이다.
    BurstInterval,
}
