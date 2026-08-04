/// <summary>
/// 아이템 대분류. 한 아이템은 하나만 가진다.
/// 사용처 판정(장착 가능 여부 등)에는 쓰지 않는다. 그건 ItemDataSO 파생 타입으로 구분한다.
/// 도감/인벤토리의 정렬·필터 같은 표시 목적에만 쓸 것.
/// </summary>
public enum ItemCategoryType
{
    // 값을 명시한다. 중간에 끼워 넣어도 이미 저장된 에셋이 밀리지 않게.
    None = 0,
    Resources = 1,
    UseableItem = 2,
    PlayerWeapon = 3,
    PlayerWeaponPart = 4,
    Unit = 5,
}
