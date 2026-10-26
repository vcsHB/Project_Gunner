/// <summary>
/// 기체 하드포인트. 어떤 아이템이 어느 슬롯에 들어가는지는
/// EquipableItemDataSO.AllowedSlots가 정한다.
///
/// 기체는 코어 / 좌팔 / 우팔 / 추진체 네 파츠로 구성된다.
/// 더 세분된 것(적재 용량, 전력량 등)은 슬롯을 늘리지 않고 파츠의 하위 모듈로 내려간다.
///
/// 폐기된 값은 재사용하지 않는다 — 3(Tool) 4(Helmet) 5(Face) 7(Bagpack) 8(HeadPhone)
/// 9(Battery) 10(UtilLoadout). 새 슬롯은 뒤에 붙일 것.
/// </summary>
public enum EquipSlotType
{
    None = 0,

    /// <summary>좌클릭이 여기 달린 것을 쓴다.</summary>
    ArmLeft = 1,

    /// <summary>우클릭이 여기 달린 것을 쓴다.</summary>
    ArmRight = 2,

    /// <summary>기체 본체. 조준점과 조준 거리를 정한다.</summary>
    Core = 6,

    /// <summary>이동과 연료.</summary>
    Booster = 12,
}
