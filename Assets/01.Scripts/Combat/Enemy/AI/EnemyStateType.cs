/// <summary>
/// 적 AI의 상태. 상태 하나당 클래스 하나가 대응한다.
///
/// 지금은 셋뿐이라 if로도 되지만, 원거리 적·보스가 들어오면 반드시 갈아엎게 된다.
/// 상태를 나누는 비용은 지금이 가장 싸다. (docs/MILESTONE.md M1)
/// </summary>
public enum EnemyStateType
{
    /// <summary>대상이 없거나 너무 멀다. 가만히 있는다.</summary>
    Idle = 0,

    /// <summary>대상을 향해 간다.</summary>
    Chase,

    /// <summary>사정권이다. 멈춰서 때린다.</summary>
    Attack,
}
