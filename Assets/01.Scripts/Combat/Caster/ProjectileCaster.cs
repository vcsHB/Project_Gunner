using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 날아가는 동안 지나온 경로를 훑는 캐스터.
///
/// 원형 질의만 하면 빠른 탄이 한 프레임에 적을 통째로 지나쳐 버린다(터널링).
/// 그래서 직전 위치에서 현재 위치까지를 CircleCast로 쓸어 담는다.
///
/// 관통 처리도 여기 있다. CasterBase의 중복 제거는 한 번의 Cast 안에서만 유효해서,
/// 프레임을 넘겨 같은 적을 다시 때리는 것은 막아주지 못한다.
/// </summary>
public class ProjectileCaster : CasterBase
{
    [Tooltip("탄의 굵기. 0에 가까울수록 정확하지만 빠른 탄이 얇은 적을 스쳐 지나가기 쉬워진다.")]
    [SerializeField] private float _radius = 0.08f;

    private readonly RaycastHit2D[] _sweepBuffer = new RaycastHit2D[MaxTargetCount];

    // 이미 맞힌 대상. 관통탄이 같은 적을 여러 프레임에 걸쳐 때리는 것을 막는다.
    private readonly HashSet<TargetBase> _hitTargets = new();

    private Vector2 _previousPosition;

    public float Radius
    {
        get => _radius;
        set => _radius = Mathf.Max(0f, value);
    }

    /// <summary>
    /// 발사 직후에 부른다. 풀에서 재사용될 때 이전 판의 경로와 명중 기록이 남아있으면
    /// 첫 프레임에 엉뚱한 곳을 훑거나, 이미 맞힌 적을 영영 못 때리게 된다.
    /// </summary>
    public void ResetTrail()
    {
        _previousPosition = transform.position;
        _hitTargets.Clear();
        Power = null;
    }

    protected override int CastTarget(ContactFilter2D filter, Collider2D[] buffer)
    {
        Vector2 current = transform.position;
        Vector2 delta = current - _previousPosition;
        float distance = delta.magnitude;

        _previousPosition = current;

        // 아직 움직이지 않았으면(발사 첫 프레임) 제자리 원형 질의로 대신한다.
        if (distance <= Mathf.Epsilon)
            return Physics2D.OverlapCircle(current, _radius, filter, buffer);

        int count = Physics2D.CircleCast(
            current - delta, _radius, delta / distance, filter, _sweepBuffer, distance);

        count = Mathf.Min(count, buffer.Length);
        for (int i = 0; i < count; i++)
            buffer[i] = _sweepBuffer[i].collider;

        return count;
    }

    protected override bool CanAffect(TargetBase target) => !_hitTargets.Contains(target);

    protected override void AfterCast(IReadOnlyList<CastHit> hits)
    {
        for (int i = 0; i < hits.Count; i++)
            _hitTargets.Add(hits[i].Target);
    }
}
