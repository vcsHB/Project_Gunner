/// <summary>
/// 적 AI 상태 하나. <b>MonoBehaviour가 아니다</b> —
/// 상태가 컴포넌트면 적 하나에 컴포넌트가 상태 수만큼 붙고, 인스펙터가 읽을 수 없게 된다.
///
/// 상태는 <see cref="EnemyAIController"/>가 내주는 것만 쓴다. 상태가 Rigidbody나 캐스터를
/// 직접 집으면 "어디서 움직였는지"를 상태마다 찾아다녀야 한다.
/// </summary>
public abstract class EnemyState
{
    protected readonly EnemyAIController Ai;

    protected EnemyState(EnemyAIController ai)
    {
        Ai = ai;
    }

    /// <summary>이 상태로 들어왔다. 타이머 초기화는 여기서.</summary>
    public virtual void Enter() { }

    /// <summary>이 상태에서 나간다. Enter에서 켠 것을 정확히 되돌린다.</summary>
    public virtual void Exit() { }

    /// <summary>매 프레임. 상태 전환도 여기서 <see cref="EnemyAIController.ChangeState"/>로 한다.</summary>
    public abstract void Tick(float deltaTime);
}
