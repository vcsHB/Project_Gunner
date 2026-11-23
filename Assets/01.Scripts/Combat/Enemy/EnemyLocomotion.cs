/// <summary>
/// 적의 다리. AI는 방향만 넘기고, 그 방향을 어떻게 몸의 움직임으로 바꿀지는 이것이 정한다.
///
/// 값은 직렬화된다. 중간에 끼워 넣지 말고 뒤에 추가할 것.
/// </summary>
public enum EnemyLocomotion
{
    /// <summary>걷는다. 착지 불가 지역 가장자리에서 멈춘다.</summary>
    Walk = 0,

    /// <summary>난다. 착지 불가 지역을 넘는다. 판정 분류(공중)는 <see cref="TargetBase"/>가 따로 정한다.</summary>
    Fly = 1,

    /// <summary>제자리. 몸은 안 움직이고 바라보기(포탑 회전)만 한다. 터렛·부표.</summary>
    Fixed = 2,
}
