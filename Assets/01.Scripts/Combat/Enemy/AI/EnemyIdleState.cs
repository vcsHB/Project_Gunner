/// <summary>
/// 대상이 없거나 탐지 범위 밖이다. 서 있는다.
///
/// 순찰은 없다. 지금 넣으면 "안 쫓아오는 게 버그인지 순찰 중인지" 구별이 안 돼서
/// 추격을 검증할 수 없다. (docs/MILESTONE.md M1 범위 밖)
/// </summary>
public class EnemyIdleState : EnemyState
{
    public EnemyIdleState(EnemyAIController ai) : base(ai) { }

    public override void Enter() => Ai.Move.Stop();

    public override void Tick(float deltaTime)
    {
        if (!Ai.HasTarget) return;

        if (Ai.DistanceToTarget <= Ai.DetectRange)
            Ai.ChangeState(EnemyStateType.Chase);
    }
}
