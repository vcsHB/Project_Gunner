using UnityEngine;

/// <summary>
/// 사정권이다. 멈춰서 때린다.
///
/// 한 번의 공격은 <b>선딜 → 타격 → 후딜</b>로 돈다. 쿨타임만 두고 즉시 때리면
/// 플레이어가 반응할 구간이 없어서 "붙으면 무조건 맞는" 적이 된다.
/// 선딜 동안에도 대상을 계속 바라본다 — 어느 쪽에서 맞는지 읽혀야 한다.
///
/// 판정은 <see cref="CasterBase"/>가 한다. 적이 직접 데미지를 계산하면
/// 방어력·치명타·상태이상이 플레이어 무기와 다르게 걸린다.
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
    }

    public override void Tick(float deltaTime)
    {
        if (!Ai.HasTarget)
        {
            Ai.ChangeState(EnemyStateType.Idle);
            return;
        }

        Ai.Move.Stop();
        Ai.Move.LookAt(Ai.DirectionToTarget);

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

        // 선딜이 끝난 시점의 자리에서 친다. 대상이 빠져나갔으면 캐스터가 아무도 못 잡는다 —
        // 그게 맞다. 회피가 성립해야 한다.
        Ai.PerformAttack();
    }
}
