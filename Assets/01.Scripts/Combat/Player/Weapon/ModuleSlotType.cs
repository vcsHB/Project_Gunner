/// <summary>
/// 무기 모딩 슬롯. 모듈 호환은 이 슬롯과 PlayerWeaponCategory로 명시한다.
/// (스탯 보유 여부로 추론하지 않는다 — 스코프처럼 특정 무기에만 끼는 것들이 있다)
/// </summary>
public enum ModuleSlotType
{
    None = 0,
    Scope,
    Barrel,
    Muzzle,

    // 탄창이 없어진 자리. 값(4)은 그대로 두고 뜻만 냉각재로 바꿨다 —
    // 에셋이 숫자로 저장하므로 값을 옮기면 끼워둔 모듈이 다른 슬롯으로 간다.
    Coolant,

    Grip,
    Stock,
}
