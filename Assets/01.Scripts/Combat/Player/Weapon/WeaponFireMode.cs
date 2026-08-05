/// <summary>
/// 발사 방식. 무기가 기본으로 가지고, 파츠가 선택지를 추가할 수 있다.
/// (연사총에 점사 모드를 붙이는 정도는 프리팹 없이 데이터만으로 처리된다)
/// </summary>
public enum WeaponFireMode
{
    None = 0,
    Single, // 단발
    Burst,  // 점사 - BurstCount 스탯만큼 나간다
    Auto,   // 연사
    Charge, // 차지 후 발사
}
