using UnityEngine;

/// <summary>
/// 사정권이다. 멈춰서 공격한다. <b>무장이 있으면 쏘고, 없으면 때린다.</b>
///
/// | | 사격 (메카) | 근접 (비메카) |
/// |---|---|---|
/// | 속도를 정하는 것 | 무기 쿨타임 + 과열 | AI의 선딜·쿨타임 |
/// | 판정 | 투사체 | <see cref="CasterBase"/> |
///
/// **사격 속도를 AI가 재지 않는 이유** — 같은 무기가 플레이어 손에서와 적 손에서
/// 다른 속도로 나가면 안 된다. 트리거만 당겨두고 나머지는 무기에 맡긴다.
///
/// **근접에 선딜을 두는 이유** — 쿨타임만 두고 즉시 때리면 반응할 구간이 없어서
/// "붙으면 무조건 맞는" 적이 된다. 선딜이 끝난 시점의 자리에서 판정하므로,
/// 그 사이 빠져나갔으면 안 맞는다. 회피가 성립해야 한다.
/// </summary>
public class EnemyAttackState : EnemyState
{
    private float _windupLeft;
    private float _cooldownLeft;
    private bool _swinging;

    public EnemyAttackState(EnemyAIController ai) : base(ai) { }

    public override void Enter()
    {
        Ai.Move.Stop();

        _swinging = false;

        // 들어오자마자 때리지 않는다. 붙는 순간 피해를 입으면 접근을 피할 방법이 없다.
        _cooldownLeft = Mathf.Max(_cooldownLeft, Ai.AttackWindup);
    }

    public override void Exit()
    {
        _swinging = false;
        _windupLeft = 0f;

        // 트리거를 놓지 않으면 사정권을 벗어난 뒤에도 계속 쏜다.
        Ai.StopFiring();
    }

    public override void Tick(float deltaTime)
    {
        if (!Ai.HasTarget)
        {
            Ai.ChangeState(EnemyStateType.Idle);
            return;
        }

        Ai.Move.Stop();

        // 정밀 사격이 조준을 고정했으면 돌지 않는다 — 이 틈에 선 밖으로 빠지는 것이 회피다.
        if (!Ai.IsAimLocked)
            Ai.Move.LookAt(Ai.DirectionToTarget);

        if (Ai.IsArmed)
        {
            TickRanged(deltaTime);
            return;
        }

        TickMelee(deltaTime);
    }

    /// <summary>쏘는 쪽. 사정권 안이면 방아쇠를 민다. 발사 속도는 무기가 정한다.</summary>
    private void TickRanged(float deltaTime)
    {
        // 고정 중에는 사정권을 벗어나도 놓지 않는다. 예고한 한 발은 제 시간에 나가야 한다.
        if (Ai.DistanceToTarget > Ai.AttackLeaveRange && !Ai.IsAimLocked)
        {
            Ai.ChangeState(EnemyStateType.Chase);
            return;
        }

        // 포탑이 아직 돌고 있으면 당기지 않는다. 놓지도 않으면 연사가 엉뚱한 쪽으로 이어진다.
        Ai.SetFiring(Ai.IsAimedAtTarget, deltaTime);
    }

    private void TickMelee(float deltaTime)
    {
        // 휘두르는 중에는 놓아주지 않는다. 중간에 끊으면 선딜만 보여주고 아무 일도 안 일어난다.
        if (_swinging)
        {
            TickWindup(deltaTime);
            return;
        }

        // 사정권에서 벗어나는 거리가 들어오는 거리보다 넓다. 같으면 경계에서 떨린다.
        if (Ai.DistanceToTarget > Ai.AttackLeaveRange)
        {
            Ai.ChangeState(EnemyStateType.Chase);
            return;
        }

        if (_cooldownLeft > 0f)
        {
            _cooldownLeft -= deltaTime;
            return;
        }

        _swinging = true;
        _windupLeft = Ai.AttackWindup;

        Ai.NotifyWindup();
    }

    private void TickWindup(float deltaTime)
    {
        _windupLeft -= deltaTime;
        if (_windupLeft > 0f) return;

        _swinging = false;
        _cooldownLeft = Ai.AttackCooltime;

        Ai.PerformAttack();
    }
}
