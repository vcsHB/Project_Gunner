/// <summary>
/// 사정권 안에서 어떻게 움직이며 쏘는가. 프리팹(<see cref="EnemyAIController"/>)에 저장된다 — 값을 바꾸지 말고 뒤에 추가할 것.
/// </summary>
public enum EnemyAttackMove
{
    /// <summary>서서 쏜다. 터렛·메카.</summary>
    Stand = 0,

    /// <summary>대상 주위를 돌며 쏜다.</summary>
    Strafe = 1,

    /// <summary>
    /// 자리를 잡고 서서 노리고, 쏘고 나면 쉬는 동안 대상 둘레의 다른 자리로 옮긴다. 사격 드론.
    /// 노리고 쏘는 동안은 서 있다 — 조준선이 흔들리지 않아야 피할 수 있다.
    /// </summary>
    Reposition = 2,
}
