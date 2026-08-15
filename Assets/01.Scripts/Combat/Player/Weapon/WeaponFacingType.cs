/// <summary>
/// 무기 리소스가 그려진 방향.
///
/// 코드는 전부 <b>"+X가 앞"</b> 하나만 씁니다. 조준 피벗이 +X를 커서로 돌리고,
/// 총구 위치·파츠 장착점·발사 방향이 전부 그 규칙 위에 서 있습니다.
/// 리소스가 어느 쪽을 보고 그려졌든 그 차이는 PlayerWeaponBase가 한 번만 흡수합니다.
/// </summary>
public enum WeaponFacingType
{
    /// <summary>총구가 오른쪽(+X)을 향하게 그려진 리소스.</summary>
    Right = 0,

    /// <summary>총구가 왼쪽(-X)을 향하게 그려진 리소스. 통째로 X 미러링된다.</summary>
    Left = 1,
}
