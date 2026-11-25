using UnityEngine;

/// <summary>
/// 대상이 없거나 탐지 범위 밖이다.
///
/// | | 하는 일 |
/// |---|---|
/// | 혼자 | 서 있는다 |
/// | 편대 대장 | 집(스폰 자리) 근처의 지점을 골라 느리게 순찰하고, 닿으면 잠깐 쉰다 |
/// | 편대 나머지 | 대장 뒤 대열 자리를 따라간다. 자리에 있으면 대장이 보는 쪽을 본다 |
///
/// 대상을 보면 쫓는다. 편대면 <b>동료가 본 것도 본 것</b>이다 — 경보(<see cref="EnemySquad.IsAlerted"/>) 중이고
/// 대상이 놓치는 거리 안이면 같이 간다.
/// </summary>
public class EnemyIdleState : EnemyState
{
    // 이 안이면 대열 자리에 닿은 것으로 본다(유닛).
    private const float SlotTolerance = 0.2f;

    // 이만큼 떨어지면 전속으로 따라잡는다(유닛). 그 안에서는 거리에 비례해 늦춘다 — 자리에서 떨지 않게.
    private const float CatchUpDistance = 1.5f;

    private const float WaypointTolerance = 0.6f;

    private Vector2 _waypoint;
    private bool _hasWaypoint;
    private float _pauseLeft;

    public EnemyIdleState(EnemyAIController ai) : base(ai) { }

    public override void Enter()
    {
        Ai.Move.Stop();

        _hasWaypoint = false;
        _pauseLeft = 0f;
    }

    public override void Tick(float deltaTime)
    {
        if (ShouldEngage())
        {
            Ai.Squad?.Alert();
            Ai.ChangeState(EnemyStateType.Chase);
            return;
        }

        EnemySquad squad = Ai.Squad;
        if (squad == null) return;

        if (squad.IsLeader(Ai)) TickPatrol(squad, deltaTime);
        else TickFollow(squad);
    }

    private bool ShouldEngage()
    {
        if (!Ai.HasTarget) return false;

        float distance = Ai.DistanceToTarget;
        if (distance <= Ai.DetectRange) return true;

        return Ai.Squad != null && Ai.Squad.IsAlerted && distance <= Ai.LoseRange;
    }

    private void TickPatrol(EnemySquad squad, float deltaTime)
    {
        if (Ai.PatrolRadius <= 0f)
        {
            Ai.Move.Stop();
            return;
        }

        if (_pauseLeft > 0f)
        {
            _pauseLeft -= deltaTime;
            Ai.Move.Stop();
            return;
        }

        if (!_hasWaypoint)
        {
            // 순찰 지점은 판정(어디서 마주치나)에 영향을 준다 — 시드 난수.
            float angle = GameRandom.Combat.Range(0f, Mathf.PI * 2f);
            float radius = Mathf.Sqrt(GameRandom.Combat.Value) * Ai.PatrolRadius;

            _waypoint = squad.Home + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            _hasWaypoint = true;
        }

        Vector2 to = _waypoint - (Vector2)Ai.transform.position;
        if (to.magnitude <= WaypointTolerance)
        {
            _hasWaypoint = false;
            _pauseLeft = Ai.RollPatrolPause();
            Ai.Move.Stop();
            return;
        }

        Ai.Move.Move(to, Ai.PatrolSpeed);
    }

    private void TickFollow(EnemySquad squad)
    {
        Vector2 slot = squad.FormationSlot(Ai, Ai.FormationSpacing);
        Vector2 to = slot - (Vector2)Ai.transform.position;
        float distance = to.magnitude;

        if (distance <= SlotTolerance)
        {
            Ai.Move.Stop();

            EnemyAIController leader = squad.Leader;
            if (leader != null) Ai.Move.LookAt(leader.Move.FacingDirection);
            return;
        }

        // 대장이 가는 만큼은 기본으로 따라 간다 — 거리만 보면 늘 한 걸음 뒤처진 자리에서 맞춰진다.
        EnemyAIController lead = squad.Leader;
        float leaderPace = lead != null && Ai.Move.MaxSpeed > 0.01f ? lead.Move.Velocity.magnitude / Ai.Move.MaxSpeed : 0f;

        Ai.Move.Move(to, Mathf.Clamp01(distance / CatchUpDistance + leaderPace));
    }
}
