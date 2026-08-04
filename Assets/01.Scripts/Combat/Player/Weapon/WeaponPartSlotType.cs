/// <summary>
/// 무기 모딩 슬롯. 파츠 호환은 이 슬롯과 PlayerWeaponCategory로 명시한다.
/// (스탯 보유 여부로 추론하지 않는다 — 스코프처럼 특정 무기에만 끼는 것들이 있다)
/// </summary>
public enum WeaponPartSlotType
{
    None = 0,
    Scope,
    Barrel,
    Muzzle,
    Magazine,
    Grip,
    Stock,
}
