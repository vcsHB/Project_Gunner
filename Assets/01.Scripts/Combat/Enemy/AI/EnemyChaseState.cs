/// <summary>
/// 대상을 향해 직선으로 간다.
///
/// <b>길찾기는 없다.</b> 벽이 아직 없어서 직선으로 충분하고, 지형이 생기는 M2에서
/// 실제로 걸리는 것을 본 뒤에 붙인다. 지금 넣으면 추격이 이상할 때
/// 길찾기 문제인지 추격 문제인지 갈라낼 수 없다.
/// </summary>
public class EnemyChaseState : EnemyState
{
    public EnemyChaseState(EnemyAIController ai) : base(ai) { }

    public override void Exit() => Ai.Move.Stop();

    public override void Tick(float deltaTime)
    {
        if (!Ai.HasTarget)
        {
            Ai.ChangeState(EnemyStateType.Idle);
            return;
        }

        // 쫓는 동안 무리에 경보를 이어 준다 — 대열에 남은 동료가 따라온다.
        Ai.Squad?.Alert();

        float distance = Ai.DistanceToTarget;

        // 놓치는 거리가 탐지 거리보다 넓다. 같은 값이면 경계에서 쫓았다 멈췄다 한다.
        if (distance > Ai.LoseRange)
        {
            Ai.ChangeState(EnemyStateType.Idle);
            return;
        }

        if (distance <= Ai.AttackRange)
        {
            Ai.ChangeState(EnemyStateType.Attack);
            return;
        }

        Ai.Move.Move(Ai.DirectionToTarget);
    }
}
