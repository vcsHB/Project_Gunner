/// <summary>
/// 플레이어 무기 공격방식에 따른 분류
/// </summary>
public enum PlayerWeaponCategory
{
    None = 0,
    Melee, // 근거리 무기
    HandGun, // 
    AutoRifle,
    SniperRifle,
    GrenadeShooter,
    Rocket,

    // 뒤에만 추가할 것. 이미 저장된 무기·파츠 에셋의 값이 밀린다.
    Shotgun,
}