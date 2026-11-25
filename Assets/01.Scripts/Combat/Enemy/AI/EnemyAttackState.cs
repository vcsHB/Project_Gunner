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
///
/// **움직이며 쏘기**(<see cref="EnemyAIController.AttackMove"/>)
///
/// | | |
/// |---|---|
/// | 서서 | 멈춰서 대상을 본다. 정밀 사격이 고정하면 돌지도 않는다 |
/// | 돌기 | 대상 주위를 돌며 쏜다 |
/// | 옮기기 | 대상 둘레의 자리로 가서 선다 → 노린다(조준선) → 쏜다 → <b>쉬는 동안 다른 자리로</b>. 편대는 자리마다 각도를 벌린다(산개) |
/// </summary>
public class EnemyAttackState : EnemyState
{
    private float _windupLeft;
    private float _cooldownLeft;
    private bool _swinging;

    // 옮기기 — 대상에서 본 자리의 각도(도)와, 거기 닿았는가.
    private float _spotAngle;
    private bool _atSpot;
    private UnitWeaponPhase _lastPhase;

    // 자리에 닿은 것으로 보는 거리(유닛).
    private const float SpotTolerance = 0.35f;

    public EnemyAttackState(EnemyAIController ai) : base(ai) { }

    public override void Enter()
    {
        Ai.Move.Stop();

        _swinging = false;

        // 지금 있는 쪽에서 시작한다 — 반대편으로 가로지르면 대상 위를 지나간다. 편대면 자리 번호만큼 벌린다.
        Vector2 fromTarget = (Vector2)Ai.transform.position - Ai.TargetPosition;
        _spotAngle = Mathf.Atan2(fromTarget.y, fromTarget.x) * Mathf.Rad2Deg;
        if (Ai.Squad != null) _spotAngle += Ai.Squad.ScatterOffset(Ai, Ai.ScatterStep);
        if (Ai.AttackMove == EnemyAttackMove.Reposition) _spotAngle = ClaimSpot(_spotAngle);

        _atSpot = false;
        _lastPhase = Ai.WeaponPhase;

        // 들어오자마자 때리지 않는다. 붙는 순간 피해를 입으면 접근을 피할 방법이 없다.
        _cooldownLeft = Mathf.Max(_cooldownLeft, Ai.AttackWindup);
    }

    public override void Exit()
    {
        AttackSpotRegistry.Release(Ai);

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

        Ai.Squad?.Alert();

        EnemyAttackMove move = Ai.IsArmed ? Ai.AttackMove : EnemyAttackMove.Stand;
        bool ready = true;

        switch (move)
        {
            // 도는 적은 서지 않고 대상 주위를 돌며 대상을 본다.
            case EnemyAttackMove.Strafe:
                Ai.Move.Move(Ai.StrafeDirection);
                Ai.Move.LookAt(Ai.DirectionToTarget, true);
                break;

            case EnemyAttackMove.Reposition:
                ready = TickReposition();
                break;

            default:
                StandAndFace();
                break;
        }

        if (Ai.IsArmed)
        {
            TickRanged(deltaTime, ready);
            return;
        }

        TickMelee(deltaTime);
    }

    private void StandAndFace()
    {
        Ai.Move.Stop();

        // 정밀 사격이 조준을 고정했으면 돌지 않는다 — 이 틈에 선 밖으로 빠지는 것이 회피다.
        if (!Ai.IsAimLocked)
            Ai.Move.LookAt(Ai.DirectionToTarget);
    }

    /// <summary>
    /// 옮기기. 노리고 쏘는 동안은 선다. 쉬기 시작하면 다음 자리를 굴리고, 쉬는 동안 거기로 간다.
    /// 반환 — 방아쇠를 당겨도 되는가(자리에 닿았거나 이미 노리는 중).
    /// </summary>
    private bool TickReposition()
    {
        UnitWeaponPhase phase = Ai.WeaponPhase;
        bool busy = phase == UnitWeaponPhase.Charging || phase == UnitWeaponPhase.Locked || phase == UnitWeaponPhase.Firing;

        // 막 다 쏘고 쉬기 시작했다 — 다음 자리.
        if (phase == UnitWeaponPhase.Resting && _lastPhase != UnitWeaponPhase.Resting)
        {
            _spotAngle += Ai.RollRepositionTurn();
            _spotAngle = ClaimSpot(_spotAngle);
            _atSpot = false;
        }

        _lastPhase = phase;

        if (busy)
        {
            StandAndFace();
            return true;
        }

        float rad = _spotAngle * Mathf.Deg2Rad;
        Vector2 spot = Ai.TargetPosition + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * Ai.HoldDistance;
        Vector2 to = spot - (Vector2)Ai.transform.position;

        if (to.magnitude > SpotTolerance)
        {
            _atSpot = false;
            Ai.Move.Move(to);
            Ai.Move.LookAt(Ai.DirectionToTarget, true);
            return false;
        }

        _atSpot = true;
        StandAndFace();
        return _atSpot;
    }

    // 같은 대상을 노리는 다른 적(편대가 달라도)과 자리가 겹치지 않게 예약한다.
    private float ClaimSpot(float desired)
        => AttackSpotRegistry.Claim(Ai, Ai.AimTarget, desired, Ai.HoldDistance, Ai.SpotSpacing);

    /// <summary>쏘는 쪽. 사정권 안이면 방아쇠를 민다. 발사 속도는 무기가 정한다.</summary>
    private void TickRanged(float deltaTime, bool ready)
    {
        // 고정 중에는 사정권을 벗어나도 놓지 않는다. 예고한 한 발은 제 시간에 나가야 한다.
        if (Ai.DistanceToTarget > Ai.AttackLeaveRange && !Ai.IsAimLocked)
        {
            Ai.ChangeState(EnemyStateType.Chase);
            return;
        }

        // 포탑이 아직 돌고 있으면 당기지 않는다. 놓지도 않으면 연사가 엉뚱한 쪽으로 이어진다.
        // 옮기는 중이면(자리에 아직 안 닿음) 새로 노리지 않는다 — 틱은 계속 줘야 쉼·반응 시간이 흐른다.
        Ai.SetFiring(ready && Ai.IsAimedAtTarget, deltaTime);
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
